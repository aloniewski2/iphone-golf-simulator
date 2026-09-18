import CoreGraphics
import Foundation

/// Both handedness modes use the same chest-facing camera placement.
/// Directions refer to the player's own body, not an unmirrored camera coordinate.
struct CameraPlayerStance {
    let handedness: Handedness
    /// Mirrored front-camera target line. The course always advances along -z;
    /// left-handed play mirrors the golfer, not the course or ball flight.
    var imageShotDirection: CGFloat { handedness == .right ? -1 : 1 }
    var title: String { handedness == .right ? "Right-handed · chest toward phone" : "Left-handed · chest toward phone" }
    var instruction: String {
        handedness == .right
            ? "Face your chest toward the phone. Take the club back to your right, then swing through to your left."
            : "Face your chest toward the phone. Take the club back to your left, then swing through to your right."
    }
}

/// Display projection of the certified address. The camera transform is locked at certification;
/// moving the player does not move the ball. The phone must remain stationary.
struct BallAddress: Equatable, Sendable {
    /// Where the ball is drawn, at the player's feet.
    let ball: CGPoint
    /// Where the hands should be at address and impact.
    let handTarget: CGPoint
    /// The player's shoulder width at the scan, for sizing the guide.
    let shoulderWidth: CGFloat

    /// Settled hands must be within this many shoulder widths of `handTarget` before a swing arms.
    static let addressTolerance = 0.45

    init(ball: CGPoint, handTarget: CGPoint, shoulderWidth: CGFloat) {
        self.ball = ball
        self.handTarget = handTarget
        self.shoulderWidth = shoulderWidth
    }

    init(calibration: PlayerCalibration, handedness: Handedness) {
        let anchor = calibration.anchor
        let width = max(calibration.signature.shoulderWidth * anchor.height, 0.02)
        // Facing a mirrored front camera, a right-hander's lead side appears on the left of the frame.
        // The ball sits a little toward the lead foot.
        let lead: Double = handedness == .right ? -1 : 1
        let x = min(max(anchor.shoulderCenterX + lead * width * 0.15, 0.05), 0.95)
        ball = CGPoint(x: x, y: min(max(anchor.shoulderCenterY - anchor.height * 0.82, 0.03), 0.95))
        handTarget = CGPoint(x: x, y: min(max(anchor.shoulderCenterY - anchor.height * 0.36, 0.05), 0.95))
        shoulderWidth = CGFloat(width)
    }

    /// Offset of `hands` from the target, in shoulder widths measured from the live frame so it
    /// does not matter how far from the camera the player is standing.
    func offset(of hands: CGPoint, shoulderWidth live: CGFloat) -> CGVector {
        let width = max(live, 0.02)
        return CGVector(dx: (hands.x - handTarget.x) / width, dy: (hands.y - handTarget.y) / width)
    }

    func isLinedUp(_ hands: CGPoint, shoulderWidth live: CGFloat) -> Bool {
        let offset = offset(of: hands, shoulderWidth: live)
        return hypot(offset.dx, offset.dy) <= Self.addressTolerance
    }
}

/// Aspect-corrected coordinates, measured in shoulder widths at certification. Once locked,
/// neither the origin nor the scale follows the player's subsequent motion.
struct SwingSpace: Equatable, Sendable {
    let shoulders: CGPoint
    let width: CGFloat
    let aspect: CGFloat

    func local(_ point: CGPoint) -> CGPoint {
        CGPoint(x: (point.x - shoulders.x) * aspect / width, y: (point.y - shoulders.y) / width)
    }

    func image(_ point: CGPoint) -> CGPoint {
        CGPoint(x: shoulders.x + point.x * width / aspect, y: shoulders.y + point.y * width)
    }
}

/// Learned from a held, comfortable grip for this player/setup, rather than scan proportions.
struct VirtualClubAddress: Equatable, Sendable {
    let grip: CGPoint
    let ball: CGPoint
}

/// Arcade contact in frozen shoulder-width units, not measured clubface geometry.
/// Uncertainty creates a retry band, never an ever-expanding automatic hit zone.
enum CameraContactPolicy {
    enum Mode { case geometric, assisted }
    enum Outcome { case hit, miss, uncertain }
    static let radius: CGFloat = 0.24

    /// Arcade strike location, not a measured physical club face. Every supported
    /// deliberate crossing connects; deviation changes efficiency rather than whiffing.
    static func assistedStrike(offset: CGVector) -> StrikeQuality {
        if hypot(offset.dx, offset.dy) <= 0.12 { return .center }
        if abs(offset.dy) >= abs(offset.dx) { return offset.dy > 0 ? .thin : .fat }
        return offset.dx > 0 ? .toe : .heel
    }

