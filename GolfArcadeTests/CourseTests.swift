import XCTest
import SceneKit
@testable import GolfArcade

final class CourseTests: XCTestCase {
    func testClubCalibrationMatchesSolver() {
        let started = ContinuousClock.now
        for club in GolfClub.allCases {
            var low = 1.0, high = 145.0
            for _ in 0..<24 {
                let speed = (low + high) / 2
                let flight = BallFlight.simulate(.init(ballSpeedMPH: speed * club.smashFactor,
                    launchAngleDegrees: club.launchAngleDegrees, spinRPM: club.spinRPM,
                    directionDegrees: 0, curveDegrees: 0))
                let distance = club == .putter ? flight.total : flight.carry
                if distance < club.referenceDistanceYards { low = speed } else { high = speed }
            }
            let speed = (low + high) / 2
            print("CLUB_SPEED \(club.rawValue) \(speed)")
            XCTAssertEqual(club.maxClubSpeedMPH, speed, "Regenerate the offline calibration after physics changes")
        }
        print("CLUB_CALIBRATION_RECOMPUTE \(started.duration(to: .now)) (168 integrations removed from first use)")
    }

    func testClubSelectionReferenceMatchesSolver() {
        for lie in [CourseLie.tee, .fairway, .fringe, .rough, .deepRough, .water, .outOfBounds] {
            let reaches = [GolfClub.wedge, .iron9, .iron, .iron5, .wood3].map {
                BallFlight.simulate($0.launch(power: lie.powerFactor, aimDegrees: 0, curveDegrees: 0)).total
            }
            print("CLUB_REFERENCE \(lie.rawValue) \(reaches)")
            XCTAssertEqual(ClubSelectionReference.reaches(for: lie), reaches,
                           "Regenerate reference reaches after physics/lie changes")
            for (index, reach) in reaches.enumerated() {
                let threshold = reach * 0.98
                let candidates = [GolfClub.wedge, .iron9, .iron, .iron5, .wood3, .driver]
                XCTAssertEqual(ClubSelectionReference.club(distance: threshold.nextDown, lie: lie), candidates[index])
                XCTAssertEqual(ClubSelectionReference.club(distance: threshold, lie: lie), candidates[index])
                XCTAssertEqual(ClubSelectionReference.club(distance: threshold.nextUp, lie: lie), candidates[index + 1])
            }
        }
        for distance in [0.0, 10, 100, 500] {
            XCTAssertEqual(ClubSelectionReference.club(distance: distance, lie: .green), .putter)
            XCTAssertEqual(ClubSelectionReference.club(distance: distance, lie: .bunker), .wedge)
        }
    }

    func testClubSelectionLookupMatchesDirectSimulationAndReportsCost() {
        let queries = [CourseLie.tee, .fringe, .rough, .deepRough].flatMap { lie in
            stride(from: 0.0, through: 400.0, by: 8.0).map { (lie, $0) }
        }
        let directStart = ContinuousClock.now
        let expected = queries.map { lie, distance -> GolfClub in
            for candidate in [GolfClub.wedge, .iron9, .iron, .iron5, .wood3] {
                let reach = BallFlight.simulate(candidate.launch(power: lie.powerFactor, aimDegrees: 0, curveDegrees: 0)).total
                if distance <= reach * 0.98 { return candidate }
            }
            return .driver
        }
        let directTime = directStart.duration(to: .now)
        let lookupStart = ContinuousClock.now
        let actual = queries.map { ClubSelectionReference.club(distance: $0.1, lie: $0.0) }
        let lookupTime = lookupStart.duration(to: .now)
        XCTAssertEqual(actual, expected)
        print("CLUB_SELECTION_COST queries=\(queries.count) direct=\(directTime) lookup=\(lookupTime)")
    }

    func testPuttRecommendationUsesSlopeAndIsDeterministic() {
        for slope in [-0.025, 0.0, 0.025] {
            var hole = Hole(number: 1, par: 3, centerline: [.zero, CoursePoint(x: 0, d: 10)],
                fairwayWidth: 40, greenRadius: 25, hazards: [])
            hole.terrain = Terrain(tiltX: slope, tiltD: 0.01, features: [])
            let plan = PuttRecommendation.solve(from: .zero, hole: hole)
            XCTAssertEqual(plan, PuttRecommendation.solve(from: .zero, hole: hole))
            XCTAssertLessThan(plan.missYards, 0.3)
            XCTAssertTrue((0...1).contains(plan.power))
            if slope != 0 { XCTAssertGreaterThan(plan.offsetDegrees * slope, 0, "Start uphill against the break") }
            let baselinePower = RangeShot.power(toReach: 10, with: .putter) ?? 1
            let baseline = RangeShot(id: 0, club: .putter, power: baselinePower, aim: 0, hole: hole)
            XCTAssertLessThanOrEqual(plan.missYards, baseline.rest.distance(to: hole.pin) + 0.0001)
        }
    }

