import SwiftUI
import SceneKit

/// Catalog shared with TennisCustomization. The preview uses the exported game meshes.
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

@MainActor struct CharacterMeshData: Decodable {
    let positions: [Float], normals: [Float], uv: [Float], triangles: [Int32]
    let texture: String, mask: String, reference: [Float]
    let positionsSlim: [Float]?, positionsBroad: [Float]?
    static var cache: [String: CharacterMeshData] = [:]
    static func load(_ name: String) -> CharacterMeshData? {
        if let cached = cache[name] { return cached }
        guard let url = Bundle.main.url(forResource: name, withExtension: "json"),
              let data = try? Data(contentsOf: url), let mesh = try? JSONDecoder().decode(Self.self, from: data) else { return nil }
        cache[name] = mesh; return mesh
    }
    func geometry(player: Player? = nil, shapeBody: Bool = false) -> SCNGeometry {
        var points = positions
        if shapeBody, let player {
            let target = player.bodySize < 0.5 ? positionsSlim : positionsBroad
            if let target, target.count == points.count {
                let weight = Float(abs(player.bodySize - 0.5) * 2)
                for i in points.indices { points[i] += (target[i] - points[i]) * weight }
            }
            for i in stride(from: 0, to: points.count, by: 3) {
                let y = points[i+1]
                if y > 1.22 {
                    let face = CharacterOptions.faceScale(player.faceShape)
                    points[i] *= face.x; points[i+1] = 1.22 + (y-1.22)*face.y; points[i+2] *= face.z
                }
            }
        }
        func source(_ values: [Float], _ semantic: SCNGeometrySource.Semantic, _ size: Int) -> SCNGeometrySource {
            let bytes = values.withUnsafeBytes { Data($0) }
            return SCNGeometrySource(data: bytes, semantic: semantic, vectorCount: values.count / size,
                                     usesFloatComponents: true, componentsPerVector: size, bytesPerComponent: 4,
                                     dataOffset: 0, dataStride: size * 4)
        }
        let indices = triangles.withUnsafeBytes { Data($0) }
        return SCNGeometry(sources: [source(points, .vertex, 3), source(normals, .normal, 3), source(uv.enumerated().map { $0.offset % 2 == 1 ? 1 - $0.element : $0.element }, .texcoord, 2)],
                           elements: [SCNGeometryElement(data: indices, primitiveType: .triangles, primitiveCount: triangles.count / 3, bytesPerIndex: 4)])
    }
}

/// Locker framing: the whole player, or a close-up of the head (hair / headwear tabs), like a cosmetics shop.
enum PreviewFraming: Equatable { case body, head }

struct CharacterModelPreview: UIViewRepresentable {
    let player: Player
    var cameraDistance: Float = 4.8
    var framing: PreviewFraming = .body
    var idleSport: Sport? = nil
    var practiceSequence: Int? = nil
    @Environment(\.accessibilityReduceMotion) private var reduceMotion
    func makeCoordinator() -> Coordinator { Coordinator(cameraDistance: cameraDistance) }
    func makeUIView(context: Context) -> SCNView {
        let view = SCNView()
        view.backgroundColor = .clear
        view.scene = context.coordinator.scene
        view.allowsCameraControl = idleSport == nil
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
        let animate = idleSport != nil && !reduceMotion && !SportsSession.shared.reduceMotion
        context.coordinator.configureIdle(sport: idleSport, animate: animate && practiceSequence == nil)
        if let sequence = practiceSequence { context.coordinator.practice(sequence) }
        view.isPlaying = animate || practiceSequence != nil
        view.preferredFramesPerSecond = 30
    }
    static func dismantleUIView(_ view: SCNView, coordinator: Coordinator) {
        view.isPlaying = false
        coordinator.practiceTimer?.invalidate()
        coordinator.character.removeAllActions()
        coordinator.scene.rootNode.childNode(withName: "idleBall", recursively: false)?.removeAllActions()
        view.scene = nil
    }

