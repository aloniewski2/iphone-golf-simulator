import SwiftUI
import SceneKit

/// Catalog shared with TennisCustomization.
enum CharacterOptions {
    static let hair = ["Bald", "Swept", "Curls", "Bob"]
    static let hairColors = ["Black", "Brown", "Auburn", "Blond", "Silver", "Blue"]
    static let hairHex = ["211C1A", "593722", "A54D2B", "D8B365", "BCC0C5", "365D99"]
    static let faces = ["Balanced", "Round", "Long", "Broad"]
    static let heights = ["Short", "Compact", "Medium", "Tall", "Very tall"]
    static let builds = ["Slim", "Lean", "Medium", "Strong", "Broad"]
    static func scale(_ p: Player) -> SCNVector3 {
        let width: Float = 1
        return SCNVector3(width, Float(1), width)
    }
    static func faceScale(_ index: Int) -> SCNVector3 {
        [SCNVector3(1,1,1), SCNVector3(1.12,0.94,1), SCNVector3(0.92,1.08,1), SCNVector3(1.16,1,1)][index]
    }
}

/// Locker framing, like a cosmetics shop: the whole player, or a close-up of the part being changed
/// (head, torso for the kit, feet for shoes, the racket hand for the racket).
enum PreviewFraming: Equatable { case body, head, torso, feet, racket }

struct CharacterModelPreview: UIViewRepresentable {
    let player: Player
    var cameraDistance: Float = 4.8
    var framing: PreviewFraming = .body
    var idleSport: Sport? = nil
    var practiceSequence: Int? = nil
    var menuActivity: ClubPreviewActivity? = nil
    @Environment(\.accessibilityReduceMotion) private var reduceMotion
    func makeCoordinator() -> Coordinator { Coordinator(cameraDistance: cameraDistance) }
    func makeUIView(context: Context) -> SCNView {
        let view = SCNView()
        view.backgroundColor = .clear
        view.scene = context.coordinator.scene
        view.allowsCameraControl = idleSport == nil && menuActivity == nil
        view.defaultCameraController.interactionMode = .orbitTurntable
        view.defaultCameraController.target = SCNVector3(0, 0.95, 0)
        view.defaultCameraController.minimumVerticalAngle = -15
        view.defaultCameraController.maximumVerticalAngle = 30
        view.autoenablesDefaultLighting = false
        view.antialiasingMode = .multisampling4X
        view.accessibilityLabel = idleSport == nil ? "Your 3D character. Drag to rotate." : "Your player warming up"
        context.coordinator.update(player)
        context.coordinator.setFraming(framing, view: view, animated: false)
        updateIdle(view, context: context)
        return view
    }
    func updateUIView(_ view: SCNView, context: Context) {
        context.coordinator.update(player)
        context.coordinator.setFraming(framing, view: view, animated: true)
        updateIdle(view, context: context)
    }
    private func updateIdle(_ view: SCNView, context: Context) {
        if let menuActivity {
            context.coordinator.configureClub(menuActivity, animate: !reduceMotion && !SportsSession.shared.reduceMotion)
            view.isPlaying = !reduceMotion && !SportsSession.shared.reduceMotion
            view.preferredFramesPerSecond = 60
            return
        }
        let animate = idleSport != nil && !reduceMotion && !SportsSession.shared.reduceMotion
        context.coordinator.configureIdle(sport: idleSport, animate: animate)   // (the idle used to stop for the practice swing because it turned the whole character; it is the skeleton now)
        if let sequence = practiceSequence { context.coordinator.practice(sequence) }
        view.isPlaying = animate || practiceSequence != nil
        view.preferredFramesPerSecond = 60   // LOCKER_MIRROR: the idle view runs at the same 60 fps as the club tiles
    }
    static func dismantleUIView(_ view: SCNView, coordinator: Coordinator) {
        view.isPlaying = false
        coordinator.practiceTimer?.invalidate()
        coordinator.clubTimer?.invalidate()
        coordinator.stopMotion()
        coordinator.character.removeAllActions()
        coordinator.scene.rootNode.childNode(withName: "idleBall", recursively: false)?.removeAllActions()
        view.scene = nil
    }

