import SwiftUI

struct NativeCourseScreen: View {
  @ObservedObject var flow: GameFlow
  @State private var session: GameSession
  @Environment(\.scenePhase) private var scenePhase

  init(flow: GameFlow, practice: Bool = false) {
    self.flow = flow
    var preferences = UserDefaults.standard
    #if DEBUG
    let arguments = ProcessInfo.processInfo.arguments
    if arguments.contains("-finalRoundFixture") {
      preferences = UserDefaults(suiteName: "NativeRoundFixture.\(UUID().uuidString)")!
      preferences.set("touch", forKey: "range.swingInput")
    }
    #endif
    let session = GameSession(course: practice ? .easy : flow.course, players: flow.players,
                              practice: practice, defaults: preferences)
    #if DEBUG
    if ProcessInfo.processInfo.arguments.contains("-startOnGreen") {
      session.round.dropOnGreenForTesting()
      session.round.automaticAim = ProcessInfo.processInfo.arguments.contains("-automaticAimFixture")
    }
    if arguments.contains("-finalRoundFixture") {
      session.round.prepareFinalTurnForTesting(tied: arguments.contains("-tiedRoundFixture"), finishAtStrokeCap: true)
    }
    if arguments.contains("-manualProgression") { session.round.automaticProgression = false }
    #endif
    _session = State(initialValue: session)
  }

  var body: some View {
    ControllerView(session: session, round: session.round, motion: session.motion) {
      session.stop()
      flow.quitToMenu()
    }
    .onAppear {
      session.start()
      session.setForeground(scenePhase == .active)
    }
    .onDisappear { session.stop() }
    .onChange(of: scenePhase) { _, phase in session.setForeground(phase == .active) }
  }
}

struct ControllerView: View {
  @Bindable var session: GameSession
  @ObservedObject var round: CourseRound
  @ObservedObject var motion: PhoneSwingController
  let quit: () -> Void
  @State private var display = DisplayCoordinator.shared
  @AppStorage("range.swingInput") private var swingInput: SwingInput = .phone
  @State private var sheet: Panel?
  @State private var resumeAfterPanel = false
  private enum Panel: String, Identifiable {
    case plan, scorecard, settings
    var id: String { rawValue }
  }
  private var useTouch: Bool { swingInput == .touch }

  private var external: Bool { if case .external = display.destination { true } else { false } }
  private var setupEnabled: Bool { session.canConfigureShot && round.phase == .ready }

  var body: some View {
    NavigationStack {
      GeometryReader { geometry in
        let wide = geometry.size.width > geometry.size.height * 1.2
        let layout =
          wide ? AnyLayout(HStackLayout(spacing: 0)) : AnyLayout(VStackLayout(spacing: 0))
        layout {
          if !external {
            NativeViewportHost()
              .frame(
                width: wide ? geometry.size.width * 0.56 : nil,
                height: wide ? nil : min(340, max(160, geometry.size.height * 0.42))
              )
              .clipShape(RoundedRectangle(cornerRadius: 16))
              .padding(wide ? .leading : .horizontal, 12)
              .accessibilityIdentifier("nativeViewport")
          }
          VStack(spacing: 0) {
            turnHeader
            ScrollView {
              if round.phase == .complete {
                NativeRoundResults(round: round, players: session.players).padding(16)
              } else { setupControls.padding(16) }
            }
              .accessibilityIdentifier("nativeControls")
            Divider()
            shotActions.padding(.horizontal, 16).padding(.vertical, 10)
              .background(.regularMaterial)
          }
        }
      }
      .navigationTitle(round.course.name)
      .navigationBarTitleDisplayMode(.inline)
      .toolbar {
        ToolbarItem(placement: .topBarLeading) { Button("Menu", action: quit) }
        ToolbarItem(placement: .topBarTrailing) {
          Button("Settings", systemImage: "gearshape") { present(.settings) }
        }
        ToolbarItem(placement: .topBarTrailing) {
          Button(session.paused ? "Resume" : "Pause") { session.paused.toggle() }
        }
      }
      .task(id: session.planningInput) {
        await session.preparePreview(session.planningInput)
      }
      .sheet(
        item: $sheet,
        onDismiss: {
          round.editingShot = false
          session.refreshPreferences()
          if resumeAfterPanel { session.paused = false }
          if useTouch { motion.stop() }
        }
      ) { panel in
        switch panel {
        case .plan:
          ShotControls(
            round: round, preparedPreview: session.previewShot, usesPreparedPreview: true)
        case .settings: SettingsView()
        case .scorecard: NativeScorecardPanel(round: round, players: session.players)
        }
      }
      .onChange(of: round.club) { _, club in
        session.cancelUnfinishedSwing()
        motion.setClub(club)
      }
      .onChange(of: useTouch) { _, value in
        session.cancelUnfinishedSwing()
        if value { motion.stop() } else if session.isActive { motion.start() }
      }
      .onChange(of: round.practiceMode) { _, _ in session.cancelUnfinishedSwing() }
    }
  }

