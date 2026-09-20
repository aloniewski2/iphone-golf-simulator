import XCTest

@MainActor
final class RangePlayTests: XCTestCase {
    func testNativeArcadeBallReadabilityAtAddressAndInFlight() {
        continueAfterFailure = false
        let app = XCUIApplication()
        app.launchArguments = ["-realityKit", "-manualProgression", "-fixturePlayers", "1", "-startCourse", "sunward-resort-v1", "-range.swingInput", "touch"]
        app.launch()
        let swing = app.buttons["nativeTouchSwing"]
        XCTAssertTrue(swing.waitForExistence(timeout: 30))
        let ready = XCTNSPredicateExpectation(predicate: NSPredicate { _, _ in swing.isEnabled }, object: nil)
        XCTAssertEqual(XCTWaiter.wait(for: [ready], timeout: 45), .completed)
        screenshot(app, "Arcade ball — Sunward address")
        swing.tap()
        XCTAssertTrue(app.buttons["Skip flight"].waitForExistence(timeout: 10))
        let start = Date()
        let flight = XCTNSPredicateExpectation(predicate: NSPredicate { _, _ in Date().timeIntervalSince(start) >= 1.2 }, object: nil)
        XCTAssertEqual(XCTWaiter.wait(for: [flight], timeout: 5), .completed)
        screenshot(app, "Arcade ball — Sunward flight")
        XCTAssertTrue(app.buttons["Replay"].waitForExistence(timeout: 40))
        screenshot(app, "Arcade ball — Sunward resting lie")
        app.buttons["Next shot"].tap()
        XCTAssertTrue(app.staticTexts["nativeTurnStatus"].label.contains("Stroke 2"))
    }

    func testOnlyMainCourseIsSelectableForSoloAndMultiplayer() {
        continueAfterFailure = false
        for mode in ["menuSolo", "menuMultiplayer"] {
            let app = XCUIApplication()
            app.launchArguments = ["-realityKit", "-fixturePlayers", "2", "-range.swingInput", "touch"]
            app.launch()
            XCTAssertTrue(app.buttons[mode].waitForExistence(timeout: 15))
            app.buttons[mode].tap()
            XCTAssertTrue(app.buttons["continueToCourses"].waitForExistence(timeout: 5))
            app.buttons["continueToCourses"].tap()
            let resort = app.buttons["course-sunward-resort-v1"]
            XCTAssertTrue(resort.waitForExistence(timeout: 10))
            XCTAssertEqual(app.buttons.matching(NSPredicate(format: "identifier BEGINSWITH %@", "course-")).count, 1)
            for hidden in ["course-easy", "course-medium", "course-hard", "course-sunward"] {
                XCTAssertFalse(app.buttons[hidden].exists)
            }
            screenshot(app, "Only Sunward Resort offered — \(mode)")
            resort.tap()
            let swing = app.buttons["nativeTouchSwing"]
            XCTAssertTrue(swing.waitForExistence(timeout: 30))
            let ready = XCTNSPredicateExpectation(predicate: NSPredicate { _, _ in swing.isEnabled }, object: nil)
            XCTAssertEqual(XCTWaiter.wait(for: [ready], timeout: 45), .completed)
            XCTAssertTrue(app.navigationBars["Sunward Resort · Nine"].exists)
            app.terminate()
        }
    }

    func testNativeRealityKitTouchShotAndReplay() {
        continueAfterFailure = false
        let app = XCUIApplication()
        app.launchArguments = ["-realityKit", "-manualProgression", "-fixturePlayers", "1", "-startCourse", "meadow", "-phoneController"]
        app.launch()
        XCTAssertTrue(app.otherElements["nativeViewport"].waitForExistence(timeout: 30))
        let ready = XCTNSPredicateExpectation(predicate: NSPredicate { _, _ in
            !app.staticTexts["nativeAssetStatus"].exists
        }, object: nil)
        XCTAssertEqual(XCTWaiter.wait(for: [ready], timeout: 45), .completed)
        screenshot(app, "RealityKit — converted Meadow Run and bound golfer")
        let touch = app.switches["nativeTouchControls"]
        for _ in 0..<5 where !touch.isHittable { app.scrollViews["nativeControls"].swipeUp() }
        XCTAssertTrue(touch.waitForExistence(timeout: 5))
        touch.coordinate(withNormalizedOffset: CGVector(dx: 0.9, dy: 0.5)).tap()
        XCTAssertEqual(touch.value as? String, "1")
        let swing = app.buttons["nativeTouchSwing"]
        if !swing.isHittable { app.swipeUp() }
        XCTAssertTrue(swing.isEnabled); swing.tap()
        let replay = app.buttons["Replay"]
        XCTAssertTrue(replay.waitForExistence(timeout: 35))
        XCTAssertTrue(app.staticTexts["shotLie"].exists)
        XCTAssertTrue(app.staticTexts["nativeShotExplanation"].exists)
        screenshot(app, "RealityKit — completed authoritative touch shot")
        replay.tap()
        XCTAssertTrue(app.buttons["Skip flight"].waitForExistence(timeout: 5))
        app.buttons["Skip flight"].tap()
        XCTAssertTrue(app.buttons["Next shot"].waitForExistence(timeout: 10))
        app.buttons["Next shot"].tap()
        app.swipeDown()
        XCTAssertTrue(app.staticTexts["nativeTurnStatus"].label.contains("Stroke 2"))
    }

