import RealityKit
import UIKit

/// State belongs to the decorative entity. Nothing here participates in shot physics.
struct NativeBreezeComponent: Component {
    let rest: simd_quatf
    let phase: Float
    let flag: Bool
}

@MainActor
final class NativeCourseEffects {
    let root = Entity()
    let impact = Entity()
    let trail = Entity()
    let ballLocator: ModelEntity
    let pinBeacon = Entity()
    private let pinBeam: ModelEntity
    private let pinBadge: ModelEntity
    private var trailDots: [ModelEntity] = []
    private var trailShotID: Int?
    private var trailRequest: ShotRequest?
    private var trailOrigin: CoursePoint?
    private var decorations: [Entity] = []
    private var pieces: [ModelEntity] = []
    private var sandMaterial = UnlitMaterial(color: UIColor(red: 0.98, green: 0.83, blue: 0.58, alpha: 1))
    private var grassMaterial = UnlitMaterial(color: UIColor(red: 0.53, green: 0.72, blue: 0.30, alpha: 1))
    private var lastSand: Bool?
    private var greenRead: NativeGreenRead?
    private var waterModels: [Entity] = []
    private var waterStarted: Double?
    private var grassBindings: [NativeGrassBinding] = []
    private var grassStarted: Double?

    init() {
        // A fixed topology with no external input; generation failure is a programmer error.
        ballLocator = ModelEntity(mesh: try! Self.locatorMesh(), materials: [UnlitMaterial(color: .white)])
        let scale = Float(GolfUnits.metresPerYard)
        pinBeam = ModelEntity(mesh: .generateCylinder(height: scale, radius: 0.055 * scale),
                              materials: [UnlitMaterial(color: .systemYellow)])
        pinBeam.components.set(OpacityComponent(opacity: 0.6))
        var badgeMaterial = UnlitMaterial()
        badgeMaterial.color = .init(tint: .white, texture: .init(try! Self.pinBadgeTexture()))
        badgeMaterial.blending = .transparent(opacity: .init(floatLiteral: 1))
        badgeMaterial.faceCulling = .none
        pinBadge = ModelEntity(mesh: .generatePlane(width: scale, height: 1.2 * scale), materials: [badgeMaterial])
        for (order, model) in [pinBeam, pinBadge].enumerated() {
            model.components.set(ModelSortGroupComponent(group: .planarUIAlwaysInFront, order: Int32(100 + order)))
            model.components.set(GroundingShadowComponent(castsShadow: false))
            pinBeacon.addChild(model)
        }
        pinBeacon.name = "holeNavigationBeacon"
        root.addChild(pinBeacon)
        root.name = "native-course-effects"
        impact.name = "native-impact-burst"
        root.addChild(impact)
        trail.name = "native-flight-trail"
        ballLocator.name = "ballLocatorNotBallGeometry"
        trail.components.set(OpacityComponent(opacity: 0.65))
        ballLocator.components.set(OpacityComponent(opacity: 0.65))
        ballLocator.components.set(GroundingShadowComponent(castsShadow: false))
        root.addChild(trail); root.addChild(ballLocator)
        let trailMesh = MeshResource.generateSphere(radius: 0.065 * scale)
        for index in 0..<60 {
            let dot = ModelEntity(mesh: trailMesh, materials: [UnlitMaterial(color: .white)])
            dot.name = "trail-dot-\(index)"
            dot.components.set(GroundingShadowComponent(castsShadow: false))
            trail.addChild(dot); trailDots.append(dot)
        }
        let meshes = (0..<3).map { MeshResource.generateSphere(radius: Float(0.025 + Double($0) * 0.009) * scale) }
        for index in 0..<18 {
            let piece = ModelEntity(mesh: meshes[index % 3], materials: [grassMaterial])
            piece.name = "impact-piece-\(index)"
            piece.components.set(GroundingShadowComponent(castsShadow: false))
            impact.addChild(piece)
            pieces.append(piece)
        }
        impact.isEnabled = false
        trail.isEnabled = false; ballLocator.isEnabled = false
        pinBeacon.isEnabled = false
    }

