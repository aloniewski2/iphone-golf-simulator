import SwiftUI

// Hand-drawn, code-only graphics for the home screen: a sunny sky with slowly turning rays,
// drifting clouds and a rolling sea; balls from every sport bouncing with squash and stretch;
// confetti floating up; and squishy "jelly" buttons. Everything is vector (Canvas / Path), so
// it stays crisp on a TV and costs nothing to ship. Reduce Motion freezes it all in place.


// MARK: - Showroom style (home screen): Fall Guys / Wii Sports Resort rather than toy box

enum Showroom {
    static let violet = Color(hex: "5B2BD9"), violetDeep = Color(hex: "2A0F73"), magenta = Color(hex: "E0409E")
    static let cyan = Color(hex: "38D6FF"), yellow = Color(hex: "FFD21F"), yellowDeep = Color(hex: "D99A00")
    static let ink = Color(hex: "16123A"), card = Color(hex: "F7F6FF"), muted = Color(hex: "6D6A8C")
    static func display(_ size: CGFloat) -> Font { .system(size: size, weight: .black, design: .rounded).width(.condensed) }
    static func text(_ size: CGFloat, _ weight: Font.Weight = .semibold) -> Font { .system(size: size, weight: weight, design: .rounded) }
}


