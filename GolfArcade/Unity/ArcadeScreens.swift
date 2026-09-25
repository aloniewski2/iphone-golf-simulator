import SwiftUI

// The sports-arcade front end around the tennis screens: title, main menu, game select, each
// sport's hub, character, settings, how-to, the golf lesson, connecting a screen, and loading.
// Laid out for the 1280×720 TV canvas, or stacked for the phone held upright (`compact`).

/// "ISLAND SPORTS ARCADE", tilted like a sticker.
struct ArcadeLogo: View {
    var scale: CGFloat = 1
    var body: some View {
        VStack(spacing: -12 * scale) {
            HStack(spacing: 10 * scale) {
                TennisBallIcon().frame(width: 54 * scale, height: 54 * scale)
                ArcadeText(text: "ISLAND", size: 64 * scale, top: .white, bottom: Arcade.sky)
            }
            ArcadeText(text: "SPORTS", size: 104 * scale, top: Arcade.gold, bottom: Arcade.sunDeep)
            Text("ARCADE  ·  SWING YOUR PHONE")
                .font(Arcade.font(15 * scale, .heavy)).tracking(3 * scale).foregroundStyle(Arcade.navy)
                .padding(.horizontal, 18 * scale).padding(.vertical, 7 * scale)
                .background(Capsule().fill(LinearGradient(colors: [Arcade.gold, Arcade.goldDeep], startPoint: .top, endPoint: .bottom)))
                .overlay(Capsule().strokeBorder(Arcade.navyDeep, lineWidth: 2.5 * scale))
                .padding(.top, 20 * scale)
        }
        .rotationEffect(.degrees(-4))
    }
}

/// Character-free venue art, contained within the offered space just like the video layer.
private struct EmptyVenueBackdrop: View {
    var body: some View {
        Color.clear.overlay {
            if let image = UIImage(named: "menu-backdrop.jpg") {
                Image(uiImage: image).resizable().scaledToFill()
            } else {
                LinearGradient(colors: [Arcade.skyDeep, Arcade.navyDeep], startPoint: .top, endPoint: .bottom)
            }
        }.clipped()
    }
}

/// B-roll that cycles through clips with a crossfade.
struct BrollReel: View {
    let clips: [String]
    var interval: Double = 6
    var dim = 0.35
    @State private var index = 0
    var body: some View {
        ZStack {
            EmptyVenueBackdrop()
            if !clips.isEmpty {
                LoopingVideo(clip: clips[index % clips.count]).id(index).transition(.opacity)
            }
            Color.black.opacity(dim)
        }
        .task(id: clips) {
            while !Task.isCancelled && clips.count > 1 {
                try? await Task.sleep(for: .seconds(interval))
                withAnimation(.easeInOut(duration: 0.8)) { index += 1 }
            }
        }
    }
    /// Every sport's clips, interleaved, for the title and main menu.
    static var everySport: [String] {
        let lists = Sport.allCases.map(\.clips)
        return (0..<(lists.map(\.count).max() ?? 0)).flatMap { i in lists.compactMap { $0[safe: i] } }
    }
}

struct TitleScreen: View {
    let menu: TennisMenu
    let compact: Bool
    @State private var blink = false
    var body: some View {
        ZStack {
            BrollReel(clips: BrollReel.everySport, interval: 5, dim: 0.3).ignoresSafeArea()
            VStack(spacing: 0) {
                Spacer()
                ArcadeLogo(scale: compact ? 0.62 : 1)
                Spacer()
                Text(compact ? "TAP TO START" : "PRESS  Ⓐ  TO START")
                    .font(Arcade.font(compact ? 24 : 34)).italic().foregroundStyle(.white)
                    .shadow(color: Arcade.navyDeep, radius: 0, x: 0, y: 3)
                    .opacity(blink ? 1 : 0.25).scaleEffect(blink ? 1.04 : 0.98)
                if TennisCampaign.shared.titles > 0 {
                    Label("\(TennisCampaign.shared.titles)× Tropical Open champion", systemImage: "trophy.fill")
                        .font(Arcade.font(16, .heavy)).foregroundStyle(Arcade.gold).padding(.top, 12)
                }
                Spacer().frame(height: compact ? 70 : 60)
            }
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .contentShape(Rectangle())
        .onTapGesture { menu.tap("start") }
        .onAppear { withAnimation(.easeInOut(duration: 0.8).repeatForever(autoreverses: true)) { blink = true } }
    }
}

// MARK: - Main menu

struct MainScreen: View {
    let menu: TennisMenu
    let compact: Bool
    var body: some View {
        let s = SportsSession.shared
        let player = s.players.indices.contains(s.playerIndex) ? s.players[s.playerIndex] : nil
        VStack(alignment: .leading, spacing: compact ? 14 : 22) {
            HStack(alignment: .top) {
                ArcadeLogo(scale: compact ? 0.36 : 0.44)
                Spacer()
                if let player { PlayerChip(player: player, compact: compact) }
            }
            if compact {
                ScrollView {
                    VStack(spacing: 14) {
                        ForEach(Self.items, id: \.id) { item in tile(item) }
                        StatsStrip(compact: true)
                    }.padding(.vertical, 12).padding(.horizontal, 20)
                }
            } else {
                HStack(spacing: 22) { ForEach(Self.items, id: \.id) { item in tile(item) } }
                StatsStrip(compact: false)
                Spacer(minLength: 0)
                HintBar()
            }
        }
        .padding(compact ? 0 : 44).padding(.top, compact ? 12 : 0)
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
    }

