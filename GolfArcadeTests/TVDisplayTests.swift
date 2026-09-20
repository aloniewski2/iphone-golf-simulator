import XCTest
import SceneKit
@testable import GolfArcade

@MainActor
final class TVDisplayTests: XCTestCase {
    func testRemovingPhoneRendererCannotStopTVAnimationClock() {
        let scene = CourseScene()
        scene.start()
        XCTAssertTrue(scene.isAnimating)
        let view = SCNView()
        view.scene = scene.scene
        CourseSceneView.dismantleUIView(view, coordinator: nil)
        XCTAssertTrue(scene.isAnimating, "The course screen, not either renderer, owns animation")
        XCTAssertNil(view.scene)
        scene.stop()
        XCTAssertFalse(scene.isAnimating)
    }

    func testTVObservesPhoneClubAimAndOneAuthoritativeShot() {
        let round = CourseRound()
        let scene = CourseScene()
        let session = TVGameSession(round: round, scene: scene)
        round.club = .iron9
        round.adjustAim(14)
        XCTAssertEqual(session.round.club, .iron9)
        XCTAssertEqual(session.round.combinedAim, 14)
        round.charge(0.5)
        XCTAssertTrue(round.release(execution: SwingImpact(power: 0.5, source: .phone)))
        XCTAssertEqual(session.round.phase, .flying)
        XCTAssertEqual(session.round.activeShot, round.activeShot)
        XCTAssertFalse(round.release(), "TV cannot cause a duplicate scored release")
        scene.stop()
    }


    func testTVModeDefaultsOn() {
        let defaults = UserDefaults(suiteName: UUID().uuidString)!
        XCTAssertTrue(GolfTVDisplay(defaults: defaults).enabled)
    }

    func testPhoneSceneLeavesHostingToSwiftUI() {
        let configuration = GolfAppDelegate.configuration(for: .windowApplication)
        XCTAssertEqual(configuration.role, .windowApplication)
        XCTAssertNil(configuration.name)
        XCTAssertNil(configuration.delegateClass)
        XCTAssertNil(configuration.sceneClass)
        XCTAssertNil(configuration.storyboard)
    }

    func testDisplayModeChangesRetainOneRoundAndDoNotSpendAStroke() throws {
        let suite = "TVDisplayTests.\(UUID().uuidString)"
        let defaults = try XCTUnwrap(UserDefaults(suiteName: suite))
        defer { defaults.removePersistentDomain(forName: suite) }
        let display = GolfTVDisplay(defaults: defaults)
        let round = CourseRound(defaults: defaults)
        let scene = CourseScene()
        display.present(round: round, scene: scene)
        let session = try XCTUnwrap(display.session)
        round.charge(0.7)
        XCTAssertTrue(round.release())
        let shot = round.activeShot
        for enabled in [true, false, true, false] {
            display.enabled = enabled
            display.present(round: round, scene: scene)
            XCTAssertTrue(display.session === session)
            XCTAssertTrue(display.session?.round === round)
            XCTAssertTrue(display.session?.scene === scene)
            XCTAssertEqual(round.phase, .flying)
            XCTAssertEqual(round.activeShot, shot)
            XCTAssertEqual(round.strokes, 0)
        }
        round.skipFlight()
        XCTAssertEqual(round.strokes, 1 + (shot?.penaltyStrokes ?? 0))
        display.enabled = true
        XCTAssertTrue(GolfTVDisplay(defaults: defaults).enabled)
        display.end(round: CourseRound(defaults: defaults))
        XCTAssertTrue(display.session === session, "An old view cannot detach a newer round")
        display.end(round: round)
        XCTAssertNil(display.session)
    }

    func testExternalSceneUsesSeparateNoninteractiveDelegate() {
        let configuration = GolfTVDisplay.sceneConfiguration()
        XCTAssertEqual(configuration.role, .windowExternalDisplayNonInteractive)
        XCTAssertTrue(configuration.delegateClass === GolfExternalSceneDelegate.self)
    }
}
