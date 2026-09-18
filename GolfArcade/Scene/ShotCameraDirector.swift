import simd

/// Decides where the course camera is: behind the golfer at address, low behind the ball on the
/// green, a hero shot of the golfer right after impact, a close chase of the ball, then a 3/4 view
/// as it lands. Pure, so every cut can be unit-tested; `CourseScene` smooths between frames.
enum ShotCameraDirector {
    enum Stage: Equatable, Sendable { case address, green, hero, chase, landing, roll, holeCam }

    struct Inputs {
        var ball: CoursePoint
        var heading: Double
        var aim: Double
        var distanceToPin: Double
        var onGreen: Bool
        var handedness: Handedness
        var shot: RangeShot?
        var elapsed: Double
        var reaction: AvatarAnimations.Reaction?
        var landingTime: Double?
        /// The cup, for the putting cameras.
        var pin: CoursePoint? = nil
    }

    /// A putt within this far of the cup, still rolling toward it, is watched from behind the hole.
    static let holeCamReach = 3.5

    struct Shot: Equatable {
        var stage: Stage
        var position: simd_float3
        var lookAt: simd_float3
        var fieldOfView: Float
        /// How quickly the camera settles on this framing (1/s). Stage changes into hero and chase cut.
        var damping: Float
    }

    /// Hero shot length: longer for better swings; putts go straight to the ball.
    static func heroDuration(reaction: AvatarAnimations.Reaction?, club: GolfClub, flightDuration: Double) -> Double {
        if club == .putter { return 0 }
        if flightDuration < 3 { return 0.6 }
        switch reaction {
        case .pure, .holed: return 1.8
        case .solid: return 1.5
        case .meh: return 1.2
        case .bad, .disaster, nil: return 1.0
        }
    }

    static func stage(_ inputs: Inputs) -> Stage {
        guard let shot = inputs.shot else { return inputs.onGreen ? .green : .address }
        if shot.club == .putter, shot.request.type == .putt, let pin = inputs.pin {
            // A putt is watched, not chased: the camera holds behind the ball, then, as the ball
            // closes on the cup, cuts to behind the hole to watch it arrive (the games' hole cam).
            let time = min(inputs.elapsed, shot.duration)
            let now = shot.position(at: time), soon = shot.position(at: min(shot.duration, time + 0.5))
            let here = CoursePoint(x: now.lateralYards, d: now.distanceYards)
            let next = CoursePoint(x: soon.lateralYards, d: soon.distanceYards)
            let closing = next.distance(to: pin) <= here.distance(to: pin) + 0.01
            if here.distance(to: pin) <= holeCamReach, closing || shot.isHoled || inputs.elapsed >= shot.duration { return .holeCam }
            return inputs.elapsed >= shot.duration ? .landing : .roll
        }
        if inputs.elapsed < heroDuration(reaction: inputs.reaction, club: shot.club, flightDuration: shot.duration) { return .hero }
        if let landing = inputs.landingTime, inputs.elapsed >= landing { return .landing }
        if inputs.elapsed >= shot.duration { return .landing }
        return .chase
    }

