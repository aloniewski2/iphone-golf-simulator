import simd

/// Procedural poses: the canned swing, bystander idling, post-shot reactions and knockdowns.
/// Pure functions of time so they are easy to test and to blend with the live mimic.
enum AvatarAnimations {
    // Upper-body frame: origin at the hips, same axes as the pose.
    private static let pivot = simd_float3(0.55, 1.8, 0)
    private static let shoulders = [simd_float3(0.7, 2.0, -AvatarSize.shoulderHalfWidth), simd_float3(0.7, 2.0, AvatarSize.shoulderHalfWidth)]
    private static let neck = simd_float3(0.75, 2.15, 0)
    private static let head = simd_float3(0.95, 2.8, 0)
    private static let addressHands = simd_float3(1.9, -0.3, 0.1)
    private static let neutralGrip = UpperBody().pose(
        left: addressHands + simd_float3(0.12, -0.08, 0),
        right: addressHands + simd_float3(-0.12, 0.08, 0)).handCenter

    static let address = swingArc(degrees: 0)
    static let finish = swingArc(degrees: -150)

    /// Original phase-shaped fallback for touch/phone input. Camera poses retain their
    /// measured grip and authoritative club; this never substitutes for camera contact.
    static func swingArc(degrees: Double, club: GolfClub = .driver) -> BodyPose3D {
        if club == .putter { return puttingArc(degrees: degrees) }
        let degrees = max(-150, min(150, degrees.isFinite ? degrees : 0))
        let radians = Float(degrees * .pi / 180)
        let toAddress = addressHands - pivot
        let radius = simd_length(toAddress)
        let d0 = toAddress / radius
        var d1 = simd_float3(-0.25, 0.2, 1)
        d1 = simd_normalize(d1 - simd_dot(d1, d0) * d0)
        let hands = swingHands(degrees: degrees)
        let progress = Float(abs(degrees) / 150)
        let release = degrees < 0 ? smoothstep(0.12, 1, progress) : 0
        var upper = UpperBody(twist: -radians * (degrees < 0 ? 0.40 : 0.30))
        upper.hipTurn = -radians * (degrees < 0 ? 0.27 : 0.12)
        upper.trailHeel = release * 0.22
        upper.offset.z = -release * 0.16
        var pose = upper.pose(left: hands + simd_float3(0.12, -0.08, 0), right: hands + simd_float3(-0.12, 0.08, 0))
        let armLine = simd_normalize(hands - pivot)
        let tangent = simd_normalize(-sin(radians) * d0 + cos(radians) * d1) * (degrees < 0 ? -1 : 1)
        let hingeLimit: Double = club == .wedge ? 0.65 : club == .iron ? 0.85 : 1
        let hinge = Float(min(1, abs(degrees) / 100) * .pi / 2 * hingeLimit)
        // Address the same small ground ball as the camera rig instead of extending the
        // shaft along the forearms and burying its head below the turf.
        let addressShaft = simd_normalize(AvatarSize.ball - neutralGrip)
        let shaftArc = simd_quatf(from: d0, to: armLine).act(addressShaft)
        pose.clubDirection = upper.rotate(simd_normalize(cos(hinge) * shaftArc + sin(hinge) * tangent))
        pose.virtualClubHead = pose.clubGrip + pose.clubDirection * simd_length(AvatarSize.ball - neutralGrip)
        return pose
    }

    /// Catmull-Rom hand targets: width in the takeaway, folded top, extension then
    /// high finish. Unlike a single chest circle, ascent and release have distinct shapes.
    static func swingHands(degrees: Double) -> simd_float3 {
        let keys: [simd_float3] = degrees >= 0
            ? [addressHands, simd_float3(1.85, 0.3, 1.0), simd_float3(0.65, 1.8, 1.55), simd_float3(0.3, 2.55, 1.1)]
            : [addressHands, simd_float3(1.85, 0.35, -1.05), simd_float3(0.65, 1.85, -1.55), simd_float3(0.15, 2.6, -1.05)]
        let u = Float(min(150, abs(degrees.isFinite ? degrees : 0)) / 50)
        let i = min(2, Int(u)), t = u - Float(i)
        let a = keys[max(0, i - 1)], b = keys[i], c = keys[i + 1], d = keys[min(3, i + 2)]
        return 0.5 * ((2 * b) + (c - a) * t + (2 * a - 5 * b + 4 * c - d) * t * t
            + (-a + 3 * b - 3 * c + d) * t * t * t)
    }

    /// Quiet lower body, connected shoulder/arm pendulum and very little wrist release.
    /// Presentation only: live camera contact still uses the measured authoritative club.
    static func puttingArc(degrees: Double) -> BodyPose3D {
        var pose = address
        let angle = Float(max(-150, min(150, degrees)) / 150) * 0.19
        let axis = simd_float3(1, 0, 0)
        let rotation = simd_quatf(angle: -angle, axis: axis)
        let pivot = pose.shoulderCenter
        for joint in [BodyJoint.leftShoulder, .rightShoulder, .leftElbow, .rightElbow, .leftWrist, .rightWrist] {
            pose.joints[joint] = pivot + rotation.act(pose[joint] - pivot)
        }
        pose.virtualClubGrip = pivot + rotation.act(address.clubGrip - pivot)
        pose.virtualClubHead = pivot + rotation.act(AvatarSize.ball - pivot)
        pose.clubDirection = simd_normalize(pose.clubHead - pose.clubGrip)
        return pose
    }

