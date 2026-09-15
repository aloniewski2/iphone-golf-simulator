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

