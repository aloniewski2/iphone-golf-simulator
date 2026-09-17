import XCTest

/// Shared helpers for the UI test classes.
extension XCTestCase {
    func any(_ app: XCUIApplication, _ identifier: String) -> XCUIElement {
        app.descendants(matching: .any)[identifier]
    }

    /// Polls `check` until it passes or `timeout` elapses. Unlike an XCTNSPredicateExpectation,
    /// a miss costs one snapshot rather than a full debug capture of the element, which in a
    /// busy camera screen takes long enough for a countdown to run past.
    @discardableResult
    func waitUntil(_ timeout: TimeInterval, _ description: String, check: () -> Bool) -> Bool {
        let deadline = Date().addingTimeInterval(timeout)
        repeat {
            if check() { return true }
            usleep(250_000)
        } while Date() < deadline
        XCTFail("timed out after \(Int(timeout))s waiting for \(description)")
        return false
    }

    func screenshot(_ app: XCUIApplication, _ name: String) {
        let attachment = XCTAttachment(screenshot: app.screenshot())
        attachment.name = name
        attachment.lifetime = .keepAlways
        add(attachment)
    }
}
