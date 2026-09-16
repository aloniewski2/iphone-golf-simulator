import SwiftUI

/// The live camera: a big window while the player lines up over the ball, then a small corner
/// view once they have held address. The skeleton is only a faint aid while finding the player;
/// during a swing the view shows just the drawn club and ball.
struct CameraStage: View {
    @ObservedObject var camera: CameraSwingController
    let ballAddress: BallAddress?
    let handedness: Handedness
    let expanded: Bool
    let gesturesEnabled: Bool

    var body: some View {
        VStack(spacing: expanded ? 14 : 0) {
            if expanded { prompt }
            feed
        }
        .accessibilityElement(children: .contain)
        .accessibilityLabel("Player camera")
        .accessibilityValue(expanded ? "expanded" : "minimized")
        .accessibilityIdentifier("cameraStage")
    }

    private var feed: some View {
        ZStack(alignment: .topTrailing) {
            Color.black
            if case .running = camera.status {
                CameraPreview(
                    session: camera.tracker.session,
                    videoRotationAngle: camera.tracker.videoRotationAngle,
                    isVideoMirrored: camera.tracker.isVideoMirrored
                )
                if expanded, camera.phase == .findingPlayer || camera.needsLineUp {
                    PoseSkeletonView(frame: camera.frame, frameAspect: camera.tracker.frameAspect, contentMode: .fit)
                        .opacity(0.35)
                }
                if let ballAddress {
                    CameraClubOverlay(
                        frame: camera.frame,
                        address: ballAddress,
                        frameAspect: camera.tracker.frameAspect,
                        swingAngle: camera.swingAngle,
                        handedness: handedness,
                        linedUp: camera.phase != .findingPlayer && !camera.needsLineUp,
                        strike: camera.lastStrike,
                        strikeOffset: camera.lastStrikeOffset,
                        scale: expanded ? 2.2 : 1
                    )
                }
            } else {
                VStack(spacing: 8) {
                    Image(systemName: "person.crop.rectangle").font(expanded ? .largeTitle : .title2)
                    if expanded { Text(statusText).font(.subheadline.bold()) }
                }
                .opacity(0.6)
                .frame(maxWidth: .infinity, maxHeight: .infinity)
            }
            if let strike = camera.lastStrike, !expanded {
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
        .clipShape(RoundedRectangle(cornerRadius: expanded ? 22 : 13))
        .overlay(RoundedRectangle(cornerRadius: expanded ? 22 : 13).strokeBorder(borderColor, lineWidth: expanded ? 3 : 2))
        .shadow(color: .black.opacity(0.35), radius: 8, y: 4)
    }

    private var prompt: some View {
        HStack(spacing: 12) {
            ZStack {
                Circle().stroke(.white.opacity(0.2), lineWidth: 5)
                Circle().trim(from: 0, to: camera.readyProgress)
                    .stroke(Color.mint, style: StrokeStyle(lineWidth: 5, lineCap: .round))
                    .rotationEffect(.degrees(-90))
                    .animation(.linear(duration: 0.1), value: camera.readyProgress)
                Image(systemName: camera.readyProgress > 0 ? "figure.golf" : "scope").font(.headline)
            }
            .frame(width: 44, height: 44)
            VStack(alignment: .leading, spacing: 2) {
                Text(title).font(.headline)
                Text(detail).font(.caption).foregroundStyle(.white.opacity(0.7))
            }
            Spacer(minLength: 0)
        }
        .padding(.horizontal, 16).padding(.vertical, 10)
        .background(.black.opacity(0.65), in: RoundedRectangle(cornerRadius: 18))
        .accessibilityElement(children: .combine)
        .accessibilityIdentifier("readyPrompt")
    }

    private var statusText: String {
        switch camera.status {
        case .denied: "Camera access is off"
        case .unavailable(let reason): reason
        default: "Starting camera"
        }
    }

    private var title: String {
        if camera.readyProgress > 0 { return "Hold it…" }
        if camera.phase == .findingPlayer { return "Step into view" }
        return camera.needsLineUp ? "Put the club on the ball" : "Hold still over the ball"
    }

    private var detail: String {
        camera.readyProgress > 0 ? "Getting ready to play" : "Your hands go in the ring; the club head shows where the ball is"
    }

    private var borderColor: Color {
        if camera.phase == .findingPlayer { return .white.opacity(0.5) }
        return camera.needsLineUp ? .yellow : .mint
    }
}

/// A brief glyph when a gesture is recognized, and a hand icon while one is armed.
struct GestureFlash: View {
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
