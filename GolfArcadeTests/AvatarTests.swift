import CoreGraphics
import SceneKit
import simd
import XCTest
@testable import GolfArcade

final class AvatarTests: XCTestCase {
    func testTwoBoneParallelBendStaysFinite() {
        let arm = AvatarAnimations.twoBone(from: .zero, to: simd_float3(0, 0, 2), upper: 1.35, lower: 1.35, bend: simd_float3(0, 0, 1))
        XCTAssertTrue(arm.joint.x.isFinite && arm.joint.y.isFinite && arm.joint.z.isFinite)
    }

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

    func testCalibratedClubFitsNaturalAddressAndNeverStretchesArms() {
        for side in [Handedness.right, .left] {
            let address = VirtualClubAddress(grip: CGPoint(x: 0.3, y: -1.6), ball: CGPoint(x: 0.3, y: -3))
            let space = SwingSpace(shoulders: .zero, width: 1, aspect: 1)
            for offset in [0.0, 0.4, 4.0] {
                let club = VirtualClubState(time: 0, space: space, address: address,
                    grip: CGPoint(x: address.grip.x + offset, y: address.grip.y), angle: 0, confidence: 1)
                var pose = AvatarAnimations.address
                pose.apply(club, handedness: side)
                XCTAssertEqual(length(pose, .leftShoulder, .leftElbow), AvatarSize.upperArm, accuracy: 0.001)
                XCTAssertEqual(length(pose, .leftElbow, .leftWrist), AvatarSize.forearm, accuracy: 0.001)
                XCTAssertEqual(length(pose, .rightShoulder, .rightElbow), AvatarSize.upperArm, accuracy: 0.001)
                XCTAssertEqual(length(pose, .rightElbow, .rightWrist), AvatarSize.forearm, accuracy: 0.001)
                if offset == 0 {
                    XCTAssertLessThan(simd_distance(pose.handCenter, AvatarAnimations.address.handCenter), 0.15)
                    XCTAssertEqual(pose.clubHead, AvatarSize.ball)
                }
            }
        }
    }

    func testDepthRejectsStaleFutureMissingAndInvalidValues() {
        let estimate = BodyDepthEstimate(timestamp: 1, normalizedDepth: [BodyJoint.leftElbow.rawValue: 0.1,
            BodyJoint.rightElbow.rawValue: .nan, BodyJoint.nose.rawValue: 0.8])
        XCTAssertNotNil(estimate.offset(for: .leftElbow, at: 1.1))
        XCTAssertNil(estimate.offset(for: .leftElbow, at: 1.2))
        XCTAssertNil(estimate.offset(for: .leftElbow, at: 0.9))
        XCTAssertNil(estimate.offset(for: .leftWrist, at: 1))
        XCTAssertNil(estimate.offset(for: .rightElbow, at: 1))
        XCTAssertNil(estimate.offset(for: .nose, at: 1))
    }

    func testReactionsMatchTheShot() {
        var hole = Course.easy.holes[0]
        hole.terrain = .flat
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
        let origin = CoursePoint(x: hole.pin.x, d: hole.pin.d - 6)
        let power = RangeShot.power(toReach: 6.2, with: .putter)!
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


