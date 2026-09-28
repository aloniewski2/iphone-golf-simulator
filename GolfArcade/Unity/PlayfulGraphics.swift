import SwiftUI

// Hand-drawn, code-only graphics for the home screen: a sunny sky with slowly turning rays,
// drifting clouds and a rolling sea; balls from every sport bouncing with squash and stretch;
// confetti floating up; and squishy "jelly" buttons. Everything is vector (Canvas / Path), so
// it stays crisp on a TV and costs nothing to ship. Reduce Motion freezes it all in place.

enum Playful {
    static let sunA = Color(hex: "FFD23F"), sunB = Color(hex: "FF8A3D")
    static let pink = Color(hex: "FF5FA2"), sky = Color(hex: "4FC3F7"), skyDeep = Color(hex: "2B7BE4")
    static let sea = Color(hex: "1ECBB5"), seaDeep = Color(hex: "0E8FA0"), cream = Color(hex: "FFF7E6")
    static let ink = Color(hex: "1B1F3B")
    static func font(_ size: CGFloat, _ weight: Font.Weight = .heavy) -> Font { .system(size: size, weight: weight, design: .rounded) }

    @MainActor static var still: Bool { SportsSession.shared.reduceMotion }
}

/// The whole sky: gradient, turning sun rays, clouds, and a sea with rolling waves.
struct SunnyBackdrop: View {
    var body: some View {
        TimelineView(.animation(minimumInterval: 1 / 30, paused: Playful.still)) { context in
            let t = Playful.still ? 0 : context.date.timeIntervalSinceReferenceDate
            Canvas { g, size in
                let w = size.width, h = size.height
                // Sky.
                g.fill(Path(CGRect(origin: .zero, size: size)), with: .linearGradient(
                    Gradient(colors: [Playful.skyDeep, Playful.sky, Color(hex: "FFE08A"), Color(hex: "FFB36B")]),
                    startPoint: .zero, endPoint: CGPoint(x: 0, y: h)))
                // Sun rays, slowly turning, centred high on the right.
                let sun = CGPoint(x: w * 0.78, y: h * 0.26)
                let rays = 18
                for i in 0..<rays {
                    let a = Double(i) / Double(rays) * .pi * 2 + t * 0.06
                    var p = Path()
                    p.move(to: sun)
                    p.addLine(to: CGPoint(x: sun.x + cos(a - 0.07) * w, y: sun.y + sin(a - 0.07) * w))
                    p.addLine(to: CGPoint(x: sun.x + cos(a + 0.07) * w, y: sun.y + sin(a + 0.07) * w))
                    p.closeSubpath()
                    g.fill(p, with: .color(.white.opacity(i % 2 == 0 ? 0.13 : 0.06)))
                }
                // The sun, breathing.
                let r = h * 0.11 * (1 + 0.03 * sin(t * 1.4))
                g.fill(Path(ellipseIn: CGRect(x: sun.x - r * 1.7, y: sun.y - r * 1.7, width: r * 3.4, height: r * 3.4)),
                       with: .radialGradient(Gradient(colors: [Playful.sunA.opacity(0.55), .clear]), center: sun, startRadius: r, endRadius: r * 1.7))
                g.fill(Path(ellipseIn: CGRect(x: sun.x - r, y: sun.y - r, width: r * 2, height: r * 2)),
                       with: .linearGradient(Gradient(colors: [Playful.sunA, Playful.sunB]), startPoint: CGPoint(x: sun.x, y: sun.y - r), endPoint: CGPoint(x: sun.x, y: sun.y + r)))
                // Clouds drifting across and wrapping round.
                for (i, c) in [(0.18, 0.2, 1.0), (0.55, 0.12, 0.7), (0.9, 0.34, 0.85), (0.35, 0.42, 0.55)].enumerated() {
                    let x = (c.0 * w + t * (12 + Double(i) * 5)).truncatingRemainder(dividingBy: w + 320) - 160
                    cloud(g, at: CGPoint(x: x, y: c.1 * h), scale: c.2 * h / 720)
                }
                // Sea: two rolling wave layers.
                for layer in 0..<2 {
                    let base = h * (layer == 0 ? 0.8 : 0.86)
                    var p = Path(); p.move(to: CGPoint(x: 0, y: h))
                    var x = 0.0
                    while x <= w + 8 {
                        let y = base + sin(x / w * .pi * 5 + t * (layer == 0 ? 0.9 : -1.2)) * h * 0.012
                        p.addLine(to: CGPoint(x: x, y: y)); x += 8
                    }
                    p.addLine(to: CGPoint(x: w, y: h)); p.closeSubpath()
                    g.fill(p, with: .color(layer == 0 ? Playful.sea : Playful.seaDeep))
                }
            }
        }
        .ignoresSafeArea()
    }

