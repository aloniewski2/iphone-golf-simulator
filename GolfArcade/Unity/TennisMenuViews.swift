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
    @Environment(\.accessibilityReduceMotion) private var reduceMotion
    var body: some View {
        ClubCameraHost(route: menu.screen, reducedMotion: true, paused: SportsSession.shared.menuPauseVisible, content: AnyView(screenContent))
            .safeAreaInset(edge: .top) {
                if menu.screen != .title && menu.screen != .main && !SportsSession.shared.menuPauseVisible {
                    HStack {
                        Button { menu.back() } label: { Label("Back", systemImage: "chevron.left") }
                            .accessibilityIdentifier("menu-back")
                        Spacer()
                        Button("Main Menu") { menu.goHome() }.accessibilityIdentifier("menu-home")
                    }
                    .font(IslandUI.font(compact ? 16 : 22, bold: true)).foregroundStyle(IslandUI.navy)
                    .padding(.horizontal, compact ? 22 : 40).frame(minHeight: compact ? 48 : 60)
                    .background(IslandUI.paper)
                }
            }
    }
    private var screenContent: some View {
        Group {
            if SportsSession.shared.menuPauseVisible { IslandPauseScreen(compact: compact) }
            else { switch menu.screen {
            case .title: IslandTitleScreen(menu: menu, compact: compact)
            case .main: IslandHomeScreen(menu: menu, compact: compact)
            case .party, .multiplayer, .localChoice, .localPlayers, .onlineChoice: IslandPartyScreen(menu: menu, compact: compact)
            case .homeEmotes: HomeEmoteScreen(menu: menu, compact: compact)
            case .online(.loading): LoadingScreen(menu: menu, compact: compact)
            case .online(let route): IslandOnlineScreen(menu: menu, route: route, compact: compact)
            case .gameSelect: IslandSportsScreen(menu: menu, compact: compact)
            case .hub(let sport): IslandHubScreen(menu: menu, sport: sport, compact: compact)
            case .locked(let sport): ClubLockedScreen(menu: menu, sport: sport, compact: compact)
            case .campaign: IslandLadderScreen(menu: menu, compact: compact, exhibition: false)
            case .exhibition: IslandLadderScreen(menu: menu, compact: compact, exhibition: true)
            case .training: IslandTrainingScreen(menu: menu, compact: compact)
            case .character: IslandLockerScreen(menu: menu, compact: compact)
            case .settings: IslandSettingsScreen(menu: menu, compact: compact)
            case .howTo: IslandGuideScreen(menu: menu, compact: compact, golf: false)
            case .connect: IslandConnectScreen(menu: menu, compact: compact)
            case .loading: LoadingScreen(menu: menu, compact: compact)
            case .results: IslandResultsScreen(menu: menu, compact: compact)
            case .story: IslandStoryScreen(menu: menu, compact: compact)
            case .map: if menu.mapSport == .golf { GolfCourseScreen(menu: menu, compact: compact) } else { IslandCourtScreen(menu: menu, compact: compact) }
            case .postMatch: IslandResultsScreen(menu: menu, compact: compact, postMatch: true)
            } }
        }.transaction { $0.animation = nil; $0.disablesAnimations = true }
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


