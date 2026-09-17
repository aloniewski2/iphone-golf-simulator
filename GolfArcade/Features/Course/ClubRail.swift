import SwiftUI

/// Floating rail on the right edge: settings menu, clubs, and aim.
struct ClubRail<MenuContent: View>: View {
    @ObservedObject var round: CourseRound
    /// Club highlighted for gesture navigation (swipe up/down), or nil when clubs are locked.
    let focusedClub: GolfClub?
    @ViewBuilder let menu: () -> MenuContent
    private enum Sheet: String, Identifiable { case shot; var id: String { rawValue } }
    @State private var sheet: Sheet?

    var body: some View {
        VStack(spacing: 7) {
            Menu { menu() } label: {
                Image(systemName: "ellipsis").font(.headline).frame(width: 45, height: 38)
            }
            .accessibilityLabel("Course menu")
            .accessibilityIdentifier("courseMenu")

            Divider().overlay(.white.opacity(0.18))

            ForEach(GolfClub.allCases) { club in
                Button { round.club = club } label: {
                    VStack(spacing: 3) {
                        Image(systemName: club.symbol).font(.system(size: 16, weight: .bold))
                        Text(club.shortName).font(.system(size: 9, weight: .black, design: .rounded))
                    }
                    .frame(width: 45, height: 46)
                    .background(round.club == club ? Palette.cream : .white.opacity(0.08), in: RoundedRectangle(cornerRadius: 11))
                    .foregroundStyle(round.club == club ? Palette.ink : Palette.cream)
                }
                .disabled(round.phase != .ready)
                .accessibilityLabel("\(club.displayName), \(Int(club.mockDistance)) yards")
                .accessibilityAddTraits(round.club == club ? .isSelected : [])
                .accessibilityIdentifier("club-\(club.rawValue)")
            }

            Divider().overlay(.white.opacity(0.18))

            VStack(spacing: 2) {
                Button { round.adjustAim(-round.aimStep) } label: {
                    Image(systemName: "arrow.left").frame(width: 45, height: 44)
                }
                .accessibilityLabel("Aim left")
                .accessibilityIdentifier("aimLeft")
                Button { round.adjustAim(round.aimStep) } label: {
                    Image(systemName: "arrow.right").frame(width: 45, height: 44)
                }
                .accessibilityLabel("Aim right")
                .accessibilityIdentifier("aimRight")
            }
            .font(.subheadline.bold())
            .disabled(round.phase != .ready)
            Text(round.combinedAim == 0 ? round.targetLabel : String(format: "%+.1f°", round.combinedAim))
                .font(.system(size: 9, weight: .black, design: .rounded)).monospacedDigit()
                .opacity(0.8)
            Button {
                round.editingShot = true
                sheet = .shot
            } label: {
                Image(systemName: "scope").frame(width: 44, height: 36)
            }
            .disabled(round.phase != .ready)
            .accessibilityLabel("Choose target and shot")
            .accessibilityIdentifier("shotControls")
        }
        .frame(width: 59)
        .padding(7)
        .background(.ultraThinMaterial.opacity(0.9), in: RoundedRectangle(cornerRadius: 18))
        .background(.black.opacity(0.35), in: RoundedRectangle(cornerRadius: 18))
        .overlay(RoundedRectangle(cornerRadius: 18).stroke(.white.opacity(0.12)))
        .environment(\.colorScheme, .dark)
        .sheet(item: $sheet, onDismiss: { round.editingShot = false }) { _ in
            ShotControls(round: round)
        }
    }
}

/// A freely chosen course target, full-circle recovery aim, and explicit short-game/shape controls.
struct ShotControls: View {
    @ObservedObject var round: CourseRound
    @Environment(\.dismiss) private var dismiss

