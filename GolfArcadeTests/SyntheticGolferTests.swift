import XCTest
import simd
@testable import GolfArcade

/// The mocked human is the stand-in for real players: these tests hold it to human motion and
/// confirm the swing detector and stance aiming read it the way they must read a person.
final class SyntheticGolferTests: XCTestCase {
    func testBodyKeepsHumanProportionsThroughTheSwing() {
        let golfer = SyntheticGolfer()
        for time in stride(from: 0.0, through: golfer.duration, by: 0.05) {
            let joints = golfer.body(at: time)
            XCTAssertEqual(simd_distance(joints[.leftShoulder]!, joints[.leftElbow]!), 0.32, accuracy: 0.001, "\(time)")
            XCTAssertEqual(simd_distance(joints[.leftElbow]!, joints[.leftWrist]!), 0.30, accuracy: 0.001, "\(time)")
            XCTAssertEqual(simd_distance(joints[.rightHip]!, joints[.rightKnee]!), 0.45, accuracy: 0.001, "\(time)")
            XCTAssertEqual(simd_distance(joints[.leftShoulder]!, joints[.rightShoulder]!), 0.40, accuracy: 0.001, "\(time)")
            XCTAssertLessThan(simd_distance(joints[.leftWrist]!, joints[.rightWrist]!), 0.12, "hands stay together at \(time)")
            XCTAssertGreaterThan(joints[.nose]!.y, joints[.neck]!.y, "head above the neck at \(time)")
            for (_, point) in joints { XCTAssertGreaterThanOrEqual(point.y, 0, "nothing below the ground at \(time)") }
        }
    }

    func testFullSwingTurnsBackThenThroughLikeAGolfer() {
        let golfer = SyntheticGolfer()
        let address = golfer.body(at: 0)
        let top = golfer.body(at: golfer.addressHold + 0.9)
        let finish = golfer.body(at: golfer.duration)
        // Right-hander: at the top the hands are high over the trail (right, +z) shoulder.
        XCTAssertGreaterThan(top[.leftWrist]!.y, address[.leftWrist]!.y + 0.7)
        XCTAssertGreaterThan(top[.leftWrist]!.z, 0.2)
        XCTAssertGreaterThan(top[.rightShoulder]!.z, top[.leftShoulder]!.z, "shoulders never fold over")
        // The backswing brings the lead shoulder toward the phone; the finish faces the target.
        XCTAssertGreaterThan(top[.leftShoulder]!.x, top[.rightShoulder]!.x + 0.2)
        XCTAssertGreaterThan(finish[.rightShoulder]!.x, finish[.leftShoulder]!.x + 0.2)
        XCTAssertLessThan(finish[.leftWrist]!.z, -0.1, "hands finish over the lead shoulder")
        XCTAssertLessThan(finish[.root]!.z, address[.root]!.z - 0.05, "weight ends on the lead side")
        // Every joint is on screen the whole time.
        for pose in golfer.poses() {
            for (_, point) in pose.frame!.points {
                XCTAssert((0...1).contains(point.location.x) && (0...1).contains(point.location.y), "\(pose.time): \(point.location)")
            }
        }
    }

    func testLeftHanderIsTheMirrorImage() {
        let right = SyntheticGolfer(handedness: .right), left = SyntheticGolfer(handedness: .left)
        for time in [0.0, 1.5, 2.0, 2.4] {
            let a = right.body(at: time), b = left.body(at: time)
            XCTAssertEqual(a[.leftWrist]!.z, -b[.rightWrist]!.z, accuracy: 0.001)
            XCTAssertEqual(a[.leftWrist]!.x, b[.rightWrist]!.x, accuracy: 0.001)
            XCTAssertEqual(a[.leftShoulder]!.z, -b[.rightShoulder]!.z, accuracy: 0.001)
        }
    }

