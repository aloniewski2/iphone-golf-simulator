import CoreGraphics
import SceneKit
import simd
import XCTest
@testable import GolfArcade

final class AvatarTests: XCTestCase {
    func testClubFaceAimsDownCourseNotAlongShaftForBothStances() {
        let orientation = ClubGeometry.headOrientation(shaftUp: simd_float3(-1.3, 2, 0))
        let forward = orientation.act(simd_float3(0, 0, -1))
        for hand in [Handedness.right, .left] {
            let mirror = GolferStance(handedness: hand).mirror
            let worldForward = simd_float3(forward.x * mirror, forward.y, forward.z)
            XCTAssertEqual(worldForward.z, -1, accuracy: 0.0001)
            XCTAssertEqual(worldForward.x, 0, accuracy: 0.0001)
        }
        XCTAssertEqual(CameraPlayerStance(handedness: .right).imageShotDirection, -1)
        XCTAssertEqual(CameraPlayerStance(handedness: .left).imageShotDirection, 1)
    }

    func testCameraPresentationRejectsInvertedHeadAndKeepsBoneLengths() {
        var filter = CameraAvatarPoseFilter()
        var observed = AvatarAnimations.address
        observed.joints[.nose] = observed[.neck] + simd_float3(0, -4, 3)
        observed.joints[.leftWrist] = simd_float3(20, 8, -10)
        observed.virtualClubHead = AvatarSize.ball
        let pose = filter.update(observed, frame: frame(time: 0, hands: CGPoint(x: 0.5, y: 0.4)), at: 0)
        XCTAssertGreaterThan(pose[.nose].y, pose[.neck].y + 0.5)
        XCTAssertEqual(length(pose, .neck, .nose), AvatarSize.neckToHead, accuracy: 0.001)
        XCTAssertEqual(length(pose, .leftShoulder, .leftElbow), AvatarSize.upperArm, accuracy: 0.001)
        XCTAssertEqual(length(pose, .leftElbow, .leftWrist), AvatarSize.forearm, accuracy: 0.001)
        XCTAssertEqual(length(pose, .leftHip, .leftKnee), AvatarSize.thigh, accuracy: 0.001)
        XCTAssertEqual(pose.clubHead, AvatarSize.ball, "anatomical repair cannot relocate contact")
    }

    func testPartialBodyCannotContortPreviouslyCoherentAvatar() {
        var filter = CameraAvatarPoseFilter()
        let full = frame(time: 0, hands: CGPoint(x: 0.5, y: 0.4))
        let good = filter.update(AvatarAnimations.address, frame: full, at: 0)
        var corrupt = AvatarAnimations.address
        corrupt.joints[.nose] = simd_float3(0, -100, 0)
        let partial = PoseFrame(timestamp: 0.03, points: [.nose: PosePoint(location: .zero, confidence: 1)])
        let result = filter.update(corrupt, frame: partial, at: 0.03)
        XCTAssertEqual(result.joints, good.joints)
        XCTAssertFalse(result.clubVisible)
    }

    func testTwoBoneParallelBendStaysFinite() {
        let arm = AvatarAnimations.twoBone(from: .zero, to: simd_float3(0, 0, 2), upper: 1.35, lower: 1.35, bend: simd_float3(0, 0, 1))
        XCTAssertTrue(arm.joint.x.isFinite && arm.joint.y.isFinite && arm.joint.z.isFinite)
    }

    @MainActor
    func testDetailedRigReusesMeshesAndKeepsFiniteTrackedTransforms() {
        let rig = AvatarRig(shirt: .systemOrange)
        func nodes() -> [SCNNode] {
            var result: [SCNNode] = []
            rig.node.enumerateChildNodes { node, _ in result.append(node) }
            return result
        }
        let original = nodes().map(ObjectIdentifier.init)
        XCTAssertNotNil(rig.node.childNode(withName: "tailoredPolo", recursively: true))
        XCTAssertNotNil(rig.node.childNode(withName: "golferFace", recursively: true))
        XCTAssertNotNil(rig.node.childNode(withName: "golfCap", recursively: true))
        XCTAssertLessThan(original.count, 100)
        for mirrored in [false, true] {
            rig.setMirrored(mirrored)
            for angle in stride(from: -180.0, through: 180, by: 5) {
                let pose = AvatarAnimations.swingArc(degrees: angle)
                rig.apply(pose)
                XCTAssertEqual(rig.node.childNode(withName: "golferFace", recursively: true)?.simdPosition, pose[.nose])
                for node in nodes() {
                    for value in [node.simdPosition.x, node.simdPosition.y, node.simdPosition.z,
                                  node.simdOrientation.vector.x, node.simdOrientation.vector.y,
                                  node.simdOrientation.vector.z, node.simdOrientation.vector.w] {
                        XCTAssertTrue(value.isFinite)
                    }
                }
            }
        }
        XCTAssertEqual(nodes().map(ObjectIdentifier.init), original, "posing must not allocate new scene nodes")
    }

