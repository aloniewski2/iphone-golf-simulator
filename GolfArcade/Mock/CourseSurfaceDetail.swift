import SceneKit
import UIKit
import simd

/// Actual near-ground geometry and shared terrain shading. No generated movie
/// pixels enter this path: the same playable ground supports every blade.
@MainActor
extension CourseArt {
    private static let clubhouseDetailMaterials:[SCNMaterial] = [
        UIColor(red:0.96,green:0.94,blue:0.83,alpha:1),
        UIColor(red:0.18,green:0.36,blue:0.39,alpha:1),
        UIColor(red:0.24,green:0.15,blue:0.09,alpha:1),
        UIColor(red:0.69,green:0.48,blue:0.20,alpha:1),
        UIColor(red:0.70,green:0.66,blue:0.53,alpha:1)
    ].enumerated().map { index,color in
        let material=SCNMaterial();material.name="Resort facade palette \(index)"
        material.lightingModel = .physicallyBased;material.diffuse.contents=color
        material.roughness.contents=index == 1 ? 0.25 : index == 3 ? 0.32 : 0.82
        material.metalness.contents=index == 3 ? 0.6 : 0
        return material
    }

    /// Complete all visible elevations, using five shared opaque materials.
    /// Windows are inset dark panes with raised frames, not transparent planes.
    static func clubhouseFacadeDetails()->SCNNode {
        let root=SCNNode();root.name="clubhouseFacadeDetails"
        func box(_ name:String,_ size:SCNVector3,_ position:SCNVector3,_ material:Int) {
            let geometry=SCNBox(width:CGFloat(size.x),height:CGFloat(size.y),length:CGFloat(size.z),chamferRadius:0.035)
            geometry.materials=[clubhouseDetailMaterials[material]]
            let node=SCNNode(geometry:geometry);node.name=name;node.position=position;root.addChildNode(node)
        }
        for side:Float in [-1,1] {
            for z:Float in [-3.1,2.6] {
                box("sideWindow",SCNVector3(0.14,3.2,2.7),SCNVector3(side*11.08,4.5,z),1)
                for offset:Float in [-1.45,0,1.45] {
                    box("sideWindowMullion",SCNVector3(0.24,3.5,0.13),SCNVector3(side*11.18,4.5,z+offset),0)
                }
                for y:Float in [2.8,4.5,6.2] {
                    box("sideWindowFrame",SCNVector3(0.25,0.14,3.05),SCNVector3(side*11.18,y,z),0)
                }
                box("sideWindowSill",SCNVector3(0.50,0.22,3.2),SCNVector3(side*11.25,2.72,z),4)
            }
            box("sideCornice",SCNVector3(0.32,0.24,12.3),SCNVector3(side*11.16,7.72,0),0)
        }
        for x:Float in [-7,0,7] {
            box("rearWindow",SCNVector3(3.4,3.2,0.14),SCNVector3(x,4.5,-6.08),1)
            for offset:Float in [-1.8,0,1.8] {
                box("rearWindowMullion",SCNVector3(0.13,3.5,0.24),SCNVector3(x+offset,4.5,-6.18),0)
            }
            for y:Float in [2.8,4.5,6.2] {
                box("rearWindowFrame",SCNVector3(3.75,0.14,0.25),SCNVector3(x,y,-6.18),0)
            }
        }
        for z:Float in [-6.16,6.16] {
            box("facadeCornice",SCNVector3(22.4,0.24,0.32),SCNVector3(0,7.72,z),0)
        }
        box("entranceDoor",SCNVector3(3.4,4.5,0.16),SCNVector3(0,2.65,6.10),2)
        for x:Float in [-0.82,0.82] {
            box("entranceGlazing",SCNVector3(1.36,2.85,0.12),SCNVector3(x,3.25,6.23),1)
        }
        for x:Float in [-1.85,0,1.85] {
            box("entranceFrame",SCNVector3(0.18,4.75,0.30),SCNVector3(x,2.72,6.24),0)
        }
        box("entranceLintel",SCNVector3(3.9,0.24,0.34),SCNVector3(0,5.12,6.24),0)
        for x:Float in [-0.25,0.25] {
            box("doorHandle",SCNVector3(0.07,0.5,0.15),SCNVector3(x,2.4,6.43),3)
        }
        box("entranceStep",SCNVector3(6,0.20,0.7),SCNVector3(0,0.10,11.6),4)
        return root
    }

    /// Sparse, irregular wet-bank planting. Reserve each entire leaf envelope
    /// outside playable lies and all hazards; this never changes a water penalty.
    static func shorelinePlantingSites(_ hole:Hole)->[CoursePoint] {
        guard hole.fairwayBoundary != nil else { return [] }
        var sites:[CoursePoint]=[]
        let lodge=clubhouseSite(hole)
        for lake in hole.hazards where lake.kind == .water {
            let count=max(64,Int(ceil(.pi*(lake.width+lake.length)/1.4)))
            for i in 0..<count {
                let angle=Double(i)*2 * .pi/Double(count)
                // Leave open stretches instead of making an ornamental hedge ring.
                guard sin(angle*7+Double(lake.id))*0.65+sin(angle*13)>0.1 else { continue }
                let scale=lake.boundaryScale(at:angle)
                let edge=CoursePoint(x:lake.x+cos(angle)*lake.width/2*scale,
                    d:lake.distance+sin(angle)*lake.length/2*scale)
                let radial=lake.radialSurface(at:edge)
                let length=max(0.0001,hypot(radial.dx,radial.dd))
                for row in 0..<3 {
                    let offset=0.65+Double(row)*0.65+0.12*sin(Double(i)*2.399)
                    let p=CoursePoint(x:edge.x+radial.dx/length*offset,d:edge.d+radial.dd/length*offset)
                    guard treeClearsClubhouse(p,radius:0.5,site:lodge),hole.lie(at:p) == .outOfBounds,
                        (0..<16).allSatisfy({ sample in
                            let a=Double(sample)*2 * .pi/16
                            let q=CoursePoint(x:p.x+cos(a)*0.48,d:p.d+sin(a)*0.48)
                            return hole.lie(at:q) == .outOfBounds && !hole.hazards.contains {$0.contains(q)}
                        }) else { continue }
                    sites.append(p)
                }
            }
        }
        return sites
    }

    static let shorelinePlantMaterial:SCNMaterial = {
        let material=SCNMaterial();material.name="Sunward bank sedges"
        material.lightingModel = .physicallyBased;material.diffuse.contents=UIColor.white
        material.roughness.contents=0.92;material.isDoubleSided=true
        return material
    }()

    struct WoodlandSite:Equatable {
        let point:CoursePoint
        let scale:Float
        let angle:Float
        let variant:Int
        let group:Int
    }

