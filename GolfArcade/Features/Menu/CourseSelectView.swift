import SwiftUI

struct CourseSelectView: View {
    @ObservedObject var flow: GameFlow
    @ObservedObject var camera: CameraSwingController
    @AppStorage("gestures.enabled") private var gesturesEnabled = true
    @State private var focus = 0

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 20) {
                Button { flow.screen = .players } label: { Label("Players", systemImage: "chevron.left") }
                    .font(.subheadline.bold())
                    .foregroundStyle(focus == Course.all.count ? .mint : Palette.cream)
                    .accessibilityIdentifier("backToPlayers")
                VStack(alignment: .leading, spacing: 6) {
                    Text("CHOOSE A COURSE")
                        .font(.system(size: 12, weight: .black, design: .rounded)).tracking(2)
                        .foregroundStyle(.mint)
                    Text("Where are you playing?").font(.system(size: 30, weight: .black, design: .rounded))
                    Text(flow.players.map(\.name).joined(separator: " · "))
                        .font(.caption).foregroundStyle(.white.opacity(0.6))
                }
                VStack(spacing: 12) {
                    ForEach(Array(Course.all.enumerated()), id: \.element.id) { index, course in
                        MenuRow(focused: focus == index, action: { flow.play(course) }) { card(course) }
                            .accessibilityIdentifier("course-\(course.difficulty.rawValue)")
                    }
                }
                if gesturesEnabled { GestureStatusChip(camera: camera) }
            }
            .padding(24)
            .frame(maxWidth: 620)
            .frame(maxWidth: .infinity)
        }
        .background(Palette.background.ignoresSafeArea())
        .accessibilityIdentifier("courseSelection")
        .onNavGesture(camera) { gesture in
            let count = Course.all.count + 1
            switch gesture {
            case .up, .left: focus = (focus + count - 1) % count
            case .down, .right: focus = (focus + 1) % count
            case .select:
                if focus < Course.all.count { flow.play(Course.all[focus]) } else { flow.screen = .players }
            }
        }
    }

    private func card(_ course: Course) -> some View {
        let color = Palette.difficulty(course.difficulty)
        return HStack(spacing: 14) {
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
            if let best = UserDefaults.standard.object(forKey: "course.\(course.id).best") as? Int {
                VStack(spacing: 2) {
                    Text("BEST").font(.system(size: 9, weight: .black))
                    Text(ScoreFormat.toPar(best)).font(.headline).monospacedDigit()
                }
                .foregroundStyle(.mint)
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
