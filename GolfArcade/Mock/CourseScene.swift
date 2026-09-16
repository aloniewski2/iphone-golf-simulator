import QuartzCore
import SceneKit
import SwiftUI

/// Everything the scene needs from the round, written by the SwiftUI view whenever it changes.
struct SceneInputs {
    var hole: Hole
    var ball: CoursePoint
    var heading: Double
    var distanceToPin: Double
    var lie: CourseLie
    var club: GolfClub
    var aim: Double
    var handedness: Handedness
    var shot: RangeShot?
    var isReplay: Bool
    var flightStart: Date?
    var pausedAt: Date?
    /// Live swing arc, for the canned golfer when there is no camera pose.
    var swingAngle: Double
    var bystanders: [Bystander]

    struct Bystander: Equatable {
        let id: UUID
        let colorIndex: Int
    }

    var onGreen: Bool { lie == .green || club == .putter }

    func elapsed(at date: Date) -> Double {
        guard let flightStart else { return 0 }
        return max(0, (pausedAt ?? date).timeIntervalSince(flightStart))
    }
}

/// The 3D hole: built from `Hole` data so what you see is exactly what the lie checks use.
/// Course yards map to scene units one-to-one with `x` right and distance along `-z`.
///
/// It animates itself on a display link: the avatar copies the player at camera rate, reactions
/// and knockdowns play out, and `ShotCameraDirector` moves the camera, without SwiftUI re-rendering.
@MainActor
final class CourseScene: NSObject {
    let scene = SCNScene()
    let camera = SCNNode()
    var inputs: SceneInputs?
    /// Called when the club knocks a bystander over.
    var onBystanderHit: (() -> Void)?

    private let courseNode = SCNNode()
    /// Sits on the ball and faces the pin; carries the golfer, tee, aim line, impact flash and bystanders.
    private let stance = SCNNode()
    private let ball = SCNNode()
    private let tee = SCNNode()
    private let shadow = SCNNode()
    private let trail = SCNNode()
    private let aimLine = SCNNode()
    private let impact = SCNNode()
    private let golfer = AvatarRig(shirt: UIColor(red: 0.95, green: 0.45, blue: 0.3, alpha: 1))
    private(set) var hole: Hole?

    private var displayLink: CADisplayLink?
    private var lastTick: CFTimeInterval?
    private var lastShot: RangeShot?
    private var reaction: AvatarAnimations.Reaction?
    private var landingTime: Double?
    private var cameraStage: ShotCameraDirector.Stage?
    private var cameraLook = simd_float3.zero

    // Copying the player.
    private var retargeter: PoseRetargeter?
    private var livePose: BodyPose3D?
    private var livePoseTime: CFTimeInterval = 0
    private var recorder = SwingRecorder()
    private var cannedAngle = 0.0
    private var cannedLaunchAngle = 0.0

    // Other players standing around.
    private var bystanderRigs: [UUID: AvatarRig] = [:]
    private var bystanderOrder: [SceneInputs.Bystander] = []
    private var knockdowns: [UUID: (start: CFTimeInterval, push: simd_float3)] = [:]
    private var contact = ClubContact()

    private static let sky = UIColor(red: 0.65, green: 0.84, blue: 0.88, alpha: 1)
    private static let outOfBounds = UIColor(red: 0.15, green: 0.33, blue: 0.24, alpha: 1)
    private static let rough = UIColor(red: 0.22, green: 0.47, blue: 0.30, alpha: 1)
    private static let fairway = UIColor(red: 0.33, green: 0.63, blue: 0.37, alpha: 1)
    private static let green = UIColor(red: 0.45, green: 0.78, blue: 0.43, alpha: 1)
    private static let sand = UIColor(red: 0.90, green: 0.81, blue: 0.59, alpha: 1)
    private static let water = UIColor(red: 0.16, green: 0.52, blue: 0.78, alpha: 1)

