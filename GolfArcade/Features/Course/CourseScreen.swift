import Combine
import SwiftUI

/// The phone owns one round and animation clock. The external display only renders them.
struct CourseScreen: View {
    @ObservedObject var flow: GameFlow
    var practice = false
    @ObservedObject private var tv = GolfTVDisplay.shared
    @StateObject private var round = CourseRound()
    @StateObject private var motion = PhoneSwingController()
    @StateObject private var scene = CourseScene()
    @StateObject private var audio = RangeAudio()
    @StateObject private var feedback = SwingFeedbackController()
    @State private var demoTask: Task<Void, Never>?
    @State private var releaseTask: Task<Void, Never>?
    @State private var authoredDownswingAngle: Double?
    @State private var paused = false
    @State private var started = false
    @State private var flyoverStarted:Date?
    @State private var flyoverProgress:Double?
    private enum Sheet: String, Identifiable { case tv, shot, controller; var id: String { rawValue } }
    @State private var sheet: Sheet?
    @AppStorage("arcade.hapticsEnabled") private var haptics = true
    @AppStorage("range.soundEnabled") private var sound = true
    @AppStorage("range.swingInput") private var swingInput: SwingInput = .phone
    @AppStorage("controller.sensitivity") private var sensitivity = 1.8
    @Environment(\.scenePhase) private var appPhase
    @Environment(\.accessibilityReduceMotion) private var reduceMotion
    @State private var tick = Timer.publish(every: 1.0 / 30, on: .main, in: .common).autoconnect()

    private var players: [Player] { flow.players }
    private var player: Player {
        players.indices.contains(round.playerIndex) ? players[round.playerIndex] : players[0]
    }
    private var controllerLayout: Bool {
        #if DEBUG
        if ProcessInfo.processInfo.arguments.contains("-controllerLayout") { return true }
        #endif
        return tv.active || swingInput != .touch
    }
    private var controlsEnabled: Bool {
        round.phase == .ready && round.canSwing && !motion.isArmed && motion.status != .followThrough && releaseTask == nil && demoTask == nil && flyoverProgress == nil
    }

    var body: some View {
        ZStack {
            if let session = tvPreviewSession {
                TVCourseView(session: session, round: round, scene: scene)
            } else if controllerLayout { controller }
            else { touchCourse }
        }
        .foregroundStyle(Palette.cream)
        .background(Palette.ink.ignoresSafeArea())
        .preferredColorScheme(.dark)
        .sheet(item: $sheet, onDismiss: resumeAfterSheet) { destination in
            switch destination {
            case .tv: TVSettingsView()
            case .shot: ShotControls(round: round)
            case .controller: SettingsView()
            }
        }
        .onAppear(perform: start)
        .onDisappear {
            stopFeedback()
            motion.stop()
            motion.onEvent = nil
            scene.stop()
            tv.end(round: round)
            UIApplication.shared.isIdleTimerDisabled = false
        }
        .onReceive(tick) { date in
            if let start=flyoverStarted {
                let progress=date.timeIntervalSince(start)/8
                if progress>=1 { flyoverStarted=nil;flyoverProgress=nil }
                else { flyoverProgress=progress }
            }
            let wasFlying = round.phase == .flying
            let wasReplay = round.isReplay
            round.advance(at: date)
            if wasFlying, round.phase != .flying, !wasReplay { landed() }
            scene.inputs = sceneInputs
            if tv.session?.playerName != player.name { tv.session?.playerName = player.name }
            let status = flyoverProgress != nil ? "Hole tour · controls resume when the flyover ends" : swingInput == .phone ? motionCopy : "Ready · aim and swing using touch on your iPhone"
            if tv.session?.controllerStatus != status { tv.session?.controllerStatus = status }
        }
        .onChange(of: appPhase) { _, _ in synchronizeActivity() }
        .onChange(of: paused) { _, _ in synchronizeActivity() }
        .onChange(of: tv.active) { _, _ in stopFeedback(); scene.inputs = sceneInputs }
        .onChange(of: swingInput) { _, _ in stopFeedback(); synchronizeActivity() }
        .onChange(of: round.club) { _, club in stopFeedback(); motion.setClub(club) }
        .onChange(of: round.shotType) { _, _ in stopFeedback() }
        .onChange(of: round.aim) { _, _ in motion.disarm() }
        .onChange(of: round.target) { _, _ in motion.disarm() }
        .onChange(of: round.editingShot) { _, editing in if editing { stopFeedback() } }
        .onChange(of: round.playerIndex) { _, _ in beginTurn() }
        .onChange(of: round.holeIndex) { _, _ in beginTurn() }
        .onChange(of: haptics) { _, value in feedback.isEnabled = value }
        .onChange(of: sound) { _, value in audio.enabled = value }
        .onChange(of: sensitivity) { _, value in stopFeedback(); motion.sensitivity = value }
        .onChange(of: motion.status) { old, value in
            if value == .address, old != .address {
                if haptics { UINotificationFeedbackGenerator().notificationOccurred(.success) }
                audio.ready()
            }
            if value == .downswing { audio.whoosh(power: max(0.3, round.power)) }
        }
    }

