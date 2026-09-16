import SceneKit
import UIKit

/// A Mii-style golfer built from simple shapes, posed entirely from a `BodyPose3D`. Nodes are
/// created once; `apply` only moves them, so it is cheap to call every frame.
@MainActor
final class AvatarRig {
    /// Placement, facing and mirroring. The body's own frame is the pose frame.
    let node = SCNNode()
    private let body = SCNNode()
    private var bones: [(BodyJoint, BodyJoint, SCNNode)] = []
    private let head = SCNNode()
    private let cap = SCNNode()
    private let pelvis = SCNNode()
    private var hands: [SCNNode] = []
    private var feet: [SCNNode] = []
    private let shaft = SCNNode()
    private let clubHead = SCNNode()

    init(shirt: UIColor) {
        let trousers = UIColor(red: 0.16, green: 0.2, blue: 0.3, alpha: 1)
        let skin = UIColor(red: 0.93, green: 0.78, blue: 0.62, alpha: 1)
        let cream = UIColor(red: 0.96, green: 0.96, blue: 0.86, alpha: 1)
        node.addChildNode(body)

        func material(_ geometry: SCNGeometry, _ color: UIColor) -> SCNGeometry {
            geometry.firstMaterial?.diffuse.contents = color
            geometry.firstMaterial?.isDoubleSided = true
            return geometry
        }
        func bone(_ from: BodyJoint, _ to: BodyJoint, radius: CGFloat, color: UIColor) {
            let bone = SCNNode(geometry: material(SCNCylinder(radius: radius, height: 1), color))
            bone.pivot = SCNMatrix4MakeTranslation(0, -0.5, 0)
            body.addChildNode(bone)
            bones.append((from, to, bone))
        }
        bone(.leftHip, .leftKnee, radius: 0.27, color: trousers)
        bone(.leftKnee, .leftAnkle, radius: 0.24, color: trousers)
        bone(.rightHip, .rightKnee, radius: 0.27, color: trousers)
        bone(.rightKnee, .rightAnkle, radius: 0.24, color: trousers)
        bone(.root, .neck, radius: 0.55, color: shirt)
        bone(.leftShoulder, .rightShoulder, radius: 0.32, color: shirt)
        bone(.neck, .nose, radius: 0.18, color: skin)
        bone(.leftShoulder, .leftElbow, radius: 0.17, color: shirt)
        bone(.leftElbow, .leftWrist, radius: 0.15, color: skin)
        bone(.rightShoulder, .rightElbow, radius: 0.17, color: shirt)
        bone(.rightElbow, .rightWrist, radius: 0.15, color: skin)

        pelvis.geometry = material(SCNSphere(radius: 0.55), trousers)
        body.addChildNode(pelvis)
        head.geometry = material(SCNSphere(radius: 0.5), skin)
        body.addChildNode(head)
        cap.geometry = material(SCNCylinder(radius: 0.58, height: 0.12), cream)
        body.addChildNode(cap)
        for _ in 0..<2 {
            let hand = SCNNode(geometry: material(SCNSphere(radius: 0.21), skin))
            body.addChildNode(hand)
            hands.append(hand)
            let foot = SCNNode(geometry: material(SCNBox(width: 0.7, height: 0.25, length: 0.4, chamferRadius: 0.1), UIColor(white: 0.15, alpha: 1)))
            body.addChildNode(foot)
            feet.append(foot)
        }
        shaft.geometry = material(SCNCylinder(radius: 0.06, height: 1), .lightGray)
        shaft.pivot = SCNMatrix4MakeTranslation(0, -0.5, 0)
        body.addChildNode(shaft)
        clubHead.geometry = material(SCNBox(width: 0.55, height: 0.35, length: 0.95, chamferRadius: 0.1), .darkGray)
        body.addChildNode(clubHead)
        apply(AvatarAnimations.address)
    }

    /// Faces the ball from the left of it (right-handers) or the right (left-handers).
    func setMirrored(_ mirrored: Bool) {
        node.simdScale = simd_float3(mirrored ? -1 : 1, 1, 1)
    }

    func apply(_ pose: BodyPose3D) {
        body.simdOrientation = pose.lean
        for (from, to, bone) in bones { place(bone, from: pose[from], to: pose[to]) }
        pelvis.simdPosition = pose[.root]
        head.simdPosition = pose[.nose]
        let up = simd_length(pose[.nose] - pose[.neck]) > 0.01 ? simd_normalize(pose[.nose] - pose[.neck]) : simd_float3(0, 1, 0)
        cap.simdPosition = pose[.nose] + up * 0.36
        cap.simdOrientation = Self.align(up)
        hands[0].simdPosition = pose[.leftWrist]
        hands[1].simdPosition = pose[.rightWrist]
        feet[0].simdPosition = pose[.leftAnkle] + simd_float3(0.25, -0.02, 0)
        feet[1].simdPosition = pose[.rightAnkle] + simd_float3(0.25, -0.02, 0)

        let grip: simd_float3
        let direction: simd_float3
        if pose.clubDropped {
            grip = simd_float3(1.1, 0.08, -1.1)
            direction = simd_normalize(simd_float3(0.35, 0, 1))
        } else {
            grip = pose.handCenter
            direction = pose.clubDirection
        }
        let end = grip + direction * AvatarSize.clubLength
        place(shaft, from: grip, to: end)
        clubHead.simdPosition = end + direction * 0.1
        clubHead.simdOrientation = Self.align(direction)
    }

    private func place(_ bone: SCNNode, from start: simd_float3, to end: simd_float3) {
        let length = simd_length(end - start)
        bone.simdPosition = start
        bone.simdScale = simd_float3(1, max(0.05, length), 1)
        guard length > 0.001 else { return }
        bone.simdOrientation = Self.align((end - start) / length)
    }

    /// Rotation taking a shape's long axis (+y) onto `direction`, in the body's own frame.
    /// (`simdLook(at:)` works in world space, which breaks once the rig is moved and turned.)
    private static func align(_ direction: simd_float3) -> simd_quatf {
        let unit = simd_normalize(direction)
        if simd_dot(unit, simd_float3(0, -1, 0)) > 0.9999 { return simd_quatf(angle: .pi, axis: simd_float3(1, 0, 0)) }
        return simd_quatf(from: simd_float3(0, 1, 0), to: unit)
    }
}
