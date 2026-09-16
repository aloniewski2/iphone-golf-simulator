import SceneKit
import SwiftUI

/// The 3D hole: built from `Hole` data so what you see is exactly what the lie checks use.
/// Course yards map to scene units one-to-one with `x` right and distance along `-z`.
@MainActor
final class CourseScene {
    let scene = SCNScene()
    let camera = SCNNode()
    private let courseNode = SCNNode()
    /// Sits on the ball and faces the pin; carries the golfer, aim line and impact flash.
    private let stance = SCNNode()
    private let ball = SCNNode()
    private let shadow = SCNNode()
    private let trail = SCNNode()
    private let aimLine = SCNNode()
    private let impact = SCNNode()
    private let golfer = Golfer()
    private(set) var hole: Hole?
    private var lastShot: RangeShot?
    private var lastElapsed = 0.0

    private static let sky = UIColor(red: 0.65, green: 0.84, blue: 0.88, alpha: 1)
    private static let outOfBounds = UIColor(red: 0.15, green: 0.33, blue: 0.24, alpha: 1)
    private static let rough = UIColor(red: 0.22, green: 0.47, blue: 0.30, alpha: 1)
    private static let fairway = UIColor(red: 0.33, green: 0.63, blue: 0.37, alpha: 1)
    private static let green = UIColor(red: 0.45, green: 0.78, blue: 0.43, alpha: 1)
    private static let sand = UIColor(red: 0.90, green: 0.81, blue: 0.59, alpha: 1)
    private static let water = UIColor(red: 0.16, green: 0.52, blue: 0.78, alpha: 1)

    init() {
        scene.background.contents = Self.sky
        scene.fogColor = Self.sky
        scene.fogStartDistance = 320
        scene.fogEndDistance = 700
        camera.camera = SCNCamera()
        camera.camera?.fieldOfView = 58
        camera.camera?.zFar = 1000
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
        scene.rootNode.addChildNode(courseNode)

        ball.geometry = SCNSphere(radius: 0.55)
        ball.geometry?.firstMaterial?.diffuse.contents = UIColor.white
        ball.geometry?.firstMaterial?.emission.contents = UIColor(white: 0.15, alpha: 1)
        scene.rootNode.addChildNode(ball)
        shadow.geometry = SCNCylinder(radius: 0.9, height: 0.025)
        shadow.geometry?.firstMaterial?.diffuse.contents = UIColor.black.withAlphaComponent(0.25)
        scene.rootNode.addChildNode(shadow)
        scene.rootNode.addChildNode(trail)
        scene.rootNode.addChildNode(stance)
        stance.addChildNode(golfer.node)
        stance.addChildNode(aimLine)
        impact.geometry = SCNSphere(radius: 1)
        impact.geometry?.firstMaterial?.diffuse.contents = UIColor.systemYellow
        impact.geometry?.firstMaterial?.emission.contents = UIColor.systemYellow
        impact.position = SCNVector3(0, 0.6, 0)
        stance.addChildNode(impact)
        for i in 1...10 {
            let dot = SCNNode(geometry: SCNSphere(radius: 0.18))
            dot.geometry?.firstMaterial?.diffuse.contents = UIColor.white.withAlphaComponent(0.75)
            dot.position = SCNVector3(0, 0.35, -Float(i * 3))
            aimLine.addChildNode(dot)
        }
    }

