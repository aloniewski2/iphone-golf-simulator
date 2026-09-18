import Combine
import SceneKit
import SwiftUI
import UIKit

/// The phone owns gameplay and tracking. External windows only observe these existing objects.
@MainActor
final class TVGameSession {
    let round: CourseRound
    let scene: CourseScene
    let camera: CameraSwingController
    let usesCamera: Bool
    init(round: CourseRound, scene: CourseScene, camera: CameraSwingController, usesCamera: Bool) {
        self.round = round; self.scene = scene; self.camera = camera
        self.usesCamera = usesCamera
    }
}

@MainActor
final class GolfTVDisplay: ObservableObject {
    static let shared = GolfTVDisplay()
    @Published var enabled: Bool {
        didSet {
            defaults.set(enabled, forKey: "display.landscapeTV")
            refreshWindows()
            if enabled && !oldValue { requestSetup() }
        }
    }
    @Published private(set) var connected = false
    @Published private(set) var session: TVGameSession?
    @Published private(set) var setupRequestID = 0
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
        enabled = defaults.bool(forKey: "display.landscapeTV")
    }

    func present(round: CourseRound, scene: CourseScene, camera: CameraSwingController, usesCamera: Bool = true) {
        guard session?.round !== round || session?.usesCamera != usesCamera else { return }
        session = TVGameSession(round: round, scene: scene, camera: camera, usesCamera: usesCamera)
    }

    /// The phone consumes this request between shots; never alter an in-flight result here.
    func requestSetup() { setupRequestID += 1 }

    func end(round: CourseRound) {
        guard session?.round === round else { return }
        session = nil
    }

    func connect(_ scene: UIWindowScene) {
        let isNew = scenes[scene.session.persistentIdentifier] == nil
        scenes[scene.session.persistentIdentifier] = scene
        connected = true
        refreshWindows()
        if enabled && isNew { requestSetup() }
    }

    func disconnect(_ scene: UIScene) {
        let id = scene.session.persistentIdentifier
        windows.removeValue(forKey: id)?.isHidden = true
        scenes.removeValue(forKey: id)
        connected = !scenes.isEmpty
        // Keep the round, shot and fixed camera calibration intact.
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
        let config = UISceneConfiguration(name: "Golf TV", sessionRole: .windowExternalDisplayNonInteractive)
        config.sceneClass = UIWindowScene.self
        config.delegateClass = GolfExternalSceneDelegate.self
        return config
    }
}

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
        if role == .windowExternalDisplayNonInteractive { return GolfTVDisplay.sceneConfiguration() }
        return UISceneConfiguration(name: nil, sessionRole: role)
    }

    func application(_ application: UIApplication, supportedInterfaceOrientationsFor window: UIWindow?) -> UIInterfaceOrientationMask {
        if window?.windowScene?.session.role == .windowExternalDisplayNonInteractive { return .landscape }
        return GolfTVDisplay.shared.enabled ? .portrait : .allButUpsideDown
    }
}

@MainActor
final class GolfExternalSceneDelegate: NSObject, UIWindowSceneDelegate {
    func scene(_ scene: UIScene, willConnectTo session: UISceneSession, options: UIScene.ConnectionOptions) {
        guard let windowScene = scene as? UIWindowScene else { return }
        GolfTVDisplay.shared.connect(windowScene)
    }
    func sceneDidDisconnect(_ scene: UIScene) { GolfTVDisplay.shared.disconnect(scene) }
}

private final class TVHostingController: UIHostingController<GolfTVRoot> {
    override var supportedInterfaceOrientations: UIInterfaceOrientationMask { .landscape }
    override var prefersStatusBarHidden: Bool { true }
}

