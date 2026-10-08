import SceneKit
import UIKit

/// The lobby's scene: a round plaza with a tower in the middle, four short paths, and the places they lead to. Everything is placed from `LobbyLayout`;
/// the shapes are simple modelled forms in the club's palette (ivory plaster, teak, navy, terracotta, lime), lit by one warm sun with soft shadows.
@MainActor final class LobbyScene {
    let layout: LobbyLayout
    let scene = SCNScene()
    /// Characters are added here (so the scene's own geometry can be flattened without touching them).
    let people = SCNNode()
    let cameraNode = SCNNode()
    let sun = SCNNode()
    /// The glowing ring of each station, and the floating name above it.
    private(set) var rings: [String: SCNNode] = [:]
    private(set) var labels: [String: SCNNode] = [:]
    /// A big invisible floor for taps to land on.
    private(set) var tapPlane = SCNNode()
    private(set) var pennants: [SCNNode] = []

    typealias K = LobbyKit
    typealias P = LobbyLayout.P

    init(layout: LobbyLayout = .standard) {
        self.layout = layout
        scene.rootNode.name = "lobby"
        build()
    }

    /// Layout (x east, y north) to scene (x, y, z): north is -z.
    static func scn(_ p: P, _ y: Float = 0) -> SCNVector3 { SCNVector3(p.x, y, -p.y) }

    // MARK: assembly
    private func build() {
        sky()
        lights()
        // each area is merged into a few draw calls on its own (one big merge drops pieces)
        for area in [ground(), plaza(), tower(), paths(), lockerRoom(), playGate(), partyTerrace(), proShop(), greenery()] {
            #if DEBUG
            if ProcessInfo.processInfo.environment["LOBBY_NOFLATTEN"] != nil { scene.rootNode.addChildNode(area); continue }
            #endif
            scene.rootNode.addChildNode(Self.hoisted(area).flattenedClone())
        }
        scene.rootNode.addChildNode(people)
        stationMarkers()
        flags()
        camera()
        tapSurface()
    }

    /// A copy of an area with every piece of geometry moved up to be a direct child of the area (at the place it stands), so that merging it cannot lose a piece that sat inside a group.
    static func hoisted(_ area: SCNNode) -> SCNNode {
        let flat = SCNNode(); flat.name = area.name
        area.enumerateChildNodes { node, _ in
            guard node.geometry != nil else { return }
            let copy = SCNNode(geometry: node.geometry)
            copy.transform = node.convertTransform(SCNMatrix4Identity, to: area)
            copy.castsShadow = node.castsShadow; copy.renderingOrder = node.renderingOrder
            flat.addChildNode(copy)
        }
        return flat
    }

    // MARK: sky, light, camera
    private func sky() {
        let image = K.skyImage()
        scene.background.contents = image
        scene.lightingEnvironment.contents = image
        scene.lightingEnvironment.intensity = 0.9
        scene.fogColor = K.colour("CFE5EE"); scene.fogStartDistance = 90; scene.fogEndDistance = 330; scene.fogDensityExponent = 1.4
    }
    private func lights() {
        let key = SCNLight(); key.type = .directional; key.color = K.colour("FFF1D6"); key.intensity = 1250; key.castsShadow = true
        key.shadowMapSize = CGSize(width: 2048, height: 2048); key.shadowRadius = 3.2; key.shadowSampleCount = 8
        key.shadowColor = UIColor(red: 0.10, green: 0.14, blue: 0.30, alpha: 0.38)
        key.automaticallyAdjustsShadowProjection = true; key.maximumShadowDistance = 70; key.shadowCascadeCount = 2
        sun.light = key; sun.eulerAngles = SCNVector3(-0.95, 0.55, 0)
        scene.rootNode.addChildNode(sun)
        let fill = SCNNode(); let amb = SCNLight(); amb.type = .ambient; amb.color = K.colour("C9DBF2"); amb.intensity = 520
        fill.light = amb; scene.rootNode.addChildNode(fill)
    }
    private func camera() {
        let cam = SCNCamera(); cam.zNear = 0.3; cam.zFar = 520; cam.fieldOfView = 50; cam.wantsHDR = false
        cameraNode.camera = cam; cameraNode.name = "lobbyCamera"
        scene.rootNode.addChildNode(cameraNode)
    }
    private func tapSurface() {
        let plane = SCNPlane(width: 160, height: 160)
        let m = SCNMaterial(); m.diffuse.contents = UIColor.clear; m.colorBufferWriteMask = []; m.writesToDepthBuffer = false
        plane.materials = [m]
        tapPlane = SCNNode(geometry: plane); tapPlane.eulerAngles.x = -.pi / 2; tapPlane.position.y = 0; tapPlane.name = "tapPlane"; tapPlane.opacity = 0.01
        scene.rootNode.addChildNode(tapPlane)
    }

