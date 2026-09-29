import SwiftUI

/// The courts a tennis match can be played on. Raw values are Unity's `TennisVenue` keys (the
/// `venue` field of the start message); the picture is MenuArt/map-<key>.jpg, an in-game shot.
enum TennisVenueChoice: String, CaseIterable, Identifiable {
    case resort, skyscraper, volcano
    var id: String { rawValue }
    var title: String {
        switch self { case .resort: "Tropical Resort"; case .skyscraper: "Sky Tower"; case .volcano: "Magma Crater" }
    }
    var tagline: String {
        switch self {
        case .resort: "The original: sunset over the bay, stands full of fans"
        case .skyscraper: "A rooftop above the clouds. Hit it wide and the ball falls forever"
        case .volcano: "A court floating over a lava lake. Don't miss long"
        }
    }
    var badge: String {
        switch self { case .resort: "CLASSIC"; case .skyscraper: "OPEN EDGE"; case .volcano: "OVER LAVA" }
    }
    var tint: Color {
        switch self { case .resort: Club.sky; case .skyscraper: Club.violet; case .volcano: Club.coral }
    }
    /// The image file, without extension.
    var art: String { "map-\(rawValue)" }
}

/// Shown after choosing a match and before it loads: pick the court. On the TV the remote moves
/// between the cards; on the phone you tap one. The choice is remembered for next time.
struct ClubMapScreen: View {
    let menu: TennisMenu
    let compact: Bool
    var body: some View {
        ClubScreen(scene: "court", breadcrumb: ["Tennis", "Choose your court"], compact: compact, tint: 0.4) {
            VStack(spacing: 16) {
                if compact {
                    ScrollView {
                        VStack(spacing: 14) {
                            ForEach(Array(TennisVenueChoice.allCases.enumerated()), id: \.offset) { i, v in card(v).frame(height: 210).clubEntrance(i) }
                        }.padding(.vertical, 6)
                    }
                } else {
                    HStack(spacing: 18) {
                        ForEach(Array(TennisVenueChoice.allCases.enumerated()), id: \.offset) { i, v in card(v).clubEntrance(i) }
                    }.frame(height: 440)
                }
                HStack {
                    ClubButton(title: "Back", icon: "", focused: menu.isFocused("back"), style: .quiet, size: 20) { menu.tap("back") }
                    Spacer()
                }
            }
        }
    }

    private func card(_ venue: TennisVenueChoice) -> some View {
        let id = "map-\(venue.rawValue)"
        let focused = menu.isFocused(id)
        let last = SportsSession.shared.tennisVenue == venue.rawValue
        let shape = RoundedRectangle(cornerRadius: compact ? 26 : 34, style: .continuous)
        return Button { menu.tap(id) } label: {
            ZStack(alignment: .bottomLeading) {
                Color.clear.overlay {
                    if let img = UIImage(named: "\(venue.art).jpg") {
                        Image(uiImage: img).resizable().scaledToFill()
                    } else {
                        LinearGradient(colors: [venue.tint, Club.lagoonDeep], startPoint: .top, endPoint: .bottom)
                    }
                }.clipped()
                LinearGradient(colors: [.clear, Club.lagoonDeep.opacity(0.92)], startPoint: .center, endPoint: .bottom)
                VStack(alignment: .leading, spacing: compact ? 3 : 6) {
                    HStack(spacing: 8) {
                        Text(venue.badge).font(Club.caps(11)).tracking(1.2).foregroundStyle(.white)
                            .padding(.horizontal, 8).padding(.vertical, 3).background(Capsule().fill(venue.tint))
                        if last { Text("LAST PLAYED").font(Club.caps(11)).tracking(1.2).foregroundStyle(Club.ink)
                            .padding(.horizontal, 8).padding(.vertical, 3).background(Capsule().fill(Club.sun)) }
                    }
                    Text(venue.title).font(Club.display(compact ? 34 : 44)).foregroundStyle(.white).lineLimit(1).minimumScaleFactor(0.6)
                        .shadow(color: .black.opacity(0.4), radius: 0, x: 0, y: 3)
                    Text(venue.tagline).font(Club.ui(compact ? 13 : 15, 600)).foregroundStyle(.white.opacity(0.92)).lineLimit(2)
                }
                .padding(compact ? 14 : 20)
            }
            .clipShape(shape)
            .overlay(shape.strokeBorder(venue.tint, lineWidth: 4))
        }
        .buttonStyle(ClubPress())
        .clubFocus(focused, corner: compact ? 26 : 34)
        .accessibilityLabel("\(venue.title). \(venue.tagline)")
        .accessibilityIdentifier("tennisMap_\(venue.rawValue)")
    }
}
