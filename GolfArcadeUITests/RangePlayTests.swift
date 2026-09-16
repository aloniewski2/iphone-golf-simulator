import XCTest

final class RangePlayTests: XCTestCase {
    private func any(_ app: XCUIApplication, _ identifier: String) -> XCUIElement {
        app.descendants(matching: .any)[identifier]
    }

    private func screenshot(_ app: XCUIApplication, _ name: String) {
        let attachment = XCTAttachment(screenshot: app.screenshot())
        attachment.name = name
        attachment.lifetime = .keepAlways
        add(attachment)
    }

    @MainActor
    func testMenuToFullScreenCourseInCameraMode() {
        let app = XCUIApplication()
        app.launchArguments += ["-skipPlayerCalibration", "-startInCameraMode"]
        app.launch()
        XCTAssertTrue(app.buttons["menuSolo"].waitForExistence(timeout: 15))
        screenshot(app, "Main menu")
        app.buttons["menuSolo"].tap()
        XCTAssertTrue(app.buttons["continueToCourses"].waitForExistence(timeout: 5))
        app.buttons["continueToCourses"].tap()
        XCTAssertTrue(app.buttons["course-hard"].waitForExistence(timeout: 5))
        screenshot(app, "Course select")
        app.buttons["course-hard"].tap()
        XCTAssertTrue(any(app, "golfCourse").waitForExistence(timeout: 15))
        XCTAssertTrue(any(app, "cameraPanel").waitForExistence(timeout: 5))
        XCTAssertTrue(any(app, "cameraPip").exists)
        XCTAssertTrue(app.buttons["club-driver"].exists)
        sleep(2)
        screenshot(app, "Full-screen course with camera and club rail")
    }

    @MainActor
    func testPlayAHoleOutWithTheTouchPad() {
        let app = XCUIApplication()
        app.launchArguments.append("-skipPlayerCalibration")
        app.launch()
        XCTAssertTrue(app.buttons["menuSolo"].waitForExistence(timeout: 15))
        app.buttons["menuSolo"].tap()
        app.buttons["continueToCourses"].tap()
        app.buttons["course-easy"].tap()
        let pad = app.otherElements["swingPad"]
        XCTAssertTrue(pad.waitForExistence(timeout: 15))
        screenshot(app, "Easy hole 1 tee")

        var replayed = false
        for _ in 0..<14 {
            if app.buttons["continueHole"].exists { break }
            if app.buttons["nextShot"].exists {
                if !replayed {
                    replayed = true
                    app.buttons["replayShot"].tap()
                    XCTAssertTrue(app.buttons["nextShot"].waitForExistence(timeout: 25))
                    screenshot(app, "Shot result")
                }
                app.buttons["nextShot"].tap()
            }
            guard pad.waitForExistence(timeout: 5) else { continue }
            pad.coordinate(withNormalizedOffset: CGVector(dx: 0.5, dy: 0.1))
                .press(forDuration: 0.1, thenDragTo: pad.coordinate(withNormalizedOffset: CGVector(dx: 0.5, dy: 0.9)))
            _ = app.buttons["nextShot"].waitForExistence(timeout: 25) || app.buttons["continueHole"].waitForExistence(timeout: 1)
        }
        XCTAssertTrue(app.buttons["continueHole"].waitForExistence(timeout: 25), "the stroke cap guarantees the hole ends")
        XCTAssertTrue(any(app, "holeResult").exists)
        screenshot(app, "Hole complete")
        app.buttons["continueHole"].tap()
        XCTAssertTrue(any(app, "holeChip").waitForExistence(timeout: 5))
        XCTAssertTrue(any(app, "holeChip").label.contains("H2"))

        app.buttons["courseMenu"].tap()
        app.buttons["Quit to menu"].tap()
        XCTAssertTrue(app.buttons["menuSolo"].waitForExistence(timeout: 5))
    }

    @MainActor
    func testMultiplayerAnnouncesEachTurn() {
        let app = XCUIApplication()
        app.launchArguments += ["-skipPlayerCalibration", "-fixturePlayers", "2"]
        app.launch()
        XCTAssertTrue(app.buttons["menuMultiplayer"].waitForExistence(timeout: 15))
        app.buttons["menuMultiplayer"].tap()
        XCTAssertTrue(app.buttons["continueToCourses"].waitForExistence(timeout: 5))
        XCTAssertTrue(app.buttons["addPlayer"].exists)
        screenshot(app, "Multiplayer setup")
        app.buttons["continueToCourses"].tap()
        app.buttons["course-medium"].tap()
        let banner = any(app, "turnBanner")
        XCTAssertTrue(banner.waitForExistence(timeout: 10))
        XCTAssertTrue(app.staticTexts["PLAYER 1'S TURN"].exists)
        screenshot(app, "Player 1 turn banner")
    }
}
