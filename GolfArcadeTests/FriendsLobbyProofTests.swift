import SwiftUI
import XCTest
@testable import GolfArcade

/// Friends + controller lock (2026-10-11), the phone's screens as they look (written to $MENU_SNAP_DIR/friends-*.png): the invite
/// banner drawn above a sheet (BANNER_VISIBLE), the Friends list with one-tap invites to online and offline friends, the party
/// bench, the online bench, the match-over panel, and the Plaza on a phone with no TV (the plaza above the controls; the plaza
/// picture is Unity's own still from HubPhoneOnlyTests when $PLAZA_STILL is set).
@MainActor
final class FriendsLobbyProofTests: XCTestCase {
    private var savedDisplay = false
    override func setUp() async throws {
        savedDisplay = SportsSession.shared.displayConnected
        HubSession.shared.debugReset(); OnboardingFlow.shared.exitToMenu(); TennisMenu.shared.debugShow(.main)
    }
    override func tearDown() async throws { HubTestState.restore(display: savedDisplay); ClubHub.shared.debugReset() }

    /// BANNER_VISIBLE: the invite banner is in a window above every other (here: over the Friends sheet), so a sheet or the plaza
    /// on the phone can never hide it; touches beside it go through to the app.
    func testTheInviteBannerShowsAboveASheet() async throws {
        InviteBannerWindow.install()
        let banner = try XCTUnwrap(InviteBannerWindow.window)
        let scene = try XCTUnwrap(banner.windowScene)
        XCTAssertGreaterThan(banner.windowLevel.rawValue, UIWindow.Level.alert.rawValue, "above alerts, sheets and the plaza")
        // a phone screen with a sheet up (the Friends list)
        let window = UIWindow(windowScene: scene); window.frame = scene.coordinateSpace.bounds
        let root = UIHostingController(rootView: ZStack { Color(hex: "16294A"); Text("Plaza controller").foregroundStyle(.white) }.ignoresSafeArea())
        window.rootViewController = root; window.isHidden = false
        let sheet = UIHostingController(rootView: ClubHubView(hub: .shared)); sheet.modalPresentationStyle = .pageSheet
        root.present(sheet, animated: false)
        // an invite arrives (in the plaza: no match, nothing loading)
        let hub = ClubHub.shared
        let liveContext = hub.context; hub.context = { ClubHubContext() }; defer { hub.context = liveContext }
        hub.doNotDisturb = false
        hub.notification(invite: HubInvite(inviteID: "proof", from: HubPlayerRef(id: "sam", name: "Sam"), sport: "golf", lobbyCode: "ABC234", group: 0, capacity: 4,
                                           expiresAt: Date().timeIntervalSince1970 * 1000 + 100_000), accept: nil)
        XCTAssertEqual(hub.banner?.from.name, "Sam")
        try await Task.sleep(for: .seconds(1.2))
        // what the screen shows: every window, back to front
        let size = scene.coordinateSpace.bounds.size
        let image = UIGraphicsImageRenderer(size: size).image { _ in
            for w in scene.windows.filter({ !$0.isHidden }).sorted(by: { $0.windowLevel.rawValue < $1.windowLevel.rawValue }) where w === window || w === banner {
                w.drawHierarchy(in: w.frame, afterScreenUpdates: true)
            }
        }
        try save(image, "friends-banner-over-sheet")
        // the banner takes touches only on itself: the middle of the screen goes to the app below
        XCTAssertNil(banner.hitTest(CGPoint(x: size.width / 2, y: size.height * 0.7), with: nil), "touches beside the banner go through")
        let joinPoint = try XCTUnwrap(banner.rootViewController?.view.subviews.first).frame
        XCTAssertNotNil(banner.hitTest(CGPoint(x: joinPoint.midX, y: joinPoint.midY), with: nil), "the banner itself takes its taps")
        root.dismiss(animated: false); window.isHidden = true
        hub.snooze(hub.banner!)
    }

    func testFriendsListAndPanels() async throws {
        let hub = HubSession.shared
        SportsSession.shared.displayConnected = true
        // the party bench while a friend is still on their way, the online bench searching, the match over
        hub.debugLive(place: "tennis")
        try await render(HubOnlineBenchPanelPreview(), "friends-online-bench")
        try await render(HubMatchOverPreview(), "friends-match-over")
    }

