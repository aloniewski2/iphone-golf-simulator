import CoreGraphics
import simd
import XCTest
@testable import GolfArcade

final class AvatarTests: XCTestCase {
    private func length(_ pose: BodyPose3D, _ a: BodyJoint, _ b: BodyJoint) -> Float { simd_length(pose[a] - pose[b]) }

    func testCannedSwingKeepsLimbLengthsAndTravelsThroughTheArc() {
        let address = AvatarAnimations.swingArc(degrees: 0)
        let top = AvatarAnimations.swingArc(degrees: 150)
        let finish = AvatarAnimations.swingArc(degrees: -150)
        for pose in [address, top, finish] {
            XCTAssertEqual(length(pose, .leftShoulder, .leftElbow), AvatarSize.upperArm, accuracy: 0.01)
            XCTAssertEqual(length(pose, .rightElbow, .rightWrist), AvatarSize.forearm, accuracy: 0.01)
            XCTAssertEqual(length(pose, .leftHip, .leftKnee), AvatarSize.thigh, accuracy: 0.01)
        }
        XCTAssertGreaterThan(top.handCenter.y, address.handCenter.y + 2, "hands rise to the top")
        XCTAssertGreaterThan(finish.handCenter.y, address.handCenter.y + 2, "a high finish")
        XCTAssertLessThan(simd_length(address.clubHead - AvatarSize.ball), 1.5, "at address the club head is at the ball")
    }

    /// A front-facing golfer in normalized image coordinates: shoulders 0.2 wide at y 0.72.
    private func frame(time: Double, hands: CGPoint, elbowOut: CGFloat = 0.02, dropping: Set<BodyJoint> = []) -> PoseFrame {
        var points: [BodyJoint: CGPoint] = [
            .nose: CGPoint(x: 0.5, y: 0.86), .neck: CGPoint(x: 0.5, y: 0.75),
            .leftShoulder: CGPoint(x: 0.4, y: 0.72), .rightShoulder: CGPoint(x: 0.6, y: 0.72),
            .leftHip: CGPoint(x: 0.44, y: 0.45), .rightHip: CGPoint(x: 0.56, y: 0.45), .root: CGPoint(x: 0.5, y: 0.45),
            .leftKnee: CGPoint(x: 0.43, y: 0.27), .rightKnee: CGPoint(x: 0.57, y: 0.27),
            .leftAnkle: CGPoint(x: 0.42, y: 0.1), .rightAnkle: CGPoint(x: 0.58, y: 0.1)
        ]
        points[.leftWrist] = CGPoint(x: hands.x - 0.015, y: hands.y)
        points[.rightWrist] = CGPoint(x: hands.x + 0.015, y: hands.y)
        points[.leftElbow] = CGPoint(x: (0.4 + hands.x) / 2 - elbowOut, y: (0.72 + hands.y) / 2)
        points[.rightElbow] = CGPoint(x: (0.6 + hands.x) / 2 + elbowOut, y: (0.72 + hands.y) / 2)
        for joint in dropping { points[joint] = nil }
        return PoseFrame(timestamp: time, points: points.mapValues { PosePoint(location: $0, confidence: 0.9) })
    }

    private var calibration: PlayerCalibration {
        PlayerCalibration(signature: PlayerCalibration.uiTestingFixture.signature, anchor: BodyAnchor(shoulderCenterX: 0.5, shoulderCenterY: 0.72, height: 0.76))
    }

    func testRetargetedSwingKeepsBonesAndReachesTowardTheBall() {
        var retargeter = PoseRetargeter(calibration: calibration, handedness: .right, frameAspect: 0.75)
        for i in 0..<90 {
            let t = Double(i) / 30
            let angle = sin(t * 2) * 2.4
            let hands = CGPoint(x: 0.5 + sin(angle) * 0.3, y: 0.72 - cos(angle) * 0.3)
            let pose = retargeter.update(frame(time: t, hands: hands), swingAngle: Double(angle) * 57, at: t)
            XCTAssertEqual(length(pose, .leftShoulder, .leftElbow), AvatarSize.upperArm, accuracy: 0.02)
            XCTAssertEqual(length(pose, .rightElbow, .rightWrist), AvatarSize.forearm, accuracy: 0.02)
            XCTAssertEqual(length(pose, .root, .neck), AvatarSize.torso, accuracy: 0.02)
            XCTAssertGreaterThanOrEqual(pose[.leftWrist].x, pose[.leftShoulder].x - 0.01, "hands never go behind the body")
            XCTAssertEqual(min(pose[.leftAnkle].y, pose[.rightAnkle].y), 0.12, accuracy: 0.01, "feet stay on the ground")
        }
    }

