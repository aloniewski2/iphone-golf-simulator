import SwiftUI
import SceneKit

/// Real, baked-UV geometry. ImageGen source projections are authoring inputs only;
/// no scene photograph, screen-facing billboard, or camera projection is used at runtime.
struct ClubEnvironmentAsset: Decodable {
    let positions: [Float], normals: [Float], uv: [Float]
    let triangles: [Int32]
    let texture: String
    let camera: [Float], target: [Float]
    let room: String

    @MainActor private static var cache: [String: ClubEnvironmentAsset] = [:]
    @MainActor static func load(_ room: ClubRoom) -> ClubEnvironmentAsset? {
        if let asset = cache[room.rawValue] { return asset }
        guard let url = Bundle.main.url(forResource: "Club_\(room.rawValue)", withExtension: "json"),
              let data = try? Data(contentsOf: url), let asset = try? JSONDecoder().decode(Self.self, from: data) else { return nil }
        cache[room.rawValue] = asset
        return asset
    }
    func geometry() -> SCNGeometry {
        func source(_ values: [Float], _ semantic: SCNGeometrySource.Semantic, _ width: Int) -> SCNGeometrySource {
            SCNGeometrySource(data: values.withUnsafeBytes { Data($0) }, semantic: semantic,
                vectorCount: values.count / width, usesFloatComponents: true, componentsPerVector: width,
                bytesPerComponent: 4, dataOffset: 0, dataStride: width * 4)
        }
        return SCNGeometry(sources: [source(positions, .vertex, 3), source(normals, .normal, 3), source(uv, .texcoord, 2)],
            elements: [SCNGeometryElement(data: triangles.withUnsafeBytes { Data($0) }, primitiveType: .triangles,
                primitiveCount: triangles.count / 3, bytesPerIndex: 4)])
    }
}

private final class ClubEnvironmentSceneView: SCNView {
    override func layoutSubviews() {
        super.layoutSubviews()
        let portrait = bounds.height > bounds.width
        pointOfView?.camera?.projectionDirection = portrait ? .vertical : .horizontal
        pointOfView?.camera?.fieldOfView = portrait ? 55 : 60
    }
}

