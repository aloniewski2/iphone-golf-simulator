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

final class PlayerCalibrationTests: XCTestCase {
    func testCalibrationCompletesAfterStableFullBodySamples() {
        var accumulator = CalibrationAccumulator()
        var result: PlayerCalibration?

        for index in 0..<CalibrationAccumulator.requiredSampleCount {
            result = accumulator.ingest(calibrationFrame(timestamp: Double(index) / 30))
        }

        XCTAssertNotNil(result)
        XCTAssertEqual(accumulator.progress, 1, accuracy: 0.001)
        XCTAssertEqual(accumulator.assessment, .ready)
    }

    func testOneWeakAnkleDoesNotBlockCalibration() {
        var accumulator = CalibrationAccumulator()
        let partiallyOccluded = calibrationFrame(timestamp: 0, dropping: [.leftAnkle])

        XCTAssertNil(accumulator.ingest(partiallyOccluded))
        XCTAssertEqual(accumulator.sampleCount, 1)
        XCTAssertGreaterThan(accumulator.progress, 0)
        XCTAssertEqual(accumulator.assessment, .ready)
    }

    func testMissingBothFeetDoesNotAdvanceCalibration() {
        var accumulator = CalibrationAccumulator()
        let incomplete = calibrationFrame(timestamp: 0, dropping: [.leftAnkle, .rightAnkle])

        XCTAssertNil(accumulator.ingest(incomplete))
        XCTAssertEqual(accumulator.progress, 0)
        XCTAssertEqual(accumulator.assessment, .incompleteBody)
    }

    func testBriefTrackingDropoutPreservesProgress() {
        var accumulator = CalibrationAccumulator()
        for index in 0..<10 {
            XCTAssertNil(accumulator.ingest(calibrationFrame(timestamp: Double(index) / 30)))
        }

        XCTAssertNil(accumulator.ingest(calibrationFrame(timestamp: 10.0 / 30, dropping: [.leftAnkle, .rightAnkle])))
        XCTAssertEqual(accumulator.sampleCount, 10)
        XCTAssertEqual(accumulator.assessment, .incompleteBody)

        var result: PlayerCalibration?
        for index in 11...30 {
            result = accumulator.ingest(calibrationFrame(timestamp: Double(index) / 30))
        }
        XCTAssertNotNil(result)
        XCTAssertEqual(accumulator.progress, 1, accuracy: 0.001)
    }

    func testMultiplePeoplePauseWithoutErasingProgress() {
        var accumulator = CalibrationAccumulator()
        for index in 0..<8 {
            _ = accumulator.ingest(calibrationFrame(timestamp: Double(index) / 30))
        }

        XCTAssertNil(accumulator.ingest(calibrationFrame(timestamp: 8.0 / 30), detectedBodyCount: 2))
        XCTAssertEqual(accumulator.sampleCount, 8)
        XCTAssertEqual(accumulator.assessment, .multiplePeople)
    }

    func testOpenPoseWorksWhenCoordinatesAreMirrored() {
        let original = calibrationFrame(timestamp: 0)
        let mirrored = PoseFrame(
            timestamp: original.timestamp,
            points: original.points.mapValues {
                PosePoint(location: CGPoint(x: 1 - $0.location.x, y: $0.location.y), confidence: $0.confidence)
            }
        )

        XCTAssertTrue(mirrored.hasOpenCalibrationPose)
        XCTAssertTrue(mirrored.hasCalibrationBody)
    }

    func testClockwiseSidewaysPoseIsCorrectedAndCanCalibrate() {
        let original = calibrationFrame(timestamp: 0)
        let sideways = transform(original) { CGPoint(x: $0.y, y: 1 - $0.x) }
        let correction = sideways.correctingSidewaysOrientation()

        XCTAssertTrue(correction.quarterTurned)
        assertLandmarks(correction.frame, match: original)

        var accumulator = CalibrationAccumulator()
        var result: PlayerCalibration?
        for index in 0..<CalibrationAccumulator.requiredSampleCount {
            let frame = calibrationFrame(timestamp: Double(index) / 30)
            let rotated = transform(frame) { CGPoint(x: $0.y, y: 1 - $0.x) }
            result = accumulator.ingest(rotated.correctingSidewaysOrientation().frame)
        }
        XCTAssertNotNil(result)
        XCTAssertEqual(accumulator.progress, 1, accuracy: 0.001)
        XCTAssertEqual(accumulator.assessment, .ready)
    }

