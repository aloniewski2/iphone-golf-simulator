import Foundation

enum StrikeQuality: String, Sendable {
    case center, thin, fat, heel, toe, miss
    var displayName: String { rawValue.capitalized }
}

enum ShotShape: String, Sendable {
    case straight, draw, fade, hook, slice
    var displayName: String { rawValue.capitalized }
}

struct ShotResult: Equatable, Sendable {
    let club: GolfClub
    let strike: StrikeQuality
    let shape: ShotShape
    let ballSpeedMPH: Double
    let launchAngleDegrees: Double
    let directionDegrees: Double
    let curveDegrees: Double
    let carryYards: Double
    let rolloutYards: Double
    let apexYards: Double
    let confidence: Double
    var totalYards: Double { carryYards + rolloutYards }
}

struct ArcadeShotEngine: Sendable {
    let errorRetention = 0.35

    func calculate(metrics: SwingMetrics, club: GolfClub) -> ShotResult {
        let speed = metrics.normalizedWristSpeed
        // Reference-body wrist units per second, a virtual full-swing calibration.
        let power = speed.isFinite ? min(1, max(0, speed / 4)) : 0
        let strike = strikeQuality(from: metrics)
        let rawDirection = metrics.swingDirection.isFinite ? min(max(metrics.swingDirection * 8, -24), 24) : 0
        let direction = rawDirection * errorRetention
        let curve = club == .putter ? 0 : direction * 0.55
        let launch = club.launch(power: power, aimDegrees: direction, curveDegrees: curve,
                                 speedFactor: strike.efficiency)
        let flight = BallFlight.simulate(launch)

        return ShotResult(
            club: club, strike: strike, shape: shotShape(curve: curve), ballSpeedMPH: launch.ballSpeedMPH,
            launchAngleDegrees: launch.launchAngleDegrees, directionDegrees: direction, curveDegrees: curve,
            carryYards: flight.carry, rolloutYards: flight.roll, apexYards: flight.apex, confidence: metrics.confidence
        )
    }

    private func strikeQuality(from metrics: SwingMetrics) -> StrikeQuality {
        if metrics.confidence < 0.22 { return .miss }
        if metrics.impactHeightDelta > 0.032 { return .thin }
        if metrics.impactHeightDelta < -0.035 { return .fat }
        return .center
    }

    private func shotShape(curve: Double) -> ShotShape {
        switch curve {
        case ..<(-4): .hook
        case -4..<(-1.2): .draw
        case -1.2...1.2: .straight
        case 1.2...4: .fade
        default: .slice
        }
    }
}

