import SwiftUI

/// The power gauge from motion golf games: a bar at the side of the screen that fills as the
/// player swings back, marked with how far the club goes and where the target sits on it, so
/// you can see how far you are about to hit it before you commit. The fill follows the meter
/// in distance (see `GolfClub.distanceYards(meter:)`), so half a bar is half the yardage.
struct PowerGauge: View {
    let club: GolfClub
    /// Meter reading, 0...1.
    let power: Double
    /// Reading that lands on the target; nil when the club cannot reach it.
    let targetPower: Double?
    let targetLabel: String
    /// True once the ball is away: the fill freezes at what the swing delivered.
    let struck: Bool
    var height: CGFloat = 240

    private let trackWidth: CGFloat = 16
    private var isPutt: Bool { club == .putter }

    var body: some View {
        HStack(alignment: .bottom, spacing: 6) {
            track
                .frame(width: trackWidth, height: height)
                .overlay(alignment: .top) { readout }
            labels
                .frame(height: height, alignment: .bottom)
        }
        .accessibilityElement(children: .ignore)
        .accessibilityLabel("Power gauge")
        .accessibilityValue(accessibilityValue)
        .accessibilityIdentifier("powerGauge")
    }

    // MARK: - Pieces

    private var track: some View {
        ZStack(alignment: .bottom) {
            Capsule().fill(.black.opacity(0.5))
            fill
            ticks
            Capsule().stroke(.white.opacity(0.18), lineWidth: 1)
        }
    }

    private var fill: some View {
        GeometryReader { geometry in
            let filled = geometry.size.height * CGFloat(min(max(power, 0), 1))
            VStack(spacing: 0) {
                Spacer(minLength: 0)
                Capsule()
                    .fill(LinearGradient(
                        colors: [.mint, .yellow, .orange],
                        startPoint: .bottom, endPoint: .top
                    ))
                    .frame(height: geometry.size.height)
                    .mask(alignment: .bottom) {
                        Rectangle().frame(height: max(filled, power > 0 ? trackWidth : 0))
                    }
                    .opacity(struck ? 0.75 : 1)
            }
            .animation(.easeOut(duration: 0.1), value: power)
        }
        .padding(2)
    }

    private var ticks: some View {
        GeometryReader { geometry in
            ForEach([0.25, 0.5, 0.75], id: \.self) { fraction in
                Rectangle()
                    .fill(.white.opacity(0.35))
                    .frame(width: trackWidth - 6, height: 1)
                    .position(x: geometry.size.width / 2, y: geometry.size.height * (1 - fraction))
            }
        }
    }

    /// Travels with the top of the fill: the distance this reading flies.
    private var readout: some View {
        GeometryReader { geometry in
            if power > 0.001 {
                let y = geometry.size.height * (1 - CGFloat(min(max(power, 0), 1)))
                Text(distanceText(power))
                    .font(.system(size: 12, weight: .heavy, design: .rounded)).monospacedDigit()
                    .foregroundStyle(.black)
                    .padding(.horizontal, 7).padding(.vertical, 3)
                    .background(struck ? Color.white : Color.yellow, in: Capsule())
                    .fixedSize()
                    .position(x: trackWidth / 2, y: min(max(y, 10), geometry.size.height - 10))
                    .offset(x: trackWidth / 2 + 4 + 22)
                    .animation(.easeOut(duration: 0.1), value: power)
                    .accessibilityHidden(true)
            }
        }
    }

    private var labels: some View {
        GeometryReader { geometry in
            ZStack(alignment: .topLeading) {
                // The club's full distance, out of the readout's way once the bar is nearly full.
                Text(distanceText(1))
                    .font(.system(size: 10, weight: .heavy, design: .rounded))
                    .foregroundStyle(.white.opacity(0.85))
                    .position(x: 22, y: 6)
                    .opacity(power > 0.86 ? 0 : 1)
                // Where the target sits on the bar; pinned to the top when the club cannot reach it.
                let reach = targetPower ?? 1
                let y = geometry.size.height * (1 - CGFloat(min(max(reach, 0), 1)))
                HStack(spacing: 3) {
                    Image(systemName: "flag.fill").font(.system(size: 9, weight: .bold))
                    Text(targetPower == nil ? "\(targetLabel) +" : targetLabel)
                        .font(.system(size: 9, weight: .heavy, design: .rounded))
                }
                .foregroundStyle(targetPower == nil ? .white.opacity(0.6) : .white)
                .padding(.horizontal, 6).padding(.vertical, 3)
                .background(.black.opacity(0.55), in: Capsule())
                .fixedSize()
                .position(x: 30, y: min(max(y, 18), geometry.size.height - 8))
                .opacity(power > 0.001 && abs(power - reach) < 0.14 ? 0.35 : 1)
            }
            .animation(.easeOut(duration: 0.2), value: targetPower)
        }
        .frame(width: 70)
    }

    // MARK: - Text

    private func distanceText(_ reading: Double) -> String {
        let yards = club.distanceYards(meter: reading)
        return isPutt ? "\(Int((yards * 3).rounded())) FT" : "\(Int(yards.rounded())) YD"
    }

    private var accessibilityValue: String {
        var parts = ["\(Int((power * 100).rounded())) percent, \(distanceText(power))"]
        if let targetPower { parts.append("\(targetLabel.lowercased()) at \(Int((targetPower * 100).rounded())) percent") }
        if struck { parts.append("struck") }
        return parts.joined(separator: ", ")
    }
}

#Preview {
    ZStack {
        Color.green.ignoresSafeArea()
        HStack(spacing: 40) {
            PowerGauge(club: .driver, power: 0.72, targetPower: 0.85, targetLabel: "LANDING", struck: false)
            PowerGauge(club: .putter, power: 0.3, targetPower: 0.2, targetLabel: "PIN", struck: true)
            PowerGauge(club: .iron, power: 0, targetPower: nil, targetLabel: "PIN", struck: false)
        }
    }
}
