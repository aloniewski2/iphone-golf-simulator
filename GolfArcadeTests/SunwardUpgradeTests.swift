import XCTest
import SceneKit
import simd
@testable import GolfArcade

final class SunwardUpgradeTests: XCTestCase {
    @MainActor func testResortPathIsContinuousAndAvoidsPlayableGround() throws {
        for hole in Course.sunwardResort.holes {
            let route=CourseArt.resortRoute(hole)
            XCTAssertGreaterThan(route.count,60)
            XCTAssertEqual(route,CourseArt.resortRoute(hole))
            XCTAssertLessThan(route.first!.d,hole.tee.d-30)
            XCTAssertGreaterThan(route.last!.d,hole.pin.d+30)
            for (a,b) in zip(route,route.dropFirst()) {
                XCTAssertLessThan(a.distance(to:b),2.25)
                XCTAssertLessThanOrEqual(abs(b.x-a.x)/(b.d-a.d),0.451)
                for sample in 0...8 {
                    let t=Double(sample)/8,length=a.distance(to:b)
                    for across in [-1.4,0,1.4] {
                        let p=CoursePoint(x:a.x+(b.x-a.x)*t+(b.d-a.d)/length*across,
                            d:a.d+(b.d-a.d)*t-(b.x-a.x)/length*across)
                        XCTAssertEqual(hole.lie(at:p),.outOfBounds,"Hole \(hole.number) path intersects a scored lie")
                        XCTAssertTrue(CourseArt.treeClearsClubhouse(p,radius:0,site:CourseArt.clubhouseSite(hole)))
                        XCTAssertTrue(hole.trees.allSatisfy {p.distance(to:$0.center)>$0.trunkRadius+1})
                    }
                }
            }
            let root=CourseArt.resortPath(hole)
            let geometry=try XCTUnwrap(root.childNode(withName:"sunwardResortPath",recursively:true)?.geometry)
            XCTAssertEqual(geometry.elements[0].primitiveCount,(route.count-1)*8,"No skipped rows or disconnected path stubs")
            XCTAssertLessThan(geometry.elements[0].primitiveCount,5000)
            let source=try XCTUnwrap(geometry.sources(for:.vertex).first)
            source.data.withUnsafeBytes { bytes in
                for i in 0..<source.vectorCount {
                    let offset=source.dataOffset+i*source.dataStride
                    let x=Double(bytes.loadUnaligned(fromByteOffset:offset,as:Float.self))
                    let y=Double(bytes.loadUnaligned(fromByteOffset:offset+4,as:Float.self))
                    let d = -Double(bytes.loadUnaligned(fromByteOffset:offset+8,as:Float.self))
                    let p=CoursePoint(x:x,d:d)
                    XCTAssertEqual(hole.lie(at:p),.outOfBounds)
                    XCTAssertEqual(y,Double(CourseArt.dressingHeight(p,hole:hole))+0.035,accuracy:0.001)
                }
            }
        }
    }

    @MainActor func testClubhouseFacadeHasBoundedSharedMaterialDetails() {
        let detail=CourseArt.clubhouseFacadeDetails()
        XCTAssertEqual(detail.childNodes.filter {$0.name == "sideWindow"}.count,4)
        XCTAssertEqual(detail.childNodes.filter {$0.name == "rearWindow"}.count,3)
        XCTAssertNotNil(detail.childNode(withName:"entranceDoor",recursively:false))
        XCTAssertEqual(Set(detail.childNodes.compactMap {$0.geometry?.firstMaterial}.map {ObjectIdentifier($0)}).count,5)
        for node in detail.childNodes {
            let bounds=node.boundingBox
            let low=node.simdConvertPosition(simd_float3(bounds.min),to:detail)
            let high=node.simdConvertPosition(simd_float3(bounds.max),to:detail)
            XCTAssertGreaterThanOrEqual(low.x,-14);XCTAssertLessThanOrEqual(high.x,14)
            XCTAssertGreaterThanOrEqual(low.z,-15);XCTAssertLessThanOrEqual(high.z,12)
            XCTAssertGreaterThanOrEqual(low.y,0);XCTAssertLessThanOrEqual(high.y,8)
        }
    }

    @MainActor func testOptInCanopyLightingStudy() throws {
        guard ProcessInfo.processInfo.environment["GOLF_LIGHTING_STUDY"] == "1" else {throw XCTSkip("Opt-in matched lighting study")}
        let course=CourseScene(),hole=Course.sunwardResort.holes[0]
        primeCourseReview(course,hole:hole)
        let sunNode=try XCTUnwrap(course.scene.rootNode.childNode(withName:"sunwardWarmKey",recursively:true))
        let sun=try XCTUnwrap(sunNode.light)
        let fill=try XCTUnwrap(course.scene.rootNode.childNode(withName:"sunwardSkyBounce",recursively:true)?.light)
        let currentIntensity=sun.intensity,currentColor=sun.color,currentAngles=sunNode.eulerAngles
        let currentFill=fill.intensity,currentEnvironment=course.scene.lightingEnvironment.intensity
        let currentExposure=course.camera.camera!.exposureOffset
        let renderer=SCNRenderer(device:nil,options:nil);renderer.scene=course.scene;renderer.pointOfView=course.camera
        let folder=FileManager.default.urls(for:.documentDirectory,in:.userDomainMask)[0].appendingPathComponent("CanopyLightingStudy")
        try FileManager.default.createDirectory(at:folder,withIntermediateDirectories:true)
        for name in ["current","side-key","back-key","back-key-soft"] {
            sun.intensity=name == "current" ? currentIntensity : name == "back-key" ? 1700 : 1500
            sun.color=name == "current" ? currentColor : UIColor(red:1,green:0.91,blue:0.78,alpha:1)
            sunNode.eulerAngles=name == "current" ? currentAngles : name == "side-key" ? SCNVector3(-0.6,1,0) : SCNVector3(-0.52,2.1,0)
            fill.intensity=name == "current" ? currentFill : name == "back-key-soft" ? 220 : 160
            course.scene.lightingEnvironment.intensity=name == "current" ? currentEnvironment : name == "back-key" ? 0.65 : 0.85
            course.camera.camera?.exposureOffset=name == "current" ? currentExposure : 0.12
            for (view,eye,target) in [
                ("rough",SCNVector3(hole.pin.x+23,hole.surface(at:.init(x:hole.pin.x+23,d:hole.pin.d-29)).heightYards+1.4,-hole.pin.d+29),SCNVector3(hole.pin.x,hole.surface(at:hole.pin).heightYards+1,-hole.pin.d-8)),
                ("film",SCNVector3(hole.pin.x+23,hole.surface(at:hole.pin).heightYards+7,-hole.pin.d+29),SCNVector3(hole.pin.x,3,-hole.pin.d-17)),
                ("tee",SCNVector3(hole.tee.x+5,hole.surface(at:hole.tee).heightYards+3.5,-hole.tee.d+7),SCNVector3(hole.tee.x,3,-hole.tee.d-60))] {
                course.camera.position=eye
                course.camera.look(at:target,up:SCNVector3(0,1,0),localFront:SCNVector3(0,0,-1));course.camera.camera?.fieldOfView=54
                course.updatePinBeacon(pin:hole.pin,sunk:false)
                let image=renderer.snapshot(atTime:0,with:CGSize(width:1280,height:720),antialiasingMode:.multisampling4X)
                try XCTUnwrap(image.pngData()).write(to:folder.appendingPathComponent(name+"-"+view+".png"))
            }
        }
        print("CANOPY_LIGHTING_STUDY="+folder.path)
    }

    @MainActor func testRoundedCanopiesKeepTheirReservedFootprint() {
        for seed in 0..<6 {
            let tree=CourseArt.branchingTree(seed:seed)
            var minY=Float.infinity,maxY:Float=0,maxRadius:Float=0
            tree.enumerateChildNodes { node,_ in
                guard let source=node.geometry?.sources(for:.vertex).first else {return}
                source.data.withUnsafeBytes { bytes in
                    for index in 0..<source.vectorCount {
                        let offset=source.dataOffset+index*source.dataStride
                        let local=simd_float3(bytes.loadUnaligned(fromByteOffset:offset,as:Float.self),
                            bytes.loadUnaligned(fromByteOffset:offset+4,as:Float.self),bytes.loadUnaligned(fromByteOffset:offset+8,as:Float.self))
                        let p=node.simdConvertPosition(local,to:tree)
                        minY=min(minY,p.y);maxY=max(maxY,p.y);maxRadius=max(maxRadius,hypot(p.x,p.z))
                    }
                }
            }
            XCTAssertLessThan(maxRadius,4,"Tree must fit its reserved out-of-play radius")
            XCTAssertLessThan(maxY,8.5,"Rounded crowns should not revert to the narrow tall silhouette")
            XCTAssertGreaterThan(maxRadius,3.2)
            XCTAssertGreaterThan(maxY,7.5)
            XCTAssertGreaterThanOrEqual(minY,-0.001)
        }
    }

    @MainActor func testImportedNatureTreesHaveCutoutsAndBoundedGeometry() throws {
        for index in 3...5 {
            let asset=try XCTUnwrap(SunwardAsset.load("NatureTree\(index)"))
            XCTAssertTrue(asset.valid);XCTAssertTrue(asset.bones.isEmpty)
            let count=asset.meshes.reduce(0) {$0+$1.indices.count/3}
            // Crown-fitted pruning removes hidden upper branches, not trunk detail.
            XCTAssertGreaterThan(count,2000);XCTAssertLessThan(count,3000)
            var barkTop:Float=0,crownBottom=Float.infinity
            for mesh in asset.meshes {
                XCTAssertEqual(mesh.colors?.count,mesh.positions.count/3*4)
                for i in stride(from:0,to:mesh.positions.count,by:3) {
                    XCTAssertLessThan(hypot(mesh.positions[i],mesh.positions[i+2]),4)
                    XCTAssertGreaterThanOrEqual(mesh.positions[i+1],0)
                    XCTAssertLessThanOrEqual(mesh.positions[i+1],8.001)
                    let normal=simd_float3(mesh.normals[i],mesh.normals[i+1],mesh.normals[i+2])
                    XCTAssertEqual(simd_length(normal),1,accuracy:0.001)
                    if asset.materials[mesh.material].name == "sculpted-canopy" {
                        crownBottom=min(crownBottom,mesh.positions[i+1])
                    } else {barkTop=max(barkTop,mesh.positions[i+1])}
                }
            }
            XCTAssertGreaterThan(barkTop,2.8,"Preserve the modeled lower trunk")
            XCTAssertLessThan(barkTop,3.6,"Old branch tips must not protrude through the new crown")
            XCTAssertLessThan(crownBottom,barkTop-0.3,"Pruned trunk must overlap the opaque crown")
            for definition in asset.materials {
                let material=SunwardAsset.material(definition)
                if definition.texture != nil {XCTAssertNotNil(material.diffuse.contents as? UIImage)}
                XCTAssertTrue(material.writesToDepthBuffer)
                if definition.alphaCutoff != nil {
                    XCTAssertTrue(material.isDoubleSided)
                    XCTAssertEqual(material.blendMode,.replace)
                    XCTAssertTrue(material.shaderModifiers?[.surface]?.contains("discard_fragment") == true)
                }
            }
        }
        XCTAssertTrue(CourseArt.trees.allSatisfy { $0.name == "canopyTree" })
        for tree in CourseArt.trees {
            XCTAssertTrue(tree.geometry?.materials.contains(where:{$0.name == "sculpted-canopy"}) == true,
                "Runtime groves must use the imported asset, not silently fall back")
        }
    }

