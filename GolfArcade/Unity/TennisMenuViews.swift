import AVKit
import SwiftUI

/// The tennis front end's look, matched to the in-game HUD (TennisHud): chunky rounded
/// lettering with a navy outline, sky / sun / gold gradients, and a glow on whatever has focus.
enum Arcade {
    static let navy = Color(red: 0.05, green: 0.10, blue: 0.28)
    static let navyDeep = Color(red: 0.02, green: 0.05, blue: 0.16)
    static let sky = Color(red: 0.25, green: 0.70, blue: 1.0)
    static let skyDeep = Color(red: 0.08, green: 0.34, blue: 0.86)
    static let sun = Color(red: 1.0, green: 0.76, blue: 0.20)
    static let sunDeep = Color(red: 1.0, green: 0.40, blue: 0.12)
    static let gold = Color(red: 1.0, green: 0.90, blue: 0.42)
    static let goldDeep = Color(red: 0.96, green: 0.60, blue: 0.12)
    static let sea = Color(red: 0.12, green: 0.85, blue: 0.78)
    static let seaDeep = Color(red: 0.03, green: 0.48, blue: 0.62)
    static let crimson = Color(red: 0.92, green: 0.14, blue: 0.24)
    static let crimsonDeep = Color(red: 0.36, green: 0.02, blue: 0.08)
    static let lime = Color(red: 0.62, green: 0.93, blue: 0.22)

    static func font(_ size: CGFloat, _ weight: Font.Weight = .black) -> Font { .system(size: size, weight: weight, design: .rounded) }

    /// Card colours by round: the draw warms up toward the final.
    static func roundColors(_ round: Int) -> (Color, Color) {
        switch round {
        case 0, 1: (sea, seaDeep)
        case 2, 3: (sky, skyDeep)
        case 4, 5: (Color(red: 0.72, green: 0.42, blue: 1.0), Color(red: 0.30, green: 0.10, blue: 0.62))
        case 6, 7: (sun, sunDeep)
        default: (crimson, crimsonDeep)
        }
    }
}

/// Outlined, gradient-filled game lettering.
struct ArcadeText: View {
    let text: String
    var size: CGFloat
    var top: Color = .white
    var bottom: Color = Arcade.gold
    var outline: Color = Arcade.navyDeep
    var italic = true
    var body: some View {
        let w = max(1.5, size * 0.045)
        Text(text).font(Arcade.font(size)).italic(italic)
            .foregroundStyle(LinearGradient(colors: [top, bottom], startPoint: .top, endPoint: .bottom))
            .shadow(color: outline, radius: 0, x: w, y: 0).shadow(color: outline, radius: 0, x: -w, y: 0)
            .shadow(color: outline, radius: 0, x: 0, y: w).shadow(color: outline, radius: 0, x: 0, y: -w)
            .shadow(color: outline.opacity(0.6), radius: 0, x: 0, y: w * 2.4)
    }
}

/// Glow, lift and a slow breathe on the focused item; a shake when a choice is refused.
struct FocusGlow: ViewModifier {
    let focused: Bool
    var accent: Color = Arcade.gold
    var corner: CGFloat = 24
    var refusals = 0
    @State private var breathe = false
    func body(content: Content) -> some View {
        content
            .overlay(RoundedRectangle(cornerRadius: corner, style: .continuous)
                .strokeBorder(LinearGradient(colors: [.white, accent], startPoint: .top, endPoint: .bottom), lineWidth: focused ? 5 : 0))
            .shadow(color: focused ? accent.opacity(0.85) : .black.opacity(0.35), radius: focused ? 22 : 8, y: focused ? 0 : 6)
            .scaleEffect(focused ? (breathe ? 1.065 : 1.045) : 1)
            .zIndex(focused ? 1 : 0)
            .modifier(Shake(travel: focused ? CGFloat(refusals) : 0))
            .animation(.spring(response: 0.32, dampingFraction: 0.62), value: focused)
            .animation(.default, value: refusals)
            .onAppear { withAnimation(.easeInOut(duration: 0.9).repeatForever(autoreverses: true)) { breathe = true } }
    }
}

struct Shake: GeometryEffect {
    var travel: CGFloat
    var animatableData: CGFloat { get { travel } set { travel = newValue } }
    func effectValue(size: CGSize) -> ProjectionTransform {
        ProjectionTransform(CGAffineTransform(translationX: 12 * sin(travel * .pi * 6), y: 0))
    }
}

