import Foundation
import simd

/// Physical ball flight: gravity, aerodynamic drag, Magnus lift from backspin (tilted for a draw
/// or fade), then bounce and roll on a fairway. Integrated once per shot and sampled for playback.
///
/// Units are SI inside, yards outside. Coefficients follow the usual golf-ball fits: drag rises and
/// lift grows with spin ratio, spin decays slowly in the air and is lost on the first bounce.
struct BallFlight: Equatable, Sendable {
    struct Surface {
        var height: Double = 0 // metres relative to the shot origin
        var slopeX: Double = 0
        var slopeZ: Double = 0
        var deceleration: Double = 3.2
        var restitution: Double = 0.35
        var bounceFriction: Double = 0.6
        var isWater = false
        var isSand = false
    }
    struct Trunk: Equatable, Sendable {
        let id: Int
        let center: simd_double2
        let base: Double
        let height: Double
        let radius: Double
    }
    struct Event: Equatable, Sendable {
        enum Kind: String, Sendable { case tree, bunkerLip, lipOut, cup, water }
        let kind: Kind
        let time: Double
        let obstacleID: Int?
    }
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
    var events: [Event] = []
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

    static func simulate(_ launch: Launch, windX: Double = 0, windZ: Double = 0,
                         trunks: [Trunk] = [], cup: simd_double2? = nil,
                         surface: ((Double,Double) -> Surface)? = nil) -> BallFlight {
        let mass = 0.04593
        let radius = 0.02135
        let area = Double.pi * radius * radius
        let airDensity = 1.225
        let gravity = 9.81
        let metersPerYard = 0.9144
        let dt = 1.0 / 240
        let wind = simd_double3(windX.isFinite ? max(-20, min(20, windX)) : 0, 0,
                               windZ.isFinite ? max(-20, min(20, windZ)) : 0)
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
        var events: [Event] = []
        var cupCooldown = 0.0
        var stopped = false

        while time < maxDuration {
            let before=position
            if airborne {
                let relativeVelocity = velocity - wind
                let v = simd_length(relativeVelocity)
                var acceleration = simd_double3(0, -gravity, 0)
                if v > 0.01 {
                    let spinRatio = radius * spin / v
                    let drag = 0.21 + 0.5 * spinRatio
                    let lift = min(0.33, 2.05 * spinRatio)
                    let dynamicPressure = 0.5 * airDensity * area * v * v
                    let direction = relativeVelocity / v
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
                let ground = surface?(position.x,position.z) ?? Surface()
                let normal=simd_normalize(simd_double3(-ground.slopeX,1,-ground.slopeZ))
                let incoming=simd_dot(velocity,normal)
                if position.y <= ground.height, (surface == nil ? velocity.y : incoming) < 0 {
                    if ground.isSand, ground.slopeX*velocity.x+ground.slopeZ*velocity.z > 0.5,
                       events.last?.kind != .bunkerLip {
                        events.append(Event(kind:.bunkerLip,time:time+dt,obstacleID:nil))
                    }
                    position.y = ground.height
                    if carryMeters == nil { carryMeters = simd_length(simd_double2(position.x, position.z)) }
                    if surface != nil {
                        velocity=(velocity-normal*incoming)*ground.bounceFriction-normal*incoming*ground.restitution
                    } else {
                        velocity.y = -velocity.y * restitution
                        velocity.x *= bounceFriction; velocity.z *= bounceFriction
                    }
                    spin = 0
                    if velocity.y < 1.2 {
                        velocity.y = 0
                        airborne = false
                        rolling = true
                    }
                }
            } else if rolling {
                if let surface {
                    let ground = surface(position.x,position.z)
                    let speed = hypot(velocity.x,velocity.z)
                    if speed < 0.01, hypot(ground.slopeX,ground.slopeZ)*gravity < ground.deceleration*0.9 { break }
                    var ax = -gravity*ground.slopeX, az = -gravity*ground.slopeZ
                    if speed > 0.00001 {
                        let friction=min(ground.deceleration,speed/dt)
                        ax -= friction*velocity.x/speed; az -= friction*velocity.z/speed
                    }
                    velocity.x += ax*dt; velocity.z += az*dt
                    position.x += velocity.x*dt; position.z += velocity.z*dt
                    position.y = surface(position.x,position.z).height
                } else {
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
            }
            // Swept trunk checks: do not skip a narrow trunk between high-speed frames.
            let a=simd_double2(before.x,before.z), b=simd_double2(position.x,position.z)
            let contacts=trunks.compactMap { tree -> (Trunk,GolfInteractions.CircleContact)? in
                guard let hit=GolfInteractions.sweptCircle(from:a,to:b,center:tree.center,radius:tree.radius+radius) else { return nil }
                let y=before.y+(position.y-before.y)*hit.fraction
                return y >= tree.base-radius && y <= tree.base+tree.height+radius ? (tree,hit) : nil
            }
            if let (tree,hit)=contacts.min(by:{$0.1.fraction < $1.1.fraction}) {
                let n=simd_double3(hit.normal.x,0,hit.normal.y)
                velocity=(velocity-n*(1.35*min(0,simd_dot(velocity,n))))*0.85
                position=simd_double3(hit.point.x,before.y+(position.y-before.y)*hit.fraction,hit.point.y)
                    + n*0.001 + velocity*dt*(1-hit.fraction)
                if events.last?.obstacleID != tree.id || time-(events.last?.time ?? 0)>0.05 {
                    events.append(Event(kind:.tree,time:time+dt,obstacleID:tree.id))
                }
            }
            if let surface {
                let ground=surface(position.x,position.z)
                if position.y <= ground.height+0.001, ground.isWater {
                    position.y=ground.height; stopped=true
                    events.append(Event(kind:.water,time:time+dt,obstacleID:nil))
                }
            }
            if !stopped, rolling, let cup, time >= cupCooldown {
                switch GolfInteractions.cup(from:a,to:simd_double2(position.x,position.z),velocity:simd_double2(velocity.x,velocity.z),center:cup) {
                case .none: break
                case .captured:
                    position.x=cup.x; position.z=cup.y
                    position.y=surface?(cup.x,cup.y).height ?? 0
                    velocity = .zero; stopped=true
                    events.append(Event(kind:.cup,time:time+dt,obstacleID:nil))
                case .lipOut(let p,let v):
                    position.x=p.x; position.z=p.y; position.y=surface?(p.x,p.y).height ?? 0
                    velocity.x=v.x; velocity.z=v.y; cupCooldown=time+0.12
                    events.append(Event(kind:.lipOut,time:time+dt,obstacleID:nil))
                }
            }
            time += dt
            if time + 1e-9 >= nextSample {
                samples.append(Sample(time: nextSample, point: FlightPoint(
                    lateralYards: position.x / metersPerYard,
                    heightYards: max(0, position.y - (surface?(position.x,position.z).height ?? 0)) / metersPerYard,
                    distanceYards: position.z / metersPerYard
                )))
                nextSample += sampleInterval
            }
            if stopped { break }
        }
        let finalDistance = simd_length(simd_double2(position.x, position.z))
        samples.append(Sample(time: nextSample, point: FlightPoint(
            lateralYards: position.x / metersPerYard, heightYards: 0, distanceYards: position.z / metersPerYard
        )))
        let carry = (carryMeters ?? 0) / metersPerYard // a shot that never flew is all roll
        return BallFlight(samples: samples, carry: carry, roll: max(0, finalDistance / metersPerYard - carry), apex: apexMeters / metersPerYard,events:events)
    }
}

extension GolfClub {
    /// Standard virtual bag, not a measurement of the empty-handed player's club speed.
    /// Woods/irons use carry; the putter uses total roll. Labels and flight share this calibration.
    var referenceDistanceYards: Double {
        switch self {
        case .driver: 250
        case .wood3: 210
        case .iron5: 180
        case .iron: 160
        case .iron9: 135
        case .wedge: 90
        case .putter: 25
        }
    }

    /// Calibrated once through the same aerodynamic/rolling solver, not a distance
    /// multiplier applied after landing. Every lower-power shot keeps real flight integration.
    var maxClubSpeedMPH: Double {
        Self.calibratedSpeeds[self]!
    }

    private static let calibratedSpeeds: [GolfClub: Double] = Dictionary(uniqueKeysWithValues: allCases.map { club in
        var low = 1.0, high = 145.0
        for _ in 0..<24 {
            let speed = (low + high) / 2
            let flight = BallFlight.simulate(.init(ballSpeedMPH: speed * club.smashFactor,
                launchAngleDegrees: club.launchAngleDegrees, spinRPM: club.spinRPM,
                directionDegrees: 0, curveDegrees: 0))
            let distance = club == .putter ? flight.total : flight.carry
            if distance < club.referenceDistanceYards { low = speed } else { high = speed }
        }
        return (club, (low + high) / 2)
    })

    var launchAngleDegrees: Double {
        switch self {
        case .driver: 12.5
        case .wood3: 14
        case .iron5: 16
        case .iron: 18
        case .iron9: 24
        case .wedge: 30
        case .putter: 0
        }
    }

    /// Backspin at full speed, rpm. Scales with club speed.
    var spinRPM: Double {
        switch self {
        case .driver: 2600
        case .wood3: 3500
        case .iron5: 4800
        case .iron: 6200
        case .iron9: 8000
        case .wedge: 9500
        case .putter: 0
        }
    }

    /// Ball speed from club speed and a fair strike.
    func launch(power: Double, aimDegrees: Double, curveDegrees: Double) -> BallFlight.Launch {
        let p = min(max(power.isFinite ? power : 0, 0), 1)
        let clubSpeed = maxClubSpeedMPH * p
        return BallFlight.Launch(
            ballSpeedMPH: clubSpeed * smashFactor,
            launchAngleDegrees: launchAngleDegrees,
            spinRPM: spinRPM * clubSpeed / maxClubSpeedMPH,
            directionDegrees: aimDegrees,
            curveDegrees: curveDegrees
        )
    }
}
