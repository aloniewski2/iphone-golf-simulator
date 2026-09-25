import Foundation

/// One line of the story: who says it and what they say. `speaker` is "ray", "you",
/// "announcer", or an opponent key ("Milo"…); the portrait comes from it.
struct StoryLine: Equatable {
    let speaker: String
    let text: String
}

/// The Island Circuit story. An unknown player turns up at the Tropical Open; Ray Makoa —
/// "Old Ray", the island champion who lost the final to Viktor Kron years ago and never
/// picked up a racket again — watches the first match from the stands, offers to coach, and
/// walks the player up the ladder: a briefing and a warning before every rival, reminders at
/// the changeovers, and a word after every result, all the way to Viktor.
@MainActor
enum TennisStory {
    static let coachName = "RAY MAKOA"

    static func name(for speaker: String) -> String {
        switch speaker {
        case "ray": return coachName
        case "stranger": return "???"
        case "announcer": return "ANNOUNCER"
        case "you": return "YOU"
        default:
            return TennisCampaign.draw.first { $0.key == speaker }.map { $0.name.uppercased() } ?? speaker.uppercased()
        }
    }

    /// Portrait art for a speaker (bundled in MenuArt).
    static func art(for speaker: String, female: Bool) -> String {
        switch speaker {
        case "ray", "stranger": return "menu-hero-ray"
        case "you": return female ? "menu-hero-player-female" : "menu-hero-player-male"
        case "announcer": return "menu-trophy"
        default: return "menu-hero-\(speaker.lowercased())"
        }
    }

    // MARK: - The first-time tutorial

    static let tutorialIntro: [StoryLine] = [
        StoryLine(speaker: "announcer", text: "Welcome to the Tropical Open! Before your first match, every new player spends a few minutes on the practice court."),
        StoryLine(speaker: "stranger", text: "(An old man in a faded champion's cap is already out there, a basket of balls at his feet.) New face, huh? Come on — I'll feed you a few."),
        StoryLine(speaker: "stranger", text: "Your phone is your racket. Point it at the TV, step to move, swing to hit. I'll talk you through the rest, one shot at a time."),
    ]
    static let tutorialDone: [StoryLine] = [
        StoryLine(speaker: "stranger", text: "Not bad. Not bad at all. You move like someone who's going to be trouble."),
        StoryLine(speaker: "announcer", text: "Tutorial complete! The Island Circuit, training court and exhibition matches are now open."),
    ]

    // MARK: - Before each match

