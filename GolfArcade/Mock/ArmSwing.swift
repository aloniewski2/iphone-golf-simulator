import CoreGraphics
import Foundation

/// Signed, aspect-corrected camera motion driving one virtual club and its swept contact.
/// Putting has its own thresholds. Address stays fixed during a stroke, including slow strokes.
struct ArmSwingDetector {
    enum Phase { case findingPlayer, lineUp, address, backswing, downswing, finish }

    enum Readiness: String {
        case bodyNotVisible, handsNotVisible, feetNotVisible, lowConfidence, handsApart, lowerHands
        case returnToGrip, holdStill, ready, swinging, recovering, retry

        var title: String {
            switch self {
            case .bodyNotVisible: "Show your torso and grip"
            case .handsNotVisible: "Bring your hands into view"
            case .feetNotVisible: "Show both feet to place the ball"
            case .lowConfidence: "Tracking is uncertain"
            case .handsApart: "Bring your hands together"
            case .lowerHands: "Lower your grip below your shoulders"
            case .returnToGrip: "Return to your grip or recenter"
            case .holdStill: "Hold your grip briefly"
            case .ready: "Ready. Take it back"
            case .swinging: "Swing through the ball"
            case .recovering: "Brief tracking gap · position kept"
            case .retry: "Contact unclear · retry, no stroke"
            }
        }

        var detail: String {
            switch self {
            case .bodyNotVisible, .handsNotVisible:
                "Face your chest toward the phone with your shoulders and grip visible. Keep the phone upright near waist height."
            case .feetNotVisible:
                "Step back until both feet and the floor are visible. The ball will lock on the ground by your feet."
            case .lowConfidence:
                "Face a light, avoid a bright light behind you, and keep your hands visible."
            case .handsApart, .lowerHands:
                "Use empty hands in a comfortable golf grip. You do not need to reach toward the virtual ball."
            case .returnToGrip:
                "Return to the highlighted grip, or tap Recenter grip to fit your new stance."
            case .holdStill:
                "Keep a relaxed grip for a moment. Wait for Ready before swinging."
            case .ready, .swinging:
                "Take it back, then swing through. Keep a clear space around you."
            case .recovering:
                "Your ball and grip calibration stay locked while tracking reconnects."
            case .retry:
                "The camera could not see enough of contact. Your ball stays put; no stroke is charged."
            }
        }

        static func missingSample(in frame: PoseFrame?) -> Self {
            guard let frame else { return .bodyNotVisible }
            guard frame.point(.leftShoulder) != nil, frame.point(.rightShoulder) != nil else { return .bodyNotVisible }
            let wrists = [BodyJoint.leftWrist, .rightWrist].compactMap { frame.points[$0]?.confidence }
            return wrists.isEmpty ? .handsNotVisible : .lowConfidence
        }
    }

    struct Sample: Equatable {
        let time: Double
        let shoulderCenter: CGPoint
        let shoulderWidth: CGFloat
        let hands: CGPoint
        var aspect: CGFloat = 1
        var confidence: Double = 1
        var floorY: CGFloat?
        var hasComfortableGrip = true

