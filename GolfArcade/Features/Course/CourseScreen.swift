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
    // StateObject's lazy construction is essential: @State(initialValue:) eagerly constructs
    // and discards new hardware engines every time the parent receives a camera frame.
    @StateObject private var scene = CourseScene()
    @StateObject private var audio = RangeAudio()
    @StateObject private var feedback = SwingFeedbackController()
    @State private var demoTask: Task<Void, Never>?
    @State private var rescanPresented = false
    @State private var bannerVisible = false
    @State private var bannerTask: Task<Void, Never>?
    /// Certification shows the ground ball full-screen for five seconds, then returns to play.
    @State private var stageExpanded = true
    @AppStorage("arcade.hapticsEnabled") private var haptics = true
    @AppStorage("range.soundEnabled") private var sound = true
    @AppStorage("range.swingInput") private var swingInput: SwingInput = .camera
    @AppStorage("gestures.enabled") private var gesturesEnabled = true
    @AppStorage("camera.depthExperiment") private var depthExperiment = false
    @AppStorage("camera.capture60") private var capture60 = false
    @Environment(\.scenePhase) private var appPhase

    // ContentView observes the camera, so this View value is rebuilt for every pose. A plain
    // stored Timer publisher would be replaced before it fires on a busy real camera.
    @State private var tick = Timer.publish(every: 1.0 / 30, on: .main, in: .common).autoconnect()


    private var players: [Player] { flow.players }
    private var player: Player {
        players.indices.contains(round.playerIndex) ? players[round.playerIndex] : players[0]
    }
    private var isMultiplayer: Bool { players.count > 1 }
    private var usesCamera: Bool { swingInput == .camera }

    var body: some View {
        cleanupView.preferredColorScheme(.dark)
    }

    private var layoutView: some View {
        GeometryReader { proxy in
            courseCanvas(size: proxy.size)
        }
        .foregroundStyle(Palette.cream)
        .background(Palette.ink.ignoresSafeArea())
    }

    private var interactionView: some View {
        layoutView
        .fullScreenCover(isPresented: $rescanPresented) {
            CalibrationView(tracker: camera.tracker, playerName: player.name, onComplete: { calibration in
                flow.finishScan(player.id, calibration: calibration)
                flow.screen = .playing
                rescanPresented = false
            }, onCancel: { rescanPresented = false })
        }
        .onReceive(tick) { date in
            guard appPhase == .active, !rescanPresented else { return }
            camera.advancePositionReview()
            let wasFlying = round.phase == .flying
            let wasReplay = round.isReplay
            round.advance(at: date)
            if wasFlying, round.phase != .flying, !wasReplay { landed() }
        }
        .onNavGesture(camera) { handleGesture($0) }
        .onReceive(camera.$frame) { frame in
            guard usesCamera else { return }
            scene.ingest(frame, swingAngle: camera.swingAngle, frameAspect: camera.tracker.frameAspect,
                         virtualClub: camera.virtualClub, positionLocked: camera.isPositionLocked)
        }
        .onChange(of: camera.reviewSecondsRemaining) { old, remaining in
            guard usesCamera else { return }
            if remaining > 0 {
                if !stageExpanded { withAnimation { stageExpanded = true } }
            }
            else if old > 0 {
                guard camera.isPositionLocked, !camera.isCheckingSwing else { return }
                withAnimation(.spring(duration: 0.55)) { stageExpanded = false }
                if camera.hasPlayableTracking { audio.ready() }
            }
        }
        .onChange(of: camera.addressAimDegrees) { _, degrees in
            if usesCamera { round.setStanceAim(degrees) }
        }
        .onChange(of: round.phase) { _, phase in
            if phase == .ready { round.setStanceAim(usesCamera ? camera.addressAimDegrees : 0) }
        }
        .onAppear {
            scene.onBystanderHit = {
                audio.thump()
                UIImpactFeedbackGenerator(style: .rigid).impactOccurred()
            }
            round.start(course: flow.course, playerCount: players.count)
            round.automaticProgression = true
            #if DEBUG
            // UI tests that press Next shot / Continue themselves must not be raced by the timer.
            if ProcessInfo.processInfo.arguments.contains("-manualProgression") { round.automaticProgression = false }
            #endif
            feedback.isEnabled = haptics
            audio.enabled = sound
            motion.setClub(round.club)
            camera.setClub(round.club, type: round.shotType)
            camera.requiresPositionReview = true
            camera.requiresSwingCheck = false
            camera.contactAssistance = true
            camera.tracker.setTracking(mode: depthExperiment ? .depthPreview : .body2D, framesPerSecond: capture60 ? 60 : 30)
            motion.onEvent = { handleSwing($0) }
            camera.onEvent = { handleSwing($0) }
            beginTurn()
            updateInputs()
        }
    }

    private var settingsView: some View {
        interactionView
        .onChange(of: haptics) { _, value in feedback.isEnabled = value }
        .onChange(of: sound) { _, value in audio.enabled = value }
        .onChange(of: round.club) { _, club in motion.setClub(club); camera.setClub(club, type: round.shotType) }
        .onChange(of: round.shotType) { _, type in camera.setClub(round.club, type: type) }
        .onChange(of: swingInput) { _, _ in stopFeedback(); stageExpanded = usesCamera; beginTurn(announce: false); updateInputs() }
        .onChange(of: gesturesEnabled) { _, _ in updateInputs() }
        .onChange(of: depthExperiment) { _, _ in updateCameraExperiment() }
        .onChange(of: capture60) { _, _ in updateCameraExperiment() }
        .onChange(of: rescanPresented) { _, presented in
            if presented { stopFeedback(); round.pause() } else { round.resume(); beginTurn(announce: false) }
            updateInputs()
        }
        .onChange(of: round.playerIndex) { _, _ in beginTurn() }
        .onChange(of: round.holeIndex) { _, _ in beginTurn(resetSetup: false) }
        .onChange(of: appPhase) { _, phase in
            if phase != .active { stopFeedback(); round.pause() } else { round.resume() }
            updateInputs()
        }
    }

    private var cleanupView: some View {
        settingsView
        .onDisappear {
            UIApplication.shared.isIdleTimerDisabled = false
            stopFeedback()
            bannerTask?.cancel()
            motion.stop()
            camera.onEvent = nil
            camera.requiresPositionReview = false
            camera.ballAddress = nil
        }
    }

    private func courseCanvas(size: CGSize) -> some View {
            ZStack {
                CourseSceneView(scene: scene, inputs: sceneInputs)
                .ignoresSafeArea()
                .accessibilityElement()
                .accessibilityLabel("\(flow.course.name), hole \(round.hole.number)")
                .accessibilityValue(resourceDiagnostics)
                .accessibilityIdentifier("golfCourse")

                LinearGradient(colors: [.black.opacity(0.18), .clear, .clear, .black.opacity(0.28)], startPoint: .top, endPoint: .bottom)
                    .ignoresSafeArea()
                    .allowsHitTesting(false)

                VStack(alignment: .leading, spacing: 5) {
                    holeChip
                    if round.canSwing {
                        HoleDirectionCue(round: round)
                            .frame(maxWidth: max(180, size.width - 150), alignment: .leading)
                    }
                    Text("\(round.targetLabel) · \(Int(round.distanceToTarget.rounded())) YD")
                        .font(.system(size: 12, weight: .heavy, design: .rounded))
                        .padding(.horizontal, 10).padding(.vertical, 6)
                        .background(.black.opacity(0.45), in: Capsule())
                        .accessibilityIdentifier("landingTarget")
                    if round.canSwing {
                        let preview = round.trajectoryPreview
                        Text("\(round.club.shortName) · \(Int(preview.power * 100))% · \(Int(preview.carry.rounded())) CARRY / \(Int(preview.total.rounded())) TOTAL")
                            .font(.system(size: 11, weight: .bold, design: .rounded)).monospacedDigit()
                            .lineLimit(2)
                            .frame(maxWidth: max(180, size.width - 150), alignment: .leading)
                            .padding(8).background(.black.opacity(0.65), in: Capsule())
                            .accessibilityIdentifier("trajectoryEstimate")
                        Text("CENTER-STRIKE GUIDE · AIM \(String(format: "%+.1f°", round.combinedAim))")
                            .font(.system(size: 10, weight: .bold)).foregroundStyle(.mint)
                            .lineLimit(2)
                            .frame(maxWidth: max(180, size.width - 150), alignment: .leading)
                    }
                }
                    .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
                    .padding(.leading, 14).padding(.top, 8)

                HoleOverview(round: round)
                    .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topTrailing)
                    .padding(.trailing, 10).padding(.top, 42)

                ClubRail(round: round, focusedClub: round.canSwing ? round.club : nil) { menuItems }
                    .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .trailing)
                    .padding(.trailing, 10)

                bottomPanel(width: size.width)

                if usesCamera {
                    if stageExpanded {
                        Color.black.ignoresSafeArea().transition(.opacity)
                    }
                    CameraStage(
                        camera: camera,
                        ballAddress: camera.displayAddress,
                        handedness: player.handedness,
                        expanded: stageExpanded,
                        viewportSize: cameraStageSize(size),
                        gesturesEnabled: gesturesEnabled,
                        onResize: { guard camera.reviewSecondsRemaining == 0 else { return }; withAnimation { stageExpanded.toggle() } },
                        onRecenter: { stopFeedback(); camera.resetAddress(); stageExpanded = true }
                    )
                    .frame(width: cameraStageSize(size).width, height: cameraStageSize(size).height)
                    .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: stageExpanded ? .center : .bottomLeading)
                    .padding(.leading, stageExpanded ? 0 : 12).padding(.bottom, stageExpanded ? 0 : 12)
                    .accessibilityAction(named: "Resize camera") { if camera.reviewSecondsRemaining == 0 { stageExpanded.toggle() } }
                }

                if (bannerVisible || (usesCamera && isMultiplayer && camera.phase == .findingPlayer && round.canSwing)) && !(usesCamera && stageExpanded) {
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

    // MARK: - Overlays

    private func cameraStageSize(_ size: CGSize) -> CGSize {
        if stageExpanded { return size }
        let width: CGFloat = size.width > size.height ? 160 : 132
        return CGSize(width: width, height: width / max(camera.tracker.frameAspect, 0.3))
    }

    private var resourceDiagnostics: String {
        #if DEBUG
        "scene=\(CourseScene.initializationCount);audio=\(RangeAudio.initializationCount);haptics=\(SwingFeedbackController.initializationCount)"
        #else
        ""
        #endif
    }

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
                    .frame(maxWidth: usesCamera ? 230 : min(width * 0.68, 440))
            }
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .bottom)
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
                SwingPad(club: round.club, power: round.power, charging: round.phase == .charging, onCharge: { value in
                    guard demoTask == nil, round.canSwing else { return }
                    charge(value)
                }, onRelease: { direction in
                    guard demoTask == nil else { return }
                    release(execution: SwingImpact(power: round.power, startLineDegrees: direction))
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
                    StatusPill(icon: camera.needsLineUp ? "scope" : "figure.golf", title: cameraCopy, detail: nil, titleLineLimit: 3)
                        .accessibilityIdentifier("cameraPanel")
                    if !camera.hasPlayableTracking {
                        Button("Camera setup", systemImage: "viewfinder") { withAnimation { stageExpanded = true } }
                            .font(.caption.bold())
                            .accessibilityIdentifier("cameraSetup")
                    }
                    Button("Use touch", systemImage: "hand.draw") { swingInput = .touch }
                        .font(.caption.bold())
                        .accessibilityIdentifier("useTouch")
                }
            }
        }
    }

    @ViewBuilder
    private var menuItems: some View {
        Picker("Swing input", selection: $swingInput) {
            ForEach(SwingInput.allCases) { Text($0.title).tag($0) }
        }
        if usesCamera {
            Button("Reset comfortable grip", systemImage: "figure.golf") { stopFeedback(); camera.resetAddress() }
            Text("Contact assist on · strike location affects the shot")
            Toggle("Experimental 3D avatar depth", isOn: $depthExperiment)
            Toggle("Try 60 fps capture", isOn: $capture60)
            Button("Rescan \(player.name)", systemImage: "viewfinder") { rescanPresented = true }
        }
        Button("Restart hole", systemImage: "arrow.counterclockwise") { stopFeedback(); round.restartHole() }
        Button("Restart round", systemImage: "arrow.triangle.2.circlepath") { stopFeedback(); round.restart() }
        Toggle("Sound effects", isOn: $sound)
        Toggle("Haptics", isOn: $haptics)
        Button("Quit to menu", systemImage: "house", role: .destructive) { quit() }
    }

    private var cameraCopy: String {
        if camera.isCheckingSwing { return camera.swingCheck.title }
        if camera.reviewSecondsRemaining > 0 { return "Ball locked · \(camera.reviewSecondsRemaining)" }
        switch camera.status {
        case .idle, .requestingPermission: return "Starting camera"
        case .denied: return "Camera access is off"
        case .unavailable(let reason): return reason
        case .running: break
        }
        if !camera.hasPlayableTracking { return camera.readiness.title }
        return switch camera.phase {
        case .findingPlayer: camera.readiness.title
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

    private func beginTurn(announce: Bool = true, resetSetup: Bool = true) {
        let current = player
        // A new hole does not move the real phone or player. Keep the certified
        // playing space; player/input changes and explicit recenter still reset it.
        if resetSetup {
            stageExpanded = usesCamera
            camera.handedness = current.handedness
            camera.resetAddress()
            camera.tracker.setCalibration(current.calibration)
            scene.configurePlayer(
                calibration: usesCamera ? current.calibration : nil,
                handedness: current.handedness,
                frameAspect: camera.tracker.frameAspect
            )
        }
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
        switch AvatarAnimations.Reaction.classify(shot) {
        case .pure, .holed: audio.celebrate()
        case .bad, .disaster: audio.groan()
        case .solid, .meh: if shot.lie == .green { audio.celebrate() }
        }
        if round.phase == .holed { feedback.playImpact() }
    }

    private func nextShot() { round.nextShot() }

    private var sceneInputs: SceneInputs {
        SceneInputs(
            hole: round.hole,
            ball: round.ball,
            heading: round.heading,
            distanceToPin: round.distanceToPin,
            lie: round.lie,
            club: round.club,
            aim: round.combinedAim,
            handedness: player.handedness,
            shot: round.activeShot,
            isReplay: round.isReplay,
            flightStart: round.flightStart,
            pausedAt: round.pausedAt,
            swingAngle: usesCamera ? camera.swingAngle : round.power * 150,
            bystanders: players.filter { $0.id != player.id }.map { SceneInputs.Bystander(id: $0.id, colorIndex: $0.colorIndex) },
            preview: round.canSwing ? round.trajectoryPreview : nil
        )
    }

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
            case .left: round.adjustAim(-round.aimStep)
            case .right: round.adjustAim(round.aimStep)
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
        UIApplication.shared.isIdleTimerDisabled = live && usesCamera
        if live, swingInput == .phone { motion.start() } else { motion.stop() }
        camera.gesturesEnabled = live && gesturesEnabled && usesCamera
        if live, usesCamera { camera.start() } else { camera.stop() }
    }

    private func updateCameraExperiment() {
        stopFeedback()
        camera.resetAddress()
        camera.tracker.setTracking(mode: depthExperiment ? .depthPreview : .body2D, framesPerSecond: capture60 ? 60 : 30)
        stageExpanded = true
    }

    // MARK: - Swinging

    private func handleSwing(_ event: SwingInputEvent) {
        guard demoTask == nil else { return }
        switch event {
        case .load(let value):
            guard round.canSwing else { return }
            charge(value)
        case .cancel:
            if round.phase == .charging { stopFeedback() }
        case .impact(let impact):
            guard round.canSwing else { return }
            round.charge(impact.power)
            release(execution: impact)
        }
    }

    private func charge(_ power: Double) {
        if round.phase == .ready { feedback.beginBackswing(interactive: true) }
        round.charge(power)
        feedback.updateTension(round.power)
        audio.tension(round.power)
    }

    private func release(execution: SwingImpact? = nil) {
        feedback.endBackswing()
        audio.stop()
        if round.release(execution: execution), execution?.strike != .miss {
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
            release(execution: SwingImpact(power: round.power, source: .demo))
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
