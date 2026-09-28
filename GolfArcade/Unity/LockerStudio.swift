import SwiftUI
import SceneKit

/// The phone locker. Cosmetics-shop layout (Switch Sports / Mii Maker / Fortnite locker): a big turntable of your
/// player on top, category tabs, then visual pickers — thumbnail cards for styles, swatch rows and continuous
/// colour ranges (LockerColors.swift) for every colour. Hair and headwear tabs zoom the camera to the head.
struct LockerStudio: View {
    let menu: TennisMenu
    @State private var tab: Tab
    init(menu: TennisMenu, startTab: Tab = .body) { self.menu = menu; _tab = State(initialValue: startTab) }
    @State private var outfitSlot = "shirt"
    @State private var editingName = false
    @State private var name = ""
    @State private var lastSave = Date.distantPast

    enum Tab: String, CaseIterable, Identifiable {
        case body, hair, headwear, outfit, racket
        var id: String { rawValue }
        var title: String { ["Body", "Hair", "Headwear", "Outfit", "Racket"][Self.allCases.firstIndex(of: self)!] }
        var icon: String { ["figure.stand", "scissors", "sun.max.fill", "tshirt.fill", "tennis.racket"][Self.allCases.firstIndex(of: self)!] }
    }

    var body: some View {
        let p = menu.player ?? Player(name: "Player 1", colorIndex: 0)
        GeometryReader { geo in
            VStack(spacing: 10) {
                stage(p).frame(height: max(260, geo.size.height * 0.46))
                tabBar
                ScrollView(showsIndicators: false) {
                    VStack(alignment: .leading, spacing: 16) { panel(p) }
                        .padding(14).frame(maxWidth: .infinity, alignment: .leading)
                }
                .background(RoundedRectangle(cornerRadius: 24, style: .continuous).fill(Club.lagoonDeep.opacity(0.78)))
                .overlay(RoundedRectangle(cornerRadius: 24, style: .continuous).strokeBorder(.white.opacity(0.12), lineWidth: 1))
                HStack(spacing: 10) {
                    ClubButton(title: "Shuffle", icon: "dice.fill", focused: false, style: .secondary, size: 16) { menu.tap("randomize") }
                    ClubButton(title: "Done", icon: "checkmark", focused: false, size: 16) { menu.tap("back") }
                        .frame(maxWidth: .infinity, alignment: .trailing)
                }
            }
        }
        .onAppear { name = p.name }
    }

    // MARK: stage

    private func stage(_ p: Player) -> some View {
        ZStack(alignment: .bottom) {
            RoundedRectangle(cornerRadius: 28, style: .continuous)
                .fill(RadialGradient(colors: [Color(hex: "FFE7B8").opacity(0.55), Color(hex: "FF9F7A").opacity(0.18), .clear],
                                     center: .init(x: 0.5, y: 0.42), startRadius: 10, endRadius: 260))
            CharacterModelPreview(player: p, cameraDistance: 3.3, framing: tab == .hair || tab == .headwear ? .head : .body)
                .padding(.bottom, 34)
            VStack {
                HStack {
                    Label("Drag to spin", systemImage: "arrow.left.and.right").font(Club.ui(11, 700)).foregroundStyle(.white.opacity(0.75))
                        .padding(.horizontal, 10).padding(.vertical, 5).background(Capsule().fill(.black.opacity(0.28)))
                    Spacer()
                }
                Spacer()
            }.padding(10)
            nameTag(p).padding(.bottom, 8)
        }
    }

    private func nameTag(_ p: Player) -> some View {
        Group {
            if editingName {
                TextField("Your name", text: $name)
                    .font(Club.title(18)).foregroundStyle(Club.ink).multilineTextAlignment(.center)
                    .submitLabel(.done).onSubmit { rename(name); editingName = false }
                    .frame(width: 180)
            } else {
                Button { name = p.name; editingName = true } label: {
                    HStack(spacing: 6) { Text(p.name).font(Club.title(18)); Image(systemName: "pencil").font(.system(size: 12, weight: .black)) }
                }.buttonStyle(.plain)
            }
        }
        .foregroundStyle(Club.ink).padding(.horizontal, 18).padding(.vertical, 7).background(Capsule().fill(Club.sun))
        .accessibilityLabel("Name, \(p.name)")
    }

    private var tabBar: some View {
        HStack(spacing: 6) {
            ForEach(Tab.allCases) { t in
                Button { withAnimation(Club.spring) { tab = t } } label: {
                    VStack(spacing: 3) {
                        Image(systemName: t.icon).font(.system(size: 18, weight: .bold))
                        Text(t.title).font(Club.ui(11, 700)).lineLimit(1).minimumScaleFactor(0.8)
                    }
                    .foregroundStyle(tab == t ? Club.ink : .white)
                    .frame(maxWidth: .infinity).padding(.vertical, 8)
                    .background(RoundedRectangle(cornerRadius: 14, style: .continuous).fill(tab == t ? Club.sun : .white.opacity(0.12)))
                }
                .buttonStyle(.plain).accessibilityAddTraits(tab == t ? .isSelected : [])
            }
        }
    }

