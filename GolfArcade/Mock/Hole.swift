import Foundation

/// A point on the course, in yards. `z` is negative downrange from the tee, like the scene.
struct CoursePoint: Equatable, Sendable {
    var x: Double
    var z: Double

    func distance(to other: CoursePoint) -> Double { hypot(other.x - x, other.z - z) }

    /// Compass heading from here to `other`, in degrees; 0 points straight downrange (−z).
    func heading(to other: CoursePoint) -> Double {
        atan2(other.x - x, -(other.z - z)) * 180 / .pi
    }

    /// Distance from this point to the segment a–b.
    func distance(toSegment a: CoursePoint, _ b: CoursePoint) -> Double {
        let abx = b.x - a.x, abz = b.z - a.z
        let length = abx * abx + abz * abz
        guard length > 0 else { return distance(to: a) }
        let t = min(1, max(0, ((x - a.x) * abx + (z - a.z) * abz) / length))
        return distance(to: CoursePoint(x: a.x + abx * t, z: a.z + abz * t))
    }
}

/// Where the ball sits. Each lie takes something off the strike and changes how the ball rolls.
enum Lie: String, Sendable {
    case tee, fairway, rough, bunker, green

    var displayName: String {
        switch self {
        case .tee: "Tee"
        case .fairway: "Fairway"
        case .rough: "Rough"
        case .bunker: "Bunker"
        case .green: "Green"
        }
    }

    /// Fraction of ball speed that survives the lie.
    var strikeFactor: Double {
        switch self {
        case .tee, .fairway, .green: 1
        case .rough: 0.82
        case .bunker: 0.62
        }
    }

    /// Rolling deceleration on this surface, m/s². Greens are quick, rough grabs the ball.
    var rollingDeceleration: Double {
        switch self {
        case .green: 0.9
        case .tee, .fairway: BallFlight.fairwayRolling
        case .rough: 6
        case .bunker: 14
        }
    }
}

/// One hole: a fairway corridor along a centreline, a green with a cup, and a few bunkers.
struct Hole: Equatable, Sendable {
    struct Bunker: Equatable, Sendable {
        let center: CoursePoint
        let radius: Double
    }

    let number: Int
    let name: String
    let par: Int
    let tee: CoursePoint
    /// Fairway centreline from the tee to the green.
    let centerline: [CoursePoint]
    let fairwayHalfWidth: Double
    let greenCenter: CoursePoint
    let greenRadius: Double
    let cup: CoursePoint
    let bunkers: [Bunker]
    /// Balls that stop this close to the cup are in, and a rolling ball this close drops.
    let cupRadius = 0.5

    var length: Double { tee.distance(to: cup) }

    func lie(at point: CoursePoint) -> Lie {
        if point.distance(to: tee) < 4 { return .tee }
        if point.distance(to: greenCenter) <= greenRadius { return .green }
        if bunkers.contains(where: { point.distance(to: $0.center) <= $0.radius }) { return .bunker }
        for index in 1..<centerline.count where point.distance(toSegment: centerline[index - 1], centerline[index]) <= fairwayHalfWidth {
            return .fairway
        }
        return .rough
    }

    /// Feet from the cup, the number golfers care about on the green.
    func feetToCup(from point: CoursePoint) -> Int { Int((point.distance(to: cup) * 3).rounded()) }

    /// "Birdie", "Bogey", … for a finished hole.
    static func scoreName(strokes: Int, par: Int) -> String {
        switch strokes - par {
        case ..<(-2): "Albatross"
        case -2: "Eagle"
        case -1: "Birdie"
        case 0: "Par"
        case 1: "Bogey"
        case 2: "Double bogey"
        case 3: "Triple bogey"
        default: "+\(strokes - par)"
        }
    }

    /// A gentle dogleg-left par 4. Driver and a short iron reach the green; a long drive can
    /// find the fairway bunker on the right.
    static let first = Hole(
        number: 1,
        name: "Meadow Bend",
        par: 4,
        tee: CoursePoint(x: 0, z: 0),
        centerline: [CoursePoint(x: 0, z: 0), CoursePoint(x: 0, z: -190), CoursePoint(x: -22, z: -290), CoursePoint(x: -38, z: -360)],
        fairwayHalfWidth: 19,
        greenCenter: CoursePoint(x: -38, z: -360),
        greenRadius: 14,
        cup: CoursePoint(x: -40, z: -364),
        bunkers: [
            Hole.Bunker(center: CoursePoint(x: 26, z: -255), radius: 8),
            Hole.Bunker(center: CoursePoint(x: -56, z: -348), radius: 6),
            Hole.Bunker(center: CoursePoint(x: -22, z: -376), radius: 5)
        ]
    )
}
