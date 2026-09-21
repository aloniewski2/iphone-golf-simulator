import SwiftUI

struct SportsHome: View {
    @State private var session = SportsSession.shared
    @Environment(\.scenePhase) private var scenePhase
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
            .buttonStyle(.borderless)
            .background(DisplayRegistration().frame(width:0,height:0))
            .onChange(of:scenePhase) { _,phase in
                if phase != .active { session.pause(reason:"App inactive — return to the controller and tap Ready") }
                SportsRuntime.shared().setForeground(phase == .active)
            }
        }
    }
}

/// Blocks play until the court direction is locked. A single Ready tap cannot reveal where
/// the TV is when the phone's facing changes every stroke, so this is captured once up front.
private struct AxisGatePanel:View {
    @Bindable var session:SportsSession
    var body:some View {
        VStack(spacing:14) {
            Text("Set the court direction").font(.headline)
            SportsCameraPreview(motion:session.motion)
                .frame(height:180).clipShape(RoundedRectangle(cornerRadius:12))
                .overlay(RoundedRectangle(cornerRadius:12).stroke(session.axisGate.progress>0 ? .green : .secondary,lineWidth:2))
                .accessibilityIdentifier("axisGatePreview")
            Text("Point the back of your phone at the TV, hold it level, and keep still.")
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
            if session.axisGate.stalledFor > 12 {
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
