import SwiftUI

/// The online hub (one button on the phone): invites, your party, who is online, who is not, adding friends, inviting someone
/// without the app. Dark and quiet like the plaza controller. PLAN_OnlineHub_FriendsInvites.md §4.
struct ClubHubView: View {
    @Bindable var hub = ClubHub.shared
    var dismiss: () -> Void = {}
    @State private var code = ""
    @State private var sharing = false
    @State private var shareText = ""
    @State private var confirmJoin: HubInvite?

    var body: some View {
        NavigationStack {
            ScrollView {
                VStack(alignment: .leading, spacing: 18) {
                    statusCard
                    if !hub.notice.isEmpty { noticeBar }
                    if let invite = hub.resolvedInvite { codeCard(invite) }
                    if !hub.invites.isEmpty { invitesSection }
                    partySection
                    friendsSections
                    addSection
                    codeSection
                    settingsSection
                }
                .padding(.horizontal, 20).padding(.vertical, 14)
            }
            .background(LinearGradient(colors: [Color(hex: "16294A"), IslandUI.dark], startPoint: .top, endPoint: .bottom).ignoresSafeArea())
            .navigationTitle("Friends").navigationBarTitleDisplayMode(.inline)
            .toolbar { ToolbarItem(placement: .confirmationAction) { Button("Done") { hub.clearNotice(); dismiss() }.accessibilityIdentifier("club-hub-done") } }
            .toolbarBackground(Color(hex: "16294A"), for: .navigationBar).toolbarBackground(.visible, for: .navigationBar)
        }
        .preferredColorScheme(.dark)
        .foregroundStyle(.white)
        .task { hub.start(); await hub.refresh() }
        .sheet(isPresented: $sharing) { ShareSheet(items: [shareText]).presentationDetents([.medium, .large]) }
        .confirmationDialog("Leave your party to join \(confirmJoin?.from.name ?? "them")?", isPresented: Binding(get: { confirmJoin != nil }, set: { if !$0 { confirmJoin = nil } }), titleVisibility: .visible) {
            Button("Leave and join") { if let i = confirmJoin { hub.accept(i) }; confirmJoin = nil }
            Button("Stay", role: .cancel) { confirmJoin = nil }
        }
        .accessibilityIdentifier("club-hub")
    }

    // MARK: sections

    private var statusCard: some View {
        HStack(spacing: 12) {
            Circle().fill(statusColor).frame(width: 12, height: 12).shadow(color: statusColor, radius: 5)
            VStack(alignment: .leading, spacing: 2) {
                Text(statusTitle).font(IslandUI.font(17, bold: true)).accessibilityIdentifier("club-hub-status")
                Text(statusDetail).font(IslandUI.font(13)).foregroundStyle(.white.opacity(0.65))
            }
            Spacer()
            if hub.link == .signedOut {
                Button("Sign in") { ClubSignIn.signIn() }
                    .buttonStyle(.borderedProminent).tint(IslandUI.lime).foregroundStyle(IslandUI.navy)
            }
        }
        .padding(14).background(.white.opacity(0.08), in: RoundedRectangle(cornerRadius: 18, style: .continuous))
    }
    private var statusColor: Color {
        switch hub.link { case .online: IslandUI.lime; case .connecting: .orange; case .notConfigured: .gray; case .signedOut: .orange; case .offline: .red.opacity(0.8) }
    }
    private var statusTitle: String {
        switch hub.link {
        case .online: hub.invisible ? "You appear offline" : "You are online"
        case .connecting: "Connecting…"
        case .notConfigured: "Friends"
        case .signedOut: "Sign in to Game Center"
        case .offline: "Not connected"
        }
    }
    private var statusDetail: String {
        switch hub.link {
        case .online: "\(hub.onlineFriends.count) friend\(hub.onlineFriends.count == 1 ? "" : "s") online"
        case .connecting: "Reaching the Club Hub"
        case .notConfigured: "Friends online need the Club Hub service, which this build has no address for yet."
        case .signedOut: "Game Center keeps your friends list."
        case .offline(let why): why.isEmpty ? "Trying again…" : why
        }
    }
    private var noticeBar: some View {
        Text(hub.notice).font(IslandUI.font(14, bold: true)).foregroundStyle(IslandUI.navy)
            .frame(maxWidth: .infinity, alignment: .leading).padding(12).background(IslandUI.lime, in: RoundedRectangle(cornerRadius: 14, style: .continuous))
            .onTapGesture { hub.clearNotice() }.accessibilityIdentifier("club-hub-notice")
    }

