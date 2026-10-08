import SceneKit
import UIKit

/// Small building blocks for the lobby scene: cached materials and textures, grounded primitives, sign textures, a palm, and the sky.
/// The first version of the world is simple modelled forms; the art kit from Blender can replace the pieces without touching the layout.
@MainActor enum LobbyKit {
    // MARK: palette (the club's colours)
    static let ivory = "EFE3C8", limestone = "DCCBA8", paver = "E3D3B0", teak = "9A642D", navy = "0D1A52", terracotta = "BD542E", leaf = "2E7A26", leaf2 = "4A9A30"
    static let turquoise = "12A2B5", lime = "D4F25A", coral = "F2665A", white = "F6F5F0", gold = "DBA22A", dark = "1B1830", sand = "E8D8B4"

    static func colour(_ hex: String, alpha: CGFloat = 1) -> UIColor {
        let n = UInt32(hex, radix: 16) ?? 0xFFFFFF
        return UIColor(red: CGFloat((n >> 16) & 255) / 255, green: CGFloat((n >> 8) & 255) / 255, blue: CGFloat(n & 255) / 255, alpha: alpha)
    }

    // MARK: materials
    private static var materials: [String: SCNMaterial] = [:]
    /// A matte material in a flat colour (shared by everything that asks for the same one).
    static func mat(_ hex: String, emission: Bool = false, double: Bool = false) -> SCNMaterial {
        let key = "\(hex)|\(emission)|\(double)"
        if let m = materials[key] { return m }
        let m = SCNMaterial()
        m.lightingModel = emission ? .constant : .lambert
        m.diffuse.contents = colour(hex)
        if emission { m.emission.contents = colour(hex) }
        m.isDoubleSided = double
        materials[key] = m
        return m
    }
    /// A tiled texture material (`tile` repeats per metre).
    static func textured(_ name: String, tile: Float, tint: String? = nil) -> SCNMaterial {
        let key = "tex|\(name)|\(tile)|\(tint ?? "")"
        if let m = materials[key] { return m }
        let m = SCNMaterial()
        m.lightingModel = .lambert
        m.diffuse.contents = textures[name] ?? makeTexture(name)
        m.diffuse.wrapS = .repeat; m.diffuse.wrapT = .repeat
        m.diffuse.mipFilter = .linear; m.diffuse.minificationFilter = .linear; m.diffuse.maxAnisotropy = 4
        m.diffuse.contentsTransform = SCNMatrix4MakeScale(tile, tile, 1)
        materials[key] = m
        return m
    }

