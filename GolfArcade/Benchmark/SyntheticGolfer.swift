import CoreGraphics
import Foundation
import simd

/// A procedural, full-body golfer swinging the way a real one does, seen through the front
/// camera. It stands in for a person during development and in tests: the same joints Vision
/// reports, moving with a hip and shoulder turn, weight shift, a folding trail arm and a wrist
/// hinge, so the swing detector, the avatar and the stance line all meet human motion instead
/// of hands travelling on a circle.
///
/// Body frame in metres: origin between the feet on the ground, `+y` up, `+z` the golfer's
/// right, `+x` toward the phone. The phone is in front of the golfer, so the target line runs
/// along `z`; a right-hander's target is at `-z`.
struct SyntheticGolfer {
    enum Stroke { case full, putt }

    var handedness: Handedness = .right
    var stroke: Stroke = .full
    /// Degrees the whole stance is turned; positive brings the right side nearer the phone.
    var stanceYaw = 0.0
    /// Seconds the golfer stands still at address before swinging.
    var addressHold = 0.8
    /// Metres from the phone to the golfer's feet.
    var cameraDistance = 2.6
    /// Height of the lens above the ground, in metres.
    var cameraHeight = 1.0
    /// Vertical field of view of the front camera, in degrees.
    var fieldOfView = 68.0
    /// Delivered frame aspect (width / height); portrait phones are narrower than tall.
    var aspect: CGFloat = 0.75
    var framesPerSecond = 30.0
    /// Image-space jitter of a real tracker, in normalized units (standard deviation).
    var jitter = 0.0025
    /// Seed for that jitter; vary it to sample the spread of many takes of the same swing.
    var jitterSeed: UInt64 = 12345
    /// The 3D body pose runs on every third frame, like the tracker.
    var orientationEvery = 3
    /// How far back the swing goes, as a fraction of the authored top: 1 puts the hands over the
    /// trail shoulder like a tour player, 0.75 stops at shoulder height like most amateurs,
    /// 0.5 is a half swing. Putts are unaffected.
    var backswing = 1.0
    /// Speed of the whole motion relative to the authored, tour-like timing: 0.7 is the
    /// leisurely tempo of a weekend golfer, 1.2 a whip.
    var tempo = 1.0
    /// Metres the hands stay high through the downswing before diving at the ball: the
    /// over-the-top move that steepens the attack. 0 retraces the backswing; negative drops
    /// the hands inside for a shallow, in-to-out delivery.
    var overTheTop = 0.0

    // Proportions of a 1.78 m golfer.
    private static let hipHeight: Float = 0.93
    private static let torso: Float = 0.50
    private static let shoulderHalfWidth: Float = 0.20
    private static let hipHalfWidth: Float = 0.14
    private static let upperArm: Float = 0.32
    private static let forearm: Float = 0.30
    private static let thigh: Float = 0.45
    private static let shin: Float = 0.44
    private static let stanceHalfWidth: Float = 0.22

    /// Seconds from the first swing frame to the last; the golfer holds the finish after it.
    var swingDuration: Double { keys.last!.time }
    var duration: Double { addressHold + swingDuration + 0.8 }

    /// One moment of the swing: where the body is and how it is turned.
    struct Key {
        var time: Double
        /// Degrees the shoulders and hips are turned; positive is the backswing direction.
        var shoulderTurn: Double
        var hipTurn: Double
        /// Where the hands are, in the body frame.
        var hands: simd_float3
        /// Forward bend of the spine from vertical, in degrees.
        var spineTilt: Double
        /// Lateral shift of the hips toward the trail side, in metres.
        var weightShift: Float
        /// How much the hips drop below address height, in metres.
        var squat: Float
        /// Lift of the trail heel, in metres.
        var trailHeel: Float
    }