struct ClubEnvironmentView: UIViewRepresentable {
    var room: ClubRoom
    @Environment(\.accessibilityReduceMotion) private var reduceMotion
    func makeCoordinator() -> Coordinator { Coordinator() }
    func makeUIView(context: Context) -> SCNView {
        let view = ClubEnvironmentSceneView()
        view.backgroundColor = UIColor(red: 0.66, green: 0.82, blue: 0.90, alpha: 1)
        view.antialiasingMode = .multisampling4X
        view.autoenablesDefaultLighting = false
        view.allowsCameraControl = false
        view.isUserInteractionEnabled = false
        view.preferredFramesPerSecond = 30
        view.accessibilityElementsHidden = true
        updateUIView(view, context: context)
        return view
    }
    func updateUIView(_ view: SCNView, context: Context) {
        context.coordinator.show(room, in: view, animate: !reduceMotion && !SportsSession.shared.reduceMotion)
    }
    static func dismantleUIView(_ view: SCNView, coordinator: Coordinator) {
        view.isPlaying = false; coordinator.camera.removeAllActions(); view.scene = nil
    }
    @MainActor final class Coordinator {
        private var room: ClubRoom?
        private var moving = false
        private(set) var camera = SCNNode()
        func show(_ room: ClubRoom, in view: SCNView, animate: Bool) {
            if self.room != room {
                self.room = room; camera.removeAllActions(); moving = false
                let scene = SCNScene()
                scene.background.contents = UIColor(red: 0.66, green: 0.82, blue: 0.90, alpha: 1)
                scene.fogColor = UIColor(red: 0.70, green: 0.83, blue: 0.86, alpha: 1)
                scene.fogStartDistance = 55; scene.fogEndDistance = 145
                guard let asset = ClubEnvironmentAsset.load(room) else { view.scene = scene; return }
                let geometry = asset.geometry(), material = SCNMaterial()
                material.name = "Baked ImageGen albedo · \(room.rawValue)"
                material.lightingModel = .physicallyBased
                material.diffuse.contents = UIImage(named: asset.texture + ".png")
                material.diffuse.magnificationFilter = .linear
                material.diffuse.minificationFilter = .linear
                material.diffuse.mipFilter = .linear
                material.diffuse.maxAnisotropy = 8
                material.roughness.contents = 0.82
                material.metalness.contents = 0
                material.isDoubleSided = true
                geometry.materials = [material]
                let environment = SCNNode(geometry: geometry); environment.name = "ClubEnvironment_\(room.rawValue)"
                scene.rootNode.addChildNode(environment)
                camera = SCNNode(); camera.name = "Club environment camera"; camera.camera = SCNCamera()
                camera.camera?.fieldOfView = 60; camera.camera?.projectionDirection = .horizontal
                camera.camera?.zNear = 0.1; camera.camera?.zFar = 240
                camera.camera?.wantsHDR = false; camera.camera?.exposureOffset = 0
                camera.position = SCNVector3(asset.camera[0], asset.camera[1], asset.camera[2])
                camera.look(at: SCNVector3(asset.target[0], asset.target[1], asset.target[2]))
                scene.rootNode.addChildNode(camera)
                let key = SCNNode(); key.light = SCNLight(); key.light?.type = .directional
                key.light?.intensity = room == .locker ? 850 : 1050
                key.light?.color = UIColor(red: 1, green: 0.94, blue: 0.82, alpha: 1)
                key.position = SCNVector3(-8, 14, 8); key.look(at: SCNVector3(0, 0, -5))
                key.light?.castsShadow = true; key.light?.shadowMode = .forward
                key.light?.shadowMapSize = CGSize(width: 2048, height: 2048)
                key.light?.automaticallyAdjustsShadowProjection = false
                key.light?.zNear = 1; key.light?.zFar = 75
                key.light?.orthographicScale = 35; key.light?.shadowRadius = 5; key.light?.shadowSampleCount = 8
                key.light?.shadowColor = UIColor(red: 0.14, green: 0.19, blue: 0.23, alpha: 0.24)
                scene.rootNode.addChildNode(key)
                let fill = SCNNode(); fill.light = SCNLight(); fill.light?.type = .ambient
                fill.light?.intensity = 420; fill.light?.color = UIColor(red: 0.91, green: 0.95, blue: 1, alpha: 1)
                scene.rootNode.addChildNode(fill)
                view.scene = scene; view.pointOfView = camera
            }
            if moving != animate {
                moving = animate; camera.removeAllActions()
                if let asset = ClubEnvironmentAsset.load(room) {
                    let base = SCNVector3(asset.camera[0], asset.camera[1], asset.camera[2])
                    let target = SCNVector3(asset.target[0], asset.target[1], asset.target[2])
                    camera.position = base; camera.look(at: target)
                    if animate {
                        // Genuine depth parallax, deliberately quiet behind readable native controls.
                        let drift = SCNAction.customAction(duration: 20) { node, elapsed in
                            let phase = Float(elapsed / 20) * .pi * 2
                            node.position = SCNVector3(base.x + sin(phase) * 0.24, base.y, base.z)
                            node.look(at: target)
                        }
                        camera.runAction(.repeatForever(drift))
                    }
                }
            }
            view.isPlaying = animate
        }
    }
}

// Cinemagraph treatment of the approved stills. Architecture stays rigid; only
// green foliage and the distant water receive small, localized UV displacement.
import SpriteKit

struct ClubLivingImageView: UIViewRepresentable {
    let room: ClubRoom
    @Environment(\.accessibilityReduceMotion) private var reduceMotion
    @Environment(\.scenePhase) private var scenePhase

    func makeUIView(context: Context) -> LivingClubView {
        let view = LivingClubView()
        view.isUserInteractionEnabled = false
        view.accessibilityElementsHidden = true
        view.preferredFramesPerSecond = 30
        view.ignoresSiblingOrder = true
        return view
    }
    func updateUIView(_ view: LivingClubView, context: Context) {
        view.show(room, animated: !reduceMotion && !SportsSession.shared.reduceMotion && scenePhase == .active)
    }
    static func dismantleUIView(_ view: LivingClubView, coordinator: ()) {
        view.isPaused = true
        view.presentScene(nil)
    }
}

