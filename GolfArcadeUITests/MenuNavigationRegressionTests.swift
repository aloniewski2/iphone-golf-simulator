import XCTest

final class MenuNavigationRegressionTests: XCTestCase {
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
        app.buttons["menu-back"].tap()
        XCTAssertTrue(app.buttons["Golf"].waitForExistence(timeout: 5))
        app.buttons["menu-home"].tap()
        XCTAssertTrue(play.waitForExistence(timeout: 5))
    }
}
