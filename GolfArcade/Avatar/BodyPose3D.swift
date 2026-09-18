import CoreGraphics
import simd

/// Mirror the golfer, not the camera feed or the course. Both stances meet the SAME ball.
struct GolferStance {
    let handedness: Handedness
    var mirror: Float { handedness == .right ? 1 : -1 }
    var position: simd_float3 { simd_float3(-AvatarSize.ball.x * mirror, 0, 0) }
    func coursePoint(_ local: simd_float3) -> simd_float3 {
        simd_float3(local.x * mirror, local.y, local.z) + position
    }
}

/// One frozen mapping for the measured body AND gameplay club. This preserves observed
/// image-plane movement, rather than posing the arms with an unrelated golf animation.
/// The camera has no measured depth: this is a calibrated 2.5D projection, not 3D mocap.
struct CameraPoseProjection {
    let space: SwingSpace
    let address: VirtualClubAddress
    let handedness: Handedness

    init(club: VirtualClubState, handedness: Handedness) {
        space = club.space
        address = club.address
        self.handedness = handedness
    }

    func project(local point: CGPoint) -> simd_float3 {
        let shaft = AvatarAnimations.address.handCenter - AvatarSize.ball
        let reach = max(0.01, Float(address.grip.y - address.ball.y))
        let scale = shaft.y / reach
        let side: Float = handedness == .right ? 1 : -1
        return AvatarSize.ball + simd_float3(shaft.x * Float(point.y - address.ball.y) / reach,
            Float(point.y - address.ball.y) * scale, Float(point.x - address.ball.x) * scale * side)
    }

    func project(image point: CGPoint) -> simd_float3 { project(local: space.local(point)) }

    /// Body depth is unobserved: keep a neutral anatomical depth instead of leaning the
    /// entire skeleton along the shaft. The measured image-plane y/z stays exact.
    func bodyJoint(_ joint: BodyJoint, image point: CGPoint) -> simd_float3 {
        var result = project(image: point)
        if joint != .leftWrist && joint != .rightWrist {
            result.x = AvatarAnimations.address[joint].x
        }
        return result
    }
}

/// Stable rig-space proportions; rendered in course yards using `courseScale`.
enum AvatarSize {
    static let courseScale: Float = 0.32
    static let courseBallRadius: CGFloat = 0.0235
    /// Presentation aid only; does not enlarge contact or the cup in the solver.
    static let visibleBallRadius: CGFloat = courseBallRadius * 1.5
    static let hipHeight: Float = 2.4
    static let hipHalfWidth: Float = 0.5
    static let shoulderHalfWidth: Float = 0.75
    static let torso: Float = 2.2
    static let neckToHead: Float = 0.7
    static let upperArm: Float = 1.35
    static let forearm: Float = 1.35
    static let thigh: Float = 1.2
    static let shin: Float = 1.2
    static let clubLength: Float = 2.4
    /// Head to ankle, the same span the body scan measures as `BodyAnchor.height`.
    static let height: Float = 5.05
    /// Where the ball sits relative to the golfer's feet: +x toward the ball.
    static let ball = simd_float3(3.2, 0.10, 0)
}

/// A full-body pose in the golfer's own frame: feet on y = 0 under the hips, `+x` toward the
/// ball, `+y` up, `+z` the golfer's right. Left-handers use the same frame; the rig mirrors it.
struct BodyPose3D: Equatable, Sendable {
    var joints: [BodyJoint: simd_float3]
    /// Unit vector from the hands to the club head.
    var clubDirection: simd_float3
    /// The club lies on the ground instead of in the hands.
    var clubDropped = false
    /// Camera setup has no authoritative club yet; do not display a misleading active shaft.
    var clubVisible = true
    /// Exact projection of the gameplay clubhead for live camera swings and their recordings.
    var virtualClubHead: simd_float3?
    /// Same confidence-weighted grip used by contact, without overwriting measured wrists.
    var virtualClubGrip: simd_float3?
    var provenance: [BodyJoint: JointProvenance] = [:]
    /// Whole-body rotation about the feet, for knockdowns.
    var lean = simd_quatf(angle: 0, axis: simd_float3(0, 1, 0))

    subscript(_ joint: BodyJoint) -> simd_float3 { joints[joint] ?? .zero }

    var handCenter: simd_float3 { (self[.leftWrist] + self[.rightWrist]) / 2 }
    var clubGrip: simd_float3 { virtualClubGrip ?? handCenter }
    var shoulderCenter: simd_float3 { (self[.leftShoulder] + self[.rightShoulder]) / 2 }
    var clubHead: simd_float3 { virtualClubHead ?? (clubGrip + clubDirection * AvatarSize.clubLength) }

