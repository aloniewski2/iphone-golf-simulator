import SwiftUI

struct OnboardingRoot: View {
    let flow: OnboardingFlow
    var body: some View {
        Group {
            if flow.step == .account { OnboardingAccountScreen(flow: flow) }
            else if flow.step == .character { OnboardingCharacterScreen(flow: flow) }
            else if flow.step == .connect { OnboardingConnectScreen(flow: flow) }
            else if flow.step != .done { OnboardingGamePicker(flow: flow) }
            else { Text(flow.step.key).accessibilityIdentifier("onboarding-root") }
        }
        .safeAreaInset(edge: .top) {
            HStack {
                Button { flow.back() } label: { Label("Back", systemImage: "chevron.left") }
                    .accessibilityIdentifier("onboarding-back")
                Spacer()
                Button("Main Menu") { flow.exitToMenu() }.accessibilityIdentifier("onboarding-exit")
            }
            .font(IslandUI.font(16, bold: true)).foregroundStyle(IslandUI.navy)
            .padding(.horizontal, 22).frame(minHeight: 48).background(IslandUI.paper)
        }
    }
}

struct OnboardingScaffold<Content: View>: View {
    let title: String
    let step: String
    @ViewBuilder let content: () -> Content
    var body: some View {
        ZStack {
            IslandBackdrop()
            ScrollView {
                VStack(alignment: .leading, spacing: 20) {
                    Text(step).font(IslandUI.font(12, bold: true)).tracking(2).foregroundStyle(IslandUI.muted)
                    Text(title).font(IslandUI.font(32, bold: true)).fixedSize(horizontal: false, vertical: true)
                    content()
                }.padding(24).frame(maxWidth: 540, alignment: .leading)
                    .background(IslandUI.paper.opacity(0.94), in: RoundedRectangle(cornerRadius: 28))
                    .padding(18).padding(.top, 18).frame(maxWidth: .infinity)
            }.scrollDismissesKeyboard(.interactively)
        }.foregroundStyle(IslandUI.navy).preferredColorScheme(.light)
    }
}

struct OnboardingHoldingCard: View {
    var body: some View {
        ZStack {
            IslandBackdrop()
            VStack(spacing: 24) {
                IslandWordmark(size: 56)
                Label("Finish setup on your phone", systemImage: "iphone").font(IslandUI.font(32, bold: true))
            }.foregroundStyle(IslandUI.navy)
        }
    }
}
