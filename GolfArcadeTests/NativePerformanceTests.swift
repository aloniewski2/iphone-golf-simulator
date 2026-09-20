import XCTest
import simd
@testable import GolfArcade

final class NativePerformanceTests: XCTestCase {
    func testEmptyMeasurementsAreUnavailableNotZero() throws {
        let summary = NativeMetricSamples().summary
        XCTAssertEqual(summary.count, 0)
        XCTAssertNil(summary.p95MS)
        XCTAssertNil(summary.meanMS)
        XCTAssertNil(summary.maxMS)
        let data = try JSONEncoder().encode(summary)
        let object = try XCTUnwrap(JSONSerialization.jsonObject(with: data) as? [String: Any])
        XCTAssertNil(object["p95MS"])
    }

    func testNearestRankPercentileAndExactTotals() {
        var values = NativeMetricSamples()
        for value in 1...100 { values.record(Double(value)) }
        let summary = values.summary
        XCTAssertEqual(summary.count, 100)
        XCTAssertEqual(summary.percentileSampleCount, 100)
        XCTAssertEqual(summary.meanMS, 50.5)
        XCTAssertEqual(summary.p95MS, 95)
        XCTAssertEqual(summary.maxMS, 100)
        XCTAssertEqual(summary.countOver16_67MS, 84)
    }

    func testBoundedWindowRetainsRecentSamplesWithoutLosingTotalCount() {
        var values = NativeMetricSamples(capacity: 4)
        for value in 1...10 { values.record(Double(value)) }
        values.record(.nan); values.record(.infinity); values.record(-1)
        XCTAssertEqual(values.values.sorted(), [7, 8, 9, 10])
        XCTAssertEqual(values.summary.count, 10)
        XCTAssertEqual(values.summary.percentileSampleCount, 4)
        XCTAssertEqual(values.summary.meanMS, 5.5)
        XCTAssertEqual(values.summary.p95MS, 10)
        XCTAssertEqual(values.summary.maxMS, 10)
    }

    func testPauseDoesNotBecomeAFrameHitch() {
        let audit = NativePerformanceAudit(enabled: true, windowDuration: .infinity)
        audit.beginFrame(now: 100, running: true, context: "phone/ready")
        audit.endFrame(now: 100.002)
        audit.beginFrame(now: 100.016, running: true, context: "phone/ready")
        audit.endFrame(now: 100.019)
        audit.beginFrame(now: 101, running: false, context: "paused")
        audit.endFrame(now: 101.001)
        audit.beginFrame(now: 120, running: true, context: "phone/ready")
        audit.endFrame(now: 120.004)
        let frame = audit.metricsForTesting[.gameUpdateInterval]
        XCTAssertEqual(frame?.count, 1)
        XCTAssertEqual(frame?.p95MS ?? -1, 16, accuracy: 0.001)
        let systems = audit.metricsForTesting[.gameSystemsElapsed]
        XCTAssertEqual(systems?.count, 3)
        XCTAssertEqual(systems?.maxMS ?? -1, 4, accuracy: 0.001)
    }

    func testSensorTimelineResetsAndDisabledAuditDoesNothing() {
        let audit = NativePerformanceAudit(enabled: true, windowDuration: .infinity)
        let sample: (Double) -> MotionSample = { time in
            MotionSample(timestamp: time, attitude: simd_quatd(angle: 0, axis: SIMD3(1, 0, 0)),
                rotationRate: .zero, userAcceleration: .zero, callbackTimestamp: time + 0.003)
        }
        audit.sensorReceived(sample(100)); audit.sensorReceived(sample(100.01))
        audit.resetSensorTimeline()
        audit.sensorReceived(sample(120))
        XCTAssertEqual(audit.metricsForTesting[.sensorInterval]?.count, 1)
        XCTAssertEqual(audit.metricsForTesting[.sensorInterval]?.meanMS ?? -1, 10, accuracy: 0.001)
        XCTAssertEqual(audit.metricsForTesting[.sensorDelivery]?.count, 3)
        XCTAssertEqual(audit.metricsForTesting[.sensorDelivery]?.p95MS ?? -1, 3, accuracy: 0.001)
        let disabled = NativePerformanceAudit(enabled: false)
        disabled.sensorReceived(sample(1))
        disabled.record(.trajectoryPreparation, milliseconds: 2)
        disabled.beginFrame(now: 1, running: true, context: "phone")
        disabled.endFrame(now: 2)
        XCTAssertTrue(disabled.metricsForTesting.isEmpty)
    }

    func testConcurrentCountersRemainBoundedAndExact() {
        let audit = NativePerformanceAudit(enabled: true, windowDuration: .infinity)
        DispatchQueue.concurrentPerform(iterations: 8) { _ in
            for _ in 0..<1_000 { audit.record(.filterAndRecognize, milliseconds: 2) }
        }
        let summary = audit.metricsForTesting[.filterAndRecognize]
        XCTAssertEqual(summary?.count, 8_000)
        XCTAssertEqual(summary?.percentileSampleCount, 4_096)
        XCTAssertEqual(summary?.meanMS, 2)
        XCTAssertEqual(summary?.p95MS, 2)
    }
}
