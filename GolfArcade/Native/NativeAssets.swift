import Foundation
import RealityKit
import UIKit

/// Imported material names can disappear after RealityKit changes a material's
/// blend mode. Keep the authored slot roles independently for subsequent edits
/// and for cloned multiplayer golfers.
struct NativeGolferMaterialRoles: Component {
    let names: [String]
}

@MainActor
enum NativeGolferStyle {
    private static let registration: Void = NativeGolferMaterialRoles.registerComponent()

    static func apply(_ appearance: GolferAppearance, to golfer: Entity, showClub: Bool = true, club: GolfClub = .iron) {
        _ = registration
        let clubKind = club == .putter ? "putter" : club == .driver || club == .wood3 ? "driver" : "iron"
        func color(_ hex: UInt32) -> UIColor {
            UIColor(red: CGFloat((hex >> 16) & 255) / 255, green: CGFloat((hex >> 8) & 255) / 255, blue: CGFloat(hex & 255) / 255, alpha: 1)
        }
        func visit(_ entity: Entity) {
            if var model = entity.components[ModelComponent.self] {
                let roles: NativeGolferMaterialRoles
                if let existing = entity.components[NativeGolferMaterialRoles.self], existing.names.count == model.materials.count {
                    roles = existing
                } else {
                    roles = .init(names: model.materials.map { $0.name ?? "" })
                    entity.components.set(roles)
                }
                model.materials = model.materials.enumerated().map { index, material in
                    guard var pbr = material as? PhysicallyBasedMaterial else { return material }
                    let hex: UInt32?
                    let name = roles.names[index]
                    let role = name.hasPrefix("cap_") ? String(name.dropFirst(4)) : name.hasPrefix("visor_") ? String(name.dropFirst(6)) : name.hasPrefix("club_") ? String(name.dropFirst(5)) : name
                    switch role {
                    case "shirt", "cuff": hex = appearance.outfit.hex
                    case "trousers": hex = appearance.trousersHex
                    case "skin": hex = appearance.skin.hex
                    case "hair": hex = appearance.hair.hex
                    case "ivory": hex = appearance.accentHex
                    default: hex = nil
                    }
                    if let hex { pbr.baseColor.tint = color(hex) }
                    if name.hasPrefix("cap_") || name.hasPrefix("visor_") {
                        let visible = name.hasPrefix("cap_") ? appearance.headwear == .cap : appearance.headwear == .visor
                        pbr.blending = visible ? .opaque : .transparent(opacity: .init(floatLiteral: 0))
                    }
                    if name.hasPrefix("club_") {
                        let variant = ["driver", "iron", "putter"].first { name.hasPrefix("club_" + $0 + "_") }
                        let visible = showClub && (variant == nil || variant == clubKind)
                        pbr.blending = visible ? .opaque : .transparent(opacity: .init(floatLiteral: 0))
                    }
                    // Mirroring for a left-handed stance must retain both surfaces.
                    pbr.faceCulling = .none
                    return pbr
                }
                entity.components.set(model)
            }
            if entity.name == "cap" { entity.isEnabled = appearance.headwear == .cap }
            if entity.name == "visor" { entity.isEnabled = appearance.headwear == .visor }
            for child in entity.children { visit(child) }
        }
        visit(golfer)
    }
}

/// USDZ visuals use metres, +Y up, and -Z down-course. Golf metadata stays in yards.
enum GolfUnits {
    static let metresPerYard = 0.9144
    static func position(_ point: CoursePoint, heightYards: Double) -> SIMD3<Float> {
        SIMD3(Float(point.x * metresPerYard), Float(heightYards * metresPerYard), Float(-point.d * metresPerYard))
    }
    static func ballPosition(_ sample: FlightPoint, hole: Hole) -> SIMD3<Float> {
        let point = CoursePoint(x: sample.lateralYards, d: sample.distanceYards)
        return position(point, heightYards: sample.heightYards + hole.surface(at: point).heightYards) + SIMD3(0, GolfBallVisual.radiusMetres, 0)
    }
}

