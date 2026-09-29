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

// A self-contained lobby design system, independent of the legacy arcade screens.
private enum LobbyStyle {
    static let ink = Color(hex: "091321")
    static let lime = Color(hex: "D5FF42")
    static func display(_ size: CGFloat) -> Font { .custom("AvenirNextCondensed-Heavy", fixedSize: size) }
    static func label(_ size: CGFloat) -> Font { .system(size: size, weight: .semibold) }
}

private struct LobbyButtonStyle: ButtonStyle {
    var selected = false
    @Environment(\.accessibilityReduceMotion) private var reduceMotion
    func makeBody(configuration: Configuration) -> some View {
        configuration.label
            .overlay(Rectangle().strokeBorder(.white, lineWidth: selected ? 2 : 0).padding(-5))
            .brightness(configuration.isPressed ? -0.1 : 0)
            .scaleEffect(configuration.isPressed && !reduceMotion ? 0.98 : 1)
            .animation(reduceMotion ? nil : .easeOut(duration: 0.12), value: configuration.isPressed)
            .animation(reduceMotion ? nil : .easeOut(duration: 0.12), value: selected)
    }
}

struct LobbyBackdrop: View {
    var body: some View {
        GeometryReader { g in
            ZStack {
                LinearGradient(colors: [Color(hex: "254968"), Color(hex: "142A40"), Color(hex: "0A1525")],
                               startPoint: .topTrailing, endPoint: .bottomLeading)
                Path { p in
                    let w = g.size.width, h = g.size.height
                    p.move(to: CGPoint(x: w * 0.55, y: h * 0.42)); p.addLine(to: CGPoint(x: w * 0.26, y: h))
                    p.move(to: CGPoint(x: w * 0.86, y: h * 0.42)); p.addLine(to: CGPoint(x: w * 1.2, y: h))
                    p.move(to: CGPoint(x: w * 0.55, y: h * 0.42)); p.addLine(to: CGPoint(x: w * 0.86, y: h * 0.42))
                    p.move(to: CGPoint(x: w * 0.44, y: h * 0.64)); p.addLine(to: CGPoint(x: w, y: h * 0.64))
                    p.move(to: CGPoint(x: w * 0.705, y: h * 0.42)); p.addLine(to: CGPoint(x: w * 0.72, y: h * 0.64))
                }.stroke(.white.opacity(0.09), lineWidth: 1)
                LinearGradient(colors: [LobbyStyle.ink.opacity(0.85), .clear], startPoint: .leading, endPoint: .trailing)
            }
        }
    }
}

/// Home, styled like a character-select stage (Fall Guys, Wii Sports Resort): your player on a
/// lit pedestal, one big PLAY, and clean cards for everything else. Plain words for people who
/// don't play games, but a grown-up look: no confetti, no candy gloss.
struct MainScreen: View {
    let menu: TennisMenu
    let compact: Bool
    private var player: Player? {
        let session = SportsSession.shared
        return session.players.indices.contains(session.playerIndex) ? session.players[session.playerIndex] : nil
    }
    private var hello: String {
        let hour = Calendar.current.component(.hour, from: Date())
        return hour < 12 ? "Good morning" : hour < 18 ? "Welcome back" : "Good evening"
    }
    var body: some View {
        ZStack {
            ShowroomBackdrop(pedestal: compact ? CGPoint(x: 0.5, y: 0.47) : CGPoint(x: 0.25, y: 0.87))
            if compact { phone } else { tv }
        }
    }

    // MARK: TV (1280×720 canvas)

    private var tv: some View {
        VStack(spacing: 0) {
            topBar
            HStack(alignment: .bottom, spacing: 36) {
                stage.frame(width: 420)
                VStack(alignment: .leading, spacing: 22) {
                    VStack(alignment: .leading, spacing: 4) {
                        Text("\(hello), \(player?.name ?? "friend")".uppercased())
                            .font(Showroom.text(16, .heavy)).tracking(2.5).foregroundStyle(Showroom.cyan)
                        Text("What are we playing?").font(Showroom.display(52)).foregroundStyle(.white)
                            .shadow(color: Showroom.violetDeep.opacity(0.6), radius: 0, x: 0, y: 4)
                    }
                    playButton.frame(height: 150)
                    HStack(spacing: 18) {
                        card("homeCampaign", "Adventure", "Take on 10 island champions", "map.fill", Showroom.magenta)
                        card("character", "Your look", "Style your player", "tshirt.fill", Showroom.violet)
                        card("homePlay", "With friends", "Coming soon", "person.2.fill", Color(hex: "1FA8C9"))
                    }.frame(height: 150)
                }
                .padding(.bottom, 30)
            }
            .frame(maxHeight: .infinity, alignment: .bottom)
        }
        .padding(.horizontal, 44).padding(.top, 28).padding(.bottom, 26)
    }

