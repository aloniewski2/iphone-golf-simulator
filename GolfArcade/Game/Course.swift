import Foundation

/// A spot on the ground, in yards: `x` to the right of the tee line, `d` down the hole.
struct CoursePoint: Equatable, Hashable, Codable, Sendable {
    var x: Double
    var d: Double

    static let zero = CoursePoint(x: 0, d: 0)

    func distance(to other: CoursePoint) -> Double { hypot(other.x - x, other.d - d) }

    /// Degrees right of straight down the hole from here to `other`.
    func heading(to other: CoursePoint) -> Double { atan2(other.x - x, other.d - d) * 180 / .pi }
}

enum CourseDifficulty: String, CaseIterable, Codable, Sendable {
    case easy, medium, hard

    var displayName: String { rawValue.capitalized }
}

enum CourseHazardKind: String, Codable, Sendable {
    case bunker, water

    var displayName: String { rawValue.capitalized }
}

struct CourseHazard: Identifiable, Equatable, Sendable {
    let id: Int
    let kind: CourseHazardKind
    let x: Double
    let distance: Double
    let width: Double
    let length: Double

    func contains(_ point: CoursePoint) -> Bool {
        let dx = (point.x - x) / max(width / 2, 0.001)
        let dz = (point.d - distance) / max(length / 2, 0.001)
        return dx * dx + dz * dz <= 1
    }
}

/// Where a ball comes to rest. Lies change the next shot; water and out of bounds cost a stroke.
enum CourseLie: String, Equatable, Codable, Sendable {
    case tee, fairway, rough, bunker, green, water, outOfBounds

    var displayName: String {
        switch self {
        case .outOfBounds: "Out of bounds"
        default: rawValue.capitalized
        }
    }

    /// Share of a full shot's power a lie allows. Putts are not affected.
    var powerFactor: Double {
        switch self {
        case .rough: 0.85
        case .bunker: 0.6
        default: 1
        }
    }

    var penaltyStrokes: Int { self == .water || self == .outOfBounds ? 1 : 0 }
}

/// The shape of the ground, in yards: an overall tilt plus mounds, swales and ridges. The ball
/// rolls with it, the turf is drawn on it, and the green-reading grid flows down it, so what you
/// see is exactly what the putt will do.
struct Terrain: Equatable, Sendable {
    struct Feature: Equatable, Sendable {
        var center: CoursePoint
        /// A ridge runs from `center` to `end`; a mound or swale has no end.
        var end: CoursePoint?
        var radius: Double
        /// Yards; negative sinks a swale or hollow.
        var height: Double

        /// Distance from the feature's spine, and the direction away from it.
        fileprivate func span(to point: CoursePoint) -> (distance: Double, awayX: Double, awayD: Double) {
            var nearest = center
            if let end {
                let dx = end.x - center.x, dd = end.d - center.d
                let length = max(dx * dx + dd * dd, 0.0001)
                let t = min(1, max(0, ((point.x - center.x) * dx + (point.d - center.d) * dd) / length))
                nearest = CoursePoint(x: center.x + dx * t, d: center.d + dd * t)
            }
            let distance = point.distance(to: nearest)
            guard distance > 0.0001 else { return (0, 0, 0) }
            return (distance, (point.x - nearest.x) / distance, (point.d - nearest.d) / distance)
        }
    }

    /// Rise per yard toward +x (right) and toward +d (down the hole).
    var tiltX: Double
    var tiltD: Double
    var features: [Feature]

    static let flat = Terrain(tiltX: 0, tiltD: 0, features: [])

    /// Height above the datum, in yards.
    func elevation(at point: CoursePoint) -> Double {
        var height = tiltX * point.x + tiltD * point.d
        for feature in features {
            let u = feature.span(to: point).distance / max(feature.radius, 0.001)
            guard u < 1 else { continue }
            // (1 - u²)²: flat on top and at the rim, so nothing kinks.
            height += feature.height * pow(1 - u * u, 2)
        }
        return height
    }

    /// Rise per yard in x and d. A ball accelerates the opposite way.
    func gradient(at point: CoursePoint) -> (dx: Double, dd: Double) {
        var dx = tiltX, dd = tiltD
        for feature in features {
            let span = feature.span(to: point)
            let radius = max(feature.radius, 0.001)
            let u = span.distance / radius
            guard u < 1, u > 0 else { continue }
            let slope = feature.height * -4 * u * (1 - u * u) / radius
            dx += slope * span.awayX
            dd += slope * span.awayD
        }
        return (dx, dd)
    }

    /// Steepness, as a fraction (0.02 = 2%).
    func slope(at point: CoursePoint) -> Double {
        let g = gradient(at: point)
        return hypot(g.dx, g.dd)
    }
}

/// One hole. The fairway follows `centerline` from the tee (first point) to the pin (last point);
/// all measurements share the flight model's yards so drawing and scoring use one source of truth.
struct Hole: Identifiable, Equatable, Sendable {
    let number: Int
    let par: Int
    let centerline: [CoursePoint]
    let fairwayWidth: Double
    let greenRadius: Double
    let hazards: [CourseHazard]
    var terrain: Terrain = .flat

