import Combine
import CoreGraphics
import Foundation

/// Drives the range from the front camera: Vision body pose → `ArmSwingDetector` → range events.
/// Only the shoulders and hands are read; the ball launches at the detected impact frame.
@MainActor
final class CameraSwingController: ObservableObject {
    @Published private(set) var phase: SwingPhase = .findingPlayer
    @Published private(set) var status: CameraPoseTracker.Status = .idle
    @Published private(set) var frame: PoseFrame?
    @Published private(set) var virtualClub: VirtualClubState?
    /// Degrees along the swing arc, for the golfer avatar: positive back, negative through.
    @Published private(set) var swingAngle = 0.0
    @Published private(set) var addressAimDegrees = 0.0
    @Published private(set) var lastStrike: StrikeQuality?
    /// Where the hands crossed the ball on the last impact, in shoulder widths.
    @Published private(set) var lastStrikeOffset: CGVector?
    /// Live hands offset from the ball's hand target, in shoulder widths.
    @Published private(set) var handsOffset: CGVector?
    /// True while the player is in view but must move their hands over the virtual ball.
    @Published private(set) var needsLineUp = false
    /// 0–1 while the player holds a still address over the ball; 1 means they are ready to play.
    @Published private(set) var readyProgress = 0.0
    @Published private(set) var readiness: ArmSwingDetector.Readiness = .bodyNotVisible
    @Published private(set) var isPositionLocked = false
    @Published private(set) var reviewSecondsRemaining = 0
    @Published private(set) var lockedAddress: BallAddress?
    @Published private(set) var swingCheck = CameraSwingCheck()
    var requiresSwingCheck = false
    var contactAssistance = false {
        didSet { detector.contactMode = contactAssistance ? .assisted : .geometric }
    }
    var isCheckingSwing: Bool { requiresSwingCheck && isPositionLocked && !swingCheck.isComplete }
    /// Seconds the locked ball is reviewed before play. UI tests may stretch it with
    /// `-positionReviewSeconds N`, since a slow test runner cannot keep up with a 5 s countdown.
    static let positionReviewDuration: Double = {
        #if DEBUG
        let arguments = ProcessInfo.processInfo.arguments
        if let index = arguments.firstIndex(of: "-positionReviewSeconds"), arguments.indices.contains(index + 1),
           let seconds = Double(arguments[index + 1]), seconds >= 3, seconds <= 60 {
            return seconds
        }
        #endif
        return 5
    }()
    var requiresPositionReview = false {
        didSet { detector.requiresVisibleFeetForSetup = requiresPositionReview }
    }
    private var reviewEndsAt: Double?
    private var reviewCompleted = false
    var hasPlayableTracking: Bool {
        !isCheckingSwing && reviewSecondsRemaining == 0 && virtualClub != nil && (readiness == .ready || readiness == .swinging)
    }
    /// Keep the measured club through follow-through and brief dropouts; never predict contact
    /// from this frozen presentation. The detector owns recovery and collision decisions.
    var hasVisibleClub: Bool {
        virtualClub != nil
    }
    private var captureTime = 0.0
    private var lastImpactTime: Double?
    private var orientationTracking = false
    var onEvent: ((SwingInputEvent) -> Void)?
    /// Optional lab observer. Normal play does not retain pose recordings.
    var onObservation: ((CameraSwingObservation) -> Void)?
    /// Fist swipes and punches for navigating menus and the club rail.
    let gestures = PassthroughSubject<NavGesture, Never>()
    @Published private(set) var lastGesture: (gesture: NavGesture, at: Date)?
    /// True while a hand is armed for a gesture, for on-screen feedback.
    @Published private(set) var gestureArmed = false
    var gesturesEnabled = false {
        didSet {
            tracker.setHandPoseEnabled(gesturesEnabled)
            if !gesturesEnabled { gestureRecognizer = HandGestureRecognizer(); gestureArmed = false }
        }
    }
    var handedness: Handedness = .right {
        didSet {
            detector.handedness = handedness
            // Never retain a right-handed address/review for a newly left-handed player.
            if oldValue != handedness { resetAddress() }
        }
    }
    var ballAddress: BallAddress? {
        didSet { detector.ballAddress = ballAddress }
    }
    var displayAddress: BallAddress? {
        if let lockedAddress { return lockedAddress }
        guard let virtualClub else { return nil }
        return BallAddress(ball: virtualClub.space.image(virtualClub.address.ball),
            handTarget: virtualClub.space.image(virtualClub.address.grip),
            shoulderWidth: virtualClub.space.width / virtualClub.space.aspect)
    }
    private var club: GolfClub = .driver
    private var shotType: ShotType = .full

