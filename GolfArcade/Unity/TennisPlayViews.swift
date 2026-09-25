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
                MenuBackdrop(dim: TennisMenu.shared.screen == .title ? 0.1 : 0.45)
                TennisMenuScreen(compact: false)
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
            MenuBackdrop(dim: TennisMenu.shared.screen == .title ? 0.1 : 0.55)
            TennisMenuScreen(compact: true)
        }
        .preferredColorScheme(.dark)
    }
}

/// The phone while the TV shows the menu: a remote. Arrows move, A selects, B goes back.
struct TennisRemote: View {
    @Bindable var menu = TennisMenu.shared
    var body: some View {
        VStack(spacing: 22) {
            HStack {
                Label("Connected to TV", systemImage: "tv.fill").font(Arcade.font(16, .heavy)).foregroundStyle(Arcade.sea)
                Spacer()
                TennisBallIcon().frame(width: 30, height: 30)
            }
            VStack(spacing: 4) {
                Text(screenName).font(Arcade.font(15, .heavy)).tracking(2).foregroundStyle(.white.opacity(0.6))
                if menu.screen == .story, let line = menu.storyLine {
                    // The same line the TV is showing, so the story reads on either screen.
                    Text(line.text).font(Arcade.font(SportsSession.shared.bigText ? 24 : 18, .bold)).foregroundStyle(.white).multilineTextAlignment(.center)
                        .padding(.horizontal, 14).fixedSize(horizontal: false, vertical: true)
                    Text("A: next  ·  B: skip").font(Arcade.font(13, .heavy)).foregroundStyle(Arcade.gold)
                } else {
                    Text(focusName).font(Arcade.font(SportsSession.shared.bigText ? 32 : 26)).italic().foregroundStyle(.white).lineLimit(1).minimumScaleFactor(0.6)
                }
                if !menu.notice.isEmpty { Text(menu.notice).font(Arcade.font(14, .semibold)).foregroundStyle(Arcade.gold) }
            }
            .frame(maxWidth: .infinity).padding(.vertical, 14)
            .background(RoundedRectangle(cornerRadius: 20).fill(Arcade.navyDeep.opacity(0.7)))
            Spacer(minLength: 0)
            DPad(menu: menu)
            Spacer(minLength: 0)
            HStack(spacing: 40) {
                RemoteButton(label: "B", caption: "Back", top: Arcade.skyDeep, bottom: Arcade.navy, size: 84) { menu.back() }
                RemoteButton(label: "A", caption: "Select", top: Arcade.sun, bottom: Arcade.sunDeep, size: 110) { menu.select() }
            }
            Text(menu.screen == .loading ? "Loading the court on your TV…" : "Look at your TV — this phone is your remote and your racket.")
                .font(Arcade.font(13, .semibold)).foregroundStyle(.white.opacity(0.6)).multilineTextAlignment(.center)
        }
        .padding(24)
        .background(LinearGradient(colors: [Arcade.navy, Arcade.navyDeep], startPoint: .top, endPoint: .bottom).ignoresSafeArea())
        .preferredColorScheme(.dark)
    }

    private var screenName: String {
        switch menu.screen {
        case .title: "TITLE"; case .main: "MAIN MENU"; case .gameSelect: "CHOOSE YOUR GAME"
        case .hub(let sport): sport.title; case .locked(let sport): "\(sport.title) · COMING SOON"
        case .campaign: "ISLAND CIRCUIT"; case .exhibition: "EXHIBITION"; case .training: "TRAINING"
        case .character: "CHARACTER"; case .settings: "SETTINGS"; case .howTo: "HOW TO PLAY"; case .golfLesson: "GOLF LESSON"
        case .connect: "CONNECT"; case .loading: "LOADING"; case .results: "RESULTS"
        case .story: menu.storyLine.map { TennisStory.name(for: $0.speaker) } ?? "STORY"
        }
    }
    private var focusName: String {
        let id = menu.focused
        if id.hasPrefix("round"), let r = Int(id.dropFirst(5)) {
            let o = TennisCampaign.draw[r]
            return TennisCampaign.shared.unlocked(r) ? "\(o.roundTitle) · \(o.name)" : "\(o.roundTitle) · Locked"
        }
        switch id {
        case "start": return menu.screen == .title ? "Press A to start" : "Start"
        case "level" where menu.screen == .training: return "Coach: \(TennisMenu.trainingLevels[menu.trainingLevel].name)"
        case "": return "…"
        default: return TennisMenu.label(for: id)
        }
    }
}

