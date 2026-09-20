import Foundation
import simd

enum ShotShapeChoice: String, CaseIterable, Codable, Identifiable, Sendable {
    case straight, draw, fade
    var id: Self { self }
    var title: String { rawValue.capitalized }
    func curve(handedness: Handedness) -> Double {
        let rightHanded: Double = self == .draw ? -8 : self == .fade ? 8 : 0
        return handedness == .right ? rightHanded : -rightHanded
    }
}

enum ShotTrajectory: String, CaseIterable, Codable, Identifiable, Sendable {
    case low, normal, high
    var id: Self { self }
    var title: String { rawValue.capitalized }
    var loftOffset: Double { self == .low ? -6 : self == .high ? 7 : 0 }
}

/// World horizontal velocity in metres/second. Frozen in each shot request.
struct CourseWind: Equatable, Codable, Sendable {
    var x: Double = 0
    var d: Double = 0
    static let calm = CourseWind()
    var speedMPH: Double { hypot(x, d) / 0.44704 }
    var bearing: Double { atan2(x, d) * 180 / .pi }
    func local(to heading: Double) -> (x: Double, z: Double) {
        let angle = heading * .pi / 180
        return (x * cos(angle) - d * sin(angle), x * sin(angle) + d * cos(angle))
    }
}

enum ShotType: String, CaseIterable, Identifiable, Sendable {
    case full, pitch, chip, bunker, putt
    var id: Self { self }
    var title: String { rawValue.capitalized }
    var speedGain: Double {
        switch self {
        case .full, .putt: 1
        case .pitch: 0.55
        case .chip: 0.28
        case .bunker: 0.72
        }
    }
    func supports(club: GolfClub,lie: CourseLie) -> Bool {
        switch self {
        case .putt: club == .putter
        case .full: club != .putter
        case .bunker: club == .wedge && lie == .bunker
        case .pitch: club == .wedge || club == .iron9
        case .chip: [.iron5,.iron,.iron9,.wedge].contains(club)
        }
    }
    func configure(_ launch: inout BallFlight.Launch) {
        switch self {
        case .pitch: launch.launchAngleDegrees=44; launch.spinRPM *= 0.9
        case .chip: launch.launchAngleDegrees=12; launch.spinRPM *= 0.35
        case .bunker: launch.launchAngleDegrees=52; launch.spinRPM *= 0.7
        case .full,.putt: break
        }
    }
}

/// Complete, replayable intent + execution contract. Target selection never rewrites execution.
struct ShotRequest: Equatable, Sendable {
    let club: GolfClub
    let targetHeading: Double
    let type: ShotType
    var execution: SwingImpact
    var shape: ShotShapeChoice = .straight
    var trajectory: ShotTrajectory = .normal
    var handedness: Handedness = .right
    var wind: CourseWind = .calm
    var simulationVersion = 2
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
        case .wood3: "3W"
        case .iron5: "5I"
        case .iron: "7I"
        case .iron9: "9I"
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
    /// The ball's path on the course, sampled at `BallFlight.sampleInterval`, once the ground's
    /// shape has had its say on the roll. Nil when shot without a hole.
    private let path: [FlightPoint]?
    private let pathRoll: Double?

