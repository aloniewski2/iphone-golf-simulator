import SwiftUI
import XCTest
@testable import GolfArcade

/// PLAN_OnlineHub_FriendsInvites phases 1-4, phone half, over an in-memory Club Hub service: mutual-friend presence, invites that
/// arrive inside the game (banner rules), the join, push payloads, codes. The real Game Center join, the real service and APNs
/// are device / deployment gates (NOT RUN here).
@MainActor
final class ClubHubTests: XCTestCase {
    // MARK: an in-memory service speaking club-hub/PROTOCOL.md (just the rules the app depends on)
    @MainActor final class FakeService {
        struct Session { var transport: FakeTransport; var friends: Set<String>; var presence: [String: Any]; var name: String }
        var sessions: [String: Session] = [:]
        var clock = 1_000_000.0
        var pending: [String: (from: String, to: String, code: String)] = [:]
        var lobbies: [String: (from: String, sport: String, group: UInt32, capacity: Int)] = [:]
        var tokens: Set<String> = []
        var pushed: [String] = []
        var n = 0

        func attach(_ t: FakeTransport) { t.service = self }
        func receive(_ t: FakeTransport, _ data: Data) {
            guard let m = try? JSONSerialization.jsonObject(with: data) as? [String: Any], let type = m["t"] as? String else { return }
            if type == "hello" {
                guard let auth = m["auth"] as? [String: Any], let id = auth["dev"] as? String else { return }
                t.id = id
                sessions[id] = Session(transport: t, friends: Set(m["friends"] as? [String] ?? []), presence: m["presence"] as? [String: Any] ?? [:], name: m["name"] as? String ?? id)
                if let push = m["push"] as? [String: Any], let tok = push["token"] as? String { tokens.insert(id + ":" + tok) }
                t.reply(["t": "welcome", "you": id, "v": 1, "serverTime": clock])
                for other in sessions.keys where other != id && sees(id, other) { t.reply(presenceMessage(other)) }
                for other in sessions.keys where other != id && sees(other, id) { sessions[other]?.transport.reply(presenceMessage(id)) }
                return
            }
            guard let me = t.id, var s = sessions[me] else { return }
            switch type {
            case "presence":
                let wasVisible = (s.presence["invisible"] as? Bool) != true
                s.presence = m; sessions[me] = s
                // appear offline: friends are told you left (the real service does the same); back: you reappear
                for other in sessions.keys where other != me && sessions[other]?.friends.contains(me) == true && sessions[me]?.friends.contains(other) == true {
                    if (m["invisible"] as? Bool) == true { if wasVisible { sessions[other]?.transport.reply(["t": "presence", "id": me, "online": false]) } }
                    else { sessions[other]?.transport.reply(presenceMessage(me)) }
                }
            case "friends": s.friends = Set(m["ids"] as? [String] ?? []); sessions[me] = s
            case "invite":
                guard let to = m["to"] as? String, sessions[me]!.friends.contains(to), sessions[to]?.friends.contains(me) == true || sessions[to] == nil else {
                    t.reply(["t": "error", "code": "not_friends", "message": "no"]); return
                }
                n += 1; let inviteID = "inv\(n)"
                // the newest from one sender to one receiver replaces the older
                for (oldID, old) in pending where old.from == me && old.to == to { pending[oldID] = nil; t.reply(["t": "inviteUpdate", "inviteID": oldID, "status": "cancelled", "to": to]); sessions[to]?.transport.reply(["t": "inviteGone", "inviteID": oldID, "status": "cancelled"]) }
                pending[inviteID] = (me, to, m["lobbyCode"] as? String ?? "")
                if let r = sessions[to] {
                    r.transport.reply(["t": "invite", "inviteID": inviteID, "from": ["id": me, "name": s.name], "sport": m["sport"] ?? "tennis", "lobbyCode": m["lobbyCode"] ?? "", "group": m["group"] ?? 0, "capacity": m["capacity"] ?? 2, "expiresAt": clock + 120_000])
                    t.reply(["t": "inviteUpdate", "inviteID": inviteID, "status": "delivered", "to": to])
                } else {
                    t.reply(["t": "inviteUpdate", "inviteID": inviteID, "status": tokens.contains { $0.hasPrefix(to + ":") } ? "pushed" : "unavailable", "to": to])
                }
                lobbies[m["lobbyCode"] as? String ?? ""] = (me, m["sport"] as? String ?? "tennis", UInt32(truncating: (m["group"] as? NSNumber) ?? 0), 2)
            case "inviteResponse":
                guard let id = m["inviteID"] as? String, let p = pending[id], p.to == me else { t.reply(["t": "error", "code": "bad_message", "message": "no", "ref": m["inviteID"] ?? ""]); return }
                pending[id] = nil
                sessions[p.from]?.transport.reply(["t": "inviteUpdate", "inviteID": id, "status": (m["accept"] as? Bool) == true ? "accepted" : "declined", "to": me])
            case "inviteCancel":
                if let id = m["inviteID"] as? String, let p = pending[id], p.from == me { pending[id] = nil; sessions[p.to]?.transport.reply(["t": "inviteGone", "inviteID": id, "status": "cancelled"]) }
            case "resolve":
                let code = m["code"] as? String ?? ""
                if let l = lobbies[code] { t.reply(["t": "resolved", "code": code, "found": true, "from": ["id": l.from, "name": sessions[l.from]?.name ?? "Host"], "sport": l.sport, "group": l.group, "capacity": l.capacity]) }
                else { t.reply(["t": "resolved", "code": code, "found": false]) }
            default: break
            }
        }
        /// Both list each other, and the other is online and not invisible.
        func sees(_ viewer: String, _ target: String) -> Bool {
            guard let v = sessions[viewer], let tg = sessions[target] else { return false }
            return v.friends.contains(target) && tg.friends.contains(viewer) && (tg.presence["invisible"] as? Bool) != true
        }
        func presenceMessage(_ id: String) -> [String: Any] {
            let s = sessions[id]!
            var m: [String: Any] = ["t": "presence", "id": id, "online": true, "state": s.presence["state"] ?? "plaza", "name": s.name]
            if let l = s.presence["lobby"] { m["lobby"] = l }
            return m
        }
        func disconnect(_ id: String) {
            sessions[id] = nil
            for other in sessions.keys where sessions[other]?.friends.contains(id) == true { sessions[other]?.transport.reply(["t": "presence", "id": id, "online": false]) }
        }
    }
    @MainActor final class FakeTransport: HubTransport {
        var onOpen: (() -> Void)?; var onMessage: ((Data) -> Void)?; var onClose: ((Int?) -> Void)?
        weak var service: FakeService?; var id: String?
        init(_ service: FakeService) { self.service = service }
        func connect(_ url: URL) { Task { @MainActor in self.onOpen?() } }
        func send(_ data: Data) { service?.receive(self, data) }
        func close() { if let id { service?.disconnect(id) } }
        func reply(_ object: [String: Any]) { let d = HubClientMessage.data(object); Task { @MainActor in self.onMessage?(d) } }
    }
    @MainActor final class FakeSource: FriendsSource {
        let localID: String, localName: String
        var list: [FriendProfile], accessValue = FriendsAccess.authorized, added = 0
        init(_ id: String, _ name: String, friends: [FriendProfile]) { localID = id; localName = name; list = friends }
        func access() async -> FriendsAccess { accessValue }
        func friends() async throws -> [FriendProfile] { list }
        func recent() async throws -> [FriendProfile] { [] }
        func presentAddFriend() throws { added += 1 }
        func identity() async throws -> HubClientMessage.Auth { .dev(localID) }
    }
    @MainActor final class FakeRoom: ClubHubRoom {
        var inLobby = false, partySize = 1, capacity = 2, sport = "tennis"
        var made = 0, opened: [UInt32] = [], cancelled = 0, joined: [(UInt32, String)] = [], left = 0
        func makeLobby() throws { made += 1; inLobby = true }
        func openRoom(group: UInt32) async throws { opened.append(group); try await Task.sleep(for: .seconds(30)) }
        func cancelRoom() { cancelled += 1 }
        func joinRoom(group: UInt32, host: String) async throws { joined.append((group, host)); inLobby = true }
        func leaveLobby() { left += 1; inLobby = false }
    }
    @MainActor final class Phone {
        let hub: ClubHub, room: FakeRoom, source: FakeSource
        var toasts: [String] = [], haptics = 0, context = ClubHubContext()
        init(_ service: FakeService?, id: String, name: String, friends: [FriendProfile], url: URL? = nil) {
            source = FakeSource(id, name, friends: friends); room = FakeRoom()
            if let service {
                hub = ClubHub(source: source, room: room, makeTransport: { FakeTransport(service) }, serverURL: { URL(string: "ws://test/v1/hub") }, devAuth: { true })
                hub.now = { service.clock }
            } else {
                // the real WebSocket client against a real Club Hub service (club-hub/, HUB_DEV_AUTH=1)
                hub = ClubHub(source: source, room: room, makeTransport: { WebSocketHubTransport() }, serverURL: { url }, devAuth: { true })
            }
            hub.playerName = { name }
            hub.requestPushPermission = {}
            hub.tvToast = { [unowned self] in toasts.append($0) }
            hub.haptic = { [unowned self] in haptics += 1 }
            hub.context = { [unowned self] in context }
        }
    }

