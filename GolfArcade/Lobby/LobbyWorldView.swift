import SceneKit
import SwiftUI
import UIKit

// MARK: - the world on screen

/// The lobby as a screen: the 3D world with the controls on top. On the phone (`compact`) the controls are touch: a joystick where the thumb lands, tap to walk, A, Quick Menu.
/// On the TV they are not drawn: the phone is the remote, and the TV shows where you are and what A will do.
struct LobbyWorldScreen: View {
    @Bindable var menu: TennisMenu
    let compact: Bool
    @State private var world = LobbyWorld.shared
    /// The scene is built the first time the screen is made (and stays built); false means it could not be, and the classic menus take over.
    @State private var ready = LobbyWorld.shared.prepare()

    var body: some View {
        ZStack {
            Color(red: 0.55, green: 0.76, blue: 0.9).ignoresSafeArea()
            if ready { LobbySceneView(world: world).ignoresSafeArea() }
            if compact { LobbyTouchControls(world: world, menu: menu) } else { LobbyTVOverlay(world: world) }
            LobbyChrome(world: world, menu: menu, compact: compact)
            if world.quickMenuOpen { LobbyQuickMenu(world: world, menu: menu, compact: compact) }
        }
        .onAppear { world.appear(menu: menu) }
        .onDisappear { world.disappear() }
        .accessibilityIdentifier("lobby-world")
    }
}

/// The SceneKit view. It reports its shape (so the camera frames a phone and a TV differently) and turns a tap into a spot on the ground.
struct LobbySceneView: UIViewRepresentable {
    let world: LobbyWorld
    final class View: SCNView {
        var onLayout: ((CGSize) -> Void)?
        override func layoutSubviews() { super.layoutSubviews(); onLayout?(bounds.size) }
    }
    func makeUIView(context: Context) -> View {
        let view = View(frame: .zero, options: [SCNView.Option.preferredRenderingAPI.rawValue: SCNRenderingAPI.metal.rawValue])
        view.scene = world.lobbyScene?.scene
        view.pointOfView = world.lobbyScene?.cameraNode
        view.antialiasingMode = .multisampling4X
        view.preferredFramesPerSecond = 60
        view.isPlaying = true; view.rendersContinuously = true
        view.autoenablesDefaultLighting = false; view.allowsCameraControl = false
        view.backgroundColor = .clear
        view.isUserInteractionEnabled = false
        view.accessibilityIdentifier = "lobby-scene"
        view.onLayout = { [weak world] size in world?.setViewport(size: size) }
        world.setViewport(size: view.bounds.size)
        world.groundHit = { [weak view, weak world] point in
            guard let view, let plane = world?.lobbyScene?.tapPlane else { return nil }
            let hits = view.hitTest(point, options: [.rootNode: plane, .searchMode: SCNHitTestSearchMode.all.rawValue, .ignoreHiddenNodes: false])
            guard let hit = hits.first(where: { $0.node === plane }) else { return nil }
            return LobbyWorld.P(Float(hit.worldCoordinates.x), -Float(hit.worldCoordinates.z))
        }
        return view
    }
    func updateUIView(_ view: View, context: Context) {
        if view.scene !== world.lobbyScene?.scene { view.scene = world.lobbyScene?.scene; view.pointOfView = world.lobbyScene?.cameraNode }
    }
    static func dismantleUIView(_ view: View, coordinator: ()) { view.isPlaying = false; view.scene = nil }
}

// MARK: - what the screen says

enum LobbyCopy {
    static func prompt(_ s: LobbyLayout.Station) -> String {
        switch s.action {
        case .locker: "Open \(s.title)"
        case .play: "Play \(s.title)"
        case .howToPlay: "How to Play"
        case .party: "Open the party"
        case .shop: "Coming soon"
        }
    }
}

