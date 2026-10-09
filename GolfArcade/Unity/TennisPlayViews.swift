import SwiftUI

/// What the TV shows whenever a match is not on: the menu, loading and results, laid out on a
/// 1280×720 canvas and scaled to the screen so every TV frames it the same.
struct TennisTVRoot: View {
    var body: some View {
        GeometryReader { g in
            // Fit the 1280×720 canvas (16:10 Mac screens get backdrop above and below), less
            // the edge margin for TVs that crop.
            let scale = min(g.size.width / 1280, g.size.height / 720) * (1 - SportsSession.shared.overscan)
            ZStack {
                Club.lagoonDeep   // around the canvas on 16:10 screens; each screen paints its own scene
                Group {
                    if OnboardingFlow.shared.holdsMenu { OnboardingHoldingCard() }
                    else { TennisMenuScreen(compact: false)
                        .overlay(alignment: .bottomTrailing) {
                            Text("BETA · Feedback on your phone").font(IslandUI.font(13, bold: true))
                                .foregroundStyle(IslandUI.navy).padding(8).background(IslandUI.paper, in: Capsule()).padding(12)
                                .allowsHitTesting(false)
                        }
                    }
                }
                    .frame(width: 1280, height: 720)
                    .scaleEffect(scale)
                    .frame(width: g.size.width, height: g.size.height)
            }
        }
        .ignoresSafeArea()
        .preferredColorScheme(.dark)
    }
}

/// The phone with no TV: the whole menu, upright and touch-driven.
struct TennisPhoneMenu: View {
    var body: some View {
        ZStack {
            Club.lagoonDeep.ignoresSafeArea()
            TennisMenuScreen(compact: true)
        }
        .preferredColorScheme(.dark)
    }
}

/// The phone while the TV shows the menu: a remote you click. Four arrow buttons move the focus one step per click (hold to
/// repeat — no swiping anywhere), A selects, B goes back, Home returns to the main menu. A strip shows what the TV is on.
/// Dark and quiet on purpose: your eyes are on the TV.
struct TennisRemote: View {
    @Bindable var menu = TennisMenu.shared
    var body: some View {
        VStack(spacing: 18) {
            HStack {
                HStack(spacing: 8) {
                    Circle().fill(IslandUI.lime).frame(width: 10, height: 10).shadow(color: IslandUI.lime, radius: 5)
                    Text("Connected to TV").font(IslandUI.font(15, bold: true)).accessibilityIdentifier("controller-connection")
                }
                .padding(.horizontal, 14).padding(.vertical, 8).background(.white.opacity(0.1), in: Capsule())
                Spacer()
            }
            onTV
            Spacer(minLength: 0)
            if menu.screen == .story || menu.screen == .results {
                Button { menu.select() } label: {
                    Text("Next").font(IslandUI.font(22, bold: true)).foregroundStyle(IslandUI.navy)
                        .frame(maxWidth: .infinity, minHeight: 56).background(IslandUI.lime, in: Capsule())
                }.buttonStyle(.plain).accessibilityIdentifier("menuNext")
            }
            DPad(menu: menu)
            Text("Click to move one step · hold to repeat").font(IslandUI.font(13, bold: true)).foregroundStyle(.white.opacity(0.55))
            Spacer(minLength: 0)
            HStack(alignment: .center, spacing: 28) {
                RemoteButton(label: "‹", caption: "Back", size: 76, filled: false) { menu.back() }
                RemoteButton(label: "A", caption: "Select", size: 112, filled: true) { menu.select() }
                RemoteButton(label: "⌂", caption: "Home", size: 76, filled: false) { menu.goHome() }
            }
            Text(menu.screen == .loading ? "Loading the court on your TV…" : "When the match starts, hold this like a racket.")
                .font(IslandUI.font(13, bold: false)).foregroundStyle(.white.opacity(0.55)).multilineTextAlignment(.center)
        }
        .foregroundStyle(.white)
        .padding(24)
        .background(LinearGradient(colors: [Color(hex: "16294A"), IslandUI.dark], startPoint: .top, endPoint: .bottom).ignoresSafeArea())
        .preferredColorScheme(.dark)
    }

    /// What the TV shows and what is focused on it (or the story line, which reads on either screen).
    private var onTV: some View {
        VStack(alignment: .leading, spacing: 4) {
            Text("ON THE TV · \(screenName)").font(IslandUI.font(12, bold: true)).tracking(1.6).foregroundStyle(IslandUI.lime)
            if menu.screen == .story, let line = menu.storyLine {
                Text(line.text).font(IslandUI.font(SportsSession.shared.bigText ? 22 : 17, bold: true)).fixedSize(horizontal: false, vertical: true)
                Text("A: next  ·  B: skip").font(IslandUI.font(12, bold: true)).foregroundStyle(.white.opacity(0.6))
            } else {
                Text(focusName).font(IslandUI.font(SportsSession.shared.bigText ? 30 : 24, bold: true)).lineLimit(2).minimumScaleFactor(0.6)
            }
            if !menu.notice.isEmpty { IslandMenuNotice(menu: menu, compact: true).padding(.top, 4) }
        }
        .frame(maxWidth: .infinity, alignment: .leading).padding(.horizontal, 18).padding(.vertical, 14)
        .background(.white.opacity(0.08), in: RoundedRectangle(cornerRadius: 20, style: .continuous))
        .overlay(RoundedRectangle(cornerRadius: 20, style: .continuous).strokeBorder(.white.opacity(0.14), lineWidth: 1.5))
    }

