import CoreGraphics
import Foundation

enum BenchmarkChallenge: String, CaseIterable, Codable, Identifiable {
    case straight, left, right, gentle, medium, hard, puttThreeFeet, puttNineFeet, nonSwing
    case pausedSwing, abortedTakeaway, briefOcclusion
    var id: Self { self }
    var isNegative: Bool { self == .nonSwing || self == .abortedTakeaway }
    var title: String {
        switch self {
        case .straight: "Straight shot"
        case .left: "Start left"
        case .right: "Start right"
        case .gentle: "Gentle swing"
        case .medium: "Medium swing"
        case .hard: "Firm swing"
        case .puttThreeFeet: "Putt 3 feet"
        case .puttNineFeet: "Putt 9 feet"
        case .nonSwing: "No swing / everyday movement"
        case .pausedSwing: "Pause at the top"
        case .abortedTakeaway: "Take back, then cancel"
        case .briefOcclusion: "Briefly hidden grip"
        }
    }
    var instruction: String {
        switch self {
        case .nonSwing: "Do not swing. Reposition, scratch your head or make small waggles. Any detected stroke counts as accidental."
        case .pausedSwing: "Address, take the club back, hold for four seconds, then complete ONE controlled swing."
        case .abortedTakeaway: "Address, take your hands back, then slowly lower them to rest. Do not swing through. No shot should launch."
        case .briefOcclusion: "Make ONE controlled swing with a natural brief grip occlusion near the top. Keep your body in frame; do not cover the camera."
        case .puttThreeFeet, .puttNineFeet: "Hold your hands together and still to address the ball, then make ONE gentle putting stroke."
        default: "Hold your hands together and still to address the ball, then make ONE controlled swing. Stay in frame until the timer ends."
        }
    }
    var club: GolfClub { targetFeet == nil ? .driver : .putter }
    var targetFeet: Double? {
        switch self { case .puttThreeFeet: 3; case .puttNineFeet: 9; default: nil }
    }
    var isDirection: Bool { self == .left || self == .straight || self == .right }
    var isPower: Bool { self == .gentle || self == .medium || self == .hard }
    func accepts(_ shot: BenchmarkImpact) -> Bool {
        guard shot.strike != "miss", shot.confidence >= 0.45 else { return false }
        switch self {
        case .straight: return abs(shot.startLineDegrees) <= 5
        case .left: return (-45 ..< -5).contains(shot.startLineDegrees)
        case .right: return shot.startLineDegrees > 5 && shot.startLineDegrees <= 45
        case .gentle: return shot.power > 0 && shot.power < 0.35
        case .medium: return (0.35 ..< 0.7).contains(shot.power)
        case .hard: return shot.power >= 0.7
        case .puttThreeFeet, .puttNineFeet:
            return abs(shot.distanceYards * 3 - targetFeet!) <= targetFeet! * 0.25
        case .pausedSwing, .briefOcclusion: return true
        case .nonSwing, .abortedTakeaway: return false
        }
    }
}

struct BenchmarkImpact: Codable, Equatable {
    let time: Double
    let power: Double
    let startLineDegrees: Double
    let strike: String
    let confidence: Double
    let distanceYards: Double

    init(_ impact: SwingImpact, time: Double, club: GolfClub) {
        self.time = time
        power = impact.power
        startLineDegrees = impact.startLineDegrees
        strike = impact.strike.rawValue
        confidence = impact.confidence
        distanceYards = RangeShot(id: 0, club: club, power: impact.power,
            aim: impact.startLineDegrees, strike: impact.strike).total
    }
}

/// Lossless detector inputs, not images. Missing poses remain explicit samples in the trace.
struct BenchmarkPose: Codable, Equatable {
    struct Joint: Codable, Equatable {
        let name: String
        let x: Double
        let y: Double
        let confidence: Float
    }
    let time: Double
    let aspect: Double
    let joints: [Joint]?
    let depth: BodyDepthEstimate?
    /// Stance line on the frames the 3D pose ran; absent in recordings made before it existed.
    var orientation: BodyOrientation?

