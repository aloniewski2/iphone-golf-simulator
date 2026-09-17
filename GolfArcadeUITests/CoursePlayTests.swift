import XCTest

/// Touch-pad play, course art, multiplayer turns and the Practice Lab.
/// Runs on its own CI worker alongside `CameraPlayTests`.
final class CoursePlayTests: XCTestCase {
    func testPracticeLabWithoutCalibrationAndSyntheticReplay() {
        let app = XCUIApplication()
        app.launch()
        XCTAssertTrue(app.buttons["menuPractice"].waitForExistence(timeout: 15))
        app.buttons["menuPractice"].tap()
        XCTAssertTrue(any(app, "practiceLab").waitForExistence(timeout: 5))
        XCTAssertFalse(app.buttons["labStartTrial"].isEnabled)
        screenshot(app, "Practice Lab live diagnostics")
        app.segmentedControls["labPage"].buttons.element(boundBy: 1).tap()
        XCTAssertTrue(app.buttons["labFixture"].waitForExistence(timeout: 5))
        app.buttons["labFixture"].tap()
        XCTAssertTrue(app.staticTexts["Synthetic fixture — excluded from camera benchmarks"].waitForExistence(timeout: 5))
        XCTAssertTrue(app.buttons["labExport"].isEnabled)
        screenshot(app, "Practice Lab benchmark summary")
        app.swipeUp()
        app.buttons["Replay trace"].tap()
        XCTAssertTrue(any(app, "labReplayResult").waitForExistence(timeout: 5))
        XCTAssertTrue(any(app, "labReplayResult").label.contains("matches recorded output"))
        XCTAssertTrue(app.sliders["labReplayScrubber"].exists)
        screenshot(app, "Practice Lab deterministic replay")
        app.buttons["Done"].tap()
        app.buttons["labExport"].tap()
        // The system export picker confirms with Save or Move depending on the OS release, and
        // takes a while to appear the first time on a fresh simulator.
        let confirm = app.buttons.matching(NSPredicate(format: "label == 'Save' OR label == 'Move'")).firstMatch
        XCTAssertTrue(confirm.waitForExistence(timeout: 30), "the system export sheet appears")
        screenshot(app, "Practice Lab JSON export")
        // Dismiss the native file sheet without saving a test file or assuming a Cancel label.
        app.coordinate(withNormalizedOffset: CGVector(dx: 0.5, dy: 0.09))
            .press(forDuration: 0.1, thenDragTo: app.coordinate(withNormalizedOffset: CGVector(dx: 0.5, dy: 0.9)))
        XCTAssertFalse(confirm.exists)
        app.buttons["labExit"].tap()
        app.buttons["Discard session and leave"].tap()
        XCTAssertTrue(app.buttons["menuSolo"].waitForExistence(timeout: 5))
    }

    func testCourseArtAcrossAllDifficulties() {
        for difficulty in ["easy", "medium", "hard"] {
            let app = XCUIApplication()
            app.launchArguments = ["-skipPlayerCalibration", "-range.swingInput", "touch"]
            app.launch()
            XCTAssertTrue(app.buttons["menuSolo"].waitForExistence(timeout: 15))
            app.buttons["menuSolo"].tap()
            XCTAssertTrue(app.buttons["continueToCourses"].waitForExistence(timeout: 5))
            usleep(350_000) // Let the navigation crossfade finish before tapping its destination.
            app.buttons["continueToCourses"].tap()
            XCTAssertTrue(app.buttons["course-\(difficulty)"].waitForExistence(timeout: 5))
            app.buttons["course-\(difficulty)"].tap()
            XCTAssertTrue(app.otherElements["swingPad"].waitForExistence(timeout: 10))
            XCTAssertTrue(app.buttons["holeOverview"].exists)
            XCTAssertTrue(any(app, "landingTarget").label.contains("LANDING"))
            usleep(500_000)
            screenshot(app, "Routed course and proportional ball — \(difficulty)")
            app.buttons["holeOverview"].tap()
            XCTAssertTrue(any(app, "targetMap").waitForExistence(timeout: 5))
            screenshot(app, "Full hole routing — \(difficulty)")
            app.buttons["Aim at pin"].tap()
            XCTAssertTrue(any(app, "plannedTarget").label.contains("Pin"))
            app.buttons["Follow fairway"].tap()
            XCTAssertTrue(any(app, "plannedTarget").label.contains("Landing"))
            app.buttons["Done"].tap()
            app.terminate()
        }
    }