final class LivingClubView: SKView {
    private var room: ClubRoom?
    private var photo: SKSpriteNode?
    private var motionShader: SKShader?
    override func layoutSubviews() {
        super.layoutSubviews()
        guard bounds.width > 0, bounds.height > 0 else { return }
        scene?.size = bounds.size
        (scene as? LivingClubScene)?.fitImage()
    }
    func show(_ room: ClubRoom, animated: Bool) {
        if self.room != room || scene == nil {
            self.room = room
            let scene = LivingClubScene(size: bounds.size.width > 0 ? bounds.size : CGSize(width: 1280, height: 720))
            scene.scaleMode = .resizeFill
            scene.backgroundColor = .init(red: 0.76, green: 0.86, blue: 0.9, alpha: 1)
            let sprite = SKSpriteNode(texture: SKTexture(imageNamed: "club-\(room.rawValue).png"))
            sprite.texture?.filteringMode = .linear
            // UV coordinates are bottom-up. Restrict water shimmer to the bay,
            // avoiding the blue courts, roof canvas, and the white architecture.
            let bay: SIMD4<Float>
            switch room {
            case .entrance: bay = SIMD4(0.06, 0.48, 0.43, 0.56)
            case .loading: bay = SIMD4(0.43, 0.85, 0.57, 0.74)
            default: bay = SIMD4(0.51, 0.90, 0.53, 0.65)
            }
            let shader = SKShader(source: """
            void main() {
                vec2 uv = v_tex_coord;
                vec4 original = texture2D(u_texture, uv);
                float green = smoothstep(0.025, 0.11, original.g - original.b)
                    * smoothstep(-0.015, 0.055, original.g - original.r)
                    * (1.0 - smoothstep(0.60, 0.90, original.r));
                float breeze = sin(u_time * 0.85 + uv.y * 8.0) * 0.0009
                    + sin(u_time * 1.35 + uv.x * 11.0) * 0.00035;
                vec2 sampleUV = uv + vec2(breeze, breeze * 0.28) * green;
                float water = smoothstep(u_bay.x, u_bay.x + 0.03, uv.x)
                    * (1.0 - smoothstep(u_bay.y - 0.03, u_bay.y, uv.x))
                    * smoothstep(u_bay.z, u_bay.z + 0.02, uv.y)
                    * (1.0 - smoothstep(u_bay.w - 0.02, u_bay.w, uv.y))
                    * smoothstep(0.07, 0.22, original.b - original.r);
                sampleUV.x += sin(uv.y * 420.0 + u_time * 1.1) * 0.00065 * water;
                vec4 color = texture2D(u_texture, clamp(sampleUV, vec2(0.001), vec2(0.999)));
                color.rgb += vec3(0.009 * sin(uv.y * 620.0 + uv.x * 27.0 - u_time * 1.2) * water);
                gl_FragColor = color;
            }
            """)
            shader.uniforms = [SKUniform(name: "u_bay", vectorFloat4: bay)]
            motionShader = shader; photo = sprite
            scene.addChild(sprite)
            scene.photo = sprite
            scene.fitImage()
            presentScene(scene)
            setNeedsLayout(); layoutIfNeeded()
        }
        photo?.shader = animated ? motionShader : nil
        isPaused = !animated
    }
}

private final class LivingClubScene: SKScene {
    weak var photo: SKSpriteNode?
    override func didChangeSize(_ oldSize: CGSize) { fitImage() }
    override func didMove(to view: SKView) { fitImage() }
    func fitImage() {
        guard let photo, let imageSize = photo.texture?.size(), size.width > 0, size.height > 0 else { return }
        let scale = max(size.width / imageSize.width, size.height / imageSize.height) * 1.012
        photo.size = CGSize(width: imageSize.width * scale, height: imageSize.height * scale)
        photo.position = CGPoint(x: size.width / 2, y: size.height / 2)
    }
}
