import XCTest
@testable import GolfArcade

final class HoleTests: XCTestCase {
    let hole = Hole.first

    func testLiesAcrossTheHole() {
        XCTAssertEqual(hole.lie(at: hole.tee), .tee)
        XCTAssertEqual(hole.lie(at: CoursePoint(x: 5, z: -120)), .fairway)
        XCTAssertEqual(hole.lie(at: CoursePoint(x: 40, z: -120)), .rough)
        XCTAssertEqual(hole.lie(at: hole.bunkers[0].center), .bunker)
        XCTAssertEqual(hole.lie(at: hole.greenCenter), .green)
        XCTAssertEqual(hole.lie(at: CoursePoint(x: -30, z: -330)), .fairway, "the dogleg corridor follows the centreline")
        XCTAssertEqual(hole.par, 4)
        XCTAssertEqual(hole.length, 366, accuracy: 1)
    }

    func testHeadingsAndNames() {
        XCTAssertEqual(CoursePoint(x: 0, z: 0).heading(to: CoursePoint(x: 0, z: -100)), 0, accuracy: 1e-9)
        XCTAssertEqual(CoursePoint(x: 0, z: 0).heading(to: CoursePoint(x: 100, z: 0)), 90, accuracy: 1e-9)
        XCTAssertLessThan(hole.tee.heading(to: hole.cup), 0, "the pin is left of straight downrange")
        XCTAssertEqual(Hole.scoreName(strokes: 3, par: 4), "Birdie")
        XCTAssertEqual(Hole.scoreName(strokes: 4, par: 4), "Par")
        XCTAssertEqual(Hole.scoreName(strokes: 6, par: 4), "Double bogey")
    }

    func testCourseShotStartsWhereTheBallLiesAndAimsAtThePin() {
        let from = CoursePoint(x: 10, z: -150)
        let heading = from.heading(to: hole.cup)
        let shot = RangeShot(id: 1, club: .iron, power: 0.8, aim: 0, from: from, heading: heading, lie: .fairway, on: hole)
        XCTAssertEqual(shot.coursePoint(at: 0).x, from.x, accuracy: 1e-9)
        XCTAssertEqual(shot.coursePoint(at: 0).z, from.z, accuracy: 1e-9)
        let rest = shot.restingPoint
        XCTAssertLessThan(rest.distance(to: hole.cup), from.distance(to: hole.cup), "it flew toward the pin")
        XCTAssertEqual(from.heading(to: rest), heading, accuracy: 0.5, "straight shots stay on the heading")
    }

    func testRoughAndSandCostDistance() {
        let fairway = RangeShot(id: 1, club: .iron, power: 0.8, aim: 0, from: hole.tee, heading: 0, lie: .fairway, on: hole)
        let rough = RangeShot(id: 1, club: .iron, power: 0.8, aim: 0, from: hole.tee, heading: 0, lie: .rough, on: hole)
        let sand = RangeShot(id: 1, club: .iron, power: 0.8, aim: 0, from: hole.tee, heading: 0, lie: .bunker, on: hole)
        XCTAssertLessThan(rough.total, fairway.total * 0.9)
        XCTAssertLessThan(sand.total, rough.total)
    }

    func testSoftPuttIsATapInAndFullPuttCrossesTheGreen() {
        let from = CoursePoint(x: hole.cup.x, z: hole.cup.z + 8)
        let heading = from.heading(to: hole.cup)
        let soft = RangeShot(id: 1, club: .putter, power: 0.1, aim: 0, from: from, heading: heading, lie: .green, on: hole)
        let full = RangeShot(id: 2, club: .putter, power: 1, aim: 0, from: from, heading: heading, lie: .green, on: hole)
        XCTAssertLessThan(soft.total, 2, "a soft putt barely moves")
        XCTAssertGreaterThan(full.total, 20, "a full putt runs across the green")
    }

