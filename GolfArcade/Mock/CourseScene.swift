import QuartzCore
import SceneKit
import SceneKit.ModelIO
import SwiftUI
import CryptoKit

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
    /// Body aiming rotates the golfer/trajectory, not the viewpoint. Manual aiming still pans.
    var cameraAim: Double? = nil
    var reduceMotion = false
    var appearance: GolferAppearance? = nil
    var flyoverProgress: Double? = nil
    var cameraHeading: Double { heading + (cameraAim ?? aim) }

    struct Bystander: Equatable {
        let id: UUID
        let colorIndex: Int
        var appearance: GolferAppearance? = nil
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
final class CourseScene: NSObject, ObservableObject {
    #if DEBUG
    private(set) static var initializationCount = 0
    #endif
    let scene = SCNScene()
    let camera = SCNNode()
    let phoneRenderAudit = GolfRenderAudit(name:"phone")
    let tvRenderAudit = GolfRenderAudit(name:"tv")
    var inputs: SceneInputs?
    /// Called when the club knocks a bystander over.
    var onBystanderHit: (() -> Void)?
    /// Called once when the ball comes down, and once more if it drops in the cup.
    var onLanding: ((RangeAudio.Landing) -> Void)?
    private var landingAnnounced = false
    private var cupAnnounced = false

    private let courseNode = SCNNode()
    private(set) var lagoonMaterial = CourseArt.waterMaterial
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
    private let impactEffects = GolfImpactEffects()
    private var pinFlag: SCNNode?
    private var golfer = AvatarRig(shirt: UIColor(red: 0.12, green: 0.48, blue: 0.49, alpha: 1))
    private var golferAppearance: GolferAppearance?
    private(set) var hole: Hole?
    /// Dots on the putting surface that drift downhill, faster where it is steeper: the read.
    private let greenGrid = SCNNode()
    private var greenGridTimeOrigin: CFTimeInterval?

    /// Height of the ground at `point`, in yards.
    private func ground(_ point: CoursePoint) -> Float {
        Float(hole?.surface(at: point).heightYards ?? 0)
    }

    private var displayLink: CADisplayLink?
    private var lastTick: CFTimeInterval?
    private var lastShot: RangeShot?
    private var reaction: AvatarAnimations.Reaction?
    private var landingTime: Double?
    private var cameraStage: ShotCameraDirector.Stage?
    private var cameraLook = simd_float3.zero
    private var breezeTrees: [(node: SCNNode, rest: simd_quatf, phase: Float)] = []

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
    typealias PoseSource = GolferAnimationState.Source
    private(set) var animationState: GolferAnimationState?
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
        scene.lightingEnvironment.contents = CourseArt.daylightEnvironment
        scene.lightingEnvironment.intensity = 0.85
        scene.fogColor = Self.sky
        scene.fogStartDistance = 160
        scene.fogEndDistance = 620
        camera.camera = SCNCamera()
        camera.camera?.fieldOfView = 55
        camera.camera?.zNear = 0.3
        camera.camera?.zFar = 1000
        camera.camera?.wantsHDR = true
        camera.camera?.wantsExposureAdaptation = false
        camera.camera?.exposureOffset = 0.12
        // The SceneKit SSAO pass produces visible depth-pattern speckling on this
        // course's distant curved surfaces (verified with matched on/off renders).
        // Use sun shadows plus authored local contact/vertex shade instead.
        camera.camera?.screenSpaceAmbientOcclusionIntensity = 0
        camera.camera?.screenSpaceAmbientOcclusionRadius = 0.25
        camera.camera?.screenSpaceAmbientOcclusionDepthThreshold = 0.12
        camera.camera?.screenSpaceAmbientOcclusionBias = 0.015
        camera.camera?.bloomIntensity = 0.08
        camera.camera?.bloomThreshold = 1.2
        scene.rootNode.addChildNode(camera)
        let sun = SCNNode()
        sun.light = SCNLight()
        sun.light?.type = .directional
        sun.name = "sunwardWarmKey"
        sun.light?.intensity = 1500
        sun.light?.color = UIColor(red: 1, green: 0.91, blue: 0.78, alpha: 1)
        sun.light?.castsShadow = true
        sun.light?.shadowMode = .forward
        sun.light?.shadowBias = 0.04
        sun.light?.shadowMapSize = CGSize(width: 2048, height: 2048)
        sun.light?.shadowSampleCount = 16
        sun.light?.shadowRadius = 6
        sun.light?.shadowColor = UIColor(red: 0.13, green: 0.16, blue: 0.26, alpha: 0.55)
        sun.light?.maximumShadowDistance = 220
        sun.light?.shadowCascadeCount = 3
        sun.light?.shadowCascadeSplittingFactor = 0.7
        sun.light?.orthographicScale = 45
        sun.eulerAngles = CourseArt.sunAngles
        scene.rootNode.addChildNode(sun)
        let ambient = SCNNode()
        ambient.light = SCNLight()
        ambient.light?.type = .ambient
        ambient.light?.intensity = 70
        ambient.light?.color = UIColor(red: 0.79, green: 0.88, blue: 1, alpha: 1)
        scene.rootNode.addChildNode(ambient)
        let fill=SCNNode();fill.name="sunwardSkyBounce";fill.light=SCNLight()
        fill.light?.type = .directional;fill.light?.intensity=220
        fill.light?.color=UIColor(red:0.67,green:0.84,blue:1,alpha:1)
        fill.eulerAngles=SCNVector3(-0.45,2.3,0)
        scene.rootNode.addChildNode(fill)
        scene.rootNode.addChildNode(courseNode)
        buildPinBeacon()

        ball.name = "visibilityAssistedBall"
        ball.geometry = SCNSphere(radius: CGFloat(GolfBallVisual.radiusYards))
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
        scene.rootNode.addChildNode(impactEffects.node)
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

    var isAnimating: Bool { displayLink != nil }

    func start() {
        guard displayLink == nil else { return }
        // Establish terrain, golfer and camera before exposing a continuously
        // rendered view. Waiting for the first display-link callback flashed the
        // camera at world origin (sky and the navigation ring, no course).
        tick(now:CACurrentMediaTime())
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
        lagoonMaterial=CourseArt.waterMaterial

        let pin = hole.pin
        let landscape = SCNNode(geometry: CourseArt.landscape(hole))
        landscape.name = "sculptedLandscape"
        landscape.geometry?.materials = [CourseArt.roughMaterial]
        courseNode.addChildNode(landscape)

        if CourseArt.usesSharedPlayableSurface(hole) {
            let surface = SCNNode(geometry: CourseArt.playableSurface(hole))
            lagoonMaterial=CourseArt.lagoonMaterial(hole)
            surface.geometry?.materials[4]=lagoonMaterial
            surface.name = "sharedPlayableSurface"
            courseNode.addChildNode(surface)
        }

        let roughWidth = hole.fairwayWidth + Hole.roughWidth * 2
        if !CourseArt.usesSharedPlayableSurface(hole) {
        for (a, b) in zip(hole.centerline, hole.centerline.dropFirst()) {
            turf(from: a, to: b, width: roughWidth, material: CourseArt.roughMaterial, y: -0.08)
            turf(from: a, to: b, width: hole.fairwayWidth + 2.2, material: CourseArt.fringeMaterial, y: -0.065)
            turf(from: a, to: b, width: hole.fairwayWidth, material: CourseArt.fairwayMaterial, y: -0.05)
        }
        }
        if !CourseArt.usesSharedPlayableSurface(hole) {
            add(SCNBox(width:7,height:0.04,length:5,chamferRadius:0.02),UIColor(red:0.28,green:0.58,blue:0.34,alpha:1),
                at:world(hole.tee,y:ground(hole.tee)-0.065))
        }
        for side in [-1.0, 1.0] {
            let point = CoursePoint(x: hole.tee.x + side * 2.8, d: hole.tee.d + 1)
            let marker = add(SCNBox(width: 0.35, height: 0.28, length: 0.35, chamferRadius: 0.08),
                UIColor(red: 0.94, green: 0.66, blue: 0.32, alpha: 1), at: world(point, y: ground(point) + 0.10))
            marker.name = "teeMarker"
        }
        if hole.greenBoundary == nil && !CourseArt.usesSharedPlayableSurface(hole) {
            turf(from: pin, to: pin, width: hole.greenRadius * 2 + 3, material: CourseArt.fringeMaterial, y: -0.045)
            turf(from: pin, to: pin, width: hole.greenRadius * 2, material: CourseArt.greenMaterial, y: -0.035)
        }

        for hazard in hole.hazards where !CourseArt.usesSharedPlayableSurface(hole) {
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
        disc(radius: 0.059, color: UIColor(white: 0.08, alpha: 1), at: pin, y: pinGround - (hole.fairwayBoundary == nil ? 0.132 : 0.098))
        buildGreenGrid(hole)

        CourseArt.plantTrees(along: hole, parent: courseNode)
        for tree in hole.trees {
            let base=Double(ground(tree.center))
            let trunk=add(SCNCylinder(radius:tree.trunkRadius,height:tree.trunkHeight),
                UIColor(red:0.35,green:0.24,blue:0.15,alpha:1),at:world(tree.center,y:Float(base+tree.trunkHeight/2)))
            trunk.name="collidableTrunk-\(tree.id)"
            trunk.geometry?.materials=[CourseArt.obstacleBark]
            let crown=CourseArt.obstacleCrown(radius:tree.crownRadius,seed:tree.id)
            crown.position=world(tree.center,y:Float(base+tree.trunkHeight*0.68))
            crown.eulerAngles.y=Float(tree.id)*1.7
            crown.name="decorativeCrown-\(tree.id)"
            courseNode.addChildNode(crown)
        }
        if !hole.trees.isEmpty { courseNode.addChildNode(CourseArt.canopyShadows(hole.trees.map(\.center),hole:hole)) }
        // The continuous landscape provides the skyline; detached spherical hills
        // looked like props and could visibly intersect the back of the green.
        CourseArt.dress(hole, parent: courseNode)
        breezeTrees = courseNode.childNodes.enumerated().compactMap { index, node in
            guard ["canopyTree","SunwardTree","sunwardOuterGrove"].contains(node.name ?? "") else { return nil }
            return (node,node.simdOrientation,Float(index)*1.73)
        }
    }

    // MARK: - Frame

    @objc private func step(_ link: CADisplayLink) {
        tick(now: CACurrentMediaTime())
    }

    #if DEBUG
    /// Development-only conversion source. The shipping RealityKit renderer never
    /// constructs this scene. Clone visual content without cameras or gameplay UI.
    func nativeExportScene(textures: URL) throws -> SCNScene {
        let output = SCNScene()
        let metres = SCNNode()
        metres.name = "CourseMetres"
        metres.simdScale = SIMD3(repeating: Float(GolfUnits.metresPerYard))
        let visuals = courseNode.clone()
        visuals.name = "CourseVisuals"
        // SceneKit cycles a shorter material list across geometry elements; its
        // USD exporter instead indexes that list directly. Make it explicit.
        var materialCache: [ObjectIdentifier: SCNMaterial] = [:]
        var geometryCache: [ObjectIdentifier: SCNGeometry] = [:]
        var exportError: Error?
        try FileManager.default.createDirectory(at: textures, withIntermediateDirectories: true)
        var nodes: [SCNNode] = []
        visuals.enumerateChildNodes { node, _ in nodes.append(node) }
        for node in nodes {
            guard let source = node.geometry else { continue }
            let geometryKey = ObjectIdentifier(source)
            if let cached = geometryCache[geometryKey] { node.geometry = cached; continue }
            let geometry: SCNGeometry
            if ["sharedPlayableSurface", "sculptedLandscape"].contains(node.name ?? ""), let data = source.sources(for: .color).first {
                // These channels were shader parameters, not display RGBA. Bake
                // ambient/rim shading and force opaque terrain instead of exporting
                // the sand-rim parameter as alpha (which erases most of the course).
                var colors: [Float] = []
                for index in 0..<data.vectorCount {
                    func channel(_ component: Int) -> Float {
                        data.data.withUnsafeBytes { $0.loadUnaligned(fromByteOffset: data.dataOffset + index * data.dataStride + component * 4, as: Float.self) }
                    }
                    let shade = max(0.55, min(1, channel(1)))
                    let rim = max(0, min(1, channel(3))) * 0.35
                    colors += [shade * (1 - rim * 0.46), shade * (1 - rim * 0.57), shade * (1 - rim * 0.73), 1]
                }
                let baked = SCNGeometrySource(data: colors.withUnsafeBytes { Data($0) }, semantic: .color,
                    vectorCount: data.vectorCount, usesFloatComponents: true, componentsPerVector: 4,
                    bytesPerComponent: 4, dataOffset: 0, dataStride: 16)
                geometry = SCNGeometry(sources: source.sources.filter { $0.semantic != .color } + [baked], elements: source.elements)
            } else if source is SCNBox || source is SCNSphere || source is SCNCylinder || source is SCNText ||
                        source is SCNShape || source is SCNCone || source is SCNTorus || source is SCNCapsule || source is SCNPlane {
                geometry = SCNGeometry(mdlMesh: MDLMesh(scnGeometry: source))
            } else {
                geometry = source.copy() as! SCNGeometry
            }
            let materials = source.materials
            if !materials.isEmpty {
                geometry.materials = (0..<geometry.elementCount).map { index in
                    let original = materials[index % materials.count]
                    let key = ObjectIdentifier(original)
                    if let cached = materialCache[key] { return cached }
                    let material = original.copy() as! SCNMaterial
                    material.name = "NativeMaterial_\(materialCache.count)_" + (original.name ?? "surface")
                    material.shaderModifiers = nil
                    let turfLie: CourseLie? = original === CourseArt.roughMaterial ? .rough :
                        original === CourseArt.deepRoughMaterial ? .deepRough :
                        original === CourseArt.fringeMaterial ? .fringe : nil
                    if let turfLie {
                        let tint = CourseArt.turfTint(turfLie)
                        material.multiply.contents = UIColor(red: CGFloat(tint.x), green: CGFloat(tint.y), blue: CGFloat(tint.z), alpha: 1)
                    }
                    for property in [material.diffuse, material.normal, material.roughness,
                                     material.metalness, material.emission, material.ambientOcclusion,
                                     material.transparent, material.multiply] {
                        if let originalImage = property.contents as? UIImage {
                            let longest = max(originalImage.size.width, originalImage.size.height)
                            let ratio = min(1, 1024 / longest)
                            let size = CGSize(width: originalImage.size.width * ratio, height: originalImage.size.height * ratio)
                            let format = UIGraphicsImageRendererFormat(); format.scale = 1
                            let image = UIGraphicsImageRenderer(size: size, format: format).image { _ in
                                originalImage.draw(in: CGRect(origin: .zero, size: size))
                            }
                            guard let data = image.pngData() else { continue }
                            let hash = SHA256.hash(data: data).map { String(format: "%02x", $0) }.joined()
                            let file = textures.appendingPathComponent(hash + ".png")
                            do { try data.write(to: file); property.contents = file }
                            catch { exportError = error }
                        }
                    }
                    materialCache[key] = material
                    return material
                }
            }
            node.geometry = geometry
            if geometry.elementCount > 1 {
                // Never let USD infer an element's material index. Explicit
                // children avoid exporter aliasing of all eight terrain lies.
                node.geometry = nil
                for (index, element) in geometry.elements.enumerated() {
                    let part = SCNGeometry(sources: geometry.sources, elements: [element])
                    part.materials = [geometry.materials[index]]
                    let child = SCNNode(geometry: part)
                    child.name = "materialPart_\(index)"
                    node.addChildNode(child)
                }
            } else {
                // Remove ModelIO's hidden material bindings on primitive meshes.
                let fresh = SCNGeometry(sources: geometry.sources, elements: geometry.elements)
                fresh.materials = geometry.materials
                node.geometry = fresh
                geometryCache[geometryKey] = fresh
            }
        }
        if let exportError { throw exportError }
        metres.addChildNode(visuals)
        if let hole {
            for (name, point) in [("tee", hole.tee), ("pin", hole.pin)] {
                let marker = SCNNode()
                marker.name = name
                marker.position = world(point, y: ground(point))
                metres.addChildNode(marker)
            }
        }
        output.rootNode.addChildNode(metres)
        return output
    }

    /// One display-link frame, for tests that check what the scene does over a shot.
    func stepForTesting(now: CFTimeInterval = CACurrentMediaTime()) { tick(now: now) }
    var presentedPoseForTesting: BodyPose3D { presentedPose ?? AvatarAnimations.address }
    #endif

    private func tick(now: CFTimeInterval) {
        guard let inputs else { return }
        let dt = Float(min(lastTick.map { now - $0 } ?? 1.0 / 60, 0.1))
        lastTick = now
        load(inputs.hole)
        if golferAppearance != inputs.appearance {
            golfer.node.removeFromParentNode()
            golfer=AvatarRig(shirt:inputs.appearance?.shirtColor ?? Self.playerColor(0),appearance:inputs.appearance)
            stance.addChildNode(golfer.node)
            golferAppearance=inputs.appearance
        }
        pinFlag?.eulerAngles.y = inputs.reduceMotion ? 0 : Float(sin(now * 1.8) * 0.035)
        // Only decorative trees move. Collision trunks and gameplay geometry stay fixed.
        for tree in breezeTrees {
            let angle:Float = inputs.reduceMotion ? 0 : sin(Float(now.truncatingRemainder(dividingBy:1000))*0.72+tree.phase)*0.008
            tree.node.simdOrientation = tree.rest * simd_quatf(angle:angle,axis:simd_float3(0,0,1))
        }
        let materialTime:Float=inputs.reduceMotion ? Float(0) : Float(now.truncatingRemainder(dividingBy:1000))
        CourseArt.waterMaterial.setValue(materialTime,forKey:"sunwardTime")
        lagoonMaterial.setValue(materialTime,forKey:"sunwardTime")
        CourseArt.grassBladeMaterial.setValue(materialTime,forKey:"sunwardTime")
        CourseArt.grassBladeMaterial.setValue(NSValue(scnVector3:SCNVector3(inputs.ball.x,0,-inputs.ball.d)),forKey:"grassBall")

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
        let sample=GolferAnimationState(timestamp:now,pose:pose,source:source)
        animationState=sample
        golfer.setClub(shot?.club ?? inputs.club)
        golfer.apply(sample.pose)
        golfer.express(shot == nil ? nil : reaction,time:now,reduceMotion:inputs.reduceMotion)
        // Anchor the soft contact patch to the actual sloped course, not rig-space y=0.
        let footCenter=(pose[.leftAnkle]+pose[.rightAnkle])/2
        let footWorld=golfer.node.convertPosition(SCNVector3(footCenter.x,0,footCenter.z),to:scene.rootNode)
        let footPoint=CoursePoint(x:Double(footWorld.x),d:-Double(footWorld.z))
        golfer.setContactGroundHeight((ground(footPoint)-ground(origin))/AvatarSize.courseScale)
        updateBystanders(inputs, golferPose: pose, mirror: mirror, now: now)

        // Ball.
        let point = shot?.position(at: elapsed) ?? FlightPoint(lateralYards: inputs.ball.x, heightYards: 0, distanceYards: inputs.ball.d)
        let teed = shot == nil && inputs.lie == .tee
        let visibleLie = inputs.hole.lie(at: CoursePoint(x: point.lateralYards, d: point.distanceYards))
        let lift = Double(ground(CoursePoint(x: point.lateralYards, d: point.distanceYards)))
        let restingHeight = lift + (inputs.hole.fairwayBoundary != nil ? 0 : visibleLie == .green ? -0.035 : visibleLie == .bunker || visibleLie == .water ? -0.025 : -0.05)
        let visualRadius = GolfBallVisual.radiusYards
        let teeLift = Double(AvatarSize.ball.y * AvatarSize.courseScale) - Double(AvatarSize.courseBallRadius)
        ball.position = SCNVector3(point.lateralYards, point.heightYards + (teed ? lift + teeLift : restingHeight) + visualRadius, -point.distanceYards)
        shadow.position = SCNVector3(point.lateralYards, restingHeight + 0.003, -point.distanceYards)
        ballLocator.position = SCNVector3(point.lateralYards, restingHeight + 0.008, -point.distanceYards)
        let sunk = shot.map { $0.isHoled && elapsed >= $0.duration } ?? false
        if let shot, !inputs.isReplay || elapsed > 0 { announceLanding(shot, elapsed: elapsed, hole: inputs.hole, sunk: sunk) }
        ball.isHidden = sunk
        shadow.isHidden = sunk
        ballLocator.isHidden = sunk || (shot != nil && elapsed < (shot?.duration ?? 0))
        tee.isHidden = inputs.lie != .tee && shot?.origin != inputs.hole.tee

        impactEffects.update(origin:world(origin,y:ground(origin)),elapsed:elapsed,
            lie:inputs.hole.lie(at:origin),heading:heading,
            enabled:!inputs.reduceMotion && shot != nil && shot?.strike != .miss && shot?.club != .putter)
        let burst = min(max(elapsed / 0.23, 0), 1)
        impact.isHidden = inputs.reduceMotion || shot == nil || shot?.strike == .miss || elapsed > 0.23
        impact.opacity = CGFloat((1 - burst) * 0.7)
        impact.scale = SCNVector3(0.10 + burst * 0.16, 0.10 + burst * 0.16, 0.10 + burst * 0.16)
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
            return (AuthoredGolfMotion.swing(degrees: cannedAngle, club: inputs?.club ?? .driver,
                type: inputs?.preview?.request.type ?? .full), .canned)
        }
        let followThrough = 1.05
        var source = PoseSource.canned
        func follow(_ t: Double) -> BodyPose3D {
            if isReplay, let recorded = recorder.pose(atImpactOffset: t) { source = .recorded; return recorded }
            if !isReplay, let live { source = .live; return live }
            source = .canned
            return AuthoredGolfMotion.followThrough(elapsed: t, club: shot?.club ?? .driver, type: shot?.request.type ?? .full)
        }
        if elapsed < followThrough { return (follow(elapsed), source) }
        if let shot, inputs?.reduceMotion != true,
           let time = ShotCameraDirector.celebrationTime(shot: shot, elapsed: elapsed) {
            let target = AuthoredGolfMotion.reaction(.holed, time: time)
            let weight = smoothstep(0, 0.22, Float(time)) *
                (1 - smoothstep(Float(AvatarAnimations.reactionLength - 0.4), Float(AvatarAnimations.reactionLength), Float(time)))
            return (AuthoredGolfMotion.transition(AvatarAnimations.address, target, weight), .canned)
        }
        if shot?.club == .putter || shot?.request.type == .chip || shot?.request.type == .pitch || inputs?.reduceMotion == true {
            // Do not turn a quiet putt into a driver-sized finish or club twirl.
            // Start at the held finish, not partway through an earlier return clock.
            return (AuthoredGolfMotion.returnToAddress(progress:smoothstep(Float(followThrough),Float(followThrough + 0.8),Float(elapsed)),
                club:shot?.club ?? .driver,type:shot?.request.type ?? .full), source)
        }
        let reactionTime = elapsed - followThrough
        guard reactionTime < AvatarAnimations.reactionLength else {
            return live.map { ($0, $0 == .cameraWaiting ? .waiting : .live) } ?? (AvatarAnimations.address, .canned)
        }
        let reaction = reaction ?? .solid
        let target = reaction == .pure || reaction == .solid
            ? AuthoredGolfMotion.heldFinish(time:reactionTime,club:shot?.club ?? .driver,type:shot?.request.type ?? .full)
            : AuthoredGolfMotion.reaction(reaction, time: reactionTime)
        var pose = AuthoredGolfMotion.transition(follow(followThrough), target, smoothstep(0, 0.25, Float(reactionTime)))
        let fadeOut = Float(AvatarAnimations.reactionLength - reactionTime)
        if live == nil && (reaction == .pure || reaction == .solid) && fadeOut < 1.05 {
            // Let the arms lower along the authored arc instead of compressing
            // the whole recovery into the last 0.4 seconds of a reaction.
            pose=AuthoredGolfMotion.returnToAddress(progress:smoothstep(0,1.05,1.05-fadeOut),
                club:shot?.club ?? .driver,type:shot?.request.type ?? .full)
        } else if fadeOut < 0.4 {
            if let live { pose=BodyPose3D.lerp(live,pose,fadeOut/0.4) }
            else { pose=AuthoredGolfMotion.transition(AvatarAnimations.address,pose,fadeOut/0.4) }
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
            dot.geometry?.firstMaterial?.diffuse.contents = point.heightYards > 0.03 ? UIColor.systemMint : UIColor.systemOrange
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
        // At greenside distance the real flag is the landmark. Keep the large
        // locator for long shots, without a neon UI mast dominating close play.
        pinBeacon.opacity = CGFloat(HoleNavigation.beaconOpacity(cameraDistance: distance))
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
            for bystander in inputs.bystanders where bystanderRigs[bystander.id] == nil || !bystanderOrder.contains(bystander) {
                bystanderRigs[bystander.id]?.node.removeFromParentNode()
                let rig = AvatarRig(shirt: Self.playerColor(bystander.colorIndex),appearance:bystander.appearance)
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
            if inputs.reduceMotion { rig.apply(AvatarAnimations.idle(time:0,seed:seed)); continue }
            if let knock = knockdowns[bystander.id] {
                let t = now - knock.start
                if t < AvatarAnimations.knockdownLength {
                    // Push into the bystander's own frame.
                    let c = cos(facing), s = sin(facing)
                    let push = simd_float3(knock.push.x * c - knock.push.z * s, 0, knock.push.x * s + knock.push.z * c)
                    rig.apply(AuthoredGolfMotion.recovery(time: t, push: push))
                    continue
                }
                knockdowns[bystander.id] = nil
            }
            rig.apply(AvatarAnimations.idle(time: now, seed: seed))
            targets.append(ClubContact.Target(id: bystander.id, base: spot + simd_float3(0, 0.8, 0), top: spot + simd_float3(0, 5.2, 0), radius: 1.0))
        }

        // The club in the stance frame: the golfer rig is only moved and mirrored.
        func stancePoint(_ p: simd_float3) -> simd_float3 { simd_float3(p.x * mirror, p.y, p.z) + golferPosition }
        guard !golferPose.clubDropped, !inputs.reduceMotion else { return }
        let hits = contact.update(hands: stancePoint(golferPose.clubGrip), head: stancePoint(golferPose.clubHead), at: now, targets: targets)
        for hit in hits {
            knockdowns[hit.id] = (now, hit.push)
            onBystanderHit?()
        }
    }

    private func moveCamera(_ inputs: SceneInputs, shot: RangeShot?, elapsed: Double, dt: Float) {
        if let progress=inputs.flyoverProgress {
            let view=CourseArt.flyover(inputs.hole,progress:inputs.reduceMotion ? 1 : progress)
            camera.simdPosition=view.position;cameraLook=view.lookAt
            camera.camera?.fieldOfView=54
            camera.simdLook(at:cameraLook,up:simd_float3(0,1,0),localFront:simd_float3(0,0,-1))
            cameraStage=nil
            return
        }
        let framing = ShotCameraDirector.shot(ShotCameraDirector.Inputs(
            ball: inputs.ball, heading: inputs.cameraHeading, aim: 0, distanceToPin: inputs.distanceToPin,
            onGreen: inputs.onGreen && shot == nil, handedness: inputs.handedness, shot: shot, elapsed: elapsed,
            reaction: reaction, landingTime: landingTime, reduceMotion: inputs.reduceMotion,
            liveCamera: retargeter != nil && !inputs.isReplay
        ))
        // A post-cup close-up must cut, not fly back across an entire par five.
        let cut = cameraStage == nil ||
            (cameraStage != framing.stage && (framing.stage == .celebration || cameraStage == .celebration))
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
        greenGrid.childNodes.forEach { $0.removeFromParentNode() }
        if greenGrid.parent == nil { scene.rootNode.addChildNode(greenGrid) }
        greenGrid.name = "greenReadGrid"
        greenGrid.geometry=CourseArt.greenReadGeometry(hole)
        greenGrid.castsShadow=false
        greenGridTimeOrigin=nil
    }

    private func updateGreenGrid(visible: Bool, now: CFTimeInterval) {
        greenGrid.isHidden = !visible
        guard visible else { return }
        if greenGridTimeOrigin == nil { greenGridTimeOrigin=now }
        greenGrid.geometry?.firstMaterial?.setValue(Float(now-(greenGridTimeOrigin ?? now)),forKey:"readTime")
        greenGrid.geometry?.firstMaterial?.setValue(Float(inputs?.reduceMotion == true ? 0 : 1),forKey:"readMotion")
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

}

struct CourseSceneView: UIViewRepresentable {
    let scene: CourseScene
    let inputs: SceneInputs
    var externalDisplayActive = false
    var ownsLifecycle = true

    func makeCoordinator() -> CourseScene? { ownsLifecycle ? scene : nil }

    func makeUIView(context: Context) -> SCNView {
        let view = CourseRenderView()
        view.scene = scene.scene
        view.pointOfView = scene.camera
        view.renderPolicy = .phone(externalDisplayActive: externalDisplayActive)
        view.rendersContinuously = true
        view.delegate = scene.phoneRenderAudit
        view.isUserInteractionEnabled = false
        scene.inputs = inputs
        if ownsLifecycle { scene.start() }
        return view
    }

    func updateUIView(_ view: SCNView, context: Context) {
        scene.inputs = inputs
        (view as? CourseRenderView)?.renderPolicy = .phone(externalDisplayActive: externalDisplayActive)
    }

    static func dismantleUIView(_ view: SCNView, coordinator: CourseScene?) {
        coordinator?.stop()
        view.rendersContinuously = false
        view.scene = nil
    }
}

/// The SwiftUI wrapper supplies one course description. SceneKit's automatically generated
/// accessibility subtree otherwise exposes every animated tree/bone and can stall snapshots
/// and VoiceOver while the camera is updating the golfer.
/// Shared, deterministic art resources. No textures, meshes or materials are
/// allocated by the display-link loop; course dressing stays outside playable lies.
@MainActor
enum CourseArt {
    #if DEBUG
    static var exportsNativeGeometry = false
    #endif
    static func usesSharedPlayableSurface(_ hole: Hole) -> Bool {
        #if DEBUG
        if exportsNativeGeometry { return true }
        #endif
        return hole.fairwayBoundary != nil
    }
    private static func optimized(_ node: SCNNode) -> SCNNode {
        #if DEBUG
        if exportsNativeGeometry { return node }
        #endif
        return node.flattenedClone()
    }
    static let sunAngles=SCNVector3(-0.52,2.1,0)
    static let sunDirection=(simd_quatf(angle:sunAngles.y,axis:simd_float3(0,1,0)) *
        simd_quatf(angle:sunAngles.x,axis:simd_float3(1,0,0))).act(simd_float3(0,0,1))
    static let fairwayMaterial = turfMaterial(base: UIColor(red: 0.42, green: 0.62, blue: 0.26, alpha: 1), stripes: true)
    static let roughMaterial = turfMaterial(base: UIColor(red: 0.29, green: 0.46, blue: 0.22, alpha: 1), stripes: false,tint:SCNVector3(turfTint(.rough)))
    static let deepRoughMaterial = turfMaterial(base: UIColor(red: 0.25, green: 0.39, blue: 0.19, alpha: 1), stripes: false,tint:SCNVector3(turfTint(.deepRough)))
    static let fringeMaterial = turfMaterial(base: UIColor(red: 0.43, green: 0.64, blue: 0.29, alpha: 1), stripes: false,tint:SCNVector3(turfTint(.fringe)))
    static let greenMaterial = turfMaterial(base: UIColor(red: 0.55, green: 0.73, blue: 0.36, alpha: 1), stripes: true, crosscut: true)
    static let sandMaterial:SCNMaterial = {
        let material=turfMaterial(base:UIColor(red:0.96,green:0.82,blue:0.62,alpha:1),stripes:false,sand:true)
        material.shaderModifiers?[.surface]?.append("""

        // A narrow earth/thatch seam gives the cut sand edge material thickness.
        // Metadata is sampled from the actual bunker boundary, not a decal ring.
        float rim = saturate(in.terrainData.a);
        float grain = 0.9 + 0.1 * sin(in.terrainWorld.x * 11.0 + sin(in.terrainWorld.y * 9.0));
        _surface.diffuse.rgb *= mix(float3(1.0), float3(0.54,0.43,0.27), rim * grain * 0.7);
        """)
        return material
    }()
    static let shoreMaterial: SCNMaterial = {
        let material = turfMaterial(base:.brown,stripes:false)
        material.name="Sunward exposed bank soil"
        material.shaderModifiers?[.surface]?.append("""

        float wet = smoothstep(0.0,1.0,in.terrainData.b);
        float grain = 0.94 + 0.06*sin(in.terrainWorld.x*3.1)*sin(in.terrainWorld.y*2.7);
        float3 soil = float3(0.32,0.27,0.17)*grain;
        _surface.diffuse.rgb = mix(_surface.diffuse.rgb,soil,wet);
        _surface.roughness = mix(0.95,0.78,wet);
        """)
        return material
    }()
    static let waterMaterial: SCNMaterial = {
        let material = SCNMaterial()
        material.lightingModel = .physicallyBased
        material.name = "Sunward rippled lagoon"
        material.diffuse.contents = UIColor(red: 0.08, green: 0.54, blue: 0.53, alpha: 1)
        material.roughness.contents = 0.19
        material.metalness.contents = 0.05
        material.normal.contents = detailNormal(sand:false,water:true)
        material.normal.wrapS = .repeat; material.normal.wrapT = .repeat
        material.normal.mipFilter = .linear; material.normal.maxAnisotropy = 8
        material.normal.contentsTransform = SCNMatrix4MakeScale(8,8,1)
        material.normal.intensity = 0.035
        let waterVertex=terrainVertex.replacingOccurrences(of:"#pragma body",with:"""
        float3 waterAxisX;
        float3 waterAxisZ;
        #pragma body
        out.waterAxisX = normalize((scn_node.modelViewTransform * float4(1.0,0.0,0.0,0.0)).xyz);
        out.waterAxisZ = normalize((scn_node.modelViewTransform * float4(0.0,0.0,1.0,0.0)).xyz);
        """)
        material.shaderModifiers = [.geometry:waterVertex,.surface: """
        #pragma arguments
        float sunwardTime;
        #pragma body
        // World-anchored wave gradients move the reflected light, not just a
        // painted brightness pattern. The shared water/collision plane is fixed.
        float2 p = in.terrainWorld;
        float a = dot(p,float2(0.75,0.31)) + sunwardTime * 0.70;
        float b = dot(p,float2(-0.35,1.15)) - sunwardTime * 0.52;
        float c = dot(p,float2(1.8,-1.2)) + sunwardTime * 0.91;
        float2 slope = 0.035*cos(a)*float2(0.75,0.31)
                     + 0.018*cos(b)*float2(-0.35,1.15)
                     + 0.007*cos(c)*float2(1.8,-1.2);
        _surface.normal = normalize(_surface.normal - in.waterAxisX*slope.x - in.waterAxisZ*slope.y);
        float ripple = sin(a)*cos(b);
        float shallow = saturate(in.terrainData.r);
        _surface.diffuse.rgb = mix(float3(0.025,0.27,0.31),float3(0.13,0.47,0.39),shallow);
        _surface.diffuse.rgb *= 0.99 + 0.01 * ripple;
        """]
        material.setValue(Float(0),forKey:"sunwardTime")
        return material
    }()

    /// Offline-rendered surroundings from this exact authored hole, sampled only
    /// by water. No capture work on the frame loop and no change to sky/ground IBL.
    /// A local box projection corrects the largest camera-translation errors of
    /// an infinitely distant cubemap; this is not a dynamic planar reflection.
    static func lagoonMaterial(_ hole:Hole) -> SCNMaterial {
        guard Course.sunwardResort.holes.contains(hole),
              let lake=hole.hazards.first(where:{$0.kind == .water}),
              let level=hole.waterElevations[lake.id] else {return waterMaterial}
        let faces=(0..<6).compactMap { index -> UIImage? in
            guard let url=Bundle.main.url(forResource:"hole-\(hole.number)-face-\(index)",withExtension:"png",subdirectory:"Reflections") else {return nil}
            return UIImage(contentsOfFile:url.path)
        }
        guard faces.count == 6 else {return waterMaterial}
        return lagoonMaterial(faces:faces,center:SCNVector3(lake.x,level+0.05,-lake.distance),
            extent:SCNVector3(lake.width/2+10,30,lake.length/2+10))
    }

    static func lagoonMaterial(faces:[UIImage],center:SCNVector3,extent:SCNVector3) -> SCNMaterial {
        let material=waterMaterial.copy() as! SCNMaterial
        material.name="Sunward local lagoon reflection"
        let format=UIGraphicsImageRendererFormat();format.scale=1;format.opaque=false
        let atlas=UIGraphicsImageRenderer(size:CGSize(width:1536,height:256),format:format).image { _ in
            for (index,face) in faces.enumerated() {face.draw(in:CGRect(x:index*256,y:0,width:256,height:256))}
        }
        let map=SCNMaterialProperty(contents:atlas)
        map.minificationFilter = .linear;map.magnificationFilter = .linear;map.mipFilter = .none
        material.setValue(map,forKey:"lagoonReflection")
        material.setValue(center,forKey:"lagoonCenter")
        material.setValue(extent,forKey:"lagoonExtent")
        material.shaderModifiers?[.fragment]="""
        #pragma arguments
        texture2d<float> lagoonReflection;
        float3 lagoonCenter;
        float3 lagoonExtent;
        #pragma body
        float3 direction = reflect(-normalize(_surface.view),normalize(_surface.normal));
        direction = normalize((scn_frame.inverseViewTransform * float4(direction,0.0)).xyz);
        float3 skyDirection=direction;
        float3 position = (scn_frame.inverseViewTransform * float4(_surface.position,1.0)).xyz;
        float3 ray = select(float3(-1.0),float3(1.0),direction>=0.0)*max(abs(direction),float3(0.0001));
        float3 farPlane = lagoonCenter + select(-lagoonExtent,lagoonExtent,ray>0.0);
        float3 distances = (farPlane-position)/ray;
        float distance = max(0.0,min(distances.x,min(distances.y,distances.z)));
        direction = position + direction*distance-lagoonCenter;
        // Explicit atlas projection avoids SceneKit's undocumented image-cube
        // face transforms. UVs match the horizontally flipped camera captures.
        float3 magnitude=abs(direction);
        float face=0.0;float2 uv;
        if(magnitude.x>=magnitude.y && magnitude.x>=magnitude.z) {
            face=direction.x>=0.0 ? 0.0 : 1.0;
            uv=float2(direction.x>=0.0 ? -direction.z : direction.z,-direction.y)/magnitude.x;
        } else if(magnitude.y>=magnitude.z) {
            face=direction.y>=0.0 ? 2.0 : 3.0;
            uv=float2(direction.x,direction.y>=0.0 ? direction.z : -direction.z)/magnitude.y;
        } else {
            face=direction.z>=0.0 ? 4.0 : 5.0;
            uv=float2(direction.z>=0.0 ? direction.x : -direction.x,-direction.y)/magnitude.z;
        }
        uv=clamp(uv*0.5+0.5,float2(0.5/256.0),float2(1.0-0.5/256.0));
        uv.x=(face+uv.x)/6.0;
        constexpr sampler lagoonSampler(filter::linear);
        float4 captured = lagoonReflection.sample(lagoonSampler,uv);
        float zenith=pow(max(0.0,skyDirection.y),0.45);
        float3 sky=float3(0.86-0.46*zenith,0.89-0.23*zenith,0.89+0.03*zenith);
        float3 reflected = mix(sky*sky,captured.rgb,captured.a);
        float facing = saturate(dot(normalize(_surface.normal),normalize(_surface.view)));
        float fresnel = 0.08 + 0.64*pow(1.0-facing,2.0);
        _output.color.rgb = mix(_output.color.rgb*0.62,reflected*float3(0.88,1.0,0.96),fresnel);
        """
        return material
    }
    static let sky: UIImage = {
        let format = UIGraphicsImageRendererFormat()
        format.scale = 1;format.opaque=true
        // A valid 2:1 spherical projection, not a screen-stretched blue strip.
        // Warm horizon haze and the broad solar glow remain fixed in world space.
        let sun=sunDirection
        return UIGraphicsImageRenderer(size: CGSize(width: 1024, height: 512), format: format).image { context in
            for y in stride(from:0,to:512,by:2) {
                let theta=Float(y)/512 * .pi,elevation=cos(theta)
                let zenith=pow(max(0,elevation),0.45)
                for x in stride(from:0,to:1024,by:2) {
                    let phi=Float(x)/1024 * 2 * .pi
                    let direction=simd_float3(sin(theta)*sin(phi),elevation,sin(theta)*cos(phi))
                    let glow=pow(max(0,simd_dot(direction,sun)),32)*0.22
                    let red=0.86-0.46*zenith+glow
                    let green=0.89-0.23*zenith+glow*0.60
                    let blue=0.89+0.03*zenith
                    UIColor(red:CGFloat(min(1,red)),green:CGFloat(min(1,green)),blue:CGFloat(blue),alpha:1).setFill()
                    context.cgContext.fill(CGRect(x:x,y:y,width:2,height:2))
                }
            }
        }
    }()

    /// Small, generated equirectangular irradiance map: sky above, warm ground bounce below.
    /// This gives PBR garments/skin curved highlights without a large downloaded HDR asset.
    static let daylightEnvironment: UIImage = {
        let format=UIGraphicsImageRendererFormat();format.scale=1
        return UIGraphicsImageRenderer(size:CGSize(width:256,height:128),format:format).image { context in
            for y in 0..<128 {
                let t=CGFloat(y)/127
                let horizon=exp(-pow((t-0.5)/0.22,2))
                UIColor(red:0.39+0.34*horizon,green:0.49+0.27*horizon,
                    blue:0.57+0.23*horizon-0.19*t,alpha:1).setFill()
                context.cgContext.fill(CGRect(x:0,y:y,width:256,height:1))
            }
        }
    }()

    /// Tileable tangent-space microgeometry: light reacts to blades/rake grooves,
    /// rather than merely painting noise into an otherwise perfectly smooth floor.
    private static func detailNormal(sand: Bool, water: Bool = false) -> UIImage {
        let size = 128
        let format=UIGraphicsImageRendererFormat();format.scale=1;format.opaque=true
        return UIGraphicsImageRenderer(size:CGSize(width:size,height:size),format:format).image { context in
            for y in 0..<size { for x in 0..<size {
                let u=Double(x)/Double(size)*2*Double.pi, v=Double(y)/Double(size)*2*Double.pi
                let dx:Double, dy:Double
                if water {
                    dx=0.18*cos(u*3+sin(v*2))+0.08*cos(u*7-v*4)
                    dy=0.14*cos(v*4+sin(u*2))+0.06*cos(v*9+u*5)
                } else if sand {
                    dx=0.05*cos(u*8+sin(v*2))
                    dy=0.17*cos(v*12+sin(u*2))+0.04*cos(v*23+u*7)
                } else {
                    dx=0.24*cos(u*19+sin(v*7))+0.10*cos(u*31-v*13)
                    dy=0.18*cos(v*27+sin(u*5))+0.08*cos(v*17+u*23)
                }
                let n=simd_normalize(simd_double3(-dx,-dy,1))
                UIColor(red:(n.x+1)/2,green:(n.y+1)/2,blue:(n.z+1)/2,alpha:1).setFill()
                context.cgContext.fill(CGRect(x:x,y:y,width:1,height:1))
            } }
        }
    }
    private static let grassNormal = detailNormal(sand:false)
    private static let sandNormal = detailNormal(sand:true)

    private static func turfMaterial(base: UIColor, stripes: Bool, crosscut: Bool = false, sand: Bool = false,
                                     tint:SCNVector3=SCNVector3(1,1,1)) -> SCNMaterial {
        let format = UIGraphicsImageRendererFormat()
        format.scale = 1
        let image = UIGraphicsImageRenderer(size: CGSize(width: 256, height: 256), format: format).image { context in
            base.setFill()
            context.cgContext.fill(CGRect(x: 0, y: 0, width: 256, height: 256))
            // Periodic low-frequency color variation avoids a perfectly flat carpet.
            // Sand gets fine grains and rake ripples, never grass-blade strokes.
            if !crosscut { for y in stride(from: 0, to: 256, by: 4) {
                for x in stride(from: 0, to: 256, by: 4) {
                    let variation = sin(Double(x) * .pi / 64) * cos(Double(y) * .pi / 128)
                    (variation > 0 ? UIColor.white : UIColor.black).withAlphaComponent(abs(variation) * 0.012).setFill()
                    // RendererContext.fill defaults to .copy, replacing opaque turf with
                    // translucent pixels. Composite grain over the base instead.
                    context.cgContext.fill(CGRect(x: x, y: y, width: 4, height: 4))
                }
            } }
            if sand {
                for row in stride(from: 0, to: 256, by: 8) {
                    let path = UIBezierPath()
                    path.move(to: CGPoint(x: 0, y: row))
                    for x in stride(from: 4, through: 256, by: 4) {
                        path.addLine(to: CGPoint(x: Double(x), y: Double(row) + sin(Double(x) * .pi / 64) * 2))
                    }
                    UIColor.black.withAlphaComponent(0.035).setStroke()
                    path.lineWidth = 0.6
                    path.stroke()
                }
            }
            var seed: UInt64 = 74523
            for _ in 0..<5_000 {
                seed = seed &* 6364136223846793005 &+ 1
                let x = Int((seed >> 24) % 256)
                seed = seed &* 6364136223846793005 &+ 1
                let y = Int((seed >> 24) % 256)
                (x % 2 == 0 ? UIColor.white : UIColor.black).withAlphaComponent(0.035).setFill()
                context.cgContext.fill(CGRect(x: x, y: y, width: 1, height: sand ? 1 : 2))
            }
        }
        let material = SCNMaterial()
        material.lightingModel = .physicallyBased
        let textureName = sand ? "SunwardSand" : stripes ? "SunwardFairway" : "SunwardRough"
        material.name = textureName
        material.diffuse.contents = crosscut ? image : (UIImage(named:textureName) ?? image)
        material.diffuse.wrapS = .repeat
        material.diffuse.wrapT = .repeat
        material.diffuse.mipFilter = .linear
        material.diffuse.maxAnisotropy = 8
        // The authored rough image contains full blades. A two-yard tile made
        // those blades microscopic and collapsed its detail into green noise.
        material.diffuse.contentsTransform = SCNMatrix4MakeScale(sand || stripes ? 12 : 4, sand || stripes ? 12 : 4, 1)
        material.normal.contents = sand ? sandNormal : grassNormal
        material.normal.intensity = crosscut ? 0.012 : sand ? 0.10 : stripes ? 0.065 : 0.35
        material.normal.wrapS = .repeat;material.normal.wrapT = .repeat
        material.normal.mipFilter = .linear;material.normal.maxAnisotropy = 8
        material.normal.contentsTransform = SCNMatrix4MakeScale(6,6,1)
        if stripes {
            // The world-space grain tile is four yards, not the old 24-yard coarse marks.
            // Mowing stays broad and subtle regardless of texture sampling.
            material.shaderModifiers = [.surface: """
            #pragma body
            float band = sin(in.terrainWorld.y * 0.55 + in.terrainWorld.x * 0.18);
            _surface.diffuse.rgb *= 1.0 + \(crosscut ? "0.035" : "0.065") * tanh(band * 3.0);
            """]
            if crosscut {
                material.shaderModifiers?[.surface]?.append("\n_surface.diffuse.rgb *= 1.0 + 0.012 * sin(_surface.diffuseTexcoord.x * 6.2831853);")
            }
        }
        material.roughness.contents = sand ? 0.86 : crosscut ? 0.95 : 0.88
        material.shaderModifiers?[.geometry] = terrainVertex
        if material.shaderModifiers == nil { material.shaderModifiers = [.geometry:terrainVertex] }
        let existing=material.shaderModifiers?[.surface] ?? "#pragma body\n"
        material.shaderModifiers?[.surface] = existing + "\n" + terrainGrain.replacingOccurrences(of:"#pragma body",with:"")
        material.shaderModifiers?[.surface]?.append("\n_surface.diffuse.rgb *= float3(\(tint.x),\(tint.y),\(tint.z));")
        return material
    }

    /// A capsule exactly follows the segment-distance fairway rule, including caps.
    /// A stadium shape draped over the terrain: rows across the width at every step along the
    /// length, the rows shrinking around the end caps, so the exact outline is kept and the
    /// interior follows every mound and tilt.
    static func capsule(from a: CoursePoint, to b: CoursePoint, width: Double, y: Float, terrain: Terrain = .flat,
                        height: ((CoursePoint) -> Float)? = nil) -> SCNGeometry {
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
            // The cap extends beyond the station. Clamping here flattened the ends,
            // so visible turf disagreed with the capsule-distance lie classification.
            let along = s
            for row in 0...across {
                let t = -half + Double(row) / Double(across) * 2 * half
                let point = CoursePoint(x: a.x + ux * along - ud * t, d: a.d + ud * along + ux * t)
                let h = (height?(point) ?? Float(terrain.elevation(at: point))) + y
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
    static func backgroundRamp(_ distance:Double)->Double {
        let t=max(0,min(1,distance/80))
        return t*t*t*(t*(t*6-15)+10)
    }

    static func blendedClearance(_ a:Double,_ b:Double)->Double {
        let width=24.0,h=max(0,width-abs(a-b))/width
        return min(a,b)-h*h*width*0.25
    }

    static func elevation(_ point: CoursePoint, hole: Hole) -> Float {
        let surface=hole.surface(at:point)
        var outside = max(0, hole.distanceFromCenterline(point) - hole.fairwayWidth / 2 - Hole.roughWidth - 8)
        if let fairway=hole.fairwayBoundary {
            if surface.lie != .outOfBounds { outside=0 }
            else {
                outside=blendedClearance(fairway.distance(to:point)-Hole.roughWidth-8,
                    (hole.greenBoundary?.distance(to:point) ?? point.distance(to:hole.pin)-hole.greenRadius)-10)
                for hazard in hole.hazards {
                    let extent=1+abs(hazard.contour)*1.4
                    let dx=max(0,abs(point.x-hazard.x)-hazard.width/2*extent)
                    let dd=max(0,abs(point.d-hazard.distance)-hazard.length/2*extent)
                    outside=blendedClearance(outside,hypot(dx,dd)-10)
                }
                outside=max(0,outside)
            }
        }
        let blend = hole.fairwayBoundary == nil ? pow(min(1,outside/65),2) : backgroundRamp(outside)
        let wave = hole.fairwayBoundary == nil
            ? 7 + 4 * sin(point.x * 0.018 + point.d * 0.009) + 3 * cos(point.d * 0.025)
            : resortBackdropRelief(point,clearance:outside)
        // Coarse landscape triangles must stay below the 0.8-yard bunker bowls.
        // A 0.72-yard underlay pierced their floors between coarse sample vertices.
        return Float(surface.heightYards) + Float(-2.0 + blend * wave)
    }

    /// Lower rolling foothills reveal a separate distant ridge. Both layers
    /// belong to the continuous terrain, not floating decorative hill meshes.
    /// Clearance is measured only outside protected playable ground.
    static func resortBackdropRelief(_ point:CoursePoint,clearance:Double)->Double {
        let foothills=8 + 5*sin(point.x*0.021+point.d*0.009)+3*cos(point.d*0.033)
        let ridgeBlend=backgroundRamp((clearance-100)*(80.0/120.0))
        let ridge=16 + 7*sin(point.x*0.011-point.d*0.008)
        return foothills+ridgeBlend*ridge
    }

    static func fineSurfaceExtents(_ hole:Hole)->SIMD4<Double> {
        var bounds=hole.centerline+(hole.fairwayBoundary?.points ?? [])+(hole.greenBoundary?.points ?? [])
        for hazard in hole.hazards {
            let extent=1+abs(hazard.contour)*1.4
            bounds.append(CoursePoint(x:hazard.x-hazard.width/2*extent,d:hazard.distance-hazard.length/2*extent))
            bounds.append(CoursePoint(x:hazard.x+hazard.width/2*extent,d:hazard.distance+hazard.length/2*extent))
        }
        return SIMD4((bounds.map(\.x).min() ?? 0)-35,(bounds.map(\.x).max() ?? 0)+35,
            (bounds.map(\.d).min() ?? 0)-35,(bounds.map(\.d).max() ?? 0)+35)
    }

    static func dressingHeight(_ point: CoursePoint, hole: Hole) -> Float {
        if usesSharedPlayableSurface(hole) {
            let limits=fineSurfaceExtents(hole)
            if point.x >= limits.x,point.x <= limits.y,point.d >= limits.z,point.d <= limits.w {
                return surfaceHeight(point, hole: hole)
            }
        }
        return elevation(point, hole: hole)
    }

    /// Only the non-playable outer border blends into the low-detail landscape.
    /// Playable ground, including every bunker and green, remains the solver's surface.
    static func surfaceHeight(_ point: CoursePoint, hole: Hole, extents: SIMD4<Double>? = nil,
                              sampled: CourseSurface? = nil) -> Float {
        let surface = sampled ?? hole.surface(at: point)
        let height = Float(surface.heightYards)
        guard surface.lie == .outOfBounds, usesSharedPlayableSurface(hole) else { return height }
        let limits: SIMD4<Double>
        if let extents { limits = extents }
        else {
            limits = fineSurfaceExtents(hole)
        }
        let edge = min(point.x-limits.x, limits.y-point.x, point.d-limits.z, limits.w-point.d)
        let u = Float(max(0, min(1, edge / 16)))
        let blend = u * u * (3 - 2 * u)
        // Continue the same distant hills through the fine mesh. Flattening its
        // interior to solver height made an artificial cliff at the mesh bounds.
        return elevation(point,hole:hole)+2*blend
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
        let columns = 144, rows = 192
        let minX = min(hole.tee.x, hole.pin.x) - 360
        let maxX = max(hole.tee.x, hole.pin.x) + 360
        let minD = -140.0, maxD = hole.pin.d + 400
        var vertices: [SCNVector3] = [], normals: [SCNVector3] = [], uv: [CGPoint] = [], indices: [Int32] = []
        let limits=fineSurfaceExtents(hole)
        func coveredByPlayableMesh(_ index:Int32)->Bool {
            guard usesSharedPlayableSurface(hole) else { return false }
            let vertex=vertices[Int(index)]
            return Double(vertex.x)>limits.x && Double(vertex.x)<limits.y &&
                -Double(vertex.z)>limits.z && -Double(vertex.z)<limits.w
        }
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
                // The fine mesh completely covers this rectangle. Do not leave
                // a second coarse floor underneath it: interpolation of that
                // floor could poke through a deep bunker between sample points.
                for triangle in [[a,a+1,b],[a+1,b+1,b]] where !triangle.allSatisfy(coveredByPlayableMesh) {
                    indices += triangle
                }
            }
        }
        let metadata=Array(repeating:SIMD4<Float>(0,1,0,0),count:vertices.count)
        let metadataSource=SCNGeometrySource(data:metadata.withUnsafeBytes{Data($0)},semantic:.color,
            vectorCount:vertices.count,usesFloatComponents:true,componentsPerVector:4,bytesPerComponent:4,dataOffset:0,dataStride:16)
        let tangents:[SIMD4<Float>]=normals.map { value in
            let n=simd_float3(value),t=simd_normalize(simd_float3(n.y,-n.x,0))
            return SIMD4(t.x,t.y,t.z,1)
        }
        let tangentSource=SCNGeometrySource(data:tangents.withUnsafeBytes{Data($0)},semantic:.tangent,
            vectorCount:vertices.count,usesFloatComponents:true,componentsPerVector:4,bytesPerComponent:4,dataOffset:0,dataStride:16)
        return SCNGeometry(sources: [.init(vertices: vertices), .init(normals: normals), .init(textureCoordinates: uv),metadataSource,tangentSource],
                           elements: [SCNGeometryElement(indices: indices, primitiveType: .triangles)])
    }

    /// One sampled terrain mesh, with material assignment from the same surface query used
    /// by flight and rolling. Bunkers are bowls in this mesh, not flat discs above the floor.
    static func playableSurface(_ hole: Hole) -> SCNGeometry {
        let extents=fineSurfaceExtents(hole)
        let minX=extents.x,maxX=extents.y,minD=extents.z,maxD=extents.w
        // A shared regular grid avoids T-junction cracks while resolving bunker lips and
        // authored fairway boundaries more finely. Geometry is generated only on hole load.
        let columns = max(2, Int(ceil((maxX-minX)/0.8))), rows = max(2, Int(ceil((maxD-minD)/0.8)))
        var vertices: [SCNVector3] = [], normals: [SCNVector3] = [], uv: [CGPoint] = []
        var groups = Array(repeating: [Int32](), count: 8)
        var surfaceData:[Float]=[]
        var labels: [Int] = []
        func materialIndex(_ point: CoursePoint) -> Int {
            let lie=hole.lie(at:point)
            if lie != .water && lie != .bunker {
                for lake in hole.hazards where lake.kind == .water {
                    let radius=lake.radialSurface(at:point).radius
                    let width=1.1/min(lake.width/2,lake.length/2)
                    if radius>1 && radius<1+width { return 7 }
                }
            }
            switch lie {
            case .tee,.fairway: return 0
            case .green: return 1
            case .fringe: return 2
            case .bunker: return 3
            case .water: return 4
            case .deepRough: return 6
            default: return 5
            }
        }
        func appendVertex(_ point: CoursePoint) -> Int32 {
            // Round first: the height query and exported coordinates must agree at a lip.
            let point = CoursePoint(x:Double(Float(point.x)), d:Double(Float(point.d)))
            let surface = hole.surface(at:point), index = Int32(vertices.count)
            vertices.append(SCNVector3(Float(point.x),surfaceHeight(point,hole:hole,
                extents:SIMD4(minX,maxX,minD,maxD),sampled:surface),-Float(point.d)))
            if surface.lie == .outOfBounds {
                // The outer blend is render-only background relief; its normal
                // must describe that rendered height, not the flat solver field.
                let limits=SIMD4(minX,maxX,minD,maxD),e=0.05
                let dx=(surfaceHeight(.init(x:point.x+e,d:point.d),hole:hole,extents:limits)
                    - surfaceHeight(.init(x:point.x-e,d:point.d),hole:hole,extents:limits))/Float(2*e)
                let dd=(surfaceHeight(.init(x:point.x,d:point.d+e),hole:hole,extents:limits)
                    - surfaceHeight(.init(x:point.x,d:point.d-e),hole:hole,extents:limits))/Float(2*e)
                normals.append(SCNVector3(simd_normalize(simd_float3(-dx,1,dd))))
            } else {
                normals.append(SCNVector3(simd_normalize(simd_float3(Float(-surface.slopeX),1,Float(surface.slopeD)))))
            }
            uv.append(CGPoint(x:point.x/24,y:point.d/24)); labels.append(materialIndex(point))
            var shallow:Float=0,occlusion:Float=1,wetBank:Float=0,sandRim:Float=0
            // Clipped water triangles also share vertices just outside the lie
            // boundary. Depth must be continuous there, not a water-only mask.
            if let lake=hole.hazards.filter({$0.kind == .water}).min(by:{
                abs($0.radialSurface(at:point).radius-1) < abs($1.radialSurface(at:point).radius-1)
            }) {
                let distance=(1-lake.radialSurface(at:point).radius)*min(lake.width,lake.length)/2
                shallow=Float(exp(-max(0,distance)/4))
                wetBank=Float(max(0,min(1,1+distance/1.1)))
            }
            for bunker in hole.hazards where bunker.kind == .bunker {
                let radial=bunker.radialSurface(at:point),radius=radial.radius
                // Gradient converts radial units to approximate yards normal to
                // an irregular edge, including long thin bunker lobes.
                let edgeDistance=abs(1-radius)/max(0.0001,hypot(radial.dx,radial.dd))
                sandRim=max(sandRim,Float(max(0,1-edgeDistance/0.45)))
                if radius<1 {occlusion=Float(0.68+0.32*min(1,radius*radius))}
            }
            surfaceData += [shallow,occlusion,wetBank,sandRim]
            return index
        }
        for row in 0...rows { for column in 0...columns {
            let point = CoursePoint(x:minX+Double(column)/Double(columns)*(maxX-minX), d:minD+Double(row)/Double(rows)*(maxD-minD))
            _ = appendVertex(point)
        } }
        struct CutKey: Hashable { let edge:UInt64; let region:Int }
        var boundaryVertices: [CutKey:Int32] = [:]
        func boundary(_ a: Int32, _ b: Int32, region:Int, inside:(CoursePoint)->Bool) -> Int32 {
            let key = CutKey(edge:UInt64(min(a,b)) << 32 | UInt64(max(a,b)),region:region)
            if let cached = boundaryVertices[key] { return cached }
            let va=vertices[Int(a)], vb=vertices[Int(b)]
            var low=0.0, high=1.0
            func point(_ t: Double) -> CoursePoint {
                CoursePoint(x:Double(va.x)+(Double(vb.x)-Double(va.x))*t,
                    d:-Double(va.z)-(Double(vb.z)-Double(va.z))*t)
            }
            let startInside=inside(point(0))
            for _ in 0..<18 {
                let mid=(low+high)/2
                if inside(point(mid)) == startInside { low=mid } else { high=mid }
            }
            let index=appendVertex(point((low+high)/2)); boundaryVertices[key]=index; return index
        }
        func emit(_ triangle: [Int32]) {
            let kinds = Set(triangle.map { labels[Int($0)] })
            if kinds.count == 1 { groups[labels[Int(triangle[0])]].append(contentsOf:triangle); return }
            // Partition in the same priority order as Hole.lie. Each implicit
            // boundary is clipped separately, including three-way junctions.
            // The old centroid fan painted sand spikes across green/fringe
            // because a triangle's centroid is not its material intersection.
            var remaining=triangle
            func take(_ material:Int, region:Int, inside:(CoursePoint)->Bool) {
                guard remaining.count>=3 else {return}
                var selected:[Int32]=[],outside:[Int32]=[]
                func point(_ index:Int32)->CoursePoint {
                    let v=vertices[Int(index)];return CoursePoint(x:Double(v.x),d:-Double(v.z))
                }
                for i in remaining.indices {
                    let a=remaining[i],b=remaining[(i+1)%remaining.count]
                    let ia=inside(point(a)),ib=inside(point(b))
                    if ia {selected.append(a)} else {outside.append(a)}
                    if ia != ib {
                        let cut=boundary(a,b,region:region,inside:inside)
                        selected.append(cut);outside.append(cut)
                    }
                }
                if selected.count>=3 { for i in 1..<selected.count-1 {
                    groups[material].append(contentsOf:[selected[0],selected[i],selected[i+1]])
                } }
                remaining=outside
            }
            for (index,hazard) in hole.hazards.enumerated() {
                take(hazard.kind == .water ? 4 : 3,region:index,inside:hazard.contains)
            }
            take(7,region:100) { point in
                hole.hazards.contains { lake in
                    guard lake.kind == .water else {return false}
                    let radius=lake.radialSurface(at:point).radius
                    return radius<1+1.1/min(lake.width/2,lake.length/2)
                }
            }
            take(1,region:101) { hole.greenBoundary?.contains($0) ?? ($0.distance(to:hole.pin)<=hole.greenRadius) }
            take(2,region:102) { (hole.greenBoundary?.distance(to:$0) ?? .infinity)<=2 }
            take(0,region:103) { point in
                point.distance(to:hole.tee)<=4 || (hole.fairwayBoundary?.contains(point) ?? (hole.distanceFromCenterline(point)<=hole.fairwayWidth/2))
            }
            if let fairway=hole.fairwayBoundary {
                take(5,region:104) { fairway.distance(to:$0)<=12 }
                take(6,region:105) { fairway.distance(to:$0)<=Hole.roughWidth }
            }
            if remaining.count>=3 { for i in 1..<remaining.count-1 {
                groups[5].append(contentsOf:[remaining[0],remaining[i],remaining[i+1]])
            } }
        }
        for row in 0..<rows { for column in 0..<columns {
            let a=Int32(row*(columns+1)+column), b=a+Int32(columns+1)
            for triangle in [[a,a+1,b],[a+1,b+1,b]] {
                emit(triangle)
            }
        } }
        let dataSource=SCNGeometrySource(data:surfaceData.withUnsafeBytes{Data($0)},semantic:.color,
            vectorCount:vertices.count,usesFloatComponents:true,componentsPerVector:4,bytesPerComponent:4,dataOffset:0,dataStride:16)
        // Analytic +u tangent for the world-x/world-distance UV mapping. SceneKit's
        // automatic tangent builder can fail on clipped boundary triangles and
        // empty hazard material groups (not every hole has a lake).
        let tangents:[SIMD4<Float>]=normals.map { value in
            let n=simd_float3(value)
            let t=simd_normalize(simd_float3(n.y,-n.x,0))
            return SIMD4(t.x,t.y,t.z,1)
        }
        let tangentSource=SCNGeometrySource(data:tangents.withUnsafeBytes{Data($0)},semantic:.tangent,
            vectorCount:vertices.count,usesFloatComponents:true,componentsPerVector:4,bytesPerComponent:4,dataOffset:0,dataStride:16)
        let geometry = SCNGeometry(sources:[.init(vertices:vertices),.init(normals:normals),.init(textureCoordinates:uv),dataSource,tangentSource],
            elements:groups.map { SCNGeometryElement(indices:$0,primitiveType:.triangles) })
        geometry.materials=[fairwayMaterial,greenMaterial,fringeMaterial,sandMaterial,waterMaterial,roughMaterial,deepRoughMaterial,shoreMaterial]
        return geometry
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

    /// Original lobed canopy mesh: overlapping leaf masses without perfect ball silhouettes.
    /// Shared by decorative trees and the crowns above authoritative collision trunks.
    static func foliage(radius: Double, seed: Int, rows:Int = 16, columns:Int = 24, tapered:Bool = false) -> SCNGeometry {
        var vertices: [SCNVector3] = [], normals: [SCNVector3] = [], indices: [Int32] = []
        var colors: [Float] = []
        var texcoords: [CGPoint] = []
        func point(_ latitude: Double, _ longitude: Double) -> simd_float3 {
            let bulge = 1 + sin(latitude) * (0.035 * sin(longitude * 5 + Double(seed))
                + 0.025 * cos(latitude * 7 + longitude * 3))
            let taper = tapered ? 0.88-0.16*cos(latitude) : 1
            return simd_float3(Float(sin(latitude)*cos(longitude)*taper),Float(cos(latitude)),Float(sin(latitude)*sin(longitude)*taper)) * Float(radius*bulge)
        }
        for row in 0...rows { for column in 0...columns {
            let latitude = Double(row) / Double(rows) * .pi
            let longitude = Double(column) / Double(columns) * 2 * .pi
            let p = point(latitude,longitude)
            let tangent = point(latitude,longitude+0.001)-point(latitude,longitude-0.001)
            let vertical = point(latitude+0.001,longitude)-point(latitude-0.001,longitude)
            let n = simd_cross(tangent,vertical)
            vertices.append(SCNVector3(p))
            texcoords.append(CGPoint(x:Double(column)/Double(columns),y:Double(row)/Double(rows)))
            normals.append(SCNVector3(simd_length(n) > 0.000001 ? simd_normalize(n) : simd_normalize(p)))
            // Stable local canopy occlusion. Baked per vertex, so it costs no
            // full-screen pass and cannot shimmer as the camera or TV moves.
            let height=Float((cos(latitude)+1)/2)
            let shade = 0.70 + 0.30 * pow(height,0.65)
            colors += [shade*(0.91+0.09*height),shade,shade*(1.06-0.12*height),1]
        } }
        for row in 0..<rows { for column in 0..<columns {
            let a = Int32(row * (columns + 1) + column), b = a + Int32(columns + 1)
            indices += [a, a + 1, b, a + 1, b + 1, b]
        } }
        let colorSource = SCNGeometrySource(data:colors.withUnsafeBytes{Data($0)},semantic:.color,
            vectorCount:vertices.count,usesFloatComponents:true,componentsPerVector:4,
            bytesPerComponent:4,dataOffset:0,dataStride:16)
        return SCNGeometry(sources: [.init(vertices: vertices), .init(normals: normals), .init(textureCoordinates:texcoords), colorSource],
            elements: [SCNGeometryElement(indices: indices, primitiveType: .triangles)])
    }

    static func plantTrees(along hole: Hole, parent: SCNNode) {
        var shadowCenters:[CoursePoint]=[]
        let lodgeSite=clubhouseSite(hole)
        let route=resortRoute(hole)
        func tree(at point: CoursePoint, index: Int) {
            let scale = Float(0.85 + Double(index % 5) * 0.09)
            // Decorative trunks/crowns must not appear in water or a playable lie.
            guard clearsResortRoute(point,radius:1.2,route:route),treeClearsClubhouse(point,radius:4*Double(scale),site:lodgeSite),hole.lie(at:point) == .outOfBounds,
                (0..<16).allSatisfy({ sample in
                    let angle=Double(sample)*Double.pi/8,radius=4*Double(scale)
                    return hole.lie(at:CoursePoint(x:point.x+cos(angle)*radius,d:point.d+sin(angle)*radius)) == .outOfBounds
                }) else { return }
            let tree = trees[index % trees.count].clone()
            tree.position = SCNVector3(Float(point.x), dressingHeight(point, hole: hole), -Float(point.d))
            tree.scale = SCNVector3(scale, scale * 1.25, scale)
            tree.simdOrientation = simd_quatf(angle: Float(index) * 1.7, axis: simd_float3(0,1,0))
            parent.addChildNode(tree)
            shadowCenters.append(point)
        }
        let edge = hole.fairwayWidth / 2 + Hole.roughWidth + 5
        var index = 0
        for (a,b) in zip(hole.centerline, hole.centerline.dropFirst()) {
            let length = max(0.001, a.distance(to: b))
            let ux = (b.x-a.x)/length, ud = (b.d-a.d)/length
            var along = 6.0
            while along < length {
                for side in [-1.0,1.0] {
                    let offset = edge + Double(index * 7 % 16)
                    let point = CoursePoint(x:a.x+ux*along+ud*offset*side,d:a.d+ud*along-ux*offset*side)
                    if hole.distanceFromCenterline(point) >= edge-0.5, point.distance(to:hole.pin) > hole.greenRadius+12 {
                        tree(at:point,index:index)
                    }
                    index += 1
                }
                along += 12 + Double(index * 11 % 13)
            }
        }
        let pin = hole.pin
        let direction = hole.centerline[hole.centerline.count-2].heading(to:pin) * .pi/180
        for i in -4...4 {
            let back = hole.greenRadius+Hole.roughWidth+10+Double(abs(i)*3), side = Double(i)*11
            tree(at:CoursePoint(x:pin.x+sin(direction)*back+cos(direction)*side,
                d:pin.d+cos(direction)*back-sin(direction)*side),index:index+i+4)
        }
        parent.addChildNode(canopyShadows(shadowCenters,hole:hole))
        // Depth-layered outer groves from the establishing studies. Stay entirely
        // off scored ground, share prototype geometry, and batch shadows once.
        var outerShadows:[CoursePoint]=[]
        for (segment,pair) in zip(hole.centerline,hole.centerline.dropFirst()).enumerated() {
            let (a,b)=pair,length=max(1,a.distance(to:b)),ux=(b.x-a.x)/length,ud=(b.d-a.d)/length
            for row in 0..<2 { for station in 0..<max(1,Int(length/22)) { for side in [-1.0,1.0] {
                let along=Double(station)*22+Double(row)*11+8
                let offset=edge+18+Double(row)*19+sin(Double(station+segment*9)*2.4)*8
                let p=CoursePoint(x:a.x+ux*along+ud*offset*side,d:a.d+ud*along-ux*offset*side)
                guard clearsResortRoute(p,radius:1.2,route:route),treeClearsClubhouse(p,radius:7,site:lodgeSite),hole.lie(at:p) == .outOfBounds,
                    (0..<8).allSatisfy({j in hole.lie(at:CoursePoint(x:p.x+cos(Double(j)*Double.pi/4)*7,d:p.d+sin(Double(j)*Double.pi/4)*7)) == .outOfBounds}) else {continue}
                let index=station+segment*7+row*3
                let model=trees[index%trees.count].clone();model.name="sunwardOuterGrove"
                let size=Float(1.05+Double(index%5)*0.12)
                model.simdScale=simd_float3(size,size * 1.25,size)
                model.position=SCNVector3(Float(p.x),dressingHeight(p,hole:hole),-Float(p.d))
                model.simdOrientation=simd_quatf(angle:Float(index)*2.4,axis:simd_float3(0,1,0))
                parent.addChildNode(model);outerShadows.append(p)
            } } }
        }
        parent.addChildNode(canopyShadows(outerShadows,hole:hole))
        let backdrop=backdropGrovePoints(hole)
        for (index,point) in backdrop.enumerated() {
            guard clearsResortRoute(point,radius:1.2,route:route) else {continue}
            let model=trees[index%trees.count].clone()
            model.name="sunwardBackdropGrove"
            model.position=SCNVector3(Float(point.x),dressingHeight(point,hole:hole),-Float(point.d))
            let size=Float(1.05+Double(index%5)*0.16)
            model.simdScale=simd_float3(size,size * 1.25,size)
            model.eulerAngles.y=Float(index)*2.399
            parent.addChildNode(model)
        }
        parent.addChildNode(canopyShadows(backdrop.filter {clearsResortRoute($0,radius:1.2,route:route)},hole:hole))
    }

    /// One draw call for soft terrain-conforming canopy shade beyond the real-time shadow
    /// range. No changed terrain/colliders. Vertex alpha fades the edges, never opaque discs.
    static func canopyShadows(_ centers:[CoursePoint],hole:Hole)->SCNNode {
        var vertices:[SCNVector3]=[],colors:[Float]=[],indices:[Int32]=[],uv:[CGPoint]=[]
        // Project the approximate canopy center with the same sun as the sky
        // and real-time light; distant shade must not point in another direction.
        let offset=simd_float2(-sunDirection.x,sunDirection.z)*(5/max(0.1,sunDirection.y))
        for center in centers {
            let base=Int32(vertices.count)
            for ring in 0...6 { for col in 0..<32 {
                let r=Double(ring)/6,theta=Double(col)/32 * 2 * .pi
                let p=CoursePoint(x:center.x+Double(offset.x)+cos(theta)*6.8*r,d:center.d+Double(offset.y)+sin(theta)*4.8*r)
                vertices.append(SCNVector3(Float(p.x),dressingHeight(p,hole:hole)+0.025,-Float(p.d)))
                let alpha=Float((1-r*r)*(1-r*r)*0.23)
                colors += [0.08,0.16,0.14,alpha]
                uv.append(CGPoint(x:0,y:0))
                if ring>0 {
                    let a=base+Int32((ring-1)*32+col),b=base+Int32((ring-1)*32+(col+1)%32)
                    indices += [a,b,a+32,b,b+32,a+32]
                }
            } }
            // Local sky occlusion stays beneath the trunk at every view distance.
            // It is separate from sun-projected shade and does not slide with it.
            let rootBase=Int32(vertices.count)
            for ring in 0...4 { for col in 0..<24 {
                let r=Double(ring)/4,theta=Double(col)/24 * 2 * .pi
                let p=CoursePoint(x:center.x+cos(theta)*2.6*r,d:center.d+sin(theta)*2.6*r)
                vertices.append(SCNVector3(Float(p.x),dressingHeight(p,hole:hole)+0.030,-Float(p.d)))
                colors += [0.07,0.12,0.09,Float(pow(1-r*r,3)*0.16)]
                uv.append(CGPoint(x:1,y:0))
                if ring>0 {
                    let a=rootBase+Int32((ring-1)*24+col),b=rootBase+Int32((ring-1)*24+(col+1)%24)
                    indices += [a,b,a+24,b,b+24,a+24]
                }
            } }
        }
        let colorSource=SCNGeometrySource(data:colors.withUnsafeBytes{Data($0)},semantic:.color,
            vectorCount:vertices.count,usesFloatComponents:true,componentsPerVector:4,
            bytesPerComponent:4,dataOffset:0,dataStride:16)
        let geometry=SCNGeometry(sources:[.init(vertices:vertices),.init(textureCoordinates:uv),colorSource],
            elements:[SCNGeometryElement(indices:indices,primitiveType:.triangles)])
        let material=SCNMaterial();material.lightingModel = .constant
        material.diffuse.contents=UIColor.white;material.blendMode = .alpha
        material.writesToDepthBuffer=false;material.isDoubleSided=true
        material.shaderModifiers=[.geometry:"""
        #pragma body
        float range = length((scn_node.modelViewTransform * _geometry.position).xyz);
        _geometry.color.a *= mix(smoothstep(175.0,225.0,range),1.0,_geometry.texcoords[0].x);
        """]
        geometry.materials=[material]
        let node=SCNNode(geometry:geometry);node.castsShadow=false;node.name="terrainConformingCanopyShade"
        return node
    }

    static let trees: [SCNNode] = (0..<6).map { index in
        // Authored branches with sculpted crowns fitted to the foliage cloud.
        // Shared geometry, no alpha-card canopy overdraw. Keep a fallback for
        // legacy bundles, but acceptance tests require the shipped models.
        if let model=SunwardAsset.prop("NatureTree\(3+index%3)") {
            model.enumerateChildNodes { node,_ in
                for material in node.geometry?.materials ?? [] where material.name == "sculpted-canopy" {
                    material.normal.contents=canopyNormal
                    material.normal.intensity=0.16
                    material.normal.wrapS = .repeat;material.normal.wrapT = .repeat
                }
            }
            model.name="canopyTree"
            return optimized(model)
        }
        return optimized(branchingTree(seed:index))
    }

    static let obstacleBark:SCNMaterial = {
        guard let definition=SunwardAsset.load("NatureTree3")?.materials.first else {return SCNMaterial()}
        return SunwardAsset.material(definition)
    }()

    /// Decorative crowns share the grove asset language, but the authoritative
    /// cylinder remains the exact collision trunk. Normalize the crown alone;
    /// importing the full crooked tree would misrepresent its hit volume.
    static let obstacleCrownGeometry:[SCNGeometry]=(3...5).compactMap { index in
        guard let asset=SunwardAsset.load("NatureTree\(index)"),
              let mesh=asset.meshes.first(where:{asset.materials[$0.material].name == "sculpted-canopy"}) else {return nil}
        let material=SunwardAsset.material(asset.materials[mesh.material])
        material.normal.contents=canopyNormal;material.normal.intensity=0.16
        let geometry=SunwardAsset.geometry(mesh,material:material)
        return geometry
    }

    static func obstacleCrown(radius:Double,seed:Int)->SCNNode {
        guard !obstacleCrownGeometry.isEmpty else {
            return SCNNode(geometry:foliage(radius:radius,seed:seed))
        }
        let geometry=obstacleCrownGeometry[seed%obstacleCrownGeometry.count]
        let (low,high)=geometry.boundingBox
        let center=simd_float3((low.x+high.x)/2,low.y,(low.z+high.z)/2)
        // Reserve a circular footprint even for asymmetrical source crowns.
        var span:Float=0.01
        if let source=geometry.sources(for:.vertex).first {
            source.data.withUnsafeBytes { bytes in
                for index in 0..<source.vectorCount {
                    let offset=source.dataOffset+index*source.dataStride
                    let x=bytes.loadUnaligned(fromByteOffset:offset,as:Float.self)-center.x
                    let z=bytes.loadUnaligned(fromByteOffset:offset+8,as:Float.self)-center.z
                    span=max(span,hypot(x,z))
                }
            }
        }
        let scale=simd_float3(Float(radius)/span,Float(radius*1.65)/max(0.01,high.y-low.y),Float(radius)/span)
        let node=SCNNode(geometry:geometry)
        node.simdPivot=simd_float4x4(SCNMatrix4MakeTranslation(center.x,center.y,center.z))
        node.simdScale=scale
        return node
    }

    static let canopyMaterials:[SCNMaterial]=(0..<6).map { seed in
        let crownMaterial=SCNMaterial();crownMaterial.lightingModel = .physicallyBased
        crownMaterial.name="Sunward foliage \(seed)"
        crownMaterial.diffuse.contents=UIColor(red:0.36+Double(seed%3)*0.020,
            green:0.51+Double(seed%2)*0.025,blue:0.17+Double(seed%3)*0.006,alpha:1)
        crownMaterial.roughness.contents=0.92
        crownMaterial.normal.contents=canopyNormal
        crownMaterial.normal.wrapS = .repeat;crownMaterial.normal.wrapT = .repeat
        crownMaterial.normal.contentsTransform=SCNMatrix4MakeScale(2,2,1)
        crownMaterial.normal.intensity=0.16
        return crownMaterial
    }

    /// Film-directed sculpted crowns with fine normal detail. Geometry is built
    /// once per prototype and instanced; no transparent leaf overdraw.
    static func branchingTree(seed:Int) -> SCNNode {
        let tree=SCNNode();tree.name="canopyTree"
        let crownMaterial=canopyMaterials[seed%canopyMaterials.count]
        let bark=SCNMaterial();bark.lightingModel = .physicallyBased
        bark.diffuse.contents=UIColor(red:0.32,green:0.23,blue:0.14,alpha:1)
        bark.roughness.contents=0.95
        func branch(_ from:simd_float3,_ to:simd_float3,_ radius:CGFloat) {
            let direction=to-from
            let shape=SCNCone(topRadius:radius*0.55,bottomRadius:radius,height:CGFloat(simd_length(direction)))
            shape.radialSegmentCount=8;shape.materials=[bark]
            let node=SCNNode(geometry:shape);node.simdPosition=(from+to)/2
            node.simdOrientation=simd_quatf(from:simd_float3(0,1,0),to:simd_normalize(direction))
            tree.addChildNode(node)
        }
        branch(simd_float3(0,0,0),simd_float3(0.3,6.5,0),0.46)
        for crown in 0..<4 {
            let angle=Float(crown)*2.399+Float(seed)*0.7
            let radius:Float=crown<3 ? 1.20 : 0.18
            let center=simd_float3(cos(angle)*radius,Float(crown<3 ? 4.5 : 6.2)+Float(crown%3)*0.16,sin(angle)*radius)
            branch(simd_float3(0.2,3.8+Float(crown%3)*0.5,0),center,0.16)
            let core=foliage(radius:crown<3 ? 2.15 : 1.95,seed:seed+crown,rows:20,columns:28,tapered:crown == 3)
            core.materials=[crownMaterial]
            let node=SCNNode(geometry:core);node.simdPosition=center
            node.simdScale=simd_float3(1.07,0.94+Float(seed%3)*0.03,1)
            tree.addChildNode(node)
        }
        return tree
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

    /// A terrain-draped warm stone path from the approved resort kit. It is
    /// decoration outside all playable lies, not a new terrain/collision rule.
    static func resortPath(_ hole: Hole) -> SCNNode {
        if hole.fairwayBoundary != nil {return continuousResortPath(hole)}
        var vertices:[SCNVector3]=[], normals:[SCNVector3]=[], colors:[Float]=[], indices:[Int32]=[]
        var previousRow: Int32?
        let offset = hole.fairwayWidth / 2 + Hole.roughWidth + 16
        for (a,b) in zip(hole.centerline,hole.centerline.dropFirst()) {
            previousRow = nil
            let length = max(0.01,a.distance(to:b)), ux=(b.x-a.x)/length, ud=(b.d-a.d)/length
            let count = max(2,Int(ceil(length/1.2)))
            for step in 0...count {
                let t=Double(step)/Double(count)
                let wander=2.2*sin((a.d+(b.d-a.d)*t)*0.035)
                let center=CoursePoint(x:a.x+(b.x-a.x)*t-ud*(offset+wander), d:a.d+(b.d-a.d)*t+ux*(offset+wander))
                let points=(0...4).map { column in
                    let across=(Double(column)/4-0.5)*2.6
                    return CoursePoint(x:center.x+ud*across,d:center.d-ux*across)
                }
                guard points.allSatisfy({hole.lie(at:$0) == .outOfBounds}),
                    hole.trees.allSatisfy({center.distance(to:$0.center)>$0.trunkRadius+2}) else {
                    previousRow=nil;continue
                }
                let row=Int32(vertices.count)
                for (column,p) in points.enumerated() {
                    let height=dressingHeight(p,hole:hole)
                    let dx=dressingHeight(CoursePoint(x:p.x+0.1,d:p.d),hole:hole)-height
                    let dz=dressingHeight(CoursePoint(x:p.x,d:p.d-0.1),hole:hole)-height
                    vertices.append(SCNVector3(Float(p.x),height+0.04,-Float(p.d)))
                    normals.append(SCNVector3(simd_normalize(simd_float3(-dx/0.1,1,-dz/0.1))))
                    let shade:Float = column == 0 || column == 4 ? 0.77 : 1
                    colors += [shade,shade,shade,1]
                }
                if let prior=previousRow { for c:Int32 in 0..<4 {
                    let a=prior+c,b=row+c
                    indices += [a,b,a+1,a+1,b,b+1]
                } }
                previousRow=row
            }
        }
        let colorSource=SCNGeometrySource(data:colors.withUnsafeBytes{Data($0)},semantic:.color,
            vectorCount:vertices.count,usesFloatComponents:true,componentsPerVector:4,bytesPerComponent:4,dataOffset:0,dataStride:16)
        let geometry=SCNGeometry(sources:[.init(vertices:vertices),.init(normals:normals),colorSource],
            elements:[SCNGeometryElement(indices:indices,primitiveType:.triangles)])
        let material=SCNMaterial();material.lightingModel = .physicallyBased
        material.diffuse.contents=UIColor(red:0.78,green:0.66,blue:0.49,alpha:1)
        material.roughness.contents=0.96;material.isDoubleSided=true
        geometry.materials=[material]
        let node=SCNNode(geometry:geometry);node.name="sunwardResortPath";node.castsShadow=false
        return node
    }

    static func dress(_ hole: Hole, parent: SCNNode) {
        let lodgeSite=clubhouseSite(hole)
        let route=resortRoute(hole)
        parent.addChildNode(resortPath(hole))
        parent.addChildNode(livingTurf(hole))
        parent.addChildNode(bunkerEdgeTurf(hole))
        parent.addChildNode(bunkerSoilCollar(hole))
        parent.addChildNode(shorelinePlanting(hole))
        parent.addChildNode(shorelineRocks(hole))
        parent.addChildNode(borderPlanting(hole))
        parent.addChildNode(woodland(hole))
        let ivory = UIColor(red: 0.96, green: 0.94, blue: 0.83, alpha: 1)
        // Decorative clusters never occupy a scored lie or an authoritative tree trunk.
        for (index, point) in decorationPoints(hole).enumerated() {
            // Layered groves break up the old evenly spaced tree-row silhouette. Every
            // trunk and its canopy footprint stay beyond scored ground.
            for member in 0..<3 {
                let angle=Double(index)*2.4+Double(member)*2.1
                let p=CoursePoint(x:point.x+cos(angle)*7,d:point.d+sin(angle)*7)
                let clear=(0..<8).allSatisfy { sample in
                    let theta=Double(sample) * .pi / 4
                    return hole.lie(at:CoursePoint(x:p.x+cos(theta)*6,d:p.d+sin(theta)*6)) == .outOfBounds
                }
                guard clear, clearsResortRoute(p,radius:1.2,route:route),treeClearsClubhouse(p,radius:6,site:lodgeSite),hole.lie(at:p) == .outOfBounds else { continue }
                let tree=trees[(index+member)%trees.count].clone()
                let size=Float(0.82+Double((index+member)%4)*0.17)
                tree.scale=SCNVector3(size,size * 1.25,size)
                tree.position=SCNVector3(Float(p.x),dressingHeight(p,hole:hole),-Float(p.d))
                tree.simdOrientation=simd_quatf(angle:Float(angle),axis:simd_float3(0,1,0))
                tree.name="sunwardGroveTree"
                parent.addChildNode(tree)
            }
            if clearsResortRoute(point,radius:5.5,route:route),let rocks = rockKit?.clone() {
                rocks.position = SCNVector3(Float(point.x), dressingHeight(point, hole: hole), -Float(point.d))
                let size = Float(1.6 + Double(index % 3) * 0.35)
                rocks.scale = SCNVector3(size, size, size)
                rocks.eulerAngles.y = Float(index) * 1.9
                parent.addChildNode(rocks)
            }
            for bush in 0..<3 {
                let p = CoursePoint(x: point.x + Double(bush-1) * 1.5, d: point.d + 2)
                guard clearsResortRoute(p,radius:1.7,route:route),hole.lie(at:p) == .outOfBounds else { continue }
                let shrub = part(foliage(radius:1.5,seed:index+bush),
                    color:UIColor(red:0.24,green:0.48,blue:0.27,alpha:1),
                    position:SCNVector3(Float(p.x),dressingHeight(p,hole:hole)+0.7,-Float(p.d)),parent:parent)
                shrub.scale = SCNVector3(1,0.65,1)
            }
        }
        // One pair of gameplay tee markers is built in CourseScene.load. Do not add
        // a second unrelated blue pair on top of the approved resort furniture.
        let sign = SCNNode()
        sign.name="sunwardTeeSign"
        // The original three-yard board towered over the golfer and filled
        // a gameplay-camera edge. A waist-height course marker fits the rig scale.
        sign.simdScale=simd_float3(repeating:0.45)
        sign.position = SCNVector3(hole.tee.x + 9,
            hole.surface(at: CoursePoint(x: hole.tee.x + 9, d: hole.tee.d - 2)).heightYards, -hole.tee.d + 2)
        _ = part(SCNCylinder(radius: 0.12, height: 2.8), color: ivory, position: SCNVector3(0, 1, 0), parent: sign)
        _ = part(SCNBox(width: 2.5, height: 1.6, length: 0.18, chamferRadius: 0.12),
            color: UIColor(red: 0.10, green: 0.26, blue: 0.21, alpha: 1), position: SCNVector3(0, 2.2, 0), parent: sign)
        let text = SCNText(string: String(format: "%02d", hole.number), extrusionDepth: 0.015)
        text.font = UIFont.systemFont(ofSize: 1, weight: .bold)
        text.flatness = 0.2
        let number = part(text, color: ivory, position: SCNVector3(-0.7, 1.73, 0.12), parent: sign)
        number.scale = SCNVector3(0.8, 0.8, 0.8)
        // Keep this tiny hierarchy explicit: a lazy flattened clone can expose
        // an empty bounding box before its first render, breaking culling/review.
        parent.addChildNode(sign)
        let edge = hole.fairwayWidth / 2 + Hole.roughWidth + 3
        if hole.fairwayBoundary == nil {
        for (a, b) in zip(hole.centerline, hole.centerline.dropFirst()) {
            let distance = max(1, a.distance(to: b))
            let offsetX = (b.d - a.d) / distance * edge, offsetD = -(b.x - a.x) / distance * edge
            let path = SCNNode(geometry: capsule(from: CoursePoint(x: a.x + offsetX, d: a.d + offsetD),
                to: CoursePoint(x: b.x + offsetX, d: b.d + offsetD), width: 3.2, y: 0.025, terrain: hole.terrain,
                height: { dressingHeight($0, hole: hole) }))
            path.geometry?.firstMaterial?.diffuse.contents = UIColor(red: 0.72, green: 0.70, blue: 0.59, alpha: 1)
            path.name = "cartPath"
            parent.addChildNode(path)
        }
        }
        // A visible destination beyond the green. Footprint checks keep every
        // wall/step outside scored terrain, including unusual doglegs.
        let lodge = SCNNode()
        let lodgePoint=lodgeSite
        lodge.name="sunwardClubhouse"
        lodge.position = SCNVector3(Float(lodgePoint.x),dressingHeight(lodgePoint,hole:hole),-Float(lodgePoint.d))
        _ = part(SCNBox(width: 22, height: 8, length: 12, chamferRadius: 0.5), color: ivory,
                 position: SCNVector3(0, 4, 0), parent: lodge)
        _ = part(SCNBox(width: 22.2, height: 4, length: 12.2, chamferRadius: 0.1),
            color: UIColor(red: 0.55, green: 0.55, blue: 0.48, alpha: 1),
            position: SCNVector3(0, -2, 0), parent: lodge)
        let roof = part(gableRoof(width:27,height:6,depth:17),
                        color: UIColor(red: 0.76, green: 0.31, blue: 0.21, alpha: 1), position: SCNVector3(0, 8, 0), parent: lodge)
        roof.geometry?.materials=[roofTileMaterial]
        roof.name = "clubhouseRoof"
        for x in [-7.0,0,7] {
            _ = part(SCNBox(width:3.6,height:2.8,length:2.6,chamferRadius:0.12),color:ivory,
                position:SCNVector3(x,11.6,4.3),parent:lodge)
            let dormerRoof = part(gableRoof(width:3.2,height:1.8,depth:4.1),color:ivory,
                position:SCNVector3(x,13,4.3),parent:lodge)
            dormerRoof.eulerAngles.y = .pi/2
            dormerRoof.geometry?.materials=[roofTileMaterial]
            _ = part(SCNBox(width:1.6,height:1.9,length:0.12,chamferRadius:0.12),
                color:UIColor(red:0.24,green:0.39,blue:0.42,alpha:1),position:SCNVector3(x,11.7,5.65),parent:lodge)
            _ = part(SCNBox(width:0.12,height:2,length:0.18,chamferRadius:0.02),color:ivory,
                position:SCNVector3(x,11.7,5.75),parent:lodge)
        }
        for x in [-7.0, 7] {
            _ = part(SCNBox(width: 3.8, height: 3.4, length: 0.15, chamferRadius: 0.12),
                     color: UIColor(red: 0.26, green: 0.48, blue: 0.55, alpha: 1), position: SCNVector3(x, 4.5, 6.1), parent: lodge)
        }
        // Reference-matched resort portico: a real modeled facade, not the old blank box.
        _ = part(SCNBox(width:25,height:0.45,length:6,chamferRadius:0.12),color:ivory,
            position:SCNVector3(0,0.20,8.7),parent:lodge)
        _ = part(SCNBox(width:25,height:0.6,length:5.5,chamferRadius:0.12),color:ivory,
            position:SCNVector3(0,7,8.5),parent:lodge)
        for x in [-10.5,-3.5,3.5,10.5] {
            _ = part(SCNCylinder(radius:0.34,height:6.8),color:ivory,position:SCNVector3(x,3.6,10.4),parent:lodge)
            _ = part(SCNBox(width:1,height:0.8,length:1,chamferRadius:0.14),
                color:UIColor(red:0.79,green:0.48,blue:0.34,alpha:1),position:SCNVector3(x,0.6,10.4),parent:lodge)
        }
        for x in [-7.0,7] {
            for offset in [-1.9,1.9] {
                _ = part(SCNBox(width:0.14,height:3.6,length:0.22,chamferRadius:0.03),color:ivory,
                    position:SCNVector3(x+offset,4.5,6.2),parent:lodge)
            }
            _ = part(SCNBox(width:0.10,height:3.4,length:0.22,chamferRadius:0.02),color:ivory,
                position:SCNVector3(x,4.5,6.2),parent:lodge)
            _ = part(SCNBox(width:4,height:0.14,length:0.22,chamferRadius:0.03),color:ivory,
                position:SCNVector3(x,4.5,6.2),parent:lodge)
            _ = part(SCNBox(width:4.4,height:0.65,length:1.15,chamferRadius:0.15),
                color:UIColor(red:0.67,green:0.34,blue:0.22,alpha:1),position:SCNVector3(x,2.55,6.5),parent:lodge)
            for flower in -2...2 {
                _ = part(foliage(radius:0.45,seed:flower+3),color:UIColor(red:0.85,green:0.47,blue:0.38,alpha:1),
                    position:SCNVector3(x+Double(flower)*0.7,3,6.5),parent:lodge)
            }
        }
        for x in [-8.0,8] {
            _ = part(SCNBox(width:1.35,height:5,length:1.35,chamferRadius:0.15),color:ivory,
                position:SCNVector3(x,12,0),parent:lodge)
            _ = part(SCNBox(width:1.85,height:0.4,length:1.85,chamferRadius:0.1),color:ivory,
                position:SCNVector3(x,14.4,0),parent:lodge)
        }
        lodge.addChildNode(clubhouseFacadeDetails())
        parent.addChildNode(optimized(lodge))
        if let courtyardTree=SunwardAsset.prop("SunwardTree") {
            courtyardTree.position=SCNVector3(Float(lodgePoint.x-17),
                dressingHeight(CoursePoint(x:lodgePoint.x-17,d:lodgePoint.d),hole:hole),-Float(lodgePoint.d))
            courtyardTree.simdScale=simd_float3(repeating:0.7)
            parent.addChildNode(optimized(courtyardTree))
        }
        // Distant atmosphere belongs in the sky, not opaque white sphere props.
    }

    private static let rockKit: SCNNode? = {
        guard let source = SunwardAsset.prop("SunwardRocks") else { return nil }
        return optimized(source)
    }()
    /// Reference-inspired tee → fairway → green tour of the actual course. Arc-length
    /// sampling follows doglegs; it never pretends the generated movie is the map.
    static func flyover(_ hole:Hole,progress:Double)->(position:simd_float3,lookAt:simd_float3) {
        let t=max(0,min(1,progress)), points=hole.centerline
        let lengths=zip(points,points.dropFirst()).map {$0.distance(to:$1)}
        let total=lengths.reduce(0,+), distance=total*(t*t*(3-2*t))
        func sample(_ distance:Double)->CoursePoint {
            var remaining=max(0,min(total,distance))
            for index in lengths.indices {
                let u=min(1,remaining/max(0.001,lengths[index]))
                if remaining<=lengths[index] || index == lengths.count-1 {
                    return CoursePoint(x:points[index].x+(points[index+1].x-points[index].x)*u,
                        d:points[index].d+(points[index+1].d-points[index].d)*u)
                }
                remaining-=lengths[index]
            }
            return points[0]
        }
        let point=sample(distance)
        // Look across a 36-yard window so dogleg vertices don't snap the camera.
        let heading=sample(distance-18).heading(to:sample(distance+18))
        let angle=Float(heading*Double.pi/180),height=Float(hole.surface(at:point).heightYards)
        let forward=simd_float3(sin(angle),0,-cos(angle)),right=simd_float3(cos(angle),0,sin(angle))
        let target=simd_float3(Float(point.x),height,-Float(point.d))
        let eye=target-forward*35+right*Float(24-10*t)+simd_float3(0,Float(25-8*t),0)
        return (eye,target+forward*14)
    }
    static func decorationPoints(_ hole: Hole) -> [CoursePoint] {
        let offset = hole.fairwayWidth / 2 + Hole.roughWidth + 12
        return hole.centerline.enumerated().flatMap { index, p in
            [-1.0,1.0].map { CoursePoint(x:p.x+$0*offset,d:p.d+Double(index%2)*9) }
        }.filter { point in
            hole.lie(at:point) == .outOfBounds &&
                hole.trees.allSatisfy { point.distance(to:$0.center) > $0.trunkRadius+4 }
        }
    }
}

/// Bound GPU pixels independently of UIKit's native screen resolution. In TV mode
/// the phone is a responsive companion preview, not a second full-resolution TV.
/// This policy never changes the 60 Hz scene clock or camera/shot processing.
struct CourseRenderPolicy: Equatable {
    let framesPerSecond: Int
    let maximumPixelDimension: CGFloat
    static func phone(externalDisplayActive: Bool) -> Self {
        .init(framesPerSecond: externalDisplayActive ? 30 : 60,
              maximumPixelDimension: externalDisplayActive ? 960 : 1920)
    }
    static let television = Self(framesPerSecond: 60, maximumPixelDimension: 1920)
    func scale(for size: CGSize, nativeScale: CGFloat) -> CGFloat {
        guard max(size.width, size.height) > 0 else { return nativeScale }
        return min(nativeScale, maximumPixelDimension / max(size.width, size.height))
    }
}

final class CourseRenderView: SCNView {
    var renderPolicy = CourseRenderPolicy.phone(externalDisplayActive: false) {
        didSet {
            guard oldValue != renderPolicy else { return }
            applyRenderPolicy()
        }
    }
    override func didMoveToWindow() {
        super.didMoveToWindow()
        applyRenderPolicy()
    }
    override func layoutSubviews() {
        super.layoutSubviews()
        applyRenderPolicy()
    }
    private func applyRenderPolicy() {
        preferredFramesPerSecond = renderPolicy.framesPerSecond
        antialiasingMode = .multisampling2X
        let scale = renderPolicy.scale(for: bounds.size, nativeScale: window?.screen.scale ?? traitCollection.displayScale)
        if abs(contentScaleFactor - scale) > 0.001 { contentScaleFactor = scale }
    }
    override var accessibilityElements: [Any]? {
        get { [] }
        set { }
    }
}
