import XCTest
import simd
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
    func testPuttingViewSwitchesOnTheGreenWithThePutterAndDuringThePutt() {
        let round = RangeRound(mode: .hole(hole), defaults: UserDefaults(suiteName: "HoleTests-\(UUID().uuidString)")!)
        XCTAssertFalse(round.isPutting, "not on the tee")
        let start = Date()
        round.charge(0.9); round.release(at: start); round.advance(at: start.addingTimeInterval(30)); round.nextShot()
        round.club = .wedge
        var onGreen = 0.9
        for p in stride(from: 0.3, through: 1.0, by: 0.005) {
            let probe = RangeShot(id: 0, club: .wedge, power: p, aim: 0, from: round.ballPosition, heading: round.baseHeading, lie: round.lie, on: hole)
            if hole.lie(at: probe.restingPoint) == .green { onGreen = p; break }
        }
        round.charge(onGreen); round.release(at: start); round.advance(at: start.addingTimeInterval(30)); round.nextShot()
        XCTAssertEqual(round.lie, .green)
        XCTAssertEqual(round.club, .putter)
        XCTAssertTrue(round.isPutting, "green + putter = putting view")
        round.club = .wedge
        XCTAssertFalse(round.isPutting, "chipping from the green is not the putting view")
        round.club = .putter
        round.charge(0.4); round.release(at: start)
        XCTAssertTrue(round.isPutting, "the view holds while the putt rolls")
        XCTAssertFalse(RangeRound(defaults: UserDefaults(suiteName: "HoleTests-range")!).isPutting, "never on the range")
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

final class ShotPreviewTests: XCTestCase {
    @MainActor
    func testPreviewShowsFullPowerAtAddressAndTheLiveLoadWhileCharging() {
        let round = RangeRound(mode: .hole(.first), defaults: UserDefaults(suiteName: "Preview-\(UUID().uuidString)")!)
        let full = try! XCTUnwrap(round.preview)
        XCTAssertEqual(full.power, 1)
        XCTAssertEqual(full.club, .driver)
        XCTAssertEqual(full.total, GolfClub.driver.mockDistance, accuracy: 25, "the hole's hills change the roll-out from the flat range number")
        XCTAssertTrue(round.preview == full, "cached while nothing changes")

        round.charge(0.5)
        let half = try! XCTUnwrap(round.preview)
        XCTAssertEqual(half.power, 0.5, accuracy: 0.011)
        XCTAssertLessThan(half.total, full.total * 0.8, "the landing ring walks in as the load drops")
        round.charge(0.503)
        XCTAssertEqual(round.preview, half, "tiny drag changes reuse the cached preview")

        round.aim = 10
        let aimed = try! XCTUnwrap(round.preview)
        XCTAssertGreaterThan(aimed.restingPoint.x, half.restingPoint.x, "aim right moves the line-up right")

        round.release()
        XCTAssertNil(round.preview, "no line-up while the ball is in the air")
    }

    @MainActor
    func testPreviewFollowsTheBallAroundTheCourse() {
        let hole = Hole.first
        let round = RangeRound(mode: .hole(hole), defaults: UserDefaults(suiteName: "Preview-\(UUID().uuidString)")!)
        let start = Date()
        round.charge(0.9)
        round.release(at: start)
        round.advance(at: start.addingTimeInterval(30))
        round.nextShot()
        let next = try! XCTUnwrap(round.preview)
        XCTAssertEqual(next.origin, round.ballPosition)
        XCTAssertEqual(next.heading, round.ballPosition.heading(to: hole.cup), accuracy: 1e-9, "lined up on the pin from the new lie")
    }
}

final class TerrainTests: XCTestCase {
    let hole = Hole.first

    func testSlopeIsTheGradientOfTheElevation() {
        let step = 1e-4
        for point in [hole.tee, hole.cup, CoursePoint(x: 10, z: -200), CoursePoint(x: 30, z: -250), CoursePoint(x: -30, z: -350)] {
            let slope = hole.terrain.slope(at: point)
            let dx = (hole.elevation(at: CoursePoint(x: point.x + step, z: point.z)) - hole.elevation(at: CoursePoint(x: point.x - step, z: point.z))) / (2 * step)
            let dz = (hole.elevation(at: CoursePoint(x: point.x, z: point.z + step)) - hole.elevation(at: CoursePoint(x: point.x, z: point.z - step))) / (2 * step)
            XCTAssertEqual(slope.x, dx, accuracy: 1e-6)
            XCTAssertEqual(slope.z, dz, accuracy: 1e-6)
        }
        XCTAssertEqual(Terrain.flat.elevation(at: hole.cup), 0)
    }

    func testTheHoleIsSculpted() {
        XCTAssertGreaterThan(hole.elevation(at: hole.tee), 2, "elevated tee")
        XCTAssertLessThan(hole.elevation(at: CoursePoint(x: 4, z: -205)), -1, "swale in the landing area")
        XCTAssertGreaterThan(hole.elevation(at: hole.greenCenter), 2.5, "raised green")
        let cross = hole.terrain.slope(at: hole.cup)
        XCTAssertGreaterThan(hypot(cross.x, cross.z), 0.01, "the green tilts, so putts break")
        XCTAssertLessThan(hypot(cross.x, cross.z), 0.05, "but not so much a ball cannot stop on it")
        XCTAssertGreaterThan(hole.rise(from: hole.tee), -1, "the green sits about level with the tee")
        XCTAssertEqual(hole.playingDistance(from: CoursePoint(x: 0, z: -200)), CoursePoint(x: 0, z: -200).distance(to: hole.cup) + hole.rise(from: CoursePoint(x: 0, z: -200)), accuracy: 1e-9)
        XCTAssertGreaterThan(hole.rise(from: CoursePoint(x: 0, z: -200)), 3, "the approach from the swale plays uphill")
    }

    func testTheBallStaysOnTheGrass() {
        let shots = [
            RangeShot(id: 1, club: .driver, power: 1, aim: 0, from: hole.tee, heading: hole.tee.heading(to: hole.cup), lie: .tee, on: hole),
            RangeShot(id: 2, club: .iron, power: 0.9, aim: 6, from: CoursePoint(x: 0, z: -200), heading: CoursePoint(x: 0, z: -200).heading(to: hole.cup), lie: .fairway, on: hole),
            RangeShot(id: 3, club: .putter, power: 0.7, aim: 0, from: CoursePoint(x: -30, z: -352), heading: CoursePoint(x: -30, z: -352).heading(to: hole.cup), lie: .green, on: hole)
        ]
        for shot in shots {
            for sample in shot.flight.samples {
                let ground = hole.elevation(at: RangeShot.coursePoint(sample.point, origin: shot.origin, heading: shot.heading))
                XCTAssertGreaterThanOrEqual(sample.point.heightYards, ground - 0.02, "never under the ground")
            }
            let rest = shot.restingPoint
            XCTAssertEqual(shot.landing.heightYards, hole.elevation(at: rest), accuracy: 0.02, "comes to rest on the grass")
            XCTAssertEqual(shot.position(at: 0).heightYards, hole.elevation(at: shot.origin), accuracy: 1e-9, "starts on the grass")
        }
    }

    func testPuttsBreakDownTheSlope() {
        // Straight putts from a ring around the cup: each drifts toward the low side of the green.
        var broke = 0
        for angle in stride(from: 0.0, to: 360, by: 45) {
            let from = CoursePoint(x: hole.cup.x + 7 * cos(angle * .pi / 180), z: hole.cup.z + 7 * sin(angle * .pi / 180))
            guard hole.lie(at: from) == .green else { continue }
            let heading = from.heading(to: hole.cup)
            let putt = RangeShot(id: 1, club: .putter, power: 0.5, aim: 0, from: from, heading: heading, lie: .green, on: hole)
            let rest = putt.restingPoint
            let lateral = putt.landing.lateralYards
            // The lateral slope across the line, in the shot's frame: the ball must drift down it.
            let mid = CoursePoint(x: (from.x + rest.x) / 2, z: (from.z + rest.z) / 2)
            let slope = hole.terrain.slope(at: mid)
            let radians = heading * .pi / 180
            let across = slope.x * cos(radians) + slope.z * sin(radians)
            if abs(across) > 0.004 {
                XCTAssertEqual(lateral.sign, (-across).sign, "breaks downhill from \(from)")
                if abs(lateral) > 0.1 { broke += 1 }
            }
        }
        XCTAssertGreaterThan(broke, 2, "the green has enough tilt to break several putts")
    }

    func testDownhillPuttsRunFartherThanUphill() {
        // Same stroke, opposite directions across the cup: rise decides how far it runs.
        let cross = hole.terrain.slope(at: hole.cup)
        let direction = CoursePoint(x: cross.x / hypot(cross.x, cross.z), z: cross.z / hypot(cross.x, cross.z))
        let low = CoursePoint(x: hole.cup.x - direction.x * 6, z: hole.cup.z - direction.z * 6)
        let high = CoursePoint(x: hole.cup.x + direction.x * 6, z: hole.cup.z + direction.z * 6)
        XCTAssertGreaterThan(hole.rise(from: low), 0, "putting up the slope")
        XCTAssertLessThan(hole.rise(from: high), 0, "putting down the slope")
        let uphill = RangeShot(id: 1, club: .putter, power: 0.45, aim: 0, from: low, heading: low.heading(to: hole.cup), lie: .green, on: hole)
        let downhill = RangeShot(id: 2, club: .putter, power: 0.45, aim: 0, from: high, heading: high.heading(to: hole.cup), lie: .green, on: hole)
        XCTAssertGreaterThan(downhill.flight.total, uphill.flight.total * 1.15)
    }

    func testFlatGroundBehavesAsBefore() {
        let launch = GolfClub.iron.launch(power: 0.8, aimDegrees: 0, curveDegrees: 0)
        let flat = BallFlight.simulate(launch)
        let stated = BallFlight.simulate(launch, ground: BallFlight.flatGround)
        XCTAssertEqual(flat, stated)
        XCTAssertEqual(flat.landing.heightYards, 0)
        // A shot into a rising slope lands sooner and runs less than the same shot on the flat.
        let uphill = BallFlight.simulate(launch) { position in (position.y * 0.06, simd_double2(0, 0.06)) }
        XCTAssertLessThan(uphill.carry, flat.carry)
        XCTAssertLessThan(uphill.total, flat.total)
        XCTAssertGreaterThan(uphill.landing.heightYards, 5, "it finished up the hill")
    }
}

final class SceneScaleTests: XCTestCase {
    func testTheBallKeepsItsSizeOnScreenAndNeverVanishes() {
        // Apparent size is scale ÷ distance: the same from 18 yd out to 135 yd, so a ball that
        // has just landed 75 yd from the camera is as easy to see as one at address.
        let near = MeadowScene.adaptiveScale(distance: 18, full: 45, floor: 0.4, ceiling: 3)
        let landing = MeadowScene.adaptiveScale(distance: 75, full: 45, floor: 0.4, ceiling: 3)
        let far = MeadowScene.adaptiveScale(distance: 135, full: 45, floor: 0.4, ceiling: 3)
        XCTAssertEqual(near / 18, landing / 75, accuracy: 1e-6)
        XCTAssertEqual(landing / 75, far / 135, accuracy: 1e-6)
        XCTAssertEqual(MeadowScene.adaptiveScale(distance: 300, full: 45, floor: 0.4, ceiling: 3), 3, "capped, so it never becomes a boulder")
        XCTAssertEqual(MeadowScene.adaptiveScale(distance: 7, full: 45, floor: 0.4, ceiling: 3), 0.4, "and shrinks toward true scale on the green")
        XCTAssertEqual(MeadowScene.adaptiveScale(distance: 366, full: 90, floor: 0.3), 1, "the flag is full arcade size from the tee")
    }
}
