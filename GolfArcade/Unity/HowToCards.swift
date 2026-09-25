import AVFoundation
import SwiftUI

/// One illustrated how-to card: a title, a few short steps, and a small animated diagram.
struct HowToCard: Identifiable, Equatable {
    enum Art: Equatable { case mirror, mac, stand, swing, backhand, serve, golfStance, golfSwing, golfAim, golfClubs }
    let id: String
    let title: String
    let steps: [String]
    let art: Art
}

/// The how-to guide (main menu → How to play) and the cards the loading screen cycles through.
enum HowTo {
    static let connectTV = HowToCard(id: "tv", title: "Connect to your TV", steps: [
        "Open Control Center on your iPhone (swipe down from the top right).",
        "Tap Screen Mirroring and choose your Apple TV or AirPlay TV.",
        "Keep the phone unlocked and on the same Wi-Fi — it is your racket.",
    ], art: .mirror)
    static let connectMac = HowToCard(id: "mac", title: "Play on a MacBook", steps: [
        "On the Mac: System Settings → General → AirDrop & Handoff → turn on AirPlay Receiver.",
        "On the iPhone: Control Center → Screen Mirroring → choose the Mac.",
        "Put the Mac where you can see it from 2 m away, full screen, lid open.",
    ], art: .mac)
    static let stand = HowToCard(id: "stand", title: "Stand and hold", steps: [
        "Stand about 2 m back, facing the screen, with room to swing.",
        "Hold the phone like a racket handle, screen facing the TV for forehands.",
        "Point the back of the phone at the TV once to lock the court direction.",
    ], art: .stand)
    static let swing = HowToCard(id: "swing", title: "Forehand", steps: [
        "Step sideways to run — your player follows you across the court.",
        "Swing as the ball rises to the top of its bounce.",
        "Point the racket face where you want the ball to go.",
    ], art: .swing)
    static let backhand = HowToCard(id: "backhand", title: "Backhand", steps: [
        "Ball on your other side? Turn the phone so the camera faces the TV.",
        "Swing across your body — the game picks the side from where the ball is.",
        "Clean contact beats a harder swing: the ball flies faster off the sweet spot.",
    ], art: .backhand)
    static let serve = HowToCard(id: "serve", title: "Serve", steps: [
        "Press TOSS as the meter crosses the middle — green is a perfect toss.",
        "Swing down hard as the ball peaks.",
        "Aiming at the lines is risky: a loose toss can fault.",
    ], art: .serve)

    static let pages = [connectTV, connectMac, stand, swing, backhand, serve]

    /// What the loading screen cycles through for a sport.
    static func loadingCards(_ sport: Sport) -> [HowToCard] {
        sport == .golf ? [connectTV, connectMac] + GolfLesson.cards : pages
    }

    static func tips(_ sport: Sport) -> [String] {
        switch sport {
        case .golf: [
            "A smooth tempo sends the ball further than a fast, jerky swing.",
            "Aim with the arrows before you swing — wind shows on the flag.",
            "Longer clubs go further but punish a mistimed swing more.",
            "Take the phone back slowly, then accelerate through the ball.",
            "Keep your wrist firm at impact for a straight shot.",
        ]
        default: [
            "Point the racket face where you want the ball to go.",
            "Clean contact, not a harder swing, makes the ball fly faster.",
            "Swing as the ball rises to the top of its bounce.",
            "Three great hits in a row charge a super shot.",
            "Lean toward a wide ball early to get a jump on it.",
            "Get back to the middle after every shot.",
            "A green toss is a perfect serve — but the best returners still get it back.",
            "Deep balls push your opponent back; short balls invite an attack.",
            "Stronger rivals hunt your weaker side. Mix up your shots.",
            "Losing? Coach Ray's changeover tips tell you what's working against you.",
            "Keep your phone unlocked and in your hand — it is your racket.",
            "Mirroring to a Mac? Full-screen the AirPlay window for the best picture.",
        ]
        }
    }
}

