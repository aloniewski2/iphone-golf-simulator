import SwiftUI

/// The phone while the Plaza is on the TV (PLAN_MenuHub_WalkableWorld §6, Explore): an analog stick with magnitude (a short push
/// walks, a full push runs, holding it full sprints), A / B, ☰ (go anywhere, or the classic menu), Emote and Party, and a strip
/// saying what you are standing at on the TV. Dark and quiet like the remote: the player's eyes are on the TV.
struct HubControllerView: View {
    @Bindable var hub = HubSession.shared
    @State private var quickMenu = false
    @State private var emotes = false
    private var session: SportsSession { .shared }

    var body: some View {
        if let bay = hub.station, HubSession.partyBays.contains(bay), hub.party != nil { HubPartyBayPanel(hub: hub) }
        else if hub.panelOpen { HubStationPanel(hub: hub) } else { explore }
    }

    private var explore: some View {
        VStack(spacing: 16) {
            header
            onTV
            // the whole middle of the phone is the stick: a thumb put down anywhere there walks the hero, eyes on the TV
            HubStick { x, y, mag in hub.setStick(x: x, y: y, magnitude: mag) }
                .frame(maxWidth: .infinity, minHeight: HubStick.diameter + 10, maxHeight: .infinity)
                .opacity(hub.phase == .live ? 1 : 0.4)
                .disabled(hub.phase != .live)
            Text(hub.phase == .booting ? "Watch the TV · the stick wakes up at 100%" : "Thumb down anywhere here · short push walks · full push runs · hold to sprint")
                .font(IslandUI.font(13, bold: true)).foregroundStyle(.white.opacity(0.55)).multilineTextAlignment(.center)
            HStack(alignment: .center, spacing: 22) {
                HubRoundButton(label: "B", caption: "Back", size: 74) { hub.pressB() }
                HubRoundButton(label: "A", caption: aCaption, size: 104, filled: true) { hub.pressA() }
                    .opacity(hub.zone.map { $0.kind != .door } ?? false ? 1 : 0.55)
                HubRoundButton(label: "☺", caption: "Emote", size: 74) { emotes = true }
            }
            HStack(spacing: 12) {
                pill("line.3.horizontal", "Menu", id: "hub-menu") { quickMenu = true }
                // one button for everything social: friends online, invites, your party, adding friends
                pill("person.2.fill", friendsTitle, id: "hub-party") { hub.inviteFriends() }
                    .overlay(alignment: .topTrailing) {
                        if ClubHub.shared.unreadInvites > 0 {
                            Text("\(ClubHub.shared.unreadInvites)").font(IslandUI.font(12, bold: true)).foregroundStyle(IslandUI.navy)
                                .frame(minWidth: 22, minHeight: 22).background(IslandUI.lime, in: Circle()).offset(x: 4, y: -6)
                                .accessibilityIdentifier("hub-invite-badge")
                        }
                    }
            }
        }
        .foregroundStyle(.white)
        .padding(.horizontal, 24).padding(.vertical, 18)
        .background(LinearGradient(colors: [Color(hex: "16294A"), IslandUI.dark], startPoint: .top, endPoint: .bottom).ignoresSafeArea())
        .preferredColorScheme(.dark)
        .sheet(isPresented: $quickMenu) { HubQuickMenu(hub: hub, dismiss: { quickMenu = false }).presentationDetents([.medium, .large]) }
        .confirmationDialog("Emote", isPresented: $emotes, titleVisibility: .visible) {
            ForEach(currentEmotes, id: \.self) { id in Button(EmoteCatalog.name(id)) { hub.emote(id) } }
        }
        .accessibilityIdentifier("hub-controller")
    }

    private var friendsTitle: String { hub.party.map { "Friends · \($0.participants.count) here" } ?? "Friends" }
    private var currentEmotes: [String] {
        session.players.indices.contains(session.playerIndex) ? session.players[session.playerIndex].equippedEmotes : EmoteCatalog.defaults
    }
    private var aCaption: String {
        switch hub.zone?.kind { case .station: "Use"; case .bay: "Sit"; default: "A" }
    }

