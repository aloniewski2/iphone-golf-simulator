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
    static func beaconOpacity(cameraDistance: Double) -> Double {
        let t=max(0,min(1,(cameraDistance-45)/45))
        return t*t*(3-2*t)
    }
}

/// What a putt has to deal with between the ball and the target: how much it climbs or falls,
/// and which way the ground tips it. Read from the same terrain the ball rolls on.
struct GreenRead: Equatable {
    /// Rise from ball to target as a fraction of the distance (0.02 = 2% uphill); negative is downhill.
    let rise: Double
    /// Average cross-slope along the line: positive tips the ball right of the line.
    let crossSlope: Double

    init(terrain: Terrain, from ball: CoursePoint, to target: CoursePoint) {
        let distance = ball.distance(to: target)
        guard distance > 0.05 else { rise = 0; crossSlope = 0; return }
        rise = (terrain.elevation(at: target) - terrain.elevation(at: ball)) / distance
        let ux = (target.x - ball.x) / distance, ud = (target.d - ball.d) / distance
        var across = 0.0
        let samples = 8
        for i in 0..<samples {
            let t = (Double(i) + 0.5) / Double(samples)
            let g = terrain.gradient(at: CoursePoint(x: ball.x + ux * distance * t, d: ball.d + ud * distance * t))
            // Right of the line is (ud, -ux); a slope that falls to the right tips the ball right.
            across += -(g.dx * ud - g.dd * ux)
        }
        crossSlope = across / Double(samples)
    }

    var label: String {
        var parts: [String] = []
        if abs(rise) >= 0.004 { parts.append(String(format: "%@ %.1f%%", rise > 0 ? "UPHILL" : "DOWNHILL", abs(rise) * 100)) }
        if abs(crossSlope) >= 0.004 { parts.append(String(format: "BREAKS %@ %.1f%%", crossSlope > 0 ? "RIGHT" : "LEFT", abs(crossSlope) * 100)) }
        return parts.isEmpty ? "FLAT" : parts.joined(separator: " · ")
    }
}

/// A round of stroke play: each player plays a hole out in turn, then everyone moves to the next.
@MainActor
final class CourseRound: ObservableObject {
    enum Phase { case ready, charging, flying, landed, holed, complete }

    @Published private(set) var phase: Phase = .ready
    @Published var club: GolfClub = .driver {
        didSet {
            if oldValue != club {
                shotType = club == .putter ? .putt : (club == .wedge && lie == .bunker ? .bunker : .full)
            }
        }
    }
    @Published var aim = 0.0
    @Published var shotType: ShotType = .full
    @Published var shotShape: ShotShapeChoice = .straight
    @Published var trajectory: ShotTrajectory = .normal
    @Published var handedness: Handedness = .right
    @Published var practiceMode = false {
        didSet { if oldValue != practiceMode { cancelCharge(); practiceImpact = nil } }
    }
    @Published private(set) var practiceImpact: SwingImpact?
    var wind: CourseWind { hole.wind }
    @Published var curve = 0.0
    @Published var editingShot = false
    @Published var automaticProgression = false
    @Published private(set) var nextShotAt: Date?
    @Published private(set) var stanceAim = 0.0
    @Published private var manualAimSelected = false
    @Published var automaticAim = false {
        didSet { if automaticAim { target = nil; aim = 0; stanceAim = 0; curve = 0; manualAimSelected = false } }
    }
    var usesBodyAim: Bool { !automaticAim && !manualAimSelected }
    private var planningKey: String?
    private var planningHole: Hole?
    private var planningPower = 1.0
    private var strengthCache: (key: String, value: Double)?
    private var strengthHole: Hole?
    private var workerConditions: ShotPlanningConditions?
    private var workerPower: Double?
    private var strengthTask: Task<Void,Never>?
    private var frozenStrength: Double?
    private var puttCache: (origin: CoursePoint, hole: Hole, plan: PuttRecommendation)?
    /// Native sessions supply worker results; comparison rounds retain their synchronous API.
    var usesPreparedPuttRecommendations = false
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
    var targetLabel: String { automaticAim ? (club == .putter && lie == .green ? "AUTO · GREEN LINE" : intendedTarget == hole.pin ? "AUTO · HOLE" : "AUTO · SAFE LANDING") : intendedTarget == hole.pin ? "PIN" : target == nil ? "LANDING" : "TARGET" }
    var heading: Double { ball.heading(to: intendedTarget) }
    var aimStep: Double { club == .putter ? 0.25 : 2 }
    var combinedAim: Double { automaticAim ? (automaticPuttPlan?.offsetDegrees ?? 0) : aim + stanceAim }
    var automaticPuttPlan: PuttRecommendation? {
        guard automaticAim, club == .putter, lie == .green else { return nil }
        if puttCache?.origin != ball || puttCache?.hole != hole {
            guard !usesPreparedPuttRecommendations else { return nil }
            puttCache = (ball, hole, PuttRecommendation.solve(from: ball, hole: hole))
        }
        return puttCache?.plan
    }

