import SwiftUI

// Player progression: every finished match earns XP ("rep"), XP fills a level bar, and a level
// gives a reward (the reward list is empty for now; the hook and the animation are in place).

/// What Unity reports when a match ends (the "matchStats" event: "key=value;...").
struct MatchStats: Equatable {
    var won = false
    var pointsWon = 0, pointsLost = 0, aces = 0, winners = 0
    var longest = 0, perfect = 0, great = 0
    var setsWon = 0, setsLost = 0, gamesWon = 0, gamesLost = 0
    var hits = 0, seconds = 0

    init(won: Bool = false) { self.won = won }
    init(line: String) {
        for pair in line.split(separator: ";") {
            let kv = pair.split(separator: "=", maxSplits: 1).map(String.init)
            guard kv.count == 2, let d = Double(kv[1]) else { continue }
            let v = Int(d.rounded())
            switch kv[0] {
            case "won": won = v == 1
            case "pointsWon": pointsWon = v; case "pointsLost": pointsLost = v
            case "aces": aces = v; case "winners": winners = v
            case "longest": longest = v; case "perfect": perfect = v; case "great": great = v
            case "setsWon": setsWon = v; case "setsLost": setsLost = v
            case "gamesWon": gamesWon = v; case "gamesLost": gamesLost = v
            case "hits": hits = v; case "seconds": seconds = v
            default: break
            }
        }
    }
}

/// How much XP each level asks for, and what each stretch of levels is called.
enum LevelCurve {
    static let maxLevel = 99
    /// XP to climb from `level` to the next one: 200, 266, 344, 434 ...
    static func needed(_ level: Int) -> Int {
        let l = max(1, level) - 1
        return 200 + 60 * l + 6 * l * l
    }
    static func rank(_ level: Int) -> String {
        switch level {
        case ..<5: "Rookie"
        case 5..<10: "Club Player"
        case 10..<20: "Regular"
        case 20..<30: "Contender"
        case 30..<40: "Semi-Pro"
        case 40..<60: "Pro"
        default: "Legend"
        }
    }
}

/// Something a level unlocks. There are none yet: `LevelRewards.rewards` is the place to add them.
struct LevelReward: Identifiable, Equatable {
    let id: String
    var title: String
    var detail: String
    var icon: String
}
enum LevelRewards {
    static func rewards(for level: Int) -> [LevelReward] { [] }
}

/// One line of the post-match XP breakdown.
struct XPLine: Identifiable, Equatable {
    let id: Int
    var title: String
    var detail: String
    var xp: Int
}

/// Everything the post-match screen shows: the result, the XP earned and where it took the level.
struct PostMatchSummary: Equatable {
    var won: Bool
    var score: String
    var opponent: String
    var stats: MatchStats
    var lines: [XPLine]
    var total: Int
    var practice = false
    var startLevel: Int, startXP: Int
    var endLevel: Int, endXP: Int
    var levelsGained: [Int] { endLevel > startLevel ? Array((startLevel + 1)...endLevel) : [] }
}

@MainActor @Observable
final class Progression {
    static let shared = Progression()

    struct Entry: Codable, Equatable {
        var level = 1
        var xp = 0          // into the current level
        var totalXP = 0
        var matches = 0
        var wins = 0
    }

    private static let key = "sports.progression.v1"
    private(set) var entries: [String: Entry]

    private init() {
        if let data = UserDefaults.standard.data(forKey: Self.key), let decoded = try? JSONDecoder().decode([String: Entry].self, from: data) {
            entries = decoded
        } else { entries = [:] }
    }

    func entry(for player: UUID) -> Entry { entries[player.uuidString] ?? Entry() }

    private func save() {
        if let data = try? JSONEncoder().encode(entries) { UserDefaults.standard.set(data, forKey: Self.key) }
    }

