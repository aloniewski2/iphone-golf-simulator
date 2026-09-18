import XCTest

/// Camera-mode play: the locked ball review, stance guides, recentering and certified swings.
/// Runs on its own CI worker alongside `CoursePlayTests`.
final class CameraPlayTests: XCTestCase {
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
        let stage = any(app, "cameraStage")
        XCTAssertTrue(stage.waitForExistence(timeout: 5))
        XCTAssertEqual(stage.value as? String, "expanded", "camera setup starts large before certification")
        XCTAssertTrue(any(app, "readyPrompt").exists)
        XCTAssertTrue(any(app, "cameraPanel").waitForExistence(timeout: 5))
        XCTAssertTrue((stage.value as? String ?? "").hasSuffix("expanded"), "stage: \(stage.value ?? "")")
        XCTAssertTrue(app.buttons["club-driver"].exists)
        sleep(1)
        screenshot(app, "Camera starts expanded before certification")
        XCTAssertTrue(any(app, "readyPrompt").waitForExistence(timeout: 5))
        XCTAssertTrue((stage.value as? String ?? "").hasSuffix("expanded"), "stage: \(stage.value ?? "")")
        XCTAssertTrue(app.buttons["recenterGrip"].isHittable)
        app.buttons["recenterGrip"].tap()
        XCTAssertEqual(stage.value as? String, "expanded", "recentering does not accidentally collapse setup")
        screenshot(app, "Camera setup with recenter and tracking guidance")
        app.buttons["minimizeCamera"].tap()
        XCTAssertTrue((stage.value as? String ?? "").hasSuffix("minimized"), "stage: \(stage.value ?? "")")
        app.buttons["useTouch"].tap()
        XCTAssertTrue(any(app, "swingPad").waitForExistence(timeout: 5), "camera failure always has an in-play fallback")
        app.buttons["shotControls"].tap()
        XCTAssertTrue(any(app, "targetMap").waitForExistence(timeout: 5))
        screenshot(app, "Choose a target and shot")
        app.buttons["Done"].tap()
        app.buttons["club-putter"].tap()
        XCTAssertTrue(any(app, "swingPad").label.contains("Putting"))
        XCTAssertTrue(app.sliders["puttPower"].exists)
        screenshot(app, "Precision putting controls")
        app.buttons["puttStroke"].tap()
        XCTAssertTrue(app.buttons["nextShot"].waitForExistence(timeout: 10))
    }

    @MainActor
    func testCertifiedBallExpandsThenContractsWithoutResetting() {
        let app = XCUIApplication()
        // A longer review than play uses: the sequence is what is under test, and a CI runner's
        // accessibility snapshots are far slower than a five-second countdown.
        app.launchArguments += ["-skipPlayerCalibration", "-startInCameraMode", "-fixtureLockedCamera", "-positionReviewSeconds", "20", "-manualProgression"]
        app.launch()
        XCTAssertTrue(app.buttons["menuSolo"].waitForExistence(timeout: 15))
        app.buttons["menuSolo"].tap()
        XCTAssertTrue(app.buttons["continueToCourses"].waitForExistence(timeout: 10))
        app.buttons["continueToCourses"].tap()
        XCTAssertTrue(app.buttons["course-easy"].waitForExistence(timeout: 10))
        app.buttons["course-easy"].tap()
        let stage = any(app, "cameraStage")
        func journal() -> String { stage.exists ? (stage.value as? String ?? "") : "" }
        waitUntil(20, "the stage to open") { journal().hasPrefix("expanded") }
        XCTAssertGreaterThan(stage.frame.width, app.frame.width * 0.95)
        // The stage opens large before the fixture has locked the ball; the lock follows shortly.
        XCTAssertTrue(any(app, "lockedBallOverlay").waitForExistence(timeout: 15))
        let preview = any(app, "liveCameraPreview")
        let previewIdentity = (preview.value as? String)?.components(separatedBy: ";").first
        XCTAssertNotNil(previewIdentity)
        screenshot(app, "Certified ball full-screen countdown — synthetic fixture")
        XCTAssertFalse(app.buttons["nextShot"].exists, "the setup rehearsal cannot score")
        // The stage journals its states, so a slow runner reads the whole review afterwards:
        // it stayed large through the countdown (a second of full framing to orient, the middle
        // zoomed on the ground ball, a second back at full framing) and only then shrank.
        waitUntil(60, "the review to finish") { journal().hasSuffix("minimized") }
        XCTAssertTrue(journal().hasSuffix("review>expanded>closeup>expanded>minimized"), "stage journal: \(journal())")
        XCTAssertEqual((preview.value as? String)?.components(separatedBy: ";").first, previewIdentity,
                       "zooming must preserve the same live preview")
        usleep(600_000)
        XCTAssertTrue(stage.label.contains("full frame"))
        XCTAssertTrue(any(app, "lockedBallOverlay").exists)
        XCTAssertTrue(any(app, "cameraPanel").label.contains("Ready"))
        XCTAssertEqual((preview.value as? String)?.components(separatedBy: ";").first, previewIdentity,
                       "resizing must not destroy the live preview layer")
        screenshot(app, "Certified ball retained after contraction — synthetic fixture")
        XCTAssertEqual(any(app, "golfCourse").value as? String, "scene=1;audio=1;haptics=1",
                       "camera redraws must not construct discarded hardware engines")
        XCTAssertFalse(app.buttons["nextShot"].exists, "certification cannot launch a shot")
        preview.tap()
        XCTAssertTrue(app.buttons["minimizeCamera"].waitForExistence(timeout: 3))
        XCTAssertEqual((preview.value as? String)?.components(separatedBy: ";").first, previewIdentity)
        app.buttons["minimizeCamera"].tap()
        app.buttons["useTouch"].tap()
        app.buttons["club-putter"].tap()
        app.buttons["puttStroke"].tap()
        XCTAssertTrue(app.buttons["nextShot"].waitForExistence(timeout: 10), "touch fallback still works after camera transitions")
    }

    func testHandednessGuideUsesSameCameraPosition() {
        for hand in ["Left", "Right"] {
            let app = XCUIApplication()
            app.launchArguments = ["-skipPlayerCalibration", "-startInCameraMode"]
            app.launch()
            XCTAssertTrue(app.buttons["menuSolo"].waitForExistence(timeout: 15))
            app.buttons["menuSolo"].tap()
            XCTAssertTrue(app.buttons[hand].waitForExistence(timeout: 5))
            app.buttons[hand].tap()
            app.buttons["continueToCourses"].tap()
            app.buttons["course-easy"].tap()
            let guide = any(app, "cameraStanceGuide")
            XCTAssertTrue(guide.waitForExistence(timeout: 10))
            XCTAssertTrue(guide.label.contains("\(hand)-handed · chest toward phone"))
            screenshot(app, "\(hand)-handed chest-facing setup")
            app.terminate()
        }
    }

    func testRecenterDuringReviewRestartsConfirmationWithoutRehearsal() {
        let app = XCUIApplication()
        app.launchArguments = ["-skipPlayerCalibration", "-startInCameraMode", "-fixtureLockedCamera", "-positionReviewSeconds", "40"]
        app.launch()
        XCTAssertTrue(app.buttons["menuSolo"].waitForExistence(timeout: 15))
        app.buttons["menuSolo"].tap()
        XCTAssertTrue(app.buttons["continueToCourses"].waitForExistence(timeout: 10))
        app.buttons["continueToCourses"].tap()
        XCTAssertTrue(app.buttons["course-easy"].waitForExistence(timeout: 10))
        app.buttons["course-easy"].tap()
        let stage = any(app, "cameraStage")
        func journal() -> String { stage.exists ? (stage.value as? String ?? "") : "" }
        waitUntil(25, "the review countdown to begin") { journal().contains("review") }
        // Give the review a few seconds, then recenter in the middle of it: the review must
        // start over from the top rather than replaying a rehearsal. The 40 s review leaves a
        // slow runner time to find the button while the stage is still up.
        sleep(6)
        XCTAssertTrue(app.buttons["recenterGrip"].waitForExistence(timeout: 15))
        app.buttons["recenterGrip"].tap()
        waitUntil(90, "the second review to finish") { journal().hasSuffix("minimized") }
        let log = journal()
        XCTAssertEqual(log.components(separatedBy: "review").count - 1, 2, "two reviews, the second started by recentering: \(log)")
        XCTAssertTrue(log.hasSuffix("review>expanded>closeup>expanded>minimized"), "the second review ran in full: \(log)")
        XCTAssertFalse(app.buttons["nextShot"].exists)
        XCTAssertTrue(any(app, "cameraPanel").label.contains("Ready"))
    }

    func testCertifiedCameraSwingCompletesAtHighFrameRate() {
        assertCertifiedCameraSwing(hand: "Right")
    }

    func testLeftHandedCertifiedCameraSwingCompletes() {
        assertCertifiedCameraSwing(hand: "Left")
    }

    private func assertCertifiedCameraSwing(hand: String) {
        let app = XCUIApplication()
        app.launchArguments = ["-skipPlayerCalibration", "-startInCameraMode", "-fixtureLockedCamera", "-fixtureSwingAfterLock", "-manualProgression", "-flightTimeScale", "4"]
        app.launch()
        XCTAssertTrue(app.buttons["menuSolo"].waitForExistence(timeout: 15))
        app.buttons["menuSolo"].tap()
        XCTAssertTrue(app.buttons[hand].waitForExistence(timeout: 5))
        app.buttons[hand].tap()
        app.buttons["continueToCourses"].tap()
        app.buttons["course-easy"].tap()
        XCTAssertTrue(any(app, "lockedBallOverlay").waitForExistence(timeout: 5))
        let stage = any(app, "cameraStage")
        waitUntil(30, "the stage to minimize after the review") { stage.exists && (stage.value as? String ?? "").hasSuffix("minimized") }
        screenshot(app, "\(hand)-handed mirrored golfer — synthetic fixture")
        XCTAssertTrue(app.buttons["nextShot"].waitForExistence(timeout: 25),
                      "camera detector → shot → flight → result must run under continuous 60 Hz updates")
        XCTAssertEqual(any(app, "golfCourse").value as? String, "scene=1;audio=1;haptics=1")
        screenshot(app, "\(hand)-handed camera swing completed — synthetic high-frequency fixture")
    }

    func testPhysicalCameraPreviewSurvivesResizing() throws {
        #if targetEnvironment(simulator)
        throw XCTSkip("Requires an unlocked physical iPhone and real camera; never a synthetic fixture")
        #else
        let app = XCUIApplication()
        // Use the player's real saved calibration, not the fixture roster or synthetic poses.
        app.launchArguments = ["-range.swingInput", "camera", "-gestures.enabled", "NO"]
        app.launch()
        XCTAssertTrue(app.buttons["menuSolo"].waitForExistence(timeout: 15))
        app.buttons["menuSolo"].tap()
        let proceed = app.buttons["continueToCourses"]
        XCTAssertTrue(proceed.waitForExistence(timeout: 5))
        XCTAssertTrue(proceed.isEnabled, "Physical validation requires the player's saved scan")
        guard proceed.isEnabled else { return }
        proceed.tap()
        app.buttons["course-easy"].tap()
        let preview = any(app, "liveCameraPreview")
        XCTAssertTrue(preview.waitForExistence(timeout: 10))
        let rendering = XCTNSPredicateExpectation(predicate: NSPredicate(format: "value CONTAINS 'rendering=1'"), object: preview)
        XCTAssertEqual(XCTWaiter.wait(for: [rendering], timeout: 15), .completed)
        let identity = (preview.value as? String)?.components(separatedBy: ";").first
        XCTAssertGreaterThan(preview.frame.width, 100)
        screenshot(app, "Physical iPhone live setup")
        // Let the person hold a grip, then require the actual detector to certify and contract.
        XCTAssertTrue(any(app, "lockedBallOverlay").waitForExistence(timeout: 45))
        screenshot(app, "Physical iPhone five-second ground-ball confirmation")
        let stage = any(app, "cameraStage")
        let minimized = XCTNSPredicateExpectation(predicate: NSPredicate(format: "value ENDSWITH 'minimized'"), object: stage)
        XCTAssertEqual(XCTWaiter.wait(for: [minimized], timeout: 8), .completed)
        usleep(700_000)
        for _ in 0..<2 {
            XCTAssertEqual((preview.value as? String)?.components(separatedBy: ";").first, identity)
            XCTAssertTrue((preview.value as? String)?.contains("rendering=1") == true)
            preview.tap()
            XCTAssertTrue(app.buttons["minimizeCamera"].waitForExistence(timeout: 3))
            app.buttons["minimizeCamera"].tap()
            usleep(700_000) // Hit the final inset position, not the moving expansion/shrink layer.
        }
        screenshot(app, "Physical iPhone live preview and club after contraction")
        XCTAssertTrue(any(app, "lockedBallOverlay").exists)
        XCTAssertEqual(any(app, "golfCourse").value as? String, "scene=1;audio=1;haptics=1")
        #endif
    }

    func testPhysicalCameraSwingReachesShotResult() throws {
        #if targetEnvironment(simulator)
        throw XCTSkip("Requires a person taking an intentional swing in front of the physical iPhone")
        #else
        let app = XCUIApplication()
        app.launchArguments = ["-range.swingInput", "camera", "-gestures.enabled", "NO"]
        app.launch()
        XCTAssertTrue(app.buttons["menuSolo"].waitForExistence(timeout: 15))
        app.buttons["menuSolo"].tap()
        XCTAssertTrue(app.buttons["continueToCourses"].waitForExistence(timeout: 5))
        guard app.buttons["continueToCourses"].isEnabled else { XCTFail("Needs saved player scan"); return }
        app.buttons["continueToCourses"].tap()
        app.buttons["course-easy"].tap()
        XCTAssertTrue(any(app, "lockedBallOverlay").waitForExistence(timeout: 45))
        screenshot(app, "Physical swing check — certified ball")
        let stage = any(app, "cameraStage")
        let minimized = XCTNSPredicateExpectation(predicate: NSPredicate(format: "value ENDSWITH 'minimized'"), object: stage)
        XCTAssertEqual(XCTWaiter.wait(for: [minimized], timeout: 8), .completed, "No rehearsal gate should block the five-second confirmation")
        screenshot(app, "Physical swing check — idle avatar after automatic contraction")
        XCTAssertTrue(any(app, "trajectoryEstimate").exists, "Ready state must show the trajectory estimate")
        XCTAssertTrue(app.buttons["nextShot"].waitForExistence(timeout: 60), "An intentional real-camera swing must reach its shot result")
        screenshot(app, "Physical swing check — shot result")
        XCTAssertTrue(any(app, "trajectoryEstimate").waitForExistence(timeout: 10), "Next shot must become ready without tapping a button")
        XCTAssertFalse(app.buttons["nextShot"].exists)
        screenshot(app, "Physical swing check — automatic next shot and selected club")
        XCTAssertEqual(any(app, "golfCourse").value as? String, "scene=1;audio=1;haptics=1")
        #endif
    }

    func testPhysicalNormalLaunchCameraPlay() throws {
        #if targetEnvironment(simulator)
        throw XCTSkip("Requires a saved player and an intentional real-camera swing on iPhone")
        #else
        let app = XCUIApplication()
        // Exercise saved settings, not input overrides or synthetic fixtures. This flag only
        // retains local joint-confidence/event diagnostics for this explicit physical test.
        app.launchArguments = ["-recordCameraTrace"]
        app.launch()
        XCTAssertTrue(app.buttons["menuSettings"].waitForExistence(timeout: 15))
        app.buttons["menuSettings"].tap()
        let input = app.buttons.matching(NSPredicate(format: "label BEGINSWITH %@", "Swing input")).firstMatch
        XCTAssertTrue(input.waitForExistence(timeout: 5))
        input.tap()
        app.buttons["Camera"].tap()
        app.buttons["Done"].tap()
        app.terminate()
        app.launch()
        XCTAssertTrue(app.buttons["menuSolo"].waitForExistence(timeout: 15))
        app.buttons["menuSolo"].tap()
        XCTAssertTrue(app.buttons["continueToCourses"].waitForExistence(timeout: 5))
        guard app.buttons["continueToCourses"].isEnabled else { XCTFail("Needs saved player scan"); return }
        app.buttons["continueToCourses"].tap()
        app.buttons["course-easy"].tap()
        let stage = any(app, "cameraStage")
        XCTAssertTrue(stage.waitForExistence(timeout: 10), "Camera preference must survive an ordinary relaunch")
        XCTAssertTrue((stage.value as? String ?? "").hasSuffix("expanded"), "stage: \(stage.value ?? "")")
        let preview = any(app, "liveCameraPreview")
        let rendering = XCTNSPredicateExpectation(predicate: NSPredicate(format: "value CONTAINS 'rendering=1'"), object: preview)
        XCTAssertEqual(XCTWaiter.wait(for: [rendering], timeout: 15), .completed)
        XCTAssertTrue(any(app, "lockedBallOverlay").waitForExistence(timeout: 45))
        screenshot(app, "Normal launch — certified ground ball")
        let closeUp = XCTNSPredicateExpectation(predicate: NSPredicate(format: "label CONTAINS 'ground close-up'"), object: stage)
        XCTAssertEqual(XCTWaiter.wait(for: [closeUp], timeout: 4), .completed)
        screenshot(app, "Normal launch — ground ball and feet close-up")
        let minimized = XCTNSPredicateExpectation(predicate: NSPredicate(format: "value ENDSWITH 'minimized'"), object: stage)
        XCTAssertEqual(XCTWaiter.wait(for: [minimized], timeout: 8), .completed)
        XCTAssertTrue((preview.value as? String)?.contains("rendering=1") == true)
        screenshot(app, "Normal launch — live camera after contraction")
        XCTAssertTrue(app.buttons["nextShot"].waitForExistence(timeout: 60), "A real swing must reach a result without launch overrides")
        screenshot(app, "Normal launch — real swing result")
        #endif
    }
}
