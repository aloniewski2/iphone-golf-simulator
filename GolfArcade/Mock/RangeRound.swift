import Combine
import Foundation

@MainActor
final class RangeRound: ObservableObject {
    enum Phase { case ready, charging, flying, landed, complete }
    @Published private(set) var phase: Phase = .ready
    @Published var club: GolfClub = .iron
    @Published var aim = 0.0
    @Published private(set) var power = 0.0
    @Published private(set) var shots: [RangeShot] = []
    @Published private(set) var activeShot: RangeShot?
    @Published private(set) var flightStart: Date?
    @Published private(set) var isReplay = false
    @Published private(set) var best: Int
    let shotLimit = 5
    private let defaults: UserDefaults
    private var pausedAt: Date?

    init(defaults: UserDefaults = .standard) {
        self.defaults = defaults
        best = defaults.integer(forKey: "range.bestScore")
    }

    var score: Int { shots.reduce(0) { $0 + $1.points } }
    var canSwing: Bool { phase == .ready || phase == .charging }
    var shotNumber: Int { min(shots.count + (canSwing ? 1 : 0), shotLimit) }

    func charge(_ value: Double) {
        guard canSwing else { return }
        power = min(max(value, 0), 1)
        phase = .charging
    }

    @discardableResult
    func release(at date: Date = .now, curve: Double = 0) -> Bool {
        guard phase == .charging, power >= 0.06, shots.count < shotLimit else {
            cancelCharge()
            return false
        }
        let shot = RangeShot(id: shots.count + 1, club: club, power: power, aim: aim, curve: curve)
        activeShot = shot
        shots.append(shot)
        isReplay = false
        flightStart = date
        phase = .flying
        return true
    }

    func cancelCharge() {
        guard phase == .charging else { return }
        power = 0
        phase = .ready
    }

    func elapsed(at date: Date) -> Double {
        guard let start = flightStart else { return 0 }
        return max(0, (pausedAt ?? date).timeIntervalSince(start))
    }

    func advance(at date: Date) {
        guard phase == .flying, let shot = activeShot, elapsed(at: date) >= shot.duration else { return }
        phase = shots.count == shotLimit ? .complete : .landed
        if phase == .complete, score > best {
            best = score
            defaults.set(best, forKey: "range.bestScore")
        }
    }

    func nextShot() {
        guard phase == .landed else { return }
        activeShot = nil
        flightStart = nil
        power = 0
        isReplay = false
        phase = .ready
    }

    func replay(at date: Date = .now) {
        guard (phase == .landed || phase == .complete), activeShot != nil else { return }
        isReplay = true
        flightStart = date
        phase = .flying
    }

    func restart() {
        shots = []
        activeShot = nil
        flightStart = nil
        pausedAt = nil
        power = 0
        aim = 0
        isReplay = false
        phase = .ready
    }

    func pause(at date: Date = .now) { cancelCharge(); pausedAt = date }
    func resume(at date: Date = .now) {
        if let pausedAt, let flightStart { self.flightStart = flightStart.addingTimeInterval(date.timeIntervalSince(pausedAt)) }
        pausedAt = nil
    }
}