    private func codeCard(_ invite: HubInvite) -> some View {
        VStack(alignment: .leading, spacing: 10) {
            Text("INVITE FOUND").font(IslandUI.font(12, bold: true)).tracking(1.6).foregroundStyle(IslandUI.lime)
            Text("\(invite.from.name) is hosting \(invite.sportTitle)").font(IslandUI.font(20, bold: true))
            Button { hub.accept(invite) } label: { bigButton("Join \(invite.from.name)") }.accessibilityIdentifier("club-code-join")
        }
        .padding(14).background(.white.opacity(0.1), in: RoundedRectangle(cornerRadius: 18, style: .continuous))
        .overlay(RoundedRectangle(cornerRadius: 18, style: .continuous).strokeBorder(IslandUI.lime.opacity(0.6), lineWidth: 2))
    }

    private var invitesSection: some View {
        section("Invites", badge: hub.invites.count) {
            ForEach(hub.invites) { invite in
                VStack(alignment: .leading, spacing: 10) {
                    HStack(spacing: 12) {
                        avatar(invite.from.name, dot: nil)
                        VStack(alignment: .leading, spacing: 2) {
                            Text(invite.from.name).font(IslandUI.font(17, bold: true))
                            Text("invites you to \(invite.sportTitle) · Online").font(IslandUI.font(13)).foregroundStyle(.white.opacity(0.65))
                        }
                        Spacer()
                    }
                    HStack(spacing: 10) {
                        Button { join(invite) } label: { smallButton("Join", filled: true) }.accessibilityIdentifier("club-invite-join-\(invite.from.id)")
                        Button { hub.decline(invite) } label: { smallButton("Not now", filled: false) }.accessibilityIdentifier("club-invite-decline-\(invite.from.id)")
                    }
                }
                .padding(12).background(.white.opacity(0.07), in: RoundedRectangle(cornerRadius: 14, style: .continuous))
            }
        }
    }

    private var partySection: some View {
        let lobby = MultiplayerService.shared.lobby
        return Group {
            if let lobby, lobby.phase == .lobby, MultiplayerService.shared.isParty {
                section("Your party") {
                    ForEach(lobby.participants.filter { !$0.isGuest }, id: \.id) { p in
                        HStack(spacing: 12) {
                            avatar(p.name, dot: p.connected ? IslandUI.lime : .gray)
                            Text(p.name + (p.id == MultiplayerService.shared.localID ? " (you)" : "")).font(IslandUI.font(16, bold: true))
                            if p.id == lobby.ownerID { Image(systemName: "crown.fill").font(.system(size: 13)).foregroundStyle(IslandUI.lime) }
                            Spacer()
                        }
                    }
                    Button(role: .destructive) { hub.leaveParty() } label: { smallButton("Leave party", filled: false) }
                        .accessibilityIdentifier("club-party-leave")
                }
            }
        }
    }