    // MARK: the island
    private func ground() -> SCNNode {
        let n = SCNNode(); n.name = "ground"
        let grass = K.textured("grass", tile: 32)
        K.cylinder(58, 1.6, y: -1.74, mat: grass, segments: 64, parent: n)                 // top of the lawn at -0.14: the plaza stands a little above it
        K.cone(top: 58, bottom: 63, 3.6, y: -5.3, mat: K.mat("D8C9A6"), segments: 64, parent: n)   // the cliff
        // the sea
        let sea = SCNPlane(width: 1400, height: 1400)
        let sm = SCNMaterial(); sm.lightingModel = .blinn; sm.diffuse.contents = K.colour(K.turquoise); sm.specular.contents = UIColor(white: 0.9, alpha: 1); sm.shininess = 0.6
        sea.materials = [sm]
        let seaNode = SCNNode(geometry: sea); seaNode.eulerAngles.x = -.pi / 2; seaNode.position.y = -3.4; n.addChildNode(seaNode)
        // far hills, hazy
        let hills = K.mat("86A892")
        for (x, y, r, h) in [(-130, 210, 80, 26), (-30, 280, 100, 34), (120, 250, 90, 28), (250, 130, 80, 22), (-270, 70, 90, 26), (-210, -160, 70, 16), (190, -230, 80, 18)] as [(Float, Float, Float, Float)] {
            K.sphere(r, x: x, y: -h * 0.3, z: -y, mat: hills, squash: h / r, segments: 24, parent: n)
        }
        // lawn swells
        var rng = K.SeededRandom(seed: 5)
        for _ in 0..<16 {
            let a = Float(rng.next(in: 0...(2 * Double.pi))), r = Float(rng.next(in: 30...50)), s = Float(rng.next(in: 5...9))
            K.sphere(s, x: cos(a) * r, y: -0.14, z: sin(a) * r, mat: grass, squash: 0.12, segments: 12, parent: n)
        }
        return n
    }

    // MARK: the plaza
    private func plaza() -> SCNNode {
        let n = SCNNode(); n.name = "plaza"
        let pavers = K.textured("pavers", tile: 8.6)
        K.cylinder(LobbyLayout.plazaRadius, 0.16, y: -0.16, mat: pavers, segments: 64, parent: n)
        K.cylinder(LobbyLayout.plazaRadius + 0.4, 0.1, y: -0.17, mat: K.mat(K.limestone), segments: 64, parent: n)
        K.cylinder(6.25, 0.01, y: 0.001, mat: K.mat(K.navy), segments: 64, parent: n)
        K.cylinder(5.95, 0.01, y: 0.003, mat: K.textured("pavers", tile: 6), segments: 64, parent: n)
        K.cylinder(4.5, 0.01, y: 0.005, mat: K.mat(K.lime), segments: 64, parent: n)
        K.cylinder(4.25, 0.01, y: 0.007, mat: K.textured("pavers", tile: 4.5), segments: 64, parent: n)
        for k in 0..<4 {   // the compass star
            let a = Float(k) * .pi / 2
            K.box(0.3, 0.01, 4.6, x: cos(a) * 3.0, y: 0.009, z: sin(a) * 3.0, mat: K.mat(K.navy), chamfer: 0, yaw: -a + .pi / 2, parent: n)
        }
        // lamps
        for k in 0..<8 {
            let a = Float(22.5 + 45 * Double(k)) * .pi / 180
            lamp(at: P(cos(a) * 8.0, sin(a) * 8.0), parent: n)
        }
        bench(at: P(5.2, -6.2), heading: -.pi / 4 - .pi / 2, parent: n)
        bench(at: P(-5.2, 6.2), heading: .pi * 3 / 4 - .pi / 2, parent: n)
        for (i, p) in [P(6.4, 5.4), P(-6.6, -4.8), P(6.8, -2.0), P(-6.9, 2.2)].enumerated() { planter(at: p, seed: i, parent: n) }
        // the way-finders at the mouths of the paths, turned to face the middle of the plaza
        banner("LOCKER", at: P(-9.4, 3.2), n)
        banner("PLAY", at: P(-3.2, 9.5), n)
        banner("PARTY", at: P(9.4, 3.2), n)
        banner("SHOP", at: P(-9.0, -5.0), n, fill: "7B8196")
        return n
    }

