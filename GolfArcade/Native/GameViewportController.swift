import RealityKit
import UIKit

@MainActor
final class GameViewportController: UIViewController {
    let arView: ARView
    let world = AnchorEntity(world: .zero)
    let ball = ModelEntity(mesh: .generateSphere(radius: GolfBallVisual.radiusMetres), materials: [SimpleMaterial(color: .white, roughness: 0.35, isMetallic: false)])
    let camera = PerspectiveCamera()
    private(set) var golfer: Entity?
    private(set) var impactDate: Date?
    private let loader = NativeAssetLoader()
    private weak var session: GameSession?
    private var courseEntity: Entity?
    private var loadTask: Task<Void, Never>?
    private var preloadTask: Task<Void, Never>?
    private var loadGeneration: UInt64 = 0
    private var playback: AnimationPlaybackController?
    private var activeClip: String?
    private var acceptedRequest: ShotRequest?
    private var impactMovesBall = false
    private var cameraStage: ShotCameraDirector.Stage?
    private var cameraLook = SIMD3<Float>.zero
    private var cameraDate: Date?
    private var cameraShotID: Int?
    private var landingTime: Double?
    private var contactConstraints: NativeGolferConstraints?
    private let bystanders = NativeBystanderPresentation()
    private let effects = NativeCourseEffects()
    private let trajectoryGuide = Entity()
    private var guideRevision = -1
    private var displayedClub: GolfClub?

    init(session: GameSession) {
        GolfSystemRegistry.register()
        arView = ARView(frame: .zero, cameraMode: .nonAR, automaticallyConfigureSession: false)
        self.session = session
        super.init(nibName: nil, bundle: nil)
        world.name = "golf-world"
        world.components.set(SessionComponent(session: session))
        ball.name = "authoritative-ball"
        ball.components.set(ScoredBallComponent())
        ball.isEnabled = false
        world.addChild(ball)
        world.addChild(camera)
        world.addChild(bystanders.root)
        world.addChild(effects.root)
        trajectoryGuide.name = "trajectory-guide"
        world.addChild(trajectoryGuide)
        camera.camera.fieldOfViewInDegrees = 58
        let sunlight = DirectionalLight()
        sunlight.name = "sun"
        sunlight.light.intensity = 5_000
        sunlight.shadow = .init(shadowProjection: .automatic(maximumDistance: 90), depthBias: 0.5)
        sunlight.orientation = simd_quatf(angle: -.pi / 3, axis: SIMD3(1, 0.3, 0))
        world.addChild(sunlight)
        arView.scene.addAnchor(world)
        arView.environment.background = .color(UIColor(red: 0.56, green: 0.73, blue: 0.83, alpha: 1))
        arView.renderOptions.insert(.disableMotionBlur)
    }

    required init?(coder: NSCoder) { fatalError("Use init(session:)") }
    override func loadView() { view = arView }
    #if DEBUG
    var animationDiagnostic: String { "clip=\(activeClip ?? "nil") time=\(playback?.time ?? -1) speed=\(playback?.speed ?? -1)" }
    var bystanderPlayerIDs: [UUID] { bystanders.playerIDs }
    var recoveringBystanderIDs: [UUID] { bystanders.recoveringPlayerIDs }
    var decorativeMotionCount: Int { effects.decorationCount }
    #endif

