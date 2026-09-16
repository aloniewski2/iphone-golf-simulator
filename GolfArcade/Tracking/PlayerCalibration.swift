import CoreGraphics
import Foundation

struct PlayerCalibration: Codable, Equatable, Sendable {
    static let currentVersion = 2

    let version: Int
    let capturedAt: Date
    let signature: BodySignature
    let anchor: BodyAnchor

    init(signature: BodySignature, anchor: BodyAnchor, capturedAt: Date = Date()) {
        version = Self.currentVersion
        self.capturedAt = capturedAt
        self.signature = signature
        self.anchor = anchor
    }

    #if DEBUG
    static let uiTestingFixture = PlayerCalibration(
        signature: BodySignature(
            shoulderWidth: 0.22, hipWidth: 0.16, torsoLength: 0.30,
            upperArmLength: 0.20, forearmLength: 0.18, thighLength: 0.28, shinLength: 0.27
        ),
        anchor: BodyAnchor(shoulderCenterX: 0.5, shoulderCenterY: 0.72, height: 0.72)
    )
    #endif

    func matchScore(for frame: PoseFrame) -> Double? {
        guard let proportionDistance = signature.distance(to: frame),
              let shoulderCenter = frame.shoulderCenter,
              let shoulderWidth = frame.shoulderWidth else { return nil }

        let centerDistance = hypot(
            Double(shoulderCenter.x) - anchor.shoulderCenterX,
            Double(shoulderCenter.y) - anchor.shoulderCenterY
        )
        let expectedShoulderWidth = signature.shoulderWidth * anchor.height
        let scaleDistance = expectedShoulderWidth > 0
            ? abs(Double(shoulderWidth) - expectedShoulderWidth) / expectedShoulderWidth
            : 1

        // Proportions identify the player. Screen position and apparent scale are softer signals,
        // allowing natural movement without letting a background pose take over.
        return proportionDistance + centerDistance * 0.28 + scaleDistance * 0.08
    }
}

struct BodySignature: Codable, Equatable, Sendable {
    let shoulderWidth: Double
    let hipWidth: Double
    let torsoLength: Double
    let upperArmLength: Double
    let forearmLength: Double
    let thighLength: Double
    let shinLength: Double

    init(
        shoulderWidth: Double, hipWidth: Double, torsoLength: Double,
        upperArmLength: Double, forearmLength: Double, thighLength: Double, shinLength: Double
    ) {
        self.shoulderWidth = shoulderWidth
        self.hipWidth = hipWidth
        self.torsoLength = torsoLength
        self.upperArmLength = upperArmLength
        self.forearmLength = forearmLength
        self.thighLength = thighLength
        self.shinLength = shinLength
    }

    init?(frame: PoseFrame) {
        guard let bounds = frame.calibrationBounds, bounds.height > 0,
              let leftShoulder = frame.point(.leftShoulder, minimumConfidence: 0.35),
              let rightShoulder = frame.point(.rightShoulder, minimumConfidence: 0.35),
              let leftHip = frame.point(.leftHip, minimumConfidence: 0.35),
              let rightHip = frame.point(.rightHip, minimumConfidence: 0.35),
              let arms = Self.averageSegment(in: frame, pairs: [(.leftShoulder, .leftElbow), (.rightShoulder, .rightElbow)]),
              let forearms = Self.averageSegment(in: frame, pairs: [(.leftElbow, .leftWrist), (.rightElbow, .rightWrist)]),
              let thighs = Self.averageSegment(in: frame, pairs: [(.leftHip, .leftKnee), (.rightHip, .rightKnee)]),
              let shins = Self.averageSegment(in: frame, pairs: [(.leftKnee, .leftAnkle), (.rightKnee, .rightAnkle)]) else { return nil }

        let scale = Double(bounds.height)
        shoulderWidth = pointDistance(leftShoulder, rightShoulder) / scale
        hipWidth = pointDistance(leftHip, rightHip) / scale
        torsoLength = pointDistance(midpoint(leftShoulder, rightShoulder), midpoint(leftHip, rightHip)) / scale
        upperArmLength = arms / scale
        forearmLength = forearms / scale
        thighLength = thighs / scale
        shinLength = shins / scale
    }

    private static func averageSegment(in frame: PoseFrame, pairs: [(BodyJoint, BodyJoint)]) -> Double? {
        let lengths = pairs.compactMap { start, end -> Double? in
            guard let a = frame.point(start, minimumConfidence: 0.35),
                  let b = frame.point(end, minimumConfidence: 0.35) else { return nil }
            return pointDistance(a, b)
        }
        guard !lengths.isEmpty else { return nil }
        return lengths.reduce(0, +) / Double(lengths.count)
    }

