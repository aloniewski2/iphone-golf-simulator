import Metal
import RealityKit

@MainActor
final class NativeGreenRead {
    let entity: ModelEntity
    let sampleCount: Int
    private var material: CustomMaterial
    private var started: Double?

    private struct Vertex {
        var position: SIMD3<Float>
        var normal = SIMD3<Float>(0, 1, 0)
        var flow: SIMD2<Float>
        var curve: SIMD2<Float>
        var parameters: SIMD2<Float>
    }

    init(hole: Hole) throws {
        guard let device = MTLCreateSystemDefaultDevice(), let library = device.makeDefaultLibrary(),
              library.makeFunction(name: "nativeGreenReadMotion") != nil,
              library.makeFunction(name: "nativeGreenReadSurface") != nil else {
            throw NativeAssetError.missing("Native green-read shader library")
        }
        material = try CustomMaterial(surfaceShader: .init(named: "nativeGreenReadSurface", in: library),
            geometryModifier: .init(named: "nativeGreenReadMotion", in: library), lightingModel: .unlit)
        material.blending = .transparent(opacity: .init(floatLiteral: 1))
        material.faceCulling = .none
        material.custom.value = SIMD4(0, 0, Float(GolfUnits.metresPerYard), 0)
        let samples = GreenReadPattern.samples(hole)
        sampleCount = samples.count
        guard !samples.isEmpty else { throw NativeAssetError.invalid("Green has no readable surface samples") }
        var vertices: [Vertex] = [], indices: [UInt32] = []
        for sample in samples {
            let base = UInt32(vertices.count)
            for index in 0...8 {
                let angle = Double(max(0, index - 1)) * 2 * .pi / 8, radius = index == 0 ? 0.0 : 0.09
                let point = CoursePoint(x: sample.point.x + cos(angle) * radius, d: sample.point.d - sin(angle) * radius)
                vertices.append(Vertex(position: GolfUnits.position(point, heightYards: sample.height + 0.025),
                    flow: sample.flow, curve: sample.curve, parameters: SIMD2(sample.speed, Float(sample.slope))))
            }
            for index in 0..<8 { indices += [base, base + UInt32(index + 1), base + UInt32((index + 1) % 8 + 1)] }
        }
        let descriptor = LowLevelMesh.Descriptor(vertexCapacity: vertices.count, vertexAttributes: [
            .init(semantic: .position, format: .float3, offset: MemoryLayout<Vertex>.offset(of: \.position)!),
            .init(semantic: .normal, format: .float3, offset: MemoryLayout<Vertex>.offset(of: \.normal)!),
            .init(semantic: .uv0, format: .float2, offset: MemoryLayout<Vertex>.offset(of: \.flow)!),
            .init(semantic: .uv1, format: .float2, offset: MemoryLayout<Vertex>.offset(of: \.curve)!),
            .init(semantic: .uv2, format: .float2, offset: MemoryLayout<Vertex>.offset(of: \.parameters)!)
        ], vertexLayouts: [.init(bufferIndex: 0, bufferStride: MemoryLayout<Vertex>.stride)], indexCapacity: indices.count)
        let mesh = try LowLevelMesh(descriptor: descriptor)
        mesh.withUnsafeMutableBytes(bufferIndex: 0) { buffer in vertices.withUnsafeBytes { buffer.copyMemory(from: $0) } }
        mesh.withUnsafeMutableIndices { buffer in indices.withUnsafeBytes { buffer.copyMemory(from: $0) } }
        let minimum = vertices.reduce(SIMD3<Float>(repeating: .infinity)) { simd_min($0, $1.position) }
        let maximum = vertices.reduce(SIMD3<Float>(repeating: -.infinity)) { simd_max($0, $1.position) }
        // Account for the complete one-yard shader displacement when culling.
        let margin = SIMD3<Float>(repeating: Float(GolfUnits.metresPerYard) * 1.1)
        mesh.parts.replaceAll([.init(indexCount: indices.count, bounds: .init(min: minimum - margin, max: maximum + margin))])
        entity = ModelEntity(mesh: try MeshResource(from: mesh), materials: [material])
        entity.name = "greenReadGrid"
        entity.components.set(GroundingShadowComponent(castsShadow: false))
        entity.isEnabled = false
    }

    func update(time: Double, visible: Bool, reduceMotion: Bool) {
        entity.isEnabled = visible
        guard visible else { return }
        if started == nil { started = time }
        material.custom.value = SIMD4(Float(max(0, time - started!)), reduceMotion ? 0 : 1, Float(GolfUnits.metresPerYard), 0)
        entity.model?.materials = [material]
    }
}