    private var friendsSections: some View {
        Group {
            if hub.access == .authorized || !hub.friends.isEmpty {
                section("Online", badge: hub.onlineFriends.count) {
                    if hub.onlineFriends.isEmpty { empty(hub.link == .online ? "None of your friends are online." : "Online status needs a connection.") }
                    ForEach(hub.onlineFriends) { friendRow($0) }
                }
                section("Offline", badge: hub.offlineFriends.count) {
                    if hub.offlineFriends.isEmpty { empty("Nobody offline.") }
                    ForEach(hub.offlineFriends) { friendRow($0) }
                    if !hub.offlineFriends.isEmpty, hub.link == .online { Text("Invite anyone: an offline friend sees it the moment they open the game.").font(IslandUI.font(12)).foregroundStyle(.white.opacity(0.5)) }
                }
            } else {
                section("Friends") {
                    switch hub.access {
                    case .notDetermined:
                        Text("Allow Motion Club to see your Game Center friends, so you can see who is online and invite them.").font(IslandUI.font(14)).foregroundStyle(.white.opacity(0.75))
                        Button { Task { await hub.allowFriends() } } label: { bigButton("Allow friends list") }.accessibilityIdentifier("club-allow-friends")
                    case .denied, .restricted:
                        Text("Friends are off for this game. Turn them on in Settings › Game Center › Motion Club.").font(IslandUI.font(14)).foregroundStyle(.white.opacity(0.75))
                    default:
                        empty("Sign in to Game Center to see your friends.")
                    }
                }
            }
            if !hub.blockedFriends.isEmpty {
                section("Blocked") { ForEach(hub.blockedFriends) { f in
                    HStack { Text(f.name).font(IslandUI.font(15)); Spacer(); Button("Unblock") { hub.unblock(f) }.font(IslandUI.font(14, bold: true)) }
                } }
            }
        }
    }

    private func friendRow(_ f: ClubHub.Friend) -> some View {
        let state = f.online ? stateText(f.state) : "Offline"
        let sent = hub.sent[f.id]
        return HStack(spacing: 12) {
            avatar(f.name, dot: f.online ? IslandUI.lime : .gray.opacity(0.6))
            VStack(alignment: .leading, spacing: 2) {
                Text(f.name).font(IslandUI.font(17, bold: true))
                Text(sent?.text ?? state).font(IslandUI.font(13)).foregroundStyle(sent == nil ? .white.opacity(0.6) : IslandUI.lime)
            }
            Spacer()
            if inParty(f.id) {
                Text("In your party").font(IslandUI.font(13, bold: true)).foregroundStyle(IslandUI.lime)
            } else if let sent, sent.pending {
                Button { hub.cancelInvite(to: f) } label: { smallButton("Cancel", filled: false) }
            } else if sent == .accepted {
                ProgressView().tint(IslandUI.lime)
            } else {
                // one tap, online or offline (offline friends get it the moment they open the game)
                Button { hub.invite(f) } label: { smallButton("Invite", filled: true) }
                    .disabled(hub.link != .online).opacity(hub.link == .online ? 1 : 0.4)
                    .accessibilityIdentifier("club-invite-\(f.id)")
            }
        }
        .padding(10).background(.white.opacity(0.06), in: RoundedRectangle(cornerRadius: 14, style: .continuous))
        .contextMenu { Button(role: .destructive) { hub.block(f) } label: { Label("Block \(f.name)", systemImage: "hand.raised.fill") } }
        .accessibilityElement(children: .combine).accessibilityIdentifier("club-friend-\(f.id)")
    }

    private func inParty(_ id: String) -> Bool {
        MultiplayerService.shared.isParty && MultiplayerService.shared.lobby?.participants.contains { $0.id == id } == true
    }
    private func stateText(_ s: HubPresenceState?) -> String {
        switch s { case .plaza?: "In the Plaza"; case .lobby?: "In a lobby"; case .match?: "In a match"; case .away?: "Away"; case .dnd?: "Do not disturb"; case nil: "Online" }
    }

    private var addSection: some View {
        section("Add friends") {
            Button { hub.addFriend() } label: { rowButton("person.badge.plus", "Add a friend", "Opens Game Center's friend request") }.accessibilityIdentifier("club-add-friend")
            ForEach(hub.recentNotFriends.prefix(5)) { p in
                HStack { avatar(p.name, dot: nil); Text(p.name).font(IslandUI.font(15, bold: true)); Spacer(); Text("Played recently").font(IslandUI.font(12)).foregroundStyle(.white.opacity(0.5)) }
            }
        }
    }

