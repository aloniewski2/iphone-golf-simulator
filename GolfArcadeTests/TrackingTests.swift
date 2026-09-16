import XCTest
@testable import GolfArcade

final class TrackingTests: XCTestCase {
    func testOneEuroFilterHoldsStillAndFollowsMotion() {
        var filter = OneEuroFilter()
        var seed: UInt32 = 7
        func noise() -> Double { seed = 1664525 &* seed &+ 1013904223; return Double(seed) / Double(UInt32.max) * 0.02 - 0.01 }
        var outputs: [Double] = []
        for i in 0..<120 { outputs.append(filter.filter(0.5 + noise(), at: Double(i) / 30)) } // jittery but still
        let settled = outputs.suffix(60)
        let spread = settled.max()! - settled.min()!
        XCTAssertLessThan(spread, 0.008, "±1 % jitter at rest is squashed")
        // Now a fast move: 0.5 → 0.9 over 8 frames, as in a downswing.
        var time = 4.0
        var last = 0.0
        for step in 1...8 {
            time += 1.0 / 30
            last = filter.filter(0.5 + 0.4 * Double(step) / 8, at: time)
        }
        XCTAssertGreaterThan(last, 0.82, "fast motion passes through with little lag")
    }

    func testRegionOfInterestMapping() {
        let roi = CGRect(x: 0.2, y: 0.1, width: 0.5, height: 0.8)
        let mapped = CameraPoseTracker.framePoint(CGPoint(x: 0.5, y: 0.5), in: roi)
        XCTAssertEqual(mapped.x, 0.45, accuracy: 1e-9)
        XCTAssertEqual(mapped.y, 0.5, accuracy: 1e-9)
        XCTAssertEqual(CameraPoseTracker.framePoint(.zero, in: roi), CGPoint(x: 0.2, y: 0.1))
    }

    func testFocusBoxWrapsThePlayerWithHeadroomAndNeverCropsTooTight() {
        let high: Float = 0.9
        func p(_ x: CGFloat, _ y: CGFloat) -> PosePoint { PosePoint(location: CGPoint(x: x, y: y), confidence: high) }
        let player = PoseFrame(timestamp: 0, points: [
            .leftShoulder: p(0.45, 0.62), .rightShoulder: p(0.55, 0.62), .leftHip: p(0.46, 0.5), .rightHip: p(0.54, 0.5),
            .leftWrist: p(0.5, 0.42), .rightWrist: p(0.5, 0.42), .nose: p(0.5, 0.7)
        ])
        let box = try! XCTUnwrap(CameraPoseTracker.focusBox(around: player))
        XCTAssertGreaterThanOrEqual(box.width, 0.4)
        XCTAssertGreaterThanOrEqual(box.height, 0.5)
        XCTAssertGreaterThan(box.maxY - 0.7, 0.12, "headroom above the nose for the hands at the top")
        XCTAssertTrue(CGRect(x: 0, y: 0, width: 1, height: 1).contains(box))
        XCTAssertTrue(box.contains(CGPoint(x: 0.5, y: 0.42)) && box.contains(CGPoint(x: 0.5, y: 0.7)))
        let edge = PoseFrame(timestamp: 0, points: [
            .leftShoulder: p(0.02, 0.95), .rightShoulder: p(0.12, 0.95), .leftHip: p(0.03, 0.8), .rightHip: p(0.11, 0.8), .nose: p(0.07, 0.99)
        ])
        let clamped = try! XCTUnwrap(CameraPoseTracker.focusBox(around: edge))
        XCTAssertTrue(CGRect(x: 0, y: 0, width: 1, height: 1).contains(clamped), "clamped to the frame at the edges")
        XCTAssertNil(CameraPoseTracker.focusBox(around: PoseFrame(timestamp: 0, points: [.nose: p(0.5, 0.5)])), "one joint is not a player")
    }
}
