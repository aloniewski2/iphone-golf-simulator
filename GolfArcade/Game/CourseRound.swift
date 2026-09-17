import Combine
import Foundation

/// Navigation only: never changes the target, contact tolerance, cup or shot physics.
struct HoleNavigation: Equatable {
    let distance: Double
    let relativeBearing: Double

    init(ball: CoursePoint, pin: CoursePoint, aimHeading: Double) {
        distance = ball.distance(to: pin)
        var angle = (ball.heading(to: pin) - aimHeading).truncatingRemainder(dividingBy: 360)
        if angle > 180 { angle -= 360 }
        if angle < -180 { angle += 360 }
        relativeBearing = distance < 0.01 ? 0 : angle
    }

    var prominence: Double { min(1, max(0, (distance - 15) / 185)) }
    var directionLabel: String {
        if abs(relativeBearing) < 2 { return "ON YOUR AIM LINE" }
        return "\(Int(abs(relativeBearing).rounded()))° \(relativeBearing < 0 ? "LEFT" : "RIGHT") OF AIM"
    }
    static func beaconScale(cameraDistance: Double) -> Float {
        Float(max(0.8, min(28, cameraDistance * 0.045)))
    }
}

/// A round of stroke play: each player plays a hole out in turn, then everyone moves to the next.
@MainActor
final class CourseRound: ObservableObject {
    enum Phase { case ready, charging, flying, landed, holed, complete }

    @Published private(set) var phase: Phase = .ready
    @Published var club: GolfClub = .driver {
        didSet { if oldValue != club { shotType = club == .putter ? .putt : .full } }
    }
    @Published var aim = 0.0
    @Published var shotType: ShotType = .full
    @Published var curve = 0.0
    @Published var editingShot = false
    @Published var automaticProgression = false
    @Published private(set) var nextShotAt: Date?
    @Published private(set) var stanceAim = 0.0
    private var manualAimSelected = false
    private var planningKey: String?
    private var planningPower = 1.0
    private var previewCache: RangeShot?
    private var previewKey: ShotRequest?
    private var previewOrigin: CoursePoint?
    private var previewLie: CourseLie?
    private var previewHole: Hole?
    @Published private(set) var target: CoursePoint?
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
    var canSwing: Bool { !editingShot && pausedAt == nil && (phase == .ready || phase == .charging) }
    var distanceToPin: Double { ball.distance(to: hole.pin) }
    var intendedTarget: CoursePoint { target ?? hole.recommendedTarget(from: ball) }
    var distanceToTarget: Double { ball.distance(to: intendedTarget) }
    var targetLabel: String { intendedTarget == hole.pin ? "PIN" : target == nil ? "LANDING" : "TARGET" }
    var heading: Double { ball.heading(to: intendedTarget) }
    var aimStep: Double { club == .putter ? 0.25 : 2 }
    var combinedAim: Double { aim + stanceAim }
    var holeNavigation: HoleNavigation {
        HoleNavigation(ball: ball, pin: hole.pin, aimHeading: heading + combinedAim)
    }

    /// An explicit aim choice wins over the held-grip offset without resetting tracking.
    func aimAtPin() {
        selectTarget(hole.pin)
    }

    func setStanceAim(_ degrees: Double) {
        guard phase == .ready, !editingShot, !manualAimSelected, degrees.isFinite else { return }
        let value = (max(-StanceAimSettler.range, min(StanceAimSettler.range, degrees)) * 2).rounded() / 2
        if stanceAim != value { stanceAim = value }
    }

    /// Cached ideal-center prediction using the same launch/lie/flight model as release.
    /// It is an aim guide, not a promise about the player's future swing speed or strike.
    var trajectoryPreview: RangeShot {
        let key = "\(club.rawValue)-\(shotType.rawValue)-\(lie.rawValue)-\(distanceToTarget)"
        if key != planningKey {
            planningKey = key
            planningPower = RangeShot.power(toReach: distanceToTarget, with: club,
                type: shotType, lieFactor: club == .putter ? 1 : lie.powerFactor) ?? 1
        }
        let predictedPower = phase == .charging ? max(0.05, (power * 20).rounded() / 20) : planningPower
        let request = ShotRequest(club: club, targetHeading: heading + combinedAim,
            type: club == .putter ? .putt : shotType,
            execution: SwingImpact(power: predictedPower, curveDegrees: curve))
        if previewKey != request || previewOrigin != ball || previewLie != lie || previewHole != hole {
            previewCache = RangeShot(id: 0, request: request, origin: ball,
                lieFactor: club == .putter ? 1 : lie.powerFactor, hole: hole)
            previewKey = request; previewOrigin = ball; previewLie = lie; previewHole = hole
        }
        return previewCache!
    }

    func selectTarget(_ point: CoursePoint) {
        guard phase == .ready, point.x.isFinite, point.d.isFinite, ball.distance(to: point) > 0.01 else { return }
        target = point
        manualAimSelected = true
        stanceAim = 0
        aim = 0
        club = suggestedClub()
    }