/// Four arrows round a centre select, and swipes anywhere on it move too.
private struct DPad: View {
    let menu: TennisMenu
    var body: some View {
        let size: CGFloat = 250
        ZStack {
            Circle().fill(LinearGradient(colors: [Color(white: 0.22), Color(white: 0.08)], startPoint: .top, endPoint: .bottom))
                .overlay(Circle().strokeBorder(.white.opacity(0.15), lineWidth: 2))
                .shadow(color: .black.opacity(0.5), radius: 14, y: 8)
            arrow("chevron.up", .up).offset(y: -size * 0.32)
            arrow("chevron.down", .down).offset(y: size * 0.32)
            arrow("chevron.left", .left).offset(x: -size * 0.32)
            arrow("chevron.right", .right).offset(x: size * 0.32)
            Button { menu.select() } label: {
                Circle().fill(Color(white: 0.16)).frame(width: size * 0.3, height: size * 0.3)
                    .overlay(Circle().strokeBorder(.white.opacity(0.2), lineWidth: 2))
            }.buttonStyle(PressStyle())
        }
        .frame(width: size, height: size)
        .gesture(DragGesture(minimumDistance: 30).onEnded { v in
            let dx = v.translation.width, dy = v.translation.height
            if abs(dx) > abs(dy) { menu.move(dx < 0 ? .left : .right) } else { menu.move(dy < 0 ? .up : .down) }
        })
    }
    private func arrow(_ icon: String, _ direction: MenuMove) -> some View {
        Button { menu.move(direction) } label: {
            Image(systemName: icon).font(.system(size: 34, weight: .black)).foregroundStyle(.white)
                .frame(width: 80, height: 80).contentShape(Rectangle())
        }
        .buttonStyle(PressStyle())
        .accessibilityLabel("Move \(String(describing: direction))")
    }
}

private struct RemoteButton: View {
    let label: String
    let caption: String
    let top: Color
    let bottom: Color
    let size: CGFloat
    var action: () -> Void
    var body: some View {
        VStack(spacing: 8) {
            Button(action: action) {
                Text(label).font(Arcade.font(size * 0.42)).foregroundStyle(.white)
                    .frame(width: size, height: size)
                    .background(Circle().fill(LinearGradient(colors: [top, bottom], startPoint: .top, endPoint: .bottom)))
                    .overlay(Circle().strokeBorder(.white.opacity(0.35), lineWidth: 3))
                    .shadow(color: bottom.opacity(0.6), radius: 12, y: 6)
            }.buttonStyle(PressStyle())
            Text(caption.uppercased()).font(Arcade.font(13, .heavy)).tracking(2).foregroundStyle(.white.opacity(0.7))
        }
        .accessibilityLabel(caption)
    }
}

private struct PressStyle: ButtonStyle {
    func makeBody(configuration: Configuration) -> some View {
        configuration.label.scaleEffect(configuration.isPressed ? 0.9 : 1).brightness(configuration.isPressed ? 0.12 : 0)
            .animation(.spring(response: 0.2, dampingFraction: 0.6), value: configuration.isPressed)
    }
}

// MARK: - In game

