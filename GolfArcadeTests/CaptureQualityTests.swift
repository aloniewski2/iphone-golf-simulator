import XCTest
@testable import GolfArcade

final class BodyModelTests: XCTestCase {
    private let high: Float = 0.9
    private func p(_ x: CGFloat, _ y: CGFloat, _ c: Float = 0.9) -> PosePoint { PosePoint(location: CGPoint(x: x, y: y), confidence: c) }
    private func address(_ t: Double) -> PoseFrame {
        PoseFrame(timestamp: t, points: [
            .leftShoulder: p(0.4, 0.7), .rightShoulder: p(0.6, 0.7),
            .leftElbow: p(0.42, 0.58), .rightElbow: p(0.58, 0.58),
            .leftWrist: p(0.49, 0.47), .rightWrist: p(0.51, 0.47)
        ])
    }

    func testLearnsLengthsAtAddressThenRejectsStretchedArmsAndSplitGrip() {
        var model = BodyModel()
        for i in 0..<15 { _ = model.apply(to: address(Double(i) / 30), isStill: true) }
        XCTAssertTrue(model.isCalibrated)
        XCTAssertEqual(model.lengths[BodyModel.Bone(from: .leftShoulder, to: .rightShoulder)] ?? 0, 0.2, accuracy: 1e-6)

        // A wrist reported far past the forearm's length is impossible in 2D: it is dropped.
        var bad = address(1).points
        bad[.leftWrist] = p(0.2, 0.2, 0.5)
        let cleaned = model.apply(to: PoseFrame(timestamp: 1, points: bad), isStill: false)
        XCTAssertNil(cleaned.points[.leftWrist])
        XCTAssertNotNil(cleaned.points[.rightWrist])

        // Foreshortening (arm turning toward the camera) is allowed: shorter bones pass.
        var short = address(2).points
        short[.leftWrist] = p(0.44, 0.55)
        XCTAssertNotNil(model.apply(to: PoseFrame(timestamp: 2, points: short), isStill: false).points[.leftWrist])

        // Wrists a shoulder-width apart cannot both be on the grip: the less confident one goes.
        var split = address(3).points
        split[.rightWrist] = p(0.75, 0.5, 0.6)
        let fixed = model.apply(to: PoseFrame(timestamp: 3, points: split), isStill: false)
        XCTAssertNil(fixed.points[.rightWrist])
        XCTAssertNotNil(fixed.points[.leftWrist])
    }

    func testDoesNotLearnWhileMoving() {
        var model = BodyModel()
        for i in 0..<30 { _ = model.apply(to: address(Double(i) / 30), isStill: false) }
        XCTAssertFalse(model.isCalibrated)
    }
}

final class CaptureReadinessTests: XCTestCase {
    private func p(_ x: CGFloat, _ y: CGFloat, _ c: Float = 0.9) -> PosePoint { PosePoint(location: CGPoint(x: x, y: y), confidence: c) }
    private func player(scale: CGFloat = 1, confidence: Float = 0.9) -> PoseFrame {
        func s(_ x: CGFloat, _ y: CGFloat) -> PosePoint { p(0.5 + (x - 0.5) * scale, 0.5 + (y - 0.5) * scale, confidence) }
        return PoseFrame(timestamp: 0, points: [
            .nose: s(0.5, 0.85), .leftShoulder: s(0.42, 0.75), .rightShoulder: s(0.58, 0.75),
            .leftElbow: s(0.4, 0.62), .rightElbow: s(0.6, 0.62), .leftWrist: s(0.48, 0.5), .rightWrist: s(0.52, 0.5),
            .leftHip: s(0.45, 0.5), .rightHip: s(0.55, 0.5), .leftAnkle: s(0.45, 0.15), .rightAnkle: s(0.55, 0.15)
        ])
    }

    func testChecklistGoesGreenOnlyWhenEverythingIsRight() {
        var readiness = CaptureReadiness()
        var input = CaptureReadiness.Input(frame: player())
        readiness.evaluate(input, at: 0)
        XCTAssertEqual(readiness.failing, [.jointsSteady], "everything but the steady timer passes at once")
        readiness.evaluate(input, at: 0.5)
        XCTAssertFalse(readiness.isReady)
        readiness.evaluate(input, at: 1.0)
        XCTAssertTrue(readiness.isReady)

        input.rollDegrees = 15
        readiness.evaluate(input, at: 1.1)
        XCTAssertEqual(readiness.firstProblem, .level)
        input.rollDegrees = 0
        input.exposureOffsetEV = -2
        readiness.evaluate(input, at: 1.2)
        XCTAssertEqual(readiness.firstProblem, .exposure)
        input.exposureOffsetEV = 0
        input.peopleInView = 2
        readiness.evaluate(input, at: 1.3)
        XCTAssertEqual(readiness.firstProblem, .singlePerson)
        input.peopleInView = 1
        input.frame = player(scale: 0.5)
        readiness.evaluate(input, at: 1.4)
        XCTAssertEqual(readiness.firstProblem, .bodySize, "a body filling a third of the frame is too far away")
        input.frame = player(confidence: 0.3)
        readiness.evaluate(input, at: 1.5)
        XCTAssertTrue(readiness.failing.contains(.jointsSteady))
    }

    func testBodyHeightFallsBackToTheTorsoWhenFeetAreOut() {
        let full = player()
        XCTAssertEqual(CaptureReadiness.bodyHeight(of: full) ?? 0, 0.7, accuracy: 1e-6)
        var noFeet = full.points
        noFeet[.leftAnkle] = nil
        noFeet[.rightAnkle] = nil
        let estimated = CaptureReadiness.bodyHeight(of: PoseFrame(timestamp: 0, points: noFeet)) ?? 0
        XCTAssertEqual(estimated, 0.25 * 3.3, accuracy: 1e-6)
    }
}
