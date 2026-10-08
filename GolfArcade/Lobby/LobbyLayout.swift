import Foundation
import simd

/// The club lobby: where everything sits. This is the one data file the lobby reads: the scene builder, the walking rules, the stations, the Quick Menu and the
/// party spots all take their numbers from here, so changing the art (or moving a station) cannot break the rest.
///
/// Metres. `x` runs east, `y` runs north, the plaza's centre is (0, 0). A heading is the way someone faces: 0 = north, +π/2 = east.
/// Nothing in here knows about SceneKit or the menus, so the rules (what is walkable, how long a walk takes) are tested without a screen.
struct LobbyLayout: Sendable {
    typealias P = SIMD2<Float>

    // MARK: shapes
    enum Shape: Sendable {
        case circle(center: P, radius: Float)
        /// A rectangle `size.x` wide (along its own x) and `size.y` deep, turned by `yaw` radians counter-clockwise (seen from above, north up).
        case rect(center: P, size: P, yaw: Float)

        func contains(_ p: P, margin: Float = 0) -> Bool {
            switch self {
            case .circle(let c, let r): return simd_length(p - c) <= r - margin
            case .rect(let c, let s, let yaw):
                let d = p - c, co = cos(-yaw), si = sin(-yaw)
                let local = P(d.x * co - d.y * si, d.x * si + d.y * co)
                return abs(local.x) <= s.x / 2 - margin && abs(local.y) <= s.y / 2 - margin
            }
        }
    }
    struct Obstacle: Sendable { var center: P; var radius: Float }

    // MARK: stations
    /// What a station does when the player presses A in its ring. The menus those open are the ones the app already has.
    enum Action: Equatable, Sendable {
        /// The Locker, opened on one of its tabs ("gear", "customize", "emotes").
        case locker(tab: String)
        /// The gate to a sport's game picker.
        case play(sport: String)
        case howToPlay
        /// The party board: playing with friends.
        case party
        /// Closed for now.
        case shop
    }
    struct Station: Identifiable, Sendable {
        var id: String
        var title: String
        var detail: String
        /// An SF Symbol for the Quick Menu and the prompt.
        var icon: String
        var ring: P
        var radius: Float
        /// The way you face when you arrive (the Quick Menu, coming back from a match).
        var heading: Float
        var action: Action
        var locked = false
    }

    // MARK: the layout
    static let plazaRadius: Float = 8.6
    static let deckHeight: Float = 0.8
    static let jogSpeed: Float = 5.5

    let spawn: P
    let spawnHeading: Float
    let stations: [Station]
    let walkable: [Shape]
    let obstacles: [Obstacle]
    /// Where a party's four characters stand on the terrace, and which way they face.
    let partySpots: [P]
    let partyHeading: Float
    /// Where the camera sits relative to the player (behind and above) and what it looks at.
    let cameraBehind: Float, cameraHeight: Float, cameraLookHeight: Float

