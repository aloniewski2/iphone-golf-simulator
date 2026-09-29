import AVFoundation
import CoreText
import SwiftUI

// "Island Sports Club": the one visual system for every menu. A resort sports club — Wii Sports
// Resort warmth, Fall Guys energy — with its own type (Bricolage Grotesque display, Rubik UI),
// a fixed palette, rendered 3D objects instead of stock icons (MenuArt/club-*.png), a painted
// scene behind each area (MenuArt/scene-*.jpg), and one motion language: staggered springs in,
// a sheen across whatever has focus, buttons that press into their base, a stripe wipe between
// screens, and a click / pop / whoosh to go with them.

enum Club {
    // Palette.
    static let lagoon = Color(hex: "0E2A47"), lagoonDeep = Color(hex: "081A2E"), violet = Color(hex: "5B2BD9")
    static let coral = Color(hex: "FF5B4A"), green = Color(hex: "1FBF75"), sun = Color(hex: "FFD21F"), sunDeep = Color(hex: "D99A00")
    static let cream = Color(hex: "F7F4EC"), creamDeep = Color(hex: "D8D0BC"), ink = Color(hex: "16123A"), muted = Color(hex: "6B6780")
    static let sky = Color(hex: "38C6FF")

    /// The painted scene that stands for a sport (bowling, football and boxing share the pavilion).
    static func scene(for sport: Sport) -> String {
        switch sport { case .tennis: "court"; case .golf: "cliff"; default: "pavilion" }
    }

    static func color(_ sport: Sport) -> Color {
        switch sport {
        case .tennis: sky; case .golf: green; case .bowling: violet; case .football: coral; case .boxing: Color(hex: "E0245E")
        }
    }

    // Type: Bricolage Grotesque (wght 200–800, wdth 75–100, opsz 12–96), Rubik (wght 300–900).
    static func display(_ size: CGFloat, condensed: Bool = true) -> Font {
        ClubFonts.font(.display, size: size, weight: 800, width: condensed ? 78 : 100)
    }
    static func title(_ size: CGFloat) -> Font { ClubFonts.font(.display, size: size, weight: 760, width: 92) }
    static func ui(_ size: CGFloat, _ weight: CGFloat = 500) -> Font { ClubFonts.font(.ui, size: size, weight: weight, width: nil) }
    static func caps(_ size: CGFloat) -> Font { ClubFonts.font(.ui, size: size, weight: 700, width: nil) }

    // Motion.
    static let spring = Animation.spring(response: 0.42, dampingFraction: 0.72)
    static let pop = Animation.spring(response: 0.3, dampingFraction: 0.6)
    @MainActor static var still: Bool { SportsSession.shared.reduceMotion }
}

/// The bundled variable fonts, registered once and instanced at the exact axes asked for.
enum ClubFonts {
    enum Face { case display, ui }
    nonisolated(unsafe) private static var descriptors: [Face: CTFontDescriptor] = [:]
    nonisolated(unsafe) private static var registered = false

    static func register() {
        guard !registered else { return }
        registered = true
        for (face, file) in [(Face.display, "Bricolage"), (.ui, "Rubik")] {
            guard let url = Bundle.main.url(forResource: file, withExtension: "ttf") else { continue }
            CTFontManagerRegisterFontsForURL(url as CFURL, .process, nil)
            if let list = CTFontManagerCreateFontDescriptorsFromURL(url as CFURL) as? [CTFontDescriptor], let d = list.first {
                descriptors[face] = d
            }
        }
    }

    static func font(_ face: Face, size: CGFloat, weight: CGFloat, width: CGFloat?) -> Font {
        register()
        guard let base = descriptors[face] else {
            return .system(size: size, weight: weight > 650 ? .heavy : weight > 450 ? .semibold : .regular, design: .rounded)
        }
        var axes: [NSNumber: NSNumber] = [NSNumber(value: 0x77676874): NSNumber(value: Double(weight))]   // 'wght'
        if let width { axes[NSNumber(value: 0x77647468)] = NSNumber(value: Double(width)) }             // 'wdth'
        if face == .display { axes[NSNumber(value: 0x6F70737A)] = NSNumber(value: Double(min(96, max(12, size)))) } // 'opsz'
        let d = CTFontDescriptorCreateCopyWithAttributes(base, [kCTFontVariationAttribute: axes] as CFDictionary)
        return Font(CTFontCreateWithFontDescriptor(d, size, nil))
    }
}