extension View {
    func focusGlow(_ focused: Bool, accent: Color = Arcade.gold, corner: CGFloat = 24, refusals: Int = 0) -> some View {
        modifier(FocusGlow(focused: focused, accent: accent, corner: corner, refusals: refusals))
    }
}

/// A glossy chunky panel, like the scoreboard plaque.
struct Plaque: View {
    var top: Color = Arcade.skyDeep
    var bottom: Color = Arcade.navy
    var corner: CGFloat = 24
    var body: some View {
        RoundedRectangle(cornerRadius: corner, style: .continuous)
            .fill(LinearGradient(colors: [top, bottom], startPoint: .top, endPoint: .bottom))
            .overlay(RoundedRectangle(cornerRadius: corner, style: .continuous).strokeBorder(.white.opacity(0.35), lineWidth: 2))
            .overlay(alignment: .top) {
                RoundedRectangle(cornerRadius: corner, style: .continuous).fill(.white.opacity(0.14))
                    .frame(height: 18).padding(.horizontal, 10).padding(.top, 5)
            }
    }
}

/// The key art, slowly drifting, with a glint crossing it now and then.
struct MenuBackdrop: View {
    var dim = 0.25
    @State private var drift = false
    var body: some View {
        GeometryReader { g in
            ZStack {
                if let art = UIImage(named: "menu-backdrop.jpg") ?? UIImage(named: "menu-backdrop") {
                    Image(uiImage: art).resizable().scaledToFill()
                        .frame(width: g.size.width, height: g.size.height)
                        .scaleEffect(drift ? 1.08 : 1.0, anchor: .topTrailing)
                        .clipped()
                } else {
                    LinearGradient(colors: [Arcade.sky, Arcade.sunDeep], startPoint: .top, endPoint: .bottom)
                }
                LinearGradient(colors: [Arcade.navyDeep.opacity(0.75 * dim + 0.35), .clear, Arcade.navyDeep.opacity(0.9 * dim + 0.2)],
                               startPoint: .leading, endPoint: .trailing)
                LinearGradient(colors: [.clear, Arcade.navyDeep.opacity(0.7)], startPoint: .center, endPoint: .bottom)
            }
            .onAppear { withAnimation(.easeInOut(duration: 24).repeatForever(autoreverses: true)) { drift = true } }
        }
        .ignoresSafeArea()
    }
}

/// "TROPICAL OPEN" with its tennis ball, tilted like a sticker.
struct TennisLogo: View {
    var scale: CGFloat = 1
    @State private var spin = false
    var body: some View {
        VStack(spacing: -14 * scale) {
            HStack(spacing: 10 * scale) {
                TennisBallIcon().frame(width: 64 * scale, height: 64 * scale)
                    .rotationEffect(.degrees(spin ? 360 : 0))
                ArcadeText(text: "TROPICAL", size: 72 * scale, top: .white, bottom: Arcade.sky)
            }
            ArcadeText(text: "OPEN", size: 118 * scale, top: Arcade.gold, bottom: Arcade.sunDeep)
            Text("ISLAND TENNIS CHAMPIONSHIP")
                .font(Arcade.font(15 * scale, .heavy)).tracking(3 * scale).foregroundStyle(Arcade.navy)
                .padding(.horizontal, 18 * scale).padding(.vertical, 7 * scale)
                .background(Capsule().fill(LinearGradient(colors: [Arcade.gold, Arcade.goldDeep], startPoint: .top, endPoint: .bottom)))
                .overlay(Capsule().strokeBorder(Arcade.navyDeep, lineWidth: 2.5 * scale))
                .padding(.top, 22 * scale)
        }
        .rotationEffect(.degrees(-4))
        .onAppear { withAnimation(.linear(duration: 6).repeatForever(autoreverses: false)) { spin = true } }
    }
}

struct TennisBallIcon: View {
    var body: some View {
        ZStack {
            Circle().fill(RadialGradient(colors: [Color(red: 0.9, green: 1, blue: 0.4), Color(red: 0.55, green: 0.8, blue: 0.05)], center: .topLeading, startRadius: 2, endRadius: 60))
            Circle().stroke(Arcade.navyDeep, lineWidth: 3)
            GeometryReader { g in
                let w = g.size.width
                Path { p in
                    p.addArc(center: CGPoint(x: -w * 0.12, y: w * 0.5), radius: w * 0.5, startAngle: .degrees(-50), endAngle: .degrees(50), clockwise: false)
                    p.move(to: CGPoint(x: w * 1.12 + w * 0.5 * cos(.pi * 130 / 180), y: w * 0.5 + w * 0.5 * sin(.pi * 130 / 180)))
                    p.addArc(center: CGPoint(x: w * 1.12, y: w * 0.5), radius: w * 0.5, startAngle: .degrees(130), endAngle: .degrees(230), clockwise: false)
                }.stroke(.white, lineWidth: max(2, w * 0.06))
            }.clipShape(Circle())
        }
    }
}

