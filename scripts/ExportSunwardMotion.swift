import Foundation
import simd

/// Offline authoring export. Edit the source poses and phase metadata, then regenerate.
/// Seedance is an art/timing reference only; these are authored game-space poses, not mocap.
@main struct ExportSunwardMotion {
    static func sample(_ pose: BodyPose3D, at time: Double) -> AuthoredGolfMotion.Sample {
        func a(_ v:simd_float3)->[Float] { [v.x,v.y,v.z] }
        let q=pose.lean.vector
        return .init(time:time,joints:Dictionary(uniqueKeysWithValues:BodyJoint.allCases.map { ($0.rawValue,a(pose[$0])) }),
            grip:a(pose.clubGrip),head:a(pose.clubHead),lean:[q.x,q.y,q.z,q.w],dropped:pose.clubDropped)
    }
    static func main() throws {
        var clips:[AuthoredGolfMotion.Clip]=[]
        let families:[(String,GolfClub,ShotType)]=[("driver",.driver,.full),("iron",.iron,.full),
            ("chip",.wedge,.chip),("pitch",.wedge,.pitch),("bunker",.wedge,.bunker),("putt",.putter,.putt)]
        for (name,_,_) in families {
            let samples=stride(from:-150.0,through:150,by:2.5).map { sample(SunwardMotionAuthor.swing($0,family:name),at:$0) }
            clips.append(.init(name:name,domain:"swing-angle-degrees",impactTime:0,
                phases:["address":0,"backswing":75,"top":150,"impact":0,"release":-60,"finish":-150,
                    "followThroughSeconds":name == "putt" ? 0.32 : name == "chip" ? 0.36 : name == "pitch" ? 0.48 : 0.64],samples:samples))
        }
        for kind in AvatarAnimations.Reaction.allCases {
            let samples=(0...78).map { frame in
                let time=Double(frame)/30
                return sample(kind == .holed ? celebration(time) : AvatarAnimations.reaction(kind,time:time),at:time)
            }
            clips.append(.init(name:"reaction-"+kind.rawValue,domain:"seconds",impactTime:nil,
                phases:["anticipation":0,"accent":0.7,"settle":2.6],samples:samples))
        }
        let recovery=(0...66).map { sample(SunwardMotionAuthor.recovery(Double($0)/30),at:Double($0)/30) }
        clips.append(.init(name:"knockdown",domain:"seconds",impactTime:nil,phases:["fall":0,"land":0.35,"recover":1.35,"stand":2.2],samples:recovery))
        precondition(clips.allSatisfy(\.valid))
        let file=AuthoredGolfMotion.Library(version:1,source:"Sunward conversion v4: connected two-hand grip, transported elbow bend planes, head-clearing wraparound finish; individually authored from Seedance reference landmarks, not extracted motion capture",clips:clips)
        let encoder=JSONEncoder();encoder.outputFormatting=[.sortedKeys]
        try encoder.encode(file).write(to:URL(fileURLWithPath:CommandLine.arguments[1]),options:.atomic)
        print("Exported \(clips.count) validated clips")
    }

    // Reference-inspired restrained fist pump. The club stays in the left hand,
    // rather than drifting to the midpoint between a raised fist and the grip.
    static func celebration(_ time: Double) -> BodyPose3D {
        let t=Float(time), pulse=max(0,sin(min(1,max(0,(t-0.3)/1.3)) * .pi))
        var upper=AvatarAnimations.UpperBody(twist:0.12,lean:-0.04,crouch:0.08*pulse)
        upper.offset.y=0.04*pulse
        var pose=upper.pose(left:simd_float3(0.75,0.30,-0.82),right:simd_float3(0.6,0.3+pulse*1.3,0.9))
        pose.virtualClubGrip=pose[.leftWrist]
        pose.clubDirection=simd_normalize(simd_float3(0.18,-1,0.06))
        pose.virtualClubHead=pose.clubGrip+pose.clubDirection*SunwardMotionAuthor.shaftLength
        return pose
    }
}