    // MARK: panels

    @ViewBuilder private func panel(_ p: Player) -> some View {
        switch tab {
        case .body:
            section("Player") {
                HStack(spacing: 10) {
                    card("Boy", icon: "figure.stand", selected: !p.standardFemale) { edit { $0.standardFemale = false; if $0.haircutValue == nil { $0.haircut = 0 } } }
                    card("Girl", icon: "figure.stand.dress", selected: p.standardFemale) { edit { $0.standardFemale = true; if $0.haircutValue == nil { $0.haircut = 1 } } }
                }
            }
            section("Skin tone", swatch: Color(hex: p.skinHex)) {
                RangeSlider(value: p.skinT, stops: LockerColor.skinStops.map { Color(hex: $0) }, label: "Skin tone") { t, done in edit(save: done) { $0.setSkin(t) } }
                swatchRow(LockerColor.skinPresets.map { (LockerColor.ramp(LockerColor.skinStops, $0), $0) }, selected: p.skinT) { t in edit { $0.setSkin(t) } }
            }
            section("Plays") {
                HStack(spacing: 10) {
                    card("Right-handed", icon: "hand.raised.fill", selected: p.handedness != .left) { edit { $0.handedness = .right } }
                    card("Left-handed", icon: "hand.raised.fill", selected: p.handedness == .left, mirror: true) { edit { $0.handedness = .left } }
                }
            }
        case .hair:
            section("Hairstyle") {
                LazyVGrid(columns: Array(repeating: GridItem(.flexible(), spacing: 10), count: 3), spacing: 10) {
                    ForEach(0..<HeroV4.offered, id: \.self) { i in
                        thumbCard(HeroV4.haircuts[i], image: LockerThumbs.image(p, haircut: i, headwear: 0), selected: p.shownHaircut == i) { edit { $0.haircut = i } }
                    }
                }
            }
            section("Natural colour", swatch: p.hairDyed ? nil : Color(hex: p.hairHex)) {
                RangeSlider(value: p.hairT, stops: LockerColor.hairStops.map { Color(hex: $0) }, label: "Hair colour") { t, done in edit(save: done) { $0.setHair(natural: t) } }
                swatchRow(LockerColor.hairPresets.map { (LockerColor.ramp(LockerColor.hairStops, $0.1), $0.1) }, selected: p.hairDyed ? -1 : p.hairT) { t in edit { $0.setHair(natural: t) } }
            }
            section("Dye", swatch: p.hairDyed ? Color(hex: p.hairHex) : nil) {
                let v = p.look["hair"] ?? []
                RangeSlider(value: p.hairDyed ? v[0] : 0.6, stops: LockerColor.rainbow, label: "Hair dye", dimmed: !p.hairDyed) { h, done in
                    edit(save: done) { $0.setHair(dyeHue: h, shade: p.hairDyed ? v[1] : 0.1) }
                }
            }
        case .headwear:
            section("Headwear") {
                LazyVGrid(columns: Array(repeating: GridItem(.flexible(), spacing: 10), count: 2), spacing: 10) {
                    ForEach(HeroV4.headwear.indices, id: \.self) { i in
                        thumbCard(HeroV4.headwear[i], image: LockerThumbs.image(p, haircut: p.shownHaircut, headwear: i), selected: p.hairStyle == i) { edit { $0.hairStyle = i } }
                    }
                }
            }
        case .outfit:
            HStack(spacing: 8) {
                ForEach([("shirt", "Shirt"), ("shorts", "Shorts"), ("accent", "Shoes")], id: \.0) { slot, title in
                    Button { withAnimation(Club.spring) { outfitSlot = slot } } label: {
                        HStack(spacing: 6) {
                            Circle().fill(p.outfitColor(slot) ?? .white.opacity(0.3)).frame(width: 14, height: 14).overlay(Circle().strokeBorder(.white.opacity(0.6), lineWidth: 1))
                            Text(title).font(Club.ui(14, 700))
                        }
                        .foregroundStyle(outfitSlot == slot ? Club.ink : .white).padding(.horizontal, 12).padding(.vertical, 8)
                        .background(Capsule().fill(outfitSlot == slot ? Club.cream : .white.opacity(0.12)))
                    }.buttonStyle(.plain)
                }
            }
            colourEditor(p, slot: outfitSlot)
        case .racket:
            colourEditor(p, slot: "racket")
        }
    }

