import SwiftUI
import SceneKit

// The sports-arcade front end around the tennis screens: title, main menu, game select, each
// sport's hub, character, settings, how-to, the golf lesson, connecting a screen, and loading.
// Laid out for the 1280×720 TV canvas, or stacked for the phone held upright (`compact`).


// MARK: - Main menu

// A self-contained lobby design system, independent of the legacy arcade screens.


// MARK: - Game select


// MARK: - A sport's hub


// MARK: - Exhibition


// MARK: - Character


// MARK: - Settings

struct SettingsScreen: View {
    let menu: TennisMenu
    let compact: Bool
    var body: some View {
        let s = SportsSession.shared
        let p = s.players.indices.contains(s.playerIndex) ? s.players[s.playerIndex] : nil
        VStack(alignment: .leading, spacing: compact ? 10 : 12) {
            ArcadeText(text: "SETTINGS", size: compact ? 34 : 46, top: .white, bottom: Arcade.sky)
            ScrollView(.horizontal, showsIndicators: false) {
                HStack(spacing: 10) {
                    ForEach(SettingsTab.allCases, id: \.self) { tab in
                        let id = "tab-\(tab.rawValue)"
                        let current = menu.settingsTab == tab
                        Text(tab.title.uppercased()).font(Arcade.font(compact ? 14 : 17)).tracking(1)
                            .foregroundStyle(current ? Arcade.navyDeep : .white)
                            .padding(.horizontal, 16).padding(.vertical, 8)
                            .background(Capsule().fill(current ? AnyShapeStyle(LinearGradient(colors: [Arcade.gold, Arcade.goldDeep], startPoint: .top, endPoint: .bottom)) : AnyShapeStyle(Arcade.navyDeep.opacity(0.7))))
                            .focusGlow(menu.isFocused(id), corner: 30)
                            .onTapGesture { menu.tap(id) }
                    }
                }.padding(.vertical, 8).padding(.horizontal, 4)
            }
            VStack(spacing: 10) {
                ForEach(TennisMenu.settingsRows(menu.settingsTab), id: \.self) { id in
                    let (label, value) = Self.describe(id, s, p)
                    ChoiceRow(label: label, value: value, focused: menu.isFocused(id), compact: compact) { menu.tap(id) }
                }
            }
            if !menu.notice.isEmpty {
                Text(menu.notice).font(Arcade.font(compact ? 14 : 17, .bold)).foregroundStyle(Arcade.gold)
            }
            Spacer(minLength: 0)
            HStack {
                ArcadeButton(title: "Back", icon: "chevron.left", focused: menu.isFocused("back"),
                             top: Arcade.skyDeep, bottom: Arcade.navy, size: compact ? 20 : 22) { menu.tap("back") }
                Spacer()
                if !compact { HintBar() }
            }
        }
        .padding(compact ? 18 : 36)
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
    }

    static func describe(_ id: String, _ s: SportsSession, _ p: Player?) -> (String, String) {
        switch id {
        case "level": ("Training & exhibition coach", TennisMenu.trainingLevels.first { abs($0.difficulty - s.tennisDifficulty) < 0.01 }?.name ?? "Standard")
        case "coaching": ("Coaching tips in matches", s.coachingTips ? "On" : "Off")
        case "resetTips": ("Show every coaching tip again", "Reset")
        case "resetProgress": ("Reset tutorials & campaign", TennisMenu.shared.confirmingReset ? "Press again" : "Reset…")
        case "controls": ("Controls", s.touch ? "Touch" : "Swing the phone")
        case "range": ("Step to cross court", "\(Int(s.travel * 100)) cm")
        case "hand": ("Plays", p?.handedness == .left ? "Left-handed" : "Right-handed")
        case "relock": ("Court direction", "Set again")
        case "timing": ("Swing timing check", "Run next match")
        case "howto":
            ("How to connect a TV or Mac", SportsDisplays.displayKind == .mac ? "Mac connected" : SportsDisplays.displayKind == .tv ? "TV connected" : "Open")
        case "fps": ("120 fps on the phone", s.highFrameRate ? "On" : "Off")
        case "overscan": ("Screen edge margin", s.overscan == 0 ? "None" : "\(Int(s.overscan * 100))%")
        case "sound": ("Sound", s.sound ? "On" : "Off")
        case "haptics": ("Haptics", s.haptics ? "On" : "Off")
        case "plazaMenus": ("Plaza menus on the TV", HubSession.shared.plazaMenus ? "On" : "Off")
        case "bigText": ("Larger text on the phone remote", s.bigText ? "On" : "Off")
        case "reduceMotion": ("Reduce motion (still b-roll)", s.reduceMotion ? "On" : "Off")
        case "classic": ("Classic options menu", "Open")
        case "bench": ("Frame-time benchmark", "Info")
        default: (id, "")
        }
    }
}