    @MainActor func testLocalTreeShadeFollowsTerrainWithoutChangingCollision() throws {
        for hole in Course.sunwardResort.holes {
            let centers=hole.trees.map(\.center)
            let node=CourseArt.canopyShadows(centers,hole:hole)
            XCTAssertNil(node.physicsBody);XCTAssertFalse(node.castsShadow)
            let geometry=try XCTUnwrap(node.geometry)
            let vertices=try XCTUnwrap(geometry.sources(for:.vertex).first)
            let uv=try XCTUnwrap(geometry.sources(for:.texcoord).first)
            let colors=try XCTUnwrap(geometry.sources(for:.color).first)
            XCTAssertEqual(vertices.vectorCount,centers.count*344)
            XCTAssertEqual(geometry.elements[0].primitiveCount,centers.count*576)
            XCTAssertEqual(geometry.materials.count,1)
            XCTAssertFalse(geometry.firstMaterial!.writesToDepthBuffer)
            var rootCount=0
            vertices.data.withUnsafeBytes { points in uv.data.withUnsafeBytes { tex in colors.data.withUnsafeBytes { rgba in
                for index in 0..<vertices.vectorCount {
                    let mode=tex.loadUnaligned(fromByteOffset:uv.dataOffset+index*uv.dataStride,as:Float.self)
                    guard mode == 1 else {continue};rootCount += 1
                    let offset=vertices.dataOffset+index*vertices.dataStride
                    let p=CoursePoint(x:Double(points.loadUnaligned(fromByteOffset:offset,as:Float.self)),d:-Double(points.loadUnaligned(fromByteOffset:offset+8,as:Float.self)))
                    let y=points.loadUnaligned(fromByteOffset:offset+4,as:Float.self)
                    XCTAssertEqual(y,CourseArt.dressingHeight(p,hole:hole)+0.030,accuracy:0.001)
                    let center=centers[index/344]
                    XCTAssertLessThanOrEqual(center.distance(to:p),2.601)
                    let alpha=rgba.loadUnaligned(fromByteOffset:colors.dataOffset+index*colors.dataStride+12,as:Float.self)
                    XCTAssertGreaterThanOrEqual(alpha,0);XCTAssertLessThanOrEqual(alpha,0.161)
                    if index%344>=320 {XCTAssertEqual(alpha,0,accuracy:0.00001)}
                }
            } } }
            XCTAssertEqual(rootCount,centers.count*120)
        }
    }

    @MainActor func testBunkerSoilCollarFollowsAuthoritativeSandEdge() {
        for hole in Course.sunwardResort.holes {
            let node=CourseArt.bunkerSoilCollar(hole),geometry=node.geometry!
            XCTAssertLessThan(geometry.elements[0].primitiveCount,10000)
            XCTAssertEqual(geometry.materials.count,1)
            let source=geometry.sources(for:.vertex)[0]
            source.data.withUnsafeBytes { bytes in
                for index in 0..<source.vectorCount {
                    let offset=source.dataOffset+index*source.dataStride
                    let x=Double(bytes.loadUnaligned(fromByteOffset:offset,as:Float.self))
                    let y=Double(bytes.loadUnaligned(fromByteOffset:offset+4,as:Float.self))
                    let d = -Double(bytes.loadUnaligned(fromByteOffset:offset+8,as:Float.self))
                    let point=CoursePoint(x:x,d:d)
                    XCTAssertEqual(y,hole.surface(at:point).heightYards+0.009,accuracy:0.0001)
                    XCTAssertTrue(hole.hazards.contains { hazard in
                        guard hazard.kind == .bunker else {return false}
                        let radial=hazard.radialSurface(at:point)
                        let distance=(1-radial.radius)/max(0.0001,hypot(radial.dx,radial.dd))
                        return distance >= -0.0001 && distance<0.22
                    },"Soil must remain within the narrow sand-side collar")
                }
            }
        }
    }

    @MainActor func testClippedSandAndGreenTrianglesRespectScoringRegions() {
        for hole in Course.sunwardResort.holes {
            let geometry=CourseArt.playableSurface(hole),source=geometry.sources(for:.vertex)[0]
            let vertices:[CoursePoint]=source.data.withUnsafeBytes { bytes in
                (0..<source.vectorCount).map { index in
                    let offset=source.dataOffset+index*source.dataStride
                    return CoursePoint(x:Double(bytes.loadUnaligned(fromByteOffset:offset,as:Float.self)),
                        d:-Double(bytes.loadUnaligned(fromByteOffset:offset+8,as:Float.self)))
                }
            }
            for (material,lie) in [(3,CourseLie.bunker),(1,.green)] {
                let element=geometry.elements[material]
                let indices:[Int32]=element.data.withUnsafeBytes { bytes in
                    (0..<element.primitiveCount*3).map {bytes.loadUnaligned(fromByteOffset:$0*4,as:Int32.self)}
                }
                var worst:CoursePoint?,mismatches=0
                for i in stride(from:0,to:indices.count,by:3) {
                    let a=vertices[Int(indices[i])],b=vertices[Int(indices[i+1])],c=vertices[Int(indices[i+2])]
                    let center=CoursePoint(x:(a.x+b.x+c.x)/3,d:(a.d+b.d+c.d)/3)
                    guard hole.lie(at:center) != lie else {continue}
                    // Sub-centimeter float/root error and curved-edge chords
                    // are distinct from the old quarter-yard material spikes.
                    let nearBoundary=(0..<12).contains { step in
                        let angle=Double(step)*2 * .pi/12
                        return hole.lie(at:.init(x:center.x+cos(angle)*0.025,d:center.d+sin(angle)*0.025)) == lie
                    }
                    if !nearBoundary {mismatches+=1;worst=center}
                }
                XCTAssertEqual(mismatches,0,"Hole \(hole.number), \(lie): material crosses the scoring edge near \(String(describing:worst))")
            }
        }
    }

    @MainActor func testBackgroundReliefHasNoClampedSlopeCrease() {
        XCTAssertEqual(CourseArt.backgroundRamp(-1),0)
        XCTAssertEqual(CourseArt.backgroundRamp(81),1)
        for edge in [0.0,80.0] {
            let h=0.001
            let left=(CourseArt.backgroundRamp(edge)-CourseArt.backgroundRamp(edge-h))/h
            let right=(CourseArt.backgroundRamp(edge+h)-CourseArt.backgroundRamp(edge))/h
            XCTAssertEqual(left,right,accuracy:0.000001)
            XCTAssertEqual(left,0,accuracy:0.000001)
        }
        for a in stride(from:-20.0,through:120.0,by:1.0) {
            for b in stride(from:-20.0,through:120.0,by:7.0) {
                XCTAssertLessThanOrEqual(CourseArt.blendedClearance(a,b),min(a,b))
            }
        }
        let h=0.001
        let left=(CourseArt.blendedClearance(40,40)-CourseArt.blendedClearance(40-h,40))/h
        let right=(CourseArt.blendedClearance(40+h,40)-CourseArt.blendedClearance(40,40))/h
        XCTAssertEqual(left,right,accuracy:0.0001,"Nearest protected regions must meet without a ridge crease")
    }

    @MainActor func testResortFoothillsAndDistantRidgeJoinSmoothly() {
        for x in stride(from:-320.0,through:320,by:40) {
            for d in stride(from:-100.0,through:800,by:60) {
                let point=CoursePoint(x:x,d:d)
                let near=CourseArt.resortBackdropRelief(point,clearance:80)
                let far=CourseArt.resortBackdropRelief(point,clearance:240)
                XCTAssertGreaterThanOrEqual(near,-0.001)
                XCTAssertLessThanOrEqual(near,16.001)
                XCTAssertGreaterThanOrEqual(far-near,8.999)
                XCTAssertLessThanOrEqual(far-near,23.001)
                for edge in [100.0,220.0] {
                    let h=0.001,value=CourseArt.resortBackdropRelief(point,clearance:edge)
                    let left=(value-CourseArt.resortBackdropRelief(point,clearance:edge-h))/h
                    let right=(CourseArt.resortBackdropRelief(point,clearance:edge+h)-value)/h
                    XCTAssertEqual(left,right,accuracy:0.000001)
                }
            }
        }
    }

    @MainActor func testLandscapeSeamDiagnosticCaptures() throws {
        let course=CourseScene(),hole=Course.sunwardResort.holes[0]
        primeCourseReview(course,hole:hole)
        let lake=try XCTUnwrap(hole.hazards.first {$0.kind == .water})
        course.camera.position=SCNVector3(lake.x+lake.width/2+12,12,-lake.distance+24)
        course.camera.look(at:SCNVector3(lake.x,0,-lake.distance-15),up:SCNVector3(0,1,0),localFront:SCNVector3(0,0,-1));course.camera.camera?.fieldOfView=54
        let sun=try XCTUnwrap(course.scene.rootNode.childNode(withName:"sunwardWarmKey",recursively:true)?.light)
        let sunNode=try XCTUnwrap(course.scene.rootNode.childNode(withName:"sunwardWarmKey",recursively:true))
        let direction=sunNode.simdOrientation.act(simd_float3(0,0,1))
        XCTAssertLessThan(simd_distance(direction,CourseArt.sunDirection),0.00001,
            "Sky glow and distant canopy shade must use the actual light direction")
        let renderer=SCNRenderer(device:nil,options:nil);renderer.scene=course.scene;renderer.pointOfView=course.camera
        let folder=FileManager.default.urls(for:.documentDirectory,in:.userDomainMask)[0].appendingPathComponent("LandscapeSeamReview")
        try FileManager.default.createDirectory(at:folder,withIntermediateDirectories:true)
        let normal=CourseArt.roughMaterial.normal.intensity
        defer {CourseArt.roughMaterial.normal.intensity=normal}
        for name in ["baseline","no-shadow","no-normal","no-fog"] {
            sun.castsShadow=name != "no-shadow"
            CourseArt.roughMaterial.normal.intensity=name == "no-normal" ? 0 : normal
            course.scene.fogEndDistance=name == "no-fog" ? 100000 : 620
            course.scene.fogStartDistance=name == "no-fog" ? 99000 : 160
            let image=renderer.snapshot(atTime:0,with:CGSize(width:1280,height:720),antialiasingMode:.multisampling4X)
            try XCTUnwrap(image.pngData()).write(to:folder.appendingPathComponent(name+".png"))
        }
        print("LANDSCAPE_SEAM_REVIEW="+folder.path)
    }

    @MainActor func testFineTerrainIncludesEveryHazardAndItsOuterBank() {
        for hole in Course.sunwardResort.holes {
            let limits=CourseArt.fineSurfaceExtents(hole)
            for hazard in hole.hazards {
                for i in 0..<128 {
                    let a=Double(i)*2 * .pi/128,scale=hazard.boundaryScale(at:a)
                    for offset in [0.0,1.0,3.0,8.0] {
                        let p=CoursePoint(x:hazard.x+cos(a)*(hazard.width/2*scale+offset),
                            d:hazard.distance+sin(a)*(hazard.length/2*scale+offset))
                        XCTAssertGreaterThan(p.x-limits.x,8);XCTAssertGreaterThan(limits.y-p.x,8)
                        XCTAssertGreaterThan(p.d-limits.z,8);XCTAssertGreaterThan(limits.w-p.d,8)
                        XCTAssertEqual(Double(CourseArt.surfaceHeight(p,hole:hole)),hole.surface(at:p).heightYards,accuracy:0.0001,
                            "The coarse-landscape transition must not alter a hazard bank")
                    }
                }
            }
        }
    }

    @MainActor func testShorelinePlantingRespectsPlayableLiesAndWaterBoundary() throws {
        var total=0
        for hole in Course.sunwardResort.holes {
            let sites=CourseArt.shorelinePlantingSites(hole);total += sites.count
            XCTAssertEqual(sites,CourseArt.shorelinePlantingSites(hole))
            for site in sites {
                for i in 0..<32 {
                    let a=Double(i)*2 * .pi/32
                    let p=CoursePoint(x:site.x+cos(a)*0.42,d:site.d+sin(a)*0.42)
                    XCTAssertEqual(hole.lie(at:p),.outOfBounds)
                    XCTAssertFalse(hole.hazards.contains {$0.contains(p)})
                }
            }
            let node=CourseArt.shorelinePlanting(hole),geometry=try XCTUnwrap(node.geometry)
            XCTAssertTrue(node.childNodes.isEmpty);XCTAssertNil(node.physicsBody)
            XCTAssertEqual(geometry.elements[0].primitiveCount,sites.count*21)
            XCTAssertLessThan(sites.count*21,30_000)
            XCTAssertEqual(geometry.materials.count,1)
            let source=try XCTUnwrap(geometry.sources(for:.vertex).first)
            source.data.withUnsafeBytes { bytes in
                for (index,site) in sites.enumerated() {
                    for vertex in 0..<35 {
                        let offset=source.dataOffset+(index*35+vertex)*source.dataStride
                        let x=Double(bytes.loadUnaligned(fromByteOffset:offset,as:Float.self))
                        let y=Double(bytes.loadUnaligned(fromByteOffset:offset+4,as:Float.self))
                        let d = -Double(bytes.loadUnaligned(fromByteOffset:offset+8,as:Float.self))
                        XCTAssertLessThan(hypot(x-site.x,d-site.d),0.42)
                        XCTAssertEqual(hole.lie(at:CoursePoint(x:x,d:d)),.outOfBounds)
                        let base=Double(CourseArt.surfaceHeight(site,hole:hole))
                        XCTAssertGreaterThanOrEqual(y,base-0.016);XCTAssertLessThan(y,base+0.81)
                    }
                }
            }
        }
        XCTAssertGreaterThan(total,100)
    }

