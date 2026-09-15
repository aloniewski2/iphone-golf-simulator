import CoreHaptics
import UIKit

@MainActor
final class SwingFeedbackController {
    var isEnabled = true {
        didSet {
            if !isEnabled { endBackswing() }
        }
    }

    private let supportsCustomHaptics = CHHapticEngine.capabilitiesForHardware().supportsHaptics
    private var engine: CHHapticEngine?
    private var backswingPlayer: CHHapticAdvancedPatternPlayer?

    init() {
        prepareEngine()
    }

    /// Builds a soft load from takeaway toward the top without aggressively shaking a mounted phone.
    func beginBackswing() {
        guard isEnabled else { return }
        endBackswing()

        guard supportsCustomHaptics else {
            UIImpactFeedbackGenerator(style: .soft).impactOccurred(intensity: 0.35)
            return
        }

        do {
            try ensureEngineIsRunning()
            let event = CHHapticEvent(
                eventType: .hapticContinuous,
                parameters: [
                    CHHapticEventParameter(parameterID: .hapticIntensity, value: 1),
                    CHHapticEventParameter(parameterID: .hapticSharpness, value: 0)
                ],
                relativeTime: 0,
                duration: 1.25
            )
            let intensity = CHHapticParameterCurve(
                parameterID: .hapticIntensityControl,
                controlPoints: [
                    .init(relativeTime: 0, value: 0.08),
                    .init(relativeTime: 0.55, value: 0.18),
                    .init(relativeTime: 1.1, value: 0.36)
                ],
                relativeTime: 0
            )
            let sharpness = CHHapticParameterCurve(
                parameterID: .hapticSharpnessControl,
                controlPoints: [
                    .init(relativeTime: 0, value: -0.3),
                    .init(relativeTime: 0.65, value: 0),
                    .init(relativeTime: 1.1, value: 0.35)
                ],
                relativeTime: 0
            )
            let pattern = try CHHapticPattern(events: [event], parameterCurves: [intensity, sharpness])
            backswingPlayer = try engine?.makeAdvancedPlayer(with: pattern)
            try backswingPlayer?.start(atTime: CHHapticTimeImmediate)
        } catch {
            UIImpactFeedbackGenerator(style: .soft).impactOccurred(intensity: 0.35)
        }
    }

    func endBackswing() {
        try? backswingPlayer?.stop(atTime: CHHapticTimeImmediate)
        backswingPlayer = nil
    }

    /// A dense first pulse represents contact and the crisp tail gives it a club-on-ball snap.
    func playImpact() {
        guard isEnabled else { return }
        endBackswing()

        guard supportsCustomHaptics else {
            let generator = UIImpactFeedbackGenerator(style: .heavy)
            generator.prepare()
            generator.impactOccurred(intensity: 1)
            return
        }

        do {
            try ensureEngineIsRunning()
            let events = [
                CHHapticEvent(
                    eventType: .hapticTransient,
                    parameters: [
                        CHHapticEventParameter(parameterID: .hapticIntensity, value: 1),
                        CHHapticEventParameter(parameterID: .hapticSharpness, value: 0.45)
                    ],
                    relativeTime: 0
                ),
                CHHapticEvent(
                    eventType: .hapticTransient,
                    parameters: [
                        CHHapticEventParameter(parameterID: .hapticIntensity, value: 0.55),
                        CHHapticEventParameter(parameterID: .hapticSharpness, value: 1)
                    ],
                    relativeTime: 0.045
                )
            ]
            let pattern = try CHHapticPattern(events: events, parameters: [])
            let player = try engine?.makePlayer(with: pattern)
            try player?.start(atTime: CHHapticTimeImmediate)
        } catch {
            UIImpactFeedbackGenerator(style: .heavy).impactOccurred(intensity: 1)
        }
    }

    private func prepareEngine() {
        guard supportsCustomHaptics else { return }
        engine = try? CHHapticEngine()
        engine?.playsHapticsOnly = true
        try? engine?.start()
    }

    private func ensureEngineIsRunning() throws {
        if engine == nil { prepareEngine() }
        try engine?.start()
    }
}