    private var screenName: String {
        switch menu.screen {
        case .online(let route): route.rawValue.uppercased()
        case .party: "PLAY"; case .multiplayer: "MULTIPLAYER"; case .localChoice: "LOCAL MULTIPLAYER"; case .onlineChoice: "ONLINE"; case .homeEmotes: "EMOTES"; case .quickPlay: "QUICK PLAY"
        case .title: "TITLE"; case .main: "HOME"; case .gameSelect: "CHOOSE YOUR SPORT"
        case .hub(let sport): sport.title; case .locked(let sport): "\(sport.title) · COMING SOON"
        case .campaign: "ISLAND CIRCUIT"; case .exhibition: "QUICK MATCH"; case .training: "TRAINING"
        case .character: "LOCKER"; case .settings: "SETTINGS"; case .howTo: "HOW TO PLAY"; case .golfLesson: "GOLF LESSON"
        case .connect: "CONNECT"; case .loading: "LOADING"; case .results: "RESULTS"; case .map: menu.mapSport == .golf ? "CHOOSE YOUR COURSE" : "CHOOSE YOUR COURT"; case .postMatch: "MATCH REP"
        case .story: menu.storyLine.map { TennisStory.name(for: $0.speaker).uppercased() } ?? "STORY"
        }
    }
    private var focusName: String {
        let id = menu.focused
        if menu.screen == .map, let choice = menu.mapChoices.first(where: { "map-\($0.id)" == id }) { return choice.title }
        if id.hasPrefix("round"), let r = Int(id.dropFirst(5)) {
            let o = TennisCampaign.draw[r]
            return TennisCampaign.shared.unlocked(r) ? "\(o.roundTitle.capitalized) · \(o.name)" : "\(o.roundTitle.capitalized) · Locked"
        }
        switch id {
        case "start": return menu.screen == .title ? "Press A to start" : "Start"
        case "level" where menu.screen == .training: return "Coach: \(TennisMenu.trainingLevels[menu.trainingLevel].name)"
        case "homeContinue": return menu.continueLabel.subtitle
        case "": return "…"
        default: return TennisMenu.label(for: id)
        }
    }
}

/// Four arrow buttons in a plus. One click = one step; holding a button repeats after a short delay.
private struct DPad: View {
    let menu: TennisMenu
    var body: some View {
        let cell: CGFloat = 96, gap: CGFloat = 8
        ZStack {
            arrow("arrowtriangle.up.fill", .up).offset(y: -(cell + gap))
            arrow("arrowtriangle.left.fill", .left).offset(x: -(cell + gap))
            arrow("arrowtriangle.right.fill", .right).offset(x: cell + gap)
            arrow("arrowtriangle.down.fill", .down).offset(y: cell + gap)
            Circle().strokeBorder(.white.opacity(0.14), style: StrokeStyle(lineWidth: 2, dash: [4, 5])).frame(width: 64, height: 64)
        }
        .frame(width: cell * 3 + gap * 2, height: cell * 3 + gap * 2)
    }
    private func arrow(_ icon: String, _ direction: MenuMove) -> some View {
        RepeatButton(action: { menu.move(direction) }) { pressed in
            Image(systemName: icon).font(.system(size: 34, weight: .bold))
                .foregroundStyle(pressed ? IslandUI.lime : .white)
                .frame(width: 96, height: 96)
                .background(pressed ? IslandUI.lime.opacity(0.22) : .white.opacity(0.09), in: RoundedRectangle(cornerRadius: 26, style: .continuous))
                .overlay(RoundedRectangle(cornerRadius: 26, style: .continuous).strokeBorder(pressed ? IslandUI.lime : .white.opacity(0.22), lineWidth: 2.5))
                .shadow(color: pressed ? IslandUI.lime.opacity(0.45) : .black.opacity(0.35), radius: pressed ? 12 : 0, y: pressed ? 0 : 4)
                .scaleEffect(pressed ? 0.95 : 1)
        }
        .accessibilityLabel("Move \(String(describing: direction))")
    }
}

/// Acts on touch-down; keeps acting while held. A tap is one action; sliding the finger does nothing extra.
private struct RepeatButton<Label: View>: View {
    let action: () -> Void
    @ViewBuilder let label: (Bool) -> Label
    @State private var pressed = false
    @State private var timer: Timer?
    var body: some View {
        label(pressed)
            .contentShape(Rectangle())
            .gesture(DragGesture(minimumDistance: 0)
                .onChanged { _ in
                    guard !pressed else { return }
                    pressed = true; action()
                    timer?.invalidate()
                    let start = Timer(timeInterval: 0.42, repeats: false) { _ in
                        MainActor.assumeIsolated {
                            guard pressed else { return }
                            let repeater = Timer(timeInterval: 0.11, repeats: true) { _ in MainActor.assumeIsolated { action() } }
                            RunLoop.main.add(repeater, forMode: .common); timer = repeater
                        }
                    }
                    RunLoop.main.add(start, forMode: .common); timer = start
                }
                .onEnded { _ in pressed = false; timer?.invalidate(); timer = nil })
            .accessibilityAddTraits(.isButton)
            .accessibilityAction { action() }
            .onDisappear { timer?.invalidate(); timer = nil }
    }
}

