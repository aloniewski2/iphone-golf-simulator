import SceneKit
import SwiftUI

@MainActor
final class MeadowScene {
    let scene = SCNScene()
    let camera = SCNNode()
    private let ball = SCNNode()
    private let shadow = SCNNode()
    private let trail = SCNNode()
    private let preview = SCNNode()
    private let landingRing = SCNNode()
    private var lastPreview: RangeShot?
    private let impact = SCNNode()
    private let golfer = Golfer()
    private let golferMount = SCNNode()
    private let cupMarker = SCNNode()
    private let greenGrid = SCNNode()
    private let beads = SCNNode()
    private var beadAges: [Double] = []
    private var lastBeadTime: Double?
    private let flag = SCNNode()
    private let hole: Hole?
    private var lastShot: RangeShot?
    private var lastElapsed = 0.0

    /// How the green is framed while putting: low behind the ball, or straight down to read the line.
    enum PuttView { case behind, overhead }

    init(mode: GameMode = .range) {
        hole = mode.hole
        scene.background.contents = UIColor(red: 0.65, green: 0.84, blue: 0.88, alpha: 1)
        scene.fogColor = UIColor(red: 0.65, green: 0.84, blue: 0.88, alpha: 1)
        scene.fogStartDistance = 290
        scene.fogEndDistance = 600
        camera.camera = SCNCamera()
        // A phone is held upright: a tall field of view is what keeps the fairway and the far
        // side of the green in the frame, where the games' landscape cameras use a wide one.
        camera.camera?.fieldOfView = 68
        camera.camera?.zNear = 0.5
        camera.camera?.zFar = 900
        scene.rootNode.addChildNode(camera)
        let sun = SCNNode()
        sun.light = SCNLight()
        sun.light?.type = .directional
        sun.light?.intensity = 1300
        sun.eulerAngles = SCNVector3(-0.85, -0.4, 0)
        scene.rootNode.addChildNode(sun)
        let ambient = SCNNode()
        ambient.light = SCNLight()
        ambient.light?.type = .ambient
        ambient.light?.intensity = 650
        scene.rootNode.addChildNode(ambient)

        let ground = SCNBox(width: 900, height: 1, length: 1000, chamferRadius: 0)
        add(ground, color: UIColor(red: 0.18, green: 0.40, blue: 0.29, alpha: 1), at: SCNVector3(0, -0.8, -220))
        switch mode {
        case .range: buildRange()
        case .hole(let hole): buildCourse(hole)
        }
        // Distant hills on the horizon, far enough past the green to sit in the haze from there.
        for i in 0..<6 {
            let hill = SCNSphere(radius: 55)
            hill.segmentCount = 12
            let node = add(hill, color: UIColor(red: 0.27, green: 0.47, blue: 0.41, alpha: 1), at: SCNVector3(Float(i * 80 - 200), -14, mode.hole == nil ? -340 : -580))
            node.scale = SCNVector3(1.4, 0.9, 1)
        }
        ball.geometry = SCNSphere(radius: 0.42)
        ball.geometry?.firstMaterial?.diffuse.contents = UIColor.white
        ball.geometry?.firstMaterial?.emission.contents = UIColor(white: 0.3, alpha: 1)
        scene.rootNode.addChildNode(ball)
        shadow.geometry = SCNCylinder(radius: 0.9, height: 0.025)
        shadow.geometry?.firstMaterial?.diffuse.contents = UIColor.black.withAlphaComponent(0.25)
        scene.rootNode.addChildNode(shadow)
        golferMount.addChildNode(golfer.node)
        scene.rootNode.addChildNode(golferMount)
        scene.rootNode.addChildNode(trail)
        for _ in 0..<36 {
            let dot = SCNNode(geometry: SCNSphere(radius: 0.22))
            dot.geometry?.firstMaterial?.diffuse.contents = UIColor.white.withAlphaComponent(0.8)
            dot.geometry?.firstMaterial?.emission.contents = UIColor(white: 0.3, alpha: 1)
            preview.addChildNode(dot)
        }
        scene.rootNode.addChildNode(preview)
        landingRing.geometry = SCNTorus(ringRadius: 2.4, pipeRadius: 0.2)
        landingRing.geometry?.firstMaterial?.diffuse.contents = UIColor.systemYellow
        landingRing.geometry?.firstMaterial?.emission.contents = UIColor.systemYellow.withAlphaComponent(0.5)
        scene.rootNode.addChildNode(landingRing)
        impact.geometry = SCNSphere(radius: 1)
        impact.geometry?.firstMaterial?.diffuse.contents = UIColor.systemYellow
        impact.geometry?.firstMaterial?.emission.contents = UIColor.systemYellow
        impact.position = SCNVector3(0, 0.6, 0)
        scene.rootNode.addChildNode(impact)
        update(shot: nil, elapsed: 0, power: 0, aim: 0)
    }

