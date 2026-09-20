import Combine
import Foundation
import simd

/// Main-actor UI adapter; no live sensor filtering or recognition happens here.
@MainActor
final class PhoneSwingController: ObservableObject {
    enum Status: Equatable { case unavailable, idle, settling, address, backswing, downswing, followThrough }
    @Published private(set) var status: Status = .idle
    @Published private(set) var isArmed = false
    private(set) var latestSnapshot: MotionSnapshot?
    private(set) var latestMeasurement: SwingMeasurement?
    var onEvent: ((SwingInputEvent) -> Void)?
    var onMeasurement: ((SwingMeasurement) -> Void)?
    var sensitivity = 1.8
    var handedness: Handedness = .right
    var selectedAimDegrees = 0.0
    private var club: GolfClub = .driver
    private var armID: UUID?
    private(set) var isRunning = false
    private let performance: NativePerformanceAudit?
    init(performance: NativePerformanceAudit? = nil) { self.performance = performance }
    private lazy var service = MotionManager(performance: performance) { [weak self] in
        // At most one main-queue drain is pending; this is not one task per sensor sample.
        DispatchQueue.main.async { [weak self] in self?.receive() }
    }
    #if DEBUG && targetEnvironment(simulator)
    var usesTestMotion = ProcessInfo.processInfo.arguments.contains("-testPhoneMotion")
    private var replayRecognizer = SwingRecognizer()
    #endif

    var isAvailable: Bool {
        #if DEBUG && targetEnvironment(simulator)
        if usesTestMotion { return true }
        #endif
        return service.isAvailable
    }

    func start() {
        guard isAvailable else { status = .unavailable; return }
        guard !isRunning else { return }
        isRunning = true
        status = .idle
        #if DEBUG && targetEnvironment(simulator)
        if usesTestMotion { return }
        #endif
        service.start()
    }

    func stop() {
        disarm()
        service.stop()
        isRunning = false
        status = isAvailable ? .idle : .unavailable
    }

    func setClub(_ value: GolfClub) { disarm(); club = value }

    func arm() {
        guard isRunning, !isArmed, status != .followThrough else { return }
        let configuration = SwingConfiguration(club: club, handedness: handedness,
            sensitivity: sensitivity, selectedAimDegrees: selectedAimDegrees)
        armID = configuration.id
        latestMeasurement = nil
        isArmed = true
        status = .settling
        #if DEBUG && targetEnvironment(simulator)
        if usesTestMotion { replayRecognizer.arm(configuration); return }
        #endif
        service.arm(configuration: configuration)
    }

    func disarm() {
        let wasArmed = isArmed
        armID = nil; isArmed = false
        service.cancel()
        #if DEBUG && targetEnvironment(simulator)
        _ = replayRecognizer.cancel()
        #endif
        status = isAvailable ? .idle : .unavailable
        if wasArmed { onEvent?(.cancel) }
    }

    private func receive() {
        let batch = service.drain()
        consume(snapshot: batch.snapshot, events: batch.events)
    }

    private func consume(snapshot: MotionSnapshot?, events: [SwingEvent]) {
        if let snapshot, snapshot.configurationID == armID {
            latestSnapshot = snapshot
            let nextStatus: Status = switch snapshot.phase {
            case .idle: .idle
            case .settling: .settling
            case .ready: .address
            case .backswing: .backswing
            case .downswing: .downswing
            case .impact, .followThrough: .followThrough
            }
            if status != nextStatus { status = nextStatus }
            if isArmed, snapshot.phase == .backswing || snapshot.phase == .downswing {
                onEvent?(.load(snapshot.load))
            }
        }
        for event in events {
            switch event {
            case .cancelled(let id, let reason):
                guard id == armID || reason == .sensorFailure else { continue }
                isArmed = false; armID = nil
                status = reason == .sensorFailure ? .unavailable : .idle
                if reason == .sensorFailure { isRunning = false }
                onEvent?(.cancel)
            case .impact(let measurement):
                guard isArmed, measurement.configuration.id == armID else { continue }
                isArmed = false
                status = .followThrough
                latestMeasurement = measurement
                onMeasurement?(measurement)
                onEvent?(.impact(measurement.impact))
            }
        }
    }

    #if DEBUG && targetEnvironment(simulator)
    /// Same pure recognizer as the serial service. Synchronous solely for deterministic tests.
    func ingest(time: Double, attitude: simd_quatd, rotationRate: SIMD3<Double>) {
        guard usesTestMotion, isRunning else { return }
        let event = replayRecognizer.ingest(MotionSample(timestamp: time, attitude: attitude,
            rotationRate: rotationRate, userAcceleration: .zero))
        consume(snapshot: replayRecognizer.snapshot, events: event.map { [$0] } ?? [])
    }
    #endif
}