// MARK: - How to play, and the golf lesson


// MARK: - Connect a screen (phone only)


// MARK: - Loading

/// Ten short tips per sport, including controls, identity and game-night etiquette.
enum PartyLoadingTips {
    static func tips(for sport: Sport) -> [String] {
        let shared = [
            "Leave enough room for a full swing.",
            "Pause before passing the phone.",
            "Your outfit is saved in the Locker.",
            "Play on a TV for a wider view.",
            "Adjust sound and haptics in Settings.",
            "You can change your look between matches."
        ]
        return shared + (sport == .golf ? [
            "A smooth swing beats a frantic one.",
            "Check your aim before you take the shot.",
            "Give the golfer a little quiet before the swing.",
            "Use a shorter swing for a short putt."
        ] : [
            "Tap Toss, then time your serve swing.",
            "Meet the ball in front of you for a clean return.",
            "Aim for open court instead of swinging harder.",
            "Tap Dive to reach a wide ball."
        ])
    }
}

@MainActor enum PresentationPortraits {
    private static let cache = NSCache<NSString, UIImage>()
    static func key(_ player: Player, sport: Sport) -> String {
        let encoder = JSONEncoder(); encoder.outputFormatting = [.sortedKeys]
        guard let data = try? encoder.encode(player), var appearance = (try? JSONSerialization.jsonObject(with: data)) as? [String: Any] else { return "unavailable" }
        appearance.removeValue(forKey: "id"); appearance.removeValue(forKey: "name")
        return "v1|\(sport.rawValue)|" + ((try? JSONSerialization.data(withJSONObject: appearance, options: .sortedKeys))?.base64EncodedString() ?? "unavailable")
    }
    static func image(_ player: Player, sport: Sport) -> UIImage? {
        let key = key(player, sport: sport) as NSString
        if let image = cache.object(forKey: key) { return image }
        let coordinator = CharacterModelPreview.Coordinator(cameraDistance: 3.8)
        coordinator.update(player, sport: sport)
        guard let root = coordinator.hero, let asset = MatchHero.asset(female: player.standardFemale, golf: sport == .golf),
              let rig = HeroRig(root: root, asset: asset),
              let clip = rig.data.clips.first(where: { $0.key == "wave" || $0.key == "bringIt" })?.value else { return nil }
        rig.attach(); rig.apply(clip.pose(at: clip.length * 0.35, loop: false))
        rig.data.applyMorphWeights(clip.morphWeights(at: clip.length * 0.35, loop: false), to: root)
        coordinator.camera?.position = SCNVector3(0, 0.95, 3.8)
        coordinator.camera?.look(at: SCNVector3(0, 0.9, 0))
        let renderer = SCNRenderer(device: nil, options: nil)
        renderer.scene = coordinator.scene; renderer.pointOfView = coordinator.camera
        let image = renderer.snapshot(atTime: 0, with: CGSize(width: 320, height: 320), antialiasingMode: .multisampling4X)
        cache.countLimit = 24; cache.setObject(image, forKey: key)
        return image
    }
}

