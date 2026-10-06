import SwiftUI

struct OnboardingMotionPrimer: View {
    let flow: OnboardingFlow
    var body: some View {
        OnboardingScaffold(title: "Your phone is the racket", step: "MOTION PLAY") {
            HowToArt(art: .stand).frame(height: 180)
            Text("To know where the TV is, the game uses the rear camera once to lock the court direction. Nothing is recorded or uploaded.").font(IslandUI.font(19))
            Text("The game will ask for camera access when you start the tutorial. Touch controls are always available.").font(IslandUI.font(15)).foregroundStyle(IslandUI.muted)
            IslandAction(title: "Got it", focused: false, primary: true) { flow.primer(touch: false) }.accessibilityIdentifier("onboarding-primer-motion")
            IslandAction(title: "Use touch instead", focused: false) { flow.primer(touch: true) }.accessibilityIdentifier("onboarding-primer-touch")
        }
    }
}
