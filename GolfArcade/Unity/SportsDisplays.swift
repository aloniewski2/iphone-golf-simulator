import SwiftUI

@MainActor
final class SportsDisplays: NSObject {
    static let shared = SportsDisplays()
    weak var phone: UIWindow?
    var external: UIWindow?
    private var preview: UIWindow?
    private var multiplayerRenderer: UIWindow?
    private var loadingCover: UIWindow?
    private var previewOverlay: UIViewController?
    private var registration: AnyObject?
    private override init() {
        super.init()
        NotificationCenter.default.addObserver(self,selector:#selector(refreshNotification),name:UIApplication.didBecomeActiveNotification,object:nil)
        NotificationCenter.default.addObserver(self,selector:#selector(refreshNotification),name:UIScene.didActivateNotification,object:nil)
    }
    @objc private func refreshNotification(_ notification:Notification) { refresh() }
    func refresh() {
        let scenes=UIApplication.shared.connectedScenes.compactMap { $0 as? UIWindowScene }
        if phone == nil {
            phone=scenes.first(where: { $0.session.role == .windowApplication })?.windows.first(where: { $0.isKeyWindow })
        }
        if external == nil, let scene=scenes.first(where: { $0.session.role == .windowExternalDisplayNonInteractive && $0.activationState != .unattached }) {
            let window=UIWindow(windowScene:scene)
            window.rootViewController=Self.tvRoot(); window.isHidden=false; external=window
        }
        SportsSession.shared.displayConnected = external != nil
        if let screen = external?.windowScene?.screen {
            // AirPlay to a Mac arrives like any other screen, usually 16:10 (1440×900,
            // 1512×982…) or the size of the receiver's window; the TV canvas fits either.
            NSLog("[SportsDisplay] external %@ bounds=%.0fx%.0f scale=%.1f native=%.0fx%.0f kind=%@",
                  SportsTiming.currentTV(), screen.bounds.width, screen.bounds.height, screen.scale,
                  screen.nativeBounds.width, screen.nativeBounds.height, Self.displayKind.rawValue)
        }
        NSLog("[SportsDisplay] refresh scenes=%@ external=%d",scenes.map { $0.session.role.rawValue }.joined(separator:","),external != nil ? 1 : 0)
    }
    func register(from controller:UIViewController) {
        guard let window=controller.view.window else { return }
        phone=window
        if #available(iOS 27.0, *), registration == nil {
            let config=UISceneConfiguration(name:"Sports External",sessionRole:.windowExternalDisplayNonInteractive)
            config.delegateClass=SportsExternalScene.self
            var owner=controller
            while let parent=owner.parent { owner=parent }
            registration=owner.registerSceneAccessory(.externalNonInteractive(sceneConfiguration:config))
        }
        refresh()
    }
    func gameWindow(preview usePreview:Bool) -> UIWindow? {
        refresh()
        guard let external else { return nil }
        showMenu()
        return external
    }
    /// Golf guests watch the host's screen. Keep their Unity replica underneath
    /// the opaque native controller so it can supply map and turn state.
    func multiplayerControllerWindow() -> UIWindow? {
        guard SportsSession.shared.multiplayerControllerOnly else { return nil }
        refresh()
        if let external { return external }
        guard let scene=phone?.windowScene else { return nil }
        if multiplayerRenderer == nil {
            let window=UIWindow(windowScene:scene)
            window.windowLevel = UIWindow.Level(rawValue:UIWindow.Level.normal.rawValue - 1)
            let root=UIViewController();root.view.backgroundColor = .black
            window.rootViewController=root;multiplayerRenderer=window
        }
        multiplayerRenderer?.isHidden=false
        return multiplayerRenderer
    }
    /// Explicit automated verification may render on the paired phone without
    /// an AirPlay receiver. Normal play retains its external-display contract.
    func benchmarkWindow() -> UIWindow? {
        guard SportsSession.benchmark else { return nil }
        refresh()
        if let external { showMenu(); return external }
        guard let scene = phone?.windowScene else { return nil }
        if preview == nil {
            let window = UIWindow(windowScene: scene)
            let root = UIViewController(); root.view.backgroundColor = .black
            window.rootViewController = root; preview = window
        }
        return preview
    }
    private var exitCover: UIWindow?
    private var exitGeneration = 0
    func beginExitCover() {
        guard let destination = external ?? preview ?? phone, let scene = destination.windowScene else { return }
        lingeringTip?.isHidden=true; lingeringTip=nil
        exitGeneration += 1; let generation = exitGeneration
        exitCover?.isHidden = true
        let cover = UIWindow(windowScene: scene); cover.windowLevel = .alert + 1
        let host = UIHostingController(rootView: ZStack {
            Club.lagoonDeep.ignoresSafeArea()
            Text("Back to the clubhouse").font(IslandUI.font(28, bold: true)).foregroundStyle(.white)
        })
        cover.rootViewController = host; cover.backgroundColor = UIColor(Club.lagoonDeep)
        exitCover=cover; cover.isHidden=false; host.view.layoutIfNeeded()
        ClubSound.play("whoosh", volume: 0.35)
        let duration = SportsSession.shared.presentationIntros == "full" ? 1.0 : 0.8
        DispatchQueue.main.asyncAfter(deadline: .now()+duration) { [weak self] in
            guard let self, generation==self.exitGeneration else { return }
            self.external?.rootViewController?.view.layoutIfNeeded(); self.phone?.rootViewController?.view.layoutIfNeeded()
            cover.isHidden=true; self.exitCover=nil
        }
    }
    func beginLoadingCover(in destination: UIWindow, startRuntime: @escaping @MainActor () -> Void) {
        lingeringTip?.isHidden=true; lingeringTip=nil
        exitGeneration += 1; exitCover?.isHidden=true; exitCover=nil
        finishLoadingCover()
        guard let scene = destination.windowScene else { startRuntime(); return }
        let cover = UIWindow(windowScene: scene)
        cover.windowLevel = UIWindow.Level(rawValue: destination.windowLevel.rawValue + 100)
        cover.backgroundColor = .clear
        let host = UIHostingController(rootView: PresentationLoadingCover(compact: scene.session.role == .windowApplication))
        host.view.backgroundColor = .clear; cover.rootViewController = host
        loadingCover = cover; cover.isHidden = false
        host.view.setNeedsLayout(); host.view.layoutIfNeeded()
        let launch = SportsSession.shared.sessionID
        // Give UIKit a display cycle before runEmbedded blocks the main thread on cold startup.
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.05) {
            guard self.loadingCover === cover, SportsSession.shared.active, SportsSession.shared.sessionID == launch else { return }
            SportsSession.shared.loading.cardAppeared()
            startRuntime()
        }
    }
    func revealLoadingFrame() { external?.isHidden = true; preview?.isHidden = false }
    private var lingeringTip: UIWindow?
    func finishLoadingCover() {
        if SportsSession.shared.loading.phase == .finished, let tip = SportsSession.shared.loading.tip,
           let scene = loadingCover?.windowScene, SportsSession.shared.loading.tipRemaining > 0 {
            let window=UIWindow(windowScene:scene); window.windowLevel = .alert
            let host=UIHostingController(rootView: VStack { Spacer(); Text(tip).font(IslandUI.font(18, bold: true)).foregroundStyle(.white).padding(16).background(IslandUI.navy, in: Capsule()).padding(.bottom, 48) })
            host.view.backgroundColor = .clear; window.backgroundColor = .clear; window.rootViewController=host
            window.isUserInteractionEnabled=false; window.isHidden=false; lingeringTip=window
            DispatchQueue.main.asyncAfter(deadline:.now()+SportsSession.shared.loading.tipRemaining) { [weak self] in
                window.isHidden=true; if self?.lingeringTip === window { self?.lingeringTip=nil }
            }
        }
        loadingCover?.isHidden = true; loadingCover = nil
    }