    static let standard: LobbyLayout = {
        let lockerX: Float = -17.5, gateY: Float = 18.5, terraceX: Float = 18
        let west = -Float.pi / 2, east = Float.pi / 2, north: Float = 0, southwest = -3 * Float.pi / 4
        let stations = [
            Station(id: "locker-gear", title: "Gear", detail: "Skin, racket, club, shoes", icon: "tshirt.fill", ring: P(-19.4, 3.0), radius: 1.4, heading: west, action: .locker(tab: "gear")),
            Station(id: "locker-colors", title: "Colors", detail: "Shirt, shorts, skin, hair", icon: "paintpalette.fill", ring: P(-19.4, 0.0), radius: 1.4, heading: west, action: .locker(tab: "customize")),
            Station(id: "locker-emotes", title: "Emotes", detail: "Pick the three you take", icon: "face.smiling.fill", ring: P(-19.4, -3.0), radius: 1.4, heading: west, action: .locker(tab: "emotes")),
            Station(id: "gate-tennis", title: "Tennis", detail: "Matches, campaign, training", icon: "tennisball.fill", ring: P(-4.8, 13.9), radius: 1.6, heading: north, action: .play(sport: "tennis")),
            Station(id: "gate-golf", title: "Golf", detail: "Pick a course", icon: "flag.fill", ring: P(4.8, 13.9), radius: 1.6, heading: north, action: .play(sport: "golf")),
            Station(id: "gate-howto", title: "How to Play", detail: "The guide", icon: "questionmark.circle.fill", ring: P(0, 12.9), radius: 1.3, heading: north, action: .howToPlay),
            Station(id: "party-board", title: "Party Board", detail: "Play with friends", icon: "person.3.fill", ring: P(13.6, 0), radius: 1.6, heading: east, action: .party),
            Station(id: "shop", title: "Pro Shop", detail: "Coming soon", icon: "lock.fill", ring: P(-10.9, -10.9), radius: 1.6, heading: southwest, action: .shop, locked: true),
        ]
        let d = Float(0.70710678)
        let walkable: [Shape] = [
            .circle(center: P(0, 0), radius: plazaRadius + 0.2),
            // the four paths, plaza edge to station
            .rect(center: P(-13, 0), size: P(10.4, 4.4), yaw: 0),
            .rect(center: P(0, 13), size: P(4.4, 10.4), yaw: 0),
            .rect(center: P(13, 0), size: P(10.4, 4.4), yaw: 0),
            .rect(center: P(-12.4 * d, -12.4 * d), size: P(9.0, 4.4), yaw: .pi / 4),
            // the places they lead to
            .rect(center: P(lockerX, 0), size: P(10.4, 8.6), yaw: 0),                  // the locker room
            .rect(center: P(0, 14.35), size: P(16.4, 5.7), yaw: 0),                    // in front of the gate
            .rect(center: P(terraceX, 0), size: P(10.4, 9.2), yaw: 0),                 // the terrace deck
            .rect(center: P(-14.6 * d, -14.6 * d), size: P(5.4, 4.4), yaw: .pi / 4),   // the pro shop's porch
        ]
        var obstacles = [Obstacle(center: P(0, 0), radius: 3.9)]   // the tower
        for k in 0..<8 { let a = Float(22.5 + 45 * Double(k)) * .pi / 180; obstacles.append(Obstacle(center: P(cos(a) * 8.0, sin(a) * 8.0), radius: 0.3)) }   // lamps
        obstacles += [P(6.4, 5.4), P(-6.6, -4.8), P(6.8, -2.0), P(-6.9, 2.2)].map { Obstacle(center: $0, radius: 0.85) }   // planters
        obstacles += [Obstacle(center: P(5.2, -6.2), radius: 1.0), Obstacle(center: P(-5.2, 6.2), radius: 1.0)]   // benches
        for px in [-4.9, 0.0, 4.9] { for s in [-1.0, 1.0] { obstacles.append(Obstacle(center: P(lockerX + Float(px), Float(s) * 4.4), radius: 0.35)) } }   // the locker room's posts
        obstacles += [Obstacle(center: P(lockerX - 4.7, 3.0), radius: 0.9), Obstacle(center: P(lockerX - 4.7, -3.0), radius: 0.9)]   // gear rack and emote backdrop
        obstacles += [Obstacle(center: P(0, 17.2), radius: 0.9)]   // how-to sign
        obstacles += [P(-8.2, gateY - 1.2), P(-1.35, gateY - 1.2), P(1.35, gateY - 1.2), P(8.2, gateY - 1.2)].map { Obstacle(center: $0, radius: 1.0) }   // gate piers
        obstacles += [Obstacle(center: P(terraceX - 2.7, 0), radius: 0.9)]   // the party board's post
        obstacles += [Obstacle(center: P(terraceX + 4.4, -4.0), radius: 1.0), Obstacle(center: P(terraceX + 4.2, 4.0), radius: 0.9)]   // lounge and parasol
        return LobbyLayout(spawn: P(-2.0, -5.5), spawnHeading: 0.35, stations: stations, walkable: walkable, obstacles: obstacles,
                           partySpots: [P(20.4, -3.6), P(20.4, -1.2), P(20.4, 1.2), P(20.4, 3.6)], partyHeading: -Float.pi / 2,
                           cameraBehind: 4.6, cameraHeight: 2.5, cameraLookHeight: 1.1)
    }()

    // MARK: rules
    func station(_ id: String) -> Station? { stations.first { $0.id == id } }

    func isWalkable(_ p: P) -> Bool { walkable.contains { $0.contains(p) } && !obstacles.contains { simd_length(p - $0.center) < $0.radius } }

