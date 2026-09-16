import XCTest

final class RangePlayTests: XCTestCase {
    @MainActor
    func testCompletedCalibrationShowsCourseInCameraMode() {
        let app = XCUIApplication()
        app.launchArguments += ["-skipPlayerCalibration", "-startInCameraMode"]
        app.launch()
        XCTAssertTrue(app.descendants(matching: .any)["courseSelection"].waitForExistence(timeout: 15))
        app.buttons["course-easy"].tap()
        app.buttons["playSelectedCourse"].tap()
        XCTAssertTrue(app.descendants(matching: .any)["golfCourse"].waitForExistence(timeout: 15))
        XCTAssertTrue(app.descendants(matching: .any)["cameraPanel"].waitForExistence(timeout: 5))
        XCTAssertTrue(app.descendants(matching: .any)["cameraPip"].exists)
        let attachment = XCTAttachment(screenshot: app.screenshot())
        attachment.name = "Full-screen course with camera and club rail"
        attachment.lifetime = .keepAlways
        add(attachment)
    }

    @MainActor
    func testPlayableFiveShotRoundAndReplay() throws {
        let app = XCUIApplication()
        app.launchArguments.append("-skipPlayerCalibration")
        app.launch()
        XCTAssertTrue(app.buttons["course-medium"].waitForExistence(timeout: 15))
        app.buttons["course-medium"].tap()
        app.buttons["playSelectedCourse"].tap()
        XCTAssertTrue(app.otherElements["swingPad"].waitForExistence(timeout: 15))
        app.buttons["club-wedge"].tap()
        let pad = app.otherElements["swingPad"]
        let start = pad.coordinate(withNormalizedOffset: CGVector(dx: 0.5, dy: 0.12))
        let end = pad.coordinate(withNormalizedOffset: CGVector(dx: 0.5, dy: 0.85))
        start.press(forDuration: 0.1, thenDragTo: end)
        XCTAssertTrue(app.buttons["nextShot"].waitForExistence(timeout: 25))
        app.buttons["replayShot"].tap()
        XCTAssertTrue(app.buttons["nextShot"].waitForExistence(timeout: 25))
        app.buttons["nextShot"].tap()
        for index in 2...5 {
            let pad = app.otherElements["swingPad"]
            pad.coordinate(withNormalizedOffset: CGVector(dx: 0.5, dy: 0.12))
                .press(forDuration: 0.1, thenDragTo: pad.coordinate(withNormalizedOffset: CGVector(dx: 0.5, dy: 0.85)))
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
        XCTAssertTrue(app.otherElements["swingPad"].waitForExistence(timeout: 5))
    }
}