        init?(frame: PoseFrame, frameAspect: CGFloat = 1, certifiedSpace: SwingSpace? = nil) {
            let left = frame.point(.leftShoulder), right = frame.point(.rightShoulder)
            guard certifiedSpace != nil || (left != nil && right != nil) else { return nil }
            // Require a confidently measured wrist, but blend the second wrist continuously.
            // A hard 0.45 inclusion cutoff made the grip jump halfway between wrists and
            // manufactured a reversal when the weaker wrist oscillated around that cutoff.
            let measuredWrists = [BodyJoint.leftWrist, .rightWrist].compactMap { frame.points[$0] }
            guard (measuredWrists.map(\.confidence).max() ?? 0) >= 0.45 else { return nil }
            let leftHand = frame.point(.leftWrist, minimumConfidence: 0.25)
            let rightHand = frame.point(.rightWrist, minimumConfidence: 0.25)
            let hands: CGPoint
            switch (leftHand, rightHand) {
            case let (a?, b?):
                let leftWeight = pow(CGFloat(max(0, (frame.points[.leftWrist]?.confidence ?? 0) - 0.25)), 2)
                let rightWeight = pow(CGFloat(max(0, (frame.points[.rightWrist]?.confidence ?? 0) - 0.25)), 2)
                let total = max(0.0001, leftWeight + rightWeight)
                hands = CGPoint(x: (a.x * leftWeight + b.x * rightWeight) / total,
                                y: (a.y * leftWeight + b.y * rightWeight) / total)
            case let (a?, nil), let (nil, a?): hands = a
            default: return nil
            }
            time = frame.timestamp
            aspect = max(frameAspect, 0.1)
            if let certifiedSpace {
                // Once certified, shoulders define a FIXED coordinate system. A torso turn
                // must not discard a currently measured wrist or rescale its backswing.
                shoulderCenter = certifiedSpace.shoulders
                shoulderWidth = certifiedSpace.width
            } else if let left, let right {
                shoulderCenter = CGPoint(x: (left.x + right.x) / 2, y: (left.y + right.y) / 2)
                shoulderWidth = max(0.02, hypot((right.x - left.x) * aspect, right.y - left.y))
            } else { return nil }
            self.hands = hands
            if let a = leftHand, let b = rightHand {
                hasComfortableGrip = hypot((a.x - b.x) * aspect, a.y - b.y) / shoulderWidth < 0.8
            }
            let shoulders = [BodyJoint.leftShoulder, .rightShoulder].compactMap { frame.points[$0]?.confidence }
            let wrists = [BodyJoint.leftWrist, .rightWrist].compactMap { frame.points[$0]?.confidence }.max() ?? 0
            confidence = certifiedSpace == nil ? Double(min(shoulders.min() ?? 0, wrists)) : Double(wrists)
            let ankles = [BodyJoint.leftAnkle, .rightAnkle].compactMap { frame.point($0)?.y }
            // Put the ground contact in front of the toes, not at the ankle joint.
            // This is a front-camera estimate; the user can adjust it explicitly.
            if ankles.count == 2, let ankleY = ankles.min() {
                floorY = max(0.025, ankleY - (frame.bodyBounds?.height ?? 0.7) * 0.065)
            }
        }

        init(time: Double, shoulderCenter: CGPoint, shoulderWidth: CGFloat, hands: CGPoint) {
            self.time = time
            self.shoulderCenter = shoulderCenter
            self.shoulderWidth = shoulderWidth
            self.hands = hands
        }

        var space: SwingSpace { SwingSpace(shoulders: shoulderCenter, width: max(shoulderWidth, 0.02), aspect: aspect) }
        var grip: CGPoint { space.local(hands) }
    }

    var backswingStart = 20.0
    var fullBackswing = 140.0
    var downswingSpeed = 150.0
    var minimumSwingSpeed = 120.0
    var fullDownswingSpeed = 800.0
    var speedWeight = 0.4
    var stillDuration = 0.35
    var trackingGracePeriod = 0.6
    /// A player may pause at the top. Bound an abandoned attempt without imposing a
    /// three-second total deadline on an otherwise visible, deliberate swing.
    var swingTimeout = 10.0
    var handedness: Handedness = .right
    /// Optional explicit guide; normal play learns the grip from the first comfortable hold.
    var ballAddress: BallAddress?
    var swingsEnabled = true
    var requiresVisibleFeetForSetup = false
    var contactMode: CameraContactPolicy.Mode = .geometric