    private var header: some View {
        HStack {
            HStack(spacing: 8) {
                Circle().fill(hub.phase == .live ? IslandUI.lime : .orange).frame(width: 10, height: 10)
                    .shadow(color: hub.phase == .live ? IslandUI.lime : .orange, radius: 5)
                Text(hub.phase == .live ? "Connected to TV" : "Loading on the TV").font(IslandUI.font(15, bold: true))
                    .accessibilityIdentifier("hub-connection")
            }
            .padding(.horizontal, 14).padding(.vertical, 8).background(.white.opacity(0.1), in: Capsule())
            Spacer()
            Text(Self.placeName(hub.place)).font(IslandUI.font(13, bold: true)).tracking(1.4).foregroundStyle(IslandUI.lime)
                .accessibilityIdentifier("hub-place")
        }
    }

    private var onTV: some View {
        VStack(alignment: .leading, spacing: 4) {
            Text("ON THE TV · \(Self.placeName(hub.place))").font(IslandUI.font(12, bold: true)).tracking(1.6).foregroundStyle(IslandUI.lime)
            Text(hub.phase == .booting ? hub.loadStage : hub.zone?.prompt ?? Self.idleHint(hub.place))
                .font(IslandUI.font(session.bigText ? 28 : 22, bold: true)).lineLimit(2).minimumScaleFactor(0.6)
                .accessibilityIdentifier("hub-prompt")
            if hub.phase == .booting {
                // the TV's loading bar, small
                HStack(spacing: 12) {
                    Capsule().fill(.white.opacity(0.16)).frame(height: 10)
                        .overlay(alignment: .leading) { GeometryReader { g in Capsule().fill(IslandUI.lime).frame(width: max(10, g.size.width * hub.loadProgress)) } }
                    Text("\(Int((hub.loadProgress * 100).rounded()))%").font(Club.title(20)).foregroundStyle(IslandUI.lime).frame(width: 56, alignment: .trailing)
                }
                .animation(.easeOut(duration: 0.3), value: hub.loadProgress)
                .accessibilityElement(children: .ignore).accessibilityLabel("Loading \(Int((hub.loadProgress * 100).rounded())) percent")
                .accessibilityIdentifier("hub-loading-bar")
            }
            if let detail = hub.zone?.detail, !detail.isEmpty, hub.zone?.kind != .door {
                Text(detail).font(IslandUI.font(14, bold: false)).foregroundStyle(.white.opacity(0.7))
            }
            if !hub.notice.isEmpty { Text(hub.notice).font(IslandUI.font(13, bold: true)).foregroundStyle(IslandUI.lime).padding(.top, 2) }
        }
        .frame(maxWidth: .infinity, alignment: .leading).padding(.horizontal, 18).padding(.vertical, 14)
        .background(.white.opacity(0.08), in: RoundedRectangle(cornerRadius: 20, style: .continuous))
        .overlay(RoundedRectangle(cornerRadius: 20, style: .continuous).strokeBorder(.white.opacity(0.14), lineWidth: 1.5))
    }

    private func pill(_ icon: String, _ title: String, id: String, action: @escaping () -> Void) -> some View {
        Button(action: action) {
            Label(title, systemImage: icon).font(IslandUI.font(16, bold: true)).foregroundStyle(.white)
                .frame(maxWidth: .infinity, minHeight: 46)
                .background(.white.opacity(0.09), in: Capsule())
                .overlay(Capsule().strokeBorder(.white.opacity(0.22), lineWidth: 2))
        }.buttonStyle(.plain).accessibilityIdentifier(id)
    }