    func loadHole() {
        guard let session else { return }
        if world.scene == nil { arView.scene.addAnchor(world) }
        cancelLoading()
        loadGeneration &+= 1
        let generation = loadGeneration
        let course = session.round.course, hole = session.round.hole, index = session.round.holeIndex
        session.setAssetState(ready: false, message: "Loading \(course.name), hole \(hole.number)…")
        courseEntity?.isEnabled = false; golfer?.isEnabled = false; ball.isEnabled = false
        bystanders.root.isEnabled = false
        effects.clear()
        loadTask = Task { [weak self, weak session] in
            guard let self, let session else { return }
            let loadStarted = CACurrentMediaTime()
            do {
                let courseEntity = try await loader.course(course, hole: hole)
                let golfer = try await loader.golfer()
                try Task.checkCancellation()
                guard generation == loadGeneration else { return }
                self.courseEntity?.removeFromParent(); self.golfer?.removeFromParent()
                playback?.stop(); playback = nil; activeClip = nil
                impactDate = nil; acceptedRequest = nil
                cameraStage = nil; cameraDate = nil; cameraShotID = nil; landingTime = nil
                self.courseEntity = courseEntity; self.golfer = golfer
                guard let definition = loader.manifest?.golfer,
                      let model = golfer.findEntity(named: definition.skeletonEntity) as? ModelEntity else {
                    throw NativeAssetError.invalid("Missing skeletal model for contact correction")
                }
                try bystanders.prepare(template: golfer, definition: definition, playerCount: session.players.count)
                contactConstraints = try NativeGolferConstraints(model: model)
                world.addChild(courseEntity); world.addChild(golfer)
                effects.bind(course: courseEntity)
                try effects.configureGreenRead(hole: hole)
                golfer.components.set(GolferPresentationComponent())
                updateAppearance(session.player.golferAppearance)
                ball.isEnabled = true
                session.setAssetState(ready: true, message: "Ready")
                session.performance.record(.assetLoad, milliseconds: (CACurrentMediaTime() - loadStarted) * 1_000)
                loader.retain(course: course, index: index)
                if course.holes.indices.contains(index + 1) {
                    preloadTask = Task { [weak self] in
                        guard let self else { return }
                        _ = try? await loader.course(course, hole: course.holes[index + 1])
                    }
                }
            } catch is CancellationError {
                return
            } catch {
                guard generation == loadGeneration else { return }
                session.setAssetState(ready: false, message: error.localizedDescription)
            }
        }
    }

    func cancelLoading() {
        loadGeneration &+= 1
        loadTask?.cancel(); preloadTask?.cancel()
        loadTask = nil; preloadTask = nil
    }

    /// Tear down scene bindings before releasing imported assets. Display handover
    /// never calls this: it reparents the live viewport without interrupting play.
    func stopPresentation() {
        cancelLoading()
        playback?.stop(); playback = nil; activeClip = nil
        arView.scene.removeAnchor(world)
        golfer?.stopAllAnimations(recursive: true)
        contactConstraints?.model.components.remove(IKComponent.self)
        contactConstraints = nil
        bystanders.clear()
        effects.clear()
        courseEntity?.removeFromParent(); golfer?.removeFromParent()
        courseEntity = nil; golfer = nil
        ball.isEnabled = false
        trajectoryGuide.children.removeAll()
        guideRevision = -1
        impactDate = nil; acceptedRequest = nil
    }

    func applyImpact(_ request: ShotRequest, practice: Bool = false) {
        guard let session else { return }
        impactDate = session.date
        acceptedRequest = request
        impactMovesBall = !practice && request.execution.strike != .miss
        let clip = AuthoredGolfMotion.family(club: request.club, type: request.type)
        selectAnimation(clip)
        if let marker = loader.manifest?.golfer.impactMarkers[clip] { playback?.time = marker }
    }

    func applyInputSnapshot() {
        guard let session, let golfer else { return }
        golfer.components.set(GolferPresentationComponent(motion: session.motion.latestSnapshot))
    }

    func updateBall() {
        guard let session, session.assetsReady else { return }
        let round = session.round
        let sample: FlightPoint
        if let shot = round.activeShot {
            sample = shot.position(at: round.elapsed(at: session.date))
        } else {
            sample = FlightPoint(lateralYards: round.ball.x, heightYards: 0, distanceYards: round.ball.d)
        }
        ball.position = GolfUnits.ballPosition(sample, hole: round.hole)
        ball.isEnabled = round.phase != .complete && !(round.activeShot.map { $0.isHoled && round.elapsed(at: session.date) + 0.000001 >= $0.duration } ?? false)
        ball.components.set(ScoredBallComponent(shotID: round.activeShot?.id, elapsed: round.elapsed(at: session.date)))
        // No PhysicsBodyComponent: only the Swift solver controls this scored ball.
    }