    private var networkOverlay: UIHostingController<MultiplayerMatchOverlay>?
    func installMultiplayerOverlay(in root: UIViewController) {
        guard SportsSession.shared.multiplayerMatchID != nil else { return }
        networkOverlay?.willMove(toParent:nil); networkOverlay?.view.removeFromSuperview(); networkOverlay?.removeFromParent()
        let host = UIHostingController(rootView:MultiplayerMatchOverlay(compact:true)); host.view.backgroundColor = .clear
        root.addChild(host); root.view.addSubview(host.view); host.didMove(toParent:root); networkOverlay = host
        host.view.translatesAutoresizingMaskIntoConstraints = false
        NSLayoutConstraint.activate([host.view.leadingAnchor.constraint(equalTo:root.view.safeAreaLayoutGuide.leadingAnchor),host.view.trailingAnchor.constraint(equalTo:root.view.safeAreaLayoutGuide.trailingAnchor),host.view.topAnchor.constraint(equalTo:root.view.safeAreaLayoutGuide.topAnchor),host.view.heightAnchor.constraint(equalToConstant:150)])
    }
    private func installBetaMark(in root: UIViewController) {
        let tag = 9023
        guard root.view.viewWithTag(tag) == nil else { return }
        let label = UILabel(); label.tag = tag; label.text = " MOTION CLUB · BETA "
        label.font = .boldSystemFont(ofSize: 12); label.textColor = .white
        label.backgroundColor = UIColor.black.withAlphaComponent(0.5)
        label.layer.cornerRadius = 6; label.clipsToBounds = true; label.isUserInteractionEnabled = false
        root.view.addSubview(label); label.translatesAutoresizingMaskIntoConstraints = false
        NSLayoutConstraint.activate([
            label.trailingAnchor.constraint(equalTo: root.view.safeAreaLayoutGuide.trailingAnchor, constant: -16),
            label.bottomAnchor.constraint(equalTo: root.view.safeAreaLayoutGuide.bottomAnchor, constant: -8),
            label.heightAnchor.constraint(equalToConstant: 24)
        ])
    }
    func restorePhoneControls() {
        if let root = external?.rootViewController { installBetaMark(in: root) }
        if external != nil && preview != nil { endPreview() }
        phone?.makeKeyAndVisible()
        if let preview {
            if !SportsSession.shared.loading.finished {
                preview.isHidden = true; phone?.makeKeyAndVisible(); return
            }
            previewOverlay?.willMove(toParent: nil); previewOverlay?.view.removeFromSuperview(); previewOverlay?.removeFromParent()
            phone?.isHidden=true
            preview.makeKeyAndVisible()
            let overlay=UIHostingController(rootView:SportsPreviewControls())
            previewOverlay=overlay
            guard let root=preview.rootViewController else { return }
            root.addChild(overlay); root.view.addSubview(overlay.view); overlay.didMove(toParent:root)
            installMultiplayerOverlay(in:root)
            overlay.view.translatesAutoresizingMaskIntoConstraints=false
            NSLayoutConstraint.activate([
                overlay.view.leadingAnchor.constraint(equalTo:root.view.safeAreaLayoutGuide.leadingAnchor),
                overlay.view.trailingAnchor.constraint(equalTo:root.view.safeAreaLayoutGuide.trailingAnchor),
                overlay.view.bottomAnchor.constraint(equalTo:root.view.safeAreaLayoutGuide.bottomAnchor),
                overlay.view.heightAnchor.constraint(equalToConstant:150)])
        } else if let root = external?.rootViewController { installMultiplayerOverlay(in:root) }
    }
    /// Finished matches use the full native phone screen, including on-phone gameplay.
    func showMatchControls() {
        preview?.isHidden = true
        phone?.makeKeyAndVisible()
    }

