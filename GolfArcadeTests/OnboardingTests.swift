import XCTest
import UIKit
@testable import GolfArcade

@MainActor final class OnboardingTests: XCTestCase {
    private func fixture() -> (UserDefaults, OnboardingStore, SportProgress) {
        let d = UserDefaults(suiteName: "onboarding-tests-\(UUID())")!
        return (d, OnboardingStore(defaults: d), SportProgress(defaults: d))
    }
    func testFreshStartsAtAccountAndPersistsAcrossInstances() {
        let (_, store, progress) = fixture()
        let flow = OnboardingFlow(store: store, progress: progress, arguments: [])
        XCTAssertEqual(flow.step, .account)
        flow.next(); XCTAssertEqual(flow.step, .character)
        XCTAssertEqual(OnboardingFlow(store: store, progress: progress, arguments: []).step, .character)
    }
    func testExistingTennisOrGolfSkipsOnboarding() {
        for sport in [Sport.tennis, .golf] {
            let (_, store, progress) = fixture(); progress.completeTutorial(sport)
            let flow = OnboardingFlow(store: store, progress: progress, arguments: [])
            XCTAssertEqual(flow.step, .done); XCTAssertFalse(flow.active)
        }
    }
    func testResetOnlyClearsFTUEKeys() {
        let (d, store, progress) = fixture()
        let roster = Data([1, 2, 3]); d.set(roster, forKey: "players.v1")
        progress.completeTutorial(.tennis)
        store.step = .connect; store.account = "gc:test"; store.characterInitialized = true
        _ = OnboardingFlow(store: store, progress: progress, arguments: ["-resetOnboarding", "-forceOnboarding"])
        XCTAssertEqual(store.step, .account); XCTAssertNil(store.account); XCTAssertFalse(store.characterInitialized)
        XCTAssertEqual(d.data(forKey: "players.v1"), roster)
        XCTAssertTrue(d.bool(forKey: "sport.tennis.tutorialDone.v1"))
    }
    func testLegacyArgumentsSkipUnlessForced() {
        for arg in ["-skipPlayerCalibration", "-startInCameraMode", "-startCourse", "--lobby-mock", "--lobby", "--character-editor", "--resume-tennis-round-3", "-benchTennis", "-range.swingInput"] {
            let (_, s, p) = fixture()
            XCTAssertFalse(OnboardingFlow(store: s, progress: p, arguments: [arg]).active, arg)
            XCTAssertTrue(OnboardingFlow(store: s, progress: p, arguments: [arg, "-forceOnboarding"]).active, arg)
        }
    }
    func testSavedSetupResumesAndRetiredLessonStepsReturnToChoose() {
        for step in [OnboardingStep.account, .character, .connect, .motionPrimer, .choose, .tutorial(.tennis), .reward(.golf)] {
            let (_, s, p) = fixture(); s.step = step; s.explicitRun = true; p.completeTutorial(.tennis)
            let expected: OnboardingStep
            switch step {
            case .motionPrimer, .tutorial, .reward: expected = .choose
            default: expected = step
            }
            XCTAssertEqual(OnboardingFlow(store: s, progress: p, arguments: []).step, expected)
            XCTAssertTrue(p.finishedTutorial(.tennis), "migration preserves earned progress")
        }
    }
    func testGuestIdentityIsStableAndNameIsBounded() {
        let (_, s, _) = fixture(); let key = s.guest()
        XCTAssertTrue(key.hasPrefix("guest:")); XCTAssertEqual(s.guest(), key)
        XCTAssertEqual(OnboardingStore.name("  \n"), "Player 1")
        XCTAssertEqual(OnboardingStore.name(String(repeating: "a", count: 60)).count, 40)
        XCTAssertEqual(OnboardingStore.name(" Ray \n"), "Ray")
    }
    func testCharacterLookPersistsWithExistingRosterAPI() {
        let (d, _, _) = fixture()
        var player = Player(name: "Ray", colorIndex: 0); player.standardFemale = true
        player.setSkin(0.62); player.haircut = 2; player.setHair(natural: 0.4)
        PlayerRosterStore.save([player], defaults: d)
        let saved = PlayerRosterStore.load(defaults: d).first
        XCTAssertEqual(saved?.name, "Ray"); XCTAssertEqual(saved?.standardFemale, true)
        XCTAssertEqual(saved?.skinT, player.skinT); XCTAssertEqual(saved?.haircut, 2); XCTAssertEqual(saved?.hairT, player.hairT)
    }

