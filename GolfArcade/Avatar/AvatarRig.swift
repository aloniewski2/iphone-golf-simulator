import SceneKit
import UIKit
import ModelIO
import SceneKit.ModelIO

/// Rounded original sports-game golfer. All meshes are built once; the existing
/// measured joints and authoritative club endpoints continue to drive every pose.
@MainActor
final class AvatarRig {
    let node = SCNNode()
    private let body = SCNNode()
    private let skinMesh: GolferSkin?
    private let authoredSkin: ResortGolferSkin?
    private let sunwardSkin: SunwardGolferSkin?
    private var mirrored = false
    private let torso = SCNNode()
    private let collar = SCNNode()
    private let head = SCNNode()
    private let pelvis = SCNNode()
    private var hands: [SCNNode] = []
    private var feet: [SCNNode] = []
    private let shaft = SCNNode()
    private let handle = SCNNode()
    private let clubHead = SCNNode()
    private let contactShadow = SCNNode()
    private var displayedClub: GolfClub?
    private var eyeParts:[SCNNode]=[]
    private var brows:[SCNNode]=[]
    private let exportAppearance: GolferAppearance

    init(shirt: UIColor, authored: Bool = false, appearance: GolferAppearance? = nil) {
        exportAppearance = appearance ?? .preset(.cove)
        node.name = "premiumGolfer"
        let shadowMaterial=SCNMaterial()
        shadowMaterial.lightingModel = .constant
        shadowMaterial.diffuse.contents = UIColor.black.withAlphaComponent(0.22)
        shadowMaterial.blendMode = .alpha
        shadowMaterial.writesToDepthBuffer = false
        shadowMaterial.shaderModifiers = [.surface: """
        #pragma transparent
        #pragma body
        float distance = length((_surface.diffuseTexcoord - float2(0.5)) * 2.0);
        _surface.diffuse.a *= 1.0 - smoothstep(0.12, 1.0, distance);
        """]
        contactShadow.geometry = SCNPlane(width:2.7,height:2.0)
        contactShadow.geometry?.materials = [shadowMaterial]
        contactShadow.eulerAngles.x = -.pi/2
        contactShadow.castsShadow = false
        contactShadow.name = "golferContactShadow"
        node.addChildNode(contactShadow)
        let shirt = appearance?.shirtColor ?? shirt
        let navy = appearance?.trousersColor ?? UIColor(red: 0.12, green: 0.18, blue: 0.27, alpha: 1)
        let skin = appearance?.skinColor ?? UIColor(red: 0.91, green: 0.69, blue: 0.49, alpha: 1)
        let ivory = appearance?.accentColor ?? UIColor(red: 0.98, green: 0.97, blue: 0.91, alpha: 1)
        let hair = appearance?.hairColor ?? UIColor(red: 0.19, green: 0.12, blue: 0.09, alpha: 1)
        authoredSkin = authored ? ResortGolferSkin(shirt: shirt, skin: skin, trousers: navy, hair: hair) : nil
        sunwardSkin = authored ? nil : SunwardGolferSkin(appearance: appearance ?? .preset(.cove))
        skinMesh = authoredSkin == nil && sunwardSkin == nil ? GolferSkin(shirt: shirt, trousers: navy, skin: skin) : nil
        node.addChildNode(body)
        if let meshNode = authoredSkin?.node ?? sunwardSkin?.node ?? skinMesh?.node { body.addChildNode(meshNode) }
        torso.name = "tailoredPolo"
        body.addChildNode(torso)
        let placket = Self.part(SCNBox(width: 0.045, height: 0.20, length: 0.115, chamferRadius: 0.018), shirt)
        placket.position = SCNVector3(0.49, 0.79, 0)
        torso.addChildNode(placket)
        for y in [0.75, 0.82] {
            let button = Self.part(SCNSphere(radius: 0.027), ivory)
            button.position = SCNVector3(0.525, y, 0)
            button.scale.x = 0.4
            torso.addChildNode(button)
        }
        let crest = Self.part(SCNSphere(radius: 0.075), ivory)
        crest.position = SCNVector3(0.53, 0.70, -0.33)
        crest.scale = SCNVector3(0.15, 0.5, 1)
        torso.addChildNode(crest)
        pelvis.scale = SCNVector3(0.75, 0.65, 1.18)
        body.addChildNode(pelvis)
        collar.geometry = SCNTorus(ringRadius: 0.255, pipeRadius: 0.045)
        collar.geometry?.materials = [Self.material(shirt)]
        body.addChildNode(collar)
        // Two sewn collar points, rather than a thick floating neck ring.
        for side: Float in [-1, 1] {
            let path = UIBezierPath()
            path.move(to: CGPoint(x:0,y:0));path.addLine(to:CGPoint(x:0.31,y:0.035))
            path.addLine(to:CGPoint(x:0.26,y:-0.32));path.addLine(to:CGPoint(x:0.035,y:-0.21));path.close()
            let flap = SCNShape(path:path,extrusionDepth:0.045);flap.chamferRadius=0.018
            let tip = Self.part(flap, shirt)
            tip.simdPosition = simd_float3(0.30, -0.01, side * 0.025)
            tip.simdOrientation = simd_quatf(angle:0.8,axis:simd_float3(0,0,1)) *
                simd_quatf(angle:side * .pi/2,axis:simd_float3(0,1,0))
            tip.name = "foldedPoloCollar"
            collar.addChildNode(tip)
        }
        head.name = "golferFace"
        if sunwardSkin != nil {
            head.scale = SCNVector3(1.52, 1.50, 1.56)
            // Mesh-to-joint offset, not a change to the tracked skull anchor.
            head.pivot = SCNMatrix4MakeTranslation(0,-0.19/1.5,0)
        }
        head.geometry = Self.faceGeometry()
        let complexion=Self.material(skin)
        complexion.roughness.contents=0.62
        // Subtle warm cheeks are painted in mesh UV space, independent of chosen skin tone.
        complexion.shaderModifiers = [.surface: """
        #pragma body
        float2 uv = _surface.diffuseTexcoord;
        float side = min(abs(uv.x - 0.09), abs(uv.x - 0.91));
        float cheek = exp(-pow(side / 0.042, 2.0) - pow((uv.y - 0.40) / 0.095, 2.0));
        _surface.diffuse.rgb = mix(_surface.diffuse.rgb, _surface.diffuse.rgb * float3(1.08, 0.83, 0.80), cheek * 0.45);
        """]
        head.geometry?.materials = [complexion]
        body.addChildNode(head)
        for side: Float in [-1, 1] {
            let ear = Self.part(SCNSphere(radius: 0.16), skin)
            ear.simdPosition = simd_float3(-0.02, -0.02, side * 0.59)
            ear.scale = SCNVector3(0.7, 1.2, 0.65)
            head.addChildNode(ear)
            let earInset=Self.part(SCNSphere(radius:0.105),skin)
            earInset.simdPosition=simd_float3(0.045,-0.02,side*0.645)
            earInset.scale=SCNVector3(0.45,1.12,0.25)
            earInset.geometry?.firstMaterial?.multiply.contents=UIColor(red:1,green:0.83,blue:0.79,alpha:1)
            head.addChildNode(earInset)
            let eye = Self.part(SCNSphere(radius: 0.153), UIColor(white: 0.97, alpha: 1))
            eye.simdPosition = simd_float3(0.535, 0.075, side * 0.235)
            eye.scale = SCNVector3(0.25, 1.13, 1.02)
            eye.geometry?.firstMaterial?.roughness.contents=0.28
            head.addChildNode(eye)
            eyeParts.append(eye)
            let pupil = Self.part(SCNSphere(radius: 0.105), UIColor(red: 0.20, green: 0.10, blue: 0.055, alpha: 1))
            pupil.simdPosition = simd_float3(0.570, 0.068, side * 0.224)
            pupil.scale = SCNVector3(0.16, 1.1, 0.93)
            pupil.geometry?.firstMaterial?.roughness.contents=0.2
            head.addChildNode(pupil)
            eyeParts.append(pupil)
            let glint = Self.part(SCNSphere(radius: 0.019), .white)
            glint.simdPosition = simd_float3(0.588, 0.10, side * 0.224 - 0.025)
            glint.scale.x=0.3
            head.addChildNode(glint)
            eyeParts.append(glint)
            let brow = Self.part(SCNCapsule(capRadius: 0.037, height: 0.23), hair)
            brow.simdPosition = simd_float3(0.55, 0.235, side * 0.23)
            brow.eulerAngles.x = .pi / 2 + side * 0.1
            head.addChildNode(brow)
            brows.append(brow)
        }
        let nose = Self.part(SCNSphere(radius: 0.145), skin)
        nose.position = SCNVector3(0.605, -0.06, 0)
        nose.scale = SCNVector3(0.80, 0.77, 0.82)
        head.addChildNode(nose)
        for i in 0..<12 {
            func smile(_ t: Float) -> simd_float3 { simd_float3(0.57, -0.27 + 0.09 * pow(2 * t - 1, 2), (t - 0.5) * 0.34) }
            let line = Self.part(SCNCylinder(radius: 0.012, height: 1), hair)
            line.pivot = SCNMatrix4MakeTranslation(0, -0.5, 0)
            head.addChildNode(line)
            place(line, from: smile(Float(i) / 12), to: smile(Float(i + 1) / 12))
        }
        let hairBack = Self.part(SCNSphere(radius: 0.635), hair)
        hairBack.position = SCNVector3(-0.12, 0.12, 0)
        hairBack.scale = SCNVector3(0.81, 0.86, 0.97)
        hairBack.name = "golferHair"
        head.addChildNode(hairBack)
        let cap = Self.part(Self.profileGeometry([
            (0.26,0.63,0.62,-0.025),(0.40,0.64,0.63,-0.025),
            (0.56,0.58,0.57,-0.04),(0.70,0.40,0.40,-0.05),(0.77,0.01,0.01,-0.05)]), ivory)
        cap.name = "golfCap"
        if appearance?.headwear == .visor {
            cap.geometry = SCNTorus(ringRadius:0.59,pipeRadius:0.045)
            cap.geometry?.materials = [Self.material(ivory)]
            cap.position.y=0.28
            let crown=Self.part(Self.profileGeometry([
                (0.27,0.60,0.59,-0.025),(0.43,0.62,0.61,-0.025),
                (0.61,0.51,0.52,-0.04),(0.75,0.23,0.30,-0.05),(0.79,0.01,0.01,-0.05)]),hair)
            crown.name="visorHairCrown"
            crown.geometry?.firstMaterial?.shaderModifiers = [.surface: """
            #pragma body
            float strand = sin(_surface.diffuseTexcoord.x * 75.3982 + _surface.diffuseTexcoord.y * 4.0);
            _surface.diffuse.rgb *= 0.95 + 0.05 * strand;
            """]
            head.addChildNode(crown)
        }
        head.addChildNode(cap)
        if appearance?.headwear != .visor {
            let topButton=Self.part(SCNSphere(radius:0.055),ivory)
            topButton.name = "capTopButton"
            topButton.position=SCNVector3(-0.05,0.775,0);topButton.scale.y=0.55
            head.addChildNode(topButton)
        }
        let brim = Self.part(SCNSphere(radius: 1), ivory)
        brim.scale = SCNVector3(0.46, 0.042, 0.62)
        brim.position = SCNVector3(0.45, 0.32, 0)
        brim.name = "capBrim"
        brim.eulerAngles.z = -0.08
        head.addChildNode(brim)
        let badge = Self.part(SCNSphere(radius: 0.10), shirt)
        badge.position = SCNVector3(0.59, 0.48, 0)
        badge.name = "capBadge"
        badge.scale = SCNVector3(0.17, 1, 1)
        head.addChildNode(badge)
        for index in 0..<2 {
            let handColor = index == 0 ? UIColor(white: 0.95, alpha: 1) : skin
            let hand = Self.part(SCNSphere(radius: 0.17), handColor)
            hand.name = index == 0 ? "leadGlove" : "trailHand"
            hand.scale = SCNVector3(1.12, 1.45, 1.16)
            for finger in 0..<4 {
                let curl = Self.part(SCNCapsule(capRadius: 0.043, height: 0.20), handColor)
                curl.simdPosition = simd_float3(0.07, -0.02, Float(finger) * 0.07 - 0.105)
                curl.simdOrientation = simd_quatf(angle: 0.55, axis: simd_float3(0,0,1))
                hand.addChildNode(curl)
            }
            let thumb = Self.part(SCNCapsule(capRadius: 0.055, height: 0.22), handColor)
            thumb.simdPosition = simd_float3(0.13, 0.065, index == 0 ? 0.12 : -0.12)
            thumb.simdOrientation = simd_quatf(angle: 0.7, axis: simd_float3(1,0,0))
            hand.addChildNode(thumb)
            if index == 0 {
                let tab=Self.part(SCNBox(width:0.045,height:0.09,length:0.16,chamferRadius:0.022),navy)
                tab.position=SCNVector3(-0.135,0.07,0);hand.addChildNode(tab)
            }
            body.addChildNode(hand)
            hands.append(hand)
            let foot = SCNNode()
            let shoe = Self.part(SCNBox(width: 0.78, height: 0.3, length: 0.47, chamferRadius: 0.14), ivory)
            shoe.position.y = 0.03
            foot.addChildNode(shoe)
            let sole = Self.part(SCNBox(width: 0.82, height: 0.09, length: 0.49, chamferRadius: 0.04), navy)
            sole.position.y = -0.1
            foot.addChildNode(sole)
            let saddle = Self.part(SCNBox(width: 0.23, height: 0.305, length: 0.475, chamferRadius: 0.065), navy)
            saddle.position.x = -0.03
            foot.addChildNode(saddle)
            for x in [-0.08, 0.0, 0.08] {
                let lace = Self.part(SCNBox(width: 0.028, height: 0.015, length: 0.23, chamferRadius: 0.007), ivory)
                lace.position = SCNVector3(x, 0.192, 0)
                foot.addChildNode(lace)
            }
            body.addChildNode(foot)
            foot.scale=SCNVector3(1.28,1.25,1.28)
            feet.append(foot)
        }
        shaft.geometry = SCNCylinder(radius: 0.018, height: 1)
        shaft.geometry?.materials = [Self.material(UIColor(white: 0.8, alpha: 1), metal: 0.85)]
        shaft.pivot = SCNMatrix4MakeTranslation(0, -0.5, 0)
        body.addChildNode(shaft)
        handle.geometry = SCNCylinder(radius: 0.038, height: 1)
        handle.geometry?.materials = [Self.material(navy)]
        handle.pivot = SCNMatrix4MakeTranslation(0, -0.5, 0)
        body.addChildNode(handle)
        clubHead.name = "clubHead"
        clubHead.geometry = SCNBox(width: 0.30, height: 0.18, length: 0.38, chamferRadius: 0.055)
        clubHead.geometry?.materials = [Self.material(UIColor(white: 0.68, alpha: 1), metal: 0.75)]
        body.addChildNode(clubHead)
        let face = Self.part(SCNBox(width: 0.29, height: 0.16, length: 0.015, chamferRadius: 0.008), ivory)
        face.name = "strikingFace"
        face.position.z = -0.195
        clubHead.addChildNode(face)
        if authoredSkin != nil {
            head.geometry = nil
            head.simdScale = simd_float3(repeating:0.62)
            collar.simdScale = simd_float3(repeating:0.65)
            for child in head.childNodes where !["golfCap", "capBrim", "capBadge", "golferHair"].contains(child.name ?? "") { child.isHidden = true }
            hands.forEach { $0.isHidden = true }
        }
        apply(AvatarAnimations.address)
    }

