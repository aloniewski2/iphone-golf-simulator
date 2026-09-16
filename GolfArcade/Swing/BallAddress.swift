import CoreGraphics
import Foundation

/// The virtual ball, fixed in camera space where the player stood during their body scan.
///
/// Points are normalized Vision coordinates (origin bottom-left, y up), the same space as
/// `PoseFrame`. The camera sees hands, not a club head, so contact is judged by where the hands
/// pass `handTarget`: the spot above the ball where hands hang at a good address.
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
