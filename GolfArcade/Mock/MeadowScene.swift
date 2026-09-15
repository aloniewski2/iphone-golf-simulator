import SceneKit
import SwiftUI

@MainActor
final class MeadowScene {
    let scene = SCNScene()
    let camera = SCNNode()
    private let ball = SCNNode()
    private let shadow = SCNNode()
    private let club = SCNNode()
    private let trail = SCNNode()
    private let aimLine = SCNNode()
    private let impact = SCNNode()
    private let avatar = SCNNode()
    private var avatarArms: [(bone: SCNNode, from: BodyJoint, to: BodyJoint)] = []
    private var avatarHands: [BodyJoint: SCNNode] = [:]
    private let avatarClub = SCNNode()
    private var lastShot: RangeShot?

    /// Shoulder width of the avatar in scene yards; arms are scaled from the player's shoulders.
    private let avatarShoulderWidth: Float = 1.6
    private let avatarShoulderHeight: Float = 4.6

    init() {
        scene.background.contents = UIColor(red: 0.65, green: 0.84, blue: 0.88, alpha: 1)
        scene.fogColor = UIColor(red: 0.65, green: 0.84, blue: 0.88, alpha: 1)
        scene.fogStartDistance = 290
        scene.fogEndDistance = 600
        camera.camera = SCNCamera()
        camera.camera?.fieldOfView = 58
        camera.camera?.zFar = 900
        scene.rootNode.addChildNode(camera)
        let sun = SCNNode()
        sun.light = SCNLight()
        sun.light?.type = .directional
        sun.light?.intensity = 1300
        sun.eulerAngles = SCNVector3(-0.85, -0.4, 0)
        scene.rootNode.addChildNode(sun)
        let ambient = SCNNode()
        ambient.light = SCNLight()
        ambient.light?.type = .ambient
        ambient.light?.intensity = 650
        scene.rootNode.addChildNode(ambient)

        let ground = SCNBox(width: 700, height: 1, length: 800, chamferRadius: 0)
        add(ground, color: UIColor(red: 0.18, green: 0.40, blue: 0.29, alpha: 1), at: SCNVector3(0, -1, -190))
        for i in 0..<16 {
            let strip = SCNBox(width: 75 + CGFloat(i) * 1.4, height: 0.15, length: 16, chamferRadius: 0)
            let green = UIColor(red: 0.30, green: i.isMultiple(of: 2) ? 0.61 : 0.57, blue: 0.37, alpha: 1)
            add(strip, color: green, at: SCNVector3(0, -0.4, -Float(i * 16)))
        }
        for target in RangeTarget.all {
            let colors: [UIColor] = [.systemOrange, .systemTeal, .systemYellow]
            for (scale, color) in [(1.0, colors[target.id]), (0.6, UIColor.white), (0.25, colors[target.id])] {
                let disc = SCNCylinder(radius: target.radius * scale, height: 0.10)
                disc.radialSegmentCount = 64
                add(disc, color: color, at: SCNVector3(target.x, 0.04 + (1 - scale) * 0.3, -target.distance))
            }
            add(SCNCylinder(radius: 0.13, height: 8), color: .white, at: SCNVector3(target.x, 4, -target.distance))
            add(SCNBox(width: 4, height: 2.2, length: 0.12, chamferRadius: 0.1), color: colors[target.id], at: SCNVector3(target.x + 2, 7.1, -target.distance))
            label("\(Int(target.distance)) YD", at: SCNVector3(target.x - 5, 11, -target.distance))
        }
        for i in 0..<40 {
            let side: Float = i.isMultiple(of: 2) ? -1 : 1
            let x = side * Float(47 + (i * 13 % 37))
            let z = -Float(i * 7 + 12)
            let height = CGFloat(8 + i % 8)
            add(SCNCylinder(radius: 0.65, height: 4), color: .brown, at: SCNVector3(x, 2, z))
            let cone = SCNCone(topRadius: 0, bottomRadius: 4.5, height: height)
            cone.radialSegmentCount = 7
            add(cone, color: UIColor(red: 0.10, green: 0.31 + Double(i % 3) * 0.05, blue: 0.25, alpha: 1), at: SCNVector3(x, Float(height / 2) + 3, z))
        }
        for i in 0..<6 {
            let hill = SCNSphere(radius: 55)
            hill.segmentCount = 12
            let node = add(hill, color: UIColor(red: 0.24, green: 0.43, blue: 0.38, alpha: 1), at: SCNVector3(Float(i * 80 - 200), -10, -340))
            node.scale = SCNVector3(1.4, 0.9, 1)
        }
        add(SCNBox(width: 9, height: 0.25, length: 7, chamferRadius: 0.4), color: UIColor(red: 0.09, green: 0.29, blue: 0.22, alpha: 1), at: SCNVector3(0, -0.1, 1))
        ball.geometry = SCNSphere(radius: 0.55)
        ball.geometry?.firstMaterial?.diffuse.contents = UIColor.white
        ball.geometry?.firstMaterial?.emission.contents = UIColor(white: 0.15, alpha: 1)
        scene.rootNode.addChildNode(ball)
        shadow.geometry = SCNCylinder(radius: 0.9, height: 0.025)
        shadow.geometry?.firstMaterial?.diffuse.contents = UIColor.black.withAlphaComponent(0.25)
        scene.rootNode.addChildNode(shadow)
        let shaft = SCNNode(geometry: SCNCylinder(radius: 0.07, height: 6))
        shaft.geometry?.firstMaterial?.diffuse.contents = UIColor.lightGray
        shaft.position.y = 3
        club.addChildNode(shaft)
        let head = SCNNode(geometry: SCNBox(width: 1.4, height: 0.65, length: 0.85, chamferRadius: 0.3))
        head.geometry?.firstMaterial?.diffuse.contents = UIColor.darkGray
        club.addChildNode(head)
        club.position = SCNVector3(1.5, 0.6, 0.4)
        scene.rootNode.addChildNode(club)
        scene.rootNode.addChildNode(trail)
        scene.rootNode.addChildNode(aimLine)
        impact.geometry = SCNSphere(radius: 1)
        impact.geometry?.firstMaterial?.diffuse.contents = UIColor.systemYellow
        impact.geometry?.firstMaterial?.emission.contents = UIColor.systemYellow
        impact.position = SCNVector3(0, 0.6, 0)
        scene.rootNode.addChildNode(impact)
        buildAvatar()
        for i in 1...10 {
            let dot = SCNNode(geometry: SCNSphere(radius: 0.18))
            dot.geometry?.firstMaterial?.diffuse.contents = UIColor.white.withAlphaComponent(0.75)
            dot.position = SCNVector3(0, 0.35, -Float(i * 3))
            aimLine.addChildNode(dot)
        }
        update(shot: nil, elapsed: 0, power: 0, aim: 0)
    }