    func testCounterClockwiseSidewaysPoseIsCorrectedAndCanCalibrate() {
        let original = calibrationFrame(timestamp: 0)
        let sideways = transform(original) { CGPoint(x: 1 - $0.y, y: $0.x) }
        let correction = sideways.correctingSidewaysOrientation()

        XCTAssertTrue(correction.quarterTurned)
        assertLandmarks(correction.frame, match: original)

        var accumulator = CalibrationAccumulator()
        XCTAssertNil(accumulator.ingest(correction.frame))
        XCTAssertEqual(accumulator.sampleCount, 1)
        XCTAssertEqual(accumulator.assessment, .ready)
    }

    func testNormalGolfLeanIsNotRotated() {
        let leaning = transform(calibrationFrame(timestamp: 0)) {
            CGPoint(x: $0.x + (0.5 - $0.y) * 0.18, y: $0.y)
        }

        let correction = leaning.correctingSidewaysOrientation()

        XCTAssertFalse(correction.quarterTurned)
        XCTAssertEqual(correction.frame, leaning)
    }

    func testProgressRestartsOnlyAfterPlayerIsGoneForTwoSeconds() {
        var accumulator = CalibrationAccumulator()
        for index in 0..<8 {
            _ = accumulator.ingest(calibrationFrame(timestamp: Double(index) / 30))
        }

        accumulator.reportTracking(bodyCount: 0, timestamp: 2.5)
        XCTAssertEqual(accumulator.sampleCount, 0)
        XCTAssertEqual(accumulator.assessment, .noBody)
    }

    func testStoredBodySignaturePrefersTheCalibratedPlayer() throws {
        let calibration = try XCTUnwrap(makeCalibration())
        let matching = calibrationFrame(timestamp: 1)
        let background = calibrationFrame(timestamp: 1, armScale: 1.75)

        let matchingScore = try XCTUnwrap(calibration.matchScore(for: matching))
        let backgroundScore = try XCTUnwrap(calibration.matchScore(for: background))

        XCTAssertLessThan(matchingScore, backgroundScore)
        XCTAssertLessThan(matchingScore, 0.24)
    }

    func testSignatureCanMatchPlayerWhenLegsLeaveSwingFrame() throws {
        let calibration = try XCTUnwrap(makeCalibration())
        let upperBodyOnly = calibrationFrame(
            timestamp: 1,
            dropping: [.nose, .neck, .root, .leftHip, .rightHip, .leftKnee, .rightKnee, .leftAnkle, .rightAnkle]
        )

        let score = try XCTUnwrap(calibration.matchScore(for: upperBodyOnly))
        XCTAssertLessThan(score, 0.24)
    }

    func testAcquiredPlayerSurvivesArmForeshorteningAndMissingLegs() throws {
        let calibration = try XCTUnwrap(makeCalibration())
        var selector = PlayerPoseSelector()
        XCTAssertNotNil(selector.select(from: [calibrationFrame(timestamp: 0)], calibration: calibration, at: 0))
        let swing = calibrationFrame(timestamp: 0.03, dropping: [.leftAnkle, .rightAnkle, .leftKnee, .rightKnee], armScale: 0.25)
        XCTAssertEqual(selector.select(from: [swing], calibration: calibration, at: 0.03)?.frame, swing,
                       "limb proportions must not re-certify an already acquired player every frame")
    }

    func testPlayerContinuityDoesNotJumpToADistantBystander() throws {
        let calibration = try XCTUnwrap(makeCalibration())
        var selector = PlayerPoseSelector()
        let original = calibrationFrame(timestamp: 0)
        XCTAssertNotNil(selector.select(from: [original], calibration: calibration, at: 0))
        let bystander = transform(calibrationFrame(timestamp: 0.03)) { CGPoint(x: $0.x + 0.55, y: $0.y) }
        XCTAssertNil(selector.select(from: [bystander], calibration: calibration, at: 0.03))
        XCTAssertEqual(selector.select(from: [bystander, original], calibration: calibration, at: 0.06)?.frame, original)
        XCTAssertNil(selector.select(from: [original, original], calibration: calibration, at: 0.09), "ambiguous overlap is not an identity match")
    }

