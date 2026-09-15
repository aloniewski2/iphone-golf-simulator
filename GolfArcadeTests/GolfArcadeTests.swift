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

    func testSameSwingFartherFromTheCameraStillProducesAShot() {
        // Half-size body: every distance and speed in frame units is halved, so only body-relative
        // thresholds can recognise it.
        var machine = SwingStateMachine()
        var time = 0.0
        var shotMetrics: SwingMetrics?
        func feed(_ handY: CGFloat, step: Double = 0.05) {
            time += step
            if case .shotReady(let metrics) = machine.ingest(frame(time: time, handY: handY, scale: 0.5)) { shotMetrics = metrics }
        }
        for _ in 0..<10 { feed(0.34) }
        feed(0.42); feed(0.55); feed(0.66); feed(0.58); feed(0.46); feed(0.35); feed(0.40)
        for _ in 0..<7 { feed(0.52) }
        XCTAssertNotNil(shotMetrics)
        XCTAssertEqual(shotMetrics?.normalizedWristSpeed ?? 0, 2.6, accuracy: 0.01, "same value as the full-size body: reference-body units")
    }

    func testLosingAWristAtTheTopDoesNotResetTheSwing() {
        var machine = SwingStateMachine()
        var time = 0.0
        var events: [SwingEvent] = []
        func feed(_ handY: CGFloat, drop: Set<BodyJoint> = []) {
            time += 0.05
            if let event = machine.ingest(frame(time: time, handY: handY, dropping: drop)) { events.append(event) }
        }
        for _ in 0..<10 { feed(0.34) }
        feed(0.42); feed(0.55)
        feed(0.66, drop: [.rightWrist])                    // one wrist hidden: still tracked
        feed(0.66, drop: [.leftWrist, .rightWrist])        // both gone for a frame: skipped
        feed(0.58); feed(0.46); feed(0.35); feed(0.40)
        for _ in 0..<7 { feed(0.52) }
        XCTAssertFalse(events.contains(.phaseChanged(.findingPlayer)), "brief dropouts must not reset to Step into frame")
        XCTAssertTrue(events.contains { if case .shotReady = $0 { true } else { false } })
    }

    func testLosingTheBodyForLongerResetsTheSwing() {
        var machine = SwingStateMachine()
        var time = 0.0
        var events: [SwingEvent] = []
        func feed(_ handY: CGFloat, drop: Set<BodyJoint> = []) {
            time += 0.05
            if let event = machine.ingest(frame(time: time, handY: handY, dropping: drop)) { events.append(event) }
        }
        for _ in 0..<10 { feed(0.34) }
        feed(0.42); feed(0.55)
        for _ in 0..<15 { feed(0.66, drop: [.leftShoulder]) } // 0.75 s without a body
        XCTAssertEqual(events.last, .phaseChanged(.findingPlayer))
    }

    private func frame(time: Double, handY: CGFloat, scale: CGFloat = 1, dropping: Set<BodyJoint> = []) -> PoseFrame {
        let high: Float = 0.95
        func p(_ x: CGFloat, _ y: CGFloat) -> PosePoint {
            PosePoint(location: CGPoint(x: 0.5 + (x - 0.5) * scale, y: 0.5 + (y - 0.5) * scale), confidence: high)
        }
        let points: [BodyJoint: PosePoint] = [
            .leftShoulder: p(0.4, 0.68), .rightShoulder: p(0.6, 0.68),
            .leftWrist: p(0.48, handY), .rightWrist: p(0.52, handY),
            .leftHip: p(0.44, 0.48), .rightHip: p(0.56, 0.48)
        ]
        return PoseFrame(timestamp: time, points: points.filter { !dropping.contains($0.key) })
    }
}
