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
    /// Degrees along the swing arc, for the golfer avatar: positive back, negative through.
    @Published private(set) var swingAngle = 0.0
    @Published private(set) var lastStrike: StrikeQuality?
    /// Where the hands crossed the ball on the last impact, in shoulder widths.
    @Published private(set) var lastStrikeOffset: CGVector?
    /// Live hands offset from the ball's hand target, in shoulder widths.
    @Published private(set) var handsOffset: CGVector?
    /// True while the player is in view but must move their hands over the virtual ball.
    @Published private(set) var needsLineUp = false
    /// 0–1 while the player holds a still address over the ball; 1 means they are ready to play.
    @Published private(set) var readyProgress = 0.0
    static let readyHoldDuration = 1.5
    var onEvent: ((SwingInputEvent) -> Void)?
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
        didSet { detector.handedness = handedness }
    }
    var ballAddress: BallAddress? {
        didSet { detector.ballAddress = ballAddress }
    }

    let tracker = CameraPoseTracker()
    private var detector = ArmSwingDetector()
    private var gestureRecognizer = HandGestureRecognizer()
    private var subscriptions: Set<AnyCancellable> = []

    var isRunning: Bool { !subscriptions.isEmpty }

    func start() {
        // Another screen (the body scan) may have stopped the shared tracker; always restart it.
        guard !isRunning else { tracker.start(); return }
        detector = ArmSwingDetector()
        detector.handedness = handedness
        detector.ballAddress = ballAddress
        tracker.$status.receive(on: DispatchQueue.main).sink { [weak self] in self?.status = $0 }.store(in: &subscriptions)
        tracker.$latestFrame.dropFirst().receive(on: DispatchQueue.main).sink { [weak self] in self?.process($0) }.store(in: &subscriptions)
        tracker.start()
    }

    func stop() {
        subscriptions.removeAll()
        tracker.stop()
        detector = ArmSwingDetector()
        detector.handedness = handedness
        detector.ballAddress = ballAddress
        frame = nil
        swingAngle = 0
        lastStrike = nil
        lastStrikeOffset = nil
        handsOffset = nil
        needsLineUp = false
        readyProgress = 0
        phase = .findingPlayer
    }

    private func process(_ frame: PoseFrame?) {
        self.frame = frame
        let time = frame?.timestamp ?? ProcessInfo.processInfo.systemUptime
        let event = detector.ingest(frame.flatMap(ArmSwingDetector.Sample.init), at: time)
        phase = switch detector.phase {
        case .findingPlayer, .lineUp: .findingPlayer
        case .address: .address
        case .backswing: .backswing
        case .downswing: .downswing
        case .finish: .finish
        }
        swingAngle = detector.swingAngle
        needsLineUp = detector.phase == .lineUp
        let progress = detector.addressHeldSince.map { min(1, (time - $0) / Self.readyHoldDuration) } ?? 0
        if progress != readyProgress { readyProgress = progress }
        handsOffset = detector.handsOffset
        if case .impact(_, _, let strike) = event {
            lastStrike = strike
            lastStrikeOffset = detector.lastStrikeOffset
        }
        if case .load = event {
            lastStrike = nil
            lastStrikeOffset = nil
        }
        if let event { onEvent?(event) }
        recognizeGesture(frame, at: time, afterImpact: { if case .impact = event { true } else { false } }())
    }

    private func recognizeGesture(_ frame: PoseFrame?, at time: Double, afterImpact: Bool) {
        guard gesturesEnabled else { return }
        // A golf swing is never a menu gesture.
        if afterImpact || detector.phase == .backswing || detector.phase == .downswing {
            gestureRecognizer.suppress(until: time + 1)
        }
        let gesture = gestureRecognizer.ingest(frame, at: time)
        let armed = !gestureRecognizer.armed.isEmpty
        if gestureArmed != armed { gestureArmed = armed }
        guard let gesture else { return }
        lastGesture = (gesture, Date())
        gestures.send(gesture)
    }
}
