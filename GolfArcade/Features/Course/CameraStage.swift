import SwiftUI

/// Presentation-only crop. Camera pixels and overlay coordinates share this transform;
/// the detector's frozen ball position and collision geometry never change.
struct CameraBallFocus: Equatable {
    let scale: CGFloat
    let offset: CGSize

    init(ball: CGPoint?, size: CGSize, active: Bool) {
        guard active, let ball, size.width > 0, size.height > 0 else {
            scale = 1
            offset = .zero
            return
        }
        scale = 1.85
        let x = min(1, max(0, ball.x))
        let y = 1 - min(1, max(0, ball.y)) // Vision coordinates have their origin at the bottom.
        let limitX = size.width * (scale - 1) / 2
        let limitY = size.height * (scale - 1) / 2
        offset = CGSize(
            width: min(limitX, max(-limitX, (0.5 - x) * size.width * scale)),
            height: min(limitY, max(-limitY, (0.67 - 0.5 - (y - 0.5) * scale) * size.height))
        )
    }
}

/// The live camera: a big window while the player lines up over the ball, then a small corner
/// view once they have held address. The skeleton is only a faint aid while finding the player;
/// during a swing the view shows just the drawn club and ball.
struct CameraStage: View {
    @ObservedObject var camera: CameraSwingController
    let ballAddress: BallAddress?
    let handedness: Handedness
    let expanded: Bool
    let viewportSize: CGSize
    let gesturesEnabled: Bool
    var previewOnTV = false
    var onResize: () -> Void = {}
    var onRecenter: () -> Void = {}

    var body: some View {
        // Keep ONE preview at the same structural identity while resizing. Branching between
        // two copies of `feed` tears down/reconnects AVFoundation during the transition.
        let aspect = max(camera.tracker.frameAspect, 0.3)
        let width = min(viewportSize.width, viewportSize.height * aspect)
        let imageSize = CGSize(width: width, height: width / aspect)
        let focus = CameraBallFocus(ball: ballAddress?.ball, size: imageSize, active: showsGroundCloseUp)
        return ZStack {
            // Sizes come from the final layout, not an animating GeometryReader. Reading the
            // intermediate animated size and animating it again creates a chasing transition.
            ZStack {
                Color.black
                ZStack {
                    feed
                        .frame(width: imageSize.width, height: imageSize.height)
                        .scaleEffect(focus.scale)
                        .offset(focus.offset)
                        .animation(.easeInOut(duration: 0.45), value: focus)
                }
                    .frame(width: imageSize.width, height: imageSize.height)
                    .clipped()
                    .clipShape(RoundedRectangle(cornerRadius: expanded ? 22 : 13))
                    .overlay(RoundedRectangle(cornerRadius: expanded ? 22 : 13).strokeBorder(borderColor, lineWidth: expanded ? 3 : 2))
                    .shadow(color: .black.opacity(0.35), radius: 8, y: 4)
                    .onTapGesture { if !expanded { onResize() } }
            }
            .frame(width: viewportSize.width, height: viewportSize.height)
            .overlay(alignment: .top) { if expanded { prompt.padding(16) } }
        }
        .accessibilityElement(children: .contain)
        .accessibilityLabel(showsGroundCloseUp ? "Player camera, ground close-up" : "Player camera, full frame")
        .accessibilityValue(expanded ? "expanded" : "minimized")
        .accessibilityIdentifier("cameraStage")
    }

    private var feed: some View {
        ZStack(alignment: .topTrailing) {
            Color.black
            // Keep the layer attached while the session starts or recovers, too.
            if previewOnTV {
                Text("Camera and ball shown on TV").font(.headline).padding()
                    .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .bottom)
            } else {
            CameraPreview(
                session: camera.tracker.session,
                videoRotationAngle: camera.tracker.videoRotationAngle,
                isVideoMirrored: camera.tracker.isVideoMirrored,
                videoGravity: .resizeAspect
            )
            }
            if case .running = camera.status {
                if expanded, camera.phase == .findingPlayer || camera.needsLineUp || camera.isSyntheticPreview {
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
                        linedUp: camera.hasPlayableTracking,
                        strike: camera.lastStrike,
                        strikeOffset: camera.lastStrikeOffset,
                        virtualClub: camera.virtualClub,
                        positionLocked: camera.isPositionLocked,
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
            if camera.isSyntheticPreview {
                Text("SYNTHETIC UI TEST").font(.caption2.bold()).padding(6).background(.black)
                    .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .bottom)
                    .allowsHitTesting(false)
            }
        }
    }