    struct Item { let id, title, subtitle, icon: String; let colors: (Color, Color) }
    static let items = [
        Item(id: "play", title: "PLAY", subtitle: "Golf · Tennis · more coming", icon: "sportscourt.fill", colors: (Arcade.sun, Arcade.sunDeep)),
        Item(id: "character", title: "CHARACTER", subtitle: "Look · kit colours · hand", icon: "person.crop.circle.fill", colors: (Arcade.sea, Arcade.seaDeep)),
        Item(id: "settings", title: "SETTINGS", subtitle: "Controls · display · sound", icon: "gearshape.fill", colors: (Arcade.sky, Arcade.skyDeep)),
        Item(id: "howto", title: "HOW TO PLAY", subtitle: "Connect · stand · swing", icon: "questionmark.circle.fill",
             colors: (Color(red: 0.72, green: 0.42, blue: 1.0), Color(red: 0.30, green: 0.10, blue: 0.62))),
    ]

    private func tile(_ item: Item) -> some View {
        let focused = menu.isFocused(item.id)
        return ZStack(alignment: .bottomLeading) {
            Plaque(top: item.colors.0, bottom: item.colors.1, corner: 26)
            if item.id == "play" {
                BrollReel(clips: BrollReel.everySport, interval: 4, dim: 0.15)
            } else if item.id == "character" {
                let s = SportsSession.shared
                let female = s.players.indices.contains(s.playerIndex) && s.players[s.playerIndex].standardFemale
                HeroArt(name: female ? "menu-hero-player-female" : "menu-hero-player-male")
                    .frame(height: compact ? 150 : 250).frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topTrailing)
                    .offset(x: 16, y: -4)
            } else {
                Image(systemName: item.icon).font(.system(size: compact ? 80 : 130, weight: .black))
                    .foregroundStyle(.white.opacity(0.22)).rotationEffect(.degrees(-8))
                    .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topTrailing).padding(18)
            }
            LinearGradient(colors: [.clear, item.colors.1.opacity(0.95)], startPoint: .center, endPoint: .bottom)
            VStack(alignment: .leading, spacing: 4) {
                ArcadeText(text: item.title, size: compact ? 32 : 36)
                Text(item.subtitle).font(Arcade.font(compact ? 14 : 15, .bold)).foregroundStyle(.white)
            }.padding(compact ? 18 : 20)
        }
        .frame(width: compact ? nil : 275, height: compact ? 150 : 350)
        .frame(maxWidth: compact ? .infinity : nil)
        .clipShape(RoundedRectangle(cornerRadius: 26, style: .continuous))
        .focusGlow(focused, corner: 26)
        .contentShape(Rectangle())
        .onTapGesture { menu.tap(item.id) }
    }
}

/// The player's name and look, top right of the main menu.
struct PlayerChip: View {
    let player: Player
    let compact: Bool
    var body: some View {
        HStack(spacing: 10) {
            Circle().fill(Color(hex: Outfit.skins[player.standardSkin]))
                .overlay(Image(systemName: player.standardFemale ? "person.fill" : "person.fill").foregroundStyle(.white.opacity(0.85)).font(.system(size: 18, weight: .black)))
                .overlay(Circle().strokeBorder(Outfit.color(player.shirt) ?? .white, lineWidth: 3))
                .frame(width: compact ? 36 : 46, height: compact ? 36 : 46)
            VStack(alignment: .leading, spacing: 0) {
                Text(player.name.uppercased()).font(Arcade.font(compact ? 15 : 19)).foregroundStyle(.white)
                Text(player.handedness == .left ? "Left-handed" : "Right-handed").font(Arcade.font(compact ? 11 : 13, .semibold)).foregroundStyle(.white.opacity(0.75))
            }
        }
        .padding(.horizontal, 14).padding(.vertical, 8)
        .background(Capsule().fill(Arcade.navyDeep.opacity(0.75)))
        .padding(.trailing, compact ? 16 : 0)
    }
}

/// Lifetime numbers, along the bottom of the main menu.
struct StatsStrip: View {
    let compact: Bool
    var body: some View {
        let p = SportProgress.shared, c = TennisCampaign.shared
        let stats: [(String, String)] = [
            ("\(p.matchesPlayed)", "Matches"), ("\(p.matchesWon)", "Wins"), ("\(p.bestRally)", "Best rally"),
            ("\(c.won)/\(TennisCampaign.draw.count)", "Circuit"), ("\(c.titles)", "Titles"),
        ]
        HStack(spacing: compact ? 8 : 16) {
            ForEach(stats, id: \.1) { value, label in
                VStack(spacing: 0) {
                    Text(value).font(Arcade.font(compact ? 18 : 24)).foregroundStyle(Arcade.gold).monospacedDigit()
                    Text(label.uppercased()).font(Arcade.font(compact ? 9 : 11, .heavy)).tracking(1).foregroundStyle(.white.opacity(0.8))
                }
                .frame(maxWidth: .infinity)
            }
        }
        .padding(.vertical, compact ? 8 : 10).padding(.horizontal, 12)
        .background(RoundedRectangle(cornerRadius: 16).fill(Arcade.navyDeep.opacity(0.72)))
        .frame(maxWidth: compact ? .infinity : 760)
    }
}

// MARK: - Game select