    // MARK: Phone, upright

    private var phone: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 16) {
                topBar
                stage.frame(height: 330)
                Text("What are we playing?").font(Showroom.display(34)).foregroundStyle(.white)
                playButton.frame(height: 110)
                HStack(spacing: 12) {
                    card("homeCampaign", "Adventure", "10 island champions", "map.fill", Showroom.magenta)
                    card("character", "Your look", "Style your player", "tshirt.fill", Showroom.violet)
                }.frame(height: 130)
                card("homePlay", "With friends", "Coming soon", "person.2.fill", Color(hex: "1FA8C9")).frame(height: 90)
            }.padding(20)
        }
    }

    // MARK: Pieces

    private var topBar: some View {
        HStack(alignment: .center, spacing: 14) {
            wordmark
            Spacer()
            pill("howto", "How to play", "questionmark.circle.fill")
            pill("settings", "Settings", "gearshape.fill")
        }
    }

    /// A clean wordmark instead of the tilted sticker logo.
    private var wordmark: some View {
        VStack(alignment: .leading, spacing: -4) {
            Text("ISLAND").font(Showroom.text(compact ? 12 : 15, .heavy)).tracking(compact ? 5 : 7).foregroundStyle(.white.opacity(0.85))
            Text("SPORTS").font(Showroom.display(compact ? 34 : 44)).foregroundStyle(.white)
                .overlay(alignment: .bottomLeading) {
                    Capsule().fill(Showroom.yellow).frame(width: compact ? 58 : 76, height: compact ? 5 : 6).offset(y: compact ? 4 : 5)
                }
        }
    }

    private func pill(_ id: String, _ title: String, _ icon: String) -> some View {
        let focused = menu.isFocused(id)
        return Button { menu.tap(id) } label: {
            Label(compact ? "" : title, systemImage: icon)
                .labelStyle(.titleAndIcon)
                .font(Showroom.text(compact ? 15 : 17, .bold))
                .foregroundStyle(focused ? Showroom.ink : .white)
                .padding(.horizontal, compact ? 12 : 18).frame(height: compact ? 40 : 46)
                .background(Capsule().fill(focused ? Color.white : Color.white.opacity(0.14)))
                .overlay(Capsule().strokeBorder(.white.opacity(focused ? 0 : 0.25), lineWidth: 1.5))
        }
        .buttonStyle(.plain)
        .scaleEffect(focused ? 1.06 : 1).animation(.spring(response: 0.3, dampingFraction: 0.6), value: focused)
        .accessibilityLabel(title)
    }

    /// Your player on the pedestal (the backdrop draws the pedestal and spotlight under it).
    private var stage: some View {
        ZStack(alignment: .bottom) {
            if let player {
                CharacterModelPreview(player: player, cameraDistance: 3.9)
                    .padding(.bottom, compact ? 26 : 34)
            } else {
                HeroArt(name: "menu-hero-player-male").padding(.bottom, 40)
            }
            if let player {
                Text(player.name.uppercased())
                    .font(Showroom.text(compact ? 13 : 15, .heavy)).tracking(2).foregroundStyle(Showroom.ink)
                    .padding(.horizontal, 16).padding(.vertical, 7)
                    .background(Capsule().fill(.white))
                    .shadow(color: .black.opacity(0.25), radius: 6, y: 3)
            }
        }
    }

    /// The one big button: straight into a game.
    private var playButton: some View {
        SlabButton(fill: Showroom.yellow, slab: Showroom.yellowDeep, focused: menu.isFocused("quickPlay"), corner: 30) {
            menu.tap("quickPlay")
        } label: {
            HStack(spacing: compact ? 16 : 26) {
                Text("PLAY").font(Showroom.display(compact ? 58 : 92)).foregroundStyle(Showroom.ink)
                VStack(alignment: .leading, spacing: 4) {
                    Text("Tennis or golf").font(Showroom.text(compact ? 17 : 24, .heavy)).foregroundStyle(Showroom.ink)
                    Text("Swing your phone like the real thing").font(Showroom.text(compact ? 13 : 17)).foregroundStyle(Showroom.ink.opacity(0.7))
                }
                Spacer(minLength: 0)
                Image(systemName: "arrow.right").font(.system(size: compact ? 26 : 38, weight: .black)).foregroundStyle(Showroom.ink)
            }.padding(.horizontal, compact ? 22 : 36)
        }
        .accessibilityLabel("Play").accessibilityIdentifier("home-quickPlay")
    }

    /// A clean white card with a coloured badge.
    private func card(_ id: String, _ title: String, _ subtitle: String, _ icon: String, _ tint: Color) -> some View {
        SlabButton(fill: Showroom.card, slab: Color(hex: "C9C4E8"), focused: menu.isFocused(id), corner: 24) { menu.tap(id) } label: {
            VStack(alignment: .leading, spacing: compact ? 6 : 8) {
                ZStack {
                    RoundedRectangle(cornerRadius: 12, style: .continuous).fill(tint)
                    Image(systemName: icon).font(.system(size: compact ? 18 : 22, weight: .black)).foregroundStyle(.white)
                }.frame(width: compact ? 38 : 48, height: compact ? 38 : 48)
                Spacer(minLength: 0)
                Text(title).font(Showroom.text(compact ? 18 : 24, .heavy)).foregroundStyle(Showroom.ink).lineLimit(1).minimumScaleFactor(0.7)
                Text(subtitle).font(Showroom.text(compact ? 12 : 15)).foregroundStyle(Showroom.muted).lineLimit(1).minimumScaleFactor(0.7)
            }
            .padding(compact ? 14 : 18).frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .leading)
        }
        .accessibilityLabel(title).accessibilityHint(subtitle).accessibilityIdentifier("home-\(id)")
    }
}

