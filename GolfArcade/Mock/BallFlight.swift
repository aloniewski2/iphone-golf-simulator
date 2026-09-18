import Foundation
import simd

/// Physical ball flight: gravity, aerodynamic drag, Magnus lift from backspin (tilted for a draw
/// or fade), then bounce and roll on a fairway. Integrated once per shot and sampled for playback.
///
/// Units are SI inside, yards outside. Coefficients follow the usual golf-ball fits: drag rises and
/// lift grows with spin ratio, spin decays slowly in the air and is lost on the first bounce.
struct BallFlight: Equatable, Sendable {
    struct Launch: Equatable, Sendable {
        var ballSpeedMPH: Double
        var launchAngleDegrees: Double
        var spinRPM: Double
        /// Initial direction, degrees right of the target line.
        var directionDegrees: Double
        /// Tilt of the spin axis, degrees. Positive curves right (fade/slice).
        var curveDegrees: Double
    }

    private struct Sample: Equatable, Sendable {
        let time: Double
        let point: FlightPoint
    }

    static let sampleInterval = 1.0 / 60
    static let maxDuration = 16.0

    private let samples: [Sample]
    let carry: Double
    let roll: Double
    let apex: Double
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

    static func simulate(_ launch: Launch) -> BallFlight {
        let mass = 0.04593
        let radius = 0.02135
        let area = Double.pi * radius * radius
        let airDensity = 1.225
        let gravity = 9.81
        let metersPerYard = 0.9144
        let dt = 1.0 / 240
        let restitution = 0.35
        let bounceFriction = 0.6
        let rollingDeceleration = 3.2

        let speed = max(0, launch.ballSpeedMPH) * 0.44704
        let elevation = launch.launchAngleDegrees * .pi / 180
        let azimuth = launch.directionDegrees * .pi / 180
        // x = right, y = up, z = downrange
        var position = simd_double3(0, 0, 0)
        var velocity = simd_double3(sin(azimuth) * cos(elevation), sin(elevation), cos(azimuth) * cos(elevation)) * speed
        var spin = max(0, launch.spinRPM) * 2 * .pi / 60
        let tilt = launch.curveDegrees * .pi / 180

        var samples: [Sample] = [Sample(time: 0, point: FlightPoint(lateralYards: 0, heightYards: 0, distanceYards: 0))]
        var time = 0.0
        var nextSample = sampleInterval
        var airborne = speed > 0.5 && elevation > 0.002 // a putt rolls from the first inch
        var rolling = !airborne
        var carryMeters: Double?
        var apexMeters = 0.0

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
                if position.y <= 0, velocity.y < 0 {
                    position.y = 0
                    if carryMeters == nil { carryMeters = simd_length(simd_double2(position.x, position.z)) }
                    velocity.y = -velocity.y * restitution
                    velocity.x *= bounceFriction
                    velocity.z *= bounceFriction
                    spin = 0
                    if velocity.y < 1.2 {
                        velocity.y = 0
                        airborne = false
                        rolling = true
                    }
                }
            } else if rolling {
                let horizontal = simd_double2(velocity.x, velocity.z)
                let ground = simd_length(horizontal)
                if ground <= 0 { break }
                let slowed = max(0, ground - rollingDeceleration * dt)
                let direction = horizontal / ground
                let movingTime = min(dt, ground / rollingDeceleration)
                let travel = ground * movingTime - 0.5 * rollingDeceleration * movingTime * movingTime
                velocity = simd_double3(direction.x * slowed, 0, direction.y * slowed)
                position += simd_double3(direction.x * travel, 0, direction.y * travel)
                position.y = 0
            }
            time += dt
            if time + 1e-9 >= nextSample {
                samples.append(Sample(time: nextSample, point: FlightPoint(
                    lateralYards: position.x / metersPerYard,
                    heightYards: max(0, position.y) / metersPerYard,
                    distanceYards: position.z / metersPerYard
                )))
                nextSample += sampleInterval
            }
        }
        let finalDistance = simd_length(simd_double2(position.x, position.z))
        samples.append(Sample(time: nextSample, point: FlightPoint(
            lateralYards: position.x / metersPerYard, heightYards: 0, distanceYards: position.z / metersPerYard
        )))
        let carry = (carryMeters ?? 0) / metersPerYard // a shot that never flew is all roll
        return BallFlight(samples: samples, carry: carry, roll: max(0, finalDistance / metersPerYard - carry), apex: apexMeters / metersPerYard)
    }
}

