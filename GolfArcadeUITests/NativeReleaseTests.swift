import XCTest

/// Runs against the optimized native-only product through ordinary navigation.
/// No renderer flag, seeded players, course shortcut, or DEBUG fixture is used.
@MainActor
final class NativeReleaseTests: XCTestCase {
    func testOptInFullNineHoleRoundThroughReleaseMenus() throws {
        guard ProcessInfo.processInfo.environment["GOLF_NATIVE_ROUND_AUDIT"] == "1" else {
            throw XCTSkip("Opt-in full nine-hole Release playthrough")
        }
        let app = launch()
        enterResort(app, mode: "menuSolo")
        var visited: [Int] = []
        var shots = 0
        for _ in 0..<100 {
            if app.buttons["nativePlayAgain"].exists { break }
            let swing = readySwing(app)
            let header = app.staticTexts.matching(NSPredicate(format: "label CONTAINS %@", " · Hole ")).firstMatch.label
            let status = app.staticTexts["nativeTurnStatus"].label
            let holeText = try XCTUnwrap(header.components(separatedBy: " · Hole ").last?.components(separatedBy: " · ").first)
            let hole = try XCTUnwrap(Int(holeText))
            if visited.last != hole {
                XCTAssertEqual(hole, (visited.last ?? 0) + 1, "Every hole must be reached in order")
                visited.append(hole)
                capture(app, "Release full round — hole \(hole) address")
            }
            XCTAssertLessThan(shots, 100, "Stroke caps must bound the full round")
            swing.tap()
            shots += 1
            let advanced = XCTNSPredicateExpectation(predicate: NSPredicate { _, _ in
                if app.buttons["nativePlayAgain"].exists { return true }
                guard swing.exists && swing.isEnabled else { return false }
                let nextHeader = app.staticTexts.matching(NSPredicate(format: "label CONTAINS %@", " · Hole ")).firstMatch
                return app.staticTexts["nativeTurnStatus"].label != status || (nextHeader.exists && nextHeader.label != header)
            }, object: nil)
            // Let flight, result delay and hole loading finish naturally. Tapping
            // transient result buttons races the normal automatic progression.
            XCTAssertEqual(XCTWaiter.wait(for: [advanced], timeout: 90), .completed)
        }
        XCTAssertEqual(visited, Array(1...9))
        XCTAssertTrue(app.buttons["nativePlayAgain"].waitForExistence(timeout: 10))
        XCTAssertTrue(app.staticTexts["roundComplete"].exists)
        let total = app.staticTexts["nativeRoundTotal-0"].label
        let strokeText = try XCTUnwrap(total.components(separatedBy: ": ").last?.components(separatedBy: " strokes").first)
        XCTAssertGreaterThanOrEqual(try XCTUnwrap(Int(strokeText)), shots, "Final total includes every accepted shot and any penalties")
        capture(app, "Release full nine-hole round — final scores")
        app.buttons["nativePlayAgain"].tap()
        _ = readySwing(app)
        XCTAssertTrue(app.staticTexts["nativeTurnStatus"].label.contains("Stroke 1"))
        XCTAssertTrue(app.staticTexts.matching(NSPredicate(format: "label CONTAINS %@", " · Hole 1 · ")).firstMatch.exists)
        XCTAssertFalse(app.staticTexts["roundComplete"].exists)
        capture(app, "Release full round — restarted")
        app.buttons["Menu"].tap()
        XCTAssertTrue(app.buttons["menuSolo"].waitForExistence(timeout: 10))
    }

    func testOptInCourseLoadProfile() throws {
        guard ProcessInfo.processInfo.environment["GOLF_NATIVE_PROFILE"] == "1" else {
            throw XCTSkip("Opt-in focused course-load profile")
        }
        let app = launch(arguments: ["-nativePerformanceAudit"])
        // Stable-menu window lets the host attach its sampling profiler before course entry.
        waitForTrace(seconds: 60)
        enterResort(app, mode: "menuSolo")
        readySwing(app)
        waitForTrace(seconds: 25)
        capture(app, "Native course-load trace complete")
        // Keep the app alive while the host requests the trace's processed results.
        waitForTrace(seconds: 30)
    }

    private func waitForTrace(seconds: Double) {
        let start = Date()
        let elapsed = XCTNSPredicateExpectation(predicate: NSPredicate { _, _ in
            Date().timeIntervalSince(start) >= seconds
        }, object: nil)
        XCTAssertEqual(XCTWaiter.wait(for: [elapsed], timeout: seconds + 5), .completed)
    }

    func testOptInMovingCameraPerformanceCapture() throws {
        guard ProcessInfo.processInfo.environment["GOLF_NATIVE_PERFORMANCE_AUDIT"] == "1" else {
            throw XCTSkip("Opt-in optimized native performance capture")
        }
        let app = launch(arguments: ["-nativePerformanceAudit"])
        enterResort(app, mode: "menuSolo")
        readySwing(app)
        for _ in 0..<3 {
            let flyover = app.buttons["nativeFlyover"]
            flyover.tap()
            let start = Date()
            let finished = XCTNSPredicateExpectation(predicate: NSPredicate { _, _ in
                Date().timeIntervalSince(start) > 13 && flyover.label.contains("View hole flyover")
            }, object: nil)
            XCTAssertEqual(XCTWaiter.wait(for: [finished], timeout: 20), .completed)
        }
        readySwing(app).tap()
        XCTAssertTrue(app.buttons["Replay"].waitForExistence(timeout: 35))
        app.buttons["Pause"].tap()
        capture(app, "Native-only optimized performance run")
        app.buttons["Menu"].tap()
        XCTAssertTrue(app.buttons["menuSolo"].waitForExistence(timeout: 10))
    }

