import SceneKit
import AVFoundation
import ImageIO
import UniformTypeIdentifiers
import simd
import XCTest
@testable import GolfArcade

final class VisualAssetTests: XCTestCase {
    /// Opt-in time-domain render of the production rig and post-impact policy.
    /// This is a native animation review, not generated footage or a gameplay capture.
    @MainActor func testOptInRuntimeMotionReview() throws {
        guard ProcessInfo.processInfo.environment["GOLF_MOTION_REVIEW"] == "1" else {
            throw XCTSkip("Set GOLF_MOTION_REVIEW=1 to export continuous runtime rig reviews")
        }
        let course=CourseScene();course.stop()
        for node in course.scene.rootNode.childNodes where node !== course.camera && node.light == nil { node.removeFromParentNode() }
        course.scene.background.contents=UIColor(red:0.76,green:0.84,blue:0.88,alpha:1)
        let floor=SCNNode(geometry:SCNPlane(width:40,height:40));floor.eulerAngles.x = -.pi/2
        floor.geometry?.firstMaterial?.diffuse.contents=UIColor(white:0.7,alpha:1)
        course.scene.rootNode.addChildNode(floor)
        let look=GolferAppearance.preset(.cove)
        let rig=AvatarRig(shirt:look.shirtColor,authored:false,appearance:look)
        course.scene.rootNode.addChildNode(rig.node)
        let renderer=SCNRenderer(device:nil,options:nil);renderer.scene=course.scene;renderer.pointOfView=course.camera
        course.camera.camera?.fieldOfView=43
        course.camera.position=SCNVector3(4,2.2,3)
        course.camera.look(at:SCNVector3(0.25,1.05,0),up:SCNVector3(0,1,0),localFront:SCNVector3(0,0,-1))
        let folder=FileManager.default.urls(for:.documentDirectory,in:.userDomainMask)[0].appendingPathComponent("RuntimeMotionReview")
        try FileManager.default.createDirectory(at:folder,withIntermediateDirectories:true)
        let hole=Course.sunwardResort.holes[0]
        let frameCount=121, fps=24.0
        for (club,type) in [(GolfClub.driver,ShotType.full),(.iron,.full),(.wedge,.chip),(.wedge,.pitch),(.wedge,.bunker),(.putter,.putt)] {
          for mirrored in [false,true] {
            rig.setClub(club);rig.setMirrored(mirrored)
            rig.node.simdScale *= AvatarSize.courseScale
            let request=ShotRequest(club:club,targetHeading:0,type:type,execution:SwingImpact(power:0.6),simulationVersion:hole.simulationVersion)
            let shot=RangeShot(id:11,request:request,origin:hole.tee,hole:hole)
            let name=AuthoredGolfMotion.family(club:club,type:type)+(mirrored ? "-left" : "-right")
            let destination=try XCTUnwrap(CGImageDestinationCreateWithURL(folder.appendingPathComponent(name+".gif") as CFURL,UTType.gif.identifier as CFString,frameCount,nil))
            CGImageDestinationSetProperties(destination,[kCGImagePropertyGIFDictionary:[kCGImagePropertyGIFLoopCount:0]] as CFDictionary)
            var keyframes:[UIImage]=[]
            for frame in 0..<frameCount {
                try autoreleasepool {
                    let elapsed=Double(frame)/fps-1
                    // The one-second lead-in is an explicit authored backswing;
                    // impact onward invokes the same clock used during gameplay.
                    let pose:BodyPose3D
                    if elapsed < 0 {
                        let t=elapsed+1
                        let angle=t<0.7 ? 150*Double(smoothstep(0,0.7,Float(t))) : 150*(1-Double(smoothstep(0.7,1,Float(t))))
                        pose=AuthoredGolfMotion.swing(degrees:angle,club:club,type:type)
                    } else {
                        pose=course.golferPose(shot:shot,elapsed:elapsed,isReplay:false,swingAngle:0,now:10+elapsed)
                    }
                    rig.apply(pose)
                    let image=renderer.snapshot(atTime:Double(frame)/fps,with:CGSize(width:480,height:480),antialiasingMode:.multisampling4X)
                    CGImageDestinationAddImage(destination,try XCTUnwrap(image.cgImage),[kCGImagePropertyGIFDictionary:[kCGImagePropertyGIFDelayTime:1/fps]] as CFDictionary)
                    if [0,16,24,32,49,68,87,96,102,108,114,120].contains(frame) { keyframes.append(image) }
                }
            }
            XCTAssertTrue(CGImageDestinationFinalize(destination))
            let format=UIGraphicsImageRendererFormat();format.scale=1
            let sheet=UIGraphicsImageRenderer(size:CGSize(width:1920,height:1440),format:format).image { _ in
                for (index,image) in keyframes.enumerated() { image.draw(in:CGRect(x:index%4*480,y:index/4*480,width:480,height:480)) }
            }
            try XCTUnwrap(sheet.pngData()).write(to:folder.appendingPathComponent(name+"-timed.png"))
          }
        }
        print("RUNTIME_MOTION_REVIEW="+folder.path)
    }

