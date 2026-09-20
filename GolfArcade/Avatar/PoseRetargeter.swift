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
    private(set) var torsoTurn = TorsoTurnEstimator()

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
            if let frame { torsoTurn.lock(on: frame, aspect: frameAspect) }
        }
        guard let projection = cameraProjection else {
            torsoTurn.reset()
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
            // The turn is the one thing the image plane cannot show: the shoulders and hips
            // rotate about the spine, so their ends move toward and away from the phone.
            let dt = max(1.0 / 120, min(0.1, lastTime.map { time - $0 } ?? 1.0 / 30))
            torsoTurn.update(frame, aspect: frameAspect, swingAngle: swingAngle, handedness: handedness, dt: dt)
            Self.applyTurn(torsoTurn, handedness: handedness, to: &next)
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

extension PoseRetargeter {
    /// Places the ends of the shoulder and hip lines nearer or farther from the ball by the
    /// estimated turn. Image-plane positions stay exactly as measured.
    static func applyTurn(_ turn: TorsoTurnEstimator, handedness: Handedness, to pose: inout BodyPose3D) {
        let address = AvatarAnimations.address
        // The camera projection mirrors a left-hander, so their right side sits on -z.
        let rightIsPositive: Float = handedness == .right ? 1 : -1
        for (left, right, yaw, halfWidth) in [
            (BodyJoint.leftShoulder, BodyJoint.rightShoulder, turn.shoulderYaw, AvatarSize.shoulderHalfWidth),
            (.leftHip, .rightHip, turn.hipYaw, AvatarSize.hipHalfWidth)
        ] {
            let centerX = (address[left].x + address[right].x) / 2
            let reach = halfWidth * sin(yaw * .pi / 180)
            pose.joints[right]?.x = centerX + reach * rightIsPositive
            pose.joints[left]?.x = centerX - reach * rightIsPositive
        }
    }
}

/// How far the shoulders and hips have turned about the spine, read from how much their lines
/// foreshorten in the image against the widths locked at address. Magnitude comes from the
/// image; the sign comes from the 3D body pose while it is fresh (setting up) and otherwise
/// from the swing direction: back is one way, through is the other. Once a line's ends cross
/// in the image the turn has passed 90°, as it does at a full finish.
///
/// Yaw is in the avatar's frame, in degrees: positive turns the `+z` end toward the ball.
struct TorsoTurnEstimator {
    private(set) var shoulderYaw: Float = 0
    private(set) var hipYaw: Float = 0
    private var squareShoulderWidth: CGFloat?
    private var squareHipWidth: CGFloat?
    private var shoulderSense: CGFloat = 1
    private var hipSense: CGFloat = 1
    private var lastSign: Float = 0
    private var shoulderFilter = OneEuroFilter(minCutoff: 1.6, beta: 0.2)
    private var hipFilter = OneEuroFilter(minCutoff: 1.4, beta: 0.15)

    var isLocked: Bool { squareShoulderWidth != nil }

    /// The widths seen at the moment the ball locks are the reference: turned by the stance
    /// line if the 3D pose measured one, square otherwise.
    mutating func lock(on frame: PoseFrame, aspect: CGFloat) {
        reset()
        let stance = frame.orientation.flatMap { $0.isFresh(at: frame.timestamp) ? $0 : nil }
        if let width = Self.width(frame, .leftShoulder, .rightShoulder, aspect: aspect) {
            squareShoulderWidth = width / CGFloat(max(0.3, cos((stance?.shoulderYaw ?? 0) * .pi / 180)))
            shoulderSense = Self.sense(frame, .leftShoulder, .rightShoulder)
        }
        if let width = Self.width(frame, .leftHip, .rightHip, aspect: aspect) {
            squareHipWidth = width / CGFloat(max(0.3, cos((stance?.hipYaw ?? 0) * .pi / 180)))
            hipSense = Self.sense(frame, .leftHip, .rightHip)
        }
    }

    mutating func reset() {
        shoulderYaw = 0
        hipYaw = 0
        squareShoulderWidth = nil
        squareHipWidth = nil
        lastSign = 0
        shoulderFilter = OneEuroFilter(minCutoff: 1.6, beta: 0.2)
        hipFilter = OneEuroFilter(minCutoff: 1.4, beta: 0.15)
    }

    mutating func update(_ frame: PoseFrame, aspect: CGFloat, swingAngle: Double, handedness: Handedness, dt: Double) {
        guard let squareShoulderWidth else { return }
        let side: Float = handedness == .right ? 1 : -1
        // Backswing turns the lead shoulder toward the ball, which is a negative yaw in the
        // avatar's (already mirrored) frame for either handedness.
        if swingAngle > 10 { lastSign = -1 } else if swingAngle < -10 { lastSign = 1 }
        let fresh = frame.orientation.flatMap { $0.isFresh(at: frame.timestamp) ? $0 : nil }
        func yaw(_ measured: CGFloat?, square: CGFloat, sense: CGFloat, threeD: Double?) -> Float? {
            if let threeD, abs(swingAngle) < 10 { return Float(threeD) * side }
            guard let measured, square > 0.001 else { return nil }
            let ratio = min(1, measured / square)
            // acos is steep near square, so a couple of percent of tracker jitter reads as ten
            // degrees; take the noise floor off rather than let a square stance flutter.
            var degrees = max(0, Float(acos(Double(ratio)) * 180 / .pi) - 3)
            if sense < 0 { degrees = 180 - degrees } // the ends have crossed: past a right angle
            let sign = lastSign != 0 ? lastSign : (fresh.map { Float($0.shoulderYaw) * side >= 0 ? 1 : -1 } ?? 1)
            return degrees * sign
        }
        let shoulderWidth = Self.width(frame, .leftShoulder, .rightShoulder, aspect: aspect)
        let shoulderCross = Self.sense(frame, .leftShoulder, .rightShoulder) * shoulderSense
        if let raw = yaw(shoulderWidth, square: squareShoulderWidth, sense: shoulderCross, threeD: fresh?.shoulderYaw) {
            shoulderYaw = shoulderFilter.filter(simd_float3(raw, 0, 0), dt: dt).x
        }
        let hipWidth = Self.width(frame, .leftHip, .rightHip, aspect: aspect)
        let hipCross = Self.sense(frame, .leftHip, .rightHip) * hipSense
        // Without a hip reference the hips follow the shoulders at the usual half turn.
        guard let squareHipWidth, var raw = yaw(hipWidth, square: squareHipWidth, sense: hipCross, threeD: fresh?.hipYaw) else {
            hipYaw = shoulderYaw * 0.5
            return
        }
        // Hips never out-turn the shoulders going back, and lead them by a bounded amount through.
        let limit = abs(shoulderYaw) + 25
        raw = max(-limit, min(limit, raw))
        hipYaw = hipFilter.filter(simd_float3(raw, 0, 0), dt: dt).x
    }

    private static func width(_ frame: PoseFrame, _ left: BodyJoint, _ right: BodyJoint, aspect: CGFloat) -> CGFloat? {
        guard let a = frame.point(left, minimumConfidence: 0.45), let b = frame.point(right, minimumConfidence: 0.45) else { return nil }
        return hypot((b.x - a.x) * aspect, b.y - a.y)
    }

    /// +1 when the right end is to the image right of the left end, -1 when they have crossed.
    private static func sense(_ frame: PoseFrame, _ left: BodyJoint, _ right: BodyJoint) -> CGFloat {
        guard let a = frame.point(left, minimumConfidence: 0.45), let b = frame.point(right, minimumConfidence: 0.45) else { return 1 }
        return b.x >= a.x ? 1 : -1
    }
}

/// Presentation-only anatomical reconstruction. Raw camera observations and club/contact
/// coordinates remain untouched. Partial faces must not flip the golfer upside down.
struct CameraAvatarPoseFilter {
    private var filters: [BodyJoint: OneEuroFilter] = [:]
    private var lastTime: Double?
    private var lastGoodTime: Double?
    private(set) var pose = BodyPose3D.cameraWaiting

    mutating func update(_ observed: BodyPose3D, frame: PoseFrame?, at time: Double) -> BodyPose3D {
        let dt = max(1.0 / 120, min(0.1, lastTime.map { time - $0 } ?? 1.0 / 30))
        lastTime = time
        // Hold a coherent body when the phone sees only a head/hand or loses the torso.
        let torsoJoints:[BodyJoint]=[.leftShoulder,.rightShoulder,.leftHip,.rightHip]
        let visible=Set(torsoJoints.filter { frame?.point($0,minimumConfidence:0.45) != nil })
        let fullTorso=visible.count == 4
        // A turning golfer commonly occludes the far shoulder/hip. Previously losing ONE
        // torso landmark froze every wrist/club sample, even with a clearly observed grip.
        // Bootstrap from a complete torso, then retain the missing side while following
        // a supported partial body. A lone face/hand still cannot drive the rig.
        let supportedPartial=lastGoodTime != nil && visible.count >= 2 &&
            !visible.isDisjoint(with:[.leftShoulder,.rightShoulder]) &&
            !visible.isDisjoint(with:[.leftHip,.rightHip]) &&
            [BodyJoint.leftWrist,.rightWrist].contains { frame?.point($0,minimumConfidence:0.45) != nil }
        guard let frame, fullTorso || supportedPartial else {
            // A brief pre/post-impact gap holds presentation only. No predicted sample
            // is sent to the detector, and a longer loss hides the club.
            pose.clubVisible = pose.clubVisible && lastGoodTime.map { time - $0 <= 0.18 } == true
            for joint in BodyJoint.allCases { pose.provenance[joint] = pose.clubVisible ? .held : .unavailable }
            return pose
        }
        lastGoodTime = time
        var target = pose
        for joint in BodyJoint.allCases {
            guard frame.point(joint, minimumConfidence: 0.45) != nil else { continue }
            let value = observed[joint]
            guard value.x.isFinite, value.y.isFinite, value.z.isFinite else { continue }
            var filter = filters[joint] ?? OneEuroFilter(minCutoff: 1.1, beta: 0.4)
            target.joints[joint] = filter.filter(value, dt: dt)
            filters[joint] = filter
        }
        func direction(_ delta: simd_float3, fallback: simd_float3) -> simd_float3 {
            simd_length(delta) > 0.001 ? simd_normalize(delta) : simd_normalize(fallback)
        }
        var next = observed
        for joint in BodyJoint.allCases {
            next.provenance[joint] = frame.point(joint,minimumConfidence:0.45) == nil ? .inferred : .observed
        }
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
        let rawHips = target[.rightHip] - target[.leftHip]
        let hipSide = direction(simd_float3(rawHips.x, 0, rawHips.z), fallback: simd_float3(side.x, 0, side.z))
        for (hip, knee, ankle, shoulder, elbow, wrist, sign) in [
            (BodyJoint.leftHip, BodyJoint.leftKnee, BodyJoint.leftAnkle, BodyJoint.leftShoulder, BodyJoint.leftElbow, BodyJoint.leftWrist, Float(-1)),
            (.rightHip, .rightKnee, .rightAnkle, .rightShoulder, .rightElbow, .rightWrist, Float(1))
        ] {
            next.joints[hip] = root + hipSide * AvatarSize.hipHalfWidth * sign
            let foot = simd_float3(target[ankle].x, max(0.12,min(0.55,target[ankle].y)), target[ankle].z)
            let leg = AvatarAnimations.twoBone(from: next[hip], to: foot, upper: AvatarSize.thigh, lower: AvatarSize.shin,
                bend: target[knee] - (target[hip] + target[ankle]) / 2 + simd_float3(0.1, 0, 0))
            next.joints[knee] = leg.joint
            next.joints[ankle] = leg.end
            // Hands are solved to the same grip as the rendered/collision club; only
            // uncertain arm geometry is reconstructed. Club endpoints remain untouched.
            let hand = observed.virtualClubGrip.map { $0 + observed.clubDirection * sign * 0.065 } ?? target[wrist]
            let arm = AvatarAnimations.twoBone(from: next[shoulder], to: hand, upper: AvatarSize.upperArm, lower: AvatarSize.forearm,
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
