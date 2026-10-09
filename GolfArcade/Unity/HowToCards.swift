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
        "Your player runs to the ball. Lean toward it early to get a quicker start.",
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

/// Optional setup before the interactive practice, aim and hole lesson.
enum GolfLesson {
    static let cards = [
        HowToCard(id: "g-stance", title: "Set up", steps: [
            "Hold the phone like a club grip, or use touch controls — we’ll practice, aim, then play a friendly hole.",
        ], art: .golfStance),
    ]
}


/// A muted, looping b-roll clip from MenuVideo, aspect-filled, with its poster frame behind it
/// (shown alone when motion is reduced, while the clip loads, and in snapshots).
struct LoopingVideo: View {
    let clip: String
    var playing = true
    var aspectFit = false
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

struct LoopingPlayer: UIViewRepresentable {
    let url: URL
    var playing = true
    var aspectFit = false
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
        layer.player = player; layer.videoGravity = aspectFit ? .resizeAspect : .resizeAspectFill
        view.backgroundColor = .clear
        if playing { player.play() }
        return view
    }
    func updateUIView(_ view: PlayerView, context: Context) { if playing { view.player?.play() } else { view.player?.pause() } }
    static func dismantleUIView(_ view: PlayerView, coordinator: ()) { view.player?.pause(); view.looper = nil }
}