    func testHandsOutToTheTrailSideMirrorForLeftHanders() {
        let trailSide = frame(time: 0, hands: CGPoint(x: 0.75, y: 0.62))
        var right = PoseRetargeter(calibration: calibration, handedness: .right, frameAspect: 0.75)
        var left = PoseRetargeter(calibration: calibration, handedness: .left, frameAspect: 0.75)
        let rightPose = right.update(trailSide, swingAngle: 90, at: 0)
        let leftPose = left.update(trailSide, swingAngle: 90, at: 0)
        XCTAssertGreaterThan(rightPose.handCenter.z, 1)
        XCTAssertEqual(leftPose.handCenter.z, -rightPose.handCenter.z, accuracy: 0.01)
    }

    func testLostWristsHoldThenEaseBackToAddress() {
        var retargeter = PoseRetargeter(calibration: calibration, handedness: .right, frameAspect: 0.75)
        var held = BodyPose3D.lerp(.init(joints: [:], clubDirection: .zero), .init(joints: [:], clubDirection: .zero), 0)
        for i in 0..<20 {
            held = retargeter.update(frame(time: Double(i) / 30, hands: CGPoint(x: 0.78, y: 0.9)), swingAngle: 150, at: Double(i) / 30)
        }
        let top = held[.leftWrist]
        let arms: Set<BodyJoint> = [.leftWrist, .rightWrist, .leftElbow, .rightElbow]
        let lost = retargeter.update(frame(time: 0.8, hands: .zero, dropping: arms), swingAngle: 150, at: 0.8)
        XCTAssertLessThan(simd_length(lost[.leftWrist] - top), 0.3, "a brief dropout holds the hands")
        var later = lost
        for i in 0..<40 {
            let t = 1.8 + Double(i) / 30
            later = retargeter.update(frame(time: t, hands: .zero, dropping: arms), swingAngle: 0, at: t)
        }
        XCTAssertLessThan(simd_length(later[.leftWrist] - AvatarAnimations.address[.leftWrist]), 1.5, "a long dropout eases back toward address")
    }

    func testClubHeadRestsOnTheBallAtAddress() {
        let hands = simd_float3(1.9, 2.1, 0.1)
        let direction3D = ClubGeometry.direction3D(hands: hands, shoulders: simd_float3(0.7, 4.4, 0), ball: AvatarSize.ball, swingAngle: 0)
        XCTAssertEqual(simd_dot(direction3D, simd_normalize(AvatarSize.ball - hands)), 1, accuracy: 0.0001)

        let address = BallAddress(calibration: .uiTestingFixture, handedness: .right)
        let direction2D = ClubGeometry.direction2D(hands: address.handTarget, shoulders: CGPoint(x: 0.5, y: 0.72), ball: address.ball, swingAngle: 0, handedness: .right)
        let length = hypot(address.ball.x - address.handTarget.x, address.ball.y - address.handTarget.y)
        XCTAssertEqual(address.handTarget.x + direction2D.dx * length, address.ball.x, accuracy: 0.001)
        XCTAssertEqual(address.handTarget.y + direction2D.dy * length, address.ball.y, accuracy: 0.001)

        let top = ClubGeometry.direction3D(hands: simd_float3(1, 5, 2), shoulders: simd_float3(0.7, 4.4, 0), ball: AvatarSize.ball, swingAngle: 150)
        XCTAssertGreaterThan(top.y, 0.5, "the club cocks up at the top of the backswing")
    }