    /// Driver-length full swing: a wide takeaway, a full turn to the top with the hands over the
    /// trail shoulder, hips leading the downswing, the chest facing the target at the finish.
    static let fullSwing: [Key] = [
        Key(time: 0.00, shoulderTurn: 0, hipTurn: 0, hands: simd_float3(0.36, 0.86, -0.03), spineTilt: 28, weightShift: 0, squat: 0, trailHeel: 0),
        Key(time: 0.30, shoulderTurn: 40, hipTurn: 15, hands: simd_float3(0.31, 0.94, 0.40), spineTilt: 28, weightShift: 0.02, squat: 0, trailHeel: 0),
        Key(time: 0.60, shoulderTurn: 72, hipTurn: 32, hands: simd_float3(0.12, 1.35, 0.50), spineTilt: 27, weightShift: 0.04, squat: 0, trailHeel: 0),
        Key(time: 0.90, shoulderTurn: 86, hipTurn: 45, hands: simd_float3(-0.06, 1.66, 0.34), spineTilt: 26, weightShift: 0.06, squat: 0, trailHeel: 0),
        Key(time: 1.00, shoulderTurn: 84, hipTurn: 32, hands: simd_float3(-0.03, 1.62, 0.38), spineTilt: 27, weightShift: 0.03, squat: 0.02, trailHeel: 0),
        Key(time: 1.12, shoulderTurn: 50, hipTurn: 0, hands: simd_float3(0.16, 1.15, 0.46), spineTilt: 28, weightShift: -0.02, squat: 0.03, trailHeel: 0),
        Key(time: 1.24, shoulderTurn: -14, hipTurn: -40, hands: simd_float3(0.36, 0.84, -0.08), spineTilt: 27, weightShift: -0.06, squat: 0.02, trailHeel: 0.02),
        Key(time: 1.38, shoulderTurn: -60, hipTurn: -68, hands: simd_float3(0.34, 0.98, -0.52), spineTilt: 22, weightShift: -0.08, squat: 0, trailHeel: 0.06),
        Key(time: 1.56, shoulderTurn: -95, hipTurn: -85, hands: simd_float3(0.10, 1.48, -0.42), spineTilt: 14, weightShift: -0.10, squat: 0, trailHeel: 0.10),
        Key(time: 1.75, shoulderTurn: -112, hipTurn: -92, hands: simd_float3(-0.08, 1.70, -0.24), spineTilt: 8, weightShift: -0.10, squat: 0, trailHeel: 0.12)
    ]

    /// When the authored full swing reaches the ball; keys before it shrink with `backswing`.
    private static let fullSwingImpactTime = 1.24
    /// The top of the authored full swing; downswing keys between here and impact take `overTheTop`.
    private static let fullSwingTopTime = 1.0

    /// A shoulder-rocked putt: no hip turn, no wrist hinge, the hands sweep a short arc.
    static let putt: [Key] = [
        Key(time: 0.00, shoulderTurn: 0, hipTurn: 0, hands: simd_float3(0.40, 0.76, -0.02), spineTilt: 34, weightShift: 0, squat: 0, trailHeel: 0),
        Key(time: 0.60, shoulderTurn: 6, hipTurn: 1, hands: simd_float3(0.39, 0.77, 0.10), spineTilt: 34, weightShift: 0, squat: 0, trailHeel: 0),
        Key(time: 0.75, shoulderTurn: 6, hipTurn: 1, hands: simd_float3(0.39, 0.77, 0.10), spineTilt: 34, weightShift: 0, squat: 0, trailHeel: 0),
        Key(time: 1.25, shoulderTurn: -5, hipTurn: -1, hands: simd_float3(0.40, 0.77, -0.10), spineTilt: 34, weightShift: 0, squat: 0, trailHeel: 0),
        Key(time: 1.55, shoulderTurn: -6, hipTurn: -1, hands: simd_float3(0.39, 0.78, -0.13), spineTilt: 34, weightShift: 0, squat: 0, trailHeel: 0)
    ]

    // MARK: - Body

    /// The authored timeline adjusted to this golfer: a shorter `backswing` blends the keys up to
    /// mid-downswing toward address (the finish is the same), `tempo` scales every key's time.
    private var keys: [Key] {
        let base = stroke == .full ? Self.fullSwing : Self.putt
        let address = base[0]
        let fraction = min(max(backswing, 0.2), 1.2)
        let rate = min(max(tempo, 0.3), 2)
        return base.map { key in
            var adjusted = key
            adjusted.time = key.time / rate
            if stroke == .full, key.time < Self.fullSwingImpactTime, fraction != 1 {
                adjusted.hands = address.hands + (key.hands - address.hands) * Float(fraction)
                adjusted.shoulderTurn = address.shoulderTurn + (key.shoulderTurn - address.shoulderTurn) * fraction
                adjusted.hipTurn = address.hipTurn + (key.hipTurn - address.hipTurn) * fraction
            }
            if stroke == .full, key.time > Self.fullSwingTopTime, key.time < Self.fullSwingImpactTime {
                adjusted.hands.y += Float(overTheTop)
            }
            return adjusted
        }
    }