    /// Distinct silhouettes from the driver/iron/putt reference set. The contact point
    /// and shaft endpoints do not change with cosmetic club-head geometry.
    func setClub(_ club:GolfClub) {
        guard displayedClub != club else { return };displayedClub=club
        let geometry:SCNGeometry
        if club == .putter {
            geometry=SCNBox(width:0.48,height:0.13,length:0.20,chamferRadius:0.045)
        } else if club == .driver || club == .wood3 {
            let shape=SCNSphere(radius:1);shape.segmentCount=32
            geometry=shape
        } else {
            geometry=SCNBox(width:0.33,height:0.20,length:0.12,chamferRadius:0.035)
        }
        geometry.materials=[Self.material(club == .driver || club == .wood3 ? UIColor(white:0.12,alpha:1) : UIColor(white:0.65,alpha:1),metal:0.7)]
        clubHead.geometry=geometry
        clubHead.simdScale = club == .driver || club == .wood3 ? simd_float3(0.24,0.15,0.22) : simd_float3(repeating:1)
        // The original cuboid face cannot be scaled with the driver sphere.
        clubHead.childNode(withName:"strikingFace",recursively:false)?.isHidden=true
    }

    func express(_ reaction:AvatarAnimations.Reaction?,time:Double,reduceMotion:Bool) {
        let blink = !reduceMotion && time.truncatingRemainder(dividingBy:4.3)<0.12
        for (index,part) in eyeParts.enumerated() {
            let normal:Float=index % 3 == 0 ? 1.13 : index % 3 == 1 ? 1.1 : 1
            part.simdScale.y=blink ? 0.06 : normal
        }
        for (index,brow) in brows.enumerated() {
            let side:Float=index == 0 ? -1 : 1
            let tilt:Float = reaction == .holed || reaction == .pure ? 0.20 : reaction == .bad || reaction == .disaster ? -0.22 : 0.10
            brow.eulerAngles.x = .pi/2 + side*tilt
            brow.position.y = reaction == .holed ? 0.27 : 0.235
        }
    }