    @MainActor func testRuntimeFollowThroughJoinsReturnWithoutSkippingPoses() {
        let hole=Course.sunwardResort.holes[0]
        for (club,type) in [(GolfClub.driver,ShotType.full),(.iron,.full),(.wedge,.chip),(.wedge,.pitch),(.wedge,.bunker),(.putter,.putt)] {
          for reduceMotion in [false,true] {
            let scene=CourseScene();scene.stop()
            scene.inputs=SceneInputs(hole:hole,ball:hole.tee,heading:0,distanceToPin:hole.length,
                lie:.tee,club:club,aim:0,handedness:.right,shot:nil,isReplay:false,
                flightStart:nil,pausedAt:nil,swingAngle:0,bystanders:[],reduceMotion:reduceMotion)
            let request=ShotRequest(club:club,targetHeading:0,type:type,execution:SwingImpact(power:0.6),simulationVersion:hole.simulationVersion)
            let shot=RangeShot(id:11,request:request,origin:hole.tee,hole:hole)
            let before=scene.golferPose(shot:shot,elapsed:1.0499,isReplay:false,swingAngle:0,now:10)
            let after=scene.golferPose(shot:shot,elapsed:1.0501,isReplay:false,swingAngle:0,now:10.0002)
            for joint in BodyJoint.allCases {
                XCTAssertLessThan(simd_distance(before[joint],after[joint]),0.01,"\(club) / \(type) / \(joint): phase transition skips poses")
            }
            XCTAssertLessThan(simd_distance(before.clubHead,after.clubHead),0.01,"\(club) / \(type): club jumps at return boundary")
            // Also exercise the complete return clock, not merely static clip samples.
            for boundary in [1.85,2.6,3.25,3.65] {
                let a=scene.golferPose(shot:shot,elapsed:boundary-0.0001,isReplay:false,swingAngle:0,now:10)
                let b=scene.golferPose(shot:shot,elapsed:boundary+0.0001,isReplay:false,swingAngle:0,now:10)
                for joint in BodyJoint.allCases {
                    XCTAssertLessThan(simd_distance(a[joint],b[joint]),0.01,"\(type) at \(boundary): \(joint) jumps")
                }
                XCTAssertLessThan(simd_distance(a.clubHead,b.clubHead),0.01,"\(type) at \(boundary): club jumps")
            }
          }
        }
    }