    /// The body at `time` seconds, in the body frame, before the stance turn.
    func body(at time: Double) -> [BodyJoint: simd_float3] {
        let key = Self.interpolate(keys, at: time - addressHold)
        let mirror: Float = handedness == .right ? 1 : -1
        let turn = { (degrees: Double) in simd_quatf(angle: Float(-degrees * .pi / 180) * mirror, axis: simd_float3(0, 1, 0)) }
        // Bending forward from the hips brings the chest toward the phone (+x).
        let tilt = simd_quatf(angle: Float(-key.spineTilt * .pi / 180), axis: simd_float3(0, 0, 1))
        var joints: [BodyJoint: simd_float3] = [:]

        let root = simd_float3(0.02, Self.hipHeight - key.squat, key.weightShift * mirror)
        joints[.root] = root
        let hipLine = turn(key.hipTurn).act(simd_float3(0, 0, Self.hipHalfWidth))
        joints[.leftHip] = root - hipLine
        joints[.rightHip] = root + hipLine

        // The spine bends toward the ball; the shoulders turn around that bent axis.
        let chest = root + tilt.act(simd_float3(0, Self.torso, 0))
        let shoulderLine = tilt.act(turn(key.shoulderTurn).act(simd_float3(0, 0, Self.shoulderHalfWidth)))
        joints[.leftShoulder] = chest - shoulderLine
        joints[.rightShoulder] = chest + shoulderLine
        joints[.neck] = chest + tilt.act(simd_float3(0.02, 0.09, 0))
        // The head stays down over the ball through impact, then follows the chest to the finish.
        let headTurn = turn(key.shoulderTurn * (key.shoulderTurn < -40 ? 0.35 : 0.12))
        joints[.nose] = joints[.neck]! + tilt.act(headTurn.act(simd_float3(0.11, 0.16, 0)))

        var hands = key.hands
        hands.z *= mirror
        // Both hands hold one grip: if a keyframe reaches past the arms, bring the grip in
        // toward the chest rather than letting each wrist stop short on its own line.
        let reach = Self.upperArm + Self.forearm - 0.03
        let chestCenter = (joints[.leftShoulder]! + joints[.rightShoulder]!) / 2
        for _ in 0..<12 {
            let farthest = max(simd_distance(hands, joints[.leftShoulder]!), simd_distance(hands, joints[.rightShoulder]!))
            guard farthest > reach else { break }
            hands = chestCenter + (hands - chestCenter) * (reach / farthest) * 0.995
        }
        let gripGap = simd_float3(0, 0, 0.04)
        for (shoulder, elbow, wrist, side) in [
            (BodyJoint.leftShoulder, BodyJoint.leftElbow, BodyJoint.leftWrist, Float(-1)),
            (.rightShoulder, .rightElbow, .rightWrist, 1)
        ] {
            // The lead arm stays long; the trail arm folds outward and back as the club goes up.
            let isTrail = side * mirror > 0
            let fold = isTrail ? simd_float3(-0.6, -0.2, side) : simd_float3(-0.3, -0.7, side * 0.4)
            let target = hands + gripGap * side
            let arm = AvatarAnimations.twoBone(from: joints[shoulder]!, to: target,
                upper: Self.upperArm, lower: Self.forearm, bend: fold)
            joints[elbow] = arm.joint
            joints[wrist] = arm.end
        }

        for (hip, knee, ankle, side) in [
            (BodyJoint.leftHip, BodyJoint.leftKnee, BodyJoint.leftAnkle, Float(-1)),
            (.rightHip, .rightKnee, .rightAnkle, 1)
        ] {
            let isTrail = side * mirror > 0
            var foot = simd_float3(0, 0.08, side * Self.stanceHalfWidth)
            if isTrail { foot += simd_float3(key.trailHeel * 0.6, key.trailHeel, 0) }
            let leg = AvatarAnimations.twoBone(from: joints[hip]!, to: foot, upper: Self.thigh, lower: Self.shin,
                bend: simd_float3(1, 0, side * 0.15))
            joints[knee] = leg.joint
            joints[ankle] = leg.end
        }
        return joints
    }

    /// Cubic Hermite through the keys with Catmull-Rom tangents, so motion flows through each
    /// key instead of stopping at it: the hands are fastest through the ball, not parked on it.
    /// The first and last keys start and end at rest.
    private static func interpolate(_ keys: [Key], at time: Double) -> Key {
        guard let first = keys.first, let last = keys.last else { fatalError("empty timeline") }
        if time <= first.time { return first }
        if time >= last.time { return last }
        var index = 0
        while keys[index + 1].time < time { index += 1 }
        let a = keys[index], b = keys[index + 1]
        let before = index > 0 ? keys[index - 1] : nil
        let after = index + 2 < keys.count ? keys[index + 2] : nil
        let span = b.time - a.time
        let u = (time - a.time) / span
        let h00 = 2 * u * u * u - 3 * u * u + 1, h10 = u * u * u - 2 * u * u + u
        let h01 = -2 * u * u * u + 3 * u * u, h11 = u * u * u - u * u
        func blend(_ value: (Key) -> Double) -> Double {
            let tangentA = before.map { (value(b) - value($0)) / (b.time - $0.time) } ?? 0
            let tangentB = after.map { (value($0) - value(a)) / ($0.time - a.time) } ?? 0
            return h00 * value(a) + h10 * span * tangentA + h01 * value(b) + h11 * span * tangentB
        }
        func blend3(_ value: (Key) -> simd_float3) -> simd_float3 {
            simd_float3(Float(blend { Double(value($0).x) }), Float(blend { Double(value($0).y) }), Float(blend { Double(value($0).z) }))
        }
        return Key(
            time: time,
            shoulderTurn: blend(\.shoulderTurn),
            hipTurn: blend(\.hipTurn),
            hands: blend3(\.hands),
            spineTilt: blend(\.spineTilt),
            weightShift: Float(blend { Double($0.weightShift) }),
            squat: Float(blend { Double($0.squat) }),
            trailHeel: Float(blend { Double($0.trailHeel) })
        )
    }