/// UI sounds: tick on move, pop on select, whoosh between screens. Silent when Sound is off.
@MainActor
enum ClubSound {
    private static var players: [String: AVAudioPlayer] = [:]
    static func play(_ name: String, volume: Float = 0.5) {
        guard SportsSession.shared.sound else { return }
        if players[name] == nil, let url = Bundle.main.url(forResource: name, withExtension: "wav") {
            players[name] = try? AVAudioPlayer(contentsOf: url)
            players[name]?.prepareToPlay()
        }
        guard let p = players[name] else { return }
        p.volume = volume; p.currentTime = 0; p.play()
    }
}

// MARK: - Backdrop

/// A painted scene for each area, drifting slowly, with animated light shafts and dust in the
/// air drawn over it, and a lagoon tint so text on top always reads.
struct ClubBackdrop: View {
    let scene: String
    var tint: Double = 0.45
    var body: some View {
        TimelineView(.animation(minimumInterval: 1 / 30, paused: Club.still)) { context in
            let t = Club.still ? 0 : context.date.timeIntervalSinceReferenceDate
            GeometryReader { g in
                ZStack {
                    Color.clear.overlay {
                        if let img = UIImage(named: "scene-\(scene).jpg") {
                            Image(uiImage: img).resizable().scaledToFill()
                                .scaleEffect(1.06)
                                .offset(x: sin(t * 0.05) * g.size.width * 0.012, y: cos(t * 0.04) * g.size.height * 0.01)
                        } else {
                            LinearGradient(colors: [Club.lagoon, Club.lagoonDeep], startPoint: .top, endPoint: .bottom)
                        }
                    }.clipped()
                    Canvas { ctx, size in
                        // Light shafts from the top right, swaying.
                        for i in 0..<4 {
                            let x0 = size.width * (0.55 + Double(i) * 0.11) + sin(t * 0.3 + Double(i)) * 20
                            var p = Path()
                            p.move(to: CGPoint(x: x0, y: -10)); p.addLine(to: CGPoint(x: x0 + 60, y: -10))
                            p.addLine(to: CGPoint(x: x0 - size.width * 0.18, y: size.height)); p.addLine(to: CGPoint(x: x0 - size.width * 0.25, y: size.height))
                            p.closeSubpath()
                            ctx.fill(p, with: .linearGradient(Gradient(colors: [.white.opacity(0.10), .clear]), startPoint: .zero, endPoint: CGPoint(x: 0, y: size.height)))
                        }
                        // Dust motes drifting up through the light.
                        for i in 0..<40 {
                            let s = Double(i) * 7.31
                            let x = (s * 97).truncatingRemainder(dividingBy: size.width)
                            let y = size.height - ((t * (6 + Double(i % 5) * 3) + s * 31).truncatingRemainder(dividingBy: size.height))
                            let r = 1.2 + Double(i % 3)
                            ctx.fill(Path(ellipseIn: CGRect(x: x + sin(t + s) * 8, y: y, width: r, height: r)), with: .color(.white.opacity(0.35)))
                        }
                    }
                    LinearGradient(colors: [Club.lagoonDeep.opacity(tint + 0.25), Club.lagoonDeep.opacity(tint * 0.3), Club.lagoonDeep.opacity(tint + 0.35)],
                                   startPoint: .top, endPoint: .bottom)
                    LinearGradient(colors: [Club.lagoonDeep.opacity(tint), .clear], startPoint: .leading, endPoint: .center)
                }
            }
        }
        .ignoresSafeArea()
    }
}

// MARK: - Screen scaffold