    private func lamp(at p: P, parent: SCNNode) {
        let dark = K.mat("1D2144")
        let g = SCNNode(); g.position = LobbyScene.scn(p)
        K.cylinder(0.09, 3.3, mat: dark, segments: 10, parent: g)
        K.cylinder(0.2, 0.12, mat: dark, segments: 10, parent: g)
        K.sphere(0.2, y: 3.45, mat: K.mat("FFE29A", emission: true), parent: g)
        K.cone(top: 0.02, bottom: 0.28, 0.2, y: 3.62, mat: dark, segments: 8, parent: g)
        parent.addChildNode(g)
    }
    private func bench(at p: P, heading: Float, parent: SCNNode) {
        let g = SCNNode(); g.position = LobbyScene.scn(p); g.eulerAngles.y = -heading
        let teak = K.mat(K.teak), iron = K.mat("1D2144")
        K.box(1.7, 0.09, 0.5, y: 0.45, mat: teak, parent: g)
        K.box(1.7, 0.45, 0.08, y: 0.55, z: 0.24, mat: teak, parent: g)
        for s: Float in [-0.7, 0.7] { K.box(0.1, 0.45, 0.45, x: s, mat: iron, parent: g) }
        parent.addChildNode(g)
    }
    private func planter(at p: P, seed: Int, parent: SCNNode) {
        let g = SCNNode(); g.position = LobbyScene.scn(p)
        K.cone(top: 0.7, bottom: 0.58, 0.7, mat: K.mat(K.terracotta), segments: 18, parent: g)
        K.sphere(0.78, y: 1.0, mat: K.textured("hedge", tile: 2), squash: 0.8, parent: g)
        var rng = K.SeededRandom(seed: UInt64(seed + 1))
        for _ in 0..<14 {
            K.sphere(0.07, x: Float(rng.next(in: -0.6...0.6)), y: Float(rng.next(in: 1.1...1.55)), z: Float(rng.next(in: -0.6...0.6)), mat: K.mat(K.coral), segments: 6, parent: g)
        }
        parent.addChildNode(g)
    }
    /// A pole with a hanging pennant sign, turned toward the middle of the plaza.
    private func banner(_ text: String, at p: P, _ parent: SCNNode, fill: String = LobbyKit.navy) {
        let g = SCNNode(); g.position = LobbyScene.scn(p)
        let toCentre = atan2(-p.x, -p.y)   // heading from the pole to the plaza's centre
        g.eulerAngles.y = -toCentre
        K.cylinder(0.08, 3.6, mat: K.mat("1D2144"), segments: 8, parent: g)
        K.box(2.7, 0.06, 0.06, x: 1.3, y: 3.3, mat: K.mat("1D2144"), parent: g)
        let s = K.sign(text, width: 2.4, height: 0.8, fill: fill)
        s.position = SCNVector3(1.3, 2.45, 0); s.eulerAngles.y = .pi   // its face looks along +z, toward the plaza
        g.addChildNode(s)
        parent.addChildNode(g)
    }

    // MARK: the tower
    private func tower() -> SCNNode {
        let n = SCNNode(); n.name = "tower"
        let ivory = K.textured("ivory", tile: 3), navy = K.mat(K.navy), dark = K.mat("1F1B2E"), white = K.mat(K.white), brass = K.mat(K.gold)
        K.box(5.6, 0.9, 5.6, y: 0, mat: K.textured("pavers", tile: 3), chamfer: 0.1, parent: n)
        K.box(4.4, 7.2, 4.4, y: 0.9, mat: ivory, chamfer: 0.12, parent: n)
        K.box(4.75, 0.38, 4.75, y: 5.2, mat: navy, chamfer: 0.06, parent: n)
        // tall windows and the club crest on the south face
        for sx: Float in [-1.1, 1.1] {
            K.box(0.8, 1.9, 0.1, x: sx, y: 2.2, z: 2.2, mat: dark, chamfer: 0.02, parent: n)
            K.cylinder(0.4, 0.1, x: sx, y: 4.1, z: 2.2, mat: dark, segments: 16, parent: n).eulerAngles.x = .pi / 2
        }
        K.box(0.9, 1.15, 0.08, y: 2.6, z: 2.22, mat: navy, chamfer: 0.04, parent: n)
        K.cylinder(0.22, 0.04, y: 3.05, z: 2.27, mat: K.mat("FAD14A", emission: true), segments: 20, parent: n).eulerAngles.x = .pi / 2
        K.box(3.7, 4.3, 3.7, y: 8.1, mat: ivory, chamfer: 0.1, parent: n)
        for (dx, dz, yaw): (Float, Float, Float) in [(0, 1.86, 0), (0, -1.86, 0), (1.86, 0, .pi / 2), (-1.86, 0, .pi / 2)] {
            let g = SCNNode(); g.position = SCNVector3(dx, 0, dz); g.eulerAngles.y = yaw
            K.box(1.0, 1.7, 0.1, y: 10.0, mat: dark, chamfer: 0.02, parent: g)
            K.cylinder(0.5, 0.1, y: 11.7, mat: dark, segments: 16, parent: g).eulerAngles.x = .pi / 2
            // the clock on every face
            let face = SCNPlane(width: 1.5, height: 1.5); face.materials = [LobbyKit.signMaterial(Self.clockImage)]
            let f = SCNNode(geometry: face); f.position = SCNVector3(0, 8.9, 0.07); g.addChildNode(f)
            n.addChildNode(g)
        }
        K.box(4.0, 0.5, 4.0, y: 12.4, mat: navy, chamfer: 0.06, parent: n)
        K.roof(4.7, 4.7, 5.4, y: 12.9, mat: K.mat(K.terracotta), parent: n)
        K.cylinder(0.07, 1.7, y: 18.2, mat: brass, segments: 8, parent: n)
        // the pennant (it flutters)
        let flag = SCNNode(); flag.position = SCNVector3(0, 19.2, 0)
        let plane = SCNPlane(width: 1.3, height: 0.8)
        let fm = LobbyKit.signMaterial(Self.pennantImage); fm.isDoubleSided = true; plane.materials = [fm]
        let pn = SCNNode(geometry: plane); pn.position = SCNVector3(0.65, 0, 0); flag.addChildNode(pn)
        flag.runAction(.repeatForever(.sequence([.rotateBy(x: 0, y: 0.18, z: 0, duration: 1.7), .rotateBy(x: 0, y: -0.36, z: 0, duration: 3.4), .rotateBy(x: 0, y: 0.18, z: 0, duration: 1.7)])))
        n.addChildNode(flag)
        _ = white
        return n
    }
    private static let clockImage: UIImage = UIGraphicsImageRenderer(size: CGSize(width: 256, height: 256)).image { ctx in
        let g = ctx.cgContext
        LobbyKit.colour(LobbyKit.navy).setFill(); g.fillEllipse(in: CGRect(x: 4, y: 4, width: 248, height: 248))
        LobbyKit.colour(LobbyKit.white).setFill(); g.fillEllipse(in: CGRect(x: 18, y: 18, width: 220, height: 220))
        LobbyKit.colour(LobbyKit.navy).setStroke(); g.setLineCap(.round)
        for i in 0..<12 {
            let a = CGFloat(i) * .pi / 6, r1: CGFloat = i % 3 == 0 ? 84 : 96, r2: CGFloat = 108
            g.setLineWidth(i % 3 == 0 ? 7 : 3)
            g.move(to: CGPoint(x: 128 + sin(a) * r1, y: 128 - cos(a) * r1)); g.addLine(to: CGPoint(x: 128 + sin(a) * r2, y: 128 - cos(a) * r2)); g.strokePath()
        }
        g.setLineWidth(9); g.move(to: CGPoint(x: 128, y: 128)); g.addLine(to: CGPoint(x: 128 - 34, y: 128 - 30)); g.strokePath()
        g.setLineWidth(6); g.move(to: CGPoint(x: 128, y: 128)); g.addLine(to: CGPoint(x: 128 + 20, y: 128 - 78)); g.strokePath()
        LobbyKit.colour(LobbyKit.terracotta).setFill(); g.fillEllipse(in: CGRect(x: 120, y: 120, width: 16, height: 16))
    }
    private static let pennantImage: UIImage = UIGraphicsImageRenderer(size: CGSize(width: 260, height: 160)).image { ctx in
        LobbyKit.colour(LobbyKit.navy).setFill(); ctx.fill(CGRect(x: 0, y: 0, width: 260, height: 160))
        LobbyKit.colour("FAD14A").setFill(); ctx.cgContext.fillEllipse(in: CGRect(x: 150, y: 24, width: 84, height: 84))
        LobbyKit.colour(LobbyKit.white).setFill()
        let palm = UIBezierPath(); palm.move(to: CGPoint(x: 92, y: 140)); palm.addLine(to: CGPoint(x: 100, y: 70)); palm.addLine(to: CGPoint(x: 108, y: 140)); palm.fill()
        for a in [-1.2, -0.6, 0, 0.6, 1.2] as [CGFloat] {
            ctx.cgContext.saveGState(); ctx.cgContext.translateBy(x: 100, y: 70); ctx.cgContext.rotate(by: a)
            ctx.cgContext.fillEllipse(in: CGRect(x: -6, y: -62, width: 12, height: 58)); ctx.cgContext.restoreGState()
        }
    }