    @MainActor
    func testAutomaticPuttPreviewMatchesActualAndRecommendationStaysFixedWhileCharging() throws {
        let round = CourseRound()
        round.automaticAim = true
        round.dropOnGreenForTesting(yards: 8)
        let preview = round.trajectoryPreview
        let suggested = round.recommendedPower
        let heading = round.heading + round.combinedAim
        round.charge(0.12)
        XCTAssertEqual(round.recommendedPower, suggested)
        XCTAssertEqual(round.heading + round.combinedAim, heading)
        round.charge(preview.power)
        XCTAssertTrue(round.release())
        let actual = try XCTUnwrap(round.activeShot)
        XCTAssertEqual(actual.request, preview.request)
        XCTAssertEqual(actual.rest, preview.rest)
    }

    @MainActor
    func testRenderedFairwayCapsExtendToPhysicalLieBoundary() {
        let mesh = CourseArt.capsule(from: .zero, to: CoursePoint(x: 0, d: 100), width: 40, y: 0)
        XCTAssertEqual(mesh.boundingBox.min.z, -120, accuracy: 0.01)
        XCTAssertEqual(mesh.boundingBox.max.z, 20, accuracy: 0.01)
    }

    func testRecommendedRouteReachesPinAcrossAuthoredHolesWithoutHazardLandings() {
        for course in Course.all { for hole in course.holes {
            let route = hole.recommendedRoute(from: hole.tee)
            XCTAssertEqual(route.first, hole.tee)
            XCTAssertEqual(route.last, hole.pin, "\(course.name) hole \(hole.number)")
            XCTAssertEqual(Set(route).count, route.count)
            XCTAssertEqual(route, hole.recommendedRoute(from: hole.tee))
            for landing in route.dropFirst() {
                XCTAssertTrue([CourseLie.fairway, .green].contains(hole.lie(at: landing)), "Unsafe landing \(landing)")
            }
        } }
    }

    func testAutomaticLandingAvoidsBunkerOnNominalCenterline() {
        let hole = Hole(number: 1, par: 4,
            centerline: [.zero, CoursePoint(x: 0, d: 180), CoursePoint(x: 60, d: 350)],
            fairwayWidth: 44, greenRadius: 16,
            hazards: [CourseHazard(id: 1, kind: .bunker, x: 0, distance: 180, width: 14, length: 20)])
        let target = hole.recommendedTarget(from: hole.tee)
        XCTAssertNotEqual(target, hole.centerline[1])
        XCTAssertEqual(hole.lie(at: target), .fairway)
        for offset in [CoursePoint(x: 6,d: 0), CoursePoint(x: -6,d: 0), CoursePoint(x: 0,d: 6), CoursePoint(x: 0,d: -6)] {
            XCTAssertEqual(hole.lie(at: CoursePoint(x: target.x + offset.x, d: target.d + offset.d)), .fairway)
        }
        XCTAssertEqual(hole.recommendedRoute(from: target).last, hole.pin)
    }

    @MainActor
    func testAutomaticRouteCannotBeChangedByBodyTurnOrManualAim() {
        let round = CourseRound()
        round.automaticAim = true
        let target = round.intendedTarget, ball = round.ball
        round.setStanceAim(25); round.adjustAim(-45); round.aimAtPin(); round.resumeBodyAim(at: 10)
        XCTAssertEqual(round.combinedAim, 0)
        XCTAssertFalse(round.usesBodyAim)
        XCTAssertEqual(round.intendedTarget, target)
        XCTAssertEqual(round.ball, ball)
        XCTAssertNotEqual(target, round.hole.pin, "Opening drive follows the dogleg, not a shortcut to the flag")
    }

    func testHoleNavigationWrapsBearingsAndEmphasizesDistance() {
        let right = HoleNavigation(ball: .zero, pin: CoursePoint(x: 100, d: 0), aimHeading: 0)
        XCTAssertEqual(right.relativeBearing, 90)
        XCTAssertEqual(right.directionLabel, "90° RIGHT OF AIM")
        let left = HoleNavigation(ball: .zero, pin: CoursePoint(x: -100, d: 0), aimHeading: 0)
        XCTAssertEqual(left.relativeBearing, -90)
        XCTAssertEqual(left.directionLabel, "90° LEFT OF AIM")
        let wrapped = HoleNavigation(ball: .zero, pin: CoursePoint(x: 0, d: 100), aimHeading: 350)
        XCTAssertEqual(wrapped.relativeBearing, 10)
        XCTAssertEqual(HoleNavigation(ball: .zero, pin: .zero, aimHeading: 90).relativeBearing, 0)
        let near = HoleNavigation(ball: .zero, pin: CoursePoint(x: 0, d: 5), aimHeading: 0)
        let far = HoleNavigation(ball: .zero, pin: CoursePoint(x: 0, d: 350), aimHeading: 0)
        XCTAssertEqual(near.prominence, 0)
        XCTAssertEqual(far.prominence, 1)
        XCTAssertGreaterThan(HoleNavigation.beaconScale(cameraDistance: 350), HoleNavigation.beaconScale(cameraDistance: 30))
        XCTAssertLessThanOrEqual(HoleNavigation.beaconScale(cameraDistance: 10000), 28)
    }