/// The phone during a match: your racket. Its string bed is the same hit radar as the TV's,
/// showing where each shot met the strings and how well it was timed.
struct TennisRacketController: View {
    @Bindable var session: SportsSession
    @State private var showOptions = false
    @State private var power = 0.6
    @State private var steering = 0.0
    var body: some View {
        VStack(spacing: 14) {
            if session.ready && session.loading.finished { Scoreline(session: session) }
            if let step = session.tutorialStep { TutorialPanel(step: step) }
            if !session.ready || !session.loading.finished {
                // The same loading screen as the TV: tips, how-to cards, the flowing bar.
                LoadingScreen(menu: .shared, compact: true).padding(-24)
            } else if !session.touch && !session.axisGate.locked {
                ScrollView { AxisGatePanel(session: session) }
            } else if session.measuringDelay {
                DelayProbePanel()
            } else if session.timingPrompt {
                TimingCheckPrompt(session: session)
            } else if session.checkingTiming {
                TimingCheckPanel(countdownEnds: session.timingCountdownEnds)
            } else if !session.paused && (session.tennisPhase == "serve" || session.tennisPhase == "toss") {
                ServePanel(session: session)
            } else if !session.paused && session.tennisPhase == "receive" {
                ReceivePanel(session: session)
            } else {
                RacketRadar(contacts: session.contacts)
                    .frame(maxWidth: .infinity, maxHeight: .infinity)
                    .overlay(alignment: .top) { LastHit(contact: session.contacts.last) }
                if !session.timingNote.isEmpty && session.contacts.isEmpty {
                    Text(session.timingNote).font(Arcade.font(14, .bold)).foregroundStyle(Arcade.lime).multilineTextAlignment(.center)
                }
                ProgressView(value: session.stamina) { Text("STAMINA").font(Arcade.font(12, .heavy)).tracking(2).foregroundStyle(.white.opacity(0.7)) }
                    .tint(session.stamina > 0.35 ? Arcade.lime : Arcade.crimson)
                if session.touch {
                    Slider(value: $steering, in: -1...1) { Text("Court position") }
                        .onChange(of: steering) { _, v in session.steer(v) }
                    HStack {
                        Slider(value: $power, in: 0.15...1) { Text("Swing power") }
                        Button("Swing") { session.swing(power) }.font(Arcade.font(20)).buttonStyle(.borderedProminent).tint(Arcade.sunDeep)
                            .disabled(session.paused)
                    }
                }
            }
            if session.ready && session.paused && !session.measuringDelay && (session.touch || session.axisGate.locked) {
                Text(session.status).font(Arcade.font(14, .semibold)).foregroundStyle(.white.opacity(0.8)).multilineTextAlignment(.center)
                if !session.delayTip.isEmpty {
                    Label(session.delayTip, systemImage: "tv.badge.wifi")
                        .font(Arcade.font(13, .bold)).foregroundStyle(Arcade.gold).multilineTextAlignment(.leading)
                        .padding(10).background(RoundedRectangle(cornerRadius: 12).fill(Arcade.navyDeep.opacity(0.7)))
                }
                ArcadeButton(title: "Ready", icon: "play.fill", focused: false, top: Arcade.lime, bottom: Color.green, size: 28) { session.readyToPlay() }
            }
            HStack {
                Button { if session.paused { showOptions = true } else { session.pause(); showOptions = true } } label: {
                    Label(session.paused ? "Options" : "Pause", systemImage: session.paused ? "slider.horizontal.3" : "pause.fill")
                        .font(Arcade.font(18, .heavy)).foregroundStyle(.white)
                        .padding(.horizontal, 20).padding(.vertical, 12)
                        .background(Capsule().fill(Arcade.navyDeep.opacity(0.8)))
                }
                Spacer()
                if !session.touch && !session.trackingWarning.isEmpty {
                    Image(systemName: "exclamationmark.triangle.fill").foregroundStyle(.orange)
                }
            }
        }
        .padding(20)
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .background(LinearGradient(colors: [Arcade.skyDeep, Arcade.navyDeep], startPoint: .top, endPoint: .bottom).ignoresSafeArea())
        .preferredColorScheme(.dark)
        .confirmationDialog("Match options", isPresented: $showOptions, titleVisibility: .visible) {
            if session.paused && session.ready { Button("Resume") { session.readyToPlay() } }
            if !session.touch { Button("Re-aim at the TV") { session.beginAxisCapture() }; Button("Left/right swapped — flip") { session.flipSteering() } }
            if session.displayConnected && session.sport == "tennis" { Button("Re-check swing timing") { session.recheckTiming() } }
            Button(session.touch ? "Use motion controls" : "Use touch controls") { if session.touch { session.useMotion() } else { session.useTouch() } }
            Button("Quit to menu", role: .destructive) { session.end() }
        }
    }
}

