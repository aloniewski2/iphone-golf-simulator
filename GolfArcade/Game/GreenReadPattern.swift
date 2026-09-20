import Foundation
import simd

/// Renderer-independent downhill read, sampled from the authoritative surface.
enum GreenReadPattern {
    struct Sample: Sendable {
        let point: CoursePoint
        let height: Double
        let flow: SIMD2<Float>
        let speed: Float
        let slope: Double
        let curve: SIMD2<Float>
    }

    static func samples(_ hole: Hole) -> [Sample] {
        let reach = hole.greenRadius + 2
        var result: [Sample] = []
        for x in stride(from: hole.pin.x - reach, through: hole.pin.x + reach, by: 2.0) {
            for d in stride(from: hole.pin.d - reach, through: hole.pin.d + reach, by: 2.0) {
                let point = CoursePoint(x: x, d: d)
                guard hole.lie(at: point) == .green, point.distance(to: hole.pin) > 1.7 else { continue }
                let surface = hole.surface(at: point), slope = hypot(surface.slopeX, surface.slopeD)
                let flow: SIMD2<Float> = slope > 0.0005 ? simd_normalize(SIMD2(Float(-surface.slopeX), Float(surface.slopeD))) : .zero
                let low = CoursePoint(x: x - Double(flow.x), d: d + Double(flow.y))
                let high = CoursePoint(x: x + Double(flow.x), d: d - Double(flow.y))
                guard hole.lie(at: low) == .green, hole.lie(at: high) == .green else { continue }
                let h0 = surface.heightYards, hm = hole.surface(at: low).heightYards, hp = hole.surface(at: high).heightYards
                result.append(Sample(point: point, height: h0, flow: flow, speed: Float(min(1.4, slope * 40)), slope: slope,
                                     curve: SIMD2(Float((hp - hm) / 2), Float((hp + hm) / 2 - h0))))
            }
        }
        return result
    }
}
