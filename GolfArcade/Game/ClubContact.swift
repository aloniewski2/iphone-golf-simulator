import Foundation
import simd

/// Detects the swinging club hitting another player's avatar. Just for fun: it knocks them over
/// and never affects the score.
struct ClubContact {
    struct Target: Equatable {
        let id: UUID
        /// Bottom and top of the body capsule, in the stance frame.
        let base: simd_float3
        let top: simd_float3
        var radius: Float = 0.7
    }

    struct Hit: Equatable {
        let id: UUID
        /// Horizontal direction the club was travelling, in the stance frame.
        let push: simd_float3
    }

    /// Club-head speed (yd/s) needed to knock someone over; a gentle touch does nothing.
    var speedThreshold: Float = 6
    var cooldown = 1.0

    private var last: (time: Double, head: simd_float3)?
    private var lastHit: [UUID: Double] = [:]

    mutating func update(hands: simd_float3, head: simd_float3, at time: Double, targets: [Target]) -> [Hit] {
        defer { last = (time, head) }
        guard let last, time > last.time else { return [] }
        let velocity = (head - last.head) / Float(time - last.time)
        guard simd_length(velocity) >= speedThreshold else { return [] }
        var hits: [Hit] = []
        for target in targets {
            if let previous = lastHit[target.id], time - previous < cooldown { continue }
            let distance = Self.segmentDistance(hands, head, target.base, target.top)
            guard distance <= target.radius else { continue }
            lastHit[target.id] = time
            var push = velocity
            push.y = 0
            hits.append(Hit(id: target.id, push: simd_length(push) > 0.001 ? simd_normalize(push) : simd_float3(0, 0, -1)))
        }
        return hits
    }

    mutating func reset() {
        last = nil
    }

    /// Closest distance between segments p1–q1 and p2–q2.
    static func segmentDistance(_ p1: simd_float3, _ q1: simd_float3, _ p2: simd_float3, _ q2: simd_float3) -> Float {
        let d1 = q1 - p1, d2 = q2 - p2, r = p1 - p2
        let a = simd_dot(d1, d1), e = simd_dot(d2, d2), f = simd_dot(d2, r)
        var s: Float = 0, t: Float = 0
        if a <= 1e-6, e <= 1e-6 { return simd_length(r) }
        if a <= 1e-6 {
            t = min(max(f / e, 0), 1)
        } else {
            let c = simd_dot(d1, r)
            if e <= 1e-6 {
                s = min(max(-c / a, 0), 1)
            } else {
                let b = simd_dot(d1, d2)
                let denominator = a * e - b * b
                s = denominator != 0 ? min(max((b * f - c * e) / denominator, 0), 1) : 0
                t = (b * s + f) / e
                if t < 0 { t = 0; s = min(max(-c / a, 0), 1) }
                else if t > 1 { t = 1; s = min(max((b - c) / a, 0), 1) }
            }
        }
        return simd_length((p1 + d1 * s) - (p2 + d2 * t))
    }
}