    /// Shown before round `i`: the build-up, the rival's taunt, and Ray's warning.
    static func before(_ i: Int) -> [StoryLine] {
        switch i {
        case 0: return [
            StoryLine(speaker: "announcer", text: "Welcome to the Tropical Open — ten matches, one island, one champion. First up, a newcomer nobody has heard of…"),
            StoryLine(speaker: "Milo", text: "Hey, new kid! Don't worry, I'll make this quick. I always make it quick. Mostly into the net, but quick!"),
            StoryLine(speaker: "stranger", text: "(From the stands, an old man in a faded champion's cap leans forward.) Hm. Let's see what you've got."),
        ]
        case 1: return [
            StoryLine(speaker: "ray", text: "Round two. Tama Kealoha. Nice kid, terrible to play. He hits the ball to the sky and it lands on your baseline like a coconut."),
            StoryLine(speaker: "Tama", text: "Up, up and away, bro. Hope you like standing at the back fence."),
            StoryLine(speaker: "ray", text: "Warning: don't let those loops push you back. Step in, take them on the rise, and put them into the corners. He hates running."),
        ]
        case 2: return [
            StoryLine(speaker: "ray", text: "Suki Tanaka. They call her the Needle because she finds gaps you didn't know you had."),
            StoryLine(speaker: "Suki", text: "Your court is so big. It would be a shame not to use all of it."),
            StoryLine(speaker: "ray", text: "Warning: she'll pull you wide off the second shot, then hit into the open court. After every shot, get back to the middle. Every. Shot."),
        ]
        case 3: return [
            StoryLine(speaker: "ray", text: "Dex Okafor. Biggest serve on the island. When he's serving, points are over in one hit."),
            StoryLine(speaker: "Dex", text: "You ever returned a 180 k serve? No? Today's gonna be educational."),
            StoryLine(speaker: "ray", text: "Warning: his first serve goes wide on the deuce side when he's in trouble. Stand a step over, get your racket on it, and just get it back deep. His rally game is ordinary — make him play one."),
        ]
        case 4: return [
            StoryLine(speaker: "ray", text: "Lina Voss. The Counter. She doesn't hit winners. She doesn't have to. She just never misses."),
            StoryLine(speaker: "Lina", text: "Hit it as hard as you like. It'll come back. It always comes back."),
            StoryLine(speaker: "ray", text: "Warning: going for the lines against her is how you lose. Be patient — deep, deep, deep, then change direction when she's off balance. Outlast her."),
        ]
        case 5: return [
            StoryLine(speaker: "ray", text: "Quarterfinals. Best of three sets from here on — your legs will matter now. And you've drawn Bruno Castell."),
            StoryLine(speaker: "Bruno", text: "I heard Old Ray found himself a project. Cute. I'm going to hit the ball through you, kid."),
            StoryLine(speaker: "ray", text: "Warning: his ball is heavy and fast. Don't swing harder — swing earlier, keep your shape, and use his pace. Short backswing, move the ball side to side, and he'll break down."),
        ]
        case 6: return [
            StoryLine(speaker: "ray", text: "Rosa Marín. The Magician. Slice that skids, drop shots that die, and never the same ball twice."),
            StoryLine(speaker: "Rosa", text: "Pick a card, any card… oh, too slow. Point to me."),
            StoryLine(speaker: "ray", text: "Warning: when she slices low, get down and lift it. When she stands deep and her racket opens, the drop shot's coming — start running forward early."),
        ]
        case 7: return [
            StoryLine(speaker: "ray", text: "Semifinal. Jax Rourke. The Sniper. He aims for the lines and he usually hits them."),
            StoryLine(speaker: "Jax", text: "Chalk flies. Crowd gasps. That's my whole thing. Try to keep up."),
            StoryLine(speaker: "ray", text: "Warning: he'd rather miss than play safe — so don't give him free points. Every ball back, deep and down the middle. Make him hit one more line than he can."),
        ]
        case 8: return [
            StoryLine(speaker: "ray", text: "Nadia Petrova. The Ice Queen. The only player who's ever pushed Viktor to a fifth set."),
            StoryLine(speaker: "Nadia", text: "I've watched all your matches. You favour one side. By the second set, you won't have a favourite side."),
            StoryLine(speaker: "ray", text: "Warning: she finds your weaker wing and hammers it. Mix up your returns, cover your weak side a step early, and when she comes to your strength — punish her."),
        ]
        case 9: return [
            StoryLine(speaker: "ray", text: "…Fifteen years ago I stood in that final. Two sets up against a young Viktor Kron. I didn't win another game. I never played again."),
            StoryLine(speaker: "Viktor", text: "Makoa. You brought a child to do what you couldn't. How sentimental."),
            StoryLine(speaker: "ray", text: "Best of five sets. He never misses, he reaches everything, and he'll ace you if you give him a short second serve. There's no trick. Every point, every ball, your best. Warning: vary your serve, hit to the corners, and never — ever — give him a short ball."),
        ]
        default: return []
        }
    }

    /// Ray's reminders at the changeovers of round `i` (shown on the TV during the match).
    static func changeovers(_ i: Int) -> [String] {
        switch i {
        case 0: return ["Keep it in play. He'll miss before you do.", "Watch the ball onto the strings. Nice and easy."]
        case 1: return ["Step in on those loops — take them early!", "Hit to the corners, he hates running."]
        case 2: return ["Back to the middle after every shot!", "She went wide last time — cover the open court."]
        case 3: return ["Stand a step wider on the deuce side for his first serve.", "Just get the return back deep. Make him rally."]
        case 4: return ["Patience. Deep, deep, then change direction.", "Don't go for the lines — outlast her."]
        case 5: return ["Swing earlier, not harder. Use his pace.", "Move him side to side — big men hate turning."]
        case 6: return ["Get down to that slice and lift it.", "Racket open, standing deep? Drop shot coming — run!"]
        case 7: return ["One more ball! Make him hit one more line.", "Deep and through the middle. No free points."]
        case 8: return ["She's hunting your weaker side — cover it early.", "Mix it up. Don't let her settle into a pattern."]
        case 9: return ["Never give him a short ball.", "Vary your serve. Corners, corners, corners.", "Breathe. Every point is a new point.", "Fifteen years, kid. Let's finish it."]
        default: return []
        }
    }

