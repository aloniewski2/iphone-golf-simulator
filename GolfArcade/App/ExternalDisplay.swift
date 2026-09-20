import Combine
#if !NATIVE_ONLY
import SceneKit
#endif
import SwiftUI
import UIKit

#if !NATIVE_ONLY
/// The phone owns gameplay and motion input. External windows only observe these existing objects.
@MainActor
final class TVGameSession: ObservableObject {
    @Published var playerName = ""
    @Published var controllerStatus = "Choose club and aim on your iPhone"
    let round: CourseRound
    let scene: CourseScene
    init(round: CourseRound, scene: CourseScene) {
        self.round = round; self.scene = scene
    }
}

@MainActor
final class GolfTVDisplay: ObservableObject {
    static let shared = GolfTVDisplay()
    @Published var enabled: Bool {
        didSet {
            defaults.set(enabled, forKey: "display.landscapeTV")
            refreshWindows()
        }
    }
    @Published private(set) var connected = false
    @Published private(set) var session: TVGameSession?
    var active: Bool { enabled && connected }
    @Published var delayCheck = false
    @Published private(set) var flashNumber = 0
    @Published private(set) var flashVisible = false
    private let defaults: UserDefaults
    private var scenes: [String: UIWindowScene] = [:]
    private var windows: [String: UIWindow] = [:]
    private var flashTask: Task<Void, Never>?

    init(defaults: UserDefaults = .standard) {
        self.defaults = defaults
        enabled = defaults.object(forKey: "display.landscapeTV") == nil ? true : defaults.bool(forKey: "display.landscapeTV")
    }

    func present(round: CourseRound, scene: CourseScene) {
        guard session?.round !== round || session?.scene !== scene else { return }
        session = TVGameSession(round: round, scene: scene)
    }

    func end(round: CourseRound) {
        guard session?.round === round else { return }
        session = nil
    }

    func connect(_ scene: UIWindowScene) {
        scenes[scene.session.persistentIdentifier] = scene
        connected = true
        refreshWindows()
    }

    func disconnect(_ scene: UIScene) {
        let id = scene.session.persistentIdentifier
        windows.removeValue(forKey: id)?.isHidden = true
        scenes.removeValue(forKey: id)
        connected = !scenes.isEmpty
        // Keep the current round and in-flight shot intact.
    }

    private func refreshWindows() {
        if !enabled {
            for window in windows.values { window.isHidden = true; window.rootViewController = nil }
            windows.removeAll()
            return
        }
        for (id, scene) in scenes where windows[id] == nil {
            let window = UIWindow(windowScene: scene)
            window.rootViewController = TVHostingController(rootView: GolfTVRoot(display: self))
            windows[id] = window
            window.isHidden = false // Never steal key-window input from the phone.
        }
    }

    func flash() {
        flashTask?.cancel()
        flashNumber += 1
        flashVisible = true
        flashTask = Task { @MainActor [weak self] in
            try? await Task.sleep(for: .seconds(0.8))
            guard !Task.isCancelled else { return }
            self?.flashVisible = false
        }
    }

    static func sceneConfiguration() -> UISceneConfiguration {
        GolfExternalSceneDelegate.configuration()
    }
}
#endif

@MainActor
final class GolfAppDelegate: NSObject, UIApplicationDelegate {
    func application(_ application: UIApplication, configurationForConnecting session: UISceneSession,
                     options: UIScene.ConnectionOptions) -> UISceneConfiguration {
        Self.configuration(for: session.role)
    }

    static func configuration(for role: UISceneSession.Role) -> UISceneConfiguration {
        // SwiftUI must own the application's WindowGroup. Do not return the
        // session's UIKit scene class (or reuse an external-display configuration)
        // for the phone: it can leave a live process with no hosted phone content.
        if role == .windowApplication {
            return UISceneConfiguration(name: nil, sessionRole: role)
        }
        #if compiler(>=6.4)
        if #available(iOS 27, *) { return UISceneConfiguration(name: nil, sessionRole: role) }
        #endif
        if role == .windowExternalDisplayNonInteractive { return GolfExternalSceneDelegate.configuration() }
        return UISceneConfiguration(name: nil, sessionRole: role)
    }

    func application(_ application: UIApplication, supportedInterfaceOrientationsFor window: UIWindow?) -> UIInterfaceOrientationMask {
        if window?.windowScene?.session.role == .windowExternalDisplayNonInteractive { return .landscape }
        #if NATIVE_ONLY
        let enabled = DisplayCoordinator.shared.enabled
        #else
        let enabled = DisplayCoordinator.usesNativeRenderer ? DisplayCoordinator.shared.enabled : GolfTVDisplay.shared.enabled
        #endif
        return enabled ? .portrait : .allButUpsideDown
    }
}

