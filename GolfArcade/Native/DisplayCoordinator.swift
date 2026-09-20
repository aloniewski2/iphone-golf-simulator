import Observation
import SwiftUI
import UIKit

@MainActor @Observable
final class DisplayCoordinator {
    static let shared = DisplayCoordinator()
    var enabled: Bool {
        didSet { defaults.set(enabled, forKey: "display.landscapeTV"); route() }
    }
    private(set) var connected = false
    var delayCheck = false
    private(set) var flashNumber = 0
    private(set) var flashVisible = false
    func flash() {
        flashNumber += 1; flashVisible = true
        let number = flashNumber
        Task { [weak self] in
            try? await Task.sleep(for: .milliseconds(500))
            guard let self, flashNumber == number else { return }
            flashVisible = false
        }
    }
    private(set) var destination: Destination = .phone
    enum Destination: Equatable { case phone, external(String) }
    @ObservationIgnored private var session: GameSession?
    @ObservationIgnored private weak var phoneHost: UIViewController?
    @ObservationIgnored private var externalHosts: [String: UIViewController] = [:]
    @ObservationIgnored private var windows: [String: UIWindow] = [:]
    @ObservationIgnored private let defaults: UserDefaults

    init(defaults: UserDefaults = .standard) {
        self.defaults = defaults
        enabled = defaults.object(forKey: "display.landscapeTV") == nil ? true : defaults.bool(forKey: "display.landscapeTV")
    }

    /// Shipping Release excludes the comparison renderer. Debug/Comparison builds
    /// retain the opt-in native flag for the paired development regression suites.
    static var usesNativeRenderer: Bool {
        #if NATIVE_ONLY
        true
        #else
        ProcessInfo.processInfo.arguments.contains("-realityKit")
        #endif
    }

    func present(_ session: GameSession) {
        if self.session !== session { self.session?.cancelUnfinishedSwing() }
        self.session = session
        route()
    }

    func end(_ session: GameSession) {
        guard self.session === session else { return }
        detach(session.viewport)
        self.session = nil
    }

    func setPhoneHost(_ host: UIViewController) { phoneHost = host; route() }
    func removePhoneHost(_ host: UIViewController) {
        guard phoneHost === host else { return }
        if let session, session.viewport.parent === host { detach(session.viewport) }
        phoneHost = nil
    }

    @discardableResult
    func connect(_ scene: UIWindowScene) -> UIWindow {
        let id = scene.session.persistentIdentifier
        if let window = windows[id] { return window }
        let host = NativeTVHost()
        let window = UIWindow(windowScene: scene)
        window.rootViewController = host
        windows[id] = window; externalHosts[id] = host
        connected = true
        window.isHidden = false
        route()
        return window
    }

    func disconnect(_ scene: UIScene) {
        let id = scene.session.persistentIdentifier
        externalHosts.removeValue(forKey: id)
        let oldWindow = windows.removeValue(forKey: id)
        connected = !windows.isEmpty
        route() // Reparent the same view before disposing of the old window.
        oldWindow?.isHidden = true
        oldWindow?.rootViewController = nil
    }

    /// Separated for lifecycle tests without manufacturing UIWindowScene instances.
    #if DEBUG
    func attachForTesting(session: GameSession, phone: UIViewController, external: UIViewController?) {
        self.session = session; phoneHost = phone
        externalHosts = external.map { ["test": $0] } ?? [:]
        connected = external != nil
        route()
    }
    #endif

    private func route() {
        let externalID = enabled ? externalHosts.keys.sorted().first : nil
        let next: Destination = externalID.map(Destination.external) ?? .phone
        let host = externalID.flatMap { externalHosts[$0] } ?? phoneHost
        if destination != next { session?.cancelUnfinishedSwing(); destination = next }
        for (id, window) in windows { window.isHidden = !enabled || id != externalID }
        guard let session, let host else { return }
        let viewport = session.viewport
        guard viewport.parent !== host else { return }
        detach(viewport)
        host.addChild(viewport)
        host.view.addSubview(viewport.view)
        viewport.view.translatesAutoresizingMaskIntoConstraints = false
        NSLayoutConstraint.activate([
            viewport.view.leadingAnchor.constraint(equalTo: host.view.leadingAnchor),
            viewport.view.trailingAnchor.constraint(equalTo: host.view.trailingAnchor),
            viewport.view.topAnchor.constraint(equalTo: host.view.topAnchor),
            viewport.view.bottomAnchor.constraint(equalTo: host.view.bottomAnchor)
        ])
        viewport.didMove(toParent: host)
        (host as? NativeTVHost)?.bringFlashToFront()
    }