extension GolfClub {
    /// Standard virtual bag, not a measurement of the empty-handed player's club speed.
    /// Woods/irons use carry; the putter uses total roll. Labels and flight share this calibration.
    var referenceDistanceYards: Double {
        switch self {
        case .driver: 250
        case .iron: 160
        case .wedge: 90
        case .putter: 25
        }
    }

    /// Calibrated once through the same aerodynamic/rolling solver, not a distance
    /// multiplier applied after landing. Every lower-power shot keeps real flight integration.
    var maxClubSpeedMPH: Double {
        Self.calibration[self]!.maxSpeed
    }

    /// Club speed that flies (rolls, for the putter) `fraction` of the reference distance.
    /// Distance grows faster than linearly with speed, so this is what makes a half-filled
    /// meter a half-distance shot rather than a third of one.
    func clubSpeedMPH(meter fraction: Double) -> Double {
        let table = Self.calibration[self]!.distances
        let target = min(max(fraction, 0), 1) * referenceDistanceYards
        guard target > 0 else { return 0 }
        let step = maxClubSpeedMPH / Double(table.count - 1)
        var index = 1
        while index < table.count - 1, table[index] < target { index += 1 }
        let low = table[index - 1], high = table[index]
        let within = high > low ? min(max((target - low) / (high - low), 0), 1) : 1
        return (Double(index - 1) + within) * step
    }

    private struct Calibration {
        let maxSpeed: Double
        /// Distance at evenly spaced club speeds from rest to `maxSpeed`.
        let distances: [Double]
    }

    private static let calibration: [GolfClub: Calibration] = Dictionary(uniqueKeysWithValues: allCases.map { club in
        func distance(at speed: Double, spin: Double) -> Double {
            let flight = BallFlight.simulate(.init(ballSpeedMPH: speed * club.smashFactor,
                launchAngleDegrees: club.launchAngleDegrees, spinRPM: spin,
                directionDegrees: 0, curveDegrees: 0))
            return club == .putter ? flight.total : flight.carry
        }
        var low = 1.0, high = 145.0
        for _ in 0..<24 {
            let speed = (low + high) / 2
            if distance(at: speed, spin: club.spinRPM) < club.referenceDistanceYards { low = speed } else { high = speed }
        }
        let maxSpeed = (low + high) / 2
        // Sampled with the spin `launch` gives each speed, so the table inverts `launch` exactly.
        let steps = 40
        let distances = (0...steps).map { step -> Double in
            let speed = maxSpeed * Double(step) / Double(steps)
            return distance(at: speed, spin: club.spinRPM * speed / maxSpeed)
        }
        return (club, Calibration(maxSpeed: maxSpeed, distances: distances))
    })

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

    /// Shape of the meter. Full swings read distance straight, like every arcade golf meter:
    /// 0.5 on a 250-yard driver carries 125. The putter curves it (distance ∝ meter^1.5) so
    /// the short end has room: a tap-in is a small but readable stroke, a 30-footer a medium
    /// one, and the full stroke still rolls the rated 25 yards. Console games get the same
    /// effect by switching putter ranges; here the camera's arc resolution makes the curve the
    /// better fit.
    var meterExponent: Double { self == .putter ? 1.5 : 1 }

    /// Rated distance (carry, or total roll for the putter) at a meter reading. What the HUD
    /// quotes while the meter fills; `launch` flies exactly this on a fair strike.
    func distanceYards(meter power: Double) -> Double {
        let p = min(max(power.isFinite ? power : 0, 0), 1)
        return referenceDistanceYards * pow(p, meterExponent)
    }

    /// Launch for a meter reading: the club speed that flies `distanceYards(meter:)` comes
    /// from the calibration table, so every shot still goes through the full flight model.
    /// `speedFactor` then scales that speed for things that really do cost speed — a thin
    /// strike, a chip motion, a buried lie.
    func launch(power: Double, aimDegrees: Double, curveDegrees: Double, speedFactor: Double = 1) -> BallFlight.Launch {
        let p = min(max(power.isFinite ? power : 0, 0), 1)
        let factor = min(max(speedFactor.isFinite ? speedFactor : 0, 0), 1)
        let clubSpeed = clubSpeedMPH(meter: pow(p, meterExponent)) * factor
        return BallFlight.Launch(
            ballSpeedMPH: clubSpeed * smashFactor,
            launchAngleDegrees: launchAngleDegrees,
            spinRPM: spinRPM * clubSpeed / maxClubSpeedMPH,
            directionDegrees: aimDegrees,
            curveDegrees: curveDegrees
        )
    }
}
