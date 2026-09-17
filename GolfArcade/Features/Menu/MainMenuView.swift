import SwiftUI

struct MainMenuView: View {
    @ObservedObject var flow: GameFlow
    @ObservedObject var camera: CameraSwingController
    @AppStorage("gestures.enabled") private var gesturesEnabled = true
    @State private var focus = 0
    @State private var settingsPresented = false

    private enum Item: Int, CaseIterable { case solo, multiplayer, practice, settings }

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 22) {
                VStack(alignment: .leading, spacing: 6) {
                    Text("GOLF ARCADE")
                        .font(.system(size: 12, weight: .black, design: .rounded)).tracking(2)
                        .foregroundStyle(.mint)
                    Text("Your body is the club.").font(.system(size: 34, weight: .black, design: .rounded))
                }
                VStack(spacing: 12) {
                    row(.solo, icon: "person.fill", title: "Solo", detail: soloDetail)
                        .accessibilityIdentifier("menuSolo")
                    row(.multiplayer, icon: "person.3.fill", title: "Multiplayer", detail: "2–4 players take turns on one phone")
                        .accessibilityIdentifier("menuMultiplayer")
                    row(.practice, icon: "waveform.path.ecg", title: "Practice Lab", detail: "Measure swings, test control, replay pose traces")
                        .accessibilityIdentifier("menuPractice")
                    row(.settings, icon: "gearshape.fill", title: "Settings", detail: "Swing input, sound, gestures")
                        .accessibilityIdentifier("menuSettings")
                }
                if gesturesEnabled { GestureStatusChip(camera: camera) }
            }
            .padding(24)
            .frame(maxWidth: 620)
            .frame(maxWidth: .infinity)
        }
        .background(Palette.background.ignoresSafeArea())
        .accessibilityIdentifier("mainMenu")
        .sheet(isPresented: $settingsPresented) { SettingsView() }
        .onNavGesture(camera) { gesture in
            guard !settingsPresented else { return }
            switch gesture {
            case .up, .left: focus = (focus + Item.allCases.count - 1) % Item.allCases.count
            case .down, .right: focus = (focus + 1) % Item.allCases.count
            case .select: activate(Item(rawValue: focus) ?? .solo)
            }
        }
    }

    private var soloDetail: String {
        flow.roster.first?.isScanned == true ? "Play as \(flow.roster[0].name)" : "Scan your body, then pick a course"
    }

    private func row(_ item: Item, icon: String, title: String, detail: String) -> some View {
        MenuRow(focused: focus == item.rawValue, action: { activate(item) }) {
            HStack(spacing: 14) {
                Image(systemName: icon).font(.title2.bold())
                    .frame(width: 46, height: 46)
                    .background(.mint.opacity(0.18), in: Circle())
                    .foregroundStyle(.mint)
                VStack(alignment: .leading, spacing: 3) {
                    Text(title).font(.title3.bold())
                    Text(detail).font(.caption).foregroundStyle(.white.opacity(0.62))
                }
                Spacer()
                Image(systemName: "chevron.right").foregroundStyle(.white.opacity(0.4))
            }
        }
    }

    private func activate(_ item: Item) {
        focus = item.rawValue
        switch item {
        case .solo: flow.choose(.solo)
        case .multiplayer: flow.choose(.multiplayer)
        case .practice: flow.screen = .practice
        case .settings: settingsPresented = true
        }
    }
}

struct SettingsView: View {
    @AppStorage("range.swingInput") private var swingInput: SwingInput = .camera
    @AppStorage("range.soundEnabled") private var sound = true
    @AppStorage("arcade.hapticsEnabled") private var haptics = true
    @AppStorage("gestures.enabled") private var gesturesEnabled = true
    @Environment(\.dismiss) private var dismiss

    var body: some View {
        NavigationStack {
            Form {
                Section {
                    Picker("Swing input", selection: $swingInput) {
                        ForEach(SwingInput.allCases) { Text($0.title).tag($0) }
                    }
                } footer: {
                    Text("Camera tracks your body and uses the virtual ball. Touch and Phone are for trying the game without a camera setup.")
                }
                Section {
                    Toggle("Gesture controls", isOn: $gesturesEnabled)
                } footer: {
                    Text("Hold a fist away from your other hand, then swipe to move and punch toward the camera to select. From far away, raise your hand to shoulder height instead of making a fist.")
                }
                Section {
                    Toggle("Sound effects", isOn: $sound)
                    Toggle("Haptics", isOn: $haptics)
                }
            }
            .navigationTitle("Settings")
            .toolbar { ToolbarItem(placement: .confirmationAction) { Button("Done") { dismiss() } } }
        }
        .preferredColorScheme(.dark)
    }
}
