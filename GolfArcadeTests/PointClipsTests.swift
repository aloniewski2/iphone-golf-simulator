import XCTest
@testable import GolfArcade

@MainActor final class PointClipsTests: XCTestCase {
    func testSavingAnOldPointDoesNotReenableCapture() {
        let clips = PointClips()
        clips.receive(["state": "saved", "hasClip": true, "enabled": false])
        XCTAssertFalse(clips.bufferingEnabled)
        XCTAssertTrue(clips.hasClip)
    }
    func testPerformanceFailureKeepsPreviousGoodPoint() {
        let clips = PointClips()
        clips.exporting = true
        clips.receive(["state": "unsupported", "hasClip": true, "enabled": false, "message": "Below 60 fps"])
        XCTAssertFalse(clips.bufferingEnabled)
        XCTAssertFalse(clips.exporting)
        XCTAssertTrue(clips.hasClip)
        XCTAssertEqual(clips.message, "Below 60 fps")
    }
    func testRejectsExportOutsideClipLibrary() {
        let clips = PointClips()
        clips.receive(["state": "saved", "url": "/tmp/unrelated.mp4", "enabled": true])
        XCTAssertNil(clips.exported)
    }
    func testResetClearsSessionRecordingState() {
        let clips = PointClips()
        clips.receive(["state": "available", "enabled": true, "hasClip": true])
        clips.reset()
        XCTAssertFalse(clips.bufferingEnabled)
        XCTAssertFalse(clips.hasClip)
        XCTAssertEqual(clips.state, "off")
    }
}