    /// `swingAngle` is where the player's swing is right now, in degrees of arc: 0 at address,
    /// positive going back, negative through. Once a shot is launched the golfer plays its own
    /// downswing and follow-through, and the live angle is ignored until the next ball.
    /// `ball`/`heading` say where the ball rests and which way the next shot faces when no shot is in flight.
    /// `focusYards` is how far the next shot is expected to go; short putts pull the camera in close.
    /// `preview` is the predicted shot for the current club, aim, and load, drawn as a dotted flight
    /// and a landing ring whenever no ball is in the air.
    /// `putting` switches to the putting presentation: the flag comes out, the slope grid and its
    /// beads appear, and the camera holds still low behind the ball (or straight above the line,
    /// `puttView: .overhead`) while the ball rolls.
    func update(shot: RangeShot?, elapsed: Double, power: Double, aim: Double, swingAngle: Double = 0, handedness: Handedness = .right,
                ball restingBall: CoursePoint = CoursePoint(x: 0, z: 0), heading: Double = 0, focusYards: Double = 60, preview: RangeShot? = nil,
                putting: Bool = false, puttView: PuttView = .behind) {
        SCNTransaction.begin()
        SCNTransaction.disableActions = true
        golfer.handedness = handedness
        if let shot {
            if lastShot != shot || elapsed < lastElapsed - 0.5 { golfer.launch(replay: lastShot == shot) }
            golfer.animate(elapsed: elapsed)
        } else {
            golfer.follow(swingAngle)
        }
        lastElapsed = elapsed
        if lastShot != shot {
            trail.childNodes.forEach { $0.removeFromParentNode() }
            if let shot {
                let isPutt = shot.club == .putter && shot.lie == .green
                for i in 0..<60 {
                    let point = shot.position(at: Double(i) / 59 * shot.duration)
                    // Well under the ball's size and faint, so the ball at rest never reads as one more dot.
                    let dot = SCNNode(geometry: SCNSphere(radius: 0.13))
                    dot.geometry?.firstMaterial?.diffuse.contents = UIColor.white.withAlphaComponent(0.45)
                    dot.simdPosition = world(point, origin: shot.origin, heading: shot.heading) + simd_float3(0, isPutt ? 0.2 : 0.5, 0)
                    trail.addChildNode(dot)
                }
            }
            lastShot = shot
        }
        // Everything below is relative to the shot's origin and heading: the tee facing downrange
        // on the range, the ball facing the pin on a hole. `base` sits on the grass at the origin.
        let origin = shot?.origin ?? restingBall
        let facing = shot?.heading ?? heading
        let point = shot?.position(at: elapsed) ?? FlightPoint(lateralYards: 0, heightYards: Double(elevation(Float(origin.x), Float(origin.z))), distanceYards: 0)
        let ballWorld = world(point, origin: origin, heading: facing)
        let forward = Self.forward(facing)
        let right = simd_float3(-forward.z, 0, forward.x)
        let base = grounded(simd_float3(Float(origin.x), 0, Float(origin.z)))
        let along = Float(point.distanceYards)
        let lateral = Float(point.lateralYards)
        let aboveGround = max(0, ballWorld.y - elevation(ballWorld.x, ballWorld.z))
        ball.isHidden = shot?.isHoled == true && elapsed >= (shot?.duration ?? 0)
        shadow.simdPosition = grounded(ballWorld, up: 0.15)
        greenGrid.isHidden = !putting
        beads.isHidden = !putting
        flag.isHidden = putting
        if putting { animateBeads() } else { lastBeadTime = nil }
        if putting, let hole {
            // Putting, the way the golf games frame it: the camera stays put so you watch the ball
            // roll. Behind: low over the ball so the grid's perspective shows the slope, the cup
            // in the upper part of the frame. Overhead: straight down the line, cup at the top.
            let cup = grounded(simd_float3(Float(hole.cup.x), 0, Float(hole.cup.z)))
            let flat = simd_float3(cup.x - base.x, 0, cup.z - base.z)
            let distance = max(1, simd_length(flat))
            let line = flat / distance
            let lineRight = simd_float3(-line.z, 0, line.x)
            switch puttView {
            case .behind:
                var position = base - line * (5 + distance * 0.45) + lineRight * 1.4
                position.y = max(base.y, elevation(position.x, position.z)) + 2.4 + distance * 0.12
                var target = base + line * (distance * 0.55)
                target.y = elevation(target.x, target.z) + 0.2
                clearView(from: &position, to: cup + simd_float3(0, 0.3, 0))
                camera.simdPosition = position
                camera.simdLook(at: target)
            case .overhead:
                var middle = base + line * (distance / 2)
                middle.y = max(base.y, cup.y)
                camera.simdPosition = middle + simd_float3(0, max(9, distance * 1.1), 0) - line * 0.01
                camera.simdLook(at: middle, up: line, localFront: simd_float3(0, 0, -1))
            }
        } else {
            // Full shots get the high, wide chase view; short shots are framed tighter. Once the
            // ball is down the camera closes in behind it, so the lie and the way ahead are what
            // you see. It rides on the ground under it and looks at the grass ahead, so an
            // elevated tee looks down the hole and a raised green is seen climbing up to the flag.
            let closeness = Float(min(1, max(0, (shot?.total ?? focusYards) / 60)))
            let settled = shot.map { Float(min(1, max(0, (elapsed - $0.flight.carryTime - 0.2) / 1.2))) } ?? 0
            let smooth = settled * settled * (3 - 2 * settled)
            let back = simd_mix(12 + 26 * closeness, 11, smooth)
            let up = simd_mix(7 + 15 * closeness, 5.5, smooth)
            let side = simd_mix(4 + 8 * closeness, 4, smooth)
            let chase = along * (0.86 + 0.14 * smooth)
            var position = base + forward * (chase - back) + right * (lateral * 0.65 + side) + simd_float3(0, up + aboveGround * 0.55, 0)
            position.y = max(position.y, elevation(position.x, position.z) + 3)
            let ahead = simd_mix(max(min(40, Float(shot?.total ?? focusYards) + 10), along + 12), along + 9, smooth)
            var target = base + forward * ahead + right * lateral
            target.y = elevation(target.x, target.z) + aboveGround * 0.75
            clearView(from: &position, to: ballWorld)
            camera.simdPosition = position
            camera.simdLook(at: target)
        }
        // The ball keeps the same size on screen wherever the camera is, like a marker: it grows
        // with distance so it never shrinks to a speck in the air or on landing, and shrinks toward
        // true scale as the camera closes in on the green. The flag and cup ring are arcade-sized
        // so they can be seen from the tee and shrink up close, so nothing towers over the green.
        let ballScale = Self.adaptiveScale(distance: simd_distance(camera.simdPosition, ballWorld), full: 45, floor: 0.4, ceiling: 3)
        ball.simdScale = simd_float3(repeating: ballScale)
        ball.simdPosition = ballWorld + simd_float3(0, 0.6 * ballScale, 0)
        shadow.simdScale = simd_float3(repeating: ballScale)
        if let hole {
            let cup = grounded(simd_float3(Float(hole.cup.x), 0, Float(hole.cup.z)))
            flag.simdScale = simd_float3(repeating: Self.adaptiveScale(distance: simd_distance(camera.simdPosition, cup), full: 90, floor: 0.3))
            cupMarker.simdScale = simd_float3(repeating: Self.adaptiveScale(distance: simd_distance(camera.simdPosition, cup), full: 40, floor: 0.45))
        }
        for dot in trail.childNodes + self.preview.childNodes { dot.simdScale = simd_float3(repeating: min(1, ballScale)) }
        let burst = min(max(elapsed / 0.23, 0), 1)
        impact.isHidden = shot == nil || elapsed > 0.23
        impact.simdPosition = base + simd_float3(0, 0.6, 0)
        impact.opacity = CGFloat((1 - burst) * 0.7)
        impact.scale = SCNVector3(1 + burst * 3, 1 + burst * 3, 1 + burst * 3)
        updatePreview(shot == nil ? preview : nil)
        golferMount.simdPosition = base
        golferMount.eulerAngles.y = Float(-facing * .pi / 180)
        if let shot {
            for (i, dot) in trail.childNodes.enumerated() { dot.isHidden = Double(i) / 59 * shot.duration > elapsed }
        }
        SCNTransaction.commit()
    }

