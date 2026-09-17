import SwiftUI

enum Palette {
    static let cream = Color(red: 0.96, green: 0.96, blue: 0.86)
    static let ink = Color(red: 0.06, green: 0.18, blue: 0.16)
    static let background = LinearGradient(
        colors: [Color(red: 0.05, green: 0.20, blue: 0.17), Color(red: 0.02, green: 0.08, blue: 0.08)],
        startPoint: .topLeading, endPoint: .bottomTrailing
    )

    static func player(_ index: Int) -> Color {
        [Color.mint, .orange, .yellow, .pink][index % 4]
    }

    static func difficulty(_ difficulty: CourseDifficulty) -> Color {
        switch difficulty {
        case .easy: .mint
        case .medium: .yellow
        case .hard: .orange
        }
    }
}

/// A large menu row that shows a mint ring when gesture focus is on it.
struct MenuRow<Label: View>: View {
    let focused: Bool
    let action: () -> Void
    @ViewBuilder let label: () -> Label

    var body: some View {
        Button(action: action) {
            label()
                .frame(maxWidth: .infinity, alignment: .leading)
                .padding(16)
                .background(.white.opacity(focused ? 0.14 : 0.06), in: RoundedRectangle(cornerRadius: 18))
                .overlay(RoundedRectangle(cornerRadius: 18).stroke(focused ? Color.mint : .white.opacity(0.09), lineWidth: focused ? 2.5 : 1))
        }
        .buttonStyle(.plain)
    }
}

extension View {
    /// Runs `action` for each camera gesture while this view is on screen.
    func onNavGesture(_ camera: CameraSwingController, perform action: @escaping (NavGesture) -> Void) -> some View {
        onReceive(camera.gestures.receive(on: RunLoop.main)) { action($0) }
    }
}

/// Small status chip: whether camera gestures are live, and a flash of the last one recognized.
struct GestureStatusChip: View {
    @ObservedObject var camera: CameraSwingController

    var body: some View {
        TimelineView(.periodic(from: .now, by: 0.2)) { timeline in
            let recent = camera.lastGesture.flatMap { timeline.date.timeIntervalSince($0.at) < 0.8 ? $0.gesture : nil }
            HStack(spacing: 6) {
                Image(systemName: recent?.symbol ?? (camera.gestureArmed ? "hand.raised.fill" : "hand.raised"))
                    .foregroundStyle(recent != nil || camera.gestureArmed ? .mint : .white.opacity(0.7))
                Text(recent.map { "\($0.rawValue.capitalized)" } ?? (camera.gestureArmed ? "Hand ready" : "Fist to swipe · punch to select"))
            }
            .font(.caption2.bold())
            .padding(.horizontal, 10).padding(.vertical, 6)
            .background(.black.opacity(0.55), in: Capsule())
            .animation(.easeOut(duration: 0.15), value: recent)
        }
        .accessibilityHidden(true)
    }
}