    init(averaging signatures: [BodySignature]) {
        func mean(_ keyPath: KeyPath<BodySignature, Double>) -> Double {
            signatures.map { $0[keyPath: keyPath] }.reduce(0, +) / Double(signatures.count)
        }
        shoulderWidth = mean(\.shoulderWidth)
        hipWidth = mean(\.hipWidth)
        torsoLength = mean(\.torsoLength)
        upperArmLength = mean(\.upperArmLength)
        forearmLength = mean(\.forearmLength)
        thighLength = mean(\.thighLength)
        shinLength = mean(\.shinLength)
    }

    func distance(to frame: PoseFrame) -> Double? {
        guard let leftShoulder = frame.point(.leftShoulder, minimumConfidence: 0.35),
              let rightShoulder = frame.point(.rightShoulder, minimumConfidence: 0.35) else { return nil }

        let currentShoulderWidth = pointDistance(leftShoulder, rightShoulder)
        var estimatedHeight = currentShoulderWidth / max(shoulderWidth, 0.001)
        var differences: [Double] = []

        if let leftHip = frame.point(.leftHip, minimumConfidence: 0.35),
           let rightHip = frame.point(.rightHip, minimumConfidence: 0.35) {
            let currentTorso = pointDistance(midpoint(leftShoulder, rightShoulder), midpoint(leftHip, rightHip))
            if currentTorso > 0 { estimatedHeight = currentTorso / max(torsoLength, 0.001) }
            differences.append(abs(pointDistance(leftHip, rightHip) / estimatedHeight - hipWidth))
        }

        differences.append(abs(currentShoulderWidth / estimatedHeight - shoulderWidth))
        appendAverageSegmentDifference(
            from: .leftShoulder, to: .leftElbow, and: .rightShoulder, to: .rightElbow,
            expected: upperArmLength, estimatedHeight: estimatedHeight, frame: frame, into: &differences
        )
        appendAverageSegmentDifference(
            from: .leftElbow, to: .leftWrist, and: .rightElbow, to: .rightWrist,
            expected: forearmLength, estimatedHeight: estimatedHeight, frame: frame, into: &differences
        )
        appendAverageSegmentDifference(
            from: .leftHip, to: .leftKnee, and: .rightHip, to: .rightKnee,
            expected: thighLength, estimatedHeight: estimatedHeight, frame: frame, into: &differences
        )
        appendAverageSegmentDifference(
            from: .leftKnee, to: .leftAnkle, and: .rightKnee, to: .rightAnkle,
            expected: shinLength, estimatedHeight: estimatedHeight, frame: frame, into: &differences
        )

        // Shoulder scale plus at least two independently observed body segments.
        guard differences.count >= 3 else { return nil }
        return differences.reduce(0, +) / Double(differences.count)
    }

    private func appendAverageSegmentDifference(
        from leftStart: BodyJoint, to leftEnd: BodyJoint,
        and rightStart: BodyJoint, to rightEnd: BodyJoint,
        expected: Double, estimatedHeight: Double, frame: PoseFrame, into differences: inout [Double]
    ) {
        let segments = [(leftStart, leftEnd), (rightStart, rightEnd)].compactMap { start, end -> Double? in
            guard let a = frame.point(start, minimumConfidence: 0.35),
                  let b = frame.point(end, minimumConfidence: 0.35) else { return nil }
            return pointDistance(a, b)
        }
        guard !segments.isEmpty else { return }
        differences.append(abs(segments.reduce(0, +) / Double(segments.count) / estimatedHeight - expected))
    }
}

struct BodyAnchor: Codable, Equatable, Sendable {
    let shoulderCenterX: Double
    let shoulderCenterY: Double
    let height: Double

    init(shoulderCenterX: Double, shoulderCenterY: Double, height: Double) {
        self.shoulderCenterX = shoulderCenterX
        self.shoulderCenterY = shoulderCenterY
        self.height = height
    }

    init(frame: PoseFrame, bounds: CGRect) {
        shoulderCenterX = Double(frame.shoulderCenter?.x ?? bounds.midX)
        shoulderCenterY = Double(frame.shoulderCenter?.y ?? bounds.midY)
        height = Double(bounds.height)
    }

    init(averaging anchors: [BodyAnchor]) {
        shoulderCenterX = anchors.map(\.shoulderCenterX).reduce(0, +) / Double(anchors.count)
        shoulderCenterY = anchors.map(\.shoulderCenterY).reduce(0, +) / Double(anchors.count)
        height = anchors.map(\.height).reduce(0, +) / Double(anchors.count)
    }
}

enum CalibrationAssessment: Equatable, Sendable {
    case ready, noBody, multiplePeople, incompleteBody, moveCloser, moveTowardCenter, armsAwayFromBody, holdStill