    func testDetectorReadsTheHumanFullSwingAsOneStroke() throws {
        let impacts = try drive(SyntheticGolfer())
        XCTAssertEqual(impacts.count, 1, "one full swing is one contact")
        let impact = try XCTUnwrap(impacts.first)
        XCTAssertNotEqual(impact.strike, .miss)
        XCTAssertGreaterThan(impact.power, 0.5, "a full driver swing is not a chip")
        XCTAssertLessThan(abs(impact.startLineDegrees), 12, "a square human swing starts near the line")
    }

    func testDetectorReadsTheHumanPuttAsAGentleStroke() throws {
        var golfer = SyntheticGolfer(stroke: .putt)
        golfer.addressHold = 1.0
        let impacts = try drive(golfer, club: .putter)
        XCTAssertEqual(impacts.count, 1)
        let putt = try XCTUnwrap(impacts.first)
        let full = try XCTUnwrap(drive(SyntheticGolfer()).first)
        XCTAssertNotEqual(putt.strike, .miss)
        XCTAssertLessThan(putt.power, 0.8, "a shoulder-rocked putt is not a full stroke")
        XCTAssertLessThan(putt.power, full.power - 0.15)
    }

    func testLeftHandedHumanSwingIsReadTheSameWay() throws {
        let impacts = try drive(SyntheticGolfer(handedness: .left), handedness: .left)
        XCTAssertEqual(impacts.count, 1)
        XCTAssertNotEqual(try XCTUnwrap(impacts.first).strike, .miss)
    }

    func testStanceTurnAimsTheShotAndLocksBeforeTheSwing() throws {
        // Chest turned toward the target (right side back from the phone) aims left for a
        // right-hander; the mirror for a left-hander. Square stances stay at zero.
        for (yaw, handedness, expected) in [(-15.0, Handedness.right, 15.0), (15, .right, -15), (-15, .left, -15), (1.5, .right, 0)] {
            var golfer = SyntheticGolfer(handedness: handedness, stanceYaw: yaw)
            golfer.addressHold = 1.2
            var detector = ArmSwingDetector()
            detector.handedness = handedness
            var aims: [(time: Double, aim: Double)] = []
            var takeaway: Double?
            var impact: Double?
            for pose in golfer.poses() {
                let sample = ArmSwingDetector.Sample(frame: pose.frame!, frameAspect: pose.aspect, certifiedSpace: detector.certifiedSpace)
                let event = detector.ingest(sample, at: pose.time)
                aims.append((pose.time, detector.addressAimDegrees))
                if detector.phase == .backswing, takeaway == nil { takeaway = pose.time }
                if case .impact? = event, impact == nil { impact = pose.time }
            }
            let takeawayTime = try XCTUnwrap(takeaway), impactTime = try XCTUnwrap(impact)
            let lockedAim = try XCTUnwrap(aims.first { $0.time >= takeawayTime }).aim
            XCTAssertEqual(lockedAim, expected, accuracy: 2.5, "yaw \(yaw) \(handedness)")
            if expected != 0 {
                let lockedAt = try XCTUnwrap(aims.last { $0.time < takeawayTime && $0.aim != lockedAim }).time
                XCTAssertLessThan(lockedAt, golfer.addressHold, "the line locks while standing at address")
            }
            for sample in aims where sample.time >= takeawayTime && sample.time <= impactTime {
                XCTAssertEqual(sample.aim, lockedAim, "the line holds through the swing at \(sample.time)")
            }
        }
    }

