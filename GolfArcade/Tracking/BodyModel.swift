import CoreGraphics
import Foundation

/// Skeleton constraints for a 2D pose. Bone lengths are learned while the player stands at
/// address, then every frame is checked against them the way a 3D model stays rigid:
/// a projected bone can look *shorter* than its true length (it is turning toward the camera)
/// but never longer, and the two wrists of a golf grip are never far apart. Joints that break
/// these rules are wrong and are dropped so they cannot leak into the swing.
struct BodyModel: Equatable, Sendable {
    struct Bone: Hashable, Sendable {
        let from: BodyJoint
        let to: BodyJoint
    }

    static let armBones = [
        Bone(from: .leftShoulder, to: .leftElbow), Bone(from: .leftElbow, to: .leftWrist),
        Bone(from: .rightShoulder, to: .rightElbow), Bone(from: .rightElbow, to: .rightWrist)
    ]
    static let shoulderBone = Bone(from: .leftShoulder, to: .rightShoulder)

    /// Frames of confident joints needed before lengths are trusted.
    var calibrationFrames = 15
    /// A bone may project up to this much longer than its calibrated length before it is rejected.
    var stretchTolerance = 1.18
    /// Wrists farther apart than this many shoulder widths cannot both be right.
    var maximumGripSpread = 0.7
    var minimumConfidence: Float = 0.45

    private(set) var lengths: [Bone: CGFloat] = [:]
    private var samples: [Bone: [CGFloat]] = [:]
    private var sampleCount = 0

    var isCalibrated: Bool { !lengths.isEmpty }

    /// Learns from a frame while the player is still, and returns the frame with any joint that
    /// violates the learned skeleton removed.
    mutating func apply(to frame: PoseFrame, isStill: Bool) -> PoseFrame {
        if !isCalibrated {
            if isStill { learn(from: frame) }
            return frame
        }
        var points = frame.points
        for bone in Self.armBones {
            guard let limit = lengths[bone],
                  let a = points[bone.from], let b = points[bone.to],
                  a.confidence >= minimumConfidence || b.confidence >= minimumConfidence else { continue }
            let length = hypot(b.location.x - a.location.x, b.location.y - a.location.y)
            if length > limit * stretchTolerance {
                // The lower-confidence end is the likely culprit.
                let culprit = a.confidence < b.confidence ? bone.from : bone.to
                points[culprit] = nil
            }
        }
        if let width = lengths[Self.shoulderBone], let left = points[.leftWrist], let right = points[.rightWrist] {
            let spread = hypot(right.location.x - left.location.x, right.location.y - left.location.y)
            if spread > width * maximumGripSpread {
                points[left.confidence < right.confidence ? .leftWrist : .rightWrist] = nil
            }
        }
        return PoseFrame(timestamp: frame.timestamp, points: points)
    }

    mutating func reset() {
        lengths = [:]
        samples = [:]
        sampleCount = 0
    }

    private mutating func learn(from frame: PoseFrame) {
        let bones = Self.armBones + [Self.shoulderBone]
        var measured: [Bone: CGFloat] = [:]
        for bone in bones {
            guard let a = frame.points[bone.from], let b = frame.points[bone.to],
                  a.confidence >= minimumConfidence, b.confidence >= minimumConfidence else { return }
            measured[bone] = hypot(b.location.x - a.location.x, b.location.y - a.location.y)
        }
        for (bone, length) in measured { samples[bone, default: []].append(length) }
        sampleCount += 1
        guard sampleCount >= calibrationFrames else { return }
        for (bone, values) in samples {
            let sorted = values.sorted()
            lengths[bone] = sorted[sorted.count / 2] // the median shrugs off a few bad frames
        }
    }
}
