import Metal
import RealityKit

/// Static shoreline leaves and cut bunker roots retain their authored vertex
/// gradients. They deliberately do not use the turf blade-motion/UV convention.
@MainActor
enum NativeCourseVertexColorSurface {
    static func install(on root: Entity) throws {
        var shader: CustomMaterial.SurfaceShader?
        func visit(_ entity: Entity) throws {
            if var model = entity.components[ModelComponent.self] {
                var changed = false
                for index in model.materials.indices {
                    guard let base = model.materials[index] as? PhysicallyBasedMaterial else { continue }
                    let name = (base.name ?? "").replacingOccurrences(of: " ", with: "_")
                    guard name.contains("Sunward_bank_sedges") || name.contains("Cut_bunker_turf_roots") else { continue }
                    if shader == nil {
                        guard let device = MTLCreateSystemDefaultDevice(), let library = device.makeDefaultLibrary(),
                              library.makeFunction(name: "nativeCourseVertexColorSurface") != nil else {
                            throw NativeAssetError.missing("Native course vertex-color shader library")
                        }
                        shader = .init(named: "nativeCourseVertexColorSurface", in: library)
                    }
                    var material = try CustomMaterial(surfaceShader: shader!, lightingModel: .lit)
                    material.baseColor = .init(base.baseColor)
                    material.roughness = .init(base.roughness)
                    material.faceCulling = .none
                    model.materials[index] = material
                    changed = true
                }
                if changed {
                    entity.components.set(model)
                    entity.components.set(GroundingShadowComponent(castsShadow: false))
                    entity.components.set(DynamicLightShadowComponent(castsShadow: false))
                }
            }
            for child in entity.children { try visit(child) }
        }
        try visit(root)
    }
}

struct NativeGrassComponent: Component {
    var materialIndices: [Int]
}

/// Course meshes are static after binding. Rebind after replacing/repositioning a
/// course; moving the viewport between phone and TV does not move its world.
@MainActor
final class NativeGrassBinding {
    let entity: Entity
    let bounds: BoundingBox
    var lastValue: SIMD4<Float>?

    init(entity: Entity) {
        self.entity = entity
        let raw = entity.visualBounds(recursive: false, relativeTo: nil)
        bounds = BoundingBox(min: raw.min - SIMD3(repeating: 0.2), max: raw.max + SIMD3(repeating: 0.2))
    }

    func needsAnimation(camera: SIMD3<Float>, ball: SIMD3<Float>) -> Bool {
        NativeGrassSurface.needsAnimation(bounds: bounds, camera: camera, ball: ball)
    }
}

@MainActor
enum NativeGrassSurface {
    private static let registration: Void = NativeGrassComponent.registerComponent()
    private static let distantValue = SIMD4<Float>(0, 1_000_000, 0, 1_000_000)

    /// Conservative world-space AABB tests matching nativeGrassMotion's existing
    /// 52-yard camera fade and 0.85-yard horizontal ball-clearance radii.
    static func needsAnimation(bounds: BoundingBox, camera: SIMD3<Float>, ball: SIMD3<Float>) -> Bool {
        func finite(_ value: SIMD3<Float>) -> Bool { value.x.isFinite && value.y.isFinite && value.z.isFinite }
        guard !bounds.isEmpty, finite(bounds.min), finite(bounds.max), finite(camera), finite(ball) else { return true }
        let cameraGap = simd_max(simd_max(bounds.min - camera, camera - bounds.max), .zero)
        let ballGap = simd_max(simd_max(bounds.min - ball, ball - bounds.max), .zero)
        let cameraRadius = Float(52 * GolfUnits.metresPerYard)
        let ballRadius = Float(0.85 * GolfUnits.metresPerYard)
        return simd_length_squared(cameraGap) <= cameraRadius * cameraRadius ||
            ballGap.x * ballGap.x + ballGap.z * ballGap.z <= ballRadius * ballRadius
    }

