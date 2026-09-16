import CoreGraphics
import Foundation

/// The pre-swing checklist the accurate apps insist on, evaluated live: the player fills enough
/// of the frame, the phone stands level, the exposure is sane, there is one person in view, and
/// the swing joints have been read confidently for a moment. Arms only move on a green list.
struct CaptureReadiness: Equatable, Sendable {
    enum Check: CaseIterable, Sendable {
        case bodySize, level, exposure, singlePerson, jointsSteady

        var title: String {
            switch self {
            case .bodySize: "Framing"
            case .level: "Phone level"
            case .exposure: "Light"
            case .singlePerson: "One player"
            case .jointsSteady: "Tracking"
            }
        }

        var fix: String {
            switch self {
            case .bodySize: "Step closer: fill at least half the frame"
            case .level: "Stand the phone up straight"
            case .exposure: "Fix the light: too dark or too bright"
            case .singlePerson: "Only one person in view"
            case .jointsSteady: "Hold still so the arms lock on"
            }
        }
    }

    struct Input: Equatable, Sendable {
        var frame: PoseFrame?
        /// Side-to-side tilt of the phone, degrees.
        var rollDegrees: Double = 0
        /// Lean back (positive) or forward, degrees.
        var pitchDegrees: Double = 0
        /// Exposure offset from the camera's target, in EV; large magnitudes mean under/over-exposed.
        var exposureOffsetEV: Double = 0
        var peopleInView: Int = 1
    }

    /// Fraction of the frame height the body should fill (OnForm: at least 50 %).
    var minimumBodyHeight = 0.5
    var maximumRoll = 8.0
    var pitchRange = -6.0...28.0
    var maximumExposureOffset = 1.2
    /// Seconds of confident swing joints before the arms are considered locked on.
    var steadyDuration = 0.8
    var jointConfidence: Float = 0.5

    private(set) var failing: [Check] = Check.allCases
    private var steadySince: Double?

    var isReady: Bool { failing.isEmpty }
    var firstProblem: Check? { failing.first }

    mutating func evaluate(_ input: Input, at time: Double) {
        var failing: [Check] = []
        if let frame = input.frame, let height = Self.bodyHeight(of: frame) {
            if height < minimumBodyHeight { failing.append(.bodySize) }
        } else {
            failing.append(.bodySize)
        }
        if abs(input.rollDegrees) > maximumRoll || !pitchRange.contains(input.pitchDegrees) { failing.append(.level) }
        if abs(input.exposureOffsetEV) > maximumExposureOffset { failing.append(.exposure) }
        if input.peopleInView != 1 { failing.append(.singlePerson) }

        let swingJoints: [BodyJoint] = [.leftShoulder, .rightShoulder, .leftElbow, .rightElbow, .leftWrist, .rightWrist]
        let confident = input.frame.map { frame in
            swingJoints.allSatisfy { (frame.points[$0]?.confidence ?? 0) >= jointConfidence }
        } ?? false
        if confident {
            steadySince = steadySince ?? time
            if time - steadySince! < steadyDuration { failing.append(.jointsSteady) }
        } else {
            steadySince = nil
            failing.append(.jointsSteady)
        }
        self.failing = failing
    }

    mutating func reset() {
        failing = Check.allCases
        steadySince = nil
    }

    /// Body height as a fraction of the frame: head to ankles when the feet are visible,
    /// otherwise estimated from the torso (a body is about 3.3 torsos tall).
    static func bodyHeight(of frame: PoseFrame) -> Double? {
        let top = frame.point(.nose)?.y ?? frame.point(.neck)?.y
        if let top, let left = frame.point(.leftAnkle), let right = frame.point(.rightAnkle) {
            return Double(top - (left.y + right.y) / 2)
        }
        if let torso = frame.torsoLength { return Double(torso) * 3.3 }
        return nil
    }
}
