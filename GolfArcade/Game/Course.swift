import Foundation

/// A spot on the ground, in yards: `x` to the right of the tee line, `d` down the hole.
struct CoursePoint: Equatable, Hashable, Codable, Sendable {
    var x: Double
    var d: Double

    static let zero = CoursePoint(x: 0, d: 0)

    func distance(to other: CoursePoint) -> Double { hypot(other.x - x, other.d - d) }

    /// Degrees right of straight down the hole from here to `other`.
    func heading(to other: CoursePoint) -> Double { atan2(other.x - x, other.d - d) * 180 / .pi }
}

enum CourseDifficulty: String, CaseIterable, Codable, Sendable {
    case easy, medium, hard

    var displayName: String { rawValue.capitalized }
}

enum CourseHazardKind: String, Codable, Sendable {
    case bunker, water

    var displayName: String { rawValue.capitalized }
}

struct CourseHazard: Identifiable, Equatable, Sendable {
    let id: Int
    let kind: CourseHazardKind
    let x: Double
    let distance: Double
    let width: Double
    let length: Double
    var contour: Double = 0

    func boundaryScale(at angle: Double) -> Double {
        1 + contour * (sin(3*angle+Double(id)) + 0.4*cos(5*angle))
    }

    /// Radius and analytic gradient in the authored outline. Rendering and hazard
    /// scoring share this shape, including the concave coves and bunker lobes.
    func radialSurface(at point: CoursePoint) -> (radius: Double, dx: Double, dd: Double) {
        let rx=max(width/2,0.001),rd=max(length/2,0.001)
        let x=(point.x-self.x)/rx,d=(point.d-distance)/rd,q=hypot(x,d)
        guard q>0.000001 else { return (0,0,0) }
        let angle=atan2(d,x),f=boundaryScale(at:angle)
        let derivative=contour*(3*cos(3*angle+Double(id))-2*sin(5*angle))
        return (q/f,(x/(q*f)+derivative*d/(q*f*f))/rx,
            (d/(q*f)-derivative*x/(q*f*f))/rd)
    }

    func contains(_ point: CoursePoint) -> Bool {
        let extent=1+abs(contour)*1.4
        guard abs(point.x-x)<=max(width/2,0.001)*extent,
              abs(point.d-distance)<=max(length/2,0.001)*extent else { return false }
        return radialSurface(at:point).radius <= 1
    }
}

/// Where a ball comes to rest. Lies change the next shot; water and out of bounds cost a stroke.
enum CourseLie: String, Equatable, Codable, Sendable {
    case tee, fairway, fringe, rough, deepRough, bunker, green, water, outOfBounds

    var displayName: String {
        switch self {
        case .outOfBounds: "Out of bounds"
        case .deepRough: "Deep rough"
        default: rawValue.capitalized
        }
    }

    /// Share of a full shot's power a lie allows. Putts are not affected.
    var powerFactor: Double {
        switch self {
        case .rough: 0.85
        case .deepRough: 0.68
        case .fringe: 0.97
        case .bunker: 0.6
        default: 1
        }
    }

    var penaltyStrokes: Int { self == .water || self == .outOfBounds ? 1 : 0 }
}

/// Shared authored boundary: map, mesh generation and lies all consume these vertices.
struct CourseRegion: Equatable, Sendable {
    let points: [CoursePoint]
    private struct Edge: Equatable, Sendable {
        let a:CoursePoint,b:CoursePoint
        let dx:Double,dd:Double,inverseLengthSquared:Double
        let minX:Double,maxX:Double,minD:Double,maxD:Double
        init(_ a:CoursePoint,_ b:CoursePoint) {
            self.a=a;self.b=b;dx=b.x-a.x;dd=b.d-a.d
            inverseLengthSquared=1/max(0.0001,dx*dx+dd*dd)
            minX=min(a.x,b.x);maxX=max(a.x,b.x);minD=min(a.d,b.d);maxD=max(a.d,b.d)
        }
    }
    private let edges:[Edge]
    private let minX:Double,maxX:Double,minD:Double,maxD:Double
    init(points:[CoursePoint]) {
        self.points=points
        edges=points.indices.map { Edge(points[$0],points[($0+points.count-1)%points.count]) }
        minX=points.map(\.x).min() ?? .infinity;maxX=points.map(\.x).max() ?? -.infinity
        minD=points.map(\.d).min() ?? .infinity;maxD=points.map(\.d).max() ?? -.infinity
    }
    func contains(_ p: CoursePoint) -> Bool {
        guard points.count >= 3,p.x>=minX,p.x<=maxX,p.d>=minD,p.d<=maxD else { return false }
        var inside = false
        for edge in edges {
            if (edge.a.d > p.d) != (edge.b.d > p.d),
               p.x < edge.dx*(p.d-edge.a.d)/edge.dd+edge.a.x { inside.toggle() }
        }
        return inside
    }
    func distance(to p: CoursePoint) -> Double {
        var nearest=Double.infinity
        for edge in edges {
            let boxX=max(0,max(edge.minX-p.x,p.x-edge.maxX))
            let boxD=max(0,max(edge.minD-p.d,p.d-edge.maxD))
            if boxX*boxX+boxD*boxD>=nearest { continue }
            let t=max(0,min(1,((p.x-edge.a.x)*edge.dx+(p.d-edge.a.d)*edge.dd)*edge.inverseLengthSquared))
            // Compare squared distances; only the winning edge needs a square
            // root. This query runs for every terrain/grass sample on hole load.
            let offsetX=p.x-edge.a.x-t*edge.dx,offsetD=p.d-edge.a.d-t*edge.dd
            nearest=min(nearest,offsetX*offsetX+offsetD*offsetD)
        }
        return sqrt(nearest)
    }
}

