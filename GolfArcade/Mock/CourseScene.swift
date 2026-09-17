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
    var preview: RangeShot? = nil

    struct Bystander: Equatable {
        let id: UUID
        let colorIndex: Int
    }

    var onGreen: Bool { lie == .green || club == .putter }

    func elapsed(at date: Date) -> Double {
        guard let flightStart else { return 0 }
        return max(0, (pausedAt ?? date).timeIntervalSince(flightStart)) * CourseRound.flightTimeScale
    }
}

/// The 3D hole: built from `Hole` data so what you see is exactly what the lie checks use.
/// Course yards map to scene units one-to-one with `x` right and distance along `-z`.
///
/// It animates itself on a display link: the avatar copies the player at camera rate, reactions
/// and knockdowns play out, and `ShotCameraDirector` moves the camera, without SwiftUI re-rendering.
@MainActor
final class CourseScene: NSObject, ObservableObject {
    #if DEBUG
    private(set) static var initializationCount = 0
    #endif
    let scene = SCNScene()
    let camera = SCNNode()
    var inputs: SceneInputs?
    /// Called when the club knocks a bystander over.
    var onBystanderHit: (() -> Void)?
    /// Called once when the ball comes down, and once more if it drops in the cup.
    var onLanding: ((RangeAudio.Landing) -> Void)?
    private var landingAnnounced = false
    private var cupAnnounced = false

    private let courseNode = SCNNode()
    /// Sits on the ball and faces the intended landing line; rig-space children scale to yards.
    private let stance = SCNNode()
    private let ball = SCNNode()
    private let tee = SCNNode()
    private let shadow = SCNNode()
    private let ballLocator = SCNNode()
    private let trail = SCNNode()
    private let aimLine = SCNNode()
    private let trajectoryGuide = SCNNode()
    private var trajectoryDots: [SCNNode] = []
    private let projectedLanding = SCNNode()
    private let pinBeacon = SCNNode()
    private let pinBeam = SCNNode()
    private let pinBadge = SCNNode()
    private var lastPreview: RangeShot?
    private let impact = SCNNode()
    private var pinFlag: SCNNode?
    private let golfer = AvatarRig(shirt: UIColor(red: 0.95, green: 0.45, blue: 0.3, alpha: 1))
    private(set) var hole: Hole?
    /// Dots on the putting surface that drift downhill, faster where it is steeper: the read.
    private let greenGrid = SCNNode()
    private var greenDots: [(node: SCNNode, base: CoursePoint, flow: simd_float3, speed: Float)] = []
    private static let greenGridSpacing = 2.0

    /// Height of the ground at `point`, in yards.
    private func ground(_ point: CoursePoint) -> Float {
        Float(hole?.terrain.elevation(at: point) ?? 0)
    }

    private var displayLink: CADisplayLink?
    private var lastTick: CFTimeInterval?
    private var lastShot: RangeShot?
    private var reaction: AvatarAnimations.Reaction?
    private var landingTime: Double?
    private var cameraStage: ShotCameraDirector.Stage?
    private var cameraLook = simd_float3.zero

    // Copying the player.
    private var retargeter: PoseRetargeter?
    private var cameraPoseFilter = CameraAvatarPoseFilter()
    private var livePose: BodyPose3D?
    private var livePoseTime: CFTimeInterval = 0
    /// The pose before `livePose`, so frames can be drawn between camera deliveries.
    private var previousLivePose: BodyPose3D?
    private var previousLivePoseTime: CFTimeInterval = 0
    private var recorder = SwingRecorder()
    private var cannedAngle = 0.0
    private var cannedAngleTime: CFTimeInterval?
    private var cannedLaunchAngle = 0.0

    /// Where the golfer's pose comes from this frame. Switching sources crossfades so the
    /// avatar never snaps from one body to another.
    enum PoseSource: Equatable { case live, waiting, canned, recorded }
    private(set) var poseSource: PoseSource = .canned
    private var presentedPose: BodyPose3D?
    private var crossfade: (from: BodyPose3D, start: CFTimeInterval)?
    private static let crossfadeLength = 0.28

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
        #if DEBUG
        Self.initializationCount += 1
        #endif
        scene.background.contents = CourseArt.sky
        scene.fogColor = Self.sky
        scene.fogStartDistance = 220
        scene.fogEndDistance = 800
        camera.camera = SCNCamera()
        camera.camera?.fieldOfView = 55
        camera.camera?.zNear = 0.3
        camera.camera?.zFar = 1000
        camera.camera?.wantsHDR = true
        camera.camera?.wantsExposureAdaptation = false
        camera.camera?.exposureOffset = 0.15
        camera.camera?.bloomIntensity = 0.08
        camera.camera?.bloomThreshold = 1.2
        scene.rootNode.addChildNode(camera)
        let sun = SCNNode()
        sun.light = SCNLight()
        sun.light?.type = .directional
        sun.light?.intensity = 1050
        sun.light?.color = UIColor(red: 1, green: 0.94, blue: 0.82, alpha: 1)
        sun.light?.castsShadow = true
        sun.light?.shadowMode = .deferred
        sun.light?.shadowMapSize = CGSize(width: 2048, height: 2048)
        sun.light?.shadowSampleCount = 8
        sun.light?.shadowRadius = 3
        sun.light?.shadowColor = UIColor(red: 0.12, green: 0.20, blue: 0.25, alpha: 0.30)
        sun.light?.maximumShadowDistance = 65
        sun.light?.orthographicScale = 45
        sun.eulerAngles = SCNVector3(-0.9, -0.65, 0)
        scene.rootNode.addChildNode(sun)
        let ambient = SCNNode()
        ambient.light = SCNLight()
        ambient.light?.type = .ambient
        ambient.light?.intensity = 430
        ambient.light?.color = UIColor(red: 0.79, green: 0.88, blue: 1, alpha: 1)
        scene.rootNode.addChildNode(ambient)
        scene.rootNode.addChildNode(courseNode)
        buildPinBeacon()

