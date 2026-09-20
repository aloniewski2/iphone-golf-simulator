import SceneKit
import UIKit
import simd

/// A welded, closed surface, bound to a skeleton. Capsules are only construction fields:
/// no cylinders, joint balls, or disconnected limb meshes are rendered.
@MainActor
final class GolferSkin {
    struct Link {
        let from: BodyJoint
        let to: BodyJoint
        let radius: Float
        let material: Int
    }
    static let links: [Link] = [
        .init(from: .root, to: .neck, radius: 0.52, material: 0),
        .init(from: .neck, to: .nose, radius: 0.20, material: 2),
        .init(from: .leftShoulder, to: .leftElbow, radius: 0.23, material: 0),
        .init(from: .leftElbow, to: .leftWrist, radius: 0.16, material: 2),
        .init(from: .rightShoulder, to: .rightElbow, radius: 0.23, material: 0),
        .init(from: .rightElbow, to: .rightWrist, radius: 0.16, material: 2),
        .init(from: .leftHip, to: .leftKnee, radius: 0.30, material: 1),
        .init(from: .leftKnee, to: .leftAnkle, radius: 0.235, material: 1),
        .init(from: .rightHip, to: .rightKnee, radius: 0.30, material: 1),
        .init(from: .rightKnee, to: .rightAnkle, radius: 0.235, material: 1),
        .init(from: .leftHip, to: .rightHip, radius: 0.39, material: 1),
        .init(from: .leftShoulder, to: .rightShoulder, radius: 0.32, material: 0)
    ]
    static let rest: BodyPose3D = {
        var pose = AvatarAnimations.address
        for (shoulder, elbow, wrist, sign) in [
            (BodyJoint.leftShoulder, BodyJoint.leftElbow, BodyJoint.leftWrist, Float(-1)),
            (.rightShoulder, .rightElbow, .rightWrist, Float(1))
        ] {
            pose.joints[elbow] = pose[shoulder] + simd_float3(0, -0.15, sign * AvatarSize.upperArm)
            pose.joints[wrist] = pose[elbow] + simd_float3(0, -0.1, sign * AvatarSize.forearm)
        }
        return pose
    }()

    struct Mesh {
        var vertices: [simd_float3] = []
        var normals: [simd_float3] = []
        var triangles: [[Int32]] = [[], [], []]
        var weights: [Float] = []
        var bones: [UInt16] = []
    }
    static let mesh = makeMesh()
    let node: SCNNode
    private let skeleton = SCNNode()
    private var bones: [SCNNode] = []

    init(shirt: UIColor, trousers: UIColor, skin: UIColor) {
        let mesh = Self.mesh
        // Material regions are attached to the rest mesh, not global-height vertex colors.
        // A height-only neck mask exposed the upper back when the golfer bent forward.
        let geometry = SCNGeometry(sources: [
            SCNGeometrySource(vertices: mesh.vertices.map(SCNVector3.init)),
            SCNGeometrySource(normals: mesh.normals.map(SCNVector3.init))
        ], elements: mesh.triangles.map { SCNGeometryElement(indices: $0, primitiveType: .triangles) })
        geometry.materials = [shirt, trousers, skin].enumerated().map { index, color in
            let material = SCNMaterial()
            material.name = ["Polo fabric", "Tailored trousers", "Skin"][index]
            material.lightingModel = .physicallyBased
            material.diffuse.contents = color
            material.roughness.contents = index == 2 ? 0.66 : 0.9
            return material
        }
        node = SCNNode(geometry: geometry)
        node.name = "continuousGolferSkin"
        node.addChildNode(skeleton)
        var inverseBinds: [NSValue] = []
        for link in Self.links {
            let bone = SCNNode()
            bone.simdTransform = Self.transform(link, pose: Self.rest)
            skeleton.addChildNode(bone)
            bones.append(bone)
            inverseBinds.append(NSValue(scnMatrix4: SCNMatrix4(simd_inverse(bone.simdTransform))))
        }
        let weightData = mesh.weights.withUnsafeBytes { Data($0) }
        let indexData = mesh.bones.withUnsafeBytes { Data($0) }
        let weights = SCNGeometrySource(data: weightData, semantic: .boneWeights,
            vectorCount: mesh.vertices.count, usesFloatComponents: true, componentsPerVector: 4,
            bytesPerComponent: 4, dataOffset: 0, dataStride: 16)
        let indices = SCNGeometrySource(data: indexData, semantic: .boneIndices,
            vectorCount: mesh.vertices.count, usesFloatComponents: false, componentsPerVector: 4,
            bytesPerComponent: 2, dataOffset: 0, dataStride: 8)
        let skinner = SCNSkinner(baseGeometry: geometry, bones: bones,
            boneInverseBindTransforms: inverseBinds, boneWeights: weights, boneIndices: indices)
        skinner.skeleton = skeleton
        node.skinner = skinner
    }

