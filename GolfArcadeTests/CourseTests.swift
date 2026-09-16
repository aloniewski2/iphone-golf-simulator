import XCTest
@testable import GolfArcade

final class CourseTests: XCTestCase {
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
        let hole = Course.easy.holes[0]
        let origin = CoursePoint(x: 0, d: hole.pin.d - 6)
        let power = try! XCTUnwrap(RangeShot.power(toReach: 7, with: .putter))
        let putt = RangeShot(id: 1, club: .putter, power: power, aim: 0, origin: origin, heading: origin.heading(to: hole.pin), hole: hole)
        XCTAssertTrue(putt.isHoled)
        XCTAssertLessThan(putt.duration, putt.flight.duration)
        let wide = RangeShot(id: 2, club: .putter, power: power, aim: 15, origin: origin, heading: origin.heading(to: hole.pin), hole: hole)
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
        XCTAssertEqual(round.club, .driver)
        let start = Date(timeIntervalSince1970: 100)
        round.charge(0.02)
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

    /// Plays straight at the pin with a near-perfect distance, then putts out.
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
                power = 1 / 1.6 // rolls exactly to the cup
            } else {
                let target = round.distanceToPin / round.lie.powerFactor
                power = RangeShot.power(toReach: min(target, club.mockDistance), with: club) ?? 1
            }
            round.charge(max(power, 0.07))
            XCTAssertTrue(round.release(at: start), file: file, line: line)
            round.advance(at: start.addingTimeInterval(30))
        }
    }
}