// MARK: - Serve

/// The player's serve on the controller: aim in the target box, walk along the baseline,
/// then TOSS with the meter in the middle. After the toss: swing as the TV's power bar peaks.
private struct ServePanel: View {
    @Bindable var session: SportsSession
    var body: some View {
        VStack(spacing: 10) {
            if session.tennisPhase == "toss" {
                Spacer()
                ArcadeText(text: "SWING AT THE TOP!", size: 34, top: .white, bottom: Arcade.gold)
                Text("Watch the power bar on the TV — it's full at the top of the toss, and the gold band is a perfect serve.")
                    .font(Arcade.font(15, .semibold)).foregroundStyle(.white.opacity(0.85)).multilineTextAlignment(.center)
                if session.touch {
                    ArcadeButton(title: "Swing", icon: "bolt.fill", focused: false, top: Arcade.sun, bottom: Arcade.sunDeep, size: 30) { session.swing(0.8) }
                }
                Spacer()
            } else {
                Text("YOUR SERVE · \(session.serveFromDeuce ? "DEUCE" : "AD") COURT")
                    .font(Arcade.font(14, .heavy)).tracking(2).foregroundStyle(.white.opacity(0.7))
                ServeAimPad(session: session).frame(maxWidth: .infinity).frame(height: 135)
                Text("Drag to aim · drag up for deep").font(Arcade.font(12, .semibold)).foregroundStyle(.white.opacity(0.6))
                MoveButtons(session: session, caption: "Move along the baseline")
                Text("Watch the meter under your player on the TV: press TOSS with the ticker in the green to land it on your aim.")
                    .font(Arcade.font(13, .semibold)).foregroundStyle(.white.opacity(0.8)).multilineTextAlignment(.center)
                ArcadeButton(title: "TOSS", icon: "arrow.up.circle.fill", focused: false, top: Arcade.lime, bottom: Color.green, size: 34) {
                    UIImpactFeedbackGenerator(style: .rigid).impactOccurred()
                    session.toss()
                }
            }
        }
    }
}

/// The target box seen from behind the baseline, with the aim point on it. The T (centre
/// line) is on the right of the box from the deuce court and on the left from the ad court.
private struct ServeAimPad: View {
    @Bindable var session: SportsSession
    var body: some View {
        GeometryReader { g in
            let w = g.size.width, h = g.size.height
            let boxW = w * 0.5, boxLeft = session.serveFromDeuce ? 0 : w * 0.5
            let across = session.serveAim.across, depth = session.serveAim.depth
            let u = session.serveFromDeuce ? 1 - (across + 1) / 2 : (across + 1) / 2
            let point = CGPoint(x: boxLeft + u * boxW, y: (1 - depth) * h)
            ZStack {
                RoundedRectangle(cornerRadius: 8).fill(Color(red: 0.12, green: 0.30, blue: 0.75))
                Rectangle().fill(Arcade.sky.opacity(0.35)).frame(width: boxW, height: h).position(x: boxLeft + boxW / 2, y: h / 2)
                Path { p in
                    p.move(to: CGPoint(x: w / 2, y: 0)); p.addLine(to: CGPoint(x: w / 2, y: h))
                    p.addRect(CGRect(x: 1, y: 1, width: w - 2, height: h - 2))
                }.stroke(.white, lineWidth: 2)
                Rectangle().fill(.white).frame(height: 5).position(x: w / 2, y: h - 2)
                Text("NET").font(Arcade.font(10, .heavy)).foregroundStyle(.white.opacity(0.7)).position(x: w / 2, y: h - 12)
                Text("T").font(Arcade.font(12, .heavy)).foregroundStyle(.white.opacity(0.8))
                    .position(x: session.serveFromDeuce ? boxW - 12 : boxW + 12, y: 12)
                Text("WIDE").font(Arcade.font(11, .heavy)).foregroundStyle(.white.opacity(0.8))
                    .position(x: session.serveFromDeuce ? 26 : w - 26, y: 12)
                Circle().fill(Arcade.gold).frame(width: 26, height: 26)
                    .overlay(Circle().strokeBorder(Arcade.navyDeep, lineWidth: 3))
                    .shadow(color: Arcade.gold, radius: 8)
                    .position(point)
            }
            .contentShape(Rectangle())
            .gesture(DragGesture(minimumDistance: 0).onChanged { v in
                let x = min(max(v.location.x, boxLeft), boxLeft + boxW)
                let u = (x - boxLeft) / boxW
                let across = session.serveFromDeuce ? 1 - 2 * u : 2 * u - 1
                session.setServeAim(across: across, depth: 1 - min(max(v.location.y / h, 0), 1))
            })
        }
    }
}