/// Every menu screen: its scene, the header (crest, where you are, your player), the content,
/// and the controller legend. Content rises in with a spring when the screen appears.
struct ClubScreen<Content: View>: View {
    let scene: String
    var breadcrumb: [String] = []
    var compact: Bool
    var showHints = true
    var tint: Double = 0.45
    @ViewBuilder var content: () -> Content
    @State private var shown = false
    var body: some View {
        ZStack {
            ClubBackdrop(scene: scene, tint: tint)
            VStack(alignment: .leading, spacing: compact ? 12 : 18) {
                ClubHeader(breadcrumb: breadcrumb, compact: compact)
                content()
                    .opacity(shown ? 1 : 0).offset(y: shown ? 0 : 26)
                    .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
                if showHints && !compact { ClubHintBar() }
            }
            .padding(.horizontal, compact ? 18 : 44).padding(.top, compact ? 12 : 26).padding(.bottom, compact ? 12 : 20)
        }
        .onAppear { withAnimation(Club.spring.delay(0.05)) { shown = true } }
    }
}

/// The club crest (rendered) with the wordmark beside it, where you are, and your player.
struct ClubHeader: View {
    var breadcrumb: [String]
    var compact: Bool
    var body: some View {
        HStack(alignment: .center, spacing: compact ? 10 : 14) {
            ClubCrest(size: compact ? 40 : 54)
            VStack(alignment: .leading, spacing: -2) {
                Text("ISLAND SPORTS CLUB").font(Club.caps(compact ? 10 : 12)).tracking(3).foregroundStyle(.white.opacity(0.75))
                HStack(spacing: 8) {
                    ForEach(Array(breadcrumb.enumerated()), id: \.offset) { i, part in
                        if i > 0 { Image(systemName: "chevron.right").font(.system(size: compact ? 11 : 14, weight: .heavy)).foregroundStyle(.white.opacity(0.5)) }
                        Text(part).font(Club.title(compact ? 20 : 30)).foregroundStyle(i == breadcrumb.count - 1 ? .white : .white.opacity(0.6))
                    }
                }
            }
            Spacer(minLength: 0)
            let s = SportsSession.shared
            if s.players.indices.contains(s.playerIndex) {
                let p = s.players[s.playerIndex]
                if !compact { LevelStrip(player: p.id).padding(.trailing, 10) }
                HStack(spacing: 8) {
                    Circle().fill(Color(hex: p.skinHex))
                        .overlay(Circle().strokeBorder(p.outfitColor("shirt") ?? Club.sun, lineWidth: 3))
                        .frame(width: compact ? 26 : 34, height: compact ? 26 : 34)
                    Text(p.name).font(Club.ui(compact ? 13 : 16, 700)).foregroundStyle(.white)
                }
                .padding(.leading, 5).padding(.trailing, 14).padding(.vertical, 5)
                .background(Capsule().fill(.white.opacity(0.14)))
                .overlay(Capsule().strokeBorder(.white.opacity(0.22), lineWidth: 1))
            }
        }
    }
}

struct ClubCrest: View {
    var size: CGFloat
    var body: some View {
        Group {
            if let img = UIImage(named: "club-crest.png") {
                Image(uiImage: img).resizable().scaledToFit()
            } else {
                ZStack { Circle().fill(Club.lagoon); Circle().strokeBorder(Club.sun, lineWidth: 3); TennisBallIcon().padding(size * 0.2) }
            }
        }
        .frame(width: size, height: size)
        .shadow(color: .black.opacity(0.35), radius: 6, y: 3)
    }
}

/// A painted scene filling exactly the space it is given, drifting very slightly.
struct SceneImage: View {
    let name: String
    var body: some View {
        Color.clear.overlay {
            if let img = UIImage(named: "scene-\(name).jpg") { Image(uiImage: img).resizable().scaledToFill() }
            else { LinearGradient(colors: [Club.lagoon, Club.lagoonDeep], startPoint: .top, endPoint: .bottom) }
        }.clipped()
    }
}

/// A rendered menu object (MenuArt/club-<name>.png), with an SF Symbol fallback.
struct ClubArt: View {
    let name: String
    var fallback = "star.fill"
    var body: some View {
        if let img = UIImage(named: "club-\(name).png") {
            Image(uiImage: img).resizable().scaledToFit()
        } else {
            Image(systemName: fallback).resizable().scaledToFit().foregroundStyle(.white).padding(8)
        }
    }
}

