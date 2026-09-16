import Combine
import SwiftUI

enum SwingInput: String, CaseIterable, Identifiable {
    case touch, phone, camera
    var id: Self { self }
    var title: String {
        switch self {
        case .touch: "Touch"
        case .phone: "Phone"
        case .camera: "Camera"
        }
    }
}

/// Stroke play on a full-screen hole: the club rail floats on the right, the camera sits in the
/// bottom-left corner, and panels appear only between shots.
struct CourseScreen: View {
    @ObservedObject var flow: GameFlow
    @ObservedObject var camera: CameraSwingController
    @StateObject private var round = CourseRound()
    @StateObject private var motion = PhoneSwingController()
    @State private var scene = CourseScene()
    @State private var audio = RangeAudio()
    @State private var feedback = SwingFeedbackController()
    @State private var demoTask: Task<Void, Never>?
    @State private var rescanPresented = false
    @State private var bannerVisible = false
    @State private var bannerTask: Task<Void, Never>?
    @AppStorage("arcade.hapticsEnabled") private var haptics = true
    @AppStorage("range.soundEnabled") private var sound = true
    @AppStorage("range.swingInput") private var swingInput: SwingInput = .camera
    @AppStorage("gestures.enabled") private var gesturesEnabled = true
    @Environment(\.scenePhase) private var appPhase

    private let tick = Timer.publish(every: 1.0 / 30, on: .main, in: .common).autoconnect()

    private var players: [Player] { flow.players }
    private var player: Player {
        players.indices.contains(round.playerIndex) ? players[round.playerIndex] : players[0]
    }
    private var isMultiplayer: Bool { players.count > 1 }
    private var usesCamera: Bool { swingInput == .camera }

