import SwiftUI
import SceneKit

/// The locker. Item-first, like a cosmetics locker: it opens on **Gear** — a shelf of big tiles per sport and slot, one tap
/// equips and the character changes at once — and keeps colour work on its own **Customize** tab as swatch rows, with the full
/// hue / shade gradients one tap away under "Custom". The camera reframes to the part being changed (body, racket hand, feet).
/// The phone layout is touch-first; the TV layout is the same screen driven by the click remote (`TennisMenu.lockerRows()`).
struct IslandLockerScreen: View {
    let menu: TennisMenu
    let compact: Bool
    var lobbyPanel = false
    @State private var editingName = false
    @State private var nameDraft = ""

    private var player: Player { menu.player ?? Player(name: "Player 1", colorIndex: 0) }
    private var k: CGFloat { compact ? 1 : 1.3 }
    private var framing: PreviewFraming {
        guard menu.lockerTab == .gear else { return .body }
        switch menu.lockerSlot { case .skin: return .body; case .racket, .club: return .racket; case .shoes: return .feet }
    }

    var body: some View {
        Group {
        if lobbyPanel { panel } else {
        GeometryReader { geo in
            ZStack {
                IslandBackdrop(room: .locker)
                if compact {
                    LinearGradient(colors: [IslandUI.paper.opacity(0.45), .clear], startPoint: .top, endPoint: UnitPoint(x: 0.5, y: 0.3)).ignoresSafeArea()
                } else {
                    LinearGradient(colors: [IslandUI.paper.opacity(0.62), .clear], startPoint: .leading, endPoint: UnitPoint(x: 0.56, y: 0.5)).ignoresSafeArea()
                }
                if compact { phone(geo.size) } else { tv(geo.size) }
            }
        }
        } }
        .preferredColorScheme(.light)
        .onAppear { nameDraft = player.name }
    }

    // MARK: layouts

    private func phone(_ size: CGSize) -> some View {
        let panelHeight = min(500, size.height * 0.56)
        return VStack(spacing: 0) {
            topBar.padding(.horizontal, 16).padding(.top, 10)
            stage.frame(maxHeight: .infinity)
            panel.frame(height: panelHeight)
        }
    }

    private func tv(_ size: CGSize) -> some View {
        ZStack(alignment: .topLeading) {
            HStack(alignment: .top, spacing: 0) {
                panel.frame(width: 640, height: 500).padding(.leading, 48).padding(.top, 118)
                stage.frame(maxWidth: .infinity, maxHeight: .infinity).padding(.top, 20).padding(.bottom, 14)
            }
            HStack(alignment: .center, spacing: 28) {
                Text("Locker").font(IslandUI.font(42, bold: true)).foregroundStyle(IslandUI.navy)
                if menu.lockerTab == .gear { sportToggle }
            }.padding(.leading, 56).padding(.top, 36)
            VStack { Spacer(); HStack { IslandHintBar(items: IslandHintBar.move); Spacer() }.padding(.leading, 48).padding(.bottom, 28) }
        }
    }

    private var topBar: some View {
        HStack(spacing: 10) {
            roundButton("chevron.left", id: "lk-done", label: "Back")
            Spacer()
            if menu.lockerTab == .gear { sportToggle }
            Spacer()
            roundButton("dice.fill", id: "lk-shuffle", label: "Shuffle colours")
        }
    }

    private func roundButton(_ icon: String, id: String, label: String) -> some View {
        Button { menu.tap(id) } label: {
            Image(systemName: icon).font(.system(size: 17, weight: .bold)).foregroundStyle(IslandUI.navy)
                .frame(width: 42, height: 42).background(IslandUI.paper.opacity(0.94), in: Circle())
                .overlay(Circle().strokeBorder(IslandUI.navy, lineWidth: menu.isFocused(id) ? 3 : 0))
                .shadow(color: .black.opacity(0.2), radius: 3, y: 2)
        }.buttonStyle(.plain).accessibilityLabel(label)
    }