    @MainActor func testBunkerEdgeTurfStaysOnGrassAndIsBounded() {
        for hole in Course.sunwardResort.holes {
            let sites=CourseArt.bunkerTurfSites(hole)
            XCTAssertGreaterThan(sites.count,100)
            for site in sites {
                XCTAssertTrue([CourseLie.rough,.deepRough,.fringe].contains(hole.lie(at:site)))
                for i in 0..<8 {
                    let a=Double(i)*2 * .pi/8
                    let p=CoursePoint(x:site.x+cos(a)*0.04,d:site.d+sin(a)*0.04)
                    XCTAssertFalse(hole.hazards.contains {$0.contains(p)})
                }
            }
            let node=CourseArt.bunkerEdgeTurf(hole)
            XCTAssertEqual(node.childNodes.count,0)
            XCTAssertEqual(node.geometry?.elements[0].primitiveCount,sites.count*3)
            XCTAssertLessThan(sites.count*3,30_000)
        }
    }

    @MainActor func testBatchedGreenReadTracksPlayableSurface() throws {
        for hole in Course.sunwardResort.holes {
            let samples=CourseArt.greenReadSamples(hole)
            XCTAssertGreaterThan(samples.count,100)
            for sample in samples {
                let surface=hole.surface(at:sample.point)
                XCTAssertLessThanOrEqual(Double(sample.flow.x)*surface.slopeX-Double(sample.flow.y)*surface.slopeD,0.000001)
                for step in -10...10 {
                    let travel=Double(step)/10
                    let point=CoursePoint(x:sample.point.x+Double(sample.flow.x)*travel,d:sample.point.d-Double(sample.flow.y)*travel)
                    XCTAssertEqual(hole.lie(at:point),.green)
                    XCTAssertGreaterThan(point.distance(to:hole.pin),0.6)
                    let height=sample.height+Double(sample.curve.x)*travel+Double(sample.curve.y)*travel*travel
                    XCTAssertEqual(height,hole.surface(at:point).heightYards,accuracy:0.01)
                }
            }
            let geometry=CourseArt.greenReadGeometry(hole)
            XCTAssertEqual(geometry.elements.count,1)
            XCTAssertEqual(geometry.elements[0].primitiveCount,samples.count*8)
            XCTAssertEqual(geometry.sources(for:.vertex)[0].vectorCount,samples.count*9)
        }
    }

    @MainActor func testWaterLightResponseMovesAndHonorsReducedMotion() throws {
        let hole=Course.sunwardResort.holes[0],course=CourseScene()
        primeCourseReview(course,hole:hole)
        let material=course.lagoonMaterial
        defer {material.setValue(Float(0),forKey:"sunwardTime")}
        course.stepForTesting(now:12)
        XCTAssertEqual((material.value(forKey:"sunwardTime") as? NSNumber)?.floatValue,0)
        course.inputs?.reduceMotion=false;course.stepForTesting(now:12)
        XCTAssertEqual((material.value(forKey:"sunwardTime") as? NSNumber)?.floatValue,12)
        course.inputs?.reduceMotion=true;course.stepForTesting(now:24)
        XCTAssertEqual((material.value(forKey:"sunwardTime") as? NSNumber)?.floatValue,0)
        let lake=try XCTUnwrap(hole.hazards.first {$0.kind == .water})
        course.camera.position=SCNVector3(lake.x+lake.width/2+12,12,-lake.distance+24)
        course.camera.look(at:SCNVector3(lake.x,0,-lake.distance-15),up:SCNVector3(0,1,0),localFront:SCNVector3(0,0,-1))
        course.camera.camera?.fieldOfView=54
        let renderer=SCNRenderer(device:nil,options:nil);renderer.scene=course.scene;renderer.pointOfView=course.camera
        let folder=FileManager.default.urls(for:.documentDirectory,in:.userDomainMask)[0].appendingPathComponent("WaterMotionReview")
        try FileManager.default.createDirectory(at:folder,withIntermediateDirectories:true)
        var frames:[Data]=[],pixels:[[UInt8]]=[]
        for (name,time) in [("wave-a",Float(0)),("wave-b",Float(2.5))] {
            material.setValue(time,forKey:"sunwardTime")
            let image=renderer.snapshot(atTime:0,with:CGSize(width:1280,height:720),antialiasingMode:.multisampling4X)
            let png=try XCTUnwrap(image.pngData());frames.append(png)
            pixels.append(Array(try XCTUnwrap(image.cgImage?.dataProvider?.data) as Data))
            try png.write(to:folder.appendingPathComponent(name+".png"))
        }
        XCTAssertNotEqual(frames[0],frames[1],"Changing wave time must change the rendered light response")
        XCTAssertEqual(pixels[0].count,pixels[1].count)
        let meanDelta=zip(pixels[0],pixels[1]).reduce(0.0) {$0+Double(abs(Int($1.0)-Int($1.1)))}/Double(pixels[0].count)
        XCTAssertGreaterThan(meanDelta,0.05,"Verify changed pixels, not only different PNG metadata")
        print("WATER_WAVE_PIXEL_MEAN_DELTA=\(meanDelta)")
        print("WATER_MOTION_REVIEW="+folder.path)
    }

    @MainActor func testBatchedGreenReadRenderAndReducedMotion() throws {
        let hole=Course.sunwardResort.holes[0],course=CourseScene()
        let ball=try XCTUnwrap(CourseArt.greenReadSamples(hole).first).point
        course.inputs=SceneInputs(hole:hole,ball:ball,heading:ball.heading(to:hole.pin),
            distanceToPin:ball.distance(to:hole.pin),lie:.green,club:.putter,
            aim:0,handedness:.right,shot:nil,isReplay:false,flightStart:nil,pausedAt:nil,
            swingAngle:0,bystanders:[],reduceMotion:true)
        course.start();course.stop()
        let grid=try XCTUnwrap(course.scene.rootNode.childNode(withName:"greenReadGrid",recursively:true))
        XCTAssertFalse(grid.isHidden)
        XCTAssertTrue(grid.childNodes.isEmpty)
        let material=try XCTUnwrap(grid.geometry?.firstMaterial)
        XCTAssertEqual((material.value(forKey:"readMotion") as? NSNumber)?.floatValue,0)
        course.camera.position=SCNVector3(hole.pin.x+5,hole.surface(at:hole.pin).heightYards+12,-hole.pin.d+18)
        course.camera.look(at:SCNVector3(hole.pin.x,hole.surface(at:hole.pin).heightYards,-hole.pin.d))
        course.camera.camera?.fieldOfView=54
        course.updatePinBeacon(pin:hole.pin,sunk:false)
        let renderer=SCNRenderer(device:nil,options:nil);renderer.scene=course.scene;renderer.pointOfView=course.camera
        let folder=FileManager.default.urls(for:.documentDirectory,in:.userDomainMask)[0].appendingPathComponent("CourseQualityReview")
        try FileManager.default.createDirectory(at:folder,withIntermediateDirectories:true)
        // Compile and exercise the shader while visible, not only during tee captures.
        for (name,time,motion) in [("green-read-still",Float(0),Float(0)),("green-read-a",Float(0),Float(1)),("green-read-b",Float(1.1),Float(1))] {
            material.setValue(time,forKey:"readTime");material.setValue(motion,forKey:"readMotion")
            let image=renderer.snapshot(atTime:0,with:CGSize(width:1280,height:720),antialiasingMode:.multisampling4X)
            try XCTUnwrap(image.pngData()).write(to:folder.appendingPathComponent(name+".png"))
            let attachment=XCTAttachment(image:image);attachment.name=name;attachment.lifetime = .keepAlways;add(attachment)
        }
    }

    @MainActor func testProductionSkyAndShadowCoverageConfiguration() throws {
        XCTAssertEqual(CourseArt.sky.size.width,CourseArt.sky.size.height*2,
            "SceneKit needs a 2:1 image to interpret the sky as a spherical projection")
        let course=CourseScene();course.stop()
        let sun=try XCTUnwrap(course.scene.rootNode.childNode(withName:"sunwardWarmKey",recursively:true)?.light)
        XCTAssertTrue(sun.castsShadow)
        XCTAssertEqual(sun.shadowCascadeCount,3)
        XCTAssertGreaterThanOrEqual(sun.maximumShadowDistance,200,
            "The 65-yard cutoff omitted most terrain shadows from elevated course cameras")
        XCTAssertNotNil(CourseArt.grassBladeMaterial.diffuse.contents as? UIImage,
            "Blades and the underlying rough must share their world-aligned albedo")
    }

    func testHazardBoundsAndCachedRegionsPreserveClassification() throws {
        for hole in Course.sunwardResort.holes {
            let region=try XCTUnwrap(hole.fairwayBoundary)
            for i in 0..<500 {
                let p=CoursePoint(x:sin(Double(i)*1.37)*220,d:Double(i%73)*8-20)
                var expected=false,previous=try XCTUnwrap(region.points.last)
                for current in region.points {
                    if (current.d>p.d) != (previous.d>p.d),
                       p.x<(previous.x-current.x)*(p.d-current.d)/(previous.d-current.d)+current.x {expected.toggle()}
                    previous=current
                }
                XCTAssertEqual(region.contains(p),expected)
                for hazard in hole.hazards {
                    XCTAssertEqual(hazard.contains(p),hazard.radialSurface(at:p).radius<=1)
                }
            }
        }
        XCTAssertFalse(CourseRegion(points:[]).contains(.zero))
        XCTAssertEqual(CourseRegion(points:[]).distance(to:.zero),.infinity)
    }

    @MainActor func testLightingCoverageReview() throws {
        let course=CourseScene();course.stop()
        let hole=Course.sunwardResort.holes[0];course.load(hole)
        let renderer=SCNRenderer(device:nil,options:nil);renderer.scene=course.scene;renderer.pointOfView=course.camera
        let sun=try XCTUnwrap(course.scene.rootNode.childNode(withName:"sunwardWarmKey",recursively:true)?.light)
        let folder=FileManager.default.urls(for:.documentDirectory,in:.userDomainMask)[0].appendingPathComponent("LightingCoverageReview")
        try FileManager.default.createDirectory(at:folder,withIntermediateDirectories:true)
        course.camera.position=SCNVector3(hole.pin.x+23,hole.surface(at:hole.pin).heightYards+7,-hole.pin.d+29)
        course.camera.look(at:SCNVector3(hole.pin.x,3,-hole.pin.d-17));course.camera.camera?.fieldOfView=54
        for name in ["current","cascaded","daylight","direct-only","reverse-key"] {
            if name != "current" {
                sun.maximumShadowDistance=220;sun.shadowCascadeCount=3;sun.shadowCascadeSplittingFactor=0.7
                sun.automaticallyAdjustsShadowProjection=true;sun.shadowBias=0.015
                sun.shadowColor=UIColor(red:0.20,green:0.15,blue:0.24,alpha:0.72)
            }
            if name == "daylight" {
                sun.color=UIColor(red:1,green:0.94,blue:0.84,alpha:1)
                sun.intensity=1250;course.scene.lightingEnvironment.intensity=0.9
                course.camera.camera?.exposureOffset=0.25
            }
            if name == "direct-only" {
                course.scene.lightingEnvironment.intensity=0
                for node in course.scene.rootNode.childNodes where node.name != "sunwardWarmKey" { node.light?.intensity=0 }
                sun.intensity=1100;course.camera.camera?.exposureOffset=0
            }
            if name == "reverse-key" {
                course.scene.rootNode.childNode(withName:"sunwardWarmKey",recursively:true)?.eulerAngles=SCNVector3(-0.45,-1.0,0)
            }
            let capture=renderer.snapshot(atTime:0,with:CGSize(width:1280,height:720),antialiasingMode:.multisampling4X)
            try XCTUnwrap(capture.pngData()).write(to:folder.appendingPathComponent(name+".png"))
        }
        print("LIGHTING_COVERAGE_REVIEW="+folder.path)
    }

    func testOptimizedBoundaryDistanceMatchesSegmentProjection() throws {
        for hole in Course.sunwardResort.holes {
            let region=try XCTUnwrap(hole.fairwayBoundary)
            for sample in 0..<60 {
                let point=CoursePoint(x:sin(Double(sample)*1.7)*160,d:Double(sample)*9)
                var expected=Double.infinity
                for i in region.points.indices {
                    let a=region.points[i],b=region.points[(i+1)%region.points.count]
                    let dx=b.x-a.x,dd=b.d-a.d
                    let t=max(0,min(1,((point.x-a.x)*dx+(point.d-a.d)*dd)/max(0.0001,dx*dx+dd*dd)))
                    expected=min(expected,hypot(point.x-(a.x+t*dx),point.d-(a.d+t*dd)))
                }
                XCTAssertEqual(region.distance(to:point),expected,accuracy:0.000000001)
            }
        }
    }

