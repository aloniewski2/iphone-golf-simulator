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
    /// `lineUp`: the player is in view and still, but their hands are not over the virtual ball.
    enum Phase { case findingPlayer, lineUp, address, backswing, downswing, finish }

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
    /// The virtual ball. When set, a swing only arms with the hands over it, and contact is judged
    /// against it. Without it, contact is judged against wherever the hands settled.
    var ballAddress: BallAddress?

    private(set) var phase: Phase = .findingPlayer
    /// Where the hands are on the arc right now, in degrees: positive back, negative through.
    private(set) var swingAngle = 0.0
    private var reference = CGVector(dx: 0, dy: -1)
    private var previous: (time: Double, arc: Double)?
    private var lastTracked: Double?
    private var stillSince: Double?
    private var peakArc = 0.0
    private var peakDownswingSpeed = 0.0
    /// Live hands offset from the ball's hand target, in shoulder widths.
    private(set) var handsOffset: CGVector?
    /// Where the hands crossed the ball on the last impact, in shoulder widths.
    private(set) var lastStrikeOffset: CGVector?
    private var addressHands: CGPoint?
    private var closestImpact: (arc: Double, sample: Sample)?

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
        handsOffset = ballAddress?.offset(of: sample.hands, shoulderWidth: sample.shoulderWidth)

        let vector = CGVector(dx: sample.hands.x - sample.shoulderCenter.x, dy: sample.hands.y - sample.shoulderCenter.y)
        let arc = Self.degrees(between: reference, and: vector)
        let speed = previous.map { abs(arc - $0.arc) / max(time - $0.time, 1.0 / 120) } ?? 0
        let closing = previous.map { arc < $0.arc } ?? false
        previous = (time, arc)
        if speed < stillSpeed { stillSince = stillSince ?? time } else { stillSince = nil }
        let isStill = stillSince.map { time - $0 >= stillDuration } ?? false
        let handsHangDown = vector.dy < 0
        swingAngle = phase == .finish ? -arc : phase == .findingPlayer || phase == .lineUp ? 0 : arc

        switch phase {
        case .findingPlayer, .lineUp, .finish:
            guard isStill, handsHangDown else {
                if phase == .findingPlayer, ballAddress != nil { phase = .lineUp }
                return nil
            }
            if let ballAddress, !ballAddress.isLinedUp(sample.hands, shoulderWidth: sample.shoulderWidth) {
                phase = .lineUp
                return nil
            }
            settle(sample, vector: vector)
            return nil

        case .address:
            if isStill, arc < backswingStart {
                if let ballAddress, !ballAddress.isLinedUp(sample.hands, shoulderWidth: sample.shoulderWidth) {
                    phase = .lineUp
                    return nil
                }
                settle(sample, vector: vector)
            }
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
                closestImpact = (arc, sample)
                return .load(load(peakArc))
            }
            if arc < backswingStart, speed < downswingSpeed {
                phase = .address
                return .cancel
            }
            return .load(load(arc))

        case .downswing:
            peakDownswingSpeed = max(peakDownswingSpeed, speed)
            if closestImpact == nil || arc < closestImpact!.arc {
                closestImpact = (arc, sample)
            }
            guard let closestImpact else { return nil }
            // A fast swing can jump across address between camera frames. Wait until either the
            // hands are almost exactly back at address or their arc starts opening on the other
            // side, then use the closest captured frame as the actual contact sample.
            let reachedBall = closestImpact.arc <= 4
            let passedBall = closestImpact.arc <= impactAngle && arc > closestImpact.arc + 4
            guard reachedBall || passedBall else { return nil }
            phase = .finish
            stillSince = nil
            guard peakDownswingSpeed >= minimumSwingSpeed else { return .cancel }
            let target = ballAddress?.handTarget ?? addressHands
            if let target {
                let width = max(closestImpact.sample.shoulderWidth, 0.02)
                lastStrikeOffset = CGVector(
                    dx: (closestImpact.sample.hands.x - target.x) / width,
                    dy: (closestImpact.sample.hands.y - target.y) / width
                )
            }
            let strike = Self.strikeQuality(
                target: target,
                impact: closestImpact.sample.hands,
                shoulderWidth: closestImpact.sample.shoulderWidth,
                handedness: handedness
            )
            return .impact(
                power: power(arc: peakArc, downswingSpeed: peakDownswingSpeed),
                curve: curve(peakArc: peakArc),
                strike: strike
            )
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
        addressHands = sample.hands
        peakArc = 0
        closestImpact = nil
        phase = .address
    }

    private mutating func reset() {
        phase = .findingPlayer
        swingAngle = 0
        previous = nil
        lastTracked = nil
        stillSince = nil
        peakArc = 0
        addressHands = nil
        closestImpact = nil
        handsOffset = nil
    }

    /// Compares the hands at impact with the ball's hand target (or the address position when there
    /// is no ball). Dividing by shoulder width makes the contact window independent of camera
    /// distance and player size.
    static func strikeQuality(
        target: CGPoint?,
        impact: CGPoint,
        shoulderWidth: CGFloat,
        handedness: Handedness
    ) -> StrikeQuality {
        guard let target, shoulderWidth > 0 else { return .miss }
        let dx = Double((impact.x - target.x) / shoulderWidth)
        let dy = Double((impact.y - target.y) / shoulderWidth)
        if hypot(dx, dy) > 0.72 { return .miss }
        if dy > 0.22 { return .thin }
        if dy < -0.22 { return .fat }
        if abs(dx) > 0.24 {
            let towardToe = handedness == .right ? dx > 0 : dx < 0
            return towardToe ? .toe : .heel
        }
        return .center
    }

    static func degrees(between a: CGVector, and b: CGVector) -> Double {
        let dot = Double(a.dx * b.dx + a.dy * b.dy)
        let magnitude = Double(hypot(a.dx, a.dy) * hypot(b.dx, b.dy))
        guard magnitude > 0 else { return 0 }
        return acos(min(1, max(-1, dot / magnitude))) * 180 / .pi
    }
}