    func testReactionsMatchTheShot() {
        let hole = Course.easy.holes[0]
        let pure = RangeShot(id: 1, club: .iron, power: 0.62, aim: 0, hole: hole)
        XCTAssertFalse(pure.isHoled)
        XCTAssertEqual(AvatarAnimations.Reaction.classify(pure), .pure)
        let fat = RangeShot(id: 2, club: .iron, power: 0.8, aim: 0, strike: .fat, hole: hole)
        XCTAssertEqual(AvatarAnimations.Reaction.classify(fat), .bad)
        let whiff = RangeShot(id: 3, club: .iron, power: 0.8, aim: 0, strike: .miss, hole: hole)
        XCTAssertEqual(AvatarAnimations.Reaction.classify(whiff), .disaster)
        let wide = RangeShot(id: 4, club: .iron, power: 0.9, aim: 22, curve: 12, hole: hole)
        XCTAssertTrue([.meh, .disaster].contains(AvatarAnimations.Reaction.classify(wide)))
        let heel = RangeShot(id: 5, club: .wedge, power: 0.6, aim: 0, strike: .heel, hole: hole)
        XCTAssertTrue([.solid, .meh].contains(AvatarAnimations.Reaction.classify(heel)))
        let origin = CoursePoint(x: 0, d: hole.pin.d - 6)
        let power = RangeShot.power(toReach: 7, with: .putter)!
        let holed = RangeShot(id: 6, club: .putter, power: power, aim: 0, origin: origin, heading: origin.heading(to: hole.pin), hole: hole)
        XCTAssertEqual(AvatarAnimations.Reaction.classify(holed), .holed)
        for kind in AvatarAnimations.Reaction.allCases {
            let pose = AvatarAnimations.reaction(kind, time: 1)
            XCTAssertEqual(length(pose, .leftShoulder, .leftElbow), AvatarSize.upperArm, accuracy: 0.02, "\(kind)")
        }
    }

    func testKnockdownFallsAwayAndGetsBackUp() {
        let push = simd_float3(0, 0, -1)
        let down = AvatarAnimations.knockdown(time: 0.8, push: push)
        XCTAssertLessThan(down.lean.act(simd_float3(0, 1, 0)).z, -0.8, "lying down in the push direction")
        let up = AvatarAnimations.knockdown(time: AvatarAnimations.knockdownLength + 0.01, push: push)
        XCTAssertGreaterThan(up.lean.act(simd_float3(0, 1, 0)).y, 0.99)
    }
}

final class ShotCameraTests: XCTestCase {
    private func inputs(shot: RangeShot?, elapsed: Double, onGreen: Bool = false, reaction: AvatarAnimations.Reaction? = .pure) -> ShotCameraDirector.Inputs {
        ShotCameraDirector.Inputs(
            ball: .zero, heading: 0, aim: 0, distanceToPin: 140, onGreen: onGreen, handedness: .right,
            shot: shot, elapsed: elapsed, reaction: reaction, landingTime: shot.flatMap(ShotCameraDirector.landingTime)
        )
    }

    func testShotSequence() {
        XCTAssertEqual(ShotCameraDirector.stage(inputs(shot: nil, elapsed: 0)), .address)
        XCTAssertEqual(ShotCameraDirector.stage(inputs(shot: nil, elapsed: 0, onGreen: true)), .green)
        let drive = RangeShot(id: 1, club: .driver, power: 0.9, aim: 0)
        XCTAssertEqual(ShotCameraDirector.stage(inputs(shot: drive, elapsed: 0.5)), .hero)
        XCTAssertEqual(ShotCameraDirector.stage(inputs(shot: drive, elapsed: 2.5)), .chase)
        XCTAssertEqual(ShotCameraDirector.stage(inputs(shot: drive, elapsed: drive.duration - 0.1)), .landing)
        XCTAssertEqual(ShotCameraDirector.stage(inputs(shot: drive, elapsed: 1.3, reaction: .bad)), .chase, "bad swings get a shorter hero shot")
        let putt = RangeShot(id: 2, club: .putter, power: 0.4, aim: 0)
        XCTAssertEqual(ShotCameraDirector.stage(inputs(shot: putt, elapsed: 0.1)), .chase, "putts skip the hero shot")
    }

