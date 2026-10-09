import Foundation

enum MultiplayerSport: String, Codable, CaseIterable, Sendable { case tennis, golf }
/// `calibrating` is tennis only: every phone is loaded and each player points their phone at the TV and taps Ready
/// before the owner starts the match. Golf goes straight from `loading` to `playing`.
enum MultiplayerPhase: String, Codable, Sendable { case lobby, loading, calibrating, playing, results, interrupted }
enum MultiplayerError: LocalizedError {
    case unavailable(String), invalidOperation(String), incompatible, full
    var errorDescription: String? {
        switch self {
        case .unavailable(let s), .invalidOperation(let s): return s
        case .incompatible: return "The players need compatible versions of the game."
        case .full: return "The lobby already has four people, including spectators."
        }
    }
}
struct MultiplayerParticipant: Codable, Equatable, Sendable {
    var id: String
    var name: String
    var seat: Int = -1
    var ready = false
    var loaded = false
    /// Tennis: this player's phone has its court direction and centre set (touch players and spectators need nothing).
    var calibrated = false
    var connected = true
    var female = false
    var left = false
    var invited = true
    var loadout: MultiplayerLoadout?
    var paused: Bool?
    /// Nil for a real device; guests are controlled only by the host that created them.
    var controllerID: String?
    var isGuest: Bool { controllerID != nil }

}
/// Cosmetic data only. Never transmits a saved profile, calibration, mesh, or image.
struct MultiplayerLoadout: Codable, Equatable, Sendable {
    var gear: [String: [String: String]] = [:]
    var skinHex: String
    var colours: [String: String] = [:]
    var emotes: [String]?
    // Flat colour fields are also readable by Unity JsonUtility.
    var shirtHex: String?
    var shortsHex: String?

