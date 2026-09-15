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
        tracker.$latestFrame.compactMap { $0 }.receive(on: DispatchQueue.main).sink { [weak self] in self?.process($0) }.store(in: &subscriptions)
        tracker.start()
    }

    func stop() {
        subscriptions.removeAll()
        tracker.stop()
        detector = ArmSwingDetector()
        detector.handedness = handedness
        frame = nil
        phase = .findingPlayer
    }

    private func process(_ frame: PoseFrame) {
        self.frame = frame
        let event = detector.ingest(ArmSwingDetector.Sample(frame: frame), at: frame.timestamp)
        phase = switch detector.phase {
        case .findingPlayer: .findingPlayer
        case .address: .address
        case .backswing: .backswing
        case .downswing: .downswing
        case .finish: .finish
        }
        if let event { onEvent?(event) }
    }
}