    static func install(on root: Entity) async throws {
        _ = registration
        // Creating each shader binding separately is expensive on large holes.
        // Copy the shared shader setup, preserving each slot's authored albedo.
        var prototype: CustomMaterial?
        var pending = [root]
        var deadline = ContinuousClock.now.advanced(by: .milliseconds(4))
        while let entity = pending.popLast() {
            try Task.checkCancellation()
            if var model = entity.components[ModelComponent.self] {
                var indices: [Int] = []
                for index in model.materials.indices {
                    guard let base = model.materials[index] as? PhysicallyBasedMaterial,
                          (base.name ?? "").replacingOccurrences(of: " ", with: "_").contains("Sunward_living_turf") else { continue }
                    if prototype == nil {
                        guard let device = MTLCreateSystemDefaultDevice(), let library = device.makeDefaultLibrary(),
                              library.makeFunction(name: "nativeGrassSurface") != nil,
                              library.makeFunction(name: "nativeGrassMotion") != nil else {
                            throw NativeAssetError.missing("Native grass shader library")
                        }
                        var material = try CustomMaterial(
                            surfaceShader: .init(named: "nativeGrassSurface", in: library),
                            geometryModifier: .init(named: "nativeGrassMotion", in: library), lightingModel: .lit)
                        material.faceCulling = .none
                        // No ball is near the course in a static card render.
                        material.custom.value = SIMD4(0, 1_000_000, 0, 1_000_000)
                        prototype = material
                    }
                    // Keep only the albedo used by this shader, not unused USD
                    // material channels that inflate the lit/shadow pipeline.
                    var material = prototype!
                    material.baseColor = .init(base.baseColor)
                    model.materials[index] = material; indices.append(index)
                }
                if !indices.isEmpty {
                    model.boundsMargin = max(model.boundsMargin, 0.2)
                    entity.components.set(model)
                    entity.components.set(NativeGrassComponent(materialIndices: indices))
                    entity.components.set(GroundingShadowComponent(castsShadow: false))
                    entity.components.set(DynamicLightShadowComponent(castsShadow: false))
                }
            }
            pending.append(contentsOf: entity.children.reversed())
            if ContinuousClock.now >= deadline {
                // Next-hole preparation shares the main actor with the current
                // game. Give input/render work a chance between short batches.
                await Task.yield()
                deadline = ContinuousClock.now.advanced(by: .milliseconds(4))
            }
        }
        try Task.checkCancellation()
    }

    static func models(in root: Entity) -> [Entity] {
        var result: [Entity] = []
        func visit(_ entity: Entity) {
            if entity.components.has(NativeGrassComponent.self) { result.append(entity) }
            for child in entity.children { visit(child) }
        }
        visit(root)
        return result
    }

    static func update(_ entities: [Entity], time: Double, ball: SIMD3<Float>, reduceMotion: Bool) {
        update(entities.map { NativeGrassBinding(entity: $0) }, time: time, ball: ball, reduceMotion: reduceMotion, camera: nil)
    }

    static func update(_ bindings: [NativeGrassBinding], time: Double, ball: SIMD3<Float>, reduceMotion: Bool,
                       camera: SIMD3<Float>?) {
        let phase: Float = reduceMotion || !time.isFinite ? 0 : Float(time)
        let activeValue = SIMD4(phase, ball.x, ball.y, ball.z)
        for binding in bindings {
            let active = camera.map { binding.needsAnimation(camera: $0, ball: ball) } ?? true
            let value = active ? activeValue : distantValue
            // Reset once on leaving range, then avoid RealityKit material/component
            // reads and writes until this patch can be affected again.
            guard binding.lastValue != value else { continue }
            let entity = binding.entity
            guard var model = entity.components[ModelComponent.self],
                  let state = entity.components[NativeGrassComponent.self] else { continue }
            var changed = false
            for index in state.materialIndices {
                guard model.materials.indices.contains(index), var material = model.materials[index] as? CustomMaterial,
                      material.custom.value != value else { continue }
                material.custom.value = value
                model.materials[index] = material; changed = true
            }
            if changed { entity.components.set(model) }
            binding.lastValue = value
        }
    }
}
