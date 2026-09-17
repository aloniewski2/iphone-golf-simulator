import Foundation

extension AvatarAnimations.Reaction {
    /// How the golfer reacts, from the strike and where the ball finished.
    static func classify(_ shot: RangeShot) -> Self {
        if shot.isHoled { return .holed }
        let lie = shot.lie ?? .fairway
        if shot.strike == .miss || lie == .water || lie == .outOfBounds { return .disaster }
        if shot.strike == .fat || lie == .bunker { return .bad }
        let goodLie = lie == .fairway || lie == .green || lie == .tee
        if shot.strike == .center, goodLie, efficiency(shot) >= 0.7 { return .pure }
        // Rough, or a thin/heel/toe strike that came up well short.
        if goodLie, efficiency(shot) >= 0.5 { return .solid }
        return .meh
    }

    /// Distance achieved against a clean strike with the same club and power.
    static func efficiency(_ shot: RangeShot) -> Double {
        let expected = BallFlight.simulate(shot.club.launch(power: shot.power, aimDegrees: 0, curveDegrees: 0)).total
        return expected > 0.5 ? shot.total / expected : 1
    }
}
