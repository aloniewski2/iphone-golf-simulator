import SwiftUI
import SceneKit

/// Shared scenery destinations; the UI stays native and the equipped avatar stays live.
enum ClubRoom: String {
    case entrance, terrace, locker, loading
    static func forTitle(_ title: String) -> ClubRoom {
        switch title {
        case "Locker": .locker
        case "Loading match", "Training", "Choose your court", "Choose a court", "Select a court", "Quick Match", "Tennis", "Golf", "How to Play", "Golf Lesson", "Match Won", "Match Lost": .loading
        default: .terrace
        }
    }
}

enum ClubPreviewActivity: String { case play, locker, settings, store }

struct ClubMenuArc: Shape {
    func path(in r: CGRect) -> Path {
        var p = Path()
        p.move(to: CGPoint(x: 0, y: 0))
        p.addCurve(to: CGPoint(x: 0, y: r.height), control1: CGPoint(x: r.width, y: r.height * 0.22), control2: CGPoint(x: r.width, y: r.height * 0.78))
        return p
    }
}

struct MotionClubHome: View {
    let menu: TennisMenu
    let compact: Bool
    @Environment(\.accessibilityReduceMotion) private var reduceMotion
    private let items = [("play", "Play", "play.fill"), ("character", "Locker", "tshirt"), ("homeEmotes", "Emotes", "face.smiling"), ("settings", "Settings", "gearshape")]
    var body: some View {
        GeometryReader { g in
            let w = g.size.width, h = g.size.height
            ZStack {
                IslandBackdrop()
                if compact { LinearGradient(colors: [.clear, IslandUI.paper.opacity(0.95)], startPoint: .top, endPoint: .bottom).ignoresSafeArea() }
                CharacterModelPreview(player: menu.player ?? Player(name: "Player 1", colorIndex: 0), cameraDistance: compact ? 3.4 : 3.25, outfitSport: menu.homeSport, menuActivity: .play)
                    .frame(width: w * (compact ? 0.87 : 0.56), height: h * (compact ? 0.48 : 0.86))
                    .position(x: w * (compact ? 0.55 : 0.73), y: h * (compact ? 0.30 : 0.56))
                    .allowsHitTesting(false)
                Button { menu.tap("homeInvite") } label: {
                    Image(systemName: "plus").font(.system(size: compact ? 24 : 32, weight: .bold))
                        .foregroundStyle(IslandUI.navy).frame(width: 54, height: 54)
                        .islandFocus(menu.isFocused("homeInvite"), radius: 27, base: IslandUI.paper)
                }.buttonStyle(.plain).accessibilityLabel("Invite friends to your lobby").accessibilityIdentifier("home-invite")
                    .position(x: w * (compact ? 0.86 : 0.92), y: h * (compact ? 0.31 : 0.53))
                IslandWordmark(size: compact ? 29 : 46)
                    .position(x: w * (compact ? 0.23 : 0.205), y: h * (compact ? 0.085 : 0.145))
                menuCards(width: compact ? w * 0.81 : w * 0.34, height: compact ? min(320, h * 0.44) : h * 0.59)
                    .position(x: w * (compact ? 0.48 : 0.235), y: h * (compact ? 0.735 : 0.565))
                VStack {
                    HStack { Spacer(); IslandProfilePill(player: menu.player, compact: compact) }
                    Spacer()
                    if !menu.notice.isEmpty { Text(menu.notice).font(IslandUI.font(16)).foregroundStyle(IslandUI.navy).padding(10).background(IslandUI.paper, in: Capsule()).accessibilityAddTraits(.updatesFrequently) }
                }.padding(compact ? 16 : 32).allowsHitTesting(false)
            }
        }.preferredColorScheme(.light)
    }
    private func menuCards(width: CGFloat, height: CGFloat) -> some View {
        ZStack(alignment: .leading) {
            ClubMenuArc().stroke(Color(hex: "B89A55"), lineWidth: compact ? 2 : 3)
                .frame(width: width * 0.25, height: height * 1.15).offset(x: -width * 0.075)
            VStack(spacing: compact ? 10 : 20) {
                ForEach(Array(items.enumerated()), id: \.element.0) { index, item in
                    let selected = menu.isFocused(item.0)
                    Button { menu.tap(item.0) } label: {
                        HStack(spacing: compact ? 14 : 22) {
                            Image(systemName: item.2).font(.system(size: compact ? 23 : 31, weight: .semibold)).frame(width: compact ? 27 : 36)
                            VStack(alignment: .leading, spacing: 0) {
                                Text(item.1).font(IslandUI.font(compact ? 25 : 36, bold: true))
                            }
                            Spacer(minLength: 0)
                            if selected { Image(systemName: "chevron.right").font(.system(size: 20, weight: .bold)) }
                        }
                        .foregroundStyle(IslandUI.navy)
                        .padding(.horizontal, compact ? 18 : 26).frame(maxWidth: .infinity, maxHeight: .infinity)
                        .background(selected ? IslandUI.lime : IslandUI.paper, in: RoundedRectangle(cornerRadius: compact ? 18 : 22))
                        .overlay(RoundedRectangle(cornerRadius: compact ? 18 : 22).strokeBorder(IslandUI.navy, lineWidth: selected ? 3.5 : 0))
                        .overlay(RoundedRectangle(cornerRadius: compact ? 18 : 22).strokeBorder(.white.opacity(0.85), lineWidth: 1.5))
                        .shadow(color: Color.black.opacity(0.13), radius: selected ? 9 : 5, y: selected ? 6 : 3)
                    }
                    .buttonStyle(.plain)
                    .scaleEffect(selected ? 1.035 : 1)
                    .offset(x: index == 1 || index == 2 ? width * 0.06 : -width * 0.035)
                    .onHover { over in if over { menu.focus(item.0) } }
                    .accessibilityLabel(item.1)
                    .accessibilityHint("Open \(item.1)")
                    .accessibilityIdentifier("home-\(item.0)")
                    .accessibilityAddTraits(selected ? .isSelected : [])
                }
            }.frame(width: width, height: height)
        }.frame(width: width, height: height)

    }
}