/// Hero art for an opponent (or the player), bundled with the app.
struct HeroArt: View {
    let name: String
    var silhouette = false
    var body: some View {
        if let image = UIImage(named: "\(name).png") ?? UIImage(named: name) {
            Image(uiImage: image).resizable().scaledToFit()
                .colorMultiply(silhouette ? .black : .white)
                .opacity(silhouette ? 0.85 : 1)
        } else {
            ZStack {
                Circle().fill(LinearGradient(colors: [Arcade.sea, Arcade.seaDeep], startPoint: .top, endPoint: .bottom))
                Circle().strokeBorder(.white.opacity(0.8), lineWidth: 6)
                Image(systemName: "figure.tennis").resizable().scaledToFit().foregroundStyle(.white).padding(48)
            }
            .aspectRatio(1, contentMode: .fit).frame(maxWidth: 260).padding(20)
        }
    }
}

/// Star rating.
struct Stars: View {
    let count: Int
    var of = 4
    var size: CGFloat = 16
    var body: some View {
        HStack(spacing: 2) {
            ForEach(0..<of, id: \.self) { i in
                Image(systemName: i < count ? "star.fill" : "star").font(.system(size: size, weight: .black))
                    .foregroundStyle(i < count ? Arcade.gold : .white.opacity(0.35))
            }
        }
    }
}

/// A chunky pill button used across the menus.
struct ArcadeButton: View {
    let title: String
    var icon: String? = nil
    var focused: Bool
    var top: Color = Arcade.sun
    var bottom: Color = Arcade.sunDeep
    var size: CGFloat = 26
    var action: () -> Void
    var body: some View {
        Button(action: action) {
            HStack(spacing: 10) {
                if let icon { Image(systemName: icon).font(.system(size: size * 0.8, weight: .black)) }
                Text(title).font(Arcade.font(size)).italic().lineLimit(1).fixedSize()
            }
            .foregroundStyle(.white).shadow(color: Arcade.navyDeep, radius: 0, x: 0, y: 2)
            .padding(.horizontal, size * 1.1).padding(.vertical, size * 0.5)
            .background(Capsule().fill(LinearGradient(colors: [top, bottom], startPoint: .top, endPoint: .bottom)))
            .overlay(Capsule().strokeBorder(Arcade.navyDeep, lineWidth: 3))
            .contentShape(Capsule())
        }
        .buttonStyle(.plain)
        .focusGlow(focused, corner: 40)
    }
}

/// The controller legend along the bottom of the TV.
struct HintBar: View {
    var body: some View {
        HStack(spacing: 28) {
            hint("arrow.up.and.down.and.arrow.left.and.right", "Move")
            hint("a.circle.fill", "Select")
            hint("b.circle.fill", "Back")
        }
        .font(Arcade.font(18, .heavy)).foregroundStyle(.white.opacity(0.9))
        .padding(.horizontal, 22).padding(.vertical, 10)
        .background(Capsule().fill(Arcade.navyDeep.opacity(0.7)))
    }
    private func hint(_ icon: String, _ label: String) -> some View {
        HStack(spacing: 8) { Image(systemName: icon).foregroundStyle(Arcade.gold); Text(label) }
    }
}

// MARK: - Screens