    init(frame: PoseFrame?, time: Double, aspect: Double) {
        self.time = time
        self.aspect = aspect
        depth = frame.flatMap { frame in
            frame.depth.map { BodyDepthEstimate(timestamp: time - (frame.timestamp - $0.timestamp),
                normalizedDepth: $0.normalizedDepth) }
        }
        orientation = frame.flatMap { frame in
            frame.orientation.map { BodyOrientation(timestamp: time - (frame.timestamp - $0.timestamp),
                shoulderYaw: $0.shoulderYaw, hipYaw: $0.hipYaw) }
        }
        joints = frame.map { frame in
            BodyJoint.allCases.compactMap { joint in
                frame.points[joint].map { Joint(name: joint.rawValue, x: $0.location.x,
                    y: $0.location.y, confidence: $0.confidence) }
            }
        }
    }

    var frame: PoseFrame? {
        guard let joints else { return nil }
        var points: [BodyJoint: PosePoint] = [:]
        for joint in joints {
            if let key = BodyJoint(rawValue: joint.name) {
                points[key] = PosePoint(location: CGPoint(x: joint.x, y: joint.y), confidence: joint.confidence)
            }
        }
        var frame = PoseFrame(timestamp: time, points: points, depth: depth)
        frame.orientation = orientation
        return frame
    }
}

struct BenchmarkTrial: Codable, Identifiable {
    enum Source: String, Codable { case camera, synthetic }
    let id: UUID
    let challenge: BenchmarkChallenge
    let handedness: Handedness
    let participant: String
    let conditions: String
    let source: Source
    let recordingConsented: Bool
    var duration = 0.0
    var interruption: String?
    var frameCount = 0
    var usableFrameCount = 0
    var multipleBodyFrameCount = 0
    var readinessFrames: [String: Int] = [:]
    var pipelineMilliseconds: [Double] = []
    var inferenceMilliseconds: [Double] = []
    var impacts: [BenchmarkImpact] = []
    var poses: [BenchmarkPose] = []
    var recordingTruncated = false
    var diagnostics: BenchmarkDiagnostics?
    /// Optional for compatibility with schema 1–3. Links relative poses to source movie PTS.
    var firstCapturePTS: Double?
    var sourceVideoConsented: Bool?

    var eligible: Bool { source == .camera && interruption == nil && frameCount > 0 }
    var succeeded: Bool {
        if challenge.isNegative { return impacts.isEmpty }
        return impacts.count == 1 && challenge.accepts(impacts[0])
    }
    var outcome: String {
        if let interruption { return "Interrupted: \(interruption)" }
        if frameCount == 0 { return "No camera frames — not scored" }
        if source == .synthetic { return "Synthetic fixture — excluded from camera benchmarks" }
        if challenge.isNegative { return impacts.isEmpty ? "No accidental strokes" : "\(impacts.count) accidental strokes" }
        if impacts.isEmpty && (diagnostics?.retryCount ?? 0) > 0 { return "Tracking/contact retry — no shot scored" }
        if impacts.isEmpty { return "Missed detection" }
        if impacts.count > 1 { return "\(impacts.count) detections — expected one" }
        return succeeded ? "Target reached" : "Detected, outside target"
    }
}

struct BenchmarkDiagnostics: Codable {
    var poseMode: CameraPoseMode = .body2D
    var requestedCaptureFPS = 30
    var attempts = 0
    var cancelCount = 0
    var retryCount = 0
    var droppedFrames = 0
    var maximumDeliveryGapMS = 0.0
    var depthFrames = 0
    var phaseFrames: [String: Int] = [:]
    var captureEvidence: CaptureEvidence?
    var thermalStates: [String: Int]?
    var captureProfile: CameraCaptureProfile?
}

struct BenchmarkRate: Codable {
    let successes: Int
    let total: Int
    var label: String { total == 0 ? "Not measured" : "\(successes)/\(total) (\(Int((Double(successes) / Double(total) * 100).rounded()))%)" }
    /// Wilson interval. Descriptive trial-level uncertainty; repeated trials aren't independent people.
    var interval: ClosedRange<Double>? {
        guard total > 0 else { return nil }
        let n = Double(total), p = Double(successes) / n, z = 1.96
        let denominator = 1 + z * z / n
        let center = (p + z * z / (2 * n)) / denominator
        let radius = z * sqrt(p * (1 - p) / n + z * z / (4 * n * n)) / denominator
        return max(0, center - radius)...min(1, center + radius)
    }
}

