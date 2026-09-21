import Foundation
import simd

enum SportsMotionGeometry {
    /// Flatten a direction onto the floor plane. Everything about court geometry is
    /// horizontal, so pitching or rolling the phone must not change the answer.
    static func horizontal(_ vector:SIMD3<Float>) -> SIMD3<Float>? {
        let flat=SIMD3<Float>(vector.x,0,vector.z)
        return simd_length(flat)>0.15 ? simd_normalize(flat) : nil
    }
    /// Which stroke the grip implies, from which face of the phone points at the TV.
    ///
    /// Screen toward the TV is a forehand (+1); rear camera toward the TV is a backhand (-1).
    /// Comparing horizontal directions in the world frame makes this independent of how far
    /// the phone is pitched or rolled — the earlier quaternion-relative test went ambiguous
    /// as soon as the phone was tilted much past level.
    static func strokeFacing(screenNormal:SIMD3<Float>,courtForward:SIMD3<Float>,previous:Double) -> Double {
        guard let flat=horizontal(screenNormal) else { return previous }
        let alignment=Double(simd_dot(flat,courtForward))
        if alignment > 0.25 { return 1 }
        if alignment < -0.25 { return -1 }
        return previous // edge-on: retain the last unambiguous grip
    }
    static func horizontalRight(cameraBack:SIMD3<Float>) -> SIMD3<Float>? {
        let axis=simd_cross(SIMD3<Float>(0,1,0),cameraBack)
        return simd_length(axis)>0.2 ? simd_normalize(axis) : nil
    }
    /// How far the rear lens is tilted away from level, in radians. The court axis is only
    /// meaningful when the lens looks along the floor plane, not at the ceiling or carpet.
    static func lensPitch(cameraBack:SIMD3<Float>) -> Double {
        let length=simd_length(cameraBack)
        guard length>1e-5 else { return .pi/2 }
        return abs(asin(Double(min(1,max(-1,cameraBack.y/length)))))
    }
}

/// Position is supplied by world tracking, never acceleration integration.
///
/// The mapping from real-world position to court position is fixed at Ready and is never
/// rebased afterwards: a given spot on the floor always means the same spot on the court.
/// Rebasing after every stroke was what let body drift accumulate until the target pinned
/// at a court edge. The only re-anchor is after a *long* tracking outage, where the world
/// position genuinely cannot be trusted to line up with the one from before the gap.
struct SteeringFilter {
    enum Phase: String { case calibrating, steering, swinging, recovering, trackingLost }
    /// A blip this short keeps the fixed mapping; anything longer re-anchors for continuity.
    static let briefOutage = 1.0
    /// A jump larger than this across an outage means ARKit relocalized somewhere else.
    static let implausibleJump = 0.5
    static let deadZone = 0.06
    /// Reach is measured per side and never assumed symmetric. The phone is held in one
    /// hand, so its position encodes the *grip* as much as the body: a right-hander's
    /// backhand carries the hand across to the left, while the forehand sits barely right
    /// of neutral. One shared `travel` therefore makes one direction far harder to reach.
    /// Each side grows instantly to whatever the player actually covers and relaxes toward
    /// it, settling on that side's real peak excursion, so full court stays reachable both
    /// ways whatever the cause of the imbalance. The floor is an absolute distance and must
    /// stay well under a weak side's reach, or it silently caps that side short of the line.
    static let reachFloor = 0.25, reachRelax = 0.05
    /// A sidestep rotates the phone too, so rate alone cannot tell stepping from swinging.
    /// A real stroke is fast AND forceful AND sustained; a step fails at least one.
    static let strokeRate = 5.0, strokeForce = 0.45, strokeHold = 0.08
    /// How long a candidate may hold steering hostage before it is written off as a step.
    static let strokeAbort = 0.18
    private(set) var phase: Phase = .calibrating
    private(set) var target: Double = 0
    private var origin = 0.0, started = 0.0, settled = 0.0, peak = 0.0
    private var lastTime = -Double.infinity, emitted = false, arc = 0.0
    var travel = 0.85
    var detectSwings = true
    var tennisStroke = false
    private var candidateDuration = 0.0, peakAcceleration = 0.0
    private var strokeArmed = true
    private var lostAt = -Double.infinity, lostPosition = 0.0
    private(set) var reachRight = 0.0, reachLeft = 0.0
    /// Peak excursion actually seen on each side, for diagnostics.
    private(set) var seenRight = 0.0, seenLeft = 0.0
    var adaptiveReach = true

    mutating func calibrate(position: Double, time: Double) {
        // Ready always returns the player to court center. Preserving the old target here
        // was why tapping Ready while pinned at an edge left you pinned at that edge.
        origin=position; target=0; phase = .steering; lastTime=time
        peak=0; emitted=false; candidateDuration=0; strokeArmed=true
        lostAt = -Double.infinity
        reachRight=travel; reachLeft=travel; seenRight=0; seenLeft=0
    }