    /// Middle-distance groups occupy only unscored land. Gaps between groups
    /// preserve long views; overlapping crowns within each group read as woods.
    static func woodlandSites(_ hole:Hole)->[WoodlandSite] {
        guard hole.fairwayBoundary != nil else {return []}
        let segments=Array(zip(hole.centerline,hole.centerline.dropFirst()))
        let total=segments.reduce(0) {$0+$1.0.distance(to:$1.1)}
        var centers:[CoursePoint]=[]
        for station in 0..<4 {
            var remaining=total*(0.12+Double(station)*0.24)
            for (a,b) in segments {
                let length=a.distance(to:b)
                if remaining>length {remaining -= length;continue}
                guard length>0.001 else {continue}
                let ux=(b.x-a.x)/length,ud=(b.d-a.d)/length
                let offset=hole.fairwayWidth/2+Hole.roughWidth+64+sin(Double(station)*2.4)*12
                for side in [-1.0,1.0] {
                    centers.append(.init(x:a.x+ux*remaining+ud*offset*side,
                        d:a.d+ud*remaining-ux*offset*side))
                }
                break
            }
        }
        for angle in [0.3,1.6,2.7] {
            centers.append(.init(x:hole.pin.x+cos(angle)*125,d:hole.pin.d+sin(angle)*125))
        }
        let route=resortRoute(hole),lodge=clubhouseSite(hole)
        var result:[WoodlandSite]=[]
        for (group,center) in centers.enumerated() {for member in 0..<6 {
            let angle=Double(member)*2.399+Double(group)*0.8
            let radius=member == 0 ? 0 : 5+Double(member)*1.3
            let p=CoursePoint(x:center.x+cos(angle)*radius,d:center.d+sin(angle)*radius)
            let scale=Float(0.9+Double((group*3+member)%7)*0.1)
            guard treeClearsClubhouse(p,radius:6,site:lodge),clearsResortRoute(p,radius:1.4,route:route),
                hole.lie(at:p) == .outOfBounds,
                result.allSatisfy({$0.point.distance(to:p)>4}),
                (0..<24).allSatisfy({ sample in
                    let a=Double(sample)*2 * .pi/24
                    let q=CoursePoint(x:p.x+cos(a)*6,d:p.d+sin(a)*6)
                    return hole.lie(at:q) == .outOfBounds && !hole.hazards.contains {$0.contains(q)}
                }) else {continue}
            result.append(.init(point:p,scale:scale,angle:Float(angle),variant:(group+member)%3,group:group))
        }}
        return result
    }

    private static let woodlandAssets=(3...5).compactMap {SunwardAsset.load("NatureTree\($0)")}
    private static let woodlandMaterials:[SCNMaterial] = {
        var result=[obstacleBark]
        for asset in woodlandAssets {
            guard let definition=asset.materials.first(where:{$0.name == "sculpted-canopy"}) else {continue}
            let material=SunwardAsset.material(definition)
            material.normal.contents=canopyNormal;material.normal.intensity=0.16
            result.append(material)
        }
        return result
    }()

    /// One cullable four-material mesh per six-tree group, rather than a new
    /// scene hierarchy for every trunk and crown. Static distant trees need no
    /// per-frame rebuild; shadow rendering uses the same geometry.
    static func woodland(_ hole:Hole)->SCNNode {
        let root=SCNNode();root.name="sunwardWoodland"
        guard woodlandAssets.count == 3,woodlandMaterials.count == 4 else {return root}
        let sites=woodlandSites(hole)
        for group in Set(sites.map(\.group)).sorted() {
            var positions:[SCNVector3]=[],normals:[SCNVector3]=[],uv:[CGPoint]=[],colors:[Float]=[]
            var indices=Array(repeating:[Int32](),count:4)
            for site in sites where site.group == group {
                let p=site.point,sx=site.scale,sy=sx*1.25,c=cos(site.angle),s=sin(site.angle)
                let ground=(0..<12).map { i -> Float in
                    let a=Double(i)*2 * .pi/12
                    return dressingHeight(.init(x:p.x+cos(a)*Double(sx),d:p.d+sin(a)*Double(sx)),hole:hole)
                }.min() ?? dressingHeight(p,hole:hole)
                let asset=woodlandAssets[site.variant]
                for mesh in asset.meshes {
                    let first=Int32(positions.count)
                    let material=asset.materials[mesh.material].name == "sculpted-canopy" ? site.variant+1 : 0
                    for vertex in 0..<(mesh.positions.count/3) {
                        let i=vertex*3,x=mesh.positions[i],z=mesh.positions[i+2]
                        positions.append(SCNVector3(Float(p.x)+(c*x+s*z)*sx,ground-0.02+mesh.positions[i+1]*sy,-Float(p.d)+(-s*x+c*z)*sx))
                        let nx=mesh.normals[i]/sx,ny=mesh.normals[i+1]/sy,nz=mesh.normals[i+2]/sx
                        normals.append(SCNVector3(simd_normalize(simd_float3(c*nx+s*nz,ny,-s*nx+c*nz))))
                        uv.append(CGPoint(x:Double(mesh.uv[vertex*2]),y:Double(1-mesh.uv[vertex*2+1])))
                        colors += (0..<4).map {mesh.colors?[vertex*4+$0] ?? 1}
                    }
                    indices[material] += mesh.indices.map {$0+first}
                }
            }
            let color=SCNGeometrySource(data:colors.withUnsafeBytes{Data($0)},semantic:.color,
                vectorCount:positions.count,usesFloatComponents:true,componentsPerVector:4,bytesPerComponent:4,dataOffset:0,dataStride:16)
            let used=indices.indices.filter {!indices[$0].isEmpty}
            let geometry=SCNGeometry(sources:[.init(vertices:positions),.init(normals:normals),.init(textureCoordinates:uv),color],
                elements:used.map {SCNGeometryElement(indices:indices[$0],primitiveType:.triangles)})
            geometry.materials=used.map {woodlandMaterials[$0]}
            let node=SCNNode(geometry:geometry);node.name="woodlandGroup-\(group)";root.addChildNode(node)
        }
        root.addChildNode(canopyShadows(sites.map(\.point),hole:hole))
        return root
    }

    struct ShoreRockSite:Equatable {
        let point:CoursePoint
        let radius:Double
        let angle:Double
        let variant:Int
    }

    static func shorelineRockSites(_ hole:Hole)->[ShoreRockSite] {
        guard hole.fairwayBoundary != nil else {return []}
        let route=resortRoute(hole),lodge=clubhouseSite(hole)
        var result:[ShoreRockSite]=[]
        for lake in hole.hazards where lake.kind == .water {
            let count=max(64,Int(ceil(.pi*(lake.width+lake.length)/2.5)))
            for index in 0..<count {
                let angle=Double(index)*2 * .pi/Double(count),phase=Double(lake.id+hole.number)
                guard sin(angle*5+phase)+0.6*sin(angle*11-phase)>0.9 else {continue}
                let radius=0.32+0.47*(sin(Double(index)*2.399+phase)*0.5+0.5)
                let scale=lake.boundaryScale(at:angle)
                let edge=CoursePoint(x:lake.x+cos(angle)*lake.width/2*scale,d:lake.distance+sin(angle)*lake.length/2*scale)
                let radial=lake.radialSurface(at:edge),length=max(0.0001,hypot(radial.dx,radial.dd))
                let offset=radius+0.45+0.6*(sin(angle*17)*0.5+0.5)
                let p=CoursePoint(x:edge.x+radial.dx/length*offset,d:edge.d+radial.dd/length*offset)
                guard clearsResortRoute(p,radius:radius+0.25,route:route),treeClearsClubhouse(p,radius:radius+0.25,site:lodge),
                    hole.trees.allSatisfy({p.distance(to:$0.center)>radius+$0.trunkRadius+0.4}),
                    hole.lie(at:p) == .outOfBounds,
                    (0..<32).allSatisfy({ sample in
                        let a=Double(sample)*2 * .pi/32
                        let q=CoursePoint(x:p.x+cos(a)*(radius+0.15),d:p.d+sin(a)*(radius+0.15))
                        return hole.lie(at:q) == .outOfBounds && !hole.hazards.contains {$0.contains(q)}
                    }) else {continue}
                result.append(.init(point:p,radius:radius,angle:Double(index)*2.399,variant:index%3))
            }
        }
        return result
    }

