import SwiftUI
import SceneKit
import AVFoundation
import XCTest
@testable import GolfArcade

/// Renders every tennis front-end screen to PNG (TV at 1280×720, phone at 402×874) so the
/// layouts can be reviewed without a TV attached. Output: $TMPDIR/tennis-menu/.
@MainActor
final class TennisMenuSnapshotTests: XCTestCase {
    func testEmoteLoadoutScreens() async throws {
        let menu = TennisMenu.shared, session = SportsSession.shared
        let oldPlayers = session.players, oldIndex = session.playerIndex
        let oldActive = session.active, oldReady = session.ready, oldPaused = session.paused
        let oldSport = session.sport, oldWindow = session.emoteWindow
        defer {
            session.players = oldPlayers; session.playerIndex = oldIndex
            session.active = oldActive; session.ready = oldReady; session.paused = oldPaused
            session.sport = oldSport; session.emoteWindow = oldWindow
            session.loading.cancel(); menu.debugShow(.title)
        }
        var p = Player(name: "Adnan", colorIndex: 0)
        p.equipEmote("pushups", slot: 0); p.equipEmote("bringIt", slot: 1)
        session.players = [p]; session.playerIndex = 0
        menu.debugShow(.character); menu.tap("lk-tab-emotes")
        XCTAssertEqual(menu.lockerTab, .emotes)
        let out = URL(fileURLWithPath: #filePath).deletingLastPathComponent().deletingLastPathComponent().appendingPathComponent("ArtDir/screenshots/emote_loadout")
        try FileManager.default.createDirectory(at: out, withIntermediateDirectories: true)
        let scene = try XCTUnwrap(UIApplication.shared.connectedScenes.compactMap { $0 as? UIWindowScene }.first)
        func capture<V: View>(_ view: V, size: CGSize, name: String) async throws {
            let host = UIHostingController(rootView: view); host.safeAreaRegions = []
            let window = UIWindow(windowScene: scene); window.frame = CGRect(origin: .zero, size: size)
            window.rootViewController = host; window.isHidden = false
            host.view.frame = window.bounds; host.view.layoutIfNeeded()
            defer { window.isHidden = true }
            try await Task.sleep(for: .milliseconds(750))
            let format = UIGraphicsImageRendererFormat(); format.scale = 1
            let image = UIGraphicsImageRenderer(size: size, format: format).image { _ in host.view.drawHierarchy(in: host.view.bounds, afterScreenUpdates: true) }
            try XCTUnwrap(image.pngData()).write(to: out.appendingPathComponent(name + ".png"))
        }
        try await capture(IslandLockerScreen(menu: menu, compact: true), size: CGSize(width: 402, height: 874), name: "locker_phone")
        try await capture(IslandLockerScreen(menu: menu, compact: false), size: CGSize(width: 1280, height: 720), name: "locker_tv")
        session.active = true; session.ready = true; session.paused = false; session.sport = "tennis"
        session.loading.begin(now: Date()); session.loading.markReady(); session.loading.skip()
        session.emoteWindow = "intro"
        XCTAssertTrue(session.canPlayEmote)
        try await capture(TennisEmoteControls(session: session).padding(20).background(IslandUI.navy), size: CGSize(width: 402, height: 180), name: "controller_intro_layout")
        session.emoteWindow = "point"
        XCTAssertTrue(session.canPlayEmote)
        session.emoteWindow = ""
        XCTAssertFalse(session.canPlayEmote)
        try await capture(TennisEmoteControls(session: session).padding(20).background(IslandUI.navy), size: CGSize(width: 402, height: 180), name: "controller_rally_layout")
    }

    func testSportLockerPreviewSwitchesOutfitEquipmentAndBack() async throws {
        let menu = TennisMenu.shared, session = SportsSession.shared
        let oldPlayers = session.players, oldIndex = session.playerIndex
        defer { session.players = oldPlayers; session.playerIndex = oldIndex; menu.debugShow(.title) }
        let out = URL(fileURLWithPath: #filePath).deletingLastPathComponent().deletingLastPathComponent().appendingPathComponent("ArtDir/screenshots/golf_locker_swap")
        try FileManager.default.createDirectory(at: out, withIntermediateDirectories: true)
        let scene = try XCTUnwrap(UIApplication.shared.connectedScenes.compactMap { $0 as? UIWindowScene }.first)
        for female in [false, true] {
            var p = Player(name: "Adnan", colorIndex: 0); p.standardFemale = female
            let c = CharacterModelPreview.Coordinator(cameraDistance: 2.7)
            c.update(p, sport: .tennis)
            let tennis = try XCTUnwrap(c.hero)
            XCTAssertGreaterThan(try XCTUnwrap(c.racket).childNodes.count, 0)
            c.update(p, sport: .golf)
            let golf = try XCTUnwrap(c.hero)
            XCTAssertFalse(golf === tennis, "sport switch must replace the preview even when Player is unchanged")
            XCTAssertNotNil(golf.childNode(withName: "Kit_Head", recursively: true))
            XCTAssertNotNil(golf.childNode(withName: "Kit_Glove_L", recursively: true))
            XCTAssertNil(golf.childNode(withName: "Kit_Sock_L", recursively: true))
            XCTAssertEqual(c.racket?.childNodes.count, 0)
            XCTAssertNotNil(golf.childNode(withName: "clubHead", recursively: true))
            let tool = try XCTUnwrap(golf.childNode(withName: "golfClub", recursively: false))
            let target = c.framingTarget(.racket), box = tool.boundingBox
            let centre = tool.convertPosition(SCNVector3((box.min.x + box.max.x)/2, (box.min.y + box.max.y)/2, (box.min.z + box.max.z)/2), to: nil)
            XCTAssertEqual(target.y, centre.y, accuracy: 0.001, "club close-up targets its full geometry")
            c.configureIdle(sport: nil, animate: false); c.applyFraming()
            XCTAssertTrue(try XCTUnwrap(c.racket).isHidden)
            XCTAssertFalse(try XCTUnwrap(golf.childNode(withName: "golfClub", recursively: false)).isHidden)
            c.update(p, sport: .tennis)
            XCTAssertGreaterThan(try XCTUnwrap(c.racket).childNodes.count, 0)
            XCTAssertNil(c.hero?.childNode(withName: "golfClub", recursively: true))
            session.players = [p]; session.playerIndex = 0; menu.debugShow(.character)
            for sport in ["tennis", "golf"] {
                menu.tap("lk-sport-" + sport)
                let size = CGSize(width: 402, height: 874)
                let host = UIHostingController(rootView: IslandLockerScreen(menu: menu, compact: true)); host.safeAreaRegions = []
                let window = UIWindow(windowScene: scene); window.frame = CGRect(origin: .zero, size: size)
                window.rootViewController = host; window.isHidden = false; host.view.frame = window.bounds; host.view.layoutIfNeeded()
                try await Task.sleep(for: .milliseconds(900))
                let format = UIGraphicsImageRendererFormat(); format.scale = 1
                let image = UIGraphicsImageRenderer(size: size, format: format).image { _ in host.view.drawHierarchy(in: host.view.bounds, afterScreenUpdates: true) }
                try XCTUnwrap(image.pngData()).write(to: out.appendingPathComponent("\(female ? "female" : "male")_\(sport).png"))
                window.isHidden = true
            }
            p.handedness = .left; c.update(p, sport: .golf)
            XCTAssertLessThan(try XCTUnwrap(c.hero).scale.x, 0, "club and glove mirror with the left-handed hero")
        }
    }

    /// Live capture of the same widescreen root used by SportsDisplays on AirPlay.
    func testRecordTVMenuTour() async throws {
        try XCTSkipUnless(ProcessInfo.processInfo.environment["RECORD_TV_MENU"] == "1", "Opt-in review capture")
        let out = URL(fileURLWithPath: #filePath).deletingLastPathComponent().deletingLastPathComponent().appendingPathComponent("ArtDir/review/tv-menus")
        try FileManager.default.createDirectory(at: out, withIntermediateDirectories: true)
        let url = out.appendingPathComponent("TV_Intro_and_Menus.mp4")
        try? FileManager.default.removeItem(at: url)
        let menu = TennisMenu.shared
        menu.debugShow(.title)
        let scene = try XCTUnwrap(UIApplication.shared.connectedScenes.compactMap { $0 as? UIWindowScene }.first)
        let size = CGSize(width: 1280, height: 720)
        let host = UIHostingController(rootView: TennisTVRoot())
        host.safeAreaRegions = []
        let window = UIWindow(windowScene: scene)
        window.frame = CGRect(origin: .zero, size: size)
        window.rootViewController = host; window.isHidden = false
        host.view.frame = window.bounds; host.view.layoutIfNeeded()
        defer { window.isHidden = true; menu.debugShow(.title); SportsSession.shared.loading.cancel() }
        try await Task.sleep(for: .seconds(2))
        let writer = try AVAssetWriter(outputURL: url, fileType: .mp4)
        let input = AVAssetWriterInput(mediaType: .video, outputSettings: [AVVideoCodecKey: AVVideoCodecType.h264, AVVideoWidthKey: 1280, AVVideoHeightKey: 720])
        let adapter = AVAssetWriterInputPixelBufferAdaptor(assetWriterInput: input, sourcePixelBufferAttributes: [kCVPixelBufferPixelFormatTypeKey as String: kCVPixelFormatType_32ARGB, kCVPixelBufferWidthKey as String: 1280, kCVPixelBufferHeightKey as String: 720, kCVPixelBufferCGImageCompatibilityKey as String: true, kCVPixelBufferCGBitmapContextCompatibilityKey as String: true])
        writer.add(input); XCTAssertTrue(writer.startWriting()); writer.startSession(atSourceTime: .zero)
        let format = UIGraphicsImageRendererFormat(); format.scale = 1; format.opaque = true
        let renderer = UIGraphicsImageRenderer(size: size, format: format)
        let started = CACurrentMediaTime()
        var action = 0
        let actions: [(Double, String)] = [(4,"start"),(9,"play"),(13,"sport-tennis"),(18,"back"),(19,"sport-golf"),(23,"back"),(24,"back"),(25,"character"),(31,"back"),(32,"settings"),(36,"back")]
        var frame = 0
        while CACurrentMediaTime() - started < 39 {
            let elapsed = CACurrentMediaTime() - started
            if action < actions.count, elapsed >= actions[action].0 { menu.tap(actions[action].1); action += 1 }
            while !input.isReadyForMoreMediaData { try await Task.sleep(for: .milliseconds(5)) }
            let image = renderer.image { _ in host.view.drawHierarchy(in: CGRect(origin: .zero, size: size), afterScreenUpdates: false) }
            if frame % 90 == 0 { try image.pngData()?.write(to: out.appendingPathComponent("review-\(frame).png")) }
            var buffer: CVPixelBuffer?
            CVPixelBufferPoolCreatePixelBuffer(nil, try XCTUnwrap(adapter.pixelBufferPool), &buffer)
            let pixel = try XCTUnwrap(buffer)
            CVPixelBufferLockBaseAddress(pixel, [])
            let context = try XCTUnwrap(CGContext(data: CVPixelBufferGetBaseAddress(pixel), width: 1280, height: 720, bitsPerComponent: 8, bytesPerRow: CVPixelBufferGetBytesPerRow(pixel), space: CGColorSpaceCreateDeviceRGB(), bitmapInfo: CGImageAlphaInfo.noneSkipFirst.rawValue))
            context.draw(try XCTUnwrap(image.cgImage), in: CGRect(origin: .zero, size: size))
            CVPixelBufferUnlockBaseAddress(pixel, [])
            XCTAssertTrue(adapter.append(pixel, withPresentationTime: CMTime(seconds: elapsed, preferredTimescale: 60000)))
            frame += 1
            try await Task.sleep(for: .milliseconds(16))
        }
        input.markAsFinished(); await writer.finishWriting()
        XCTAssertEqual(writer.status, .completed, "\(String(describing: writer.error))")
    }

    func testApprovedIslandScreens() async throws {
        let menu = TennisMenu.shared, session = SportsSession.shared
        let oldPlayers = session.players, oldIndex = session.playerIndex
        var referencePlayer = Player(name: "Adnan", colorIndex: 0); referencePlayer.setSkin(0.35); referencePlayer.hairColor = 3
        session.players = [referencePlayer]; session.playerIndex = 0
        defer { session.players = oldPlayers; session.playerIndex = oldIndex; session.loading.cancel(); session.menuPauseVisible = false; menu.debugShow(.title) }
        let out = URL(fileURLWithPath: #filePath).deletingLastPathComponent().deletingLastPathComponent().appendingPathComponent("ArtDir/ui/toybox-simple-v6/runtime")
        try FileManager.default.createDirectory(at: out, withIntermediateDirectories: true)
        let scene = try XCTUnwrap(UIApplication.shared.connectedScenes.compactMap { $0 as? UIWindowScene }.first)
        let screens: [(String, MenuScreen)] = [("intro", .title), ("home", .main), ("sports", .gameSelect), ("courts", .map), ("tennis", .hub(.tennis)), ("campaign", .campaign), ("quick-match", .exhibition), ("training", .training), ("locker", .character), ("settings", .settings), ("guide", .howTo), ("connect", .connect), ("results", .results), ("pause", .main), ("loading", .loading)]
        for (name, screen) in screens {
            menu.debugShow(screen, launch: MenuLaunch(round: 0), result: MatchResult(won: true, score: "6–4", round: 0))
            session.menuPauseVisible = name == "pause"
            if screen == .loading { session.loading.begin(now: Date()); session.loading.reach(0.72); session.loading.tick(now: Date()) }
            let size = CGSize(width: 1280, height: 720)
            let host = UIHostingController(rootView: TennisTVRoot()); host.safeAreaRegions = []
            let window = UIWindow(windowScene: scene); window.frame = CGRect(origin: .zero, size: size)
            window.rootViewController = host; window.isHidden = false; host.view.frame = window.bounds; host.view.layoutIfNeeded()
            try await Task.sleep(for: .seconds(1))
            let format = UIGraphicsImageRendererFormat(); format.scale = 1
            func capture(_ suffix: String = "") throws {
                let image = UIGraphicsImageRenderer(size: size, format: format).image { _ in host.view.drawHierarchy(in: window.bounds, afterScreenUpdates: true) }
                try XCTUnwrap(image.pngData()).write(to: out.appendingPathComponent(name + suffix + ".png"))
            }
            try capture()
            if screen == .loading {
                session.loading.practice(); try await Task.sleep(for: .milliseconds(600)); try capture("-swing")
                func findScene(_ view: UIView) -> SCNView? { if let v = view as? SCNView { return v }; return view.subviews.compactMap(findScene).first }
                let scn = try XCTUnwrap(findScene(host.view))
                let root = try XCTUnwrap(scn.scene?.rootNode.childNode(withName: MatchHero.rootName, recursively: true))
                var animated = 0
                root.enumerateChildNodes { node, _ in
                    if let morph = node.morpher, morph.weights.contains(where: { $0.doubleValue > 0.01 }) { animated += 1 }
                }
                XCTAssertGreaterThan(animated, 3, "The swing must animate the character (body, face, racket), not just increment its trigger.")
                try await Task.sleep(for: .seconds(2)); session.loading.practice(); XCTAssertEqual(session.loading.practiceSequence, 2)
                try await Task.sleep(for: .milliseconds(600)); try capture("-repeat")
            }
            window.isHidden = true
        }
        session.menuPauseVisible = false
        menu.debugShow(.main)
        try await saveHosted(TennisPhoneMenu(), "island-home-phone", CGSize(width: 402, height: 874))
        menu.debugShow(.character)
        try await saveHosted(TennisPhoneMenu(), "island-locker-phone", CGSize(width: 402, height: 874))
        for (name, route) in [("quick",MenuScreen.exhibition),("settings",.settings),("campaign",.campaign),("loading",.loading)] {
            menu.debugShow(route)
            try await saveHosted(TennisPhoneMenu(), "island-\(name)-phone", CGSize(width:402,height:874))
        }
    }

    private var folder: URL {
        // MENU_SNAP_DIR (passed as TEST_RUNNER_MENU_SNAP_DIR by xcodebuild) lets a review write straight to a chosen folder.
        let url = ProcessInfo.processInfo.environment["MENU_SNAP_DIR"].map { URL(fileURLWithPath: $0) }
            ?? FileManager.default.temporaryDirectory.appendingPathComponent("tennis-menu")
        try? FileManager.default.createDirectory(at: url, withIntermediateDirectories: true)
        return url
    }

    private func save<V: View>(_ view: V, _ name: String, _ size: CGSize) throws {
        let renderer = ImageRenderer(content: view.frame(width: size.width, height: size.height))
        renderer.scale = 1.5
        let image = try XCTUnwrap(renderer.uiImage, "\(name) did not render")
        try XCTUnwrap(image.pngData()).write(to: folder.appendingPathComponent("\(name).png"))
    }

    func testLobbyLayouts() async throws {
        let menu = TennisMenu.shared
        menu.debugShow(.title); menu.tap("start")
        defer { menu.debugShow(.title); SportsSession.shared.loading.cancel() }
        let scene = try XCTUnwrap(UIApplication.shared.connectedScenes.compactMap { $0 as? UIWindowScene }.first)
        let states: [(String, CGSize, Bool, MenuScreen, MenuLaunch?)] = [
            ("lobby-tv", CGSize(width: 1280, height: 720), false, .main, nil),
            ("lobby-mac", CGSize(width: 1440, height: 900), false, .main, nil),
            ("lobby-phone", CGSize(width: 402, height: 874), true, .main, nil),
            ("party-phone", CGSize(width: 402, height: 874), true, .party, nil),
            ("loading-tennis", CGSize(width: 1280, height: 720), false, .loading, MenuLaunch(mode: .exhibition, round: 0)),
            ("loading-golf-phone", CGSize(width: 402, height: 874), true, .loading, MenuLaunch(sport: .golf, mode: .round)),
            ("loading-stalled-phone", CGSize(width: 402, height: 874), true, .loading, MenuLaunch(mode: .training)),
            ("home-large-text", CGSize(width: 402, height: 874), true, .main, nil)
        ]
        for (name, size, compact, screen, launch) in states {
            menu.debugShow(screen, launch: launch, row: screen == .main ? 1 : 0)
            if screen == .loading {
                let loading = SportsSession.shared.loading
                let start = Date()
                loading.begin(now: start); loading.reach(0.4)
                loading.tick(now: start.addingTimeInterval(name.contains("stalled") ? 22 : 0.5))
            }
            let host = UIHostingController(rootView: Group {
                if compact { TennisPhoneMenu() } else { TennisTVRoot() }
            }.environment(\.dynamicTypeSize, name == "home-large-text" ? .accessibility1 : .large))
            let window = UIWindow(windowScene: scene)
            window.frame = CGRect(origin: .zero, size: size)
            window.rootViewController = host; window.isHidden = false
            host.view.frame = window.bounds; host.view.layoutIfNeeded()
            try await Task.sleep(for: .seconds(2))
            let image = UIGraphicsImageRenderer(size: size).image { _ in
                host.view.drawHierarchy(in: window.bounds, afterScreenUpdates: true)
            }
            try XCTUnwrap(image.pngData()).write(to: folder.appendingPathComponent("\(name).png"))
            window.isHidden = true
        }
    }

    func testLoadingIdlesRespectReducedMotionAndSport() throws {
        let preview = CharacterModelPreview.Coordinator()
        preview.update(Player(name: "Test", colorIndex: 0))
        preview.configureIdle(sport: .golf, animate: true)
        XCTAssertNotNil(preview.character.childNode(withName: "idleClub", recursively: false))
        XCTAssertEqual(preview.character.childNode(withName: "racket", recursively: true)?.isHidden, true)
        XCTAssertTrue(preview.character.hasActions)
        preview.configureIdle(sport: .tennis, animate: false)
        XCTAssertNil(preview.character.childNode(withName: "idleClub", recursively: false))
        XCTAssertFalse(preview.character.hasActions)
        XCTAssertEqual(preview.character.childNode(withName: "racket", recursively: true)?.isHidden, false)
        XCTAssertEqual(preview.scene.rootNode.childNode(withName: "idleBall", recursively: false)?.hasActions, false)
        XCTAssertEqual(PartyLoadingTips.tips(for: .tennis).count, 10)
        XCTAssertEqual(PartyLoadingTips.tips(for: .golf).count, 10)
    }

    func testCharacterModelVariants() throws {
        for i in 0..<4 {
            var player = Player(name: "Preview", colorIndex: 0)
            player.hairStyle = i; player.faceShape = i; player.standardSkin = i + 1
            player.shirt = i + 1; player.heightChoice = i; player.buildChoice = i
            let coordinator = CharacterModelPreview.Coordinator()
            coordinator.update(player)
            XCTAssertNotNil(coordinator.hero, "the locker mirror shows the match hero")
            XCTAssertGreaterThan(coordinator.hero?.childNodes.count ?? 0, 2)
            let renderer = SCNRenderer(device: nil, options: nil)
            renderer.scene = coordinator.scene
            renderer.pointOfView = coordinator.scene.rootNode.childNodes.first { $0.camera != nil }
            let image = renderer.snapshot(atTime: 0, with: CGSize(width: 600,height: 800), antialiasingMode: .multisampling4X)
            try XCTUnwrap(image.pngData()).write(to: folder.appendingPathComponent("character-variant-\(i).png"))
        }
        let player = SportsSession.shared.players[0]
        let data = try JSONEncoder().encode(player)
        XCTAssertEqual(try JSONDecoder().decode(Player.self, from: data), player)
    }

    private func saveHosted<V: View>(_ view: V, _ name: String, _ size: CGSize) async throws {
        let scene = try XCTUnwrap(UIApplication.shared.connectedScenes.compactMap { $0 as? UIWindowScene }.first)
        let host = UIHostingController(rootView: view)
        let window = UIWindow(windowScene: scene)
        window.frame = CGRect(origin: .zero, size: size); window.rootViewController = host; window.isHidden = false
        host.view.frame = window.bounds; host.view.layoutIfNeeded()
        try await Task.sleep(for: .seconds(1))
        let image = UIGraphicsImageRenderer(size: size).image { _ in host.view.drawHierarchy(in: window.bounds, afterScreenUpdates: true) }
        try XCTUnwrap(image.pngData()).write(to: folder.appendingPathComponent("\(name).png"))
        window.isHidden = true
    }

    func testNewBaseLocker() async throws {
        let session=SportsSession.shared, menu=TennisMenu.shared
        let players=session.players, index=session.playerIndex
        defer { session.players=players; session.playerIndex=index; menu.debugShow(.title) }
        for female in [false,true] {
            var p=Player(name: female ? "Maya" : "Alex",colorIndex:0)
            p.standardFemale=female; p.bodySize=female ? 1 : 0; p.hairStyle=female ? 3 : 1
            session.players=[p];session.playerIndex=0;menu.debugShow(.character)
            try await saveHosted(IslandLockerScreen(menu:menu,compact:true),"new-locker-\(female)",CGSize(width:402,height:874))
            try await saveHosted(IslandLockerScreen(menu:menu,compact:false),"new-locker-tv-\(female)",CGSize(width:1280,height:720))
        }
    }

    func testFinishedPhoneControllers() async throws {
        let session = SportsSession.shared, menu = TennisMenu.shared
        TennisCampaign.shared.restart()
        TennisCampaign.shared.record(round: 0, won: true)
        menu.debugShow(.loading, launch: MenuLaunch(round: 0), result: MatchResult(won: true, score: "3–0", round: 0))
        XCTAssertEqual(menu.afterMatchChoices.first, .next)
        session.sport = "tennis"
        session.active = true
        session.finishedMatch = (true, "3–0")
        defer { session.active = false; session.finishedMatch = nil; TennisMenu.shared.classic = false }
        try await saveHosted(SportsHome(), "finish-phone", CGSize(width: 402, height: 874))
        TennisMenu.shared.classic = true
        try await saveHosted(SportsHome(), "finish-classic", CGSize(width: 402, height: 874))
        try await saveHosted(SportsPreviewControls(), "finish-preview", CGSize(width: 800, height: 150))
    }

    func testUltimateAndDiveControls() async throws {
        let session = SportsSession.shared
        let savedChoice = session.ultimateChoice
        defer { session.loadoutLocked = false; session.chooseUltimate(savedChoice) }
        session.sport = "tennis"; session.ready = true; session.touch = true; session.paused = true
        session.finishedMatch = nil; session.loadoutLocked = false
        defer { session.ready = false; session.paused = true; session.loadoutLocked = false; session.canDive = false; session.canArmUltimate = false }
        session.chooseUltimate(.curveball)
        XCTAssertEqual(session.ultimateChoice, savedChoice, "removed ultimate selection is ignored")
        session.paused = false; session.ultimateMeter = 1; session.canArmUltimate = true; session.canDive = true; session.tennisPhase = "rally"
        try await saveHosted(TennisAbilityControls(session: session).padding().preferredColorScheme(.dark), "ultimate-actions", CGSize(width: 390,height: 180))
        try await saveHosted(SportsPreviewControls(), "ultimate-preview", CGSize(width: 800,height: 300))
        session.loading.cancel()
        try await saveHosted(TennisRacketController(session: session), "ultimate-controller", CGSize(width: 390,height: 844))
        try await saveHosted(TennisRacketController(session: session), "ultimate-controller-small", CGSize(width: 375,height: 667))
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
        session.tutorialStep = (0, 5, "SERVE: press TOSS near the meter's middle. Swing overhead as the ball reaches its highest point. Land one in the box.")
        session.tennisPhase = "serve"
        try save(TennisRacketController(session: session), "phone-tutorial-serve", phone)
        session.tutorialStep = (3, 5, "AIM RIGHT: now angle the face right and land a return toward the other gold ring.")
        session.tennisPhase = "rally"
        try save(TennisRacketController(session: session), "phone-tutorial-aim", phone)
        session.tutorialStep = nil
        session.tennisPhase = "" 
        menu.debugShow(.title)
    }
}


// Scripted component review, intentionally separate from integrated-device acceptance.
extension TennisMenuSnapshotTests {
    func testFilmPresentationNativeComponents() async throws {
        let session=SportsSession.shared, menu=TennisMenu.shared, displays=SportsDisplays.shared
        let scene=try XCTUnwrap(UIApplication.shared.connectedScenes.compactMap { $0 as? UIWindowScene }.first)
        let window=UIWindow(windowScene:scene)
        let oldPhone=displays.phone, oldActive=session.active, oldSport=session.sport, oldIntros=session.presentationIntros
        let oldMatch=session.finishedMatch
        let oldFlyover=session.holeFlyover, oldMoments=session.presentationBigMoments
        let root=URL(fileURLWithPath:NSTemporaryDirectory()).appendingPathComponent("presentation-native-films",isDirectory:true)
        try FileManager.default.createDirectory(at:root,withIntermediateDirectories:true)
        window.frame=CGRect(x:0,y:0,width:402,height:874); window.windowLevel = .normal + 2
        window.isHidden=false; displays.phone=window
        defer {
            displays.finishLoadingCover();session.loading.cancel();session.active=oldActive;session.sport=oldSport
            session.presentationIntros=oldIntros;session.finishedMatch=oldMatch
            session.holeFlyover=oldFlyover;session.presentationBigMoments=oldMoments
            displays.phone=oldPhone;window.isHidden=true;menu.debugShow(.title)
        }
        func show<V:View>(_ view:V, size:CGSize=CGSize(width:402,height:874)) {
            window.frame=CGRect(origin:.zero,size:size)
            let host=UIHostingController(rootView:view);host.safeAreaRegions=[];window.rootViewController=host
            host.view.frame=window.bounds;host.view.layoutIfNeeded()
        }
        func film(_ name:String, seconds:Double, update:(Double)->Void={_ in}) async throws {
            let dir=root.appendingPathComponent(name,isDirectory:true);try FileManager.default.createDirectory(at:dir,withIntermediateDirectories:true)
            let format=UIGraphicsImageRendererFormat();format.scale=1
            for frame in 0..<Int(seconds*10) {
                update(Double(frame)/10);window.rootViewController?.view.layoutIfNeeded()
                let image=UIGraphicsImageRenderer(size:window.bounds.size,format:format).image { _ in
                    for visible in scene.windows.filter({!$0.isHidden}).sorted(by:{$0.windowLevel < $1.windowLevel}) {
                        visible.drawHierarchy(in:window.bounds,afterScreenUpdates:true)
                    }
                }
                try XCTUnwrap(image.jpegData(compressionQuality:0.88)).write(to:dir.appendingPathComponent(String(format:"frame-%04d.jpg",frame)))
                try await Task.sleep(for:.milliseconds(100))
            }
        }
        for sport in ["tennis","golf"] {
            session.active=true;session.sport=sport
            menu.debugShow(.loading,launch:MenuLaunch(sport:sport=="golf" ? .golf:.tennis,mode:sport=="golf" ? .round:.exhibition,round:sport=="tennis" ? 0:nil))
            show(TennisPhoneMenu());session.loading.begin(now:LoadingModel.clockNow)
            session.loading.onFinish={displays.finishLoadingCover()};displays.beginLoadingCover(in:window,startRuntime:{})
            try await film("L1_\(sport)_native_cover_fixture_phone",seconds:5) { t in
                if t>=2 {session.loading.reach(t<3 ? 0.4:1)}
                if t>=3.5 {session.loading.markSceneReady();session.loading.markReady()}
                session.loading.tick(now:LoadingModel.clockNow)
            }
            displays.finishLoadingCover();session.loading.cancel()
        }
        session.active=false;session.sport="tennis"
        menu.debugShow(.loading,launch:MenuLaunch(mode:.training))
        session.loading.begin(now:LoadingModel.clockNow)
        show(LoadingScreen(menu:menu,compact:true))
        try await film("L1_long_load_tips_retry_fixture_phone",seconds:23) { t in
            if t>=10 {session.loading.reach(0.4)}
            session.loading.tick(now:LoadingModel.clockNow)
        }
        session.loading.cancel()
        let service=MultiplayerService.shared,bus=MultiplayerTests.Bus()
        let oldLaunch=service.onMatchRequested,oldReturn=service.onReturnToLobby
        service.onMatchRequested={_ in};service.onReturnToLobby={}
        service.setIdentity(name:"Adnan")
        try service.host(using:bus.link("film-host"))
        var peers:[MultiplayerService]=[]
        for (i,name) in ["Maya","Sam","Leo"].enumerated() {
            let peer=MultiplayerService(sendToRuntime:{_ in true},pollRuntime:{nil},clock:{ProcessInfo.processInfo.systemUptime})
            peer.setIdentity(name:name);peer.onMatchRequested={_ in};peer.onReturnToLobby={}
            try peer.connect(using:bus.link("film-\(i)"));peers.append(peer)
        }
        defer {for peer in peers.reversed(){peer.leave()};service.leave();service.onMatchRequested=oldLaunch;service.onReturnToLobby=oldReturn}
        try service.configure(.golf)
        for peer in peers {try peer.setReady(true)};try service.setReady(true);try service.startMatch()
        session.sport="golf"
        session.loading.begin(now:LoadingModel.clockNow,multiplayer:true)
        menu.debugShow(.online(.loading));show(LoadingScreen(menu:menu,compact:true))
        try await film("L1_multiplayer_delayed_keep_waiting_fixture_phone",seconds:24) { t in
            if t==2 {session.loading.markReady();service.runtimeLoaded();peers[0].runtimeLoaded();peers[1].runtimeLoaded()}
            service.update()
            session.loading.updatePlayers(waiting:service.lobby!.competitors.filter{!$0.loaded}.map(\.name),allReady:false)
            session.loading.tick(now:LoadingModel.clockNow)
            if t==22 {try? service.keepWaitingForLoad();session.loading.keepWaiting()}
        }
        for peer in peers.reversed(){peer.leave()};peers=[];service.leave()
        menu.debugShow(.settings);show(IslandSettingsScreen(menu:menu,compact:true))
        try await film("preferences_full_short_off_flyover_big_moments_fixture_phone",seconds:6) { t in
            session.presentationIntros=t<2 ? "full":t<4 ? "short":"off"
            session.holeFlyover=t<3;session.presentationBigMoments=t<4
        }
        session.active=true;session.sport="tennis";session.finishedMatch=(true,"3–1")
        show(MatchFinishControls(session:session))
        try await film("L7_results_rematch_leave_fixture_phone",seconds:4)
        session.finishedMatch=nil;session.active=false;menu.debugShow(.main);show(TennisPhoneMenu())
        session.presentationIntros="full";displays.beginExitCover()
        try await film("L8_native_exit_cover_fixture_phone",seconds:3)
        menu.debugShow(.loading,launch:MenuLaunch(mode:.exhibition,round:0));session.sport="tennis"
        session.loading.begin(now:LoadingModel.clockNow)
        show(LoadingScreen(menu:menu,compact:false),size:CGSize(width:1280,height:720))
        try await film("L1_tennis_loading_card_fixture_tv_canvas",seconds:4) {_ in session.loading.tick(now:LoadingModel.clockNow)}
        print("PRESENTATION_NATIVE_FILMS=\(root.path)")
    }
}
