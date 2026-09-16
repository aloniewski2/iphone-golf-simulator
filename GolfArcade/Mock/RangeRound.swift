import Combine
import Foundation

/// What the player is on: the five-shot target range, or a hole played from tee to cup.
enum GameMode: Equatable, Sendable {
    case range
    case hole(Hole)

    var hole: Hole? { if case .hole(let hole) = self { hole } else { nil } }
}

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
    @Published private(set) var mode: GameMode
    /// Where the ball lies now (course modes). On the range it is always the tee.
    @Published private(set) var ballPosition = CoursePoint(x: 0, z: 0)
    @Published private(set) var lie: Lie = .tee
    /// Most strokes allowed on a hole before the ball is picked up.
    let strokeLimit = 10
    let shotLimit = 5
    private let defaults: UserDefaults
    private var pausedAt: Date?

    init(mode: GameMode = .range, defaults: UserDefaults = .standard) {
        self.mode = mode
        self.defaults = defaults
        best = defaults.integer(forKey: Self.bestKey(for: mode))
        if let hole = mode.hole { ballPosition = hole.tee; club = .driver }
    }

    // MARK: Read-outs

    var hole: Hole? { mode.hole }
    /// Range: points earned. Hole: strokes taken.
    var score: Int { hole == nil ? shots.reduce(0) { $0 + $1.points } : shots.count }
    var strokes: Int { shots.count }
    var canSwing: Bool { phase == .ready || phase == .charging }
    var shotNumber: Int { min(shots.count + (canSwing ? 1 : 0), hole == nil ? shotLimit : strokeLimit) }
    var isHoled: Bool { activeShot?.isHoled == true && phase == .complete }
    var yardsToPin: Double { hole.map { ballPosition.distance(to: $0.cup) } ?? 0 }
    /// Compass heading the aim slider is relative to: straight downrange on the range, at the pin on a hole.
    var baseHeading: Double { hole.map { ballPosition.heading(to: $0.cup) } ?? 0 }
    var shotHeading: Double { baseHeading }

    // MARK: Swinging

    func charge(_ value: Double) {
        guard canSwing else { return }
        power = min(max(value, 0), 1)
        phase = .charging
    }

    @discardableResult
    func release(at date: Date = .now, curve: Double = 0) -> Bool {
        guard phase == .charging, power >= 0.06, shots.count < (hole == nil ? shotLimit : strokeLimit) else {
            cancelCharge()
            return false
        }
        let shot: RangeShot
        if let hole {
            shot = RangeShot(id: shots.count + 1, club: club, power: power, aim: aim, curve: curve, from: ballPosition, heading: baseHeading, lie: lie, on: hole)
        } else {
            shot = RangeShot(id: shots.count + 1, club: club, power: power, aim: aim, curve: curve)
        }
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
        if isReplay {
            phase = hasFinished ? .complete : .landed
            return
        }
        if let hole {
            ballPosition = shot.restingPoint
            lie = shot.isHoled ? .green : hole.lie(at: ballPosition)
            if lie == .green, !shot.isHoled { club = .putter }
            phase = shot.isHoled || shots.count == strokeLimit ? .complete : .landed
            if phase == .complete, shot.isHoled, best == 0 || score < best {
                best = score
                defaults.set(best, forKey: Self.bestKey(for: mode))
            }
        } else {
            phase = shots.count == shotLimit ? .complete : .landed
            if phase == .complete, score > best {
                best = score
                defaults.set(best, forKey: Self.bestKey(for: mode))
            }
        }
    }

    private var hasFinished: Bool {
        if hole != nil { return activeShot?.isHoled == true || shots.count == strokeLimit }
        return shots.count == shotLimit
    }

    func nextShot() {
        guard phase == .landed else { return }
        activeShot = nil
        flightStart = nil
        power = 0
        aim = 0
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
        if let hole {
            ballPosition = hole.tee
            lie = .tee
            club = .driver
        }
    }

    func switchMode(_ newMode: GameMode) {
        guard newMode != mode else { return }
        mode = newMode
        best = defaults.integer(forKey: Self.bestKey(for: newMode))
        restart()
        if newMode.hole == nil { club = .iron }
    }

    func pause(at date: Date = .now) { cancelCharge(); pausedAt = date }
    func resume(at date: Date = .now) {
        if let pausedAt, let flightStart { self.flightStart = flightStart.addingTimeInterval(date.timeIntervalSince(pausedAt)) }
        pausedAt = nil
    }

    private static func bestKey(for mode: GameMode) -> String {
        switch mode {
        case .range: "range.bestScore"
        case .hole(let hole): "hole\(hole.number).bestStrokes"
        }
    }
}
