import Foundation
import SceneKit
import simd

/// LOCKER_MIRROR: everything that reads the Unity export of the match heroes (CharacterAssets/MatchHero_<Sex>*, written by Unity's MatchHeroLockerExport) and turns it into
/// SceneKit parts. No UIKit and no `Player` in here (the picks arrive as `Picks`), so the same code builds the hero in the app, in the tests and in a command-line render harness.
///
///   MatchHero_<Sex>.json         manifest: parts, submeshes (each with the LOOK numbers read off the hero's runtime materials), bounds, rig description
///   MatchHero_<Sex>.lzfse        the Ready stance (frame 0 of <Sex>_ReadyIdle): positions, normals, indices; the kit's UVs; the body's bind-pose positions
///   MatchHero_<Sex>_Swing_NN     the match Forehand sampled densely around the strike (morph targets for the loading screen's practice swing)
///   MatchHero_<Sex>_Rig.lzfse    the bind-pose mesh of every skinned part with its bone weights and inverse binds, and the clips <Sex>_ReadyIdle and <Sex>_Serve as bone tracks (60 Hz)
///   MatchHero_<map>.png          the look maps the runtime materials sample (cloth weave, the kit's seam maps, the soft skin normal)
@MainActor enum MatchHeroData {
    /// Height the hero stands on the locker stage: the frame the old chibi filled, so every locker camera keeps its framing.
    static let stageHeight: Float = 1.387

    // MARK: manifest
    struct Look: Decodable {
        let shader: String
        let color: [Float], rimColor: [Float], subsurface: [Float]
        let smoothness: Float, wrap: Float, rimStrength: Float, rimPower: Float, exposure: Float, knee: Float
        let weaveTile: Float, weaveAngle: Float, weaveNormal: Float, weaveThread: Float, sheenStrength: Float, sheenPower: Float
        let saturation: Float, bumpScale: Float, bumpTriplanar: Float, bumpTile: Float
        let baseMap: String, weaveMap: String, bumpMap: String
        let normalMap: String?, maskMap: String?, trimColor: [Float]?, normalStrength: Float?, useGarmentMaps: Float?
        /// Cloth only: the size in metres of one weave tile (TennisCloth's tile = metres per UV unit / this).
        let tileMetres: Float?
        let fabricVersion: Float?
        var isCloth: Bool { shader == "TennisCloth" }
        var isCharacter: Bool { shader == "TennisCharacter" }
    }
    struct Sub: Decodable { let material: String; let indexOffset: Int; let indexCount: Int; let look: Look? }
    struct Part: Decodable {
        let name: String, kind: String
        let vertexCount: Int, positionOffset: Int, normalOffset: Int, swingPositionOffset: Int, swingNormalOffset: Int
        let uvOffset: Int?, bindPositionOffset: Int?
        let submeshes: [Sub]
    }
    struct Mat: Decodable { let name: String; let color: [Float]; let smoothness: Float }
    struct RigPartInfo: Decodable {
        let part: String, kind: String
        let vertexCount: Int, bindPositionOffset: Int, bindNormalOffset: Int, weightOffset: Int, indexOffset: Int, inverseBindOffset: Int, track: Int
        let bones: [Int]
    }
    struct ClipInfo: Decodable {
        let id: String, name: String
        let loop: Bool
        let length: Float, fps: Float, contact: Float
        let frames: Int, offset: Int
        let boundsMin: [Float], boundsMax: [Float]
        let times: [Float]?
    }
    struct RigInfo: Decodable {
        let file: String
        let bones: [String], rigid: [String]
        let trackCount: Int, floatsPerTrack: Int
        let parts: [RigPartInfo]
        let clips: [ClipInfo]
    }
    struct Manifest: Decodable {
        let sex: String, baseClip: String, swingClip: String
        let parts: [Part]; let materials: [Mat]
        let swingFrames: [String]; let swingTimes: [Float]
        let swingLength: Float, swingContact: Float
        let height: Float, headCentreY: Float, headTopY: Float, headRadius: Float
        let rig: RigInfo?
    }

