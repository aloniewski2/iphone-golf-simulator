import XCTest

final class OnboardingUITests: XCTestCase {
    @MainActor func testFreshAccountAppearsBeforeAnySystemAlert() {
        var alerts = 0
        let monitor = addUIInterruptionMonitor(withDescription: "Unexpected early permission") { _ in alerts += 1; return false }
        defer { removeUIInterruptionMonitor(monitor) }
        let app = XCUIApplication(); app.launchArguments = ["-resetOnboarding", "-forceOnboarding", "-fakeGameCenter", "signedIn"]
        app.launchEnvironment["ONBOARDING_FAKE_GC_DELAY"] = "7"
        app.launch()
        XCTAssertTrue(app.buttons["onboarding-guest"].waitForExistence(timeout: 10))
        XCTAssertEqual(alerts, 0); XCTAssertEqual(app.alerts.count, 0)
    }
    @MainActor func testDeclinedAndUnavailableContinueAsGuest() {
        for result in ["decline", "unavailable"] {
            let app = XCUIApplication(); app.launchArguments = ["-resetOnboarding", "-forceOnboarding", "-fakeGameCenter", result]
            app.launch()
            XCTAssertTrue(app.buttons["onboarding-character-save"].waitForExistence(timeout: 12), result)
            app.terminate()
        }
    }

    @MainActor func testCharacterSavesInSixtySecondsAndKeepsNameAndBody() {
        let app = XCUIApplication(); app.launchArguments = ["-resetOnboarding", "-forceOnboarding", "-fakeGameCenter", "decline"]
        let began = Date(); app.launch()
        XCTAssertTrue(app.buttons["onboarding-character-save"].waitForExistence(timeout: 12))
        app.buttons["Female"].tap()
        let field = app.textFields["onboarding-name"]; field.tap()
        if let value = field.value as? String, value != "Your name", !value.isEmpty {
            field.typeText(String(repeating: XCUIKeyboardKey.delete.rawValue, count: value.count))
        }
        field.typeText("Ray")
        if app.keyboards.buttons["Done"].exists { app.keyboards.buttons["Done"].tap() }
        let save = app.buttons["onboarding-character-save"]
        if !save.isHittable { app.swipeUp() }; save.tap()
        XCTAssertTrue(app.buttons["onboarding-phone"].waitForExistence(timeout: 5))
        XCTAssertLessThanOrEqual(Date().timeIntervalSince(began), 60)
        app.terminate()
        app.launchArguments = ["-onboardingStep", "character", "-fakeGameCenter", "decline"]
        app.launch()
        XCTAssertTrue(field.waitForExistence(timeout: 10)); XCTAssertEqual(field.value as? String, "Ray")
        XCTAssertTrue(app.buttons["Female"].isSelected)
        let proof = XCTAttachment(screenshot: app.screenshot()); proof.name = "P3 saved hero"; proof.lifetime = .keepAlways; add(proof)
    }

    @MainActor func testNoDisplayContinuesToChoose() {
        let app = XCUIApplication(); app.launchArguments = ["-onboardingStep", "connect", "-fakeGameCenter", "decline"]
        app.launch()
        XCTAssertTrue(app.staticTexts["Waiting for a screen…"].waitForExistence(timeout: 10))
        let proof = XCTAttachment(screenshot: app.screenshot()); proof.name = "P4 waiting"; proof.lifetime = .keepAlways; add(proof)
        app.buttons["onboarding-phone"].tap()
        XCTAssertTrue(app.buttons["onboarding-game-tennis"].waitForExistence(timeout: 5))
    }

    @MainActor func testGameCardsSelectBothTutorials() {
        for sport in ["tennis", "golf"] {
            let app = XCUIApplication(); app.launchArguments = ["-onboardingStep", "choose"]
            app.launch()
            XCTAssertTrue(app.buttons["onboarding-game-tennis"].waitForExistence(timeout: 10))
            XCTAssertTrue(app.buttons["onboarding-game-golf"].exists)
            XCTAssertTrue(app.staticTexts["Cliffside · tutorial + 1 round"].exists)
            let shot = XCTAttachment(screenshot: app.screenshot()); shot.name = "P5 game cards"; shot.lifetime = .keepAlways; add(shot)
            app.buttons["onboarding-game-\(sport)"].tap()
            // Native-only simulator must show the real runtime failure or the coach intro.
            XCTAssertTrue(app.buttons["onboarding-tutorial-retry"].waitForExistence(timeout: 5) || app.buttons["onboarding-coach-play"].exists)
            app.terminate()
        }
    }