    @MainActor
    func testResumeBodyAimRetainsTargetAndBallWithoutJumpingTheLine() {
        let round = CourseRound()
        round.aimAtPin()
        round.adjustAim(8)
        let target = round.intendedTarget, ball = round.ball, line = round.combinedAim
        XCTAssertFalse(round.usesBodyAim)
        round.resumeBodyAim(at: 12)
        XCTAssertTrue(round.usesBodyAim)
        XCTAssertEqual(round.combinedAim, line)
        round.setStanceAim(18)
        XCTAssertEqual(round.combinedAim, line + 6)
        XCTAssertEqual(round.intendedTarget, target)
        XCTAssertEqual(round.ball, ball)
        round.charge(0.5)
        let locked = round.combinedAim
        round.setStanceAim(-20)
        round.resumeBodyAim(at: -10)
        XCTAssertEqual(round.combinedAim, locked, "The line cannot move during a swing")
    }

    @MainActor
    func testManualAimStaysFixedAndPinCueDoesNotRedirectDogleg() {
        let round = CourseRound()
        let landing = round.intendedTarget
        let pin = round.hole.pin
        XCTAssertNotEqual(landing, pin)
        _ = round.holeNavigation
        XCTAssertEqual(round.intendedTarget, landing)
        round.setStanceAim(6)
        round.adjustAim(2)
        XCTAssertEqual(round.combinedAim, 8)
        round.setStanceAim(-9)
        XCTAssertEqual(round.combinedAim, 8, "body movement must not undo a manual aim choice")
        round.aimAtPin()
        round.setStanceAim(12)
        XCTAssertEqual(round.holeNavigation.relativeBearing, 0, accuracy: 0.001)
        XCTAssertEqual(round.hole.pin, pin)
        round.followFairway()
        round.setStanceAim(4)
        XCTAssertEqual(round.stanceAim, 4)
        XCTAssertEqual(round.intendedTarget, landing)
    }

    @MainActor
    func testBeaconIsVisualOnlyAndDoesNotDuplicateAcrossHoles() throws {
        let scene = CourseScene()
        for hole in Course.all.flatMap(\.holes) {
            scene.load(hole)
            scene.updatePinBeacon(pin: hole.pin, sunk: false)
            let beacon = try XCTUnwrap(scene.scene.rootNode.childNode(withName: "holeNavigationBeacon", recursively: true))
            XCTAssertEqual(beacon.position.x, Float(hole.pin.x))
            XCTAssertEqual(beacon.position.z, -Float(hole.pin.d))
            XCTAssertEqual(beacon.childNodes.count, 2)
            XCTAssertEqual(scene.hole, hole)
            scene.updatePinBeacon(pin: hole.pin, sunk: true)
            XCTAssertTrue(beacon.isHidden)
        }
        XCTAssertEqual(scene.scene.rootNode.childNodes.filter { $0.name == "holeNavigationBeacon" }.count, 1)
    }

    @MainActor
    func testCloseFlagTakesOverFromDistantNavigationBeacon() throws {
        let scene=CourseScene();scene.stop()
        let hole=Course.sunwardResort.holes[0];scene.load(hole)
        let ground=Float(hole.surface(at:hole.pin).heightYards)
        let beacon=try XCTUnwrap(scene.scene.rootNode.childNode(withName:"holeNavigationBeacon",recursively:true))
        for distance:Float in [10,44,45,60,75,90,180,400] {
            scene.camera.position=SCNVector3(Float(hole.pin.x)+distance,ground,-Float(hole.pin.d))
            scene.updatePinBeacon(pin:hole.pin,sunk:false)
            XCTAssertEqual(Double(beacon.opacity),HoleNavigation.beaconOpacity(cameraDistance:Double(distance)),accuracy:0.0001)
            XCTAssertFalse(beacon.isHidden)
            XCTAssertEqual(scene.hole,hole)
        }
        XCTAssertEqual(HoleNavigation.beaconOpacity(cameraDistance:45),0)
        XCTAssertEqual(HoleNavigation.beaconOpacity(cameraDistance:67.5),0.5)
        XCTAssertEqual(HoleNavigation.beaconOpacity(cameraDistance:90),1)
        scene.updatePinBeacon(pin:hole.pin,sunk:true)
        XCTAssertTrue(beacon.isHidden)
    }

    @MainActor
    func testAutomaticNextShotResetsPlanAndSelectsClubWithoutRescoring() throws {
        let round = CourseRound()
        round.automaticProgression = true
        round.adjustAim(8)
        round.curve = 3
        round.charge(0.65)
        let start = Date(timeIntervalSince1970: 1_000)
        XCTAssertTrue(round.release(at: start))
        let duration = try XCTUnwrap(round.activeShot).duration
        let landed = start.addingTimeInterval(duration + 0.1)
        round.advance(at: landed)
        XCTAssertEqual(round.phase, .landed)
        let score = round.strokes
        let spot = round.ball
        round.advance(at: landed.addingTimeInterval(2.9))
        XCTAssertEqual(round.phase, .landed)
        round.advance(at: landed.addingTimeInterval(3.1))
        XCTAssertEqual(round.phase, .ready)
        XCTAssertNil(round.activeShot)
        XCTAssertEqual(round.club, round.suggestedClub())
        XCTAssertEqual(round.aim, 0)
        XCTAssertEqual(round.curve, 0)
        XCTAssertEqual(round.ball, spot)
        round.advance(at: landed.addingTimeInterval(30))
        XCTAssertEqual(round.strokes, score)
    }

