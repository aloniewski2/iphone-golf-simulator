import CoreGraphics
import Foundation
import simd

/// Turns the camera's 2D body pose into a 3D avatar pose that copies the player.
///
/// The front camera faces the golfer, so the image's horizontal axis runs along the target line
/// (avatar `z`), vertical is up, and the missing depth is toward the ball (avatar `+x`). Every
/// bone keeps the avatar's fixed length: when a limb looks shorter in the image than it really is,
/// the difference is taken as depth, reaching toward the ball. Scaling by the body scan's height
/// makes the avatar the same size however far the player stands from the phone.
struct PoseRetargeter {
    let calibration: PlayerCalibration
    let handedness: Handedness
    var frameAspect: CGFloat
    /// Joints missing for longer than this ease back toward the address pose.
    var gracePeriod = 0.6
    var easeBackDuration = 0.35

    private(set) var pose = AvatarAnimations.address
    private var filters: [BodyJoint: OneEuroFilter] = [:]
    private var lastSeen: [BodyJoint: Double] = [:]
    private var lastTime: Double?

    init(calibration: PlayerCalibration, handedness: Handedness, frameAspect: CGFloat) {
        self.calibration = calibration
        self.handedness = handedness
        self.frameAspect = frameAspect
    }

    /// Chains from the root outward: (joint, parent, bone length, depth sign).
    private static let chain: [(BodyJoint, BodyJoint, Float, Float)] = [
        (.neck, .root, AvatarSize.torso, 1),
        (.nose, .neck, AvatarSize.neckToHead, 1),
        (.leftShoulder, .neck, AvatarSize.shoulderHalfWidth, 0),
        (.rightShoulder, .neck, AvatarSize.shoulderHalfWidth, 0),
        (.leftElbow, .leftShoulder, AvatarSize.upperArm, 1),
        (.rightElbow, .rightShoulder, AvatarSize.upperArm, 1),
        (.leftWrist, .leftElbow, AvatarSize.forearm, 1),
        (.rightWrist, .rightElbow, AvatarSize.forearm, 1),
        (.leftHip, .root, AvatarSize.hipHalfWidth, 0),
        (.rightHip, .root, AvatarSize.hipHalfWidth, 0),
        (.leftKnee, .leftHip, AvatarSize.thigh, 1),
        (.rightKnee, .rightHip, AvatarSize.thigh, 1),
        (.leftAnkle, .leftKnee, AvatarSize.shin, -1),
        (.rightAnkle, .rightKnee, AvatarSize.shin, -1)
    ]

