import Foundation

/// Exact offline results of BallFlight.simulate(club.launch(power: lie.powerFactor,
/// aimDegrees: 0, curveDegrees: 0)).total. This is a tiny reference bag, not a shot
/// approximation: terrain/wind/aim/actual trajectories still use the authoritative solver.
/// Regenerate and verify with CourseTests.testClubSelectionReferenceMatchesSolver whenever
/// launch calibration, lie factors or physics change; it prints replacement values on failure.
enum ClubSelectionReference {
    static let candidates: [GolfClub] = [.wedge, .iron9, .iron, .iron5, .wood3]
    private static let full = [100.13764565541227, 148.33119573113237, 175.83630013581995, 198.10346058544235, 230.69376454873864]
    private static let fringe = [97.9093562631485, 145.85709500460973, 172.20445633674228, 193.7413183383514, 225.45426727939645]
    private static let rough = [87.60908070389404, 133.09152678197742, 154.19428005552905, 172.05860626209918, 198.91413900958435]
    private static let deepRough = [68.31301289637958, 106.54773257663165, 117.99959718780775, 128.9558541278214, 145.4707721616511]

    static func reaches(for lie: CourseLie) -> [Double] {
        switch lie {
        case .fringe: fringe
        case .rough: rough
        case .deepRough: deepRough
        case .tee, .fairway, .water, .outOfBounds: full
        case .green, .bunker: [] // These have fixed club choices, independent of reach.
        }
    }

    static func club(distance: Double, lie: CourseLie) -> GolfClub {
        if lie == .green { return .putter }
        if lie == .bunker { return .wedge }
        for (index, reach) in reaches(for: lie).enumerated() {
            if distance <= reach * 0.98 { return candidates[index] }
        }
        return .driver
    }
}

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
            let obstructionCost=candidate.flight.events.contains(where:{$0.kind == .tree || $0.kind == .bunkerLip}) ? 12.0 : 0
            let error=candidate.rest.distance(to:target)+(candidate.penaltyStrokes > 0 ? 100 : 0)+obstructionCost
            if error < bestError { bestError=error; bestPower=Double(step)/20 }
        }
        return bestPower
    }
}