    func updateGolfer() {
        guard let session, let golfer, session.assetsReady else { return }
        let round = session.round
        let origin = round.activeShot?.origin ?? round.ball
        if displayedClub != round.club { updateAppearance(session.player.golferAppearance) }
        golfer.position = GolfUnits.position(origin, heightYards: round.hole.surface(at: origin).heightYards)
        golfer.orientation = simd_quatf(angle: Float(-(round.heading + round.combinedAim) * .pi / 180), axis: SIMD3(0, 1, 0))
        golfer.scale = SIMD3(session.player.handedness == .left ? -1 : 1, 1, 1)
        let snapshot = golfer.components[GolferPresentationComponent.self]?.motion
        if let shot = round.activeShot,
           let celebration = ShotCameraDirector.celebrationTime(shot: shot, elapsed: round.elapsed(at: session.date)) {
            selectAnimation("celebration")
            playback?.time = celebration
        } else if let shot = round.activeShot, round.elapsed(at: session.date) >= shot.duration + 0.2 {
            let reaction = AvatarAnimations.Reaction.classify(shot)
            let name = reaction == .holed ? "celebration" : "reaction-" + reaction.rawValue
            selectAnimation(name)
            playback?.time = min(loader.manifest?.golfer.clips?[name]?.duration ?? 0,
                                 max(0, round.elapsed(at: session.date) - shot.duration - 0.2))
        } else if let impactDate, let request = acceptedRequest,
                  round.activeShot != nil || session.isPreparingShot || (round.practiceMode && session.date.timeIntervalSince(impactDate) < 4) {
            let clip = AuthoredGolfMotion.family(club: request.club, type: request.type)
            selectAnimation(clip)
            let marker = loader.manifest?.golfer.impactMarkers[clip] ?? 0
            let duration = loader.manifest?.golfer.clips?[clip]?.duration ?? marker
            if request.execution.source == .phone, snapshot?.phase == .followThrough {
                playback?.time = marker + (duration - marker) * min(1, snapshot?.load ?? 0)
            } else {
                playback?.time = min(duration, marker + session.date.timeIntervalSince(impactDate))
            }
        } else {
            let clip = AuthoredGolfMotion.family(club: round.club, type: round.shotType)
            selectAnimation(clip)
            let load = snapshot?.load ?? 0
            let marker = loader.manifest?.golfer.impactMarkers[clip] ?? 1.1
            let top = marker * (0.75 / 1.1)
            playback?.time = !session.motion.isArmed ? 0 : snapshot?.phase == .downswing ? marker - load * (marker - top) : load * top
        }
        if let name = activeClip, let clip = loader.manifest?.golfer.clips?[name] {
            contactConstraints?.update(clip: clip, time: playback?.time ?? 0, hole: round.hole, world: world)
        }
        let hits = bystanders.update(session: session, world: world, activeClip: activeClip,
                                     activeTime: playback?.time ?? 0, origin: golfer.position, orientation: golfer.orientation)
        session.presentBystanderHits(hits)
    }

    func updateCamera() {
        guard let session else { return }
        let round = session.round
        if let progress = session.flyoverProgress {
            let framing = UIAccessibility.isReduceMotionEnabled
                ? NativeCourseFraming.overview(round.hole)
                : NativeCourseFraming.flyover(round.hole, progress: progress)
            camera.camera.fieldOfViewInDegrees = 55
            camera.look(at: framing.target, from: framing.eye, relativeTo: world)
            cameraStage = nil; cameraDate = nil
            return
        }
        let shot = round.activeShot
        if cameraShotID != shot?.id {
            cameraShotID = shot?.id
            landingTime = shot.flatMap(ShotCameraDirector.landingTime)
        }
        let framing = ShotCameraDirector.shot(.init(ball: round.ball,
            heading: round.heading + round.combinedAim, aim: 0, distanceToPin: round.distanceToPin,
            onGreen: (round.lie == .green || round.club == .putter) && shot == nil,
            handedness: session.player.handedness, shot: shot, elapsed: round.elapsed(at: session.date),
            reaction: shot.map { AvatarAnimations.Reaction.classify($0) }, landingTime: landingTime,
            reduceMotion: UIAccessibility.isReduceMotionEnabled))
        let ground = Float(round.hole.surface(at: .init(x: Double(framing.lookAt.x), d: Double(-framing.lookAt.z))).heightYards)
        let scale = Float(GolfUnits.metresPerYard)
        let position = (framing.position + SIMD3(0, ground, 0)) * scale
        let target = (framing.lookAt + SIMD3(0, ground, 0)) * scale
        let cut = cameraStage == nil || (cameraStage != framing.stage &&
            (framing.stage == .celebration || cameraStage == .celebration))
        let dt = Float(min(0.1, max(0, cameraDate.map { session.date.timeIntervalSince($0) } ?? 0)))
        let k: Float = cut ? 1 : 1 - exp(-framing.damping * dt)
        cameraLook = simd_mix(cameraLook, target, SIMD3(repeating: k))
        let eye = simd_mix(camera.position, position, SIMD3(repeating: k))
        camera.camera.fieldOfViewInDegrees += (framing.fieldOfView - camera.camera.fieldOfViewInDegrees) * k
        camera.look(at: cameraLook, from: eye, relativeTo: world)
        cameraStage = framing.stage; cameraDate = session.date
    }

