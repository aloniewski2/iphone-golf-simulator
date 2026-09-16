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
    @StateObject private var camera = CameraSwingController()
    @State private var meadow = MeadowScene()
    @State private var audio = RangeAudio()
    @State private var feedback = SwingFeedbackController()
    @State private var demoTask: Task<Void, Never>?
    @State private var cameraPresented = false
    @State private var helpPresented = false
    @AppStorage("arcade.hapticsEnabled") private var haptics = true
    @AppStorage("range.soundEnabled") private var sound = true
    @AppStorage("range.swingInput") private var swingInput: SwingInput = .touch
    @AppStorage("range.handedness") private var handedness: Handedness = .right
    @AppStorage("range.playHole") private var playHole = false
    @AppStorage("range.benchmark3D") private var benchmark3D = false
    @Environment(\.scenePhase) private var appPhase
    private let tick = Timer.publish(every: 1.0 / 30, on: .main, in: .common).autoconnect()
    private let cream = Color(red: 0.96, green: 0.96, blue: 0.86)
    private let ink = Color(red: 0.06, green: 0.18, blue: 0.16)

    var body: some View {
        GeometryReader { size in
            ZStack {
                ink.ignoresSafeArea()
                VStack(spacing: 0) {
                    header
                    ZStack(alignment: .top) {
                        TimelineView(.animation(minimumInterval: 1.0 / 30, paused: round.phase != .flying || appPhase != .active || cameraPresented)) { timeline in
                            MeadowSceneView(meadow: meadow, shot: round.activeShot, elapsed: round.elapsed(at: timeline.date), power: round.power, aim: round.aim, swingAngle: swingInput == .camera ? camera.swingAngle : round.power * 150, handedness: handedness, ball: round.ballPosition, heading: round.baseHeading, focusYards: round.hole == nil ? 60 : round.yardsToPin, preview: round.preview)
                        }
                        .accessibilityLabel("Three dimensional Meadow Club driving range")
                        if swingInput == .camera {
                            cameraPip
                                .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .trailing)
                                .padding(.trailing, 12)
                                .padding(.top, 84)
                        }
                        TimelineView(.animation(minimumInterval: 1.0 / 20, paused: round.phase != .flying || appPhase != .active)) { timeline in
                            CourseMinimap(mode: round.mode, ball: round.ballPosition, preview: round.preview, activeShot: round.activeShot, elapsed: round.elapsed(at: timeline.date))
                        }
                        .frame(width: 92, height: 136)
                        .overlay(RoundedRectangle(cornerRadius: 10).strokeBorder(Color.white.opacity(0.35), lineWidth: 1))
                        .overlay(alignment: .bottom) {
                            if let preview = round.preview, round.canSwing {
                                Text("\(Int(preview.total)) YD")
                                    .font(.system(size: 9, weight: .heavy, design: .rounded)).monospacedDigit()
                                    .padding(.horizontal, 6).padding(.vertical, 3)
                                    .background(ink.opacity(0.9), in: Capsule())
                                    .offset(y: 10)
                            }
                        }
                        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .leading)
                        .padding(.leading, 12)
                        .padding(.top, 84)
                        .accessibilityIdentifier("minimap")
                        VStack(spacing: 8) {
                            HStack {
                                Label(round.hole.map { "HOLE \($0.number) · \($0.name.uppercased())" } ?? "MEADOW CLUB", systemImage: round.hole == nil ? "sun.max.fill" : "flag.fill")
                                Spacer()
                                Text(round.hole.map { "PAR \($0.par) · \(Int($0.length)) YD" } ?? "NO WIND · ARCADE")
                            }
                            .font(.system(size: 10, weight: .heavy, design: .rounded))
                            .tracking(1)
                            .padding(12)
                            .background(ink.opacity(0.9), in: Capsule())
                            if let hole = round.hole {
                                HStack(spacing: 8) {
                                    Text("\(round.lie.displayName.uppercased())")
                                        .font(.system(size: 9, weight: .heavy, design: .rounded))
                                        .padding(.horizontal, 9).padding(.vertical, 7)
                                        .background(lieColor(round.lie), in: Capsule())
                                        .foregroundStyle(ink)
                                    Text(round.lie == .green ? "\(hole.feetToCup(from: round.ballPosition)) FT TO CUP" : "\(Int(round.yardsToPin)) YD TO PIN")
                                        .font(.system(size: 9, weight: .heavy, design: .rounded))
                                        .padding(.horizontal, 9).padding(.vertical, 7)
                                        .background(cream, in: Capsule())
                                        .foregroundStyle(ink)
                                        .accessibilityIdentifier("toPin")
                                }
                            } else {
                                HStack(spacing: 8) {
                                    ForEach(RangeTarget.all) { target in
                                    Text("\(target.name.uppercased())  \(Int(target.distance))")
                                        .font(.system(size: 9, weight: .heavy, design: .rounded))
                                        .padding(.horizontal, 9).padding(.vertical, 7)
                                        .background(targetColor(target.id), in: Capsule())
                                        .foregroundStyle(ink)
                                    }
                                }
                            }
                            Spacer()
                            if round.phase == .flying {
                                Label(round.isReplay ? "REPLAY" : "BALL IN FLIGHT", systemImage: "location.north.fill")
                                    .font(.caption.bold()).padding(10)
                                    .background(ink.opacity(0.9), in: Capsule())
                            } else if round.canSwing {
                                Text(round.phase == .charging ? "LOAD  \(Int(round.power * 100))%" : sceneHint)
                                    .font(.system(size: 11, weight: .black, design: .rounded)).tracking(1)
                                    .padding(12).background(ink.opacity(0.9), in: Capsule())
                            }
                        }
                        .padding(12)
                    }
                    .frame(minHeight: 175, maxHeight: .infinity)
                    .clipShape(RoundedRectangle(cornerRadius: 24))
                    .padding(.horizontal, 12)
                    controls
                }
            }
            .foregroundStyle(cream)
            .sheet(isPresented: $helpPresented) { instructions }
            .fullScreenCover(isPresented: $cameraPresented, onDismiss: { round.resume() }) {
                NavigationStack {
                    ArcadeView()
                        .toolbar {
                            ToolbarItem(placement: .topBarTrailing) { Button("Back to range") { cameraPresented = false } }
                        }
                }
            }
            .onReceive(tick) { date in
                guard appPhase == .active, !cameraPresented else { return }
                let wasFlying = round.phase == .flying
                round.advance(at: date)
                if wasFlying, round.phase != .flying, !round.isReplay { audio.celebrate() }
            }
            .onAppear {
                feedback.isEnabled = haptics
                audio.enabled = sound
                applyMode()
                motion.setClub(round.club)
                motion.onEvent = { handleSwing($0) }
                camera.handedness = handedness
                camera.benchmark3D = benchmark3D
                camera.onEvent = { handleSwing($0) }
                updateInputs()
            }
            .onChange(of: haptics) { _, value in feedback.isEnabled = value }
            .onChange(of: sound) { _, value in audio.enabled = value }
            .onChange(of: round.club) { _, club in motion.setClub(club) }
            .onChange(of: swingInput) { _, _ in stopFeedback(); updateInputs() }
            .onChange(of: handedness) { _, value in camera.handedness = value }
            .onChange(of: benchmark3D) { _, value in camera.benchmark3D = value }
            .onChange(of: playHole) { _, _ in stopFeedback(); applyMode() }
            .onChange(of: cameraPresented) { _, _ in updateInputs() }
            .onChange(of: appPhase) { _, phase in
                if phase != .active { stopFeedback(); round.pause() }
                else { round.resume() }
                updateInputs()
            }
            .onDisappear { stopFeedback(); motion.stop(); camera.stop() }
        }
        .preferredColorScheme(.dark)
    }

    private var header: some View {
        HStack(alignment: .center) {
            VStack(alignment: .leading, spacing: 2) {
                Text(round.hole == nil ? "FAIRWAY" : "HOLE \(round.hole!.number)").font(.system(size: 27, weight: .black, design: .rounded)).tracking(2)
                Text(round.hole == nil ? "FIVE SHOTS. FIND YOUR SWEET SPOT." : "PAR \(round.hole!.par). TEE TO CUP.")
                    .font(.system(size: 8, weight: .bold)).tracking(1)
                    .foregroundStyle(cream.opacity(0.6))
            }
            Spacer()
            VStack(alignment: .trailing, spacing: 3) {
                Text(round.hole == nil ? "\(round.score) PTS" : "\(round.strokes) \(round.strokes == 1 ? "STROKE" : "STROKES")")
                    .font(.system(size: 21, weight: .black, design: .rounded)).monospacedDigit()
                    .accessibilityIdentifier("roundScore")
                Text(round.hole == nil ? "SHOT \(round.shotNumber) / 5 · BEST \(round.best)" : "SHOT \(round.shotNumber) · BEST \(round.best == 0 ? "—" : "\(round.best)")")
                    .font(.system(size: 9, weight: .bold)).monospacedDigit()
            }
            Menu {
                Picker("Play", selection: $playHole) {
                    Label("Driving range", systemImage: "scope").tag(false)
                    Label("Hole 1 · Par 4", systemImage: "flag.fill").tag(true)
                }
                Toggle("Sound effects", isOn: $sound)
                Toggle("Haptics", isOn: $haptics)
                Picker("Handedness", selection: $handedness) {
                    ForEach(Handedness.allCases) { Text("\($0.displayName)-handed").tag($0) }
                }
                Toggle("3D pose benchmark (dev)", isOn: $benchmark3D)
                Button("How to play", systemImage: "questionmark.circle") { helpPresented = true }
                Button("Camera lab", systemImage: "camera") {
                    stopFeedback(); round.pause(); cameraPresented = true
                }
                Button(round.hole == nil ? "Restart round" : "Restart hole", systemImage: "arrow.counterclockwise") { stopFeedback(); round.restart() }
            } label: {
                Image(systemName: "slider.horizontal.3").frame(width: 36, height: 44)
            }
            .accessibilityLabel("Range settings")
        }
        .padding(.horizontal, 18).padding(.top, 8).padding(.bottom, 16)
    }

    @ViewBuilder private var controls: some View {
        if round.phase == .complete || round.phase == .landed {
            resultPanel
        } else {
            VStack(spacing: 12) {
                HStack(spacing: 7) {
                    ForEach(GolfClub.allCases) { club in
                        Button {
                            round.club = club
                        } label: {
                            VStack(spacing: 3) {
                                Text(club.displayName.uppercased()).font(.system(size: 10, weight: .heavy))
                                Text("\(Int(club.mockDistance)) YD").font(.system(size: 9, weight: .medium)).opacity(0.65)
                            }
                            .frame(maxWidth: .infinity).padding(.vertical, 10)
                            .background(round.club == club ? cream : Color.white.opacity(0.08), in: RoundedRectangle(cornerRadius: 12))
                            .foregroundStyle(round.club == club ? ink : cream)
                        }
                        .accessibilityIdentifier("club-\(club.rawValue)")
                    }
                }
                .disabled(round.phase != .ready)
                HStack(spacing: 12) {
                    Text("AIM").font(.system(size: 10, weight: .heavy)).tracking(1)
                    Slider(value: $round.aim, in: -22...22, step: 1).tint(cream)
                        .accessibilityLabel("Aim in degrees")
                        .disabled(round.phase != .ready)
                    Text(String(format: "%+.0f°", round.aim)).font(.system(size: 12, weight: .bold, design: .monospaced)).frame(width: 38)
                }
                HStack {
                    Picker("Swing input", selection: $swingInput) {
                        ForEach(SwingInput.allCases) { Text($0.title).tag($0) }
                    }
                    .pickerStyle(.segmented)
                    .frame(maxWidth: 230)
                    .accessibilityIdentifier("swingInput")
                    Spacer()
                    Button("Demo shot") { demo() }
                        .font(.system(size: 12, weight: .bold)).underline()
                        .disabled(!round.canSwing || demoTask != nil)
                        .accessibilityIdentifier("demoShot")
                }
                switch swingInput {
                case .touch: swingPad
                case .phone: motionPanel
                case .camera: cameraPanel
                }
            }
            .padding(16)
        }
    }

    private var swingPad: some View {
        ZStack(alignment: .leading) {
            RoundedRectangle(cornerRadius: 20).fill(Color.white.opacity(0.07))
            GeometryReader { geometry in
                RoundedRectangle(cornerRadius: 20)
                    .fill(LinearGradient(colors: [.mint.opacity(0.25), .yellow.opacity(0.55)], startPoint: .leading, endPoint: .trailing))
                    .frame(width: geometry.size.width * round.power)
                    .animation(.easeOut(duration: 0.08), value: round.power)
            }
            HStack {
                Image(systemName: round.phase == .flying ? "paperplane.fill" : "arrow.down")
                    .font(.system(size: 25, weight: .light))
                VStack(alignment: .leading, spacing: 4) {
                    Text(round.phase == .flying ? "Nice swing." : round.phase == .charging ? "Feel the load." : "Take your shot.")
                        .font(.system(size: 21, weight: .bold, design: .rounded))
                    Text(round.phase == .charging ? "Release at your chosen power" : "Pull farther for more distance")
                        .font(.system(size: 11)).opacity(0.65)
                }
                Spacer()
                Text("\(Int(round.power * 100))%")
                    .font(.system(size: 27, weight: .black, design: .rounded)).monospacedDigit()
            }.padding(16)
        }
        .frame(height: 104)
        .contentShape(Rectangle())
        .gesture(DragGesture(minimumDistance: 3).onChanged { value in
            guard demoTask == nil, round.canSwing else { return }
            charge(max(0, Double(value.translation.height)) / 100.0)
        }.onEnded { _ in
            guard demoTask == nil else { return }
            release()
        })
        // A drag interrupted by the system (notification banner, edge gesture, second finger) never
        // reports `onEnded`, which would leave the round loaded forever. A plain tap resets it.
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

    private var motionPanel: some View {
        let (title, detail) = motionCopy
        return ZStack(alignment: .leading) {
            RoundedRectangle(cornerRadius: 20).fill(Color.white.opacity(0.07))
            GeometryReader { geometry in
                RoundedRectangle(cornerRadius: 20)
                    .fill(LinearGradient(colors: [.mint.opacity(0.25), .yellow.opacity(0.55)], startPoint: .leading, endPoint: .trailing))
                    .frame(width: geometry.size.width * round.power)
                    .animation(.easeOut(duration: 0.08), value: round.power)
            }
            HStack {
                Image(systemName: round.phase == .flying ? "paperplane.fill" : "iphone.gen3.radiowaves.left.and.right")
                    .font(.system(size: 25, weight: .light))
                    .symbolEffect(.pulse, isActive: motion.status == .address)
                VStack(alignment: .leading, spacing: 4) {
                    Text(title).font(.system(size: 21, weight: .bold, design: .rounded))
                    Text(detail).font(.system(size: 11)).opacity(0.65)
                }
                Spacer()
                Text("\(Int(round.power * 100))%")
                    .font(.system(size: 27, weight: .black, design: .rounded)).monospacedDigit()
            }.padding(16)
        }
        .frame(height: 104)
        .accessibilityElement(children: .ignore)
        .accessibilityLabel("Phone swing. \(title) \(detail)")
        .accessibilityValue("\(Int(round.power * 100)) percent power")
        .accessibilityIdentifier("motionPanel")
    }

    private var motionCopy: (String, String) {
        if round.phase == .flying { return ("Nice swing.", "Watch it fly") }
        if demoTask != nil { return ("Demo swing.", "Hands off") }
        switch motion.status {
        case .unavailable: return ("Needs a real iPhone.", "Motion sensors are not available here")
        case .idle, .settling: return ("Hold still at address.", "Grip tight and hold the phone like a club")
        case .address: return ("Ready. Take it back.", "Swing through to hit the ball")
        case .backswing: return ("Feel the load.", "Swing through to hit the ball")
        case .downswing: return ("Swing!", "Power comes from your speed")
        }
    }

    private var sceneHint: String {
        switch swingInput {
        case .touch: "A LITTLE SWING. A BIG AFTERNOON."
        case .phone: "SWING THE PHONE LIKE A CLUB."
        case .camera: "PROP THE PHONE UP AND SWING."
        }
    }

    /// The whole camera frame at its true aspect, so the player can see they are fully in view.
    private var cameraPip: some View {
        ZStack {
            Color.black
            if case .running = camera.status {
                CameraPreview(session: camera.tracker.session)
                PoseSkeletonView(frame: camera.frame)
            } else {
                Image(systemName: "person.crop.rectangle").font(.title2).opacity(0.5)
            }
        }
        .aspectRatio(camera.tracker.frameAspect, contentMode: .fit)
        .frame(width: 118)
        .clipShape(RoundedRectangle(cornerRadius: 12))
        .overlay(RoundedRectangle(cornerRadius: 12).strokeBorder(trackingColor, lineWidth: 2))
        .accessibilityHidden(true)
    }

    private var cameraPanel: some View {
        ZStack(alignment: .leading) {
            RoundedRectangle(cornerRadius: 20).fill(Color.white.opacity(0.07))
            GeometryReader { geometry in
                RoundedRectangle(cornerRadius: 20)
                    .fill(LinearGradient(colors: [.mint.opacity(0.25), .yellow.opacity(0.55)], startPoint: .leading, endPoint: .trailing))
                    .frame(width: geometry.size.width * round.power)
                    .animation(.easeOut(duration: 0.08), value: round.power)
            }
            HStack {
                Image(systemName: round.phase == .flying ? "paperplane.fill" : "figure.golf")
                    .font(.system(size: 25, weight: .light))
                    .symbolEffect(.pulse, isActive: camera.phase == .address)
                VStack(alignment: .leading, spacing: 4) {
                    Text(cameraTitle).font(.system(size: 21, weight: .bold, design: .rounded))
                    if case .running = camera.status, camera.phase == .findingPlayer || camera.phase == .address {
                        readinessRow
                    } else if case .denied = camera.status {
                        Button("Open Settings") {
                            if let url = URL(string: UIApplication.openSettingsURLString) { UIApplication.shared.open(url) }
                        }
                        .font(.system(size: 11, weight: .bold)).underline()
                    } else {
                        Text(cameraDetail).font(.system(size: 11)).opacity(0.65)
                    }
                }
                Spacer()
                Text("\(Int(round.power * 100))%")
                    .font(.system(size: 27, weight: .black, design: .rounded)).monospacedDigit()
            }.padding(16)
        }
        .frame(height: 104)
        .accessibilityElement(children: .ignore)
        .accessibilityLabel("Camera swing. \(cameraTitle) \(cameraDetail)")
        .accessibilityValue("\(Int(round.power * 100)) percent power")
        .accessibilityIdentifier("cameraPanel")
    }

    /// The pre-swing checklist: a dot per check, green when it passes.
    private var readinessRow: some View {
        HStack(spacing: 6) {
            ForEach(CaptureReadiness.Check.allCases, id: \.title) { check in
                let ok = !camera.readiness.failing.contains(check)
                Label(check.title, systemImage: ok ? "checkmark.circle.fill" : "circle")
                    .labelStyle(.iconOnly)
                    .font(.system(size: 11))
                    .foregroundStyle(ok ? .mint : .white.opacity(0.35))
                    .accessibilityLabel("\(check.title) \(ok ? "ready" : "not ready")")
            }
            Text(camera.readiness.isReady ? "READY" : (camera.readiness.firstProblem?.fix ?? ""))
                .font(.system(size: 10, weight: .bold)).opacity(0.8)
                .lineLimit(1)
        }
    }

    /// Border colour for the camera view: how well the swing joints are being read.
    private var trackingColor: Color {
        guard case .running = camera.status, camera.frame != nil else { return .white.opacity(0.4) }
        let quality = camera.trackingQuality
        return quality >= 0.6 ? .mint : quality >= 0.35 ? .yellow : .orange
    }

    private var cameraDetail: String {
        let fov = camera.tracker.fieldOfView > 0 ? String(format: " · %.0f° lens", camera.tracker.fieldOfView) : ""
        if benchmark3D, camera.tracker.pose3DMilliseconds > 0 {
            return String(format: "3D pose: %.0f ms/frame · 2D feed %.0f fps", camera.tracker.pose3DMilliseconds, camera.tracker.measuredFrameRate)
        }
        if case .running = camera.status, camera.frame != nil, camera.phase == .findingPlayer || camera.phase == .address,
           let problem = camera.readiness.firstProblem {
            return problem.fix
        }
        switch camera.phase {
        case .findingPlayer: return "Get shoulders and hands in the small view" + fov
        case .address: return "Take it back to fill the meter" + fov
        case .backswing: return round.power >= 1 ? "Full power. Any more will hook" : "Further back for more"
        case .downswing, .impact: return "Through the ball"
        case .followThrough, .finish: return "Set up again for the next ball"
        }
    }

    private var cameraTitle: String {
        if round.phase == .flying { return "Nice swing." }
        if demoTask != nil { return "Demo swing." }
        switch camera.status {
        case .idle, .requestingPermission: return "Starting camera…"
        case .denied: return "Camera access is off."
        case .unavailable(let reason): return reason
        case .running: return camera.phase.displayName + "."
        }
    }

    private func updateInputs() {
        let live = appPhase == .active && !cameraPresented
        if live, swingInput == .phone { motion.start() } else { motion.stop() }
        if live, swingInput == .camera { camera.start() } else { camera.stop() }
    }

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
        case .impact(let power, let curve, let latency):
            guard round.canSwing else { return }
            if round.phase == .ready { charge(power) }
            round.charge(power)
            release(curve: curve, at: Date.now.addingTimeInterval(-latency))
        }
    }

    private var resultPanel: some View {
        VStack(spacing: 13) {
            if round.phase == .complete, let hole = round.hole {
                HStack {
                    VStack(alignment: .leading, spacing: 4) {
                        Text(round.isHoled ? "IN THE HOLE" : "PICKED UP").font(.system(size: 10, weight: .heavy)).tracking(2)
                            .accessibilityIdentifier("roundComplete")
                        Text(round.isHoled ? Hole.scoreName(strokes: round.strokes, par: hole.par) + "." : "Next time.")
                            .font(.system(size: 26, weight: .black, design: .rounded))
                    }
                    Spacer()
                    VStack(alignment: .trailing, spacing: 2) {
                        Text("\(round.strokes)").font(.system(size: 42, weight: .black, design: .rounded)).foregroundStyle(.mint)
                        Text("PAR \(hole.par)").font(.system(size: 9, weight: .bold)).opacity(0.6)
                    }
                }
            } else if round.phase == .complete {
                HStack {
                    VStack(alignment: .leading, spacing: 4) {
                        Text("ROUND COMPLETE").font(.system(size: 10, weight: .heavy)).tracking(2)
                            .accessibilityIdentifier("roundComplete")
                        Text(round.score >= 300 ? "What a round." : "One more round?")
                            .font(.system(size: 26, weight: .black, design: .rounded))
                    }
                    Spacer()
                    Text("\(round.score)").font(.system(size: 42, weight: .black, design: .rounded)).foregroundStyle(.mint)
                }
                HStack {
                    ForEach(round.shots) { shot in
                        VStack(spacing: 5) {
                            Text("\(shot.id)").font(.caption2).opacity(0.6)
                            Text("+\(shot.points)").font(.system(size: 16, weight: .black, design: .rounded))
                        }.frame(maxWidth: .infinity)
                    }
                }
            } else if let shot = round.activeShot, let hole = round.hole {
                HStack {
                    VStack(alignment: .leading, spacing: 5) {
                        Text((round.lie == .bunker ? "IN THE SAND" : round.lie == .rough ? "IN THE ROUGH" : round.lie == .green ? "ON THE GREEN" : "ON THE FAIRWAY"))
                            .font(.system(size: 10, weight: .heavy)).tracking(1)
                        Text(round.lie == .green ? "\(hole.feetToCup(from: round.ballPosition)) ft to the cup" : "\(Int(round.yardsToPin)) yd to the pin")
                            .font(.system(size: 26, weight: .black, design: .rounded)).foregroundStyle(.mint)
                    }
                    Spacer()
                    VStack(alignment: .trailing) {
                        Text("\(Int(shot.total)) yd").font(.system(size: 24, weight: .bold, design: .rounded))
                        Text("\(Int(shot.carry)) carry · \(Int(shot.roll)) roll").font(.caption).opacity(0.6)
                    }
                }
            } else if let shot = round.activeShot {
                HStack {
                    VStack(alignment: .leading, spacing: 5) {
                        Text(shot.points == 100 ? "BULLSEYE!" : shot.targetName?.uppercased() ?? "ON THE FAIRWAY")
                            .font(.system(size: 10, weight: .heavy)).tracking(1)
                        Text("+\(shot.points) points").font(.system(size: 30, weight: .black, design: .rounded)).foregroundStyle(.mint)
                    }
                    Spacer()
                    VStack(alignment: .trailing) {
                        Text("\(Int(shot.total)) yd").font(.system(size: 24, weight: .bold, design: .rounded))
                        Text("\(Int(shot.carry)) carry · \(Int(shot.roll)) roll · \(Int(shot.flight.apex)) apex").font(.caption).opacity(0.6)
                    }
                }
            }
            HStack(spacing: 10) {
                Button { round.replay() } label: {
                    Label("Replay", systemImage: "arrow.clockwise").frame(maxWidth: .infinity).padding(15)
                        .background(.white.opacity(0.1), in: RoundedRectangle(cornerRadius: 14))
                }.accessibilityIdentifier("replayShot")
                Button {
                    round.phase == .complete ? round.restart() : round.nextShot()
                } label: {
                    Text(round.phase == .complete ? "Play again" : "Next shot")
                        .frame(maxWidth: .infinity).padding(15)
                        .background(cream, in: RoundedRectangle(cornerRadius: 14)).foregroundStyle(ink)
                }.accessibilityIdentifier(round.phase == .complete ? "playAgain" : "nextShot")
            }.font(.system(size: 14, weight: .bold))
            Text(round.hole == nil ? "TARGET RINGS: 100 / 60 / 30 PTS · FAIRWAY: 10 PTS" : "ROUGH AND SAND COST DISTANCE · THE GREEN ROLLS FAST")
                .font(.system(size: 9, weight: .bold)).opacity(0.55)
        }
        .padding(20)
    }

    private var instructions: some View {
        VStack(alignment: .leading, spacing: 22) {
            Text("Welcome to the meadow.").font(.largeTitle.bold())
            Text("1. Pick a club and aim left or right.\n\n2. Pull down on the swing pad. More pull means more power. Release to hit.\n\n3. Land inside a target. The center is worth 100 points. You get five shots.\n\n4. Or choose Hole 1 in the settings menu: a par 4 played from the tee to the cup. Aim is relative to the pin, rough and sand cost distance, and the green rolls fast.")
            Text("Swing phone: grip the iPhone like a club and hold it still at address. Take it back, then swing through. Faster swings hit farther, and a new backswing tees up the next ball.")
            Text("Camera: prop the phone up facing you so your shoulders and hands are in the small view, then swing normally, with or without a club. Like Wii golf, how far back you take your hands sets the power; swing well past full and the shot drifts. The ball launches the moment the camera sees impact. Set your handedness in the settings menu.")
            Text("Hold on tight and clear the space around you before swinging. A wrist strap is a good idea.")
                .font(.callout).foregroundStyle(.orange)
            Text("This is an arcade mock with game-tuned distances. Demo shot plays the same swing automatically. Camera lab opens the experimental body-tracking prototype.")
                .font(.callout).foregroundStyle(.secondary)
            Text("Sound follows Silent Mode. Vibration requires a physical iPhone; a mounted phone cannot provide resistance to your hands.")
                .font(.callout).foregroundStyle(.secondary)
            Button("Let's play") { helpPresented = false }.buttonStyle(.borderedProminent).tint(.mint)
        }
        .padding(28)
        .presentationDetents([.large])
    }

    private func targetColor(_ id: Int) -> Color { id == 0 ? .orange : id == 1 ? .mint : .yellow }

    private func lieColor(_ lie: Lie) -> Color {
        switch lie {
        case .tee, .fairway: .mint
        case .green: Color(red: 0.6, green: 0.9, blue: 0.5)
        case .rough: .orange
        case .bunker: Color(red: 0.93, green: 0.85, blue: 0.6)
        }
    }

    private func applyMode() {
        let mode: GameMode = playHole ? .hole(.first) : .range
        guard round.mode != mode else { return }
        round.switchMode(mode)
        meadow = MeadowScene(mode: mode)
    }

    private func charge(_ power: Double) {
        if round.phase == .ready { feedback.beginBackswing(interactive: true) }
        round.charge(power)
        feedback.updateTension(round.power)
        audio.tension(round.power)
    }

    private func release(curve: Double = 0, at date: Date = .now) {
        feedback.endBackswing()
        audio.stop()
        if round.release(at: date, curve: curve) {
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