    func testNativeAutomaticProgressionPauseAndReplay() {
        continueAfterFailure = false
        let app = XCUIApplication()
        app.launchArguments = ["-realityKit", "-fixturePlayers", "1", "-startCourse", "meadow",
                               "-range.swingInput", "touch"]
        app.launch()
        let swing = app.buttons["nativeTouchSwing"]
        XCTAssertTrue(swing.waitForExistence(timeout: 30))
        let ready = XCTNSPredicateExpectation(predicate: NSPredicate { _, _ in swing.isEnabled }, object: nil)
        XCTAssertEqual(XCTWaiter.wait(for: [ready], timeout: 45), .completed)
        swing.tap()
        XCTAssertTrue(app.buttons["Skip flight"].waitForExistence(timeout: 10))
        app.buttons["Skip flight"].tap()
        app.buttons["Pause"].tap()
        XCTAssertEqual(app.staticTexts["nativeAutoProgression"].label, "Auto-advance paused")
        let paused = XCTNSPredicateExpectation(predicate: NSPredicate { _, _ in swing.exists }, object: nil)
        paused.isInverted = true
        XCTAssertEqual(XCTWaiter.wait(for: [paused], timeout: 4), .completed)
        app.buttons["Resume"].tap()
        let advanced = XCTNSPredicateExpectation(predicate: NSPredicate { _, _ in swing.exists && swing.isEnabled }, object: nil)
        XCTAssertEqual(XCTWaiter.wait(for: [advanced], timeout: 10), .completed)
        XCTAssertTrue(app.staticTexts["nativeTurnStatus"].label.contains("Stroke 2"))
        swing.tap()
        XCTAssertTrue(app.buttons["Skip flight"].waitForExistence(timeout: 10))
        app.buttons["Skip flight"].tap()
        app.buttons["Replay"].tap()
        XCTAssertTrue(app.buttons["Skip flight"].waitForExistence(timeout: 10))
        app.buttons["Skip flight"].tap()
        XCTAssertTrue(app.staticTexts["nativeAutoProgression"].label.contains("Replay paused auto-advance"))
        let replayPaused = XCTNSPredicateExpectation(predicate: NSPredicate { _, _ in swing.exists }, object: nil)
        replayPaused.isInverted = true
        XCTAssertEqual(XCTWaiter.wait(for: [replayPaused], timeout: 4), .completed)
        app.buttons["Next shot"].tap()
        XCTAssertTrue(swing.waitForExistence(timeout: 10))
        XCTAssertTrue(app.staticTexts["nativeTurnStatus"].label.contains("Stroke 3"))
    }

    func testNativeAutomaticFinalScores() {
        continueAfterFailure = false
        let app = XCUIApplication()
        app.launchArguments = ["-realityKit", "-fixturePlayers", "1", "-startCourse", "sunward-resort-v1",
                               "-finalRoundFixture", "-range.swingInput", "touch"]
        app.launch()
        let swing = app.buttons["nativeTouchSwing"]
        XCTAssertTrue(swing.waitForExistence(timeout: 30))
        let ready = XCTNSPredicateExpectation(predicate: NSPredicate { _, _ in swing.isEnabled }, object: nil)
        XCTAssertEqual(XCTWaiter.wait(for: [ready], timeout: 45), .completed)
        swing.tap()
        XCTAssertTrue(app.staticTexts["roundComplete"].waitForExistence(timeout: 35))
        XCTAssertEqual(app.staticTexts["nativeRoundTotal-0"].label, "Player 1: 41 strokes · +5")
        XCTAssertEqual(app.staticTexts["nativeRoundBest"].label, "Best: +5")
        XCTAssertTrue(app.buttons["nativePlayAgain"].isHittable)
    }

    func testNativePracticeMenuAndReturnToScoredPlay() {
        continueAfterFailure = false
        let app = XCUIApplication()
        app.launchArguments = ["-realityKit", "-manualProgression", "-fixturePlayers", "1", "-range.swingInput", "touch"]
        app.launch()
        XCTAssertTrue(app.buttons["menuPractice"].waitForExistence(timeout: 15))
        app.buttons["menuPractice"].tap()
        let swing = app.buttons["nativeTouchSwing"]
        XCTAssertTrue(swing.waitForExistence(timeout: 30))
        let ready = XCTNSPredicateExpectation(predicate: NSPredicate { _, _ in swing.isEnabled }, object: nil)
        XCTAssertEqual(XCTWaiter.wait(for: [ready], timeout: 45), .completed)
        let status = app.staticTexts["nativeTurnStatus"].label
        XCTAssertTrue(app.staticTexts["nativeLieAndWind"].exists)
        for _ in 0..<2 {
            swing.tap()
            XCTAssertTrue(app.staticTexts["practiceResult"].waitForExistence(timeout: 5))
            XCTAssertTrue(app.staticTexts["practiceResult"].label.contains("no stroke counted"))
            XCTAssertEqual(app.staticTexts["nativeTurnStatus"].label, status)
            XCTAssertFalse(app.buttons["Replay"].exists)
        }
        let practice = app.switches["nativePracticeMode"]
        for _ in 0..<5 where !practice.isHittable { app.scrollViews["nativeControls"].swipeUp() }
        XCTAssertEqual(practice.value as? String, "1")
        screenshot(app, "Native practice — no score or ball movement")
        practice.coordinate(withNormalizedOffset: CGVector(dx: 0.9, dy: 0.5)).tap()
        XCTAssertEqual(practice.value as? String, "0")
        XCTAssertFalse(app.staticTexts["practiceResult"].exists)
        swing.tap()
        XCTAssertTrue(app.buttons["Replay"].waitForExistence(timeout: 35))
        XCTAssertTrue(app.staticTexts["shotLie"].exists)
        screenshot(app, "Native shot result — lie and contact feedback")
        app.buttons["Next shot"].tap()
        XCTAssertTrue(app.staticTexts["nativeTurnStatus"].label.contains("Stroke 2"))
    }

