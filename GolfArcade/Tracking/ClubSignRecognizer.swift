import CoreGraphics
import Foundation

/// Picking a club with a hand sign: hold up one finger for the driver, two for the iron, three
/// for the wedge, four for the putter. The hand must be raised to chest height or higher and
/// apart from the other hand (a grip is never a sign), and the count has to hold steady for a
/// moment so a hand opening or closing on the way to a fist does not pick a club on its way past.
/// One club per raise: the sign has to change, or the hand drop, before it can pick again.
struct ClubSignRecognizer: Equatable {
    /// Seconds the same count must be held.
    var holdDuration = 0.5
    /// Seconds without a readable sign before the hand counts as dropped.
    var dropAfter = 0.4
    /// Wrists closer than this, in shoulder widths, are gripping together.
    var gripDistance: CGFloat = 0.8
    /// The signing hand's wrist may be at most this far below the shoulder line, in shoulder widths.
    var maximumDrop: CGFloat = 0.9

    static let clubs: [Int: GolfClub] = [1: .driver, 2: .iron, 3: .wedge, 4: .putter]

    /// The count being held right now, for on-screen feedback.
    private(set) var showing: Int?
    private var since: Double?
    private var lastSeen: Double?
    private var delivered: Int?

    mutating func ingest(_ frame: PoseFrame?, at time: Double) -> GolfClub? {
        guard let frame, let width = frame.shoulderWidth, width > 0.02,
              let leftShoulder = frame.point(.leftShoulder), let rightShoulder = frame.point(.rightShoulder) else {
            lapse(at: time)
            return nil
        }
        let shoulderY = (leftShoulder.y + rightShoulder.y) / 2
        let left = frame.point(.leftWrist), right = frame.point(.rightWrist)
        let gripping = left.flatMap { l in right.map { r in hypot(l.x - r.x, l.y - r.y) / width < gripDistance } } ?? false
        var count: Int?
        if !gripping {
            for reading in frame.hands {
                guard let fingers = reading.fingers, Self.clubs[fingers] != nil,
                      let wrist = frame.point(reading.wrist), (wrist.y - shoulderY) / width >= -maximumDrop else { continue }
                count = fingers
                break
            }
        }
        guard let count else {
            lapse(at: time)
            return nil
        }
        lastSeen = time
        if count != showing {
            showing = count
            since = time
        }
        guard let since, time - since >= holdDuration, delivered != count else { return nil }
        delivered = count
        return Self.clubs[count]
    }

    private mutating func lapse(at time: Double) {
        if let lastSeen, time - lastSeen < dropAfter { return }
        showing = nil
        since = nil
        delivered = nil
        lastSeen = nil
    }

    mutating func clear() {
        showing = nil
        since = nil
        delivered = nil
        lastSeen = nil
    }
}