    @MainActor
    func testSceneStartPrimesCourseAndCameraBeforeFirstDisplayLink() {
        let hole=Course.easy.holes[0],scene=CourseScene()
        scene.inputs=SceneInputs(hole:hole,ball:hole.tee,heading:0,distanceToPin:hole.length,
            lie:.tee,club:.driver,aim:0,handedness:.right,shot:nil,isReplay:false,
            flightStart:nil,pausedAt:nil,swingAngle:0,bystanders:[])
        scene.start()
        defer { scene.stop() }
        XCTAssertGreaterThan(simd_length(scene.camera.simdPosition),1)
        XCTAssertNotNil(scene.scene.rootNode.childNode(withName:"sculptedLandscape",recursively:true))
        XCTAssertNotNil(scene.animationState)
    }
    @MainActor
    func testMenuFilmOwnsNoAirPlayRouteAndReleasesDecoder() async throws {
        let view=SunwardMenuFilm.FilmView(frame:CGRect(x:0,y:0,width:320,height:180))
        XCTAssertNotNil(view.looper)
        XCTAssertTrue(view.player.isMuted)
        XCTAssertFalse(view.player.allowsExternalPlayback)
        XCTAssertFalse(view.isUserInteractionEnabled)
        view.player.play()
        let deadline=Date().addingTimeInterval(8)
        while !(view.player.currentTime().seconds.isFinite && view.player.currentTime().seconds > 0.1) && Date()<deadline {
            try await Task.sleep(for:.milliseconds(100))
        }
        XCTAssertGreaterThan(view.player.currentTime().seconds,0.1,
            "Bundled cinematic must actually decode and play: \(String(describing:view.player.error)) / \(String(describing:view.player.currentItem?.error))")
        view.player.pause()
        XCTAssertEqual(view.player.rate,0)
        SunwardMenuFilm.dismantleUIView(view,coordinator:())
        XCTAssertTrue(view.player.items().isEmpty)
        XCTAssertEqual(view.player.rate,0)
    }
    @MainActor
    func testHoledPuttShowsConvertedCelebrationOnlyAfterCupDrop() throws {
        var hole=Course.easy.holes[0];hole.terrain = .flat
        let origin=CoursePoint(x:hole.pin.x,d:hole.pin.d-6)
        let power=try XCTUnwrap(RangeShot.power(toReach:6.2,with:.putter))
        let shot=RangeShot(id:99,club:.putter,power:power,aim:0,origin:origin,heading:origin.heading(to:hole.pin),hole:hole)
        XCTAssertTrue(shot.isHoled)
        let scene=CourseScene();scene.stop()
        let elapsed=shot.duration+1.25
        let pose=scene.golferPose(shot:shot,elapsed:elapsed,isReplay:false,swingAngle:0,now:0)
        let expected=AuthoredGolfMotion.reaction(.holed,time:0.8)
        for joint in BodyJoint.allCases { XCTAssertLessThan(simd_distance(pose[joint],expected[joint]),0.0001) }
        XCTAssertLessThan(simd_distance(pose.clubHead,expected.clubHead),0.0001)
        for time in stride(from:0.0,through:2.6,by:0.025) {
            let celebration=AuthoredGolfMotion.reaction(.holed,time:time)
            XCTAssertGreaterThanOrEqual(celebration.clubHead.y,0,"Celebration must not bury the club")
            XCTAssertLessThan(simd_distance(celebration.clubGrip,celebration[.leftWrist]),0.001)
            XCTAssertEqual(simd_distance(celebration.clubHead,celebration.clubGrip),
                simd_distance(AvatarAnimations.address.clubGrip,AvatarSize.ball),accuracy:0.001)
        }
        var input=ShotCameraDirector.Inputs(ball:origin,heading:shot.heading,aim:0,distanceToPin:6,
            onGreen:true,handedness:.right,shot:shot,elapsed:elapsed,reaction:.holed,landingTime:nil)
        XCTAssertEqual(ShotCameraDirector.stage(input),.celebration)
        input.elapsed=shot.duration+0.2
        XCTAssertEqual(ShotCameraDirector.stage(input),.landing)
        input.elapsed=elapsed;input.reduceMotion=true
        XCTAssertEqual(ShotCameraDirector.stage(input),.landing)
        input.reduceMotion=false;input.liveCamera=true
        XCTAssertEqual(ShotCameraDirector.stage(input),.landing)
    }
    @MainActor
    func testLegacyPreviewPreferenceCannotReplaceProductionGolfer() {
        let key="presentation.authoredGolfer", old=UserDefaults.standard.object(forKey:key)
        defer { if let old { UserDefaults.standard.set(old,forKey:key) } else { UserDefaults.standard.removeObject(forKey:key) } }
        UserDefaults.standard.set(true,forKey:key)
        let rig=AvatarRig(shirt:.systemTeal)
        XCTAssertNotNil(rig.node.childNode(withName:"sunwardAuthoredGolfer",recursively:true))
        XCTAssertNil(rig.node.childNode(withName:"authoredResortGolfer",recursively:true))
    }
    @MainActor
    func testFlyoverUsesEachRealHoleAndStaysAboveTerrain() {
        for hole in Course.sunwardResort.holes {
            var previous:simd_float3?
            for t in stride(from:0.0,through:1,by:0.025) {
                let view=CourseArt.flyover(hole,progress:t)
                XCTAssertTrue(view.position.x.isFinite && view.position.y.isFinite && view.position.z.isFinite)
                let ground=CourseArt.dressingHeight(CoursePoint(x:Double(view.position.x),d:-Double(view.position.z)),hole:hole)
                XCTAssertGreaterThan(view.position.y,ground+2)
            }
            for t in stride(from:0.0,through:1,by:0.001) {
                let view=CourseArt.flyover(hole,progress:t)
                if let previous { XCTAssertLessThan(simd_distance(previous,view.position),2,"No dogleg camera snaps") }
                previous=view.position
            }
        }
    }
    func testConvertedMotionsHaveStableClubsAndGroundedFeet() throws {
        let library=try XCTUnwrap(AuthoredGolfMotion.library)
        XCTAssertTrue(library.source.contains("conversion v4"))
        let length=simd_distance(AvatarAnimations.address.clubGrip,AvatarSize.ball)
        for name in ["driver","iron","chip","pitch","bunker","putt"] {
            let clip=try XCTUnwrap(library.clips.first {$0.name==name})
            XCTAssertEqual(clip.impactTime,0)
            XCTAssertLessThan(simd_distance(clip.pose(at:0).clubHead,AvatarSize.ball),0.001)
            for angle in stride(from:-150.0,through:150,by:0.75) {
                let pose=clip.pose(at:angle)
                XCTAssertEqual(simd_distance(pose.clubGrip,pose.clubHead),length,accuracy:0.001,name)
                XCTAssertLessThan(simd_distance(pose.clubGrip,pose.handCenter),0.001,name)
                XCTAssertEqual(pose[.leftAnkle].y,0.12,accuracy:0.001,name)
                XCTAssertLessThan(simd_distance(pose[.leftWrist],pose[.rightWrist]),0.36,name)
                let headCenter=pose[.nose]+simd_float3(0,0.19,0)
                let shaft=pose.clubHead-pose.clubGrip
                let nearest=max(0,min(1,simd_dot(headCenter-pose.clubGrip,shaft)/simd_length_squared(shaft)))
                XCTAssertGreaterThan(simd_distance(headCenter,pose.clubGrip+shaft*nearest),0.85,
                    "\(name) \(angle): shaft must clear the oversized cartoon head")
            }
        }
        let chip=AuthoredGolfMotion.swing(degrees:150,club:.wedge,type:.chip)
        let pitch=AuthoredGolfMotion.swing(degrees:150,club:.wedge,type:.pitch)
        let iron=AuthoredGolfMotion.swing(degrees:150,club:.iron,type:.full)
        XCTAssertLessThan(chip.handCenter.y,pitch.handCenter.y)
        XCTAssertLessThan(pitch.handCenter.y,iron.handCenter.y)
        XCTAssertGreaterThan(simd_distance(iron.handCenter,AvatarAnimations.swingArc(degrees:150,club:.iron).handCenter),0.1,
            "The converted clip must not silently be the old procedural export")
    }

