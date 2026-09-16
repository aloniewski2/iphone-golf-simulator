import simd

/// Avatar proportions, in scene yards. The avatar is arcade-sized to match the oversized ball.
enum AvatarSize {
    static let hipHeight: Float = 2.4
    static let hipHalfWidth: Float = 0.5
    static let shoulderHalfWidth: Float = 0.75
    static let torso: Float = 2.2
    static let neckToHead: Float = 0.7
    static let upperArm: Float = 1.35
    static let forearm: Float = 1.35
    static let thigh: Float = 1.2
    static let shin: Float = 1.2
    static let clubLength: Float = 2.7
    /// Head to ankle, the same span the body scan measures as `BodyAnchor.height`.
    static let height: Float = 5.05
    /// Where the ball sits relative to the golfer's feet: +x toward the ball.
    static let ball = simd_float3(3.2, 0.35, 0)
}

/// A full-body pose in the golfer's own frame: feet on y = 0 under the hips, `+x` toward the
/// ball, `+y` up, `+z` the golfer's right. Left-handers use the same frame; the rig mirrors it.
struct BodyPose3D: Equatable, Sendable {
    var joints: [BodyJoint: simd_float3]
    /// Unit vector from the hands to the club head.
    var clubDirection: simd_float3
    /// The club lies on the ground instead of in the hands.
    var clubDropped = false
    /// Whole-body rotation about the feet, for knockdowns.
    var lean = simd_quatf(angle: 0, axis: simd_float3(0, 1, 0))

    subscript(_ joint: BodyJoint) -> simd_float3 { joints[joint] ?? .zero }

    var handCenter: simd_float3 { (self[.leftWrist] + self[.rightWrist]) / 2 }
    var shoulderCenter: simd_float3 { (self[.leftShoulder] + self[.rightShoulder]) / 2 }
    var clubHead: simd_float3 { handCenter + clubDirection * AvatarSize.clubLength }

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
            lean: simd_slerp(a.lean, b.lean, t)
        )
    }
}

func smoothstep(_ edge0: Float, _ edge1: Float, _ x: Float) -> Float {
    let t = min(max((x - edge0) / (edge1 - edge0), 0), 1)
    return t * t * (3 - 2 * t)
}