private struct PresentationRosterCard: View {
    let name: String
    let player: Player?
    let sport: Sport
    let loaded: Bool?
    let compact: Bool
    @State private var portrait: UIImage?
    private var cacheKey: String { player.map { PresentationPortraits.key($0, sport: sport) } ?? name }
    var body: some View {
        VStack(spacing: 4) {
            ZStack {
                RoundedRectangle(cornerRadius: 22).fill(Club.lagoonDeep.opacity(0.72))
                if let portrait { Image(uiImage: portrait).resizable().scaledToFit() }
                else { Image(systemName: "person.fill").resizable().scaledToFit().foregroundStyle(IslandUI.lime.opacity(0.8)).padding(26) }
            }.frame(height: compact ? 140 : 245)
            HStack(spacing: 6) {
                Text(name).font(IslandUI.font(compact ? 19 : 26, bold: true)).lineLimit(1).minimumScaleFactor(0.75)
                if loaded == true { Image(systemName: "checkmark.circle.fill").foregroundStyle(IslandUI.lime) }
            }
        }.accessibilityElement(children: .ignore).accessibilityLabel("\(name)\(loaded == true ? ", loaded" : "")")
        .task(id: cacheKey) {
            portrait = nil
            // Let the cover paint before allocating an offscreen hero render.
            try? await Task.sleep(for: .milliseconds(120))
            guard !Task.isCancelled, let player else { return }
            portrait = PresentationPortraits.image(player, sport: sport)
        }
    }
}

