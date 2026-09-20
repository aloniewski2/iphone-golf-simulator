import Combine
import Observation
import QuartzCore
import os

/// A monotonic clock presented as Dates only at the legacy round API boundary.
/// Wall-clock changes cannot advance or rewind a shot. Pauses preserve the same epoch.
struct SessionClock {
    private let epoch: Date
    private var last: Double
    private var elapsed = 0.0
    init(now: Double = CACurrentMediaTime(), epoch: Date = .now) { self.last = now; self.epoch = epoch }
    mutating func advance(now: Double, running: Bool) -> Date {
        guard now.isFinite, now >= last else { return date }
        if running { elapsed += now - last }
        last = now
        return date
    }
    var date: Date { epoch.addingTimeInterval(elapsed) }
}

struct ShotPreparation: Sendable, Equatable {
    let generation: UInt64
    let courseID: String
    let holeIndex: Int
    let playerIndex: Int
    let stroke: Int
    let origin: CoursePoint
    let lie: CourseLie
    let hole: Hole
    let request: ShotRequest
    func solve() -> RangeShot {
        RangeShot(id: stroke, request: request, origin: origin,
                  lieFactor: request.club == .putter ? 1 : lie.powerFactor, hole: hole)
    }
}

/// Includes aim mode and readiness even when the uncorrected heading has not changed.
struct NativePlanningInput: Equatable, Sendable {
    let conditions: ShotPlanningConditions
    let automaticPutt: Bool
    let isReady: Bool
}

private extension ShotRequest {
    func aiming(at heading: Double) -> Self {
        Self(club: club, targetHeading: heading, type: type, execution: execution,
             shape: shape, trajectory: trajectory, handedness: handedness, wind: wind,
             simulationVersion: simulationVersion)
    }
}

@MainActor @Observable
final class GameSession {
    let round: CourseRound
    let motion: PhoneSwingController
    let players: [Player]
    private(set) var isActive = false
    private(set) var isPreparingShot = false
    private(set) var assetStatus = "Loading course assets…"
    private(set) var assetsReady = false
    private(set) var acceptedImpactCount = 0
    private(set) var bystanderHitCount = 0
    private(set) var flyoverStarted: Date?
    var flyoverProgress: Double? { flyoverStarted.map { min(1, max(0, date.timeIntervalSince($0) / 12)) } }
    var touchPower = 0.6
    private(set) var previewShot: RangeShot?
    private(set) var previewConditions: ShotPlanningConditions?
    private(set) var previewRevision = 0
    var showTrajectory = true
    var needsPuttRecommendation: Bool {
        round.automaticAim && round.club == .putter && round.lie == .green && round.automaticPuttPlan == nil
    }
    var planningConditions: ShotPlanningConditions {
        var request = round.shotRequest(SwingImpact(power: 1, curveDegrees: round.curve))
        // The worker searches from the pin line. Publishing its correction must not change
        // this input key and start an endless sequence of identical searches.
        if round.automaticAim && round.club == .putter && round.lie == .green { request = request.aiming(at: round.heading) }
        return ShotPlanningConditions(request: request,
            origin: round.ball, target: round.intendedTarget, lie: round.lie, hole: round.hole)
    }
    var planningInput: NativePlanningInput {
        NativePlanningInput(conditions: planningConditions,
            automaticPutt: round.automaticAim && round.club == .putter && round.lie == .green,
            isReady: round.phase == .ready)
    }
    var paused = false { didSet { synchronizeActivity() } }
    @ObservationIgnored private var foreground = true
    @ObservationIgnored private var generation: UInt64 = 0
    @ObservationIgnored private var previewGeneration: UInt64 = 0
    @ObservationIgnored private var preparationTask: Task<Void, Never>?
    @ObservationIgnored private var pending: ShotPreparation?
    @ObservationIgnored private var armedRequest: ShotRequest?
    @ObservationIgnored private var clock = SessionClock()
    @ObservationIgnored private var latestFrameTime = 0.0
    @ObservationIgnored private var frameTimes: [Double] = []
    @ObservationIgnored private var frameCursor = 0
    @ObservationIgnored private var impactLatencies: [Double] = []
    @ObservationIgnored private var impactCursor = 0
    @ObservationIgnored private var observedHole = -1
    @ObservationIgnored private var observedPlayer = -1
    @ObservationIgnored private var previousMotionStatus: PhoneSwingController.Status = .idle
    @ObservationIgnored private var shotSounds = NativeShotSoundTimeline()
    @ObservationIgnored private let audio = RangeAudio()
    @ObservationIgnored private let feedback = SwingFeedbackController()
    @ObservationIgnored private let preferences: UserDefaults
    @ObservationIgnored let performance = NativePerformanceAudit()
    @ObservationIgnored private let signposter = OSSignposter(subsystem: "com.aloniewski.GolfArcade", category: "NativeGame")
    @ObservationIgnored private var viewportStorage: GameViewportController?
    var viewport: GameViewportController {
        if let viewportStorage { return viewportStorage }
        let view = GameViewportController(session: self)
        viewportStorage = view
        return view
    }
    var player: Player { players[min(round.playerIndex, players.count - 1)] }
    var canConfigureShot: Bool { assetsReady && isActive && flyoverStarted == nil && !isPreparingShot && round.canSwing && !motion.isArmed && motion.status != .followThrough }
    var canSwing: Bool { canConfigureShot && !needsPuttRecommendation }
    var date: Date { clock.date }