    private var codeSection: some View {
        section("Someone without the app?") {
            Button {
                if let text = hub.prepareShare() { shareText = text; sharing = true }
            } label: { rowButton("square.and.arrow.up", "Send an invite link", "Text them a link and a code") }.accessibilityIdentifier("club-share")
            HStack(spacing: 10) {
                TextField("Have an invite code?", text: $code).textInputAutocapitalization(.characters).autocorrectionDisabled()
                    .font(IslandUI.font(16, bold: true)).padding(12).background(.white.opacity(0.1), in: RoundedRectangle(cornerRadius: 12))
                    .accessibilityIdentifier("club-code-field")
                PasteButton(payloadType: String.self) { strings in
                    if let t = strings.first { Task { @MainActor in code = t; hub.resolve(t) } }
                }.labelStyle(.iconOnly).buttonBorderShape(.capsule).tint(IslandUI.lime)
                Button { hub.resolve(code) } label: { smallButton("Find", filled: true) }.accessibilityIdentifier("club-code-find")
            }
        }
    }

    private var settingsSection: some View {
        section("Settings") {
            Toggle("Appear offline", isOn: $hub.invisible).tint(IslandUI.lime).accessibilityIdentifier("club-appear-offline")
            Toggle("Do not disturb", isOn: $hub.doNotDisturb).tint(IslandUI.lime).accessibilityIdentifier("club-dnd")
            Text("Friends who invite you see \"Busy\" and no banner appears.").font(IslandUI.font(12)).foregroundStyle(.white.opacity(0.5))
            Text("Your Game Center friends who have Motion Club appear here.").font(IslandUI.font(12)).foregroundStyle(.white.opacity(0.5))
        }
    }

    // MARK: pieces

    private func join(_ invite: HubInvite) {
        if MultiplayerService.shared.lobby != nil, !MultiplayerService.shared.isNearby, (MultiplayerService.shared.lobby?.participants.count ?? 1) > 1 { confirmJoin = invite }
        else { hub.accept(invite) }
    }

    private func section<C: View>(_ title: String, badge: Int? = nil, @ViewBuilder _ content: () -> C) -> some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack(spacing: 8) {
                Text(title.uppercased()).font(IslandUI.font(12, bold: true)).tracking(1.6).foregroundStyle(IslandUI.lime)
                if let badge, badge > 0 { Text("\(badge)").font(IslandUI.font(12, bold: true)).foregroundStyle(IslandUI.navy).padding(.horizontal, 7).padding(.vertical, 2).background(IslandUI.lime, in: Capsule()) }
            }
            content()
        }
    }
    private func empty(_ text: String) -> some View { Text(text).font(IslandUI.font(14)).foregroundStyle(.white.opacity(0.5)).padding(.vertical, 4) }
    private func avatar(_ name: String, dot: Color?) -> some View {
        Text(String(name.prefix(1)).uppercased()).font(IslandUI.font(18, bold: true)).foregroundStyle(IslandUI.navy)
            .frame(width: 42, height: 42).background(Circle().fill(Color(hue: Double(abs(name.hashValue) % 360) / 360, saturation: 0.45, brightness: 0.95)))
            .overlay(alignment: .bottomTrailing) { if let dot { Circle().fill(dot).frame(width: 13, height: 13).overlay(Circle().stroke(Color(hex: "16294A"), lineWidth: 2.5)) } }
    }
    private func bigButton(_ title: String) -> some View {
        Text(title).font(IslandUI.font(18, bold: true)).foregroundStyle(IslandUI.navy).frame(maxWidth: .infinity, minHeight: 50).background(IslandUI.lime, in: Capsule())
    }
    private func smallButton(_ title: String, filled: Bool) -> some View {
        Text(title).font(IslandUI.font(15, bold: true)).foregroundStyle(filled ? IslandUI.navy : .white).padding(.horizontal, 16).frame(minHeight: 38)
            .background(filled ? IslandUI.lime : Color.white.opacity(0.12), in: Capsule())
    }
    private func rowButton(_ icon: String, _ title: String, _ subtitle: String) -> some View {
        HStack(spacing: 12) {
            Image(systemName: icon).font(.system(size: 18, weight: .bold)).frame(width: 42, height: 42).background(.white.opacity(0.1), in: Circle())
            VStack(alignment: .leading, spacing: 2) { Text(title).font(IslandUI.font(16, bold: true)); Text(subtitle).font(IslandUI.font(12)).foregroundStyle(.white.opacity(0.6)) }
            Spacer(); Image(systemName: "chevron.right").font(.system(size: 13, weight: .bold)).foregroundStyle(.white.opacity(0.4))
        }
        .padding(10).background(.white.opacity(0.06), in: RoundedRectangle(cornerRadius: 14, style: .continuous))
    }
}

