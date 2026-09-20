import Foundation
import simd

/// All sensor values use SI units; timestamps are CoreMotion's monotonic seconds.
struct MotionSample: Sendable {
    let timestamp: Double
    let attitude: simd_quatd
    let rotationRate: SIMD3<Double>
    let userAcceleration: SIMD3<Double>
    let callbackTimestamp: Double?

    init(timestamp: Double, attitude: simd_quatd, rotationRate: SIMD3<Double>,
         userAcceleration: SIMD3<Double>, callbackTimestamp: Double? = nil) {
        self.timestamp = timestamp; self.attitude = attitude
        self.rotationRate = rotationRate; self.userAcceleration = userAcceleration
        self.callbackTimestamp = callbackTimestamp
    }

    var isValid: Bool {
        timestamp.isFinite && attitude.vector.allFinite && rotationRate.allFinite &&
        userAcceleration.allFinite && abs(simd_length(attitude.vector) - 1) < 0.02
    }
}

private extension SIMD3<Double> {
    var allFinite: Bool { x.isFinite && y.isFinite && z.isFinite }
}
private extension SIMD4<Double> {
    var allFinite: Bool { x.isFinite && y.isFinite && z.isFinite && w.isFinite }
}

struct SwingConfiguration: Sendable {
    let id: UUID
    let club: GolfClub
    let handedness: Handedness
    let sensitivity: Double
    let selectedAimDegrees: Double

    init(id: UUID = UUID(), club: GolfClub, handedness: Handedness = .right,
         sensitivity: Double = 1.8, selectedAimDegrees: Double = 0) {
        self.id = id
        self.club = club
        self.handedness = handedness
        self.sensitivity = sensitivity.isFinite ? min(3, max(1, sensitivity)) : 1.8
        self.selectedAimDegrees = selectedAimDegrees.isFinite ? selectedAimDegrees : 0
    }
}

struct SwingMeasurement: Sendable {
    let configuration: SwingConfiguration
    let backswingTimestamp: Double
    let downswingTimestamp: Double
    let impactTimestamp: Double
    let peakAngularSpeed: Double
    let addressRelativeOrientation: simd_quatd
    let estimatedSwingDirection: SIMD3<Double>
    let confidence: Double
    var callbackTimestamp: Double? = nil
    var handedness: Handedness { configuration.handedness }

    /// Assisted direction is an estimate of the swing plane, never a measured club-face angle.
    var impact: SwingImpact {
        let direction = estimatedSwingDirection
        let offset = atan2(direction.x, max(0.001, abs(direction.z))) * 180 / .pi
        let limit = configuration.club == .putter ? 2.0 : 8.0
        return SwingImpact(
            power: min(1, peakAngularSpeed * configuration.sensitivity / configuration.club.motionFullSpeed),
            startLineDegrees: min(limit, max(-limit, offset)),
            confidence: confidence, source: .phone)
    }
}

enum SwingCancellation: String, Sendable {
    case explicit, invalidSample, sampleGap, timeout, abortedBackswing, overflow, sensorFailure
}

enum SwingEvent: Sendable {
    case impact(SwingMeasurement)
    case cancelled(UUID, SwingCancellation)
}

enum MotionPhase: String, Sendable {
    case idle, settling, ready, backswing, downswing, impact, followThrough
}

struct MotionSnapshot: Sendable {
    let timestamp: Double
    let phase: MotionPhase
    let load: Double
    let relativeOrientation: simd_quatd
    let angularSpeed: Double
    let configurationID: UUID?
}

