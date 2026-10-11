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
        case .resort: "A bright tropical bay, lush gardens and stands full of fans"
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



/// Keys match Unity's Course.All(); every listed course has modelled playable holes.
/// The old "postcards" key is retired: MultiplayerLobby.currentGolfVenue maps it to Cliffside.
enum GolfCourseChoice: String, CaseIterable {
    case cliffside, wildisles, magma
    var title: String {
        switch self { case .cliffside: "Cliffside"; case .wildisles: "Wild Isles"; case .magma: "Magma Open" }
    }
    var detail: String {
        switch self {
        case .cliffside: "Cliffs, sea stacks and island carries"
        case .wildisles: "A world tour: pine, ice, mesa, jungle"
        case .magma: "Lava crater holes and volcanic greens"
        }
    }
    var art: String { "map-golf-\(rawValue)" }
    /// The course filmed from the game, the camera circling its holes high up like the old
    /// COURSE screen (Unity: BrollCaptureTests.CaptureCourseOrbits, Tools/course_clips.sh).
    var clip: String { "course-golf-\(rawValue)" }
    var theme: String {
        switch self { case .cliffside: "OCEAN CLIFFS"; case .wildisles: "WILD ISLANDS"; case .magma: "LAVA CRATER" }
    }
    /// Holes and par, as Unity's Course.All() has them.
    var card: String {
        switch self { case .cliffside: "5 HOLES · PAR 19"; case .wildisles: "6 HOLES · PAR 25"; case .magma: "5 HOLES · PAR 20" }
    }
}
struct SportMapChoice: Identifiable {
    let id: String
    let title: String
    let detail: String
    let art: String
    static func choices(for sport: Sport) -> [Self] {
        if sport == .golf {
            return GolfCourseChoice.allCases.map { Self(id: $0.rawValue, title: $0.title, detail: $0.detail, art: $0.art) }
        }
        return TennisVenueChoice.allCases.map { Self(id: $0.rawValue, title: $0.title, detail: $0.tagline, art: $0.art) }
    }
}

/// Golf's course picker, the way the old COURSE screen showed it: the course itself, filmed from the
/// game with the camera circling its holes, filling the screen; translucent arrows step through the
/// four course themes, their names along the foot, and PLAY under the one on show. The remote's
/// left and right move the same way (the map row's focus is the course on show).
struct GolfCourseScreen: View {
    let menu: TennisMenu
    let compact: Bool
    @State private var shown: GolfCourseChoice = GolfCourseChoice(rawValue: SportsSession.shared.golfCourse) ?? .cliffside
    private let all = GolfCourseChoice.allCases

    var body: some View {
        Group { if compact { phone } else { tv } }
            .preferredColorScheme(.dark)
            .onAppear(perform: follow)
            .onChange(of: menu.focused) { _, _ in follow() }
    }

    private func follow() {
        let id = menu.focused
        if id.hasPrefix("map-"), let course = GolfCourseChoice(rawValue: String(id.dropFirst(4))) { shown = course }
    }
    private func show(_ course: GolfCourseChoice) { menu.focus("map-\(course.rawValue)"); shown = course }
    private func step(_ by: Int) {
        let i = all.firstIndex(of: shown) ?? 0
        show(all[(i + by + all.count) % all.count])
    }
    private func play() { menu.tap("map-\(shown.rawValue)") }

    // The TV: the clip fills the screen, the names and buttons over a dark foot.
    private var tv: some View {
        ZStack {
            Color.black.ignoresSafeArea()
            LoopingVideo(clip: shown.clip).ignoresSafeArea().id(shown.rawValue)
            VStack(spacing: 0) {
                LinearGradient(colors: [.black.opacity(0.6), .clear], startPoint: .top, endPoint: .bottom).frame(height: 200)
                Spacer()
                LinearGradient(colors: [.clear, .black.opacity(0.75)], startPoint: .top, endPoint: .bottom).frame(height: 420)
            }.ignoresSafeArea().allowsHitTesting(false)
            VStack(spacing: 22) {
                HStack {
                    IslandWordmark(size: 28).frame(width: 180)
                    Text("CHOOSE YOUR COURSE").font(IslandUI.font(38, bold: true)).foregroundStyle(.white).padding(.leading, 18)
                    Spacer()
                }
                Spacer()
                HStack { arrow(-1, size: 96); Spacer(); arrow(1, size: 96) }
                Spacer()
                title(size: 64)
                HStack(spacing: 14) { ForEach(all, id: \.self) { tab($0, height: 58) } }
                HStack(spacing: 18) {
                    IslandAction(title: "Back", focused: menu.isFocused("back")) { menu.tap("back") }.frame(width: 170)
                    IslandAction(title: "Play \(shown.title)", focused: menu.isFocused("map-\(shown.rawValue)"), primary: true, identifier: "golf-course-play") { play() }
                        .frame(width: 420)
                }
            }.padding(.horizontal, 52).padding(.vertical, 36)
        }
    }

