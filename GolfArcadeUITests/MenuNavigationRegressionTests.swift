import XCTest

final class MenuNavigationRegressionTests: XCTestCase {
    func testControllerShowsSetupThenServeDiveAndWinningPointEmotes() {
        continueAfterFailure = false
        let app = XCUIApplication()
        func launch(_ phase: String) {
            app.terminate()
            app.launchArguments = ["-skipOnboarding", "-controllerPhaseCheck", phase]
            app.launch()
            XCTAssertTrue(app.buttons["controller-exit"].waitForExistence(timeout: 15))
            XCTAssertFalse(app.buttons["tutorial-skip-step"].exists)
            XCTAssertFalse(app.staticTexts["COACH RAY"].exists)
        }
        launch("timing")
        XCTAssertTrue(app.buttons["timing-start"].waitForExistence(timeout: 5))
        XCTAssertFalse(app.buttons["controller-ready"].exists)
        app.buttons["timing-skip"].tap()
        XCTAssertTrue(app.buttons["controller-ready"].waitForExistence(timeout: 5))
        XCTAssertFalse(app.buttons["tennisToss"].exists)
        XCTAssertFalse(app.buttons["tennisDive"].exists)
        app.buttons["controller-ready"].tap()
        XCTAssertFalse(app.buttons["controller-ready"].exists)
        launch("serve")
        XCTAssertTrue(app.buttons["tennisToss"].waitForExistence(timeout: 5))
        XCTAssertTrue(app.buttons["tennisDive"].exists)
        XCTAssertFalse(app.buttons["controller-emote-0"].exists)
        launch("rally")
        XCTAssertTrue(app.buttons["tennisDive"].waitForExistence(timeout: 5))
        XCTAssertTrue(app.buttons["tennisDive"].isEnabled)
        XCTAssertFalse(app.buttons["tennisToss"].exists)
        launch("point")
        XCTAssertTrue(app.buttons["controller-emote-0"].waitForExistence(timeout: 5))
        XCTAssertFalse(app.buttons["tennisDive"].exists)
        XCTAssertFalse(app.buttons["tennisToss"].exists)
    }
    func testConnectedPhoneShowsOnlyRemoteControlsAcrossSportSelection() {
        let app = XCUIApplication()
        app.launchArguments = ["-skipOnboarding", "-controllerMenuCheck"]
        app.launch()
        XCTAssertTrue(app.staticTexts["controller-connection"].waitForExistence(timeout: 15))
        XCTAssertFalse(app.buttons["intro-play"].exists)
        XCTAssertFalse(app.buttons["home-play"].exists)
        XCTAssertFalse(app.buttons["Golf"].exists)
        XCTAssertTrue(app.buttons["remote-select"].exists)
        app.buttons["remote-select"].tap()
        XCTAssertTrue(app.staticTexts["controller-connection"].exists)
        XCTAssertFalse(app.buttons["home-play"].exists)
    }
    func testGolfSelectionAndBackButtonsRespondToTouches() {
        continueAfterFailure = false
        let app = XCUIApplication()
        app.launchArguments = ["-skipOnboarding"]
        app.launch()
        let intro = app.buttons["intro-play"]
        XCTAssertTrue(intro.waitForExistence(timeout: 15))
        intro.tap()
        let play = app.buttons["home-play"]
        XCTAssertTrue(play.waitForExistence(timeout: 5))
        play.tap()
        app.buttons["partySolo"].tap()
        let golf = app.buttons["select-golf"]
        XCTAssertTrue(golf.waitForExistence(timeout: 5))
        golf.tap()
        XCTAssertTrue(app.buttons["hub-round"].waitForExistence(timeout: 5))
        XCTAssertFalse(app.buttons["hub-tutorial"].exists)
        app.buttons["hub-round"].tap()
        for key in ["cliffside", "wildisles", "magma"] {
            XCTAssertTrue(app.buttons["map-\(key)"].exists)
        }
        XCTAssertFalse(app.buttons["map-postcards"].exists)
        app.buttons["menu-back"].tap()
        XCTAssertTrue(app.buttons["hub-round"].waitForExistence(timeout: 5))
        app.buttons["menu-back"].tap()
        XCTAssertTrue(app.buttons["select-golf"].waitForExistence(timeout: 5))
        app.buttons["select-tennis"].tap()
        XCTAssertTrue(app.buttons["hub-exhibition"].waitForExistence(timeout: 5))
        XCTAssertFalse(app.buttons["hub-tutorial"].exists)
        app.buttons["hub-exhibition"].tap()
        let start = app.buttons["Start Match"]
        XCTAssertTrue(start.waitForExistence(timeout: 5)); start.tap()
        for key in ["resort", "skyscraper", "volcano"] { XCTAssertTrue(app.buttons["map-\(key)"].exists) }
        app.buttons["menu-home"].tap()
        XCTAssertTrue(play.waitForExistence(timeout: 5))
    }
}