    func setMirrored(_ mirrored: Bool) {
        self.mirrored = mirrored
        node.simdScale = simd_float3(authoredSkin == nil && mirrored ? -1 : 1, 1, 1)
    }

    func setContactGroundHeight(_ height: Float) {
        contactShadow.position.y = height + 0.018
    }

    #if DEBUG
    /// Offline conversion captures existing authored accessories, then binds all
    /// of them to the same USD skeleton as the continuous body mesh.
    private var nativeAccessoryNodes: [(String, SCNNode)] {
        [("head", head), ("poloDetails", torso), ("collar", collar),
         ("leftHand", hands[0]), ("rightHand", hands[1]),
         ("leftFoot", feet[0]), ("rightFoot", feet[1]),
         ("shaft", shaft), ("handle", handle), ("clubHead", clubHead)]
    }

    func nativeExportAccessories() -> SCNScene {
        let scene = SCNScene()
        for (name, original) in nativeAccessoryNodes {
            let copy = original.clone()
            copy.name = name
            // USD has no SceneKit pivot property. Bake the geometry-space pivot
            // before splitting materials; otherwise the grip cylinder is centered
            // on the hands and the shaft stops half a club length before the head.
            copy.simdPivot = matrix_identity_float4x4
            copy.simdTransform = nativeJointWorldTransform(original) * simd_inverse(original.simdPivot)
            scene.rootNode.addChildNode(copy)
        }
        var nodes: [SCNNode] = []
        scene.rootNode.enumerateChildNodes { node, _ in nodes.append(node) }
        for node in nodes {
            guard let source = node.geometry, !source.materials.isEmpty else { continue }
            // Force primitive/SCNShape tessellation before USD's material pass.
            let geometry = SCNGeometry(mdlMesh: MDLMesh(scnGeometry: source))
            geometry.materials = (0..<geometry.elementCount).map { index in
                let material = source.materials[index % source.materials.count].copy() as! SCNMaterial
                material.shaderModifiers = nil
                let diffuse = material.diffuse.contents
                let color: UIColor?
                if let value = diffuse as? UIColor { color = value }
                else if let value = diffuse, CFGetTypeID(value as CFTypeRef) == CGColor.typeID {
                    color = UIColor(cgColor: value as! CGColor)
                } else { color = nil }
                if let color {
                    let roles: [(UIColor, String)] = [(exportAppearance.shirtColor, "shirt"),
                        (exportAppearance.trousersColor, "trousers"), (exportAppearance.skinColor, "skin"),
                        (exportAppearance.hairColor, "hair"), (exportAppearance.accentColor, "ivory")]
                    func rgba(_ value: UIColor) -> SIMD4<Double> {
                        var r: CGFloat = 0, g: CGFloat = 0, b: CGFloat = 0, a: CGFloat = 0
                        value.getRed(&r, green: &g, blue: &b, alpha: &a)
                        return SIMD4(Double(r), Double(g), Double(b), Double(a))
                    }
                    if let role = roles.first(where: { simd_length(rgba($0.0) - rgba(color)) < 0.001 }) {
                        material.name = role.1
                    }
                }
                if ["golfCap", "capTopButton", "capBadge", "visorHairCrown"].contains(node.name ?? "") {
                    material.name = (exportAppearance.headwear == .cap ? "cap_" : "visor_") + (material.name ?? "ivory")
                }
                return material
            }
            // The SceneKit USD exporter retains stale ModelIO material bindings
            // on tessellated geometry. Fresh single-material meshes make both
            // assignment and semantic tint/headwear roles explicit.
            node.geometry = nil
            for (index, element) in geometry.elements.enumerated() {
                let material = geometry.materials[index]
                let part = SCNGeometry(sources: geometry.sources, elements: [element])
                part.materials = [material]
                let child = SCNNode(geometry: part)
                child.name = "nativeMaterial_" + (material.name ?? "accessory") + "_part_\(index)"
                node.addChildNode(child)
            }
        }
        return scene
    }

