import SwiftUI

struct ShotResultCard: View {
    let shot: ShotResult
    var body: some View {
        VStack(spacing: 14) {
            HStack(alignment: .firstTextBaseline) {
                VStack(alignment: .leading, spacing: 2) {
                    Text(shot.shape.displayName).font(.title2.bold())
                    Text("\(shot.strike.displayName) contact").font(.subheadline).foregroundStyle(.secondary)
                }
                Spacer()
                Text("\(Int(shot.totalYards.rounded()))").font(.system(size: 48, weight: .black, design: .rounded))
                Text("YD").font(.caption.bold()).foregroundStyle(.secondary)
            }
            Divider()
            HStack {
                metric("BALL SPEED", "\(Int(shot.ballSpeedMPH)) mph")
                Spacer(); metric("LAUNCH", "\(Int(shot.launchAngleDegrees))°")
                Spacer(); metric("CONFIDENCE", "\(Int(shot.confidence * 100))%")
            }
        }
        .padding(18)
        .background(.ultraThinMaterial, in: RoundedRectangle(cornerRadius: 22, style: .continuous))
    }

    private func metric(_ label: String, _ value: String) -> some View {
        VStack(alignment: .leading, spacing: 3) {
            Text(label).font(.caption2.bold()).foregroundStyle(.secondary)
            Text(value).font(.subheadline.bold())
        }
    }
}