/// Pure value type used by both the serial sensor queue and recorded/synthetic replay tests.
struct SwingRecognizer: Sendable {
    private(set) var phase: MotionPhase = .idle
    private(set) var configuration: SwingConfiguration?
    private(set) var snapshot = MotionSnapshot(timestamp: 0, phase: .idle, load: 0,
        relativeOrientation: simd_quatd(ix: 0, iy: 0, iz: 0, r: 1), angularSpeed: 0, configurationID: nil)
    private var previousTimestamp: Double?
    private var filteredRate = SIMD3<Double>.zero
    private var filteredAcceleration = SIMD3<Double>.zero
    private var address = simd_quatd(ix: 0, iy: 0, iz: 0, r: 1)
    private var stableAttitude: simd_quatd?
    private var stableSince: Double?
    private var peakAngle = 0.0
    private var peakSpeed = 0.0
    private var previousRelative = SIMD3<Double>.zero
    private var backswingAt = 0.0
    private var downswingAt = 0.0
    private var impactAt = 0.0
    var cutoffHz = 12.0

    mutating func arm(_ configuration: SwingConfiguration) {
        self = SwingRecognizer()
        self.configuration = configuration
        phase = .settling
    }

    mutating func cancel(_ reason: SwingCancellation = .explicit) -> SwingEvent? {
        let old = configuration
        let unfinished = phase != .idle
        self = SwingRecognizer()
        return unfinished ? old.map { .cancelled($0.id, reason) } : nil
    }

    mutating func ingest(_ sample: MotionSample) -> SwingEvent? {
        guard sample.isValid else { return cancel(.invalidSample) }
        let dt = previousTimestamp.map { sample.timestamp - $0 }
        if let dt, dt <= 0 || dt > 0.100_001 { return cancel(dt <= 0 ? .invalidSample : .sampleGap) }
        previousTimestamp = sample.timestamp
        if let dt {
            let alpha = 1 - exp(-2 * Double.pi * cutoffHz * dt)
            filteredRate += alpha * (sample.rotationRate - filteredRate)
            filteredAcceleration += alpha * (sample.userAcceleration - filteredAcceleration)
        } else {
            filteredRate = sample.rotationRate
            filteredAcceleration = sample.userAcceleration
        }
        guard let configuration, phase != .idle else { return nil }
        let putt = configuration.club == .putter
        let startAngle = putt ? 0.035 : 0.25
        let impactRegion = putt ? 0.025 : 0.15
        let stillSpeed = putt ? 0.04 : 0.6
        let speed = simd_length(filteredRate)
        var relative = address.inverse * sample.attitude
        if relative.real < 0 { relative = simd_quatd(vector: -relative.vector) }
        let angle = 2 * acos(min(1, max(-1, relative.real)))
        let vector = simd_length(relative.imag) > 0.000_001 ? simd_normalize(relative.imag) * angle : .zero
        defer {
            previousRelative = vector
            snapshot = MotionSnapshot(timestamp: sample.timestamp, phase: phase,
                load: min(1, angle / (putt ? 0.6 : 2)), relativeOrientation: relative,
                angularSpeed: speed, configurationID: configuration.id)
        }

        if phase == .impact { phase = .followThrough }
        if phase == .followThrough {
            if sample.timestamp - impactAt >= 4 || (sample.timestamp - impactAt >= 1 && speed < stillSpeed) {
                phase = .idle
                self.configuration = nil
            }
            return nil
        }
        if (phase == .backswing || phase == .downswing), sample.timestamp - backswingAt > 8 {
            return cancel(.timeout)
        }
        if phase == .settling {
            if speed < stillSpeed, simd_length(filteredAcceleration) < 0.5 {
                if let stableAttitude,
                   Self.angle(stableAttitude, sample.attitude) > (putt ? 0.015 : 0.04) {
                    stableSince = sample.timestamp
                    self.stableAttitude = sample.attitude
                } else if stableSince == nil {
                    stableSince = sample.timestamp
                    stableAttitude = sample.attitude
                }
                if sample.timestamp - (stableSince ?? sample.timestamp) >= 0.2 {
                    address = sample.attitude
                    phase = .ready
                }
            } else { stableSince = nil; stableAttitude = nil }
            return nil
        }
        if phase == .ready {
            guard angle > startAngle else { return nil }
            phase = .backswing
            backswingAt = sample.timestamp
            peakAngle = angle
            peakSpeed = 0
            return nil
        }
        if phase == .backswing {
            peakAngle = max(peakAngle, angle)
            if angle < peakAngle - min(0.15, startAngle * 0.6), speed >= (putt ? 0.12 : 2.5) {
                phase = .downswing
                downswingAt = sample.timestamp
                peakSpeed = speed
            } else if angle < startAngle, speed < (putt ? 0.12 : 2.5) {
                return cancel(.abortedBackswing)
            } else { return nil }
        }
        guard phase == .downswing else { return nil }
        peakSpeed = max(peakSpeed, speed)
        // A swept crossing catches a fast return that passes address between 60 Hz samples.
        let delta = vector - previousRelative
        let lengthSquared = simd_length_squared(delta)
        let t = lengthSquared > 0 ? min(1, max(0, -simd_dot(previousRelative, delta) / lengthSquared)) : 0
        let nearest = simd_length(previousRelative + delta * t)
        let approaching = angle < simd_length(previousRelative) || simd_dot(previousRelative, vector) <= 0
        guard approaching, nearest <= impactRegion, peakSpeed >= (putt ? 0.10 : 1.5) else { return nil }
        phase = .impact
        impactAt = sample.timestamp
        // Express the angular axis in address space. A lever pointing down produces the
        // estimated horizontal path; reverse left-handed swings into the same aim convention.
        let rateAtAddress = relative.act(filteredRate)
        var direction = simd_cross(rateAtAddress, SIMD3<Double>(0, -1, 0))
        if configuration.handedness == .left { direction = -direction }
        if direction.z < 0 { direction = -direction }
        direction = simd_length(direction) > 0.0001 ? simd_normalize(direction) : SIMD3(0, 0, 1)
        return .impact(SwingMeasurement(configuration: configuration,
            backswingTimestamp: backswingAt, downswingTimestamp: downswingAt,
            impactTimestamp: sample.timestamp, peakAngularSpeed: peakSpeed,
            addressRelativeOrientation: relative, estimatedSwingDirection: direction, confidence: 0.9,
            callbackTimestamp: sample.callbackTimestamp))
    }

