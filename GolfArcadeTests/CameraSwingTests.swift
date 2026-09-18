import XCTest
import simd
@testable import GolfArcade

final class CameraBallFocusTests: XCTestCase {
    func testCloseUpKeepsGroundBallAndNearbyFeetInFrame() {
        let size = CGSize(width: 400, height: 700)
        let ball = CGPoint(x: 0.48, y: 0.16)
        let focus = CameraBallFocus(ball: ball, size: size, active: true)
        func displayed(_ point: CGPoint) -> CGPoint {
            CGPoint(x: size.width / 2 + (point.x - 0.5) * size.width * focus.scale + focus.offset.width,
                    y: size.height / 2 + (0.5 - point.y) * size.height * focus.scale + focus.offset.height)
        }
        XCTAssertEqual(focus.scale, 1.85)
        XCTAssertEqual(displayed(ball).x, size.width * 0.5, accuracy: 0.001)
        for point in [ball, CGPoint(x: 0.29, y: 0.16), CGPoint(x: 0.61, y: 0.17)] {
            let projected = displayed(point)
            XCTAssertTrue((0...size.width).contains(projected.x))
            XCTAssertTrue((0...size.height).contains(projected.y))
        }
    }

    func testCropCannotExposeBlankEdgesAndResetsForSwingView() {
        let size = CGSize(width: 400, height: 700)
        for point in [CGPoint.zero, CGPoint(x: 1, y: 1), CGPoint(x: 0.5, y: 0.05)] {
            let focus = CameraBallFocus(ball: point, size: size, active: true)
            XCTAssertLessThanOrEqual(abs(focus.offset.width), size.width * (focus.scale - 1) / 2)
            XCTAssertLessThanOrEqual(abs(focus.offset.height), size.height * (focus.scale - 1) / 2)
            let normal = CameraBallFocus(ball: point, size: size, active: false)
            XCTAssertEqual(normal.scale, 1)
            XCTAssertEqual(normal.offset, .zero)
        }
        XCTAssertEqual(CameraBallFocus(ball: nil, size: size, active: true).scale, 1)
    }
}

final class ArmSwingDetectorTests: XCTestCase {
    func testManualGroundCorrectionLowersFixedBallNotGrip() throws {
        var detector = ArmSwingDetector()
        _ = drive(&detector, legs: [(0.6, 0)])
        let before = try XCTUnwrap(detector.lockedDisplayAddress)
        detector.adjustGround(by: -0.03)
        let after = try XCTUnwrap(detector.lockedDisplayAddress)
        XCTAssertEqual(after.ball.y, before.ball.y - 0.03, accuracy: 0.00001)
        XCTAssertEqual(after.ball.x, before.ball.x)
        XCTAssertEqual(after.handTarget, before.handTarget)
        XCTAssertTrue(detector.isPositionLocked)
    }

    func testFloorEstimateIsBelowAnklesInFrontOfFeet() throws {
        let points: [BodyJoint: CGPoint] = [.leftShoulder: .init(x: 0.4, y: 0.7), .rightShoulder: .init(x: 0.6, y: 0.7),
            .leftWrist: .init(x: 0.5, y: 0.4), .rightWrist: .init(x: 0.51, y: 0.4),
            .nose: .init(x: 0.5, y: 0.9), .leftAnkle: .init(x: 0.4, y: 0.15), .rightAnkle: .init(x: 0.6, y: 0.15)]
        let frame = PoseFrame(timestamp: 0, points: points.mapValues { PosePoint(location: $0, confidence: 1) })
        let sample = try XCTUnwrap(ArmSwingDetector.Sample(frame: frame))
        XCTAssertLessThan(try XCTUnwrap(sample.floorY), 0.11)
        XCTAssertGreaterThan(try XCTUnwrap(sample.floorY), 0.08)
    }

    func testAssistedContactUsesLocationInsteadOfWhiffs() {
        XCTAssertEqual(CameraContactPolicy.assistedStrike(offset: .zero), .center)
        XCTAssertEqual(CameraContactPolicy.assistedStrike(offset: CGVector(dx: 0, dy: 0.7)), .thin)
        XCTAssertEqual(CameraContactPolicy.assistedStrike(offset: CGVector(dx: 0, dy: -0.7)), .fat)
        XCTAssertEqual(CameraContactPolicy.assistedStrike(offset: CGVector(dx: -0.7, dy: 0)), .heel)
        XCTAssertEqual(CameraContactPolicy.assistedStrike(offset: CGVector(dx: 0.7, dy: 0)), .toe)
        let center = RangeShot(id: 1, club: .iron, power: 0.7, aim: 0, strike: .center)
        for strike in [StrikeQuality.thin, .fat, .heel, .toe] {
            let shot = RangeShot(id: 2, club: .iron, power: 0.7, aim: 0, strike: strike)
            XCTAssertGreaterThan(shot.total, 0)
            XCTAssertLessThan(shot.total, center.total)
        }
    }

    func testAssistedModeSurvivesClubChangesAndDoesNotFireOnIdleOrDropout() {
        var detector = ArmSwingDetector()
        detector.contactMode = .assisted
        detector.configure(for: .iron)
        XCTAssertEqual(detector.contactMode, .assisted)
        XCTAssertTrue(impacts(drive(&detector, legs: [(2, 0)])).isEmpty)
        var gap = ArmSwingDetector()
        gap.contactMode = .assisted
        XCTAssertTrue(impacts(drive(&gap, legs: fullSwing, dropAt: Set(44...65))).isEmpty)
        var valid = ArmSwingDetector()
        valid.contactMode = .assisted
        XCTAssertEqual(impacts(drive(&valid, legs: fullSwing)).count, 1)
    }

