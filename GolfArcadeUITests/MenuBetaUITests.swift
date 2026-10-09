import XCTest

final class MenuBetaUITests: XCTestCase {
    @MainActor func testTouchPlayMultiplayerLocalAndFeedback() {
        continueAfterFailure = false
        let app = XCUIApplication()
        app.launchArguments = ["-skipOnboarding", "--lobby"]
        app.launch()
        let play = app.buttons["home-play"]
        XCTAssertTrue(play.waitForExistence(timeout: 20))
        XCTAssertTrue(app.buttons["home-invite"].exists)
        XCTAssertTrue(app.buttons["beta-feedback"].exists)
        play.tap()
        app.buttons["partySolo"].tap()
        XCTAssertTrue(app.buttons["select-golf"].waitForExistence(timeout: 5))
        app.buttons["select-golf"].tap()
        XCTAssertTrue(app.buttons["golf-course-play"].waitForExistence(timeout: 5))
        app.buttons["menu-back"].tap()
        app.buttons["menu-back"].tap()
        app.buttons["partyOnline"].tap()
        XCTAssertTrue(app.buttons["homeInvite"].waitForExistence(timeout: 5))
        app.buttons["onlineQuick"].tap()
        XCTAssertTrue(app.buttons["net-tennis"].waitForExistence(timeout: 5))
        XCTAssertTrue(app.buttons["net-golf"].exists)
        app.buttons["menu-back"].tap()
        app.buttons["menu-back"].tap()
        XCTAssertTrue(app.buttons["partyLocalGolf"].waitForExistence(timeout: 5))
        app.buttons["partyNearby"].tap()
        XCTAssertTrue(app.buttons["net-host"].waitForExistence(timeout: 5))
        app.buttons["menu-home"].tap()
        XCTAssertTrue(play.waitForExistence(timeout: 5))
        app.buttons["home-homeEmotes"].tap()
        XCTAssertTrue(app.buttons["home-emote-wave"].waitForExistence(timeout: 5))
        app.buttons["home-emote-wave"].tap()
        app.buttons["menu-home"].tap()
        XCTAssertTrue(app.buttons["beta-feedback"].isHittable)
        // Opening the composer is user initiated; this test never sends a message.
        app.buttons["beta-feedback"].tap()
        let fallback = app.alerts["Text beta feedback"]
        if !fallback.waitForExistence(timeout: 3) {
            XCTAssertTrue(XCUIApplication(bundleIdentifier: "com.apple.MobileSMS").wait(for: .runningForeground, timeout: 8))
        }
        let screenshot = XCUIScreen.main.screenshot()
        if let path = ProcessInfo.processInfo.environment["MENU_BETA_PROOF_PATH"] {
            try? screenshot.pngRepresentation.write(to: URL(fileURLWithPath: path).appendingPathComponent("feedback-handoff.png"))
        }
    }
}