    func testFramings() {
        let address = ShotCameraDirector.shot(inputs(shot: nil, elapsed: 0))
        XCTAssertGreaterThan(address.position.z, 5, "behind the golfer")
        XCTAssertLessThan(address.lookAt.z, -20, "looking down the hole")
        let green = ShotCameraDirector.shot(inputs(shot: nil, elapsed: 0, onGreen: true))
        XCTAssertLessThan(green.position.y, address.position.y, "lower on the green")
        XCTAssertLessThan(green.position.z, address.position.z, "and closer to the ball")
        let drive = RangeShot(id: 1, club: .driver, power: 0.9, aim: 0)
        let hero = ShotCameraDirector.shot(inputs(shot: drive, elapsed: 0.2))
        XCTAssertLessThan(hero.position.z, 0, "in front of the golfer on the target side")
        XCTAssertLessThan(simd_length(hero.lookAt - simd_float3(-2.6, 3.2, 0)), 0.5, "framing the golfer")
        let chase = ShotCameraDirector.shot(inputs(shot: drive, elapsed: 3))
        let ball = ShotCameraDirector.scenePoint(drive.position(at: 3))
        XCTAssertGreaterThan(chase.position.z, ball.z, "behind the ball")
        XCTAssertLessThan(simd_length(chase.position - ball), 14, "close to the ball")
        // A heading rotates the whole stance frame.
        let turned = ShotCameraDirector.world(simd_float3(0, 0, -10), origin: CoursePoint(x: 5, d: 20), heading: 90)
        XCTAssertEqual(turned.x, 15, accuracy: 0.001)
        XCTAssertEqual(turned.z, -20, accuracy: 0.001)
    }
}

final class ClubContactTests: XCTestCase {
    private let friend = ClubContact.Target(id: UUID(), base: simd_float3(0, 0.8, -5), top: simd_float3(0, 5.2, -5))

    func testFastClubKnocksOverAndCoolsDown() {
        var contact = ClubContact()
        XCTAssertTrue(contact.update(hands: simd_float3(0, 3, -1), head: simd_float3(0, 3, -2), at: 0, targets: [friend]).isEmpty)
        let hits = contact.update(hands: simd_float3(0, 3, -2.5), head: simd_float3(0, 3, -5.2), at: 0.05, targets: [friend])
        XCTAssertEqual(hits.map(\.id), [friend.id])
        XCTAssertLessThan(hits[0].push.z, -0.9)
        XCTAssertTrue(contact.update(hands: simd_float3(0, 3, -1), head: simd_float3(0, 3, -4.9), at: 0.1, targets: [friend]).isEmpty, "cooldown")
    }

    func testSlowTouchOrMissDoesNothing() {
        var slow = ClubContact()
        _ = slow.update(hands: simd_float3(0, 3, -2.5), head: simd_float3(0, 3, -4.9), at: 0, targets: [friend])
        XCTAssertTrue(slow.update(hands: simd_float3(0, 3, -2.5), head: simd_float3(0, 3, -5.0), at: 0.5, targets: [friend]).isEmpty)
        var wide = ClubContact()
        _ = wide.update(hands: simd_float3(4, 3, -1), head: simd_float3(4, 3, -2), at: 0, targets: [friend])
        XCTAssertTrue(wide.update(hands: simd_float3(4, 3, -2.5), head: simd_float3(4, 3, -5.2), at: 0.05, targets: [friend]).isEmpty)
    }

    func testRecorderFreezesAfterImpact() {
        var recorder = SwingRecorder()
        let address = AvatarAnimations.address, finish = AvatarAnimations.finish
        for i in 0..<30 { recorder.record(i < 15 ? address : finish, at: Double(i) / 10) }
        recorder.markImpact(at: 1.5)
        for i in 30..<60 { recorder.record(address, at: Double(i) / 10) }
        XCTAssertTrue(recorder.isFrozen)
        XCTAssertEqual(recorder.pose(atImpactOffset: 0.5), finish, "frames after the freeze are not kept")
        XCTAssertNil(recorder.pose(atImpactOffset: 3))
    }
}
