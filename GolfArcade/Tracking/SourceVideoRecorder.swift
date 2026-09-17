@preconcurrency import AVFoundation
import Foundation

struct SourceVideoClip: Codable, Identifiable, Equatable, Sendable {
    let id: UUID // Same identifier as the benchmark trial.
    let createdAt: Date
    let firstCapturePTS: Double
    let lastCapturePTS: Double
    let writtenFrames: Int
    let writerDroppedFrames: Int
    let captureEvidence: CaptureEvidence?
    let endedReason: String
    var duration: Double { max(0, lastCapturePTS - firstCapturePTS) }

    /// Both times are sample-buffer PTS, not callback wall time. Pose traces retain
    /// their own origin; therefore missing/dropped video frames never shift labels.
    func movieTime(forCapturePTS time: Double) -> Double { time - firstCapturePTS }
}

/// Confined to CameraPoseTracker.visionQueue. Video is opt-in, local, silent,
/// duration-bounded, and built from the exact oriented buffers fed to Vision.
/// No camera buffers are retained in an unbounded queue.
final class SourceVideoRecorder {
    /// Ownership is transferred to finishWriting's completion. No other code uses
    /// this writer after transfer; AVFoundation invokes completion after finalization.
    private final class FinishingWriter: @unchecked Sendable {
        let value: AVAssetWriter
        init(_ value: AVAssetWriter) { self.value = value }
    }
    static let maximumDuration = 12.0
    static let maximumSavedClips = 50
    private var id: UUID?
    private var writer: AVAssetWriter?
    private var input: AVAssetWriterInput?
    private var firstPTS: CMTime?
    private var lastPTS: CMTime?
    private var evidence: CaptureEvidence?
    private var frameCount = 0
    private var dropped = 0
    var completed: (@Sendable (Result<SourceVideoClip, RecordingError>) -> Void)?

    struct RecordingError: Error, LocalizedError, Sendable {
        let message: String
        var errorDescription: String? { message }
    }

    static func directory() throws -> URL {
        let parent = try FileManager.default.url(for: .applicationSupportDirectory, in: .userDomainMask,
            appropriateFor: nil, create: true)
        var directory = parent.appendingPathComponent("GolfResearchClips", isDirectory: true)
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        var values = URLResourceValues()
        values.isExcludedFromBackup = true
        try directory.setResourceValues(values)
        return directory
    }

    static func urls(for id: UUID) throws -> [URL] {
        let root = try directory()
        return [root.appendingPathComponent(id.uuidString + ".mov"), root.appendingPathComponent(id.uuidString + ".json"),
                root.appendingPathComponent(id.uuidString + ".trial.json")]
    }

    static func savedClips() throws -> [SourceVideoClip] {
        try FileManager.default.contentsOfDirectory(at: directory(), includingPropertiesForKeys: nil)
            .filter { $0.pathExtension == "json" }
            .compactMap { try? JSONDecoder().decode(SourceVideoClip.self, from: Data(contentsOf: $0)) }
            .sorted { $0.createdAt > $1.createdAt }
    }

    static func delete(_ id: UUID) throws {
        // UUID-derived children only; no supplied path or broad directory deletion.
        for url in try urls(for: id) where FileManager.default.fileExists(atPath: url.path) {
            try FileManager.default.removeItem(at: url)
        }
    }

    func begin(trialID: UUID, consented: Bool) throws {
        guard consented else { throw RecordingError(message: "Explicit source-video consent is required.") }
        guard id == nil else { throw RecordingError(message: "A recording is already active.") }
        guard try Self.savedClips().count < Self.maximumSavedClips else {
            throw RecordingError(message: "50 local clips saved. Export and delete clips before recording more.")
        }
        let paths = try Self.urls(for: trialID)
        guard paths.allSatisfy({ !FileManager.default.fileExists(atPath: $0.path) }) else {
            throw RecordingError(message: "This trial already has a recording; it will not be overwritten.")
        }
        id = trialID
        writer = nil; input = nil; firstPTS = nil; lastPTS = nil
        evidence = nil; frameCount = 0; dropped = 0
    }

    func append(_ sample: CMSampleBuffer, evidence currentEvidence: CaptureEvidence?) {
        guard let id else { return }
        let pts = CMSampleBufferGetPresentationTimeStamp(sample)
        guard pts.isNumeric, let buffer = CMSampleBufferGetImageBuffer(sample) else { return }
        if let firstPTS, pts.seconds - firstPTS.seconds >= Self.maximumDuration {
            finish(reason: "12-second recording limit")
            return
        }
        if let evidence, evidence.geometry != currentEvidence?.geometry {
            finish(reason: "Capture geometry changed; trial must be repeated")
            return
        }
        do {
            if writer == nil {
                let writer = try AVAssetWriter(outputURL: Self.urls(for: id)[0], fileType: .mov)
                let input = AVAssetWriterInput(mediaType: .video, outputSettings: [
                    AVVideoCodecKey: AVVideoCodecType.h264,
                    AVVideoWidthKey: CVPixelBufferGetWidth(buffer),
                    AVVideoHeightKey: CVPixelBufferGetHeight(buffer),
                    AVVideoCompressionPropertiesKey: [AVVideoAverageBitRateKey: 4_000_000]
                ])
                input.expectsMediaDataInRealTime = true
                guard writer.canAdd(input) else { throw RecordingError(message: "Video encoder cannot accept this camera format.") }
                writer.add(input)
                guard writer.startWriting() else { throw writer.error ?? RecordingError(message: "Video encoder did not start.") }
                writer.startSession(atSourceTime: pts)
                self.writer = writer; self.input = input; firstPTS = pts
                evidence = currentEvidence
            }
            guard let writer, let input else { return }
            guard writer.status == .writing else { throw writer.error ?? RecordingError(message: "Video encoding failed.") }
            guard lastPTS.map({ pts > $0 }) ?? true else { dropped += 1; return }
            guard input.isReadyForMoreMediaData else { dropped += 1; return }
            guard input.append(sample) else { throw writer.error ?? RecordingError(message: "Video frame could not be encoded.") }
            lastPTS = pts
            frameCount += 1
        } catch {
            writer?.cancelWriting()
            self.id = nil
            try? Self.delete(id)
            completed?(.failure(RecordingError(message: error.localizedDescription)))
        }
    }

    func finish(reason: String) {
        guard let id else { return }
        guard let writer, let input, let firstPTS, let lastPTS, frameCount > 0 else {
            self.id = nil
            writer?.cancelWriting()
            try? Self.delete(id)
            completed?(.failure(RecordingError(message: "No camera frames were recorded.")))
            return
        }
        let clip = SourceVideoClip(id: id, createdAt: Date(), firstCapturePTS: firstPTS.seconds,
            lastCapturePTS: lastPTS.seconds, writtenFrames: frameCount, writerDroppedFrames: dropped,
            captureEvidence: evidence, endedReason: reason)
        self.id = nil
        // Completion runs off the capture queue. A finishing writer is retained by
        // its callback; a later clip owns a different writer and unique file.
        input.markAsFinished()
        let completed = completed
        let finishing = FinishingWriter(writer)
        writer.finishWriting {
            do {
                let writer = finishing.value
                guard writer.status == .completed else { throw writer.error ?? RecordingError(message: "Recording could not finish.") }
                let metadata = try JSONEncoder().encode(clip)
                try metadata.write(to: Self.urls(for: id)[1], options: .atomic)
                completed?(.success(clip))
            } catch {
                try? Self.delete(id)
                completed?(.failure(RecordingError(message: error.localizedDescription)))
            }
        }
        self.writer = nil; self.input = nil
    }
}
