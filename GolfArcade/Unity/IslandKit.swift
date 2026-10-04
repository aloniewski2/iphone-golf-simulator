import SwiftUI

// Shared pieces of the Island menu system (IslandUI tokens live in ClubDesign.swift). Every focusable thing uses the same
// focus look — a lime fill with a navy ring — so it reads from the couch on a TV and on a phone alike.

extension IslandUI {
    static let ring = navy
    static let dark = Color(hex: "0A1424")
    static let coral = Color(hex: "FF6B4A")
    /// Row / chip surface that sits on the paper.
    static let card = Color.white
}

/// The focus look: lime fill + navy ring (+ a soft lift). Unfocused: the base fill.
struct IslandFocusStyle: ViewModifier {
    let focused: Bool
    var radius: CGFloat = 16
    var base: Color = .white
    var ringWidth: CGFloat = 3.5
    func body(content: Content) -> some View {
        content
            .background(focused ? IslandUI.lime : base, in: RoundedRectangle(cornerRadius: radius, style: .continuous))
            .overlay(RoundedRectangle(cornerRadius: radius, style: .continuous).strokeBorder(IslandUI.navy, lineWidth: focused ? ringWidth : 0))
            .shadow(color: IslandUI.navy.opacity(focused ? 0.22 : 0.08), radius: focused ? 6 : 3, y: focused ? 4 : 2)
            .accessibilityAddTraits(focused ? .isSelected : [])
    }
}
extension View {
    func islandFocus(_ focused: Bool, radius: CGFloat = 16, base: Color = .white, ringWidth: CGFloat = 3.5) -> some View {
        modifier(IslandFocusStyle(focused: focused, radius: radius, base: base, ringWidth: ringWidth))
    }
}

/// The remote's legend along the bottom of the TV.
struct IslandHintBar: View {
    let items: [(key: String, label: String)]
    var compact = false
    static let move: [(key: String, label: String)] = [("▲ ▼ ◀ ▶", "Move"), ("A", "Select"), ("B", "Back")]
    var body: some View {
        HStack(spacing: compact ? 14 : 26) {
            ForEach(Array(items.enumerated()), id: \.offset) { _, item in
                HStack(spacing: 7) { Text(item.key).foregroundStyle(IslandUI.lime); Text(item.label) }
            }
        }
        .font(IslandUI.font(compact ? 13 : 16, bold: true)).foregroundStyle(.white)
        .padding(.horizontal, compact ? 16 : 26).padding(.vertical, compact ? 7 : 9)
        .background(IslandUI.navy.opacity(0.84), in: Capsule())
        .accessibilityHidden(true)
    }
}

/// One colour dot: ringed when picked, bigger ring when the row has focus.
struct IslandSwatch: View {
    let colour: Color
    var selected = false
    var size: CGFloat = 32
    var body: some View {
        Circle().fill(colour).frame(width: size, height: size)
            .overlay(Circle().strokeBorder(IslandUI.navy.opacity(selected ? 1 : 0.16), lineWidth: selected ? 3 : 2.5))
            .overlay(Circle().strokeBorder(.white, lineWidth: selected ? 2 : 0).padding(selected ? 3 : 0))
    }
}

/// A line of swatches that can be tapped; `kit` adds the "Kit" chip (the kit's own colour) at the front.
struct IslandSwatchRow: View {
    let colours: [Color]
    let selected: Int          // -1 = kit colour, -2 = custom, else index
    var kit = false
    var size: CGFloat = 32
    let pick: (Int) -> Void
    var body: some View {
        HStack(spacing: 0) {
            if kit {
                Button { pick(-1) } label: {
                    Text("Kit").font(IslandUI.font(size * 0.34, bold: true)).foregroundStyle(IslandUI.navy)
                        .frame(width: size, height: size)
                        .background(.white, in: Circle())
                        .overlay(Circle().strokeBorder(IslandUI.navy.opacity(selected == -1 ? 1 : 0.16), lineWidth: selected == -1 ? 3 : 2.5))
                }.buttonStyle(.plain).accessibilityLabel("Kit colour")
                Spacer(minLength: 2)
            }
            ForEach(colours.indices, id: \.self) { i in
                Button { pick(i) } label: { IslandSwatch(colour: colours[i], selected: selected == i, size: size) }
                    .buttonStyle(.plain)
                if i < colours.count - 1 { Spacer(minLength: 2) }
            }
        }
    }
}

/// A two- or three-way choice drawn as a segmented pill (display + touch).
struct IslandSegments: View {
    let titles: [String]
    let selected: Int
    var compact = false
    let pick: (Int) -> Void
    var body: some View {
        HStack(spacing: 4) {
            ForEach(titles.indices, id: \.self) { i in
                Button { pick(i) } label: {
                    Text(titles[i]).font(IslandUI.font(compact ? 14 : 18, bold: true)).foregroundStyle(selected == i ? .white : IslandUI.navy)
                        .lineLimit(1).minimumScaleFactor(0.7)
                        .frame(maxWidth: .infinity).padding(.vertical, compact ? 9 : 12)
                        .background(selected == i ? IslandUI.navy : .clear, in: RoundedRectangle(cornerRadius: 11, style: .continuous))
                }.buttonStyle(.plain)
            }
        }
        .padding(4).background(.white.opacity(0.85), in: RoundedRectangle(cornerRadius: 15, style: .continuous))
    }
}

/// A switch drawn like the rest of Island (the system Toggle is blue and tiny on a TV).
struct IslandSwitch: View {
    let on: Bool
    var compact = false
    var body: some View {
        let w: CGFloat = compact ? 56 : 80, h: CGFloat = compact ? 32 : 44
        ZStack(alignment: on ? .trailing : .leading) {
            Capsule().fill(on ? IslandUI.lime : IslandUI.navy.opacity(0.18)).frame(width: w, height: h)
                .overlay(Capsule().strokeBorder(IslandUI.navy.opacity(0.55), lineWidth: 2.5))
            Circle().fill(.white).frame(width: h - 8, height: h - 8).padding(4).shadow(color: .black.opacity(0.25), radius: 1.5, y: 1)
        }
        .animation(.easeOut(duration: 0.15), value: on)
        .accessibilityHidden(true)
    }
}

/// A short message that fades on its own: refusals, "Equipped", "Coaching tips will show again".
struct IslandNotice: View {
    let text: String
    var compact = false
    var body: some View {
        if !text.isEmpty {
            Text(text).font(IslandUI.font(compact ? 14 : 18, bold: true)).foregroundStyle(.white)
                .padding(.horizontal, 18).padding(.vertical, compact ? 9 : 12)
                .background(IslandUI.navy, in: Capsule())
                .transition(.opacity.combined(with: .move(edge: .bottom)))
                .accessibilityAddTraits(.updatesFrequently)
        }
    }
}

/// The menu's current notice, shown as a pill that clears itself after a few seconds.
struct IslandMenuNotice: View {
    let menu: TennisMenu
    var compact = false
    var body: some View {
        IslandNotice(text: menu.notice, compact: compact)
            .task(id: menu.notice) {
                let shown = menu.notice
                guard !shown.isEmpty else { return }
                try? await Task.sleep(for: .seconds(3.5))
                menu.clearNotice(shown)
            }
    }
}