struct BenchmarkSummary {
    let trials: [BenchmarkTrial]
    var cameraTrials: [BenchmarkTrial] { trials.filter(\.eligible) }
    var excludedCount: Int { trials.count - cameraTrials.count }
    var recognition: BenchmarkRate {
        let swings = cameraTrials.filter { !$0.challenge.isNegative }
        return BenchmarkRate(successes: swings.filter {
            $0.impacts.count == 1 && ($0.diagnostics?.retryCount ?? 0) == 0 && ($0.diagnostics?.attempts ?? 1) <= 1
        }.count, total: swings.count)
    }
    var direction: BenchmarkRate { rate { $0.challenge.isDirection } }
    var power: BenchmarkRate { rate { $0.challenge.isPower } }
    var putting: BenchmarkRate { rate { $0.challenge.targetFeet != nil } }
    var falseStrokes: Int { negatives.reduce(0) { $0 + $1.impacts.count } }
    var negativeTrials: Int { negatives.count }
    var negativeSeconds: Double { negatives.reduce(0) { $0 + $1.duration } }
    var retries: Int { cameraTrials.reduce(0) { $0 + ($1.diagnostics?.retryCount ?? 0) } }
    var duplicateTrials: Int { cameraTrials.filter { !$0.challenge.isNegative && $0.impacts.count > 1 }.count }
    var pipelineP95: Double? { Self.percentile(cameraTrials.flatMap(\.pipelineMilliseconds), fraction: 0.95) }
    private var negatives: [BenchmarkTrial] { cameraTrials.filter { $0.challenge.isNegative } }
    private func rate(_ filter: (BenchmarkTrial) -> Bool) -> BenchmarkRate {
        let subset = cameraTrials.filter(filter)
        return BenchmarkRate(successes: subset.filter(\.succeeded).count, total: subset.count)
    }
    static func percentile(_ values: [Double], fraction: Double) -> Double? {
        let sorted = values.filter { $0.isFinite && $0 >= 0 }.sorted()
        guard !sorted.isEmpty else { return nil }
        let index = max(0, min(sorted.count - 1, Int(ceil(Double(sorted.count) * fraction)) - 1))
        return sorted[index]
    }
}

enum BenchmarkReplay {
    static func run(_ trial: BenchmarkTrial) -> [BenchmarkImpact] {
        var detector = ArmSwingDetector()
        detector.handedness = trial.handedness
        detector.configure(for: trial.challenge.club)
        return trial.poses.compactMap { pose in
            let sample = pose.frame.flatMap { ArmSwingDetector.Sample(frame: $0, frameAspect: pose.aspect, certifiedSpace: detector.certifiedSpace) }
            guard case .impact(let impact) = detector.ingest(sample, at: pose.time) else { return nil }
            return BenchmarkImpact(impact, time: pose.time, club: trial.challenge.club)
        }
    }

    static func matches(_ trial: BenchmarkTrial) -> Bool {
        guard !trial.poses.isEmpty, !trial.recordingTruncated else { return false }
        let replayed = run(trial)
        return replayed.count == trial.impacts.count && zip(replayed, trial.impacts).allSatisfy {
            abs($0.time - $1.time) < 0.00001 && abs($0.power - $1.power) < 0.00001 &&
            abs($0.startLineDegrees - $1.startLineDegrees) < 0.00001 && $0.strike == $1.strike &&
            abs($0.confidence - $1.confidence) < 0.00001 && abs($0.distanceYards - $1.distanceYards) < 0.00001
        }
    }

