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

    var isScanned: Bool { calibration != nil }

    init(id: UUID = UUID(), name: String, colorIndex: Int, handedness: Handedness = .right, calibration: PlayerCalibration? = nil) {
        self.id = id
        self.name = name
        self.colorIndex = colorIndex
        self.handedness = handedness
        self.calibration = calibration
    }
}

/// Saved players. The first player is the solo player.
enum PlayerRosterStore {
    private static let key = "players.v1"

    static func load(defaults: UserDefaults = .standard) -> [Player] {
        if let data = defaults.data(forKey: key),
           let players = try? JSONDecoder().decode([Player].self, from: data) {
            // A scan from an older calibration format no longer matches; ask for a new one.
            return players.map { player in
                var player = player
                if player.calibration?.version != PlayerCalibration.currentVersion { player.calibration = nil }
                return player
            }
        }
        // Migrate the single-player scan saved before multiplayer existed.
        let legacyHandedness = defaults.string(forKey: "range.handedness").flatMap(Handedness.init(rawValue:)) ?? .right
        let player = Player(name: "Player 1", colorIndex: 0, handedness: legacyHandedness, calibration: PlayerCalibrationStore.load(defaults: defaults))
        return [player]
    }

    static func save(_ players: [Player], defaults: UserDefaults = .standard) {
        guard let data = try? JSONEncoder().encode(players) else { return }
        defaults.set(data, forKey: key)
    }
}
