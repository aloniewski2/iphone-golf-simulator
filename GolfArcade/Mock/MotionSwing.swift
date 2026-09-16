import Combine
import CoreMotion
import simd

/// Phone-as-club swing recognizer. Feed it attitude and rotation-rate samples and it reports
/// load, cancellation, and impact. It uses no CoreMotion types so unit tests can drive it with
/// synthetic swings.
///
/// Address is wherever the phone is held still. A backswing is rotation away from address; the
/// downswing starts when the phone turns back toward address quickly; impact is the moment it
/// passes back through address (or clearly decelerates). Power comes from peak rotation speed.
/// What any hands-free swing input reports to the range.
enum SwingInputEvent: Equatable {
    case load(Double)
    case cancel
    /// `curve` tilts the ball's spin axis, in degrees: a draw (negative) or fade read from the swing.
    /// `latency` is how long ago, in seconds, impact actually happened when the event was raised —
    /// a camera reports it on the next frame, so the ball is launched back-dated by this much.
    case impact(power: Double, curve: Double = 0, latency: Double = 0)
}

struct MotionSwingDetector {
    typealias Event = SwingInputEvent

    enum Phase { case settling, address, backswing, downswing, finish }

    /// Radians from address that count as the start of a backswing.
    var backswingStart = 0.25
    /// Radians of backswing shown as 100 % load.
    var fullBackswing = 2.0
    /// Rotation speed (rad/s) that starts the downswing once the phone turns back toward address.
    var downswingSpeed = 2.5
    /// Peak rotation speed (rad/s) that produces full power. Set per club.
    var fullSpeed = 14.0
    /// Slower peaks are a waggle, not a swing, and do not spend a shot.
    var minimumSpeed = 1.5
    /// Radians from address at which the downswing counts as impact.
    var impactAngle = 0.5
    /// A phone rotating slower than this for `stillDuration` seconds is at rest. Loose on purpose:
    /// address is wherever the player happens to be holding the phone, and it should arm quickly.
    var stillSpeed = 0.6
    var stillDuration = 0.2

    private(set) var phase: Phase = .settling
    private var reference = simd_quatd(ix: 0, iy: 0, iz: 0, r: 1)
    private var stillSince: Double?
    private var peakAngle = 0.0
    private var peakSpeed = 0.0
    private var downswingStart = 0.0

    mutating func ingest(time: Double, attitude: simd_quatd, rotationRate: simd_double3) -> Event? {
        let speed = simd_length(rotationRate)
        if speed < stillSpeed {
            stillSince = stillSince ?? time
        } else {
            stillSince = nil
        }
        let isStill = stillSince.map { time - $0 >= stillDuration } ?? false
        let angle = Self.angle(from: reference, to: attitude)

        switch phase {
        case .settling, .finish:
            guard isStill else { return nil }
            reference = attitude
            phase = .address
            return nil

        case .address:
            // Drift while resting re-centres address; a real backswing is far faster than `stillSpeed`.
            if isStill { reference = attitude }
            guard angle > backswingStart else { return nil }
            phase = .backswing
            peakAngle = angle
            peakSpeed = 0
            return .load(load(angle))

        case .backswing:
            peakAngle = max(peakAngle, angle)
            if angle < peakAngle - 0.15, speed >= downswingSpeed {
                phase = .downswing
                peakSpeed = speed
                downswingStart = time
                return .load(load(peakAngle))
            }
            if angle < backswingStart, speed < downswingSpeed {
                phase = .address
                return .cancel
            }
            return .load(load(angle))

        case .downswing:
            peakSpeed = max(peakSpeed, speed)
            let decelerated = speed < peakSpeed * 0.4
            guard angle < impactAngle || decelerated || time - downswingStart > 1.2 else { return nil }
            phase = .finish
            stillSince = nil
            guard peakSpeed >= minimumSpeed else { return .cancel }
            return .impact(power: min(1, peakSpeed / fullSpeed))
        }
    }

    private func load(_ angle: Double) -> Double { min(1, max(0, angle / fullBackswing)) }

    /// Total rotation, in radians, between two orientations.
    static func angle(from a: simd_quatd, to b: simd_quatd) -> Double {
        let relative = b * a.inverse
        return 2 * acos(min(1, abs(relative.real)))
    }
}

extension GolfClub {
    /// Peak phone rotation speed (rad/s) that counts as a full swing. Short clubs need less.
    var motionFullSpeed: Double {
        switch self {
        case .driver: 16
        case .iron: 13
        case .wedge: 9
        case .putter: 3
        }
    }
}

/// Streams device motion into `MotionSwingDetector` on the main actor.
@MainActor
final class PhoneSwingController: ObservableObject {
    enum Status: Equatable { case unavailable, idle, settling, address, backswing, downswing }

    @Published private(set) var status: Status = .idle
    var onEvent: ((MotionSwingDetector.Event) -> Void)?

    private let manager = CMMotionManager()
    private var detector = MotionSwingDetector()

    var isAvailable: Bool { manager.isDeviceMotionAvailable }
    var isRunning: Bool { manager.isDeviceMotionActive }

    func start() {
        guard isAvailable else { status = .unavailable; return }
        guard !isRunning else { return }
        resetDetector()
        status = .settling
        manager.deviceMotionUpdateInterval = 1.0 / 100
        manager.startDeviceMotionUpdates(using: .xArbitraryZVertical, to: .main) { [weak self] motion, _ in
            guard let motion else { return }
            // Updates are delivered on the main queue; `OperationQueue.main` is the main actor.
            MainActor.assumeIsolated { self?.ingest(motion) }
        }
    }

    func stop() {
        manager.stopDeviceMotionUpdates()
        resetDetector()
        status = isAvailable ? .idle : .unavailable
    }

    func setClub(_ club: GolfClub) {
        detector.fullSpeed = club.motionFullSpeed
    }

    private func resetDetector() {
        let fullSpeed = detector.fullSpeed
        detector = MotionSwingDetector()
        detector.fullSpeed = fullSpeed
    }

    private func ingest(_ motion: CMDeviceMotion) {
        let q = motion.attitude.quaternion
        let rate = motion.rotationRate
        let event = detector.ingest(
            time: motion.timestamp,
            attitude: simd_quatd(ix: q.x, iy: q.y, iz: q.z, r: q.w),
            rotationRate: simd_double3(rate.x, rate.y, rate.z)
        )
        status = switch detector.phase {
        case .settling, .finish: .settling
        case .address: .address
        case .backswing: .backswing
        case .downswing: .downswing
        }
        if let event { onEvent?(event) }
    }
}
