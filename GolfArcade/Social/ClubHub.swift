import Foundation
import Observation
import UIKit

/// What the hub needs from the lobby and the game, behind a protocol so tests drive it without Game Center or Unity.
@MainActor protocol ClubHubRoom: AnyObject {
    /// This phone is in a friends' party (the lobby that lives on the hub's relay room).
    var inLobby: Bool { get }
    var partySize: Int { get }
    var capacity: Int { get }
    var sport: String { get }
    /// Host: a party over this relay room (made on the first invite). Any other lobby is left first.
    func hostParty(over transport: MultiplayerTransport) throws
    /// Guest: leave any lobby and join the host's party over this relay room. Returns once the host's lobby is here.
    func joinParty(over transport: MultiplayerTransport, host: String) async throws
    func leaveLobby()
}

/// What the player is doing right now, from the game's own state.
struct ClubHubContext: Equatable {
    var background = false
    var inMatch = false
    /// A match or the plaza is loading, or the arrival fly-in plays: banners wait until it is over.
    var busyLoading = false
}

/// The online hub (PLAN_OnlineHub_FriendsInvites.md): Game Center friends with online / offline from the Club Hub service,
/// invites that arrive inside the game, a pasted or linked invite code, and the join that follows. One instance for the app.
///
/// A party lives on the service too (its relay room, HubRoomTransport): inviting makes it, Join enters it, and the lobby, the Plaza
/// and the match travel through it. Game Center is not involved in inviting or joining (it is only who your friends are).
@MainActor @Observable
final class ClubHub {
    static let shared = ClubHub()

    enum Link: Equatable {
        /// No service address in this build: friends show without presence.
        case notConfigured
        case signedOut
        case connecting
        case online
        case offline(String)
    }
    struct Friend: Identifiable, Equatable {
        var id: String
        var name: String
        var online = false
        var state: HubPresenceState?
        var lobby: HubLobbyInfo?
    }
    enum Sent: Equatable {
        case sending, delivered, pushed, queued, accepted, declined, busy, unavailable, expired, cancelled
        var text: String {
            switch self {
            case .sending: "Sending…"; case .delivered: "Invite sent"; case .pushed: "Notified"; case .queued: "Invited · sees it when they open the game"
            case .accepted: "Joining…"; case .declined: "Not now"; case .busy: "Busy"; case .unavailable: "Not reachable"; case .expired: "No answer"; case .cancelled: "Cancelled"
            }
        }
        /// Still on its way or waiting for an answer (the row offers Cancel).
        var pending: Bool { [.sending, .delivered, .pushed, .queued].contains(self) }
    }

    // MARK: state the screens read
    private(set) var link: Link = .offline("")
    private(set) var access: FriendsAccess = .signedOut
    private(set) var friends: [Friend] = []
    private(set) var recent: [FriendProfile] = []
    /// Invites waiting for an answer, newest first.
    private(set) var invites: [HubInvite] = []
    /// The invite shown as a banner now (nil while none, while held, or once snoozed).
    private(set) var banner: HubInvite?
    private(set) var sent: [String: Sent] = [:]
    private(set) var notice = ""
    /// Joining a friend's game: who, until it connects or fails.
    private(set) var joining: String?
    /// A code looked up (pasted or from a link): who invited you.
    private(set) var resolvedInvite: HubInvite?
    var hubOpen = false
    var invisible: Bool { didSet { UserDefaults.standard.set(invisible, forKey: "clubhub.invisible"); publishPresence(force: true) } }
    var doNotDisturb: Bool { didSet { UserDefaults.standard.set(doNotDisturb, forKey: "clubhub.dnd"); publishPresence(force: true); updateBanner() } }
    private(set) var blocked: Set<String>
    /// The party this phone is in (made by the first invite, or joined): its code is the relay room's and what friends and share
    /// links use. (`group` stays in the protocol for older builds; it is 0.)
    private(set) var party: (code: String, group: UInt32)?
    /// The party's relay room (nil with no party).
    @ObservationIgnored private(set) var relayRoom: HubRoomTransport?
    /// My id on the service (the Game Center teamPlayerID).
    var selfID: String { you.isEmpty ? source.localID : you }
    @ObservationIgnored private var you = ""

