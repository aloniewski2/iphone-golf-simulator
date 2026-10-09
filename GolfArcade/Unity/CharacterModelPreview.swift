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

/// A projected contact cue in the same camera as the feet, used by every native hero stage.
@MainActor enum HeroStageGround {
    private static let texture: UIImage = {
        let format = UIGraphicsImageRendererFormat(); format.scale = 1
        return UIGraphicsImageRenderer(size: CGSize(width: 128, height: 128), format: format).image { context in
            let colors = [UIColor(red: 0.035, green: 0.075, blue: 0.10, alpha: 0.28).cgColor,
                          UIColor(red: 0.035, green: 0.075, blue: 0.10, alpha: 0).cgColor] as CFArray
            if let gradient = CGGradient(colorsSpace: CGColorSpaceCreateDeviceRGB(), colors: colors, locations: [0, 1]) {
                context.cgContext.drawRadialGradient(gradient, startCenter: CGPoint(x: 64, y: 64), startRadius: 0,
                    endCenter: CGPoint(x: 64, y: 64), endRadius: 64, options: [])
            }
        }
    }()
    static func node(name: String = "hero-floor") -> SCNNode {
        let geometry = SCNPlane(width: 1.05, height: 0.16)
        let material = SCNMaterial(); material.diffuse.contents = texture
        material.lightingModel = .constant; material.isDoubleSided = true; material.writesToDepthBuffer = false
        geometry.materials = [material]
        let node = SCNNode(geometry: geometry); node.name = name
        // Menu cameras look almost horizontally; a world-horizontal plane becomes
        // edge-on. This shallow camera-facing ellipse follows the same foot origin.
        node.position = SCNVector3(0, 0.01, -0.08)
        let facing=SCNBillboardConstraint();facing.freeAxes = .Y;node.constraints=[facing]
        node.castsShadow = false
        return node
    }
    static func terrace() -> SCNNode {
        let root = SCNNode(); root.name = "lobby-terrace"
        func box(_ name: String, _ size: SCNVector3, _ position: SCNVector3, _ colour: UIColor, _ radius: CGFloat = 0.025) {
            let g = SCNBox(width:CGFloat(size.x),height:CGFloat(size.y),length:CGFloat(size.z),chamferRadius:radius)
            let m = SCNMaterial(); m.lightingModel = .physicallyBased; m.diffuse.contents = colour
            m.roughness.contents = 0.74; g.materials = [m]
            let n = SCNNode(geometry:g); n.name = name; n.position = position; root.addChildNode(n)
        }
        box("sandstone terrace",SCNVector3(10,0.12,6),SCNVector3(0,-0.125,-1.25),UIColor(Club.cream),0.06)
        // A continuous deck gives the four podiums one real common floor.
        for i in 0..<21 {
            box("teak deck plank",SCNVector3(0.47,0.025,4.7),SCNVector3(Float(i-10)*0.476,-0.053,-1),
                UIColor(red:0.60+CGFloat(i%3)*0.025,green:0.39+CGFloat(i%3)*0.02,blue:0.23+CGFloat(i%3)*0.015,alpha:1),0.006)
        }
        box("terrace low wall",SCNVector3(10,0.5,0.16),SCNVector3(0,0.14,-3.8),UIColor(Club.cream),0.035)
        box("wall coping",SCNVector3(10.05,0.075,0.21),SCNVector3(0,0.415,-3.8),UIColor.white,0.018)
        for x:Float in [-4.4,4.4] {
            box("navy planter",SCNVector3(0.58,0.55,0.58),SCNVector3(x,0.205,-2.9),UIColor(Club.ink),0.06)
            for i in 0..<7 {
                let t = Float(i) * .pi * 2 / 7
                let leaf = SCNNode(geometry:SCNCapsule(capRadius:0.075,height:0.62))
                leaf.geometry?.firstMaterial?.diffuse.contents = UIColor(red:0.12,green:0.34+CGFloat(i%3)*0.06,blue:0.22,alpha:1)
                leaf.geometry?.firstMaterial?.roughness.contents = 0.86
                leaf.position = SCNVector3(x+sin(t)*0.13,0.72,-2.9+cos(t)*0.13)
                leaf.eulerAngles = SCNVector3(cos(t)*0.7,0,sin(t)*0.7); root.addChildNode(leaf)
            }
        }
        return root
    }
    static func plinth() -> SCNNode {
        let root=SCNNode();root.name="lobby-floor"
        func disc(radius:CGFloat,height:CGFloat,y:Float,color:UIColor) {
            let shape=SCNCylinder(radius:radius,height:height);shape.radialSegmentCount=48
            let finish=SCNMaterial();finish.lightingModel = .physicallyBased;finish.diffuse.contents=color;finish.roughness.contents=0.7
            shape.materials=[finish];let node=SCNNode(geometry:shape);node.position.y=y;root.addChildNode(node)
        }
        disc(radius:0.53,height:0.028,y:-0.043,color:UIColor(Club.ink))
        disc(radius:0.53,height:0.036,y:-0.013,color:UIColor(Club.cream))
        let contact=node(name:"foot-contact");contact.position.y=0.013;root.addChildNode(contact)
        return root
    }
}