/// The menu itself. `compact` is the phone held upright with no TV: the same screens,
/// stacked, and touch selects. Otherwise it is laid out for a 16:9 TV and driven by the remote.
struct TennisMenuScreen: View {
    @Bindable var menu = TennisMenu.shared
    var compact: Bool
    var body: some View {
        Group {
            switch menu.screen {
            case .title: TitleScreen(menu: menu, compact: compact)
            case .main: MainScreen(menu: menu, compact: compact)
            case .gameSelect: GameSelectScreen(menu: menu, compact: compact)
            case .hub(let sport): HubScreen(menu: menu, sport: sport, compact: compact)
            case .locked(let sport): LockedScreen(menu: menu, sport: sport, compact: compact)
            case .campaign: CampaignScreen(menu: menu, compact: compact)
            case .exhibition: ExhibitionScreen(menu: menu, compact: compact)
            case .training: TrainingScreen(menu: menu, compact: compact)
            case .character: CharacterScreen(menu: menu, compact: compact)
            case .settings: SettingsScreen(menu: menu, compact: compact)
            case .howTo: HowToScreen(menu: menu, compact: compact)
            case .golfLesson: GolfLessonScreen(menu: menu, compact: compact)
            case .connect: ConnectScreen(menu: menu, compact: compact)
            case .loading: LoadingScreen(menu: menu, compact: compact)
            case .results: ResultsScreen(menu: menu, compact: compact)
            case .story: StoryScreen(menu: menu, compact: compact)
            }
        }
        .transition(.asymmetric(insertion: .scale(scale: 1.08).combined(with: .opacity), removal: .opacity))
        .animation(.spring(response: 0.45, dampingFraction: 0.82), value: menu.screen)
    }
}

private struct CampaignScreen: View {
    let menu: TennisMenu
    let compact: Bool
    var body: some View {
        let campaign = TennisCampaign.shared
        let focusedRound = Int(menu.focused.dropFirst(5)) ?? campaign.nextRound
        let shown = TennisCampaign.draw[menu.focused.hasPrefix("round") ? focusedRound : campaign.nextRound]
        let shownRound = TennisCampaign.draw.firstIndex(of: shown) ?? 0
        VStack(spacing: compact ? 14 : 12) {
            HStack(alignment: .firstTextBaseline) {
                ArcadeText(text: "ISLAND CIRCUIT", size: compact ? 34 : 44)
                Spacer()
                if campaign.titles > 0 {
                    Label("× \(campaign.titles)", systemImage: "trophy.fill").font(Arcade.font(compact ? 18 : 24)).foregroundStyle(Arcade.gold)
                }
            }
            Text(campaign.champion ? "You are the Tropical Open champion. Replay any round, or start a new tournament."
                 : "Ten rivals stand between you and the trophy. Each one is tougher than the last — and Viktor waits at the end.")
                .font(Arcade.font(compact ? 15 : 16, .semibold)).foregroundStyle(.white.opacity(0.9))
                .frame(maxWidth: .infinity, alignment: .leading)
            if compact {
                ScrollView {
                    VStack(spacing: 16) {
                        ForEach(Array(TennisCampaign.draw.enumerated()), id: \.offset) { i, o in
                            OpponentCard(opponent: o, round: i, compact: true, focused: menu.isFocused("round\(i)"), refusals: menu.refusals)
                                .onTapGesture { menu.tap("round\(i)") }
                        }
                        buttons
                    }.padding(.vertical, 18).padding(.horizontal, 14)
                }
            } else {
                VStack(spacing: 12) {
                    ForEach(0..<2, id: \.self) { half in
                        HStack(spacing: 14) {
                            ForEach(half * 5..<half * 5 + 5, id: \.self) { i in
                                OpponentCard(opponent: TennisCampaign.draw[i], round: i, compact: false,
                                             focused: menu.isFocused("round\(i)"), refusals: menu.refusals)
                                    .onTapGesture { menu.tap("round\(i)") }
                                if i % 5 < 4 {
                                    Image(systemName: "chevron.right").font(.system(size: 18, weight: .black)).foregroundStyle(Arcade.gold)
                                }
                            }
                        }
                    }
                }
                OpponentDetail(opponent: shown, round: shownRound, notice: menu.notice)
                HStack(spacing: 22) { buttons; Spacer(); HintBar() }
            }
            if compact && !menu.notice.isEmpty {
                Text(menu.notice).font(Arcade.font(15, .bold)).foregroundStyle(Arcade.gold)
            }
        }
        .padding(.horizontal, compact ? 20 : 40).padding(.top, compact ? 20 : 30).padding(.bottom, compact ? 20 : 18)
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .top)
    }

    @ViewBuilder private var buttons: some View {
        ArcadeButton(title: "Back", icon: "chevron.left", focused: menu.isFocused("back"),
                     top: Arcade.skyDeep, bottom: Arcade.navy, size: compact ? 20 : 22) { menu.tap("back") }
        ArcadeButton(title: "New tournament", icon: "arrow.counterclockwise", focused: menu.isFocused("restart"),
                     top: Arcade.skyDeep, bottom: Arcade.navy, size: compact ? 20 : 22) { menu.tap("restart") }
    }
}

