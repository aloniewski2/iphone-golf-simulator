import Foundation

enum ShotType: String, CaseIterable, Identifiable, Sendable {
    case full, pitch, chip, putt
    var id: Self { self }
    var title: String { rawValue.capitalized }
    var speedGain: Double {
        switch self {
        case .full, .putt: 1
        case .pitch: 0.55
        case .chip: 0.28
        }
    }
}

/// Complete, replayable intent + execution contract. Target selection never rewrites execution.
struct ShotRequest: Equatable, Sendable {
    let club: GolfClub
    let targetHeading: Double
    let type: ShotType
    let execution: SwingImpact
}

extension GolfClub {
    /// Total distance of a straight full-power shot, from the flight model. Shown on the club buttons.
    var mockDistance: Double { Self.maxDistances[self] ?? 0 }

    private static let maxDistances: [GolfClub: Double] = Dictionary(uniqueKeysWithValues: allCases.map {
        ($0, BallFlight.simulate($0.launch(power: 1, aimDegrees: 0, curveDegrees: 0)).total)
    })

    var shortName: String {
        switch self {
        case .driver: "DR"
        case .iron: "7I"
        case .wedge: "SW"
        case .putter: "PT"
        }
    }
}

/// One stroke: a physically simulated flight from `origin`, pointed along `heading`, plus where
/// it finished on the hole. The flight model always runs in its own tee-at-zero frame; this type
/// rotates and moves it onto the course so the physics stay untouched.
struct RangeShot: Identifiable, Equatable, Sendable {
    let id: Int
    let request: ShotRequest
    let club: GolfClub
    let power: Double
    let aim: Double
    let curve: Double
    let strike: StrikeQuality
    let flight: BallFlight
    let origin: CoursePoint
    /// Degrees right of straight down the hole that `aim` is measured from (normally at the pin).
    let heading: Double
    /// Where the ball finished, on the hole's terms. Nil when shot without a hole.
    let lie: CourseLie?
    /// Seconds into the flight at which the ball dropped into the cup.
    let holedAt: Double?
    /// Where the next stroke is played from, after any drop.
    let nextPosition: CoursePoint

    var carry: Double { flight.carry }
    var roll: Double { flight.roll }
    var apex: Double { flight.apex }
    var total: Double { flight.total }
    var duration: Double { holedAt ?? flight.duration }
    var isHoled: Bool { holedAt != nil }
    var penaltyStrokes: Int { isHoled ? 0 : lie?.penaltyStrokes ?? 0 }
    /// Final resting spot on the course (before any drop).
    var rest: CoursePoint { isHoled ? nextPosition : courseLocation(flight.landing) }
    /// World position of the rest, as a flight point, for tests and older callers.
    var landing: FlightPoint { position(at: flight.duration) }

    /// `curve` tilts the spin axis for a draw or fade, in degrees; positive bends right.
    /// `lieFactor` scales power for the lie the ball is played from.
    init(
        id: Int,
        club: GolfClub,
        power: Double,
        aim: Double,
        curve: Double = 0,
        strike: StrikeQuality = .center,
        origin: CoursePoint = .zero,
        heading: Double = 0,
        lieFactor: Double = 1,
        hole: Hole? = nil
    ) {
        self.init(id: id, request: ShotRequest(
            club: club, targetHeading: heading, type: club == .putter ? .putt : .full,
            execution: SwingImpact(power: power, startLineDegrees: aim, curveDegrees: curve, strike: strike)
        ), origin: origin, lieFactor: lieFactor, hole: hole)
    }

    init(id: Int, request: ShotRequest, origin: CoursePoint, lieFactor: Double = 1, hole: Hole? = nil) {
        self.request = request
        self.id = id
        club = request.club
        let execution = request.execution
        power = min(max(execution.power.isFinite ? execution.power : 0, 0), 1)
        aim = execution.startLineDegrees.isFinite ? execution.startLineDegrees.truncatingRemainder(dividingBy: 360) : 0
        strike = execution.strike
        self.origin = origin
        heading = request.targetHeading.isFinite ? request.targetHeading : 0
        curve = club == .putter ? 0 : min(max(execution.curveDegrees.isFinite ? execution.curveDegrees : 0, -15), 15)
        let effectivePower = power * request.type.speedGain * strike.efficiency * min(max(lieFactor.isFinite ? lieFactor : 0, 0), 1)
        flight = BallFlight.simulate(club.launch(power: effectivePower, aimDegrees: self.aim, curveDegrees: self.curve))

        guard let hole else {
            lie = nil
            holedAt = nil
            nextPosition = ShotGeometry(origin: origin, heading: self.heading).ground(flight.landing)
            return
        }
        // A whiff is a stroke, but never a launch, penalty/drop, or automatic cup capture.
        guard strike != .miss, effectivePower > 0 else {
            lie = hole.lie(at: origin)
            holedAt = nil
            nextPosition = origin
            return
        }
        let geometry = ShotGeometry(origin: origin, heading: self.heading)
        let (lie, holedAt, next) = Self.resolve(flight: flight, geometry: geometry, hole: hole)
        self.lie = lie
        self.holedAt = holedAt
        nextPosition = next
    }