    func testAimMappingIsBoundedAndDeadZoned() throws {
        XCTAssertEqual(StanceAimSettler.aimDegrees(bodyYaw: 2, handedness: .right), 0)
        XCTAssertEqual(StanceAimSettler.aimDegrees(bodyYaw: 10, handedness: .right), -10)
        XCTAssertEqual(StanceAimSettler.aimDegrees(bodyYaw: 10, handedness: .left), 10)
        XCTAssertEqual(StanceAimSettler.aimDegrees(bodyYaw: -70, handedness: .right), 30)
        XCTAssertEqual(StanceAimSettler.aimDegrees(bodyYaw: .nan, handedness: .right), 0)
        var settler = StanceAimSettler()
        settler.ingest(10, at: 0)
        settler.ingest(30, at: 0.2)
        settler.ingest(12, at: 0.45)
        XCTAssertNil(settler.settled, "a turn still moving must not lock")
        settler.ingest(11, at: 0.7)
        settler.ingest(12, at: 0.9)
        XCTAssertEqual(try XCTUnwrap(settler.settled), 11.67, accuracy: 0.1)
    }

    func testAvatarTurnsItsShouldersAndHipsWithTheMockedHuman() throws {
        for handedness in [Handedness.right, .left] {
            let golfer = SyntheticGolfer(handedness: handedness)
            var detector = ArmSwingDetector()
            detector.handedness = handedness
            detector.configure(for: .driver)
            var retargeter = PoseRetargeter(calibration: .uiTestingFixture, handedness: handedness, frameAspect: golfer.aspect)
            var filter = CameraAvatarPoseFilter()
            var shoulders: [(time: Double, truth: Double, avatar: Double)] = []
            var hips: [(time: Double, truth: Double, avatar: Double)] = []
            let side = handedness == .right ? 1.0 : -1.0
            for pose in golfer.poses() {
                let frame = pose.frame!
                let sample = ArmSwingDetector.Sample(frame: frame, frameAspect: golfer.aspect, certifiedSpace: detector.certifiedSpace)
                _ = detector.ingest(sample, at: pose.time)
                let club = detector.virtualClub ?? detector.setupClub
                let measured = retargeter.updateCamera(frame, club: club, positionLocked: detector.isPositionLocked,
                                                       swingAngle: detector.swingAngle, at: pose.time)
                let avatar = filter.update(measured, frame: frame, at: pose.time)
                let truth = golfer.world(at: pose.time)
                func yaw(_ left: simd_float3, _ right: simd_float3) -> Double {
                    Double(atan2(right.x - left.x, right.z - left.z)) * 180 / .pi
                }
                // The avatar's frame is mirrored for a left-hander: the golfer's right sits on -z,
                // so the turn is the angle of the line from the -z end to the +z end.
                func avatarYaw(_ left: BodyJoint, _ right: BodyJoint) -> Double {
                    handedness == .right ? yaw(avatar[left], avatar[right]) : yaw(avatar[right], avatar[left])
                }
                shoulders.append((pose.time, yaw(truth[.leftShoulder]!, truth[.rightShoulder]!) * side,
                                  avatarYaw(.leftShoulder, .rightShoulder)))
                hips.append((pose.time, yaw(truth[.leftHip]!, truth[.rightHip]!) * side, avatarYaw(.leftHip, .rightHip)))
            }
            func at(_ samples: [(time: Double, truth: Double, avatar: Double)], _ time: Double) -> (truth: Double, avatar: Double) {
                let sample = samples.min { abs($0.time - time) < abs($1.time - time) }!
                return (sample.truth, sample.avatar)
            }
            let hold = golfer.addressHold
            let address = at(shoulders, hold - 0.1), top = at(shoulders, hold + 0.9), finish = at(shoulders, hold + 2.2)
            XCTAssertEqual(address.avatar, address.truth, accuracy: 6, "\(handedness) address")
            XCTAssertGreaterThan(abs(top.truth), 70, "the mock turns fully at the top")
            XCTAssertEqual(top.avatar, top.truth, accuracy: 18, "\(handedness) top of the backswing")
            XCTAssertEqual(finish.avatar, finish.truth, accuracy: 22, "\(handedness) finish")
            XCTAssertNotEqual((top.avatar > 0), (finish.avatar > 0), "back and through turn opposite ways")
            let hipTop = at(hips, hold + 0.9), hipFinish = at(hips, hold + 2.2)
            XCTAssertEqual(hipTop.avatar, hipTop.truth, accuracy: 18, "\(handedness) hips at the top")
            XCTAssertEqual(hipFinish.avatar, hipFinish.truth, accuracy: 25, "\(handedness) hips at the finish")
        }
    }