    static func placeName(_ place: String) -> String {
        switch place {
        case "locker": "LOCKER ROOM"; case "clubhouse": "CLUBHOUSE"; case "play": "PLAY HALL"
        case "tennis": "TENNIS"; case "golf": "GOLF"; default: "THE PLAZA"
        }
    }
    static func idleHint(_ place: String) -> String {
        switch place {
        case "plaza": "Walk to LOCKER, CLUBHOUSE or PLAY"
        case "play": "Tennis on the left · Golf on the right"
        case "tennis", "golf": "Walk into a bay to play"
        default: "Walk up to something to use it"
        }
    }
}

/// The TV while the Plaza boots (loading screen D, picked 2026-10-11): MOTION CLUB over navy stripes, a tennis ball rolling along
/// a court line as the bar, what is loading and a tip. The bar is HubSession.loadProgress (real progress: the runtime, the scene,
/// the plaza's build); at 100 % it holds a moment and fades into the arrival fly-in. Drawn on the 1280×720 TV canvas.
struct HubLoadingScreen: View {
    let hub: HubSession
    static let tips = ["A short push on the stick walks. Push it all the way to run.",
                       "Walk up to anything in the Plaza and press A to use it.",
                       "Sit on a bench in the PLAY hall to start a match.",
                       "Invite friends from your phone: they walk the Plaza with you.",
                       "Change your look at the racks in the Locker."]
    static let court = Color(hex: "3E57A8"), stripe = Color(hex: "16305A")
    var body: some View {
        let p = hub.loadProgress
        ZStack {
            Canvas { ctx, size in
                ctx.fill(Path(CGRect(origin: .zero, size: size)), with: .color(Club.ink))
                ctx.translateBy(x: size.width / 2, y: size.height / 2); ctx.rotate(by: .degrees(-28))
                let span = hypot(size.width, size.height)
                var x = -span
                while x < span { ctx.fill(Path(CGRect(x: x + 90, y: -span, width: 90, height: span * 2)), with: .color(Self.stripe)); x += 180 }
            }
            VStack(spacing: 6) {
                Text("MOTION CLUB").font(Club.display(112, condensed: false)).foregroundStyle(Club.sun)
                    .shadow(color: Club.lagoonDeep, radius: 0, y: 8)
                Text("PARTY SPORTS · BETA").font(IslandUI.font(18, bold: true)).tracking(8).foregroundStyle(Club.cream)
            }
            .position(x: 640, y: 160)
            track(p).frame(width: 1040, height: 120).position(x: 640, y: 420)
            VStack(spacing: 14) {
                Text(hub.loadStage).font(Club.title(40)).foregroundStyle(Club.cream).accessibilityIdentifier("hub-loading-stage")
                TimelineView(.periodic(from: .now, by: 4)) { context in
                    let tip = Self.tips[Int(context.date.timeIntervalSinceReferenceDate / 4) % Self.tips.count]
                    (Text("TIP   ").foregroundStyle(Club.sun).font(IslandUI.font(19, bold: true)) + Text(tip).foregroundStyle(Club.cream.opacity(0.85)).font(IslandUI.font(19)))
                }
            }
            .position(x: 640, y: 590)
        }
        .frame(width: 1280, height: 720)
        .clipped()
        .accessibilityIdentifier("hub-loading")
    }