    // MARK: paths and hedges
    private func paths() -> SCNNode {
        let n = SCNNode(); n.name = "paths"
        let top = K.textured("pavers", tile: 3)
        func strip(_ center: P, length: Float, yaw: Float) {   // yaw: counter-clockwise from +x seen from above
            K.box(length, 0.13, 4.4, x: center.x, y: -0.14, z: -center.y, mat: top, chamfer: 0, yaw: yaw, parent: n)
        }
        strip(P(-13.1, 0), length: 10.4, yaw: 0)
        strip(P(0, 13.1), length: 10.4, yaw: .pi / 2)
        strip(P(13.1, 0), length: 10.4, yaw: 0)
        let d = Float(0.70710678)
        strip(P(-12.4 * d, -12.4 * d), length: 9.0, yaw: .pi / 4)
        // hedges along the paths (short runs: the places they lead to open out)
        func hedge(_ center: P, length: Float, yaw: Float) {
            K.box(length, 0.95, 0.85, x: center.x, y: -0.14, z: -center.y, mat: K.textured("hedge", tile: 2), chamfer: 0.3, yaw: yaw, parent: n)
        }
        for s: Float in [-1, 1] {
            hedge(P(-10.3, s * 2.65), length: 3.4, yaw: 0)
            hedge(P(10.3, s * 2.65), length: 3.4, yaw: 0)
            hedge(P(s * 2.65, 10.3), length: 3.4, yaw: .pi / 2)
            // the south-west path runs along the diagonal
            let off = s * 2.65
            hedge(P(-10.0 * d + off * d, -10.0 * d - off * d), length: 4.6, yaw: .pi / 4)
        }
        return n
    }

