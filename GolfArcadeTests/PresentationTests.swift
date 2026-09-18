import CoreGraphics
import simd
import XCTest
@testable import GolfArcade

/// The things between the model and the player's senses: smooth motion and the sounds of a shot.
final class PresentationTests: XCTestCase {
    // MARK: - Sounds

    func testEveryStrikeRendersACleanShortBuffer() {
        for club in GolfClub.allCases {
            for strike in [StrikeQuality.center, .thin, .fat, .heel, .toe] {
                let sound = ImpactSound.strike(club: club, strike: strike)
                let data = sound.render()
                XCTAssertEqual(data.count, 44 + Int(sound.duration * ImpactSound.rate) * 2, "\(club) \(strike)")
                XCTAssertEqual(String(decoding: data.prefix(4), as: UTF8.self), "RIFF")
                let peak = samples(data).map { abs($0) }.max() ?? 0
                XCTAssertGreaterThan(peak, 0.3, "\(club) \(strike) must be audible")
                XCTAssertLessThanOrEqual(peak, 1, "\(club) \(strike) must not clip")
                XCTAssertLessThan(sound.duration, 0.5, "a strike is over quickly")
            }
        }
    }

    func testMishitsSoundDifferentFromACentredStrike() {
        let center = samples(ImpactSound.strike(club: .iron, strike: .center).render())
        let fat = samples(ImpactSound.strike(club: .iron, strike: .fat).render())
        let thin = samples(ImpactSound.strike(club: .iron, strike: .thin).render())
        // A fat shot is dull: its energy lingers in the turf thud; a thin one is bright and brief.
        XCTAssertGreaterThan(energy(fat, from: 0.05, to: 0.15), energy(center, from: 0.05, to: 0.15))
        XCTAssertGreaterThan(zeroCrossings(thin, upTo: 0.02), zeroCrossings(center, upTo: 0.02))
        XCTAssertNotEqual(center, fat)
    }

    func testDriverRingsHigherAndLongerThanTheWedge() {
        let driver = samples(ImpactSound.strike(club: .driver, strike: .center).render())
        let wedge = samples(ImpactSound.strike(club: .wedge, strike: .center).render())
        XCTAssertGreaterThan(zeroCrossings(driver, upTo: 0.04), zeroCrossings(wedge, upTo: 0.04))
        XCTAssertGreaterThan(energy(driver, from: 0.03, to: 0.08), energy(wedge, from: 0.03, to: 0.08))
    }

    func testWhooshGrowsWithPowerAndLandingsAreDistinct() {
        let soft = samples(ImpactSound.whoosh(power: 0.3).render()), hard = samples(ImpactSound.whoosh(power: 1).render())
        XCTAssertGreaterThan(energy(hard, from: 0, to: 0.3), energy(soft, from: 0, to: 0.3))
        for sound in [ImpactSound.turfLanding, .greenLanding, .sandLanding, .splash, .cupDrop] {
            let peak = samples(sound.render()).map { abs($0) }.max() ?? 0
            XCTAssertGreaterThan(peak, 0.2)
            XCTAssertLessThanOrEqual(peak, 1)
        }
        XCTAssertGreaterThan(samples(ImpactSound.splash.render()).count, samples(ImpactSound.greenLanding.render()).count)
    }

    // MARK: - Landing announcements