    func testNativeFinalScoresSoloWinnerTieAndRestart() {
        continueAfterFailure = false
        defer { XCUIDevice.shared.orientation = .portrait }
        for mode in ["solo", "winner", "tie"] {
            XCUIDevice.shared.orientation = .portrait
            let app = XCUIApplication()
            app.launchArguments = ["-realityKit", "-manualProgression", "-fixturePlayers", mode == "solo" ? "1" : "4",
                                   "-startCourse", "sunward-resort-v1", "-finalRoundFixture",
                                   "-range.swingInput", "touch", "-display.landscapeTV", "NO"]
            if mode == "tie" { app.launchArguments.append("-tiedRoundFixture") }
            app.launch()
            let swing = app.buttons["nativeTouchSwing"]
            XCTAssertTrue(swing.waitForExistence(timeout: 30))
            let ready = XCTNSPredicateExpectation(predicate: NSPredicate { _, _ in
                swing.isEnabled && app.staticTexts["nativeSuggestedPower"].exists
            }, object: nil)
            XCTAssertEqual(XCTWaiter.wait(for: [ready], timeout: 45), .completed)
            swing.tap()
            XCTAssertTrue(app.buttons["Final scores"].waitForExistence(timeout: 35))
            XCTAssertEqual(app.staticTexts["holeResult"].label, "Picked up")
            app.buttons["Final scores"].tap()
            let headline = app.staticTexts["roundComplete"]
            XCTAssertTrue(headline.waitForExistence(timeout: 5))
            XCTAssertEqual(headline.label, mode == "solo" ? "Round complete" : mode == "tie" ? "It's a tie" : "Player 1 wins")
            XCTAssertEqual(app.staticTexts["nativeRoundTotal-0"].label, "Player 1: 41 strokes · +5")
            if mode == "solo" {
                XCTAssertEqual(app.staticTexts["nativeRoundBest"].label, "Best: +5")
            } else {
                XCTAssertFalse(app.staticTexts["nativeRoundBest"].exists)
                XCTAssertEqual(app.staticTexts["nativeRoundTotal-3"].label,
                               mode == "tie" ? "Player 4: 41 strokes · +5" : "Player 4: 42 strokes · +6")
            }
            XCTAssertTrue(any(app, "scorecard").exists)
            let again = app.buttons["nativePlayAgain"]
            XCTAssertTrue(again.isHittable)
            screenshot(app, "Native final scores — \(mode)")
            if mode == "winner" {
                XCUIDevice.shared.orientation = .landscapeLeft
                let rotated = XCTNSPredicateExpectation(predicate: NSPredicate { _, _ in app.frame.width > app.frame.height }, object: nil)
                XCTAssertEqual(XCTWaiter.wait(for: [rotated], timeout: 5), .completed)
                XCTAssertTrue(again.isHittable)
                let capture = XCTAttachment(screenshot: XCUIScreen.main.screenshot())
                capture.name = "Native final scores — landscape winner"; capture.lifetime = .keepAlways; add(capture)
            }
            again.tap()
            let restarted = XCTNSPredicateExpectation(predicate: NSPredicate { _, _ in swing.isEnabled }, object: nil)
            XCTAssertEqual(XCTWaiter.wait(for: [restarted], timeout: 45), .completed)
            XCTAssertFalse(headline.exists)
            XCTAssertTrue(app.staticTexts["nativeTurnStatus"].label.contains("Stroke 1"))
            XCTAssertTrue(app.staticTexts.containing(NSPredicate(format: "label CONTAINS %@", "Player 1 · Hole 1")).firstMatch.exists)
            app.terminate()
        }
    }

