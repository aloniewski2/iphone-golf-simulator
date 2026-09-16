import SceneKit
import XCTest
@testable import GolfArcade

final class ArmSwingDetectorTests: XCTestCase {
    /// Rotates the hands around the shoulders through (seconds, arc degrees) legs at 30 fps.
    /// 0° is hanging straight down; positive swings toward the golfer's trail side.
    private func drive(_ detector: inout ArmSwingDetector, legs: [(seconds: Double, arc: Double)], shoulderWidth: CGFloat = 0.2, dropAt: Set<Int> = []) -> [SwingInputEvent] {
        var events: [SwingInputEvent] = []
        var time = 0.0
        var arc = 0.0
        var index = 0
        for leg in legs {
            let steps = max(1, Int(leg.seconds * 30))
            let delta = (leg.arc - arc) / Double(steps)
            for _ in 0..<steps {
                time += 1.0 / 30
                arc += delta
                index += 1
                let radians = arc * .pi / 180
                let reach = shoulderWidth * 1.6
                let hands = CGPoint(x: 0.5 + sin(radians) * reach, y: 0.7 - cos(radians) * reach)
                let sample = dropAt.contains(index) ? nil : ArmSwingDetector.Sample(time: time, shoulderCenter: CGPoint(x: 0.5, y: 0.7), shoulderWidth: shoulderWidth, hands: hands)
                if let event = detector.ingest(sample, at: time) { events.append(event) }
            }
        }
        return events
    }

    private func impacts(_ events: [SwingInputEvent]) -> [(power: Double, aim: Double)] {
        events.compactMap { if case .impact(let power, let aim) = $0 { (power, aim) } else { nil } }
    }

    private let fullSwing: [(seconds: Double, arc: Double)] = [(0.5, 0), (0.8, 140), (0.15, 140), (0.2, -30), (0.3, 120), (0.5, 120), (0.4, 0), (0.5, 0)]

    func testFullBackswingIsFullPowerAndLoadsAsTheHandsGoBack() {
        var detector = ArmSwingDetector()
        let events = drive(&detector, legs: fullSwing)
        let loads = events.compactMap { if case .load(let value) = $0 { value } else { nil } }
        XCTAssertGreaterThan(loads.count, 10)
        XCTAssertTrue(zip(loads, loads.dropFirst()).allSatisfy { $1 >= $0 - 1e-9 }, "the meter only fills")
        XCTAssertEqual(loads.max() ?? 0, 1, accuracy: 0.02)
        XCTAssertEqual(impacts(events).count, 1)
        XCTAssertEqual(impacts(events).first?.power ?? 0, 1, accuracy: 0.02)
        XCTAssertEqual(impacts(events).first?.aim ?? 9, 0, accuracy: 0.5, "a swing within the meter flies straight")
        XCTAssertEqual(detector.phase, .address, "re-armed once the hands settle back down")
    }

    func testHalfSwingIsHalfPowerAndSizeDoesNotMatter() {
        var near = ArmSwingDetector()
        var far = ArmSwingDetector()
        let half: [(seconds: Double, arc: Double)] = [(0.5, 0), (0.6, 70), (0.15, 70), (0.225, -20), (0.3, 60), (0.5, 60), (0.4, 0), (0.5, 0)]
        let nearPower = impacts(drive(&near, legs: half, shoulderWidth: 0.3)).first?.power ?? 0
        let farPower = impacts(drive(&far, legs: half, shoulderWidth: 0.08)).first?.power ?? 0
        XCTAssertEqual(nearPower, 0.5, accuracy: 0.03, "half the arc at half speed is half power")
        XCTAssertEqual(farPower, nearPower, accuracy: 1e-6, "arc angles are scale-free")
    }

    func testOverswingHooksForARightHander() {
        var detector = ArmSwingDetector()
        let events = drive(&detector, legs: [(0.5, 0), (0.9, 178), (0.15, 178), (0.3, -30), (0.3, 120), (0.5, 120), (0.4, 0), (0.5, 0)])
        let shot = impacts(events).first
        XCTAssertGreaterThan(shot?.power ?? 0, 0.9)
        XCTAssertLessThan(shot?.aim ?? 0, -3, "past the meter the ball hooks left")
    }