    private let fullSwing: [(Double, Double)] = [(0.5, 0), (0.8, 140), (0.15, 140), (0.2, -30), (0.3, -70), (0.4, 0), (0.5, 0)]

    /// Same physical trace can be encoded in different frame aspects, scales and handedness.
    private func drive(_ detector: inout ArmSwingDetector, legs: [(Double, Double)], fps: Double = 30,
                       width: CGFloat = 0.2, aspect: CGFloat = 1, mirror: Double = 1,
                       dropAt: Set<Int> = [], confidence: Double = 1, radialError: CGFloat = 0) -> [SwingInputEvent] {
        var events: [SwingInputEvent] = []
        var time = 0.0, arc = 0.0
        var index = 0
        for (duration, target) in legs {
            let steps = max(1, Int((duration * fps).rounded()))
            let delta = (target - arc) / Double(steps)
            for _ in 0..<steps {
                time += 1 / fps
                arc += delta
                index += 1
                let a = arc * mirror * .pi / 180
                let reach = 1.6 + (time > 1.3 ? radialError : 0)
                let hands = CGPoint(x: 0.5 + sin(a) * width * reach / aspect, y: 0.7 - cos(a) * width * reach)
                var sample = ArmSwingDetector.Sample(time: time, shoulderCenter: CGPoint(x: 0.5, y: 0.7), shoulderWidth: width, hands: hands)
                sample.aspect = aspect
                sample.confidence = confidence
                if let event = detector.ingest(dropAt.contains(index) ? nil : sample, at: time) { events.append(event) }
            }
        }
        return events
    }

    private func impacts(_ events: [SwingInputEvent]) -> [SwingImpact] {
        events.compactMap { if case .impact(let impact) = $0 { impact } else { nil } }
    }

    func testAssistedDeliberateOffCenterSwingConnectsWhereGeometricModeWhiffs() throws {
        var geometric = ArmSwingDetector(), assisted = ArmSwingDetector()
        assisted.contactMode = .assisted
        let miss = try XCTUnwrap(impacts(drive(&geometric, legs: fullSwing, radialError: 0.8)).first)
        let hits = impacts(drive(&assisted, legs: fullSwing, radialError: 0.8))
        let hit = try XCTUnwrap(hits.first)
        XCTAssertEqual(miss.strike, .miss)
        XCTAssertEqual(hits.count, 1)
        XCTAssertNotEqual(hit.strike, .miss)
        XCTAssertNotEqual(hit.strike, .center)
        XCTAssertGreaterThan(RangeShot(id: 1, club: .iron, power: hit.power, aim: hit.startLineDegrees, strike: hit.strike).total, 0)
    }

    func testFullSwingProducesOneContactAndRearms() throws {
        var detector = ArmSwingDetector()
        let events = drive(&detector, legs: fullSwing)
        let hit = try XCTUnwrap(impacts(events).first)
        XCTAssertEqual(impacts(events).count, 1)
        XCTAssertGreaterThan(hit.power, 0.95)
        XCTAssertNotEqual(hit.strike, .miss)
        XCTAssertEqual(hit.source, .camera)
        XCTAssertEqual(hit.curveDegrees, 0, "no invented face angle or preset hook")
        XCTAssertEqual(detector.phase, .address)
    }

    func testFasterDownswingAddsPower() throws {
        var slow = ArmSwingDetector(), fast = ArmSwingDetector()
        let a = try XCTUnwrap(impacts(drive(&slow, legs: [(0.5, 0), (0.8, 110), (0.15, 110), (0.6, -20)])).first)
        let b = try XCTUnwrap(impacts(drive(&fast, legs: [(0.5, 0), (0.8, 110), (0.15, 110), (0.15, -20)])).first)
        XCTAssertGreaterThan(b.power, a.power + 0.15)
    }

    func testGentleEightDegreePuttIsRecognized() throws {
        var detector = ArmSwingDetector()
        detector.configure(for: .putter)
        let hit = try XCTUnwrap(impacts(drive(&detector, legs: [(0.5, 0), (0.8, 8), (0.15, 8), (0.7, -3)])).first)
        XCTAssertLessThan(hit.power, 0.25)
        XCTAssertNotEqual(hit.strike, .miss)
        let shot = RangeShot(id: 1, club: .putter, power: hit.power, aim: hit.startLineDegrees, strike: hit.strike)
        XCTAssertGreaterThan(shot.total, 1, "an eight-degree stroke is a short putt, not a tap")
        XCTAssertLessThan(shot.total, 4, "and it is not a lag")
    }

    func testSlowThirtyDegreePuttIsNotAbsorbedByRecentering() {
        var detector = ArmSwingDetector()
        detector.configure(for: .putter)
        let events = drive(&detector, legs: [(0.5, 0), (1.5, 30), (0.15, 30), (1.2, -5)])
        XCTAssertEqual(impacts(events).count, 1)
    }

    func testOrdinarySmallWagglesDoNotPutt() {
        var detector = ArmSwingDetector()
        detector.configure(for: .putter)
        XCTAssertTrue(impacts(drive(&detector, legs: [(0.5, 0), (0.3, 2), (0.3, -2), (0.3, 1), (0.3, 0)])).isEmpty)
    }

    func testSlowFullPracticeSwingCancels() {
        var detector = ArmSwingDetector()
        let events = drive(&detector, legs: [(0.5, 0), (0.8, 120), (0.2, 120), (2, 0), (0.5, 0)])
        XCTAssertTrue(impacts(events).isEmpty)
        XCTAssertTrue(events.contains(.cancel))
    }

