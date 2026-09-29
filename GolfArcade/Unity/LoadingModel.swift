import Foundation
import CoreMotion

/// Real readiness gates completion; the short minimum gives the transition a readable beat.
/// Progress below 90% is an estimate informed by runtime milestones, never a network promise.
@MainActor @Observable
final class LoadingModel {
    static let minimum: TimeInterval = 2
    static let finishDuration: TimeInterval = 0.3
    /// The curve reaches `hold` at `curveDuration` seconds, then waits there for the game.
    static let hold = 0.9, curveDuration: TimeInterval = 3

    /// Shown progress, 0...1.
    private(set) var progress = 0.0
    private(set) var finished = true
    private(set) var started: Date?
    private var milestone = 0.0
    private var ready = false
    private var finishStart: Date?
    private var finishFrom = 0.0
    var onFinish: (() -> Void)?
    private(set) var practiceSequence = 0
    private var lastPractice = Date.distantPast
    private let practiceMotion = CMMotionManager()
    func practice(now: Date = Date()) {
        guard !finished, started != nil, now.timeIntervalSince(lastPractice) >= 1.5 else { return }
        lastPractice = now; practiceSequence += 1
    }
    private func startPracticeInput() {
        guard practiceMotion.isGyroAvailable else { return }
        practiceMotion.gyroUpdateInterval = 1.0 / 30
        practiceMotion.startGyroUpdates(to: .main) { [weak self] data, _ in
            guard let data else { return }
            let r = data.rotationRate
            if r.x * r.x + r.y * r.y + r.z * r.z > 20 {
                MainActor.assumeIsolated { self?.practice() }
            }
        }
    }

    private(set) var elapsed: TimeInterval = 0
    var isStalled: Bool { !finished && !ready && elapsed >= 20 }
    var statusText: String {
        if progress >= 1 || ready { return "Ready" }
        if isStalled { return "Loading is taking longer than expected. Retry or return to the menu." }
        if elapsed >= 8 { return "Still loading…" }
        if milestone >= 0.6 { return "Preparing court…" }
        if milestone >= 0.2 { return "Preparing match…" }
        return "Loading…"
    }
    var percent: Int { Int((progress * 100).rounded(.down)) }

    func begin(now: Date) {
        practiceMotion.stopGyroUpdates(); practiceSequence = 0; lastPractice = .distantPast; startPracticeInput()
        elapsed = 0; started = now; finished = false; progress = 0; milestone = 0.05; ready = false; finishStart = nil
    }
    /// A real milestone (capped below the finish, which only time and readiness unlock).
    func reach(_ value: Double) { milestone = max(milestone, min(Self.hold, value)) }
    func markReady() { ready = true; reach(Self.hold) }
    /// Benchmarks and tests: no waiting.
    func skip() { guard !finished else { return }; progress = 1; complete() }
    func cancel() { practiceMotion.stopGyroUpdates(); finished = true; started = nil; onFinish = nil }

    func tick(now: Date) {
        guard let started, !finished else { return }
        let t = max(0, now.timeIntervalSince(started)); elapsed = t
        let x = min(1, t / Self.curveDuration)
        let curve = Self.hold * (1 - pow(1 - x, 2.2))
        var target = min(Self.hold, max(curve, milestone))
        if ready && t >= Self.minimum {
            if finishStart == nil { finishStart = now; finishFrom = max(progress, target) }
            let f = min(1, now.timeIntervalSince(finishStart!) / Self.finishDuration)
            target = finishFrom + (1 - finishFrom) * (1 - pow(1 - f, 2))
            if f >= 1 { progress = 1; complete(); return }
        }
        progress = max(progress, target)
    }

    private func complete() {
        practiceMotion.stopGyroUpdates()
        finished = true
        let done = onFinish; onFinish = nil
        done?()
    }
}