struct CourseSurface: Equatable, Sendable {
    let heightYards: Double
    let slopeX: Double
    let slopeD: Double
    let lie: CourseLie
}

/// The shape of the ground, in yards: an overall tilt plus mounds, swales and ridges. The ball
/// rolls with it, the turf is drawn on it, and the green-reading grid flows down it, so what you
/// see is exactly what the putt will do.
struct Terrain: Equatable, Sendable {
    struct Feature: Equatable, Sendable {
        var center: CoursePoint
        /// A ridge runs from `center` to `end`; a mound or swale has no end.
        var end: CoursePoint?
        var radius: Double
        /// Yards; negative sinks a swale or hollow.
        var height: Double

        /// Distance from the feature's spine, and the direction away from it.
        fileprivate func span(to point: CoursePoint) -> (distance: Double, awayX: Double, awayD: Double) {
            var nearest = center
            if let end {
                let dx = end.x - center.x, dd = end.d - center.d
                let length = max(dx * dx + dd * dd, 0.0001)
                let t = min(1, max(0, ((point.x - center.x) * dx + (point.d - center.d) * dd) / length))
                nearest = CoursePoint(x: center.x + dx * t, d: center.d + dd * t)
            }
            let distance = point.distance(to: nearest)
            guard distance > 0.0001 else { return (0, 0, 0) }
            return (distance, (point.x - nearest.x) / distance, (point.d - nearest.d) / distance)
        }
    }

    /// Rise per yard toward +x (right) and toward +d (down the hole).
    var tiltX: Double
    var tiltD: Double
    var features: [Feature]

    static let flat = Terrain(tiltX: 0, tiltD: 0, features: [])

    /// Height above the datum, in yards.
    func elevation(at point: CoursePoint) -> Double {
        var height = tiltX * point.x + tiltD * point.d
        for feature in features {
            let u = feature.span(to: point).distance / max(feature.radius, 0.001)
            guard u < 1 else { continue }
            // (1 - u²)²: flat on top and at the rim, so nothing kinks.
            height += feature.height * pow(1 - u * u, 2)
        }
        return height
    }

    /// Rise per yard in x and d. A ball accelerates the opposite way.
    func gradient(at point: CoursePoint) -> (dx: Double, dd: Double) {
        var dx = tiltX, dd = tiltD
        for feature in features {
            let span = feature.span(to: point)
            let radius = max(feature.radius, 0.001)
            let u = span.distance / radius
            guard u < 1, u > 0 else { continue }
            let slope = feature.height * -4 * u * (1 - u * u) / radius
            dx += slope * span.awayX
            dd += slope * span.awayD
        }
        return (dx, dd)
    }

    /// Steepness, as a fraction (0.02 = 2%).
    func slope(at point: CoursePoint) -> Double {
        let g = gradient(at: point)
        return hypot(g.dx, g.dd)
    }
}

/// One hole. The fairway follows `centerline` from the tee (first point) to the pin (last point);
/// all measurements share the flight model's yards so drawing and scoring use one source of truth.
struct Hole: Identifiable, Equatable, Sendable {
    let number: Int
    let par: Int
    let centerline: [CoursePoint]
    let fairwayWidth: Double
    let greenRadius: Double
    var hazards: [CourseHazard]
    var terrain: Terrain = .flat
    var wind: CourseWind = .calm
    var fairwayBoundary: CourseRegion?
    var greenBoundary: CourseRegion?
    var trees: [CourseTree] = []
    /// Cached lake planes shared by rendering and the shot solver. Banks stay outside
    /// the existing hazard boundary, so neither water penalties nor routing drift.
    var waterElevations: [Int: Double] = [:]
    /// Recessed sand floors, derived once from the completed terrain and rim.
    var bunkerFloors: [Int: Double] = [:]
    var simulationVersion = 2