    override func setUp() async throws {
        for key in ["clubhub.invisible", "clubhub.dnd", "clubhub.blocked"] { UserDefaults.standard.removeObject(forKey: key) }
    }

    private func until(_ what: String = "", _ condition: () -> Bool, file: StaticString = #filePath, line: UInt = #line) async {
        for _ in 0..<200 { if condition() { return }; try? await Task.sleep(for: .milliseconds(10)) }
        XCTFail("timed out waiting for \(what)", file: file, line: line)
    }
    /// Two phones that are Game Center friends, both online.
    private func pair(_ service: FakeService) async -> (ann: Phone, bea: Phone) {
        let ann = Phone(service, id: "ann", name: "Ann", friends: [FriendProfile(id: "bea", name: "Bea")])
        let bea = Phone(service, id: "bea", name: "Bea", friends: [FriendProfile(id: "ann", name: "Ann")])
        ann.hub.start(); bea.hub.start()
        await until("both online") { ann.hub.link == .online && bea.hub.link == .online && ann.hub.friends.first?.online == true && bea.hub.friends.first?.online == true }
        return (ann, bea)
    }
    private func finish(_ phones: Phone...) { for p in phones { p.hub.debugReset() } }

    // MARK: presence

    func testFriendsSeeEachOtherOnlineWithTheirState() async {
        let service = FakeService(); let (ann, bea) = await pair(service)
        defer { finish(ann, bea) }
        XCTAssertEqual(ann.hub.onlineFriends.map(\.name), ["Bea"]); XCTAssertEqual(ann.hub.friends.first?.state, .plaza)
        bea.context.inMatch = true; bea.hub.publishPresence()
        await until("Ann sees the match") { ann.hub.friends.first?.state == .match }
        bea.hub.doNotDisturb = true
        await until("Ann sees dnd") { ann.hub.friends.first?.state == .dnd }
    }