// MARK: - Focus

/// Focus: lift, a slight tilt, a white outline and a sheen that sweeps across once.
struct ClubFocus: ViewModifier {
    let focused: Bool
    var corner: CGFloat = 24
    var refusals = 0
    @State private var sweep = false
    func body(content: Content) -> some View {
        content
            .overlay {
                GeometryReader { g in
                    LinearGradient(colors: [.clear, .white.opacity(0.45), .clear], startPoint: .leading, endPoint: .trailing)
                        .frame(width: g.size.width * 0.35)
                        .rotationEffect(.degrees(18))
                        .offset(x: sweep ? g.size.width * 1.3 : -g.size.width * 0.6)
                        .opacity(focused ? 1 : 0)
                }
                .clipShape(RoundedRectangle(cornerRadius: corner, style: .continuous))
                .allowsHitTesting(false)
            }
            .overlay(RoundedRectangle(cornerRadius: corner + 6, style: .continuous).strokeBorder(.white, lineWidth: focused ? 4 : 0).padding(-6))
            .scaleEffect(focused ? 1.045 : 1)
            .rotationEffect(.degrees(focused ? -0.8 : 0))
            .shadow(color: .black.opacity(focused ? 0.45 : 0.22), radius: focused ? 24 : 10, y: focused ? 14 : 6)
            .zIndex(focused ? 1 : 0)
            .modifier(Shake(travel: focused ? CGFloat(refusals) : 0))
            .animation(Club.pop, value: focused)
            .animation(.default, value: refusals)
            .onChange(of: focused) { _, now in
                guard now, !Club.still else { sweep = false; return }
                sweep = false
                withAnimation(.easeOut(duration: 0.7).delay(0.05)) { sweep = true }
            }
    }
}

extension View {
    func clubFocus(_ focused: Bool, corner: CGFloat = 24, refusals: Int = 0) -> some View {
        modifier(ClubFocus(focused: focused, corner: corner, refusals: refusals))
    }
    /// A staggered entrance: `index` places it in the cascade.
    func clubEntrance(_ index: Int) -> some View { modifier(ClubEntrance(index: index)) }
}

struct ClubEntrance: ViewModifier {
    let index: Int
    @State private var shown = false
    func body(content: Content) -> some View {
        content.opacity(shown ? 1 : 0).offset(y: shown ? 0 : 30).scaleEffect(shown ? 1 : 0.94)
            .onAppear { withAnimation(Club.spring.delay(0.08 + Double(index) * 0.045)) { shown = true } }
    }
}

/// Press: the face pushes down into its base.
struct ClubPress: ButtonStyle {
    func makeBody(configuration: Configuration) -> some View {
        configuration.label.offset(y: configuration.isPressed ? 4 : 0)
            .animation(.easeOut(duration: 0.08), value: configuration.isPressed)
    }
}

// MARK: - Components

/// The primary action: a sun-yellow slab with a darker base.
struct ClubButton: View {
    let title: String
    var subtitle: String? = nil
    var icon: String = "arrow.right"
    var focused: Bool
    var style: Style = .primary
    var size: CGFloat = 26
    let action: () -> Void
    enum Style { case primary, secondary, quiet }
    var body: some View {
        let (face, base, text): (Color, Color, Color) = switch style {
        case .primary: (Club.sun, Club.sunDeep, Club.ink)
        case .secondary: (Club.cream, Club.creamDeep, Club.ink)
        case .quiet: (.white.opacity(0.16), .black.opacity(0.25), .white)
        }
        Button(action: action) {
            HStack(spacing: size * 0.5) {
                VStack(alignment: .leading, spacing: 0) {
                    Text(title).font(Club.title(size)).lineLimit(1).minimumScaleFactor(0.7)
                    if let subtitle { Text(subtitle).font(Club.ui(size * 0.55)).opacity(0.75).lineLimit(1) }
                }
                if !icon.isEmpty { Image(systemName: icon).font(.system(size: size * 0.75, weight: .black)) }
            }
            .foregroundStyle(text)
            .padding(.horizontal, size * 0.95).padding(.vertical, size * 0.5)
            .background(
                ZStack {
                    RoundedRectangle(cornerRadius: size * 0.7, style: .continuous).fill(base).offset(y: 5)
                    RoundedRectangle(cornerRadius: size * 0.7, style: .continuous).fill(face)
                })
        }
        .buttonStyle(ClubPress())
        .clubFocus(focused, corner: size * 0.7)
    }
}