    func nativeExportTransforms() -> [String: [Float]] {
        func values(_ m: simd_float4x4) -> [Float] {
            [m.columns.0, m.columns.1, m.columns.2, m.columns.3].flatMap { [$0.x, $0.y, $0.z, $0.w] }
        }
        var result = Dictionary(uniqueKeysWithValues: nativeAccessoryNodes.map { ($0.0, values(nativeJointWorldTransform($0.1))) })
        for (index, matrix) in (sunwardSkin?.nativeExportBones ?? []).enumerated() {
            result["link_\(index)"] = values(matrix)
        }
        return result
    }

    private func nativeJointWorldTransform(_ node: SCNNode) -> simd_float4x4 {
        // SceneKit's world-transform accessor incorporates its geometry pivot.
        // Skeleton joints need the authored position/orientation, while the pivot
        // is baked once into vertices by nativeExportAccessories().
        var local = simd_float4x4(node.simdOrientation)
        local.columns.0 *= node.simdScale.x
        local.columns.1 *= node.simdScale.y
        local.columns.2 *= node.simdScale.z
        local.columns.3 = SIMD4(node.simdPosition, 1)
        return (node.parent?.simdWorldTransform ?? matrix_identity_float4x4) * local
    }
    #endif

    func apply(_ input: BodyPose3D) {
        let pose = authoredSkin != nil && mirrored ? input.anatomicallyMirrored : input
        shaft.isHidden = !pose.clubVisible
        handle.isHidden = !pose.clubVisible
        clubHead.isHidden = !pose.clubVisible
        body.simdOrientation = pose.lean
        if let authoredSkin { authoredSkin.apply(pose) }
        else if let sunwardSkin { sunwardSkin.apply(pose) }
        else { skinMesh?.apply(pose) }
        let up = pose[.neck] - pose[.root]
        let shoulderAxis = pose[.rightShoulder] - pose[.leftShoulder]
        torso.simdPosition = pose[.root]
        torso.simdOrientation = Self.clothingOrientation(up: up, right: shoulderAxis)
        torso.simdScale = simd_float3(1, max(0.1, simd_length(up)), 1)
        pelvis.simdPosition = pose[.root]
        collar.simdPosition = pose[.neck] - simd_normalize(up + simd_float3(0, 0.0001, 0)) * 0.06
        collar.simdOrientation = torso.simdOrientation
        head.simdPosition = pose[.nose]
        // The old head frame inherited shoulder-label reversals from the mirrored camera,
        // turning the face 180 degrees. Eyes/nose/cap brim all share this ball-facing frame.
        head.simdOrientation = authoredSkin == nil ? Self.headOrientation(for: pose) : torso.simdOrientation
        if authoredSkin == nil,pose[.rightAnkle].y>0.14,pose.handCenter.z < -0.5 {
            let release=min(1,max(0,(pose[.rightAnkle].y-0.14)/0.25))
            let watch=simd_quatf(angle:0.75,axis:simd_float3(0,1,0))*simd_quatf(angle:0.04,axis:simd_float3(0,0,1))
            head.simdOrientation=simd_slerp(head.simdOrientation,watch,release)
        }
        hands[0].simdPosition = pose[.leftWrist]
        hands[1].simdPosition = pose[.rightWrist]
        for hand in hands {
            hand.simdOrientation = Self.align(pose.clubVisible ? pose.clubDirection : simd_float3(0,-1,0))
        }
        let facing: Float = authoredSkin != nil && mirrored ? -1 : 1
        feet[0].simdPosition = pose[.leftAnkle] + simd_float3(0.22*facing, -0.01, 0)
        feet[1].simdPosition = pose[.rightAnkle] + simd_float3(0.22*facing, -0.01, 0)
        feet.forEach { $0.eulerAngles.y = facing < 0 ? .pi : 0 }
        let ankles=(pose[.leftAnkle]+pose[.rightAnkle])/2
        contactShadow.simdPosition=simd_float3(ankles.x+0.1,max(0.015,min(pose[.leftAnkle].y,pose[.rightAnkle].y)-0.105),ankles.z)
        // A lifted trail heel should not leave a flat, floating shoe. Presentation only;
        // the measured grip and virtual club endpoints remain unchanged.
        for (index, ankle) in [BodyJoint.leftAnkle, .rightAnkle].enumerated() {
            feet[index].eulerAngles.z = -min(0.65, max(0, pose[ankle].y - 0.12) * 2.2)
        }
        let grip = pose.clubDropped ? simd_float3(1.1, 0.08, -1.1) : pose.clubGrip
        let direction = pose.clubDropped ? simd_normalize(simd_float3(0.35, 0, 1)) : pose.clubDirection
        let end = pose.clubDropped ? grip + direction * AvatarSize.clubLength : pose.clubHead
        let orientation = ClubGeometry.headOrientation(shaftUp: grip - end)
        // The tracked endpoint is the contact point at the face, not the solid head's
        // center. Put the heel behind it so addressing the ball doesn't engulf it.
        let depth: Float = displayedClub == .driver || displayedClub == .wood3 ? 0.22 :
            displayedClub == .putter ? 0.10 : displayedClub == nil ? 0.195 : 0.06
        let heel = end + orientation.act(simd_float3(0, 0, depth + Float(AvatarSize.visibleBallRadius) / AvatarSize.courseScale))
        place(shaft, from: grip, to: heel)
        place(handle, from: grip, to: simd_mix(grip, heel, simd_float3(repeating: 0.17)))
        clubHead.simdPosition = heel
        clubHead.simdOrientation = orientation
    }

