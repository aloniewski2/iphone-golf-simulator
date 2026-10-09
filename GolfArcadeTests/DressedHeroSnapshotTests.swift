import SwiftUI
import SceneKit
import XCTest
@testable import GolfArcade

/// DRESS_MATCH_HEROES / SWIFT_MIRROR: the locker mirror (CharacterModelPreview) draws the match heroes DRESSED (work/hero-dressed: White tennis kit skinned to the match rig, exported by Unity's
/// MatchHeroLockerExport) and recolours the kit by role from the locker's picks with the same maths as Unity's MatchHeroLook.SetKit.  Proof renders go to work/hero-dressed/proof/swift/.
@MainActor
final class DressedHeroSnapshotTests: XCTestCase {
    private var repo: URL { URL(fileURLWithPath: #filePath).deletingLastPathComponent().deletingLastPathComponent() }
    private var proof: URL {
        let url = repo.appendingPathComponent("work/hero-dressed/proof/swift")
        try? FileManager.default.createDirectory(at: url, withIntermediateDirectories: true)
        return url
    }
    private static let kitParts = ["Kit_Top", "Kit_Bottom", "Kit_Sock_L", "Kit_Sock_R", "Kit_Shoe_L", "Kit_Shoe_R"]
    private static let roles = ["Kit_Shirt", "Kit_ShirtTrim", "Kit_Shorts", "Kit_ShortsBand", "Kit_Shoe", "Kit_Sole", "Kit_Sock"]

    private func player(female: Bool, picks: Bool) -> Player {
        var p = Player(name: female ? "Maya" : "Alex", colorIndex: 0); p.standardFemale = female; p.setSkin(female ? 0.2 : 0.45)
        if picks { p.shirt = 5; p.shorts = 2; p.accent = 3 }      // Outfit.palette: Coral FF6B4A, Navy 1E2A6E, Lime 9EE63A
        return p
    }

    /// Independent restatement of the pre-registered maths (work/hero-dressed/GATE_DEFS_preregistered.md D6), in 0...255 per channel.
    private func expectedTint(_ hex: String, derived: Bool) -> [Double] {
        let n = Int(hex, radix: 16) ?? 0
        var c = [Double((n >> 16) & 255) / 255, Double((n >> 8) & 255) / 255, Double(n & 255) / 255]
        if derived {
            let l = 0.2126 * c[0] + 0.7152 * c[1] + 0.0722 * c[2]
            c = l >= 0.5 ? c.map { $0 * 0.65 } : c.map { $0 + (1 - $0) * 0.35 }
        }
        let r: Double = max(0.9300 * c[0], 0.0350) * 255, g: Double = max(0.9300 * c[1], 0.0350) * 255, b: Double = max(0.9300 * c[2], 0.0401) * 255
        return [r, g, b]
    }
    private func channels(_ any: Any?) -> [Double] {
        guard let color = any as? UIColor else { return [-1, -1, -1] }
        var r: CGFloat = 0, g: CGFloat = 0, b: CGFloat = 0, a: CGFloat = 0; color.getRed(&r, green: &g, blue: &b, alpha: &a)
        return [Double(r) * 255, Double(g) * 255, Double(b) * 255]
    }
    private func v255(_ v: SIMD3<Float>) -> [Double] { [Double(v.x) * 255, Double(v.y) * 255, Double(v.z) * 255] }
    private func assertColour(_ got: [Double], _ want: [Double], _ message: String, file: StaticString = #filePath, line: UInt = #line) {
        for i in 0 ..< 3 { XCTAssertEqual(got[i], want[i], accuracy: 1.0, "\(message): \(got) vs \(want)", file: file, line: line) }
    }
    /// The colour a kit material is rendered with, in 0...255 sRGB: LOCKER_MIRROR draws the kit with the cloth shader, which multiplies the role's tint (a linear colour) into the seam map,
    /// so the colour is read from that tint; a flat material (skin, frame) is read from its diffuse.
    private func renderedColour(_ m: SCNMaterial) -> [Double] {
        if let tint = (m.value(forKey: "clothTint") as? NSValue)?.scnVector4Value {
            return [tint.x, tint.y, tint.z].map { Double(MatchHeroSurfaces.linearToSRGB(Float($0))) * 255 }
        }
        return channels(m.diffuse.contents)
    }
    /// role -> colour of the SceneKit materials of that role on one hero
    private func roleColours(_ hero: SCNNode) -> [String: [Double]] {
        var out: [String: [Double]] = [:]
        hero.enumerateChildNodes { node, _ in
            guard let g = node.geometry else { return }
            for m in g.materials { if let n = m.name, n.hasPrefix("Kit_") { out[n] = renderedColour(m) } }
        }
        return out
    }
    /// The authored kit (LOCKER_MIRROR: what Unity's runtime materials carry): a white upper, the dark trim and band (#09090A), a grey sole.
    private static let authoredWhite = 0.9300 * 255, authoredDark = [0.0350 * 255, 0.0350 * 255, 0.0401 * 255], authoredSole = [0.66 * 255, 0.65 * 255, 0.63 * 255]

    // MARK: the data and the stage

    func testBothHeroesAreDressedInTheMirror() throws {
        for female in [false, true] {
            let asset = try XCTUnwrap(MatchHero.asset(female: female))
            let kit = asset.manifest.parts.filter { $0.kind == "kit" }
            XCTAssertEqual(Set(kit.map(\.name)), Set(Self.kitParts), "the six kit parts ship in the manifest")
            let used = Set(kit.flatMap { $0.submeshes.map(\.material) })
            XCTAssertEqual(used, Set(Self.roles), "the kit parts carry exactly the seven role materials")
            let c = CharacterModelPreview.Coordinator(); c.update(player(female: female, picks: false))
            let hero = try XCTUnwrap(c.hero)
            for name in Self.kitParts { XCTAssertNotNil(hero.childNode(withName: name, recursively: false), "\(name) is on stage") }
            let tris = asset.manifest.parts.reduce(0) { $0 + $1.submeshes.reduce(0) { $0 + $1.indexCount } } / 3
            let kitTris = kit.reduce(0) { $0 + $1.submeshes.reduce(0) { $0 + $1.indexCount } } / 3
            NSLog("[DressedHero] \(female ? "Female" : "Male"): triangles on stage \(tris) (kit \(kitTris)), vertices \(asset.manifest.parts.reduce(0) { $0 + $1.vertexCount })")
        }
    }

    func testDefaultKitIsTheAuthoredWhiteKit() throws {
        for female in [false, true] {
            let c = CharacterModelPreview.Coordinator(); c.update(player(female: female, picks: false))
            let colours = roleColours(try XCTUnwrap(c.hero))
            XCTAssertEqual(Set(colours.keys), Set(Self.roles))
            for role in Self.roles {
                // the white kit keeps its dark piping: trim and band stay the authored dark, never the shirt white; the sole is the grey rubber
                var want: [Double] = [Self.authoredWhite, Self.authoredWhite, Self.authoredWhite]
                if role == "Kit_ShortsBand" || role == "Kit_ShirtTrim" { want = Self.authoredDark }
                if role == "Kit_Sole" { want = Self.authoredSole }
                let got: [Double] = try XCTUnwrap(colours[role])
                assertColour(got, want, "default \(role)")
            }
        }
    }

    // MARK: the maths, shared with Unity

    func testTintMathsMatchesTheUnitySide() {
        // work/hero-dressed/GATE_DEFS_preregistered.md D6: out = max(0.93 * pick, floor); derived = pick * 0.65 (light) or pick + (1 - pick) * 0.35 (dark)
        let vectors: [(hex: String, tint: [Double], derived: [Double])] = [
            ("FF7F50", [237.15, 118.11, 74.40], [154.15, 76.77, 48.36]),     // coral
            ("1E2A6E", [27.90, 39.06, 102.30], [101.14, 108.39, 149.50]),    // navy
            ("7CFC00", [115.32, 234.36, 10.23], [74.96, 152.33, 10.23]),     // lime
            ("FFFFFF", [237.15, 237.15, 237.15], [154.15, 154.15, 154.15]),  // white pick = the authored white
            ("000000", [8.93, 8.93, 10.23], [83.00, 83.00, 83.00]),          // near-black pick is floored at the authored black kit
        ]
        for v in vectors {
            let pick = MatchHero.rgb(v.hex)
            assertColour(v255(MatchHero.kitTint(pick)), v.tint, "kitTint \(v.hex)")
            assertColour(v255(MatchHero.kitTint(MatchHero.kitDerive(pick))), v.derived, "derived trim \(v.hex)")
            assertColour(expectedTint(v.hex, derived: false), v.tint, "test restatement \(v.hex)")
        }
    }

    func testPicksRecolourOnlyTheirRoles() throws {
        for female in [false, true] {
            let baseStage = CharacterModelPreview.Coordinator(); baseStage.update(player(female: female, picks: false))
            let base = roleColours(try XCTUnwrap(baseStage.hero))
            let p = player(female: female, picks: true)
            let c = CharacterModelPreview.Coordinator(); c.update(p)
            let hero = try XCTUnwrap(c.hero)
            let now = roleColours(hero)
            let shirt = try XCTUnwrap(p.outfitHex("shirt")), shorts = try XCTUnwrap(p.outfitHex("shorts")), shoes = try XCTUnwrap(p.outfitHex("accent"))
            assertColour(try XCTUnwrap(now["Kit_Shirt"]), expectedTint(shirt, derived: false), "shirt pick -> Kit_Shirt")
            assertColour(try XCTUnwrap(now["Kit_ShirtTrim"]), expectedTint(shirt, derived: true), "shirt pick -> Kit_ShirtTrim (derived)")
            assertColour(try XCTUnwrap(now["Kit_Shorts"]), expectedTint(shorts, derived: false), "shorts pick -> Kit_Shorts")
            assertColour(try XCTUnwrap(now["Kit_ShortsBand"]), expectedTint(shorts, derived: true), "shorts pick -> Kit_ShortsBand (derived)")
            assertColour(try XCTUnwrap(now["Kit_Shoe"]), expectedTint(shoes, derived: false), "accent pick -> Kit_Shoe")
            for untouched in ["Kit_Sole", "Kit_Sock"] { assertColour(try XCTUnwrap(now[untouched]), try XCTUnwrap(base[untouched]), "\(untouched) is never tinted") }
            // skin and racket frame follow their own picks, not the kit's
            let body = try XCTUnwrap(hero.childNode(withName: "Body", recursively: false)?.geometry)
            for m in body.materials { assertColour(channels(m.diffuse.contents), v255(MatchHero.rgb(p.skinHex)), "skin") }
        }
    }

    // MARK: pixels

    private func pixels(_ image: UIImage, _ size: Int) throws -> [UInt8] {
        let cg = try XCTUnwrap(image.cgImage)
        var data = [UInt8](repeating: 0, count: size * size * 4)
        data.withUnsafeMutableBytes { ptr in
            let ctx = CGContext(data: ptr.baseAddress, width: size, height: size, bitsPerComponent: 8, bytesPerRow: size * 4, space: CGColorSpaceCreateDeviceRGB(), bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)!
            ctx.draw(cg, in: CGRect(x: 0, y: 0, width: size, height: size))
        }
        return data
    }

    private func snapshot(_ c: CharacterModelPreview.Coordinator, size: CGSize) throws -> UIImage {
        let renderer = SCNRenderer(device: nil, options: nil); renderer.scene = c.scene; renderer.pointOfView = c.camera
        return renderer.snapshot(atTime: 0, with: size, antialiasingMode: .multisampling4X)
    }

    /// The scene with every kit part painted flat white and everything else flat black (constant lighting): where the kit is on screen.
    private func kitMask(_ c: CharacterModelPreview.Coordinator, size: CGSize) throws -> UIImage {
        let hero = try XCTUnwrap(c.hero)
        hero.enumerateChildNodes { node, _ in
            guard let g = node.geometry else { return }
            let isKit = node.name?.hasPrefix("Kit_") == true
            let flat = SCNMaterial(); flat.lightingModel = .constant; flat.diffuse.contents = isKit ? UIColor.white : UIColor.black; flat.isDoubleSided = true
            g.materials = g.materials.map { _ in flat }
        }
        let saved = c.scene.background.contents; c.scene.background.contents = UIColor.black
        defer { c.scene.background.contents = saved }
        return try snapshot(c, size: size)
    }

    private func stage(_ p: Player, size: CGSize = CGSize(width: 720, height: 1000), swing: Double? = nil) throws -> CharacterModelPreview.Coordinator {
        let c = CharacterModelPreview.Coordinator(cameraDistance: 3.3); c.update(p)
        c.scene.background.contents = UIColor(red: 0.93, green: 0.62, blue: 0.48, alpha: 1)   // the locker's warm coral wall
        if let t = swing {
            let hero = try XCTUnwrap(c.hero); MatchHero.preparePractice(hero)
            let asset = try XCTUnwrap(MatchHero.asset(female: p.standardFemale))
            MatchHero.practiceFrame(hero, progress: 0.1 + 0.75 * t / Double(asset.manifest.swingLength))
        }
        return c
    }

    /// Proof renders for both sexes, default and picks, in the Ready stance and at the Forehand contact frame; and the claim "the picks change only the kit" as pixels.
    func testRecordDressedSnapshots() throws {
        let size = CGSize(width: 720, height: 1000)
        for female in [false, true] {
            let sex = female ? "female" : "male"
            for (label, swing) in [("ready", nil), ("contact", 0.6667)] as [(String, Double?)] {
                let a = try stage(player(female: female, picks: false), swing: swing), b = try stage(player(female: female, picks: true), swing: swing)
                let imgA = try snapshot(a, size: size), imgB = try snapshot(b, size: size)
                try XCTUnwrap(imgA.pngData()).write(to: proof.appendingPathComponent("\(sex)_\(label)_default.png"))
                try XCTUnwrap(imgB.pngData()).write(to: proof.appendingPathComponent("\(sex)_\(label)_coral_navy_lime.png"))
                // pixel test: everything that changed lies on the kit (2 px dilation)
                let m = try stage(player(female: female, picks: true), swing: swing)
                let mask = try kitMask(m, size: size)
                let w = Int(size.width), h = Int(size.height)
                func px(_ im: UIImage) throws -> [UInt8] {
                    let cg = try XCTUnwrap(im.cgImage); var d = [UInt8](repeating: 0, count: cg.width * cg.height * 4)
                    d.withUnsafeMutableBytes { ptr in
                        let ctx = CGContext(data: ptr.baseAddress, width: cg.width, height: cg.height, bitsPerComponent: 8, bytesPerRow: cg.width * 4, space: CGColorSpaceCreateDeviceRGB(), bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)!
                        ctx.draw(cg, in: CGRect(x: 0, y: 0, width: cg.width, height: cg.height))
                    }
                    return d
                }
                let pa = try px(imgA), pb = try px(imgB), pm = try px(mask)
                let cw = try XCTUnwrap(imgA.cgImage).width, ch = try XCTUnwrap(imgA.cgImage).height
                XCTAssertEqual(cw * ch * 4, pa.count); _ = (w, h)
                var changed = 0, outside = 0, kitPixels = 0
                for y in 0 ..< ch { for x in 0 ..< cw {
                    let i = (y * cw + x) * 4
                    if pm[i] > 128 { kitPixels += 1 }
                    let diff = abs(Int(pa[i]) - Int(pb[i])) + abs(Int(pa[i + 1]) - Int(pb[i + 1])) + abs(Int(pa[i + 2]) - Int(pb[i + 2]))
                    guard diff > 24 else { continue }
                    changed += 1
                    var near = false
                    search: for dy in -2 ... 2 { for dx in -2 ... 2 {
                        let xx = x + dx, yy = y + dy
                        if xx >= 0, yy >= 0, xx < cw, yy < ch, pm[(yy * cw + xx) * 4] > 128 { near = true; break search }
                    } }
                    if !near { outside += 1 }
                } }
                NSLog("[DressedHero] \(sex) \(label): kit pixels \(kitPixels), pixels changed by the picks \(changed), outside the kit mask \(outside)")
                XCTAssertGreaterThan(changed, 2000, "the picks visibly recolour the kit (\(sex) \(label))")
                // A handful of anti-aliased edge pixels can differ between two MSAA renders; the kit mask itself must contain the recolour.
                XCTAssertLessThanOrEqual(outside, 12, "the picks change nothing but the kit (\(sex) \(label))")
            }
        }
    }

    /// The real SwiftUI view in a window (what the phone draws), read back from its SCNView: default and picks, both sexes.
    func testRecordHostedDressedView() async throws {
        let scene = try XCTUnwrap(UIApplication.shared.connectedScenes.compactMap { $0 as? UIWindowScene }.first)
        for female in [false, true] {
            for picks in [false, true] {
                let p = player(female: female, picks: picks)
                let host = UIHostingController(rootView: CharacterModelPreview(player: p, cameraDistance: 3.3).background(Color(red: 0.93, green: 0.62, blue: 0.48)))
                let window = UIWindow(windowScene: scene); window.frame = CGRect(x: 0, y: 0, width: 402, height: 560)
                window.rootViewController = host; window.isHidden = false; host.view.frame = window.bounds; host.view.layoutIfNeeded()
                try await Task.sleep(for: .seconds(1.2))
                func findScene(_ view: UIView) -> SCNView? { if let v = view as? SCNView { return v }; return view.subviews.compactMap(findScene).first }
                let scn = try XCTUnwrap(findScene(host.view), "CharacterModelPreview is an SCNView")
                let shown = try XCTUnwrap(scn.scene?.rootNode.childNode(withName: MatchHero.rootName, recursively: true), "the real view shows the match hero")
                for name in Self.kitParts { XCTAssertNotNil(shown.childNode(withName: name, recursively: false), "the real view draws \(name)") }
                let colours = roleColours(shown)
                assertColour(try XCTUnwrap(colours["Kit_Shirt"]), picks ? expectedTint(try XCTUnwrap(p.outfitHex("shirt")), derived: false) : [237.15, 237.15, 237.15], "hosted view shirt (\(picks ? "picked" : "default"))")
                try XCTUnwrap(scn.snapshot().pngData()).write(to: proof.appendingPathComponent("hosted_\(female ? "female" : "male")_\(picks ? "coral_navy_lime" : "default").png"))
                window.isHidden = true
            }
        }
    }
}