/// A card: rendered object, title, subtitle, and an optional badge; locked cards fade.
struct ClubCard: View {
    let art: String
    var fallback = "star.fill"
    let title: String
    let subtitle: String
    var badge: ClubBadge.Kind? = nil
    var tint: Color = Club.sky
    var locked = false
    var highlight = false
    var focused: Bool
    var refusals = 0
    var compact = false
    var horizontal = false
    let action: () -> Void
    var body: some View {
        Button(action: action) {
            let artView = ZStack {
                Circle().fill(tint.opacity(highlight ? 0.3 : 0.18)).blur(radius: 10)
                ClubArt(name: locked ? "lock" : art, fallback: locked ? "lock.fill" : fallback)
                    .shadow(color: .black.opacity(0.25), radius: 6, y: 4)
            }
            let textView = VStack(alignment: .leading, spacing: 3) {
                if let badge { ClubBadge(kind: badge) }
                Text(title).font(Club.title(compact ? 19 : 25)).foregroundStyle(highlight ? Club.ink : Club.ink).lineLimit(1).minimumScaleFactor(0.7)
                Text(subtitle).font(Club.ui(compact ? 12 : 15)).foregroundStyle(Club.muted).lineLimit(2).minimumScaleFactor(0.8)
            }
            Group {
                if horizontal {
                    HStack(spacing: 14) { artView.frame(width: compact ? 58 : 84, height: compact ? 58 : 84); textView; Spacer(minLength: 0) }
                        .padding(compact ? 12 : 16)
                } else {
                    VStack(alignment: .leading, spacing: 8) {
                        artView.frame(maxWidth: .infinity).frame(height: compact ? 70 : 112)
                        textView
                    }.padding(compact ? 12 : 18)
                }
            }
            .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
            .background(
                ZStack {
                    RoundedRectangle(cornerRadius: 24, style: .continuous).fill(highlight ? Club.sunDeep : Club.creamDeep).offset(y: 6)
                    RoundedRectangle(cornerRadius: 24, style: .continuous).fill(highlight ? Club.sun : Club.cream)
                    RoundedRectangle(cornerRadius: 24, style: .continuous).fill(tint).frame(height: 6).frame(maxHeight: .infinity, alignment: .top)
                        .clipShape(RoundedRectangle(cornerRadius: 24, style: .continuous))
                })
            .saturation(locked ? 0.2 : 1).opacity(locked ? 0.82 : 1)
        }
        .buttonStyle(ClubPress())
        .clubFocus(focused, corner: 24, refusals: refusals)
    }
}

struct ClubBadge: View {
    enum Kind { case new, locked, soon, done, next, boss }
    let kind: Kind
    var body: some View {
        let (text, fg, bg): (String, Color, Color) = switch kind {
        case .new: ("START HERE", Club.ink, Club.sun)
        case .locked: ("LOCKED", .white, Club.muted)
        case .soon: ("COMING SOON", .white, Club.violet)
        case .done: ("DONE ✓", .white, Club.green)
        case .next: ("NEXT UP", Club.ink, Club.sun)
        case .boss: ("FINAL BOSS", .white, Club.coral)
        }
        Text(text).font(Club.caps(11)).tracking(1.2).foregroundStyle(fg)
            .padding(.horizontal, 8).padding(.vertical, 3).background(Capsule().fill(bg))
    }
}

