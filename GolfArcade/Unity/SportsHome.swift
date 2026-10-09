import SwiftUI

/// The app's root. Tennis is a game front end: with no TV the phone shows the menu itself;
/// with a TV connected the menu moves to the TV and the phone becomes its remote; during a
/// match the phone is the racket. The classic multi-sport menu is still one option away.
struct SportsHome: View {
    @State private var session = SportsSession.shared
    @State private var menu = TennisMenu.shared
    @State private var onboarding = OnboardingFlow.shared
    @Environment(\.scenePhase) private var scenePhase
    var body: some View {
        Group {
            if session.displayConnected && !session.active { TennisRemote() }
            else if onboarding.holdsMenu { OnboardingRoot(flow: onboarding) }
            else if session.active && (!session.loading.finished || (session.sport == "golf" && !session.ready)) { LoadingScreen(menu: menu, compact: true) }
            else if menu.screen.isOnline && menu.screen != .online(.match) {
                if session.displayConnected { TennisRemote() } else { TennisPhoneMenu() }
            }
            else if session.active && session.finishedMatch != nil {
                MatchFinishControls(session: session)
                    .background(Club.lagoonDeep.ignoresSafeArea())
            }
            else if session.active && session.menuPauseVisible && session.sport != "golf" { IslandPauseScreen(compact: true) }
            else if session.active && session.sport == "golf" {
                GolfPhoneController(session: session)
            }
            else if session.active { TennisRacketController(session: session) }
            else if menu.classic { ClassicSportsHome() }
            else if session.displayConnected { TennisRemote() }
            else { TennisPhoneMenu() }
        }
        .sheet(item: Binding(get: { menu.online.sheet }, set: { menu.online.sheet = $0 })) { sheet in
            OnlineGameCenterSheet(controller: sheet.controller).ignoresSafeArea()
        }
        .safeAreaInset(edge: .bottom, spacing: 0) {
            if !session.active || session.sport != "golf" { BetaFeedbackBar() }
        }
        .simultaneousGesture(TapGesture().onEnded {
            guard session.active, session.loading.finished, !session.paused, session.multiplayerMatchID == nil else { return }
            if ["intro", "point", "finished"].contains(session.tennisPhase) || ["Intro", "Result"].contains(session.golfPhase) {
                session.command("presentationSkip")
            }
        })
        .safeAreaInset(edge: .top) {
            if session.active && session.sport != "golf" {
                HStack {
                    Text(session.displayConnected ? "Phone controller" : "Game controls")
                    Spacer()
                    Button("Exit Game") { session.exitGame() }.accessibilityIdentifier("controller-exit")
                }
                .font(IslandUI.font(16, bold: true)).foregroundStyle(IslandUI.navy)
                .padding(.horizontal, 20).frame(minHeight: 48).background(IslandUI.paper)
            }
        }
        .overlay(alignment: .bottom) {
            TimelineView(.periodic(from: .now, by: 0.1)) { _ in
                if session.loading.finished, session.loading.tipRemaining > 0, let tip = session.loading.tip {
                    Text(tip).font(IslandUI.font(15, bold: true)).foregroundStyle(.white).padding(12).background(IslandUI.navy, in: Capsule()).padding(.bottom, 48)
                }
            }
        }
        .overlay(alignment: .bottom) {
            if session.active && session.loading.finished && !session.paused && session.multiplayerMatchID == nil && (session.tennisPhase == "intro" || session.golfPhase == "Intro") {
                Button("Skip intro") { session.command("presentationSkip") }
                    .font(IslandUI.font(18, bold: true)).padding(16).background(IslandUI.lime, in: Capsule()).foregroundStyle(IslandUI.navy)
                    .accessibilityIdentifier("presentation-skip").padding(.bottom, 16)
            }
        }
        .overlay(alignment:.top) { if session.active && menu.screen == .online(.match) { MultiplayerMatchOverlay() } }
        .animation(.easeInOut(duration: 0.3), value: session.active)
        .animation(.easeInOut(duration: 0.3), value: session.displayConnected)
        .onChange(of:menu.online.service.lobby?.revision) { _,_ in menu.online.sync(menu) }
        .onChange(of:menu.online.service.lastError) { _,error in if let error { menu.onlineNotice(error) } }
        .onChange(of:menu.online.service.pendingInvite) { _,invite in if invite != nil { menu.online.acceptInvite(menu) } }
        .task {
            menu.online.installCallbacks(menu)
            let args = ProcessInfo.processInfo.arguments
            #if DEBUG
            if args.contains("-golfControllerDesign") {
                OnboardingFlow.shared.exitToMenu()
                SportsDisplays.shared.external = UIWindow(frame: CGRect(x: 0, y: 0, width: 1920, height: 1080))
                session.sport = "golf"; session.active = true; session.ready = true
                session.displayConnected = true; session.touch = false; session.paused = false
                session.golfShotReady = true; session.golfPhase = "Aim"
                session.golfController = .reference; session.loading.cancel()
                if args.contains("-golfControllerNotReady") { session.ready = false }
                if args.contains("-golfControllerLoading") { session.loading.begin(now: Date()) }
            }
            if args.contains("-controllerMenuCheck") {
                SportsDisplays.shared.external = UIWindow(frame: CGRect(x: 0, y: 0, width: 1920, height: 1080))
                session.displayConnected = true
            }
            if let i = args.firstIndex(of: "-controllerPhaseCheck"), let phase = args[safe: i + 1] {
                SportsDisplays.shared.external = UIWindow(frame: CGRect(x: 0, y: 0, width: 1920, height: 1080))
                session.displayConnected = true; session.active = true; session.ready = true
                session.sport = "tennis"; session.touch = true; session.loading.cancel()
                session.setupStage = .playing; session.paused = false; session.tennisPhase = phase
                session.canDive = phase == "rally"; session.emoteWindow = phase == "point" ? "point" : ""
                if phase == "timing" { session.offerTimingCalibration() }
                if phase == "ready" { session.setupStage = .ready; session.paused = true }
            }
            OnlineLobbyProofDriver.start(menu,args:args)
            #endif
            if let index = args.firstIndex(of:"--lobby-mock"), let count = args[safe:index+1].flatMap(Int.init) {
                try? menu.online.service.enableMock(count:count,player:menu.player ?? Player(name:"Player 1",colorIndex:0)); menu.showOnline(.lobby)
            }
            if ProcessInfo.processInfo.arguments.contains("--resume-tennis-round-2") {
                menu.resumeCampaign(round: 1)
            }
            if ProcessInfo.processInfo.arguments.contains("--resume-tennis-round-3") {
                menu.resumeCampaign(round: 2)
            }
            if ProcessInfo.processInfo.arguments.contains("--resume-tennis-round-4") {
                menu.resumeCampaign(round: 3)
            }
            if ProcessInfo.processInfo.arguments.contains("--resume-tennis-round-5") {
                menu.resumeCampaign(round: 4)
            }
            if ProcessInfo.processInfo.arguments.contains("--lobby"), menu.screen == .title { menu.tap("start") }
            if ProcessInfo.processInfo.arguments.contains("--character-editor") { menu.openCharacterEditor() }
            // `-benchTennis`: play a self-driving rally on the phone and log frame times.
            if args.contains("--playability-check") { await PlayabilityCheck.run(); return }
            if SportsSession.benchmark && !session.active {
                session.sport = args.contains("-benchGolf") ? "golf" : "tennis"
                session.touch = true; session.start(preview:!session.displayConnected)
            }
        }
        .background(DisplayRegistration().frame(width:0,height:0))
        .onChange(of:scenePhase) { _,phase in
            if phase != .active { session.pause(reason:"App inactive — return to the controller and tap Ready") }
            SportsRuntime.shared().setForeground(phase == .active)
        }
    }
}

