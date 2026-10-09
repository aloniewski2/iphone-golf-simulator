import XCTest
import SceneKit
import SwiftUI
@testable import GolfArcade

@MainActor final class MenuBetaTests: XCTestCase {
    func testPlayRoutesAndBackPaths() {
        let menu = TennisMenu()
        menu.tap("start"); XCTAssertEqual(menu.focused, "play")
        XCTAssertTrue(menu.rows(.main).flatMap { $0 }.contains("homeInvite"))
        menu.tap("play"); XCTAssertEqual(menu.screen, .party)
        menu.tap("partySolo"); XCTAssertEqual(menu.screen, .gameSelect)
        menu.back(); XCTAssertEqual(menu.screen, .party)
        // every way to play is on the one Play list: alone, in the same room, or online
        XCTAssertEqual(menu.rows(.party), [["partySolo"], ["partyLocal"], ["partyOnline"], ["back"]])
        menu.tap("partyOnline"); XCTAssertEqual(menu.screen, .onlineChoice)
        menu.tap("onlineQuick"); XCTAssertEqual(menu.screen, .online(.entry))
        menu.back(); XCTAssertEqual(menu.screen, .onlineChoice)
        menu.back(); XCTAssertEqual(menu.screen, .party)
        menu.back(); XCTAssertEqual(menu.screen, .main)
        menu.debugShow(.online(.nearby)); menu.goHome()
        XCTAssertEqual(menu.screen, .main, "Browsing without joining must return home directly")
    }
    func testLocalPlayIsTwoTapsFromPlayAndGolfOnlyAsksHowManyAreSharingThePhone() {
        let menu = TennisMenu()
        menu.tap("start"); menu.tap("play")
        menu.tap("partyLocal"); XCTAssertEqual(menu.screen, .localChoice)
        XCTAssertEqual(menu.rows(.localChoice), [["partyLocalGolf"], ["partyLocalTennis"], ["partyNearby"], ["back"]])
        menu.tap("partyLocalGolf"); XCTAssertEqual(menu.screen, .localPlayers, "golf asks one thing: how many players")
        XCTAssertEqual(menu.rows(.localPlayers), [["localPlayers2"], ["localPlayers3"], ["localPlayers4"], ["back"]])
        menu.back(); XCTAssertEqual(menu.screen, .localChoice)
        menu.back(); XCTAssertEqual(menu.screen, .party)
        menu.back(); XCTAssertEqual(menu.screen, .main)
    }
    func testControllerFocusUpdatesPreviewWithoutLaunching() {
        let menu = TennisMenu()
        menu.tap("start"); menu.tap("play"); menu.tap("partySolo")
        XCTAssertTrue(menu.focus("sport-tennis")); XCTAssertEqual(menu.previewSport, .tennis)
        let order = Sport.allCases.filter(\.playable)
        menu.move(order.first == .tennis ? .right : .left)
        XCTAssertEqual(menu.previewSport, .golf)
        XCTAssertEqual(menu.screen, .gameSelect)
        menu.move(.down); XCTAssertEqual(menu.focused, "back")
        XCTAssertEqual(menu.previewSport, .golf)
        XCTAssertTrue(menu.focus("sport-golf")); menu.select()
        XCTAssertEqual(menu.screen, .map, "golf goes straight to its courses")
        XCTAssertEqual(menu.mapSport, .golf)
        menu.back(); XCTAssertEqual(menu.screen, .gameSelect)
    }
    func testLastSportIsPerPlayerAndMigratesExistingHistory() {
        let session = SportsSession()
        let p = Player(name: "Beta test", colorIndex: 0), q = Player(name: "Other", colorIndex: 1)
        session.players = [p, q]; session.playerIndex = 0
        let d = UserDefaults.standard, historyKey = "sports.sessions.v1", key = "sports.lastSport.\(p.id.uuidString)"
        let old = d.object(forKey: historyKey)
        defer { d.set(old, forKey: historyKey); d.removeObject(forKey: key) }
        d.set([["playerID": p.id.uuidString, "sport": "golf"]], forKey: historyKey)
        XCTAssertEqual(session.lastPlayedSport, .golf)
        session.playerIndex = 1; XCTAssertEqual(session.lastPlayedSport, .tennis)
        session.playerIndex = 0; d.set("tennis", forKey: key)
        XCTAssertEqual(session.lastPlayedSport, .tennis)
    }
    func testMenuCharactersHoldTheirCurrentSportAndOnlyExplicitEmotesMove() {
        for female in [false, true] {
            var player = Player(name: "Preview", colorIndex: 0); player.standardFemale = female
            for sport in [Sport.tennis, .golf] {
                let coordinator = CharacterModelPreview.Coordinator(cameraDistance: 3.4)
                coordinator.update(player, sport: sport)
                XCTAssertNotNil(coordinator.hero)
                for activity in [ClubPreviewActivity.play, .locker, .settings] {
                    coordinator.configureClub(activity, animate: true)
                    XCTAssertNil(coordinator.motion, "Focusing a menu item must never serve or idle")
                    XCTAssertFalse(coordinator.character.hasActions)
                }
                if sport == .golf { XCTAssertNotNil(MatchHero.golfEquipment(in: coordinator.hero)) }
                coordinator.playEmote("wave", sequence: 1)
                XCTAssertEqual(coordinator.motion?.kind, .clipOnce("wave"))
                XCTAssertEqual(coordinator.motion?.freezeIdle, true)
                guard let rig = coordinator.rig, let motion = coordinator.motion else { XCTFail("Missing actual character rig"); continue }
                coordinator.pauseDisplayLink()
                let end = motion.settledAfter(rig.data) + 1
                let a = motion.pose(rig.data, at: end), b = motion.pose(rig.data, at: end + 2)
                XCTAssertEqual(a.count, b.count)
                for (first, second) in zip(a, b) { XCTAssertEqual(first.t, second.t) }
                coordinator.stopMotion()
            }
        }
    }
    func testFeedbackAndBundledGameplay() {
        XCTAssertEqual(BetaFeedback.url.absoluteString, "sms:+19085909023")
        for name in ["gameplay-golf-cliffside", "gameplay-tennis-skyscraper"] {
            XCTAssertNotNil(Bundle.main.url(forResource: name, withExtension: "mp4"), name)
        }
        XCTAssertEqual(Bundle.main.object(forInfoDictionaryKey: "CFBundleDisplayName") as? String, "Motion Club Beta")
    }
}

