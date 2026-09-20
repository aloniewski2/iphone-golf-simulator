import XCTest
import simd
@testable import GolfArcade

final class GolfMechanicsTests: XCTestCase {
    func testSweptTrunkCannotTunnelAndDoesNotCatchExitingOverlap() throws {
        let hit=try XCTUnwrap(GolfInteractions.sweptCircle(from:simd_double2(-5,0),to:simd_double2(5,0),center:.zero,radius:0.4))
        XCTAssertEqual(hit.point.x,-0.4,accuracy:0.00001)
        XCTAssertNil(GolfInteractions.sweptCircle(from:simd_double2(0.2,0),to:simd_double2(1,0),center:.zero,radius:0.4))
        let launch=BallFlight.Launch(ballSpeedMPH:14,launchAngleDegrees:0,spinRPM:0,directionDegrees:0,curveDegrees:0)
        let trunk=BallFlight.Trunk(id:1,center:simd_double2(0,2),base:0,height:6,radius:0.35)
        let shot=BallFlight.simulate(launch,trunks:[trunk])
        XCTAssertTrue(shot.events.contains {$0.kind == .tree})
        XCTAssertLessThan(shot.landing.distanceYards,2/0.9144)
        XCTAssertEqual(shot,BallFlight.simulate(launch,trunks:[trunk]))
        let suspended=BallFlight.Trunk(id:1,center:simd_double2(0,2),base:10,height:6,radius:0.35)
        XCTAssertTrue(BallFlight.simulate(launch,trunks:[suspended]).events.isEmpty)
    }

    func testPaceAndEdgeOffsetDetermineCupEntry() {
        let a=simd_double2(0,-0.2),b=simd_double2(0,0.2)
        XCTAssertEqual(GolfInteractions.cup(from:a,to:b,velocity:simd_double2(0,0.8),center:.zero),.captured)
        XCTAssertEqual(GolfInteractions.cup(from:a,to:b,velocity:simd_double2(0,4),center:.zero),.none)
        for sign in [-1.0,1] {
            let result=GolfInteractions.cup(from:simd_double2(0.06*sign,-0.2),to:simd_double2(0.06*sign,0.2),velocity:simd_double2(0,1),center:.zero)
            guard case .lipOut(let position,let velocity)=result else { XCTFail("Glancing rim strike must lip out"); continue }
            XCTAssertGreaterThan(position.x*sign,0)
            XCTAssertGreaterThan(velocity.x*sign,0)
            XCTAssertLessThan(simd_length(velocity),1,"Rim collision must not add energy")
        }
    }

    func testWaterStopsTrajectoryAtFirstGroundEntry() {
        let launch=BallFlight.Launch(ballSpeedMPH:14,launchAngleDegrees:0,spinRPM:0,directionDegrees:0,curveDegrees:0)
        let shot=BallFlight.simulate(launch,surface:{_,z in BallFlight.Surface(isWater:z>=1)})
        XCTAssertEqual(shot.events.last?.kind,.water)
        XCTAssertLessThan(shot.landing.distanceYards,1.2/0.9144)
    }

    func testPitchChipAndBunkerHaveDistinctLaunchProfiles() {
        func shot(_ type: ShotType) -> RangeShot {
            RangeShot(id:1,request:ShotRequest(club:.wedge,targetHeading:0,type:type,execution:SwingImpact(power:0.8),simulationVersion:4),origin:.zero)
        }
        XCTAssertGreaterThan(shot(.pitch).apex,shot(.chip).apex*2)
        XCTAssertGreaterThan(shot(.bunker).apex,shot(.pitch).apex)
        XCTAssertFalse(ShotType.bunker.supports(club:.driver,lie:.bunker))
        XCTAssertFalse(ShotType.bunker.supports(club:.wedge,lie:.fairway))
        XCTAssertTrue(ShotType.bunker.supports(club:.wedge,lie:.bunker))
    }

    func testVersionFourPenaltyRecoveryKeepsNextBallPlayable() {
        var hole=Hole(number:1,par:4,centerline:[.zero,.init(x:0,d:300)],fairwayWidth:20,greenRadius:10,
            hazards:[CourseHazard(id:1,kind:.water,x:0,distance:12,width:30,length:8)])
        hole.simulationVersion=4
        let wet=RangeShot(id:1,club:.putter,power:1,aim:0,hole:hole)
        XCTAssertEqual(wet.lie,.water)
        XCTAssertEqual(wet.penaltyStrokes,1)
        XCTAssertEqual(hole.lie(at:wet.nextPosition).penaltyStrokes,0)
        XCTAssertFalse(wet.isHoled)
        let outside=RangeShot(id:2,club:.driver,power:1,aim:90,hole:hole)
        XCTAssertEqual(outside.lie,.outOfBounds)
        XCTAssertEqual(outside.penaltyStrokes,1)
        XCTAssertEqual(outside.nextPosition,.zero)
    }

    func testBunkerLipSurfaceGradientMatchesHeight() {
        let hole=Course.sunwardResort.holes[0], bunker=Course.sunwardResort.holes[0].hazards.first {$0.kind == .bunker}!
        for fraction in [0.2,0.6,0.8,0.92,1.04,1.2] {
            let p=CoursePoint(x:bunker.x+bunker.width/2*fraction,d:bunker.distance)
            let epsilon=0.0001
            let gradient=(hole.surface(at:CoursePoint(x:p.x+epsilon,d:p.d)).heightYards-hole.surface(at:CoursePoint(x:p.x-epsilon,d:p.d)).heightYards)/(2*epsilon)
            XCTAssertEqual(hole.surface(at:p).slopeX,gradient,accuracy:0.0001)
        }
    }