    // MARK: resources
    /// Where the export files are found: the app bundle (the tests run inside the app). The command-line harness points this at CharacterAssets.
    static var locate: (_ name: String, _ ext: String) -> URL? = { name, ext in Bundle.main.url(forResource: name, withExtension: ext) }

    /// <name>.lzfse decompressed, or <name>.bin as is.
    static func data(_ name: String) -> Data? {
        if let u = locate(name, "lzfse"), let d = try? Data(contentsOf: u), let raw = try? (d as NSData).decompressed(using: .lzfse) { return raw as Data }
        guard let u = locate(name, "bin") else { return nil }
        return try? Data(contentsOf: u)
    }

    // MARK: one sex's data
    /// One sex's loaded data: the base-pose geometry per part and, on demand, the swing frames as morph targets and the rig (skin weights + clips).
    @MainActor final class Asset {
        let manifest: Manifest, geometry: [SCNGeometry], scale: Float
        var swing: [[SCNGeometry]]?
        private var rigLoaded = false, rigCache: RigData?
        init(manifest: Manifest, geometry: [SCNGeometry]) {
            self.manifest = manifest; self.geometry = geometry; scale = MatchHeroData.stageHeight / manifest.height
        }
        var female: Bool { manifest.sex == "Female" }
        func partIndex(_ name: String) -> Int? { manifest.parts.firstIndex { $0.name == name } }
        /// [part][frame]: positions + normals of the Forehand at manifest.swingTimes, on the base mesh's own triangles.
        func swingTargets() -> [[SCNGeometry]]? {
            if let swing { return swing }
            var perPart = [[SCNGeometry]](repeating: [], count: manifest.parts.count)
            for frame in manifest.swingFrames {
                guard let data = MatchHeroData.data(frame) else { return nil }
                for (i, part) in manifest.parts.enumerated() {
                    let n = part.vertexCount * 12
                    guard part.swingNormalOffset + n <= data.count, part.swingPositionOffset + n <= data.count else { return nil }
                    let sources = [(part.swingPositionOffset, SCNGeometrySource.Semantic.vertex), (part.swingNormalOffset, .normal)].map { offset, semantic in
                        SCNGeometrySource(data: data.subdata(in: offset ..< offset + n), semantic: semantic, vectorCount: part.vertexCount,
                                          usesFloatComponents: true, componentsPerVector: 3, bytesPerComponent: 4, dataOffset: 0, dataStride: 12)
                    }
                    perPart[i].append(SCNGeometry(sources: sources, elements: geometry[i].elements))
                }
            }
            swing = perPart; return perPart
        }
        /// The skin weights, bind-pose meshes and clips (nil when the export has no rig or its file is missing).
        func rig() -> RigData? {
            if rigLoaded { return rigCache }
            rigLoaded = true; rigCache = RigData(asset: self)
            return rigCache
        }
    }

    private static var assets: [String: Asset] = [:]

    static func asset(female: Bool, golf: Bool = false) -> Asset? {
        let name = (golf ? "GolfKitHero_" : "MatchHero_") + (female ? "Female" : "Male")
        if let a = assets[name] { return a }
        guard let url = locate(name, "json"), let md = try? Data(contentsOf: url),
              let manifest = try? JSONDecoder().decode(Manifest.self, from: md), let bin = data(name) else { return nil }
        var geometry: [SCNGeometry] = []
        for part in manifest.parts {
            let n = part.vertexCount * 12
            guard part.positionOffset + n <= bin.count, part.normalOffset + n <= bin.count else { return nil }
            func src(_ off: Int, _ sem: SCNGeometrySource.Semantic) -> SCNGeometrySource {
                SCNGeometrySource(data: bin.subdata(in: off ..< off + n), semantic: sem, vectorCount: part.vertexCount,
                                  usesFloatComponents: true, componentsPerVector: 3, bytesPerComponent: 4, dataOffset: 0, dataStride: 12)
            }
            var elements: [SCNGeometryElement] = []
            for sub in part.submeshes {
                guard sub.indexOffset + sub.indexCount * 4 <= bin.count else { return nil }
                elements.append(SCNGeometryElement(data: bin.subdata(in: sub.indexOffset ..< sub.indexOffset + sub.indexCount * 4), primitiveType: .triangles,
                                                   primitiveCount: sub.indexCount / 3, bytesPerIndex: 4))
            }
            var sources = [src(part.positionOffset, .vertex), src(part.normalOffset, .normal)]
            sources += texcoordSources(part, bin: bin) ?? []
            geometry.append(SCNGeometry(sources: sources, elements: elements))
        }
        let a = Asset(manifest: manifest, geometry: geometry); assets[name] = a; return a
    }

