import XCTest
@testable import GolfArcade

@MainActor final class AnalyticsTests: XCTestCase {
    func testEventWritesExactlyOneLocalDiagnosticsLine() async throws {
        let event = "analytics_test_\(UUID().uuidString)"
        Analytics.clearEvents(); Analytics.track(event, ["step": "account", "result": "guest"])
        XCTAssertEqual(Analytics.events.count, 1)
        let line = try XCTUnwrap(Analytics.events.first)
        XCTAssertTrue(line.contains("analytics \(event) t="))
        XCTAssertTrue(line.contains("step=account")); XCTAssertTrue(line.contains("result=guest"))
        let url = FileManager.default.urls(for: .documentDirectory, in: .userDomainMask)[0].appendingPathComponent("SportsDiagnostics.log")
        var lines: [Substring] = []
        for _ in 0..<50 {
            lines = ((try? String(contentsOf: url, encoding: .utf8)) ?? "").split(separator: "\n").filter { $0.contains(event) }
            if !lines.isEmpty { break }; try await Task.sleep(for: .milliseconds(20))
        }
        XCTAssertEqual(lines.count, 1); XCTAssertTrue(lines.first?.contains(line) == true)
    }
    func testEventsAreMonotonicBoundedAndEscaped() {
        Analytics.clearEvents(); let before = Analytics.secondsSinceInstall
        for _ in 0..<205 { Analytics.track("bounded", ["text": "two words\nnext"]) }
        XCTAssertEqual(Analytics.events.count, 200)
        XCTAssertGreaterThanOrEqual(Analytics.secondsSinceInstall, before)
        XCTAssertTrue(Analytics.events.allSatisfy { !$0.contains("\n") && $0.contains("text=two%20words%0Anext") })
    }
}