    /// The court-line bar: the ball rolls to the progress, a lime line behind it and a few bounce marks.
    private func track(_ p: Double) -> some View {
        let w = 1028.0, x = 6 + w * p
        return ZStack(alignment: .topLeading) {
            RoundedRectangle(cornerRadius: 18).fill(Self.court)
            Rectangle().fill(Club.cream.opacity(0.85)).frame(width: w, height: 4).position(x: 520, y: 60)
            Rectangle().fill(Club.cream.opacity(0.85)).frame(width: 4, height: 108).position(x: 520, y: 60)
            Capsule().fill(Club.sun).frame(width: max(14, x - 6), height: 14).position(x: 6 + max(14, x - 6) / 2, y: 60)
            ForEach(0..<3) { i in
                Circle().fill(Club.sun).opacity(0.2 + Double(i) * 0.2).frame(width: 14, height: 14)
                    .position(x: 6 + w * max(0, p - [0.09, 0.06, 0.03][i]), y: i % 2 == 0 ? 43 : 31)
            }
            HubTennisBall().frame(width: 76, height: 76).rotationEffect(.degrees(p * 1080)).position(x: x, y: 60)
            Text("\(Int((p * 100).rounded()))%").font(Club.title(26)).foregroundStyle(Club.ink)
                .padding(.horizontal, 14).padding(.vertical, 6).background(Club.cream, in: RoundedRectangle(cornerRadius: 12))
                .position(x: x, y: -34)
        }
        .overlay(RoundedRectangle(cornerRadius: 18).strokeBorder(Club.cream, lineWidth: 6))
        .background(RoundedRectangle(cornerRadius: 18).fill(Club.lagoonDeep).offset(y: 10))
        .animation(.easeOut(duration: 0.3), value: p)
        .accessibilityElement(children: .ignore).accessibilityLabel("Loading \(Int((p * 100).rounded())) percent")
    }
}

/// The loading bar's ball: lime felt, white seams, a dark outline so it reads on the court blue.
struct HubTennisBall: View {
    var body: some View {
        Canvas { ctx, size in
            let r = min(size.width, size.height) / 2, c = CGPoint(x: size.width / 2, y: size.height / 2)
            let ball = Path(ellipseIn: CGRect(x: c.x - r + 2, y: c.y - r + 2, width: r * 2 - 4, height: r * 2 - 4))
            ctx.fill(ball, with: .color(Club.sun)); ctx.stroke(ball, with: .color(Club.lagoonDeep), lineWidth: 4)
            var seams = Path()
            seams.move(to: CGPoint(x: c.x - r * 0.68, y: c.y - r * 0.53))
            seams.addCurve(to: CGPoint(x: c.x - r * 0.68, y: c.y + r * 0.53), control1: CGPoint(x: c.x - r * 0.15, y: c.y - r * 0.25), control2: CGPoint(x: c.x - r * 0.15, y: c.y + r * 0.25))
            seams.move(to: CGPoint(x: c.x + r * 0.68, y: c.y - r * 0.53))
            seams.addCurve(to: CGPoint(x: c.x + r * 0.68, y: c.y + r * 0.53), control1: CGPoint(x: c.x + r * 0.15, y: c.y - r * 0.25), control2: CGPoint(x: c.x + r * 0.15, y: c.y + r * 0.25))
            ctx.stroke(seams, with: .color(Club.cream), style: StrokeStyle(lineWidth: 5, lineCap: .round))
        }
        .accessibilityHidden(true)
    }
}

/// An analog thumbstick that floats: direction and how far it is pushed (0…1), free in every direction. The player's eyes are on
/// the TV, so the stick is not a target to hit: a thumb put down anywhere in its area is the stick's centre, and the push is
/// measured from there (the drawn stick moves under the thumb, kept whole on the screen). At rest it sits in the middle.
/// Sends on every drag change; zero on release.
struct HubStick: View {
    let send: (Double, Double, Double) -> Void
    /// The drawn stick: the base this wide, the knob travelling to its rim.
    static let diameter: CGFloat = 270
    @State private var thumb: CGSize = .zero
    /// Where the thumb came down (nil at rest).
    @State private var origin: CGPoint?
    @State private var running = false
    var body: some View {
        GeometryReader { g in
            let radius = Self.diameter / 2
            let travel = radius - 44
            let rest = CGPoint(x: g.size.width / 2, y: g.size.height / 2)
            // the drawn base: under the thumb, but never cut off by the area's edge
            let base = origin.map { CGPoint(x: min(max($0.x, radius), max(radius, g.size.width - radius)),
                                            y: min(max($0.y, radius), max(radius, g.size.height - radius))) } ?? rest
            ZStack {
                Color.clear.contentShape(Rectangle())
                stick(radius: radius, travel: travel).frame(width: Self.diameter, height: Self.diameter).position(base)
            }
            .gesture(DragGesture(minimumDistance: 0, coordinateSpace: .local)
                .onChanged { drag in
                    if origin == nil { origin = drag.startLocation }
                    let start = origin ?? drag.startLocation
                    let dx = drag.location.x - start.x, dy = drag.location.y - start.y
                    let distance = hypot(dx, dy)
                    let mag = min(1, distance / max(1, travel))
                    let length = min(travel, distance)
                    thumb = distance > 0 ? CGSize(width: dx / distance * length, height: dy / distance * length) : .zero
                    let nowRunning = mag >= 0.7
                    if nowRunning != running { running = nowRunning; if SportsSession.shared.haptics { UISelectionFeedbackGenerator().selectionChanged() } }
                    if distance > 0 { send(dx / distance, -dy / distance, mag) } else { send(0, 0, 0) }
                }
                .onEnded { _ in
                    withAnimation(.spring(response: 0.18, dampingFraction: 0.7)) { thumb = .zero; origin = nil }
                    running = false; send(0, 0, 0)
                })
            .accessibilityElement()
            .accessibilityIdentifier("hub-stick")
            .accessibilityLabel("Walk stick")
        }
    }