    var unreadInvites: Int { invites.count }
    var onlineFriends: [Friend] { friends.filter { $0.online && !blocked.contains($0.id) }.sorted { $0.name.localizedCaseInsensitiveCompare($1.name) == .orderedAscending } }
    var offlineFriends: [Friend] { friends.filter { !$0.online && !blocked.contains($0.id) }.sorted { $0.name.localizedCaseInsensitiveCompare($1.name) == .orderedAscending } }
    var blockedFriends: [Friend] { friends.filter { blocked.contains($0.id) } }
    var recentNotFriends: [FriendProfile] { recent.filter { r in !friends.contains { $0.id == r.id } } }
    /// The words for sharing this party's code (a link when the service has a landing page).
    var shareText: String? { party.map { ClubHubConfig.shareMessage(code: $0.code, from: source.localName, sport: room.sport) } }

    // MARK: dependencies
    @ObservationIgnored let source: FriendsSource
    @ObservationIgnored let room: ClubHubRoom
    @ObservationIgnored var makeTransport: @MainActor () -> HubTransport
    @ObservationIgnored var serverURL: @MainActor () -> URL?
    @ObservationIgnored var devAuth: @MainActor () -> Bool
    @ObservationIgnored var context: @MainActor () -> ClubHubContext = { ClubHubContext() }
    @ObservationIgnored var playerName: @MainActor () -> String = { "Player" }
    /// Milliseconds since 1970 (a test moves it).
    @ObservationIgnored var now: @MainActor () -> Double = { Date().timeIntervalSince1970 * 1000 }
    @ObservationIgnored var tvToast: @MainActor (String) -> Void = { _ in }
    @ObservationIgnored var haptic: @MainActor () -> Void = {}
    @ObservationIgnored var pushToken: (token: String, env: String)?
    @ObservationIgnored var requestPushPermission: @MainActor () -> Void = {}

    // MARK: private
    @ObservationIgnored private var transport: HubTransport?
    @ObservationIgnored private var connectionID = 0
    @ObservationIgnored private var helloSent = false
    @ObservationIgnored private var backoff = 1.0
    @ObservationIgnored private var reconnect: Task<Void, Never>?
    @ObservationIgnored private var clockOffset = 0.0
    @ObservationIgnored private var lastPresence: HubPresence?
    @ObservationIgnored private var lastPresenceSent = 0.0
    @ObservationIgnored private var snoozed: Set<String> = []
    @ObservationIgnored private var ticker: Task<Void, Never>?
    @ObservationIgnored private var outgoing: [String: String] = [:]   // inviteID -> friend id
    @ObservationIgnored private var joinTask: Task<Void, Never>?
    @ObservationIgnored private var pendingResponse: (inviteID: String, accept: Bool)?
    @ObservationIgnored private var pendingAccept: HubInvite?
    @ObservationIgnored private var askedPush = false
    @ObservationIgnored private var started = false
    /// The service closed this connection because another phone took the player's place (4409).
    @ObservationIgnored private var replaced = false

    init(source: FriendsSource? = nil, room: ClubHubRoom? = nil, makeTransport: (@MainActor () -> HubTransport)? = nil,
         serverURL: (@MainActor () -> URL?)? = nil, devAuth: (@MainActor () -> Bool)? = nil) {
        self.source = source ?? GameKitFriendsSource()
        self.room = room ?? MultiplayerRoom()
        self.makeTransport = makeTransport ?? { WebSocketHubTransport() }
        self.serverURL = serverURL ?? { ClubHubConfig.serverURL }
        self.devAuth = devAuth ?? { ClubHubConfig.devAuth }
        invisible = UserDefaults.standard.bool(forKey: "clubhub.invisible")
        doNotDisturb = UserDefaults.standard.bool(forKey: "clubhub.dnd")
        blocked = Set(UserDefaults.standard.stringArray(forKey: "clubhub.blocked") ?? [])
    }

    // MARK: lifecycle

    /// The app is active (or the hub opened): read the friends and go online. Safe to call again.
    func start() {
        started = true; replaced = false
        if ticker == nil {
            ticker = Task { @MainActor [weak self] in
                while !Task.isCancelled { try? await Task.sleep(for: .seconds(1)); self?.tick() }
            }
        }
        Task { await refresh() }
    }

    /// Back to the foreground, or the hub opened: friends and recents again (a friend accepted in Apple's screen shows up).
    func refresh() async {
        access = await source.access()
        if access == .authorized {
            if let list = try? await source.friends() { applyFriends(list) }
            recent = (try? await source.recent()) ?? recent
        }
        connectIfPossible()
    }

    /// The hub asks the system for the friends list (Apple's prompt, the first time).
    func allowFriends() async {
        if let list = try? await source.friends() { applyFriends(list) }
        await refresh()
    }