@MainActor
final class GolfExternalSceneDelegate: NSObject, UIWindowSceneDelegate {
    var window: UIWindow?
    static func configuration() -> UISceneConfiguration {
        let config = UISceneConfiguration(name: "Golf TV", sessionRole: .windowExternalDisplayNonInteractive)
        config.sceneClass = UIWindowScene.self
        config.delegateClass = GolfExternalSceneDelegate.self
        return config
    }
    func scene(_ scene: UIScene, willConnectTo session: UISceneSession, options: UIScene.ConnectionOptions) {
        guard let windowScene = scene as? UIWindowScene else { return }
        if DisplayCoordinator.usesNativeRenderer {
            window = DisplayCoordinator.shared.connect(windowScene)
            return
        }
        #if !NATIVE_ONLY
        GolfTVDisplay.shared.connect(windowScene)
        #endif
    }
    func sceneDidDisconnect(_ scene: UIScene) {
        if DisplayCoordinator.usesNativeRenderer { DisplayCoordinator.shared.disconnect(scene); window = nil; return }
        #if !NATIVE_ONLY
        GolfTVDisplay.shared.disconnect(scene)
        #endif
    }
}

#if !NATIVE_ONLY
private final class TVHostingController: UIHostingController<GolfTVRoot> {
    override var supportedInterfaceOrientations: UIInterfaceOrientationMask { .landscape }
    override var prefersStatusBarHidden: Bool { true }
}
#endif

/// iOS 27 requires explicit scene-accessory registration; iOS 17–26 use the app delegate.
struct TVAccessoryRegistration: UIViewControllerRepresentable {
    @State private var nativeDisplay = DisplayCoordinator.shared
    #if !NATIVE_ONLY
    @ObservedObject var display = GolfTVDisplay.shared
    #endif
    func makeUIViewController(context: Context) -> RegistrationController { RegistrationController() }
    func updateUIViewController(_ controller: RegistrationController, context: Context) {
        #if NATIVE_ONLY
        controller.setEnabled(nativeDisplay.enabled)
        #else
        controller.setEnabled(DisplayCoordinator.usesNativeRenderer ? nativeDisplay.enabled : display.enabled)
        #endif
    }

    final class RegistrationController: UIViewController {
        private var registration: AnyObject?
        private var enabled = false
        override func viewDidAppear(_ animated: Bool) {
            super.viewDidAppear(animated)
            setEnabled(enabled)
        }
        func setEnabled(_ value: Bool) {
            let changed = enabled != value
            enabled = value
            // Xcode 26 CI must also compile this file; the accessory symbols require the 27 SDK.
            #if compiler(>=6.4)
            if #available(iOS 27, *) {
                if registration == nil {
                    registration = registerSceneAccessory(.externalNonInteractive(sceneConfiguration: GolfExternalSceneDelegate.configuration()))
                }
                (registration as? UISceneAccessoryRegistration)?.isEnabled = value
            }
            #endif
            if changed || value {
                parent?.setNeedsUpdateOfSupportedInterfaceOrientations()
                if value { view.window?.windowScene?.requestGeometryUpdate(.iOS(interfaceOrientations: .portrait)) }
            }
        }
    }
}

#if !NATIVE_ONLY
struct TVSettingsView: View {
    @ObservedObject var display = GolfTVDisplay.shared
    @Environment(\.dismiss) private var dismiss
    var body: some View {
        NavigationStack {
            Form {
                Section("Display") {
                    Toggle("Landscape TV mode", isOn: $display.enabled).accessibilityIdentifier("tvMode")
                    Text(display.enabled ? (display.connected ? "TV connected · landscape game view" : "Ready for an external display") : "Standard screen mirroring")
                        .accessibilityIdentifier("tvConnectionStatus")
                    Text("Connect using iPhone Control Center → Screen Mirroring, then select your Apple TV or compatible TV. A USB-C display connection also works. Keep Golf Arcade open. The phone becomes your controller; the TV shows the course.")
                    Text("TV mode gives the display its own wide game view. Turn it off to compare ordinary mirroring. You may need to reconnect Screen Mirroring when switching modes on your receiver.")
                }
                Section("Compare display delay") {
                    Toggle("Show comparison flash", isOn: $display.delayCheck)
                    if display.delayCheck {
                        DisplayFlash(display: display)
                        Button("Flash both screens") { display.flash() }.accessibilityIdentifier("tvFlash")
                    }
                    Text("Record the phone and TV together with another camera, then count frames between matching flashes. Frame difference ÷ recording fps × 1000 gives display delay in ms. This measures the extra TV delay; swing-to-screen delay needs a recording that includes your movement too.")
                }
            }
            .navigationTitle("TV / AirPlay")
            .toolbar { ToolbarItem(placement: .confirmationAction) { Button("Done") { dismiss() } } }
        }
    }
}

struct DisplayFlash: View {
    @ObservedObject var display: GolfTVDisplay
    var body: some View {
        Text("FLASH \(display.flashNumber)")
            .font(.system(size: 28, weight: .black, design: .monospaced))
            .foregroundStyle(display.flashVisible ? .black : .white)
            .frame(maxWidth: .infinity).padding(16)
            .background(display.flashVisible ? Color.white : Color.black)
            .overlay(Rectangle().stroke(.white, lineWidth: 2))
            .accessibilityIdentifier("displayComparisonFlash")
    }
}