    func testPhysicalHoleVisibilityAndManualAim() throws {
        #if targetEnvironment(simulator)
        throw XCTSkip("Physical iPhone readability check")
        #else
        let app = XCUIApplication()
        app.launchArguments = ["-skipPlayerCalibration", "-range.swingInput", "touch", "-gestures.enabled", "NO"]
        app.launch()
        XCTAssertTrue(app.buttons["menuSolo"].waitForExistence(timeout: 15))
        app.buttons["menuSolo"].tap()
        XCTAssertTrue(app.buttons["continueToCourses"].waitForExistence(timeout: 5))
        app.buttons["continueToCourses"].tap()
        XCTAssertTrue(app.buttons["course-easy"].waitForExistence(timeout: 5))
        app.buttons["course-easy"].tap()
        let cue = any(app, "holeDirectionCue")
        XCTAssertTrue(cue.waitForExistence(timeout: 10))
        usleep(1_800_000)
        screenshot(app, "Physical iPhone — distant hole beacon and direction")
        let original = cue.label
        let right = app.buttons["aimRight"]
        XCTAssertGreaterThanOrEqual(right.frame.width, 44)
        XCTAssertGreaterThanOrEqual(right.frame.height, 44)
        right.tap(); right.tap(); right.tap()
        XCTAssertNotEqual(cue.label, original)
        screenshot(app, "Physical iPhone — manual right aim and hole bearing")
        app.buttons["holeOverview"].tap()
        XCTAssertTrue(app.buttons["Aim at pin"].waitForExistence(timeout: 5))
        app.buttons["Aim at pin"].tap()
        app.buttons["Done"].tap()
        XCTAssertTrue(cue.waitForExistence(timeout: 5))
        XCTAssertTrue(cue.label.contains("on your aim line"))
        usleep(800_000)
        screenshot(app, "Physical iPhone — pin aligned with predicted shot")
        #endif
    }

    @MainActor
    func testPlayAHoleOutWithTheTouchPad() {
        let app = XCUIApplication()
        // This test presses Next shot and Continue itself; play's automatic progression
        // would otherwise move on to the next hole before it looks.
        app.launchArguments += ["-skipPlayerCalibration", "-manualProgression", "-flightTimeScale", "4"]
        app.launch()
        XCTAssertTrue(app.buttons["menuSolo"].waitForExistence(timeout: 15))
        app.buttons["menuSolo"].tap()
        app.buttons["continueToCourses"].tap()
        app.buttons["course-easy"].tap()
        let pad = app.otherElements["swingPad"]
        XCTAssertTrue(pad.waitForExistence(timeout: 15))
        screenshot(app, "Easy hole 1 tee")

        // The first swing: hero shot of the golfer, then the chase camera.
        pad.coordinate(withNormalizedOffset: CGVector(dx: 0.5, dy: 0.1))
            .press(forDuration: 0.1, thenDragTo: pad.coordinate(withNormalizedOffset: CGVector(dx: 0.5, dy: 0.75)))
        usleep(500_000)
        screenshot(app, "Hero shot after impact")
        sleep(2)
        screenshot(app, "Chasing the ball")
        XCTAssertTrue(app.buttons["nextShot"].waitForExistence(timeout: 25) || app.buttons["continueHole"].exists)
        screenshot(app, "Ball landing")

        var replayed = false
        var puttShown = false
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
            if !puttShown, app.buttons["club-putter"].isSelected {
                puttShown = true
                sleep(1)
                screenshot(app, "Putting view on the green")
            }
            if app.buttons["puttStroke"].exists {
                app.buttons["puttStroke"].tap()
            } else {
                pad.coordinate(withNormalizedOffset: CGVector(dx: 0.5, dy: 0.1))
                    .press(forDuration: 0.1, thenDragTo: pad.coordinate(withNormalizedOffset: CGVector(dx: 0.5, dy: 0.9)))
            }
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
        app.launchArguments += ["-skipPlayerCalibration", "-fixturePlayers", "3"]
        app.launch()
        XCTAssertTrue(app.buttons["menuMultiplayer"].waitForExistence(timeout: 15))
        app.buttons["menuMultiplayer"].tap()
        XCTAssertTrue(app.buttons["continueToCourses"].waitForExistence(timeout: 5))
        XCTAssertTrue(app.buttons["addPlayer"].exists)
        screenshot(app, "Multiplayer setup")
        app.buttons["continueToCourses"].tap()
        app.buttons["course-medium"].tap()
        // The banner shows for under two seconds; look for its text in one wait.
        XCTAssertTrue(app.staticTexts["PLAYER 1'S TURN"].waitForExistence(timeout: 15))
        XCTAssertTrue(any(app, "turnBanner").exists)
        screenshot(app, "Player 1 turn banner")
        sleep(2)
        screenshot(app, "Friends standing around the golfer")
    }
}
