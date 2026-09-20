import Darwin
import Foundation
import QuartzCore
import os

/// Bounded samples with exact count/mean/max and a clearly labelled recent-sample percentile.
/// Sorting only happens when a report is serialized, never on the sensor/frame hot path.
struct NativeMetricSamples: Sendable {
    let capacity: Int
    private(set) var values: [Double] = []
    private(set) var count = 0
    private var cursor = 0
    private var sum = 0.0
    private var maximum = 0.0
    private var overFrameBudget = 0

    init(capacity: Int = 4_096) { self.capacity = max(1, capacity) }

    mutating func record(_ milliseconds: Double) {
        guard milliseconds.isFinite, milliseconds >= 0 else { return }
        count += 1; sum += milliseconds; maximum = max(maximum, milliseconds)
        if milliseconds > 1_000 / 60 { overFrameBudget += 1 }
        if values.count < capacity { values.append(milliseconds) }
        else { values[cursor] = milliseconds; cursor = (cursor + 1) % capacity }
    }

    struct Summary: Codable, Sendable {
        let count: Int
        let percentileSampleCount: Int
        let meanMS: Double?
        let p95MS: Double?
        let maxMS: Double?
        let countOver16_67MS: Int
    }

    var summary: Summary {
        let sorted = values.sorted()
        let index = max(0, Int(ceil(Double(sorted.count) * 0.95)) - 1)
        return Summary(count: count, percentileSampleCount: sorted.count,
            meanMS: count == 0 ? nil : sum / Double(count),
            p95MS: sorted.isEmpty ? nil : sorted[index], maxMS: count == 0 ? nil : maximum,
            countOver16_67MS: overFrameBudget)
    }
}

/// Opt in with -nativePerformanceAudit, including optimized device builds.
/// One lock bounds cross-queue counters; the serial writer owns all reports and disk work.
/// No camera, raw motion, player names, or external display identifiers are exported.
final class NativePerformanceAudit: @unchecked Sendable {
    enum Metric: String, CaseIterable, Sendable {
        case gameUpdateInterval, gameSystemsElapsed, sensorInterval, sensorDelivery
        case filterAndRecognize, callbackToImpactApplication, trajectoryPreparation, assetLoad
    }
    let enabled: Bool
    private let windowDuration: Double
    private let lock = NSLock()
    private let writer = DispatchQueue(label: "golf.native-performance.writer", qos: .utility)
    private let started = CACurrentMediaTime()
    private let sessionID = UUID().uuidString
    private var windowStart = CACurrentMediaTime()
    private var previousFrame: Double?
    private var previousSensor: Double?
    private var frameStart: Double?
    private var samples: [Metric: NativeMetricSamples] = [:]
    private var frameContexts: [String: Int] = [:]
    // Only accessed on writer.
    private var windows: [Window] = []
    private var windowsWritten = 0
    private let logger = Logger(subsystem: "com.aloniewski.GolfArcade", category: "NativePerformance")

    init(enabled: Bool = ProcessInfo.processInfo.arguments.contains("-nativePerformanceAudit"), windowDuration: Double = 5) {
        self.enabled = enabled
        self.windowDuration = max(1, windowDuration)
    }

    func record(_ metric: Metric, milliseconds: Double) {
        guard enabled else { return }
        lock.lock(); defer { lock.unlock() }
        samples[metric, default: NativeMetricSamples()].record(milliseconds)
    }

    func sensorReceived(_ sample: MotionSample) {
        guard enabled, sample.timestamp.isFinite else { return }
        lock.lock(); defer { lock.unlock() }
        if let previousSensor {
            samples[.sensorInterval, default: NativeMetricSamples()].record((sample.timestamp - previousSensor) * 1_000)
        }
        previousSensor = sample.timestamp
        if let callback = sample.callbackTimestamp {
            samples[.sensorDelivery, default: NativeMetricSamples()].record((callback - sample.timestamp) * 1_000)
        }
    }

    func resetSensorTimeline() {
        guard enabled else { return }
        lock.lock(); previousSensor = nil; lock.unlock()
    }

    #if DEBUG
    var metricsForTesting: [Metric: NativeMetricSamples.Summary] {
        lock.lock(); defer { lock.unlock() }
        return samples.mapValues(\.summary)
    }
    #endif

    func beginFrame(now: Double, running: Bool, context: String) {
        guard enabled else { return }
        lock.lock(); defer { lock.unlock() }
        guard running, now.isFinite else { previousFrame = nil; frameStart = nil; return }
        if let previousFrame {
            samples[.gameUpdateInterval, default: NativeMetricSamples()].record((now - previousFrame) * 1_000)
        }
        previousFrame = now; frameStart = now
        frameContexts[context, default: 0] += 1
    }

