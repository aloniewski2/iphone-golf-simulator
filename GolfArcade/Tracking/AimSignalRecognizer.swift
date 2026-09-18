import CoreGraphics
import Foundation

/// Aiming from the camera without leaving the ball: hold one arm straight out to the side at
/// shoulder height, like signalling a turn, and the line steps that way — once after a short
/// hold, then again every half second while the arm stays out. The other hand stays down near
/// the body, so a swing (both hands together on the grip) never reads as a signal and a signal
/// never reads as a swing. Sides are the screen's: the preview is a mirror, so the arm you see
/// on the left moves the line left.
///
/// All distances are in shoulder widths, so it works at any distance from the camera.
struct AimSignalRecognizer: Equatable {
    enum Side: Equatable, Sendable { case left, right }

    /// Seconds the arm must be out before the first step.
    var holdDuration = 0.3
    /// Seconds between steps while the arm stays out.
    var repeatInterval = 0.5
    /// Sideways reach from the shoulder centre that counts as an outstretched arm.
    var reach: CGFloat = 1.1
    /// The signalling hand may hang at most this far below the shoulder line.
    var maximumDrop: CGFloat = 0.3
    /// The other hand must hang at least this far below the shoulders and stay close to the body.
    var restingDrop: CGFloat = 0.4
    var restingReach: CGFloat = 0.9

    /// The side being signalled right now, or nil.
    private(set) var side: Side?
    private var since: Double?
    private var lastStep: Double?

    var isSignalling: Bool { side != nil }

    /// Returns a side each time the line should step.
    mutating func ingest(_ frame: PoseFrame?, at time: Double) -> Side? {
        guard let frame, let width = frame.shoulderWidth, width > 0.02,
              let leftShoulder = frame.point(.leftShoulder), let rightShoulder = frame.point(.rightShoulder),
              let a = frame.point(.leftWrist), let b = frame.point(.rightWrist) else {
            clear()
            return nil
        }
        let center = CGPoint(x: (leftShoulder.x + rightShoulder.x) / 2, y: (leftShoulder.y + rightShoulder.y) / 2)
        func offset(_ point: CGPoint) -> CGVector {
            CGVector(dx: (point.x - center.x) / width, dy: (point.y - center.y) / width)
        }
        let hands = [offset(a), offset(b)]
        guard let out = hands.first(where: { abs($0.dx) >= reach && $0.dy >= -maximumDrop }),
              let resting = hands.first(where: { $0 != out }),
              resting.dy <= -restingDrop, abs(resting.dx) <= restingReach else {
            clear()
            return nil
        }
        let current: Side = out.dx < 0 ? .left : .right
        if current != side {
            side = current
            since = time
            lastStep = nil
        }
        guard let since, time - since >= holdDuration else { return nil }
        if let lastStep, time - lastStep < repeatInterval { return nil }
        lastStep = time
        return current
    }

    mutating func clear() {
        side = nil
        since = nil
        lastStep = nil
    }
}