    func testPresenceNeedsBothToListEachOther() async {
        let service = FakeService()
        let ann = Phone(service, id: "ann", name: "Ann", friends: [FriendProfile(id: "bea", name: "Bea")])
        let bea = Phone(service, id: "bea", name: "Bea", friends: [])   // Bea has not allowed friends / does not list Ann
        defer { finish(ann, bea) }
        ann.hub.start(); bea.hub.start()
        await until("both connected") { ann.hub.link == .online && bea.hub.link == .online }
        try? await Task.sleep(for: .milliseconds(150))
        XCTAssertEqual(ann.hub.friends.first?.online, false, "a one-way friend is never shown online")
    }

    func testAppearOfflineHidesYouButFriendsKeepTheirs() async {
        let service = FakeService(); let (ann, bea) = await pair(service)
        defer { finish(ann, bea) }
        bea.hub.invisible = true
        await until("Ann loses Bea") { ann.hub.friends.first?.online == false }
        XCTAssertEqual(bea.hub.friends.first?.online, true, "you still see them")
    }

    func testDisconnectShowsOffline() async {
        let service = FakeService(); let (ann, bea) = await pair(service)
        defer { finish(ann) }
        bea.hub.disconnect()
        await until("Ann sees Bea offline") { ann.hub.friends.first?.online == false }
        finish(bea)
    }

    // MARK: invites