struct ShareSheet: UIViewControllerRepresentable {
    let items: [Any]
    func makeUIViewController(context: Context) -> UIActivityViewController { UIActivityViewController(activityItems: items, applicationActivities: nil) }
    func updateUIViewController(_ controller: UIActivityViewController, context: Context) {}
}

/// The in-game invite notification: a banner over whatever the phone is showing, like a console invite pop-up. It lives in a
/// window of its own above everything (InviteBannerWindow), is held while a match or a load is on, and runs a 2-minute
/// countdown. One tap on Join and you are in your friend's party, in the Plaza.
struct InviteBannerHost: View {
    var hub = ClubHub.shared
    var body: some View {
        VStack(spacing: 8) {
            if let invite = hub.banner { InviteBanner(hub: hub, invite: invite).transition(.move(edge: .top).combined(with: .opacity)) }
            if let who = hub.joining { JoiningPill(hub: hub, who: who).transition(.move(edge: .top).combined(with: .opacity)) }
            else if !hub.notice.isEmpty, !hub.hubOpen { NoticePill(hub: hub).transition(.opacity) }
        }
        .padding(.horizontal, 12).padding(.top, 6)
        .frame(maxWidth: 560)
        .animation(.spring(response: 0.4, dampingFraction: 0.8), value: hub.banner?.inviteID)
        .animation(.easeInOut(duration: 0.25), value: hub.joining)
        .allowsHitTesting(hub.banner != nil || hub.joining != nil || !hub.notice.isEmpty)
    }
}

/// The invite banner's own window, above every other window on the phone (the app's sheets, the Friends list, the Plaza drawn on
/// the phone, Apple's sheets): an invite can never be hidden behind something. Only the banner itself takes touches; everything
/// around it goes through to the app.
@MainActor enum InviteBannerWindow {
    private(set) static var window: UIWindow?
    static func install() {
        guard window == nil, let scene = UIApplication.shared.connectedScenes.compactMap({ $0 as? UIWindowScene })
            .first(where: { $0.session.role == .windowApplication }) else { return }
        let w = PassthroughWindow(windowScene: scene)
        w.windowLevel = UIWindow.Level(rawValue: UIWindow.Level.alert.rawValue + 2)
        w.backgroundColor = .clear
        let root = UIViewController(); root.view = PassthroughView(); root.view.backgroundColor = .clear
        let host = UIHostingController(rootView: InviteBannerHost())
        host.view.backgroundColor = .clear
        host.sizingOptions = .intrinsicContentSize
        root.addChild(host); root.view.addSubview(host.view); host.didMove(toParent: root)
        host.view.translatesAutoresizingMaskIntoConstraints = false
        NSLayoutConstraint.activate([
            host.view.topAnchor.constraint(equalTo: root.view.safeAreaLayoutGuide.topAnchor),
            host.view.centerXAnchor.constraint(equalTo: root.view.centerXAnchor),
            host.view.widthAnchor.constraint(lessThanOrEqualTo: root.view.widthAnchor),
        ])
        let fill = host.view.widthAnchor.constraint(equalTo: root.view.widthAnchor); fill.priority = .defaultHigh; fill.isActive = true
        w.rootViewController = root
        w.isHidden = false
        window = w
    }
}
/// Touches that land on nothing of its own go to the windows below.
final class PassthroughWindow: UIWindow {
    override func hitTest(_ point: CGPoint, with event: UIEvent?) -> UIView? {
        // (a window with nothing of its own under the touch answers itself: that touch is for the windows below too)
        guard let hit = super.hitTest(point, with: event), hit !== self, hit !== rootViewController?.view else { return nil }
        return hit
    }
}
final class PassthroughView: UIView {
    override func hitTest(_ point: CGPoint, with event: UIEvent?) -> UIView? {
        let hit = super.hitTest(point, with: event)
        return hit === self ? nil : hit
    }
}