/// While the opponent serves: get in position.
private struct ReceivePanel: View {
    @Bindable var session: SportsSession
    var body: some View {
        VStack(spacing: 18) {
            Spacer()
            ArcadeText(text: "RETURN OF SERVE", size: 30, top: .white, bottom: Arcade.sky)
            Text("Walk left or right to where you think the serve is going. Swing when it arrives.")
                .font(Arcade.font(15, .semibold)).foregroundStyle(.white.opacity(0.85)).multilineTextAlignment(.center)
            MoveButtons(session: session, caption: "Position")
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
            HStack(spacing: 28) {
                hold("chevron.left", -1)
                hold("chevron.right", 1)
            }
            Text(caption.uppercased()).font(Arcade.font(11, .heavy)).tracking(2).foregroundStyle(.white.opacity(0.6))
        }
    }
    private func hold(_ icon: String, _ direction: Double) -> some View {
        Image(systemName: icon).font(.system(size: 30, weight: .black)).foregroundStyle(.white)
            .frame(width: 86, height: 64)
            .background(RoundedRectangle(cornerRadius: 18).fill(Arcade.navyDeep.opacity(0.8)))
            .overlay(RoundedRectangle(cornerRadius: 18).strokeBorder(.white.opacity(0.3), lineWidth: 2))
            .scaleEffect(held == direction ? 0.92 : 1)
            .gesture(DragGesture(minimumDistance: 0)
                .onChanged { _ in if held != direction { held = direction; session.nudge(direction) } }
                .onEnded { _ in held = 0; session.nudge(0) })
            .accessibilityLabel(direction < 0 ? "Move left" : "Move right")
    }
}

/// While the TV flashes: the camera is timing how far its picture lags the game.
private struct DelayProbePanel: View {
    @State private var pulse = false
    var body: some View {
        VStack(spacing: 16) {
            Spacer()
            Image(systemName: "tv").font(.system(size: 64, weight: .bold)).foregroundStyle(Arcade.gold)
                .scaleEffect(pulse ? 1.08 : 0.95)
            Text("Keep pointing at the TV").font(Arcade.font(24)).foregroundStyle(.white)
            Text("It will flash a few times while we time its picture, so your swings land when you see the ball.")
                .font(Arcade.font(15, .semibold)).foregroundStyle(.white.opacity(0.8)).multilineTextAlignment(.center)
            ProgressView().tint(Arcade.gold)
            Spacer()
        }
        .onAppear { withAnimation(.easeInOut(duration: 0.5).repeatForever(autoreverses: true)) { pulse = true } }
    }
}