/// One opponent on the draw: their art (a silhouette until unlocked), round, name and rating.
struct OpponentCard: View {
    let opponent: TennisOpponent
    let round: Int
    let compact: Bool
    let focused: Bool
    let refusals: Int
    var body: some View {
        let campaign = TennisCampaign.shared
        let locked = !campaign.unlocked(round)
        let beaten = campaign.beaten(round)
        let colors = Arcade.roundColors(round)
        ZStack(alignment: compact ? .leading : .bottom) {
            Plaque(top: locked ? Color(white: 0.28) : colors.0, bottom: locked ? Color(white: 0.1) : colors.1, corner: 24)
            if opponent.boss && !locked {
                RadialGradient(colors: [Arcade.crimson.opacity(0.7), .clear], center: .center, startRadius: 10, endRadius: 180)
            }
            HeroArt(name: opponent.art, silhouette: locked)
                .frame(width: compact ? 120 : 140, height: compact ? 150 : 150)
                .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: compact ? .trailing : .top)
                .offset(y: compact ? 8 : 6)
            VStack(alignment: compact ? .leading : .center, spacing: 4) {
                HStack(spacing: 8) {
                    Text(opponent.round).font(Arcade.font(compact ? 12 : 10, .heavy)).tracking(1).lineLimit(1)
                        .foregroundStyle(Arcade.navyDeep)
                        .padding(.horizontal, 10).padding(.vertical, 4)
                        .background(Capsule().fill(LinearGradient(colors: [Arcade.gold, Arcade.goldDeep], startPoint: .top, endPoint: .bottom)))
                    if compact { badge(locked: locked, beaten: beaten).scaleEffect(0.85) }
                }
                ArcadeText(text: locked ? "???" : opponent.name.components(separatedBy: " ")[0].uppercased(), size: compact ? 28 : 22)
                if !locked { Text(compact ? "\(opponent.nickname) · \(opponent.formatTitle)" : opponent.nickname).font(Arcade.font(compact ? 14 : 12, .bold)).foregroundStyle(.white).lineLimit(1) }
                Stars(count: opponent.stars, of: 5, size: compact ? 13 : 11)
            }
            .padding(compact ? 18 : 8)
            .frame(maxWidth: .infinity, alignment: compact ? .leading : .center)
            .background(compact ? nil : LinearGradient(colors: [.clear, Arcade.navyDeep.opacity(0.85)], startPoint: .top, endPoint: .bottom))
        }
        .overlay(alignment: .topLeading) { if !compact { badge(locked: locked, beaten: beaten).scaleEffect(0.7, anchor: .topLeading).padding(8) } }
        .frame(width: compact ? nil : 196, height: compact ? 150 : 205)
        .frame(maxWidth: compact ? .infinity : nil)
        .clipShape(RoundedRectangle(cornerRadius: 24, style: .continuous))
        .focusGlow(focused, accent: opponent.boss ? Arcade.crimson : Arcade.gold, refusals: refusals)
        .contentShape(Rectangle())
    }
}

extension OpponentCard {
    @ViewBuilder func badge(locked: Bool, beaten: Bool) -> some View {
        if locked {
            Image(systemName: "lock.fill").font(.system(size: 24, weight: .black)).foregroundStyle(.white.opacity(0.85))
        } else if beaten {
            Label("WON", systemImage: "checkmark.seal.fill").font(Arcade.font(15)).foregroundStyle(.white)
                .padding(.horizontal, 10).padding(.vertical, 5).background(Capsule().fill(Color.green))
        } else {
            Text("NEXT").font(Arcade.font(15)).foregroundStyle(Arcade.navyDeep)
                .padding(.horizontal, 10).padding(.vertical, 5).background(Capsule().fill(.white))
        }
    }
}

private struct OpponentDetail: View {
    let opponent: TennisOpponent
    let round: Int
    let notice: String
    var body: some View {
        let campaign = TennisCampaign.shared
        let locked = !campaign.unlocked(round)
        HStack(spacing: 18) {
            VStack(alignment: .leading, spacing: 4) {
                Text(locked ? "LOCKED" : "\(opponent.name.uppercased()) · \(opponent.nickname.uppercased()) · \(opponent.formatTitle.uppercased())")
                    .font(Arcade.font(18)).foregroundStyle(opponent.boss ? Arcade.crimson : Arcade.gold)
                Text(notice.isEmpty ? (locked ? "Win the previous round to reveal this opponent." : opponent.blurb) : notice)
                    .font(Arcade.font(16, .semibold)).foregroundStyle(.white).lineLimit(2)
            }
            Spacer()
            if !locked {
                Text(campaign.beaten(round) ? "Ⓐ Replay" : "Ⓐ Play match").font(Arcade.font(22)).italic().foregroundStyle(Arcade.gold)
            }
        }
        .padding(.horizontal, 22).padding(.vertical, 10)
        .background(RoundedRectangle(cornerRadius: 18).fill(Arcade.navyDeep.opacity(0.75)))
    }
}