    private func updatePreview(_ previewShot: RangeShot?) {
        preview.isHidden = previewShot == nil
        landingRing.isHidden = previewShot == nil
        guard let previewShot, previewShot != lastPreview else { return }
        lastPreview = previewShot
        let isPutt = previewShot.club == .putter && previewShot.lie == .green
        let dots = preview.childNodes
        for (index, dot) in dots.enumerated() {
            // A putt has no carry: its line-up is the roll itself.
            let span = isPutt ? previewShot.duration : previewShot.flight.carryTime
            let point = previewShot.position(at: span * Double(index) / Double(dots.count - 1))
            dot.simdPosition = world(point, origin: previewShot.origin, heading: previewShot.heading) + simd_float3(0, isPutt ? 0.15 : 0.45, 0)
        }
        let rest = previewShot.restingPoint
        landingRing.simdPosition = grounded(simd_float3(Float(rest.x), 0, Float(rest.z)), up: 0.12)
        // A putt's target is the cup itself; a small ring reads better on the green than the landing ring.
        landingRing.simdScale = simd_float3(repeating: isPutt ? 0.25 : 1)
    }

    /// Scale that keeps a prop the same size on screen: 1 at `full` yards from the camera and in
    /// proportion to distance either side of that, clamped between `floor` (close up) and `ceiling`.
    nonisolated static func adaptiveScale(distance: Float, full: Float, floor: Float, ceiling: Float = 1) -> Float {
        min(ceiling, max(floor, distance / full))
    }

    // MARK: Ground

    /// Height of the grass at a course position; the range is flat.
    private func elevation(_ x: Float, _ z: Float) -> Float {
        guard let hole else { return 0 }
        return Float(hole.elevation(at: CoursePoint(x: Double(x), z: Double(z))))
    }

    /// The same x/z, resting on the grass (plus `up`).
    private func grounded(_ point: simd_float3, up: Float = 0) -> simd_float3 {
        simd_float3(point.x, elevation(point.x, point.z) + up, point.z)
    }

    /// Lifts the camera until no hill between it and `target` blocks the view, the way a game
    /// camera pops up over a rise rather than looking through it.
    private func clearView(from position: inout simd_float3, to target: simd_float3) {
        var lift: Float = 0
        for step in 1...12 {
            let t = Float(step) / 13
            let sample = position + (target - position) * t
            // Clearance over the ground tapers to nothing at the target, which sits on the grass.
            let shortfall = elevation(sample.x, sample.z) + 1.5 * (1 - t) - sample.y
            // Raising the camera by L raises the sight line at fraction t by L·(1 − t).
            if shortfall > 0 { lift = max(lift, shortfall / (1 - t)) }
        }
        position.y += lift
    }

    /// The grid's beads drift downhill, faster on steeper ground, so the break is visible at a
    /// glance (PGA TOUR 2K and Mario Golf both do this). Beads that leave the green or have run
    /// for a while start again somewhere else on it.
    private func animateBeads() {
        guard let hole else { return }
        let now = CACurrentMediaTime()
        let dt = min(0.05, now - (lastBeadTime ?? now))
        lastBeadTime = now
        for (index, bead) in beads.childNodes.enumerated() {
            beadAges[index] += dt
            let point = CoursePoint(x: Double(bead.simdPosition.x), z: Double(bead.simdPosition.z))
            let slope = hole.terrain.slope(at: point)
            let speed = 90.0 // yards per second per unit of slope: a 2 % slope drifts 1.8 yd/s
            let next = CoursePoint(x: point.x - slope.x * speed * dt, z: point.z - slope.z * speed * dt)
            if beadAges[index] > 5 || next.distance(to: hole.greenCenter) > hole.greenRadius - 0.5 {
                beadAges[index] = Double.random(in: 0..<1.5)
                let angle = Double.random(in: 0..<(2 * .pi))
                let radius = (hole.greenRadius - 1) * Double.random(in: 0..<1).squareRoot()
                let spawn = CoursePoint(x: hole.greenCenter.x + cos(angle) * radius, z: hole.greenCenter.z + sin(angle) * radius)
                bead.simdPosition = grounded(simd_float3(Float(spawn.x), 0, Float(spawn.z)), up: 0.12)
            } else {
                bead.simdPosition = grounded(simd_float3(Float(next.x), 0, Float(next.z)), up: 0.12)
            }
        }
    }

