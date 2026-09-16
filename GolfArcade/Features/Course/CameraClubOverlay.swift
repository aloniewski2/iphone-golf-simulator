import SwiftUI

/// Draws a golf ball at the player's feet, the ring their hands go in, and a club held in their
/// hands on top of the live camera. At address the club head points at the ball, so lining the
/// head up on the ball is lining up. Through the swing it follows the arms like the 3D avatar's.
struct CameraClubOverlay: View {
    let frame: PoseFrame?
    let address: BallAddress
    let frameAspect: CGFloat
    let swingAngle: Double
    let handedness: Handedness
    let linedUp: Bool
    let strike: StrikeQuality?
    let strikeOffset: CGVector?
    /// Line widths grow in the big window.
    var scale: CGFloat = 1

    var body: some View {
        Canvas { context, size in
            func screen(_ point: CGPoint) -> CGPoint {
                PoseSkeletonMapper.screenPoint(point, in: size, frameAspect: frameAspect, fitsEntireFrame: true)
            }
            let width = address.shoulderWidth
            let ballPoint = screen(address.ball)
            let target = screen(address.handTarget)
            let ringRadius = max(6, abs(screen(CGPoint(x: address.handTarget.x + width * BallAddress.addressTolerance, y: address.handTarget.y)).x - target.x))
            let ballRadius = max(3.5, ringRadius * 0.2)

            // Ball and its shadow.
            context.fill(Path(ellipseIn: CGRect(x: ballPoint.x - ballRadius * 1.3, y: ballPoint.y + ballRadius * 0.5, width: ballRadius * 2.6, height: ballRadius * 0.9)), with: .color(.black.opacity(0.35)))
            context.fill(Path(ellipseIn: CGRect(x: ballPoint.x - ballRadius, y: ballPoint.y - ballRadius, width: ballRadius * 2, height: ballRadius * 2)), with: .color(.white))

            // Where the hands go.
            let ringColor: Color = linedUp ? .mint : .yellow
            context.stroke(Path(ellipseIn: CGRect(x: target.x - ringRadius, y: target.y - ringRadius, width: ringRadius * 2, height: ringRadius * 2)),
                           with: .color(ringColor.opacity(0.85)), style: StrokeStyle(lineWidth: 1.5 * scale, dash: linedUp ? [] : [5, 4]))

            // The club, from the hands. Its length is the calibrated hands-to-ball distance.
            if let frame, let hands = frame.handCenter, let shoulders = frame.shoulderCenter {
                func corrected(_ p: CGPoint) -> CGPoint { CGPoint(x: p.x * frameAspect, y: p.y) }
                let length = hypot((address.ball.x - address.handTarget.x) * frameAspect, address.ball.y - address.handTarget.y)
                let direction = ClubGeometry.direction2D(
                    hands: corrected(hands), shoulders: corrected(shoulders), ball: corrected(address.ball),
                    swingAngle: swingAngle, handedness: handedness
                )
                let head = CGPoint(x: hands.x + direction.dx * length / frameAspect, y: hands.y + direction.dy * length)
                let grip = screen(hands)
                let headPoint = screen(head)
                var shaft = Path()
                shaft.move(to: grip)
                shaft.addLine(to: headPoint)
                context.stroke(shaft, with: .color(.black.opacity(0.4)), style: StrokeStyle(lineWidth: 4 * scale, lineCap: .round))
                context.stroke(shaft, with: .color(Color(white: 0.85)), style: StrokeStyle(lineWidth: 2.2 * scale, lineCap: .round))
                let headSize = CGSize(width: 9 * scale, height: 5 * scale)
                let angle = Angle(radians: atan2(Double(headPoint.y - grip.y), Double(headPoint.x - grip.x)))
                var headContext = context
                headContext.translateBy(x: headPoint.x, y: headPoint.y)
                headContext.rotate(by: angle + .degrees(90))
                headContext.fill(Path(roundedRect: CGRect(x: -headSize.width / 2, y: -headSize.height / 2, width: headSize.width, height: headSize.height), cornerRadius: 1.5 * scale), with: .color(Color(white: 0.25)))
                context.fill(Path(ellipseIn: CGRect(x: grip.x - 3 * scale, y: grip.y - 3 * scale, width: 6 * scale, height: 6 * scale)), with: .color(.black.opacity(0.8)))
            }

            if let strike, let strikeOffset {
                let hit = screen(CGPoint(x: address.handTarget.x + strikeOffset.dx * width, y: address.handTarget.y + strikeOffset.dy * width))
                let color: Color = strike == .center ? .mint : strike == .miss ? .red : .orange
                context.fill(Path(ellipseIn: CGRect(x: hit.x - 4, y: hit.y - 4, width: 8, height: 8)), with: .color(color))
            }
        }
        .allowsHitTesting(false)
    }
}
