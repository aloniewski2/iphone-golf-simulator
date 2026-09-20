import Foundation
import Metal
import RealityKit
import CoreGraphics
import ImageIO

struct NativeWaterComponent: Component {
    var materialIndices: [Int]
    var reflection: NativeLagoonUniforms? = nil
}

/// Two aligned float4s, mirrored by NativeLagoonUniforms in NativeWaterSurface.metal.
/// No pointer/texture resources in the custom argument buffer: the atlas uses baseColor.
struct NativeLagoonUniforms: Equatable, Sendable {
    var center: SIMD4<Float>
    var extent: SIMD4<Float>

    static func capture(for hole: Hole) -> Self? {
        guard Course.sunwardResort.holes.contains(hole),
              let lake = hole.hazards.first(where: { $0.kind == .water }),
              let level = hole.waterElevations[lake.id] else { return nil }
        return Self(center: SIMD4(Float(lake.x), Float(level + 0.05), Float(-lake.distance), 1),
                    extent: SIMD4(Float(lake.width / 2 + 10), 30, Float(lake.length / 2 + 10), 0))
    }

    /// Preserve the explicit +X,-X,+Y,-Y,+Z,-Z capture order and pixel orientation.
    static func atlas(faceURLs: [URL]) throws -> CGImage {
        guard faceURLs.count == 6,
              let context = CGContext(data: nil, width: 1536, height: 256, bitsPerComponent: 8,
                bytesPerRow: 1536 * 4, space: CGColorSpace(name: CGColorSpace.sRGB)!,
                bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue) else {
            throw NativeAssetError.invalid("Lagoon reflection requires six 256px faces")
        }
        for (index, url) in faceURLs.enumerated() {
            try Task.checkCancellation()
            guard let source = CGImageSourceCreateWithURL(url as CFURL, nil),
                  let image = CGImageSourceCreateImageAtIndex(source, 0, nil),
                  image.width == 256, image.height == 256 else {
                throw NativeAssetError.invalid("Invalid lagoon reflection face: \(url.lastPathComponent)")
            }
            context.draw(image, in: CGRect(x: index * 256, y: 0, width: 256, height: 256))
        }
        guard let image = context.makeImage() else { throw NativeAssetError.invalid("Cannot assemble lagoon atlas") }
        return image
    }
}

/// A bounded, data-only shoreline field. Generated off the render actor at load
/// time; the scored water plane and the authored mesh are never displaced.
struct NativeWaterDepth: Sendable {
    let size: Int
    let origin: SIMD2<Float>
    let span: Float
    let pixels: [UInt8]

    static func shallow(at point: CoursePoint, lakes: [CourseHazard]) -> Float {
        guard let lake = lakes.min(by: {
            abs($0.radialSurface(at: point).radius - 1) < abs($1.radialSurface(at: point).radius - 1)
        }) else { return 0 }
        let distance = (1 - lake.radialSurface(at: point).radius) * min(lake.width, lake.length) / 2
        return Float(exp(-max(0, distance) / 4))
    }

    static func make(hole: Hole) throws -> Self? {
        let lakes = hole.hazards.filter { $0.kind == .water }
        guard !lakes.isEmpty else { return nil }
        var minimum = SIMD2<Double>(repeating: .infinity), maximum = -minimum
        for lake in lakes {
            let center = SIMD2(lake.x, -lake.distance)
            let extent = SIMD2(lake.width, lake.length) * (0.5 * (1 + abs(lake.contour) * 1.4)) + 2
            minimum = simd_min(minimum, center - extent); maximum = simd_max(maximum, center + extent)
        }
        let span = max(maximum.x - minimum.x, maximum.y - minimum.y)
        let size = min(1024, max(64, Int(ceil(span / 0.5))))
        var pixels = [UInt8](); pixels.reserveCapacity(size * size)
        for row in 0..<size {
            try Task.checkCancellation()
            for column in 0..<size {
                let p = minimum + SIMD2(Double(column) + 0.5, Double(row) + 0.5) * (span / Double(size))
                pixels.append(UInt8((shallow(at: .init(x: p.x, d: -p.y), lakes: lakes) * 255).rounded()))
            }
        }
        return Self(size: size, origin: SIMD2<Float>(minimum), span: Float(span), pixels: pixels)
    }
}

@MainActor
enum NativeWaterSurface {
    static let shaderName = "nativeWaterSurface"
    private static let registration: Void = NativeWaterComponent.registerComponent()

