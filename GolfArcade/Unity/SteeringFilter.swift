import Foundation
import simd

enum SportsMotionSampleClock {
    /// Match the sensor's boot-relative acquisition time to the bridge clock while
    /// retaining callback backlog. Reject old or invalid samples instead of replaying them.
    static func acquisitionTime(sensor: Double, uptime: Double, now: Double) -> Double? {
        guard sensor.isFinite, uptime.isFinite, now.isFinite else { return nil }
        let age = uptime - sensor
        guard age >= -0.05, age <= 0.25 else { return nil }
        return now - max(0, age)
    }
}

enum SportsMotionGeometry {
    // MARK: Motion-sensor (IMU) geometry
    //
    // Swings, grip and racket-face direction come from the motion sensors, not the camera:
    // the gyro reads a sideways swing, a phone held flat, or a covered lens exactly as well
    // as an upright phone pointed at the TV, while camera tracking blurs and drops out.
    // The only thing the camera contributes is where the TV is, captured once at setup.

    /// Rotate a device-frame vector into the motion reference frame (Z vertical).
    static func rotate(_ v: SIMD3<Double>, by q: simd_quatd) -> SIMD3<Double> { q.act(v) }

    /// Heading of a direction in the horizontal plane of the motion reference frame (Z is
    /// vertical), in radians; nil when the direction is too close to vertical to have one.
    static func heading(_ v: SIMD3<Double>) -> Double? {
        let flat=SIMD2<Double>(v.x,v.y)
        return simd_length(flat)>0.15 ? atan2(flat.y,flat.x) : nil
    }

    /// Wrap an angle into -pi...pi.
    static func wrap(_ a: Double) -> Double {
        var x=a.truncatingRemainder(dividingBy:2 * .pi)
        if x > .pi { x -= 2 * .pi }
        if x < -.pi { x += 2 * .pi }
        return x
    }

    /// Grip from which face points at the TV: screen toward it is a forehand (+1), back
    /// toward it a backhand (-1); edge-on keeps the previous grip.
    static func strokeFacing(screenHeading: Double?, tvHeading: Double, previous: Double) -> Double {
        guard let screenHeading else { return previous }
        let alignment=cos(wrap(screenHeading-tvHeading))
        if alignment > 0.25 { return 1 }
        if alignment < -0.25 { return -1 }
        return previous
    }

    /// Racket-face angle at contact, degrees, positive to the player's right: how far the
    /// hitting face (the screen on a forehand, the back on a backhand) is turned from
    /// facing the TV. Headings grow counter-clockwise seen from above, which is to the
    /// player's left, hence the sign flip.
    static func faceAngle(screenHeading: Double, tvHeading: Double, facing: Double) -> Double {
        let face = facing >= 0 ? screenHeading : screenHeading + .pi
        return -wrap(face-tvHeading) * 180 / .pi
    }

    /// Aim in -1...1 from a face angle, measured against the player's own habitual face
    /// angle for that wing: people hold and swing differently, so "straight" is whatever
    /// this player usually does, and turning the face past it aims.
    /// A small dead zone keeps wrist wobble from steering a straight ball; past it the aim
    /// grows smoothly to full at `aimSpanDegrees` (a wider span than before, so fast wrist
    /// rotation through contact does not saturate it).
    static let aimSpanDegrees = 32.0, aimDeadZone = 4.0
    static func aim(faceAngle: Double, neutral: Double) -> Double {
        let d = faceAngle-neutral
        let beyond = max(0, abs(d)-aimDeadZone)/(aimSpanDegrees-aimDeadZone)
        return (d < 0 ? -1 : 1) * min(1, pow(beyond, 0.85))
    }
    /// Learn the habitual angle only from ordinary, nearly-straight swings, slowly, so
    /// deliberately aimed shots never drag "straight" after them.
    static func learnNeutral(_ neutral: Double, faceAngle: Double) -> Double {
        abs(faceAngle-neutral) < 5 ? neutral + (faceAngle-neutral)*0.1 : neutral
    }

    /// Distance band for play: close enough to see the TV, far enough to swing freely and
    /// for the camera to see the room rather than one blank wall.
    static let minTVDistance = 1.8, idealTVDistance = 2.5


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
    ///
    /// Force is the discriminator that matters: a sidestep swings the arm through a similar
    /// rotation rate but never sustains this much linear force. The rate bar is therefore
    /// kept low enough for an overhead serve, which rotates about a horizontal axis and was
    /// being missed, while the force bar still rejects walking.
    ///
    /// The hold used to be 70ms, and nothing reached the game until the stroke was also
    /// *confirmed* 60-70ms later -- about 200ms in all before the character moved, against a
    /// real forehand that lasts about 250ms. The game now starts the stroke animation at
    /// onset (see `onsets`) and cancels it if the candidate is written off (`aborts`), so the
    /// hold only has to reject single-sample spikes; confirmation still gates contact.
    static let strokeRate = 4.2, strokeForce = 0.45, strokeHold = 0.03
    /// Re-arming had to happen through a near-standstill, so raising the phone overhead for
    /// a serve left the detector disarmed and the serve swing was never seen at all.
    static let strokeArc = 0.75
    static let rearmRate = 2.8, rearmForce = 0.24
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
    /// Running counts of stroke onsets and of onsets that never became strokes. The game
    /// starts the animation on a new onset and cancels it on a new abort, so the character
    /// moves with the player's arm instead of a confirmation window later.
    private(set) var onsets = 0, aborts = 0
    private(set) var onsetTime = 0.0, confirmationTime = 0.0, abortTime = 0.0
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

