import SwiftUI
import SceneKit
import AVFoundation
import XCTest
import simd
@testable import GolfArcade

/// LOCKER_MIRROR proof: stills of the locker's close-ups (male torso, female skort, one shoe, the face), the before look next to the new look, and short recordings of the locker tile and the play
/// tile. Everything goes to work/locker-mirror/proof/. These are recorders, not gates: the gate lines are LockerMirrorTests.
@MainActor
final class LockerMirrorProofTests: XCTestCase {
    private var repo: URL { URL(fileURLWithPath: #filePath).deletingLastPathComponent().deletingLastPathComponent() }
    private var proof: URL {
        let url = repo.appendingPathComponent("work/locker-mirror/proof")
        try? FileManager.default.createDirectory(at: url, withIntermediateDirectories: true)
        return url
    }

    private func player(female: Bool, picks: Bool = false) -> Player {
        var p = Player(name: female ? "Maya" : "Alex", colorIndex: 0); p.standardFemale = female; p.setSkin(female ? 0.2 : 0.45)
        if picks { p.shirt = 5; p.shorts = 2; p.accent = 3 }
        return p
    }
    private func snapshot(_ c: CharacterModelPreview.Coordinator, size: CGSize) -> UIImage {
        let renderer = SCNRenderer(device: nil, options: nil); renderer.scene = c.scene; renderer.pointOfView = c.camera
        return renderer.snapshot(atTime: 0, with: size, antialiasingMode: .multisampling4X)
    }
    /// A frame of the posed hero. The simulator's OFFSCREEN renderer freezes skinned meshes at their first pose when it is asked for multisampled snapshots (the real SCNView does not: it is
    /// checked in LockerMirrorTests), so a posed frame is rendered at 2x without multisampling and scaled down.
    private func posedFrame(_ c: CharacterModelPreview.Coordinator, size: CGSize) -> UIImage {
        let renderer = SCNRenderer(device: nil, options: nil); renderer.scene = c.scene; renderer.pointOfView = c.camera
        let big = renderer.snapshot(atTime: 0, with: CGSize(width: size.width * 2, height: size.height * 2), antialiasingMode: .none)
        let format = UIGraphicsImageRendererFormat(); format.scale = 1; format.opaque = true
        return UIGraphicsImageRenderer(size: size, format: format).image { ctx in ctx.cgContext.interpolationQuality = .high; big.draw(in: CGRect(origin: .zero, size: size)) }
    }
    private func save(_ image: UIImage, _ name: String) throws { try XCTUnwrap(image.pngData()).write(to: proof.appendingPathComponent(name)) }

    /// The look before LOCKER_MIRROR: every kit piece a flat Blinn colour (white upper, white trim, white sole), skin at the Lit asset's smoothness 0.4 - what the mirror drew.
    private func legacyLook(_ hero: SCNNode) {
        hero.enumerateChildNodes { node, _ in
            for m in node.geometry?.materials ?? [] {
                guard let made = MatchHeroSurfaces.made(m) else { continue }
                var c = made.colour
                if made.role == "Kit_ShirtTrim" || made.role == "Kit_Sole" { c = SIMD3(repeating: 0.93) }
                let smooth: Float = made.role == "Skin" ? 0.4 : made.role.hasPrefix("Kit_") ? 0.3 : (made.look?.smoothness ?? 0.3)
                m.shaderModifiers = nil; m.lightingModel = .blinn
                m.diffuse.contents = HeroColor(red: CGFloat(c.x), green: CGFloat(c.y), blue: CGFloat(c.z), alpha: 1)
                let b = MatchHeroSurfaces.blinn(smooth)
                m.specular.contents = HeroColor(white: b.specular, alpha: 1); m.shininess = b.shininess
            }
        }
    }

    private enum Shot { case torso, skort, shoe, face, body }
    private func camera(_ c: CharacterModelPreview.Coordinator, _ shot: Shot) {
        guard let cam = c.camera else { return }
        let head = c.headTarget.y
        switch shot {
        case .torso: cam.position = SCNVector3(0.35, head - 0.40 + 0.04, 1.35); cam.look(at: SCNVector3(0, head - 0.40, 0))
        case .skort: cam.position = SCNVector3(0.40, 0.62, 1.55); cam.look(at: SCNVector3(0, 0.55, 0))
        case .shoe: cam.position = SCNVector3(0.30, 0.34, 0.95); cam.look(at: SCNVector3(0, 0.10, 0))
        case .face: cam.position = SCNVector3(0.42, head + 0.05, 0.95); cam.look(at: SCNVector3(0, head - 0.01, 0))
        case .body: cam.position = SCNVector3(0, 0.9, c.heroCameraDistance); cam.look(at: SCNVector3(0, 0.76, 0))
        }
    }

    /// Male torso, female skort, one shoe and the face, as the locker's stage draws them (the same scene, lights and hero), and the same shots with the look from before this job.
    func testRecordLockerCloseUps() throws {
        let size = CGSize(width: 900, height: 900)
        let shots: [(String, Bool, Shot)] = [("male_torso", false, .torso), ("female_skort", true, .skort), ("male_shoe", false, .shoe), ("female_shoe", true, .shoe), ("male_face", false, .face), ("female_face", true, .face),
                                              ("male_body", false, .body), ("female_body", true, .body)]
        for (name, female, shot) in shots {
            for legacy in [false, true] {
                let c = CharacterModelPreview.Coordinator(cameraDistance: 3.3); c.update(player(female: female))
                c.scene.background.contents = UIColor(red: 0.93, green: 0.62, blue: 0.48, alpha: 1)
                camera(c, shot)
                if legacy, let hero = c.hero { legacyLook(hero) }
                try save(snapshot(c, size: shot == .body ? CGSize(width: 720, height: 1000) : size), "\(name)\(legacy ? "_BEFORE" : "").png")
            }
        }
        // the picks, and the kit close-ups with the weave and thread switched off (what the maps add)
        for (name, female, shot) in [("male_torso_picked", false, Shot.torso), ("female_skort_picked", true, .skort)] {
            let c = CharacterModelPreview.Coordinator(cameraDistance: 3.3); c.update(player(female: female, picks: true))
            c.scene.background.contents = UIColor(red: 0.93, green: 0.62, blue: 0.48, alpha: 1); camera(c, shot)
            try save(snapshot(c, size: size), "\(name).png")
        }
    }

    // MARK: films

    private final class Film {
        let writer: AVAssetWriter, input: AVAssetWriterInput, adapter: AVAssetWriterInputPixelBufferAdaptor, size: CGSize
        init(url: URL, size: CGSize, bitrate: Int = 6_000_000) throws {
            try? FileManager.default.removeItem(at: url)
            self.size = size
            writer = try AVAssetWriter(outputURL: url, fileType: .mp4)
            input = AVAssetWriterInput(mediaType: .video, outputSettings: [AVVideoCodecKey: AVVideoCodecType.h264, AVVideoWidthKey: Int(size.width), AVVideoHeightKey: Int(size.height),
                                                                           AVVideoCompressionPropertiesKey: [AVVideoAverageBitRateKey: bitrate]])
            adapter = AVAssetWriterInputPixelBufferAdaptor(assetWriterInput: input, sourcePixelBufferAttributes: [kCVPixelBufferPixelFormatTypeKey as String: kCVPixelFormatType_32ARGB,
                kCVPixelBufferWidthKey as String: Int(size.width), kCVPixelBufferHeightKey as String: Int(size.height), kCVPixelBufferCGImageCompatibilityKey as String: true, kCVPixelBufferCGBitmapContextCompatibilityKey as String: true])
            writer.add(input); writer.startWriting(); writer.startSession(atSourceTime: .zero)
        }
        func append(_ image: UIImage, at seconds: Double) async throws {
            while !input.isReadyForMoreMediaData { try await Task.sleep(for: .milliseconds(2)) }
            var buffer: CVPixelBuffer?
            CVPixelBufferPoolCreatePixelBuffer(nil, try XCTUnwrap(adapter.pixelBufferPool), &buffer)
            let pixel = try XCTUnwrap(buffer); CVPixelBufferLockBaseAddress(pixel, [])
            let context = try XCTUnwrap(CGContext(data: CVPixelBufferGetBaseAddress(pixel), width: Int(size.width), height: Int(size.height), bitsPerComponent: 8, bytesPerRow: CVPixelBufferGetBytesPerRow(pixel),
                                                  space: CGColorSpaceCreateDeviceRGB(), bitmapInfo: CGImageAlphaInfo.noneSkipFirst.rawValue))
            context.draw(try XCTUnwrap(image.cgImage), in: CGRect(origin: .zero, size: size))
            CVPixelBufferUnlockBaseAddress(pixel, [])
            XCTAssertTrue(adapter.append(pixel, withPresentationTime: CMTime(seconds: seconds, preferredTimescale: 60000)))
        }
        func finish() async { input.markAsFinished(); await writer.finishWriting() }
    }

    /// A caption on a frame: what the tile is playing and the numbers that show the body moves while the root does not.
    private func caption(_ image: UIImage, _ lines: [String]) -> UIImage {
        let format = UIGraphicsImageRendererFormat(); format.scale = 1; format.opaque = true
        return UIGraphicsImageRenderer(size: image.size, format: format).image { ctx in
            image.draw(at: .zero)
            let attrs: [NSAttributedString.Key: Any] = [.font: UIFont.monospacedSystemFont(ofSize: max(13, image.size.height / 46), weight: .medium), .foregroundColor: UIColor.white]
            let h = CGFloat(lines.count) * max(13, image.size.height / 46) * 1.35 + 14
            UIColor.black.withAlphaComponent(0.55).setFill(); ctx.fill(CGRect(x: 0, y: image.size.height - h, width: image.size.width, height: h))
            for (i, line) in lines.enumerated() { (line as NSString).draw(at: CGPoint(x: 10, y: image.size.height - h + 7 + CGFloat(i) * max(13, image.size.height / 46) * 1.35), withAttributes: attrs) }
        }
    }

    /// The locker tile (ReadyIdle) and the play tile (one Serve, then Ready) of the club menu, frame by frame at the clips' own times: the scene, camera and lights the tiles use, drawn by a real
    /// multisampled SCNView set up like the one `CharacterModelPreview` makes (the simulator's OFFSCREEN renderer freezes skinned meshes after a pose change, the real view does not). Each frame is
    /// captioned with the clip, the time, the hero root's yaw (never changes) and the hips' height / the racket hand's travel (do). 30 fps.
    func testRecordLockerAndPlayTileFilms() async throws {
        let windowScene = try XCTUnwrap(UIApplication.shared.connectedScenes.compactMap { $0 as? UIWindowScene }.first)
        for female in [false, true] {
            let sex = female ? "female" : "male"
            for (tile, activity) in [("locker_tile", ClubPreviewActivity.locker), ("play_tile", .play)] {
                let c = CharacterModelPreview.Coordinator(cameraDistance: 3.25); c.update(player(female: female))
                c.configureClub(activity, animate: true); c.pauseDisplayLink()
                c.scene.background.contents = UIColor(red: 0.93, green: 0.62, blue: 0.48, alpha: 1)
                let size = CGSize(width: 540, height: 720)
                let view = SCNView(frame: CGRect(origin: .zero, size: size))
                view.scene = c.scene; view.pointOfView = c.camera; view.antialiasingMode = .multisampling4X; view.autoenablesDefaultLighting = false
                view.preferredFramesPerSecond = 60; view.isPlaying = true; view.contentScaleFactor = 2
                let window = UIWindow(windowScene: windowScene); window.frame = CGRect(origin: .zero, size: size); window.addSubview(view); window.isHidden = false
                defer { window.isHidden = true; c.stopMotion() }
                let rig = try XCTUnwrap(c.rig), hero = try XCTUnwrap(c.hero), motion = try XCTUnwrap(c.motion)
                let hips = try XCTUnwrap(rig.boneNodes.first { $0.name == "Hips" }), hand = try XCTUnwrap(rig.boneNodes.first { $0.name == "RightHand" })
                c.pose(at: 0); let y0 = hips.simdPosition.y, h0 = hand.simdPosition
                try await Task.sleep(for: .milliseconds(300))
                let duration = activity == .play ? motion.settledAfter(rig.data) + 2.4 : 6.6
                let film = try Film(url: proof.appendingPathComponent("\(tile)_\(sex).mp4"), size: size, bitrate: 5_000_000)
                var t = 0.0, frame = 0
                while t <= duration {
                    c.pose(at: t)
                    try await Task.sleep(for: .milliseconds(28))   // let the view draw this pose
                    let format = UIGraphicsImageRendererFormat(); format.scale = 1; format.opaque = true
                    let shot = view.snapshot()
                    let img = UIGraphicsImageRenderer(size: size, format: format).image { ctx in ctx.cgContext.interpolationQuality = .high; shot.draw(in: CGRect(origin: .zero, size: size)) }
                    let phase = activity == .play ? (t < motion.lead ? "Ready" : t <= motion.settledAfter(rig.data) ? "Serve" : "Ready") : "ReadyIdle loop"
                    try await film.append(caption(img, [String(format: "%@ %@  t=%.2fs  playing: %@", sex, tile, t, phase),
                                                         String(format: "hero root yaw %.3f deg (fixed)   character yaw %.3f deg", Double(hero.eulerAngles.y) * 180 / .pi, Double(c.character.eulerAngles.y) * 180 / .pi),
                                                         String(format: "hips height %+.0f mm   racket hand moved %.0f mm", Double(hips.simdPosition.y - y0) * 1000, Double(simd_distance(hand.simdPosition, h0)) * 1000)]), at: t)
                    frame += 1; t = Double(frame) / 30
                }
                await film.finish(); XCTAssertEqual(film.writer.status, .completed)
            }
        }
    }

    /// The real SwiftUI club Home (TV layout, 1280x720, the production canvas) recorded in real time while focus moves Play -> Locker -> Settings -> Play: the tiles as the app draws them.
    func testRecordRealClubHomeFocusTour() async throws {
        let menu = TennisMenu.shared, session = SportsSession.shared
        let oldPlayers = session.players, oldIndex = session.playerIndex, oldMotion = session.reduceMotion, oldOverscan = session.overscan
        session.players = [player(female: false)]; session.playerIndex = 0; session.reduceMotion = false; session.overscan = 0
        menu.debugShow(.main)
        defer { session.players = oldPlayers; session.playerIndex = oldIndex; session.reduceMotion = oldMotion; session.overscan = oldOverscan; menu.debugShow(.title) }
        let scene = try XCTUnwrap(UIApplication.shared.connectedScenes.compactMap { $0 as? UIWindowScene }.first)
        let size = CGSize(width: 1280, height: 720)
        let host = UIHostingController(rootView: TennisTVRoot()); host.safeAreaRegions = []
        let window = UIWindow(windowScene: scene); window.frame = CGRect(origin: .zero, size: size)
        window.rootViewController = host; window.isHidden = false; host.view.frame = window.bounds; host.view.layoutIfNeeded()
        defer { window.isHidden = true }
        try await Task.sleep(for: .seconds(1.2))
        let film = try Film(url: proof.appendingPathComponent("real_club_home_tiles_tv.mp4"), size: size, bitrate: 8_000_000)
        func findScene(_ view: UIView) -> SCNView? { if let v = view as? SCNView { return v }; return view.subviews.compactMap(findScene).first }
        let steps: [(Double, String, () -> Void)] = [(0, "focus: Play (one Serve, then Ready)", { menu.focus("play") }), (7, "focus: Locker (ReadyIdle)", { menu.focus("character") }),
                                                      (12.5, "focus: Settings (ReadyIdle)", { menu.focus("settings") }), (16, "focus: Play (one Serve, then Ready)", { menu.focus("play") })]
        var next = 0, label = steps[0].1
        let started = CACurrentMediaTime(), format = UIGraphicsImageRendererFormat(); format.scale = 1; format.opaque = true
        let renderer = UIGraphicsImageRenderer(size: size, format: format)
        var rootYaws = Set<Int>(), frames = 0
        while CACurrentMediaTime() - started < 23 {
            let elapsed = CACurrentMediaTime() - started
            if next < steps.count, elapsed >= steps[next].0 { steps[next].2(); label = steps[next].1; next += 1 }
            let scn = findScene(host.view)
            let heroRoot = scn?.scene?.rootNode.childNode(withName: MatchHero.rootName, recursively: true)
            let yaw = Double(heroRoot?.eulerAngles.y ?? 0) * 180 / .pi
            rootYaws.insert(Int((yaw * 1000).rounded()))
            let hand = scn?.scene?.rootNode.childNode(withName: "skeleton", recursively: true)?.childNode(withName: "RightHand", recursively: false)
            let base = renderer.image { _ in host.view.drawHierarchy(in: CGRect(origin: .zero, size: size), afterScreenUpdates: false) }
            try await film.append(caption(base, [String(format: "t=%.1fs  %@", elapsed, label), String(format: "hero root yaw %.3f deg   racket hand y %.3f m   (%d fps view)", yaw, Double(hand?.simdPosition.y ?? 0), scn?.preferredFramesPerSecond ?? 0)]), at: elapsed)
            frames += 1
            try await Task.sleep(for: .milliseconds(12))
        }
        await film.finish(); XCTAssertEqual(film.writer.status, .completed)
        NSLog("[LockerMirror] real club Home recorded: \(frames) frames; distinct hero-root yaw values seen (milli-degrees): \(rootYaws.sorted())")
        XCTAssertEqual(rootYaws.count, 1, "the hero root never turned while the tiles played")
    }
    /// The real phone Home (402x874, compact layout) recorded in real time: the play tile's Serve, then Ready.
    func testRecordRealPhoneHome() async throws {
        let menu = TennisMenu.shared, session = SportsSession.shared
        let oldPlayers = session.players, oldIndex = session.playerIndex, oldMotion = session.reduceMotion
        session.players = [player(female: true)]; session.playerIndex = 0; session.reduceMotion = false
        defer { session.players = oldPlayers; session.playerIndex = oldIndex; session.reduceMotion = oldMotion; menu.debugShow(.title) }
        let scene = try XCTUnwrap(UIApplication.shared.connectedScenes.compactMap { $0 as? UIWindowScene }.first)
        let size = CGSize(width: 402, height: 874)
        menu.debugShow(.title)
        let host = UIHostingController(rootView: TennisPhoneMenu()); host.safeAreaRegions = []
        let window = UIWindow(windowScene: scene); window.frame = CGRect(origin: .zero, size: size)
        window.rootViewController = host; window.isHidden = false; host.view.frame = window.bounds; host.view.layoutIfNeeded()
        defer { window.isHidden = true }
        try await Task.sleep(for: .seconds(0.8))
        menu.debugShow(.main)   // the Home screen: the play tile starts its Serve now
        let film = try Film(url: proof.appendingPathComponent("real_club_home_phone.mp4"), size: size, bitrate: 4_000_000)
        let format = UIGraphicsImageRendererFormat(); format.scale = 1; format.opaque = true
        let renderer = UIGraphicsImageRenderer(size: size, format: format)
        let started = CACurrentMediaTime()
        while CACurrentMediaTime() - started < 8 {
            let elapsed = CACurrentMediaTime() - started
            let base = renderer.image { _ in host.view.drawHierarchy(in: CGRect(origin: .zero, size: size), afterScreenUpdates: false) }
            try await film.append(caption(base, [String(format: "phone Home, play tile  t=%.1fs", elapsed)]), at: elapsed)
            try await Task.sleep(for: .milliseconds(12))
        }
        await film.finish(); XCTAssertEqual(film.writer.status, .completed)
    }

    /// The real locker screens (phone and TV) with the new look: body (skin slot) and shoes, both players, default kit and picked colours.
    func testRecordRealLockerScreens() async throws {
        let menu = TennisMenu.shared, session = SportsSession.shared
        let oldPlayers = session.players, oldIndex = session.playerIndex
        defer { session.players = oldPlayers; session.playerIndex = oldIndex; menu.debugShow(.title) }
        let scene = try XCTUnwrap(UIApplication.shared.connectedScenes.compactMap { $0 as? UIWindowScene }.first)
        func shot<V: View>(_ view: V, _ name: String, _ size: CGSize) async throws {
            let host = UIHostingController(rootView: view); host.safeAreaRegions = []
            let window = UIWindow(windowScene: scene); window.frame = CGRect(origin: .zero, size: size)
            window.rootViewController = host; window.isHidden = false; host.view.frame = window.bounds; host.view.layoutIfNeeded()
            defer { window.isHidden = true }
            try await Task.sleep(for: .seconds(1.2))
            let image = UIGraphicsImageRenderer(size: size).image { _ in host.view.drawHierarchy(in: window.bounds, afterScreenUpdates: true) }
            try save(image, name)
        }
        for female in [false, true] {
            for picks in [false, true] {
                session.players = [player(female: female, picks: picks)]; session.playerIndex = 0
                for (slot, id) in [("body", "lk-slot-skin"), ("shoes", "lk-slot-shoes")] {
                    menu.debugShow(.character); menu.tap(id)
                    let tag = "\(female ? "female" : "male")_\(slot)\(picks ? "_picked" : "")"
                    try await shot(IslandLockerScreen(menu: menu, compact: true), "locker_screen_phone_\(tag).png", CGSize(width: 402, height: 874))
                    if !picks { try await shot(IslandLockerScreen(menu: menu, compact: false), "locker_screen_tv_\(tag).png", CGSize(width: 1280, height: 720)) }
                }
            }
        }
    }
    private final class FrameCounter: NSObject, SCNSceneRendererDelegate, @unchecked Sendable {
        private let lock = NSLock(); private var frames = 0
        func renderer(_ renderer: any SCNSceneRenderer, didRenderScene scene: SCNScene, atTime time: TimeInterval) { lock.lock(); frames += 1; lock.unlock() }
        var count: Int { lock.lock(); defer { lock.unlock() }; return frames }
    }

    /// How fast the real SwiftUI views render while they play (the simulator's Mac GPU, so an upper bound for a phone) and what posing the skeleton costs per frame on the CPU.
    func testMeasureTileFrameRateAndPoseCost() async throws {
        let windowScene = try XCTUnwrap(UIApplication.shared.connectedScenes.compactMap { $0 as? UIWindowScene }.first)
        var report = "LOCKER_MIRROR frame rate and pose cost (iOS Simulator on this Mac)\n"
        func findScene(_ view: UIView) -> SCNView? { if let v = view as? SCNView { return v }; return view.subviews.compactMap(findScene).first }
        for (label, make) in [("club locker tile", { (p: Player) in AnyView(CharacterModelPreview(player: p, cameraDistance: 3.25, menuActivity: .locker)) }),
                              ("club play tile", { (p: Player) in AnyView(CharacterModelPreview(player: p, cameraDistance: 3.25, menuActivity: .play)) }),
                              ("tennis idle view", { (p: Player) in AnyView(CharacterModelPreview(player: p, cameraDistance: 3.0, idleSport: .tennis)) })] {
            let host = UIHostingController(rootView: make(player(female: false)))
            let window = UIWindow(windowScene: windowScene); window.frame = CGRect(x: 0, y: 0, width: 402, height: 560)
            window.rootViewController = host; window.isHidden = false; host.view.frame = window.bounds; host.view.layoutIfNeeded()
            defer { window.isHidden = true }
            try await Task.sleep(for: .seconds(1.0))
            let scn = try XCTUnwrap(findScene(host.view)); let counter = FrameCounter(); scn.delegate = counter
            let t0 = CACurrentMediaTime(), f0 = counter.count
            try await Task.sleep(for: .seconds(3))
            let fps = Double(counter.count - f0) / (CACurrentMediaTime() - t0)
            report += String(format: "%@: %.1f fps rendered over 3 s (view asks for %d)\n", label, fps, scn.preferredFramesPerSecond)
            scn.delegate = nil
            XCTAssertGreaterThan(fps, 40, "\(label) renders smoothly in the simulator")
        }
        // CPU cost of posing the skeleton (58 tracks: sample two frames, slerp, compose, set 58 node transforms)
        let c = CharacterModelPreview.Coordinator(); c.update(player(female: false)); c.configureClub(.play, animate: true); c.pauseDisplayLink()
        defer { c.stopMotion() }
        let n = 2000, start = CACurrentMediaTime()
        for k in 0 ..< n { c.pose(at: Double(k) * 0.0037) }
        let ms = (CACurrentMediaTime() - start) / Double(n) * 1000
        report += String(format: "pose(at:) on the main thread: %.3f ms per frame (%d tracks)\n", ms, c.rig?.data.info.trackCount ?? 0)
        XCTAssertLessThan(ms, 2.0, "posing the skeleton leaves the frame to the GPU")
        try report.write(to: proof.appendingPathComponent("fps_and_pose_cost.txt"), atomically: true, encoding: .utf8)
        NSLog("[LockerMirror] \(report)")
    }
    /// The real loading screen (the tennis idle view) in the TV root: ReadyIdle on the skeleton and the bouncing prop ball, one "Swing to practice" at 4 s (the Forehand morph), then back to ReadyIdle.
    func testRecordLoadingIdleAndPracticeSwing() async throws {
        let menu = TennisMenu.shared, session = SportsSession.shared
        let oldPlayers = session.players, oldIndex = session.playerIndex, oldMotion = session.reduceMotion
        session.players = [player(female: false)]; session.playerIndex = 0; session.reduceMotion = false
        defer { session.players = oldPlayers; session.playerIndex = oldIndex; session.reduceMotion = oldMotion; session.loading.cancel(); menu.debugShow(.title) }
        menu.debugShow(.loading, launch: MenuLaunch(mode: .training)); session.loading.begin(now: Date()); session.loading.reach(0.72); session.loading.tick(now: Date())
        let scene = try XCTUnwrap(UIApplication.shared.connectedScenes.compactMap { $0 as? UIWindowScene }.first)
        let size = CGSize(width: 1280, height: 720)
        let host = UIHostingController(rootView: TennisTVRoot()); host.safeAreaRegions = []
        let window = UIWindow(windowScene: scene); window.frame = CGRect(origin: .zero, size: size)
        window.rootViewController = host; window.isHidden = false; host.view.frame = window.bounds; host.view.layoutIfNeeded()
        defer { window.isHidden = true }
        func findScene(_ view: UIView) -> SCNView? { if let v = view as? SCNView { return v }; return view.subviews.compactMap(findScene).first }
        try await Task.sleep(for: .seconds(0.8))
        let film = try Film(url: proof.appendingPathComponent("loading_idle_practice_tv.mp4"), size: size, bitrate: 6_000_000)
        let format = UIGraphicsImageRendererFormat(); format.scale = 1; format.opaque = true
        let renderer = UIGraphicsImageRenderer(size: size, format: format)
        let started = CACurrentMediaTime(); var swung = false
        while CACurrentMediaTime() - started < 9 {
            let elapsed = CACurrentMediaTime() - started
            if !swung, elapsed > 4 { session.loading.practice(); swung = true }
            let scn = findScene(host.view)
            let hero = scn?.scene?.rootNode.childNode(withName: MatchHero.rootName, recursively: true)
            var morphing = 0; hero?.enumerateChildNodes { node, _ in if let m = node.morpher, m.weights.contains(where: { $0.doubleValue > 0.01 }) { morphing += 1 } }
            let on = scn?.scene?.rootNode.childNode(withName: "skeleton", recursively: true) != nil
            let base = renderer.image { _ in host.view.drawHierarchy(in: CGRect(origin: .zero, size: size), afterScreenUpdates: false) }
            try await film.append(caption(base, [String(format: "loading screen, tennis idle  t=%.1fs  %@", elapsed, on ? "ReadyIdle on the skeleton" : morphing > 0 ? "practice swing (Forehand morph)" : "still Ready stance"),
                                                 String(format: "hero root yaw %.3f deg (fixed)   %d fps view", Double(hero?.eulerAngles.y ?? 0) * 180 / .pi, scn?.preferredFramesPerSecond ?? 0)]), at: elapsed)
            try await Task.sleep(for: .milliseconds(12))
        }
        await film.finish(); XCTAssertEqual(film.writer.status, .completed)
    }
}
