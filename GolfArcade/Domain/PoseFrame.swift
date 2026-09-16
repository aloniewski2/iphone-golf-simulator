import CoreGraphics
import Foundation

enum BodyJoint: String, CaseIterable, Hashable, Sendable {
    case nose, neck, leftShoulder, rightShoulder, leftElbow, rightElbow
    case leftWrist, rightWrist, root, leftHip, rightHip
    case leftKnee, rightKnee, leftAnkle, rightAnkle
}

struct PosePoint: Equatable, Sendable {
    let location: CGPoint
    let confidence: Float
}

struct PoseFrame: Equatable, Sendable {
    let timestamp: TimeInterval
    let points: [BodyJoint: PosePoint]

    func point(_ joint: BodyJoint, minimumConfidence: Float = 0.25) -> CGPoint? {
        guard let point = points[joint], point.confidence >= minimumConfidence else { return nil }
        return point.location
    }

    var trackingConfidence: Double {
        let important: [BodyJoint] = [.leftShoulder, .rightShoulder, .leftWrist, .rightWrist, .leftHip, .rightHip]
        let values = important.compactMap { points[$0]?.confidence }
        guard !values.isEmpty else { return 0 }
        return Double(values.reduce(0, +)) / Double(important.count)
    }

    /// Shoulders, hips, and at least one wrist. In a golf grip the wrists travel together, and one is
    /// routinely hidden behind the arms at the top of the backswing.
    var hasPlayableBody: Bool {
        [.leftShoulder, .rightShoulder, .leftHip, .rightHip].allSatisfy { point($0) != nil } && handCenter != nil
    }

    var hasCalibrationBody: Bool {
        let stableCore: [BodyJoint] = [
            .nose, .leftShoulder, .rightShoulder, .leftElbow, .rightElbow,
            .leftHip, .rightHip, .leftKnee, .rightKnee
        ]
        let hasCore = stableCore.allSatisfy { point($0, minimumConfidence: 0.40) != nil }
        let hasHand = [.leftWrist, .rightWrist].contains { point($0, minimumConfidence: 0.35) != nil }
        let hasFoot = [.leftAnkle, .rightAnkle].contains { point($0, minimumConfidence: 0.35) != nil }
        return hasCore && hasHand && hasFoot
    }

    var bodyBounds: CGRect? {
        let visible = points.values.filter { $0.confidence >= 0.45 }.map(\.location)
        guard let first = visible.first else { return nil }
        return visible.dropFirst().reduce(CGRect(origin: first, size: .zero)) { bounds, point in
            bounds.union(CGRect(origin: point, size: .zero))
        }
    }

    /// Bounds for calibration use the joints that passed the calibration confidence gate. This
    /// keeps a temporarily weak second ankle from shrinking the measured person.
    var calibrationBounds: CGRect? {
        let joints: [BodyJoint] = [
            .nose, .leftShoulder, .rightShoulder, .leftElbow, .rightElbow,
            .leftWrist, .rightWrist, .leftHip, .rightHip,
            .leftKnee, .rightKnee, .leftAnkle, .rightAnkle
        ]
        let visible = joints.compactMap { point($0, minimumConfidence: 0.35) }
        guard let first = visible.first else { return nil }
        return visible.dropFirst().reduce(CGRect(origin: first, size: .zero)) { bounds, point in
            bounds.union(CGRect(origin: point, size: .zero))
        }
    }

    var shoulderCenter: CGPoint? {
        guard let left = point(.leftShoulder, minimumConfidence: 0.35),
              let right = point(.rightShoulder, minimumConfidence: 0.35) else { return nil }
        return CGPoint(x: (left.x + right.x) / 2, y: (left.y + right.y) / 2)
    }

    var shoulderWidth: CGFloat? {
        guard let left = point(.leftShoulder, minimumConfidence: 0.35),
              let right = point(.rightShoulder, minimumConfidence: 0.35) else { return nil }
        return hypot(right.x - left.x, right.y - left.y)
    }