    private static let shoreRockAssets=(1...3).compactMap {SunwardAsset.load("NatureShoreRock\($0)")}
    private static let shoreRockMaterial:SCNMaterial = {
        guard let definition=shoreRockAssets.first?.materials.first else {return SCNMaterial()}
        return SunwardAsset.material(definition)
    }()

    /// One batch of imported weathered stones embedded in non-playable banks.
    /// No stone intersects a scored lie or silently becomes a ball collider.
    static func shorelineRocks(_ hole:Hole)->SCNNode {
        var positions:[SCNVector3]=[],normals:[SCNVector3]=[],uv:[CGPoint]=[],colors:[Float]=[],indices:[Int32]=[]
        if shoreRockAssets.count == 3 {for site in shorelineRockSites(hole) {
            let p=site.point,r=site.radius,c=Float(cos(site.angle)),s=Float(sin(site.angle))
            let ground=(0..<16).map { index -> Float in
                let a=Double(index)*2 * .pi/16
                return dressingHeight(.init(x:p.x+cos(a)*r,d:p.d+sin(a)*r),hole:hole)
            }.min() ?? dressingHeight(p,hole:hole)
            let y=ground-Float(r)*0.12
            for mesh in shoreRockAssets[site.variant].meshes {
                let first=Int32(positions.count)
                for vertex in 0..<(mesh.positions.count/3) {
                    let i=vertex*3,x=mesh.positions[i],z=mesh.positions[i+2]
                    positions.append(SCNVector3(Float(p.x)+(c*x+s*z)*Float(r),y+mesh.positions[i+1]*Float(r),-Float(p.d)+(-s*x+c*z)*Float(r)))
                    let nx=mesh.normals[i],nz=mesh.normals[i+2]
                    normals.append(SCNVector3(c*nx+s*nz,mesh.normals[i+1],-s*nx+c*nz))
                    uv.append(CGPoint(x:Double(mesh.uv[vertex*2]),y:Double(1-mesh.uv[vertex*2+1])))
                    let shade:Float=0.72+0.28*min(1,max(0,mesh.positions[i+1]))
                    colors += [shade,shade,shade,1]
                }
                indices += mesh.indices.map {$0+first}
            }
        }}
        let color=SCNGeometrySource(data:colors.withUnsafeBytes{Data($0)},semantic:.color,
            vectorCount:positions.count,usesFloatComponents:true,componentsPerVector:4,bytesPerComponent:4,dataOffset:0,dataStride:16)
        let geometry=SCNGeometry(sources:[.init(vertices:positions),.init(normals:normals),.init(textureCoordinates:uv),color],
            elements:[SCNGeometryElement(indices:indices,primitiveType:.triangles)])
        geometry.materials=[shoreRockMaterial]
        let node=SCNNode(geometry:geometry);node.name="sunwardShoreRocks"
        return node
    }

    /// Seven bent, tapered leaves per clump, in one opaque batch. No alpha
    /// cards, billboard rotation, per-frame geometry or extra collision objects.
    static func shorelinePlanting(_ hole:Hole)->SCNNode {
        var positions:[SCNVector3]=[],normals:[SCNVector3]=[],colors:[Float]=[],indices:[Int32]=[]
        for (index,p) in shorelinePlantingSites(hole).enumerated() {
            let base=simd_float3(Float(p.x),surfaceHeight(p,hole:hole)-0.015,-Float(p.d))
            for leaf in 0..<7 {
                let angle=Float(index)*2.399+Float(leaf)*2.399
                let height=Float(0.35+0.45*(sin(Double(index*7+leaf)*1.73)*0.5+0.5))
                let forward=simd_float3(cos(angle),0,sin(angle)),side=simd_float3(-sin(angle),0,cos(angle))
                let middle=base+forward*0.12+simd_float3(0,height*0.60,0)
                let tip=base+forward*(0.22+height*0.22)+simd_float3(0,height,0)
                let start=Int32(positions.count)
                let vertices=[base-side*0.018,base+side*0.018,middle-side*0.035,middle+side*0.035,tip]
                let variation=Float(0.90+0.10*sin(Double(index+leaf)*2.13))
                for (row,vertex) in vertices.enumerated() {
                    positions.append(SCNVector3(vertex));normals.append(SCNVector3(0,1,0))
                    let color:simd_float3=row<2 ? [0.16,0.22,0.065] : row<4 ? [0.31,0.39,0.12] : [0.48,0.48,0.20]
                    colors += [color.x*variation,color.y*variation,color.z*variation,1]
                }
                indices += [start,start+1,start+2,start+2,start+1,start+3,start+2,start+3,start+4]
            }
        }
        let color=SCNGeometrySource(data:colors.withUnsafeBytes{Data($0)},semantic:.color,
            vectorCount:positions.count,usesFloatComponents:true,componentsPerVector:4,bytesPerComponent:4,dataOffset:0,dataStride:16)
        let geometry=SCNGeometry(sources:[.init(vertices:positions),.init(normals:normals),color],
            elements:[SCNGeometryElement(indices:indices,primitiveType:.triangles)])
        geometry.materials=[shorelinePlantMaterial]
        let node=SCNNode(geometry:geometry);node.name="sunwardShorelinePlanting";node.castsShadow=false
        return node
    }

    /// A continuous service route on the left exterior of the authored course.
    /// Expand scored contours, then propagate bends forward/backward before
    /// smoothing. Unlike deleting blocked rows, this goes AROUND a lake.
    static func resortRoute(_ hole:Hole)->[CoursePoint] {
        guard let fairway=hole.fairwayBoundary else {return []}
        var exclusions:[(CoursePoint,Double)]=[]
        func contour(_ points:[CoursePoint],clearance:Double) {
            guard let first=points.first else {return}
            for (a,b) in zip(points,Array(points.dropFirst())+[first]) {
                let count=max(1,Int(ceil(a.distance(to:b)/2)))
                for i in 0..<count {
                    let t=Double(i)/Double(count)
                    exclusions.append((CoursePoint(x:a.x+(b.x-a.x)*t,d:a.d+(b.d-a.d)*t),clearance))
                }
            }
        }
        contour(fairway.points,clearance:Hole.roughWidth+8)
        if let green=hole.greenBoundary {contour(green.points,clearance:10)}
        else {exclusions.append((hole.pin,hole.greenRadius+8))}
        exclusions.append((hole.tee,12))
        for hazard in hole.hazards {
            let count=max(64,Int(ceil(.pi*(hazard.width+hazard.length)/2)))
            contour((0..<count).map { i in
                let a=Double(i)*2 * .pi/Double(count),r=hazard.boundaryScale(at:a)
                return CoursePoint(x:hazard.x+cos(a)*hazard.width/2*r,d:hazard.distance+sin(a)*hazard.length/2*r)
            },clearance:9)
        }
        for tree in hole.trees {exclusions.append((tree.center,tree.trunkRadius+8))}
        let lodge=clubhouseSite(hole)
        contour([.init(x:lodge.x-15,d:lodge.d-13),.init(x:lodge.x+15,d:lodge.d-13),
            .init(x:lodge.x+15,d:lodge.d+13),.init(x:lodge.x-15,d:lodge.d+13)],clearance:8)
        let start=(hole.centerline.map(\.d).min() ?? hole.tee.d)-38
        let end=(hole.centerline.map(\.d).max() ?? hole.pin.d)+42
        let count=Int(ceil((end-start)/2)),step=(end-start)/Double(count)
        var x=(0...count).map { i -> Double in
            let d=start+Double(i)*step
            var edge=hole.tee.x-hole.fairwayWidth/2-Hole.roughWidth-10
            for (point,radius) in exclusions where abs(point.d-d)<radius {
                edge=min(edge,point.x-sqrt(max(0,radius*radius-pow(point.d-d,2))))
            }
            return edge
        }
        // The maximum lateral grade starts a bend before reaching an obstacle.
        for i in 1...count {x[i]=min(x[i],x[i-1]+step*0.45)}
        for i in stride(from:count-1,through:0,by:-1) {x[i]=min(x[i],x[i+1]+step*0.45)}
        for _ in 0..<2 {
            let prior=x
            for i in 0...count {
                x[i]=(-4...4).reduce(0.0) {$0+prior[max(0,min(count,i+$1))]}/9
            }
        }
        return (0...count).map {CoursePoint(x:x[$0],d:start+Double($0)*step)}
    }

