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
    static func rewards(for level: Int) -> [LevelReward] {
        level == 2 ? [LevelReward(id: "white-headband", title: "White Headband", detail: "Your first Island Sports Club reward.", icon: "sparkles")] : []
    }
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
    /// A golf round's (its card, coins and bounties), and the daily forecast streak it kept; nil for tennis.
    var golf: GolfRoundStats? = nil
    var streak = 0
}

/// What Unity reports when a golf round ends (the "golfRound" event: "key=value;..."): the card,
/// and what the round's rings and bounties paid (Unity: GolfGame.Challenges.cs).
struct GolfRoundStats: Equatable {
    var toPar = 0, strokes = 0, par = 0, holes = 0
    var aces = 0, eagles = 0, birdies = 0, pars = 0
    var coins = 0, rings = 0, bounties = 0, bountyHoles = 0
    /// Holes played in a storm or fog, twists dealt, and the day's forecast round (yyyymmdd) or 0.
    var rough = 0, twists = 0, daily = 0
    var course = ""

    init() {}
    init(line: String) {
        for pair in line.split(separator: ";") {
            let kv = pair.split(separator: "=", maxSplits: 1).map(String.init)
            guard kv.count == 2 else { continue }
            if kv[0] == "course" { course = kv[1]; continue }
            guard let v = Int(kv[1]) else { continue }
            switch kv[0] {
            case "toPar": toPar = v; case "strokes": strokes = v; case "par": par = v; case "holes": holes = v
            case "aces": aces = v; case "eagles": eagles = v; case "birdies": birdies = v; case "pars": pars = v
            case "coins": coins = v; case "rings": rings = v; case "bounties": bounties = v; case "bountyHoles": bountyHoles = v
            case "rough": rough = v; case "twists": twists = v; case "daily": daily = v
            default: break
            }
        }
    }

    /// "−2", "E", "+3".
    var toParText: String { Self.text(toPar: toPar) }
    static func text(toPar: Int) -> String { toPar == 0 ? "E" : toPar < 0 ? "\u{2212}\(-toPar)" : "+\(toPar)" }
    var courseTitle: String { GolfCourseChoice(rawValue: course)?.title ?? "the course" }
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

    /// How a golf round went, in XP: the round, the score, every birdie, the coins its rings and
    /// bounties paid, the weather braved, and the day's forecast round (the first finish each day)
    /// with its streak. Every line is a reason the player can see and chase.
    static func golfBreakdown(_ g: GolfRoundStats, dailyFirst: Bool, streak: Int) -> [XPLine] {
        var rows: [(String, String, Int)] = []
        rows.append(("Round played", "\(g.holes) holes", 30 + 10 * max(1, g.holes)))
        if g.toPar < 0 { rows.append(("Under par", g.toParText, min(160, 40 * -g.toPar))) }
        else if g.toPar == 0 { rows.append(("Level par", "", 30)) }
        if g.aces > 0 { rows.append(("Hole in one!", "\u{00D7} \(g.aces)", 150 * g.aces)) }
        if g.eagles > 0 { rows.append(("Eagles", "\u{00D7} \(g.eagles)", 60 * g.eagles)) }
        if g.birdies > 0 { rows.append(("Birdies", "\u{00D7} \(g.birdies)", 25 * g.birdies)) }
        if g.coins > 0 { rows.append(("Coins", "\(g.coins)", min(150, (g.coins + 1) / 2))) }
        if g.bounties > 0 { rows.append(("Bounties", "\(g.bounties)/\(g.bountyHoles)", 20 * g.bounties)) }
        if g.bountyHoles > 1 && g.bounties == g.bountyHoles { rows.append(("Every bounty", "Clean sweep", 60)) }
        if g.rough > 0 { rows.append(("Braved the weather", "\(g.rough) hole\(g.rough == 1 ? "" : "s")", 15 * g.rough)) }
        if dailyFirst {
            rows.append(("Daily forecast", "First round today", 150))
            if streak > 1 { rows.append(("Day streak", "\(streak) days", 20 * min(streak, 7))) }
        }
        return rows.filter { $0.2 > 0 }.enumerated().map { XPLine(id: $0.offset, title: $0.element.0, detail: $0.element.1, xp: $0.element.2) }
    }

