import Foundation
import CoreMotion

/// Loading reports actual work. The cover cannot finish before the target camera renders.
@MainActor @Observable
final class LoadingModel {
    enum Phase: String { case loading, frameReady, waitingForPlayers, transitioning, finished, cancelled, failed }
    static let minimum: TimeInterval = 1.2
    static let multiplayerMinimum: TimeInterval = 1.5
    static let finishDuration: TimeInterval = 0.25
    static var clockNow: Date { Date(timeIntervalSince1970: ProcessInfo.processInfo.systemUptime) }

    private(set) var phase: Phase = .finished
    var finished: Bool { phase == .finished || phase == .cancelled }
    private(set) var started: Date?
    private(set) var sceneProgress: Double?
    // Retained for existing capture fixtures; this is measured scene work, never elapsed time.
    var progress: Double { phase == .finished ? 1 : sceneProgress ?? 0 }
    private(set) var elapsed: TimeInterval = 0
    private(set) var waitingPlayers: [String] = []
    private(set) var transitionFraction = 0.0
    private(set) var timing: [String: Double] = [:]
    private(set) var tipIndex = -1
    private(set) var tipStarted: Date?
    var tipRemaining: TimeInterval { guard let tipStarted else { return 0 }; return max(0, 1.5-LoadingModel.clockNow.timeIntervalSince(tipStarted)) }
    /// Where in the sport's tips this load starts, so a player who loads often does not always
    /// meet the same first tip.
    private var tipOffset = 0
    private var currentTip: GameTip? {
        guard tipIndex >= 0 else { return nil }
        return GameTips.tip(SportsSession.shared.sport == "golf" ? .golf : .tennis, at: tipOffset + tipIndex)
    }
    var tip: String? { currentTip?.text }
    /// The tip's topic ("Technique", "Setup", …) for its label.
    var tipKind: String? { currentTip?.kind.title }
    /// The first tip is up almost at once, and each stays long enough to read.
    static let firstTipDelay: TimeInterval = 0.4
    static let tipInterval: TimeInterval = 6
    private var localReady = false
    private var playersReady = true
    private var readMinimum = LoadingModel.minimum
    private var finishStart: Date?
    private var errorMessage = ""
    private var stalledAfter: TimeInterval = 20
    var onFinish: (() -> Void)?
    private(set) var practiceSequence = 0
    private var lastPractice = Date.distantPast
    private let practiceMotion = CMMotionManager()

    var isStalled: Bool { !finished && phase != .transitioning && elapsed >= stalledAfter }
    var showsActivity: Bool { elapsed >= 2 && !finished && phase != .transitioning }
    var statusText: String {
        if phase == .failed { return errorMessage }
        if phase == .transitioning || phase == .frameReady || phase == .finished { return "Ready!" }
        if !waitingPlayers.isEmpty && localReady { return "Waiting for \(waitingPlayers.joined(separator: ", "))…" }
        if isStalled { return "Loading is taking longer than expected. Retry or leave the match." }
        if localReady { return "Waiting for the other players…" }
        if timing["sceneReady"] != nil { return "Preparing the first frame…" }
        if sceneProgress != nil { return "Preparing \(sceneProgress == 1 ? "the match" : "the venue")…" }
        return "Preparing your match…"
    }

    func begin(now: Date, multiplayer: Bool = false) {
        practiceMotion.stopGyroUpdates(); practiceSequence = 0; lastPractice = .distantPast
        started = now; phase = .loading; elapsed = 0; sceneProgress = nil; transitionFraction = 0
        localReady = false; playersReady = !multiplayer; readMinimum = multiplayer ? Self.multiplayerMinimum : Self.minimum
        tipIndex = -1; tipStarted = nil; tipOffset = Int.random(in: 0..<10_000)
        waitingPlayers = []; finishStart = nil; errorMessage = ""; stalledAfter = 20
        timing = ["commit": now.timeIntervalSince1970]
    }
    func cardAppeared(now: Date = LoadingModel.clockNow) { record("cardVisibleCallback", now) }
    func markBoot(now: Date = LoadingModel.clockNow) { guard phase == .loading else { return }; record("boot", now) }
    func reach(_ value: Double) {
        guard phase == .loading, value.isFinite else { return }
        sceneProgress = max(sceneProgress ?? 0, min(1, max(0, value)))
    }
    func markSceneReady(now: Date = LoadingModel.clockNow) { guard !finished, phase != .failed else { return }; record("sceneReady", now) }
    func markReady(now: Date = LoadingModel.clockNow) {
        guard !finished, phase != .failed, !localReady else { return }
        localReady = true; record("firstRenderedFrame", now)
        phase = playersReady ? .frameReady : .waitingForPlayers
    }
    func updatePlayers(waiting: [String], allReady: Bool, now: Date = LoadingModel.clockNow) {
        guard !finished, phase != .failed, phase != .transitioning else { return }
        waitingPlayers = waiting; playersReady = allReady
        if allReady { record("allPlayersReady", now) }
        if localReady { phase = allReady ? .frameReady : .waitingForPlayers }
    }
    func keepWaiting() { stalledAfter = elapsed + 20 }
    func fail(_ message: String) {
        guard !finished else { return }; phase = .failed; errorMessage = message; practiceMotion.stopGyroUpdates()
    }
    /// Only explicit automated verification may bypass the identity-read interval.
    func skip() { guard localReady, playersReady, !finished, phase != .failed else { return }; transitionFraction = 1; complete() }
    func cancel() { practiceMotion.stopGyroUpdates(); phase = .cancelled; started = nil; onFinish = nil }

    func tick(now: Date) {
        guard let started, !finished, phase != .failed else { return }
        elapsed = max(0, now.timeIntervalSince(started))
        if (!localReady || tipIndex < 0) && elapsed >= Self.firstTipDelay {
            let next = Int((elapsed - Self.firstTipDelay) / Self.tipInterval)
            if next != tipIndex { tipIndex = next; tipStarted = now }
        }
        guard localReady, playersReady, elapsed >= readMinimum else { return }
        if finishStart == nil { finishStart = now; phase = .transitioning; record("wipeStart", now) }
        transitionFraction = min(1, max(0, now.timeIntervalSince(finishStart!) / Self.finishDuration))
        if transitionFraction >= 1 { record("wipeEnd", now); complete() }
    }
    private func record(_ name: String, _ now: Date) {
        if timing[name] == nil { timing[name] = now.timeIntervalSince1970 }
    }
    private func complete() {
        practiceMotion.stopGyroUpdates(); phase = .finished
        let done = onFinish; onFinish = nil; done?()
    }
    func practice(now: Date = Date()) {
        guard !finished, phase != .failed, started != nil, now.timeIntervalSince(lastPractice) >= 1.5 else { return }
        lastPractice = now; practiceSequence += 1
    }
    private func startPracticeInput() {
        guard practiceMotion.isGyroAvailable else { return }
        practiceMotion.gyroUpdateInterval = 1.0 / 30
        practiceMotion.startGyroUpdates(to: .main) { [weak self] data, _ in
            guard let data else { return }; let r = data.rotationRate
            if r.x * r.x + r.y * r.y + r.z * r.z > 20 {
                MainActor.assumeIsolated { self?.practice() }
            }
        }
    }
}