    /// The ground's height at `p`: the terrace deck is raised and reached by a ramp up the end of its path.
    func height(at p: P) -> Float {
        let deck = Self.deckHeight
        if p.x > 10.4, abs(p.y) <= 5.2 {
            let t = min(1, max(0, (p.x - 10.4) / 2.2))
            return deck * t * t * (3 - 2 * t)
        }
        return 0
    }

    /// Where a walker who was at `a` ends up after trying to go to `b`: pushed out of obstacles, kept on the walkable ground, sliding along an edge
    /// when it can. Never inside an obstacle and never off the ground.
    func resolve(from a: P, to b: P) -> P {
        func pushed(_ p: P) -> P {
            var q = p
            for o in obstacles {
                let d = q - o.center, l = simd_length(d)
                if l < o.radius { q = o.center + (l > 1e-4 ? d / l : P(1, 0)) * o.radius }
            }
            return q
        }
        let direct = pushed(b)
        if walkable.contains(where: { $0.contains(direct) }) { return direct }
        // slide: keep whichever axis still lands on the ground
        let tryX = pushed(P(b.x, a.y)), tryY = pushed(P(a.x, b.y))
        let okX = walkable.contains { $0.contains(tryX) }, okY = walkable.contains { $0.contains(tryY) }
        if okX && okY { return simd_length(tryX - a) >= simd_length(tryY - a) ? tryX : tryY }
        if okX { return tryX }
        if okY { return tryY }
        return a
    }

    /// The station whose ring holds `p` (the nearest, when rings touch).
    func station(at p: P) -> Station? {
        stations.filter { simd_length(p - $0.ring) <= $0.radius }.min { simd_length(p - $0.ring) < simd_length(p - $1.ring) }
    }

    /// A free place to stand in a station's ring, facing its heading (the Quick Menu drops you here).
    func arrival(at station: Station) -> P {
        // stand a little before the ring's centre, on the side you walk in from
        let back = P(sin(station.heading), cos(station.heading)) * -0.15
        return station.ring + back
    }

    /// The gate you left from: the place you stand when a match ends (just in front of the gate's ring, looking back toward the plaza).
    func returnPoint(from station: Station) -> (position: P, heading: Float) {
        switch station.action {
        case .play, .howToPlay: return (station.ring + P(0, -2.0), station.heading)
        case .party: return (partySpots[0], partyHeading)
        default: return (arrival(at: station), station.heading)
        }
    }

    /// The length of the shortest walk between two points over the walkable ground (a grid search, 0.5 m cells), or nil when there is none.
    func walkingDistance(from a: P, to b: P) -> Float? {
        let cell: Float = 0.5, minX: Float = -30, minY: Float = -30, cols = 120, rows = 120
        func index(_ p: P) -> (Int, Int) { (Int(((p.x - minX) / cell).rounded(.down)), Int(((p.y - minY) / cell).rounded(.down))) }
        func center(_ c: Int, _ r: Int) -> P { P(minX + (Float(c) + 0.5) * cell, minY + (Float(r) + 0.5) * cell) }
        let start = index(a), goal = index(b)
        guard (0..<cols).contains(start.0), (0..<rows).contains(start.1), (0..<cols).contains(goal.0), (0..<rows).contains(goal.1) else { return nil }
        var dist = [Float](repeating: .infinity, count: cols * rows)
        var open: [(Float, Int, Int)] = [(0, start.0, start.1)]
        dist[start.1 * cols + start.0] = 0
        let steps: [(Int, Int, Float)] = [(1, 0, 1), (-1, 0, 1), (0, 1, 1), (0, -1, 1), (1, 1, 1.4142), (1, -1, 1.4142), (-1, 1, 1.4142), (-1, -1, 1.4142)]
        while !open.isEmpty {
            var best = 0
            for i in open.indices where open[i].0 < open[best].0 { best = i }
            let (d, c, r) = open.remove(at: best)
            if d > dist[r * cols + c] { continue }
            if c == goal.0 && r == goal.1 { return d * cell }
            for (dc, dr, w) in steps {
                let nc = c + dc, nr = r + dr
                guard (0..<cols).contains(nc), (0..<rows).contains(nr), isWalkable(center(nc, nr)) || (nc == goal.0 && nr == goal.1) else { continue }
                let nd = d + w
                if nd < dist[nr * cols + nc] { dist[nr * cols + nc] = nd; open.append((nd, nc, nr)) }
            }
        }
        return nil
    }
}