/// The name of where you are, the party, the Quick Menu button, and the prompt for the station you stand in.
private struct LobbyChrome: View {
    let world: LobbyWorld
    let menu: TennisMenu
    let compact: Bool
    private var party: [MultiplayerParticipant] { world.party }
    var body: some View {
        let u: CGFloat = compact ? 1 : 1.25
        VStack(spacing: 0) {
            HStack(alignment: .top) {
                VStack(alignment: .leading, spacing: 8 * u) {
                    chip(icon: "mappin.circle.fill", text: world.nearStation?.title ?? "The Club", u: u)
                    if party.count > 1 {
                        Button { menu.openParty() } label: { chip(icon: "person.3.fill", text: "Party  \(party.filter(\.connected).count) / 4", u: u) }
                            .buttonStyle(.plain).accessibilityIdentifier("lobby-party")
                    }
                }
                Spacer()
                if compact {
                    Button { world.toggleQuickMenu() } label: {
                        HStack(spacing: 8) {
                            Image(systemName: "line.3.horizontal").font(.system(size: 16, weight: .black))
                            Text("Quick Menu").font(IslandUI.font(16, bold: true))
                        }
                        .foregroundStyle(.white).padding(.horizontal, 16).frame(height: 46)
                        .background(IslandUI.navy, in: Capsule()).shadow(color: .black.opacity(0.25), radius: 6, y: 3)
                    }.accessibilityIdentifier("lobby-quick-menu")
                }
            }
            .padding(.horizontal, 16 * u).padding(.top, 12 * u)
            Spacer()
            if !compact, let s = world.nearStation, !world.quickMenuOpen { LobbyPrompt(station: s, u: u).padding(.bottom, 8 * u) }
            if !compact { LobbyHintBar().padding(.bottom, 26).padding(.top, 10) }
        }
        .allowsHitTesting(compact)
    }
    private func chip(icon: String, text: String, u: CGFloat) -> some View {
        HStack(spacing: 8 * u) {
            Image(systemName: icon).font(.system(size: 16 * u, weight: .bold)).foregroundStyle(IslandUI.navy)
            Text(text).font(IslandUI.font(17 * u, bold: true)).foregroundStyle(IslandUI.navy)
        }
        .padding(.horizontal, 14 * u).frame(height: 44 * u)
        .background(IslandUI.paper, in: Capsule()).shadow(color: .black.opacity(0.18), radius: 6, y: 2)
    }
}

/// What A will do here: the name of the station and a line about it.
struct LobbyPrompt: View {
    let station: LobbyLayout.Station
    var u: CGFloat = 1
    var body: some View {
        let s = station
        HStack(spacing: 12 * u) {
            ZStack {
                Circle().fill(s.locked ? Color.gray.opacity(0.5) : IslandUI.lime).frame(width: 42 * u, height: 42 * u)
                Circle().strokeBorder(IslandUI.navy, lineWidth: 3).frame(width: 42 * u, height: 42 * u)
                if s.locked { Image(systemName: "lock.fill").font(.system(size: 17 * u, weight: .black)).foregroundStyle(IslandUI.navy) }
                else { Text("A").font(IslandUI.font(20 * u, bold: true)).foregroundStyle(IslandUI.navy) }
            }
            VStack(alignment: .leading, spacing: 1) {
                Text(LobbyCopy.prompt(s)).font(IslandUI.font(20 * u, bold: true)).foregroundStyle(IslandUI.navy).lineLimit(1).minimumScaleFactor(0.8)
                Text(s.detail).font(IslandUI.font(13 * u)).foregroundStyle(IslandUI.muted).lineLimit(1).minimumScaleFactor(0.8)
            }
        }
        .padding(.leading, 12 * u).padding(.trailing, 20 * u).padding(.vertical, 10 * u)
        .background(IslandUI.paper, in: Capsule()).shadow(color: .black.opacity(0.22), radius: 8, y: 3)
        .transition(.scale.combined(with: .opacity))
        .accessibilityIdentifier("lobby-prompt")
    }
}

/// The TV's legend, as the app's other TV screens have one.
private struct LobbyHintBar: View {
    var body: some View {
        HStack(spacing: 30) {
            hint("arrow.up.and.down.and.arrow.left.and.right", "Hold to walk")
            hint("a.circle.fill", "Use")
            hint("b.circle.fill", "Back")
            hint("house.fill", "Quick Menu")
        }
        .font(IslandUI.font(20, bold: true)).foregroundStyle(.white)
        .padding(.horizontal, 26).padding(.vertical, 12)
        .background(Capsule().fill(Color(red: 0.1, green: 0.13, blue: 0.23).opacity(0.82)))
    }
    private func hint(_ icon: String, _ label: String) -> some View {
        HStack(spacing: 8) { Image(systemName: icon).foregroundStyle(IslandUI.lime); Text(label) }
    }
}