    private func detach(_ controller: UIViewController) {
        guard controller.parent != nil else { return }
        controller.willMove(toParent: nil)
        controller.view.removeFromSuperview()
        controller.removeFromParent()
    }
}

private final class NativeTVHost: UIViewController {
    private let flash = UIHostingController(rootView: NativeDisplayFlash())
    override var supportedInterfaceOrientations: UIInterfaceOrientationMask { .landscape }
    override var prefersStatusBarHidden: Bool { true }
    override func viewDidLoad() {
        super.viewDidLoad()
        view.backgroundColor = .black
        let label = UILabel()
        label.text = "Golf Arcade · Choose a course on your iPhone"
        label.textColor = .white; label.textAlignment = .center
        label.frame = view.bounds; label.autoresizingMask = [.flexibleWidth, .flexibleHeight]
        view.addSubview(label)
        addChild(flash)
        flash.view.backgroundColor = .clear
        flash.view.isUserInteractionEnabled = false
        flash.view.frame = view.bounds
        flash.view.autoresizingMask = [.flexibleWidth, .flexibleHeight]
        view.addSubview(flash.view)
        flash.didMove(toParent: self)
    }
    func bringFlashToFront() { view.bringSubviewToFront(flash.view) }
}

struct NativeDisplayFlash: View {
    @State private var display = DisplayCoordinator.shared
    var body: some View {
        if display.delayCheck {
            Text("FLASH \(display.flashNumber)")
                .font(.system(size: 28, weight: .black, design: .monospaced))
                .foregroundStyle(display.flashVisible ? .black : .white)
                .frame(maxWidth: .infinity).padding(16)
                .background(display.flashVisible ? Color.white : Color.black)
                .accessibilityIdentifier("nativeDisplayFlash")
                .frame(maxHeight: .infinity, alignment: .top)
        }
    }
}

struct NativeTVSettingsView: View {
    @State private var display = DisplayCoordinator.shared
    @Environment(\.dismiss) private var dismiss
    var body: some View {
        NavigationStack {
            Form {
                Section("Display") {
                    Toggle("Landscape TV mode", isOn: $display.enabled).accessibilityIdentifier("tvMode")
                    Text(display.connected ? "External display connected" : "Connect a display using AirPlay or a wired adapter.")
                    Text("The TV shows the same live game viewport; the phone keeps its controls. Switching displays preserves an accepted shot and cancels an unfinished swing.")
                }
                Section("Compare display delay") {
                    Toggle("Show comparison flash", isOn: $display.delayCheck)
                    if display.delayCheck {
                        NativeDisplayFlash().frame(height: 80)
                        Button("Flash both screens") { display.flash() }.accessibilityIdentifier("tvFlash")
                    }
                    Text("Record the phone and TV together with another camera. Frame difference ÷ recording fps × 1000 gives TV delay in milliseconds. This does not measure sensor-to-impact latency.")
                }
            }.navigationTitle("TV / AirPlay")
                .toolbar { ToolbarItem(placement: .confirmationAction) { Button("Done") { dismiss() } } }
        }
    }
}

struct NativeViewportHost: UIViewControllerRepresentable {
    func makeUIViewController(context: Context) -> UIViewController {
        let host = UIViewController()
        DisplayCoordinator.shared.setPhoneHost(host)
        return host
    }
    func updateUIViewController(_ controller: UIViewController, context: Context) {}
    static func dismantleUIViewController(_ controller: UIViewController, coordinator: ()) {
        DisplayCoordinator.shared.removePhoneHost(controller)
    }
}
