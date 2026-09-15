import Combine
import Foundation

@MainActor
final class ArcadeGameSession: ObservableObject {
    @Published var selectedClub: GolfClub = .driver
    @Published var handedness: Handedness = .right {
        didSet { stateMachine.handedness = handedness }
    }
    @Published private(set) var phase: SwingPhase = .findingPlayer
    @Published private(set) var lastShot: ShotResult?
    @Published private(set) var shotCount = 0

    private var stateMachine = SwingStateMachine()
    private let shotEngine = ArcadeShotEngine()

    func process(_ frame: PoseFrame) {
        guard let event = stateMachine.ingest(frame) else { return }
        switch event {
        case .phaseChanged(let newPhase): phase = newPhase
        case .shotReady(let metrics):
            lastShot = shotEngine.calculate(metrics: metrics, club: selectedClub)
            shotCount += 1
            phase = .finish
        }
    }

    func selectNextClub(direction: Int) {
        let clubs = GolfClub.allCases
        guard let current = clubs.firstIndex(of: selectedClub) else { return }
        selectedClub = clubs[(current + direction + clubs.count) % clubs.count]
    }

    func createDemoShot() {
        let variants: [(Double, Double, Double)] = [(2.7, 0.02, 0.92), (3.5, 0.34, 0.86), (2.2, -0.28, 0.78), (4.1, 0.08, 0.94)]
        let variant = variants[shotCount % variants.count]
        let metrics = SwingMetrics(
            duration: 1.1, backswingDuration: 0.72, downswingDuration: 0.28, tempo: 2.57,
            normalizedWristSpeed: variant.0, shoulderRotationDegrees: 32,
            hipRotationDegrees: 18, swingDirection: variant.1, impactHeightDelta: 0,
            balance: 0.9, confidence: variant.2
        )
        lastShot = shotEngine.calculate(metrics: metrics, club: selectedClub)
        shotCount += 1
        phase = .finish
    }
}

