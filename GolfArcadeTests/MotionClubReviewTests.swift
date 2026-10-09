import SwiftUI
import SceneKit
import AVFoundation
import XCTest
@testable import GolfArcade

@MainActor final class MotionClubReviewTests: XCTestCase {
    func testHingeActuallyRotatesAroundEdge() async throws {
        let controller = ClubCameraHost.Controller(AnyView(Color.red), route: .title, paused: false)
        let scene = try XCTUnwrap(UIApplication.shared.connectedScenes.compactMap { $0 as? UIWindowScene }.first)
        let window = UIWindow(windowScene: scene)
        window.rootViewController = controller; window.makeKeyAndVisible()
        defer { window.isHidden = true; controller.stop() }
        try await Task.sleep(for: .milliseconds(300))
        controller.show(AnyView(Color.blue), route: .character, paused: false, reduced: false)
        try await Task.sleep(for: .milliseconds(400))
        let outgoing = try XCTUnwrap(controller.view.subviews.last as? UIImageView)
        let transform = try XCTUnwrap(outgoing.layer.presentation()).transform
        print("HINGE PRESENTATION", transform.m11, transform.m13, transform.m14)
        XCTAssertLessThan(transform.m11, 0.99)
        XCTAssertGreaterThan(abs(transform.m13), 0.1)
        XCTAssertEqual(outgoing.layer.anchorPoint.x, 0)
        try await Task.sleep(for: .seconds(1))
        XCTAssertTrue(controller.view.isUserInteractionEnabled)
        XCTAssertFalse(controller.view.subviews.contains { $0 is UIImageView })
    }

    func testPreviewDoesNotChangeEquipment() throws {
        let menu = TennisMenu.shared
        menu.debugShow(.main)
        let original = menu.player
        XCTAssertTrue(menu.focus("character"))
        XCTAssertEqual(menu.screen, .main)
        XCTAssertEqual(menu.player, original)
        let focused = menu.focused
        menu.tap("nonexistent")
        XCTAssertEqual(menu.focused, focused)
        XCTAssertEqual(menu.screen, .main)
        let preview = CharacterModelPreview.Coordinator()
        let player = Player(name: "Review", colorIndex: 0)
        preview.update(player)
        preview.configureClub(.settings, animate: false)
        let root = try XCTUnwrap(preview.character.childNode(withName: MatchHero.rootName, recursively: false), "the settings mirror shows the match hero")
        XCTAssertNil(preview.character.childNode(withName: "heroV4", recursively: true), "the old baked hero is gone from the locker mirror")
        XCTAssertNotNil(root.childNode(withName: "Body", recursively: false))
        XCTAssertNotNil(preview.scene.rootNode.childNode(withName: "clubProps", recursively: false), "the deck chair stays")
        XCTAssertLessThan(abs(root.worldPosition.x), 0.01, "the hero stays on the stage centre, in front of the chair (there is no seated clip)")
        preview.configureClub(.play, animate: false)
        XCTAssertEqual(preview.previous, player)
        XCTAssertEqual(preview.character.childNode(withName: "racket", recursively: true)?.isHidden, false)
    }