    var carry: Double { flight.carry }
    /// Yards from where the ball first came down to where it stopped, as the crow flies.
    var roll: Double { pathRoll ?? flight.roll }
    var apex: Double { flight.apex }
    /// Yards from the ball's start to where it stopped, as the crow flies.
    var total: Double { path == nil ? flight.total : origin.distance(to: rest) }
    var duration: Double { holedAt ?? pathDuration }
    var isHoled: Bool { holedAt != nil }
    var penaltyStrokes: Int { isHoled ? 0 : lie?.penaltyStrokes ?? 0 }
    var explanation: String? {
        if penaltyStrokes > 0 { return lie == .water ? "Water · one penalty stroke and safe drop" : "Out of bounds · one penalty stroke, replay from previous spot" }
        if flight.events.contains(where:{$0.kind == .lipOut}) { return "Caught the cup lip" }
        if flight.events.contains(where:{$0.kind == .tree}) { return "Tree-trunk contact" }
        if flight.events.contains(where:{$0.kind == .bunkerLip}) { return "Caught the bunker lip" }
        switch strike {
        case .fat: return "Ground first"
        case .thin: return "Thin contact"
        case .heel,.toe: return "Off-center contact"
        case .miss: return "No club contact"
        case .center: return nil
        }
    }
    /// Final resting spot on the course (before any drop).
    var rest: CoursePoint {
        if isHoled { return nextPosition }
        if let last = path?.last { return CoursePoint(x: last.lateralYards, d: last.distanceYards) }
        return courseLocation(flight.landing)
    }
    /// World position of the rest, as a flight point, for tests and older callers.
    var landing: FlightPoint { position(at: pathDuration) }
    private var pathDuration: Double { path.map { Double($0.count - 1) * BallFlight.sampleInterval } ?? flight.duration }

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
            execution: SwingImpact(power: power, startLineDegrees: aim, curveDegrees: curve, strike: strike),
            simulationVersion:hole?.simulationVersion ?? 2
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
        let selectedCurve = request.type == .full ? request.shape.curve(handedness: request.handedness) : 0
        curve = club == .putter ? 0 : min(max((execution.curveDegrees.isFinite ? execution.curveDegrees : 0) + selectedCurve, -15), 15)
        let effectivePower = power * request.type.speedGain * strike.efficiency * min(max(lieFactor.isFinite ? lieFactor : 0, 0), 1)
        var launch = club.launch(power: effectivePower, aimDegrees: self.aim, curveDegrees: self.curve)
        request.type.configure(&launch)
        if club != .putter, request.type != .chip {
            launch.launchAngleDegrees = max(3, min(60, launch.launchAngleDegrees + request.trajectory.loftOffset))
        }
        if request.simulationVersion >= 4, club != .putter {
            if strike == .thin { launch.launchAngleDegrees=max(2,launch.launchAngleDegrees-7); launch.spinRPM *= 0.55 }
            if strike == .fat { launch.launchAngleDegrees=max(3,launch.launchAngleDegrees-4); launch.spinRPM *= 0.7 }
            if strike == .heel || strike == .toe {
                let side: Double = (strike == .toe ? -1 : 1) * (request.handedness == .right ? 1 : -1)
                launch.directionDegrees += side*1.5; launch.curveDegrees += side*2.5
            }
        }
        let wind = request.wind.local(to: heading)
        if let hole, hole.fairwayBoundary != nil || request.simulationVersion >= 4 {
            let geometry=ShotGeometry(origin:origin,heading:heading)
            let originHeight=hole.surface(at:origin).heightYards
            let angle=heading * .pi/180
            if club == .putter { launch.ballSpeedMPH *= sqrt(Self.greenDeceleration*0.9144/3.2) }
            func local(_ world: CoursePoint) -> simd_double2 {
                let dx=world.x-origin.x, dd=world.d-origin.d
                return simd_double2(dx*cos(angle)-dd*sin(angle),dx*sin(angle)+dd*cos(angle))*0.9144
            }
            let advanced=request.simulationVersion >= 4
            let trunks=advanced ? hole.trees.map { tree in
                BallFlight.Trunk(id:tree.id,center:local(tree.center),base:(hole.surface(at:tree.center).heightYards-originHeight)*0.9144,
                    height:tree.trunkHeight*0.9144,radius:tree.trunkRadius*0.9144)
            } : []
            flight = BallFlight.simulate(launch,windX:wind.x,windZ:wind.z,trunks:trunks,cup:advanced ? local(hole.pin) : nil) { x,z in
                let point=geometry.ground(FlightPoint(lateralYards:x/0.9144,heightYards:0,distanceYards:z/0.9144))
                let sample=hole.surface(at:point)
                let sand=sample.lie == .bunker
                return BallFlight.Surface(height:(sample.heightYards-originHeight)*0.9144,
                    slopeX:sample.slopeX*cos(angle)-sample.slopeD*sin(angle),
                    slopeZ:sample.slopeX*sin(angle)+sample.slopeD*cos(angle),
                    deceleration:Self.rollingDeceleration(on:sample.lie)*0.9144,
                    restitution:sand ? 0.08 : sample.lie == .green ? 0.25 : 0.35,
                    bounceFriction:sand ? 0.25 : sample.lie == .deepRough ? 0.35 : 0.6,
                    isWater:advanced && sample.lie == .water,isSand:advanced && sand)
            }
        } else {
            flight = BallFlight.simulate(launch, windX: wind.x, windZ: wind.z)
        }