    @MainActor final class Coordinator {
        let scene = SCNScene(), character = SCNNode()
        var framing: PreviewFraming = .body
        private var framed = false
        /// Head centre of the hero on stage (set by update): the head close-up and the style-card thumbnails aim here.
        private(set) var headCentre: Float = 1.27
        var headTarget: SCNVector3 { SCNVector3(0, headCentre, 0) }
        var camera: SCNNode? { scene.rootNode.childNodes.first(where: { $0.camera != nil }) }
        /// The hero on stage (the match hero for the locker's body pick) and its racket (hidden in close-ups, for golf and in the club menus that are not tennis).
        var hero: SCNNode? { character.childNode(withName: MatchHero.rootName, recursively: false) }
        var racket: SCNNode? { hero?.childNode(withName: MatchHero.racketName, recursively: false) }
        func applyFraming() {
            guard let cam = camera else { return }
            framed = true
            racket?.isHidden = framing == .head
            let t = framingTarget(framing)
            switch framing {
            case .head: cam.position = SCNVector3(0, headCentre + 0.04, 1.15)
            case .torso: cam.position = SCNVector3(0, t.y + 0.06, 2.1)
            case .feet: cam.position = SCNVector3(0.15, t.y + 0.22, 1.5)
            case .racket: cam.position = SCNVector3(t.x * 0.5, t.y + 0.08, 1.7)
            case .body: cam.position = SCNVector3(0, 0.9, heroCameraDistance)
            }
            cam.look(at: t)
            if var zoom = serveZoom { zoom.base = cam.simdPosition; serveZoom = zoom }
        }
        /// What the camera looks at in each framing (hero space).
        func framingTarget(_ f: PreviewFraming) -> SCNVector3 {
            switch f {
            case .head: return headTarget
            case .torso: return SCNVector3(0, headCentre - 0.40, 0)
            case .feet: return SCNVector3(0, 0.30, 0)
            case .racket:
                // The racket node's origin is the hero's: aim at the middle of its geometry (the frame and strings), in world space.
                if let r = racket {
                    let (lo, hi) = r.boundingBox
                    if hi.x > lo.x { return r.convertPosition(SCNVector3((lo.x + hi.x) / 2, (lo.y + hi.y) / 2, (lo.z + hi.z) / 2), to: nil) }
                }
                return SCNVector3(0.25, 0.95, 0.2)
            case .body: return SCNVector3(0, 0.76, 0)
            }
        }
        /// 3/4 head shot for the style cards: a metre from the face, a little off centre.
        func headShotCamera() {
            guard let cam = camera else { return }
            cam.position = SCNVector3(0.42, headCentre + 0.05, 0.95); cam.look(at: SCNVector3(0, headCentre - 0.01, 0))
        }
        func setFraming(_ f: PreviewFraming, view: SCNView, animated: Bool) {
            guard f != framing || !framed else { return }
            framing = f
            view.defaultCameraController.target = framingTarget(f)
            view.pointOfView = camera
            SCNTransaction.begin(); SCNTransaction.animationDuration = animated ? 0.45 : 0
            SCNTransaction.animationTimingFunction = CAMediaTimingFunction(name: .easeInEaseOut)
            applyFraming(); SCNTransaction.commit()
        }
        var previous: Player?
        private var lastPractice = 0
        var practiceTimer: Timer?
        /// The tennis idle plays ReadyIdle between practice swings (the practice swing itself is still the Forehand morph on the static Ready stance).
        private var idleWanted = false
        private var settleTask: Task<Void, Never>?
        func practice(_ sequence: Int) {
            guard sequence != lastPractice else { return }; lastPractice = sequence
            guard hero != nil else { return }
            practiceTimer?.invalidate(); practiceTimer = nil; settleTask?.cancel()
            guard rig != nil, var playing = motion else { startMorphPractice(); return }
            // the hero is on its skeleton (ReadyIdle): ease it to the still Ready stance first, then hand over to the morph swing
            playing.settleFrom = CACurrentMediaTime() - motionStart; motion = playing
            let wait = playing.settleDuration
            settleTask = Task { @MainActor [weak self] in
                try? await Task.sleep(for: .seconds(wait))
                if !Task.isCancelled { self?.startMorphPractice() }
            }
        }
        /// One practice swing: the match Forehand as morph targets on the static Ready-stance hero, ending back on the ReadyIdle loop when the tennis idle is on.
        private func startMorphPractice() {
            guard let root = hero else { return }
            stopMotion()   // the morph swing builds on the static Ready stance: no skeleton in its way
            MatchHero.preparePractice(root)
            practiceTimer?.invalidate()
            let start = Date()
            let timer = Timer(timeInterval: 1.0 / 60, repeats: true) { [weak self] _ in
                MainActor.assumeIsolated {
                    guard let self, let root = self.hero else { return }
                    let progress = min(1, Date().timeIntervalSince(start) / 1.5)
                    MatchHero.practiceFrame(root, progress: progress)
                    if progress >= 1 { self.practiceTimer?.invalidate(); self.practiceTimer = nil; self.endMorphPractice() }
                }
            }
            practiceTimer = timer; RunLoop.main.add(timer, forMode: .common)
        }
        private func endMorphPractice() {
            // the swing ends on the still Ready stance (all weights 0): drop the morphers so the skeleton can take over again at ReadyIdle frame 0
            hero?.enumerateChildNodes { node, _ in node.morpher = nil }
            if idleWanted { play(.ready) }
        }
        // MARK: LOCKER_MIRROR - the hero moves by playing clips on its own skeleton, never by turning the character root
        /// The skeleton player on the hero on stage (nil while the hero is the static Ready stance) and what it is playing (nil = a still Ready pose).
        private(set) var rig: HeroRig?
        private(set) var motion: MenuMotion?
        private(set) var motionStart: CFTimeInterval = 0
        /// Seconds into the motion at the last pose (the proof film and the tests read it).
        private(set) var motionTime: Double = 0
        private var displayLink: CADisplayLink?
        /// The play tile's camera pull-back (the Serve reaches 2 m high, the tile frames a 1.4 m hero): the camera position is scaled about the hero's feet by `k` at the Serve's peak, with the
        /// Serve's own blend weight, so the feet stay where they are on screen and nothing leaves the tile. `base` is the camera position with no Serve in the pose.
        private var serveZoom: (base: SIMD3<Float>, k: Float)?