    // MARK: textures (drawn once; small and tileable)
    private static var textures: [String: UIImage] = [:]
    private static func makeTexture(_ name: String) -> UIImage {
        let size = CGSize(width: 256, height: 256)
        let image = UIGraphicsImageRenderer(size: size).image { ctx in
            let g = ctx.cgContext
            switch name {
            case "pavers":
                colour(paver).setFill(); g.fill(CGRect(origin: .zero, size: size))
                var rng = SystemRandomNumberGenerator()
                for row in 0..<4 {
                    let offset: CGFloat = row % 2 == 0 ? 0 : 32
                    for col in -1..<5 {
                        let shade = CGFloat.random(in: -0.035...0.035, using: &rng)
                        let c = colour(paver)
                        var r: CGFloat = 0, gr: CGFloat = 0, b: CGFloat = 0
                        c.getRed(&r, green: &gr, blue: &b, alpha: nil)
                        UIColor(red: r + shade, green: gr + shade, blue: b + shade, alpha: 1).setFill()
                        g.fill(CGRect(x: CGFloat(col) * 64 + offset + 1.5, y: CGFloat(row) * 64 + 1.5, width: 61, height: 61))
                    }
                }
                colour("BFAE88").setStroke(); g.setLineWidth(1.5)
                for row in 0..<4 { g.move(to: CGPoint(x: 0, y: CGFloat(row) * 64)); g.addLine(to: CGPoint(x: 256, y: CGFloat(row) * 64)) }
                g.strokePath()
            case "planks":
                colour(teak).setFill(); g.fill(CGRect(origin: .zero, size: size))
                var rng = SystemRandomNumberGenerator()
                for i in 0..<8 {
                    let shade = CGFloat.random(in: -0.05...0.05, using: &rng)
                    UIColor(red: 0.60 + shade, green: 0.39 + shade, blue: 0.18 + shade, alpha: 1).setFill()
                    g.fill(CGRect(x: 0, y: CGFloat(i) * 32 + 1, width: 256, height: 30))
                }
                colour("5B3A1A", alpha: 0.5).setStroke(); g.setLineWidth(1)
                for i in 0..<8 { g.move(to: CGPoint(x: 0, y: CGFloat(i) * 32)); g.addLine(to: CGPoint(x: 256, y: CGFloat(i) * 32)) }
                g.strokePath()
            case "grass":
                colour("3C8A2C").setFill(); g.fill(CGRect(origin: .zero, size: size))
                var rng = SystemRandomNumberGenerator()
                for _ in 0..<420 {
                    let s = CGFloat.random(in: 0...1, using: &rng)
                    UIColor(red: 0.20 + 0.10 * s, green: 0.50 + 0.14 * s, blue: 0.16 + 0.05 * s, alpha: 0.55).setFill()
                    let w = CGFloat.random(in: 6...22, using: &rng)
                    let x = CGFloat.random(in: -10...256, using: &rng), y = CGFloat.random(in: -10...256, using: &rng)
                    for dx in [CGFloat(0), -256, 256] { for dy in [CGFloat(0), -256, 256] { g.fillEllipse(in: CGRect(x: x + dx, y: y + dy, width: w, height: w * 0.7)) } }
                }
            case "hedge":
                colour(leaf).setFill(); g.fill(CGRect(origin: .zero, size: size))
                var rng = SystemRandomNumberGenerator()
                for _ in 0..<300 {
                    let s = CGFloat.random(in: 0...1, using: &rng)
                    UIColor(red: 0.12 + 0.12 * s, green: 0.38 + 0.2 * s, blue: 0.10 + 0.06 * s, alpha: 0.7).setFill()
                    let w = CGFloat.random(in: 8...26, using: &rng)
                    g.fillEllipse(in: CGRect(x: CGFloat.random(in: 0...256, using: &rng), y: CGFloat.random(in: 0...256, using: &rng), width: w, height: w))
                }
            case "ivory":
                colour(ivory).setFill(); g.fill(CGRect(origin: .zero, size: size))
                var rng = SystemRandomNumberGenerator()
                for _ in 0..<500 {
                    UIColor(white: CGFloat.random(in: 0.6...1, using: &rng), alpha: 0.05).setFill()
                    g.fillEllipse(in: CGRect(x: CGFloat.random(in: 0...256, using: &rng), y: CGFloat.random(in: 0...256, using: &rng), width: 14, height: 14))
                }
            default:
                UIColor.white.setFill(); g.fill(CGRect(origin: .zero, size: size))
            }
        }
        textures[name] = image
        return image
    }