    static func headOrientation(for pose: BodyPose3D) -> simd_quatf {
        let toBall = AvatarSize.ball - pose[.nose]
        let yaw = max(-Float.pi / 3, min(Float.pi / 3, atan2(toBall.z, max(0.2, toBall.x))))
        return simd_quatf(angle: -yaw, axis: simd_float3(0, 1, 0)) *
            simd_quatf(angle: -0.12, axis: simd_float3(0, 0, 1))
    }

    /// Camera shoulder labels can reverse the local chest frame. Small clothing details
    /// must remain on the ball-facing side, just like the face, without changing the pose.
    static func clothingOrientation(up: simd_float3, right: simd_float3) -> simd_quatf {
        let frame = orientation(up: up, right: right)
        return frame.act(simd_float3(1,0,0)).x < 0
            ? frame * simd_quatf(angle: .pi, axis: simd_float3(0,1,0)) : frame
    }

    /// Shaped jaw and cheeks, rather than the previous perfect spherical head.
    private static func faceGeometry() -> SCNGeometry {
        let profile: [(Float, Float, Float, Float)] = [
            (-0.60,0.02,0.02,-0.03),(-0.53,0.30,0.29,0.015),
            (-0.39,0.48,0.46,0.035),(-0.20,0.58,0.54,0.025),
            (0.02,0.61,0.57,0),(0.24,0.58,0.56,-0.01),
            (0.44,0.49,0.49,-0.02),(0.58,0.30,0.31,-0.03),(0.64,0.01,0.01,-0.03)]
        return profileGeometry(profile)
    }