    /// Hue + shade ranges, the palette as quick swatches, and "Kit" (the club kit's own colour).
    @ViewBuilder private func colourEditor(_ p: Player, slot: String) -> some View {
        let (h, sh) = p.hueShade(slot)
        let kit = p.outfitHex(slot) == nil
        section("Colour", swatch: p.outfitColor(slot), detail: p.outfitName(slot)) {
            RangeSlider(value: h, stops: LockerColor.rainbow, label: "Hue", dimmed: kit) { v, done in edit(save: done) { $0.setOutfit(slot, hue: v, shade: sh) } }
            RangeSlider(value: (sh + 1) / 2, stops: [Color(hex: LockerColor.hueShade(h, -1)), Color(hex: LockerColor.hueShade(h, 0)), Color(hex: LockerColor.hueShade(h, 1))],
                        label: "Shade", dimmed: kit) { v, done in edit(save: done) { $0.setOutfit(slot, hue: h, shade: v * 2 - 1) } }
        }
        section("Quick picks") {
            ScrollView(.horizontal, showsIndicators: false) {
                HStack(spacing: 10) {
                    Button { edit { $0.clearOutfit(slot) } } label: {
                        Text("Kit").font(Club.ui(12, 800)).foregroundStyle(kit ? Club.ink : .white)
                            .frame(width: 40, height: 40).background(Circle().fill(kit ? Club.sun : .white.opacity(0.14)))
                    }.buttonStyle(.plain).accessibilityLabel("Kit colour")
                    ForEach(Outfit.palette.indices, id: \.self) { i in
                        let hex = Outfit.palette[i].hex
                        Button { let (a, b) = LockerColor.hueShadeOf(hex); edit { $0.setOutfit(slot, hue: a, shade: b) } } label: {
                            Circle().fill(Color(hex: hex)).frame(width: 40, height: 40)
                                .overlay(Circle().strokeBorder(p.outfitHex(slot) == LockerColor.hueShade(LockerColor.hueShadeOf(hex).0, LockerColor.hueShadeOf(hex).1) ? Club.sun : .white.opacity(0.35), lineWidth: 3))
                        }.buttonStyle(.plain).accessibilityLabel(Outfit.palette[i].name)
                    }
                }.padding(.vertical, 2)
            }
        }
    }

    // MARK: building blocks

    private func section<C: View>(_ title: String, swatch: Color? = nil, detail: String? = nil, @ViewBuilder _ content: () -> C) -> some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack(spacing: 8) {
                Text(title).font(Club.title(17)).foregroundStyle(.white)
                if let detail { Text(detail).font(Club.ui(13, 600)).foregroundStyle(.white.opacity(0.6)) }
                Spacer()
                if let swatch { Circle().fill(swatch).frame(width: 22, height: 22).overlay(Circle().strokeBorder(.white.opacity(0.7), lineWidth: 2)) }
            }
            content()
        }
    }

    private func card(_ title: String, icon: String, selected: Bool, mirror: Bool = false, action: @escaping () -> Void) -> some View {
        Button(action: action) {
            VStack(spacing: 6) {
                Image(systemName: icon).font(.system(size: 26, weight: .bold)).scaleEffect(x: mirror ? -1 : 1)
                Text(title).font(Club.ui(14, 700)).lineLimit(1).minimumScaleFactor(0.8)
            }
            .foregroundStyle(selected ? Club.ink : .white).frame(maxWidth: .infinity).padding(.vertical, 14)
            .background(RoundedRectangle(cornerRadius: 18, style: .continuous).fill(selected ? Club.sun : .white.opacity(0.1)))
        }
        .buttonStyle(.plain).accessibilityAddTraits(selected ? .isSelected : [])
    }

    private func thumbCard(_ title: String, image: UIImage?, selected: Bool, action: @escaping () -> Void) -> some View {
        Button(action: action) {
            VStack(spacing: 4) {
                ZStack {
                    RoundedRectangle(cornerRadius: 14, style: .continuous).fill(LinearGradient(colors: [Color(hex: "FFE2C4").opacity(0.5), Color(hex: "FF9F7A").opacity(0.25)], startPoint: .top, endPoint: .bottom))
                    if let image { Image(uiImage: image).resizable().scaledToFit().padding(2) }
                }.aspectRatio(1, contentMode: .fit)
                Text(title).font(Club.ui(12, 700)).lineLimit(1).minimumScaleFactor(0.8).foregroundStyle(selected ? Club.ink : .white)
            }
            .padding(6)
            .background(RoundedRectangle(cornerRadius: 18, style: .continuous).fill(selected ? Club.sun : .white.opacity(0.1)))
            .overlay(RoundedRectangle(cornerRadius: 18, style: .continuous).strokeBorder(selected ? Club.sunDeep : .clear, lineWidth: 2))
        }
        .buttonStyle(.plain).accessibilityLabel(title).accessibilityAddTraits(selected ? .isSelected : [])
    }

    private func swatchRow(_ items: [(String, Double)], selected: Double, pick: @escaping (Double) -> Void) -> some View {
        HStack(spacing: 0) {
            ForEach(items.indices, id: \.self) { i in
                Button { pick(items[i].1) } label: {
                    Circle().fill(Color(hex: items[i].0)).frame(width: 30, height: 30)
                        .overlay(Circle().strokeBorder(abs(selected - items[i].1) < 0.02 ? Club.sun : .white.opacity(0.35), lineWidth: 3))
                        .frame(maxWidth: .infinity)
                }.buttonStyle(.plain)
            }
        }
    }

    /// Change the player; saving to disk is throttled while a slider drags and always happens at the end.
    private func edit(save done: Bool = true, _ change: (inout Player) -> Void) {
        let s = SportsSession.shared
        guard s.players.indices.contains(s.playerIndex) else { return }
        change(&s.players[s.playerIndex])
        if done || Date().timeIntervalSince(lastSave) > 0.5 { s.savePlayers(); lastSave = Date() }
    }
    private func rename(_ text: String) {
        let t = text.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !t.isEmpty else { return }
        edit { $0.name = String(t.prefix(14)) }
    }
}