    /// Real TV root at its production canvas size. The tour does not start a match or alter saved progress.
    func testRecordMotionClubMenus() async throws { try await recordMenus(hinge: false) }
    func testRecordHingeTransitions() async throws { try await recordMenus(hinge: true) }
    func testRecordLivingBackgrounds() async throws { try await recordMenus(hinge: false, living: true) }
    private func recordMenus(hinge: Bool, living: Bool = false) async throws {
        let out = ProcessInfo.processInfo.environment["MENU_MOTION_DIR"].map { URL(fileURLWithPath: $0) }
            ?? URL(fileURLWithPath: #filePath).deletingLastPathComponent().deletingLastPathComponent().appendingPathComponent("ArtDir/ui/motion-club-radial-v1/runtime")
        try FileManager.default.createDirectory(at: out, withIntermediateDirectories: true)
        let menu = TennisMenu.shared, session = SportsSession.shared
        let oldPlayers = session.players, oldIndex = session.playerIndex, oldMotion = session.reduceMotion, oldOverscan = session.overscan
        var player = Player(name: "Adnan", colorIndex: 0); player.setSkin(0.35); player.hairColor = 3
        session.players = [player]; session.playerIndex = 0; session.reduceMotion = false; session.overscan = 0
        menu.debugShow(.title)
        defer { session.players = oldPlayers; session.playerIndex = oldIndex; session.reduceMotion = oldMotion; session.overscan = oldOverscan; session.loading.cancel(); session.menuPauseVisible = false; menu.debugShow(.title) }
        let scene = try XCTUnwrap(UIApplication.shared.connectedScenes.compactMap { $0 as? UIWindowScene }.first)
        let size = CGSize(width: 1280, height: 720)
        let host = UIHostingController(rootView: TennisTVRoot()); host.safeAreaRegions = []
        let window = UIWindow(windowScene: scene); window.frame = CGRect(origin: .zero, size: size)
        window.rootViewController = host; window.isHidden = false; host.view.frame = window.bounds; host.view.layoutIfNeeded()
        defer { window.isHidden = true }
        try await Task.sleep(for: .seconds(2))
        let url = out.appendingPathComponent(living ? "Motion_Club_Living_Backgrounds.mp4" : hinge ? "Motion_Club_Hinge_Transitions.mp4" : "Motion_Club_Menus_and_Transitions.mp4"); try? FileManager.default.removeItem(at: url)
        let writer = try AVAssetWriter(outputURL: url, fileType: .mp4)
        let input = AVAssetWriterInput(mediaType: .video, outputSettings: [AVVideoCodecKey: AVVideoCodecType.h264, AVVideoWidthKey: 1280, AVVideoHeightKey: 720, AVVideoCompressionPropertiesKey: [AVVideoAverageBitRateKey: 6_000_000]])
        let adapter = AVAssetWriterInputPixelBufferAdaptor(assetWriterInput: input, sourcePixelBufferAttributes: [kCVPixelBufferPixelFormatTypeKey as String: kCVPixelFormatType_32ARGB, kCVPixelBufferWidthKey as String: 1280, kCVPixelBufferHeightKey as String: 720, kCVPixelBufferCGImageCompatibilityKey as String: true, kCVPixelBufferCGBitmapContextCompatibilityKey as String: true])
        writer.add(input); XCTAssertTrue(writer.startWriting()); writer.startSession(atSourceTime: .zero)
        let format = UIGraphicsImageRendererFormat(); format.scale = 1; format.opaque = true
        let renderer = UIGraphicsImageRenderer(size: size, format: format)
        let actions: [(Double, String, () -> Void)] = living ? [
            (0,"live-entrance", {}), (5,"live-home", { menu.tap("start") }),
            (10,"live-loading", { menu.debugShow(.loading, launch: MenuLaunch(mode: .training)); session.loading.begin(now: Date()); session.loading.reach(0.65); session.loading.tick(now: Date()) }),
            (15,"live-locker", { session.loading.cancel(); menu.debugShow(.character) })
        ] : hinge ? [
            (0,"hinge-intro", {}), (2,"hinge-home", { menu.tap("start") }),
            (5,"hinge-locker", { menu.tap("character") }), (8,"hinge-back", { menu.tap("back") }),
            (11,"hinge-settings", { menu.tap("settings") })
        ] : [
            (0,"01-intro", {}), (2,"02-home-play", { menu.tap("start") }),
            (5,"03-home-locker", { menu.focus("character") }), (8,"04-home-settings", { menu.focus("settings") }),
            (11,"05-store-locked", { menu.tap("store") }), (13,"06-sports", { menu.tap("play") }),
            (16,"07-tennis", { menu.tap("sport-tennis") }),
            (19,"08-campaign", { menu.debugShow(.campaign) }),
            (22,"09-quick-match", { menu.debugShow(.exhibition) }),
            (25,"10-courts", { menu.debugShow(.map) }), (28,"11-training", { menu.debugShow(.training) }),
            (31,"12-loading-tennis", { menu.debugShow(.loading, launch: MenuLaunch(mode: .training)); session.loading.begin(now: Date()); session.loading.reach(0.72); session.loading.tick(now: Date()) }),
            (32,"13-practice", { session.loading.practice() }), (34,"14-practice-repeat", { session.loading.practice() }),
            (36,"15-loading-golf", { menu.debugShow(.main) }),
            (37,"16-golf", { menu.debugShow(.hub(.golf)) }),
            (40,"17-golf-loading", { menu.debugShow(.loading, launch: MenuLaunch(sport: .golf, mode: .round)); session.loading.begin(now: Date()); session.loading.reach(0.65); session.loading.tick(now: Date()) }),
            (43,"18-locker", { session.loading.cancel(); menu.debugShow(.character) }),
            (47,"19-settings", { menu.debugShow(.settings) }), (50,"20-guide", { menu.debugShow(.howTo) }),
            (53,"21-golf-lesson", { menu.debugShow(.golfLesson) }), (56,"22-connect", { menu.debugShow(.connect) }),
            (59,"23-results", { menu.debugShow(.results, result: MatchResult(won: true, score: "6–4", round: 0)) }),
            (62,"24-pause", { session.menuPauseVisible = true }),
            (65,"25-home", { session.menuPauseVisible = false; menu.debugShow(.main) })
        ]
        var action = 0, captured = Set<String>()
        let started = CACurrentMediaTime()
        while CACurrentMediaTime() - started < (living ? 20 : hinge ? 14 : 68) {
            let elapsed = CACurrentMediaTime() - started
            if action < actions.count, elapsed >= actions[action].0 { actions[action].2(); action += 1 }
            while !input.isReadyForMoreMediaData { try await Task.sleep(for: .milliseconds(5)) }
            let image = renderer.image { _ in host.view.drawHierarchy(in: CGRect(origin: .zero, size: size), afterScreenUpdates: false) }
            if action > 0 {
                let item = actions[action-1], delay = elapsed - item.0
                let name = delay > 1.1 ? item.1 : delay > 0.24 && delay < 0.48 ? item.1 + "-transition" : ""
                if !name.isEmpty && captured.insert(name).inserted { try image.pngData()?.write(to: out.appendingPathComponent(name + ".png")) }
            }
            var buffer: CVPixelBuffer?
            CVPixelBufferPoolCreatePixelBuffer(nil, try XCTUnwrap(adapter.pixelBufferPool), &buffer)
            let pixel = try XCTUnwrap(buffer); CVPixelBufferLockBaseAddress(pixel, [])
            let context = try XCTUnwrap(CGContext(data: CVPixelBufferGetBaseAddress(pixel), width: 1280, height: 720, bitsPerComponent: 8, bytesPerRow: CVPixelBufferGetBytesPerRow(pixel), space: CGColorSpaceCreateDeviceRGB(), bitmapInfo: CGImageAlphaInfo.noneSkipFirst.rawValue))
            context.draw(try XCTUnwrap(image.cgImage), in: CGRect(origin: .zero, size: size))
            CVPixelBufferUnlockBaseAddress(pixel, [])
            XCTAssertTrue(adapter.append(pixel, withPresentationTime: CMTime(seconds: elapsed, preferredTimescale: 60000)))
            try await Task.sleep(for: .milliseconds(16))
        }
        input.markAsFinished(); await writer.finishWriting(); XCTAssertEqual(writer.status, .completed)
        if hinge { return }
        for screen in [MenuScreen.main, .character, .settings, .loading] {
            menu.debugShow(screen, launch: MenuLaunch(mode: .training))
            let phone = UIHostingController(rootView: TennisPhoneMenu()); phone.safeAreaRegions = []
            window.rootViewController = phone; window.frame = CGRect(x:0,y:0,width:402,height:874); phone.view.frame = window.bounds
            try await Task.sleep(for: .seconds(1))
            let img = UIGraphicsImageRenderer(size: window.bounds.size).image { _ in phone.view.drawHierarchy(in:window.bounds,afterScreenUpdates:true) }
            try img.pngData()?.write(to:out.appendingPathComponent("phone-\(screen).png"))
        }
    }
    func testSmoothTVMenuTour() async throws { try await smoothTour(short: false) }
    func testSmoothHingeAndVictory() async throws { try await smoothTour(short: true) }
    private func smoothTour(short: Bool) async throws {
        let out = URL(fileURLWithPath: #filePath).deletingLastPathComponent().deletingLastPathComponent().appendingPathComponent("ArtDir/ui/motion-club-radial-v1/runtime")
        try FileManager.default.createDirectory(at: out, withIntermediateDirectories: true)
        let menu = TennisMenu.shared, session = SportsSession.shared
        let oldPlayers = session.players, oldIndex = session.playerIndex, oldMotion = session.reduceMotion, oldOverscan = session.overscan
        var player = Player(name: "Adnan", colorIndex: 0); player.setSkin(0.35); player.hairColor = 3
        session.players = [player]; session.playerIndex = 0; session.reduceMotion = false; session.overscan = 0
        menu.debugShow(.title)
        defer { session.players = oldPlayers; session.playerIndex = oldIndex; session.reduceMotion = oldMotion; session.overscan = oldOverscan; session.loading.cancel(); session.menuPauseVisible = false; menu.debugShow(.title) }
        let scene = try XCTUnwrap(UIApplication.shared.connectedScenes.compactMap { $0 as? UIWindowScene }.first)
        let size = scene.coordinateSpace.bounds.size
        let host = UIHostingController(rootView: TennisTVRoot()); host.safeAreaRegions = []
        let window = UIWindow(windowScene: scene); window.frame = CGRect(origin: .zero, size: size)
        window.rootViewController = host; window.isHidden = false; host.view.frame = window.bounds; host.view.layoutIfNeeded()
        defer { window.isHidden = true }
        try await Task.sleep(for: .seconds(2))
        let stats = MatchStats(line: "won=1;pointsWon=31;pointsLost=17;aces=3;winners=6;longest=14;perfect=9;great=12;setsWon=2;setsLost=1;gamesWon=6;gamesLost=4;hits=88;seconds=420")
        let review = PostMatchSummary(won: true, score: "6–4 3–6 7–5", opponent: "Kai", stats: stats, lines: [], total: 419, startLevel: 2, startXP: 250, endLevel: 4, endXP: 59)
        let actions: [(Double, String, () -> Void)] = short ? [
            (0,"intro", {}), (2,"home", { menu.tap("start") }),
            (5,"locker", { menu.tap("character") }), (8,"home", { menu.tap("back") }),
            (11,"victory", { menu.debugPostMatch(review, launch: MenuLaunch(mode: .exhibition, round: 0)) })
        ] : [
            (0,"01-intro", {}), (2,"02-home-play", { menu.tap("start") }),
            (5,"03-home-locker", { menu.focus("character") }), (8,"04-home-settings", { menu.focus("settings") }),
            (11,"05-store-locked", { menu.tap("store") }), (13,"06-sports", { menu.tap("play") }),
            (16,"07-tennis", { menu.tap("sport-tennis") }),
            (19,"08-campaign", { menu.debugShow(.campaign) }),
            (22,"09-quick-match", { menu.debugShow(.exhibition) }),
            (25,"10-courts", { menu.debugShow(.map) }), (28,"11-training", { menu.debugShow(.training) }),
            (31,"12-loading-tennis", { menu.debugShow(.loading, launch: MenuLaunch(mode: .training)); session.loading.begin(now: Date()); session.loading.reach(0.72); session.loading.tick(now: Date()) }),
            (32,"13-practice", { session.loading.practice() }), (34,"14-practice-repeat", { session.loading.practice() }),
            (36,"15-loading-golf", { menu.debugShow(.main) }),
            (37,"16-golf", { menu.debugShow(.hub(.golf)) }),
            (40,"17-golf-loading", { menu.debugShow(.loading, launch: MenuLaunch(sport: .golf, mode: .round)); session.loading.begin(now: Date()); session.loading.reach(0.65); session.loading.tick(now: Date()) }),
            (43,"18-locker", { session.loading.cancel(); menu.debugShow(.character) }),
            (47,"19-settings", { menu.debugShow(.settings) }), (50,"20-guide", { menu.debugShow(.howTo) }),
            (53,"21-golf-lesson", { menu.debugShow(.golfLesson) }), (56,"22-connect", { menu.debugShow(.connect) }),
            (59,"23-results", { menu.debugShow(.results, result: MatchResult(won: true, score: "6–4", round: 0)) }),
            (62,"24-pause", { session.menuPauseVisible = true }),
            (65,"25-home", { session.menuPauseVisible = false; menu.debugShow(.main) })
        ]
        var action = 0
        try String(Date().timeIntervalSince1970).write(to: out.appendingPathComponent("tour-start.txt"), atomically: true, encoding: .utf8)
        let started = CACurrentMediaTime()
        while CACurrentMediaTime() - started < (short ? 17 : 68) {
            let elapsed = CACurrentMediaTime() - started
            if action < actions.count, elapsed >= actions[action].0 { actions[action].2(); action += 1 }
            try await Task.sleep(for: .milliseconds(16))
        }
        try String(Date().timeIntervalSince1970).write(to: out.appendingPathComponent("tour-end.txt"), atomically: true, encoding: .utf8)
    }

}

@MainActor final class GolfReadyControllerTests: XCTestCase {
    func testSwitchingToTouchCancelsPracticeState() {
        let session = SportsSession(); session.sport = "golf"
        session.golfCalibrating = true; session.golfCalibrationCount = 2; session.golfShotReady = true
        session.useTouch()
        XCTAssertFalse(session.golfCalibrating)
        XCTAssertFalse(session.golfShotReady)
        XCTAssertEqual(session.golfCalibrationCount, 0)
        XCTAssertTrue(session.golfCalibrationRequired, "Motion setup must still be offered when switching back")
    }
    func testPracticeCannotBeAcceptedBeforeThreeSwings() {
        let session = SportsSession(); session.sport = "golf"
        session.golfCalibrationRequired = true; session.golfCalibrating = true
        session.golfCalibrationCount = 2
        session.finishGolfCalibration()
        XCTAssertTrue(session.golfCalibrationRequired)
        session.golfCalibrationCount = 3
        session.finishGolfCalibration()
        XCTAssertFalse(session.golfCalibrationRequired)
        XCTAssertFalse(session.golfCalibrating)
        XCTAssertEqual(session.setupStage, .ready)
    }
    func testCaptureGolfReadyAndPracticeScreens() async throws {
        let session = SportsSession(); session.sport = "golf"; session.ready = true
        session.loading.cancel(); session.golfPhase = "Aim"; session.paused = false
        let out = URL(fileURLWithPath: #filePath).deletingLastPathComponent().deletingLastPathComponent()
            .appendingPathComponent("work/golf-motion-fix/proof")
        try FileManager.default.createDirectory(at: out, withIntermediateDirectories: true)
        for stage in 0..<4 {
            session.golfCalibrationRequired = stage < 2
            session.golfCalibrating = stage == 1
            session.golfCalibrationCount = stage == 1 ? 1 : 0
            session.golfShotReady = stage == 3
            let scene = try XCTUnwrap(UIApplication.shared.connectedScenes.compactMap { $0 as? UIWindowScene }.first)
            let host = UIHostingController(rootView: GolfPhoneController(session: session)); host.safeAreaRegions = []
            let window = UIWindow(windowScene: scene); window.frame = CGRect(x: 0, y: 0, width: 393, height: 800)
            window.rootViewController = host; window.isHidden = false; host.view.frame = window.bounds
            host.view.layoutIfNeeded()
            try await Task.sleep(for: .milliseconds(150))
            let format = UIGraphicsImageRendererFormat(); format.scale = 1
            let image = UIGraphicsImageRenderer(size: window.bounds.size, format: format).image { _ in
                window.drawHierarchy(in: window.bounds, afterScreenUpdates: true)
            }
            window.isHidden = true
            try XCTUnwrap(image.pngData()).write(to: out.appendingPathComponent("controller-\(stage).png"))
            XCTAssertEqual(image.size.width,393)
        }
    }
}