/// On the TV the controls are the phone's; nothing to draw but what the remote is doing.
private struct LobbyTVOverlay: View {
    let world: LobbyWorld
    var body: some View { Color.clear.allowsHitTesting(false) }
}

// MARK: - touch (the phone on its own)

private struct LobbyTouchControls: View {
    let world: LobbyWorld
    let menu: TennisMenu
    @State private var origin: CGPoint?
    @State private var knob: CGSize = .zero
    @State private var began = Date()
    @State private var travelled: CGFloat = 0
    private let radius: CGFloat = 58
    var body: some View {
        ZStack {
            // the whole screen steers: a joystick appears under the thumb, a quick tap walks to that spot
            Color.clear.contentShape(Rectangle())
                .gesture(DragGesture(minimumDistance: 0, coordinateSpace: .local)
                    .onChanged { g in
                        if origin == nil { origin = g.startLocation; began = Date(); travelled = 0; knob = .zero }
                        let dx = g.location.x - g.startLocation.x, dy = g.location.y - g.startLocation.y
                        travelled = max(travelled, hypot(dx, dy))
                        guard travelled > 10 else { return }
                        let d = hypot(dx, dy), k = min(d, radius) / max(d, 1)
                        knob = CGSize(width: dx * k, height: dy * k)
                        world.setStick(SIMD2(Float(dx * k / radius), Float(-dy * k / radius)))
                    }
                    .onEnded { g in
                        defer { origin = nil; knob = .zero; world.setStick(SIMD2(0, 0)) }
                        if travelled <= 10, Date().timeIntervalSince(began) < 0.4 { world.tap(at: g.location) }
                    })
            if let origin, travelled > 10 {
                ZStack {
                    Circle().fill(IslandUI.navy.opacity(0.30)).overlay(Circle().strokeBorder(.white.opacity(0.9), lineWidth: 3))
                        .frame(width: radius * 2, height: radius * 2)
                    Circle().fill(IslandUI.lime).overlay(Circle().strokeBorder(IslandUI.navy, lineWidth: 3))
                        .frame(width: 58, height: 58).offset(knob)
                }.position(origin).allowsHitTesting(false)
            }
            VStack {
                Spacer()
                HStack(alignment: .bottom) {
                    VStack(alignment: .leading, spacing: 10) {
                        if let s = world.nearStation, !world.quickMenuOpen { LobbyPrompt(station: s, u: 0.9) }
                        if origin == nil && !world.steering { Text("Touch anywhere to walk").font(IslandUI.font(14, bold: true)).foregroundStyle(.white)
                            .padding(.horizontal, 14).padding(.vertical, 8).background(IslandUI.navy.opacity(0.7), in: Capsule()).allowsHitTesting(false) }
                    }.padding(.leading, 16).animation(.spring(response: 0.3, dampingFraction: 0.75), value: world.nearStation?.id)
                    Spacer(minLength: 8)
                    VStack(spacing: 14) {
                        emoteButton
                        aButton
                    }.padding(.trailing, 20)
                }.padding(.bottom, 26)
            }
            if world.emoteMenuOpen { emoteRow }
        }
    }
    private var aButton: some View {
        let near = world.nearStation
        return Button { world.activate(menu: menu) } label: {
            ZStack {
                Circle().fill(near == nil ? Color.white.opacity(0.55) : (near!.locked ? Color.gray.opacity(0.6) : IslandUI.lime)).frame(width: 84, height: 84)
                Circle().strokeBorder(IslandUI.navy, lineWidth: 4).frame(width: 84, height: 84)
                Text("A").font(IslandUI.font(38, bold: true)).foregroundStyle(IslandUI.navy)
            }
            .shadow(color: .black.opacity(0.3), radius: 6, y: 4)
            .scaleEffect(near == nil ? 0.92 : 1.0).animation(.spring(response: 0.3, dampingFraction: 0.6), value: near?.id)
        }.buttonStyle(.plain).accessibilityIdentifier("lobby-a")
    }
    private var emoteButton: some View {
        Button { world.emoteMenuOpen.toggle() } label: {
            Image(systemName: "face.smiling.fill").font(.system(size: 24, weight: .bold)).foregroundStyle(IslandUI.navy)
                .frame(width: 54, height: 54).background(IslandUI.paper, in: Circle()).overlay(Circle().strokeBorder(IslandUI.navy.opacity(0.25), lineWidth: 2))
                .shadow(color: .black.opacity(0.25), radius: 5, y: 3)
        }.buttonStyle(.plain).accessibilityIdentifier("lobby-emote")
    }
    private var emoteRow: some View {
        VStack { Spacer()
            HStack(spacing: 10) {
                ForEach(Array((menu.player?.equippedEmotes ?? EmoteCatalog.defaults).enumerated()), id: \.offset) { i, id in
                    Button { world.playEmote(slot: i) } label: {
                        Text(EmoteCatalog.name(id)).font(IslandUI.font(16, bold: true)).foregroundStyle(IslandUI.navy)
                            .padding(.horizontal, 16).frame(height: 46).background(IslandUI.paper, in: Capsule()).overlay(Capsule().strokeBorder(IslandUI.lime, lineWidth: 3))
                    }.buttonStyle(.plain)
                }
            }.padding(.bottom, 160)
        }
    }
}