    override init() {
        super.init()
        scene.background.contents = Self.sky
        scene.fogColor = Self.sky
        scene.fogStartDistance = 320
        scene.fogEndDistance = 700
        camera.camera = SCNCamera()
        camera.camera?.fieldOfView = 55
        camera.camera?.zNear = 0.3
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
        tee.geometry = SCNCylinder(radius: 0.09, height: 0.5)
        tee.geometry?.firstMaterial?.diffuse.contents = UIColor.white
        tee.position = SCNVector3(0, 0.1, 0)
        stance.addChildNode(tee)
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

    // MARK: - Lifecycle

    func start() {
        guard displayLink == nil else { return }
        let link = CADisplayLink(target: self, selector: #selector(step(_:)))
        link.preferredFrameRateRange = CAFrameRateRange(minimum: 30, maximum: 60, preferred: 60)
        link.add(to: .main, forMode: .common)
        displayLink = link
    }

    func stop() {
        displayLink?.invalidate()
        displayLink = nil
        lastTick = nil
    }

    // MARK: - Copying the player

    /// Starts copying a new player. Pass nil when the swing input has no body to copy.
    func configurePlayer(calibration: PlayerCalibration?, handedness: Handedness, frameAspect: CGFloat) {
        retargeter = calibration.map { PoseRetargeter(calibration: $0, handedness: handedness, frameAspect: frameAspect) }
        livePose = nil
        recorder.reset()
        contact.reset()
    }

    /// Feeds one camera frame. `swingAngle` sets the club's wrist hinge.
    func ingest(_ frame: PoseFrame?, swingAngle: Double, frameAspect: CGFloat) {
        guard var retargeter else { return }
        let now = CACurrentMediaTime()
        retargeter.frameAspect = frameAspect
        let pose = retargeter.update(frame, swingAngle: swingAngle, at: now)
        self.retargeter = retargeter
        guard frame != nil else { return }
        livePose = pose
        livePoseTime = now
        recorder.record(pose, at: now)
    }

    /// Rebuilds the terrain for `hole`. Cheap enough to call once per hole.
    func load(_ hole: Hole) {
        guard self.hole != hole else { return }
        self.hole = hole
        lastShot = nil
        cameraStage = nil
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

    // MARK: - Frame

    @objc private func step(_ link: CADisplayLink) {
        guard let inputs else { return }
        let now = CACurrentMediaTime()
        let dt = Float(min(lastTick.map { now - $0 } ?? 1.0 / 60, 0.1))
        lastTick = now
        load(inputs.hole)

        let shot = inputs.shot
        let elapsed = inputs.elapsed(at: Date())
        if shot != lastShot {
            trail.childNodes.forEach { $0.removeFromParentNode() }
            if let shot {
                reaction = .classify(shot)
                landingTime = ShotCameraDirector.landingTime(of: shot)
                if !inputs.isReplay || recorder.impactTime == nil { recorder.markImpact(at: now - elapsed) }
                cannedLaunchAngle = inputs.isReplay ? 150 : max(cannedAngle, 60)
                for i in 0..<60 {
                    let point = shot.position(at: Double(i) / 59 * shot.duration)
                    let dot = SCNNode(geometry: SCNSphere(radius: 0.20))
                    dot.geometry?.firstMaterial?.diffuse.contents = UIColor.white.withAlphaComponent(0.65)
                    dot.position = SCNVector3(point.lateralYards, point.heightYards + 0.5, -point.distanceYards)
                    trail.addChildNode(dot)
                }
            } else {
                reaction = nil
                landingTime = nil
                recorder.reset()
            }
            lastShot = shot
        } else if inputs.isReplay, lastFlightStart != inputs.flightStart {
            // A replay restarts the same shot from a full backswing.
            cannedLaunchAngle = 150
        }
        lastFlightStart = inputs.flightStart

        let mirror: Float = inputs.handedness == .right ? 1 : -1
        let origin = shot?.origin ?? inputs.ball
        let heading = shot?.heading ?? inputs.heading
        stance.simdPosition = ShotCameraDirector.world(.zero, origin: origin, heading: heading)
        stance.eulerAngles.y = Float(-heading * .pi / 180)
        golfer.setMirrored(mirror < 0)
        golfer.node.simdPosition = simd_float3(-3.2 * mirror, 0, 0)

        let pose = golferPose(shot: shot, elapsed: elapsed, isReplay: inputs.isReplay, swingAngle: inputs.swingAngle, now: now)
        golfer.apply(pose)
        updateBystanders(inputs, golferPose: pose, mirror: mirror, now: now)

        // Ball.
        let point = shot?.position(at: elapsed) ?? FlightPoint(lateralYards: inputs.ball.x, heightYards: 0, distanceYards: inputs.ball.d)
        let teed = shot == nil && inputs.lie == .tee
        ball.position = SCNVector3(point.lateralYards, point.heightYards + (teed ? 0.9 : 0.6), -point.distanceYards)
        shadow.position = SCNVector3(point.lateralYards, 0.15, -point.distanceYards)
        let sunk = shot.map { $0.isHoled && elapsed >= $0.duration } ?? false
        ball.isHidden = sunk
        shadow.isHidden = sunk
        tee.isHidden = inputs.lie != .tee && shot?.origin != inputs.hole.tee

        let burst = min(max(elapsed / 0.23, 0), 1)
        impact.isHidden = shot == nil || elapsed > 0.23
        impact.opacity = CGFloat((1 - burst) * 0.7)
        impact.scale = SCNVector3(1 + burst * 3, 1 + burst * 3, 1 + burst * 3)
        aimLine.isHidden = shot != nil
        aimLine.eulerAngles.y = Float(-inputs.aim * .pi / 180)
        moveCamera(inputs, shot: shot, elapsed: elapsed, dt: dt)
        if let shot {
            // Dots appear behind the ball; ones right at the chase camera would wash out the view.
            let eye = camera.simdPosition
            let ballPosition = ball.simdPosition
            for (i, dot) in trail.childNodes.enumerated() {
                dot.isHidden = Double(i) / 59 * shot.duration > elapsed
                    || simd_distance(dot.simdPosition, eye) < 4
                    || simd_distance(dot.simdPosition, ballPosition) < 2.5
            }
        }
    }

    private var lastFlightStart: Date?

    private func golferPose(shot: RangeShot?, elapsed: Double, isReplay: Bool, swingAngle: Double, now: CFTimeInterval) -> BodyPose3D {
        let live = now - livePoseTime < 0.5 ? livePose : nil
        guard let shot else {
            cannedAngle += (min(max(swingAngle, -150), 150) - cannedAngle) * 0.35
            return live ?? AvatarAnimations.swingArc(degrees: cannedAngle)
        }
        let followThrough = 0.6
        func follow(_ t: Double) -> BodyPose3D {
            if isReplay, let recorded = recorder.pose(atImpactOffset: t) { return recorded }
            if !isReplay, let live { return live }
            // Canned downswing (0.3 s) into a held finish.
            let u = min(t / 0.3, 1)
            let angle = t < 0.3 ? cannedLaunchAngle + (-150 - cannedLaunchAngle) * (u * u) : -150
            return AvatarAnimations.swingArc(degrees: angle)
        }
        if elapsed < followThrough { return follow(elapsed) }
        let reactionTime = elapsed - followThrough
        guard let reaction, reactionTime < AvatarAnimations.reactionLength else {
            return live ?? AvatarAnimations.address
        }
        let target = AvatarAnimations.reaction(reaction, time: reactionTime)
        var pose = BodyPose3D.lerp(follow(followThrough), target, smoothstep(0, 0.25, Float(reactionTime)))
        let fadeOut = Float(AvatarAnimations.reactionLength - reactionTime)
        if fadeOut < 0.4 {
            pose = BodyPose3D.lerp(live ?? AvatarAnimations.address, pose, fadeOut / 0.4)
        }
        return pose
    }

    private func updateBystanders(_ inputs: SceneInputs, golferPose: BodyPose3D, mirror: Float, now: CFTimeInterval) {
        if inputs.bystanders != bystanderOrder {
            let ids = Set(inputs.bystanders.map(\.id))
            for (id, rig) in bystanderRigs where !ids.contains(id) {
                rig.node.removeFromParentNode()
                bystanderRigs[id] = nil
            }
            for bystander in inputs.bystanders where bystanderRigs[bystander.id] == nil {
                let rig = AvatarRig(shirt: Self.playerColor(bystander.colorIndex))
                stance.addChildNode(rig.node)
                bystanderRigs[bystander.id] = rig
            }
            bystanderOrder = inputs.bystanders
        }

        let golferPosition = simd_float3(-3.2 * mirror, 0, 0)
        // First bystander stands on the lead side inside club reach; the rest wait behind the golfer.
        let spots = [simd_float3(-0.6 * mirror, 0, -4.8), simd_float3(-4.5 * mirror, 0, -2.2), simd_float3(-4.5 * mirror, 0, 2.2)]
        var targets: [ClubContact.Target] = []
        for (index, bystander) in bystanderOrder.enumerated() {
            guard let rig = bystanderRigs[bystander.id] else { continue }
            let spot = golferPosition + spots[min(index, spots.count - 1)] + simd_float3(0, 0, Float(max(0, index - 2)) * 2.5)
            let toGolfer = golferPosition - spot
            let facing = atan2(-toGolfer.z, toGolfer.x)
            rig.node.simdPosition = spot
            rig.node.eulerAngles.y = facing
            let seed = Double(index) * 1.3
            if let knock = knockdowns[bystander.id] {
                let t = now - knock.start
                if t < AvatarAnimations.knockdownLength {
                    // Push into the bystander's own frame.
                    let c = cos(facing), s = sin(facing)
                    let push = simd_float3(knock.push.x * c - knock.push.z * s, 0, knock.push.x * s + knock.push.z * c)
                    rig.apply(AvatarAnimations.knockdown(time: t, push: push, seed: seed))
                    continue
                }
                knockdowns[bystander.id] = nil
            }
            rig.apply(AvatarAnimations.idle(time: now, seed: seed))
            targets.append(ClubContact.Target(id: bystander.id, base: spot + simd_float3(0, 0.8, 0), top: spot + simd_float3(0, 5.2, 0), radius: 1.0))
        }

        // The club in the stance frame: the golfer rig is only moved and mirrored.
        func stancePoint(_ p: simd_float3) -> simd_float3 { simd_float3(p.x * mirror, p.y, p.z) + golferPosition }
        guard !golferPose.clubDropped else { return }
        let hits = contact.update(hands: stancePoint(golferPose.handCenter), head: stancePoint(golferPose.clubHead), at: now, targets: targets)
        for hit in hits {
            knockdowns[hit.id] = (now, hit.push)
            onBystanderHit?()
        }
    }

    private func moveCamera(_ inputs: SceneInputs, shot: RangeShot?, elapsed: Double, dt: Float) {
        let framing = ShotCameraDirector.shot(ShotCameraDirector.Inputs(
            ball: inputs.ball, heading: inputs.heading, aim: inputs.aim, distanceToPin: inputs.distanceToPin,
            onGreen: inputs.onGreen && shot == nil, handedness: inputs.handedness, shot: shot, elapsed: elapsed,
            reaction: reaction, landingTime: landingTime
        ))
        let cut = cameraStage == nil || (framing.stage != cameraStage && [.hero, .chase, .address, .green].contains(framing.stage))
        cameraStage = framing.stage
        if cut {
            camera.simdPosition = framing.position
            cameraLook = framing.lookAt
            camera.camera?.fieldOfView = CGFloat(framing.fieldOfView)
        } else {
            let k = 1 - exp(-framing.damping * dt)
            camera.simdPosition = simd_mix(camera.simdPosition, framing.position, simd_float3(repeating: k))
            cameraLook = simd_mix(cameraLook, framing.lookAt, simd_float3(repeating: k))
            let fov = Float(camera.camera?.fieldOfView ?? 55)
            camera.camera?.fieldOfView = CGFloat(fov + (framing.fieldOfView - fov) * k)
        }
        camera.simdLook(at: cameraLook, up: simd_float3(0, 1, 0), localFront: simd_float3(0, 0, -1))
    }

    private static func playerColor(_ index: Int) -> UIColor {
        [UIColor.systemMint, .systemOrange, .systemYellow, .systemPink][index % 4]
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

struct CourseSceneView: UIViewRepresentable {
    let scene: CourseScene
    let inputs: SceneInputs

    func makeCoordinator() -> CourseScene { scene }

    func makeUIView(context: Context) -> SCNView {
        let view = SCNView()
        view.scene = scene.scene
        view.pointOfView = scene.camera
        view.antialiasingMode = .multisampling4X
        view.preferredFramesPerSecond = 60
        view.rendersContinuously = true
        view.isUserInteractionEnabled = false
        scene.inputs = inputs
        scene.start()
        return view
    }

    func updateUIView(_ view: SCNView, context: Context) {
        scene.inputs = inputs
    }

    static func dismantleUIView(_ view: SCNView, coordinator: CourseScene) {
        coordinator.stop()
    }
}