private struct RemoteButton: View {
    let label: String
    let caption: String
    let size: CGFloat
    let filled: Bool
    var action: () -> Void
    var body: some View {
        VStack(spacing: 8) {
            Button(action: action) {
                Text(label).font(IslandUI.font(size * 0.42, bold: true)).foregroundStyle(filled ? IslandUI.navy : .white)
                    .frame(width: size, height: size)
                    .background(filled ? IslandUI.lime : .clear, in: Circle())
                    .overlay(Circle().strokeBorder(filled ? IslandUI.navy.opacity(0.0) : .white.opacity(0.42), lineWidth: 3))
                    .shadow(color: filled ? .black.opacity(0.4) : .clear, radius: 0, y: filled ? 5 : 0)
            }.buttonStyle(PressStyle()).accessibilityLabel(caption).accessibilityIdentifier("remote-\(caption.lowercased())")
            Text(caption.uppercased()).font(IslandUI.font(12, bold: true)).tracking(1.6).foregroundStyle(.white.opacity(0.6))
        }
    }
}

private struct PressStyle: ButtonStyle {
    func makeBody(configuration: Configuration) -> some View {
        configuration.label.scaleEffect(configuration.isPressed ? 0.92 : 1).brightness(configuration.isPressed ? 0.08 : 0)
            .animation(.spring(response: 0.2, dampingFraction: 0.6), value: configuration.isPressed)
    }
}

// MARK: - In game

/// The phone's backdrop while it is a controller: dark and quiet, because the player's eyes are on the TV.
private struct ControllerBackdrop: View {
    var body: some View { LinearGradient(colors: [Color(hex: "16294A"), IslandUI.dark], startPoint: .top, endPoint: .bottom).ignoresSafeArea() }
}

/// A big lime action button for the controller (Ready, Toss, Start timing check).
private struct ControllerButton: View {
    let title: String
    var icon: String? = nil
    var height: CGFloat = 64
    var size: CGFloat = 24
    let action: () -> Void
    var body: some View {
        Button(action: action) {
            HStack(spacing: 10) {
                if let icon { Image(systemName: icon).font(.system(size: size * 0.9, weight: .bold)) }
                Text(title).font(IslandUI.font(size, bold: true))
            }
            .foregroundStyle(IslandUI.navy).frame(maxWidth: .infinity, minHeight: height)
            .background { Capsule().fill(IslandUI.lime).shadow(color: .black.opacity(0.4), radius: 0, y: 5) }
        }.buttonStyle(.plain)
    }
}

/// The phone during a match: your racket. Its string bed is the same hit radar as the TV's, showing where each shot
/// met the strings and how well it was timed. Eyes are on the TV, so this screen stays calm: the score, the radar, stamina,
/// one big Dive zone. Pausing needs a hold so a swing can never trigger it.
struct TennisRacketController: View {
    @Bindable var session: SportsSession
    @State private var showOptions = false
    @State private var power = 0.6
    @State private var steering = 0.0
    @State private var shotAim = 0.0
    @State private var holdHint = false
    var body: some View {
        VStack(spacing: 14) {
            topBar
            if session.finishedMatch != nil {
                MatchFinishControls(session: session)
            } else if !session.ready || !session.loading.finished {
                // The same loading screen as the TV: tips, how-to cards, the flowing bar.
                LoadingScreen(menu: .shared, compact: true).padding(-24)
            } else if session.setupStage == .scan {
                ScrollView { AxisGatePanel(session: session) }
            } else if session.measuringDelay {
                DelayProbePanel(session: session)
            } else if session.timingPrompt {
                TimingCheckPrompt(session: session)
            } else if session.checkingTiming {
                TimingCheckPanel(countdownEnds: session.timingCountdownEnds)
            } else if session.setupStage == .ready {
                ControllerReadyPanel(session: session)
            } else if !session.paused && (session.tennisPhase == "serve" || session.tennisPhase == "toss") {
                ServePanel(session: session)
            } else if !session.paused && session.tennisPhase == "receive" {
                ReceivePanel(session: session)
            } else {
                RacketRadar(contacts: session.contacts)
                    .frame(maxWidth: .infinity, maxHeight: .infinity)
                stamina
                if !session.touch { ShotDepthControl(session:session) }
                if session.touch {
                    VStack(spacing: 6) {
                        Slider(value: $steering, in: -1...1) { Text("Court position") }
                            .onChange(of: steering) { _, v in session.steer(v) }
                        RallyAimPad(session:session).frame(height:150)
                        HStack {
                            Slider(value: $power, in: 0.15...1) { Text("Swing power") }
                            Button("Swing") { session.swing(power) }.font(IslandUI.font(24, bold: true)).frame(maxWidth: .infinity, minHeight: 70).buttonStyle(.borderedProminent).tint(IslandUI.lime)
                                .foregroundStyle(IslandUI.navy).disabled(session.paused)
                        }
                    }.tint(IslandUI.lime)
                }
            }
            if session.ready && session.loading.finished && session.finishedMatch == nil {
                if session.pointControlsVisible { TennisAbilityControls(session: session) }
                if session.canPlayEmote { TennisEmoteControls(session: session) }
            }
            if session.finishedMatch == nil && session.ready && session.paused && session.setupStage == .playing {
                Text(session.status).font(IslandUI.font(14, bold: true)).multilineTextAlignment(.center)
                ControllerButton(title: "Resume", icon: "play.fill") { session.readyToPlay() }
            }
            if session.ready && session.loading.finished && session.finishedMatch == nil && session.setupStage == .playing && session.tennisPhase == "point" {
                PointClipControls(session: session).tint(.white)
            }
        }
        .foregroundStyle(.white)
        .padding(20)
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .background(ControllerBackdrop())
        .preferredColorScheme(.dark)
        .sheet(isPresented: $showOptions) {
            IslandPauseScreen(compact: true) { showOptions = false }.presentationDetents([.large])
        }
    }

