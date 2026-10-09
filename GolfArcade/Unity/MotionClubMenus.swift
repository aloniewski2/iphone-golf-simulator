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
    private let items = [("homeContinue", "Continue", "play.circle.fill"), ("play", "Play", "play.fill"), ("character", "Locker", "tshirt"), ("settings", "Settings", "gearshape")]
    private var activity: ClubPreviewActivity {
        switch menu.focused { case "character": .locker; case "settings": .settings; default: .play }
    }
    var body: some View {
        GeometryReader { g in
            let w = g.size.width, h = g.size.height
            ZStack {
                IslandBackdrop()
                if compact { LinearGradient(colors: [.clear, IslandUI.paper.opacity(0.95)], startPoint: .top, endPoint: .bottom).ignoresSafeArea() }
                CharacterModelPreview(player: menu.player ?? Player(name: "Player 1", colorIndex: 0), cameraDistance: compact ? 3.4 : 3.25, menuActivity: activity)
                    .frame(width: w * (compact ? 0.87 : 0.56), height: h * (compact ? 0.48 : 0.86))
                    .position(x: w * (compact ? 0.55 : 0.73), y: h * (compact ? 0.30 : 0.56))
                    .allowsHitTesting(false)
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
                    let selected = menu.isFocused(item.0), locked = false, isContinue = item.0 == "homeContinue"
                    Button { menu.tap(item.0) } label: {
                        HStack(spacing: compact ? 14 : 22) {
                            Image(systemName: item.2).font(.system(size: compact ? 23 : 31, weight: .semibold)).frame(width: compact ? 27 : 36)
                            VStack(alignment: .leading, spacing: 0) {
                                if isContinue {
                                    Text(menu.continueLabel.title.uppercased()).font(IslandUI.font(compact ? 12 : 14, bold: true)).tracking(1.4).opacity(0.75)
                                    Text(menu.continueLabel.subtitle).font(IslandUI.font(compact ? 17 : 23, bold: true)).lineLimit(2).minimumScaleFactor(0.8)
                                } else {
                                    Text(item.1).font(IslandUI.font(compact ? 25 : 36, bold: true))
                                }
                            }
                            Spacer(minLength: 0)
                            if selected && !locked { Image(systemName: "chevron.right").font(.system(size: 20, weight: .bold)) }
                        }
                        .foregroundStyle(locked ? IslandUI.muted : IslandUI.navy)
                        .padding(.horizontal, compact ? 18 : 26).frame(maxWidth: .infinity, maxHeight: .infinity)
                        .background(isContinue ? IslandUI.lime : selected ? IslandUI.lime : IslandUI.paper, in: RoundedRectangle(cornerRadius: compact ? 18 : 22))
                        .overlay(RoundedRectangle(cornerRadius: compact ? 18 : 22).strokeBorder(IslandUI.navy, lineWidth: selected ? 3.5 : isContinue ? 1.5 : 0))
                        .overlay(RoundedRectangle(cornerRadius: compact ? 18 : 22).strokeBorder(.white.opacity(0.85), lineWidth: 1.5))
                        .shadow(color: Color.black.opacity(0.13), radius: selected ? 9 : 5, y: selected ? 6 : 3)
                    }
                    .buttonStyle(.plain)
                    .scaleEffect(selected && !locked ? 1.035 : 1)
                    .offset(x: index == 1 || index == 2 ? width * 0.06 : -width * 0.035)
                    .onHover { over in if over { menu.focus(item.0) } }
                    .accessibilityLabel(locked ? "Store, locked, coming soon" : item.1)
                    .accessibilityHint(locked ? "The store is not available yet." : "Open \(item.1)")
                    .accessibilityIdentifier("home-\(item.0)")
                    .accessibilityAddTraits(selected ? .isSelected : [])
                }
            }.frame(width: width, height: height)
        }.frame(width: width, height: height)
            .animation(reduceMotion || SportsSession.shared.reduceMotion ? nil : .easeOut(duration: 0.18), value: menu.focused)
    }
}