    private var controller: some View {
        VStack(spacing: 0) {
            HStack {
                VStack(alignment: .leading, spacing: 4) {
                    Text(tv.active ? "TV CONNECTED" : "PHONE CONTROLLER")
                        .font(.caption.bold()).foregroundStyle(.mint)
                        .accessibilityIdentifier("controllerDisplayStatus")
                    Text(player.name).font(.title2.bold())
                }
                Spacer()
                Button { paused.toggle() } label: {
                    Image(systemName: paused ? "play.fill" : "pause.fill").frame(width: 44, height: 44)
                }.accessibilityLabel(paused ? "Resume round" : "Pause round").accessibilityIdentifier("pauseRound")
                Menu { menuItems } label: {
                    Image(systemName: "ellipsis").frame(width: 44, height: 44)
                }.accessibilityLabel("Course menu").accessibilityIdentifier("courseMenu")
            }.padding(.horizontal, 18).padding(.top, 8)
            ScrollView {
                VStack(alignment: .leading, spacing: 16) {
                    holeChip
                    landingTarget
                    if !tv.active {
                        courseView.frame(height: 200).clipShape(RoundedRectangle(cornerRadius: 18))
                    }
                    if paused {
                        Text("Paused · resume when ready").font(.headline)
                    }
                    if round.canSwing || motion.isArmed {
                        Text("\(round.lie.rawValue.capitalized) · \(Int(round.wind.speedMPH.rounded())) MPH wind · \(Int(round.wind.bearing.rounded()))°")
                            .font(.caption.bold())
                        TargetMap(round: round, interactive: controlsEnabled)
                            .frame(height: tv.active ? 210 : 140)
                            .allowsHitTesting(controlsEnabled)
                        controllerSetup.disabled(!controlsEnabled)
                        ShotStrengthGuide(round: round)
                        if let read = round.greenRead { Text(read.label).font(.caption.bold()).foregroundStyle(.mint) }
                    }
                    resultPanel
                    if let impact = round.practiceImpact {
                        Text("Practice swing · \(Int(impact.power * 100))% power · no stroke counted")
                            .accessibilityIdentifier("practiceResult")
                    }
                    Text("Short, gentle swings only. Keep a secure grip; a wrist strap is recommended. No camera needed.")
                        .font(.caption).foregroundStyle(.secondary)
                }.padding(18)
            }
            if round.canSwing, !paused {
                inputPanel.padding(.horizontal, 18).padding(.bottom, 12)
            } else if round.phase == .flying {
                Text(round.isReplay ? "Replay on screen" : "Ball in play")
                    .font(.headline).padding()
            }
        }
        .accessibilityElement(children: .contain)
        .accessibilityIdentifier("phoneController")
    }