/// Every sport and every raw option, as the app had before the tennis front end.
struct ClassicSportsHome: View {
    @State private var session = SportsSession.shared
    var body: some View {
        @Bindable var session=session
        NavigationStack {
            Group {
            if session.active && session.tennisControllerActive {
                TennisController(session:session)
            } else {
            Form {
                Section("Display") {
                    Label(session.displayConnected ? "External display connected" : "Connect AirPlay or a wired display",systemImage:"tv")
                    Text(session.status).accessibilityIdentifier("sportsStatus")
                }
                if session.active {
                    SportsControls(session:session)
                } else {
                    Section("Game") {
                        Picker("Sport",selection:$session.sport) { Text("Golf · Cliffside").tag("golf"); Text("Tennis Rally").tag("tennis") }.accessibilityIdentifier("sportPicker")
                        Picker("Player",selection:$session.playerIndex) {
                            ForEach(session.players.indices,id:\.self) { i in Text(session.players[i].name).tag(i) }
                        }
                    }
                    SportsPlayerEditor(session:session)
                    Section("Controls") {
                        Toggle("Use touch controls",isOn:$session.touch).accessibilityIdentifier("touchControls")
                        Text("Tennis: you set the court direction once by aiming the back of the phone at the TV. After that hold the phone however you like — the grip can change between forehands and backhands. Step sideways to move; make a deliberate stroke to hit. Golf: swing the phone to hit.")
                        Slider(value:$session.travel,in:0.3...1.2) { Text("Movement range") }
                        Text("Movement range: step \(Int(session.travel*100)) cm for full court width")
                        Toggle("Sound",isOn:$session.sound)
                        Toggle("Haptics",isOn:$session.haptics)
                        Toggle("120 fps on ProMotion displays",isOn:$session.highFrameRate)
                    }
                    if session.sport == "tennis" {
                        Section("Tennis") {
                            Picker("Opponent",selection:$session.tennisDifficulty) {
                                Text("Relaxed").tag(0.15); Text("Standard").tag(0.45); Text("Tough").tag(0.8)
                            }.pickerStyle(.segmented).accessibilityIdentifier("tennisDifficulty")
                            Button("Show the coaching tips again") { UserDefaults.standard.set(true,forKey:"sports.resetCoaching") }
                        }
                    }
                    Section {
                        Button("Play on external display") { session.start() }.accessibilityIdentifier("startExternalGame")
                        Text("TV shows the game. This iPhone stays your controller.")
                        Text("A connected TV or Mac is required to play.")
                    }
                }
            }
            }
            }
            .navigationTitle("Sports Arcade")
            .toolbar {
                if !session.active { Button("Tennis menu") { TennisMenu.shared.classic=false } }
            }
            .buttonStyle(.borderless)
        }
    }
}