    private(set) var phase: Phase = .findingPlayer
    private(set) var swingAngle = 0.0
    private(set) var handsOffset: CGVector?
    private(set) var lastStrikeOffset: CGVector?
    private(set) var addressHeldSince: Double?
    private(set) var virtualClub: VirtualClubState?
    /// Visual fitting guide only. Never used by contact detection before certification.
    private(set) var setupClub: VirtualClubState?
    private(set) var readiness: Readiness = .bodyNotVisible
    private(set) var readyProgress = 0.0
    private(set) var addressAimDegrees = 0.0
    private var calibration: VirtualClubAddress?
    private var lockedSpace: SwingSpace?
    private var acquisitionSpace: SwingSpace?
    var isPositionLocked: Bool { calibration != nil && lockedSpace != nil }
    var certifiedSpace: SwingSpace? { isPositionLocked ? lockedSpace : nil }
    var lockedDisplayAddress: BallAddress? {
        guard let calibration, let lockedSpace else { return nil }
        return BallAddress(ball: lockedSpace.image(calibration.ball), handTarget: lockedSpace.image(calibration.grip),
                           shoulderWidth: lockedSpace.width / lockedSpace.aspect)
    }
    private var reference = CGVector(dx: 0, dy: -1)
    private var previous: (time: Double, angle: Double, grip: CGPoint)?
    private var lastTracked: Double?
    private var lastDelivery: Double?
    private var stillSince: Double?
    private var steadyGrips: [(time: Double, grip: CGPoint)] = []
    private var peakArc = 0.0
    private var peakSpeed = 0.0
    private var backSign = 1.0
    private var swingStart = 0.0
    private var bestContact: VirtualClubState.Contact?
    private var isPutt = false
    private var outwardFrames = 0
    private var outwardTravel = 0.0
    private var outwardSign = 0.0
    private var hadTrackingGap = false

    mutating func configure(for club: GolfClub, type: ShotType = .full) {
        let savedCalibration = calibration
        let savedSpace = lockedSpace
        let enabled = swingsEnabled
        let needsFeet = requiresVisibleFeetForSetup
        let savedGuide = ballAddress
        let savedHandedness = handedness
        let savedContactMode = contactMode
        self = ArmSwingDetector()
        calibration = savedCalibration
        lockedSpace = savedSpace
        swingsEnabled = enabled
        requiresVisibleFeetForSetup = needsFeet
        ballAddress = savedGuide
        handedness = savedHandedness
        contactMode = savedContactMode
        isPutt = club == .putter
        if isPutt {
            backswingStart = 2.5
            fullBackswing = 35
            downswingSpeed = 5
            minimumSwingSpeed = 5
            fullDownswingSpeed = 150
            speedWeight = 0.65
        } else if type == .chip || type == .pitch {
            backswingStart = 5
            fullBackswing = type == .chip ? 45 : 85
            downswingSpeed = 20
            minimumSwingSpeed = 15
            fullDownswingSpeed = 400
        }
    }

    mutating func resetAddress() {
        addressAimDegrees = 0
        calibration = nil
        lockedSpace = nil
        acquisitionSpace = nil
        ballAddress = nil
        resetTracking()
    }

    /// Explicit user correction only. Player translation never calls this method.
    mutating func adjustGround(by imageDelta: CGFloat) {
        guard imageDelta.isFinite, let address = calibration, let space = lockedSpace else { return }
        let current = space.image(address.ball)
        let floor = CGPoint(x: current.x, y: min(0.9, max(0.015, current.y + imageDelta)))
        calibration = VirtualClubAddress(grip: address.grip, ball: space.local(floor))
        resetTracking()
    }