    var hasOpenCalibrationPose: Bool {
        guard let center = shoulderCenter, let width = shoulderWidth else { return false }
        let wrists = [.leftWrist, .rightWrist].compactMap { point($0, minimumConfidence: 0.35) }
        guard !wrists.isEmpty else { return false }
        if wrists.count == 1 {
            return abs(wrists[0].x - center.x) >= width * 0.62
        }
        let xs = wrists.map(\.x)
        return (xs.max() ?? center.x) - (xs.min() ?? center.x) >= width * 1.55
    }

    var stabilityCenter: CGPoint? {
        if let root = point(.root, minimumConfidence: 0.30) { return root }
        guard let leftHip = point(.leftHip, minimumConfidence: 0.35),
              let rightHip = point(.rightHip, minimumConfidence: 0.35) else { return nil }
        return CGPoint(x: (leftHip.x + rightHip.x) / 2, y: (leftHip.y + rightHip.y) / 2)
    }

    var handCenter: CGPoint? {
        switch (point(.leftWrist), point(.rightWrist)) {
        case let (left?, right?): CGPoint(x: (left.x + right.x) / 2, y: (left.y + right.y) / 2)
        case let (only?, nil), let (nil, only?): only
        case (nil, nil): nil
        }
    }

    /// Shoulder centre to hip centre. The body-size unit that makes swing thresholds independent
    /// of how far the player stands from the camera.
    var torsoLength: CGFloat? {
        guard let ls = point(.leftShoulder), let rs = point(.rightShoulder),
              let lh = point(.leftHip), let rh = point(.rightHip) else { return nil }
        return hypot((ls.x + rs.x - lh.x - rh.x) / 2, (ls.y + rs.y - lh.y - rh.y) / 2)
    }

    var shoulderAngle: Double? { angle(from: .leftShoulder, to: .rightShoulder) }
    var hipAngle: Double? { angle(from: .leftHip, to: .rightHip) }

    private func angle(from first: BodyJoint, to second: BodyJoint) -> Double? {
        guard let a = point(first), let b = point(second) else { return nil }
        return atan2(Double(b.y - a.y), Double(b.x - a.x)) * 180 / .pi
    }
}

/// Vision returns landmarks in the orientation supplied to its request handler. If a device's
/// delivered buffer and the requested capture orientation disagree, an upright player can arrive
/// 90 degrees off in Vision coordinates. Use the shoulder-to-hip axis as a device-independent
/// sanity check and correct only an unambiguously sideways torso. This keeps normal golf lean
/// untouched while preventing a sideways pose from failing every calibration framing check.
struct UprightPoseResult: Equatable, Sendable {
    let frame: PoseFrame
    let quarterTurned: Bool
}

extension PoseFrame {
    func correctingSidewaysOrientation() -> UprightPoseResult {
        guard let leftShoulder = point(.leftShoulder, minimumConfidence: 0.35),
              let rightShoulder = point(.rightShoulder, minimumConfidence: 0.35),
              let leftHip = point(.leftHip, minimumConfidence: 0.35),
              let rightHip = point(.rightHip, minimumConfidence: 0.35) else {
            return UprightPoseResult(frame: self, quarterTurned: false)
        }

        let shoulders = CGPoint(
            x: (leftShoulder.x + rightShoulder.x) / 2,
            y: (leftShoulder.y + rightShoulder.y) / 2
        )
        let hips = CGPoint(
            x: (leftHip.x + rightHip.x) / 2,
            y: (leftHip.y + rightHip.y) / 2
        )
        let horizontal = shoulders.x - hips.x
        let vertical = shoulders.y - hips.y
        guard abs(horizontal) > max(abs(vertical) * 1.25, 0.06) else {
            return UprightPoseResult(frame: self, quarterTurned: false)
        }

        let corrected = points.mapValues { posePoint in
            let point = posePoint.location
            let location: CGPoint
            if horizontal > 0 {
                // The head is to the right: rotate coordinates counter-clockwise.
                location = CGPoint(x: 1 - point.y, y: point.x)
            } else {
                // The head is to the left: rotate coordinates clockwise.
                location = CGPoint(x: point.y, y: 1 - point.x)
            }
            return PosePoint(location: location, confidence: posePoint.confidence)
        }
        return UprightPoseResult(
            frame: PoseFrame(timestamp: timestamp, points: corrected),
            quarterTurned: true
        )
    }
}
