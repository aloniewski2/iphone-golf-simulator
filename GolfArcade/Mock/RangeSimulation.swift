import Foundation

enum CourseDifficulty: String, CaseIterable, Sendable {
    case easy, medium, hard

    var displayName: String { rawValue.capitalized }
}

enum CourseHazardKind: String, Sendable {
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

    func contains(_ point: FlightPoint) -> Bool {
        let dx = (point.lateralYards - x) / max(width / 2, 0.001)
        let dz = (point.distanceYards - distance) / max(length / 2, 0.001)
        return dx * dx + dz * dz <= 1
    }
}

/// A playable one-hole course. All measurements use yards in the same coordinate space as the
/// flight model, which lets rendering and landing detection share one source of truth.
struct GolfCourse: Identifiable, Equatable, Sendable {
    let id: String
    let name: String
    let difficulty: CourseDifficulty
    let par: Int
    let holeDistance: Double
    let fairwayWidth: Double
    let greenRadius: Double
    let hazards: [CourseHazard]

    static let easy = GolfCourse(
        id: "meadow", name: "Meadow Run", difficulty: .easy, par: 3,
        holeDistance: 135, fairwayWidth: 48, greenRadius: 19,
        hazards: [
            CourseHazard(id: 0, kind: .bunker, x: 16, distance: 116, width: 18, length: 25)
        ]
    )
    static let medium = GolfCourse(
        id: "pine", name: "Pine Bend", difficulty: .medium, par: 4,
        holeDistance: 205, fairwayWidth: 35, greenRadius: 16,
        hazards: [
            CourseHazard(id: 0, kind: .bunker, x: -12, distance: 152, width: 21, length: 30),
            CourseHazard(id: 1, kind: .bunker, x: 14, distance: 193, width: 17, length: 20)
        ]
    )
    static let hard = GolfCourse(
        id: "cliff", name: "Cliffwater", difficulty: .hard, par: 5,
        holeDistance: 265, fairwayWidth: 27, greenRadius: 13,
        hazards: [
            CourseHazard(id: 0, kind: .water, x: 0, distance: 142, width: 52, length: 34),
            CourseHazard(id: 1, kind: .bunker, x: -13, distance: 235, width: 17, length: 28),
            CourseHazard(id: 2, kind: .bunker, x: 13, distance: 250, width: 14, length: 22)
        ]
    )

    static let all = [easy, medium, hard]

    func lie(at point: FlightPoint) -> CourseLie {
        if let hazard = hazards.first(where: { $0.contains(point) }) {
            return hazard.kind == .water ? .water : .bunker
        }
        let cupDistance = hypot(point.lateralYards, point.distanceYards - holeDistance)
        if cupDistance <= greenRadius { return cupDistance <= 2.5 ? .pin : .green }
        let fairwayHalfWidth = fairwayWidth / 2
        let courseLength = holeDistance + greenRadius
        if point.distanceYards >= 0, point.distanceYards <= courseLength,
           abs(point.lateralYards) <= fairwayHalfWidth {
            return .fairway
        }
        return .rough
    }
}

enum CourseLie: String, Equatable, Sendable {
    case pin, green, fairway, rough, bunker, water

    var displayName: String {
        switch self {
        case .pin: "At the pin"
        default: rawValue.capitalized
        }
    }

    var points: Int {
        switch self {
        case .pin: 100
        case .green: 70
        case .fairway: 35
        case .rough: 15
        case .bunker: 5
        case .water: 0
        }
    }
}

extension GolfClub {
    /// Total distance of a straight full-power shot, from the flight model. Shown on the club buttons.
    var mockDistance: Double { Self.maxDistances[self] ?? 0 }

    private static let maxDistances: [GolfClub: Double] = Dictionary(uniqueKeysWithValues: allCases.map {
        ($0, BallFlight.simulate($0.launch(power: 1, aimDegrees: 0, curveDegrees: 0)).total)
    })
}

/// One shot on the range: a physically simulated flight plus the score it earned.
struct RangeShot: Identifiable, Equatable, Sendable {
    let id: Int
    let club: GolfClub
    let power: Double
    let aim: Double
    let curve: Double
    let strike: StrikeQuality
    let flight: BallFlight
    let points: Int
    let lie: CourseLie

    var carry: Double { flight.carry }
    var roll: Double { flight.roll }
    var apex: Double { flight.apex }
    var total: Double { flight.total }
    var duration: Double { flight.duration }
    var landing: FlightPoint { flight.landing }

    /// `curve` tilts the spin axis for a draw or fade, in degrees; positive bends right.
    init(
        id: Int,
        club: GolfClub,
        power: Double,
        aim: Double,
        curve: Double = 0,
        strike: StrikeQuality = .center,
        course: GolfCourse = .easy
    ) {
        self.id = id
        self.club = club
        self.power = min(max(power.isFinite ? power : 0, 0), 1)
        self.aim = min(max(aim.isFinite ? aim : 0, -22), 22)
        self.strike = strike
        let strikeCurve: Double = switch strike {
        case .heel: 5.5
        case .toe: -5.5
        default: 0.0
        }
        self.curve = min(max((curve.isFinite ? curve : 0) + strikeCurve, -15), 15)
        let effectivePower = self.power * strike.efficiency
        flight = BallFlight.simulate(club.launch(power: effectivePower, aimDegrees: self.aim, curveDegrees: self.curve))
        lie = course.lie(at: flight.landing)
        points = lie.points
    }

    func position(at time: Double) -> FlightPoint { flight.position(at: time) }

    /// Power that lands a straight shot at `distance`, found by bisection; nil if out of reach.
    static func power(toReach distance: Double, with club: GolfClub) -> Double? {
        func total(_ power: Double) -> Double {
            BallFlight.simulate(club.launch(power: power, aimDegrees: 0, curveDegrees: 0)).total
        }
        guard distance >= total(0), distance <= total(1) else { return nil }
        var low = 0.0, high = 1.0
        for _ in 0..<24 {
            let mid = (low + high) / 2
            if total(mid) < distance { low = mid } else { high = mid }
        }
        return (low + high) / 2
    }
}

extension StrikeQuality {
    var efficiency: Double {
        switch self {
        case .center: 1
        case .thin: 0.76
        case .fat: 0.55
        case .heel, .toe: 0.80
        case .miss: 0.08
        }
    }
}