    private func cloud(_ g: GraphicsContext, at c: CGPoint, scale s: CGFloat) {
        var p = Path()
        for (dx, dy, r) in [(-46.0, 6.0, 26.0), (-14, -12, 34), (22, -4, 30), (52, 8, 22)] {
            p.addEllipse(in: CGRect(x: c.x + dx * s - r * s, y: c.y + dy * s - r * s, width: r * 2 * s, height: r * 2 * s))
        }
        p.addRoundedRect(in: CGRect(x: c.x - 70 * s, y: c.y + 4 * s, width: 144 * s, height: 26 * s), cornerSize: CGSize(width: 13 * s, height: 13 * s))
        g.fill(p, with: .color(.white.opacity(0.92)))
    }
}

/// A ball from each sport, drawn in code.
enum BallKind: CaseIterable { case tennis, golf, bowling, football, glove }

struct BallArt: View {
    let kind: BallKind
    var body: some View {
        Canvas { g, size in
            let d = min(size.width, size.height), r = d / 2
            let c = CGPoint(x: size.width / 2, y: size.height / 2)
            let disc = Path(ellipseIn: CGRect(x: c.x - r, y: c.y - r, width: d, height: d))
            let shine = Path(ellipseIn: CGRect(x: c.x - r * 0.55, y: c.y - r * 0.7, width: r * 0.6, height: r * 0.4))
            switch kind {
            case .tennis:
                g.fill(disc, with: .radialGradient(Gradient(colors: [Color(hex: "EFFF7A"), Color(hex: "B6E021")]), center: CGPoint(x: c.x - r * 0.3, y: c.y - r * 0.3), startRadius: 0, endRadius: d))
                var seam = Path()
                seam.addArc(center: CGPoint(x: c.x - r * 1.05, y: c.y), radius: r * 0.8, startAngle: .degrees(-55), endAngle: .degrees(55), clockwise: false)
                seam.move(to: CGPoint(x: c.x + r * 1.05 + r * 0.8 * cos(.pi * 125 / 180), y: c.y + r * 0.8 * sin(.pi * 125 / 180)))
                seam.addArc(center: CGPoint(x: c.x + r * 1.05, y: c.y), radius: r * 0.8, startAngle: .degrees(125), endAngle: .degrees(235), clockwise: false)
                g.drawLayer { l in l.clip(to: disc); l.stroke(seam, with: .color(.white), lineWidth: d * 0.07) }
            case .golf:
                g.fill(disc, with: .radialGradient(Gradient(colors: [.white, Color(hex: "D9DEE8")]), center: CGPoint(x: c.x - r * 0.3, y: c.y - r * 0.3), startRadius: 0, endRadius: d))
                for i in 0..<14 {
                    let a = Double(i) * 2.4, rr = r * (0.2 + 0.6 * Double(i % 5) / 4)
                    let p = CGPoint(x: c.x + cos(a) * rr, y: c.y + sin(a) * rr)
                    g.fill(Path(ellipseIn: CGRect(x: p.x - d * 0.04, y: p.y - d * 0.04, width: d * 0.08, height: d * 0.08)), with: .color(Color(hex: "C3CAD6")))
                }
            case .bowling:
                g.fill(disc, with: .radialGradient(Gradient(colors: [Color(hex: "B26BFF"), Color(hex: "4B1A8F")]), center: CGPoint(x: c.x - r * 0.3, y: c.y - r * 0.3), startRadius: 0, endRadius: d))
                for (dx, dy) in [(-0.12, -0.28), (0.14, -0.3), (0.0, -0.02)] {
                    g.fill(Path(ellipseIn: CGRect(x: c.x + dx * d - d * 0.06, y: c.y + dy * d - d * 0.06, width: d * 0.12, height: d * 0.12)), with: .color(Color(hex: "2A0B55")))
                }
            case .football:
                g.fill(disc, with: .color(.white))
                let pent = { (p: CGPoint, s: CGFloat) -> Path in
                    var path = Path()
                    for k in 0..<5 {
                        let a = Double(k) / 5 * .pi * 2 - .pi / 2
                        let q = CGPoint(x: p.x + cos(a) * s, y: p.y + sin(a) * s)
                        k == 0 ? path.move(to: q) : path.addLine(to: q)
                    }
                    path.closeSubpath(); return path
                }
                g.drawLayer { l in
                    l.clip(to: disc)
                    l.fill(pent(c, r * 0.32), with: .color(Playful.ink))
                    for k in 0..<5 {
                        let a = Double(k) / 5 * .pi * 2 - .pi / 2
                        l.fill(pent(CGPoint(x: c.x + cos(a) * r * 0.95, y: c.y + sin(a) * r * 0.95), r * 0.3), with: .color(Playful.ink))
                    }
                }
                g.stroke(disc, with: .color(Playful.ink.opacity(0.25)), lineWidth: d * 0.03)
            case .glove:
                var glove = Path()
                glove.addRoundedRect(in: CGRect(x: c.x - r * 0.75, y: c.y - r * 0.85, width: r * 1.5, height: r * 1.35), cornerSize: CGSize(width: r * 0.7, height: r * 0.7))
                glove.addEllipse(in: CGRect(x: c.x - r * 1.0, y: c.y - r * 0.25, width: r * 0.7, height: r * 0.6))
                g.fill(glove, with: .linearGradient(Gradient(colors: [Color(hex: "FF5A5F"), Color(hex: "C4122E")]), startPoint: CGPoint(x: c.x, y: c.y - r), endPoint: CGPoint(x: c.x, y: c.y + r)))
                g.fill(Path(roundedRect: CGRect(x: c.x - r * 0.62, y: c.y + r * 0.45, width: r * 1.24, height: r * 0.42), cornerRadius: r * 0.12), with: .color(.white))
            }
            if kind != .glove { g.fill(shine, with: .color(.white.opacity(0.45))) }
        }
    }
}

