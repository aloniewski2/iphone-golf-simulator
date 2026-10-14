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
                HStack(alignment: .top) {
                    IslandWordmark(size: 28).frame(width: 180)
                    Text("CHOOSE YOUR COURSE").font(IslandUI.font(38, bold: true)).foregroundStyle(.white).padding(.leading, 18)
                    Spacer()
                    GolfDailyCard(menu: menu, compact: false).frame(width: 470)
                }
                Spacer()
                HStack { arrow(-1, size: 96); Spacer(); arrow(1, size: 96) }
                Spacer()
                title(size: 64)
                HStack(spacing: 14) { ForEach(all, id: \.self) { tab($0, height: 58) } }
                settings(height: 52)
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
                GolfDailyCard(menu: menu, compact: true)
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
                settings(height: 46)
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

    /// The round's match settings, before it starts: twists (two random modifiers dealt each round), the
    /// weather with things moving about the holes, and a bounty on every hole paying coins (the golden
    /// rings come with the Point Frenzy twist), each on or off, remembered for the next round.
    private func settings(height: CGFloat) -> some View {
        let session = SportsSession.shared
        return VStack(spacing: 6) {
            HStack(spacing: 10) {
                toggle("golf-twists", "Twists", icon: "dice.fill", on: session.golfTwists, height: height)
                toggle("golf-weather", compact ? "Weather" : "Weather & hazards", icon: "cloud.bolt.rain.fill", on: session.golfWeather, height: height)
                toggle("golf-challenges", "Bounties", icon: "target", on: session.golfChallenges, height: height)
            }
            Text(caption(twists: session.golfTwists, weather: session.golfWeather, challenges: session.golfChallenges))
                .font(IslandUI.font(height * 0.27)).foregroundStyle(.white.opacity(0.8)).lineLimit(1).minimumScaleFactor(0.6)
        }.frame(maxWidth: .infinity)
    }

    private func caption(twists: Bool, weather: Bool, challenges: Bool) -> String {
        if !twists && !weather && !challenges { return "Classic golf: no twists, clear skies" }
        return [twists ? "2 random twists each round" : "Classic rules",
                weather ? "storms, fog, gusts and moving obstacles" : "clear skies",
                challenges ? "a bounty on every hole" : nil].compactMap { $0 }.joined(separator: " · ")
    }

    private func toggle(_ id: String, _ title: String, icon: String, on: Bool, height: CGFloat) -> some View {
        let focused = menu.isFocused(id)
        return Button { menu.tap(id) } label: {
            HStack(spacing: 8) {
                Image(systemName: icon).font(.system(size: height * 0.34, weight: .bold))
                Text(title.uppercased()).font(IslandUI.font(height * 0.3, bold: true)).lineLimit(1).minimumScaleFactor(0.6)
                Text(on ? "ON" : "OFF").font(IslandUI.font(height * 0.26, bold: true))
                    .padding(.horizontal, 8).padding(.vertical, 3)
                    .background(on ? IslandUI.navy.opacity(0.18) : Color.white.opacity(0.18), in: Capsule())
            }
            .foregroundStyle(on ? IslandUI.navy : .white)
            .padding(.horizontal, 16).frame(maxWidth: .infinity, minHeight: height)
            .background(on ? IslandUI.lime : Color.white.opacity(0.12), in: Capsule())
            .overlay(Capsule().strokeBorder(.white.opacity(focused ? 1 : 0.45), lineWidth: focused ? 3 : 2))
        }.buttonStyle(.plain).accessibilityLabel(title).accessibilityValue(on ? "On" : "Off")
            .accessibilityAddTraits(.isButton).accessibilityIdentifier(id)
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


/// Golf's daily forecast on the course screen: today's course, its twists and the weather hole by hole
/// (a weather app's strip), the player's day streak and today's best, and PLAY to play it. Dealt by
/// GolfForecast from the date, as Unity deals the round itself.
struct GolfDailyCard: View {
    let menu: TennisMenu
    let compact: Bool

    var body: some View {
        let today = GolfForecast.today()
        let day = GolfForecast.Day(today)
        let daily = DailyGolf.shared
        let streak = daily.streak(through: today)
        let best = daily.best[today]
        let focused = menu.isFocused("golf-daily")
        return Button { menu.tap("golf-daily") } label: {
            VStack(alignment: .leading, spacing: compact ? 6 : 10) {
                HStack(spacing: 8) {
                    Text("DAILY FORECAST").font(IslandUI.font(compact ? 13 : 17, bold: true)).tracking(1.5)
                        .padding(.horizontal, 10).padding(.vertical, 4).background(IslandUI.lime, in: Capsule()).foregroundStyle(IslandUI.navy)
                    Text(GolfForecast.date(of: today).formatted(.dateTime.weekday(.wide).month(.abbreviated).day()))
                        .font(IslandUI.font(compact ? 12 : 16, bold: true)).foregroundStyle(.white.opacity(0.85)).lineLimit(1)
                    Spacer(minLength: 4)
                    if streak > 0 {
                        Label("\(streak)", systemImage: "flame.fill").font(IslandUI.font(compact ? 14 : 19, bold: true))
                            .foregroundStyle(Color(hex: "FF9F2E")).accessibilityLabel("\(streak) day streak")
                    }
                }
                HStack(alignment: .firstTextBaseline, spacing: 8) {
                    Text(day.headline.uppercased()).font(IslandUI.font(compact ? 20 : 30, bold: true)).foregroundStyle(.white).lineLimit(1).minimumScaleFactor(0.6)
                    Text("at \(day.course.title)").font(IslandUI.font(compact ? 14 : 19)).foregroundStyle(.white.opacity(0.85)).lineLimit(1)
                }
                // the weather hole by hole, like a forecast's strip
                HStack(spacing: compact ? 6 : 10) {
                    ForEach(Array(day.weather.enumerated()), id: \.offset) { i, w in
                        VStack(spacing: 2) {
                            Image(systemName: w.icon).symbolRenderingMode(.multicolor).font(.system(size: compact ? 17 : 24, weight: .semibold))
                                .frame(height: compact ? 20 : 28)
                            Text("\(i + 1)").font(IslandUI.font(compact ? 10 : 13, bold: true)).foregroundStyle(.white.opacity(0.7))
                        }.frame(minWidth: compact ? 24 : 34)
                    }
                    Spacer(minLength: 6)
                    VStack(alignment: .trailing, spacing: 3) {
                        ForEach(day.twists, id: \.self) { t in
                            Label(t.title.uppercased(), systemImage: t.icon).font(IslandUI.font(compact ? 11 : 15, bold: true)).foregroundStyle(.white).lineLimit(1)
                        }
                    }
                }
                HStack {
                    Text(best.map { "Today's best \(GolfRoundStats.text(toPar: $0)) · play again any time" } ?? "Bonus XP for your first round today · same course for everyone")
                        .font(IslandUI.font(compact ? 11 : 15)).foregroundStyle(.white.opacity(0.8)).lineLimit(1).minimumScaleFactor(0.7)
                    Spacer(minLength: 6)
                    Text(best == nil ? "PLAY" : "AGAIN").font(IslandUI.font(compact ? 13 : 18, bold: true)).foregroundStyle(IslandUI.navy)
                        .padding(.horizontal, 14).padding(.vertical, 5).background(Color(hex: "FFC93C"), in: Capsule())
                }
            }
            .padding(compact ? 12 : 18)
            .background(LinearGradient(colors: [Color(hex: "1D4E89").opacity(0.92), Color(hex: "0B2545").opacity(0.92)], startPoint: .topLeading, endPoint: .bottomTrailing),
                        in: RoundedRectangle(cornerRadius: compact ? 16 : 22))
            .overlay(RoundedRectangle(cornerRadius: compact ? 16 : 22).strokeBorder(focused ? Color.white : Color(hex: "FFC93C").opacity(0.7), lineWidth: focused ? 4 : 2))
        }.buttonStyle(.plain)
            .accessibilityLabel("Daily forecast: \(day.headline) at \(day.course.title). Twists \(day.twists.map(\.title).joined(separator: " and ")).")
            .accessibilityAddTraits(.isButton).accessibilityIdentifier("golf-daily")
    }
}