    private var sportToggle: some View {
        HStack(spacing: 0) {
            ForEach(LockerCatalog.sports, id: \.self) { sport in
                let on = menu.lockerSport == sport, id = "lk-sport-\(sport.rawValue)"
                Button { menu.tap(id) } label: {
                    Text(sport.title.capitalized).font(IslandUI.font(16 * k, bold: true))
                        .foregroundStyle(on ? .white : IslandUI.muted)
                        .padding(.horizontal, 20 * k).padding(.vertical, 8 * k)
                        .background(on ? IslandUI.navy : .clear, in: Capsule())
                        .overlay(Capsule().strokeBorder(IslandUI.lime, lineWidth: menu.isFocused(id) ? 3.5 : 0))
                }.buttonStyle(.plain).accessibilityLabel("\(sport.title.capitalized) gear").accessibilityAddTraits(on ? .isSelected : [])
            }
        }
        .padding(4).background(IslandUI.paper.opacity(0.94), in: Capsule()).shadow(color: .black.opacity(0.2), radius: 3, y: 2)
    }

    // MARK: stage

    private var stage: some View {
        ZStack(alignment: .bottom) {
            CharacterModelPreview(player: player, cameraDistance: 2.7, framing: framing, outfitSport: menu.lockerSport)
                .padding(.bottom, 18)
                .accessibilityLabel("\(player.name), your character")
            if compact { nameTag.padding(.bottom, 6) }
        }
    }

    private var nameTag: some View {
        Group {
            if editingName {
                TextField("Your name", text: $nameDraft).font(IslandUI.font(17, bold: true)).multilineTextAlignment(.center)
                    .submitLabel(.done).onSubmit { rename(nameDraft); editingName = false }.frame(width: 170)
            } else {
                Button { nameDraft = player.name; editingName = true } label: {
                    HStack(spacing: 6) { Text(player.name).font(IslandUI.font(16, bold: true)); Image(systemName: "pencil").font(.system(size: 11, weight: .bold)) }
                }.buttonStyle(.plain)
            }
        }
        .foregroundStyle(IslandUI.navy).padding(.horizontal, 16).padding(.vertical, 6)
        .background(IslandUI.lime, in: Capsule()).shadow(color: IslandUI.navy.opacity(0.25), radius: 0, y: 2)
        .accessibilityLabel("Name, \(player.name). Tap to change.")
    }

    private func rename(_ text: String) {
        let t = text.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !t.isEmpty else { return }
        menu.lockerEdit { $0.name = String(t.prefix(14)) }
    }

    // MARK: panel