    func testAcquiredPlayerRemainsSelectedWhenBackswingOccludesShoulders() throws {
        let calibration = try XCTUnwrap(makeCalibration())
        var selector = PlayerPoseSelector()
        XCTAssertNotNil(selector.select(from: [calibrationFrame(timestamp: 0)], calibration: calibration, at: 0))
        for i in 1...20 {
            let time = Double(i) / 30
            let turned = calibrationFrame(timestamp: time, dropping: [.leftShoulder, .rightShoulder], armScale: 0.5)
            XCTAssertEqual(selector.select(from: [turned], calibration: calibration, at: time)?.frame, turned,
                           "measured hips/root must preserve identity through shoulder occlusion")
        }
        let unseen = calibrationFrame(timestamp: 0.7, dropping: [.leftShoulder, .rightShoulder, .leftHip, .rightHip, .neck, .root])
        XCTAssertNil(selector.select(from: [unseen], calibration: calibration, at: 0.7), "hands alone cannot identify the player")
    }

    func testCalibrationStoreRoundTripsAndClears() throws {
        let suiteName = "PlayerCalibrationTests.\(UUID().uuidString)"
        let defaults = try XCTUnwrap(UserDefaults(suiteName: suiteName))
        defer { defaults.removePersistentDomain(forName: suiteName) }
        let calibration = try XCTUnwrap(makeCalibration())

        PlayerCalibrationStore.save(calibration, defaults: defaults)
        XCTAssertEqual(PlayerCalibrationStore.load(defaults: defaults), calibration)

        PlayerCalibrationStore.clear(defaults: defaults)
        XCTAssertNil(PlayerCalibrationStore.load(defaults: defaults))
    }

    func testRosterMigratesTheSinglePlayerScan() throws {
        let suiteName = "PlayerRosterTests.\(UUID().uuidString)"
        let defaults = try XCTUnwrap(UserDefaults(suiteName: suiteName))
        defer { defaults.removePersistentDomain(forName: suiteName) }
        let calibration = try XCTUnwrap(makeCalibration())
        PlayerCalibrationStore.save(calibration, defaults: defaults)
        defaults.set("left", forKey: "range.handedness")

        let migrated = PlayerRosterStore.load(defaults: defaults)
        XCTAssertEqual(migrated.count, 1)
        XCTAssertEqual(migrated[0].calibration, calibration)
        XCTAssertEqual(migrated[0].handedness, .left)

        var players = migrated
        players.append(Player(name: "Sam", colorIndex: 1))
        PlayerRosterStore.save(players, defaults: defaults)
        XCTAssertEqual(PlayerRosterStore.load(defaults: defaults), players)
    }

    @MainActor
    func testGameFlowNeedsEveryPlayerScanned() {
        let flow = GameFlow(fixturePlayers: [Player(name: "Ana", colorIndex: 0, calibration: .uiTestingFixture)])
        flow.choose(.solo)
        XCTAssertTrue(flow.canContinue)
        flow.choose(.multiplayer)
        XCTAssertEqual(flow.players.count, 2)
        XCTAssertFalse(flow.canContinue)
        flow.finishScan(flow.players[1].id, calibration: .uiTestingFixture)
        XCTAssertTrue(flow.canContinue)
        flow.addPlayer(); flow.addPlayer(); flow.addPlayer()
        XCTAssertEqual(flow.players.count, GameFlow.maxPlayers)
        flow.removePlayer(flow.players[3].id)
        XCTAssertEqual(flow.players.count, 3)
        flow.choose(.solo)
        XCTAssertEqual(flow.players.map(\.name), ["Ana"])
    }

    private func makeCalibration() -> PlayerCalibration? {
        var accumulator = CalibrationAccumulator()
        var result: PlayerCalibration?
        for index in 0..<CalibrationAccumulator.requiredSampleCount {
            result = accumulator.ingest(calibrationFrame(timestamp: Double(index) / 30))
        }
        return result
    }

    private func calibrationFrame(
        timestamp: TimeInterval,
        dropping: Set<BodyJoint> = [],
        armScale: CGFloat = 1
    ) -> PoseFrame {
        let confidence: Float = 0.95
        let centerX: CGFloat = 0.5
        func armX(_ x: CGFloat) -> CGFloat { centerX + (x - centerX) * armScale }
        func point(_ x: CGFloat, _ y: CGFloat) -> PosePoint {
            PosePoint(location: CGPoint(x: x, y: y), confidence: confidence)
        }
        let points: [BodyJoint: PosePoint] = [
            .nose: point(0.50, 0.87), .neck: point(0.50, 0.77),
            .leftShoulder: point(0.40, 0.72), .rightShoulder: point(0.60, 0.72),
            .leftElbow: point(armX(0.34), 0.61), .rightElbow: point(armX(0.66), 0.61),
            .leftWrist: point(armX(0.28), 0.50), .rightWrist: point(armX(0.72), 0.50),
            .root: point(0.50, 0.46),
            .leftHip: point(0.44, 0.47), .rightHip: point(0.56, 0.47),
            .leftKnee: point(0.44, 0.28), .rightKnee: point(0.56, 0.28),
            .leftAnkle: point(0.44, 0.10), .rightAnkle: point(0.56, 0.10)
        ]
        return PoseFrame(timestamp: timestamp, points: points.filter { !dropping.contains($0.key) })
    }

