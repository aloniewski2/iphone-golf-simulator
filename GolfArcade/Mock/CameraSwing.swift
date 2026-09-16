import Combine
import CoreGraphics
import CoreMotion
import Foundation

/// Drives the range from the front camera: Vision body pose → `ArmSwingDetector` → range events.
/// Only the shoulders and hands are read; the ball launches at the detected impact frame.
///
/// A readiness checklist (framing, level phone, light, one person, steady tracking) gates the
/// detector: the player can settle at address, but the meter only starts filling on a green list.
@MainActor
final class CameraSwingController: ObservableObject {
    @Published private(set) var phase: SwingPhase = .findingPlayer
    @Published private(set) var status: CameraPoseTracker.Status = .idle
    @Published private(set) var frame: PoseFrame?
    /// Degrees along the swing arc, for the golfer avatar: positive back, negative through.
    @Published private(set) var swingAngle = 0.0
    @Published private(set) var readiness = CaptureReadiness()
    var onEvent: ((SwingInputEvent) -> Void)?
    var handedness: Handedness = .right {
        didSet { detector.handedness = handedness }
    }
    /// 0–1 confidence of the joints the swing depends on.
    var trackingQuality: Double { frame?.trackingConfidence ?? 0 }
    var benchmark3D: Bool {
        get { tracker.benchmark3D }
        set { tracker.benchmark3D = newValue }
    }

    let tracker = CameraPoseTracker()
    private var detector = ArmSwingDetector()
    private let motion = CMMotionManager()
    private var tilt = (roll: 0.0, pitch: 0.0)
    private var subscriptions: Set<AnyCancellable> = []

    var isRunning: Bool { !subscriptions.isEmpty }

    func start() {
        guard !isRunning else { return }
        detector = ArmSwingDetector()
        detector.handedness = handedness
        detector.armed = false
        readiness.reset()
        tracker.$status.receive(on: DispatchQueue.main).sink { [weak self] in self?.status = $0 }.store(in: &subscriptions)
        tracker.$latestFrame.compactMap { $0 }.receive(on: DispatchQueue.main).sink { [weak self] in self?.process($0) }.store(in: &subscriptions)
        tracker.start()
        if motion.isDeviceMotionAvailable {
            motion.deviceMotionUpdateInterval = 0.1
            motion.startDeviceMotionUpdates(to: .main) { [weak self] data, _ in
                guard let gravity = data?.gravity else { return }
                // Portrait phone propped up facing the player: gravity is straight down its long
                // axis. Side tilt is the x component, lean back/forward the z component.
                let roll = atan2(gravity.x, -gravity.y) * 180 / .pi
                let pitch = atan2(-gravity.z, -gravity.y) * 180 / .pi
                MainActor.assumeIsolated { self?.tilt = (roll, pitch) }
            }
        }
    }

    func stop() {
        subscriptions.removeAll()
        tracker.stop()
        motion.stopDeviceMotionUpdates()
        detector = ArmSwingDetector()
        detector.handedness = handedness
        readiness.reset()
        frame = nil
        swingAngle = 0
        phase = .findingPlayer
    }

    private func process(_ frame: PoseFrame) {
        self.frame = frame
        readiness.evaluate(CaptureReadiness.Input(
            frame: frame, rollDegrees: tilt.roll, pitchDegrees: tilt.pitch,
            exposureOffsetEV: tracker.exposureOffsetEV, peopleInView: tracker.peopleInView
        ), at: frame.timestamp)
        // Once a backswing is under way the checklist stays out of it; it only decides whether one may start.
        if detector.phase == .address || detector.phase == .findingPlayer { detector.armed = readiness.isReady }
        let event = detector.ingest(ArmSwingDetector.Sample(frame: frame), at: frame.timestamp)
        phase = switch detector.phase {
        case .findingPlayer: .findingPlayer
        case .address: .address
        case .backswing: .backswing
        case .downswing: .downswing
        case .finish: .finish
        }
        swingAngle = detector.swingAngle
        if let event { onEvent?(event) }
    }
}
