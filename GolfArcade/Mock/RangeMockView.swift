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

struct RangeMockView: View {
    @StateObject private var round = RangeRound()
    @StateObject private var motion = PhoneSwingController()
    @ObservedObject private var camera: CameraSwingController
    @State private var meadow = MeadowScene(course: .easy)
    @State private var selectedCourse: GolfCourse?
    @State private var pendingCourse: GolfCourse?
    @State private var audio = RangeAudio()
    @State private var feedback = SwingFeedbackController()
    @State private var demoTask: Task<Void, Never>?
    @State private var cameraPresented = false
    @State private var didApplyInitialInput = false
    @AppStorage("arcade.hapticsEnabled") private var haptics = true
    @AppStorage("range.soundEnabled") private var sound = true
    @AppStorage("range.swingInput") private var swingInput: SwingInput = .touch
    @AppStorage("range.handedness") private var handedness: Handedness = .right
    @Environment(\.scenePhase) private var appPhase

    private let tick = Timer.publish(every: 1.0 / 30, on: .main, in: .common).autoconnect()
    private let cream = Color(red: 0.96, green: 0.96, blue: 0.86)
    private let ink = Color(red: 0.06, green: 0.18, blue: 0.16)
    private let calibration: PlayerCalibration
    private let startsInCameraMode: Bool
    private let onRecalibrate: () -> Void

    init(
        camera: CameraSwingController,
        calibration: PlayerCalibration,
        startsInCameraMode: Bool = false,
        onRecalibrate: @escaping () -> Void
    ) {
        self.camera = camera
        self.calibration = calibration
        self.startsInCameraMode = startsInCameraMode
        self.onRecalibrate = onRecalibrate
    }

    var body: some View {
        ZStack {
            ink.ignoresSafeArea()
            if let course = selectedCourse {
                courseView(course).transition(.opacity)
            } else {
                CourseSelectionView(selection: $pendingCourse) { begin($0) }
                    .transition(.opacity)
            }
        }
        .foregroundStyle(cream)
        .fullScreenCover(isPresented: $cameraPresented, onDismiss: { round.resume() }) {
            NavigationStack {
                ArcadeView(calibration: calibration, onRecalibrate: {
                    cameraPresented = false
                    onRecalibrate()
                })
                .toolbar {
                    ToolbarItem(placement: .topBarTrailing) {
                        Button("Back to course") { cameraPresented = false }
                    }
                }
            }
        }
        .onReceive(tick) { date in
            guard appPhase == .active, !cameraPresented, selectedCourse != nil else { return }
            let wasFlying = round.phase == .flying
            round.advance(at: date)
            if wasFlying, round.phase != .flying, !round.isReplay { audio.celebrate() }
        }
        .onAppear {
            if startsInCameraMode, !didApplyInitialInput {
                didApplyInitialInput = true
                swingInput = .camera
            }
            feedback.isEnabled = haptics
            audio.enabled = sound
            motion.setClub(round.club)
            motion.onEvent = { handleSwing($0) }
            camera.handedness = handedness
            camera.onEvent = { handleSwing($0) }
            updateInputs()
        }
        .onChange(of: haptics) { _, value in feedback.isEnabled = value }
        .onChange(of: sound) { _, value in audio.enabled = value }
        .onChange(of: round.club) { _, club in motion.setClub(club) }
        .onChange(of: swingInput) { _, _ in stopFeedback(); updateInputs() }
        .onChange(of: handedness) { _, value in camera.handedness = value }
        .onChange(of: cameraPresented) { _, _ in updateInputs() }
        .onChange(of: selectedCourse) { _, _ in updateInputs() }
        .onChange(of: appPhase) { _, phase in
            if phase != .active { stopFeedback(); round.pause() }
            else { round.resume() }
            updateInputs()
        }
        .onDisappear { stopFeedback(); motion.stop(); camera.stop() }
        .preferredColorScheme(.dark)
    }