    /// Unit vector for a compass heading (0 = downrange, −z).
    private static func forward(_ heading: Double) -> simd_float3 {
        let radians = Float(heading * .pi / 180)
        return simd_float3(sin(radians), 0, -cos(radians))
    }

    private func world(_ point: FlightPoint, origin: CoursePoint, heading: Double) -> simd_float3 {
        let course = RangeShot.coursePoint(point, origin: origin, heading: heading)
        return simd_float3(Float(course.x), Float(point.heightYards), Float(course.z))
    }

    private func buildRange() {
        for i in 0..<16 {
            let strip = SCNBox(width: 75 + CGFloat(i) * 1.4, height: 0.15, length: 16, chamferRadius: 0)
            let green = UIColor(red: 0.30, green: i.isMultiple(of: 2) ? 0.61 : 0.57, blue: 0.37, alpha: 1)
            add(strip, color: green, at: SCNVector3(0, -0.4, -Float(i * 16)))
        }
        for target in RangeTarget.all {
            let colors: [UIColor] = [.systemOrange, .systemTeal, .systemYellow]
            for (scale, color) in [(1.0, colors[target.id]), (0.6, UIColor.white), (0.25, colors[target.id])] {
                let disc = SCNCylinder(radius: target.radius * scale, height: 0.10)
                disc.radialSegmentCount = 64
                add(disc, color: color, at: SCNVector3(target.x, 0.04 + (1 - scale) * 0.3, -target.distance))
            }
            add(SCNCylinder(radius: 0.13, height: 8), color: .white, at: SCNVector3(target.x, 4, -target.distance))
            add(SCNBox(width: 4, height: 2.2, length: 0.12, chamferRadius: 0.1), color: colors[target.id], at: SCNVector3(target.x + 2, 7.1, -target.distance))
            label("\(Int(target.distance)) YD", at: SCNVector3(target.x - 5, 11, -target.distance))
        }
        for i in 0..<40 {
            let side: Float = i.isMultiple(of: 2) ? -1 : 1
            let x = side * Float(47 + (i * 13 % 37))
            let z = -Float(i * 7 + 12)
            tree(at: simd_float3(x, 0, z), height: Float(8 + i % 8), shade: i % 3)
        }
        add(SCNBox(width: 9, height: 0.25, length: 7, chamferRadius: 0.4), color: UIColor(red: 0.09, green: 0.29, blue: 0.22, alpha: 1), at: SCNVector3(0, -0.1, 1))
    }

    /// The hole as one sculpted mesh over its terrain, coloured by what grows where: fairway
    /// stripes, fringe and green, sand, tee. Then the cup, flag, slope grid, tee markers and trees,
    /// each standing on the grass at its own height.
    private func buildCourse(_ hole: Hole) {
        scene.rootNode.addChildNode(Self.terrainMesh(for: hole))
        let cupHeight = Float(hole.elevation(at: hole.cup))
        add(SCNCylinder(radius: 0.55, height: 0.3), color: UIColor(white: 0.08, alpha: 1), at: SCNVector3(Float(hole.cup.x), cupHeight + 0.04, Float(hole.cup.z)))
        // The flag stands on the cup and scales about its foot.
        flag.position = SCNVector3(Float(hole.cup.x), cupHeight, Float(hole.cup.z))
        let stick = SCNCylinder(radius: 0.13, height: 9)
        stick.firstMaterial?.diffuse.contents = UIColor.white
        let stickNode = SCNNode(geometry: stick)
        stickNode.position = SCNVector3(0, 4.5, 0)
        flag.addChildNode(stickNode)
        let cloth = SCNBox(width: 4, height: 2.2, length: 0.12, chamferRadius: 0.1)
        cloth.firstMaterial?.diffuse.contents = UIColor.systemYellow
        let clothNode = SCNNode(geometry: cloth)
        clothNode.position = SCNVector3(2, 7.6, 0)
        flag.addChildNode(clothNode)
        scene.rootNode.addChildNode(flag)
        // Slope grid, shown while putting: lines every two yards laid over the green's contours,
        // tinted by height against the cup (blue above it, red below, as Wii Sports colours it).
        greenGrid.geometry = Self.slopeGrid(for: hole)
        greenGrid.isHidden = true
        scene.rootNode.addChildNode(greenGrid)
        for _ in 0..<56 {
            let bead = SCNNode(geometry: SCNSphere(radius: 0.06))
            bead.geometry?.firstMaterial?.diffuse.contents = UIColor.white
            bead.geometry?.firstMaterial?.emission.contents = UIColor(white: 0.9, alpha: 1)
            bead.simdPosition = simd_float3(Float(hole.greenCenter.x), 0, Float(hole.greenCenter.z))
            beads.addChildNode(bead)
        }
        beadAges = Array(repeating: 6, count: beads.childNodes.count) // all respawn on the first frame
        beads.isHidden = true
        scene.rootNode.addChildNode(beads)
        cupMarker.geometry = SCNTorus(ringRadius: 2.2, pipeRadius: 0.12)
        cupMarker.geometry?.firstMaterial?.diffuse.contents = UIColor.white.withAlphaComponent(0.8)
        cupMarker.position = SCNVector3(Float(hole.cup.x), cupHeight + 0.08, Float(hole.cup.z))
        scene.rootNode.addChildNode(cupMarker)
        let teeHeight = Float(hole.elevation(at: hole.tee))
        add(SCNBox(width: 9, height: 0.25, length: 7, chamferRadius: 0.4), color: UIColor(red: 0.09, green: 0.29, blue: 0.22, alpha: 1), at: SCNVector3(Float(hole.tee.x), teeHeight + 0.02, Float(hole.tee.z) + 1))
        for side in [-1.0, 1.0] {
            add(SCNSphere(radius: 0.4), color: .white, at: SCNVector3(Float(hole.tee.x + side * 3.5), teeHeight + 0.4, Float(hole.tee.z)))
        }
        // Trees along both sides, pushed out past the fairway wherever the centreline bends.
        for i in 0..<70 {
            let side = i.isMultiple(of: 2) ? -1.0 : 1.0
            let along = Double(i) * 6 + 10
            guard let (point, direction) = Self.pointAlong(hole.centerline, distance: along) else { continue }
            let offset = hole.fairwayHalfWidth + 18 + Double((i * 13) % 23)
            let x = point.x + direction.z * side * offset
            let z = point.z - direction.x * side * offset
            tree(at: grounded(simd_float3(Float(x), 0, Float(z))), height: Float(8 + i % 8), shade: i % 3)
        }
    }