    func valid() -> Bool {
        func hex(_ s: String) -> Bool { s.utf8.count == 6 && s.allSatisfy { $0.isHexDigit && $0.isASCII } }
        return (shirtHex.map(hex) ?? true) && (shortsHex.map(hex) ?? true) && (emotes.map(EmoteCatalog.valid) ?? true) && hex(skinHex) && colours.count <= 4 && colours.allSatisfy {
            ["shirt", "shorts", "accent", "racket"].contains($0.key) && hex($0.value)
        } && gear.count <= 2 && gear.allSatisfy { sport, slots in
            ["tennis", "golf"].contains(sport) && slots.count <= 3 && slots.allSatisfy {
                ["skin", "racket", "club", "shoes"].contains($0.key) && !$0.value.isEmpty && $0.value.utf8.count <= 64
            }
        }
    }
}
struct MultiplayerLookUpdate: Codable, Sendable {
    var participantID: String
    var name: String?
    var female: Bool?
    var left: Bool?
    var loadout: MultiplayerLoadout?
}
struct MultiplayerEmote: Codable, Equatable, Sendable, Identifiable {
    var participantID: String
    var emoteID: String
    var startedAt: Double
    var eventID: String?
    var id: String { eventID ?? "\(participantID):\(startedAt)" }
    static let ids = EmoteCatalog.ids
    static let names = EmoteCatalog.names
    static let cooldown = 1.5
}
struct MultiplayerLobby: Codable, Equatable, Sendable {
    var id = UUID().uuidString
    var ownerID: String
    var revision = 0
    var sport: MultiplayerSport = .tennis
    var venue = "resort"
    var sets = 1
    var games = 3
    var phase: MultiplayerPhase = .lobby
    var participants: [MultiplayerParticipant]
    var queue: [String] = []
    var matchID = ""
    var publicAdmission = false
    var competitors: [MultiplayerParticipant] { participants.filter { $0.seat >= 0 && $0.connected }.sorted { $0.seat < $1.seat } }
    var capacity: Int { sport == .tennis ? 2 : 4 }
    var canStart: Bool {
        phase == .lobby && competitors.count >= 2 && (sport != .tennis || competitors.count == 2)
        && participants.filter { $0.seat >= 0 }.allSatisfy(\.connected)
        && competitors.allSatisfy(\.ready)
    }
    mutating func configure(sport: MultiplayerSport, venue: String) {
        self.sport = sport; self.venue = venue
        for i in participants.indices { participants[i].seat = i < capacity ? i : -1; participants[i].ready = false; participants[i].loaded = false; participants[i].calibrated = false }
        queue.removeAll(); revision += 1
    }
    mutating func add(_ player: MultiplayerParticipant) throws {
        guard !participants.contains(where: { $0.id == player.id }) else { return }
        guard participants.count < 4 else { throw MultiplayerError.full }
        var p = player
        if phase == .lobby { p.seat = (0..<capacity).first { n in !participants.contains { $0.seat == n } } ?? -1 }
        else { p.seat = -1 }
        participants.append(p); revision += 1
    }
    mutating func assign(_ id: String, seat: Int) throws {
        guard phase == .lobby, (-1..<capacity).contains(seat), let i = participants.firstIndex(where: { $0.id == id }) else {
            throw MultiplayerError.invalidOperation("Seats can change only between matches.")
        }
        guard seat == -1 || !participants.contains(where: { $0.id != id && $0.seat == seat }) else {
            throw MultiplayerError.invalidOperation("That seat is occupied.")
        }
        participants[i].seat = seat; participants[i].ready = false; queue.removeAll { $0 == id }; revision += 1
    }
    func valid() -> Bool {
        let seats = participants.filter { $0.seat >= 0 }.map(\.seat)
        return participants.count <= 4 && !participants.isEmpty && participants.contains { $0.id == ownerID }
            && Set(participants.map(\.id)).count == participants.count && Set(seats).count == seats.count
            && participants.allSatisfy { $0.controllerID == nil || (sport == .golf && $0.controllerID == ownerID && $0.id != ownerID) }
            && participants.allSatisfy { !$0.id.isEmpty && (-1..<capacity).contains($0.seat) }
            && participants.allSatisfy { $0.id.utf8.count <= 128 && $0.name.count <= 40 && ($0.loadout?.valid() ?? true) }
            && (1...3).contains(sets) && [1,3,6].contains(games) && Self.validVenue(venue, sport: sport)
    }
    /// Existing golf course ids, plus resort for saved / legacy configurations.
    static let golfVenues = ["cliffside", "postcards", "wildisles", "magma", "meadow", "resort"]
    static func validVenue(_ venue: String, sport: MultiplayerSport) -> Bool {
        (sport == .golf ? golfVenues : ["resort", "skyscraper", "volcano"]).contains(venue)
    }
    var startReason: String {
        if phase != .lobby { return "Return to the lobby first" }
        if let p = participants.first(where: { $0.seat >= 0 && !$0.connected }) { return "Waiting for \(p.name) to reconnect" }
        if competitors.count < 2 { return "Invite another player to play" }
        if let p = competitors.first(where: { !$0.ready }) { return "Waiting for \(p.name) to be ready" }
        return "Everyone is ready"
    }
}
struct MultiplayerMatchConfiguration: Codable, Sendable {
    var lobbyID: String
    var matchID: String
    var hostID: String
    var localID: String
    var sport: String
    var venue: String
    var sets: Int
    var games: Int
    var seed: Int
    var participants: [MultiplayerParticipant]
    var usesHostGolfDisplay: Bool { sport == "golf" && localID != hostID }
    func valid() -> Bool {
        guard let mode = MultiplayerSport(rawValue: sport), !lobbyID.isEmpty, !matchID.isEmpty,
              participants.contains(where: { $0.id == localID }), participants.filter({ $0.seat >= 0 }).count >= 2 else { return false }
        return MultiplayerLobby(id: lobbyID, ownerID: hostID, sport: mode, venue: venue, sets: sets, games: games, participants: participants).valid()
    }
}
/// The transport's actual sender overrides the packet's claimed sender before delivery.
struct MultiplayerPacket: Codable, Sendable {
    static let version = 4
    static let maximumBytes = 60_000
    var version = Self.version
    var lobbyID = ""
    var matchID = ""
    var sender = ""
    var sequence: Int64 = 0
    var kind: String
    var reliable = true
    var sentAt: Double = 0
    var payload = ""
}
@MainActor
protocol MultiplayerTransport: AnyObject {
    var localID: String { get }
    var peers: [String] { get }
    var onData: ((Data, String) -> Void)? { get set }
    var onPeersChanged: (() -> Void)? { get set }
    var onError: ((Error) -> Void)? { get set }
    func send(_ data: Data, to peers: [String]?, reliable: Bool) throws
    func disconnect()
}

/// Keeps the shared clock steady. A ping/pong gives one sample: `rtt` and `offset` (the owner's clock minus ours).
/// One slow leg skews a single sample by half the extra delay, so keep the samples with the lowest rtt (least
/// queueing) and slew toward their median. Mirrors `ClockFilterModel` in Tools/netsim (the same fixed vector is
/// asserted in MultiplayerTests), where the algorithm is validated against simulated jitter spikes.
struct ClockFilter {
    static let window = 16, best = 3, rttWindow = 8
    static let maxSlew = 0.005, jumpThreshold = 0.1
    private(set) var offset = 0.0
    private(set) var locked = false
    private var samples: [(rtt: Double, offset: Double)] = []
    private var rtts: [Double] = []
    var medianRTT: Double { Self.median(rtts) }