    // MARK: sign textures
    /// A sign face drawn to an image: rounded plate, bold text (one or two lines), an optional lock.
    static func signImage(_ text: String, fill: String = navy, ink: String = white, size: CGSize = CGSize(width: 512, height: 160), lock: Bool = false, corner: CGFloat = 36) -> UIImage {
        UIGraphicsImageRenderer(size: size).image { ctx in
            let g = ctx.cgContext
            let plate = UIBezierPath(roundedRect: CGRect(origin: .zero, size: size).insetBy(dx: 3, dy: 3), cornerRadius: corner)
            colour(fill).setFill(); plate.fill()
            UIColor(white: 1, alpha: 0.14).setStroke(); plate.lineWidth = 4; plate.stroke()
            let lines = text.components(separatedBy: "\n")
            // the biggest letters that fit: by height for the number of lines, and by width for the longest line
            let reference = UIFont.systemFont(ofSize: 100, weight: .heavy)
            let widest = lines.map { ($0 as NSString).size(withAttributes: [.font: reference, .kern: 4]).width }.max() ?? 100
            let usable = size.width * (lock ? 0.66 : 0.86)
            let fontSize = min(size.height / (CGFloat(lines.count) + 0.55), 100 * usable / max(widest, 1))
            let font = UIFont.systemFont(ofSize: fontSize, weight: .heavy)
            let para = NSMutableParagraphStyle(); para.alignment = .center
            let attrs: [NSAttributedString.Key: Any] = [.font: font, .foregroundColor: colour(ink), .paragraphStyle: para, .kern: fontSize * 0.04]
            let lineHeight = font.lineHeight
            let total = lineHeight * CGFloat(lines.count)
            let leftPad: CGFloat = lock ? fontSize * 0.9 : 0
            for (i, line) in lines.enumerated() {
                (line as NSString).draw(in: CGRect(x: leftPad, y: (size.height - total) / 2 + CGFloat(i) * lineHeight, width: size.width, height: lineHeight), withAttributes: attrs)
            }
            if lock {
                let s = fontSize * 0.55, cx = size.width * 0.17, cy = size.height * 0.52
                colour(ink).setFill()
                UIBezierPath(roundedRect: CGRect(x: cx - s / 2, y: cy - s * 0.1, width: s, height: s * 0.75), cornerRadius: s * 0.12).fill()
                colour(ink).setStroke(); g.setLineWidth(s * 0.14)
                g.addArc(center: CGPoint(x: cx, y: cy - s * 0.1), radius: s * 0.28, startAngle: .pi, endAngle: 0, clockwise: false); g.strokePath()
            }
        }
    }
    static func signMaterial(_ image: UIImage) -> SCNMaterial {
        let m = SCNMaterial(); m.lightingModel = .constant; m.diffuse.contents = image; m.isDoubleSided = false
        m.diffuse.mipFilter = .linear; m.diffuse.minificationFilter = .linear; m.diffuse.maxAnisotropy = 4
        return m
    }
    /// A standing sign: a plate (width x height, metres) facing -z by default (rotate the node to face the way it should); the plate has a back in `navy`.
    static func sign(_ text: String, width: Float, height: Float, fill: String = navy, ink: String = white, lock: Bool = false, lines: Int = 1) -> SCNNode {
        let node = SCNNode()
        let aspect = CGFloat(width / height)
        let px: CGFloat = 160 * CGFloat(lines)
        let image = signImage(text, fill: fill, ink: ink, size: CGSize(width: px * aspect, height: px), lock: lock)
        let face = SCNPlane(width: CGFloat(width), height: CGFloat(height)); face.materials = [signMaterial(image)]
        let faceNode = SCNNode(geometry: face); faceNode.position.z = -0.021; faceNode.eulerAngles.y = .pi
        let back = SCNBox(width: CGFloat(width), height: CGFloat(height), length: 0.04, chamferRadius: 0.02); back.materials = [mat(fill)]
        node.addChildNode(SCNNode(geometry: back)); node.addChildNode(faceNode)
        return node
    }