    // MARK: the locker room (west)
    private func lockerRoom() -> SCNNode {
        let n = SCNNode(); n.name = "locker"
        let lx: Float = -17.5
        let planks = K.textured("planks", tile: 5), teak = K.mat(K.teak), navy = K.mat(K.navy), brass = K.mat(K.gold), ivory = K.textured("ivory", tile: 3)
        K.box(11.0, 0.2, 9.4, x: lx, y: -0.2, mat: planks, chamfer: 0, parent: n)
        for px: Float in [-4.9, 0, 4.9] { for s: Float in [-1, 1] { K.box(0.34, 3.7, 0.34, x: lx + px, z: s * 4.5, mat: teak, parent: n) } }
        for px: Float in [-4.9, 0, 4.9] { K.box(0.4, 0.35, 9.6, x: lx + px, y: 3.7, mat: teak, parent: n) }
        for s: Float in [-1, 1] { K.box(10.4, 0.35, 0.35, x: lx, y: 3.7, z: s * 4.5, mat: teak, parent: n) }
        K.roof(12.6, 10.8, 1.7, x: lx, y: 4.05, mat: K.mat(K.terracotta), parent: n)
        K.box(0.4, 3.6, 9.4, x: lx - 5.2, mat: ivory, chamfer: 0.03, parent: n)
        K.box(0.14, 1.25, 9.0, x: lx - 4.95, mat: navy, chamfer: 0, parent: n)
        for i in 0..<9 {
            let z = -4.0 + Float(i)
            K.box(0.1, 2.35, 0.86, x: lx - 4.86, y: 0.1, z: z, mat: navy, chamfer: 0.03, parent: n)
            K.box(0.03, 0.22, 0.5, x: lx - 4.78, y: 2.1, z: z, mat: brass, chamfer: 0, parent: n)
            K.box(0.04, 0.26, 0.06, x: lx - 4.78, y: 1.1, z: z + 0.28, mat: brass, chamfer: 0, parent: n)
        }
        for s: Float in [-1, 1] { K.box(10.4, 0.9, 0.2, x: lx, z: s * 4.7, mat: ivory, chamfer: 0.03, parent: n) }
        // gear: a rack with rackets and a cap
        let gear = SCNNode(); gear.position = SCNVector3(lx - 4.4, 0, -3.0)
        K.box(0.5, 1.9, 2.4, mat: teak, parent: gear)
        for i in 0..<4 {
            let z = -0.9 + Float(i) * 0.6
            K.cylinder(0.025, 1.3, x: 0.26, y: 0.35, z: z, mat: K.mat("2A2A33"), segments: 6, parent: gear)
            let head = K.cylinder(0.14, 0.03, x: 0.26, y: 1.55, z: z, mat: K.mat(i % 2 == 0 ? K.white : "2C5BD6"), segments: 16, parent: gear)
            head.eulerAngles.z = .pi / 2; head.scale = SCNVector3(1, 1, 0.7)
        }
        K.cylinder(0.28, 0.1, x: 0.15, y: 1.95, mat: K.mat(K.white), segments: 18, parent: gear)
        K.cylinder(0.19, 0.12, x: 0.15, y: 2.05, mat: navy, segments: 16, parent: gear)
        n.addChildNode(gear)
        // colours: a swatch wall
        let colours = SCNNode(); colours.position = SCNVector3(lx - 4.7, 0, 0)
        K.box(0.4, 2.7, 2.8, mat: ivory, parent: colours)
        for (i, c) in [K.coral, "F6C640", K.lime, K.turquoise, "3252C8", "8F4FB8"].enumerated() {
            K.box(0.06, 0.62, 0.7, x: 0.22, y: 0.7 + Float(i / 3) * 0.85, z: -0.95 + Float(i % 3) * 0.95, mat: K.mat(c), chamfer: 0.02, parent: colours)
        }
        n.addChildNode(colours)
        // emotes: a little round stage with footlights
        let stage = SCNNode(); stage.position = SCNVector3(lx - 3.3, 0, 3.0)
        K.cylinder(1.5, 0.22, mat: navy, segments: 32, parent: stage)
        K.cylinder(1.38, 0.04, y: 0.22, mat: K.mat(K.sand), segments: 32, parent: stage)
        K.box(0.4, 2.5, 2.8, x: -1.5, mat: K.mat("F29282"), parent: stage)
        for i in 0..<7 { K.sphere(0.07, x: -1.28, y: 2.2 - 0.07 * abs(Float(i - 3)), z: -1.3 + Float(i) * 0.43, mat: K.mat("FFE9A8", emission: true), segments: 8, parent: stage) }
        n.addChildNode(stage)
        // the name over the room
        let s = K.sign("LOCKER", width: 3.0, height: 0.8, fill: K.navy, ink: K.lime)
        s.position = SCNVector3(lx + 5.1, 3.0, 0); s.eulerAngles.y = -.pi / 2   // its face looks east, toward the plaza
        n.addChildNode(s)
        // a bench and a bag on the way in
        K.box(0.5, 0.45, 2.2, x: lx - 1.0, z: -3.9, mat: teak, parent: n)
        return n
    }