    init(course: Course, players: [Player], practice: Bool = false, defaults: UserDefaults = .standard) {
        preferences = defaults
        self.players = players.isEmpty ? [Player(name: "Player 1", colorIndex: 0)] : Array(players.prefix(4))
        round = CourseRound(course: course, playerCount: self.players.count, defaults: defaults)
        round.usesPreparedPuttRecommendations = true
        round.automaticProgression = true
        round.practiceMode = practice
        motion = PhoneSwingController(performance: performance)
        motion.sensitivity = defaults.object(forKey: "controller.sensitivity") as? Double ?? 1.8
        audio.enabled = defaults.object(forKey: "range.soundEnabled") as? Bool ?? true
        feedback.isEnabled = defaults.object(forKey: "arcade.hapticsEnabled") as? Bool ?? true
        motion.onEvent = { [weak self] event in self?.receive(event) }
    }

    func start() {
        foreground = true
        isActive = foreground && !paused
        beginTurnIfNeeded()
        DisplayCoordinator.shared.present(self)
        if isActive, preferences.string(forKey: "range.swingInput") != "touch" { motion.start() }
    }

    func stop() {
        performance.flush(force: true)
        flyoverStarted = nil
        foreground = false; isActive = false
        cancelUnfinishedSwing()
        invalidatePreparation()
        motion.stop()
        viewportStorage?.stopPresentation()
        assetsReady = false
        observedHole = -1
        DisplayCoordinator.shared.end(self)
    }

    func setForeground(_ value: Bool) { foreground = value; synchronizeActivity() }

    private func synchronizeActivity() {
        _ = clock.advance(now: CACurrentMediaTime(), running: isActive)
        isActive = foreground && !paused
        latestFrameTime = 0
        performance.beginFrame(now: CACurrentMediaTime(), running: false, context: "")
        performance.flush(force: true)
        cancelUnfinishedSwing()
        if isActive {
            round.resume(at: date)
            if preferences.string(forKey: "range.swingInput") != "touch" { motion.start() }
        }
        else { round.pause(at: date); motion.stop() }
        // An accepted shot/preparation remains valid while paused; only unfinished input is cancelled.
    }

    func arm() {
        guard canSwing else { return }
        armedRequest = round.shotRequest(SwingImpact(power: 0, source: .phone))
        motion.setClub(round.club)
        motion.handedness = player.handedness
        motion.selectedAimDegrees = armedRequest!.targetHeading
        motion.arm()
    }