    /// The kit's UV0 (texcoord channel 0), or for the body (which has no UVs) its BIND-pose position in two channels: (x, y) and (z): the skin's soft normal is projected from there.
    static func texcoordSources(_ part: Part, bin: Data) -> [SCNGeometrySource]? {
        if let uv = part.uvOffset, uv >= 0, uv + part.vertexCount * 8 <= bin.count {
            return [SCNGeometrySource(data: bin.subdata(in: uv ..< uv + part.vertexCount * 8), semantic: .texcoord, vectorCount: part.vertexCount,
                                      usesFloatComponents: true, componentsPerVector: 2, bytesPerComponent: 4, dataOffset: 0, dataStride: 8)]
        }
        if let bp = part.bindPositionOffset, bp >= 0, bp + part.vertexCount * 12 <= bin.count {
            // channel 0 = (x, y), channel 1 = (z, 0): the second source reads the z of every 12-byte vertex; one extra float of padding keeps its last 8-byte read inside the buffer
            var d = bin.subdata(in: bp ..< bp + part.vertexCount * 12); d.append(contentsOf: [0, 0, 0, 0])
            return [SCNGeometrySource(data: d, semantic: .texcoord, vectorCount: part.vertexCount, usesFloatComponents: true, componentsPerVector: 2, bytesPerComponent: 4, dataOffset: 0, dataStride: 12),
                    SCNGeometrySource(data: d, semantic: .texcoord, vectorCount: part.vertexCount, usesFloatComponents: true, componentsPerVector: 2, bytesPerComponent: 4, dataOffset: 8, dataStride: 12)]
        }
        return nil
    }

    // MARK: the hero node
    /// The locker's picks, as colours: the hero does not know `Player`.
    struct Picks {
        /// sRGB colour a material is tinted with for this pick, by the exported material name ("Skin", "White_Frame", "Kit_Shirt" ...), or nil for the authored colour.
        var colour: @MainActor (_ material: String) -> SIMD3<Float>?
        var leftHanded = false
    }

    static let rootName = "matchHero", racketName = "racket"

    /// The hero node: Body, Face, the six Kit_* parts and a "racket" group (frame, strings, grip), turned to the camera a touch 3/4, mirrored for a left-hander. Static geometry in the Ready stance;
    /// `HeroRig` (MatchHeroMotion.swift) swaps the skinned parts in when something has to move.
    static func buildHero(_ asset: Asset, picks: Picks) -> SCNNode {
        let m = asset.manifest
        let root = SCNNode(); root.name = rootName
        root.eulerAngles.y = .pi + 0.35   // exported facing -z; face the camera, a touch of 3/4
        let s = asset.scale; root.scale = SCNVector3(s, s, s)
        root.setValue(asset.female, forKey: "heroFemale")
        let racket = SCNNode(); racket.name = racketName; root.addChildNode(racket)
        for (i, part) in m.parts.enumerated() {
            guard let geometry = asset.geometry[i].copy() as? SCNGeometry else { continue }
            geometry.materials = part.submeshes.map { sub in
                let trimRole = (sub.material == "Kit_Shirt" || sub.material == "Kit_ShirtTrim" || sub.material == "Kit_GolfHead" || sub.material == "Kit_GolfGlove") ? "Kit_ShirtTrim" : (sub.material == "Kit_Shorts" || sub.material == "Kit_ShortsBand") ? "Kit_ShortsBand" : ""
                let colour = picks.colour(sub.material)
                var trim = trimRole.isEmpty ? nil : picks.colour(trimRole)
                if sub.material == "Kit_Shoe", let tint = colour {
                    let pick = SIMD3<Float>(tint.x <= 0.035 ? 0 : tint.x / 0.93, tint.y <= 0.035 ? 0 : tint.y / 0.93, tint.z <= 0.0401 ? 0 : tint.z / 0.93)
                    let luminance = simd_dot(pick, SIMD3<Float>(0.2126, 0.7152, 0.0722))
                    let derived = luminance >= 0.5 ? pick * 0.65 : pick + (SIMD3<Float>(repeating: 1) - pick) * 0.35
                    trim = SIMD3<Float>(max(derived.x * 0.93, 0.035), max(derived.y * 0.93, 0.035), max(derived.z * 0.93, 0.0401))
                }
                return MatchHeroSurfaces.material(sub, colour: colour, sceneScale: s, trimColour: trim)
            }
            let node = SCNNode(geometry: geometry); node.name = part.name
            if part.name == "Kit_Glove_R" { node.isHidden = true }
            node.setValue(i, forKey: "menuPartIndex")
            (part.kind == "racket" ? racket : root).addChildNode(node)
        }
        if picks.leftHanded { root.scale.x = -root.scale.x }
        return root
    }
}

