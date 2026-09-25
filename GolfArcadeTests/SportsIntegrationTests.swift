import XCTest
import simd
@testable import GolfArcade

final class SportsIntegrationTests:XCTestCase {
    func testEveryRhythmicSwingRearmsDuringRecoveryEvenWithoutCamera() {
        for tracked in [true, false] {
            var f = SteeringFilter(); f.tennisStroke = true; f.calibrate(position: 0, time: 0)
            var count = 0
            for sample in 1...960 {
                let t = Double(sample) / 100
                let phase = (sample - 1) % 120
                // Full stroke, a short quiet reversal during recovery, then ready motion.
                let rate = phase < 27 ? 9.0 : phase < 43 ? 0.5 : 3.0
                let force = phase < 27 ? 0.8 : phase < 43 ? 0.05 : 0.3
                if f.step(position: 0, rate: rate, time: t, valid: tracked,
                          allowSwingWhileUntracked: true, acceleration: force) != nil { count += 1 }
            }
            XCTAssertEqual(count, 8, "Every full swing counts, including with a blurred camera")
        }
    }

    /// The reported "it swings when I only meant to step" failure. A sidestep rotates the
    /// phone hard but does not sustain stroke-level force.
    func testSidestepDoesNotTriggerAStroke() {
        var f=SteeringFilter(); f.travel=0.85; f.tennisStroke=true; f.calibrate(position:0,time:0)
        var t=0.0
        for i in 0..<120 {
            t+=0.01
            // Arm swings through ~4.5 rad/s with a moderate push-off, then settles.
            let rate = i<60 ? 4.5 : 0.4
            let force = i<60 ? 0.35 : 0.05
            XCTAssertNil(f.step(position:Double(i)*0.004,rate:rate,time:t,valid:true,acceleration:force))
        }
        XCTAssertEqual(f.phase,.steering,"stepping must never enter a swing")
        XCTAssertGreaterThan(f.target,0.2,"and steering must keep tracking the step")
    }

    /// A candidate that never becomes a real stroke must hand steering straight back.
    func testFalseStrokeCandidateReleasesSteeringQuickly() {
        var f=SteeringFilter(); f.travel=0.85; f.tennisStroke=true; f.calibrate(position:0,time:0)
        var t=0.0
        for _ in 0..<12 { t+=0.01; _=f.step(position:0,rate:6,time:t,valid:true,acceleration:0.5) }
        XCTAssertEqual(f.phase,.swinging)
        for _ in 0..<30 { t+=0.01; _=f.step(position:0,rate:0.2,time:t,valid:true,acceleration:0.02) }
        XCTAssertEqual(f.phase,.steering,"aborted candidate returns control well before 0.7s")
        XCTAssertLessThan(t,0.5)
    }

    /// The game starts the stroke animation at onset, so onset must arrive well before the
    /// confirmation that used to be the first thing Unity heard about.
    func testOnsetIsReportedBeforeConfirmation() {
        var f=SteeringFilter(); f.travel=0.85; f.tennisStroke=true; f.calibrate(position:0,time:0)
        var t=0.0, onsetAt = -1.0, confirmedAt = -1.0
        for _ in 0..<60 {
            t+=0.01
            if f.step(position:0,rate:9,time:t,valid:true,acceleration:0.8) != nil && confirmedAt<0 { confirmedAt=t }
            if f.onsets==1 && onsetAt<0 { onsetAt=t }
        }
        XCTAssertGreaterThan(onsetAt,0); XCTAssertGreaterThan(confirmedAt,0)
        XCTAssertLessThanOrEqual(onsetAt,0.05,"onset within a few samples of the arm starting")
        XCTAssertGreaterThanOrEqual(confirmedAt-onsetAt,0.05,"confirmation still waits for a real stroke")
        XCTAssertEqual(f.aborts,0)
    }

    /// A written-off candidate must be reported, so the game can cancel the animation.
    func testWrittenOffCandidateCountsAsAbort() {
        var f=SteeringFilter(); f.travel=0.85; f.tennisStroke=true; f.calibrate(position:0,time:0)
        var t=0.0
        for _ in 0..<5 { t+=0.01; _=f.step(position:0,rate:6,time:t,valid:true,acceleration:0.5) }
        for _ in 0..<30 { t+=0.01; _=f.step(position:0,rate:0.2,time:t,valid:true,acceleration:0.02) }
        XCTAssertEqual(f.onsets,1); XCTAssertEqual(f.aborts,1)
    }