/// Shared main action for the home, party destination and loading recovery.
private struct LobbyAction: View {
    let title: String
    var icon = "arrow.right"
    var focused = false
    var compact = true
    var primary = true
    let action: () -> Void
    var body: some View {
        Button(action: action) {
            HStack(spacing: 12) {
                Text(title).font(compact ? .headline : LobbyStyle.display(28))
                Spacer(minLength: 8)
                Image(systemName: icon)
            }
            .foregroundStyle(primary ? LobbyStyle.ink : .white)
            .padding(18).frame(maxWidth: .infinity, minHeight: 56)
            .background(primary ? LobbyStyle.lime : Color.white.opacity(0.08))
        }.buttonStyle(LobbyButtonStyle(selected: focused))
            .accessibilityLabel(title)
    }
}

/// The party entry is honest about transport not shipping in this checkout.
struct PartyLobbyScreen: View {
    let menu: TennisMenu
    let compact: Bool
    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 24) {
                Text("PLAY WITH FRIENDS").font(LobbyStyle.display(compact ? 36 : 56)).foregroundStyle(.white)
                Label("Party play is coming soon", systemImage: "person.2.fill")
                    .font(compact ? .title2.bold() : .system(size: 26, weight: .bold)).foregroundStyle(LobbyStyle.lime)
                Text("Save a spot for your friends. Couch parties and online invites are on the way.")
                    .font(compact ? .body : .system(size: 21)).foregroundStyle(.white.opacity(0.8))
                Text("Create and Join will appear here when party play is ready.")
                    .font(compact ? .subheadline : .system(size: 17)).foregroundStyle(.white.opacity(0.65))
                LobbyAction(title: "Try Quick Play", focused: menu.isFocused("partySolo"), compact: compact) { menu.tap("partySolo") }
                LobbyAction(title: "Back to Home", icon: "arrow.left", focused: menu.isFocused("back"), compact: compact, primary: false) { menu.tap("back") }
            }.padding(compact ? 24 : 48).frame(maxWidth: 720)
                .frame(maxWidth: .infinity)
        }.background(LobbyBackdrop().ignoresSafeArea())
    }
}