    /// Rebuilds the terrain for `hole`. Cheap enough to call once per hole.
    func load(_ hole: Hole) {
        guard self.hole != hole else { return }
        self.hole = hole
        lastShot = nil
        trail.childNodes.forEach { $0.removeFromParentNode() }
        courseNode.childNodes.forEach { $0.removeFromParentNode() }

        let pin = hole.pin
        let span = max(hole.length, 150)
        add(SCNBox(width: 900, height: 1, length: span + 700, chamferRadius: 0), Self.outOfBounds,
            at: SCNVector3(Float(pin.x / 2), -1.2, -Float(span / 2)))

        let roughWidth = hole.fairwayWidth + Hole.roughWidth * 2
        for (a, b) in zip(hole.centerline, hole.centerline.dropFirst()) {
            ribbon(from: a, to: b, width: roughWidth, color: Self.rough, y: -0.62)
            ribbon(from: a, to: b, width: hole.fairwayWidth, color: Self.fairway, y: -0.45)
        }
        for joint in hole.centerline {
            disc(radius: roughWidth / 2, color: Self.rough, at: joint, y: -0.62)
            disc(radius: hole.fairwayWidth / 2, color: Self.fairway, at: joint, y: -0.45)
        }
        add(SCNBox(width: 10, height: 0.3, length: 8, chamferRadius: 0.4), UIColor(red: 0.28, green: 0.58, blue: 0.34, alpha: 1),
            at: world(hole.tee, y: -0.25))
        disc(radius: hole.greenRadius, color: Self.green, at: pin, y: -0.28)

        for hazard in hole.hazards {
            let point = CoursePoint(x: hazard.x, d: hazard.distance)
            switch hazard.kind {
            case .bunker:
                let shape = SCNSphere(radius: 1)
                shape.segmentCount = 32
                let node = add(shape, Self.sand, at: world(point, y: -0.12))
                node.scale = SCNVector3(hazard.width / 2, 0.14, hazard.length / 2)
            case .water:
                let shape = SCNCylinder(radius: 1, height: 0.1)
                shape.radialSegmentCount = 48
                shape.firstMaterial?.specular.contents = UIColor.white
                shape.firstMaterial?.shininess = 0.6
                let node = add(shape, Self.water, at: world(point, y: -0.15))
                node.scale = SCNVector3(hazard.width / 2, 1, hazard.length / 2)
            }
        }

        add(SCNCylinder(radius: 0.13, height: 8), .white, at: world(pin, y: 4))
        add(SCNBox(width: 4, height: 2.2, length: 0.12, chamferRadius: 0.1), .systemOrange,
            at: SCNVector3(Float(pin.x + 2), 7.1, -Float(pin.d)))
        disc(radius: 0.5, color: UIColor(white: 0.08, alpha: 1), at: pin, y: -0.16)

        plantTrees(along: hole)
        for i in 0..<6 {
            let hill = SCNSphere(radius: 60)
            hill.segmentCount = 12
            let node = add(hill, UIColor(red: 0.24, green: 0.43, blue: 0.38, alpha: 1),
                           at: SCNVector3(Float(pin.x) + Float(i * 90 - 225), -12, -Float(pin.d + 200)))
            node.scale = SCNVector3(1.4, 0.9, 1)
        }
    }