    /// The existing navigation badge is UI artwork, regenerated once as a native texture.
    private static func pinBadgeTexture() throws -> TextureResource {
        let format = UIGraphicsImageRendererFormat()
        format.scale = 2
        let image = UIGraphicsImageRenderer(size: CGSize(width: 100, height: 120), format: format).image { _ in
            UIColor.systemYellow.setFill()
            UIBezierPath(roundedRect: CGRect(x: 3, y: 3, width: 94, height: 88), cornerRadius: 20).fill()
            let pointer = UIBezierPath()
            pointer.move(to: CGPoint(x: 35, y: 86)); pointer.addLine(to: CGPoint(x: 50, y: 115))
            pointer.addLine(to: CGPoint(x: 65, y: 86)); pointer.close(); pointer.fill()
            UIImage(systemName: "flag.fill")?.withTintColor(.black, renderingMode: .alwaysOriginal)
                .draw(in: CGRect(x: 28, y: 18, width: 44, height: 44))
            ("HOLE" as NSString).draw(in: CGRect(x: 20, y: 65, width: 65, height: 22),
                withAttributes: [.font: UIFont.boldSystemFont(ofSize: 19), .foregroundColor: UIColor.black])
        }
        return try TextureResource(image: image.cgImage!, options: .init(semantic: .color))
    }

    private static func locatorMesh() throws -> MeshResource {
        var descriptor = MeshDescriptor(name: "ball-locator-ring")
        var positions: [SIMD3<Float>] = [], normals: [SIMD3<Float>] = [], indices: [UInt32] = []
        let scale = Float(GolfUnits.metresPerYard)
        for ring in 0..<32 {
            let a = Float(ring) * 2 * .pi / 32
            for side in 0..<8 {
                let b = Float(side) * 2 * .pi / 8
                let radius = 0.12 + 0.006 * cos(b)
                positions.append(SIMD3(cos(a) * radius, sin(b) * 0.006, sin(a) * radius) * scale)
                normals.append(SIMD3(cos(a) * cos(b), sin(b), sin(a) * cos(b)))
                let p = UInt32(ring * 8 + side), q = UInt32(((ring + 1) % 32) * 8 + side)
                let r = UInt32(((ring + 1) % 32) * 8 + (side + 1) % 8), s = UInt32(ring * 8 + (side + 1) % 8)
                indices += [p, r, q, p, s, r]
            }
        }
        descriptor.positions = MeshBuffers.Positions(positions)
        descriptor.normals = MeshBuffers.Normals(normals)
        descriptor.primitives = .triangles(indices)
        return try MeshResource.generate(from: [descriptor])
    }

    var decorationCount: Int { decorations.count }

    func configureGreenRead(hole: Hole) throws {
        greenRead?.entity.removeFromParent()
        let overlay = try NativeGreenRead(hole: hole)
        root.addChild(overlay.entity)
        greenRead = overlay
    }

    func updateGreenRead(time: Double, visible: Bool, reduceMotion: Bool) {
        greenRead?.update(time: time, visible: visible, reduceMotion: reduceMotion)
    }

    func bind(course: Entity) {
        clear()
        waterModels = NativeWaterSurface.models(in: course)
        grassBindings = NativeGrassSurface.models(in: course).map { NativeGrassBinding(entity: $0) }
        func visit(_ entity: Entity) {
            let tree = ["canopyTree", "SunwardTree", "sunwardOuterGrove"].contains {
                entity.name == $0 || entity.name.hasPrefix($0 + "_")
            }
            let flag = entity.name == "clothPinFlag"
            if tree || flag {
                entity.components.set(NativeBreezeComponent(rest: entity.orientation,
                    phase: Float(decorations.count) * 1.73, flag: flag))
                decorations.append(entity)
                // Do not apply the same breeze again to nested branches.
                return
            }
            for child in entity.children { visit(child) }
        }
        visit(course)
    }

    func clear() {
        waterModels.removeAll(); waterStarted = nil
        grassBindings.removeAll(); grassStarted = nil
        for entity in decorations {
            if let state = entity.components[NativeBreezeComponent.self] { entity.orientation = state.rest }
            entity.components.remove(NativeBreezeComponent.self)
        }
        decorations.removeAll()
        impact.isEnabled = false
        trail.isEnabled = false; ballLocator.isEnabled = false; trailShotID = nil
        pinBeacon.isEnabled = false
        lastSand = nil
        greenRead?.entity.removeFromParent(); greenRead = nil
    }

    func updateWater(time: Double, reduceMotion: Bool) {
        guard time.isFinite else { return }
        if waterStarted == nil { waterStarted = time }
        NativeWaterSurface.update(waterModels, time: max(0, time - waterStarted!), reduceMotion: reduceMotion)
    }

    func updateGrass(time: Double, ball: SIMD3<Float>, reduceMotion: Bool, camera: SIMD3<Float>? = nil) {
        guard time.isFinite else { return }
        if grassStarted == nil { grassStarted = time }
        NativeGrassSurface.update(grassBindings, time: max(0, time - grassStarted!), ball: ball,
                                  reduceMotion: reduceMotion, camera: camera)
    }

