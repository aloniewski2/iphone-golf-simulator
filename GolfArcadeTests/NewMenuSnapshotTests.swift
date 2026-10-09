import SwiftUI
import SceneKit
import Darwin
import XCTest
@testable import GolfArcade

/// Renders the redesigned menu screens (phone 402×874 and TV 1280×720) so they can be reviewed without a device.
/// Output folder: $MENU_SNAP_DIR (pass TEST_RUNNER_MENU_SNAP_DIR=... to xcodebuild) or $TMPDIR/tennis-menu.
@MainActor
final class NewMenuSnapshotTests: XCTestCase {
    /// Online lobby style reference. Runs before any application changes and uses the live phone / TV roots.
    func testOnlineLobbyBeforeScreens() async throws {
        let restore = setUpPlayer(); defer { restore() }
        let menu = TennisMenu.shared
        for (name, screen) in [("home", MenuScreen.main), ("party", .party), ("locker-gear", .character),
                               ("locker-customize", .character), ("map", .map), ("post-match", .results)] {
            menu.debugShow(screen, launch: MenuLaunch(round: 0), result: MatchResult(won: true, score: "6–3", round: 0))
            if name == "locker-customize" { menu.tap("lk-tab-customize") }
            try await saveHosted(TennisPhoneMenu(), "\(name)-phone", phone)
            try await saveHosted(TennisTVRoot(), "\(name)-tv", tv)
        }
    }
    func testOnlineLobbyScreens() async throws {
        let restore = setUpPlayer(); defer { restore() }
        let menu = TennisMenu.shared, original = menu.online
        let service = MultiplayerService(sendToRuntime:{ _ in true },pollRuntime:{ nil })
        menu.online = OnlineLobbyMenu(service:service); defer { service.leave(); menu.online = original }
        menu.debugShow(.party)
        try await both("party") { IslandPartyScreen(menu:menu,compact:$0) }
        for route in [OnlineLobbyScreen.entry,.nearby,.searching] {
            menu.debugShow(.online(route)); try await both(route.rawValue) { IslandOnlineScreen(menu:menu,route:route,compact:$0) }
        }
        service.mockNearby(); menu.debugShow(.online(.nearby))
        try await both("nearby-found") { IslandOnlineScreen(menu:menu,route:.nearby,compact:$0) }
        menu.debugShow(.online(.entry)); try await both("signin") { IslandOnlineScreen(menu:menu,route:.entry,compact:$0) }
        menu.onlineNotice("The players need compatible versions of the game.")
        try await both("error") { IslandOnlineScreen(menu:menu,route:.entry,compact:$0) }
        for count in [1,2,4] {
            try service.enableMock(count:count,player:try XCTUnwrap(menu.player)); menu.debugShow(.online(.lobby))
            try await both("lobby-\(count)") { IslandOnlineScreen(menu:menu,route:.lobby,compact:$0) }
        }
        try await both("lobby-terrace") { IslandOnlineScreen(menu:menu,route:.lobby,compact:$0,room:.terrace) }
        for route in [OnlineLobbyScreen.emotes,.clothes,.settings,.leave] {
            menu.debugShow(.online(route)); try await both(route.rawValue) { IslandOnlineScreen(menu:menu,route:route,compact:$0) }
        }
        menu.debugShow(.online(.settings)); menu.tap("net-settings-seats")
        try await both("settings-seats") { IslandOnlineScreen(menu:menu,route:.settings,compact:$0) }
        menu.tap("net-settings-match")
        menu.debugShow(.online(.clothes)); menu.tap("lk-tab-customize")
        try await both("clothes-customize") { IslandOnlineScreen(menu:menu,route:.clothes,compact:$0) }
        menu.tap("lk-revert")
        try service.configure(.golf,venue:"postcards"); menu.debugShow(.online(.lobby))
        try await both("golf-four") { IslandOnlineScreen(menu:menu,route:.lobby,compact:$0) }
        try service.configure(.tennis,venue:"resort")
        service.mockState(phase:.lobby,ready:true); menu.debugShow(.online(.lobby))
        try await both("lobby-ready") { IslandOnlineScreen(menu:menu,route:.lobby,compact:$0) }
        service.mockState(phase:.loading); menu.debugShow(.online(.loading))
        try await both("loading") { IslandOnlineScreen(menu:menu,route:.loading,compact:$0) }
        service.mockState(phase:.playing,disconnected:true,spectator:true); menu.debugShow(.online(.match))
        try await both("match-reconnecting") { compact in ZStack { IslandBackdrop(room:.terrace); MultiplayerMatchOverlay(menu:menu,compact:compact) } }
        service.mockState(phase:.playing,paused:true,spectator:true)
        try await both("match-paused") { compact in ZStack { IslandBackdrop(room:.terrace); MultiplayerMatchOverlay(menu:menu,compact:compact) } }
        service.mockState(phase:.playing,spectator:true,error:"Sam left the match.")
        try await both("match-left") { compact in ZStack { IslandBackdrop(room:.terrace); MultiplayerMatchOverlay(menu:menu,compact:compact) } }
        service.mockState(phase:.results); menu.online.winnerID = service.localID; menu.debugShow(.online(.results))
        try await both("results") { IslandOnlineScreen(menu:menu,route:.results,compact:$0) }
        service.mockState(phase:.lobby,guest:true); menu.debugShow(.online(.settings))
        try await both("settings-guest") { IslandOnlineScreen(menu:menu,route:.settings,compact:$0) }
        service.mockState(phase:.lobby,error:"Loading took too long. Everyone is back in the lobby."); menu.debugShow(.online(.lobby));menu.onlineNotice(service.lastError!)
        try await both("loading-timeout") { IslandOnlineScreen(menu:menu,route:.lobby,compact:$0) }
    }
    func testOnlineLobbyClickRemoteAndTouchPanels() throws {
        let restore=setUpPlayer();defer { restore() }
        let menu=TennisMenu.shared,original=menu.online
        let service=MultiplayerService(sendToRuntime:{ _ in true },pollRuntime:{ nil })
        menu.online=OnlineLobbyMenu(service:service);defer { service.leave();menu.online=original }
        menu.debugShow(.party);menu.move(.down);menu.select()
        XCTAssertEqual(menu.screen,.online(.entry));menu.back();XCTAssertEqual(menu.screen,.party)
        menu.tap("partyOnline");XCTAssertEqual(menu.screen,.online(.entry));menu.back()
        try service.enableMock(count:4,player:try XCTUnwrap(menu.player));menu.showOnline(.lobby)
        menu.select();XCTAssertTrue(service.lobby!.participants[0].ready)
        menu.move(.down);menu.select();XCTAssertEqual(menu.screen,.online(.emotes))
        menu.select();XCTAssertEqual(service.emotes[service.localID]?.emoteID,"scuba");menu.back()
        menu.tap("net-clothes");let previous=try XCTUnwrap(menu.player)
        menu.tap("lk-tab-customize");menu.lockerEdit { $0.setOutfitHex("shirt","D3F34B") };menu.tap("lk-revert")
        XCTAssertEqual(menu.player,previous);XCTAssertEqual(menu.screen,.online(.lobby))
        menu.tap("net-settings");menu.tap("net-settings-seats")
        let observer=service.lobby!.participants[2].id;menu.tap("net-seat-\(observer)")
        XCTAssertEqual(service.lobby!.participants[2].seat,1)
        menu.tap("net-settings-match");menu.tap("net-sport-golf");XCTAssertEqual(service.lobby?.sport,.golf)
        menu.back();menu.tap("net-leave");XCTAssertEqual(menu.screen,.online(.leave));menu.back();XCTAssertEqual(menu.screen,.online(.lobby))
        service.mockState(phase:.lobby,guest:true);menu.tap("net-settings")
        XCTAssertEqual(menu.rows(menu.screen),[["net-settings-seats"],["back"]])
        let before=service.lobby;menu.tap("net-sport-tennis");XCTAssertEqual(service.lobby,before)
        menu.online.installCallbacks(menu)
        menu.online.winnerID = service.localID
        service.onResult?("{\"reason\":\"interrupted\",\"winner\":-1}")
        XCTAssertNil(menu.online.winnerID,"An interrupted result cannot crown a spectator in seat -1")
    }

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

extension NewMenuSnapshotTests {
    /// Controlled rendering measurements identify the cost before changing the shipped look.
    func testLobbyRenderBottleneckMatrix() async throws {
        let service = MultiplayerService(sendToRuntime:{ _ in true },pollRuntime:{ nil })
        try service.enableMock(count:4,player:Player(name:"Adnan",colorIndex:0)); defer { service.leave() }
        let peers = try XCTUnwrap(service.lobby?.participants)
        let scene = try XCTUnwrap(UIApplication.shared.connectedScenes.compactMap { $0 as? UIWindowScene }.first)
        var evidence: [String] = []
        for mode in ["animated", "frozen-pose", "animated-no-morphers", "animated-simple-lighting"] {
            let coordinator = LobbyHeroStage.Coordinator()
            let scn = SCNView(frame:CGRect(x:0,y:0,width:390,height:420))
            scn.scene=coordinator.recipe.scene; scn.pointOfView=coordinator.recipe.camera; scn.delegate=coordinator
            scn.isPlaying=true; scn.preferredFramesPerSecond=60; scn.antialiasingMode = .multisampling4X
            let window=UIWindow(windowScene:scene), controller=UIViewController(); controller.view=scn
            window.rootViewController=controller; window.frame=scn.frame; window.isHidden=false
            coordinator.view=scn
            coordinator.update(LobbyHeroStage(participants:peers,sport:.tennis,localID:service.localID,networkTime:service.networkTime))
            XCTAssertEqual(coordinator.heroes.count,4)
            if mode == "animated-no-morphers" {
                scn.scene?.rootNode.enumerateChildNodes { node,_ in node.morpher=nil }
            }
            if mode == "animated-simple-lighting" {
                scn.scene?.rootNode.enumerateChildNodes { node,_ in
                    for material in node.geometry?.materials ?? [] { material.shaderModifiers=nil; material.lightingModel = .lambert }
                }
            }
            if mode != "frozen-pose" { coordinator.start() }
            try await Task.sleep(for:.seconds(2))
            coordinator.resetMeasurement(); try await Task.sleep(for:.seconds(5))
            let start=CACurrentMediaTime(), count=240
            for k in 0..<count { coordinator.tickPose(at:Double(k)*0.017) }
            let poseMS=(CACurrentMediaTime()-start)*1000/Double(count)
            let row="\(mode): \(coordinator.measuredFPS) FPS, \(poseMS) ms four-player pose, \(coordinator.frames) rendered frames"
            evidence.append(row); NSLog("[VisualPerformance] \(row)")
            try XCTUnwrap(scn.snapshot().pngData()).write(to:folder.appendingPathComponent("diagnostic-\(mode).png"))
            coordinator.stop(); scn.isPlaying=false; window.isHidden=true; scn.scene=nil
        }
        try evidence.joined(separator:"\n").write(to:folder.appendingPathComponent("lobby-render-bottlenecks.txt"),atomically:true,encoding:.utf8)
    }
    func testOnlineLobbyFourHeroesRenderedFPSAndMemory() async throws {
        let service = MultiplayerService(sendToRuntime:{ _ in true },pollRuntime:{ nil })
        try service.enableMock(count:4,player:Player(name:"Adnan",colorIndex:0)); defer { service.leave() }
        let peers = try XCTUnwrap(service.lobby?.participants)
        for sport in [MultiplayerSport.tennis,.golf] {
            let stage = LobbyHeroStage(participants:peers,sport:sport,localID:service.localID,networkTime:service.networkTime)
            let coordinator = LobbyHeroStage.Coordinator()
            let scn = SCNView(frame:CGRect(x:0,y:0,width:390,height:420)); scn.backgroundColor = UIColor(IslandUI.paper)
            scn.scene = coordinator.recipe.scene; scn.pointOfView = coordinator.recipe.camera; scn.delegate = coordinator
            scn.isPlaying = true; scn.preferredFramesPerSecond = 60; scn.antialiasingMode = .multisampling4X
            let scene = try XCTUnwrap(UIApplication.shared.connectedScenes.compactMap { $0 as? UIWindowScene }.first)
            let window = UIWindow(windowScene:scene), controller = UIViewController(); controller.view = scn
            window.rootViewController = controller; window.frame = scn.frame; window.isHidden = false
            coordinator.view = scn; coordinator.update(stage); coordinator.start()
            defer { coordinator.stop(); scn.isPlaying = false; window.isHidden = true }
            XCTAssertEqual(coordinator.heroes.count,4,"All four actual \(sport.rawValue) rigs must render")
            try await Task.sleep(for:.seconds(5))
            coordinator.resetMeasurement()
            coordinator.heroes[peers[0].id]?.motion.play(sport == .golf ? "matchWin" : "scuba",at:service.networkTime,now:service.networkTime)
            try await Task.sleep(for:.seconds(10))
            let fps = coordinator.measuredFPS
            var info = task_vm_info_data_t(); var size = mach_msg_type_number_t(MemoryLayout<task_vm_info_data_t>.size / MemoryLayout<integer_t>.size)
            let status = withUnsafeMutablePointer(to:&info) { ptr in ptr.withMemoryRebound(to:integer_t.self,capacity:Int(size)) { task_info(mach_task_self_,task_flavor_t(TASK_VM_INFO),$0,&size) } }
            let memory = status == KERN_SUCCESS ? Double(info.phys_footprint) / 1_048_576 : -1
            let report = "Rendered SceneKit frames=\(coordinator.frames), seconds=\(coordinator.measuredSeconds), fps=\(fps), footprintMB=\(memory), model=\(ProcessInfo.processInfo.environment["SIMULATOR_MODEL_IDENTIFIER"] ?? UIDevice.current.model), 4 actual \(sport.rawValue) heroes + reaction, 4x MSAA\n"
            try report.write(to:folder.appendingPathComponent("performance-\(sport.rawValue).txt"),atomically:true,encoding:.utf8)
            try XCTUnwrap(scn.snapshot().pngData()).write(to:folder.appendingPathComponent("performance-\(sport.rawValue).png"))
            XCTAssertGreaterThanOrEqual(fps,55,report)
        }
    }
}