    func testEitherImageDirectionReturnsThroughBallTowardCourse() throws {
        var a = ArmSwingDetector(), b = ArmSwingDetector(), left = ArmSwingDetector()
        left.handedness = .left
        let forward = try XCTUnwrap(impacts(drive(&a, legs: fullSwing)).first)
        let reversed = try XCTUnwrap(impacts(drive(&b, legs: fullSwing, mirror: -1)).first)
        let leftHanded = try XCTUnwrap(impacts(drive(&left, legs: fullSwing, mirror: -1)).first)
        XCTAssertEqual(forward.power, reversed.power, accuracy: 0.0001)
        XCTAssertLessThan(abs(reversed.startLineDegrees), 26, "Camera-side motion must not turn a deliberate downswing into a backward shot")
        XCTAssertEqual(reversed.startLineDegrees, leftHanded.startLineDegrees, accuracy: 0.001)
        XCTAssertEqual(leftHanded.startLineDegrees, -forward.startLineDegrees, accuracy: 0.001)
    }

    func testAspectAndPlayerScaleDoNotChangeTheSamePhysicalSwing() throws {
        var portrait = ArmSwingDetector(), landscape = ArmSwingDetector()
        let a = try XCTUnwrap(impacts(drive(&portrait, legs: fullSwing, width: 0.12, aspect: 9.0 / 16)).first)
        let b = try XCTUnwrap(impacts(drive(&landscape, legs: fullSwing, width: 0.3, aspect: 16.0 / 9)).first)
        XCTAssertEqual(a.power, b.power, accuracy: 0.0001)
        XCTAssertEqual(a.startLineDegrees, b.startLineDegrees, accuracy: 0.0001)
        XCTAssertEqual(a.strike, b.strike)
    }

    func testAbortedBackswingTimesOutAndCanRecover() {
        var detector = ArmSwingDetector()
        let events = drive(&detector, legs: [(0.5, 0), (0.8, 90), (11, 90), (0.6, 0), (0.6, 0)])
        XCTAssertTrue(events.contains(.cancel))
        XCTAssertTrue(impacts(events).isEmpty)
        XCTAssertEqual(detector.phase, .address)
    }

    func testPausedBackswingStillFollowsThroughOnce() {
        for fps in [15.0, 30, 60] {
            var detector = ArmSwingDetector()
            let events = drive(&detector, legs: [(0.5, 0), (1.2, 120), (4, 120), (0.3, -30), (0.6, -60)], fps: fps)
            XCTAssertEqual(impacts(events).count, 1, "A visible pause must not cancel the swing at \(fps) fps")
        }
    }

    func testHighBackswingAcrossAngleWrapDoesNotHitUntilReturning() {
        for mirror in [1.0, -1] {
            var held = ArmSwingDetector()
            XCTAssertTrue(impacts(drive(&held, legs: [(0.5, 0), (1.2, 195), (0.5, 195)], mirror: mirror)).isEmpty)
            XCTAssertEqual(held.phase, .backswing)
            var complete = ArmSwingDetector()
            XCTAssertEqual(impacts(drive(&complete, legs: [(0.5, 0), (1.2, 195), (0.5, 195), (0.4, -30)], mirror: mirror)).count, 1)
        }
    }

    func testLoweringHandsAfterPauseDoesNotLaunch() {
        var detector = ArmSwingDetector()
        XCTAssertTrue(impacts(drive(&detector, legs: [(0.5, 0), (1, 110), (4, 110), (2, 0), (0.6, 0)])).isEmpty)
        XCTAssertEqual(detector.phase, .address)
    }

    func testContactUncertaintyIsNeitherInventedHitNorPunishedMiss() {
        XCTAssertEqual(CameraContactPolicy.classify(distance: 0.1, confidence: 0.5), .hit)
        XCTAssertEqual(CameraContactPolicy.classify(distance: 0.24, confidence: 1), .uncertain)
        XCTAssertEqual(CameraContactPolicy.classify(distance: 0.28, confidence: 0.5), .uncertain)
        XCTAssertEqual(CameraContactPolicy.classify(distance: 0.4, confidence: 0.5), .miss)
        XCTAssertEqual(CameraContactPolicy.classify(distance: 0, confidence: 0.2), .uncertain)
        XCTAssertEqual(CameraContactPolicy.classify(distance: .nan, confidence: 1), .uncertain)
    }

    func testSingleOutlierAtTopPreservesBackswingWithoutUsingItAsContact() {
        var detector = ArmSwingDetector()
        _ = drive(&detector, legs: [(0.5, 0), (0.8, 100)])
        let ball = detector.lockedDisplayAddress
        let spike = ArmSwingDetector.Sample(time: 1.333333, shoulderCenter: CGPoint(x: 0.5, y: 0.7),
                                           shoulderWidth: 0.2, hands: CGPoint(x: 0.5, y: 0.38))
        XCTAssertNil(detector.ingest(spike, at: spike.time))
        XCTAssertEqual(detector.phase, .backswing)
        XCTAssertEqual(detector.readiness, .recovering)
        XCTAssertEqual(detector.lockedDisplayAddress, ball)
    }

    func testReadinessRequiresContinuousStillness() {
        var detector = ArmSwingDetector()
        _ = drive(&detector, legs: [(0.6, 0), (0.1, 15)])
        XCTAssertEqual(detector.phase, .address)
        XCTAssertNil(detector.addressHeldSince)
    }

    func testBriefLossAtTopIsForgivenButLossThroughImpactDoesNotInventContact() {
        var top = ArmSwingDetector(), impact = ArmSwingDetector()
        XCTAssertEqual(impacts(drive(&top, legs: fullSwing, dropAt: Set(36...42))).count, 1)
        XCTAssertTrue(impacts(drive(&impact, legs: fullSwing, dropAt: Set(43...56))).isEmpty)
    }

