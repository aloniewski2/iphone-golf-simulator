import SwiftUI

struct OnboardingConnectScreen: View {
    let flow: OnboardingFlow
    @State private var session = SportsSession.shared
    @State private var mac = false
    @State private var pulse = false
    var body: some View {
        let card = mac ? HowTo.connectMac : HowTo.connectTV
        OnboardingScaffold(title: "Play on the big screen", step: "SCREEN SETUP") {
            Picker("Screen", selection: $mac) { Text("TV").tag(false); Text("Mac").tag(true) }.pickerStyle(.segmented)
            HowToArt(art: card.art).frame(height: 150)
            ForEach(Array(card.steps.enumerated()), id: \.offset) { i, text in
                HStack(alignment: .top, spacing: 12) {
                    Text("\(i + 1)").font(IslandUI.font(17, bold: true)).frame(width: 30, height: 30).background(IslandUI.lime, in: Circle())
                    Text(text).font(IslandUI.font(17)).fixedSize(horizontal: false, vertical: true)
                }
            }
            Text("Use the same Wi-Fi network and keep your phone unlocked.").font(IslandUI.font(14)).foregroundStyle(IslandUI.muted)
            Label(session.displayConnected ? "Connected to \(SportsTiming.currentTV())!" : "Waiting for a screen…", systemImage: session.displayConnected ? "checkmark.circle.fill" : "airplayvideo")
                .font(IslandUI.font(17, bold: true)).foregroundStyle(session.displayConnected ? Club.green : IslandUI.muted)
                .opacity(session.displayConnected || !pulse ? 1 : 0.55).accessibilityIdentifier("onboarding-screen-status")
            if session.displayConnected {
                IslandAction(title: "Play on the TV", focused: false, primary: true) { flow.playOnTV() }.accessibilityIdentifier("onboarding-tv")
            }
            Text("A TV or Mac is required. This phone stays your controller.").font(IslandUI.font(15))
        }
        .onAppear {
            SportsDisplays.shared.refresh()
            if !session.reduceMotion { withAnimation(.easeInOut(duration: 1.2).repeatForever(autoreverses: true)) { pulse = true } }
            if session.displayConnected { connected() }
        }
        .onChange(of: session.displayConnected) { _, value in if value { connected() } }
    }
    private func connected() {
        let kind = SportsDisplays.displayKind
        Analytics.track("tv_connected", ["kind": kind == .none ? "other" : kind.rawValue])
        ClubSound.play("pop")
        if session.haptics { UINotificationFeedbackGenerator().notificationOccurred(.success) }
    }
}

/// The guide's existing art cases, drawn with native symbols in the Island palette.
struct HowToArt: View {
    let art: HowToCard.Art
    var body: some View {
        HStack(spacing: 24) {
            Image(systemName: "iphone").font(.system(size: 64, weight: .light))
            Image(systemName: "arrow.right").font(.system(size: 25, weight: .bold)).foregroundStyle(IslandUI.lime)
            Image(systemName: art == .mac ? "laptopcomputer" : art == .stand ? "figure.tennis" : "tv").font(.system(size: 72, weight: .light))
        }.foregroundStyle(IslandUI.navy).frame(maxWidth: .infinity, maxHeight: .infinity)
            .background(IslandUI.paper, in: RoundedRectangle(cornerRadius: 20)).accessibilityHidden(true)
    }
}
