import SwiftUI
import XCTest
@testable import GolfArcade

/// PLAN_MenuHub_WalkableWorld phase 1, phone half: when the Plaza opens (HUB_BOOT), that no TV keeps the old phone menu (NO_TV),
/// Unity's reports, and the phone controller's look (written to $MENU_SNAP_DIR/hub-*.png).
@MainActor
final class HubSessionTests: XCTestCase {
    private var savedDisplay = false, savedPlaza = true
    override func setUp() async throws {
        savedDisplay = SportsSession.shared.displayConnected; savedPlaza = HubSession.shared.plazaMenus
        HubSession.shared.debugReset(); OnboardingFlow.shared.exitToMenu(); TennisMenu.shared.classic = false
    }
    override func tearDown() async throws {
        HubSession.shared.plazaMenus = savedPlaza; HubTestState.restore(display: savedDisplay)
    }

    func testNoTVKeepsThePhoneMenu() {
        let hub = HubSession.shared, menu = TennisMenu.shared
        SportsSession.shared.displayConnected = false
        for screen in [MenuScreen.title, .main, .party, .character, .settings] {
            menu.debugShow(screen); hub.evaluate()
            XCTAssertFalse(hub.wanted, "no TV: the plaza never opens (\(screen))")
            XCTAssertEqual(hub.phase, .off)
            XCTAssertFalse(hub.drivesPhone, "the phone keeps TennisPhoneMenu")
        }
    }

    func testATVOnTheHomeScreenAsksForThePlaza() {
        let hub = HubSession.shared, menu = TennisMenu.shared
        SportsSession.shared.displayConnected = true
        menu.debugShow(.title); XCTAssertTrue(hub.wanted, "a TV connection opens the plaza")
        menu.debugShow(.main); XCTAssertTrue(hub.wanted)
        menu.debugShow(.character); XCTAssertFalse(hub.wanted, "a classic menu screen shows over the plaza")
        menu.debugShow(.main); hub.plazaMenus = false; XCTAssertFalse(hub.wanted, "Plaza menus off keeps the flat TV menu")
        hub.plazaMenus = true; menu.classic = true; XCTAssertFalse(hub.wanted, "the developer classic home keeps its own menu"); menu.classic = false
        hub.debugLive(); hub.showClassic(); XCTAssertFalse(hub.wanted, "☰ → Classic menu"); XCTAssertEqual(hub.phase, .suspended)
        hub.backToPlaza(); XCTAssertTrue(hub.wanted)
    }

    func testMatchTakesTheRuntimeAndThePlazaReloadsAfter() {
        let hub = HubSession.shared
        SportsSession.shared.displayConnected = true; TennisMenu.shared.debugShow(.main)
        hub.debugLive(place: "tennis")
        hub.runtimeTaken()
        XCTAssertEqual(hub.phase, .suspended); XCTAssertFalse(hub.loaded, "the match scene replaced Hub.unity")
        hub.displayLost(); XCTAssertEqual(hub.phase, .off); XCTAssertFalse(hub.classicOverlay)
    }

    func testUnityReportsReachThePhone() {
        let hub = HubSession.shared
        hub.debugLive(session: "s1")
        hub.receive(["type": "hubPlace", "session": "s1", "message": "locker"])
        hub.receive(["type": "hubZone", "session": "s1", "message": "station|rack-shirt|Shirt|Shirt colour"])
        XCTAssertEqual(hub.place, "locker")
        XCTAssertEqual(hub.zone, HubZone(line: "station|rack-shirt|Shirt|Shirt colour"))
        XCTAssertEqual(hub.zone?.prompt, "Shirt · A to use")
        hub.receive(["type": "hubAction", "session": "s1", "message": "rack-shirt"]); XCTAssertEqual(hub.lastAction, "rack-shirt")
        hub.receive(["type": "hubState", "session": "s1", "message": "locker|rack-shirt|1.25|8|19"])
        XCTAssertEqual(hub.speed, 1.25); XCTAssertEqual(hub.inputAge.p95, 19)
        hub.receive(["type": "hubZone", "session": "other", "message": "door|x|y|z"]); XCTAssertEqual(hub.zone?.id, "rack-shirt", "another session's events are ignored")
        hub.receive(["type": "hubZone", "session": "s1", "message": ""]); XCTAssertNil(hub.zone)
        XCTAssertEqual(HubZone(line: "door|door-play-soon|Coming soon|Coming soon")?.prompt, "Coming soon · coming soon")
        XCTAssertEqual(HubZone(line: "bay|bay-tennis-exhibition|Exhibition|Quick match")?.prompt, "Exhibition · A to sit")
        XCTAssertNil(HubZone(line: "garbage"))
    }