    func updateEffects() {
        updateTrajectoryGuide()
        guard let session else { return }
        let reduceMotion = UIAccessibility.isReduceMotionEnabled
        effects.updateBreeze(time: session.date.timeIntervalSinceReferenceDate, reduceMotion: reduceMotion)
        effects.updateWater(time: session.date.timeIntervalSinceReferenceDate, reduceMotion: reduceMotion)
        effects.updateGrass(time: session.date.timeIntervalSinceReferenceDate, ball: ball.position(relativeTo: nil),
                            reduceMotion: reduceMotion, camera: camera.position(relativeTo: nil))
        let shot = session.round.activeShot
        effects.updateGreenRead(time: session.date.timeIntervalSinceReferenceDate,
            visible: session.assetsReady && session.round.phase != .complete && shot == nil && (session.round.lie == .green || session.round.club == .putter),
            reduceMotion: reduceMotion)
        effects.updateBallCues(shot: shot, elapsed: session.round.elapsed(at: session.date), hole: session.round.hole,
                              ball: ball.position, camera: camera.position, ready: session.assetsReady && session.round.phase != .complete)
        let origin = shot?.origin ?? session.round.ball
        effects.updateImpact(origin: GolfUnits.position(origin, heightYards: session.round.hole.surface(at: origin).heightYards),
            elapsed: session.round.elapsed(at: session.date), lie: session.round.hole.lie(at: origin),
            heading: session.round.heading + session.round.combinedAim,
            enabled: session.assetsReady && !reduceMotion && shot != nil && shot?.strike != .miss && shot?.club != .putter)
        let elapsed = impactDate.map { session.date.timeIntervalSince($0) } ?? 1
        // A presentation-only impact pulse, with no influence on physics/scoring.
        ball.scale = SIMD3(repeating: impactMovesBall && !reduceMotion && elapsed >= 0 && elapsed < 0.08 ? Float(1 + 0.2 * (1 - elapsed / 0.08)) : 1)
    }

    private func updateTrajectoryGuide() {
        guard let session else { return }
        trajectoryGuide.isEnabled = session.assetsReady && session.showTrajectory &&
            session.previewShot != nil && session.round.phase == .ready &&
            session.flyoverStarted == nil && !session.isPreparingShot
        guard trajectoryGuide.isEnabled, guideRevision != session.previewRevision,
              let shot = session.previewShot else { return }
        trajectoryGuide.children.removeAll()
        guideRevision = session.previewRevision
        let mesh = MeshResource.generateBox(size: 1)
        let air = UnlitMaterial(color: .systemMint), roll = UnlitMaterial(color: .systemOrange)
        for index in 1...64 {
            let previous = shot.position(at: shot.duration * Double(index - 1) / 64)
            let current = shot.position(at: shot.duration * Double(index) / 64)
            let a = GolfUnits.ballPosition(previous, hole: session.round.hole) + SIMD3<Float>(0, 0.08, 0)
            let b = GolfUnits.ballPosition(current, hole: session.round.hole) + SIMD3<Float>(0, 0.08, 0)
            let length = simd_distance(a, b)
            guard length > 0.001 else { continue }
            let segment = ModelEntity(mesh: mesh, materials: [current.heightYards > 0.03 ? air : roll])
            segment.position = (a + b) / 2
            segment.scale = SIMD3(0.018, 0.018, length)
            segment.orientation = simd_quatf(from: SIMD3(0, 0, 1), to: (b - a) / length)
            trajectoryGuide.addChild(segment)
        }
    }

