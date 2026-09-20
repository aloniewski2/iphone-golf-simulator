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

/// A large menu row that shows a mint ring when selected.
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