// MARK: - the Quick Menu

private struct LobbyQuickMenu: View {
    let world: LobbyWorld
    let menu: TennisMenu
    let compact: Bool
    var body: some View {
        ZStack {
            Color.black.opacity(0.35).ignoresSafeArea().onTapGesture { world.quickMenuOpen = false }
            VStack(spacing: 0) {
                if compact { Spacer() }
                VStack(alignment: .leading, spacing: compact ? 10 : 12) {
                    HStack {
                        Text("Quick Menu").font(IslandUI.font(compact ? 26 : 34, bold: true)).foregroundStyle(IslandUI.navy)
                        Spacer()
                        Text("Jump to").font(IslandUI.font(compact ? 14 : 18)).foregroundStyle(IslandUI.muted)
                    }
                    ForEach(Array(world.quickEntries.enumerated()), id: \.element.id) { i, e in row(e, focused: !compact && i == world.quickIndex) }
                    Text(compact ? "Prefer the old menus?  Settings > Display > Walkable club" : "A to go · B to close · Settings > Display > Walkable club for the classic menus")
                        .font(IslandUI.font(compact ? 12 : 15)).foregroundStyle(IslandUI.muted).padding(.top, 4)
                }
                .padding(compact ? 18 : 28).frame(maxWidth: compact ? .infinity : 560)
                .background(IslandUI.paper, in: RoundedRectangle(cornerRadius: compact ? 34 : 30, style: .continuous))
                .shadow(color: .black.opacity(0.3), radius: 16, y: 6)
                .padding(compact ? 12 : 0)
                if compact { Spacer().frame(height: 10) }
            }
        }
        .transition(.opacity)
        .accessibilityIdentifier("lobby-quick")
    }
    private func row(_ e: LobbyQuickEntry, focused: Bool) -> some View {
        Button { world.quickGo(e.id, menu: menu) } label: {
            HStack(spacing: 14) {
                ZStack { Circle().fill(e.locked ? Color.gray.opacity(0.3) : IslandUI.lime).frame(width: 40, height: 40)
                    Image(systemName: e.icon).font(.system(size: 18, weight: .bold)).foregroundStyle(e.locked ? IslandUI.muted : IslandUI.navy) }
                VStack(alignment: .leading, spacing: 1) {
                    Text(e.title).font(IslandUI.font(compact ? 19 : 24, bold: true)).foregroundStyle(e.locked ? IslandUI.muted : IslandUI.navy)
                    Text(e.detail).font(IslandUI.font(compact ? 12 : 15)).foregroundStyle(IslandUI.muted)
                }
                Spacer()
                if !e.locked { Image(systemName: "chevron.right").font(.system(size: 15, weight: .black)).foregroundStyle(IslandUI.navy) }
            }
            .padding(.horizontal, 14).padding(.vertical, 9)
            .background(focused ? IslandUI.lime.opacity(0.55) : Color.white.opacity(e.locked ? 0.4 : 0.9), in: RoundedRectangle(cornerRadius: 22, style: .continuous))
            .overlay(RoundedRectangle(cornerRadius: 22, style: .continuous).strokeBorder(focused ? IslandUI.navy : .clear, lineWidth: 3))
        }.buttonStyle(.plain).accessibilityIdentifier("quick-\(e.id)")
    }
}

// MARK: - the phone as the TV's remote