    mutating func ingest(_ sample: Sample?, at time: Double) -> SwingInputEvent? {
        guard lastDelivery == nil || time > lastDelivery! else { return nil }
        let deliveryGap = lastDelivery.map { time - $0 } ?? 0
        lastDelivery = time
        guard let sample, sample.confidence >= 0.45 else {
            return rejectObservation(at: time, reason: sample == nil ? .handsNotVisible : .lowConfidence)
        }
        guard previous == nil || time > previous!.time else { return nil }
        // A pause in delivery must not count as a still hold or bridge an old stroke.
        if deliveryGap > trackingGracePeriod || (!isPositionLocked && deliveryGap > 0.15) {
            let wasSwinging = phase == .backswing || phase == .downswing
            resetTracking()
            if wasSwinging { return .cancel }
        }
        if let lockedSpace, abs(lockedSpace.aspect - sample.aspect) > 0.05 {
            let wasSwinging = phase == .backswing || phase == .downswing
            resetAddress() // A real camera orientation change requires a new image-space lock.
            return wasSwinging ? .cancel : nil
        }
        let trackedGap = lastTracked.map { time - $0 } ?? 0
        if acquisitionSpace == nil { acquisitionSpace = sample.space }
        let space = lockedSpace ?? acquisitionSpace ?? sample.space
        let grip = space.local(sample.hands)
        if !isPositionLocked {
            let guideSpace = sample.space
            let guideGrip = guideSpace.local(sample.hands)
            let guide = VirtualClubAddress(grip: guideGrip, ball: proposedBall(sample, grip: guideGrip, space: guideSpace))
            setupClub = VirtualClubState(time: time, space: guideSpace, address: guide,
                grip: guideGrip, angle: 0, confidence: sample.confidence)
        }
        let vector = CGVector(dx: grip.x, dy: grip.y)
        let rawAngle = Self.signedDegrees(between: reference, and: vector)
        // atan2 wraps at +/-180. Crossing that boundary during a high backswing must
        // not look like an instantaneous return through the ball.
        let angle = previous.map { $0.angle + Self.angleDifference(rawAngle, $0.angle) } ?? rawAngle
        let dt = previous.map { time - $0.time } ?? 1.0 / 30
        let speed = previous.map { abs(Self.angleDifference(angle, $0.angle)) / max(dt, 1.0 / 120) } ?? 0
        if isPositionLocked, speed > 1_800 {
            return rejectObservation(at: time, reason: .lowConfidence)
        }
        lastTracked = time
        // Spatial stability over a short window tolerates camera jitter without filtering
        // impact motion or continually recentering a slow putt. An anchored window prevents
        // a wandering grip from accumulating hold time.
        if let first = steadyGrips.first,
           hypot(grip.x - first.grip.x, grip.y - first.grip.y) > 0.08 {
            steadyGrips.removeAll(keepingCapacity: true)
            addressHeldSince = nil
        }
        steadyGrips.append((time, grip))
        steadyGrips = Array(steadyGrips.suffix(60))
        stillSince = steadyGrips.first?.time
        let isStill = stillSince.map { time - $0 >= stillDuration } ?? false
        // Only short, bounded gaps may be interpolated between two actual measured endpoints.
        let oldClub = trackedGap <= 0.12 ? virtualClub : nil
        let oldAngle = previous?.angle ?? angle
        previous = (time, angle, grip)
        swingAngle = angle * (handedness == .right ? 1 : -1)
        if let calibration {
            handsOffset = CGVector(dx: grip.x - calibration.grip.x, dy: grip.y - calibration.grip.y)
            virtualClub = VirtualClubState(time: time, space: space, address: calibration, grip: grip, angle: angle, confidence: sample.confidence)
        }

        if isPositionLocked, !swingsEnabled {
            phase = .address
            readiness = .ready
            readyProgress = 1
            outwardFrames = 0
            outwardTravel = 0
            return nil
        }

        if phase == .backswing || phase == .downswing, time - swingStart >= swingTimeout {
            phase = .finish
            clearHold()
            readiness = .holdStill
            return .cancel
        }

        switch phase {
        case .findingPlayer, .lineUp, .finish:
            if isPositionLocked {
                // Certification is one-time. A stance adjustment never relocates the ball or
                // asks the player to return to an invisible hand target.
                if phase == .finish,
                   (!isStill || sample.hands.y >= sample.shoulderCenter.y - sample.shoulderWidth * 0.2) { return nil }
                phase = .address
                readiness = .ready
                readyProgress = 1
                outwardFrames = 0
                outwardTravel = 0
                return nil
            }
            if requiresVisibleFeetForSetup, sample.floorY == nil {
                readiness = .feetNotVisible
                clearHold()
                return nil
            }
            readiness = !sample.hasComfortableGrip ? .handsApart : vector.dy >= 0 ? .lowerHands : .holdStill
            guard sample.hasComfortableGrip, vector.dy < 0 else {
                clearHold()
                return nil
            }
            if calibration == nil, let ballAddress, !ballAddress.isLinedUp(sample.hands, shoulderWidth: sample.shoulderWidth / sample.aspect) {
                handsOffset = ballAddress.offset(of: sample.hands, shoulderWidth: sample.shoulderWidth / sample.aspect)
                phase = .lineUp
                readiness = .returnToGrip
                clearHold()
                return nil
            }
            readyProgress = min(1, (time - (stillSince ?? time)) / stillDuration)
            guard isStill else { return nil }
            let mean = CGPoint(x: steadyGrips.map { $0.grip.x }.reduce(0, +) / CGFloat(steadyGrips.count),
                               y: steadyGrips.map { $0.grip.y }.reduce(0, +) / CGFloat(steadyGrips.count))
            settle(sample, grip: sample.space.local(space.image(mean)), space: sample.space, at: time)
            return nil

        case .address:
            readiness = .ready
            readyProgress = 1
            if isStill, contactMode == .assisted, let handsOffset, abs(angle) < backswingStart {
                // Deliberate held grip translation is an explicit arcade aiming control,
                // not a measured club-face angle. Freeze it throughout the swing.
                let x = Double(handsOffset.dx)
                addressAimDegrees = max(-12, min(12, (abs(x) < 0.08 ? 0 : x * 18)))
            }
            if isStill, abs(angle) < backswingStart {
                addressHeldSince = addressHeldSince ?? time
            }
            let step = Self.angleDifference(angle, oldAngle)
            let sign = step >= 0 ? 1.0 : -1.0
            if abs(step) < 0.08 || step * angle <= 0 || !sample.hasComfortableGrip || trackedGap > 0.12 {
                outwardFrames = 0
                outwardTravel = 0
            } else {
                if sign != outwardSign { outwardFrames = 0; outwardTravel = 0 }
                outwardSign = sign
                outwardFrames += 1
                outwardTravel += abs(step)
            }
            guard outwardFrames >= 3, outwardTravel > backswingStart,
                  abs(angle) > backswingStart else { return nil }
            hadTrackingGap = false
            phase = .backswing
            readiness = .swinging
            readyProgress = 0
            addressHeldSince = nil
            backSign = angle >= 0 ? 1 : -1
            peakArc = abs(angle)
            peakSpeed = 0
            swingStart = time
            bestContact = nil
            return .load(load(peakArc))

        case .backswing, .downswing:
            readiness = .swinging
            readyProgress = 0
            let arc = angle * backSign
            let closing = Self.angleDifference(angle, oldAngle) * backSign < 0
            peakArc = max(peakArc, arc)
            if phase == .backswing {
                if closing, arc < peakArc - (isPutt ? 1 : min(12, backswingStart * 0.6)), speed >= downswingSpeed {
                    phase = .downswing
                } else if arc < backswingStart, speed < downswingSpeed {
                    phase = .finish
                    clearHold()
                    readiness = .holdStill
                    return .cancel
                } else { return .load(load(peakArc)) }
            }
            peakSpeed = max(peakSpeed, speed)
            // Image-left/right is not course-forward/backward: a camera-side change can
            // reverse it without changing the golfer's handedness. The deliberate backswing
            // establishes this stroke's forward direction; retain signed path deviation.
            let strokeDirection: Handedness = backSign >= 0 ? .right : .left
            if let oldClub, let virtualClub, let contact = virtualClub.sweptContact(from: oldClub, handedness: strokeDirection) {
                if bestContact == nil || contact.distance < bestContact!.distance { bestContact = contact }
            }
            guard arc <= 0 else { return .load(load(peakArc)) }
            phase = .finish
            clearHold()
            readiness = .holdStill
            guard peakSpeed >= minimumSwingSpeed, let contact = bestContact else {
                if hadTrackingGap { readiness = .retry }
                return .cancel
            }
            lastStrikeOffset = contact.offset
            let strike: StrikeQuality
            if contactMode == .assisted {
                // Contact assistance removes geometric whiffs, not the requirement
                // for an observed downswing. Never score across a blind impact gap.
                guard oldClub != nil else { readiness = .retry; return .cancel }
                strike = CameraContactPolicy.assistedStrike(offset: contact.offset)
            } else {
                switch CameraContactPolicy.classify(distance: contact.distance, confidence: contact.confidence) {
                case .hit: strike = .center
                case .miss: strike = .miss
                case .uncertain:
                    readiness = .retry
                    return .cancel
                }
            }
            return .impact(SwingImpact(power: power(arc: peakArc, downswingSpeed: peakSpeed),
                startLineDegrees: contact.startLine, strike: strike, confidence: contact.confidence, source: .camera))
        }
    }

