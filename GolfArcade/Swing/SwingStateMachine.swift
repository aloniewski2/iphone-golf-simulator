import CoreGraphics
import Foundation

struct SwingStateMachine: Sendable {
    private(set) var phase: SwingPhase = .findingPlayer
    var handedness: Handedness = .right

    private var stableFrameCount = 0
    private var finishFrameCount = 0
    private var previousFrame: PoseFrame?
    private var addressFrame: PoseFrame?
    private var swingStartTime: TimeInterval?
    private var downswingStartTime: TimeInterval?
    private var impactTime: TimeInterval?
    private var maxWristSpeed = 0.0
    private var maxShoulderRotation = 0.0
    private var maxHipRotation = 0.0
    private var impactDirection = 0.0
    private var impactHeightDelta = 0.0
    private var confidenceSamples: [Double] = []

    mutating func ingest(_ frame: PoseFrame) -> SwingEvent? {
        guard frame.hasPlayableBody, let hands = frame.handCenter else {
            reset(to: .findingPlayer)
            previousFrame = frame
            return .phaseChanged(.findingPlayer)
        }

        let velocity = handVelocity(current: frame, previous: previousFrame)
        let speed = hypot(velocity.dx, velocity.dy)
        maxWristSpeed = max(maxWristSpeed, speed)
        confidenceSamples.append(frame.trackingConfidence)

        switch phase {
        case .findingPlayer:
            stableFrameCount = speed < 0.12 ? stableFrameCount + 1 : 0
            if stableFrameCount >= 8 {
                phase = .address
                addressFrame = frame
                previousFrame = frame
                return .phaseChanged(.address)
            }
        case .address:
            guard let addressHands = addressFrame?.handCenter else { break }
            if hands.y - addressHands.y > 0.075 && speed > 0.16 {
                phase = .backswing
                swingStartTime = frame.timestamp
                captureRotation(frame)
                previousFrame = frame
                return .phaseChanged(.backswing)
            }
        case .backswing:
            captureRotation(frame)
            if velocity.dy < -0.18 && maxWristSpeed > 0.20 {
                phase = .downswing
                downswingStartTime = frame.timestamp
                previousFrame = frame
                return .phaseChanged(.downswing)
            }
        case .downswing:
            captureRotation(frame)
            guard let addressHands = addressFrame?.handCenter else { break }
            if hands.y <= addressHands.y + 0.045 && speed > 0.22 {
                phase = .impact
                impactTime = frame.timestamp
                impactDirection = Double(velocity.dx)
                impactHeightDelta = Double(hands.y - addressHands.y)
                previousFrame = frame
                return .phaseChanged(.impact)
            }
        case .impact:
            phase = .followThrough
            previousFrame = frame
            return .phaseChanged(.followThrough)
        case .followThrough:
            captureRotation(frame)
            finishFrameCount = speed < 0.16 ? finishFrameCount + 1 : 0
            let timedOut = frame.timestamp - (impactTime ?? frame.timestamp) > 1.2
            if finishFrameCount >= 5 || timedOut {
                let metrics = makeMetrics(finishTime: frame.timestamp)
                phase = .finish
                previousFrame = frame
                return .shotReady(metrics)
            }
        case .finish:
            if speed < 0.10 {
                stableFrameCount += 1
                if stableFrameCount >= 12 {
                    reset(to: .address)
                    addressFrame = frame
                    previousFrame = frame
                    return .phaseChanged(.address)
                }
            } else {
                stableFrameCount = 0
            }
        }

        previousFrame = frame
        return nil
    }

    mutating func reset(to newPhase: SwingPhase = .findingPlayer) {
        phase = newPhase
        stableFrameCount = 0
        finishFrameCount = 0
        previousFrame = nil
        addressFrame = nil
        swingStartTime = nil
        downswingStartTime = nil
        impactTime = nil
        maxWristSpeed = 0
        maxShoulderRotation = 0
        maxHipRotation = 0
        impactDirection = 0
        impactHeightDelta = 0
        confidenceSamples.removeAll(keepingCapacity: true)
    }

    private func handVelocity(current: PoseFrame, previous: PoseFrame?) -> CGVector {
        guard let currentHands = current.handCenter, let previous, let previousHands = previous.handCenter else { return .zero }
        let delta = max(current.timestamp - previous.timestamp, 1.0 / 120.0)
        return CGVector(dx: (currentHands.x - previousHands.x) / delta, dy: (currentHands.y - previousHands.y) / delta)
    }

    private mutating func captureRotation(_ frame: PoseFrame) {
        guard let address = addressFrame else { return }
        if let current = frame.shoulderAngle, let start = address.shoulderAngle {
            maxShoulderRotation = max(maxShoulderRotation, abs(current - start))
        }
        if let current = frame.hipAngle, let start = address.hipAngle {
            maxHipRotation = max(maxHipRotation, abs(current - start))
        }
    }

    private func makeMetrics(finishTime: TimeInterval) -> SwingMetrics {
        let start = swingStartTime ?? finishTime
        let down = downswingStartTime ?? finishTime
        let impact = impactTime ?? finishTime
        let backswing = max(down - start, 0.01)
        let downswing = max(impact - down, 0.01)
        let averageConfidence = confidenceSamples.isEmpty ? 0 : confidenceSamples.reduce(0, +) / Double(confidenceSamples.count)
        return SwingMetrics(
            duration: max(finishTime - start, 0), backswingDuration: backswing,
            downswingDuration: downswing, tempo: backswing / downswing,
            normalizedWristSpeed: maxWristSpeed, shoulderRotationDegrees: maxShoulderRotation,
            hipRotationDegrees: maxHipRotation,
            swingDirection: impactDirection * (handedness == .right ? 1 : -1),
            impactHeightDelta: impactHeightDelta, balance: 0.85,
            confidence: min(max(averageConfidence, 0), 1)
        )
    }
}