    static func install(on root: Entity, hole: Hole) async throws {
        _ = registration
        var targets: [(Entity, Int)] = []
        func visit(_ entity: Entity) {
            if let model = entity.components[ModelComponent.self] {
                for (index, material) in model.materials.enumerated() {
                    guard let pbr = material as? PhysicallyBasedMaterial else { continue }
                    let name = (pbr.name ?? "").replacingOccurrences(of: " ", with: "_")
                    guard name.contains("Sunward_rippled_lagoon") || name.contains("Sunward_local_lagoon_reflection") else { continue }
                    targets.append((entity, index))
                }
            }
            for child in entity.children { visit(child) }
        }
        visit(root)
        guard !targets.isEmpty else { return }
        let worker = Task.detached(priority: .utility) { try NativeWaterDepth.make(hole: hole) }
        let field = try await withTaskCancellationHandler { try await worker.value } onCancel: { worker.cancel() }
        try Task.checkCancellation()
        guard let field else { throw NativeAssetError.invalid("Water mesh has no authoritative shoreline") }
        guard let device = MTLCreateSystemDefaultDevice(), let library = device.makeDefaultLibrary(),
              library.makeFunction(name: "nativeWaterSurface") != nil else {
            throw NativeAssetError.missing("Native water shader library")
        }
        guard let provider = CGDataProvider(data: Data(field.pixels) as CFData),
              let image = CGImage(width: field.size, height: field.size, bitsPerComponent: 8, bitsPerPixel: 8,
                bytesPerRow: field.size, space: CGColorSpaceCreateDeviceGray(), bitmapInfo: [], provider: provider,
                decode: nil, shouldInterpolate: true, intent: .defaultIntent) else {
            throw NativeAssetError.invalid("Cannot create shoreline texture")
        }
        let texture = try await TextureResource(image: image, options: .init(semantic: .raw, mipmapsMode: .none))
        try Task.checkCancellation()
        let reflection = NativeLagoonUniforms.capture(for: hole)
        var atlasTexture: TextureResource?
        if reflection != nil {
            let urls = try (0..<6).map { index -> URL in
                guard let url = Bundle.main.url(forResource: "hole-\(hole.number)-face-\(index)",
                    withExtension: "png", subdirectory: "Reflections") else {
                    throw NativeAssetError.missing("Lagoon reflection hole \(hole.number), face \(index)")
                }
                return url
            }
            let atlasWorker = Task.detached(priority: .utility) { try NativeLagoonUniforms.atlas(faceURLs: urls) }
            let atlas = try await withTaskCancellationHandler { try await atlasWorker.value } onCancel: { atlasWorker.cancel() }
            try Task.checkCancellation()
            atlasTexture = try await TextureResource(image: atlas, options: .init(semantic: .color, mipmapsMode: .none))
        }
        try Task.checkCancellation()
        var material = try CustomMaterial(surfaceShader: .init(named: reflection == nil ? shaderName : "nativeLagoonSurface", in: library), lightingModel: .lit)
        material.faceCulling = .none
        material.custom.texture = .init(texture)
        material.custom.value = SIMD4(0, field.origin.x, field.origin.y, 1 / field.span)
        if let reflection, let atlasTexture {
            material.baseColor.texture = .init(atlasTexture)
            material.withMutableUniforms(ofType: NativeLagoonUniforms.self, stage: .surfaceShader) { values, _ in
                values = reflection
            }
        }
        for (entity, index) in targets {
            var model = entity.components[ModelComponent.self]!
            model.materials[index] = material
            entity.components.set(model)
            var state = entity.components[NativeWaterComponent.self] ?? NativeWaterComponent(materialIndices: [])
            state.materialIndices.append(index)
            state.reflection = reflection
            entity.components.set(state)
        }
    }

    static func models(in root: Entity) -> [Entity] {
        var result: [Entity] = []
        func visit(_ entity: Entity) {
            if entity.components.has(NativeWaterComponent.self) { result.append(entity) }
            for child in entity.children { visit(child) }
        }
        visit(root)
        return result
    }

    static func update(_ entities: [Entity], time: Double, reduceMotion: Bool) {
        let phase: Float = reduceMotion || !time.isFinite ? 0 : Float(time)
        for entity in entities {
            guard var model = entity.components[ModelComponent.self],
                  let state = entity.components[NativeWaterComponent.self] else { continue }
            var changed = false
            for index in state.materialIndices {
                guard model.materials.indices.contains(index), var material = model.materials[index] as? CustomMaterial,
                      material.custom.value.x != phase else { continue }
                material.custom.value.x = phase
                model.materials[index] = material; changed = true
            }
            if changed { entity.components.set(model) }
        }
    }
}