    func testNativeAutomaticGreenPlanningStaysReadyThroughPlanSheet() {
        continueAfterFailure = false
        let app = XCUIApplication()
        app.launchArguments = ["-realityKit", "-manualProgression", "-fixturePlayers", "1", "-startCourse", "sunward-resort-v1",
                               "-startOnGreen", "-automaticAimFixture", "-range.swingInput", "touch"]
        app.launch()
        let swing = app.buttons["nativeTouchSwing"]
        XCTAssertTrue(swing.waitForExistence(timeout: 30))
        let ready = XCTNSPredicateExpectation(predicate: NSPredicate { _, _ in
            swing.isEnabled && app.staticTexts["nativeSuggestedPower"].exists &&
                !app.staticTexts["nativePuttPlanning"].exists
        }, object: nil)
        XCTAssertEqual(XCTWaiter.wait(for: [ready], timeout: 45), .completed)
        XCTAssertTrue(app.staticTexts["nativeLieAndWind"].label.contains("Green"))
        let recommendation = app.staticTexts["nativeSuggestedPower"].label
        let status = app.staticTexts["nativeTurnStatus"].label
        app.buttons["nativePlanShot"].tap()
        XCTAssertTrue(app.otherElements["holeOverviewMap"].waitForExistence(timeout: 10))
        screenshot(app, "Native automatic green planning — prepared line and pace")
        app.buttons["Done"].tap()
        XCTAssertEqual(app.staticTexts["nativeSuggestedPower"].label, recommendation)
        XCTAssertEqual(app.staticTexts["nativeTurnStatus"].label, status)
        XCTAssertTrue(swing.isEnabled)
        screenshot(app, "Native Sunward putt — worker recommendation ready")
        swing.tap()
        XCTAssertTrue(app.buttons["Replay"].waitForExistence(timeout: 35))
        XCTAssertFalse(app.staticTexts["nativePuttPlanning"].exists)
        XCTAssertEqual(app.staticTexts["holeResult"].label, "Hole in one!")
        screenshot(app, "Native Sunward putt — authoritative result")
        app.buttons["Continue"].tap()
        let nextHole = XCTNSPredicateExpectation(predicate: NSPredicate { _, _ in swing.isEnabled }, object: nil)
        XCTAssertEqual(XCTWaiter.wait(for: [nextHole], timeout: 45), .completed)
        XCTAssertTrue(app.staticTexts.containing(NSPredicate(format: "label CONTAINS %@", "Hole 2")).firstMatch.exists)
        XCTAssertTrue(app.staticTexts["nativeTurnStatus"].label.contains("Stroke 1"))
    }

    func testNativePlanningScorecardAndFlyoverPreserveStroke() {
        continueAfterFailure = false
        let app = XCUIApplication()
        app.launchArguments = ["-realityKit", "-fixturePlayers", "4", "-startCourse", "meadow", "-range.swingInput", "touch"]
        app.launch()
        let plan = app.buttons["nativePlanShot"]
        XCTAssertTrue(plan.waitForExistence(timeout: 30))
        let ready = XCTNSPredicateExpectation(predicate: NSPredicate { _, _ in plan.isEnabled }, object: nil)
        XCTAssertEqual(XCTWaiter.wait(for: [ready], timeout: 45), .completed)
        let before = app.staticTexts["nativeTurnStatus"].label
        plan.tap()
        XCTAssertTrue(app.otherElements["targetMap"].waitForExistence(timeout: 10))
        app.otherElements["targetMap"].coordinate(withNormalizedOffset: CGVector(dx: 0.35, dy: 0.45)).tap()
        XCTAssertTrue(app.staticTexts["plannedTarget"].label.uppercased().contains("TARGET"))
        screenshot(app, "Native target planning — worker trajectory")
        app.buttons["Done"].tap()
        XCTAssertEqual(app.staticTexts["nativeTurnStatus"].label, before)
        app.buttons["nativeScorecard"].tap()
        XCTAssertTrue(any(app, "scorecard").waitForExistence(timeout: 5))
        XCTAssertTrue(app.staticTexts["Player 4"].exists)
        app.buttons["Done"].tap()
        app.buttons["nativeFlyover"].tap()
        XCTAssertFalse(plan.isEnabled)
        app.buttons["nativeFlyover"].tap()
        XCTAssertTrue(plan.isEnabled)
        XCTAssertEqual(app.staticTexts["nativeTurnStatus"].label, before)
        app.buttons["Settings"].tap()
        app.buttons["tvSettings"].tap()
        let tv = app.switches["tvMode"]
        XCTAssertTrue(tv.waitForExistence(timeout: 5))
        let originalTVValue = tv.value as? String
        tv.coordinate(withNormalizedOffset: CGVector(dx: 0.9, dy: 0.5)).tap()
        XCTAssertNotEqual(tv.value as? String, originalTVValue)
        tv.coordinate(withNormalizedOffset: CGVector(dx: 0.9, dy: 0.5)).tap()
        XCTAssertEqual(tv.value as? String, originalTVValue)
        let comparison = app.switches["Show comparison flash"]
        comparison.coordinate(withNormalizedOffset: CGVector(dx: 0.9, dy: 0.5)).tap()
        XCTAssertEqual(comparison.value as? String, "1")
        for _ in 0..<3 where !app.buttons["tvFlash"].isHittable { app.swipeUp() }
        XCTAssertTrue(app.buttons["tvFlash"].waitForExistence(timeout: 5))
        app.buttons["tvFlash"].tap()
        XCTAssertTrue(app.staticTexts["nativeDisplayFlash"].label.contains("FLASH 1"))
        app.navigationBars["TV / AirPlay"].buttons["Done"].tap()
        app.navigationBars["Settings"].buttons["Done"].tap()
        XCTAssertEqual(app.staticTexts["nativeTurnStatus"].label, before)
    }