    func testSoloShotReplayAndAutomaticNextShot() {
        let app = launch()
        enterResort(app, mode: "menuSolo")
        let swing = readySwing(app)
        capture(app, "Native-only release — Sunward address")
        swing.tap()
        XCTAssertTrue(app.buttons["Skip flight"].waitForExistence(timeout: 15))
        capture(app, "Native-only release — Sunward flight")
        app.buttons["Skip flight"].tap()
        XCTAssertTrue(app.buttons["Replay"].waitForExistence(timeout: 10))
        app.buttons["Replay"].tap()
        XCTAssertTrue(app.buttons["Skip flight"].waitForExistence(timeout: 10))
        app.buttons["Skip flight"].tap()
        XCTAssertTrue(app.staticTexts["nativeAutoProgression"].label.contains("Replay paused auto-advance"))
        capture(app, "Native-only release — replay result")
        app.buttons["Next shot"].tap()
        XCTAssertTrue(app.staticTexts["nativeTurnStatus"].label.contains("Stroke 2"))
        readySwing(app).tap()
        XCTAssertTrue(app.buttons["Skip flight"].waitForExistence(timeout: 15))
        app.buttons["Skip flight"].tap()
        let next = XCTNSPredicateExpectation(predicate: NSPredicate { _, _ in
            app.staticTexts["nativeTurnStatus"].label.contains("Stroke 3") && swing.exists && swing.isEnabled
        }, object: nil)
        XCTAssertEqual(XCTWaiter.wait(for: [next], timeout: 15), .completed)
        app.buttons["Menu"].tap()
        XCTAssertTrue(app.buttons["menuSolo"].waitForExistence(timeout: 10))
    }

    func testMultiplayerMenuAndScoredShot() {
        let app = launch()
        enterResort(app, mode: "menuMultiplayer")
        readySwing(app).tap()
        XCTAssertTrue(app.buttons["Skip flight"].waitForExistence(timeout: 15))
        app.buttons["Skip flight"].tap()
        XCTAssertTrue(app.buttons["Replay"].waitForExistence(timeout: 10))
        app.buttons["Pause"].tap()
        XCTAssertTrue(app.staticTexts["shotLie"].exists)
        capture(app, "Native-only release — multiplayer result")
        app.buttons["Menu"].tap()
        XCTAssertTrue(app.buttons["menuMultiplayer"].waitForExistence(timeout: 10))
    }

    func testPracticeDoesNotScoreAndReturnsToMenu() {
        let app = launch()
        app.buttons["menuPractice"].tap()
        let swing = readySwing(app)
        let status = app.staticTexts["nativeTurnStatus"].label
        swing.tap()
        XCTAssertTrue(app.staticTexts["practiceResult"].waitForExistence(timeout: 10))
        XCTAssertTrue(app.staticTexts["practiceResult"].label.contains("no stroke counted"))
        XCTAssertEqual(app.staticTexts["nativeTurnStatus"].label, status)
        XCTAssertFalse(app.buttons["Replay"].exists)
        capture(app, "Native-only release — practice")
        app.buttons["Menu"].tap()
        XCTAssertTrue(app.buttons["menuSolo"].waitForExistence(timeout: 10))
    }

    private func launch(arguments: [String] = []) -> XCUIApplication {
        continueAfterFailure = false
        let app = XCUIApplication()
        // NSArgumentDomain preference overrides preserve the saved user roster/settings.
        app.launchArguments = ["-controller.version", "1", "-range.swingInput", "touch"] + arguments
        app.launch()
        XCTAssertTrue(app.buttons["menuSolo"].waitForExistence(timeout: 20))
        return app
    }

    private func enterResort(_ app: XCUIApplication, mode: String) {
        app.buttons[mode].tap()
        let proceed = app.buttons["continueToCourses"]
        XCTAssertTrue(proceed.waitForExistence(timeout: 10))
        for _ in 0..<5 where !proceed.isHittable { app.swipeUp() }
        XCTAssertTrue(proceed.isEnabled)
        proceed.tap()
        let course = app.buttons["course-sunward-resort-v1"]
        XCTAssertTrue(course.waitForExistence(timeout: 10))
        XCTAssertEqual(app.buttons.matching(NSPredicate(format: "identifier BEGINSWITH %@", "course-")).count, 1)
        course.tap()
    }

    @discardableResult
    private func readySwing(_ app: XCUIApplication) -> XCUIElement {
        XCTAssertTrue(app.otherElements["nativeViewport"].waitForExistence(timeout: 30))
        let swing = app.buttons["nativeTouchSwing"]
        XCTAssertTrue(swing.waitForExistence(timeout: 30))
        let ready = XCTNSPredicateExpectation(predicate: NSPredicate { _, _ in swing.isEnabled }, object: nil)
        XCTAssertEqual(XCTWaiter.wait(for: [ready], timeout: 60), .completed)
        return swing
    }

    private func capture(_ app: XCUIApplication, _ name: String) {
        let attachment = XCTAttachment(screenshot: app.screenshot())
        attachment.name = name
        attachment.lifetime = .keepAlways
        add(attachment)
    }
}
