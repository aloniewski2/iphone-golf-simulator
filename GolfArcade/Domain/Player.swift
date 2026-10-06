import Foundation

/// Someone who plays: their name, which side they swing from, and their body scan.
struct Player: Codable, Identifiable, Equatable, Sendable {
    let id: UUID
    var name: String
    var colorIndex: Int
    var handedness: Handedness
    var calibration: PlayerCalibration?
    // Optional storage preserves decoding of existing players.v1 profiles.
    var standardFemaleValue: Bool?
    var standardSkinValue: Int?
    var standardFemale: Bool { get { standardFemaleValue ?? false } set { standardFemaleValue=newValue } }
    var standardSkin: Int { get { max(0,min(5,standardSkinValue ?? 2)) } set { standardSkinValue=max(0,min(5,newValue)) } }
    // Outfit colours: indices into Outfit.palette (see SportProgress.swift). Optional for the
    // same reason; nil keeps the kit's own colours.
    var shirt: Int?
    var shorts: Int?
    var accent: Int?
    var racket: Int?

    /// Continuous locker colour picks (LockerColors.swift); nil keeps the preset indices below.
    var looks: [String: [Double]]?
    /// Equipped gear, sport -> slot -> item id (LockerCatalog.swift). Optional like every field added after players.v1:
    /// older profiles decode without it, and an id the catalog no longer has falls back to the Standard item.
    var loadout: [String: [String: String]]?
    /// Three controller slots. Optional storage keeps players.v1 saves compatible.
    var emoteIDs: [String]?
    var equippedEmotes: [String] { EmoteCatalog.normalized(emoteIDs) }
    mutating func equipEmote(_ id: String, slot: Int) {
        guard (0..<3).contains(slot), EmoteCatalog.ids.contains(id) else { return }
        var picks = equippedEmotes
        if let other = picks.firstIndex(of: id) { picks.swapAt(slot, other) }
        else { picks[slot] = id }
        emoteIDs = picks
    }
    var hairStyleValue: Int?
    var haircutValue: Int?
    var hairColorValue: Int?
    var faceShapeValue: Int?
    var heightValue: Int?
    var buildValue: Int?
    var bodySizeValue: Double?
    /// Continuous cosmetic width. Older five-step profiles migrate without losing their choice.
    var bodySize: Double { get { min(1, max(0, bodySizeValue ?? Double(buildChoice) / 4)) } set { bodySizeValue = min(1, max(0, newValue)) } }
    var hairStyle: Int { get { min(3,max(0,hairStyleValue ?? 1)) } set { hairStyleValue=min(3,max(0,newValue)) } }
    /// Hero V5 haircut (HeroKit.HaircutNames: Swept, Ponytail, Bob, Long, Curly). Unset follows the body: Ponytail for a girl.
    var haircut: Int { get { min(7,max(0,haircutValue ?? (standardFemale ? 1 : 0))) } set { haircutValue=min(7,max(0,newValue)) } }
    var hairColor: Int { get { min(5,max(0,hairColorValue ?? 1)) } set { hairColorValue=min(5,max(0,newValue)) } }
    var faceShape: Int { get { min(3,max(0,faceShapeValue ?? 0)) } set { faceShapeValue=min(3,max(0,newValue)) } }
    var heightChoice: Int { get { min(4,max(0,heightValue ?? 2)) } set { heightValue=min(4,max(0,newValue)) } }
    var buildChoice: Int { get { min(4,max(0,buildValue ?? 2)) } set { buildValue=min(4,max(0,newValue)) } }

    var isScanned: Bool { calibration != nil }

    init(id: UUID = UUID(), name: String, colorIndex: Int, handedness: Handedness = .right, calibration: PlayerCalibration? = nil) {
        self.id = id
        self.name = name
        self.colorIndex = colorIndex
        self.handedness = handedness
        self.calibration = calibration
    }
}

enum EmoteCatalog {
    static let ids = ["scuba", "thrust", "spike", "wave", "bringIt", "pushups"]
    static let names = ["Scuba", "Thrust", "Spike", "Wave", "Bring it", "Pushups"]
    static let defaults = ["wave", "scuba", "spike"]
    static func name(_ id: String) -> String { ids.firstIndex(of: id).map { names[$0] } ?? "Emote" }
    static func normalized(_ saved: [String]?) -> [String] {
        var result: [String] = []
        for id in (saved ?? defaults) + defaults + ids where ids.contains(id) && !result.contains(id) {
            result.append(id)
            if result.count == 3 { break }
        }
        return result
    }
    static func valid(_ picks: [String]) -> Bool {
        picks.count == 3 && Set(picks).count == 3 && picks.allSatisfy(ids.contains)
    }
}

/// Saved players. The first player is the solo player.
enum PlayerRosterStore {
    private static let key = "players.v1"

    static func load(defaults: UserDefaults = .standard) -> [Player] {
        if let data = defaults.data(forKey: key) {
            if let players = try? JSONDecoder().decode([Player].self, from: data) {
                // A scan from an older calibration format no longer matches; ask for a new one.
                return players.map { player in
                    var player = player
                    if player.calibration?.version != PlayerCalibration.currentVersion { player.calibration = nil }
                    return player
                }
            }
            // Unreadable roster: keep the old bytes under another key before the next save overwrites them,
            // so a bad decode can never silently wipe a player's look and progress.
            keepUnreadable(data, defaults: defaults)
        }
        // Migrate the single-player scan saved before multiplayer existed.
        let legacyHandedness = defaults.string(forKey: "range.handedness").flatMap(Handedness.init(rawValue:)) ?? .right
        let player = Player(name: "Player 1", colorIndex: 0, handedness: legacyHandedness, calibration: PlayerCalibrationStore.load(defaults: defaults))
        return [player]
    }

    static func save(_ players: [Player], defaults: UserDefaults = .standard) {
        do { defaults.set(try JSONEncoder().encode(players), forKey: key) }
        catch { NSLog("[PlayerRosterStore] could not encode players: %@", String(describing: error)) }
    }

    /// Backups of rosters that failed to decode (newest three kept).
    static let unreadablePrefix = "players.v1.unreadable."
    static func keepUnreadable(_ data: Data, defaults: UserDefaults = .standard) {
        let stamp = Int(Date().timeIntervalSince1970)
        defaults.set(data, forKey: unreadablePrefix + String(stamp))
        let old = defaults.dictionaryRepresentation().keys.filter { $0.hasPrefix(unreadablePrefix) }.sorted()
        for k in old.dropLast(3) { defaults.removeObject(forKey: k) }
        NSLog("[PlayerRosterStore] players.v1 was unreadable; %d bytes kept under %@%d", data.count, unreadablePrefix, stamp)
    }
}
