import AVFoundation
import SwiftUI

struct CalibrationView: View {
    @ObservedObject var tracker: CameraPoseTracker
    var playerName: String?
    let onComplete: (PlayerCalibration) -> Void
    var onCancel: (() -> Void)?

    @State private var step: Step = .intro
    @State private var accumulator = CalibrationAccumulator()
    @State private var isFinishing = false

    private enum Step { case intro, scanning }

    var body: some View {
        ZStack {
            Color.black.ignoresSafeArea()
            CameraPreview(
                session: tracker.session,
                videoRotationAngle: tracker.videoRotationAngle,
                isVideoMirrored: tracker.isVideoMirrored,
                videoGravity: .resizeAspect
            )
            .ignoresSafeArea()
            if step == .scanning {
                PoseSkeletonView(frame: tracker.latestFrame, frameAspect: tracker.frameAspect, contentMode: .fit)
                    .ignoresSafeArea()
                CalibrationBodyGuide().stroke(guideColor, style: StrokeStyle(lineWidth: 3, dash: [9, 7]))
                    .aspectRatio(tracker.frameAspect, contentMode: .fit)
                    .padding(.horizontal, 44).padding(.vertical, 24)
                    .accessibilityHidden(true)
            }
            LinearGradient(colors: [.black.opacity(0.72), .clear, .black.opacity(0.88)], startPoint: .top, endPoint: .bottom)
                .ignoresSafeArea().allowsHitTesting(false)

            VStack(spacing: 22) {
                header
                Spacer()
                if step == .intro {
                    ScrollView { introCard }
                        .scrollIndicators(.hidden)
                } else {
                    scanControls
                }
            }
            .padding(20)

            if case .denied = tracker.status { permissionOverlay }
        }
        // The screen that presents the scan decides whether the camera keeps running afterwards.
        .onAppear { tracker.start() }
        .onReceive(tracker.$latestFrame) { frame in
            guard step == .scanning, !isFinishing else { return }
            guard let frame else {
                accumulator.reportTracking(
                    bodyCount: tracker.detectedBodyCount,
                    timestamp: tracker.latestCaptureTimestamp
                )
                return
            }
            if let calibration = accumulator.ingest(frame, detectedBodyCount: tracker.detectedBodyCount) {
                isFinishing = true
                tracker.setCalibration(calibration)
                UINotificationFeedbackGenerator().notificationOccurred(.success)
                DispatchQueue.main.asyncAfter(deadline: .now() + 0.35) { onComplete(calibration) }
            }
        }
    }

    private var header: some View {
        VStack(spacing: 6) {
            if let onCancel {
                HStack {
                    Button("Cancel", action: onCancel)
                        .font(.subheadline.bold())
                        .accessibilityIdentifier("cancelScan")
                    Spacer()
                }
            }
            Text(step == .intro ? (playerName.map { "SCAN \($0.uppercased())" } ?? "PLAYER CALIBRATION") : "FULL-BODY SCAN")
                .font(.caption.bold()).tracking(1.5).foregroundStyle(.mint)
            Text(step == .intro ? "Make every swing yours" : "Stand in the guide")
                .font(.title.bold()).multilineTextAlignment(.center)
            if step == .scanning {
                Text(accumulator.assessment.instruction)
                    .font(.subheadline.weight(.semibold)).multilineTextAlignment(.center)
                    .foregroundStyle(.white.opacity(0.82))
                    .animation(.easeInOut, value: accumulator.assessment)
            }
        }
        .frame(maxWidth: .infinity)
    }

    private var introCard: some View {
        VStack(alignment: .leading, spacing: 18) {
            Label("One quick scan", systemImage: "figure.arms.open")
                .font(.title3.bold()).foregroundStyle(.mint)
            Text("We’ll measure your body landmarks so the swing camera follows you instead of person-shaped objects in the background.")
                .font(.subheadline).foregroundStyle(.white.opacity(0.82))
            instructionRow("Set the phone where it will stay while you play", symbol: "iphone.gen3")
            instructionRow("Stand 5–10 feet away with your whole body visible", symbol: "arrow.left.and.right")
            instructionRow("Face the camera and hold your arms slightly out", symbol: "figure.stand")
            Text("No spin needed. The swing tracker uses a front-facing 2D skeleton, so turning around would hide the landmarks it needs.")
                .font(.caption).foregroundStyle(.secondary)
            Button {
                accumulator.reset()
                withAnimation(.easeInOut) { step = .scanning }
            } label: {
                Label("Start body scan", systemImage: "viewfinder")
                    .frame(maxWidth: .infinity).padding()
                    .background(Color.mint, in: RoundedRectangle(cornerRadius: 16))
                    .foregroundStyle(.black).fontWeight(.bold)
            }
            .buttonStyle(.plain)
            .disabled(tracker.status != .running)
            .opacity(tracker.status == .running ? 1 : 0.55)
        }
        .padding(22)
        .background(.ultraThinMaterial, in: RoundedRectangle(cornerRadius: 26, style: .continuous))
    }