    private func tree(at foot: simd_float3, height: Float, shade: Int) {
        add(SCNCylinder(radius: 0.65, height: 4), color: .brown, at: SCNVector3(foot.x, foot.y + 2, foot.z))
        let cone = SCNCone(topRadius: 0, bottomRadius: 4.5, height: CGFloat(height))
        cone.radialSegmentCount = 7
        add(cone, color: UIColor(red: 0.10, green: 0.31 + Double(shade) * 0.05, blue: 0.25, alpha: 1), at: SCNVector3(foot.x, foot.y + height / 2 + 3, foot.z))
    }

    /// One vertex-coloured mesh of the hole's ground, 1.5 yd between vertices. Surfaces blend
    /// over a yard at their edges so the fairway and green read as mown shapes, not steps.
    nonisolated static func terrainMesh(for hole: Hole) -> SCNNode {
        let spacing = 1.5
        let minX = -100.0, maxX = 100.0, minZ = -450.0, maxZ = 40.0
        let columns = Int((maxX - minX) / spacing) + 1
        let rows = Int((maxZ - minZ) / spacing) + 1
        var vertices: [SCNVector3] = []
        var normals: [SCNVector3] = []
        var colors: [Float] = []
        vertices.reserveCapacity(columns * rows)
        normals.reserveCapacity(columns * rows)
        colors.reserveCapacity(columns * rows * 4)
        for row in 0..<rows {
            for column in 0..<columns {
                let point = CoursePoint(x: minX + Double(column) * spacing, z: minZ + Double(row) * spacing)
                let slope = hole.terrain.slope(at: point)
                vertices.append(SCNVector3(Float(point.x), Float(hole.elevation(at: point)), Float(point.z)))
                normals.append(SCNVector3(Float(-slope.x), 1, Float(-slope.z)))
                let color = linear(surfaceColor(at: point, on: hole))
                colors += [color.x, color.y, color.z, 1]
            }
        }
        var indices: [Int32] = []
        indices.reserveCapacity((columns - 1) * (rows - 1) * 6)
        for row in 0..<(rows - 1) {
            for column in 0..<(columns - 1) {
                let a = Int32(row * columns + column), b = a + 1
                let c = a + Int32(columns), d = c + 1
                indices += [a, c, b, b, c, d]
            }
        }
        let colorSource = colors.withUnsafeBufferPointer { buffer in
            SCNGeometrySource(data: Data(buffer: buffer), semantic: .color, vectorCount: vertices.count, usesFloatComponents: true,
                              componentsPerVector: 4, bytesPerComponent: MemoryLayout<Float>.size, dataOffset: 0, dataStride: MemoryLayout<Float>.size * 4)
        }
        let geometry = SCNGeometry(sources: [SCNGeometrySource(vertices: vertices), SCNGeometrySource(normals: normals), colorSource],
                                   elements: [SCNGeometryElement(indices: indices, primitiveType: .triangles)])
        geometry.firstMaterial?.diffuse.contents = UIColor.white
        geometry.firstMaterial?.lightingModel = .lambert
        return SCNNode(geometry: geometry)
    }

    /// Vertex colours are read as linear light where `UIColor`s are sRGB; this keeps the two palettes matching.
    nonisolated private static func linear(_ srgb: simd_float3) -> simd_float3 {
        simd_float3(pow(srgb.x, 2.2), pow(srgb.y, 2.2), pow(srgb.z, 2.2))
    }

