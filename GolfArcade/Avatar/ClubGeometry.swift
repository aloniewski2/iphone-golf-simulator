import CoreGraphics
import simd

/// Where the club points, shared by the 3D avatar and the club drawn on the camera feed so both
/// always agree. At address the club head rests on the ball; through the swing it follows the
/// arms with a wrist hinge that cocks to 90° by 100° of arc.
enum ClubGeometry {
    /// The shaft determines lie, not aim. The head's striking face (-local z)
    /// points down the course (-stance z), including a mirrored left-handed rig.
    static func headOrientation(shaftUp: simd_float3) -> simd_quatf {
        let up = safeNormalize(shaftUp, fallback: simd_float3(0, 1, 0))
        var back = simd_float3(0, 0, 1) - up * simd_dot(simd_float3(0, 0, 1), up)
        if simd_length(back) < 0.001 { back = simd_float3(1, 0, 0) }
        back = simd_normalize(back)
        let right = simd_normalize(simd_cross(up, back))
        return simd_quatf(simd_float3x3(columns: (right, simd_cross(back, right), back)))
    }

    /// Wrist hinge in radians for a swing arc in degrees (positive back, negative through).
    static func hinge(swingAngle: Double) -> Float {
        Float(min(1, abs(swingAngle) / 100) * .pi / 2)
    }

    /// How much the shaft follows the arms instead of pointing at the ball.
    static func armFollow(swingAngle: Double) -> Float {
        smoothstep(5, 30, Float(abs(swingAngle)))
    }

    static func direction3D(hands: simd_float3, shoulders: simd_float3, ball: simd_float3, swingAngle: Double) -> simd_float3 {
        let toBall = safeNormalize(ball - hands, fallback: simd_float3(0.5, -0.85, 0))
        let armLine = safeNormalize(hands - shoulders, fallback: toBall)
        let base = safeNormalize(simd_mix(toBall, armLine, simd_float3(repeating: armFollow(swingAngle: swingAngle))), fallback: toBall)
        // In the front-on swing plane the club cocks "up the arc": for hands out to the trail side
        // (+z) that is up; through the ball it mirrors.
        let side: Float = swingAngle < 0 ? -1 : 1
        let tangent = safeNormalize(simd_cross(armLine, simd_float3(1, 0, 0)) * side, fallback: simd_float3(0, 1, 0))
        let h = hinge(swingAngle: swingAngle)
        return safeNormalize(cos(h) * base + sin(h) * tangent, fallback: base)
    }

    /// The same rule in the camera image. Points are aspect-corrected (x multiplied by the frame
    /// aspect) so angles are true. The mirrored image puts a right-hander's trail side on the right.
    static func direction2D(hands: CGPoint, shoulders: CGPoint, ball: CGPoint, swingAngle: Double, handedness: Handedness) -> CGVector {
        func normalized(_ v: CGVector, fallback: CGVector) -> CGVector {
            let length = hypot(v.dx, v.dy)
            return length > 0.0001 ? CGVector(dx: v.dx / length, dy: v.dy / length) : fallback
        }
        let toBall = normalized(CGVector(dx: ball.x - hands.x, dy: ball.y - hands.y), fallback: CGVector(dx: 0, dy: -1))
        let armLine = normalized(CGVector(dx: hands.x - shoulders.x, dy: hands.y - shoulders.y), fallback: toBall)
        let w = CGFloat(armFollow(swingAngle: swingAngle))
        let base = normalized(CGVector(dx: toBall.dx * (1 - w) + armLine.dx * w, dy: toBall.dy * (1 - w) + armLine.dy * w), fallback: toBall)
        let side: CGFloat = (swingAngle < 0 ? -1 : 1) * (handedness == .right ? 1 : -1)
        let tangent = CGVector(dx: -armLine.dy * side, dy: armLine.dx * side)
        let h = CGFloat(hinge(swingAngle: swingAngle))
        return normalized(CGVector(dx: cos(h) * base.dx + sin(h) * tangent.dx, dy: cos(h) * base.dy + sin(h) * tangent.dy), fallback: base)
    }

    private static func safeNormalize(_ v: simd_float3, fallback: simd_float3) -> simd_float3 {
        simd_length(v) > 0.0001 ? simd_normalize(v) : fallback
    }
}
