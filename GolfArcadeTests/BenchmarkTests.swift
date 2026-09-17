import XCTest
@testable import GolfArcade

@MainActor
final class BenchmarkTests: XCTestCase {
    private func trial(_ challenge: BenchmarkChallenge = .straight,
                       impacts: [SwingImpact] = [], source: BenchmarkTrial.Source = .camera) -> BenchmarkTrial {
        var result = BenchmarkTrial(id: UUID(), challenge: challenge, handedness: .right,
            participant: "P01", conditions: "test", source: source, recordingConsented: false)
        result.duration = 12
        result.frameCount = 360
        result.usableFrameCount = 350
        result.impacts = impacts.map { BenchmarkImpact($0, time: 2, club: challenge.club) }
        return result
    }

    private let hit = SwingImpact(power: 0.5, source: .camera)

    private func captureEvidence(generation: Int = 1, mirrored: Bool = true) -> CaptureEvidence {
        .init(configuration: .init(profile: .wideFront, cameraName: "Test", deviceType: "test",
            formatIndex: 0, requestedFPS: 120, configuredFPS: 60, formatWidth: 1280, formatHeight: 1280,
            nominalFieldOfView: 95, dynamicAspectRatio: "1:1", centerStageActive: false, fallbackReason: "Test fallback"),
            geometry: .init(width: 1280, height: 1280, rotationDegrees: 0, mirrored: mirrored,
                configurationGeneration: generation), thermalState: "nominal")
    }

    func testCaptureEvidenceAndSourceConsentAreFrozenAndKeepClockOrigin() throws {
        let lab = PracticeLab()
        lab.sourceVideoConsent = true
        lab.captureProfile = .wideFront
        lab.ingest(observation(time: 100))
        XCTAssertTrue(lab.begin(at: 100.02))
        lab.sourceVideoConsent = false
        lab.captureProfile = .baseline
        var sample = observation(time: 100.1)
        sample = CameraSwingObservation(delivery: PoseDelivery(frame: sample.delivery.frame, captureTime: 100.1,
            aspect: 1, bodyCount: 1, callbackStarted: 100.2, inferenceFinished: 100.22,
            captureEvidence: captureEvidence()), event: nil, phase: "address", stateUpdated: 100.23)
        lab.ingest(sample)
        lab.tick(at: 113)
        let trial = try XCTUnwrap(lab.trials.first)
        XCTAssertEqual(trial.sourceVideoConsented, true)
        XCTAssertEqual(trial.firstCapturePTS, 100.1)
        XCTAssertEqual(trial.diagnostics?.captureProfile, .wideFront)
        XCTAssertEqual(trial.diagnostics?.captureEvidence?.configuration.configuredFPS, 60)
        XCTAssertEqual(trial.diagnostics?.thermalStates?["nominal"], 1)
    }

    func testCoordinateChangeInterruptsTrialRatherThanInventingAccuracy() {
        let lab = PracticeLab()
        lab.ingest(observation(time: 100))
        XCTAssertTrue(lab.begin(at: 100.02))
        for (index, mirror) in [true, false].enumerated() {
            let time = 100.1 + Double(index) * 0.1
            let base = observation(time: time)
            let delivery = PoseDelivery(frame: base.delivery.frame, captureTime: time, aspect: 1,
                bodyCount: 1, callbackStarted: time, inferenceFinished: time + 0.01,
                captureEvidence: captureEvidence(mirrored: mirror))
            lab.ingest(CameraSwingObservation(delivery: delivery, event: nil, phase: "address", stateUpdated: time + 0.02))
        }
        XCTAssertNil(lab.active)
        XCTAssertEqual(lab.trials.first?.interruption, "Capture geometry changed")
        XCTAssertEqual(lab.summary.recognition.total, 0)
    }

    func testOldSchemaWithoutCaptureEvidenceStillDecodes() throws {
        let report = BenchmarkReport(device: "test", os: "test", appVersion: "test", trials: [trial()])
        var json = try XCTUnwrap(JSONSerialization.jsonObject(with: report.encodedJSON()) as? [String: Any])
        json["schemaVersion"] = 3
        json.removeValue(forKey: "cameraInventory")
        json.removeValue(forKey: "displayLatencyMeasurements")
        let decoder = JSONDecoder()
        decoder.dateDecodingStrategy = .iso8601
        let decoded = try decoder.decode(BenchmarkReport.self, from: JSONSerialization.data(withJSONObject: json))
        XCTAssertEqual(decoded.schemaVersion, 3)
        XCTAssertNil(decoded.trials.first?.firstCapturePTS)
        XCTAssertNil(decoded.cameraInventory)
    }

