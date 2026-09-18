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
                    type:party.shotType,execution:SwingImpact(power:1),wind:party.wind,simulationVersion:party.hole.simulationVersion),origin:party.ball,
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
        precondition(pickedUp == 0,"The recommended round must not depend on stroke caps")
        for preset in GolferAppearance.Preset.allCases {
            let appearance=GolferAppearance.preset(preset)
            let player=Player(name:"Fixture",colorIndex:0,appearance:appearance)
            let restored=try! JSONDecoder().decode(Player.self,from:JSONEncoder().encode(player))
            precondition(restored == player); checks += 1
        }
        let id=UUID()
        let old="{\"id\":\"\(id.uuidString)\",\"name\":\"Legacy\",\"colorIndex\":2,\"handedness\":\"right\"}"
        let legacy=try! JSONDecoder().decode(Player.self,from:Data(old.utf8))
        precondition(legacy.golferAppearance.preset == .orchard && legacy.appearance == nil)
        let cup=simd_double2(0,0)
        precondition(GolfInteractions.cup(from:simd_double2(0,-0.2),to:simd_double2(0,0.2),velocity:simd_double2(0,0.8),center:cup) == .captured)
        precondition(GolfInteractions.cup(from:simd_double2(0,-0.2),to:simd_double2(0,0.2),velocity:simd_double2(0,4),center:cup) == .none)
        let rim=GolfInteractions.cup(from:simd_double2(0.06,-0.2),to:simd_double2(0.06,0.2),velocity:simd_double2(0,1.0),center:cup)
        if case .lipOut(_,let outgoing)=rim { precondition(simd_length(outgoing)<1.0) } else { preconditionFailure("Expected rim deflection") }
        let trunk=BallFlight.Trunk(id:1,center:simd_double2(0,2),base:0,height:6,radius:0.35)
        let rolling=BallFlight.Launch(ballSpeedMPH:14,launchAngleDegrees:0,spinRPM:0,directionDegrees:0,curveDegrees:0)
        let blocked=BallFlight.simulate(rolling,trunks:[trunk])
        precondition(blocked.events.contains { $0.kind == .tree })
        precondition(blocked.landing.distanceYards < 2/0.9144)
        precondition(blocked == BallFlight.simulate(rolling,trunks:[trunk]))
        let wet=BallFlight.simulate(rolling,surface:{_,z in BallFlight.Surface(isWater:z>=1)})
        precondition(wet.events.last?.kind == .water && wet.landing.distanceYards<1.2/0.9144)
        checks += 9
        let low=BallFlight.Launch(ballSpeedMPH:30,launchAngleDegrees:4,spinRPM:0,directionDegrees:0,curveDegrees:0)
        let clipped=BallFlight.simulate(low,surface:{_,z in
            BallFlight.Surface(height:max(0,min(2,(z-1)*2)),slopeZ:z>1 && z<2 ? 2 : 0,restitution:0.08,bounceFriction:0.25,isSand:true)
        })
        precondition(clipped.events.contains {$0.kind == .bunkerLip})
        for distance in [1.0,6,14] { for slope in [-0.025,0,0.025] { for rise in [-0.03,0,0.03] {
            var green=Hole(number:1,par:3,centerline:[.zero,.init(x:0,d:distance)],fairwayWidth:60,greenRadius:30,hazards:[])
            green.simulationVersion=4; green.terrain=Terrain(tiltX:slope,tiltD:rise,features:[])
            let plan=PuttRecommendation.solve(from:.zero,hole:green)
            precondition(plan.missYards<0.15,"New green read missed: \(distance) / \(slope) / \(plan.missYards)")
            checks += 1
        } } }
        var sand=Hole(number:1,par:3,centerline:[.zero,.init(x:0,d:50)],fairwayWidth:50,greenRadius:10,
            hazards:[CourseHazard(id:1,kind:.bunker,x:0,distance:0,width:8,length:8)])
        sand.simulationVersion=4
        sand.fairwayBoundary=CourseRegion(points:[.init(x:-25,d:-10),.init(x:25,d:-10),.init(x:25,d:60),.init(x:-25,d:60)])
        func sandShot(_ type: ShotType) -> RangeShot {
            RangeShot(id:1,request:ShotRequest(club:.wedge,targetHeading:0,type:type,execution:SwingImpact(power:1),simulationVersion:4),
                origin:.zero,lieFactor:CourseLie.bunker.powerFactor,hole:sand)
        }
        precondition(sandShot(.chip).lie == .bunker,"Low chip must not escape the raised lip")
        precondition(sandShot(.bunker).lie != .bunker,"Lofted bunker shot must escape this shallow fixture")
        precondition(Course.sunwardResort.bestScoreKey.contains("physics4"))
        checks += 3
        var recovery=Hole(number:1,par:4,centerline:[.zero,.init(x:0,d:300)],fairwayWidth:20,greenRadius:10,
            hazards:[CourseHazard(id:1,kind:.water,x:0,distance:12,width:30,length:8)])
        recovery.simulationVersion=4
        let waterShot=RangeShot(id:1,club:.putter,power:1,aim:0,hole:recovery)
        precondition(waterShot.lie == .water && waterShot.penaltyStrokes == 1)
        precondition(recovery.lie(at:waterShot.nextPosition).penaltyStrokes == 0 && !waterShot.isHoled)
        let obShot=RangeShot(id:2,club:.driver,power:1,aim:90,hole:recovery)
        precondition(obShot.lie == .outOfBounds && obShot.penaltyStrokes == 1 && obShot.nextPosition == .zero)
        checks += 3
        let current=CourseRound(course:.sunwardResort)
        current.automaticAim=true; current.shotShape = .fade; current.trajectory = .high; current.charge(0.75)
        let guide=current.trajectoryPreview
        precondition(current.release())
        precondition(current.activeShot?.request == guide.request && current.activeShot?.rest == guide.rest)
        checks += 3
        print("Synthetic four-player nine-hole progression: \(attempts) deliberate solver inputs, \(pickedUp) capped hole-turns, scores \(party.scores)")
        print("PASS: \(checks) route/putting/animation/round checks using production Swift sources. Host-side logic check, not phone tracking certification.")
    }
}
