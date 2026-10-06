import SwiftUI

struct GolfTutorialController: View {
    @Bindable var session: SportsSession
    var body: some View {
        OnboardingScaffold(title: "A round with the coach", step: "GOLF LESSON") {
            if let step = session.tutorialStep {
                Text("\(step.index + 1) / \(step.count)").font(IslandUI.font(13, bold: true))
                Text(step.text).font(IslandUI.font(22, bold: true))
            }
            Text(session.feedback).font(IslandUI.font(16)).accessibilityIdentifier("golf-tutorial-feedback")
            if !session.touch { Text("Take the phone back slowly, then swing through smoothly.").font(IslandUI.font(18)) }
            if (session.tutorialStep?.index ?? 0) >= 1 {
                HStack {
                    IslandAction(title: "◀ Aim", focused: false) { session.setAim(-1) }
                    IslandAction(title: "Aim ▶", focused: false) { session.setAim(1) }
                }
            }
            HStack {
                IslandAction(title: "◀ Club", focused: false) { session.command("club", value: -1) }
                IslandAction(title: "Club ▶", focused: false) { session.command("club", value: 1) }
            }
            if session.touch { IslandAction(title: "Swing", focused: false, primary: true) { session.swing(0.7) }.disabled(session.paused) }
            if session.paused { IslandAction(title: "Ready", focused: false, primary: true) { session.readyToPlay() } }
            else { Button("Pause") { session.pause() }.font(IslandUI.font(16, bold: true)) }
        }
    }
}