    func testFairwayBoundariesDoNotCrossAtDoglegs() throws {
        func turn(_ a:CoursePoint,_ b:CoursePoint,_ c:CoursePoint)->Double {
            (b.x-a.x)*(c.d-a.d)-(b.d-a.d)*(c.x-a.x)
        }
        for hole in Course.sunwardResort.holes {
            let points=try XCTUnwrap(hole.fairwayBoundary).points
            XCTAssertGreaterThan(points.count,40)
            for i in points.indices {
                let a=points[i],b=points[(i+1)%points.count]
                XCTAssertGreaterThan(a.distance(to:b),0.001)
                for j in (i+1)..<points.count {
                    guard j != i+1, !(i == 0 && j == points.count-1) else { continue }
                    let c=points[j],d=points[(j+1)%points.count]
                    let crosses=turn(a,b,c)*turn(a,b,d) < -0.000001 && turn(c,d,a)*turn(c,d,b) < -0.000001
                    XCTAssertFalse(crosses,"Hole \(hole.number), edges \(i) and \(j)")
                }
            }
        }
    }

    @MainActor func testBorderPlantingFootprintsRemainOutsidePlay() {
        for hole in Course.sunwardResort.holes {
            let sites=CourseArt.borderPlantingSites(hole)
            XCTAssertGreaterThan(sites.count,4)
            XCTAssertLessThan(sites.count,40)
            for point in sites {
                XCTAssertTrue(CourseArt.clearsResortRoute(point,radius:3.2,route:CourseArt.resortRoute(hole)))
                for radius in [0.0,1.0,2.0,3.0] {
                    for a in stride(from:0.0,to:2 * .pi,by:0.13) {
                        XCTAssertEqual(hole.lie(at:.init(x:point.x+cos(a)*radius,
                            d:point.d+sin(a)*radius)),.outOfBounds)
                    }
                }
            }
        }
        let planting=CourseArt.borderPlanting(Course.sunwardResort.holes[0])
        var triangles=0
        planting.enumerateChildNodes { node,_ in triangles += node.geometry?.elements.reduce(0) {$0+$1.primitiveCount} ?? 0 }
        triangles += planting.geometry?.elements.reduce(0) {$0+$1.primitiveCount} ?? 0
        XCTAssertGreaterThan(triangles,1000)
        XCTAssertLessThan(triangles,120000)
        XCTAssertLessThanOrEqual(planting.geometry?.materials.count ?? 0,8)
        print("BORDER_PLANTING_TRIANGLES=\(triangles)")
    }

    @MainActor func testWoodlandBatchesStayOutsidePlayAndWithinBudget() throws {
        var total=0
        for hole in Course.sunwardResort.holes {
            let sites=CourseArt.woodlandSites(hole)
            XCTAssertEqual(sites,CourseArt.woodlandSites(hole))
            XCTAssertGreaterThan(sites.count,24);XCTAssertLessThanOrEqual(sites.count,66)
            total += sites.count
            let route=CourseArt.resortRoute(hole),lodge=CourseArt.clubhouseSite(hole)
            for site in sites {
                XCTAssertTrue(CourseArt.clearsResortRoute(site.point,radius:1.4,route:route))
                XCTAssertTrue(CourseArt.treeClearsClubhouse(site.point,radius:6,site:lodge))
                for i in 0..<64 {
                    let a=Double(i)*2 * .pi/64
                    let p=CoursePoint(x:site.point.x+cos(a)*Double(site.scale)*3.6,
                        d:site.point.d+sin(a)*Double(site.scale)*3.6)
                    XCTAssertEqual(hole.lie(at:p),.outOfBounds)
                    XCTAssertFalse(hole.hazards.contains {$0.contains(p)})
                }
            }
            let forest=CourseArt.woodland(hole)
            XCTAssertLessThanOrEqual(forest.childNodes.count,12)
            var triangles=0
            for node in forest.childNodes {
                XCTAssertNil(node.physicsBody);XCTAssertTrue(node.childNodes.isEmpty)
                let geometry=try XCTUnwrap(node.geometry)
                triangles += geometry.elements.reduce(0) {$0+$1.primitiveCount}
                if node.name?.hasPrefix("woodlandGroup") == true {
                    XCTAssertLessThanOrEqual(geometry.materials.count,4)
                    XCTAssertGreaterThanOrEqual(geometry.materials.count,2)
                    XCTAssertTrue(geometry.elements.allSatisfy {$0.primitiveCount>0})
                    let vertices=try XCTUnwrap(geometry.sources(for:.vertex).first)
                    vertices.data.withUnsafeBytes { bytes in
                        for index in 0..<vertices.vectorCount {
                            let offset=vertices.dataOffset+index*vertices.dataStride
                            let x=bytes.loadUnaligned(fromByteOffset:offset,as:Float.self)
                            let z=bytes.loadUnaligned(fromByteOffset:offset+8,as:Float.self)
                            XCTAssertEqual(hole.lie(at:.init(x:Double(x),d:-Double(z))),.outOfBounds)
                        }
                    }
                }
            }
            XCTAssertLessThan(triangles,205000)
        }
        print("WOODLAND_TREES=\(total)")
    }

    @MainActor func testBackdropGrovesAreBoundedAndOutsidePlayableLies() {
        for hole in Course.sunwardResort.holes {
            let points=CourseArt.backdropGrovePoints(hole)
            XCTAssertGreaterThan(points.count,8)
            XCTAssertLessThanOrEqual(points.count,38)
            for point in points {
                for angle in stride(from:0.0,to:2 * .pi,by:0.1) {
                    XCTAssertEqual(hole.lie(at:.init(x:point.x+cos(angle)*6.5,
                        d:point.d+sin(angle)*6.5)),.outOfBounds)
                }
            }
        }
    }

    @MainActor func testClubhouseFootprintStaysOutsidePlayableCourse() {
        for hole in Course.sunwardResort.holes {
            let site=CourseArt.clubhouseSite(hole)
            for x in stride(from:-20.0,through:16,by:1) {
                for d in stride(from:-12.0,through:15,by:1) {
                    XCTAssertEqual(hole.lie(at:.init(x:site.x+x,d:site.d+d)),.outOfBounds,"Hole \(hole.number)")
                }
            }
        }
    }

    func testBunkerFloorsAreRecessedAndGentlyGraded() throws {
        for hole in Course.sunwardResort.holes {
            for bunker in hole.hazards where bunker.kind == .bunker {
                let floor=try XCTUnwrap(hole.bunkerFloors[bunker.id])
                for angle in stride(from:0.0,to:2 * .pi,by:0.1) {
                    let radius=bunker.boundaryScale(at:angle)
                    let rim=CoursePoint(x:bunker.x+cos(angle)*bunker.width/2*radius,
                        d:bunker.distance+sin(angle)*bunker.length/2*radius)
                    XCTAssertLessThan(floor,hole.terrain.elevation(at:rim)-0.85)
                    for fraction in [0.0,0.2,0.37,0.4,0.51,0.52,0.53,0.65,0.85,0.999] {
                        let p=CoursePoint(x:bunker.x+(rim.x-bunker.x)*fraction,
                            d:bunker.distance+(rim.d-bunker.distance)*fraction)
                        let surface=hole.surface(at:p),e=0.0001
                        let dx=(hole.surface(at:.init(x:p.x+e,d:p.d)).heightYards-hole.surface(at:.init(x:p.x-e,d:p.d)).heightYards)/(2*e)
                        let dd=(hole.surface(at:.init(x:p.x,d:p.d+e)).heightYards-hole.surface(at:.init(x:p.x,d:p.d-e)).heightYards)/(2*e)
                        XCTAssertEqual(surface.slopeX,dx,accuracy:0.0001)
                        XCTAssertEqual(surface.slopeD,dd,accuracy:0.0001)
                        if fraction < 0.52 {
                            XCTAssertLessThanOrEqual(hypot(surface.slopeX,surface.slopeD),0.018)
                        }
                    }
                }
            }
        }
    }

    func testLoftedEscapeClearsEveryResortBunkerFromItsFloor() {
        for hole in Course.sunwardResort.holes {
            for bunker in hole.hazards where bunker.kind == .bunker {
                let origin=CoursePoint(x:bunker.x,d:bunker.distance)
                for heading in stride(from:0.0,to:360,by:45) {
                    let request=ShotRequest(club:.wedge,targetHeading:heading,type:.bunker,
                        execution:SwingImpact(power:1),simulationVersion:hole.simulationVersion)
                    let shot=RangeShot(id:1,request:request,origin:origin,lieFactor:CourseLie.bunker.powerFactor,hole:hole)
                    XCTAssertFalse(shot.flight.events.contains {$0.kind == .bunkerLip},
                        "Hole \(hole.number), bunker \(bunker.id), heading \(heading)")
                }
            }
        }
    }

    @MainActor func testCoarseLandscapeDoesNotRemainUnderFineCourseMesh() throws {
        let hole=Course.sunwardResort.holes[0],mesh=CourseArt.landscape(Course.sunwardResort.holes[0])
        let source=try XCTUnwrap(mesh.sources(for:.vertex).first)
        let element=try XCTUnwrap(mesh.elements.first)
        let bounds=hole.centerline+(hole.fairwayBoundary?.points ?? [])+(hole.greenBoundary?.points ?? [])
        let minX=try XCTUnwrap(bounds.map(\.x).min())-35,maxX=try XCTUnwrap(bounds.map(\.x).max())+35
        let minD=try XCTUnwrap(bounds.map(\.d).min())-35,maxD=try XCTUnwrap(bounds.map(\.d).max())+35
        source.data.withUnsafeBytes { vertices in
            element.data.withUnsafeBytes { indices in
                for triangle in 0..<element.primitiveCount {
                    let inside=(0..<3).allSatisfy { corner in
                        let index=Int(indices.loadUnaligned(fromByteOffset:(triangle*3+corner)*4,as:Int32.self))
                        let offset=source.dataOffset+index*source.dataStride
                        let x=Double(vertices.loadUnaligned(fromByteOffset:offset,as:Float.self))
                        let d = -Double(vertices.loadUnaligned(fromByteOffset:offset+8,as:Float.self))
                        return x>minX && x<maxX && d>minD && d<maxD
                    }
                    XCTAssertFalse(inside)
                }
            }
        }
    }

    func testBunkerEdgesJoinTerrainWithoutTiltingAdjacentGreens() {
        for hole in Course.sunwardResort.holes {
            for bunker in hole.hazards where bunker.kind == .bunker {
                for angle in stride(from:0.0,to:2 * .pi,by:0.1) {
                    let radius=bunker.boundaryScale(at:angle)*1.0001
                    let p=CoursePoint(x:bunker.x+cos(angle)*bunker.width/2*radius,
                        d:bunker.distance+sin(angle)*bunker.length/2*radius)
                    let surface=hole.surface(at:p),base=hole.terrain.gradient(at:p)
                    XCTAssertEqual(surface.heightYards,hole.terrain.elevation(at:p),accuracy:0.0001)
                    XCTAssertEqual(surface.slopeX,base.dx,accuracy:0.0001)
                    XCTAssertEqual(surface.slopeD,base.dd,accuracy:0.0001)
                }
            }
        }
    }

    func testFinishTransitionsPreserveArticulatedLimbsAndClub() {
        let finish=AuthoredGolfMotion.swing(degrees:-150,club:.driver,type:.full)
        let targets=[AvatarAnimations.address]+AvatarAnimations.Reaction.allCases.map {
            AuthoredGolfMotion.reaction($0,time:0.8)
        }
        for target in targets {
            var previous=finish
            for sample in 1..<100 {
                let pose=AuthoredGolfMotion.transition(finish,target,Float(sample)/100)
                for (s,e,w) in [(BodyJoint.leftShoulder,BodyJoint.leftElbow,BodyJoint.leftWrist),(.rightShoulder,.rightElbow,.rightWrist)] {
                    XCTAssertEqual(simd_distance(pose[s],pose[e]),AvatarSize.upperArm,accuracy:0.002)
                    XCTAssertEqual(simd_distance(pose[e],pose[w]),AvatarSize.forearm,accuracy:0.002)
                }
                for (h,k,a) in [(BodyJoint.leftHip,BodyJoint.leftKnee,BodyJoint.leftAnkle),(.rightHip,.rightKnee,.rightAnkle)] {
                    XCTAssertEqual(simd_distance(pose[h],pose[k]),AvatarSize.thigh,accuracy:0.002)
                    XCTAssertEqual(simd_distance(pose[k],pose[a]),AvatarSize.shin,accuracy:0.002)
                }
                XCTAssertEqual(simd_distance(pose.clubGrip,pose.clubHead),simd_distance(finish.clubGrip,finish.clubHead),accuracy:0.005)
                for joint in BodyJoint.allCases {
                    XCTAssertLessThan(simd_distance(previous[joint],pose[joint]),0.22,"Transition snap at \(sample), \(joint)")
                }
                previous=pose
            }
        }
    }

