import simd

/// Decides where the course camera is: behind the golfer at address, low behind the ball on the
/// green, a hero shot of the golfer right after impact, a close chase of the ball, then a 3/4 view
/// as it lands. Pure, so every cut can be unit-tested; `CourseScene` smooths between frames.
enum ShotCameraDirector {
    enum Stage: Equatable, Sendable { case address, green, hero, chase, landing, celebration }

    /// Let the cup drop read before showing the converted fist-pump performance.
    static func celebrationTime(shot: RangeShot, elapsed: Double) -> Double? {
        let time = elapsed - shot.duration - 0.45
        return shot.isHoled && time >= 0 && time < AvatarAnimations.reactionLength ? time : nil
    }

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
        var reduceMotion = false
        var liveCamera = false
    }

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
        if flightDuration < 3 { return min(0.4, flightDuration * 0.15) }
        switch reaction {
        case .pure, .holed: return 0.65
        case .solid: return 0.6
        case .meh: return 0.5
        case .bad, .disaster, nil: return 0.4
        }
    }

    static func stage(_ inputs: Inputs) -> Stage {
        guard let shot = inputs.shot else { return inputs.onGreen ? .green : .address }
        if !inputs.reduceMotion, !inputs.liveCamera,
           celebrationTime(shot: shot, elapsed: inputs.elapsed) != nil { return .celebration }
        if let landing = inputs.landingTime, inputs.elapsed >= landing { return .landing }
        if !inputs.reduceMotion, inputs.elapsed < heroDuration(reaction: inputs.reaction, club: shot.club, flightDuration: shot.duration) { return .hero }
        if inputs.elapsed >= shot.duration { return .landing }
        return .chase
    }

    static func shot(_ inputs: Inputs) -> Shot {
        let stage = stage(inputs)
        let mirror: Float = inputs.handedness == .right ? 1 : -1
        let scale = AvatarSize.courseScale
        if inputs.reduceMotion, let shot = inputs.shot {
            let start = scenePoint(shot.position(at: 0)), end = scenePoint(shot.position(at: shot.duration))
            let center = (start + end) / 2
            let reach = max(12, simd_distance(start,end) * 0.75 + Float(shot.apex))
            return Shot(stage: stage,position: center + simd_float3(reach*0.35,reach,reach*0.7),
                lookAt: center,fieldOfView: 55,damping: 5)
        }
        switch stage {
        case .address:
            let origin = inputs.shot?.origin ?? inputs.ball
            let center = simd_float3(-1.6 * mirror, 0, 0)
            let aim = aimDirection(inputs.aim)
            return Shot(
                stage: stage,
                position: world((center + simd_float3(10 * mirror, 10, 19)) * scale, origin: origin, heading: inputs.heading),
                lookAt: world((center + simd_float3(-4 * mirror, 1.0, 0) + aim * 12) * scale, origin: origin, heading: inputs.heading),
                fieldOfView: 50, damping: 4
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
        case .hero, .celebration:
            let shot = inputs.shot!
            if inputs.liveCamera {
                // Keep the same side of the player through measured impact/follow-through.
                // The previous orbit crossed the golfer in <0.5s, obscuring the live swing.
                var address=inputs
                address.ball=shot.origin;address.heading=shot.heading;address.shot=nil;address.onGreen=false
                var framing=Self.shot(address)
                framing.stage = .hero
                return framing
            }
            let golfer = simd_float3(-3.2 * mirror, 0, 0)
            let facing = simd_float3(mirror, 0, 0)
            let angle: Float = 50 * .pi / 180
            let direction = facing * cos(angle) + simd_float3(0, 0, -1) * sin(angle)
            let duration = max(heroDuration(reaction: inputs.reaction, club: shot.club, flightDuration: shot.duration), 0.01)
            let push = stage == .celebration ? Float(1) : Float(min(inputs.elapsed / duration, 1))
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