    static func clearsResortRoute(_ point:CoursePoint,radius:Double,route:[CoursePoint])->Bool {
        for (a,b) in zip(route,route.dropFirst()) {
            let dx=b.x-a.x,dd=b.d-a.d
            let t=max(0,min(1,((point.x-a.x)*dx+(point.d-a.d)*dd)/max(0.0001,dx*dx+dd*dd)))
            if hypot(point.x-a.x-dx*t,point.d-a.d-dd*t)<radius+1.65 {return false}
        }
        return true
    }

    static func continuousResortPath(_ hole:Hole)->SCNNode {
        let route=resortRoute(hole)
        var positions:[SCNVector3]=[],normals:[SCNVector3]=[],uv:[CGPoint]=[],colors:[Float]=[],indices:[Int32]=[]
        var distance=0.0
        for (i,p) in route.enumerated() {
            let a=route[max(0,i-1)],b=route[min(route.count-1,i+1)]
            let length=max(0.001,a.distance(to:b)),nx=(b.d-a.d)/length,nd = -(b.x-a.x)/length
            if i>0 {distance += route[i-1].distance(to:p)}
            for column in 0...4 {
                let across=(Double(column)/4-0.5)*2.8
                let q=CoursePoint(x:p.x+nx*across,d:p.d+nd*across)
                let height=dressingHeight(q,hole:hole)
                let dx=(dressingHeight(.init(x:q.x+0.1,d:q.d),hole:hole)-dressingHeight(.init(x:q.x-0.1,d:q.d),hole:hole))/0.2
                let dz=(dressingHeight(.init(x:q.x,d:q.d-0.1),hole:hole)-dressingHeight(.init(x:q.x,d:q.d+0.1),hole:hole))/0.2
                positions.append(SCNVector3(Float(q.x),height+0.035,-Float(q.d)))
                normals.append(SCNVector3(simd_normalize(simd_float3(-dx,1,-dz))))
                uv.append(CGPoint(x:across,y:distance))
                let shade:Float=column == 0 || column == 4 ? 0.73 : 1
                colors += [shade,shade,shade,1]
            }
            if i>0 {for column:Int32 in 0..<4 {
                let a=Int32((i-1)*5)+column,b=a+5
                indices += [a,b,a+1,a+1,b,b+1]
            }}
        }
        let color=SCNGeometrySource(data:colors.withUnsafeBytes{Data($0)},semantic:.color,
            vectorCount:positions.count,usesFloatComponents:true,componentsPerVector:4,bytesPerComponent:4,dataOffset:0,dataStride:16)
        let geometry=SCNGeometry(sources:[.init(vertices:positions),.init(normals:normals),.init(textureCoordinates:uv),color],
            elements:[SCNGeometryElement(indices:indices,primitiveType:.triangles)])
        let material=SCNMaterial();material.name="Sunward warm gravel route"
        material.lightingModel = .physicallyBased;material.diffuse.contents=UIColor(red:0.68,green:0.61,blue:0.45,alpha:1)
        material.roughness.contents=0.95;material.isDoubleSided=true
        geometry.materials=[material]
        let root=SCNNode();root.name="cartPath"
        let node=SCNNode(geometry:geometry);node.name="sunwardResortPath";node.castsShadow=false
        root.addChildNode(node);return root
    }

    typealias GreenReadSample = GreenReadPattern.Sample

    static func greenReadSamples(_ hole:Hole)->[GreenReadSample] {
        GreenReadPattern.samples(hole)
    }

    /// One draw call replaces hundreds of animated dot nodes. Local quadratic
    /// height samples follow the green; the physics surface itself is unchanged.
    static func greenReadGeometry(_ hole:Hole)->SCNGeometry {
        var positions:[SCNVector3]=[],uv:[CGPoint]=[],parameters:[SIMD4<Float>]=[],colors:[Float]=[],indices:[Int32]=[]
        for sample in greenReadSamples(hole) {
            let base=Int32(positions.count)
            let color=readMaterial(steepness:sample.slope).diffuse.contents as? UIColor ?? .white
            var r:CGFloat=0,g:CGFloat=0,b:CGFloat=0,a:CGFloat=0
            color.getRed(&r,green:&g,blue:&b,alpha:&a)
            for i in 0...8 {
                let angle=Double(max(0,i-1))*2 * .pi/8,radius=i == 0 ? 0.0 : 0.09
                positions.append(SCNVector3(sample.point.x+cos(angle)*radius,sample.height+0.025,-sample.point.d+sin(angle)*radius))
                uv.append(CGPoint(x:Double(sample.flow.x),y:Double(sample.flow.y)))
                parameters.append(SIMD4(sample.curve.x,sample.curve.y,sample.speed,1))
                colors += [Float(r),Float(g),Float(b),Float(a)]
            }
            for i in 0..<8 { indices += [base,base+Int32(i+1),base+Int32((i+1)%8+1)] }
        }
        let parameterSource=SCNGeometrySource(data:parameters.withUnsafeBytes{Data($0)},semantic:.tangent,
            vectorCount:positions.count,usesFloatComponents:true,componentsPerVector:4,bytesPerComponent:4,dataOffset:0,dataStride:16)
        let colorSource=SCNGeometrySource(data:colors.withUnsafeBytes{Data($0)},semantic:.color,
            vectorCount:positions.count,usesFloatComponents:true,componentsPerVector:4,bytesPerComponent:4,dataOffset:0,dataStride:16)
        let geometry=SCNGeometry(sources:[.init(vertices:positions),.init(normals:positions.map {_ in SCNVector3(0,1,0)}),
            .init(textureCoordinates:uv),parameterSource,colorSource],elements:[SCNGeometryElement(indices:indices,primitiveType:.triangles)])
        let material=SCNMaterial();material.name="Batched downhill green read"
        material.lightingModel = .constant;material.diffuse.contents=UIColor.white
        material.isDoubleSided=true;material.writesToDepthBuffer=false
        material.blendMode = .alpha
        material.shaderModifiers=[.geometry:"""
        #pragma arguments
        float readTime;
        float readMotion;
        #pragma body
        float4 parameters = _geometry.tangent;
        float travel = (fract(readTime * parameters.z * 0.5) * 2.0 - 1.0) * readMotion;
        _geometry.position.xz += _geometry.texcoords[0] * travel;
        _geometry.position.y += parameters.x * travel + parameters.y * travel * travel;
        _geometry.tangent = float4(1.0,0.0,0.0,1.0);
        """]
        material.setValue(Float(0),forKey:"readTime");material.setValue(Float(1),forKey:"readMotion")
        geometry.materials=[material]
        return geometry
    }