/// A literal door-like hinge swing between native menu destinations.
/// Hosts native menu content with immediate, stationary screen changes.
struct ClubCameraHost: UIViewControllerRepresentable {
    let route: MenuScreen
    let reducedMotion: Bool
    var paused = false
    let content: AnyView
    func makeUIViewController(context: Context) -> Controller { Controller(content, route: route, paused: paused) }
    func updateUIViewController(_ controller: Controller, context: Context) { controller.show(content, route: route, paused: paused, reduced: reducedMotion) }
    @MainActor final class Controller: UIViewController {
        private let host: UIHostingController<AnyView>
        init(_ content: AnyView, route: MenuScreen, paused: Bool) { host = UIHostingController(rootView: content); super.init(nibName: nil, bundle: nil) }
        required init?(coder: NSCoder) { fatalError("init(coder:) has not been implemented") }
        override func viewDidLoad() {
            super.viewDidLoad(); view.backgroundColor = UIColor(IslandUI.paper); view.clipsToBounds = true
            addChild(host); host.safeAreaRegions = []; host.view.backgroundColor = .clear
            host.view.frame = view.bounds; host.view.autoresizingMask = [.flexibleWidth, .flexibleHeight]
            view.addSubview(host.view); host.didMove(toParent: self)
        }
        func show(_ content: AnyView, route: MenuScreen, paused: Bool, reduced: Bool) { host.rootView = content }
        func stop() { }
    }
}

/// Display-only progression: the match transaction has already awarded these points.
struct ClubVictorySummary: View {
    let won: Bool
    let score: String
    let summary: PostMatchSummary?
    var compact = false
    private var earned: Int { summary?.total ?? 0 }

