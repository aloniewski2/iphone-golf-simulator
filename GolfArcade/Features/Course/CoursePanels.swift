import SwiftUI

struct StatusPill: View {
    let icon: String
    let title: String
    let detail: String?
    var titleLineLimit = 1

    var body: some View {
        HStack(spacing: 10) {
            Image(systemName: icon).font(.headline)
            VStack(alignment: .leading, spacing: 1) {
                Text(title).font(.subheadline.bold()).lineLimit(titleLineLimit).minimumScaleFactor(0.7)
                    .fixedSize(horizontal: false, vertical: true)
                if let detail { Text(detail).font(.caption2).foregroundStyle(.white.opacity(0.68)).lineLimit(1) }
            }
            Spacer(minLength: 0)
        }
        .padding(.horizontal, 14).padding(.vertical, 10)
        .background(.black.opacity(0.62), in: Capsule())
        .overlay(Capsule().stroke(.white.opacity(0.12)))
        .accessibilityElement(children: .combine)
    }
}

struct SwingPad: View {
    let club: GolfClub
    let power: Double
    let charging: Bool
    let onCharge: (Double) -> Void
    let onRelease: (Double) -> Void
    let onCancel: () -> Void
    let onDemo: () -> Void
    @State private var direction = 0.0
    @GestureState private var dragging = false

    var body: some View {
        if club == .putter {
            PuttingPad(onCharge: onCharge, onRelease: onRelease)
        } else {
            dragPad
        }
    }

    private var dragPad: some View {
        ZStack(alignment: .leading) {
            RoundedRectangle(cornerRadius: 18).fill(.black.opacity(0.62))
            GeometryReader { geometry in
                RoundedRectangle(cornerRadius: 18)
                    .fill(LinearGradient(colors: [.mint.opacity(0.32), .yellow.opacity(0.58)], startPoint: .leading, endPoint: .trailing))
                    .frame(width: geometry.size.width * power)
            }
            HStack(spacing: 12) {
                Image(systemName: "arrow.down").font(.title2)
                VStack(alignment: .leading, spacing: 3) {
                    Text(charging ? "Release to \(club == .putter ? "putt" : "swing")" : "Pull down gently").font(.headline)
                    Text(club == .putter ? String(format: "%.1f ft · %+.1f°", club.mockDistance * power * power * 3, direction) : String(format: "Slide sideways to steer · %+.0f°", direction))
                        .font(.caption2)
                }
                Spacer()
                Text("\(Int(power * 100))%").font(.title2.bold()).monospacedDigit()
            }
            .padding(16)
        }
        .frame(height: 100)
        .contentShape(Rectangle())
        .gesture(DragGesture(minimumDistance: 3)
            .updating($dragging) { _, state, _ in state = true }
            .onChanged {
                direction = min(25, max(-25, Double($0.translation.width) / (club == .putter ? 12 : 4)))
                onCharge(max(0, Double($0.translation.height)) / 80)
            }
            .onEnded { _ in onRelease(direction); direction = 0 })
        .onChange(of: dragging) { _, active in
            if !active {
                Task { @MainActor in
                    await Task.yield()
                    if charging { onCancel() }
                }
            }
        }
        // A drag interrupted by the system never reports `onEnded`; a tap resets it.
        .onTapGesture { onCancel() }
        .accessibilityElement(children: .ignore)
        .accessibilityLabel(club == .putter ? "Putting pad. Pull down gently and release." : "Swing pad. Pull down and release to swing.")
        .accessibilityValue("\(Int(power * 100)) percent power")
        .accessibilityIdentifier("swingPad")
        .accessibilityAction(named: "Swing at 75 percent", onDemo)
        .accessibilityAction(named: "Gentle stroke") { onCharge(0.1); onRelease(0) }
    }
}

/// Fixed-gain precision putting: the selected strength is visible before committing a stroke.
private struct PuttingPad: View {
    let onCharge: (Double) -> Void
    let onRelease: (Double) -> Void
    @State private var power = 0.1

    var body: some View {
        VStack(spacing: 8) {
            HStack {
                Text("Putt strength").font(.subheadline.bold())
                Spacer()
                Text(String(format: "%.1f ft", GolfClub.putter.mockDistance * power * power * 3))
                    .font(.subheadline.monospacedDigit())
            }
            Slider(value: $power, in: 0...1, step: 0.005) { Text("Putt strength") }
                .tint(.mint)
                .accessibilityIdentifier("puttPower")
                .accessibilityValue(String(format: "%.1f feet", GolfClub.putter.mockDistance * power * power * 3))
            Button {
                onCharge(power)
                onRelease(0)
            } label: {
                Text("Putt").font(.headline).frame(maxWidth: .infinity).padding(.vertical, 8)
            }
            .buttonStyle(.borderedProminent).tint(.mint).foregroundStyle(Palette.ink)
            .disabled(power == 0)
            .accessibilityIdentifier("puttStroke")
        }
        .padding(14)
        .background(.black.opacity(0.62), in: RoundedRectangle(cornerRadius: 18))
        .accessibilityElement(children: .contain)
        .accessibilityLabel("Putting pad")
        .accessibilityIdentifier("swingPad")
    }
}

