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

            ScrollViewReader { proxy in
            ScrollView(.vertical) {
            VStack(spacing: 7) { ForEach(GolfClub.allCases) { club in
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
                .accessibilityLabel("\(club.displayName), \(Int(club.referenceDistanceYards)) yards \(club == .putter ? "roll" : "carry")")
                .accessibilityAddTraits(round.club == club ? .isSelected : [])
                .accessibilityIdentifier("club-\(club.rawValue)")
                .id(club)
            } }
            }.frame(height: 258).scrollIndicators(.hidden)
            .onAppear { proxy.scrollTo(round.club, anchor: .center) }
            .onChange(of: round.club) { _, club in proxy.scrollTo(club, anchor: .center) }
            }

            Divider().overlay(.white.opacity(0.18))

            if !round.automaticAim { VStack(spacing: 2) {
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
            }
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
    var preparedPreview: RangeShot?
    var usesPreparedPreview = false
    @Environment(\.dismiss) private var dismiss

    var body: some View {
        NavigationStack {
            Form {
                Section("Swing mode") {
                    Toggle("Practice without scoring", isOn: $round.practiceMode)
                        .accessibilityIdentifier("practiceMode")
                    Text("Practice shows your motion and strength without moving the ball or spending a stroke. Turn it off to play.")
                        .font(.caption).foregroundStyle(.secondary)
                }
                Section(round.automaticAim ? "Recommended route" : "Tap where you want to aim") {
                    TargetMap(round: round, interactive: !round.automaticAim,
                              preparedPreview: preparedPreview, usesPreparedPreview: usesPreparedPreview).frame(height: 260)
                    Text("\(round.targetLabel.capitalized) · \(Int(round.distanceToTarget.rounded())) yd")
                        .font(.headline).accessibilityIdentifier("plannedTarget")
                    if !round.automaticAim {
                    Button("Follow fairway") { round.followFairway() }
                    Button("Aim at pin") { round.aimAtPin() }
                    }
                    Text("The target sets your intended line. Your stroke still controls the shot.")
                        .font(.caption).foregroundStyle(.secondary)
                    Text("Mint shows predicted carry and roll for a centered strike. Automatic aim follows safe landing areas; on the green it searches for a starting line and pace using the same slope physics as your putt. It is a recommendation, not a guaranteed make. Your swing controls power and contact; the ball is never steered toward the cup.")
                        .font(.caption).foregroundStyle(.secondary)
                    Text("Full centered carry: Driver 250 · 3W 210 · 5I 180 · 7I 160 · 9I 135 · SW 90 yards. Putter: 25 yards of level-green roll. Wind, lie and strike change the result.")
                        .font(.caption).foregroundStyle(.secondary)
                }
                if !round.automaticAim { Section("Fine aim") {
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
                    Text("Fine slider: ±20°. Use turn buttons for recovery shots in any direction. Manual aim stays fixed for this shot; Follow fairway restores the recommended target.")
                        .font(.caption).foregroundStyle(.secondary)
                }
                }
                if round.club != .putter {
                    Section("Shot") {
                        Picker("Stroke range", selection: $round.shotType) {
                            ForEach(ShotType.allCases.filter { $0.supports(club:round.club,lie:round.lie) }) { Text($0.title).tag($0) }
                        }.pickerStyle(.segmented)
                        Picker("Shape", selection: $round.shotShape) {
                            ForEach(ShotShapeChoice.allCases) { Text($0.title).tag($0) }
                        }.pickerStyle(.segmented).disabled(round.shotType != .full)
                            .accessibilityIdentifier("shotShape")
                        Picker("Trajectory", selection: $round.trajectory) {
                            ForEach(ShotTrajectory.allCases) { Text($0.title).tag($0) }
                        }.pickerStyle(.segmented).disabled(round.shotType == .chip)
                            .accessibilityIdentifier("shotTrajectory")
                        Text("Choose shape and trajectory here, including with automatic aim. Draw/fade follows your handedness; these are selected effects, not measured club-face angles.")
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
    var preparedPreview: RangeShot?
    var usesPreparedPreview = false

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
                func polygon(_ region: CourseRegion) -> Path {
                    var path = Path()
                    for (index,point) in region.points.enumerated() {
                        if index == 0 { path.move(to:screen(point)) } else { path.addLine(to:screen(point)) }
                    }
                    path.closeSubpath(); return path
                }
                for (index, point) in round.hole.centerline.enumerated() {
                    if index == 0 { fairway.move(to: screen(point)) } else { fairway.addLine(to: screen(point)) }
                }
                if let boundary = round.hole.fairwayBoundary {
                    let shape = polygon(boundary)
                    context.stroke(shape,with:.color(Color(red:0.25,green:0.40,blue:0.17)),lineWidth:24*scale)
                    context.fill(shape,with:.color(Color(red:0.47,green:0.70,blue:0.28)))
                } else {
                    context.stroke(fairway, with: .color(Color(red: 0.25, green: 0.40, blue: 0.17)), style: StrokeStyle(lineWidth: (round.hole.fairwayWidth + 30) * scale, lineCap: .round, lineJoin: .round))
                    context.stroke(fairway, with: .color(Color(red: 0.47, green: 0.70, blue: 0.28)), style: StrokeStyle(lineWidth: round.hole.fairwayWidth * scale, lineCap: .round, lineJoin: .round))
                }
                let green = screen(round.hole.pin), radius = round.hole.greenRadius * scale
                let greenShape = round.hole.greenBoundary.map(polygon) ?? Path(ellipseIn: CGRect(x: green.x - radius, y: green.y - radius, width: radius * 2, height: radius * 2))
                context.stroke(greenShape, with: .color(Color(red: 0.36, green: 0.58, blue: 0.27)), lineWidth: max(2, 2 * scale))
                context.fill(greenShape, with: .color(Color(red: 0.65, green: 0.81, blue: 0.37)))
                var mowing = context
                mowing.clip(to: greenShape)
                for band in -4...4 {
                    let y = green.y + CGFloat(band) * radius / 4
                    mowing.fill(Path(CGRect(x: green.x - radius, y: y, width: radius * 2, height: radius / 8)),
                        with: .color(.white.opacity(0.12)))
                }
                for hazard in round.hole.hazards {
                    var shape = Path()
                    for vertex in 0..<96 {
                        let angle=Double(vertex)/96 * 2 * Double.pi
                        let radius=hazard.boundaryScale(at:angle)
                        let p=screen(CoursePoint(x:hazard.x+cos(angle)*hazard.width/2*radius,
                            d:hazard.distance+sin(angle)*hazard.length/2*radius))
                        if vertex == 0 { shape.move(to:p) } else { shape.addLine(to:p) }
                    }
                    shape.closeSubpath()
                    context.stroke(shape, with: .color(hazard.kind == .water ? Color(red: 0.33, green: 0.75, blue: 0.76) : Color(red: 0.58, green: 0.48, blue: 0.30)), lineWidth: 2)
                    context.fill(shape, with: .color(hazard.kind == .water ? Color(red: 0.13, green: 0.48, blue: 0.61) : Color(red: 0.93, green: 0.85, blue: 0.64)))
                }
                for tree in round.hole.trees {
                    let p=screen(tree.center)
                    let radius=max(2,tree.crownRadius*scale)
                    context.fill(Path(ellipseIn:CGRect(x:p.x-radius,y:p.y-radius,width:radius*2,height:radius*2)),with:.color(.green.opacity(0.85)))
                }
                let target = round.intendedTarget
                if round.automaticAim {
                    var route = Path()
                    for (index, point) in round.hole.recommendedRoute(from: round.ball).enumerated() {
                        if index == 0 { route.move(to: screen(point)) }
                        else { route.addLine(to: screen(point)) }
                    }
                    context.stroke(route, with: .color(.yellow.opacity(0.8)), style: StrokeStyle(lineWidth: 2, dash: [6, 4]))
                }
                if (round.phase == .ready || round.phase == .charging),
                   let preview = usesPreparedPreview ? preparedPreview : round.trajectoryPreview {
                    for i in 1...48 {
                        let before=preview.position(at:preview.duration*Double(i-1)/48)
                        let point=preview.position(at:preview.duration*Double(i)/48)
                        var segment=Path()
                        segment.move(to:screen(CoursePoint(x:before.lateralYards,d:before.distanceYards)))
                        segment.addLine(to:screen(CoursePoint(x:point.lateralYards,d:point.distanceYards)))
                        context.stroke(segment,with:.color(point.heightYards > 0.03 ? .mint : .orange),
                            style:StrokeStyle(lineWidth:2,dash:point.heightYards > 0.03 ? [] : [2,2]))
                    }
                }
                var line = Path()
                line.move(to: screen(round.ball)); line.addLine(to: screen(target))
                context.stroke(line, with: .color(.white), style: StrokeStyle(lineWidth: 1, dash: [4, 4]))
                for (point, color) in [(round.hole.pin, Color.yellow), (target, Color.mint), (round.ball, Color.white)] {
                    let p = screen(point)
                    context.fill(Path(ellipseIn: CGRect(x: p.x - 6, y: p.y - 6, width: 12, height: 12)), with: .color(.black.opacity(0.65)))
                    context.fill(Path(ellipseIn: CGRect(x: p.x - 4, y: p.y - 4, width: 8, height: 8)), with: .color(color))
                }
                let pin = screen(round.hole.pin)
                var flag = Path()
                flag.move(to: pin); flag.addLine(to: CGPoint(x: pin.x, y: pin.y - 18))
                context.stroke(flag, with: .color(.white), lineWidth: 2)
                var pennant = Path()
                pennant.move(to: CGPoint(x: pin.x, y: pin.y - 18))
                pennant.addLine(to: CGPoint(x: pin.x + 10, y: pin.y - 14))
                pennant.addLine(to: CGPoint(x: pin.x, y: pin.y - 10)); pennant.closeSubpath()
                context.fill(pennant, with: .color(.yellow))
            }
            .background(Color(red: 0.10, green: 0.24, blue: 0.17), in: RoundedRectangle(cornerRadius: 12))
            .contentShape(Rectangle())
            .gesture(SpatialTapGesture().onEnded { tap in
                guard interactive else { return }
                round.selectTarget(CoursePoint(x: minX + (tap.location.x - offsetX) / scale,
                                               d: maxD - (tap.location.y - offsetY) / scale))
            })
            .accessibilityLabel("Course target map. White is the ball, yellow is the route and pin, mint is predicted flight, orange is predicted roll.")
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
