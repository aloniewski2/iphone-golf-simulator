import AVFoundation
import SwiftUI

struct ArcadeView: View {
    @StateObject private var tracker = CameraPoseTracker()
    @StateObject private var game = ArcadeGameSession()

    var body: some View {
        ZStack {
            Color.black.ignoresSafeArea()
            ScrollView {
                VStack(spacing: 16) {
                    header
                    trackingStage.frame(height: 390)
                    clubPicker
                    if let shot = game.lastShot {
                        RangeView(shot: shot).frame(height: 210)
                        ShotResultCard(shot: shot)
                    } else { safetyCard }
                    demoButton
                }
                .padding()
            }
        }
        .onAppear { tracker.start() }
        .onDisappear { tracker.stop() }
        .onReceive(tracker.$latestFrame.compactMap { $0 }) { game.process($0) }
    }

    private var header: some View {
        HStack {
            VStack(alignment: .leading, spacing: 2) {
                Text("ARCADE MODE").font(.caption.bold()).foregroundStyle(.mint)
                Text("Driving Range").font(.title.bold())
            }
            Spacer()
            Picker("Handedness", selection: $game.handedness) {
                ForEach(Handedness.allCases) { Text($0.displayName).tag($0) }
            }
            .pickerStyle(.menu).tint(.white)
        }
    }

    private var trackingStage: some View {
        ZStack {
            CameraPreview(session: tracker.session)
            PoseSkeletonView(frame: tracker.latestFrame)
            LinearGradient(colors: [.clear, .black.opacity(0.72)], startPoint: .center, endPoint: .bottom)
            VStack {
                HStack {
                    Label(statusText, systemImage: tracker.status == .running ? "circle.fill" : "camera.fill")
                        .font(.caption.bold()).padding(.horizontal, 10).padding(.vertical, 7)
                        .background(.black.opacity(0.6), in: Capsule())
                    Spacer()
                    if let frame = tracker.latestFrame {
                        Text("TRACK \(Int(frame.trackingConfidence * 100))%")
                            .font(.caption2.bold().monospacedDigit()).padding(.horizontal, 10).padding(.vertical, 7)
                            .background(.black.opacity(0.6), in: Capsule())
                    }
                }
                Spacer()
                Text(game.phase.displayName).font(.title2.bold())
                Text(instruction).font(.subheadline).foregroundStyle(.secondary).multilineTextAlignment(.center)
            }
            .padding()
            if case .denied = tracker.status { permissionOverlay }
        }
        .clipShape(RoundedRectangle(cornerRadius: 28, style: .continuous))
        .overlay(RoundedRectangle(cornerRadius: 28, style: .continuous).stroke(.white.opacity(0.14)))
    }

    private var clubPicker: some View {
        HStack(spacing: 8) {
            ForEach(GolfClub.allCases) { club in
                Button { game.selectedClub = club } label: {
                    VStack(spacing: 7) {
                        Image(systemName: club.symbol).font(.title3)
                        Text(club.displayName).font(.caption.bold())
                    }
                    .frame(maxWidth: .infinity).padding(.vertical, 12)
                    .foregroundStyle(game.selectedClub == club ? .black : .white)
                    .background(game.selectedClub == club ? Color.mint : Color.white.opacity(0.09), in: RoundedRectangle(cornerRadius: 16))
                }
                .buttonStyle(.plain)
            }
        }
        .gesture(DragGesture(minimumDistance: 30).onEnded { game.selectNextClub(direction: $0.translation.width < 0 ? 1 : -1) })
    }

    private var safetyCard: some View {
        Label {
            Text("Make sure you have room to swing. The app can warn about tracking, but it cannot confirm that your space is safe.")
        } icon: { Image(systemName: "exclamationmark.triangle.fill").foregroundStyle(.yellow) }
        .font(.subheadline).padding().background(.white.opacity(0.08), in: RoundedRectangle(cornerRadius: 18))
    }

    private var demoButton: some View {
        Button { withAnimation(.spring) { game.createDemoShot() } } label: {
            Label("Try a demo swing", systemImage: "play.fill")
                .frame(maxWidth: .infinity).padding()
                .background(.white.opacity(0.1), in: RoundedRectangle(cornerRadius: 16))
        }
        .buttonStyle(.plain).foregroundStyle(.white)
    }

    private var permissionOverlay: some View {
        VStack(spacing: 12) {
            Image(systemName: "camera.fill").font(.largeTitle)
            Text("Camera access is needed to track your swing.").multilineTextAlignment(.center)
            if let url = URL(string: UIApplication.openSettingsURLString) {
                Link("Open Settings", destination: url).buttonStyle(.borderedProminent).tint(.mint)
            }
        }
        .padding(28).background(.black.opacity(0.9), in: RoundedRectangle(cornerRadius: 22)).padding()
    }

    private var instruction: String {
        switch game.phase {
        case .findingPlayer: "Place the phone 7–12 feet away and fit your full body in frame."
        case .address: "Settle over the virtual ball, then swing when ready."
        case .backswing, .downswing, .impact: "Keep moving — we’re reading your swing."
        case .followThrough: "Hold your finish."
        case .finish: "Nice shot. Reset at address when you’re ready."
        }
    }

    private var statusText: String {
        switch tracker.status {
        case .idle: "CAMERA IDLE"
        case .requestingPermission: "ALLOW CAMERA"
        case .running: "LIVE"
        case .denied: "CAMERA OFF"
        case .unavailable: "UNAVAILABLE"
        }
    }
}