    /// Opt-in automated device run: capture the actual controller window after completion.
    func captureBenchmarkFinish() {
        guard SportsSession.benchmark, let window = phone else { return }
        let renderer = UIGraphicsImageRenderer(bounds: window.bounds)
        let image = renderer.image { _ in window.drawHierarchy(in: window.bounds, afterScreenUpdates: true) }
        let directory = FileManager.default.urls(for: .documentDirectory, in: .userDomainMask)[0]
            .appendingPathComponent("visual-overhaul/postgame", isDirectory: true)
            .appendingPathComponent(UUID().uuidString, isDirectory: true)
        try? FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        let url = directory.appendingPathComponent("PostGameDevice.png")
        try? image.pngData()?.write(to: url)
        let result = SportsSession.shared.finishedMatch
        let report: [String: Any] = ["startedUtc": ISO8601DateFormatter().string(from: Date()),
            "sport": "tennis", "matchComplete": result != nil, "won": result?.won ?? false,
            "score": result?.score ?? "", "phoneVisible": !window.isHidden,
            "launchArguments": ProcessInfo.processInfo.arguments,
            "screenshot": "PostGameDevice.png"]
        if let data = try? JSONSerialization.data(withJSONObject: report, options: [.prettyPrinted, .sortedKeys]) {
            try? data.write(to: directory.appendingPathComponent("report.json"), options: .atomic)
        }
        SportsDiagnostics.write("postgame device screenshot saved; phoneVisible=\(!window.isHidden) finished=\(SportsSession.shared.finishedMatch != nil)")
    }

