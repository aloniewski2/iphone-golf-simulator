// Gates for swing prediction (SteeringFilter.peakConfirm), built on the Mac against the app's own filter:
//   swiftc -parse-as-library -O GolfArcade/Unity/SteeringFilter.swift work/hit-feel/test_swing_prediction.swift -o work/hit-feel/out/test_swing && work/hit-feel/out/test_swing
// Synthetic 100 Hz swings: rotation rate rising to a peak and falling away, linear force following it. Each is run with
// prediction off (the old proof-only confirmation) and on, and the confirmation time is compared with the true peak.
import Foundation

struct Swing { let name: String; let peak: Double, rise: Double, fall: Double, forcePerRate: Double }

/// Confirmation time after the true peak (ms, negative = before it), the power read, and whether it confirmed at all.
struct Noise { var state: UInt64; mutating func gaussian(_ sigma: Double) -> Double {
    func u() -> Double { state = state &* 6364136223846793005 &+ 1442695040888963407; return Double(state >> 11) / Double(1 << 53) }
    return sigma * sqrt(-2 * log(max(u(), 1e-12))) * cos(2 * .pi * u()) } }

func run(_ s: Swing, prediction: Bool, noise sigma: Double = 0, seed: UInt64 = 1) -> (afterPeakMs: Double, power: Double, confirmed: Bool, atPeak: Bool) {
    var noise = Noise(state: seed)
    var f = SteeringFilter()
    f.tennisStroke = true; f.peakConfirm = prediction
    f.calibrate(position: 0, time: 0)
    let start = 0.5, dt = 0.01
    var t = dt
    while t < 2 {
        var rate = 0.3
        let x = t - start
        if x >= 0 && x < s.rise + s.fall {
            rate = x < s.rise ? s.peak * sin(.pi / 2 * x / s.rise) : s.peak * cos(.pi / 2 * (x - s.rise) / s.fall)
            rate = max(rate, 0.3)
        }
        rate = max(0, rate + noise.gaussian(sigma))
        if let power = f.step(position: 0, rate: rate, time: t, valid: true, acceleration: rate * s.forcePerRate) {
            return ((t - (start + s.rise)) * 1000, power, true, f.confirmedAtPeak)
        }
        t += dt
    }
    return (0, 0, false, false)
}

@main struct SwingPredictionGates {
    static func main() {
        var failures = 0
        func gate(_ name: String, _ pass: Bool, _ detail: String) {
            print("GATE: \(name.padding(toLength: 30, withPad: " ", startingAt: 0)) \(pass ? "PASS" : "FAIL")  \(detail)")
            if !pass { failures += 1 }
        }
        let fast = Swing(name: "fast forehand", peak: 14, rise: 0.06, fall: 0.12, forcePerRate: 0.12)
        let quick = Swing(name: "quick flat drive", peak: 18, rise: 0.05, fall: 0.10, forcePerRate: 0.12)
        let slow = Swing(name: "slow lob", peak: 9, rise: 0.16, fall: 0.18, forcePerRate: 0.1)
        let weak = Swing(name: "weak push", peak: 6.5, rise: 0.12, fall: 0.15, forcePerRate: 0.12)
        let step = Swing(name: "sidestep arm", peak: 6, rise: 0.12, fall: 0.15, forcePerRate: 0.03)
        let flinch = Swing(name: "flinch", peak: 9, rise: 0.02, fall: 0.02, forcePerRate: 0.12)

        for s in [fast, quick, slow] {
            let off = run(s, prediction: false), on = run(s, prediction: true)
            print(String(format: "  %-16@ proof %+5.0f ms power %.2f   prediction %+5.0f ms power %.2f %@", s.name as NSString,
                         off.afterPeakMs, off.power, on.afterPeakMs, on.power, (on.atPeak ? "(at peak)" : "(proof)") as NSString))
        }
        let fOff = run(fast, prediction: false), fOn = run(fast, prediction: true)
        gate("FAST_SWING_EARLIER", fOn.confirmed && fOff.confirmed && fOff.afterPeakMs - fOn.afterPeakMs >= 25 && fOn.atPeak,
             String(format: "fast forehand confirmed %.0f ms after its peak instead of %.0f", fOn.afterPeakMs, fOff.afterPeakMs))
        let qOff = run(quick, prediction: false), qOn = run(quick, prediction: true)
        gate("QUICK_SWING_EARLIER", qOff.afterPeakMs - qOn.afterPeakMs >= 25,
             String(format: "quick drive %.0f ms after its peak instead of %.0f", qOn.afterPeakMs, qOff.afterPeakMs))
        gate("AT_THE_PEAK", abs(fOn.afterPeakMs) <= 10.5 && abs(qOn.afterPeakMs) <= 10.5, "prediction confirms within one sample of the peak")
        gate("TRUE_PEAK_POWER", abs(fOn.power - min(1, (fast.peak - 4.2) / 10)) < 0.02,
             String(format: "power %.2f from the true peak (%.2f)", fOn.power, min(1, (fast.peak - 4.2) / 10)))
        let sOff = run(slow, prediction: false), sOn = run(slow, prediction: true)
        gate("SLOW_SWING_UNCHANGED", sOff.confirmed && sOn.confirmed && abs(sOn.afterPeakMs - sOff.afterPeakMs) < 0.5,
             String(format: "slow lob confirms by proof either way (%+.0f ms), before its peak as before", sOn.afterPeakMs))
        let wOn = run(weak, prediction: true), wOff = run(weak, prediction: false)
        gate("WEAK_SWING_NOT_PREDICTED", !wOn.atPeak && wOn.confirmed == wOff.confirmed && abs(wOn.afterPeakMs - wOff.afterPeakMs) < 0.5,
             "a swing under 7 rad/s waits for the proof as before")
        gate("SIDESTEP_REJECTED", !run(step, prediction: true).confirmed && !run(step, prediction: false).confirmed,
             "an arm swing without a stroke's force never confirms")
        gate("FLINCH_REJECTED", !run(flinch, prediction: true).confirmed, "a 40 ms flick does not turn enough to count")
        // Real gyro noise (0.1 rad/s on every sample, 300 runs): prediction must not jump early on a wobble.
        var times: [Double] = []
        for seed in 1...300 { let r = run(fast, prediction: true, noise: 0.1, seed: UInt64(seed)); if r.confirmed { times.append(r.afterPeakMs) } }
        times.sort()
        let median = times[times.count / 2], early = times.filter { $0 < -10 }.count
        gate("NOISE_NOT_EARLY", times.count == 300 && early <= 6 && median <= 10.5,
             String(format: "noisy fast forehand (%d of 300 confirmed): median %+.0f ms after the peak, %d more than 10 ms early, earliest %+.0f", times.count, median, early, times.first ?? 0))
        print(failures == 0 ? "RESULT: PASS" : "RESULT: FAIL")
        exit(failures == 0 ? 0 : 1)
    }
}
