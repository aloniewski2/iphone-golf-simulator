import SwiftUI

struct OnboardingAccountScreen: View {
    let flow: OnboardingFlow
    @State private var account = OnboardingAccountModel()
    var body: some View {
        OnboardingScaffold(title: "Welcome to the Island Sports Club", step: "1 · YOUR PLAYER") {
            IslandWordmark(size: 48).frame(maxWidth: .infinity).padding(.vertical, 28)
            Text("Sign in with Game Center to play friends online and keep your progress.")
                .font(IslandUI.font(19)).fixedSize(horizontal: false, vertical: true)
            if !account.message.isEmpty {
                Label(account.message, systemImage: flow.store.account?.hasPrefix("gc:") == true ? "checkmark.circle.fill" : "info.circle")
                    .font(IslandUI.font(16, bold: true)).accessibilityIdentifier("onboarding-account-status")
            }
            IslandAction(title: "Sign in with Game Center", primary: !account.guestPrimary, compact: true, identifier: "onboarding-signin") { account.start(flow) }
            IslandAction(title: "Continue as guest", primary: account.guestPrimary, compact: true, identifier: "onboarding-guest") { account.guest(flow) }
            Text("A guest can play right away. You can sign in later from Play Online.")
                .font(IslandUI.font(13)).foregroundStyle(IslandUI.muted)
        }
        .accessibilityIdentifier("onboarding-account")
        .task { account.start(flow) }
        .sheet(item: $account.sheet, onDismiss: { account.dismissed(flow) }) { item in OnlineGameCenterSheet(controller: item.controller) }
    }
}