    func testLongTrackingLossAndLowConfidenceDoNotLaunch() {
        var lost = ArmSwingDetector(), low = ArmSwingDetector()
        let events = drive(&lost, legs: [(0.5, 0), (0.8, 120), (1, 120)], dropAt: Set(40...80))
        XCTAssertTrue(events.contains(.cancel))
        XCTAssertEqual(lost.phase, .findingPlayer)
        XCTAssertTrue(impacts(drive(&low, legs: fullSwing, confidence: 0.3)).isEmpty)
    }

    func testLockedBallDoesNotFollowBodyTranslationOrScale() throws {
        var detector = ArmSwingDetector()
        _ = drive(&detector, legs: [(0.6, 0)])
        let old = try XCTUnwrap(detector.virtualClub)
        let moved = ArmSwingDetector.Sample(time: 1, shoulderCenter: CGPoint(x: 0.65, y: 0.8), shoulderWidth: 0.1, hands: CGPoint(x: 0.65, y: 0.64))
        _ = detector.ingest(moved, at: 1)
        let new = try XCTUnwrap(detector.virtualClub)
        XCTAssertEqual(old.address, new.address)
        XCTAssertEqual(old.space, new.space)
        XCTAssertEqual(old.displayAddress.ball, new.displayAddress.ball)
        XCTAssertNotEqual(old.grip, new.grip, "the hands move relative to the stationary ball")
    }

    func testSweptClubDetectsContactBetweenFramesAndAvatarUsesItsEndpoints() throws {
        let space = SwingSpace(shoulders: CGPoint(x: 0.5, y: 0.7), width: 0.2, aspect: 0.5625)
        let address = VirtualClubAddress(grip: CGPoint(x: 0, y: -1.6), ball: CGPoint(x: 0, y: -3))
        let a = VirtualClubState(time: 0, space: space, address: address, grip: CGPoint(x: 0.5, y: -1.6), angle: 0, confidence: 1)
        let b = VirtualClubState(time: 1.0 / 30, space: space, address: address, grip: CGPoint(x: -0.5, y: -1.6), angle: 0, confidence: 1)
        let contact = try XCTUnwrap(b.sweptContact(from: a, handedness: .right))
        XCTAssertEqual(contact.distance, 0, accuracy: 0.0001)
        XCTAssertEqual(contact.fraction, 0.5, accuracy: 0.0001)
        var avatar = AvatarAnimations.address
        avatar.apply(b, handedness: .right)
        XCTAssertEqual(avatar.clubHead.y, AvatarSize.ball.y, accuracy: 0.0001)
        let scale = simd_length(AvatarAnimations.address.handCenter - AvatarSize.ball) / 1.4
        XCTAssertEqual(avatar.clubHead.z, -0.5 * scale, accuracy: 0.0001)
        XCTAssertEqual(simd_length(avatar[.leftWrist] - avatar[.leftElbow]), AvatarSize.forearm, accuracy: 0.001)
    }

    func testSweptMissStaysAwayFromBallAndLongGapIsRejected() throws {
        let space = SwingSpace(shoulders: .zero, width: 1, aspect: 1)
        let address = VirtualClubAddress(grip: CGPoint(x: 0, y: -1), ball: CGPoint(x: 0, y: -2))
        let a = VirtualClubState(time: 0, space: space, address: address, grip: CGPoint(x: 1, y: 0), angle: 0, confidence: 1)
        let b = VirtualClubState(time: 0.03, space: space, address: address, grip: CGPoint(x: -1, y: 0), angle: 0, confidence: 1)
        XCTAssertEqual(try XCTUnwrap(b.sweptContact(from: a, handedness: .right)).distance, 1, accuracy: 0.0001)
        let gap = VirtualClubState(time: 0.5, space: space, address: address, grip: CGPoint(x: -1, y: -1), angle: 0, confidence: 1)
        XCTAssertNil(gap.sweptContact(from: a, handedness: .right))
    }

    func testFastCenteredSwingIsStableAcrossFrameRates() throws {
        for fps in [15.0, 24, 30, 60] {
            var detector = ArmSwingDetector()
            let hit = try XCTUnwrap(impacts(drive(&detector, legs: fullSwing, fps: fps)).first)
            XCTAssertEqual(hit.strike, .center, "\(fps) fps")
            XCTAssertEqual(hit.startLineDegrees, 0, accuracy: 0.2, "\(fps) fps")
        }
    }

    func testMovingRadiallyAwayDoesNotRevokeCertification() {
        var detector = ArmSwingDetector()
        _ = drive(&detector, legs: [(0.6, 0)])
        for i in 1...20 {
            let time = 0.6 + Double(i) / 30
            let sample = ArmSwingDetector.Sample(time: time, shoulderCenter: CGPoint(x: 0.5, y: 0.7), shoulderWidth: 0.2, hands: CGPoint(x: 0.5, y: 0.2))
            _ = detector.ingest(sample, at: time)
        }
        XCTAssertTrue(detector.isPositionLocked)
        XCTAssertEqual(detector.phase, .address)
        XCTAssertEqual(detector.readiness, .ready)
    }

    func testMissingFramesDuringDownswingCannotReuseOldContact() {
        var detector = ArmSwingDetector()
        let events = drive(&detector, legs: fullSwing, dropAt: Set(47...50))
        XCTAssertTrue(impacts(events).isEmpty)
    }

