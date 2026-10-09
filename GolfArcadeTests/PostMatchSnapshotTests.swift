import SwiftUI
import XCTest
@testable import GolfArcade

/// Plays the court picker and the post-match rep screen (with two level-ups) and saves frames of the
/// animation to ArtDir/review/post-match, so they can be looked at without a TV or a phone.
@MainActor
final class PostMatchSnapshotTests: XCTestCase {
    private var out: URL {
        let url = URL(fileURLWithPath: #filePath).deletingLastPathComponent().deletingLastPathComponent().appendingPathComponent("ArtDir/review/post-match")
        try? FileManager.default.createDirectory(at: url, withIntermediateDirectories: true)
        return url
    }

    private func snap(_ window: UIWindow, _ name: String) throws {
        let format = UIGraphicsImageRendererFormat(); format.scale = 1; format.opaque = true
        let image = UIGraphicsImageRenderer(size: window.bounds.size, format: format).image { _ in
            window.rootViewController?.view.drawHierarchy(in: window.bounds, afterScreenUpdates: true)
        }
        try XCTUnwrap(image.pngData()).write(to: out.appendingPathComponent("\(name).png"))
    }

    private func window<V: View>(_ root: V, _ size: CGSize) throws -> UIWindow {
        let scene = try XCTUnwrap(UIApplication.shared.connectedScenes.compactMap { $0 as? UIWindowScene }.first)
        let host = UIHostingController(rootView: root)
        host.safeAreaRegions = []
        let window = UIWindow(windowScene: scene)
        window.frame = CGRect(origin: .zero, size: size)
        window.rootViewController = host; window.isHidden = false
        host.view.frame = window.bounds; host.view.layoutIfNeeded()
        return window
    }

    /// A won three-set match worth enough XP from level 2 to pass two levels.
    private func summary(won: Bool = true, practice: Bool = false) -> PostMatchSummary {
        let stats = MatchStats(line: "won=\(won ? 1 : 0);pointsWon=31;pointsLost=17;aces=3;winners=6;longest=14;perfect=9;great=12;setsWon=2;setsLost=1;gamesWon=6;gamesLost=4;hits=88;seconds=420")
        let lines = Progression.breakdown(stats: stats, won: won, practice: practice, difficulty: 0.6)
        let total = lines.reduce(0) { $0 + $1.xp }
        var level = 2, xp = 250 + total
        while xp >= LevelCurve.needed(level) { xp -= LevelCurve.needed(level); level += 1 }
        return PostMatchSummary(won: won, score: "6–4 3–6 7–5", opponent: "Kai", stats: stats, lines: lines, total: total, practice: practice,
                                startLevel: 2, startXP: 250, endLevel: level, endXP: xp)
    }

    func testActualPhoneRootShowsFinishActions() async throws {
        let session = SportsSession.shared, menu = TennisMenu.shared
        let connected = session.displayConnected
        menu.debugShow(.loading, launch: MenuLaunch(round: 0))
        session.active = true; session.finishedMatch = nil; session.sport = "tennis"
        defer { session.active = false; session.finishedMatch = nil; session.displayConnected = connected; menu.debugShow(.title) }
        let w = try window(SportsHome(), CGSize(width: 402, height: 874))
        defer { w.isHidden = true }
        session.receiveMatchSnapshot(["matchComplete": true, "matchWon": true, "finalScore": "3–0"])
        try await Task.sleep(for: .milliseconds(600))
        try snap(w, "phone-finish-actions")
        XCTAssertNotNil(session.finishedMatch)
    }

    func testCourtPickerAndRepShow() async throws {
        let menu = TennisMenu.shared
        defer { menu.debugShow(.title) }
        let tv = CGSize(width: 1280, height: 720), phone = CGSize(width: 402, height: 874)

        // The court picker, TV and phone.
        menu.debugShow(.map)
        var w = try window(TennisTVRoot(), tv)
        try await Task.sleep(for: .seconds(1.6)); try snap(w, "map-tv"); w.isHidden = true
        w = try window(TennisMenuScreen(compact: true), phone)
        try await Task.sleep(for: .seconds(1.6)); try snap(w, "map-phone"); w.isHidden = true

        // The rep show on the TV, frame by frame.
        let s = summary()
        XCTAssertGreaterThanOrEqual(s.levelsGained.count, 2, "the made-up match should pass two levels")
        menu.debugPostMatch(s, launch: MenuLaunch(mode: .exhibition, round: 0))
        w = try window(TennisTVRoot(), tv)
        for i in 0..<14 {
            try await Task.sleep(for: .seconds(1))
            try snap(w, String(format: "rep-tv-%02d", i))
        }
        if !menu.postMatchDone { menu.tap("pm-skip") }
        try await Task.sleep(for: .seconds(1))
        try snap(w, "rep-tv-final"); w.isHidden = true
        XCTAssertTrue(menu.postMatchDone, "the show ends and the buttons come")

        // And on the phone, after the show.
        menu.debugPostMatch(summary(won: false, practice: true), launch: MenuLaunch(mode: .training))
        w = try window(TennisMenuScreen(compact: true), phone)
        try await Task.sleep(for: .seconds(1)); menu.tap("pm-skip")
        try await Task.sleep(for: .seconds(1)); try snap(w, "rep-phone-final"); w.isHidden = true
    }

    func testXPBreakdownAndLevelCurve() {
        XCTAssertEqual(LevelCurve.needed(1), 200)
        XCTAssertLessThan(LevelCurve.needed(5), LevelCurve.needed(6))
        let win = Progression.breakdown(stats: MatchStats(won: true), won: true, practice: false, difficulty: 0)
        let loss = Progression.breakdown(stats: MatchStats(won: false), won: false, practice: false, difficulty: 0)
        XCTAssertGreaterThan(win.reduce(0) { $0 + $1.xp }, loss.reduce(0) { $0 + $1.xp }, "winning earns more")
        XCTAssertGreaterThan(loss.reduce(0) { $0 + $1.xp }, 0, "even a loss earns something")
        let half = Progression.breakdown(stats: MatchStats(won: true), won: true, practice: true, difficulty: 0)
        XCTAssertLessThan(half.reduce(0) { $0 + $1.xp }, win.reduce(0) { $0 + $1.xp })
    }

    func testAwardLevelsUpAndPersists() {
        let player = UUID()
        let before = Progression.shared.entry(for: player)
        XCTAssertEqual(before.level, 1)
        let big = MatchStats(line: "won=1;pointsWon=40;aces=8;winners=15;longest=20;perfect=15;setsWon=2;gamesLost=0;setsLost=0")
        let result = Progression.shared.award(player: player, stats: big, won: true, score: "6–0 6–0", opponent: "Kai", practice: false, difficulty: 1)
        XCTAssertGreaterThan(result.total, 400)
        XCTAssertGreaterThan(result.endLevel, 1, "a big win levels up")
        XCTAssertEqual(Progression.shared.entry(for: player).level, result.endLevel)
        XCTAssertEqual(Progression.shared.entry(for: player).wins, 1)
        XCTAssertTrue(LevelRewards.rewards(for: result.endLevel).isEmpty, "no rewards yet")
    }
}