    func testPuttDropsWhenItRollsOverTheCup() {
        let from = CoursePoint(x: hole.cup.x, z: hole.cup.z + 6) // 18 ft straight putt
        let heading = from.heading(to: hole.cup)
        var holed: RangeShot?
        for p in stride(from: 0.1, through: 1.0, by: 0.02) {
            let shot = RangeShot(id: 1, club: .putter, power: p, aim: 0, from: from, heading: heading, lie: .green, on: hole)
            if shot.isHoled { holed = shot; break }
        }
        let shot = try! XCTUnwrap(holed, "some putting power holes an 18-footer")
        XCTAssertEqual(shot.targetName, "In the hole")
        XCTAssertLessThan(shot.duration, shot.flight.duration, "playback stops at the cup")
        XCTAssertLessThan(shot.restingPoint.distance(to: hole.cup), hole.cupRadius + 0.01)
        let miss = RangeShot(id: 2, club: .putter, power: 0.9, aim: 8, from: from, heading: heading, lie: .green, on: hole)
        XCTAssertFalse(miss.isHoled, "a putt pushed 8° right misses")
    }

    @MainActor
    func testPlayTheHoleTeeToCup() {
        let defaults = UserDefaults(suiteName: "HoleTests-\(UUID().uuidString)")!
        let round = RangeRound(mode: .hole(hole), defaults: defaults)
        let start = Date(timeIntervalSince1970: 100)
        XCTAssertEqual(round.club, .driver)
        XCTAssertEqual(round.lie, .tee)
        XCTAssertEqual(Int(round.yardsToPin), Int(hole.length))

        // Drive down the middle.
        round.charge(0.9)
        XCTAssertTrue(round.release(at: start))
        round.advance(at: start.addingTimeInterval(30))
        XCTAssertEqual(round.phase, .landed)
        XCTAssertEqual(round.strokes, 1)
        XCTAssertLessThan(round.yardsToPin, 180)
        round.nextShot()

        // Approach: pick the club and power that reach the pin, then putt out.
        var strokes = 1
        while round.phase != .complete, strokes < 8 {
            let distance = round.yardsToPin
            let club: GolfClub = round.lie == .green ? .putter : distance > 150 ? .iron : .wedge
            round.club = club
            var power = RangeShot.power(toReach: distance, with: club) ?? 1
            if round.lie == .green {
                // Putts use their own scale: pick the softest power that holes out, else the closest.
                let candidates = stride(from: 0.05, through: 1.0, by: 0.01).map { p -> (Double, RangeShot) in
                    (p, RangeShot(id: 0, club: .putter, power: p, aim: 0, from: round.ballPosition, heading: round.baseHeading, lie: .green, on: hole))
                }
                power = candidates.first { $0.1.isHoled }?.0 ?? candidates.min { $0.1.restingPoint.distance(to: hole.cup) < $1.1.restingPoint.distance(to: hole.cup) }!.0
            }
            round.charge(max(0.05, power))
            XCTAssertTrue(round.release(at: start))
            round.advance(at: start.addingTimeInterval(30))
            strokes += 1
            XCTAssertEqual(round.strokes, strokes)
            if round.phase == .landed { round.nextShot() }
        }
        XCTAssertEqual(round.phase, .complete)
        XCTAssertTrue(round.isHoled)
        XCTAssertLessThanOrEqual(round.strokes, 6)
        XCTAssertEqual(round.best, round.strokes)
        XCTAssertEqual(RangeRound(mode: .hole(hole), defaults: defaults).best, round.strokes, "best strokes persist")

        round.restart()
        XCTAssertEqual(round.lie, .tee)
        XCTAssertEqual(round.strokes, 0)
        XCTAssertEqual(round.ballPosition, hole.tee)
    }

    @MainActor
    func testSwitchingModesResetsAndKeepsSeparateBests() {
        let defaults = UserDefaults(suiteName: "HoleTests-\(UUID().uuidString)")!
        let round = RangeRound(defaults: defaults)
        XCTAssertNil(round.hole)
        round.switchMode(.hole(hole))
        XCTAssertEqual(round.hole, hole)
        XCTAssertEqual(round.club, .driver)
        round.switchMode(.range)
        XCTAssertNil(round.hole)
        XCTAssertEqual(round.club, .iron)
    }
}
