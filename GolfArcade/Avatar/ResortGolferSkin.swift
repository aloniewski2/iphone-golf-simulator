import SceneKit
import UIKit
import simd

/// Authored CC0 topology with original golf palette and our pose retargeting.
/// Import is offline; only the small validated mesh contract ships in the application.
@MainActor
final class ResortGolferSkin {
    struct Asset: Decodable {
        struct Bone: Decodable { let name: String; let parent: Int }
        struct Mesh: Decodable {
            let positions: [Float], normals: [Float], uv: [Float], weights: [Float]
            let joints: [UInt16], indices: [Int32]
            let material: Int
        }
        let version: Int
        let bones: [Bone], inverseBinds: [Float], meshes: [Mesh]
    }
    private static let asset: Asset? = {
        guard let url = Bundle.main.url(forResource: "ResortGolfer", withExtension: "golfmesh"),
              let data = try? Data(contentsOf: url), let asset = try? JSONDecoder().decode(Asset.self, from: data),
              asset.version == 1, asset.inverseBinds.count == asset.bones.count * 16,
              asset.meshes.allSatisfy({ m in
                  let count = m.positions.count / 3
                  return count > 0 && m.positions.count == count * 3 && m.normals.count == count * 3 &&
                    m.weights.count == count * 4 && m.joints.count == count * 4 && m.uv.count == count * 2 &&
                    m.indices.allSatisfy { $0 >= 0 && $0 < count } &&
                    m.joints.allSatisfy { $0 < asset.bones.count } && m.positions.allSatisfy(\.isFinite)
              }) else { return nil }
        return asset
    }()

    let node = SCNNode()
    private var bones: [SCNNode] = []
    private var rests: [simd_float4x4] = []
    private var names: [String: Int] = [:]
    private let asset: Asset
    private let conversion: simd_float4x4

    init?(shirt: UIColor, skin: UIColor, trousers: UIColor = UIColor(red:0.10,green:0.16,blue:0.23,alpha:1), hair: UIColor = UIColor(red:0.12,green:0.075,blue:0.04,alpha:1)) {
        guard let asset = Self.asset else { return nil }
        self.asset = asset
        var conversion = simd_float4x4(simd_quatf(angle: .pi / 2, axis: simd_float3(0, 1, 0)))
        conversion.columns.0 *= 3.15; conversion.columns.1 *= 3.15; conversion.columns.2 *= 3.15
        conversion.columns.3.y = 0.03
        self.conversion = conversion
        node.name = "authoredResortGolfer"
        for (index, definition) in asset.bones.enumerated() {
            let start = index * 16
            func column(_ c: Int) -> simd_float4 {
                let p = start + c * 4
                return simd_float4(asset.inverseBinds[p], asset.inverseBinds[p+1], asset.inverseBinds[p+2], asset.inverseBinds[p+3])
            }
            let rest = conversion * simd_inverse(simd_float4x4(columns: (column(0),column(1),column(2),column(3))))
            let bone = SCNNode(); bone.name = definition.name; bone.simdTransform = rest
            node.addChildNode(bone); bones.append(bone); rests.append(rest); names[definition.name] = index
        }
        for mesh in asset.meshes {
            var vertices: [SCNVector3] = [], normals: [SCNVector3] = [], colors: [simd_float4] = []
            var texcoords: [CGPoint] = []
            func rgba(_ c: UIColor) -> simd_float4 {
                var r: CGFloat=0, g: CGFloat=0, b: CGFloat=0, a: CGFloat=0
                c.getRed(&r, green:&g, blue:&b, alpha:&a)
                return simd_float4(Float(r),Float(g),Float(b),Float(a))
            }
            let shirtColor = rgba(shirt), skinColor = rgba(skin)
            let navy = rgba(trousers), ivory = simd_float4(0.94,0.94,0.87,1)
            for i in 0..<(mesh.positions.count / 3) {
                let raw = simd_float4(mesh.positions[i*3],mesh.positions[i*3+1],mesh.positions[i*3+2],1)
                let p = conversion * raw
                vertices.append(SCNVector3(p.x,p.y,p.z))
                let normal = conversion * simd_float4(mesh.normals[i*3],mesh.normals[i*3+1],mesh.normals[i*3+2],0)
                normals.append(SCNVector3(simd_normalize(simd_float3(normal.x,normal.y,normal.z))))
                texcoords.append(CGPoint(x: Double(mesh.uv[i*2]), y: Double(1-mesh.uv[i*2+1])))
                // Crisp garment regions in the asset's rest frame; no skin-colour blend at the waist.
                let color: simd_float4
                let dominant = (0..<4).max { mesh.weights[i*4+$0] < mesh.weights[i*4+$1] } ?? 0
                let boneName = asset.bones[Int(mesh.joints[i*4+dominant])].name
                if mesh.material == 0 { color = rgba(hair) }
                else if mesh.material == 1 { color = simd_float4(repeating: 1) }
                else if boneName.hasPrefix("foot") || boneName.hasPrefix("ball") { color = ivory }
                else if boneName.hasPrefix("thigh") || boneName.hasPrefix("calf") || boneName == "pelvis" { color = navy }
                else if boneName.hasPrefix("spine") || boneName.hasPrefix("clavicle") || boneName.hasPrefix("upperarm") { color = shirtColor }
                else { color = skinColor }
                colors.append(color)
            }
            let material = SCNMaterial(); material.lightingModel = .physicallyBased
            material.roughness.contents = 0.78; material.metalness.contents = 0
            if mesh.material == 1 { material.diffuse.contents = UIImage(named: "GolferEyes") }
            else { material.diffuse.contents = UIColor.white }
            let colorData = colors.withUnsafeBytes { Data($0) }
            let geometry = SCNGeometry(sources: [.init(vertices:vertices),.init(normals:normals),.init(textureCoordinates:texcoords),
                SCNGeometrySource(data:colorData, semantic:.color, vectorCount:colors.count, usesFloatComponents:true,
                    componentsPerVector:4, bytesPerComponent:4, dataOffset:0, dataStride:16)],
                elements:[SCNGeometryElement(indices:mesh.indices, primitiveType:.triangles)])
            geometry.materials = [material]
            let model = SCNNode(geometry:geometry)
            let weightData = mesh.weights.withUnsafeBytes { Data($0) }, jointData = mesh.joints.withUnsafeBytes { Data($0) }
            let weights = SCNGeometrySource(data:weightData,semantic:.boneWeights,vectorCount:vertices.count,
                usesFloatComponents:true,componentsPerVector:4,bytesPerComponent:4,dataOffset:0,dataStride:16)
            let indices = SCNGeometrySource(data:jointData,semantic:.boneIndices,vectorCount:vertices.count,
                usesFloatComponents:false,componentsPerVector:4,bytesPerComponent:2,dataOffset:0,dataStride:8)
            model.skinner = SCNSkinner(baseGeometry:geometry,bones:bones,
                boneInverseBindTransforms:rests.map { NSValue(scnMatrix4:SCNMatrix4(simd_inverse($0))) },
                boneWeights:weights,boneIndices:indices)
            model.skinner?.skeleton = node
            node.addChildNode(model)
        }
    }