    private var progress: (level: Int, xp: Int) {
        guard let summary else { return (1, 0) }
        var level = summary.startLevel, xp = summary.startXP + earned
        while level < LevelCurve.maxLevel && xp >= LevelCurve.needed(level) {
            xp -= LevelCurve.needed(level); level += 1
        }
        return (level, min(xp, LevelCurve.needed(level)))
    }
    var body: some View {
        VStack(alignment: .leading, spacing: compact ? 14 : 20) {
            HStack(spacing: 12) {
                Image(systemName: won ? "trophy.fill" : "tennisball.fill")
                    .font(.system(size: compact ? 26 : 38))
                    .foregroundStyle(Color(red: 0.75, green: 0.52, blue: 0.16))
                Text(won ? "VICTORY" : "MATCH COMPLETE")
                    .font(IslandUI.font(compact ? 34 : 64, bold: true)).tracking(compact ? 1 : 3)
                    .minimumScaleFactor(0.6).lineLimit(1)
            }
            HStack(alignment: .firstTextBaseline, spacing: 14) {
                Text(score).font(IslandUI.font(compact ? 30 : 42, bold: true)).minimumScaleFactor(0.6).lineLimit(1)
                if let summary { Text("vs. \(summary.opponent)").font(IslandUI.font(compact ? 16 : 20)).foregroundStyle(IslandUI.muted) }
            }
            if let summary {
                VStack(alignment: .leading, spacing: 12) {
                    HStack(alignment: .firstTextBaseline) {
                        VStack(alignment: .leading, spacing: 4) {
                            Text(progress.level > summary.startLevel ? "LEVEL UP" : "CLUB PROGRESS")
                                .font(IslandUI.font(12, bold: true)).tracking(2)
                            Text("Level \(progress.level)").font(IslandUI.font(compact ? 24 : 30, bold: true))
                        }
                        Spacer()
                        Text("+\(earned) XP").font(IslandUI.font(compact ? 28 : 36, bold: true)).monospacedDigit()
                    }
                    GeometryReader { g in
                        Capsule().fill(.white.opacity(0.2))
                        Capsule().fill(IslandUI.lime)
                            .frame(width: max(0, g.size.width * CGFloat(progress.xp) / CGFloat(LevelCurve.needed(progress.level))))
                    }.frame(height: 10)
                    HStack {
                        Text(LevelCurve.rank(progress.level))
                        Spacer()
                        Text(progress.level >= LevelCurve.maxLevel ? "Max level" : "\(progress.xp) / \(LevelCurve.needed(progress.level)) XP")
                            .monospacedDigit()
                    }.font(IslandUI.font(13))
                }.padding(compact ? 18 : 24).foregroundStyle(.white)
                    .background(IslandUI.navy, in: RoundedRectangle(cornerRadius: 20))
                    .accessibilityElement(children: .ignore)
                    .accessibilityLabel("Earned \(summary.total) XP. Level \(summary.endLevel), \(summary.endXP) XP.")
                HStack(spacing: compact ? 20 : 36) {
                    stat("ACES", summary.stats.aces)
                    stat("WINNERS", summary.stats.winners)
                    stat("BEST RALLY", summary.stats.longest)
                }
            }
        }.foregroundStyle(IslandUI.navy)

    }
    private func stat(_ title: String, _ value: Int) -> some View {
        VStack(alignment: .leading, spacing: 3) {
            Text("\(value)").font(IslandUI.font(compact ? 22 : 28, bold: true))
            Text(title).font(IslandUI.font(11, bold: true)).tracking(1).foregroundStyle(IslandUI.muted)
        }
    }
}


@MainActor enum BetaFeedback {
    static let url = URL(string: "sms:+19085909023")!
    static func open() { UIApplication.shared.open(url) }
}

struct BetaFeedbackBar: View {
    @Environment(\.openURL) private var openURL
    @State private var unavailable = false
    var body: some View {
        HStack {
            Text("MOTION CLUB · BETA").font(IslandUI.font(11, bold: true)).tracking(1)
            Spacer()
            Button {
                openURL(BetaFeedback.url) { accepted in unavailable = !accepted }
            } label: {
                Label("Feedback", systemImage: "message.fill").font(IslandUI.font(14, bold: true))
                    .padding(.vertical, 10)
            }.accessibilityLabel("Send beta feedback by text").accessibilityIdentifier("beta-feedback")
        }.foregroundStyle(IslandUI.navy).padding(.horizontal, 18).background(IslandUI.paper)
            .alert("Text beta feedback", isPresented: $unavailable) {
                Button("Copy number") { UIPasteboard.general.string = "908-590-9023" }
                Button("Close", role: .cancel) { }
            } message: { Text("Text 908-590-9023. Messages is not available on this device.") }
    }
}
