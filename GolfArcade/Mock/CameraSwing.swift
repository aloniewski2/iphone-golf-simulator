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
    var onEvent: ((SwingInputEvent) -> Void)?
    var handedness: Handedness = .right {
        didSet { detector.handedness = handedness }
    }

    let tracker = CameraPoseTracker()
    private var detector = ArmSwingDetector()
    private var subscriptions: Set<AnyCancellable> = []

    var isRunning: Bool { !subscriptions.isEmpty }

    func start() {
        guard !isRunning else { return }
        detector = ArmSwingDetector()
        detector.handedness = handedness
        tracker.$status.receive(on: DispatchQueue.main).sink { [weak self] in self?.status = $0 }.store(in: &subscriptions)
        tracker.$latestFrame.dropFirst().receive(on: DispatchQueue.main).sink { [weak self] in self?.process($0) }.store(in: &subscriptions)
        tracker.start()
    }

    func stop() {
        subscriptions.removeAll()
        tracker.stop()
        detector = ArmSwingDetector()
        detector.handedness = handedness
        frame = nil
        swingAngle = 0
        lastStrike = nil
        phase = .findingPlayer
    }

    private func process(_ frame: PoseFrame?) {
        self.frame = frame
        let time = frame?.timestamp ?? ProcessInfo.processInfo.systemUptime
        let event = detector.ingest(frame.flatMap(ArmSwingDetector.Sample.init), at: time)
        phase = switch detector.phase {
        case .findingPlayer: .findingPlayer
        case .address: .address
        case .backswing: .backswing
        case .downswing: .downswing
        case .finish: .finish
        }
        swingAngle = detector.swingAngle
        if case .impact(_, _, let strike) = event { lastStrike = strike }
        if case .load = event { lastStrike = nil }
        if let event { onEvent?(event) }
    }
}