    private func courseView(_ course: GolfCourse) -> some View {
        GeometryReader { proxy in
            ZStack {
                TimelineView(.animation(
                    minimumInterval: 1.0 / 30,
                    paused: round.phase != .flying || appPhase != .active || cameraPresented
                )) { timeline in
                    MeadowSceneView(
                        meadow: meadow,
                        shot: round.activeShot,
                        elapsed: round.elapsed(at: timeline.date),
                        power: round.power,
                        aim: round.aim,
                        swingAngle: swingInput == .camera ? camera.swingAngle : round.power * 150,
                        handedness: handedness
                    )
                }
                .ignoresSafeArea()
                .accessibilityLabel("\(course.name) golf course")
                .accessibilityIdentifier("golfCourse")

                LinearGradient(colors: [.black.opacity(0.14), .clear, .black.opacity(0.24)], startPoint: .top, endPoint: .bottom)
                    .ignoresSafeArea()
                    .allowsHitTesting(false)

                floatingClubBar
                    .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .trailing)
                    .padding(.trailing, 10)

                if swingInput == .camera {
                    cameraPip
                        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .bottomLeading)
                        .padding(.leading, 12)
                        .padding(.bottom, 12)
                }

                if round.phase == .landed || round.phase == .complete {
                    resultPanel
                        .frame(maxWidth: min(proxy.size.width - 32, 470))
                        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .bottom)
                        .padding(.bottom, 16)
                } else {
                    inputPanel
                        .frame(maxWidth: swingInput == .camera ? 178 : min(proxy.size.width * 0.58, 440))
                        .frame(
                            maxWidth: .infinity,
                            maxHeight: .infinity,
                            alignment: swingInput == .camera ? .bottomTrailing : .bottom
                        )
                        .padding(.trailing, swingInput == .camera ? 72 : 0)
                        .padding(.bottom, 16)
                }
            }
        }
    }

    private var floatingClubBar: some View {
        VStack(spacing: 7) {
            Menu {
                Picker("Swing input", selection: $swingInput) {
                    ForEach(SwingInput.allCases) { Text($0.title).tag($0) }
                }
                Picker("Handedness", selection: $handedness) {
                    ForEach(Handedness.allCases) { Text("\($0.displayName)-handed").tag($0) }
                }
                Toggle("Sound effects", isOn: $sound)
                Toggle("Haptics", isOn: $haptics)
                Button("Choose another course", systemImage: "map") { leaveCourse() }
                Button("Camera lab", systemImage: "camera") {
                    stopFeedback(); round.pause(); cameraPresented = true
                }
                Button("Recalibrate player", systemImage: "viewfinder", action: onRecalibrate)
                Button("Restart round", systemImage: "arrow.counterclockwise") { stopFeedback(); round.restart() }
            } label: {
                Image(systemName: "ellipsis").frame(width: 45, height: 42)
            }
            .accessibilityLabel("Course settings")

            Divider().overlay(.white.opacity(0.18))

            ForEach(GolfClub.allCases) { club in
                Button { round.club = club } label: {
                    VStack(spacing: 3) {
                        Image(systemName: club.symbol).font(.system(size: 17, weight: .bold))
                        Text(shortName(club)).font(.system(size: 9, weight: .black, design: .rounded))
                    }
                    .frame(width: 45, height: 48)
                    .background(round.club == club ? cream : .white.opacity(0.08), in: RoundedRectangle(cornerRadius: 11))
                    .foregroundStyle(round.club == club ? ink : cream)
                }
                .disabled(round.phase != .ready)
                .accessibilityLabel("\(club.displayName), \(Int(club.mockDistance)) yards")
                .accessibilityIdentifier("club-\(club.rawValue)")
            }

            Divider().overlay(.white.opacity(0.18))

            Button { round.aim = max(-22, round.aim - 2) } label: {
                Image(systemName: "arrow.left").frame(width: 45, height: 34)
            }
            .disabled(round.phase != .ready)
            Button { round.aim = min(22, round.aim + 2) } label: {
                Image(systemName: "arrow.right").frame(width: 45, height: 34)
            }
            .disabled(round.phase != .ready)
        }
        .frame(width: 59)
        .padding(7)
        .background(.black.opacity(0.62), in: RoundedRectangle(cornerRadius: 18))
        .overlay(RoundedRectangle(cornerRadius: 18).stroke(.white.opacity(0.12)))
        .padding(.vertical, 20)
    }

    @ViewBuilder private var inputPanel: some View {
        switch swingInput {
        case .touch:
            swingPad
        case .phone:
            statusPill(icon: "iphone.gen3.radiowaves.left.and.right", title: motionCopy.0, detail: motionCopy.1)
                .accessibilityIdentifier("motionPanel")
        case .camera:
            statusPill(icon: "figure.golf", title: cameraTitle, detail: cameraDetail)
                .accessibilityIdentifier("cameraPanel")
        }
    }

    private var swingPad: some View {
        ZStack(alignment: .leading) {
            RoundedRectangle(cornerRadius: 18).fill(.black.opacity(0.66))
            GeometryReader { geometry in
                RoundedRectangle(cornerRadius: 18)
                    .fill(LinearGradient(colors: [.mint.opacity(0.32), .yellow.opacity(0.58)], startPoint: .leading, endPoint: .trailing))
                    .frame(width: geometry.size.width * round.power)
            }
            HStack(spacing: 12) {
                Image(systemName: "arrow.down").font(.title2)
                Text(round.phase == .charging ? "Release to swing" : "Pull down to swing").font(.headline)
                Spacer()
                Text("\(Int(round.power * 100))%").font(.title2.bold()).monospacedDigit()
            }
            .padding(16)
        }
        .frame(height: 74)
        .contentShape(Rectangle())
        .gesture(DragGesture(minimumDistance: 3).onChanged { value in
            guard demoTask == nil, round.canSwing else { return }
            charge(max(0, Double(value.translation.height)) / 100)
        }.onEnded { _ in
            guard demoTask == nil else { return }
            release()
        })
        .onTapGesture {
            guard demoTask == nil, round.phase == .charging else { return }
            stopFeedback()
        }
        .accessibilityElement(children: .ignore)
        .accessibilityLabel("Swing pad. Pull down and release to swing.")
        .accessibilityValue("\(Int(round.power * 100)) percent power")
        .accessibilityIdentifier("swingPad")
        .accessibilityAction(named: "Swing at 75 percent") { demo() }
    }

    private func statusPill(icon: String, title: String, detail: String) -> some View {
        HStack(spacing: 12) {
            Image(systemName: icon).font(.title2)
            VStack(alignment: .leading, spacing: 2) {
                Text(title)
                    .font(.subheadline.bold())
                    .lineLimit(1)
                    .minimumScaleFactor(0.72)
                Text(detail).font(.caption).foregroundStyle(.white.opacity(0.68)).lineLimit(1)
            }
            Spacer(minLength: 0)
        }
        .padding(.horizontal, 15)
        .padding(.vertical, 11)
        .background(.black.opacity(0.66), in: Capsule())
        .overlay(Capsule().stroke(.white.opacity(0.12)))
        .accessibilityElement(children: .combine)
    }

    private var cameraPip: some View {
        ZStack(alignment: .topTrailing) {
            Color.black
            if case .running = camera.status {
                CameraPreview(session: camera.tracker.session, videoRotationAngle: camera.tracker.videoRotationAngle, isVideoMirrored: camera.tracker.isVideoMirrored)
                PoseSkeletonView(frame: camera.frame, frameAspect: camera.tracker.frameAspect, contentMode: .fit)
                VirtualBallGuide(calibration: calibration, handedness: handedness, frameAspect: camera.tracker.frameAspect, strike: camera.lastStrike)
            } else {
                Image(systemName: "person.crop.rectangle").font(.title2).opacity(0.5)
            }
            if let strike = camera.lastStrike {
                Text(strike.displayName.uppercased())
                    .font(.system(size: 8, weight: .black, design: .rounded))
                    .padding(.horizontal, 6).padding(.vertical, 4)
                    .background(strike == .center ? Color.mint : Color.orange, in: Capsule())
                    .foregroundStyle(ink)
                    .padding(6)
            }
            VStack {
                Spacer()
                HStack {
                    Spacer()
                    Text("\(Int(round.power * 100))%")
                        .font(.caption.bold()).monospacedDigit()
                        .padding(.horizontal, 7).padding(.vertical, 4)
                        .background(.black.opacity(0.72), in: Capsule())
                        .padding(6)
                }
            }
        }
        .aspectRatio(camera.tracker.frameAspect, contentMode: .fit)
        .frame(width: 132)
        .clipShape(RoundedRectangle(cornerRadius: 13))
        .overlay(RoundedRectangle(cornerRadius: 13).strokeBorder(camera.phase == .findingPlayer ? .white.opacity(0.5) : .mint, lineWidth: 2))
        .shadow(color: .black.opacity(0.35), radius: 8, y: 4)
        .accessibilityLabel("Player camera with virtual ball alignment guide")
        .accessibilityIdentifier("cameraPip")
    }

    private var resultPanel: some View {
        VStack(spacing: 11) {
            if round.phase == .complete {
                HStack {
                    VStack(alignment: .leading, spacing: 3) {
                        Text("ROUND COMPLETE").font(.caption2.bold()).tracking(1.5)
                            .accessibilityIdentifier("roundComplete")
                        Text("\(round.score) points").font(.title.bold())
                            .accessibilityIdentifier("roundScore")
                    }
                    Spacer()
                    Text("BEST \(round.best)").font(.caption.bold()).foregroundStyle(.mint)
                }
            } else if let shot = round.activeShot {
                HStack {
                    VStack(alignment: .leading, spacing: 3) {
                        Text("\(shot.strike.displayName.uppercased()) CONTACT").font(.caption2.bold()).tracking(1)
                        Text(shot.lie.displayName).font(.title2.bold())
                    }
                    Spacer()
                    VStack(alignment: .trailing, spacing: 2) {
                        Text("\(Int(shot.total)) yd").font(.title2.bold()).monospacedDigit()
                        Text("+\(shot.points) points").font(.caption.bold()).foregroundStyle(.mint)
                    }
                }
            }
            HStack(spacing: 9) {
                Button { round.replay() } label: {
                    Label("Replay", systemImage: "arrow.clockwise")
                        .frame(maxWidth: .infinity).padding(.vertical, 12)
                        .background(.white.opacity(0.1), in: RoundedRectangle(cornerRadius: 12))
                }
                .accessibilityIdentifier("replayShot")
                Button { round.phase == .complete ? round.restart() : round.nextShot() } label: {
                    Text(round.phase == .complete ? "Play again" : "Next shot")
                        .frame(maxWidth: .infinity).padding(.vertical, 12)
                        .background(cream, in: RoundedRectangle(cornerRadius: 12)).foregroundStyle(ink)
                }
                .accessibilityIdentifier(round.phase == .complete ? "playAgain" : "nextShot")
            }
            .font(.subheadline.bold())
        }
        .padding(16)
        .background(.black.opacity(0.74), in: RoundedRectangle(cornerRadius: 20))
        .overlay(RoundedRectangle(cornerRadius: 20).stroke(.white.opacity(0.12)))
    }

    private var motionCopy: (String, String) {
        if round.phase == .flying { return ("Nice swing", "Watch the ball") }
        switch motion.status {
        case .unavailable: return ("Needs a real iPhone", "Motion sensors unavailable")
        case .idle, .settling: return ("Hold still at address", "Grip the phone like a club")
        case .address: return ("Ready", "Take it back, then swing through")
        case .backswing: return ("Backswing", "Keep loading")
        case .downswing: return ("Swing", "Drive through the ball")
        }
    }

    private var cameraDetail: String {
        switch camera.phase {
        case .findingPlayer: "Line your club up with the virtual ball"
        case .address: "Hold still, then take it back"
        case .backswing: "Keep your eyes on the ball"
        case .downswing, .impact: "Strike through the ball"
        case .followThrough, .finish: "Finish the swing"
        }
    }

    private var cameraTitle: String {
        if round.phase == .flying { return "Ball in flight" }
        return switch camera.status {
        case .idle, .requestingPermission: "Starting camera"
        case .denied: "Camera access is off"
        case .unavailable(let reason): reason
        case .running: camera.phase.displayName
        }
    }

    private func begin(_ course: GolfCourse) {
        stopFeedback()
        round.selectCourse(course)
        meadow = MeadowScene(course: course)
        withAnimation(.easeInOut(duration: 0.25)) { selectedCourse = course }
    }

    private func leaveCourse() {
        stopFeedback()
        motion.stop()
        camera.stop()
        pendingCourse = nil
        withAnimation(.easeInOut(duration: 0.25)) { selectedCourse = nil }
    }

    private func updateInputs() {
        let live = appPhase == .active && !cameraPresented && selectedCourse != nil
        if live, swingInput == .phone { motion.start() } else { motion.stop() }
        if live, swingInput == .camera { camera.start() } else { camera.stop() }
    }

    private func handleSwing(_ event: SwingInputEvent) {
        guard demoTask == nil else { return }
        switch event {
        case .load(let value):
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

    private func shortName(_ club: GolfClub) -> String {
        switch club {
        case .driver: "DR"
        case .iron: "7I"
        case .wedge: "SW"
        case .putter: "PT"
        }
    }
}

private struct CourseSelectionView: View {
    @Binding var selection: GolfCourse?
    let onPlay: (GolfCourse) -> Void

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 22) {
                VStack(alignment: .leading, spacing: 6) {
                    Text("CHOOSE A COURSE")
                        .font(.system(size: 12, weight: .black, design: .rounded)).tracking(2)
                        .foregroundStyle(.mint)
                    Text("Where are you playing?").font(.system(size: 34, weight: .black, design: .rounded))
                }

                VStack(spacing: 12) {
                    ForEach(GolfCourse.all) { course in
                        Button { selection = course } label: {
                            HStack(spacing: 14) {
                                Image(systemName: courseIcon(course))
                                    .font(.title2.bold())
                                    .frame(width: 46, height: 46)
                                    .background(courseColor(course).opacity(0.2), in: Circle())
                                    .foregroundStyle(courseColor(course))
                                VStack(alignment: .leading, spacing: 4) {
                                    Text(course.name).font(.title3.bold())
                                    Text("\(course.difficulty.displayName) · Par \(course.par) · \(Int(course.holeDistance)) yd")
                                        .font(.caption).foregroundStyle(.white.opacity(0.62))
                                    Text(hazardDescription(course)).font(.caption2).foregroundStyle(.white.opacity(0.48))
                                }
                                Spacer()
                                Image(systemName: selection == course ? "checkmark.circle.fill" : "circle")
                                    .font(.title2).foregroundStyle(selection == course ? .mint : .white.opacity(0.28))
                            }
                            .padding(16)
                            .background(.white.opacity(selection == course ? 0.13 : 0.06), in: RoundedRectangle(cornerRadius: 18))
                            .overlay(RoundedRectangle(cornerRadius: 18).stroke(selection == course ? Color.mint : .white.opacity(0.09), lineWidth: 1.5))
                        }
                        .buttonStyle(.plain)
                        .accessibilityIdentifier("course-\(course.difficulty.rawValue)")
                    }
                }

                Button {
                    if let selection { onPlay(selection) }
                } label: {
                    Text(selection.map { "Play \($0.name)" } ?? "Select a course")
                        .font(.headline)
                        .frame(maxWidth: .infinity)
                        .padding(.vertical, 15)
                        .background(selection == nil ? .white.opacity(0.12) : Color.mint, in: RoundedRectangle(cornerRadius: 15))
                        .foregroundStyle(selection == nil ? .white.opacity(0.45) : Color(red: 0.06, green: 0.18, blue: 0.16))
                }
                .disabled(selection == nil)
                .accessibilityIdentifier("playSelectedCourse")
            }
            .padding(24)
            .frame(maxWidth: 620)
            .frame(maxWidth: .infinity)
        }
        .background(
            LinearGradient(colors: [Color(red: 0.05, green: 0.20, blue: 0.17), Color(red: 0.02, green: 0.08, blue: 0.08)], startPoint: .topLeading, endPoint: .bottomTrailing)
                .ignoresSafeArea()
        )
        .accessibilityIdentifier("courseSelection")
    }

    private func courseColor(_ course: GolfCourse) -> Color {
        switch course.difficulty {
        case .easy: .mint
        case .medium: .yellow
        case .hard: .orange
        }
    }

    private func courseIcon(_ course: GolfCourse) -> String {
        switch course.difficulty {
        case .easy: "leaf.fill"
        case .medium: "tree.fill"
        case .hard: "water.waves"
        }
    }

    private func hazardDescription(_ course: GolfCourse) -> String {
        let bunkers = course.hazards.filter { $0.kind == .bunker }.count
        let water = course.hazards.contains { $0.kind == .water }
        if water { return "Water crossing · \(bunkers) bunkers" }
        return bunkers == 1 ? "1 greenside bunker" : "\(bunkers) bunkers"
    }
}