/// A small pill (header actions, tabs).
struct ClubPill: View {
    let title: String
    var icon: String? = nil
    var selected = false
    var focused: Bool
    var compact = false
    let action: () -> Void
    var body: some View {
        Button(action: action) {
            HStack(spacing: 6) {
                if let icon { Image(systemName: icon).font(.system(size: compact ? 13 : 16, weight: .heavy)) }
                Text(title).font(Club.ui(compact ? 13 : 16, 700))
            }
            .foregroundStyle(selected || focused ? Club.ink : .white)
            .padding(.horizontal, compact ? 12 : 16).frame(height: compact ? 34 : 42)
            .background(Capsule().fill(selected ? Club.sun : focused ? Color.white : Color.white.opacity(0.14)))
            .overlay(Capsule().strokeBorder(.white.opacity(selected || focused ? 0 : 0.25), lineWidth: 1))
        }
        .buttonStyle(ClubPress())
        .scaleEffect(focused ? 1.07 : 1).animation(Club.pop, value: focused)
    }
}

/// "Label   ‹ value ›", changed sideways (or with the arrows by touch).
struct ClubRow: View {
    let label: String
    let value: String
    var swatch: Color? = nil
    var focused: Bool
    var compact = false
    var step: ((Int) -> Void)? = nil
    let action: () -> Void
    var body: some View {
        HStack(spacing: 10) {
            Text(label).font(Club.ui(compact ? 14 : 18, 600)).foregroundStyle(focused ? Club.ink : .white)
            Spacer(minLength: 8)
            if let swatch { Circle().fill(swatch).frame(width: 18, height: 18).overlay(Circle().strokeBorder(.white, lineWidth: 2)) }
            HStack(spacing: 8) {
                Button { step?(-1) } label: { Image(systemName: "chevron.left").frame(width: 30, height: 34) }.buttonStyle(.plain).opacity(focused ? 1 : 0.4)
                Text(value).font(Club.title(compact ? 16 : 21)).frame(minWidth: compact ? 70 : 130).lineLimit(1).minimumScaleFactor(0.7)
                Button { step?(1) } label: { Image(systemName: "chevron.right").frame(width: 30, height: 34) }.buttonStyle(.plain).opacity(focused ? 1 : 0.4)
            }
            .foregroundStyle(focused ? Club.ink : .white)
        }
        .padding(.horizontal, compact ? 12 : 18).padding(.vertical, compact ? 6 : 8)
        .background(RoundedRectangle(cornerRadius: 16, style: .continuous).fill(focused ? Club.cream : Color.black.opacity(0.28)))
        .overlay(RoundedRectangle(cornerRadius: 16, style: .continuous).strokeBorder(.white.opacity(focused ? 0 : 0.12), lineWidth: 1))
        .scaleEffect(focused ? 1.02 : 1).animation(Club.pop, value: focused)
        .contentShape(Rectangle())
        .onTapGesture(perform: action)
    }
}

/// The controller legend along the bottom of the TV.
struct ClubHintBar: View {
    var body: some View {
        HStack(spacing: 22) {
            hint("dpad.fill", "Move"); hint("a.circle.fill", "Select"); hint("b.circle.fill", "Back")
            Spacer()
            Label("Your phone is the remote", systemImage: "iphone.gen3").font(Club.ui(13, 600)).foregroundStyle(.white.opacity(0.6))
        }
        .font(Club.ui(14, 700)).foregroundStyle(.white.opacity(0.85))
    }
    private func hint(_ icon: String, _ label: String) -> some View {
        HStack(spacing: 6) { Image(systemName: icon).foregroundStyle(Club.sun); Text(label) }
    }
}

/// The stripe wipe between screens: three diagonal bands sweep across and away.
struct ClubWipe: View {
    let trigger: Int
    @State private var progress: CGFloat = 1.2
    @State private var active = false
    var body: some View {
        GeometryReader { g in
            let w = g.size.width, h = g.size.height
            ZStack {
                ForEach(0..<3, id: \.self) { i in
                    let colors = [Club.sun, Club.coral, Club.violet]
                    Rectangle().fill(colors[i])
                        .frame(width: w * 0.5, height: h * 2)
                        .rotationEffect(.degrees(20))
                        .offset(x: (progress - CGFloat(i) * 0.12) * (w * 1.6) - w * 0.8)
                }
            }
            .frame(width: w, height: h).clipped()
        }
        .opacity(active ? 1 : 0)   // only visible while it sweeps
        .allowsHitTesting(false)
        .onChange(of: trigger) { _, _ in
            guard !Club.still else { return }
            progress = -0.6; active = true
            withAnimation(.easeInOut(duration: 0.55)) { progress = 1.6 } completion: { active = false }
        }
        .ignoresSafeArea()
    }
}