    @MainActor
    func testImportedTreeAndMenuFilmAreRuntimeResources() throws {
        XCTAssertNotNil(Bundle.main.url(forResource:"SunwardMenu",withExtension:"mp4"))
        XCTAssertNotNil(SunwardAsset.prop("SunwardTree"),"Imported topiary remains the courtyard asset")
        XCTAssertTrue(CourseArt.trees.allSatisfy {$0.name == "canopyTree"})
        let parent=SCNNode();CourseArt.plantTrees(along:Course.sunwardResort.holes[0],parent:parent)
        XCTAssertNotNil(parent.childNode(withName:"canopyTree",recursively:true))
    }

    func testRecoveryUsesSeatedPoseAndReturnsUpright() {
        let sit=AuthoredGolfMotion.recovery(time:0.8,push:simd_float3(-1,0,0))
        let stand=AuthoredGolfMotion.recovery(time:2.2,push:simd_float3(-1,0,0))
        XCTAssertLessThan(sit[.root].y,1)
        XCTAssertGreaterThan(stand[.root].y,2.3)
        XCTAssertTrue(sit.clubDropped)
        for t in stride(from:0.0,through:2.2,by:0.02) {
            let p=AuthoredGolfMotion.recovery(time:t,push:simd_float3(0,0,1))
            XCTAssertTrue(BodyJoint.allCases.allSatisfy { p[$0].y >= 0 })
        }
    }