    /// What grows at a point, as an RGB colour, feathered a yard either side of each edge.
    nonisolated private static func surfaceColor(at point: CoursePoint, on hole: Hole) -> simd_float3 {
        let rough = simd_float3(0.22, 0.46, 0.31)
        let fairway = simd_float3(0.32, 0.62, 0.38)
        let fairwayStripe = simd_float3(0.30, 0.58, 0.36)
        let fringe = simd_float3(0.40, 0.70, 0.42)
        let green = simd_float3(0.48, 0.78, 0.46)
        let sand = simd_float3(0.90, 0.84, 0.62)
        let teeBox = simd_float3(0.28, 0.56, 0.36)
        func edge(_ distanceOutside: Double) -> Float { Float(min(1, max(0, 0.5 - distanceOutside / 2))) }
        var fairwayDistance = Double.infinity
        for index in 1..<hole.centerline.count {
            fairwayDistance = min(fairwayDistance, point.distance(toSegment: hole.centerline[index - 1], hole.centerline[index]) - hole.fairwayHalfWidth)
        }
        let stripe = Int((point.z / 9).rounded(.down)).isMultiple(of: 2) ? fairway : fairwayStripe
        var color = simd_mix(rough, stripe, simd_float3(repeating: edge(fairwayDistance)))
        let greenDistance = point.distance(to: hole.greenCenter) - hole.greenRadius
        color = simd_mix(color, fringe, simd_float3(repeating: edge(greenDistance - 4)))
        color = simd_mix(color, green, simd_float3(repeating: edge(greenDistance)))
        for bunker in hole.bunkers {
            let distance = point.distance(to: bunker.center) - bunker.radius
            color = simd_mix(color, fringe, simd_float3(repeating: edge(distance - 1)))
            color = simd_mix(color, sand, simd_float3(repeating: edge(distance)))
        }
        color = simd_mix(color, teeBox, simd_float3(repeating: edge(point.distance(to: hole.tee) - 5)))
        return color
    }

    /// The putting grid as thin ribbons draped over the green, one geometry. Colour says how each
    /// spot sits against the cup: white level, blue above (a putt from there runs downhill), red
    /// below (uphill), stronger the bigger the difference.
    nonisolated static func slopeGrid(for hole: Hole) -> SCNGeometry {
        let cupHeight = hole.elevation(at: hole.cup)
        var vertices: [SCNVector3] = []
        var normals: [SCNVector3] = []
        var colors: [Float] = []
        var indices: [Int32] = []
        let half: Float = 0.05
        func lay(_ points: [CoursePoint]) {
            guard points.count >= 2 else { return }
            for (index, point) in points.enumerated() {
                let previous = points[max(0, index - 1)], next = points[min(points.count - 1, index + 1)]
                let direction = simd_normalize(simd_float2(Float(next.x - previous.x), Float(next.z - previous.z)))
                let side = simd_float2(-direction.y, direction.x) * half
                let height = Float(hole.elevation(at: point)) + 0.06
                let difference = hole.elevation(at: point) - cupHeight
                let strength = Float(min(1, abs(difference) / 0.3))
                let tint = difference > 0 ? simd_float3(0.35, 0.6, 1) : simd_float3(1, 0.42, 0.35)
                let color = linear(simd_mix(simd_float3(1, 1, 1), tint, simd_float3(repeating: strength)))
                for offset in [-side, side] {
                    vertices.append(SCNVector3(Float(point.x) + offset.x, height, Float(point.z) + offset.y))
                    normals.append(SCNVector3(0, 1, 0))
                    colors += [color.x, color.y, color.z, 0.85]
                }
                if index > 0 {
                    let a = Int32(vertices.count - 4)
                    indices += [a, a + 1, a + 2, a + 1, a + 3, a + 2]
                }
            }
        }
        let radius = hole.greenRadius
        var offset = -radius + 2
        while offset < radius {
            let chord = (radius * radius - offset * offset).squareRoot()
            let steps = max(2, Int(chord * 2))
            lay((0...steps).map { CoursePoint(x: hole.greenCenter.x - chord + chord * 2 * Double($0) / Double(steps), z: hole.greenCenter.z + offset) })
            lay((0...steps).map { CoursePoint(x: hole.greenCenter.x + offset, z: hole.greenCenter.z - chord + chord * 2 * Double($0) / Double(steps)) })
            offset += 2
        }
        let colorSource = colors.withUnsafeBufferPointer { buffer in
            SCNGeometrySource(data: Data(buffer: buffer), semantic: .color, vectorCount: vertices.count, usesFloatComponents: true,
                              componentsPerVector: 4, bytesPerComponent: MemoryLayout<Float>.size, dataOffset: 0, dataStride: MemoryLayout<Float>.size * 4)
        }
        let geometry = SCNGeometry(sources: [SCNGeometrySource(vertices: vertices), SCNGeometrySource(normals: normals), colorSource],
                                   elements: [SCNGeometryElement(indices: indices, primitiveType: .triangles)])
        geometry.firstMaterial?.diffuse.contents = UIColor.white
        geometry.firstMaterial?.emission.contents = UIColor(white: 0.35, alpha: 1)
        geometry.firstMaterial?.isDoubleSided = true
        geometry.firstMaterial?.readsFromDepthBuffer = true
        return geometry
    }

    /// Point and unit direction `distance` yards along a polyline.
    private static func pointAlong(_ line: [CoursePoint], distance: Double) -> (CoursePoint, CoursePoint)? {
        var remaining = distance
        for index in 1..<line.count {
            let a = line[index - 1], b = line[index]
            let length = a.distance(to: b)
            if remaining <= length {
                let t = remaining / length
                return (CoursePoint(x: a.x + (b.x - a.x) * t, z: a.z + (b.z - a.z) * t), CoursePoint(x: (b.x - a.x) / length, z: (b.z - a.z) / length))
            }
            remaining -= length
        }
        return nil
    }

