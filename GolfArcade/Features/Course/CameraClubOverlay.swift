import SwiftUI

/// Draws the fixed ground target and measured club over the live camera. The bright ball is
/// drawn LAST so the club head cannot hide the very target the player is trying to see.
struct CameraClubOverlay: View {
    let frame: PoseFrame?
    let address: BallAddress
    let frameAspect: CGFloat
    let swingAngle: Double
    let handedness: Handedness
    let linedUp: Bool
    let strike: StrikeQuality?
    let strikeOffset: CGVector?
    let virtualClub: VirtualClubState?
    var positionLocked = true
    /// Line widths grow in the big window.
    var scale: CGFloat = 1

    var body: some View {
        Canvas { context, size in
            func screen(_ point: CGPoint) -> CGPoint {
                PoseSkeletonMapper.screenPoint(point, in: size, frameAspect: frameAspect, fitsEntireFrame: true)
            }
            let ballRadius = 7.5 * scale
            let groundPoint = screen(address.ball)
            // The frozen coordinate is the ground contact, not a floating ball center.
            let ballPoint = CGPoint(x: groundPoint.x, y: groundPoint.y - ballRadius)
            let forward = CameraPlayerStance(handedness: handedness).imageShotDirection

            // A target line runs ACROSS the stance, never along the shaft toward
            // the phone. Both the face stripe and arrow use this same direction.
            let arrowLength = min(65 * scale, max(0, forward < 0 ? ballPoint.x - 12 : size.width - ballPoint.x - 12))
            let tip = CGPoint(x: ballPoint.x + forward * arrowLength, y: ballPoint.y)
            if arrowLength > 22 * scale {
                var target = Path()
                target.move(to: CGPoint(x: ballPoint.x + forward * 13 * scale, y: ballPoint.y))
                target.addLine(to: tip)
                target.move(to: CGPoint(x: tip.x - forward * 7 * scale, y: tip.y - 4 * scale))
                target.addLine(to: tip)
                target.addLine(to: CGPoint(x: tip.x - forward * 7 * scale, y: tip.y + 4 * scale))
                context.stroke(target, with: .color(.mint), style: StrokeStyle(lineWidth: 2 * scale, lineCap: .round))
                context.draw(Text("SHOT").font(.system(size: 8 * scale, weight: .heavy)).foregroundColor(.mint),
                    at: CGPoint(x: ballPoint.x + forward * arrowLength * 0.65, y: ballPoint.y + 12 * scale))
            }

            // The club, from the hands. Its length is the calibrated hands-to-ball distance.
            if let virtualClub {
                let grip = screen(virtualClub.space.image(virtualClub.grip))
                let headPoint = screen(virtualClub.space.image(virtualClub.head))
                var shaft = Path()
                shaft.move(to: grip)
                shaft.addLine(to: headPoint)
                context.stroke(shaft, with: .color(.black.opacity(0.4)), style: StrokeStyle(lineWidth: 4 * scale, lineCap: .round))
                context.stroke(shaft, with: .color(Color(white: 0.85)), style: StrokeStyle(lineWidth: 2.2 * scale, lineCap: .round))
                let headSize = CGSize(width: 12 * scale, height: 9 * scale)
                var headContext = context
                headContext.translateBy(x: headPoint.x, y: headPoint.y)
                headContext.rotate(by: .degrees(-virtualClub.angle))
                let backX = forward < 0 ? CGFloat(0) : -headSize.width
                headContext.fill(Path(roundedRect: CGRect(x: backX, y: -headSize.height / 2, width: headSize.width, height: headSize.height), cornerRadius: 1.5 * scale), with: .color(Color(white: 0.25)))
                var face = Path()
                face.move(to: CGPoint(x: 0, y: -headSize.height / 2))
                face.addLine(to: CGPoint(x: 0, y: headSize.height / 2))
                headContext.stroke(face, with: .color(.white), lineWidth: 1.5 * scale)
                context.fill(Path(ellipseIn: CGRect(x: grip.x - 3 * scale, y: grip.y - 3 * scale, width: 6 * scale, height: 6 * scale)), with: .color(.black.opacity(0.8)))
            }

            if let strike, let strikeOffset, let virtualClub {
                let hit = screen(virtualClub.space.image(CGPoint(x: virtualClub.address.ball.x + strikeOffset.dx, y: virtualClub.address.ball.y + strikeOffset.dy)))
                let color: Color = strike == .center ? .mint : strike == .miss ? .red : .orange
                context.fill(Path(ellipseIn: CGRect(x: hit.x - 4, y: hit.y - 4, width: 8, height: 8)), with: .color(color))
            }

            // A high-contrast ground ring, ball and pointer stay legible over patterned floors.
            let ground = CGRect(x: groundPoint.x - ballRadius * 2.5, y: groundPoint.y - ballRadius * 0.7,
                                width: ballRadius * 5, height: ballRadius * 1.8)
            context.fill(Path(ellipseIn: ground), with: .color(.black.opacity(0.65)))
            context.stroke(Path(ellipseIn: ground), with: .color(.mint), lineWidth: 2 * scale)
            let ball = Path(ellipseIn: CGRect(x: ballPoint.x - ballRadius, y: ballPoint.y - ballRadius,
                                             width: ballRadius * 2, height: ballRadius * 2))
            context.fill(ball, with: .color(.white))
            context.stroke(ball, with: .color(.black), lineWidth: 1.2 * scale)
            let labelX = max(45 * scale, min(size.width - 45 * scale, ballPoint.x))
            let labelY = max(13 * scale, ballPoint.y - 31 * scale)
            let plate = CGRect(x: labelX - 43 * scale, y: labelY - 9 * scale, width: 86 * scale, height: 18 * scale)
            context.fill(Path(roundedRect: plate, cornerRadius: 5 * scale), with: .color(.black.opacity(0.85)))
            context.draw(Text(positionLocked ? "BALL LOCKED" : "FITTING CLUB")
                .font(.system(size: 9 * scale, weight: .heavy)).foregroundColor(.white),
                at: CGPoint(x: labelX, y: labelY))
            var pointer = Path()
            pointer.move(to: CGPoint(x: ballPoint.x - 3 * scale, y: ballPoint.y - 17 * scale))
            pointer.addLine(to: CGPoint(x: ballPoint.x, y: ballPoint.y - 13 * scale))
            pointer.addLine(to: CGPoint(x: ballPoint.x + 3 * scale, y: ballPoint.y - 17 * scale))
            context.stroke(pointer, with: .color(.mint), style: StrokeStyle(lineWidth: 2 * scale, lineCap: .round))
        }
        .allowsHitTesting(false)
        .accessibilityElement()
        .accessibilityLabel(positionLocked ? "Locked ball and virtual club; shot travels \(handedness == .right ? "left" : "right") across the camera view" : "Club fitting guide; hold your grip")
        .accessibilityIdentifier(positionLocked ? "lockedBallOverlay" : "clubFittingOverlay")
    }
}