struct CharacterModelPreview: UIViewRepresentable {
    let player: Player
    var cameraDistance: Float = 4.8
    var framing: PreviewFraming = .body
    var outfitSport: Sport = .tennis
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
        context.coordinator.update(player, sport: outfitSport)
        context.coordinator.setFraming(framing, view: view, animated: false)
        updateIdle(view, context: context)
        return view
    }
    func updateUIView(_ view: SCNView, context: Context) {
        context.coordinator.update(player, sport: outfitSport)
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
            racket?.isHidden = framing == .head || outfitSport == .golf
            hero?.setValue(framing == .head, forKey: "heroEquipmentHidden")
            MatchHero.golfEquipment(in: hero)?.isHidden = framing == .head
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
                // Frame the equipped sport's tool, including the full club shaft and head.
                let equipment = outfitSport == .golf ? MatchHero.golfEquipment(in: hero) : racket
                if let r = equipment {
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
        private var outfitSport: Sport = .tennis
        private var lastPractice = 0
        var practiceTimer: Timer?
        /// The tennis idle plays ReadyIdle between practice swings (the practice swing itself is still the Forehand morph on the static Ready stance).
        private var idleWanted = false
        private var settleTask: Task<Void, Never>?
        func practice(_ sequence: Int) {
            guard sequence != lastPractice else { return }; lastPractice = sequence
            guard hero != nil else { return }
            practiceTimer?.invalidate(); practiceTimer = nil; settleTask?.cancel()
            if outfitSport == .golf {
                play(MenuMotion(kind: .clipOnce("golfIron"), lead: 0.18, fadeIn: 0.24, fadeOut: 0.35))
                return
            }
            guard outfitSport == .tennis else { return }
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
            guard outfitSport == .tennis || outfitSport == .golf else { motion = nil; return }
            motion = m
            guard let m, let root = hero, let asset = MatchHero.asset(female: root.value(forKey: "heroFemale") as? Bool ?? false, golf: outfitSport == .golf), let r = HeroRig(root: root, asset: asset) else { return }
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
            previous = nil; update(equipped, sport: outfitSport); clubKey = key
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
            let floor=HeroStageGround.node()
            floor.isHidden = true
            scene.rootNode.addChildNode(floor)
        }
        func update(_ p: Player, sport: Sport = .tennis) {
            guard p != previous || sport != outfitSport else { return }; previous=p; outfitSport=sport; idleKey = nil
            let playing = sport == .tennis ? motion : nil
            stopLink(); rig = nil   // the old hero's skeleton goes with its nodes
            character.childNodes.forEach { $0.removeFromParentNode() }
            character.scale = SCNVector3(1, 1, 1)
            // The locker mirror is the match hero itself (MatchHero.build): the male or female body the locker's sex pick selects, bald,
            // skin-tinted by the pick, holding the classic racket. What you see here is the mesh that walks onto the court.
            guard let hero = MatchHero.build(p, sport: sport), let asset = MatchHero.asset(female: p.standardFemale, golf: sport == .golf) else { return }
            headCentre = asset.manifest.headCentreY * asset.scale
            character.addChildNode(hero)
            scene.rootNode.childNode(withName: "hero-floor", recursively: false)?.isHidden = false
            if !framed { applyFraming() }   // re-tinting never moves the camera the player has turned
            racket?.isHidden = framing == .head || sport == .golf
            if let playing { play(playing, keepClock: true) }   // a new look on the stage keeps the tile's motion (and its clock) going
        }
        /// Small original prop comedy: a ball bounces too high, or rolls past a golfer's feet.
        /// Reduced motion keeps the same sport-specific props in a still pose.
        func configureIdle(sport: Sport?, animate: Bool) {
            let key = "\(sport?.rawValue ?? "none")-\(animate)"
            guard key != idleKey else { return }; idleKey = key
            idleWanted = animate && (sport == .tennis || sport == .golf)
            character.removeAllActions(); character.position = SCNVector3Zero; character.eulerAngles = SCNVector3Zero
            stopMotion()
            scene.rootNode.childNode(withName: "idleBall", recursively: false)?.removeFromParentNode()
            character.childNode(withName: "idleClub", recursively: false)?.removeFromParentNode()
            character.childNode(withName: MatchHero.racketName, recursively: true)?.isHidden = (sport == .golf || outfitSport == .golf) || framing == .head   // close-ups: the Ready racket crosses the chin
            guard let sport else { return }
            let ball = SCNNode(geometry: SCNSphere(radius: sport == .golf ? 0.032 : 0.065))
            ball.name = "idleBall"
            ball.geometry?.firstMaterial?.diffuse.contents = sport == .golf ? UIColor.white : UIColor(red: 0.8, green: 1, blue: 0.2, alpha: 1)
            ball.position = SCNVector3(0.45, sport == .golf ? 0.04 : 0.65, 0.25)
            scene.rootNode.addChildNode(ball)
            if sport == .golf {
                if MatchHero.golfEquipment(in: hero) == nil,
                   let equipped = previous, let root = hero {
                    root.addChildNode(MatchHero.golfClub(player: equipped))
                }
            }
            guard animate else { return }
            if sport == .golf {
                ball.runAction(.repeatForever(.sequence([
                    .move(to: SCNVector3(-0.4, 0.04, 0.25), duration: 1.1), .wait(duration: 0.4),
                    .move(to: SCNVector3(0.45, 0.04, 0.25), duration: 1.1), .wait(duration: 0.4)
                ])))
                play(.ready)
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
    static func asset(female: Bool, golf: Bool = false, distance: Bool = false) -> Asset? { MatchHeroData.asset(female: female, golf: golf, distance: distance) }

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
        case "Kit_Shirt", "Kit_GolfGlove", "Kit_GolfHead": return pick("shirt").map(kitTint)
        case "Kit_ShirtTrim": return pick("shirt").map { kitTint(kitDerive($0)) }
        case "Kit_Shorts": return pick("shorts").map(kitTint)
        case "Kit_ShortsBand", "Kit_GolfHardware": return pick("shorts").map { kitTint(kitDerive($0)) }
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
    static func build(_ p: Player, sport: Sport = .tennis, distance: Bool = false) -> SCNNode? {
        guard let asset = asset(female: p.standardFemale, golf: sport == .golf, distance: distance) else { return nil }
        let picks = MatchHeroData.Picks(colour: { name in
            switch name {
            case "Skin": return rgb(p.skinHex)
            case "White_Frame": return p.outfitHex("racket").map(rgb)
            case let role where role.hasPrefix("Kit_"): return kitColour(role: role, player: p)
            default: return nil
            }
        }, leftHanded: p.handedness == .left)
        let root = MatchHeroData.buildHero(asset, picks: picks)
        root.setValue(sport == .golf, forKey: "heroGolf")
        if sport == .golf, golfEquipment(in: root) == nil { root.addChildNode(golfClub(player: p)) }
        return root
    }

    static func golfEquipment(in root: SCNNode?) -> SCNNode? {
        root?.childNodes.first(where: { $0.name?.hasPrefix("Club_") == true && !$0.isHidden })
            ?? root?.childNode(withName: "Club_Iron", recursively: false)
            ?? root?.childNode(withName: "golfClub", recursively: false)
    }

    static func golfClub(player: Player) -> SCNNode {
        let club = SCNNode(); club.name = "golfClub"
        var hand = SIMD3<Float>(0.25, 0.95, -0.25)
        if let tennis = asset(female: player.standardFemale),
           let i = tennis.manifest.parts.firstIndex(where: { $0.name == "Racket_Grip" }) {
            let node = SCNNode(geometry: tennis.geometry[i]), box = node.boundingBox
            hand = SIMD3((box.min.x + box.max.x) / 2, (box.min.y + box.max.y) / 2, (box.min.z + box.max.z) / 2)
        }
        // The authored grip origin remains the hand pivot; only the menu display pose
        // rotates it. All shaft/head geometry and finishes come from the game asset.
        let direction = simd_normalize(SIMD3<Float>(0.40, -0.91, -0.08))
        if let mesh = NativeGolfEquipment.make("Iron", tint: player.outfitHex("racket").map(rgb)) {
            mesh.simdPosition = hand
            mesh.simdOrientation = simd_quatf(from: SIMD3<Float>(0, -1, 0), to: direction)
            club.addChildNode(mesh)
        }
        return club
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

/// One SceneKit surface for the whole party. Each hero keeps its own rig, look and animation clock.
struct LobbyHeroStage: UIViewRepresentable {
    let participants: [MultiplayerParticipant]
    let sport: MultiplayerSport
    let localID: String
    var editingPlayer: Player?
    var emotes: [String: MultiplayerEmote] = [:]
    var networkTime: Double
    var winnerID: String?
    var introduce = true
    var animate = true
    func makeCoordinator() -> Coordinator { Coordinator() }
    func makeUIView(context: Context) -> SCNView {
        let view = SCNView(); view.backgroundColor = .clear
        view.delegate = context.coordinator
        view.scene = context.coordinator.recipe.scene; view.pointOfView = context.coordinator.recipe.camera
        view.autoenablesDefaultLighting = false; view.allowsCameraControl = false
        view.antialiasingMode = .multisampling4X; view.preferredFramesPerSecond = 60; view.isPlaying = animate
        view.accessibilityIdentifier = "online-hero-stage"
        context.coordinator.view = view; context.coordinator.update(self); context.coordinator.start()
        return view
    }
    func updateUIView(_ view: SCNView, context: Context) {
        view.isPlaying = animate; context.coordinator.update(self)
        view.accessibilityLabel = participants.map { "\($0.name), \($0.female ? "female" : "male"), \($0.left ? "left handed" : "right handed")" }.joined(separator: "; ")
    }
    static func dismantleUIView(_ view: SCNView, coordinator: Coordinator) { coordinator.stop(); view.isPlaying = false; view.scene = nil }

    @MainActor final class Coordinator: NSObject, SCNSceneRendererDelegate {
        let recipe = CharacterModelPreview.Coordinator(cameraDistance: 5)
        weak var view: SCNView?
        struct Hero { var container: SCNNode; var player: Player; var motion: LobbyHeroMotion; var eventID: String? }
        private(set) var heroes: [String: Hero] = [:]
        private var link: CADisplayLink?
        private var offset = 0.0
        private var ordered: [MultiplayerParticipant] = []
        private var sport: MultiplayerSport = .tennis
        private var winner: String?
        private var animate = true
        private(set) var frames = 0
        private(set) var measuredSeconds = 0.0
        private var previousFrame = 0.0
        private var framedSize = CGSize.zero
        override init() {
            super.init()
            recipe.scene.rootNode.childNode(withName: "hero-floor", recursively: false)?.removeFromParentNode()
            recipe.scene.rootNode.addChildNode(HeroStageGround.terrace())
        }
        func resetMeasurement() { frames = 0; measuredSeconds = 0; previousFrame = 0 }
        nonisolated func renderer(_ renderer: any SCNSceneRenderer, didRenderScene scene: SCNScene, atTime time: TimeInterval) {
            Task { @MainActor [weak self] in self?.recordFrame(time) }
        }
        private func recordFrame(_ time: Double) {
            if previousFrame > 0 { measuredSeconds += time - previousFrame; frames += 1 }; previousFrame = time
        }
        var measuredFPS: Double { measuredSeconds > 0 ? Double(frames) / measuredSeconds : 0 }
        func update(_ state: LobbyHeroStage) {
            let sportChanged = sport != state.sport
            offset = state.networkTime - CACurrentMediaTime(); animate = state.animate; sport = state.sport
            let now = state.networkTime, incoming = Set(state.participants.map(\.id))
            for id in Array(heroes.keys) where !incoming.contains(id) {
                if let hero = heroes.removeValue(forKey: id) { hero.container.runAction(.sequence([.fadeOut(duration: 0.18),.removeFromParentNode()])) }
            }
            ordered = state.participants
            for (i,p) in ordered.enumerated() {
                let player = p.id == state.localID ? state.editingPlayer ?? p.lobbyPlayer : p.lobbyPlayer
                let old = heroes[p.id]
                if old?.player != player || sportChanged {
                    let heroSport: Sport = sport == .golf ? .golf : .tennis
                    guard let root = MatchHero.build(player, sport: heroSport, distance: true), let asset = MatchHero.asset(female:player.standardFemale, golf:heroSport == .golf, distance:true), let rig = HeroRig(root:root,asset:asset) else { continue }
                    let holder = old?.container ?? SCNNode()
                    holder.childNodes.filter { $0.name != "lobby-floor" }.forEach { $0.removeFromParentNode() }; holder.addChildNode(root)
                    let motion = LobbyHeroMotion(rig:rig,idleOffset:Double(i) * 0.73)
                    if let previous = old?.motion, let clip = previous.clip {
                        motion.play(clip,at:previous.startedAt,now:now)
                        if let queued = previous.queued { motion.play(queued,at:now,now:now) }
                    }
                    heroes[p.id] = Hero(container:holder,player:player,motion:motion,eventID:old?.eventID)
                    #if DEBUG
                    OnlineLobbyProofDriver.event("hero-look","\(p.id) \(player.outfitHex("shirt") ?? "kit")")
                    #endif
                    if old == nil {
                        let shadow = HeroStageGround.plinth()
                        holder.position = SCNVector3((Float(i)-Float(ordered.count-1)/2)*1.22,0,sport == .tennis && p.seat < 0 ? -0.65 : 0)
                        holder.addChildNode(shadow)
                        recipe.scene.rootNode.addChildNode(holder)
                        holder.opacity = state.animate ? 0 : 1; holder.scale = SCNVector3(0.92,0.92,0.92)
                        // Give existing heroes time to make room before the newcomer appears.
                        let joinDelay = state.animate ? 0.22 : 0
                        holder.runAction(.sequence([.wait(duration:joinDelay),.group([.fadeIn(duration:0.2),.scale(to:1,duration:0.22)])]))
                        if state.animate && state.introduce { motion.play("wave",at:now+joinDelay,now:now) }
                    }
                }
                if let event = state.emotes[p.id], heroes[p.id]?.eventID != event.id {
                    heroes[p.id]?.eventID = event.id; heroes[p.id]?.motion.play(event.emoteID,at:event.startedAt,now:now)
                    #if DEBUG
                    OnlineLobbyProofDriver.event("hero-emote","\(p.id) \(event.emoteID) sent=\(event.startedAt) queued=\(heroes[p.id]?.motion.queued ?? "none")")
                    #endif
                }
            }
            if state.winnerID != winner { winner = state.winnerID; if let winner, state.animate { heroes[winner]?.motion.play("matchWin",at:now,now:now) } }
            reframe(); tickPose(at:now)
        }
        private func reframe() {
            let count = max(1,ordered.count), spacing: Float = 1.22
            SCNTransaction.begin(); SCNTransaction.animationDuration = animate ? 0.22 : 0
            for (i,p) in ordered.enumerated() {
                let x = (Float(i) - Float(count-1)/2) * spacing
                heroes[p.id]?.container.position = SCNVector3(x,0,sport == .tennis && p.seat < 0 ? -0.65 : 0)
            }
            SCNTransaction.commit()
            // Panel height changes resize the view immediately; matching its camera immediately keeps every hero in frame.
            SCNTransaction.begin(); SCNTransaction.disableActions = true
            if let camera = recipe.camera {
                let aspect = Float(max(1,view?.bounds.width ?? 402) / max(1,view?.bounds.height ?? 300))
                let width = Float(count - 1) * spacing + 1.3
                let distance = max(3.3,width / (2 * tan(Float.pi * 32 / 360) * aspect))
                camera.position = SCNVector3(0,1.65,distance); camera.look(at:SCNVector3(0,0.77,0))
            }
            SCNTransaction.commit()
        }
        func start() {
            guard link == nil else { return }
            let l = CADisplayLink(target:self,selector:#selector(frame(_:))); l.preferredFrameRateRange = CAFrameRateRange(minimum:60,maximum:60,preferred:60); l.add(to:.main,forMode:.common); link = l
        }
        @objc private func frame(_ link: CADisplayLink) {
            if let size = view?.bounds.size, size != framedSize { framedSize = size; reframe() }
            tickPose(at:link.timestamp + offset)
        }
        func tickPose(at time: Double) { for h in heroes.values { h.motion.pose(at:animate ? time : 0) } }
        func stop() { link?.invalidate(); link = nil; heroes.removeAll() }
    }
}

@MainActor enum LobbyEmoteThumbs {
    private static var cache: [String:UIImage] = [:]
    static func image(_ id: String, player: Player) -> UIImage? {
        let key = "\(id)|\(player.standardFemale)|\(player.handedness)|\(player.skinHex)|\(Player.outfitSlots.map { player.outfitHex($0) ?? "kit" }.joined(separator:"|"))"
        if let image = cache[key] { return image }
        let c = CharacterModelPreview.Coordinator(cameraDistance:4.6); c.update(player)
        guard let root = c.hero, let asset = MatchHero.asset(female:player.standardFemale), let rig = HeroRig(root:root,asset:asset), let clip = rig.data.clips[id] else { return nil }
        rig.attach(); rig.apply(clip.pose(at:clip.length * 0.35,loop:false))
        rig.data.applyMorphWeights(clip.morphWeights(at:clip.length * 0.35,loop:false),to:root)
        c.camera?.position = SCNVector3(0,0.9,4.6); c.camera?.look(at:SCNVector3(0,0.8,0))
        let renderer = SCNRenderer(device:nil,options:nil); renderer.scene = c.scene; renderer.pointOfView = c.camera
        let image = renderer.snapshot(atTime:0,with:CGSize(width:224,height:196),antialiasingMode:.multisampling4X)
        if cache.count > 24 { cache.removeAll() }; cache[key] = image; return image
    }
}
