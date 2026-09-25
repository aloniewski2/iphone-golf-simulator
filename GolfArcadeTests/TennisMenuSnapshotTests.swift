import SwiftUI
import XCTest
@testable import GolfArcade

/// Renders every tennis front-end screen to PNG (TV at 1280×720, phone at 402×874) so the
/// layouts can be reviewed without a TV attached. Output: $TMPDIR/tennis-menu/.
@MainActor
final class TennisMenuSnapshotTests: XCTestCase {
    private var folder: URL {
        let url = FileManager.default.temporaryDirectory.appendingPathComponent("tennis-menu")
        try? FileManager.default.createDirectory(at: url, withIntermediateDirectories: true)
        return url
    }

    private func save<V: View>(_ view: V, _ name: String, _ size: CGSize) throws {
        let renderer = ImageRenderer(content: view.frame(width: size.width, height: size.height))
        renderer.scale = 1.5
        let image = try XCTUnwrap(renderer.uiImage, "\(name) did not render")
        try XCTUnwrap(image.pngData()).write(to: folder.appendingPathComponent("\(name).png"))
    }

    func testRenderScreens() throws {
        let menu = TennisMenu.shared
        LoopingVideo.stillsOnly = true; defer { LoopingVideo.stillsOnly = false }
        let tv = CGSize(width: 1280, height: 720), phone = CGSize(width: 402, height: 874)
        let states: [(String, MenuScreen, MenuLaunch?, MatchResult?, Int, Int)] = [
            ("title", .title, nil, nil, 0, 0),
            ("main", .main, nil, nil, 0, 0),
            ("game-select", .gameSelect, nil, nil, 0, 1),
            ("hub-tennis", .hub(.tennis), nil, nil, 0, 0),
            ("hub-golf", .hub(.golf), nil, nil, 0, 1),
            ("locked-boxing", .locked(.boxing), nil, nil, 0, 0),
            ("exhibition", .exhibition, nil, nil, 0, 0),
            ("character", .character, nil, nil, 4, 0),
            ("howto", .howTo, nil, nil, 0, 1),
            ("golf-lesson", .golfLesson, nil, nil, 0, 1),
            ("connect", .connect, nil, nil, 1, 0),
            ("loading-tutorial", .loading, MenuLaunch(mode: .tutorial), nil, 0, 0),
            ("loading-golf", .loading, MenuLaunch(sport: .golf, mode: .round), nil, 0, 0),
            ("campaign", .campaign, nil, nil, 0, 0),
            ("campaign-locked", .campaign, nil, nil, 0, 2),
            ("training", .training, nil, nil, 1, 0),
            ("settings", .settings, nil, nil, 3, 0),
            ("loading", .loading, MenuLaunch(round: 3), nil, 0, 0),
            ("loading-final", .loading, MenuLaunch(round: 9), nil, 0, 0),
            ("loading-training", .loading, MenuLaunch(round: nil), nil, 0, 0),
            ("results-win", .results, MenuLaunch(round: 0), MatchResult(won: true, score: "3–1", round: 0), 0, 0),
            ("results-loss", .results, MenuLaunch(round: 3), MatchResult(won: false, score: "1–3", round: 3), 0, 0),
        ]
        for (name, screen, launch, result, row, column) in states {
            menu.debugShow(screen, launch: launch, result: result, row: row, column: column)
            try save(TennisTVRoot(), "tv-\(name)", tv)
            try save(TennisPhoneMenu(), "phone-\(name)", phone)
        }
        for tab in SettingsTab.allCases {
            menu.debugShow(.settings, row: 1, column: 0, tab: tab)
            try save(TennisTVRoot(), "tv-settings-\(tab.rawValue)", tv)
        }
        // A 16:10 MacBook receiving AirPlay: the canvas fits with backdrop above and below.
        menu.debugShow(.gameSelect, row: 0, column: 1)
        try save(TennisTVRoot(), "mac-game-select", CGSize(width: 1440, height: 900))
        menu.debugShow(.campaign, row: 0, column: 1)
        try save(TennisRemote(), "phone-remote", phone)
        for (name, lines, index) in [("story-ray", TennisStory.before(9), 0), ("story-rival", TennisStory.before(3), 1),
                                     ("story-offer", TennisStory.afterWin(0), 3)] {
            menu.debugStory(lines, index: index)
            try save(TennisTVRoot(), "tv-\(name)", tv)
            try save(TennisPhoneMenu(), "phone-\(name)", phone)
        }
        try save(TennisRemote(), "phone-remote-story", phone)
        menu.debugShow(.campaign, row: 1, column: 4)
        try save(TennisTVRoot(), "tv-campaign-final", tv)

        let session = SportsSession.shared
        session.ready = true; session.touch = true; session.paused = false; session.opponentName = "Suki"
        session.score = TennisScore(line: "2,1,GAMES 2–1 · 30–15 · YOUR SERVE")
        session.contacts = ["0.1,0.05,5,0", "-0.4,0.3,3,0", "0.6,-0.5,1,0", "0.0,-0.1,4,0", "0.2,0.2,5,1"].compactMap(TennisContact.init(line:))
        try save(TennisRacketController(session: session), "phone-racket", phone)
        session.axisGate.locked = true
        for phase in ["serve", "toss", "receive"] {
            session.tennisPhase = phase
            try save(TennisRacketController(session: session), "phone-\(phase)", phone)
        }
        session.tennisPhase = "" 
        menu.debugShow(.title)
    }
}
