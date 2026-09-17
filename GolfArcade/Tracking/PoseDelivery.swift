import Foundation
import CoreGraphics

/// One coherent Vision result, including a missing pose. Times use the host monotonic clock.
/// callbackStarted is AFTER camera delivery, so pipelineMS excludes sensor/preview/display delay.
struct PoseDelivery: Sendable {
    let frame: PoseFrame?
    let captureTime: Double
    let aspect: CGFloat
    let bodyCount: Int
    let callbackStarted: Double
    let inferenceFinished: Double
    /// Raw current-frame candidates for opt-in diagnostics; never used as unverified contact.
    var candidateFrames: [PoseFrame] = []
    var droppedFrames: Int = 0
    var poseMode: CameraPoseMode = .body2D
    var captureEvidence: CaptureEvidence?
}

#if DEBUG
/// Opt-in local joint/event trace for a recorded physical test. No camera images are stored.
/// Off on ordinary launch and absent from Release builds; bounded to 9,000 delivered frames.
final class CameraMotionTrace: @unchecked Sendable {
    struct Row: Codable {
        let pose: BenchmarkPose
        let candidates: [BenchmarkPose]
        let phase: String
        let readiness: String
        let locked: Bool
        let reviewSeconds: Int
        let angle: Double
        let event: String?
    }
    private let queue = DispatchQueue(label: "golf.camera.diagnostic-writer", qos: .utility)
    private let handle: FileHandle
    private var count = 0

    static func requested() -> CameraMotionTrace? {
        guard ProcessInfo.processInfo.arguments.contains("-recordCameraTrace") else { return nil }
        return try? CameraMotionTrace()
    }

    private init() throws {
        let url = try FileManager.default.url(for: .documentDirectory, in: .userDomainMask,
                                              appropriateFor: nil, create: true)
            .appendingPathComponent("camera-motion-trace.jsonl")
        FileManager.default.createFile(atPath: url.path, contents: nil)
        handle = try FileHandle(forWritingTo: url)
        try handle.truncate(atOffset: 0)
    }

    func record(_ row: Row) {
        guard count < 9_000 else { return }
        count += 1
        queue.async { [handle] in
            guard var data = try? JSONEncoder().encode(row) else { return }
            data.append(0x0a)
            try? handle.write(contentsOf: data)
        }
    }
}
#endif

struct CameraSwingObservation {
    let delivery: PoseDelivery
    let event: SwingInputEvent?
    let phase: String
    let stateUpdated: Double
    var readiness: String? = nil

    var pipelineMS: Double { max(0, (stateUpdated - delivery.callbackStarted) * 1_000) }
    var inferenceMS: Double { max(0, (delivery.inferenceFinished - delivery.callbackStarted) * 1_000) }
}