    @MainActor final class Coordinator {
        let scene = SCNScene(), character = SCNNode()
        var framing: PreviewFraming = .body
        private var framed = false
        /// Hero head centre in the locker (Ready crouch): about y = 1.08. Close-ups hide the racket, which the
        /// Ready pose holds across the chin.
        static let headTarget = SCNVector3(0, 1.08, 0)
        func applyFraming() {
            guard let cam = scene.rootNode.childNodes.first(where: { $0.camera != nil }) else { return }
            framed = true
            character.childNode(withName: "heroV4", recursively: false)?.childNode(withName: "racket", recursively: false)?.isHidden = framing == .head
            if framing == .head { cam.position = SCNVector3(0, 1.13, 1.7); cam.look(at: Self.headTarget) }
            else { cam.position = SCNVector3(0, 0.9, heroCameraDistance); cam.look(at: SCNVector3(0, 0.76, 0)) }
        }
        func setFraming(_ f: PreviewFraming, view: SCNView, animated: Bool) {
            guard f != framing || !framed else { return }
            framing = f
            view.defaultCameraController.target = f == .head ? Self.headTarget : SCNVector3(0, 0.76, 0)
            view.pointOfView = scene.rootNode.childNodes.first(where: { $0.camera != nil })
            SCNTransaction.begin(); SCNTransaction.animationDuration = animated ? 0.45 : 0
            SCNTransaction.animationTimingFunction = CAMediaTimingFunction(name: .easeInEaseOut)
            applyFraming(); SCNTransaction.commit()
        }
        var previous: Player?
        private var lastPractice = 0
        var practiceTimer: Timer?
        func practice(_ sequence: Int) {
            guard sequence != lastPractice else { return }; lastPractice = sequence
            guard let root = character.childNode(withName: "heroV4", recursively: false) else { return }
            HeroV4.preparePractice(root)
            practiceTimer?.invalidate()
            let start = Date()
            let timer = Timer(timeInterval: 1.0 / 60, repeats: true) { [weak self] _ in
                MainActor.assumeIsolated {
                    guard let self, let root = self.character.childNode(withName: "heroV4", recursively: false) else { return }
                    let progress = min(1, Date().timeIntervalSince(start) / 1.5)
                    HeroV4.practiceFrame(root, progress: progress)
                    if progress >= 1 { self.practiceTimer?.invalidate(); self.practiceTimer = nil }
                }
            }
            practiceTimer = timer; RunLoop.main.add(timer, forMode: .common)
        }
        private var idleKey: String?
        let heroCameraDistance: Float
        init(cameraDistance: Float = 4.8) {
            heroCameraDistance = max(2.4, cameraDistance) * 1.05
            scene.rootNode.addChildNode(character)
            let camera = SCNNode(); camera.camera = SCNCamera(); camera.camera?.fieldOfView = 32; camera.camera?.zNear = 0.05   // close-ups sit ~1 m away
            camera.position = SCNVector3(0, 1.0, cameraDistance); camera.look(at: SCNVector3(0,0.92,0))
            scene.rootNode.addChildNode(camera)
            // Hero V5 light recipe (ArtDir/hero/v5_proof/LIGHTING.md), same philosophy as the court: warm soft key at
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
            character.childNodes.forEach { $0.removeFromParentNode() }
            character.scale = SCNVector3(1, 1, 1)
            // The locker mirror is the game's own Hero V4 (exported by HeroLockerExport), dressed by the
            // same rules the game uses (HeroKit.cs): what you pick here is what walks onto the court.
            if let hero = HeroV4.build(p) {
                character.addChildNode(hero)
                if !framed { applyFraming() }   // re-dressing never moves the camera the player has turned
                hero.childNode(withName: "racket", recursively: false)?.isHidden = framing == .head
                return
            }
        }
        /// Small original prop comedy: a ball bounces too high, or rolls past a golfer's feet.
        /// Reduced motion keeps the same sport-specific props in a still pose.
        func configureIdle(sport: Sport?, animate: Bool) {
            let key = "\(sport?.rawValue ?? "none")-\(animate)"
            guard key != idleKey else { return }; idleKey = key
            character.removeAllActions(); character.position = SCNVector3Zero; character.eulerAngles = SCNVector3Zero
            scene.rootNode.childNode(withName: "idleBall", recursively: false)?.removeFromParentNode()
            character.childNode(withName: "idleClub", recursively: false)?.removeFromParentNode()
            character.childNode(withName: "racket", recursively: true)?.isHidden = sport == .golf || framing == .head   // close-ups: the Ready racket crosses the chin
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
                ball.runAction(.repeatForever(.sequence([bounce, drop, .wait(duration: 0.2)])))
                character.runAction(.repeatForever(.sequence([
                    .rotateTo(x: -0.06, y: 0.12, z: -0.05, duration: 0.65),
                    .rotateTo(x: 0.03, y: -0.08, z: 0.05, duration: 0.65), .wait(duration: 0.2)
                ])))
            }
        }
        func node(_ name:String, player:Player, hair:Bool=false, part:String?=nil) -> SCNNode? {
            guard let mesh=CharacterMeshData.load(name) else { return nil }
            let geometry=mesh.geometry(player:player, shapeBody:part != nil && !(part?.hasPrefix("Hand") ?? false));let material=SCNMaterial();material.lightingModel = .blinn
            material.specular.contents=UIColor(white:0.025,alpha:1)
            material.roughness.contents=0.7;material.isDoubleSided=true
            if part == "Top" { material.diffuse.contents = UIColor(player.outfitColor("shirt") ?? Color(red:0.12,green:0.48,blue:0.68)) }
            else if part == "Bottom" { material.diffuse.contents = UIColor(player.outfitColor("shorts") ?? Color(red:0.035,green:0.08,blue:0.17)) }
            else if part == "Shoes" { material.diffuse.contents = UIColor(player.outfitColor("accent") ?? Color(red:0.9,green:0.93,blue:0.95)) }
            else if part == "Trim" { material.diffuse.contents = UIColor(white:0.94,alpha:1) }
            else if part == "Limb" || (part?.hasPrefix("Hand") ?? false) { material.diffuse.contents = UIColor(Color(hex:player.skinHex)) }
            else { material.diffuse.contents = hair ? UIColor(Color(hex:player.hairHex)) : CharacterTextures.recolor(mesh, player:player) }
            geometry.materials=[material];return SCNNode(geometry:geometry)
        }
    }
}