    func testCaptureCoordinateChangeCancelsControllerWithoutImpact() {
        let camera = CameraSwingController()
        var cancellations = 0
        var impacts = 0
        camera.onEvent = {
            if case .cancel = $0 { cancellations += 1 }
            if case .impact = $0 { impacts += 1 }
        }
        for (index, mirror) in [true, true, false].enumerated() {
            let time = Double(index)
            let delivery = PoseDelivery(frame: nil, captureTime: time, aspect: 1, bodyCount: 0,
                callbackStarted: time, inferenceFinished: time,
                captureEvidence: captureEvidence(mirrored: mirror))
            camera.process(delivery, now: time)
        }
        XCTAssertEqual(cancellations, 1)
        XCTAssertEqual(impacts, 0)
        XCTAssertFalse(camera.isPositionLocked)
    }

    func testRetriesAndRepeatedAttemptsCannotPassFirstTryMetric() {
        var retry = trial(impacts: [hit])
        retry.diagnostics = BenchmarkDiagnostics(attempts: 1, retryCount: 1)
        var repeated = trial(impacts: [hit])
        repeated.diagnostics = BenchmarkDiagnostics(attempts: 2)
        let summary = BenchmarkSummary(trials: [retry, repeated, trial(.abortedTakeaway), trial(.pausedSwing, impacts: [hit])])
        XCTAssertEqual(summary.recognition.total, 3)
        XCTAssertEqual(summary.recognition.successes, 1)
        XCTAssertEqual(summary.retries, 1)
        XCTAssertEqual(summary.negativeTrials, 1)
    }

    func testTrialDiagnosticsDeduplicateLoadsAndFreezeExperiment() throws {
        let lab = PracticeLab()
        lab.poseMode = .depthPreview
        lab.requestedCaptureFPS = 60
        lab.ingest(observation(time: 100))
        XCTAssertTrue(lab.begin(at: 100.02))
        lab.poseMode = .body2D
        lab.requestedCaptureFPS = 30
        lab.ingest(observation(time: 100.1, event: .load(0.2)))
        lab.ingest(observation(time: 100.2, event: .load(0.4)))
        var retry = observation(time: 100.4, event: .cancel)
        retry.readiness = "retry"
        lab.ingest(retry)
        lab.ingest(observation(time: 100.5, event: .load(0.2)))
        lab.ingest(observation(time: 100.6, event: .impact(hit)))
        lab.tick(at: 113)
        let diagnostics = try XCTUnwrap(lab.trials.first?.diagnostics)
        XCTAssertEqual(diagnostics.poseMode, .depthPreview)
        XCTAssertEqual(diagnostics.requestedCaptureFPS, 60)
        XCTAssertEqual(diagnostics.attempts, 2)
        XCTAssertEqual(diagnostics.cancelCount, 1)
        XCTAssertEqual(diagnostics.retryCount, 1)
        XCTAssertEqual(diagnostics.maximumDeliveryGapMS, 200, accuracy: 0.001)
        XCTAssertEqual(lab.summary.recognition.successes, 0)
    }

    func testDepthTraceUsesRelativeTimeAndRoundTrips() throws {
        let original = PoseFrame(timestamp: 100.1, points: BenchmarkReplay.fixture().poses[0].frame!.points,
            depth: BodyDepthEstimate(timestamp: 100.05, normalizedDepth: [BodyJoint.leftElbow.rawValue: 0.1]))
        let trace = BenchmarkPose(frame: original, time: 0.1, aspect: 1)
        let decoded = try JSONDecoder().decode(BenchmarkPose.self, from: JSONEncoder().encode(trace))
        XCTAssertEqual(decoded, trace)
        XCTAssertEqual(decoded.depth!.timestamp, 0.05, accuracy: 0.0001)
        XCTAssertNotNil(decoded.frame?.depth?.offset(for: .leftElbow, at: decoded.time))
    }

    private func observation(time: Double, event: SwingInputEvent? = nil, missing: Bool = false) -> CameraSwingObservation {
        let pose = BenchmarkReplay.fixture().poses[0].frame!
        let frame = missing ? nil : PoseFrame(timestamp: time, points: pose.points)
        return CameraSwingObservation(delivery: PoseDelivery(frame: frame, captureTime: time,
            aspect: 1, bodyCount: missing ? 0 : 1, callbackStarted: time,
            inferenceFinished: time + 0.01), event: event, phase: "address", stateUpdated: time + 0.015)
    }