    // The phone: the clip in a wide frame at the top (it is filmed for a TV), the courses below it.
    private var phone: some View {
        ZStack {
            LinearGradient(colors: [Color(hex: "0B2545"), Color(hex: "13315C")], startPoint: .top, endPoint: .bottom).ignoresSafeArea()
            VStack(alignment: .leading, spacing: 16) {
                Text("Choose a course").font(IslandUI.font(30, bold: true)).foregroundStyle(.white)
                ZStack {
                    LoopingVideo(clip: shown.clip).id(shown.rawValue)
                    HStack { arrow(-1, size: 54); Spacer(); arrow(1, size: 54) }.padding(.horizontal, 10)
                }
                .aspectRatio(16 / 9, contentMode: .fit)
                .clipShape(RoundedRectangle(cornerRadius: 18))
                .overlay(RoundedRectangle(cornerRadius: 18).strokeBorder(.white.opacity(0.85), lineWidth: 3))
                title(size: 34)
                ScrollView {
                    VStack(spacing: 10) { ForEach(all, id: \.self) { tab($0, height: 52).frame(maxWidth: .infinity) } }
                }
                IslandAction(title: "Play \(shown.title)", primary: true, compact: true, identifier: "golf-course-play") { play() }
            }.padding(22)
        }
    }

    private func title(size: CGFloat) -> some View {
        VStack(spacing: 6) {
            Text(shown.theme).font(IslandUI.font(size * 0.28, bold: true)).foregroundStyle(IslandUI.navy)
                .padding(.horizontal, 14).padding(.vertical, 5).background(IslandUI.lime, in: Capsule())
            Text(shown.title.uppercased()).font(IslandUI.font(size, bold: true)).foregroundStyle(.white)
                .shadow(color: .black.opacity(0.5), radius: 6, y: 3).lineLimit(1).minimumScaleFactor(0.6)
            Text("\(shown.detail)  ·  \(shown.card)").font(IslandUI.font(size * 0.3)).foregroundStyle(.white.opacity(0.9))
                .lineLimit(1).minimumScaleFactor(0.6)
        }.frame(maxWidth: .infinity).accessibilityElement(children: .combine)
    }

    private func tab(_ course: GolfCourseChoice, height: CGFloat) -> some View {
        let on = course == shown
        return Button { show(course) } label: {
            Text(course.title.uppercased()).font(IslandUI.font(height * 0.36, bold: true))
                .foregroundStyle(on ? IslandUI.navy : .white).lineLimit(1).minimumScaleFactor(0.7)
                .padding(.horizontal, 22).frame(minWidth: 150, minHeight: height)
                .background(on ? Color.white : Color.white.opacity(0.16), in: Capsule())
                .overlay(Capsule().strokeBorder(.white.opacity(on ? 1 : 0.5), lineWidth: 2))
        }.buttonStyle(.plain).accessibilityLabel(course.title).accessibilityAddTraits(on ? .isSelected : [])
            .accessibilityIdentifier("map-\(course.rawValue)")
    }

    private func arrow(_ by: Int, size: CGFloat) -> some View {
        Button { step(by) } label: {
            Image(systemName: by < 0 ? "chevron.left" : "chevron.right").font(.system(size: size * 0.42, weight: .bold))
                .foregroundStyle(.white).frame(width: size, height: size)
                .background(.white.opacity(0.18), in: Circle()).overlay(Circle().strokeBorder(.white.opacity(0.55), lineWidth: 2))
        }.buttonStyle(.plain).accessibilityLabel(by < 0 ? "Previous course" : "Next course")
    }
}