    var body: some View {
        NavigationStack {
            Form {
                Section("Tap where you want to aim") {
                    TargetMap(round: round).frame(height: 260)
                    Text("\(round.targetLabel.capitalized) · \(Int(round.distanceToTarget.rounded())) yd")
                        .font(.headline).accessibilityIdentifier("plannedTarget")
                    Button("Follow fairway") { round.followFairway() }
                    Button("Aim at pin") { round.aimAtPin() }
                    Text("The target sets your intended line. Your stroke still controls the shot.")
                        .font(.caption).foregroundStyle(.secondary)
                    Text("Mint dots show predicted carry and roll for a centered strike. Hold your grip slightly left/right to nudge camera aim, or use the aim buttons. Aim freezes once the backswing starts.")
                        .font(.caption).foregroundStyle(.secondary)
                    Text("Full centered shots: Driver 250 yd carry · 7-iron 160 · Wedge 90 · Putter 25 yd roll. Rough, sand, partial swings and off-center contact reduce reach.")
                        .font(.caption).foregroundStyle(.secondary)
                }
                Section("Fine aim") {
                    Slider(value: Binding(get: { max(-20, min(20, round.aim)) },
                                          set: { round.setManualAim($0) }), in: -20...20, step: round.aimStep) { Text("Fine aim offset") }
                    HStack {
                        Button("Left") { round.adjustAim(-round.aimStep) }
                        Spacer()
                        Text(String(format: "%+.2f°", round.aim)).monospacedDigit()
                        Spacer()
                        Button("Right") { round.adjustAim(round.aimStep) }
                    }
                    HStack {
                        Button("Turn left 45°") { round.adjustAim(-45) }
                        Spacer()
                        Button("Turn right 45°") { round.adjustAim(45) }
                    }
                    Text("Fine slider: ±20°. Use turn buttons for recovery shots in any direction. Manual aim stays fixed for this shot; Follow fairway restores motion aim.")
                        .font(.caption).foregroundStyle(.secondary)
                }
                if round.club != .putter {
                    Section("Shot") {
                        Picker("Stroke range", selection: $round.shotType) {
                            ForEach([ShotType.full, .pitch, .chip]) { Text($0.title).tag($0) }
                        }.pickerStyle(.segmented)
                        Slider(value: $round.curve, in: -15...15, step: 1) { Text("Draw or fade") }
                        Text(round.curve == 0 ? "Straight" : round.curve < 0 ? "Draw \(Int(-round.curve))°" : "Fade \(Int(round.curve))°")
                        Text("Choose the shape here; the camera cannot measure a club face.")
                            .font(.caption).foregroundStyle(.secondary)
                    }
                } else {
                    Section("Putting") {
                        Text("Short pull, short roll. Power has the same meaning everywhere on the green.")
                    }
                }
            }
            .navigationTitle("Plan your shot")
            .toolbar { ToolbarItem(placement: .confirmationAction) { Button("Done") { dismiss() } } }
        }
    }
}

/// The gold destination stays distinct from the mint shot prediction and dogleg landing target.
struct HoleDirectionCue: View {
    @ObservedObject var round: CourseRound

    var body: some View {
        let navigation = round.holeNavigation
        HStack(spacing: 10) {
            Image(systemName: "arrow.up")
                .font(.system(size: 25 + navigation.prominence * 7, weight: .black))
                .rotationEffect(.degrees(navigation.relativeBearing))
                .frame(width: 38, height: 40)
                .accessibilityHidden(true)
            VStack(alignment: .leading, spacing: 3) {
                Label("HOLE · \(Int(navigation.distance.rounded())) YD", systemImage: "flag.fill")
                    .font(.system(size: 15 + navigation.prominence * 3, weight: .heavy, design: .rounded))
                    .minimumScaleFactor(0.8).lineLimit(1)
                Text(navigation.directionLabel)
                    .font(.system(size: 10, weight: .bold)).lineLimit(2)
            }
        }
        .foregroundStyle(.yellow)
        .padding(10)
        .background(.black.opacity(0.78), in: RoundedRectangle(cornerRadius: 14))
        .overlay(RoundedRectangle(cornerRadius: 14).stroke(.yellow.opacity(0.4 + navigation.prominence * 0.5), lineWidth: 2))
        .accessibilityElement(children: .ignore)
        .accessibilityLabel("Hole, \(Int(navigation.distance.rounded())) yards. \(navigation.directionLabel.lowercased()). Gold marks the hole; mint marks your shot.")
        .accessibilityIdentifier("holeDirectionCue")
        .allowsHitTesting(false)
    }
}

struct TargetMap: View {
    @ObservedObject var round: CourseRound
    var interactive = true

