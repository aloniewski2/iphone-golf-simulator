from pathlib import Path
p=Path('GolfArcade/Unity/IslandScreens.swift');s=p.read_text();s=s.replace('''                    HStack(spacing:8) {
                        action("net-ready",local?.ready == true ? "Ready" : "Ready?",icon:"checkmark",ready:local?.ready ?? false)
                        action("net-emotes","Emotes",icon:"face.smiling")
                    }
                    HStack { action("net-clothes","Change Clothes",icon:"tshirt"); action("net-settings","Settings",icon:"slider.horizontal.3") }''','''                    action("net-ready",local?.ready == true ? "Ready" : "Ready?",icon:"checkmark",ready:local?.ready ?? false)
                    HStack { action("net-emotes","Emotes",icon:"face.smiling"); action("net-clothes","Clothes",icon:"tshirt") }
                    action("net-settings","Match Settings",icon:"slider.horizontal.3")''');s=s.replace('case .lobby: min(382,height * 0.45)','case .lobby: min(438,height * 0.51)');p.write_text(s)
p=Path('GolfArcade/Unity/TennisMenu.swift');s=p.read_text().replace('[["net-ready","net-emotes","net-clothes"],["net-settings"],["net-invite","net-find"]]','[["net-ready"],["net-emotes","net-clothes"],["net-settings"],["net-invite","net-find"]]');p.write_text(s)
p=Path('GolfArcade/Unity/IslandKit.swift');s=p.read_text().replace('Text(participant.seat < 0 ? "Watching" : "P\\(participant.seat + 1)").islandType(compact ? 11 : 14, bold: true)','Text(participant.seat < 0 ? "Watching" : "P\\(participant.seat + 1)").islandType(compact ? 11 : 14, bold: true).lineLimit(1).minimumScaleFactor(0.6)');p.write_text(s)
p=Path('GolfArcade/Unity/CharacterModelPreview.swift');s=p.read_text().replace('view.scene = context.coordinator.recipe.scene; view.pointOfView','view.delegate = context.coordinator\n        view.scene = context.coordinator.recipe.scene; view.pointOfView');s=s.replace('@MainActor final class Coordinator: NSObject {','@MainActor final class Coordinator: NSObject, @preconcurrency SCNSceneRendererDelegate {');s=s.replace('        private var previousFrame = 0.0','        private var previousFrame = 0.0\n        private var framedSize = CGSize.zero\n        func resetMeasurement() { frames = 0; measuredSeconds = 0; previousFrame = 0 }\n        nonisolated func renderer(_ renderer: any SCNSceneRenderer, didRenderScene scene: SCNScene, atTime time: TimeInterval) {\n            Task { @MainActor [weak self] in self?.recordFrame(time) }\n        }\n        private func recordFrame(_ time: Double) {\n            if previousFrame > 0 { measuredSeconds += time - previousFrame; frames += 1 }; previousFrame = time\n        }');s=s.replace('''                        recipe.scene.rootNode.addChildNode(holder)
                        holder.opacity''','''                        let shadow = SCNNode(geometry:SCNPlane(width:0.85,height:0.42))
                        shadow.eulerAngles.x = -.pi / 2; shadow.position.y = -0.02
                        shadow.geometry?.firstMaterial?.diffuse.contents = UIColor(IslandUI.navy).withAlphaComponent(0.15)
                        shadow.geometry?.firstMaterial?.lightingModel = .constant; shadow.geometry?.firstMaterial?.isDoubleSided = true
                        holder.addChildNode(shadow)
                        recipe.scene.rootNode.addChildNode(holder)
                        holder.opacity''');s=s.replace('''            if previousFrame > 0 { measuredSeconds += link.timestamp - previousFrame; frames += 1 }; previousFrame = link.timestamp
            reframe(); tickPose(at:link.timestamp + offset)''','''            if let size = view?.bounds.size, size != framedSize { framedSize = size; reframe() }
            tickPose(at:link.timestamp + offset)''');p.write_text(s)
