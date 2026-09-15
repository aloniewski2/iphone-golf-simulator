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
        let inferredClubSpeed = min(max(28 + metrics.normalizedWristSpeed * 24, 20), 125)
        let clubSpeed = inferredClubSpeed * club.speedMultiplier
        let strike = strikeQuality(from: metrics)
        let ballSpeed = clubSpeed * club.smashFactor * efficiency(for: strike)
        let rawDirection = min(max(metrics.swingDirection * 8, -24), 24)
        let direction = rawDirection * errorRetention
        let curve = direction * 0.55
        let launch = club == .putter ? 1.5 : max(4, club.loftDegrees * 0.72 + (strike == .thin ? -4 : strike == .fat ? 5 : 0))
        let launchRadians = launch * .pi / 180
        let carry: Double
        let rollout: Double
        let apex: Double

        if club == .putter {
            carry = 0
            rollout = min(max(ballSpeed * 0.85, 3), 55)
            apex = 0
        } else {
            let speedYardsFactor = ballSpeed * ballSpeed / 180
            carry = min(max(speedYardsFactor * sin(2 * launchRadians) * 1.55, 12), club == .driver ? 330 : 220)
            rollout = carry * (club == .driver ? 0.12 : club == .iron ? 0.07 : 0.03)
            apex = max(carry * tan(launchRadians) * 0.24, 3)
        }

        return ShotResult(
            club: club, strike: strike, shape: shotShape(curve: curve), ballSpeedMPH: ballSpeed,
            launchAngleDegrees: launch, directionDegrees: direction, curveDegrees: curve,
            carryYards: carry, rolloutYards: rollout, apexYards: apex, confidence: metrics.confidence
        )
    }

    private func strikeQuality(from metrics: SwingMetrics) -> StrikeQuality {
        if metrics.confidence < 0.22 { return .miss }
        if metrics.impactHeightDelta > 0.032 { return .thin }
        if metrics.impactHeightDelta < -0.035 { return .fat }
        return .center
    }

    private func efficiency(for strike: StrikeQuality) -> Double {
        switch strike {
        case .center: 1
        case .thin: 0.82
        case .fat: 0.68
        case .heel, .toe: 0.76
        case .miss: 0.42
        }
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