    /// View-owned task cancellation coalesces aim edits. Only immutable inputs
    /// reach the worker; a stale answer cannot replace the current target guide.
    func preparePreview(_ key: NativePlanningInput) async {
        guard key == planningInput else { return }
        previewGeneration &+= 1
        let revision = previewGeneration, roundGeneration = generation
        previewShot = nil; previewConditions = nil
        guard key.isReady else { return }
        let input = key.conditions
        do { try await Task.sleep(for: .milliseconds(100)) } catch { return }
        guard revision == previewGeneration, roundGeneration == generation,
              key == planningInput, armedRequest == nil, !isPreparingShot else { return }
        let worker = Task.detached(priority: .utility) { () -> (shot: RangeShot, putt: PuttRecommendation?)? in
            guard !Task.isCancelled else { return nil }
            var request = input.request
            let putt = key.automaticPutt ? PuttRecommendation.solve(from: input.origin, hole: input.hole) : nil
            let power: Double
            if let putt {
                power = putt.power
                request = request.aiming(at: input.origin.heading(to: input.hole.pin) + putt.offsetDegrees)
            } else if input.hole.fairwayBoundary != nil { power = input.solve() }
            else {
                var bestPower = 1.0, bestError = Double.infinity
                for step in 1...20 {
                    guard !Task.isCancelled else { return nil }
                    var request = input.request; request.execution.power = Double(step) / 20
                    let shot = RangeShot(id: 0, request: request, origin: input.origin,
                        lieFactor: request.club == .putter ? 1 : input.lie.powerFactor, hole: input.hole)
                    let error = shot.rest.distance(to: input.target) + (shot.penaltyStrokes > 0 ? 100 : 0)
                    if error < bestError { bestError = error; bestPower = request.execution.power }
                }
                power = bestPower
            }
            guard !Task.isCancelled else { return nil }
            request.execution.power = power
            let shot = RangeShot(id: 0, request: request, origin: input.origin,
                lieFactor: request.club == .putter ? 1 : input.lie.powerFactor, hole: input.hole)
            return (shot, putt)
        }
        let result = await withTaskCancellationHandler { await worker.value } onCancel: { worker.cancel() }
        guard !Task.isCancelled, revision == previewGeneration, roundGeneration == generation,
              key == planningInput, armedRequest == nil, !isPreparingShot, let result else { return }
        if let putt = result.putt {
            guard round.acceptPuttRecommendation(putt, origin: input.origin, hole: input.hole) else { return }
        }
        previewShot = result.shot; previewConditions = input; previewRevision &+= 1
    }

    func refreshPreferences() {
        motion.sensitivity = preferences.object(forKey: "controller.sensitivity") as? Double ?? 1.8
        audio.enabled = preferences.object(forKey: "range.soundEnabled") as? Bool ?? true
        feedback.isEnabled = preferences.object(forKey: "arcade.hapticsEnabled") as? Bool ?? true
    }

    func startFlyover() {
        guard assetsReady, isActive, round.phase == .ready, !isPreparingShot else { return }
        cancelUnfinishedSwing()
        flyoverStarted = date
    }
    func finishFlyover() { flyoverStarted = nil }

    func cancelUnfinishedSwing() {
        armedRequest = nil
        motion.disarm()
        round.cancelCharge()
        feedback.endBackswing()
        audio.stop()
    }

    private func receive(_ event: SwingInputEvent) {
        guard isActive else { return }
        switch event {
        case .load(let load):
            feedback.updateTension(load)
            audio.tension(load)
        case .cancel: armedRequest = nil; round.cancelCharge()
        case .impact(let impact):
            guard let frozen = armedRequest else { return }
            armedRequest = nil
            var request = frozen; request.execution = impact
            submit(request)
        }
    }

    func swingUsingTouch() {
        guard canSwing else { return }
        submit(round.shotRequest(SwingImpact(power: touchPower, source: .touch)))
    }

    func presentBystanderHits(_ count: Int) {
        guard isActive, count > 0 else { return }
        bystanderHitCount += count
        for _ in 0..<count { audio.thump() }
    }