        ball.name = "visibilityAssistedBall"
        ball.geometry = SCNSphere(radius: AvatarSize.visibleBallRadius)
        (ball.geometry as? SCNSphere)?.segmentCount = 32
        ball.geometry?.firstMaterial?.diffuse.contents = UIColor.white
        ball.geometry?.firstMaterial?.emission.contents = UIColor(white: 0.15, alpha: 1)
        ball.geometry?.firstMaterial?.roughness.contents = 0.38
        ball.geometry?.firstMaterial?.lightingModel = .physicallyBased
        scene.rootNode.addChildNode(ball)
        shadow.geometry = SCNCylinder(radius: 0.045, height: 0.002)
        shadow.geometry?.firstMaterial?.diffuse.contents = UIColor.black.withAlphaComponent(0.25)
        scene.rootNode.addChildNode(shadow)
        ballLocator.name = "ballLocatorNotBallGeometry"
        ballLocator.geometry = SCNTorus(ringRadius: 0.12, pipeRadius: 0.006)
        ballLocator.geometry?.firstMaterial?.diffuse.contents = UIColor.white.withAlphaComponent(0.65)
        ballLocator.geometry?.firstMaterial?.lightingModel = .constant
        ballLocator.castsShadow = false
        scene.rootNode.addChildNode(ballLocator)
        scene.rootNode.addChildNode(trail)
        scene.rootNode.addChildNode(stance)
        stance.name = "yardScaleStance"
        stance.simdScale = simd_float3(repeating: AvatarSize.courseScale)
        stance.addChildNode(golfer.node)
        tee.geometry = SCNCylinder(radius: 0.018, height: 0.20)
        tee.geometry?.firstMaterial?.diffuse.contents = UIColor.white
        tee.position = SCNVector3(0, -0.075, 0)
        stance.addChildNode(tee)
        stance.addChildNode(aimLine)
        trajectoryGuide.name = "predictedTrajectory"
        scene.rootNode.addChildNode(trajectoryGuide)
        for _ in 0..<56 {
            let dot = SCNNode(geometry: SCNSphere(radius: 0.09))
            dot.geometry?.firstMaterial?.diffuse.contents = UIColor.systemMint.withAlphaComponent(0.85)
            dot.geometry?.firstMaterial?.lightingModel = .constant
            dot.castsShadow = false
            trajectoryGuide.addChildNode(dot)
            trajectoryDots.append(dot)
        }
        projectedLanding.name = "predictedLanding"
        projectedLanding.geometry = SCNTorus(ringRadius: 1.2, pipeRadius: 0.08)
        projectedLanding.geometry?.firstMaterial?.diffuse.contents = UIColor.systemMint
        projectedLanding.geometry?.firstMaterial?.lightingModel = .constant
        projectedLanding.castsShadow = false
        trajectoryGuide.addChildNode(projectedLanding)
        impact.geometry = SCNSphere(radius: 1)
        impact.geometry?.firstMaterial?.diffuse.contents = UIColor.systemYellow
        impact.geometry?.firstMaterial?.emission.contents = UIColor.systemYellow
        impact.position = SCNVector3(0, 0.6, 0)
        stance.addChildNode(impact)
        for i in 1...10 {
            let dot = SCNNode(geometry: SCNCylinder(radius: 0.05, height: 0.01))
            dot.geometry?.firstMaterial?.diffuse.contents = UIColor.white.withAlphaComponent(0.75)
            dot.position = SCNVector3(0, -0.10, -Float(i * 6))
            aimLine.addChildNode(dot)
        }
    }

    // MARK: - Lifecycle

    func start() {
        guard displayLink == nil else { return }
        let link = CADisplayLink(target: self, selector: #selector(step(_:)))
        link.preferredFrameRateRange = CAFrameRateRange(minimum: 60, maximum: 120, preferred: 120)
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
        cameraPoseFilter = CameraAvatarPoseFilter()
        livePose = calibration == nil ? nil : .cameraWaiting
        livePoseTime = CACurrentMediaTime()
        previousLivePose = nil
        recorder.reset()
        contact.reset()
    }

    /// Feeds one camera frame. `swingAngle` sets the club's wrist hinge.
    func ingest(_ frame: PoseFrame?, swingAngle: Double, frameAspect: CGFloat, virtualClub: VirtualClubState? = nil, positionLocked: Bool = false) {
        guard var retargeter else { return }
        let now = CACurrentMediaTime()
        retargeter.frameAspect = frameAspect
        let measuredPose = retargeter.updateCamera(frame, club: virtualClub, positionLocked: positionLocked,
                                          swingAngle: swingAngle, at: now)
        let pose = cameraPoseFilter.update(measuredPose, frame: frame, at: now)
        self.retargeter = retargeter
        previousLivePose = livePose
        previousLivePoseTime = livePoseTime
        livePose = pose
        livePoseTime = now
        recorder.record(pose, at: now)
    }

    /// The live pose drawn one camera interval behind, so the avatar glides between the 30 Hz
    /// deliveries instead of stepping at them. A stalled camera simply holds the last pose.
    private func interpolatedLivePose(at now: CFTimeInterval) -> BodyPose3D? {
        guard let livePose else { return nil }
        guard let previous = previousLivePose, livePoseTime > previousLivePoseTime, now >= livePoseTime else { return livePose }
        let interval = livePoseTime - previousLivePoseTime
        guard interval > 1.0 / 120, interval < 0.2 else { return livePose }
        let delay = min(max(interval, 1.0 / 60), 0.05)
        let t = (now - delay - previousLivePoseTime) / interval
        return BodyPose3D.lerp(previous, livePose, Float(min(max(t, 0), 1)))
    }

    /// Crossfades between pose sources; within a source the pose is passed straight through.
    private func present(_ pose: BodyPose3D, from source: PoseSource, now: CFTimeInterval) -> BodyPose3D {
        if source != poseSource, let presentedPose {
            crossfade = (presentedPose, now)
            poseSource = source
        }
        var result = pose
        if let crossfade {
            let u = smoothstep(0, Float(Self.crossfadeLength), Float(now - crossfade.start))
            if u >= 1 { self.crossfade = nil } else { result = BodyPose3D.lerp(crossfade.from, pose, u) }
        }
        presentedPose = result
        return result
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
        let landscape = SCNNode(geometry: CourseArt.landscape(hole))
        landscape.name = "sculptedLandscape"
        landscape.geometry?.materials = [CourseArt.roughMaterial]
        courseNode.addChildNode(landscape)

        let roughWidth = hole.fairwayWidth + Hole.roughWidth * 2
        for (a, b) in zip(hole.centerline, hole.centerline.dropFirst()) {
            turf(from: a, to: b, width: roughWidth, material: CourseArt.roughMaterial, y: -0.08)
            turf(from: a, to: b, width: hole.fairwayWidth + 2.2, material: CourseArt.fringeMaterial, y: -0.065)
            turf(from: a, to: b, width: hole.fairwayWidth, material: CourseArt.fairwayMaterial, y: -0.05)
        }
        add(SCNBox(width: 7, height: 0.04, length: 5, chamferRadius: 0.02), UIColor(red: 0.28, green: 0.58, blue: 0.34, alpha: 1),
            at: world(hole.tee, y: ground(hole.tee) - 0.065))
        turf(from: pin, to: pin, width: hole.greenRadius * 2 + 3, material: CourseArt.fringeMaterial, y: -0.045)
        turf(from: pin, to: pin, width: hole.greenRadius * 2, material: CourseArt.greenMaterial, y: -0.035)

        for hazard in hole.hazards {
            let point = CoursePoint(x: hazard.x, d: hazard.distance)
            switch hazard.kind {
            case .bunker:
                let shape = SCNSphere(radius: 1)
                shape.segmentCount = 32
                let node = add(shape, Self.sand, at: world(point, y: ground(point) - 0.035))
                node.scale = SCNVector3(hazard.width / 2, 0.01, hazard.length / 2)
                node.geometry?.materials = [CourseArt.sandMaterial]
                CourseArt.hazardEdge(hazard, parent: courseNode, y: ground(point))
            case .water:
                let shape = SCNCylinder(radius: 1, height: 0.01)
                shape.radialSegmentCount = 48
                shape.firstMaterial?.specular.contents = UIColor.white
                shape.firstMaterial?.shininess = 0.6
                let node = add(shape, Self.water, at: world(point, y: ground(point) - 0.03))
                node.scale = SCNVector3(hazard.width / 2, 1, hazard.length / 2)
                node.geometry?.materials = [CourseArt.waterMaterial]
                CourseArt.hazardEdge(hazard, parent: courseNode, y: ground(point))
            }
        }

        let pinGround = ground(pin)
        add(SCNCylinder(radius: 0.015, height: 2.4), .white, at: world(pin, y: pinGround + 1.17))
        let flag = add(CourseArt.flag(), UIColor(red: 0.98, green: 0.43, blue: 0.17, alpha: 1),
                       at: world(pin, y: pinGround + 2.28))
        flag.simdScale = simd_float3(repeating: 0.28)
        flag.name = "clothPinFlag"
        pinFlag = flag
        disc(radius: 0.059, color: UIColor(white: 0.08, alpha: 1), at: pin, y: pinGround - 0.132)
        buildGreenGrid(hole)

        plantTrees(along: hole)
        for i in 0..<6 {
            let hill = SCNSphere(radius: 60)
            hill.segmentCount = 32
            let node = add(hill, UIColor(red: 0.24, green: 0.43, blue: 0.38, alpha: 1),
                           at: SCNVector3(Float(pin.x) + Float(i * 90 - 225), -12, -Float(pin.d + 200)))
            node.scale = SCNVector3(1.4, 0.9, 1)
        }
        CourseArt.dress(hole, parent: courseNode)
    }

    // MARK: - Frame

    @objc private func step(_ link: CADisplayLink) {
        tick(now: CACurrentMediaTime())
    }

    #if DEBUG
    /// One display-link frame, for tests that check what the scene does over a shot.
    func stepForTesting(now: CFTimeInterval = CACurrentMediaTime()) { tick(now: now) }
    var presentedPoseForTesting: BodyPose3D { presentedPose ?? AvatarAnimations.address }
    #endif

    private func tick(now: CFTimeInterval) {
        guard let inputs else { return }
        let dt = Float(min(lastTick.map { now - $0 } ?? 1.0 / 60, 0.1))
        lastTick = now
        load(inputs.hole)
        pinFlag?.eulerAngles.y = Float(sin(now * 1.8) * 0.035)

        let shot = inputs.shot
        updateTrajectory(inputs.preview, visible: shot == nil)
        let elapsed = inputs.elapsed(at: Date())
        if shot != lastShot {
            trail.childNodes.forEach { $0.removeFromParentNode() }
            landingAnnounced = false
            cupAnnounced = false
            if let shot {
                reaction = .classify(shot)
                landingTime = ShotCameraDirector.landingTime(of: shot)
                if !inputs.isReplay || recorder.impactTime == nil { recorder.markImpact(at: now - elapsed) }
                cannedLaunchAngle = inputs.isReplay ? 150 : max(cannedAngle, 60)
                for i in 0..<60 {
                    let point = shot.position(at: Double(i) / 59 * shot.duration)
                    let dot = SCNNode(geometry: SCNSphere(radius: 0.065))
                    dot.geometry?.firstMaterial?.diffuse.contents = UIColor.white.withAlphaComponent(0.65)
                    let lift = ground(CoursePoint(x: point.lateralYards, d: point.distanceYards))
                    dot.position = SCNVector3(Float(point.lateralYards), Float(point.heightYards) + lift, -Float(point.distanceYards))
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

        let playerStance = GolferStance(handedness: inputs.handedness)
        let mirror = playerStance.mirror
        let origin = shot?.origin ?? inputs.ball
        let heading = shot?.heading ?? (inputs.heading + inputs.aim)
        stance.simdPosition = ShotCameraDirector.world(.zero, origin: origin, heading: heading) + simd_float3(0, ground(origin), 0)
        stance.eulerAngles.y = Float(-heading * .pi / 180)
        golfer.setMirrored(mirror < 0)
        golfer.node.simdPosition = playerStance.position

        let (raw, source) = golferPoseAndSource(shot: shot, elapsed: elapsed, isReplay: inputs.isReplay, swingAngle: inputs.swingAngle, now: now)
        let pose = present(raw, from: source, now: now)
        golfer.apply(pose)
        updateBystanders(inputs, golferPose: pose, mirror: mirror, now: now)

        // Ball.
        let point = shot?.position(at: elapsed) ?? FlightPoint(lateralYards: inputs.ball.x, heightYards: 0, distanceYards: inputs.ball.d)
        let teed = shot == nil && inputs.lie == .tee
        let visibleLie = inputs.hole.lie(at: CoursePoint(x: point.lateralYards, d: point.distanceYards))
        let lift = Double(ground(CoursePoint(x: point.lateralYards, d: point.distanceYards)))
        let restingHeight = lift + (visibleLie == .green ? -0.035 : visibleLie == .bunker || visibleLie == .water ? -0.025 : -0.05)
        ball.position = SCNVector3(point.lateralYards, point.heightYards + (teed ? lift + Double(AvatarSize.ball.y * AvatarSize.courseScale) : restingHeight + Double(AvatarSize.visibleBallRadius)), -point.distanceYards)
        shadow.position = SCNVector3(point.lateralYards, restingHeight + 0.003, -point.distanceYards)
        ballLocator.position = SCNVector3(point.lateralYards, restingHeight + 0.008, -point.distanceYards)
        let sunk = shot.map { $0.isHoled && elapsed >= $0.duration } ?? false
        if let shot, !inputs.isReplay || elapsed > 0 { announceLanding(shot, elapsed: elapsed, hole: inputs.hole, sunk: sunk) }
        ball.isHidden = sunk
        shadow.isHidden = sunk
        ballLocator.isHidden = sunk || (shot != nil && elapsed < (shot?.duration ?? 0))
        tee.isHidden = inputs.lie != .tee && shot?.origin != inputs.hole.tee

        let burst = min(max(elapsed / 0.23, 0), 1)
        impact.isHidden = shot == nil || shot?.strike == .miss || elapsed > 0.23
        impact.opacity = CGFloat((1 - burst) * 0.7)
        impact.scale = SCNVector3(1 + burst * 3, 1 + burst * 3, 1 + burst * 3)
        aimLine.isHidden = shot != nil
        aimLine.eulerAngles.y = 0
        updateGreenGrid(visible: shot == nil && inputs.onGreen, now: now)
        moveCamera(inputs, shot: shot, elapsed: elapsed, dt: dt)
        updatePinBeacon(pin: inputs.hole.pin, sunk: sunk)
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

    /// One landing sound as the ball first comes down (what it lands on decides which), and the
    /// cup when it drops. Putts only roll, so they are silent until the cup.
    private func announceLanding(_ shot: RangeShot, elapsed: Double, hole: Hole, sunk: Bool) {
        if !landingAnnounced, let landingTime, elapsed >= landingTime {
            landingAnnounced = true
            let point = shot.position(at: landingTime)
            let before = shot.position(at: max(0, landingTime - 1.0 / 30))
            let speed = hypot(point.lateralYards - before.lateralYards, point.distanceYards - before.distanceYards) * 30
            let spot = CoursePoint(x: point.lateralYards, d: point.distanceYards)
            switch hole.lie(at: spot) {
            case .water: onLanding?(.water)
            case .bunker: onLanding?(.sand)
            case let lie: onLanding?(.turf(lie, speed: speed))
            }
        }
        if sunk, !cupAnnounced {
            cupAnnounced = true
            onLanding?(.cup)
        }
    }

    func golferPose(shot: RangeShot?, elapsed: Double, isReplay: Bool, swingAngle: Double, now: CFTimeInterval) -> BodyPose3D {
        golferPoseAndSource(shot: shot, elapsed: elapsed, isReplay: isReplay, swingAngle: swingAngle, now: now).pose
    }

    func golferPoseAndSource(shot: RangeShot?, elapsed: Double, isReplay: Bool, swingAngle: Double, now: CFTimeInterval) -> (pose: BodyPose3D, source: PoseSource) {
        // Camera players remain the character throughout setup, backswing and follow-through.
        // A hit/miss reaction must never take over their body. Replays use the measured trace.
        let interpolated = interpolatedLivePose(at: now)
        if retargeter != nil, !isReplay {
            return interpolated.map { ($0, $0 == .cameraWaiting ? .waiting : .live) } ?? (.cameraWaiting, .waiting)
        }
        let live = now - livePoseTime < 0.5 ? interpolated : (retargeter == nil ? nil : .cameraWaiting)
        guard shot != nil else {
            // Frame-rate independent easing toward the input's arc.
            let dt = min(0.1, max(0, now - (cannedAngleTime ?? now)))
            cannedAngleTime = now
            cannedAngle += (min(max(swingAngle, -150), 150) - cannedAngle) * (1 - exp(-dt * 22))
            if let live { return (live, live == .cameraWaiting ? .waiting : .live) }
            return (AvatarAnimations.swingArc(degrees: cannedAngle), .canned)
        }
        let followThrough = 0.6
        var source = PoseSource.canned
        func follow(_ t: Double) -> BodyPose3D {
            if isReplay, let recorded = recorder.pose(atImpactOffset: t) { source = .recorded; return recorded }
            if !isReplay, let live { source = .live; return live }
            // Canned downswing (0.3 s) into a held finish.
            let u = min(t / 0.3, 1)
            let angle = t < 0.3 ? cannedLaunchAngle + (-150 - cannedLaunchAngle) * (u * u) : -150
            source = .canned
            return AvatarAnimations.swingArc(degrees: angle)
        }
        if elapsed < followThrough { return (follow(elapsed), source) }
        let reactionTime = elapsed - followThrough
        guard let reaction, reactionTime < AvatarAnimations.reactionLength else {
            return live.map { ($0, $0 == .cameraWaiting ? .waiting : .live) } ?? (AvatarAnimations.address, .canned)
        }
        let target = AvatarAnimations.reaction(reaction, time: reactionTime)
        var pose = BodyPose3D.lerp(follow(followThrough), target, smoothstep(0, 0.25, Float(reactionTime)))
        let fadeOut = Float(AvatarAnimations.reactionLength - reactionTime)
        if fadeOut < 0.4 {
            pose = BodyPose3D.lerp(live ?? AvatarAnimations.address, pose, fadeOut / 0.4)
        }
        return (pose, source)
    }

    private func updateTrajectory(_ preview: RangeShot?, visible: Bool) {
        trajectoryGuide.isHidden = !visible || preview == nil
        guard visible, let preview, preview != lastPreview else { return }
        lastPreview = preview
        let putting = preview.club == .putter
        for (index, dot) in trajectoryDots.enumerated() {
            let t = Double(index + 1) / Double(trajectoryDots.count)
            let point = preview.position(at: preview.duration * t)
            let lift = Double(ground(CoursePoint(x: point.lateralYards, d: point.distanceYards)))
            dot.position = SCNVector3(point.lateralYards, lift + max(0.06, point.heightYards), -point.distanceYards)
            // A putt's line is read close up: even small dots all the way along.
            dot.simdScale = simd_float3(repeating: putting ? 0.22 : Float(0.5 + t * 1.7))
        }
        let end = preview.position(at: preview.duration)
        projectedLanding.position = SCNVector3(end.lateralYards, Double(ground(CoursePoint(x: end.lateralYards, d: end.distanceYards))) + 0.06, -end.distanceYards)
        projectedLanding.simdScale = simd_float3(repeating: preview.club == .putter ? 0.18 : 1)
    }

    private func buildPinBeacon() {
        pinBeacon.name = "holeNavigationBeacon"
        scene.rootNode.addChildNode(pinBeacon)
        func navigationMaterial(_ color: UIColor) -> SCNMaterial {
            let material = SCNMaterial()
            material.diffuse.contents = color
            material.lightingModel = .constant
            material.readsFromDepthBuffer = false
            material.writesToDepthBuffer = false
            material.isDoubleSided = true
            return material
        }
        pinBeam.geometry = SCNCylinder(radius: 0.055, height: 1)
        pinBeam.geometry?.materials = [navigationMaterial(.systemYellow.withAlphaComponent(0.6))]
        pinBeam.renderingOrder = 100
        pinBeam.castsShadow = false
        pinBeacon.addChildNode(pinBeam)
        let badge = SCNPlane(width: 1, height: 1.2)
        let material = navigationMaterial(.white)
        let format = UIGraphicsImageRendererFormat()
        format.scale = 2
        material.diffuse.contents = UIGraphicsImageRenderer(size: CGSize(width: 100, height: 120), format: format).image { _ in
            UIColor.systemYellow.setFill()
            UIBezierPath(roundedRect: CGRect(x: 3, y: 3, width: 94, height: 88), cornerRadius: 20).fill()
            let pointer = UIBezierPath()
            pointer.move(to: CGPoint(x: 35, y: 86)); pointer.addLine(to: CGPoint(x: 50, y: 115))
            pointer.addLine(to: CGPoint(x: 65, y: 86)); pointer.close(); pointer.fill()
            UIImage(systemName: "flag.fill")?.withTintColor(.black, renderingMode: .alwaysOriginal)
                .draw(in: CGRect(x: 28, y: 18, width: 44, height: 44))
            ("HOLE" as NSString).draw(in: CGRect(x: 20, y: 65, width: 65, height: 22),
                withAttributes: [.font: UIFont.boldSystemFont(ofSize: 19), .foregroundColor: UIColor.black])
        }
        badge.materials = [material]
        pinBadge.geometry = badge
        pinBadge.constraints = [SCNBillboardConstraint()]
        pinBadge.renderingOrder = 101
        pinBadge.castsShadow = false
        pinBeacon.addChildNode(pinBadge)
    }

    /// Visibility assist only; the actual flag and regulation-sized cup stay unchanged.
    func updatePinBeacon(pin: CoursePoint, sunk: Bool) {
        pinBeacon.isHidden = sunk
        pinBeacon.position = world(pin, y: ground(pin))
        let distance = Double(simd_distance(camera.simdPosition, pinBeacon.simdPosition))
        let scale = HoleNavigation.beaconScale(cameraDistance: distance)
        let height = max(2.8, scale * 1.15)
        pinBeam.simdScale = simd_float3(max(1, scale * 0.8), height, max(1, scale * 0.8))
        pinBeam.position.y = height / 2
        pinBadge.simdScale = simd_float3(repeating: scale)
        pinBadge.position.y = height + scale * 0.6
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
        let hits = contact.update(hands: stancePoint(golferPose.clubGrip), head: stancePoint(golferPose.clubHead), at: now, targets: targets)
        for hit in hits {
            knockdowns[hit.id] = (now, hit.push)
            onBystanderHit?()
        }
    }

    private func moveCamera(_ inputs: SceneInputs, shot: RangeShot?, elapsed: Double, dt: Float) {
        let framing = ShotCameraDirector.shot(ShotCameraDirector.Inputs(
            ball: inputs.ball, heading: inputs.heading + inputs.aim, aim: 0, distanceToPin: inputs.distanceToPin,
            onGreen: inputs.onGreen && shot == nil, handedness: inputs.handedness, shot: shot, elapsed: elapsed,
            reaction: reaction, landingTime: landingTime
        ))
        let cut = cameraStage == nil || (framing.stage != cameraStage && [.hero, .chase, .address, .green].contains(framing.stage))
        cameraStage = framing.stage
        // The framing is worked out on flat ground; lift it by the terrain under its subject.
        let lift = simd_float3(0, ground(CoursePoint(x: Double(framing.lookAt.x), d: Double(-framing.lookAt.z))), 0)
        let position = framing.position + lift, lookAt = framing.lookAt + lift
        if cut {
            camera.simdPosition = position
            cameraLook = lookAt
            camera.camera?.fieldOfView = CGFloat(framing.fieldOfView)
        } else {
            let k = 1 - exp(-framing.damping * dt)
            camera.simdPosition = simd_mix(camera.simdPosition, position, simd_float3(repeating: k))
            cameraLook = simd_mix(cameraLook, lookAt, simd_float3(repeating: k))
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

    /// The read: a lattice of dots over the green that slide downhill, faster on steeper ground,
    /// coloured from calm mint on the flat through amber to red where it really tips.
    private func buildGreenGrid(_ hole: Hole) {
        greenDots.removeAll()
        greenGrid.childNodes.forEach { $0.removeFromParentNode() }
        if greenGrid.parent == nil { scene.rootNode.addChildNode(greenGrid) }
        greenGrid.name = "greenReadGrid"
        let spacing = Self.greenGridSpacing
        let reach = hole.greenRadius + spacing
        let pin = hole.pin
        var x = pin.x - reach
        while x <= pin.x + reach {
            var d = pin.d - reach
            while d <= pin.d + reach {
                let point = CoursePoint(x: x, d: d)
                if point.distance(to: pin) <= hole.greenRadius + 0.5, point.distance(to: pin) > 0.6 {
                    let gradient = hole.terrain.gradient(at: point)
                    let steepness = hypot(gradient.dx, gradient.dd)
                    let dot = SCNNode(geometry: SCNSphere(radius: 0.09))
                    dot.geometry?.materials = [CourseArt.readMaterial(steepness: steepness)]
                    dot.scale = SCNVector3(1, 0.35, 1)
                    dot.castsShadow = false
                    greenGrid.addChildNode(dot)
                    let flow = steepness > 0.0005
                        ? simd_normalize(simd_float3(Float(-gradient.dx), 0, Float(gradient.dd)))
                        : simd_float3(0, 0, 0)
                    greenDots.append((dot, point, flow, Float(min(1.4, steepness * 40))))
                }
                d += spacing
            }
            x += spacing
        }
    }

    private func updateGreenGrid(visible: Bool, now: CFTimeInterval) {
        greenGrid.isHidden = !visible
        guard visible else { return }
        let spacing = Float(Self.greenGridSpacing)
        for dot in greenDots {
            // Slide along the fall line and wrap inside the cell, so the lattice streams downhill.
            let travel = (Float(now) * dot.speed).truncatingRemainder(dividingBy: spacing) - spacing / 2
            let offset = dot.flow * travel
            let point = CoursePoint(x: dot.base.x + Double(offset.x), d: dot.base.d - Double(offset.z))
            dot.node.simdPosition = simd_float3(Float(point.x), ground(point) + 0.02, -Float(point.d))
        }
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

    private func turf(from a: CoursePoint, to b: CoursePoint, width: Double, material: SCNMaterial, y: Float) {
        let node = SCNNode(geometry: CourseArt.capsule(from: a, to: b, width: width, y: y, terrain: hole?.terrain ?? .flat))
        node.geometry?.materials = [material]
        node.name = "mownTurf"
        courseNode.addChildNode(node)
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
        let tree = CourseArt.trees[index % CourseArt.trees.count].clone()
        tree.position = world(point, y: hole.map { CourseArt.elevation(point, hole: $0) } ?? -0.4)
        let scale = Float(0.85 + Double(index % 5) * 0.09)
        tree.scale = SCNVector3(scale, scale, scale)
        tree.eulerAngles.y = Float(index) * 1.7
        courseNode.addChildNode(tree)
    }
}

struct CourseSceneView: UIViewRepresentable {
    let scene: CourseScene
    let inputs: SceneInputs

    func makeCoordinator() -> CourseScene { scene }

    func makeUIView(context: Context) -> SCNView {
        let view = CourseRenderView()
        view.scene = scene.scene
        view.pointOfView = scene.camera
        view.antialiasingMode = .multisampling4X
        view.preferredFramesPerSecond = 120
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

/// The SwiftUI wrapper supplies one course description. SceneKit's automatically generated
/// accessibility subtree otherwise exposes every animated tree/bone and can stall snapshots
/// and VoiceOver while the camera is updating the golfer.
/// Shared, deterministic art resources. No textures, meshes or materials are
/// allocated by the display-link loop; course dressing stays outside playable lies.
@MainActor
enum CourseArt {
    static let fairwayMaterial = turfMaterial(base: UIColor(red: 0.32, green: 0.64, blue: 0.26, alpha: 1), stripes: true)
    static let roughMaterial = turfMaterial(base: UIColor(red: 0.27, green: 0.49, blue: 0.22, alpha: 1), stripes: false)
    static let fringeMaterial = turfMaterial(base: UIColor(red: 0.37, green: 0.59, blue: 0.23, alpha: 1), stripes: false)
    static let greenMaterial = turfMaterial(base: UIColor(red: 0.52, green: 0.75, blue: 0.32, alpha: 1), stripes: true)
    static let sandMaterial = turfMaterial(base: UIColor(red: 0.91, green: 0.83, blue: 0.64, alpha: 1), stripes: false)
    static let waterMaterial: SCNMaterial = {
        let material = SCNMaterial()
        material.lightingModel = .physicallyBased
        material.diffuse.contents = UIColor(red: 0.16, green: 0.60, blue: 0.71, alpha: 1)
        material.roughness.contents = 0.23
        material.metalness.contents = 0.18
        return material
    }()
    static let sky: UIImage = {
        let format = UIGraphicsImageRendererFormat()
        format.scale = 1
        return UIGraphicsImageRenderer(size: CGSize(width: 8, height: 512), format: format).image { context in
            for y in 0..<512 {
                let t = CGFloat(y) / 511
                UIColor(red: 0.26 + t * 0.51, green: 0.61 + t * 0.29, blue: 0.87 + t * 0.08, alpha: 1).setFill()
                context.fill(CGRect(x: 0, y: y, width: 8, height: 1))
            }
        }
    }()

    private static func turfMaterial(base: UIColor, stripes: Bool) -> SCNMaterial {
        let format = UIGraphicsImageRendererFormat()
        format.scale = 1
        let image = UIGraphicsImageRenderer(size: CGSize(width: 256, height: 256), format: format).image { context in
            base.setFill()
            context.fill(CGRect(x: 0, y: 0, width: 256, height: 256))
            if stripes {
                UIColor.white.withAlphaComponent(0.085).setFill()
                context.cgContext.fill(CGRect(x: 0, y: 0, width: 256, height: 128))
                UIColor.black.withAlphaComponent(0.025).setFill()
                context.cgContext.fill(CGRect(x: 0, y: 128, width: 256, height: 128))
            }
            var seed: UInt64 = 74523
            for _ in 0..<5_000 {
                seed = seed &* 6364136223846793005 &+ 1
                let x = Int((seed >> 24) % 256)
                seed = seed &* 6364136223846793005 &+ 1
                let y = Int((seed >> 24) % 256)
                (x % 2 == 0 ? UIColor.white : UIColor.black).withAlphaComponent(0.055).setFill()
                context.cgContext.fill(CGRect(x: x, y: y, width: 1, height: 2))
            }
        }
        let material = SCNMaterial()
        material.lightingModel = .physicallyBased
        material.diffuse.contents = image
        material.diffuse.wrapS = .repeat
        material.diffuse.wrapT = .repeat
        material.diffuse.mipFilter = .linear
        material.diffuse.maxAnisotropy = 4
        material.roughness.contents = 0.95
        return material
    }

    /// A capsule exactly follows the segment-distance fairway rule, including caps.
    /// A stadium shape draped over the terrain: rows across the width at every step along the
    /// length, the rows shrinking around the end caps, so the exact outline is kept and the
    /// interior follows every mound and tilt.
    static func capsule(from a: CoursePoint, to b: CoursePoint, width: Double, y: Float, terrain: Terrain = .flat) -> SCNGeometry {
        let r = width / 2
        let length = a.distance(to: b)
        let ux = length > 0.001 ? (b.x - a.x) / length : 1, ud = length > 0.001 ? (b.d - a.d) / length : 0
        let spacing = 1.6
        let columns = max(6, Int(((length + 2 * r) / spacing).rounded(.up)))
        let across = max(6, min(28, Int((width / spacing).rounded(.up))))
        var vertices: [SCNVector3] = [], normals: [SCNVector3] = [], uv: [CGPoint] = [], indices: [Int32] = []
        for column in 0...columns {
            let s = -r + Double(column) / Double(columns) * (length + 2 * r)
            // Half-width at this station: full along the body, circular at the caps.
            let over = s < 0 ? -s : max(0, s - length)
            let half = sqrt(max(0, r * r - over * over))
            let along = min(max(s, 0), length)
            for row in 0...across {
                let t = -half + Double(row) / Double(across) * 2 * half
                let point = CoursePoint(x: a.x + ux * along - ud * t, d: a.d + ud * along + ux * t)
                let h = Float(terrain.elevation(at: point)) + y
                let g = terrain.gradient(at: point)
                vertices.append(SCNVector3(Float(point.x), h, -Float(point.d)))
                normals.append(SCNVector3(simd_normalize(simd_float3(Float(-g.dx), 1, Float(g.dd)))))
                uv.append(CGPoint(x: point.x / 24, y: point.d / 24))
            }
        }
        let stride = Int32(across + 1)
        for column in 0..<columns {
            for row in 0..<across {
                let i = Int32(column) * stride + Int32(row)
                indices += [i, i + stride, i + 1, i + 1, i + stride, i + stride + 1]
            }
        }
        return SCNGeometry(sources: [.init(vertices: vertices), .init(normals: normals), .init(textureCoordinates: uv)],
                           elements: [SCNGeometryElement(indices: indices, primitiveType: .triangles)])
    }

    /// The hole's own ground shape, sitting a little under the turf, running out into rolling
    /// hills beyond the tree line.
    static func elevation(_ point: CoursePoint, hole: Hole) -> Float {
        let outside = max(0, hole.distanceFromCenterline(point) - hole.fairwayWidth / 2 - Hole.roughWidth - 8)
        let blend = min(1, outside / 65)
        let wave = 9 + 6 * sin(point.x * 0.018 + point.d * 0.009) + 4 * cos(point.d * 0.025)
        return Float(hole.terrain.elevation(at: point)) + Float(-0.72 + blend * blend * wave)
    }

    private static var readMaterials: [Int: SCNMaterial] = [:]

    /// Dot colour for the green read: mint on the flat, amber through orange to red as it steepens.
    static func readMaterial(steepness: Double) -> SCNMaterial {
        let bucket = min(4, Int(steepness / 0.01))
        if let cached = readMaterials[bucket] { return cached }
        let colors = [UIColor(red: 0.78, green: 1, blue: 0.9, alpha: 0.85), UIColor(red: 1, green: 0.95, blue: 0.55, alpha: 0.9),
                      UIColor(red: 1, green: 0.78, blue: 0.3, alpha: 0.95), UIColor(red: 1, green: 0.55, blue: 0.2, alpha: 1),
                      UIColor(red: 1, green: 0.3, blue: 0.25, alpha: 1)]
        let material = SCNMaterial()
        material.lightingModel = .constant
        material.diffuse.contents = colors[bucket]
        material.emission.contents = colors[bucket].withAlphaComponent(0.5)
        material.blendMode = .alpha
        material.writesToDepthBuffer = false
        readMaterials[bucket] = material
        return material
    }

    static func landscape(_ hole: Hole) -> SCNGeometry {
        let columns = 72, rows = 96
        let minX = min(hole.tee.x, hole.pin.x) - 360
        let maxX = max(hole.tee.x, hole.pin.x) + 360
        let minD = -140.0, maxD = hole.pin.d + 400
        var vertices: [SCNVector3] = [], normals: [SCNVector3] = [], uv: [CGPoint] = [], indices: [Int32] = []
        for row in 0...rows {
            for column in 0...columns {
                let p = CoursePoint(x: minX + Double(column) / Double(columns) * (maxX - minX),
                                    d: minD + Double(row) / Double(rows) * (maxD - minD))
                let h = elevation(p, hole: hole)
                let dx = elevation(CoursePoint(x: p.x + 1, d: p.d), hole: hole) - h
                let dz = elevation(CoursePoint(x: p.x, d: p.d - 1), hole: hole) - h
                vertices.append(SCNVector3(Float(p.x), h, -Float(p.d)))
                normals.append(SCNVector3(simd_normalize(simd_float3(-dx, 1, -dz))))
                uv.append(CGPoint(x: p.x / 24, y: p.d / 24))
            }
        }
        for row in 0..<rows {
            for column in 0..<columns {
                let a = Int32(row * (columns + 1) + column), b = a + Int32(columns + 1)
                indices += [a, a + 1, b, a + 1, b + 1, b]
            }
        }
        return SCNGeometry(sources: [.init(vertices: vertices), .init(normals: normals), .init(textureCoordinates: uv)],
                           elements: [SCNGeometryElement(indices: indices, primitiveType: .triangles)])
    }

    private static func part(_ shape: SCNGeometry, color: UIColor, position: SCNVector3, parent: SCNNode) -> SCNNode {
        let material = SCNMaterial()
        material.lightingModel = .physicallyBased
        material.diffuse.contents = color
        material.roughness.contents = 0.8
        shape.materials = [material]
        let node = SCNNode(geometry: shape)
        node.position = position
        parent.addChildNode(node)
        return node
    }

    static let trees: [SCNNode] = (0..<6).map { index in
        let tree = SCNNode()
        tree.name = "canopyTree"
        let trunk = SCNCylinder(radius: 0.48, height: 6)
        trunk.radialSegmentCount = 10
        _ = part(trunk, color: UIColor(red: 0.36, green: 0.25, blue: 0.16, alpha: 1),
                 position: SCNVector3(0, 2.4, 0), parent: tree)
        let evergreen = index % 3 == 0
        for crown in 0..<(evergreen ? 3 : 5) {
            let theta = Double(crown) * 2.4
            let leaf: SCNGeometry
            let position: SCNVector3
            if evergreen {
                let cone = SCNCone(topRadius: 0.4, bottomRadius: CGFloat(4.6 - Double(crown) * 0.85), height: 5.5)
                cone.radialSegmentCount = 20
                leaf = cone
                position = SCNVector3(0, 5.5 + Double(crown) * 2.6, 0)
            } else {
                let sphere = SCNSphere(radius: 3.5)
                sphere.segmentCount = 16
                leaf = sphere
                position = crown == 4 ? SCNVector3(0, 10, 0) : SCNVector3(cos(theta) * 2.3, 7 + Double(crown % 2), sin(theta) * 2.3)
            }
            let color = UIColor(red: 0.20 + Double(crown % 2) * 0.045,
                                green: 0.40 + Double(index % 3) * 0.035 + Double(crown % 2) * 0.05,
                                blue: 0.18 + Double(index % 2) * 0.025, alpha: 1)
            let node = part(leaf, color: color, position: position, parent: tree)
            if !evergreen { node.scale = SCNVector3(1.05, 0.95 + Float(crown % 2) * 0.2, 0.9) }
        }
        return tree.flattenedClone()
    }

    static func flag() -> SCNGeometry {
        var vertices: [SCNVector3] = [], uv: [CGPoint] = [], indices: [Int32] = []
        for row in 0...1 {
            for i in 0...16 {
                let x = Float(i) / 16 * 3.6
                vertices.append(SCNVector3(x, -Float(row) * 1.9 - x * 0.07, sin(x * 2.5) * x * 0.10))
                uv.append(CGPoint(x: Double(i) / 16, y: Double(row)))
            }
        }
        for i in 0..<16 {
            let a = Int32(i), b = a + 17
            indices += [a, b, a + 1, a + 1, b, b + 1]
        }
        let geometry = SCNGeometry(sources: [.init(vertices: vertices), .init(textureCoordinates: uv)],
                                   elements: [SCNGeometryElement(indices: indices, primitiveType: .triangles)])
        geometry.firstMaterial?.isDoubleSided = true
        return geometry
    }

    static func hazardEdge(_ hazard: CourseHazard, parent: SCNNode, y ground: Float = 0) {
        // One mesh per ellipse, not dozens of independently drawn rim segments.
        // Both edges stay inside the existing scoring boundary.
        var vertices: [SCNVector3] = [], indices: [Int32] = []
        for i in 0...64 {
            let angle = Double(i) / 64 * .pi * 2
            for radius in [0.482, 0.499] {
                vertices.append(SCNVector3(Float(hazard.x + cos(angle) * hazard.width * radius), ground - 0.025,
                                           Float(-hazard.distance - sin(angle) * hazard.length * radius)))
            }
        }
        for i in 0..<64 {
            let a = Int32(i * 2), b = a + 2
            indices += [a, a + 1, b, a + 1, b + 1, b]
        }
        let mesh = SCNGeometry(sources: [.init(vertices: vertices),
            .init(normals: vertices.map { _ in SCNVector3(0, 1, 0) })],
            elements: [SCNGeometryElement(indices: indices, primitiveType: .triangles)])
        let color = hazard.kind == .bunker
            ? UIColor(red: 0.65, green: 0.62, blue: 0.37, alpha: 1)
            : UIColor(red: 0.52, green: 0.68, blue: 0.57, alpha: 1)
        let rim = part(mesh, color: color, position: SCNVector3Zero, parent: parent)
        rim.name = "hazardLip"
    }

    static func dress(_ hole: Hole, parent: SCNNode) {
        let ivory = UIColor(red: 0.96, green: 0.94, blue: 0.83, alpha: 1)
        // Tee furniture gives the opening shot a recognizable sense of place.
        for side in [-1.0, 1.0] {
            let marker = part(SCNSphere(radius: 0.38), color: UIColor(red: 0.19, green: 0.39, blue: 0.67, alpha: 1),
                position: SCNVector3(hole.tee.x + side * 4.3, 0.12, -hole.tee.d - 1), parent: parent)
            marker.scale = SCNVector3(1, 0.65, 1)
        }
        let sign = SCNNode()
        sign.position = SCNVector3(hole.tee.x + 9, 0, -hole.tee.d + 2)
        _ = part(SCNCylinder(radius: 0.12, height: 2.8), color: ivory, position: SCNVector3(0, 1, 0), parent: sign)
        _ = part(SCNBox(width: 2.5, height: 1.6, length: 0.18, chamferRadius: 0.12),
            color: UIColor(red: 0.10, green: 0.26, blue: 0.21, alpha: 1), position: SCNVector3(0, 2.2, 0), parent: sign)
        let text = SCNText(string: String(format: "%02d", hole.number), extrusionDepth: 0.015)
        text.font = UIFont.systemFont(ofSize: 1, weight: .bold)
        text.flatness = 0.2
        let number = part(text, color: ivory, position: SCNVector3(-0.7, 1.73, 0.12), parent: sign)
        number.scale = SCNVector3(0.8, 0.8, 0.8)
        parent.addChildNode(sign.flattenedClone())
        let edge = hole.fairwayWidth / 2 + Hole.roughWidth + 3
        for (a, b) in zip(hole.centerline, hole.centerline.dropFirst()) {
            let distance = max(1, a.distance(to: b))
            let offsetX = (b.d - a.d) / distance * edge, offsetD = -(b.x - a.x) / distance * edge
            let path = SCNNode(geometry: capsule(from: CoursePoint(x: a.x + offsetX, d: a.d + offsetD),
                to: CoursePoint(x: b.x + offsetX, d: b.d + offsetD), width: 3.2, y: -0.48))
            path.geometry?.firstMaterial?.diffuse.contents = UIColor(red: 0.72, green: 0.70, blue: 0.59, alpha: 1)
            path.name = "cartPath"
            parent.addChildNode(path)
        }
        // A modest clubhouse beyond the green, outside all playable lies.
        let lodge = SCNNode()
        lodge.position = SCNVector3(hole.pin.x + 65, 0, -hole.pin.d - 75)
        _ = part(SCNBox(width: 22, height: 8, length: 12, chamferRadius: 0.5), color: ivory,
                 position: SCNVector3(0, 4, 0), parent: lodge)
        let roof = part(SCNPyramid(width: 27, height: 6, length: 17),
                        color: UIColor(red: 0.26, green: 0.35, blue: 0.35, alpha: 1), position: SCNVector3(0, 8, 0), parent: lodge)
        roof.name = "clubhouseRoof"
        for x in [-7.0, 0, 7] {
            _ = part(SCNBox(width: 3.8, height: 3.4, length: 0.15, chamferRadius: 0.12),
                     color: UIColor(red: 0.26, green: 0.48, blue: 0.55, alpha: 1), position: SCNVector3(x, 4.5, 6.1), parent: lodge)
        }
        parent.addChildNode(lodge.flattenedClone())
        for i in 0..<10 {
            let cloud = SCNNode()
            cloud.position = SCNVector3(Float(i * 64 - 260), Float(75 + i % 3 * 17), -Float(hole.pin.d + 160 + Double(i % 3) * 70))
            for puff in 0..<3 {
                let sphere = SCNSphere(radius: 13)
                sphere.segmentCount = 12
                let node = part(sphere, color: .white, position: SCNVector3(Float(puff - 1) * 13, Float(puff % 2) * 4, 0), parent: cloud)
                node.scale = SCNVector3(1.2, 0.48, 0.7)
                node.castsShadow = false
            }
            parent.addChildNode(cloud.flattenedClone())
        }
    }
}

private final class CourseRenderView: SCNView {
    override var accessibilityElements: [Any]? {
        get { [] }
        set { }
    }
}
