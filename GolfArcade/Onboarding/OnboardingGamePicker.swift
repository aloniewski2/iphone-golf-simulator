import SwiftUI

struct OnboardingGamePicker: View {
    let flow: OnboardingFlow
    var body: some View {
        OnboardingScaffold(title: "Choose your game", step: "LET’S PLAY") {
            game(.tennis, title: "Tennis", detail: "The Tropical Open · Island Circuit", scene: "tennis", art: "racket", symbol: "tennis.racket")
            game(.golf, title: "Golf", detail: "Cliffside · play a round", scene: "golf", art: "golf", symbol: "figure.golf")
        }
    }
    private func game(_ sport: Sport, title: String, detail: String, scene: String, art: String, symbol: String) -> some View {
        Button { flow.choose(sport) } label: {
            VStack(alignment: .leading, spacing: 0) {
                SceneImage(name: Club.scene(for: sport)).frame(height: 130).overlay(alignment: .trailing) { ClubArt(name: art, fallback: symbol).frame(width: 100, height: 100).padding(16) }
                VStack(alignment: .leading, spacing: 8) {
                    Text(title).font(IslandUI.font(27, bold: true))
                    Text(detail).font(IslandUI.font(16))
                    if sport == .golf { Text("Campaign & training coming soon").font(IslandUI.font(12)).foregroundStyle(IslandUI.muted) }
                }.padding(18).frame(maxWidth: .infinity, alignment: .leading).background(IslandUI.paper)
            }.clipShape(RoundedRectangle(cornerRadius: 22)).foregroundStyle(IslandUI.navy)
        }.buttonStyle(ClubPress()).accessibilityIdentifier("onboarding-game-\(sport.rawValue)")
    }
}
