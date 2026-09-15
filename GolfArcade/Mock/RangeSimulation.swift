import Foundation

/// Range targets, in yards.
struct RangeTarget: Identifiable, Equatable, Sendable {
    let id: Int
    let name: String
    let x: Double
    let distance: Double
    let radius: Double

    static let all = [
        RangeTarget(id: 0, name: "Cove", x: -8, distance: 45, radius: 13),
        RangeTarget(id: 1, name: "Grove", x: 9, distance: 100, radius: 19),
        RangeTarget(id: 2, name: "Summit", x: 0, distance: 180, radius: 25)
    ]
}

extension GolfClub {
    /// Total distance of a straight full-power shot, from the flight model. Shown on the club buttons.
    var mockDistance: Double { Self.maxDistances[self] ?? 0 }

    private static let maxDistances: [GolfClub: Double] = Dictionary(uniqueKeysWithValues: allCases.map {
        ($0, BallFlight.simulate($0.launch(power: 1, aimDegrees: 0, curveDegrees: 0)).total)
    })
}

/// One shot on the range: a physically simulated flight plus the score it earned.
struct RangeShot: Identifiable, Equatable, Sendable {
    let id: Int
    let club: GolfClub
    let power: Double
    let aim: Double
    let curve: Double
    let flight: BallFlight
    let points: Int
    let targetName: String?

    var carry: Double { flight.carry }
    var roll: Double { flight.roll }
    var apex: Double { flight.apex }
    var total: Double { flight.total }
    var duration: Double { flight.duration }
    var landing: FlightPoint { flight.landing }

    /// `curve` tilts the spin axis for a draw or fade, in degrees; positive bends right.
    init(id: Int, club: GolfClub, power: Double, aim: Double, curve: Double = 0) {
        self.id = id
        self.club = club
        self.power = min(max(power.isFinite ? power : 0, 0), 1)
        self.aim = min(max(aim.isFinite ? aim : 0, -22), 22)
        self.curve = min(max(curve.isFinite ? curve : 0, -15), 15)
        flight = BallFlight.simulate(club.launch(power: self.power, aimDegrees: self.aim, curveDegrees: self.curve))
        let end = flight.landing
        let nearest = RangeTarget.all.min {
            hypot(end.lateralYards - $0.x, end.distanceYards - $0.distance) / $0.radius
                < hypot(end.lateralYards - $1.x, end.distanceYards - $1.distance) / $1.radius
        }!
        let error = hypot(end.lateralYards - nearest.x, end.distanceYards - nearest.distance) / nearest.radius
        points = error <= 0.25 ? 100 : error <= 0.6 ? 60 : error <= 1 ? 30 : 10
        targetName = error <= 1 ? nearest.name : nil
    }

    func position(at time: Double) -> FlightPoint { flight.position(at: time) }

    /// Power that lands a straight shot at `distance`, found by bisection; nil if out of reach.
    static func power(toReach distance: Double, with club: GolfClub) -> Double? {
        func total(_ power: Double) -> Double {
            BallFlight.simulate(club.launch(power: power, aimDegrees: 0, curveDegrees: 0)).total
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
