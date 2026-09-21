import Foundation

/// Position is supplied by world tracking, never acceleration integration.
struct SteeringFilter {
    enum Phase: String { case calibrating, steering, swinging, recovering, trackingLost }
    private(set) var phase: Phase = .calibrating
    private(set) var target: Double = 0
    private var origin = 0.0, anchorTarget = 0.0, started = 0.0, settled = 0.0, peak = 0.0
    private var lastTime = -Double.infinity, emitted = false
    var travel = 0.35
    mutating func calibrate(position: Double, time: Double) {
        origin=position; anchorTarget=target; phase = .steering; lastTime=time; peak=0; emitted=false
    }
    mutating func step(position: Double, rate: Double, time: Double, valid: Bool) -> Double? {
        guard time > lastTime else { return nil }
        let dt=min(time-lastTime,0.05); lastTime=time
        guard valid else { phase = .trackingLost; return nil }
        guard phase != .calibrating && phase != .trackingLost else { return nil }
        switch phase {
        case .steering:
            if rate > 3.0 { phase = .swinging; started=time; peak=rate; emitted=false; return nil }
            let shift=(position-origin)/max(0.15,travel)
            let wanted=max(-1,min(1,anchorTarget+(abs(shift)<0.06 ? 0 : shift)))
            target += (wanted-target)*(1-exp(-dt*14))
        case .swinging:
            peak=max(peak,rate)
            if !emitted && time-started>=0.045 && peak>=4.5 { emitted=true; return min(1,peak/14) }
            if time-started>=0.46 { phase = .recovering; settled=time }
        case .recovering:
            if rate>1.5 { settled=time }
            if time-settled>=0.25 {
                origin=position; anchorTarget=target; phase = .steering; peak=0
            }
        default: break
        }
        return nil
    }
}