    func setClub(_ club: GolfClub, type: ShotType = .full) {
        self.club = club
        shotType = type
        detector.configure(for: club, type: type)
        resetPresentation()
    }

    func resetAddress() {
        detector.resetAddress()
        reviewCompleted = false
        swingCheck = CameraSwingCheck()
        resetPresentation()
    }

    func adjustGround(by imageDelta: CGFloat) {
        guard isPositionLocked, phase != .backswing, phase != .downswing else { return }
        detector.adjustGround(by: imageDelta)
        reviewCompleted = false
        resetPresentation()
    }

    func checkSwingAgain() {
        swingCheck = CameraSwingCheck()
        requiresSwingCheck = true
        reviewCompleted = false
        resetPresentation()
        detector.swingsEnabled = false
    }

    private func resetPresentation() {
        virtualClub = nil
        readyProgress = 0
        phase = .findingPlayer
        readiness = .bodyNotVisible
        needsLineUp = false
        swingAngle = 0
        addressAimDegrees = 0
        handsOffset = nil
        lastStrike = nil
        lastStrikeOffset = nil
        lastImpactTime = nil
        isPositionLocked = detector.isPositionLocked
        lockedAddress = detector.lockedDisplayAddress
        reviewSecondsRemaining = 0
        reviewEndsAt = nil
        detector.swingsEnabled = true
        // Geometry must be cleared before the frame notification reaches the scene.
        frame = nil
    }

    let tracker = CameraPoseTracker()
    private var detector = ArmSwingDetector()
    private var gestureRecognizer = HandGestureRecognizer()
    private var aimSignalRecognizer = AimSignalRecognizer()
    /// The side of an outstretched arm at address, for on-screen feedback.
    @Published private(set) var aimSignal: AimSignalRecognizer.Side?
    private var subscriptions: Set<AnyCancellable> = []
    #if DEBUG
    private let motionTrace = CameraMotionTrace.requested()
    #endif

    var isRunning: Bool { !subscriptions.isEmpty }

    var isSyntheticPreview: Bool {
        #if DEBUG && targetEnvironment(simulator)
        ProcessInfo.processInfo.arguments.contains("-fixtureLockedCamera")
        #else
        false
        #endif
    }