// MARK: - Approved Island menus (ArtDir/ui/toybox-simple-v6)
// Deliberately separate from Arcade: the in-match serve meter keeps its original tokens.
enum IslandUI {
    static let navy = Color(hex: "09286F")
    static let lime = Color(hex: "D3F34B")
    static let paper = Color(hex: "FAF8F3")
    static let muted = Color(hex: "516383")
    static func font(_ size: CGFloat, bold: Bool = false) -> Font {
        .system(size: size, weight: bold ? .bold : .medium, design: .rounded)
    }
}

struct IslandBackdrop: View {
    var intro = false
    var body: some View {
        GeometryReader { g in
            Image(uiImage: UIImage(named: intro ? "island-intro.png" : "island-terrace.png") ?? UIImage(named: "scene-home.jpg") ?? UIImage())
                .resizable().scaledToFill().frame(width: g.size.width, height: g.size.height).clipped()
        }.ignoresSafeArea().accessibilityHidden(true)
    }
}

struct IslandPalm: Shape {
    func path(in r: CGRect) -> Path {
        var p = Path()
        func move(_ x: CGFloat, _ y: CGFloat) { p.move(to: CGPoint(x: r.width*x, y: r.height*y)) }
        func curve(_ x: CGFloat, _ y: CGFloat, _ a: CGFloat, _ b: CGFloat, _ c: CGFloat, _ d: CGFloat) {
            p.addCurve(to: CGPoint(x:r.width*x,y:r.height*y), control1: CGPoint(x:r.width*a,y:r.height*b), control2: CGPoint(x:r.width*c,y:r.height*d))
        }
        move(0.43,1); curve(0.5,0.34,0.42,0.72,0.48,0.48); curve(0.55,1,0.53,0.61,0.55,0.85); p.closeSubpath()
        for tip in [(0.03,0.16,0.27,0.0), (0.01,0.57,0.10,0.27), (0.26,0.88,0.24,0.48), (0.68,0.85,0.69,0.44), (0.94,0.56,0.87,0.27), (0.91,0.16,0.68,0.02)] {
            move(0.51,0.36)
            curve(tip.0,tip.1,tip.2,tip.3,tip.2,tip.3)
            curve(0.51,0.36,(tip.0+0.51)/2,(tip.1+0.36)/2,0.48,0.48)
            p.closeSubpath()
        }
        return p
    }
}

struct IslandWordmark: View {
    var size: CGFloat = 36
    var body: some View {
        VStack(spacing: 0) {
            ZStack(alignment: .topTrailing) {
                Circle().fill(Color(hex: "FFD145")).frame(width: size * 0.9, height: size * 0.9).offset(x: size * 0.25)
                IslandPalm().fill(IslandUI.navy).frame(width: size * 1.5, height: size * 1.12)
            }.frame(height: size * 1.12)
            Text("ISLAND").font(IslandUI.font(size, bold: true)).tracking(size * 0.035)
            Text("SPORTS CLUB").font(IslandUI.font(size * 0.32, bold: true)).tracking(size * 0.095)
        }.foregroundStyle(IslandUI.navy).accessibilityElement(children: .ignore).accessibilityLabel("Island Sports Club")
    }
}

