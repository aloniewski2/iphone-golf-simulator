import XCTest
@testable import GolfArcade

final class RangeSimulationTests: XCTestCase {
    func testStandardBagReferenceDistancesAndMonotonicPower() {
        for club in GolfClub.allCases {
            let full = BallFlight.simulate(club.launch(power: 1, aimDegrees: 0, curveDegrees: 0))
            XCTAssertEqual(club == .putter ? full.total : full.carry, club.referenceDistanceYards, accuracy: 0.1)
            var previous = 0.0
            for power in stride(from: 0.1, through: 1.0, by: 0.1) {
                let flight = BallFlight.simulate(club.launch(power: power, aimDegrees: 0, curveDegrees: 0))
                XCTAssertGreaterThan(flight.total, previous)
                previous = flight.total
            }
        }
    }

    /// As in arcade golf, the meter reads distance: 60% on a 250-yard driver carries 150. The
    /// putter's meter is curved toward the short end, and the HUD's quoted distance is what flies.
    func testMeterReadsDistance() {
        for club in GolfClub.allCases {
            XCTAssertEqual(club.meterExponent, club == .putter ? 1.5 : 1)
            for power in stride(from: 0.1, through: 1.0, by: 0.1) {
                let flight = BallFlight.simulate(club.launch(power: power, aimDegrees: 0, curveDegrees: 0))
                let distance = club == .putter ? flight.total : flight.carry
                XCTAssertEqual(distance, club.distanceYards(meter: power), accuracy: club.referenceDistanceYards * 0.02, "\(club) at \(power)")
                if club != .putter { XCTAssertEqual(distance, club.referenceDistanceYards * power, accuracy: club.referenceDistanceYards * 0.02) }
            }
        }
        XCTAssertEqual(GolfClub.putter.distanceYards(meter: 0.25), 25 * 0.125, accuracy: 0.001, "a quarter stroke is a nine-foot putt")
        let thin = BallFlight.simulate(GolfClub.driver.launch(power: 1, aimDegrees: 0, curveDegrees: 0, speedFactor: 0.76))
        XCTAssertLessThan(thin.carry, 250 * 0.76, "a thin strike loses club speed, which costs more than its share of distance")
    }

    func testPowerAndClubChangeDistanceAndAimChangesLanding() {
        let soft = RangeShot(id: 1, club: .driver, power: 0.2, aim: -15)
        let hard = RangeShot(id: 2, club: .driver, power: 0.9, aim: 15)
        let wedge = RangeShot(id: 3, club: .wedge, power: 0.9, aim: 0)
        XCTAssertLessThan(soft.total, hard.total)
        XCTAssertLessThan(wedge.total, hard.total)
        XCTAssertLessThan(soft.landing.lateralYards, 0)
        XCTAssertGreaterThan(hard.landing.lateralYards, 0)
    }

    func testMeasuredContactChangesBallFlight() {
        let center = RangeShot(id: 1, club: .driver, power: 0.9, aim: 0, strike: .center)
        let thin = RangeShot(id: 2, club: .driver, power: 0.9, aim: 0, strike: .thin)
        let miss = RangeShot(id: 3, club: .driver, power: 0.9, aim: 0, strike: .miss)
        XCTAssertGreaterThan(center.total, thin.total)
        XCTAssertGreaterThan(thin.total, miss.total)
        XCTAssertEqual(miss.strike, .miss)
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
}

final class ClubRealismTests: XCTestCase {
    /// Each club flies like its real one at full power: launch-monitor shape, not just distance.
    func testClubsFlyLikeTheirRealCounterparts() {
        func shape(_ club: GolfClub) -> (carry: Double, roll: Double, apex: Double, clubSpeed: Double) {
            let flight = BallFlight.simulate(club.launch(power: 1, aimDegrees: 0, curveDegrees: 0))
            return (flight.carry, flight.roll, flight.apex, club.maxClubSpeedMPH)
        }
        let driver = shape(.driver), iron = shape(.iron), wedge = shape(.wedge)
        // Driver: a good amateur's 105 mph, 250 carry, a low-thirties apex and a running roll-out.
        XCTAssertEqual(driver.clubSpeed, 106, accuracy: 3)
        XCTAssertEqual(driver.carry, 250, accuracy: 1)
        XCTAssertEqual(driver.apex, 33, accuracy: 4)
        XCTAssertGreaterThan(driver.roll, 14)
        // 7-iron: high-80s club speed for 160, apex under 30, and it hops rather than runs.
        XCTAssertEqual(iron.clubSpeed, 88, accuracy: 4)
        XCTAssertEqual(iron.carry, 160, accuracy: 1)
        XCTAssertEqual(iron.apex, 28, accuracy: 4)
        XCTAssertLessThan(iron.roll, 11)
        XCTAssertGreaterThan(iron.roll, 3)
        // Sand wedge: steep and spinning, it checks up within a few yards.
        XCTAssertEqual(wedge.clubSpeed, 70, accuracy: 4)
        XCTAssertEqual(wedge.carry, 90, accuracy: 1)
        XCTAssertEqual(wedge.apex, 21, accuracy: 4)
        XCTAssertLessThan(wedge.roll, 5)
        XCTAssertLessThan(driver.roll * 0.5, driver.roll - iron.roll + 1, "the driver runs out far more than an iron")
    }

    func testFullSwingArcShortensWithTheClub() {
        var driver = ArmSwingDetector(), iron = ArmSwingDetector(), wedge = ArmSwingDetector()
        driver.configure(for: .driver)
        iron.configure(for: .iron)
        wedge.configure(for: .wedge)
        XCTAssertGreaterThan(driver.fullBackswing, iron.fullBackswing)
        XCTAssertGreaterThan(iron.fullBackswing, wedge.fullBackswing)
        XCTAssertEqual(wedge.power(arc: 100, downswingSpeed: 450), 1, accuracy: 1e-9, "a natural full wedge swing fills its meter")
        XCTAssertLessThan(driver.power(arc: 100, downswingSpeed: 550), 0.9, "the same arc is not a full driver swing")
    }
}