    func testReturnToAddressUsesTheReviewedArcWithoutMovingImpact() {
        for (club,type) in [(GolfClub.driver,ShotType.full),(.iron,.full),(.wedge,.pitch),(.wedge,.chip),(.wedge,.bunker),(.putter,.putt)] {
            let start=AuthoredGolfMotion.returnToAddress(progress:0,club:club,type:type)
            let finish=AuthoredGolfMotion.swing(degrees:-150,club:club,type:type)
            let end=AuthoredGolfMotion.returnToAddress(progress:1,club:club,type:type)
            for joint in BodyJoint.allCases {
                XCTAssertLessThan(simd_distance(start[joint],finish[joint]),0.00001)
                XCTAssertLessThan(simd_distance(end[joint],AvatarAnimations.address[joint]),0.00001)
            }
            XCTAssertLessThan(simd_distance(end.clubHead,AvatarSize.ball),0.00001)
            for sample in 0...100 {
                let pose=AuthoredGolfMotion.returnToAddress(progress:Float(sample)/100,club:club,type:type)
                XCTAssertEqual(simd_distance(pose.clubGrip,pose.clubHead),simd_distance(finish.clubGrip,finish.clubHead),accuracy:0.002)
                XCTAssertLessThan(simd_distance(pose[.leftWrist],pose.clubGrip),0.20)
                XCTAssertLessThan(simd_distance(pose[.rightWrist],pose.clubGrip),0.20)
            }
        }
    }

    @MainActor func testLivingTurfKeepsExposedHazardBanksBare() {
        for hole in Course.sunwardResort.holes {
            for hazard in hole.hazards {
                XCTAssertFalse(CourseArt.turfClearsHazardEdges(.init(x:hazard.x,d:hazard.distance),hole:hole))
                for i in 0..<40 {
                    let angle=Double(i)*2 * .pi/40
                    let point=CoursePoint(x:hazard.x+cos(angle)*hazard.width/2,
                        d:hazard.distance+sin(angle)*hazard.length/2)
                    let radial=hazard.radialSurface(at:point).radius
                    let distance=(radial-1)*min(hazard.width,hazard.length)/2
                    if distance <= (hazard.kind == .water ? 1.25 : 0.20) {
                        XCTAssertFalse(CourseArt.turfClearsHazardEdges(point,hole:hole))
                    }
                }
            }
        }
    }

    @MainActor func testTurfBladeLightingMatchesSupportingSlopeAndTint() throws {
        for hole in Course.sunwardResort.holes {
            let root=CourseArt.livingTurf(hole)
            let nodes=root.childNodes+[CourseArt.bunkerEdgeTurf(hole)]
            var checked=0
            for node in nodes {
                let geometry=try XCTUnwrap(node.geometry)
                let positions=try XCTUnwrap(geometry.sources(for:.vertex).first)
                let normals=try XCTUnwrap(geometry.sources(for:.normal).first)
                let colors=try XCTUnwrap(geometry.sources(for:.color).first)
                func read(_ source:SCNGeometrySource,_ vertex:Int,_ component:Int)->Float {
                    source.data.withUnsafeBytes {$0.loadUnaligned(fromByteOffset:source.dataOffset+vertex*source.dataStride+component*4,as:Float.self)}
                }
                // First two vertices are symmetric around each clump's root.
                for i in stride(from:0,to:positions.vectorCount-2,by:9*37) {
                    let point=CoursePoint(x:Double((read(positions,i,0)+read(positions,i+1,0))/2),
                        d:-Double((read(positions,i,2)+read(positions,i+1,2))/2))
                    let surface=hole.surface(at:point)
                    let expected=simd_normalize(simd_float3(Float(-surface.slopeX),1,Float(surface.slopeD)))
                    let normal=simd_float3(read(normals,i,0),read(normals,i,1),read(normals,i,2))
                    XCTAssertLessThan(simd_distance(normal,expected),0.001)
                    let color=simd_float3(read(colors,i,0),read(colors,i,1),read(colors,i,2))
                    let palettes=[CourseLie.rough,.deepRough,.fringe].map {simd_normalize(CourseArt.turfTint($0))}
                    XCTAssertLessThan(palettes.map {simd_distance(simd_normalize(color),$0)}.min() ?? 1,0.0001)
                    checked += 1
                }
            }
            XCTAssertGreaterThan(checked,500)
        }
        XCTAssertNotNil(CourseArt.greenMaterial.diffuse.contents as? UIImage,"The putting surface needs fine grain, not a flat color fill")
    }

    @MainActor func testLivingTurfIsChunkedAndDoesNotReplacePlayableGround() {
        let hole=Course.sunwardResort.holes[0],turf=CourseArt.livingTurf(Course.sunwardResort.holes[0])
        XCTAssertGreaterThan(turf.childNodes.count,10)
        var triangles=0
        for node in turf.childNodes {
            XCTAssertFalse(node.castsShadow)
            triangles += node.geometry?.elements.reduce(0) {$0+$1.primitiveCount} ?? 0
            XCTAssertLessThan(node.geometry?.sources(for:.vertex).first?.vectorCount ?? 0,15000)
        }
        XCTAssertGreaterThan(triangles,10000)
        XCTAssertLessThan(triangles,750000)
        XCTAssertEqual(hole,Course.sunwardResort.holes[0])
        print("LIVING_TURF_TRIANGLES=\(triangles), CHUNKS=\(turf.childNodes.count)")
    }

    @MainActor func testGrassRootsFollowSlopesAndFullyRetiredBladesLeaveNoPixels() throws {
        for hole in Course.sunwardResort.holes {
            let nodes=CourseArt.livingTurf(hole).childNodes+[CourseArt.bunkerEdgeTurf(hole)]
            for node in nodes {
                let source=try XCTUnwrap(node.geometry?.sources(for:.vertex).first)
                let uv=try XCTUnwrap(node.geometry?.sources(for:.texcoord).first)
                func read(_ s:SCNGeometrySource,_ vertex:Int,_ component:Int)->Float {
                    s.data.withUnsafeBytes {$0.loadUnaligned(fromByteOffset:s.dataOffset+vertex*s.dataStride+component*4,as:Float.self)}
                }
                for start in stride(from:0,to:source.vectorCount-2,by:3*79) {for vertex in start...start+1 {
                    let p=CoursePoint(x:Double(read(source,vertex,0)),d:-Double(read(source,vertex,2)))
                    let inset:Double=node.name == "sunwardBunkerEdgeTurf" ? 0.006 : 0.004
                    XCTAssertEqual(Double(read(source,vertex,1)),hole.surface(at:p).heightYards-inset,accuracy:0.003)
                    // Root corners never sway or flatten as if they were tips.
                    XCTAssertEqual(read(uv,vertex,1),0)
                }}
            }
        }
        let course=CourseScene(),hole=Course.sunwardResort.holes[0];primeCourseReview(course,hole:hole)
        let turf=try XCTUnwrap(course.scene.rootNode.childNode(withName:"sunwardLivingTurf",recursively:true))
        let patch=try XCTUnwrap(turf.childNodes.first)
        turf.childNodes.forEach {$0.isHidden=true}
        let bounds=patch.boundingBox
        let center=(simd_float3(bounds.min)+simd_float3(bounds.max))/2
        course.camera.simdPosition=center+simd_float3(0,20,100)
        course.camera.look(at:SCNVector3(center),up:SCNVector3(0,1,0),localFront:SCNVector3(0,0,-1))
        let renderer=SCNRenderer(device:nil,options:nil);renderer.scene=course.scene;renderer.pointOfView=course.camera
        func pixels() throws -> [UInt8] {
            let image=renderer.snapshot(atTime:0,with:CGSize(width:640,height:360),antialiasingMode:.multisampling4X)
            return Array(try XCTUnwrap(image.cgImage?.dataProvider?.data) as Data)
        }
        _ = try pixels();let absent=try pixels()
        patch.isHidden=false;let retired=try pixels()
        XCTAssertEqual(absent.count,retired.count)
        let mean=zip(absent,retired).reduce(0.0) {$0+Double(abs(Int($1.0)-Int($1.1)))}/Double(absent.count)
        XCTAssertLessThan(mean,0.02,"A fully faded grass chunk must be visually absent, not leave bright ground specks")
    }

    @MainActor func testImportedShoreRocksStayOutsideScoredLiesAndPaths() throws {
        for variant in 1...3 {
            let asset=try XCTUnwrap(SunwardAsset.load("NatureShoreRock\(variant)"))
            XCTAssertTrue(asset.valid);XCTAssertTrue(asset.bones.isEmpty)
            XCTAssertTrue(asset.materials.allSatisfy {$0.texture == "Rocks_Diffuse.png"})
            let triangles=asset.meshes.reduce(0) {$0+$1.indices.count/3}
            XCTAssertGreaterThan(triangles,200);XCTAssertLessThan(triangles,600)
            for mesh in asset.meshes {for i in stride(from:0,to:mesh.positions.count,by:3) {
                XCTAssertLessThanOrEqual(hypot(mesh.positions[i],mesh.positions[i+2]),1.001)
                XCTAssertGreaterThanOrEqual(mesh.positions[i+1],0)
            }}
        }
        var total=0
        for hole in Course.sunwardResort.holes {
            let sites=CourseArt.shorelineRockSites(hole),route=CourseArt.resortRoute(hole)
            XCTAssertEqual(sites,CourseArt.shorelineRockSites(hole));total += sites.count
            let node=CourseArt.shorelineRocks(hole),geometry=try XCTUnwrap(node.geometry)
            XCTAssertNil(node.physicsBody);XCTAssertTrue(node.childNodes.isEmpty)
            XCTAssertEqual(geometry.materials.count,1);XCTAssertLessThan(geometry.elements[0].primitiveCount,30_000)
            XCTAssertNotNil(geometry.firstMaterial?.diffuse.contents as? UIImage)
            let source=try XCTUnwrap(geometry.sources(for:.vertex).first)
            for vertex in 0..<source.vectorCount {
                let xyz=source.data.withUnsafeBytes { bytes in
                    (0..<3).map {bytes.loadUnaligned(fromByteOffset:source.dataOffset+vertex*source.dataStride+$0*4,as:Float.self)}
                }
                let p=CoursePoint(x:Double(xyz[0]),d:-Double(xyz[2]))
                XCTAssertEqual(hole.lie(at:p),.outOfBounds)
                XCTAssertFalse(hole.hazards.contains {$0.contains(p)})
                XCTAssertTrue(CourseArt.clearsResortRoute(p,radius:0,route:route))
            }
        }
        XCTAssertGreaterThan(total,50)
        print("SHORE_ROCK_TOTAL=\(total)")
    }

    func testAuthoredSwingKeepsHandsAndLimbsConnected() throws {
        let length = simd_distance(AvatarAnimations.address.clubGrip,AvatarSize.ball)
        for (club,type) in [(GolfClub.driver,ShotType.full),(.iron,.full),(.wedge,.pitch),(.wedge,.chip),(.wedge,.bunker),(.putter,.putt)] {
            var previous: BodyPose3D?
            for angle in stride(from:-150.0,through:150,by:1.0) {
                let pose = AuthoredGolfMotion.swing(degrees:angle,club:club,type:type)
                for (s,e,w) in [(BodyJoint.leftShoulder,BodyJoint.leftElbow,BodyJoint.leftWrist),(.rightShoulder,.rightElbow,.rightWrist)] {
                    XCTAssertEqual(simd_distance(pose[s],pose[e]),AvatarSize.upperArm,accuracy:0.025)
                    XCTAssertEqual(simd_distance(pose[e],pose[w]),AvatarSize.forearm,accuracy:0.025)
                    XCTAssertLessThan(simd_distance(pose[w],pose.clubGrip),0.20,"\(club) \(type) \(angle)")
                }
                XCTAssertEqual(simd_distance(pose.clubGrip,pose.clubHead),length,accuracy:0.002)
                if let previous {
                    for joint in BodyJoint.allCases { XCTAssertLessThan(simd_distance(previous[joint],pose[joint]),0.16) }
                }
                previous = pose
            }
        }
    }