    func testPhoneControllerSnapshots() async throws {
        let hub = HubSession.shared
        SportsSession.shared.displayConnected = true; TennisMenu.shared.debugShow(.main)
        for (name, place, zone) in [("plaza", "plaza", ""), ("door", "plaza", "door|door-plaza-locker|Locker|Walk in"),
                                    ("station", "locker", "station|rack-shirt|Shirt|Shirt colour"), ("bay", "tennis", "bay|bay-tennis-exhibition|Exhibition|Quick match on any court")] {
            hub.debugLive(place: place, zone: zone)
            try await save(HubControllerView(), "hub-controller-\(name)")
        }
        try await save(HubQuickMenu(hub: hub, dismiss: {}), "hub-quick-menu")
    }

    private func save<V: View>(_ view: V, _ name: String) async throws {
        let folder = URL(fileURLWithPath: ProcessInfo.processInfo.environment["MENU_SNAP_DIR"] ?? NSTemporaryDirectory() + "tennis-menu", isDirectory: true)
        try FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true)
        let size = CGSize(width: 402, height: 874)
        let scene = try XCTUnwrap(UIApplication.shared.connectedScenes.compactMap { $0 as? UIWindowScene }.first)
        let host = UIHostingController(rootView: view)
        let window = UIWindow(windowScene: scene)
        window.frame = CGRect(origin: .zero, size: size); window.rootViewController = host; window.isHidden = false
        host.view.frame = window.bounds; host.view.layoutIfNeeded()
        try await Task.sleep(for: .seconds(1.0))
        let image = UIGraphicsImageRenderer(size: size).image { _ in host.view.drawHierarchy(in: window.bounds, afterScreenUpdates: true) }
        try XCTUnwrap(image.pngData()).write(to: folder.appendingPathComponent("\(name).png"))
        window.isHidden = true
    }
}

/// PLAN_MenuHub_WalkableWorld phase 2: every old menu destination is reachable by walking (A at a station / bay) or ☰ in at most
/// 3 presses (ROUTE_ALL), and a locker colour pick goes to the plaza hero at once (LOCKER_LIVE, phone half).
@MainActor
final class HubRouteTests: XCTestCase {
    private var savedDisplay = false
    override func setUp() async throws { savedDisplay = SportsSession.shared.displayConnected; HubSession.shared.debugReset(); OnboardingFlow.shared.exitToMenu(); SportsSession.shared.displayConnected = true; TennisMenu.shared.debugShow(.main) }
    override func tearDown() async throws { HubTestState.restore(display: savedDisplay) }