    private var scanControls: some View {
        VStack(spacing: 14) {
            scanStatus
            ProgressView(value: accumulator.progress)
                .tint(.mint).scaleEffect(x: 1, y: 2)
                .accessibilityLabel("Body scan progress")
                .accessibilityValue("\(Int(accumulator.progress * 100)) percent")
            HStack {
                Text(isFinishing ? "Calibration complete" : "SCANNING")
                Spacer()
                Text("\(accumulator.sampleCount)/\(CalibrationAccumulator.requiredSampleCount)")
                    .monospacedDigit()
            }
            .font(.caption.bold())
            Button("Review setup instructions") {
                accumulator.reset()
                withAnimation(.easeInOut) { step = .intro }
            }
            .font(.footnote).foregroundStyle(.white.opacity(0.7))
        }
        .padding(20)
        .background(.ultraThinMaterial, in: RoundedRectangle(cornerRadius: 22, style: .continuous))
    }

    private var scanStatus: some View {
        Label {
            Text(accumulator.assessment == .ready ? "Body locked — keep holding" : "Waiting for a valid pose")
        } icon: {
            Image(systemName: accumulator.assessment == .ready ? "checkmark.circle.fill" : "viewfinder.circle")
        }
        .font(.caption.bold())
        .foregroundStyle(accumulator.assessment == .ready ? .mint : .yellow)
    }

    private func instructionRow(_ text: String, symbol: String) -> some View {
        Label {
            Text(text).font(.subheadline.weight(.semibold))
        } icon: {
            Image(systemName: symbol).frame(width: 24).foregroundStyle(.mint)
        }
    }

    private var guideColor: Color { accumulator.assessment == .ready ? .mint : .white.opacity(0.72) }

    private var permissionOverlay: some View {
        VStack(spacing: 12) {
            Image(systemName: "camera.fill").font(.largeTitle)
            Text("Camera access is required to calibrate your swing.").multilineTextAlignment(.center)
            if let url = URL(string: UIApplication.openSettingsURLString) {
                Link("Open Settings", destination: url).buttonStyle(.borderedProminent).tint(.mint)
            }
        }
        .padding(28).background(.black.opacity(0.94), in: RoundedRectangle(cornerRadius: 22)).padding()
    }
}
private struct CalibrationBodyGuide: Shape {
    func path(in rect: CGRect) -> Path {
        var path = Path()
        let centerX = rect.midX
        let headRadius = min(rect.width, rect.height) * 0.07
        path.addEllipse(in: CGRect(x: centerX - headRadius, y: rect.minY, width: headRadius * 2, height: headRadius * 2))
        path.move(to: CGPoint(x: centerX, y: rect.minY + headRadius * 2))
        path.addLine(to: CGPoint(x: centerX, y: rect.minY + rect.height * 0.48))
        path.move(to: CGPoint(x: centerX, y: rect.minY + rect.height * 0.2))
        path.addLine(to: CGPoint(x: rect.minX + rect.width * 0.16, y: rect.minY + rect.height * 0.42))
        path.move(to: CGPoint(x: centerX, y: rect.minY + rect.height * 0.2))
        path.addLine(to: CGPoint(x: rect.maxX - rect.width * 0.16, y: rect.minY + rect.height * 0.42))
        path.move(to: CGPoint(x: centerX, y: rect.minY + rect.height * 0.48))
        path.addLine(to: CGPoint(x: rect.minX + rect.width * 0.30, y: rect.maxY))
        path.move(to: CGPoint(x: centerX, y: rect.minY + rect.height * 0.48))
        path.addLine(to: CGPoint(x: rect.maxX - rect.width * 0.30, y: rect.maxY))
        return path
    }
}
