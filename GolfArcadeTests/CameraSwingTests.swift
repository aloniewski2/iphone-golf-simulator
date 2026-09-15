import SceneKit
import XCTest
@testable import GolfArcade

final class ArmSwingDetectorTests: XCTestCase {
    /// Rotates the hands around the shoulders through (seconds, arc degrees) legs at 30 fps.
    /// 0° is hanging straight down; positive swings toward the golfer's trail side.
    private func drive(_ detector: inout ArmSwingDetector, legs: [(seconds: Double, arc: Double)], shoulderWidth: CGFloat = 0.2, dropAt: Set<Int> = []) -> [SwingInputEvent] {
        var events: [SwingInputEvent] = []
        var time = 0.0
        var arc = 0.0
        var index = 0
        for leg in legs {
            let steps = max(1, Int(leg.seconds * 30))
            let delta = (leg.arc - arc) / Double(steps)
            for _ in 0..<steps {
                time += 1.0 / 30
                arc += delta
                index += 1
                let radians = arc * .pi / 180
                let reach = shoulderWidth * 1.6
                let hands = CGPoint(x: 0.5 + sin(radians) * reach, y: 0.7 - cos(radians) * reach)
                let sample = dropAt.contains(index) ? nil : ArmSwingDetector.Sample(time: time, shoulderCenter: CGPoint(x: 0.5, y: 0.7), shoulderWidth: shoulderWidth, hands: hands)
                if let event = detector.ingest(sample, at: time) { events.append(event) }
            }
        }
        return events
    }

    private func impacts(_ events: [SwingInputEvent]) -> [(power: Double, aim: Double)] {
        events.compactMap { if case .impact(let power, let aim) = $0 { (power, aim) } else { nil } }
    }

    private let fullSwing: [(seconds: Double, arc: Double)] = [(0.5, 0), (0.8, 140), (0.15, 140), (0.2, -30), (0.3, 120), (0.5, 120), (0.4, 0), (0.5, 0)]

    func testFullBackswingIsFullPowerAndLoadsAsTheHandsGoBack() {
        var detector = ArmSwingDetector()
        let events = drive(&detector, legs: fullSwing)
        let loads = events.compactMap { if case .load(let value) = $0 { value } else { nil } }
        XCTAssertGreaterThan(loads.count, 10)
        XCTAssertTrue(zip(loads, loads.dropFirst()).allSatisfy { $1 >= $0 - 1e-9 }, "the meter only fills")
        XCTAssertEqual(loads.max() ?? 0, 1, accuracy: 0.02)
        XCTAssertEqual(impacts(events).count, 1)
        XCTAssertEqual(impacts(events).first?.power ?? 0, 1, accuracy: 0.02)
        XCTAssertEqual(impacts(events).first?.aim ?? 9, 0, accuracy: 0.5, "a swing within the meter flies straight")
        XCTAssertEqual(detector.phase, .address, "re-armed once the hands settle back down")
    }

    func testHalfSwingIsHalfPowerAndSizeDoesNotMatter() {
        var near = ArmSwingDetector()
        var far = ArmSwingDetector()
        let half: [(seconds: Double, arc: Double)] = [(0.5, 0), (0.6, 70), (0.15, 70), (0.225, -20), (0.3, 60), (0.5, 60), (0.4, 0), (0.5, 0)]
        let nearPower = impacts(drive(&near, legs: half, shoulderWidth: 0.3)).first?.power ?? 0
        let farPower = impacts(drive(&far, legs: half, shoulderWidth: 0.08)).first?.power ?? 0
        XCTAssertEqual(nearPower, 0.5, accuracy: 0.03, "half the arc at half speed is half power")
        XCTAssertEqual(farPower, nearPower, accuracy: 1e-6, "arc angles are scale-free")
    }

    func testOverswingHooksForARightHander() {
        var detector = ArmSwingDetector()
        let events = drive(&detector, legs: [(0.5, 0), (0.9, 178), (0.15, 178), (0.3, -30), (0.3, 120), (0.5, 120), (0.4, 0), (0.5, 0)])
        let shot = impacts(events).first
        XCTAssertGreaterThan(shot?.power ?? 0, 0.9)
        XCTAssertLessThan(shot?.aim ?? 0, -3, "past the meter the ball hooks left")
    }