    // MARK: the play gate (north)
    private func playGate() -> SCNNode {
        let n = SCNNode(); n.name = "gate"
        let gz: Float = -18.5   // scene z of the gate line
        let ivory = K.textured("ivory", tile: 3), navy = K.mat(K.navy), terra = K.mat(K.terracotta)
        K.box(18.2, 0.2, 5.4, x: 0, y: -0.2, z: -14.5, mat: K.textured("pavers", tile: 5), chamfer: 0, parent: n)
        for (px, w): (Float, Float) in [(-8.2, 2.0), (-1.35, 1.5), (1.35, 1.5), (8.2, 2.0)] { K.box(w, 5.0, 2.2, x: px, y: 0, z: gz, mat: ivory, chamfer: 0.1, parent: n) }
        for ax: Float in [-4.8, 4.8] {
            K.box(8.6, 1.5, 2.2, x: ax, y: 5.0, z: gz, mat: ivory, chamfer: 0.1, parent: n)
            K.box(5.6, 0.26, 0.06, x: ax, y: 5.1, z: gz + 1.12, mat: navy, chamfer: 0, parent: n)
            K.roof(9.2, 3.0, 1.1, x: ax, y: 6.5, z: gz, mat: terra, parent: n)
        }
        K.box(19.0, 0.5, 2.4, x: 0, y: 6.5, z: gz, mat: navy, chamfer: 0.08, parent: n)
        let t = K.sign("TENNIS", width: 4.2, height: 1.0, fill: "1F7A52"); t.position = SCNVector3(-4.8, 3.9, gz + 1.15); t.eulerAngles.y = .pi; n.addChildNode(t)
        let g = K.sign("GOLF", width: 4.2, height: 1.0, fill: "205C2A"); g.position = SCNVector3(4.8, 3.9, gz + 1.15); g.eulerAngles.y = .pi; n.addChildNode(g)
        let title = K.sign("PLAY GATE", width: 6.0, height: 1.0, fill: K.navy, ink: K.lime); title.position = SCNVector3(0, 7.3, gz + 1.3); title.eulerAngles.y = .pi; n.addChildNode(title)
        // through the arches: a court and a fairway
        K.box(6.2, 0.06, 10.5, x: -4.8, y: -0.05, z: gz - 6.4, mat: K.mat("2D66C2"), chamfer: 0, parent: n)
        K.box(6.4, 0.07, 0.07, x: -4.8, y: 0.0, z: gz - 6.4, mat: K.mat(K.white), chamfer: 0, parent: n)
        K.box(0.06, 0.9, 5.6, x: -4.8, y: 0, z: gz - 6.4, mat: K.mat(K.white), chamfer: 0, parent: n).eulerAngles.y = .pi / 2
        let fairway = K.cylinder(4.5, 0.06, x: 4.8, y: -0.05, z: gz - 9.0, mat: K.mat("5FB44A"), segments: 32, parent: n); fairway.scale = SCNVector3(0.8, 1, 1.9)
        K.cylinder(0.03, 2.4, x: 6.4, y: 0, z: gz - 12.0, mat: K.mat(K.white), segments: 6, parent: n)
        K.box(0.7, 0.4, 0.03, x: 6.75, y: 1.9, z: gz - 12.0, mat: K.mat(K.coral), chamfer: 0, parent: n)
        // how to play: a standing board in the middle, on the line the walkable ground ends
        let board = SCNNode(); board.position = SCNVector3(0, 0, -17.2)
        for s: Float in [-0.9, 0.9] { K.box(0.14, 2.6, 0.14, x: s, mat: K.mat(K.teak), parent: board) }
        let sign = K.sign("HOW TO\nPLAY", width: 2.2, height: 1.5, fill: "F5ECD2", ink: K.navy, lines: 2)
        sign.position = SCNVector3(0, 1.6, 0); sign.eulerAngles.y = .pi; board.addChildNode(sign)
        n.addChildNode(board)
        return n
    }