    func testFixtureReplaysAndNeverCountsAsRealEvidence() {
        let fixture = BenchmarkReplay.fixture()
        XCTAssertEqual(fixture.impacts.count, 1)
        XCTAssertTrue(BenchmarkReplay.matches(fixture))
        let summary = BenchmarkSummary(trials: [fixture])
        XCTAssertEqual(summary.recognition.total, 0)
        XCTAssertNil(summary.pipelineP95)
        XCTAssertEqual(summary.excludedCount, 1)
    }

    func testMissedAndDuplicateDetectionsStayInDenominator() {
        let summary = BenchmarkSummary(trials: [trial(), trial(impacts: [hit]), trial(impacts: [hit, hit])])
        XCTAssertEqual(summary.recognition.total, 3)
        XCTAssertEqual(summary.recognition.successes, 1)
        XCTAssertEqual(summary.duplicateTrials, 1)
        XCTAssertEqual(summary.direction.successes, 1)
        XCTAssertEqual(summary.direction.total, 3)
    }

    func testNegativeTrialsCountEveryAccidentalDetectionAndExposure() {
        let summary = BenchmarkSummary(trials: [trial(.nonSwing), trial(.nonSwing, impacts: [hit, hit])])
        XCTAssertEqual(summary.falseStrokes, 2)
        XCTAssertEqual(summary.negativeTrials, 2)
        XCTAssertEqual(summary.negativeSeconds, 24)
        XCTAssertEqual(summary.recognition.total, 0)
    }

    func testInterruptedAndEmptyTrialsAreExplicitlyExcluded() {
        var interrupted = trial(impacts: [hit])
        interrupted.interruption = "Background"
        var empty = trial()
        empty.frameCount = 0
        let summary = BenchmarkSummary(trials: [interrupted, empty, trial()])
        XCTAssertEqual(summary.excludedCount, 2)
        XCTAssertEqual(summary.recognition.total, 1)
        XCTAssertEqual(summary.recognition.successes, 0)
    }

    func testDirectionBoundariesAndWhiffs() {
        func impact(_ angle: Double, strike: StrikeQuality = .center) -> BenchmarkImpact {
            BenchmarkImpact(SwingImpact(power: 0.5, startLineDegrees: angle, strike: strike), time: 0, club: .driver)
        }
        XCTAssertTrue(BenchmarkChallenge.straight.accepts(impact(-5)))
        XCTAssertTrue(BenchmarkChallenge.straight.accepts(impact(5)))
        XCTAssertTrue(BenchmarkChallenge.left.accepts(impact(-6)))
        XCTAssertFalse(BenchmarkChallenge.left.accepts(impact(180)))
        XCTAssertFalse(BenchmarkChallenge.right.accepts(impact(-6)))
        XCTAssertFalse(BenchmarkChallenge.straight.accepts(impact(0, strike: .miss)))
    }

    func testNoSamplesNeverLooksLikePerfectAccuracy() {
        XCTAssertEqual(BenchmarkSummary(trials: []).recognition.label, "Not measured")
        XCTAssertNil(BenchmarkRate(successes: 0, total: 0).interval)
        let interval = BenchmarkRate(successes: 10, total: 10).interval!
        XCTAssertLessThan(interval.lowerBound, 0.8)
        XCTAssertEqual(interval.upperBound, 1, accuracy: 0.000001)
    }

    func testPipelineP95UsesNearestRankAndIgnoresInvalidNumbers() {
        XCTAssertEqual(BenchmarkSummary.percentile((1...100).map(Double.init) + [.nan, -.infinity], fraction: 0.95), 95)
        XCTAssertNil(BenchmarkSummary.percentile([], fraction: 0.95))
    }

    func testConsentOffRetainsMetricsButNoPoseCoordinates() throws {
        let lab = PracticeLab()
        lab.ingest(observation(time: 100))
        XCTAssertTrue(lab.begin(at: 100.02))
        lab.ingest(observation(time: 100.1, event: .impact(hit)))
        lab.tick(at: 113)
        let result = try XCTUnwrap(lab.trials.first)
        XCTAssertEqual(result.impacts.count, 1)
        XCTAssertTrue(result.poses.isEmpty)
        XCTAssertFalse(result.recordingConsented)
        XCTAssertEqual(result.pipelineMilliseconds[0], 15, accuracy: 0.00001)
        XCTAssertEqual(result.duration, 12)
    }

