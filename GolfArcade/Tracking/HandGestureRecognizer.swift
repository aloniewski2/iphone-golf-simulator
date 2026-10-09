import CoreGraphics
import Foundation

/// Menu navigation from the camera.
enum NavGesture: String, Equatable, Sendable {
    case left, right, up, down, select

    var symbol: String {
        switch self {
        case .left: "arrow.left"
        case .right: "arrow.right"
        case .up: "arrow.up"
        case .down: "arrow.down"
        case .select: "hand.raised.fill"
        }
    }
}

enum HandShape: Equatable, Sendable {
    case fist, open, unknown
}

/// A hand-pose reading attached to one of the body's wrists.
struct HandReading: Equatable, Sendable {
    let wrist: BodyJoint
    let shape: HandShape
    /// Fingers held straight out (index to little), when all four could be read.
    var fingers: Int? = nil
}

/// Turns deliberate one-handed motions into `NavGesture`s.
///
/// A hand **arms** when it is held as a fist (or, when the camera is too far to read fingers,
/// raised to shoulder height) for a moment, apart from the other hand. A golf grip keeps both
/// wrists together, so an address or swing never arms. From armed:
/// - **swipe**: the wrist travels about a shoulder width in one direction within half a second;
/// - **punch**: the arm drives straight at the camera, so the wrist collapses onto the shoulder
///   in the 2D image without travelling sideways.
///
/// All distances are in shoulder widths, so it works at any distance from the camera.
struct HandGestureRecognizer {
    /// Seconds a hand must hold the arming pose.
    var armDuration = 0.2
    /// Seconds a fist stays armed after it is lost (fast motion blurs the fingers).
    var armGrace = 0.5
    /// Wrists closer than this are gripping together, not gesturing.
    var gripDistance: CGFloat = 0.8
    /// Swipe length and time window.
    var swipeDistance: CGFloat = 0.9
    var swipeWindow = 0.45
    /// The cross axis must stay under this share of the swipe axis.
    var swipeStraightness: CGFloat = 0.5
    /// Punch: shoulder-to-wrist image distance must fall by this share, and by at least `punchMinimum`.
    var punchCollapse: CGFloat = 0.4
    var punchMinimum: CGFloat = 0.3
    var punchWindow = 0.35
    /// The fist must end within this reach of the shoulder and have stopped (per-frame travel).
    var punchReach: CGFloat = 0.55
    var punchStopSpeed: CGFloat = 0.05
    /// Quiet time after any gesture.
    var cooldown = 0.6

    private struct Point { let time: Double; let wrist: CGPoint; let reach: CGFloat }

    private struct HandState {
        var poseSince: Double?
        /// Last time a fist was seen. Only a fist earns the grace period: lowering a raised
        /// open hand must not read as a swipe down.
        var lastFist: Double?
        var history: [Point] = []
    }

    private var hands: [BodyJoint: HandState] = [.leftWrist: HandState(), .rightWrist: HandState()]
    private var quietUntil = -Double.infinity

    /// Which hands are armed right now, for on-screen feedback.
    private(set) var armed: Set<BodyJoint> = []

    /// Ignore everything until `time` (used around golf swings).
    mutating func suppress(until time: Double) {
        quietUntil = max(quietUntil, time)
        clear()
    }

    mutating func ingest(_ frame: PoseFrame?, at time: Double) -> NavGesture? {
        guard let frame, let width = frame.shoulderWidth, width > 0.02 else {
            clear()
            return nil
        }
        guard time >= quietUntil else {
            clear()
            return nil
        }
        let shapes = Dictionary(frame.hands.map { ($0.wrist, $0.shape) }, uniquingKeysWith: { first, _ in first })
        let left = frame.point(.leftWrist), right = frame.point(.rightWrist)
        let gripping = left.flatMap { l in right.map { r in hypot(l.x - r.x, l.y - r.y) / width < gripDistance } } ?? false

        for (wrist, shoulder) in [(BodyJoint.leftWrist, BodyJoint.leftShoulder), (.rightWrist, .rightShoulder)] {
            guard let hand = frame.point(wrist), let shoulderPoint = frame.point(shoulder) else {
                hands[wrist] = HandState()
                armed.remove(wrist)
                continue
            }
            var state = hands[wrist] ?? HandState()
            let shape = shapes[wrist] ?? .unknown
            let raised = (hand.y - shoulderPoint.y) / width > -0.25
            let posed = !gripping && (shape == .fist || (shape == .unknown && raised))
            if posed {
                state.poseSince = state.poseSince ?? time
                if shape == .fist { state.lastFist = time }
            } else if let lastFist = state.lastFist, time - lastFist <= armGrace, !gripping {
                // Keep a fist armed through motion blur.
            } else {
                state.poseSince = nil
                state.lastFist = nil
            }
            let isArmed = state.poseSince.map { time - $0 >= armDuration } ?? false
            guard isArmed, !gripping else {
                if !isArmed { state.history.removeAll() }
                hands[wrist] = state
                armed.remove(wrist)
                continue
            }
            armed.insert(wrist)

            let reach = hypot(hand.x - shoulderPoint.x, hand.y - shoulderPoint.y) / width
            state.history.append(Point(time: time, wrist: hand, reach: reach))
            state.history.removeAll { time - $0.time > max(swipeWindow, punchWindow) }
            hands[wrist] = state

            if let gesture = recognize(state.history, now: time, width: width) {
                quietUntil = time + cooldown
                clear()
                return gesture
            }
        }
        return nil
    }

    private func recognize(_ history: [Point], now: Double, width: CGFloat) -> NavGesture? {
        guard let current = history.last else { return nil }
        for old in history.dropLast() where now - old.time <= swipeWindow {
            let dx = (current.wrist.x - old.wrist.x) / width
            let dy = (current.wrist.y - old.wrist.y) / width
            if abs(dx) >= swipeDistance, abs(dy) <= abs(dx) * swipeStraightness { return dx > 0 ? .right : .left }
            if abs(dy) >= swipeDistance, abs(dx) <= abs(dy) * swipeStraightness { return dy > 0 ? .up : .down }
        }
        // A punch stops dead at full extension with the fist in front of the shoulder; a swipe that
        // passes the shoulder keeps moving.
        let previous = history.count > 1 ? history[history.count - 2] : current
        let stopped = hypot(current.wrist.x - previous.wrist.x, current.wrist.y - previous.wrist.y) / width < punchStopSpeed
        guard stopped, current.reach <= punchReach else { return nil }
        for old in history.dropLast() where now - old.time <= punchWindow {
            let collapse = old.reach - current.reach
            let travel = hypot(current.wrist.x - old.wrist.x, current.wrist.y - old.wrist.y) / width
            if collapse >= punchMinimum, collapse >= old.reach * punchCollapse, travel < swipeDistance {
                return .select
            }
        }
        return nil
    }

    private mutating func clear() {
        hands = [.leftWrist: HandState(), .rightWrist: HandState()]
        armed = []
    }
}