    func endFrame(now: Double) {
        guard enabled else { return }
        lock.lock()
        if let frameStart {
            samples[.gameSystemsElapsed, default: NativeMetricSamples()].record((now - frameStart) * 1_000)
        }
        frameStart = nil
        lock.unlock()
        flush(now: now)
    }

    func flush(now: Double = CACurrentMediaTime(), force: Bool = false) {
        guard enabled, now.isFinite else { return }
        lock.lock()
        guard now >= windowStart, force || now - windowStart >= windowDuration else { lock.unlock(); return }
        let metrics = samples, contexts = frameContexts
        let start = windowStart - started, duration = now - windowStart
        samples.removeAll(keepingCapacity: true); frameContexts.removeAll(keepingCapacity: true)
        windowStart = now
        if force { previousFrame = nil; frameStart = nil }
        lock.unlock()
        writer.async { [self] in
            let summary = Dictionary(uniqueKeysWithValues: Metric.allCases.map {
                ($0.rawValue, (metrics[$0] ?? NativeMetricSamples()).summary)
            })
            let window = Window(startSeconds: start, wallDurationSeconds: duration, metrics: summary,
                frameContexts: contexts, physicalFootprintBytes: Self.physicalFootprint(),
                thermalState: ProcessInfo.processInfo.thermalState.rawValue)
            windowsWritten += 1
            if windows.count == 720 { windows.removeFirst() }
            windows.append(window)
            do {
                let report = Report(session: sessionID, windowsWritten: windowsWritten, windows: windows)
                let encoder = JSONEncoder(); encoder.outputFormatting = [.prettyPrinted, .sortedKeys]
                let data = try encoder.encode(report)
                let folder = FileManager.default.urls(for: .documentDirectory, in: .userDomainMask)[0]
                let file = folder.appendingPathComponent("native-performance-\(sessionID).json")
                try data.write(to: file, options: .atomic)
                logger.info("Native performance report: \(file.path, privacy: .public)")
            } catch { logger.error("Performance report failed: \(error.localizedDescription, privacy: .public)") }
        }
    }

    struct Window: Codable, Sendable {
        let startSeconds: Double
        let wallDurationSeconds: Double
        let metrics: [String: NativeMetricSamples.Summary]
        let frameContexts: [String: Int]
        let physicalFootprintBytes: UInt64?
        let thermalState: Int
    }

    struct Report: Encodable {
        let schemaVersion = 1
        let session: String
        let windowsWritten: Int
        let windows: [Window]
        let operatingSystem = ProcessInfo.processInfo.operatingSystemVersionString
        let simulator = ProcessInfo.processInfo.environment["SIMULATOR_MODEL_IDENTIFIER"]
        let machine = NativePerformanceAudit.machineIdentifier()
        let appVersion = Bundle.main.object(forInfoDictionaryKey: "CFBundleShortVersionString") as? String
        #if DEBUG
        let optimized = false
        #else
        let optimized = true
        #endif
        #if NATIVE_ONLY
        let nativeOnly = true
        #else
        let nativeOnly = false
        #endif
        let notes = "Game-update intervals and elapsed time across ordered app ECS systems are not exclusive CPU, GPU-completion or display-presentation timings. Paused/loading frames are excluded. At most 720 windows are retained (normally five seconds, with partial lifecycle windows); each metric retains 4096 recent samples for p95, while count/mean/max cover the whole window. Empty measurements are unavailable, not zero. Memory is process physical footprint sampled at export, not a captured peak; thermal state is the OS raw value. Sensor delivery uses CoreMotion uptime to callback; impact latency ends after applying the accepted presentation. No physical-swing or AirPlay latency certification."
    }

    private static func machineIdentifier() -> String {
        var info = utsname(); uname(&info)
        return withUnsafeBytes(of: &info.machine) { bytes in
            String(decoding: bytes.prefix { $0 != 0 }, as: UTF8.self)
        }
    }

    private static func physicalFootprint() -> UInt64? {
        var info = task_vm_info_data_t()
        var count = mach_msg_type_number_t(MemoryLayout<task_vm_info_data_t>.size / MemoryLayout<integer_t>.size)
        let capacity = Int(count)
        let result = withUnsafeMutablePointer(to: &info) { pointer in
            pointer.withMemoryRebound(to: integer_t.self, capacity: capacity) {
                task_info(mach_task_self_, task_flavor_t(TASK_VM_INFO), $0, &count)
            }
        }
        return result == KERN_SUCCESS ? info.phys_footprint : nil
    }
}
