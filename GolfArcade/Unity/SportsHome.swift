import SwiftUI

/// The app's root. Tennis is a game front end: with no TV the phone shows the menu itself;
/// with a TV connected the menu moves to the TV and the phone becomes its remote; during a
/// match the phone is the racket. The classic multi-sport menu is still one option away.
struct SportsHome: View {
    @State private var session = SportsSession.shared
    @State private var menu = TennisMenu.shared
    @Environment(\.scenePhase) private var scenePhase
    var body: some View {
        Group {
            if menu.classic { ClassicSportsHome() }
            else if session.active && session.sport == "golf" { GolfPhoneController(session: session) }
            else if session.active { TennisRacketController(session: session) }
            else if session.displayConnected { TennisRemote() }
            else { TennisPhoneMenu() }
        }
        .animation(.easeInOut(duration: 0.3), value: session.active)
        .animation(.easeInOut(duration: 0.3), value: session.displayConnected)
        .task {
            // `-benchTennis`: play a self-driving rally on the phone and log frame times.
            if SportsSession.benchmark && !session.active { session.sport="tennis"; session.start(preview:true) }
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
                        Button("On-phone touch preview") { session.start(preview:true) }.accessibilityIdentifier("startUnityPreview").disabled(session.displayConnected)
                        if session.displayConnected { Text("Disconnect the TV to use on-phone preview.").font(.caption) }
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
            Text("Set the court direction").font(.headline)
            SportsCameraPreview(motion:session.motion)
                .frame(height:180).clipShape(RoundedRectangle(cornerRadius:12))
                .overlay(RoundedRectangle(cornerRadius:12).stroke(session.axisGate.progress>0 ? .green : .secondary,lineWidth:2))
                .overlay { Image(systemName: "plus").font(.title2).foregroundStyle(.white).shadow(radius: 2) }
                .accessibilityIdentifier("axisGatePreview")
            Text("Point the rear camera at your TV or MacBook screen and hold still. Keep the screen under the crosshair, with some of the room visible. A laptop can sit below eye level. Leave enough room to swing; no scan or code is needed.")
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
                Button("Quit to menu",role:.destructive) { session.end() }
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

/// Golf in progress, from the new menu: loading, then the golf controls.
struct GolfPhoneController: View {
    @Bindable var session: SportsSession
    var body: some View {
        if !session.ready || !session.loading.finished {
            ZStack { MenuBackdrop(dim: 0.55); LoadingScreen(menu: .shared, compact: true) }.preferredColorScheme(.dark)
        } else {
            NavigationStack {
                Form { SportsControls(session: session) }
                    .navigationTitle(TennisMenu.shared.launch?.mode == .tutorial ? "Golf lesson — hit a shot" : "Cliffside")
            }
        }
    }
}