    /// The meter follows the backswing: a tour-length swing fills it, the shoulder-high swing
    /// most players make at an easier tempo still reads ~90%+ (a 230-yard drive), and easing
    /// off costs distance in proportion, not by the square.
    func testFullSwingsFillTheMeterAndSofterSwingsLoseDistanceInProportion() throws {
        func power(backswing: Double, tempo: Double, fps: Double = 30) throws -> Double {
            var golfer = SyntheticGolfer()
            golfer.backswing = backswing
            golfer.tempo = tempo
            golfer.framesPerSecond = fps
            return try XCTUnwrap(drive(golfer).first).power
        }
        let tour = try power(backswing: 1, tempo: 1)
        let amateur = try power(backswing: 0.8, tempo: 0.8)
        let amateurOnDevice = try power(backswing: 0.8, tempo: 0.8, fps: 60)
        let easy = try power(backswing: 0.65, tempo: 0.7)
        let half = try power(backswing: 0.5, tempo: 0.6)
        XCTAssertGreaterThanOrEqual(tour, 0.98, "a full swing at tour tempo is full power")
        XCTAssertGreaterThanOrEqual(amateur, 0.88, "an ordinary full swing drives it 230+")
        XCTAssertGreaterThanOrEqual(amateurOnDevice, 0.88)
        XCTAssertLessThan(easy, amateur - 0.12, "a three-quarter swing gives up distance")
        XCTAssertGreaterThan(easy, 0.6)
        XCTAssertLessThan(half, easy - 0.15)
        XCTAssertGreaterThan(half, 0.3, "a half swing is still half a shot, not a chip")
        let shot = RangeShot(id: 1, club: .driver, power: amateur, aim: 0)
        XCTAssertGreaterThanOrEqual(shot.total, 230)
    }

    func testTempoOnlyAdjustsAroundTheBackswing() throws {
        let detector = ArmSwingDetector()
        let full = detector.power(arc: detector.fullBackswing, downswingSpeed: detector.fullDownswingSpeed)
        XCTAssertEqual(full, 1, accuracy: 1e-9, "an ordinary tempo delivers what the backswing loaded")
        XCTAssertEqual(detector.power(arc: detector.fullBackswing * 0.5, downswingSpeed: detector.fullDownswingSpeed), 0.5, accuracy: 1e-9)
        XCTAssertEqual(detector.power(arc: detector.fullBackswing * 2, downswingSpeed: detector.fullDownswingSpeed * 2), 1)
        let lazy = detector.power(arc: detector.fullBackswing, downswingSpeed: 0)
        XCTAssertEqual(lazy, 1 - detector.speedWeight, accuracy: 1e-9, "a dead-slow downswing still keeps most of it")
        XCTAssertGreaterThan(detector.power(arc: detector.fullBackswing * 0.9, downswingSpeed: detector.fullDownswingSpeed * 1.25), 0.9)
    }

    /// Twelve takes of the same swing through a jittery tracker start within a degree of each
    /// other: the path is read from long chords either side of the ball, not one noisy frame.
    func testStartLineIsConsistentAcrossTakes() throws {
        var lines: [Double] = []
        for seed in 0..<12 {
            var golfer = SyntheticGolfer()
            golfer.backswing = 0.8
            golfer.tempo = 0.8
            golfer.jitter = 0.004
            golfer.jitterSeed = UInt64(seed * 7919 + 1)
            golfer.addressHold = 2
            lines.append(try XCTUnwrap(drive(golfer).first).startLineDegrees)
        }
        let mean = lines.reduce(0, +) / Double(lines.count)
        let spread = (lines.map { ($0 - mean) * ($0 - mean) }.reduce(0, +) / Double(lines.count - 1)).squareRoot()
        XCTAssertLessThan(spread, 1.0, "takes: \(lines)")
        XCTAssertLessThan(abs(mean), 5, "a square swing starts near the line even before the player's neutral settles")
    }