    // MARK: primitives (all grounded: the node's y is the base)
    /// Place a node so that the bottom of its geometry rests at (x, y, z). (Pivots are not used: flattening the scene ignores them.)
    static func grounded(_ n: SCNNode, x: Float = 0, y: Float = 0, z: Float = 0) -> SCNNode {
        n.position = SCNVector3(x, y - Float(n.boundingBox.min.y), z); return n
    }
    @discardableResult static func box(_ w: Float, _ h: Float, _ d: Float, x: Float = 0, y: Float = 0, z: Float = 0, mat m: SCNMaterial, chamfer: Float = 0.03, yaw: Float = 0, parent: SCNNode? = nil) -> SCNNode {
        let g = SCNBox(width: CGFloat(w), height: CGFloat(h), length: CGFloat(d), chamferRadius: CGFloat(min(chamfer, min(w, h, d) / 2.2)))
        g.chamferSegmentCount = 2; g.materials = [m]
        let n = grounded(SCNNode(geometry: g), x: x, y: y, z: z); n.eulerAngles.y = yaw
        parent?.addChildNode(n); return n
    }
    @discardableResult static func cylinder(_ r: Float, _ h: Float, x: Float = 0, y: Float = 0, z: Float = 0, mat m: SCNMaterial, segments: Int = 24, parent: SCNNode? = nil) -> SCNNode {
        let g = SCNCylinder(radius: CGFloat(r), height: CGFloat(h)); g.radialSegmentCount = segments; g.materials = [m]
        let n = grounded(SCNNode(geometry: g), x: x, y: y, z: z)
        parent?.addChildNode(n); return n
    }
    @discardableResult static func cone(top: Float, bottom: Float, _ h: Float, x: Float = 0, y: Float = 0, z: Float = 0, mat m: SCNMaterial, segments: Int = 20, parent: SCNNode? = nil) -> SCNNode {
        let g = SCNCone(topRadius: CGFloat(top), bottomRadius: CGFloat(bottom), height: CGFloat(h)); g.radialSegmentCount = segments; g.materials = [m]
        let n = grounded(SCNNode(geometry: g), x: x, y: y, z: z)
        parent?.addChildNode(n); return n
    }
    @discardableResult static func sphere(_ r: Float, x: Float = 0, y: Float = 0, z: Float = 0, mat m: SCNMaterial, squash: Float = 1, segments: Int = 16, parent: SCNNode? = nil) -> SCNNode {
        let g = SCNSphere(radius: CGFloat(r)); g.segmentCount = segments; g.materials = [m]
        let n = SCNNode(geometry: g); n.position = SCNVector3(x, y, z); n.scale = SCNVector3(1, squash, 1)
        parent?.addChildNode(n); return n
    }
    /// A hipped roof over a w x d plan, h high (a flat-topped pyramid when `tip` > 0).
    @discardableResult static func roof(_ w: Float, _ d: Float, _ h: Float, x: Float = 0, y: Float = 0, z: Float = 0, mat m: SCNMaterial, yaw: Float = 0, parent: SCNNode? = nil) -> SCNNode {
        let g = SCNPyramid(width: CGFloat(w), height: CGFloat(h), length: CGFloat(d)); g.materials = [m]
        let n = grounded(SCNNode(geometry: g), x: x, y: y, z: z); n.eulerAngles.y = yaw
        parent?.addChildNode(n); return n
    }
    /// A ridge roof: two sloped sides over a w (along x) x d plan, h high at the ridge.
    @discardableResult static func gable(_ w: Float, _ d: Float, _ h: Float, x: Float = 0, y: Float = 0, z: Float = 0, mat m: SCNMaterial, yaw: Float = 0, parent: SCNNode? = nil) -> SCNNode {
        let hw = w / 2, hd = d / 2
        let v: [SCNVector3] = [SCNVector3(-hw, 0, -hd), SCNVector3(hw, 0, -hd), SCNVector3(hw, 0, hd), SCNVector3(-hw, 0, hd), SCNVector3(-hw, h, 0), SCNVector3(hw, h, 0)]
        // each face has its own vertices so the normals are flat
        var pos: [SCNVector3] = [], nor: [SCNVector3] = [], idx: [Int32] = []
        func face(_ a: Int, _ b: Int, _ c: Int, _ dd: Int? = nil) {
            let pts = dd == nil ? [v[a], v[b], v[c]] : [v[a], v[b], v[c], v[dd!]]
            let e1 = SIMD3<Float>(Float(pts[1].x - pts[0].x), Float(pts[1].y - pts[0].y), Float(pts[1].z - pts[0].z))
            let e2 = SIMD3<Float>(Float(pts[2].x - pts[0].x), Float(pts[2].y - pts[0].y), Float(pts[2].z - pts[0].z))
            let n3 = simd_normalize(simd_cross(e1, e2)); let n = SCNVector3(n3.x, n3.y, n3.z)
            let base = Int32(pos.count)
            pos += pts; nor += pts.map { _ in n }
            idx += [base, base + 1, base + 2]
            if dd != nil { idx += [base, base + 2, base + 3] }
        }
        face(0, 1, 2, 3); face(0, 4, 5, 1); face(3, 2, 5, 4); face(0, 3, 4); face(1, 5, 2)
        let g = SCNGeometry(sources: [SCNGeometrySource(vertices: pos), SCNGeometrySource(normals: nor)], elements: [SCNGeometryElement(indices: idx, primitiveType: .triangles)])
        g.materials = [m]
        let n = SCNNode(geometry: g); n.position = SCNVector3(x, y, z); n.eulerAngles.y = yaw
        parent?.addChildNode(n); return n
    }

