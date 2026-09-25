import XCTest
@testable import GolfArcade

/// The TV delay probe, on synthetic camera readings: 60 fps frames of the TV's brightness
/// through a black-then-white flash that appears `delay` after the game drew it.
final class SportsDelayProbeTests: XCTestCase {
    /// Brightness the camera reads: the game picture (mid), black for 0.35 s, white for
    /// 0.25 s, then the game again, all shifted by the TV's delay. Frames at 60 fps with a
    /// phase offset so crossings fall between frames.
    private func series(rendered: Double, delay: Double, dark: Double = 20, bright: Double = 210,
                        game: Double = 120, noise: Double = 0, phase: Double = 0.004) -> [(Double, Double)] {
        var out = [(Double, Double)]()
        var generator = SystemRandomNumberGenerator()
        var t = rendered - 0.8 + phase
        while t < rendered + 0.9 {
            let shown = t - delay        // what the game was drawing when this light left the TV
            var level = game
            if shown >= rendered - 0.35 && shown < rendered { level = dark }
            if shown >= rendered && shown < rendered + 0.25 { level = bright }
            let jitter = noise > 0 ? Double.random(in: -noise...noise, using: &generator) : 0
            out.append((t, level + jitter))
            t += 1.0 / 60
        }
        return out
    }

    func testRecoversTheDelayWithinAFrame() throws {
        for delay in [0.05, 0.12, 0.18, 0.26, 0.4] {
            let measured = try XCTUnwrap(SportsDelayProbe.delay(series(rendered: 100, delay: delay), rendered: 100), "delay \(delay)")
            XCTAssertEqual(measured, delay, accuracy: 1.0 / 60, "delay \(delay)")
        }
    }

    func testSurvivesSensorNoise() throws {
        let measured = try XCTUnwrap(SportsDelayProbe.delay(series(rendered: 50, delay: 0.16, noise: 6), rendered: 50))
        XCTAssertEqual(measured, 0.16, accuracy: 0.03)
    }

    func testRejectsAFlashTheCameraNeverSaw() {
        // Camera pointed away: brightness barely moves.
        let flat = series(rendered: 10, delay: 0.15, dark: 118, bright: 124)
        XCTAssertNil(SportsDelayProbe.delay(flat, rendered: 10))
        XCTAssertNil(SportsDelayProbe.delay([], rendered: 10))
    }

    func testMedianNeedsTwoReadings() {
        XCTAssertNil(SportsDelayProbe.combine([0.15]))
        XCTAssertEqual(SportsDelayProbe.combine([0.15, 0.30, 0.16, 0.14]) ?? 0, 0.16, accuracy: 1e-9)
    }
}