        guard let hole else {
            lie = nil
            holedAt = nil
            nextPosition = ShotGeometry(origin: origin, heading: self.heading).ground(flight.landing)
            path = nil
            pathRoll = nil
            return
        }
        // A whiff is a stroke, but never a launch, penalty/drop, or automatic cup capture.
        guard strike != .miss, effectivePower > 0 else {
            lie = hole.lie(at: origin)
            holedAt = nil
            nextPosition = origin
            path = nil
            pathRoll = nil
            return
        }
        let geometry = ShotGeometry(origin: origin, heading: self.heading)
        let grounded: [FlightPoint]
        if hole.fairwayBoundary != nil || request.simulationVersion >= 4 {
            let recordedFlight = flight
            let count=Int(ceil(recordedFlight.duration/BallFlight.sampleInterval))
            grounded=(0...count).map { geometry.world(recordedFlight.position(at:Double($0)*BallFlight.sampleInterval)) }
        } else {
            grounded=Self.groundPath(flight: flight, geometry: geometry, hole: hole, putt: club == .putter)
        }
        path = grounded
        let resolved=Self.resolve(path:grounded,origin:origin,hole:hole,legacyCup:request.simulationVersion < 4)
        let captured=flight.events.first(where:{$0.kind == .cup})
        let (lie,holedAt,next) = captured.map { (CourseLie.green,Optional($0.time),hole.pin) } ?? resolved
        self.lie = lie
        self.holedAt = holedAt
        nextPosition = next
        // Roll runs from the first touchdown after flight (the start, for a putt) to the rest.
        var touchdown = origin
        for index in 1..<grounded.count where grounded[index - 1].heightYards > 0.03 && grounded[index].heightYards <= 0.03 {
            touchdown = CoursePoint(x: grounded[index].lateralYards, d: grounded[index].distanceYards)
            break
        }
        let stopped = holedAt != nil ? hole.pin : CoursePoint(x: grounded[grounded.count - 1].lateralYards, d: grounded[grounded.count - 1].distanceYards)
        pathRoll = stopped.distance(to: touchdown)
    }

    /// Position on the course (world yards) at `time` seconds into the flight.
    func position(at time: Double) -> FlightPoint {
        if isHoled, time >= duration {
            return FlightPoint(lateralYards: nextPosition.x, heightYards: 0, distanceYards: nextPosition.d)
        }
        guard let path else {
            return ShotGeometry(origin: origin, heading: heading).world(flight.position(at: min(time, duration)))
        }
        let clamped = min(max(time, 0), duration)
        let index = clamped / BallFlight.sampleInterval
        let lower = min(Int(index), path.count - 1)
        guard lower + 1 < path.count else { return path[lower] }
        let fraction = index - Double(lower)
        let a = path[lower], b = path[lower + 1]
        return FlightPoint(lateralYards: a.lateralYards + (b.lateralYards - a.lateralYards) * fraction,
                           heightYards: a.heightYards + (b.heightYards - a.heightYards) * fraction,
                           distanceYards: a.distanceYards + (b.distanceYards - a.distanceYards) * fraction)
    }

    /// Yards per second squared: gravity, and the flight model's rolling friction, in course units.
    private static let gravityYards = 9.81 / 0.9144
    /// The flight model's roll is a slow fairway. Real greens are far quicker, so a putt spends
    /// long enough rolling for the ground to move it; rough and sand grab the ball.
    static let fairwayDeceleration = 3.2 / 0.9144
    static let greenDeceleration = 1.5
    static func rollingDeceleration(on lie: CourseLie) -> Double {
        switch lie {
        case .green: greenDeceleration
        case .fringe: greenDeceleration * 1.5
        case .rough, .outOfBounds: fairwayDeceleration * 1.6
        case .deepRough: fairwayDeceleration * 2.1
        case .bunker: fairwayDeceleration * 2.5
        case .water: fairwayDeceleration * 4
        case .tee, .fairway: fairwayDeceleration
        }
    }

    /// Puts the flight on the course, then lets the ground steer the roll: the flight model's
    /// bounces are kept as they are, but from the moment the ball is rolling it accelerates down
    /// any slope it is on and is slowed by the grass it is on. Uphill comes up short, downhill
    /// runs on, a side slope breaks the line; a ball on a steep enough face keeps going.
    /// A putt's start speed is scaled for the green's pace so the club's calibrated distances
    /// hold on level ground; the extra time on the way is where the break comes from.
    static func groundPath(flight: BallFlight, geometry: ShotGeometry, hole: Hole, putt: Bool) -> [FlightPoint] {
        let step = BallFlight.sampleInterval
        let count = Int((flight.duration / step).rounded()) + 1
        var path: [FlightPoint] = (0..<count).map { geometry.world(flight.position(at: Double($0) * step)) }
        // Rolling starts after the last sample that was clearly off the ground.
        var rollingFrom = 0
        for (index, point) in path.enumerated() where point.heightYards > 0.03 { rollingFrom = index + 1 }
        guard rollingFrom + 1 < path.count else { return path }
        let start = path[rollingFrom]
        let next = path[rollingFrom + 1]
        var x = start.lateralYards, d = start.distanceYards
        var vx = (next.lateralYards - x) / step, vd = (next.distanceYards - d) / step
        if putt {
            let pace = sqrt(rollingDeceleration(on: hole.lie(at: CoursePoint(x: x, d: d))) / fairwayDeceleration)
            vx *= pace
            vd *= pace
        }
        path.removeSubrange((rollingFrom + 1)...)
        let terrain = hole.terrain
        let substeps = 4
        let dt = step / Double(substeps)
        var elapsed = Double(rollingFrom) * step
        while elapsed < BallFlight.maxDuration {
            for _ in 0..<substeps {
                let point = CoursePoint(x: x, d: d)
                let slope = terrain.gradient(at: point)
                let deceleration = rollingDeceleration(on: hole.lie(at: point))
                let speed = hypot(vx, vd)
                let downhill = hypot(slope.dx, slope.dd) * gravityYards
                if speed < 0.02, downhill < deceleration * 0.9 { vx = 0; vd = 0; break }
                var ax = -gravityYards * slope.dx, ad = -gravityYards * slope.dd
                if speed > 0.0001 {
                    let friction = min(deceleration, speed / dt)
                    ax -= friction * vx / speed
                    ad -= friction * vd / speed
                }
                vx += ax * dt
                vd += ad * dt
                x += vx * dt
                d += vd * dt
            }
            elapsed += step
            path.append(FlightPoint(lateralYards: x, heightYards: 0, distanceYards: d))
            if vx == 0, vd == 0 { break }
        }
        return path
    }

    private func courseLocation(_ local: FlightPoint) -> CoursePoint {
        let world = ShotGeometry(origin: origin, heading: heading).world(local)
        return CoursePoint(x: world.lateralYards, d: world.distanceYards)
    }

    /// Walks the sampled path: a slow ball crossing the cup drops; any ground contact in water is
    /// wet even if the ball would have skipped out; out of bounds replays from the same spot.
    private static func resolve(path: [FlightPoint], origin: CoursePoint, hole: Hole, legacyCup: Bool = true) -> (CourseLie, Double?, CoursePoint) {
        let step = BallFlight.sampleInterval
        var previous = CoursePoint(x: path[0].lateralYards, d: path[0].distanceYards)
        for index in 1..<max(1, path.count) {
            let previousTime = Double(index - 1) * step
            let time = Double(index) * step
            let local = path[index]
            let point = CoursePoint(x: local.lateralYards, d: local.distanceYards)
            let onGround = local.heightYards < 0.05
            if onGround {
                let dx = point.x - previous.x, dd = point.d - previous.d
                let lengthSquared = dx * dx + dd * dd
                let fraction = lengthSquared > 0 ? min(1, max(0, ((hole.pin.x - previous.x) * dx + (hole.pin.d - previous.d) * dd) / lengthSquared)) : 0
                let closest = CoursePoint(x: previous.x + fraction * dx, d: previous.d + fraction * dd)
                let speed = sqrt(lengthSquared) / max(time - previousTime, 0.0001)
                if legacyCup, closest.distance(to: hole.pin) <= Hole.cupCaptureRadius, speed < 2 {
                    return (.green, previousTime + fraction * (time - previousTime), hole.pin)
                }
                if hole.hazards.contains(where: { $0.kind == .water && $0.contains(point) }) {
                    return (.water, nil, drop(from: point, toward: origin, hole: hole))
                }
            }
            previous = point
        }
        let last = path[path.count - 1]
        let rest = CoursePoint(x: last.lateralYards, d: last.distanceYards)
        let duration = Double(path.count - 1) * step
        if legacyCup, rest.distance(to: hole.pin) <= Hole.cupCaptureRadius { return (.green, duration, hole.pin) }
        let lie = hole.lie(at: rest)
        switch lie {
        case .water: return (.water, nil, drop(from: rest, toward: origin, hole: hole))
        case .outOfBounds: return (.outOfBounds, nil, origin)
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
                let candidate=CoursePoint(x: point.x + ux * back, d: point.d + ud * back)
                if hole.lie(at:candidate).penaltyStrokes == 0 { return candidate }
            }
        }
        return origin
    }

    /// Power that lands a straight shot at `distance`, found by bisection; nil if out of reach.
    static func power(toReach distance: Double, with club: GolfClub, type: ShotType = .full, lieFactor: Double = 1) -> Double? {
        func total(_ power: Double) -> Double {
            var launch=club.launch(power: power * (club == .putter ? 1 : type.speedGain * lieFactor), aimDegrees: 0, curveDegrees: 0)
            type.configure(&launch)
            return BallFlight.simulate(launch).total
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

/// A recommendation, never a correction applied after impact. Runs the SAME terrain
/// solver used by the actual stroke, searching line and pace together. Bounded search
/// may miss a solution on extreme greens; `missYards` makes that limitation explicit.
struct PuttRecommendation: Equatable, Sendable {
    let offsetDegrees: Double
    let power: Double
    let missYards: Double

    static func solve(from origin: CoursePoint, hole: Hole) -> Self {
        let base = origin.heading(to: hole.pin)
        let seed = RangeShot.power(toReach: origin.distance(to: hole.pin), with: .putter) ?? 1
        func evaluate(_ offset: Double, _ power: Double) -> Self {
            let shot = RangeShot(id: 0, club: .putter, power: power, aim: 0,
                origin: origin, heading: base + offset, hole: hole)
            return Self(offsetDegrees: offset, power: power, missYards: shot.rest.distance(to: hole.pin))
        }
        var best = evaluate(0, seed)
        // Prefer lower-power solutions among equally good candidates. Do not use a
        // widened cup or a pin-directed force to make the recommendation succeed.
        func better(_ a: Self, than b: Self) -> Bool {
            a.missYards < b.missYards - 0.00001 ||
                (abs(a.missYards - b.missYards) < 0.00001 && a.power < b.power)
        }
        for offset in [-36.0, -18, 0, 18, 36] {
            for factor in [0.65, 0.85, 1.0, 1.2, 1.45] {
                if Task.isCancelled { return best }
                let candidate = evaluate(offset, min(1, max(0.001, seed * factor)))
                if better(candidate, than: best) { best = candidate }
            }
        }
        var angleStep = 9.0, powerStep = max(0.015, seed * 0.18)
        for _ in 0..<8 {
            let center = best
            for a in [-1.0, 0, 1] { for p in [-1.0, 0, 1] {
                if Task.isCancelled { return best }
                let candidate = evaluate(max(-60, min(60, center.offsetDegrees + a * angleStep)),
                    min(1, max(0.001, center.power + p * powerStep)))
                if better(candidate, than: best) { best = candidate }
            } }
            angleStep *= 0.5; powerStep *= 0.5
        }
        return best
    }
}