    func followFairway() {
        guard phase == .ready else { return }
        target = nil
        manualAimSelected = false
        stanceAim = 0
        aim = 0
        club = suggestedClub()
    }

    func adjustAim(_ degrees: Double) {
        guard phase == .ready, degrees.isFinite else { return }
        // Explicit buttons take over from motion aim for this shot, retaining its current line.
        if !manualAimSelected { aim += stanceAim; stanceAim = 0; manualAimSelected = true }
        aim = (aim + degrees).truncatingRemainder(dividingBy: 360)
        if aim > 180 { aim -= 360 }
        if aim < -180 { aim += 360 }
    }

    func setManualAim(_ degrees: Double) {
        guard degrees.isFinite else { return }
        adjustAim(degrees - combinedAim)
    }
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
        power = min(max(value.isFinite ? value : 0, 0), 1)
        phase = .charging
    }

    @discardableResult
    func release(at date: Date = .now, curve: Double = 0, strike: StrikeQuality = .center, execution: SwingImpact? = nil) -> Bool {
        guard canSwing, phase == .charging, power > 0 else {
            cancelCharge()
            return false
        }
        var impact = execution ?? SwingImpact(power: power, curveDegrees: curve, strike: strike)
        guard impact.confidence.isFinite, impact.confidence >= 0.45 else { cancelCharge(); return false }
        impact.power = power
        impact.curveDegrees += self.curve
        let request = ShotRequest(club: club, targetHeading: heading + combinedAim,
                                  type: club == .putter ? .putt : shotType, execution: impact)
        let shot = RangeShot(id: strokes + 1, request: request, origin: ball,
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
        guard pausedAt == nil else { return }
        if automaticProgression, let nextShotAt, date >= nextShotAt, !editingShot {
            if phase == .landed { nextShot() }
            else if phase == .holed { continueAfterHole() }
            return
        }
        guard phase == .flying, let shot = activeShot, elapsed(at: date) + 0.000001 >= shot.duration else { return }
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
        if automaticProgression { nextShotAt = date.addingTimeInterval(phase == .holed ? 5 : 3) }
    }

    /// Tee up from where the last ball finished.
    func nextShot() {
        guard phase == .landed else { return }
        activeShot = nil
        flightStart = nil
        power = 0
        isReplay = false
        nextShotAt = nil
        aim = 0
        stanceAim = 0
        manualAimSelected = false
        curve = 0
        target = nil
        club = suggestedClub()
        shotType = club == .putter ? .putt : .full
        phase = .ready
    }

    func replay(at date: Date = .now) {
        guard phase == .landed || phase == .holed, activeShot != nil else { return }
        isReplay = true
        nextShotAt = nil // Replays never auto-progress or spend another stroke.
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
        nextShotAt = nil
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

    func pause(at date: Date = .now) {
        guard pausedAt == nil else { return }
        cancelCharge()
        pausedAt = date
    }

    func resume(at date: Date = .now) {
        if let pausedAt, let flightStart { self.flightStart = flightStart.addingTimeInterval(date.timeIntervalSince(pausedAt)) }
        if let pausedAt, let nextShotAt { self.nextShotAt = nextShotAt.addingTimeInterval(date.timeIntervalSince(pausedAt)) }
        pausedAt = nil
    }

    /// The club most players would pull from here.
    func suggestedClub() -> GolfClub {
        if lie == .green { return .putter }
        if lie == .bunker { return .wedge }
        // Lie penalties scale launch SPEED, not yardage linearly. Compare actual
        // simulated reach, otherwise rough/sand club recommendations overpromise.
        for candidate in [GolfClub.wedge, .iron] {
            let reach = BallFlight.simulate(candidate.launch(power: lie.powerFactor, aimDegrees: 0, curveDegrees: 0)).total
            if distanceToTarget <= reach * 0.98 { return candidate }
        }
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
        stanceAim = 0
        manualAimSelected = false
        nextShotAt = nil
        curve = 0
        target = nil
        isReplay = false
        club = suggestedClub()
        shotType = club == .putter ? .putt : .full
        phase = .ready
    }

    private func lieAfterDrop(_ shot: RangeShot) -> CourseLie {
        let dropLie = hole.lie(at: shot.nextPosition)
        return dropLie == .water || dropLie == .outOfBounds ? .rough : dropLie
    }

    private func saveBestIfNeeded() {
        guard playerCount == 1, scores[0].allSatisfy({ $0 != nil }) else { return }
        let result = toPar(for: 0)
        if best == nil || result < best! {
            best = result
            defaults.set(result, forKey: Self.bestKey(course))
        }
    }

    // New routings have different pars/distances; retain old records without comparing them.
    private static func bestKey(_ course: Course) -> String { course.bestScoreKey }

    private static func loadBest(course: Course, defaults: UserDefaults) -> Int? {
        defaults.object(forKey: bestKey(course)) as? Int
    }
}