    /// Rough on each side of the fairway. Beyond it (the tree line) is out of bounds.
    static let roughWidth = 24.0
    /// Small, explicit arcade tolerance around the cup, in yards.
    static let cupCaptureRadius = 0.12

    var id: Int { number }
    var tee: CoursePoint { centerline[0] }
    var pin: CoursePoint { centerline[centerline.count - 1] }
    var length: Double { zip(centerline, centerline.dropFirst()).reduce(0) { $0 + $1.0.distance(to: $1.1) } }

    /// Follow the next landing station, not a straight shortcut through a dogleg.
    /// Project onto the nearest route segment so a passed station never aims backwards.
    func recommendedTarget(from ball: CoursePoint) -> CoursePoint {
        guard centerline.count > 2, ball.distance(to: pin) > greenRadius + 20 else { return safeLanding(near: pin, from: ball) }
        var closest = Double.infinity
        var segment = 0
        for i in 0..<(centerline.count - 1) {
            let a = centerline[i], b = centerline[i + 1]
            let dx = b.x - a.x, dd = b.d - a.d
            let t = min(1, max(0, ((ball.x - a.x) * dx + (ball.d - a.d) * dd) / max(0.001, dx * dx + dd * dd)))
            let distance = ball.distance(to: CoursePoint(x: a.x + t * dx, d: a.d + t * dd))
            if distance <= closest { closest = distance; segment = i }
        }
        var station = segment + 1
        while station < centerline.count - 1 && ball.distance(to: centerline[station]) < 35 { station += 1 }
        return safeLanding(near: centerline[station], from: ball)
    }

    /// Landing stations for the overview. These are intended landing areas, not a
    /// claim that the ball rolls along this line or clears every intervening obstacle.
    func recommendedRoute(from ball: CoursePoint) -> [CoursePoint] {
        var route = [ball]
        for _ in 0..<(centerline.count + 2) {
            guard let current = route.last, current.distance(to: pin) > 0.01 else { break }
            let next = recommendedTarget(from: current)
            guard !route.contains(where: { $0.distance(to: next) < 0.01 }) else { break }
            route.append(next)
        }
        return route
    }

    /// A deterministic route recommendation, not a guarantee of the actual shot result.
    /// Favor the center of playable turf and leave a dispersion margin around hazards.
    private func safeLanding(near target: CoursePoint, from ball: CoursePoint) -> CoursePoint {
        if lie(at: ball) == .green { return pin }
        func risk(_ p: CoursePoint) -> Double {
            let margin = min(6, ball.distance(to: p) * 0.04)
            return [(0.0,0.0), (margin,0), (-margin,0), (0,margin), (0,-margin)].reduce(0) { sum, offset in
                let point = CoursePoint(x: p.x + offset.0, d: p.d + offset.1)
                if trees.contains(where: { $0.center.distance(to:point) < $0.trunkRadius+2 }) { return sum+65 }
                switch lie(at: point) {
                case .green, .fairway, .tee: return sum
                case .fringe: return sum + 3
                case .rough: return sum + 15
                case .deepRough: return sum + 30
                case .bunker: return sum + 50
                case .water, .outOfBounds: return sum + 250
                }
            }
        }
        if risk(target) == 0 { return target }
        var best = target, score = Double.infinity
        let spacing = max(3, min(8, fairwayWidth / 5))
        for x in -4...4 { for d in -4...4 {
            let p = CoursePoint(x: target.x + Double(x) * spacing, d: target.d + Double(d) * spacing)
            guard ball.distance(to: p) > 3 else { continue }
            let cost = risk(p) + p.distance(to: target) * 0.8 + distanceFromCenterline(p) * 0.15
            if cost < score { score = cost; best = p }
        } }
        return best
    }

    func distanceFromCenterline(_ point: CoursePoint) -> Double {
        zip(centerline, centerline.dropFirst()).map { a, b in
            let dx = b.x - a.x, dd = b.d - a.d
            let lengthSquared = max(dx * dx + dd * dd, 0.0001)
            let t = min(max(((point.x - a.x) * dx + (point.d - a.d) * dd) / lengthSquared, 0), 1)
            return point.distance(to: CoursePoint(x: a.x + dx * t, d: a.d + dd * t))
        }.min() ?? .infinity
    }

