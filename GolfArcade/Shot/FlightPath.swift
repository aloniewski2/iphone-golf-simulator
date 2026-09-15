import Foundation

struct FlightPoint: Equatable, Sendable {
    let lateralYards: Double
    let heightYards: Double
    let distanceYards: Double
}

struct FlightPath: Equatable, Sendable {
    let points: [FlightPoint]

    init(shot: ShotResult, sampleCount: Int = 48) {
        guard shot.carryYards > 0 else {
            points = (0..<max(sampleCount, 2)).map { index in
                let progress = Double(index) / Double(max(sampleCount - 1, 1))
                return FlightPoint(lateralYards: 0, heightYards: 0, distanceYards: shot.rolloutYards * progress)
            }
            return
        }
        points = (0..<max(sampleCount, 2)).map { index in
            let progress = Double(index) / Double(max(sampleCount - 1, 1))
            let distance = shot.carryYards * progress
            let arc = 4 * shot.apexYards * progress * (1 - progress)
            let startLine = tan(shot.directionDegrees * .pi / 180) * distance
            let curve = tan(shot.curveDegrees * .pi / 180) * distance * progress
            return FlightPoint(lateralYards: startLine + curve, heightYards: arc, distanceYards: distance)
        }
    }
}