    /// Pause (hold) and the score, side by side.
    private var topBar: some View {
        HStack(spacing: 12) {
            VStack(spacing: 4) {
                Image(systemName: session.paused ? "slider.horizontal.3" : "pause.fill").font(.system(size: 17, weight: .bold))
                    .frame(width: 46, height: 46).background(.clear, in: Circle())
                    .overlay(Circle().strokeBorder(.white.opacity(0.38), lineWidth: 2.5))
                    // Paused: one tap opens the options. Playing: a hold, so a swing can never pause the match.
                    .onTapGesture { if session.paused { showOptions = true } else { withAnimation { holdHint = true } } }
                    .onLongPressGesture(minimumDuration: 0.5) { if !session.paused { session.pause() }; showOptions = true }
                    .accessibilityLabel(session.paused ? "Options" : "Pause. Hold to pause.")
                    .accessibilityAddTraits(.isButton)
                    .accessibilityAction(named: "Pause") { if !session.paused { session.pause() }; showOptions = true }
            }
            if session.ready && session.loading.finished { Scoreline(session: session) } else { Spacer() }
            if !session.touch && !session.trackingWarning.isEmpty {
                Image(systemName: "exclamationmark.triangle.fill").foregroundStyle(.orange)
            }
        }
        .overlay(alignment: .bottomLeading) {
            if holdHint {
                Text("Hold to pause").font(IslandUI.font(12, bold: true)).foregroundStyle(IslandUI.navy)
                    .padding(.horizontal, 10).padding(.vertical, 5).background(IslandUI.lime, in: Capsule()).offset(y: 30)
                    .task { try? await Task.sleep(for: .seconds(1.6)); withAnimation { holdHint = false } }
            }
        }
    }

    private var stamina: some View {
        VStack(spacing: 6) {
            HStack {
                Text("STAMINA").font(IslandUI.font(12, bold: true)).tracking(1.6).foregroundStyle(.white.opacity(0.65))
                Spacer()
                Text("\(Int(session.stamina * 100))%").font(IslandUI.font(12, bold: true)).foregroundStyle(.white.opacity(0.65))
            }
            Capsule().fill(.white.opacity(0.12)).frame(height: 10)
                .overlay(alignment: .leading) {
                    GeometryReader { g in Capsule().fill(session.stamina > 0.35 ? IslandUI.lime : Arcade.crimson).frame(width: g.size.width * min(1, max(0, session.stamina))) }
                }
        }
    }
}

struct TennisEmoteControls: View {
    let session: SportsSession
    var body: some View {
        EquippedEmoteControls(title: "YOUR POINT · CELEBRATE", ids: session.matchEmotes,
            enabled: session.canPlayEmote,
            notice: session.emoteNotice.isEmpty ? "Choose an emote to celebrate your point." : session.emoteNotice,
            identifier: "controller-emote") { session.playEmote(slot: $0) }
    }
}

/// Shared presentation for the same equipped emotes in golf and tennis.
struct EquippedEmoteControls: View {
    let title: String
    let ids: [String]
    let enabled: Bool
    let notice: String
    let identifier: String
    let select: (Int) -> Void
    var body: some View {
        VStack(alignment: .leading, spacing: 8) {
            Text(title).font(IslandUI.font(12, bold: true))
                .foregroundStyle(enabled ? IslandUI.lime : .white.opacity(0.55))
            HStack(spacing: 8) {
                ForEach(Array(ids.enumerated()), id: \.offset) { slot, id in
                    Button { select(slot) } label: {
                        VStack(spacing: 4) {
                            Text("\(slot + 1)").font(IslandUI.font(11, bold: true)).opacity(0.6)
                            Text(EmoteCatalog.name(id)).font(IslandUI.font(15, bold: true)).lineLimit(1).minimumScaleFactor(0.75)
                        }.frame(maxWidth: .infinity, minHeight: 52)
                            .foregroundStyle(enabled ? IslandUI.navy : .white.opacity(0.5))
                            .background(enabled ? IslandUI.lime : .white.opacity(0.08), in: RoundedRectangle(cornerRadius: 14))
                    }.buttonStyle(.plain).disabled(!enabled)
                        .accessibilityIdentifier("\(identifier)-\(slot)")
                        .accessibilityLabel("Play \(EmoteCatalog.name(id)), emote slot \(slot + 1)")
                }
            }
            Text(notice).font(IslandUI.font(11, bold: false)).foregroundStyle(.white.opacity(0.55))
        }
    }
}

// MARK: - Serve

/// The player's serve on the controller: tap the target box to aim, hold ◀ ▶ to walk along the baseline, then TOSS with the
/// meter in the middle. After the toss: swing as the TV's power bar peaks.
private struct ServePanel: View {
    @Bindable var session: SportsSession
    var body: some View {
        VStack(spacing: 12) {
            if session.tennisPhase == "toss" {
                Spacer()
                Image(systemName:"figure.tennis").font(.system(size:52)).foregroundStyle(IslandUI.lime)

                if session.touch { ControllerButton(title: "Swing", icon: "bolt.fill") { session.swing(0.8) } }
                Spacer()
            } else {
                Text(session.serveFromDeuce ? "DEUCE" : "AD")
                    .font(IslandUI.font(13, bold: true)).tracking(2).foregroundStyle(.white.opacity(0.7))
                ServeAimPad(session: session).frame(maxWidth: .infinity).frame(height: 190)

                MoveButtons(session: session, caption: "Hold to walk the baseline")
                Spacer(minLength: 0)
                ControllerButton(title: "TOSS", icon: "arrow.up.circle.fill", height: 112, size: 40) {
                    if session.haptics { UIImpactFeedbackGenerator(style: .rigid).impactOccurred() }
                    session.toss()
                }.accessibilityIdentifier("tennisToss")

            }
        }
    }
}