    // MARK: the party terrace (east)
    private func partyTerrace() -> SCNNode {
        let n = SCNNode(); n.name = "terrace"
        let tx: Float = 18
        let planks = K.textured("planks", tile: 4), teak = K.mat(K.teak), navy = K.mat(K.navy)
        K.box(11.0, LobbyLayout.deckHeight, 10.0, x: tx, mat: planks, chamfer: 0.03, parent: n)
        // steps up the west end (the ramp underneath is what the walkers use)
        for k in 0..<4 { K.box(0.55, 0.2 * Float(k + 1), 4.4, x: tx - 5.5 - 0.55 * Float(4 - k) + 0.3, mat: planks, chamfer: 0.01, parent: n) }
        for s: Float in [-1, 1] {
            for k in 0..<6 { K.box(0.14, 1.0, 0.14, x: tx - 5.2 + Float(k) * 2.1, y: 0.8, z: s * 4.8, mat: teak, parent: n) }
            K.box(11.0, 0.1, 0.1, x: tx, y: 1.7, z: s * 4.8, mat: teak, parent: n)
        }
        for k in 0..<5 { K.box(0.14, 1.0, 0.14, x: tx + 5.4, y: 0.8, z: -4.2 + Float(k) * 2.1, mat: teak, parent: n) }
        K.box(0.1, 0.1, 10.0, x: tx + 5.4, y: 1.7, mat: teak, parent: n)
        // the board for playing with friends
        let board = SCNNode(); board.position = SCNVector3(tx - 2.7, LobbyLayout.deckHeight, 0)
        for s: Float in [-1.2, 1.2] { K.box(0.14, 2.7, 0.14, z: s, mat: teak, parent: board) }
        let sign = K.sign("PARTY\nBOARD", width: 3.0, height: 1.9, fill: K.navy, ink: K.lime, lines: 2)
        sign.position = SCNVector3(0, 1.75, 0); sign.eulerAngles.y = -.pi / 2 - .pi; board.addChildNode(sign)   // faces west, toward the plaza
        for (i, c) in [K.coral, K.lime, K.white, "7FB6F2"].enumerated() {
            K.box(0.04, 0.45, 0.5, x: -0.08, y: 1.55 + 0.0, z: -1.05 + Float(i) * 0.7, mat: K.mat(c), chamfer: 0.01, parent: board).position.x = -0.1
        }
        n.addChildNode(board)
        // four spots, one for each person in the party
        for p in layout.partySpots {
            let y = LobbyLayout.deckHeight
            K.cylinder(1.0, 0.1, x: p.x, y: y, z: -p.y, mat: navy, segments: 32, parent: n)
            K.cylinder(0.94, 0.02, x: p.x, y: y + 0.1, z: -p.y, mat: K.mat(K.lime, emission: true), segments: 32, parent: n)
            K.cylinder(0.8, 0.025, x: p.x, y: y + 0.105, z: -p.y, mat: K.mat(K.sand), segments: 32, parent: n)
        }
        // lounge, parasol
        let y = LobbyLayout.deckHeight
        K.box(1.0, 0.5, 1.7, x: tx + 4.4, y: y, z: 4.0, mat: teak, parent: n)
        K.box(0.9, 0.2, 1.5, x: tx + 4.4, y: y + 0.5, z: 4.0, mat: navy, parent: n)
        K.cylinder(0.05, 2.6, x: tx + 4.2, y: y, z: -4.0, mat: K.mat(K.white), segments: 8, parent: n)
        K.cone(top: 0.05, bottom: 1.7, 0.55, x: tx + 4.2, y: y + 2.3, z: -4.0, mat: K.mat(K.navy), segments: 16, parent: n)
        K.cone(top: 0.05, bottom: 1.66, 0.54, x: tx + 4.2, y: y + 2.32, z: -4.0, mat: K.mat(K.white), segments: 8, parent: n).eulerAngles.y = 0.2
        // string lights
        for s: Float in [-1, 1] { for k in 0..<11 { K.sphere(0.07, x: tx - 5 + Float(k), y: y + 2.4 - 0.2 * sin(Float(k) / 10 * .pi), z: s * 4.8, mat: K.mat("FFE9A8", emission: true), segments: 8, parent: n) } }
        return n
    }

    // MARK: the pro shop (south-west, closed)
    private func proShop() -> SCNNode {
        let n = SCNNode(); n.name = "shop"
        let r: Float = 20.6, d = Float(0.70710678)
        let ivory = K.textured("ivory", tile: 3), navy = K.mat(K.navy), slate = K.mat("8A91A6"), brass = K.mat(K.gold)
        // the porch: the walkable ground leads right up to the front
        let porch = SCNNode(); porch.position = SCNVector3(-r * d, 0, r * d); porch.eulerAngles.y = -.pi / 4   // the front looks north-east, at the plaza
        K.box(7.6, 0.2, 5.8, x: 0, y: -0.2, z: -5.1, mat: K.textured("pavers", tile: 4), chamfer: 0, parent: porch)
        K.box(7.4, 3.4, 5.2, y: 0, mat: ivory, chamfer: 0.1, parent: porch)
        K.gable(8.4, 6.2, 1.6, y: 3.4, mat: K.mat(K.terracotta), parent: porch)
        let front: Float = -2.62
        for sx: Float in [-2.3, 2.3] {
            K.box(2.2, 1.8, 0.1, x: sx, y: 1.0, z: front - 0.02, mat: K.mat("45506B"), chamfer: 0.02, parent: porch)
            for k in 0..<7 { K.box(2.0, 0.05, 0.05, x: sx, y: 1.15 + Float(k) * 0.24, z: front - 0.1, mat: K.mat("2C3550"), chamfer: 0, parent: porch) }
        }
        K.box(1.3, 2.5, 0.12, y: 0, z: front - 0.02, mat: navy, chamfer: 0.03, parent: porch)
        K.box(0.26, 0.3, 0.08, y: 1.0, z: front - 0.12, mat: brass, chamfer: 0.02, parent: porch)
        let awning = K.box(7.0, 0.14, 1.6, y: 3.1, z: front - 0.8, mat: K.mat("9AA0B4"), chamfer: 0.03, parent: porch); awning.eulerAngles.x = 0.12
        let sign = K.sign("PRO SHOP", width: 4.0, height: 0.8, fill: "7B8196", ink: "EEF0F6"); sign.position = SCNVector3(0, 3.55, front - 0.06); sign.eulerAngles.y = 0; sign.eulerAngles.y = .pi * 0; porch.addChildNode(sign)
        sign.eulerAngles.y = .pi * 0
        // the notice and a rope across the front
        let notice = K.sign("COMING\nSOON", width: 2.2, height: 1.1, fill: "F5ECD2", ink: K.navy, lines: 2)
        notice.position = SCNVector3(2.8, 1.2, front - 2.2); porch.addChildNode(notice)
        for sx: Float in [-3.0, -1.0, 1.0, 3.0] {
            K.cylinder(0.05, 0.9, x: sx, z: front - 1.7, mat: K.mat("1D2144"), segments: 8, parent: porch)
            K.sphere(0.09, x: sx, y: 0.95, z: front - 1.7, mat: brass, parent: porch)
        }
        for sx: Float in [-2.0, 0.0, 2.0] { K.box(2.0, 0.04, 0.04, x: sx, y: 0.78, z: front - 1.7, mat: navy, chamfer: 0, parent: porch) }
        _ = slate
        n.addChildNode(porch)
        return n
    }