    func testLakePlanesAndBankGradientsAgreeWithPhysics() throws {
        for hole in Course.sunwardResort.holes {
            for lake in hole.hazards where lake.kind == .water {
                let level = try XCTUnwrap(hole.waterElevations[lake.id])
                for a in stride(from:0.0,to:2 * .pi,by:0.25) {
                    for r in [0.0,0.5,0.95,1.05,1.15] {
                        let shape=lake.boundaryScale(at:a)
                        let p=CoursePoint(x:lake.x+cos(a)*lake.width/2*r*shape,d:lake.distance+sin(a)*lake.length/2*r*shape)
                        let s=hole.surface(at:p)
                        if r < 1 { XCTAssertEqual(s.heightYards,level,accuracy:0.00001) }
                        let e=0.0001
                        let dx=(hole.surface(at:.init(x:p.x+e,d:p.d)).heightYards-hole.surface(at:.init(x:p.x-e,d:p.d)).heightYards)/(2*e)
                        let dd=(hole.surface(at:.init(x:p.x,d:p.d+e)).heightYards-hole.surface(at:.init(x:p.x,d:p.d-e)).heightYards)/(2*e)
                        XCTAssertEqual(s.slopeX,dx,accuracy:0.001)
                        XCTAssertEqual(s.slopeD,dd,accuracy:0.001)
                    }
                }
            }
        }
    }

    @MainActor func testFullSwingReviewContactSheet() throws {
        let course=CourseScene();course.stop()
        for node in course.scene.rootNode.childNodes where node !== course.camera && node.light == nil { node.removeFromParentNode() }
        course.scene.background.contents=UIColor(red:0.76,green:0.84,blue:0.88,alpha:1)
        let floor=SCNNode(geometry:SCNPlane(width:40,height:40));floor.eulerAngles.x = -.pi/2
        floor.geometry?.firstMaterial?.diffuse.contents=UIColor(white:0.7,alpha:1)
        course.scene.rootNode.addChildNode(floor)
        let look=GolferAppearance.preset(.cove)
        let rig=AvatarRig(shirt:look.shirtColor,authored:false,appearance:look)
        rig.setClub(.driver);rig.node.simdScale=simd_float3(repeating:AvatarSize.courseScale)
        course.scene.rootNode.addChildNode(rig.node)
        let renderer=SCNRenderer(device:nil,options:nil);renderer.scene=course.scene;renderer.pointOfView=course.camera
        course.camera.camera?.fieldOfView=43
        let folder=FileManager.default.urls(for:.documentDirectory,in:.userDomainMask)[0].appendingPathComponent("SwingReview")
        try FileManager.default.createDirectory(at:folder,withIntermediateDirectories:true)
        let angles:[Double]=[0,100,150,45,0,-45,-90,-120,-150]
        for mirrored in [false,true] {
            rig.setMirrored(mirrored)
            rig.node.simdScale *= AvatarSize.courseScale
            var frames:[UIImage]=[]
            let sequences=[angles.map { AuthoredGolfMotion.swing(degrees:$0,club:.driver,type:.full) },
                (0...8).map { AuthoredGolfMotion.returnToAddress(progress:Float($0)/8,club:.driver,type:.full) }]
            for sequence in sequences {
                for eye in [SCNVector3(4,2.2,3),SCNVector3(0.4,2.1,4.7)] {
                    course.camera.position=eye;course.camera.look(at:SCNVector3(0.25,1.05,0),up:SCNVector3(0,1,0),localFront:SCNVector3(0,0,-1))
                    for pose in sequence {
                        rig.apply(pose)
                        frames.append(renderer.snapshot(atTime:0,with:CGSize(width:320,height:400),antialiasingMode:.multisampling4X))
                    }
                }
            }
            let format=UIGraphicsImageRendererFormat();format.scale=1
            let sheet=UIGraphicsImageRenderer(size:CGSize(width:2880,height:1600),format:format).image { _ in
                for (i,frame) in frames.enumerated() { frame.draw(in:CGRect(x:(i%9)*320,y:(i/9)*400,width:320,height:400)) }
            }
            try XCTUnwrap(sheet.pngData()).write(to:folder.appendingPathComponent(mirrored ? "left.png" : "right.png"))
        }
        print("SWING_REVIEW="+folder.path)
    }
    @MainActor func testPlayableCourseUsesDetailedRuntimeMaterials() {
        for material in [CourseArt.fairwayMaterial,CourseArt.roughMaterial,CourseArt.sandMaterial,CourseArt.waterMaterial] {
            XCTAssertNotNil(material.normal.contents as? UIImage)
            XCTAssertEqual(material.lightingModel,.physicallyBased)
        }
        let tree=CourseArt.branchingTree(seed:0)
        var triangles=0
        tree.enumerateChildNodes { node,_ in
            triangles += node.geometry?.elements.reduce(0) {$0+$1.primitiveCount} ?? 0
        }
        triangles += tree.geometry?.elements.reduce(0) {$0+$1.primitiveCount} ?? 0
        XCTAssertGreaterThan(triangles,1900)
        XCTAssertLessThan(triangles,6000,"Prototype detail must remain bounded for instanced groves")
        let scene=CourseScene();scene.load(Course.sunwardResort.holes[0])
        XCTAssertNotNil(scene.scene.rootNode.childNode(withName:"sharedPlayableSurface",recursively:true))
        XCTAssertNotNil(scene.scene.rootNode.childNode(withName:"canopyTree",recursively:true))
        scene.stop()
    }

    @MainActor private func primeCourseReview(_ course:CourseScene,hole:Hole) {
        course.inputs=SceneInputs(hole:hole,ball:hole.tee,heading:hole.tee.heading(to:hole.pin),
            distanceToPin:hole.tee.distance(to:hole.pin),lie:hole.lie(at:hole.tee),club:.driver,
            aim:0,handedness:.right,shot:nil,isReplay:false,flightStart:nil,pausedAt:nil,
            swingAngle:0,bystanders:[],reduceMotion:true)
        // Exercise the production first-tick path before moving the review camera.
        // A bare load left the pin beacon and guides at their initial origin.
        course.start();course.stop()
    }

    @MainActor func testCourseQualityReviewCaptures() throws {
        let course=CourseScene();course.stop()
        let hole=Course.sunwardResort.holes[0];primeCourseReview(course,hole:hole)
        let renderer=SCNRenderer(device:nil,options:nil);renderer.scene=course.scene;renderer.pointOfView=course.camera
        let folder=FileManager.default.urls(for:.documentDirectory,in:.userDomainMask)[0].appendingPathComponent("CourseQualityReview")
        try FileManager.default.createDirectory(at:folder,withIntermediateDirectories:true)
        let lake=try XCTUnwrap(hole.hazards.first {$0.kind == .water})
        let bunker=try XCTUnwrap(hole.hazards.last {$0.kind == .bunker})
        let lodge=CourseArt.clubhouseSite(hole),lodgeY=Double(CourseArt.dressingHeight(CourseArt.clubhouseSite(hole),hole:hole))
        let frames:[(String,SCNVector3,SCNVector3)]=[
            ("rough-detail",SCNVector3(hole.pin.x+23,hole.surface(at:.init(x:hole.pin.x+23,d:hole.pin.d-29)).heightYards+1.4,-hole.pin.d+29),SCNVector3(hole.pin.x,hole.surface(at:hole.pin).heightYards+1,-hole.pin.d-8)),
            ("clubhouse",SCNVector3(lodge.x-27,lodgeY+10,-lodge.d+32),SCNVector3(lodge.x,lodgeY+6,-lodge.d+2)),
            ("bunker",SCNVector3(bunker.x-13,hole.terrain.elevation(at:.init(x:bunker.x,d:bunker.distance))+7,-bunker.distance+15),SCNVector3(bunker.x,hole.bunkerFloors[bunker.id] ?? 0,-bunker.distance)),
            ("course-facing",SCNVector3(hole.pin.x-20,hole.surface(at:hole.pin).heightYards+9,-hole.pin.d-30),SCNVector3(hole.pin.x-8,3,-hole.pin.d+38)),
            ("film-angle",SCNVector3(hole.pin.x+23,hole.surface(at:hole.pin).heightYards+7,-hole.pin.d+29),SCNVector3(hole.pin.x,3,-hole.pin.d-17)),
            ("green",SCNVector3(hole.pin.x+77,55,-hole.pin.d+95),SCNVector3(hole.pin.x,hole.surface(at:hole.pin).heightYards,-hole.pin.d+10)),
            ("tee",SCNVector3(hole.tee.x+5,hole.surface(at:hole.tee).heightYards+3.5,-hole.tee.d+7),SCNVector3(hole.tee.x,3,-hole.tee.d-60)),
            ("lagoon",SCNVector3(lake.x+lake.width/2+12,12,-lake.distance+24),SCNVector3(lake.x,0,-lake.distance-15))]
        for (name,eye,target) in frames {
            course.camera.position=eye
            course.camera.look(at:target,up:SCNVector3(0,1,0),localFront:SCNVector3(0,0,-1))
            course.camera.camera?.fieldOfView=54
            course.updatePinBeacon(pin:hole.pin,sunk:false)
            let image=renderer.snapshot(atTime:0,with:CGSize(width:1280,height:720),antialiasingMode:.multisampling4X)
            try XCTUnwrap(image.pngData()).write(to:folder.appendingPathComponent(name+".png"))
            let attachment=XCTAttachment(image:image);attachment.name="Playable Sunward \(name)";attachment.lifetime = .keepAlways;add(attachment)
        }
        print("COURSE_QUALITY_REVIEW="+folder.path)
    }

    @MainActor private func bakeLagoonFaces(_ course:CourseScene,hole:Hole) throws -> [UIImage] {
        let lake=try XCTUnwrap(hole.hazards.first {$0.kind == .water})
        let level=try XCTUnwrap(hole.waterElevations[lake.id])
        // Separate static capture scene: never mutate the live background,
        // avatars, guides or materials while building an environment atlas.
        let scene=SCNScene()
        scene.background.contents=UIColor.clear
        scene.lightingEnvironment.contents=CourseArt.daylightEnvironment
        scene.lightingEnvironment.intensity=course.scene.lightingEnvironment.intensity
        scene.fogColor=course.scene.fogColor;scene.fogStartDistance=course.scene.fogStartDistance;scene.fogEndDistance=course.scene.fogEndDistance
        for source in course.scene.rootNode.childNodes where source.light != nil ||
            source.childNode(withName:"sharedPlayableSurface",recursively:true) != nil {
            let node=source.clone()
            if let surface=node.childNode(withName:"sharedPlayableSurface",recursively:true),let geometry=surface.geometry {
                surface.geometry=geometry.copy() as? SCNGeometry
                surface.geometry?.materials[4]=CourseArt.waterMaterial // No reflection feedback.
            }
            scene.rootNode.addChildNode(node)
        }
        let camera=SCNNode();camera.camera=course.camera.camera?.copy() as? SCNCamera
        scene.rootNode.addChildNode(camera)
        let eye=simd_float3(Float(lake.x),Float(level+0.05),Float(-lake.distance))
        camera.simdPosition=eye;camera.camera?.fieldOfView=90;camera.camera?.zNear=0.1
        let renderer=SCNRenderer(device:nil,options:nil);renderer.scene=scene;renderer.pointOfView=camera
        let directions:[simd_float3]=[[1,0,0],[-1,0,0],[0,1,0],[0,-1,0],[0,0,1],[0,0,-1]]
        return directions.enumerated().map { index,direction in
            let up=SCNVector3(index == 2 ? simd_float3(0,0,-1) : index == 3 ? simd_float3(0,0,1) : simd_float3(0,1,0))
            camera.look(at:SCNVector3(eye+direction),up:up,localFront:SCNVector3(0,0,-1))
            let snapshot=renderer.snapshot(atTime:0,with:CGSize(width:256,height:256),antialiasingMode:.multisampling4X)
            let format=UIGraphicsImageRendererFormat();format.scale=1;format.opaque=false
            return UIGraphicsImageRenderer(size:CGSize(width:256,height:256),format:format).image { context in
                context.cgContext.translateBy(x:256,y:0);context.cgContext.scaleBy(x:-1,y:1)
                snapshot.draw(in:CGRect(x:0,y:0,width:256,height:256))
            }
        }
    }

    @MainActor func testOptInBakeLagoonReflections() throws {
        guard ProcessInfo.processInfo.environment["GOLF_BAKE_REFLECTIONS"] == "1" else {throw XCTSkip("Offline environment asset export")}
        let folder=FileManager.default.urls(for:.documentDirectory,in:.userDomainMask)[0].appendingPathComponent("LagoonReflectionAssets")
        try FileManager.default.createDirectory(at:folder,withIntermediateDirectories:true)
        for hole in Course.sunwardResort.holes where hole.hazards.contains(where:{$0.kind == .water}) {
            let course=CourseScene();primeCourseReview(course,hole:hole)
            for (index,face) in try bakeLagoonFaces(course,hole:hole).enumerated() {
                try XCTUnwrap(face.pngData()).write(to:folder.appendingPathComponent("hole-\(hole.number)-face-\(index).png"))
            }
        }
        print("LAGOON_REFLECTION_ASSETS="+folder.path)
    }

