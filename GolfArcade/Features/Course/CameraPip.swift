import SwiftUI

/// Bottom-left camera view with the player's skeleton and the virtual ball they must line up with.
struct CameraPip: View {
    @ObservedObject var camera: CameraSwingController
    let ballAddress: BallAddress?
    let gesturesEnabled: Bool

    var body: some View {
        ZStack(alignment: .topTrailing) {
            Color.black
            if case .running = camera.status {
                CameraPreview(
                    session: camera.tracker.session,
                    videoRotationAngle: camera.tracker.videoRotationAngle,
                    isVideoMirrored: camera.tracker.isVideoMirrored
                )
                PoseSkeletonView(frame: camera.frame, frameAspect: camera.tracker.frameAspect, contentMode: .fit)
                if let ballAddress {
                    VirtualBallGuide(
                        address: ballAddress,
                        frameAspect: camera.tracker.frameAspect,
                        linedUp: camera.phase != .findingPlayer && !camera.needsLineUp,
                        strike: camera.lastStrike,
                        strikeOffset: camera.lastStrikeOffset
                    )
                }
            } else {
                Image(systemName: "person.crop.rectangle").font(.title2).opacity(0.5)
            }
            if let strike = camera.lastStrike {
                Text(strike.displayName.uppercased())
                    .font(.system(size: 8, weight: .black, design: .rounded))
                    .padding(.horizontal, 6).padding(.vertical, 4)
                    .background(strike == .center ? Color.mint : Color.orange, in: Capsule())
                    .foregroundStyle(Palette.ink)
                    .padding(6)
            }
            if gesturesEnabled {
                GestureFlash(camera: camera)
                    .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .bottomLeading)
                    .padding(6)
            }
        }
        .aspectRatio(camera.tracker.frameAspect, contentMode: .fit)
        .clipShape(RoundedRectangle(cornerRadius: 13))
        .overlay(RoundedRectangle(cornerRadius: 13).strokeBorder(borderColor, lineWidth: 2))
        .shadow(color: .black.opacity(0.35), radius: 8, y: 4)
        .accessibilityElement()
        .accessibilityLabel("Player camera with virtual ball")
        .accessibilityIdentifier("cameraPip")
    }

    private var borderColor: Color {
        if camera.phase == .findingPlayer { return .white.opacity(0.5) }
        return camera.needsLineUp ? .yellow : .mint
    }
}

/// A brief glyph when a gesture is recognized, and a hand icon while one is armed.
private struct GestureFlash: View {
    @ObservedObject var camera: CameraSwingController

    var body: some View {
        TimelineView(.periodic(from: .now, by: 0.15)) { timeline in
            let recent = camera.lastGesture.flatMap { timeline.date.timeIntervalSince($0.at) < 0.7 ? $0.gesture : nil }
            if let symbol = recent?.symbol ?? (camera.gestureArmed ? "hand.raised.fill" : nil) {
                Image(systemName: symbol)
                    .font(.system(size: 13, weight: .bold))
                    .frame(width: 26, height: 26)
                    .background(recent != nil ? Color.mint : .black.opacity(0.6), in: Circle())
                    .foregroundStyle(recent != nil ? Palette.ink : .mint)
            }
        }
    }
}

/// Draws the ball at the player's feet, the ring the hands must sit in at address, and where the
/// hands crossed on the last swing.
struct VirtualBallGuide: View {
    let address: BallAddress
    let frameAspect: CGFloat
    let linedUp: Bool
    let strike: StrikeQuality?
    let strikeOffset: CGVector?

    var body: some View {
        Canvas { context, size in
            func screen(_ point: CGPoint) -> CGPoint {
                PoseSkeletonMapper.screenPoint(point, in: size, frameAspect: frameAspect, fitsEntireFrame: true)
            }
            let width = address.shoulderWidth
            let ball = screen(address.ball)
            let target = screen(address.handTarget)
            let ringRadius = max(6, abs(screen(CGPoint(x: address.handTarget.x + width * BallAddress.addressTolerance, y: address.handTarget.y)).x - target.x))
            let ballRadius = max(3, ringRadius * 0.22)

            var shaft = Path()
            shaft.move(to: target)
            shaft.addLine(to: ball)
            context.stroke(shaft, with: .color(.white.opacity(0.35)), style: StrokeStyle(lineWidth: 1, dash: [3, 3]))
            context.fill(Path(ellipseIn: CGRect(x: ball.x - ballRadius, y: ball.y - ballRadius, width: ballRadius * 2, height: ballRadius * 2)), with: .color(.white))

            let ringColor: Color = linedUp ? .mint : .yellow
            context.stroke(Path(ellipseIn: CGRect(x: target.x - ringRadius, y: target.y - ringRadius, width: ringRadius * 2, height: ringRadius * 2)),
                           with: .color(ringColor.opacity(0.9)), lineWidth: 2)

            if let strike, let strikeOffset {
                let hit = screen(CGPoint(x: address.handTarget.x + strikeOffset.dx * width, y: address.handTarget.y + strikeOffset.dy * width))
                let color: Color = strike == .center ? .mint : strike == .miss ? .red : .orange
                context.fill(Path(ellipseIn: CGRect(x: hit.x - 4, y: hit.y - 4, width: 8, height: 8)), with: .color(color))
            }
        }
        .allowsHitTesting(false)
    }
}
