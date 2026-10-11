import SwiftUI
import SceneKit
import XCTest
@testable import GolfArcade

/// HERO_MAINSTAY / SWIFT_LOCKER: the locker mirror (CharacterModelPreview, which every locker / club / loading screen embeds) shows the same two bodies the
/// match plays (work/match-anim-set HeroBase Male / Female, bald, painted face, the classic racket), exported by Unity's MatchHeroLockerExport. These tests
/// check what is in the SceneKit scene and write the proof renders to work/hero-mainstay/proof/swift_locker/.
@MainActor
final class MatchHeroLockerTests: XCTestCase {
    private static let kitNodes: Set<String> = ["Kit_Top", "Kit_Bottom", "Kit_Sock_L", "Kit_Sock_R", "Kit_Shoe_L", "Kit_Shoe_R"]
    private static let allowedNodes: Set<String> = Set(["matchHero", "racket", "Body", "Face", "Racket_Racket_Classic", "Racket_ButtCap", "Racket_Grip", "Racket_StringBed"]).union(kitNodes)   // DRESS_MATCH_HEROES: the White tennis kit is part of the hero
    private var repo: URL { URL(fileURLWithPath: #filePath).deletingLastPathComponent().deletingLastPathComponent() }
    private var proof: URL {
        let url = repo.appendingPathComponent("work/hero-mainstay/proof/swift_locker")
        try? FileManager.default.createDirectory(at: url, withIntermediateDirectories: true)
        return url
    }

    private func player(female: Bool, skin: Double = 0.35) -> Player {
        var p = Player(name: female ? "Maya" : "Alex", colorIndex: 0); p.standardFemale = female; p.setSkin(skin); return p
    }

    private func vertices(_ geometry: SCNGeometry) throws -> [Float] {
        let src = try XCTUnwrap(geometry.sources(for: .vertex).first)
        return src.data.withUnsafeBytes { bytes in
            (0 ..< src.vectorCount * 3).map { bytes.loadUnaligned(fromByteOffset: src.dataOffset + ($0 / 3) * src.dataStride + ($0 % 3) * 4, as: Float.self) }
        }
    }

    private func centroid(_ geometry: SCNGeometry) throws -> SIMD3<Float> {
        let v = try vertices(geometry); var sum = SIMD3<Float>(repeating: 0)
        for i in stride(from: 0, to: v.count, by: 3) { sum += SIMD3(v[i], v[i + 1], v[i + 2]) }
        return sum / Float(v.count / 3)
    }

    private func channels(_ color: UIColor) -> [Int] {
        var r: CGFloat = 0, g: CGFloat = 0, b: CGFloat = 0, a: CGFloat = 0; color.getRed(&r, green: &g, blue: &b, alpha: &a)
        return [r, g, b].map { Int(($0 * 255).rounded()) }
    }
    private func channels(hex: String) -> [Int] {
        let n = Int(hex, radix: 16) ?? 0; return [(n >> 16) & 255, (n >> 8) & 255, n & 255]
    }
    private func assertColour(_ color: UIColor, hex: String, _ message: String, file: StaticString = #filePath, line: UInt = #line) {
        let a = channels(color), b = channels(hex: hex)
        for i in 0 ..< 3 { XCTAssertLessThanOrEqual(abs(a[i] - b[i]), 1, "\(message): \(a) vs \(b)", file: file, line: line) }
    }

    // MARK: the data

    func testBothMatchHeroesShipAndTheOldHeroDoesNot() throws {
        for female in [false, true] {
            let asset = try XCTUnwrap(MatchHero.asset(female: female), "MatchHero_\(female ? "Female" : "Male") is in the app bundle")
            XCTAssertEqual(asset.manifest.sex, female ? "Female" : "Male")
            XCTAssertEqual(asset.manifest.baseClip, female ? "Female_ReadyIdle" : "Male_ReadyIdle")
            XCTAssertEqual(asset.manifest.swingClip, female ? "Female_Forehand" : "Male_Forehand")
            let body = try XCTUnwrap(asset.manifest.parts.first { $0.name == "Body" })
            XCTAssertEqual(body.vertexCount, (body.submeshes.first?.look?.skinPigmentUV ?? 0) > 0.5 ? (female ? 34443 : 44168) : (female ? 23683 : 33196), "the HeroBase \(female ? "Female" : "Male") source body")
            XCTAssertEqual(Set(asset.manifest.parts.map(\.name)), Set(["Body", "Face", "Racket_Racket_Classic", "Racket_ButtCap", "Racket_Grip", "Racket_StringBed"]).union(Self.kitNodes), "body, painted face, the worn tennis kit and the classic racket only")
            XCTAssertEqual(asset.manifest.swingFrames.count, asset.manifest.swingTimes.count)
            XCTAssertEqual(asset.manifest.swingContact, 0.6667, accuracy: 0.001, "the match Forehand contact time")
        }
        // the old baked hero (HeroMenu / HeroV4 / HeroLounge / HeroSwing_*, and the old Higgs PlayerMale* base) is not in the app bundle any more
        for name in ["HeroMenu", "HeroV4", "HeroLounge", "HeroSwing_00", "HeroSwing_19", "HeroV4_Atlas", "PlayerMaleSkin", "PlayerFemaleSkin", "PlayerMaleColor"] {
            for ext in ["json", "lzfse", "bin", "png", "mask"] { XCTAssertNil(Bundle.main.url(forResource: name, withExtension: ext), "\(name).\(ext) must not ship") }
        }
    }

    // MARK: the mirror

    func testLockerMirrorShowsTheMatchHeroForTheLockersSex() throws {
        for female in [false, true] {
            var p = player(female: female)
            p.racket = 2
            let c = CharacterModelPreview.Coordinator(); c.update(p)
            let hero = try XCTUnwrap(c.hero, "the mirror shows a hero")
            XCTAssertEqual(c.character.childNodes.count, 1)
            XCTAssertEqual(hero.value(forKey: "heroFemale") as? Bool, female, "sex follows the locker pick")
            XCTAssertNil(c.character.childNode(withName: "heroV4", recursively: true), "the old baked hero is not on stage")
            hero.enumerateHierarchy { node, _ in XCTAssertTrue(Self.allowedNodes.contains(node.name ?? ""), "unexpected node '\(node.name ?? "?")': only the body, face, the worn kit and the racket may be on stage") }
            let bodyGeometry = try XCTUnwrap(hero.childNode(withName: "Body", recursively: false)?.geometry)
            XCTAssertEqual(bodyGeometry.sources(for: .vertex).first?.vectorCount, (MatchHeroSurfaces.made(bodyGeometry.materials[0])?.look?.skinPigmentUV ?? 0) > 0.5 ? (female ? 34443 : 44168) : (female ? 23683 : 33196))
            // skin pick on the body, racket pick on the frame, white strings
            for material in bodyGeometry.materials {
                let made = try XCTUnwrap(MatchHeroSurfaces.made(material))
                if (made.look?.skinPigmentUV ?? 0) > 0.5 {
                    let c = made.colour
                    assertColour(UIColor(red: CGFloat(c.x), green: CGFloat(c.y), blue: CGFloat(c.z), alpha: 1), hex: p.skinHex, "relative pigment retains the selected skin tint")
                    XCTAssertTrue(made.look?.baseMapLinear == true)
                    XCTAssertEqual(made.look?.baseMap, "MatchHero_OriginalSeam_RelativeSkinPigment")
                    XCTAssertNotNil(material.value(forKey: "heroTint"))
                } else {
                    assertColour(try XCTUnwrap(material.diffuse.contents as? UIColor), hex: p.skinHex, "legacy skin pick on the body")
                }
            }
            let frame = try XCTUnwrap(hero.childNode(withName: "Racket_Racket_Classic", recursively: true)?.geometry?.firstMaterial?.diffuse.contents as? UIColor)
            assertColour(frame, hex: try XCTUnwrap(p.outfitHex("racket")), "the locker's racket colour tints the frame")
            let strings = try XCTUnwrap(hero.childNode(withName: "Racket_StringBed", recursively: true)?.geometry?.firstMaterial?.diffuse.contents as? UIColor)
            XCTAssertNotEqual(channels(strings), channels(frame), "the strings keep their own colour")
            // the body is a real human-height mannequin on the stage, bald: the top of the head is the top of the body
            let (lo, hi) = try XCTUnwrap(hero.childNode(withName: "Body", recursively: false)).boundingBox
            XCTAssertEqual(Float(hi.y - lo.y) * Float(abs(hero.scale.y)), MatchHero.stageHeight, accuracy: 0.06)
        }
    }

    func testHairHeadwearAndBuildPicksDoNotChangeTheMesh() throws {
        for female in [false, true] {
            var a = player(female: female), b = player(female: female)
            a.haircut = 0; a.hairStyle = 0; a.faceShape = 0; a.heightChoice = 2; a.bodySize = 0.5
            b.haircut = 4; b.hairStyle = 3; b.faceShape = 3; b.heightChoice = 4; b.bodySize = 1
            let ca = CharacterModelPreview.Coordinator(), cb = CharacterModelPreview.Coordinator(); ca.update(a); cb.update(b)
            let ha = try XCTUnwrap(ca.hero), hb = try XCTUnwrap(cb.hero)
            var namesA: [String] = [], namesB: [String] = []
            ha.enumerateHierarchy { n, _ in namesA.append(n.name ?? "") }; hb.enumerateHierarchy { n, _ in namesB.append(n.name ?? "") }
            XCTAssertEqual(namesA, namesB, "no hair or hat node appears for any haircut / headwear pick: the heroes are bald")
            XCTAssertEqual(try vertices(try XCTUnwrap(ha.childNode(withName: "Body", recursively: false)?.geometry)),
                           try vertices(try XCTUnwrap(hb.childNode(withName: "Body", recursively: false)?.geometry)))
        }
    }

    func testLeftHandedLockerMirrorsTheHero() throws {
        var p = player(female: false); p.handedness = .left
        let c = CharacterModelPreview.Coordinator(); c.update(p)
        XCTAssertLessThan(try XCTUnwrap(c.hero).scale.x, 0)
        p.handedness = .right; c.update(p)
        XCTAssertGreaterThan(try XCTUnwrap(c.hero).scale.x, 0)
    }

    func testRacketHidesInCloseUpsAndForGolf() throws {
        let c = CharacterModelPreview.Coordinator(); c.update(player(female: true))
        XCTAssertEqual(c.racket?.isHidden, false)
        c.framing = .head; c.applyFraming()
        XCTAssertEqual(c.racket?.isHidden, true, "the Ready racket crosses the chin in head close-ups")
        c.framing = .body; c.applyFraming(); c.configureIdle(sport: .golf, animate: false)
        XCTAssertEqual(c.racket?.isHidden, true)
    }

    // MARK: the practice swing (the match Forehand)

    func testPracticeSwingPlaysTheMatchForehand() throws {
        for female in [false, true] {
            let c = CharacterModelPreview.Coordinator(); c.update(player(female: female))
            let hero = try XCTUnwrap(c.hero), asset = try XCTUnwrap(MatchHero.asset(female: female))
            MatchHero.preparePractice(hero)
            var animated = 0
            hero.enumerateChildNodes { node, _ in if let m = node.morpher { animated += 1; XCTAssertEqual(m.targets.count, asset.manifest.swingFrames.count) } }
            XCTAssertEqual(animated, asset.manifest.parts.count, "every part has the swing as morph targets")
            let body = try XCTUnwrap(hero.childNode(withName: "Body", recursively: false))
            for t in try XCTUnwrap(body.morpher).targets { XCTAssertTrue(try vertices(t).allSatisfy(\.isFinite)) }
            // at the clip's contact time the contact frame has the full weight
            let contactProgress = 0.1 + 0.75 * Double(asset.manifest.swingContact / asset.manifest.swingLength)
            MatchHero.practiceFrame(hero, progress: contactProgress)
            let contactIndex = try XCTUnwrap(asset.manifest.swingTimes.firstIndex { abs($0 - asset.manifest.swingContact) < 0.002 })
            let morph = try XCTUnwrap(body.morpher)
            XCTAssertGreaterThan(morph.weight(forTargetAt: contactIndex), 0.95, "the Forehand contact frame is the swing's peak")
            // the racket really travels: string bed centre at the first and at the contact frame
            let strings = try XCTUnwrap(hero.childNode(withName: "Racket_StringBed", recursively: true)?.morpher)
            let move = simd_distance(try centroid(strings.targets[0]), try centroid(strings.targets[contactIndex]))
            XCTAssertGreaterThan(move, 0.3, "the strings sweep more than 30 cm between the backswing and the contact (metres in the file)")
            // outside the swing the hero is back in the Ready stance
            MatchHero.practiceFrame(hero, progress: 0)
            XCTAssertTrue(morph.weights.allSatisfy { $0.doubleValue == 0 })
        }
    }

    func testLoadingPracticeAndClubActivitiesUseTheMatchHero() throws {
        let p = player(female: false)
        let c = CharacterModelPreview.Coordinator(); c.update(p)
        c.practice(1)
        c.practiceTimer?.invalidate()
        XCTAssertNotNil(c.hero?.childNode(withName: "Body", recursively: false)?.morpher)
        for activity in [ClubPreviewActivity.play, .locker, .settings, .store] {
            let club = CharacterModelPreview.Coordinator(); club.update(p)
            club.configureClub(activity, animate: false)
            club.clubTimer?.invalidate()
            XCTAssertNotNil(club.hero, "\(activity) shows the match hero")
            XCTAssertEqual(club.racket?.isHidden, activity != .play)
        }
    }

    /// Mean absolute difference (0...255 per channel) between two renders of the same size.
    private func difference(_ a: UIImage, _ b: UIImage) throws -> Double {
        func pixels(_ image: UIImage) throws -> [UInt8] {
            let cg = try XCTUnwrap(image.cgImage), w = 96, h = 96
            var data = [UInt8](repeating: 0, count: w * h * 4)
            data.withUnsafeMutableBytes { ptr in
                let ctx = CGContext(data: ptr.baseAddress, width: w, height: h, bitsPerComponent: 8, bytesPerRow: w * 4, space: CGColorSpaceCreateDeviceRGB(), bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)!
                ctx.interpolationQuality = .medium; ctx.draw(cg, in: CGRect(x: 0, y: 0, width: w, height: h))
            }
            return data
        }
        let pa = try pixels(a), pb = try pixels(b)
        return zip(pa, pb).reduce(0.0) { $0 + abs(Double($1.0) - Double($1.1)) } / Double(pa.count)
    }

    // (The haircut / headwear style-card thumbnails were retired with the item-first locker; its shelf thumbnails are LockerThumbs.image(_:slot:item:).)

    // MARK: proof renders

    private func snapshot(_ c: CharacterModelPreview.Coordinator, size: CGSize) throws -> Data {
        let renderer = SCNRenderer(device: nil, options: nil); renderer.scene = c.scene; renderer.pointOfView = c.camera
        return try XCTUnwrap(renderer.snapshot(atTime: 0, with: size, antialiasingMode: .multisampling4X).pngData())
    }

    /// The scenes the phone's locker, club menus and loading screen build, rendered to PNG: the Ready stance full body and head, the practice swing at the contact.
    func testRecordLockerMirrorProof() throws {
        for female in [false, true] {
            let sex = female ? "female" : "male"
            var p = player(female: female, skin: female ? 0.2 : 0.45); p.racket = female ? 4 : 1
            for (label, framing) in [("body", PreviewFraming.body), ("head", .head)] {
                let c = CharacterModelPreview.Coordinator(cameraDistance: 3.3); c.framing = framing; c.update(p)
                c.scene.background.contents = UIColor(red: 0.93, green: 0.62, blue: 0.48, alpha: 1)   // the locker's warm coral wall
                try snapshot(c, size: CGSize(width: 720, height: 1000)).write(to: proof.appendingPathComponent("locker_\(sex)_\(label).png"))
            }
            // the practice swing: the match Forehand at its contact frame, as the loading screen / club "play" tile shows it
            let c = CharacterModelPreview.Coordinator(cameraDistance: 3.0); c.update(p)
            c.scene.background.contents = UIColor(red: 0.93, green: 0.62, blue: 0.48, alpha: 1)
            let hero = try XCTUnwrap(c.hero); MatchHero.preparePractice(hero)
            let asset = try XCTUnwrap(MatchHero.asset(female: female))
            for (label, clipTime) in [("backswing", 0.42), ("contact", Double(asset.manifest.swingContact)), ("follow", 0.9)] {
                MatchHero.practiceFrame(hero, progress: 0.1 + 0.75 * clipTime / Double(asset.manifest.swingLength))
                try snapshot(c, size: CGSize(width: 720, height: 1000)).write(to: proof.appendingPathComponent("practice_\(sex)_\(label).png"))
            }
            // the club menus
            for activity in [ClubPreviewActivity.settings, .store, .locker] {
                let club = CharacterModelPreview.Coordinator(cameraDistance: 3.25); club.update(p); club.configureClub(activity, animate: false); club.clubTimer?.invalidate()
                club.scene.background.contents = UIColor(red: 0.93, green: 0.62, blue: 0.48, alpha: 1)
                try snapshot(club, size: CGSize(width: 720, height: 1000)).write(to: proof.appendingPathComponent("club_\(sex)_\(activity.rawValue).png"))
            }
        }
    }

    /// The real SwiftUI view in a window (what the phone draws), read back from its SCNView.
    func testRecordHostedLockerView() async throws {
        let scene = try XCTUnwrap(UIApplication.shared.connectedScenes.compactMap { $0 as? UIWindowScene }.first)
        for female in [false, true] {
            let p = player(female: female)
            let host = UIHostingController(rootView: CharacterModelPreview(player: p, cameraDistance: 3.3).background(Color(red: 0.93, green: 0.62, blue: 0.48)))
            let window = UIWindow(windowScene: scene); window.frame = CGRect(x: 0, y: 0, width: 402, height: 560)
            window.rootViewController = host; window.isHidden = false; host.view.frame = window.bounds; host.view.layoutIfNeeded()
            try await Task.sleep(for: .seconds(1.2))
            func findScene(_ view: UIView) -> SCNView? { if let v = view as? SCNView { return v }; return view.subviews.compactMap(findScene).first }
            let scn = try XCTUnwrap(findScene(host.view), "CharacterModelPreview is an SCNView")
            let shown = try XCTUnwrap(scn.scene?.rootNode.childNode(withName: MatchHero.rootName, recursively: true), "the real view shows the match hero")
            XCTAssertEqual(shown.value(forKey: "heroFemale") as? Bool, female)
            try XCTUnwrap(scn.snapshot().pngData()).write(to: proof.appendingPathComponent("hosted_view_\(female ? "female" : "male").png"))
            window.isHidden = true
        }
    }
}
