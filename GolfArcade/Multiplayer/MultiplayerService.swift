import Foundation
import Observation
import UIKit
import GameKit

/// Public integration surface for future UI. Creating the singleton never starts networking.
@MainActor @Observable
final class MultiplayerService {
    static let shared = MultiplayerService()
    private(set) var lobby: MultiplayerLobby?
    private(set) var lastError: String?
    private(set) var searching = false
    private(set) var loadingNeedsDecision = false
    private(set) var discoveredLobbies: [LocalMultiplayerTransport.DiscoveredLobby] = []
    private(set) var pendingInvite: GKInvite?
    private(set) var roundTrip: Double = 0
    private(set) var emotes: [String: MultiplayerEmote] = [:]
    #if DEBUG
    @ObservationIgnored var proofRecording = false
    @ObservationIgnored private(set) var proofSnapshot: String?
    @ObservationIgnored private var proofPeers: Set<String> = []
    @ObservationIgnored private var proofKinds: Set<String> = []
    #endif
    var onMatchRequested: ((MultiplayerMatchConfiguration) -> Void)?
    var onReturnToLobby: (() -> Void)?
    var onResult: ((String) -> Void)?
    @ObservationIgnored private var transport: (any MultiplayerTransport)?
    @ObservationIgnored private var gameCenter: GameCenterTransport?
    @ObservationIgnored private var local: LocalMultiplayerTransport?
    @ObservationIgnored private var timer: Timer?
    @ObservationIgnored private var encoder = JSONEncoder()
    @ObservationIgnored private var decoder = JSONDecoder()
    @ObservationIgnored private var sequence: Int64 = 0
    @ObservationIgnored private var seen: [String: Int64] = [:]
    @ObservationIgnored private var hello: [String: MultiplayerParticipant] = [:]
    @ObservationIgnored private var disconnected: [String: Double] = [:]
    @ObservationIgnored private var epoch = 0
    @ObservationIgnored private var prepared = false
    @ObservationIgnored private var loadingStarted: Double = 0
    @ObservationIgnored private var presentationReadyAt: [String: Double] = [:]
    @ObservationIgnored private var scheduledRunAt: Double = 0
    @ObservationIgnored private var winnerSeat: Int = -1
    @ObservationIgnored private var searchGeneration = 0
    @ObservationIgnored private var matchConfiguration: MultiplayerMatchConfiguration?
    @ObservationIgnored private var quickSport: MultiplayerSport?
    @ObservationIgnored private var clockOffset: Double = 0
    @ObservationIgnored private var clockFilter = ClockFilter()
    @ObservationIgnored private var nextPing: Double = 0
    @ObservationIgnored private var nextHello: Double = 0
    // Noticing a player who goes quiet without Apple (or Wi-Fi) reporting a disconnect.
    @ObservationIgnored private var lastHeard: [String: Double] = [:]
    @ObservationIgnored private var silent: [String: Int] = [:]
    @ObservationIgnored private var nextHeartbeat: Double = 0
    @ObservationIgnored private var silenceArmedAt: Double = .infinity
    @ObservationIgnored private var stats = NetStats()
    // Tennis setup between loading and play: when it began, whether the "still waiting" note went out, and which competitors
    // reloaded mid-match (play stays paused for them until they point their phone at the TV again).
    @ObservationIgnored private var calibrationStarted: Double = 0
    @ObservationIgnored private var calibrationNoticed = false
    @ObservationIgnored private var awaitingCalibration: Set<String> = []
    /// Kinds Apple refused to send unreliably (too big): sent reliably from then on.
    @ObservationIgnored private var unreliableRefused: Set<String> = []
    @ObservationIgnored private var rate: [String: (Double, Int)] = [:]
    @ObservationIgnored private var name = "Player"
    @ObservationIgnored private var female = false
    @ObservationIgnored private var left = false
    @ObservationIgnored private var loadout: MultiplayerLoadout?
    @ObservationIgnored private var lastLook: [String: Double] = [:]
    @ObservationIgnored private var lastEmote: [String: Double] = [:]
    @ObservationIgnored private var localEmoteAt = -Double.infinity
    @ObservationIgnored private let runtimeSend: @MainActor (String) -> Bool
    @ObservationIgnored private let runtimePoll: @MainActor () -> String?
    @ObservationIgnored private let clock: @MainActor () -> Double
    var localID: String { transport?.localID ?? "" }
    var isOwner: Bool { lobby?.ownerID == localID && !localID.isEmpty }
    var localSeat: Int { lobby?.participants.first { $0.id == localID }?.seat ?? -1 }
    private var now: Double { clock() }
    var networkTime: Double { now + (isOwner ? 0 : clockOffset) }
    var emoteCooldown: Double { max(0, MultiplayerEmote.cooldown - (now - localEmoteAt)) }
    @ObservationIgnored private var sharedPhoneHost = false
    var isNearby: Bool { (local != nil || sharedPhoneHost) && transport != nil }
    var localJoinCode: String? { isNearby && isOwner ? local?.joinCode : nil }
    func joinLocal(code: String) throws {
        let clean = code.trimmingCharacters(in: .whitespacesAndNewlines).uppercased()
        let matches = discoveredLobbies.filter { LocalMultiplayerTransport.joinCode(for: $0.id.components(separatedBy: "~").last ?? "") == clean }
        guard clean.count == 6, matches.count == 1, let item = matches.first else {
            throw MultiplayerError.invalidOperation("Lobby not found. Check the six-character code and use the same Wi-Fi.")
        }
        try joinLocal(item)
    }
    func addLocalGuest() throws {
        try requireOwnerIdle()
        guard isNearby, lobby?.sport == .golf else { throw MultiplayerError.invalidOperation("Shared-phone guests are available in local golf.") }
        let number = (1...4).first { n in !lobby!.participants.contains { $0.name == "Guest \(n)" } } ?? 4
        let palette = ["F4A65A", "78C5E8", "C997DF", "8AD0AD", "F19DAB", "F5DD7C"]
        let used = Set(lobby!.participants.compactMap { $0.loadout?.colours["shirt"] })
        let shirt = palette.filter { !used.contains($0) }.randomElement() ?? palette[0]
        let shorts = ["23344D", "F0E2CC", "425D4D"].randomElement()!
        let skin = ["F2D0AF", "D5A17C", "A8704E", "754B37"].randomElement()!
        let look = MultiplayerLoadout(skinHex: skin, colours: ["shirt":shirt,"shorts":shorts], emotes: EmoteCatalog.defaults, shirtHex:shirt, shortsHex:shorts)
        try lobby?.add(MultiplayerParticipant(id: UUID().uuidString, name: "Guest \(number)", ready: true, female: Bool.random(), loadout:look, controllerID:localID))
        publishLobby()
    }
    func removeLocalGuest() throws {
        try requireOwnerIdle()
        guard let index = lobby?.participants.lastIndex(where: { $0.controllerID == localID }) else { return }
        lobby?.participants.remove(at:index); lobby?.revision += 1; publishLobby()
    }
    var authenticated: Bool { GKLocalPlayer.local.isAuthenticated }
    func clearError() { lastError = nil }

