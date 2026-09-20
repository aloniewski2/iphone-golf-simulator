import XCTest
import simd
@testable import GolfArcade

final class NativeMotionTests: XCTestCase {
    private func replay(hz: Double = 100, club: GolfClub = .driver, hand: Handedness = .right,
                        jitter: Bool = false, legs: [(Double, Double)]) -> (SwingRecognizer, [SwingEvent]) {
        var recognizer = SwingRecognizer()
        recognizer.arm(SwingConfiguration(club: club, handedness: hand, sensitivity: 1))
        var time = 0.0, angle = 0.0
        var events: [SwingEvent] = []
        for (duration, target) in legs {
            let steps = max(1, Int(duration * hz))
            let delta = (target - angle) / Double(steps)
            for i in 0..<steps {
                let dt = (1 / hz) * (jitter ? (i.isMultiple(of: 2) ? 0.8 : 1.2) : 1)
                time += dt; angle += delta
                let sign = hand == .right ? 1.0 : -1.0
                let sample = MotionSample(timestamp: time,
                    attitude: simd_quatd(angle: sign * angle, axis: SIMD3(1, 0, 0)),
                    rotationRate: SIMD3(sign * delta / dt, 0, 0), userAcceleration: .zero)
                if let event = recognizer.ingest(sample) { events.append(event) }
            }
        }
        return (recognizer, events)
    }
    private func hits(_ events: [SwingEvent]) -> [SwingMeasurement] {
        events.compactMap { if case .impact(let value) = $0 { value } else { nil } }
    }

    func test60And100HzJitterAndBothHandsProduceOneImpact() {
        for hz in [60.0, 100.0] {
            for hand in [Handedness.left, .right] {
                let (recognizer, events) = replay(hz: hz, hand: hand, jitter: true,
                    legs: [(0.6, 0), (0.8, 1.8), (2, 1.8), (0.3, -0.8), (4, -0.8), (0.8, 0), (0.5, 1.8), (0.3, -0.8)])
                XCTAssertEqual(hits(events).count, 1)
                XCTAssertEqual(recognizer.phase, .idle)
                guard let m = hits(events).first else { continue }
                XCTAssertGreaterThan(m.impactTimestamp, m.downswingTimestamp)
                XCTAssertGreaterThan(m.downswingTimestamp, m.backswingTimestamp)
                XCTAssertEqual(m.handedness, hand)
                XCTAssertLessThanOrEqual(abs(m.impact.startLineDegrees), 8)
                XCTAssertGreaterThan(m.peakAngularSpeed, 7)
            }
        }
    }

    func testSlowPuttAtBothRates() {
        for hz in [60.0, 100.0] {
            let (_, events) = replay(hz: hz, club: .putter,
                legs: [(0.6, 0), (0.8, 0.14), (0.4, 0.14), (0.8, -0.06), (4, -0.06)])
            XCTAssertEqual(hits(events).count, 1)
            XCTAssertLessThan(hits(events).first?.impact.power ?? 1, 0.15)
            XCTAssertLessThanOrEqual(abs(hits(events).first?.impact.startLineDegrees ?? 10), 2)
        }
    }

    func testDecelerationAwayFromAddressNeverManufacturesImpact() {
        let (_, events) = replay(legs: [(0.6, 0), (0.8, 1.8), (0.1, 1.2), (9, 1.2)])
        XCTAssertTrue(hits(events).isEmpty)
        XCTAssertTrue(events.contains { if case .cancelled(_, .timeout) = $0 { true } else { false } })
    }

    func testNoisyRestCannotCalibrateOrHit() {
        let (_, events) = replay(legs: (0..<40).map { (0.08, $0.isMultiple(of: 2) ? 0.12 : -0.12) })
        XCTAssertTrue(hits(events).isEmpty)
    }

    func testWeakWaggleDoesNotHit() {
        let (_, events) = replay(legs: [(0.6, 0), (2, 0.7), (3, 0), (1, 0)])
        XCTAssertTrue(hits(events).isEmpty)
    }