    /// Low planting layers the tree line without introducing untracked obstacles
    /// on scored turf. Clearance includes the complete three-yard cluster radius.
    static func borderPlantingSites(_ hole:Hole) -> [CoursePoint] {
        let route=resortRoute(hole)
        // Bring some low planting to the route/lake layer, rather than putting
        // every shrub under the distant tree line. Leave broad open intervals.
        let routeGardens=route.enumerated().compactMap { index,point -> CoursePoint? in
            guard index>12,index<route.count-12,index.isMultiple(of:18) else {return nil}
            return CoursePoint(x:point.x+(index.isMultiple(of:36) ? -6.5 : 6.5),d:point.d)
        }
        let candidates=routeGardens+decorationPoints(hole)+backdropGrovePoints(hole).enumerated().compactMap { index,point in
            index.isMultiple(of:2) ? CoursePoint(x:point.x-2,d:point.d-3) : nil
        }
        let lodge=clubhouseSite(hole)
        return Array(candidates.filter { point in
            guard abs(point.x-lodge.x)>25 || abs(point.d-lodge.d)>23 else { return false }
            return clearsResortRoute(point,radius:3.2,route:route) && hole.lie(at:point) == .outOfBounds && (0..<24).allSatisfy { i in
                let a=Double(i)*2 * .pi/24
                return hole.lie(at:.init(x:point.x+cos(a)*3.2,d:point.d+sin(a)*3.2)) == .outOfBounds
            }
        }.prefix(36))
    }

    private static let borderPrototypes:[SCNNode]=(0..<4).map { seed in
        let root=SCNNode()
        let leaf=SCNMaterial();leaf.lightingModel = .physicallyBased;leaf.roughness.contents=0.95
        leaf.diffuse.contents=UIColor(red:0.28+Double(seed%2)*0.04,green:0.43,blue:0.16,alpha:1)
        let petal=SCNMaterial();petal.lightingModel = .physicallyBased;petal.roughness.contents=0.8
        petal.diffuse.contents=seed.isMultiple(of:2)
            ? UIColor(red:0.95,green:0.66,blue:0.42,alpha:1)
            : UIColor(red:0.91,green:0.84,blue:0.55,alpha:1)
        for i in 0..<5 {
            let angle=Float(i)*2.399+Float(seed)*0.8
            let shape=foliage(radius:0.78+Double(i%3)*0.12,seed:seed+i,rows:8,columns:12,tapered:true)
            shape.materials=[leaf]
            let shrub=SCNNode(geometry:shape)
            shrub.simdPosition=simd_float3(cos(angle)*1.6,0.45,sin(angle)*1.6)
            shrub.simdScale=simd_float3(1,0.7,1);root.addChildNode(shrub)
        }
        // Small opaque blossom clusters give warm accents at ground level.
        // No transparent leaf cards, per-frame work, or collision geometry.
        let blossom=foliage(radius:0.10,seed:seed,rows:4,columns:6);blossom.materials=[petal]
        for i in 0..<18 {
            let a=Float(i)*2.399+Float(seed),r:Float=1.4+Float(i%5)*0.22
            let spray=SCNNode();spray.simdPosition=simd_float3(cos(a)*r,0.37+Float(i%3)*0.11,sin(a)*r)
            for j in 0..<3 {
                let flower=SCNNode(geometry:blossom)
                let theta=Float(j)*2.094
                flower.simdPosition=simd_float3(cos(theta)*0.10,Float(j%2)*0.03,sin(theta)*0.10)
                flower.simdScale=simd_float3(1,0.55,1)
                spray.addChildNode(flower)
            }
            root.addChildNode(spray)
        }
        return root
    }

    static func borderPlanting(_ hole:Hole) -> SCNNode {
        let root=SCNNode();root.name="sunwardBorderPlanting"
        for (i,point) in borderPlantingSites(hole).enumerated() {
            let cluster=borderPrototypes[i%borderPrototypes.count].clone()
            let ground=dressingHeight(point,hole:hole)
            cluster.simdPosition=simd_float3(Float(point.x),ground,-Float(point.d))
            for child in cluster.childNodes {
                let p=CoursePoint(x:point.x+Double(child.position.x),d:point.d-Double(child.position.z))
                child.position.y += dressingHeight(p,hole:hole)-ground
            }
            root.addChildNode(cluster)
        }
        // Explicitly merge authored vertices rather than relying on SceneKit's
        // lazy flattenedClone representation (which produced an empty batch).
        var vertices:[SCNVector3]=[],normals:[SCNVector3]=[],colors:[Float]=[]
        var materials:[SCNMaterial]=[],groups:[[Int32]]=[]
        func vector(_ source:SCNGeometrySource,_ index:Int,_ component:Int)->Float {
            source.data.withUnsafeBytes { bytes in
                bytes.loadUnaligned(fromByteOffset:source.dataOffset+index*source.dataStride+component*source.bytesPerComponent,as:Float.self)
            }
        }
        root.enumerateChildNodes { node,_ in
            guard let geometry=node.geometry,
                let positions=geometry.sources(for:.vertex).first,
                let normal=geometry.sources(for:.normal).first,
                let color=geometry.sources(for:.color).first,
                let material=geometry.firstMaterial else { return }
            let group:Int
            if let index=materials.firstIndex(where:{$0 === material}) { group=index }
            else { group=materials.count;materials.append(material);groups.append([]) }
            let first=Int32(vertices.count),transform=node.simdWorldTransform
            let normalTransform=simd_transpose(simd_inverse(transform))
            for i in 0..<positions.vectorCount {
                let p=transform*simd_float4(vector(positions,i,0),vector(positions,i,1),vector(positions,i,2),1)
                let n=normalTransform*simd_float4(vector(normal,i,0),vector(normal,i,1),vector(normal,i,2),0)
                vertices.append(SCNVector3(p.x,p.y,p.z))
                normals.append(SCNVector3(simd_normalize(simd_float3(n.x,n.y,n.z))))
                colors += (0..<4).map { vector(color,i,$0) }
            }
            for element in geometry.elements {
                element.data.withUnsafeBytes { bytes in
                    for index in 0..<(element.primitiveCount*3) {
                        groups[group].append(first+bytes.loadUnaligned(fromByteOffset:index*4,as:Int32.self))
                    }
                }
            }
        }
        let color=SCNGeometrySource(data:colors.withUnsafeBytes{Data($0)},semantic:.color,
            vectorCount:vertices.count,usesFloatComponents:true,componentsPerVector:4,bytesPerComponent:4,dataOffset:0,dataStride:16)
        let geometry=SCNGeometry(sources:[.init(vertices:vertices),.init(normals:normals),color],
            elements:groups.map { SCNGeometryElement(indices:$0,primitiveType:.triangles) })
        geometry.materials=materials
        let merged=SCNNode(geometry:geometry);merged.name=root.name
        return merged
    }

    /// Break the empty horizon beyond the green without adding invisible
    /// obstacles to scored terrain or placing trees through the clubhouse.
    static func backdropGrovePoints(_ hole: Hole) -> [CoursePoint] {
        let lodge=clubhouseSite(hole)
        var points:[CoursePoint]=[]
        for row in 0..<2 {
            for station in 0..<18 {
                // Six irregular groups per depth layer, not evenly spaced beads.
                // Within-group crowns overlap, while open windows reveal hills.
                let group=station/3,member=station%3
                let angle=(-0.22+Double(group)/5*3.58)+Double(row)*0.13
                let radius=hole.greenRadius+44+Double(row)*23+sin(Double(group)*2.4)*7
                let localAngle=Double(member)*2.399+Double(group)*0.8
                let point=CoursePoint(x:hole.pin.x+cos(angle)*radius+cos(localAngle)*5.5,
                    d:hole.pin.d+sin(angle)*radius+sin(localAngle)*5.5)
                guard abs(point.x-lodge.x)>24 || abs(point.d-lodge.d)>22 else { continue }
                guard hole.lie(at:point) == .outOfBounds,
                    (0..<16).allSatisfy({ i in
                        let a=Double(i)*2 * .pi/16
                        return hole.lie(at:.init(x:point.x+cos(a)*7,d:point.d+sin(a)*7)) == .outOfBounds
                    }) else { continue }
                points.append(point)
            }
        }
        return points
    }

