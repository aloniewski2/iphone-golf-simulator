import SwiftUI

@main
struct GolfArcadeApp: App {
    @UIApplicationDelegateAdaptor(SportsAppDelegate.self) private var appDelegate
    var body: some Scene {
        WindowGroup {
            SportsHome()
        }
    }
}