    func update(shot: RangeShot?, elapsed: Double, power: Double, aim: Double, pose: PoseFrame? = nil) {
        SCNTransaction.begin()
        SCNTransaction.disableActions = true
        updateAvatar(pose, load: shot == nil ? power : 0)
        if lastShot != shot {
            trail.childNodes.forEach { $0.removeFromParentNode() }
            if let shot {
                for i in 0..<60 {
                    let point = shot.position(at: Double(i) / 59 * shot.duration)
                    let dot = SCNNode(geometry: SCNSphere(radius: 0.20))
                    dot.geometry?.firstMaterial?.diffuse.contents = UIColor.white.withAlphaComponent(0.65)
                    dot.position = SCNVector3(point.lateralYards, point.heightYards + 0.5, -point.distanceYards)
                    trail.addChildNode(dot)
                }
            }
            lastShot = shot
        }
        let point = shot?.position(at: elapsed) ?? FlightPoint(lateralYards: 0, heightYards: 0, distanceYards: 0)
        ball.position = SCNVector3(point.lateralYards, point.heightYards + 0.6, -point.distanceYards)
        shadow.position = SCNVector3(point.lateralYards, 0.15, -point.distanceYards)
        let distance = Float(point.distanceYards)
        camera.position = SCNVector3(Float(point.lateralYards) * 0.65 + 12, 22 + Float(point.heightYards) * 0.55, 38 - distance * 0.86)
        camera.look(at: SCNVector3(Float(point.lateralYards), max(0, Float(point.heightYards) * 0.75), -max(40, distance + 14)))
        club.isHidden = shot != nil
        let burst = min(max(elapsed / 0.23, 0), 1)
        impact.isHidden = shot == nil || elapsed > 0.23
        impact.opacity = CGFloat((1 - burst) * 0.7)
        impact.scale = SCNVector3(1 + burst * 3, 1 + burst * 3, 1 + burst * 3)
        club.eulerAngles = SCNVector3(0, 0, Float(-power * 1.8))
        club.position.y = 0.6 + Float(power * 5)
        aimLine.isHidden = shot != nil
        aimLine.eulerAngles.y = Float(-aim * .pi / 180)
        if let shot {
            for (i, dot) in trail.childNodes.enumerated() { dot.isHidden = Double(i) / 59 * shot.duration > elapsed }
        }
        SCNTransaction.commit()
    }