    private static func profileGeometry(_ control: [(Float,Float,Float,Float)]) -> SCNGeometry {
        // Cubic loft, not a faceted stack of rings: shared by sculpted face and cap.
        func vector(_ p:(Float,Float,Float,Float))->simd_float4 { simd_float4(p.0,p.1,p.2,p.3) }
        var profile:[(Float,Float,Float,Float)]=[]
        for row in 0..<control.count-1 {
            let a=vector(control[max(0,row-1)]),b=vector(control[row])
            let c=vector(control[row+1]),d=vector(control[min(control.count-1,row+2)])
            for step in 0..<6 {
                let t=Float(step)/6
                let p=(2*b+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t)*0.5
                profile.append((p.x,max(0.001,p.y),max(0.001,p.z),p.w))
            }
        }
        profile.append(control.last!)
        let columns = 48
        var vertices: [SCNVector3] = [], normals: [SCNVector3] = [], indices: [Int32] = []
        var uv:[CGPoint]=[]
        for (row,p) in profile.enumerated() {
            let previous = profile[max(0,row-1)], next = profile[min(profile.count-1,row+1)]
            let dy = max(0.001,next.0-previous.0)
            for column in 0...columns {
                let angle = Float(column) / Float(columns) * 2 * .pi
                let c = cos(angle), s = sin(angle)
                vertices.append(SCNVector3(p.2*c+p.3,p.0,p.1*s))
                uv.append(CGPoint(x:Double(column)/Double(columns),y:Double(row)/Double(profile.count-1)))
                let slope = ((next.2-previous.2)*c*c + (next.1-previous.1)*s*s)/dy
                normals.append(SCNVector3(simd_normalize(simd_float3(c,-slope,s))))
            }
        }
        for row in 0..<profile.count-1 { for col in 0..<columns {
            let a=Int32(row*(columns+1)+col), b=a+Int32(columns+1)
            indices += [a,b,a+1,a+1,b,b+1]
        } }
        return SCNGeometry(sources:[.init(vertices:vertices),.init(normals:normals),.init(textureCoordinates:uv)],
            elements:[SCNGeometryElement(indices:indices,primitiveType:.triangles)])
    }