private struct TrainingScreen: View {
    let menu: TennisMenu
    let compact: Bool
    var body: some View {
        let level = TennisMenu.trainingLevels[menu.trainingLevel]
        VStack(alignment: .leading, spacing: compact ? 16 : 24) {
            ArcadeText(text: "TRAINING COURT", size: compact ? 36 : 56, top: .white, bottom: Arcade.sea)
            Text("A free rally against the club coach. No score pressure: work on timing the ball at the top of its bounce, aiming with the racket face, and hitting the sweet spot. Your controller shows every contact on the strings.")
                .font(Arcade.font(compact ? 16 : 21, .semibold)).foregroundStyle(.white).frame(maxWidth: 760, alignment: .leading)
            ChoiceRow(label: "Coach level", value: level.name, focused: menu.isFocused("level"), compact: compact) { menu.tap("level") }
            HStack(spacing: 20) {
                ArcadeButton(title: "Start training", icon: "play.fill", focused: menu.isFocused("start"),
                             top: Arcade.sea, bottom: Arcade.seaDeep, size: compact ? 24 : 30) { menu.tap("start") }
                ArcadeButton(title: "Back", icon: "chevron.left", focused: menu.isFocused("back"),
                             top: Arcade.skyDeep, bottom: Arcade.navy, size: compact ? 20 : 24) { menu.tap("back") }
            }
            Spacer(minLength: 0)
            if !compact { HintBar() }
        }
        .padding(compact ? 22 : 60)
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
    }
}

/// "Label   ‹ value ›", changed with left/right (or a tap, which steps forward).
struct ChoiceRow: View {
    let label: String
    let value: String
    let focused: Bool
    let compact: Bool
    var action: () -> Void
    var body: some View {
        HStack {
            Text(label.uppercased()).font(Arcade.font(compact ? 16 : 22, .heavy)).tracking(1).foregroundStyle(.white.opacity(0.85))
            Spacer()
            HStack(spacing: 14) {
                Image(systemName: "chevron.left").opacity(focused ? 1 : 0.3)
                Text(value).font(Arcade.font(compact ? 20 : 26)).italic().frame(minWidth: compact ? 90 : 150)
                Image(systemName: "chevron.right").opacity(focused ? 1 : 0.3)
            }.foregroundStyle(focused ? Arcade.gold : .white)
        }
        .padding(.horizontal, 22).padding(.vertical, compact ? 12 : 11)
        .background(RoundedRectangle(cornerRadius: 18).fill(Arcade.navyDeep.opacity(focused ? 0.9 : 0.6)))
        .frame(maxWidth: 760)
        .focusGlow(focused, corner: 18)
        .contentShape(Rectangle())
        .onTapGesture(perform: action)
    }
}

struct AirPlayButton: UIViewRepresentable {
    func makeUIView(context: Context) -> AVRoutePickerView {
        let view = AVRoutePickerView()
        view.tintColor = .white; view.activeTintColor = UIColor(Arcade.gold)
        view.prioritizesVideoDevices = true
        return view
    }
    func updateUIView(_ view: AVRoutePickerView, context: Context) {}
}