    // MARK: - After each match

    static func afterWin(_ i: Int) -> [StoryLine] {
        switch i {
        case 0: return [
            StoryLine(speaker: "Milo", text: "Whoa. Okay. You're actually good. Don't tell anyone I said that."),
            StoryLine(speaker: "stranger", text: "(The old man from the stands walks over.) Your footwork is lazy and your backhand's a mess. But you've got something."),
            StoryLine(speaker: "ray", text: "Name's Ray Makoa. I used to win this thing. I've watched a lot of players come and go — you're the first in years I'd bother coaching."),
            StoryLine(speaker: "ray", text: "Nine more matches. At the end of them is Viktor Kron. Nobody beats Viktor. But I think, with some work… you might. So — want a coach?"),
            StoryLine(speaker: "you", text: "Let's do it, Coach."),
            StoryLine(speaker: "ray", text: "Good. First lesson: I'll tell you about every opponent before you play them. Listen to the warnings. They're the difference."),
        ]
        case 1: return [StoryLine(speaker: "ray", text: "You stepped in and took his loops out of the sky. That's how you do it. Next up — Suki. Bring your running shoes.")]
        case 2: return [StoryLine(speaker: "ray", text: "You kept finding the middle. She ran out of angles. Next: Dex. Hope you like fast serves.")]
        case 3: return [StoryLine(speaker: "Dex", text: "Man… nobody gets my serve back like that."),
                        StoryLine(speaker: "ray", text: "One break was all it took. Now Lina. She's the first real wall. Patience, kid.")]
        case 4: return [StoryLine(speaker: "ray", text: "You outlasted the Counter. People are starting to talk about you. From here it's best of three sets — the real tour.")]
        case 5: return [StoryLine(speaker: "Bruno", text: "…Old Ray's project hits back. Fine. You earned it."),
                        StoryLine(speaker: "ray", text: "You used his own pace against him. Beautiful. Next, Rosa — and her box of tricks.")]
        case 6: return [StoryLine(speaker: "ray", text: "You saw the drop shots coming. That's reading the game, not just hitting the ball. Semifinals now. Jax.")]
        case 7: return [StoryLine(speaker: "Jax", text: "Missed the line by that much. Story of my day."),
                        StoryLine(speaker: "ray", text: "You made him hit one more. And one more. And one more. One match from the final.")]
        case 8: return [StoryLine(speaker: "Nadia", text: "Interesting. You changed your game halfway through. Viktor won't let you do that."),
                        StoryLine(speaker: "ray", text: "She's right about one thing: Viktor's a different animal. Rest up. Tomorrow we finish what I started fifteen years ago.")]
        case 9: return [
            StoryLine(speaker: "announcer", text: "Game, set and MATCH! After years of total dominance, Viktor Kron has FALLEN!"),
            StoryLine(speaker: "Viktor", text: "…Makoa. Your student is better than you ever were. Well played."),
            StoryLine(speaker: "ray", text: "Fifteen years I've been carrying that final around. You just set it down for me, kid."),
            StoryLine(speaker: "ray", text: "Tropical Open champion. Now — same time next year? Viktor will want his title back."),
        ]
        default: return []
        }
    }

    static func afterLoss(_ i: Int) -> [StoryLine] {
        switch i {
        case 0: return [StoryLine(speaker: "stranger", text: "(The old man shakes his head.) Too much, too soon. Keep it in play, kid. Try again.")]
        case 9: return [StoryLine(speaker: "Viktor", text: "Nobody beats me, Makoa. Not you. Not your child."),
                        StoryLine(speaker: "ray", text: "That's what he said to me, too. You took more off him than anyone has in years. Again. Every ball, your best.")]
        default:
            let o = TennisCampaign.draw[i]
            return [StoryLine(speaker: o.key, text: tauntAfterWin[i]),
                    StoryLine(speaker: "ray", text: "Shake it off. Remember: \(o.blurb)")]
        }
    }

    private static let tauntAfterWin = [
        "Told you I'd make it quick!",
        "Too high for you? Stretch more, bro.",
        "Such a big court. You only used half.",
        "Thanks for the practice returns.",
        "See? It always comes back.",
        "Through you. Like I said.",
        "Now you see it, now you don't.",
        "Chalk flies, crowd gasps. Every time.",
        "Predictable. I told you I'd find it.",
        "",
    ]
}
