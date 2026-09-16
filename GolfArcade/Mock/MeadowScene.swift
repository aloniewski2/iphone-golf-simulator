import SceneKit
import SwiftUI

@MainActor
final class MeadowScene {
    let scene = SCNScene()
    let camera = SCNNode()
    private let ball = SCNNode()
    private let shadow = SCNNode()
    private let trail = SCNNode()
    private let aimLine = SCNNode()
    private let impact = SCNNode()
    private let golfer = Golfer()
    let course: GolfCourse
    private var lastShot: RangeShot?
    private var lastElapsed = 0.0

    init(course: GolfCourse = .easy) {
        self.course = course
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
        let stripCount = max(10, Int(course.holeDistance / 16) + 2)
        for i in 0..<stripCount {
            let perspectiveWidth = course.fairwayWidth + Double(i) * 0.08
            let strip = SCNBox(width: perspectiveWidth, height: 0.15, length: 16, chamferRadius: 0)
            let green = UIColor(red: 0.30, green: i.isMultiple(of: 2) ? 0.61 : 0.57, blue: 0.37, alpha: 1)
            add(strip, color: green, at: SCNVector3(0, -0.4, -Float(i * 16)))
        }

        let green = SCNCylinder(radius: course.greenRadius, height: 0.20)
        green.radialSegmentCount = 64
        add(green, color: UIColor(red: 0.43, green: 0.76, blue: 0.42, alpha: 1), at: SCNVector3(0, -0.25, -course.holeDistance))
        for hazard in course.hazards {
            let shape = SCNSphere(radius: 1)
            shape.segmentCount = 32
            let color = hazard.kind == .bunker
                ? UIColor(red: 0.88, green: 0.79, blue: 0.57, alpha: 1)
                : UIColor(red: 0.18, green: 0.55, blue: 0.78, alpha: 1)
            let node = add(shape, color: color, at: SCNVector3(hazard.x, -0.18, -hazard.distance))
            node.scale = SCNVector3(hazard.width / 2, 0.12, hazard.length / 2)
        }
        add(SCNCylinder(radius: 0.13, height: 8), color: .white, at: SCNVector3(0, 4, -course.holeDistance))
        add(SCNBox(width: 4, height: 2.2, length: 0.12, chamferRadius: 0.1), color: .systemOrange, at: SCNVector3(2, 7.1, -course.holeDistance))
        label("\(Int(course.holeDistance)) YD", at: SCNVector3(-5, 11, -course.holeDistance))

        let treeCount = course.difficulty == .easy ? 28 : course.difficulty == .medium ? 38 : 48
        for i in 0..<treeCount {
            let side: Float = i.isMultiple(of: 2) ? -1 : 1
            let edge = Float(course.fairwayWidth / 2 + 12)
            let x = side * (edge + Float(i * 13 % 37))
            let z = -Float(i * 7 % max(20, Int(course.holeDistance)) + 12)
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
        scene.rootNode.addChildNode(golfer.node)
        scene.rootNode.addChildNode(trail)
        scene.rootNode.addChildNode(aimLine)
        impact.geometry = SCNSphere(radius: 1)
        impact.geometry?.firstMaterial?.diffuse.contents = UIColor.systemYellow
        impact.geometry?.firstMaterial?.emission.contents = UIColor.systemYellow
        impact.position = SCNVector3(0, 0.6, 0)
        scene.rootNode.addChildNode(impact)
        for i in 1...10 {
            let dot = SCNNode(geometry: SCNSphere(radius: 0.18))
            dot.geometry?.firstMaterial?.diffuse.contents = UIColor.white.withAlphaComponent(0.75)
            dot.position = SCNVector3(0, 0.35, -Float(i * 3))
            aimLine.addChildNode(dot)
        }
        update(shot: nil, elapsed: 0, power: 0, aim: 0)
    }

    /// `swingAngle` is where the player's swing is right now, in degrees of arc: 0 at address,
    /// positive going back, negative through. Once a shot is launched the golfer plays its own
    /// downswing and follow-through, and the live angle is ignored until the next ball.
    func update(shot: RangeShot?, elapsed: Double, power: Double, aim: Double, swingAngle: Double = 0, handedness: Handedness = .right) {
        SCNTransaction.begin()
        SCNTransaction.disableActions = true
        golfer.handedness = handedness
        if let shot {
            if lastShot != shot || elapsed < lastElapsed - 0.5 { golfer.launch(replay: lastShot == shot) }
            golfer.animate(elapsed: elapsed)
        } else {
            golfer.follow(swingAngle)
        }
        lastElapsed = elapsed
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
        let lookDistance = shot == nil ? min(Float(course.holeDistance), 95) : max(40, distance + 14)
        camera.look(at: SCNVector3(Float(point.lateralYards), max(0, Float(point.heightYards) * 0.75), -lookDistance))
        let burst = min(max(elapsed / 0.23, 0), 1)
        impact.isHidden = shot == nil || elapsed > 0.23
        impact.opacity = CGFloat((1 - burst) * 0.7)
        impact.scale = SCNVector3(1 + burst * 3, 1 + burst * 3, 1 + burst * 3)
        aimLine.isHidden = shot != nil
        aimLine.eulerAngles.y = Float(-aim * .pi / 180)
        if let shot {
            for (i, dot) in trail.childNodes.enumerated() { dot.isHidden = Double(i) / 59 * shot.duration > elapsed }
        }
        SCNTransaction.commit()
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

/// A Mii-style golfer beside the tee. The body is rigid; the arms have fixed lengths and travel
/// along one clean swing arc around the chest, driven by a single angle. Elbows come from two-bone
/// IK, so the arms bend but never stretch. The shoulders turn with the arc for a fuller motion.
@MainActor
final class Golfer {
    let node = SCNNode()
    var handedness: Handedness = .right {
        didSet { if handedness != oldValue { applyHandedness() } }
    }

    private let upperBody = SCNNode()
    private let bones: [SCNNode]          // left upper, left fore, right upper, right fore
    private let hands: [SCNNode]
    private let club = SCNNode()
    private var angle = 0.0                // displayed arc, degrees
    private var launchAngle = 0.0

    // Upper-body frame (origin at the hips, 2.4 above the feet): +x toward the ball,
    // +z the golfer's right (toward the range camera), +y up.
    private let hipHeight: Float = 2.4
    private let pivot = simd_float3(0.55, 1.8, 0)            // chest, where the arc is centred
    private let shoulders = [simd_float3(0.7, 2.0, -0.75), simd_float3(0.7, 2.0, 0.75)] // left, right
    private let addressHands = simd_float3(1.9, -0.3, 0.1)
    private let upperArm: Float = 1.35
    private let forearm: Float = 1.35
    private let clubLength: Float = 2.7
    private let fullBackswing = 150.0
    private let finish = -150.0

    init() {
        let shirt = UIColor(red: 0.95, green: 0.45, blue: 0.3, alpha: 1)
        let trousers = UIColor(red: 0.16, green: 0.2, blue: 0.3, alpha: 1)
        let skin = UIColor(red: 0.93, green: 0.78, blue: 0.62, alpha: 1)
        let cream = UIColor(red: 0.96, green: 0.96, blue: 0.86, alpha: 1)
        func part(_ geometry: SCNGeometry, _ color: UIColor, at position: simd_float3, parent: SCNNode, tilt: Float = 0) {
            geometry.firstMaterial?.diffuse.contents = color
            geometry.firstMaterial?.isDoubleSided = true
            let part = SCNNode(geometry: geometry)
            part.simdPosition = position
            part.eulerAngles.z = tilt
            parent.addChildNode(part)
        }
        // Stance is along z; the golfer leans a little toward the ball (+x).
        part(SCNCapsule(capRadius: 0.26, height: 2.5), trousers, at: simd_float3(0.05, 1.25, -0.6), parent: node, tilt: -0.05)
        part(SCNCapsule(capRadius: 0.26, height: 2.5), trousers, at: simd_float3(0.05, 1.25, 0.6), parent: node, tilt: -0.05)
        upperBody.simdPosition = simd_float3(0, hipHeight, 0)
        node.addChildNode(upperBody)
        part(SCNCapsule(capRadius: 0.55, height: 2.0), shirt, at: simd_float3(0.3, 0.95, 0), parent: upperBody, tilt: -0.32)
        let shoulderBar = SCNCapsule(capRadius: 0.32, height: 2.1)
        shoulderBar.firstMaterial?.diffuse.contents = shirt
        let bar = SCNNode(geometry: shoulderBar)
        bar.simdPosition = simd_float3(0.7, 2.0, 0)
        bar.eulerAngles.x = .pi / 2 // lies along z, joining the two shoulders
        upperBody.addChildNode(bar)
        part(SCNSphere(radius: 0.5), skin, at: simd_float3(0.95, 2.75, 0), parent: upperBody)
        part(SCNCylinder(radius: 0.58, height: 0.12), cream, at: simd_float3(0.95, 3.1, 0), parent: upperBody)

        var bones: [SCNNode] = []
        for index in 0..<4 {
            let bone = SCNNode(geometry: SCNCylinder(radius: 0.16, height: 1))
            bone.geometry?.firstMaterial?.diffuse.contents = index % 2 == 0 ? shirt : skin
            bone.geometry?.firstMaterial?.isDoubleSided = true
            bone.pivot = SCNMatrix4MakeTranslation(0, -0.5, 0)
            upperBody.addChildNode(bone)
            bones.append(bone)
        }
        self.bones = bones
        var hands: [SCNNode] = []
        for _ in 0..<2 {
            let hand = SCNNode(geometry: SCNSphere(radius: 0.21))
            hand.geometry?.firstMaterial?.diffuse.contents = skin
            upperBody.addChildNode(hand)
            hands.append(hand)
        }
        self.hands = hands
        let shaft = SCNNode(geometry: SCNCylinder(radius: 0.06, height: 1))
        shaft.geometry?.firstMaterial?.diffuse.contents = UIColor.lightGray
        shaft.pivot = SCNMatrix4MakeTranslation(0, -0.5, 0)
        club.addChildNode(shaft)
        let head = SCNNode(geometry: SCNBox(width: 0.55, height: 0.35, length: 0.95, chamferRadius: 0.1))
        head.geometry?.firstMaterial?.diffuse.contents = UIColor.darkGray
        head.position = SCNVector3(0.1, 1, 0.15)
        club.addChildNode(head)
        upperBody.addChildNode(club)
        applyHandedness()
        pose(0)
    }

    /// Live tracking: ease toward the player's arc so the motion stays clean.
    func follow(_ target: Double) {
        let clamped = min(max(target, finish), fullBackswing)
        angle += (clamped - angle) * 0.35
        pose(angle)
    }

    func launch(replay: Bool) {
        launchAngle = replay ? fullBackswing : max(angle, 60)
    }

    /// Canned downswing (0.3 s) into a held finish, then back to address for the next ball.
    func animate(elapsed: Double) {
        let t: Double
        if elapsed < 0.3 {
            let u = elapsed / 0.3
            t = launchAngle + (finish - launchAngle) * (u * u) // accelerates through the ball
        } else if elapsed < 3 {
            t = finish
        } else {
            let u = min(1, (elapsed - 3) / 1.2)
            t = finish + (0 - finish) * (1 - cos(u * .pi)) / 2
        }
        angle = t
        pose(t)
    }

    private func applyHandedness() {
        let mirror: Float = handedness == .right ? 1 : -1
        node.simdScale = simd_float3(mirror, 1, 1)
        node.simdPosition = simd_float3(-3.2 * mirror, 0, 0)
    }

    /// Hands move on a circle around the chest: down-forward at address, out to the right at 90°,
    /// up and behind the trail shoulder at the top; negative angles mirror through to the finish.
    private func pose(_ degrees: Double) {
        let radians = Float(degrees * .pi / 180)
        let toAddress = addressHands - pivot
        let radius = simd_length(toAddress)
        let d0 = toAddress / radius
        var d1 = simd_float3(-0.25, 0.2, 1)
        d1 = simd_normalize(d1 - simd_dot(d1, d0) * d0)
        let hands = pivot + (cos(radians) * d0 + sin(radians) * d1) * radius
        // The shoulders turn with the arms, about a third of the arc.
        upperBody.eulerAngles.y = -radians * 0.35

        for side in 0..<2 {
            let shoulder = shoulders[side]
            let hand = hands + simd_float3(side == 0 ? 0.12 : -0.12, side == 0 ? -0.08 : 0.08, 0)
            var reach = hand - shoulder
            let distance = min(simd_length(reach), upperArm + forearm - 0.05)
            reach = simd_normalize(reach) * distance
            let wrist = shoulder + reach
            // Two-bone IK: elbows bend out and back, never past straight.
            let axis = reach / distance
            let a = (upperArm * upperArm - forearm * forearm + distance * distance) / (2 * distance)
            let height = sqrt(max(0, upperArm * upperArm - a * a))
            var bend = simd_float3(-0.7, -0.3, side == 0 ? -1 : 1)
            bend = simd_normalize(bend - simd_dot(bend, axis) * axis)
            let elbow = shoulder + axis * a + bend * height
            place(bones[side * 2], from: shoulder, to: elbow)
            place(bones[side * 2 + 1], from: elbow, to: wrist)
            self.hands[side].simdPosition = wrist
        }
        // Wrist hinge: the club hangs on the arm line at address and cocks up to 90° along the
        // direction of travel, so it lies over the shoulder at the top and points skyward through.
        let armLine = simd_normalize(hands - pivot)
        let tangent = simd_normalize(-sin(radians) * d0 + cos(radians) * d1) * (degrees < 0 ? -1 : 1)
        let hinge = Float(min(1, abs(degrees) / 100) * .pi / 2)
        let shaft = simd_normalize(cos(hinge) * armLine + sin(hinge) * tangent)
        club.simdPosition = hands
        club.simdScale = simd_float3(1, clubLength, 1)
        club.simdLook(at: hands + shaft * clubLength, up: simd_float3(0, 0, 1), localFront: simd_float3(0, 1, 0))
    }

    private func place(_ bone: SCNNode, from start: simd_float3, to end: simd_float3) {
        bone.simdPosition = start
        bone.simdScale = simd_float3(1, max(0.05, simd_length(end - start)), 1)
        bone.simdLook(at: end, up: simd_float3(0, 0, 1), localFront: simd_float3(0, 1, 0))
    }
}

struct MeadowSceneView: UIViewRepresentable {
    let meadow: MeadowScene
    let shot: RangeShot?
    let elapsed: Double
    let power: Double
    let aim: Double
    var swingAngle: Double = 0
    var handedness: Handedness = .right

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
        meadow.update(shot: shot, elapsed: elapsed, power: power, aim: aim, swingAngle: swingAngle, handedness: handedness)
    }
}
