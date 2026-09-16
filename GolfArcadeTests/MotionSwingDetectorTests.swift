import XCTest
import simd
@testable import GolfArcade

final class MotionSwingDetectorTests: XCTestCase {
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
        events.compactMap { if case .impact(let power, _, _) = $0 { power } else { nil } }
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
}
