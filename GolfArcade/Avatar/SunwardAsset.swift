import SceneKit
import UIKit
import simd

/// Offline-authored assets. Files are validated before constructing any SceneKit objects.
@MainActor
enum SunwardAsset {
    struct Material: Decodable {
        let name: String; let color: [Float]; let roughness: Float; let metalness: Float; let texture: String?
        let alphaCutoff: Float?; let doubleSided: Bool?
    }
    struct Bone: Decodable { let name: String; let parent: Int }
    struct Mesh: Decodable {
        let positions: [Float], normals: [Float], uv: [Float], weights: [Float]
        let joints: [UInt16], indices: [Int32]
        let material: Int
        let colors: [Float]?
    }
    struct Asset: Decodable {
        let version: Int
        let coordinateSpace: String
        let bones: [Bone], boneLinks: [Int], inverseBinds: [Float]
        let materials: [Material], meshes: [Mesh]
        @MainActor var valid: Bool {
            guard version == 2, coordinateSpace == "sunward-rig", !meshes.isEmpty,
                  bones.count == boneLinks.count, inverseBinds.count == bones.count * 16,
                  inverseBinds.allSatisfy(\.isFinite), boneLinks.allSatisfy({ GolferSkin.links.indices.contains($0) }),
                  materials.allSatisfy({ $0.color.count == 4 && $0.color.allSatisfy(\.isFinite) && $0.roughness.isFinite && $0.metalness.isFinite && ($0.alphaCutoff.map { $0.isFinite && $0 >= 0 && $0 <= 1 } ?? true) }) else { return false }
            return meshes.allSatisfy { m in
                let count = m.positions.count / 3
                guard count > 0, count < 100_000, m.positions.count == count * 3, m.normals.count == count * 3,
                      m.uv.count == count * 2, m.positions.allSatisfy(\.isFinite), m.normals.allSatisfy(\.isFinite),
                      m.uv.allSatisfy(\.isFinite), materials.indices.contains(m.material), m.indices.count.isMultiple(of: 3),
                      m.indices.allSatisfy({ $0 >= 0 && $0 < count }) else { return false }
                if let colors=m.colors, colors.count != count*4 || !colors.allSatisfy(\.isFinite) { return false }
                if bones.isEmpty { return m.weights.isEmpty && m.joints.isEmpty }
                guard m.weights.count == count * 4, m.joints.count == count * 4,
                      m.weights.allSatisfy({ $0.isFinite && $0 >= 0 }), m.joints.allSatisfy({ $0 < bones.count }) else { return false }
                return stride(from: 0, to: m.weights.count, by: 4).allSatisfy { abs(m.weights[$0..<$0+4].reduce(0,+)-1) < 0.001 }
            }
        }
    }
    private static var cache: [String: Asset] = [:]
    static func load(_ name: String) -> Asset? {
        if let cached = cache[name] { return cached }
        guard let url = Bundle.main.url(forResource: name, withExtension: "golfmesh") ?? Bundle.main.url(forResource:name,withExtension:"golfmesh",subdirectory:"Nature"),
              let data = try? Data(contentsOf: url), let asset = try? JSONDecoder().decode(Asset.self, from: data), asset.valid else { return nil }
        cache[name] = asset
        return asset
    }
    static func material(_ definition: Material, appearance: GolferAppearance? = nil) -> SCNMaterial {
        let material = SCNMaterial(); material.name = definition.name; material.lightingModel = .physicallyBased
        let c = definition.color
        var color = UIColor(red: CGFloat(c[0]), green: CGFloat(c[1]), blue: CGFloat(c[2]), alpha: CGFloat(c[3]))
        if let appearance {
            switch definition.name {
            case "shirt": color = appearance.shirtColor
            case "cuff": color = appearance.shirtColor.withMultipliedRGB(0.72)
            case "trousers": color = appearance.trousersColor
            case "skin": color = appearance.skinColor
            case "ivory": color = appearance.accentColor
            case "hair": color = appearance.hairColor
            default: break
            }
        }
        material.diffuse.contents = color
        if let texture = definition.texture, texture == (texture as NSString).lastPathComponent {
            if let image = UIImage(named: texture) ?? UIImage(named:"Nature/"+texture) { material.diffuse.contents = image }
            else { material.diffuse.contents = color }
            material.multiply.contents = color
        }
        material.roughness.contents = definition.roughness; material.metalness.contents = definition.metalness
        material.isDoubleSided=definition.doubleSided ?? false
        if let cutoff=definition.alphaCutoff {
            // Opaque cutouts write depth and cast leaf-shaped shadows; no sorted
            // transparent canopy layers. The source's alpha mask stays intact.
            material.blendMode = .replace
            material.writesToDepthBuffer=true
            material.shaderModifiers=[.surface:"""
            #pragma body
            if (_surface.diffuse.a < \(cutoff)) { discard_fragment(); }
            _surface.diffuse.a = 1.0;
            """]
        }
        return material
    }
    static func geometry(_ m: Mesh, material: SCNMaterial) -> SCNGeometry {
        let points = stride(from: 0, to: m.positions.count, by: 3).map { SCNVector3(m.positions[$0],m.positions[$0+1],m.positions[$0+2]) }
        let normals = stride(from: 0, to: m.normals.count, by: 3).map { SCNVector3(m.normals[$0],m.normals[$0+1],m.normals[$0+2]) }
        let uv = stride(from: 0, to: m.uv.count, by: 2).map { CGPoint(x: Double(m.uv[$0]),y: Double(1-m.uv[$0+1])) }
        var sources:[SCNGeometrySource]=[.init(vertices:points),.init(normals:normals),.init(textureCoordinates:uv)]
        if let colors=m.colors { sources.append(SCNGeometrySource(data:colors.withUnsafeBytes{Data($0)},semantic:.color,vectorCount:points.count,usesFloatComponents:true,componentsPerVector:4,bytesPerComponent:4,dataOffset:0,dataStride:16)) }
        let geometry = SCNGeometry(sources: sources,
            elements:[SCNGeometryElement(indices:m.indices,primitiveType:.triangles)])
        geometry.materials=[material]; return geometry
    }
    static func prop(_ name: String) -> SCNNode? {
        guard let asset=load(name), asset.bones.isEmpty else { return nil }
        let node=SCNNode(); node.name=name
        let materials=asset.materials.map { material($0) }
        for m in asset.meshes { node.addChildNode(SCNNode(geometry:geometry(m,material:materials[m.material]))) }
        return node
    }
}