    private func stick(radius: CGFloat, travel: CGFloat) -> some View {
        ZStack {
            Circle().fill(Color(hex: "1F3B6E"))
                .overlay(Circle().stroke(Color(hex: "B4E5FF").opacity(0.55), lineWidth: 5))
                .overlay(Circle().stroke(.white.opacity(0.35), lineWidth: 1.5).padding(4))
            // the run ring: past it you run
            Circle().strokeBorder(IslandUI.lime.opacity(running ? 0.75 : 0.25), style: StrokeStyle(lineWidth: 2, dash: [5, 6]))
                .frame(width: (44 + travel * 0.7) * 2, height: (44 + travel * 0.7) * 2)
            ForEach(0..<4) { i in
                Image(systemName: "triangle.fill").font(.system(size: 12, weight: .bold)).foregroundStyle(.white.opacity(0.5))
                    .offset(y: -(radius - 16)).rotationEffect(.degrees(Double(i) * 90))
            }
            Circle().fill(LinearGradient(colors: [.white, Color(hex: "D0E9FA")], startPoint: .top, endPoint: .bottom))
                .frame(width: 88, height: 88)
                .overlay(Circle().stroke(running ? IslandUI.lime : .white, lineWidth: 3))
                .shadow(color: .black.opacity(0.45), radius: 0, y: 5)
                .offset(thumb)
        }
        .allowsHitTesting(false)
    }
}

private struct HubRoundButton: View {
    let label: String
    let caption: String
    let size: CGFloat
    var filled = false
    var action: () -> Void
    var body: some View {
        VStack(spacing: 7) {
            Button(action: action) {
                Text(label).font(IslandUI.font(size * 0.42, bold: true)).foregroundStyle(filled ? IslandUI.navy : .white)
                    .frame(width: size, height: size)
                    .background(filled ? IslandUI.lime : .clear, in: Circle())
                    .overlay(Circle().strokeBorder(filled ? .clear : .white.opacity(0.42), lineWidth: 3))
                    .shadow(color: filled ? .black.opacity(0.4) : .clear, radius: 0, y: filled ? 5 : 0)
            }.buttonStyle(.plain).accessibilityLabel(caption).accessibilityIdentifier("hub-\(caption.lowercased())")
            Text(caption.uppercased()).font(IslandUI.font(12, bold: true)).tracking(1.6).foregroundStyle(.white.opacity(0.6))
        }
    }
}