/// The target box seen from behind the baseline, with the aim point on it. A tap sets the target. The T (centre line) is on
/// the right of the box from the deuce court and on the left from the ad court.
struct ServeAimPad: View {
    @Bindable var session: SportsSession
    var body: some View {
        GeometryReader { g in
            let w = g.size.width, h = g.size.height
            let boxW = w * 0.5, boxLeft = session.serveFromDeuce ? 0 : w * 0.5
            let across = session.serveAim.across, depth = session.serveAim.depth
            let u = session.serveFromDeuce ? 1 - (across + 1) / 2 : (across + 1) / 2
            let point = CGPoint(x: boxLeft + u * boxW, y: (1 - depth) * h)
            ZStack {
                RoundedRectangle(cornerRadius: 14).fill(Color(red: 0.12, green: 0.30, blue: 0.75))
                Rectangle().fill(.white.opacity(0.14)).frame(width: boxW, height: h).position(x: boxLeft + boxW / 2, y: h / 2)
                Path { p in
                    p.move(to: CGPoint(x: w / 2, y: 0)); p.addLine(to: CGPoint(x: w / 2, y: h))
                    p.addRect(CGRect(x: 1, y: 1, width: w - 2, height: h - 2))
                }.stroke(.white, lineWidth: 2)
                Rectangle().fill(.white).frame(height: 5).position(x: w / 2, y: h - 2)
                Text("NET").font(IslandUI.font(10, bold: true)).foregroundStyle(.white.opacity(0.7)).position(x: w / 2, y: h - 12)
                Text("T").font(IslandUI.font(12, bold: true)).foregroundStyle(.white.opacity(0.85))
                    .position(x: session.serveFromDeuce ? boxW - 12 : boxW + 12, y: 12)
                Text("WIDE").font(IslandUI.font(11, bold: true)).foregroundStyle(.white.opacity(0.85))
                    .position(x: session.serveFromDeuce ? 26 : w - 26, y: 12)
                Circle().fill(Color(hex: "FFD145")).frame(width: 30, height: 30)
                    .overlay(Circle().strokeBorder(IslandUI.dark, lineWidth: 3.5))
                    .shadow(color: Color(hex: "FFD145"), radius: 8)
                    .position(point)
            }
            .clipShape(RoundedRectangle(cornerRadius: 14))
            .contentShape(Rectangle())
            .gesture(SpatialTapGesture().onEnded { v in
                let x = min(max(v.location.x, boxLeft), boxLeft + boxW)
                let u = (x - boxLeft) / boxW
                let across = session.serveFromDeuce ? 1 - 2 * u : 2 * u - 1
                session.setServeAim(across: across, depth: 1 - min(max(v.location.y / h, 0), 1))
                if session.haptics { UISelectionFeedbackGenerator().selectionChanged() }
            })
            .accessibilityElement().accessibilityLabel("Serve target. Tap to aim.")
        }
    }
}

/// While the opponent serves: get in position.
private struct ReceivePanel: View {
    @Bindable var session: SportsSession
    var body: some View {
        VStack(spacing: 18) {
            Spacer()
            Image(systemName:"figure.tennis").font(.system(size:52)).foregroundStyle(IslandUI.lime)

            MoveButtons(session: session, caption: "Hold to walk")
            Spacer()
        }
    }
}

/// Hold to walk left or right.
private struct MoveButtons: View {
    let session: SportsSession
    let caption: String
    @State private var held = 0.0
    var body: some View {
        VStack(spacing: 6) {
            HStack(spacing: 16) {
                hold("arrowtriangle.left.fill", -1)
                hold("arrowtriangle.right.fill", 1)
            }
            Text(caption.uppercased()).font(IslandUI.font(11, bold: true)).tracking(1.6).foregroundStyle(.white.opacity(0.55))
        }
        .onDisappear { held = 0; session.nudge(0) }
    }
    private func hold(_ icon: String, _ direction: Double) -> some View {
        let on = held == direction
        return Image(systemName: icon).font(.system(size: 30, weight: .bold)).foregroundStyle(on ? IslandUI.lime : .white)
            .frame(maxWidth: .infinity, minHeight: 78)
            .background(on ? IslandUI.lime.opacity(0.22) : .white.opacity(0.09), in: RoundedRectangle(cornerRadius: 24, style: .continuous))
            .overlay(RoundedRectangle(cornerRadius: 24, style: .continuous).strokeBorder(on ? IslandUI.lime : .white.opacity(0.22), lineWidth: 2.5))
            .scaleEffect(on ? 0.96 : 1)
            .gesture(DragGesture(minimumDistance: 0)
                .onChanged { _ in if held != direction { held = direction; session.nudge(direction) } }
                .onEnded { _ in held = 0; session.nudge(0) })
            .accessibilityLabel(direction < 0 ? "Move left" : "Move right")
    }
}

// MARK: - Setup

/// While the TV flashes: the camera is timing how far its picture lags the game.
private struct DelayProbePanel: View {
    let session: SportsSession
    @State private var pulse = false
    var body: some View {
        VStack(spacing: 16) {
            Spacer()
            Image(systemName: "tv").font(.system(size: 64, weight: .bold)).foregroundStyle(IslandUI.lime).scaleEffect(pulse ? 1.08 : 0.95)
            SportsCameraPreview(motion: session.motion).frame(height: 180)
                .clipShape(RoundedRectangle(cornerRadius: 14))
                .overlay { Image(systemName: "plus").font(.title).foregroundStyle(.white) }
            Text("2 · Measure screen delay").font(IslandUI.font(24, bold: true))
            Text("It will flash a few times while we time its picture, so your swings land when you see the ball.")
                .font(IslandUI.font(15, bold: true)).foregroundStyle(.white.opacity(0.8)).multilineTextAlignment(.center)
            ProgressView().tint(IslandUI.lime)
            Spacer()
        }
        .onAppear { withAnimation(.easeInOut(duration: 0.5).repeatForever(autoreverses: true)) { pulse = true } }
    }
}

