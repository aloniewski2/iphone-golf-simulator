import SceneKit
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

/// A static pose; redraw only when cosmetic choices change, not on a tracking timer.
struct GolferAppearancePreview: UIViewRepresentable {
    let appearance: GolferAppearance
    let handedness: Handedness
    func makeCoordinator() -> Coordinator { Coordinator() }
    final class Coordinator { var appearance: GolferAppearance?; var handedness: Handedness?; var rig: AvatarRig? }
    func makeUIView(context: Context) -> SCNView {
        let view=SCNView(); view.scene=SCNScene(); view.backgroundColor = .clear
        view.autoenablesDefaultLighting=true; view.allowsCameraControl=false
        view.antialiasingMode = .multisampling4X
        let camera=SCNNode(); camera.camera=SCNCamera(); camera.camera?.fieldOfView=42
        view.scene?.rootNode.addChildNode(camera); view.pointOfView=camera
        return view
    }
    func updateUIView(_ view: SCNView,context: Context) {
        guard context.coordinator.appearance != appearance || context.coordinator.handedness != handedness else { return }
        context.coordinator.rig?.node.removeFromParentNode()
        let rig=AvatarRig(shirt:appearance.shirtColor,appearance:appearance)
        rig.setMirrored(handedness == .left); rig.apply(AvatarAnimations.address)
        view.scene?.rootNode.addChildNode(rig.node)
        view.pointOfView?.position=SCNVector3(handedness == .left ? -11 : 11,5.5,7)
        view.pointOfView?.look(at:SCNVector3(0.4,2.8,0))
        context.coordinator.rig=rig; context.coordinator.appearance=appearance; context.coordinator.handedness=handedness
    }
}
