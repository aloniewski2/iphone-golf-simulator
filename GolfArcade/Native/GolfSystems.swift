import RealityKit
import QuartzCore

struct SessionComponent: Component { weak var session: GameSession? }
struct ScoredBallComponent: Component { var shotID: Int?; var elapsed = 0.0 }
struct GolferPresentationComponent: Component { var motion: MotionSnapshot? }

@MainActor
enum GolfSystemRegistry {
    private static var registered = false
    static let query = EntityQuery(where: .has(SessionComponent.self))
    static func register() {
        guard !registered else { return }
        registered = true
        SessionComponent.registerComponent()
        ScoredBallComponent.registerComponent()
        GolferPresentationComponent.registerComponent()
        NativeBreezeComponent.registerComponent()
        InputApplicationSystem.registerSystem()
        GameplaySystem.registerSystem()
        BallPresentationSystem.registerSystem()
        GolferAnimationSystem.registerSystem()
        CameraSystem.registerSystem()
        EffectsSystem.registerSystem()
    }
    static func sessions(_ context: SceneUpdateContext, apply: (GameSession) -> Void) {
        for entity in context.entities(matching: query, updatingSystemWhen: .rendering) {
            if let session = entity.components[SessionComponent.self]?.session { apply(session) }
        }
    }
}

struct InputApplicationSystem: System {
    init(scene: RealityKit.Scene) {}
    @MainActor func update(context: SceneUpdateContext) {
        GolfSystemRegistry.sessions(context) {
            $0.beginPerformanceFrame()
            $0.viewport.applyInputSnapshot()
        }
    }
}
struct GameplaySystem: System {
    static var dependencies: [SystemDependency] { [.after(InputApplicationSystem.self)] }
    init(scene: RealityKit.Scene) {}
    @MainActor func update(context: SceneUpdateContext) { GolfSystemRegistry.sessions(context) { $0.advance() } }
}
struct BallPresentationSystem: System {
    static var dependencies: [SystemDependency] { [.after(GameplaySystem.self)] }
    init(scene: RealityKit.Scene) {}
    @MainActor func update(context: SceneUpdateContext) { GolfSystemRegistry.sessions(context) { $0.viewport.updateBall() } }
}
struct GolferAnimationSystem: System {
    static var dependencies: [SystemDependency] { [.after(BallPresentationSystem.self)] }
    init(scene: RealityKit.Scene) {}
    @MainActor func update(context: SceneUpdateContext) { GolfSystemRegistry.sessions(context) { $0.viewport.updateGolfer() } }
}
struct CameraSystem: System {
    static var dependencies: [SystemDependency] { [.after(GolferAnimationSystem.self)] }
    init(scene: RealityKit.Scene) {}
    @MainActor func update(context: SceneUpdateContext) { GolfSystemRegistry.sessions(context) { $0.viewport.updateCamera() } }
}
struct EffectsSystem: System {
    static var dependencies: [SystemDependency] { [.after(CameraSystem.self)] }
    init(scene: RealityKit.Scene) {}
    @MainActor func update(context: SceneUpdateContext) {
        GolfSystemRegistry.sessions(context) {
            $0.viewport.updateEffects()
            if $0.performance.enabled { $0.performance.endFrame(now: CACurrentMediaTime()) }
        }
    }
}