/// Offered the first time on a TV: what the timing check is for, and a button to start it when the player is ready.
private struct TimingCheckPrompt: View {
    let session: SportsSession
    var body: some View {
        VStack(spacing: 14) {
            Spacer()
            Image(systemName: "metronome.fill").font(.system(size: 56, weight: .bold)).foregroundStyle(IslandUI.lime)
            Text("2 · Timing calibration").font(IslandUI.font(26, bold: true))
            Text("Every TV shows the picture a little late. This quick check measures that delay so your swings land exactly when you see the ball.\n\nThe flashes measure picture delay directly. Now follow the bouncing ball to check your swings. Swing every time it reaches the line; the check will keep the screen measurement.")
                .font(IslandUI.font(15, bold: true)).foregroundStyle(.white.opacity(0.85)).multilineTextAlignment(.center).padding(.horizontal, 12)
            if !session.timingNote.isEmpty {
                Text(session.timingNote).font(IslandUI.font(14, bold: true)).multilineTextAlignment(.center)
            }
            if !session.touch {
                Button("Retry screen flashes") { session.measureTVDelay() }.accessibilityIdentifier("timing-flashes")
            }
            ControllerButton(title: "Start timing check", icon: "play.fill", height: 58, size: 21) { session.startTimingCheck() }.accessibilityIdentifier("timing-start")
            Button("Skip for now") { session.skipTimingCheck() }.accessibilityIdentifier("timing-skip")
                .font(IslandUI.font(15, bold: true)).foregroundStyle(.white.opacity(0.7))
            Spacer()
        }
    }
}

/// While the TV runs the timing check: a get-ready countdown, then swing with the ball.
private struct TimingCheckPanel: View {
    let countdownEnds: Date?
    var body: some View {
        TimelineView(.periodic(from: .now, by: 0.2)) { context in
            let left = countdownEnds.map { max(0, $0.timeIntervalSince(context.date)) } ?? 0
            VStack(spacing: 16) {
                Spacer()
                if left > 0 {
                    Text("Get ready").font(IslandUI.font(26, bold: true))
                    Text("\(Int(left.rounded(.up)))").font(IslandUI.font(110, bold: true)).foregroundStyle(IslandUI.lime).monospacedDigit()
                    Text("Swing every time the ball drops onto the line.")
                        .font(IslandUI.font(16, bold: true)).foregroundStyle(.white.opacity(0.85)).multilineTextAlignment(.center)
                } else {
                    Image(systemName: "metronome.fill").font(.system(size: 64, weight: .bold)).foregroundStyle(IslandUI.lime)
                    Text("Swing on every bounce!").font(IslandUI.font(26, bold: true))
                    Text("Follow the ball on the display. Two bounces warm you up; the next seven record your timing. Reset your arm between swings.")
                        .font(IslandUI.font(15, bold: true)).foregroundStyle(.white.opacity(0.8)).multilineTextAlignment(.center)
                }
                Spacer()
            }
        }
    }
}

private struct Scoreline: View {
    let session: SportsSession
    var body: some View {
        HStack(spacing: 10) {
            Text("YOU").font(IslandUI.font(15, bold: true)).foregroundStyle(IslandUI.lime)
            Text("\(session.score.player)").font(IslandUI.font(26, bold: true)).monospacedDigit()
            Text("–").font(IslandUI.font(20, bold: true)).foregroundStyle(.white.opacity(0.5))
            Text("\(session.score.opponent)").font(IslandUI.font(26, bold: true)).monospacedDigit()
            Text(session.opponentName.uppercased()).font(IslandUI.font(15, bold: true)).foregroundStyle(Arcade.sky).lineLimit(1).minimumScaleFactor(0.7)
            Spacer(minLength: 4)
            if !session.score.detail.isEmpty {
                Text(session.score.detail.components(separatedBy: "·").dropFirst().first?.trimmingCharacters(in: .whitespaces) ?? "")
                    .font(IslandUI.font(13, bold: true)).foregroundStyle(.white.opacity(0.7)).lineLimit(1)
            }
        }
        .padding(.horizontal, 16).padding(.vertical, 8)
        .background(.white.opacity(0.1), in: Capsule())
    }
}

/// The grade of the last hit, popping in over the racket.
private struct LastHit: View {
    let contact: TennisContact?
    @State private var shown: UUID?
    @State private var pop = false
    var body: some View {
        Group {
            if let contact {
                VStack(spacing: 2) {
                    Text(contact.gradeName + "!").font(IslandUI.font(40, bold: true)).foregroundStyle(color(contact)).shadow(color: IslandUI.dark.opacity(0.7), radius: 0, y: 3)
                    // Which way the swing was off, so timing can be learned ball by ball.
                    Text(contact.timingWord)
                        .font(IslandUI.font(14, bold: true)).tracking(2)
                        .foregroundStyle(contact.timingWord == "ON TIME" ? IslandUI.lime : .white.opacity(0.85))
                }
                .scaleEffect(pop ? 1 : 1.6).opacity(pop ? 1 : 0)
            }
        }
        .padding(.top, 2)
        .onChange(of: contact?.id) { _, id in
            pop = false
            withAnimation(.spring(response: 0.3, dampingFraction: 0.5)) { pop = true }
            shown = id
        }
        .onAppear { pop = contact != nil }
    }
    private func color(_ c: TennisContact) -> Color { RacketRadar.color(c) }
}