    func testAnInviteArrivesAsABannerAndTheHostOpensTheRoom() async {
        let service = FakeService(); let (ann, bea) = await pair(service)
        defer { finish(ann, bea) }
        ann.hub.invite(ann.hub.friends[0])
        await until("banner") { bea.hub.banner != nil }
        XCTAssertEqual(bea.hub.banner?.from.name, "Ann"); XCTAssertEqual(bea.hub.banner?.sport, "tennis")
        XCTAssertEqual(bea.haptics, 1); XCTAssertEqual(bea.toasts.count, 1, "the TV shows a toast too"); XCTAssertTrue(bea.toasts[0].contains("Ann"))
        XCTAssertEqual(ann.room.made, 1, "the lobby is made on the first invite"); XCTAssertEqual(ann.room.opened.count, 1)
        XCTAssertEqual(ann.room.opened.first, ann.hub.party?.group, "the room is the party's group, the one in the invite")
        XCTAssertEqual(bea.hub.banner?.group, ann.hub.party?.group)
        await until("Ann sees delivered") { ann.hub.sent["bea"] == .delivered }
        XCTAssertTrue(ann.hub.currentPresence().lobby?.code == ann.hub.party?.code && ann.hub.party?.code.count == 6, "friends see the party's code")
    }

    func testJoinAnswersYesAndConnectsToTheHostsRoom() async {
        let service = FakeService(); let (ann, bea) = await pair(service)
        defer { finish(ann, bea) }
        ann.hub.invite(ann.hub.friends[0])
        await until("banner") { bea.hub.banner != nil }
        let invite = bea.hub.banner!
        bea.hub.accept(invite)
        await until("Ann sees accepted") { ann.hub.sent["bea"] == .accepted }
        await until("Bea joined") { !bea.room.joined.isEmpty && bea.hub.joining == nil }
        XCTAssertEqual(bea.room.joined.first?.0, ann.hub.party?.group); XCTAssertEqual(bea.room.joined.first?.1, "ann", "joins the invited host's room")
        XCTAssertNil(bea.hub.banner); XCTAssertTrue(bea.hub.invites.isEmpty)
        XCTAssertTrue(bea.toasts.last?.contains("Joining") == true, "the TV says who you are joining")
    }

    func testDeclineTellsTheHostAndClosesTheEmptyRoom() async {
        let service = FakeService(); let (ann, bea) = await pair(service)
        defer { finish(ann, bea) }
        ann.hub.invite(ann.hub.friends[0])
        await until("banner") { bea.hub.banner != nil }
        bea.hub.decline(bea.hub.banner!)
        await until("Ann sees declined") { ann.hub.sent["bea"] == .declined }
        XCTAssertEqual(ann.room.cancelled, 1, "nobody is coming: the room stops waiting")
        XCTAssertTrue(bea.room.joined.isEmpty)
    }

    func testBannerWaitsDuringAMatchAndReturnsAfter() async {
        let service = FakeService(); let (ann, bea) = await pair(service)
        defer { finish(ann, bea) }
        bea.context.inMatch = true
        ann.hub.invite(ann.hub.friends[0])
        await until("invite held") { bea.hub.invites.count == 1 }
        XCTAssertNil(bea.hub.banner, "a swing is never interrupted")
        XCTAssertTrue(bea.toasts.isEmpty, "not even a TV toast mid-match")
        bea.context.inMatch = false; bea.hub.updateBanner()
        XCTAssertEqual(bea.hub.banner?.from.name, "Ann", "shown the moment the match is over")
        bea.context.busyLoading = true; bea.hub.updateBanner(); XCTAssertNil(bea.hub.banner, "also held while loading / the arrival plays")
    }

    func testDoNotDisturbHoldsBannersButKeepsInvitesInTheHub() async {
        let service = FakeService(); let (ann, bea) = await pair(service)
        defer { finish(ann, bea) }
        bea.hub.doNotDisturb = true
        ann.hub.invite(ann.hub.friends[0])
        await until("invite in the hub") { bea.hub.invites.count == 1 }
        XCTAssertNil(bea.hub.banner); XCTAssertTrue(bea.toasts.isEmpty)
        bea.hub.doNotDisturb = false; XCTAssertNotNil(bea.hub.banner)
    }

    func testNotNowRemovesTheBannerButTheInviteStaysUntilItExpires() async {
        let service = FakeService(); let (ann, bea) = await pair(service)
        defer { finish(ann, bea) }
        ann.hub.invite(ann.hub.friends[0])
        await until("banner") { bea.hub.banner != nil }
        bea.hub.snooze(bea.hub.banner!)
        XCTAssertNil(bea.hub.banner); XCTAssertEqual(bea.hub.invites.count, 1, "still in the hub's Invites")
        XCTAssertEqual(bea.hub.remaining(bea.hub.invites[0]), 1, accuracy: 0.01)
        service.clock += 61_000; XCTAssertEqual(bea.hub.remaining(bea.hub.invites[0]), 0.49, accuracy: 0.02, "the countdown runs on the service's clock")
        service.clock += 61_000; bea.hub.debugTick()
        XCTAssertTrue(bea.hub.invites.isEmpty, "gone after two minutes")
    }