    func testNativeViewportStaysVisibleWhileControlsScrollAndRotate() {
        continueAfterFailure = false
        defer { XCUIDevice.shared.orientation = .portrait }
        let app = XCUIApplication()
        app.launchArguments = ["-realityKit", "-fixturePlayers", "4", "-startCourse", "meadow", "-range.swingInput", "touch", "-display.landscapeTV", "NO"]
        app.launch()
        let swing = app.buttons["nativeTouchSwing"]
        XCTAssertTrue(swing.waitForExistence(timeout: 30))
        let ready = XCTNSPredicateExpectation(predicate: NSPredicate { _, _ in swing.isEnabled }, object: nil)
        XCTAssertEqual(XCTWaiter.wait(for: [ready], timeout: 45), .completed)
        let viewport = app.otherElements["nativeViewport"]
        let before = viewport.frame
        let turn = app.staticTexts["nativeTurnStatus"].label
        app.scrollViews["nativeControls"].swipeUp()
        XCTAssertEqual(viewport.frame, before)
        XCTAssertTrue(swing.isHittable)
        screenshot(app, "Native multiplayer — pinned course and swing controls")
        XCUIDevice.shared.orientation = .landscapeLeft
        let rotated = XCTNSPredicateExpectation(predicate: NSPredicate { _, _ in
            app.frame.width > app.frame.height
        }, object: nil)
        XCTAssertEqual(XCTWaiter.wait(for: [rotated], timeout: 5), .completed)
        XCTAssertTrue(swing.waitForExistence(timeout: 5))
        XCTAssertTrue(swing.isHittable)
        XCTAssertGreaterThan(viewport.frame.width, viewport.frame.height)
        let sideBySide = XCTNSPredicateExpectation(predicate: NSPredicate { _, _ in
            app.staticTexts["nativeTurnStatus"].frame.minX >= viewport.frame.maxX
        }, object: nil)
        XCTAssertEqual(XCTWaiter.wait(for: [sideBySide], timeout: 5), .completed)
        XCTAssertEqual(app.staticTexts["nativeTurnStatus"].label, turn)
        // Accessibility geometry changes before UIKit's rotation compositor settles.
        Thread.sleep(forTimeInterval: 1)
        let landscape = XCTAttachment(screenshot: XCUIScreen.main.screenshot())
        landscape.name = "Native multiplayer — landscape controller"
        landscape.lifetime = .keepAlways
        add(landscape)
    }

    func testSunwardPhoneAndTVPresentationReview() {
        continueAfterFailure=false
        defer {XCUIDevice.shared.orientation = .portrait}
        for tv in [false,true] {
            let app=XCUIApplication()
            app.launchArguments=["-skipPlayerCalibration","-phoneController","-startCourse","sunward-resort-v1"]
            if tv {app.launchArguments += ["-tvPresentationPreview","-display.landscapeTV","NO"]}
            app.launch()
            XCUIDevice.shared.orientation=tv ? .landscapeLeft : .portrait
            if tv {
                let landscape=XCTNSPredicateExpectation(predicate:NSPredicate {_,_ in app.frame.width>app.frame.height},object:nil)
                XCTAssertEqual(XCTWaiter.wait(for:[landscape],timeout:10),.completed)
                XCTAssertTrue(app.staticTexts.containing(NSPredicate(format:"label CONTAINS %@","YD TO HOLE")).firstMatch.waitForExistence(timeout:20))
            } else {
                XCTAssertTrue(any(app,"phoneController").waitForExistence(timeout:25))
                XCTAssertTrue(any(app,"holeChip").label.contains("326"))
            }
            if tv {
                // app.screenshot can crop with stale portrait bounds after a
                // rotation; capture the composed display as the existing TV test does.
                let attachment=XCTAttachment(data:XCUIScreen.main.screenshot().pngRepresentation,uniformTypeIdentifier:"public.png")
                attachment.name="Sunward TV layout — simulator only";attachment.lifetime = .keepAlways;add(attachment)
            } else {screenshot(app,"Sunward phone controller — actual gameplay layout")}
            app.terminate()
        }
    }

    func testOptInReleaseFlyoverPerformanceAudit() throws {
        guard ProcessInfo.processInfo.environment["GOLF_VISUAL_AUDIT"] == "1" else {
            throw XCTSkip("Opt-in moving-camera render audit")
        }
        continueAfterFailure=false
        let app=XCUIApplication()
        // Use ordinary navigation: Release deliberately ignores debug shortcuts.
        app.launchArguments=["-range.swingInput","phone","-visualPerformanceAudit"]
        app.launch()
        XCTAssertTrue(app.buttons["menuSolo"].waitForExistence(timeout:15));app.buttons["menuSolo"].tap()
        XCTAssertTrue(app.buttons["continueToCourses"].waitForExistence(timeout:10));app.buttons["continueToCourses"].tap()
        let course=app.buttons["course-sunward-resort-v1"]
        XCTAssertTrue(course.waitForExistence(timeout:10));course.tap()
        XCTAssertTrue(any(app,"phoneController").waitForExistence(timeout:30))
        for _ in 0..<4 {
            app.buttons["courseMenu"].tap();app.buttons["viewFlyover"].tap()
            XCTAssertTrue(app.buttons["finishFlyover"].waitForExistence(timeout:5))
            let started=Date()
            let elapsed=XCTNSPredicateExpectation(predicate:NSPredicate { _,_ in Date().timeIntervalSince(started)>12 },object:nil)
            XCTAssertEqual(XCTWaiter.wait(for:[elapsed],timeout:15),.completed)
            if app.buttons["finishFlyover"].exists { app.buttons["finishFlyover"].tap() }
        }
        screenshot(app,"Release playable-course audit after moving flyovers")
    }