    func endPreview() {
        finishLoadingCover()
        networkOverlay?.willMove(toParent:nil); networkOverlay?.view.removeFromSuperview(); networkOverlay?.removeFromParent(); networkOverlay = nil
        if let child=previewOverlay { child.willMove(toParent:nil); child.view.removeFromSuperview(); child.removeFromParent() }
        previewOverlay=nil
        preview?.isHidden=true; preview=nil; multiplayerRenderer?.isHidden=true; multiplayerRenderer=nil; phone?.makeKeyAndVisible()
    }
    func isPreview(_ window:UIWindow?) -> Bool { window != nil && window === preview }

    enum DisplayKind: String { case none, tv, mac }
    /// What the phone is mirroring to: a Mac (AirPlay Receiver) or a TV. The receiver's name
    /// usually says ("Adnan's MacBook Pro"); otherwise a 16:10 screen is almost always a Mac.
    static var displayKind: DisplayKind {
        guard let screen = shared.external?.windowScene?.screen else { return .none }
        let name = SportsTiming.currentTV().lowercased()
        if name.contains("mac") || name.contains("imac") { return .mac }
        let aspect = screen.bounds.width / max(1, screen.bounds.height)
        return abs(aspect - 1.6) < 0.06 ? .mac : .tv
    }
    /// The TV's own screen: the menu, loading and results (TennisTVRoot). It sits above
    /// Unity's display and is hidden while a game is on.
    static func tvRoot() -> UIViewController {
        let host=UIHostingController(rootView:TennisTVRoot())
        host.view.backgroundColor = .black
        return host
    }
    func showMenu() {
        guard let window=external else { return }
        if !(window.rootViewController is UIHostingController<TennisTVRoot>) { window.rootViewController=Self.tvRoot() }
        window.windowLevel = .normal + 1
        window.isHidden=false
    }
}

final class SportsAppDelegate: NSObject, UIApplicationDelegate {
    func application(_ application:UIApplication, supportedInterfaceOrientationsFor window:UIWindow?) -> UIInterfaceOrientationMask {
        if SportsDisplays.shared.isPreview(window) || window?.windowScene?.session.role == .windowExternalDisplayNonInteractive { return .landscape }
        return .portrait
    }
    func application(_ application:UIApplication, configurationForConnecting session:UISceneSession, options:UIScene.ConnectionOptions) -> UISceneConfiguration {
        let config=UISceneConfiguration(name:session.role == .windowExternalDisplayNonInteractive ? "Sports External" : nil,sessionRole:session.role)
        if session.role == .windowExternalDisplayNonInteractive {
            config.delegateClass=SportsExternalScene.self
        }
        return config
    }
}

final class SportsExternalScene: NSObject, UIWindowSceneDelegate {
    var window:UIWindow?
    func scene(_ scene:UIScene, willConnectTo session:UISceneSession, options:UIScene.ConnectionOptions) {
        NSLog("[SportsDisplay] external scene connected")
        guard let scene=scene as? UIWindowScene else { return }
        let window=UIWindow(windowScene:scene)
        window.rootViewController=SportsDisplays.tvRoot(); window.isHidden=false
        self.window=window; SportsDisplays.shared.external=window; SportsSession.shared.displayConnected=true
        SportsDisplays.shared.showMenu()
        SportsSession.shared.routeGameToExternalDisplay()
    }
    func sceneDidDisconnect(_ scene:UIScene) {
        SportsSession.shared.pause(reason:"Display disconnected — reconnect and tap Ready"); SportsSession.shared.displayConnected=false
        SportsSession.shared.status="Display disconnected. Reconnect your TV or Mac to continue; this phone remains your controller."
        SportsDisplays.shared.external=nil; window=nil
    }
}

