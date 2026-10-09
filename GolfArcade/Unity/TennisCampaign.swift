import Foundation

/// One opponent on the Island Circuit. `key` must match TennisRoster.Key in Unity, which
/// dresses the rival in their own body and gives them their playing style; everything shown
/// on screen, and the story around them, lives here and in TennisStory.
struct TennisOpponent: Identifiable, Equatable {
    let key: String
    let name: String
    let nickname: String
    let round: String
    let blurb: String
    /// Unity's OpponentDifficulty (the profile's rung): 0 club player, 1 the champion.
    let difficulty: Double
    let stars: Int
    let boss: Bool
    /// Match format: sets needed to win, games per set (3 = one short first-to-three set).
    let sets: Int
    let games: Int
    var id: String { key }
    var roundTitle: String { round.capitalized }
    /// "First to 3 games", "Best of 3 sets", "Best of 5 sets".
    var formatTitle: String { sets == 1 ? "One set · first to \(games) games" : "Best of \(sets * 2 - 1) sets" }
    /// Hero art bundled with the app (GolfArcade/Unity/MenuArt).
    var art: String { "menu-hero-\(key.lowercased())" }
}

/// The campaign: ten rivals on the Island Circuit, each tougher than the last, ending with
/// Viktor Kron. Progress is the number of rivals beaten, kept across launches.
@MainActor @Observable
final class TennisCampaign {
    static let shared = TennisCampaign()

    static let draw: [TennisOpponent] = [
        TennisOpponent(key: "Milo", name: "Milo Reyes", nickname: "The Wildcard", round: "ROUND 1",
                       blurb: "All energy, no plan. Keep it deep and in play, and he'll hand you points.",
                       difficulty: 0.05, stars: 1, boss: false, sets: 1, games: 3),
        TennisOpponent(key: "Tama", name: "Tama Kealoha", nickname: "The Moonballer", round: "ROUND 2",
                       blurb: "Hits high, heavy loops to your baseline. Step in and take them early.",
                       difficulty: 0.20, stars: 1, boss: false, sets: 1, games: 3),
        TennisOpponent(key: "Suki", name: "Suki Tanaka", nickname: "The Needle", round: "ROUND 3",
                       blurb: "Pinpoint angles. She'll pull you wide, so recover to the middle after every shot.",
                       difficulty: 0.33, stars: 2, boss: false, sets: 1, games: 3),
        TennisOpponent(key: "Dex", name: "Dex Okafor", nickname: "Big Serve", round: "ROUND 4",
                       blurb: "The biggest serve on the island. Break him once and the set is yours.",
                       difficulty: 0.45, stars: 2, boss: false, sets: 1, games: 3),
        TennisOpponent(key: "Lina", name: "Lina Voss", nickname: "The Counter", round: "ROUND 5",
                       blurb: "Gets everything back. You won't hit through her — you have to outlast her.",
                       difficulty: 0.55, stars: 3, boss: false, sets: 1, games: 3),
        TennisOpponent(key: "Bruno", name: "Bruno Castell", nickname: "The Hammer", round: "QUARTERFINAL",
                       blurb: "Hits through the court. Time it early and use his pace against him.",
                       difficulty: 0.65, stars: 3, boss: false, sets: 2, games: 6),
        TennisOpponent(key: "Rosa", name: "Rosa Marín", nickname: "The Magician", round: "QUARTERFINAL II",
                       blurb: "Slice, drop shots, changes of rhythm. Stay on your toes and don't sit back.",
                       difficulty: 0.74, stars: 4, boss: false, sets: 2, games: 6),
        TennisOpponent(key: "Jax", name: "Jax Rourke", nickname: "The Sniper", round: "SEMIFINAL",
                       blurb: "Lives on the lines. Make him hit one more ball — he'd rather miss than play safe.",
                       difficulty: 0.83, stars: 4, boss: false, sets: 2, games: 6),
        TennisOpponent(key: "Nadia", name: "Nadia Petrova", nickname: "The Ice Queen", round: "SEMIFINAL II",
                       blurb: "Reads your game and hunts your weaker side. Mix it up or she'll pick you apart.",
                       difficulty: 0.92, stars: 5, boss: false, sets: 2, games: 6),
        TennisOpponent(key: "Viktor", name: "Viktor Kron", nickname: "The Wall", round: "FINAL",
                       blurb: "Three-time champion. Never misses, never smiles. Nobody has taken a set off him in years.",
                       difficulty: 1.0, stars: 5, boss: true, sets: 3, games: 6),
    ]

    private let progressKey = "tennis.campaign.won.v2"
    private let titlesKey = "tennis.campaign.titles.v2"
    private let storyKey = "tennis.story.seen.v1"

    /// Rivals beaten in the current run (0...10). Ten means the title is won.
    private(set) var won: Int
    /// Titles won, ever.
    private(set) var titles: Int
    /// Story beats already shown ("pre3", "win0"…), so replays can skip them.
    private(set) var seenBeats: Set<String>

    private init() {
        won = min(Self.draw.count, max(0, UserDefaults.standard.integer(forKey: progressKey)))
        titles = UserDefaults.standard.integer(forKey: titlesKey)
        seenBeats = Set(UserDefaults.standard.stringArray(forKey: storyKey) ?? [])
    }

    var champion: Bool { won >= Self.draw.count }
    /// The rival to play next (the champion again once the title is won).
    var nextRound: Int { min(won, Self.draw.count - 1) }
    var next: TennisOpponent { Self.draw[nextRound] }

    func unlocked(_ round: Int) -> Bool { round <= won && round < Self.draw.count }
    func beaten(_ round: Int) -> Bool { round < won }

    /// Record a finished campaign match. Winning the next round advances the ladder; losing
    /// never undoes rounds already won — you replay the rival you lost to.
    func record(round: Int, won didWin: Bool) {
        guard didWin, round == won, won < Self.draw.count else { return }
        won += 1
        if won == Self.draw.count { titles += 1; UserDefaults.standard.set(titles, forKey: titlesKey) }
        UserDefaults.standard.set(won, forKey: progressKey)
    }

    func seen(_ beat: String) -> Bool { seenBeats.contains(beat) }
    func markSeen(_ beat: String) {
        seenBeats.insert(beat); UserDefaults.standard.set(Array(seenBeats), forKey: storyKey)
    }

    /// Start the circuit again from the first round (the story replays from the start).
    func restart() {
        won = 0; UserDefaults.standard.set(0, forKey: progressKey)
        seenBeats = []; UserDefaults.standard.set([String](), forKey: storyKey)
    }
}