struct QuickPlayScreen: View {
    let menu: TennisMenu
    let compact: Bool
    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 24) {
                Text("QUICK PLAY").font(LobbyStyle.display(compact ? 40 : 58)).foregroundStyle(.white)
                Text("A casual solo game. No ranks, no pressure.")
                    .font(compact ? .body : .system(size: 22)).foregroundStyle(.white.opacity(0.75))
                let layout = compact ? AnyLayout(VStackLayout(spacing: 20)) : AnyLayout(HStackLayout(spacing: 24))
                layout {
                    sport("quickTennis", "Tennis", "A relaxed match against AI", "tennisball.fill")
                    sport("quickGolf", "Golf", "A solo round at Cliffside", "figure.golf")
                }
                LobbyAction(title: "Back to Home", icon: "arrow.left", focused: menu.isFocused("back"), compact: compact, primary: false) { menu.tap("back") }
            }.padding(compact ? 24 : 48).frame(maxWidth: 1040).frame(maxWidth: .infinity)
        }.background(LobbyBackdrop().ignoresSafeArea())
    }
    private func sport(_ id: String, _ title: String, _ subtitle: String, _ icon: String) -> some View {
        VStack(alignment: .leading, spacing: 18) {
            Image(systemName: icon).font(.system(size: 40, weight: .medium)).foregroundStyle(LobbyStyle.lime)
            Text(subtitle).font(compact ? .body : .system(size: 20)).foregroundStyle(.white)
            LobbyAction(title: "Play \(title)", focused: menu.isFocused(id), compact: compact) { menu.tap(id) }
        }.padding(24).frame(maxWidth: .infinity, alignment: .leading).background(.white.opacity(0.06))
    }
}