    static func shot(_ inputs: Inputs) -> Shot {
        let stage = stage(inputs)
        let mirror: Float = inputs.handedness == .right ? 1 : -1
        let scale = AvatarSize.courseScale
        switch stage {
        case .address:
            // Behind the ball on the side away from the golfer, so he stands clear on the left
            // (right for a left-hander) and can never come between the camera and the ball,
            // whatever his pose; the pair turn together about the ball as the line moves.
            let origin = inputs.shot?.origin ?? inputs.ball
            let center = simd_float3(-1.0 * mirror, 0, 0)
            let aim = aimDirection(inputs.aim)
            return Shot(
                stage: stage,
                position: world((simd_float3(1.2 * mirror, 10, 18)) * scale, origin: origin, heading: inputs.heading),
                lookAt: world((center + simd_float3(0, 1.0, 0) + aim * 12) * scale, origin: origin, heading: inputs.heading),
                fieldOfView: 50, damping: 5
            )
        case .green:
            // Low and a little behind the ball on the side away from the golfer, so the golfer
            // frames the left edge and the line to the hole is clear: ball, break, cup.
            let aim = aimDirection(inputs.aim)
            let reach = Float(min(max(inputs.distanceToPin, 4), 30))
            return Shot(
                stage: stage,
                position: world(simd_float3(2.6 * mirror, 5.0, 12 + reach * 0.12) * scale, origin: inputs.ball, heading: inputs.heading),
                lookAt: world(aim * Float(max(inputs.distanceToPin * 0.5, 3)) + simd_float3(0, 0.15, 0), origin: inputs.ball, heading: inputs.heading),
                fieldOfView: 50, damping: 4
            )
        case .hero:
            let shot = inputs.shot!
            let golfer = simd_float3(-3.2 * mirror, 0, 0)
            let facing = simd_float3(mirror, 0, 0)
            let angle: Float = 50 * .pi / 180
            let direction = facing * cos(angle) + simd_float3(0, 0, -1) * sin(angle)
            let duration = max(heroDuration(reaction: inputs.reaction, club: shot.club, flightDuration: shot.duration), 0.01)
            let push = Float(min(inputs.elapsed / duration, 1))
            let distance: Float = 16 - 3 * push
            return Shot(
                stage: stage,
                position: world((golfer + direction * distance + simd_float3(0, 4.5, 0)) * scale, origin: shot.origin, heading: shot.heading),
                lookAt: world((golfer + facing * 0.6 + simd_float3(0, 3.2, 0)) * scale, origin: shot.origin, heading: shot.heading),
                fieldOfView: 45, damping: 8
            )
        case .chase:
            let shot = inputs.shot!
            let ball = scenePoint(shot.position(at: inputs.elapsed))
            let direction = travelDirection(shot, at: inputs.elapsed)
            let height = ball.y
            return Shot(
                stage: stage,
                position: ball - direction * (7 + height * 0.12) + simd_float3(0, 2.2 + height * 0.1, 0),
                lookAt: ball + direction * 12 + simd_float3(0, -height * 0.2, 0),
                fieldOfView: 60, damping: 6
            )
        case .roll:
            // Where the putt was read from: low behind the ball's start, the whole line in view.
            let shot = inputs.shot!
            let aim = aimDirection(0)
            let reach = Float(min(max(inputs.distanceToPin, 4), 30))
            return Shot(
                stage: stage,
                position: world(simd_float3(2.6 * mirror, 5.0, 12 + reach * 0.12) * scale, origin: shot.origin, heading: shot.heading),
                lookAt: world(aim * Float(max(inputs.distanceToPin * 0.5, 3)) + simd_float3(0, 0.15, 0), origin: shot.origin, heading: shot.heading),
                fieldOfView: 50, damping: 4
            )
        case .holeCam:
            // Behind the hole, low, looking back along the line at the ball rolling in.
            let shot = inputs.shot!
            let pin = inputs.pin!
            let ball = scenePoint(shot.position(at: min(inputs.elapsed, shot.duration)))
            let cup = simd_float3(Float(pin.x), 0, -Float(pin.d))
            var away = cup - simd_float3(ball.x, 0, ball.z)
            if simd_length(away) < 0.05 { away = forwardDirection(heading: shot.heading + shot.aim) }
            away = simd_normalize(away)
            let side = simd_float3(-away.z, 0, away.x)
            return Shot(
                stage: stage,
                position: cup + away * 2.6 + side * 0.9 + simd_float3(0, 1.15, 0),
                lookAt: simd_mix(ball, cup, simd_float3(repeating: 0.35)) + simd_float3(0, 0.06, 0),
                fieldOfView: 48, damping: 5
            )
        case .landing:
            let shot = inputs.shot!
            let ball = scenePoint(shot.position(at: min(inputs.elapsed, shot.duration)))
            let forward = forwardDirection(heading: shot.heading + shot.aim)
            let right = simd_float3(-forward.z, 0, forward.x)
            return Shot(
                stage: stage,
                position: ball + right * 6 - forward * 5 + simd_float3(0, 3, 0),
                lookAt: ball + simd_float3(0, 0.5, 0),
                fieldOfView: 55, damping: 2
            )
        }
    }

    /// First time the ball comes back down after being airborne, or nil for a ball that only rolls.
    static func landingTime(of shot: RangeShot) -> Double? {
        var airborne = false
        var time = 0.05
        while time < shot.duration {
            let height = shot.position(at: time).heightYards
            if height > 0.5 { airborne = true }
            if airborne, height < 0.3 { return time }
            time += 1.0 / 30
        }
        return nil
    }

    // MARK: - Space

    /// Stance frame (x right of the line, y up, -z down the line) to scene coordinates.
    static func world(_ local: simd_float3, origin: CoursePoint, heading: Double) -> simd_float3 {
        let theta = Float(heading * .pi / 180)
        let x = local.x * cos(theta) - local.z * sin(theta)
        let z = local.x * sin(theta) + local.z * cos(theta)
        return simd_float3(Float(origin.x) + x, local.y, -Float(origin.d) + z)
    }

    static func scenePoint(_ point: FlightPoint) -> simd_float3 {
        simd_float3(Float(point.lateralYards), Float(point.heightYards) + Float(AvatarSize.courseBallRadius), -Float(point.distanceYards))
    }

    private static func aimDirection(_ aim: Double) -> simd_float3 {
        let a = Float(aim * .pi / 180)
        return simd_float3(sin(a), 0, -cos(a))
    }

    private static func forwardDirection(heading: Double) -> simd_float3 {
        let theta = Float(heading * .pi / 180)
        return simd_float3(sin(theta), 0, -cos(theta))
    }

    private static func travelDirection(_ shot: RangeShot, at time: Double) -> simd_float3 {
        let now = scenePoint(shot.position(at: time))
        let before = scenePoint(shot.position(at: max(0, time - 0.1)))
        var flat = now - before
        flat.y = 0
        return simd_length(flat) > 0.05 ? simd_normalize(flat) : forwardDirection(heading: shot.heading + shot.aim)
    }
}
