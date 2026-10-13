import SceneKit
import AppKit
import Metal
import simd

// Hero sync: the LOBBY half of the lobby-vs-game sheets. A macOS SceneKit render that compiles the app's own MatchHeroData / MatchHeroSurfaces / MatchHeroMotion
// files and draws the exported hero with the locker's lights and the same cameras as MatchHeroLockerExport.CaptureLobbyParity (the Unity half).
// usage: lobby_render <outDir> [cases]   env: LP_ASSETS (export folder), LP_GOLF=1 (golf heroes), LP_DISTANCE=1 (the tennis lobby's Distance variant),
//        LP_FRAMINGS=body,head,torso,feet

let repo = "/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator"
let assetsDir = ProcessInfo.processInfo.environment["LP_ASSETS"] ?? (repo + "/GolfArcade/Unity/CharacterAssets")

func png(_ image: NSImage, _ path: String) {
    guard let tiff = image.tiffRepresentation, let rep = NSBitmapImageRep(data: tiff), let data = rep.representation(using: .png, properties: [:]) else { return }
    try? data.write(to: URL(fileURLWithPath: path))
}

@MainActor
final class Stage {
    let scene = SCNScene(), character = SCNNode(), cameraNode = SCNNode()
    init(cameraDistance: Float) {
        scene.rootNode.addChildNode(character)
        cameraNode.camera = SCNCamera(); cameraNode.camera?.fieldOfView = 32; cameraNode.camera?.zNear = 0.05
        cameraNode.position = SCNVector3(0, 1.0, CGFloat(cameraDistance)); cameraNode.look(at: SCNVector3(0, 0.92, 0))
        scene.rootNode.addChildNode(cameraNode)
        func light(_ type: SCNLight.LightType, _ intensity: CGFloat, _ color: NSColor, elevation: Float, azimuth: Float, shadow: Bool = false) {
            let node = SCNNode(), l = SCNLight(); l.type = type; l.intensity = intensity; l.color = color
            if shadow {
                l.castsShadow = true; l.shadowRadius = 6; l.shadowSampleCount = 8; l.shadowMode = .deferred
                l.shadowColor = NSColor(red: 0.12, green: 0.14, blue: 0.24, alpha: 0.35); l.orthographicScale = 2.2
            }
            node.light = l
            let e = elevation * .pi / 180, a = azimuth * .pi / 180
            node.position = SCNVector3(CGFloat(-sin(a) * cos(e) * 6), CGFloat(sin(e) * 6), CGFloat(cos(a) * cos(e) * 6))
            node.look(at: SCNVector3(0, 0.8, 0)); scene.rootNode.addChildNode(node)
        }
        // LP_RIG=key / ambient: one term of the rig alone (the same switch as CaptureLobbyParity), for calibration
        let rig = ProcessInfo.processInfo.environment["LP_RIG"] ?? "lobby"
        if rig != "ambient" { light(.directional, 1050, NSColor(red: 1, green: 0.92, blue: 0.82, alpha: 1), elevation: 40, azimuth: 38, shadow: true) }
        if rig == "lobby" {
            light(.directional, 370, NSColor(red: 0.84, green: 0.9, blue: 1, alpha: 1), elevation: 18, azimuth: -55)
            light(.directional, 520, NSColor(red: 1, green: 0.96, blue: 0.9, alpha: 1), elevation: 35, azimuth: 160)
        }
        let amb = SCNNode(); amb.light = SCNLight(); amb.light?.type = .ambient; amb.light?.intensity = rig == "key" ? 0 : 210
        amb.light?.color = NSColor(red: 0.86, green: 0.88, blue: 0.96, alpha: 1); scene.rootNode.addChildNode(amb)
        scene.background.contents = NSColor(red: 0.93, green: 0.62, blue: 0.48, alpha: 1)
    }
    func frame(_ framing: String, cameraDistance: Float, headCentre: Float) {
        let dist = max(2.4, cameraDistance) * 1.05
        switch framing {
        case "head": cameraNode.position = SCNVector3(0, CGFloat(headCentre + 0.04), 1.15); cameraNode.look(at: SCNVector3(0, CGFloat(headCentre), 0))
        case "torso": cameraNode.position = SCNVector3(0, CGFloat(headCentre - 0.34), 2.1); cameraNode.look(at: SCNVector3(0, CGFloat(headCentre - 0.40), 0))
        case "feet": cameraNode.position = SCNVector3(0.15, 0.30 + 0.22, 1.5); cameraNode.look(at: SCNVector3(0, 0.30, 0))
        case "shirt": cameraNode.position = SCNVector3(0, CGFloat(headCentre - 0.40 + 0.06), 1.15); cameraNode.look(at: SCNVector3(0, CGFloat(headCentre - 0.40), 0))
        default: cameraNode.position = SCNVector3(0, 0.9, CGFloat(dist)); cameraNode.look(at: SCNVector3(0, 0.76, 0))
        }
    }
    func snapshot(_ size: CGSize, device: MTLDevice) -> NSImage {
        let r = SCNRenderer(device: device, options: nil); r.scene = scene; r.pointOfView = cameraNode
        return r.snapshot(atTime: 0, with: size, antialiasingMode: .multisampling4X)
    }
}