    func lie(at point: CoursePoint) -> CourseLie {
        if let hazard = hazards.first(where: { $0.contains(point) }) {
            return hazard.kind == .water ? .water : .bunker
        }
        if greenBoundary?.contains(point) ?? (point.distance(to: pin) <= greenRadius) { return .green }
        if let greenBoundary, greenBoundary.distance(to:point) <= 2 { return .fringe }
        if point.distance(to: tee) <= 4 { return .tee }
        if let fairwayBoundary {
            if fairwayBoundary.contains(point) { return .fairway }
            let edge = fairwayBoundary.distance(to:point)
            if edge <= 12 { return .rough }
            if edge <= Self.roughWidth { return .deepRough }
            return .outOfBounds
        }
        let offset = distanceFromCenterline(point)
        if offset <= fairwayWidth / 2 { return .fairway }
        if offset <= fairwayWidth / 2 + Self.roughWidth { return .rough }
        return .outOfBounds
    }

    func surface(at point: CoursePoint) -> CourseSurface {
            var height=terrain.elevation(at:point)
            var gradient=terrain.gradient(at:point)
            if fairwayBoundary != nil {
                for hazard in hazards where hazard.kind == .bunker {
                    let extent=1+abs(hazard.contour)*1.4
                    guard abs(point.x-hazard.x)<=hazard.width/2*extent,
                          abs(point.d-hazard.distance)<=hazard.length/2*extent else { continue }
                    let radial=hazard.radialSurface(at:point),radius=radial.radius
                    // A graded floor must not inherit a neighboring mound's
                    // hump. Join it to the unmodified terrain with a C2 blend,
                    // entirely inside the scoring boundary.
                    if radius < 1 {
                        let center=CoursePoint(x:hazard.x,d:hazard.distance)
                        let anchor=bunkerFloors[hazard.id] ?? (terrain.elevation(at:center)-1.25)
                        let dx=max(-0.012,min(0.012,terrain.tiltX)),dd=max(-0.012,min(0.012,terrain.tiltD))
                        let floor=anchor+dx*(point.x-hazard.x)+dd*(point.d-hazard.distance)
                        // A broader sand floor and a more defined cut bank read
                        // as an excavation, not sand draped across a mound.
                        let t=max(0,min(1,(radius-0.52)/0.48))
                        let blend=t*t*t*(t*(t*6-15)+10)
                        let derivative=radius>0.52 ? 30*t*t*(t-1)*(t-1)/0.48 : 0
                        let delta=height-floor
                        height=floor+delta*blend
                        gradient.dx=dx+(gradient.dx-dx)*blend+delta*derivative*radial.dx
                        gradient.dd=dd+(gradient.dd-dd)*blend+delta*derivative*radial.dd
                    }
                }
                for hazard in hazards where hazard.kind == .water {
                    guard let level = waterElevations[hazard.id] else { continue }
                    let rx = max(1, hazard.width / 2), rd = max(1, hazard.length / 2)
                    let extent=(1+6/min(rx,rd))*(1+abs(hazard.contour)*1.4)
                    guard abs(point.x-hazard.x)<=rx*extent,
                          abs(point.d-hazard.distance)<=rd*extent else { continue }
                    let radial=hazard.radialSurface(at:point)
                    let r = radial.radius, width = 6 / min(rx, rd)
                    guard r < 1 + width else { continue }
                    if r <= 1 {
                        height = level; gradient = (0, 0)
                    } else {
                        let t = (r - 1) / width
                        let blend = t*t*t*(t*(t*6-15)+10)
                        let derivative = 30*t*t*(t-1)*(t-1) / width
                        let delta = height - level
                        height = level + delta * blend
                        gradient.dx = gradient.dx * blend + delta * derivative * radial.dx
                        gradient.dd = gradient.dd * blend + delta * derivative * radial.dd
                    }
                }
            }
        return CourseSurface(heightYards:height,slopeX:gradient.dx,slopeD:gradient.dd,lie:lie(at:point))
    }
}

struct Course: Identifiable, Equatable, Sendable {
    let id: String
    let name: String
    let difficulty: CourseDifficulty
    let holes: [Hole]

    var par: Int { holes.reduce(0) { $0 + $1.par } }
    var bestScoreKey: String {
        let physics=holes.map(\.simulationVersion).max() ?? 2
        // Retain old scores on disk, but do not compare the new physical-cup rules with v3.
        return physics >= 4 ? "course.\(id).routing2.physics\(physics).best" : "course.\(id).routing2.best"
    }
    var length: Double { holes.reduce(0) { $0 + $1.length } }
    var bunkerCount: Int { holes.reduce(0) { $0 + $1.hazards.filter { $0.kind == .bunker }.count } }
    var hasWater: Bool { holes.contains { $0.hazards.contains { $0.kind == .water } } }