// MARK: - the rig: skin weights, bind-pose meshes and the clips as bone tracks

/// One bone / rigid-part track at one instant: translation, rotation, scale, all in hero space (z mirrored like the geometry).
struct HeroTrackPose { var t: SIMD3<Float>; var q: simd_quatf; var s: SIMD3<Float> }

extension HeroTrackPose {
    var matrix: simd_float4x4 {
        var m = simd_matrix4x4(q)
        m.columns.0 *= s.x; m.columns.1 *= s.y; m.columns.2 *= s.z
        m.columns.3 = SIMD4(t, 1)
        return m
    }
    static func mix(_ a: HeroTrackPose, _ b: HeroTrackPose, _ w: Float) -> HeroTrackPose {
        var qb = b.q
        if simd_dot(a.q.vector, qb.vector) < 0 { qb = simd_quatf(vector: -qb.vector) }
        return HeroTrackPose(t: simd_mix(a.t, b.t, SIMD3(repeating: w)), q: simd_normalize(simd_slerp(a.q, qb, w)), s: simd_mix(a.s, b.s, SIMD3(repeating: w)))
    }
}

@MainActor final class RigData {
    /// One skinned part: the bind-pose geometry (bind positions and normals, the part's texcoords) with its bone weights / indices and the inverse bind of every bone it uses.
    struct Skinned {
        let partIndex: Int, name: String
        let geometry: SCNGeometry, weights: SCNGeometrySource, indices: SCNGeometrySource
        let inverseBinds: [NSValue]
        /// skeleton index of each of the part's own bones
        let bones: [Int]
    }
    /// One clip: `frames` samples of `trackCount` tracks, 10 floats each (translation, rotation xyzw, scale).
    struct Clip {
        let info: MatchHeroData.ClipInfo
        let trackCount: Int
        let data: [Float]
        var frames: Int { info.frames }
        var length: Double { Double(info.length) }
        func track(_ frame: Int, _ track: Int) -> HeroTrackPose {
            let o = (frame * trackCount + track) * 10
            return HeroTrackPose(t: SIMD3(data[o], data[o + 1], data[o + 2]), q: simd_quatf(ix: data[o + 3], iy: data[o + 4], iz: data[o + 5], r: data[o + 6]), s: SIMD3(data[o + 7], data[o + 8], data[o + 9]))
        }
        /// Every track at clip time `time`, interpolated between the two surrounding samples (a loop wraps; otherwise the time is clamped to the clip).
        func pose(at time: Double, loop: Bool) -> [HeroTrackPose] {
            let n = frames - 1
            var f: Double
            if loop { f = time.truncatingRemainder(dividingBy: length) / length * Double(n); if f < 0 { f += Double(n) } }
            else { f = min(max(time, 0), length) / length * Double(n) }
            var i0 = min(n, Int(f.rounded(.down))), i1 = min(n, i0 + 1), w = Float(f - Double(i0))
            if let times = info.times, times.count == frames {
                let t = Float(min(max(time,0),length))
                var low = 0, high = n
                while low < high { let mid = (low+high+1)/2; if times[mid] <= t { low=mid } else { high=mid-1 } }
                i0=low; i1=min(n,low+1); w=i0 == i1 ? 0 : (t-times[i0])/max(0.000001,times[i1]-times[i0])
            }
            return (0 ..< trackCount).map { i in
                let a = track(i0, i)
                return w == 0 || i0 == i1 ? a : HeroTrackPose.mix(a, track(i1, i), w)
            }
        }
    }