/// What the phone shows while the TV is in the world: arrows you hold to walk, A to use a station, B to back out, Home for the Quick Menu.
struct LobbyRemotePad: View {
    let world: LobbyWorld
    let menu: TennisMenu
    var body: some View {
        VStack(spacing: 16) {
            Spacer(minLength: 0)
            let cell: CGFloat = 92, gap: CGFloat = 8
            ZStack {
                holdArrow("arrowtriangle.up.fill", 0).offset(y: -(cell + gap))
                holdArrow("arrowtriangle.left.fill", 2).offset(x: -(cell + gap))
                holdArrow("arrowtriangle.right.fill", 3).offset(x: cell + gap)
                holdArrow("arrowtriangle.down.fill", 1).offset(y: cell + gap)
                Circle().strokeBorder(.white.opacity(0.14), style: StrokeStyle(lineWidth: 2, dash: [4, 5])).frame(width: 62, height: 62)
            }.frame(width: cell * 3 + gap * 2, height: cell * 3 + gap * 2)
            Text("Hold to walk · two arrows for diagonals").font(IslandUI.font(13, bold: true)).foregroundStyle(.white.opacity(0.55))
            Spacer(minLength: 0)
            HStack(alignment: .center, spacing: 26) {
                round("‹", "Back", size: 74, filled: false) { world.back() }
                round("A", "Use", size: 108, filled: true) { world.activate(menu: menu) }
                round("⌂", "Quick Menu", size: 74, filled: false) { world.toggleQuickMenu() }
            }
            HStack(spacing: 10) {
                ForEach(Array((menu.player?.equippedEmotes ?? EmoteCatalog.defaults).enumerated()), id: \.offset) { i, id in
                    Button { world.playEmote(slot: i) } label: {
                        Text(EmoteCatalog.name(id)).font(IslandUI.font(14, bold: true)).foregroundStyle(.white)
                            .padding(.horizontal, 14).frame(height: 38).overlay(Capsule().strokeBorder(.white.opacity(0.4), lineWidth: 2))
                    }.buttonStyle(.plain)
                }
            }
        }
    }
    private func holdArrow(_ icon: String, _ index: Int) -> some View {
        HoldArrow(world: world, index: index, icon: icon)
    }
    private func round(_ label: String, _ caption: String, size: CGFloat, filled: Bool, action: @escaping () -> Void) -> some View {
        VStack(spacing: 8) {
            Button(action: action) {
                Text(label).font(IslandUI.font(size * 0.42, bold: true)).foregroundStyle(filled ? IslandUI.navy : .white)
                    .frame(width: size, height: size).background(filled ? IslandUI.lime : .clear, in: Circle())
                    .overlay(Circle().strokeBorder(filled ? .clear : .white.opacity(0.42), lineWidth: 3))
            }.buttonStyle(.plain).accessibilityLabel(caption).accessibilityIdentifier("remote-\(caption.lowercased().replacingOccurrences(of: " ", with: "-"))")
            Text(caption.uppercased()).font(IslandUI.font(11, bold: true)).tracking(1.4).foregroundStyle(.white.opacity(0.6))
        }
    }
}

/// An arrow that walks while it is held. Two held at once make a diagonal.
private struct HoldArrow: View {
    let world: LobbyWorld
    let index: Int
    let icon: String
    @State private var pressed = false
    var body: some View {
        Image(systemName: icon).font(.system(size: 34, weight: .bold))
            .foregroundStyle(pressed ? IslandUI.lime : .white).frame(width: 92, height: 92)
            .background(pressed ? IslandUI.lime.opacity(0.22) : .white.opacity(0.09), in: RoundedRectangle(cornerRadius: 26, style: .continuous))
            .overlay(RoundedRectangle(cornerRadius: 26, style: .continuous).strokeBorder(pressed ? IslandUI.lime : .white.opacity(0.22), lineWidth: 2.5))
            .scaleEffect(pressed ? 0.95 : 1)
            .contentShape(Rectangle())
            .gesture(DragGesture(minimumDistance: 0)
                .onChanged { _ in if !pressed { pressed = true; world.arrow(index, down: true) } }
                .onEnded { _ in pressed = false; world.arrow(index, down: false) })
            .accessibilityLabel("Walk \(["up", "down", "left", "right"][index])")
            .accessibilityIdentifier("walk-\(["up", "down", "left", "right"][index])")
    }
}
/// The TV's legend, as the app's other TV screens have one.