    @discardableResult
    mutating func add(rtt: Double, offset raw: Double) -> Double {
        guard rtt.isFinite, raw.isFinite, rtt >= 0 else { return offset }
        samples.append((rtt: rtt, offset: raw)); if samples.count > Self.window { samples.removeFirst() }
        rtts.append(rtt); if rtts.count > Self.rttWindow { rtts.removeFirst() }
        let lowest = samples.sorted { ($0.rtt, $0.offset) < ($1.rtt, $1.offset) }.prefix(Self.best).map { $0.offset }
        let target = Self.median(lowest)
        if !locked || abs(target - offset) > Self.jumpThreshold { offset = target; locked = true }
        else { offset += max(-Self.maxSlew, min(Self.maxSlew, target - offset)) }
        return offset
    }

    static func median(_ values: [Double]) -> Double {
        guard !values.isEmpty else { return 0 }
        let sorted = values.sorted(), n = sorted.count
        return n % 2 == 1 ? sorted[n / 2] : (sorted[n / 2 - 1] + sorted[n / 2]) / 2
    }
}

/// Timing rules for noticing that a player's connection has gone quiet (see PLAN_Multiplayer_OnlineLocal.md, P1.4).
enum MultiplayerTuning {
    /// A competitor the owner has heard nothing from (no packet of any kind) for this long pauses the match for everyone.
    static let silenceSeconds = 0.4
    /// Packets heard from a quiet competitor before play resumes.
    static let resumeBeats = 3
    /// How often a competitor's phone tells the owner it is still there during play (10 per second).
    static let heartbeatInterval = 0.1
    /// Seconds after the shared start before silence is judged: the first frames of a freshly loaded scene can stall the app.
    static let silenceGrace = 1.0
    /// Apple limits how big an "unreliable" message may be and does not say by how much. Only a refusal of a message bigger
    /// than this makes us send that kind reliably instead; a small one that fails is some other problem and is rethrown.
    static let unreliableSafeBytes = 900
    /// A competitor that comes back mid-match reloads the game and points its phone at the TV again before play resumes
    /// for it; that takes longer than the plain 15 s a dropped connection is given.
    static let recalibrationSeconds = 45.0
    /// An unfinished setup is mentioned to everyone after this long (nobody is forced to wait: Leave is always there).
    static let calibrationNoticeSeconds = 60.0
}

/// Local statistics for tuning once the app is live. Nothing is uploaded: the line goes to SportsDiagnostics.log when a match ends.
struct NetStats {
    private(set) var rtts: [Double] = []
    var pingsSent = 0, pongsHeard = 0, silences = 0, resumes = 0, drops = 0
    var largestPacket = 0, unreliableRefused = 0

    mutating func note(rtt: Double) {
        guard rtt.isFinite, rtt >= 0 else { return }
        pongsHeard += 1; rtts.append(rtt); if rtts.count > 300 { rtts.removeFirst() }
    }
    var medianRTT: Double { ClockFilter.median(rtts) }
    var p95RTT: Double {
        guard !rtts.isEmpty else { return 0 }
        let sorted = rtts.sorted()
        return sorted[min(sorted.count - 1, Int(Double(sorted.count) * 0.95))]
    }
    /// Mean change between successive round trips: a simple measure of how jumpy the link is.
    var jitter: Double {
        guard rtts.count > 1 else { return 0 }
        return zip(rtts, rtts.dropFirst()).map { abs($1 - $0) }.reduce(0, +) / Double(rtts.count - 1)
    }
    var lossPercent: Double { pingsSent == 0 ? 0 : max(0, 100 * Double(pingsSent - pongsHeard) / Double(pingsSent)) }
    var isEmpty: Bool { pingsSent == 0 && rtts.isEmpty && silences == 0 && largestPacket == 0 }
    var summary: String {
        String(format: "rtt median %.0f ms, p95 %.0f ms, jitter %.0f ms, ping loss %.1f%% (%d of %d answered), quiet pauses %d (resumed %d, dropped %d), largest packet %d bytes, unreliable refused %d",
               medianRTT * 1000, p95RTT * 1000, jitter * 1000, lossPercent, pongsHeard, pingsSent, silences, resumes, drops, largestPacket, unreliableRefused)
    }
}