    func power(arc: Double, downswingSpeed: Double) -> Double {
        let length = min(1, max(0, arc / fullBackswing))
        let velocity = min(1, max(0, downswingSpeed / fullDownswingSpeed))
        return length * (1 - speedWeight) + velocity * speedWeight
    }

    private func load(_ arc: Double) -> Double { min(1, max(0, arc / fullBackswing)) }

    private mutating func settle(_ sample: Sample, grip: CGPoint, space: SwingSpace, at time: Double) {
        if calibration == nil {
            lockedSpace = space
            calibration = VirtualClubAddress(grip: grip, ball: proposedBall(sample, grip: grip, space: space))
        }
        setupClub = nil
        let referenceGrip = calibration!.grip
        reference = CGVector(dx: referenceGrip.x, dy: referenceGrip.y)
        let angle = Self.signedDegrees(between: reference, and: CGVector(dx: grip.x, dy: grip.y))
        previous = (time, angle, grip)
        virtualClub = VirtualClubState(time: time, space: space, address: calibration!, grip: grip, angle: angle, confidence: sample.confidence)
        handsOffset = CGVector(dx: grip.x - referenceGrip.x, dy: grip.y - referenceGrip.y)
        addressHeldSince = time
        peakArc = 0
        bestContact = nil
        outwardFrames = 0
        outwardTravel = 0
        acquisitionSpace = nil
        phase = .address
        readiness = .ready
        readyProgress = 1
    }