    /// The party bench: who is on it, who it waits for (a friend walking into the tennis room), a friend elsewhere in the plaza.
    func testThePartyBench() async throws {
        SportsSession.shared.displayConnected = true
        let bus = HubPartyTests.Bus()
        func phone(_ name: String, _ shirt: String) -> MultiplayerService {
            let s = MultiplayerService(sendToRuntime: { _ in true }, pollRuntime: { nil }, clock: { 100 })
            s.onMatchRequested = { _ in }; s.onReturnToLobby = {}; s.setScreen(true); s.requiresStableLink = false
            s.setIdentity(name: name, loadout: MultiplayerLoadout(skinHex: "C47A4C", colours: ["shirt": shirt], emotes: EmoteCatalog.defaults, shirtHex: shirt, shortsHex: "24345E"))
            return s
        }
        let a = phone("Ann", "E8505B"), b = phone("Bea", "3F62CC"), c = phone("Cy", "7BDA4A")
        try a.host(using: bus.link("a")); try b.connect(using: bus.link("b")); try c.connect(using: bus.link("c"))
        defer { c.leave(); b.leave(); a.leave() }
        func hub(_ s: MultiplayerService, _ id: String) -> HubSession { let h = HubSession(); h.partyService = s; h.debugLive(session: id); return h }
        let ha = hub(a, "ha"), hb = hub(b, "hb"), hc = hub(c, "hc")
        hc.receive(["type": "hubPose", "session": "hc", "message": "locker|0.000|-1000.000|0.000|0.0|0.00||"])
        hb.receive(["type": "hubPose", "session": "hb", "message": "tennis|204.600|-1000.000|0.000|0.0|1.00||"])
        ha.receive(["type": "hubAction", "session": "ha", "message": "bay-tennis-online"])
        XCTAssertEqual(ha.partyWaitingFor, ["Bea"])
        try await render(HubPartyBayPanel(hub: ha), "friends-party-bench")
    }

    /// PHONE_ONLY: with no TV the plaza draws in the top of the phone's screen (Unity), the same controls under it; PLAY is shut.
    func testThePlazaOnAPhoneWithNoTV() async throws {
        let hub = HubSession.shared
        SportsSession.shared.displayConnected = false
        hub.debugLive(place: "plaza", zone: "door|door-plaza-play|Play|TV needed", onPhone: true)
        let still = ProcessInfo.processInfo.environment["PLAZA_STILL"].flatMap { UIImage(contentsOfFile: $0) }
        try await render(ZStack(alignment: .top) {
            Group {
                if let still { Image(uiImage: still).resizable().aspectRatio(contentMode: .fill).frame(width: 402, height: 874, alignment: .top).clipped() }
                else { LinearGradient(colors: [Color(hex: "F5A07A"), Color(hex: "6FB7D9")], startPoint: .top, endPoint: .bottom) }
            }.ignoresSafeArea()
            HubControllerView()
        }, "friends-phone-only-plaza")
        XCTAssertEqual(hub.zone?.prompt, "Play · connect a TV (AirPlay) to play")
    }

    private func render<V: View>(_ view: V, _ name: String, size: CGSize = CGSize(width: 402, height: 874)) async throws {
        let scene = try XCTUnwrap(UIApplication.shared.connectedScenes.compactMap { $0 as? UIWindowScene }.first)
        let host = UIHostingController(rootView: view); let window = UIWindow(windowScene: scene)
        window.frame = CGRect(origin: .zero, size: size); window.rootViewController = host; window.isHidden = false
        host.view.frame = window.bounds; host.view.layoutIfNeeded(); try await Task.sleep(for: .seconds(0.8))
        let image = UIGraphicsImageRenderer(size: size).image { _ in host.view.drawHierarchy(in: window.bounds, afterScreenUpdates: true) }
        try save(image, name); window.isHidden = true
    }
    private func save(_ image: UIImage, _ name: String) throws {
        guard let dir = ProcessInfo.processInfo.environment["MENU_SNAP_DIR"] else { return }
        let folder = URL(fileURLWithPath: dir, isDirectory: true)
        try FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true)
        try XCTUnwrap(image.pngData()).write(to: folder.appendingPathComponent("\(name).png"))
    }
}

/// The online bench's panel while it searches (no Game Center screen: just this).
private struct HubOnlineBenchPanelPreview: View {
    var body: some View {
        let hub = HubSession.shared
        hub.debugOnlineBench(.golf, searching: true)
        return HubOnlineBenchPanel(hub: hub)
    }
}
private struct HubMatchOverPreview: View {
    var body: some View {
        let hub = HubSession.shared
        hub.onlineMatchFinished("You won!")
        return HubMatchOverPanel(hub: hub)
    }
}
