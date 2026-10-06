import XCTest
import UIKit
@testable import GolfArcade

@MainActor final class MenuFlowRegressionTests: XCTestCase {
    func testGolfHasOnePlayableRouteWithoutATutorialPrerequisite() {
        let menu = TennisMenu()
        XCTAssertEqual(TennisMenu.hubItems(.golf), ["round"])
        XCTAssertTrue(menu.hubUnlocked(.golf, "round"))
        menu.debugShow(.hub(.golf))
        XCTAssertEqual(menu.focused, "round")
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