  private var turnHeader: some View {
    VStack(alignment: .leading, spacing: 4) {
      HStack {
        Text(round.phase == .complete ? "Round complete" : "\(session.player.name) · Hole \(round.hole.number) · Par \(round.hole.par)").font(
          .headline)
        Spacer(minLength: 4)
        if external {
          Image(systemName: "tv").foregroundStyle(.mint).accessibilityLabel("TV connected")
        }
      }
      Text(round.phase == .complete ? "\(round.course.holes.count) holes · Par \(round.course.par)" : "\(Int(round.distanceToPin.rounded())) yd to pin · Stroke \(round.strokeNumber)")
        .font(.subheadline).foregroundStyle(.secondary)
        .accessibilityIdentifier("nativeTurnStatus")
    }.padding(.horizontal, 16).padding(.vertical, 10)
  }

  private var setupControls: some View {
    VStack(alignment: .leading, spacing: 16) {
      if !session.assetsReady {
        Label(session.assetStatus, systemImage: "shippingbox")
          .accessibilityIdentifier("nativeAssetStatus")
        Text("Play becomes available when the course and golfer finish loading.")
          .font(.caption).foregroundStyle(.secondary)
      }
      Button(
        session.flyoverStarted == nil ? "View hole flyover" : "Finish flyover",
        systemImage: "binoculars"
      ) {
        if session.flyoverStarted == nil { session.startFlyover() } else { session.finishFlyover() }
      }
      .disabled(!session.assetsReady || !session.isActive || round.phase != .ready)
      .accessibilityIdentifier("nativeFlyover")
      HStack {
        Button("Plan shot", systemImage: "map") { present(.plan) }
          .disabled(!setupEnabled).accessibilityIdentifier("nativePlanShot")
        Spacer()
        Button("Scorecard", systemImage: "list.number") { present(.scorecard) }
          .accessibilityIdentifier("nativeScorecard")
      }
      Text("\(round.targetLabel) · \(Int(round.distanceToTarget.rounded())) yd")
      if round.phase == .ready && session.needsPuttRecommendation {
        Label("Reading green…", systemImage: "clock")
          .font(.caption).accessibilityIdentifier("nativePuttPlanning")
      }
      Text("Lie: \(round.lie.displayName) · Wind \(Int(round.wind.speedMPH.rounded())) mph")
        .font(.caption).accessibilityIdentifier("nativeLieAndWind")
      if round.wind.speedMPH >= 0.5 {
        Text(windDescription).font(.caption).foregroundStyle(.secondary)
      }
      if let preview = session.previewShot {
        Text(
          "Suggested strength \(Int((preview.request.execution.power * 100).rounded()))% · centered strike"
        )
        .font(.caption).accessibilityIdentifier("nativeSuggestedPower")
      }
      if let read = round.greenRead {
        Text(read.label).foregroundStyle(.mint).accessibilityIdentifier("greenRead")
      }
      Toggle("Show trajectory guide", isOn: $session.showTrajectory)
      Group {
        Picker("Club", selection: $round.club) {
          ForEach(GolfClub.allCases) { club in Text(club.shortName).tag(club) }
        }.pickerStyle(.menu)
        HStack {
          Button("Aim left", systemImage: "arrow.left") { round.adjustAim(-round.aimStep) }
          Spacer()
          Text(String(format: "Aim %.1f°", round.combinedAim)).monospacedDigit()
          Spacer()
          Button("Aim right", systemImage: "arrow.right") { round.adjustAim(round.aimStep) }
        }
        if !round.automaticAim {
          HStack {
            Button("Aim at pin") { round.aimAtPin() }
            Spacer()
            Button("Follow fairway") { round.followFairway() }
          }
        }
        Picker("Shot type", selection: $round.shotType) {
          ForEach(ShotType.allCases.filter { $0.supports(club: round.club, lie: round.lie) }) {
            Text($0.title).tag($0)
          }
        }
        if round.club != .putter {
          Picker("Trajectory", selection: $round.trajectory) {
            ForEach(ShotTrajectory.allCases) { Text($0.title).tag($0) }
          }.pickerStyle(.segmented).disabled(round.shotType == .chip)
          Picker("Shape", selection: $round.shotShape) {
            ForEach(ShotShapeChoice.allCases) { Text($0.title).tag($0) }
          }.pickerStyle(.segmented).disabled(round.shotType != .full)
        }
        Toggle("Practice swing (no stroke)", isOn: $round.practiceMode)
          .accessibilityIdentifier("nativePracticeMode")
      }.disabled(!setupEnabled)
      if let impact = round.practiceImpact {
        Text("Practice swing · \(Int(impact.power * 100))% power · no stroke counted")
          .font(.caption).accessibilityIdentifier("practiceResult")
      }
      Toggle(
        "Touch controls",
        isOn: Binding(get: { useTouch }, set: { swingInput = $0 ? .touch : .phone })
      )
      .disabled(motion.isArmed || session.isPreparingShot)
      .accessibilityIdentifier("nativeTouchControls")
      if !useTouch {
        Text("Short, gentle swings only. Keep a secure grip; a wrist strap is recommended. No camera needed.")
          .font(.caption).foregroundStyle(.secondary).accessibilityIdentifier("nativeSwingSafety")
      }
      Toggle("External display", isOn: $display.enabled)
    }
  }