    func apply(_ pose: BodyPose3D) {
        for (index, link) in Self.links.enumerated() { bones[index].simdTransform = Self.transform(link, pose: pose) }
    }

    static func transform(_ link: Link, pose: BodyPose3D) -> simd_float4x4 {
        let a = pose[link.from], delta = pose[link.to] - a
        let length = max(0.001, simd_length(delta))
        let direction = simd_length(delta) > 0.001 ? delta / length : simd_float3(0, 1, 0)
        var matrix = simd_float4x4(simd_quatf(from: simd_float3(0, 1, 0), to: direction))
        // A bone direction alone cannot express rotation ABOUT that bone. In particular,
        // a vertical spine used to leave the shirt facing forward while shoulders turned.
        // Bind and pose in the same anatomical frame so the continuous torso turns too.
        if link.from == .root || link.from == .neck {
            let across = pose[.rightShoulder] - pose[.leftShoulder]
            let projected = across - simd_dot(across, direction) * direction
            if simd_length_squared(projected) > 0.0001 {
                let z = simd_normalize(projected)
                let x = simd_normalize(simd_cross(direction, z))
                matrix = simd_float4x4(columns: (simd_float4(x, 0), simd_float4(direction, 0),
                    simd_float4(simd_cross(x, direction), 0), simd_float4(0, 0, 0, 1)))
            }
        }
        // Carry each arm's bend plane into its skinning frame. A shortest-arc
        // quaternion only aligns the bone axis and rolls unpredictably when the
        // arm passes overhead; the elbow then looks twisted despite valid joints.
        let arm: (BodyJoint, BodyJoint, BodyJoint, Float)?
        switch link.from {
        case .leftShoulder, .leftElbow: arm = (.leftShoulder,.leftElbow,.leftWrist,1)
        case .rightShoulder, .rightElbow: arm = (.rightShoulder,.rightElbow,.rightWrist,-1)
        default: arm = nil
        }
        if let (shoulder,elbow,wrist,side) = arm {
            let normal = simd_cross(pose[elbow]-pose[shoulder],pose[wrist]-pose[elbow]) * side
            if simd_length_squared(normal) > 0.00001 {
                let x = simd_normalize(normal)
                let z = simd_normalize(simd_cross(x,direction))
                matrix = simd_float4x4(columns:(simd_float4(simd_cross(direction,z),0),
                    simd_float4(direction,0),simd_float4(z,0),simd_float4(0,0,0,1)))
            }
        }
        matrix.columns.1 *= length
        matrix.columns.3 = simd_float4(a, 1)
        return matrix
    }

    private static func distances(_ p: simd_float3) -> [Float] {
        links.enumerated().map { index, link in
            let a = rest[link.from], b = rest[link.to], d = b - a
            let t = max(0, min(1, simd_dot(p - a, d) / max(0.001, simd_length_squared(d))))
            var v = p - (a + d * t)
            if index == 0 {
                v.z *= 0.70 // broader chest, with a slight taper at the waist
                v.x *= 1.08
            }
            return simd_length(v) - link.radius
        }
    }

    private static func field(_ p: simd_float3) -> Float {
        distances(p).reduce(Float(100)) { a, b in
            let k: Float = 0.16
            let h = max(k - abs(a - b), 0) / k
            return min(a, b) - h * h * k * 0.25
        }
    }