/// ☰: the Destiny-style launcher. Jump to any room, or straight to a shortcut.
struct HubQuickMenu: View {
    let hub: HubSession
    let dismiss: () -> Void
    static let places: [(id: String, title: String, icon: String)] = [
        ("plaza", "The Plaza", "sun.horizon.fill"), ("locker", "Locker Room", "tshirt.fill"), ("clubhouse", "Clubhouse", "crown.fill"),
        ("play", "PLAY Hall", "flag.checkered"), ("tennis", "Tennis room", "tennis.racket"), ("golf", "Golf room", "figure.golf")]
    /// One tap to the thing you came for (ROUTE_ALL: every old menu destination in two presses).
    static let shortcuts: [(id: String, title: String, icon: String)] = [
        ("bay-tennis-exhibition", "Quick match", "bolt.fill"), ("bay-tennis-campaign", "Island Circuit", "trophy.fill"),
        ("bay-tennis-training", "Training", "figure.tennis"), ("bay-golf-round", "Golf round", "figure.golf"),
        ("bay-tennis-online", "Play online", "globe"), ("bay-golf-pass", "Pass-the-phone golf", "iphone.and.arrow.forward"),
        ("look-mirror", "Locker", "tshirt.fill"), ("desk-settings", "Settings", "gearshape.fill"), ("screen-howto", "How to play", "questionmark.circle.fill"),
        ("board-invite", "Invite friends", "person.badge.plus")]
    var body: some View {
        NavigationStack {
            List {
                Section("Go to") {
                    ForEach(Self.places, id: \.id) { place in
                        Button { hub.go(place.id); dismiss() } label: { Label(place.title, systemImage: place.icon) }
                            .accessibilityIdentifier("hub-go-\(place.id)")
                    }
                }
                Section("Shortcuts") {
                    ForEach(Self.shortcuts, id: \.id) { s in
                        Button {
                            // inviting needs no walk to the board: the Friends hub opens at once
                            if s.id == "board-invite" { dismiss(); hub.inviteFriends(afterSheet: true) } else { hub.openStation(s.id); dismiss() }
                        } label: { Label(s.title, systemImage: s.icon) }
                            .accessibilityIdentifier("hub-use-\(s.id)")
                    }
                }
                Section {
                    // Local play has no bay of its own: pass-the-phone golf, two phones on one TV for tennis, or a nearby lobby
                    Button { dismiss(); hub.openPanel(.localChoice) } label: {
                        Label("Play together (Local)", systemImage: "person.2.wave.2.fill")
                    }.accessibilityIdentifier("hub-local")
                    Button { dismiss(); BetaFeedback.open() } label: { Label("Send feedback", systemImage: "envelope") }
                }
            }
            .navigationTitle("Menu").navigationBarTitleDisplayMode(.inline)
            .toolbar { ToolbarItem(placement: .confirmationAction) { Button("Done", action: dismiss) } }
        }
    }
}

/// A station or bay in use: the TV frames your hero; the phone shows that station's screen (the same screens as the phone menu).
struct HubStationPanel: View {
    let hub: HubSession
    var body: some View {
        VStack(spacing: 0) {
            HStack {
                Text("ON THE TV · \(HubControllerView.placeName(hub.place))").font(IslandUI.font(12, bold: true)).tracking(1.4).foregroundStyle(IslandUI.lime)
                Spacer()
                Button { hub.leaveStation() } label: {
                    Text("Done").font(IslandUI.font(16, bold: true)).foregroundStyle(IslandUI.navy)
                        .padding(.horizontal, 18).padding(.vertical, 8).background(IslandUI.lime, in: Capsule())
                }.buttonStyle(.plain).accessibilityIdentifier("hub-station-done")
            }
            .padding(.horizontal, 20).padding(.vertical, 10)
            .background(Color(hex: "16294A"))
            ZStack { Club.lagoonDeep.ignoresSafeArea(); TennisMenuScreen(compact: true) }
        }
        .foregroundStyle(.white)
        .preferredColorScheme(.dark)
        .accessibilityIdentifier("hub-station-panel")
    }
}