    /// A simple golfer beside the tee: a fixed body, and arms that follow the player.
    private func buildAvatar() {
        let cream = UIColor(red: 0.96, green: 0.96, blue: 0.86, alpha: 1)
        let shirt = UIColor(red: 0.95, green: 0.45, blue: 0.3, alpha: 1)
        let trousers = UIColor(red: 0.16, green: 0.2, blue: 0.3, alpha: 1)
        let skin = UIColor(red: 0.93, green: 0.78, blue: 0.62, alpha: 1)

        func part(_ geometry: SCNGeometry, _ color: UIColor, at position: SCNVector3, tilt: Float = 0) -> SCNNode {
            geometry.firstMaterial?.diffuse.contents = color
            let node = SCNNode(geometry: geometry)
            node.position = position
            node.eulerAngles.z = tilt
            avatar.addChildNode(node)
            return node
        }
        let legHeight: Float = 2.4
        let torsoHeight: Float = 2.2
        part(SCNCapsule(capRadius: 0.24, height: CGFloat(legHeight)), trousers, at: SCNVector3(-0.45, legHeight / 2, 0))
        part(SCNCapsule(capRadius: 0.24, height: CGFloat(legHeight)), trousers, at: SCNVector3(0.45, legHeight / 2, 0))
        part(SCNCapsule(capRadius: 0.5, height: CGFloat(torsoHeight) + 0.6), shirt, at: SCNVector3(0, legHeight + torsoHeight / 2, 0))
        part(SCNSphere(radius: 0.48), skin, at: SCNVector3(0, avatarShoulderHeight + 0.85, 0))
        part(SCNCylinder(radius: 0.55, height: 0.12), cream, at: SCNVector3(0, avatarShoulderHeight + 1.2, 0)) // cap brim

        for (from, to) in [(BodyJoint.leftShoulder, BodyJoint.leftElbow), (.leftElbow, .leftWrist), (.rightShoulder, .rightElbow), (.rightElbow, .rightWrist)] {
            let bone = SCNNode(geometry: SCNCylinder(radius: 0.14, height: 1))
            bone.geometry?.firstMaterial?.diffuse.contents = to == .leftWrist || to == .rightWrist ? skin : shirt
            bone.pivot = SCNMatrix4MakeTranslation(0, -0.5, 0) // grows from its base
            avatar.addChildNode(bone)
            avatarArms.append((bone, from, to))
        }
        for joint in [BodyJoint.leftWrist, .rightWrist] {
            let hand = SCNNode(geometry: SCNSphere(radius: 0.2))
            hand.geometry?.firstMaterial?.diffuse.contents = skin
            avatar.addChildNode(hand)
            avatarHands[joint] = hand
        }
        let shaft = SCNNode(geometry: SCNCylinder(radius: 0.06, height: 1))
        shaft.geometry?.firstMaterial?.diffuse.contents = UIColor.lightGray
        shaft.pivot = SCNMatrix4MakeTranslation(0, -0.5, 0)
        avatarClub.addChildNode(shaft)
        let head = SCNNode(geometry: SCNBox(width: 0.5, height: 0.35, length: 0.9, chamferRadius: 0.1))
        head.geometry?.firstMaterial?.diffuse.contents = UIColor.darkGray
        head.position = SCNVector3(0, 1, 0.2)
        avatarClub.addChildNode(head)
        avatar.addChildNode(avatarClub)

        avatar.position = SCNVector3(3.2, 0, 0)
        scene.rootNode.addChildNode(avatar)
    }