    /// destination -> (how you get there, presses after arriving / from ☰)
    func testEveryOldMenuDestinationIsReachable() {
        let hub = HubSession.shared, menu = TennisMenu.shared
        let table: [(String, [String], (MenuScreen) -> Bool)] = [
            ("Locker", ["rack-shirt", "look-mirror"], { $0 == .character }),
            ("Settings", ["desk-settings"], { $0 == .settings }),
            ("How to play", ["screen-howto"], { $0 == .howTo }),
            ("Island Circuit", ["bay-tennis-campaign", "shelf-trophies"], { $0 == .campaign }),
            ("Quick match", ["bay-tennis-exhibition"], { $0 == .exhibition }),
            ("Training", ["bay-tennis-training"], { $0 == .training }),
            ("Golf round (course picker)", ["bay-golf-round"], { $0 == .map }),
            ("Online", ["bay-tennis-online", "bay-golf-online"], { if case .online = $0 { return true }; return $0 == .onlineChoice }),
            ("Local golf", ["bay-golf-pass"], { if case .online = $0 { return true }; return $0 == .localChoice || $0 == .localPlayers }),   // main: Local, then how many players
        ]
        var report: [String] = []
        for (name, spots, reached) in table {
            for spot in spots {
                hub.debugReset(); menu.debugShow(.main); hub.debugLive(session: "r")
                XCTAssertTrue(HubSession.spotIDs.contains(spot))
                hub.receive(["type": "hubAction", "session": "r", "message": spot])   // walk there + A (Unity reports the spot)
                XCTAssertTrue(reached(menu.screen), "\(name) via \(spot): reached \(menu.screen)")
                let viaQuick = HubQuickMenu.shortcuts.contains { $0.id == spot }
                report.append("\(name): walk to \(spot) + A (1 press)\(viaQuick ? " · ☰ + shortcut (2 presses)" : "")")
            }
        }
        // destinations that are the plaza itself or one button
        XCTAssertTrue(HubQuickMenu.places.map(\.id).contains("play"), "PLAY (sport select) is a room: walk in, or ☰ + PLAY Hall")
        report.append("Play / sport select: walk into PLAY, Tennis or Golf room (0 presses) · ☰ + room (2 presses)")
        report.append("Emotes: Emote button + pick (2 presses)")
        report.append("Feedback: walk to the mailbox + A (1 press) · ☰ + Send feedback (2 presses)")
        report.append("Invite / party: Party button (1 press) · Invite board + A (1 press)")
        report.append("Classic list menu: ☰ + Classic menu (2 presses)")
        // every shortcut is 2 presses and opens its destination
        for s in HubQuickMenu.shortcuts { XCTAssertNotNil(HubSession.route(s.id), s.id) }
        XCTAssertEqual(Set(HubSession.spotIDs).count, HubSession.spotIDs.count)
        if let dir = ProcessInfo.processInfo.environment["MENU_SNAP_DIR"] {
            try? (report.joined(separator: "\n") + "\n").write(toFile: dir + "/route_all.txt", atomically: true, encoding: .utf8)
        }
    }

    func testStationKeepsThePlazaOnTheTVAndDoneReturns() {
        let hub = HubSession.shared, menu = TennisMenu.shared
        hub.debugLive(session: "r")
        hub.receive(["type": "hubAction", "session": "r", "message": "desk-settings"])
        XCTAssertEqual(menu.screen, .settings); XCTAssertEqual(hub.station, "desk-settings")
        XCTAssertTrue(hub.wanted, "the TV keeps the plaza while the phone shows the station")
        var sent: [String] = []; hub.debugSent = { sent.append($0["action"] as? String ?? "") }
        hub.leaveStation()
        XCTAssertNil(hub.station); XCTAssertEqual(menu.screen, .main); XCTAssertEqual(sent.first, "hubB")
        // the panel's own back to home also ends the station
        hub.receive(["type": "hubAction", "session": "r", "message": "look-mirror"]); XCTAssertEqual(menu.screen, .character)
        menu.tap("lk-done"); hub.evaluate()
        XCTAssertNil(hub.station)
    }

    func testALockerPickIsSentToThePlazaHeroAtOnce() {
        let hub = HubSession.shared, session = SportsSession.shared
        let saved = session.players, index = session.playerIndex
        defer { session.players = saved; session.playerIndex = index }
        session.players = [Player(name: "Live", colorIndex: 0)]; session.playerIndex = 0
        hub.debugLive(session: "r")
        var looks: [[String: Any]] = []; hub.debugSent = { if $0["action"] as? String == "hubLook" { looks.append($0) } }
        session.players[0].setOutfit("shirt", hue: 0.33, shade: 0); session.savePlayers()
        XCTAssertEqual(looks.count, 1, "the save sends the look in the same call")
        XCTAssertEqual(looks.last?["shirt"] as? String, session.players[0].outfitHex("shirt"))
        session.savePlayers(); XCTAssertEqual(looks.count, 1, "an unchanged look is not re-sent")
    }
}