    static func angle(_ a: simd_quatd, _ b: simd_quatd) -> Double {
        2 * acos(min(1, abs((a.inverse * b).real)))
    }
}

/// Bounded handoff. A generation fence invalidates queued events synchronously on cancel.
/// NSLock protects every field; nothing is published while the lock is held.
final class MotionHandoff: @unchecked Sendable {
    struct Batch: Sendable { let snapshot: MotionSnapshot?; let events: [SwingEvent] }
    private let lock = NSLock()
    private var generation: UInt64 = 0
    private var snapshot: MotionSnapshot?
    private var events: [SwingEvent] = []
    private var scheduled = false
    private let capacity: Int
    init(capacity: Int = 16) { self.capacity = max(1, capacity) }

    func invalidate() -> UInt64 {
        lock.lock(); defer { lock.unlock() }
        generation &+= 1
        snapshot = nil; events.removeAll(keepingCapacity: true)
        return generation
    }

    /// Returns whether the producer needs to schedule a drain, and whether it must disarm.
    func publish(snapshot: MotionSnapshot?, event: SwingEvent?, generation: UInt64) -> (notify: Bool, overflow: Bool) {
        lock.lock(); defer { lock.unlock() }
        guard generation == self.generation else { return (false, false) }
        if let snapshot { self.snapshot = snapshot }
        var overflow = false
        if let event {
            if events.count >= capacity {
                let id: UUID
                switch event { case .impact(let m): id = m.configuration.id; case .cancelled(let value, _): id = value }
                events = [.cancelled(id, .overflow)]
                self.snapshot = nil
                overflow = true
            } else { events.append(event) }
        }
        let notify = !scheduled && (self.snapshot != nil || !events.isEmpty)
        if notify { scheduled = true }
        return (notify, overflow)
    }

    func drain() -> Batch {
        lock.lock(); defer { lock.unlock() }
        let result = Batch(snapshot: snapshot, events: events)
        snapshot = nil; events.removeAll(keepingCapacity: true); scheduled = false
        return result
    }
}