    private static func p(_ x: Double, _ d: Double) -> CoursePoint { CoursePoint(x: x, d: d) }
    private static func bunker(_ id: Int, _ x: Double, _ d: Double, _ w: Double, _ l: Double) -> CourseHazard {
        CourseHazard(id: id, kind: .bunker, x: x, distance: d, width: w, length: l)
    }
    private static func water(_ id: Int, _ x: Double, _ d: Double, _ w: Double, _ l: Double) -> CourseHazard {
        CourseHazard(id: id, kind: .water, x: x, distance: d, width: w, length: l)
    }
    private static func mound(_ x: Double, _ d: Double, r: Double, h: Double) -> Terrain.Feature {
        Terrain.Feature(center: p(x, d), end: nil, radius: r, height: h)
    }
    private static func ridge(_ x0: Double, _ d0: Double, _ x1: Double, _ d1: Double, r: Double, h: Double) -> Terrain.Feature {
        Terrain.Feature(center: p(x0, d0), end: p(x1, d1), radius: r, height: h)
    }
    private static func shaped(_ hole: Hole, _ terrain: Terrain) -> Hole {
        var shaped = hole
        shaped.terrain = terrain
        return shaped
    }

    // Greens: contours of a few tenths of a yard across the putting surface (1–3% slopes) so a
    // putt breaks, plus gentle movement through the fairways. Heights are in yards.
    static let easy = Course(id: "meadow", name: "Meadow Run", difficulty: .easy, holes: [
        shaped(Hole(number: 1, par: 4, centerline: [p(0, 0), p(0, 185), p(38, 245), p(105, 330)], fairwayWidth: 50, greenRadius: 20,
             hazards: [bunker(0, -22, 174, 16, 27), bunker(1, 60, 255, 18, 30), bunker(2, 86, 324, 14, 21)]),
             Terrain(tiltX: 0.004, tiltD: 0.003, features: [
                mound(118, 322, r: 26, h: 0.4), mound(96, 342, r: 16, h: -0.2),
                mound(-30, 120, r: 60, h: 1.8), ridge(20, 200, 60, 230, r: 30, h: -1.2)])),
        shaped(Hole(number: 2, par: 4, centerline: [p(0, 0), p(0, 170), p(-18, 280)], fairwayWidth: 46, greenRadius: 20,
             hazards: [bunker(0, 23, 172, 16, 22), bunker(1, -38, 272, 12, 16)]),
             Terrain(tiltX: -0.006, tiltD: 0.004, features: [
                ridge(-30, 268, -6, 292, r: 15, h: 0.25), mound(-18, 265, r: 15, h: -0.18),
                mound(40, 90, r: 55, h: 1.5), mound(-45, 200, r: 45, h: 1.1)])),
        shaped(Hole(number: 3, par: 3, centerline: [p(0, 0), p(8, 115)], fairwayWidth: 50, greenRadius: 20,
             hazards: [bunker(0, -12, 108, 14, 18)]),
             Terrain(tiltX: 0.008, tiltD: -0.005, features: [mound(18, 106, r: 18, h: 0.3), mound(-4, 126, r: 14, h: -0.18)]))
    ])

    static let medium = Course(id: "pine", name: "Pine Bend", difficulty: .medium, holes: [
        shaped(Hole(number: 1, par: 4, centerline: [p(0, 0), p(0, 205), p(55, 285), p(110, 345)], fairwayWidth: 36, greenRadius: 17,
             hazards: [bunker(0, -17, 194, 16, 26), bunker(1, 28, 234, 14, 22), bunker(2, 125, 339, 12, 19)]),
             Terrain(tiltX: -0.01, tiltD: 0.006, features: [
                ridge(96, 336, 124, 352, r: 14, h: 0.28), mound(112, 358, r: 15, h: -0.22),
                mound(-50, 150, r: 60, h: 2.4), mound(70, 250, r: 40, h: -1.4)])),
        shaped(Hole(number: 2, par: 3, centerline: [p(0, 0), p(-6, 160)], fairwayWidth: 32, greenRadius: 16,
             hazards: [bunker(0, -24, 154, 14, 20), bunker(1, 10, 172, 14, 14)]),
             Terrain(tiltX: 0.012, tiltD: 0.008, features: [mound(-16, 150, r: 16, h: 0.3), mound(4, 168, r: 13, h: -0.2), mound(-40, 80, r: 45, h: 1.6)])),
        shaped(Hole(number: 3, par: 4, centerline: [p(0, 0), p(0, 180), p(-35, 300)], fairwayWidth: 34, greenRadius: 17,
             hazards: [bunker(0, 18, 188, 16, 24), bunker(1, -19, 170, 14, 22), bunker(2, -53, 292, 12, 18)]),
             Terrain(tiltX: 0.006, tiltD: -0.012, features: [
                ridge(-50, 292, -20, 306, r: 15, h: 0.3), mound(-30, 288, r: 14, h: -0.18),
                ridge(-40, 120, 40, 140, r: 35, h: 2.2)]))
    ])