/// Blocks play until the court direction is locked. A single Ready tap cannot reveal where
/// the TV is when the phone's facing changes every stroke, so this is captured once up front.
struct AxisGatePanel:View {
    @Bindable var session:SportsSession
    var body:some View {
        VStack(spacing:14) {
            Text("1 · TV scan").font(.headline)
            SportsCameraPreview(motion:session.motion)
                .frame(height:180).clipShape(RoundedRectangle(cornerRadius:12))
                .overlay(RoundedRectangle(cornerRadius:12).stroke(session.axisGate.progress>0 ? .green : .secondary,lineWidth:2))
                .overlay { Image(systemName: "plus").font(.title2).foregroundStyle(.white).shadow(radius: 2) }
                .accessibilityIdentifier("axisGatePreview")
            Text("Point the rear camera at your TV or MacBook screen and hold still. Keep the screen under the crosshair, with some of the room visible. A laptop can sit below eye level. Leave enough room to swing.")
                .font(.subheadline).multilineTextAlignment(.center)
            ProgressView(value:session.axisGate.progress)
            Text(session.axisGate.message).font(.footnote).multilineTextAlignment(.center)
                .accessibilityIdentifier("axisGateMessage")
            if !session.axisGate.detail.isEmpty {
                Text(session.axisGate.detail).font(.caption2).foregroundStyle(.secondary)
                    .accessibilityIdentifier("axisGateDetail")
            }
            Text("You only do this once per session. After it locks, any grip or angle works.")
                .font(.caption).multilineTextAlignment(.center)
            if !session.axisGate.locked {
                Button("Use the direction I'm pointing now") { session.useCurrentDirection() }
                    .accessibilityIdentifier("forceAxis")
            }
            Button("Use touch controls instead") { session.useTouch() }
        }.padding(20).frame(maxWidth:.infinity)
            .accessibilityIdentifier("axisGatePanel")
    }
}