    func updateBallCues(shot: RangeShot?, elapsed: Double, hole: Hole, ball: SIMD3<Float>, camera: SIMD3<Float>, ready: Bool) {
        let sunk = shot.map { $0.isHoled && elapsed + 0.000001 >= $0.duration } ?? false
        ballLocator.isEnabled = ready && !sunk && (shot == nil || elapsed + 0.000001 >= shot!.duration)
        let point = CoursePoint(x: Double(ball.x) / GolfUnits.metresPerYard, d: -Double(ball.z) / GolfUnits.metresPerYard)
        ballLocator.position = GolfUnits.position(point, heightYards: hole.surface(at: point).heightYards + 0.008)
        updatePin(hole: hole, camera: camera, hidden: !ready || sunk)
        trail.isEnabled = ready && shot != nil
        guard ready, let shot else { trailShotID = nil; return }
        if trailShotID != shot.id || trailRequest != shot.request || trailOrigin != shot.origin {
            trailShotID = shot.id
            trailRequest = shot.request; trailOrigin = shot.origin
            for (index, dot) in trailDots.enumerated() {
                let sample = shot.position(at: Double(index) / 59 * shot.duration)
                dot.position = GolfUnits.ballPosition(sample, hole: hole) - SIMD3(0, GolfBallVisual.radiusMetres, 0)
            }
        }
        let scale = Float(GolfUnits.metresPerYard)
        for (index, dot) in trailDots.enumerated() {
            dot.isEnabled = Double(index) / 59 * shot.duration <= elapsed &&
                simd_distance(dot.position, camera) >= 4 * scale && simd_distance(dot.position, ball) >= 2.5 * scale
        }
    }

    private func updatePin(hole: Hole, camera: SIMD3<Float>, hidden: Bool) {
        pinBeacon.position = GolfUnits.position(hole.pin, heightYards: hole.surface(at: hole.pin).heightYards)
        let distance = Double(simd_distance(camera, pinBeacon.position)) / GolfUnits.metresPerYard
        let opacity = Float(HoleNavigation.beaconOpacity(cameraDistance: distance))
        pinBeacon.isEnabled = !hidden && opacity > Float.ulpOfOne
        pinBeacon.components.set(OpacityComponent(opacity: opacity))
        guard pinBeacon.isEnabled else { return }
        let scale = HoleNavigation.beaconScale(cameraDistance: distance)
        let height = max(2.8, scale * 1.15), units = Float(GolfUnits.metresPerYard)
        pinBeam.scale = SIMD3(max(1, scale * 0.8), height, max(1, scale * 0.8))
        pinBeam.position.y = height * units / 2
        pinBadge.scale = SIMD3(repeating: scale)
        let position = pinBeacon.position + SIMD3(0, (height + scale * 0.6) * units, 0)
        pinBadge.look(at: camera, from: position, relativeTo: root, forward: .positiveZ)
    }

    func updateBreeze(time: Double, reduceMotion: Bool) {
        let time = Float(time.truncatingRemainder(dividingBy: 1_000))
        for entity in decorations {
            guard let state = entity.components[NativeBreezeComponent.self] else { continue }
            let angle: Float = reduceMotion ? 0 : state.flag ? sin(time * 1.8) * 0.035 : sin(time * 0.72 + state.phase) * 0.008
            let axis = state.flag ? SIMD3<Float>(0, 1, 0) : SIMD3<Float>(0, 0, 1)
            entity.orientation = state.rest * simd_quatf(angle: angle, axis: axis)
        }
    }

    /// Same bounded 18-piece burst, timing and paths as the owned comparison effect,
    /// converted once at the yards-to-metres boundary. Replays seek deterministically.
    func updateImpact(origin: SIMD3<Float>, elapsed: Double, lie: CourseLie, heading: Double, enabled: Bool) {
        let sand = lie == .bunker
        let duration = sand ? 0.55 : 0.32
        impact.isEnabled = enabled && elapsed.isFinite && elapsed >= 0 && elapsed <= duration
        guard impact.isEnabled else { return }
        impact.position = origin
        if lastSand != sand {
            for piece in pieces { piece.model?.materials = [sand ? sandMaterial : grassMaterial] }
            lastSand = sand
        }
        impact.components.set(OpacityComponent(opacity: Float(1 - elapsed / duration)))
        let t = Float(elapsed), yaw = Float(-heading * .pi / 180)
        for (index, piece) in pieces.enumerated() {
            let angle = Float(index) * 2.39996 + yaw
            let speed = Float(0.3 + Double(index % 5) * 0.14)
            let height = max(0, Float(sand ? 1.8 : 1.1) * t - 3 * t * t)
            piece.position = SIMD3(cos(angle) * speed * t, height + 0.03, sin(angle) * speed * t) * Float(GolfUnits.metresPerYard)
        }
    }
}