    static let hard = Course(id: "cliff", name: "Cliffwater", difficulty: .hard, holes: [
        shaped(Hole(number: 1, par: 4, centerline: [p(0, 0), p(0, 225), p(-70, 300), p(-115, 380)], fairwayWidth: 28, greenRadius: 14,
             hazards: [water(0, 35, 145, 45, 185), bunker(1, -15, 207, 12, 26), bunker(2, -100, 373, 12, 19)]),
             Terrain(tiltX: -0.012, tiltD: 0.008, features: [
                mound(-104, 372, r: 15, h: 0.32), ridge(-128, 372, -110, 392, r: 13, h: -0.22),
                mound(-60, 260, r: 50, h: 3.0), mound(30, 60, r: 50, h: -2.0)])),
        shaped(Hole(number: 2, par: 5, centerline: [p(0, 0), p(0, 240), p(-30, 390), p(-30, 470)], fairwayWidth: 26, greenRadius: 14,
             hazards: [bunker(0, 13, 245, 12, 24), water(1, -30, 415, 56, 22), bunker(2, -45, 468, 12, 16), bunker(3, -14, 480, 10, 12)]),
             Terrain(tiltX: 0.01, tiltD: -0.008, features: [
                ridge(-42, 456, -18, 456, r: 12, h: 0.26), mound(-30, 486, r: 12, h: -0.2),
                mound(30, 200, r: 55, h: 2.6), mound(-60, 330, r: 45, h: 2.0)])),
        shaped(Hole(number: 3, par: 3, centerline: [p(0, 0), p(0, 170)], fairwayWidth: 24, greenRadius: 13,
             hazards: [water(0, 0, 95, 80, 44), bunker(1, -14, 164, 12, 16), bunker(2, 14, 177, 12, 14)]),
             Terrain(tiltX: -0.012, tiltD: 0.01, features: [mound(12, 181, r: 14, h: 0.3), mound(-12, 159, r: 12, h: -0.2), mound(0, 30, r: 40, h: 1.2)]))
    ])

    /// Original compact resort loop: a broad dogleg, a diagonal pond carry and a
    /// two-stage par five. Authored here, not traced from another game's layout.
    static let sunward = Course(id: "sunward", name: "Sunward Links · Legacy 3", difficulty: .medium, holes: [
        shaped(Hole(number: 1, par: 4,
            centerline: [p(0, 0), p(-12, 165), p(44, 242), p(112, 306)],
            fairwayWidth: 43, greenRadius: 19,
            hazards: [water(0, -49, 125, 40, 145), bunker(1, 9, 159, 16, 29),
                      bunker(2, 89, 294, 15, 23), bunker(3, 134, 314, 14, 20)]),
            Terrain(tiltX: 0.003, tiltD: 0.004, features: [
                mound(-44, 90, r: 50, h: 1.4), mound(58, 220, r: 48, h: 1.8),
                ridge(97, 314, 122, 321, r: 21, h: 0.23)])),
        shaped(Hole(number: 2, par: 3, centerline: [p(0, 0), p(36, 136)],
            fairwayWidth: 38, greenRadius: 19,
            hazards: [water(0, 12, 72, 57, 45), bunker(1, 16, 133, 13, 23),
                      bunker(2, 51, 151, 13, 17)]),
            Terrain(tiltX: -0.005, tiltD: 0.002, features: [
                mound(47, 127, r: 22, h: 0.26), mound(23, 149, r: 20, h: -0.16)])),
        shaped(Hole(number: 3, par: 5,
            centerline: [p(0, 0), p(8, 188), p(-55, 325), p(-109, 432)],
            fairwayWidth: 40, greenRadius: 20,
            hazards: [bunker(0, 28, 179, 17, 27), water(1, -91, 299, 42, 118),
                      bunker(2, -38, 329, 15, 27), bunker(3, -131, 421, 15, 23)]),
            Terrain(tiltX: 0.003, tiltD: -0.002, features: [
                ridge(-5, 220, -35, 260, r: 48, h: 1.8), mound(-50, 355, r: 38, h: -0.9),
                ridge(-122, 442, -96, 439, r: 22, h: 0.25)]))
    ])