/// iOS 27 requires explicit scene-accessory registration; iOS 17–26 use the app delegate.
struct TVAccessoryRegistration: UIViewControllerRepresentable {
    @ObservedObject var display = GolfTVDisplay.shared
    func makeUIViewController(context: Context) -> RegistrationController { RegistrationController() }
    func updateUIViewController(_ controller: RegistrationController, context: Context) { controller.setEnabled(display.enabled) }

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
                    registration = registerSceneAccessory(.externalNonInteractive(sceneConfiguration: GolfTVDisplay.sceneConfiguration()))
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
                    Text("Connect using iPhone Control Center → Screen Mirroring, then select your Apple TV or compatible TV. A USB-C display connection also works. Keep the phone upright with your body and feet visible.")
                    Text("TV mode gives the display its own wide game view. Turn it off to compare ordinary mirroring. You may need to reconnect Screen Mirroring when switching modes on your receiver.")
                    if display.session?.usesCamera == true {
                        Button("Reposition player and ball") { display.requestSetup(); dismiss() }
                            .accessibilityIdentifier("tvReposition")
                    }
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
                TVCourseView(round: session.round, scene: session.scene, camera: session.camera, usesCamera: session.usesCamera)
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

private struct TVCourseView: View {
    @ObservedObject var round: CourseRound
    let scene: CourseScene
    @ObservedObject var camera: CameraSwingController
    let usesCamera: Bool
    private var showsSetup: Bool {
        usesCamera && (round.phase == .ready || round.phase == .charging) &&
            camera.needsPositionSetup
    }
    var body: some View {
        ZStack {
            // Avoid rendering a second 3D course behind the full-screen setup.
            if !showsSetup { TVSceneView(scene: scene).ignoresSafeArea() }
            if usesCamera {
                GeometryReader { proxy in
                    TVCameraSetupView(camera: camera, expanded: showsSetup)
                        .frame(width: showsSetup ? proxy.size.width : proxy.size.width * 0.34,
                               height: showsSetup ? proxy.size.height : proxy.size.height * 0.56)
                        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .bottomLeading)
                }
            }
            if !showsSetup {
            VStack {
                HStack(alignment: .top) {
                    VStack(alignment: .leading, spacing: 12) {
                        Text("HOLE \(round.hole.number) · PAR \(round.hole.par) · \(round.strokes) SHOTS")
                            .font(.system(size: 26, weight: .heavy, design: .rounded))
                        Text("\(Int(round.distanceToPin.rounded())) YD TO HOLE")
                            .font(.system(size: 34, weight: .black, design: .rounded)).foregroundStyle(.yellow)
                        if round.canSwing { Text(round.holeNavigation.directionLabel).font(.title2.bold()) }
                    }.padding(20).background(.black.opacity(0.72), in: RoundedRectangle(cornerRadius: 20))
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
                    .padding(.horizontal, 30).padding(.vertical, 16)
                    .background(.black.opacity(0.75), in: Capsule())
            }.padding(36).foregroundStyle(.white)
            }
        }
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
            if usesCamera {
                if camera.hasPlayableTracking {
                    if round.automaticAim { return "Aim set · follow the recommended route · take your swing" }
                    return round.usesBodyAim
                        ? "Turn your stance to aim · \(String(format: "%+.0f°", round.combinedAim)) · swing when ready"
                        : "Manual aim · swing when ready (resume body aim on iPhone)"
                }
                return camera.isPositionLocked ? "Ball stays locked · bring your hands back into view" : camera.readiness.title
            }
            return "Ready · use your iPhone controls"
        }
    }
}