  private var shotActions: some View {
    VStack(alignment: .leading, spacing: 8) {
      if round.phase == .ready {
        if useTouch {
          HStack {
            Text("Power").font(.caption)
            Slider(value: $session.touchPower, in: 0.02...1) { Text("Power") }
              .disabled(!session.canSwing)
              .accessibilityIdentifier("nativeTouchPower")
          }
          Button("Swing \(Int(session.touchPower * 100))%") { session.swingUsingTouch() }
            .buttonStyle(.borderedProminent).controlSize(.large).disabled(!session.canSwing)
            .accessibilityIdentifier("nativeTouchSwing")
        } else {
          Text(motionStatus).font(.caption).accessibilityIdentifier("nativeMotionStatus")
          Button(motion.isArmed ? "Cancel" : "Ready") {
            if motion.isArmed { session.cancelUnfinishedSwing() } else { session.arm() }
          }.buttonStyle(.borderedProminent).controlSize(.large)
            .disabled(!motion.isArmed && (!session.canSwing || !motion.isAvailable))
        }
      }
      if round.phase == .flying {
        Button("Skip flight") { round.skipFlight(at: session.date) }
      }
      if round.phase == .landed || round.phase == .holed {
        if let shot = round.activeShot { NativeShotSummary(round: round, shot: shot) }
        HStack {
          Button("Replay") { session.replay() }
          Button(round.phase == .holed ? (round.isLastTurn ? "Final scores" : "Continue") : "Next shot") { session.next() }
        }
        if let progressionHint {
          Text(progressionHint).font(.caption).foregroundStyle(.mint)
            .accessibilityIdentifier("nativeAutoProgression")
        }
      }
      if round.phase == .complete {
        HStack {
          Button("Menu", action: quit).accessibilityIdentifier("nativeResultsMenu")
          Spacer()
          Button("Play again") { session.restart() }
            .buttonStyle(.borderedProminent).accessibilityIdentifier("nativePlayAgain")
        }
      }
    }.frame(maxWidth: .infinity, alignment: .leading)
  }

  private func present(_ panel: Panel) {
    session.cancelUnfinishedSwing()
    resumeAfterPanel = !session.paused
    session.paused = true
    round.editingShot = panel == .plan
    sheet = panel
  }