    func testComfortableOffCenterGripDoesNotBiasNeutralStartLine() throws {
        let space = SwingSpace(shoulders: .zero, width: 1, aspect: 1)
        let address = VirtualClubAddress(grip: CGPoint(x: 0.3, y: -1.6), ball: CGPoint(x: 0.3, y: -3))
        func state(_ degrees: Double, time: Double) -> VirtualClubState {
            let angle = CGFloat(degrees * .pi / 180)
            let cosine = cos(angle)
            let sine = sin(angle)
            let x = address.grip.x * cosine - address.grip.y * sine
            let y = address.grip.x * sine + address.grip.y * cosine
            let grip = CGPoint(x: x, y: y)
            return VirtualClubState(time: time, space: space, address: address, grip: grip, angle: degrees, confidence: 1)
        }
        let contact = try XCTUnwrap(state(-10, time: 0.03).sweptContact(from: state(10, time: 0), handedness: .right))
        XCTAssertLessThan(contact.distance, 0.01)
        XCTAssertEqual(contact.startLine, 0, accuracy: 0.1)
    }

    func testNoisyComfortableGripAcquiresAddressAtMultipleFrameRates() {
        for fps in [15.0, 30, 60] {
            for club in [GolfClub.iron, .putter] {
                var detector = ArmSwingDetector()
                detector.configure(for: club)
                for i in 0..<Int(fps) {
                    let t = Double(i) / fps
                    // +/- 0.02 shoulder widths: enough to break the old per-frame speed gate.
                    let noise = i.isMultiple(of: 2) ? 0.02 : -0.02
                    let sample = ArmSwingDetector.Sample(time: t, shoulderCenter: .zero, shoulderWidth: 1,
                                                         hands: CGPoint(x: noise, y: -1.6 + noise * 0.3))
                    XCTAssertNil(detector.ingest(sample, at: t))
                    if t < detector.stillDuration { XCTAssertLessThan(detector.readyProgress, 1) }
                }
                XCTAssertEqual(detector.phase, .address, "\(fps) fps, \(club)")
                XCTAssertEqual(detector.readiness, .ready)
                XCTAssertEqual(detector.readyProgress, 1)
            }
        }
    }

    func testMovingHandsCannotAccumulateCalibrationTime() {
        var detector = ArmSwingDetector()
        for i in 0..<120 {
            let t = Double(i) / 30
            let sample = ArmSwingDetector.Sample(time: t, shoulderCenter: .zero, shoulderWidth: 1,
                hands: CGPoint(x: 0.3 * t, y: -1.6))
            XCTAssertNil(detector.ingest(sample, at: t))
        }
        XCTAssertEqual(detector.phase, .findingPlayer)
        XCTAssertNil(detector.virtualClub)
        XCTAssertLessThan(detector.readyProgress, 1)
    }

    func testInvalidGripReportsReasonAndMustStartANewHold() {
        var detector = ArmSwingDetector()
        for i in 0..<30 {
            let t = Double(i) / 30
            var sample = ArmSwingDetector.Sample(time: t, shoulderCenter: .zero, shoulderWidth: 1, hands: CGPoint(x: 0, y: -1.6))
            sample.hasComfortableGrip = false
            XCTAssertNil(detector.ingest(sample, at: t))
            XCTAssertEqual(detector.readiness, .handsApart)
            XCTAssertEqual(detector.readyProgress, 0)
        }
        let valid = ArmSwingDetector.Sample(time: 1, shoulderCenter: .zero, shoulderWidth: 1, hands: CGPoint(x: 0, y: -1.6))
        _ = detector.ingest(valid, at: 1)
        XCTAssertEqual(detector.phase, .findingPlayer)
        XCTAssertEqual(detector.readyProgress, 0)
        var low = valid
        low.confidence = 0.3
        _ = detector.ingest(low, at: 1.03)
        XCTAssertEqual(detector.readiness, .lowConfidence)
        XCTAssertNil(detector.virtualClub)
    }

    func testSilentDeliveryGapDoesNotCountAsHoldingStill() {
        var detector = ArmSwingDetector()
        for t in [0.0, 0.1, 1.0] {
            _ = detector.ingest(.init(time: t, shoulderCenter: .zero, shoulderWidth: 1, hands: CGPoint(x: 0, y: -1.6)), at: t)
        }
        XCTAssertEqual(detector.phase, .findingPlayer)
        XCTAssertEqual(detector.readyProgress, 0)
        XCTAssertNil(detector.virtualClub)
    }

    func testResetClearsReadinessAndTheOldClubImmediately() {
        var detector = ArmSwingDetector()
        _ = drive(&detector, legs: [(0.6, 0)])
        XCTAssertEqual(detector.readiness, .ready)
        detector.resetAddress()
        XCTAssertEqual(detector.phase, .findingPlayer)
        XCTAssertEqual(detector.readyProgress, 0)
        XCTAssertNil(detector.virtualClub)
        XCTAssertNil(detector.handsOffset)
    }

    func testSingleFrameJumpCannotBecomeASwing() {
        var detector = ArmSwingDetector()
        let events = drive(&detector, legs: [(0.6, 0), (1.0 / 30, 100), (1.0 / 30, 0), (0.6, 0)])
        XCTAssertTrue(impacts(events).isEmpty)
        XCTAssertTrue(detector.isPositionLocked)
    }

    func testGroundBallIsNotRaisedByNarrowSideOnShoulders() throws {
        var detector = ArmSwingDetector()
        detector.requiresVisibleFeetForSetup = true
        for i in 0..<20 {
            var sample = ArmSwingDetector.Sample(time: Double(i) / 30, shoulderCenter: CGPoint(x: 0.5, y: 0.72),
                shoulderWidth: 0.06, hands: CGPoint(x: 0.52, y: 0.48))
            sample.floorY = 0.08
            _ = detector.ingest(sample, at: sample.time)
        }
        XCTAssertTrue(detector.isPositionLocked)
        let ball = try XCTUnwrap(detector.lockedDisplayAddress?.ball)
        XCTAssertEqual(ball.y, 0.08, accuracy: 0.0001, "the ball must be at measured ground, not capped club length")
        XCTAssertEqual(ball.x, 0.52, accuracy: 0.0001)
    }