    /// Software smoke test only. It never contributes to camera accuracy or timing statistics.
    static func fixture() -> BenchmarkTrial {
        var trial = BenchmarkTrial(id: UUID(), challenge: .straight, handedness: .right,
            participant: "synthetic", conditions: "30 fps generated pose arc", source: .synthetic,
            recordingConsented: false)
        var time = 0.0, angle = 0.0
        for (duration, target) in [(0.5, 0.0), (0.8, 140.0), (0.15, 140.0), (0.2, -30.0), (0.5, 0.0)] {
            let steps = max(1, Int((duration * 30).rounded()))
            let delta = (target - angle) / Double(steps)
            for _ in 0..<steps {
                time += 1 / 30
                angle += delta
                let radians = angle * .pi / 180
                let wrist = CGPoint(x: 0.5 + sin(radians) * 0.32, y: 0.7 - cos(radians) * 0.32)
                let points: [BodyJoint: PosePoint] = [
                    .nose: PosePoint(location: CGPoint(x: 0.5, y: 0.83), confidence: 1),
                    .neck: PosePoint(location: CGPoint(x: 0.5, y: 0.72), confidence: 1),
                    .leftShoulder: PosePoint(location: CGPoint(x: 0.4, y: 0.7), confidence: 1),
                    .rightShoulder: PosePoint(location: CGPoint(x: 0.6, y: 0.7), confidence: 1),
                    .leftElbow: PosePoint(location: CGPoint(x: (0.4 + wrist.x) / 2, y: (0.7 + wrist.y) / 2), confidence: 1),
                    .rightElbow: PosePoint(location: CGPoint(x: (0.6 + wrist.x) / 2, y: (0.7 + wrist.y) / 2), confidence: 1),
                    .leftWrist: PosePoint(location: wrist, confidence: 1),
                    .rightWrist: PosePoint(location: wrist, confidence: 1),
                    .root: PosePoint(location: CGPoint(x: 0.5, y: 0.45), confidence: 1),
                    .leftHip: PosePoint(location: CGPoint(x: 0.43, y: 0.45), confidence: 1),
                    .rightHip: PosePoint(location: CGPoint(x: 0.57, y: 0.45), confidence: 1),
                    .leftKnee: PosePoint(location: CGPoint(x: 0.41, y: 0.28), confidence: 1),
                    .rightKnee: PosePoint(location: CGPoint(x: 0.59, y: 0.28), confidence: 1),
                    .leftAnkle: PosePoint(location: CGPoint(x: 0.4, y: 0.1), confidence: 1),
                    .rightAnkle: PosePoint(location: CGPoint(x: 0.6, y: 0.1), confidence: 1)
                ]
                trial.poses.append(BenchmarkPose(frame: PoseFrame(timestamp: time, points: points), time: time, aspect: 1))
            }
        }
        trial.duration = time
        trial.frameCount = trial.poses.count
        trial.usableFrameCount = trial.frameCount
        trial.impacts = run(trial)
        return trial
    }
}

struct BenchmarkReport: Codable {
    var schemaVersion = 4
    var protocolVersion = "practice-lab-v3-capture-evidence"
    var detectorVersion = "arm-swing-2d-v4-arcade-contact"
    var createdAt = Date()
    let device: String
    let os: String
    let appVersion: String
    let trials: [BenchmarkTrial]
    let summary: Snapshot
    var cameraInventory: [CaptureDeviceCapability]?
    var displayLatencyMeasurements: [DisplayLatencyMeasurement]?
    var limitations = [
        "Prompt labels are player intent, not independently verified motion ground truth.",
        "Pipeline timing is capture-callback to detector state, not motion-to-display latency.",
        "Camera results only; synthetic fixtures and interrupted trials are excluded from rates.",
        "Simulated start line and distance are not physical clubface, launch or spin measurements.",
        "Repeated trials are clustered by participant; a small session cannot certify release gates."
    ]

    struct Snapshot: Codable {
        let recognition: BenchmarkRate
        let direction: BenchmarkRate
        let power: BenchmarkRate
        let putting: BenchmarkRate
        let accidentalStrokes: Int
        let nonSwingTrials: Int
        let nonSwingSeconds: Double
        let excludedTrials: Int
        let pipelineP95Milliseconds: Double?
        let trackingRetries: Int?
        let duplicateTrials: Int?
    }

    init(device: String, os: String, appVersion: String, trials: [BenchmarkTrial], cameraInventory: [CaptureDeviceCapability]? = nil,
         displayLatencyMeasurements: [DisplayLatencyMeasurement]? = nil) {
        self.device = device
        self.os = os
        self.appVersion = appVersion
        self.trials = trials
        self.cameraInventory = cameraInventory
        self.displayLatencyMeasurements = displayLatencyMeasurements
        let metrics = BenchmarkSummary(trials: trials)
        summary = Snapshot(recognition: metrics.recognition, direction: metrics.direction,
            power: metrics.power, putting: metrics.putting, accidentalStrokes: metrics.falseStrokes,
            nonSwingTrials: metrics.negativeTrials, nonSwingSeconds: metrics.negativeSeconds,
            excludedTrials: metrics.excludedCount, pipelineP95Milliseconds: metrics.pipelineP95,
            trackingRetries: metrics.retries, duplicateTrials: metrics.duplicateTrials)
    }

    func encodedJSON() throws -> Data {
        let encoder = JSONEncoder()
        encoder.outputFormatting = [.prettyPrinted, .sortedKeys]
        encoder.dateEncodingStrategy = .iso8601
        return try encoder.encode(self)
    }
}