    func awardGolf(player: UUID, stats: GolfRoundStats, dailyFirst: Bool, streak: Int) -> PostMatchSummary {
        let lines = Self.golfBreakdown(stats, dailyFirst: dailyFirst, streak: streak)
        let total = max(0, lines.reduce(0) { $0 + $1.xp })
        var entry = entry(for: player)
        let startLevel = entry.level, startXP = entry.xp
        entry.xp += total; entry.totalXP += total; entry.matches += 1; if stats.toPar <= 0 { entry.wins += 1 }
        while entry.level < LevelCurve.maxLevel && entry.xp >= LevelCurve.needed(entry.level) {
            entry.xp -= LevelCurve.needed(entry.level); entry.level += 1
        }
        if entry.level >= LevelCurve.maxLevel { entry.xp = min(entry.xp, LevelCurve.needed(entry.level) - 1) }
        entries[player.uuidString] = entry
        save()
        return PostMatchSummary(won: stats.toPar <= 0, score: "\(stats.toParText)  \u{00B7}  \(stats.strokes) strokes", opponent: stats.courseTitle,
                                stats: MatchStats(won: stats.toPar <= 0), lines: lines, total: total, practice: false,
                                startLevel: startLevel, startXP: startXP, endLevel: entry.level, endXP: entry.xp, golf: stats, streak: streak)
    }

    /// Bank a finished match: add its XP, level up as far as it goes, and describe it for the screen.
    @discardableResult
    func awardTutorial(player: UUID, sport: Sport, store: OnboardingStore = OnboardingFlow.shared.store) -> Int {
        guard sport.playable, !store.rewardClaimed(sport) else { return 0 }
        let pendingKey = "ftue.pendingReward.\(sport.rawValue).v1"
        let amountKey = pendingKey + ".xp", itemKey = pendingKey + ".item"
        var entry: Entry
        let amount: Int
        let item: String?
        // A durable target entry lets a launch interrupted between XP and claim writes finish once.
        if let data = store.defaults.data(forKey: pendingKey), let saved = try? JSONDecoder().decode(Entry.self, from: data) {
            entry = saved; amount = store.defaults.integer(forKey: amountKey); item = store.defaults.string(forKey: itemKey)
        } else {
            entry = self.entry(for: player)
            let first = !store.anyRewardClaimed
            amount = first ? (entry.level < 2 ? max(0, LevelCurve.needed(1) - entry.xp) : 0) : 50
            item = first ? "white-headband" : nil
            entry.xp += amount; entry.totalXP += amount
            while entry.level < LevelCurve.maxLevel && entry.xp >= LevelCurve.needed(entry.level) {
                entry.xp -= LevelCurve.needed(entry.level); entry.level += 1
            }
            store.defaults.set(amount, forKey: amountKey); store.defaults.set(item, forKey: itemKey)
            guard let data = try? JSONEncoder().encode(entry) else { return 0 }
            store.defaults.set(data, forKey: pendingKey)
        }
        entries[player.uuidString] = entry; save()
        store.markReward(sport, xp: amount, item: item)
        store.defaults.removeObject(forKey: pendingKey)
        Analytics.track("reward_claimed", ["sport": sport.rawValue, "item": item ?? "none", "level": String(entry.level)])
        return amount
    }

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


// MARK: - The day's forecast round

/// Golf's daily forecast: one round a day, the same for everyone — the day's course, its twists and
/// each hole's weather dealt from the date, with the same dice as Unity's (Course/RoundEvents.cs:
/// Dice, Mix, Deal, WeatherFor), so the course screen can show the forecast before it is played.
/// Unity's ChallengeTests.TheDiceAreTheAppsDice holds the numbers both must agree on.
enum GolfForecast {
    /// SplitMix64 from a seed, as RoundEvents' Dice.
    struct Dice {
        var state: UInt64
        init(_ seed: Int) { state = UInt64(UInt32(truncatingIfNeeded: seed)) }
        mutating func bits() -> UInt64 {
            state &+= 0x9E3779B97F4A7C15
            var z = state
            z = (z ^ (z >> 30)) &* 0xBF58476D1CE4E5B9
            z = (z ^ (z >> 27)) &* 0x94D049BB133111EB
            return z ^ (z >> 31)
        }
        /// In [0, 1).
        mutating func nextDouble() -> Double { Double(bits() >> 11) * (1.0 / 9007199254740992.0) }
        mutating func next(_ n: Int) -> Int { n <= 0 ? 0 : min(n - 1, Int(nextDouble() * Double(n))) }
    }