    // MARK: a palm
    private static var frond: SCNGeometry?
    private static func frondGeometry() -> SCNGeometry {
        if let frond { return frond }
        let segments = 8, length: Float = 2.9, droop: Float = 1.5, half: Float = 0.5
        var pos: [SCNVector3] = [], nor: [SCNVector3] = [], idx: [Int32] = []
        for s in 0...segments {
            let t = Float(s) / Float(segments)
            let x = length * t, y = 0.55 * sin(Float.pi * min(t * 1.4, 1)) - droop * t * t
            let w = half * sin(Float.pi * pow(0.1 + 0.9 * t, 0.9)) * (1 - 0.3 * t)
            pos.append(SCNVector3(x, y, w)); pos.append(SCNVector3(x, y, -w))
            nor.append(SCNVector3(0, 1, 0)); nor.append(SCNVector3(0, 1, 0))
        }
        for s in 0..<segments {
            let a = Int32(s * 2); idx += [a, a + 1, a + 3, a, a + 3, a + 2]
        }
        let g = SCNGeometry(sources: [SCNGeometrySource(vertices: pos), SCNGeometrySource(normals: nor)], elements: [SCNGeometryElement(indices: idx, primitiveType: .triangles)])
        let m = SCNMaterial(); m.lightingModel = .lambert; m.diffuse.contents = colour("2C8A28"); m.isDoubleSided = true
        g.materials = [m]
        frond = g
        return g
    }
    static func palm(height: Float, lean: Float, azimuth: Float, seed: Int) -> SCNNode {
        let root = SCNNode()
        let trunkMat = mat("8B6A47")
        var rng = SeededRandom(seed: UInt64(seed))
        let segments = 6
        var prev = SIMD3<Float>(0, 0, 0)
        for i in 1...segments {
            let t = Float(i) / Float(segments)
            let p = SIMD3<Float>(cos(azimuth) * lean * t * t * height * 0.12, height * t, sin(azimuth) * lean * t * t * height * 0.12)
            let mid = (prev + p) / 2, len = simd_length(p - prev)
            let c = SCNNode(geometry: { let g = SCNCone(topRadius: CGFloat(0.2 - 0.07 * t), bottomRadius: CGFloat(0.2 - 0.07 * (t - 1 / Float(segments))), height: CGFloat(len) * 1.04); g.radialSegmentCount = 8; g.materials = [trunkMat]; return g }())
            c.simdPosition = mid
            c.simdOrientation = simd_quatf(from: SIMD3(0, 1, 0), to: simd_normalize(p - prev))
            root.addChildNode(c); prev = p
        }
        let crown = SCNNode(); crown.simdPosition = prev; root.addChildNode(crown)
        for k in 0..<10 {
            let f = SCNNode(geometry: frondGeometry())
            f.eulerAngles = SCNVector3(Float(rng.next(in: -0.12...0.12)), Float(k) * .pi * 2 / 10 + Float(rng.next(in: -0.15...0.15)), Float(rng.next(in: -0.1...0.18)))
            let s = Float(rng.next(in: 0.85...1.15)); f.scale = SCNVector3(s, s, s)
            crown.addChildNode(f)
        }
        return root
    }

    struct SeededRandom {
        var state: UInt64
        init(seed: UInt64) { state = seed &* 6364136223846793005 &+ 1442695040888963407 }
        mutating func next() -> Double {
            state = state &* 6364136223846793005 &+ 1442695040888963407
            return Double((state >> 33) & 0xFFFFFF) / Double(0x1000000)
        }
        mutating func next(in range: ClosedRange<Double>) -> Double { range.lowerBound + (range.upperBound - range.lowerBound) * next() }
    }

    // MARK: sky
    /// A vertical sky gradient as a 2:1 sphere map: the background and the soft light that fills the shadows.
    static func skyImage() -> UIImage {
        let size = CGSize(width: 512, height: 256)
        return UIGraphicsImageRenderer(size: size).image { ctx in
            let colors = [colour("2E86E0"), colour("6FB6F0"), colour("BFE3F4"), colour("F4EBD3"), colour("B7CFC0")].map(\.cgColor) as CFArray
            let gradient = CGGradient(colorsSpace: CGColorSpaceCreateDeviceRGB(), colors: colors, locations: [0, 0.32, 0.47, 0.5, 1])!
            ctx.cgContext.drawLinearGradient(gradient, start: .zero, end: CGPoint(x: 0, y: size.height), options: [])
            // a few soft clouds
            var rng = SeededRandom(seed: 7)
            for _ in 0..<26 {
                let x = CGFloat(rng.next(in: 0...512)), y = CGFloat(rng.next(in: 30...110)), w = CGFloat(rng.next(in: 40...120))
                UIColor(white: 1, alpha: 0.55).setFill()
                for dx in [CGFloat(0), -512, 512] { ctx.cgContext.fillEllipse(in: CGRect(x: x + dx, y: y, width: w, height: w * 0.28)) }
            }
        }
    }
}