    func testInvalidTimestampsAndSampleGapsDisarm() {
        for time in [0.0, -0.1, Double.nan, 0.11] {
            var recognizer = SwingRecognizer()
            recognizer.arm(.init(club: .driver))
            _ = recognizer.ingest(.init(timestamp: 0, attitude: simd_quatd(ix: 0, iy: 0, iz: 0, r: 1), rotationRate: .zero, userAcceleration: .zero))
            let event = recognizer.ingest(.init(timestamp: time, attitude: simd_quatd(ix: 0, iy: 0, iz: 0, r: 1), rotationRate: .zero, userAcceleration: .zero))
            guard case .cancelled = event else { XCTFail("Expected cancellation for \(time)"); continue }
            XCTAssertNil(recognizer.configuration)
            XCTAssertEqual(recognizer.phase, .idle)
        }
    }

    func testInvalidQuaternionCancelsCalibration() {
        var recognizer = SwingRecognizer()
        recognizer.arm(.init(club: .driver))
        guard case .cancelled(_, .invalidSample) = recognizer.ingest(.init(timestamp: 0,
            attitude: simd_quatd(vector: .zero), rotationRate: .zero, userAcceleration: .zero)) else {
            return XCTFail("Invalid orientation must disarm")
        }
    }

    func testCancelledArmCannotHitAndCanRearmExplicitly() {
        var recognizer = SwingRecognizer()
        recognizer.arm(.init(club: .iron))
        guard case .cancelled(_, .explicit) = recognizer.cancel() else { return XCTFail("Missing cancellation") }
        XCTAssertNil(recognizer.ingest(.init(timestamp: 1, attitude: simd_quatd(ix: 0, iy: 0, iz: 0, r: 1), rotationRate: SIMD3(20, 0, 0), userAcceleration: .zero)))
        recognizer.arm(.init(club: .putter))
        XCTAssertEqual(recognizer.phase, .settling)
    }

    func testHandoffCoalescesAndInvalidatesOldImpacts() {
        let handoff = MotionHandoff(capacity: 2)
        let id = UUID(), generation = handoff.invalidate()
        XCTAssertTrue(handoff.publish(snapshot: nil, event: .cancelled(id, .timeout), generation: generation).notify)
        XCTAssertFalse(handoff.publish(snapshot: nil, event: .cancelled(id, .timeout), generation: generation).notify)
        let overflow = handoff.publish(snapshot: nil, event: .cancelled(id, .timeout), generation: generation)
        XCTAssertTrue(overflow.overflow)
        let batch = handoff.drain()
        XCTAssertEqual(batch.events.count, 1)
        guard case .cancelled(_, .overflow) = batch.events[0] else { return XCTFail("Overflow must be explicit") }
        _ = handoff.invalidate()
        XCTAssertFalse(handoff.publish(snapshot: nil, event: .cancelled(id, .timeout), generation: generation).notify)
        XCTAssertTrue(handoff.drain().events.isEmpty)
    }

    func testConfigurationFreezesAimAndCalibration() {
        let config = SwingConfiguration(club: .driver, handedness: .left, sensitivity: 2.5, selectedAimDegrees: 34)
        let measurement = SwingMeasurement(configuration: config, backswingTimestamp: 1, downswingTimestamp: 2,
            impactTimestamp: 3, peakAngularSpeed: 4, addressRelativeOrientation: simd_quatd(ix: 0, iy: 0, iz: 0, r: 1),
            estimatedSwingDirection: SIMD3(1, 0, 0.01), confidence: 0.9)
        XCTAssertEqual(measurement.configuration.selectedAimDegrees, 34)
        XCTAssertEqual(measurement.impact.startLineDegrees, 8)
        XCTAssertEqual(measurement.impact.power, 4 * 2.5 / 16)
    }

    func testInterruptionDuringFollowThroughReturnsToIdleWithoutSecondImpact() {
        var (recognizer, events) = replay(legs: [(0.6, 0), (0.8, 1.8), (0.3, -0.8)])
        XCTAssertEqual(hits(events).count, 1)
        let sample = MotionSample(timestamp: recognizer.snapshot.timestamp + 0.2,
            attitude: simd_quatd(angle: -0.8, axis: SIMD3(1, 0, 0)), rotationRate: .zero, userAcceleration: .zero)
        guard case .cancelled(_, .sampleGap) = recognizer.ingest(sample) else { return XCTFail("Interruption must clear the finish state") }
        XCTAssertEqual(recognizer.phase, .idle)
        XCTAssertNil(recognizer.configuration)
    }
}