    /// Reflect joint positions and swap anatomy, never use negative mesh scale/winding.
    var anatomicallyMirrored: BodyPose3D {
        var result = self
        func reflect(_ p: simd_float3) -> simd_float3 { simd_float3(-p.x,p.y,p.z) }
        for joint in BodyJoint.allCases { result.joints[joint] = reflect(self[joint]) }
        let pairs: [(BodyJoint,BodyJoint)] = [(.leftShoulder,.rightShoulder),(.leftElbow,.rightElbow),
                             (.leftWrist,.rightWrist),(.leftHip,.rightHip),(.leftKnee,.rightKnee),(.leftAnkle,.rightAnkle)]
        for (left,right) in pairs {
            result.joints[left] = reflect(self[right]); result.joints[right] = reflect(self[left])
            result.provenance[left] = provenance[right]; result.provenance[right] = provenance[left]
        }
        // Conjugate the whole-body rotation by the reflection; keep a proper rotation.
        var reflection=matrix_identity_float3x3
        reflection.columns.0.x = -1
        result.lean=simd_quatf(reflection * simd_float3x3(lean) * reflection)
        result.virtualClubGrip = reflect(clubGrip)
        result.virtualClubHead = reflect(clubHead)
        result.clubDirection = reflect(clubDirection)
        return result
    }

    static var cameraWaiting: BodyPose3D {
        var pose = AvatarAnimations.address
        pose.clubVisible = false
        return pose
    }

    mutating func apply(_ club: VirtualClubState, handedness: Handedness) {
        let addressGrip = AvatarAnimations.address.handCenter
        let shaft = addressGrip - AvatarSize.ball
        let reach = max(0.01, Float(club.address.grip.y - club.address.ball.y))
        let scale = simd_length(shaft) / reach
        let up = simd_normalize(shaft)
        let side: Float = handedness == .right ? 1 : -1
        func project(_ point: CGPoint) -> simd_float3 {
            AvatarSize.ball + up * Float(point.y - club.address.ball.y) * scale
                + simd_float3(0, 0, Float(point.x - club.address.ball.x) * scale * side)
        }
        let grip = project(club.grip)
        // The clubhead remains the exact projection of gameplay contact. Only the visual arm
        // reach is constrained; never stretch the forearms to force a noisy hand coordinate.
        for (shoulder, elbow, wrist, side) in [
            (BodyJoint.leftShoulder, BodyJoint.leftElbow, BodyJoint.leftWrist, Float(-1)),
            (.rightShoulder, .rightElbow, .rightWrist, Float(1))
        ] {
            let arm = AvatarAnimations.twoBone(from: self[shoulder], to: grip + simd_float3(0, 0, side * 0.06),
                upper: AvatarSize.upperArm, lower: AvatarSize.forearm, bend: simd_float3(-0.7, -0.3, side))
            joints[elbow] = arm.joint
            joints[wrist] = arm.end
        }
        virtualClubHead = project(club.head)
        let direction = clubHead - handCenter
        if simd_length(direction) > 0.001 { clubDirection = simd_normalize(direction) }
    }

    static func lerp(_ a: BodyPose3D, _ b: BodyPose3D, _ t: Float) -> BodyPose3D {
        let t = min(max(t, 0), 1)
        var joints: [BodyJoint: simd_float3] = [:]
        for joint in BodyJoint.allCases {
            joints[joint] = simd_mix(a[joint], b[joint], simd_float3(repeating: t))
        }
        let club = simd_mix(a.clubDirection, b.clubDirection, simd_float3(repeating: t))
        return BodyPose3D(
            joints: joints,
            clubDirection: simd_length(club) > 0.001 ? simd_normalize(club) : b.clubDirection,
            clubDropped: t < 0.5 ? a.clubDropped : b.clubDropped,
            clubVisible: t < 0.5 ? a.clubVisible : b.clubVisible,
            virtualClubHead: a.virtualClubHead != nil || b.virtualClubHead != nil ? simd_mix(a.clubHead, b.clubHead, simd_float3(repeating: t)) : nil,
            virtualClubGrip: a.virtualClubGrip != nil || b.virtualClubGrip != nil ? simd_mix(a.clubGrip, b.clubGrip, simd_float3(repeating: t)) : nil,
            provenance: t < 0.5 ? a.provenance : b.provenance,
            lean: simd_slerp(a.lean, b.lean, t)
        )
    }
}

func smoothstep(_ edge0: Float, _ edge1: Float, _ x: Float) -> Float {
    let t = min(max((x - edge0) / (edge1 - edge0), 0), 1)
    return t * t * (3 - 2 * t)
}