    func testFirstTutorialFillsLevelTwoAndReplayPaysNothing() {
        let (_, store, _) = fixture(); let player = UUID()
        _ = Progression.shared.award(player: player, stats: MatchStats(), won: false, score: "", opponent: "", practice: false, difficulty: 0)
        let before = Progression.shared.entry(for: player)
        Analytics.clearEvents()
        XCTAssertEqual(Progression.shared.awardTutorial(player: player, sport: .tennis, store: store), 200 - before.xp)
        let earned = Progression.shared.entry(for: player)
        XCTAssertEqual(earned.level, 2); XCTAssertEqual(earned.xp, 0)
        XCTAssertTrue(store.requestedHeadband)
        XCTAssertEqual(Progression.shared.awardTutorial(player: player, sport: .tennis, store: store), 0)
        XCTAssertEqual(Progression.shared.entry(for: player), earned)
        XCTAssertEqual(Analytics.events.filter { $0.contains("reward_claimed") }.count, 1)
        XCTAssertEqual(Progression.shared.awardTutorial(player: player, sport: .golf, store: store), 50)
        XCTAssertEqual(Progression.shared.entry(for: player).xp, 50)
        XCTAssertNil(store.rewardItem(.golf)); XCTAssertEqual(store.rewardItem(.tennis), "white-headband")
        XCTAssertEqual(Progression.shared.awardTutorial(player: player, sport: .golf, store: store), 0)
        XCTAssertEqual(LevelRewards.rewards(for: 2).first?.id, "white-headband")
    }

    func testRetiredAimStepReturnsToChooseWithoutLosingStoredProgress() {
        let (_, store, progress) = fixture(); store.step = .tutorial(.tennis); store.tutorialStep = 2
        let flow = OnboardingFlow(store: store, progress: progress, arguments: [])
        XCTAssertEqual(flow.step, .choose); XCTAssertEqual(flow.store.tutorialStep, 2)
        flow.finish(.golf); XCTAssertEqual(TennisMenu.shared.screen, .hub(.golf)); XCTAssertFalse(flow.active)
    }

    func testReplayPreservesHeroProgressAndLevel() {
        let (defaults, store, progress) = fixture()
        let hero = Data([9, 8, 7]); defaults.set(hero, forKey: "players.v1")
        progress.completeTutorial(.tennis)
        let flow = OnboardingFlow(store: store, progress: progress, arguments: [])
        let player = UUID(); let level = Progression.shared.entry(for: player)
        flow.replay()
        XCTAssertEqual(flow.step, .account); XCTAssertTrue(store.characterInitialized)
        XCTAssertEqual(defaults.data(forKey: "players.v1"), hero); XCTAssertTrue(progress.finishedTutorial(.tennis))
        XCTAssertEqual(Progression.shared.awardTutorial(player: player, sport: .tennis, store: store), 0)
        XCTAssertEqual(Progression.shared.entry(for: player), level)
    }

    func testInterruptedRewardJournalRecoversWithoutDoubleXP() throws {
        let (_, store, _) = fixture(); let player = UUID()
        let target = Progression.Entry(level: 2, xp: 0, totalXP: 200, matches: 0, wins: 0)
        let key = "ftue.pendingReward.tennis.v1"
        store.defaults.set(200, forKey: key + ".xp"); store.defaults.set("white-headband", forKey: key + ".item")
        store.defaults.set(try JSONEncoder().encode(target), forKey: key)
        XCTAssertEqual(Progression.shared.awardTutorial(player: player, sport: .tennis, store: store), 200)
        XCTAssertEqual(Progression.shared.entry(for: player), target)
        XCTAssertEqual(Progression.shared.awardTutorial(player: player, sport: .tennis, store: store), 0)
        XCTAssertEqual(Progression.shared.entry(for: player).totalXP, 200)
    }

    private final class SignedInPresenter: GameCenterAccount {
        var calls = 0
        func authenticate(present: @escaping (UIViewController) -> Void) async -> GameCenterAccountResult {
            calls += 1; present(UIViewController()); return .signedIn(id: "fixture", name: "Ray")
        }
    }
    func testAuthenticationStartsOnceAndSuccessfulSheetDismissalKeepsAccount() async throws {
        let (_, store, progress) = fixture(); let flow = OnboardingFlow(store: store, progress: progress, arguments: [])
        let account = SignedInPresenter(); let model = OnboardingAccountModel(account: account)
        model.start(flow); model.start(flow)
        try await Task.sleep(for: .milliseconds(100))
        model.dismissed(flow)
        XCTAssertEqual(account.calls, 1); XCTAssertEqual(store.account, "gc:fixture")
        try await Task.sleep(for: .milliseconds(800))
        XCTAssertEqual(flow.step, .character); XCTAssertEqual(store.account, "gc:fixture")
    }
}
