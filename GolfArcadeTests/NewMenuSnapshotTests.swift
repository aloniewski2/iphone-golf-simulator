import SwiftUI
import XCTest
@testable import GolfArcade

/// Renders the redesigned menu screens (phone 402×874 and TV 1280×720) so they can be reviewed without a device.
/// Output folder: $MENU_SNAP_DIR (pass TEST_RUNNER_MENU_SNAP_DIR=... to xcodebuild) or $TMPDIR/tennis-menu.
@MainActor
final class NewMenuSnapshotTests: XCTestCase {
    private var folder: URL {
        let url = ProcessInfo.processInfo.environment["MENU_SNAP_DIR"].map { URL(fileURLWithPath: $0) }
            ?? FileManager.default.temporaryDirectory.appendingPathComponent("tennis-menu")
        try? FileManager.default.createDirectory(at: url, withIntermediateDirectories: true)
        return url
    }

    private func saveHosted<V: View>(_ view: V, _ name: String, _ size: CGSize) async throws {
        let scene = try XCTUnwrap(UIApplication.shared.connectedScenes.compactMap { $0 as? UIWindowScene }.first)
        let host = UIHostingController(rootView: view)
        host.safeAreaRegions = []
        let window = UIWindow(windowScene: scene)
        window.frame = CGRect(origin: .zero, size: size); window.rootViewController = host; window.isHidden = false
        host.view.frame = window.bounds; host.view.layoutIfNeeded()
        try await Task.sleep(for: .seconds(1.2))
        let image = UIGraphicsImageRenderer(size: size).image { _ in host.view.drawHierarchy(in: window.bounds, afterScreenUpdates: true) }
        try XCTUnwrap(image.pngData()).write(to: folder.appendingPathComponent("\(name).png"))
        window.isHidden = true
    }

    private let phone = CGSize(width: 402, height: 874), tv = CGSize(width: 1280, height: 720)

    private func both<V: View>(_ name: String, _ make: (Bool) -> V) async throws {
        try await saveHosted(make(true), "\(name)-phone", phone)
        try await saveHosted(make(false), "\(name)-tv", tv)
    }

    private func setUpPlayer() -> () -> Void {
        let session = SportsSession.shared, saved = session.players, index = session.playerIndex
        var p = Player(name: "Adnan", colorIndex: 0)
        p.setSkin(0.45); p.setOutfit("shirt", hue: 0.015, shade: 0.1); p.setOutfit("shorts", hue: 0.62, shade: -0.5)
        session.players = [p]; session.playerIndex = 0
        return { session.players = saved; session.playerIndex = index; TennisMenu.shared.debugShow(.title) }
    }

    func testPlayFlowScreens() async throws {
        let restore = setUpPlayer(); defer { restore() }
        let menu = TennisMenu.shared, progress = SportProgress.shared, campaign = TennisCampaign.shared
        progress.completeTutorial(.tennis)
        campaign.restart(); campaign.record(round: 0, won: true)
        menu.debugShow(.main)
        try await both("home") { IslandHomeScreen(menu: menu, compact: $0) }
        menu.debugShow(.hub(.tennis), row: 1)
        try await both("hub") { IslandHubScreen(menu: menu, sport: .tennis, compact: $0) }
        menu.debugShow(.campaign, row: 2)
        try await both("campaign") { IslandLadderScreen(menu: menu, compact: $0, exhibition: false) }
        menu.tap("campaignMore")
        try await both("campaign-confirm") { IslandLadderScreen(menu: menu, compact: $0, exhibition: false) }
        menu.debugShow(.map)
        try await both("court") { IslandCourtScreen(menu: menu, compact: $0) }
        menu.debugShow(.settings, row: 1)
        try await both("settings") { IslandSettingsScreen(menu: menu, compact: $0) }
        campaign.restart(); progress.resetTutorials()
        menu.debugShow(.hub(.tennis), row: 0)
        try await both("hub-new-player") { IslandHubScreen(menu: menu, sport: .tennis, compact: $0) }
    }


    /// The locker's states: Gear (skin / racket / shoes), Customize, and the full colour range.
    func testLockerStates() async throws {
        let restore = setUpPlayer(); defer { restore() }
        let session = SportsSession.shared
        session.players[0].standardFemale = true
        let menu = TennisMenu.shared; menu.debugShow(.character)
        let steps: [(String, [String])] = [("gear-skin", []), ("gear-racket", ["lk-slot-racket"]), ("gear-shoes", ["lk-slot-shoes"]),
                                           ("customize", ["lk-tab-customize"]), ("range-shirt", ["lk-shirt"])]
        for (name, taps) in steps {
            taps.forEach { menu.tap($0) }
            try await both("locker-\(name)") { IslandLockerScreen(menu: menu, compact: $0) }
        }
        menu.back(); menu.back()
        session.players[0].standardFemale = false
        menu.debugShow(.character); menu.tap("lk-slot-skin")
        try await both("locker-male") { IslandLockerScreen(menu: menu, compact: $0) }
    }

    func testRivalPortraits() async throws {
        let restore = setUpPlayer(); defer { restore() }
        var images: [UIImage] = []
        for o in TennisCampaign.draw { images.append(try XCTUnwrap(RivalPortraits.image(o), o.name)) }
        let sheet = UIGraphicsImageRenderer(size: CGSize(width: 240 * 5, height: 360 * 2)).image { _ in
            UIColor(red: 0.95, green: 0.93, blue: 0.88, alpha: 1).setFill(); UIRectFill(CGRect(x: 0, y: 0, width: 1200, height: 720))
            for (i, img) in images.enumerated() { img.draw(in: CGRect(x: CGFloat(i % 5) * 240, y: CGFloat(i / 5) * 360, width: 240, height: 360)) }
        }
        try XCTUnwrap(sheet.pngData()).write(to: folder.appendingPathComponent("rival-portraits.png"))
    }

    func testMatchOverScreens() async throws {
        let restore = setUpPlayer(); defer { restore() }
        let menu = TennisMenu.shared
        TennisCampaign.shared.restart(); TennisCampaign.shared.record(round: 1, won: true)
        menu.debugShow(.results, launch: MenuLaunch(round: 1), result: MatchResult(won: true, score: "6–3 · 6–4", round: 1))
        try await both("match-over") { IslandResultsScreen(menu: menu, compact: $0) }
        TennisCampaign.shared.restart()
    }

    func testRemoteScreens() async throws {
        let restore = setUpPlayer(); defer { restore() }
        let menu = TennisMenu.shared
        menu.debugShow(.campaign, row: 2)
        try await saveHosted(TennisRemote(), "remote-phone", phone)
        menu.debugShow(.character)
        try await saveHosted(TennisRemote(), "remote-locker-phone", phone)
    }
}
