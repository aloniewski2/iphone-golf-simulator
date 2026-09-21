import SwiftUI

@MainActor
final class SportsDisplays: NSObject {
    static let shared = SportsDisplays()
    weak var phone: UIWindow?
    var external: UIWindow?
    private var preview: UIWindow?
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
            let waiting=UIViewController(); waiting.view.backgroundColor = .black
            window.rootViewController=waiting; window.isHidden=false; external=window
        }
        SportsSession.shared.displayConnected = external != nil
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
        if !usePreview { showWaiting("Loading your game…"); return external }
        guard let scene=phone?.windowScene else { return nil }
        let window=UIWindow(windowScene:scene); preview=window; return window
    }
    func restorePhoneControls() {
        phone?.isHidden=false; phone?.makeKey()
        if let preview {
            phone?.isHidden=true
            preview.makeKeyAndVisible()
            let overlay=UIHostingController(rootView:SportsPreviewControls())
            previewOverlay=overlay
            guard let root=preview.rootViewController else { return }
            root.addChild(overlay); root.view.addSubview(overlay.view); overlay.didMove(toParent:root)
            overlay.view.translatesAutoresizingMaskIntoConstraints=false
            NSLayoutConstraint.activate([
                overlay.view.leadingAnchor.constraint(equalTo:root.view.safeAreaLayoutGuide.leadingAnchor),
                overlay.view.trailingAnchor.constraint(equalTo:root.view.safeAreaLayoutGuide.trailingAnchor),
                overlay.view.bottomAnchor.constraint(equalTo:root.view.safeAreaLayoutGuide.bottomAnchor),
                overlay.view.heightAnchor.constraint(equalToConstant:150)])
        }
    }
    func endPreview() {
        if let child=previewOverlay { child.willMove(toParent:nil); child.view.removeFromSuperview(); child.removeFromParent() }
        previewOverlay=nil
        preview?.isHidden=true; preview=nil; phone?.makeKeyAndVisible()
    }
    func isPreview(_ window:UIWindow?) -> Bool { window != nil && window === preview }
    func showWaiting(_ message:String) {
        guard let window=external,let view=window.rootViewController?.view else { return }
        let label=(view.subviews.compactMap { $0 as? UILabel }.first) ?? UILabel(frame:view.bounds)
        label.text=message; label.textColor = .white; label.textAlignment = .center
        label.autoresizingMask=[.flexibleWidth,.flexibleHeight]
        if label.superview == nil { view.addSubview(label) }
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
        let waiting=UIViewController(); waiting.view.backgroundColor = .black
        let label=UILabel(frame:scene.coordinateSpace.bounds); label.text="Choose a sport on your iPhone"; label.textColor = .white; label.textAlignment = .center
        label.autoresizingMask=[.flexibleWidth,.flexibleHeight]; waiting.view.addSubview(label)
        window.rootViewController=waiting; window.isHidden=false
        self.window=window; SportsDisplays.shared.external=window; SportsSession.shared.displayConnected=true
        SportsDisplays.shared.showWaiting("Choose a sport on your iPhone")
        if SportsSession.shared.active && SportsSession.shared.ready {
            SportsRuntime.shared().attach(to:window)
            SportsSession.shared.command("display")
            SportsDisplays.shared.restorePhoneControls()
            SportsSession.shared.status="Display reconnected. Waiting for a gameplay frame…"
        }
    }
    func sceneDidDisconnect(_ scene:UIScene) {
        SportsSession.shared.pause(reason:"Display disconnected — reconnect and tap Ready"); SportsSession.shared.displayConnected=false
        SportsSession.shared.status="Display disconnected. Reconnect or return to the menu for on-phone preview."
        SportsDisplays.shared.external=nil; window=nil
    }
}

private struct SportsPreviewControls:View {
    @State private var position=0.0
    @State private var power=0.6
    @State private var session=SportsSession.shared
    var body:some View {
        VStack {
            HStack {
                Text(session.feedback).font(.caption).lineLimit(1)
                Button(session.paused ? "Ready" : "Pause") { if session.paused { session.readyToPlay() } else { session.pause() } }
                Button("Menu") { session.end() }
            }
            HStack {
                if session.sport == "tennis" { Slider(value:$position,in:-1...1).accessibilityLabel("Court position").onChange(of:position) { _,v in session.steer(v) } }
                else { Button("Aim left") { session.setAim(-1) }; Button("Aim right") { session.setAim(1) }; Button("Club") { session.command("club",value:1) } }
                Slider(value:$power,in:0.1...1).accessibilityLabel("Swing power")
                Button("Swing") { session.swing(power) }.disabled(session.paused)
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