    private var controllerSetup: some View {
        VStack(alignment: .leading, spacing: 12) {
            Picker("Club", selection: $round.club) {
                ForEach(GolfClub.allCases) {
                    Text("\($0.displayName) · \(Int($0.referenceDistanceYards)) yd").tag($0)
                }
            }.accessibilityIdentifier("controllerClub")
            HStack {
                Button { round.adjustAim(-round.aimStep) } label: {
                    Label("Left", systemImage: "arrow.left").frame(minHeight: 44)
                }.accessibilityIdentifier("aimLeft")
                Spacer()
                Text(String(format: "AIM %+.1f°", round.combinedAim))
                    .font(.headline.monospacedDigit()).accessibilityIdentifier("controllerAim")
                Spacer()
                Button { round.adjustAim(round.aimStep) } label: {
                    Label("Right", systemImage: "arrow.right").frame(minHeight: 44)
                }.accessibilityIdentifier("aimRight")
            }
            HStack {
                Button("Aim at pin") { round.aimAtPin() }
                Spacer()
                Button("Follow fairway") { round.followFairway() }
            }.font(.caption.bold())
            Text("Tap the map to choose any target; use arrows for fine aim.")
                .font(.caption).foregroundStyle(.secondary)
            if round.club != .putter {
                Picker("Shot", selection: $round.shotType) {
                    ForEach(ShotType.allCases.filter { $0.supports(club: round.club, lie: round.lie) }) {
                        Text($0.title).tag($0)
                    }
                }.accessibilityIdentifier("controllerShotType")
            }
            Button("Shot shape, trajectory & target", systemImage: "scope") { present(.shot) }
                .accessibilityIdentifier("shotControls")
        }
        .padding(14).background(.white.opacity(0.06), in: RoundedRectangle(cornerRadius: 16))
    }

