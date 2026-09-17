import CoreGraphics
import Foundation
import simd

/// Turns the camera's 2D body pose into a 3D avatar pose that copies the player.
///
/// The front camera faces the golfer, so the image's horizontal axis runs along the target line
/// (avatar `z`), vertical is up, and the missing depth is toward the ball (avatar `+x`). Every
/// bone keeps the avatar's fixed length. Missing depth is ambiguous, so forward reach is
/// bounded rather than treating every foreshortened bone as pointing straight toward the ball.
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
    private var cameraProjection: CameraPoseProjection?

    init(calibration: PlayerCalibration, handedness: Handedness, frameAspect: CGFloat) {
        self.calibration = calibration
        self.handedness = handedness
        self.frameAspect = frameAspect
    }

    /// Camera play copies measured joints in the same fixed coordinate system as contact.
    /// Missing joints hold their last measured position; visible joints continue to move.
    /// Never replace an observed elbow/wrist with an inverse-kinematics golf stance.
    mutating func updateCamera(_ frame: PoseFrame?, club: VirtualClubState?, positionLocked: Bool,
                               swingAngle: Double, at time: Double) -> BodyPose3D {
        if !positionLocked { cameraProjection = nil }
        if positionLocked, let club,
           cameraProjection == nil || cameraProjection?.space != club.space || cameraProjection?.address != club.address {
            cameraProjection = CameraPoseProjection(club: club, handedness: handedness)
        }
        guard let projection = cameraProjection else {
            var next = update(frame, swingAngle: swingAngle, at: time)
            next.clubVisible = club != nil
            pose = next
            return next
        }
        var next = pose
        if let frame {
            for joint in BodyJoint.allCases {
                guard let point = frame.point(joint) else { continue }
                next.joints[joint] = projection.bodyJoint(joint, image: point)
                if joint != .leftWrist && joint != .rightWrist,
                   let depth = frame.depth?.offset(for: joint, at: frame.timestamp) {
                    // Depth is experimental visual augmentation only. The observed
                    // image-plane motion and authoritative grip/club stay unchanged.
                    let target = AvatarAnimations.address[joint].x + max(-0.8, min(0.8, -depth * AvatarSize.height))
                    let dt = Float(max(0, min(0.1, lastTime.map { time - $0 } ?? 1.0 / 30)))
                    let weight = 1 - exp(-dt * 18)
                    next.joints[joint]?.x = pose[joint].x + (target - pose[joint].x) * weight
                }
                lastSeen[joint] = time
            }
        }
        if let club {
            // A hidden wrist in a certified two-handed grip follows the measured grip,
            // while both visible wrists and both measured elbows remain untouched.
            for wrist in [BodyJoint.leftWrist, .rightWrist] where frame?.point(wrist) == nil {
                next.joints[wrist] = projection.project(local: club.grip)
            }
            next.virtualClubHead = projection.project(local: club.head)
            next.virtualClubGrip = projection.project(local: club.grip)
            let direction = next.clubHead - next.clubGrip
            if simd_length(direction) > 0.001 { next.clubDirection = simd_normalize(direction) }
        }
        // Keep a short gap visually continuous, but do not invent a new contact or trajectory.
        next.clubVisible = club != nil || [BodyJoint.leftWrist, .rightWrist].contains {
            lastSeen[$0].map { time - $0 <= gracePeriod } ?? false
        }
        pose = next
        lastTime = time
        return next
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
            let inferredDepth = sqrt(max(0, length * length - simd_length_squared(projected)))
            let depthLimit: Float = (joint == .neck || joint == .nose) ? 0.25 : 0.55
            let depth = depthSign == 0 ? 0 : min(inferredDepth, length * depthLimit) * depthSign
            // Redistribute ambiguous shortening in the image plane, preserving exact bone
            // lengths without stacking invented depth through torso, upper arm and forearm.
            let planarLength = sqrt(max(0, length * length - depth * depth))
            if projectedLength > 0.0001 {
                projected = simd_normalize(projected) * planarLength
            } else {
                let fallback = AvatarAnimations.address[joint] - AvatarAnimations.address[parent]
                let planar = SIMD2(fallback.z, fallback.y)
                projected = simd_length(planar) > 0.0001 ? simd_normalize(planar) * planarLength : SIMD2(0, -planarLength)
            }
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

/// Presentation-only anatomical reconstruction. Raw camera observations and club/contact
/// coordinates remain untouched. Partial faces must not flip the golfer upside down.
struct CameraAvatarPoseFilter {
    private var filters: [BodyJoint: OneEuroFilter] = [:]
    private var lastTime: Double?
    private(set) var pose = BodyPose3D.cameraWaiting

    mutating func update(_ observed: BodyPose3D, frame: PoseFrame?, at time: Double) -> BodyPose3D {
        let dt = max(1.0 / 120, min(0.1, lastTime.map { time - $0 } ?? 1.0 / 30))
        lastTime = time
        // Hold a coherent body when the phone sees only a head/hand or loses the torso.
        guard let frame, [.leftShoulder, .rightShoulder, .leftHip, .rightHip].allSatisfy({
            frame.point($0, minimumConfidence: 0.45) != nil
        }) else {
            pose.clubVisible = false
            return pose
        }
        var target = pose
        for joint in BodyJoint.allCases {
            guard frame.point(joint, minimumConfidence: 0.45) != nil else { continue }
            let value = observed[joint]
            guard value.x.isFinite, value.y.isFinite, value.z.isFinite else { continue }
            var filter = filters[joint] ?? OneEuroFilter(minCutoff: 1.8, beta: 0.15)
            target.joints[joint] = filter.filter(value, dt: dt)
            filters[joint] = filter
        }
        func direction(_ delta: simd_float3, fallback: simd_float3) -> simd_float3 {
            simd_length(delta) > 0.001 ? simd_normalize(delta) : simd_normalize(fallback)
        }
        var next = observed
        let root = simd_float3(0, AvatarSize.hipHeight, target[.root].z)
        next.joints[.root] = root
        let rawUp = direction(target[.neck] - target[.root], fallback: simd_float3(0.2, 1, 0))
        let up = direction(simd_float3(min(0.45, max(-0.2, rawUp.x)), max(0.75, rawUp.y),
                                       min(0.35, max(-0.35, rawUp.z))), fallback: simd_float3(0, 1, 0))
        next.joints[.neck] = root + up * AvatarSize.torso
        let rawSide = target[.rightShoulder] - target[.leftShoulder]
        let side = direction(simd_float3(rawSide.x, min(0.35, max(-0.35, rawSide.y)), rawSide.z),
                             fallback: simd_float3(0, 0, 1))
        let shoulderCenter = next[.neck] - up * 0.15
        next.joints[.leftShoulder] = shoulderCenter - side * AvatarSize.shoulderHalfWidth
        next.joints[.rightShoulder] = shoulderCenter + side * AvatarSize.shoulderHalfWidth
        // Vision's nose is a facial landmark, not a skull orientation sensor. Keep
        // it above the neck with bounded observed lateral tilt; no head inversion.
        let headDelta = target[.nose] - target[.neck]
        let headUp = direction(simd_float3(0.18, max(0.55, headDelta.y), min(0.25, max(-0.25, headDelta.z))),
                               fallback: simd_float3(0.2, 1, 0))
        next.joints[.nose] = next[.neck] + headUp * AvatarSize.neckToHead
        for (hip, knee, ankle, shoulder, elbow, wrist, sign) in [
            (BodyJoint.leftHip, BodyJoint.leftKnee, BodyJoint.leftAnkle, BodyJoint.leftShoulder, BodyJoint.leftElbow, BodyJoint.leftWrist, Float(-1)),
            (.rightHip, .rightKnee, .rightAnkle, .rightShoulder, .rightElbow, .rightWrist, Float(1))
        ] {
            next.joints[hip] = root + side * AvatarSize.hipHalfWidth * sign
            let foot = simd_float3(target[ankle].x, 0.12, target[ankle].z)
            let leg = AvatarAnimations.twoBone(from: next[hip], to: foot, upper: AvatarSize.thigh, lower: AvatarSize.shin,
                bend: target[knee] - (target[hip] + target[ankle]) / 2 + simd_float3(0.1, 0, 0))
            next.joints[knee] = leg.joint
            next.joints[ankle] = leg.end
            let arm = AvatarAnimations.twoBone(from: next[shoulder], to: target[wrist], upper: AvatarSize.upperArm, lower: AvatarSize.forearm,
                bend: target[elbow] - (target[shoulder] + target[wrist]) / 2 + simd_float3(-0.1, 0, sign * 0.05))
            next.joints[elbow] = arm.joint
            next.joints[wrist] = arm.end
        }
        // This layer never moves the authoritative club or ball to repair bad tracking.
        pose = next
        return next
    }
}

/// One-euro filter: smooths jitter when still, follows quickly when moving fast (a downswing).
struct OneEuroFilter {
    var minCutoff: Float = 1.4
    var beta: Float = 0.05
    var derivativeCutoff: Float = 1
    private var value: simd_float3?
    private var derivative = simd_float3.zero

    init(minCutoff: Float = 1.4, beta: Float = 0.05, derivativeCutoff: Float = 1) {
        self.minCutoff = minCutoff
        self.beta = beta
        self.derivativeCutoff = derivativeCutoff
    }

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