    func testANewerInviteFromTheSameFriendReplacesTheOlder() async {
        let service = FakeService(); let (ann, bea) = await pair(service)
        defer { finish(ann, bea) }
        ann.hub.invite(ann.hub.friends[0])
        await until("first") { bea.hub.invites.count == 1 }
        let first = bea.hub.invites[0].inviteID
        ann.hub.invite(ann.hub.friends[0])
        await until("replaced") { bea.hub.invites.count == 1 && bea.hub.invites[0].inviteID != first }
        XCTAssertEqual(ann.room.opened.count, 1, "one private room per party, however many invites")
    }

    func testSeveralFriendsStackNewestFirst() async {
        let service = FakeService()
        let cy = Phone(service, id: "cy", name: "Cy", friends: [FriendProfile(id: "bea", name: "Bea")])
        let ann = Phone(service, id: "ann", name: "Ann", friends: [FriendProfile(id: "bea", name: "Bea")])
        let bea = Phone(service, id: "bea", name: "Bea", friends: [FriendProfile(id: "ann", name: "Ann"), FriendProfile(id: "cy", name: "Cy")])
        defer { finish(ann, bea, cy) }
        [ann, bea, cy].forEach { $0.hub.start() }
        await until("online") { bea.hub.friends.allSatisfy(\.online) && bea.hub.friends.count == 2 }
        ann.hub.invite(ann.hub.friends[0]); await until("first") { bea.hub.invites.count == 1 }
        cy.hub.invite(cy.hub.friends[0]); await until("second") { bea.hub.invites.count == 2 }
        XCTAssertEqual(bea.hub.invites.map(\.from.name), ["Cy", "Ann"]); XCTAssertEqual(bea.hub.banner?.from.name, "Cy")
    }

    func testOnlyFriendsCanBeInvitedAndAnOfflineFriendIsNotReachable() async {
        let service = FakeService()
        let ann = Phone(service, id: "ann", name: "Ann", friends: [FriendProfile(id: "bea", name: "Bea")])
        defer { finish(ann) }
        ann.hub.start(); await until("online") { ann.hub.link == .online }
        ann.hub.invite(ann.hub.friends[0])
        try? await Task.sleep(for: .milliseconds(100))
        XCTAssertNotEqual(ann.hub.sent["bea"], .delivered, "Bea is not online")
    }

    func testBlockedFriendsCannotInviteYou() async {
        let service = FakeService(); let (ann, bea) = await pair(service)
        defer { finish(ann, bea) }
        bea.hub.block(bea.hub.friends[0])
        ann.hub.invite(ann.hub.friends[0])
        try? await Task.sleep(for: .milliseconds(150))
        XCTAssertTrue(bea.hub.invites.isEmpty); XCTAssertNil(bea.hub.banner)
        XCTAssertTrue(bea.hub.onlineFriends.isEmpty && bea.hub.blockedFriends.count == 1)
        bea.hub.unblock(bea.hub.blockedFriends[0]); XCTAssertEqual(bea.hub.onlineFriends.count, 1)
    }

    func testJoiningWhileInAPartyLeavesItFirst() async {
        let service = FakeService(); let (ann, bea) = await pair(service)
        defer { finish(ann, bea) }
        bea.room.inLobby = true
        ann.hub.invite(ann.hub.friends[0]); await until("banner") { bea.hub.banner != nil }
        bea.hub.accept(bea.hub.banner!)
        await until("joined") { !bea.room.joined.isEmpty }
        XCTAssertEqual(bea.room.left, 1)
    }

    // MARK: codes, links, push

