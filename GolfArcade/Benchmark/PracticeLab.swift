import Foundation
import Observation

@MainActor @Observable
final class PracticeLab {
    static let trialDuration = 12.0
    static let maximumTrials = 50
    static let maximumFrames = 1_800
    var challenge: BenchmarkChallenge = .straight
    var handedness: Handedness = .right
    var participant = "P01"
    var conditions = ""
    var recordingConsent = false
    var poseMode: CameraPoseMode = .body2D
    var requestedCaptureFPS = 30
    var captureProfile: CameraCaptureProfile = .baseline
    var sourceVideoConsent = false
    private(set) var trials: [BenchmarkTrial] = []
    private(set) var displayLatencyMeasurements: [DisplayLatencyMeasurement] = []
    private(set) var active: BenchmarkTrial?
    private(set) var remaining = 0.0
    private(set) var livePhase = "Camera stopped"
    private(set) var confidence = 0.0
    private(set) var fps: Double?
    private(set) var pipelineMS: Double?
    private(set) var inferenceMS: Double?
    private(set) var lastImpact: BenchmarkImpact?
    @ObservationIgnored private(set) var lastUsableAt: Double?
    private var startedAt = 0.0
    private var previousCapture: Double?
    private var firstCapture: Double?
    private var lastLiveUpdate = 0.0
    private var recentCaptureTimes: [Double] = []
    private var attemptActive = false
    private var initialDroppedFrames: Int?

    var summary: BenchmarkSummary { BenchmarkSummary(trials: trials) }
    var isFull: Bool { trials.count >= Self.maximumTrials }
    var latestRecording: BenchmarkTrial? { trials.last { !$0.poses.isEmpty && !$0.recordingTruncated } }

    @discardableResult
    func begin(at time: Double) -> Bool {
        guard active == nil, !isFull, let lastUsableAt, time - lastUsableAt < 0.5 else { return false }
        active = BenchmarkTrial(id: UUID(), challenge: challenge, handedness: handedness,
            participant: String(participant.prefix(64)), conditions: String(conditions.prefix(240)),
            source: .camera, recordingConsented: recordingConsent)
        active?.diagnostics = BenchmarkDiagnostics(poseMode: poseMode, requestedCaptureFPS: requestedCaptureFPS)
        active?.diagnostics?.captureProfile = captureProfile
        active?.sourceVideoConsented = sourceVideoConsent
        attemptActive = false
        initialDroppedFrames = nil
        startedAt = time
        firstCapture = nil
        previousCapture = nil
        lastImpact = nil
        remaining = Self.trialDuration
        return true
    }