    /// The player's usual swing becomes the zero line after a few swings; only a change of
    /// shape turns the ball — over the top pulls, and a left-hander's pull goes the other way.
    func testUsualSwingFliesStraightAndOverTheTopPulls() throws {
        func golfer(overTheTop: Double = 0, handedness: Handedness = .right, seed: UInt64) -> SyntheticGolfer {
            var golfer = SyntheticGolfer(handedness: handedness)
            golfer.backswing = 0.8
            golfer.tempo = 0.8
            golfer.overTheTop = overTheTop
            golfer.jitterSeed = seed
            return golfer
        }
        for handedness in [Handedness.right, .left] {
            let usual = (1...4).map { golfer(handedness: handedness, seed: UInt64($0)) }
            let impacts = try drive(usual + [golfer(overTheTop: 0.15, handedness: handedness, seed: 9), golfer(overTheTop: -0.15, handedness: handedness, seed: 10)], handedness: handedness)
            XCTAssertEqual(impacts.count, 6, "\(handedness)")
            let settled = impacts[3].startLineDegrees
            XCTAssertLessThan(abs(settled), 1, "\(handedness): the usual swing is square by the fourth go")
            let pull = impacts[4].startLineDegrees - settled
            let push = impacts[5].startLineDegrees - settled
            let sign: Double = handedness == .right ? -1 : 1
            XCTAssertGreaterThan(pull * sign, 1, "\(handedness): over the top pulls, \(pull)°")
            XCTAssertLessThan(push * sign, -0.5, "\(handedness): dropping inside pushes, \(push)°")
        }
    }

    func testPathNeutralIsTheMedianAndRampsIn() {
        var neutral = PathNeutral()
        XCTAssertEqual(neutral.value, 0)
        neutral.record(-6)
        XCTAssertEqual(neutral.value, -2, accuracy: 1e-9, "one swing counts a third")
        neutral.record(-6)
        neutral.record(30) // one wild swing does not move a median much
        XCTAssertEqual(neutral.value, -6, accuracy: 1e-9)
        for _ in 0..<20 { neutral.record(4) }
        XCTAssertEqual(neutral.value, 4, accuracy: 1e-9, "old swings age out of the window")
        XCTAssertEqual(neutral.count, PathNeutral.window)
        XCTAssertEqual(VirtualClubState.startLine(deviation: 4, neutral: 4), 0)
        XCTAssertEqual(VirtualClubState.startLine(deviation: -40, neutral: 0), -VirtualClubState.startLineLimit)
    }

    // MARK: - Helpers

    private func drive(_ golfer: SyntheticGolfer, club: GolfClub = .driver, handedness: Handedness = .right) throws -> [SwingImpact] {
        try drive([golfer], club: club, handedness: handedness)
    }

    /// Several swings in a row through one detector, as a player would make them.
    private func drive(_ golfers: [SyntheticGolfer], club: GolfClub = .driver, handedness: Handedness = .right) throws -> [SwingImpact] {
        var detector = ArmSwingDetector()
        detector.handedness = handedness
        detector.configure(for: club)
        var impacts: [SwingImpact] = []
        var offset = 0.0
        for golfer in golfers {
            for pose in golfer.poses() {
                let sample = ArmSwingDetector.Sample(frame: pose.frame!, frameAspect: pose.aspect, certifiedSpace: detector.certifiedSpace)
                if case .impact(let impact)? = detector.ingest(sample, at: pose.time + offset) { impacts.append(impact) }
            }
            offset += golfer.duration + 0.5
        }
        return impacts
    }
}