    var body: some View {
        GeometryReader { proxy in
            let points = round.hole.centerline + [round.ball, round.hole.pin]
            let minX = (points.map(\.x).min() ?? 0) - 70
            let maxX = (points.map(\.x).max() ?? 0) + 70
            let minD = (points.map(\.d).min() ?? 0) - 50
            let maxD = (points.map(\.d).max() ?? 0) + 50
            let scale = min(proxy.size.width / (maxX - minX), proxy.size.height / (maxD - minD))
            let offsetX = (proxy.size.width - (maxX - minX) * scale) / 2
            let offsetY = (proxy.size.height - (maxD - minD) * scale) / 2
            Canvas { context, _ in
                func screen(_ p: CoursePoint) -> CGPoint {
                    CGPoint(x: offsetX + (p.x - minX) * scale, y: offsetY + (maxD - p.d) * scale)
                }
                var fairway = Path()
                for (index, point) in round.hole.centerline.enumerated() {
                    if index == 0 { fairway.move(to: screen(point)) } else { fairway.addLine(to: screen(point)) }
                }
                context.stroke(fairway, with: .color(Color(red: 0.25, green: 0.40, blue: 0.17)), style: StrokeStyle(lineWidth: (round.hole.fairwayWidth + 30) * scale, lineCap: .round, lineJoin: .round))
                context.stroke(fairway, with: .color(Color(red: 0.47, green: 0.70, blue: 0.28)), style: StrokeStyle(lineWidth: round.hole.fairwayWidth * scale, lineCap: .round, lineJoin: .round))
                let green = screen(round.hole.pin), radius = round.hole.greenRadius * scale
                context.fill(Path(ellipseIn: CGRect(x: green.x - radius, y: green.y - radius, width: radius * 2, height: radius * 2)), with: .color(Color(red: 0.65, green: 0.81, blue: 0.37)))
                for hazard in round.hole.hazards {
                    let center = screen(CoursePoint(x: hazard.x, d: hazard.distance))
                    let width = hazard.width * scale, height = hazard.length * scale
                    context.fill(Path(ellipseIn: CGRect(x: center.x - width / 2, y: center.y - height / 2, width: width, height: height)), with: .color(hazard.kind == .water ? .blue : .yellow.opacity(0.7)))
                }
                let target = round.intendedTarget
                if round.phase == .ready || round.phase == .charging {
                    let preview = round.trajectoryPreview
                    var arc = Path()
                    for i in 0...48 {
                        let point = preview.position(at: preview.duration * Double(i) / 48)
                        let p = screen(CoursePoint(x: point.lateralYards, d: point.distanceYards))
                        if i == 0 { arc.move(to: p) } else { arc.addLine(to: p) }
                    }
                    context.stroke(arc, with: .color(.mint), style: StrokeStyle(lineWidth: 2, dash: [3, 3]))
                }
                var line = Path()
                line.move(to: screen(round.ball)); line.addLine(to: screen(target))
                context.stroke(line, with: .color(.white), style: StrokeStyle(lineWidth: 1, dash: [4, 4]))
                for (point, color) in [(round.hole.pin, Color.yellow), (target, Color.mint), (round.ball, Color.white)] {
                    let p = screen(point)
                    context.fill(Path(ellipseIn: CGRect(x: p.x - 4, y: p.y - 4, width: 8, height: 8)), with: .color(color))
                }
            }
            .background(Color(red: 0.10, green: 0.24, blue: 0.17), in: RoundedRectangle(cornerRadius: 12))
            .contentShape(Rectangle())
            .gesture(SpatialTapGesture().onEnded { tap in
                guard interactive else { return }
                round.selectTarget(CoursePoint(x: minX + (tap.location.x - offsetX) / scale,
                                               d: maxD - (tap.location.y - offsetY) / scale))
            })
            .accessibilityLabel("Course target map. White is the ball, yellow is the pin, mint is your target.")
            .accessibilityIdentifier(interactive ? "targetMap" : "holeOverviewMap")
        }
    }
}

/// A small, always-readable route overview; full editing remains in the shot-planning sheet.
struct HoleOverview: View {
    @ObservedObject var round: CourseRound
    private enum Panel: String, Identifiable { case plan; var id: String { rawValue } }
    @State private var panel: Panel?

    var body: some View {
        Button {
            round.editingShot = true
            panel = .plan
        } label: {
            VStack(spacing: 4) {
                TargetMap(round: round, interactive: false)
                    .frame(width: 104, height: 120).allowsHitTesting(false)
                Text("HOLE MAP").font(.system(size: 9, weight: .heavy, design: .rounded))
            }
            .padding(6)
            .background(.black.opacity(0.48), in: RoundedRectangle(cornerRadius: 14))
        }
        .buttonStyle(.plain)
        .disabled(round.phase != .ready)
        .accessibilityLabel("Plan hole. \(round.targetLabel.capitalized), \(Int(round.distanceToTarget.rounded())) yards. Pin, \(Int(round.distanceToPin.rounded())) yards.")
        .accessibilityIdentifier("holeOverview")
        .sheet(item: $panel, onDismiss: { round.editingShot = false }) { _ in ShotControls(round: round) }
    }
}