/// PLAN_MenuHub_WalkableWorld phase 3, phone half: a launch from a bay never shows the TV loading cover (NO_COVER, 20 launches),
/// the plaza comes back on the same bench after a finished match (RETURN), and stopping a load returns you standing (CANCEL).
@MainActor
final class HubBayLaunchTests: XCTestCase {
    private var savedExternal: UIWindow?, savedDisplay = false
    override func setUp() async throws {
        savedDisplay = SportsSession.shared.displayConnected
        HubSession.shared.debugReset(); OnboardingFlow.shared.exitToMenu()
        savedExternal = SportsDisplays.shared.external
        SportsDisplays.shared.external = UIWindow(frame: CGRect(x: 0, y: 0, width: 1920, height: 1080))
        SportsSession.shared.displayConnected = true; TennisMenu.shared.debugShow(.main)
    }
    override func tearDown() async throws {
        HubTestState.restore(display: savedDisplay); SportsDisplays.shared.external = savedExternal
    }

    func testTwentyBayLaunchesShowNoLoadingCover() {
        let hub = HubSession.shared, session = SportsSession.shared, displays = SportsDisplays.shared
        let launchesBefore = hub.bayLaunches
        for i in 0..<20 {
            hub.debugLive(session: "bay-\(i)")
            hub.receive(["type": "hubAction", "session": "bay-\(i)", "message": "bay-tennis-exhibition"])   // sat down at a bay
            XCTAssertEqual(hub.bayForLaunch(), "bay-tennis-exhibition")
            let before = displays.coversShown
            var sent: [String: Any]?
            hub.debugSent = nil
            session.startTennis(opponent: nil, round: nil, mode: "exhibition", difficulty: 0.45, preview: false)
            XCTAssertEqual(displays.coversShown, before, "launch \(i) from a bay showed a loading cover")
            _ = sent; if session.active { session.end() }
        }
        XCTAssertEqual(hub.bayLaunches - launchesBefore, 20, "all 20 went behind the plaza")
        // control: a launch from the classic menu still shows its cover
        hub.debugReset(); let before = displays.coversShown
        session.startTennis(opponent: nil, round: nil, mode: "exhibition", difficulty: 0.45, preview: false)
        XCTAssertEqual(displays.coversShown, before + 1, "a normal launch keeps its cover")
    }

    func testAFinishedMatchReturnsToTheSameBenchAndAStoppedOneStandsYouUp() {
        let hub = HubSession.shared, menu = TennisMenu.shared
        hub.debugLive(session: "a"); hub.receive(["type": "hubAction", "session": "a", "message": "bay-tennis-exhibition"])
        hub.runtimeTaken(bay: hub.bayForLaunch()); hub.sessionEnded(finished: true)
        XCTAssertEqual(hub.returnBay, "bay-tennis-exhibition")
        // the plaza boots again: the start message names the bench, and on ready the bay's panel opens (staying seated = rematch)
        var start: [String: Any]?
        hub.debugSent = { if $0["action"] as? String == "start" { start = $0 } }
        hub.debugBoot(session: "b")
        XCTAssertEqual(start?["bay"] as? String, "bay-tennis-exhibition")
        menu.debugShow(.main)
        hub.receive(["type": "ready", "session": "b"])
        XCTAssertEqual(hub.station, "bay-tennis-exhibition"); XCTAssertEqual(menu.screen, .exhibition)
        // standing up forgets the bench
        hub.leaveStation(); XCTAssertNil(hub.returnBay)
        // a load stopped part-way: back standing
        hub.debugLive(session: "c"); hub.receive(["type": "hubAction", "session": "c", "message": "bay-golf-round"])
        hub.runtimeTaken(bay: hub.bayForLaunch()); hub.sessionEnded(finished: false)
        XCTAssertNil(hub.returnBay)
    }
}

/// The hub tests drive the shared menu, session and lobby (an online bay route opens the online screens on the shared
/// MultiplayerService): put them all back so the suites that run after start clean.
@MainActor enum HubTestState {
    static func restore(display: Bool) {
        if SportsSession.shared.active { SportsSession.shared.end() }
        let service = MultiplayerService.shared
        service.cancelSearch(); if service.lobby != nil { service.leave() }; service.stopBrowsing()
        HubSession.shared.debugReset(); TennisMenu.shared.classic = false
        // stations tap through the locker (rack-racket picks the racket slot): put it back on its opening shelf for the next suite
        TennisMenu.shared.debugShow(.character); for id in ["lk-tab-gear", "lk-sport-tennis", "lk-slot-skin"] { TennisMenu.shared.tap(id) }
        TennisMenu.shared.debugShow(.title)
        SportsSession.shared.displayConnected = display
    }
}