@MainActor
final class SunwardGolferSkin {
    let node=SCNNode()
    private let asset: SunwardAsset.Asset
    private var bones: [SCNNode]=[]
    private var corrections: [simd_float4x4]=[]
    init?(appearance: GolferAppearance) {
        guard let asset=SunwardAsset.load("SunwardGolfer"), !asset.bones.isEmpty else { return nil }
        self.asset=asset; node.name="sunwardAuthoredGolfer"
        var inverseBinds:[NSValue]=[]
        for index in asset.bones.indices {
            let v=asset.inverseBinds
            func column(_ c:Int)->simd_float4 { let p=index*16+c*4; return simd_float4(v[p],v[p+1],v[p+2],v[p+3]) }
            let inverseBind=simd_float4x4(columns:(column(0),column(1),column(2),column(3)))
            let bind=simd_inverse(inverseBind)
            let link=GolferSkin.links[asset.boneLinks[index]]
            corrections.append(simd_inverse(GolferSkin.transform(link,pose:GolferSkin.rest))*bind)
            let bone=SCNNode(); bone.name=asset.bones[index].name; bone.simdTransform=bind
            node.addChildNode(bone); bones.append(bone); inverseBinds.append(NSValue(scnMatrix4:SCNMatrix4(inverseBind)))
        }
        let materials=asset.materials.map { SunwardAsset.material($0,appearance:appearance) }
        for m in asset.meshes {
            let geometry=SunwardAsset.geometry(m,material:materials[m.material]); let count=m.positions.count/3
            let weights=SCNGeometrySource(data:m.weights.withUnsafeBytes{Data($0)},semantic:.boneWeights,vectorCount:count,usesFloatComponents:true,componentsPerVector:4,bytesPerComponent:4,dataOffset:0,dataStride:16)
            let indices=SCNGeometrySource(data:m.joints.withUnsafeBytes{Data($0)},semantic:.boneIndices,vectorCount:count,usesFloatComponents:false,componentsPerVector:4,bytesPerComponent:2,dataOffset:0,dataStride:8)
            let part=SCNNode(geometry:geometry)
            part.skinner=SCNSkinner(baseGeometry:geometry,bones:bones,boneInverseBindTransforms:inverseBinds,boneWeights:weights,boneIndices:indices)
            part.skinner?.skeleton=node; node.addChildNode(part)
        }
    }
    func apply(_ pose: BodyPose3D) {
        for index in bones.indices { bones[index].simdTransform=GolferSkin.transform(GolferSkin.links[asset.boneLinks[index]],pose:pose)*corrections[index] }
    }
    #if DEBUG
    var nativeExportBones: [simd_float4x4] { bones.map(\.simdWorldTransform) }
    #endif
}

private extension UIColor {
    func withMultipliedRGB(_ multiplier: CGFloat) -> UIColor {
        var r:CGFloat=0,g:CGFloat=0,b:CGFloat=0,a:CGFloat=0; getRed(&r,green:&g,blue:&b,alpha:&a)
        return UIColor(red:r*multiplier,green:g*multiplier,blue:b*multiplier,alpha:a)
    }
}