    func testBunkerLipRejectsLowFlightAndLoftedEscapeClearsSand() {
        let low=BallFlight.Launch(ballSpeedMPH:30,launchAngleDegrees:4,spinRPM:0,directionDegrees:0,curveDegrees:0)
        let clipped=BallFlight.simulate(low,surface:{_,z in
            BallFlight.Surface(height:max(0,min(2,(z-1)*2)),slopeZ:z>1 && z<2 ? 2 : 0,
                restitution:0.08,bounceFriction:0.25,isSand:true)
        })
        XCTAssertTrue(clipped.events.contains {$0.kind == .bunkerLip})
        var hole=Hole(number:1,par:3,centerline:[.zero,CoursePoint(x:0,d:50)],fairwayWidth:50,greenRadius:10,
            hazards:[CourseHazard(id:1,kind:.bunker,x:0,distance:0,width:8,length:8)])
        hole.simulationVersion=4
        hole.fairwayBoundary=CourseRegion(points:[.init(x:-25,d:-10),.init(x:25,d:-10),.init(x:25,d:60),.init(x:-25,d:60)])
        func stroke(_ type: ShotType) -> RangeShot {
            RangeShot(id:1,request:ShotRequest(club:.wedge,targetHeading:0,type:type,execution:SwingImpact(power:1),simulationVersion:4),
                origin:.zero,lieFactor:CourseLie.bunker.powerFactor,hole:hole)
        }
        XCTAssertEqual(stroke(.chip).lie,.bunker)
        XCTAssertNotEqual(stroke(.bunker).lie,.bunker)
        XCTAssertGreaterThan(stroke(.bunker).apex,stroke(.chip).apex)
    }

    func testVersionFourPuttingRecommendationsAcrossDistanceAndSlope() {
        for distance in [1.0,6,14] { for slope in [-0.025,0,0.025] { for rise in [-0.03,0,0.03] {
            var hole=Hole(number:1,par:3,centerline:[.zero,.init(x:0,d:distance)],fairwayWidth:60,greenRadius:30,hazards:[])
            hole.simulationVersion=4
            hole.terrain=Terrain(tiltX:slope,tiltD:rise,features:[])
            let plan=PuttRecommendation.solve(from:.zero,hole:hole)
            let shot=RangeShot(id:1,club:.putter,power:plan.power,aim:plan.offsetDegrees,hole:hole)
            XCTAssertLessThan(plan.missYards,0.15,"\(distance) yards / slope \(slope)")
            XCTAssertEqual(shot,RangeShot(id:1,club:.putter,power:plan.power,aim:plan.offsetDegrees,hole:hole))
            if shot.isHoled { XCTAssertTrue(shot.flight.events.contains {$0.kind == .cup}) }
        } } }
    }

    @MainActor func testSavedPlayerAppearanceSurvivesNewFlowAndDoesNotChangeHandedness() throws {
        let name="presets.\(UUID().uuidString)"
        let defaults=try XCTUnwrap(UserDefaults(suiteName:name))
        defer { defaults.removePersistentDomain(forName:name) }
        let flow=GameFlow(defaults:defaults), id=flow.roster[0].id
        flow.setHandedness(id,.left)
        flow.setAppearance(id,.preset(.sunset))
        let restored=GameFlow(defaults:defaults)
        XCTAssertEqual(restored.roster[0].golferAppearance,.preset(.sunset))
        XCTAssertEqual(restored.roster[0].handedness,.left)
    }

    @MainActor func testResortPreviewAndReplayUseTheSameSurfaceVersion() {
        let round=CourseRound(course:.sunwardResort)
        round.automaticAim=true; round.shotShape = .fade; round.trajectory = .high
        round.charge(0.75)
        let preview=round.trajectoryPreview
        XCTAssertTrue(round.release())
        XCTAssertEqual(round.activeShot?.request.simulationVersion,Course.sunwardResort.holes[0].simulationVersion)
        XCTAssertEqual(round.activeShot?.request.simulationVersion,7)
        XCTAssertEqual(round.activeShot?.rest,preview.rest)
        XCTAssertEqual(round.activeShot?.flight.events,preview.flight.events)
        round.skipFlight(); let scored=round.strokes
        round.replay(); round.skipFlight()
        XCTAssertEqual(round.strokes,scored)
    }

    func testPresetsAndLegacyPlayersRoundTrip() throws {
        XCTAssertEqual(GolferAppearance.Preset.allCases.count,4)
        for preset in GolferAppearance.Preset.allCases {
            var look=GolferAppearance.preset(preset); look.skin = .deep; look.hair = .silver; look.outfit = .navy
            let player=Player(name:"Saved",colorIndex:0,handedness:.left,appearance:look)
            XCTAssertEqual(try JSONDecoder().decode(Player.self,from:JSONEncoder().encode(player)),player)
        }
        let json="{\"id\":\"\(UUID().uuidString)\",\"name\":\"Legacy\",\"colorIndex\":3,\"handedness\":\"right\"}"
        let old=try JSONDecoder().decode(Player.self,from:Data(json.utf8))
        XCTAssertNil(old.appearance)
        XCTAssertEqual(old.golferAppearance.preset,.sunset)
    }
}
