import Combine
import Foundation

/// A round of stroke play: each player plays a hole out in turn, then everyone moves to the next.
@MainActor
final class CourseRound: ObservableObject {
    enum Phase { case ready, charging, flying, landed, holed, complete }

    @Published private(set) var phase: Phase = .ready
    @Published var club: GolfClub = .driver
    @Published var aim = 0.0
    @Published private(set) var power = 0.0
    @Published private(set) var activeShot: RangeShot?
    @Published private(set) var flightStart: Date?
    @Published private(set) var isReplay = false
    @Published private(set) var course: Course
    @Published private(set) var playerCount: Int
    @Published private(set) var playerIndex = 0
    @Published private(set) var holeIndex = 0
    @Published private(set) var ball: CoursePoint
    @Published private(set) var lie: CourseLie = .tee
    @Published private(set) var strokes = 0
    /// Strokes per player per hole; nil until the hole is finished.
    @Published private(set) var scores: [[Int?]]
    @Published private(set) var pickedUp = false
    @Published private(set) var best: Int?

    /// A player who reaches par plus this many strokes picks up so the round keeps moving.
    static let strokesOverParCap = 5
    private let defaults: UserDefaults
    @Published private(set) var pausedAt: Date?

    init(course: Course = .easy, playerCount: Int = 1, defaults: UserDefaults = .standard) {
        self.defaults = defaults
        self.course = course
        self.playerCount = max(1, playerCount)
        ball = course.holes[0].tee
        scores = Array(repeating: Array(repeating: nil, count: course.holes.count), count: max(1, playerCount))
        best = Self.loadBest(course: course, defaults: defaults)
        club = suggestedClub()
    }

    var hole: Hole { course.holes[holeIndex] }
    var canSwing: Bool { phase == .ready || phase == .charging }
    var distanceToPin: Double { ball.distance(to: hole.pin) }
    /// Aim zero points at the pin.
    var heading: Double { ball.heading(to: hole.pin) }
    var strokeNumber: Int { strokes + 1 }
    var isLastTurn: Bool { holeIndex == course.holes.count - 1 && playerIndex == playerCount - 1 }

    func total(for player: Int) -> Int { scores[player].compactMap { $0 }.reduce(0, +) }

    /// Strokes relative to par over the holes this player has finished.
    func toPar(for player: Int) -> Int {
        zip(scores[player], course.holes).reduce(0) { sum, pair in
            pair.0.map { sum + $0 - pair.1.par } ?? sum
        }
    }

    func start(course: Course, playerCount: Int) {
        self.course = course
        self.playerCount = max(1, playerCount)
        best = Self.loadBest(course: course, defaults: defaults)
        restart()
    }

    func charge(_ value: Double) {
        guard canSwing else { return }
        power = min(max(value, 0), 1)
        phase = .charging
    }

    @discardableResult
    func release(at date: Date = .now, curve: Double = 0, strike: StrikeQuality = .center) -> Bool {
        guard phase == .charging, power >= 0.06 else {
            cancelCharge()
            return false
        }
        let shotPower = club == .putter ? puttPower(for: power) : power
        let shot = RangeShot(
            id: strokes + 1,
            club: club,
            power: shotPower,
            aim: aim,
            curve: club == .putter ? 0 : curve,
            strike: strike,
            origin: ball,
            heading: heading,
            lieFactor: club == .putter ? 1 : lie.powerFactor,
            hole: hole
        )
        activeShot = shot
        isReplay = false
        flightStart = date
        phase = .flying
        return true
    }

    func cancelCharge() {
        guard phase == .charging else { return }
        power = 0
        phase = .ready
    }

    func elapsed(at date: Date) -> Double {
        guard let start = flightStart else { return 0 }
        return max(0, (pausedAt ?? date).timeIntervalSince(start))
    }