  private var progressionHint: String? {
    guard round.automaticProgression else { return nil }
    guard round.nextShotAt != nil else {
      return round.isReplay ? "Replay paused auto-advance · continue when ready." : nil
    }
    if session.paused { return "Auto-advance paused" }
    if round.phase == .landed { return "Next shot and club after 3 seconds · Replay pauses auto-advance." }
    let next = round.isLastTurn ? "Final scores" : round.playerIndex < round.playerCount - 1 ? "Next player" : "Next hole"
    return "\(next) after 5 seconds · Replay pauses auto-advance."
  }

  private var windDescription: String {
    let wind = round.wind.local(to: round.heading + round.combinedAim)
    let along = "\(Int((abs(wind.z) / 0.44704).rounded())) mph \(wind.z >= 0 ? "tailwind" : "headwind")"
    let across = "\(Int((abs(wind.x) / 0.44704).rounded())) mph toward your \(wind.x >= 0 ? "right" : "left")"
    return "Relative to aim: \(along) · \(across)"
  }

  private var motionStatus: String {
    switch motion.status {
    case .unavailable: "Motion is unavailable on this device. Use touch controls."
    case .idle: "Choose club and aim, then tap Ready."
    case .settling: "Hold still to set your address."
    case .address: "Ready for your swing."
    case .backswing: "Backswing"
    case .downswing: "Downswing"
    case .followThrough: "Finish your follow-through."
    }
  }
}

private struct NativeRoundResults: View {
  @ObservedObject var round: CourseRound
  let players: [Player]

  var body: some View {
    VStack(alignment: .leading, spacing: 12) {
      Text(headline).font(.title2.bold()).accessibilityIdentifier("roundComplete")
      ForEach(players.indices, id: \.self) { index in
        Text("\(players[index].name): \(round.total(for: index)) strokes · \(ScoreFormat.toPar(round.toPar(for: index)))")
          .accessibilityIdentifier("nativeRoundTotal-\(index)")
      }
      if players.count == 1, let best = round.best {
        Text("Best: \(ScoreFormat.toPar(best))").foregroundStyle(.mint)
          .accessibilityIdentifier("nativeRoundBest")
      }
      ScrollView(.horizontal) { ScorecardView(round: round, players: players) }
    }.frame(maxWidth: .infinity, alignment: .leading)
  }

  private var headline: String {
    guard players.count > 1 else { return "Round complete" }
    let best = players.indices.min { round.total(for: $0) < round.total(for: $1) } ?? 0
    let tied = players.indices.filter { round.total(for: $0) == round.total(for: best) }.count > 1
    return tied ? "It's a tie" : "\(players[best].name) wins"
  }
}

private struct NativeShotSummary: View {
  @ObservedObject var round: CourseRound
  let shot: RangeShot

  var body: some View {
    VStack(alignment: .leading, spacing: 3) {
      if round.phase == .holed {
        let strokes = round.scores[round.playerIndex][round.holeIndex] ?? round.strokes
        Text(round.pickedUp ? "Picked up" : ScoreFormat.holeName(strokes: strokes, par: round.hole.par))
          .font(.headline).accessibilityIdentifier("holeResult")
        Text("\(strokes) \(strokes == 1 ? "stroke" : "strokes") · Par \(round.hole.par) · Total \(ScoreFormat.toPar(round.toPar(for: round.playerIndex)))")
          .font(.caption)
      } else {
        Text("\(shot.lie?.displayName ?? "In play") · \(Int(shot.total.rounded())) yd")
          .font(.headline).foregroundStyle(shot.penaltyStrokes > 0 ? .orange : .primary)
          .accessibilityIdentifier("shotLie")
        Text(shot.explanation ?? "\(shot.strike.displayName) contact")
          .font(.caption).foregroundStyle(.secondary).accessibilityIdentifier("nativeShotExplanation")
      }
    }
  }
}

private struct NativeScorecardPanel: View {
  @ObservedObject var round: CourseRound
  let players: [Player]
  @Environment(\.dismiss) private var dismiss
  var body: some View {
    NavigationStack {
      ScrollView(.horizontal) { ScorecardView(round: round, players: players).padding() }
        .navigationTitle("Scorecard")
        .toolbar { ToolbarItem(placement: .confirmationAction) { Button("Done") { dismiss() } } }
    }.preferredColorScheme(.dark)
  }
}
