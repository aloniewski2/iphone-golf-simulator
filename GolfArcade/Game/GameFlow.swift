import Combine
import Foundation

/// Where the app is: menus, player setup and scans, course choice, and play.
@MainActor
final class GameFlow: ObservableObject {
    enum Mode: String, Sendable { case solo, multiplayer }
    enum Screen: Equatable, Sendable { case menu, players, scan(UUID), courses, playing }

    static let maxPlayers = 4

    @Published var screen: Screen = .menu
    @Published private(set) var mode: Mode = .solo
    @Published private(set) var roster: [Player]
    @Published private(set) var course: Course = .easy

    private let defaults: UserDefaults
    private let persists: Bool

    init(defaults: UserDefaults = .standard, fixturePlayers: [Player]? = nil) {
        self.defaults = defaults
        persists = fixturePlayers == nil
        roster = fixturePlayers ?? PlayerRosterStore.load(defaults: defaults)
        if roster.isEmpty { roster = [Player(name: "Player 1", colorIndex: 0)] }
    }

    /// Who is playing this round, in turn order.
    var players: [Player] { mode == .solo ? Array(roster.prefix(1)) : roster }
    var minimumPlayers: Int { mode == .solo ? 1 : 2 }
    var canContinue: Bool { players.count >= minimumPlayers && players.allSatisfy(\.isScanned) }
    var canAddPlayer: Bool { mode == .multiplayer && roster.count < Self.maxPlayers }

    func choose(_ mode: Mode) {
        self.mode = mode
        while roster.count < minimumPlayers { addPlayer() }
        screen = .players
    }

    func addPlayer() {
        guard roster.count < Self.maxPlayers else { return }
        let used = Set(roster.map(\.colorIndex))
        let color = (0..<Self.maxPlayers).first { !used.contains($0) } ?? roster.count
        roster.append(Player(name: "Player \(roster.count + 1)", colorIndex: color, handedness: roster.first?.handedness ?? .right))
        save()
    }

    func removePlayer(_ id: UUID) {
        guard roster.count > minimumPlayers else { return }
        roster.removeAll { $0.id == id }
        save()
    }

    func rename(_ id: UUID, to name: String) {
        update(id) { $0.name = name.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty ? $0.name : name }
    }

    func setHandedness(_ id: UUID, _ handedness: Handedness) {
        update(id) { $0.handedness = handedness }
    }

    func scan(_ id: UUID) { screen = .scan(id) }

    func finishScan(_ id: UUID, calibration: PlayerCalibration) {
        update(id) { $0.calibration = calibration }
        screen = .players
    }

    func cancelScan() { screen = .players }

    func chooseCourse() {
        guard canContinue else { return }
        screen = .courses
    }

    func play(_ course: Course) {
        self.course = course
        screen = .playing
    }

    func quitToMenu() { screen = .menu }

    func player(_ id: UUID) -> Player? { roster.first { $0.id == id } }

    private func update(_ id: UUID, _ change: (inout Player) -> Void) {
        guard let index = roster.firstIndex(where: { $0.id == id }) else { return }
        change(&roster[index])
        save()
    }

    private func save() {
        guard persists else { return }
        PlayerRosterStore.save(roster, defaults: defaults)
    }
}