struct IslandAction: View {
    let title: String
    var focused = false
    var primary = false
    var tint: Color? = nil
    var compact = false
    var identifier = ""
    let action: () -> Void
    @Environment(\.accessibilityReduceMotion) private var reduceMotion
    @ScaledMetric(relativeTo: .title3) private var typeScale = 1.0
    var body: some View {
        Button(action: action) {
            HStack(spacing: 16) {
                Text(title).font(IslandUI.font(compact ? 20 * min(typeScale, 1.5) : 27, bold: true)).lineLimit(1).minimumScaleFactor(0.8)
                Spacer(minLength: 10)
                if primary { Image(systemName: "play.fill").font(.system(size: 15, weight: .bold)) }
            }
            .foregroundStyle(IslandUI.navy)
            .padding(.horizontal, 22).frame(minHeight: compact ? 48 : 58)
            .background(primary ? (tint ?? IslandUI.lime) : focused ? Color.white.opacity(0.8) : .clear, in: RoundedRectangle(cornerRadius: primary ? 26 : 12))
            .overlay(RoundedRectangle(cornerRadius: primary ? 26 : 12).strokeBorder(IslandUI.navy.opacity(focused ? 0.8 : 0), lineWidth: 2))
            .shadow(color: IslandUI.navy.opacity(primary ? 0.09 : 0), radius: 3, y: 2)
        }.buttonStyle(.plain).accessibilityLabel(title).accessibilityIdentifier(identifier)
            .accessibilityAddTraits(focused ? .isSelected : [])
            .animation(reduceMotion ? nil : .easeOut(duration: 0.15), value: focused)
    }
}

struct IslandShell<Content: View>: View {
    var title: String
    var compact: Bool
    @ViewBuilder var content: () -> Content
    var body: some View {
        ZStack {
            IslandBackdrop()
            if compact { IslandUI.paper.opacity(0.92).ignoresSafeArea() }
            else { LinearGradient(colors: [IslandUI.paper.opacity(0.88), .clear], startPoint: .top, endPoint: .center).ignoresSafeArea().allowsHitTesting(false) }
            VStack(alignment: .leading, spacing: compact ? 18 : 24) {
                HStack(alignment: .center) {
                    if !compact { IslandWordmark(size: 28).frame(width: 180); Divider().frame(height: 52).padding(.horizontal, 20) }
                    Text(title).font(IslandUI.font(compact ? 30 : 42, bold: true)).foregroundStyle(IslandUI.navy)
                    Spacer()
                }
                content().frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
            }.padding(.horizontal, compact ? 22 : 52).padding(.vertical, compact ? 22 : 36)
        }.preferredColorScheme(.light)
    }
}

struct IslandPlayer: View {
    let player: Player
    var sport: Sport? = nil
    var body: some View {
        GeometryReader { g in
            ZStack {
                Ellipse().fill(IslandUI.navy.opacity(0.14)).frame(width: g.size.width * 0.35, height: 16).blur(radius: 10)
                    .position(x: g.size.width * 0.52, y: g.size.height * 0.94).allowsHitTesting(false)
                CharacterModelPreview(player: player, cameraDistance: 2.95, idleSport: sport)
                    .accessibilityLabel("\(player.name), equipped character")
            }
        }
    }
}

struct IslandSelector: View {
    let label: String
    let value: String
    var swatch: Color? = nil
    var focused = false
    var compact = false
    let step: (Int) -> Void
    var body: some View {
        HStack(spacing: 12) {
            Text(label).font(IslandUI.font(compact ? 15 : 20, bold: true)).lineLimit(1).minimumScaleFactor(0.8)
            Spacer(minLength: 8)
            if let swatch { Circle().fill(swatch).frame(width: 23, height: 23).overlay(Circle().strokeBorder(IslandUI.navy.opacity(0.18))) }
            Button { step(-1) } label: { Image(systemName: "chevron.left").frame(width: 36, height: 44) }
                .accessibilityLabel("Previous \(label)")
            Text(value).font(IslandUI.font(compact ? 15 : 20, bold: true)).lineLimit(1).minimumScaleFactor(0.7)
                .frame(width: compact ? 96 : 152)
            Button { step(1) } label: { Image(systemName: "chevron.right").frame(width: 36, height: 44) }
                .accessibilityLabel("Next \(label)")
        }.buttonStyle(.plain).foregroundStyle(IslandUI.navy)
            .padding(.horizontal, compact ? 12 : 18)
            .background(focused ? IslandUI.lime : Color.white.opacity(0.9), in: RoundedRectangle(cornerRadius: 13))
            .shadow(color: IslandUI.navy.opacity(0.06), radius: 3, y: 2)
            .accessibilityElement(children: .contain)
    }
}