/// Shows where the hands are relative to the ball's target ring while lining up.
struct LineUpIndicator: View {
    let offset: CGVector

    var body: some View {
        let scale: CGFloat = 26 / BallAddress.addressTolerance
        let dot = CGPoint(x: min(max(offset.dx * scale, -40), 40), y: min(max(-offset.dy * scale, -40), 40))
        ZStack {
            Circle().stroke(.yellow.opacity(0.9), lineWidth: 2).frame(width: 52, height: 52)
            Circle().fill(.white).frame(width: 8, height: 8)
            Circle().fill(.mint).frame(width: 12, height: 12).offset(x: dot.x, y: dot.y)
        }
        .frame(width: 92, height: 92)
        .background(.black.opacity(0.45), in: Circle())
        .accessibilityHidden(true)
    }
}

struct TurnBanner: View {
    let name: String
    let color: Color
    let detail: String
    let showsName: Bool

    var body: some View {
        VStack(spacing: 2) {
            if showsName {
                Text("\(name)'s turn".uppercased()).font(.system(size: 13, weight: .black, design: .rounded)).foregroundStyle(color)
            }
            Text(detail).font(.subheadline.bold())
        }
        .padding(.horizontal, 18).padding(.vertical, 10)
        .background(.black.opacity(0.7), in: Capsule())
        .overlay(Capsule().stroke(color.opacity(0.6), lineWidth: 1.5))
        .accessibilityIdentifier("turnBanner")
    }
}

private struct PanelButtons: View {
    let primary: String
    let primaryID: String
    let onReplay: () -> Void
    let onPrimary: () -> Void

    var body: some View {
        HStack(spacing: 9) {
            Button(action: onReplay) {
                Label("Replay", systemImage: "arrow.clockwise")
                    .frame(maxWidth: .infinity).padding(.vertical, 12)
                    .background(.white.opacity(0.1), in: RoundedRectangle(cornerRadius: 12))
            }
            .accessibilityIdentifier("replayShot")
            Button(action: onPrimary) {
                Text(primary)
                    .frame(maxWidth: .infinity).padding(.vertical, 12)
                    .background(Palette.cream, in: RoundedRectangle(cornerRadius: 12)).foregroundStyle(Palette.ink)
            }
            .accessibilityIdentifier(primaryID)
        }
        .font(.subheadline.bold())
        .buttonStyle(.plain)
    }
}

private struct Panel<Content: View>: View {
    @ViewBuilder let content: () -> Content

    var body: some View {
        VStack(spacing: 11, content: content)
            .padding(16)
            .background(.black.opacity(0.74), in: RoundedRectangle(cornerRadius: 20))
            .overlay(RoundedRectangle(cornerRadius: 20).stroke(.white.opacity(0.12)))
    }
}

struct ShotResultPanel: View {
    @ObservedObject var round: CourseRound
    let shot: RangeShot
    let showsStrike: Bool
    let onReplay: () -> Void
    let onNext: () -> Void

    var body: some View {
        Panel {
            HStack(alignment: .top) {
                VStack(alignment: .leading, spacing: 3) {
                    Text(subtitle).font(.caption2.bold()).tracking(1).foregroundStyle(.white.opacity(0.7))
                    Text(shot.lie?.displayName ?? "In play").font(.title2.bold())
                        .foregroundStyle(shot.penaltyStrokes > 0 ? .orange : Palette.cream)
                        .accessibilityIdentifier("shotLie")
                }
                Spacer()
                VStack(alignment: .trailing, spacing: 2) {
                    Text("\(Int(shot.total.rounded())) yd").font(.title2.bold()).monospacedDigit()
                    Text("\(Int(round.distanceToPin.rounded())) yd to pin").font(.caption.bold()).foregroundStyle(.mint)
                }
            }
            PanelButtons(primary: "Next shot", primaryID: "nextShot", onReplay: onReplay, onPrimary: onNext)
            if round.nextShotAt != nil {
                Text("Next shot and club in 3 seconds · Replay pauses auto-advance")
                    .font(.caption2).foregroundStyle(.mint)
                    .accessibilityIdentifier("autoNextShot")
            }
        }
    }

    private var subtitle: String {
        var parts = ["STROKE \(round.strokes)"]
        if shot.penaltyStrokes > 0 { parts.append("+\(shot.penaltyStrokes) PENALTY · DROP") }
        if showsStrike { parts.append("\(shot.strike.displayName.uppercased()) CONTACT") }
        return parts.joined(separator: " · ")
    }
}

