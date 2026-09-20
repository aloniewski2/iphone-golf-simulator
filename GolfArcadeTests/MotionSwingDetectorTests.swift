import XCTest
import simd
@testable import GolfArcade

final class MotionSwingDetectorTests: XCTestCase {
    func testCameraPreferenceMigratesOnceAndTouchChoiceSurvives() throws {
        let suite = "ControllerMigration.\(UUID().uuidString)"
        let defaults = try XCTUnwrap(UserDefaults(suiteName: suite))
        defer { defaults.removePersistentDomain(forName: suite) }
        defaults.set("camera", forKey: "range.swingInput")
        defaults.set(true, forKey: "gestures.enabled")
        ControllerPreferences.migrate(defaults: defaults)
        XCTAssertEqual(defaults.string(forKey: "range.swingInput"), "phone")
        XCTAssertFalse(defaults.bool(forKey: "gestures.enabled"))
        defaults.set("touch", forKey: "range.swingInput")
        ControllerPreferences.migrate(defaults: defaults)
        XCTAssertEqual(defaults.string(forKey: "range.swingInput"), "touch")
        XCTAssertEqual(SwingInput.allCases, [.phone, .touch])
    }

    func testPhoneGateRejectsEveryEventUntilArmed() {
        var gate = PhoneSwingGate()
        XCTAssertNil(gate.accept(.load(0.8), time: 0))
        XCTAssertNil(gate.accept(.impact(SwingImpact(power: 1, source: .phone)), time: 1))
        XCTAssertNil(gate.accept(.cancel, time: 2))
    }

    func testFollowThroughAfterImpactCannotProduceAnotherShot() {
        var gate = PhoneSwingGate()
        let impact = SwingInputEvent.impact(SwingImpact(power: 0.6, source: .phone))
        gate.arm()
        XCTAssertEqual(gate.accept(.load(0.5), time: 0), .load(0.5))
        XCTAssertEqual(gate.accept(impact, time: 1), impact)
        XCTAssertFalse(gate.isArmed)
        XCTAssertNil(gate.accept(impact, time: 2))
        gate.arm()
        XCTAssertEqual(gate.accept(impact, time: 3), impact)
    }

    func testCancelAndInterruptionsDisarmWithoutAStroke() {
        for _ in ["cancel button", "club", "pause", "background", "disconnect", "turn"] {
            var gate = PhoneSwingGate()
            gate.arm()
            _ = gate.accept(.load(0.8), time: 0)
            gate.disarm()
            XCTAssertNil(gate.accept(.impact(SwingImpact(power: 1)), time: 1))
        }
    }

    func testReadyStaysArmedWhilePlayerGetsComfortable() {
        var gate = PhoneSwingGate()
        gate.arm()
        XCTAssertNil(gate.accept(nil, time: 100))
        XCTAssertNil(gate.accept(nil, time: 130))
        XCTAssertTrue(gate.isArmed)
        XCTAssertEqual(gate.accept(.load(1), time: 131), .load(1))
    }

    func testAbortedBackswingConsumesTheArm() {
        var gate = PhoneSwingGate()
        gate.arm()
        XCTAssertEqual(gate.accept(.cancel, time: 1), .cancel)
        XCTAssertNil(gate.accept(.impact(SwingImpact(power: 1)), time: 2))
    }

    /// Rotates a phone about one axis through a list of (duration, target angle) legs at 100 Hz.
    private func drive(_ detector: inout MotionSwingDetector, legs: [(seconds: Double, angle: Double)]) -> [MotionSwingDetector.Event] {
        var events: [MotionSwingDetector.Event] = []
        var time = 0.0
        var angle = 0.0
        for leg in legs {
            let steps = max(1, Int(leg.seconds * 100))
            let delta = (leg.angle - angle) / Double(steps)
            for _ in 0..<steps {
                time += 0.01
                angle += delta
                let attitude = simd_quatd(angle: angle, axis: simd_double3(1, 0, 0))
                let rate = simd_double3(delta / 0.01, 0, 0)
                if let event = detector.ingest(time: time, attitude: attitude, rotationRate: rate) { events.append(event) }
            }
        }
        return events
    }

    private func impacts(_ events: [MotionSwingDetector.Event]) -> [Double] {
        events.compactMap { if case .impact(let impact) = $0 { impact.power } else { nil } }
    }

    func testFullSwingLoadsThenImpactsWithSpeedBasedPower() {
        var detector = MotionSwingDetector()
        detector.fullSpeed = 14
        let events = drive(&detector, legs: [
            (0.6, 0),        // hold still at address
            (0.8, 1.8),      // backswing
            (0.2, 1.8),      // pause at the top
            (0.25, -0.4),    // downswing through address: 8.8 rad/s
            (0.6, -0.4)      // hold the finish
        ])
        let loads = events.compactMap { if case .load(let value) = $0 { value } else { nil } }
        XCTAssertGreaterThan(loads.count, 10)
        XCTAssertEqual(loads.max() ?? 0, 0.9, accuracy: 0.05)
        XCTAssertEqual(impacts(events).count, 1)
        XCTAssertEqual(impacts(events).first ?? 0, 8.8 / 14, accuracy: 0.05)
        XCTAssertFalse(events.contains(.cancel))
        XCTAssertEqual(detector.phase, .address, "the finish position becomes the new address")
    }