    @discardableResult
    private func add(_ geometry: SCNGeometry, color: UIColor, at position: SCNVector3) -> SCNNode {
        geometry.firstMaterial?.diffuse.contents = color
        let node = SCNNode(geometry: geometry)
        node.position = position
        scene.rootNode.addChildNode(node)
        return node
    }

    private func label(_ text: String, at position: SCNVector3) {
        let geometry = SCNText(string: text, extrusionDepth: 0.01)
        geometry.font = .systemFont(ofSize: 2.2, weight: .heavy)
        geometry.flatness = 0.3
        let node = add(geometry, color: .white, at: position)
        let facing = SCNBillboardConstraint()
        facing.freeAxes = .Y
        node.constraints = [facing]
    }
}

/// A Mii-style golfer beside the tee. The body is rigid; the arms have fixed lengths and travel
/// along one clean swing arc around the chest, driven by a single angle. Elbows come from two-bone
/// IK, so the arms bend but never stretch. The shoulders turn with the arc for a fuller motion.
@MainActor
final class Golfer {
    let node = SCNNode()
    var handedness: Handedness = .right {
        didSet { if handedness != oldValue { applyHandedness() } }
    }

    private let upperBody = SCNNode()
    private let bones: [SCNNode]          // left upper, left fore, right upper, right fore
    private let hands: [SCNNode]
    private let club = SCNNode()
    private var angle = 0.0                // displayed arc, degrees
    private var launchAngle = 0.0

    // Upper-body frame (origin at the hips, 2.4 above the feet): +x toward the ball,
    // +z the golfer's right (toward the range camera), +y up.
    private let hipHeight: Float = 2.4
    private let pivot = simd_float3(0.55, 1.8, 0)            // chest, where the arc is centred
    private let shoulders = [simd_float3(0.7, 2.0, -0.75), simd_float3(0.7, 2.0, 0.75)] // left, right
    private let addressHands = simd_float3(1.9, -0.3, 0.1)
    private let upperArm: Float = 1.35
    private let forearm: Float = 1.35
    private let clubLength: Float = 2.7
    private let fullBackswing = 150.0
    private let finish = -150.0

    init() {
        let shirt = UIColor(red: 0.95, green: 0.45, blue: 0.3, alpha: 1)
        let trousers = UIColor(red: 0.16, green: 0.2, blue: 0.3, alpha: 1)
        let skin = UIColor(red: 0.93, green: 0.78, blue: 0.62, alpha: 1)
        let cream = UIColor(red: 0.96, green: 0.96, blue: 0.86, alpha: 1)
        func part(_ geometry: SCNGeometry, _ color: UIColor, at position: simd_float3, parent: SCNNode, tilt: Float = 0) {
            geometry.firstMaterial?.diffuse.contents = color
            geometry.firstMaterial?.isDoubleSided = true
            let part = SCNNode(geometry: geometry)
            part.simdPosition = position
            part.eulerAngles.z = tilt
            parent.addChildNode(part)
        }
        // Stance is along z; the golfer leans a little toward the ball (+x).
        part(SCNCapsule(capRadius: 0.26, height: 2.5), trousers, at: simd_float3(0.05, 1.25, -0.6), parent: node, tilt: -0.05)
        part(SCNCapsule(capRadius: 0.26, height: 2.5), trousers, at: simd_float3(0.05, 1.25, 0.6), parent: node, tilt: -0.05)
        upperBody.simdPosition = simd_float3(0, hipHeight, 0)
        node.addChildNode(upperBody)
        part(SCNCapsule(capRadius: 0.55, height: 2.0), shirt, at: simd_float3(0.3, 0.95, 0), parent: upperBody, tilt: -0.32)
        let shoulderBar = SCNCapsule(capRadius: 0.32, height: 2.1)
        shoulderBar.firstMaterial?.diffuse.contents = shirt
        let bar = SCNNode(geometry: shoulderBar)
        bar.simdPosition = simd_float3(0.7, 2.0, 0)
        bar.eulerAngles.x = .pi / 2 // lies along z, joining the two shoulders
        upperBody.addChildNode(bar)
        part(SCNSphere(radius: 0.5), skin, at: simd_float3(0.95, 2.75, 0), parent: upperBody)
        part(SCNCylinder(radius: 0.58, height: 0.12), cream, at: simd_float3(0.95, 3.1, 0), parent: upperBody)

        var bones: [SCNNode] = []
        for index in 0..<4 {
            let bone = SCNNode(geometry: SCNCylinder(radius: 0.16, height: 1))
            bone.geometry?.firstMaterial?.diffuse.contents = index % 2 == 0 ? shirt : skin
            bone.geometry?.firstMaterial?.isDoubleSided = true
            bone.pivot = SCNMatrix4MakeTranslation(0, -0.5, 0)
            upperBody.addChildNode(bone)
            bones.append(bone)
        }
        self.bones = bones
        var hands: [SCNNode] = []
        for _ in 0..<2 {
            let hand = SCNNode(geometry: SCNSphere(radius: 0.21))
            hand.geometry?.firstMaterial?.diffuse.contents = skin
            upperBody.addChildNode(hand)
            hands.append(hand)
        }
        self.hands = hands
        let shaft = SCNNode(geometry: SCNCylinder(radius: 0.06, height: 1))
        shaft.geometry?.firstMaterial?.diffuse.contents = UIColor.lightGray
        shaft.pivot = SCNMatrix4MakeTranslation(0, -0.5, 0)
        club.addChildNode(shaft)
        let head = SCNNode(geometry: SCNBox(width: 0.55, height: 0.35, length: 0.95, chamferRadius: 0.1))
        head.geometry?.firstMaterial?.diffuse.contents = UIColor.darkGray
        head.position = SCNVector3(0.1, 1, 0.15)
        club.addChildNode(head)
        upperBody.addChildNode(club)
        applyHandedness()
        pose(0)
    }