    func testSunwardCourseSelectionLoadsActualResortNotConceptFilm() {
        continueAfterFailure=false
        let app=XCUIApplication()
        app.launchArguments=["-skipPlayerCalibration","-phoneController"]
        app.launch()
        XCTAssertTrue(app.staticTexts["conceptFilmNotice"].waitForExistence(timeout:15))
        app.buttons["menuSolo"].tap()
        app.buttons["continueToCourses"].tap()
        let resort=app.buttons["course-sunward-resort-v1"]
        XCTAssertTrue(resort.waitForExistence(timeout:10))
        resort.tap()
        XCTAssertTrue(any(app,"phoneController").waitForExistence(timeout:25))
        XCTAssertTrue(any(app,"holeChip").label.contains("326"))
        XCTAssertFalse(app.staticTexts["conceptFilmNotice"].exists)
    }

    func testReadyTapSurvivesFingerLiftAndCanBeCancelledOrPaused() {
        continueAfterFailure = false
        let app = XCUIApplication()
        app.launchArguments = ["-skipPlayerCalibration", "-phoneController", "-startCourse", "easy", "-testPhoneMotion"]
        app.launch()
        let ready = app.buttons["armSwing"]
        XCTAssertTrue(ready.waitForExistence(timeout: 20))
        XCTAssertEqual(ready.label, "Ready")
        let before = any(app, "holeChip").label
        ready.tap() // XCTest lifts the finger; tracking must remain armed afterward.
        XCTAssertEqual(ready.label, "Cancel swing")
        XCTAssertTrue(any(app, "motionPanel").label.contains("Tracking"))
        ready.tap()
        XCTAssertEqual(ready.label, "Ready")
        XCTAssertEqual(any(app, "holeChip").label, before)
        ready.tap()
        app.buttons["pauseRound"].tap()
        XCTAssertFalse(ready.exists)
        app.buttons["pauseRound"].tap()
        XCTAssertTrue(ready.waitForExistence(timeout: 5))
        XCTAssertEqual(ready.label, "Ready")
        XCTAssertEqual(any(app, "holeChip").label, before)
    }

    func testRealHoleFlyoverCancelsWithoutSpendingAStroke() {
        let app=XCUIApplication()
        app.launchArguments=["-skipPlayerCalibration","-phoneController","-startCourse","sunward-resort-v1"]
        app.launch()
        XCTAssertTrue(any(app,"phoneController").waitForExistence(timeout:20))
        let score=any(app,"holeChip").label
        app.buttons["courseMenu"].tap();app.buttons["viewFlyover"].tap()
        XCTAssertTrue(app.buttons["finishFlyover"].waitForExistence(timeout:5))
        XCTAssertFalse(any(app,"armSwing").exists)
        screenshot(app,"Real Sunward hole flyover — controller and TV share this scene")
        app.buttons["finishFlyover"].tap()
        XCTAssertFalse(app.buttons["finishFlyover"].exists)
        XCTAssertEqual(any(app,"holeChip").label,score)
    }
    func testTVPresentationInLandscapeSimulator() {
        let app = XCUIApplication()
        app.launchArguments = ["-skipPlayerCalibration", "-startCourse", "easy", "-tvPresentationPreview", "-display.landscapeTV", "NO"]
        defer { XCUIDevice.shared.orientation = .portrait }
        app.launch()
        XCUIDevice.shared.orientation = .landscapeLeft
        let landscape = XCTNSPredicateExpectation(predicate: NSPredicate { _, _ in
            app.frame.width > app.frame.height
        }, object: nil)
        XCTAssertEqual(XCTWaiter.wait(for: [landscape], timeout: 10), .completed)
        XCTAssertTrue(app.staticTexts.containing(NSPredicate(format: "label CONTAINS %@", "YD TO HOLE")).firstMatch.waitForExistence(timeout: 15))
        XCTAssertFalse(any(app, "cameraStage").exists)
        let screenshotData = XCUIScreen.main.screenshot().pngRepresentation
        let attachment = XCTAttachment(data: screenshotData, uniformTypeIdentifier: "public.png")
        attachment.name = "TV presentation — landscape simulator, not an AirPlay connection test"
        attachment.lifetime = .keepAlways
        add(attachment)
    }