/// Uses the same four-region masks and luminance-preserving tint as Unity KitRecolor.
@MainActor enum CharacterTextures {
    static var tintCache: [String: UIImage] = [:]
    static var tintProfile = ""
    static var originals: [String:[UInt8]] = [:]
    static func bytes(_ name:String) -> [UInt8]? {
        if let b=originals[name] { return b }
        if let url=Bundle.main.url(forResource:name,withExtension:"mask"), let data=try? Data(contentsOf:url) { let b=Array(data); originals[name]=b; return b }
        guard let url=Bundle.main.url(forResource:name,withExtension:"png"), let image=UIImage(contentsOfFile:url.path)?.cgImage else { return nil }
        var b=[UInt8](repeating:0,count:512*512*4)
        b.withUnsafeMutableBytes { ptr in
            let ctx=CGContext(data:ptr.baseAddress,width:512,height:512,bitsPerComponent:8,bytesPerRow:2048,space:CGColorSpaceCreateDeviceRGB(),bitmapInfo:CGImageAlphaInfo.premultipliedLast.rawValue)!
            ctx.draw(image,in:CGRect(x:0,y:0,width:512,height:512))
        }
        originals[name]=b;return b
    }
    static func recolor(_ mesh:CharacterMeshData, player:Player) -> Any {
        let profile = "\(player.standardSkin)-\(player.shirt ?? -1)-\(player.shorts ?? -1)-\(player.accent ?? -1)"
        if profile != tintProfile { tintCache.removeAll(); tintProfile = profile }
        if let cached = tintCache[mesh.texture] { return cached }
        guard var pixels=bytes(mesh.texture), let mask=bytes(mesh.mask) else { return UIColor(Color(hex:player.skinHex)) }
        let hexes:[String?]=[player.outfitHex("shirt"),player.outfitHex("shorts"),player.outfitHex("accent"),player.skinHex]
        let colors=hexes.map { hex -> [Double]? in
            guard let hex, let n=UInt32(hex,radix:16) else { return nil }
            return [Double((n>>16)&255)/255,Double((n>>8)&255)/255,Double(n&255)/255]
        }
        for i in stride(from:0,to:pixels.count,by:4) {
            let lum=(Double(pixels[i])*0.2126+Double(pixels[i+1])*0.7152+Double(pixels[i+2])*0.0722)/255
            for channel in 0..<4 {
                guard mask[i+channel]>0, let color=colors[channel] else { continue }
                let weight=Double(mask[i+channel])/255
                let shade=min(1.35,max(0.25,lum/max(0.01,Double(mesh.reference[channel]))))
                for c in 0..<3 { pixels[i+c]=UInt8(min(255,max(0,Double(pixels[i+c])*(1-weight)+color[c]*shade*255*weight))) }
            }
        }
        let data=Data(pixels) as CFData
        guard let provider=CGDataProvider(data:data),let cg=CGImage(width:512,height:512,bitsPerComponent:8,bitsPerPixel:32,bytesPerRow:2048,space:CGColorSpaceCreateDeviceRGB(),bitmapInfo:CGBitmapInfo(rawValue:CGImageAlphaInfo.premultipliedLast.rawValue),provider:provider,decode:nil,shouldInterpolate:true,intent:.defaultIntent) else { return UIColor.white }
        let image=UIImage(cgImage:cg); tintCache[mesh.texture]=image; return image
    }
}