    // MARK: Motion-sensor racket face (works in any grip, camera or not)

    func testRacketFaceAimFollowsTheFaceNotTheCamera() {
        let tv = 0.3, deg = Double.pi / 180
        // Headings grow counter-clockwise seen from above, i.e. toward the player's LEFT.
        XCTAssertEqual(SportsMotionGeometry.faceAngle(screenHeading:tv,tvHeading:tv,facing:1),0,accuracy:1e-9)
        XCTAssertEqual(SportsMotionGeometry.faceAngle(screenHeading:tv-20*deg,tvHeading:tv,facing:1),20,accuracy:1e-6,
                       "forehand face turned to the player's right aims right")
        XCTAssertEqual(SportsMotionGeometry.faceAngle(screenHeading:tv + Double.pi - 15*deg,tvHeading:tv,facing:-1),15,accuracy:1e-6,
                       "on a backhand the back of the phone is the face")
        XCTAssertEqual(SportsMotionGeometry.faceAngle(screenHeading:tv+30*deg,tvHeading:tv,facing:1),-30,accuracy:1e-6)
        // Wrap-around: a heading just past pi is still a small turn.
        XCTAssertEqual(SportsMotionGeometry.faceAngle(screenHeading:-Double.pi + 0.05,tvHeading:Double.pi - 0.05,facing:1),-5.73,accuracy:0.01)
    }

    func testAimIsRelativeToThePlayersHabitAndSaturates() {
        XCTAssertEqual(SportsMotionGeometry.aim(faceAngle:0,neutral:0),0)
        XCTAssertEqual(SportsMotionGeometry.aim(faceAngle:SportsMotionGeometry.aimSpanDegrees,neutral:0),1)
        XCTAssertEqual(SportsMotionGeometry.aim(faceAngle:90,neutral:0),1)
        XCTAssertEqual(SportsMotionGeometry.aim(faceAngle:3,neutral:0),0,"inside the dead zone the ball goes straight")
        XCTAssertLessThan(SportsMotionGeometry.aim(faceAngle:-18,neutral:0),-0.4)
        // A player who habitually swings with the face 10° open: that is their "straight".
        XCTAssertEqual(SportsMotionGeometry.aim(faceAngle:10,neutral:10),0)
        XCTAssertEqual(SportsMotionGeometry.learnNeutral(0,faceAngle:4),0.4,accuracy:1e-9)
        XCTAssertEqual(SportsMotionGeometry.learnNeutral(0,faceAngle:10),0,"an aimed shot is not learnt as habit")
        XCTAssertEqual(SportsMotionGeometry.learnNeutral(0,faceAngle:30),0,"a deliberately aimed shot is not learnt as habit")
    }

    func testGripFromHeadingsWhicheverWayThePhoneIsHeld() {
        let tv = 1.0
        XCTAssertEqual(SportsMotionGeometry.strokeFacing(screenHeading:tv+0.2,tvHeading:tv,previous:-1),1)
        XCTAssertEqual(SportsMotionGeometry.strokeFacing(screenHeading:tv + Double.pi,tvHeading:tv,previous:1),-1)
        XCTAssertEqual(SportsMotionGeometry.strokeFacing(screenHeading:tv + Double.pi/2,tvHeading:tv,previous:-1),-1,"edge-on keeps the last grip")
        XCTAssertEqual(SportsMotionGeometry.strokeFacing(screenHeading:nil,tvHeading:tv,previous:1),1,"screen pointing at the floor keeps the last grip")
        XCTAssertNil(SportsMotionGeometry.heading(SIMD3<Double>(0,0,1)),"straight up has no heading")
        XCTAssertEqual(SportsMotionGeometry.heading(SIMD3<Double>(0,1,0))!,Double.pi/2,accuracy:1e-9)
        // A phone held flat (screen up) and rolled: the device's +Z is vertical, no heading,
        // so a flat horizontal swing keeps the grip it started with rather than flipping.
        let flat=simd_quatd(angle:0,axis:SIMD3<Double>(0,0,1))
        XCTAssertNil(SportsMotionGeometry.heading(SportsMotionGeometry.rotate(SIMD3<Double>(0,0,1),by:flat)))
    }

