import Foundation

/// The recorder in the Unity plugin, through the app's runtime bridge. Not available in the simulator or before the game is on the TV
/// (the bridge reports false), which the dock shows as "ready once the game is up on the TV".
@MainActor final class LiveCaptureRuntime: CaptureRuntime {
    private var runtime: SportsRuntime { .shared() }
    var available: Bool { runtime.captureAvailable() }
    var active: Bool { runtime.captureActive() }
    func start(_ configJSON: String) -> Bool { runtime.startCapture(configJSON) }
    func stop() { runtime.stopCapture() }
    func setHandler(_ handler: (@MainActor (String) -> Void)?) {
        guard let handler else { runtime.setCaptureHandler(nil); return }
        // the bridge delivers on the main queue
        runtime.setCaptureHandler { json in MainActor.assumeIsolated { handler(json) } }
    }
    /// Voice into the recording (the voice engine calls this from its audio threads, so it is a plain C call with no actor hop).
    nonisolated static func pushVoice(_ samples: UnsafePointer<Float>, frames: Int, rate: Int, source: Int) {
        SportsRuntime.shared().captureVoice(samples, frames: Int32(frames), rate: Int32(rate), source: Int32(source))
    }
}