    @discardableResult
    func acceptPuttRecommendation(_ plan: PuttRecommendation, origin: CoursePoint, hole: Hole) -> Bool {
        guard usesPreparedPuttRecommendations, phase == .ready, automaticAim, club == .putter,
              lie == .green, ball == origin, self.hole == hole,
              plan.offsetDegrees.isFinite, abs(plan.offsetDegrees) <= 60,
              plan.power.isFinite, (0.001...1).contains(plan.power),
              plan.missYards.isFinite, plan.missYards >= 0 else { return false }
        objectWillChange.send()
        puttCache = (origin, hole, plan)
        planningKey = nil
        return true
    }
    /// Independent of the live charge; this marker never chases the user's swing.
    var recommendedPower: Double {
        if phase == .charging, let frozenStrength { return frozenStrength }
        if let plan = automaticPuttPlan { return plan.power }
        if hole.fairwayBoundary != nil {
            let conditions=ShotPlanningConditions(request:shotRequest(SwingImpact(power:1,curveDegrees:curve)),
                origin:ball,target:intendedTarget,lie:lie,hole:hole)
            if workerConditions != conditions {
                strengthTask?.cancel()
                workerConditions=conditions; workerPower=nil
                strengthTask=Task { @MainActor [weak self] in
                    let worker=Task.detached(priority:.userInitiated) { conditions.solve() }
                    let value=await withTaskCancellationHandler { await worker.value } onCancel: { worker.cancel() }
                    guard !Task.isCancelled, let self, self.workerConditions == conditions else { return }
                    self.workerPower=value
                    self.planningKey=nil
                    self.objectWillChange.send()
                }
            }
            return workerPower ?? conditions.initialPower
        }
        let key = "\(club.rawValue)-\(shotType.rawValue)-\(lie.rawValue)-\(ball)-\(intendedTarget)-\(shotShape)-\(trajectory)-\(handedness)-\(wind)-\(heading)-\(combinedAim)-\(curve)"
        if strengthCache?.key != key || strengthHole != hole {
            // Select strength against the same course trajectory as release, including wind,
            // shape and ground. Coarse bounded search also works for non-monotonic hazard outcomes.
            var bestPower = 1.0, bestError = Double.infinity
            for step in 1...20 {
                let power = Double(step) / 20
                let candidate = RangeShot(id: 0, request: shotRequest(SwingImpact(power: power)), origin: ball,
                    lieFactor: club == .putter ? 1 : lie.powerFactor, hole: hole)
                let error = candidate.rest.distance(to: intendedTarget) + (candidate.penaltyStrokes > 0 ? 100 : 0)
                if error < bestError { bestError = error; bestPower = power }
            }
            strengthCache = (key, bestPower)
            strengthHole = hole
        }
        return strengthCache?.value ?? 1
    }
    var holeNavigation: HoleNavigation {
        HoleNavigation(ball: ball, pin: hole.pin, aimHeading: heading + combinedAim)
    }
    /// The putt in front of the player, when there is one to read.
    var greenRead: GreenRead? {
        guard club == .putter || lie == .green else { return nil }
        return GreenRead(terrain: hole.terrain, from: ball, to: intendedTarget)
    }

    /// An explicit aim choice wins over the held-grip offset without resetting tracking.
    func aimAtPin() {
        selectTarget(hole.pin)
    }

    func setStanceAim(_ degrees: Double) {
        guard !automaticAim, phase == .ready, !editingShot, !manualAimSelected, degrees.isFinite else { return }
        let value = (max(-StanceAimSettler.range, min(StanceAimSettler.range, degrees)) * 2).rounded() / 2
        if stanceAim != value { stanceAim = value }
    }

    /// Resume relative to the chosen line, without jumping the target or moving the ball.
    func resumeBodyAim(at degrees: Double) {
        guard !automaticAim, phase == .ready, !editingShot, degrees.isFinite else { return }
        let line = combinedAim
        manualAimSelected = false
        setStanceAim(degrees)
        aim = line - stanceAim
    }

