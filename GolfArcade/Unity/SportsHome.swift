import SwiftUI

struct SportsHome: View {
    @State private var session = SportsSession.shared
    @Environment(\.scenePhase) private var scenePhase
    var body: some View {
        @Bindable var session=session
        NavigationStack {
            Form {
                Section("Display") {
                    Label(session.displayConnected ? "External display connected" : "Connect AirPlay or a wired display",systemImage:"tv")
                    Text(session.status).accessibilityIdentifier("sportsStatus")
                }
                if session.active {
                    SportsControls(session:session)
                } else {
                    Section("Game") {
                        Picker("Sport",selection:$session.sport) { Text("Golf · Cliffside").tag("golf"); Text("Tennis Rally").tag("tennis") }
                        Picker("Player",selection:$session.playerIndex) {
                            ForEach(session.players.indices,id:\.self) { i in Text(session.players[i].name).tag(i) }
                        }
                    }
                    SportsPlayerEditor(session:session)
                    Section("Controls") {
                        Toggle("Use touch controls",isOn:$session.touch)
                        Text("Physical tennis steering uses the rear camera. Keep it uncovered, face the display and leave clear space around you. Swing motion is excluded from steering.")
                        Slider(value:$session.travel,in:0.15...0.75) { Text("Steering travel") }
                        Text("Side travel: \(Int(session.travel*100)) cm")
                        Toggle("Sound",isOn:$session.sound)
                        Toggle("Haptics",isOn:$session.haptics)
                    }
                    Section {
                        Button("Play on external display") { session.start() }.disabled(!session.displayConnected).accessibilityIdentifier("startExternalGame")
                        Button("On-phone touch preview") { session.start(preview:true) }.accessibilityIdentifier("startUnityPreview")
                    }
                }
            }
            .navigationTitle("Sports Arcade")
            .background(DisplayRegistration().frame(width:0,height:0))
            .onChange(of:scenePhase) { _,phase in
                if phase != .active { session.pause() }
                SportsRuntime.shared().setForeground(phase == .active)
            }
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
        Section("\(session.sport.capitalized) controller") {
            Text("Motion: \(session.phase)")
            Text(session.feedback)
            if session.sport == "tennis" { ProgressView("Stamina",value:session.stamina) }
            Button("Calibrate neutral position") { session.calibrate() }.disabled(session.touch)
            Button(session.paused ? "Resume" : "Pause") { if session.paused { session.resume() } else { session.pause() } }.disabled(!session.ready)
            if session.sport == "golf" {
                HStack { Button("Previous club") { session.command("club",value:-1) }; Spacer(); Button("Next club") { session.command("club",value:1) } }
            } else { Button("New ball") { session.command("refeed") } }
            Slider(value:$aim,in:-1...1) { Text("Aim") }.onChange(of:aim) { _,value in session.setAim(value) }
        }
        if session.touch {
            Section("Touch fallback") {
                if session.sport == "tennis" {
                    Slider(value:$steering,in:-1...1) { Text("Court position") }.onChange(of:steering) { _,value in session.steer(value) }
                }
                Slider(value:$power,in:0.1...1) { Text("Swing power") }
                Button("Swing") { session.swing(power) }.disabled(session.paused)
            }
        }
        Section("Session") {
            if session.paused {
                Toggle("Sound",isOn:$session.sound).onChange(of:session.sound) { _,v in session.command("sound",value:v ? 1 : 0) }
                Toggle("Haptics",isOn:$session.haptics).onChange(of:session.haptics) { _,v in session.command("haptics",value:v ? 1 : 0) }
            }
            Button("Switch to touch controls") { session.useTouch() }.disabled(session.touch)
            Button("Restart") { session.end(); session.start() }
            Button("Return to menu",role:.destructive) { session.end() }
        }
    }
}