    @MainActor
    func testAutomaticProgressionPausesAndReplayDoesNotAdvance() throws {
        let round = CourseRound()
        round.automaticProgression = true
        let start = Date(timeIntervalSince1970: 100)
        round.charge(0.5)
        XCTAssertTrue(round.release(at: start))
        let end = start.addingTimeInterval(try XCTUnwrap(round.activeShot).duration + 0.1)
        round.advance(at: end)
        round.pause(at: end.addingTimeInterval(1))
        round.advance(at: end.addingTimeInterval(40))
        XCTAssertEqual(round.phase, .landed)
        round.resume(at: end.addingTimeInterval(40))
        round.advance(at: end.addingTimeInterval(41))
        XCTAssertEqual(round.phase, .landed)
        round.replay(at: end.addingTimeInterval(41))
        let score = round.strokes
        round.advance(at: end.addingTimeInterval(100))
        round.advance(at: end.addingTimeInterval(200))
        XCTAssertEqual(round.phase, .landed)
        XCTAssertEqual(round.strokes, score)
        XCTAssertNil(round.nextShotAt)
    }

    @MainActor
    func testTrajectoryUsesExactReleaseSolverAndAimCanPointBothWays() throws {
        for direction in [-15.0, 15] {
            let round = CourseRound()
            round.setStanceAim(direction / 3)
            round.adjustAim(direction)
            let preview = round.trajectoryPreview
            round.charge(preview.power)
            XCTAssertTrue(round.release())
            let actual = try XCTUnwrap(round.activeShot)
            XCTAssertEqual(actual.total, preview.total, accuracy: 0.00001)
            XCTAssertEqual(actual.rest.x, preview.rest.x, accuracy: 0.00001)
            XCTAssertEqual(actual.rest.d, preview.rest.d, accuracy: 0.00001)
            XCTAssertEqual(actual.heading, round.heading + direction + direction / 3, accuracy: 0.00001)
        }
    }

    func testLieAwarePowerPredictionMatchesRealReach() throws {
        for lie in [CourseLie.fairway, .rough, .bunker] {
            let power = try XCTUnwrap(RangeShot.power(toReach: 20, with: .wedge, lieFactor: lie.powerFactor))
            let shot = RangeShot(id: 1, club: .wedge, power: power, aim: 0, lieFactor: lie.powerFactor)
            XCTAssertEqual(shot.total, 20, accuracy: 0.01)
        }
    }

    @MainActor
    func testOpeningTargetsFollowTheFairwayInsteadOfThePin() {
        for course in Course.all {
            let round = CourseRound(course: course)
            XCTAssertEqual(round.hole.par, 4)
            XCTAssertGreaterThan(round.hole.length, 330)
            XCTAssertNotEqual(round.intendedTarget, round.hole.pin)
            XCTAssertEqual(round.hole.lie(at: round.intendedTarget), .fairway)
            XCTAssertGreaterThan(abs(round.heading - round.ball.heading(to: round.hole.pin)), 12)
            XCTAssertEqual(round.targetLabel, "LANDING")
            let first = round.intendedTarget
            XCTAssertNotEqual(round.hole.recommendedTarget(from: first), first, "advance to the next station at the dogleg")
            round.selectTarget(round.hole.pin)
            XCTAssertEqual(round.targetLabel, "PIN")
            round.followFairway()
            XCTAssertEqual(round.intendedTarget, first)
        }
        let short = Course.easy.holes[2]
        XCTAssertEqual(short.recommendedTarget(from: short.tee), short.pin)
        let hole = Course.easy.holes[0]
        XCTAssertEqual(hole.recommendedTarget(from: CoursePoint(x: hole.pin.x, d: hole.pin.d - 10)), hole.pin)
        XCTAssertEqual(hole.recommendedTarget(from: CoursePoint(x: 70, d: 290)), hole.pin)
    }

    @MainActor
    func testBallClubAndGolferShareCourseScale() throws {
        let scene = CourseScene()
        let sphere = try XCTUnwrap(scene.scene.rootNode.childNode(withName: "visibilityAssistedBall", recursively: true)?.geometry as? SCNSphere)
        let stance = try XCTUnwrap(scene.scene.rootNode.childNode(withName: "yardScaleStance", recursively: true))
        let head = try XCTUnwrap(stance.childNode(withName: "clubHead", recursively: true)?.geometry as? SCNBox)
        let headWidth = head.length * CGFloat(AvatarSize.courseScale)
        XCTAssertGreaterThan(headWidth, sphere.radius * 2)
        XCTAssertLessThan(headWidth / (sphere.radius * 2), 3)
        XCTAssertEqual(sphere.radius, 0.0235, accuracy: 0.0001, "The locator ring supplies visibility without inflating the ball")
        XCTAssertEqual(AvatarSize.courseBallRadius, 0.0235, accuracy: 0.0001, "visibility does not change physics")
        XCTAssertEqual(stance.scale.y, AvatarSize.courseScale)
        XCTAssertLessThan(abs(AvatarAnimations.address.clubHead.y - AvatarSize.ball.y), 0.03)
        XCTAssertNotNil(scene.scene.rootNode.childNode(withName: "ballLocatorNotBallGeometry", recursively: true))
    }