    init(sendToRuntime: @escaping @MainActor (String) -> Bool = { SportsRuntime.shared().pushNetwork($0) },
         pollRuntime: @escaping @MainActor () -> String? = { SportsRuntime.shared().pollNetwork() },
         clock: @escaping @MainActor () -> Double = { ProcessInfo.processInfo.systemUptime }) {
        self.runtimeSend = sendToRuntime; self.runtimePoll = pollRuntime; self.clock = clock
    }
    /// Shared host path also permits an injected transport for protocol verification.
    func host(using connection: any MultiplayerTransport, sharedPhone: Bool = false) throws {
        guard transport == nil, !searching else { throw MultiplayerError.invalidOperation("Leave the current lobby first.") }
        sharedPhoneHost = sharedPhone; bind(connection); createLobby(owner: localID)
    }
    func connect(using connection: any MultiplayerTransport) throws {
        guard transport == nil, !searching else { throw MultiplayerError.invalidOperation("Leave the current lobby first.") }
        bind(connection)
    }
    /// Register invitation/lifecycle listeners without presenting authentication or matchmaking UI.
    func prepare() {
        guard !prepared, !ProcessInfo.processInfo.arguments.contains("--lobby-mock") else { return }; prepared = true
        _ = ensureGameCenter()
        NotificationCenter.default.addObserver(forName: UIApplication.didEnterBackgroundNotification, object: nil, queue: .main) { [weak self] _ in Task { @MainActor in self?.setForeground(false) } }
        NotificationCenter.default.addObserver(forName: UIApplication.didBecomeActiveNotification, object: nil, queue: .main) { [weak self] _ in Task { @MainActor in self?.setForeground(true) } }
    }
    func createOnlineLobby() throws {
        guard transport == nil, !searching else { throw MultiplayerError.invalidOperation("Already in a lobby.") }
        local?.disconnect(); local = nil; discoveredLobbies = []
        let gc=ensureGameCenter()
        guard GKLocalPlayer.local.isAuthenticated else { throw MultiplayerError.unavailable("Sign in to Game Center first.") }
        bind(gc); createLobby(owner: localID)
    }
    func setIdentity(name: String, female: Bool = false, left: Bool = false, loadout: MultiplayerLoadout? = nil) {
        guard loadout?.valid() ?? true else { return }
        self.name = String(name.prefix(40)); self.female = female; self.left = left; self.loadout = loadout
        if lobby?.phase == .lobby { try? updateLook(loadout, name: self.name, female: female, left: left) }
        else if lobby == nil, transport != nil { try? broadcast("hello", payload: json(identity())) }
    }
    func updateLook(_ look: MultiplayerLoadout?, name: String? = nil, female: Bool? = nil, left: Bool? = nil) throws {
        guard lobby?.phase == .lobby, look?.valid() ?? true else { throw MultiplayerError.invalidOperation("Change clothes between matches.") }
        let update = MultiplayerLookUpdate(participantID: localID, name: name, female: female, left: left, loadout: look)
        try sendControl("look", payload: json(update))
        self.loadout = look
        if let name { self.name = String(name.prefix(40)) }; if let female { self.female = female }; if let left { self.left = left }
    }
    /// Local response is immediate; the owner confirms the timestamp and rebroadcasts the same event id.
    func playEmote(_ id: String) throws {
        guard lobby?.phase == .lobby, MultiplayerEmote.ids.contains(id) else { throw MultiplayerError.invalidOperation("Emotes are available in the lobby.") }
        guard emoteCooldown == 0 else { throw MultiplayerError.invalidOperation("Wait a moment before the next emote.") }
        let event = MultiplayerEmote(participantID: localID, emoteID: id, startedAt: networkTime, eventID: UUID().uuidString)
        try sendControl("emote", payload: json(event))
        if !isOwner { emotes[localID] = event }
        localEmoteAt = now
    }
    func authenticate(present: @escaping (UIViewController) -> Void) async throws {
        let gc = ensureGameCenter(); try await gc.authenticate(present: present)
    }
    private func ensureGameCenter() -> GameCenterTransport {
        if let gameCenter { return gameCenter }
        let gc = GameCenterTransport(); gameCenter = gc
        gc.onInvitation = { [weak self] in self?.pendingInvite = $0 }
        gc.onConnected = { [weak self, weak gc] in if let gc { self?.bind(gc) } }
        return gc
    }
    /// Return Apple's controller to the caller. No custom lobby/online screen is added.
    func inviteFriends() throws -> UIViewController {
        guard local == nil else { throw MultiplayerError.invalidOperation("This is a local Wi-Fi lobby.") }
        let gc=ensureGameCenter()
        if transport == nil { try createOnlineLobby() }
        return try gc.invitationController()
    }
    func acceptInvitation() throws -> UIViewController {
        guard transport == nil else { throw MultiplayerError.invalidOperation("Leave the current lobby before accepting an invitation.") }
        guard let invite = pendingInvite else { throw MultiplayerError.invalidOperation("There is no pending invitation.") }
        let result = try ensureGameCenter().accept(invite); pendingInvite = nil; return result
    }
    func quickMatch(_ sport: MultiplayerSport, golfPlayers: Int = 4) async throws {
        guard transport == nil, !searching else { throw MultiplayerError.invalidOperation("Leave the current lobby first.") }
        searching = true; quickSport = sport
        searchGeneration += 1; let operation = searchGeneration
        defer { if operation == searchGeneration { searching = false } }
        do { try await ensureGameCenter().quickMatch(sport: sport, count: golfPlayers) }
        catch { if operation == searchGeneration { quickSport = nil; lastError = error.localizedDescription }; throw error }
    }
    func cancelSearch() { searchGeneration += 1; gameCenter?.cancelSearch(); searching = false; if transport == nil { quickSport = nil } }
    func hostLocal(name: String = "Friends") throws {
        guard transport == nil, !searching else { throw MultiplayerError.invalidOperation("Leave the current lobby first.") }
        local?.disconnect()
        let lan = LocalMultiplayerTransport(); local = lan; bind(lan)
        do { try lan.host(name: name); createLobby(owner: localID) }
        catch { leave(); throw error }
    }
    func browseLocal() {
        guard transport == nil else { return }
        let lan = local ?? LocalMultiplayerTransport(); local = lan
        lan.onError = { [weak self] in self?.lastError = $0.localizedDescription }
        lan.onDiscovery = { [weak self] in self?.discoveredLobbies = $0 }; lan.browse()
    }
    func stopBrowsing() { if transport == nil { local?.disconnect(); local = nil; discoveredLobbies = [] } }
    func swapSeat(_ incoming: String, with outgoing: String) throws {
        try requireOwnerIdle()
        guard var state = lobby, let seat = state.participants.first(where: { $0.id == outgoing })?.seat,
              seat >= 0, state.participants.contains(where: { $0.id == incoming && $0.seat == -1 }) else { throw MultiplayerError.invalidOperation("Choose a spectator and a playing seat.") }
        try state.assign(outgoing, seat: -1); try state.assign(incoming, seat: seat); lobby = state; publishLobby()
    }
    func requestReturnToLobby() throws {
        guard lobby?.phase == .results || lobby?.phase == .interrupted else { throw MultiplayerError.invalidOperation("The match must finish first.") }
        if isOwner { try returnToLobby() } else { try sendControl("return") }
    }
    func joinLocal(_ item: LocalMultiplayerTransport.DiscoveredLobby) throws {
        guard transport == nil, !searching else { throw MultiplayerError.invalidOperation("Leave the current lobby first.") }
        let lan = local ?? LocalMultiplayerTransport(); local = lan; bind(lan)
        do { try lan.join(item) } catch { leave(); throw error }
    }
    func configure(_ sport: MultiplayerSport, venue: String = "resort", sets: Int = 1, games: Int = 3) throws {
        try requireOwnerIdle()
        guard MultiplayerLobby.validVenue(venue, sport: sport), (1...3).contains(sets), [1,3,6].contains(games) else { throw MultiplayerError.invalidOperation("Invalid match settings.") }
        if sport != .golf { lobby?.participants.removeAll { $0.isGuest } }
        lobby?.configure(sport: sport, venue: venue)
        for i in lobby!.participants.indices where lobby!.participants[i].isGuest { lobby!.participants[i].ready = true }
        lobby?.sets = sets; lobby?.games = games; publishLobby()
    }
    func assignSeat(_ player: String, seat: Int) throws { try requireOwnerIdle(); try lobby?.assign(player, seat: seat); publishLobby() }
    func setReady(_ ready: Bool) throws {
        if ready, onMatchRequested == nil, SportsSession.shared.active { throw MultiplayerError.invalidOperation("Finish the current sport session before becoming ready.") }
        try sendControl("ready", payload: ready ? "true" : "false")
    }
    func queueForNextMatch(_ queue: Bool = true) throws { try sendControl("queue", payload: queue ? "true" : "false") }
    func startMatch() throws {
        try requireOwnerIdle()
        guard onMatchRequested != nil || !SportsSession.shared.active else { throw MultiplayerError.invalidOperation("Finish the current sport session first.") }
        guard let lobby, lobby.canStart else { throw MultiplayerError.invalidOperation("At least two competitors must be ready.") }
        presentationReadyAt.removeAll(); scheduledRunAt = 0; silent.removeAll(); silenceArmedAt = .infinity; stats = NetStats()
        awaitingCalibration.removeAll(); calibrationNoticed = false
        let id = UUID().uuidString
        self.lobby?.matchID = id; self.lobby?.phase = .loading; emotes.removeAll()
        loadingStarted = now; loadingNeedsDecision = false; winnerSeat = -1; quickSport = nil
        for i in self.lobby!.participants.indices { self.lobby!.participants[i].loaded = false; self.lobby!.participants[i].calibrated = false }
        self.lobby?.revision += 1; publishLobby()
        let config = MultiplayerMatchConfiguration(lobbyID: lobby.id, matchID: id, hostID: localID, localID: "", sport: lobby.sport.rawValue, venue: lobby.venue, sets: lobby.sets, games: lobby.games, seed: Int.random(in: 1...Int(Int32.max)), participants: lobby.participants)
        let payload = try json(config)
        try broadcast("launch", payload: payload); launch(payload)
    }
    func rematch() throws {
        guard isOwner, lobby?.phase == .results,
              (lobby?.competitors.filter(\.connected).count ?? 0) >= 2 else {
            throw MultiplayerError.invalidOperation("The host can rematch after the result with at least two connected competitors.")
        }
        try returnToLobby(rotateSeats: false)
        for i in lobby!.participants.indices {
            lobby!.participants[i].ready = lobby!.participants[i].connected && lobby!.participants[i].seat >= 0
        }
        try startMatch()
    }
    func returnToLobby(rotateSeats: Bool = true) throws {
        guard isOwner else { throw MultiplayerError.invalidOperation("Only the lobby owner can end the match.") }
        try broadcast("stop"); stopRuntime()
        lobby?.phase = .lobby; lobby?.matchID = ""
        lobby?.participants.removeAll { !$0.connected }
        let remaining = Set(lobby!.participants.map(\.id))
        lobby?.queue.removeAll { !remaining.contains($0) }
        if rotateSeats && lobby?.sport == .tennis { rotate() }
        for i in lobby!.participants.indices { lobby!.participants[i].ready = lobby!.participants[i].isGuest; lobby!.participants[i].loaded = false; lobby!.participants[i].calibrated = false }
        awaitingCalibration.removeAll()
        lobby?.revision += 1; publishLobby()
    }
    func findMorePlayers() async throws {
        try requireOwnerIdle(); guard !searching, let gc = gameCenter, transport === gc else { throw MultiplayerError.invalidOperation("Public matching needs an idle online lobby.") }
        guard lobby!.participants.count < 4 else { throw MultiplayerError.full }
        lobby?.publicAdmission = true; publishLobby(); searching = true
        searchGeneration += 1; let operation = searchGeneration
        defer { if operation == searchGeneration { searching = false; lobby?.publicAdmission = false; lobby?.revision += 1; publishLobby() } }
        try await gc.findMore(sport: lobby!.sport)
    }
    func leave() {
        sharedPhoneHost = false
        if transport != nil { try? broadcast("leave") }
        epoch += 1; searchGeneration += 1; timer?.invalidate(); timer = nil; stopRuntime()
        transport?.onPeersChanged = nil; transport?.disconnect(); transport = nil; local = nil
        #if DEBUG
        proofPeers.removeAll();proofSnapshot=nil
        #endif
        loadingNeedsDecision = false; lobby = nil; hello.removeAll(); seen.removeAll(); disconnected.removeAll(); rate.removeAll(); matchConfiguration = nil; quickSport = nil; searching = false
        lastLook.removeAll(); lastEmote.removeAll(); emotes.removeAll(); localEmoteAt = -Double.infinity; clockOffset = 0; clockFilter = ClockFilter(); nextPing = 0; lastHeard.removeAll(); silent.removeAll(); nextHeartbeat = 0; silenceArmedAt = .infinity
        awaitingCalibration.removeAll(); calibrationNoticed = false
    }
    private func requireOwnerIdle() throws {
        guard isOwner, lobby?.phase == .lobby else { throw MultiplayerError.invalidOperation("The owner can change this only in the lobby.") }
    }
    private func bind(_ connection: any MultiplayerTransport) {
        if transport === connection { peersChanged(); return }
        transport = connection; epoch += 1; let token = epoch
        connection.onData = { [weak self] data, peer in self?.receive(data, from: peer) }
        connection.onPeersChanged = { [weak self] in self?.peersChanged() }
        connection.onError = { [weak self] error in
            guard let self else { return }; self.lastError = error.localizedDescription
            if self.matchConfiguration != nil {
                if self.isOwner { self.interrupt(error.localizedDescription) }
                else { try? self.sendControl("availability", payload: "false"); self.stopRuntime() }
            }
        }
        timer?.invalidate(); let t = Timer(timeInterval: 1.0 / 60, repeats: true) { [weak self] _ in MainActor.assumeIsolated { self?.update() } }
        timer = t; RunLoop.main.add(t, forMode: .common); peersChanged()
        // Existing owners respond to hello before the initial deterministic election.
        Task { @MainActor [weak self] in
            try? await Task.sleep(for: .milliseconds(750))
            guard let self, self.epoch == token, self.lobby == nil, self.local == nil, let transport = self.transport else { return }
            let owner = ([transport.localID] + transport.peers).sorted().first!
            if owner == self.localID { self.createLobby(owner: owner) }
        }
    }
    private func createLobby(owner: String) {
        var me = identity(); me.seat = 0; lobby = MultiplayerLobby(ownerID: owner, participants: [me])
        for p in hello.values.sorted(by: { $0.id < $1.id }) { try? lobby?.add(p) }
        if let sport = quickSport { lobby?.configure(sport: sport, venue: "resort") }
        publishLobby()
        if quickSport != nil { try? setReady(true) }
    }
    private func identity() -> MultiplayerParticipant { MultiplayerParticipant(id: localID, name: name, female: female, left: left, invited: quickSport == nil, loadout: loadout) }
    private func peersChanged() {
        guard let transport else { return }
        try? broadcast("hello", payload: (try? json(identity())) ?? "")
        if isOwner {
            for i in lobby!.participants.indices where lobby!.participants[i].id != localID && !lobby!.participants[i].isGuest {
                let id = lobby!.participants[i].id, connected = transport.peers.contains(id)
                #if DEBUG
                if proofPeers.contains(id) { continue }
                #endif
                if !connected && lobby!.participants[i].connected { disconnected[id] = now; lobby!.participants[i].connected = false
                    if lobby!.participants[i].seat >= 0 { try? broadcast("suspend", payload: id); pushRuntime("suspend", payload: id) }
                }
                if connected {
                    if disconnected[id] != nil, [.loading,.calibrating,.playing].contains(lobby!.phase) { lobby!.participants[i].loaded = false; lobby!.participants[i].calibrated = false }
                    else { disconnected.removeValue(forKey: id) }
                    lobby!.participants[i].connected = true
                }
            }
            lobby?.revision += 1; publishLobby()
        } else if let owner = lobby?.ownerID, !transport.peers.contains(owner) {
            lobby?.phase = .interrupted; stopRuntime(); lastError = "The host disconnected. Leave and create a new lobby."
        }
    }
    private func receive(_ data: Data, from peer: String) {
        guard data.count <= MultiplayerPacket.maximumBytes, let transport, transport.peers.contains(peer),
              var packet = try? decoder.decode(MultiplayerPacket.self, from: data) else { return }
        guard packet.version == MultiplayerPacket.version else { lastError = MultiplayerError.incompatible.localizedDescription; return }
        let bucket = rate[peer] ?? (now, 0)
        if now - bucket.0 < 1 && bucket.1 >= 180 { return }
        rate[peer] = now - bucket.0 >= 1 ? (now,1) : (bucket.0,bucket.1+1)
        packet.sender = peer
        let key = peer + ":" + packet.kind + ":" + String(packet.reliable)
        guard packet.sequence > (seen[key] ?? -1) else { return }; seen[key] = packet.sequence
        lastHeard[peer] = now
        if silent[peer] != nil { quietPeerHeard(peer) }
        if packet.kind == "hello" {
            guard var p = try? decoder.decode(MultiplayerParticipant.self, from: Data(packet.payload.utf8)), p.loadout?.valid() ?? true else { return }
            p.controllerID = nil; p.id = peer; p.name = String(p.name.prefix(40)); p.seat = -1; p.ready = false; p.loaded = false; p.connected = true; p.invited = !(lobby?.publicAdmission ?? false)
            hello[peer] = p
            if isOwner { do {
                try lobby?.add(p); publishLobby()
                if var config=matchConfiguration, [.loading,.calibrating,.playing,.results].contains(lobby!.phase), lobby!.competitors.count >= 2 {
                    config.localID=""; config.participants=lobby!.participants
                    try transmit("launch", payload: json(config), to: [peer])
                }
            } catch { lastError = error.localizedDescription; try? transmit("notice",payload:error.localizedDescription,to:[peer]) } }
            return
        }
        if packet.kind == "lobby" {
            guard let state = try? decoder.decode(MultiplayerLobby.self, from: Data(packet.payload.utf8)), state.valid(), state.ownerID == peer,
                  lobby == nil || (lobby?.ownerID == peer && lobby?.id == state.id && state.revision > lobby!.revision) else { return }
            lobby = state
            if state.phase != .loading { loadingNeedsDecision = false }
            if state.phase != .lobby { emotes.removeAll() }
            if quickSport != nil && state.phase == .lobby && !(state.participants.first { $0.id == localID }?.ready ?? true) { try? setReady(true) }
            return
        }
        guard let state = lobby, packet.lobbyID == state.id else { return }
        if packet.kind == "emote", !isOwner {
            guard peer == state.ownerID, state.phase == .lobby,
                  let event = try? decoder.decode(MultiplayerEmote.self, from: Data(packet.payload.utf8)),
                  event.startedAt.isFinite, MultiplayerEmote.ids.contains(event.emoteID), state.participants.contains(where: { $0.id == event.participantID }) else { return }
            emotes[event.participantID] = event
            #if DEBUG
            if proofRecording { OnlineLobbyProofDriver.event("emote-received","\(event.participantID) \(event.emoteID) sent=\(event.startedAt)") }
            #endif
            return
        }
        if ["ready","queue","loaded","calibrated","leave","availability","look","emote","return","keepWaiting"].contains(packet.kind) {
            if isOwner { handleControl(packet) }; return
        }
        if packet.kind == "input" {
            guard isOwner, packet.matchID == state.matchID, state.phase == .playing,
                  state.participants.contains(where: { $0.id == peer && $0.seat >= 0 && $0.connected }) else { return }
            pushRuntimePacket(packet); return
        }
        if packet.kind == "ping", isOwner { try? transmit("pong", payload: packet.payload, reliable: false, to: [peer]); return }
        guard peer == state.ownerID else { return }
        switch packet.kind {
        case "loadTimeout": if packet.matchID == state.matchID && state.phase == .loading { loadingNeedsDecision = true }
        case "loadContinue": if packet.matchID == state.matchID && state.phase == .loading { loadingNeedsDecision = false }
        case "notice": lastError = String(packet.payload.prefix(200))
        case "launch": guard packet.matchID == state.matchID, [.loading,.calibrating,.playing,.results].contains(state.phase) else { return }; launch(packet.payload)
        case "run": guard packet.matchID == state.matchID else { return }; pushRuntimePacket(packet)
        case "snapshot", "golfShot": guard packet.matchID == state.matchID else { return }; pushRuntimePacket(packet)
        case "suspend", "resumePeer", "drop": pushRuntimePacket(packet)
        case "result": guard packet.matchID == state.matchID else { return }; pushRuntimePacket(packet); rememberResult(packet.payload); onResult?(packet.payload)
        case "stop": stopRuntime()
        case "pong":
            guard let sent = Double(packet.payload) else { return }
            // One slow packet must not move the shared clock: keep the least-delayed samples and slew (ClockFilter).
            stats.note(rtt: max(0, now - sent))
            clockOffset = clockFilter.add(rtt: max(0, now - sent), offset: packet.sentAt - (sent + now) / 2)
            roundTrip = clockFilter.medianRTT
            pushRuntime("clock", payload: String(clockOffset))
        default: break
        }
    }
    /// A competitor that went quiet is heard again: after a few packets, resume play.
    private func quietPeerHeard(_ id: String) {
        guard isOwner, let heard = silent[id] else { return }
        silent[id] = heard + 1
        guard heard + 1 >= MultiplayerTuning.resumeBeats else { return }
        silent.removeValue(forKey: id); disconnected.removeValue(forKey: id); stats.resumes += 1
        try? broadcast("resumePeer", payload: id); pushRuntime("resumePeer", payload: id)
    }
    private func handleControl(_ p: MultiplayerPacket) {
        guard let i = lobby?.participants.firstIndex(where: { $0.id == p.sender }) else { return }
        switch p.kind {
        case "keepWaiting":
            guard lobby?.phase == .loading, p.matchID == lobby?.matchID, loadingNeedsDecision else { return }
            loadingStarted = now; loadingNeedsDecision = false; try? broadcast("loadContinue"); return
        case "return": if lobby?.phase == .results || lobby?.phase == .interrupted { try? returnToLobby() }; return
        case "look":
            guard lobby?.phase == .lobby, now - (lastLook[p.sender] ?? -Double.infinity) >= 0.1 - 1e-6,
                  let update = try? decoder.decode(MultiplayerLookUpdate.self, from: Data(p.payload.utf8)),
                  update.participantID == p.sender, update.loadout?.valid() ?? true else { return }
            lastLook[p.sender] = now; lobby!.participants[i].loadout = update.loadout
            #if DEBUG
            if proofRecording { OnlineLobbyProofDriver.event("look-accepted","\(p.sender) sent=\(p.sentAt)") }
            #endif
            if let name = update.name { lobby!.participants[i].name = String(name.prefix(40)) }
            if let female = update.female { lobby!.participants[i].female = female }
            if let left = update.left { lobby!.participants[i].left = left }
        case "emote":
            guard lobby?.phase == .lobby, now - (lastEmote[p.sender] ?? -Double.infinity) >= MultiplayerEmote.cooldown,
                  var event = try? decoder.decode(MultiplayerEmote.self, from: Data(p.payload.utf8)),
                  event.participantID == p.sender, event.startedAt.isFinite, MultiplayerEmote.ids.contains(event.emoteID),
                  (event.eventID?.utf8.count ?? 0) <= 64 else { return }
            lastEmote[p.sender] = now; event.startedAt = now; emotes[p.sender] = event
            try? broadcast("emote", payload: json(event)); return
        case "ready": if lobby?.phase == .lobby {
            lobby!.participants[i].ready = p.payload == "true"
            for g in lobby!.participants.indices where lobby!.participants[g].controllerID == p.sender { lobby!.participants[g].ready = p.payload == "true" }
        }
        case "queue": if lobby!.participants[i].seat == -1 {
            lobby!.queue.removeAll { $0 == p.sender }; if p.payload == "true" { lobby!.queue.append(p.sender) }
        }
        case "loaded": if p.matchID == lobby?.matchID {
            let reported = Double(p.payload) ?? now
            guard reported.isFinite else { return }
            presentationReadyAt[p.sender] = max(now, min(now + 2, reported))
            lobby!.participants[i].loaded = true
            for g in lobby!.participants.indices where lobby!.participants[g].controllerID == p.sender {
                lobby!.participants[g].loaded = true
                presentationReadyAt[lobby!.participants[g].id] = presentationReadyAt[p.sender]
            }
            silent.removeValue(forKey: p.sender); lastHeard[p.sender] = now
            // A tennis competitor that has just (re)loaded has no court direction yet, whatever it reported before.
            let setUpAgain = lobby!.sport == .tennis && lobby!.participants[i].seat >= 0 && [.calibrating, .playing].contains(lobby!.phase)
            if setUpAgain { lobby!.participants[i].calibrated = false }
            if setUpAgain, lobby!.phase == .playing {
                // Mid-match: play stays paused for this player until it reports `calibrated` (it gets longer than a plain
                // dropped connection to do so).
                awaitingCalibration.insert(p.sender)
                if disconnected[p.sender] == nil { try? broadcast("suspend", payload: p.sender); pushRuntime("suspend", payload: p.sender) }
                disconnected[p.sender] = now
            } else if disconnected.removeValue(forKey: p.sender) != nil {
                try? broadcast("resumePeer", payload: p.sender); pushRuntime("resumePeer", payload: p.sender)
            }
            if lobby?.phase == .playing || lobby?.phase == .results { try? transmit("run", payload: String(scheduledRunAt), to: [p.sender]); pushRuntime("snapshotRequest") }
        }
        case "calibrated":
            // Tennis only: the player has pointed the phone at the TV and tapped Ready (touch players report at once).
            guard p.matchID == lobby?.matchID, lobby!.sport == .tennis, lobby!.participants[i].seat >= 0,
                  [.calibrating, .playing].contains(lobby!.phase) else { return }
            lobby!.participants[i].calibrated = true
            silent.removeValue(forKey: p.sender); lastHeard[p.sender] = now
            awaitingCalibration.remove(p.sender)
            if lobby!.phase == .playing, disconnected.removeValue(forKey: p.sender) != nil {
                try? broadcast("resumePeer", payload: p.sender); pushRuntime("resumePeer", payload: p.sender)
            }
        case "leave":
            if lobby!.participants[i].seat >= 0 && lobby!.phase == .playing { lastError = "\(lobby!.participants[i].name) left the match." }
            if lobby!.participants[i].seat >= 0 { try? broadcast("drop", payload: p.sender); pushRuntime("drop", payload: p.sender) }
            lobby!.participants.remove(at: i); lobby!.queue.removeAll { $0 == p.sender }; awaitingCalibration.remove(p.sender)
        case "availability":
            lobby!.participants[i].paused = p.payload == "false"
            if lobby!.participants[i].seat >= 0 && lobby!.phase == .playing {
                if p.payload == "false" { disconnected[p.sender] = now; try? broadcast("suspend", payload: p.sender); pushRuntime("suspend", payload: p.sender) }
                // Coming back to the foreground does not resume a player who still has to point at the TV.
                else if !awaitingCalibration.contains(p.sender) { disconnected.removeValue(forKey: p.sender); silent.removeValue(forKey: p.sender); lastHeard[p.sender] = now; try? broadcast("resumePeer", payload: p.sender); pushRuntime("resumePeer", payload: p.sender) }
            }
        default: break
        }
        lobby?.revision += 1; publishLobby()
        if lobby?.phase == .loading, lobby!.competitors.count >= 2, lobby!.competitors.allSatisfy(\.loaded) {
            loadingNeedsDecision = false
            if lobby!.sport == .tennis { beginCalibration() } else { beginPlay() }
        } else if lobby?.phase == .calibrating, lobby!.competitors.count >= 2, lobby!.competitors.allSatisfy({ $0.loaded && $0.calibrated }) {
            beginPlay()
        } else if abortSetupIfShort() {
            return
        } else if quickSport != nil, lobby?.canStart == true { try? startMatch() }
    }
    /// Everyone has loaded. Tennis now lets each player set up their controller; the owner starts play when all are done.
    private func beginCalibration() {
        calibrationStarted = now; calibrationNoticed = false; awaitingCalibration.removeAll()
        for i in lobby!.participants.indices { lobby!.participants[i].calibrated = false }
        lobby!.phase = .calibrating; lobby!.revision += 1; publishLobby()
    }
    /// Schedule the shared start (after the slowest phone's loading cover has gone) and tell everyone.
    private func beginPlay() {
        scheduledRunAt = max(now, lobby!.competitors.map { presentationReadyAt[$0.id] ?? now }.max() ?? now) + max(0.5, roundTrip * 2)
        lobby!.phase = .playing; lobby!.revision += 1; publishLobby()
        try? broadcast("run", payload: String(scheduledRunAt)); pushRuntime("run", payload: String(scheduledRunAt))
        silenceArmedAt = scheduledRunAt + MultiplayerTuning.silenceGrace; silent.removeAll()
        for c in lobby!.competitors { lastHeard[c.id] = now }
    }
    /// Setup cannot finish with fewer than two seated players (one left, or was dropped): go back to the lobby, not wait forever.
    @discardableResult private func abortSetupIfShort() -> Bool {
        guard isOwner, lobby?.phase == .calibrating, lobby!.participants.filter({ $0.seat >= 0 }).count < 2 else { return false }
        let text = "The other player left during setup. Everyone is back in the lobby."
        try? returnToLobby(rotateSeats: false)
        lastError = text; try? broadcast("notice", payload: text)
        return true
    }
    func runtimeLoaded(readyAfter: Double = 0) { try? sendControl("loaded", payload: String(networkTime + min(2, max(0, readyAfter)))) }
    /// Tennis: this phone's controller is set up (court direction found and centre taken, or touch controls chosen).
    func runtimeCalibrated() { try? sendControl("calibrated") }
    func keepWaitingForLoad() throws {
        guard lobby?.phase == .loading else { return }
        try sendControl("keepWaiting")
    }
    func runtimeUnavailable(_ reason: String) {
        if isOwner { interrupt(reason) }
        else { try? sendControl("availability", payload: "false"); stopRuntime(); lastError = reason }
    }
    func setForeground(_ foreground: Bool) {
        guard let state = lobby, state.phase == .playing else { return }
        if isOwner && !foreground { interrupt("The host entered the background.") }
        else { try? sendControl("availability", payload: foreground ? "true" : "false") }
    }
    private func sendControl(_ kind: String, payload: String = "") throws {
        guard let lobby else { throw MultiplayerError.invalidOperation("There is no connected lobby.") }
        if isOwner { var p = packet(kind, payload: payload); p.sender = localID; handleControl(p) }
        else { try transmit(kind, payload: payload, to: [lobby.ownerID]) }
    }
    private func publishLobby() { if let lobby { if isOwner { local?.advertise(sport:lobby.sport.rawValue,players:lobby.participants.count) }; try? broadcast("lobby", payload: (try? json(lobby)) ?? "") } }
    private func packet(_ kind: String, payload: String = "", reliable: Bool = true) -> MultiplayerPacket {
        sequence += 1
        return MultiplayerPacket(lobbyID: lobby?.id ?? "", matchID: lobby?.matchID ?? "", sender: localID, sequence: sequence, kind: kind, reliable: reliable, sentAt: now, payload: payload)
    }
    private func broadcast(_ kind: String, payload: String = "", reliable: Bool = true) throws { try transmit(kind, payload: payload, reliable: reliable, to: nil) }
    private func transmit(_ kind: String, payload: String = "", reliable: Bool = true, to: [String]? = nil) throws {
        guard transport != nil else { return }
        try sendPacket(packet(kind, payload: payload, reliable: reliable), to: to)
    }
    private func sendPacket(_ p: MultiplayerPacket, to ids: [String]?) throws {
        guard let transport else { return }
        let data = try encoder.encode(p)
        stats.largestPacket = max(stats.largestPacket, data.count)
        let reliable = p.reliable || unreliableRefused.contains(p.kind)
        do { try transport.send(data, to: ids, reliable: reliable) }
        catch {
            // Apple limits how big an unreliable message may be and does not say how much. If it refuses a big one, send that
            // kind reliably from now on; any other failure is not about size, so let it through.
            guard !reliable, data.count > MultiplayerTuning.unreliableSafeBytes else { throw error }
            unreliableRefused.insert(p.kind); stats.unreliableRefused += 1
            SportsDiagnostics.write("unreliable \(p.kind) refused at \(data.count) bytes (\(error.localizedDescription)); sending it reliably from now on")
            try transport.send(data, to: ids, reliable: true)
        }
    }
    /// Write this match's link statistics to the local diagnostics log (nothing is uploaded), then start afresh.
    private func reportStats() {
        guard !stats.isEmpty else { return }
        SportsDiagnostics.write("multiplayer \(lobby?.sport.rawValue ?? "?") \(isOwner ? "owner" : "guest"): \(stats.summary)")
        stats = NetStats()
    }
    private func json<T: Encodable>(_ value: T) throws -> String { String(decoding: try encoder.encode(value), as: UTF8.self) }
    private func launch(_ payload: String) {
        guard var config = try? decoder.decode(MultiplayerMatchConfiguration.self, from: Data(payload.utf8)), config.matchID != matchConfiguration?.matchID else { return }
        config.localID = localID
        guard config.valid(), config.hostID == lobby?.ownerID, config.lobbyID == lobby?.id, config.matchID == lobby?.matchID else { lastError = "Invalid match configuration."; return }
        guard onMatchRequested != nil || !SportsSession.shared.active else { lastError = "Finish the current sport session before joining this match."; return }
        matchConfiguration = config; quickSport = nil; lastError = nil; stats = NetStats()
        #if DEBUG
        if proofRecording { OnlineLobbyProofDriver.event("launch-config","bytes=\(payload.utf8.count) \(payload)") }
        #endif
        if let onMatchRequested { onMatchRequested(config) } else { SportsSession.shared.startMultiplayer(config) }
    }
    private func pushRuntimePacket(_ packet: MultiplayerPacket) {
        #if DEBUG
        if proofRecording && packet.kind == "run" { OnlineLobbyProofDriver.event("runtime-run",(try? json(packet)) ?? "") }
        if proofRecording && packet.kind == "snapshot" { proofSnapshot=packet.payload }
        #endif
 if let text = try? json(packet), !runtimeSend(text), matchConfiguration != nil { lastError="The Unity multiplayer bridge is unavailable or full. Re-export Unity before playing." } }
    private func pushRuntime(_ kind: String, payload: String = "") { pushRuntimePacket(packet(kind, payload: payload)) }
    private func stopRuntime() {
        reportStats()
        pushRuntime("stop"); matchConfiguration = nil
        if let onReturnToLobby { onReturnToLobby() }
        else if SportsSession.shared.multiplayerMatchID != nil { SportsSession.shared.end() }
    }
    private func rotate() {
        guard var state = lobby, let next = state.queue.first, let waiting = state.participants.firstIndex(where: { $0.id == next && $0.connected && $0.seat == -1 }) else { return }
        let vacant = (0..<2).first { n in !state.participants.contains { $0.seat == n } } ?? (winnerSeat == 1 ? 0 : 1)
        if let outgoing = state.participants.firstIndex(where: { $0.seat == vacant }) { state.participants[outgoing].seat = -1 }
        state.participants[waiting].seat = vacant; state.queue.removeFirst(); lobby = state
    }
    private func rememberResult(_ payload: String) {
        if let data = payload.data(using: .utf8), let result = try? JSONSerialization.jsonObject(with: data) as? [String: Any] { winnerSeat = result["winner"] as? Int ?? -1 }
    }
    private func interrupt(_ reason: String) {
        lobby?.phase = .interrupted; lobby?.revision += 1; publishLobby()
        try? broadcast("stop"); stopRuntime(); lastError = reason
    }
    func update() {
        guard let transport else { return }
        // Ping often enough to keep the clock filter fed (5/s in a match, 2/s otherwise). Unreliable, so a lost ping is
        // skipped instead of queueing behind other traffic and arriving late.
        if now >= nextPing { nextPing = now + (lobby?.phase == .playing ? 0.2 : 0.5)
            if !isOwner, let owner = lobby?.ownerID { stats.pingsSent += 1; try? transmit("ping", payload: String(now), reliable: false, to: [owner]) }
        }
        // A competitor's phone tells the owner it is still there, 10 times a second during play.
        if !isOwner, lobby?.phase == .playing, localSeat >= 0, now >= nextHeartbeat, let owner = lobby?.ownerID {
            nextHeartbeat = now + MultiplayerTuning.heartbeatInterval
            try? transmit("hb", reliable: false, to: [owner])
        }
        if now >= nextHello { nextHello = now + 1
            if lobby == nil { try? broadcast("hello", payload: (try? json(identity())) ?? "") }
        }
        if isOwner {
            if lobby?.phase == .loading, !loadingNeedsDecision, now - loadingStarted >= 20 {
                loadingNeedsDecision = true
                try? broadcast("loadTimeout")
            }
            // A competitor we have heard nothing from pauses the match for everyone until it is heard again. Apple only
            // reports a disconnect after a while, and meanwhile the opponent would keep scoring.
            if lobby?.phase == .playing, now >= silenceArmedAt {
                for p in lobby!.participants where p.seat >= 0 && p.connected && !p.isGuest && p.id != localID {
                    guard disconnected[p.id] == nil, silent[p.id] == nil else { continue }
                    if now - (lastHeard[p.id] ?? now) >= MultiplayerTuning.silenceSeconds {
                        silent[p.id] = 0; disconnected[p.id] = now; stats.silences += 1
                        try? broadcast("suspend", payload: p.id); pushRuntime("suspend", payload: p.id)
                    }
                }
            }
            // Setup that drags on is mentioned to everyone (nobody is forced to wait: Leave is always on screen).
            if lobby?.phase == .calibrating, !calibrationNoticed, now - calibrationStarted >= MultiplayerTuning.calibrationNoticeSeconds {
                calibrationNoticed = true
                let waiting = lobby!.competitors.filter { !$0.calibrated }.map(\.name)
                if !waiting.isEmpty {
                    let text = "Still waiting for \(waiting.joined(separator: " and ")) to finish setting up."
                    lastError = text; try? broadcast("notice", payload: text)
                }
            }
            // A dropped connection gets 15 s; a player who came back mid-match and has to point at the TV again gets longer.
            for (id, since) in disconnected where now - since >= (awaitingCalibration.contains(id) ? MultiplayerTuning.recalibrationSeconds : 15) {
                disconnected.removeValue(forKey: id); silent.removeValue(forKey: id); awaitingCalibration.remove(id); stats.drops += 1; try? broadcast("drop", payload: id); pushRuntime("drop", payload: id)
                if let i = lobby?.participants.firstIndex(where: { $0.id == id }) { lobby!.participants[i].seat = -1; lobby!.participants[i].connected = id == localID || transport.peers.contains(id) }
                lobby?.revision += 1; publishLobby()
                abortSetupIfShort()
            }
        }
        for _ in 0..<64 {
            guard let text = runtimePoll() else { break }
            #if DEBUG
            if proofRecording, let raw = try? JSONSerialization.jsonObject(with:Data(text.utf8)) as? [String:Any],let kind=raw["kind"] as? String,proofKinds.insert(kind).inserted { OnlineLobbyProofDriver.event("runtime-output",text) }
            #endif
            guard let data = text.data(using: .utf8), let outgoing = try? decoder.decode(MultiplayerPacket.self, from: data) else { continue }
            if outgoing.kind == "bridgeError", matchConfiguration != nil {
                if isOwner { interrupt(outgoing.payload) }
                else { try? sendControl("availability", payload: "false"); stopRuntime(); lastError = outgoing.payload }
                continue
            }
            guard outgoing.matchID == lobby?.matchID else { continue }
            if outgoing.kind == "stats" { SportsDiagnostics.write("unity network stats: \(outgoing.payload)"); continue }
            do {
                if outgoing.kind == "availability" { try sendControl("availability", payload: outgoing.payload) }
                else if outgoing.kind == "input" {
                    guard localSeat >= 0, lobby?.phase == .playing else { continue }
                    if isOwner { var p = packet("input", payload: outgoing.payload, reliable: outgoing.reliable); p.sender = localID; pushRuntimePacket(p) }
                    else { try transmit("input", payload: outgoing.payload, reliable: outgoing.reliable, to: [lobby!.ownerID]) }
                } else if isOwner, ["snapshot","golfShot","result"].contains(outgoing.kind) {
                    try sendPacket(packet(outgoing.kind, payload: outgoing.payload, reliable: outgoing.reliable), to: nil)
                    #if DEBUG
                    if proofRecording && outgoing.kind == "snapshot" { proofSnapshot=outgoing.payload }
                    #endif
                    if outgoing.kind == "result" { rememberResult(outgoing.payload); lobby?.phase = .results; lobby?.revision += 1; publishLobby(); onResult?(outgoing.payload) }
                }
            } catch { lastError = error.localizedDescription }
        }
    }
}