    func addFriend() {
        do { try source.presentAddFriend() } catch { say(error.localizedDescription) }
    }

    private func applyFriends(_ list: [FriendProfile]) {
        let old = Dictionary(uniqueKeysWithValues: friends.map { ($0.id, $0) })
        friends = list.map { p in var f = old[p.id] ?? Friend(id: p.id, name: p.name); f.name = p.name; return f }
        if helloSent { send(HubClientMessage.friends(list.map(\.id))) }
    }

    func background() {
        guard link == .online else { return }
        publishPresence(force: true)
    }

    // MARK: connection

    private func connectIfPossible() {
        guard started, transport == nil, !replaced else { return }
        guard source.localID.isEmpty == false, access != .signedOut else { link = .signedOut; return }
        guard serverURL() != nil else { link = .notConfigured; return }
        connect()
    }

    private func connect() {
        guard let url = serverURL() else { link = .notConfigured; return }
        link = .connecting; helloSent = false
        connectionID += 1; let id = connectionID
        let t = makeTransport(); transport = t
        t.onOpen = { [weak self] in
            guard let self, self.connectionID == id else { return }
            Task { await self.sendHello(id) }
        }
        t.onMessage = { [weak self] data in
            guard let self, self.connectionID == id else { return }
            self.receive(data)
        }
        t.onClose = { [weak self] code in
            guard let self, self.connectionID == id else { return }
            self.closed(code)
        }
        t.connect(url)
    }

    private func sendHello(_ id: Int) async {
        let auth: HubClientMessage.Auth
        if devAuth() { auth = .dev(source.localID) }
        else {
            do { auth = try await source.identity() }
            catch { if connectionID == id { link = .offline(error.localizedDescription); transport?.close(); transport = nil; scheduleReconnect() }; return }
        }
        guard connectionID == id, let transport else { return }
        let presence = currentPresence()
        lastPresence = presence; lastPresenceSent = now()
        transport.send(HubClientMessage.hello(auth: auth, name: playerName(), friends: friends.map(\.id), presence: presence, push: pushToken))
        helloSent = true
    }

    private func closed(_ code: Int?) {
        transport = nil; helloSent = false
        for i in friends.indices { friends[i].online = false; friends[i].state = nil; friends[i].lobby = nil }
        switch code {
        case 4401:
            link = .offline("The Club Hub could not verify you. Open Game Center and sign in again.")
        case 4409:
            // another phone signed in as this player: do not fight it for the connection (until the app is opened again)
            link = .offline("You are signed in on another device."); replaced = true; return
        default:
            link = .offline("")
        }
        // an invite stays valid on the service for its two minutes; the list is what this phone knew
        scheduleReconnect()
    }

    private func scheduleReconnect() {
        reconnect?.cancel()
        let delay = backoff; backoff = min(30, backoff * 2)
        reconnect = Task { @MainActor [weak self] in
            try? await Task.sleep(for: .seconds(delay))
            guard let self, !Task.isCancelled, self.transport == nil else { return }
            self.connectIfPossible()
        }
    }

    func disconnect() {
        reconnect?.cancel()
        if helloSent { transport?.send(HubClientMessage.bye) }
        connectionID += 1; transport?.close(); transport = nil; helloSent = false
        link = serverURL() == nil ? .notConfigured : .offline("")
    }

    private func send(_ data: Data) { if helloSent { transport?.send(data) } }

    // MARK: presence

    func currentPresence() -> HubPresence {
        let c = context()
        let state: HubPresenceState = c.background ? .away : doNotDisturb ? .dnd : c.inMatch ? .match : room.inLobby ? .lobby : .plaza
        var lobby: HubLobbyInfo?
        if let party, room.inLobby { lobby = HubLobbyInfo(code: party.code, sport: room.sport, size: room.partySize, capacity: room.capacity, open: false, group: party.group) }
        return HubPresence(state: state, lobby: lobby, invisible: invisible)
    }

    /// Sends my state if it changed, or as the heartbeat (at least every 20 s).
    func publishPresence(force: Bool = false) {
        guard helloSent else { return }
        let p = currentPresence()
        if !force, p == lastPresence, now() - lastPresenceSent < 20_000 { return }
        lastPresence = p; lastPresenceSent = now()
        send(HubClientMessage.presence(p))
    }

    // MARK: messages