    /// Forehand is screen-to-TV, backhand is lens-to-TV, and tilting the phone must not
    /// change the answer.
    func testStrokeFacingFollowsWhichFaceIsTowardTheTV() {
        let forward=SIMD3<Float>(0,0,-1) // TV lies along -Z
        // Lens at the TV means the screen normal points away from it: backhand.
        XCTAssertEqual(SportsMotionGeometry.strokeFacing(screenNormal:SIMD3<Float>(0,0,1),courtForward:forward,previous:0),-1)
        // Screen at the TV: forehand.
        XCTAssertEqual(SportsMotionGeometry.strokeFacing(screenNormal:SIMD3<Float>(0,0,-1),courtForward:forward,previous:0),1)
        // Steeply pitched but still screen-forward: still a forehand.
        let pitched=simd_normalize(SIMD3<Float>(0,0.9,-0.6))
        XCTAssertEqual(SportsMotionGeometry.strokeFacing(screenNormal:pitched,courtForward:forward,previous:0),1)
        // Edge-on keeps the last unambiguous grip instead of flapping.
        XCTAssertEqual(SportsMotionGeometry.strokeFacing(screenNormal:SIMD3<Float>(1,0,0),courtForward:forward,previous:1),1)
        XCTAssertEqual(SportsMotionGeometry.strokeFacing(screenNormal:SIMD3<Float>(1,0,0),courtForward:forward,previous:-1),-1)
        // Straight up or down has no horizontal meaning at all.
        XCTAssertEqual(SportsMotionGeometry.strokeFacing(screenNormal:SIMD3<Float>(0,1,0),courtForward:forward,previous:-1),-1)
    }