/// The locked Hero V4 as the game plays it: baked Ready pose + racket + the three hats
/// (CharacterAssets/HeroV4.json/.bin, written by Unity HeroLockerExport), recoloured with a Swift port of
/// Unity HeroKit / KitRecolor (linear-light, luminance-preserving region tint) so the mirror matches the court.
@MainActor enum HeroV4 {
    struct Sub: Decodable { let material: String; let indexOffset: Int; let indexCount: Int }
    struct Part: Decodable { let name: String; let hat: String?; let hair: String?; let body: String?; let vertexCount: Int, positionOffset: Int, normalOffset: Int, uvOffset: Int; let submeshes: [Sub] }
    struct Mat: Decodable { let name: String; let texture: String?; let color: [Float]; let transparent: Bool; let smoothness: Float }
    struct Manifest: Decodable { let parts: [Part]; let materials: [Mat] }
    struct Ref: Decodable { let shirt: Float, shorts: Float, hair: Float, skin: Float }
    static let headwear = ["None", "Visor", "Cap", "Sweatband"]
    static let haircuts = ["Swept", "Ponytail", "Bob", "Long", "Curly", "Bald", "Buzz", "Waves"]
    /// Haircuts the menus offer. Bald / Buzz / Waves stay out until the head rebuild lands (the V4 skull under
    /// them is a face panel on an oversized dome).
    nonisolated static var offered: Int { HeadRebuild.shipped ? 8 : 5 }
    nonisolated enum HeadRebuild { static let shipped = false }
    static var manifest: Manifest?, ref: Ref?, geometry: [String: SCNGeometry] = [:]
    static var atlasCache: (key: String, image: UIImage)?
    static var iris: UIImage?

    static func load() -> Bool {
        if manifest != nil { return true }
        let menu = Bundle.main.url(forResource: "HeroMenu", withExtension: "json") != nil
        guard let mu = Bundle.main.url(forResource: menu ? "HeroMenu" : "HeroV4", withExtension: "json"), let md = try? Data(contentsOf: mu),
              let m = try? JSONDecoder().decode(Manifest.self, from: md),
              let bin = previewData(menu ? "HeroMenu" : "HeroV4") else { return false }
        if let ru = Bundle.main.url(forResource: "HeroV4_KitRef", withExtension: "json"), let rd = try? Data(contentsOf: ru) { ref = try? JSONDecoder().decode(Ref.self, from: rd) }
        for (i, part) in m.parts.enumerated() {
            func src(_ off: Int, _ comps: Int, _ sem: SCNGeometrySource.Semantic) -> SCNGeometrySource {
                SCNGeometrySource(data: bin.subdata(in: off ..< off + part.vertexCount * comps * 4), semantic: sem, vectorCount: part.vertexCount,
                                  usesFloatComponents: true, componentsPerVector: comps, bytesPerComponent: 4, dataOffset: 0, dataStride: comps * 4)
            }
            let elements = part.submeshes.map { sub in
                SCNGeometryElement(data: bin.subdata(in: sub.indexOffset ..< sub.indexOffset + sub.indexCount * 4), primitiveType: .triangles,
                                   primitiveCount: sub.indexCount / 3, bytesPerIndex: 4)
            }
            geometry["\(i)"] = SCNGeometry(sources: [src(part.positionOffset, 3, .vertex), src(part.normalOffset, 3, .normal), src(part.uvOffset, 2, .texcoord)], elements: elements)
        }
        if let u = Bundle.main.url(forResource: "HeroV4_Iris", withExtension: "png") { iris = UIImage(contentsOfFile: u.path) }
        manifest = m; return true
    }

