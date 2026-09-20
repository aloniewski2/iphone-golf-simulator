import SwiftUI
import AVFoundation

struct MainMenuView: View {
    @ObservedObject var flow: GameFlow
    @State private var focus = 0
    @State private var settingsPresented = false
    @Environment(\.scenePhase) private var scenePhase
    @Environment(\.accessibilityReduceMotion) private var reduceMotion

    private enum Item: Int, CaseIterable { case solo, multiplayer, practice, settings }

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 22) {
                VStack(alignment: .leading, spacing: 6) {
                    Text("GOLF ARCADE")
                        .font(.system(size: 12, weight: .black, design: .rounded)).tracking(2)
                        .foregroundStyle(.mint)
                    Text("Your phone. Your swing.").font(.system(size: 34, weight: .black, design: .rounded))
                }
                if !reduceMotion {
                    Text("Background: concept film · not gameplay")
                        .font(.caption).foregroundStyle(.white.opacity(0.8))
                        .accessibilityIdentifier("conceptFilmNotice")
                }
                VStack(spacing: 12) {
                    row(.solo, icon: "person.fill", title: "Solo", detail: soloDetail)
                        .accessibilityIdentifier("menuSolo")
                    row(.multiplayer, icon: "person.3.fill", title: "Multiplayer", detail: "2–4 players take turns on one phone")
                        .accessibilityIdentifier("menuMultiplayer")
                    row(.practice, icon: "waveform.path.ecg", title: "Practice", detail: "Try phone swings and touch shots without scoring")
                        .accessibilityIdentifier("menuPractice")
                    row(.settings, icon: "gearshape.fill", title: "Settings", detail: "Controller, sound, TV / AirPlay")
                        .accessibilityIdentifier("menuSettings")
                }
            }
            .padding(24)
            .frame(maxWidth: 620)
            .frame(maxWidth: .infinity)
        }
        .background {
            GeometryReader { geometry in
                Image(uiImage:SunwardCourseArtwork.image(hole:1)).resizable().scaledToFill()
                    .frame(width:geometry.size.width,height:geometry.size.height).clipped()
                    .overlay {
                        if !reduceMotion {
                            SunwardMenuFilm(playing:scenePhase == .active && !settingsPresented)
                        }
                    }
                    .overlay(LinearGradient(colors:[Color.black.opacity(0.25),Color.black.opacity(0.80)],startPoint:.top,endPoint:.bottom))
            }.ignoresSafeArea().accessibilityHidden(true)
        }
        .accessibilityIdentifier("mainMenu")
        .sheet(isPresented: $settingsPresented) { SettingsView() }
    }

    private var soloDetail: String {
        "Choose your golfer and course"
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

/// The accepted 1080p Higgsfield course film, used only as decorative menu media.
/// Never overlays gameplay, owns an AirPlay route, or keeps decoding after navigation.
struct SunwardMenuFilm: UIViewRepresentable {
    let playing: Bool
    func makeUIView(context:Context)->FilmView { FilmView() }
    func updateUIView(_ view:FilmView,context:Context) { playing ? view.player.play() : view.player.pause() }
    static func dismantleUIView(_ view:FilmView,coordinator:()) {
        view.player.pause();view.looper?.disableLooping();view.player.removeAllItems()
    }
    final class FilmView:UIView {
        override class var layerClass:AnyClass { AVPlayerLayer.self }
        let player=AVQueuePlayer()
        var looper:AVPlayerLooper?
        override init(frame:CGRect) {
            super.init(frame:frame)
            isUserInteractionEnabled=false;isAccessibilityElement=false
            player.isMuted=true;player.allowsExternalPlayback=false
            guard let url=Bundle.main.url(forResource:"SunwardMenu",withExtension:"mp4") else { return }
            looper=AVPlayerLooper(player:player,templateItem:AVPlayerItem(url:url))
            let videoLayer=layer as! AVPlayerLayer
            videoLayer.player=player;videoLayer.videoGravity = .resizeAspectFill
        }
        required init?(coder:NSCoder) { nil }
    }
}

/// Plain bundled renders, not asset-catalog symbols. Explicit decoding also supports
/// generated course cards copied by Xcode's resources phase without an imageset.
@MainActor enum SunwardCourseArtwork {
    private static var cache:[Int:UIImage]=[:]
    static func image(hole:Int)->UIImage {
        if let image=cache[hole] { return image }
        guard let url=Bundle.main.url(forResource:"SunwardHole-\(hole)",withExtension:"png"),
              let image=UIImage(contentsOfFile:url.path) else { return UIImage() }
        cache[hole]=image
        return image
    }
}

struct SettingsView: View {
    @State private var tvSettingsPresented = false
    @AppStorage("range.swingInput") private var swingInput: SwingInput = .phone
    @AppStorage("range.soundEnabled") private var sound = true
    @AppStorage("arcade.hapticsEnabled") private var haptics = true
    @AppStorage("controller.sensitivity") private var sensitivity = 1.8
    @Environment(\.dismiss) private var dismiss

    var body: some View {
        NavigationStack {
            Form {
                Section {
                    Button("TV / AirPlay", systemImage: "airplayvideo") { tvSettingsPresented = true }
                        .accessibilityIdentifier("tvSettings")
                }
                Section {
                    Picker("Swing input", selection: $swingInput) {
                        ForEach(SwingInput.allCases) { Text($0.title).tag($0) }
                    }
                } footer: {
                    Text("Use your phone as a motion controller, or choose Touch. Connect AirPlay for a separate TV game view.")
                }
                Section("Motion controller") {
                    Text("Choose club and aim, then tap Ready once. Steady the phone until the ready vibration, then make a short, gentle swing and follow through without touching the screen. Tap Cancel swing to cancel before impact. Never use a full-force golf swing; keep a secure grip and use a wrist strap.")
                    Slider(value: $sensitivity, in: 1...3, step: 0.1) { Text("Motion sensitivity") }
                    Text("Sensitivity: \(sensitivity, specifier: "%.1f")× · higher needs less movement")
                }
                Section {
                    Toggle("Sound effects", isOn: $sound)
                    Toggle("Haptics", isOn: $haptics)
                }
            }
            .navigationTitle("Settings")
            .sheet(isPresented: $tvSettingsPresented) {
                #if NATIVE_ONLY
                NativeTVSettingsView()
                #else
                if DisplayCoordinator.usesNativeRenderer { NativeTVSettingsView() }
                else { TVSettingsView() }
                #endif
            }
            .toolbar { ToolbarItem(placement: .confirmationAction) { Button("Done") { dismiss() } } }
        }
        .preferredColorScheme(.dark)
    }
}
