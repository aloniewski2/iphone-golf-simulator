import SwiftUI

@main
struct GolfArcadeApp: App {
    @UIApplicationDelegateAdaptor(SportsAppDelegate.self) private var appDelegate
    init() { MultiplayerService.shared.prepare() }
    var body: some Scene {
        WindowGroup {
            SportsHome()
        }
    }
}