private struct GolfTVRoot: View {
    @ObservedObject var display: GolfTVDisplay
    var body: some View {
        ZStack {
            Color.black.ignoresSafeArea()
            if let session = display.session {
                TVCourseView(session: session, round: session.round, scene: session.scene)
            } else {
                VStack(spacing: 24) {
                    Image(systemName: "figure.golf").font(.system(size: 90))
                    Text("GOLF ARCADE").font(.system(size: 48, weight: .black, design: .rounded))
                    Text("Choose a game and set up your swing on your iPhone.").font(.title)
                }.foregroundStyle(.white)
            }
            if display.delayCheck {
                DisplayFlash(display: display).frame(width: 340)
                    .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .bottom)
                    .padding(36)
            }
        }.preferredColorScheme(.dark)
    }
}

struct TVCourseView: View {
    @ObservedObject var session: TVGameSession
    @ObservedObject var round: CourseRound
    let scene: CourseScene
    var body: some View {
        GeometryReader { proxy in
            ZStack {
                TVSceneView(scene: scene).ignoresSafeArea()
                hud.frame(width: 1280, height: 720)
                    .scaleEffect(min(proxy.size.width / 1280, proxy.size.height / 720))
                    .frame(width: proxy.size.width, height: proxy.size.height)
            }
        }
    }

    private var hud: some View {
            VStack {
                HStack(alignment: .top) {
                    VStack(alignment: .leading, spacing: 12) {
                        Text(session.playerName.uppercased())
                            .font(.system(size: 20, weight: .bold, design: .rounded))
                            .lineLimit(1).minimumScaleFactor(0.6)
                        Text("HOLE \(round.hole.number) · PAR \(round.hole.par) · \(round.strokes) SHOTS")
                            .font(.system(size: 26, weight: .heavy, design: .rounded))
                        Text("\(Int(round.distanceToPin.rounded())) YD TO HOLE")
                            .font(.system(size: 34, weight: .black, design: .rounded)).foregroundStyle(.yellow)
                        if round.canSwing { Text(round.holeNavigation.directionLabel).font(.title2.bold()) }
                    }.frame(maxWidth: 540, alignment: .leading)
                        .padding(20).background(.black.opacity(0.72), in: RoundedRectangle(cornerRadius: 20))
                    Spacer()
                    VStack(alignment: .trailing, spacing: 10) {
                        Text(round.club.displayName.uppercased()).font(.title.bold())
                        Text("\(round.targetLabel) · \(Int(round.distanceToTarget.rounded())) YD").font(.title2)
                        if let read = round.greenRead { Text(read.label).font(.title3).foregroundStyle(.mint) }
                        if round.canSwing { ShotStrengthGuide(round: round).frame(width: 260) }
                    }.padding(20).background(.black.opacity(0.72), in: RoundedRectangle(cornerRadius: 20))
                }
                Spacer()
                Text(status).font(.system(size: 28, weight: .bold, design: .rounded))
                    .lineLimit(2).multilineTextAlignment(.center).frame(maxWidth: 900)
                    .padding(.horizontal, 30).padding(.vertical, 16)
                    .background(.black.opacity(0.75), in: Capsule())
            }.padding(36).foregroundStyle(.white)
    }
    private var status: String {
        if round.pausedAt != nil { return "Paused · return to Golf Arcade on your iPhone" }
        switch round.phase {
        case .complete: return "Round complete · continue on your iPhone"
        case .holed: return round.pickedUp ? "Stroke limit · next hole" : "Holed!"
        case .landed: return "\(Int(round.activeShot?.total.rounded() ?? 0)) YD · next shot shortly"
        case .flying: return round.isReplay ? "Replay" : "Ball in play"
        case .charging: return "\(Int(round.power * 100))% · swing through"
        case .ready:
            return session.controllerStatus
        }
    }
}

/// A second renderer of the same scene. It never starts/stops tracking, advances a round,
/// changes scene inputs, or takes ownership of the phone's animation loop.
private struct TVSceneView: UIViewRepresentable {
    let scene: CourseScene
    func makeUIView(context: Context) -> SCNView {
        let view = CourseRenderView()
        view.renderPolicy = .television
        view.scene = scene.scene
        view.pointOfView = scene.camera
        view.isUserInteractionEnabled = false
        view.rendersContinuously = true
        view.delegate = scene.tvRenderAudit
        return view
    }
    func updateUIView(_ view: SCNView, context: Context) {
        if view.scene !== scene.scene {
            view.scene = scene.scene
            view.pointOfView = scene.camera
        }
    }
    static func dismantleUIView(_ view: SCNView, coordinator: ()) {
        view.rendersContinuously = false
        view.scene = nil
    }
}
#endif