struct GameSelectScreen: View {
    let menu: TennisMenu
    let compact: Bool
    var body: some View {
        VStack(alignment: .leading, spacing: compact ? 12 : 18) {
            ArcadeText(text: "CHOOSE YOUR GAME", size: compact ? 32 : 46)
            if compact {
                ScrollView { VStack(spacing: 14) { ForEach(Sport.allCases) { tile($0) } }.padding(.vertical, 12).padding(.horizontal, 4) }
            } else {
                HStack(spacing: 16) { ForEach(Sport.allCases) { tile($0) } }
            }
            HStack {
                ArcadeButton(title: "Back", icon: "chevron.left", focused: menu.isFocused("back"),
                             top: Arcade.skyDeep, bottom: Arcade.navy, size: compact ? 20 : 22) { menu.tap("back") }
                Spacer()
                if !compact { HintBar() }
            }
        }
        .padding(compact ? 18 : 40)
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
    }

    private func tile(_ sport: Sport) -> some View {
        let id = "sport-\(sport.rawValue)"
        let focused = menu.isFocused(id)
        let progress = SportProgress.shared
        return ZStack(alignment: .bottomLeading) {
            Plaque(top: sport.playable ? sport.colors.0 : Color(white: 0.3), bottom: sport.playable ? sport.colors.1 : Color(white: 0.1), corner: 24)
            Group {
                if let clip = sport.clips.first { LoopingVideo(clip: clip, playing: focused) }
                else { EmptyVenueBackdrop() }
            }
            .saturation(sport.playable ? 1 : 0.25).opacity(sport.playable ? 1 : 0.7)
            LinearGradient(colors: [.clear, .black.opacity(0.85)], startPoint: .center, endPoint: .bottom)
            VStack(alignment: .leading, spacing: 4) {
                HStack(spacing: 6) {
                    Image(systemName: sport.icon).font(.system(size: compact ? 22 : 24, weight: .black)).foregroundStyle(.white)
                    ArcadeText(text: sport.title, size: compact ? 30 : 25)
                }
                Text(sport.tagline).font(Arcade.font(compact ? 13 : 13, .bold)).foregroundStyle(.white.opacity(0.9)).lineLimit(2)
            }.padding(16)
        }
        .overlay(alignment: .topLeading) {
            Group {
                if !sport.playable {
                    Label("COMING SOON", systemImage: "lock.fill").font(Arcade.font(13)).foregroundStyle(.white)
                        .padding(.horizontal, 10).padding(.vertical, 5).background(Capsule().fill(.black.opacity(0.7)))
                } else if !progress.finishedTutorial(sport) {
                    Label("NEW", systemImage: "sparkles").font(Arcade.font(13)).foregroundStyle(Arcade.navyDeep)
                        .padding(.horizontal, 10).padding(.vertical, 5).background(Capsule().fill(Arcade.gold))
                }
            }.padding(10)
        }
        .frame(width: compact ? nil : 226, height: compact ? 150 : 470)
        .frame(maxWidth: compact ? .infinity : nil)
        .clipShape(RoundedRectangle(cornerRadius: 24, style: .continuous))
        .focusGlow(focused, accent: sport.playable ? Arcade.gold : .white, corner: 24)
        .contentShape(Rectangle())
        .onTapGesture { menu.tap(id) }
    }
}

// MARK: - A sport's hub

struct HubScreen: View {
    let menu: TennisMenu
    let sport: Sport
    let compact: Bool
    var body: some View {
        let done = SportProgress.shared.finishedTutorial(sport)
        let layout = compact ? AnyLayout(VStackLayout(spacing: 14)) : AnyLayout(HStackLayout(alignment: .top, spacing: 28))
        VStack(alignment: .leading, spacing: compact ? 12 : 18) {
            layout {
                ZStack(alignment: .bottomLeading) {
                    BrollReel(clips: sport.clips, interval: 6, dim: 0.1)
                    LinearGradient(colors: [.clear, .black.opacity(0.8)], startPoint: .center, endPoint: .bottom)
                    VStack(alignment: .leading, spacing: 4) {
                        ArcadeText(text: sport.title, size: compact ? 40 : 60, top: .white, bottom: sport.colors.0)
                        Text(sport.tagline).font(Arcade.font(compact ? 14 : 18, .bold)).foregroundStyle(.white)
                        if !done {
                            Label("New here? Start with the tutorial — it unlocks everything else.", systemImage: "graduationcap.fill")
                                .font(Arcade.font(compact ? 13 : 16, .bold)).foregroundStyle(Arcade.gold).padding(.top, 4)
                        }
                    }.padding(compact ? 16 : 24)
                }
                .frame(width: compact ? nil : 560, height: compact ? 200 : 470)
                .frame(maxWidth: compact ? .infinity : nil)
                .clipShape(RoundedRectangle(cornerRadius: 26, style: .continuous))
                .overlay(RoundedRectangle(cornerRadius: 26, style: .continuous).strokeBorder(.white.opacity(0.3), lineWidth: 2))

                VStack(spacing: compact ? 10 : 12) {
                    ForEach(TennisMenu.hubItems(sport), id: \.self) { id in item(id) }
                    if !menu.notice.isEmpty {
                        Text(menu.notice).font(Arcade.font(compact ? 14 : 17, .bold)).foregroundStyle(Arcade.gold)
                            .frame(maxWidth: .infinity, alignment: .leading)
                    }
                }
                .frame(maxWidth: .infinity)
            }
            HStack(spacing: 16) {
                ArcadeButton(title: "Back", icon: "chevron.left", focused: menu.isFocused("back"),
                             top: Arcade.skyDeep, bottom: Arcade.navy, size: compact ? 18 : 22) { menu.tap("back") }
                if done {
                    ArcadeButton(title: "Replay tutorial", icon: "arrow.counterclockwise", focused: menu.isFocused("replayTutorial"),
                                 top: Arcade.skyDeep, bottom: Arcade.navy, size: compact ? 18 : 22) { menu.tap("replayTutorial") }
                }
                Spacer()
                if !compact { HintBar() }
            }
        }
        .padding(compact ? 18 : 40)
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
    }