/// Golf's first-time lesson: these cards, then one practice shot.
enum GolfLesson {
    static let cards = [
        HowToCard(id: "g-stance", title: "Set up", steps: [
            "Stand side-on to the TV, feet shoulder-width apart.",
            "Hold the phone in both hands like a club grip, screen facing you.",
        ], art: .golfStance),
        HowToCard(id: "g-swing", title: "Swing", steps: [
            "Take the phone back slowly over your shoulder.",
            "Swing down and through in one smooth motion — tempo beats force.",
        ], art: .golfSwing),
        HowToCard(id: "g-aim", title: "Aim", steps: [
            "Use the arrows on your phone to aim before the shot.",
            "Watch the flag: wind pushes the ball in the air.",
        ], art: .golfAim),
        HowToCard(id: "g-clubs", title: "Clubs", steps: [
            "Switch clubs with the club button: driver off the tee, irons toward the green.",
            "Now hit your first practice shot to finish the lesson.",
        ], art: .golfClubs),
    ]
}

/// A how-to card, drawn with SF Symbols and shapes so it stays crisp at any size.
struct HowToCardView: View {
    let card: HowToCard
    var compact = false
    var body: some View {
        HStack(alignment: .center, spacing: compact ? 14 : 26) {
            HowToArt(art: card.art).frame(width: compact ? 96 : 190, height: compact ? 96 : 190)
            VStack(alignment: .leading, spacing: compact ? 6 : 10) {
                Text(card.title.uppercased()).font(Arcade.font(compact ? 18 : 28)).italic().foregroundStyle(Arcade.gold)
                ForEach(Array(card.steps.enumerated()), id: \.offset) { i, step in
                    HStack(alignment: .top, spacing: 10) {
                        Text("\(i + 1)").font(Arcade.font(compact ? 13 : 17)).foregroundStyle(Arcade.navyDeep)
                            .frame(width: compact ? 22 : 30, height: compact ? 22 : 30).background(Circle().fill(Arcade.gold))
                        Text(step).font(Arcade.font(compact ? 14 : 19, .semibold)).foregroundStyle(.white)
                            .fixedSize(horizontal: false, vertical: true)
                    }
                }
            }
            .frame(maxWidth: .infinity, alignment: .leading)
        }
        .padding(compact ? 14 : 24)
        .background(RoundedRectangle(cornerRadius: 22, style: .continuous).fill(Arcade.navyDeep.opacity(0.85)))
        .overlay(RoundedRectangle(cornerRadius: 22, style: .continuous).strokeBorder(.white.opacity(0.18), lineWidth: 2))
    }
}

/// The little animated diagram on a how-to card.
struct HowToArt: View {
    let art: HowToCard.Art
    @State private var phase = false
    var body: some View {
        ZStack {
            Circle().fill(LinearGradient(colors: [Arcade.sky, Arcade.skyDeep], startPoint: .top, endPoint: .bottom))
            Circle().strokeBorder(.white.opacity(0.7), lineWidth: 4)
            content.foregroundStyle(.white)
        }
        .onAppear { withAnimation(.easeInOut(duration: 1.1).repeatForever(autoreverses: true)) { phase = true } }
    }

