import SwiftUI
import SceneKit
import XCTest
@testable import GolfArcade

@MainActor final class ClubEnvironmentTests: XCTestCase {
    func testAllScenesHaveBakedMeshAndUVs() throws {
        for room in [ClubRoom.entrance, .terrace, .locker, .loading] {
            let asset = try XCTUnwrap(ClubEnvironmentAsset.load(room))
            XCTAssertGreaterThan(asset.triangles.count / 3, 1000)
            XCTAssertLessThan(asset.triangles.count / 3, 100000)
            XCTAssertEqual(asset.positions.count, asset.normals.count)
            XCTAssertEqual(asset.uv.count / 2, asset.positions.count / 3)
            XCTAssertTrue(asset.uv.allSatisfy { $0 >= 0 && $0 <= 1 })
            XCTAssertNotNil(UIImage(named: asset.texture + ".png"))
            XCTAssertGreaterThan((asset.positions.max() ?? 0) - (asset.positions.min() ?? 0), 15)
        }
    }
    func testCaptureRealMenuEnvironments() async throws {
        let out = URL(fileURLWithPath: #filePath).deletingLastPathComponent().deletingLastPathComponent().appendingPathComponent("ArtDir/environments/motion-club-3d/review-live-images")
        try FileManager.default.createDirectory(at: out, withIntermediateDirectories: true)
        let menu = TennisMenu.shared, session = SportsSession.shared
        let oldPlayers = session.players, oldIndex = session.playerIndex, motion = session.reduceMotion
        var player = Player(name: "Adnan", colorIndex: 0); player.setSkin(0.35); player.hairColor = 3
        session.players = [player]; session.playerIndex = 0; session.reduceMotion = true
        defer { session.players = oldPlayers; session.playerIndex = oldIndex; session.reduceMotion = motion; session.loading.cancel(); menu.debugShow(.title) }
        let scene = try XCTUnwrap(UIApplication.shared.connectedScenes.compactMap { $0 as? UIWindowScene }.first)
        for (screen,name) in [(MenuScreen.title,"01-entrance"),(.main,"02-terrace"),(.character,"03-locker"),(.loading,"04-loading"),(.settings,"05-settings")] {
            menu.debugShow(screen, launch: screen == .loading ? MenuLaunch(mode: .training) : nil)
            if screen == .loading { session.loading.begin(now: Date()); session.loading.reach(0.65); session.loading.tick(now: Date()) }
            let host = UIHostingController(rootView: TennisTVRoot()); host.safeAreaRegions = []
            let w = UIWindow(windowScene: scene); w.frame = CGRect(x: 0,y: 0,width: 1280,height: 720); w.rootViewController = host; w.isHidden = false
            host.view.frame = w.bounds; host.view.layoutIfNeeded()
            try await Task.sleep(for: .seconds(2))
            let format = UIGraphicsImageRendererFormat(); format.scale = 1
            let image = UIGraphicsImageRenderer(size: w.bounds.size, format: format).image { _ in host.view.drawHierarchy(in: w.bounds, afterScreenUpdates: true) }
            try XCTUnwrap(image.pngData()).write(to: out.appendingPathComponent(name + "-runtime.png"))
            w.isHidden = true
        }
        menu.debugShow(.main)
        let host = UIHostingController(rootView: TennisPhoneMenu()); host.safeAreaRegions = []
        let w = UIWindow(windowScene: scene); w.frame = CGRect(x:0,y:0,width:402,height:874);w.rootViewController=host;w.isHidden=false
        defer { w.isHidden=true }
        host.view.frame=w.bounds;host.view.layoutIfNeeded();try await Task.sleep(for:.seconds(2))
        let format=UIGraphicsImageRendererFormat();format.scale=1
        let image=UIGraphicsImageRenderer(size:w.bounds.size,format:format).image { _ in host.view.drawHierarchy(in:w.bounds,afterScreenUpdates:true) }
        try XCTUnwrap(image.pngData()).write(to:out.appendingPathComponent("06-phone-runtime.png"))
    }
    func testCaptureAlternateViewOfBakedGeometry() async throws {
        let out = URL(fileURLWithPath: #filePath).deletingLastPathComponent().deletingLastPathComponent().appendingPathComponent("ArtDir/environments/motion-club-3d/review-live-images")
        try FileManager.default.createDirectory(at: out, withIntermediateDirectories: true)
        let c = ClubEnvironmentView.Coordinator(), view = SCNView(frame: CGRect(x:0,y:0,width:1280,height:720))
        c.show(.terrace,in:view,animate:false)
        let renderer = SCNRenderer(device: nil, options: nil); renderer.scene = view.scene; renderer.pointOfView = c.camera
        for (x,name) in [(Float(-3),"terrace-angle-left"),(Float(3),"terrace-angle-right")] {
            c.camera.position.x=x;c.camera.look(at:SCNVector3(0,2,-5))
            let image=renderer.snapshot(atTime:0,with:CGSize(width:1280,height:720),antialiasingMode:.multisampling4X)
            try XCTUnwrap(image.pngData()).write(to:out.appendingPathComponent(name+".png"))
        }
    }
    func testCameraParallaxAndReduceMotion() async throws {
        let c = ClubEnvironmentView.Coordinator(), view = SCNView(frame: CGRect(x:0,y:0,width:1280,height:720))
        c.show(.terrace, in:view, animate:true)
        XCTAssertTrue(c.camera.hasActions)
        XCTAssertNotNil(view.scene?.rootNode.childNode(withName:"ClubEnvironment_terrace",recursively:false)?.geometry)
        c.show(.terrace,in:view,animate:false)
        XCTAssertFalse(c.camera.hasActions)
        XCTAssertFalse(view.isPlaying)
        c.show(.locker,in:view,animate:false)
        XCTAssertNotNil(view.scene?.rootNode.childNode(withName:"ClubEnvironment_locker",recursively:false))
    }
}