    func start() {
        // Another screen (the body scan) may have stopped the shared tracker; always restart it.
        guard !isRunning else { if !isSyntheticPreview { tracker.start() }; return }
        detector.configure(for: club, type: shotType)
        detector.handedness = handedness
        detector.ballAddress = ballAddress
        #if DEBUG && targetEnvironment(simulator)
        if isSyntheticPreview {
            // An explicitly labeled UI-test fixture, never available in a phone build.
            // `-fixtureHumanSwing` swaps the hands-on-a-circle arc for a full-body golfer;
            // `-fixtureStanceYaw <degrees>` turns that golfer's stance for aiming.
            let arguments = ProcessInfo.processInfo.arguments
            let humanSwing = arguments.contains("-fixtureHumanSwing")
            let fixture = humanSwing
                ? SyntheticGolfer(handedness: handedness, stanceYaw: UserDefaults.standard.double(forKey: "fixtureStanceYaw")).poses()
                : BenchmarkReplay.fixture().poses
            let playsSwing = arguments.contains("-fixtureSwingAfterLock")
            var swingStarted: Double?
            var checkStarted: Double?
            status = .running
            // Deliberately faster than the course clock: catches view-owned timer starvation
            // that a synchronized 30 Hz fixture used to hide.
            Timer.publish(every: 1.0 / 60, on: .main, in: .common).autoconnect().sink { [weak self] _ in
                guard let self else { return }
                let now = ProcessInfo.processInfo.systemUptime
                if !self.isCheckingSwing { checkStarted = nil }
                if self.isCheckingSwing, checkStarted == nil { checkStarted = now + 0.3 }
                if playsSwing, self.requiresPositionReview, self.reviewCompleted, swingStarted == nil {
                    swingStarted = now + 0.75
                }
                let offset = swingStarted.map { max(0, now - $0) }
                    ?? (self.isCheckingSwing ? checkStarted.map { max(0, now - $0) } : nil) ?? 0
                let pose = fixture.last(where: { $0.time <= offset }) ?? fixture[0]
                let next = fixture.first(where: { $0.time > offset }) ?? pose
                let fraction = max(0, min(1, (offset - pose.time) / max(next.time - pose.time, 0.001)))
                let points = pose.frame!.points.mapValues { $0 }
                var interpolated = points
                for (joint, point) in points {
                    guard let target = next.frame?.points[joint] else { continue }
                    interpolated[joint] = PosePoint(location: CGPoint(
                        x: point.location.x + (target.location.x - point.location.x) * fraction,
                        y: point.location.y + (target.location.y - point.location.y) * fraction), confidence: point.confidence)
                }
                if self.handedness == .left, !humanSwing {
                    interpolated = interpolated.mapValues {
                        PosePoint(location: CGPoint(x: 1 - $0.location.x, y: $0.location.y), confidence: $0.confidence)
                    }
                }
                var frame = PoseFrame(timestamp: now, points: interpolated)
                if let orientation = pose.frame?.orientation, offset - pose.time < 0.1 {
                    frame.orientation = BodyOrientation(timestamp: now, shoulderYaw: orientation.shoulderYaw, hipYaw: orientation.hipYaw)
                }
                self.process(PoseDelivery(frame: frame, captureTime: now,
                    aspect: 0.75, bodyCount: 1, callbackStarted: now, inferenceFinished: now))
            }.store(in: &subscriptions)
            return
        }
        #endif
        tracker.$status.receive(on: DispatchQueue.main).sink { [weak self] in self?.status = $0 }.store(in: &subscriptions)
        tracker.$latestDelivery.dropFirst().receive(on: DispatchQueue.main).sink { [weak self] in
            if let delivery = $0 { self?.process(delivery) }
        }.store(in: &subscriptions)
        tracker.start()
    }

    func stop() {
        subscriptions.removeAll()
        tracker.stop()
        orientationTracking = false
        tracker.setBodyOrientationEnabled(false)
        detector.configure(for: club, type: shotType)
        detector.handedness = handedness
        detector.ballAddress = ballAddress
        resetPresentation()
    }

    private var lastCaptureGeometry: CaptureGeometry?

