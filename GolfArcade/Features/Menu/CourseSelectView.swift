import SwiftUI

struct CourseSelectView: View {
    @ObservedObject var flow: GameFlow
    @State private var focus = 0
    // Keep the comparison courses and their assets, but offer only the main
    // Sunward Resort course in the player-facing menu.
    private let featuredCourses = [Course.sunwardResort]

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 20) {
                Button { flow.screen = .players } label: { Label("Players", systemImage: "chevron.left") }
                    .font(.subheadline.bold())
                    .foregroundStyle(focus == featuredCourses.count ? .mint : Palette.cream)
                    .accessibilityIdentifier("backToPlayers")
                VStack(alignment: .leading, spacing: 6) {
                    Text("GOLF COURSE")
                        .font(.system(size: 12, weight: .black, design: .rounded)).tracking(2)
                        .foregroundStyle(.mint)
                    Text("Ready to tee off?").font(.system(size: 30, weight: .black, design: .rounded))
                    Text(flow.players.map(\.name).joined(separator: " · "))
                        .font(.caption).foregroundStyle(.white.opacity(0.6))
                }
                VStack(spacing: 12) {
                    ForEach(Array(featuredCourses.enumerated()), id: \.element.id) { index, course in
                        MenuRow(focused: focus == index, action: { flow.play(course) }) { card(course) }
                            .accessibilityIdentifier("course-\(course.id.hasPrefix("sunward") ? course.id : course.difficulty.rawValue)")
                    }
                }
            }
            .padding(24)
            .frame(maxWidth: 620)
            .frame(maxWidth: .infinity)
        }
        .background(Palette.background.ignoresSafeArea())
        .accessibilityIdentifier("courseSelection")
    }

    private func card(_ course: Course) -> some View {
        let color = Palette.difficulty(course.difficulty)
        return VStack(alignment: .leading, spacing: 12) {
                NativeCoursePreviewImage(courseID: course.id, hole: course.holes[0].number)
                    .frame(height: 156).clipped()
                    .overlay(alignment: .bottomLeading) {
                        Text(course.name.uppercased()).font(.system(size: 11, weight: .black, design: .rounded)).tracking(2)
                            .padding(10).background(.black.opacity(0.55), in: Capsule()).padding(12)
                    }
                    .clipShape(RoundedRectangle(cornerRadius: 12))
                    .accessibilityLabel("\(course.name), rendered from the playable RealityKit course")
            HStack(spacing: 14) {
            Image(systemName: icon(course.difficulty))
                .font(.title2.bold())
                .frame(width: 46, height: 46)
                .background(color.opacity(0.2), in: Circle())
                .foregroundStyle(color)
            VStack(alignment: .leading, spacing: 4) {
                Text(course.name).font(.title3.bold())
                Text("\(course.difficulty.displayName) · \(course.holes.count) holes · Par \(course.par) · \(Int(course.length)) yd")
                    .font(.caption).foregroundStyle(.white.opacity(0.62))
                Text(hazards(course)).font(.caption2).foregroundStyle(.white.opacity(0.48))
            }
            Spacer()
            if let best = UserDefaults.standard.object(forKey: course.bestScoreKey) as? Int {
                VStack(spacing: 2) {
                    Text("BEST").font(.system(size: 9, weight: .black))
                    Text(ScoreFormat.toPar(best)).font(.headline).monospacedDigit()
                }
                .foregroundStyle(.mint)
            }
            }
            if course.id == Course.sunwardResort.id {
                ScrollView(.horizontal, showsIndicators: false) {
                    HStack(spacing: 6) {
                        ForEach(1...9, id: \.self) { hole in
                            NativeCoursePreviewImage(courseID: course.id, hole: hole)
                                .frame(width: 76, height: 48).clipped()
                                .overlay(alignment: .bottomLeading) {
                                    Text("\(hole)").font(.caption2.bold()).padding(4).background(.black.opacity(0.6))
                                }.clipShape(RoundedRectangle(cornerRadius: 6))
                                .accessibilityLabel("Hole \(hole) preview")
                        }
                    }
                }
            }
        }
    }

    private func icon(_ difficulty: CourseDifficulty) -> String {
        switch difficulty {
        case .easy: "leaf.fill"
        case .medium: "tree.fill"
        case .hard: "water.waves"
        }
    }

    private func hazards(_ course: Course) -> String {
        let bunkers = "\(course.bunkerCount) bunkers"
        return course.hasWater ? "Water carries · \(bunkers)" : bunkers
    }
}

/// Offline snapshots of the runtime USDZ, not an additional live 3D renderer.
struct NativeCoursePreviewImage: View {
    let courseID: String
    let hole: Int
    @State private var image: UIImage?
    @State private var missing = false
    private var resource: String { "NativePreview-\(courseID)-\(hole)" }

    var body: some View {
        Group {
            if let image {
                Image(uiImage: image).resizable().scaledToFill()
            } else if missing {
                Label("Course preview unavailable", systemImage: "photo")
                    .font(.caption).foregroundStyle(.secondary)
                    .frame(maxWidth: .infinity, maxHeight: .infinity)
            } else {
                Rectangle().fill(.white.opacity(0.06))
            }
        }
        .task(id: resource) {
            image = nil; missing = false
            guard let url = Bundle.main.url(forResource: resource, withExtension: "jpg"),
                  let loaded = UIImage(contentsOfFile: url.path) else {
                missing = true
                return
            }
            let decoded = await loaded.byPreparingForDisplay()
            guard !Task.isCancelled else { return }
            image = decoded ?? loaded
        }
    }
}

enum ScoreFormat {
    static func toPar(_ value: Int) -> String {
        value == 0 ? "E" : value > 0 ? "+\(value)" : "\(value)"
    }

    static func holeName(strokes: Int, par: Int) -> String {
        if strokes == 1 { return "Hole in one!" }
        switch strokes - par {
        case ...(-3): return "Albatross"
        case -2: return "Eagle"
        case -1: return "Birdie"
        case 0: return "Par"
        case 1: return "Bogey"
        case 2: return "Double bogey"
        default: return "+\(strokes - par)"
        }
    }
}