    private var panel: some View {
        VStack(alignment: .leading, spacing: 12 * k) {
            if let range = menu.lockerRange { rangePanel(range) }
            else {
                tabs
                if lobbyPanel && menu.lockerTab == .gear { sportToggle }
                Group {
                    switch menu.lockerTab {
                    case .gear: gear
                    case .customize: customize
                    case .emotes: emotes
                    }
                }
                .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .top)
                footer
            }
        }
        .padding(.horizontal, 18 * k).padding(.top, 16 * k).padding(.bottom, compact ? 26 : 20)
        .frame(maxWidth: .infinity)
        .background(IslandUI.paper.opacity(0.96), in: RoundedRectangle(cornerRadius: compact ? 32 : 26, style: .continuous))
        .shadow(color: IslandUI.navy.opacity(0.22), radius: 18, y: compact ? -8 : 8)
    }

    private var tabs: some View {
        HStack(spacing: 6) {
            ForEach([(TennisMenu.LockerTab.gear, "Gear", "lk-tab-gear"), (.customize, "Customize", "lk-tab-customize"), (.emotes, "Emotes", "lk-tab-emotes")], id: \.2) { tab, title, id in
                let on = menu.lockerTab == tab
                Button { menu.tap(id) } label: {
                    Text(title).font(IslandUI.font(17 * k, bold: true)).foregroundStyle(IslandUI.navy)
                        .frame(maxWidth: .infinity).padding(.vertical, 11 * k)
                        .background(on ? IslandUI.lime : .clear, in: RoundedRectangle(cornerRadius: 14, style: .continuous))
                        .overlay(RoundedRectangle(cornerRadius: 14, style: .continuous).strokeBorder(IslandUI.navy, lineWidth: menu.isFocused(id) ? 3.5 : 0))
                }.buttonStyle(.plain).accessibilityAddTraits(on ? .isSelected : [])
            }
        }
        .padding(5).background(IslandUI.navy.opacity(0.07), in: RoundedRectangle(cornerRadius: 18, style: .continuous))
    }

    private var footer: some View {
        HStack(spacing: 12) {
            if !compact { footerButton("Shuffle", id: "lk-shuffle", primary: false) }
            if menu.lockerDirty {
                if compact {
                    Button { menu.tap("lk-revert") } label: {
                        Text("Revert").font(IslandUI.font(16, bold: true)).foregroundStyle(IslandUI.muted).padding(.horizontal, 6)
                    }.buttonStyle(.plain)
                } else { footerButton("Revert", id: "lk-revert", primary: false) }
            }
            footerButton("Done", id: "lk-done", primary: true)
        }
    }

    private var emotes: some View {
        VStack(alignment: .leading, spacing: 10 * k) {
            Text("Pick a slot, then choose an emote.")
                .font(IslandUI.font(14 * k, bold: true)).foregroundStyle(IslandUI.muted)
            HStack(spacing: 8) {
                ForEach(0..<3, id: \.self) { slot in
                    let id = "lk-emote-slot-\(slot)", selected = menu.lockerEmoteSlot == slot
                    Button { menu.tap(id) } label: {
                        VStack(spacing: 4) {
                            Text("SLOT \(slot + 1)").font(IslandUI.font(10 * k, bold: true))
                            Text(EmoteCatalog.name(player.equippedEmotes[slot])).font(IslandUI.font(14 * k, bold: true))
                        }
                        .frame(maxWidth: .infinity, minHeight: 50 * k)
                        .background(selected ? IslandUI.lime : .white, in: RoundedRectangle(cornerRadius: 12))
                        .overlay(RoundedRectangle(cornerRadius: 12).strokeBorder(IslandUI.navy, lineWidth: menu.isFocused(id) ? 3 : selected ? 1.5 : 0))
                    }.buttonStyle(.plain).accessibilityIdentifier(id).accessibilityAddTraits(selected ? .isSelected : [])
                }
            }
            ScrollView {
                LazyVGrid(columns: Array(repeating: GridItem(.flexible(), spacing: 8), count: 3), spacing: 8) {
                    ForEach(EmoteCatalog.ids, id: \.self) { emote in
                        emoteTile(emote)
                    }
                }
            }
            Text("Tap one on your controller for your intro or after you score.")
                .font(IslandUI.font(12 * k, bold: false)).foregroundStyle(IslandUI.muted)
        }.foregroundStyle(IslandUI.navy)
    }

    private func emoteTile(_ emote: String) -> some View {
        let id = "lk-emote-\(emote)", slot = player.equippedEmotes.firstIndex(of: emote)
        return Button { menu.tap(id) } label: {
            Group {
                if compact {
                    VStack(spacing: 4) { emoteImage(emote); emoteDetails(emote, slot: slot) }
                } else {
                    HStack(spacing: 2) { emoteImage(emote); emoteDetails(emote, slot: slot) }.padding(.horizontal, 4)
                }
            }
            .frame(maxWidth: .infinity).padding(.vertical, 6)
            .background(slot != nil ? IslandUI.lime.opacity(0.3) : .white, in: RoundedRectangle(cornerRadius: 14))
            .overlay(RoundedRectangle(cornerRadius: 14).strokeBorder(IslandUI.navy, lineWidth: menu.isFocused(id) ? 3 : 0))
        }.buttonStyle(.plain).accessibilityIdentifier(id)
            .accessibilityLabel("\(EmoteCatalog.name(emote)), \(slot.map { "equipped in slot \($0 + 1)" } ?? "not equipped"). Equip in slot \(menu.lockerEmoteSlot + 1).")
    }

    private func emoteImage(_ emote: String) -> some View {
        Group {
            if let image = LobbyEmoteThumbs.image(emote, player: player) {
                Image(uiImage: image).resizable().scaledToFit()
            } else { Image(systemName: "figure.dance").font(.system(size: 32)) }
        }.frame(height: compact ? 54 : 56)
    }

    private func emoteDetails(_ emote: String, slot: Int?) -> some View {
        VStack(spacing: 4) {
            Text(EmoteCatalog.name(emote)).font(IslandUI.font(13 * k, bold: true)).lineLimit(1).minimumScaleFactor(0.75)
            Text(slot.map { "Slot \($0 + 1)" } ?? "Equip").font(IslandUI.font(10 * k, bold: true)).foregroundStyle(IslandUI.muted)
        }
    }

    private func footerButton(_ title: String, id: String, primary: Bool) -> some View {
        Button { menu.tap(id) } label: {
            Text(title).font(IslandUI.font(compact ? 18 : 22, bold: true)).foregroundStyle(IslandUI.navy)
                .frame(maxWidth: .infinity, minHeight: compact ? 52 : 58)
                .background(primary ? IslandUI.lime : .white, in: Capsule())
                .overlay(Capsule().strokeBorder(IslandUI.navy, lineWidth: menu.isFocused(id) ? 4 : primary ? 2 : 0))
                .shadow(color: IslandUI.navy.opacity(0.15), radius: 3, y: 2)
        }.buttonStyle(.plain).accessibilityLabel(title)
    }

    // MARK: gear

    private var gear: some View {
        VStack(alignment: .leading, spacing: 12 * k) {
            HStack(spacing: 8) {
                ForEach(LockerCatalog.slots(for: menu.lockerSport)) { slot in
                    let on = menu.lockerSlot == slot, id = "lk-slot-\(slot.rawValue)"
                    Button { menu.tap(id) } label: {
                        HStack(spacing: 6) {
                            Image(systemName: slot.icon).font(.system(size: 14 * k, weight: .bold))
                            Text(slot.title(for: menu.lockerSport)).font(IslandUI.font(15 * k, bold: true))
                        }
                        .foregroundStyle(on ? .white : IslandUI.navy).frame(maxWidth: .infinity).padding(.vertical, 10 * k)
                        .background(on ? IslandUI.navy : .white, in: RoundedRectangle(cornerRadius: 14, style: .continuous))
                        .overlay(RoundedRectangle(cornerRadius: 14, style: .continuous).strokeBorder(IslandUI.lime, lineWidth: menu.isFocused(id) ? 4 : 0))
                        .shadow(color: IslandUI.navy.opacity(0.1), radius: 2, y: 1)
                    }.buttonStyle(.plain).accessibilityAddTraits(on ? .isSelected : [])
                }
            }
            shelf
            if let colour = menu.lockerItemColourSlot { colourRow(colour) }
            else if menu.lockerSlot == .skin {
                Button { menu.tap("lk-tab-customize") } label: {
                    HStack(spacing: 6) { Text("Shirt, \(menu.lockerBottomName.lowercased()) and skin colours").font(IslandUI.font(14 * k, bold: true)); Text("Customize ›").font(IslandUI.font(14 * k, bold: true)).foregroundStyle(IslandUI.muted) }
                        .foregroundStyle(IslandUI.navy)
                }.buttonStyle(.plain)
            }
        }
    }

    private var shelf: some View {
        let items = menu.lockerShelf, worn = menu.lockerEquipped
        return ScrollView(.horizontal, showsIndicators: false) {
            HStack(spacing: 10 * k) {
                ForEach(items) { item in
                    let id = "lk-item-\(item.id)"
                    LockerTile(item: item, player: player, sport: menu.lockerSport, selected: worn?.id == item.id, focused: menu.isFocused(id), scale: k)
                        .onTapGesture { menu.tap(id) }
                }
                if items.count < 4 {
                    Text("More gear\ncoming soon").font(IslandUI.font(13 * k, bold: false)).multilineTextAlignment(.center).foregroundStyle(IslandUI.muted)
                        .frame(width: 112 * k, height: 134 * k)
                        .overlay(RoundedRectangle(cornerRadius: 18, style: .continuous).strokeBorder(IslandUI.navy.opacity(0.25), style: StrokeStyle(lineWidth: 2, dash: [6, 5])))
                        .accessibilityHidden(true)
                }
            }.padding(.horizontal, 8).padding(.vertical, 8)
        }.padding(.horizontal, -8)
    }

    private func colourRow(_ slot: String) -> some View {
        VStack(alignment: .leading, spacing: 8 * k) {
            HStack {
                Text("Colour").font(IslandUI.font(15 * k, bold: true))
                Spacer()
                Button { menu.tap("lk-colour") } label: { Text("Custom ›").font(IslandUI.font(13 * k, bold: true)).foregroundStyle(IslandUI.muted) }.buttonStyle(.plain)
            }
            IslandSwatchRow(colours: TennisMenu.lockerSwatches.map { Color(hex: $0.hex) }, selected: menu.lockerSwatchIndex(slot), kit: true, size: 27 * k) { menu.lockerPick(slot, index: $0) }
        }
        .foregroundStyle(IslandUI.navy).padding(.horizontal, 12).padding(.vertical, 9)
        .islandFocus(menu.isFocused("lk-colour"), radius: 14, base: .clear, ringWidth: 3)
    }

    // MARK: customize

    private var customize: some View {
        ScrollView(showsIndicators: false) {
            VStack(spacing: 8 * k) {
                labelledRow("Player", id: "lk-body") {
                    IslandSegments(titles: ["Boy", "Girl"], selected: player.standardFemale ? 1 : 0, compact: compact) { i in menu.lockerEdit { $0.standardFemale = i == 1 } }
                }
                labelledRow("Plays", id: "lk-hand") {
                    IslandSegments(titles: ["Right", "Left"], selected: player.handedness == .left ? 1 : 0, compact: compact) { i in menu.lockerEdit { $0.handedness = i == 1 ? .left : .right } }
                }
                labelledRow("Skin", id: "lk-skin", custom: "skin") {
                    IslandSwatchRow(colours: LockerColor.skinPresets.map { Color(hex: LockerColor.ramp(LockerColor.skinStops, $0)) }, selected: menu.lockerSkinIndex(), size: 28 * k) { menu.lockerPickSkin($0) }
                }
                labelledRow("Shirt", id: "lk-shirt", custom: "shirt") {
                    IslandSwatchRow(colours: TennisMenu.lockerSwatches.map { Color(hex: $0.hex) }, selected: menu.lockerSwatchIndex("shirt"), kit: true, size: 23 * k) { menu.lockerPick("shirt", index: $0) }
                }
                labelledRow(menu.lockerBottomName, id: "lk-shorts", custom: "shorts") {
                    IslandSwatchRow(colours: TennisMenu.lockerSwatches.map { Color(hex: $0.hex) }, selected: menu.lockerSwatchIndex("shorts"), kit: true, size: 23 * k) { menu.lockerPick("shorts", index: $0) }
                }
            }.padding(.horizontal, 3).padding(.vertical, 4)
        }
    }

    private func labelledRow<C: View>(_ title: String, id: String, custom: String? = nil, @ViewBuilder _ content: () -> C) -> some View {
        HStack(spacing: 8 * k) {
            Text(title).font(IslandUI.font(16 * k, bold: true)).lineLimit(1).minimumScaleFactor(0.8).frame(width: 54 * k, alignment: .leading)
            content()
            if let custom {
                Button { menu.lockerOpenRangeForTouch(custom) } label: { Image(systemName: "slider.horizontal.3").font(.system(size: 15 * k, weight: .bold)).foregroundStyle(IslandUI.muted).frame(width: 28 * k, height: 28 * k) }
                    .buttonStyle(.plain).accessibilityLabel("Custom \(title.lowercased()) colour")
            }
        }
        .foregroundStyle(IslandUI.navy).padding(.horizontal, 12 * k).padding(.vertical, 9 * k)
        .islandFocus(menu.isFocused(id), radius: 15)
    }

    // MARK: full range

    private func rangePanel(_ slot: String) -> some View {
        let p = player
        let title = slot == "skin" ? "Skin tone" : slot == "shirt" ? "Shirt" : slot == "shorts" ? menu.lockerBottomName : slot == "accent" ? "Shoes" : (menu.lockerSlot == .club ? "Club" : "Racket")
        return VStack(alignment: .leading, spacing: 14 * k) {
            HStack {
                Text("\(title) · custom colour").font(IslandUI.font(20 * k, bold: true))
                Spacer()
                Circle().fill(slot == "skin" ? Color(hex: p.skinHex) : (p.outfitColor(slot) ?? .white.opacity(0.4))).frame(width: 26, height: 26).overlay(Circle().strokeBorder(IslandUI.navy.opacity(0.3), lineWidth: 2))
            }
            if slot == "skin" {
                rangeRow("Tone", id: "lk-range-skin") {
                    RangeSlider(value: p.skinT, stops: LockerColor.skinStops.map { Color(hex: $0) }, label: "Skin tone") { t, done in menu.lockerEdit(save: done) { $0.setSkin(t) } }
                }
            } else {
                let (h, sh) = p.hueShade(slot)
                rangeRow("Hue", id: "lk-range-hue") {
                    RangeSlider(value: h, stops: LockerColor.rainbow, label: "Hue") { v, done in menu.lockerEdit(save: done) { $0.setOutfit(slot, hue: v, shade: sh) } }
                }
                rangeRow("Shade", id: "lk-range-shade") {
                    RangeSlider(value: (sh + 1) / 2, stops: [Color(hex: LockerColor.hueShade(h, -1)), Color(hex: LockerColor.hueShade(h, 0)), Color(hex: LockerColor.hueShade(h, 1))], label: "Shade") { v, done in
                        menu.lockerEdit(save: done) { $0.setOutfit(slot, hue: h, shade: v * 2 - 1) }
                    }
                }
            }
            Spacer(minLength: 0)
            footerButton("Done", id: "lk-range-close", primary: true)
        }
    }

    private func rangeRow<C: View>(_ title: String, id: String, @ViewBuilder _ content: () -> C) -> some View {
        HStack(spacing: 10) {
            Text(title).font(IslandUI.font(16 * k, bold: true)).frame(width: 56 * k, alignment: .leading)
            content()
        }
        .foregroundStyle(IslandUI.navy).padding(.horizontal, 12).padding(.vertical, 6)
        .islandFocus(menu.isFocused(id), radius: 15)
    }
}