struct LoadingScreen: View {
    let menu: TennisMenu
    let compact: Bool
    private var session: SportsSession { .shared }
    private var service: MultiplayerService { menu.online.service }
    private var sport: Sport { service.lobby?.phase == .loading ? (service.lobby?.sport == .golf ? .golf : .tennis) : menu.launch?.sport ?? Sport(rawValue: session.sport) ?? .tennis }
    private var venueID: String { service.lobby?.phase == .loading ? service.lobby?.venue ?? (sport == .golf ? session.golfCourse : session.tennisVenue) : (sport == .golf ? session.golfCourse : session.tennisVenue) }
    private var venue: String { sport == .golf ? (GolfCourseChoice(rawValue: venueID)?.title ?? "Cliffside") : (TennisVenueChoice(rawValue: venueID)?.title ?? "Tropical Resort") }
    private var modeLine: String {
        if sport == .golf { return "Golf · Stroke Play · \(venue)" }
        if menu.launch?.mode == .training { return "Tennis · Practice · \(venue)" }
        return "Tennis · Singles · \(venue)"
    }
    var body: some View {
        GeometryReader { geometry in
            ZStack {
                Club.lagoonDeep
                VStack(spacing: compact ? 18 : 24) {
                    Text("MOTION CLUB · BETA").font(IslandUI.font(12, bold: true)).tracking(1.5)
                    Text(venue).font(IslandUI.font(compact ? 32 : 58, bold: true))
                    Text(modeLine).font(IslandUI.font(compact ? 15 : 23)).multilineTextAlignment(.center)
                    GameplayPreview(sport: sport, venue: venueID)
                        .aspectRatio(16 / 9, contentMode: .fit).clipShape(RoundedRectangle(cornerRadius: 18))
                    Spacer(minLength: 0)
                    loadingStatus
                    HStack(spacing: 16) {
                        if session.multiplayerMatchID != nil || service.lobby?.phase == .loading {
                            if service.loadingNeedsDecision {
                                IslandAction(title: "Keep waiting", focused: menu.isFocused("net-wait"), primary: true, compact: true) { menu.tap("net-wait") }
                            }
                            IslandAction(title: "Leave match", focused: menu.isFocused("net-leave"), compact: true) { menu.tap("net-leave") }
                        } else {
                            IslandAction(title: "Leave match", focused: menu.isFocused("loadingBack"), compact: true) { menu.tap("loadingBack") }
                            if session.loading.isStalled || session.loading.phase == .failed {
                                IslandAction(title: "Retry", focused: menu.isFocused("loadingRetry"), primary: true, compact: true) { menu.tap("loadingRetry") }
                            }
                        }
                    }
                }.padding(compact ? 24 : 64).frame(maxWidth: .infinity)
            }.foregroundStyle(.white).clipped()
        }.ignoresSafeArea().accessibilityIdentifier("presentation-loading-card")
    }
    private var roster: some View {
        let competitors = session.multiplayerMatchID != nil || service.lobby?.phase == .loading ? service.lobby?.competitors ?? [] : []
        return Group {
            if !competitors.isEmpty {
                LazyVGrid(columns: Array(repeating: GridItem(.flexible(), spacing: 12), count: compact ? min(2, competitors.count) : competitors.count), spacing: 12) {
                    ForEach(competitors, id: \.id) { participant in
                        PresentationRosterCard(name: participant.name, player: participant.loadout == nil ? nil : participant.lobbyPlayer,
                                               sport: sport, loaded: participant.loaded, compact: compact)
                    }
                }
            } else {
                HStack(alignment: .center, spacing: 12) {
                    PresentationRosterCard(name: menu.player?.name ?? "You", player: menu.player, sport: sport, loaded: nil, compact: compact)
                    if sport == .tennis {
                        Text("VS").font(IslandUI.font(compact ? 16 : 26, bold: true)).foregroundStyle(IslandUI.lime)
                        PresentationRosterCard(name: menu.loadingOpponent?.name ?? "Practice", player: menu.loadingOpponent.map { RivalLooks.player(for: $0) }, sport: sport, loaded: nil, compact: compact)
                    }
                }
            }
        }.frame(maxWidth: compact ? 440 : 1040)
    }
    private var loadingStatus: some View {
        VStack(spacing: 10) {
            HStack(spacing: 10) {
                if session.loading.showsActivity { ProgressView().tint(IslandUI.lime) }
                if session.loading.phase == .transitioning { Image(systemName: "checkmark.circle.fill").foregroundStyle(IslandUI.lime) }
                Text(session.loading.statusText).font(IslandUI.font(compact ? 17 : 23, bold: true)).multilineTextAlignment(.center)
            }
            if let tip = session.loading.tip {
                VStack(spacing: 6) {
                    Text((session.loading.tipKind ?? "Tip").uppercased()).font(IslandUI.font(12, bold: true)).tracking(1.6).foregroundStyle(IslandUI.lime)
                    Text(tip).font(IslandUI.font(compact ? 16 : 22, bold: true)).multilineTextAlignment(.center).fixedSize(horizontal: false, vertical: true)
                }
                .padding(.horizontal, 20).padding(.vertical, 12).frame(maxWidth: compact ? .infinity : 780)
                .background(.white.opacity(0.1), in: RoundedRectangle(cornerRadius: 16, style: .continuous))
                .accessibilityElement(children: .combine).accessibilityIdentifier("loading-tip")
            }
            if session.loading.elapsed >= 10, let measured = session.loading.sceneProgress, measured < 1 {
                ProgressView("Venue loading", value: measured).tint(IslandUI.lime).frame(maxWidth: 350)
            }
        }.accessibilityIdentifier("presentation-loading-status")
    }
    static func label(_ launch: MenuLaunch, _ opponent: TennisOpponent?) -> String {
        launch.sport == .golf ? (GolfCourseChoice(rawValue: SportsSession.shared.golfCourse)?.title ?? "Cliffside") : (TennisVenueChoice(rawValue: SportsSession.shared.tennisVenue)?.title ?? "Tropical Resort")
    }
}


