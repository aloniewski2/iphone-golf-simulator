import XCTest
@testable import GolfArcade

final class ClubSignRecognizerTests: XCTestCase {
    private let shoulderY: CGFloat = 0.7

    /// A player facing the camera, right hand raised beside the head showing `fingers`.
    private func frame(fingers: Int?, hand: CGPoint = CGPoint(x: 0.66, y: 0.78), other: CGPoint = CGPoint(x: 0.42, y: 0.42), at time: Double) -> PoseFrame {
        let point = { (location: CGPoint) in PosePoint(location: location, confidence: 0.9) }
        return PoseFrame(timestamp: time, points: [
            .leftShoulder: point(CGPoint(x: 0.4, y: shoulderY)),
            .rightShoulder: point(CGPoint(x: 0.6, y: shoulderY)),
            .leftWrist: point(other),
            .rightWrist: point(hand)
        ], hands: fingers.map { [HandReading(wrist: .rightWrist, shape: $0 == 0 ? .fist : .unknown, fingers: $0)] } ?? [])
    }

    private func hold(_ recognizer: inout ClubSignRecognizer, fingers: Int?, from start: Double, for seconds: Double,
                      hand: CGPoint = CGPoint(x: 0.66, y: 0.78), other: CGPoint = CGPoint(x: 0.42, y: 0.42)) -> [GolfClub] {
        stride(from: start, to: start + seconds, by: 1.0 / 15).compactMap {
            recognizer.ingest(frame(fingers: fingers, hand: hand, other: other, at: $0), at: $0)
        }
    }

    func testFingersHeldUpPickTheClubOnceAfterAHold() {
        var recognizer = ClubSignRecognizer()
        XCTAssertEqual(hold(&recognizer, fingers: 2, from: 0, for: 1.5), [.iron], "two fingers, held: the iron, once")
        XCTAssertEqual(recognizer.showing, 2)
        // Change the sign without lowering the hand: a new pick.
        XCTAssertEqual(hold(&recognizer, fingers: 4, from: 1.5, for: 1.0), [.putter])
        // Drop the hand, raise it again with one finger: the driver.
        XCTAssertTrue(hold(&recognizer, fingers: nil, from: 2.5, for: 0.6).isEmpty)
        XCTAssertNil(recognizer.showing)
        XCTAssertEqual(hold(&recognizer, fingers: 1, from: 3.1, for: 0.8), [.driver])
        XCTAssertEqual(hold(&recognizer, fingers: 3, from: 3.9, for: 0.8), [.wedge])
    }

    func testBriefCountsAndGripsAndFistsPickNothing() {
        var recognizer = ClubSignRecognizer()
        // A hand opening on its way to a fist passes through counts too quickly to hold.
        XCTAssertTrue(hold(&recognizer, fingers: 3, from: 0, for: 0.2).isEmpty)
        XCTAssertTrue(hold(&recognizer, fingers: 0, from: 0.2, for: 1.0).isEmpty, "a fist is not a club")
        // Two fingers, but the hands are together on the grip.
        XCTAssertTrue(hold(&recognizer, fingers: 2, from: 1.2, for: 1.0, hand: CGPoint(x: 0.5, y: 0.42), other: CGPoint(x: 0.47, y: 0.41)).isEmpty)
        // Two fingers, but the hand hangs by the hip.
        XCTAssertTrue(hold(&recognizer, fingers: 2, from: 2.2, for: 1.0, hand: CGPoint(x: 0.66, y: 0.45)).isEmpty)
        // A blink in the reading does not restart the hold.
        var steady = ClubSignRecognizer()
        var picks = hold(&steady, fingers: 2, from: 0, for: 0.3)
        picks += hold(&steady, fingers: nil, from: 0.3, for: 0.1)
        picks += hold(&steady, fingers: 2, from: 0.4, for: 0.3)
        XCTAssertEqual(picks, [.iron], "held for half a second overall, with a blink")
    }
}
