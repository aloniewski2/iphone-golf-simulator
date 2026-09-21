import XCTest

final class SportsNavigationTests:XCTestCase {
    func testNativeMenuHasNoScanGateAndExplainsMissingUnity() {
        let app=XCUIApplication(); app.launch()
        XCTAssertTrue(app.navigationBars["Sports Arcade"].waitForExistence(timeout:10))
        let preview=app.buttons["startUnityPreview"]
        let form=app.collectionViews.firstMatch
        for _ in 0..<8 { if preview.exists && preview.isHittable { break }; form.swipeUp() }
        XCTAssertTrue(preview.exists)
        preview.tap()
        app.swipeDown(); app.swipeDown()
        XCTAssertTrue(app.staticTexts.containing(NSPredicate(format:"label CONTAINS %@","physical-device build")).firstMatch.waitForExistence(timeout:5))
    }
}