    func receive(_ data: Data) {
        guard let message = HubServerMessage.decode(data) else { return }
        switch message {
        case .welcome(let id, let serverTime):
            link = .online; backoff = 1
            if !id.isEmpty { you = id }
            if serverTime > 0 { clockOffset = serverTime - now() }
            if let pending = pendingResponse { pendingResponse = nil; send(HubClientMessage.inviteResponse(pending.inviteID, accept: pending.accept)) }
            // back after a dropped connection: into the party's room again (the opener re-opens it if the service lost it)
            if let r = relayRoom, !r.closed { send(r.opener ? HubClientMessage.roomOpen(r.code) : HubClientMessage.roomJoin(r.code)) }
            if let p = pendingAccept { if expired(p) { pendingAccept = nil; joining = nil; say("That invite has ended.") } else { accept(p) } }
        case .presence(let id, let online, let state, let lobby, let name):
            guard let i = friends.firstIndex(where: { $0.id == id }) else {
                if online { friends.append(Friend(id: id, name: name ?? "Friend", online: true, state: state, lobby: lobby)) }
                return
            }
            if let name, !name.isEmpty { friends[i].name = name }
            friends[i].online = online; friends[i].state = online ? state : nil; friends[i].lobby = online ? lobby : nil
        case .invite(let invite): received(invite)
        case .inviteUpdate(let inviteID, let status, let to): updated(inviteID, status, to: to)
        case .inviteGone(let inviteID, _): remove(inviteID)
        case .resolved(let code, let found, let from, let sport, let group, let capacity):
            guard found, let from, let group else { say("That invite has ended."); resolvedInvite = nil; return }
            resolvedInvite = HubInvite(inviteID: "code-" + code, from: from, sport: sport ?? "tennis", lobbyCode: code, group: group,
                                       capacity: capacity ?? 2, expiresAt: now() + 120_000)
        case .roomPeers(let code, let peers):
            if let r = relayRoom, r.code == code { r.roomPeers(peers) }
        case .relay(let code, let from, let text):
            if let r = relayRoom, r.code == code { r.received(text, from: from) }
        case .error(let code, let text, let ref):
            if code.hasPrefix("room_") {
                // about the party's room: the host's is reopened at once if it went (the service restarted); a guest is refused.
                // (A late one about a room already left is nobody's business.)
                if let r = relayRoom, r.code == ref || ref == "rd" {
                    if r.opener, code == "room_gone" { send(HubClientMessage.roomOpen(r.code)); return }
                    r.refused(code, message: text)
                }
                return
            }
            switch code {
            case "rate_limited": say("Slow down a moment.")
            case "not_friends": say("You can invite friends who also have you as a friend in Game Center.")
            case "blocked": break
            default: if !text.isEmpty { say(text) }
            }
        case .pong, .unknown: break
        }
    }

    // MARK: invites you receive

    private func received(_ invite: HubInvite) {
        guard !blocked.contains(invite.from.id) else { return }
        invites.removeAll { $0.inviteID == invite.inviteID || $0.from.id == invite.from.id }   // the newest from one friend replaces the older
        invites.insert(invite, at: 0)
        haptic()
        if !context().inMatch, !doNotDisturb { tvToast("\(invite.from.name) invited you · \(invite.sportTitle) · accept on your phone") }
        updateBanner()
    }

    private func updated(_ inviteID: String, _ status: String, to: String?) {
        // the sender learns an invite's id from the first update, which names the friend it was for
        if let to { outgoing[inviteID] = to }
        guard let friend = outgoing[inviteID] else { return }
        let next: Sent?
        switch status {
        case "delivered": next = .delivered; case "pushed": next = .pushed; case "queued": next = .queued; case "accepted": next = .accepted
        case "declined": next = .declined; case "busy": next = .busy; case "unavailable": next = .unavailable
        case "expired": next = .expired; case "cancelled": next = .cancelled; default: next = nil
        }
        guard let next else { return }
        sent[friend] = next
        if [.declined, .busy, .unavailable, .expired, .cancelled].contains(next) { outgoing[inviteID] = nil }
    }

    private func remove(_ inviteID: String) {
        invites.removeAll { $0.inviteID == inviteID }
        snoozed.remove(inviteID)
        updateBanner()
    }

    /// Banner rules: held while a match or a load is on (a swing is never interrupted), when Do Not Disturb is on, or in the
    /// background; never shown once snoozed (it stays in the hub); shows the newest.
    func updateBanner() {
        let c = context()
        if c.inMatch || c.busyLoading || c.background || doNotDisturb { banner = nil; return }
        banner = invites.first { !snoozed.contains($0.inviteID) && !expired($0) }
    }

