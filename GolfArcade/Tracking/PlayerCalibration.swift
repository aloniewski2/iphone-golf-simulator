import CoreGraphics
import Foundation

struct PlayerCalibration: Codable, Equatable, Sendable {
    static let currentVersion = 1

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
        guard let bounds = frame.bodyBounds, bounds.height > 0,
              let leftShoulder = frame.point(.leftShoulder, minimumConfidence: 0.45),
              let rightShoulder = frame.point(.rightShoulder, minimumConfidence: 0.45),
              let leftElbow = frame.point(.leftElbow, minimumConfidence: 0.45),
              let rightElbow = frame.point(.rightElbow, minimumConfidence: 0.45),
              let leftWrist = frame.point(.leftWrist, minimumConfidence: 0.45),
              let rightWrist = frame.point(.rightWrist, minimumConfidence: 0.45),
              let leftHip = frame.point(.leftHip, minimumConfidence: 0.45),
              let rightHip = frame.point(.rightHip, minimumConfidence: 0.45),
              let leftKnee = frame.point(.leftKnee, minimumConfidence: 0.45),
              let rightKnee = frame.point(.rightKnee, minimumConfidence: 0.45),
              let leftAnkle = frame.point(.leftAnkle, minimumConfidence: 0.45),
              let rightAnkle = frame.point(.rightAnkle, minimumConfidence: 0.45) else { return nil }

        let scale = Double(bounds.height)
        shoulderWidth = pointDistance(leftShoulder, rightShoulder) / scale
        hipWidth = pointDistance(leftHip, rightHip) / scale
        torsoLength = pointDistance(midpoint(leftShoulder, rightShoulder), midpoint(leftHip, rightHip)) / scale
        upperArmLength = average(pointDistance(leftShoulder, leftElbow), pointDistance(rightShoulder, rightElbow)) / scale
        forearmLength = average(pointDistance(leftElbow, leftWrist), pointDistance(rightElbow, rightWrist)) / scale
        thighLength = average(pointDistance(leftHip, leftKnee), pointDistance(rightHip, rightKnee)) / scale
        shinLength = average(pointDistance(leftKnee, leftAnkle), pointDistance(rightKnee, rightAnkle)) / scale
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
    case ready, incompleteBody, moveCloser, moveTowardCenter, armsAwayFromBody, holdStill

    var instruction: String {
        switch self {
        case .ready: "Hold still — scanning your proportions"
        case .incompleteBody: "Fit your head, hands, and feet inside the frame"
        case .moveCloser: "Move closer so your body fills the guide"
        case .moveTowardCenter: "Step into the center of the guide"
        case .armsAwayFromBody: "Face forward with your arms slightly away from your sides"
        case .holdStill: "Hold still for just a moment"
        }
    }
}

struct CalibrationAccumulator: Sendable {
    static let requiredSampleCount = 45

    private(set) var assessment: CalibrationAssessment = .incompleteBody
    private(set) var sampleCount = 0
    private var signatures: [BodySignature] = []
    private var anchors: [BodyAnchor] = []
    private var previousRoot: CGPoint?

    var progress: Double { min(Double(sampleCount) / Double(Self.requiredSampleCount), 1) }

    mutating func ingest(_ frame: PoseFrame) -> PlayerCalibration? {
        guard let signature = BodySignature(frame: frame), let bounds = frame.bodyBounds,
              frame.hasCalibrationBody else {
            reject(.incompleteBody)
            return nil
        }
        guard bounds.height >= 0.42 else {
            reject(.moveCloser)
            return nil
        }
        guard (0.22...0.78).contains(bounds.midX), (0.28...0.72).contains(bounds.midY) else {
            reject(.moveTowardCenter)
            return nil
        }
        guard frame.hasOpenCalibrationPose else {
            reject(.armsAwayFromBody)
            return nil
        }
        if let root = frame.point(.root, minimumConfidence: 0.55),
           let previousRoot, pointDistance(root, previousRoot) > 0.018 {
            self.previousRoot = root
            reject(.holdStill, keepRecentSamples: true)
            return nil
        }

        previousRoot = frame.point(.root, minimumConfidence: 0.55)
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
    }

    private mutating func reject(_ reason: CalibrationAssessment, keepRecentSamples: Bool = false) {
        assessment = reason
        if keepRecentSamples, signatures.count > 8 {
            signatures.removeFirst(signatures.count - 8)
            anchors.removeFirst(anchors.count - 8)
        } else if !keepRecentSamples {
            signatures.removeAll(keepingCapacity: true)
            anchors.removeAll(keepingCapacity: true)
        }
        sampleCount = signatures.count
    }
}

enum PlayerCalibrationStore {
    private static let key = "playerBodyCalibration.v1"

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

private func average(_ first: Double, _ second: Double) -> Double { (first + second) / 2 }