    func testPhoneControllerSetupAimPauseAndNoCamera() {
        continueAfterFailure = false
        let app = XCUIApplication()
        app.launchArguments = ["-skipPlayerCalibration", "-phoneController", "-startCourse", "easy", "-manualProgression"]
        app.launch()
        XCTAssertTrue(any(app, "phoneController").waitForExistence(timeout: 20))
        XCTAssertFalse(any(app, "cameraStage").exists)
        XCTAssertFalse(app.buttons["cameraSetup"].exists)
        let hole = any(app, "holeChip").label
        app.swipeUp()
        let aim = any(app, "controllerAim")
        XCTAssertTrue(aim.waitForExistence(timeout: 5))
        let before = aim.label
        app.buttons["aimRight"].tap()
        XCTAssertNotEqual(aim.label, before)
        app.buttons["aimLeft"].tap()
        XCTAssertEqual(aim.label, before)
        app.buttons["controllerClub"].tap()
        app.buttons.matching(NSPredicate(format: "label BEGINSWITH %@", "Sand wedge")).firstMatch.tap()
        XCTAssertTrue(app.buttons["controllerClub"].label.contains("Sand wedge"))
        app.buttons["controllerShotType"].tap()
        app.buttons["Chip"].tap()
        XCTAssertTrue(app.buttons["controllerShotType"].label.contains("Chip"))
        app.buttons["shotControls"].tap()
        XCTAssertTrue(any(app, "plannedTarget").waitForExistence(timeout: 5))
        app.buttons["Done"].tap()
        screenshot(app, "Phone controller — club, direction and swing controls")
        app.buttons["pauseRound"].tap()
        XCTAssertTrue(app.staticTexts["Paused · resume when ready"].waitForExistence(timeout: 5))
        XCTAssertEqual(any(app, "holeChip").label, hole)
        app.buttons["pauseRound"].tap()
        app.buttons["courseMenu"].tap()
        app.buttons["TV / AirPlay"].tap()
        XCTAssertTrue(app.switches["tvMode"].waitForExistence(timeout: 5))
        XCTAssertFalse(app.buttons["tvReposition"].exists)
        app.buttons["Done"].tap()
        XCTAssertEqual(any(app, "holeChip").label, hole)
    }

    func testUnscannedPlayersCanStartAndUseTouchFallback() {
        continueAfterFailure = false
        let app = XCUIApplication()
        app.launchArguments = ["-skipPlayerCalibration", "-fixturePlayers", "2", "-phoneController"]
        app.launch()
        app.buttons["menuMultiplayer"].tap()
        XCTAssertTrue(app.buttons["continueToCourses"].waitForExistence(timeout: 5))
        XCTAssertTrue(app.buttons["continueToCourses"].isEnabled)
        XCTAssertFalse(app.buttons["scan-Player 1"].exists)
        XCTAssertFalse(app.buttons["scan-Player 2"].exists)
        app.buttons["continueToCourses"].tap()
        app.buttons["course-sunward-resort-v1"].tap()
        XCTAssertTrue(any(app, "phoneController").waitForExistence(timeout: 15))
        app.buttons["courseMenu"].tap()
        app.buttons["Touch"].tap()
        XCTAssertTrue(any(app, "swingPad").waitForExistence(timeout: 5))
        XCTAssertFalse(any(app, "cameraStage").exists)
        screenshot(app, "Touch fallback without camera setup")
    }

    func testPracticeUsesControllerWithoutScanning() {
        let app = XCUIApplication()
        app.launchArguments = ["-skipPlayerCalibration", "-phoneController"]
        app.launch()
        XCTAssertTrue(app.buttons["menuPractice"].waitForExistence(timeout: 15))
        app.buttons["menuPractice"].tap()
        XCTAssertTrue(any(app, "phoneController").waitForExistence(timeout: 15))
        XCTAssertFalse(any(app, "practiceLab").exists)
        XCTAssertFalse(any(app, "cameraStage").exists)
    }

    func testSunwardVisualUpgradeTouchShot() {
        continueAfterFailure=false
        let app=XCUIApplication()
        app.launchArguments=["-skipPlayerCalibration","-startCourse","sunward-resort-v1",
            "-manualProgression",
            "-presentation.authoredGolfer","NO"]
        app.launch()
        let pad=app.otherElements["swingPad"]
        XCTAssertTrue(pad.waitForExistence(timeout:25))
        screenshot(app,"Sunward updated playable golfer and tee")
        pad.coordinate(withNormalizedOffset:CGVector(dx:0.5,dy:0.1))
            .press(forDuration:0.1,thenDragTo:pad.coordinate(withNormalizedOffset:CGVector(dx:0.5,dy:0.8)))
        XCTAssertTrue(app.buttons["nextShot"].waitForExistence(timeout:25))
        let score=any(app,"holeChip").label
        app.buttons["courseMenu"].tap()
        app.buttons["Phone"].tap()
        XCTAssertTrue(any(app, "phoneController").waitForExistence(timeout: 5))
        XCTAssertEqual(any(app, "holeChip").label, score, "Switching render layouts must preserve the round")
        app.buttons["courseMenu"].tap()
        app.buttons["Touch"].tap()
        XCTAssertTrue(app.buttons["nextShot"].waitForExistence(timeout: 5))
        XCTAssertEqual(any(app, "holeChip").label, score)
        screenshot(app,"Sunward updated shot result")
        app.buttons["replayShot"].tap()
        XCTAssertTrue(app.buttons["nextShot"].waitForExistence(timeout:25))
        XCTAssertEqual(any(app,"holeChip").label,score,"Replay cannot spend another stroke")
        app.buttons["nextShot"].tap()
        XCTAssertTrue(pad.waitForExistence(timeout:10))
    }
    func testChooseSaveAndReopenGolferPreset() {
        continueAfterFailure=false
        let app=XCUIApplication()
        app.launchArguments=["-skipPlayerCalibration","-range.swingInput","touch","-gestures.enabled","NO"]
        app.launch()
        XCTAssertTrue(app.buttons["menuSolo"].waitForExistence(timeout:15)); app.buttons["menuSolo"].tap()
        let edit=app.buttons["editGolfer-Player 1"]
        XCTAssertTrue(edit.waitForExistence(timeout:5)); edit.tap()
        XCTAssertTrue(app.otherElements["nativeAppearancePreview"].waitForExistence(timeout:15))
        XCTAssertTrue(app.segmentedControls["golferPreset"].waitForExistence(timeout:5))
        app.segmentedControls["golferPreset"].buttons["Sunset"].tap()
        screenshot(app,"Sunset golfer — saved preset editor")
        app.buttons["saveGolfer"].tap()
        XCTAssertTrue(edit.waitForExistence(timeout:5)); edit.tap()
        XCTAssertTrue(app.segmentedControls["golferPreset"].buttons["Sunset"].isSelected)
        app.buttons["Cancel"].tap()
    }

