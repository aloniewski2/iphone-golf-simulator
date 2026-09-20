import Foundation
import simd

/// Arcade readability only. Never use these dimensions for contacts or scoring.
enum GolfBallVisual {
    static let radiusMetres: Float = 0.065
    static let radiusYards = Double(radiusMetres) / 0.9144
}

/// Tunable deterministic game approximations, in SI units. Not sensor measurements.
enum GolfInteractions {
    struct CircleContact: Equatable {
        let fraction: Double
        let point: simd_double2
        let normal: simd_double2
    }
    static func sweptCircle(from a: simd_double2,to b: simd_double2,center: simd_double2,radius: Double) -> CircleContact? {
        let delta=b-a, offset=a-center, length2=simd_length_squared(delta)
        guard radius > 0, length2 > 1e-12 else { return nil }
        let c=simd_length_squared(offset)-radius*radius
        let projection=simd_dot(offset,delta)
        let discriminant=projection*projection-length2*c
        guard discriminant >= 0 else { return nil }
        let fraction=c <= 0 ? 0 : (-projection-sqrt(discriminant))/length2
        guard fraction >= 0, fraction <= 1 else { return nil }
        let point=a+delta*fraction
        let radial=point-center
        let normal=simd_length_squared(radial)>1e-12 ? simd_normalize(radial) : -simd_normalize(delta)
        guard simd_dot(delta,normal)<0 else { return nil } // Never trap a ball escaping an overlap.
        return CircleContact(fraction:fraction,point:point,normal:normal)
    }

    enum CupResult: Equatable { case none, captured, lipOut(position:simd_double2,velocity:simd_double2) }
    static let cupRadius=0.054
    static let ballRadius=0.02135

    static func cup(from a: simd_double2,to b: simd_double2,velocity: simd_double2,center: simd_double2) -> CupResult {
        let speed=simd_length(velocity)
        guard speed > 1e-6,
              let hit=sweptCircle(from:a,to:b,center:center,radius:cupRadius+ballRadius) else { return .none }
        let direction=velocity/speed, toCup=center-a
        let offset=abs(toCup.x*direction.y-toCup.y*direction.x)
        // Available drop chord shortens toward the edge; therefore edge entry tolerates less pace.
        let chord=sqrt(max(0,1-pow(offset/cupRadius,2)))
        let captureSpeed=1.65*chord
        if offset <= cupRadius-ballRadius*0.2, speed <= captureSpeed { return .captured }
        // A fast central putt bridges the opening. A glancing, moderate-speed rim strike
        // loses energy and redirects away from the cup; neither outcome snaps to the pin.
        guard offset >= cupRadius*0.55, speed < 2.8 else { return .none }
        let incoming=simd_dot(velocity,hit.normal)
        let outgoing=(velocity-hit.normal*incoming*1.35)*0.78
        return .lipOut(position:center+hit.normal*(cupRadius+ballRadius+0.002),velocity:outgoing)
    }
}

struct CourseTree: Identifiable, Equatable, Sendable {
    let id: Int
    let center: CoursePoint
    let trunkRadius: Double // yards, also used by the rendered cylinder
    let trunkHeight: Double
    var crownRadius: Double = 3.4 // decorative foliage, deliberately not collision geometry
}