    let info: MatchHeroData.RigInfo
    private(set) var skinned: [Skinned] = []
    private(set) var clips: [String: Clip] = [:]
    var boneCount: Int { info.bones.count }

    init?(asset: MatchHeroData.Asset) {
        guard let info = asset.manifest.rig, let blob = MatchHeroData.data(info.file) else { return nil }
        self.info = info
        func floats(_ offset: Int, _ count: Int) -> [Float]? {
            guard offset >= 0, offset + count * 4 <= blob.count else { return nil }
            var out = [Float](repeating: 0, count: count)
            out.withUnsafeMutableBytes { $0.copyBytes(from: blob[offset ..< offset + count * 4]) }
            return out
        }
        for rp in info.parts where rp.kind == "skin" {
            guard let pi = asset.partIndex(rp.part) else { return nil }
            let v = rp.vertexCount
            guard rp.bindPositionOffset + v * 12 <= blob.count, rp.bindNormalOffset + v * 12 <= blob.count, rp.weightOffset + v * 16 <= blob.count, rp.indexOffset + v * 4 <= blob.count,
                  let inv = floats(rp.inverseBindOffset, rp.bones.count * 16) else { return nil }
            func src(_ off: Int, _ sem: SCNGeometrySource.Semantic) -> SCNGeometrySource {
                SCNGeometrySource(data: blob.subdata(in: off ..< off + v * 12), semantic: sem, vectorCount: v, usesFloatComponents: true, componentsPerVector: 3, bytesPerComponent: 4, dataOffset: 0, dataStride: 12)
            }
            let base = asset.geometry[pi]
            let geometry = SCNGeometry(sources: [src(rp.bindPositionOffset, .vertex), src(rp.bindNormalOffset, .normal)] + base.sources(for: .texcoord), elements: base.elements)
            let weights = SCNGeometrySource(data: blob.subdata(in: rp.weightOffset ..< rp.weightOffset + v * 16), semantic: .boneWeights, vectorCount: v,
                                            usesFloatComponents: true, componentsPerVector: 4, bytesPerComponent: 4, dataOffset: 0, dataStride: 16)
            let indices = SCNGeometrySource(data: blob.subdata(in: rp.indexOffset ..< rp.indexOffset + v * 4), semantic: .boneIndices, vectorCount: v,
                                            usesFloatComponents: false, componentsPerVector: 4, bytesPerComponent: 1, dataOffset: 0, dataStride: 4)
            // inverse binds are stored row-major: simd wants columns
            let binds = (0 ..< rp.bones.count).map { b -> NSValue in
                let r = (0 ..< 16).map { inv[b * 16 + $0] }
                let m = simd_float4x4(columns: (SIMD4(r[0], r[4], r[8], r[12]), SIMD4(r[1], r[5], r[9], r[13]), SIMD4(r[2], r[6], r[10], r[14]), SIMD4(r[3], r[7], r[11], r[15])))
                return NSValue(scnMatrix4: SCNMatrix4(m))
            }
            skinned.append(Skinned(partIndex: pi, name: rp.part, geometry: geometry, weights: weights, indices: indices, inverseBinds: binds, bones: rp.bones))
        }
        for c in info.clips {
            guard let d = floats(c.offset, c.frames * info.trackCount * 10) else { return nil }
            clips[c.id] = Clip(info: c, trackCount: info.trackCount, data: d)
        }
        guard clips["ready"] != nil else { return nil }
    }

    /// Blend of two poses (same track count): `w` = 0 is `a`, 1 is `b`.
    static func blend(_ a: [HeroTrackPose], _ b: [HeroTrackPose], _ w: Float) -> [HeroTrackPose] {
        w <= 0 ? a : w >= 1 ? b : zip(a, b).map { HeroTrackPose.mix($0, $1, w) }
    }
}
