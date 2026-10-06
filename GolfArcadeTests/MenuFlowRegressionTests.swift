import XCTest
import UIKit
@testable import GolfArcade

@MainActor final class MenuFlowRegressionTests: XCTestCase {
    func testCalibrationPrecedesReadyAndDoesNotStartAPoint() {
        let session = SportsSession()
        session.active = true; session.ready = true; session.displayConnected = true
        session.sport = "tennis"; session.touch = true; session.loading.cancel()
        session.readyToPlay()
        XCTAssertTrue(session.paused)
        XCTAssertEqual(session.setupStage, .scan)
        session.offerTimingCalibration()
        XCTAssertEqual(session.setupStage, .timing)
        XCTAssertTrue(session.timingPrompt)
        session.readyToPlay(); session.resume()
        XCTAssertTrue(session.paused, "Ready/Resume must not bypass calibration")
        session.startTimingCheck()
        XCTAssertTrue(session.checkingTiming)
        XCTAssertFalse(session.paused, "Phone samples must flow during TV calibration")
        XCTAssertFalse(session.pointControlsVisible)
        session.receiveTimingResult("failed")
        XCTAssertEqual(session.setupStage, .ready)
        XCTAssertTrue(session.paused, "Calibration completion must wait for Ready")
        XCTAssertFalse(session.checkingTiming)
        session.readyToPlay()
        XCTAssertEqual(session.setupStage, .playing)
        XCTAssertFalse(session.paused)
        session.tennisPhase = "serve"
        XCTAssertTrue(session.tossVisible)
    }
    func testPointControlsAndCelebrationFollowActualPointPhase() {
        let session = SportsSession()
        session.active = true; session.ready = true; session.loading.cancel()
        session.sport = "tennis"; session.setupStage = .playing; session.paused = false
        for phase in ["serve", "toss", "receive", "rally"] {
            session.tennisPhase = phase
            XCTAssertTrue(session.pointControlsVisible)
            XCTAssertEqual(session.tossVisible, phase == "serve")
            XCTAssertFalse(session.canPlayEmote)
        }
        session.tennisPhase = "point"
        XCTAssertFalse(session.pointControlsVisible)
        session.emoteWindow = "intro"; XCTAssertFalse(session.canPlayEmote)
        session.emoteWindow = ""; XCTAssertFalse(session.canPlayEmote)
        session.emoteWindow = "point"; XCTAssertTrue(session.canPlayEmote)
        session.paused = true; XCTAssertFalse(session.canPlayEmote)
    }
    func testInterruptedTimingReturnsToCalibrationWithoutStartingPlay() {
        let session = SportsSession()
        session.active = true; session.ready = true; session.displayConnected = true
        session.sport = "tennis"; session.touch = true; session.loading.cancel()
        session.offerTimingCalibration(); session.startTimingCheck()
        session.pause(reason: "Display disconnected")
        XCTAssertTrue(session.timingPrompt)
        XCTAssertFalse(session.checkingTiming)
        XCTAssertEqual(session.setupStage, .timing)
        session.menuPauseVisible = true
        session.readyToPlay()
        XCTAssertFalse(session.menuPauseVisible, "Resume must return to the interrupted setup screen")
        XCTAssertTrue(session.paused)
        session.receiveTimingResult("100")
        XCTAssertEqual(session.setupStage, .timing, "Ignore late results from a cancelled check")
        session.skipTimingCheck()
        XCTAssertEqual(session.setupStage, .ready)
        XCTAssertTrue(session.paused)
    }
    func testGolfHasOnePlayableRouteWithoutATutorialPrerequisite() {
        let menu = TennisMenu()
        XCTAssertEqual(TennisMenu.hubItems(.golf), ["round"])
        XCTAssertTrue(menu.hubUnlocked(.golf, "round"))
        menu.debugShow(.hub(.golf))
        XCTAssertEqual(menu.focused, "round")
    }
    func testBothSportsChooseMapsBeforeStartingTheRuntime() {
        let menu = TennisMenu()
        for sport in [Sport.golf, .tennis] {
            menu.debugShow(.hub(sport))
            menu.begin(MenuLaunch(sport: sport, mode: .tutorial), onPhone: true)
            XCTAssertEqual(menu.screen, .map)
            XCTAssertEqual(menu.launch?.mode, sport == .golf ? .round : .exhibition)
            XCTAssertEqual(menu.mapChoices.count, sport == .golf ? 4 : 3)
            XCTAssertFalse(SportsSession.shared.active)
            menu.back()
            XCTAssertEqual(menu.screen, .hub(sport))
        }
        XCTAssertEqual(TennisMenu.hubItems(.tennis), ["exhibition", "campaign", "training"])
        for id in TennisMenu.hubItems(.tennis) { XCTAssertTrue(menu.hubUnlocked(.tennis, id)) }
    }
    func testStartupFailureStopsLoadingAndOffersTheSameMapAgain() {
        let menu = TennisMenu.shared
        menu.begin(MenuLaunch(sport: .golf, mode: .round), onPhone: true)
        SportsSession.shared.loading.begin(now: Date())
        SportsSession.shared.failStartup("Display unavailable")
        XCTAssertTrue(SportsSession.shared.loading.finished)
        XCTAssertFalse(SportsSession.shared.active)
        XCTAssertEqual(menu.screen, .map)
        XCTAssertEqual(menu.mapSport, .golf)
        XCTAssertEqual(menu.notice, "Display unavailable")
        menu.goHome()
    }
    func testRetiredTennisTutorialAndRewardSavesReturnToSelection() {
        for step in [OnboardingStep.tutorial(.tennis), .reward(.tennis), .motionPrimer] {
            let defaults = UserDefaults(suiteName: "retired-\(UUID())")!
            let store = OnboardingStore(defaults: defaults)
            store.step = step; store.explicitRun = true
            let flow = OnboardingFlow(store: store, progress: SportProgress(defaults: defaults), arguments: [])
            XCTAssertEqual(flow.step, .choose)
            XCTAssertFalse(store.rewardClaimed(.tennis))
        }
    }
    func testMainMenuIsReachableFromLoadingStoryAndEverySetupRoute() {
        let menu = TennisMenu()
        for screen in [MenuScreen.loading, .connect, .story, .map, .character, .settings, .golfLesson, .postMatch, .hub(.golf)] {
            menu.debugShow(screen)
            menu.goHome()
            XCTAssertEqual(menu.screen, .main, "\(screen)")
            XCTAssertNil(menu.launch)
        }
    }
    func testLoadingBackCancelsAndReturnsToAMenu() {
        let menu = TennisMenu()
        menu.debugShow(.loading)
        menu.back()
        XCTAssertEqual(menu.screen, .main)
        XCTAssertFalse(SportsSession.shared.active)
    }
    func testSetupCanGoBackAndExitWithoutCompletingATutorial() {
        let defaults = UserDefaults(suiteName: "navigation-\(UUID())")!
        let store = OnboardingStore(defaults: defaults)
        let progress = SportProgress(defaults: defaults)
        store.step = .connect
        let flow = OnboardingFlow(store: store, progress: progress, arguments: [])
        flow.back()
        XCTAssertEqual(flow.step, .character)
        flow.exitToMenu()
        XCTAssertEqual(flow.step, .done)
        XCTAssertFalse(flow.holdsMenu)
        XCTAssertFalse(progress.finishedTutorial(.golf))
        XCTAssertFalse(store.rewardClaimed(.golf))
    }
    func testRetiredGolfTutorialSaveReturnsToGameSelection() {
        let defaults = UserDefaults(suiteName: "golf-resume-\(UUID())")!
        let store = OnboardingStore(defaults: defaults)
        store.step = .tutorial(.golf); store.explicitRun = true
        let flow = OnboardingFlow(store: store, progress: SportProgress(defaults: defaults), arguments: [])
        XCTAssertEqual(flow.step, .choose)
    }
    func testPhoneGameplayIsBlockedWithoutAnExternalScreen() {
        let displays = SportsDisplays.shared
        let saved = displays.external
        defer { displays.external = saved }
        displays.external = nil
        XCTAssertNil(displays.gameWindow(preview: true))
        XCTAssertNil(displays.gameWindow(preview: false))
        let menu = TennisMenu.shared
        menu.begin(MenuLaunch(sport: .golf, mode: .round), onPhone: true)
        menu.tap("map-cliffside")
        XCTAssertEqual(menu.screen, .connect)
        XCTAssertFalse(SportsSession.shared.active)
        XCTAssertFalse(menu.rows(.connect).flatMap { $0 }.contains("phone"))
        menu.goHome()
    }
    func testAirPlayWindowWinsOverRequestedPhonePreview() {
        let displays = SportsDisplays.shared
        let oldExternal = displays.external, oldPhone = displays.phone
        let oldConnected = SportsSession.shared.displayConnected
        defer {
            displays.external = oldExternal; displays.phone = oldPhone
            SportsSession.shared.displayConnected = oldConnected
        }
        let external = UIWindow(frame: CGRect(x: 0, y: 0, width: 1920, height: 1080))
        displays.external = external
        XCTAssertTrue(displays.gameWindow(preview: true) === external)
        XCTAssertTrue(SportsSession.shared.displayConnected)
    }
}