        /// Play `m` on the hero (ReadyIdle loop, or one Serve then the loop); nil leaves the static Ready stance. A display link poses the skeleton every frame.
        func play(_ m: MenuMotion?, keepClock: Bool = false) {
            stopLink(); rig?.detach(); rig = nil
            motion = m
            guard let m, let root = hero, let asset = MatchHero.asset(female: root.value(forKey: "heroFemale") as? Bool ?? false), let r = HeroRig(root: root, asset: asset) else { return }
            r.attach(); rig = r
            serveZoom = m.kind == .serveOnce && framing == .body ? computeServeZoom(root: root, rig: r.data, asset: asset) : nil
            if !keepClock { motionStart = CACurrentMediaTime() }
            pose(at: CACurrentMediaTime() - motionStart)
            let link = CADisplayLink(target: LinkTarget(self), selector: #selector(LinkTarget.fire(_:)))
            link.preferredFrameRateRange = CAFrameRateRange(minimum: 60, maximum: 60, preferred: 60)
            link.add(to: .main, forMode: .common); displayLink = link
        }
        func stopMotion() {
            settleTask?.cancel(); settleTask = nil
            stopLink(); rig?.detach(); rig = nil; motion = nil
            if let zoom = serveZoom { camera?.simdPosition = zoom.base; serveZoom = nil }
        }
        /// How far the camera has to back off (about the feet) for the whole Serve to stay in the tile: the racket (its box, moved by its own track) and the top of the head at every frame of the
        /// clip must stay inside the top of the view (7 % margin).
        private func computeServeZoom(root: SCNNode, rig: RigData, asset: MatchHero.Asset) -> (base: SIMD3<Float>, k: Float)? {
            guard let cam = camera, let cc = cam.camera, let serve = rig.clips["serve"] else { return nil }
            let racketParts = rig.info.parts.filter { $0.kind == "rigid" && $0.part.hasPrefix("Racket_") }.compactMap { rp -> (track: Int, box: (SCNVector3, SCNVector3))? in
                guard let i = asset.partIndex(rp.part) else { return nil }
                return (rp.track, asset.geometry[i].boundingBox)
            }
            let head = rig.info.bones.firstIndex(of: "Head")
            var points: [SIMD3<Float>] = []
            for f in 0 ..< serve.frames {
                let pose = serve.pose(at: Double(f) * serve.length / Double(serve.frames - 1), loop: false)
                for r in racketParts {
                    let (lo, hi) = r.box
                    for x in [lo.x, hi.x] { for y in [lo.y, hi.y] { for z in [lo.z, hi.z] { let w = pose[r.track].matrix * SIMD4(Float(x), Float(y), Float(z), 1); points.append(root.simdConvertPosition(SIMD3(w.x, w.y, w.z), to: nil)) } } }
                }
                if let head { let h = pose[head].t; points.append(root.simdConvertPosition(h + SIMD3(0, 0.2, 0), to: nil)) }   // the head bone is at the jaw: 0.2 m clears the crown
            }
            let base = cam.simdPosition, view = simd_inverse(simd_float4x4(cam.simdOrientation))   // the camera keeps its orientation; only its position is scaled
            let tanHalf = Float(tan(cc.fieldOfView * .pi / 360))
            func ndcTop(_ k: Float) -> Float {
                points.map { q in let v = view * SIMD4(q - base * k, 1); return v.y / max(0.01, -v.z) / tanHalf }.max() ?? 0
            }
            guard ndcTop(1) > 0.93 else { return (base, 1) }
            var lowK: Float = 1, highK: Float = 2
            for _ in 0 ..< 24 { let mid = (lowK + highK) / 2; if ndcTop(mid) > 0.93 { lowK = mid } else { highK = mid } }
            return (base, highK)
        }
        private func stopLink() { displayLink?.invalidate(); displayLink = nil }
        /// Stop the display link but keep the skeleton and the motion: a film or a test then poses the hero at explicit times (`pose(at:)`).
        func pauseDisplayLink() { stopLink() }
        /// Pose the skeleton for `time` seconds into the motion (the display link calls this every frame; tests and the proof film call it with explicit times).
        func pose(at time: Double) {
            motionTime = time
            guard let motion, let rig else { return }
            rig.apply(motion, at: time)
            if let zoom = serveZoom { camera?.simdPosition = zoom.base * (1 + (zoom.k - 1) * motion.serveWeight(rig.data, at: time)) }
        }
        @MainActor private final class LinkTarget: NSObject {
            weak var owner: Coordinator?
            init(_ owner: Coordinator) { self.owner = owner }
            @objc func fire(_ link: CADisplayLink) { if let owner { owner.pose(at: link.targetTimestamp - owner.motionStart) } }
        }

        var clubTimer: Timer?
        private var clubKey: String?
        func configureClub(_ activity: ClubPreviewActivity, animate: Bool) {
            let key = "\(activity.rawValue)-\(animate)"
            guard key != clubKey else { return }
            clubTimer?.invalidate(); practiceTimer?.invalidate(); practiceTimer = nil
            stopMotion()
            character.removeAllActions(); character.eulerAngles = SCNVector3Zero
            scene.rootNode.childNode(withName: "clubProps", recursively: false)?.removeFromParentNode()
            scene.rootNode.childNode(withName: "idleBall", recursively: false)?.removeFromParentNode()
            guard let equipped = previous else { return }
            previous = nil; update(equipped); clubKey = key
            guard hero != nil else { return }
            racket?.isHidden = activity != .play
            let props = SCNNode(); props.name = "clubProps"; scene.rootNode.addChildNode(props)
            func box(_ size: SCNVector3, _ position: SCNVector3, _ color: UIColor, _ radius: CGFloat = 0.02) -> SCNNode {
                let n = SCNNode(geometry: SCNBox(width: CGFloat(size.x), height: CGFloat(size.y), length: CGFloat(size.z), chamferRadius: radius))
                n.position = position; n.geometry?.firstMaterial?.diffuse.contents = color
                n.geometry?.firstMaterial?.roughness.contents = 0.85; props.addChildNode(n); return n
            }
            let wood = UIColor(red: 0.57, green: 0.34, blue: 0.16, alpha: 1)
            let navy = UIColor(IslandUI.navy)
            if activity == .settings {
                // Open wooden deck chair, angled with the hero. Navy/cream canvas strips. The match set has no seated clip, so the hero stands
                // in front of the chair in the Ready pose.
                let chair = SCNNode(); props.addChildNode(chair)
                for x: Float in [-0.30, 0.30] {
                    for z: Float in [-0.22, 0.30] { _ = box(SCNVector3(0.045,0.48,0.045), SCNVector3(x,0.23,z),wood) }
                    _ = box(SCNVector3(0.055,0.055,0.65),SCNVector3(x,0.56,0.03),wood)
                }
                for i in 0..<9 {
                    let x = Float(i-4)*0.06
                    _ = box(SCNVector3(0.06,0.035,0.55),SCNVector3(x,0.36,0.02),i%2 == 0 ? navy : .white,0)
                    let back = box(SCNVector3(0.06,0.64,0.035),SCNVector3(x,0.68,-0.28),i%2 == 0 ? navy : .white,0)
                    back.eulerAngles.x = -0.20
                }
                props.eulerAngles.y = 0.35
                props.position = SCNVector3(-0.12, 0, -0.66)   // the chair stands behind the hero, who stands in front of it
            } else if activity == .store {
                _ = box(SCNVector3(0.40,0.6,0.3), SCNVector3(0.57,0.3,-0.13), wood)
                _ = box(SCNVector3(0.075,0.095,0.03),SCNVector3(0.57,0.44,0.035),UIColor(red:0.8,green:0.64,blue:0.3,alpha:1))
            }
            // The hero is the motion: the locker, settings and store tiles play the ReadyIdle loop in place; the play tile plays one Serve and goes back to Ready. Reduced motion keeps the still Ready stance.
            play(animate ? (activity == .play ? .serveOnce : .ready) : nil)
        }
        private var idleKey: String?
        let heroCameraDistance: Float
        init(cameraDistance: Float = 4.8) {
            heroCameraDistance = max(2.4, cameraDistance) * 1.05
            scene.rootNode.addChildNode(character)
            let camera = SCNNode(); camera.camera = SCNCamera(); camera.camera?.fieldOfView = 32; camera.camera?.zNear = 0.05   // close-ups sit ~1 m away
            camera.position = SCNVector3(0, 1.0, cameraDistance); camera.look(at: SCNVector3(0,0.92,0))
            scene.rootNode.addChildNode(camera)
            // Light recipe (ArtDir/hero/v5_proof/LIGHTING.md), same philosophy as the court: warm soft key at
            // 40 deg elevation from camera-left, cool fill at ~35% from camera-right, a rim from behind, low cool ambient.
            func light(_ type: SCNLight.LightType, _ intensity: CGFloat, _ color: UIColor, elevation: Float, azimuth: Float, shadow: Bool = false) {
                let node = SCNNode(), l = SCNLight(); l.type = type; l.intensity = intensity; l.color = color
                if shadow {
                    l.castsShadow = true; l.shadowRadius = 6; l.shadowSampleCount = 8; l.shadowMode = .deferred
                    l.shadowColor = UIColor(red: 0.12, green: 0.14, blue: 0.24, alpha: 0.35); l.orthographicScale = 2.2
                }
                node.light = l
                // direction the light comes FROM: azimuth 0 = camera (+z), positive = camera-left
                let e = elevation * .pi / 180, a = azimuth * .pi / 180
                node.position = SCNVector3(-sin(a) * cos(e) * 6, sin(e) * 6, cos(a) * cos(e) * 6)
                node.look(at: SCNVector3(0, 0.8, 0)); scene.rootNode.addChildNode(node)
            }
            light(.directional, 1050, UIColor(red: 1, green: 0.92, blue: 0.82, alpha: 1), elevation: 40, azimuth: 38, shadow: true)   // key
            light(.directional, 370, UIColor(red: 0.84, green: 0.9, blue: 1, alpha: 1), elevation: 18, azimuth: -55)                   // fill
            light(.directional, 520, UIColor(red: 1, green: 0.96, blue: 0.9, alpha: 1), elevation: 35, azimuth: 160)                   // rim
            let amb = SCNNode(); amb.light = SCNLight(); amb.light?.type = .ambient; amb.light?.intensity = 210
            amb.light?.color = UIColor(red: 0.86, green: 0.88, blue: 0.96, alpha: 1); scene.rootNode.addChildNode(amb)
            let floor=SCNNode(geometry:SCNCylinder(radius:0.48,height:0.025));floor.position.y = -0.025
            floor.geometry?.firstMaterial?.diffuse.contents=UIColor.clear
            floor.isHidden = true
            scene.rootNode.addChildNode(floor)
        }
        func update(_ p: Player) {
            guard p != previous else { return }; previous=p; idleKey = nil
            let playing = motion
            stopLink(); rig = nil   // the old hero's skeleton goes with its nodes
            character.childNodes.forEach { $0.removeFromParentNode() }
            character.scale = SCNVector3(1, 1, 1)
            // The locker mirror is the match hero itself (MatchHero.build): the male or female body the locker's sex pick selects, bald,
            // skin-tinted by the pick, holding the classic racket. What you see here is the mesh that walks onto the court.
            guard let hero = MatchHero.build(p), let asset = MatchHero.asset(female: p.standardFemale) else { return }
            headCentre = asset.manifest.headCentreY * asset.scale
            character.addChildNode(hero)
            if !framed { applyFraming() }   // re-tinting never moves the camera the player has turned
            racket?.isHidden = framing == .head
            if let playing { play(playing, keepClock: true) }   // a new look on the stage keeps the tile's motion (and its clock) going
        }
        /// Small original prop comedy: a ball bounces too high, or rolls past a golfer's feet.
        /// Reduced motion keeps the same sport-specific props in a still pose.
        func configureIdle(sport: Sport?, animate: Bool) {
            let key = "\(sport?.rawValue ?? "none")-\(animate)"
            guard key != idleKey else { return }; idleKey = key
            idleWanted = animate && sport == .tennis
            character.removeAllActions(); character.position = SCNVector3Zero; character.eulerAngles = SCNVector3Zero
            stopMotion()
            scene.rootNode.childNode(withName: "idleBall", recursively: false)?.removeFromParentNode()
            character.childNode(withName: "idleClub", recursively: false)?.removeFromParentNode()
            character.childNode(withName: MatchHero.racketName, recursively: true)?.isHidden = sport == .golf || framing == .head   // close-ups: the Ready racket crosses the chin
            guard let sport else { return }
            let ball = SCNNode(geometry: SCNSphere(radius: sport == .golf ? 0.032 : 0.065))
            ball.name = "idleBall"
            ball.geometry?.firstMaterial?.diffuse.contents = sport == .golf ? UIColor.white : UIColor(red: 0.8, green: 1, blue: 0.2, alpha: 1)
            ball.position = SCNVector3(0.45, sport == .golf ? 0.04 : 0.65, 0.25)
            scene.rootNode.addChildNode(ball)
            if sport == .golf {
                let club = SCNNode(); club.name = "idleClub"
                let shaft = SCNNode(geometry: SCNCylinder(radius: 0.012, height: 0.65))
                shaft.geometry?.firstMaterial?.diffuse.contents = UIColor.lightGray
                let head = SCNNode(geometry: SCNBox(width: 0.13, height: 0.045, length: 0.06, chamferRadius: 0.01))
                head.position = SCNVector3(-0.045, -0.33, 0)
                head.geometry?.firstMaterial?.diffuse.contents = UIColor.darkGray
                club.addChildNode(shaft); club.addChildNode(head)
                club.position = SCNVector3(previous?.handedness == .left ? -0.35 : 0.35, 0.38, 0.14)
                character.addChildNode(club)
            }
            guard animate else { return }
            if sport == .golf {
                ball.runAction(.repeatForever(.sequence([
                    .move(to: SCNVector3(-0.4, 0.04, 0.25), duration: 1.1), .wait(duration: 0.4),
                    .move(to: SCNVector3(0.45, 0.04, 0.25), duration: 1.1), .wait(duration: 0.4)
                ])))
                character.runAction(.repeatForever(.sequence([
                    .rotateTo(x: 0, y: -0.16, z: 0.04, duration: 0.7), .wait(duration: 0.8),
                    .rotateTo(x: 0, y: 0.16, z: -0.04, duration: 0.7), .wait(duration: 0.8)
                ])))
            } else {
                let bounce = SCNAction.move(to: SCNVector3(0.45, 1.55, 0.25), duration: 0.65)
                bounce.timingMode = .easeOut
                let drop = SCNAction.move(to: SCNVector3(0.45, 0.10, 0.25), duration: 0.65)
                drop.timingMode = .easeIn
                ball.runAction(.repeatForever(.sequence([bounce, drop, .wait(duration: 0.2)])))   // the loose ball is a prop; the hero's motion is the ReadyIdle clip, not a twist of the whole character
                play(.ready)
            }
        }
    }
}

/// Menu catalogue only: the names and counts the locker lists for haircut and headwear (HeroKit.cs keeps the same tables on the Unity side).
/// The match heroes are bald and wear no kit (HERO_MAINSTAY), so nothing in this enum is drawn; the hero on the locker stage is `MatchHero`.
@MainActor enum HeroV4 {
    static let headwear = ["None", "Visor", "Cap", "Sweatband"]
    static let haircuts = ["Swept", "Ponytail", "Bob", "Long", "Curly", "Bald", "Buzz", "Waves"]
    /// Haircuts the menus offer. Bald / Buzz / Waves stay out until the head rebuild lands.
    nonisolated static var offered: Int { HeadRebuild.shipped ? 8 : 5 }
    nonisolated enum HeadRebuild { static let shipped = false }
}

/// The two match heroes as the game plays them (HERO_MAINSTAY + DRESS_MATCH_HEROES): the male and female bodies from work/match-anim-set, bald, with the painted face,
/// the classic racket in the right hand and the White tennis kit (polo, shorts / skort, socks, shoes: parts `Kit_Top`, `Kit_Bottom`, `Kit_Sock_L/R`, `Kit_Shoe_L/R`), exported by Unity
/// (MatchHeroLockerExport) from Resources/Tennis/Customization/PlayerMale|PlayerFemale:
///   CharacterAssets/MatchHero_<Sex>.json         parts, submeshes (each with the LOOK numbers of Unity's runtime material), bounds, the rig description
///   CharacterAssets/MatchHero_<Sex>.lzfse        the Ready stance (frame 0 of <Sex>_ReadyIdle), positions + normals + indices; the kit's UVs; the body's bind-pose positions
///   CharacterAssets/MatchHero_<Sex>_Swing_NN     the match Forehand clip sampled at manifest.swingTimes (morph targets for the loading screen's practice swing)
///   CharacterAssets/MatchHero_<Sex>_Rig.lzfse    LOCKER_MIRROR: bind-pose meshes + bone weights + the clips <Sex>_ReadyIdle and <Sex>_Serve as bone tracks (the menu tiles play these)
///   CharacterAssets/MatchHero_<map>.png          the cloth weave, the kit's seam maps and the soft skin normal Unity's materials sample
/// The data types, loaders, surfaces (the cloth / skin look) and the skeleton player are in MatchHeroData / MatchHeroSurfaces / MatchHeroMotion.swift.
/// The locker's skin pick tints the body, its racket colour tints the frame, and its shirt / shorts / shoes ("accent") picks recolour the kit by role with the SAME maths as Unity's
/// MatchHeroLook.SetKit (`kitTint`, `kitDerive`); hair and headwear are not part of these bodies.
@MainActor enum MatchHero {
    static let rootName = MatchHeroData.rootName, racketName = MatchHeroData.racketName
    /// Height the hero stands on the locker stage: the frame the old chibi filled, so every locker camera keeps its framing.
    static let stageHeight = MatchHeroData.stageHeight