/// A literal door-like hinge swing between native menu destinations.
/// The outgoing screen pivots on its vertical edge; the destination stays stationary behind it.
struct ClubCameraHost: UIViewControllerRepresentable {
    let route: MenuScreen
    let reducedMotion: Bool
    var paused = false
    let content: AnyView
    func makeUIViewController(context: Context) -> Controller { Controller(content, route: route, paused: paused) }
    func updateUIViewController(_ controller: Controller, context: Context) { controller.show(content, route: route, paused: paused, reduced: reducedMotion) }
    @MainActor final class Controller: UIViewController, @preconcurrency CAAnimationDelegate {
        private var host: UIHostingController<AnyView>
        private var route: MenuScreen
        private var displayLink: CADisplayLink?
        private var started: CFTimeInterval = 0
        private var reduced = false
        private var direction: CGFloat = 1
        private var paused: Bool
        private var outgoing: UIView?
        init(_ content: AnyView, route: MenuScreen, paused: Bool) { host = UIHostingController(rootView: content); self.route = route; self.paused = paused; super.init(nibName: nil, bundle: nil) }
        required init?(coder: NSCoder) { fatalError("init(coder:) has not been implemented") }
        override func viewDidLoad() {
            super.viewDidLoad(); view.backgroundColor = UIColor(IslandUI.paper); view.clipsToBounds = true
            addChild(host); host.safeAreaRegions = []; host.view.backgroundColor = .clear
            host.view.frame = view.bounds; host.view.autoresizingMask = [.flexibleWidth, .flexibleHeight]
            view.addSubview(host.view); host.didMove(toParent: self)
        }
        func show(_ content: AnyView, route next: MenuScreen, paused: Bool, reduced: Bool) {
            guard isViewLoaded else { host.rootView = content; route = next; self.paused = paused; return }
            guard next != route || self.paused != paused else { host.rootView = content; return }
            displayLink?.invalidate(); outgoing?.removeFromSuperview(); outgoing = nil
            host.view.layer.transform = CATransform3DIdentity; host.view.alpha = 1
            let format = UIGraphicsImageRendererFormat(); format.scale = 1
            let image = UIGraphicsImageRenderer(size: view.bounds.size, format: format).image { _ in
                host.view.drawHierarchy(in: view.bounds, afterScreenUpdates: false)
            }
            let old = UIImageView(image: image); old.frame = view.bounds
            direction = next == .main || next == .title || (route != .main && next == .gameSelect) ? -1 : 1
            // Keep the hinge edge fixed in screen space. Changing anchorPoint alone would jump the layer.
            old.layer.anchorPoint = CGPoint(x: direction > 0 ? 0 : 1, y: 0.5)
            old.layer.position = CGPoint(x: direction > 0 ? view.bounds.minX : view.bounds.maxX, y: view.bounds.midY)
            old.layer.isDoubleSided = false
            let shade = UIView(frame: old.bounds); shade.backgroundColor = .black; shade.alpha = 0; shade.tag = 701
            shade.autoresizingMask = [.flexibleWidth, .flexibleHeight]; old.addSubview(shade)
            route = next; self.paused = paused; self.reduced = reduced
            host.rootView = content; host.view.layoutIfNeeded()
            view.addSubview(old); outgoing = old
            view.isUserInteractionEnabled = false
            // Start outside SwiftUI's update transaction, after the destination has committed.
            DispatchQueue.main.asyncAfter(deadline: .now() + 0.05) { [weak self, weak old] in
                guard let self, let old, self.outgoing === old else { return }
                self.view.bringSubviewToFront(old)
            if reduced {
                let fade = CABasicAnimation(keyPath: "opacity")
                fade.fromValue = 1; fade.toValue = 0; fade.duration = 0.16
                fade.fillMode = .forwards; fade.isRemovedOnCompletion = false; fade.delegate = self
                old.layer.add(fade, forKey: "clubFade")
            } else {
                var hinge = CATransform3DIdentity
                hinge.m34 = -1 / max(800, self.view.bounds.width * 1.5)
                let turn = CAKeyframeAnimation(keyPath: "transform")
                turn.values = (0...60).map { frame in
                    let t = CGFloat(frame) / 60
                    let eased = t * t * (3 - 2 * t)
                    return NSValue(caTransform3D: CATransform3DRotate(hinge, self.direction * (.pi / 2) * eased, 0, 1, 0))
                }
                turn.calculationMode = .linear
                turn.duration = 0.8; turn.timingFunction = CAMediaTimingFunction(name: .easeInEaseOut)
                turn.fillMode = .forwards; turn.isRemovedOnCompletion = false; turn.delegate = self
                old.layer.add(turn, forKey: "clubHinge")
                let dim = CABasicAnimation(keyPath: "opacity")
                dim.fromValue = 0; dim.toValue = 0.28; dim.duration = 0.8
                dim.fillMode = .forwards; dim.isRemovedOnCompletion = false
                shade.layer.add(dim, forKey: "clubShade")
            }
            }
        }
        func animationDidStop(_ anim: CAAnimation, finished flag: Bool) {
            outgoing?.removeFromSuperview(); outgoing = nil
            host.view.layer.transform = CATransform3DIdentity; host.view.alpha = 1; view.isUserInteractionEnabled = true
        }
        func stop() { displayLink?.invalidate(); displayLink = nil; outgoing?.removeFromSuperview(); outgoing = nil; view.isUserInteractionEnabled = true }
    }
    static func dismantleUIViewController(_ controller: Controller, coordinator: ()) { controller.stop() }
}


/// Display-only progression: the match transaction has already awarded these points.
struct ClubVictorySummary: View {
    let won: Bool
    let score: String
    let summary: PostMatchSummary?
    var compact = false
    @Environment(\.accessibilityReduceMotion) private var reduceMotion
    @State private var earned = 0

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
            .task(id: "\(summary?.total ?? 0)-\(summary?.startLevel ?? 0)-\(summary?.startXP ?? 0)") {
                earned = 0
                guard let summary else { return }
                if reduceMotion { earned = summary.total; return }
                for frame in 1...60 {
                    do { try await Task.sleep(for: .milliseconds(30)) } catch { return }
                    earned = Int((Double(summary.total) * Double(frame) / 60).rounded())
                }
            }
    }
    private func stat(_ title: String, _ value: Int) -> some View {
        VStack(alignment: .leading, spacing: 3) {
            Text("\(value)").font(IslandUI.font(compact ? 22 : 28, bold: true))
            Text(title).font(IslandUI.font(11, bold: true)).tracking(1).foregroundStyle(IslandUI.muted)
        }
    }
}