    private func info(_ id: String) -> (title: String, subtitle: String, icon: String) {
        let c = TennisCampaign.shared
        let done = SportProgress.shared.finishedTutorial(sport)
        switch id {
        case "tutorial": return (sport == .tennis ? "Tutorial with Coach Ray" : "Golf lesson",
                                 done ? "Completed ✓ — replay any time" : "Start here · about 5 minutes", "graduationcap.fill")
        case "campaign": return ("Island Circuit", c.champion ? "Champion! Defend your title" : "Story campaign · round \(c.nextRound + 1) of 10 · vs \(c.next.name)", "trophy.fill")
        case "training": return ("Training court", "Free rally against the club coach", "figure.tennis")
        case "exhibition": return ("Exhibition match", "Quick match against any rival you've reached", "bolt.fill")
        case "round": return ("Play Cliffside", "A round on the ocean links", "flag.fill")
        case "golfCampaign": return ("Campaign", "Coming soon", "trophy.fill")
        default: return ("Driving range", "Coming soon", "target")
        }
    }

    private func item(_ id: String) -> some View {
        let unlocked = menu.hubUnlocked(sport, id)
        let focused = menu.isFocused(id)
        let (title, subtitle, icon) = info(id)
        let highlight = id == "tutorial" && !SportProgress.shared.finishedTutorial(sport)
        return HStack(spacing: 14) {
            Image(systemName: unlocked ? icon : "lock.fill").font(.system(size: compact ? 22 : 28, weight: .black))
                .foregroundStyle(unlocked ? (highlight ? Arcade.navyDeep : Arcade.gold) : .white.opacity(0.5))
                .frame(width: compact ? 40 : 52, height: compact ? 40 : 52)
                .background(Circle().fill(highlight ? Arcade.gold : Arcade.navyDeep.opacity(0.7)))
            VStack(alignment: .leading, spacing: 2) {
                Text(title.uppercased()).font(Arcade.font(compact ? 18 : 23)).italic().foregroundStyle(unlocked ? .white : .white.opacity(0.5))
                Text(subtitle).font(Arcade.font(compact ? 12 : 15, .semibold)).foregroundStyle(.white.opacity(unlocked ? 0.85 : 0.45)).lineLimit(1)
            }
            Spacer(minLength: 0)
            if unlocked { Image(systemName: "chevron.right").font(.system(size: 18, weight: .black)).foregroundStyle(.white.opacity(0.7)) }
        }
        .padding(.horizontal, 16).padding(.vertical, compact ? 10 : 13)
        .background(RoundedRectangle(cornerRadius: 20, style: .continuous)
            .fill(highlight ? LinearGradient(colors: [Arcade.sun, Arcade.sunDeep], startPoint: .top, endPoint: .bottom)
                  : LinearGradient(colors: [Arcade.navy.opacity(0.9), Arcade.navyDeep.opacity(0.9)], startPoint: .top, endPoint: .bottom)))
        .focusGlow(focused, corner: 20, refusals: focused ? menu.refusals : 0)
        .contentShape(Rectangle())
        .onTapGesture { menu.tap(id) }
    }
}

