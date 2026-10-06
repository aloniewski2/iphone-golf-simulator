import XCTest

final class MenuNavigationRegressionTests: XCTestCase {
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
        let golf = app.buttons["Golf"]
        XCTAssertTrue(golf.waitForExistence(timeout: 5))
        golf.tap()
        XCTAssertTrue(app.buttons["hub-round"].waitForExistence(timeout: 5))
        XCTAssertFalse(app.buttons["hub-tutorial"].exists)
        app.buttons["hub-round"].tap()
        for key in ["cliffside", "postcards", "wildisles", "magma"] {
            XCTAssertTrue(app.buttons["map-\(key)"].exists)
        }
        app.buttons["menu-back"].tap()
        XCTAssertTrue(app.buttons["hub-round"].waitForExistence(timeout: 5))
        app.buttons["menu-back"].tap()
        XCTAssertTrue(app.buttons["Golf"].waitForExistence(timeout: 5))
        app.buttons["Tennis"].tap()
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