    static func mix(_ seed: Int, _ hole: Int, _ salt: Int) -> Int {
        let s = UInt64(UInt32(truncatingIfNeeded: seed)), h = UInt64(UInt32(truncatingIfNeeded: hole)), t = UInt64(UInt32(truncatingIfNeeded: salt))
        var z = (s &* 0x9E3779B97F4A7C15) ^ (h &* 0xC2B2AE3D27D4EB4F) ^ (t &* 0x165667B19E3779F9)
        z = (z ^ (z >> 30)) &* 0xBF58476D1CE4E5B9
        z = (z ^ (z >> 27)) &* 0x94D049BB133111EB
        z ^= z >> 31
        return Int(z & 0x7FFFFFFF)
    }

    /// Unity's Twist, in its order.
    enum Twist: Int, CaseIterable {
        case moonBall, bounceHouse, turboGreens, bucketCup, pinball, bananaBall, hurricane, stickyGreens
        var title: String {
            switch self {
            case .moonBall: "Moon Ball"; case .bounceHouse: "Bounce House"; case .turboGreens: "Turbo Greens"; case .bucketCup: "Bucket Cup"
            case .pinball: "Pinball"; case .bananaBall: "Banana Ball"; case .hurricane: "Hurricane"; case .stickyGreens: "Sticky Greens"
            }
        }
        var icon: String {
            switch self {
            case .moonBall: "moon.fill"; case .bounceHouse: "arrow.up.and.down.circle.fill"; case .turboGreens: "hare.fill"; case .bucketCup: "circle.circle.fill"
            case .pinball: "star.fill"; case .bananaBall: "arrow.turn.up.right"; case .hurricane: "hurricane"; case .stickyGreens: "hand.raised.fill"
            }
        }
    }

    /// Unity's Weather, in its order.
    enum Weather: Int { case clear, gusty, rain, storm, fog
        var icon: String {
            switch self { case .clear: "sun.max.fill"; case .gusty: "wind"; case .rain: "cloud.rain.fill"; case .storm: "cloud.bolt.rain.fill"; case .fog: "cloud.fog.fill" }
        }
        func title(magma: Bool) -> String {
            switch self {
            case .clear: magma ? "Clear" : "Sunshine"; case .gusty: "Gusty"; case .rain: magma ? "Ashfall" : "Rain"
            case .storm: magma ? "Eruption" : "Thunderstorm"; case .fog: magma ? "Volcanic Smoke" : "Sea Fog"
            }
        }
    }

    /// RoundEvents.Deal: two twists, never both green twists.
    static func deal(_ seed: Int) -> [Twist] {
        var dice = Dice(mix(seed, 0, 0x7157))
        var pool = Twist.allCases, dealt: [Twist] = []
        for _ in 0..<2 {
            let k = dice.next(pool.count)
            dealt.append(pool.remove(at: k))
            if dealt.last == .turboGreens || dealt.last == .stickyGreens { pool.removeAll { $0 == .turboGreens || $0 == .stickyGreens } }
        }
        return dealt
    }