    /// Rough on each side of the fairway. Beyond it (the tree line) is out of bounds.
    static let roughWidth = 24.0
    /// Small, explicit arcade tolerance around the cup, in yards.
    static let cupCaptureRadius = 0.12

    var id: Int { number }
    var tee: CoursePoint { centerline[0] }
    var pin: CoursePoint { centerline[centerline.count - 1] }
    var length: Double { zip(centerline, centerline.dropFirst()).reduce(0) { $0 + $1.0.distance(to: $1.1) } }

    /// Follow the next landing station, not a straight shortcut through a dogleg.
    /// Project onto the nearest route segment so a passed station never aims backwards.
    func recommendedTarget(from ball: CoursePoint) -> CoursePoint {
        guard centerline.count > 2, ball.distance(to: pin) > greenRadius + 20 else { return pin }
        var closest = Double.infinity
        var segment = 0
        for i in 0..<(centerline.count - 1) {
            let a = centerline[i], b = centerline[i + 1]
            let dx = b.x - a.x, dd = b.d - a.d
            let t = min(1, max(0, ((ball.x - a.x) * dx + (ball.d - a.d) * dd) / max(0.001, dx * dx + dd * dd)))
            let distance = ball.distance(to: CoursePoint(x: a.x + t * dx, d: a.d + t * dd))
            if distance <= closest { closest = distance; segment = i }
        }
        var station = segment + 1
        while station < centerline.count - 1 && ball.distance(to: centerline[station]) < 35 { station += 1 }
        return centerline[station]
    }

    func distanceFromCenterline(_ point: CoursePoint) -> Double {
        zip(centerline, centerline.dropFirst()).map { a, b in
            let dx = b.x - a.x, dd = b.d - a.d
            let lengthSquared = max(dx * dx + dd * dd, 0.0001)
            let t = min(max(((point.x - a.x) * dx + (point.d - a.d) * dd) / lengthSquared, 0), 1)
            return point.distance(to: CoursePoint(x: a.x + dx * t, d: a.d + dd * t))
        }.min() ?? .infinity
    }

    func lie(at point: CoursePoint) -> CourseLie {
        if let hazard = hazards.first(where: { $0.contains(point) }) {
            return hazard.kind == .water ? .water : .bunker
        }
        if point.distance(to: pin) <= greenRadius { return .green }
        if point.distance(to: tee) <= 4 { return .tee }
        let offset = distanceFromCenterline(point)
        if offset <= fairwayWidth / 2 { return .fairway }
        if offset <= fairwayWidth / 2 + Self.roughWidth { return .rough }
        return .outOfBounds
    }
}

struct Course: Identifiable, Equatable, Sendable {
    let id: String
    let name: String
    let difficulty: CourseDifficulty
    let holes: [Hole]

    var par: Int { holes.reduce(0) { $0 + $1.par } }
    var bestScoreKey: String { "course.\(id).routing2.best" }
    var length: Double { holes.reduce(0) { $0 + $1.length } }
    var bunkerCount: Int { holes.reduce(0) { $0 + $1.hazards.filter { $0.kind == .bunker }.count } }
    var hasWater: Bool { holes.contains { $0.hazards.contains { $0.kind == .water } } }

    private static func p(_ x: Double, _ d: Double) -> CoursePoint { CoursePoint(x: x, d: d) }
    private static func bunker(_ id: Int, _ x: Double, _ d: Double, _ w: Double, _ l: Double) -> CourseHazard {
        CourseHazard(id: id, kind: .bunker, x: x, distance: d, width: w, length: l)
    }
    private static func water(_ id: Int, _ x: Double, _ d: Double, _ w: Double, _ l: Double) -> CourseHazard {
        CourseHazard(id: id, kind: .water, x: x, distance: d, width: w, length: l)
    }
    private static func mound(_ x: Double, _ d: Double, r: Double, h: Double) -> Terrain.Feature {
        Terrain.Feature(center: p(x, d), end: nil, radius: r, height: h)
    }
    private static func ridge(_ x0: Double, _ d0: Double, _ x1: Double, _ d1: Double, r: Double, h: Double) -> Terrain.Feature {
        Terrain.Feature(center: p(x0, d0), end: p(x1, d1), radius: r, height: h)
    }
    private static func shaped(_ hole: Hole, _ terrain: Terrain) -> Hole {
        var shaped = hole
        shaped.terrain = terrain
        return shaped
    }