private struct TennisController:View {
    @Bindable var session:SportsSession
    @State private var steering=0.0
    @State private var aim=0.0
    @State private var power=0.6
    var body:some View {
        GeometryReader { geometry in
            VStack(spacing:16) {
                HStack {
                    Label("Tennis controller",systemImage:"tennis.racket")
                    Spacer()
                    Button(session.paused ? "Ready" : "Pause") {
                        if session.paused { session.readyToPlay() } else { session.pause() }
                    }.disabled(!session.touch && !session.axisGate.locked)
                        .accessibilityIdentifier("sessionPauseResume")
                }
                if !session.touch && !session.axisGate.locked { AxisGatePanel(session:session) }
                Text(session.paused ? session.status : session.touch ? "Touch controls active" : "Step sideways to position. Swing the phone to hit.")
                    .font(.subheadline).multilineTextAlignment(.center).accessibilityIdentifier("sportsStatus")
                if !session.touch && !session.trackingWarning.isEmpty {
                    Label(session.trackingWarning,systemImage:"exclamationmark.triangle.fill")
                        .font(.footnote).foregroundStyle(.orange)
                        .accessibilityIdentifier("trackingWarning")
                }
                ProgressView("Stamina",value:session.stamina)
                Spacer(minLength:0)
                Text("Aim · \(aim < -0.15 ? "Left" : aim > 0.15 ? "Right" : "Center")")
                Slider(value:$aim,in:-1...1) { Text("Shot direction") }
                    .onChange(of:aim) { _,value in session.setAim(value) }
                    .accessibilityIdentifier("tennisAim")
                Text("Yellow target: aim · Cyan line: ball flight\nHarder strokes are faster but need cleaner timing and positioning.")
                    .font(.footnote).multilineTextAlignment(.center)
                Text(session.feedback).font(.footnote).multilineTextAlignment(.center)
                    .accessibilityIdentifier("controllerFeedback")
                if session.touch {
                    Slider(value:$steering,in:-1...1) { Text("Court position") }
                        .onChange(of:steering) { _,value in session.steer(value) }
                    Slider(value:$power,in:0.15...1) { Text("Swing power") }
                    Button("Swing") { session.swing(power) }
                        .disabled(session.paused || !session.ready).accessibilityIdentifier("controllerSwing")
                }
                if !session.touch && session.axisGate.locked {
                    Button("Left/right are swapped — flip") { session.flipSteering() }
                        .font(.footnote).accessibilityIdentifier("flipSteering")
                    Button("Re-aim at the TV") { session.beginAxisCapture() }
                        .font(.footnote).accessibilityIdentifier("reAimAxis")
                }
                Spacer(minLength:0)
                PointClipControls(session: session)
                Button("Quit to menu",role:.destructive) { if session.multiplayerMatchID != nil { TennisMenu.shared.online.select("net-leave",menu:TennisMenu.shared) } else { session.end() } }
                    .frame(maxWidth:.infinity,minHeight:48)
                    .accessibilityIdentifier("sessionMenu")
            }.padding(24).frame(maxWidth:.infinity,maxHeight:.infinity)
        }
    }
}

private struct SportsPlayerEditor:View {
    @Bindable var session:SportsSession
    var body:some View {
        Section("Permanent character") {
            TextField("Name",text:$session.players[session.playerIndex].name)
            Toggle("Female character",isOn:$session.players[session.playerIndex].standardFemale)
            Picker("Skin tone",selection:$session.players[session.playerIndex].standardSkin) {
                ForEach(0..<6) { Text(["Fair","Light","Tan","Olive","Brown","Deep"][$0]).tag($0) }
            }
            Picker("Hand",selection:$session.players[session.playerIndex].handedness) { Text("Right").tag(Handedness.right); Text("Left").tag(Handedness.left) }
        }
    }
}

