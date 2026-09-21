import SwiftUI

@MainActor
final class SportsDisplays {
    static let shared = SportsDisplays()
    weak var phone: UIWindow?
    var external: UIWindow?
    private var preview: UIWindow?
    private var previewOverlay: UIViewController?
    private var registration: AnyObject?
    func register(from controller:UIViewController) {
        guard let window=controller.view.window else { return }
        phone=window
        if #available(iOS 27.0, *), registration == nil {
            let config=UISceneConfiguration(name:"Sports External",sessionRole:.windowExternalDisplayNonInteractive)
            config.delegateClass=SportsExternalScene.self
            registration=controller.registerSceneAccessory(.externalNonInteractive(sceneConfiguration:config))
        }
    }
    func gameWindow(preview usePreview:Bool) -> UIWindow? {
        if !usePreview { return external }
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
}

final class SportsAppDelegate: NSObject, UIApplicationDelegate {
    func application(_ application:UIApplication, supportedInterfaceOrientationsFor window:UIWindow?) -> UIInterfaceOrientationMask {
        if SportsDisplays.shared.isPreview(window) || (window != nil && window === SportsDisplays.shared.external) { return .landscape }
        return .portrait
    }
    func application(_ application:UIApplication, configurationForConnecting session:UISceneSession, options:UIScene.ConnectionOptions) -> UISceneConfiguration {
        let config=UISceneConfiguration(name:nil,sessionRole:session.role)
        if #unavailable(iOS 27.0), session.role == .windowExternalDisplayNonInteractive { config.delegateClass=SportsExternalScene.self }
        return config
    }
}

final class SportsExternalScene: NSObject, UIWindowSceneDelegate {
    var window:UIWindow?
    func scene(_ scene:UIScene, willConnectTo session:UISceneSession, options:UIScene.ConnectionOptions) {
        guard let scene=scene as? UIWindowScene else { return }
        let window=UIWindow(windowScene:scene)
        let waiting=UIViewController(); waiting.view.backgroundColor = .black
        let label=UILabel(frame:scene.coordinateSpace.bounds); label.text="Choose a sport on your iPhone"; label.textColor = .white; label.textAlignment = .center
        label.autoresizingMask=[.flexibleWidth,.flexibleHeight]; waiting.view.addSubview(label)
        window.rootViewController=waiting; window.isHidden=false
        self.window=window; SportsDisplays.shared.external=window; SportsSession.shared.displayConnected=true
        if SportsSession.shared.active && SportsSession.shared.ready {
            SportsRuntime.shared().attach(to:window)
            SportsSession.shared.command("display")
            window.isHidden=true
            SportsDisplays.shared.restorePhoneControls()
            SportsSession.shared.status="Display reconnected. Recalibrate and resume when ready."
        }
    }
    func sceneDidDisconnect(_ scene:UIScene) {
        SportsSession.shared.pause(); SportsSession.shared.displayConnected=false
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
                Button(session.paused ? "Resume" : "Pause") { if session.paused { session.resume() } else { session.pause() } }
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
    func updateUIViewController(_ controller:Controller,context:Context) {}
}
