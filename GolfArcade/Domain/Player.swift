import Foundation

/// Player identity, handedness and appearance. Calibration is a legacy decode field only.
struct Player: Codable, Identifiable, Equatable, Sendable {
    let id: UUID
    var name: String
    var colorIndex: Int
    var handedness: Handedness
    /// Ignored and removed by the roster store when migrating camera-era saves.
    var calibration: PlayerCalibration?
    /// Optional for backward-compatible decoding of the existing players.v1 roster.
    var appearance: GolferAppearance?
    var golferAppearance: GolferAppearance { appearance ?? .forPlayerColor(colorIndex) }

    var isScanned: Bool { calibration != nil }

    init(id: UUID = UUID(), name: String, colorIndex: Int, handedness: Handedness = .right, calibration: PlayerCalibration? = nil, appearance: GolferAppearance? = nil) {
        self.id = id
        self.name = name
        self.colorIndex = colorIndex
        self.handedness = handedness
        self.calibration = calibration
        self.appearance = appearance
    }
}

/// Saved players. The first player is the solo player.
enum PlayerRosterStore {
    private static let key = "players.v1"

    static func load(defaults: UserDefaults = .standard) -> [Player] {
        PlayerCalibrationStore.clear(defaults: defaults)
        if let data = defaults.data(forKey: key),
           let players = try? JSONDecoder().decode([Player].self, from: data) {
            let migrated = players.map { player in
                var player = player
                player.calibration = nil
                return player
            }
            save(migrated, defaults: defaults)
            return migrated
        }
        // Preserve handedness, never restore the retired body scan.
        let legacyHandedness = defaults.string(forKey: "range.handedness").flatMap(Handedness.init(rawValue:)) ?? .right
        let player = Player(name: "Player 1", colorIndex: 0, handedness: legacyHandedness)
        return [player]
    }

    static func save(_ players: [Player], defaults: UserDefaults = .standard) {
        let profiles = players.map { player in
            var player = player
            player.calibration = nil
            return player
        }
        guard let data = try? JSONEncoder().encode(profiles) else { return }
        defaults.set(data, forKey: key)
    }
}