extension TennisMenu {
    /// A readable name for a menu item, for the phone remote.
    static func label(for id: String) -> String {
        if id.hasPrefix("home-emote-") { return TennisMenu.homeEmoteName(String(id.dropFirst(11))) }
        if id.hasPrefix("sport-"), let s = Sport(rawValue: String(id.dropFirst(6))) { return s.playable ? s.title.capitalized : "\(s.title.capitalized) · coming soon" }
        if id.hasPrefix("tab-"), let t = SettingsTab(rawValue: String(id.dropFirst(4))) { return "Settings · \(t.title)" }
        if id.hasPrefix("lk-") { return lockerLabel(id) }
        if id.hasPrefix("map-"), let v = TennisVenueChoice(rawValue: String(id.dropFirst(4))) { return v.title }
        if id.hasPrefix("rival"), let r = Int(id.dropFirst(5)) {
            let o = TennisCampaign.draw[r]
            return TennisCampaign.shared.unlocked(r) ? "Exhibition · \(o.name)" : "Exhibition · Locked"
        }
        switch id {
        case "homeCampaign": return "Campaign"
        case "store": return "Store · Coming soon"
        case "campaignPlay": return "Play Round"
        case "quickOpponent": return "Opponent"
        case "quickDifficulty": return "Difficulty"
        case "quickLength": return "Match length"
        case "quickStart": return "Start Match"
        case "homePlay": return "Play with Friends"
        case "partySolo": return "Single Player"
        case "partyMultiplayer": return "Multiplayer"
        case "partyOnline": return "Online"
        case "partyLocal": return "Local"
        case "partyNearby": return "Join a Friend"
        case "partyLocalGolf": return "Golf · Pass the Phone"
        case "partyLocalTennis": return "Tennis · Two Phones"
        case "localPlayers2", "localPlayers3", "localPlayers4": return "\(id.dropFirst("localPlayers".count)) Players"
        case "onlineQuick": return "Quick Match"
        case "homeInvite": return "Play with Friends"
        case "homeEmotes": return "Emotes"
        case "betaFeedback": return "Beta Feedback"
        case "loadingBack": return "Back to Home"; case "loadingRetry": return "Retry loading"
        case "play": return "Play"; case "character": return "Locker"; case "settings": return "Settings"
        case "howto": return "How to play"; case let g where g.hasPrefix("guide-"): return GuideDeck(rowID: g)?.title ?? "Guide"; case "tutorial": return "Tutorial"; case "replayTutorial": return "Replay tutorial"
        case "campaign": return "Island Circuit"; case "training": return "Training court"; case "exhibition": return "Exhibition"
        case "round": return "Play Cliffside"; case "golfCampaign": return "Golf campaign · coming soon"; case "golfTraining": return "Driving range · coming soon"
        case "nextPage", "nextCard": return "Next"; case "prev": return "Previous"; case "randomize": return "Randomize"; case "reset": return "Kit colours"
        default:
            let s = SportsSession.shared
            let p = s.players.indices.contains(s.playerIndex) ? s.players[s.playerIndex] : nil
            let (label, value) = SettingsScreen.describe(id, s, p)
            if label != id { return "\(label): \(value)" }
            switch id {
            case "body": return "Body: \(p?.standardFemale == true ? "Female" : "Male")"
            case "skin": return "Skin: \(TennisMenu.skinTones[p?.standardSkin ?? 2])"
            case "shirt": return "Shirt: \((p?.outfitName("shirt") ?? "Kit colour"))"
            case "shorts": return "\(TennisMenu.shared.lockerBottomName): \((p?.outfitName("shorts") ?? "Kit colour"))"
            case "accent": return "Bands: \((p?.outfitName("accent") ?? "Kit colour"))"
            case "racket": return "Racket: \((p?.outfitName("racket") ?? "Kit colour"))"
            default: return id.prefix(1).uppercased() + id.dropFirst()
            }
        }
    }
}