/// One camera session and the exact same frozen image-space ball used by the phone/solver.
/// The portrait image is letterboxed, never stretched to fit the landscape display.
struct TVCameraSetupView: View {
    @ObservedObject var camera: CameraSwingController
    var expanded = true
    var body: some View {
        GeometryReader { proxy in
            HStack(spacing: 28) {
                let width = min(proxy.size.width * (expanded ? 0.58 : 1), proxy.size.height * camera.tracker.frameAspect)
                let size = CGSize(width: width, height: width / max(0.3, camera.tracker.frameAspect))
                let focus = CameraBallFocus(ball: camera.displayAddress?.ball, size: size,
                    active: expanded && (2...4).contains(camera.reviewSecondsRemaining))
                ZStack {
                    CameraPreview(session: camera.tracker.session,
                        videoRotationAngle: camera.tracker.videoRotationAngle,
                        isVideoMirrored: camera.tracker.isVideoMirrored, videoGravity: .resizeAspect)
                    PoseSkeletonView(frame: camera.frame, frameAspect: camera.tracker.frameAspect, contentMode: .fit)
                        .opacity(camera.isPositionLocked ? 0.15 : 0.65)
                    if let address = camera.displayAddress {
                        CameraClubOverlay(frame: camera.frame, address: address,
                            frameAspect: camera.tracker.frameAspect, swingAngle: camera.swingAngle,
                            handedness: camera.handedness, linedUp: camera.hasPlayableTracking,
                            strike: camera.lastStrike, strikeOffset: camera.lastStrikeOffset,
                            virtualClub: camera.virtualClub, positionLocked: camera.isPositionLocked, scale: expanded ? 2.2 : 1)
                    }
                }
                .frame(width: size.width, height: size.height)
                .scaleEffect(focus.scale).offset(focus.offset)
                .frame(width: size.width, height: size.height).clipped()
                .animation(.easeInOut(duration: 0.4), value: focus)
                if expanded {
                    VStack(alignment: .leading, spacing: 22) {
                        Text(camera.reviewSecondsRemaining > 0 ? "FIND YOUR BALL" : "GET IN POSITION")
                            .font(.system(size: 32, weight: .black, design: .rounded)).foregroundStyle(.mint)
                        Text(title).font(.system(size: 27, weight: .bold))
                            .accessibilityIdentifier("tvSetupStatus")
                        Text(detail).font(.system(size: 23))
                        ProgressView(value: camera.readyProgress).tint(.mint)
                        Text(CameraPlayerStance(handedness: camera.handedness).instruction).font(.title3)
                        Text("\(camera.poseUpdatesPerSecond) pose updates/s · \(camera.tracker.detectedBodyCount) people visible")
                            .font(.title3.monospacedDigit()).foregroundStyle(.yellow)
                        Text("Need to move the phone or ball? On iPhone: Recenter grip.\nFloor height wrong? Use Lower ball / Raise ball.")
                            .font(.callout).foregroundStyle(.white.opacity(0.8))
                    }.frame(maxWidth: .infinity, alignment: .leading).padding(.trailing, 24)
                }
            }.frame(maxWidth: .infinity, maxHeight: .infinity)
        }
        .background(.black).foregroundStyle(.white)
        .accessibilityIdentifier("tvCameraSetup")
    }
    private var title: String {
        if case .denied = camera.status { return "Camera permission needed" }
        if case .unavailable = camera.status { return "Camera unavailable" }
        if camera.trackingIsStale { return "Waiting for fresh camera tracking" }
        if camera.reviewSecondsRemaining > 0 { return "Ball locked · wait \(camera.reviewSecondsRemaining)" }
        return camera.readiness.title
    }
    private var detail: String {
        if case .denied = camera.status { return "Enable Camera for Golf Arcade in iPhone Settings." }
        if case .unavailable(let reason) = camera.status { return reason }
        if camera.trackingIsStale { return "Do not swing yet. Keep Golf Arcade open on the unlocked phone. Your ball stays fixed." }
        if camera.reviewSecondsRemaining > 0 {
            return "Find the ball and floor ring beside your feet in this camera view. Hold your grip while the countdown finishes; then swing through that spot."
        }
        return "Step back until both feet and your fully extended arms fit in view, with space above your hands. Then lower your hands and hold your grip."
    }
}

/// A second renderer of the same scene. It never starts/stops tracking, advances a round,
/// changes scene inputs, or takes ownership of the phone's animation loop.
private struct TVSceneView: UIViewRepresentable {
    let scene: CourseScene
    func makeUIView(context: Context) -> SCNView {
        let view = SCNView()
        view.scene = scene.scene
        view.pointOfView = scene.camera
        view.isUserInteractionEnabled = false
        view.preferredFramesPerSecond = 60
        view.antialiasingMode = .multisampling2X
        view.rendersContinuously = true
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