/// A ball bouncing on its own shadow, squashing as it lands.
struct BouncingBall: View {
    let kind: BallKind
    var size: CGFloat = 60
    var height: CGFloat = 70
    var period: Double = 1.1
    var phase: Double = 0
    var body: some View {
        TimelineView(.animation(paused: Playful.still)) { context in
            let t = Playful.still ? 0.5 : (context.date.timeIntervalSinceReferenceDate / period + phase).truncatingRemainder(dividingBy: 1)
            let arc = 4 * t * (1 - t)                         // 0 on the ground, 1 at the top
            let squash = max(0, 1 - arc * 6) * 0.22           // only near the ground
            VStack(spacing: 0) {
                BallArt(kind: kind)
                    .frame(width: size * (1 + squash), height: size * (1 - squash))
                    .rotationEffect(.degrees(t * 360 * (kind == .glove ? 0.1 : 1)))
                    .offset(y: -arc * height)
                Ellipse().fill(.black.opacity(0.18 - 0.1 * arc))
                    .frame(width: size * (0.9 - 0.4 * arc), height: size * 0.16)
            }
            .frame(height: size + height + size * 0.2, alignment: .bottom)
        }
    }
}

/// Confetti bits floating up and turning, for a party feel behind everything.
struct FloatingConfetti: View {
    var count = 26
    var body: some View {
        TimelineView(.animation(minimumInterval: 1 / 30, paused: Playful.still)) { context in
            let t = Playful.still ? 0 : context.date.timeIntervalSinceReferenceDate
            Canvas { g, size in
                let colors = [Playful.pink, Playful.sunA, Playful.sea, Color.white, Color(hex: "B26BFF")]
                for i in 0..<count {
                    let seed = Double(i) * 12.9898
                    let fx = (sin(seed) * 43758.5453).truncatingRemainder(dividingBy: 1)
                    let speed = 18 + abs(fx) * 26
                    let x = abs(fx) * size.width + sin(t * 0.8 + seed) * 18
                    let y = size.height - ((t * speed + abs(fx) * 900).truncatingRemainder(dividingBy: size.height + 40)) + 20
                    var piece = g
                    piece.translateBy(x: x, y: y)
                    piece.rotate(by: .radians(t * (1 + abs(fx)) + seed))
                    let rect = CGRect(x: -5, y: -3, width: i % 3 == 0 ? 7 : 11, height: 6)
                    piece.fill(i % 4 == 0 ? Path(ellipseIn: rect) : Path(roundedRect: rect, cornerRadius: 2),
                               with: .color(colors[i % colors.count].opacity(0.85)))
                }
            }
        }
        .allowsHitTesting(false)
    }
}

