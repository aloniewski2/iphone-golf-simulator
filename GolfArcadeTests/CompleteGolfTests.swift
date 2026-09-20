import XCTest
import SceneKit
import simd
@testable import GolfArcade

final class CompleteGolfTests: XCTestCase {
    func testNineHoleCourseKeepsLegacyIdentityAndSharedBunkerFloor() {
        XCTAssertEqual(Course.sunwardResort.holes.count,9)
        XCTAssertEqual(Course.sunwardResort.par,36)
        XCTAssertEqual(Course.sunward.holes.count,3)
        XCTAssertNotEqual(Course.sunward.id,Course.sunwardResort.id)
        for hole in Course.sunwardResort.holes {
            XCTAssertEqual(hole.lie(at:hole.tee),.tee)
            XCTAssertEqual(hole.lie(at:hole.pin),.green)
            for hazard in hole.hazards where hazard.kind == .bunker {
                let point=CoursePoint(x:hazard.x,d:hazard.distance)
                XCTAssertEqual(hole.surface(at:point).lie,.bunker)
                XCTAssertEqual(hole.surface(at:point).heightYards,hole.terrain.elevation(at:point)-0.8,accuracy:0.0001)
            }
        }
    }

    func testSharedSurfaceShotIsDeterministicAndPlanningUsesCompleteConditions() {
        let hole=Course.sunwardResort.holes[0]
        let request=ShotRequest(club:.driver,targetHeading:hole.tee.heading(to:hole.recommendedRoute(from:hole.tee)[1]),
            type:.full,execution:SwingImpact(power:0.8),wind:hole.wind,simulationVersion:3)
        let first=RangeShot(id:1,request:request,origin:hole.tee,hole:hole)
        XCTAssertEqual(first,RangeShot(id:1,request:request,origin:hole.tee,hole:hole))
        XCTAssertTrue(first.duration.isFinite && first.duration > 0)
        let conditions=ShotPlanningConditions(request:request,origin:hole.tee,target:hole.pin,lie:.tee,hole:hole)
        var other=request; other.trajectory = .high
        XCTAssertNotEqual(conditions,ShotPlanningConditions(request:other,origin:hole.tee,target:hole.pin,lie:.tee,hole:hole))
    }

    func testShortShotLandingWinsOverHeroCameraAndReducedMotionIsStable() {
        let shot=RangeShot(id:1,club:.wedge,power:0.1,aim:0)
        var input=ShotCameraDirector.Inputs(ball:.zero,heading:0,aim:0,distanceToPin:10,onGreen:false,
            handedness:.right,shot:shot,elapsed:0.15,reaction:.pure,landingTime:0.1)
        XCTAssertEqual(ShotCameraDirector.stage(input),.landing)
        input.reduceMotion=true
        let start=ShotCameraDirector.shot(input)
        input.elapsed=shot.duration
        XCTAssertEqual(ShotCameraDirector.shot(input).position,start.position)
        XCTAssertEqual(ShotCameraDirector.shot(input).lookAt,start.lookAt)
    }

    func testSevenClubsKeepLegacyIdentifiersAndCalibratedCarry() {
        XCTAssertEqual(GolfClub.allCases.count, 7)
        XCTAssertEqual(GolfClub(rawValue: "iron"), .iron)
        XCTAssertEqual(GolfClub(rawValue: "wedge"), .wedge)
        for club in GolfClub.allCases {
            let shot = BallFlight.simulate(club.launch(power: 1, aimDegrees: 0, curveDegrees: 0))
            XCTAssertEqual(club == .putter ? shot.total : shot.carry, club.referenceDistanceYards, accuracy: 0.15)
        }
    }

    func testWindIsDeterministicAndCourseRelative() {
        let launch = GolfClub.driver.launch(power: 0.8, aimDegrees: 0, curveDegrees: 0)
        let left = BallFlight.simulate(launch, windX: -4), right = BallFlight.simulate(launch, windX: 4)
        XCTAssertLessThan(left.landing.lateralYards, 0)
        XCTAssertGreaterThan(right.landing.lateralYards, 0)
        XCTAssertEqual(right, BallFlight.simulate(launch, windX: 4))
        XCTAssertEqual(CourseWind(x: 4,d: 0).local(to: 90).z, 4, accuracy: 0.0001)
        XCTAssertLessThan(BallFlight.simulate(launch,windZ: -4).carry, BallFlight.simulate(launch,windZ: 4).carry)
    }

    func testChosenShapeAndTrajectoryUseActualSolver() {
        let base = ShotRequest(club: .iron, targetHeading: 0, type: .full, execution: SwingImpact(power: 0.8))
        var high = base, low = base, draw = base
        high.trajectory = .high; low.trajectory = .low; draw.shape = .draw
        XCTAssertGreaterThan(RangeShot(id: 0,request: high,origin: .zero).apex, RangeShot(id: 0,request: low,origin: .zero).apex)
        XCTAssertLessThan(RangeShot(id: 0,request: draw,origin: .zero).landing.lateralYards, 0)
        draw.handedness = .left
        XCTAssertGreaterThan(RangeShot(id: 0,request: draw,origin: .zero).landing.lateralYards, 0)
        let chip = ShotRequest(club: .wedge,targetHeading: 0,type: .chip,execution: SwingImpact(power: 0.8),shape: .draw)
        XCTAssertEqual(RangeShot(id: 0,request: chip,origin: .zero).curve, 0)
    }

