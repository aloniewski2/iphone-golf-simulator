import Foundation
import SceneKit
import simd

/// The four actual golf meshes, exported from blender/scripts/golf_clubs.py.
/// Metres; origin is the original grip socket. Butt +Y, shaft down -Y, toe +X,
/// striking face +Z. Keep this root at the hand socket before applying a display pose.
@MainActor enum NativeGolfEquipment {
    struct Finish: Decodable {
        let name: String, color: [Float], metallic: Float, roughness: Float
        let indices: [UInt32]
    }
    struct Asset: Decodable {
        let id: String, positions: [Float], normals: [Float], materials: [Finish]
        let soleY: Float, loftDegrees: Float
    }
    static var locate: (_ name: String, _ ext: String) -> URL? = { name, ext in
        Bundle.main.url(forResource: name, withExtension: ext)
    }
    private static var assets: [String: Asset] = [:]
    static func asset(_ id: String) -> Asset? {
        let key = ["Driver", "Iron", "Wedge", "Putter"].first { $0.caseInsensitiveCompare(id) == .orderedSame }
        guard let key else { return nil }
        if let a = assets[key] { return a }
        guard let u = locate("NativeGolfEquipment_" + key, "json"), let d = try? Data(contentsOf: u),
              let a = try? JSONDecoder().decode(Asset.self, from: d),
              a.positions.count == a.normals.count, a.positions.count % 3 == 0 else { return nil }
        assets[key] = a; return a
    }
    /// Returns a fresh independently tintable node. A reward tint preserves the authored
    /// roughness/metal identity; rubber, scorelines and white alignment marks stay legible.
    static func make(_ id: String, tint: SIMD3<Float>? = nil) -> SCNNode? {
        guard let a = asset(id) else { return nil }
        func source(_ values: [Float], _ semantic: SCNGeometrySource.Semantic) -> SCNGeometrySource {
            let data = values.withUnsafeBytes { Data($0) }
            return SCNGeometrySource(data: data, semantic: semantic, vectorCount: values.count / 3,
                usesFloatComponents: true, componentsPerVector: 3, bytesPerComponent: 4, dataOffset: 0, dataStride: 12)
        }
        let count = a.positions.count / 3
        var elements: [SCNGeometryElement] = [], materials: [SCNMaterial] = []
        for finish in a.materials {
            guard finish.indices.count % 3 == 0, finish.indices.allSatisfy({ Int($0) < count }), finish.color.count == 3 else { return nil }
            let data = finish.indices.withUnsafeBytes { Data($0) }
            elements.append(SCNGeometryElement(data: data, primitiveType: .triangles,
                primitiveCount: finish.indices.count / 3, bytesPerIndex: 4))
            let m = SCNMaterial(); m.name = "CLUB " + finish.name; m.lightingModel = .physicallyBased
            let authored = SIMD3<Float>(finish.color[0], finish.color[1], finish.color[2])
            let c = ["grip", "groove", "white"].contains(finish.name) ? authored : (tint ?? authored)
            m.diffuse.contents = CGColor(red: CGFloat(c.x), green: CGFloat(c.y), blue: CGFloat(c.z), alpha: 1)
            m.metalness.contents = finish.metallic; m.roughness.contents = finish.roughness
            m.isDoubleSided = false; materials.append(m)
        }
        let geometry = SCNGeometry(sources: [source(a.positions, .vertex), source(a.normals, .normal)], elements: elements)
        geometry.materials = materials
        let node = SCNNode(geometry: geometry); node.name = "nativeGolfEquipment_" + a.id
        node.setValue(a.soleY, forKey: "clubSoleY"); node.setValue(a.loftDegrees, forKey: "clubLoftDegrees")
        return node
    }
}