    // The export's data types, loaders and the skeleton player live in MatchHeroData / MatchHeroSurfaces / MatchHeroMotion (no UIKit, no Player: the same files build the hero in the render harness).
    typealias Look = MatchHeroData.Look
    typealias Sub = MatchHeroData.Sub
    typealias Part = MatchHeroData.Part
    typealias Mat = MatchHeroData.Mat
    typealias Manifest = MatchHeroData.Manifest
    typealias Asset = MatchHeroData.Asset

    static func previewData(_ name: String) -> Data? { MatchHeroData.data(name) }
    static func asset(female: Bool) -> Asset? { MatchHeroData.asset(female: female) }

    // MARK: the worn kit (DRESS_MATCH_HEROES) - the same maths as Unity's MatchHeroLook.SetKit
    /// sRGB albedo of the authored White kit, and the floor of a tint (the authored Black kit's albedo, so a black pick still shows folds and seams).
    static let kitWhite: Float = 0.9300, kitBlackR: Float = 0.0350, kitBlackB: Float = 0.0401
    /// A picked colour on the near-white kit material: multiply (a white pick is the authored white), floored at the authored black.
    static func kitTint(_ pick: SIMD3<Float>) -> SIMD3<Float> {
        SIMD3(max(kitWhite * pick.x, kitBlackR), max(kitWhite * pick.y, kitBlackR), max(kitWhite * pick.z, kitBlackB))
    }
    /// The trim / band colour that goes with a shirt / shorts pick: darkened 35 % when the pick is light (luminance >= .5), lightened 35 % toward white when it is dark.
    static func kitDerive(_ pick: SIMD3<Float>) -> SIMD3<Float> {
        let l = 0.2126 * pick.x + 0.7152 * pick.y + 0.0722 * pick.z
        return l >= 0.5 ? pick * 0.65 : pick + (SIMD3<Float>(repeating: 1) - pick) * 0.35
    }
    /// The colour of one kit role for this player, or nil for the authored colour (no pick = "kit colour"):
    /// shirt -> Kit_Shirt + Kit_ShirtTrim, shorts -> Kit_Shorts + Kit_ShortsBand, accent (shoes) -> Kit_Shoe; Kit_Sole and Kit_Sock are never touched.
    static func kitColour(role: String, player p: Player) -> SIMD3<Float>? {
        func pick(_ slot: String) -> SIMD3<Float>? { p.outfitHex(slot).map(rgb) }
        switch role {
        case "Kit_Shirt": return pick("shirt").map(kitTint)
        case "Kit_ShirtTrim": return pick("shirt").map { kitTint(kitDerive($0)) }
        case "Kit_Shorts": return pick("shorts").map(kitTint)
        case "Kit_ShortsBand": return pick("shorts").map { kitTint(kitDerive($0)) }
        case "Kit_Shoe": return pick("accent").map(kitTint)
        default: return nil
        }
    }