    func testFasterDownswingHitsHarderFromTheSameBackswing() {
        var lazy = ArmSwingDetector()
        var quick = ArmSwingDetector()
        let lazyPower = impacts(drive(&lazy, legs: [(0.5, 0), (0.8, 110), (0.15, 110), (0.6, -20), (0.3, 60), (0.5, 60), (0.4, 0), (0.5, 0)])).first?.power ?? 0
        let quickPower = impacts(drive(&quick, legs: [(0.5, 0), (0.8, 110), (0.15, 110), (0.15, -20), (0.3, 60), (0.5, 60), (0.4, 0), (0.5, 0)])).first?.power ?? 0
        XCTAssertGreaterThan(quickPower, lazyPower + 0.15)
    }

    func testSlowPracticeSwingDoesNotSpendAShot() {
        var detector = ArmSwingDetector()
        let events = drive(&detector, legs: [(0.5, 0), (0.8, 120), (0.2, 120), (2.0, 0), (0.5, 0)])
        XCTAssertTrue(impacts(events).isEmpty)
        XCTAssertTrue(events.contains(.cancel))
    }

    func testBriefTrackingLossAtTheTopIsForgiven() {
        var detector = ArmSwingDetector()
        let events = drive(&detector, legs: fullSwing, dropAt: Set(36...42)) // 7 frames (~0.23 s) near the top
        XCTAssertEqual(impacts(events).count, 1)
        XCTAssertFalse(events.contains(.cancel))
    }

    func testLosingThePlayerForLongerCancels() {
        var detector = ArmSwingDetector()
        let events = drive(&detector, legs: [(0.5, 0), (0.8, 120), (1.0, 120)], dropAt: Set(40...80))
        XCTAssertEqual(events.last, .cancel)
        XCTAssertEqual(detector.phase, .findingPlayer)
    }

    func testMeasuredCameraSwingChangesBallFlight() throws {
        var shortDetector = ArmSwingDetector()
        var fullDetector = ArmSwingDetector()
        var hookDetector = ArmSwingDetector()
        let short = try XCTUnwrap(impacts(drive(&shortDetector, legs: [
            (0.5, 0), (0.6, 70), (0.15, 70), (0.225, -20)
        ])).first)
        let full = try XCTUnwrap(impacts(drive(&fullDetector, legs: fullSwing)).first)
        let hook = try XCTUnwrap(impacts(drive(&hookDetector, legs: [
            (0.5, 0), (0.9, 178), (0.15, 178), (0.3, -30)
        ])).first)

        let shortShot = RangeShot(id: 1, club: .driver, power: short.power, aim: 0, curve: short.aim)
        let fullShot = RangeShot(id: 2, club: .driver, power: full.power, aim: 0, curve: full.aim)
        let hookShot = RangeShot(id: 3, club: .driver, power: hook.power, aim: 0, curve: hook.aim)

        XCTAssertGreaterThan(fullShot.total, shortShot.total)
        XCTAssertEqual(fullShot.landing.lateralYards, 0, accuracy: 0.1)
        XCTAssertLessThan(hookShot.landing.lateralYards, -1)
    }
}

final class GolferAvatarTests: XCTestCase {
    @MainActor
    func testArmsKeepTheirLengthAndSwingCleanlyThroughTheArc() {
        let golfer = Golfer()
        let arms = golfer.node.childNodes.flatMap { $0.childNodes }.filter { ($0.geometry as? SCNCylinder)?.height == 1 && ($0.geometry as? SCNCylinder)?.radius == 0.16 }
        XCTAssertEqual(arms.count, 4)
        let club = golfer.node.childNodes.flatMap { $0.childNodes }.first { $0.childNodes.count == 2 }!
        let addressClubY = club.position.y
        for _ in 0..<40 { golfer.follow(150) } // eases toward the top of the backswing
        XCTAssertGreaterThan(club.position.y, addressClubY + 2, "hands rise to the top")
        XCTAssertTrue(arms.allSatisfy { $0.scale.y <= 1.36 }, "arm segments never stretch past their length")
        XCTAssertTrue(arms.allSatisfy { $0.scale.y >= 0.6 }, "and never collapse")
        let top = club.position
        golfer.launch(replay: false)
        golfer.animate(elapsed: 0.15)
        golfer.animate(elapsed: 0.5)
        XCTAssertLessThan(club.position.z, top.z - 1, "the finish is through on the other side")
        golfer.animate(elapsed: 5)
        XCTAssertEqual(club.position.y, addressClubY, accuracy: 0.05, "back to address for the next ball")
        XCTAssertTrue(arms.allSatisfy { $0.scale.y <= 1.36 })
    }
}