/// No sockets or Game Center: a deterministic party for screenshots and animation checks.
@MainActor private final class LobbyMockTransport: MultiplayerTransport {
    let localID = "00000000-0000-0000-0000-000000000001"
    var peers: [String] { [] }
    var onData: ((Data,String)->Void)?
    var onPeersChanged: (()->Void)?
    var onError: ((Error)->Void)?
    func send(_ data:Data,to peers:[String]?,reliable:Bool) throws { }
    func disconnect() { }
}

extension MultiplayerService {
    func enableMock(count: Int, player: Player) throws {
        leave(); setIdentity(name:player.name,female:player.standardFemale,left:player.handedness == .left,loadout:player.multiplayerLoadout)
        try host(using:LobbyMockTransport())
        let names = ["Sam","Maya","Leo"]
        for i in 1..<max(1,min(4,count)) {
            var p = Player(name:names[i-1],colorIndex:i,handedness:i == 2 ? .left : .right)
            p.standardFemale = i % 2 == 1; p.setSkin(Double(i) * 0.23)
            p.setOutfitHex("shirt",["D3F34B","FF6B4A","34435A"][i-1]); p.setOutfitHex("shorts",i == 2 ? "101D35" : "FAF8F3")
            try lobby?.add(MultiplayerParticipant(id:String(format:"00000000-0000-0000-0000-%012d",i+1),name:p.name,ready:i != 1,female:p.standardFemale,left:p.handedness == .left,loadout:p.multiplayerLoadout))
        }
        lobby?.revision += 1; publishLobby()
    }
    #if DEBUG
    func mockState(phase:MultiplayerPhase, ready:Bool = false, disconnected:Bool = false, paused:Bool = false, spectator:Bool = false, guest:Bool = false, error:String? = nil) {
        guard transport is LobbyMockTransport else { return }
        lobby?.phase = phase
        lastError = error
        if guest, let peer = lobby?.participants.first(where:{ $0.id != localID }) { lobby?.ownerID = peer.id }
        for i in lobby!.participants.indices { lobby!.participants[i].ready = ready; lobby!.participants[i].loaded = i % 2 == 0 }
        if lobby!.participants.count > 1 { lobby!.participants[1].connected = !disconnected; lobby!.participants[1].paused = paused }
        if spectator { lobby!.participants[0].seat = -1 }
        lobby?.revision += 1
    }
    func mockNearby() {
        discoveredLobbies = [LocalMultiplayerTransport.DiscoveredLobby(id:"proof-nearby",name:"Sam",endpoint:.service(name:"Sam",type:"_partysports._tcp",domain:"local.",interface:nil),sport:"golf",players:3)]
    }
    #endif
}