    /// Standing relaxed and leaning on the club, breathing and shifting weight.
    static func idle(time: Double, seed: Double = 0) -> BodyPose3D {
        let t = Float(time + seed * 1.7)
        let breathe = sin(t * 1.9) * 0.04
        let shift = sin(t * 0.6 + Float(seed)) * 0.12
        let glance = sin(t * 0.37 + Float(seed) * 2) > 0.85 ? Float(0.25) : 0
        var upper = UpperBody(twist: glance * 0.4, lean: -0.05)
        upper.offset = simd_float3(0, breathe, shift)
        var pose = upper.pose(left: simd_float3(0.3, -0.62, -0.95), right: simd_float3(0.55, -0.55, 0.9))
        pose.clubDirection = simd_normalize(simd_float3(0.35, -1, 0.1))
        return pose
    }

    enum Reaction: String, CaseIterable, Sendable {
        case pure, solid, meh, bad, disaster, holed
    }

    /// Seconds a reaction plays before the avatar returns to copying the player.
    static let reactionLength = 2.6

    /// `time` is seconds since the reaction started (the follow-through has finished by then).
    static func reaction(_ kind: Reaction, time: Double) -> BodyPose3D {
        let t = Float(time)
        switch kind {
        case .pure:
            var pose = finish
            if t > 0.7, t < 1.5 {
                // Club twirl in the hands.
                let spin = (t - 0.7) / 0.8 * 2 * .pi
                let axis = simd_normalize(pose.handCenter - pose.shoulderCenter)
                pose.clubDirection = simd_quatf(angle: spin, axis: axis).act(pose.clubDirection)
            }
            return pose
        case .solid:
            var pose = finish
            let nod = t > 0.4 && t < 1.2 ? sin((t - 0.4) / 0.8 * 2 * .pi) * 0.14 : 0
            pose.joints[.nose]? += simd_float3(0.05, -abs(nod), 0)
            return pose
        case .meh:
            let drop = smoothstep(0, 0.6, t)
            let upper = UpperBody(twist: simd_mix(-0.9, 0, drop))
            let lowHands = simd_float3(1.0, -0.25, 0)
            let from = finish
            var pose = upper.pose(
                left: simd_mix(upper.unrotate(from[.leftWrist]), lowHands + simd_float3(0, 0, -0.18), simd_float3(repeating: drop)),
                right: simd_mix(upper.unrotate(from[.rightWrist]), lowHands + simd_float3(0, 0, 0.18), simd_float3(repeating: drop))
            )
            pose.clubDirection = simd_normalize(simd_mix(from.clubDirection, simd_float3(0.45, -1, 0.3), simd_float3(repeating: drop)))
            let shake = t > 0.6 && t < 1.6 ? sin((t - 0.6) * 16) * 0.16 : 0
            pose.joints[.nose]? += simd_float3(0, 0, shake)
            return pose
        case .bad:
            let slump = smoothstep(0, 0.5, t)
            let upper = UpperBody(twist: 0, lean: 0.28 * slump, crouch: 0.15 * slump)
            var pose = upper.pose(left: simd_float3(0.15, 0.15, -0.9), right: simd_float3(0.15, 0.15, 0.9))
            pose.joints[.nose]? += simd_float3(0.2, -0.25 * slump, 0)
            pose.clubDropped = t > 0.25
            pose.clubDirection = simd_float3(0, -1, 0)
            return pose
        case .disaster:
            let stagger = smoothstep(0, 0.35, t)
            var upper = UpperBody(twist: 0, lean: -0.2 * stagger)
            upper.offset = simd_float3(-0.6 * stagger, 0, 0)
            let wobble = sin(t * 5) * 0.08
            var pose = upper.pose(left: simd_float3(0.75, 3.0 + wobble, -0.35), right: simd_float3(0.75, 3.0 - wobble, 0.35))
            pose.clubDropped = true
            pose.clubDirection = simd_float3(0, -1, 0)
            return pose
        case .holed:
            let jump = t < 1.05 ? max(0, sin(t * 6)) * 0.7 : 0
            var upper = UpperBody(twist: 0, lean: -0.15)
            upper.offset = simd_float3(0, jump, 0)
            var pose = upper.pose(left: simd_float3(0.5, 4.3, -1.0), right: simd_float3(0.5, 4.3, 1.0))
            pose.clubDirection = simd_normalize(simd_float3(0.2, 1, 0.4))
            return pose
        }
    }

    static let knockdownLength = 2.2

