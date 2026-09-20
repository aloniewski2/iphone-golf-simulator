import XCTest
@testable import GolfArcade

/// The ground has shape, and the ball answers to it.
final class TerrainTests: XCTestCase {
    private let sloped = Terrain(tiltX: 0.02, tiltD: -0.01, features: [
        Terrain.Feature(center: CoursePoint(x: 10, d: 40), end: nil, radius: 12, height: 0.6),
        Terrain.Feature(center: CoursePoint(x: -20, d: 10), end: CoursePoint(x: 20, d: 20), radius: 8, height: -0.4)
    ])

    func testGradientMatchesTheSlopeOfTheSurface() {
        for (x, d) in [(0.0, 0.0), (12.0, 44.0), (-15.0, 12.0), (4.0, 36.0), (30.0, 5.0)] {
            let point = CoursePoint(x: x, d: d)
            let g = sloped.gradient(at: point)
            let h = 0.001
            let numericX = (sloped.elevation(at: CoursePoint(x: x + h, d: d)) - sloped.elevation(at: CoursePoint(x: x - h, d: d))) / (2 * h)
            let numericD = (sloped.elevation(at: CoursePoint(x: x, d: d + h)) - sloped.elevation(at: CoursePoint(x: x, d: d - h))) / (2 * h)
            XCTAssertEqual(g.dx, numericX, accuracy: 0.0005, "\(point)")
            XCTAssertEqual(g.dd, numericD, accuracy: 0.0005, "\(point)")
        }
        XCTAssertEqual(Terrain.flat.elevation(at: CoursePoint(x: 50, d: 300)), 0)
        XCTAssertEqual(sloped.elevation(at: CoursePoint(x: 10, d: 40)), 0.02 * 10 - 0.01 * 40 + 0.6, accuracy: 0.0001, "a mound's full height at its centre")
    }

    func testEveryAuthoredGreenIsPuttableButNotFlat() {
        for course in Course.all {
            for hole in course.holes {
                // Check the actual putting surface, not the surrounding square's
                // rough/bunker corners. Organic greens extend past their nominal
                // radius in places, so sample the full boundary's extents too.
                let radius=hole.greenRadius*1.15
                let slopes = stride(from: -radius, through: radius, by: 1).flatMap { x in
                    stride(from: -radius, through: radius, by: 1).compactMap { d -> Double? in
                        let point=CoursePoint(x:hole.pin.x+x,d:hole.pin.d+d)
                        guard hole.lie(at:point) == .green else { return nil }
                        let surface=hole.surface(at:point)
                        return hypot(surface.slopeX,surface.slopeD)
                    }
                }
                XCTAssertGreaterThan(slopes.count,30)
                let steepest=slopes.max() ?? 0
                XCTAssertGreaterThan(steepest, 0.008, "\(course.name) \(hole.number): a green with nothing to read")
                XCTAssertLessThan(steepest, 0.06, "\(course.name) \(hole.number): a putt could never stop on that")
                XCTAssertLessThan(hole.terrain.slope(at: hole.pin), 0.035, "\(course.name) \(hole.number): the cup sits on a manageable slope")
            }
        }
    }

    func testPuttBreaksDownhillAndClimbsShort() throws {
        func flat(_ terrain: Terrain) -> Hole {
            var hole = Hole(number: 1, par: 3, centerline: [.zero, CoursePoint(x: 0, d: 100)], fairwayWidth: 40, greenRadius: 60, hazards: [])
            hole.terrain = terrain
            return hole
        }
        let origin = CoursePoint(x: 0, d: 80)
        let level = RangeShot(id: 1, club: .putter, power: 0.5, aim: 0, origin: origin, heading: 0, hole: flat(.flat))
        let tipsRight = RangeShot(id: 1, club: .putter, power: 0.5, aim: 0, origin: origin, heading: 0, hole: flat(Terrain(tiltX: -0.03, tiltD: 0, features: [])))
        let uphill = RangeShot(id: 1, club: .putter, power: 0.5, aim: 0, origin: origin, heading: 0, hole: flat(Terrain(tiltX: 0, tiltD: 0.03, features: [])))
        let downhill = RangeShot(id: 1, club: .putter, power: 0.5, aim: 0, origin: origin, heading: 0, hole: flat(Terrain(tiltX: 0, tiltD: -0.03, features: [])))
        XCTAssertEqual(level.rest.x, 0, accuracy: 0.01, "nothing tips a putt on level ground")
        XCTAssertGreaterThan(tipsRight.rest.x, 0.5, "ground falling to the right carries the ball right")
        XCTAssertGreaterThan(downhill.rest.d - origin.d, (level.rest.d - origin.d) * 1.2)
        XCTAssertLessThan(uphill.rest.d - origin.d, (level.rest.d - origin.d) * 0.85)
        XCTAssertEqual(level.total, level.flight.total, accuracy: 0.15, "a level green leaves the putter's calibrated distance alone")
        XCTAssertGreaterThan(level.duration, level.flight.duration * 1.3, "a quick green takes its time")
        // The path is continuous and the preview reads the same path the shot takes.
        var previous = tipsRight.position(at: 0)
        for time in stride(from: 0.0, through: tipsRight.duration, by: 1.0 / 60) {
            let point = tipsRight.position(at: time)
            XCTAssertLessThan(hypot(point.lateralYards - previous.lateralYards, point.distanceYards - previous.distanceYards), 0.6, "\(time)")
            previous = point
        }
        XCTAssertEqual(tipsRight.landing.lateralYards, tipsRight.rest.x, accuracy: 0.001)
    }

