import XCTest
import SceneKit
import simd
@testable import GolfArcade

/// Compare the shipped SceneKit graph with vertices sampled independently from
/// Unity's live rig, including garment correctives and the actual golf clubs.
@MainActor final class VisualOverhaulNativeTests: XCTestCase {
    private struct Golden: Decodable {
        struct Sample: Decodable { let clip: String; let frame: Int; let part: String; let vertices: [[Double]] }
        let samples: [Sample]
    }
    private func floats(_ source: SCNGeometrySource) -> [Float] {
        source.data.withUnsafeBytes { raw in
            (0..<source.vectorCount * source.componentsPerVector).map { k in
                raw.loadUnaligned(fromByteOffset: source.dataOffset + k / source.componentsPerVector * source.dataStride + k % source.componentsPerVector * 4, as: Float.self)
            }
        }
    }
    private func integers(_ source: SCNGeometrySource) throws -> [Int] {
        guard !source.usesFloatComponents, [1,2,4].contains(source.bytesPerComponent) else {
            throw NSError(domain:"VisualGolden",code:1,userInfo:[NSLocalizedDescriptionKey:"Unsupported live SceneKit bone index format"])
        }
        return source.data.withUnsafeBytes { raw in
            (0..<source.vectorCount*source.componentsPerVector).map { k in
                let offset = source.dataOffset + k/source.componentsPerVector*source.dataStride + k%source.componentsPerVector*source.bytesPerComponent
                switch source.bytesPerComponent {
                case 1: return Int(raw.loadUnaligned(fromByteOffset:offset,as:UInt8.self))
                case 2: return Int(raw.loadUnaligned(fromByteOffset:offset,as:UInt16.self))
                default: return Int(raw.loadUnaligned(fromByteOffset:offset,as:UInt32.self))
                }
            }
        }
    }
    /// Adoption is exact, including the distance heroes used in party previews.
    func testOriginalSurfaceAndBlinkContractAcrossAllShippedVariants() throws {
        let faceRoles: Set<String> = ["Reference_Face_Brow", "Reference_Face_Sclera", "Reference_Face_Iris", "Reference_Face_Pupil", "Reference_Face_Limbal", "Reference_Face_Catch"]
        let resources = ["MatchHero_OriginalEye_RelativeIris", "MatchHero_OriginalEye_RelativeSclera", "MatchHero_OriginalSeam_RelativeSkinPigment"]
        for name in resources { XCTAssertNotNil(Bundle.main.url(forResource: name, withExtension: "png"), name) }
        for golf in [false, true] { for female in [false, true] { for distance in golf ? [false] : [false, true] {
            let asset = try XCTUnwrap(MatchHero.asset(female: female, golf: golf, distance: distance))
            let rig = try XCTUnwrap(asset.rig())
            XCTAssertEqual(rig.info.bones.count, golf ? 54 : 53)
            if golf { XCTAssertEqual(rig.info.bones.last, "Club") }
            for role in ["Body", "Face"] {
                let index = try XCTUnwrap(asset.manifest.parts.firstIndex { $0.name == role })
                let part = asset.manifest.parts[index]
                XCTAssertEqual(part.vertexCount, role == "Body" ? (female ? 23230 : 33694) : (female ? 3004 : 3824), "accepted source exact count")
                XCTAssertEqual(asset.geometry[index].sources(for: .texcoord).count, role == "Body" ? 3 : 1)
                let skin = try XCTUnwrap(rig.skinned.first { $0.name == role })
                XCTAssertEqual(skin.morphTargets.map(\.name), ["Hero_Blink_Half", "Hero_Blink"])
                if role == "Body" {
                    let look = try XCTUnwrap(part.submeshes.first?.look)
                    XCTAssertEqual(look.skinFinish, female ? 2 : 1)
                    XCTAssertEqual(look.skinPigmentUV, 2)
                    XCTAssertEqual(look.baseMapLinear, true)
                    XCTAssertEqual(look.baseMap, resources[2])
                } else {
                    XCTAssertEqual(Set(part.submeshes.map(\.material)), faceRoles.union(female ? ["Reference_Face_IrisIn"] : []))
                    XCTAssertTrue(part.submeshes.allSatisfy { $0.look?.anatomicalFace == true })
                }
            }
        } } }
    }
    func testShippedGolfAndTennisRigsMatchUnityGoldenVertices() throws {
        let folder = ProcessInfo.processInfo.environment["VISUAL_GOLDEN_DIR"].map { URL(fileURLWithPath:$0) }
        let bundle = Bundle(for:VisualOverhaulNativeTests.self)
        var evidence: [String] = []
        for golf in [false,true] { for female in [false,true] { for distance in golf ? [false] : [false,true] {
            let name = (golf ? "GolfKitHero_" : "MatchHero_") + (female ? "Female" : "Male") + (distance ? "_Distance" : "")
            let asset = try XCTUnwrap(MatchHero.asset(female:female,golf:golf,distance:distance),name)
            let root = MatchHeroData.buildHero(asset,picks:MatchHeroData.Picks(colour:{ _ in nil }))
            let live = try XCTUnwrap(HeroRig(root:root,asset:asset)); live.attach()
            defer { live.detach() }
            XCTAssertEqual(live.data.boneCount,golf ? 54 : 53,name)
            let fixture = try XCTUnwrap(folder?.appendingPathComponent(name+"_golden.json")
                ?? bundle.url(forResource:name+"_golden",withExtension:"lzfse")
                ?? bundle.url(forResource:name+"_golden",withExtension:"json"),"Independent Unity fixture for "+name)
            let encoded = try Data(contentsOf:fixture)
            let raw = fixture.pathExtension == "lzfse" ? try (encoded as NSData).decompressed(using:.lzfse) as Data : encoded
            let golden = try JSONDecoder().decode(Golden.self,from:raw)
            XCTAssertGreaterThan(golden.samples.count,100,name)
            var checked=0,worst:Float=0
            var decoded: [String:(bind:[Float],targets:[[Float]],weights:[Float],indices:[Int],inverse:[simd_float4x4])] = [:]
            for sample in golden.samples {
                let clip = try XCTUnwrap(live.data.clips[sample.clip])
                guard (0..<clip.frames).contains(sample.frame) else {
                    XCTFail("\(name) \(sample.clip): Unity sample frame \(sample.frame) outside native \(clip.frames) frames"); return
                }
                let time: Double
                if let times = clip.info.times, times.count == clip.frames { time = Double(times[sample.frame]) }
                else { time = Double(sample.frame)*clip.length/Double(max(1,clip.frames-1)) }
                live.apply(clip.pose(at:time,loop:false))
                live.data.applyMorphWeights(clip.morphWeights(at:time,loop:false),to:root)
                let node = try XCTUnwrap(root.childNode(withName:sample.part,recursively:true))
                if decoded[sample.part] == nil {
                    let geometry = try XCTUnwrap(node.geometry)
                    // SceneKit widens UInt8 input indices to UInt16 in a live SCNSkinner.
                    // Decode the graph's declared format once per immutable mesh.
                    decoded[sample.part] = (
                        floats(try XCTUnwrap(geometry.sources(for:.vertex).first)),
                        node.morpher?.targets.map { floats($0.sources(for:.vertex)[0]) } ?? [],
                        node.skinner.map { floats($0.boneWeights) } ?? [],
                        try node.skinner.map { try integers($0.boneIndices) } ?? [],
                        (node.skinner?.boneInverseBindTransforms ?? []).map { simd_float4x4($0.scnMatrix4Value) })
                }
                let data = try XCTUnwrap(decoded[sample.part])
                let bind=data.bind, targets=data.targets, weights=data.weights, indices=data.indices
                let rootInverse=root.simdWorldTransform.inverse
                let matrices=node.skinner.map { skinner in data.inverse.enumerated().map { b,inverse in rootInverse*skinner.bones[b].simdWorldTransform*inverse } } ?? []
                let morphWeights=(0..<targets.count).map { Float(node.morpher?.weight(forTargetAt:$0) ?? 0) }
                let rigidMatrix=rootInverse*node.simdWorldTransform
                for vertex in sample.vertices {
                    let i=Int(vertex[0]); var p=SIMD3<Float>(bind[i*3],bind[i*3+1],bind[i*3+2])
                    for (shape,target) in targets.enumerated() {
                        let weight=morphWeights[shape]
                        // The exported absolute targets are converted to deltas when
                        // RigData loads them, matching SceneKit's additive semantics.
                        p += weight*SIMD3(target[i*3],target[i*3+1],target[i*3+2])
                    }
                    var got:SIMD3<Float>
                    if node.skinner != nil {
                        var sum=SIMD4<Float>(repeating:0)
                        for k in 0..<4 where weights[i*4+k]>0 {
                            let b=Int(indices[i*4+k])
                            let matrix=matrices[b]
                            sum += weights[i*4+k]*(matrix*SIMD4(p,1))
                        }
                        got=SIMD3(sum.x,sum.y,sum.z)
                    } else {
                        let v=rigidMatrix*SIMD4(p,1); got=SIMD3(v.x,v.y,v.z)
                    }
                    let want=SIMD3<Float>(Float(vertex[1]),Float(vertex[2]),Float(vertex[3]))
                    let error=simd_distance(got,want); worst=max(worst,error); checked+=1
                    if error>0.0005 { XCTFail("\(name) \(sample.clip) frame\(sample.frame) \(sample.part) vertex\(i): \(error*1000)mm"); return }
                }
            }
            evidence.append("\(name): \(checked) live-graph vertices, worst \(worst*1000)mm")
        } } }
        let output = folder ?? URL(fileURLWithPath:NSTemporaryDirectory())
        try evidence.joined(separator:"\n").write(to:output.appendingPathComponent("native-live-graph-parity.txt"),atomically:true,encoding:.utf8)
    }
}