    static func clubhouseSite(_ hole: Hole) -> CoursePoint {
        for (dx,dd) in [(48.0,43.0),(58.0,55.0),(70.0,78.0),(90.0,100.0)] {
            let site=CoursePoint(x:hole.pin.x+dx,d:hole.pin.d+dd)
            let clear=stride(from:-20.0,through:16.0,by:4).allSatisfy { x in
                stride(from:-12.0,through:15.0,by:3).allSatisfy { d in
                    hole.lie(at:CoursePoint(x:site.x+x,d:site.d+d)) == .outOfBounds
                }
            }
            if clear { return site }
        }
        return CoursePoint(x:hole.pin.x+140,d:hole.pin.d+140)
    }

    static func treeClearsClubhouse(_ point:CoursePoint,radius:Double,site:CoursePoint)->Bool {
        let dx=max(0,abs(point.x-site.x)-15),dd=max(0,abs(point.d-site.d)-12)
        return hypot(dx,dd)>radius
    }

    static let roofTileMaterial: SCNMaterial = {
        let material=SCNMaterial();material.name="Sunward terracotta roof tiles"
        material.lightingModel = .physicallyBased;material.roughness.contents=0.82
        material.diffuse.contents=UIColor(red:0.70,green:0.28,blue:0.17,alpha:1)
        material.shaderModifiers=[.surface:"""
        #pragma body
        float2 tile = _surface.diffuseTexcoord * float2(1.8,1.3);
        tile.x += floor(tile.y) * 0.5;
        float2 cell = floor(tile);
        float2 uv = fract(tile);
        float rim = smoothstep(0.015,0.045,min(min(uv.x,1.0-uv.x),min(uv.y,1.0-uv.y)));
        float tone = fract(sin(dot(cell,float2(127.1,311.7))) * 43758.5453);
        _surface.diffuse.rgb *= (0.82+tone*0.22)*(0.80+rim*0.20);
        """]
        return material
    }()

    /// Closed gable roof, with flat face normals and metre-scale UVs. Unlike a
    /// pyramid, the long ridge reads as resort architecture from course cameras.
    static func gableRoof(width:Float,height:Float,depth:Float) -> SCNGeometry {
        let w=width/2,d=depth/2
        let p:[simd_float3]=[[-w,0,d],[w,0,d],[-w,height,0],[w,height,0],[-w,0,-d],[w,0,-d]]
        let faces=[[0,1,2],[1,3,2],[4,2,5],[5,2,3],[0,2,4],[1,5,3],[0,4,1],[1,4,5]]
        var vertices:[SCNVector3]=[],normals:[SCNVector3]=[],uv:[CGPoint]=[],indices:[Int32]=[]
        for face in faces {
            let n=simd_normalize(simd_cross(p[face[1]]-p[face[0]],p[face[2]]-p[face[0]]))
            for index in face {
                let vertex=p[index]
                indices.append(Int32(vertices.count));vertices.append(SCNVector3(vertex));normals.append(SCNVector3(n))
                uv.append(CGPoint(x:Double(vertex.x),y:Double(vertex.z)))
            }
        }
        return SCNGeometry(sources:[.init(vertices:vertices),.init(normals:normals),.init(textureCoordinates:uv)],
            elements:[SCNGeometryElement(indices:indices,primitiveType:.triangles)])
    }

    /// Seamless multi-scale leaf-mass normal map. Isotropic value noise avoids
    /// the linear grass/rake pattern previously stretched over every crown.
    static let canopyNormal: UIImage = {
        let size=128
        func noise(_ x:Double,_ y:Double,_ grid:Int)->Double {
            let u=x*Double(grid)/Double(size),v=y*Double(grid)/Double(size)
            let ix=Int(floor(u)),iy=Int(floor(v)),fx=u-floor(u),fy=v-floor(v)
            func value(_ i:Int,_ j:Int)->Double {
                let a=((i%grid)+grid)%grid,b=((j%grid)+grid)%grid
                var n=UInt64(a+317*b+7919)
                n = (n ^ (n>>13)) &* 1274126177
                return Double(n%65536)/65535
            }
            let tx=fx*fx*(3-2*fx),ty=fy*fy*(3-2*fy)
            let lower = value(ix,iy)*(1-tx)+value(ix+1,iy)*tx
            let upper = value(ix,iy+1)*(1-tx)+value(ix+1,iy+1)*tx
            return lower*(1-ty) + upper*ty
        }
        func height(_ x:Double,_ y:Double)->Double {
            noise(x,y,12)*0.8+noise(x,y,29)*0.28+noise(x,y,53)*0.08
        }
        let format=UIGraphicsImageRendererFormat();format.scale=1;format.opaque=true
        return UIGraphicsImageRenderer(size:CGSize(width:size,height:size),format:format).image { context in
            for y in 0..<size { for x in 0..<size {
                let u=Double(x),v=Double(y)
                let normal=simd_normalize(simd_double3((height(u-1,v)-height(u+1,v))*1.8,
                    (height(u,v-1)-height(u,v+1))*1.8,1))
                UIColor(red:(normal.x+1)/2,green:(normal.y+1)/2,blue:(normal.z+1)/2,alpha:1).setFill()
                context.cgContext.fill(CGRect(x:x,y:y,width:1,height:1))
            } }
        }
    }()

    static let terrainVertex = """
    #pragma varyings
    float4 terrainData;
    float2 terrainWorld;
    #pragma body
    out.terrainData = _geometry.color;
    out.terrainWorld = _geometry.position.xz;
    _geometry.color = float4(1.0);
    """

    static let terrainGrain = """
    #pragma body
    float2 world = in.terrainWorld;
    float variation = sin(world.x * 0.13 + sin(world.y * 0.17)) * sin(world.y * 0.23)
                    + 0.5 * sin(world.x * 0.69 + world.y * 0.42);
    _surface.diffuse.rgb *= 0.96 + variation * 0.035;
    _surface.ambientOcclusion *= in.terrainData.g;
    """

    static func turfTint(_ lie:CourseLie)->simd_float3 {
        switch lie {
        case .deepRough: return simd_float3(1.04,1.04,0.88)
        case .fringe: return simd_float3(1.30,1.22,0.96)
        default: return simd_float3(1.22,1.16,0.96)
        }
    }