/// A sport that isn't playable yet.
struct LockedScreen: View {
    let menu: TennisMenu
    let sport: Sport
    let compact: Bool
    var body: some View {
        ZStack(alignment: .bottomLeading) {
            BrollReel(clips: sport.clips, interval: 6, dim: 0.35).ignoresSafeArea()
            VStack(alignment: .leading, spacing: 12) {
                Label("COMING SOON", systemImage: "lock.fill").font(Arcade.font(compact ? 16 : 22)).foregroundStyle(Arcade.navyDeep)
                    .padding(.horizontal, 14).padding(.vertical, 6).background(Capsule().fill(Arcade.gold))
                ArcadeText(text: sport.title, size: compact ? 56 : 96, top: .white, bottom: sport.colors.0)
                Text("\(sport.tagline). We're building it now — golf and tennis are ready to play today.")
                    .font(Arcade.font(compact ? 16 : 22, .semibold)).foregroundStyle(.white).frame(maxWidth: 640, alignment: .leading)
                ArcadeButton(title: "Back", icon: "chevron.left", focused: menu.isFocused("back"),
                             top: Arcade.skyDeep, bottom: Arcade.navy, size: compact ? 20 : 24) { menu.tap("back") }
                    .padding(.top, 8)
            }
            .padding(compact ? 22 : 56)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
    }
}

// MARK: - Exhibition

struct ExhibitionScreen: View {
    let menu: TennisMenu
    let compact: Bool
    var body: some View {
        let campaign = TennisCampaign.shared
        VStack(alignment: .leading, spacing: compact ? 12 : 12) {
            ArcadeText(text: "EXHIBITION", size: compact ? 34 : 44)
            Text("A single short set against any rival you've reached on the Island Circuit.")
                .font(Arcade.font(compact ? 15 : 16, .semibold)).foregroundStyle(.white.opacity(0.9))
            if compact {
                ScrollView {
                    VStack(spacing: 14) {
                        ForEach(Array(TennisCampaign.draw.enumerated()), id: \.offset) { i, o in
                            OpponentCard(opponent: o, round: i, compact: true, focused: menu.isFocused("rival\(i)"), refusals: menu.refusals)
                                .onTapGesture { menu.tap("rival\(i)") }
                        }
                    }.padding(.vertical, 12).padding(.horizontal, 6)
                }
            } else {
                VStack(spacing: 12) {
                    ForEach(0..<2, id: \.self) { half in
                        HStack(spacing: 22) {
                            ForEach(half * 5..<half * 5 + 5, id: \.self) { i in
                                OpponentCard(opponent: TennisCampaign.draw[i], round: i, compact: false,
                                             focused: menu.isFocused("rival\(i)"), refusals: menu.refusals)
                                    .onTapGesture { menu.tap("rival\(i)") }
                            }
                        }
                    }
                }
            }
            Text(menu.notice.isEmpty ? "\(min(campaign.won + 1, 10)) of 10 rivals available" : menu.notice)
                .font(Arcade.font(compact ? 14 : 17, .bold)).foregroundStyle(Arcade.gold)
            HStack {
                ArcadeButton(title: "Back", icon: "chevron.left", focused: menu.isFocused("back"),
                             top: Arcade.skyDeep, bottom: Arcade.navy, size: compact ? 18 : 22) { menu.tap("back") }
                Spacer()
                if !compact { HintBar() }
            }
        }
        .padding(.horizontal, compact ? 18 : 40).padding(.vertical, compact ? 18 : 26)
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
    }
}

// MARK: - Character

struct CharacterScreen: View {
    let menu: TennisMenu
    let compact: Bool
    @State private var name = ""
    var body: some View {
        let s = SportsSession.shared
        let p = s.players.indices.contains(s.playerIndex) ? s.players[s.playerIndex] : Player(name: "Player 1", colorIndex: 0)
        let rows: [(String, String, String, Color?)] = [
            ("body", "Body", p.standardFemale ? "Female" : "Male", nil),
            ("skin", "Skin tone", TennisMenu.skinTones[p.standardSkin], Color(hex: Outfit.skins[p.standardSkin])),
            ("hand", "Plays", p.handedness == .left ? "Left-handed" : "Right-handed", nil),
            ("shirt", "Shirt", Outfit.name(p.shirt), Outfit.color(p.shirt)),
            ("shorts", "Shorts / skirt", Outfit.name(p.shorts), Outfit.color(p.shorts)),
            ("accent", "Headband & wristbands", Outfit.name(p.accent), Outfit.color(p.accent)),
            ("racket", "Racket", Outfit.name(p.racket), Outfit.color(p.racket)),
        ]
        let layout = compact ? AnyLayout(VStackLayout(spacing: 12)) : AnyLayout(HStackLayout(alignment: .top, spacing: 30))
        VStack(alignment: .leading, spacing: compact ? 10 : 14) {
            ArcadeText(text: "CHARACTER", size: compact ? 34 : 46, top: .white, bottom: Arcade.sea)
            layout {
                KitPreview(player: p, compact: compact)
                let list = VStack(spacing: compact ? 8 : 8) {
                    if compact {
                        HStack {
                            Text("NAME").font(Arcade.font(16, .heavy)).tracking(1).foregroundStyle(.white.opacity(0.85))
                            TextField("Your name", text: $name).font(Arcade.font(20)).foregroundStyle(Arcade.gold)
                                .multilineTextAlignment(.trailing).submitLabel(.done)
                                .onSubmit { rename(name) }
                        }
                        .padding(.horizontal, 22).padding(.vertical, 12)
                        .background(RoundedRectangle(cornerRadius: 18).fill(Arcade.navyDeep.opacity(0.6)))
                    }
                    ForEach(rows, id: \.0) { row in
                        ChoiceRow(label: row.1, value: row.2, focused: menu.isFocused(row.0), compact: compact) { menu.tap(row.0) }
                            .overlay(alignment: .leading) {
                                if let swatch = row.3 {
                                    Circle().fill(swatch).frame(width: 18, height: 18).overlay(Circle().strokeBorder(.white, lineWidth: 2))
                                        .offset(x: -8)
                                }
                            }
                    }
                    HStack(spacing: 14) {
                        ArcadeButton(title: "Randomize", icon: "dice.fill", focused: menu.isFocused("randomize"),
                                     top: Arcade.sea, bottom: Arcade.seaDeep, size: compact ? 17 : 20) { menu.tap("randomize") }
                        ArcadeButton(title: "Kit colours", icon: "arrow.uturn.backward", focused: menu.isFocused("reset"),
                                     top: Arcade.skyDeep, bottom: Arcade.navy, size: compact ? 17 : 20) { menu.tap("reset") }
                        ArcadeButton(title: "Done", icon: "checkmark", focused: menu.isFocused("back"),
                                     size: compact ? 17 : 20) { menu.tap("back") }
                    }.padding(.top, 6)
                }
                if compact { ScrollView { list.padding(.horizontal, 6) } } else { list }
            }
        }
        .padding(compact ? 18 : 36)
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
        .onAppear { name = p.name }
    }