    @MainActor
    func testCourseDressingIsBoundedAndKeepsPlayingSurfaceFlat() {
        let scene = CourseScene()
        for hole in Course.all.flatMap(\.holes) {
            let unchanged = hole
            scene.load(hole)
            var nodes = 0
            scene.scene.rootNode.enumerateChildNodes { _, _ in nodes += 1 }
            if nodes>=850 {
                var owners:[String:Int]=[:]
                scene.scene.rootNode.enumerateChildNodes { node,_ in
                    let owner=node.name ?? node.parent?.name ?? node.parent?.parent?.name ?? "unnamed"
                    owners[owner,default:0] += 1
                }
                print("COURSE_NODE_BUDGET hole=\(hole.number) length=\(hole.length) nodes=\(nodes) owners=\(owners.sorted {$0.value>$1.value}.prefix(12))")
            }
            XCTAssertLessThan(nodes, 900, "Hole \(hole.number), length \(hole.length): course dressing needs a bounded mobile render budget")
            XCTAssertNotNil(scene.scene.rootNode.childNode(withName: "sculptedLandscape", recursively: true))
            XCTAssertNotNil(scene.scene.rootNode.childNode(withName: "cartPath", recursively: true))
            for point in hole.centerline {
                XCTAssertEqual(CourseArt.elevation(point, hole: hole), Float(hole.surface(at: point).heightYards) - 2.0, accuracy: 0.001,
                               "The existing two-yard underlay clearance must not pierce bunker floors")
            }
            XCTAssertEqual(scene.hole, unchanged, "visual dressing cannot change lie or shot geometry")
            scene.load(hole)
            var after = 0
            scene.scene.rootNode.enumerateChildNodes { _, _ in after += 1 }
            XCTAssertEqual(after, nodes, "repeated load must not duplicate scenery")
        }
    }

    func testCoursesGetHarder() {
        XCTAssertEqual(Course.all.prefix(3).map(\.difficulty), [.easy, .medium, .hard])
        XCTAssertEqual(Set(Course.all.map(\.id)).count, Course.all.count)
        XCTAssertEqual(Course.sunward.holes.map(\.par), [4, 3, 5])
        XCTAssertTrue(Course.all.filter { $0.id != Course.sunwardResort.id }.allSatisfy { $0.holes.count == 3 })
        XCTAssertEqual(Course.sunwardResort.holes.count, 9)
        XCTAssertEqual(Course.sunwardResort.par, 36)
        XCTAssertLessThan(Course.easy.par, Course.hard.par)
        let averageWidth = { (course: Course) in course.holes.map(\.fairwayWidth).reduce(0, +) / Double(course.holes.count) }
        XCTAssertGreaterThan(averageWidth(.easy), averageWidth(.medium))
        XCTAssertGreaterThan(averageWidth(.medium), averageWidth(.hard))
        XCTAssertFalse(Course.easy.hasWater)
        XCTAssertTrue(Course.hard.hasWater)
        XCTAssertTrue(Course.all.allSatisfy { $0.bunkerCount > 0 })
    }

    func testLiesFollowTheHoleLayout() {
        let hole = Course.easy.holes[1] // dogleg left
        XCTAssertEqual(hole.lie(at: hole.tee), .tee)
        XCTAssertEqual(hole.lie(at: CoursePoint(x: 0, d: 100)), .fairway)
        XCTAssertEqual(hole.lie(at: CoursePoint(x: hole.fairwayWidth / 2 + 5, d: 100)), .rough)
        XCTAssertEqual(hole.lie(at: CoursePoint(x: 120, d: 100)), .outOfBounds)
        XCTAssertEqual(hole.lie(at: hole.pin), .green)
        let bunker = hole.hazards[0]
        XCTAssertEqual(hole.lie(at: CoursePoint(x: bunker.x, d: bunker.distance)), .bunker)
        // Along the dogleg's second leg, not the tee line.
        XCTAssertEqual(hole.lie(at: CoursePoint(x: -9, d: 225)), .fairway)
        XCTAssertEqual(hole.lie(at: CoursePoint(x: 30, d: 260)), .rough)
        XCTAssertEqual(hole.lie(at: CoursePoint(x: 60, d: 260)), .outOfBounds)
        let water = Course.hard.holes[0].hazards.first { $0.kind == .water }!
        XCTAssertEqual(Course.hard.holes[0].lie(at: CoursePoint(x: water.x, d: water.distance)), .water)
    }

    func testShotFromTheFairwayTravelsTowardThePin() {
        let hole = Course.easy.holes[1]
        let origin = CoursePoint(x: 0, d: 170)
        let heading = origin.heading(to: hole.pin)
        XCTAssertLessThan(heading, 0, "the pin is up and to the left")
        let shot = RangeShot(id: 1, club: .wedge, power: 0.8, aim: 0, origin: origin, heading: heading, hole: hole)
        XCTAssertEqual(shot.position(at: 0).distanceYards, origin.d, accuracy: 0.001)
        XCTAssertEqual(shot.rest.distance(to: origin), shot.total, accuracy: 0.01)
        let before = origin.distance(to: hole.pin)
        XCTAssertLessThan(shot.rest.distance(to: hole.pin), before)
        XCTAssertLessThan(shot.rest.x, origin.x)
    }