    /// Cached ideal-center prediction using the same launch/lie/flight model as release.
    /// It is an aim guide, not a promise about the player's future swing speed or strike.
    var trajectoryPreview: RangeShot {
        let key = "\(club.rawValue)-\(shotType.rawValue)-\(lie.rawValue)-\(distanceToTarget)-\(ball)-\(automaticAim)-\(shotShape)-\(trajectory)-\(wind)-\(handedness)"
        if key != planningKey || planningHole != hole {
            planningKey = key
            planningHole = hole
            planningPower = recommendedPower
        }
        let predictedPower = phase == .charging ? max(0.05, (power * 20).rounded() / 20) : planningPower
        let request = shotRequest(SwingImpact(power: predictedPower, curveDegrees: curve))
        if previewKey != request || previewOrigin != ball || previewLie != lie || previewHole != hole {
            previewCache = RangeShot(id: 0, request: request, origin: ball,
                lieFactor: club == .putter ? 1 : lie.powerFactor, hole: hole)
            previewKey = request; previewOrigin = ball; previewLie = lie; previewHole = hole
        }
        return previewCache!
    }

    func selectTarget(_ point: CoursePoint) {
        guard !automaticAim, phase == .ready, point.x.isFinite, point.d.isFinite, ball.distance(to: point) > 0.01 else { return }
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
        guard !automaticAim, phase == .ready, degrees.isFinite else { return }
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
        if phase != .charging { frozenStrength=recommendedPower }
        power = min(max(value.isFinite ? value : 0, 0), 1)
        phase = .charging
    }

    /// Practice records feedback only, without asking the solver for a recommended charge.
    @discardableResult
    func recordPracticeImpact(_ impact: SwingImpact) -> Bool {
        guard practiceMode, canSwing, impact.power.isFinite, impact.power > 0,
              impact.confidence.isFinite, impact.confidence >= 0.45 else { return false }
        var bounded = impact
        bounded.power = min(impact.power, 1)
        practiceImpact = bounded
        cancelCharge()
        return true
    }

    func shotRequest(_ impact: SwingImpact) -> ShotRequest {
        ShotRequest(club: club, targetHeading: heading + combinedAim,
            type: club == .putter ? .putt : shotType.supports(club:club,lie:lie) ? shotType : .full, execution: impact,
            shape: club == .putter || shotType != .full ? .straight : shotShape,
            trajectory: club == .putter || shotType == .chip ? .normal : trajectory,
            handedness: handedness, wind: wind, simulationVersion: hole.simulationVersion)
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
        if practiceMode {
            recordPracticeImpact(impact)
            return false // A practice swing cannot create a scored event, sound or replay.
        }
        impact.curveDegrees += self.curve
        let request = shotRequest(impact)
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

    /// Accepts a worker-built trajectory through the existing round/scoring lifecycle.
    /// The session additionally fences this by round generation and frozen preparation input.
    @discardableResult
    func acceptPreparedShot(_ shot: RangeShot, at date: Date) -> Bool {
        guard (phase == .ready || phase == .charging), !practiceMode, shot.id == strokeNumber, shot.origin == ball,
              shot.request.execution.confidence >= 0.45, shot.power > 0 else { return false }
        activeShot = shot
        power = shot.power
        isReplay = false
        flightStart = date
        phase = .flying
        return true
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
        shotShape = .straight
        trajectory = .normal
        target = nil
        club = suggestedClub()
        shotType = club == .putter ? .putt : club == .wedge && lie == .bunker ? .bunker : .full
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
        // Reference reaches include the nonlinear effect of lie penalties on speed.
        // All values are verified against the solver, with no integration on this UI path.
        ClubSelectionReference.club(distance: distanceToTarget, lie: lie)
    }

    #if DEBUG
    /// UI fixture only: seed completed prior turns, leaving a real final putt to play.
    /// Callers must supply isolated defaults so fixture scores never become user records.
    func prepareFinalTurnForTesting(tied: Bool, finishAtStrokeCap: Bool = false) {
        restart()
        scores = (0..<playerCount).map { player in
            course.holes.enumerated().map { index, hole in
                if player == playerCount - 1 && index == course.holes.count - 1 { return nil }
                return hole.par + (finishAtStrokeCap && index == course.holes.count - 1 ? Self.strokesOverParCap : 0)
                    + (!tied && player > 0 && index == 0 ? 1 : 0)
            }
        }
        holeIndex = course.holes.count - 1
        playerIndex = playerCount - 1
        beginTurn()
        strokes = hole.par - 1 + (finishAtStrokeCap ? Self.strokesOverParCap : 0)
        dropOnGreenForTesting(yards: 0.5)
        automaticAim = true
    }

    /// Development only: start the current turn with the ball on the green, `yards` short of the pin.
    func dropOnGreenForTesting(yards: Double = 8) {
        guard phase == .ready else { return }
        ball = CoursePoint(x: hole.pin.x, d: hole.pin.d - yards)
        lie = .green
        target = nil
        club = suggestedClub()
        shotType = .putt
    }
    #endif

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
        shotShape = .straight
        trajectory = .normal
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