    private var touchCourse: some View {
        GeometryReader { proxy in
            ZStack {
                courseView.ignoresSafeArea()
                VStack(alignment: .leading, spacing: 8) {
                    holeChip
                    landingTarget
                    HoleDirectionCue(round: round).frame(width: 220)
                    if round.canSwing { ShotStrengthGuide(round: round).frame(width: 220) }
                    if let read = round.greenRead {
                        Text(read.label).font(.caption.bold()).foregroundStyle(.mint)
                            .accessibilityIdentifier("greenRead")
                    }
                }.frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading).padding(12)
                HoleOverview(round: round)
                    .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topTrailing).padding(12)
                ClubRail(round: round, focusedClub: round.canSwing ? round.club : nil) { menuItems }
                    .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .trailing).padding(.trailing, 10)
                VStack {
                    Spacer()
                    if round.canSwing { inputPanel }
                    resultPanel
                }
                .frame(width: min(proxy.size.width - 100, 470))
                .padding(.bottom, 16).padding(.trailing, 80)
            }
        }
    }

    private var courseView: some View {
        CourseSceneView(scene: scene, inputs: sceneInputs, externalDisplayActive: tv.active, ownsLifecycle: false)
            .accessibilityElement()
            .accessibilityLabel("\(round.course.name), hole \(round.hole.number)")
            .accessibilityValue(resourceDiagnostics)
            .accessibilityIdentifier("golfCourse")
    }

    private var holeChip: some View {
        Text("\(player.name) · H\(round.hole.number) · PAR \(round.hole.par) · \(Int(round.distanceToPin.rounded())) YD · \(round.strokes) SHOTS")
            .font(.caption.bold()).monospacedDigit()
            .accessibilityIdentifier("holeChip")
    }

    private var landingTarget: some View {
        Text("\(round.targetLabel) · \(Int(round.distanceToTarget.rounded())) YD")
            .font(.caption.bold()).monospacedDigit()
            .padding(8).background(.black.opacity(0.4), in: Capsule())
            .accessibilityIdentifier("landingTarget")
    }

    @ViewBuilder private var resultPanel: some View {
        switch round.phase {
        case .complete:
            RoundCompletePanel(round: round, players: players, onPlayAgain: playAgain, onMenu: quit)
        case .holed:
            if let shot = round.activeShot {
                HoleCompletePanel(round: round, player: player, shot: shot, isMultiplayer: players.count > 1,
                    onReplay: { round.replay() }, onContinue: { stopFeedback(); round.continueAfterHole() })
            }
        case .landed:
            if let shot = round.activeShot {
                ShotResultPanel(round: round, shot: shot, showsStrike: false,
                    onReplay: { round.replay() }, onNext: { round.nextShot() })
            }
        case .ready, .charging, .flying: EmptyView()
        }
    }

    @ViewBuilder private var inputPanel: some View {
        if flyoverProgress != nil {
            Button("Finish hole tour",systemImage:"flag.checkered") { stopFeedback() }
                .buttonStyle(.borderedProminent).accessibilityIdentifier("finishFlyover")
        } else if swingInput == .touch {
            SwingPad(club: round.club, power: round.power, charging: round.phase == .charging,
                onCharge: { guard demoTask == nil, releaseTask == nil, round.canSwing else { return }; charge($0) },
                onRelease: { direction in
                    guard demoTask == nil else { return }
                    release(execution: SwingImpact(power: round.power, startLineDegrees: direction))
                }, onCancel: {
                    if releaseTask == nil { stopFeedback() }
                }, onDemo: demo)
        } else {
            VStack(spacing: 8) {
                Text(motionCopy).font(.headline).accessibilityIdentifier("motionPanel")
                if motion.status == .unavailable {
                    Button("Use touch controls") { swingInput = .touch }.accessibilityIdentifier("useTouch")
                } else {
                    Button {
                        if motion.isArmed { stopFeedback() }
                        else if controlsEnabled {
                            motion.handedness = player.handedness
                            motion.selectedAimDegrees = round.heading + round.combinedAim
                            motion.arm()
                        }
                    } label: {
                        Text(motion.isArmed ? "Cancel swing" : "Ready")
                            .font(.headline).frame(maxWidth: .infinity).frame(height: 64)
                            .background(motion.isArmed ? Color.orange : Color.mint, in: RoundedRectangle(cornerRadius: 18))
                            .foregroundStyle(Palette.ink)
                    }
                        .buttonStyle(.plain)
                        .disabled(!motion.isArmed && !controlsEnabled)
                        .accessibilityIdentifier("armSwing")
                        .accessibilityHint(motion.isArmed ? "Cancels tracking without taking a shot." : "Tap once, steady the phone until it vibrates, then swing and follow through. No holding required.")
                    Text("Tap Ready once. Steady the phone until it vibrates, then swing gently and follow through. No need to hold the screen.")
                        .font(.caption).foregroundStyle(.secondary)
                }
            }
        }
    }

    @ViewBuilder private var menuItems: some View {
        Picker("Swing input", selection: $swingInput) {
            ForEach(SwingInput.allCases) { Text($0.title).tag($0) }
        }
        Button("TV / AirPlay", systemImage: "airplayvideo") { present(.tv) }
        Button("Controller settings", systemImage: "gearshape") { present(.controller) }
        Button("View hole flyover",systemImage:"binoculars") {
            stopFeedback();flyoverStarted=Date();flyoverProgress=0
        }.disabled(round.phase != .ready || paused).accessibilityIdentifier("viewFlyover")
        Button(paused ? "Resume round" : "Pause round", systemImage: paused ? "play" : "pause") { paused.toggle() }
        Button("Restart hole", systemImage: "arrow.counterclockwise") { stopFeedback(); round.restartHole() }
        Button("Restart round", systemImage: "arrow.triangle.2.circlepath") { playAgain() }
        Toggle("Sound effects", isOn: $sound)
        Toggle("Haptics", isOn: $haptics)
        Button("Quit to menu", systemImage: "house", role: .destructive) { quit() }
    }

    private var motionCopy: String {
        switch motion.status {
        case .unavailable: "Motion unavailable · use touch"
        case .idle: "Choose club and aim, then tap Ready"
        case .settling: "Tracking · steady the phone for a moment"
        case .address: "Ready · swing freely, no buttons to hold"
        case .backswing: "Backswing · \(Int(round.power * 100))%"
        case .downswing: "Swing through"
        case .followThrough: "Ball hit · finish your follow-through"
        }
    }

    private func start() {
        if started {
            tv.present(round: round, scene: scene)
            synchronizeActivity()
            return
        }
        started = true
        round.start(course: practice ? .easy : flow.course, playerCount: players.count)
        round.practiceMode = practice
        round.automaticProgression = true
        round.automaticAim = false
        #if DEBUG
        if ProcessInfo.processInfo.arguments.contains("-startOnGreen") { round.dropOnGreenForTesting() }
        if ProcessInfo.processInfo.arguments.contains("-manualProgression") { round.automaticProgression = false }
        #endif
        feedback.isEnabled = haptics
        audio.enabled = sound
        motion.sensitivity = sensitivity
        motion.setClub(round.club)
        motion.onEvent = handleSwing
        scene.onBystanderHit = { audio.thump() }
        scene.onLanding = { audio.landing($0) }
        beginTurn()
        scene.inputs = sceneInputs
        scene.start()
        tv.present(round: round, scene: scene)
        synchronizeActivity()
    }

    private func beginTurn() {
        stopFeedback()
        round.handedness = player.handedness
        round.setManualAim(0)
        motion.setClub(round.club)
        scene.configurePlayer(calibration: nil, handedness: player.handedness, frameAspect: 0.75)
    }

    private func present(_ destination: Sheet) {
        stopFeedback()
        round.pause()
        motion.stop()
        sheet = destination
        if destination == .shot { round.editingShot = true }
    }

    private func resumeAfterSheet() {
        round.editingShot = false
        synchronizeActivity()
    }

    private func synchronizeActivity() {
        stopFeedback()
        let live = appPhase == .active && sheet == nil && !paused
        UIApplication.shared.isIdleTimerDisabled = live
        if live {
            round.resume()
            scene.start()
            if swingInput == .phone { motion.start() } else { motion.stop() }
        } else {
            round.pause()
            motion.stop()
            scene.stop()
        }
    }

    private var sceneInputs: SceneInputs {
        SceneInputs(hole: round.hole, ball: round.ball, heading: round.heading,
            distanceToPin: round.distanceToPin, lie: round.lie, club: round.club,
            aim: round.combinedAim, handedness: player.handedness, shot: round.activeShot,
            isReplay: round.isReplay, flightStart: round.flightStart, pausedAt: round.pausedAt,
            swingAngle: authoredDownswingAngle ?? round.power * 150,
            bystanders: players.filter { $0.id != player.id }.map {
                SceneInputs.Bystander(id: $0.id, colorIndex: $0.colorIndex, appearance: $0.golferAppearance)
            },
            preview: round.canSwing ? round.trajectoryPreview : nil,
            cameraAim: nil, reduceMotion: reduceMotion, appearance: player.golferAppearance,flyoverProgress:flyoverProgress)
    }

    private var resourceDiagnostics: String {
        #if DEBUG
        "scene=\(CourseScene.initializationCount);audio=\(RangeAudio.initializationCount);haptics=\(SwingFeedbackController.initializationCount)"
        #else
        ""
        #endif
    }

    /// Simulator-only presentation inspection; never pretends a receiver is connected.
    private var tvPreviewSession: TVGameSession? {
        #if DEBUG
        if ProcessInfo.processInfo.arguments.contains("-tvPresentationPreview") { return tv.session }
        #endif
        return nil
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

    private func playAgain() { stopFeedback(); round.restart(); beginTurn() }
    private func quit() { stopFeedback(); flow.quitToMenu() }

    private func handleSwing(_ event: SwingInputEvent) {
        guard demoTask == nil, sheet == nil, !paused, appPhase == .active,flyoverProgress == nil else { return }
        switch event {
        case .load(let value): if round.canSwing { charge(value) }
        case .cancel: if round.phase == .charging { stopFeedback() }
        case .impact(let impact):
            guard round.canSwing else { return }
            round.charge(impact.power)
            // Phone impact is authoritative now; do not add a delayed animation before flight.
            commitRelease(execution: impact)
        }
    }

    private func charge(_ power: Double) {
        guard releaseTask == nil else { return }
        if round.phase == .ready { feedback.beginBackswing(interactive: true) }
        round.charge(power)
        feedback.updateTension(round.power)
        audio.tension(round.power)
    }

    private func release(execution: SwingImpact) {
        guard releaseTask == nil else { return }
        if round.phase == .charging, round.power > 0, !reduceMotion {
            let top = round.power * 150
            releaseTask = Task { @MainActor in
                defer { releaseTask = nil; authoredDownswingAngle = nil }
                for frame in 0...12 {
                    guard !Task.isCancelled else { return }
                    let t = Double(frame) / 12
                    authoredDownswingAngle = top * (1 - t * t)
                    do { try await Task.sleep(for: .milliseconds(15)) } catch { return }
                }
                commitRelease(execution: execution)
            }
        } else { commitRelease(execution: execution) }
    }

    private func commitRelease(execution: SwingImpact) {
        feedback.endBackswing()
        audio.stop()
        if round.release(execution: execution) {
            feedback.playImpact()
            audio.impact(club: round.club, strike: execution.strike, power: execution.power)
        }
        scene.inputs = sceneInputs
    }

    private func demo() {
        guard round.canSwing, demoTask == nil, releaseTask == nil else { return }
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
        flyoverStarted=nil;flyoverProgress=nil
        motion.disarm()
        releaseTask?.cancel()
        releaseTask = nil
        authoredDownswingAngle = nil
        demoTask?.cancel()
        demoTask = nil
        feedback.endBackswing()
        audio.stop()
        round.cancelCharge()
    }
}