    func testWaterCostsAStrokeAndDropsShortOfIt() {
        let hole = Course.hard.holes[2] // water carry from the tee
        let water = hole.hazards.first { $0.kind == .water }!
        let power = try! XCTUnwrap(RangeShot.power(toReach: water.distance, with: .iron))
        let shot = RangeShot(id: 1, club: .iron, power: power, aim: 0, hole: hole)
        XCTAssertEqual(shot.lie, .water)
        XCTAssertEqual(shot.penaltyStrokes, 1)
        XCTAssertNotEqual(hole.lie(at: shot.nextPosition), .water)
        XCTAssertLessThan(shot.nextPosition.d, water.distance)
    }

    func testOutOfBoundsReplaysFromTheSameSpot() {
        let hole = Course.hard.holes[0]
        let shot = RangeShot(id: 1, club: .driver, power: 1, aim: 22, curve: 15, hole: hole)
        XCTAssertEqual(shot.lie, .outOfBounds)
        XCTAssertEqual(shot.nextPosition, hole.tee)
        XCTAssertEqual(shot.penaltyStrokes, 1)
    }

    func testPuttRollingSlowlyOverTheCupDrops() {
        var hole = Course.easy.holes[0]
        hole.terrain = .flat // capture mechanics, read on a level green
        let origin = CoursePoint(x: hole.pin.x, d: hole.pin.d - 6)
        let power = try! XCTUnwrap(RangeShot.power(toReach: 6.2, with: .putter))
        let putt = RangeShot(id: 1, club: .putter, power: power, aim: 0, origin: origin, heading: origin.heading(to: hole.pin), hole: hole)
        XCTAssertTrue(putt.isHoled)
        let wide = RangeShot(id: 2, club: .putter, power: power, aim: 15, origin: origin, heading: origin.heading(to: hole.pin), hole: hole)
        XCTAssertLessThan(putt.duration, wide.duration, "a holed putt ends when it drops, before it would have stopped")
        XCTAssertFalse(wide.isHoled)
        XCTAssertEqual(wide.lie, .green)
    }

    @MainActor
    func testStrokesCountUntilHoledThenPlayersAndHolesRotate() {
        let defaults = UserDefaults(suiteName: "CourseTests-\(UUID().uuidString)")!
        let round = CourseRound(defaults: defaults)
        round.start(course: .easy, playerCount: 2)
        let start = Date(timeIntervalSince1970: 100)
        XCTAssertEqual(round.phase, .ready)
        XCTAssertEqual(round.ball, round.hole.tee)

        playUntilHoled(round, from: start)
        XCTAssertEqual(round.phase, .holed)
        let firstScore = try! XCTUnwrap(round.scores[0][0])
        XCTAssertGreaterThan(firstScore, 0)
        round.replay(at: start)
        round.advance(at: start.addingTimeInterval(30))
        XCTAssertEqual(round.phase, .holed, "a replay changes nothing")
        XCTAssertEqual(round.scores[0][0], firstScore)

        round.continueAfterHole()
        XCTAssertEqual(round.playerIndex, 1)
        XCTAssertEqual(round.holeIndex, 0)
        XCTAssertEqual(round.strokes, 0)
        XCTAssertEqual(round.ball, round.hole.tee)

        playUntilHoled(round, from: start)
        round.continueAfterHole()
        XCTAssertEqual(round.playerIndex, 0)
        XCTAssertEqual(round.holeIndex, 1)

        for _ in 0..<4 {
            playUntilHoled(round, from: start)
            round.continueAfterHole()
        }
        XCTAssertEqual(round.phase, .complete)
        XCTAssertTrue(round.scores.allSatisfy { $0.allSatisfy { $0 != nil } })
        XCTAssertNil(round.best, "best scores are solo only")
    }

    @MainActor
    func testStrokeCapPicksUpAndSoloBestIsSaved() {
        let defaults = UserDefaults(suiteName: "CourseTests-\(UUID().uuidString)")!
        let round = CourseRound(defaults: defaults)
        round.start(course: .easy, playerCount: 1)
        let start = Date(timeIntervalSince1970: 100)
        let cap = round.hole.par + CourseRound.strokesOverParCap
        while round.phase != .holed {
            round.club = .putter
            round.charge(0.1)
            XCTAssertTrue(round.release(at: start))
            round.advance(at: start.addingTimeInterval(30))
            round.nextShot()
        }
        XCTAssertTrue(round.pickedUp)
        XCTAssertEqual(round.scores[0][0], cap)
        round.continueAfterHole()
        for _ in 0..<2 {
            playUntilHoled(round, from: start)
            round.continueAfterHole()
        }
        XCTAssertEqual(round.phase, .complete)
        XCTAssertEqual(round.best, round.toPar(for: 0))
        let reloaded = CourseRound(defaults: defaults)
        reloaded.start(course: .easy, playerCount: 1)
        XCTAssertEqual(reloaded.best, round.toPar(for: 0))
    }