    static let grassBladeMaterial: SCNMaterial = {
        let material=SCNMaterial();material.name="Sunward living turf"
        material.lightingModel = .physicallyBased
        material.diffuse.contents=UIImage(named:"SunwardRough") ?? UIColor(red:0.32,green:0.45,blue:0.15,alpha:1)
        material.diffuse.wrapS = .repeat;material.diffuse.wrapT = .repeat
        material.diffuse.mipFilter = .linear
        material.roughness.contents=0.88;material.isDoubleSided=true
        material.shaderModifiers = [.geometry:"""
        #pragma arguments
        float sunwardTime;
        float3 grassBall;
        #pragma varyings
        float3 bladeTint;
        float bladeHeight;
        float3 turfNormal;
        float3 leafNormal;
        float bladeFade;
        #pragma body
        float height = _geometry.texcoords[0].y;
        out.bladeHeight = height;
        out.turfNormal = normalize((scn_node.normalTransform * float4(_geometry.normal,0.0)).xyz);
        float bladeAngle=_geometry.texcoords[0].x;
        out.leafNormal=normalize((scn_node.normalTransform * float4(-sin(bladeAngle),0.0,cos(bladeAngle),0.0)).xyz);
        // Sample the same albedo at the same world coordinate as the ground;
        // unrelated bright tip colors made individual blades look like glitter.
        _geometry.texcoords[0] = float2(_geometry.position.x, -_geometry.position.z) / 6.0;
        float distance = length((scn_node.modelViewTransform * _geometry.position).xyz);
        float fade = 1.0 - smoothstep(24.0, 52.0, distance);
        out.bladeFade=fade;
        float ballClear = smoothstep(0.45, 0.85, length(_geometry.position.xz - grassBall.xz));
        _geometry.position.y -= height * (1.0 - fade * ballClear);
        // Fully retired blades must be below their supporting floor, not tiny
        // bright coplanar triangles left over from the fade.
        _geometry.position.y -= 0.08*(1.0-fade);
        float breeze = sin(_geometry.position.x * 0.45 + _geometry.position.z * 0.29 + sunwardTime * 1.5);
        _geometry.position.x += breeze * height * 0.2 * fade * ballClear;
        out.bladeTint = _geometry.color.rgb;
        _geometry.color = float4(1.0);
        """,.surface:"""
        #pragma body
        _surface.diffuse.rgb *= in.bladeTint;
        // Anchor the lighting to the supporting slope, with a restrained leaf
        // tilt so nearby clumps have volume without becoming bright flecks.
        float3 leaf=dot(in.leafNormal,_surface.view)<0.0 ? -in.leafNormal : in.leafNormal;
        _surface.normal = normalize(in.turfNormal+leaf*0.22*in.bladeFade);
        _surface.ambientOcclusion = mix(1.0,0.72+0.28*saturate(in.bladeHeight*12.0),in.bladeFade);
        """]
        material.setValue(Float(0),forKey:"sunwardTime")
        material.setValue(NSValue(scnVector3:SCNVector3(0,0,0)),forKey:"grassBall")
        return material
    }()

    /// Spatial chunks permit frustum culling; the shader smoothly flattens tiny
    /// distant blades before they alias. Opaque blades avoid alpha overdraw.
    static func turfClearsHazardEdges(_ point: CoursePoint, hole: Hole) -> Bool {
        hole.hazards.allSatisfy { hazard in
            let clearance = hazard.kind == .water ? 1.25 : 0.20
            let radius = hazard.radialSurface(at: point).radius
            return (radius - 1) * min(hazard.width, hazard.length) / 2 > clearance
        }
    }

    /// A draped soil collar exposes the cut turf roots at the sand boundary.
    /// Separate fine contour geometry keeps its width stable instead of relying
    /// on metadata interpolated across the 0.8-yard terrain cells.
    static func bunkerSoilCollar(_ hole:Hole)->SCNNode {
        var positions:[SCNVector3]=[],normals:[SCNVector3]=[],colors:[Float]=[],indices:[Int32]=[]
        for bunker in hole.hazards where bunker.kind == .bunker {
            let count=max(128,Int(ceil(.pi*(bunker.width+bunker.length)/0.25)))
            let start=Int32(positions.count)
            for i in 0...count {
                let angle=Double(i%count)*2 * .pi/Double(count)
                let scale=bunker.boundaryScale(at:angle)
                let edge=CoursePoint(x:bunker.x+cos(angle)*bunker.width/2*scale,
                    d:bunker.distance+sin(angle)*bunker.length/2*scale)
                let radial=bunker.radialSurface(at:edge)
                let gradient=max(0.0001,hypot(radial.dx,radial.dd))
                let width=0.18+0.025*sin(angle*19+Double(bunker.id))
                for row in 0..<3 {
                    let offset=Double(row)/2*width
                    let point=CoursePoint(x:edge.x-radial.dx/gradient*offset,d:edge.d-radial.dd/gradient*offset)
                    let surface=hole.surface(at:point)
                    positions.append(SCNVector3(point.x,surface.heightYards+0.009,-point.d))
                    normals.append(SCNVector3(simd_normalize(simd_float3(Float(-surface.slopeX),1,Float(surface.slopeD)))))
                    let shade=Float(0.92+0.08*sin(angle*37))
                    // Dark roots at the turf; warm dry earth at the sand edge.
                    let color:[Float]=row == 0 ? [0.20,0.24,0.075] : row == 1 ? [0.34,0.26,0.12] : [0.63,0.49,0.28]
                    colors += [color[0]*shade,color[1]*shade,color[2]*shade,1]
                }
            }
            for i in 0..<count { for row in 0..<2 {
                let a=start+Int32(i*3+row),b=a+3
                indices += [a,b,a+1,a+1,b,b+1]
            } }
        }
        let color=SCNGeometrySource(data:colors.withUnsafeBytes{Data($0)},semantic:.color,
            vectorCount:positions.count,usesFloatComponents:true,componentsPerVector:4,bytesPerComponent:4,dataOffset:0,dataStride:16)
        let geometry=SCNGeometry(sources:[.init(vertices:positions),.init(normals:normals),color],
            elements:[SCNGeometryElement(indices:indices,primitiveType:.triangles)])
        let material=SCNMaterial();material.name="Cut bunker turf roots"
        material.lightingModel = .physicallyBased;material.diffuse.contents=UIColor.white
        material.roughness.contents=1;material.isDoubleSided=true
        geometry.materials=[material]
        let node=SCNNode(geometry:geometry);node.name="sunwardBunkerSoilCollar";node.castsShadow=false
        return node
    }

    /// Closely clipped tufts on the grass side of the authoritative sand edge.
    /// This is surface dressing, not a raised collision rim or another sand disc.
    static func bunkerTurfSites(_ hole:Hole)->[CoursePoint] {
        var result:[CoursePoint]=[]
        for bunker in hole.hazards where bunker.kind == .bunker {
            let count=max(96,Int(ceil(.pi*(bunker.width+bunker.length)/0.4)))
            for i in 0..<count {
                let angle=Double(i)*2 * .pi/Double(count)
                let scale=bunker.boundaryScale(at:angle)
                let edge=CoursePoint(x:bunker.x+cos(angle)*bunker.width/2*scale,
                    d:bunker.distance+sin(angle)*bunker.length/2*scale)
                let radial=bunker.radialSurface(at:edge)
                let length=max(0.0001,hypot(radial.dx,radial.dd))
                for (row,offset) in [0.12,0.28,0.48].enumerated() {
                    let stagger=sin(Double(i*13+row*7))*0.04
                    let p=CoursePoint(x:edge.x+radial.dx/length*offset-radial.dd/length*stagger,
                        d:edge.d+radial.dd/length*offset+radial.dx/length*stagger)
                    let lie=hole.lie(at:p)
                    guard lie == .rough || lie == .deepRough || lie == .fringe else {continue}
                    // Full tuft footprint stays clear of both sand and water.
                    guard hole.hazards.allSatisfy({ hazard in
                        (hazard.radialSurface(at:p).radius-1)*min(hazard.width,hazard.length)/2 > 0.07
                    }) else {continue}
                    result.append(p)
                }
            }
        }
        return result
    }