    private func rename(_ text: String) {
        let s = SportsSession.shared
        let trimmed = text.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty, s.players.indices.contains(s.playerIndex) else { return }
        s.players[s.playerIndex].name = String(trimmed.prefix(14)); s.savePlayers()
    }
}

/// The character's look: their art, and their kit drawn in the chosen colours.
struct KitPreview: View {
    let player: Player
    let compact: Bool
    var body: some View {
        ZStack(alignment: .bottom) {
            Plaque(top: Arcade.sea, bottom: Arcade.seaDeep, corner: 26)
            HeroArt(name: player.standardFemale ? "menu-hero-player-female" : "menu-hero-player-male")
                .frame(height: compact ? 190 : 380).offset(x: compact ? -60 : -70, y: -30)
            VStack(spacing: compact ? 6 : 10) {
                kitPiece("tshirt.fill", Outfit.color(player.shirt) ?? .white, "SHIRT")
                kitPiece("rectangle.fill", Outfit.color(player.shorts) ?? Color(white: 0.4), player.standardFemale ? "SKIRT" : "SHORTS")
                kitPiece("circle.circle.fill", Outfit.color(player.accent) ?? .white, "BANDS")
                kitPiece("tennis.racket", Outfit.color(player.racket) ?? Arcade.sunDeep, "RACKET")
            }
            .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topTrailing).padding(compact ? 12 : 18)
            Text(player.name.uppercased()).font(Arcade.font(compact ? 18 : 24)).foregroundStyle(.white)
                .padding(.horizontal, 16).padding(.vertical, 6).background(Capsule().fill(Arcade.navyDeep.opacity(0.8)))
                .padding(.bottom, 14)
        }
        .frame(width: compact ? nil : 420, height: compact ? 230 : 520)
        .frame(maxWidth: compact ? .infinity : nil)
        .clipShape(RoundedRectangle(cornerRadius: 26, style: .continuous))
    }
    private func kitPiece(_ symbol: String, _ color: Color, _ label: String) -> some View {
        VStack(spacing: 2) {
            Image(systemName: symbol).font(.system(size: compact ? 24 : 38, weight: .black)).foregroundStyle(color)
                .shadow(color: .black.opacity(0.5), radius: 2, y: 2)
            Text(label).font(Arcade.font(compact ? 9 : 11, .heavy)).tracking(1).foregroundStyle(.white)
        }
        .frame(width: compact ? 58 : 84, height: compact ? 50 : 76)
        .background(RoundedRectangle(cornerRadius: 14).fill(Arcade.navyDeep.opacity(0.7)))
    }
}

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

struct HowToScreen: View {
    let menu: TennisMenu
    let compact: Bool
    var body: some View {
        CardPager(title: "HOW TO PLAY", cards: HowTo.pages, index: menu.howToPage, menu: menu, compact: compact,
                  nextID: "nextPage", nextTitle: "Next", finalTitle: nil) {
            if HowTo.pages[menu.howToPage].art == .mirror || HowTo.pages[menu.howToPage].art == .mac {
                HStack(spacing: 10) {
                    AirPlayButton().frame(width: 44, height: 44)
                        .background(Circle().fill(LinearGradient(colors: [Arcade.sky, Arcade.skyDeep], startPoint: .top, endPoint: .bottom)))
                    Text("Or pick a screen here").font(Arcade.font(14, .semibold)).foregroundStyle(.white.opacity(0.8))
                }
            }
        }
    }
}

struct GolfLessonScreen: View {
    let menu: TennisMenu
    let compact: Bool
    var body: some View {
        CardPager(title: "GOLF LESSON", cards: GolfLesson.cards, index: menu.lessonCard, menu: menu, compact: compact,
                  nextID: "nextCard", nextTitle: "Next", finalTitle: "Hit a practice shot") { EmptyView() }
    }
}

/// A page of how-to cards with dots and Back / Previous / Next.
struct CardPager<Extra: View>: View {
    let title: String
    let cards: [HowToCard]
    let index: Int
    let menu: TennisMenu
    let compact: Bool
    let nextID: String
    let nextTitle: String
    let finalTitle: String?
    @ViewBuilder var extra: () -> Extra
    var body: some View {
        let last = index >= cards.count - 1
        VStack(alignment: .leading, spacing: compact ? 14 : 22) {
            ArcadeText(text: title, size: compact ? 34 : 46, top: .white, bottom: Arcade.gold)
            Spacer(minLength: 0)
            HowToCardView(card: cards[min(index, cards.count - 1)], compact: compact).id(index)
                .transition(.asymmetric(insertion: .move(edge: .trailing).combined(with: .opacity), removal: .opacity))
            HStack(spacing: 8) {
                ForEach(cards.indices, id: \.self) { i in
                    Capsule().fill(i == index ? Arcade.gold : .white.opacity(0.35)).frame(width: i == index ? 30 : 12, height: 10)
                }
            }.frame(maxWidth: .infinity)
            extra()
            Spacer(minLength: 0)
            HStack(spacing: 14) {
                ArcadeButton(title: "Back", icon: "chevron.left", focused: menu.isFocused("back"),
                             top: Arcade.skyDeep, bottom: Arcade.navy, size: compact ? 17 : 22) { menu.tap("back") }
                Spacer()
                ArcadeButton(title: "Previous", icon: "arrow.left", focused: menu.isFocused("prev"),
                             top: Arcade.skyDeep, bottom: Arcade.navy, size: compact ? 17 : 22) { menu.tap("prev") }
                if !(last && finalTitle == nil) {
                    ArcadeButton(title: last ? (finalTitle ?? nextTitle) : nextTitle, icon: last ? "play.fill" : "arrow.right",
                                 focused: menu.isFocused(nextID), size: compact ? 17 : 22) { menu.tap(nextID) }
                }
            }
        }
        .padding(compact ? 18 : 48)
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
        .animation(.spring(response: 0.4, dampingFraction: 0.85), value: index)
    }
}

