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
        let required: [BodyJoint] = [
            .nose, .neck, .leftShoulder, .rightShoulder, .leftElbow, .rightElbow,
            .leftWrist, .rightWrist, .root, .leftHip, .rightHip,
            .leftKnee, .rightKnee, .leftAnkle, .rightAnkle
        ]
        return required.allSatisfy { point($0, minimumConfidence: 0.55) != nil }
    }

    var bodyBounds: CGRect? {
        let visible = points.values.filter { $0.confidence >= 0.45 }.map(\.location)
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
        guard let leftWrist = point(.leftWrist, minimumConfidence: 0.55),
              let rightWrist = point(.rightWrist, minimumConfidence: 0.55),
              let leftHip = point(.leftHip, minimumConfidence: 0.55),
              let rightHip = point(.rightHip, minimumConfidence: 0.55),
              let width = shoulderWidth else { return false }
        return leftWrist.x < leftHip.x - width * 0.12
            && rightWrist.x > rightHip.x + width * 0.12
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