    func advance(at date: Date) {
        guard phase == .flying, let shot = activeShot, elapsed(at: date) >= shot.duration else { return }
        if isReplay {
            phase = finishedHole ? .holed : .landed
            return
        }
        strokes += 1 + shot.penaltyStrokes
        lie = shot.isHoled ? .green : shot.lie == .water || shot.lie == .outOfBounds ? lieAfterDrop(shot) : shot.lie ?? .fairway
        ball = shot.nextPosition
        let cap = hole.par + Self.strokesOverParCap
        if shot.isHoled || strokes >= cap {
            pickedUp = !shot.isHoled
            scores[playerIndex][holeIndex] = shot.isHoled ? strokes : cap
            phase = .holed
        } else {
            phase = .landed
        }
    }

    /// Tee up from where the last ball finished.
    func nextShot() {
        guard phase == .landed else { return }
        activeShot = nil
        flightStart = nil
        power = 0
        aim = 0
        isReplay = false
        club = suggestedClub()
        phase = .ready
    }

    func replay(at date: Date = .now) {
        guard phase == .landed || phase == .holed, activeShot != nil else { return }
        isReplay = true
        flightStart = date
        phase = .flying
    }

    /// Jumps a flight or replay to where the ball stops.
    func skipFlight(at date: Date = .now) {
        guard phase == .flying, let shot = activeShot else { return }
        flightStart = (pausedAt ?? date).addingTimeInterval(-shot.duration)
        advance(at: date)
    }

    /// After a hole is finished: the next player tees off, or everyone moves on.
    func continueAfterHole() {
        guard phase == .holed else { return }
        if isLastTurn {
            activeShot = nil
            phase = .complete
            saveBestIfNeeded()
            return
        }
        if playerIndex < playerCount - 1 {
            playerIndex += 1
        } else {
            playerIndex = 0
            holeIndex += 1
        }
        beginTurn()
    }

    func restartHole() {
        guard phase != .complete else { return }
        scores[playerIndex][holeIndex] = nil
        beginTurn()
    }

    func restart() {
        scores = Array(repeating: Array(repeating: nil, count: course.holes.count), count: playerCount)
        playerIndex = 0
        holeIndex = 0
        pausedAt = nil
        beginTurn()
    }

    func pause(at date: Date = .now) { cancelCharge(); pausedAt = date }

    func resume(at date: Date = .now) {
        if let pausedAt, let flightStart { self.flightStart = flightStart.addingTimeInterval(date.timeIntervalSince(pausedAt)) }
        pausedAt = nil
    }

    /// The club most players would pull from here.
    func suggestedClub() -> GolfClub {
        if lie == .green { return .putter }
        if lie == .bunker { return .wedge }
        let distance = distanceToPin / max(lie.powerFactor, 0.1)
        if distance <= GolfClub.wedge.mockDistance * 0.95 { return .wedge }
        if distance <= GolfClub.iron.mockDistance * 0.95 { return .iron }
        return .driver
    }

    // MARK: - Private

    private var finishedHole: Bool { scores[playerIndex][holeIndex] != nil }

    private func beginTurn() {
        ball = hole.tee
        lie = .tee
        strokes = 0
        pickedUp = false
        activeShot = nil
        flightStart = nil
        power = 0
        aim = 0
        isReplay = false
        club = suggestedClub()
        phase = .ready
    }

    private func lieAfterDrop(_ shot: RangeShot) -> CourseLie {
        let dropLie = hole.lie(at: shot.nextPosition)
        return dropLie == .water || dropLie == .outOfBounds ? .rough : dropLie
    }

    /// Putting is about touch, not strength: a full stroke rolls about 1.6 times the distance to
    /// the cup (at least 8 yards), so the meter has the same feel from any length.
    private func puttPower(for value: Double) -> Double {
        let target = value * max(8, distanceToPin * 1.6)
        if let power = RangeShot.power(toReach: target, with: .putter) { return power }
        return target < GolfClub.putter.mockDistance ? 0 : 1
    }

    private func saveBestIfNeeded() {
        guard playerCount == 1, scores[0].allSatisfy({ $0 != nil }) else { return }
        let result = toPar(for: 0)
        if best == nil || result < best! {
            best = result
            defaults.set(result, forKey: Self.bestKey(course))
        }
    }

    private static func bestKey(_ course: Course) -> String { "course.\(course.id).best" }

    private static func loadBest(course: Course, defaults: UserDefaults) -> Int? {
        defaults.object(forKey: bestKey(course)) as? Int
    }
}
