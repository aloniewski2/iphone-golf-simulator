import SwiftUI

@MainActor @Observable
final class OnboardingFlow {
    static let shared = OnboardingFlow()
    let store: OnboardingStore
    private let progress: SportProgress
    private(set) var step: OnboardingStep
    private(set) var tutorialBegan: Double?
    private var firstHit = false
    private var launched = false
    private var reportedExercise: String?
    private(set) var coachIntro = false
    var active: Bool { step != .done }
    var holdsMenu: Bool { active && { if case .tutorial = step { return coachIntro || !SportsSession.shared.active }; return true }() }

    init(store: OnboardingStore = OnboardingStore(), progress: SportProgress = .shared,
         arguments: [String] = ProcessInfo.processInfo.arguments) {
        self.store = store; self.progress = progress
        let first = store.defaults.object(forKey: "ftue.installedAt.v1") == nil
        _ = store.installedAt
        var selected = store.step
        #if DEBUG
        // Seeds an existing save for the P9 returning-player UI test; never completes a live lesson.
        if let value = ProcessInfo.processInfo.environment["ONBOARDING_SEED_COMPLETED_SPORT"], let sport = Sport(rawValue: value), sport.playable { progress.completeTutorial(sport) }
        if arguments.contains("-resetOnboarding") { store.reset(); _ = store.installedAt; selected = .account }
        let force = arguments.contains("-forceOnboarding")
        let legacy = arguments.contains { arg in
            ["-skipPlayerCalibration", "-startInCameraMode", "-startCourse", "--lobby-mock", "--lobby", "--character-editor", "-benchTennis", "-range.swingInput"].contains(arg) || arg.hasPrefix("--resume-tennis-round-")
        }
        let unitHost = ProcessInfo.processInfo.environment["XCTestConfigurationFilePath"] != nil && arguments == ProcessInfo.processInfo.arguments
        if force { store.explicitRun = true; selected = .account }
        else if arguments.contains("-skipOnboarding") || legacy || unitHost { selected = .done }
        if let i = arguments.firstIndex(of: "-onboardingStep"), arguments.indices.contains(i + 1),
           let override = OnboardingStep(key: arguments[i + 1]) { selected = override; store.explicitRun = true }
        #endif
        // Explicit replay/other-sport runs and the reward already earned survive process death.
        if progress.anyTutorialDone && !store.explicitRun {
            selected = .done; Analytics.track("onboarding_skipped_existing")
        }
        switch selected {
        case .tutorial, .reward, .motionPrimer: selected = .choose; store.explicitRun = false
        default: break
        }
        // The shipped app opens the menu directly. Identity editing remains in the locker.
        if arguments == ProcessInfo.processInfo.arguments && !arguments.contains("-forceOnboarding") {
            selected = .done; store.explicitRun = false
        }
        store.step = selected; step = selected
        if first { Analytics.track("app_first_open") }
        let n = store.defaults.integer(forKey: "ftue.sessions.v1") + 1
        store.defaults.set(n, forKey: "ftue.sessions.v1")
        Analytics.track("session_start", ["n": String(n)])
        viewed()
    }
    private func viewed() {
        Analytics.track("onboarding_step_view", ["step": step.key])
        if step == .connect { Analytics.track("connect_view") }
    }
    func move(to next: OnboardingStep) { store.step = next; step = next; viewed() }
    func next() {
        switch step {
        case .account: move(to: .character)
        case .character: move(to: .connect)
        case .connect: playOnPhone()
        case .motionPrimer: primer(touch: false)
        case .reward(let sport): finish(sport)
        default: break
        }
    }
    func guest() { _ = store.guest(); Analytics.track("guest_selected"); next() }
    func playOnPhone() { move(to: .connect) }
    func playOnTV() {
        guard SportsSession.shared.displayConnected else { return }
        store.touch = false; SportsSession.shared.touch = false; move(to: .choose)
    }
    func primer(touch: Bool) {
        store.touch = touch; SportsSession.shared.touch = touch
        Analytics.track("motion_primer", ["choice": touch ? "touch" : "motion"]); move(to: .choose)
    }
    func choose(_ sport: Sport) {
        guard sport.playable else { return }
        store.explicitRun = false; launched = false; coachIntro = false
        move(to: .done)
        TennisMenu.shared.begin(MenuLaunch(sport: sport, mode: sport == .golf ? .round : .exhibition, round: sport == .tennis ? 0 : nil))
    }
    func launchTutorialIfNeeded() {
        if case .tutorial = step { move(to: .choose) }
    }
    func tutorialStarted(_ sport: Sport) {
        tutorialBegan = ProcessInfo.processInfo.systemUptime; firstHit = false; reportedExercise = nil
        Analytics.track("tutorial_start", ["sport": sport.rawValue, "resumedAt": String(store.tutorialStep)])
        Analytics.track("match_start", ["mode": "tutorial"])
    }
    func resumeTutorial() { launched = false; launchTutorialIfNeeded() }
    func showCoachIntro() { coachIntro = true }
    func continueIntro() {
        TennisCampaign.shared.markSeen("tutorialIntro")
        coachIntro = false
        TennisMenu.shared.startTutorial(.tennis)
    }
    func reportedStep(_ index: Int, sport: Sport) {
        if sport == .tennis { store.tutorialStep = index }
        let key = "\(sport.rawValue):\(index)"
        if reportedExercise != key { reportedExercise = key; Analytics.track("tutorial_step_start", ["i": String(index)]) }
    }
    func hit() {
        guard !firstHit, let began = tutorialBegan else { return }
        firstHit = true
        Analytics.track("first_hit", ["seconds_since_tutorial_start": String(ProcessInfo.processInfo.systemUptime - began), "seconds_since_install": String(Analytics.secondsSinceInstall)])
    }
    func completed(_ sport: Sport) {
        if let player = TennisMenu.shared.player { Progression.shared.awardTutorial(player: player.id, sport: sport, store: store) }
        Analytics.track("tutorial_complete", ["sport": sport.rawValue, "seconds": String(tutorialBegan.map { ProcessInfo.processInfo.systemUptime - $0 } ?? 0)])
        let score = SportsSession.shared.score.detail
        let won = sport == .golf ? "true" : score.contains("15–0") ? "true" : score.contains("0–15") ? "false" : "unknown"
        Analytics.track("match_end", ["mode": "tutorial", "won": won])
        move(to: .reward(sport))
    }
    func finish(_ sport: Sport) {
        store.explicitRun = false; move(to: .done)
        TennisMenu.shared.finishOnboarding(sport)
        Analytics.track("onboarding_done", ["seconds_since_install": String(Analytics.secondsSinceInstall)])
    }
    /// Leaving setup never claims a tutorial or awards its reward.
    func exitToMenu() {
        store.explicitRun = false; launched = false; coachIntro = false; tutorialBegan = nil
        move(to: .done)
        TennisMenu.shared.goHome()
    }
    func back() {
        coachIntro = false; launched = false
        let destination: OnboardingStep
        switch step {
        case .account, .done: exitToMenu(); return
        case .character: destination = .account
        case .connect: destination = .character
        case .motionPrimer: destination = .connect
        case .choose: destination = .connect
        case .tutorial: destination = .choose
        case .reward(let sport): finish(sport); return
        }
        move(to: destination)
        if SportsSession.shared.active { SportsSession.shared.end() }
    }
    func replay() {
        for sport in [Sport.tennis, .golf] where progress.finishedTutorial(sport) && !store.rewardClaimed(sport) {
            store.markReward(sport, xp: 0, item: nil)
        }
        store.explicitRun = true; store.characterInitialized = true; launched = false
        Analytics.track("onboarding_replay"); move(to: .account)
    }
}
