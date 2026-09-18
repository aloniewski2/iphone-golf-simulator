import Foundation

/// Keeps the last few seconds of the player's copied poses so a replay shows their real
/// follow-through instead of a canned one.
struct SwingRecorder {
    var window = 4.0
    /// Seconds after impact kept in a frozen recording.
    var afterImpact = 0.8

    private var samples: [(time: Double, pose: BodyPose3D)] = []
    private(set) var impactTime: Double?

    var isFrozen: Bool {
        guard let impactTime, let last = samples.last else { return false }
        return last.time >= impactTime + afterImpact
    }

    mutating func record(_ pose: BodyPose3D, at time: Double) {
        guard !isFrozen else { return }
        samples.append((time, pose))
        let cutoff = time - window
        if let first = samples.firstIndex(where: { $0.time >= cutoff }), first > 0 { samples.removeFirst(first) }
    }

    /// Marks impact; recording stops `afterImpact` seconds later.
    mutating func markImpact(at time: Double) {
        impactTime = time
    }

    mutating func reset() {
        samples.removeAll()
        impactTime = nil
    }

    /// Pose at `offset` seconds from impact, or nil outside the recording.
    func pose(atImpactOffset offset: Double) -> BodyPose3D? {
        guard let impactTime else { return nil }
        let time = impactTime + offset
        guard let first = samples.first, let last = samples.last, time >= first.time, time <= last.time else { return nil }
        guard let upper = samples.firstIndex(where: { $0.time >= time }) else { return last.pose }
        guard upper > 0 else { return samples[0].pose }
        let a = samples[upper - 1], b = samples[upper]
        let t = Float((time - a.time) / max(b.time - a.time, 0.0001))
        return BodyPose3D.lerp(a.pose, b.pose, t)
    }
}