    func testTennisTranslationAndGripRotationDoNotSwing() {
        var f=SteeringFilter(); f.travel=0.35; f.tennisStroke=true; f.calibrate(position:0,time:0)
        for i in 1...100 { XCTAssertNil(f.step(position:0.2,rate:2.8,time:Double(i)/100,valid:true,acceleration:0.5)) }
        XCTAssertGreaterThan(f.target,0.4)
        for i in 101...200 { XCTAssertNil(f.step(position:-0.2,rate:6,time:Double(i)/100,valid:true,acceleration:0.05)) }
        XCTAssertLessThan(f.target,-0.4); XCTAssertEqual(f.phase,.steering)
    }
    func testTennisStrokePowerAndBoundedRecovery() {
        func stroke(_ rate:Double) -> Double {
            var f=SteeringFilter(); f.travel=0.35; f.tennisStroke=true; f.calibrate(position:0,time:0)
            var powers:[Double]=[]
            for i in 1...100 {
                if let p=f.step(position:0,rate:rate,time:Double(i)/100,valid:true,acceleration:i<40 ? 0.6 : 0) { powers.append(p) }
            }
            XCTAssertEqual(powers.count,1)
            for i in 101...140 { _=f.step(position:0.2,rate:2,time:Double(i)/100,valid:true,acceleration:0) }
            XCTAssertEqual(f.phase,.steering); XCTAssertGreaterThan(f.target,0.3)
            return powers.first ?? 0
        }
        XCTAssertGreaterThan(stroke(13),stroke(6))
    }
    func testMovementOnlyTennisNeverSwingsOrFreezesOnRotation() {
        var f=SteeringFilter(); f.travel=0.35; f.detectSwings=false; f.calibrate(position:0,time:0)
        for i in 1...100 { XCTAssertNil(f.step(position:0.2,rate:12,time:Double(i)/100,valid:true)) }
        XCTAssertGreaterThan(f.target,0.4); XCTAssertEqual(f.phase,.steering)
        for i in 101...200 { XCTAssertNil(f.step(position:-0.2,rate:12,time:Double(i)/100,valid:true)) }
        XCTAssertLessThan(f.target,-0.4); XCTAssertEqual(f.phase,.steering)
    }
    func testHorizontalGripDoesNotRotateSteeringAxis() throws {
        for roll:Float in [0,.pi/2,-.pi/2,.pi] {
            let orientation=simd_quatf(angle:.pi/4,axis:SIMD3<Float>(0,1,0))*simd_quatf(angle:roll,axis:SIMD3<Float>(0,0,1))
            let right=try XCTUnwrap(SportsMotionGeometry.horizontalRight(cameraBack:orientation.act(SIMD3<Float>(0,0,1))))
            XCTAssertEqual(right.x,sqrt(0.5),accuracy:0.001)
            XCTAssertEqual(right.z,-sqrt(0.5),accuracy:0.001)
        }
        XCTAssertNil(SportsMotionGeometry.horizontalRight(cameraBack:SIMD3<Float>(0,1,0)))
    }
    func testModerateTennisStrokeEmitsOnceDuringBriefTrackingBlur() {
        var f=SteeringFilter(); f.calibrate(position:0,time:0)
        var hits=0
        for i in 1...35 {
            if f.step(position:2,rate:3,time:Double(i)/100,valid:i<4,allowSwingWhileUntracked:true) != nil { hits+=1 }
        }
        XCTAssertEqual(hits,1); XCTAssertEqual(f.target,0)
        _=f.step(position:2,rate:3,time:0.7,valid:false)
        XCTAssertEqual(f.phase,.trackingLost)
        _=f.step(position:2,rate:0,time:0.75,valid:true)
        XCTAssertEqual(f.phase,.steering,"tracking loss is transient, never terminal")
    }
    func testShortGyroSpikeDoesNotSwing() {
        var f=SteeringFilter(); f.calibrate(position:0,time:0)
        XCTAssertNil(f.step(position:0,rate:8,time:0.01,valid:true))
        for i in 2...80 { XCTAssertNil(f.step(position:0,rate:0.1,time:Double(i)/100,valid:true)) }
    }
    func testExternalDisplaySceneIsDeclaredInBuiltApp() throws {
        let manifest=try XCTUnwrap(Bundle.main.object(forInfoDictionaryKey:"UIApplicationSceneManifest") as? [String:Any])
        let configurations=try XCTUnwrap(manifest["UISceneConfigurations"] as? [String:Any])
        let external=try XCTUnwrap(configurations["UIWindowSceneSessionRoleExternalDisplayNonInteractive"] as? [[String:Any]])
        XCTAssertEqual(external.first?["UISceneConfigurationName"] as? String,"Sports External")
        XCTAssertEqual(external.first?["UISceneDelegateClassName"] as? String,"GolfArcade.SportsExternalScene")
    }
    func testPhysicalShiftMovesRightWithoutSwing() {
        var f=SteeringFilter(); f.travel=0.35; f.calibrate(position:0,time:0)
        for i in 1...100 { XCTAssertNil(f.step(position:0.2,rate:0.2,time:Double(i)/100,valid:true)) }
        XCTAssertGreaterThan(f.target,0.4); XCTAssertEqual(f.phase,.steering)
    }
    func testSwingFreezesSteeringAndEmitsOnce() {
        var f=SteeringFilter(); f.calibrate(position:0,time:0)
        var hits=0
        for i in 1...50 { if f.step(position:Double(i)/100,rate:10,time:Double(i)/100,valid:true) != nil { hits+=1 } }
        XCTAssertEqual(hits,1); XCTAssertEqual(f.target,0,accuracy:0.001)
    }
    /// After a stroke the mapping set at Ready still holds: standing 15cm right of centre
    /// means 15cm right of centre, not "wherever you happen to be is the new centre".
    func testRecoveryKeepsFixedMappingFromReady() {
        var f=SteeringFilter(); f.travel=0.65; f.adaptiveReach=false; f.calibrate(position:0,time:0)
        for i in 1...50 { _=f.step(position:0.4,rate:10,time:Double(i)/100,valid:true) }
        for i in 51...150 { _=f.step(position:0.15,rate:0,time:Double(i)/100,valid:true) }
        XCTAssertEqual(f.phase,.steering)
        XCTAssertEqual(f.target,SteeringFilter.courtPosition(delta:0.15,span:0.65),accuracy:0.01)
    }

    /// The reported "pins at the right edge" failure: repeated strokes at one physical spot
    /// used to rebase the origin every time until the target saturated.
    func testRepeatedStrokesDoNotDriftTheCourtPosition() {
        var f=SteeringFilter(); f.travel=0.65; f.adaptiveReach=false; f.tennisStroke=true; f.calibrate(position:0,time:0)
        var t=0.0
        for _ in 0..<12 {
            for _ in 0..<60 { t+=0.01; _=f.step(position:0.2,rate:9,time:t,valid:true,acceleration:0.6) }
            for _ in 0..<80 { t+=0.01; _=f.step(position:0.2,rate:0.2,time:t,valid:true,acceleration:0.02) }
        }
        XCTAssertEqual(f.phase,.steering)
        XCTAssertEqual(f.target,SteeringFilter.courtPosition(delta:0.2,span:0.65),accuracy:0.02)
        XCTAssertLessThan(f.target,0.9,"court position must not creep toward the edge")
    }

