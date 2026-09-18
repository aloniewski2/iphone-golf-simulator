import XCTest
@testable import GolfArcade

@MainActor
final class TVDisplayTests: XCTestCase {
    func testSetupRequestsNeverResetOrRescoreAnInFlightShot() throws {
        let suite = "TVSetup.\(UUID().uuidString)"
        let defaults = try XCTUnwrap(UserDefaults(suiteName: suite))
        defer { defaults.removePersistentDomain(forName: suite) }
        let display = GolfTVDisplay(defaults: defaults)
        let round = CourseRound(defaults: defaults)
        display.present(round: round, scene: CourseScene(), camera: CameraSwingController())
        round.charge(0.6)
        XCTAssertTrue(round.release())
        let shot = round.activeShot
        display.enabled = true
        let first = display.setupRequestID
        XCTAssertGreaterThan(first, 0)
        display.enabled = true
        XCTAssertEqual(display.setupRequestID, first, "An unchanged toggle cannot repeatedly recenter")
        display.requestSetup()
        XCTAssertEqual(display.setupRequestID, first + 1)
        XCTAssertEqual(round.phase, .flying)
        XCTAssertEqual(round.activeShot, shot)
        XCTAssertEqual(round.strokes, 0)
        for phase in [CourseRound.Phase.flying, .landed, .holed, .complete] {
            XCTAssertFalse(CourseScreen.canReposition(for: phase))
        }
        XCTAssertTrue(CourseScreen.canReposition(for: .ready))
        XCTAssertTrue(CourseScreen.canReposition(for: .charging))
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
        let camera = CameraSwingController()
        display.present(round: round, scene: scene, camera: camera)
        let session = try XCTUnwrap(display.session)
        round.charge(0.7)
        XCTAssertTrue(round.release())
        let shot = round.activeShot
        for enabled in [true, false, true, false] {
            display.enabled = enabled
            display.present(round: round, scene: scene, camera: camera)
            XCTAssertTrue(display.session === session)
            XCTAssertTrue(display.session?.round === round)
            XCTAssertTrue(display.session?.scene === scene)
            XCTAssertTrue(display.session?.camera === camera)
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