struct SportsPreviewControls:View {
    @State private var position=0.0
    @State private var power=0.6
    @State private var session=SportsSession.shared
    var body:some View {
        VStack {
            if session.finishedMatch != nil {
                MatchFinishControls(session: session, compact: true)
            } else {
            HStack {
                if session.sport == "tennis" { PointClipControls(session: session) }
                if session.sport != "tennis" { Text(session.feedback).font(.caption).lineLimit(1) }
                Button(session.paused || (session.sport == "golf" && !session.golfShotReady) ? "Ready" : "Pause") { if session.paused || (session.sport == "golf" && !session.golfShotReady) { session.readyToPlay() } else { session.pause() } }
                Button("Exit Game") { session.exitGame() }.accessibilityIdentifier("game-exit")
            }
            if session.sport == "golf", session.golfPhase == "Result" {
                GolfShotResultControls(session: session)
            }
            if session.sport == "golf", session.golfPhase == "RoundDone" {
                Button(session.golfHasNextHole ? "Next Hole" : "Play Again") { session.command("golfContinue") }
                    .accessibilityIdentifier("golf-continue")
            }
            if session.sport == "tennis" && session.ready {
                if session.tennisPhase == "rally" { RallyAimPad(session:session).frame(width:180,height:90); TennisAbilityControls(session: session) }
                if session.tennisPhase == "serve" || session.tennisPhase == "toss" { ServeAimPad(session:session).frame(width:180,height:90); if session.tennisPhase == "serve" { Button("Toss") { session.toss() } } }
            }
            if session.sport != "golf" || session.golfPhase == "Aim" {
            HStack {
                if session.sport == "tennis" { Slider(value:$position,in:-1...1).accessibilityLabel("Court position").onChange(of:position) { _,v in session.steer(v) } }
                else { Button("Aim left") { session.setAim(-1) }; Button("Aim right") { session.setAim(1) }; Button("Club") { session.command("club",value:1) } }
                Slider(value:$power,in:(session.sport == "golf" ? 0.02 : 0.1)...1).accessibilityLabel("Swing power")
                Button("Swing") { session.swing(power) }.disabled(session.paused || (session.sport == "golf" && (session.golfPhase != "Aim" || !session.golfShotReady)))
            }
            }
            }
        }.padding().background(.regularMaterial)
    }
}

struct DisplayRegistration: UIViewControllerRepresentable {
    final class Controller:UIViewController {
        override func viewDidAppear(_ animated:Bool) { super.viewDidAppear(animated); SportsDisplays.shared.register(from:self) }
    }
    func makeUIViewController(context:Context) -> Controller { Controller() }
    func updateUIViewController(_ controller:Controller,context:Context) {
        DispatchQueue.main.async { SportsDisplays.shared.register(from:controller) }
    }
}

/// An independent native window covers Unity startup and fades only after the rendered-frame gate.
private struct PresentationLoadingCover: View {
    let compact: Bool
    private var loading: LoadingModel { SportsSession.shared.loading }
    var body: some View {
        GeometryReader { geometry in
            ZStack {
            Club.lagoonDeep
            LoadingScreen(menu: .shared, compact: compact)
                .frame(width: compact ? geometry.size.width : 1280, height: compact ? geometry.size.height : 720)
                .scaleEffect(compact ? 1 : min(geometry.size.width / 1280, geometry.size.height / 720))
                .frame(width: geometry.size.width, height: geometry.size.height)
            }.opacity(1 - loading.transitionFraction)
        }.ignoresSafeArea()
        .onChange(of: loading.phase) { _, phase in
            if phase == .transitioning { ClubSound.play("pop", volume: 0.35); SportsDisplays.shared.revealLoadingFrame() }
        }
    }
}