    /// Arms come from the tracked pose when there is one (mirrored, scaled by shoulder width), and
    /// otherwise swing procedurally with the load meter so every input mode animates the golfer.
    private func updateAvatar(_ pose: PoseFrame?, load: Double) {
        let base = SCNVector3(0, avatarShoulderHeight, 0.3)
        var joints: [BodyJoint: SCNVector3] = [:]
        if let pose, let left = pose.point(.leftShoulder), let right = pose.point(.rightShoulder) {
            let center = CGPoint(x: (left.x + right.x) / 2, y: (left.y + right.y) / 2)
            let width = max(0.02, hypot(right.x - left.x, right.y - left.y))
            let scale = avatarShoulderWidth / Float(width)
            for joint in [BodyJoint.leftShoulder, .rightShoulder, .leftElbow, .rightElbow, .leftWrist, .rightWrist] {
                guard let point = pose.point(joint) else { continue }
                joints[joint] = SCNVector3(base.x + Float(point.x - center.x) * scale, base.y + Float(point.y - center.y) * scale, base.z)
            }
        } else {
            // Hands hang below the shoulders at address and arc up to the trail side with the load.
            let angle = Float(load) * 2.3
            let reach = avatarShoulderWidth * 1.4
            let hands = SCNVector3(base.x + sin(angle) * reach, base.y - cos(angle) * reach, base.z)
            let half = avatarShoulderWidth / 2
            joints[.leftShoulder] = SCNVector3(base.x - half, base.y, base.z)
            joints[.rightShoulder] = SCNVector3(base.x + half, base.y, base.z)
            for (shoulder, elbow, wrist) in [(BodyJoint.leftShoulder, BodyJoint.leftElbow, BodyJoint.leftWrist), (.rightShoulder, .rightElbow, .rightWrist)] {
                let s = joints[shoulder]!
                joints[wrist] = SCNVector3(hands.x + (shoulder == .leftShoulder ? -0.12 : 0.12), hands.y, hands.z)
                joints[elbow] = SCNVector3((s.x + hands.x) / 2 + (shoulder == .leftShoulder ? -0.3 : 0.3), (s.y + hands.y) / 2 - 0.15, base.z + 0.2)
            }
        }
        for (bone, from, to) in avatarArms {
            guard let start = joints[from], let end = joints[to] else { bone.isHidden = true; continue }
            bone.isHidden = false
            bone.position = start
            bone.scale = SCNVector3(1, max(0.05, simd_length(simd_float3(end) - simd_float3(start))), 1)
            bone.look(at: end, up: SCNVector3(0, 0, 1), localFront: SCNVector3(0, 1, 0))
        }
        for (joint, hand) in avatarHands {
            if let position = joints[joint] { hand.isHidden = false; hand.position = position } else { hand.isHidden = true }
        }
        // The club continues the line from the shoulders through the hands.
        let wrists = [joints[.leftWrist], joints[.rightWrist]].compactMap { $0 }
        guard !wrists.isEmpty, let ls = joints[.leftShoulder], let rs = joints[.rightShoulder] else { avatarClub.isHidden = true; return }
        avatarClub.isHidden = false
        let hands = simd_float3(wrists.map(simd_float3.init).reduce(simd_float3(), +)) / Float(wrists.count)
        let shoulders = (simd_float3(ls) + simd_float3(rs)) / 2
        var direction = hands - shoulders
        direction.z = 0
        if simd_length(direction) < 0.05 { direction = simd_float3(0, -1, 0) }
        let clubLength = avatarShoulderWidth * 1.7
        avatarClub.position = SCNVector3(hands)
        avatarClub.scale = SCNVector3(1, clubLength, 1)
        avatarClub.look(at: SCNVector3(hands + simd_normalize(direction) * clubLength), up: SCNVector3(0, 0, 1), localFront: SCNVector3(0, 1, 0))
    }

    @discardableResult
    private func add(_ geometry: SCNGeometry, color: UIColor, at position: SCNVector3) -> SCNNode {
        geometry.firstMaterial?.diffuse.contents = color
        let node = SCNNode(geometry: geometry)
        node.position = position
        scene.rootNode.addChildNode(node)
        return node
    }

    private func label(_ text: String, at position: SCNVector3) {
        let geometry = SCNText(string: text, extrusionDepth: 0.01)
        geometry.font = .systemFont(ofSize: 2.2, weight: .heavy)
        geometry.flatness = 0.3
        let node = add(geometry, color: .white, at: position)
        let facing = SCNBillboardConstraint()
        facing.freeAxes = .Y
        node.constraints = [facing]
    }
}

struct MeadowSceneView: UIViewRepresentable {
    let meadow: MeadowScene
    let shot: RangeShot?
    let elapsed: Double
    let power: Double
    let aim: Double
    var pose: PoseFrame? = nil

    func makeUIView(context: Context) -> SCNView {
        let view = SCNView()
        view.scene = meadow.scene
        view.pointOfView = meadow.camera
        view.antialiasingMode = .multisampling4X
        view.preferredFramesPerSecond = 60
        view.isUserInteractionEnabled = false
        return view
    }

    func updateUIView(_ view: SCNView, context: Context) {
        meadow.update(shot: shot, elapsed: elapsed, power: power, aim: aim, pose: pose)
    }
}