    var body: some View {
        GeometryReader { proxy in
            ZStack {
                TimelineView(.animation(minimumInterval: 1.0 / 30, paused: round.phase != .flying || appPhase != .active)) { timeline in
                    CourseSceneView(
                        scene: scene,
                        hole: round.hole,
                        ball: round.ball,
                        heading: round.heading,
                        distanceToPin: round.distanceToPin,
                        shot: round.activeShot,
                        elapsed: round.elapsed(at: timeline.date),
                        aim: round.aim,
                        swingAngle: usesCamera ? camera.swingAngle : round.power * 150,
                        handedness: player.handedness
                    )
                }
                .ignoresSafeArea()
                .accessibilityElement()
                .accessibilityLabel("\(flow.course.name), hole \(round.hole.number)")
                .accessibilityIdentifier("golfCourse")

                LinearGradient(colors: [.black.opacity(0.18), .clear, .clear, .black.opacity(0.28)], startPoint: .top, endPoint: .bottom)
                    .ignoresSafeArea()
                    .allowsHitTesting(false)

                holeChip
                    .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
                    .padding(.leading, 14).padding(.top, 8)

                ClubRail(round: round, focusedClub: round.canSwing ? round.club : nil) { menuItems }
                    .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .trailing)
                    .padding(.trailing, 10)

                if usesCamera {
                    CameraPip(camera: camera, ballAddress: camera.ballAddress, gesturesEnabled: gesturesEnabled)
                        .frame(width: proxy.size.width > proxy.size.height ? 160 : 132)
                        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .bottomLeading)
                        .padding(.leading, 12).padding(.bottom, 12)
                }

                bottomPanel(width: proxy.size.width)

                if bannerVisible || (usesCamera && isMultiplayer && camera.phase == .findingPlayer && round.canSwing) {
                    TurnBanner(
                        name: player.name,
                        color: Palette.player(player.colorIndex),
                        detail: bannerVisible ? "Hole \(round.hole.number) · Stroke \(round.strokeNumber)" : "Step into the camera view",
                        showsName: isMultiplayer
                    )
                    .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .top)
                    .padding(.top, 54)
                    .transition(.move(edge: .top).combined(with: .opacity))
                    .allowsHitTesting(false)
                }
            }
        }
        .foregroundStyle(Palette.cream)
        .background(Palette.ink.ignoresSafeArea())
        .fullScreenCover(isPresented: $rescanPresented) {
            CalibrationView(tracker: camera.tracker, playerName: player.name, onComplete: { calibration in
                flow.finishScan(player.id, calibration: calibration)
                flow.screen = .playing
                rescanPresented = false
            }, onCancel: { rescanPresented = false })
        }
        .onReceive(tick) { date in
            guard appPhase == .active, !rescanPresented else { return }
            let wasFlying = round.phase == .flying
            let wasReplay = round.isReplay
            round.advance(at: date)
            if wasFlying, round.phase != .flying, !wasReplay { landed() }
        }
        .onNavGesture(camera) { handleGesture($0) }
        .onAppear {
            round.start(course: flow.course, playerCount: players.count)
            feedback.isEnabled = haptics
            audio.enabled = sound
            motion.setClub(round.club)
            motion.onEvent = { handleSwing($0) }
            camera.onEvent = { handleSwing($0) }
            beginTurn()
            updateInputs()
        }
        .onChange(of: haptics) { _, value in feedback.isEnabled = value }
        .onChange(of: sound) { _, value in audio.enabled = value }
        .onChange(of: round.club) { _, club in motion.setClub(club) }
        .onChange(of: swingInput) { _, _ in stopFeedback(); updateInputs() }
        .onChange(of: gesturesEnabled) { _, _ in updateInputs() }
        .onChange(of: rescanPresented) { _, presented in
            if presented { stopFeedback(); round.pause() } else { round.resume(); beginTurn(announce: false) }
            updateInputs()
        }
        .onChange(of: round.playerIndex) { _, _ in beginTurn() }
        .onChange(of: round.holeIndex) { _, _ in beginTurn() }
        .onChange(of: appPhase) { _, phase in
            if phase != .active { stopFeedback(); round.pause() } else { round.resume() }
            updateInputs()
        }
        .onDisappear {
            stopFeedback()
            bannerTask?.cancel()
            motion.stop()
            camera.onEvent = nil
            camera.ballAddress = nil
        }
        .preferredColorScheme(.dark)
    }

    // MARK: - Overlays

    private var holeChip: some View {
        HStack(spacing: 7) {
            if isMultiplayer {
                Circle().fill(Palette.player(player.colorIndex)).frame(width: 8, height: 8)
                Text(player.name.uppercased())
            }
            Text("H\(round.hole.number) · PAR \(round.hole.par) · \(Int(round.distanceToPin.rounded())) YD")
                .monospacedDigit()
            if round.strokes > 0 { Text("· \(round.strokes) SHOT\(round.strokes == 1 ? "" : "S")").monospacedDigit() }
        }
        .font(.system(size: 10, weight: .heavy, design: .rounded))
        .padding(.horizontal, 10).padding(.vertical, 6)
        .background(.black.opacity(0.45), in: Capsule())
        .accessibilityElement(children: .combine)
        .accessibilityIdentifier("holeChip")
    }

    @ViewBuilder
    private func bottomPanel(width: CGFloat) -> some View {
        let panelWidth = min(width - 32, 470)
        Group {
            switch round.phase {
            case .complete:
                RoundCompletePanel(round: round, players: players, onPlayAgain: playAgain, onMenu: quit)
                    .frame(maxWidth: panelWidth)
            case .holed:
                if let shot = round.activeShot, round.phase == .holed {
                    HoleCompletePanel(round: round, player: player, shot: shot, isMultiplayer: isMultiplayer, onReplay: { round.replay() }, onContinue: continueAfterHole)
                        .frame(maxWidth: panelWidth)
                }
            case .landed:
                if let shot = round.activeShot {
                    ShotResultPanel(round: round, shot: shot, showsStrike: usesCamera, onReplay: { round.replay() }, onNext: nextShot)
                        .frame(maxWidth: panelWidth)
                }
            case .ready, .charging, .flying:
                inputPanel
                    .frame(maxWidth: usesCamera ? 230 : min(width * 0.58, 440))
            }
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: usesCamera && round.canSwing ? .bottom : .bottom)
        .padding(.bottom, 16)
        .padding(.leading, usesCamera && round.canSwing ? 150 : 0)
        .padding(.trailing, 80)
    }

    @ViewBuilder
    private var inputPanel: some View {
        if round.phase == .flying {
            if round.isReplay {
                StatusPill(icon: "play.fill", title: "Replay", detail: "Punch or swipe right to skip")
                    .onTapGesture { round.skipFlight() }
            }
        } else {
            switch swingInput {
            case .touch:
                SwingPad(power: round.power, charging: round.phase == .charging, onCharge: { value in
                    guard demoTask == nil, round.canSwing else { return }
                    charge(value)
                }, onRelease: {
                    guard demoTask == nil else { return }
                    release()
                }, onCancel: {
                    guard demoTask == nil, round.phase == .charging else { return }
                    stopFeedback()
                }, onDemo: demo)
            case .phone:
                StatusPill(icon: "iphone.gen3.radiowaves.left.and.right", title: motionCopy, detail: nil)
                    .accessibilityIdentifier("motionPanel")
            case .camera:
                VStack(spacing: 8) {
                    if camera.needsLineUp, let offset = camera.handsOffset {
                        LineUpIndicator(offset: offset)
                    }
                    StatusPill(icon: camera.needsLineUp ? "scope" : "figure.golf", title: cameraCopy, detail: nil)
                        .accessibilityIdentifier("cameraPanel")
                }
            }
        }
    }

    @ViewBuilder
    private var menuItems: some View {
        if usesCamera {
            Button("Rescan \(player.name)", systemImage: "viewfinder") { rescanPresented = true }
        }
        Button("Restart hole", systemImage: "arrow.counterclockwise") { stopFeedback(); round.restartHole() }
        Button("Restart round", systemImage: "arrow.triangle.2.circlepath") { stopFeedback(); round.restart() }
        Toggle("Sound effects", isOn: $sound)
        Toggle("Haptics", isOn: $haptics)
        Button("Quit to menu", systemImage: "house", role: .destructive) { quit() }
    }

    private var cameraCopy: String {
        switch camera.status {
        case .idle, .requestingPermission: return "Starting camera"
        case .denied: return "Camera access is off"
        case .unavailable(let reason): return reason
        case .running: break
        }
        if camera.needsLineUp { return "Line your hands up with the ball" }
        return switch camera.phase {
        case .findingPlayer: isMultiplayer ? "\(player.name), step into view" : "Step into view"
        case .address: "Ready. Take it back"
        case .backswing: round.power >= 1 ? "Full power" : "Backswing"
        case .downswing, .impact: "Through the ball"
        case .followThrough, .finish: "Set up again"
        }
    }

    private var motionCopy: String {
        switch motion.status {
        case .unavailable: "Motion needs a real iPhone"
        case .idle, .settling: "Hold still at address"
        case .address: "Ready. Take it back"
        case .backswing: "Backswing"
        case .downswing: "Swing"
        }
    }

    // MARK: - Turn flow

    private func beginTurn(announce: Bool = true) {
        let current = player
        camera.handedness = current.handedness
        camera.ballAddress = current.calibration.map { BallAddress(calibration: $0, handedness: current.handedness) }
        camera.tracker.setCalibration(current.calibration)
        guard announce else { return }
        bannerTask?.cancel()
        withAnimation(.easeOut(duration: 0.2)) { bannerVisible = true }
        bannerTask = Task { @MainActor in
            try? await Task.sleep(for: .seconds(1.6))
            guard !Task.isCancelled else { return }
            withAnimation(.easeIn(duration: 0.25)) { bannerVisible = false }
        }
    }

    private func landed() {
        guard let shot = round.activeShot else { return }
        if shot.isHoled || shot.lie == .green { audio.celebrate() }
        if round.phase == .holed { feedback.playImpact() }
    }

    private func nextShot() { round.nextShot() }

    private func continueAfterHole() {
        stopFeedback()
        round.continueAfterHole()
    }

    private func playAgain() {
        stopFeedback()
        round.restart()
        beginTurn()
    }

    private func quit() {
        stopFeedback()
        camera.tracker.setCalibration(nil)
        flow.quitToMenu()
    }

    private func handleGesture(_ gesture: NavGesture) {
        guard !rescanPresented else { return }
        switch round.phase {
        case .ready:
            let clubs = GolfClub.allCases
            let index = clubs.firstIndex(of: round.club) ?? 0
            switch gesture {
            case .up: round.club = clubs[(index + clubs.count - 1) % clubs.count]
            case .down: round.club = clubs[(index + 1) % clubs.count]
            case .left: round.aim = max(-22, round.aim - 2)
            case .right: round.aim = min(22, round.aim + 2)
            case .select: break
            }
        case .charging: break
        case .flying:
            if gesture == .select || gesture == .right { round.skipFlight() }
        case .landed:
            if gesture == .left { round.replay() } else if gesture == .right || gesture == .select { nextShot() }
        case .holed:
            if gesture == .left { round.replay() } else if gesture == .right || gesture == .select { continueAfterHole() }
        case .complete:
            if gesture == .select || gesture == .right { playAgain() } else if gesture == .left { quit() }
        }
    }

    private func updateInputs() {
        let live = appPhase == .active && !rescanPresented
        if live, swingInput == .phone { motion.start() } else { motion.stop() }
        camera.gesturesEnabled = live && gesturesEnabled && usesCamera
        if live, usesCamera { camera.start() } else { camera.stop() }
    }

    // MARK: - Swinging

    private func handleSwing(_ event: SwingInputEvent) {
        guard demoTask == nil else { return }
        switch event {
        case .load(let value):
            // A new backswing after a landed shot tees up the next one without touching the screen.
            if round.phase == .landed { round.nextShot() }
            guard round.canSwing else { return }
            charge(value)
        case .cancel:
            if round.phase == .charging { stopFeedback() }
        case .impact(let power, let curve, let strike):
            guard round.canSwing else { return }
            round.charge(power)
            release(curve: curve, strike: strike)
        }
    }

    private func charge(_ power: Double) {
        if round.phase == .ready { feedback.beginBackswing(interactive: true) }
        round.charge(power)
        feedback.updateTension(round.power)
        audio.tension(round.power)
    }

    private func release(curve: Double = 0, strike: StrikeQuality = .center) {
        feedback.endBackswing()
        audio.stop()
        if round.release(curve: curve, strike: strike) {
            feedback.playImpact()
            audio.impact(club: round.club)
        }
    }

    private func demo() {
        guard round.canSwing, demoTask == nil else { return }
        demoTask = Task { @MainActor in
            defer { demoTask = nil }
            for i in 1...10 {
                guard !Task.isCancelled else { return }
                charge(Double(i) * 0.075)
                do { try await Task.sleep(for: .milliseconds(70)) } catch { return }
            }
            release()
        }
    }

    private func stopFeedback() {
        demoTask?.cancel()
        demoTask = nil
        feedback.endBackswing()
        audio.stop()
        round.cancelCharge()
    }
}