    var instruction: String {
        switch self {
        case .ready: "Hold still — scanning your proportions"
        case .noBody: "Step into the guide so your full body is visible"
        case .multiplePeople: "Only the player should be in the camera frame"
        case .incompleteBody: "Keep your face and at least one hand and foot visible"
        case .moveCloser: "Move closer so your body fills the guide"
        case .moveTowardCenter: "Step into the center of the guide"
        case .armsAwayFromBody: "Face forward with your arms slightly away from your sides"
        case .holdStill: "Hold still for just a moment"
        }
    }
}

struct CalibrationAccumulator: Sendable {
    static let requiredSampleCount = 30

    private(set) var assessment: CalibrationAssessment = .incompleteBody
    private(set) var sampleCount = 0
    private var signatures: [BodySignature] = []
    private var anchors: [BodyAnchor] = []
    private var previousRoot: CGPoint?
    private var lastAcceptedTimestamp: TimeInterval?

    var progress: Double { min(Double(sampleCount) / Double(Self.requiredSampleCount), 1) }

    mutating func ingest(_ frame: PoseFrame, detectedBodyCount: Int = 1) -> PlayerCalibration? {
        guard detectedBodyCount == 1 else {
            reject(detectedBodyCount > 1 ? .multiplePeople : .noBody, at: frame.timestamp)
            return nil
        }
        guard let signature = BodySignature(frame: frame), let bounds = frame.calibrationBounds,
              frame.hasCalibrationBody else {
            reject(.incompleteBody, at: frame.timestamp)
            return nil
        }
        guard bounds.height >= 0.42 else {
            reject(.moveCloser, at: frame.timestamp)
            return nil
        }
        guard (0.22...0.78).contains(bounds.midX), (0.28...0.72).contains(bounds.midY) else {
            reject(.moveTowardCenter, at: frame.timestamp)
            return nil
        }
        guard frame.hasOpenCalibrationPose else {
            reject(.armsAwayFromBody, at: frame.timestamp)
            return nil
        }
        if let root = frame.stabilityCenter,
           let previousRoot, pointDistance(root, previousRoot) > 0.045 {
            self.previousRoot = root
            reject(.holdStill, at: frame.timestamp)
            return nil
        }

        previousRoot = frame.stabilityCenter
        lastAcceptedTimestamp = frame.timestamp
        assessment = .ready
        signatures.append(signature)
        anchors.append(BodyAnchor(frame: frame, bounds: bounds))
        sampleCount = signatures.count

        guard sampleCount >= Self.requiredSampleCount else { return nil }
        return PlayerCalibration(
            signature: BodySignature(averaging: signatures),
            anchor: BodyAnchor(averaging: anchors)
        )
    }

    mutating func reset() {
        assessment = .incompleteBody
        sampleCount = 0
        signatures.removeAll(keepingCapacity: true)
        anchors.removeAll(keepingCapacity: true)
        previousRoot = nil
        lastAcceptedTimestamp = nil
    }

    mutating func reportTracking(bodyCount: Int, timestamp: TimeInterval? = nil) {
        guard bodyCount != 1 else { return }
        assessment = bodyCount > 1 ? .multiplePeople : .noBody
        if let timestamp { expireSamplesIfNeeded(at: timestamp) }
    }

    private mutating func reject(_ reason: CalibrationAssessment, at timestamp: TimeInterval) {
        assessment = reason
        // A blink, occluded ankle, or one noisy Vision frame must not erase a nearly finished scan.
        // If the player has truly left for two seconds, start a fresh reference instead of mixing people.
        expireSamplesIfNeeded(at: timestamp)
    }

    private mutating func expireSamplesIfNeeded(at timestamp: TimeInterval) {
        if let lastAcceptedTimestamp, timestamp - lastAcceptedTimestamp > 2.0 {
            signatures.removeAll(keepingCapacity: true)
            anchors.removeAll(keepingCapacity: true)
            previousRoot = nil
            self.lastAcceptedTimestamp = nil
        }
        sampleCount = signatures.count
    }
}

enum PlayerCalibrationStore {
    private static let key = "playerBodyCalibration.v2"

    static func load(defaults: UserDefaults = .standard) -> PlayerCalibration? {
        guard let data = defaults.data(forKey: key),
              let calibration = try? JSONDecoder().decode(PlayerCalibration.self, from: data),
              calibration.version == PlayerCalibration.currentVersion else { return nil }
        return calibration
    }

    static func save(_ calibration: PlayerCalibration, defaults: UserDefaults = .standard) {
        guard let data = try? JSONEncoder().encode(calibration) else { return }
        defaults.set(data, forKey: key)
    }

    static func clear(defaults: UserDefaults = .standard) {
        defaults.removeObject(forKey: key)
    }
}

private func pointDistance(_ first: CGPoint, _ second: CGPoint) -> Double {
    hypot(Double(second.x - first.x), Double(second.y - first.y))
}

private func midpoint(_ first: CGPoint, _ second: CGPoint) -> CGPoint {
    CGPoint(x: (first.x + second.x) / 2, y: (first.y + second.y) / 2)
}