/// Offered the first time on a TV: what the timing check is for, and a button to start it
/// when the player is ready (it used to begin on its own the moment play resumed).
private struct TimingCheckPrompt: View {
    let session: SportsSession
    var body: some View {
        VStack(spacing: 14) {
            Spacer()
            Image(systemName: "metronome.fill").font(.system(size: 56, weight: .bold)).foregroundStyle(Arcade.gold)
            Text("Timing check").font(Arcade.font(26)).foregroundStyle(.white)
            Text("Every TV shows the picture a little late. This quick check measures that delay so your swings land exactly when you see the ball.\n\nA ball will bounce on a line on the TV. Swing every time it drops onto the line — a steady rhythm, about ten seconds.")
                .font(Arcade.font(15, .semibold)).foregroundStyle(.white.opacity(0.85)).multilineTextAlignment(.center)
                .padding(.horizontal, 12)
            ArcadeButton(title: "Start timing check", icon: "play.fill", focused: true, top: Arcade.sun, bottom: Arcade.sunDeep, size: 22) {
                session.startTimingCheck()
            }
            Button("Skip for now") { session.timingPrompt = false }
                .font(Arcade.font(15, .bold)).foregroundStyle(.white.opacity(0.7))
            Spacer()
        }
    }
}

/// While the TV runs the timing check: a get-ready countdown, then swing with the ball.
private struct TimingCheckPanel: View {
    let countdownEnds: Date?
    @State private var beat = false
    var body: some View {
        TimelineView(.periodic(from: .now, by: 0.2)) { context in
            let left = countdownEnds.map { max(0, $0.timeIntervalSince(context.date)) } ?? 0
            VStack(spacing: 16) {
                Spacer()
                if left > 0 {
                    Text("Get ready").font(Arcade.font(26)).foregroundStyle(.white)
                    Text("\(Int(left.rounded(.up)))").font(Arcade.font(96)).foregroundStyle(Arcade.gold).monospacedDigit()
                    Text("Swing every time the ball drops onto the line.")
                        .font(Arcade.font(16, .semibold)).foregroundStyle(.white.opacity(0.85)).multilineTextAlignment(.center)
                } else {
                    Image(systemName: "metronome.fill").font(.system(size: 64, weight: .bold)).foregroundStyle(Arcade.gold)
                        .scaleEffect(beat ? 1.1 : 0.92)
                    Text("Swing on every bounce!").font(Arcade.font(26)).foregroundStyle(.white)
                    Text("Watch the TV and swing each time the ball hits the line — a steady rhythm.")
                        .font(Arcade.font(15, .semibold)).foregroundStyle(.white.opacity(0.8)).multilineTextAlignment(.center)
                }
                Spacer()
            }
        }
        .onAppear { withAnimation(.easeInOut(duration: 0.4).repeatForever(autoreverses: true)) { beat = true } }
    }
}

