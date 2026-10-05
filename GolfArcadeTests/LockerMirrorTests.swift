import SwiftUI
import SceneKit
import AVFoundation
import XCTest
import simd
@testable import GolfArcade

/// LOCKER_MIRROR: the Swift locker mirror draws the match heroes the way Unity's runtime materials do (cloth weave + thread map + seams on the kit, the soft skin normal, smoothness per surface)
/// and moves them by playing Unity's own clips (<Sex>_ReadyIdle on the locker tile and the tennis idle, one <Sex>_Serve on the play tile) on the hero's skeleton, never by turning the character root.
/// The gate lines are in work/locker-mirror/GATE_DEFS_preregistered.md; the proof stills and films are written to work/locker-mirror/proof/.
@MainActor
final class LockerMirrorTests: XCTestCase {
    private var repo: URL { URL(fileURLWithPath: #filePath).deletingLastPathComponent().deletingLastPathComponent() }
    private var proof: URL {
        let url = repo.appendingPathComponent("work/locker-mirror/proof")
        try? FileManager.default.createDirectory(at: url, withIntermediateDirectories: true)
        return url
    }
    private static let kitParts = ["Kit_Top", "Kit_Bottom", "Kit_Sock_L", "Kit_Sock_R", "Kit_Shoe_L", "Kit_Shoe_R"]

    private func player(female: Bool, picks: Bool = false) -> Player {
        var p = Player(name: female ? "Maya" : "Alex", colorIndex: 0); p.standardFemale = female; p.setSkin(female ? 0.2 : 0.45)
        if picks { p.shirt = 5; p.shorts = 2; p.accent = 3 }      // Outfit.palette: Coral FF6B4A, Navy 1E2A6E, Lime 9EE63A
        return p
    }

    /// Every material on the hero by exported name (the kit's left and right pieces each have their own, so a name maps to several).
    private func materials(_ hero: SCNNode) -> [String: [SCNMaterial]] {
        var out: [String: [SCNMaterial]] = [:]
        hero.enumerateChildNodes { node, _ in for m in node.geometry?.materials ?? [] { if let n = m.name { out[n, default: []].append(m) } } }
        return out
    }
    private func look(_ m: SCNMaterial) throws -> MatchHero.Look { try XCTUnwrap(MatchHeroSurfaces.made(m)?.look, "\(m.name ?? "?") was built from an exported look") }
    private func vec4(_ m: SCNMaterial, _ key: String) -> SCNVector4? { (m.value(forKey: key) as? NSValue)?.scnVector4Value }
    private func luminance(_ c: SIMD3<Float>) -> Float { 0.2126 * c.x + 0.7152 * c.y + 0.0722 * c.z }
    private func tint(_ m: SCNMaterial) throws -> SIMD3<Float> {
        let v = try XCTUnwrap(vec4(m, "clothTint"), "\(m.name ?? "?") is cloth")
        return SIMD3(MatchHeroSurfaces.linearToSRGB(Float(v.x)), MatchHeroSurfaces.linearToSRGB(Float(v.y)), MatchHeroSurfaces.linearToSRGB(Float(v.z)))
    }
    /// A material property's contents as a CGImage (a plain `as? CGImage` always "succeeds" on Any, so the CF type id decides).
    private func cgImage(_ any: Any?) -> CGImage? {
        guard let any, CFGetTypeID(any as AnyObject) == CGImage.typeID else { return nil }
        return (any as! CGImage)
    }
    private func hasClothShader(_ m: SCNMaterial) -> Bool { m.shaderModifiers?[.lightingModel] != nil && m.shaderModifiers?[.surface]?.contains("weaveMap") == true }

    // ================================================================== gate line 1: the kit samples the weave and the thread map

    func testKitMaterialsSampleTheWeaveTheThreadMapAndTheSeamMap() throws {
        for female in [false, true] {
            let c = CharacterModelPreview.Coordinator(); c.update(player(female: female))
            let all = materials(try XCTUnwrap(c.hero))
            for role in ["Kit_Shirt", "Kit_Shorts", "Kit_Sock", "Kit_Shoe"] {
                let list = try XCTUnwrap(all[role], "\(role) is on the hero")
                for m in list {
                    let l = try look(m)
                    XCTAssertTrue(l.isCloth, "\(role) is TennisCloth (\(female ? "female" : "male"))")
                    XCTAssertTrue(hasClothShader(m), "\(role) runs the cloth surface shader")
                    XCTAssertFalse(l.weaveMap.isEmpty, "\(role) names the weave map")
                    XCTAssertGreaterThan(l.weaveTile, 1, "\(role) has a weave tile")
                    // the weave map is bound as a LINEAR image (the sampler returns its normal and thread numbers raw), and read by the shader
                    let weave = try XCTUnwrap(cgImage((m.value(forKey: "weaveMap") as? SCNMaterialProperty)?.contents), "\(role) binds the weave map")
                    XCTAssertEqual(weave.colorSpace?.name as String?, CGColorSpace.linearSRGB as String, "the weave map is tagged linear")
                    let params = try XCTUnwrap(vec4(m, "weaveParams"))
                    XCTAssertGreaterThan(params.z, 0, "\(role) tilts the light with the weave normal")
                    XCTAssertGreaterThan(params.w, 0, "\(role) multiplies the thread break into the colour")
                    XCTAssertEqual(Float(params.x), l.weaveTile, accuracy: 1e-3, "the tile on the material is the exported one")
                    // the seam / collar / hem map is the diffuse (multiplied into the colour), on the kit's UVs
                    XCTAssertNotNil(cgImage(m.diffuse.contents), "\(role) binds its seam map")
                    XCTAssertFalse(l.baseMap.isEmpty)
                }
            }
        }
    }

    /// "Export UVs for kit parts only": the six kit pieces carry UV0 (and in the scene one texcoord channel); the body has none (its two texcoord channels carry the bind-pose position), the face and the racket
    /// have nothing at all.
    func testOnlyTheKitHasUVsInTheExport() throws {
        for female in [false, true] {
            let asset = try XCTUnwrap(MatchHero.asset(female: female))
            for (i, part) in asset.manifest.parts.enumerated() {
                let channels = asset.geometry[i].sources(for: .texcoord).count
                switch part.kind {
                case "kit":
                    XCTAssertGreaterThanOrEqual(part.uvOffset ?? -1, 0, "\(part.name) has UVs"); XCTAssertEqual(channels, 1, "\(part.name): the kit's UV0")
                    XCTAssertTrue((part.bindPositionOffset ?? -1) < 0)
                case "body":
                    XCTAssertTrue((part.uvOffset ?? -1) < 0, "the body has no UVs"); XCTAssertGreaterThanOrEqual(part.bindPositionOffset ?? -1, 0, "the body carries its bind pose"); XCTAssertEqual(channels, 2)
                default:
                    XCTAssertTrue((part.uvOffset ?? -1) < 0 && (part.bindPositionOffset ?? -1) < 0, "\(part.name) (\(part.kind)) has no UVs"); XCTAssertEqual(channels, 0)
                }
            }
        }
    }

    /// "Tiled the same way TennisCloth does": tiles per UV unit = metres per UV unit of the submesh / the role's tile size in metres (MatchHeroLook.ClothFor), the metres per UV unit being
    /// sqrt(3D area / UV area) of the submesh in the bind pose. Recomputed here from the shipped rig and UVs, independently of the exporter.
    func testWeaveTileIsMetresPerUvOverTheRolesTileSize() throws {
        let tileMetres: [String: Float] = ["Kit_Shirt": 0.026, "Kit_ShirtTrim": 0.022, "Kit_Shorts": 0.029, "Kit_ShortsBand": 0.022, "Kit_Shoe": 0.024, "Kit_Sole": 0.020, "Kit_Sock": 0.020]
        for female in [false, true] {
            let asset = try XCTUnwrap(MatchHero.asset(female: female)), rig = try XCTUnwrap(asset.rig())
            let bin = try XCTUnwrap(MatchHeroData.data(female ? "MatchHero_Female" : "MatchHero_Male"))
            for skinned in rig.skinned where skinned.name.hasPrefix("Kit_") {
                let part = asset.manifest.parts[skinned.partIndex]
                let pos = try floats(skinned.geometry.sources(for: .vertex)[0].data)
                let uv = try floats(try XCTUnwrap(skinned.geometry.sources(for: .texcoord).first).data)
                XCTAssertEqual(uv.count, part.vertexCount * 2)
                for sub in part.submeshes {
                    guard let tile = tileMetres[sub.material] else { XCTFail("unknown kit role \(sub.material)"); continue }
                    XCTAssertEqual(try XCTUnwrap(sub.look?.tileMetres), tile, accuracy: 1e-6, "\(sub.material): the exported tile size is MatchHeroLook.ClothFor's")
                    var a3 = 0.0, a2 = 0.0
                    let idx = (0 ..< sub.indexCount).map { k in bin.withUnsafeBytes { $0.loadUnaligned(fromByteOffset: sub.indexOffset + k * 4, as: Int32.self) } }
                    // (the file's winding is flipped with the z mirror, which does not change areas)
                    var t = 0
                    while t + 2 < idx.count {
                        let i0 = Int(idx[t]), i1 = Int(idx[t + 1]), i2 = Int(idx[t + 2]); t += 3
                        let p0 = SIMD3(pos[i0 * 3], pos[i0 * 3 + 1], pos[i0 * 3 + 2]), p1 = SIMD3(pos[i1 * 3], pos[i1 * 3 + 1], pos[i1 * 3 + 2]), p2 = SIMD3(pos[i2 * 3], pos[i2 * 3 + 1], pos[i2 * 3 + 2])
                        a3 += Double(simd_length(simd_cross(p1 - p0, p2 - p0)) * 0.5)
                        let e1 = SIMD2(uv[i1 * 2] - uv[i0 * 2], uv[i1 * 2 + 1] - uv[i0 * 2 + 1]), e2 = SIMD2(uv[i2 * 2] - uv[i0 * 2], uv[i2 * 2 + 1] - uv[i0 * 2 + 1])
                        a2 += Double(abs(e1.x * e2.y - e1.y * e2.x) * 0.5)
                    }
                    let want = Float((a3 / a2).squareRoot()) / tile
                    let got = try XCTUnwrap(sub.look).weaveTile
                    XCTAssertEqual(got, want, accuracy: want * 0.01, "\(female ? "Female" : "Male") \(skinned.name) \(sub.material): tiles per UV unit")
                }
            }
        }
        // the male shirt's UV islands are turned ~41 degrees to up: its weave is turned by the same angle; the female shirt and the other pieces are axis aligned
        let male = try XCTUnwrap(MatchHero.asset(female: false)), female = try XCTUnwrap(MatchHero.asset(female: true))
        let shirt = { (a: MatchHero.Asset) in a.manifest.parts.first { $0.name == "Kit_Top" }!.submeshes.first { $0.material == "Kit_Shirt" }!.look! }
        XCTAssertEqual(shirt(male).weaveAngle, 41, accuracy: 0.01)
        XCTAssertEqual(shirt(female).weaveAngle, 0, accuracy: 0.01)
    }

    /// Seams stay darker: the baked seam map has its darkening on the hems, the collar and the folds of the OUTER wall. Sampled at the kit's UVs the way Unity samples it (v up), the hem and collar
    /// vertices are darker than the rest; with the rows the other way round they are not. A flipped map would put the darkening in the wrong places.
    func testSeamMapOrientationPutsTheDarkeningOnTheHemsAndCollar() throws {
        for female in [false, true] {
            let asset = try XCTUnwrap(MatchHero.asset(female: female))
            let bin = try XCTUnwrap(MatchHeroData.data(female ? "MatchHero_Female" : "MatchHero_Male"))
            // (the shirts, and the male shorts: the female skort is two layers, where a hem / waist split says nothing about the rows)
            for (partName, tag) in female ? [("Kit_Top", "Top")] : [("Kit_Top", "Top"), ("Kit_Bottom", "Bottom")] {
                let part = try XCTUnwrap(asset.manifest.parts.first { $0.name == partName })
                let map = try XCTUnwrap(MatchHeroSurfaces.image("MatchHero_Kit_\(female ? "Female" : "Male")_\(tag)", linear: false, maxPixels: 1024))
                let w = map.width, h = map.height
                var px = [UInt8](repeating: 0, count: w * h * 4)
                let ctx = CGContext(data: &px, width: w, height: h, bitsPerComponent: 8, bytesPerRow: w * 4, space: CGColorSpace(name: CGColorSpace.sRGB)!, bitmapInfo: CGImageAlphaInfo.noneSkipLast.rawValue)!
                ctx.draw(map, in: CGRect(x: 0, y: 0, width: w, height: h))
                let v = part.vertexCount
                let pos = bin.withUnsafeBytes { raw in (0 ..< v).map { raw.loadUnaligned(fromByteOffset: part.positionOffset + $0 * 12 + 4, as: Float.self) } }   // y
                let uvOffset = try XCTUnwrap(part.uvOffset)
                let uvs = bin.withUnsafeBytes { raw in (0 ..< v).map { SIMD2(raw.loadUnaligned(fromByteOffset: uvOffset + $0 * 8, as: Float.self), raw.loadUnaligned(fromByteOffset: uvOffset + $0 * 8 + 4, as: Float.self)) } }
                // the image rows are stored upside down at load (Unity's v = 0 is the bottom row): row = v * h on the stored image is what the sampler reads
                func sample(_ uv: SIMD2<Float>, flipped: Bool) -> Float {
                    let x = min(w - 1, max(0, Int(uv.x * Float(w)))), row = min(h - 1, max(0, Int(uv.y * Float(h))))
                    return Float(px[((flipped ? h - 1 - row : row) * w + x) * 4]) / 255
                }
                let sorted = pos.sorted()
                let lo = sorted[v * 4 / 100], hi = sorted[v * 96 / 100]
                let edge = (0 ..< v).filter { pos[$0] <= lo || pos[$0] >= hi }, rest = (0 ..< v).filter { pos[$0] > lo && pos[$0] < hi }
                func darkening(flipped: Bool) -> Float {
                    let values = uvs.map { sample($0, flipped: flipped) }
                    return rest.map { values[$0] }.reduce(0, +) / Float(rest.count) - edge.map { values[$0] }.reduce(0, +) / Float(edge.count)
                }
                let right = darkening(flipped: false), wrong = darkening(flipped: true)
                XCTAssertGreaterThan(right, 0.004, "\(female ? "Female" : "Male") \(partName): the hem and collar are darker than the rest (by \(right))")
                XCTAssertGreaterThan(right, wrong + 0.004, "\(female ? "Female" : "Male") \(partName): and darker than they would be with the rows the other way round (\(wrong))")
            }
        }
    }

    /// Default trim: shirt trim and the shorts band stay the authored dark (#09090A), not the shirt white; a picked shirt colour still derives its trim with kitDerive; the shorts band the same.
    func testDefaultTrimIsTheAuthoredDarkAndAPickedShirtStillDerivesItsTrim() throws {
        for female in [false, true] {
            let c = CharacterModelPreview.Coordinator(); c.update(player(female: female))
            let all = materials(try XCTUnwrap(c.hero))
            for role in ["Kit_ShirtTrim", "Kit_ShortsBand"] {
                let t = try tint(try XCTUnwrap(all[role]?.first))
                XCTAssertEqual(t.x * 255, 9, accuracy: 1, role); XCTAssertEqual(t.y * 255, 9, accuracy: 1, role); XCTAssertEqual(t.z * 255, 10, accuracy: 1, role)   // #09090A
                let white = try tint(try XCTUnwrap(all[role == "Kit_ShirtTrim" ? "Kit_Shirt" : "Kit_Shorts"]?.first))
                XCTAssertLessThan(luminance(t), 0.06); XCTAssertGreaterThan(luminance(white), 0.8, "the trim is not the garment's white")
            }
            // a pick: the garment takes kitTint(pick), the trim kitTint(kitDerive(pick))
            let p = player(female: female, picks: true)
            let d = CharacterModelPreview.Coordinator(); d.update(p)
            let picked = materials(try XCTUnwrap(d.hero))
            let shirt = MatchHero.rgb(try XCTUnwrap(p.outfitHex("shirt")))
            for (role, want) in [("Kit_Shirt", MatchHero.kitTint(shirt)), ("Kit_ShirtTrim", MatchHero.kitTint(MatchHero.kitDerive(shirt)))] {
                let got = try tint(try XCTUnwrap(picked[role]?.first))
                XCTAssertEqual(got.x, want.x, accuracy: 0.005, role); XCTAssertEqual(got.y, want.y, accuracy: 0.005, role); XCTAssertEqual(got.z, want.z, accuracy: 0.005, role)
            }
        }
    }

    /// Sole: darker and rougher than the upper, and the shoe pick does not recolour it.
    func testSoleIsDarkerAndRougherThanTheUpperAndIsNotRecoloured() throws {
        for female in [false, true] {
            let c = CharacterModelPreview.Coordinator(); c.update(player(female: female))
            let all = materials(try XCTUnwrap(c.hero))
            let upper = try XCTUnwrap(all["Kit_Shoe"]?.first), sole = try XCTUnwrap(all["Kit_Sole"]?.first)
            XCTAssertLessThan(luminance(try tint(sole)), luminance(try tint(upper)) - 0.15, "the sole is darker than the upper")
            XCTAssertLessThan(try look(sole).smoothness, try look(upper).smoothness, "the sole is rougher than the upper")
            XCTAssertEqual(try look(sole).sheenStrength, 0, "rubber has no cloth sheen")
            XCTAssertLessThan(try look(sole).weaveNormal, try look(upper).weaveNormal)
            let d = CharacterModelPreview.Coordinator(); d.update(player(female: female, picks: true))
            let picked = materials(try XCTUnwrap(d.hero))
            XCTAssertNotEqual(try tint(try XCTUnwrap(picked["Kit_Shoe"]?.first)).x, try tint(upper).x, "the shoe pick recolours the upper")
            XCTAssertEqual(try tint(try XCTUnwrap(picked["Kit_Sole"]?.first)), try tint(sole), "the shoe pick leaves the sole alone")
        }
    }

    /// Cloth has a soft grazing sheen and a faint BROAD gloss, never a small hard highlight: the gloss exponent is 28 * smoothness + 2 (<= 9 for every cloth role), its peak 0.35 * smoothness.
    func testClothHasASheenAndNoHighlightDot() throws {
        let c = CharacterModelPreview.Coordinator(); c.update(player(female: false))
        for m in materials(try XCTUnwrap(c.hero)).values.joined() where hasClothShader(m) {
            let l = try look(m)
            XCTAssertLessThanOrEqual(l.smoothness * 28 + 2, 9.01, "\(m.name ?? "?"): a broad lobe, not a dot")
            XCTAssertLessThanOrEqual(l.smoothness, 0.25)
            let cloth = try XCTUnwrap(vec4(m, "clothLook"))
            XCTAssertEqual(Float(cloth.w), l.sheenPower, accuracy: 1e-4)
            if m.name == "Kit_Shirt" { XCTAssertGreaterThan(l.sheenStrength, 0.3, "the shirt has a grazing sheen") }
        }
        XCTAssertTrue(MatchHeroSurfaces.clothLighting.contains("pow(1.0 - ndv"), "the sheen grows toward the silhouette")
    }

    // ---------------------------------------------------------------- pixels: the shirt is not one flat white

    /// Luminance (0...1) of every pixel, the mask of the pixels that belong to the part painted white in `maskImage` (eroded by `erode` px), and the image size.
    private func lumaAndMask(_ image: UIImage, mask maskImage: UIImage, erode: Int = 4) throws -> (luma: [Float], mask: [Bool], w: Int, h: Int) {
        let a = try rgba(image), m = try rgba(maskImage)
        var luma = [Float](repeating: 0, count: a.w * a.h), inside = [Bool](repeating: false, count: a.w * a.h)
        for i in 0 ..< a.w * a.h { luma[i] = (0.2126 * Float(a.px[i * 4]) + 0.7152 * Float(a.px[i * 4 + 1]) + 0.0722 * Float(a.px[i * 4 + 2])) / 255; inside[i] = m.px[i * 4] > 200 }
        var eroded = inside
        for y in erode ..< a.h - erode { for x in erode ..< a.w - erode where inside[y * a.w + x] {
            outer: for dy in stride(from: -erode, through: erode, by: erode) { for dx in stride(from: -erode, through: erode, by: erode) where !inside[(y + dy) * a.w + x + dx] { eroded[y * a.w + x] = false; break outer } }
        } }
        return (luma, eroded, a.w, a.h)
    }
    /// Mean |luminance - its 7x7 box blur| over the mask: the fine detail on the garment (threads, seams, trim, fold edges), and the mean luminance.
    private func detail(_ d: (luma: [Float], mask: [Bool], w: Int, h: Int)) -> (detail: Float, mean: Float, count: Int) {
        var sum: Float = 0, lum: Float = 0, n = 0
        for y in 3 ..< d.h - 3 { for x in 3 ..< d.w - 3 where d.mask[y * d.w + x] {
            var blur: Float = 0
            for dy in -3 ... 3 { for dx in -3 ... 3 { blur += d.luma[(y + dy) * d.w + x + dx] } }
            sum += abs(d.luma[y * d.w + x] - blur / 49); lum += d.luma[y * d.w + x]; n += 1
        } }
        return (n > 0 ? sum / Float(n) : 0, n > 0 ? lum / Float(n) : 0, n)
    }
    /// The scene with `part` painted flat white and everything else flat black.
    private func partMask(_ c: CharacterModelPreview.Coordinator, part: (String) -> Bool, size: CGSize) throws -> UIImage {
        let hero = try XCTUnwrap(c.hero)
        var originals: [(SCNNode, [SCNMaterial])] = []
        hero.enumerateChildNodes { node, _ in
            guard let g = node.geometry else { return }
            originals.append((node, g.materials))
            let flat = SCNMaterial(); flat.lightingModel = .constant; flat.diffuse.contents = part(node.name ?? "") ? UIColor.white : UIColor.black; flat.isDoubleSided = true
            g.materials = g.materials.map { _ in flat }
        }
        let saved = c.scene.background.contents; c.scene.background.contents = UIColor.black
        defer { c.scene.background.contents = saved; for (node, mats) in originals { node.geometry?.materials = mats } }
        return snapshot(c, size: size)
    }
    private func clothMaterials(_ c: CharacterModelPreview.Coordinator, roles: Set<String>) throws -> [SCNMaterial] {
        materials(try XCTUnwrap(c.hero)).filter { roles.contains($0.key) }.flatMap(\.value).filter { hasClothShader($0) }
    }

    /// FAIL if the shirt is still one flat white: at the locker's torso framing the shirt has fine detail the flat look does not (threads, seams, trim), switching the weave / thread / seam map off
    /// removes it, and each of the three changes the picture on its own.
    func testShirtIsNotOneFlatWhite() throws {
        let size = CGSize(width: 900, height: 900)
        for female in [false, true] {
            let tag = female ? "female" : "male"
            func scene() -> CharacterModelPreview.Coordinator {
                let c = CharacterModelPreview.Coordinator(cameraDistance: 3.3); c.update(player(female: female))
                c.scene.background.contents = UIColor(red: 0.93, green: 0.62, blue: 0.48, alpha: 1)
                guard let cam = c.camera else { return c }
                cam.position = SCNVector3(0.35, c.headTarget.y - 0.36, 1.35); cam.look(at: SCNVector3(0, c.headTarget.y - 0.40, 0))   // the shirt close-up
                return c
            }
            let full = scene(), flat = scene()
            let mask = try partMask(full, part: { $0 == "Kit_Top" }, size: size)
            let lookFull = try lumaAndMask(snapshot(full, size: size), mask: mask)
            // the look before this job: flat Blinn colours (white upper, white trim)
            for m in materials(try XCTUnwrap(flat.hero)).values.joined() where m.name?.hasPrefix("Kit_") == true {
                m.shaderModifiers = nil; m.lightingModel = .blinn; m.diffuse.contents = UIColor(white: 0.93, alpha: 1)
                m.specular.contents = UIColor(white: 0.04, alpha: 1); m.shininess = 0.21
            }
            let lookFlat = try lumaAndMask(snapshot(flat, size: size), mask: mask)
            let dFull = detail(lookFull), dFlat = detail(lookFlat)
            NSLog("[LockerMirror] \(tag) shirt close-up: \(dFull.count) shirt px, detail \(dFull.detail) (flat look \(dFlat.detail)), mean luma \(dFull.mean) (flat look \(dFlat.mean))")
            XCTAssertGreaterThan(dFull.count, 20000, "the shirt fills the frame")
            XCTAssertGreaterThan(dFull.detail, dFlat.detail * 2.0, "\(tag): the shirt has fine detail the flat white did not (\(dFull.detail) vs \(dFlat.detail))")
            XCTAssertGreaterThan(dFull.mean, 0.6, "and it still reads as a white shirt")
            // each ingredient on its own changes the picture: weave normal, thread break, seam map
            func variant(_ tweak: (SCNMaterial) -> Void) throws -> (mean: Float, p99: Float) {
                let c = scene()
                for m in try clothMaterials(c, roles: ["Kit_Shirt"]) { tweak(m) }
                let l = try lumaAndMask(snapshot(c, size: size), mask: mask)
                var diffs: [Float] = []
                for i in 0 ..< l.luma.count where lookFull.mask[i] && l.mask[i] { diffs.append(abs(l.luma[i] - lookFull.luma[i])) }
                guard !diffs.isEmpty else { return (0, 0) }
                return (diffs.reduce(0, +) / Float(diffs.count), diffs.sorted()[diffs.count * 99 / 100])
            }
            let noNormal = try variant { m in if let v = self.vec4(m, "weaveParams") { m.setValue(SCNVector4(v.x, v.y, 0, v.w), forKey: "weaveParams") } }
            let noThread = try variant { m in if let v = self.vec4(m, "weaveParams") { m.setValue(SCNVector4(v.x, v.y, v.z, 0), forKey: "weaveParams") } }
            let noSeams = try variant { m in m.diffuse.contents = UIColor.white }
            NSLog("[LockerMirror] \(tag) shirt: luma change (mean / 99th pct) with the weave normal off \(noNormal), thread break off \(noThread), seam map off \(noSeams)")
            XCTAssertGreaterThan(noNormal.mean, 0.01, "\(tag): the weave normal changes the shirt"); XCTAssertGreaterThan(noThread.mean, 0.01, "\(tag): the thread break changes the shirt")
            XCTAssertGreaterThan(noSeams.mean, 0.001, "\(tag): the seam map changes the shirt"); XCTAssertGreaterThan(noSeams.p99, 0.02, "\(tag): the seam map darkens its seams and folds")
        }
    }

    // ================================================================== gate line 2: skin, eyes, face, racket

    func testSkinIsAboutSmoothness02WithAWeakNormalAndNoWeaveOnSkinOrEyes() throws {
        for female in [false, true] {
            let c = CharacterModelPreview.Coordinator(); c.update(player(female: female))
            let hero = try XCTUnwrap(c.hero), all = materials(hero)
            let skin = try XCTUnwrap(all["Skin"]?.first), sl = try look(skin)
            XCTAssertEqual(sl.smoothness, 0.2, accuracy: 0.03, "skin smoothness is about 0.2, not 0.35")
            XCTAssertLessThanOrEqual(sl.bumpScale, 0.25, "the skin normal is weak")
            XCTAssertGreaterThan(sl.bumpScale, 0.05, "and it is there")
            XCTAssertEqual(sl.bumpTriplanar, 1, "projected triplanar from the bind pose (the body has no UVs)")
            XCTAssertNotNil(cgImage((skin.value(forKey: "skinBumpMap") as? SCNMaterialProperty)?.contents), "the soft normal map is bound")
            XCTAssertTrue(skin.shaderModifiers?[.surface]?.contains("bp.zy") == true && skin.shaderModifiers?[.geometry]?.contains("bindPos") == true, "triplanar from the bind-pose position")
            // the body has no UVs; its texcoord channels carry the bind-pose position
            let bodyGeometry = try XCTUnwrap(hero.childNode(withName: "Body", recursively: false)?.geometry)
            XCTAssertEqual(bodyGeometry.sources(for: .texcoord).count, 2, "the bind pose rides in two texcoord channels")
            // the normal is weak but it is there: on the skin pixels, switching it off changes the picture by well under 2 % of the range and by more than nothing
            do {
                let size = CGSize(width: 600, height: 800)
                func shot(_ bump: Float) throws -> (luma: [Float], mask: [Bool], w: Int, h: Int) {
                    let c = stage(player(female: female), framing: .body, distance: 2.4)
                    for m in try XCTUnwrap(materials(try XCTUnwrap(c.hero))["Skin"]) { if let v = vec4(m, "skinBump") { m.setValue(SCNVector4(v.x * bump, v.y, 0, 0), forKey: "skinBump") } }
                    let mask = try partMask(c, part: { $0 == "Body" }, size: size)
                    return try lumaAndMask(snapshot(c, size: size), mask: mask)
                }
                let on = try shot(1), off = try shot(0)
                var diff: Float = 0, n = 0
                for i in 0 ..< on.luma.count where on.mask[i] && off.mask[i] { diff += abs(on.luma[i] - off.luma[i]); n += 1 }
                let meanDiff = diff / Float(max(1, n))
                NSLog("[LockerMirror] \(female ? "female" : "male") skin normal on vs off: mean luma change \(meanDiff) over \(n) skin px")
                XCTAssertGreaterThan(meanDiff, 0.0003, "the soft skin normal is wired (it changes the skin)"); XCTAssertLessThan(meanDiff, 0.02, "and it is weak")
            }
            // eyes stay smoother than skin; the decals and the eyes carry no weave and no skin normal
            for name in ["Face_Sclera", "Face_Iris", "Face_Pupil", "Face_Catch"] {
                let m = try XCTUnwrap(all[name]?.first)
                XCTAssertGreaterThan(try look(m).smoothness, sl.smoothness + 0.4, "\(name) is smoother than skin")
            }
            for (name, list) in all where name.hasPrefix("Face_") || name == "Skin" || name == "White_Frame" || name == "White_Strings" {
                for m in list {
                    XCTAssertFalse(hasClothShader(m), "\(name): not cloth, no weave")
                    XCTAssertFalse(m.shaderModifiers?.values.contains { $0.contains("weaveMap") } ?? false, "\(name): nothing samples a weave")
                    if name != "Skin" { XCTAssertNil(m.shaderModifiers, "\(name): no shader modifiers (no weave, no skin normal)") }
                }
            }
            // racket: the grip is dark and rougher than the shirt, the strings a touch smoother than the grip, neither has a weave
            let grip = try XCTUnwrap(all["Black_Grip"]?.first), shirt = try XCTUnwrap(all["Kit_Shirt"]?.first), strings = try XCTUnwrap(all["White_Strings"]?.first)
            XCTAssertLessThan(luminance(try tint(grip)), 0.2, "the grip is dark")
            XCTAssertLessThan(try look(grip).smoothness, try look(shirt).smoothness, "the grip is rougher than the shirt")
            XCTAssertGreaterThan(try look(strings).smoothness, try look(grip).smoothness, "the strings are smoother than the grip")
            XCTAssertLessThan(try look(strings).smoothness - (try look(grip).smoothness), 0.15, "a touch smoother")
            XCTAssertEqual(try look(grip).weaveNormal, 0); XCTAssertEqual(try look(grip).weaveThread, 0)
            XCTAssertEqual(grip.shaderModifiers?[.surface], MatchHeroSurfaces.clothSurfaceFlat, "the racket has no UVs: the grip is cloth without a weave")
        }
    }

    // ================================================================== rig data and skinning

    private func floats(_ data: Data) throws -> [Float] {
        let count = data.count / 4
        var out = [Float](repeating: 0, count: count)
        out.withUnsafeMutableBytes { $0.copyBytes(from: data.prefix(count * 4)) }
        return out
    }
    private struct Golden: Decodable { struct Sample: Decodable { let clip: String; let frame: Int; let part: String; let vertices: [[Double]] }; let samples: [Sample] }

    /// The shipped rig (bind meshes, bone weights, inverse binds, the clips as bone tracks) reproduces Unity's own CPU skinning of the live rig: golden vertices straight from Unity at known
    /// clip times, for every part, both clips, both sexes.
    func testShippedRigReproducesUnitysPoses() throws {
        for female in [false, true] {
            let asset = try XCTUnwrap(MatchHero.asset(female: female)), rig = try XCTUnwrap(asset.rig())
            let url = repo.appendingPathComponent("work/locker-mirror/data/MatchHero_\(female ? "Female" : "Male")_golden.json")
            let golden = try JSONDecoder().decode(Golden.self, from: Data(contentsOf: url))
            XCTAssertGreaterThan(golden.samples.count, 100)
            var worst: Float = 0, checked = 0
            // per skinned part: bind positions, weights, indices, inverse binds as plain arrays
            struct Skin { let pos: [Float]; let w: [Float]; let idx: [UInt8]; let inv: [simd_float4x4]; let bones: [Int] }
            var skins: [String: Skin] = [:]
            for s in rig.skinned {
                let inv = s.inverseBinds.map { simd_float4x4($0.scnMatrix4Value) }
                skins[s.name] = Skin(pos: try floats(s.geometry.sources(for: .vertex)[0].data), w: try floats(s.weights.data), idx: [UInt8](s.indices.data), inv: inv, bones: s.bones)
            }
            for g in golden.samples {
                let clip = try XCTUnwrap(rig.clips[g.clip])
                let t = Double(g.frame) * clip.length / Double(clip.frames - 1)
                let pose = clip.pose(at: t, loop: false)
                for v in g.vertices {
                    let i = Int(v[0]), want = SIMD3<Float>(Float(v[1]), Float(v[2]), Float(v[3]))
                    var got = SIMD3<Float>(repeating: 0)
                    if let skin = skins[g.part] {
                        var sum = SIMD4<Float>(repeating: 0)
                        for k in 0 ..< 4 {
                            let w = skin.w[i * 4 + k]; if w <= 0 { continue }
                            let b = Int(skin.idx[i * 4 + k])
                            let m = pose[skin.bones[b]].matrix * skin.inv[b]
                            sum += w * (m * SIMD4(skin.pos[i * 3], skin.pos[i * 3 + 1], skin.pos[i * 3 + 2], 1))
                        }
                        got = SIMD3(sum.x, sum.y, sum.z)
                    } else {
                        // a rigid part: its base-pose vertex moved by the part's track
                        let pi = try XCTUnwrap(asset.partIndex(g.part)), track = try XCTUnwrap(rig.info.parts.first { $0.part == g.part }?.track)
                        let base = try floats(asset.geometry[pi].sources(for: .vertex)[0].data)
                        let r = pose[track].matrix * SIMD4(base[i * 3], base[i * 3 + 1], base[i * 3 + 2], 1)
                        got = SIMD3(r.x, r.y, r.z)
                    }
                    let d = simd_distance(got, want); worst = max(worst, d); checked += 1
                    if d > 0.001 { XCTFail("\(female ? "Female" : "Male") \(g.clip) frame \(g.frame) \(g.part) v\(i): \(d * 1000) mm off Unity's pose"); return }
                }
            }
            NSLog("[LockerMirror] \(female ? "Female" : "Male"): \(checked) golden vertices, worst deviation from Unity's CPU skinning \(worst * 1000) mm")
            XCTAssertLessThan(worst, 0.0005)
        }
    }

    /// (The simulator's OFFSCREEN renderer freezes skinned meshes at their first pose when it is asked for multisampled snapshots, so posed frames are rendered without multisampling; the real
    /// SCNView is checked separately, with its own multisampling, in testBothMenuViewsRunAt60fpsAndMoveTheHeroForReal.)
    private func snapshot(_ c: CharacterModelPreview.Coordinator, size: CGSize, aa: SCNAntialiasingMode = .none) -> UIImage {
        let renderer = SCNRenderer(device: nil, options: nil); renderer.scene = c.scene; renderer.pointOfView = c.camera
        return renderer.snapshot(atTime: 0, with: size, antialiasingMode: aa)
    }
    private func rgba(_ image: UIImage) throws -> (px: [UInt8], w: Int, h: Int) {
        let cg = try XCTUnwrap(image.cgImage); var d = [UInt8](repeating: 0, count: cg.width * cg.height * 4)
        d.withUnsafeMutableBytes { ptr in
            let ctx = CGContext(data: ptr.baseAddress, width: cg.width, height: cg.height, bitsPerComponent: 8, bytesPerRow: cg.width * 4, space: CGColorSpaceCreateDeviceRGB(), bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)!
            ctx.draw(cg, in: CGRect(x: 0, y: 0, width: cg.width, height: cg.height))
        }
        return (d, cg.width, cg.height)
    }
    private func stage(_ p: Player, framing: PreviewFraming = .body, distance: Float = 3.3) -> CharacterModelPreview.Coordinator {
        let c = CharacterModelPreview.Coordinator(cameraDistance: distance); c.update(p)
        c.framing = framing; c.applyFraming()
        c.scene.background.contents = UIColor(red: 0.93, green: 0.62, blue: 0.48, alpha: 1)   // the locker's warm coral wall
        return c
    }

    /// SceneKit's GPU skinning of the bind-pose meshes at ReadyIdle frame 0 is the static Ready-stance hero (not a vertex moves when the skeleton takes over).
    func testSkinnedHeroAtReadyFrameZeroIsTheStaticHero() throws {
        for female in [false, true] {
            let a = stage(player(female: female)), b = stage(player(female: female))
            b.play(.ready); b.pose(at: 0)
            XCTAssertEqual(b.rig?.isAttached, true, "the skeleton is attached")
            let size = CGSize(width: 720, height: 1000)
            let pa = try rgba(snapshot(a, size: size)), pb = try rgba(snapshot(b, size: size))
            b.stopMotion()
            var sum = 0, over = 0
            for i in stride(from: 0, to: pa.px.count, by: 4) { for k in 0 ..< 3 { let d = abs(Int(pa.px[i + k]) - Int(pb.px[i + k])); sum += d; if d > 24 { over += 1 } } }
            let mean = Double(sum) / Double(pa.px.count / 4 * 3)
            NSLog("[LockerMirror] \(female ? "Female" : "Male"): static vs skinned at ReadyIdle[0]: mean abs diff \(mean) / 255, channels over 24: \(over)")
            XCTAssertLessThan(mean, 0.5); XCTAssertLessThan(over, 200)
        }
    }

    // ================================================================== gate line 3: motion

    private func yaw(_ n: SCNNode) -> Float { n.eulerAngles.y }
    private func bone(_ rig: HeroRig, _ name: String) throws -> SCNNode { try XCTUnwrap(rig.boneNodes.first { $0.name == name }, "bone \(name)") }

    /// Locker tile and tennis idle play ReadyIdle, in place: the pose changes, the loop closes on the same Ready pose, and nothing turns the character root.
    func testLockerTileAndTennisIdlePlayReadyIdleInPlace() throws {
        for female in [false, true] {
            for mode in ["locker", "settings", "store", "tennisIdle"] {
                let c = CharacterModelPreview.Coordinator(); c.update(player(female: female))
                switch mode {
                case "locker": c.configureClub(.locker, animate: true)
                case "settings": c.configureClub(.settings, animate: true)
                case "store": c.configureClub(.store, animate: true)
                default: c.configureIdle(sport: .tennis, animate: true)
                }
                defer { c.stopMotion() }
                let label = "\(female ? "female" : "male") \(mode)"
                XCTAssertEqual(c.motion, .ready, "\(label) plays ReadyIdle")
                let rig = try XCTUnwrap(c.rig, "\(label): the hero is on its skeleton"); XCTAssertTrue(rig.isAttached)
                let hero = try XCTUnwrap(c.hero)
                let rootYaw = yaw(hero), rootEuler = hero.eulerAngles
                XCTAssertEqual(rootYaw, .pi + 0.35, accuracy: 1e-6)
                let hand = try bone(rig, "RightHand"), hips = try bone(rig, "Hips")
                c.pose(at: 0); let h0 = hand.simdPosition, p0 = hips.simdPosition
                var travel: Float = 0, hipTravel: Float = 0
                for step in 1 ... 22 {
                    c.pose(at: Double(step) * 0.1)
                    travel = max(travel, simd_distance(hand.simdPosition, h0)); hipTravel = max(hipTravel, simd_distance(hips.simdPosition, p0))
                    XCTAssertEqual(hero.eulerAngles.y, rootYaw, accuracy: 1e-7, "\(label): the root does not yaw"); XCTAssertEqual(hero.eulerAngles.x, rootEuler.x); XCTAssertEqual(hero.eulerAngles.z, rootEuler.z)
                    XCTAssertEqual(c.character.eulerAngles.y, 0, accuracy: 1e-7); XCTAssertEqual(c.character.eulerAngles.z, 0, accuracy: 1e-7); XCTAssertEqual(c.character.eulerAngles.x, 0, accuracy: 1e-7)
                }
                XCTAssertGreaterThan(travel, 0.02, "\(label): the body pose changes (racket hand travels \(travel * 1000) mm)")
                // ReadyIdle is 2.2 s long and its last frame is its first: back to the same Ready pose
                c.pose(at: 2.2)
                XCTAssertLessThan(simd_distance(hand.simdPosition, h0), 1e-4, "\(label): back to the same Ready pose"); XCTAssertLessThan(simd_distance(hips.simdPosition, p0), 1e-4)
                XCTAssertFalse(c.character.hasActions, "\(label): no action turns the character")
                XCTAssertNil(c.practiceTimer, "\(label): no practice timer")
                XCTAssertNil(c.hero?.childNode(withName: "Body", recursively: false)?.morpher, "\(label): no Forehand morph on the hero")
            }
        }
    }

    /// The play tile plays one Serve in place, then returns to Ready and stays there: the hips rise for the hop once, never again.
    func testPlayTilePlaysOneServeThenReady() throws {
        for female in [false, true] {
            let c = CharacterModelPreview.Coordinator(); c.update(player(female: female))
            c.configureClub(.play, animate: true); defer { c.stopMotion() }
            XCTAssertEqual(c.motion, .serveOnce)
            let rig = try XCTUnwrap(c.rig), hips = try bone(rig, "Hips"), hero = try XCTUnwrap(c.hero)
            let motion = try XCTUnwrap(c.motion)
            XCTAssertEqual(try XCTUnwrap(c.hero?.childNode(withName: "racket", recursively: false)).isHidden, false, "the play tile holds the racket")
            let ready = try XCTUnwrap(rig.data.clips["ready"]), serve = try XCTUnwrap(rig.data.clips["serve"])
            XCTAssertEqual(serve.info.name, female ? "Female_Serve" : "Male_Serve")
            XCTAssertEqual(ready.info.name, female ? "Female_ReadyIdle" : "Male_ReadyIdle")
            let settled = motion.settledAfter(rig.data)
            XCTAssertEqual(settled, motion.lead + serve.length, accuracy: 1e-9)
            c.pose(at: 0); let readyY = hips.simdPosition.y
            // the hops: stretches where the hips are well above the Ready stance (the Serve's pre-toss pop and its contact hop), over 14 s at 30 Hz. All of them lie inside the Serve; none before it, none after.
            var hopsBefore = 0, hopsDuring = 0, hopsAfter = 0, inside = false, peak: Float = 0, peakAt = 0.0
            var t = 0.0
            while t <= 14 {
                c.pose(at: t)
                let rise = hips.simdPosition.y - readyY
                if rise > 0.10 {
                    if !inside { inside = true; if t < motion.lead { hopsBefore += 1 } else if t <= settled { hopsDuring += 1 } else { hopsAfter += 1 } }
                    if rise > peak { peak = rise; peakAt = t }
                } else if rise < 0.05 { inside = false }
                XCTAssertEqual(hero.eulerAngles.y, .pi + 0.35, accuracy: 1e-7, "the root never yaws")
                t += 1.0 / 30
            }
            XCTAssertEqual(hopsBefore, 0, "Ready until the Serve starts"); XCTAssertEqual(hopsAfter, 0, "one Serve, then Ready for good")
            XCTAssertGreaterThanOrEqual(hopsDuring, 1, "the Serve hops")
            XCTAssertGreaterThan(peak, 0.15, "the dip-and-jump Serve lifts the hips by \(peak * 1000) mm")
            XCTAssertGreaterThan(peakAt, motion.lead); XCTAssertLessThan(peakAt, settled)
            // after the Serve: the pose IS the Ready loop's, exactly, for good
            for later in stride(from: settled + 0.01, through: settled + 12, by: 0.37) {
                let a = motion.pose(rig.data, at: later), b = MenuMotion.ready.pose(rig.data, at: later)
                for (x, y) in zip(a, b) { XCTAssertLessThan(simd_distance(x.t, y.t), 1e-6) }
            }
            XCTAssertNil(c.practiceTimer); XCTAssertFalse(c.character.hasActions)
            XCTAssertNil(hero.childNode(withName: "Body", recursively: false)?.morpher, "no Forehand loop on the play tile")
            // the serve starts from Ready and ends in Ready: blended in and out, no pop
            let start = motion.pose(rig.data, at: motion.lead - 0.001), start2 = motion.pose(rig.data, at: motion.lead + 0.001)
            for (x, y) in zip(start, start2) { XCTAssertLessThan(simd_distance(x.t, y.t), 0.01, "no pop at the start of the Serve") }
            let end = motion.pose(rig.data, at: settled - 0.001), end2 = motion.pose(rig.data, at: settled + 0.001)
            for (x, y) in zip(end, end2) { XCTAssertLessThan(simd_distance(x.t, y.t), 0.01, "no pop at the end of the Serve") }
        }
    }

    /// A left-hander's hero is the same hero mirrored: its skeleton plays the same clips under the mirrored root, the root keeps its mirror and its yaw, and every pose is finite.
    func testLeftHandedHeroPlaysTheSameClipsMirrored() throws {
        for female in [false, true] {
            var p = player(female: female); p.handedness = .left
            let c = CharacterModelPreview.Coordinator(); c.update(p); c.configureClub(.play, animate: true); c.pauseDisplayLink()
            defer { c.stopMotion() }
            let hero = try XCTUnwrap(c.hero), rig = try XCTUnwrap(c.rig), hand = try bone(rig, "RightHand")
            XCTAssertLessThan(hero.scale.x, 0, "the left-hander's hero is mirrored")
            var worst: Float = 0
            for t in stride(from: 0.0, through: 4.0, by: 0.1) {
                c.pose(at: t)
                XCTAssertLessThan(hero.scale.x, 0); XCTAssertEqual(hero.eulerAngles.y, .pi + 0.35, accuracy: 1e-6)
                let w = hand.simdWorldPosition; XCTAssertTrue(w.x.isFinite && w.y.isFinite && w.z.isFinite)
                worst = max(worst, abs(w.x))
            }
            XCTAssertGreaterThan(worst, 0.05, "the hand moves")
            // the same pose as the right-hander's, mirrored in world x about the hero's axis (the bone's local position is the same file-space number)
            var r = player(female: female); r.handedness = .right
            let d = CharacterModelPreview.Coordinator(); d.update(r); d.configureClub(.play, animate: true); d.pauseDisplayLink(); defer { d.stopMotion() }
            let rhand = try bone(try XCTUnwrap(d.rig), "RightHand")
            c.pose(at: 1.3); d.pose(at: 1.3)
            XCTAssertLessThan(simd_distance(hand.simdPosition, rhand.simdPosition), 1e-6, "same bone transform in the hero's own space")
        }
    }

    /// The tennis idle on the loading screen plays ReadyIdle between practice swings and hands over cleanly to the swing (still the Forehand morph on the static Ready stance) and back: the real loading
    /// screen in the TV root, driven by the real "Swing to practice" trigger.
    func testLoadingScreenIdlesBetweenPracticeSwingsAndHandsOverCleanly() async throws {
        let menu = TennisMenu.shared, session = SportsSession.shared
        let oldPlayers = session.players, oldIndex = session.playerIndex, oldMotion = session.reduceMotion
        session.players = [player(female: true)]; session.playerIndex = 0; session.reduceMotion = false
        defer { session.players = oldPlayers; session.playerIndex = oldIndex; session.reduceMotion = oldMotion; session.loading.cancel(); menu.debugShow(.title) }
        menu.debugShow(.loading, launch: MenuLaunch(mode: .training)); session.loading.begin(now: Date()); session.loading.reach(0.72); session.loading.tick(now: Date())
        let windowScene = try XCTUnwrap(UIApplication.shared.connectedScenes.compactMap { $0 as? UIWindowScene }.first)
        let size = CGSize(width: 1280, height: 720)
        let host = UIHostingController(rootView: TennisTVRoot()); host.safeAreaRegions = []
        let window = UIWindow(windowScene: windowScene); window.frame = CGRect(origin: .zero, size: size)
        window.rootViewController = host; window.isHidden = false; host.view.frame = window.bounds; host.view.layoutIfNeeded()
        defer { window.isHidden = true }
        try await Task.sleep(for: .seconds(1.2))
        let scn = try XCTUnwrap(findScene(host.view), "the loading screen shows the hero")
        let hero = try XCTUnwrap(scn.scene?.rootNode.childNode(withName: MatchHero.rootName, recursively: true))
        func skeleton() -> SCNNode? { scn.scene?.rootNode.childNode(withName: "skeleton", recursively: true) }
        func morphing() -> Int { var n = 0; hero.enumerateChildNodes { node, _ in if let m = node.morpher, m.weights.contains(where: { $0.doubleValue > 0.01 }) { n += 1 } }; return n }
        func morphers() -> Int { var n = 0; hero.enumerateChildNodes { node, _ in if node.morpher != nil { n += 1 } }; return n }
        XCTAssertEqual(scn.preferredFramesPerSecond, 60, "the idle view runs at 60 fps")
        XCTAssertNotNil(skeleton(), "the tennis idle plays ReadyIdle on the skeleton, not a turn of the character"); XCTAssertEqual(morphers(), 0)
        XCTAssertEqual(hero.eulerAngles.y, .pi + 0.35, accuracy: 1e-6)
        for round in 1 ... 2 {
            session.loading.practice()
            try await Task.sleep(for: .milliseconds(650))
            XCTAssertGreaterThan(morphing(), 3, "round \(round): the practice swing animates the body, the face and the racket")
            XCTAssertNil(skeleton(), "round \(round): the swing builds on the static Ready stance")
            XCTAssertEqual(hero.eulerAngles.y, .pi + 0.35, accuracy: 1e-6)
            try await Task.sleep(for: .milliseconds(1700))
            XCTAssertEqual(morphers(), 0, "round \(round): the swing is over")
            XCTAssertNotNil(skeleton(), "round \(round): back on the ReadyIdle loop")
            XCTAssertEqual(hero.eulerAngles.y, .pi + 0.35, accuracy: 1e-6)
        }
    }

    /// Reduced motion keeps a still Ready pose: no skeleton, no clock, no display link; the static Ready-stance hero.
    func testReducedMotionStaysAStillReadyPose() throws {
        for female in [false, true] {
            for mode in ["locker", "play", "tennisIdle"] {
                let c = CharacterModelPreview.Coordinator(); c.update(player(female: female))
                switch mode { case "locker": c.configureClub(.locker, animate: false); case "play": c.configureClub(.play, animate: false); default: c.configureIdle(sport: .tennis, animate: false) }
                XCTAssertNil(c.motion, "\(mode): nothing is playing"); XCTAssertNil(c.rig)
                let body = try XCTUnwrap(c.hero?.childNode(withName: "Body", recursively: false))
                XCTAssertNil(body.skinner, "\(mode): the static Ready stance")
                XCTAssertFalse(c.character.hasActions)
                XCTAssertEqual(c.hero?.eulerAngles.y ?? 0, .pi + 0.35, accuracy: 1e-7)
            }
        }
    }

    private func findScene(_ view: UIView) -> SCNView? { if let v = view as? SCNView { return v }; return view.subviews.compactMap(findScene).first }

    /// Both menu views (the club tiles and the idle view) run at 60 fps; the real SwiftUI view in a window moves its hero for real, and with reduced motion on it stands still.
    func testBothMenuViewsRunAt60fpsAndMoveTheHeroForReal() async throws {
        let scene = try XCTUnwrap(UIApplication.shared.connectedScenes.compactMap { $0 as? UIWindowScene }.first)
        let session = SportsSession.shared, oldReduce = session.reduceMotion
        defer { session.reduceMotion = oldReduce }
        for reduce in [false, true] {
            session.reduceMotion = reduce
            for (label, make) in [("club locker tile", { (p: Player) in AnyView(CharacterModelPreview(player: p, cameraDistance: 3.25, menuActivity: .locker)) }),
                                  ("club play tile", { (p: Player) in AnyView(CharacterModelPreview(player: p, cameraDistance: 3.25, menuActivity: .play)) }),
                                  ("tennis idle view", { (p: Player) in AnyView(CharacterModelPreview(player: p, cameraDistance: 3.0, idleSport: .tennis)) })] {
                let host = UIHostingController(rootView: make(player(female: false)))
                let window = UIWindow(windowScene: scene); window.frame = CGRect(x: 0, y: 0, width: 402, height: 560)
                window.rootViewController = host; window.isHidden = false; host.view.frame = window.bounds; host.view.layoutIfNeeded()
                defer { window.isHidden = true }
                try await Task.sleep(for: .seconds(0.8))
                let scn = try XCTUnwrap(findScene(host.view), "\(label) is an SCNView")
                XCTAssertEqual(scn.preferredFramesPerSecond, 60, "\(label) runs at 60 fps")
                let hero = try XCTUnwrap(scn.scene?.rootNode.childNode(withName: MatchHero.rootName, recursively: true))
                XCTAssertEqual(hero.eulerAngles.y, .pi + 0.35, accuracy: 1e-6, "\(label): the root has not turned")
                let skeleton = scn.scene?.rootNode.childNode(withName: "skeleton", recursively: true)
                if reduce {
                    XCTAssertFalse(scn.isPlaying, "\(label): reduced motion does not run the render loop"); XCTAssertNil(skeleton, "\(label): still Ready pose")
                } else {
                    XCTAssertTrue(scn.isPlaying, "\(label) plays")
                    let rig = try XCTUnwrap(skeleton, "\(label): the hero is on its skeleton")
                    let hand = try XCTUnwrap(rig.childNode(withName: "RightHand", recursively: false))
                    let a = hand.simdPosition, shotA = try rgba(scn.snapshot())
                    try await Task.sleep(for: .seconds(0.7))
                    let b = hand.simdPosition, shotB = try rgba(scn.snapshot())
                    XCTAssertGreaterThan(simd_distance(a, b), 0.001, "\(label): the display link poses the skeleton (the racket hand moved \(simd_distance(a, b) * 1000) mm in 0.7 s)")
                    // and the pixels of the real, multisampled view change: the SKINNED body deforms on screen (not only the bone nodes)
                    var changed = 0
                    for i in stride(from: 0, to: min(shotA.px.count, shotB.px.count), by: 4) where abs(Int(shotA.px[i]) - Int(shotB.px[i])) + abs(Int(shotA.px[i + 1]) - Int(shotB.px[i + 1])) + abs(Int(shotA.px[i + 2]) - Int(shotB.px[i + 2])) > 30 { changed += 1 }
                    NSLog("[LockerMirror] \(label): \(changed) pixels of the real view changed in 0.7 s")
                    XCTAssertGreaterThan(changed, 150, "\(label): the body is seen moving in the real view (\(changed) px changed)")
                    XCTAssertEqual(hero.eulerAngles.y, .pi + 0.35, accuracy: 1e-6)
                }
            }
        }
    }
}

extension LockerMirrorTests {
    func testLobbyEmoteTracksMatchLiveUnityAtTenFrames() throws {
        struct Samples: Decodable { var samples:[Sample] }
        struct Sample: Decodable { var clip:String; var time:Double; var tracks:[[Float]] }
        var maxRotation: Float = 0, maxRigidMM: Float = 0
        for female in [false,true] {
            let asset = try XCTUnwrap(MatchHero.asset(female:female)), data = try XCTUnwrap(asset.rig())
            let file = repo.appendingPathComponent("work/online-lobby/emote_parity/data/MatchHero_\(female ? "Female" : "Male")_emote_parity.json")
            let samples = try JSONDecoder().decode(Samples.self,from:Data(contentsOf:file))
            XCTAssertEqual(samples.samples.count,60)
            for sample in samples.samples {
                let clip = try XCTUnwrap(data.clips[sample.clip]); XCTAssertEqual(clip.info.fps,60,accuracy:0.001)
                let got = clip.pose(at:sample.time,loop:false)
                XCTAssertEqual(got.count,sample.tracks.count)
                for (i,p) in got.enumerated() {
                    let values = sample.tracks[i], q = simd_quatf(ix:values[3],iy:values[4],iz:values[5],r:values[6])
                    let angle = 2 * acos(min(1,abs(simd_dot(simd_normalize(p.q).vector,simd_normalize(q).vector)))) * 180 / .pi
                    maxRotation = max(maxRotation,angle); XCTAssertLessThanOrEqual(angle,2,"\(female) \(sample.clip) t\(sample.time) track\(i)")
                    if i >= data.info.bones.count {
                        let error = simd_distance(p.t,SIMD3(values[0],values[1],values[2])) * 1000
                        maxRigidMM = max(maxRigidMM,error); XCTAssertLessThan(error,5,"rigid racket follows Unity")
                    }
                }
            }
            for id in MultiplayerEmote.ids {
                let clip = try XCTUnwrap(data.clips[id])
                let motion = MenuMotion(kind:.clipOnce(id),lead:0,fadeIn:0.12,fadeOut:0.2)
                for time in [0,clip.length+0.2] {
                    let readyAtTime = try XCTUnwrap(data.clips["ready"]).pose(at:time,loop:true)
                    for (p,r) in zip(motion.pose(data,at:time),readyAtTime) {
                        XCTAssertLessThan(simd_distance(p.t,r.t),0.005,"\(id) READY end")
                        let angle = 2 * acos(min(1,abs(simd_dot(p.q.vector,r.q.vector)))) * 180 / .pi
                        XCTAssertLessThan(angle,2,"\(id) READY end rotation")
                    }
                }
            }
        }
        let report = "P1 sampled 6 clips × 2 heroes × 10 times; max rotation \(maxRotation) degrees; max rigid translation \(maxRigidMM) mm\n"
        try report.write(to:repo.appendingPathComponent("work/online-lobby/emote_parity/rotation-report.txt"),atomically:true,encoding:.utf8)
    }
    func testLobbyMotionQueuesAtMostOneAndSettlesToOffsetIdle() throws {
        let c = CharacterModelPreview.Coordinator(); c.update(player(female:false))
        let rig = try XCTUnwrap(HeroRig(root:try XCTUnwrap(c.hero),asset:try XCTUnwrap(MatchHero.asset(female:false))))
        let motion = LobbyHeroMotion(rig:rig,idleOffset:0.73)
        motion.play("scuba",at:100,now:100); motion.play("wave",at:101,now:101); motion.play("pushups",at:102,now:102)
        XCTAssertEqual(motion.queued,"pushups")
        motion.pose(at:motion.endTime + 0.01); XCTAssertEqual(motion.clip,"pushups"); XCTAssertNil(motion.queued)
        motion.pose(at:motion.endTime + 0.01); XCTAssertNil(motion.clip)
        let t = motion.endTime + 1, expected = try XCTUnwrap(rig.data.clips["ready"]).pose(at:t+0.73,loop:true)
        motion.pose(at:t)
        for (node,pose) in zip(rig.boneNodes,expected) { XCTAssertLessThan(simd_distance(node.simdPosition,pose.t),0.00001) }
    }
}