    static var hairDetailCache: [String: UIImage] = [:]
    static func hairDetail(_ name: String) -> UIImage? {
        if let c = hairDetailCache[name] { return c }
        guard let u = Bundle.main.url(forResource: "HeroV4_" + name, withExtension: "png"), let img = UIImage(contentsOfFile: u.path) else { return nil }
        hairDetailCache[name] = img; return img
    }
    static func rgb(_ hex: String) -> SIMD3<Float> {
        let n = UInt32(hex, radix: 16) ?? 0xFFFFFF
        return SIMD3(Float((n >> 16) & 255) / 255, Float((n >> 8) & 255) / 255, Float(n & 255) / 255)
    }
    static func color(_ c: SIMD3<Float>) -> UIColor { UIColor(red: CGFloat(c.x), green: CGFloat(c.y), blue: CGFloat(c.z), alpha: 1) }

    static func build(_ p: Player) -> SCNNode? {
        guard load(), let m = manifest else { return nil }
        let root = SCNNode(); root.name = "heroV4"; root.eulerAngles.y = .pi + 0.35   // exported facing -z; face the camera, a touch of 3/4
        root.scale = SCNVector3(0.92, 0.92, 0.92)   // the chibi hero is wide in Ready: fit the mirror frame
        let racket = SCNNode(); racket.name = "racket"; root.addChildNode(racket)
        let wanted = headwear[max(0, min(3, p.hairStyle))]
        // one haircut (fall back to Swept if this locker bake predates the others)
        let cuts = Set(m.parts.compactMap { $0.hair }), cut = cuts.contains(haircuts[p.shownHaircut]) ? haircuts[p.shownHaircut] : "Swept"
        let skin = rgb(p.skinHex), hair = rgb(p.hairHex)
        let mats = Dictionary(uniqueKeysWithValues: m.materials.map { ($0.name, $0) })
        let atlas = tintedAtlas(p)
        let mirrored = p.handedness == .left
        for (i, part) in m.parts.enumerated() {
            if let hat = part.hat, !hat.isEmpty, hat == "Worn" ? wanted == "None" : hat != wanted { continue }
            let short = ["Bald", "Buzz", "Waves"].contains(cut)
            if let h = part.hair, !h.isEmpty, h == "Short" ? !short : h != cut { continue }
            if part.hat != nil, !(part.hat ?? "").isEmpty, part.hat != "Worn", part.hat != "None", (part.hair ?? "").isEmpty, short { continue }   // short cuts wear the "Short" hats
            if let b = part.body, !b.isEmpty, b != (p.standardFemale ? "Girl" : "Boy") { continue }
            guard let base = geometry["\(i)"]?.copy() as? SCNGeometry else { continue }
            base.materials = part.submeshes.map { sub in
                let info = mats[sub.material]; let mat = SCNMaterial()
                mat.lightingModel = .blinn; mat.specular.contents = UIColor(white: 0.05, alpha: 1); mat.shininess = 0.15   // soft plastic, no specular blobs
                mat.isDoubleSided = true   // the hero's Unity materials render with Cull Off: thin shells (hair!) need both sides
                var c = SIMD3<Float>(info?.color[0] ?? 1, info?.color[1] ?? 1, info?.color[2] ?? 1)
                let n = sub.material
                if n.hasPrefix("Hero_01_HairTuft") { c = hair }
                else if n.contains("CoveredFoundation") { c = hair * 0.8 }
                else if n.hasPrefix("Hero lid skin") { c = skin * SIMD3(0.97, 0.9, 0.86) }
                else if n.hasPrefix("Hero_01_ShoeCleanWhite"), let a = p.outfitHex("accent") { c = rgb(a) }
                else if n.hasPrefix("Hero_Racket_Blue"), let r = p.outfitHex("racket") { c = rgb(r) }
                if info?.texture == "atlas", let atlas { mat.diffuse.contents = atlas }
                else if info?.texture == "iris", let iris { mat.diffuse.contents = iris }
                else if let t = info?.texture, t.hasPrefix("Hair_"), let img = hairDetail(t) {
                    mat.diffuse.contents = img; mat.diffuse.wrapS = .repeat; mat.diffuse.wrapT = .repeat
                    let l = c.x * 0.2126 + c.y * 0.7152 + c.z * 0.0722   // keep the pattern readable on near-black hair (HeroKit.DetailTint)
                    mat.multiply.contents = color(l >= 0.16 ? c : c + SIMD3(repeating: 0.16 - l))
                }
                else { mat.diffuse.contents = color(c) }
                if info?.transparent == true { mat.transparency = CGFloat(info?.color[3] ?? 1); mat.writesToDepthBuffer = false }
                return mat
            }
            let node = SCNNode(geometry: base); node.name = part.name
            node.setValue(i, forKey: "menuPartIndex")
            (part.name.hasPrefix("Hero_Racket") || part.name.hasPrefix("TwoHand") ? racket : root).addChildNode(node)
        }
        if mirrored { root.scale.x = -root.scale.x }
        return root
    }


