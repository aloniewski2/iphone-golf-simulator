import UIKit

extension GolferAppearance {
    static func color(_ hex: UInt32) -> UIColor {
        UIColor(red:CGFloat((hex >> 16) & 255)/255,green:CGFloat((hex >> 8) & 255)/255,blue:CGFloat(hex & 255)/255,alpha:1)
    }
    var shirtColor: UIColor { Self.color(outfit.hex) }
    var skinColor: UIColor { Self.color(skin.hex) }
    var hairColor: UIColor { Self.color(hair.hex) }
    var trousersColor: UIColor { Self.color(trousersHex) }
    var accentColor: UIColor { Self.color(accentHex) }
}