    @MainActor
    func testClubSuggestionAndPausedFlight() {
        let round = CourseRound(defaults: UserDefaults(suiteName: "CourseTests-\(UUID().uuidString)")!)
        round.start(course: .medium, playerCount: 1)
        XCTAssertEqual(round.club, .wood3, "Club selection targets the safe landing station, not the distant pin")
        let start = Date(timeIntervalSince1970: 100)
        round.charge(0)
        XCTAssertFalse(round.release(at: start))
        XCTAssertEqual(round.strokes, 0)
        round.charge(0.8)
        XCTAssertTrue(round.release(at: start))
        round.pause(at: start.addingTimeInterval(1))
        round.resume(at: start.addingTimeInterval(101))
        XCTAssertEqual(round.elapsed(at: start.addingTimeInterval(101)), 1)
        round.skipFlight(at: start.addingTimeInterval(102))
        XCTAssertNotEqual(round.phase, .flying)
        XCTAssertEqual(round.strokes, 1 + (round.activeShot?.penaltyStrokes ?? 0))
    }

    /// Plays through the recommended landing areas with a near-perfect distance, then putts out.
    @MainActor
    private func playUntilHoled(_ round: CourseRound, from start: Date, file: StaticString = #filePath, line: UInt = #line) {
        var guardCount = 0
        while round.phase != .holed {
            guardCount += 1
            guard guardCount < 20 else { return XCTFail("hole never finished", file: file, line: line) }
            if round.phase == .landed { round.nextShot() }
            let club = round.suggestedClub()
            round.club = club
            let power: Double
            if club == .putter {
                power = RangeShot.power(toReach: round.distanceToPin, with: .putter) ?? 1
            } else {
                let target = round.distanceToTarget / round.lie.powerFactor
                power = RangeShot.power(toReach: min(target, club.mockDistance), with: club) ?? 1
            }
            round.charge(max(power, 0.07))
            XCTAssertTrue(round.release(at: start), file: file, line: line)
            round.advance(at: start.addingTimeInterval(30))
        }
    }

    func testEveryClubHasZeroLaunchAtZeroPowerAndWhiffsStayPut() {
        for club in GolfClub.allCases {
            let zero = RangeShot(id: 1, club: club, power: 0, aim: 0)
            XCTAssertEqual(zero.total, 0, accuracy: 0.000001)
            XCTAssertEqual(club.launch(power: 0, aimDegrees: 0, curveDegrees: 0).ballSpeedMPH, 0)
            let origin = CoursePoint(x: Course.easy.holes[0].pin.x, d: Course.easy.holes[0].pin.d - 0.05)
            let miss = RangeShot(id: 2, club: club, power: 1, aim: 0, strike: .miss, origin: origin, hole: Course.easy.holes[0])
            XCTAssertEqual(miss.total, 0, accuracy: 0.000001)
            XCTAssertEqual(miss.nextPosition, origin)
            XCTAssertFalse(miss.isHoled)
            XCTAssertEqual(miss.penaltyStrokes, 0)
        }
    }

    func testPreciseShortPuttsAndChipsHaveSolutions() throws {
        for distance in [0.01, 0.1, 0.25, 0.5, 1.0, 1.2, 8.0] {
            let power = try XCTUnwrap(RangeShot.power(toReach: distance, with: .putter))
            let shot = RangeShot(id: 1, club: .putter, power: power, aim: 0)
            XCTAssertEqual(shot.total, distance, accuracy: 0.0001)
        }
        for club in [GolfClub.wedge, .driver] {
            let soft = RangeShot(id: 1, club: club, power: 0.001, aim: 0)
            XCTAssertLessThan(soft.total, 0.01)
        }
    }

    func testHoledBallRenderedAndLogicalEndpointsAgree() throws {
        var hole = Course.easy.holes[0]
        hole.terrain = .flat
        let origin = CoursePoint(x: hole.pin.x, d: hole.pin.d - 8)
        let power = try XCTUnwrap(RangeShot.power(toReach: 8, with: .putter))
        let shot = RangeShot(id: 1, club: .putter, power: power, aim: 0, origin: origin, hole: hole)
        XCTAssertTrue(shot.isHoled)
        let endpoint = shot.position(at: shot.duration)
        XCTAssertEqual(endpoint.distanceYards, shot.nextPosition.d, accuracy: 0.000001)
        XCTAssertEqual(endpoint.lateralYards, shot.nextPosition.x, accuracy: 0.000001)
        XCTAssertEqual(shot.rest, hole.pin)
        let short = RangeShot(id: 2, club: .putter, power: try XCTUnwrap(RangeShot.power(toReach: 7.5, with: .putter)), aim: 0, origin: origin, hole: hole)
        XCTAssertFalse(short.isHoled, "capture must not rescue a half-yard distance error")
    }

    func testTargetAndExecutionAreSeparateAndAllowRecoveryDirections() {
        let request = ShotRequest(club: .iron, targetHeading: 120, type: .full,
                                  execution: SwingImpact(power: 0.5, startLineDegrees: 8, confidence: 0.8, source: .camera))
        let shot = RangeShot(id: 1, request: request, origin: .zero)
        XCTAssertEqual(shot.request, request)
        XCTAssertEqual(shot.heading, 120)
        XCTAssertEqual(shot.aim, 8)
        XCTAssertLessThan(shot.landing.distanceYards, 0)
        XCTAssertEqual(CoursePoint.zero.heading(to: shot.rest), 128, accuracy: 0.01)
    }