// MARK: - Connect a screen (phone only)

struct ConnectScreen: View {
    let menu: TennisMenu
    let compact: Bool
    var body: some View {
        ScrollView {
            VStack(spacing: 16) {
                Image(systemName: "rectangle.on.rectangle").font(.system(size: 54, weight: .bold)).foregroundStyle(Arcade.gold).padding(.top, 20)
                ArcadeText(text: "CONNECT A SCREEN", size: compact ? 32 : 46)
                Text("Mirror to a TV or a MacBook and the game plays there. Your iPhone becomes the racket.")
                    .font(Arcade.font(16, .semibold)).foregroundStyle(.white).multilineTextAlignment(.center).padding(.horizontal, 16)
                HowToCardView(card: HowTo.connectTV, compact: true)
                HowToCardView(card: HowTo.connectMac, compact: true)
                HStack(spacing: 12) {
                    AirPlayButton().frame(width: 54, height: 54)
                        .background(Circle().fill(LinearGradient(colors: [Arcade.sky, Arcade.skyDeep], startPoint: .top, endPoint: .bottom)))
                        .overlay(Circle().strokeBorder(.white, lineWidth: 2))
                    Text("AirPlay devices").font(Arcade.font(14, .semibold)).foregroundStyle(.white.opacity(0.8))
                }
                Text("Waiting for a screen… the match starts as soon as one connects.")
                    .font(Arcade.font(13, .semibold)).foregroundStyle(Arcade.gold)
                ArcadeButton(title: "Play on this phone", icon: "iphone.landscape", focused: menu.isFocused("phone"),
                             top: Arcade.sea, bottom: Arcade.seaDeep, size: 20) { menu.tap("phone") }
                ArcadeButton(title: "Back", icon: "chevron.left", focused: menu.isFocused("back"),
                             top: Arcade.skyDeep, bottom: Arcade.navy, size: 18) { menu.tap("back") }
                    .padding(.bottom, 20)
            }
            .padding(.horizontal, 16)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
    }
}

// MARK: - Loading

/// While the game loads: b-roll behind, the match-up, a how-to card and a tip that change as
/// you wait, and a bar that flows to 100% (LoadingModel keeps it up at least ten seconds).
struct LoadingScreen: View {
    let menu: TennisMenu
    let compact: Bool
    @State private var slam = false
    @State private var tip = 0
    @State private var card = 0
    private let tipTimer = Timer.publish(every: 3.5, on: .main, in: .common).autoconnect()
    private let cardTimer = Timer.publish(every: 5, on: .main, in: .common).autoconnect()
    var body: some View {
        let launch = menu.launch ?? MenuLaunch(mode: .training)
        let sport = launch.sport
        let s = SportsSession.shared
        let tips = HowTo.tips(sport), cards = HowTo.loadingCards(sport)
        ZStack {
            BrollReel(clips: sport.clips, interval: 5, dim: 0.45).ignoresSafeArea()
            VStack(spacing: compact ? 10 : 16) {
                Text(Self.label(launch, menu.loadingOpponent))
                    .font(Arcade.font(compact ? 15 : 22, .heavy)).tracking(2).foregroundStyle(Arcade.navyDeep)
                    .padding(.horizontal, 22).padding(.vertical, 8)
                    .background(Capsule().fill(LinearGradient(colors: [Arcade.gold, Arcade.goldDeep], startPoint: .top, endPoint: .bottom)))
                    .overlay(Capsule().strokeBorder(Arcade.navyDeep, lineWidth: 3))
                    .padding(.top, compact ? 14 : 24)
                matchup(launch, compact: compact).frame(maxHeight: .infinity)
                HowToCardView(card: cards[card % cards.count], compact: true)
                    .frame(maxWidth: compact ? .infinity : 860).id(card).transition(.opacity)
                VStack(spacing: 8) {
                    Text(tips[tip % tips.count]).font(Arcade.font(compact ? 14 : 19, .semibold)).foregroundStyle(.white)
                        .multilineTextAlignment(.center).id(tip).transition(.opacity)
                    ProgressBar(progress: s.loading.progress).frame(width: compact ? 320 : 820, height: 16)
                    Text(s.loading.progress >= 1 ? "READY!" : "LOADING  \(s.loading.percent)%")
                        .font(Arcade.font(compact ? 13 : 16, .heavy)).tracking(3).foregroundStyle(.white.opacity(0.9)).monospacedDigit()
                }
                .padding(.horizontal, 18).padding(.vertical, 12)
                .background(RoundedRectangle(cornerRadius: 20).fill(Arcade.navyDeep.opacity(0.75)))
                .padding(.bottom, compact ? 16 : 24)
            }
            .padding(.horizontal, compact ? 12 : 40)
        }
        .onAppear { withAnimation(.spring(response: 0.45, dampingFraction: 0.55).delay(0.3)) { slam = true } }
        .onReceive(tipTimer) { _ in withAnimation { tip += 1 } }
        .onReceive(cardTimer) { _ in withAnimation(.easeInOut(duration: 0.5)) { card += 1 } }
    }