    /// Position on the course (world yards) at `time` seconds into the flight.
    func position(at time: Double) -> FlightPoint {
        if isHoled, time >= duration {
            return FlightPoint(lateralYards: nextPosition.x, heightYards: 0, distanceYards: nextPosition.d)
        }
        return ShotGeometry(origin: origin, heading: heading).world(flight.position(at: min(time, duration)))
    }

    private func courseLocation(_ local: FlightPoint) -> CoursePoint {
        let world = ShotGeometry(origin: origin, heading: heading).world(local)
        return CoursePoint(x: world.lateralYards, d: world.distanceYards)
    }

    /// Walks the sampled flight: a slow ball crossing the cup drops; any ground contact in water is
    /// wet even if the ball would have skipped out; out of bounds replays from the same spot.
    private static func resolve(flight: BallFlight, geometry: ShotGeometry, hole: Hole) -> (CourseLie, Double?, CoursePoint) {
        let step = BallFlight.sampleInterval
        var time = 0.0
        var previous = geometry.ground(flight.position(at: 0))
        while time < flight.duration {
            let previousTime = time
            time = min(time + step, flight.duration)
            let local = flight.position(at: time)
            let point = geometry.ground(local)
            let onGround = local.heightYards < 0.05
            if onGround {
                let dx = point.x - previous.x, dd = point.d - previous.d
                let lengthSquared = dx * dx + dd * dd
                let fraction = lengthSquared > 0 ? min(1, max(0, ((hole.pin.x - previous.x) * dx + (hole.pin.d - previous.d) * dd) / lengthSquared)) : 0
                let closest = CoursePoint(x: previous.x + fraction * dx, d: previous.d + fraction * dd)
                let speed = sqrt(lengthSquared) / max(time - previousTime, 0.0001)
                if closest.distance(to: hole.pin) <= Hole.cupCaptureRadius, speed < 2 {
                    return (.green, previousTime + fraction * (time - previousTime), hole.pin)
                }
                if hole.hazards.contains(where: { $0.kind == .water && $0.contains(point) }) {
                    return (.water, nil, drop(from: point, toward: geometry.origin, hole: hole))
                }
            }
            previous = point
        }
        let rest = geometry.ground(flight.landing)
        if rest.distance(to: hole.pin) <= Hole.cupCaptureRadius { return (.green, flight.duration, hole.pin) }
        let lie = hole.lie(at: rest)
        switch lie {
        case .water: return (.water, nil, drop(from: rest, toward: geometry.origin, hole: hole))
        case .outOfBounds: return (.outOfBounds, nil, geometry.origin)
        default: return (lie, nil, rest)
        }
    }

    /// Steps back along the line of play until clear of water, plus a club-length.
    private static func drop(from point: CoursePoint, toward origin: CoursePoint, hole: Hole) -> CoursePoint {
        let length = point.distance(to: origin)
        guard length > 0.5 else { return origin }
        let ux = (origin.x - point.x) / length, ud = (origin.d - point.d) / length
        var travelled = 0.0
        var spot = point
        while travelled < length {
            travelled += 1
            spot = CoursePoint(x: point.x + ux * travelled, d: point.d + ud * travelled)
            if !hole.hazards.contains(where: { $0.kind == .water && $0.contains(spot) }) {
                let back = min(travelled + 2, length)
                return CoursePoint(x: point.x + ux * back, d: point.d + ud * back)
            }
        }
        return origin
    }

    /// Power that lands a straight shot at `distance`, found by bisection; nil if out of reach.
    static func power(toReach distance: Double, with club: GolfClub, type: ShotType = .full, lieFactor: Double = 1) -> Double? {
        func total(_ power: Double) -> Double {
            BallFlight.simulate(club.launch(power: power * (club == .putter ? 1 : type.speedGain * lieFactor), aimDegrees: 0, curveDegrees: 0)).total
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

/// Maps the flight model's frame (x right, z downrange from zero) onto the course.
struct ShotGeometry: Equatable, Sendable {
    let origin: CoursePoint
    let heading: Double

    func world(_ local: FlightPoint) -> FlightPoint {
        let radians = heading * .pi / 180
        let c = cos(radians), s = sin(radians)
        return FlightPoint(
            lateralYards: origin.x + local.lateralYards * c + local.distanceYards * s,
            heightYards: local.heightYards,
            distanceYards: origin.d - local.lateralYards * s + local.distanceYards * c
        )
    }

    func ground(_ local: FlightPoint) -> CoursePoint {
        let point = world(local)
        return CoursePoint(x: point.lateralYards, d: point.distanceYards)
    }
}

extension StrikeQuality {
    var efficiency: Double {
        switch self {
        case .center: 1
        case .thin: 0.76
        case .fat: 0.55
        case .heel, .toe: 0.80
        case .miss: 0
        }
    }
}