private struct VirtualBallGuide: View {
    let calibration: PlayerCalibration
    let handedness: Handedness
    let frameAspect: CGFloat
    let strike: StrikeQuality?

    var body: some View {
        Canvas { context, size in
            let side: CGFloat = handedness == .right ? 1 : -1
            let normalized = CGPoint(
                x: min(max(CGFloat(calibration.anchor.shoulderCenterX) + side * 0.16, 0.08), 0.92),
                y: min(max(CGFloat(calibration.anchor.shoulderCenterY - calibration.anchor.height * 0.72), 0.06), 0.32)
            )
            let center = PoseSkeletonMapper.screenPoint(normalized, in: size, frameAspect: frameAspect, fitsEntireFrame: true)
            let color: Color = strike == nil || strike == .center ? .mint : strike == .miss ? .red : .orange
            context.stroke(Path(ellipseIn: CGRect(x: center.x - 12, y: center.y - 12, width: 24, height: 24)), with: .color(color.opacity(0.8)), lineWidth: 2)
            context.fill(Path(ellipseIn: CGRect(x: center.x - 5, y: center.y - 5, width: 10, height: 10)), with: .color(.white))
            var line = Path()
            line.move(to: CGPoint(x: center.x - 18, y: center.y))
            line.addLine(to: CGPoint(x: center.x + 18, y: center.y))
            context.stroke(line, with: .color(color.opacity(0.65)), lineWidth: 1)
        }
        .allowsHitTesting(false)
    }
}
