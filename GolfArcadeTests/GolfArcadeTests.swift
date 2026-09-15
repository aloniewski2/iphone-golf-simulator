import XCTest
@testable import GolfArcade

final class ShotEngineTests: XCTestCase {
    private let engine = ArcadeShotEngine()

    func testFasterSwingCarriesFarther() {
        let slow = engine.calculate(metrics: metrics(speed: 1.7), club: .driver)
        let fast = engine.calculate(metrics: metrics(speed: 3.8), club: .driver)
        XCTAssertGreaterThan(fast.carryYards, slow.carryYards)
    }

    func testArcadeForgivenessReducesDirectionError() {
        let shot = engine.calculate(metrics: metrics(speed: 3, direction: 1), club: .iron)
        XCTAssertEqual(shot.directionDegrees, 2.8, accuracy: 0.001)
    }

    func testThinStrikeLosesBallSpeed() {
        let center = engine.calculate(metrics: metrics(speed: 3), club: .iron)
        let thin = engine.calculate(metrics: metrics(speed: 3, impactHeight: 0.05), club: .iron)
        XCTAssertEqual(center.strike, .center)
        XCTAssertEqual(thin.strike, .thin)
        XCTAssertLessThan(thin.ballSpeedMPH, center.ballSpeedMPH)
    }

    func testFlightPathStartsAndLandsAtGroundLevel() {
        let path = FlightPath(shot: engine.calculate(metrics: metrics(speed: 3), club: .wedge))
        XCTAssertEqual(path.points.first?.heightYards, 0)
        XCTAssertEqual(path.points.last?.heightYards, 0)
        XCTAssertGreaterThan(path.points[path.points.count / 2].heightYards, 0)
    }

    private func metrics(speed: Double, direction: Double = 0, impactHeight: Double = 0) -> SwingMetrics {
        SwingMetrics(duration: 1.1, backswingDuration: 0.7, downswingDuration: 0.3, tempo: 2.33,
                     normalizedWristSpeed: speed, shoulderRotationDegrees: 30, hipRotationDegrees: 15,
                     swingDirection: direction, impactHeightDelta: impactHeight, balance: 0.9, confidence: 0.9)
    }
}

final class SwingStateMachineTests: XCTestCase {
    func testSyntheticSwingProducesShotMetrics() {
        var machine = SwingStateMachine()
        var time = 0.0
        var shotMetrics: SwingMetrics?
        func feed(_ handY: CGFloat, step: Double = 0.05) {
            time += step
            if case .shotReady(let metrics) = machine.ingest(frame(time: time, handY: handY)) { shotMetrics = metrics }
        }
        for _ in 0..<10 { feed(0.34) }
        feed(0.42); feed(0.55); feed(0.66); feed(0.58); feed(0.46); feed(0.35); feed(0.40)
        for _ in 0..<7 { feed(0.52) }
        XCTAssertNotNil(shotMetrics)
        XCTAssertGreaterThan(shotMetrics?.normalizedWristSpeed ?? 0, 0.2)
    }

    private func frame(time: Double, handY: CGFloat) -> PoseFrame {
        let high: Float = 0.95
        return PoseFrame(timestamp: time, points: [
            .leftShoulder: PosePoint(location: CGPoint(x: 0.4, y: 0.68), confidence: high),
            .rightShoulder: PosePoint(location: CGPoint(x: 0.6, y: 0.68), confidence: high),
            .leftWrist: PosePoint(location: CGPoint(x: 0.48, y: handY), confidence: high),
            .rightWrist: PosePoint(location: CGPoint(x: 0.52, y: handY), confidence: high),
            .leftHip: PosePoint(location: CGPoint(x: 0.44, y: 0.48), confidence: high),
            .rightHip: PosePoint(location: CGPoint(x: 0.56, y: 0.48), confidence: high)
        ])
    }
}