    private func restPoint(_ name: String) -> simd_float3 {
        guard let index = names[name] else { return .zero }
        let p = rests[index].columns.3
        return simd_float3(p.x,p.y,p.z)
    }

    func apply(_ pose: BodyPose3D) {
        var deltas: [Int:simd_float4x4] = [:]
        let bodyY=simd_normalize(pose[.neck]-pose[.root]+simd_float3(0,0.0001,0))
        let across=pose[.rightShoulder]-pose[.leftShoulder]
        let bodyZ=simd_normalize(across-bodyY*simd_dot(across,bodyY)+simd_float3(0,0,0.0001))
        let bodyX=simd_normalize(simd_cross(bodyY,bodyZ))
        let bodyRotation=simd_quatf(simd_float3x3(columns:(bodyX,bodyY,simd_cross(bodyX,bodyY))))
        func transform(_ name: String, _ child: String?, _ from: simd_float3, _ to: simd_float3, torso: Bool = false) {
            guard let index = names[name] else { return }
            let start = restPoint(name), end = child.map(restPoint) ?? (start + simd_float3(0,0.3,0))
            let source = end-start, target = to-from
            guard simd_length(source)>0.0001,simd_length(target)>0.0001 else { return }
            let sourceY = simd_normalize(source), targetY = simd_normalize(target)
            // Carry torso twist into limbs before solving their direction. A shortest-arc
            // solve from rest alone turns left-handed shoulders and wrists inside out.
            var rotation = simd_quatf(from:bodyRotation.act(sourceY),to:targetY) * bodyRotation
            if torso {
                let across = pose[.rightShoulder]-pose[.leftShoulder]
                let z = across-targetY*simd_dot(across,targetY)
                if simd_length(z)>0.001 {
                    let targetZ = simd_normalize(z), targetX=simd_normalize(simd_cross(targetY,targetZ))
                    let sourceZ=simd_float3(0,0,1), sourceX=simd_normalize(simd_cross(sourceY,sourceZ))
                    let sourceFrame=simd_float3x3(columns:(sourceX,sourceY,simd_cross(sourceX,sourceY)))
                    let targetFrame=simd_float3x3(columns:(targetX,targetY,simd_cross(targetX,targetY)))
                    rotation=simd_quatf(targetFrame * sourceFrame.transpose)
                }
            }
            let factor=max(0.5,min(1.5,simd_length(target)/simd_length(source)))
            // Stretch only along the bone, preserving body thickness.
            let stretch=matrix_identity_float3x3 + (factor-1) * simd_float3x3(columns:(sourceY*sourceY.x,sourceY*sourceY.y,sourceY*sourceY.z))
            let linear=simd_float3x3(rotation)*stretch
            let translation=from-linear*start
            deltas[index]=simd_float4x4(columns:(simd_float4(linear.columns.0,0),simd_float4(linear.columns.1,0),
                simd_float4(linear.columns.2,0),simd_float4(translation,1)))
        }
        let root=pose[.root], neck=pose[.neck], up=neck-root
        transform("pelvis","spine_01",root,root+up*0.12,torso:true)
        transform("spine_01","spine_02",root+up*0.12,root+up*0.38,torso:true)
        transform("spine_02","spine_03",root+up*0.38,root+up*0.65,torso:true)
        transform("spine_03","neck_01",root+up*0.65,neck,torso:true)
        transform("neck_01","Head",neck,pose[.nose]-simd_float3(0,0.25,0),torso:true)
        transform("Head",nil,pose[.nose]-simd_float3(0,0.25,0),pose[.nose]+simd_float3(0,0.05,0),torso:true)
        let limbs: [(String,BodyJoint,BodyJoint,BodyJoint,BodyJoint,BodyJoint,BodyJoint)] = [
            ("l",BodyJoint.leftShoulder,.leftElbow,.leftWrist,.leftHip,.leftKnee,.leftAnkle),
            ("r",BodyJoint.rightShoulder,.rightElbow,.rightWrist,.rightHip,.rightKnee,.rightAnkle)]
        for (side,s,e,w,h,k,a) in limbs {
            let restClavicle=restPoint("clavicle_"+side)
            let spineDelta=names["spine_03"].flatMap { deltas[$0] } ?? matrix_identity_float4x4
            let clavicle=spineDelta * simd_float4(restClavicle,1)
            transform("clavicle_"+side,"upperarm_"+side,simd_float3(clavicle.x,clavicle.y,clavicle.z),pose[s])
            transform("upperarm_"+side,"lowerarm_"+side,pose[s],pose[e])
            transform("lowerarm_"+side,"hand_"+side,pose[e],pose[w])
            let handDirection = pose.clubVisible ? pose.clubDirection : simd_normalize(pose[w]-pose[e]+simd_float3(0.0001,0,0))
            transform("hand_"+side,"middle_01_"+side,pose[w],pose[w]+handDirection*0.3)
            transform("thigh_"+side,"calf_"+side,pose[h],pose[k])
            transform("calf_"+side,"foot_"+side,pose[k],pose[a])
            let facing: Float = pose[.rightShoulder].z >= pose[.leftShoulder].z ? 1 : -1
            transform("foot_"+side,"ball_"+side,pose[a],pose[a]+simd_float3(0.42*facing,0,0))
        }
        // Unobserved finger/clavicle bones inherit their nearest solved ancestor.
        for i in bones.indices {
            var ancestor=i, depth=0
            while deltas[ancestor] == nil && asset.bones[ancestor].parent >= 0 && depth < asset.bones.count {
                ancestor=asset.bones[ancestor].parent; depth += 1
            }
            bones[i].simdTransform=(deltas[ancestor] ?? matrix_identity_float4x4)*rests[i]
        }
        // Authored grip fallback, not measured finger curl. Preserve the bone hierarchy while
        // bending each phalanx, instead of leaving five open fingers through the shaft.
        if pose.clubVisible {
            for i in bones.indices {
                let name=asset.bones[i].name, parent=asset.bones[i].parent
                guard parent >= 0, ["index_","middle_","ring_","pinky_","thumb_"].contains(where:name.hasPrefix) else { continue }
                let leaf=name.contains("leaf"), thumb=name.hasPrefix("thumb")
                let angle: Float = leaf ? 0 : thumb ? 0.35 : name.contains("_01_") ? 0.55 : 0.9
                let bend=simd_float4x4(simd_quatf(angle:angle,axis:simd_float3(1,0,0)))
                bones[i].simdTransform=bones[parent].simdTransform * simd_inverse(rests[parent]) * rests[i] * bend
            }
        }
    }
}