    @MainActor
    func testSceneAnnouncesTheLandingOnceAndTheCupWhenHoled() throws {
        let scene = CourseScene()
        let hole = Course.easy.holes[0]
        var heard: [RangeAudio.Landing] = []
        scene.onLanding = { heard.append($0) }
        let shot = RangeShot(id: 1, club: .iron, power: 0.8, aim: 0, origin: hole.tee, heading: 0, hole: hole)
        let landing = try XCTUnwrap(ShotCameraDirector.landingTime(of: shot))
        var inputs = SceneInputs(hole: hole, ball: hole.tee, heading: 0, distanceToPin: hole.length, lie: .tee, club: .iron,
                                 aim: 0, handedness: .right, shot: shot, isReplay: false, flightStart: Date(timeIntervalSinceNow: -landing + 0.5),
                                 pausedAt: nil, swingAngle: 0, bystanders: [])
        scene.inputs = inputs
        scene.stepForTesting()
        XCTAssertTrue(heard.isEmpty, "nothing lands before the ball comes down")
        inputs.flightStart = Date(timeIntervalSinceNow: -landing - 0.05)
        scene.inputs = inputs
        scene.stepForTesting()
        scene.stepForTesting()
        XCTAssertEqual(heard.count, 1)
        if case .turf(let lie, let speed)? = heard.first {
            XCTAssertNotEqual(lie, .water)
            XCTAssertGreaterThan(speed, 5)
        } else {
            XCTFail("an iron onto grass lands on turf: \(heard)")
        }
        // A holed putt: silent until the cup.
        heard.removeAll()
        let cupSide = CoursePoint(x: hole.pin.x, d: hole.pin.d - 2)
        let power = try XCTUnwrap(RangeShot.power(toReach: 2.6, with: .putter))
        let putt = RangeShot(id: 2, club: .putter, power: power, aim: 0, origin: cupSide, heading: 0, hole: hole)
        XCTAssertTrue(putt.isHoled, "a two-yard putt straight at the cup drops")
        inputs.shot = putt
        inputs.flightStart = Date(timeIntervalSinceNow: -putt.duration - 0.1)
        scene.inputs = inputs
        scene.stepForTesting()
        scene.stepForTesting()
        XCTAssertEqual(heard, [.cup])
    }

    // MARK: - Smoothness

    @MainActor
    func testLivePoseIsDrawnBetweenCameraDeliveries() {
        let scene = CourseScene()
        scene.configurePlayer(calibration: .uiTestingFixture, handedness: .right, frameAspect: 0.75)
        let start = CACurrentMediaTime()
        scene.ingest(frame(hands: CGPoint(x: 0.5, y: 0.4)), swingAngle: 0, frameAspect: 0.75)
        let first = scene.golferPose(shot: nil, elapsed: 0, isReplay: false, swingAngle: 0, now: start + 10)
        // A second delivery a camera frame later: frames drawn in between glide toward it.
        Thread.sleep(forTimeInterval: 1.0 / 30)
        scene.ingest(frame(hands: CGPoint(x: 0.7, y: 0.8)), swingAngle: 60, frameAspect: 0.75)
        let second = CACurrentMediaTime()
        let early = scene.golferPose(shot: nil, elapsed: 0, isReplay: false, swingAngle: 0, now: second)
        let late = scene.golferPose(shot: nil, elapsed: 0, isReplay: false, swingAngle: 0, now: second + 0.5)
        XCTAssertNotEqual(late.handCenter, first.handCenter, "the avatar reaches the new pose")
        let travelled = simd_distance(early.handCenter, first.handCenter)
        let total = simd_distance(late.handCenter, first.handCenter)
        XCTAssertGreaterThan(total, 0.1)
        XCTAssertLessThan(travelled, total * 0.6, "right at delivery the avatar is still mostly on the previous pose")
    }