    static func rgb(_ hex: String) -> SIMD3<Float> {
        let n = UInt32(hex, radix: 16) ?? 0xFFFFFF
        return SIMD3(Float((n >> 16) & 255) / 255, Float((n >> 8) & 255) / 255, Float(n & 255) / 255)
    }

    /// The hero node: Body, Face, the six Kit_* parts and a "racket" group (frame, strings, grip), turned to the camera a touch 3/4, mirrored for a left-hander. Surfaces follow Unity's runtime
    /// materials (MatchHeroSurfaces): the skin tone, the racket colour and the kit's role tints are the locker's picks, everything else is the authored look.
    static func build(_ p: Player) -> SCNNode? {
        guard let asset = asset(female: p.standardFemale) else { return nil }
        let picks = MatchHeroData.Picks(colour: { name in
            switch name {
            case "Skin": return rgb(p.skinHex)
            case "White_Frame": return p.outfitHex("racket").map(rgb)
            case let role where role.hasPrefix("Kit_"): return kitColour(role: role, player: p)
            default: return nil
            }
        }, leftHanded: p.handedness == .left)
        return MatchHeroData.buildHero(asset, picks: picks)
    }

    /// Vertex targets sampled from the unchanged match Forehand (the clip the court plays), for the locker's practice swing.
    static func preparePractice(_ root: SCNNode) {
        guard let female = root.value(forKey: "heroFemale") as? Bool, let asset = asset(female: female), let targets = asset.swingTargets() else { return }
        root.enumerateChildNodes { node, _ in
            guard node.morpher == nil, let i = node.value(forKey: "menuPartIndex") as? Int, targets.indices.contains(i), !targets[i].isEmpty else { return }
            let morph = SCNMorpher(); morph.targets = targets[i]; morph.calculationMode = .normalized; morph.unifiesNormals = false; node.morpher = morph
        }
    }

