import SwiftUI

struct SwingFeedbackOverlay: View {
    let phase: SwingPhase
    let impactPulse: Int

    @State private var impactOpacity = 0.0

    var body: some View {
        ZStack {
            ForEach(0..<3, id: \.self) { index in
                Circle()
                    .stroke(ringColor.opacity(0.48 - Double(index) * 0.11), lineWidth: 3)
                    .scaleEffect(ringScale + Double(index) * 0.13)
            }
            .frame(width: 150, height: 150)

            if phase == .backswing {
                Text("LOAD")
                    .font(.caption.bold().monospaced())
                    .foregroundStyle(.yellow)
                    .transition(.opacity)
            }

            Color.white
                .opacity(impactOpacity)
                .blendMode(.screen)
                .ignoresSafeArea()
        }
        .allowsHitTesting(false)
        .animation(.easeInOut(duration: phase == .backswing ? 0.9 : 0.18), value: phase)
        .task(id: impactPulse) {
            guard impactPulse > 0 else { return }
            impactOpacity = 0.72
            try? await Task.sleep(for: .milliseconds(55))
            withAnimation(.easeOut(duration: 0.2)) { impactOpacity = 0 }
        }
        .accessibilityHidden(true)
    }

    private var ringScale: Double {
        switch phase {
        case .backswing: 0.7
        case .downswing: 1.05
        case .impact: 1.22
        default: 0.9
        }
    }

    private var ringColor: Color {
        switch phase {
        case .backswing: .yellow
        case .downswing: .orange
        case .impact: .white
        default: .mint
        }
    }
}