    @ViewBuilder private var content: some View {
        GeometryReader { g in
            let s = min(g.size.width, g.size.height)
            ZStack {
                switch art {
                case .mirror:
                    Image(systemName: "iphone").font(.system(size: s * 0.3, weight: .bold)).offset(x: -s * 0.18, y: s * 0.1)
                    Image(systemName: "tv").font(.system(size: s * 0.34, weight: .bold)).offset(x: s * 0.14, y: -s * 0.1)
                    Image(systemName: "rectangle.on.rectangle").font(.system(size: s * 0.14, weight: .black))
                        .foregroundStyle(Arcade.gold).opacity(phase ? 1 : 0.3).offset(y: -s * 0.02)
                case .mac:
                    Image(systemName: "laptopcomputer").font(.system(size: s * 0.38, weight: .bold)).offset(x: s * 0.08, y: -s * 0.04)
                    Image(systemName: "iphone").font(.system(size: s * 0.24, weight: .bold)).offset(x: -s * 0.24, y: s * 0.16)
                    Image(systemName: "airplayvideo").font(.system(size: s * 0.13, weight: .black))
                        .foregroundStyle(Arcade.gold).opacity(phase ? 1 : 0.3).offset(x: -s * 0.08, y: s * 0.02)
                case .stand:
                    Image(systemName: "figure.stand").font(.system(size: s * 0.46, weight: .bold)).offset(x: -s * 0.12)
                    Image(systemName: "arrow.left.and.right").font(.system(size: s * 0.14, weight: .black))
                        .foregroundStyle(Arcade.gold).offset(x: phase ? s * 0.22 : s * 0.14, y: s * 0.25)
                    Image(systemName: "tv").font(.system(size: s * 0.18, weight: .bold)).offset(x: s * 0.24, y: -s * 0.18)
                case .swing, .backhand:
                    Image(systemName: "figure.tennis").font(.system(size: s * 0.5, weight: .bold))
                        .scaleEffect(x: art == .backhand ? -1 : 1)
                        .rotationEffect(.degrees(phase ? (art == .backhand ? 10 : -10) : 0))
                    Circle().fill(Arcade.lime).frame(width: s * 0.1).offset(x: (art == .backhand ? -1 : 1) * (phase ? s * 0.32 : s * 0.2), y: -s * 0.12)
                case .serve:
                    Image(systemName: "figure.tennis").font(.system(size: s * 0.46, weight: .bold)).offset(y: s * 0.08)
                    Circle().fill(Arcade.lime).frame(width: s * 0.1).offset(x: s * 0.06, y: phase ? -s * 0.34 : -s * 0.1)
                case .golfStance, .golfSwing:
                    Image(systemName: "figure.golf").font(.system(size: s * 0.5, weight: .bold))
                        .rotationEffect(.degrees(art == .golfSwing && phase ? -14 : 0))
                case .golfAim:
                    Image(systemName: "flag.fill").font(.system(size: s * 0.34, weight: .bold)).offset(x: s * 0.1, y: -s * 0.06)
                    Image(systemName: "arrow.left.and.right").font(.system(size: s * 0.16, weight: .black))
                        .foregroundStyle(Arcade.gold).offset(x: phase ? -s * 0.06 : -s * 0.18, y: s * 0.24)
                case .golfClubs:
                    Image(systemName: "figure.golf").font(.system(size: s * 0.4, weight: .bold)).offset(x: -s * 0.1)
                    Image(systemName: "arrow.triangle.2.circlepath").font(.system(size: s * 0.16, weight: .black))
                        .foregroundStyle(Arcade.gold).rotationEffect(.degrees(phase ? 180 : 0)).offset(x: s * 0.22, y: -s * 0.2)
                }
            }
            .frame(width: g.size.width, height: g.size.height)
        }
    }
}

/// A muted, looping b-roll clip from MenuVideo, aspect-filled, with its poster frame behind it
/// (shown alone when motion is reduced, while the clip loads, and in snapshots).
struct LoopingVideo: View {
    let clip: String
    var playing = true
    /// Snapshots (ImageRenderer cannot draw a player layer) show the poster frames.
    nonisolated(unsafe) static var stillsOnly = false
    var body: some View {
        let still = Self.stillsOnly || SportsSession.shared.reduceMotion || !playing
        // Fill exactly the space offered: an aspect-filled image must not widen its container
        // (that pushed titles laid over the video off the edge).
        Color.clear
            .overlay {
                if let poster = UIImage(named: "\(clip).jpg") {
                    Image(uiImage: poster).resizable().scaledToFill()
                } else {
                    LinearGradient(colors: [Arcade.skyDeep, Arcade.navyDeep], startPoint: .top, endPoint: .bottom)
                }
            }
            .overlay {
                if !still, let url = Bundle.main.url(forResource: clip, withExtension: "mp4") {
                    LoopingPlayer(url: url).id(clip)
                }
            }
            .clipped()
    }
}

private struct LoopingPlayer: UIViewRepresentable {
    let url: URL
    final class PlayerView: UIView {
        override static var layerClass: AnyClass { AVPlayerLayer.self }
        var looper: AVPlayerLooper?
        var player: AVQueuePlayer?
    }
    func makeUIView(context: Context) -> PlayerView {
        let view = PlayerView()
        let player = AVQueuePlayer()
        player.isMuted = true
        player.preventsDisplaySleepDuringVideoPlayback = false
        view.looper = AVPlayerLooper(player: player, templateItem: AVPlayerItem(url: url))
        view.player = player
        let layer = view.layer as! AVPlayerLayer
        layer.player = player; layer.videoGravity = .resizeAspectFill
        view.backgroundColor = .clear
        player.play()
        return view
    }
    func updateUIView(_ view: PlayerView, context: Context) {}
    static func dismantleUIView(_ view: PlayerView, coordinator: ()) { view.player?.pause(); view.looper = nil }
}
