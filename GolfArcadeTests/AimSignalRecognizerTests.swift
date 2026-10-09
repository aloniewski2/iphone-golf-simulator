import XCTest
@testable import GolfArcade

final class AimSignalRecognizerTests: XCTestCase {
    private let shoulderY: CGFloat = 0.7
    private let width: CGFloat = 0.2

    /// A player facing the camera: shoulders at x 0.4 and 0.6, hands wherever the test puts them.
    private func frame(left: CGPoint, right: CGPoint, at time: Double) -> PoseFrame {
        let point = { (location: CGPoint) in PosePoint(location: location, confidence: 0.9) }
        return PoseFrame(timestamp: time, points: [
            .leftShoulder: point(CGPoint(x: 0.4, y: shoulderY)),
            .rightShoulder: point(CGPoint(x: 0.6, y: shoulderY)),
            .leftWrist: point(left),
            .rightWrist: point(right)
        ])
    }

    private func run(_ recognizer: inout AimSignalRecognizer, left: CGPoint, right: CGPoint, from start: Double, for seconds: Double) -> [AimSignalRecognizer.Side] {
        stride(from: start, to: start + seconds, by: 1.0 / 30).compactMap { recognizer.ingest(frame(left: left, right: right, at: $0), at: $0) }
    }

    private let gripLeft = CGPoint(x: 0.47, y: 0.42), gripRight = CGPoint(x: 0.5, y: 0.41)

    func testArmOutToTheSideStepsThatWayAndKeepsSteppingWhileHeld() {
        var recognizer = AimSignalRecognizer()
        // Left arm straight out at shoulder height (1.3 widths from the centre), right hand hanging.
        let steps = run(&recognizer, left: CGPoint(x: 0.24, y: 0.69), right: CGPoint(x: 0.58, y: 0.5), from: 0, for: 1.6)
        XCTAssertEqual(steps, [.left, .left, .left], "one step after the hold, then one every half second")
        XCTAssertTrue(recognizer.isSignalling)
        // Arm comes back down to the grip: nothing more, and the hold starts over next time.
        XCTAssertTrue(run(&recognizer, left: gripLeft, right: gripRight, from: 1.6, for: 0.5).isEmpty)
        XCTAssertFalse(recognizer.isSignalling)
        XCTAssertEqual(run(&recognizer, left: CGPoint(x: 0.42, y: 0.5), right: CGPoint(x: 0.78, y: 0.72), from: 2.1, for: 0.4), [.right])
    }

    func testGripSwingAndHalfRaisedArmsAreNotSignals() {
        var recognizer = AimSignalRecognizer()
        // Address, backswing (hands together, sweeping up and to the side) and follow-through.
        XCTAssertTrue(run(&recognizer, left: gripLeft, right: gripRight, from: 0, for: 0.5).isEmpty)
        XCTAssertTrue(run(&recognizer, left: CGPoint(x: 0.66, y: 0.62), right: CGPoint(x: 0.7, y: 0.64), from: 0.5, for: 0.6).isEmpty)
        XCTAssertTrue(run(&recognizer, left: CGPoint(x: 0.3, y: 0.75), right: CGPoint(x: 0.27, y: 0.78), from: 1.1, for: 0.6).isEmpty, "both hands out together is a swing")
        // One arm out but well below the shoulders is reaching for something, not signalling.
        XCTAssertTrue(run(&recognizer, left: CGPoint(x: 0.24, y: 0.55), right: CGPoint(x: 0.58, y: 0.5), from: 1.7, for: 0.6).isEmpty)
        // Arm out, but the other hand is also up: not the pose.
        XCTAssertTrue(run(&recognizer, left: CGPoint(x: 0.24, y: 0.69), right: CGPoint(x: 0.62, y: 0.68), from: 2.3, for: 0.6).isEmpty)
        XCTAssertFalse(recognizer.isSignalling)
    }

    func testMissingJointsClearTheSignal() {
        var recognizer = AimSignalRecognizer()
        XCTAssertEqual(run(&recognizer, left: CGPoint(x: 0.24, y: 0.69), right: CGPoint(x: 0.58, y: 0.5), from: 0, for: 0.4), [.left])
        XCTAssertNil(recognizer.ingest(nil, at: 0.5))
        XCTAssertFalse(recognizer.isSignalling)
    }
}