    /// `ball` and `heading` describe the ball at rest; while `shot` is set the flight drives the
    /// ball and camera. `swingAngle` is the live swing arc in degrees (0 at address, positive back).
    func update(
        ball rest: CoursePoint,
        heading: Double,
        distanceToPin: Double,
        shot: RangeShot?,
        elapsed: Double,
        aim: Double,
        swingAngle: Double = 0,
        handedness: Handedness = .right
    ) {
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

        let origin = shot?.origin ?? rest
        let frameHeading = shot?.heading ?? heading
        let geometry = ShotGeometry(origin: origin, heading: frameHeading)
        stance.position = world(origin, y: 0)
        stance.eulerAngles.y = Float(-frameHeading * .pi / 180)

        // Short shots get a lower, closer camera so a putt still fills the screen.
        // During a flight, frame from where the shot started so the camera doesn't lurch when it lands.
        let framingDistance = shot.flatMap { shot in hole.map { shot.origin.distance(to: $0.pin) } } ?? distanceToPin
        let scale = min(max(framingDistance / 120, 0.3), 1)
        let local = shot.map { $0.flight.position(at: min(elapsed, $0.duration)) }
            ?? FlightPoint(lateralYards: 0, heightYards: 0, distanceYards: 0)
        let point = geometry.world(local)
        ball.position = SCNVector3(point.lateralYards, point.heightYards + 0.6, -point.distanceYards)
        shadow.position = SCNVector3(point.lateralYards, 0.15, -point.distanceYards)
        let sunk = shot.map { $0.isHoled && elapsed >= $0.duration } ?? false
        ball.isHidden = sunk
        shadow.isHidden = sunk

        let cameraLocal = FlightPoint(
            lateralYards: local.lateralYards * 0.65 + 12 * scale,
            heightYards: 22 * scale + local.heightYards * 0.55,
            distanceYards: local.distanceYards * 0.86 - 38 * scale
        )
        let lookAhead = shot == nil ? min(max(framingDistance, 12), 95) : max(40 * scale, local.distanceYards + 14 * scale)
        let lookLocal = FlightPoint(lateralYards: local.lateralYards, heightYards: local.heightYards * 0.75, distanceYards: lookAhead)
        let cameraWorld = geometry.world(cameraLocal)
        let lookWorld = geometry.world(lookLocal)
        camera.position = SCNVector3(cameraWorld.lateralYards, cameraWorld.heightYards, -cameraWorld.distanceYards)
        camera.look(at: SCNVector3(lookWorld.lateralYards, max(0, lookWorld.heightYards), -lookWorld.distanceYards))

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

    private func world(_ point: CoursePoint, y: Float) -> SCNVector3 {
        SCNVector3(Float(point.x), y, -Float(point.d))
    }

    @discardableResult
    private func add(_ geometry: SCNGeometry, _ color: UIColor, at position: SCNVector3) -> SCNNode {
        geometry.firstMaterial?.diffuse.contents = color
        let node = SCNNode(geometry: geometry)
        node.position = position
        courseNode.addChildNode(node)
        return node
    }

    private func disc(radius: Double, color: UIColor, at point: CoursePoint, y: Float) {
        let shape = SCNCylinder(radius: radius, height: 0.2)
        shape.radialSegmentCount = 48
        add(shape, color, at: world(point, y: y))
    }

    private func ribbon(from a: CoursePoint, to b: CoursePoint, width: Double, color: UIColor, y: Float) {
        let length = a.distance(to: b)
        guard length > 0 else { return }
        let middle = CoursePoint(x: (a.x + b.x) / 2, d: (a.d + b.d) / 2)
        let node = add(SCNBox(width: width, height: 0.2, length: length, chamferRadius: 0), color, at: world(middle, y: y))
        node.eulerAngles.y = Float(-a.heading(to: b) * .pi / 180)
    }

    /// Two staggered lines of trees just beyond the rough mark out of bounds, plus a stand behind the green.
    private func plantTrees(along hole: Hole) {
        let edge = hole.fairwayWidth / 2 + Hole.roughWidth + 5
        var index = 0
        for (a, b) in zip(hole.centerline, hole.centerline.dropFirst()) {
            let length = a.distance(to: b)
            let ux = (b.x - a.x) / length, ud = (b.d - a.d) / length
            var along = 6.0
            while along < length {
                for side in [-1.0, 1.0] {
                    let offset = edge + Double(index * 7 % 16)
                    let point = CoursePoint(x: a.x + ux * along + ud * offset * side, d: a.d + ud * along - ux * offset * side)
                    if hole.distanceFromCenterline(point) >= edge - 0.5, point.distance(to: hole.pin) > hole.greenRadius + 12 {
                        tree(at: point, index: index)
                    }
                    index += 1
                }
                along += 13
            }
        }
        let pin = hole.pin
        let direction = hole.centerline[hole.centerline.count - 2].heading(to: pin) * .pi / 180
        for i in -4...4 {
            let back = hole.greenRadius + Hole.roughWidth + 10 + Double(abs(i) * 3)
            let side = Double(i) * 11
            let point = CoursePoint(
                x: pin.x + sin(direction) * back + cos(direction) * side,
                d: pin.d + cos(direction) * back - sin(direction) * side
            )
            tree(at: point, index: index + i + 4)
        }
    }

    private func tree(at point: CoursePoint, index: Int) {
        let height = CGFloat(8 + index % 8)
        add(SCNCylinder(radius: 0.65, height: 4), .brown, at: world(point, y: 2))
        let cone = SCNCone(topRadius: 0, bottomRadius: 4.5, height: height)
        cone.radialSegmentCount = 7
        add(cone, UIColor(red: 0.10, green: 0.31 + Double(index % 3) * 0.05, blue: 0.25, alpha: 1),
            at: world(point, y: Float(height / 2) + 3))
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

struct CourseSceneView: UIViewRepresentable {
    let scene: CourseScene
    let hole: Hole
    let ball: CoursePoint
    let heading: Double
    let distanceToPin: Double
    let shot: RangeShot?
    let elapsed: Double
    let aim: Double
    var swingAngle: Double = 0
    var handedness: Handedness = .right

    func makeUIView(context: Context) -> SCNView {
        let view = SCNView()
        view.scene = scene.scene
        view.pointOfView = scene.camera
        view.antialiasingMode = .multisampling4X
        view.preferredFramesPerSecond = 60
        view.isUserInteractionEnabled = false
        return view
    }

    func updateUIView(_ view: SCNView, context: Context) {
        scene.load(hole)
        scene.update(
            ball: ball, heading: heading, distanceToPin: distanceToPin, shot: shot,
            elapsed: elapsed, aim: aim, swingAngle: swingAngle, handedness: handedness
        )
    }
}