    static func previewData(_ name: String) -> Data? {
        if let u = Bundle.main.url(forResource: name, withExtension: "lzfse"),
           let d = try? Data(contentsOf: u), let raw = try? (d as NSData).decompressed(using: .lzfse) { return raw as Data }
        guard let u = Bundle.main.url(forResource: name, withExtension: "bin") else { return nil }
        return try? Data(contentsOf: u)
    }

    /// Vertex targets sampled from the unchanged gameplay Forehand. Every wardrobe variant is
    /// baked with the same indices as HeroMenu, so live tint/slot choices are preserved.
    static func preparePractice(_ root: SCNNode) {
        guard let manifest else { return }
        var nodes: [SCNNode] = []
        root.enumerateChildNodes { node, _ in if node.value(forKey: "menuPartIndex") != nil { nodes.append(node) } }
        if nodes.first?.morpher == nil {
            var targets: [Int: [SCNGeometry]] = [:]
            for frame in [0, 3, 6, 9, 12, 15, 18, 19] {
                guard let data = previewData(String(format: "HeroSwing_%02d", frame)) else { return }
                for node in nodes {
                    guard let i = node.value(forKey: "menuPartIndex") as? Int, manifest.parts.indices.contains(i), let base = node.geometry else { continue }
                    let part = manifest.parts[i], length = part.vertexCount * 12
                    guard part.normalOffset + length <= data.count else { return }
                    let sources = [(part.positionOffset, SCNGeometrySource.Semantic.vertex), (part.normalOffset, .normal)].map { offset, semantic in
                        SCNGeometrySource(data: data.subdata(in: offset..<offset+length), semantic: semantic, vectorCount: part.vertexCount,
                                          usesFloatComponents: true, componentsPerVector: 3, bytesPerComponent: 4, dataOffset: 0, dataStride: 12)
                    }
                    targets[i, default: []].append(SCNGeometry(sources: sources, elements: base.elements))
                }
            }
            for node in nodes {
                guard let i = node.value(forKey: "menuPartIndex") as? Int, let t = targets[i], t.count == 8 else { continue }
                let morph = SCNMorpher(); morph.targets = t; morph.calculationMode = .normalized; morph.unifiesNormals = false; node.morpher = morph
            }
        }
    }

    static func practiceFrame(_ root: SCNNode, progress: Double) {
        let active = min(1, progress / 0.1) * min(1, (1 - progress) / 0.15)
        let frame = min(7, max(0, (progress - 0.1) / 0.75 * 7))
        let lo = Int(frame), hi = min(7, lo + 1), mix = frame - Double(lo)
        SCNTransaction.begin(); SCNTransaction.disableActions = true
        root.enumerateChildNodes { node, _ in
            guard let morph = node.morpher else { return }
            for i in 0..<8 { morph.setWeight((i == lo ? (1 - mix) * active : 0) + (i == hi ? mix * active : 0), forTargetAt: i) }
        }
        SCNTransaction.commit()
    }

