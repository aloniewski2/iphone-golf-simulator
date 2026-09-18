import Foundation
import simd

@main
struct SunwardCoreCheck {
    @MainActor static func main() {
        var checks = 0
        for course in Course.all { for hole in course.holes {
            let route = hole.recommendedRoute(from: hole.tee)
            precondition(route.last == hole.pin, "Route did not reach pin: \(course.name) \(hole.number)")
            for p in route.dropFirst() {
                precondition([CourseLie.fairway, .green].contains(hole.lie(at: p)), "Unsafe route: \(course.name)")
                checks += 1
            }
        } }
        for slope in [-0.025, 0.0, 0.025] {
            var hole = Hole(number: 1, par: 3, centerline: [.zero, CoursePoint(x: 0, d: 10)],
                fairwayWidth: 40, greenRadius: 25, hazards: [])
            hole.terrain = Terrain(tiltX: slope, tiltD: 0.01, features: [])
            let started = Date()
            let plan = PuttRecommendation.solve(from: .zero, hole: hole)
            print("slope=\(slope), line=\(plan.offsetDegrees), power=\(plan.power), miss=\(plan.missYards), ms=\(Date().timeIntervalSince(started) * 1000)")
            precondition(plan.missYards < 0.3)
            precondition(plan == PuttRecommendation.solve(from: .zero, hole: hole))
            if slope != 0 { precondition(plan.offsetDegrees * slope > 0) }
            checks += 3
        }
        let address = AvatarAnimations.address
        for angle in stride(from: -150.0, through: 150, by: 1) {
            let pose = AvatarAnimations.swingArc(degrees: angle)
            precondition(pose[.leftAnkle] == address[.leftAnkle])
            precondition(abs(simd_length(pose.clubDirection) - 1) < 0.001)
            for (a,b,l) in [(BodyJoint.leftShoulder, BodyJoint.leftElbow, AvatarSize.upperArm),
                (.rightElbow,.rightWrist,AvatarSize.forearm),(.leftHip,.leftKnee,AvatarSize.thigh)] {
                precondition(abs(simd_distance(pose[a],pose[b]) - l) < 0.01)
            }
            if angle < 150 { precondition(simd_distance(pose.handCenter, AvatarAnimations.swingArc(degrees: angle + 1).handCenter) < 0.15) }
            checks += 6
        }
        precondition(AvatarAnimations.swingArc(degrees: .nan) == address)
        for angle in [-150.0, -60, 0, 60, 150] {
            let putt = AvatarAnimations.swingArc(degrees: angle, club: .putter)
            for joint in [BodyJoint.root, .nose, .leftAnkle, .rightAnkle] { precondition(putt[joint] == address[joint]) }
            checks += 4
        }
        let round = CourseRound()
        round.automaticAim = true
        round.dropOnGreenForTesting(yards: 8)
        let preview = round.trajectoryPreview, recommended = round.recommendedPower
        let line = round.heading + round.combinedAim
        round.charge(0.12)
        precondition(round.recommendedPower == recommended)
        precondition(round.heading + round.combinedAim == line)
        round.charge(preview.power)
        precondition(round.release())
        precondition(round.activeShot?.request == preview.request)
        precondition(round.activeShot?.rest == preview.rest)
        checks += 5
        precondition(Course.sunwardResort.holes.count == 9 && Course.sunwardResort.par == 36)
        precondition(Course.sunward.id != Course.sunwardResort.id && Course.sunward.holes.count == 3)
        for club in GolfClub.allCases {
            let flight=BallFlight.simulate(club.launch(power:1,aimDegrees:0,curveDegrees:0))
            precondition(abs((club == .putter ? flight.total : flight.carry)-club.referenceDistanceYards)<0.15)
            checks += 1
        }
        for hole in Course.sunwardResort.holes {
            for hazard in hole.hazards where hazard.kind == .bunker {
                let center=CoursePoint(x:hazard.x,d:hazard.distance)
                precondition(hole.surface(at:center).heightYards < hole.terrain.elevation(at:center)-0.7)
                checks += 1
            }
        }
        let launch=GolfClub.driver.launch(power:0.8,aimDegrees:0,curveDegrees:0)
        let breeze=BallFlight.simulate(launch,windX:4)
        precondition(breeze == BallFlight.simulate(launch,windX:4))
        precondition(breeze.landing.lateralYards > 0)
        var pickedUp=0, attempts=0
        let party=CourseRound(course:.sunwardResort,playerCount:4)
        party.automaticAim=true
        while party.phase != .complete && attempts < 400 {
            if party.phase == .holed {
                if party.pickedUp { pickedUp += 1 }
                party.continueAfterHole(); continue
            }
            if party.phase == .landed { party.nextShot(); continue }
            let power: Double
            if party.club == .putter { power=party.recommendedPower }
            else {
                let conditions=ShotPlanningConditions(request:ShotRequest(club:party.club,targetHeading:party.heading+party.combinedAim,
                    type:party.shotType,execution:SwingImpact(power:1),wind:party.wind,simulationVersion:3),origin:party.ball,
                    target:party.intendedTarget,lie:party.lie,hole:party.hole)
                power=conditions.solve()
            }
            party.charge(power)
            precondition(party.release())
            party.skipFlight()
            let scored=party.strokes
            party.replay(); party.skipFlight()
            precondition(party.strokes == scored,"Replay duplicated score")
            attempts += 1; checks += 2
        }
        precondition(party.phase == .complete)
        precondition(party.scores.allSatisfy { $0.count == 9 && $0.allSatisfy { $0 != nil } })
        print("Synthetic four-player nine-hole progression: \(attempts) deliberate solver inputs, \(pickedUp) capped hole-turns, scores \(party.scores)")
        print("PASS: \(checks) route/putting/animation/round checks using production Swift sources. Host-side logic check, not phone tracking certification.")
    }
}