    /// How a match went, in XP. Every line is a reason the player can see and chase.
    static func breakdown(stats: MatchStats, won: Bool, practice: Bool, difficulty: Double) -> [XPLine] {
        var rows: [(String, String, Int)] = []
        rows.append(("Match played", "", 60))
        rows.append(won ? ("Victory", "", 120) : ("Good effort", "", 30))
        if won && difficulty > 0.05 {
            rows.append((difficulty >= 0.7 ? "Tough opponent" : "Rival strength", "", Int((120 * difficulty * 0.6).rounded())))
        }
        if stats.setsWon + stats.setsLost > 1 && stats.setsWon > 0 { rows.append(("Sets won", "× \(stats.setsWon)", stats.setsWon * 25)) }
        if stats.aces > 0 { rows.append(("Aces", "× \(stats.aces)", min(stats.aces, 8) * 12)) }
        if stats.winners > 0 { rows.append(("Winners", "× \(stats.winners)", min(stats.winners, 15) * 6)) }
        if stats.longest > 6 { rows.append(("Longest rally", "\(stats.longest) shots", min((stats.longest - 6) * 2, 30))) }
        if stats.perfect > 0 { rows.append(("Perfect timing", "× \(stats.perfect)", min(stats.perfect * 3, 45))) }
        if stats.pointsWon > 0 { rows.append(("Points won", "\(stats.pointsWon)", min(stats.pointsWon, 40))) }
        if won && stats.gamesLost == 0 && stats.setsLost == 0 { rows.append(("Shutout", "", 60)) }
        // A practice match earns half of every line.
        return rows.filter { $0.2 > 0 }.enumerated().map {
            XPLine(id: $0.offset, title: $0.element.0, detail: $0.element.1, xp: practice ? max(1, $0.element.2 / 2) : $0.element.2)
        }
    }

    /// Bank a finished match: add its XP, level up as far as it goes, and describe it for the screen.
    func award(player: UUID, stats: MatchStats, won: Bool, score: String, opponent: String, practice: Bool, difficulty: Double) -> PostMatchSummary {
        let lines = Self.breakdown(stats: stats, won: won, practice: practice, difficulty: difficulty)
        let total = max(0, lines.reduce(0) { $0 + $1.xp })
        var entry = entry(for: player)
        let startLevel = entry.level, startXP = entry.xp
        entry.xp += total; entry.totalXP += total; entry.matches += 1; if won { entry.wins += 1 }
        while entry.level < LevelCurve.maxLevel && entry.xp >= LevelCurve.needed(entry.level) {
            entry.xp -= LevelCurve.needed(entry.level); entry.level += 1
        }
        if entry.level >= LevelCurve.maxLevel { entry.xp = min(entry.xp, LevelCurve.needed(entry.level) - 1) }
        entries[player.uuidString] = entry
        save()
        return PostMatchSummary(won: won, score: score, opponent: opponent, stats: stats, lines: lines, total: total, practice: practice,
                                startLevel: startLevel, startXP: startXP, endLevel: entry.level, endXP: entry.xp)
    }
}

/// A level and its XP bar, small: the header and the home screen show it.
struct LevelStrip: View {
    let player: UUID
    var compact = false
    var body: some View {
        let e = Progression.shared.entry(for: player)
        let need = LevelCurve.needed(e.level)
        HStack(spacing: 8) {
            Text("LV \(e.level)").font(Club.caps(compact ? 11 : 13)).tracking(1).foregroundStyle(Club.ink)
                .padding(.horizontal, 8).padding(.vertical, 3).background(Capsule().fill(Club.sun))
            Text(LevelCurve.rank(e.level).uppercased()).font(Club.caps(compact ? 10 : 12)).tracking(1.5).foregroundStyle(.white.opacity(0.85))
            GeometryReader { g in
                ZStack(alignment: .leading) {
                    Capsule().fill(.white.opacity(0.2))
                    Capsule().fill(Club.sun).frame(width: g.size.width * CGFloat(e.xp) / CGFloat(need))
                }
            }
            .frame(width: compact ? 70 : 110, height: 6)
            Text("\(e.xp)/\(need) XP").font(Club.ui(compact ? 10 : 12, 600)).foregroundStyle(.white.opacity(0.75)).monospacedDigit()
        }
    }
}
