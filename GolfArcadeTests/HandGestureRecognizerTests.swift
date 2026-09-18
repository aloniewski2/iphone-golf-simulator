import XCTest
@testable import GolfArcade

final class HandGestureRecognizerTests: XCTestCase {
    private let shoulderY: CGFloat = 0.7
    private let width: CGFloat = 0.2

    /// A player facing the camera: left shoulder at x 0.4, right at 0.6.
    private func frame(left: CGPoint, right: CGPoint, leftShape: HandShape? = nil, rightShape: HandShape? = nil, at time: Double) -> PoseFrame {
        let point = { (location: CGPoint) in PosePoint(location: location, confidence: 0.9) }
        var hands: [HandReading] = []
        if let leftShape { hands.append(HandReading(wrist: .leftWrist, shape: leftShape)) }
        if let rightShape { hands.append(HandReading(wrist: .rightWrist, shape: rightShape)) }
        return PoseFrame(timestamp: time, points: [
            .leftShoulder: point(CGPoint(x: 0.4, y: shoulderY)),
            .rightShoulder: point(CGPoint(x: 0.6, y: shoulderY)),
            .leftWrist: point(left),
            .rightWrist: point(right)
        ], hands: hands)
    }

    /// Resting left hand; the right hand does the gesture.
    private func run(_ path: [(time: Double, right: CGPoint, shape: HandShape?)], recognizer: inout HandGestureRecognizer) -> [NavGesture] {
        path.compactMap { step in
            recognizer.ingest(frame(left: CGPoint(x: 0.35, y: 0.4), right: step.right, rightShape: step.shape, at: step.time), at: step.time)
        }
    }

    private func hold(_ point: CGPoint, shape: HandShape?, from start: Double, for seconds: Double) -> [(Double, CGPoint, HandShape?)] {
        stride(from: start, to: start + seconds, by: 1.0 / 30).map { ($0, point, shape) }
    }

    private func move(from a: CGPoint, to b: CGPoint, shape: HandShape?, start: Double, seconds: Double) -> [(Double, CGPoint, HandShape?)] {
        let steps = Int(seconds * 30)
        return (1...steps).map { i in
            let t = CGFloat(i) / CGFloat(steps)
            return (start + Double(i) / 30, CGPoint(x: a.x + (b.x - a.x) * t, y: a.y + (b.y - a.y) * t), shape)
        }
    }

    func testFistSwipesInEachDirection() {
        let home = CGPoint(x: 0.7, y: 0.5)
        let cases: [(CGPoint, NavGesture)] = [
            (CGPoint(x: 0.95, y: 0.5), .right), (CGPoint(x: 0.45, y: 0.55), .left),
            (CGPoint(x: 0.72, y: 0.75), .up), (CGPoint(x: 0.7, y: 0.25), .down)
        ]
        for (end, expected) in cases {
            var recognizer = HandGestureRecognizer()
            let path = hold(home, shape: .fist, from: 0, for: 0.4) + move(from: home, to: end, shape: .fist, start: 0.4, seconds: 0.25)
            XCTAssertEqual(run(path, recognizer: &recognizer), [expected], "\(expected)")
        }
    }

    func testPunchTowardCameraSelects() {
        var recognizer = HandGestureRecognizer()
        // Chambered fist low beside the body, then driven at the camera: it lands on the shoulder in 2D.
        let chamber = CGPoint(x: 0.72, y: 0.5)
        let extended = CGPoint(x: 0.62, y: 0.66)
        let path = hold(chamber, shape: .fist, from: 0, for: 0.4)
            + move(from: chamber, to: extended, shape: .fist, start: 0.4, seconds: 0.15)
            + hold(extended, shape: .fist, from: 0.6, for: 0.1)
        XCTAssertEqual(run(path, recognizer: &recognizer), [.select])
    }

    func testOpenHandAndSlowDriftDoNothing() {
        var recognizer = HandGestureRecognizer()
        let home = CGPoint(x: 0.7, y: 0.5)
        let open = hold(home, shape: .open, from: 0, for: 0.4) + move(from: home, to: CGPoint(x: 0.95, y: 0.5), shape: .open, start: 0.4, seconds: 0.25)
        XCTAssertEqual(run(open, recognizer: &recognizer), [])
        var slow = HandGestureRecognizer()
        let drift = hold(home, shape: .fist, from: 0, for: 0.4) + move(from: home, to: CGPoint(x: 0.95, y: 0.5), shape: .fist, start: 0.4, seconds: 1.5)
        XCTAssertEqual(run(drift, recognizer: &slow), [])
    }

    func testRaisedHandFallbackWhenFingersCannotBeRead() {
        var recognizer = HandGestureRecognizer()
        let raised = CGPoint(x: 0.7, y: 0.75)
        let path = hold(raised, shape: nil, from: 0, for: 0.4) + move(from: raised, to: CGPoint(x: 0.95, y: 0.78), shape: nil, start: 0.4, seconds: 0.25)
        XCTAssertEqual(run(path, recognizer: &recognizer), [.right])

        var lowered = HandGestureRecognizer()
        let drop = hold(raised, shape: nil, from: 0, for: 0.4) + move(from: raised, to: CGPoint(x: 0.7, y: 0.3), shape: nil, start: 0.4, seconds: 0.25)
        XCTAssertEqual(run(drop, recognizer: &lowered), [], "lowering a raised open hand is not a swipe down")
    }

    func testGolfSwingWithHandsTogetherNeverNavigates() {
        var recognizer = HandGestureRecognizer()
        var gestures: [NavGesture] = []
        // Both hands gripping together, swinging from address up past the shoulder and through.
        for i in 0..<60 {
            let t = Double(i) / 30
            let angle = sin(t * 3) * 2.2
            let hands = CGPoint(x: 0.5 + CGFloat(sin(angle)) * 0.3, y: 0.7 - CGFloat(cos(angle)) * 0.3)
            let reading = frame(
                left: CGPoint(x: hands.x - 0.02, y: hands.y), right: CGPoint(x: hands.x + 0.02, y: hands.y),
                leftShape: .fist, rightShape: .fist, at: t
            )
            if let gesture = recognizer.ingest(reading, at: t) { gestures.append(gesture) }
        }
        XCTAssertEqual(gestures, [])
    }

    func testCooldownAndSuppression() {
        var recognizer = HandGestureRecognizer()
        let home = CGPoint(x: 0.7, y: 0.5)
        let right = CGPoint(x: 0.95, y: 0.5)
        let first = hold(home, shape: .fist, from: 0, for: 0.4) + move(from: home, to: right, shape: .fist, start: 0.4, seconds: 0.25)
        let back = move(from: right, to: home, shape: .fist, start: 0.7, seconds: 0.25)
        XCTAssertEqual(run(first + back, recognizer: &recognizer), [.right], "the return stroke falls inside the cooldown")

        var suppressed = HandGestureRecognizer()
        suppressed.suppress(until: 5)
        XCTAssertEqual(run(first, recognizer: &suppressed), [])
    }
}