struct NativeAssetManifest: Decodable {
    struct HoleAsset: Decodable { let courseID: String; let hole: Int; let file: String }
    struct GolferAsset: Decodable {
        struct Contact: Decodable {
            let time: Double
            let leftHand: [Float], rightHand: [Float], leftFoot: [Float], rightFoot: [Float]
            let leftHandRotation: [Float]?, rightHandRotation: [Float]?
            let clubGrip: [Float]?, clubHead: [Float]?, clubDropped: Bool?
            func rotation(_ name: String) -> simd_quatf? {
                guard let value = name == "leftHand" ? leftHandRotation : rightHandRotation,
                      value.count == 4, value.allSatisfy(\.isFinite) else { return nil }
                return simd_normalize(simd_quatf(vector: SIMD4(value)))
            }
            func position(_ name: String) -> SIMD3<Float>? {
                let value: [Float]
                switch name {
                case "leftHand": value = leftHand
                case "rightHand": value = rightHand
                case "leftFoot": value = leftFoot
                case "rightFoot": value = rightFoot
                case "clubGrip": guard let clubGrip else { return nil }; value = clubGrip
                case "clubHead": guard let clubHead else { return nil }; value = clubHead
                default: return nil
                }
                guard value.count == 3, value.allSatisfy(\.isFinite) else { return nil }
                return SIMD3(value)
            }
        }
        struct Clip: Decodable {
            let start: Double; let duration: Double; let impact: Double
            let contacts: [Contact]?
            func clubIsDropped(at time: Double) -> Bool {
                guard let contacts, !contacts.isEmpty else { return false }
                var lo = 0, hi = contacts.count
                while lo < hi {
                    let mid = (lo + hi) / 2
                    if contacts[mid].time <= time { lo = mid + 1 } else { hi = mid }
                }
                return contacts[max(0, lo - 1)].clubDropped ?? false
            }
            func rotation(_ name: String, at time: Double) -> simd_quatf? {
                guard let contacts, let first = contacts.first, let last = contacts.last else { return nil }
                if time <= first.time { return first.rotation(name) }
                if time >= last.time { return last.rotation(name) }
                var lo = 0, hi = contacts.count - 1
                while hi - lo > 1 {
                    let mid = (lo + hi) / 2
                    if contacts[mid].time <= time { lo = mid } else { hi = mid }
                }
                guard let a = contacts[lo].rotation(name), let b = contacts[hi].rotation(name) else { return nil }
                return simd_slerp(a, b, Float((time - contacts[lo].time) / (contacts[hi].time - contacts[lo].time)))
            }
            func contact(_ name: String, at time: Double) -> SIMD3<Float>? {
                guard let contacts, let first = contacts.first, let last = contacts.last else { return nil }
                if time <= first.time { return first.position(name) }
                if time >= last.time { return last.position(name) }
                var lo = 0, hi = contacts.count - 1
                while hi - lo > 1 {
                    let mid = (lo + hi) / 2
                    if contacts[mid].time <= time { lo = mid } else { hi = mid }
                }
                guard let a = contacts[lo].position(name), let b = contacts[hi].position(name) else { return nil }
                let amount = Float((time - contacts[lo].time) / (contacts[hi].time - contacts[lo].time))
                return simd_mix(a, b, SIMD3(repeating: amount))
            }
        }
        let file: String
        let skeletonEntity: String
        let requiredJoints: [String]
        let impactMarkers: [String: Double]
        let clips: [String: Clip]?
    }
    let version: Int
    let metresPerUnit: Double
    let upAxis: String
    let holes: [HoleAsset]
    let golfer: GolferAsset

    func validate() throws {
        guard version == 1, metresPerUnit == 1, upAxis == "Y",
              !golfer.requiredJoints.isEmpty, !golfer.skeletonEntity.isEmpty else {
            throw NativeAssetError.invalid("Expected version 1, metres, Y-up, and an authored skeleton")
        }
        let families = ["driver", "iron", "chip", "pitch", "bunker", "putt"]
        guard families.allSatisfy({ golfer.impactMarkers[$0].map { $0.isFinite && $0 > 0 } ?? false }) else {
            throw NativeAssetError.invalid("Missing swing impact markers")
        }
        let files = holes.map(\.file) + [golfer.file]
        guard files.allSatisfy({ $0 == ($0 as NSString).lastPathComponent && $0.hasSuffix(".usdz") }) else {
            throw NativeAssetError.invalid("Asset names must be local USDZ filenames")
        }
        guard Set(holes.map { "\($0.courseID):\($0.hole)" }).count == holes.count else {
            throw NativeAssetError.invalid("Duplicate course/hole visual definitions")
        }
    }

    var missingCourseEntries: [String] {
        Course.all.flatMap { course in course.holes.compactMap { hole in
            holes.contains { $0.courseID == course.id && $0.hole == hole.number } ? nil : "\(course.id)/\(hole.number)"
        } }
    }
}

enum NativeAssetError: LocalizedError {
    case missing(String), invalid(String)
    var errorDescription: String? {
        switch self {
        case .missing(let name): "Missing production asset: \(name)"
        case .invalid(let reason): "Asset validation failed: \(reason)"
        }
    }
}

/// Never fabricates scenery when production content is missing. Keeps only current/next holes.
@MainActor
final class NativeAssetLoader {
    private let bundle: Bundle
    private var cache: [String: Entity] = [:]
    private(set) var manifest: NativeAssetManifest?
    init(bundle: Bundle = .main) { self.bundle = bundle }

