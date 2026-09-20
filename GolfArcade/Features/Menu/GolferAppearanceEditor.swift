import RealityKit
import SwiftUI

struct GolferAppearanceEditor: View {
    @ObservedObject var flow: GameFlow
    let player: Player
    @State private var look: GolferAppearance
    @Environment(\.dismiss) private var dismiss

    init(flow: GameFlow, player: Player) {
        self.flow=flow; self.player=player
        _look=State(initialValue:player.golferAppearance)
    }

    var body: some View {
        NavigationStack {
            Form {
                Section {
                    GolferAppearancePreview(appearance:look,handedness:player.handedness)
                        .frame(height:240)
                        .accessibilityLabel("\(look.preset.title) golfer in \(look.outfit.title), \(look.headwear.title)")
                }
                Section("Original resort presets") {
                    Picker("Preset",selection:Binding(get:{look.preset},set:{look = .preset($0)})) {
                        ForEach(GolferAppearance.Preset.allCases) { Text($0.title).tag($0) }
                    }.pickerStyle(.segmented).accessibilityIdentifier("golferPreset")
                    Text("Presets change appearance only. All golfers use your saved handedness and the same swing mechanics.")
                        .font(.caption).foregroundStyle(.secondary)
                }
                Section("Personalize") {
                    Picker("Skin tone",selection:$look.skin) { ForEach(GolferAppearance.Skin.allCases) { Text($0.title).tag($0) } }
                        .accessibilityIdentifier("golferSkin")
                    Picker("Hair color",selection:$look.hair) { ForEach(GolferAppearance.Hair.allCases) { Text($0.title).tag($0) } }
                    Picker("Outfit color",selection:$look.outfit) { ForEach(GolferAppearance.Outfit.allCases) { Text($0.title).tag($0) } }
                    Picker("Headwear",selection:$look.headwear) { ForEach(GolferAppearance.Headwear.allCases) { Text($0.title).tag($0) } }
                        .pickerStyle(.segmented)
                }
            }
            .navigationTitle("\(player.name)'s golfer")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement:.cancellationAction) { Button("Cancel") { dismiss() } }
                ToolbarItem(placement:.confirmationAction) {
                    Button("Save") { flow.setAppearance(player.id,look); dismiss() }.accessibilityIdentifier("saveGolfer")
                }
            }
        }
    }
}

/// Loads the exact packaged golfer used in gameplay; cosmetic edits reuse it.
struct GolferAppearancePreview: View {
    let appearance: GolferAppearance
    let handedness: Handedness
    @State private var golfer: Entity?
    @State private var failure: String?

    var body: some View {
        ZStack {
            if let golfer {
                NativeAppearanceViewport(golfer: golfer, appearance: appearance, handedness: handedness)
                    .accessibilityIdentifier("nativeAppearancePreview")
            } else if let failure {
                ContentUnavailableView("Golfer unavailable", systemImage: "exclamationmark.triangle",
                    description: Text(failure))
            } else {
                ProgressView("Loading golfer…")
            }
        }
        .task {
            guard golfer == nil else { return }
            do {
                let loaded = try await NativeAssetLoader().golfer()
                try Task.checkCancellation()
                golfer = loaded
            } catch is CancellationError {
                return
            } catch {
                failure = error.localizedDescription
            }
        }
    }
}

private struct NativeAppearanceViewport: UIViewRepresentable {
    let golfer: Entity
    let appearance: GolferAppearance
    let handedness: Handedness

    @MainActor final class Coordinator {
        let anchor = AnchorEntity(world: .zero)
        let camera = PerspectiveCamera()
        var playback: AnimationPlaybackController?
        var appearance: GolferAppearance?
        var handedness: Handedness?
    }

    func makeCoordinator() -> Coordinator { Coordinator() }

    func makeUIView(context: Context) -> ARView {
        let view = ARView(frame: .zero, cameraMode: .nonAR, automaticallyConfigureSession: false)
        view.environment.background = .color(.clear)
        view.renderOptions.insert(.disableMotionBlur)
        let anchor = context.coordinator.anchor
        anchor.addChild(golfer)
        let light = DirectionalLight()
        light.light.intensity = 3_500
        light.orientation = simd_quatf(angle: -.pi / 3, axis: SIMD3(1, 0.3, 0))
        light.shadow = .init(shadowProjection: .automatic(maximumDistance: 8), depthBias: 0.5)
        anchor.addChild(light)
        let camera = context.coordinator.camera
        camera.camera.fieldOfViewInDegrees = 38
        anchor.addChild(camera)
        view.scene.addAnchor(anchor)
        if let animation = golfer.availableAnimations.first(where: { $0.name == "iron" }) {
            let playback = golfer.playAnimation(animation, transitionDuration: 0)
            playback.speed = 0; playback.time = 0
            context.coordinator.playback = playback
        }
        return view
    }

    func updateUIView(_ view: ARView, context: Context) {
        if context.coordinator.appearance != appearance {
            NativeGolferStyle.apply(appearance, to: golfer)
            context.coordinator.appearance = appearance
        }
        if context.coordinator.handedness != handedness {
            let direction: Float = handedness == .left ? -1 : 1
            golfer.scale.x = direction
            context.coordinator.camera.look(at: SIMD3(-0.5 * direction, 0.85, 0),
                from: SIMD3(2 * direction, 1.6, 3.3), relativeTo: context.coordinator.anchor)
            context.coordinator.handedness = handedness
        }
    }

    static func dismantleUIView(_ view: ARView, coordinator: Coordinator) {
        coordinator.playback?.stop()
        view.scene.removeAnchor(coordinator.anchor)
        coordinator.anchor.children.removeAll()
    }
}