    /// Re-anchor so that the player's current real position maps to the court position they
    /// already hold. Continuous, no jump, and no accumulated drift.
    /// Signed court position in -1...1 for a real-world displacement, with a soft dead zone.
    static func courtPosition(delta: Double, span: Double) -> Double {
        let reach=max(0.15,span)
        let dead=deadZone*reach
        let travelled=abs(delta)-dead
        guard travelled > 0 else { return 0 }
        return travelled/max(0.01,reach-dead)*(delta<0 ? -1 : 1)
    }
    /// Inverse of `courtPosition`: the displacement that would produce this court position.
    static func displacement(target: Double, span: Double) -> Double {
        guard target != 0 else { return 0 }
        let reach=max(0.15,span)
        let dead=deadZone*reach
        return (abs(target)*(reach-dead)+dead)*(target<0 ? -1 : 1)
    }
    private mutating func reanchor(position: Double) {
        origin = position - Self.displacement(target:target,span:target<0 ? reachLeft : reachRight)
    }

    mutating func step(position: Double, rate: Double, time: Double, valid: Bool, allowSwingWhileUntracked: Bool = false, acceleration: Double = 0) -> Double? {
        guard time > lastTime else { return nil }
        let dt=min(time-lastTime,0.05); lastTime=time
        guard phase != .calibrating else { return nil }

        if phase == .swinging {
            // A stroke in flight is driven by the gyro, not the camera. Motion blur must
            // never abandon a swing the player has already started.
            if !valid && !allowSwingWhileUntracked {
                lostAt=time; lostPosition=position; phase = .trackingLost; return nil
            }
        } else if !valid {
            // Tracking loss is transient, never terminal. Gyro strokes may still land while
            // the camera recovers; positional steering simply holds its last court position.
            if phase != .trackingLost { lostAt=time; lostPosition=position }
            phase = .trackingLost
            guard allowSwingWhileUntracked else { return nil }
        } else if phase == .trackingLost {
            // First trustworthy frame after a gap. A brief blur keeps the fixed mapping so
            // real movement during the blip still counts; a long outage or a teleporting
            // origin re-anchors instead of snapping the player across the court.
            let outage = time-lostAt
            if outage > Self.briefOutage || abs(position-lostPosition) > Self.implausibleJump { reanchor(position:position) }
            phase = .steering
            peak=0; candidateDuration=0; strokeArmed=true
        }

        switch phase {
        case .steering, .trackingLost:
            // Norms are independent of portrait/landscape grip. Translation alone,
            // or slowly turning the phone, must not lock positional steering.
            if rate<2.0 && acceleration<0.15 { strokeArmed=true }
            let onset = tennisStroke
                ? strokeArmed && rate > Self.strokeRate && acceleration > Self.strokeForce
                : rate > 2.2
            candidateDuration = onset ? candidateDuration+dt : 0
            if detectSwings && onset && (!tennisStroke || candidateDuration >= Self.strokeHold) {
                phase = .swinging; started=time; peak=rate; peakAcceleration=acceleration; arc=0; emitted=false; strokeArmed=false; return nil
            }
            guard valid, phase == .steering else { return nil }
            let delta=position-origin
            // Learn reach only while steering: a follow-through flings the phone far wider
            // than the player can actually cover on their feet.
            if adaptiveReach {
                let floor=Self.reachFloor
                seenRight=max(seenRight,delta); seenLeft=max(seenLeft,-delta)
                reachRight = max(floor, max(reachRight - Self.reachRelax * dt, delta))
                reachLeft = max(floor, max(reachLeft - Self.reachRelax * dt, -delta))
            }
            let span = delta>=0 ? reachRight : reachLeft
            // Take the dead zone off in metres BEFORE normalising, so the far end of the
            // player's reach still maps to the edge of the court. Normalising first and
            // subtracting after would quietly make full court unreachable.
            let wanted=max(-1,min(1,Self.courtPosition(delta:delta,span:span)))
            target += (wanted-target)*(1-exp(-dt*14))
        case .swinging:
            peak=max(peak,rate)
            peakAcceleration=max(peakAcceleration,acceleration)
            arc += rate*dt
            let confirmed = tennisStroke ? arc>=0.55 && peakAcceleration>=Self.strokeForce : arc>=0.25
            if !emitted && time-started>=0.06 && confirmed && rate>=2.2 {
                emitted=true
                return tennisStroke ? max(0.15,min(1,(peak-Self.strokeRate)/10)) : min(1,peak/12)
            }
            // A candidate that never becomes a real stroke hands steering straight back,
            // instead of freezing the player for three quarters of a second.
            if tennisStroke && !emitted && time-started>=Self.strokeAbort && !confirmed {
                phase = .steering; peak=0; candidateDuration=0; strokeArmed=false
                return nil
            }
            if time-started>=0.46 { phase = .recovering; settled=time }
        case .recovering:
            // Recovery no longer waits for stillness, because it no longer has to pick a
            // moment to rebase. Holding steering for up to 0.85s after every stroke is what
            // made a rally feel unresponsive; the fixed mapping resumes as soon as the
            // racket-swing window is over.
            if valid && (rate<2.5 || time-settled>=0.25) {
                phase = .steering; peak=0; candidateDuration=0
            }
        default: break
        }
        return nil
    }
}