    func testFasterDownswingHitsHarderFromTheSameBackswing() {
        var lazy = ArmSwingDetector()
        var quick = ArmSwingDetector()
        let lazyPower = impacts(drive(&lazy, legs: [(0.5, 0), (0.8, 110), (0.15, 110), (0.6, -20), (0.3, 60), (0.5, 60), (0.4, 0), (0.5, 0)])).first?.power ?? 0
        let quickPower = impacts(drive(&quick, legs: [(0.5, 0), (0.8, 110), (0.15, 110), (0.15, -20), (0.3, 60), (0.5, 60), (0.4, 0), (0.5, 0)])).first?.power ?? 0
        XCTAssertGreaterThan(quickPower, lazyPower + 0.15)
    }

    func testSlowPracticeSwingDoesNotSpendAShot() {
        var detector = ArmSwingDetector()
        let events = drive(&detector, legs: [(0.5, 0), (0.8, 120), (0.2, 120), (2.0, 0), (0.5, 0)])
        XCTAssertTrue(impacts(events).isEmpty)
        XCTAssertTrue(events.contains(.cancel))
    }

    func testBriefTrackingLossAtTheTopIsForgiven() {
        var detector = ArmSwingDetector()
        let events = drive(&detector, legs: fullSwing, dropAt: Set(36...42)) // 7 frames (~0.23 s) near the top
        XCTAssertEqual(impacts(events).count, 1)
        XCTAssertFalse(events.contains(.cancel))
    }

    func testLosingThePlayerForLongerCancels() {
        var detector = ArmSwingDetector()
        let events = drive(&detector, legs: [(0.5, 0), (0.8, 120), (1.0, 120)], dropAt: Set(40...80))
        XCTAssertEqual(events.last, .cancel)
        XCTAssertEqual(detector.phase, .findingPlayer)
    }
}

final class MeadowAvatarTests: XCTestCase {
    @MainActor
    func testArmsFollowThePoseAndSwingWithTheLoadOtherwise() {
        let meadow = MeadowScene()
        let high: Float = 0.9
        func p(_ x: CGFloat, _ y: CGFloat) -> PosePoint { PosePoint(location: CGPoint(x: x, y: y), confidence: high) }
        let hangingHands = PoseFrame(timestamp: 1, points: [
            .leftShoulder: p(0.4, 0.78), .rightShoulder: p(0.6, 0.78),
            .leftElbow: p(0.42, 0.65), .rightElbow: p(0.58, 0.65), .leftWrist: p(0.48, 0.5), .rightWrist: p(0.52, 0.5)
        ])
        meadow.update(shot: nil, elapsed: 0, power: 0, aim: 0, pose: hangingHands)
        guard let avatar = meadow.scene.rootNode.childNodes.first(where: { node in node.childNodes.contains { $0.childNodes.count == 2 } }) else {
            return XCTFail("avatar node not found")
        }
        let club = avatar.childNodes.first { $0.childNodes.count == 2 }!
        let handsDown = club.position.y
        let topOfBackswing = PoseFrame(timestamp: 2, points: [
            .leftShoulder: p(0.4, 0.78), .rightShoulder: p(0.6, 0.78),
            .leftElbow: p(0.62, 0.86), .rightElbow: p(0.7, 0.88), .leftWrist: p(0.72, 0.98), .rightWrist: p(0.74, 0.98)
        ])
        meadow.update(shot: nil, elapsed: 0, power: 0, aim: 0, pose: topOfBackswing)
        XCTAssertGreaterThan(club.position.y, handsDown + 2, "hands rise with the tracked pose")
        XCTAssertFalse(club.isHidden)

        meadow.update(shot: nil, elapsed: 0, power: 0, aim: 0, pose: nil)
        let atAddress = club.position
        meadow.update(shot: nil, elapsed: 0, power: 1, aim: 0, pose: nil)
        XCTAssertGreaterThan(club.position.y, atAddress.y + 1, "without a camera the arms swing with the load meter")
    }
}