    private mutating func clearHold() {
        stillSince = nil
        steadyGrips.removeAll(keepingCapacity: true)
        addressHeldSince = nil
        readyProgress = 0
    }

    /// Invalid points and absent points share the same bounded recovery path. One
    /// outlier does not erase a backswing, and it can never become a contact endpoint.
    private mutating func rejectObservation(at time: Double, reason: Readiness) -> SwingInputEvent? {
        setupClub = nil
        clearHold()
        readiness = isPositionLocked ? .recovering : reason
        hadTrackingGap = true
        bestContact = nil
        if isPositionLocked, let lastTracked, time - lastTracked <= trackingGracePeriod { return nil }
        let wasSwinging = phase == .backswing || phase == .downswing
        resetTracking()
        readiness = wasSwinging ? .retry : reason
        return wasSwinging ? .cancel : nil
    }

    private func proposedBall(_ sample: Sample, grip: CGPoint, space: SwingSpace) -> CGPoint {
        if let ballAddress { return space.local(ballAddress.ball) }
        if let floorY = sample.floorY {
            // Never cap measured ground by shoulder width: side-on shoulders are narrow and
            // the old 2.5-width cap put the ball halfway up the player's shin.
            return CGPoint(x: grip.x, y: space.local(CGPoint(x: sample.hands.x, y: floorY)).y)
        }
        let minimumY = space.local(CGPoint(x: sample.hands.x, y: 0.04)).y
        return CGPoint(x: grip.x, y: max(minimumY, grip.y - 1.4))
    }

    private mutating func resetTracking() {
        phase = .findingPlayer
        swingAngle = 0
        previous = nil
        lastTracked = nil
        lastDelivery = nil
        clearHold()
        readiness = .bodyNotVisible
        virtualClub = nil
        setupClub = nil
        handsOffset = nil
        addressHeldSince = nil
        bestContact = nil
        outwardFrames = 0
        outwardTravel = 0
        acquisitionSpace = nil
    }

    static func signedDegrees(between a: CGVector, and b: CGVector) -> Double {
        atan2(a.dx * b.dy - a.dy * b.dx, a.dx * b.dx + a.dy * b.dy) * 180 / .pi
    }

    private static func angleDifference(_ a: Double, _ b: Double) -> Double {
        let delta = (a - b).truncatingRemainder(dividingBy: 360)
        return delta > 180 ? delta - 360 : delta < -180 ? delta + 360 : delta
    }

    static func degrees(between a: CGVector, and b: CGVector) -> Double { abs(signedDegrees(between: a, and: b)) }
}