    private func submit(_ request: ShotRequest) {
        guard assetsReady, isActive, round.canSwing, !isPreparingShot,
              request.execution.power.isFinite, request.execution.power > 0,
              request.execution.confidence.isFinite, request.execution.confidence >= 0.45 else { return }
        if round.practiceMode {
            guard round.recordPracticeImpact(request.execution) else { return }
            viewport.applyImpact(request, practice: true)
            recordImpactLatency(request)
            audio.whoosh(power: request.execution.power)
            return
        }
        generation &+= 1
        let input = ShotPreparation(generation: generation, courseID: round.course.id,
            holeIndex: round.holeIndex, playerIndex: round.playerIndex, stroke: round.strokeNumber,
            origin: round.ball, lie: round.lie, hole: round.hole, request: request)
        pending = input
        isPreparingShot = true
        acceptedImpactCount += 1
        let acceptedDate = date
        signposter.emitEvent("ImpactAccepted")
        viewport.applyImpact(request)
        recordImpactLatency(request)
        audio.impact(club: request.club, strike: request.execution.strike, power: request.execution.power)
        feedback.playImpact()
        let interval = signposter.beginInterval("TrajectoryPreparation")
        let preparationStarted = CACurrentMediaTime()
        preparationTask = Task { [weak self] in
            let worker = Task.detached(priority: .userInitiated) { input.solve() }
            let shot = await withTaskCancellationHandler { await worker.value } onCancel: { worker.cancel() }
            guard let self else { return }
            self.signposter.endInterval("TrajectoryPreparation", interval)
            self.performance.record(.trajectoryPreparation, milliseconds: (CACurrentMediaTime() - preparationStarted) * 1_000)
            guard !Task.isCancelled, self.pending == input, self.generation == input.generation else { return }
            self.isPreparingShot = false
            self.pending = nil
            guard self.round.course.id == input.courseID, self.round.holeIndex == input.holeIndex,
                  self.round.playerIndex == input.playerIndex, self.round.ball == input.origin else { return }
            // Use accepted impact time so worker cost does not delay ball motion/animation.
            _ = self.round.acceptPreparedShot(shot, at: acceptedDate)
            self.shotSounds.reset(for: shot)
        }
    }

    private func recordImpactLatency(_ request: ShotRequest) {
        guard request.execution.source == .phone, let callback = motion.latestMeasurement?.callbackTimestamp else { return }
        let latency = (CACurrentMediaTime() - callback) * 1_000
        guard latency.isFinite, latency >= 0 else { return }
        performance.record(.callbackToImpactApplication, milliseconds: latency)
        if impactLatencies.count < 256 { impactLatencies.append(latency) }
        else { impactLatencies[impactCursor] = latency; impactCursor = (impactCursor + 1) % 256 }
    }

    func advance(now: Double = CACurrentMediaTime()) {
        _ = clock.advance(now: now, running: isActive)
        guard isActive else { return }
        if latestFrameTime > 0 {
            let delta = now - latestFrameTime
            if frameTimes.count < 1_200 { frameTimes.append(delta) }
            else { frameTimes[frameCursor] = delta; frameCursor = (frameCursor + 1) % 1_200 }
        }
        latestFrameTime = now
        if flyoverProgress == 1 { finishFlyover() }
        updateMotionFeedback()
        if let shot = round.activeShot {
            for event in shotSounds.events(at: round.elapsed(at: date), shot: shot, hole: round.hole) {
                switch event {
                case .landing(let landing): audio.landing(landing)
                case .result:
                    switch AvatarAnimations.Reaction.classify(shot) {
                    case .pure, .holed: audio.celebrate()
                    case .bad, .disaster: audio.groan()
                    case .solid, .meh: if shot.lie == .green { audio.celebrate() }
                    }
                    if shot.isHoled { feedback.playImpact() }
                }
            }
        }
        round.advance(at: date)
        beginTurnIfNeeded()
    }

    func beginPerformanceFrame() {
        guard performance.enabled else { return }
        let route: String = if case .external = DisplayCoordinator.shared.destination { "external" } else { "phone" }
        let phase = flyoverStarted == nil ? String(describing: round.phase) : "flyover"
        performance.beginFrame(now: CACurrentMediaTime(), running: isActive && assetsReady,
            context: "\(route)/hole-\(round.hole.number)/\(phase)")
    }

