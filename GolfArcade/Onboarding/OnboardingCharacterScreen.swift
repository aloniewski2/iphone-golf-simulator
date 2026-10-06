import SwiftUI

struct OnboardingCharacterScreen: View {
    let flow: OnboardingFlow
    @State private var session = SportsSession.shared
    @State private var name = ""
    @State private var randomizedOnly = true
    @State private var pop = false
    private var player: Player { session.players[session.playerIndex] }
    var body: some View {
        OnboardingScaffold(title: "Make yourself at home", step: "1 · YOUR PLAYER") {
            CharacterModelPreview(player: player, cameraDistance: 3.4, idleSport: .tennis)
                .frame(height: 225).scaleEffect(pop ? 1.025 : 1)
                .accessibilityLabel("\(player.name), \(player.standardFemale ? "Female" : "Male") player")
                .accessibilityIdentifier("onboarding-hero")
            VStack(alignment: .leading, spacing: 12) {
                Picker("Body", selection: Binding(get: { player.standardFemale }, set: { value in edit { $0.standardFemale = value } })) {
                    Text("Male").tag(false); Text("Female").tag(true)
                }.pickerStyle(.segmented).accessibilityIdentifier("onboarding-body")
                colourRow("Skin", value: Binding(get: { player.skinT }, set: { value in edit { $0.setSkin(value) } }), id: "onboarding-skin")
                IslandSelector(label: "Hair", value: HeroV4.haircuts[safe: player.shownHaircut] ?? "Style", compact: true) { direction in
                    edit { $0.haircut = ($0.haircut + direction + HeroV4.offered) % HeroV4.offered }
                }
                colourRow("Hair colour", value: Binding(get: { player.hairT }, set: { value in edit { $0.setHair(natural: value) } }), id: "onboarding-hair-colour")
                TextField("Your name", text: $name).font(IslandUI.font(18, bold: true))
                    .padding(14).background(.white, in: RoundedRectangle(cornerRadius: 12))
                    .submitLabel(.done).accessibilityIdentifier("onboarding-name")
                    .onChange(of: name) { _, value in
                        if value.count > 40 { name = String(value.prefix(40)) }
                        edit { $0.name = OnboardingStore.name(value) }
                    }
                IslandAction(title: "Randomize", compact: true, identifier: "onboarding-randomize") {
                    TennisMenu.shared.randomizeLook(); randomizedOnly = true
                }
            }
            IslandAction(title: "That's me!", primary: true, compact: true, identifier: "onboarding-character-save") {
                session.players[session.playerIndex].name = OnboardingStore.name(name); session.savePlayers()
                let p = player
                MultiplayerService.shared.setIdentity(name: p.name, female: p.standardFemale, left: p.handedness == .left, loadout: p.multiplayerLoadout)
                Analytics.track("hero_created", ["female": String(p.standardFemale), "named": String(p.name != "Player 1"), "randomizedOnly": String(randomizedOnly)])
                flow.next()
            }
            Text("More clothes in the Locker later.").font(IslandUI.font(13)).foregroundStyle(IslandUI.muted)
        }
        .accessibilityIdentifier("onboarding-character")
        .onAppear {
            if !flow.store.characterInitialized {
                TennisMenu.shared.randomizeLook(); flow.store.characterInitialized = true
            }
            name = player.name == "Player 1" ? "" : player.name
        }
        .onChange(of: player) { _, _ in
            guard !session.reduceMotion else { return }
            withAnimation(Club.spring) { pop = true }
            Task { @MainActor in
                try? await Task.sleep(for: .milliseconds(220))
                withAnimation(Club.spring) { pop = false }
            }
        }
    }
    private func colourRow(_ title: String, value: Binding<Double>, id: String) -> some View {
        VStack(alignment: .leading, spacing: 4) {
            Text(title).font(IslandUI.font(14, bold: true))
            Slider(value: value, in: 0...1).tint(IslandUI.navy).accessibilityLabel(title).accessibilityIdentifier(id)
        }
    }
    private func edit(_ update: (inout Player) -> Void) {
        update(&session.players[session.playerIndex]); session.savePlayers(); randomizedOnly = false
    }
}