    @MainActor func testPracticeNeverScoresOrMovesBall() {
        let round = CourseRound()
        let ball = round.ball
        round.practiceMode = true
        round.charge(0.75)
        XCTAssertFalse(round.release(execution: SwingImpact(power: 0.75,source: .camera)))
        XCTAssertEqual(round.practiceImpact?.power, 0.75)
        XCTAssertEqual(round.phase, .ready); XCTAssertNil(round.activeShot)
        round.advance(at: .distantFuture)
        XCTAssertEqual(round.strokes, 0); XCTAssertEqual(round.ball, ball)
        round.practiceMode = false
        round.charge(0.75)
        XCTAssertTrue(round.release())
    }

    @MainActor func testPracticeImpactValidationWithoutCharging() {
        let round = CourseRound()
        XCTAssertFalse(round.recordPracticeImpact(SwingImpact(power: 0.5)))
        round.practiceMode = true
        for power in [Double.nan, .infinity, -.infinity, -0.1, 0] {
            XCTAssertFalse(round.recordPracticeImpact(SwingImpact(power: power)))
        }
        for confidence in [Double.nan, .infinity, -.infinity, 0.44] {
            XCTAssertFalse(round.recordPracticeImpact(SwingImpact(power: 0.5, confidence: confidence)))
        }
        XCTAssertNil(round.practiceImpact)
        round.editingShot = true
        XCTAssertFalse(round.recordPracticeImpact(SwingImpact(power: 0.5)))
        round.editingShot = false
        round.pause()
        XCTAssertFalse(round.recordPracticeImpact(SwingImpact(power: 0.5)))
        round.resume()
        let ball = round.ball, scores = round.scores
        XCTAssertTrue(round.recordPracticeImpact(SwingImpact(power: 1.2)))
        XCTAssertEqual(round.practiceImpact?.power, 1)
        XCTAssertEqual(round.phase, .ready)
        XCTAssertEqual(round.power, 0)
        XCTAssertNil(round.activeShot)
        round.advance(at: .distantFuture)
        XCTAssertEqual(round.ball, ball)
        XCTAssertEqual(round.scores, scores)
        XCTAssertEqual(round.strokes, 0)
        round.practiceMode = false
        XCTAssertNil(round.practiceImpact)
    }

    @MainActor func testAutomaticAimPreviewMatchesChosenShot() {
        let round = CourseRound()
        round.automaticAim = true; round.shotShape = .fade; round.trajectory = .high
        let power = round.recommendedPower
        round.charge(power)
        // Preview quantizes live power in 5% steps; recommendations use the same steps.
        let preview = round.trajectoryPreview
        XCTAssertTrue(round.release())
        XCTAssertEqual(preview.request, round.activeShot?.request)
        XCTAssertEqual(preview.rest, round.activeShot?.rest)
    }

    func testAnatomicalMirrorIsInvolutiveAndKeepsClubConsistent() {
        let pose = AvatarAnimations.swingArc(degrees: 95)
        let mirror = pose.anatomicallyMirrored
        XCTAssertEqual(mirror[.leftWrist].x, -pose[.rightWrist].x)
        XCTAssertEqual(mirror.clubHead.x, -pose.clubHead.x)
        XCTAssertEqual(mirror.anatomicallyMirrored.joints, pose.joints)
    }

    @MainActor func testAuthoredGolferImportsAndRendersReviewMatrix() throws {
        let authored = try XCTUnwrap(ResortGolferSkin(shirt: .systemTeal, skin: .systemBrown))
        XCTAssertEqual(authored.node.name, "authoredResortGolfer")
        let scene = SCNScene(); scene.background.contents = UIColor(white: 0.18,alpha: 1)
        let rig = AvatarRig(shirt: .systemTeal,authored: true)
        scene.rootNode.addChildNode(rig.node)
        let light = SCNNode(); light.light = SCNLight(); light.light?.type = .omni
        light.light?.intensity = 650; light.position = SCNVector3(6,9,7); scene.rootNode.addChildNode(light)
        let fill = SCNNode(); fill.light = SCNLight(); fill.light?.type = .ambient; fill.light?.intensity = 180
        scene.rootNode.addChildNode(fill)
        let camera = SCNNode(); camera.camera = SCNCamera(); camera.camera?.fieldOfView = 42
        scene.rootNode.addChildNode(camera)
        let renderer = SCNRenderer(device: nil, options: nil); renderer.scene = scene; renderer.pointOfView = camera
        for mirror in [false,true] { for angle in [0.0,95,-110] {
            rig.setMirrored(mirror); rig.apply(AvatarAnimations.swingArc(degrees: angle))
            XCTAssertGreaterThan(simd_determinant(rig.node.simdTransform), 0)
            camera.position = SCNVector3(mirror ? -11 : 11,6,7); camera.look(at: SCNVector3(0,2.7,0))
            let image = renderer.snapshot(atTime: 0,with: CGSize(width:768,height:768),antialiasingMode:.multisampling4X)
            let attachment = XCTAttachment(image:image); attachment.name = "authored-\(mirror ? "left" : "right")-\(angle)"
            attachment.lifetime = .keepAlways; add(attachment)
        } }
    }
}