    static func classify(distance: CGFloat, confidence: Double) -> Outcome {
        guard distance.isFinite, confidence.isFinite, confidence >= 0.45, distance >= 0 else { return .uncertain }
        let uncertainty = CGFloat(0.02 + (1 - min(1, confidence)) * 0.12)
        if distance + uncertainty <= radius { return .hit }
        if distance - uncertainty > radius { return .miss }
        return .uncertain
    }
}

/// Authoritative inferred club. Camera, avatar, swept contact, and feedback use these endpoints.
struct VirtualClubState: Equatable, Sendable {
    let time: Double
    let space: SwingSpace
    let address: VirtualClubAddress
    let grip: CGPoint
    let head: CGPoint
    let angle: Double
    let confidence: Double

    init(time: Double, space: SwingSpace, address: VirtualClubAddress, grip: CGPoint, angle: Double, confidence: Double) {
        self.time = time
        self.space = space
        self.address = address
        self.grip = grip
        self.confidence = confidence
        self.angle = angle
        let a = angle * .pi / 180
        let dx = address.ball.x - address.grip.x, dy = address.ball.y - address.grip.y
        head = CGPoint(x: grip.x + dx * cos(a) - dy * sin(a), y: grip.y + dx * sin(a) + dy * cos(a))
    }

    var displayAddress: BallAddress {
        BallAddress(ball: space.image(address.ball), handTarget: space.image(address.grip), shoulderWidth: space.width / space.aspect)
    }

    struct Contact {
        let offset: CGVector
        let fraction: Double
        let startLine: Double
        let confidence: Double
        var distance: Double { hypot(offset.dx, offset.dy) }
    }

    /// Closest point of the continuous clubhead segment, not the nearest captured hand frame.
    func sweptContact(from previous: VirtualClubState, handedness: Handedness) -> Contact? {
        guard time > previous.time, time - previous.time <= 0.15 else { return nil }
        var degrees = (angle - previous.angle).truncatingRemainder(dividingBy: 360)
        if degrees > 180 { degrees -= 360 }
        if degrees < -180 { degrees += 360 }
        let radians = degrees * .pi / 180
        func rotate(_ p: CGPoint, _ a: Double) -> CGPoint {
            CGPoint(x: p.x * cos(a) - p.y * sin(a), y: p.x * sin(a) + p.y * cos(a))
        }
        // Integrate the virtual arm/shaft rotation between frames, plus measured residual grip
        // translation. A coarse straight chord would turn a centered fast swing into a thin/miss.
        let rotatedGrip = rotate(previous.grip, radians)
        let residual = CGPoint(x: grip.x - rotatedGrip.x, y: grip.y - rotatedGrip.y)
        let shaft = CGPoint(x: previous.head.x - previous.grip.x, y: previous.head.y - previous.grip.y)
        func point(at fraction: Double) -> CGPoint {
            let rotated = rotate(CGPoint(x: previous.grip.x + shaft.x, y: previous.grip.y + shaft.y), radians * fraction)
            return CGPoint(x: rotated.x + residual.x * fraction, y: rotated.y + residual.y * fraction)
        }
        let steps = max(1, Int(ceil(abs(degrees) / 3)))
        var bestFraction = 0.0
        var closest = previous.head
        var bestDistance = CGFloat.infinity
        var a = previous.head
        for step in 1...steps {
            let b = point(at: Double(step) / Double(steps))
            let dx = b.x - a.x, dy = b.y - a.y
            let lengthSquared = dx * dx + dy * dy
            let t = lengthSquared > 0.000001 ? min(1, max(0, ((address.ball.x - a.x) * dx + (address.ball.y - a.y) * dy) / lengthSquared)) : 0
            let candidate = CGPoint(x: a.x + t * dx, y: a.y + t * dy)
            let distance = hypot(candidate.x - address.ball.x, candidate.y - address.ball.y)
            if distance < bestDistance {
                bestDistance = distance
                closest = candidate
                bestFraction = (Double(step - 1) + t) / Double(steps)
            }
            a = b
        }
        let rotatedHead = rotate(previous.head, radians * bestFraction)
        let dx = -rotatedHead.y * radians + residual.x
        let dy = rotatedHead.x * radians + residual.y
        guard hypot(dx, dy) > 0.000001 else { return nil }
        let side: Double = handedness == .right ? 1 : -1
        // Signed 2D arcade estimate: depth and club-face angle cannot be recovered from this input.
        let forward = -dx * side
        let slope = atan2(dy * side, abs(dx)) * 180 / .pi
        let neutralSlope = atan2(-address.ball.x, -address.ball.y) * 180 / .pi
        let deviation = slope - (forward >= 0 ? neutralSlope : -neutralSlope)
        let line = (forward >= 0 ? 0 : 180) + min(25, max(-25, deviation))
        return Contact(offset: CGVector(dx: closest.x - address.ball.x, dy: closest.y - address.ball.y),
                       fraction: bestFraction, startLine: line, confidence: min(confidence, previous.confidence))
    }
}