    /// Falls away from `push` (a horizontal direction in the avatar's frame), lies still with a
    /// small bounce, then gets back up.
    static func knockdown(time: Double, push: simd_float3, seed: Double = 0) -> BodyPose3D {
        var pose = idle(time: time, seed: seed)
        let t = Float(time)
        let fallen: Float = 1.45
        let angle: Float
        if t < 0.35 {
            let u = t / 0.35
            angle = u * u * fallen
        } else if t < 1.35 {
            angle = fallen - sin(min((t - 0.35) / 0.25, 1) * .pi) * 0.12
        } else {
            angle = fallen * (1 - smoothstep(1.35, Float(knockdownLength), t))
        }
        var flat = simd_float3(push.x, 0, push.z)
        if simd_length(flat) < 0.001 { flat = simd_float3(-1, 0, 0) }
        let axis = simd_normalize(simd_cross(simd_float3(0, 1, 0), simd_normalize(flat)))
        pose.lean = simd_quatf(angle: angle, axis: axis)
        pose.clubDropped = t < Float(knockdownLength)
        return pose
    }

    /// Builds a pose from an upper body placed by twist, forward lean and crouch, with the hands
    /// reached by two-bone IK so arm lengths never change.
    struct UpperBody {
        var twist: Float = 0
        var lean: Float = 0
        var crouch: Float = 0
        var hipTurn: Float = 0
        var trailHeel: Float = 0
        var offset = simd_float3.zero

        private var rotation: simd_quatf {
            simd_quatf(angle: twist, axis: simd_float3(0, 1, 0)) * simd_quatf(angle: -lean, axis: simd_float3(0, 0, 1))
        }

        func rotate(_ v: simd_float3) -> simd_float3 { rotation.act(v) }
        func unrotate(_ world: simd_float3) -> simd_float3 {
            rotation.inverse.act(world - simd_float3(0, AvatarSize.hipHeight - crouch, 0) - offset)
        }
        func place(_ v: simd_float3) -> simd_float3 { rotation.act(v) + simd_float3(0, AvatarSize.hipHeight - crouch, 0) + offset }

        /// `left` and `right` are hand targets in the upper-body frame.
        func pose(left: simd_float3, right: simd_float3) -> BodyPose3D {
            var joints: [BodyJoint: simd_float3] = [:]
            let root = simd_float3(0, AvatarSize.hipHeight - crouch, 0) + offset
            joints[.root] = root
            let hips = simd_quatf(angle: hipTurn, axis: simd_float3(0, 1, 0))
            joints[.leftHip] = root + hips.act(simd_float3(0, 0, -AvatarSize.hipHalfWidth))
            joints[.rightHip] = root + hips.act(simd_float3(0, 0, AvatarSize.hipHalfWidth))
            for (hip, knee, ankle, z) in [(BodyJoint.leftHip, BodyJoint.leftKnee, BodyJoint.leftAnkle, Float(-0.6)), (.rightHip, .rightKnee, .rightAnkle, 0.6)] {
                let foot = simd_float3(0.05, 0.12 + (ankle == .rightAnkle ? trailHeel : 0), z)
                let (kneePoint, _) = AvatarAnimations.twoBone(from: joints[hip]!, to: foot, upper: AvatarSize.thigh, lower: AvatarSize.shin, bend: simd_float3(1, 0, 0))
                joints[knee] = kneePoint
                joints[ankle] = foot
            }
            joints[.neck] = place(neck)
            joints[.nose] = place(head)
            let hands = [left, right]
            for side in 0..<2 {
                let shoulder = place(shoulders[side])
                let (elbow, wrist) = AvatarAnimations.twoBone(
                    from: shoulder, to: place(hands[side]),
                    upper: AvatarSize.upperArm, lower: AvatarSize.forearm,
                    bend: rotate(simd_float3(-0.7, -0.3, side == 0 ? -1 : 1))
                )
                joints[side == 0 ? .leftShoulder : .rightShoulder] = shoulder
                joints[side == 0 ? .leftElbow : .rightElbow] = elbow
                joints[side == 0 ? .leftWrist : .rightWrist] = wrist
            }
            let handCenter = (joints[.leftWrist]! + joints[.rightWrist]!) / 2
            let toBall = simd_normalize(AvatarSize.ball - handCenter)
            return BodyPose3D(joints: joints, clubDirection: toBall)
        }
    }

    /// Two-bone IK: the middle joint bends toward `bend`; the chain never stretches.
    static func twoBone(from start: simd_float3, to target: simd_float3, upper: Float, lower: Float, bend: simd_float3) -> (joint: simd_float3, end: simd_float3) {
        var reach = target - start
        let length = simd_length(reach)
        guard length > 0.0001 else { return (start + simd_float3(0, -upper, 0), start + simd_float3(0, -upper - lower, 0)) }
        let distance = min(length, upper + lower - 0.02)
        reach = reach / length * distance
        let end = start + reach
        let axis = reach / distance
        let a = (upper * upper - lower * lower + distance * distance) / (2 * distance)
        let height = sqrt(max(0, upper * upper - a * a))
        var direction = bend - simd_dot(bend, axis) * axis
        if simd_length(direction) < 0.0001 {
            let fallback = abs(axis.z) < 0.9 ? simd_float3(0, 0, 1) : simd_float3(1, 0, 0)
            direction = fallback - simd_dot(fallback, axis) * axis
        }
        let joint = start + axis * a + simd_normalize(direction) * height
        return (joint, end)
    }
}
