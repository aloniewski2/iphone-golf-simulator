import simd

/// Camera paths use authoritative hole metadata, never reference-film geometry.
enum NativeCourseFraming {
    static func overview(_ hole: Hole) -> (eye: SIMD3<Float>, target: SIMD3<Float>) {
        let xs = hole.centerline.map(\.x), ds = hole.centerline.map(\.d)
        let point = CoursePoint(x: (xs.min()! + xs.max()!) / 2, d: (ds.min()! + ds.max()!) / 2)
        let target = GolfUnits.position(point, heightYards: hole.surface(at: point).heightYards)
        let reach = Float(max(100, hole.length) * GolfUnits.metresPerYard)
        return (target + SIMD3(reach * 0.22, reach * 0.72, reach * 0.64), target)
    }

    static func flyover(_ hole: Hole, progress: Double) -> (eye: SIMD3<Float>, target: SIMD3<Float>) {
        let t = max(0, min(1, progress)), points = hole.centerline
        let lengths = zip(points, points.dropFirst()).map { $0.distance(to: $1) }
        let total = lengths.reduce(0, +), distance = total * t * t * (3 - 2 * t)
        func sample(_ distance: Double) -> CoursePoint {
            var remaining = max(0, min(total, distance))
            for index in lengths.indices {
                let u = min(1, remaining / max(0.001, lengths[index]))
                if remaining <= lengths[index] || index == lengths.count - 1 {
                    return CoursePoint(x: points[index].x + (points[index + 1].x - points[index].x) * u,
                        d: points[index].d + (points[index + 1].d - points[index].d) * u)
                }
                remaining -= lengths[index]
            }
            return points[0]
        }
        let point = sample(distance)
        let angle = Float(sample(distance - 18).heading(to: sample(distance + 18)) * .pi / 180)
        let forward = SIMD3<Float>(sin(angle), 0, -cos(angle)), right = SIMD3<Float>(cos(angle), 0, sin(angle))
        let target = GolfUnits.position(point, heightYards: hole.surface(at: point).heightYards)
        let unit = Float(GolfUnits.metresPerYard)
        return (target + (-forward * 35 + right * Float(24 - 10 * t) + SIMD3(0, Float(25 - 8 * t), 0)) * unit,
            target + forward * 14 * unit)
    }
}