    private static func makeMesh() -> Mesh {
        let step: Float = 0.085
        let minimum = simd_float3(-0.9, -0.4, -3.9)
        let nx = 36, ny = 73, nz = 94
        func id(_ x: Int, _ y: Int, _ z: Int) -> Int { (x * ny + y) * nz + z }
        var points: [simd_float3] = [], values: [Float] = []
        for x in 0..<nx { for y in 0..<ny { for z in 0..<nz {
            let p = minimum + simd_float3(Float(x), Float(y), Float(z)) * step
            points.append(p); values.append(field(p))
        } } }
        var mesh = Mesh()
        var edgeVertices: [UInt64: Int32] = [:]
        func appendVertex(_ p: simd_float3) -> Int32 {
            let e: Float = 0.003
            let n = simd_float3(field(p + simd_float3(e, 0, 0)) - field(p - simd_float3(e, 0, 0)),
                                field(p + simd_float3(0, e, 0)) - field(p - simd_float3(0, e, 0)),
                                field(p + simd_float3(0, 0, e)) - field(p - simd_float3(0, 0, e)))
            let index = Int32(mesh.vertices.count)
            mesh.vertices.append(p)
            mesh.normals.append(simd_length(n) > 0.00001 ? simd_normalize(n) : simd_float3(0, 1, 0))
            let distances = distances(p)
            let nearest = distances.indices.sorted { distances[$0] < distances[$1] }.prefix(4)
            let floor = distances[nearest.first!]
            let weights = nearest.map { exp(-(distances[$0] - floor) * 18) }
            let total = weights.reduce(0, +)
            mesh.weights += weights.map { $0 / total }
            mesh.bones += nearest.map { UInt16($0) }
            return index
        }
        func vertex(_ a: Int, _ b: Int) -> Int32 {
            let key = UInt64(min(a, b)) << 32 | UInt64(max(a, b))
            if let existing = edgeVertices[key] { return existing }
            let t = values[a] / (values[a] - values[b])
            let index = appendVertex(points[a] + (points[b] - points[a]) * t)
            edgeVertices[key] = index
            return index
        }
        // Cut shared triangles exactly at clothing seams. Merely classifying each
        // triangle's midpoint produced a sawtooth sleeve and waistband silhouette.
        let sleeveZ = AvatarSize.shoulderHalfWidth + AvatarSize.upperArm * 0.56
        let seams: [(normal: simd_float3, offset: Float, material: Int)] = [
            (simd_float3(0,-1,0), -(AvatarSize.hipHeight + 0.12), 1),
            (simd_float3(0,0,1), sleeveZ, 2),
            (simd_float3(0,0,-1), sleeveZ, 2),
            (simd_float3(0,1,0), rest[.neck].y + 0.42, 2)
        ]
        var seamVertices = Array(repeating: [UInt64: Int32](), count: seams.count)
        func emit(_ polygon: [Int32], material: Int) {
            guard polygon.count >= 3 else { return }
            for i in 1..<polygon.count-1 { mesh.triangles[material] += [polygon[0], polygon[i], polygon[i+1]] }
        }
        func dress(_ polygon: [Int32], seam: Int) {
            guard polygon.count >= 3 else { return }
            guard seam < seams.count else { emit(polygon, material: 0); return }
            let plane = seams[seam]
            var positive: [Int32] = [], negative: [Int32] = []
            for i in polygon.indices {
                let a = polygon[i], b = polygon[(i+1) % polygon.count]
                let pa = mesh.vertices[Int(a)], pb = mesh.vertices[Int(b)]
                let da = simd_dot(pa, plane.normal) - plane.offset
                let db = simd_dot(pb, plane.normal) - plane.offset
                if da >= 0 { positive.append(a) }
                if da <= 0 { negative.append(a) }
                if (da < 0 && db > 0) || (da > 0 && db < 0) {
                    let key = UInt64(min(a,b)) << 32 | UInt64(max(a,b))
                    let cut: Int32
                    if let cached = seamVertices[seam][key] { cut = cached }
                    else {
                        cut = appendVertex(pa + (pb-pa) * (da / (da-db)))
                        seamVertices[seam][key] = cut
                    }
                    positive.append(cut); negative.append(cut)
                }
            }
            emit(positive, material: plane.material)
            dress(negative, seam: seam + 1)
        }
        func triangle(_ a: Int32, _ b: Int32, _ c: Int32) {
            let p = mesh.vertices[Int(a)], q = mesh.vertices[Int(b)], r = mesh.vertices[Int(c)]
            let outward = mesh.normals[Int(a)] + mesh.normals[Int(b)] + mesh.normals[Int(c)]
            let winding: [Int32] = simd_dot(simd_cross(q - p, r - p), outward) >= 0 ? [a,b,c] : [a,c,b]
            dress(winding, seam: 0)
        }
        // Consistent body-diagonal tetrahedra share all boundary edges between cells.
        let tetrahedra = [[0,5,1,6], [0,1,2,6], [0,2,3,6], [0,3,7,6], [0,7,4,6], [0,4,5,6]]
        for x in 0..<nx-1 { for y in 0..<ny-1 { for z in 0..<nz-1 {
            let cube = [id(x,y,z), id(x+1,y,z), id(x+1,y+1,z), id(x,y+1,z),
                        id(x,y,z+1), id(x+1,y,z+1), id(x+1,y+1,z+1), id(x,y+1,z+1)]
            guard cube.contains(where: { values[$0] < 0 }), cube.contains(where: { values[$0] >= 0 }) else { continue }
            for tetrahedron in tetrahedra {
                let inside = tetrahedron.map { cube[$0] }.filter { values[$0] < 0 }
                let outside = tetrahedron.map { cube[$0] }.filter { values[$0] >= 0 }
                if inside.count == 1 {
                    triangle(vertex(inside[0],outside[0]), vertex(inside[0],outside[1]), vertex(inside[0],outside[2]))
                } else if inside.count == 3 {
                    triangle(vertex(outside[0],inside[0]), vertex(outside[0],inside[1]), vertex(outside[0],inside[2]))
                } else if inside.count == 2 {
                    let a = vertex(inside[0],outside[0]), b = vertex(inside[0],outside[1])
                    let c = vertex(inside[1],outside[0]), d = vertex(inside[1],outside[1])
                    triangle(a,b,c); triangle(b,d,c)
                }
            }
        } } }
        return mesh
    }
}