    func testBallStopsOnGentleSlopesAndRunsOffSteepOnes() {
        var hole = Hole(number: 1, par: 3, centerline: [.zero, CoursePoint(x: 0, d: 100)], fairwayWidth: 60, greenRadius: 10, hazards: [])
        hole.terrain = Terrain(tiltX: 0, tiltD: -0.05, features: [])
        let gentle = RangeShot(id: 1, club: .putter, power: 0.2, aim: 0, origin: CoursePoint(x: 0, d: 50), heading: 0, hole: hole)
        XCTAssertLessThan(gentle.duration, BallFlight.maxDuration - 0.1, "a 5% slope is not enough to keep a ball rolling forever")
        hole.terrain = Terrain(tiltX: 0, tiltD: -0.5, features: [])
        let cliff = RangeShot(id: 1, club: .putter, power: 0.2, aim: 0, origin: CoursePoint(x: 0, d: 50), heading: 0, hole: hole)
        XCTAssertGreaterThan(cliff.rest.d, 90, "a 50% face keeps the ball going")
    }

    func testAPuttStillDropsOnAContouredGreen() throws {
        let hole = Course.easy.holes[2]
        let start = CoursePoint(x: hole.pin.x, d: hole.pin.d - 3)
        let read = GreenRead(terrain: hole.terrain, from: start, to: hole.pin)
        XCTAssertNotEqual(read.label, "FLAT", "hole 3's green has something to read")
        // Somewhere in a sensible window of line and pace, the putt drops.
        var holed: (aim: Double, power: Double)?
        search: for aim in stride(from: -8.0, through: 8, by: 0.5) {
            for power in stride(from: 0.2, through: 0.6, by: 0.005) {
                let putt = RangeShot(id: 1, club: .putter, power: power, aim: aim, origin: start, heading: 0, hole: hole)
                if putt.isHoled { holed = (aim, power); break search }
            }
        }
        let made = try XCTUnwrap(holed, "a three-yard putt on hole 3 can be holed")
        // And the read points the right way: the holing line is aimed against the break.
        if abs(read.crossSlope) > 0.005 {
            XCTAssertEqual(made.aim.sign, (-read.crossSlope).sign, "aim into the break: \(read.label), holed at \(made.aim)°")
        }
    }

    func testGreenReadLabelsRiseAndBreak() {
        let uphill = GreenRead(terrain: Terrain(tiltX: 0, tiltD: 0.02, features: []), from: .zero, to: CoursePoint(x: 0, d: 10))
        XCTAssertEqual(uphill.rise, 0.02, accuracy: 0.0001)
        XCTAssertEqual(uphill.crossSlope, 0, accuracy: 0.0001)
        XCTAssertEqual(uphill.label, "UPHILL 2.0%")
        // Ground falling to the right of the line tips the ball right.
        let breaking = GreenRead(terrain: Terrain(tiltX: -0.015, tiltD: 0, features: []), from: .zero, to: CoursePoint(x: 0, d: 10))
        XCTAssertGreaterThan(breaking.crossSlope, 0.01)
        XCTAssertEqual(breaking.label, "BREAKS RIGHT 1.5%")
        XCTAssertEqual(GreenRead(terrain: .flat, from: .zero, to: CoursePoint(x: 3, d: 4)).label, "FLAT")
    }

    func testCameraPuttsTakeTheirLineFromTheAim() throws {
        var golfer = SyntheticGolfer(stroke: .putt)
        golfer.addressHold = 1.0
        var detector = ArmSwingDetector()
        detector.configure(for: .putter)
        var impacts: [SwingImpact] = []
        for pose in golfer.poses() {
            let sample = ArmSwingDetector.Sample(frame: pose.frame!, frameAspect: pose.aspect, certifiedSpace: detector.certifiedSpace)
            if case .impact(let impact)? = detector.ingest(sample, at: pose.time) { impacts.append(impact) }
        }
        XCTAssertEqual(try XCTUnwrap(impacts.first).startLineDegrees, 0, "a front camera cannot read a putter face; the stroke is distance only")
    }
}