    /// New layout identity deliberately does not reuse the legacy three-hole score key.
    static let sunwardResort: Course = {
        var holes = sunward.holes + [
            shaped(Hole(number:4,par:4,centerline:[p(0,0),p(4,175),p(83,265),p(111,337)],fairwayWidth:38,greenRadius:18,
                hazards:[bunker(0,-15,163,17,29),bunker(1,72,249,16,22),bunker(2,130,330,16,24)]),
                Terrain(tiltX:0.004,tiltD:0.003,features:[mound(38,196,r:40,h:2),mound(118,345,r:24,h:0.3)])),
            shaped(Hole(number:5,par:3,centerline:[p(0,0),p(-27,153)],fairwayWidth:35,greenRadius:18,
                hazards:[bunker(0,-47,145,17,27),bunker(1,-8,164,16,22)]),
                Terrain(tiltX:-0.003,tiltD:0.018,features:[mound(-34,161,r:25,h:0.3)])),
            shaped(Hole(number:6,par:5,centerline:[p(0,0),p(0,207),p(60,340),p(32,476)],fairwayWidth:44,greenRadius:21,
                hazards:[water(0,-41,316,48,180),bunker(1,23,199,17,27),bunker(2,44,347,15,25),bunker(3,51,472,16,25)]),
                Terrain(tiltX:0.002,tiltD:-0.002,features:[ridge(-10,260,45,306,r:40,h:1.3),mound(25,489,r:27,h:0.28)])),
            shaped(Hole(number:7,par:4,centerline:[p(0,0),p(-8,181),p(-72,294),p(-61,370)],fairwayWidth:42,greenRadius:19,
                hazards:[bunker(0,14,172,19,27),bunker(1,-91,287,18,32),bunker(2,-79,367,15,20)]),
                Terrain(tiltX:0.005,tiltD:0.003,features:[ridge(-20,90,18,148,r:45,h:2.1),mound(-55,251,r:45,h:-1.2),mound(-53,379,r:24,h:0.25)])),
            shaped(Hole(number:8,par:4,centerline:[p(0,0),p(10,152),p(70,236)],fairwayWidth:39,greenRadius:17,
                hazards:[bunker(0,-9,141,19,33),bunker(1,32,169,21,25),bunker(2,53,231,17,24),water(3,111,196,39,100)]),
                Terrain(tiltX:-0.004,tiltD:0.002,features:[mound(76,245,r:21,h:0.26)])),
            shaped(Hole(number:9,par:4,centerline:[p(0,0),p(-6,195),p(53,301),p(105,370)],fairwayWidth:46,greenRadius:22,
                hazards:[water(0,-49,171,42,173),bunker(1,17,184,19,31),bunker(2,81,359,17,28),bunker(3,128,375,17,24)]),
                Terrain(tiltX:0.003,tiltD:-0.003,features:[mound(46,246,r:42,h:1.6),ridge(91,382,119,381,r:25,h:0.25)]))
        ]
        let breezes=[CourseWind(x:1.3,d:0.6),CourseWind(x:-1.5,d:-0.5),CourseWind(x:1,d:1.2),
            CourseWind(x:-0.8,d:0.5),CourseWind(x:1.2,d:-0.8),CourseWind(x:-1,d:1.4),
            CourseWind(x:1.5,d:0),CourseWind(x:-1.2,d:-0.6),CourseWind(x:0.8,d:0.7)]
        for index in holes.indices {
            var hole=holes[index]
            var left:[CoursePoint]=[],right:[CoursePoint]=[]
            for segment in 0..<(hole.centerline.count-1) {
                let a=hole.centerline[segment],b=hole.centerline[segment+1]
                let previous=hole.centerline[max(0,segment-1)]
                let next=hole.centerline[min(hole.centerline.count-1,segment+2)]
                // A continuous tangent at every dogleg prevents overlapping
                // left/right offsets from making triangular fairway notches.
                let m0=CoursePoint(x:(b.x-previous.x)*0.5,d:(b.d-previous.d)*0.5)
                let m1=CoursePoint(x:(next.x-a.x)*0.5,d:(next.d-a.d)*0.5)
                let samples=max(12,Int(ceil(a.distance(to:b)/5)))
                let count=segment == hole.centerline.count-2 ? samples+1 : samples
                for step in 0..<count {
                    let t=Double(step)/Double(samples),t2=t*t,t3=t2*t
                    let h0=2*t3-3*t2+1,h1=t3-2*t2+t,h2 = -2*t3+3*t2,h3=t3-t2
                    let x=h0*a.x+h1*m0.x+h2*b.x+h3*m1.x
                    let d=h0*a.d+h1*m0.d+h2*b.d+h3*m1.d
                    let dx=(6*t2-6*t)*a.x+(3*t2-4*t+1)*m0.x+(-6*t2+6*t)*b.x+(3*t2-2*t)*m1.x
                    let dd=(6*t2-6*t)*a.d+(3*t2-4*t+1)*m0.d+(-6*t2+6*t)*b.d+(3*t2-2*t)*m1.d
                    let length=max(0.001,hypot(dx,dd))
                    let width=hole.fairwayWidth/2*(0.88+0.16*sin((Double(segment)+t)*2.1+Double(index)))
                    left.append(p(x-dd/length*width,d+dx/length*width))
                    right.append(p(x+dd/length*width,d-dx/length*width))
                }
            }
            hole.fairwayBoundary=CourseRegion(points:left+right.reversed())
            hole.greenBoundary=CourseRegion(points:(0..<96).map { vertex in
                let angle=Double(vertex)/96 * .pi*2
                let radius=hole.greenRadius*(1+0.10*sin(angle*3+Double(index)))
                return p(hole.pin.x+cos(angle)*radius,hole.pin.d+sin(angle)*radius*0.93)
            })
            hole.wind=breezes[index]
            hole.simulationVersion=7
            // Playable relief, with every new mound clear of the putting surface.
            for side in [-1.0,1.0] {
                for forward in [-22.0,34.0] {
                    // Lower approach-side shoulders keep greenside bunkers
                    // carved into a bank instead of perched on a tall cone.
                    let height = forward < 0 ? 1.4 : (side < 0 ? 3.0 : 3.5)
                    hole.terrain.features.append(mound(hole.pin.x+side*(hole.greenRadius+12),
                        hole.pin.d+forward,r:forward < 0 ? 16 : 22,h:height))
                }
            }
            hole.terrain.features.append(ridge(hole.pin.x-14,hole.pin.d+hole.greenRadius+19,
                hole.pin.x+14,hole.pin.d+hole.greenRadius+19,r:14,h:3.2))
            for (a,b) in zip(hole.centerline,hole.centerline.dropFirst()) {
                let center=p((a.x+b.x)/2,(a.d+b.d)/2)
                if center.distance(to:hole.pin)>60 && center.distance(to:hole.tee)>45 {
                    let heading=a.heading(to:b) * .pi/180
                    for side in [-1.0,1.0] {
                        hole.terrain.features.append(mound(center.x+cos(heading)*side*hole.fairwayWidth*0.6,
                            center.d-sin(heading)*side*hole.fairwayWidth*0.6,r:23,h:1.7))
                    }
                }
            }
            for hazard in hole.hazards.indices {
                hole.hazards[hazard].contour = hole.hazards[hazard].kind == .water ? 0.085 : 0.12
            }
            for lake in hole.hazards where lake.kind == .water {
                let edge = (0..<64).map { index in
                    let a = Double(index) * 2 * .pi / 64
                    let radius=lake.boundaryScale(at:a)
                    return hole.terrain.elevation(at: p(lake.x + cos(a)*lake.width/2*radius,
                        lake.distance + sin(a)*lake.length/2*radius))
                }
                hole.waterElevations[lake.id] = (edge.min() ?? 0) - 0.85
            }
            for bunker in hole.hazards where bunker.kind == .bunker {
                let center=p(bunker.x,bunker.distance)
                let rim=(0..<64).map { index in
                    let a=Double(index)*2 * .pi/64,r=bunker.boundaryScale(at:a)
                    return hole.terrain.elevation(at:p(bunker.x+cos(a)*bunker.width/2*r,
                        bunker.distance+sin(a)*bunker.length/2*r))
                }
                hole.bunkerFloors[bunker.id]=min(hole.terrain.elevation(at:center)-1.6,(rim.min() ?? 0)-0.9)
            }
            // Playable trunks are explicit, separate from decorative distant vegetation.
            let station=hole.centerline[min(1,hole.centerline.count-1)]
            hole.trees=[CourseTree(id:0,center:p(station.x-hole.fairwayWidth*0.43,station.d+12),trunkRadius:0.42,trunkHeight:7),
                        CourseTree(id:1,center:p(station.x+hole.fairwayWidth*0.47,station.d-18),trunkRadius:0.36,trunkHeight:6.5)]
            holes[index]=hole
        }
        return Course(id:"sunward-resort-v1",name:"Sunward Resort · Nine",difficulty:.medium,holes:holes)
    }()

    static let all = [easy, medium, hard, sunwardResort, sunward]
}