    /// Live tracking: ease toward the player's arc so the motion stays clean.
    func follow(_ target: Double) {
        let clamped = min(max(target, finish), fullBackswing)
        angle += (clamped - angle) * 0.35
        pose(angle)
    }

    func launch(replay: Bool) {
        launchAngle = replay ? fullBackswing : max(angle, 60)
    }

    /// Canned downswing (0.3 s) into a held finish, then back to address for the next ball.
    func animate(elapsed: Double) {
        let t: Double
        if elapsed < 0.3 {
            let u = elapsed / 0.3
            t = launchAngle + (finish - launchAngle) * (u * u) // accelerates through the ball
        } else if elapsed < 3 {
            t = finish
        } else {
            let u = min(1, (elapsed - 3) / 1.2)
            t = finish + (0 - finish) * (1 - cos(u * .pi)) / 2
        }
        angle = t
        pose(t)
    }

    /// The rig is built 3.6 yd tall; shown at human height so the ball and the green stay in proportion.
    private let stature: Float = 0.62

    private func applyHandedness() {
        let mirror: Float = handedness == .right ? 1 : -1
        node.simdScale = simd_float3(mirror * stature, stature, stature)
        node.simdPosition = simd_float3(-3.2 * mirror * stature, 0, 0)
    }

    /// Hands move on a circle around the chest: down-forward at address, out to the right at 90°,
    /// up and behind the trail shoulder at the top; negative angles mirror through to the finish.
    private func pose(_ degrees: Double) {
        let radians = Float(degrees * .pi / 180)
        let toAddress = addressHands - pivot
        let radius = simd_length(toAddress)
        let d0 = toAddress / radius
        var d1 = simd_float3(-0.25, 0.2, 1)
        d1 = simd_normalize(d1 - simd_dot(d1, d0) * d0)
        let hands = pivot + (cos(radians) * d0 + sin(radians) * d1) * radius
        // The shoulders turn with the arms, about a third of the arc.
        upperBody.eulerAngles.y = -radians * 0.35

        for side in 0..<2 {
            let shoulder = shoulders[side]
            let hand = hands + simd_float3(side == 0 ? 0.12 : -0.12, side == 0 ? -0.08 : 0.08, 0)
            var reach = hand - shoulder
            let distance = min(simd_length(reach), upperArm + forearm - 0.05)
            reach = simd_normalize(reach) * distance
            let wrist = shoulder + reach
            // Two-bone IK: elbows bend out and back, never past straight.
            let axis = reach / distance
            let a = (upperArm * upperArm - forearm * forearm + distance * distance) / (2 * distance)
            let height = sqrt(max(0, upperArm * upperArm - a * a))
            var bend = simd_float3(-0.7, -0.3, side == 0 ? -1 : 1)
            bend = simd_normalize(bend - simd_dot(bend, axis) * axis)
            let elbow = shoulder + axis * a + bend * height
            place(bones[side * 2], from: shoulder, to: elbow)
            place(bones[side * 2 + 1], from: elbow, to: wrist)
            self.hands[side].simdPosition = wrist
        }
        // Wrist hinge: the club hangs on the arm line at address and cocks up to 90° along the
        // direction of travel, so it lies over the shoulder at the top and points skyward through.
        let armLine = simd_normalize(hands - pivot)
        let tangent = simd_normalize(-sin(radians) * d0 + cos(radians) * d1) * (degrees < 0 ? -1 : 1)
        let hinge = Float(min(1, abs(degrees) / 100) * .pi / 2)
        let shaft = simd_normalize(cos(hinge) * armLine + sin(hinge) * tangent)
        club.simdPosition = hands
        club.simdScale = simd_float3(1, clubLength, 1)
        club.simdLook(at: hands + shaft * clubLength, up: simd_float3(0, 0, 1), localFront: simd_float3(0, 1, 0))
    }

    private func place(_ bone: SCNNode, from start: simd_float3, to end: simd_float3) {
        bone.simdPosition = start
        bone.simdScale = simd_float3(1, max(0.05, simd_length(end - start)), 1)
        bone.simdLook(at: end, up: simd_float3(0, 0, 1), localFront: simd_float3(0, 1, 0))
    }
}

struct MeadowSceneView: UIViewRepresentable {
    let meadow: MeadowScene
    let shot: RangeShot?
    let elapsed: Double
    let power: Double
    let aim: Double
    var swingAngle: Double = 0
    var handedness: Handedness = .right
    var ball = CoursePoint(x: 0, z: 0)
    var heading = 0.0
    var focusYards = 60.0
    var preview: RangeShot? = nil
    var putting = false
    var puttView: MeadowScene.PuttView = .behind

    func makeUIView(context: Context) -> SCNView {
        let view = SCNView()
        view.scene = meadow.scene
        view.pointOfView = meadow.camera
        view.antialiasingMode = .multisampling4X
        view.preferredFramesPerSecond = 60
        view.isUserInteractionEnabled = false
        return view
    }

    func updateUIView(_ view: SCNView, context: Context) {
        if view.scene !== meadow.scene {
            view.scene = meadow.scene
            view.pointOfView = meadow.camera
        }
        meadow.update(shot: shot, elapsed: elapsed, power: power, aim: aim, swingAngle: swingAngle, handedness: handedness, ball: ball, heading: heading, focusYards: focusYards, preview: preview, putting: putting, puttView: puttView)
    }
}
