import XCTest

@MainActor
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
        XCTAssertTrue(app.buttons["Save"].waitForExistence(timeout: 30), "the system export sheet takes a while on a fresh simulator")
        screenshot(app, "Practice Lab JSON export")
        // Dismiss the native file sheet without saving a test file or assuming a Cancel label.
        app.coordinate(withNormalizedOffset: CGVector(dx: 0.5, dy: 0.09))
            .press(forDuration: 0.1, thenDragTo: app.coordinate(withNormalizedOffset: CGVector(dx: 0.5, dy: 0.9)))
        XCTAssertFalse(app.buttons["Save"].exists)
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
        XCTAssertEqual(stage.value as? String, "expanded")
        XCTAssertTrue(app.buttons["club-driver"].exists)
        sleep(1)
        screenshot(app, "Camera starts expanded before certification")
        XCTAssertTrue(any(app, "readyPrompt").waitForExistence(timeout: 5))
        XCTAssertEqual(stage.value as? String, "expanded")
        XCTAssertTrue(app.buttons["recenterGrip"].isHittable)
        app.buttons["recenterGrip"].tap()
        XCTAssertEqual(stage.value as? String, "expanded", "recentering does not accidentally collapse setup")
        screenshot(app, "Camera setup with recenter and tracking guidance")
        app.buttons["minimizeCamera"].tap()
        XCTAssertEqual(stage.value as? String, "minimized")
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
        app.launchArguments += ["-skipPlayerCalibration", "-startInCameraMode", "-fixtureLockedCamera", "-positionReviewSeconds", "14", "-manualProgression"]
        app.launch()
        XCTAssertTrue(app.buttons["menuSolo"].waitForExistence(timeout: 15))
        app.buttons["menuSolo"].tap()
        XCTAssertTrue(app.buttons["continueToCourses"].waitForExistence(timeout: 10))
        app.buttons["continueToCourses"].tap()
        XCTAssertTrue(app.buttons["course-easy"].waitForExistence(timeout: 10))
        app.buttons["course-easy"].tap()
        let stage = any(app, "cameraStage")
        let expanded = XCTNSPredicateExpectation(predicate: NSPredicate(format: "value == 'expanded'"), object: stage)
        XCTAssertEqual(XCTWaiter.wait(for: [expanded], timeout: 20), .completed)
        XCTAssertGreaterThan(stage.frame.width, app.frame.width * 0.95)
        XCTAssertTrue(any(app, "lockedBallOverlay").exists)
        let preview = any(app, "liveCameraPreview")
        let previewIdentity = (preview.value as? String)?.components(separatedBy: ";").first
        XCTAssertNotNil(previewIdentity)
        let reviewing = XCTNSPredicateExpectation(predicate: NSPredicate(format: "label CONTAINS 'Position confirmed'"),
                                                   object: app.staticTexts["cameraSetupStatus"])
        XCTAssertEqual(XCTWaiter.wait(for: [reviewing], timeout: 20), .completed)
        XCTAssertFalse(app.buttons["nextShot"].exists, "the setup rehearsal cannot score")
        screenshot(app, "Certified ball full-screen countdown — synthetic fixture")
        // Partway through the review the stage is still large and zoomed on the ground ball.
        let closeUp = XCTNSPredicateExpectation(
            predicate: NSPredicate(format: "value == 'expanded' AND label CONTAINS 'ground close-up'"), object: stage)
        XCTAssertEqual(XCTWaiter.wait(for: [closeUp], timeout: 20), .completed, "the ground marker stays large for the review")
        screenshot(app, "Ground ball and feet close-up — synthetic fixture")
        XCTAssertEqual((preview.value as? String)?.components(separatedBy: ";").first, previewIdentity,
                       "zooming must preserve the same live preview")
        let minimized = XCTNSPredicateExpectation(predicate: NSPredicate(format: "value == 'minimized'"), object: stage)
        XCTAssertEqual(XCTWaiter.wait(for: [minimized], timeout: 25), .completed)
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
        app.launchArguments = ["-skipPlayerCalibration", "-startInCameraMode", "-fixtureLockedCamera", "-positionReviewSeconds", "12"]
        app.launch()
        XCTAssertTrue(app.buttons["menuSolo"].waitForExistence(timeout: 15))
        app.buttons["menuSolo"].tap()
        XCTAssertTrue(app.buttons["continueToCourses"].waitForExistence(timeout: 10))
        app.buttons["continueToCourses"].tap()
        XCTAssertTrue(app.buttons["course-easy"].waitForExistence(timeout: 10))
        app.buttons["course-easy"].tap()
        let status = app.staticTexts["cameraSetupStatus"]
        let reviewing = XCTNSPredicateExpectation(predicate: NSPredicate(format: "label CONTAINS 'Position confirmed'"), object: status)
        XCTAssertEqual(XCTWaiter.wait(for: [reviewing], timeout: 25), .completed)
        // Let the countdown run well down, then recenter: it must start over from the top
        // rather than replaying a rehearsal.
        let runDown = XCTNSPredicateExpectation(predicate: NSPredicate(format: "label MATCHES '.*· [1-8]$'"), object: status)
        XCTAssertEqual(XCTWaiter.wait(for: [runDown], timeout: 25), .completed)
        XCTAssertTrue(app.buttons["recenterGrip"].waitForExistence(timeout: 10))
        app.buttons["recenterGrip"].tap()
        let restarted = XCTNSPredicateExpectation(predicate: NSPredicate(format: "label MATCHES '.*· 1[0-2]$'"), object: status)
        XCTAssertEqual(XCTWaiter.wait(for: [restarted], timeout: 25), .completed, "the countdown restarts from the full review length")
        XCTAssertEqual(any(app, "cameraStage").value as? String, "expanded")
        XCTAssertFalse(app.buttons["nextShot"].exists)
        let minimized = XCTNSPredicateExpectation(predicate: NSPredicate(format: "value == 'minimized'"), object: any(app, "cameraStage"))
        XCTAssertEqual(XCTWaiter.wait(for: [minimized], timeout: 30), .completed)
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
        app.launchArguments = ["-skipPlayerCalibration", "-startInCameraMode", "-fixtureLockedCamera", "-fixtureSwingAfterLock", "-manualProgression"]
        app.launch()
        XCTAssertTrue(app.buttons["menuSolo"].waitForExistence(timeout: 15))
        app.buttons["menuSolo"].tap()
        XCTAssertTrue(app.buttons[hand].waitForExistence(timeout: 5))
        app.buttons[hand].tap()
        app.buttons["continueToCourses"].tap()
        app.buttons["course-easy"].tap()
        XCTAssertTrue(any(app, "lockedBallOverlay").waitForExistence(timeout: 5))
        let stage = any(app, "cameraStage")
        let minimized = XCTNSPredicateExpectation(predicate: NSPredicate(format: "value == 'minimized'"), object: stage)
        XCTAssertEqual(XCTWaiter.wait(for: [minimized], timeout: 15), .completed)
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
        let minimized = XCTNSPredicateExpectation(predicate: NSPredicate(format: "value == 'minimized'"), object: stage)
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
        let minimized = XCTNSPredicateExpectation(predicate: NSPredicate(format: "value == 'minimized'"), object: stage)
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
        XCTAssertEqual(stage.value as? String, "expanded")
        let preview = any(app, "liveCameraPreview")
        let rendering = XCTNSPredicateExpectation(predicate: NSPredicate(format: "value CONTAINS 'rendering=1'"), object: preview)
        XCTAssertEqual(XCTWaiter.wait(for: [rendering], timeout: 15), .completed)
        XCTAssertTrue(any(app, "lockedBallOverlay").waitForExistence(timeout: 45))
        screenshot(app, "Normal launch — certified ground ball")
        let closeUp = XCTNSPredicateExpectation(predicate: NSPredicate(format: "label CONTAINS 'ground close-up'"), object: stage)
        XCTAssertEqual(XCTWaiter.wait(for: [closeUp], timeout: 4), .completed)
        screenshot(app, "Normal launch — ground ball and feet close-up")
        let minimized = XCTNSPredicateExpectation(predicate: NSPredicate(format: "value == 'minimized'"), object: stage)
        XCTAssertEqual(XCTWaiter.wait(for: [minimized], timeout: 8), .completed)
        XCTAssertTrue((preview.value as? String)?.contains("rendering=1") == true)
        screenshot(app, "Normal launch — live camera after contraction")
        XCTAssertTrue(app.buttons["nextShot"].waitForExistence(timeout: 60), "A real swing must reach a result without launch overrides")
        screenshot(app, "Normal launch — real swing result")
        #endif
    }

    @MainActor
    func testPlayAHoleOutWithTheTouchPad() {
        let app = XCUIApplication()
        // This test presses Next shot and Continue itself; play's automatic progression
        // would otherwise move on to the next hole before it looks.
        app.launchArguments += ["-skipPlayerCalibration", "-manualProgression"]
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