/// A continuous colour range: the gradient itself is the track, a big knob sits on the pick.
struct RangeSlider: View {
    let value: Double
    let stops: [Color]
    let label: String
    var dimmed = false
    let changed: (Double, Bool) -> Void
    @State private var drag: Double?
    var body: some View {
        GeometryReader { g in
            let w = max(1, g.size.width - 34), x = (drag ?? value) * w
            ZStack(alignment: .leading) {
                Capsule().fill(LinearGradient(colors: stops, startPoint: .leading, endPoint: .trailing))
                    .frame(height: 22).padding(.horizontal, 17).opacity(dimmed ? 0.55 : 1)
                    .overlay(Capsule().strokeBorder(.white.opacity(0.35), lineWidth: 1).padding(.horizontal, 17))
                Circle().fill(.white).frame(width: 34, height: 34).shadow(color: .black.opacity(0.35), radius: 3, y: 2)
                    .overlay(Circle().fill(color(at: drag ?? value)).padding(6)).offset(x: x)
                    .opacity(dimmed && drag == nil ? 0.6 : 1)
            }
            .frame(maxHeight: .infinity)
            .contentShape(Rectangle())
            .gesture(DragGesture(minimumDistance: 0)
                .onChanged { v in let t = min(1, max(0, (v.location.x - 17) / w)); drag = t; changed(t, false) }
                .onEnded { v in let t = min(1, max(0, (v.location.x - 17) / w)); drag = nil; changed(t, true) })
        }
        .frame(height: 38)
        .accessibilityElement().accessibilityLabel(label).accessibilityValue("\(Int((value * 100).rounded())) percent")
        .accessibilityAdjustableAction { dir in changed(min(1, max(0, value + (dir == .increment ? 0.05 : -0.05))), true) }
    }
    private func color(at t: Double) -> Color {
        guard stops.count > 1 else { return stops.first ?? .white }
        let x = min(1, max(0, t)) * Double(stops.count - 1); let i = min(stops.count - 2, Int(x))
        return x - Double(i) < 0.5 ? stops[i] : stops[i + 1]
    }
}

/// Head-shot thumbnails of the real locker hero for the style cards (rendered offscreen once per look).
@MainActor enum LockerThumbs {
    private static var cache: [String: UIImage] = [:]
    static func image(_ p: Player, haircut: Int, headwear: Int) -> UIImage? {
        let key = "\(haircut)-\(headwear)-\(p.hairHex)-\(p.skinHex)-\(p.standardFemale)"
        if let c = cache[key] { return c }
        var q = p; q.haircut = haircut; q.hairStyle = headwear
        let c = CharacterModelPreview.Coordinator(); c.update(q)
        c.framing = .head; c.applyFraming()
        c.scene.rootNode.childNode(withName: "idleBall", recursively: true)?.removeFromParentNode()
        if let cam = c.scene.rootNode.childNodes.first(where: { $0.camera != nil }) {
            cam.position = SCNVector3(0.5, 1.16, 1.2); cam.look(at: SCNVector3(0, 1.1, 0))   // 3/4 head shot: hair front, side and back read
        }
        let r = SCNRenderer(device: nil, options: nil); r.scene = c.scene
        r.pointOfView = c.scene.rootNode.childNodes.first(where: { $0.camera != nil })
        let img = r.snapshot(atTime: 0, with: CGSize(width: 220, height: 220), antialiasingMode: .multisampling4X)
        if cache.count > 60 { cache.removeAll() }
        cache[key] = img
        return img
    }
}