    func testResortIgnoresLegacyGolferPreferenceAndPreservesOverview() {
        continueAfterFailure=false
        let app=XCUIApplication()
        app.launchArguments=["-skipPlayerCalibration","-startCourse","sunward-resort-v1",
            "-range.swingInput","touch","-gestures.enabled","NO","-presentation.authoredGolfer","YES"]
        app.launch()
        XCTAssertTrue(any(app,"golfCourse").waitForExistence(timeout:30))
        XCTAssertEqual(app.state,.runningForeground)
        XCTAssertTrue(any(app,"golfCourse").label.contains("Sunward Resort"))
        XCTAssertTrue(any(app,"shotStrengthGuide").waitForExistence(timeout:5))
        screenshot(app,"Resort nine — production golfer despite legacy preview preference")
        app.buttons["holeOverview"].tap()
        XCTAssertTrue(any(app,"plannedTarget").waitForExistence(timeout:5))
        screenshot(app,"Resort nine — shared terrain and route map")
        app.buttons["Done"].tap()
    }

    func testSunwardOriginalCourseAndShotGuide() {
        continueAfterFailure = false
        let app = XCUIApplication()
        app.launchArguments = ["-skipPlayerCalibration", "-startCourse", "sunward", "-range.swingInput", "touch", "-gestures.enabled", "NO"]
        app.launch()
        XCTAssertTrue(any(app, "golfCourse").waitForExistence(timeout: 20))
        XCTAssertEqual(app.state, .runningForeground, "Leave the phone in GolfArcade during this visual check.")
        XCTAssertTrue(any(app, "golfCourse").label.contains("Sunward Links"))
        XCTAssertTrue(any(app, "shotStrengthGuide").waitForExistence(timeout: 5))
        screenshot(app, "Sunward Links — original course and strength guide")
        app.buttons["holeOverview"].tap()
        XCTAssertTrue(any(app, "plannedTarget").waitForExistence(timeout: 5))
        screenshot(app, "Sunward Links — route overview")
        XCTAssertEqual(app.state, .runningForeground, "The phone left GolfArcade during the visual check.")
        XCTAssertTrue(app.buttons["Done"].waitForExistence(timeout: 5))
        app.buttons["Done"].tap()
    }

    func testColdLaunchRendersPhoneMenuWithTVModeOffAndOn() {
        for tvEnabled in ["NO", "YES"] {
            let app = XCUIApplication()
            app.launchArguments = ["-display.landscapeTV", tvEnabled]
            app.launch()
            XCTAssertTrue(app.buttons["menuSolo"].waitForExistence(timeout: 20),
                          "Phone must render its SwiftUI menu with TV mode \(tvEnabled)")
            XCTAssertTrue(app.buttons["menuSettings"].isHittable)
            screenshot(app, "Physical phone cold launch — TV mode \(tvEnabled)")
            app.terminate()
        }
    }

    private func any(_ app: XCUIApplication, _ identifier: String) -> XCUIElement {
        app.descendants(matching: .any)[identifier]
    }

    private func screenshot(_ app: XCUIApplication, _ name: String) {
        let attachment = XCTAttachment(screenshot: app.screenshot())
        attachment.name = name
        attachment.lifetime = .keepAlways
        add(attachment)
    }

    func testCourseArtAcrossAllDifficulties() {
        for difficulty in ["easy", "medium", "hard"] {
            let app = XCUIApplication()
            // Retained comparison courses are debug fixtures, not menu choices.
            app.launchArguments = ["-skipPlayerCalibration", "-range.swingInput", "touch", "-startCourse", difficulty]
            app.launch()
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

    func testPlayAHoleOutWithTheTouchPad() {
        let app = XCUIApplication()
        // This test presses Next shot and Continue itself; play's automatic progression
        // would otherwise move on to the next hole before it looks.
        app.launchArguments += ["-skipPlayerCalibration", "-manualProgression", "-startCourse", "easy"]
        app.launch()
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
        app.buttons["course-sunward-resort-v1"].tap()
        // The banner shows for under two seconds; look for its text in one wait.
        XCTAssertTrue(any(app, "holeChip").waitForExistence(timeout: 15))
        XCTAssertTrue(any(app, "holeChip").label.contains("Player 1"))
        // The successful text wait verifies the transient banner. A second UI
        // snapshot can occur after its two-second dismissal and race that result.
        screenshot(app, "Player 1 turn banner")
        sleep(2)
        screenshot(app, "Friends standing around the golfer")
    }
}