struct HoleCompletePanel: View {
    @ObservedObject var round: CourseRound
    let player: Player
    let shot: RangeShot
    let isMultiplayer: Bool
    let onReplay: () -> Void
    let onContinue: () -> Void

    var body: some View {
        let strokes = round.scores[round.playerIndex][round.holeIndex] ?? round.strokes
        Panel {
            HStack {
                VStack(alignment: .leading, spacing: 3) {
                    Text((isMultiplayer ? "\(player.name) · " : "") + "HOLE \(round.hole.number)")
                        .font(.caption2.bold()).tracking(1).foregroundStyle(Palette.player(player.colorIndex))
                    Text(round.pickedUp ? "Picked up" : ScoreFormat.holeName(strokes: strokes, par: round.hole.par))
                        .font(.title.bold())
                        .accessibilityIdentifier("holeResult")
                }
                Spacer()
                VStack(alignment: .trailing, spacing: 2) {
                    Text("\(strokes) strokes").font(.title3.bold()).monospacedDigit()
                    Text("Par \(round.hole.par) · Total \(ScoreFormat.toPar(round.toPar(for: round.playerIndex)))")
                        .font(.caption.bold()).foregroundStyle(.mint)
                }
            }
            PanelButtons(primary: continueTitle, primaryID: "continueHole", onReplay: onReplay, onPrimary: onContinue)
        }
    }

    private var continueTitle: String {
        if round.isLastTurn { return "Final scores" }
        return round.playerIndex < round.playerCount - 1 ? "Next player" : "Next hole"
    }
}

struct RoundCompletePanel: View {
    @ObservedObject var round: CourseRound
    let players: [Player]
    let onPlayAgain: () -> Void
    let onMenu: () -> Void

    var body: some View {
        Panel {
            HStack {
                VStack(alignment: .leading, spacing: 3) {
                    Text("ROUND COMPLETE").font(.caption2.bold()).tracking(1.5)
                        .accessibilityIdentifier("roundComplete")
                    Text(headline).font(.title2.bold())
                }
                Spacer()
                if players.count == 1, let best = round.best {
                    Text("BEST \(ScoreFormat.toPar(best))").font(.caption.bold()).foregroundStyle(.mint)
                }
            }
            ScorecardView(round: round, players: players)
            HStack(spacing: 9) {
                Button(action: onMenu) {
                    Label("Menu", systemImage: "house")
                        .frame(maxWidth: .infinity).padding(.vertical, 12)
                        .background(.white.opacity(0.1), in: RoundedRectangle(cornerRadius: 12))
                }
                .accessibilityIdentifier("quitToMenu")
                Button(action: onPlayAgain) {
                    Text("Play again")
                        .frame(maxWidth: .infinity).padding(.vertical, 12)
                        .background(Palette.cream, in: RoundedRectangle(cornerRadius: 12)).foregroundStyle(Palette.ink)
                }
                .accessibilityIdentifier("playAgain")
            }
            .font(.subheadline.bold())
            .buttonStyle(.plain)
        }
    }

    private var headline: String {
        guard players.count > 1 else { return "\(round.total(for: 0)) strokes · \(ScoreFormat.toPar(round.toPar(for: 0)))" }
        let best = players.indices.min { round.total(for: $0) < round.total(for: $1) } ?? 0
        let tied = players.indices.filter { round.total(for: $0) == round.total(for: best) }.count > 1
        return tied ? "It's a tie" : "\(players[best].name) wins"
    }
}

struct ScorecardView: View {
    @ObservedObject var round: CourseRound
    let players: [Player]

    var body: some View {
        Grid(alignment: .trailing, horizontalSpacing: 10, verticalSpacing: 6) {
            GridRow {
                Text("HOLE").gridColumnAlignment(.leading)
                ForEach(round.course.holes) { Text("\($0.number)") }
                Text("TOT")
                Text("±")
            }
            .font(.caption2.bold()).foregroundStyle(.white.opacity(0.6))
            GridRow {
                Text("Par").gridColumnAlignment(.leading)
                ForEach(round.course.holes) { Text("\($0.par)") }
                Text("\(round.course.par)")
                Text("")
            }
            .font(.caption).foregroundStyle(.white.opacity(0.6))
            ForEach(Array(players.enumerated()), id: \.element.id) { index, player in
                GridRow {
                    HStack(spacing: 5) {
                        Circle().fill(Palette.player(player.colorIndex)).frame(width: 7, height: 7)
                        Text(player.name).lineLimit(1)
                    }
                    ForEach(round.course.holes.indices, id: \.self) { hole in
                        Text(round.scores[index][hole].map(String.init) ?? "–")
                    }
                    Text("\(round.total(for: index))").bold()
                    Text(ScoreFormat.toPar(round.toPar(for: index))).bold().foregroundStyle(.mint)
                }
                .font(.subheadline).monospacedDigit()
            }
        }
        .accessibilityIdentifier("scorecard")
    }
}