    @MainActor
    func testRenderConvertedMotionFamilies() throws {
        let game=CourseScene(), scene=game.scene
        // Isolate the production golfer and lighting; do not leave the game's own tiny
        // default golfer and pin underneath the review rig.
        for node in scene.rootNode.childNodes where node.camera == nil && node.light == nil { node.removeFromParentNode() }
        let rig=AvatarRig(shirt:.systemTeal,authored:false,appearance:.preset(.cove))
        scene.rootNode.addChildNode(rig.node)
        game.camera.position=SCNVector3(12,6,8);game.camera.look(at:SCNVector3(0.7,3,0))
        let renderer=SCNRenderer(device:nil,options:nil);renderer.scene=scene;renderer.pointOfView=game.camera
        for (name,club,type) in [("driver",GolfClub.driver,ShotType.full),("iron",.iron,.full),("chip",.wedge,.chip),
            ("pitch",.wedge,.pitch),("bunker",.wedge,.bunker),("putt",.putter,.putt)] {
            rig.setClub(club)
            let format=UIGraphicsImageRendererFormat();format.scale=1
            var captures:[UIImage]=[]
            for angle in [0.0,75,150,-60,-150] {
                rig.apply(AuthoredGolfMotion.swing(degrees:angle,club:club,type:type))
                captures.append(renderer.snapshot(atTime:0,with:CGSize(width:360,height:480),antialiasingMode:.multisampling4X))
            }
            let sheet=UIGraphicsImageRenderer(size:CGSize(width:1800,height:480),format:format).image {_ in
                for (index,image) in captures.enumerated() { image.draw(in:CGRect(x:index*360,y:0,width:360,height:480)) }
            }
            let attachment=XCTAttachment(image:sheet);attachment.name="converted-"+name;attachment.lifetime = .keepAlways;add(attachment)
        }
        rig.setClub(.putter)
        for name in ["celebration","recovery"] {
            let format=UIGraphicsImageRendererFormat();format.scale=1
            var captures:[UIImage]=[]
            for time in [0.0,0.45,0.85,1.5,2.2] {
                rig.apply(name == "celebration" ? AuthoredGolfMotion.reaction(.holed,time:time) :
                    AuthoredGolfMotion.recovery(time:time,push:simd_float3(-1,0,0)))
                captures.append(renderer.snapshot(atTime:0,with:CGSize(width:360,height:480),antialiasingMode:.multisampling4X))
            }
            let sheet=UIGraphicsImageRenderer(size:CGSize(width:1800,height:480),format:format).image {_ in
                for (index,image) in captures.enumerated() { image.draw(in:CGRect(x:index*360,y:0,width:360,height:480)) }
            }
            let attachment=XCTAttachment(image:sheet);attachment.name="converted-"+name;attachment.lifetime = .keepAlways;add(attachment)
        }
    }
    @MainActor
    func testSunwardAssetsAndTurfPixels() throws {
        for name in ["SunwardGolfer", "SunwardTree", "SunwardRocks"] {
            let asset = try XCTUnwrap(SunwardAsset.load(name), name)
            XCTAssertTrue(asset.valid)
            // The reference-matched golfer uses ~13k vertices; props retain their 10k cap.
            XCTAssertLessThan(asset.meshes.reduce(0) { $0 + $1.positions.count / 3 }, name == "SunwardGolfer" ? 16_000 : 10_000)
        }
        let image = try XCTUnwrap(CourseArt.fairwayMaterial.diffuse.contents as? UIImage)
        let cg = try XCTUnwrap(image.cgImage)
        var pixel = [UInt8](repeating: 0, count: 4)
        let context = try XCTUnwrap(CGContext(data: &pixel, width: 1, height: 1, bitsPerComponent: 8, bytesPerRow: 4,
            space: CGColorSpaceCreateDeviceRGB(), bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue))
        context.draw(cg, in: CGRect(x: 0, y: 0, width: 1, height: 1))
        XCTAssertEqual(pixel[3], 255, "Course texture must remain fully opaque")
        XCTAssertGreaterThan(pixel[1], pixel[2], "Fairway texture must be green, not blue: \(pixel)")
        XCTAssertGreaterThan(pixel[1], pixel[0], "Fairway texture must be green: \(pixel)")
    }
    @MainActor
    func testPoloTrianglesRespectExactClothingSeams() {
        let mesh = GolferSkin.mesh
        let sleeve = AvatarSize.shoulderHalfWidth + AvatarSize.upperArm * 0.56
        for index in Set(mesh.triangles[0]) {
            let p = mesh.vertices[Int(index)]
            XCTAssertGreaterThanOrEqual(p.y, AvatarSize.hipHeight + 0.12 - 0.0001)
            XCTAssertLessThanOrEqual(p.y, GolferSkin.rest[.neck].y + 0.4201)
            XCTAssertLessThanOrEqual(abs(p.z), sleeve + 0.0001)
        }
        XCTAssertLessThan(mesh.vertices.count, 60_000)
    }