private struct Scoreline: View {
    let session: SportsSession
    var body: some View {
        HStack(spacing: 12) {
            Text("YOU").font(Arcade.font(18)).foregroundStyle(Arcade.sun)
            Text("\(session.score.player)").font(Arcade.font(34)).foregroundStyle(.white).monospacedDigit()
            Text("–").font(Arcade.font(28)).foregroundStyle(.white.opacity(0.5))
            Text("\(session.score.opponent)").font(Arcade.font(34)).foregroundStyle(.white).monospacedDigit()
            Text(session.opponentName.uppercased()).font(Arcade.font(18)).foregroundStyle(Arcade.sky)
            Spacer()
            if !session.score.detail.isEmpty {
                Text(session.score.detail.components(separatedBy: "·").dropFirst().first?.trimmingCharacters(in: .whitespaces) ?? "")
                    .font(Arcade.font(14, .bold)).foregroundStyle(.white.opacity(0.75)).lineLimit(1)
            }
        }
        .padding(.horizontal, 18).padding(.vertical, 10)
        .background(RoundedRectangle(cornerRadius: 18).fill(Arcade.navyDeep.opacity(0.75)))
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
                    ArcadeText(text: contact.gradeName + "!", size: 40, top: .white, bottom: color(contact))
                    // Which way the swing was off, so timing can be learned ball by ball.
                    Text(contact.timingWord)
                        .font(Arcade.font(15, .heavy)).tracking(2)
                        .foregroundStyle(contact.timingWord == "ON TIME" ? Arcade.lime : .white.opacity(0.85))
                }
                .scaleEffect(pop ? 1 : 1.6).opacity(pop ? 1 : 0)
            }
        }
        .padding(.top, 6)
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
        case 5: return Arcade.gold
        case 4: return Arcade.lime
        case 3: return Color(red: 0.45, green: 0.9, blue: 0.45)
        case 2: return Arcade.sky
        default: return Arcade.crimson
        }
    }

    var body: some View {
        GeometryReader { g in
            // String bed 0.35 m × 0.48 m: keep its real proportions.
            let headH = min(g.size.height * 0.68, g.size.width * 0.9 / 0.73)
            let headW = headH * 0.73
            let centre = CGPoint(x: g.size.width / 2, y: headH / 2 + 10)
            ZStack {
                // Handle and throat.
                Path { p in
                    let base = CGPoint(x: centre.x, y: centre.y + headH / 2)
                    p.move(to: CGPoint(x: base.x - headW * 0.28, y: base.y - headH * 0.05))
                    p.addLine(to: CGPoint(x: base.x - headW * 0.06, y: base.y + headH * 0.22))
                    p.move(to: CGPoint(x: base.x + headW * 0.28, y: base.y - headH * 0.05))
                    p.addLine(to: CGPoint(x: base.x + headW * 0.06, y: base.y + headH * 0.22))
                }.stroke(Arcade.navyDeep, style: StrokeStyle(lineWidth: 14, lineCap: .round))
                RoundedRectangle(cornerRadius: 10)
                    .fill(LinearGradient(colors: [Arcade.sunDeep, Color(red: 0.55, green: 0.15, blue: 0.05)], startPoint: .leading, endPoint: .trailing))
                    .frame(width: headW * 0.16, height: g.size.height - headH - 10 - headH * 0.2)
                    .position(x: centre.x, y: centre.y + headH / 2 + headH * 0.2 + (g.size.height - headH - 10 - headH * 0.2) / 2)
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
                .stroke(.white.opacity(0.35), lineWidth: 1.5)
                .mask(Ellipse().frame(width: headW, height: headH).position(centre))
                // Sweet spot.
                ForEach(0..<3, id: \.self) { i in
                    Ellipse().strokeBorder(Arcade.lime.opacity(0.55 - Double(i) * 0.15), lineWidth: 3)
                        .frame(width: headW * (0.3 + CGFloat(i) * 0.22), height: headH * (0.3 + CGFloat(i) * 0.22))
                        .position(centre)
                }
                Text("SWEET SPOT").font(Arcade.font(11, .heavy)).tracking(2).foregroundStyle(Arcade.lime.opacity(0.8))
                    .position(x: centre.x, y: centre.y + headH * 0.2)
                // Frame.
                Ellipse().strokeBorder(LinearGradient(colors: [Arcade.sun, Arcade.sunDeep], startPoint: .top, endPoint: .bottom), lineWidth: headW * 0.06)
                    .frame(width: headW + headW * 0.06, height: headH + headW * 0.06).position(centre)
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

/// The tutorial's current step on the phone: what to do now, and how far through the lesson.
struct TutorialPanel: View {
    let step: (index: Int, count: Int, text: String)
    var body: some View {
        VStack(alignment: .leading, spacing: 8) {
            HStack {
                Label("COACH RAY", systemImage: "graduationcap.fill").font(Arcade.font(13, .heavy)).tracking(2).foregroundStyle(Arcade.navyDeep)
                Spacer()
                Text("STEP \(min(step.index + 1, step.count)) OF \(step.count)").font(Arcade.font(13, .heavy)).foregroundStyle(Arcade.navyDeep)
            }
            Text(step.text).font(Arcade.font(18, .bold)).foregroundStyle(Arcade.navyDeep).fixedSize(horizontal: false, vertical: true)
            HStack(spacing: 4) {
                ForEach(0..<step.count, id: \.self) { i in
                    Capsule().fill(i < step.index ? Arcade.seaDeep : i == step.index ? Arcade.navyDeep : Arcade.navyDeep.opacity(0.25)).frame(height: 6)
                }
            }
        }
        .padding(14)
        .background(RoundedRectangle(cornerRadius: 18).fill(LinearGradient(colors: [Arcade.gold, Arcade.goldDeep], startPoint: .top, endPoint: .bottom)))
    }
}