    func testFeetNeededOnlyForInitialGroundLock() {
        var detector = ArmSwingDetector()
        detector.requiresVisibleFeetForSetup = true
        _ = drive(&detector, legs: [(0.6, 0)])
        XCTAssertFalse(detector.isPositionLocked)
        XCTAssertEqual(detector.readiness, .feetNotVisible)
        for i in 20..<40 {
            var sample = ArmSwingDetector.Sample(time: Double(i) / 30, shoulderCenter: CGPoint(x: 0.5, y: 0.7),
                shoulderWidth: 0.2, hands: CGPoint(x: 0.5, y: 0.38))
            sample.floorY = 0.08
            _ = detector.ingest(sample, at: sample.time)
        }
        XCTAssertTrue(detector.isPositionLocked)
        let ball = detector.lockedDisplayAddress
        let noFeet = ArmSwingDetector.Sample(time: 1.4, shoulderCenter: CGPoint(x: 0.5, y: 0.7),
            shoulderWidth: 0.2, hands: CGPoint(x: 0.5, y: 0.38))
        _ = detector.ingest(noFeet, at: 1.4)
        XCTAssertEqual(detector.readiness, .ready)
        XCTAssertEqual(detector.lockedDisplayAddress, ball)
    }

    func testOneDroppedImpactFrameCanRecoverFromMeasuredEndpoints() {
        var detector = ArmSwingDetector()
        XCTAssertEqual(impacts(drive(&detector, legs: fullSwing, dropAt: [48])).count, 1)
    }

    func testCameraAspectChangeClearsLockWithoutInventingImpact() {
        var detector = ArmSwingDetector()
        _ = drive(&detector, legs: [(0.6, 0)])
        var turned = ArmSwingDetector.Sample(time: 0.63, shoulderCenter: CGPoint(x: 0.5, y: 0.7),
            shoulderWidth: 0.2, hands: CGPoint(x: 0.5, y: 0.38))
        turned.aspect = 1.7
        XCTAssertNil(detector.ingest(turned, at: 0.63))
        XCTAssertFalse(detector.isPositionLocked)
        XCTAssertNil(detector.lockedDisplayAddress)
        XCTAssertEqual(CameraPoseTracker.deliveredAspect(width: 720, height: 1280), 0.5625)
    }
}

@MainActor
final class CameraReadinessTests: XCTestCase {
    func testRehearsalPrecedesReviewAndNeverScoresForEitherHand() {
        for handedness in Handedness.allCases {
            let camera = CameraSwingController()
            camera.requiresPositionReview = true
            camera.requiresSwingCheck = true
            camera.handedness = handedness
            var events: [SwingInputEvent] = []
            camera.onEvent = { events.append($0) }
            for i in 0..<20 { camera.process(delivery(Double(i) / 30), now: Double(i) / 30) }
            XCTAssertTrue(camera.isCheckingSwing)
            XCTAssertEqual(camera.reviewSecondsRemaining, 0)
            let ball = camera.displayAddress
            let fixture = BenchmarkReplay.fixture().poses
            var time = 0.7
            for pose in fixture {
                time = 0.7 + pose.time
                let frame = PoseFrame(timestamp: time, points: pose.frame!.points)
                camera.process(PoseDelivery(frame: frame, captureTime: time, aspect: 1, bodyCount: 1,
                    callbackStarted: time, inferenceFinished: time), now: time)
            }
            for _ in 0..<20 { time += 1.0 / 30; camera.process(delivery(time), now: time) }
            XCTAssertTrue(camera.swingCheck.isComplete)
            XCTAssertFalse(camera.isCheckingSwing)
            XCTAssertGreaterThan(camera.reviewSecondsRemaining, 0)
            XCTAssertEqual(camera.displayAddress, ball)
            XCTAssertTrue(events.isEmpty, "rehearsal must not emit load, hit or cancellation")
            for _ in 0..<180 { time += 1.0 / 30; camera.process(delivery(time), now: time) }
            XCTAssertTrue(camera.hasPlayableTracking)
            XCTAssertEqual(camera.reviewSecondsRemaining, 0)
            XCTAssertEqual(camera.displayAddress, ball)
            camera.checkSwingAgain()
            XCTAssertTrue(camera.isCheckingSwing)
            XCTAssertFalse(camera.hasPlayableTracking)
            XCTAssertEqual(camera.displayAddress, ball)
        }
    }

    func testSwingCheckRequiresBothSidesAndStableReturnAndResetsAfterLoss() {
        var check = CameraSwingCheck()
        func feed(_ angle: Double, _ time: Double, missing: Bool = false) {
            let frame = delivery(time, missing: missing).frame
            check.ingest(angle: angle, frame: frame,
                sample: frame.flatMap { ArmSwingDetector.Sample(frame: $0) }, at: time)
        }
        feed(-30, 0)
        XCTAssertEqual(check.stage, .takeBack)
        feed(-50, 0.1)
        XCTAssertEqual(check.stage, .swingThrough)
        feed(0, 0.2)
        XCTAssertEqual(check.stage, .swingThrough, "returning from an aborted takeaway is not follow-through")
        feed(25, 0.3)
        XCTAssertEqual(check.stage, .returnToGrip)
        feed(0, 0.4)
        feed(0, 0.6)
        XCTAssertFalse(check.isComplete)
        feed(0, 0.8)
        XCTAssertTrue(check.isComplete)
        check = CameraSwingCheck()
        feed(50, 1)
        feed(0, 1.7, missing: true)
        XCTAssertEqual(check.stage, .takeBack)
        feed(-25, 1.8)
        XCTAssertEqual(check.stage, .takeBack)
    }