    @discardableResult
    mutating func update(_ frame: PoseFrame?, swingAngle: Double, at time: Double) -> BodyPose3D {
        let dt = lastTime.map { max(time - $0, 1.0 / 120) } ?? 1.0 / 30
        lastTime = time
        guard let frame, let rootImage = Self.root(of: frame) else {
            easeMissing(Set(BodyJoint.allCases), at: time, dt: dt)
            return pose
        }

        let scale = AvatarSize.height / Float(max(calibration.anchor.height, 0.05))
        let zSign: Float = handedness == .right ? 1 : -1
        // Smooth the image points, then solve: bones keep exact lengths and jitter is filtered.
        var smoothed: [BodyJoint: SIMD2<Float>] = [.root: .zero]
        for joint in BodyJoint.allCases where joint != .root {
            guard let point = frame.point(joint) else { continue }
            let raw = simd_float3(Float((point.x - rootImage.x) * frameAspect) * zSign * scale, Float(point.y - rootImage.y) * scale, 0)
            var filter = filters[joint] ?? OneEuroFilter()
            let value = filter.filter(raw, dt: dt)
            filters[joint] = filter
            smoothed[joint] = SIMD2(value.x, value.y)
        }
        func image(_ joint: BodyJoint) -> SIMD2<Float>? { smoothed[joint] }

        var raw: [BodyJoint: simd_float3] = [.root: simd_float3(0, AvatarSize.hipHeight, 0)]
        var missing: Set<BodyJoint> = []
        for (joint, parent, length, depthSign) in Self.chain {
            let parentPoint = raw[parent] ?? pose[parent]
            guard let child2D = image(joint), let parent2D = image(parent) else {
                missing.insert(joint)
                // Carry the joint with its parent so a lost wrist still follows the arm.
                raw[joint] = parentPoint + (pose[joint] - pose[parent])
                continue
            }
            var projected = child2D - parent2D // (z, y)
            let projectedLength = simd_length(projected)
            if projectedLength > length { projected *= length / projectedLength }
            let depth = depthSign == 0 ? 0 : sqrt(max(0, length * length - simd_length_squared(projected))) * depthSign
            raw[joint] = parentPoint + simd_float3(depth, projected.y, projected.x)
            lastSeen[joint] = time
        }

        // Stand on the ground: lift or lower the whole body so the lower ankle touches down.
        let ankles = [BodyJoint.leftAnkle, .rightAnkle].filter { !missing.contains($0) }.compactMap { raw[$0]?.y }
        if let lowest = ankles.min() {
            let lift = 0.12 - lowest
            for key in raw.keys { raw[key]!.y += lift }
        }

        var joints: [BodyJoint: simd_float3] = [:]
        for joint in BodyJoint.allCases {
            var value = raw[joint] ?? pose[joint]
            if missing.contains(joint), let seen = lastSeen[joint], time - seen > gracePeriod {
                let t = Float(min(1, (time - seen - gracePeriod) / easeBackDuration))
                value = simd_mix(value, AvatarAnimations.address[joint], simd_float3(repeating: t))
            }
            joints[joint] = value
        }

        var next = BodyPose3D(joints: joints, clubDirection: pose.clubDirection)
        next.clubDirection = ClubGeometry.direction3D(
            hands: next.handCenter, shoulders: next.shoulderCenter, ball: AvatarSize.ball, swingAngle: swingAngle
        )
        pose = next
        return pose
    }

    private mutating func easeMissing(_ joints: Set<BodyJoint>, at time: Double, dt: Double) {
        var next = pose
        for joint in joints {
            guard let seen = lastSeen[joint], time - seen > gracePeriod else { continue }
            let t = Float(min(1, dt / easeBackDuration))
            next.joints[joint] = simd_mix(pose[joint], AvatarAnimations.address[joint], simd_float3(repeating: t))
        }
        if joints.count == BodyJoint.allCases.count, lastSeen.values.allSatisfy({ time - $0 > gracePeriod }) {
            next.clubDirection = simd_normalize(simd_mix(pose.clubDirection, AvatarAnimations.address.clubDirection, simd_float3(repeating: Float(min(1, dt / easeBackDuration)))))
        }
        pose = next
    }

    private static func root(of frame: PoseFrame) -> CGPoint? {
        if let left = frame.point(.leftHip), let right = frame.point(.rightHip) {
            return CGPoint(x: (left.x + right.x) / 2, y: (left.y + right.y) / 2)
        }
        return frame.point(.root)
    }
}

/// One-euro filter: smooths jitter when still, follows quickly when moving fast (a downswing).
struct OneEuroFilter {
    var minCutoff: Float = 1.4
    var beta: Float = 0.05
    var derivativeCutoff: Float = 1
    private var value: simd_float3?
    private var derivative = simd_float3.zero

    mutating func filter(_ input: simd_float3, dt: Double) -> simd_float3 {
        let dt = Float(dt)
        guard let previous = value else {
            value = input
            return input
        }
        let rawDerivative = (input - previous) / dt
        derivative = simd_mix(derivative, rawDerivative, simd_float3(repeating: Self.alpha(cutoff: derivativeCutoff, dt: dt)))
        let cutoff = minCutoff + beta * simd_length(derivative)
        let next = simd_mix(previous, input, simd_float3(repeating: Self.alpha(cutoff: cutoff, dt: dt)))
        value = next
        return next
    }

    private static func alpha(cutoff: Float, dt: Float) -> Float {
        let tau = 1 / (2 * .pi * cutoff)
        return 1 / (1 + tau / dt)
    }
}
