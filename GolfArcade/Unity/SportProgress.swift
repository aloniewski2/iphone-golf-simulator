import SwiftUI

/// Every sport on the game-select screen. Golf and tennis play; the rest are shown, with their
/// b-roll, as coming soon.
enum Sport: String, CaseIterable, Identifiable, Hashable {
    case golf, tennis, bowling, football, boxing
    var id: String { rawValue }

    var title: String { rawValue.uppercased() }
    var tagline: String {
        switch self {
        case .golf: "Cliffside links above the ocean"
        case .tennis: "The Tropical Open · Island Circuit"
        case .bowling: "Neon lanes, perfect strikes"
        case .football: "Seaside stadium, top-corner finishes"
        case .boxing: "Beach-side ring, big padded gloves"
        }
    }
    var playable: Bool { self == .golf || self == .tennis }
    var icon: String {
        switch self {
        case .golf: "figure.golf"; case .tennis: "figure.tennis"; case .bowling: "figure.bowling"
        case .football: "figure.soccer"; case .boxing: "figure.boxing"
        }
    }
    var colors: (Color, Color) {
        switch self {
        case .golf: (Arcade.lime, Color(red: 0.10, green: 0.45, blue: 0.20))
        case .tennis: (Arcade.sky, Arcade.skyDeep)
        case .bowling: (Color(red: 0.95, green: 0.35, blue: 0.85), Color(red: 0.35, green: 0.08, blue: 0.50))
        case .football: (Arcade.sun, Arcade.sunDeep)
        case .boxing: (Arcade.crimson, Arcade.crimsonDeep)
        }
    }
    /// Only reviewed, entirely character-free videos belong here. All previous clips
    /// contain players; until suitable footage is added, show the empty venue artwork.
    var clips: [String] { [] }

}

/// Per-sport progress outside the tennis campaign: whether the first-time tutorial is done
/// (everything else in that sport stays locked until it is) and simple lifetime stats.
@MainActor @Observable
final class SportProgress {
    static let shared = SportProgress()
    private let defaults: UserDefaults
    private(set) var tutorialDone: Set<Sport>
    private(set) var matchesPlayed: Int
    private(set) var matchesWon: Int
    private(set) var bestRally: Int

    init(defaults: UserDefaults = .standard) {
        self.defaults = defaults
        tutorialDone = Set(Sport.allCases.filter { defaults.bool(forKey: Self.key($0)) })
        matchesPlayed = defaults.integer(forKey: "sport.stats.played.v1")
        matchesWon = defaults.integer(forKey: "sport.stats.won.v1")
        bestRally = defaults.integer(forKey: "sport.stats.rally.v1")
    }
    private static func key(_ s: Sport) -> String { "sport.\(s.rawValue).tutorialDone.v1" }

    func finishedTutorial(_ s: Sport) -> Bool { tutorialDone.contains(s) }
    func completeTutorial(_ s: Sport) { tutorialDone.insert(s); defaults.set(true, forKey: Self.key(s)) }
    func resetTutorials() {
        for s in Sport.allCases { defaults.removeObject(forKey: Self.key(s)) }
        tutorialDone = []
    }
    func recordMatch(won: Bool) {
        matchesPlayed += 1; defaults.set(matchesPlayed, forKey: "sport.stats.played.v1")
        if won { matchesWon += 1; defaults.set(matchesWon, forKey: "sport.stats.won.v1") }
    }
    func recordRally(_ hits: Int) {
        guard hits > bestRally else { return }
        bestRally = hits; defaults.set(hits, forKey: "sport.stats.rally.v1")
    }
}

/// Outfit colours for the character screen and the launch message. The same hex values tint
/// the body in Unity (TennisLook.Recolor) and the preview in the menu.
enum Outfit {
    static let palette: [(name: String, hex: String)] = [
        ("White", "F2F2F0"), ("Sky", "3FA9F5"), ("Navy", "1E2A6E"), ("Lime", "9EE63A"),
        ("Sun", "FFC233"), ("Coral", "FF6B4A"), ("Crimson", "E0243C"), ("Violet", "8A4FFF"),
        ("Teal", "1FB5A8"), ("Pink", "FF5FA2"), ("Charcoal", "2E3138"), ("Sand", "E3CFA4"),
    ]
    static func color(_ index: Int?) -> Color? {
        guard let index, palette.indices.contains(index) else { return nil }
        return Color(hex: palette[index].hex)
    }
    static func hex(_ index: Int?) -> String {
        guard let index, palette.indices.contains(index) else { return "" }
        return palette[index].hex
    }
    static func name(_ index: Int?) -> String {
        guard let index, palette.indices.contains(index) else { return "Kit colour" }
        return palette[index].name
    }
    /// Skin tones, matching the six the game offers.
    static let skins: [String] = ["FFE0C4", "EEC4A0", "E2A06E", "C47C50", "965A38", "603C28"]
}

extension Color {
    init(hex: String) {
        var v: UInt64 = 0
        Scanner(string: hex.trimmingCharacters(in: CharacterSet(charactersIn: "#"))).scanHexInt64(&v)
        self.init(red: Double((v >> 16) & 0xFF) / 255, green: Double((v >> 8) & 0xFF) / 255, blue: Double(v & 0xFF) / 255)
    }
}
