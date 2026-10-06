import SwiftUI

// Old saved tutorial routes use the normal controller now.
struct GolfTutorialController: View {
    @Bindable var session: SportsSession
    var body: some View { GolfPhoneController(session: session) }
}