    func expired(_ invite: HubInvite) -> Bool { invite.expiresAt + 0 <= now() + clockOffset }
    /// Fraction of the 2 minutes left (the banner's countdown).
    func remaining(_ invite: HubInvite) -> Double { max(0, min(1, (invite.expiresAt - (now() + clockOffset)) / 120_000)) }

    /// Not now: off the banner, still in the hub until it expires.
    func snooze(_ invite: HubInvite) { snoozed.insert(invite.inviteID); updateBanner() }

    func decline(_ invite: HubInvite) {
        send(HubClientMessage.inviteResponse(invite.inviteID, accept: false))
        remove(invite.inviteID)
    }

    /// Join (one tap on the banner): say yes to the service, then enter the host's party room. The phone stays where it is (the
    /// Plaza's controller); the friend appears in your Plaza and you in theirs. In a party of your own you leave it first.
    func accept(_ invite: HubInvite) {
        guard joining == nil || pendingAccept?.inviteID == invite.inviteID else { return }
        guard link == .online else {
            // (opened from a notification, or the connection is coming back): join the moment it is up
            pendingAccept = invite; joining = invite.from.name; hubOpen = false; connectIfPossible(); return
        }
        pendingAccept = nil
        resolvedInvite = nil
        if !invite.inviteID.hasPrefix("code-") { send(HubClientMessage.inviteResponse(invite.inviteID, accept: true)) }
        remove(invite.inviteID)
        joining = invite.from.name; hubOpen = false
        tvToast("Joining \(invite.from.name)…")
        joinTask?.cancel()
        joinTask = Task { @MainActor [weak self] in
            guard let self else { return }
            if self.room.inLobby || self.relayRoom != nil { self.leaveParty() }
            let relay = HubRoomTransport(hub: self, code: invite.lobbyCode, opener: false)
            self.relayRoom = relay
            self.send(HubClientMessage.roomJoin(invite.lobbyCode))
            do {
                try await self.room.joinParty(over: relay, host: invite.from.id)
                guard self.relayRoom === relay else { return }
                self.party = (invite.lobbyCode, 0); self.joining = nil
                self.say("You joined \(invite.from.name)'s party"); self.tvToast("You joined \(invite.from.name)'s party")
                self.publishPresence(force: true)
                self.onJoined(invite.from.id)
            } catch {
                if self.relayRoom === relay { self.relayRoom = nil; relay.disconnect() }
                self.joining = nil
                if !(error is CancellationError) { self.say(relay.refusal ?? error.localizedDescription) }
            }
        }
    }

    func cancelJoin() { joinTask?.cancel(); joinTask = nil; pendingAccept = nil; if party == nil, let r = relayRoom { relayRoom = nil; r.disconnect() }; joining = nil }
    /// Joined a friend's party (their id): the Plaza takes you to them.
    @ObservationIgnored var onJoined: @MainActor (String) -> Void = { _ in }

    // MARK: invites you send

    /// Invite a friend (one tap, online or offline) to your party, made now if you have none. Online: their game shows the banner at
    /// once. Offline: the service keeps it and they get it the moment they open the game (or a notification, where push is set up).
    func invite(_ friend: Friend) {
        guard link == .online else { say(link == .notConfigured ? "Friends are not available in this build yet." : "Not connected to the Club Hub. Trying again…"); connectIfPossible(); return }
        guard let party = ensureParty() else { return }
        sent[friend.id] = .sending
        send(HubClientMessage.invite(to: friend.id, sport: room.sport, lobbyCode: party.code, group: party.group, capacity: room.capacity))
        publishPresence(force: true)
        haptic()
        if !askedPush { askedPush = true; requestPushPermission() }
    }

    /// Someone without the app: make the party now and give back the words to send. Nil if it cannot be made.
    func prepareShare() -> String? {
        guard link == .online, let party = ensureParty() else { if link != .online { say("Connect to the Club Hub to share an invite.") }; return nil }
        publishPresence(force: true)
        return ClubHubConfig.shareMessage(code: party.code, from: source.localName, sport: room.sport)
    }

    /// The party to invite to: the one this phone is in, or a new one hosted here over a fresh relay room.
    private func ensureParty() -> (code: String, group: UInt32)? {
        if let party, room.inLobby, let r = relayRoom, !r.closed { return party }
        let code = ClubHubConfig.newCode()
        let relay = HubRoomTransport(hub: self, code: code, opener: true)
        do {
            if relayRoom != nil { leaveParty() }
            relayRoom = relay
            send(HubClientMessage.roomOpen(code))
            try room.hostParty(over: relay)
        } catch {
            if relayRoom === relay { relayRoom = nil; relay.disconnect() }
            say(error.localizedDescription); return nil
        }
        party = (code, 0)
        return party
    }