    @MainActor
    func testRenderedResortSurfaceMatchesPhysicsAndHasBoundedGeometry() {
        for hole in Course.sunwardResort.holes {
            let geometry = CourseArt.playableSurface(hole)
            XCTAssertEqual(geometry.materials.count, 8, "Deep rough and exposed bank soil have distinct materials")
            let source = geometry.sources(for: .vertex)[0]
            XCTAssertLessThan(source.vectorCount, 220_000, "Hole \(hole.number)")
            // Check sampled vertices of the actual render mesh, not a second terrain model.
            source.data.withUnsafeBytes { bytes in
                for index in stride(from: 0, to: source.vectorCount, by: 31) {
                    let offset = source.dataOffset + index * source.dataStride
                    let x = bytes.loadUnaligned(fromByteOffset: offset, as: Float.self)
                    let y = bytes.loadUnaligned(fromByteOffset: offset + 4, as: Float.self)
                    let z = bytes.loadUnaligned(fromByteOffset: offset + 8, as: Float.self)
                    let surface = hole.surface(at: CoursePoint(x: Double(x), d: -Double(z)))
                    if surface.lie != .outOfBounds {
                        XCTAssertEqual(Double(y), surface.heightYards, accuracy: 0.0001)
                    } else {
                        XCTAssertEqual(y, CourseArt.surfaceHeight(CoursePoint(x: Double(x), d: -Double(z)), hole: hole), accuracy: 0.0001)
                    }
                }
            }
        }
    }

    func testShortGameFamiliesStayCompactWithoutChangingAddressContact() {
        let full = AvatarAnimations.swingArc(degrees: 150, club: .wedge)
        let pitch = AvatarAnimations.swingArc(degrees: 150, club: .wedge, type: .pitch)
        let chip = AvatarAnimations.swingArc(degrees: 150, club: .wedge, type: .chip)
        XCTAssertLessThan(chip.handCenter.y, pitch.handCenter.y)
        XCTAssertLessThan(pitch.handCenter.y, full.handCenter.y)
        for type in [ShotType.full, .chip, .pitch, .bunker] {
            let pose = AvatarAnimations.swingArc(degrees: 0, club: .wedge, type: type)
            XCTAssertLessThan(simd_distance(pose.clubHead, AvatarSize.ball), 0.001)
        }
        let chipFinish = AvatarAnimations.swingArc(degrees: -150, club: .wedge, type: .chip)
        XCTAssertEqual(chipFinish[.rightAnkle], AvatarAnimations.address[.rightAnkle])
    }
}
