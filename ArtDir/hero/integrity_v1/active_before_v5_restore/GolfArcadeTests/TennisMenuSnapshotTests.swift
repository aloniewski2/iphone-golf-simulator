import SwiftUI
import SceneKit
import AVFoundation
import XCTest
@testable import GolfArcade

/// Renders every tennis front-end screen to PNG (TV at 1280×720, phone at 402×874) so the
/// layouts can be reviewed without a TV attached. Output: $TMPDIR/tennis-menu/.
@MainActor
final class TennisMenuSnapshotTests: XCTestCase {
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
                let root = try XCTUnwrap(scn.scene?.rootNode.childNode(withName: "heroV4", recursively: true))
                var animated = 0
                root.enumerateChildNodes { node, _ in
                    if let morph = node.morpher, morph.weights.contains(where: { $0.doubleValue > 0.01 }) { animated += 1 }
                }
                XCTAssertGreaterThan(animated, 5, "The swing must animate the character, not just increment its trigger.")
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
            ("quick-play-tv", CGSize(width: 1280, height: 720), false, .quickPlay, nil),
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
        XCTAssertEqual(preview.character.childNode(withName: "racket", recursively: false)?.isHidden, true)
        XCTAssertTrue(preview.character.hasActions)
        preview.configureIdle(sport: .tennis, animate: false)
        XCTAssertNil(preview.character.childNode(withName: "idleClub", recursively: false))
        XCTAssertFalse(preview.character.hasActions)
        XCTAssertEqual(preview.character.childNode(withName: "racket", recursively: false)?.isHidden, false)
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
            XCTAssertGreaterThan(coordinator.character.childNodes.count, 1)
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

    func testHeroIntegrityCustomizationAndSize() throws {
        func vertices(_ geometry: SCNGeometry) throws -> [Float] {
            let src = try XCTUnwrap(geometry.sources(for: .vertex).first)
            return src.data.withUnsafeBytes { bytes in
                (0..<src.vectorCount * 3).map { bytes.loadUnaligned(fromByteOffset: src.dataOffset + ($0 / 3) * src.dataStride + ($0 % 3) * 4, as: Float.self) }
            }
        }
        XCTAssertTrue(HeroV4.load())
        for female in [false, true] {
            for cut in 0..<HeroV4.offered {
                for hat in 0..<4 {
                    var p = Player(name: "Integrity", colorIndex: 0); p.standardFemale = female; p.haircut = cut; p.hairStyle = hat
                    p.setSkin(female ? 0.9 : 0.2); p.setHair(natural: cut.isMultiple(of: 2) ? 0.1 : 0.8)
                    let hero = try XCTUnwrap(HeroV4.build(p))
                    let hairs = hero.childNodes(passingTest: { node, _ in (node.name ?? "").hasPrefix("Hair_") && !(node.name ?? "").hasPrefix("Hair_BandFill") })
                    XCTAssertEqual(hairs.count, 1, "one cut for body=\(female) cut=\(cut) hat=\(hat)")
                    XCTAssertNotNil(hero.childNode(withName: "Body_NeckSeal", recursively: true))
                    XCTAssertTrue(try vertices(try XCTUnwrap(hairs.first?.geometry)).allSatisfy(\.isFinite))
                }
            }
            var p = Player(name: "Build", colorIndex: 0); p.standardFemale = female; p.bodySize = 0
            let slim = try XCTUnwrap(HeroV4.build(p)); p.bodySize = 1; let broad = try XCTUnwrap(HeroV4.build(p))
            for name in ["Body_Skin", "Shirt_Default", "Shorts_Default"] {
                let a = try vertices(try XCTUnwrap(slim.childNode(withName: name, recursively: true)?.geometry))
                let b = try vertices(try XCTUnwrap(broad.childNode(withName: name, recursively: true)?.geometry))
                XCTAssertEqual(a.count, b.count); XCTAssertTrue(b.allSatisfy(\.isFinite))
                XCTAssertGreaterThan(zip(a,b).reduce(Float(0)) { $0 + abs($1.0-$1.1) }, 0.1, "Size changes \(name)")
            }
            HeroV4.preparePractice(slim); HeroV4.preparePractice(broad)
            let a = try vertices(try XCTUnwrap(slim.childNode(withName: "Body_Skin", recursively: true)?.morpher?.targets.first))
            let b = try vertices(try XCTUnwrap(broad.childNode(withName: "Body_Skin", recursively: true)?.morpher?.targets.first))
            XCTAssertGreaterThan(zip(a,b).reduce(Float(0)) { $0 + abs($1.0-$1.1) }, 0.1, "Loading swing retains build")
            HeroV4.applyLounge(slim); HeroV4.applyLounge(broad)
            let seated = try vertices(try XCTUnwrap(slim.childNode(withName: "Body_Skin", recursively: true)?.morpher?.targets.first))
            XCTAssertTrue(seated.allSatisfy(\.isFinite))
        }
    }