    func cancelInvite(to friend: Friend) {
        for (inviteID, id) in outgoing where id == friend.id { send(HubClientMessage.inviteCancel(inviteID)); outgoing[inviteID] = nil }
        sent[friend.id] = .cancelled
    }

    /// Leave the party (its lobby and its room).
    func leaveParty() {
        let r = relayRoom
        room.leaveLobby()               // (the lobby's transport is the relay: leaving it closes the room too)
        if let r, !r.closed { r.disconnect() }
        partyEnded()
    }

    /// The party ended or everyone left: a new party gets a fresh code.
    func partyEnded() {
        party = nil; sent = [:]; outgoing = [:]
        if let r = relayRoom, r.closed { relayRoom = nil }
        publishPresence(force: true)
    }

    // MARK: the relay (HubRoomTransport's side)

    func relayLive(_ r: HubRoomTransport) -> Bool { relayRoom === r && helloSent }
    func relay(_ code: String, to: [String]?, _ text: String) { send(HubClientMessage.relay(code, to: to, text)) }
    /// The party's transport closed (the lobby was left): leave the room on the service.
    func roomClosed(_ r: HubRoomTransport) {
        send(HubClientMessage.roomLeave(r.code))
        if relayRoom === r { relayRoom = nil; party = nil; sent = [:]; outgoing = [:]; publishPresence(force: true) }
    }

    // MARK: codes and links

    /// A code typed, pasted, or opened from a link: ask the service who it belongs to; the answer is `resolvedInvite`.
    func resolve(_ text: String) {
        guard let code = ClubHubConfig.normalizedCode(text) else { say("That is not an invite code. It has six letters and numbers."); return }
        guard link == .online else { say("Connect to the Club Hub to use an invite code."); return }
        resolvedInvite = nil
        send(HubClientMessage.resolve(code))
    }

    func open(url: URL) {
        guard let code = ClubHubConfig.normalizedCode(url.absoluteString) else { return }
        hubOpen = true
        resolve(code)
    }

    // MARK: blocks

    func block(_ friend: Friend) {
        blocked.insert(friend.id); UserDefaults.standard.set(Array(blocked), forKey: "clubhub.blocked")
        send(HubClientMessage.block(friend.id, true))
        invites.removeAll { $0.from.id == friend.id }; updateBanner()
    }
    func unblock(_ friend: Friend) {
        blocked.remove(friend.id); UserDefaults.standard.set(Array(blocked), forKey: "clubhub.blocked")
        send(HubClientMessage.block(friend.id, false))
    }

    // MARK: push

    func setPushToken(_ token: String, env: String) {
        pushToken = (token, env)
        send(HubClientMessage.push(token: token, env: env))
    }

    /// The player tapped a notification (or its Join / Not now): the invite it carried, answered once connected.
    func notification(invite: HubInvite, accept: Bool?) {
        guard !expired(invite) else { say("That invite has ended."); return }
        if let accept {
            if accept { self.accept(invite) } else { pendingResponse = (invite.inviteID, false); if helloSent { send(HubClientMessage.inviteResponse(invite.inviteID, accept: false)); pendingResponse = nil } }
        } else {
            invites.removeAll { $0.inviteID == invite.inviteID }; invites.insert(invite, at: 0); snoozed.remove(invite.inviteID); updateBanner()
        }
    }

    // MARK: housekeeping

    func say(_ text: String) { notice = text }
    func clearNotice() { notice = "" }

    private func tick() {
        // expired invites leave; the banner moves on; the heartbeat goes out
        let before = invites.count
        invites.removeAll { expired($0) }
        if invites.count != before || banner != nil || !invites.isEmpty { updateBanner() }
        if link == .online { publishPresence() }
    }

    /// Tests: one tick without waiting a second.
    func debugTick() { tick() }
    /// Tests: forget everything (the shared instance is reused between tests).
    func debugReset() {
        disconnect(); ticker?.cancel(); ticker = nil; started = false; joinTask?.cancel()
        friends = []; invites = []; banner = nil; sent = [:]; outgoing = [:]; notice = ""; joining = nil; resolvedInvite = nil
        party = nil; relayRoom = nil; snoozed = []; pendingResponse = nil; pendingAccept = nil; askedPush = false; hubOpen = false; you = ""
    }
}
