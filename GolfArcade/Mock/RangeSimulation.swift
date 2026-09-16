import Foundation

/// Range targets, in yards.
struct RangeTarget: Identifiable, Equatable, Sendable {
    let id: Int
    let name: String
    let x: Double
    let distance: Double
    let radius: Double

    static let all = [
        RangeTarget(id: 0, name: "Cove", x: -8, distance: 45, radius: 13),
        RangeTarget(id: 1, name: "Grove", x: 9, distance: 100, radius: 19),
        RangeTarget(id: 2, name: "Summit", x: 0, distance: 180, radius: 25)
    ]
}

extension GolfClub {
    /// Total distance of a straight full-power shot, from the flight model. Shown on the club buttons.
    var mockDistance: Double { Self.maxDistances[self] ?? 0 }

    private static let maxDistances: [GolfClub: Double] = Dictionary(uniqueKeysWithValues: allCases.map {
        ($0, BallFlight.simulate($0.launch(power: 1, aimDegrees: 0, curveDegrees: 0)).total)
    })
}

/// One shot: a physically simulated flight from wherever the ball lies, plus what it earned.
/// Range shots start at the tee aimed downrange and score against the targets; course shots start
/// at the ball, aim at the pin, feel their lie, and can finish in the cup.
struct RangeShot: Identifiable, Equatable, Sendable {
    let id: Int
    let club: GolfClub
    let power: Double
    let aim: Double
    let curve: Double
    let flight: BallFlight
    let points: Int
    let targetName: String?
    /// Where the ball started, and the compass heading of the shot (0 = straight downrange).
    let origin: CoursePoint
    let heading: Double
    let lie: Lie
    /// Set when the ball drops in the cup part-way through its roll; playback stops there.
    let holedAt: Double?

    var carry: Double { flight.carry }
    var roll: Double { flight.roll }
    var apex: Double { flight.apex }
    var total: Double { flight.total }
    var duration: Double { holedAt ?? flight.duration }
    var isHoled: Bool { holedAt != nil }
    var landing: FlightPoint { position(at: duration) }
    /// Where the ball came to rest on the course.
    var restingPoint: CoursePoint { coursePoint(at: duration) }

    /// A range shot from the tee.
    init(id: Int, club: GolfClub, power: Double, aim: Double, curve: Double = 0) {
        self.id = id
        self.club = club
        self.power = min(max(power.isFinite ? power : 0, 0), 1)
        self.aim = min(max(aim.isFinite ? aim : 0, -22), 22)
        self.curve = min(max(curve.isFinite ? curve : 0, -15), 15)
        origin = CoursePoint(x: 0, z: 0)
        heading = 0
        lie = .tee
        holedAt = nil
        flight = BallFlight.simulate(club.launch(power: self.power, aimDegrees: self.aim, curveDegrees: self.curve))
        let end = flight.landing
        let nearest = RangeTarget.all.min {
            hypot(end.lateralYards - $0.x, end.distanceYards - $0.distance) / $0.radius
                < hypot(end.lateralYards - $1.x, end.distanceYards - $1.distance) / $1.radius
        }!
        let error = hypot(end.lateralYards - nearest.x, end.distanceYards - nearest.distance) / nearest.radius
        points = error <= 0.25 ? 100 : error <= 0.6 ? 60 : error <= 1 ? 30 : 10
        targetName = error <= 1 ? nearest.name : nil
    }

    /// A course shot from `origin` toward `heading`, from the given lie, on `hole`.
    init(id: Int, club: GolfClub, power: Double, aim: Double, curve: Double = 0, from origin: CoursePoint, heading: Double, lie: Lie, on hole: Hole) {
        self.id = id
        self.club = club
        self.power = min(max(power.isFinite ? power : 0, 0), 1)
        self.aim = min(max(aim.isFinite ? aim : 0, -22), 22)
        self.curve = min(max(curve.isFinite ? curve : 0, -15), 15)
        self.origin = origin
        self.heading = heading
        self.lie = lie
        var launch = club.launch(power: self.power, aimDegrees: self.aim, curveDegrees: self.curve)
        launch.ballSpeedMPH *= lie.strikeFactor
        if club == .putter, lie == .green {
            // A putt is a stroke, not a swing: no speed floor, so a soft pull is a tap-in and a full
            // pull crosses the whole green.
            launch.ballSpeedMPH = club.maxClubSpeedMPH * self.power * 0.55
        }
        // Roll out on the surface the ball lands on: simulate, see where it came down, resimulate.
        let first = BallFlight.simulate(launch, rollingDeceleration: lie.rollingDeceleration)
        let landingLie = hole.lie(at: Self.coursePoint(first.position(at: first.carryTime), origin: origin, heading: heading))
        let flight = landingLie.rollingDeceleration == lie.rollingDeceleration ? first : BallFlight.simulate(launch, rollingDeceleration: landingLie.rollingDeceleration)
        self.flight = flight
        // A rolling ball that passes over the cup drops in; one that stops beside it is given.
        let drop = flight.samples.first { sample in
            sample.time >= flight.carryTime && sample.point.heightYards <= 0.01
                && Self.coursePoint(sample.point, origin: origin, heading: heading).distance(to: hole.cup) <= hole.cupRadius
        }
        holedAt = drop?.time
        let rest = Self.coursePoint(flight.landing, origin: origin, heading: heading)
        let finalLie = hole.lie(at: rest)
        points = 0
        targetName = drop != nil ? "In the hole" : finalLie.displayName
    }

    func position(at time: Double) -> FlightPoint { flight.position(at: min(time, duration)) }

    /// Ball position on the course at `time`.
    func coursePoint(at time: Double) -> CoursePoint { Self.coursePoint(position(at: time), origin: origin, heading: heading) }

    /// Rotates a shot-local point (lateral, distance) onto the course.
    static func coursePoint(_ point: FlightPoint, origin: CoursePoint, heading: Double) -> CoursePoint {
        let radians = heading * .pi / 180
        return CoursePoint(
            x: origin.x + point.distanceYards * sin(radians) + point.lateralYards * cos(radians),
            z: origin.z - point.distanceYards * cos(radians) + point.lateralYards * sin(radians)
        )
    }

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
