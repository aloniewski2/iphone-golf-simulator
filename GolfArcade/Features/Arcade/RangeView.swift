import SwiftUI

struct RangeView: View {
    let shot: ShotResult

    var body: some View {
        GeometryReader { geometry in
            let path = FlightPath(shot: shot)
            ZStack {
                LinearGradient(colors: [.blue.opacity(0.7), .cyan.opacity(0.3)], startPoint: .top, endPoint: .bottom)
                Ellipse().fill(.green.gradient)
                    .frame(width: geometry.size.width * 1.5, height: geometry.size.height * 0.48)
                    .offset(y: geometry.size.height * 0.39)
                Canvas { context, size in
                    guard path.points.count > 1 else { return }
                    let maxDistance = max(shot.carryYards, shot.rolloutYards, 1)
                    let maxHeight = max(shot.apexYards, 1)
                    var line = Path()
                    for (index, point) in path.points.enumerated() {
                        let x = size.width * (0.08 + 0.84 * point.distanceYards / maxDistance)
                        let y = size.height * (0.82 - 0.58 * point.heightYards / maxHeight)
                        if index == 0 { line.move(to: CGPoint(x: x, y: y)) } else { line.addLine(to: CGPoint(x: x, y: y)) }
                    }
                    context.stroke(line, with: .color(.white), style: StrokeStyle(lineWidth: 4, lineCap: .round, dash: [7, 7]))
                }
                VStack {
                    Spacer()
                    HStack { Text("TEE"); Spacer(); Image(systemName: "flag.fill") }
                        .font(.caption.bold()).foregroundStyle(.white)
                        .padding(.horizontal, 24).padding(.bottom, 16)
                }
            }
        }
        .clipShape(RoundedRectangle(cornerRadius: 24, style: .continuous))
        .accessibilityLabel("Ball flight for a \(shot.shape.displayName.lowercased()) shot carrying \(Int(shot.carryYards)) yards")
    }
}