    @MainActor
    func testPoseSourceSwitchesCrossfadeInsteadOfSnapping() {
        let scene = CourseScene()
        let hole = Course.easy.holes[0]
        var inputs = SceneInputs(hole: hole, ball: hole.tee, heading: 0, distanceToPin: hole.length, lie: .tee, club: .driver,
                                 aim: 0, handedness: .right, shot: nil, isReplay: false, flightStart: nil, pausedAt: nil,
                                 swingAngle: 0, bystanders: [])
        // The touch-pad golfer is wound up into a backswing.
        inputs.swingAngle = 120
        scene.inputs = inputs
        let start = CACurrentMediaTime()
        for i in 0..<12 { scene.stepForTesting(now: start + Double(i) * 0.1) }
        let canned = scene.presentedPoseForTesting
        XCTAssertEqual(scene.poseSource, .canned)
        XCTAssertGreaterThan(simd_distance(canned.handCenter, BodyPose3D.cameraWaiting.handCenter), 1)
        // A camera player appears: the avatar eases from that stance to the waiting one.
        scene.configurePlayer(calibration: .uiTestingFixture, handedness: .right, frameAspect: 0.75)
        scene.stepForTesting(now: start + 1.21)
        let blended = scene.presentedPoseForTesting
        XCTAssertEqual(scene.poseSource, .waiting)
        let toTarget = simd_distance(blended.handCenter, BodyPose3D.cameraWaiting.handCenter)
        let fromStart = simd_distance(blended.handCenter, canned.handCenter)
        XCTAssertGreaterThan(toTarget, 0.5, "the first frame after a switch is still near the old body")
        XCTAssertLessThan(fromStart, 0.2)
        scene.stepForTesting(now: start + 1.35)
        let midway = scene.presentedPoseForTesting
        XCTAssertLessThan(simd_distance(midway.handCenter, BodyPose3D.cameraWaiting.handCenter), toTarget, "and keeps easing toward the new one")
        scene.stepForTesting(now: start + 1.6)
        XCTAssertLessThan(simd_distance(scene.presentedPoseForTesting.handCenter, BodyPose3D.cameraWaiting.handCenter), 0.001, "then arrives")
    }

    // MARK: - Helpers

    private func samples(_ data: Data) -> [Double] {
        stride(from: 44, to: data.count - 1, by: 2).map { index in
            Double(Int16(bitPattern: UInt16(data[index]) | (UInt16(data[index + 1]) << 8))) / 32767
        }
    }

    private func energy(_ samples: [Double], from start: Double, to end: Double) -> Double {
        let range = Int(start * ImpactSound.rate)..<min(samples.count, Int(end * ImpactSound.rate))
        return samples[range].reduce(0) { $0 + $1 * $1 }
    }

    private func zeroCrossings(_ samples: [Double], upTo seconds: Double) -> Int {
        let slice = samples.prefix(Int(seconds * ImpactSound.rate))
        return zip(slice, slice.dropFirst()).filter { ($0 >= 0) != ($1 >= 0) }.count
    }

    private func frame(hands: CGPoint) -> PoseFrame {
        var points: [BodyJoint: CGPoint] = [
            .nose: CGPoint(x: 0.5, y: 0.86), .neck: CGPoint(x: 0.5, y: 0.75),
            .leftShoulder: CGPoint(x: 0.4, y: 0.72), .rightShoulder: CGPoint(x: 0.6, y: 0.72),
            .leftHip: CGPoint(x: 0.44, y: 0.45), .rightHip: CGPoint(x: 0.56, y: 0.45), .root: CGPoint(x: 0.5, y: 0.45),
            .leftKnee: CGPoint(x: 0.43, y: 0.27), .rightKnee: CGPoint(x: 0.57, y: 0.27),
            .leftAnkle: CGPoint(x: 0.42, y: 0.1), .rightAnkle: CGPoint(x: 0.58, y: 0.1)
        ]
        points[.leftWrist] = CGPoint(x: hands.x - 0.015, y: hands.y)
        points[.rightWrist] = CGPoint(x: hands.x + 0.015, y: hands.y)
        points[.leftElbow] = CGPoint(x: (0.4 + hands.x) / 2, y: (0.72 + hands.y) / 2)
        points[.rightElbow] = CGPoint(x: (0.6 + hands.x) / 2, y: (0.72 + hands.y) / 2)
        return PoseFrame(timestamp: CACurrentMediaTime(), points: points.mapValues { PosePoint(location: $0, confidence: 0.9) })
    }
}