private struct ResultsScreen: View {
    let menu: TennisMenu
    let compact: Bool
    @State private var pop = false
    var body: some View {
        let campaign = TennisCampaign.shared
        let result = menu.result
        let round = result?.round ?? 0
        let opponent = TennisCampaign.draw[round]
        let won = result?.won ?? false
        let crowned = won && round == TennisCampaign.draw.count - 1
        let layout = compact ? AnyLayout(VStackLayout(spacing: 12)) : AnyLayout(HStackLayout(spacing: 40))
        ZStack {
            (won ? LinearGradient(colors: [Arcade.sun.opacity(0.55), Arcade.navyDeep.opacity(0.9)], startPoint: .top, endPoint: .bottom)
                 : LinearGradient(colors: [Arcade.crimsonDeep.opacity(0.8), Arcade.navyDeep.opacity(0.95)], startPoint: .top, endPoint: .bottom))
                .ignoresSafeArea()
            if crowned { Confetti() }
            layout {
                if crowned {
                    HeroArt(name: "menu-trophy").frame(maxWidth: compact ? 200 : 380, maxHeight: compact ? 220 : 460)
                        .scaleEffect(pop ? 1 : 0.4).rotationEffect(.degrees(pop ? 0 : -20))
                } else {
                    HeroArt(name: opponent.art).frame(maxWidth: compact ? 160 : 360, maxHeight: compact ? 220 : 480)
                        .saturation(won ? 0.4 : 1).opacity(won ? 0.75 : 1)
                }
                VStack(alignment: compact ? .center : .leading, spacing: compact ? 10 : 16) {
                    ArcadeText(text: crowned ? "CHAMPION!" : won ? "VICTORY!" : "DEFEAT",
                               size: compact ? 58 : 96, top: won ? .white : Color(white: 0.9), bottom: won ? Arcade.gold : Arcade.crimson)
                        .scaleEffect(pop ? 1 : 1.8).opacity(pop ? 1 : 0)
                    Text(won ? "You beat \(opponent.name) \(result?.score ?? "")" : "\(opponent.name) wins \(result.map { String($0.score.reversed()) } ?? "")")
                        .font(Arcade.font(compact ? 20 : 30)).foregroundStyle(.white)
                    Text(crowned ? "Tropical Open champion. Viktor \"The Wall\" has finally fallen — and Old Ray finally gets his final back."
                         : won ? "Coach Ray: \"Next up, \(campaign.next.roundTitle.lowercased()): \(campaign.next.name), \(campaign.next.nickname). \(campaign.next.formatTitle).\""
                         : "Coach Ray: \"Shake it off. \(opponent.blurb)\"")
                        .font(Arcade.font(compact ? 15 : 20, .semibold)).foregroundStyle(.white.opacity(0.9))
                        .multilineTextAlignment(compact ? .center : .leading).frame(maxWidth: 560)
                    let items = menu.rows(.results).flatMap { $0 }
                    VStack(alignment: compact ? .center : .leading, spacing: 12) {
                        ForEach(items, id: \.self) { id in
                            ArcadeButton(title: label(id, crowned: crowned), icon: icon(id), focused: menu.isFocused(id),
                                         top: id == "menu" ? Arcade.skyDeep : Arcade.sun, bottom: id == "menu" ? Arcade.navy : Arcade.sunDeep,
                                         size: compact ? 20 : 26) { menu.tap(id) }
                        }
                    }.padding(.top, 10)
                }
            }
            .padding(compact ? 20 : 60)
        }
        .onAppear { withAnimation(.spring(response: 0.5, dampingFraction: 0.55).delay(0.2)) { pop = true } }
    }
    private func label(_ id: String, crowned: Bool) -> String {
        switch id {
        case "continue": "Next match"
        case "retry": "Rematch"
        case "restart": "New tournament"
        default: crowned ? "Back to the draw" : "The draw"
        }
    }
    private func icon(_ id: String) -> String {
        switch id { case "continue": "play.fill"; case "retry": "arrow.clockwise"; case "restart": "arrow.counterclockwise"; default: "list.bullet" }
    }
}