    // MARK: - Camera

    /// The body as the phone sees it: joints after the stance turn, in the body frame.
    func world(at time: Double) -> [BodyJoint: simd_float3] {
        let stance = simd_quatf(angle: Float(stanceYaw * .pi / 180), axis: simd_float3(0, 1, 0))
        return body(at: time).mapValues { stance.act($0) }
    }

    /// Normalized image point (origin bottom-left, `y` up), as Vision reports it from the
    /// mirrored front camera: the golfer's right appears on the right of the image.
    func project(_ point: simd_float3) -> CGPoint {
        let depth = max(0.3, Float(cameraDistance) - point.x)
        let focal = 0.5 / tan(Float(fieldOfView * .pi / 360))
        return CGPoint(x: 0.5 + CGFloat(point.z / depth * focal) / aspect,
                       y: 0.5 + CGFloat((point.y - Float(cameraHeight)) / depth * focal))
    }

    /// The pose Vision would report for this moment, including the stance line on the frames
    /// the 3D request would run on.
    func frame(at time: Double, index: Int) -> PoseFrame {
        let joints = world(at: time)
        var noise = Noise(seed: UInt64(index) &* 0x9E3779B97F4A7C15 &+ jitterSeed)
        var points: [BodyJoint: PosePoint] = [:]
        // Fixed joint order: dictionary order varies per process and would reshuffle the noise.
        for joint in BodyJoint.allCases {
            guard let position = joints[joint] else { continue }
            let image = project(position)
            let confidence: Float = switch joint {
            case .leftWrist, .rightWrist: 0.86
            case .leftAnkle, .rightAnkle: 0.78
            case .nose: 0.9
            default: 0.94
            }
            points[joint] = PosePoint(location: CGPoint(x: image.x + noise.next() * jitter, y: image.y + noise.next() * jitter),
                                      confidence: confidence)
        }
        var frame = PoseFrame(timestamp: time, points: points)
        // Like the tracker, the last 3D reading rides along on the frames between runs.
        if orientationEvery > 0 {
            let stamped = index - index % orientationEvery
            let stampedTime = Double(stamped) / framesPerSecond
            let body = world(at: stampedTime)
            if let shoulders = Self.yaw(from: body[.leftShoulder]!, to: body[.rightShoulder]!),
               let hips = Self.yaw(from: body[.leftHip]!, to: body[.rightHip]!) {
                frame.orientation = BodyOrientation(timestamp: stampedTime, shoulderYaw: shoulders, hipYaw: hips)
            }
        }
        return frame
    }

    /// Camera-relative yaw of a left-to-right line: positive when the right end is nearer the phone.
    static func yaw(from left: simd_float3, to right: simd_float3) -> Double? {
        let across = right.z - left.z
        guard across > 0.05 else { return nil }
        return Double(atan2(right.x - left.x, across)) * 180 / .pi
    }

    /// The whole performance at the configured frame rate, ready for the replay and fixture paths.
    func poses() -> [BenchmarkPose] {
        let count = Int((duration * framesPerSecond).rounded())
        return (0..<count).map { index in
            let time = Double(index) / framesPerSecond
            return BenchmarkPose(frame: frame(at: time, index: index), time: time, aspect: aspect)
        }
    }

    /// Deterministic Gaussian-ish noise so a run is repeatable frame for frame.
    private struct Noise {
        private var state: UInt64
        init(seed: UInt64) { state = seed | 1 }
        private mutating func uniform() -> CGFloat {
            state = state &* 6364136223846793005 &+ 1442695040888963407
            return CGFloat(state >> 11) / CGFloat(1 << 53)
        }
        mutating func next() -> CGFloat { (uniform() + uniform() + uniform()) * 2 - 3 }
    }
}
