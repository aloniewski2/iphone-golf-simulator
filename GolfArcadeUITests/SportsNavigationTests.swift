import XCTest

final class SportsNavigationTests:XCTestCase {
    @MainActor func testPhysicalExternalControllerGolfAndTennis() throws {
        try verifyExternalSports(["Golf · Cliffside","Tennis Rally"])
    }
    @MainActor func testPhysicalExternalTennisMap() throws {
        try verifyExternalSports(["Tennis Rally"])
    }
    @MainActor private func verifyExternalSports(_ sports:[String]) throws {
        #if targetEnvironment(simulator)
        throw XCTSkip("Requires a physical iPhone and connected external receiver")
        #else
        let app=XCUIApplication(); app.launch()
        continueAfterFailure=false
        let connected=app.staticTexts["External display connected"]
        guard connected.waitForExistence(timeout:15) else { throw XCTSkip("Connect the external display before this device test") }
        func reveal(_ element:XCUIElement) {
            let form=app.collectionViews.firstMatch
            for _ in 0..<10 { if element.exists && element.isHittable { return }; form.swipeUp() }
            for _ in 0..<12 { if element.exists && element.isHittable { return }; form.swipeDown() }
        }
        for sport in sports {
            let picker=app.buttons["sportPicker"]; reveal(picker)
            if !picker.label.contains(sport) {
                picker.tap()
                let option=app.buttons[sport]
                if !option.waitForExistence(timeout:3) { reveal(picker); picker.tap() }
                XCTAssertTrue(option.waitForExistence(timeout:3),"Sport selection menu must open")
                option.tap()
            }
            let touch=app.switches["touchControls"]; reveal(touch)
            if touch.value as? String != "1" { touch.coordinate(withNormalizedOffset:CGVector(dx:0.92,dy:0.5)).tap() }
            XCTAssertEqual(touch.value as? String,"1","Touch mode must be selected before testing touch commands")
            let play=app.buttons["startExternalGame"]; reveal(play)
            XCTAssertFalse(app.buttons["startUnityPreview"].isEnabled)
            play.tap()
            let resume=app.buttons["sessionPauseResume"]; reveal(resume)
            XCTAssertEqual(XCTWaiter.wait(for:[XCTNSPredicateExpectation(predicate:NSPredicate(format:"exists == true AND enabled == true"),object:resume)],timeout:30),.completed)
            XCTAssertTrue(app.navigationBars["Sports Arcade"].exists,"Phone must retain its native controller, not a Unity preview")
            resume.tap()
            XCTAssertEqual(resume.label,"Pause")
            let swing=app.buttons["controllerSwing"]; reveal(swing)
            if sport == "Tennis Rally" {
                XCTAssertTrue(app.sliders["tennisAim"].exists,"Aim must remain available after Ready")
                XCTAssertGreaterThan(app.buttons["sessionMenu"].frame.minY,swing.frame.maxY)
                XCTAssertEqual(app.buttons["sessionMenu"].label,"Quit to menu")
            }
            XCTAssertTrue(swing.isEnabled); swing.tap()
            if sport == "Golf · Cliffside" {
                let feedback=app.staticTexts["controllerFeedback"]
                XCTAssertEqual(XCTWaiter.wait(for:[XCTNSPredicateExpectation(predicate:NSPredicate(format:"label CONTAINS 'Flight' OR label CONTAINS 'Carry'"),object:feedback)],timeout:10),.completed,"Touch must reach Unity and produce a shot")
            }
            let picture=XCTAttachment(screenshot:app.screenshot()); picture.name="Native controller — \(sport)"; picture.lifetime = .keepAlways; add(picture)
            let menu=app.buttons["sessionMenu"]; reveal(menu); menu.tap()
        }
        #endif
    }
    func testNativeMenuHasNoScanGateAndExplainsMissingUnity() {
        let app=XCUIApplication(); app.launch()
        XCTAssertTrue(app.navigationBars["Sports Arcade"].waitForExistence(timeout:10))
        let preview=app.buttons["startUnityPreview"]
        let form=app.collectionViews.firstMatch
        for _ in 0..<8 { if preview.exists && preview.isHittable { break }; form.swipeUp() }
        XCTAssertTrue(preview.exists)
        // The action must recheck routing rather than remain disabled by stale state.
        XCTAssertTrue(app.buttons["startExternalGame"].isEnabled)
        preview.tap()
        app.swipeDown(); app.swipeDown()
        XCTAssertTrue(app.staticTexts.containing(NSPredicate(format:"label CONTAINS %@","physical-device build")).firstMatch.waitForExistence(timeout:5))
    }
}