    private static func material(_ color: UIColor, metal: CGFloat = 0) -> SCNMaterial {
        let result = SCNMaterial()
        result.lightingModel = .physicallyBased
        result.diffuse.contents = color
        result.roughness.contents = metal > 0 ? 0.28 : 0.76
        result.metalness.contents = metal
        return result
    }

    private static func part(_ geometry: SCNGeometry, _ color: UIColor, metal: CGFloat = 0) -> SCNNode {
        geometry.materials = [material(color, metal: metal)]
        return SCNNode(geometry: geometry)
    }

    private static func poloGeometry() -> SCNGeometry {
        let rings: [(Float, Float, Float)] = [(0, 0.33, 0.50), (0.06, 0.41, 0.60),
            (0.42, 0.44, 0.67), (0.78, 0.44, 0.77), (0.90, 0.39, 0.71), (1, 0.22, 0.28)]
        let segments = 32
        var vertices: [SCNVector3] = [], normals: [SCNVector3] = [], indices: [Int32] = []
        for (y, depth, width) in rings {
            for i in 0...segments {
                let angle = Float(i) / Float(segments) * .pi * 2
                vertices.append(SCNVector3(cos(angle) * depth, y, sin(angle) * width))
                normals.append(SCNVector3(simd_normalize(simd_float3(cos(angle) / depth, 0.1, sin(angle) / width))))
            }
        }
        for ring in 0..<rings.count - 1 {
            for i in 0..<segments {
                let a = Int32(ring * (segments + 1) + i), b = a + Int32(segments + 1)
                indices += [a, b, a + 1, a + 1, b, b + 1]
            }
        }
        return SCNGeometry(sources: [.init(vertices: vertices), .init(normals: normals)],
                           elements: [SCNGeometryElement(indices: indices, primitiveType: .triangles)])
    }

    private func place(_ bone: SCNNode, from start: simd_float3, to end: simd_float3) {
        let length = simd_length(end - start)
        bone.simdPosition = start
        bone.simdScale = simd_float3(1, max(0.05, length), 1)
        if length > 0.001 { bone.simdOrientation = Self.align((end - start) / length) }
    }

    private static func orientation(up: simd_float3, right: simd_float3) -> simd_quatf {
        guard simd_length(up) > 0.001, simd_length(right) > 0.001 else { return simd_quatf() }
        let y = simd_normalize(up), cross = simd_cross(y, right)
        guard simd_length(cross) > 0.001 else { return align(y) }
        let x = simd_normalize(cross), z = simd_normalize(simd_cross(x, y))
        return simd_quatf(simd_float3x3(columns: (x, y, z)))
    }

    private static func align(_ direction: simd_float3) -> simd_quatf {
        guard simd_length(direction) > 0.001 else { return simd_quatf() }
        let unit = simd_normalize(direction)
        if simd_dot(unit, simd_float3(0, -1, 0)) > 0.9999 { return simd_quatf(angle: .pi, axis: simd_float3(1, 0, 0)) }
        return simd_quatf(from: simd_float3(0, 1, 0), to: unit)
    }
}