/// A squishy, candy-coloured button that wiggles when it has focus.
struct JellyButton<Label: View>: View {
    let colors: (Color, Color)
    var focused = false
    var corner: CGFloat = 30
    let action: () -> Void
    @ViewBuilder var label: () -> Label
    var body: some View {
        Button(action: action) {
            label()
                .frame(maxWidth: .infinity, maxHeight: .infinity)
                .background(
                    ZStack {
                        RoundedRectangle(cornerRadius: corner, style: .continuous).fill(colors.1).offset(y: 7)
                        RoundedRectangle(cornerRadius: corner, style: .continuous)
                            .fill(LinearGradient(colors: [colors.0, colors.0, colors.1.opacity(0.55)], startPoint: .top, endPoint: .bottom))
                        RoundedRectangle(cornerRadius: corner, style: .continuous).fill(.white.opacity(0.28))
                            .padding(.horizontal, 14).frame(height: 14).frame(maxHeight: .infinity, alignment: .top).padding(.top, 7)
                    }
                )
                .overlay(RoundedRectangle(cornerRadius: corner, style: .continuous).strokeBorder(.white, lineWidth: focused ? 5 : 0))
        }
        .buttonStyle(JellyPress())
        .modifier(Wiggle(active: focused))
        .shadow(color: focused ? colors.0.opacity(0.7) : .black.opacity(0.15), radius: focused ? 20 : 8, y: 6)
        .zIndex(focused ? 1 : 0)
    }
}

private struct JellyPress: ButtonStyle {
    func makeBody(configuration: Configuration) -> some View {
        configuration.label
            .scaleEffect(x: configuration.isPressed ? 1.06 : 1, y: configuration.isPressed ? 0.9 : 1)
            .animation(.spring(response: 0.25, dampingFraction: 0.45), value: configuration.isPressed)
    }
}

/// A gentle wobble and lift while focused.
struct Wiggle: ViewModifier {
    let active: Bool
    func body(content: Content) -> some View {
        TimelineView(.animation(paused: !active || Playful.still)) { context in
            let t = context.date.timeIntervalSinceReferenceDate
            content
                .rotationEffect(.degrees(active && !Playful.still ? sin(t * 5) * 1.6 : 0))
                .scaleEffect(active ? 1.06 + (Playful.still ? 0 : 0.015 * sin(t * 3)) : 1)
                .animation(.spring(response: 0.35, dampingFraction: 0.55), value: active)
        }
    }
}

/// A comic speech bubble with a tail on the lower left.
struct SpeechBubble: View {
    let text: String
    var size: CGFloat = 22
    @State private var pop = false
    var body: some View {
        Text(text)
            .font(Playful.font(size)).foregroundStyle(Playful.ink).multilineTextAlignment(.leading)
            .padding(.horizontal, size * 0.8).padding(.vertical, size * 0.55)
            .background(
                ZStack(alignment: .bottomLeading) {
                    RoundedRectangle(cornerRadius: size, style: .continuous).fill(.white)
                    Triangle().fill(.white).frame(width: size * 1.1, height: size * 0.9).offset(x: size * 1.2, y: size * 0.8)
                }
            )
            .shadow(color: .black.opacity(0.18), radius: 8, y: 4)
            .scaleEffect(pop ? 1 : 0.3, anchor: .bottomLeading).opacity(pop ? 1 : 0)
            .onAppear { withAnimation(.spring(response: 0.5, dampingFraction: 0.5).delay(0.4)) { pop = true } }
    }
}