    static func bunkerEdgeTurf(_ hole:Hole)->SCNNode {
        var vertices:[SCNVector3]=[],normals:[SCNVector3]=[],uv:[CGPoint]=[],colors:[Float]=[],indices:[Int32]=[]
        for (index,point) in bunkerTurfSites(hole).enumerated() {
            let height=Float(0.05+0.04*(sin(Double(index)*2.399)*0.5+0.5))
            let surface=hole.surface(at:point),tint=turfTint(surface.lie)
            let normal=simd_normalize(simd_float3(Float(-surface.slopeX),1,Float(surface.slopeD)))
            let center=simd_float3(Float(point.x),Float(surface.heightYards)-0.006,-Float(point.d))
            for blade in 0..<3 {
                let angle=Float(index)*2.399+Float(blade)*1.047
                let side=simd_float3(cos(angle)*0.032,0,sin(angle)*0.032)
                let tip=center+simd_float3(cos(angle+0.4)*0.02,height,sin(angle+0.4)*0.02)
                let first=Int32(vertices.count)
                var left=center-side,right=center+side
                let rise=side.x*Float(surface.slopeX)-side.z*Float(surface.slopeD)
                left.y -= rise;right.y += rise
                for (i,p) in [left,right,tip].enumerated() {
                    vertices.append(SCNVector3(p));normals.append(SCNVector3(normal))
                    uv.append(CGPoint(x:Double(angle),y:i == 2 ? Double(height) : 0))
                    let shade:Float=i == 2 ? 1.02 : 0.82
                    colors += [shade*tint.x,shade*tint.y,shade*tint.z,1]
                }
                indices += [first,first+1,first+2]
            }
        }
        let color=SCNGeometrySource(data:colors.withUnsafeBytes{Data($0)},semantic:.color,
            vectorCount:vertices.count,usesFloatComponents:true,componentsPerVector:4,bytesPerComponent:4,dataOffset:0,dataStride:16)
        let geometry=SCNGeometry(sources:[.init(vertices:vertices),.init(normals:normals),.init(textureCoordinates:uv),color],
            elements:[SCNGeometryElement(indices:indices,primitiveType:.triangles)])
        geometry.materials=[grassBladeMaterial]
        let node=SCNNode(geometry:geometry);node.name="sunwardBunkerEdgeTurf";node.castsShadow=false
        return node
    }

    static func livingTurf(_ hole: Hole) -> SCNNode {
        let root=SCNNode();root.name="sunwardLivingTurf"
        guard hole.fairwayBoundary != nil else { return root }
        let bounds=hole.centerline+(hole.fairwayBoundary?.points ?? [])+(hole.greenBoundary?.points ?? [])
        let minX=(bounds.map(\.x).min() ?? 0)-20,maxX=(bounds.map(\.x).max() ?? 0)+20
        let minD=(bounds.map(\.d).min() ?? 0)-20,maxD=(bounds.map(\.d).max() ?? 0)+20
        var patches:[(Double,Double,Double)]=[]
        for x in stride(from:minX,to:maxX,by:20.0) {
            for d in stride(from:minD,to:maxD,by:20.0) {
                let center=CoursePoint(x:x+10,d:d+10)
                if center.distance(to:hole.pin)<58 || center.distance(to:hole.tee)<33 {
                    // Keep small cullable batches where blades are denser.
                    for dx in [0.0,10.0] { for dd in [0.0,10.0] { patches.append((x+dx,d+dd,10)) } }
                } else { patches.append((x,d,20)) }
            }
        }
        var seed=UInt64(hole.number*917+41)
        func random()->Double { seed=seed &* 6364136223846793005 &+ 1;return Double((seed>>32)%65536)/65536 }
        for (x0,d0,chunk) in patches {
                let center=CoursePoint(x:x0+chunk/2,d:d0+chunk/2)
                let detailed=center.distance(to:hole.pin)<43 || center.distance(to:hole.tee)<18
                let spacing=detailed ? 0.14 : 0.60
                var vertices:[SCNVector3]=[],normals:[SCNVector3]=[],uv:[CGPoint]=[],colors:[Float]=[],indices:[Int32]=[]
                func flush() {
                    guard !vertices.isEmpty else { return }
                    let color=SCNGeometrySource(data:colors.withUnsafeBytes{Data($0)},semantic:.color,
                        vectorCount:vertices.count,usesFloatComponents:true,componentsPerVector:4,bytesPerComponent:4,dataOffset:0,dataStride:16)
                    let geometry=SCNGeometry(sources:[.init(vertices:vertices),.init(normals:normals),.init(textureCoordinates:uv),color],
                        elements:[SCNGeometryElement(indices:indices,primitiveType:.triangles)])
                    geometry.materials=[grassBladeMaterial]
                    let node=SCNNode(geometry:geometry);node.castsShadow=false;root.addChildNode(node)
                    vertices.removeAll(keepingCapacity:true);normals.removeAll(keepingCapacity:true)
                    uv.removeAll(keepingCapacity:true);colors.removeAll(keepingCapacity:true);indices.removeAll(keepingCapacity:true)
                }
                for x in stride(from:x0,to:min(maxX,x0+chunk),by:spacing) {
                    for d in stride(from:d0,to:min(maxD,d0+chunk),by:spacing) {
                        let point=CoursePoint(x:x+(random()-0.5)*spacing,d:d+(random()-0.5)*spacing)
                        let lie=hole.lie(at:point)
                        guard lie == .rough || lie == .deepRough || lie == .fringe else { continue }
                        // Exposed bank soil must stay bare. A small setback at
                        // sand also prevents crossed blades overhanging the lip.
                        guard turfClearsHazardEdges(point,hole:hole) else { continue }
                        // These accepted lies are all scored ground. Avoid
                        // rebuilding distant-landscape bounds for every tuft.
                        let surface=hole.surface(at:point),baseHeight=Float(surface.heightYards)
                        let turfNormal=simd_normalize(simd_float3(Float(-surface.slopeX),1,Float(surface.slopeD)))
                        let tint=turfTint(lie)
                        let height=Float(lie == .fringe ? 0.018+random()*0.014 : 0.075+random()*0.055)
                        let angle=Float(random()*2 * .pi)
                        if vertices.count+9>14994 { flush() }
                        // Three simple tapered blades provide much more ground
                        // coverage per triangle than two five-vertex bent blades.
                        for blade in 0..<3 {
                            let a=angle+Float(blade)*2.094,width:Float=lie == .fringe ? 0.012 : 0.035
                            let dx=cos(a)*width,dz=sin(a)*width
                            let lean=simd_float3(cos(a+0.6),0,sin(a+0.6))*height*0.28
                            let center=simd_float3(Float(point.x),baseHeight-0.004,-Float(point.d))
                            let side=simd_float3(dx,0,dz)
                            var left=center-side,right=center+side
                            // The centimeter-wide roots follow the local tangent;
                            // avoid repeating full hazard queries for every corner.
                            let rise=side.x*Float(surface.slopeX)-side.z*Float(surface.slopeD)
                            left.y -= rise;right.y += rise
                            let positions=[left,right,center+simd_float3(0,height,0)+lean]
                            let first=Int32(vertices.count),tone=Float(0.96+random()*0.08)
                            for (i,p) in positions.enumerated() {
                                vertices.append(SCNVector3(p));normals.append(SCNVector3(turfNormal))
                                uv.append(CGPoint(x:Double(a),y:i<2 ? 0 : Double(height)))
                                let shade=tone*(i<2 ? 0.72 : 1.03)
                                colors += [shade*tint.x,shade*tint.y,shade*tint.z,1]
                            }
                            indices += [first,first+1,first+2]
                        }
                    }
                }
                flush()
        }
        return root
    }
}