    @MainActor func testBundledLagoonReflectionsAreLocalAndCoverEveryResortLake() throws {
        let course=CourseScene()
        for hole in Course.sunwardResort.holes {
            primeCourseReview(course,hole:hole)
            let geometry=try XCTUnwrap(course.scene.rootNode.childNode(withName:"sharedPlayableSurface",recursively:true)?.geometry)
            XCTAssertTrue(geometry.materials[4] === course.lagoonMaterial)
            XCTAssertTrue(course.scene.lightingEnvironment.contents as? UIImage === CourseArt.daylightEnvironment)
            guard let lake=hole.hazards.first(where:{$0.kind == .water}) else {
                XCTAssertTrue(course.lagoonMaterial === CourseArt.waterMaterial);continue
            }
            XCTAssertFalse(course.lagoonMaterial === CourseArt.waterMaterial,"No silent fallback on hole \(hole.number)")
            let map=try XCTUnwrap(course.lagoonMaterial.value(forKey:"lagoonReflection") as? SCNMaterialProperty)
            let atlas=try XCTUnwrap(map.contents as? UIImage)
            XCTAssertEqual(atlas.size,CGSize(width:1536,height:256))
            XCTAssertNotNil(course.lagoonMaterial.shaderModifiers?[.fragment])
            let center=try XCTUnwrap(course.lagoonMaterial.value(forKey:"lagoonCenter") as? NSValue).scnVector3Value
            XCTAssertEqual(center.x,Float(lake.x),accuracy:0.001)
            XCTAssertEqual(center.y,Float(hole.waterElevations[lake.id]!+0.05),accuracy:0.001)
            XCTAssertEqual(center.z,Float(-lake.distance),accuracy:0.001)
            for index in 0..<6 {
                let url=try XCTUnwrap(Bundle.main.url(forResource:"hole-\(hole.number)-face-\(index)",withExtension:"png",subdirectory:"Reflections"))
                let image=try XCTUnwrap(UIImage(contentsOfFile:url.path))
                XCTAssertEqual(image.size,CGSize(width:256,height:256))
                if index == 2 {
                    let cg=try XCTUnwrap(image.cgImage),bytes=Array(cg.dataProvider!.data! as Data)
                    // The zenith is transparent: runtime computes a continuous
                    // sky instead of exposing color seams between capture views.
                    XCTAssertEqual(bytes[128*cg.bytesPerRow+128*cg.bitsPerPixel/8+3],0)
                }
            }
        }
        for hole in Course.easy.holes+Course.sunward.holes {
            XCTAssertTrue(CourseArt.lagoonMaterial(hole) === CourseArt.waterMaterial)
        }
        var modified=Course.sunwardResort.holes[0];modified.simulationVersion += 1
        XCTAssertTrue(CourseArt.lagoonMaterial(modified) === CourseArt.waterMaterial,"Never bind an old lake map to changed geometry")
    }

    @MainActor func testOptInLagoonReflectionStudy() throws {
        guard ProcessInfo.processInfo.environment["GOLF_REFLECTION_STUDY"] == "1" else {throw XCTSkip("Opt-in actual-scene reflection study")}
        let folder=FileManager.default.urls(for:.documentDirectory,in:.userDomainMask)[0].appendingPathComponent("LagoonReflectionStudy")
        try FileManager.default.createDirectory(at:folder,withIntermediateDirectories:true)
        let course=CourseScene();let hole=Course.sunwardResort.holes[0];primeCourseReview(course,hole:hole)
        let lake=try XCTUnwrap(hole.hazards.first {$0.kind == .water}),level=try XCTUnwrap(hole.waterElevations[lake.id])
        let geometry=try XCTUnwrap(course.scene.rootNode.childNode(withName:"sharedPlayableSurface",recursively:true)?.geometry)
        let faces=try bakeLagoonFaces(course,hole:hole)
        let reflected=CourseArt.lagoonMaterial(faces:faces,center:SCNVector3(lake.x,level+0.05,-lake.distance),
            extent:SCNVector3(lake.width/2+10,30,lake.length/2+10))
        let renderer=SCNRenderer(device:nil,options:nil);renderer.scene=course.scene;renderer.pointOfView=course.camera
        course.camera.camera?.fieldOfView=54
        for (view,position) in [
            ("bank",SCNVector3(lake.x+lake.width/2+12,12,-lake.distance+24)),
            ("opposite",SCNVector3(lake.x-lake.width/2-12,9,-lake.distance-24)),
            ("low",SCNVector3(lake.x+lake.width/2+8,4,-lake.distance+24)),
            ("overhead",SCNVector3(lake.x+5,60,-lake.distance+10))] {
            course.camera.position=position
            course.camera.look(at:SCNVector3(lake.x,level,-lake.distance),up:SCNVector3(0,1,0),localFront:SCNVector3(0,0,-1))
            for (name,material) in [("baseline",CourseArt.waterMaterial),("reflected",reflected)] {
                geometry.materials[4]=material
                let image=renderer.snapshot(atTime:0,with:CGSize(width:1280,height:720),antialiasingMode:.multisampling4X)
                try XCTUnwrap(image.pngData()).write(to:folder.appendingPathComponent(view+"-"+name+".png"))
            }
        }
        print("LAGOON_REFLECTION_STUDY="+folder.path)
    }

    @MainActor func testLagoonReflectionChangesWaterWithoutChangingSkyOrTerrain() throws {
        let course=CourseScene(),hole=Course.sunwardResort.holes[0]
        primeCourseReview(course,hole:hole)
        let lake=try XCTUnwrap(hole.hazards.first {$0.kind == .water})
        let geometry=try XCTUnwrap(course.scene.rootNode.childNode(withName:"sharedPlayableSurface",recursively:true)?.geometry)
        let material=geometry.materials[4]
        defer {geometry.materials[4]=material}
        course.camera.position=SCNVector3(lake.x+lake.width/2+12,12,-lake.distance+24)
        course.camera.look(at:SCNVector3(lake.x,0,-lake.distance-15),up:SCNVector3(0,1,0),localFront:SCNVector3(0,0,-1))
        course.camera.camera?.fieldOfView=54
        let renderer=SCNRenderer(device:nil,options:nil);renderer.scene=course.scene;renderer.pointOfView=course.camera
        func capture(_ material:SCNMaterial) throws -> CGImage {
            geometry.materials[4]=material
            return try XCTUnwrap(renderer.snapshot(atTime:0,with:CGSize(width:640,height:360),antialiasingMode:.multisampling4X).cgImage)
        }
        _ = try capture(CourseArt.waterMaterial) // Prime render resources before comparison.
        let baseline=try capture(CourseArt.waterMaterial),reflected=try capture(material)
        let a=Array(baseline.dataProvider!.data! as Data),b=Array(reflected.dataProvider!.data! as Data)
        XCTAssertEqual(baseline.bytesPerRow,reflected.bytesPerRow)
        func meanDifference(x:Range<Int>,y:Range<Int>) -> Double {
            var delta=0.0,count=0
            for row in y {for column in x {for channel in 0..<3 {
                let offset=row*baseline.bytesPerRow+column*baseline.bitsPerPixel/8+channel
                delta += Double(abs(Int(a[offset])-Int(b[offset])));count += 1
            }}}
            return delta/Double(count)
        }
        XCTAssertLessThan(meanDifference(x:0..<640,y:0..<120),0.05,"Water-only reflection must not change sky or distant terrain")
        XCTAssertGreaterThan(meanDifference(x:120..<380,y:220..<310),2,"The production map must visibly affect water pixels")
    }

    @MainActor func testAllResortLakeReflectionReviewCaptures() throws {
        let folder=FileManager.default.urls(for:.documentDirectory,in:.userDomainMask)[0].appendingPathComponent("LakeReflectionReview")
        try FileManager.default.createDirectory(at:folder,withIntermediateDirectories:true)
        var frames:[(Int,UIImage)]=[]
        for hole in Course.sunwardResort.holes {
            guard let lake=hole.hazards.first(where:{$0.kind == .water}),let level=hole.waterElevations[lake.id] else {continue}
            let course=CourseScene();primeCourseReview(course,hole:hole)
            XCTAssertEqual(course.lagoonMaterial.name,"Sunward local lagoon reflection")
            let eye=CoursePoint(x:lake.x+lake.width/2+12,d:lake.distance-24)
            course.camera.position=SCNVector3(eye.x,max(level+12,Double(CourseArt.dressingHeight(eye,hole:hole))+3),-eye.d)
            course.camera.look(at:SCNVector3(lake.x,level,-lake.distance),up:SCNVector3(0,1,0),localFront:SCNVector3(0,0,-1))
            course.camera.camera?.fieldOfView=54
            let renderer=SCNRenderer(device:nil,options:nil);renderer.scene=course.scene;renderer.pointOfView=course.camera
            let image=renderer.snapshot(atTime:0,with:CGSize(width:1280,height:720),antialiasingMode:.multisampling4X)
            try XCTUnwrap(image.pngData()).write(to:folder.appendingPathComponent("hole-\(hole.number).png"))
            frames.append((hole.number,image))
        }
        XCTAssertEqual(frames.count,6)
        let format=UIGraphicsImageRendererFormat();format.scale=1
        let sheet=UIGraphicsImageRenderer(size:CGSize(width:1920,height:720),format:format).image { _ in
            for (index,frame) in frames.enumerated() {
                let x=index%3*640,y=index/3*360
                frame.1.draw(in:CGRect(x:x,y:y,width:640,height:360))
                ("HOLE \(frame.0)" as NSString).draw(at:CGPoint(x:x+16,y:y+12),withAttributes:[.font:UIFont.boldSystemFont(ofSize:20),.foregroundColor:UIColor.white])
            }
        }
        try XCTUnwrap(sheet.pngData()).write(to:folder.appendingPathComponent("all-lakes.png"))
        print("LAKE_REFLECTION_REVIEW="+folder.path)
    }
    @MainActor func testAllNineSunwardHolesFinishForFourPlayersWithoutLosingScores() throws {
        let suite="SunwardRound-"+UUID().uuidString
        let defaults=try XCTUnwrap(UserDefaults(suiteName:suite))
        defer { defaults.removePersistentDomain(forName:suite) }
        let round=CourseRound(course:.sunwardResort,playerCount:4,defaults:defaults)
        let date=Date(timeIntervalSince1970:100)
        for hole in 0..<9 {
            for player in 0..<4 {
                XCTAssertEqual(round.holeIndex,hole);XCTAssertEqual(round.playerIndex,player)
                for _ in 0..<(round.hole.par+CourseRound.strokesOverParCap) {
                    if round.phase == .holed { break }
                    round.club = .putter;round.charge(0.1)
                    XCTAssertTrue(round.release(at:date))
                    round.skipFlight(at:date.addingTimeInterval(1))
                    round.nextShot()
                }
                XCTAssertEqual(round.phase,.holed)
                XCTAssertNotNil(round.scores[player][hole])
                round.continueAfterHole()
            }
        }
        XCTAssertEqual(round.phase,.complete)
        XCTAssertEqual(round.scores.count,4)
        XCTAssertTrue(round.scores.allSatisfy {$0.count == 9 && $0.allSatisfy {$0 != nil}})
        XCTAssertNil(round.best,"Multiplayer cannot overwrite a solo best score")
    }
    func testTVPixelBudgetPreservesMainDisplayCadence() {
        let phone = CourseRenderPolicy.phone(externalDisplayActive: true)
        XCTAssertEqual(phone.framesPerSecond, 30)
        XCTAssertEqual(CourseRenderPolicy.television.framesPerSecond, 60)
        XCTAssertEqual(CourseRenderPolicy.phone(externalDisplayActive: false).framesPerSecond, 60)
        let phoneSize = CGSize(width: 440, height: 956)
        XCTAssertEqual(phone.scale(for: phoneSize, nativeScale: 3) * 956, 960, accuracy: 0.01)
        XCTAssertEqual(CourseRenderPolicy.television.scale(for: CGSize(width:3840,height:2160), nativeScale:1), 0.5)
        XCTAssertEqual(CourseRenderPolicy.television.scale(for: CGSize(width:1280,height:720), nativeScale:1), 1)
    }