    private var showsGroundCloseUp: Bool {
        // One second for orientation, the middle of the review on the ball/feet, then one
        // second back at full framing before the existing transition to the swing view.
        let last = max(2, Int(CameraSwingController.positionReviewDuration) - 1)
        return expanded && camera.isPositionLocked && (2...last).contains(camera.reviewSecondsRemaining)
    }

    private var prompt: some View {
        VStack(spacing: 10) {
          if !camera.isPositionLocked {
              VStack(alignment: .leading, spacing: 5) {
                  Label(CameraPlayerStance(handedness: handedness).title, systemImage: "figure.golf")
                      .font(.subheadline.bold()).foregroundStyle(.mint)
                  Text(CameraPlayerStance(handedness: handedness).instruction)
                      .font(.caption).fixedSize(horizontal: false, vertical: true)
                  Text("Step back until both feet and fully extended arms fit, with room above your hands. Lower your hands to lock the ball; then keep the phone still.")
                      .font(.caption2).foregroundStyle(.white.opacity(0.7))
              }
              .frame(maxWidth: .infinity, alignment: .leading)
              .accessibilityElement(children: .combine)
              .accessibilityIdentifier("cameraStanceGuide")
              Divider().overlay(.white.opacity(0.2))
          }
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
                Text(title).font(.headline).accessibilityIdentifier("cameraSetupStatus")
                Text(detail).font(.caption).foregroundStyle(.white.opacity(0.7))
            }
            Spacer(minLength: 0)
          }
          HStack {
              Button("Recenter grip", systemImage: "scope", action: onRecenter)
                  .accessibilityIdentifier("recenterGrip")
              Spacer()
              Button("Minimize", action: onResize)
                  .disabled(camera.reviewSecondsRemaining > 0)
                  .accessibilityIdentifier("minimizeCamera")
          }
          .font(.caption.bold())
          if camera.isPositionLocked {
              HStack {
                  Button("Lower ball", systemImage: "arrow.down.to.line") { camera.adjustGround(by: -0.015) }
                      .accessibilityIdentifier("lowerGroundBall")
                  Spacer()
                  Button("Raise ball", systemImage: "arrow.up.to.line") { camera.adjustGround(by: 0.015) }
                      .accessibilityIdentifier("raiseGroundBall")
              }.font(.caption.bold())
              Text("Place the ring on the floor in front of your feet. Keep the phone still.")
                  .font(.caption2).foregroundStyle(.white.opacity(0.75))
          }
        }
        .padding(.horizontal, 16).padding(.vertical, 10)
        .background(.black.opacity(0.65), in: RoundedRectangle(cornerRadius: 18))
        .accessibilityElement(children: .contain)
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
        if camera.trackingIsStale { return "Waiting for fresh camera tracking" }
        if camera.isCheckingSwing { return camera.swingCheck.title }
        if camera.reviewSecondsRemaining > 0 { return "Position confirmed · \(camera.reviewSecondsRemaining)" }
        if case .running = camera.status { return camera.readiness.title }
        return statusText
    }

    private var detail: String {
        if camera.trackingIsStale { return "Do not swing yet. Tracking has paused; your ball stays fixed." }
        if camera.isCheckingSwing { return camera.swingCheck.detail }
        if camera.reviewSecondsRemaining > 0 {
            return showsGroundCloseUp
                ? "Close-up: see the ball beside your feet. Its position stays fixed. Wait for the swing view."
                : camera.reviewSecondsRemaining == 1
                    ? "The ball stays in that spot. Returning to the swing view—get ready."
                    : "Find the larger ground ball by your feet. We’ll zoom in, then return to the swing view."
        }
        return switch camera.status {
        case .running: camera.hasPlayableTracking && camera.contactAssistance
            ? "Swing through. Contact is assisted; your strike location and swing speed affect the shot."
            : camera.readiness.detail
        case .denied: "Enable Camera for Golf Arcade in iPhone Settings, or minimize this panel and use touch."
        case .unavailable: "Minimize this panel and use touch. Camera swings require an available front camera."
        default: "Waiting for camera access. You can minimize this panel and use touch."
        }
    }

    private var borderColor: Color {
        camera.hasPlayableTracking ? .mint : .yellow
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