/// One item on the shelf: a thumbnail of the real hero wearing / holding it, its name, a check when equipped.
struct LockerTile: View {
    let item: LockerItem
    let player: Player
    let sport: Sport
    let selected: Bool
    let focused: Bool
    var scale: CGFloat = 1
    @State private var image: UIImage?
    private var key: String { LockerThumbs.key(player, slot: item.slot, item: item) }
    var body: some View {
        VStack(spacing: 0) {
            ZStack {
                LinearGradient(colors: [Color(hex: "F4F0E6"), Color(hex: "E7E0CF")], startPoint: .top, endPoint: .bottom)
                if let image { Image(uiImage: image).resizable().scaledToFill() }
            }.frame(height: 98 * scale).clipped()
            Text(item.name).font(IslandUI.font(14 * scale, bold: true)).foregroundStyle(IslandUI.navy).frame(maxWidth: .infinity, minHeight: 36 * scale)
        }
        .frame(width: 112 * scale, height: 134 * scale)
        .background(.white, in: RoundedRectangle(cornerRadius: 18, style: .continuous))
        .clipShape(RoundedRectangle(cornerRadius: 18, style: .continuous))
        .overlay(alignment: .topTrailing) {
            if selected {
                Image(systemName: "checkmark").font(.system(size: 12 * scale, weight: .black)).foregroundStyle(IslandUI.navy)
                    .frame(width: 24 * scale, height: 24 * scale).background(IslandUI.lime, in: Circle()).overlay(Circle().strokeBorder(IslandUI.navy, lineWidth: 2)).padding(7)
            }
        }
        .overlay(RoundedRectangle(cornerRadius: 18, style: .continuous).strokeBorder(IslandUI.navy, lineWidth: selected ? 3.5 : 0))
        .overlay(RoundedRectangle(cornerRadius: 22, style: .continuous).strokeBorder(IslandUI.lime, lineWidth: focused ? 5 : 0).padding(-5))
        .shadow(color: IslandUI.navy.opacity(0.14), radius: 4, y: 2)
        .task(id: key) { image = LockerThumbs.image(player, slot: item.slot, item: item) }
        .accessibilityElement(children: .ignore).accessibilityLabel("\(item.name) \(item.slot.title(for: sport))")
        .accessibilityAddTraits(selected ? [.isButton, .isSelected] : .isButton)
    }
}