    // Greens: contours of a few tenths of a yard across the putting surface (1–3% slopes) so a
    // putt breaks, plus gentle movement through the fairways. Heights are in yards.
    static let easy = Course(id: "meadow", name: "Meadow Run", difficulty: .easy, holes: [
        shaped(Hole(number: 1, par: 4, centerline: [p(0, 0), p(0, 185), p(38, 245), p(105, 330)], fairwayWidth: 50, greenRadius: 20,
             hazards: [bunker(0, -22, 174, 16, 27), bunker(1, 60, 255, 18, 30), bunker(2, 86, 324, 14, 21)]),
             Terrain(tiltX: 0.004, tiltD: 0.003, features: [
                mound(118, 322, r: 26, h: 0.4), mound(96, 342, r: 16, h: -0.2),
                mound(-30, 120, r: 60, h: 1.8), ridge(20, 200, 60, 230, r: 30, h: -1.2)])),
        shaped(Hole(number: 2, par: 4, centerline: [p(0, 0), p(0, 170), p(-18, 280)], fairwayWidth: 46, greenRadius: 20,
             hazards: [bunker(0, 23, 172, 16, 22), bunker(1, -38, 272, 12, 16)]),
             Terrain(tiltX: -0.006, tiltD: 0.004, features: [
                ridge(-30, 268, -6, 292, r: 15, h: 0.25), mound(-18, 265, r: 15, h: -0.18),
                mound(40, 90, r: 55, h: 1.5), mound(-45, 200, r: 45, h: 1.1)])),
        shaped(Hole(number: 3, par: 3, centerline: [p(0, 0), p(8, 115)], fairwayWidth: 50, greenRadius: 20,
             hazards: [bunker(0, -12, 108, 14, 18)]),
             Terrain(tiltX: 0.008, tiltD: -0.005, features: [mound(18, 106, r: 18, h: 0.3), mound(-4, 126, r: 14, h: -0.18)]))
    ])

    static let medium = Course(id: "pine", name: "Pine Bend", difficulty: .medium, holes: [
        shaped(Hole(number: 1, par: 4, centerline: [p(0, 0), p(0, 205), p(55, 285), p(110, 345)], fairwayWidth: 36, greenRadius: 17,
             hazards: [bunker(0, -17, 194, 16, 26), bunker(1, 28, 234, 14, 22), bunker(2, 125, 339, 12, 19)]),
             Terrain(tiltX: -0.01, tiltD: 0.006, features: [
                ridge(96, 336, 124, 352, r: 14, h: 0.28), mound(112, 358, r: 15, h: -0.22),
                mound(-50, 150, r: 60, h: 2.4), mound(70, 250, r: 40, h: -1.4)])),
        shaped(Hole(number: 2, par: 3, centerline: [p(0, 0), p(-6, 160)], fairwayWidth: 32, greenRadius: 16,
             hazards: [bunker(0, -24, 154, 14, 20), bunker(1, 10, 172, 14, 14)]),
             Terrain(tiltX: 0.012, tiltD: 0.008, features: [mound(-16, 150, r: 16, h: 0.3), mound(4, 168, r: 13, h: -0.2), mound(-40, 80, r: 45, h: 1.6)])),
        shaped(Hole(number: 3, par: 4, centerline: [p(0, 0), p(0, 180), p(-35, 300)], fairwayWidth: 34, greenRadius: 17,
             hazards: [bunker(0, 18, 188, 16, 24), bunker(1, -19, 170, 14, 22), bunker(2, -53, 292, 12, 18)]),
             Terrain(tiltX: 0.006, tiltD: -0.012, features: [
                ridge(-50, 292, -20, 306, r: 15, h: 0.3), mound(-30, 288, r: 14, h: -0.18),
                ridge(-40, 120, 40, 140, r: 35, h: 2.2)]))
    ])

    static let hard = Course(id: "cliff", name: "Cliffwater", difficulty: .hard, holes: [
        shaped(Hole(number: 1, par: 4, centerline: [p(0, 0), p(0, 225), p(-70, 300), p(-115, 380)], fairwayWidth: 28, greenRadius: 14,
             hazards: [water(0, 35, 145, 45, 185), bunker(1, -15, 207, 12, 26), bunker(2, -100, 373, 12, 19)]),
             Terrain(tiltX: -0.012, tiltD: 0.008, features: [
                mound(-104, 372, r: 15, h: 0.32), ridge(-128, 372, -110, 392, r: 13, h: -0.22),
                mound(-60, 260, r: 50, h: 3.0), mound(30, 60, r: 50, h: -2.0)])),
        shaped(Hole(number: 2, par: 5, centerline: [p(0, 0), p(0, 240), p(-30, 390), p(-30, 470)], fairwayWidth: 26, greenRadius: 14,
             hazards: [bunker(0, 13, 245, 12, 24), water(1, -30, 415, 56, 22), bunker(2, -45, 468, 12, 16), bunker(3, -14, 480, 10, 12)]),
             Terrain(tiltX: 0.01, tiltD: -0.008, features: [
                ridge(-42, 456, -18, 456, r: 12, h: 0.26), mound(-30, 486, r: 12, h: -0.2),
                mound(30, 200, r: 55, h: 2.6), mound(-60, 330, r: 45, h: 2.0)])),
        shaped(Hole(number: 3, par: 3, centerline: [p(0, 0), p(0, 170)], fairwayWidth: 24, greenRadius: 13,
             hazards: [water(0, 0, 95, 80, 44), bunker(1, -14, 164, 12, 16), bunker(2, 14, 177, 12, 14)]),
             Terrain(tiltX: -0.012, tiltD: 0.01, features: [mound(12, 181, r: 14, h: 0.3), mound(-12, 159, r: 12, h: -0.2), mound(0, 30, r: 40, h: 1.2)]))
    ])

    static let all = [easy, medium, hard]
}