    /// Ready always means "I am standing at the centre of the court".
    func testReadyRecentersFromAPinnedTarget() {
        var f=SteeringFilter(); f.travel=0.65; f.adaptiveReach=false; f.calibrate(position:0,time:0)
        for i in 1...200 { _=f.step(position:3,rate:0.1,time:Double(i)/100,valid:true) }
        XCTAssertEqual(f.target,1,accuracy:0.001)
        f.calibrate(position:3,time:3)
        XCTAssertEqual(f.target,0); XCTAssertEqual(f.phase,.steering)
        for i in 301...400 { _=f.step(position:3,rate:0.1,time:Double(i)/100,valid:true) }
        XCTAssertEqual(f.target,0,accuracy:0.001)
    }

    /// A camera blur mid-rally must not end the rally. Steering resumes by itself.
    func testBriefTrackingBlurRecoversWithoutReady() {
        var f=SteeringFilter(); f.travel=0.65; f.adaptiveReach=false; f.calibrate(position:0,time:0)
        for i in 1...60 { _=f.step(position:0.3,rate:0.1,time:Double(i)/100,valid:true) }
        let held=f.target
        XCTAssertGreaterThan(held,0.3)
        for i in 61...90 { _=f.step(position:0.3,rate:0.1,time:Double(i)/100,valid:false) }
        XCTAssertEqual(f.phase,.trackingLost)
        XCTAssertEqual(f.target,held,accuracy:0.001,"position is held, not reset, while blurred")
        for i in 91...200 { _=f.step(position:0.3,rate:0.1,time:Double(i)/100,valid:true) }
        XCTAssertEqual(f.phase,.steering)
        XCTAssertEqual(f.target,SteeringFilter.courtPosition(delta:0.3,span:0.65),accuracy:0.01)
    }

    /// A long outage re-anchors instead of teleporting the player across the court.
    func testLongOutageReanchorsWithoutJump() {
        var f=SteeringFilter(); f.travel=0.65; f.calibrate(position:0,time:0)
        for i in 1...60 { _=f.step(position:0.3,rate:0.1,time:Double(i)/100,valid:true) }
        let held=f.target
        for i in 61...260 { _=f.step(position:0.3,rate:0.1,time:Double(i)/100,valid:false) }
        _=f.step(position:9,rate:0.1,time:2.61,valid:true)
        XCTAssertEqual(f.phase,.steering)
        XCTAssertEqual(f.target,held,accuracy:0.01,"re-anchoring keeps the court position continuous")
    }
    /// Losing tracking must not require a Ready tap to get moving again — that is what made
    /// a rally last only a few seconds.
    func testTrackingLossRecoversOnItsOwn() {
        var f=SteeringFilter(); f.calibrate(position:0,time:0)
        _=f.step(position:2,rate:0,time:1,valid:false)
        XCTAssertEqual(f.phase,.trackingLost)
        _=f.step(position:2,rate:0,time:2,valid:true)
        XCTAssertEqual(f.phase,.steering)
    }

    /// The landscape-grip failure: a lens aimed at a side wall used to be accepted and gave a
    /// court axis at right angles to the player, so stepping sideways did nothing.
    func testAxisGateRejectsALensThatIsNotLevel() {
        XCTAssertEqual(SportsMotionGeometry.lensPitch(cameraBack:SIMD3<Float>(0,0,1)),0,accuracy:0.001)
        XCTAssertEqual(SportsMotionGeometry.lensPitch(cameraBack:SIMD3<Float>(0,1,0)),.pi/2,accuracy:0.001)
        XCTAssertEqual(SportsMotionGeometry.lensPitch(cameraBack:SIMD3<Float>(0,-1,0)),.pi/2,accuracy:0.001)
        let tilted=SIMD3<Float>(0,sqrt(0.5),sqrt(0.5))
        XCTAssertGreaterThan(SportsMotionGeometry.lensPitch(cameraBack:tilted),25.0*Double.pi/180)
    }