    func testChipAndPitchRangesAreExplicitAndProgressive() {
        func shot(_ type: ShotType) -> RangeShot {
            RangeShot(id: 1, request: ShotRequest(club: .wedge, targetHeading: 0, type: type, execution: SwingImpact(power: 0.7)), origin: .zero)
        }
        XCTAssertLessThan(shot(.chip).total, shot(.pitch).total)
        XCTAssertLessThan(shot(.pitch).total, shot(.full).total)
    }

    @MainActor
    func testRepeatedPauseIsIdempotentAndBlocksShots() {
        let round = CourseRound()
        let start = Date(timeIntervalSince1970: 100)
        round.charge(1)
        XCTAssertTrue(round.release(at: start))
        round.pause(at: start.addingTimeInterval(1))
        round.pause(at: start.addingTimeInterval(5))
        round.resume(at: start.addingTimeInterval(10))
        XCTAssertEqual(round.elapsed(at: start.addingTimeInterval(10)), 1)
        round.restart()
        round.pause()
        round.charge(0.5)
        XCTAssertFalse(round.release())
    }

    @MainActor
    func testPuttingGainDoesNotDependOnPinDistance() throws {
        func shot(pin: Double) throws -> RangeShot {
            let hole = Hole(number: 1, par: 3, centerline: [.zero, CoursePoint(x: 0, d: pin)], fairwayWidth: 25, greenRadius: 20, hazards: [])
            let course = Course(id: "gain-\(pin)", name: "Gain", difficulty: .easy, holes: [hole])
            let round = CourseRound(course: course)
            round.club = .putter
            round.charge(0.1)
            XCTAssertTrue(round.release())
            return try XCTUnwrap(round.activeShot)
        }
        let near = try shot(pin: 2), far = try shot(pin: 12)
        XCTAssertEqual(near.power, 0.1)
        XCTAssertEqual(near.flight, far.flight)
    }

    @MainActor
    func testWhiffCountsOnceWithoutMovingAndReplayDoesNotAddStroke() throws {
        let round = CourseRound()
        let origin = round.ball
        let start = Date(timeIntervalSince1970: 100)
        round.charge(1)
        XCTAssertTrue(round.release(at: start, strike: .miss))
        round.advance(at: start.addingTimeInterval(1))
        XCTAssertEqual(round.strokes, 1)
        XCTAssertEqual(round.ball, origin)
        XCTAssertEqual(round.phase, .landed)
        round.replay(at: start)
        round.advance(at: start.addingTimeInterval(1))
        XCTAssertEqual(round.strokes, 1)
    }

    @MainActor
    func testChosenTargetAndFineAimResetForNextShot() {
        let round = CourseRound()
        let target = CoursePoint(x: 15, d: -30)
        round.selectTarget(target)
        round.adjustAim(80)
        round.club = .putter
        round.adjustAim(round.aimStep)
        XCTAssertEqual(round.aim, 80.25)
        round.charge(0.1)
        let start = Date(timeIntervalSince1970: 100)
        XCTAssertTrue(round.release(at: start))
        round.advance(at: start.addingTimeInterval(30))
        round.nextShot()
        XCTAssertNil(round.target)
        XCTAssertEqual(round.aim, 0)
        XCTAssertEqual(round.club, round.suggestedClub())
    }

    @MainActor
    func testLowConfidenceAndShotEditingCannotSpendAStroke() {
        let round = CourseRound()
        round.editingShot = true
        round.charge(0.5)
        XCTAssertFalse(round.release())
        round.editingShot = false
        round.charge(0.5)
        XCTAssertFalse(round.release(execution: SwingImpact(power: 0.5, confidence: 0.2, source: .camera)))
        XCTAssertEqual(round.strokes, 0)
    }

    @MainActor
    func testTeeToCupCompletesWithoutStrokeCap() throws {
        let hole = Hole(number: 1, par: 3, centerline: [.zero, CoursePoint(x: 0, d: 50)], fairwayWidth: 30, greenRadius: 10, hazards: [])
        let course = Course(id: "regression-hole", name: "Practice", difficulty: .easy, holes: [hole])
        let round = CourseRound(course: course)
        let start = Date(timeIntervalSince1970: 100)
        round.club = .wedge
        round.charge(try XCTUnwrap(RangeShot.power(toReach: 45, with: .wedge)))
        XCTAssertTrue(round.release(at: start))
        round.advance(at: start.addingTimeInterval(30))
        XCTAssertEqual(round.phase, .landed)
        round.nextShot()
        XCTAssertEqual(round.club, .putter)
        round.charge(try XCTUnwrap(RangeShot.power(toReach: round.distanceToPin, with: .putter)))
        XCTAssertTrue(round.release(at: start))
        round.advance(at: start.addingTimeInterval(30))
        XCTAssertEqual(round.phase, .holed)
        XCTAssertFalse(round.pickedUp)
        XCTAssertEqual(round.strokes, 2)
        XCTAssertEqual(round.ball, hole.pin)
    }
}