    /// Port of Unity KitRecolor as HeroKit drives it (linear light; R shirt, G shorts+trim, B baked hair, skin mask).
    static func tintedAtlas(_ p: Player) -> UIImage? {
        let key = "\(p.skinHex)-\(p.hairHex)-\(p.outfitHex("shirt") ?? "-")-\(p.outfitHex("shorts") ?? "-")"
        if let c = atlasCache, c.key == key { return c.image }
        let size = 1024
        func pixels(_ name: String) -> [UInt8]? {
            guard let u = Bundle.main.url(forResource: name, withExtension: "png"), let img = UIImage(contentsOfFile: u.path)?.cgImage else { return nil }
            var b = [UInt8](repeating: 0, count: size * size * 4)
            b.withUnsafeMutableBytes { ptr in
                let ctx = CGContext(data: ptr.baseAddress, width: size, height: size, bitsPerComponent: 8, bytesPerRow: size * 4,
                                    space: CGColorSpace(name: CGColorSpace.sRGB)!, bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)!
                ctx.interpolationQuality = .high; ctx.draw(img, in: CGRect(x: 0, y: 0, width: size, height: size))
            }
            return b
        }
        guard var px = pixels("HeroV4_Atlas"), let mask = pixels("HeroV4_KitMask"), let skinMask = pixels("HeroV4_SkinMask"), let ref else { return nil }
        let toLin: [Float] = (0..<256).map { i in let c = Float(i) / 255; return c <= 0.04045 ? c / 12.92 : powf((c + 0.055) / 1.055, 2.4) }
        func lin(_ c: SIMD3<Float>) -> SIMD3<Float> { SIMD3(toLin[Int(c.x * 255)], toLin[Int(c.y * 255)], toLin[Int(c.z * 255)]) }
        func srgb(_ v: Float) -> UInt8 { let c = max(0, min(1, v)); let s = c <= 0.0031308 ? c * 12.92 : 1.055 * powf(c, 1 / 2.4) - 0.055; return UInt8(max(0, min(255, s * 255 + 0.5))) }
        let targets: [SIMD3<Float>?] = [p.outfitHex("shirt").map { lin(rgb($0)) }, p.outfitHex("shorts").map { lin(rgb($0)) },
                                        lin(rgb(p.hairHex)), lin(rgb(p.skinHex))]
        let refs: [Float] = [ref.shirt, ref.shorts, ref.hair, ref.skin]
        px.withUnsafeMutableBufferPointer { o in
            mask.withUnsafeBufferPointer { mk in
                skinMask.withUnsafeBufferPointer { sk in
                    for i in stride(from: 0, to: size * size * 4, by: 4) {
                        let w: [Float] = [Float(mk[i]) / 255, Float(mk[i + 1]) / 255, Float(mk[i + 2]) / 255, Float(sk[i]) / 255]
                        if w[0] + w[1] + w[2] + w[3] <= 0 { continue }
                        var c = SIMD3<Float>(toLin[Int(o[i])], toLin[Int(o[i + 1])], toLin[Int(o[i + 2])])
                        let l = c.x * 0.2126 + c.y * 0.7152 + c.z * 0.0722
                        for k in 0..<4 {
                            guard w[k] > 0, let t = targets[k] else { continue }
                            let lu = k == 3 ? refs[3] + (l - refs[3]) * 0.45 : l
                            let shaded = t * min(1.35, max(0.25, lu / max(refs[k], 0.01)))
                            c = c + (shaded - c) * min(1, w[k])
                        }
                        o[i] = srgb(c.x); o[i + 1] = srgb(c.y); o[i + 2] = srgb(c.z)
                    }
                }
            }
        }
        let data = Data(px) as CFData
        guard let provider = CGDataProvider(data: data),
              let cg = CGImage(width: size, height: size, bitsPerComponent: 8, bitsPerPixel: 32, bytesPerRow: size * 4, space: CGColorSpace(name: CGColorSpace.sRGB)!,
                               bitmapInfo: CGBitmapInfo(rawValue: CGImageAlphaInfo.premultipliedLast.rawValue), provider: provider, decode: nil, shouldInterpolate: true, intent: .defaultIntent) else { return nil }
        let image = UIImage(cgImage: cg); atlasCache = (key, image); return image
    }
}
