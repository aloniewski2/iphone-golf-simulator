import SwiftUI
import SceneKit

/// How each campaign rival looks on the one character standard (the match heroes): body, skin tone and kit colours, as Unity's
/// TennisRoster has them. The hub and the campaign draw a rival by rendering that hero (not an old illustration), so the
/// portrait is the same character that walks onto the court.
struct RivalLook {
    let female: Bool
    /// Index into `Outfit.skins` (the roster's HeroSkin).
    let skin: Int
    let shirt, shorts, shoes, racket: String
}

enum RivalLooks {
    static let table: [String: RivalLook] = [
        "Milo":   RivalLook(female: false, skin: 2, shirt: "FF8A3D", shorts: "2B2F6B", shoes: "FFFFFF", racket: "FF8A3D"),
        "Tama":   RivalLook(female: false, skin: 3, shirt: "1FB5A5", shorts: "F4F1E8", shoes: "1FB5A5", racket: "1B6B63"),
        "Suki":   RivalLook(female: true,  skin: 0, shirt: "FF6FA8", shorts: "3A2C6B", shoes: "FFFFFF", racket: "FF6FA8"),
        "Dex":    RivalLook(female: false, skin: 5, shirt: "D8342C", shorts: "1B1B1B", shoes: "D8342C", racket: "1B1B1B"),
        "Lina":   RivalLook(female: true,  skin: 0, shirt: "9C7BFF", shorts: "F4F1E8", shoes: "9C7BFF", racket: "5B3FD1"),
        "Bruno":  RivalLook(female: false, skin: 4, shirt: "2E7D32", shorts: "1B1B1B", shoes: "2E7D32", racket: "1B1B1B"),
        "Rosa":   RivalLook(female: true,  skin: 2, shirt: "FF5E5B", shorts: "2B2F6B", shoes: "FFFFFF", racket: "FFC53D"),
        "Jax":    RivalLook(female: false, skin: 1, shirt: "222222", shorts: "D8342C", shoes: "222222", racket: "D8342C"),
        "Nadia":  RivalLook(female: true,  skin: 0, shirt: "9FD8FF", shorts: "F4F1E8", shoes: "9FD8FF", racket: "3A7BD5"),
        "Viktor": RivalLook(female: false, skin: 1, shirt: "14213D", shorts: "14213D", shoes: "E8B931", racket: "E8B931"),
    ]

    /// The rival as a `Player` the locker mirror can draw.
    static func player(for opponent: TennisOpponent) -> Player {
        var p = Player(name: opponent.name, colorIndex: 0)
        guard let look = table[opponent.key] else { return p }
        p.standardFemale = look.female
        p.standardSkin = look.skin
        for (slot, hex) in [("shirt", look.shirt), ("shorts", look.shorts), ("accent", look.shoes), ("racket", look.racket)] {
            p.setOutfitHex(slot, hex)   // the roster's exact colour, as the match uses
        }
        return p
    }
}

/// Offscreen portraits of the rivals (rendered once, cached).
@MainActor enum RivalPortraits {
    private static var cache: [String: UIImage] = [:]
    static func image(_ opponent: TennisOpponent) -> UIImage? {
        if let hit = cache[opponent.key] { return hit }
        let c = CharacterModelPreview.Coordinator(cameraDistance: 2.9)
        c.update(RivalLooks.player(for: opponent))
        c.framing = .body; c.applyFraming()
        c.scene.rootNode.childNode(withName: "idleBall", recursively: true)?.removeFromParentNode()
        let r = SCNRenderer(device: nil, options: nil); r.scene = c.scene; r.pointOfView = c.camera
        let img = r.snapshot(atTime: 0, with: CGSize(width: 480, height: 720), antialiasingMode: .multisampling4X)
        cache[opponent.key] = img
        return img
    }
}

/// A rival's portrait: the match hero in their colours (blank for the instant it takes to render).
struct RivalPortrait: View {
    let opponent: TennisOpponent
    @State private var image: UIImage?
    var body: some View {
        Group {
            if let image { Image(uiImage: image).resizable().scaledToFit() }
            else { Color.clear }
        }
        .task(id: opponent.key) { image = RivalPortraits.image(opponent) }
        .accessibilityHidden(true)
    }
}