    // MARK: greenery
    private func greenery() -> SCNNode {
        let n = SCNNode(); n.name = "greenery"
        let spots: [(Float, Float)] = [(-9, 10.5), (9, 11), (-12.5, 8.5), (12.5, 9.5), (-8, -9.5), (10, -9), (13.5, -10), (-6, -16), (4, -14), (-20, 11), (-22, -10), (22, -10), (24, 11), (-3, 24), (8, 25),
                                       (-14, 22), (16, 20), (-27, 3), (29, -2), (3, -27), (-18, -22), (20, -20), (-10, 16.5), (11, 16), (-6, -12.5), (8, -12.5), (-26, 12), (27, 14), (-2, 31), (12, 29)]
        var rng = K.SeededRandom(seed: 11)
        for (i, s) in spots.enumerated() {
            let p = K.palm(height: Float(rng.next(in: 6.5...9.5)), lean: Float(rng.next(in: 0.5...1.0)), azimuth: Float(rng.next(in: 0...6.28)), seed: 100 + i)
            p.position = SCNVector3(s.0, -0.14, -s.1); n.addChildNode(p)
        }
        for i in 0..<16 {
            let a = Float(rng.next(in: 0...6.28)), r = Float(rng.next(in: 24...46))
            let x = cos(a) * r, y = sin(a) * r
            if abs(x) < 15 && y > 10 && y < 30 { continue }
            K.cylinder(0.26, 4.0, x: x, y: -0.14, z: -y, mat: K.mat("66482C"), segments: 8, parent: n)
            K.sphere(Float(rng.next(in: 2.3...3.4)), x: x, y: 4.4, z: -y, mat: K.textured("hedge", tile: 3), squash: 0.82, segments: 14, parent: n)
            _ = i
        }
        // garden hedges beside the plaza
        for (x, y, w): (Float, Float, Float) in [(-12.3, 6.4, 5), (-12.3, -6.4, 5), (12.3, 6.4, 5), (12.3, -6.4, 5)] {
            K.box(w, 1.1, 1.0, x: x, y: -0.14, z: -y, mat: K.textured("hedge", tile: 2), chamfer: 0.35, parent: n)
        }
        return n
    }

    // MARK: stations
    private func stationMarkers() {
        for s in layout.stations {
            let ring = SCNNode(); ring.name = "ring-" + s.id
            let y = layout.height(at: s.ring)
            ring.position = LobbyScene.scn(s.ring, y + 0.02)
            let color = s.locked ? "9CA3B8" : K.lime
            let outer = SCNCylinder(radius: CGFloat(s.radius), height: 0.03); outer.radialSegmentCount = 40; outer.materials = [K.mat(color, emission: true)]
            ring.addChildNode(SCNNode(geometry: outer))
            let inner = SCNCylinder(radius: CGFloat(s.radius * 0.84), height: 0.034); inner.radialSegmentCount = 40; inner.materials = [K.textured("pavers", tile: s.radius * 1.6)]
            ring.addChildNode(SCNNode(geometry: inner))
            let glow = SCNCylinder(radius: CGFloat(s.radius * 1.12), height: 0.01); glow.radialSegmentCount = 40
            let gm = SCNMaterial(); gm.lightingModel = .constant; gm.diffuse.contents = K.colour(color, alpha: 0.28); gm.blendMode = .add; gm.writesToDepthBuffer = false
            glow.materials = [gm]; ring.addChildNode(SCNNode(geometry: glow))
            scene.rootNode.addChildNode(ring); rings[s.id] = ring
            // the floating name
            let label = SCNNode()
            let face = SCNPlane(width: s.title.count > 9 ? 2.3 : 1.7, height: 0.52)
            face.materials = [LobbyKit.signMaterial(LobbyKit.signImage(s.title.uppercased(), fill: s.locked ? "7B8196" : K.navy, ink: s.locked ? "EEF0F6" : K.white, size: CGSize(width: 480, height: 150), lock: s.locked))]
            label.addChildNode(SCNNode(geometry: face))
            label.position = LobbyScene.scn(s.ring, y + 2.7)
            let bb = SCNBillboardConstraint(); bb.freeAxes = [.Y]; label.constraints = [bb]
            scene.rootNode.addChildNode(label); labels[s.id] = label
        }
    }
    private func flags() {
        _ = pennants
    }
}