/// The player's name and look, top right of the main menu.
struct PlayerChip: View {
    let player: Player
    let compact: Bool
    var body: some View {
        HStack(spacing: 10) {
            Circle().fill(Color(hex: player.skinHex))
                .overlay(Image(systemName: player.standardFemale ? "person.fill" : "person.fill").foregroundStyle(.white.opacity(0.85)).font(.system(size: 18, weight: .black)))
                .overlay(Circle().strokeBorder(player.outfitColor("shirt") ?? .white, lineWidth: 3))
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
            ("skin", "Skin tone", TennisMenu.skinTones[p.standardSkin], Color(hex: p.skinHex)),
            ("hair", "Hairstyle", CharacterOptions.hair[p.hairStyle], nil),
            ("hairColor", "Hair color", CharacterOptions.hairColors[p.hairColor], Color(hex: p.hairHex)),
            ("face", "Face shape", CharacterOptions.faces[p.faceShape], nil),
            ("build", "Size", "\(Int(p.bodySize * 100))%", nil),
            ("hand", "Plays", p.handedness == .left ? "Left-handed" : "Right-handed", nil),
            ("shirt", "Shirt", p.outfitName("shirt"), p.outfitColor("shirt")),
            ("shorts", "Shorts / skirt", p.outfitName("shorts"), p.outfitColor("shorts")),
            ("accent", "Headband & wristbands", p.outfitName("accent"), p.outfitColor("accent")),
            ("racket", "Racket", p.outfitName("racket"), p.outfitColor("racket")),
        ]
        let layout = AnyLayout(HStackLayout(alignment: .top, spacing: compact ? 10 : 30))
        VStack(alignment: .leading, spacing: compact ? 10 : 14) {
            ArcadeText(text: "CHARACTER", size: compact ? 34 : 46, top: .white, bottom: Arcade.sea)
            layout {
                let list = VStack(spacing: compact ? 8 : 8) {
                    if compact {
                        HStack {
                            Text("NAME").font(Arcade.font(10, .heavy)).tracking(1).foregroundStyle(.white.opacity(0.85))
                            TextField("Your name", text: $name).font(Arcade.font(14)).foregroundStyle(Arcade.gold)
                                .multilineTextAlignment(.trailing).submitLabel(.done)
                                .onSubmit { rename(name) }
                                .onChange(of: name) { _, value in rename(value) }
                        }
                        .padding(.horizontal, 10).padding(.vertical, 12)
                        .background(RoundedRectangle(cornerRadius: 18).fill(Arcade.navyDeep.opacity(0.6)))
                    }
                    ForEach(rows, id: \.0) { row in
                        if row.0 == "build" {
                            CharacterSizeControl(value: Binding(get: { p.bodySize }, set: { menu.setBodySize($0) }), focused: menu.isFocused("build"), compact: compact).id("build")
                        } else {
                        VStack(alignment: .leading, spacing: 6) {
                            Text(row.1.uppercased()).font(Arcade.font(compact ? 10 : 14, .heavy)).foregroundStyle(.white.opacity(0.65))
                            HStack(spacing: 4) {
                                Button { _ = menu.adjust(row.0, by: -1) } label: { Image(systemName: "chevron.left").frame(width: compact ? 26 : 40, height: 44) }.accessibilityLabel("Previous \(row.1)")
                                Text(row.2).font(Arcade.font(compact ? 13 : 22)).frame(maxWidth: .infinity).lineLimit(2).minimumScaleFactor(0.7)
                                Button { _ = menu.adjust(row.0, by: 1) } label: { Image(systemName: "chevron.right").frame(width: compact ? 26 : 40, height: 44) }.accessibilityLabel("Next \(row.1)")
                            }.foregroundStyle(.white)
                        }.padding(compact ? 8 : 12)
                            .background(RoundedRectangle(cornerRadius: 14).fill(menu.isFocused(row.0) ? Arcade.skyDeep : Arcade.navyDeep.opacity(0.8)))
                            .id(row.0)
                            .overlay(alignment: .leading) {
                                if let swatch = row.3 {
                                    Circle().fill(swatch).frame(width: 18, height: 18).overlay(Circle().strokeBorder(.white, lineWidth: 2))
                                        .offset(x: -8)
                                }
                            }
                    }
                    }
                    AnyLayout(compact ? VStackLayout(spacing: 8) : VStackLayout(spacing: 10)) {
                        ArcadeButton(title: "Randomize", icon: "dice.fill", focused: menu.isFocused("randomize"),
                                     top: Arcade.sea, bottom: Arcade.seaDeep, size: compact ? 17 : 20) { menu.tap("randomize") }
                        ArcadeButton(title: "Kit colours", icon: "arrow.uturn.backward", focused: menu.isFocused("reset"),
                                     top: Arcade.skyDeep, bottom: Arcade.navy, size: compact ? 17 : 20) { menu.tap("reset") }
                        ArcadeButton(title: "Done", icon: "checkmark", focused: menu.isFocused("back"),
                                     size: compact ? 17 : 20) { menu.tap("back") }
                    }.padding(.top, 6)
                }
                ScrollViewReader { proxy in
                    ScrollView { list.padding(.horizontal, 6) }
                        .onChange(of: menu.focused) { _, id in withAnimation { proxy.scrollTo(id, anchor: .center) } }
                }
                .frame(maxWidth: compact ? 205 : 500)
                KitPreview(player: p, compact: compact)
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

/// The same Tripo-derived meshes and saved appearance used by the player on court.
struct KitPreview: View {
    let player: Player
    let compact: Bool
    var body: some View {
        VStack(spacing: 12) {
            Text(player.name.uppercased()).font(Arcade.font(compact ? 16 : 28)).foregroundStyle(.white)
            CharacterModelPreview(player: player)
                .frame(maxWidth: .infinity, maxHeight: .infinity)
            Text("DRAG TO ROTATE").font(Arcade.font(compact ? 9 : 13, .heavy)).foregroundStyle(.white.opacity(0.6))
            Text("Saved automatically").font(.caption).foregroundStyle(Arcade.sea)
        }
        .padding(compact ? 8 : 20)
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .background(RoundedRectangle(cornerRadius: 24).fill(Arcade.navyDeep.opacity(0.7)))
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
                        ProgressView(value: session.loading.progress).tint(IslandUI.lime)
                            .accessibilityLabel("Loading progress").accessibilityValue("\(session.loading.percent) percent")
                        Text("\(session.loading.percent)%").font(IslandUI.font(21, bold: true)).monospacedDigit().foregroundStyle(IslandUI.navy)
                    }
                    if launch.sport == .tennis { Button { session.loading.practice() } label: {
                        Label("Swing to practice", systemImage: "tennis.racket").font(IslandUI.font(compact ? 20 : 26, bold: true)).foregroundStyle(IslandUI.navy).padding(.vertical, 12)
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
        if id.hasPrefix("map-"), let v = TennisVenueChoice(rawValue: String(id.dropFirst(4))) { return v.title }
        if id.hasPrefix("rival"), let r = Int(id.dropFirst(5)) {
            let o = TennisCampaign.draw[r]
            return TennisCampaign.shared.unlocked(r) ? "Exhibition · \(o.name)" : "Exhibition · Locked"
        }
        switch id {
        case "homeCampaign": return "Campaign"
        case "campaignPlay": return "Play Round"
        case "quickOpponent": return "Opponent"
        case "quickDifficulty": return "Difficulty"
        case "quickLength": return "Match length"
        case "quickStart": return "Start Match"
        case "homePlay": return "Play with Friends"
        case "quickPlay", "partySolo": return "Quick Play"
        case "quickTennis": return "Casual tennis against AI"; case "quickGolf": return "Solo golf round"
        case "loadingBack": return "Back to Home"; case "loadingRetry": return "Retry loading"
        case "play": return "All sports"; case "character": return "Cosmetics"; case "settings": return "Settings"
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