private struct SportsControls:View {
    @Bindable var session:SportsSession
    @State private var steering=0.0
    @State private var aim=0.0
    @State private var power=0.6
    var body:some View {
        if session.sport == "tennis" && !session.touch && !session.axisGate.locked {
            Section("Court direction") { AxisGatePanel(session:session) }
        }
        Section("\(session.sport.capitalized) controller") {
            Text(session.touch ? "Input: Touch controls" : "Input: Phone motion")
            if !session.touch && !session.trackingWarning.isEmpty {
                Label(session.trackingWarning,systemImage:"exclamationmark.triangle.fill")
                    .foregroundStyle(.orange).accessibilityIdentifier("trackingWarning")
            }
            Text(session.paused ? "Stand at your center and tap Ready" : "Phone controller active")
            Text(session.feedback).accessibilityIdentifier("controllerFeedback")
            if session.sport == "tennis" { ProgressView("Stamina",value:session.stamina) }
            Button(session.paused ? "Ready" : "Pause") { if session.paused { session.readyToPlay() } else { session.pause() } }
                .disabled(!session.ready || (session.sport == "tennis" && !session.touch && !session.axisGate.locked))
                .accessibilityIdentifier("sessionPauseResume")
            if session.sport == "tennis" && !session.touch && session.axisGate.locked {
                Button("Left/right are swapped — flip") { session.flipSteering() }.accessibilityIdentifier("flipSteering")
                Button("Re-aim at the TV") { session.beginAxisCapture() }.accessibilityIdentifier("reAimAxis")
            }
            if session.sport == "golf" {
                HStack { Button("Previous club") { session.command("club",value:-1) }; Spacer(); Button("Next club") { session.command("club",value:1) } }
            } else { Button("New serve") { session.command("refeed") } }
            Slider(value:$aim,in:-1...1) { Text("Aim") }.onChange(of:aim) { _,value in session.setAim(value) }
        }
        if session.touch {
            Section("Touch fallback") {
                if session.sport == "tennis" {
                    Slider(value:$steering,in:-1...1) { Text("Court position") }.onChange(of:steering) { _,value in session.steer(value) }
                }
                Slider(value:$power,in:0.1...1) { Text("Swing power") }
                Button("Swing") { session.swing(power) }.disabled(session.paused).accessibilityIdentifier("controllerSwing")
            }
        }
        if session.sport == "tennis" {
            Section("Recording") { PointRecordingSettings(session: session) }
        }
        Section("Session") {
            if session.paused {
                Toggle("Sound",isOn:$session.sound).onChange(of:session.sound) { _,v in session.command("sound",value:v ? 1 : 0) }
                Toggle("Haptics",isOn:$session.haptics).onChange(of:session.haptics) { _,v in session.command("haptics",value:v ? 1 : 0) }
            }
            Button("Switch to touch controls") { session.useTouch() }.disabled(session.touch)
            Button("Switch to motion controls") { session.useMotion() }.disabled(!session.touch || !session.ready)
            Button("Restart") { session.end(); session.start() }
            Button("Return to menu",role:.destructive) { session.end() }.accessibilityIdentifier("sessionMenu")
        }
    }
}

@MainActor private enum PlayabilityCheck {
    static func run() async {
        let session = SportsSession.shared, menu = TennisMenu.shared
        OnboardingFlow.shared.exitToMenu()
        guard session.displayConnected else { SportsDiagnostics.write("PLAYABILITY CHECK requires external screen; phone gameplay disabled"); return }
        session.touch = true
        for sport in [Sport.golf, .tennis] {
            menu.begin(MenuLaunch(sport: sport, mode: sport == .golf ? .round : .exhibition, round: sport == .tennis ? 0 : nil))
            guard menu.screen == .map else { SportsDiagnostics.write("PLAYABILITY FAIL map \(sport)"); return }
            let key = sport == .golf ? "postcards" : "resort"
            menu.tap("map-\(key)")
            for _ in 0..<400 {
                if session.ready && session.loading.finished || !session.active { break }
                try? await Task.sleep(for: .milliseconds(100))
            }
            guard session.active && session.ready && !session.paused else {
                SportsDiagnostics.write("PLAYABILITY FAIL \(sport) \(session.status)"); return
            }
            SportsDiagnostics.write("PLAYABILITY READY \(sport) map=\(key) external=\(session.displayConnected)")
            if sport == .golf {
                session.swing(0.65)
                var flight = false
                for _ in 0..<80 {
                    flight = flight || session.golfPhase == "Flight"
                    try? await Task.sleep(for: .milliseconds(100))
                }
                SportsDiagnostics.write("PLAYABILITY \(flight ? "PASS" : "FAIL") golf swing state=\(session.golfPhase) feedback=\(session.feedback)")
            } else {
                try? await Task.sleep(for: .seconds(12))
                SportsDiagnostics.write("PLAYABILITY \(session.active && !session.paused && session.contacts.count > 0 ? "PASS" : "FAIL") tennis contacts=\(session.contacts.count) phase=\(session.tennisPhase)")
            }
            session.end()
            try? await Task.sleep(for: .seconds(1))
        }
        menu.goHome()
        SportsDiagnostics.write("PLAYABILITY CHECK COMPLETE")
    }
}