struct Triangle: Shape {
    func path(in r: CGRect) -> Path {
        var p = Path(); p.move(to: CGPoint(x: r.minX, y: r.minY)); p.addLine(to: CGPoint(x: r.maxX, y: r.minY))
        p.addLine(to: CGPoint(x: r.minX + r.width * 0.15, y: r.maxY)); p.closeSubpath(); return p
    }
}

// MARK: - Showroom style (home screen): Fall Guys / Wii Sports Resort rather than toy box

enum Showroom {
    static let violet = Color(hex: "5B2BD9"), violetDeep = Color(hex: "2A0F73"), magenta = Color(hex: "E0409E")
    static let cyan = Color(hex: "38D6FF"), yellow = Color(hex: "FFD21F"), yellowDeep = Color(hex: "D99A00")
    static let ink = Color(hex: "16123A"), card = Color(hex: "F7F6FF"), muted = Color(hex: "6D6A8C")
    static func display(_ size: CGFloat) -> Font { .system(size: size, weight: .black, design: .rounded).width(.condensed) }
    static func text(_ size: CGFloat, _ weight: Font.Weight = .semibold) -> Font { .system(size: size, weight: weight, design: .rounded) }
}

/// A stage for the character select feel: a deep violet-to-magenta gradient, bold diagonal
/// stripes sliding slowly, a spotlight and a glowing pedestal where the player stands, and the
/// sports' balls floating in soft focus at different depths.
struct ShowroomBackdrop: View {
    /// Where the pedestal sits, as a fraction of the size.
    var pedestal = CGPoint(x: 0.24, y: 0.86)
    var body: some View {
        TimelineView(.animation(minimumInterval: 1 / 30, paused: Playful.still)) { context in
            let t = Playful.still ? 0 : context.date.timeIntervalSinceReferenceDate
            GeometryReader { geo in
                let w = geo.size.width, h = geo.size.height
                ZStack {
                    Canvas { g, size in
                        g.fill(Path(CGRect(origin: .zero, size: size)), with: .linearGradient(
                            Gradient(colors: [Showroom.violetDeep, Showroom.violet, Showroom.magenta]),
                            startPoint: CGPoint(x: 0, y: size.height), endPoint: CGPoint(x: size.width, y: 0)))
                        // Wide diagonal stripes, sliding.
                        let band = size.height * 0.16, shift = (t * 14).truncatingRemainder(dividingBy: band * 2)
                        var stripes = Path()
                        var x = -size.height - band * 2 + shift
                        while x < size.width + band * 2 {
                            stripes.move(to: CGPoint(x: x, y: size.height)); stripes.addLine(to: CGPoint(x: x + band, y: size.height))
                            stripes.addLine(to: CGPoint(x: x + band + size.height, y: 0)); stripes.addLine(to: CGPoint(x: x + size.height, y: 0))
                            stripes.closeSubpath(); x += band * 2
                        }
                        g.fill(stripes, with: .color(.white.opacity(0.05)))
                        // Spotlight falling on the pedestal.
                        let p = CGPoint(x: pedestal.x * size.width, y: pedestal.y * size.height)
                        var cone = Path()
                        cone.move(to: CGPoint(x: p.x - size.width * 0.05, y: 0)); cone.addLine(to: CGPoint(x: p.x + size.width * 0.05, y: 0))
                        cone.addLine(to: CGPoint(x: p.x + size.width * 0.17, y: p.y)); cone.addLine(to: CGPoint(x: p.x - size.width * 0.17, y: p.y))
                        cone.closeSubpath()
                        g.fill(cone, with: .linearGradient(Gradient(colors: [.white.opacity(0.0), .white.opacity(0.16)]),
                                                           startPoint: .zero, endPoint: CGPoint(x: 0, y: p.y)))
                        // Pedestal: a glowing disc with a rim.
                        let rx = min(size.width * 0.13, size.height * 0.3), ry = size.height * 0.04
                        g.fill(Path(ellipseIn: CGRect(x: p.x - rx * 1.5, y: p.y - ry * 1.6, width: rx * 3, height: ry * 3.2)),
                               with: .radialGradient(Gradient(colors: [Showroom.cyan.opacity(0.45), .clear]), center: p, startRadius: 0, endRadius: rx * 1.5))
                        g.fill(Path(ellipseIn: CGRect(x: p.x - rx, y: p.y - ry + ry * 0.5, width: rx * 2, height: ry * 2)), with: .color(Showroom.violetDeep.opacity(0.9)))
                        g.fill(Path(ellipseIn: CGRect(x: p.x - rx, y: p.y - ry, width: rx * 2, height: ry * 2)),
                               with: .linearGradient(Gradient(colors: [.white.opacity(0.95), Showroom.cyan]), startPoint: CGPoint(x: p.x, y: p.y - ry), endPoint: CGPoint(x: p.x, y: p.y + ry)))
                        g.stroke(Path(ellipseIn: CGRect(x: p.x - rx * 0.8, y: p.y - ry * 0.8, width: rx * 1.6, height: ry * 1.6)), with: .color(Showroom.violet.opacity(0.35)), lineWidth: 2)
                    }
                    // Floating balls at three depths: far ones small and soft, near ones sharp.
                    ForEach(Array(Self.floaters.enumerated()), id: \.offset) { i, f in
                        let bob = sin(t * f.speed + Double(i)) * 14
                        BallArt(kind: f.kind)
                            .frame(width: f.size * h, height: f.size * h)
                            .rotationEffect(.degrees(t * f.spin))
                            .blur(radius: f.blur)
                            .opacity(f.blur > 2 ? 0.55 : 0.95)
                            .position(x: f.x * w, y: f.y * h + bob)
                    }
                }
            }
        }
        .ignoresSafeArea()
    }

