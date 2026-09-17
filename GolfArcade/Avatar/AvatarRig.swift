import SceneKit
import UIKit

/// Rounded original sports-game golfer. All meshes are built once; the existing
/// measured joints and authoritative club endpoints continue to drive every pose.
@MainActor
final class AvatarRig {
    let node = SCNNode()
    private let body = SCNNode()
    private var bones: [(BodyJoint, BodyJoint, SCNNode)] = []
    private var sleeves: [(BodyJoint, BodyJoint, SCNNode)] = []
    private var joints: [(BodyJoint, SCNNode)] = []
    private let torso = SCNNode()
    private let collar = SCNNode()
    private let head = SCNNode()
    private let pelvis = SCNNode()
    private var hands: [SCNNode] = []
    private var feet: [SCNNode] = []
    private let shaft = SCNNode()
    private let handle = SCNNode()
    private let clubHead = SCNNode()

    init(shirt: UIColor) {
        node.name = "premiumGolfer"
        let navy = UIColor(red: 0.12, green: 0.18, blue: 0.27, alpha: 1)
        let skin = UIColor(red: 0.91, green: 0.69, blue: 0.49, alpha: 1)
        let ivory = UIColor(red: 0.98, green: 0.97, blue: 0.91, alpha: 1)
        let hair = UIColor(red: 0.19, green: 0.12, blue: 0.09, alpha: 1)
        node.addChildNode(body)
        func bone(_ from: BodyJoint, _ to: BodyJoint, radius: CGFloat, color: UIColor) {
            let shape = SCNCapsule(capRadius: radius, height: 1)
            shape.radialSegmentCount = 16
            let part = Self.part(shape, color)
            part.pivot = SCNMatrix4MakeTranslation(0, -0.5, 0)
            body.addChildNode(part)
            bones.append((from, to, part))
        }
        for (hip, knee, ankle, shoulder, elbow, wrist) in [
            (BodyJoint.leftHip, BodyJoint.leftKnee, BodyJoint.leftAnkle, BodyJoint.leftShoulder, BodyJoint.leftElbow, BodyJoint.leftWrist),
            (.rightHip, .rightKnee, .rightAnkle, .rightShoulder, .rightElbow, .rightWrist)
        ] {
            bone(hip, knee, radius: 0.29, color: navy)
            bone(knee, ankle, radius: 0.235, color: navy)
            bone(shoulder, elbow, radius: 0.18, color: skin)
            bone(elbow, wrist, radius: 0.145, color: skin)
            let sleeve = Self.part(SCNCapsule(capRadius: 0.245, height: 1), shirt)
            sleeve.pivot = SCNMatrix4MakeTranslation(0, -0.5, 0)
            body.addChildNode(sleeve)
            sleeves.append((shoulder, elbow, sleeve))
            for (joint, radius, color) in [(knee, 0.245, navy), (shoulder, 0.25, shirt), (elbow, 0.165, skin)] {
                let part = Self.part(SCNSphere(radius: radius), color)
                body.addChildNode(part)
                joints.append((joint, part))
            }
        }
        bone(.neck, .nose, radius: 0.21, color: skin)
        torso.name = "tailoredPolo"
        torso.geometry = Self.poloGeometry()
        torso.geometry?.materials = [Self.material(shirt)]
        body.addChildNode(torso)
        let placket = Self.part(SCNBox(width: 0.03, height: 0.22, length: 0.12, chamferRadius: 0.015), ivory)
        placket.position = SCNVector3(0.43, 0.83, 0)
        torso.addChildNode(placket)
        for y in [0.76, 0.84, 0.92] {
            let button = Self.part(SCNSphere(radius: 0.023), navy)
            button.position = SCNVector3(0.455, y, 0)
            button.scale.y = 0.45
            torso.addChildNode(button)
        }
        let crest = Self.part(SCNSphere(radius: 0.075), ivory)
        crest.position = SCNVector3(0.42, 0.76, -0.33)
        crest.scale = SCNVector3(0.15, 0.5, 1)
        torso.addChildNode(crest)
        pelvis.geometry = SCNSphere(radius: 0.56)
        pelvis.geometry?.materials = [Self.material(navy)]
        pelvis.scale = SCNVector3(0.75, 0.65, 1.18)
        body.addChildNode(pelvis)
        collar.geometry = SCNTorus(ringRadius: 0.26, pipeRadius: 0.09)
        collar.geometry?.materials = [Self.material(ivory)]
        body.addChildNode(collar)
        head.name = "golferFace"
        head.geometry = SCNSphere(radius: 0.62)
        (head.geometry as? SCNSphere)?.segmentCount = 32
        head.geometry?.materials = [Self.material(skin)]
        body.addChildNode(head)
        for side: Float in [-1, 1] {
            let ear = Self.part(SCNSphere(radius: 0.13), skin)
            ear.simdPosition = simd_float3(-0.02, -0.02, side * 0.59)
            ear.scale = SCNVector3(0.7, 1.2, 0.65)
            head.addChildNode(ear)
            let eye = Self.part(SCNSphere(radius: 0.12), ivory)
            eye.simdPosition = simd_float3(0.565, 0.07, side * 0.225)
            eye.scale = SCNVector3(0.38, 1.12, 0.82)
            head.addChildNode(eye)
            let pupil = Self.part(SCNSphere(radius: 0.061), navy)
            pupil.simdPosition = simd_float3(0.608, 0.06, side * 0.218)
            pupil.scale = SCNVector3(0.32, 1.2, 0.88)
            head.addChildNode(pupil)
            let glint = Self.part(SCNSphere(radius: 0.019), .white)
            glint.simdPosition = simd_float3(0.63, 0.088, side * 0.218 - 0.015)
            head.addChildNode(glint)
            let brow = Self.part(SCNCapsule(capRadius: 0.028, height: 0.2), hair)
            brow.simdPosition = simd_float3(0.55, 0.235, side * 0.23)
            brow.eulerAngles.x = .pi / 2 + side * 0.1
            head.addChildNode(brow)
        }
        let nose = Self.part(SCNSphere(radius: 0.13), skin)
        nose.position = SCNVector3(0.635, -0.06, 0)
        nose.scale = SCNVector3(1, 0.8, 0.85)
        head.addChildNode(nose)
        for i in 0..<8 {
            func smile(_ t: Float) -> simd_float3 { simd_float3(0.57, -0.27 + 0.09 * pow(2 * t - 1, 2), (t - 0.5) * 0.34) }
            let line = Self.part(SCNCylinder(radius: 0.018, height: 1), hair)
            line.pivot = SCNMatrix4MakeTranslation(0, -0.5, 0)
            head.addChildNode(line)
            place(line, from: smile(Float(i) / 8), to: smile(Float(i + 1) / 8))
        }
        let hairBack = Self.part(SCNSphere(radius: 0.635), hair)
        hairBack.position = SCNVector3(-0.12, 0.12, 0)
        hairBack.scale = SCNVector3(0.81, 0.86, 0.97)
        head.addChildNode(hairBack)
        let cap = Self.part(SCNSphere(radius: 0.66), ivory)
        cap.name = "golfCap"
        cap.position = SCNVector3(-0.025, 0.39, 0)
        cap.scale = SCNVector3(1.02, 0.56, 1.03)
        head.addChildNode(cap)
        let brim = Self.part(SCNBox(width: 0.82, height: 0.075, length: 1.15, chamferRadius: 0.12), ivory)
        brim.position = SCNVector3(0.56, 0.32, 0)
        brim.eulerAngles.z = -0.08
        head.addChildNode(brim)
        let badge = Self.part(SCNSphere(radius: 0.10), shirt)
        badge.position = SCNVector3(0.59, 0.48, 0)
        badge.scale = SCNVector3(0.17, 1, 1)
        head.addChildNode(badge)
        for index in 0..<2 {
            let hand = Self.part(SCNSphere(radius: 0.205), index == 0 ? ivory : skin)
            hand.scale = SCNVector3(0.9, 1.13, 0.86)
            body.addChildNode(hand)
            hands.append(hand)
            let foot = SCNNode()
            let shoe = Self.part(SCNBox(width: 0.78, height: 0.3, length: 0.47, chamferRadius: 0.14), ivory)
            shoe.position.y = 0.03
            foot.addChildNode(shoe)
            let sole = Self.part(SCNBox(width: 0.82, height: 0.09, length: 0.49, chamferRadius: 0.04), navy)
            sole.position.y = -0.1
            foot.addChildNode(sole)
            let saddle = Self.part(SCNBox(width: 0.23, height: 0.305, length: 0.475, chamferRadius: 0.065), navy)
            saddle.position.x = -0.03
            foot.addChildNode(saddle)
            for x in [-0.08, 0.0, 0.08] {
                let lace = Self.part(SCNBox(width: 0.028, height: 0.015, length: 0.23, chamferRadius: 0.007), ivory)
                lace.position = SCNVector3(x, 0.192, 0)
                foot.addChildNode(lace)
            }
            body.addChildNode(foot)
            feet.append(foot)
        }
        shaft.geometry = SCNCylinder(radius: 0.018, height: 1)
        shaft.geometry?.materials = [Self.material(UIColor(white: 0.8, alpha: 1), metal: 0.85)]
        shaft.pivot = SCNMatrix4MakeTranslation(0, -0.5, 0)
        body.addChildNode(shaft)
        handle.geometry = SCNCylinder(radius: 0.038, height: 1)
        handle.geometry?.materials = [Self.material(navy)]
        handle.pivot = SCNMatrix4MakeTranslation(0, -0.5, 0)
        body.addChildNode(handle)
        clubHead.name = "clubHead"
        clubHead.geometry = SCNBox(width: 0.30, height: 0.18, length: 0.38, chamferRadius: 0.055)
        clubHead.geometry?.materials = [Self.material(UIColor(white: 0.68, alpha: 1), metal: 0.75)]
        body.addChildNode(clubHead)
        let face = Self.part(SCNBox(width: 0.29, height: 0.16, length: 0.015, chamferRadius: 0.008), ivory)
        face.name = "strikingFace"
        face.position.z = -0.195
        clubHead.addChildNode(face)
        apply(AvatarAnimations.address)
    }

