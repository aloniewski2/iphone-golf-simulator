import Foundation
import simd

/// Physical ball flight: gravity, aerodynamic drag, Magnus lift from backspin (tilted for a draw
/// or fade), then bounce and roll over the ground. Integrated once per shot and sampled for playback.
///
/// Units are SI inside, yards outside. Coefficients follow the usual golf-ball fits: drag rises and
/// lift grows with spin ratio, spin decays slowly in the air and is lost on the first bounce.
/// The ground can be hilly: a bounce kicks off the slope it lands on, and a rolling ball is pulled
/// downhill, which is what makes a putt break.
struct BallFlight: Equatable, Sendable {
    /// The ground under a shot, in the shot's own frame and metres: height above the datum at a
    /// (right, downrange) position, and its rise per metre along those two axes.
    typealias Ground = (simd_double2) -> (height: Double, gradient: simd_double2)

    static func flatGround(_ position: simd_double2) -> (height: Double, gradient: simd_double2) { (0, .zero) }

    struct Launch: Equatable, Sendable {
        var ballSpeedMPH: Double
        var launchAngleDegrees: Double
        var spinRPM: Double
        /// Initial direction, degrees right of the target line.
        var directionDegrees: Double
        /// Tilt of the spin axis, degrees. Positive curves right (fade/slice).
        var curveDegrees: Double
    }

    struct Sample: Equatable, Sendable {
        let time: Double
        let point: FlightPoint
    }

    static let sampleInterval = 1.0 / 60
    static let maxDuration = 16.0
    /// Rolling deceleration on a firm fairway, m/s².
    static let fairwayRolling = 3.2

    let samples: [Sample]
    let carry: Double
    let roll: Double
    let apex: Double
    /// When the ball first touched the ground.
    let carryTime: Double
    var duration: Double { samples.last?.time ?? 0 }
    var total: Double { carry + roll }
    var landing: FlightPoint { samples.last?.point ?? FlightPoint(lateralYards: 0, heightYards: 0, distanceYards: 0) }

    func position(at time: Double) -> FlightPoint {
        guard let last = samples.last else { return FlightPoint(lateralYards: 0, heightYards: 0, distanceYards: 0) }
        guard time > 0 else { return samples[0].point }
        guard time < last.time else { return last.point }
        let index = time / Self.sampleInterval
        let lower = min(Int(index), samples.count - 2)
        let fraction = index - Double(lower)
        let a = samples[lower].point
        let b = samples[lower + 1].point
        return FlightPoint(
            lateralYards: a.lateralYards + (b.lateralYards - a.lateralYards) * fraction,
            heightYards: a.heightYards + (b.heightYards - a.heightYards) * fraction,
            distanceYards: a.distanceYards + (b.distanceYards - a.distanceYards) * fraction
        )
    }