struct InviteBanner: View {
    let hub: ClubHub
    let invite: HubInvite
    var body: some View {
        TimelineView(.periodic(from: .now, by: 0.5)) { _ in
            VStack(spacing: 10) {
                HStack(spacing: 12) {
                    Text(String(invite.from.name.prefix(1)).uppercased()).font(IslandUI.font(20, bold: true)).foregroundStyle(IslandUI.navy)
                        .frame(width: 46, height: 46).background(Circle().fill(IslandUI.lime))
                    VStack(alignment: .leading, spacing: 2) {
                        Text(invite.from.name).font(IslandUI.font(17, bold: true))
                        Text("invites you to \(invite.sportTitle) · Online").font(IslandUI.font(13)).foregroundStyle(.white.opacity(0.75))
                    }
                    Spacer(minLength: 0)
                    if hub.invites.count > 1 { Text("+\(hub.invites.count - 1)").font(IslandUI.font(13, bold: true)).padding(.horizontal, 8).padding(.vertical, 3).background(.white.opacity(0.15), in: Capsule()) }
                }
                HStack(spacing: 10) {
                    Button { hub.accept(invite) } label: {
                        Text("Join").font(IslandUI.font(17, bold: true)).foregroundStyle(IslandUI.navy).frame(maxWidth: .infinity, minHeight: 44).background(IslandUI.lime, in: Capsule())
                    }.accessibilityIdentifier("invite-banner-join")
                    Button { hub.snooze(invite) } label: {
                        Text("Not now").font(IslandUI.font(16, bold: true)).frame(maxWidth: .infinity, minHeight: 44).background(.white.opacity(0.14), in: Capsule())
                    }.accessibilityIdentifier("invite-banner-later")
                }
                GeometryReader { g in
                    Capsule().fill(.white.opacity(0.15)).overlay(alignment: .leading) { Capsule().fill(IslandUI.lime).frame(width: g.size.width * hub.remaining(invite)) }
                }.frame(height: 4)
            }
            .padding(14)
            .background(Color(hex: "16294A").opacity(0.97), in: RoundedRectangle(cornerRadius: 22, style: .continuous))
            .overlay(RoundedRectangle(cornerRadius: 22, style: .continuous).strokeBorder(.white.opacity(0.2), lineWidth: 1.5))
            .shadow(color: .black.opacity(0.35), radius: 14, y: 6)
        }
        .foregroundStyle(.white).preferredColorScheme(.dark)
        .contentShape(RoundedRectangle(cornerRadius: 22))
        .onTapGesture { hub.hubOpen = true }
        .accessibilityElement(children: .contain).accessibilityIdentifier("invite-banner")
    }
}

private struct JoiningPill: View {
    let hub: ClubHub
    let who: String
    var body: some View {
        HStack(spacing: 10) {
            ProgressView().tint(IslandUI.navy)
            Text("Joining \(who)…").font(IslandUI.font(15, bold: true))
            Button("Cancel") { hub.cancelJoin() }.font(IslandUI.font(14, bold: true)).padding(.leading, 6)
        }
        .foregroundStyle(IslandUI.navy).padding(.horizontal, 16).frame(minHeight: 44).background(IslandUI.lime, in: Capsule())
        .accessibilityIdentifier("club-joining")
    }
}

private struct NoticePill: View {
    let hub: ClubHub
    var body: some View {
        Text(hub.notice).font(IslandUI.font(14, bold: true)).foregroundStyle(IslandUI.navy).multilineTextAlignment(.center)
            .padding(.horizontal, 16).padding(.vertical, 10).background(IslandUI.lime, in: RoundedRectangle(cornerRadius: 16, style: .continuous))
            .task(id: hub.notice) { try? await Task.sleep(for: .seconds(4)); hub.clearNotice() }
            .accessibilityIdentifier("club-notice")
    }
}
