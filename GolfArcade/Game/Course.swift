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

/// One hole. The fairway follows `centerline` from the tee (first point) to the pin (last point);
/// all measurements share the flight model's yards so drawing and scoring use one source of truth.
struct Hole: Identifiable, Equatable, Sendable {
    let number: Int
    let par: Int
    let centerline: [CoursePoint]
    let fairwayWidth: Double
    let greenRadius: Double
    let hazards: [CourseHazard]

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

    static let easy = Course(id: "meadow", name: "Meadow Run", difficulty: .easy, holes: [
        Hole(number: 1, par: 4, centerline: [p(0, 0), p(0, 185), p(38, 245), p(105, 330)], fairwayWidth: 50, greenRadius: 20,
             hazards: [bunker(0, -22, 174, 16, 27), bunker(1, 60, 255, 18, 30), bunker(2, 86, 324, 14, 21)]),
        Hole(number: 2, par: 4, centerline: [p(0, 0), p(0, 170), p(-18, 280)], fairwayWidth: 46, greenRadius: 20,
             hazards: [bunker(0, 23, 172, 16, 22), bunker(1, -38, 272, 12, 16)]),
        Hole(number: 3, par: 3, centerline: [p(0, 0), p(8, 115)], fairwayWidth: 50, greenRadius: 20,
             hazards: [bunker(0, -12, 108, 14, 18)])
    ])

    static let medium = Course(id: "pine", name: "Pine Bend", difficulty: .medium, holes: [
        Hole(number: 1, par: 4, centerline: [p(0, 0), p(0, 205), p(55, 285), p(110, 345)], fairwayWidth: 36, greenRadius: 17,
             hazards: [bunker(0, -17, 194, 16, 26), bunker(1, 28, 234, 14, 22), bunker(2, 125, 339, 12, 19)]),
        Hole(number: 2, par: 3, centerline: [p(0, 0), p(-6, 160)], fairwayWidth: 32, greenRadius: 16,
             hazards: [bunker(0, -24, 154, 14, 20), bunker(1, 10, 172, 14, 14)]),
        Hole(number: 3, par: 4, centerline: [p(0, 0), p(0, 180), p(-35, 300)], fairwayWidth: 34, greenRadius: 17,
             hazards: [bunker(0, 18, 188, 16, 24), bunker(1, -19, 170, 14, 22), bunker(2, -53, 292, 12, 18)])
    ])

    static let hard = Course(id: "cliff", name: "Cliffwater", difficulty: .hard, holes: [
        Hole(number: 1, par: 4, centerline: [p(0, 0), p(0, 225), p(-70, 300), p(-115, 380)], fairwayWidth: 28, greenRadius: 14,
             hazards: [water(0, 35, 145, 45, 185), bunker(1, -15, 207, 12, 26), bunker(2, -100, 373, 12, 19)]),
        Hole(number: 2, par: 5, centerline: [p(0, 0), p(0, 240), p(-30, 390), p(-30, 470)], fairwayWidth: 26, greenRadius: 14,
             hazards: [bunker(0, 13, 245, 12, 24), water(1, -30, 415, 56, 22), bunker(2, -45, 468, 12, 16), bunker(3, -14, 480, 10, 12)]),
        Hole(number: 3, par: 3, centerline: [p(0, 0), p(0, 170)], fairwayWidth: 24, greenRadius: 13,
             hazards: [water(0, 0, 95, 80, 44), bunker(1, -14, 164, 12, 16), bunker(2, 14, 177, 12, 14)])
    ])

    static let all = [easy, medium, hard]
}