    /// progress 0...1 over one practice swing: ease out of the Ready stance, play the Forehand clip, ease back.
    static func practiceFrame(_ root: SCNNode, progress: Double) {
        guard let female = root.value(forKey: "heroFemale") as? Bool, let asset = asset(female: female) else { return }
        let times = asset.manifest.swingTimes.map(Double.init)
        let active = min(1, progress / 0.1) * min(1, (1 - progress) / 0.15)
        let clipTime = min(1, max(0, (progress - 0.1) / 0.75)) * Double(asset.manifest.swingLength)
        let hi = min(times.count - 1, times.firstIndex(where: { $0 >= clipTime }) ?? times.count - 1), lo = max(0, hi - 1)
        let span = times[hi] - times[lo], mix = span > 0 ? min(1, max(0, (clipTime - times[lo]) / span)) : 1
        SCNTransaction.begin(); SCNTransaction.disableActions = true
        root.enumerateChildNodes { node, _ in
            guard let morph = node.morpher else { return }
            for i in 0 ..< morph.targets.count {
                morph.setWeight(CGFloat((i == lo ? (1 - mix) * active : 0) + (i == hi ? (lo == hi ? 1 : mix) * active : 0)), forTargetAt: i)
            }
        }
        SCNTransaction.commit()
    }
}
