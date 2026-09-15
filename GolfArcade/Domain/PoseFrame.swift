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

    var hasPlayableBody: Bool {
        [.leftShoulder, .rightShoulder, .leftWrist, .rightWrist, .leftHip, .rightHip]
            .allSatisfy { point($0) != nil }
    }

    var handCenter: CGPoint? {
        guard let left = point(.leftWrist), let right = point(.rightWrist) else { return nil }
        return CGPoint(x: (left.x + right.x) / 2, y: (left.y + right.y) / 2)
    }

    var shoulderAngle: Double? { angle(from: .leftShoulder, to: .rightShoulder) }
    var hipAngle: Double? { angle(from: .leftHip, to: .rightHip) }

    private func angle(from first: BodyJoint, to second: BodyJoint) -> Double? {
        guard let a = point(first), let b = point(second) else { return nil }
        return atan2(Double(b.y - a.y), Double(b.x - a.x)) * 180 / .pi
    }
}

