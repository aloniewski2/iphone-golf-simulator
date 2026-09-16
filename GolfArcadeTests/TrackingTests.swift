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
}