    private func transform(_ frame: PoseFrame, _ transform: (CGPoint) -> CGPoint) -> PoseFrame {
        PoseFrame(
            timestamp: frame.timestamp,
            points: frame.points.mapValues {
                PosePoint(location: transform($0.location), confidence: $0.confidence)
            }
        )
    }

    private func assertLandmarks(
        _ actual: PoseFrame,
        match expected: PoseFrame,
        file: StaticString = #filePath,
        line: UInt = #line
    ) {
        for joint in BodyJoint.allCases {
            guard let actualPoint = actual.points[joint], let expectedPoint = expected.points[joint] else {
                XCTFail("Missing landmark \(joint)", file: file, line: line)
                continue
            }
            XCTAssertEqual(actualPoint.location.x, expectedPoint.location.x, accuracy: 0.0001, file: file, line: line)
            XCTAssertEqual(actualPoint.location.y, expectedPoint.location.y, accuracy: 0.0001, file: file, line: line)
            XCTAssertEqual(actualPoint.confidence, expectedPoint.confidence, file: file, line: line)
        }
    }
}

final class PoseSkeletonMapperTests: XCTestCase {
    func testCameraRotationFallsBackToSupportedRightAngles() {
        XCTAssertEqual(CameraRotation.nearestRightAngle(to: 89.7), 90)
        XCTAssertEqual(CameraRotation.nearestRightAngle(to: 271), 270)
        XCTAssertEqual(CameraRotation.nearestRightAngle(to: -90), 270)
        XCTAssertEqual(CameraRotation.nearestRightAngle(to: 359.9), 0)
    }

    func testAspectFitMapsPortraitFrameIntoVisibleCameraRect() {
        let size = CGSize(width: 390, height: 844)
        let topLeft = PoseSkeletonMapper.screenPoint(
            CGPoint(x: 0, y: 1), in: size, frameAspect: 0.75, fitsEntireFrame: true
        )
        let bottomRight = PoseSkeletonMapper.screenPoint(
            CGPoint(x: 1, y: 0), in: size, frameAspect: 0.75, fitsEntireFrame: true
        )

        XCTAssertEqual(topLeft.x, 0, accuracy: 0.001)
        XCTAssertEqual(topLeft.y, 162, accuracy: 0.001)
        XCTAssertEqual(bottomRight.x, 390, accuracy: 0.001)
        XCTAssertEqual(bottomRight.y, 682, accuracy: 0.001)
    }

    func testAspectFillAccountsForHorizontalCameraCropping() {
        let size = CGSize(width: 390, height: 844)
        let topLeft = PoseSkeletonMapper.screenPoint(
            CGPoint(x: 0, y: 1), in: size, frameAspect: 0.75, fitsEntireFrame: false
        )
        let center = PoseSkeletonMapper.screenPoint(
            CGPoint(x: 0.5, y: 0.5), in: size, frameAspect: 0.75, fitsEntireFrame: false
        )

        XCTAssertEqual(topLeft.x, -121.5, accuracy: 0.001)
        XCTAssertEqual(topLeft.y, 0, accuracy: 0.001)
        XCTAssertEqual(center.x, size.width / 2, accuracy: 0.001)
        XCTAssertEqual(center.y, size.height / 2, accuracy: 0.001)
    }

    func testLandscapeCenterRemainsAligned() {
        let size = CGSize(width: 844, height: 390)
        let center = PoseSkeletonMapper.screenPoint(
            CGPoint(x: 0.5, y: 0.5), in: size, frameAspect: 4.0 / 3.0, fitsEntireFrame: true
        )

        XCTAssertEqual(center.x, size.width / 2, accuracy: 0.001)
        XCTAssertEqual(center.y, size.height / 2, accuracy: 0.001)
    }
}
