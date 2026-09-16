import SwiftUI

/// Wii-style overhead map: the hole (or range targets), the ball, the predicted flight for the
/// current club and load, and the ball in the air while a shot plays.
struct CourseMinimap: View {
    let mode: GameMode
    let ball: CoursePoint
    let preview: RangeShot?
    let activeShot: RangeShot?
    let elapsed: Double

    private var bounds: (minX: Double, maxX: Double, minZ: Double, maxZ: Double) {
        switch mode {
        case .range:
            return (-60, 60, -215, 15)
        case .hole(let hole):
            var xs = hole.centerline.map(\.x) + hole.bunkers.map(\.center.x) + [hole.tee.x, hole.cup.x]
            var zs = hole.centerline.map(\.z) + hole.bunkers.map(\.center.z) + [hole.tee.z, hole.cup.z]
            xs += [hole.greenCenter.x - hole.greenRadius, hole.greenCenter.x + hole.greenRadius]
            zs += [hole.greenCenter.z - hole.greenRadius]
            return (xs.min()! - 35, xs.max()! + 35, zs.min()! - 20, zs.max()! + 12)
        }
    }

    var body: some View {
        Canvas { context, size in
            let b = bounds
            let scale = min(size.width / (b.maxX - b.minX), size.height / (b.maxZ - b.minZ))
            let offset = CGPoint(x: (size.width - (b.maxX - b.minX) * scale) / 2, y: (size.height - (b.maxZ - b.minZ) * scale) / 2)
            func map(_ point: CoursePoint) -> CGPoint {
                CGPoint(x: offset.x + (point.x - b.minX) * scale, y: offset.y + (point.z - b.minZ) * scale)
            }
            func circle(_ center: CoursePoint, _ radius: Double, _ color: Color) {
                let c = map(center)
                let r = radius * scale
                context.fill(Path(ellipseIn: CGRect(x: c.x - r, y: c.y - r, width: r * 2, height: r * 2)), with: .color(color))
            }

            context.fill(Path(roundedRect: CGRect(origin: .zero, size: size), cornerRadius: 10), with: .color(Color(red: 0.12, green: 0.3, blue: 0.22)))
            switch mode {
            case .range:
                var strip = Path()
                strip.addRect(CGRect(x: map(CoursePoint(x: -40, z: -210)).x, y: map(CoursePoint(x: 0, z: -210)).y, width: 80 * scale, height: 215 * scale))
                context.fill(strip, with: .color(Color(red: 0.3, green: 0.58, blue: 0.36)))
                for target in RangeTarget.all {
                    let color: Color = target.id == 0 ? .orange : target.id == 1 ? .mint : .yellow
                    circle(CoursePoint(x: target.x, z: -target.distance), target.radius, color)
                    circle(CoursePoint(x: target.x, z: -target.distance), target.radius * 0.5, .white)
                }
            case .hole(let hole):
                var fairway = Path()
                fairway.move(to: map(hole.centerline[0]))
                for point in hole.centerline.dropFirst() { fairway.addLine(to: map(point)) }
                context.stroke(fairway, with: .color(Color(red: 0.3, green: 0.58, blue: 0.36)), style: StrokeStyle(lineWidth: hole.fairwayHalfWidth * 2 * scale, lineCap: .round, lineJoin: .round))
                circle(hole.greenCenter, hole.greenRadius + 4, Color(red: 0.4, green: 0.7, blue: 0.42))
                circle(hole.greenCenter, hole.greenRadius, Color(red: 0.48, green: 0.78, blue: 0.46))
                for bunker in hole.bunkers { circle(bunker.center, bunker.radius, Color(red: 0.9, green: 0.84, blue: 0.62)) }
                circle(hole.tee, 3, Color(red: 0.09, green: 0.29, blue: 0.22))
                let cup = map(hole.cup)
                var flag = Path()
                flag.move(to: cup)
                flag.addLine(to: CGPoint(x: cup.x, y: cup.y - 9))
                flag.addLine(to: CGPoint(x: cup.x + 6, y: cup.y - 7))
                flag.addLine(to: CGPoint(x: cup.x, y: cup.y - 5))
                context.fill(flag, with: .color(.yellow))
                context.stroke(flag, with: .color(.white), lineWidth: 1)
            }

            // The line-up: predicted flight from the ball to where it will finish.
            if let preview, activeShot == nil {
                var path = Path()
                path.move(to: map(preview.coursePoint(at: 0)))
                let steps = 24
                for index in 1...steps { path.addLine(to: map(preview.coursePoint(at: preview.duration * Double(index) / Double(steps)))) }
                context.stroke(path, with: .color(.white.opacity(0.9)), style: StrokeStyle(lineWidth: 1.5, dash: [3, 3]))
                let landing = map(preview.restingPoint)
                context.stroke(Path(ellipseIn: CGRect(x: landing.x - 4, y: landing.y - 4, width: 8, height: 8)), with: .color(.yellow), lineWidth: 1.5)
            }

            let position = activeShot.map { $0.coursePoint(at: elapsed) } ?? ball
            let dot = map(position)
            context.fill(Path(ellipseIn: CGRect(x: dot.x - 3, y: dot.y - 3, width: 6, height: 6)), with: .color(.white))
            context.stroke(Path(ellipseIn: CGRect(x: dot.x - 3, y: dot.y - 3, width: 6, height: 6)), with: .color(.black.opacity(0.6)), lineWidth: 0.8)
        }
        .accessibilityHidden(true)
    }
}
