import SwiftUI

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

struct LoadingScreen: View {
    let menu: TennisMenu
    let compact: Bool
    @State private var tip = 0
    private var session: SportsSession { .shared }
    var body: some View {
        let launch = menu.launch ?? MenuLaunch(mode: .training)
        IslandShell(title: "Loading match", compact: compact) {
            let layout = compact ? AnyLayout(VStackLayout(spacing: 16)) : AnyLayout(HStackLayout(spacing: 60))
            layout {
                VStack(alignment: .leading, spacing: 24) {
                    Text(Self.label(launch, menu.loadingOpponent)).font(IslandUI.font(compact ? 22 : 28, bold: true)).foregroundStyle(IslandUI.navy)
                    HStack(spacing: 14) {
                        GeometryReader { bar in
                            ZStack(alignment: .leading) {
                                Capsule().fill(IslandUI.navy.opacity(0.10))
                                Capsule().fill(IslandUI.lime).frame(width: max(0, bar.size.width * session.loading.progress))
                            }
                        }.frame(height: compact ? 18 : 24)
                            .accessibilityElement().accessibilityLabel("Loading progress").accessibilityValue("\(session.loading.percent) percent")
                        Text("\(session.loading.percent)%").font(IslandUI.font(21, bold: true)).monospacedDigit().foregroundStyle(IslandUI.navy)
                    }
                    if launch.sport == .tennis { Button { session.loading.practice() } label: {
                        Label("Swing to practice", systemImage: "tennis.racket").font(IslandUI.font(compact ? 20 : 26, bold: true)).foregroundStyle(IslandUI.navy).padding(.horizontal, 20).padding(.vertical, 14).background(IslandUI.lime, in: Capsule())
                    }.buttonStyle(.plain).accessibilityLabel("Practice a swing").accessibilityHint("You can also swing your phone while loading.").accessibilityIdentifier("loading-practice") }
                    Text(session.loading.statusText).font(IslandUI.font(16)).foregroundStyle(IslandUI.muted)
                    Spacer(minLength: 4)
                    Text(PartyLoadingTips.tips(for: launch.sport)[tip % 10]).font(IslandUI.font(compact ? 16 : 19)).foregroundStyle(IslandUI.navy).fixedSize(horizontal: false, vertical: true)
                    HStack {
                        IslandAction(title: "Back", focused: menu.isFocused("loadingBack"), compact: true) { menu.tap("loadingBack") }
                        if session.loading.isStalled { IslandAction(title: "Retry", focused: menu.isFocused("loadingRetry"), primary: true, compact: true) { menu.tap("loadingRetry") } }
                    }
                }.frame(width: compact ? nil : 470)
                if let player = session.players[safe: session.playerIndex] {
                    CharacterModelPreview(player: player, cameraDistance: 3.0, idleSport: launch.sport, practiceSequence: launch.sport == .tennis ? session.loading.practiceSequence : nil)
                        .frame(maxWidth: .infinity, maxHeight: .infinity).frame(minHeight: compact ? 250 : 0)
                        .accessibilityLabel("Your equipped character practicing")
                }
            }
        }.task {
            while !Task.isCancelled {
                do { try await Task.sleep(for: .seconds(4)) } catch { return }; tip += 1
            }
        }
    }
    static func label(_ launch: MenuLaunch, _ opponent: TennisOpponent?) -> String {
        switch (launch.sport, launch.mode) {
        case (.golf, .tutorial): "Golf Lesson"
        case (.golf, _): "Cliffside Golf"
        case (_, .tutorial): "Tennis Tutorial"
        case (_, .training): "Practice Court"
        default: TennisVenueChoice(rawValue: SportsSession.shared.tennisVenue)?.title ?? "Tropical Open"
        }
    }
}


extension TennisMenu {
    /// A readable name for a menu item, for the phone remote.
    static func label(for id: String) -> String {
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
        case "quickPlay", "partySolo": return "Quick Play"
        case "quickTennis": return "Casual tennis against AI"; case "quickGolf": return "Solo golf round"
        case "loadingBack": return "Back to Home"; case "loadingRetry": return "Retry loading"
        case "play": return "Play"; case "character": return "Locker"; case "settings": return "Settings"
        case "howto": return "How to play"; case "tutorial": return "Tutorial"; case "replayTutorial": return "Replay tutorial"
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
            case "shorts": return "Shorts: \((p?.outfitName("shorts") ?? "Kit colour"))"
            case "accent": return "Bands: \((p?.outfitName("accent") ?? "Kit colour"))"
            case "racket": return "Racket: \((p?.outfitName("racket") ?? "Kit colour"))"
            default: return id.prefix(1).uppercased() + id.dropFirst()
            }
        }
    }
}