    func testHeroIntegrityPhonePreview() throws {
        let out = URL(fileURLWithPath: #filePath).deletingLastPathComponent().deletingLastPathComponent().appendingPathComponent("ArtDir/hero/integrity_v1/native")
        try FileManager.default.createDirectory(at: out, withIntermediateDirectories: true)
        for (label, cut, hat, female, size) in [("default",0,1,false,0.5),("nohat",0,0,false,0.5),("ponytail",1,1,true,0.5),("bob",2,0,true,0.5),("slim",0,1,false,0.0),("broad",0,1,false,1.0)] {
            var p = Player(name: "Review", colorIndex: 0); p.haircut = cut; p.hairStyle = hat; p.standardFemale = female; p.bodySize = size; p.setSkin(0.35)
            let c = CharacterModelPreview.Coordinator(); c.update(p)
            let renderer = SCNRenderer(device: nil, options: nil); renderer.scene = c.scene; renderer.pointOfView = c.scene.rootNode.childNodes.first { $0.camera != nil }
            let image = renderer.snapshot(atTime: 0, with: CGSize(width: 720, height: 1000), antialiasingMode: .multisampling4X)
            try XCTUnwrap(image.pngData()).write(to: out.appendingPathComponent(label+".png"))
        }
    }

    /// Hero V5 proof: the locker mirror (the phone's customize screen renders exactly this SceneKit scene) in the
    /// framings the plan gates on -- head close-ups front / 3-4 / side / back / top, low front, and a full-body
    /// turn -- for no hat, visor, cap and sweatband, on a flat magenta backdrop so any see-through hair shows.
    /// Written next to the repo: ArtDir/hero/v5_proof/locker/.
    func testHeroV5LockerProof() throws {
        let out = URL(fileURLWithPath: #filePath).deletingLastPathComponent().deletingLastPathComponent()
            .appendingPathComponent("ArtDir/hero/v5_proof/locker")
        try FileManager.default.createDirectory(at: out, withIntermediateDirectories: true)
        for (hat, name) in [(0, "nohat"), (1, "visor"), (2, "cap"), (3, "sweatband")] {
            var p = Player(name: "V5", colorIndex: 0); p.hairStyle = hat
            let c = CharacterModelPreview.Coordinator(); c.update(p)
            c.scene.background.contents = UIColor(red: 1, green: 0, blue: 1, alpha: 1)
            c.scene.rootNode.childNode(withName: "idleBall", recursively: true)?.removeFromParentNode()
            let hair = try XCTUnwrap(c.character.childNode(withName: hat == 0 ? "Hair_Default_Free" : "Hair_Default", recursively: true), "hair node")
            let (mn, mx) = hair.boundingBox
            let lo = hair.convertPosition(mn, to: nil), hi = hair.convertPosition(mx, to: nil)
            let head = SCNVector3((lo.x + hi.x) / 2, (lo.y + hi.y) / 2 - 0.02, (lo.z + hi.z) / 2)
            let size = max(abs(hi.x - lo.x), abs(hi.y - lo.y))
            let cam = SCNNode(); cam.camera = SCNCamera(); cam.camera?.fieldOfView = 30; c.scene.rootNode.addChildNode(cam)
            let renderer = SCNRenderer(device: nil, options: nil); renderer.scene = c.scene; renderer.pointOfView = cam
            func shot(_ label: String, yaw: Float, pitch: Float, dist: Float, target: SCNVector3, size px: CGSize) throws {
                cam.position = SCNVector3(target.x + dist * sin(yaw) * cos(pitch), target.y + dist * sin(pitch), target.z + dist * cos(yaw) * cos(pitch))
                cam.look(at: target)
                let img = renderer.snapshot(atTime: 0, with: px, antialiasingMode: .multisampling4X)
                try XCTUnwrap(img.pngData()).write(to: out.appendingPathComponent("\(name)_\(label).png"))
            }
            let d = Float(size) * 3.2
            for (label, yaw, pitch) in [("front", Float(0), Float(0.05)), ("34", Float(0.6), Float(0.08)), ("side", Float(1.57), Float(0.05)),
                                        ("back", Float(3.14), Float(0.1)), ("top", Float(2.6), Float(1.0)), ("lowfront", Float(0.2), Float(-0.35))] {
                try shot(label, yaw: yaw, pitch: pitch, dist: d, target: head, size: CGSize(width: 700, height: 700))
            }
            let body = SCNVector3(head.x, head.y * 0.52, head.z)
            for (label, yaw) in [("turn_front", Float(0)), ("turn_34", Float(0.6)), ("turn_side", Float(1.57)), ("turn_back", Float(3.14))] {
                try shot(label, yaw: yaw, pitch: 0.06, dist: Float(head.y) * 3.6, target: body, size: CGSize(width: 500, height: 900))
            }
        }
    }

    /// Hero V5 haircuts x boy/girl in the locker light (ArtDir/hero/v5_proof/styles/<cut>_<body>_<hat>_<view>.png).
    func testHeroV5StylesProof() throws {
        let out = URL(fileURLWithPath: #filePath).deletingLastPathComponent().deletingLastPathComponent()
            .appendingPathComponent("ArtDir/hero/v5_proof/styles")
        try FileManager.default.createDirectory(at: out, withIntermediateDirectories: true)
        let combos: [(Int, Bool)] = [(0, false), (1, true), (2, true), (3, true), (4, true), (4, false), (0, true)]
        for (cut, girl) in combos {
            for hat in [1, 0] {
                var p = Player(name: "V5", colorIndex: 0); p.standardFemale = girl; p.haircut = cut; p.hairStyle = hat
                let c = CharacterModelPreview.Coordinator(); c.update(p)
                c.scene.background.contents = UIColor(red: 0.93, green: 0.62, blue: 0.48, alpha: 1)   // the locker's warm coral wall
                c.scene.rootNode.childNode(withName: "idleBall", recursively: true)?.removeFromParentNode()
                let hairName = (cut == 0 ? "Hair_Default" : "Hair_" + HeroV4.haircuts[cut]) + (hat == 0 ? "_Free" : "")   // no hat: the un-pressed sculpt
                let hair = try XCTUnwrap(c.character.childNode(withName: hairName, recursively: true), "hair node \(hairName)")
                let cuts = c.character.childNodes(passingTest: { n, _ in (n.name ?? "").hasPrefix("Hair_") && !(n.name ?? "").hasPrefix("Hair_BandFill") })
                XCTAssertEqual(cuts.map { $0.name ?? "" }, [hairName], "exactly one haircut")
                let (mn, mx) = hair.boundingBox
                let lo = hair.convertPosition(mn, to: nil), hi = hair.convertPosition(mx, to: nil)
                let head = SCNVector3((lo.x + hi.x) / 2, max(lo.y, hi.y) - 0.2, (lo.z + hi.z) / 2)
                let cam = SCNNode(); cam.camera = SCNCamera(); cam.camera?.fieldOfView = 30; c.scene.rootNode.addChildNode(cam)
                let renderer = SCNRenderer(device: nil, options: nil); renderer.scene = c.scene; renderer.pointOfView = cam
                let tag = "\(HeroV4.haircuts[cut])_\(girl ? "girl" : "boy")_\(hat == 0 ? "nohat" : "visor")"
                func shot(_ label: String, yaw: Float, pitch: Float, dist: Float, target: SCNVector3, size px: CGSize) throws {
                    cam.position = SCNVector3(target.x + dist * sin(yaw) * cos(pitch), target.y + dist * sin(pitch), target.z + dist * cos(yaw) * cos(pitch))
                    cam.look(at: target)
                    let img = renderer.snapshot(atTime: 0, with: px, antialiasingMode: .multisampling4X)
                    try XCTUnwrap(img.pngData()).write(to: out.appendingPathComponent("\(tag)_\(label).png"))
                }
                for (label, yaw) in [("34", Float(0.5)), ("back", Float(2.8))] {
                    try shot(label, yaw: yaw, pitch: 0.08, dist: 1.5, target: head, size: CGSize(width: 600, height: 600))
                }
                if hat == 1 { try shot("turn", yaw: 0.45, pitch: 0.06, dist: 4.2, target: SCNVector3(head.x, head.y * 0.55, head.z), size: CGSize(width: 500, height: 900)) }
            }
        }
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
            try await saveHosted(ClubCharacterScreen(menu:menu,compact:true),"new-locker-\(female)",CGSize(width:402,height:874))
            try await saveHosted(ClubCharacterScreen(menu:menu,compact:false),"new-locker-tv-\(female)",CGSize(width:1280,height:720))
        }
    }

    /// Every haircut x headwear on dark skin + black hair, side / 3/4 / back head shots (hair-fit audit).
    func testHairFitAudit() throws {
        let out = URL(fileURLWithPath: #filePath).deletingLastPathComponent().deletingLastPathComponent()
            .appendingPathComponent("ArtDir/hero/v5_proof/hairfit")
        try FileManager.default.createDirectory(at: out, withIntermediateDirectories: true)
        for cut in 0..<HeroV4.haircuts.count {
            for hat in 0..<4 {
                var p = Player(name: "Fit", colorIndex: 0); p.standardFemale = cut != 0; p.haircut = cut; p.hairStyle = hat
                p.setSkin(0.95); p.setHair(natural: 0.0)
                let c = CharacterModelPreview.Coordinator(); c.framing = .head; c.update(p)
                c.scene.background.contents = UIColor(red: 0.93, green: 0.62, blue: 0.48, alpha: 1)
                c.character.childNode(withName: "racket", recursively: true)?.isHidden = true
                let cam = try XCTUnwrap(c.scene.rootNode.childNodes.first { $0.camera != nil })
                let r = SCNRenderer(device: nil, options: nil); r.scene = c.scene; r.pointOfView = cam
                for (label, yaw) in [("34", Float(0.6)), ("side", Float(1.57)), ("back", Float(2.9))] {
                    cam.position = SCNVector3(1.3 * sin(yaw), 1.14, 1.3 * cos(yaw)); cam.look(at: SCNVector3(0, 1.08, 0))
                    let img = r.snapshot(atTime: 0, with: CGSize(width: 360, height: 360), antialiasingMode: .multisampling4X)
                    try XCTUnwrap(img.pngData()).write(to: out.appendingPathComponent("\(HeroV4.haircuts[cut])_\(HeroV4.headwear[hat])_\(label).png"))
                }
            }
        }
    }

    func testLockerStudioTabs() async throws {
        let session = SportsSession.shared, saved = session.players, index = session.playerIndex
        defer { session.players = saved; session.playerIndex = index }
        var p = Player(name: "Maya", colorIndex: 0); p.standardFemale = true; p.haircut = 1; p.hairStyle = 1
        p.setSkin(0.45); p.setHair(natural: 0.7); p.setOutfit("shirt", hue: 0.93, shade: 0.1); p.setOutfit("shorts", hue: 0.62, shade: -0.5)
        session.players = [p]; session.playerIndex = 0
        let menu = TennisMenu.shared; menu.debugShow(.character)
        for tab in LockerStudio.Tab.allCases {
            try await saveHosted(ClubScreen(scene: "locker", breadcrumb: ["Clubhouse", "Your look"], compact: true, tint: 0.35) { LockerStudio(menu: menu, startTab: tab) },
                                 "locker-studio-\(tab.rawValue)", CGSize(width: 402, height: 874))
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