    func setMirrored(_ mirrored: Bool) { node.simdScale = simd_float3(mirrored ? -1 : 1, 1, 1) }

    func apply(_ pose: BodyPose3D) {
        shaft.isHidden = !pose.clubVisible
        handle.isHidden = !pose.clubVisible
        clubHead.isHidden = !pose.clubVisible
        body.simdOrientation = pose.lean
        for (from, to, bone) in bones { place(bone, from: pose[from], to: pose[to]) }
        for (from, to, sleeve) in sleeves { place(sleeve, from: pose[from], to: simd_mix(pose[from], pose[to], simd_float3(repeating: 0.57))) }
        for (joint, node) in joints { node.simdPosition = pose[joint] }
        let up = pose[.neck] - pose[.root]
        let shoulderAxis = pose[.rightShoulder] - pose[.leftShoulder]
        torso.simdPosition = pose[.root]
        torso.simdOrientation = Self.orientation(up: up, right: shoulderAxis)
        torso.simdScale = simd_float3(1, max(0.1, simd_length(up)), 1)
        pelvis.simdPosition = pose[.root]
        collar.simdPosition = pose[.neck] - simd_normalize(up + simd_float3(0, 0.0001, 0)) * 0.06
        collar.simdOrientation = torso.simdOrientation
        head.simdPosition = pose[.nose]
        head.simdOrientation = Self.orientation(up: pose[.nose] - pose[.neck], right: shoulderAxis)
        hands[0].simdPosition = pose[.leftWrist]
        hands[1].simdPosition = pose[.rightWrist]
        feet[0].simdPosition = pose[.leftAnkle] + simd_float3(0.22, -0.01, 0)
        feet[1].simdPosition = pose[.rightAnkle] + simd_float3(0.22, -0.01, 0)
        let grip = pose.clubDropped ? simd_float3(1.1, 0.08, -1.1) : pose.clubGrip
        let direction = pose.clubDropped ? simd_normalize(simd_float3(0.35, 0, 1)) : pose.clubDirection
        let end = pose.clubDropped ? grip + direction * AvatarSize.clubLength : pose.clubHead
        let orientation = ClubGeometry.headOrientation(shaftUp: grip - end)
        // The tracked endpoint is the contact point at the face, not the solid head's
        // center. Put the heel behind it so addressing the ball doesn't engulf it.
        let heel = end + orientation.act(simd_float3(0, 0, 0.195 + Float(AvatarSize.visibleBallRadius) / AvatarSize.courseScale))
        place(shaft, from: grip, to: heel)
        place(handle, from: grip, to: simd_mix(grip, heel, simd_float3(repeating: 0.17)))
        clubHead.simdPosition = heel
        clubHead.simdOrientation = orientation
    }