    func process(_ delivery: PoseDelivery, now: Double = ProcessInfo.processInfo.systemUptime) {
        if let geometry = delivery.captureEvidence?.geometry {
            if let previous = lastCaptureGeometry, previous != geometry {
                resetAddress()
                onEvent?(.cancel)
            }
            lastCaptureGeometry = geometry
        }
        // The monotonic deadline also advances from incoming frames, independent of SwiftUI.
        advancePositionReview(at: now)
        let frame = delivery.frame
        let time = delivery.captureTime
        captureTime = time
        let sample = frame.flatMap { ArmSwingDetector.Sample(frame: $0, frameAspect: delivery.aspect, certifiedSpace: detector.certifiedSpace) }
        if requiresSwingCheck && !swingCheck.isComplete { detector.swingsEnabled = false }
        let event = detector.ingest(sample, at: time)
        isPositionLocked = detector.isPositionLocked
        lockedAddress = detector.lockedDisplayAddress
        if !isPositionLocked {
            reviewCompleted = false; reviewEndsAt = nil; reviewSecondsRemaining = 0
            swingCheck = CameraSwingCheck()
        }
        if isCheckingSwing {
            var check = swingCheck
            check.ingest(angle: detector.swingAngle, frame: frame,
                         sample: detector.readiness == .recovering ? nil : sample, at: time)
            if check != swingCheck { swingCheck = check }
        }
        if isPositionLocked, !isCheckingSwing, requiresPositionReview, !reviewCompleted, reviewEndsAt == nil {
            reviewEndsAt = now + Self.positionReviewDuration
            reviewSecondsRemaining = Int(Self.positionReviewDuration)
            detector.swingsEnabled = false
        }
        if isPositionLocked, !isCheckingSwing, !requiresPositionReview { detector.swingsEnabled = true }
        readiness = sample == nil && detector.readiness != .recovering && detector.readiness != .retry
            ? .missingSample(in: frame) : detector.readiness
        phase = switch detector.phase {
        case .findingPlayer, .lineUp: .findingPlayer
        case .address: .address
        case .backswing: .backswing
        case .downswing: .downswing
        case .finish: .finish
        }
        // Read the stance line only while the player sets up; the 3D pose is too costly to
        // run through a swing, and the line is frozen by then anyway.
        let wantsOrientation = isPositionLocked && (phase == .address || phase == .findingPlayer)
        if wantsOrientation != orientationTracking {
            orientationTracking = wantsOrientation
            tracker.setBodyOrientationEnabled(wantsOrientation)
        }
        swingAngle = detector.swingAngle
        let aim = (detector.addressAimDegrees * 2).rounded() / 2
        if addressAimDegrees != aim { addressAimDegrees = aim }
        needsLineUp = detector.phase == .lineUp
        let progress = detector.readyProgress
        if progress != readyProgress { readyProgress = progress }
        handsOffset = detector.handsOffset
        virtualClub = detector.virtualClub ?? detector.setupClub
        if case .impact(let impact) = event {
            lastImpactTime = time
            lastStrike = impact.strike
            lastStrikeOffset = detector.lastStrikeOffset
        }
        if case .load = event {
            lastStrike = nil
            lastStrikeOffset = nil
        }
        // Publish the frame after its matching geometry so rendering never uses last-frame contact.
        self.frame = frame
        onObservation?(CameraSwingObservation(delivery: delivery, event: event,
            phase: String(describing: detector.phase), stateUpdated: ProcessInfo.processInfo.systemUptime,
            readiness: readiness.rawValue))
        #if DEBUG
        motionTrace?.record(.init(pose: BenchmarkPose(frame: frame, time: time, aspect: delivery.aspect),
            candidates: delivery.candidateFrames.map { BenchmarkPose(frame: $0, time: time, aspect: delivery.aspect) },
            phase: String(describing: detector.phase), readiness: readiness.rawValue,
            locked: isPositionLocked, reviewSeconds: reviewSecondsRemaining, angle: swingAngle,
            event: event.map { String(describing: $0) }))
        #endif
        if let event { onEvent?(event) }
        recognizeGesture(frame, at: time, afterImpact: { if case .impact = event { true } else { false } }())
        recognizeAimSignal(frame, at: time)
    }

    /// An arm held out to the side at address steps the line that way (see `AimSignalRecognizer`).
    /// Unlike the menu gestures this needs no hand pose and runs whenever the player is set up
    /// at the ball, which is exactly when the line needs moving.
    private func recognizeAimSignal(_ frame: PoseFrame?, at time: Double) {
        let atAddress = isPositionLocked && !isCheckingSwing && detector.phase == .address
        let side = atAddress ? aimSignalRecognizer.ingest(frame, at: time) : nil
        if !atAddress { aimSignalRecognizer.clear() }
        let signalling = aimSignalRecognizer.side
        if aimSignal != signalling { aimSignal = signalling }
        guard let side else { return }
        let gesture: NavGesture = side == .left ? .left : .right
        lastGesture = (gesture, Date())
        gestures.send(gesture)
    }

