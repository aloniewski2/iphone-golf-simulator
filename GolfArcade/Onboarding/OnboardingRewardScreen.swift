import SwiftUI

struct OnboardingRewardScreen: View {
    let flow: OnboardingFlow
    let sport: Sport
    @State private var revealed = false
    var body: some View {
        OnboardingScaffold(title: "You’re in the club!", step: "TUTORIAL COMPLETE") {
            if let player = TennisMenu.shared.player {
                CharacterModelPreview(player: player, cameraDistance: 3.4, idleSport: sport).frame(height: 230)
                    .scaleEffect(revealed ? 1 : 0.85)
                LevelStrip(player: player.id).padding(18).frame(maxWidth: .infinity).background(IslandUI.navy, in: RoundedRectangle(cornerRadius: 16))
            }
            Text("+\(flow.store.rewardXP(sport)) XP").font(IslandUI.font(36, bold: true)).accessibilityIdentifier("onboarding-reward-xp")
            if flow.store.rewardItem(sport) == "white-headband" {
                VStack(alignment: .leading, spacing: 8) {
                    Label("NEW: White Headband", systemImage: "sparkles").font(IslandUI.font(23, bold: true))
                    Text("Saved to your rewards").font(IslandUI.font(15))
                    Button("Equip") { flow.store.requestEquip("white-headband") }.font(IslandUI.font(16, bold: true))
                }.padding(20).frame(maxWidth: .infinity, alignment: .leading).background(IslandUI.lime, in: RoundedRectangle(cornerRadius: 20))
            }
            IslandAction(title: "Let’s play \(sport.rawValue)", focused: false, primary: true) { flow.finish(sport) }.accessibilityIdentifier("onboarding-reward-continue")
        }.onAppear {
            if SportsSession.shared.reduceMotion { revealed = true }
            else { withAnimation(Club.spring) { revealed = true } }
            ClubSound.play("pop")
        }
    }
}