/// The racket seen face-on: frame, strings, the sweet spot, and a dot for each recent hit
/// (newest brightest). Contact x/y arrive in half-widths of the string bed.
struct RacketRadar: View {
    let contacts: [TennisContact]
    @State private var ripple = false

    static func color(_ c: TennisContact) -> Color {
        if c.supercharged { return Arcade.sea }
        switch c.grade {
        case 5: return Color(hex: "FFD145")
        case 4: return IslandUI.lime
        case 3: return Color(red: 0.45, green: 0.9, blue: 0.45)
        case 2: return Arcade.sky
        default: return Arcade.crimson
        }
    }

    var body: some View {
        GeometryReader { g in
            // String bed 0.35 m × 0.48 m: keep its real proportions.
            let headH = min(g.size.height * 0.70, g.size.width * 0.9 / 0.73)
            let headW = headH * 0.73
            let centre = CGPoint(x: g.size.width / 2, y: headH / 2 + 34)
            ZStack {
                // Handle and throat.
                Path { p in
                    let base = CGPoint(x: centre.x, y: centre.y + headH / 2)
                    p.move(to: CGPoint(x: base.x - headW * 0.28, y: base.y - headH * 0.05))
                    p.addLine(to: CGPoint(x: base.x - headW * 0.06, y: base.y + headH * 0.22))
                    p.move(to: CGPoint(x: base.x + headW * 0.28, y: base.y - headH * 0.05))
                    p.addLine(to: CGPoint(x: base.x + headW * 0.06, y: base.y + headH * 0.22))
                }.stroke(IslandUI.dark, style: StrokeStyle(lineWidth: 14, lineCap: .round))
                RoundedRectangle(cornerRadius: 10)
                    .fill(IslandUI.lime)
                    .frame(width: headW * 0.16, height: max(10, g.size.height - headH - 34 - headH * 0.2))
                    .position(x: centre.x, y: centre.y + headH / 2 + headH * 0.2 + max(10, g.size.height - headH - 34 - headH * 0.2) / 2)
                // Strings.
                Ellipse().fill(Color.white.opacity(0.06)).frame(width: headW, height: headH).position(centre)
                Path { p in
                    for i in 1..<14 {
                        let x = centre.x - headW / 2 + headW * CGFloat(i) / 14
                        p.move(to: CGPoint(x: x, y: centre.y - headH / 2)); p.addLine(to: CGPoint(x: x, y: centre.y + headH / 2))
                    }
                    for i in 1..<18 {
                        let y = centre.y - headH / 2 + headH * CGFloat(i) / 18
                        p.move(to: CGPoint(x: centre.x - headW / 2, y: y)); p.addLine(to: CGPoint(x: centre.x + headW / 2, y: y))
                    }
                }
                .stroke(.white.opacity(0.3), lineWidth: 1.5)
                .mask(Ellipse().frame(width: headW, height: headH).position(centre))
                // Sweet spot.
                ForEach(0..<3, id: \.self) { i in
                    Ellipse().strokeBorder(IslandUI.lime.opacity(0.6 - Double(i) * 0.17), lineWidth: 3)
                        .frame(width: headW * (0.3 + CGFloat(i) * 0.22), height: headH * (0.3 + CGFloat(i) * 0.22))
                        .position(centre)
                }
                Text("SWEET SPOT").font(IslandUI.font(11, bold: true)).tracking(2).foregroundStyle(IslandUI.lime.opacity(0.85))
                    .position(x: centre.x, y: centre.y + headH * 0.2)
                // Frame.
                Ellipse().strokeBorder(.white, lineWidth: headW * 0.055)
                    .frame(width: headW + headW * 0.055, height: headH + headW * 0.055).position(centre)
                    .shadow(color: .black.opacity(0.4), radius: 8, y: 5)
                // Hits.
                ForEach(Array(contacts.enumerated()), id: \.element.id) { i, c in
                    let newest = i == contacts.count - 1
                    let age = Double(contacts.count - 1 - i)
                    let p = CGPoint(x: centre.x + CGFloat(max(-1.1, min(1.1, c.x))) * headW / 2,
                                    y: centre.y - CGFloat(max(-1.1, min(1.1, c.y))) * headH / 2)
                    ZStack {
                        if newest {
                            Circle().stroke(Self.color(c), lineWidth: 4).frame(width: 34, height: 34)
                                .scaleEffect(ripple ? 2.6 : 1).opacity(ripple ? 0 : 1)
                        }
                        Circle().fill(Self.color(c)).frame(width: newest ? 30 : 18, height: newest ? 30 : 18)
                            .overlay(Circle().strokeBorder(.white, lineWidth: newest ? 3 : 1.5))
                            .shadow(color: Self.color(c), radius: newest ? 12 : 0)
                    }
                    .opacity(newest ? 1 : max(0.2, 0.8 - age * 0.07))
                    .position(p)
                }
            }
            .onChange(of: contacts.last?.id) { _, _ in
                ripple = false
                withAnimation(.easeOut(duration: 0.7)) { ripple = true }
            }
        }
    }
}