    func testFasterSwingHitsHarderAndClampsAtFullPower() {
        var slow = MotionSwingDetector()
        var fast = MotionSwingDetector()
        let slowPower = impacts(drive(&slow, legs: [(0.6, 0), (0.8, 1.8), (0.2, 1.8), (0.5, -0.4), (0.6, -0.4)])).first ?? 0
        let fastPower = impacts(drive(&fast, legs: [(0.6, 0), (0.8, 1.8), (0.2, 1.8), (0.1, -0.4), (0.6, -0.4)])).first ?? 0
        XCTAssertLessThan(slowPower, fastPower)
        XCTAssertEqual(fastPower, 1, "22 rad/s exceeds full speed")
    }

    func testSlowWaggleCancelsWithoutImpact() {
        var detector = MotionSwingDetector()
        let events = drive(&detector, legs: [(0.6, 0), (1.0, 0.7), (1.0, 0.05), (0.6, 0.05)])
        XCTAssertTrue(impacts(events).isEmpty)
        XCTAssertEqual(events.last, .cancel)
        XCTAssertEqual(detector.phase, .address)
    }

    func testNothingHappensUntilThePhoneSettles() {
        var detector = MotionSwingDetector()
        let events = drive(&detector, legs: [(0.5, 2.0), (0.5, 0), (0.5, 2.0)])
        XCTAssertTrue(events.isEmpty)
        XCTAssertEqual(detector.phase, .settling)
    }

    func testAngleBetweenOrientations() {
        let a = simd_quatd(angle: 0.3, axis: simd_double3(0, 1, 0))
        let b = simd_quatd(angle: 1.1, axis: simd_double3(0, 1, 0))
        XCTAssertEqual(MotionSwingDetector.angle(from: a, to: b), 0.8, accuracy: 1e-9)
        XCTAssertEqual(MotionSwingDetector.angle(from: a, to: a), 0, accuracy: 1e-9)
    }

    func testGentlePhonePuttUsesClubSpecificThresholds() {
        var detector = MotionSwingDetector()
        detector.configure(for: .putter)
        let events = drive(&detector, legs: [(0.6, 0), (0.8, 0.14), (0.15, 0.14), (0.8, -0.04)])
        XCTAssertEqual(impacts(events).count, 1)
        XCTAssertLessThan(impacts(events).first ?? 1, 0.15)
    }

    func testHeldBackswingTimesOut() {
        var detector = MotionSwingDetector()
        let events = drive(&detector, legs: [(0.6, 0), (0.8, 1.8), (8.5, 1.8)])
        XCTAssertTrue(impacts(events).isEmpty)
        XCTAssertTrue(events.contains(.cancel))
    }

    func testDeliberatePauseAtTopStillHitsOnFollowThrough() {
        var detector = MotionSwingDetector()
        let events = drive(&detector, legs: [(0.6, 0), (1.2, 1.8), (4, 1.8), (0.3, -1.2), (1, -1.2)])
        XCTAssertEqual(impacts(events).count, 1)
        XCTAssertFalse(events.contains(.cancel))
    }

    #if DEBUG && targetEnvironment(simulator)
    @MainActor
    func testSingleReadyTapDrivesRealRoundThroughImpactAndFinish() {
        for club in [GolfClub.driver, .iron, .putter] {
            for direction in [-1.0, 1.0] {
                let controller = PhoneSwingController()
                controller.usesTestMotion = true
                controller.setClub(club)
                controller.start()
                let round = CourseRound()
                round.start(course: .easy, playerCount: 1)
                round.club = club
                var hitCount = 0
                var sawFollowThrough = false
                controller.onEvent = { event in
                    switch event {
                    case .load(let value): round.charge(value)
                    case .cancel: round.cancelCharge()
                    case .impact(let impact):
                        hitCount += 1
                        XCTAssertEqual(controller.status, .followThrough)
                        round.charge(impact.power)
                        XCTAssertTrue(round.release(execution: impact))
                    }
                }
                controller.arm() // One tap. No touch-down or touch-up calls during the swing.
                var time = 0.0
                var angle = 0.0
                let top = club == .putter ? 0.14 : 1.8
                let finish = club == .putter ? -0.08 : -1.5
                for leg in [(12.0, 0.0), (0.8, top), (0.4, top), (club == .putter ? 0.7 : 0.3, finish), (1.5, finish), (0.8, 0.0), (0.6, 0.0)] {
                    let steps = Int(leg.0 * 100)
                    let delta = (leg.1 * direction - angle) / Double(steps)
                    for _ in 0..<steps {
                        time += 0.01; angle += delta
                        controller.ingest(time: time, attitude: simd_quatd(angle: angle, axis: simd_double3(1, 0, 0)), rotationRate: simd_double3(delta / 0.01, 0, 0))
                        sawFollowThrough = sawFollowThrough || controller.status == .followThrough
                    }
                }
                XCTAssertEqual(hitCount, 1, "\(club) direction \(direction)")
                XCTAssertNotNil(round.activeShot)
                XCTAssertEqual(round.phase, .flying)
                XCTAssertTrue(sawFollowThrough)
                XCTAssertFalse(controller.isArmed)
                XCTAssertEqual(controller.status, .idle)
                controller.stop()
            }
        }
    }

    @MainActor
    func testReadyCancellationStopsSamplesWithoutAHit() {
        let controller = PhoneSwingController()
        controller.usesTestMotion = true
        controller.start()
        var hits = 0
        controller.onEvent = { if case .impact = $0 { hits += 1 } }
        controller.arm()
        controller.disarm()
        for frame in 0..<200 {
            controller.ingest(time: Double(frame) / 100, attitude: simd_quatd(angle: Double(frame) / 10, axis: simd_double3(1, 0, 0)), rotationRate: simd_double3(10, 0, 0))
        }
        XCTAssertEqual(hits, 0)
        XCTAssertFalse(controller.isArmed)
        controller.stop()
    }
    #endif
}