    func ingest(_ observation: CameraSwingObservation) {
        let delivery = observation.delivery
        let sample = delivery.frame.flatMap { ArmSwingDetector.Sample(frame: $0, frameAspect: delivery.aspect) }
        let usable = sample.map { $0.confidence >= 0.45 } ?? false
        if usable { lastUsableAt = observation.stateUpdated }
        // A rolling second of delivered poses, including missing-player results.
        recentCaptureTimes.append(delivery.captureTime)
        recentCaptureTimes = Array(recentCaptureTimes.filter { delivery.captureTime - $0 <= 1 }.suffix(120))
        if observation.stateUpdated - lastLiveUpdate >= 0.2 {
            lastLiveUpdate = observation.stateUpdated
            livePhase = observation.phase
            confidence = sample?.confidence ?? 0
            pipelineMS = observation.pipelineMS
            inferenceMS = observation.inferenceMS
            if let first = recentCaptureTimes.first, let last = recentCaptureTimes.last, last > first {
                fps = Double(recentCaptureTimes.count - 1) / (last - first)
            }
        }
        guard active != nil else { return }
        if let evidence = delivery.captureEvidence {
            if let previous = active?.diagnostics?.captureEvidence, previous.geometry != evidence.geometry {
                interrupt("Capture geometry changed", at: observation.stateUpdated)
                return
            }
            if active?.diagnostics?.captureEvidence == nil { active?.diagnostics?.captureEvidence = evidence }
            var states = active?.diagnostics?.thermalStates ?? [:]
            states[evidence.thermalState, default: 0] += 1
            active?.diagnostics?.thermalStates = states
        }
        guard observation.stateUpdated - startedAt < Self.trialDuration else {
            finish(at: startedAt + Self.trialDuration)
            return
        }
        guard previousCapture.map({ delivery.captureTime > $0 }) ?? true else { return }
        if let previousCapture {
            let maximumGap = max(active?.diagnostics?.maximumDeliveryGapMS ?? 0,
                                 (delivery.captureTime - previousCapture) * 1_000)
            active?.diagnostics?.maximumDeliveryGapMS = maximumGap
        }
        initialDroppedFrames = initialDroppedFrames ?? delivery.droppedFrames
        active?.diagnostics?.droppedFrames = max(0, delivery.droppedFrames - (initialDroppedFrames ?? 0))
        active?.diagnostics?.phaseFrames[observation.phase, default: 0] += 1
        if delivery.frame?.depth != nil { active?.diagnostics?.depthFrames += 1 }
        switch observation.event {
        case .load:
            if !attemptActive { active?.diagnostics?.attempts += 1; attemptActive = true }
        case .impact:
            if !attemptActive { active?.diagnostics?.attempts += 1 }
            attemptActive = false
        case .cancel:
            active?.diagnostics?.cancelCount += 1
            if observation.readiness == "retry" { active?.diagnostics?.retryCount += 1 }
            attemptActive = false
        case nil: break
        }
        previousCapture = delivery.captureTime
        firstCapture = firstCapture ?? delivery.captureTime
        active?.firstCapturePTS = firstCapture
        let time = delivery.captureTime - firstCapture!
        active?.frameCount += 1
        if let readiness = observation.readiness {
            active?.readinessFrames[readiness, default: 0] += 1
        }
        if usable { active?.usableFrameCount += 1 }
        if delivery.bodyCount > 1 { active?.multipleBodyFrameCount += 1 }
        if (active?.pipelineMilliseconds.count ?? 0) < Self.maximumFrames {
            active?.pipelineMilliseconds.append(observation.pipelineMS)
            active?.inferenceMilliseconds.append(observation.inferenceMS)
        }
        if active?.recordingConsented == true {
            if (active?.poses.count ?? 0) < Self.maximumFrames {
                active?.poses.append(BenchmarkPose(frame: delivery.frame, time: time, aspect: delivery.aspect))
            } else {
                active?.recordingTruncated = true
            }
        }
        if case .impact(let impact) = observation.event, let active {
            let result = BenchmarkImpact(impact, time: time, club: active.challenge.club)
            self.active?.impacts.append(result)
            lastImpact = result
        }
    }

    func tick(at time: Double) {
        if let lastUsableAt, time - lastUsableAt > 1 {
            confidence = 0
            livePhase = "Tracking stale / unavailable"
            fps = nil
            pipelineMS = nil
            inferenceMS = nil
        }
        guard active != nil else { return }
        remaining = max(0, Self.trialDuration - (time - startedAt))
        if remaining == 0 { finish(at: startedAt + Self.trialDuration) }
    }

    func interrupt(_ reason: String, at time: Double) {
        guard active != nil else { return }
        active?.interruption = reason
        finish(at: time)
    }

    private func finish(at time: Double) {
        guard var result = active else { return }
        result.duration = max(0, min(Self.trialDuration, time - startedAt))
        trials.append(result)
        active = nil
        remaining = 0
    }

    func cameraStopped() {
        lastUsableAt = nil
        confidence = 0
        fps = nil
        pipelineMS = nil
        inferenceMS = nil
        livePhase = "Camera stopped"
        recentCaptureTimes = []
    }

    func addFixture() {
        guard active == nil, !isFull else { return }
        trials.append(BenchmarkReplay.fixture())
    }

    func clear() {
        guard active == nil else { return }
        trials = []
        lastImpact = nil
        displayLatencyMeasurements = []
    }

    @discardableResult
    func addDisplayLatency(_ measurement: DisplayLatencyMeasurement) -> Bool {
        guard active == nil, measurement.isValid, displayLatencyMeasurements.count < 200 else { return false }
        displayLatencyMeasurements.append(measurement)
        return true
    }
}