    /// Once the axis is locked, the phone's own roll is irrelevant: the same sideways step
    /// produces the same court position in portrait and in either landscape grip.
    func testLockedAxisGivesTheSameSteeringForEveryGrip() throws {
        let heading=simd_quatf(angle:.pi/3,axis:SIMD3<Float>(0,1,0))
        var targets:[Double]=[]
        for roll:Float in [0,.pi/2,-.pi/2,.pi] {
            let orientation=heading*simd_quatf(angle:roll,axis:SIMD3<Float>(0,0,1))
            let axis=try XCTUnwrap(SportsMotionGeometry.horizontalRight(cameraBack:orientation.act(SIMD3<Float>(0,0,1))))
            let stepRight=axis*0.3
            var f=SteeringFilter(); f.travel=0.65
            f.calibrate(position:0,time:0)
            for i in 1...150 { _=f.step(position:Double(simd_dot(stepRight,axis)),rate:0.1,time:Double(i)/100,valid:true) }
            targets.append(f.target)
        }
        for value in targets { XCTAssertEqual(value,targets[0],accuracy:0.0001) }
        XCTAssertGreaterThan(targets[0],0.3)
    }
    /// Full court must stay reachable at the end of the player's own range, whatever that
    /// range turns out to be — otherwise adapting the reach would cap them short of the line.
    func testFullCourtIsReachableAtTheEdgeOfReach() {
        for span in [0.3,0.65,0.85,1.4] {
            XCTAssertEqual(SteeringFilter.courtPosition(delta:span,span:span),1,accuracy:1e-9)
            XCTAssertEqual(SteeringFilter.courtPosition(delta:-span,span:span),-1,accuracy:1e-9)
            XCTAssertEqual(SteeringFilter.courtPosition(delta:0,span:span),0)
            // Round-trips through the inverse used to re-anchor after a tracking outage.
            for target in [-1.0,-0.4,0,0.4,1.0] {
                let back=SteeringFilter.courtPosition(delta:SteeringFilter.displacement(target:target,span:span),span:span)
                XCTAssertEqual(back,target,accuracy:1e-9)
            }
        }
    }

    /// The reported "much harder to move right than left". The phone is held in one hand, so
    /// the grip itself carries it left for a right-hander; a single shared reach then makes
    /// the far side unreachable. Learning each side separately must restore both directions.
    func testUnevenPhysicalReachStillCoversBothSidesOfTheCourt() {
        var f=SteeringFilter(); f.travel=0.85; f.calibrate(position:0,time:0)
        var t=0.0
        // Ten slow sweeps: easily 0.8m to the left, but only 0.3m to the right.
        for _ in 0..<10 {
            for _ in 0..<60 { t+=0.01; _=f.step(position:-0.8,rate:0.1,time:t,valid:true) }
            for _ in 0..<60 { t+=0.01; _=f.step(position:0.3,rate:0.1,time:t,valid:true) }
        }
        for _ in 0..<80 { t+=0.01; _=f.step(position:-0.8,rate:0.1,time:t,valid:true) }
        let left=f.target
        for _ in 0..<80 { t+=0.01; _=f.step(position:0.3,rate:0.1,time:t,valid:true) }
        let right=f.target
        XCTAssertLessThan(left,-0.9,"the easy side still reaches the line")
        XCTAssertGreaterThan(right,0.9,"and so does the side the player can barely reach")
    }

    func testOutOfOrderSamplesDoNotMove() {
        var f=SteeringFilter(); f.calibrate(position:0,time:10)
        XCTAssertNil(f.step(position:4,rate:12,time:9,valid:true)); XCTAssertEqual(f.target,0)
    }
    func testOldPlayerJSONKeepsIdentityAndDefaults() throws {
        let id=UUID()
        let json="{\"id\":\"\(id)\",\"name\":\"Saved player\",\"colorIndex\":1,\"handedness\":\"left\"}"
        let p=try JSONDecoder().decode(Player.self,from:Data(json.utf8))
        XCTAssertEqual(p.id,id); XCTAssertEqual(p.handedness,.left)
        XCTAssertFalse(p.standardFemale); XCTAssertEqual(p.standardSkin,2)
    }
    func testNewCharacterFieldsRoundTrip() throws {
        var p=Player(name:"Test",colorIndex:0); p.standardFemale=true; p.standardSkin=5
        XCTAssertEqual(try JSONDecoder().decode(Player.self,from:JSONEncoder().encode(p)),p)
    }
}