/// Shot statistics and reactions remain visible until the automatic handoff.
struct GolfShotResultControls: View {
    @Bindable var session: SportsSession
    var body: some View {
        if let party=session.golfController.party {
            VStack(spacing:12) {
                Text(party.outcome).font(Club.ui(22,900)).foregroundStyle(.white)
                HStack {
                    stat("CARRY",party.carry); stat("ROLL",party.roll); stat("TOTAL",party.total)
                }
                Text("Next player in \(Int(ceil(party.resultSeconds)))").font(Club.ui(16,700)).monospacedDigit().foregroundStyle(.white)
                ProgressView(value:max(0,min(1,party.resultSeconds/3))).tint(IslandUI.lime)
            }.accessibilityIdentifier("golf-party-shot-result")
        } else {
            EquippedEmoteControls(title:"YOUR SHOT · EMOTE",ids:session.matchEmotes,
                enabled:!session.paused && session.golfPhase == "Result",notice:"Next shot starts automatically.",identifier:"golf-emote") {
                    session.command("golfEmote",value:Double($0))
                }
        }
    }
    private func stat(_ title:String,_ yards:Double) -> some View {
        VStack { Text(title).font(Club.ui(11,700)); Text("\(Int((yards * (session.golfController.party?.putt == true ? 3 : 1)).rounded())) \(session.golfController.party?.putt == true ? "ft" : "yd")").font(Club.ui(22,900)).monospacedDigit() }
            .frame(maxWidth:.infinity).foregroundStyle(.white)
    }
}

/// A host can choose any guest sharing this phone; peers only receive controls for their own seat.
struct GolfPartyEmoteControls: View {
    @Bindable var session: SportsSession
    let party: GolfControllerReading.Party
    @State private var selectedSeat: Int = -1
    private var localPlayers: [GolfControllerReading.PartyPlayer] { party.players.filter(\.controlled) }
    private var selected: GolfControllerReading.PartyPlayer? {
        localPlayers.first { $0.seat == selectedSeat } ?? localPlayers.first { $0.seat == party.turn } ?? localPlayers.first
    }
    var body: some View {
        VStack(alignment:.leading,spacing:14) {
            Text(party.phase == "result" ? "EVERYONE CAN REACT" : "\(party.player.uppercased())’S TURN")
                .font(Club.ui(20,900)).foregroundStyle(.white)
            if party.shared {
                Text(party.phase == "result" ? "Choose a player, then an emote." : "React as a guest while \(party.player) takes their shot.").font(Club.ui(14,600)).foregroundStyle(.white)
            }
            if localPlayers.count > 1 {
                Picker("Emote as",selection:Binding(get:{selected?.seat ?? -1},set:{selectedSeat=$0})) {
                    ForEach(localPlayers) { player in Text(player.name).tag(player.seat) }
                }.pickerStyle(.menu).tint(.white).accessibilityIdentifier("golf-emote-player")
            }
            if let selected {
                EquippedEmoteControls(title:selected.name.uppercased(),ids:selected.emotes,
                    enabled:selected.canEmote && !session.paused,notice:selected.canEmote ? "Pick an emote" : "Watch the shot · react after it lands",identifier:"golf-party-emote") { slot in
                        session.command("golfEmote",value:Double(slot),value2:Double(selected.seat))
                    }
            }
        }.accessibilityIdentifier("golf-party-controls")
    }
}
