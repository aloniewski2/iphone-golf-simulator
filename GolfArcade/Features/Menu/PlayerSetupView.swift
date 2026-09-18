import SwiftUI

struct PlayerSetupView: View {
    @ObservedObject var flow: GameFlow
    @ObservedObject var camera: CameraSwingController
    @AppStorage("gestures.enabled") private var gesturesEnabled = true
    @State private var focus = 0
    @State private var editingGolfer: Player?

    /// Gesture focus runs over each player row, then Add (multiplayer), Continue, Back.
    private enum Target: Equatable { case player(Int), add, proceed, back }

    private var targets: [Target] {
        flow.players.indices.map(Target.player) + (flow.canAddPlayer ? [.add] : []) + [.proceed, .back]
    }

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 20) {
                Button { flow.quitToMenu() } label: { Label("Menu", systemImage: "chevron.left") }
                    .font(.subheadline.bold())
                    .foregroundStyle(isFocused(.back) ? .mint : Palette.cream)
                    .accessibilityIdentifier("backToMenu")
                VStack(alignment: .leading, spacing: 6) {
                    Text(flow.mode == .solo ? "SOLO" : "MULTIPLAYER")
                        .font(.system(size: 12, weight: .black, design: .rounded)).tracking(2)
                        .foregroundStyle(.mint)
                    Text(flow.mode == .solo ? "Who's playing?" : "Scan every player")
                        .font(.system(size: 30, weight: .black, design: .rounded))
                    Text("Scan and play with your chest facing the phone. Select your handedness so the golfer and swing direction match you.")
                        .font(.caption).foregroundStyle(.white.opacity(0.6))
                }

                VStack(spacing: 12) {
                    ForEach(Array(flow.players.enumerated()), id: \.element.id) { index, player in
                        playerRow(player, focused: isFocused(.player(index)))
                    }
                }

                Text("Same camera position for everyone. Right-handed: backswing to your right, through to your left. Left-handed: backswing to your left, through to your right. The in-game golfer mirrors for left-handed play.")
                    .font(.callout).foregroundStyle(.white.opacity(0.8))
                    .accessibilityIdentifier("cameraStanceInstructions")

                if flow.canAddPlayer {
                    MenuRow(focused: isFocused(.add), action: flow.addPlayer) {
                        Label("Add player", systemImage: "plus.circle.fill").font(.headline).foregroundStyle(.mint)
                    }
                    .accessibilityIdentifier("addPlayer")
                }

                Button { flow.chooseCourse() } label: {
                    Text(flow.canContinue ? "Choose a course" : "Scan \(flow.mode == .solo ? "your body" : "every player") to continue")
                        .font(.headline)
                        .frame(maxWidth: .infinity).padding(.vertical, 15)
                        .background(flow.canContinue ? Color.mint : .white.opacity(0.12), in: RoundedRectangle(cornerRadius: 15))
                        .foregroundStyle(flow.canContinue ? Palette.ink : .white.opacity(0.45))
                        .overlay(RoundedRectangle(cornerRadius: 15).stroke(isFocused(.proceed) ? Palette.cream : .clear, lineWidth: 2.5))
                }
                .disabled(!flow.canContinue)
                .accessibilityIdentifier("continueToCourses")

                if gesturesEnabled { GestureStatusChip(camera: camera) }
            }
            .padding(24)
            .frame(maxWidth: 620)
            .frame(maxWidth: .infinity)
        }
        .background(Palette.background.ignoresSafeArea())
        .accessibilityIdentifier("playerSetup")
        .sheet(item:$editingGolfer) { player in GolferAppearanceEditor(flow:flow,player:player) }
        .onNavGesture(camera) { gesture in
            let count = targets.count
            let current = min(focus, count - 1)
            switch gesture {
            case .up: focus = (current + count - 1) % count
            case .down: focus = (current + 1) % count
            case .left, .right:
                if case .player(let index) = targets[current] {
                    let player = flow.players[index]
                    flow.setHandedness(player.id, player.handedness == .right ? .left : .right)
                }
            case .select: activate(targets[current])
            }
        }
    }

    private func isFocused(_ target: Target) -> Bool {
        guard !targets.isEmpty else { return false }
        return targets[min(focus, targets.count - 1)] == target
    }

    private func activate(_ target: Target) {
        switch target {
        case .player(let index): flow.scan(flow.players[index].id)
        case .add: flow.addPlayer()
        case .proceed: flow.chooseCourse()
        case .back: flow.quitToMenu()
        }
    }

    private func playerRow(_ player: Player, focused: Bool) -> some View {
        HStack(spacing: 12) {
            Circle().fill(Palette.player(player.colorIndex)).frame(width: 14, height: 14)
            VStack(alignment: .leading, spacing: 6) {
                TextField("Name", text: Binding(get: { player.name }, set: { flow.rename(player.id, to: $0) }))
                    .font(.headline)
                    .textInputAutocapitalization(.words)
                    .submitLabel(.done)
                Label(player.isScanned ? "Scanned" : "Not scanned", systemImage: player.isScanned ? "checkmark.circle.fill" : "exclamationmark.circle")
                    .font(.caption.bold())
                    .foregroundStyle(player.isScanned ? .mint : .yellow)
                    .fixedSize(horizontal: true, vertical: false)
                HStack(spacing: 8) {
                    Picker("Handedness", selection: Binding(get: { player.handedness }, set: { flow.setHandedness(player.id, $0) })) {
                        Text("Right").tag(Handedness.right)
                        Text("Left").tag(Handedness.left)
                    }
                    .pickerStyle(.segmented)
                    .frame(width: 120)
                }
                .font(.caption.bold())
                Button { editingGolfer=player } label: {
                    Label("Golfer · \(player.golferAppearance.preset.title)",systemImage:"person.crop.square")
                        .font(.caption.bold())
                }.accessibilityIdentifier("editGolfer-\(player.name)")
            }
            Spacer(minLength: 4)
            Button(player.isScanned ? "Rescan" : "Scan") { flow.scan(player.id) }
                .buttonStyle(.borderedProminent)
                .tint(player.isScanned ? .gray : .mint)
                .accessibilityIdentifier("scan-\(player.name)")
            if flow.mode == .multiplayer, flow.roster.count > flow.minimumPlayers {
                Button(role: .destructive) { flow.removePlayer(player.id) } label: { Image(systemName: "trash") }
                    .accessibilityLabel("Remove \(player.name)")
            }
        }
        .padding(14)
        .background(.white.opacity(focused ? 0.14 : 0.06), in: RoundedRectangle(cornerRadius: 18))
        .overlay(RoundedRectangle(cornerRadius: 18).stroke(focused ? Color.mint : .white.opacity(0.09), lineWidth: focused ? 2.5 : 1))
    }
}