    func testHandednessChangeClearsPreviousGripAndReview() {
        let camera = CameraSwingController()
        camera.requiresPositionReview = true
        for i in 0..<20 { camera.process(delivery(Double(i) / 30), now: 100) }
        XCTAssertTrue(camera.isPositionLocked)
        camera.handedness = .left
        XCTAssertFalse(camera.isPositionLocked)
        XCTAssertNil(camera.lockedAddress)
        XCTAssertNil(camera.virtualClub)
        XCTAssertEqual(camera.reviewSecondsRemaining, 0)
        XCTAssertFalse(camera.hasPlayableTracking)
    }

    func testBothHandednessModesFacePhoneWithOppositeSwingGuidance() {
        let right = CameraPlayerStance(handedness: .right), left = CameraPlayerStance(handedness: .left)
        XCTAssertTrue(right.instruction.contains("Face your chest toward the phone"))
        XCTAssertTrue(left.instruction.contains("Face your chest toward the phone"))
        XCTAssertTrue(right.instruction.contains("back to your right"))
        XCTAssertTrue(left.instruction.contains("back to your left"))
        XCTAssertTrue(right.instruction.contains("through to your left"))
        XCTAssertTrue(left.instruction.contains("through to your right"))
    }

    func testWristConfidenceCrossingCannotJumpGripHalfwayAcrossTheHands() throws {
        let fixed = SwingSpace(shoulders: CGPoint(x: 0.5, y: 0.72), width: 0.15, aspect: 0.5625)
        func sample(_ weakConfidence: Float) throws -> ArmSwingDetector.Sample {
            let frame = PoseFrame(timestamp: 0, points: [
                .leftWrist: PosePoint(location: CGPoint(x: 0.44, y: 0.79), confidence: weakConfidence),
                .rightWrist: PosePoint(location: CGPoint(x: 0.35, y: 0.87), confidence: 0.75)
            ])
            return try XCTUnwrap(ArmSwingDetector.Sample(frame: frame, frameAspect: fixed.aspect, certifiedSpace: fixed))
        }
        let before = try sample(0.44), after = try sample(0.46)
        XCTAssertLessThan(hypot(after.hands.x - before.hands.x, after.hands.y - before.hands.y), 0.004,
                          "confidence changes alone must not manufacture a backswing reversal")
    }

    func testCertifiedWristContinuesThroughOccludedShouldersButNotMissingHands() {
        let camera = CameraSwingController()
        for i in 0..<20 { camera.process(delivery(Double(i) / 30)) }
        XCTAssertTrue(camera.isPositionLocked)
        let lockedBall = camera.displayAddress
        let visible = delivery(0.7).frame!
        let turnedFrame = PoseFrame(timestamp: 0.7, points: visible.points.filter {
            $0.key != .leftShoulder && $0.key != .rightShoulder
        })
        let turned = PoseDelivery(frame: turnedFrame, captureTime: 0.7, aspect: 1, bodyCount: 1,
                                  callbackStarted: 0.7, inferenceFinished: 0.7)
        camera.process(turned)
        XCTAssertEqual(camera.readiness, .ready)
        XCTAssertNotNil(camera.virtualClub)
        XCTAssertEqual(camera.displayAddress, lockedBall)
        let noHands = PoseFrame(timestamp: 0.73, points: visible.points.filter {
            $0.key != .leftWrist && $0.key != .rightWrist
        })
        XCTAssertNil(ArmSwingDetector.Sample(frame: noHands, certifiedSpace: SwingSpace(shoulders: .zero, width: 1, aspect: 1)),
                     "certification never invents a hidden hand position")
        XCTAssertNil(ArmSwingDetector.Sample(frame: turned.frame!), "initial setup still requires measured shoulders")
    }

    func testClubIsVisibleWhileFittingButCannotFireBeforeCertification() {
        let camera = CameraSwingController()
        var events: [SwingInputEvent] = []
        camera.onEvent = { events.append($0) }
        camera.process(delivery(0))
        XCTAssertNotNil(camera.virtualClub)
        XCTAssertNotNil(camera.displayAddress)
        XCTAssertTrue(camera.hasVisibleClub)
        XCTAssertFalse(camera.isPositionLocked)
        XCTAssertFalse(camera.hasPlayableTracking)
        XCTAssertTrue(events.isEmpty)
        camera.process(delivery(0.03, missing: true))
        XCTAssertNil(camera.virtualClub, "do not show a fitting guide for unseen hands")
    }

    func testLiveFramesAdvanceReviewWithoutAViewTimer() {
        let camera = CameraSwingController()
        camera.requiresPositionReview = true
        for i in 0..<40 { camera.process(delivery(Double(i) / 60), now: 100 + Double(i) / 60) }
        XCTAssertEqual(camera.reviewSecondsRemaining, 5)
        for i in 40..<360 { camera.process(delivery(Double(i) / 60), now: 100 + Double(i) / 60) }
        XCTAssertTrue(camera.isPositionLocked)
        XCTAssertEqual(camera.reviewSecondsRemaining, 0)
        XCTAssertTrue(camera.hasPlayableTracking)
    }

    private func delivery(_ time: Double, missing: Bool = false) -> PoseDelivery {
        let points: [BodyJoint: CGPoint] = [.leftShoulder: CGPoint(x: 0.4, y: 0.7),
            .rightShoulder: CGPoint(x: 0.6, y: 0.7), .leftWrist: CGPoint(x: 0.49, y: 0.38),
            .rightWrist: CGPoint(x: 0.51, y: 0.38),
            .leftAnkle: CGPoint(x: 0.4, y: 0.1), .rightAnkle: CGPoint(x: 0.6, y: 0.1)]
        let frame = missing ? nil : PoseFrame(timestamp: time, points: points.mapValues { PosePoint(location: $0, confidence: 0.95) })
        return PoseDelivery(frame: frame, captureTime: time, aspect: 1, bodyCount: missing ? 0 : 1,
                            callbackStarted: time, inferenceFinished: time)
    }