/// Seated in an online bay with your party (PLAN §3/§4): who is here (✓ ready), who the bench is waiting for, READY, and for the
/// host START (start anyway) and Call party. The match starts by itself when everyone sits here and is ready.
struct HubPartyBayPanel: View {
    let hub: HubSession
    private var party: MultiplayerLobby? { hub.party }
    private var me: String { hub.partyService.localID }
    var body: some View {
        VStack(alignment: .leading, spacing: 14) {
            HStack {
                Text("PARTY · \(hub.station?.contains("golf") == true ? "GOLF" : "TENNIS") · ONLINE").font(IslandUI.font(13, bold: true)).tracking(1.4).foregroundStyle(IslandUI.lime)
                Spacer()
                Button { hub.leaveStation() } label: { Text("Leave seat").font(IslandUI.font(15, bold: true)).foregroundStyle(.white).padding(.horizontal, 14).padding(.vertical, 8).background(.white.opacity(0.12), in: Capsule()) }
                    .buttonStyle(.plain).accessibilityIdentifier("hub-party-leave-seat")
            }
            Text(hub.partyWaitingFor.isEmpty ? "Everyone is on the bench" : "Waiting for " + hub.partyWaitingFor.joined(separator: ", "))
                .font(IslandUI.font(22, bold: true)).accessibilityIdentifier("hub-party-waiting")
            VStack(spacing: 8) {
                ForEach(party?.participants.filter { !$0.isGuest } ?? [], id: \.id) { p in
                    let seated = p.id == me || hub.friendBays[p.id] == hub.station
                    HStack {
                        Circle().fill(seated ? IslandUI.lime : .white.opacity(0.25)).frame(width: 12, height: 12)
                        Text(p.name + (p.id == party?.ownerID ? " · host" : "") + (p.id == me ? " (you)" : "")).font(IslandUI.font(17, bold: true))
                        Spacer()
                        Text(p.ready ? "✓ Ready" : seated ? "Seated" : "On the way").font(IslandUI.font(14, bold: true)).foregroundStyle(p.ready ? IslandUI.lime : .white.opacity(0.6))
                    }
                    .padding(12).background(.white.opacity(0.07), in: RoundedRectangle(cornerRadius: 14))
                }
            }
            Spacer(minLength: 0)
            let ready = party?.participants.first { $0.id == me }?.ready ?? false
            Button { hub.setPartyReady(!ready) } label: {
                Text(ready ? "NOT READY" : "READY").font(IslandUI.font(24, bold: true)).foregroundStyle(IslandUI.navy)
                    .frame(maxWidth: .infinity, minHeight: 62).background(ready ? Color.white : IslandUI.lime, in: Capsule())
            }.buttonStyle(.plain).accessibilityIdentifier("hub-party-ready")
            if hub.partyService.isOwner {
                HStack(spacing: 12) {
                    Button { hub.callParty() } label: { Label("Call party", systemImage: "megaphone.fill").font(IslandUI.font(16, bold: true)).frame(maxWidth: .infinity, minHeight: 48).background(.white.opacity(0.12), in: Capsule()) }
                        .buttonStyle(.plain).accessibilityIdentifier("hub-party-call")
                    Button { hub.forcePartyStart() } label: { Label("Start", systemImage: "play.fill").font(IslandUI.font(16, bold: true)).frame(maxWidth: .infinity, minHeight: 48).background(.white.opacity(0.12), in: Capsule()) }
                        .buttonStyle(.plain).disabled(hub.partyService.lobby?.canStart != true).accessibilityIdentifier("hub-party-start")
                }
            }
            if let reason = party?.startReason, !(party?.canStart ?? false) { Text(reason).font(IslandUI.font(13, bold: true)).foregroundStyle(.white.opacity(0.6)) }
        }
        .foregroundStyle(.white).padding(22)
        .background(LinearGradient(colors: [Color(hex: "16294A"), IslandUI.dark], startPoint: .top, endPoint: .bottom).ignoresSafeArea())
        .preferredColorScheme(.dark)
        .accessibilityIdentifier("hub-party-bay")
    }
}