    struct Floater { let kind: BallKind; let x, y, size, blur, speed, spin: Double }
    static let floaters = [
        Floater(kind: .football, x: 0.47, y: 0.17, size: 0.07, blur: 4, speed: 0.7, spin: 6),
        Floater(kind: .tennis, x: 0.93, y: 0.2, size: 0.11, blur: 0, speed: 0.9, spin: -10),
        Floater(kind: .bowling, x: 0.06, y: 0.28, size: 0.06, blur: 5, speed: 0.6, spin: 4),
        Floater(kind: .golf, x: 0.58, y: 0.93, size: 0.08, blur: 2.5, speed: 1.1, spin: 12),
        Floater(kind: .glove, x: 0.97, y: 0.9, size: 0.12, blur: 0, speed: 0.8, spin: 0),
    ]
}

/// A confident, solid button: flat colour, a darker slab beneath it for depth, no gloss.
/// Focus lifts and tilts it slightly and draws a white outline — no constant wobble.
struct SlabButton<Label: View>: View {
    let fill: Color
    let slab: Color
    var focused = false
    var corner: CGFloat = 22
    let action: () -> Void
    @ViewBuilder var label: () -> Label
    var body: some View {
        Button(action: action) {
            label()
                .frame(maxWidth: .infinity, maxHeight: .infinity)
                .background(
                    ZStack {
                        RoundedRectangle(cornerRadius: corner, style: .continuous).fill(slab).offset(y: 6)
                        RoundedRectangle(cornerRadius: corner, style: .continuous).fill(fill)
                    })
                .overlay(RoundedRectangle(cornerRadius: corner, style: .continuous).strokeBorder(.white, lineWidth: focused ? 4 : 0).padding(-7))
        }
        .buttonStyle(SlabPress())
        .scaleEffect(focused ? 1.045 : 1).rotationEffect(.degrees(focused ? -1.2 : 0))
        .shadow(color: .black.opacity(focused ? 0.35 : 0.2), radius: focused ? 22 : 10, y: focused ? 12 : 6)
        .animation(.spring(response: 0.3, dampingFraction: 0.6), value: focused)
        .zIndex(focused ? 1 : 0)
    }
}

private struct SlabPress: ButtonStyle {
    func makeBody(configuration: Configuration) -> some View {
        configuration.label.offset(y: configuration.isPressed ? 4 : 0)
            .animation(.easeOut(duration: 0.08), value: configuration.isPressed)
    }
}
