import XCTest
@preconcurrency import AVFoundation
@testable import GolfArcade

final class CaptureEvidenceTests: XCTestCase {
    private func format(_ index: Int, width: Int = 1280, height: Int = 720, fov: Double = 90,
                        minimum: Double = 30, maximum: Double = 60) -> CaptureFormatCapability {
        .init(index: index, width: width, height: height, fieldOfView: fov,
              rates: [.init(minimum: minimum, maximum: maximum)])
    }

    func testCoverageWinsOverNarrowHighFPS() {
        let choice = CaptureFormatSelection.choose([format(0, fov: 95, maximum: 30),
            format(1, fov: 75, maximum: 120)], requestedFPS: 120)
        XCTAssertEqual(choice, .init(index: 0, fps: 30))
    }

    func testHigherRateWinsAtEquivalentCoverageAndSmallerFormatBreaksTie() {
        let choice = CaptureFormatSelection.choose([format(0, maximum: 30),
            format(1, width: 1920, height: 1080, maximum: 120), format(2, maximum: 120)], requestedFPS: 120)
        XCTAssertEqual(choice, .init(index: 2, fps: 120))
    }

    func testFPSFallbackHonorsMinimumAndMaximumRanges() {
        XCTAssertNil(CaptureFormatSelection.choose([format(0, minimum: 60)], requestedFPS: 30))
        XCTAssertEqual(CaptureFormatSelection.choose([format(0)], requestedFPS: 120)?.fps, 60)
        XCTAssertEqual(CaptureFormatSelection.choose([format(0)], requestedFPS: 999)?.fps, 30)
    }

    func testPhotoFormatsAndLowResolutionDoNotEnterLiveTrackingSelection() {
        XCTAssertNil(CaptureFormatSelection.choose([format(0, width: 4032, height: 4032),
            format(1, width: 640, height: 480)], requestedFPS: 30))
    }

    func testClipTimingRetainsCapturePTSInsteadOfCallbackTime() {
        let clip = SourceVideoClip(id: UUID(), createdAt: Date(), firstCapturePTS: 100.125,
            lastCapturePTS: 101, writtenFrames: 20, writerDroppedFrames: 3, captureEvidence: nil, endedReason: "test")
        XCTAssertEqual(clip.movieTime(forCapturePTS: 100.625), 0.5, accuracy: 0.00001)
        XCTAssertEqual(clip.duration, 0.875, accuracy: 0.00001)
    }

    func testRecorderRequiresExplicitConsentAndCannotOverwriteTrial() throws {
        let recorder = SourceVideoRecorder()
        let id = UUID()
        XCTAssertThrowsError(try recorder.begin(trialID: id, consented: false))
        try recorder.begin(trialID: id, consented: true)
        XCTAssertThrowsError(try recorder.begin(trialID: UUID(), consented: true))
        recorder.finish(reason: "test")
        try SourceVideoRecorder.delete(id)
    }

    func testEmptyRecordingReportsFailureInsteadOfCreatingSuccessfulEvidence() throws {
        let recorder = SourceVideoRecorder()
        let finished = expectation(description: "empty recording fails")
        recorder.completed = { result in
            if case .success = result { XCTFail("An empty recording must not claim success") }
            finished.fulfill()
        }
        let id = UUID()
        try recorder.begin(trialID: id, consented: true)
        recorder.finish(reason: "test")
        wait(for: [finished], timeout: 2)
        XCTAssertFalse(try SourceVideoRecorder.savedClips().contains { $0.id == id })
        try SourceVideoRecorder.delete(id)
    }

    func testRecordingWritesPlayableVideoAndTimestampSidecar() async throws {
        let recorder = SourceVideoRecorder()
        let id = UUID()
        let finished = expectation(description: "video finalized")
        recorder.completed = { result in
            switch result {
            case .success(let clip):
                XCTAssertEqual(clip.id, id)
                XCTAssertGreaterThan(clip.writtenFrames, 0)
                XCTAssertEqual(clip.firstCapturePTS, 10, accuracy: 0.001)
            case .failure(let error): XCTFail(error.localizedDescription)
            }
            finished.fulfill()
        }
        try recorder.begin(trialID: id, consented: true)
        for index in 0..<12 {
            recorder.append(try sample(time: 10 + Double(index) / 30), evidence: nil)
        }
        recorder.finish(reason: "synthetic encoder test")
        await fulfillment(of: [finished], timeout: 10)
        let urls = try SourceVideoRecorder.urls(for: id)
        defer { try? SourceVideoRecorder.delete(id) }
        let asset = AVURLAsset(url: urls[0])
        let video = try await asset.loadTracks(withMediaType: .video)
        let audio = try await asset.loadTracks(withMediaType: .audio)
        XCTAssertEqual(video.count, 1)
        XCTAssertEqual(audio.count, 0)
        let metadata = try JSONDecoder().decode(SourceVideoClip.self, from: Data(contentsOf: urls[1]))
        XCTAssertEqual(metadata.id, id)
        XCTAssertLessThanOrEqual(metadata.duration, SourceVideoRecorder.maximumDuration)
    }

    private func sample(time: Double) throws -> CMSampleBuffer {
        var buffer: CVPixelBuffer?
        XCTAssertEqual(CVPixelBufferCreate(kCFAllocatorDefault, 128, 128, kCVPixelFormatType_32BGRA,
            [kCVPixelBufferIOSurfacePropertiesKey: [:]] as CFDictionary, &buffer), kCVReturnSuccess)
        let pixelBuffer = try XCTUnwrap(buffer)
        CVPixelBufferLockBaseAddress(pixelBuffer, [])
        memset(CVPixelBufferGetBaseAddress(pixelBuffer), 80, CVPixelBufferGetDataSize(pixelBuffer))
        CVPixelBufferUnlockBaseAddress(pixelBuffer, [])
        var description: CMVideoFormatDescription?
        XCTAssertEqual(CMVideoFormatDescriptionCreateForImageBuffer(allocator: kCFAllocatorDefault,
            imageBuffer: pixelBuffer, formatDescriptionOut: &description), noErr)
        var timing = CMSampleTimingInfo(duration: CMTime(value: 1, timescale: 30),
            presentationTimeStamp: CMTime(seconds: time, preferredTimescale: 60_000), decodeTimeStamp: .invalid)
        var sample: CMSampleBuffer?
        XCTAssertEqual(CMSampleBufferCreateReadyWithImageBuffer(allocator: kCFAllocatorDefault, imageBuffer: pixelBuffer,
            formatDescription: try XCTUnwrap(description), sampleTiming: &timing, sampleBufferOut: &sample), noErr)
        return try XCTUnwrap(sample)
    }

    func testExternalLatencyRequiresIndependentFrameEvidence() {
        func measurement(fps: Double = 240, start: Int = 100, end: Int = 124, clip: String = "clip-A") -> DisplayLatencyMeasurement {
            .init(id: UUID(), destination: .airPlay, referenceClip: clip, recordingFPS: fps,
                motionFrame: start, responseFrame: end, receiverAndConditions: "Apple TV / test network")
        }
        XCTAssertEqual(measurement().milliseconds!, 100, accuracy: 0.001)
        XCTAssertNil(measurement(fps: .nan).milliseconds)
        XCTAssertNil(measurement(fps: 0).milliseconds)
        XCTAssertNil(measurement(start: -1).milliseconds)
        XCTAssertNil(measurement(end: 99).milliseconds)
        XCTAssertNil(measurement(clip: " ").milliseconds)
    }
}