/// Shared by the regular controller, classic controller and landscape Unity overlay.
/// Never exposes an advance action before Unity confirms a completed match.
struct MatchFinishControls: View {
    @Bindable var session: SportsSession
    var compact = false
    var body: some View {
        if let result = session.finishedMatch {
            VStack(alignment: .leading, spacing: 16) {
                ScrollView {
                    ClubVictorySummary(won: result.won, score: result.score, summary: TennisMenu.shared.postMatch, compact: true)
                }.scrollBounceBehavior(.basedOnSize)
                VStack(spacing: 8) {
                    IslandAction(title: "Rematch", primary: true, compact: true, identifier: "postGameReplay") { session.advanceAfterMatch(.replay) }
                    IslandAction(title: "Leave", compact: true, identifier: "postGameMenu") { session.advanceAfterMatch(.menu) }
                }
            }.padding(compact ? 14 : 24)
                .background(IslandUI.paper, in: RoundedRectangle(cornerRadius: 24))
        }
    }
}


/// Shared by the AirPlay racket screen and on-phone preview. Choices lock at first Ready.
struct TennisAbilityControls: View {
    let session: SportsSession
    var body: some View {
        AbilityButton(title: "DIVE", icon: "arrow.down.to.line", caption: session.diveCooldown > 0 ? "\(Int(ceil(session.diveCooldown)))s" : session.canDive ? "ready" : "waiting for ball",
                      enabled: !session.paused && session.canDive) { session.dive() }
            .accessibilityLabel("Dive toward the ball").accessibilityIdentifier("tennisDive")
    }
}

/// The one big thumb zone on the racket screen.
private struct AbilityButton: View {
    let title: String, icon: String, caption: String
    let enabled: Bool
    let action: () -> Void
    var body: some View {
        Button(action: action) {
            HStack(spacing: 12) {
                Image(systemName: icon).font(.system(size: 28, weight: .bold))
                Text(title).font(IslandUI.font(30, bold: true)).tracking(1)
                Text(caption).font(IslandUI.font(14, bold: true)).opacity(0.85)
            }
            .foregroundStyle(.white).frame(maxWidth: .infinity, minHeight: 92)
            .background(LinearGradient(colors: [Color(hex: "2A66E0"), Color(hex: "1C45A8")], startPoint: .top, endPoint: .bottom), in: RoundedRectangle(cornerRadius: 28, style: .continuous))
            .shadow(color: .black.opacity(0.4), radius: 0, y: 5)
            .opacity(enabled ? 1 : 0.45)
        }
        .buttonStyle(.plain).disabled(!enabled)
    }
}

struct ControllerReadyPanel: View {
    let session: SportsSession
    var body: some View {
        VStack(spacing: 18) {
            Spacer()
            Image(systemName: "checkmark.circle.fill").font(.system(size: 64)).foregroundStyle(IslandUI.lime)
            Text("3 · Ready to play").font(IslandUI.font(28, bold: true))
            if !session.timingNote.isEmpty { Text(session.timingNote).multilineTextAlignment(.center) }
            Text("Watch the TV. Your phone stays your controller.").multilineTextAlignment(.center)
            ControllerButton(title: "Ready to play", icon: "play.fill") { session.readyToPlay() }
                .accessibilityIdentifier("controller-ready")
            Spacer()
        }.frame(maxWidth: .infinity, maxHeight: .infinity)
    }
}

// Compatibility for archived previews; the aiming exercise has been retired.
struct AimingCalibrationPanel: View {
    let session: SportsSession
    var body: some View { ControllerReadyPanel(session: session) }
}

struct RallyAimPad:View {
    @Bindable var session:SportsSession
    var body:some View {
        GeometryReader { geometry in
            let w=geometry.size.width, h=geometry.size.height, inset:CGFloat=14
            let width=max(1,w-inset*2), height=max(1,h-inset*2)
            ZStack {
                RoundedRectangle(cornerRadius:16).fill(IslandUI.navy)
                Path { p in
                    p.addRect(CGRect(x:inset,y:inset,width:width,height:height))
                    p.move(to:CGPoint(x:w/2,y:inset)); p.addLine(to:CGPoint(x:w/2,y:h-inset))
                    p.move(to:CGPoint(x:inset,y:h*0.48)); p.addLine(to:CGPoint(x:w-inset,y:h*0.48))
                }.stroke(.white.opacity(0.65),lineWidth:2)
                Circle().fill(IslandUI.lime).frame(width:18,height:18)
                    .position(x:inset+CGFloat((session.shotAim+1)/2)*width,y:inset+CGFloat(1-session.shotDepth)*height)
            }.contentShape(Rectangle())
                .gesture(DragGesture(minimumDistance:0).onChanged { value in
                    let across=Double((value.location.x-inset)/width)*2-1
                    let depth=1-Double((value.location.y-inset)/height)
                    session.setShotAim(across:across,depth:depth)
                })
                .accessibilityElement(children:.ignore).accessibilityLabel("Shot target. Left and right, short and deep.")
                .accessibilityIdentifier("rally-aim-pad")
        }
    }
}

private struct ShotDepthControl:View {
    @Bindable var session:SportsSession
    var body:some View {
        HStack(spacing:12) {
            ForEach([0.15,0.5,0.9],id:\.self) { depth in
                Button { session.setShotDepth(depth) } label: {
                    Image(systemName:depth < 0.3 ? "arrow.down" : depth > 0.7 ? "arrow.up" : "minus")
                        .font(.system(size:22,weight:.bold)).frame(maxWidth:.infinity).padding(.vertical,12)
                        .background(abs(session.shotDepth-depth)<0.2 ? IslandUI.lime : .white.opacity(0.15),in:RoundedRectangle(cornerRadius:12))
                        .foregroundStyle(abs(session.shotDepth-depth)<0.2 ? IslandUI.navy : .white)
                }.accessibilityLabel(depth < 0.3 ? "Short shot" : depth > 0.7 ? "Deep shot" : "Middle depth")
            }
        }.accessibilityIdentifier("shot-depth-control")
    }
}