    @MainActor func testShadingDiagnosticCaptures() throws {
        let folder = FileManager.default.urls(for:.documentDirectory,in:.userDomainMask)[0].appendingPathComponent("SunwardShading")
        try FileManager.default.createDirectory(at:folder,withIntermediateDirectories:true)
        let course = CourseScene(); course.stop(); course.load(Course.sunwardResort.holes[0])
        let hole = Course.sunwardResort.holes[0]
        course.camera.position = SCNVector3(hole.pin.x+26,8,-hole.pin.d+30)
        course.camera.look(at:SCNVector3(hole.pin.x,7,-hole.pin.d-28))
        let renderer = SCNRenderer(device:nil,options:nil)
        renderer.scene = course.scene; renderer.pointOfView = course.camera
        let sun = try XCTUnwrap(course.scene.rootNode.childNode(withName:"sunwardWarmKey", recursively:true)?.light)
        for (name, shadows, ao, backfaces) in [("baseline",true,true,false),("no-ao",true,false,false),("no-shadows",false,true,false),("backfaces",true,false,true)] {
            sun.castsShadow = shadows; sun.forcesBackFaceCasters = backfaces
            course.camera.camera?.screenSpaceAmbientOcclusionIntensity = ao ? 0.45 : 0
            let image = renderer.snapshot(atTime:0,with:CGSize(width:1280,height:720),antialiasingMode:.multisampling2X)
            try XCTUnwrap(image.pngData()).write(to:folder.appendingPathComponent(name+".png"))
        }
        print("SUNWARD_SHADING="+folder.path)
    }
    @MainActor func testBundledCourseCardsDecodeForNativeMenus() {
        for hole in 1...9 {
            let image=SunwardCourseArtwork.image(hole:hole)
            XCTAssertGreaterThan(image.size.width,100)
            XCTAssertGreaterThan(image.size.height,100)
        }
    }
    @MainActor func testIntegrationPortraitsUseProductionLighting() throws {
        let folder=FileManager.default.urls(for:.documentDirectory,in:.userDomainMask)[0].appendingPathComponent("SunwardIntegration")
        try FileManager.default.createDirectory(at:folder,withIntermediateDirectories:true)
        let course=CourseScene();course.stop()
        // Retain production lighting/camera only; omit the gameplay golfer, beacon and guides.
        for node in course.scene.rootNode.childNodes where node !== course.camera && node.light == nil {
            node.removeFromParentNode()
        }
        let floor=SCNNode(geometry:SCNPlane(width:40,height:40));floor.eulerAngles.x = -.pi/2
        floor.geometry?.firstMaterial?.lightingModel = .physicallyBased
        floor.geometry?.firstMaterial?.diffuse.contents=UIColor(red:0.64,green:0.68,blue:0.64,alpha:1)
        floor.geometry?.firstMaterial?.roughness.contents=0.9
        course.scene.rootNode.addChildNode(floor)
        course.camera.position=SCNVector3(3.6,1.9,2.2)
        course.camera.look(at:SCNVector3(0.20,1.00,0));course.camera.camera?.fieldOfView=40
        let renderer=SCNRenderer(device:nil,options:nil);renderer.scene=course.scene;renderer.pointOfView=course.camera
        for preset in GolferAppearance.Preset.allCases {
            let look=GolferAppearance.preset(preset)
            let rig=AvatarRig(shirt:look.shirtColor,authored:false,appearance:look)
            rig.setClub(.driver)
            rig.node.simdScale=simd_float3(repeating:AvatarSize.courseScale)
            course.scene.rootNode.addChildNode(rig.node)
            for (name,angle) in [("address",0.0),("backswing",110),("finish",-120)] {
                rig.apply(AuthoredGolfMotion.swing(degrees:angle,club:.driver,type:.full))
                let image=renderer.snapshot(atTime:0,with:CGSize(width:768,height:896),antialiasingMode:.multisampling4X)
                try XCTUnwrap(image.pngData()).write(to:folder.appendingPathComponent("\(preset.rawValue)-\(name).png"))
                let attachment=XCTAttachment(image:image);attachment.name="Sunward \(preset.rawValue) \(name)"
                attachment.lifetime = .keepAlways;add(attachment)
            }
            rig.node.removeFromParentNode()
        }
        XCTAssertNotNil(course.scene.lightingEnvironment.contents)
        XCTAssertEqual(course.camera.camera?.screenSpaceAmbientOcclusionIntensity,0,
            "Distant surfaces must not use the speckling SSAO pass")
        XCTAssertEqual(course.scene.rootNode.childNode(withName:"sunwardWarmKey",recursively:true)?.light?.castsShadow,true)
        print("SUNWARD_INTEGRATION="+folder.path)
    }
    @MainActor func testClothingFrontSurvivesReversedCameraShoulders() {
        let pose=AvatarAnimations.address
        for sign: Float in [-1,1] {
            let frame=AvatarRig.clothingOrientation(up:pose[.neck]-pose[.root],
                right:(pose[.rightShoulder]-pose[.leftShoulder])*sign)
            XCTAssertGreaterThan(frame.act(simd_float3(1,0,0)).x,0.5)
        }
    }
    @MainActor func testAllLooksAndBothHandsUseAuthoredSkin() throws {
        for look in GolferAppearance.Preset.allCases {
            for mirrored in [false,true] {
                let appearance=GolferAppearance.preset(look)
                let rig=AvatarRig(shirt:appearance.shirtColor,authored:false,appearance:appearance)
                XCTAssertNotNil(rig.node.childNode(withName:"sunwardAuthoredGolfer",recursively:true))
                rig.setMirrored(mirrored)
                for angle in [-150.0,-90,0,90,150] {
                    rig.apply(AuthoredGolfMotion.swing(degrees:angle,club:.driver,type:.full))
                    XCTAssertTrue(rig.node.simdTransform.columns.0.x.isFinite)
                }
            }
        }
    }
    func testAuthoredLibraryPreservesImpactAndShortGame() throws {
        let library=try XCTUnwrap(AuthoredGolfMotion.library)
        XCTAssertEqual(library.clips.count,13)
        for clip in library.clips { XCTAssertTrue(clip.valid) }
        for club in GolfClub.allCases {
            for type in [ShotType.full,.chip,.pitch,.bunker,.putt] {
                let impact=AuthoredGolfMotion.followThrough(elapsed:0,club:club,type:type)
                XCTAssertLessThan(simd_distance(impact.clubHead,AvatarSize.ball),0.001)
                let pose=AuthoredGolfMotion.swing(degrees:77.4,club:club,type:type)
                XCTAssertTrue(pose.clubHead.x.isFinite)
            }
        }
        let putt=AuthoredGolfMotion.swing(degrees:150,club:.putter,type:.putt)
        XCTAssertEqual(putt[.leftAnkle],AvatarAnimations.address[.leftAnkle])
    }
    @MainActor func testDecorationStaysOutsidePlay() {
        for hole in Course.sunwardResort.holes {
            for p in CourseArt.decorationPoints(hole) { XCTAssertEqual(hole.lie(at:p),.outOfBounds) }
        }
    }
    /// Explicit export invocation produces cards from the actual iOS game scene, not AI terrain.
    @MainActor func testExportNineCourseCards() throws {
        let folder=FileManager.default.urls(for:.documentDirectory,in:.userDomainMask)[0].appendingPathComponent("SunwardCards")
        try FileManager.default.createDirectory(at:folder,withIntermediateDirectories:true)
        let course=CourseScene(); course.stop()
        let renderer=SCNRenderer(device:nil,options:nil);renderer.scene=course.scene;renderer.pointOfView=course.camera
        for hole in Course.sunwardResort.holes {
            primeCourseReview(course,hole:hole)
            // Render upgrades must not silently resize the authoritative trunks.
            for tree in hole.trees {
                let trunkNode=try XCTUnwrap(course.scene.rootNode.childNode(withName:"collidableTrunk-\(tree.id)",recursively:true))
                let trunk=try XCTUnwrap(trunkNode.geometry as? SCNCylinder)
                XCTAssertEqual(Double(trunk.radius),tree.trunkRadius,accuracy:0.00001)
                XCTAssertEqual(Double(trunk.height),tree.trunkHeight,accuracy:0.00001)
                let crown=try XCTUnwrap(course.scene.rootNode.childNode(withName:"decorativeCrown-\(tree.id)",recursively:true))
                XCTAssertTrue(CourseArt.obstacleCrownGeometry.contains(where:{$0 === crown.geometry}))
                XCTAssertEqual(crown.geometry?.firstMaterial?.name,"sculpted-canopy")
                XCTAssertTrue(trunk.firstMaterial === CourseArt.obstacleBark)
                XCTAssertNil(crown.physicsBody,"Decorative foliage must not change trunk collisions")
                let vertices=try XCTUnwrap(crown.geometry?.sources(for:.vertex).first)
                vertices.data.withUnsafeBytes { bytes in
                    let transform=crown.simdTransform*simd_inverse(crown.simdPivot)
                    for index in 0..<vertices.vectorCount {
                        let offset=vertices.dataOffset+index*vertices.dataStride
                        let local=simd_float4(bytes.loadUnaligned(fromByteOffset:offset,as:Float.self),bytes.loadUnaligned(fromByteOffset:offset+4,as:Float.self),bytes.loadUnaligned(fromByteOffset:offset+8,as:Float.self),1)
                        let p=transform*local
                        XCTAssertLessThanOrEqual(hypot(Double(p.x)-tree.center.x,Double(p.z)+tree.center.d),tree.crownRadius+0.001)
                        let base=hole.surface(at:tree.center).heightYards
                        XCTAssertGreaterThanOrEqual(Double(p.y),base+tree.trunkHeight*0.68-0.001)
                        XCTAssertLessThanOrEqual(Double(p.y),base+tree.trunkHeight*0.68+tree.crownRadius*1.65+0.001)
                    }
                }
            }
            let surface=try XCTUnwrap(course.scene.rootNode.childNode(withName:"sharedPlayableSurface",recursively:true)?.geometry)
            let tangent=try XCTUnwrap(surface.sources(for:.tangent).first)
            XCTAssertEqual(tangent.vectorCount,surface.sources(for:.vertex).first?.vectorCount)
            tangent.data.withUnsafeBytes { bytes in
                let values=bytes.bindMemory(to:Float.self)
                for index in 0..<tangent.vectorCount {
                    let t=simd_float3(values[index*4],values[index*4+1],values[index*4+2])
                    XCTAssertTrue(t.x.isFinite && t.y.isFinite && t.z.isFinite)
                    XCTAssertEqual(simd_length(t),1,accuracy:0.0001)
                    XCTAssertEqual(values[index*4+3],1)
                }
            }
            let environment=try XCTUnwrap(course.scene.rootNode.childNode(withName:"sculptedLandscape",recursively:true)?.parent)
            let sign=try XCTUnwrap(environment.childNode(withName:"sunwardTeeSign",recursively:false))
            let signBox=sign.boundingBox
            XCTAssertLessThan((signBox.max.y-signBox.min.y)*sign.scale.y,1.6,"Course marker must not tower over the golfer")
            XCTAssertGreaterThan((signBox.max.y-signBox.min.y)*sign.scale.y,0.8)
            let route=CourseArt.resortRoute(hole)
            for node in environment.childNodes where ["canopyTree","SunwardTree","sunwardOuterGrove","sunwardGroveTree","sunwardBackdropGrove"].contains(node.name ?? "") {
                XCTAssertTrue(CourseArt.clearsResortRoute(.init(x:Double(node.position.x),d:-Double(node.position.z)),
                    radius:1.2,route:route),"Tree trunk intersects the resort path")
                XCTAssertEqual(hole.lie(at:CoursePoint(x:Double(node.position.x),d:Double(-node.position.z))),.outOfBounds,
                    "Decorative tree \(node.name ?? "") intrudes into play on hole \(hole.number)")
                if node.name != "SunwardTree" {
                    let site=CourseArt.clubhouseSite(hole)
                    XCTAssertTrue(CourseArt.treeClearsClubhouse(.init(x:Double(node.position.x),d:-Double(node.position.z)),
                        radius:4*Double(node.scale.x),site:site),"Canopy intersects clubhouse on hole \(hole.number)")
                    for i in 0..<16 {
                        let angle=Double(i)*2 * .pi/16,radius=4*Double(node.scale.x)
                        let edge=CoursePoint(x:Double(node.position.x)+cos(angle)*radius,d:-Double(node.position.z)+sin(angle)*radius)
                        XCTAssertEqual(hole.lie(at:edge),.outOfBounds,"Scaled canopy footprint intrudes on hole \(hole.number)")
                    }
                }
            }
            course.camera.position=SCNVector3(hole.pin.x+77,55,-hole.pin.d+95)
            course.camera.look(at:SCNVector3(hole.pin.x,hole.surface(at:hole.pin).heightYards,-hole.pin.d+10))
            course.camera.camera?.fieldOfView=54
            course.updatePinBeacon(pin:hole.pin,sunk:false)
            let image=renderer.snapshot(atTime:0,with:CGSize(width:1280,height:720),antialiasingMode:.multisampling4X)
            try XCTUnwrap(image.pngData()).write(to:folder.appendingPathComponent("SunwardHole-\(hole.number).png"))
            let attachment=XCTAttachment(image:image);attachment.name="Sunward hole \(hole.number)";attachment.lifetime = .keepAlways;add(attachment)
        }
        print("SUNWARD_CARDS="+folder.path)
    }
}