    private static func material(_ color: UIColor, metal: CGFloat = 0) -> SCNMaterial {
        let result = SCNMaterial()
        result.lightingModel = .physicallyBased
        result.diffuse.contents = color
        result.roughness.contents = metal > 0 ? 0.28 : 0.76
        result.metalness.contents = metal
        return result
    }

    private static func part(_ geometry: SCNGeometry, _ color: UIColor, metal: CGFloat = 0) -> SCNNode {
        geometry.materials = [material(color, metal: metal)]
        return SCNNode(geometry: geometry)
    }

    private static func poloGeometry() -> SCNGeometry {
        let rings: [(Float, Float, Float)] = [(0, 0.33, 0.50), (0.06, 0.41, 0.60),
            (0.42, 0.44, 0.67), (0.78, 0.44, 0.77), (0.90, 0.39, 0.71), (1, 0.22, 0.28)]
        let segments = 32
        var vertices: [SCNVector3] = [], normals: [SCNVector3] = [], indices: [Int32] = []
        for (y, depth, width) in rings {
            for i in 0...segments {
                let angle = Float(i) / Float(segments) * .pi * 2
                vertices.append(SCNVector3(cos(angle) * depth, y, sin(angle) * width))
                normals.append(SCNVector3(simd_normalize(simd_float3(cos(angle) / depth, 0.1, sin(angle) / width))))
            }
        }
        for ring in 0..<rings.count - 1 {
            for i in 0..<segments {
                let a = Int32(ring * (segments + 1) + i), b = a + Int32(segments + 1)
                indices += [a, b, a + 1, a + 1, b, b + 1]
            }
        }
        return SCNGeometry(sources: [.init(vertices: vertices), .init(normals: normals)],
                           elements: [SCNGeometryElement(indices: indices, primitiveType: .triangles)])
    }

    private func place(_ bone: SCNNode, from start: simd_float3, to end: simd_float3) {
        let length = simd_length(end - start)
        bone.simdPosition = start
        bone.simdScale = simd_float3(1, max(0.05, length), 1)
        if length > 0.001 { bone.simdOrientation = Self.align((end - start) / length) }
    }

    private static func orientation(up: simd_float3, right: simd_float3) -> simd_quatf {
        guard simd_length(up) > 0.001, simd_length(right) > 0.001 else { return simd_quatf() }
        let y = simd_normalize(up), cross = simd_cross(y, right)
        guard simd_length(cross) > 0.001 else { return align(y) }
        let x = simd_normalize(cross), z = simd_normalize(simd_cross(x, y))
        return simd_quatf(simd_float3x3(columns: (x, y, z)))
    }

    private static func align(_ direction: simd_float3) -> simd_quatf {
        guard simd_length(direction) > 0.001 else { return simd_quatf() }
        let unit = simd_normalize(direction)
        if simd_dot(unit, simd_float3(0, -1, 0)) > 0.9999 { return simd_quatf(angle: .pi, axis: simd_float3(1, 0, 0)) }
        return simd_quatf(from: simd_float3(0, 1, 0), to: unit)
    }
}