    private func updateMotionFeedback() {
        let status = motion.status
        guard status != previousMotionStatus else { return }
        previousMotionStatus = status
        switch status {
        case .address: audio.ready()
        case .backswing: feedback.beginBackswing(interactive: true)
        case .downswing: audio.whoosh(power: max(0.3, motion.latestSnapshot?.load ?? 0))
        case .idle, .unavailable: feedback.endBackswing()
        default: break
        }
    }

    var frameTiming: (samples: Int, p95MS: Double) {
        let sorted = frameTimes.sorted()
        return (sorted.count, sorted.isEmpty ? 0 : sorted[min(sorted.count - 1, Int(Double(sorted.count) * 0.95))] * 1_000)
    }

    var impactTiming: (samples: Int, p95MS: Double) {
        let sorted = impactLatencies.sorted()
        return (sorted.count, sorted.isEmpty ? 0 : sorted[min(sorted.count - 1, Int(Double(sorted.count) * 0.95))])
    }

    func replay() {
        cancelUnfinishedSwing()
        guard round.phase == .landed || round.phase == .holed, let shot = round.activeShot else { return }
        round.replay(at: date)
        shotSounds.reset(for: shot)
        viewport.applyImpact(shot.request)
    }
    func next() {
        cancelUnfinishedSwing()
        if round.phase == .landed { round.nextShot() }
        else if round.phase == .holed { round.continueAfterHole() }
        beginTurnIfNeeded()
    }
    func restart() { invalidatePreparation(); cancelUnfinishedSwing(); round.restart(); observedHole = -1; beginTurnIfNeeded() }

    private func invalidatePreparation() {
        generation &+= 1; preparationTask?.cancel(); preparationTask = nil
        previewGeneration &+= 1; previewShot = nil; previewConditions = nil
        pending = nil; isPreparingShot = false
    }

    private func beginTurnIfNeeded() {
        guard observedHole != round.holeIndex || observedPlayer != round.playerIndex else { return }
        let holeChanged = observedHole != round.holeIndex
        observedHole = round.holeIndex; observedPlayer = round.playerIndex
        finishFlyover()
        cancelUnfinishedSwing()
        round.handedness = player.handedness
        motion.setClub(round.club)
        if holeChanged { viewport.loadHole() }
        else { viewport.updateAppearance(player.golferAppearance) }
    }

    func setAssetState(ready: Bool, message: String) {
        assetsReady = ready; assetStatus = message
        if !ready { cancelUnfinishedSwing() }
    }
}

/// One-shot presentation events, driven by the same monotonic trajectory time as
/// the ball. Replays explicitly reset this timeline; scoring is never involved.
@MainActor
struct NativeShotSoundTimeline {
    enum Event: Equatable { case landing(RangeAudio.Landing), result }
    private var landingTime: Double?
    private var landed = false
    private var finished = false
    mutating func reset(for shot: RangeShot) {
        landingTime = ShotCameraDirector.landingTime(of: shot)
        landed = false; finished = false
    }
    mutating func events(at elapsed: Double, shot: RangeShot, hole: Hole) -> [Event] {
        var events: [Event] = []
        if !landed, let landingTime, elapsed >= landingTime {
            landed = true
            let point = shot.position(at: landingTime)
            let before = shot.position(at: max(0, landingTime - 1.0 / 30))
            let speed = hypot(point.lateralYards - before.lateralYards, point.distanceYards - before.distanceYards) * 30
            switch hole.lie(at: .init(x: point.lateralYards, d: point.distanceYards)) {
            case .water: events.append(.landing(.water))
            case .bunker: events.append(.landing(.sand))
            case let lie: events.append(.landing(.turf(lie, speed: speed)))
            }
        }
        if !finished, elapsed >= shot.duration {
            finished = true
            if shot.isHoled { events.append(.landing(.cup)) }
            events.append(.result)
        }
        return events
    }
}