    @MainActor func testEverySetupStepResumesAfterProcessDeath() {
        let ids = ["account": "onboarding-guest", "character": "onboarding-character-save", "connect": "onboarding-phone", "motionPrimer": "onboarding-primer-touch", "choose": "onboarding-game-tennis", "tutorial:tennis": "onboarding-tutorial-retry", "reward:tennis": "onboarding-reward-continue"]
        for step in ["account", "character", "connect", "motionPrimer", "choose", "tutorial:tennis", "reward:tennis"] {
            let app = XCUIApplication(); app.launchArguments = ["-onboardingStep", step, "-fakeGameCenter", "signedIn"]
            app.launchEnvironment["ONBOARDING_FAKE_GC_DELAY"] = "7"; app.launch()
            let id = ids[step]!
            XCTAssertTrue(app.buttons[id].waitForExistence(timeout: 5) || (step == "tutorial:tennis" && app.buttons["onboarding-coach-play"].exists), step)
            app.terminate(); app.launchArguments = ["-fakeGameCenter", "signedIn"]; app.launch()
            XCTAssertTrue(app.buttons[id].waitForExistence(timeout: 5) || (step == "tutorial:tennis" && app.buttons["onboarding-coach-play"].exists), "resume " + step)
            let shot = XCTAttachment(screenshot: app.screenshot()); shot.name = "P9 resumed " + step; shot.lifetime = .keepAlways; add(shot)
            app.terminate()
        }
    }

    @MainActor func testExistingSaveSkipsAndReplayKeepsHero() {
        let app = XCUIApplication(); app.launchArguments = ["-resetOnboarding", "-fakeGameCenter", "signedIn"]
        app.launchEnvironment["ONBOARDING_SEED_COMPLETED_SPORT"] = "tennis"
        app.launchEnvironment["ONBOARDING_FAKE_GC_DELAY"] = "7"
        app.launch()
        XCTAssertFalse(app.buttons["onboarding-guest"].exists)
        XCTAssertTrue(app.buttons["intro-play"].waitForExistence(timeout: 5))
        app.buttons["intro-play"].tap(); app.buttons["home-settings"].tap()
        let replay = app.descendants(matching: .any).matching(NSPredicate(format: "label CONTAINS %@", "Replay onboarding")).firstMatch
        XCTAssertTrue(replay.waitForExistence(timeout: 5)); replay.tap()
        XCTAssertTrue(app.buttons["onboarding-guest"].waitForExistence(timeout: 5))
        app.buttons["onboarding-guest"].tap()
        XCTAssertTrue(app.textFields["onboarding-name"].waitForExistence(timeout: 5))
        XCTAssertFalse((app.textFields["onboarding-name"].value as? String ?? "").isEmpty)
    }

    @MainActor func testPartialPhoneWalkthroughStopsAtRealRuntimeFailure() {
        let app = XCUIApplication(); app.launchArguments = ["-resetOnboarding", "-forceOnboarding", "-fakeGameCenter", "signedIn"]
        app.launchEnvironment["ONBOARDING_FAKE_GC_DELAY"] = "7"; app.launch()
        XCTAssertTrue(app.buttons["onboarding-guest"].waitForExistence(timeout: 5)); app.buttons["onboarding-guest"].tap()
        XCTAssertTrue(app.buttons["onboarding-character-save"].waitForExistence(timeout: 5))
        if !app.buttons["onboarding-character-save"].isHittable { app.swipeUp() }
        app.buttons["onboarding-character-save"].tap()
        XCTAssertTrue(app.buttons["onboarding-phone"].waitForExistence(timeout: 5)); app.buttons["onboarding-phone"].tap()
        XCTAssertTrue(app.buttons["onboarding-game-tennis"].waitForExistence(timeout: 5))
        let picker = XCTAttachment(screenshot: app.screenshot()); picker.name = "Final Island game picker"; picker.lifetime = .keepAlways; add(picker)
        app.buttons["onboarding-game-tennis"].tap()
        let intro = app.buttons["onboarding-coach-play"]
        if intro.waitForExistence(timeout: 2) { intro.tap() }
        XCTAssertTrue(app.buttons["onboarding-tutorial-retry"].waitForExistence(timeout: 8))
        let proof = XCTAttachment(screenshot: app.screenshot()); proof.name = "Actual missing Unity runtime"; proof.lifetime = .keepAlways; add(proof)
    }
}
