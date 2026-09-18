import Foundation

/// Immutable complete planning key. A worker never reads the live round while a user swings.
struct ShotPlanningConditions: Equatable, Sendable {
    let request: ShotRequest
    let origin: CoursePoint
    let target: CoursePoint
    let lie: CourseLie
    let hole: Hole

    var initialPower: Double {
        let reference = request.club.referenceDistanceYards * max(0.1,lie.powerFactor*request.type.speedGain)
        return min(1,max(0.05,(sqrt(origin.distance(to:target)/reference)*20).rounded()/20))
    }

    func solve() -> Double {
        var bestPower=initialPower, bestError=Double.infinity
        for step in 1...20 {
            if Task.isCancelled { return bestPower }
            var request=request
            request.execution.power=Double(step)/20
            let candidate=RangeShot(id:0,request:request,origin:origin,
                lieFactor:request.club == .putter ? 1 : lie.powerFactor,hole:hole)
            let error=candidate.rest.distance(to:target)+(candidate.penaltyStrokes > 0 ? 100 : 0)
            if error < bestError { bestError=error; bestPower=Double(step)/20 }
        }
        return bestPower
    }
}