/// Offscreen renders of the real hero for the shelf tiles (cached per look).
@MainActor enum LockerThumbs {
    private static var cache: [String: UIImage] = [:]
    static func key(_ p: Player, slot: LockerSlot, item: LockerItem) -> String {
        "\(p.standardFemale)|\(p.skinHex)|\(p.outfitHex("shirt") ?? "k")|\(p.outfitHex("shorts") ?? "k")|\(p.outfitHex("accent") ?? "k")|\(p.outfitHex("racket") ?? "k")|\(slot.rawValue)|\(item.id)"
    }
    static func image(_ p: Player, slot: LockerSlot, item: LockerItem) -> UIImage? {
        let k = key(p, slot: slot, item: item)
        if let c = cache[k] { return c }
        let c = CharacterModelPreview.Coordinator(cameraDistance: 3.1); c.update(p, sport: item.sports == [.golf] ? .golf : .tennis)
        c.framing = slot == .skin ? .body : slot == .shoes ? .feet : .racket; c.applyFraming()
        c.scene.rootNode.childNode(withName: "idleBall", recursively: true)?.removeFromParentNode()
        let r = SCNRenderer(device: nil, options: nil); r.scene = c.scene; r.pointOfView = c.camera
        let img = r.snapshot(atTime: 0, with: CGSize(width: 240, height: 240), antialiasingMode: .multisampling4X)
        if cache.count > 80 { cache.removeAll() }
        cache[k] = img
        return img
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