    /// A view-owned timer drives the review; no delayed task can collapse a later player's setup.
    func advancePositionReview(at time: Double = ProcessInfo.processInfo.systemUptime) {
        guard let end = reviewEndsAt else { return }
        let remaining = max(0, Int(ceil(end - time)))
        if reviewSecondsRemaining != remaining { reviewSecondsRemaining = remaining }
        if reviewSecondsRemaining == 0 {
            reviewEndsAt = nil
            reviewCompleted = true
            detector.swingsEnabled = true
        }
    }

    private func recognizeGesture(_ frame: PoseFrame?, at time: Double, afterImpact: Bool) {
        guard gesturesEnabled, !isCheckingSwing else { return }
        // A golf swing is never a menu gesture.
        if afterImpact || detector.phase == .backswing || detector.phase == .downswing {
            gestureRecognizer.suppress(until: time + 1)
        }
        let gesture = gestureRecognizer.ingest(frame, at: time)
        let armed = !gestureRecognizer.armed.isEmpty
        if gestureArmed != armed { gestureArmed = armed }
        guard let gesture else { return }
        // Set up at the ball, a swipe left or right moves the line like the arm signal does;
        // club changes and selection wait until the shot is played.
        if requiresPositionReview, isPositionLocked, gesture != .left, gesture != .right { return }
        lastGesture = (gesture, Date())
        gestures.send(gesture)
    }
}

/// One non-scoring rehearsal verifies that BOTH sides of the motion are visible.
/// It never emits a swing event, moves the ball, or requires a fast practice stroke.
struct CameraSwingCheck: Equatable {
    enum Stage { case takeBack, swingThrough, returnToGrip, complete }
    private(set) var stage: Stage = .takeBack
    private var sign = 1.0
    private var lastVisible: Double?
    private var returnedAt: Double?
    private var returnedGrip: CGPoint?
    var isComplete: Bool { stage == .complete }
    var title: String {
        switch stage {
        case .takeBack: "Swing check: slowly take it back"
        case .swingThrough: "Swing check: slowly follow through"
        case .returnToGrip: "Swing check: return to your grip"
        case .complete: "Swing check complete"
        }
    }
    var detail: String { "One slow rehearsal, no shot. Keep your grip in view on both sides. Then hold still to see the ball." }

    mutating func ingest(angle: Double, frame: PoseFrame?, sample: ArmSwingDetector.Sample?, at time: Double) {
        guard !isComplete else { return }
        let wristVisible = frame.map { frame in
            [BodyJoint.leftWrist, .rightWrist].contains {
                guard let point = frame.point($0, minimumConfidence: 0.45) else { return false }
                return (0.03...0.97).contains(point.x) && (0.03...0.97).contains(point.y)
            }
        } ?? false
        guard wristVisible, let sample, sample.confidence >= 0.45, angle.isFinite else {
            returnedAt = nil
            returnedGrip = nil
            if let lastVisible, time - lastVisible > 0.6 { self = CameraSwingCheck() }
            return
        }
        if let lastVisible, time - lastVisible > 0.6 { self = CameraSwingCheck() }
        lastVisible = time
        switch stage {
        case .takeBack:
            if abs(angle) >= 35 { sign = angle >= 0 ? 1 : -1; stage = .swingThrough }
        case .swingThrough:
            if angle * sign <= -20 { stage = .returnToGrip }
        case .returnToGrip:
            if abs(angle) <= 10, sample.hasComfortableGrip {
                if let grip = returnedGrip, hypot(sample.grip.x - grip.x, sample.grip.y - grip.y) > 0.08 {
                    returnedAt = nil
                    returnedGrip = nil
                }
                returnedAt = returnedAt ?? time
                returnedGrip = returnedGrip ?? sample.grip
                if time - returnedAt! >= 0.35 { stage = .complete }
            } else { returnedAt = nil; returnedGrip = nil }
        case .complete: break
        }
    }
}