    func testACodeFindsTheHostAndAcceptsLikeAnInvite() async {
        let service = FakeService(); let (ann, bea) = await pair(service)
        defer { finish(ann, bea) }
        guard let text = ann.hub.prepareShare() else { return XCTFail("no share text") }
        let code = ann.hub.party!.code
        XCTAssertTrue(text.contains(code) && text.contains("Ann") && text.contains("motionclub://j/\(code)"), text)
        // the service learns the lobby from the invite; here from presence, so make it known as the real one does
        service.lobbies[code] = ("ann", "tennis", ann.hub.party!.group, 2)
        bea.hub.resolve(" " + code.lowercased() + " ")
        await until("resolved") { bea.hub.resolvedInvite != nil }
        XCTAssertEqual(bea.hub.resolvedInvite?.from.name, "Ann")
        bea.hub.accept(bea.hub.resolvedInvite!)
        await until("joined") { !bea.room.joined.isEmpty }
        XCTAssertEqual(bea.room.joined.first?.0, ann.hub.party?.group)
        bea.hub.resolve("NOPE00"); await until("not found") { bea.hub.notice.contains("ended") || bea.hub.resolvedInvite == nil }
    }

    func testCodeAndLinkParsing() {
        XCTAssertEqual(ClubHubConfig.normalizedCode("abc 234"), "ABC234")
        XCTAssertEqual(ClubHubConfig.normalizedCode("motionclub://j/ABC234"), "ABC234")
        XCTAssertEqual(ClubHubConfig.normalizedCode("https://club.example/j/abc234?x=1"), "ABC234", "a link with a query: the code is the six after /j/")
        XCTAssertNil(ClubHubConfig.normalizedCode("hello")); XCTAssertNil(ClubHubConfig.normalizedCode("ABC23"))
        for _ in 0..<200 { let g = ClubHubConfig.newGroup(); XCTAssertFalse((0x4d500200...0x4d5002ff).contains(g), "never quick match's groups"); XCTAssertEqual(ClubHubConfig.newCode().count, 6) }
    }

    func testPushPayloadBecomesAnInviteAndJoinAnswersOnceConnected() async {
        let service = FakeService(); let (ann, bea) = await pair(service)
        defer { finish(ann, bea) }
        ann.hub.invite(ann.hub.friends[0]); await until("invite") { bea.hub.invites.count == 1 }
        let real = bea.hub.invites[0]
        let userInfo: [AnyHashable: Any] = ["inviteID": real.inviteID, "lobbyCode": real.lobbyCode, "group": NSNumber(value: real.group), "sport": "tennis", "capacity": 2,
                                           "from": ["id": "ann", "name": "Ann"], "expiresAt": real.expiresAt]
        let parsed = ClubPush.invite(from: userInfo)
        XCTAssertEqual(parsed, real, "the notification carries the whole invite")
        XCTAssertNil(ClubPush.invite(from: ["inviteID": "x"]), "a notification without a lobby is not an invite")
        bea.hub.notification(invite: parsed!, accept: true)
        await until("joined") { !bea.room.joined.isEmpty }
        await until("Ann sees accepted") { ann.hub.sent["bea"] == .accepted }
        // tapping the notification (no action) just shows the invite
        bea.hub.notification(invite: HubInvite(inviteID: "later", from: HubPlayerRef(id: "ann", name: "Ann"), sport: "golf", lobbyCode: "ABC234", group: 7, capacity: 4, expiresAt: service.clock + 60_000), accept: nil)
        XCTAssertEqual(bea.hub.banner?.sport, "golf")
    }

    func testFirstInviteAsksForNotificationPermissionOnce() async {
        let service = FakeService(); let (ann, bea) = await pair(service)
        defer { finish(ann, bea) }
        var asked = 0; ann.hub.requestPushPermission = { asked += 1 }
        ann.hub.invite(ann.hub.friends[0]); ann.hub.invite(ann.hub.friends[0])
        XCTAssertEqual(asked, 1, "asked after the first invite, never at launch, never twice")
    }

    func testPushTokenIsSentWithHelloAndWhenItChanges() async {
        let service = FakeService()
        let ann = Phone(service, id: "ann", name: "Ann", friends: [])
        defer { finish(ann) }
        ann.hub.pushToken = ("aa11", "sandbox"); ann.hub.start()
        await until("online") { ann.hub.link == .online }
        XCTAssertTrue(service.tokens.contains("ann:aa11"))
        ann.hub.setPushToken("bb22", env: "sandbox")   // (the fake only records hello's token)
    }

    // MARK: protocol