    /// RoundEvents.WeatherFor: a hole's weather as it starts.
    static func weather(_ seed: Int, hole: Int, hurricane: Bool) -> Weather {
        var dice = Dice(mix(seed, hole, 1))
        let u = dice.nextDouble()
        let (clear, gusty, rain, fog) = hurricane ? (0.12, 0.42, 0.58, 0.58) : (0.36, 0.56, 0.74, 0.85)
        return u < clear ? .clear : u < gusty ? .gusty : u < rain ? .rain : u < fog ? .fog : .storm
    }

    /// Today as yyyymmdd: the round's seed.
    static func today(_ date: Date = Date()) -> Int {
        let c = Calendar.current.dateComponents([.year, .month, .day], from: date)
        return (c.year ?? 2026) * 10000 + (c.month ?? 1) * 100 + (c.day ?? 1)
    }
    static func date(of day: Int) -> Date {
        Calendar.current.date(from: DateComponents(year: day / 10000, month: day / 100 % 100, day: day % 100)) ?? Date()
    }
    /// The course the day's round is played on: each in turn.
    static func course(for day: Int) -> GolfCourseChoice {
        let n = Calendar.current.ordinality(of: .day, in: .era, for: date(of: day)) ?? 0
        let all = GolfCourseChoice.allCases
        return all[n % all.count]
    }
    static func holes(on course: GolfCourseChoice) -> Int { Int(course.card.split(separator: " ").first ?? "") ?? 3 }

    /// The day's round as the course screen shows it.
    struct Day: Equatable {
        var day: Int, course: GolfCourseChoice, twists: [Twist], weather: [Weather]
        init(_ day: Int) {
            self.day = day; course = GolfForecast.course(for: day); twists = GolfForecast.deal(day)
            let hurricane = twists.contains(.hurricane)
            weather = (0..<GolfForecast.holes(on: course)).map { GolfForecast.weather(day, hole: $0, hurricane: hurricane) }
        }
        /// The roughest weather on the day's holes, for the card's headline.
        var headline: String {
            let magma = course == .magma
            for w in [Weather.storm, .fog, .rain, .gusty] where weather.contains(w) { return w.title(magma: magma) }
            return Weather.clear.title(magma: magma)
        }
    }
}

/// The days the forecast round was finished, and its best score each day: the streak is the run of
/// days up to today (or yesterday, while today's is still to play).
@MainActor @Observable
final class DailyGolf {
    static let shared = DailyGolf()
    private static let key = "golf.daily.v1"
    private(set) var best: [Int: Int]
    private init() {
        if let data = UserDefaults.standard.data(forKey: Self.key), let saved = try? JSONDecoder().decode([Int: Int].self, from: data) { best = saved } else { best = [:] }
    }
    private func save() { if let data = try? JSONEncoder().encode(best) { UserDefaults.standard.set(data, forKey: Self.key) } }

    func played(_ day: Int) -> Bool { best[day] != nil }

    /// Consecutive days finished, counting back from `day` (or the day before, when `day` isn't done yet).
    func streak(through day: Int = GolfForecast.today()) -> Int {
        var date = GolfForecast.date(of: day)
        if !played(day) { date = Calendar.current.date(byAdding: .day, value: -1, to: date) ?? date }
        var n = 0
        while played(GolfForecast.today(date)) {
            n += 1
            date = Calendar.current.date(byAdding: .day, value: -1, to: date) ?? date
        }
        return n
    }

    /// A forecast round finished: whether it was the day's first (the bonus is paid once a day), and the streak with it.
    @discardableResult
    func record(day: Int, toPar: Int) -> (first: Bool, streak: Int) {
        let first = best[day] == nil
        best[day] = min(best[day] ?? toPar, toPar)
        save()
        return (first, streak(through: day))
    }
}