#if DEBUG
extension MultiplayerService {
    func breakProofConnection() { transport?.disconnect() }
    func addProofSpectators() throws {
        try requireOwnerIdle()
        for (i,name) in ["Maya","Leo"].enumerated() {
            let id="lobby-proof-\(i)";if lobby!.participants.contains(where:{$0.id==id}) {continue}
            var p=Player(name:name,colorIndex:i+2);p.standardFemale=i == 0;p.setSkin(Double(i)*0.6+0.1);p.setOutfitHex("shirt",i == 0 ? "D3F34B" : "34435A")
            try lobby?.add(MultiplayerParticipant(id:id,name:name,seat:-1,ready:true,female:p.standardFemale,loadout:p.multiplayerLoadout));proofPeers.insert(id)
        }
        for i in lobby!.participants.indices where proofPeers.contains(lobby!.participants[i].id) { lobby!.participants[i].seat = -1 }
        lobby?.revision+=1;publishLobby()
    }
    func removeProofSpectator() { guard isOwner else {return};lobby?.participants.removeAll { $0.id == "lobby-proof-1" };proofPeers.remove("lobby-proof-1");lobby?.revision+=1;publishLobby() }
    func sendProofInput(_ input:[String:Any]) throws {
        guard lobby?.phase == .playing,localSeat>=0 else { return }
        let payload=String(decoding:try JSONSerialization.data(withJSONObject:input),as:UTF8.self)
        if isOwner { pushRuntimePacket(packet("input",payload:payload)) } else { try transmit("input",payload:payload,to:[lobby!.ownerID]) }
    }
}
#endif