    func testReadyUIAndDetectorBecomeReadyTogetherAndResetTogether() {
        let camera = CameraSwingController()
        for i in 0..<20 {
            camera.process(delivery(Double(i) / 30))
            XCTAssertEqual(camera.readyProgress == 1, camera.phase == .address)
            XCTAssertEqual(camera.hasPlayableTracking, camera.phase == .address)
        }
        XCTAssertEqual(camera.readiness, .ready)
        camera.resetAddress()
        XCTAssertEqual(camera.phase, .findingPlayer)
        XCTAssertEqual(camera.readyProgress, 0)
        XCTAssertFalse(camera.hasPlayableTracking)
        XCTAssertNil(camera.virtualClub)
    }

    func testMissingPoseImmediatelyRemovesReadyPresentationDuringGracePeriod() {
        let camera = CameraSwingController()
        for i in 0..<20 { camera.process(delivery(Double(i) / 30)) }
        XCTAssertTrue(camera.hasPlayableTracking)
        camera.process(delivery(0.7, missing: true))
        XCTAssertEqual(camera.readiness, .recovering)
        XCTAssertFalse(camera.hasPlayableTracking)
        XCTAssertEqual(camera.readyProgress, 0)
        XCTAssertNotNil(camera.virtualClub, "a short gap retains the last measured visual club")
        XCTAssertTrue(camera.isPositionLocked)
        XCTAssertNotNil(camera.displayAddress)
    }

    func testPositionReviewLastsFiveSecondsAndDoesNotRepeatAfterDropout() {
        let camera = CameraSwingController()
        camera.requiresPositionReview = true
        var events: [SwingInputEvent] = []
        camera.onEvent = { events.append($0) }
        for i in 0..<20 { camera.process(delivery(Double(i) / 30), now: 100) }
        let locked = camera.displayAddress
        XCTAssertTrue(camera.isPositionLocked)
        XCTAssertEqual(camera.reviewSecondsRemaining, 5)
        XCTAssertFalse(camera.hasPlayableTracking)
        for pose in BenchmarkReplay.fixture().poses {
            camera.process(PoseDelivery(frame: pose.frame, captureTime: pose.time + 1, aspect: pose.aspect, bodyCount: 1,
                callbackStarted: 100, inferenceFinished: 100), now: 101)
        }
        XCTAssertTrue(events.isEmpty, "setup movement cannot fire while reviewing the ball")
        camera.advancePositionReview(at: 104)
        XCTAssertEqual(camera.reviewSecondsRemaining, 1)
        camera.advancePositionReview(at: 105)
        XCTAssertEqual(camera.reviewSecondsRemaining, 0)
        camera.process(delivery(6, missing: true), now: 106)
        camera.process(delivery(6.03), now: 106.03)
        XCTAssertEqual(camera.reviewSecondsRemaining, 0)
        XCTAssertEqual(camera.displayAddress, locked)
        camera.resetAddress()
        XCTAssertFalse(camera.isPositionLocked)
    }

    func testLongAbsenceKeepsBallButDoesNotStayInBriefGapStateForever() {
        let camera = CameraSwingController()
        for i in 0..<20 { camera.process(delivery(Double(i) / 30)) }
        let ball = camera.displayAddress
        for i in 20..<80 { camera.process(delivery(Double(i) / 30, missing: true)) }
        XCTAssertEqual(camera.readiness, .bodyNotVisible)
        XCTAssertNil(camera.virtualClub)
        XCTAssertFalse(camera.hasPlayableTracking)
        XCTAssertEqual(camera.displayAddress, ball)
        camera.process(delivery(2.7))
        XCTAssertEqual(camera.readiness, .ready)
        XCTAssertEqual(camera.displayAddress, ball)
    }

    func testNoisySetupThenIntentionalSwingDeliversExactlyOneImpactAndReasonTelemetry() {
        let camera = CameraSwingController()
        camera.setClub(.iron)
        var impacts: [SwingImpact] = []
        var reasons: Set<String> = []
        camera.onEvent = {
            if case .impact(let impact) = $0 {
                impacts.append(impact)
                XCTAssertTrue(camera.hasVisibleClub, "the avatar must keep the contact frame and follow-through")
            }
        }
        camera.onObservation = { if let reason = $0.readiness { reasons.insert(reason) } }
        for (index, pose) in BenchmarkReplay.fixture().poses.enumerated() {
            var frame = pose.frame!
            if pose.time <= 0.5 {
                let dx = index.isMultiple(of: 2) ? 0.004 : -0.004
                var points = frame.points
                for joint in [BodyJoint.leftWrist, .rightWrist] {
                    let old = points[joint]!
                    points[joint] = PosePoint(location: CGPoint(x: old.location.x + dx, y: old.location.y), confidence: old.confidence)
                }
                frame = PoseFrame(timestamp: pose.time, points: points)
            }
            camera.process(PoseDelivery(frame: frame, captureTime: pose.time, aspect: pose.aspect, bodyCount: 1,
                                        callbackStarted: pose.time, inferenceFinished: pose.time))
        }
        XCTAssertEqual(impacts.count, 1)
        XCTAssertNotEqual(impacts.first?.strike, .miss)
        XCTAssertTrue(reasons.contains("holdStill"))
        XCTAssertTrue(reasons.contains("ready"))
        XCTAssertTrue(reasons.contains("swinging"))
    }
}
