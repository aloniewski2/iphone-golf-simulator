import Foundation

/// Shared input preference; independent of either renderer's screen.
enum SwingInput: String, CaseIterable, Identifiable {
    case phone, touch
    var id: Self { self }
    var title: String {
        switch self {
        case .touch: "Touch"
        case .phone: "Phone"
        }
    }
}

enum Handedness: String, CaseIterable, Identifiable, Codable, Sendable {
    case right, left
    var id: Self { self }
    var displayName: String { rawValue.capitalized }
}

enum SwingPhase: String, Sendable {
    case findingPlayer, address, backswing, downswing, impact, followThrough, finish

    var displayName: String {
        switch self {
        case .findingPlayer: "Step into frame"
        case .address: "Ready"
        case .backswing: "Backswing"
        case .downswing: "Downswing"
        case .impact: "Impact"
        case .followThrough: "Follow through"
        case .finish: "Shot complete"
        }
    }
}

struct SwingMetrics: Equatable, Sendable {
    let duration: TimeInterval
    let backswingDuration: TimeInterval
    let downswingDuration: TimeInterval
    let tempo: Double
    let normalizedWristSpeed: Double
    let shoulderRotationDegrees: Double
    let hipRotationDegrees: Double
    let swingDirection: Double
    let impactHeightDelta: Double
    let balance: Double
    let confidence: Double
}

enum CameraPoseSwingEvent: Equatable, Sendable {
    case phaseChanged(SwingPhase)
    case shotReady(SwingMetrics)
}