@MainActor
func run() {
    let args = CommandLine.arguments
    let out = args.count > 1 ? args[1] : "."
    let only = args.count > 2 ? Set(args[2].split(separator: ",").map(String.init)) : nil
    if let e = ProcessInfo.processInfo.environment["LM_EXPOSURE"], let v = Float(e) { MatchHeroSurfaces.sceneExposure = v }
    try? FileManager.default.createDirectory(atPath: out, withIntermediateDirectories: true)
    MatchHeroData.locate = { name, ext in
        let u = URL(fileURLWithPath: assetsDir + "/" + name + "." + ext); return FileManager.default.fileExists(atPath: u.path) ? u : nil
    }
    let device = MTLCreateSystemDefaultDevice()!
    func sRGB(_ hex: String) -> SIMD3<Float> {
        let n = UInt32(hex, radix: 16) ?? 0xFFFFFF
        return SIMD3(Float((n >> 16) & 255) / 255, Float((n >> 8) & 255) / 255, Float(n & 255) / 255)
    }
    func kitTint(_ pick: SIMD3<Float>) -> SIMD3<Float> { SIMD3(max(0.93 * pick.x, 0.035), max(0.93 * pick.y, 0.035), max(0.93 * pick.z, 0.0401)) }
    func kitDerive(_ pick: SIMD3<Float>) -> SIMD3<Float> { let l = 0.2126 * pick.x + 0.7152 * pick.y + 0.0722 * pick.z; return l >= 0.5 ? pick * 0.65 : pick + (SIMD3<Float>(repeating: 1) - pick) * 0.35 }
    let cases: [(id: String, skin: String?, shirt: String?, shorts: String?, shoes: String?)] = [
        ("default", nil, nil, nil, nil), ("s1", "FFE0C4", nil, nil, nil), ("s2", "E6AE7E", "FF6B4A", "1E2A6E", "9EE63A"),
        ("s3", "B7744A", "2F6FD6", "F4F0E6", "FFFFFF"), ("s4", "74432C", "7BC043", "23344D", "E04F3A"), ("s5", "3E2317", "F5C542", "FFFFFF", "2A2A2A")]
    for female in [false, true] {
        let sex = female ? "female" : "male"
        let golf = ProcessInfo.processInfo.environment["LP_GOLF"] == "1"
        guard let asset = MatchHeroData.asset(female: female, golf: golf, distance: !golf && ProcessInfo.processInfo.environment["LP_DISTANCE"] == "1") else { print("no asset"); return }
        let headCentre = asset.manifest.headCentreY * asset.scale
        for c in cases where only == nil || only!.contains(c.id) {
            let picks = MatchHeroData.Picks(colour: { name in
                switch name {
                case "Skin": return c.skin.map(sRGB)
                case "Kit_Shirt": return c.shirt.map { kitTint(sRGB($0)) }
                case "Kit_ShirtTrim": return c.shirt.map { kitTint(kitDerive(sRGB($0))) }
                case "Kit_Shorts": return c.shorts.map { kitTint(sRGB($0)) }
                case "Kit_ShortsBand": return c.shorts.map { kitTint(kitDerive(sRGB($0))) }
                case "Kit_Shoe": return c.shoes.map { kitTint(sRGB($0)) }
                default: return nil
                }
            })
            for framing in (ProcessInfo.processInfo.environment["LP_FRAMINGS"] ?? "body,head,torso,feet").split(separator: ",").map(String.init) {
                let st = Stage(cameraDistance: 3.25); st.frame(framing, cameraDistance: 3.25, headCentre: headCentre)
                let hero = MatchHeroData.buildHero(asset, picks: picks); st.character.addChildNode(hero)
                if let rig = HeroRig(root: hero, asset: asset) { rig.attach() }
                png(st.snapshot(CGSize(width: 720, height: 1000), device: device), "\(out)/\(sex)_\(c.id)_\(framing).png")
            }
        }
    }
}
@main struct LobbyRender { static func main() { MainActor.assumeIsolated { run() } } }