/// A story scene: the speaker's portrait, their name plaque, and the line typing out. A / tap
/// finishes the line, then moves on; B / Skip jumps to what comes after.
private struct StoryScreen: View {
    let menu: TennisMenu
    let compact: Bool
    @State private var shown = 0
    @State private var typing: Task<Void, Never>?
    var body: some View {
        let line = menu.storyLine ?? StoryLine(speaker: "ray", text: "")
        let s = SportsSession.shared
        let female = s.players.indices.contains(s.playerIndex) && s.players[s.playerIndex].standardFemale
        let rival = TennisCampaign.draw.first { $0.key == line.speaker }
        let accent: Color = line.speaker == "ray" || line.speaker == "stranger" ? Arcade.sea
            : line.speaker == "you" ? Arcade.sun : rival?.boss == true ? Arcade.crimson : rival != nil ? Arcade.sunDeep : Arcade.gold
        let text = String(line.text.prefix(shown))
        ZStack(alignment: .bottom) {
            LinearGradient(colors: [.clear, Arcade.navyDeep.opacity(0.85)], startPoint: .top, endPoint: .bottom).ignoresSafeArea()
            HeroArt(name: TennisStory.art(for: line.speaker, female: female), silhouette: line.speaker == "stranger")
                .frame(maxHeight: compact ? 360 : 560)
                .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: line.speaker == "you" ? .bottomTrailing : .bottomLeading)
                .padding(.horizontal, compact ? 0 : 70).padding(.bottom, compact ? 190 : 120)
                .id(line.speaker).transition(.move(edge: line.speaker == "you" ? .trailing : .leading).combined(with: .opacity))
            VStack(alignment: .leading, spacing: 10) {
                Text(TennisStory.name(for: line.speaker)).font(Arcade.font(compact ? 18 : 24, .heavy)).tracking(2)
                    .foregroundStyle(Arcade.navyDeep).padding(.horizontal, 18).padding(.vertical, 6)
                    .background(Capsule().fill(LinearGradient(colors: [.white, accent], startPoint: .top, endPoint: .bottom)))
                    .overlay(Capsule().strokeBorder(Arcade.navyDeep, lineWidth: 3))
                    .offset(y: compact ? -22 : -28).padding(.bottom, compact ? -22 : -28)
                Text(text).font(Arcade.font(compact ? 18 : 27, .bold)).foregroundStyle(.white)
                    .frame(maxWidth: .infinity, minHeight: compact ? 110 : 120, alignment: .topLeading)
                HStack {
                    Text("\(menu.storyIndex + 1) / \(menu.story.count)").font(Arcade.font(compact ? 13 : 16, .heavy)).foregroundStyle(.white.opacity(0.5))
                    Spacer()
                    ArcadeButton(title: "Skip", icon: "forward.end.fill", focused: menu.isFocused("skip"),
                                 top: Arcade.skyDeep, bottom: Arcade.navy, size: compact ? 16 : 18) { menu.tap("skip") }
                    ArcadeButton(title: shown < line.text.count ? "…" : "Next", icon: "chevron.right", focused: menu.isFocused("next"),
                                 size: compact ? 16 : 18) { advance(line) }
                }
            }
            .padding(.horizontal, compact ? 20 : 34).padding(.top, compact ? 26 : 30).padding(.bottom, compact ? 16 : 20)
            .background(Plaque(top: Arcade.navy, bottom: Arcade.navyDeep, corner: 26).opacity(0.95))
            .overlay(RoundedRectangle(cornerRadius: 26, style: .continuous).strokeBorder(accent.opacity(0.8), lineWidth: 3))
            .padding(.horizontal, compact ? 12 : 60).padding(.bottom, compact ? 20 : 34)
        }
        .contentShape(Rectangle())
        .onTapGesture { advance(line) }
        .onAppear { if menu.storyInstant { shown = line.text.count } else { type(line) } }
        .onChange(of: menu.storyIndex) { _, _ in type(menu.storyLine ?? line) }
        .animation(.spring(response: 0.4, dampingFraction: 0.8), value: line.speaker)
    }

    /// A tap mid-line shows the whole line; a tap on a finished line moves on.
    private func advance(_ line: StoryLine) {
        if shown < line.text.count { typing?.cancel(); shown = line.text.count } else { menu.advanceStory() }
    }

    private func type(_ line: StoryLine) {
        typing?.cancel(); shown = 0
        typing = Task { @MainActor in
            while shown < line.text.count, !Task.isCancelled {
                try? await Task.sleep(for: .milliseconds(24)); shown += 1
            }
        }
    }
}

/// Falling confetti for the title.
struct Confetti: View {
    @State private var fall = false
    private let pieces = (0..<70).map { _ in (x: CGFloat.random(in: 0...1), delay: Double.random(in: 0...2.5), hue: Double.random(in: 0...1), size: CGFloat.random(in: 8...16), spin: Double.random(in: -360...360)) }
    var body: some View {
        GeometryReader { g in
            ForEach(0..<pieces.count, id: \.self) { i in
                let p = pieces[i]
                RoundedRectangle(cornerRadius: 2).fill(Color(hue: p.hue, saturation: 0.8, brightness: 1))
                    .frame(width: p.size, height: p.size * 0.5)
                    .rotationEffect(.degrees(fall ? p.spin : 0))
                    .position(x: p.x * g.size.width, y: fall ? g.size.height + 40 : -40)
                    .animation(.linear(duration: 3.2).delay(p.delay).repeatForever(autoreverses: false), value: fall)
            }
        }
        .ignoresSafeArea()
        .onAppear { fall = true }
    }
}