    func testConsentAndConfigurationAreFrozenForActiveTrial() throws {
        let lab = PracticeLab()
        lab.recordingConsent = true
        lab.challenge = .nonSwing
        lab.ingest(observation(time: 100))
        XCTAssertTrue(lab.begin(at: 100.02))
        lab.recordingConsent = false
        lab.challenge = .hard
        lab.ingest(observation(time: 100.1, missing: true))
        lab.tick(at: 113)
        let result = try XCTUnwrap(lab.trials.first)
        XCTAssertTrue(result.recordingConsented)
        XCTAssertEqual(result.challenge, .nonSwing)
        XCTAssertEqual(result.poses.count, 1)
        XCTAssertNil(result.poses.first?.frame)
        XCTAssertEqual(result.usableFrameCount, 0)
    }

    func testStaleTrackingCannotStartTrialAndInterruptionDoesNotCountAsMiss() {
        let lab = PracticeLab()
        XCTAssertFalse(lab.begin(at: 100))
        lab.ingest(observation(time: 100))
        XCTAssertFalse(lab.begin(at: 102))
        lab.ingest(observation(time: 103))
        XCTAssertTrue(lab.begin(at: 103.02))
        lab.ingest(observation(time: 103.1))
        lab.interrupt("Background", at: 104)
        XCTAssertEqual(lab.trials.count, 1)
        XCTAssertEqual(lab.summary.recognition.total, 0)
        lab.tick(at: 130)
        XCTAssertEqual(lab.trials.count, 1)
    }

    func testNoDetectionIsRecordedAfterFullWindowAndResetAllowsNextTrial() {
        let lab = PracticeLab()
        lab.ingest(observation(time: 100))
        XCTAssertTrue(lab.begin(at: 100.02))
        lab.ingest(observation(time: 101))
        lab.ingest(observation(time: 112.1, event: .impact(hit)))
        XCTAssertNil(lab.active)
        XCTAssertEqual(lab.trials.count, 1)
        XCTAssertTrue(lab.trials[0].impacts.isEmpty)
        XCTAssertEqual(lab.summary.recognition.total, 1)
        XCTAssertTrue(lab.begin(at: 112.2))
    }

    func testRecordReplayRoundTripsJSONAndDetectsChangedOutput() throws {
        let fixture = BenchmarkReplay.fixture()
        let report = BenchmarkReport(device: "test", os: "test", appVersion: "test", trials: [fixture])
        let decoder = JSONDecoder()
        decoder.dateDecodingStrategy = .iso8601
        let decoded = try decoder.decode(BenchmarkReport.self, from: report.encodedJSON())
        XCTAssertEqual(decoded.schemaVersion, 4)
        XCTAssertEqual(decoded.summary.recognition.total, 0)
        XCTAssertEqual(decoded.summary.excludedTrials, 1)
        XCTAssertEqual(decoded.trials[0].poses, fixture.poses)
        XCTAssertTrue(BenchmarkReplay.matches(decoded.trials[0]))
        var changed = fixture
        changed.impacts = []
        XCTAssertFalse(BenchmarkReplay.matches(changed))
        changed = fixture
        changed.recordingTruncated = true
        XCTAssertFalse(BenchmarkReplay.matches(changed))
    }

    func testLiveStyleTraceReplaysFromFreshDetectorAtRelativeTimestamps() throws {
        let lab = PracticeLab()
        lab.recordingConsent = true
        lab.ingest(observation(time: 100))
        XCTAssertTrue(lab.begin(at: 100.02))
        var detector = ArmSwingDetector()
        detector.configure(for: .driver)
        for pose in BenchmarkReplay.fixture().poses {
            let time = 100.1 + pose.time
            let frame = PoseFrame(timestamp: time, points: pose.frame!.points)
            let event = detector.ingest(ArmSwingDetector.Sample(frame: frame, frameAspect: pose.aspect), at: time)
            lab.ingest(CameraSwingObservation(delivery: PoseDelivery(frame: frame, captureTime: time,
                aspect: pose.aspect, bodyCount: 1, callbackStarted: time, inferenceFinished: time + 0.01),
                event: event, phase: "test", stateUpdated: time + 0.015))
        }
        lab.tick(at: 113)
        let recorded = try XCTUnwrap(lab.trials.first)
        XCTAssertEqual(recorded.impacts.count, 1)
        XCTAssertTrue(BenchmarkReplay.matches(recorded))
    }

    func testSessionLimitCanBeClearedAndDoesNotGrowUnbounded() {
        let lab = PracticeLab()
        for _ in 0..<60 { lab.addFixture() }
        XCTAssertEqual(lab.trials.count, PracticeLab.maximumTrials)
        XCTAssertTrue(lab.isFull)
        lab.clear()
        XCTAssertFalse(lab.isFull)
        lab.addFixture()
        XCTAssertEqual(lab.trials.count, 1)
    }
}
