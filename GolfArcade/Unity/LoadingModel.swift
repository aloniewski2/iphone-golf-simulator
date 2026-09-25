import Foundation

/// The loading bar. It shows at least `minimum` seconds — long enough to read a tip and a
/// how-to card — and flows from 0 to 100% instead of jumping: a time curve carries it toward
/// 90%, real milestones from the game (runtime booted, scene loading, ready) push it ahead of
/// the curve when they arrive early, it never goes backwards, and the last 10% only runs once
/// the game is ready *and* the minimum time is up.
@MainActor @Observable
final class LoadingModel {
    static let minimum: TimeInterval = 10
    static let finishDuration: TimeInterval = 0.6
    /// The curve reaches `hold` at `curveDuration` seconds, then waits there for the game.
    static let hold = 0.9, curveDuration: TimeInterval = 9.5

    /// Shown progress, 0...1.
    private(set) var progress = 0.0
    private(set) var finished = true
    private(set) var started: Date?
    private var milestone = 0.0
    private var ready = false
    private var finishStart: Date?
    private var finishFrom = 0.0
    var onFinish: (() -> Void)?

    var percent: Int { Int((progress * 100).rounded(.down)) }

    func begin(now: Date) {
        started = now; finished = false; progress = 0; milestone = 0.05; ready = false; finishStart = nil
    }
    /// A real milestone (capped below the finish, which only time and readiness unlock).
    func reach(_ value: Double) { milestone = max(milestone, min(Self.hold, value)) }
    func markReady() { ready = true; reach(Self.hold) }
    /// Benchmarks and tests: no waiting.
    func skip() { guard !finished else { return }; progress = 1; complete() }
    func cancel() { finished = true; started = nil; onFinish = nil }

    func tick(now: Date) {
        guard let started, !finished else { return }
        let t = now.timeIntervalSince(started)
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
        finished = true
        let done = onFinish; onFinish = nil
        done?()
    }
}