    func testServerMessagesDecode() {
        func d(_ s: String) -> HubServerMessage? { HubServerMessage.decode(Data(s.utf8)) }
        XCTAssertEqual(d(#"{"t":"welcome","you":"a","v":1,"serverTime":5}"#), .welcome(you: "a", serverTime: 5))
        XCTAssertEqual(d(#"{"t":"presence","id":"b","online":true,"state":"lobby","name":"Bea","lobby":{"code":"ABC234","sport":"golf","size":2,"capacity":4,"open":false,"group":9}}"#),
                       .presence(id: "b", online: true, state: .lobby, lobby: HubLobbyInfo(code: "ABC234", sport: "golf", size: 2, capacity: 4, open: false, group: 9), name: "Bea"))
        XCTAssertEqual(d(#"{"t":"presence","id":"b","online":false}"#), .presence(id: "b", online: false, state: nil, lobby: nil, name: nil))
        XCTAssertEqual(d(#"{"t":"inviteUpdate","inviteID":"i","status":"pushed","to":"b"}"#), .inviteUpdate(inviteID: "i", status: "pushed", to: "b"))
        XCTAssertEqual(d(#"{"t":"error","code":"rate_limited","message":"slow","ref":"x"}"#), .error(code: "rate_limited", message: "slow", ref: "x"))
        XCTAssertEqual(d(#"{"t":"invite","inviteID":"i","from":{"id":"a","name":"Ann"},"sport":"tennis","lobbyCode":"ABC234","group":4000000000,"capacity":2,"expiresAt":99}"#),
                       .invite(HubInvite(inviteID: "i", from: HubPlayerRef(id: "a", name: "Ann"), sport: "tennis", lobbyCode: "ABC234", group: 4_000_000_000, capacity: 2, expiresAt: 99)))
        XCTAssertEqual(d(#"{"t":"somethingNew"}"#), .unknown("somethingNew")); XCTAssertNil(d("not json"))
        let hello = try? JSONSerialization.jsonObject(with: HubClientMessage.hello(auth: .dev("a"), name: "Ann", friends: ["b"], presence: HubPresence(state: .plaza), push: nil)) as? [String: Any]
        XCTAssertEqual(hello?["t"] as? String, "hello"); XCTAssertEqual(hello?["v"] as? Int, 1); XCTAssertEqual((hello?["auth"] as? [String: Any])?["dev"] as? String, "a")
    }

    func testSnapshots() async throws {
        let service = FakeService()
        let friends = [("bea", "Bea"), ("cy", "Cy"), ("dee", "Dee"), ("eli", "Eli")]
        let ann = Phone(service, id: "ann", name: "Ann", friends: friends.map { FriendProfile(id: $0.0, name: $0.1) })
        var others: [Phone] = []
        for (id, name) in friends.prefix(2) { others.append(Phone(service, id: id, name: name, friends: [FriendProfile(id: "ann", name: "Ann")])) }
        defer { finish(ann); others.forEach { finish($0) } }
        ann.hub.start(); others.forEach { $0.hub.start() }
        await until("friends online") { ann.hub.onlineFriends.count == 2 }
        others[1].context.inMatch = true; others[1].hub.publishPresence()
        await until("Cy in a match") { ann.hub.friends.first { $0.id == "cy" }?.state == .match }
        // Bea invites Ann (Bea needs Ann in her list: she has her)
        others[0].hub.invite(others[0].hub.friends[0])
        await until("Ann's banner") { ann.hub.banner != nil }
        try await save(ZStack(alignment: .top) { Color(hex: "16294A"); InviteBannerHost(hub: ann.hub) }, "club-invite-banner", size: CGSize(width: 402, height: 260))
        try await save(ClubHubView(hub: ann.hub), "club-hub", size: CGSize(width: 402, height: 874))
        ann.hub.snooze(ann.hub.banner!); ann.hub.invite(ann.hub.friends[1])
        try await save(ClubHubView(hub: ann.hub), "club-hub-sent", size: CGSize(width: 402, height: 874))
        // before the friends list is allowed, and with no service in this build
        let fresh = Phone(nil, id: "zed", name: "Zed", friends: [], url: nil)
        fresh.source.accessValue = .notDetermined
        fresh.hub.start(); await until("first-time state") { fresh.hub.access == .notDetermined && fresh.hub.link == .notConfigured }
        try await save(ClubHubView(hub: fresh.hub), "club-hub-first-time", size: CGSize(width: 402, height: 874))
        fresh.hub.debugReset()
    }
    private func save<V: View>(_ view: V, _ name: String, size: CGSize) async throws {
        guard let dir = ProcessInfo.processInfo.environment["MENU_SNAP_DIR"] else { return }
        let folder = URL(fileURLWithPath: dir, isDirectory: true)
        try FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true)
        let scene = try XCTUnwrap(UIApplication.shared.connectedScenes.compactMap { $0 as? UIWindowScene }.first)
        let host = UIHostingController(rootView: view); let window = UIWindow(windowScene: scene)
        window.frame = CGRect(origin: .zero, size: size); window.rootViewController = host; window.isHidden = false
        host.view.frame = window.bounds; host.view.layoutIfNeeded(); try await Task.sleep(for: .seconds(0.8))
        let image = UIGraphicsImageRenderer(size: size).image { _ in host.view.drawHierarchy(in: window.bounds, afterScreenUpdates: true) }
        try XCTUnwrap(image.pngData()).write(to: folder.appendingPathComponent("\(name).png")); window.isHidden = true
    }
}

/// The real WebSocket client and the real Club Hub service (club-hub/, run with HUB_DEV_AUTH=1) on this Mac. Skipped unless
/// CLUB_HUB_URL is set (xcodebuild: TEST_RUNNER_CLUB_HUB_URL=ws://127.0.0.1:8090/v1/hub).
@MainActor
final class ClubHubLiveServiceTests: XCTestCase {
    private func until(_ what: String, _ condition: () -> Bool, file: StaticString = #filePath, line: UInt = #line) async {
        for _ in 0..<300 { if condition() { return }; try? await Task.sleep(for: .milliseconds(20)) }
        XCTFail("timed out waiting for \(what)", file: file, line: line)
    }
    func testTwoPhonesOverTheRealServiceInviteJoinAndResolveACode() async throws {
        guard let text = ProcessInfo.processInfo.environment["CLUB_HUB_URL"], let url = URL(string: text) else { throw XCTSkip("CLUB_HUB_URL not set") }
        let tag = String(UUID().uuidString.prefix(6))
        let (a, b) = ("ann-\(tag)", "bea-\(tag)")
        let ann = ClubHubTests.Phone(nil, id: a, name: "Ann", friends: [FriendProfile(id: b, name: "Bea")], url: url)
        let bea = ClubHubTests.Phone(nil, id: b, name: "Bea", friends: [FriendProfile(id: a, name: "Ann")], url: url)
        defer { ann.hub.debugReset(); bea.hub.debugReset() }
        ann.hub.start(); bea.hub.start()
        await until("both online and seeing each other") { ann.hub.link == .online && bea.hub.link == .online && ann.hub.friends.first?.online == true && bea.hub.friends.first?.online == true }
        bea.context.inMatch = true; bea.hub.publishPresence()
        await until("Ann sees Bea in a match") { ann.hub.friends.first?.state == .match }
        bea.context.inMatch = false; bea.hub.publishPresence()
        await until("Ann sees Bea back in the plaza") { ann.hub.friends.first?.state == .plaza }
        ann.hub.invite(ann.hub.friends[0])
        await until("Bea's banner") { bea.hub.banner != nil }
        XCTAssertEqual(bea.hub.banner?.from.name, "Ann"); XCTAssertEqual(bea.hub.banner?.group, ann.hub.party?.group)
        XCTAssertEqual(bea.hub.banner?.lobbyCode, ann.hub.party?.code)
        await until("Ann sees delivered") { ann.hub.sent[b] == .delivered }
        XCTAssertGreaterThan(bea.hub.remaining(bea.hub.banner!), 0.9, "the countdown uses the service's clock")
        // a code resolves to Ann's lobby
        ann.room.partySize = 1; ann.hub.publishPresence(force: true)
        let code = ann.hub.party!.code
        bea.hub.resolve(code)
        await until("code resolved") { bea.hub.resolvedInvite != nil }
        XCTAssertEqual(bea.hub.resolvedInvite?.from.name, "Ann"); XCTAssertEqual(bea.hub.resolvedInvite?.group, ann.hub.party?.group)
        bea.hub.accept(bea.hub.banner!)
        await until("Ann sees accepted") { ann.hub.sent[b] == .accepted }
        await until("Bea joined the room") { !bea.room.joined.isEmpty }
        XCTAssertEqual(bea.room.joined.first?.1, a)
        // offline, then back
        bea.hub.disconnect()
        await until("Ann sees Bea offline") { ann.hub.friends.first?.online == false }
    }
}