    func updateAppearance(_ appearance: GolferAppearance) {
        guard let golfer, let session else { return }
        NativeGolferStyle.apply(appearance, to: golfer, club: session.round.club)
        displayedClub = session.round.club
    }

    private func selectAnimation(_ name: String) {
        guard name != activeClip, let golfer,
              let resource = golfer.availableAnimations.first(where: { $0.name == name }) else { return }
        playback?.stop()
        playback = golfer.playAnimation(resource, transitionDuration: 0)
        // Keep the animation evaluator active while the system controls time.
        playback?.speed = 0
        activeClip = name
    }
}

/// Correct the bound skeleton using RealityKit's IK solver. No disconnected limb
/// entities are repositioned. Authored hand contacts preserve the club grip while
/// feet receive the exact same terrain elevations used by the scored ball.
@MainActor
final class NativeGolferConstraints {
    let model: ModelEntity
    private let contacts = ["leftHand", "rightHand", "leftFoot", "rightFoot"]
    init(model: ModelEntity) throws {
        self.model = model
        guard let skeleton = model.model?.mesh.contents.skeletons.first else {
            throw NativeAssetError.invalid("Bound golfer has no mesh skeleton")
        }
        var rig = try IKRig(for: skeleton)
        rig.maxIterations = 40
        rig.globalFkWeight = 1
        var constraints: [IKRig.Constraint] = []
        for name in contacts {
            guard let joint = skeleton.joints.first(where: { $0.name.hasSuffix("/" + name) }) else {
                throw NativeAssetError.invalid("Missing \(name) skeletal contact")
            }
            var constraint = name.hasSuffix("Hand")
                ? IKRig.Constraint.parent(named: name, on: joint.name,
                    positionWeight: SIMD3(repeating: 100), orientationWeight: SIMD3(repeating: 10))
                : IKRig.Constraint.point(named: name, on: joint.name, positionWeight: SIMD3(repeating: 100))
            constraint.positionDemand?.influenceDepthMaxJointCount = 3
            constraint.orientationDemand?.influenceDepthMaxJointCount = 1
            constraints.append(constraint)
        }
        rig.constraints = IKRig.ConstraintsCollection(constraints)
        // Keep the complete chain active: freezing spine/shoulder joints holds
        // their reference rotations instead of following the authored twist.
        // FK demands preserve the animated posture; contact influence stays local.
        if model.components[SkeletalPosesComponent.self] == nil {
            model.components.set(SkeletalPosesComponent(poses: [SkeletalPose(id: skeleton.id, from: skeleton)]))
        }
        let resource = try IKResource(rig: rig)
        let component = IKComponent(resource: resource)
        guard contacts.allSatisfy({ rig.constraints[$0] != nil }) else {
            throw NativeAssetError.invalid("Golfer contact rig is incomplete")
        }
        model.components.set(component)
    }

    func update(clip: NativeAssetManifest.GolferAsset.Clip, time: Double, hole: Hole, world: Entity) {
        guard let component = model.components[IKComponent.self] else { return }
        for solver in component.solvers {
            for name in contacts {
                guard var target = clip.contact(name, at: time), let constraint = solver.constraints[name] else { continue }
                if name.hasSuffix("Foot") {
                    var worldPosition = model.convert(position: target, to: world)
                    let point = CoursePoint(x: Double(worldPosition.x) / GolfUnits.metresPerYard,
                                            d: -Double(worldPosition.z) / GolfUnits.metresPerYard)
                    // Root-entity sway/jumps are part of the authored foot lift;
                    // correcting the surface must not cancel that root motion.
                    let lift = max(0, target.y + model.position.y)
                    worldPosition.y = Float(hole.surface(at: point).heightYards * GolfUnits.metresPerYard) + lift
                    target = model.convert(position: worldPosition, from: world)
                }
                constraint.target.translation = target
                constraint.animationOverrideWeight = (position: 1, rotation: 0)
                if name.hasSuffix("Hand"), let rotation = clip.rotation(name, at: time) {
                    constraint.target.rotation = rotation
                    constraint.animationOverrideWeight = (position: 1, rotation: 1)
                }
            }
        }
        model.components.set(component)
    }
}
