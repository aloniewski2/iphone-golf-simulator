import SwiftUI

@main
struct GolfArcadeApp: App {
    @UIApplicationDelegateAdaptor(GolfAppDelegate.self) private var appDelegate
    var body: some Scene {
        WindowGroup {
            ContentView()
                .background(TVAccessoryRegistration().frame(width: 0, height: 0))
        }
    }
}