    func testOppositeCameraSwingsMirrorWholeGolferAndMeetSameBall() {
        let space = SwingSpace(shoulders: CGPoint(x: 0.5, y: 0.72), width: 0.2, aspect: 1)
        let address = VirtualClubAddress(grip: CGPoint(x: 0.1, y: -1.5), ball: CGPoint(x: 0.1, y: -2.8))
        let mirroredAddress = VirtualClubAddress(grip: CGPoint(x: -0.1, y: -1.5), ball: CGPoint(x: -0.1, y: -2.8))
        var right = PoseRetargeter(calibration: calibration, handedness: .right, frameAspect: 1)
        var left = PoseRetargeter(calibration: calibration, handedness: .left, frameAspect: 1)
        let rightStance = GolferStance(handedness: .right), leftStance = GolferStance(handedness: .left)
        for (index, angle) in [0.0, 110, 0, -65].enumerated() {
            let a = angle * .pi / 180
            let grip = CGPoint(x: address.grip.x * cos(a) - address.grip.y * sin(a),
                               y: address.grip.x * sin(a) + address.grip.y * cos(a))
            let club = VirtualClubState(time: Double(index), space: space, address: address, grip: grip, angle: angle, confidence: 1)
            let mirrorClub = VirtualClubState(time: Double(index), space: space, address: mirroredAddress,
                grip: CGPoint(x: -grip.x, y: grip.y), angle: -angle, confidence: 1)
            let observed = frame(time: Double(index), hands: space.image(grip))
            let mirrorFrame = PoseFrame(timestamp: Double(index), points: observed.points.mapValues {
                PosePoint(location: CGPoint(x: 1 - $0.location.x, y: $0.location.y), confidence: $0.confidence)
            })
            let r = right.updateCamera(observed, club: club, positionLocked: true, swingAngle: angle, at: Double(index))
            let l = left.updateCamera(mirrorFrame, club: mirrorClub, positionLocked: true, swingAngle: angle, at: Double(index))
            let pairs = BodyJoint.allCases.map { (r[$0], l[$0]) } + [(r.clubGrip, l.clubGrip), (r.clubHead, l.clubHead)]
            for (rp, lp) in pairs {
                let worldR = rightStance.coursePoint(rp), worldL = leftStance.coursePoint(lp)
                XCTAssertEqual(worldL.x, -worldR.x, accuracy: 0.0001)
                XCTAssertEqual(worldL.y, worldR.y, accuracy: 0.0001)
                XCTAssertEqual(worldL.z, worldR.z, accuracy: 0.0001)
            }
            if angle == 0 {
                XCTAssertLessThan(simd_distance(rightStance.coursePoint(r.clubHead), leftStance.coursePoint(l.clubHead)), 0.0001)
            }
        }
        XCTAssertLessThan(rightStance.position.x, 0)
        XCTAssertGreaterThan(leftStance.position.x, 0)
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

    func testForeshortenedBodyCannotInventExtremeForwardReach() {
        let original = frame(time: 0, hands: CGPoint(x: 0.5, y: 0.5))
        let short = PoseFrame(timestamp: 0, points: original.points.mapValues {
            PosePoint(location: CGPoint(x: 0.5 + ($0.location.x - 0.5) * 0.4,
                                        y: 0.45 + ($0.location.y - 0.45) * 0.4), confidence: $0.confidence)
        })
        var retargeter = PoseRetargeter(calibration: calibration, handedness: .right, frameAspect: 0.5625)
        let pose = retargeter.update(short, swingAngle: 0, at: 0)
        XCTAssertLessThanOrEqual(pose[.neck].x - pose[.root].x, AvatarSize.torso * 0.25 + 0.001)
        XCTAssertLessThan(pose.handCenter.x, 2.1)
        XCTAssertEqual(length(pose, .leftShoulder, .leftElbow), AvatarSize.upperArm, accuracy: 0.001)
        XCTAssertEqual(length(pose, .rightElbow, .rightWrist), AvatarSize.forearm, accuracy: 0.001)
        XCTAssertFalse(BodyPose3D.cameraWaiting.clubVisible)
        XCTAssertEqual(BodyPose3D.cameraWaiting.handCenter, AvatarAnimations.address.handCenter)
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

    func testLiveCameraCopiesMeasuredElbowsWristsAndFeetInClubSpace() {
        let space = SwingSpace(shoulders: CGPoint(x: 0.5, y: 0.72), width: 0.15, aspect: 0.75)
        let address = VirtualClubAddress(grip: space.local(CGPoint(x: 0.5, y: 0.4)),
                                         ball: space.local(CGPoint(x: 0.5, y: 0.08)))
        var retargeter = PoseRetargeter(calibration: calibration, handedness: .right, frameAspect: 0.75)
        for (time, hands) in [(0.0, CGPoint(x: 0.5, y: 0.4)), (0.4, CGPoint(x: 0.78, y: 0.9)),
                              (0.7, CGPoint(x: 0.25, y: 0.88))] {
            let measured = frame(time: time, hands: hands)
            let club = VirtualClubState(time: time, space: space, address: address,
                                       grip: space.local(hands), angle: 100, confidence: 0.9)
            let projected = CameraPoseProjection(club: club, handedness: .right)
            let pose = retargeter.updateCamera(measured, club: club, positionLocked: true, swingAngle: 100, at: time)
            for joint in BodyJoint.allCases {
                XCTAssertLessThan(simd_distance(pose[joint], projected.bodyJoint(joint, image: measured.point(joint)!)), 0.0001,
                                  "\(joint) must come from the observed pose, not a canned golf stance")
            }
            XCTAssertLessThan(simd_distance(pose.handCenter, projected.project(local: club.grip)), 0.0001)
            XCTAssertEqual(pose.clubHead, projected.project(local: club.head))
            XCTAssertEqual(projected.project(local: address.ball), AvatarSize.ball)
            XCTAssertEqual(pose[.root].x, AvatarAnimations.address[.root].x)
            XCTAssertEqual(pose[.leftAnkle].x, AvatarAnimations.address[.leftAnkle].x,
                           "the body must not lean wholesale along the shaft")
        }
    }

    func testLiveCameraGapKeepsPoseAndBodyTranslationDoesNotMoveBall() {
        let measured = frame(time: 0, hands: CGPoint(x: 0.78, y: 0.9))
        let space = SwingSpace(shoulders: CGPoint(x: 0.5, y: 0.72), width: 0.15, aspect: 0.75)
        let address = VirtualClubAddress(grip: space.local(CGPoint(x: 0.5, y: 0.4)), ball: space.local(CGPoint(x: 0.5, y: 0.08)))
        let club = VirtualClubState(time: 0, space: space, address: address, grip: space.local(CGPoint(x: 0.78, y: 0.9)), angle: 100, confidence: 0.9)
        var retargeter = PoseRetargeter(calibration: calibration, handedness: .right, frameAspect: 0.75)
        let pose = retargeter.updateCamera(measured, club: club, positionLocked: true, swingAngle: 100, at: 0)
        let gap = retargeter.updateCamera(nil, club: nil, positionLocked: true, swingAngle: 0, at: 0.1)
        XCTAssertEqual(gap.joints, pose.joints)
        XCTAssertTrue(gap.clubVisible)
        let shifted = PoseFrame(timestamp: 0.2, points: measured.points.mapValues {
            PosePoint(location: CGPoint(x: $0.location.x + 0.04, y: $0.location.y), confidence: $0.confidence)
        })
        let moved = retargeter.updateCamera(shifted, club: nil, positionLocked: true, swingAngle: 0, at: 0.2)
        XCTAssertGreaterThan(moved[.root].z, pose[.root].z)
        let projection = CameraPoseProjection(club: club, handedness: .right)
        XCTAssertEqual(projection.project(local: address.ball), AvatarSize.ball)
        XCTAssertEqual(moved[.leftElbow], projection.bodyJoint(.leftElbow, image: shifted.point(.leftElbow)!))
    }

    func testExperimentalDepthDoesNotAlterGripBallOrImagePlane() {
        let time = 1.0
        var measured = frame(time: time, hands: CGPoint(x: 0.5, y: 0.4))
        measured.depth = BodyDepthEstimate(timestamp: time, normalizedDepth: [
            BodyJoint.leftElbow.rawValue: 0.1, BodyJoint.leftWrist.rawValue: 0.2])
        let space = SwingSpace(shoulders: CGPoint(x: 0.5, y: 0.72), width: 0.15, aspect: 0.75)
        let address = VirtualClubAddress(grip: space.local(CGPoint(x: 0.5, y: 0.4)), ball: space.local(CGPoint(x: 0.5, y: 0.08)))
        let club = VirtualClubState(time: time, space: space, address: address, grip: address.grip, angle: 0, confidence: 0.9)
        let projection = CameraPoseProjection(club: club, handedness: .right)
        var retargeter = PoseRetargeter(calibration: calibration, handedness: .right, frameAspect: 0.75)
        let result = retargeter.updateCamera(measured, club: club, positionLocked: true, swingAngle: 0, at: time)
        let elbow = projection.bodyJoint(.leftElbow, image: measured.point(.leftElbow)!)
        XCTAssertNotEqual(result[.leftElbow].x, elbow.x)
        XCTAssertEqual(result[.leftElbow].y, elbow.y)
        XCTAssertEqual(result[.leftElbow].z, elbow.z)
        XCTAssertEqual(result[.leftWrist], projection.bodyJoint(.leftWrist, image: measured.point(.leftWrist)!))
        XCTAssertEqual(result.clubHead, projection.project(local: club.head))
        XCTAssertEqual(projection.project(local: address.ball), AvatarSize.ball)
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

    @MainActor
    func testCameraAvatarNeverFallsBackToAReactionOrDefaultStance() {
        let scene = CourseScene()
        scene.configurePlayer(calibration: calibration, handedness: .right, frameAspect: 0.75)
        scene.ingest(frame(time: 0, hands: CGPoint(x: 0.78, y: 0.9)), swingAngle: 100, frameAspect: 0.75)
        let before = scene.golferPose(shot: nil, elapsed: 0, isReplay: false, swingAngle: 0, now: 0)
        XCTAssertNotEqual(before.joints, BodyPose3D.cameraWaiting.joints, "unplayable contact must not suppress visible body motion")
        let shot = RangeShot(id: 1, club: .iron, power: 0.5, aim: 0, strike: .miss)
        for elapsed in [0.2, 0.7, 1.5, 3.0] {
            XCTAssertEqual(scene.golferPose(shot: shot, elapsed: elapsed, isReplay: false, swingAngle: 0, now: 10), before,
                           "follow-through stays player controlled even after impact or a stale delivery")
        }
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
        XCTAssertLessThan(address.lookAt.z, -3, "looking down the landing line in course yards")
        let forward = simd_normalize(address.lookAt - address.position)
        let right = simd_normalize(simd_cross(forward, simd_float3(0, 1, 0)))
        let up = simd_cross(right, forward)
        let toBall = simd_float3(0, Float(AvatarSize.courseBallRadius), 0) - address.position
        let verticalNDC = simd_dot(toBall, up) / (simd_dot(toBall, forward) * tan(address.fieldOfView * .pi / 360))
        XCTAssertGreaterThan(verticalNDC, -0.6, "the small ball must remain above the bottom controls")
        let green = ShotCameraDirector.shot(inputs(shot: nil, elapsed: 0, onGreen: true))
        XCTAssertLessThan(green.position.y, address.position.y, "lower on the green")
        XCTAssertLessThan(green.position.z, address.position.z, "and closer to the ball")
        let drive = RangeShot(id: 1, club: .driver, power: 0.9, aim: 0)
        let hero = ShotCameraDirector.shot(inputs(shot: drive, elapsed: 0.2))
        XCTAssertLessThan(hero.position.z, 0, "in front of the golfer on the target side")
        XCTAssertLessThan(simd_length(hero.lookAt - simd_float3(-2.6, 3.2, 0) * AvatarSize.courseScale), 0.2, "framing the yard-scale golfer")
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
