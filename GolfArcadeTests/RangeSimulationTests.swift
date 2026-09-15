import XCTest
@testable import GolfArcade

final class RangeSimulationTests: XCTestCase {
    func testPowerAndClubChangeDistanceAndAimChangesLanding() {
        let soft = RangeShot(id: 1, club: .driver, power: 0.2, aim: -15)
        let hard = RangeShot(id: 2, club: .driver, power: 0.9, aim: 15)
        let wedge = RangeShot(id: 3, club: .wedge, power: 0.9, aim: 0)
        XCTAssertLessThan(soft.total, hard.total)
        XCTAssertLessThan(wedge.total, hard.total)
        XCTAssertLessThan(soft.landing.lateralYards, 0)
        XCTAssertGreaterThan(hard.landing.lateralYards, 0)
    }

    func testBullseyeAndFairwayScoring() {
        let power = try! XCTUnwrap(RangeShot.power(toReach: 180, with: .driver))
        let perfect = RangeShot(id: 1, club: .driver, power: power, aim: 0)
        XCTAssertEqual(perfect.points, 100)
        XCTAssertEqual(perfect.targetName, "Summit")
        let miss = RangeShot(id: 2, club: .driver, power: 1, aim: 22)
        XCTAssertEqual(miss.points, 10)
        XCTAssertNil(miss.targetName)
    }

    func testFlightModelIsPlausibleGolf() {
        let drive = BallFlight.simulate(GolfClub.driver.launch(power: 1, aimDegrees: 0, curveDegrees: 0))
        XCTAssertGreaterThan(drive.carry, 200)
        XCTAssertLessThan(drive.carry, 300)
        XCTAssertGreaterThan(drive.apex, 20)
        XCTAssertLessThan(drive.apex, 50)
        XCTAssertGreaterThan(drive.roll, 5)
        XCTAssertGreaterThan(drive.duration, 5)
        let wedge = BallFlight.simulate(GolfClub.wedge.launch(power: 1, aimDegrees: 0, curveDegrees: 0))
        XCTAssertLessThan(wedge.carry, drive.carry)
        XCTAssertLessThan(wedge.roll, drive.roll, "high spin and steep descent check up faster")
        let putt = BallFlight.simulate(GolfClub.putter.launch(power: 1, aimDegrees: 0, curveDegrees: 0))
        XCTAssertLessThan(putt.apex, 0.3)
        XCTAssertEqual(putt.carry, 0)
        XCTAssertGreaterThan(putt.total, 20)
        XCTAssertLessThan(putt.total, 40)
    }

    func testCurveBendsTheBallAndDragShortensIt() {
        let straight = BallFlight.simulate(GolfClub.iron.launch(power: 0.9, aimDegrees: 0, curveDegrees: 0))
        let fade = BallFlight.simulate(GolfClub.iron.launch(power: 0.9, aimDegrees: 0, curveDegrees: 8))
        let draw = BallFlight.simulate(GolfClub.iron.launch(power: 0.9, aimDegrees: 0, curveDegrees: -8))
        XCTAssertEqual(straight.landing.lateralYards, 0, accuracy: 0.01)
        XCTAssertGreaterThan(fade.landing.lateralYards, 3)
        XCTAssertLessThan(draw.landing.lateralYards, -3)
        XCTAssertLessThan(fade.total, straight.total, "a tilted spin axis spends lift sideways")
        // Vacuum range for the same launch: no drag, no lift.
        var launch = GolfClub.driver.launch(power: 1, aimDegrees: 0, curveDegrees: 0)
        let v = launch.ballSpeedMPH * 0.44704
        let vacuumCarryYards = v * v * sin(2 * launch.launchAngleDegrees * .pi / 180) / 9.81 / 0.9144
        let spinning = BallFlight.simulate(launch)
        launch.spinRPM = 0
        let knuckleball = BallFlight.simulate(launch)
        XCTAssertLessThan(knuckleball.carry, vacuumCarryYards, "with no lift, drag must cost distance")
        XCTAssertGreaterThan(spinning.carry, knuckleball.carry * 1.3, "backspin lift is what keeps a drive in the air")
        XCTAssertGreaterThan(spinning.apex, knuckleball.apex * 1.5)
    }

    func testTrajectoryIsContinuousAndEndsAtScoredPosition() {
        for club in GolfClub.allCases {
            let shot = RangeShot(id: 1, club: club, power: 0.8, aim: 12)
            XCTAssertEqual(shot.position(at: 0).distanceYards, 0)
            var previous = shot.position(at: 0)
            for index in 1...1000 {
                let point = shot.position(at: shot.duration * Double(index) / 1000)
                XCTAssertGreaterThanOrEqual(point.distanceYards, previous.distanceYards)
                XCTAssertGreaterThanOrEqual(point.heightYards, -0.0001)
                XCTAssertLessThan(abs(point.heightYards - previous.heightYards), 0.5)
                previous = point
            }
            XCTAssertEqual(shot.landing.heightYards, 0, accuracy: 0.0001)
            XCTAssertEqual(hypot(shot.landing.distanceYards, shot.landing.lateralYards), shot.total, accuracy: 0.0001)
            if club == .putter { XCTAssertEqual(shot.position(at: 1).heightYards, 0) }
        }
    }

    @MainActor
    func testFiveShotRoundReplayAndRestart() {
        let defaults = UserDefaults(suiteName: "RangeTests-\(UUID().uuidString)")!
        let round = RangeRound(defaults: defaults)
        let start = Date(timeIntervalSince1970: 100)
        XCTAssertFalse(round.release(at: start))
        for index in 1...5 {
            round.charge(0.75)
            XCTAssertTrue(round.release(at: start))
            XCTAssertFalse(round.release(at: start))
            round.advance(at: start.addingTimeInterval(30))
            XCTAssertEqual(round.shots.count, index)
            let score = round.score
            round.replay(at: start)
            round.advance(at: start.addingTimeInterval(30))
            XCTAssertEqual(round.score, score)
            XCTAssertEqual(round.shots.count, index)
            if index < 5 { round.nextShot() }
        }
        XCTAssertEqual(round.phase, .complete)
        XCTAssertGreaterThan(round.best, 0)
        XCTAssertEqual(RangeRound(defaults: defaults).best, round.score)
        round.charge(1)
        XCTAssertFalse(round.release())
        round.restart()
        XCTAssertEqual(round.phase, .ready)
        XCTAssertEqual(round.score, 0)
        XCTAssertEqual(round.shotNumber, 1)
    }

    @MainActor
    func testBackgroundPauseAndCancelledChargeDoNotConsumeShots() {
        let round = RangeRound()
        let start = Date(timeIntervalSince1970: 100)
        round.charge(0.02)
        XCTAssertFalse(round.release())
        XCTAssertTrue(round.shots.isEmpty)
        round.charge(0.8)
        round.pause(at: start)
        XCTAssertEqual(round.phase, .ready)
        round.resume(at: start)
        round.charge(0.8)
        XCTAssertTrue(round.release(at: start))
        round.pause(at: start.addingTimeInterval(1))
        round.resume(at: start.addingTimeInterval(101))
        XCTAssertEqual(round.elapsed(at: start.addingTimeInterval(101)), 1)
    }
}