        if rate < Self.rearmRate && acceleration < Self.rearmForce {
            strokeArmed = true
        }
        if phase == .swinging || phase == .recovering {
            // A stroke in flight is driven by the gyro, not the camera. Motion blur must
            // never abandon a swing the player has already started.
            if !valid && !allowSwingWhileUntracked {
                if !emitted { aborts+=1; abortTime=time }
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
            if rate<Self.rearmRate && acceleration<Self.rearmForce { strokeArmed=true }
            let onset = tennisStroke
                ? strokeArmed && rate > Self.strokeRate && acceleration > Self.strokeForce
                : rate > 2.2
            candidateDuration = onset ? candidateDuration+dt : 0
            if detectSwings && onset && (!tennisStroke || candidateDuration >= Self.strokeHold) {
                phase = .swinging; started=time; peak=rate; peakAcceleration=acceleration; arc=0; emitted=false; strokeArmed=false
                onsets+=1; onsetTime=time
                return nil
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
            // Arc now accumulates from the (shorter) onset hold, so the bar is raised by the
            // rotation the old 70ms hold used to absorb: the same flick is still rejected.
            let confirmed = tennisStroke ? arc>=Self.strokeArc && peakAcceleration>=Self.strokeForce : arc>=0.25
            if !emitted && time-started>=0.06 && confirmed && rate>=2.2 {
                emitted=true; confirmationTime=time
                return tennisStroke ? max(0.15,min(1,(peak-Self.strokeRate)/10)) : min(1,peak/12)
            }
            // A candidate that never becomes a real stroke hands steering straight back,
            // instead of freezing the player for three quarters of a second.
            if tennisStroke && !emitted && time-started>=Self.strokeAbort && !confirmed {
                phase = .steering; peak=0; candidateDuration=0; strokeArmed=false
                aborts+=1; abortTime=time
                return nil
            }
            if time-started>=0.46 {
                if !emitted { aborts+=1; abortTime=time }
                phase = .recovering; settled=time
            }
        case .recovering:
            // Recovery no longer waits for stillness, because it no longer has to pick a
            // moment to rebase. Holding steering for up to 0.85s after every stroke is what
            // made a rally feel unresponsive; the fixed mapping resumes as soon as the
            // racket-swing window is over.
            if (valid || allowSwingWhileUntracked) && (rate<2.5 || time-settled>=0.25) {
                phase = valid ? .steering : .trackingLost; peak=0; candidateDuration=0
            }
        default: break
        }
        return nil
    }
}

struct TennisAimWing: Codable, Equatable, Sendable {
    var neutral: Double
    var left: Double
    var right: Double
    static let standard = Self(neutral: 0, left: -32, right: 32)
    var valid: Bool { neutral.isFinite && left.isFinite && right.isFinite && neutral-left >= 8 && right-neutral >= 8 && right-left <= 150 }
    static func relative(_ angle: Double, to center: Double) -> Double {
        SportsMotionGeometry.wrap((angle-center) * .pi/180) * 180 / .pi
    }
    func aim(_ angle: Double) -> Double {
        guard valid, angle.isFinite else { return 0 }
        let delta = Self.relative(angle, to: neutral)
        let dead = 2.5
        let span = delta < 0 ? neutral-left : right-neutral
        return (delta < 0 ? -1 : 1) * min(1, 0.8 * max(0, abs(delta)-dead) / (span-dead))
    }
    static func fit(_ samples: [Int: [Double]]) -> Self? {
        guard let centers=samples[0], let ls=samples[-1], let rs=samples[1], centers.count >= 2, ls.count >= 2, rs.count >= 2,
              (centers+ls+rs).allSatisfy({ $0.isFinite }) else { return nil }
        func mean(_ angles: [Double], around center: Double) -> Double {
            center + angles.map { relative($0, to: center) }.reduce(0,+) / Double(angles.count)
        }
        let center=mean(centers,around:centers[0])
        let l=mean(ls,around:center), r=mean(rs,around:center)
        for angles in [centers,ls,rs] {
            let m=mean(angles,around:center)
            if angles.contains(where:{ abs(relative($0,to:m)) > 7 }) { return nil }
        }
        let wing=Self(neutral:center,left:l,right:r)
        return wing.valid ? wing : nil
    }
}

struct TennisAimProfile: Codable, Equatable, Sendable {
    var forehand: TennisAimWing
    var backhand: TennisAimWing
    var valid: Bool { forehand.valid && backhand.valid }
    func aim(angle: Double, facing: Double) -> Double { (facing >= 0 ? forehand : backhand).aim(angle) }
}

struct TennisAimSwing: Sendable {
    var time: Double
    var angle: Double
    var facing: Double
}

/// The requested lane is a lesson instruction; only actual new-shot landings validate it.
struct TennisAimLesson {
    enum Phase { case capture, validation, complete }
    struct Trial { var wing: Int; var lane: Int }
    var phase: Phase
    var trials: [Trial]
    var index=0
    var samples = [Int: [Int: [Double]]]()
    var profile: TennisAimProfile?
    var successes=0
    var wingSuccesses=[0,0]
    var quick: Bool
    var message="Watch the highlighted target, then swing comfortably."
    var current: Trial? { index < trials.count ? trials[index] : nil }
    var progress: String { phase == .capture ? "Learn your swing · \(index+1) of \(trials.count)" : "Aim check · \(min(index+1,trials.count)) of \(trials.count)" }
    init(saved: TennisAimProfile? = nil) {
        quick=saved?.valid == true; profile=quick ? saved : nil
        phase=quick ? .validation : .capture
        trials=quick ? [Trial(wing:0,lane:-1),Trial(wing:1,lane:0),Trial(wing:0,lane:1)] : Self.captureTrials
    }
    static var captureTrials: [Trial] {
        [0,1].flatMap { wing in [0,-1,1,0,-1,1].map { Trial(wing:wing,lane:$0) } }
    }
    static var validationTrials: [Trial] {
        [0,1].flatMap { wing in [1,0,-1,0,-1,1].map { Trial(wing:wing,lane:$0) } }
    }
    static func lane(at x: Double) -> Int { x < -1.2 ? -1 : x > 1.2 ? 1 : 0 }
    /// Returns true when this attempt is accepted; retries preserve the current instruction.
    mutating func record(_ swing: TennisAimSwing, landingX: Double, legal: Bool) -> Bool {
        guard let trial=current, swing.time.isFinite, swing.angle.isFinite, swing.facing.isFinite, landingX.isFinite else { return false }
        let wing=swing.facing >= 0 ? 0 : 1
        guard wing == trial.wing else { message="Use the \(trial.wing == 0 ? "forehand" : "backhand") grip for this shot. Try again."; return false }
        if phase == .capture {
            guard legal else { message="Let's repeat that shot. Swing as the practice ball reaches you."; return false }
            samples[wing,default:[:]][trial.lane,default:[]].append(swing.angle)
        } else {
            if legal && Self.lane(at:landingX) == trial.lane { successes+=1; wingSuccesses[wing]+=1; message="That reached the target." }
            else { message="That missed the lane. Keep the racket face pointed toward the target." }
        }
        index+=1
        if index == trials.count {
            if phase == .capture {
                guard let forehand=TennisAimWing.fit(samples[0] ?? [:]), let backhand=TennisAimWing.fit(samples[1] ?? [:]) else {
                    // Keep valid lanes and recapture only an ambiguous lane or stroke side.
                    let badWing=TennisAimWing.fit(samples[0] ?? [:]) == nil ? 0 : 1
                    let group=samples[badWing] ?? [:]
                    let center=group[0]?.first ?? 0
                    let badLane=[0,-1,1].first { lane in
                        guard let angles=group[lane], angles.count >= 2 else { return true }
                        let d=angles.map { TennisAimWing.relative($0,to:center) }
                        let spread: Double = (d.max() ?? 0) - (d.min() ?? 0)
                        if spread > 14 { return true }
                        let mean: Double = d.reduce(0, +) / Double(d.count)
                        return lane != 0 && mean * Double(lane) < 8
                    } ?? 0
                    samples[badWing]?[badLane]=[]
                    trials=[Trial(wing:badWing,lane:badLane),Trial(wing:badWing,lane:badLane)]; index=0
                    message="Those swings overlapped. Repeat this target with a small, clear change of racket angle."
                    return true
                }
                profile=TennisAimProfile(forehand:forehand,backhand:backhand)
                phase = .validation; trials=Self.validationTrials; index=0; successes=0; wingSuccesses=[0,0]
                message="Your grip is set. Now try fresh targets."
            } else if quick ? successes == 3 : wingSuccesses.allSatisfy({ $0 >= 5 }) {
                phase = .complete; message="Aiming is ready. Your swing mapping stays fixed during the match."
            } else {
                if quick { self=Self(); message="Let's refresh your swing calibration for this session." }
                else {
                    // Recalibrate only the stroke side that failed new-shot validation.
                    let badWing=wingSuccesses[0] < 5 ? 0 : 1
                    samples[badWing]=[:]; phase = .capture
                    trials=[0,-1,1,0,-1,1].map { Trial(wing:badWing,lane:$0) }; index=0
                    message="Let's tune the \(badWing == 0 ? "forehand" : "backhand") again."
                }
            }
        }
        return true
    }
}