    func readManifest() throws -> NativeAssetManifest {
        if let manifest { return manifest }
        guard let url = bundle.url(forResource: "GolfNativeAssets", withExtension: "json") else {
            throw NativeAssetError.missing("GolfNativeAssets.json and converted course/golfer USDZ packages")
        }
        let value = try JSONDecoder().decode(NativeAssetManifest.self, from: Data(contentsOf: url))
        try value.validate()
        manifest = value
        return value
    }

    func course(_ course: Course, hole: Hole) async throws -> Entity {
        let manifest = try readManifest()
        guard let entry = manifest.holes.first(where: { $0.courseID == course.id && $0.hole == hole.number }) else {
            throw NativeAssetError.missing("\(course.id), hole \(hole.number)")
        }
        let entity = try await load(entry.file)
        // Mandatory exported landmarks prove the coordinate boundary before gameplay starts.
        for (name, point) in [("tee", hole.tee), ("pin", hole.pin)] {
            guard let marker = entity.findEntity(named: name) else { throw NativeAssetError.invalid("\(entry.file) lacks \(name)") }
            let expected = GolfUnits.position(point, heightYards: hole.surface(at: point).heightYards)
            guard simd_distance(marker.position(relativeTo: entity), expected) < 0.02 else {
                throw NativeAssetError.invalid("\(entry.file) \(name) differs from authoritative terrain")
            }
        }
        try await NativeWaterSurface.install(on: entity, hole: hole)
        try await NativeGrassSurface.install(on: entity)
        try NativeCourseVertexColorSurface.install(on: entity)
        // Cache the prepared material/texture with the same current/next-hole
        // lifetime as its USDZ. Gameplay and card renders share this presentation.
        cache[entry.file] = entity.clone(recursive: true)
        return entity
    }

    func golfer() async throws -> Entity {
        let definition = try readManifest().golfer
        let root = try await load(definition.file)
        try Self.installClips(definition, on: root)
        guard let model = root.findEntity(named: definition.skeletonEntity) as? ModelEntity,
              Set(definition.requiredJoints).isSubset(of: Set(model.jointNames)) else {
            throw NativeAssetError.invalid("Golfer has no compatible bound skeleton")
        }
        let names = Set(root.availableAnimations.compactMap(\.name))
        let required = Set(["driver", "iron", "chip", "pitch", "bunker", "putt", "celebration", "recovery", "idle"])
        guard required.isSubset(of: names) else {
            throw NativeAssetError.invalid("Golfer clips missing: \(required.subtracting(names).sorted().joined(separator: ", "))")
        }
        for (name, marker) in definition.impactMarkers {
            guard let clip = root.availableAnimations.first(where: { $0.name == name }), marker < clip.definition.duration else {
                throw NativeAssetError.invalid("Impact marker lies outside clip \(name)")
            }
        }
        return root
    }

    /// USD contains one continuous skeletal performance; named ranges cut it
    /// into embedded authored clips without relying on importer-specific naming.
    static func installClips(_ definition: NativeAssetManifest.GolferAsset, on root: Entity) throws {
        guard let clips = definition.clips else { return }
        guard let source = root.availableAnimations.first else {
            throw NativeAssetError.invalid("Golfer USDZ has no embedded skeletal performance")
        }
        var resources: [String: AnimationResource] = [:]
        for (name, clip) in clips {
            guard clip.start.isFinite, clip.duration.isFinite, clip.start >= 0, clip.duration > 0,
                  clip.start + clip.duration <= source.definition.duration + 0.04 else {
                throw NativeAssetError.invalid("Invalid embedded animation range: \(name)")
            }
            resources[name] = try AnimationResource.generate(with: AnimationView(source: source.definition,
                name: name, trimStart: clip.start, trimDuration: clip.duration))
        }
        root.components.set(AnimationLibraryComponent(animations: resources))
    }

    func retain(course: Course, index: Int) {
        guard let manifest else { return }
        let numbers = Set(course.holes.dropFirst(index).prefix(2).map(\.number))
        let keep = Set(manifest.holes.filter { $0.courseID == course.id && numbers.contains($0.hole) }.map(\.file) + [manifest.golfer.file])
        cache = cache.filter { keep.contains($0.key) }
    }

    private func load(_ filename: String) async throws -> Entity {
        if let cached = cache[filename] { return cached.clone(recursive: true) }
        guard let url = bundle.url(forResource: (filename as NSString).deletingPathExtension, withExtension: "usdz") else {
            throw NativeAssetError.missing(filename)
        }
        let entity = try await Entity(contentsOf: url)
        try Task.checkCancellation()
        cache[filename] = entity
        return entity.clone(recursive: true)
    }
}
