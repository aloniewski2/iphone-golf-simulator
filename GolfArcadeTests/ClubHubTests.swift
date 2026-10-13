import SwiftUI
import XCTest
@testable import GolfArcade

/// PLAN_OnlineHub_FriendsInvites phases 1-4, phone half, over an in-memory Club Hub service: friend presence, invites that arrive
/// inside the game (banner rules), one-tap join into the host's party over the service's relay room, offline invites, push
/// payloads, codes. The real service is ClubHubLiveServiceTests; APNs and two real phones are device / deployment gates.
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
        /// Party rooms (the relay): members in the order they came in, and who may come in.
        var rooms: [String: (members: [String], allowed: Set<String>)] = [:]
        /// Invites waiting for an offline friend (delivered at their hello).
        var queued: [String: [[String: Any]]] = [:]
        var relayed = 0

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
                for invite in queued.removeValue(forKey: id) ?? [] {
                    t.reply(invite)
                    if let from = (invite["from"] as? [String: Any])?["id"] as? String { sessions[from]?.transport.reply(["t": "inviteUpdate", "inviteID": invite["inviteID"] ?? "", "status": "delivered", "to": id]) }
                }
                return
            }
            guard let me = t.id, var s = sessions[me] else { return }
            switch type {
            case "presence":
                let wasVisible = (s.presence["invisible"] as? Bool) != true
                s.presence = m; sessions[me] = s
                // appear offline: friends are told you left (the real service does the same); back: you reappear
                for other in sessions.keys where other != me && (sessions[other]?.friends.contains(me) == true || sessions[me]?.friends.contains(other) == true) {
                    if (m["invisible"] as? Bool) == true { if wasVisible { sessions[other]?.transport.reply(["t": "presence", "id": me, "online": false]) } }
                    else { sessions[other]?.transport.reply(presenceMessage(me)) }
                }
            case "friends": s.friends = Set(m["ids"] as? [String] ?? []); sessions[me] = s
            case "invite":
                // friends on either list
                guard let to = m["to"] as? String, sessions[me]!.friends.contains(to) || sessions[to]?.friends.contains(me) == true else {
                    t.reply(["t": "error", "code": "not_friends", "message": "no"]); return
                }
                if let code = m["lobbyCode"] as? String, rooms[code]?.members.contains(me) == true { rooms[code]?.allowed.insert(to) }
                n += 1; let inviteID = "inv\(n)"
                // the newest from one sender to one receiver replaces the older
                for (oldID, old) in pending where old.from == me && old.to == to { pending[oldID] = nil; t.reply(["t": "inviteUpdate", "inviteID": oldID, "status": "cancelled", "to": to]); sessions[to]?.transport.reply(["t": "inviteGone", "inviteID": oldID, "status": "cancelled"]) }
                pending[inviteID] = (me, to, m["lobbyCode"] as? String ?? "")
                let invite: [String: Any] = ["t": "invite", "inviteID": inviteID, "from": ["id": me, "name": s.name], "sport": m["sport"] ?? "tennis", "lobbyCode": m["lobbyCode"] ?? "", "group": m["group"] ?? 0, "capacity": m["capacity"] ?? 2, "expiresAt": clock + 120_000]
                if let r = sessions[to] {
                    r.transport.reply(invite)
                    t.reply(["t": "inviteUpdate", "inviteID": inviteID, "status": "delivered", "to": to])
                } else if tokens.contains(where: { $0.hasPrefix(to + ":") }) {
                    t.reply(["t": "inviteUpdate", "inviteID": inviteID, "status": "pushed", "to": to])
                } else {
                    // offline: it waits for them
                    queued[to, default: []].append(invite)
                    t.reply(["t": "inviteUpdate", "inviteID": inviteID, "status": "queued", "to": to])
                }
                lobbies[m["lobbyCode"] as? String ?? ""] = (me, m["sport"] as? String ?? "tennis", UInt32(truncating: (m["group"] as? NSNumber) ?? 0), 2)
            case "inviteResponse":
                guard let id = m["inviteID"] as? String, let p = pending[id], p.to == me else { t.reply(["t": "error", "code": "bad_message", "message": "no", "ref": m["inviteID"] ?? ""]); return }
                pending[id] = nil
                sessions[p.from]?.transport.reply(["t": "inviteUpdate", "inviteID": id, "status": (m["accept"] as? Bool) == true ? "accepted" : "declined", "to": me])
            case "inviteCancel":
                if let id = m["inviteID"] as? String, let p = pending[id], p.from == me { pending[id] = nil; sessions[p.to]?.transport.reply(["t": "inviteGone", "inviteID": id, "status": "cancelled"]) }
            case "roomOpen", "roomJoin", "roomLeave":
                let code = m["room"] as? String ?? ""
                if type == "roomLeave" { rooms[code]?.members.removeAll { $0 == me }; t.reply(["t": "roomPeers", "room": code, "peers": [] as [Any]]); peers(code); return }
                if rooms[code] == nil {
                    guard type == "roomOpen" else { t.reply(["t": "error", "code": "room_gone", "message": "gone", "ref": code]); return }
                    rooms[code] = ([], [me])
                }
                guard rooms[code]!.allowed.contains(me) else { t.reply(["t": "error", "code": type == "roomOpen" ? "room_taken" : "room_denied", "message": "no", "ref": code]); return }
                if !rooms[code]!.members.contains(me) { rooms[code]!.members.append(me) }
                peers(code)
            case "rd":
                let code = m["room"] as? String ?? ""
                guard let room = rooms[code], room.members.contains(me) else { t.reply(["t": "error", "code": "room_gone", "message": "no", "ref": "rd"]); return }
                relayed += 1
                for id in (m["to"] as? [String]) ?? room.members where id != me && room.members.contains(id) {
                    sessions[id]?.transport.reply(["t": "rd", "room": code, "from": me, "d": m["d"] ?? ""])
                }
            case "resolve":
                let code = m["code"] as? String ?? ""
                rooms[code]?.allowed.insert(me)
                if let l = lobbies[code] { t.reply(["t": "resolved", "code": code, "found": true, "from": ["id": l.from, "name": sessions[l.from]?.name ?? "Host"], "sport": l.sport, "group": l.group, "capacity": l.capacity]) }
                else { t.reply(["t": "resolved", "code": code, "found": false]) }
            default: break
            }
        }
        /// One lists the other, and the other is online and not invisible.
        func sees(_ viewer: String, _ target: String) -> Bool {
            guard let v = sessions[viewer], let tg = sessions[target] else { return false }
            return (v.friends.contains(target) || tg.friends.contains(viewer)) && (tg.presence["invisible"] as? Bool) != true
        }
        func peers(_ code: String) {
            guard let room = rooms[code] else { return }
            let list = room.members.map { ["id": $0, "name": sessions[$0]?.name ?? $0] }
            for id in room.members { sessions[id]?.transport.reply(["t": "roomPeers", "room": code, "peers": list]) }
        }
        func presenceMessage(_ id: String) -> [String: Any] {
            let s = sessions[id]!
            var m: [String: Any] = ["t": "presence", "id": id, "online": true, "state": s.presence["state"] ?? "plaza", "name": s.name]
            if let l = s.presence["lobby"] { m["lobby"] = l }
            return m
        }
        func disconnect(_ id: String) {
            sessions[id] = nil
            for code in rooms.keys where rooms[code]!.members.contains(id) { rooms[code]!.members.removeAll { $0 == id }; peers(code) }
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
    /// A lobby stand-in: records what the hub asked for. (`ServiceRoom` is the real MultiplayerService over the relay.)
    @MainActor final class FakeRoom: ClubHubRoom {
        var inLobby = false, partySize = 1, capacity = 2, sport = "tennis"
        var hosted: [String] = [], joined: [(code: String, host: String)] = [], left = 0
        func hostParty(over transport: MultiplayerTransport) throws { hosted.append((transport as? HubRoomTransport)?.code ?? ""); inLobby = true }
        func joinParty(over transport: MultiplayerTransport, host: String) async throws { joined.append(((transport as? HubRoomTransport)?.code ?? "", host)); inLobby = true }
        func leaveLobby() { left += 1; inLobby = false }
    }
    /// The real lobby (a MultiplayerService of its own, no Unity) over the hub's relay room: the whole party path.
    @MainActor final class ServiceRoom: ClubHubRoom {
        let service: MultiplayerService
        init(_ name: String) {
            service = MultiplayerService(sendToRuntime: { _ in true }, pollRuntime: { nil })
            service.onMatchRequested = { _ in }; service.onReturnToLobby = {}
            service.setScreen(true); service.requiresStableLink = false
            service.setIdentity(name: name, loadout: MultiplayerLoadout(skinHex: "C47A4C", colours: ["shirt": "E8505B"], emotes: EmoteCatalog.defaults, shirtHex: "E8505B", shortsHex: "24345E"))
        }
        var inLobby: Bool { service.lobby != nil && service.isParty }
        var partySize: Int { service.lobby?.participants.count ?? 1 }
        var capacity: Int { service.lobby?.capacity ?? 2 }
        var sport: String { service.lobby?.sport.rawValue ?? "tennis" }
        func hostParty(over transport: MultiplayerTransport) throws { try service.hostParty(over: transport) }
        func joinParty(over transport: MultiplayerTransport, host: String) async throws { try await service.joinParty(over: transport, host: host) }
        func leaveLobby() { if service.isParty { service.leave() } }
    }
    @MainActor final class Phone {
        let hub: ClubHub, room: FakeRoom, source: FakeSource
        /// With `realLobby`: the phone's MultiplayerService (its party over the relay).
        let lobby: ServiceRoom?
        var mp: MultiplayerService { lobby!.service }
        var toasts: [String] = [], haptics = 0, context = ClubHubContext()
        init(_ service: FakeService?, id: String, name: String, friends: [FriendProfile], url: URL? = nil, realLobby: Bool = false) {
            source = FakeSource(id, name, friends: friends); room = FakeRoom(); lobby = realLobby ? ServiceRoom(name) : nil
            let r: ClubHubRoom = lobby ?? room
            if let service {
                hub = ClubHub(source: source, room: r, makeTransport: { FakeTransport(service) }, serverURL: { URL(string: "ws://test/v1/hub") }, devAuth: { true })
                hub.now = { service.clock }
            } else {
                // the real WebSocket client against a real Club Hub service (club-hub/, HUB_DEV_AUTH=1)
                hub = ClubHub(source: source, room: r, makeTransport: { WebSocketHubTransport() }, serverURL: { url }, devAuth: { true })
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
    private func pair(_ service: FakeService, realLobby: Bool = false) async -> (ann: Phone, bea: Phone) {
        let ann = Phone(service, id: "ann", name: "Ann", friends: [FriendProfile(id: "bea", name: "Bea")], realLobby: realLobby)
        let bea = Phone(service, id: "bea", name: "Bea", friends: [FriendProfile(id: "ann", name: "Ann")], realLobby: realLobby)
        ann.hub.start(); bea.hub.start()
        await until("both online") { ann.hub.link == .online && bea.hub.link == .online && ann.hub.friends.first?.online == true && bea.hub.friends.first?.online == true }
        return (ann, bea)
    }
    private func finish(_ phones: Phone...) { for p in phones { p.lobby?.service.leave(); p.hub.debugReset() } }

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

    /// REGISTERED: a friend whose phone never allowed the friends list (so lists nobody) is still seen online, by both sides.
    func testOneSideListingTheOtherIsEnoughToSeeEachOther() async {
        let service = FakeService()
        let ann = Phone(service, id: "ann", name: "Ann", friends: [FriendProfile(id: "bea", name: "Bea")])
        let bea = Phone(service, id: "bea", name: "Bea", friends: [])   // Bea has not allowed the friends list yet
        defer { finish(ann, bea) }
        ann.hub.start(); bea.hub.start()
        await until("each sees the other") { ann.hub.friends.first?.online == true && bea.hub.onlineFriends.map(\.id) == ["ann"] }
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
        XCTAssertEqual(ann.room.hosted, [ann.hub.party?.code ?? "-"], "the party is made on the first invite, over the relay room for its code")
        XCTAssertEqual(service.rooms[ann.hub.party?.code ?? ""]?.members, ["ann"], "the host is in its room on the service")
        XCTAssertEqual(bea.hub.banner?.lobbyCode, ann.hub.party?.code)
        XCTAssertTrue(service.rooms[ann.hub.party?.code ?? ""]?.allowed.contains("bea") == true, "the invite lets Bea in")
        await until("Ann sees delivered") { ann.hub.sent["bea"] == .delivered }
        XCTAssertTrue(ann.hub.currentPresence().lobby?.code == ann.hub.party?.code && ann.hub.party?.code.count == 6, "friends see the party's code")
    }

    func testJoinAnswersYesAndEntersTheHostsRoom() async {
        let service = FakeService(); let (ann, bea) = await pair(service)
        defer { finish(ann, bea) }
        ann.hub.invite(ann.hub.friends[0])
        await until("banner") { bea.hub.banner != nil }
        let invite = bea.hub.banner!
        bea.hub.accept(invite)
        await until("Ann sees accepted") { ann.hub.sent["bea"] == .accepted }
        await until("Bea joined") { !bea.room.joined.isEmpty && bea.hub.joining == nil }
        XCTAssertEqual(bea.room.joined.first?.code, ann.hub.party?.code); XCTAssertEqual(bea.room.joined.first?.host, "ann", "joins the invited host's party")
        XCTAssertEqual(service.rooms[ann.hub.party?.code ?? ""]?.members, ["ann", "bea"], "both in the party's room")
        XCTAssertNil(bea.hub.banner); XCTAssertTrue(bea.hub.invites.isEmpty)
        XCTAssertTrue(bea.toasts.first { $0.contains("Joining") } != nil, "the TV says who you are joining")
        XCTAssertEqual(bea.hub.party?.code, ann.hub.party?.code, "Bea is in Ann's party (she can invite to it too)")
    }

    func testDeclineTellsTheHost() async {
        let service = FakeService(); let (ann, bea) = await pair(service)
        defer { finish(ann, bea) }
        ann.hub.invite(ann.hub.friends[0])
        await until("banner") { bea.hub.banner != nil }
        bea.hub.decline(bea.hub.banner!)
        await until("Ann sees declined") { ann.hub.sent["bea"] == .declined }
        XCTAssertTrue(bea.room.joined.isEmpty)
    }

    /// INVITE_RELAY: one tap on Join puts the friend in the host's party (the real lobby code over the service's relay room, no Game
    /// Center): the party's lobby is the host's, and where each one walks in the Plaza reaches the other. Leaving tells the host.
    func testOneTapJoinPutsBothPhonesInOnePartyOverTheRelay() async {
        let service = FakeService(); let (ann, bea) = await pair(service, realLobby: true)
        defer { finish(ann, bea) }
        ann.hub.invite(ann.hub.friends[0])
        await until("banner") { bea.hub.banner != nil }
        bea.hub.accept(bea.hub.banner!)
        await until("Bea in Ann's party") { bea.mp.lobby?.ownerID == "ann" && ann.mp.lobby?.participants.count == 2 && bea.hub.joining == nil }
        XCTAssertTrue(ann.mp.isParty && bea.mp.isParty); XCTAssertTrue(ann.mp.isOwner)
        XCTAssertEqual(bea.mp.lobby?.participants.map(\.name).sorted(), ["Ann", "Bea"])
        XCTAssertTrue(bea.hub.notice.contains("joined"), bea.hub.notice)
        var heardByBea: [String] = [], heardByAnn: [String] = []
        bea.mp.onHub = { _, _, from in heardByBea.append(from) }; ann.mp.onHub = { _, _, from in heardByAnn.append(from) }
        ann.mp.sendHub("hubPresence", payload: "{}", reliable: false); bea.mp.sendHub("hubPresence", payload: "{}", reliable: false)
        await until("both hear each other") { heardByBea.contains("ann") && heardByAnn.contains("bea") }
        XCTAssertGreaterThan(service.relayed, 4, "the party's traffic goes through the relay room")
        bea.hub.leaveParty()
        await until("Ann's party is Ann again") { ann.mp.lobby?.participants.count == 1 }
        XCTAssertNil(bea.mp.lobby); XCTAssertNil(bea.hub.party)
        XCTAssertEqual(service.rooms[ann.hub.party?.code ?? ""]?.members, ["ann"])
        // and Ann can invite again into the same party
        ann.hub.invite(ann.hub.friends[0]); await until("banner again") { bea.hub.banner != nil }
        XCTAssertEqual(bea.hub.banner?.lobbyCode, ann.hub.party?.code)
    }

    /// A join that the service refuses (the party ended) says so and leaves nothing behind.
    func testJoiningAnEndedPartySaysSo() async {
        let service = FakeService(); let (_, bea) = await pair(service, realLobby: true)
        defer { finish(bea) }
        bea.hub.accept(HubInvite(inviteID: "code-ZZZZZZ", from: HubPlayerRef(id: "ann", name: "Ann"), sport: "tennis", lobbyCode: "ZZZZZZ", group: 0, capacity: 2, expiresAt: service.clock + 60_000))
        await until("refused") { bea.hub.joining == nil && !bea.hub.notice.isEmpty }
        XCTAssertTrue(bea.hub.notice.contains("ended"), bea.hub.notice)
        XCTAssertNil(bea.mp.lobby); XCTAssertNil(bea.hub.party)
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
        XCTAssertEqual(ann.room.hosted.count, 1, "one party (one relay room) however many invites")
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

    /// OFFLINE: one tap invites an offline friend too; the invite waits and shows as a banner the moment they open the game.
    func testAnOfflineFriendGetsTheInviteWhenTheyOpenTheGame() async {
        let service = FakeService()
        let ann = Phone(service, id: "ann", name: "Ann", friends: [FriendProfile(id: "bea", name: "Bea")])
        defer { finish(ann) }
        ann.hub.start(); await until("online") { ann.hub.link == .online }
        XCTAssertEqual(ann.hub.offlineFriends.map(\.id), ["bea"])
        ann.hub.invite(ann.hub.friends[0])
        await until("queued") { ann.hub.sent["bea"] == .queued }
        let bea = Phone(service, id: "bea", name: "Bea", friends: [FriendProfile(id: "ann", name: "Ann")])
        defer { finish(bea) }
        bea.hub.start()
        await until("Bea's banner as she opens the game") { bea.hub.banner?.from.name == "Ann" }
        await until("Ann sees delivered") { ann.hub.sent["bea"] == .delivered }
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
        XCTAssertEqual(bea.room.joined.first?.code, code)
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
        XCTAssertEqual(bea.hub.banner?.from.name, "Ann")
        XCTAssertEqual(bea.hub.banner?.lobbyCode, ann.hub.party?.code)
        await until("Ann sees delivered") { ann.hub.sent[b] == .delivered }
        XCTAssertGreaterThan(bea.hub.remaining(bea.hub.banner!), 0.9, "the countdown uses the service's clock")
        // a code resolves to Ann's lobby
        ann.room.partySize = 1; ann.hub.publishPresence(force: true)
        let code = ann.hub.party!.code
        bea.hub.resolve(code)
        await until("code resolved") { bea.hub.resolvedInvite != nil }
        XCTAssertEqual(bea.hub.resolvedInvite?.from.name, "Ann"); XCTAssertEqual(bea.hub.resolvedInvite?.lobbyCode, code)
        bea.hub.accept(bea.hub.banner!)
        await until("Ann sees accepted") { ann.hub.sent[b] == .accepted }
        await until("Bea joined the room") { !bea.room.joined.isEmpty }
        XCTAssertEqual(bea.room.joined.first?.host, a)
        // offline, then back
        bea.hub.disconnect()
        await until("Ann sees Bea offline") { ann.hub.friends.first?.online == false }
    }

    /// INVITE_RELAY over the real service: Ann invites, Bea taps Join once and is in Ann's party; the party's lobby and Plaza
    /// traffic go through the service's room both ways; an offline friend's invite waits and arrives when they connect.
    func testOneTapJoinIntoTheHostsPartyOverTheRealRelay() async throws {
        guard let text = ProcessInfo.processInfo.environment["CLUB_HUB_URL"], let url = URL(string: text) else { throw XCTSkip("CLUB_HUB_URL not set") }
        let tag = String(UUID().uuidString.prefix(6))
        let (a, b, c) = ("ann-\(tag)", "bea-\(tag)", "cy-\(tag)")
        let ann = ClubHubTests.Phone(nil, id: a, name: "Ann", friends: [FriendProfile(id: b, name: "Bea"), FriendProfile(id: c, name: "Cy")], url: url, realLobby: true)
        let bea = ClubHubTests.Phone(nil, id: b, name: "Bea", friends: [], url: url, realLobby: true)   // Bea never allowed her friends list
        defer { for p in [ann, bea] { p.mp.leave(); p.hub.debugReset() } }
        ann.hub.start(); bea.hub.start()
        await until("both online, each sees the other") { ann.hub.friends.first { $0.id == b }?.online == true && bea.hub.onlineFriends.contains { $0.id == a } }
        ann.hub.invite(ann.hub.friends.first { $0.id == b }!)
        await until("Bea's banner") { bea.hub.banner != nil }
        bea.hub.accept(bea.hub.banner!)
        await until("Bea in Ann's party") { bea.mp.lobby?.ownerID == a && ann.mp.lobby?.participants.count == 2 && bea.hub.joining == nil }
        var heardByBea: [String] = [], heardByAnn: [String] = []
        bea.mp.onHub = { _, _, from in heardByBea.append(from) }; ann.mp.onHub = { _, _, from in heardByAnn.append(from) }
        for _ in 0..<5 { ann.mp.sendHub("hubPresence", payload: "{}", reliable: false); bea.mp.sendHub("hubPresence", payload: "{}", reliable: false); try await Task.sleep(for: .milliseconds(50)) }
        await until("plaza traffic both ways") { heardByBea.contains(a) && heardByAnn.contains(b) }
        // Cy is offline: the invite waits, and arrives as Cy opens the game
        ann.hub.invite(ann.hub.friends.first { $0.id == c }!)
        await until("queued for Cy") { ann.hub.sent[c] == .queued }
        let cy = ClubHubTests.Phone(nil, id: c, name: "Cy", friends: [FriendProfile(id: a, name: "Ann")], url: url, realLobby: true)
        defer { cy.mp.leave(); cy.hub.debugReset() }
        cy.hub.start()
        await until("Cy's banner") { cy.hub.banner?.from.id == a }
        cy.hub.accept(cy.hub.banner!)
        await until("three in Ann's party") { ann.mp.lobby?.participants.count == 3 && cy.mp.lobby?.ownerID == a }
        // Bea leaves: Ann is told
        bea.hub.leaveParty()
        await until("Bea gone") { ann.mp.lobby?.participants.count == 2 }
    }
}
