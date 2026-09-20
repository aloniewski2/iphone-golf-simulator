import CoreMotion
import Foundation
import simd
import os
import QuartzCore

/// All mutable sensor/recognizer state is confined to `queue`. The only cross-queue state
/// is the locked handoff. The Sendable conformance describes this explicit confinement.
final class MotionManager: @unchecked Sendable {
    private let manager = CMMotionManager()
    private let queue: OperationQueue
    private let handoff = MotionHandoff()
    private let notify: @Sendable () -> Void
    private let signposter = OSSignposter(subsystem: "com.aloniewski.GolfArcade", category: "Motion")
    let isAvailable: Bool
    private var recognizer = SwingRecognizer()
    private var generation: UInt64 = 0
    private var lastSnapshotAt = -Double.infinity
    private var rawSamples: [MotionSample] = []
    private var rawIndex = 0
    private let diagnosticCapacity = 1_000
    private let performance: NativePerformanceAudit?

    init(performance: NativePerformanceAudit? = nil, notify: @escaping @Sendable () -> Void) {
        self.performance = performance
        self.notify = notify
        isAvailable = manager.isDeviceMotionAvailable
        queue = OperationQueue()
        queue.name = "golf.motion.serial"
        queue.qualityOfService = .userInteractive
        queue.maxConcurrentOperationCount = 1
    }

    func start() {
        queue.addOperation { [self] in
            guard isAvailable, !manager.isDeviceMotionActive else { return }
            performance?.resetSensorTimeline()
            manager.deviceMotionUpdateInterval = 1.0 / 100
            manager.startDeviceMotionUpdates(using: .xArbitraryZVertical, to: queue) { [weak self] motion, error in
                let callbackTimestamp = CACurrentMediaTime()
                guard let self else { return }
                guard let motion, error == nil else {
                    let id = self.recognizer.configuration?.id ?? UUID()
                    let event = self.recognizer.cancel(.sensorFailure) ?? .cancelled(id, .sensorFailure)
                    self.publish(event: event)
                    self.manager.stopDeviceMotionUpdates()
                    return
                }
                let q = motion.attitude.quaternion, r = motion.rotationRate, a = motion.userAcceleration
                self.ingest(MotionSample(timestamp: motion.timestamp,
                    attitude: simd_quatd(ix: q.x, iy: q.y, iz: q.z, r: q.w),
                    rotationRate: SIMD3(r.x, r.y, r.z),
                    userAcceleration: SIMD3(a.x, a.y, a.z) * 9.80665, callbackTimestamp: callbackTimestamp))
            }
        }
    }

    func arm(configuration: SwingConfiguration) {
        let next = handoff.invalidate()
        queue.addOperation { [self] in
            generation = next
            recognizer.arm(configuration)
            lastSnapshotAt = -.infinity
        }
    }

    func cancel() {
        let next = handoff.invalidate()
        queue.addOperation { [self] in
            generation = next
            _ = recognizer.cancel()
            lastSnapshotAt = -.infinity
        }
    }

    func stop() {
        let next = handoff.invalidate()
        queue.addOperation { [self] in
            generation = next
            _ = recognizer.cancel()
            manager.stopDeviceMotionUpdates()
            performance?.resetSensorTimeline()
        }
    }

    func drain() -> MotionHandoff.Batch { handoff.drain() }

    /// Diagnostic copies happen only on explicit request, never on the callback hot path.
    func diagnosticSamples() async -> [MotionSample] {
        await withCheckedContinuation { continuation in
            queue.addOperation { [self] in
                let samples = rawSamples.count < diagnosticCapacity ? rawSamples :
                    Array(rawSamples[rawIndex...]) + Array(rawSamples[..<rawIndex])
                continuation.resume(returning: samples)
            }
        }
    }

    private func ingest(_ sample: MotionSample) {
        performance?.sensorReceived(sample)
        signposter.emitEvent("SensorCallback")
        if rawSamples.count < diagnosticCapacity { rawSamples.append(sample) }
        else { rawSamples[rawIndex] = sample; rawIndex = (rawIndex + 1) % diagnosticCapacity }
        let interval = signposter.beginInterval("FilterAndRecognize")
        let filterStarted = performance?.enabled == true ? CACurrentMediaTime() : nil
        let event = recognizer.ingest(sample)
        if let filterStarted {
            performance?.record(.filterAndRecognize, milliseconds: (CACurrentMediaTime() - filterStarted) * 1_000)
        }
        signposter.endInterval("FilterAndRecognize", interval)
        publish(event: event)
    }

    private func publish(event: SwingEvent?) {
        let snapshot = recognizer.snapshot
        // Discrete events bypass throttling; continuous status is coalesced to <=30 Hz.
        let due = snapshot.timestamp - lastSnapshotAt >= 1.0 / 30
        if due { lastSnapshotAt = snapshot.timestamp }
        guard due || event != nil else { return }
        let result = handoff.publish(snapshot: due ? snapshot : nil, event: event, generation: generation)
        if result.overflow { _ = recognizer.cancel(.overflow) }
        if result.notify { notify() }
    }
}
