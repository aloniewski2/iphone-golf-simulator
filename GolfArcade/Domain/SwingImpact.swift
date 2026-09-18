import Foundation

enum SwingInputEvent: Equatable {
    case load(Double)
    case cancel
    case impact(SwingImpact)
}

enum ShotInputSource: String, Equatable, Sendable { case camera, phone, touch, demo }

/// Input-independent execution, deliberately separate from a recommended target.
/// Camera direction is an arcade estimate, not a measured physical club face.
struct SwingImpact: Equatable, Sendable {
    var power: Double
    var startLineDegrees: Double = 0
    var curveDegrees: Double = 0
    var strike: StrikeQuality = .center
    var confidence: Double = 1
    var source: ShotInputSource = .touch
}
