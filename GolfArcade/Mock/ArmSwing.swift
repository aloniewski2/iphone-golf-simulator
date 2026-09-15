import CoreGraphics
import Foundation

/// Wii-Sports-style swing reader for the camera. Only the shoulders and one hand matter.
///
/// The "power meter" is the **arm arc**: the angle of the hands around the shoulder centre, measured
/// from where they hung at address. Taking the hands back fills the meter; a full backswing is full
/// power, and going well past it is an over-swing that pulls the shot off line — exactly the Wii
/// model, where backswing length sets distance and the fore-swing only steers. Angles are
/// scale-free, so the player can stand anywhere the camera can see their shoulders.
struct ArmSwingDetector {
    enum Phase { case findingPlayer, address, backswing, downswing, finish }

    struct Sample: Equatable {
        let time: Double
        let shoulderCenter: CGPoint
        let shoulderWidth: CGFloat
        let hands: CGPoint

        init?(frame: PoseFrame) {
            guard let left = frame.point(.leftShoulder), let right = frame.point(.rightShoulder),
                  let hands = frame.handCenter else { return nil }
            time = frame.timestamp
            shoulderCenter = CGPoint(x: (left.x + right.x) / 2, y: (left.y + right.y) / 2)
            shoulderWidth = max(0.02, hypot(right.x - left.x, right.y - left.y))
            self.hands = hands
        }

        init(time: Double, shoulderCenter: CGPoint, shoulderWidth: CGFloat, hands: CGPoint) {
            self.time = time
            self.shoulderCenter = shoulderCenter
            self.shoulderWidth = shoulderWidth
            self.hands = hands
        }
    }

    /// Degrees of arc that start the backswing.
    var backswingStart = 20.0
    /// Degrees of arc shown as 100 % load. Hands level with the shoulders is about 90.
    var fullBackswing = 140.0
    /// Beyond this the meter is "in the red": the shot drifts, more the further past it goes.
    var overswing = 165.0
    /// Arc closing faster than this (deg/s) after the top is the downswing.
    var downswingSpeed = 150.0
    /// Slower peak downswings are practice swings and do not spend a shot.
    var minimumSwingSpeed = 120.0
    /// Hand-arc closing speed (deg/s) that counts as a full-speed downswing.
    var fullDownswingSpeed = 800.0
    /// Share of power that comes from downswing speed; the rest is backswing length.
    var speedWeight = 0.4
    /// Degrees of arc at which the downswing counts as impact.
    var impactAngle = 35.0
    /// Hands moving slower than this (deg/s) for `stillDuration` seconds are at rest.
    var stillSpeed = 40.0
    var stillDuration = 0.25
    /// Frames without shoulders or a hand are skipped for this long before the player is "gone".
    var trackingGracePeriod = 0.6
    /// Decides which way an over-swing hooks.
    var handedness: Handedness = .right

    private(set) var phase: Phase = .findingPlayer
    private var reference = CGVector(dx: 0, dy: -1)
    private var previous: (time: Double, arc: Double)?
    private var lastTracked: Double?
    private var stillSince: Double?
    private var peakArc = 0.0
    private var peakDownswingSpeed = 0.0

    /// Pass `nil` when the frame has no usable shoulders or hand.
    mutating func ingest(_ sample: Sample?, at time: Double) -> SwingInputEvent? {
        guard let sample else {
            let lost = time - (lastTracked ?? time)
            if phase != .findingPlayer, lost <= trackingGracePeriod { return nil }
            let wasSwinging = phase == .backswing || phase == .downswing
            reset()
            return wasSwinging ? .cancel : nil
        }
        lastTracked = time

        let vector = CGVector(dx: sample.hands.x - sample.shoulderCenter.x, dy: sample.hands.y - sample.shoulderCenter.y)
        let arc = Self.degrees(between: reference, and: vector)
        let speed = previous.map { abs(arc - $0.arc) / max(time - $0.time, 1.0 / 120) } ?? 0
        let closing = previous.map { arc < $0.arc } ?? false
        previous = (time, arc)
        if speed < stillSpeed { stillSince = stillSince ?? time } else { stillSince = nil }
        let isStill = stillSince.map { time - $0 >= stillDuration } ?? false
        let handsHangDown = vector.dy < 0

        switch phase {
        case .findingPlayer, .finish:
            guard isStill, handsHangDown else { return nil }
            settle(sample, vector: vector)
            return nil

        case .address:
            if isStill, arc < backswingStart { settle(sample, vector: vector) }
            guard arc > backswingStart else { return nil }
            phase = .backswing
            peakArc = arc
            peakDownswingSpeed = 0
            return .load(load(arc))

        case .backswing:
            peakArc = max(peakArc, arc)
            if closing, arc < peakArc - 12, speed >= downswingSpeed {
                phase = .downswing
                peakDownswingSpeed = speed
                return .load(load(peakArc))
            }
            if arc < backswingStart, speed < downswingSpeed {
                phase = .address
                return .cancel
            }
            return .load(load(arc))

        case .downswing:
            peakDownswingSpeed = max(peakDownswingSpeed, speed)
            guard arc < impactAngle else { return nil }
            phase = .finish
            stillSince = nil
            guard peakDownswingSpeed >= minimumSwingSpeed else { return .cancel }
            return .impact(power: power(arc: peakArc, downswingSpeed: peakDownswingSpeed), curve: curve(peakArc: peakArc))
        }
    }

    /// Backswing length sets most of the power; how fast the arms come through adds the rest.
    func power(arc: Double, downswingSpeed: Double) -> Double {
        let length = min(1, arc / fullBackswing)
        let velocity = min(1, downswingSpeed / fullDownswingSpeed)
        return min(1, max(0.15, length * (1 - speedWeight) + velocity * speedWeight))
    }

    /// Wii rule: past the meter the ball drifts, hooking harder the further over you go.
    func curve(peakArc: Double) -> Double {
        let hookSide: Double = handedness == .right ? -1 : 1
        return min(max(0, peakArc - overswing) * 0.6, 12) * hookSide
    }

    private func load(_ arc: Double) -> Double { min(1, max(0, arc / fullBackswing)) }

    private mutating func settle(_ sample: Sample, vector: CGVector) {
        reference = vector
        peakArc = 0
        phase = .address
    }

    private mutating func reset() {
        phase = .findingPlayer
        previous = nil
        lastTracked = nil
        stillSince = nil
        peakArc = 0
    }

    static func degrees(between a: CGVector, and b: CGVector) -> Double {
        let dot = Double(a.dx * b.dx + a.dy * b.dy)
        let magnitude = Double(hypot(a.dx, a.dy) * hypot(b.dx, b.dy))
        guard magnitude > 0 else { return 0 }
        return acos(min(1, max(-1, dot / magnitude))) * 180 / .pi
    }
}
