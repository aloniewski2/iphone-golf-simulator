import XCTest
import SceneKit
@testable import GolfArcade

final class CourseTests: XCTestCase {
    func testLieAwarePowerPredictionMatchesRealReach() throws {
        for lie in [CourseLie.fairway, .rough, .bunker] {
            let power = try XCTUnwrap(RangeShot.power(toReach: 20, with: .wedge, lieFactor: lie.powerFactor))
            let shot = RangeShot(id: 1, club: .wedge, power: power, aim: 0, lieFactor: lie.powerFactor)
            XCTAssertEqual(shot.total, 20, accuracy: 0.01)
        }
    }

    func testCoursesGetHarder() {
        XCTAssertEqual(Course.all.map(\.difficulty), [.easy, .medium, .hard])
        XCTAssertTrue(Course.all.allSatisfy { $0.holes.count == 3 })
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
            XCTAssertLessThan(soft.total, 1, "a tenth of a percent is a tenth of a percent of the rated distance, plus its dribble")
            XCTAssertGreaterThan(soft.total, 0)
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

}

final class CupPhysicsTests: XCTestCase {
    func testTheCupTakesADyingPuttAnywhereButAFirmOneOnlyThroughTheMiddle() {
        XCTAssertTrue(RangeShot.cupCaptures(speed: 0.3, offset: 0.11), "dead weight on the edge falls in")
        XCTAssertTrue(RangeShot.cupCaptures(speed: 1.6, offset: 0.0), "firm through the middle drops")
        XCTAssertFalse(RangeShot.cupCaptures(speed: 1.6, offset: 0.10), "the same pace on the edge horseshoes out")
        XCTAssertFalse(RangeShot.cupCaptures(speed: 2.4, offset: 0.0), "a rammed putt jumps the hole")
        XCTAssertFalse(RangeShot.cupCaptures(speed: 0.1, offset: 0.13), "past the rim is a miss however slow")
    }

    func testAFirmPuttOnTheEdgeLipsOutAndRunsPast() throws {
        let hole = Course.easy.holes[0]
        let from = CoursePoint(x: hole.pin.x, d: hole.pin.d - 3)
        let heading = from.heading(to: hole.pin)
        // Aimed to catch the edge of the cup at pace: it must not drop, and it must not simply
        // roll straight through the hole either — the rim kicks it aside and it finishes past.
        let edge = Double(atan2(0.1, 3) * 180 / .pi)
        let firm = try XCTUnwrap(RangeShot.power(toReach: 7, with: .putter))
        let putt = RangeShot(id: 1, club: .putter, power: firm, aim: edge, origin: from, heading: heading, hole: hole)
        XCTAssertFalse(putt.isHoled)
        let rest = putt.nextPosition
        XCTAssertGreaterThan(rest.distance(to: hole.pin), 0.4, "kicked away from the hole")
        XCTAssertLessThan(rest.distance(to: hole.pin), 3.5, "the rim took most of its pace")
        // The same line at dying pace drops.
        let soft = try XCTUnwrap(RangeShot.power(toReach: 3.3, with: .putter))
        let dying = RangeShot(id: 2, club: .putter, power: soft, aim: edge, origin: from, heading: heading, hole: hole)
        XCTAssertTrue(dying.isHoled, "a dying putt catching the edge falls in")
    }
}
