import XCTest

final class RangePlayTests: XCTestCase {
    @MainActor
    func testPlayableFiveShotRoundAndReplay() throws {
        let app = XCUIApplication()
        app.launch()
        XCTAssertTrue(app.buttons["demoShot"].waitForExistence(timeout: 15))
        app.buttons["club-wedge"].tap()
        let pad = app.otherElements["swingPad"]
        let start = pad.coordinate(withNormalizedOffset: CGVector(dx: 0.5, dy: 0.12))
        let end = pad.coordinate(withNormalizedOffset: CGVector(dx: 0.5, dy: 0.85))
        start.press(forDuration: 0.1, thenDragTo: end)
        XCTAssertTrue(app.buttons["nextShot"].waitForExistence(timeout: 25))
        let score = app.staticTexts["roundScore"].label
        app.buttons["replayShot"].tap()
        XCTAssertTrue(app.buttons["nextShot"].waitForExistence(timeout: 25))
        XCTAssertEqual(app.staticTexts["roundScore"].label, score)
        app.buttons["nextShot"].tap()
        for index in 2...5 {
            app.buttons["demoShot"].tap()
            if index < 5 {
                XCTAssertTrue(app.buttons["nextShot"].waitForExistence(timeout: 25))
                app.buttons["nextShot"].tap()
            }
        }
        XCTAssertTrue(app.buttons["playAgain"].waitForExistence(timeout: 25))
        let attachment = XCTAttachment(screenshot: app.screenshot())
        attachment.name = "Completed five-shot round"
        attachment.lifetime = .keepAlways
        add(attachment)
        app.buttons["playAgain"].tap()
        XCTAssertTrue(app.buttons["demoShot"].waitForExistence(timeout: 5))
        XCTAssertEqual(app.staticTexts["roundScore"].label, "0 PTS")
    }
}