    /// `rollingDeceleration` is the surface the ball rolls out on (green ≈ 0.9, fairway 3.2, rough 6).
    /// Heights in the samples are above the datum, so on flat ground they are heights above the grass.
    static func simulate(_ launch: Launch, rollingDeceleration: Double = fairwayRolling, ground: Ground = flatGround) -> BallFlight {
        let mass = 0.04593
        let radius = 0.02135
        let area = Double.pi * radius * radius
        let airDensity = 1.225
        let gravity = 9.81
        let metersPerYard = 0.9144
        let dt = 1.0 / 240
        let restitution = 0.35
        let bounceFriction = 0.6

        let speed = max(0, launch.ballSpeedMPH) * 0.44704
        let elevation = launch.launchAngleDegrees * .pi / 180
        let azimuth = launch.directionDegrees * .pi / 180
        // x = right, y = up, z = downrange
        let startHeight = ground(.zero).height
        var position = simd_double3(0, startHeight, 0)
        var velocity = simd_double3(sin(azimuth) * cos(elevation), sin(elevation), cos(azimuth) * cos(elevation)) * speed
        var spin = max(0, launch.spinRPM) * 2 * .pi / 60
        let tilt = launch.curveDegrees * .pi / 180

        var samples: [Sample] = [Sample(time: 0, point: FlightPoint(lateralYards: 0, heightYards: startHeight / metersPerYard, distanceYards: 0))]
        var time = 0.0
        var nextSample = sampleInterval
        var airborne = speed > 0.5 && elevation > 0.002 // a putt rolls from the first inch
        var rolling = !airborne
        var carryMeters: Double?
        var carryTime = 0.0
        var apexMeters = startHeight

        while time < maxDuration {
            if airborne {
                let v = simd_length(velocity)
                var acceleration = simd_double3(0, -gravity, 0)
                if v > 0.01 {
                    let spinRatio = radius * spin / v
                    let drag = 0.21 + 0.5 * spinRatio
                    let lift = min(0.33, 2.05 * spinRatio)
                    let dynamicPressure = 0.5 * airDensity * area * v * v
                    let direction = velocity / v
                    // Backspin axis lies flat and perpendicular to travel; tilting it adds sideways lift.
                    let flat = simd_normalize(simd_double3(-direction.z, 0, direction.x))
                    let axis = simd_normalize(flat * cos(tilt) + simd_double3(0, 1, 0) * sin(tilt))
                    let liftDirection = simd_cross(axis, direction)
                    acceleration += (-direction * drag + liftDirection * lift) * dynamicPressure / mass
                    spin *= exp(-dt / 25)
                }
                velocity += acceleration * dt
                position += velocity * dt
                apexMeters = max(apexMeters, position.y)
                let surface = ground(simd_double2(position.x, position.z))
                let normal = simd_normalize(simd_double3(-surface.gradient.x, 1, -surface.gradient.y))
                let into = simd_dot(velocity, normal)
                if position.y <= surface.height, into < 0 {
                    position.y = surface.height
                    if carryMeters == nil {
                        carryMeters = simd_length(simd_double2(position.x, position.z))
                        carryTime = time
                    }
                    // Bounce off the slope: the part of the speed into the ground comes back
                    // damped, the part along it is scrubbed by friction.
                    let along = velocity - normal * into
                    velocity = along * bounceFriction - normal * into * restitution
                    spin = 0
                    if -into * restitution < 1.2 {
                        velocity = simd_double3(along.x, 0, along.z) * bounceFriction
                        airborne = false
                        rolling = true
                    }
                }
            } else if rolling {
                let surface = ground(simd_double2(position.x, position.z))
                // Gravity pulls the ball down the slope, the grass slows it along its path.
                var horizontal = simd_double2(velocity.x, velocity.z) - gravity * surface.gradient * dt
                let speedAlong = simd_length(horizontal)
                if speedAlong < 0.05 { break }
                horizontal *= max(0, speedAlong - rollingDeceleration * dt) / speedAlong
                velocity = simd_double3(horizontal.x, 0, horizontal.y)
                position += velocity * dt
                position.y = ground(simd_double2(position.x, position.z)).height
            }
            time += dt
            if time + 1e-9 >= nextSample {
                samples.append(Sample(time: nextSample, point: FlightPoint(
                    lateralYards: position.x / metersPerYard,
                    heightYards: position.y / metersPerYard,
                    distanceYards: position.z / metersPerYard
                )))
                nextSample += sampleInterval
            }
        }
        let finalDistance = simd_length(simd_double2(position.x, position.z))
        samples.append(Sample(time: nextSample, point: FlightPoint(
            lateralYards: position.x / metersPerYard,
            heightYards: ground(simd_double2(position.x, position.z)).height / metersPerYard,
            distanceYards: position.z / metersPerYard
        )))
        let carry = (carryMeters ?? 0) / metersPerYard // a shot that never flew is all roll
        return BallFlight(samples: samples, carry: carry, roll: max(0, finalDistance / metersPerYard - carry), apex: (apexMeters - startHeight) / metersPerYard, carryTime: carryTime)
    }
}

extension GolfClub {
    /// Club-head speed at 100 % power, mph. Amateur-plus numbers; the arcade scales down from here.
    var maxClubSpeedMPH: Double {
        switch self {
        case .driver: 108
        case .iron: 88
        case .wedge: 72
        case .putter: 27
        }
    }

    var launchAngleDegrees: Double {
        switch self {
        case .driver: 12.5
        case .iron: 18
        case .wedge: 30
        case .putter: 0
        }
    }

    /// Backspin at full speed, rpm. Scales with club speed.
    var spinRPM: Double {
        switch self {
        case .driver: 2600
        case .iron: 6200
        case .wedge: 9500
        case .putter: 0
        }
    }

    /// Ball speed from club speed and a fair strike.
    func launch(power: Double, aimDegrees: Double, curveDegrees: Double) -> BallFlight.Launch {
        let p = min(max(power, 0), 1)
        let clubSpeed = maxClubSpeedMPH * (0.25 + 0.75 * p)
        return BallFlight.Launch(
            ballSpeedMPH: clubSpeed * smashFactor,
            launchAngleDegrees: launchAngleDegrees,
            spinRPM: spinRPM * clubSpeed / maxClubSpeedMPH,
            directionDegrees: aimDegrees,
            curveDegrees: curveDegrees
        )
    }
}