extension MenuBetaTests {
    func testPhoneAndTVProof() async throws {
        let menu = TennisMenu.shared, session = SportsSession.shared
        let savedReading = session.golfController
        let savedSport = session.sport, savedCourse = session.golfCourse, savedVenue = session.tennisVenue
        let players = session.players, index = session.playerIndex
        defer { session.golfController = savedReading; session.sport = savedSport; session.golfCourse = savedCourse; session.tennisVenue = savedVenue; session.players = players; session.playerIndex = index; menu.debugShow(.title); session.loading.cancel() }
        let out = URL(fileURLWithPath: ProcessInfo.processInfo.environment["MENU_BETA_PROOF_PATH"] ?? NSTemporaryDirectory() + "menu-beta-proof")
        try FileManager.default.createDirectory(at: out, withIntermediateDirectories: true)
        let scene = try XCTUnwrap(UIApplication.shared.connectedScenes.compactMap { $0 as? UIWindowScene }.first)
        func capture<V: View>(_ view: V, _ name: String, tv: Bool = false) async throws {
            let size = tv ? CGSize(width: 1280, height: 720) : CGSize(width: 402, height: 874)
            let host = UIHostingController(rootView: view); host.safeAreaRegions = []
            let window = UIWindow(windowScene: scene); window.frame = CGRect(origin: .zero, size: size)
            window.rootViewController = host; window.isHidden = false
            host.view.frame = window.bounds; host.view.layoutIfNeeded()
            defer { window.isHidden = true }
            try await Task.sleep(for: .milliseconds(850))
            let format = UIGraphicsImageRendererFormat(); format.scale = 1
            let image = UIGraphicsImageRenderer(size: size, format: format).image { _ in host.view.drawHierarchy(in: host.view.bounds, afterScreenUpdates: true) }
            try XCTUnwrap(image.pngData()).write(to: out.appendingPathComponent(name + ".png"))
        }
        session.golfController = .reference
        try await capture(GolfPhoneController(session: session), "golf-controller-beta")
        for female in [false, true] {
            var p = Player(name: "Adnan", colorIndex: 0); p.standardFemale = female
            session.players = [p]; session.playerIndex = 0
            let key = "sports.lastSport.\(p.id.uuidString)"
            for sport in [Sport.tennis, .golf] {
                UserDefaults.standard.set(sport.rawValue, forKey: key)
                menu.debugShow(.main)
                try await capture(MotionClubHome(menu: menu, compact: true).safeAreaInset(edge: .bottom) { BetaFeedbackBar() }, "home-\(female ? "female" : "male")-\(sport.rawValue)")
                if !female { try await capture(TennisMenuScreen(compact: false), "home-\(sport.rawValue)-tv", tv: true) }
            }
            UserDefaults.standard.removeObject(forKey: key)
        }
        for route in [MenuScreen.party, .multiplayer, .localChoice, .onlineChoice] {
            menu.debugShow(route)
            try await capture(IslandPartyScreen(menu: menu, compact: true).safeAreaInset(edge: .bottom) { BetaFeedbackBar() }, "route-\(String(describing: route))")
        }
        for sport in [Sport.tennis, .golf] {
            menu.debugShow(.gameSelect); menu.focus("sport-\(sport.rawValue)")
            try await capture(IslandSportsScreen(menu: menu, compact: true), "picker-\(sport.rawValue)")
            try await capture(IslandSportsScreen(menu: menu, compact: false), "picker-\(sport.rawValue)-tv", tv: true)
            session.sport = sport.rawValue; session.golfCourse = "cliffside"; session.tennisVenue = "skyscraper"
            menu.debugShow(.loading, launch: MenuLaunch(sport: sport, mode: sport == .golf ? .round : .exhibition))
            session.loading.begin(now: Date())
            try await capture(LoadingScreen(menu: menu, compact: true), "loading-\(sport.rawValue)")
        }
    }
}