    static func label(_ launch: MenuLaunch, _ opponent: TennisOpponent?) -> String {
        switch (launch.sport, launch.mode) {
        case (.golf, .tutorial): "GOLF LESSON · PRACTICE SHOT"
        case (.golf, _): "CLIFFSIDE LINKS"
        case (_, .tutorial): "PRACTICE COURT · TUTORIAL"
        case (_, .training): "TRAINING COURT"
        case (_, .exhibition): "EXHIBITION · \(opponent?.nickname.uppercased() ?? "")"
        default: opponent.map { "ISLAND CIRCUIT · \($0.round) · \($0.formatTitle.uppercased())" } ?? "TROPICAL OPEN"
        }
    }

    @ViewBuilder private func matchup(_ launch: MenuLaunch, compact: Bool) -> some View {
        let s = SportsSession.shared
        let female = s.players.indices.contains(s.playerIndex) && s.players[s.playerIndex].standardFemale
        let you = s.players.indices.contains(s.playerIndex) ? s.players[s.playerIndex].name.uppercased() : "YOU"
        if launch.sport == .golf {
            VStack(spacing: 4) {
                Image(systemName: "flag.fill").font(.system(size: compact ? 44 : 70, weight: .black)).foregroundStyle(Arcade.crimson)
                ArcadeText(text: launch.mode == .tutorial ? "YOUR FIRST SHOT" : "18 HOLES BY THE SEA", size: compact ? 28 : 48)
            }.scaleEffect(slam ? 1 : 0.6).opacity(slam ? 1 : 0)
        } else {
            let opponent = menu.loadingOpponent
            let rightArt = opponent?.art ?? "menu-hero-ray"
            let rightName = opponent.map { $0.name.components(separatedBy: " ")[0].uppercased() } ?? (launch.mode == .tutorial ? "COACH RAY" : "COACH")
            HStack(spacing: 0) {
                side(art: female ? "menu-hero-player-female" : "menu-hero-player-male", name: you, from: -1)
                ArcadeText(text: "VS", size: compact ? 54 : 90, top: .white, bottom: Arcade.gold)
                    .scaleEffect(slam ? 1 : 3).opacity(slam ? 1 : 0).rotationEffect(.degrees(-8))
                side(art: rightArt, name: rightName, from: 1)
            }
        }
    }

    private func side(art: String, name: String, from: CGFloat) -> some View {
        VStack(spacing: 0) {
            HeroArt(name: art).frame(maxHeight: .infinity)
            ArcadeText(text: name, size: compact ? 22 : 34)
        }
        .frame(maxWidth: .infinity)
        .offset(x: slam ? 0 : from * 300).opacity(slam ? 1 : 0)
    }
}

/// A chunky progress bar that glides between updates, with a shine sweeping along the fill.
struct ProgressBar: View {
    let progress: Double
    @State private var shine = false
    var body: some View {
        GeometryReader { g in
            ZStack(alignment: .leading) {
                Capsule().fill(Arcade.navyDeep.opacity(0.9))
                Capsule().fill(LinearGradient(colors: [Arcade.gold, Arcade.sunDeep], startPoint: .leading, endPoint: .trailing))
                    .frame(width: max(16, g.size.width * min(1, max(0, progress))))
                    .overlay(alignment: .leading) {
                        LinearGradient(colors: [.clear, .white.opacity(0.55), .clear], startPoint: .leading, endPoint: .trailing)
                            .frame(width: 60).offset(x: shine ? g.size.width : -60)
                    }
                    .clipShape(Capsule())
                    .animation(.linear(duration: 0.12), value: progress)
            }
            .overlay(Capsule().strokeBorder(.white.opacity(0.35), lineWidth: 1.5))
        }
        .onAppear { withAnimation(.linear(duration: 1.4).repeatForever(autoreverses: false)) { shine = true } }
    }
}

extension TennisMenu {
    /// A readable name for a menu item, for the phone remote.
    static func label(for id: String) -> String {
        if id.hasPrefix("sport-"), let s = Sport(rawValue: String(id.dropFirst(6))) { return s.playable ? s.title.capitalized : "\(s.title.capitalized) · coming soon" }
        if id.hasPrefix("tab-"), let t = SettingsTab(rawValue: String(id.dropFirst(4))) { return "Settings · \(t.title)" }
        if id.hasPrefix("rival"), let r = Int(id.dropFirst(5)) {
            let o = TennisCampaign.draw[r]
            return TennisCampaign.shared.unlocked(r) ? "Exhibition · \(o.name)" : "Exhibition · Locked"
        }
        switch id {
        case "play": return "Play"; case "character": return "Character"; case "settings": return "Settings"
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
            case "shirt": return "Shirt: \(Outfit.name(p?.shirt))"
            case "shorts": return "Shorts: \(Outfit.name(p?.shorts))"
            case "accent": return "Bands: \(Outfit.name(p?.accent))"
            case "racket": return "Racket: \(Outfit.name(p?.racket))"
            default: return id.prefix(1).uppercased() + id.dropFirst()
            }
        }
    }
}
