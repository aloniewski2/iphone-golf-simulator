import Foundation
import simd

/// Locally exported, editable pose samples. No video decoding or network work in gameplay.
/// This presentation library cannot produce an impact or replace a measured camera pose.
enum AuthoredGolfMotion {
    struct Sample: Codable, Sendable {
        let time: Double
        let joints: [String: [Float]]
        let grip: [Float], head: [Float], lean: [Float]
        let dropped: Bool
        var valid: Bool {
            time.isFinite && joints.count == BodyJoint.allCases.count &&
            BodyJoint.allCases.allSatisfy { joints[$0.rawValue]?.count == 3 && joints[$0.rawValue]!.allSatisfy(\.isFinite) } &&
            [grip, head].allSatisfy { $0.count == 3 && $0.allSatisfy(\.isFinite) } &&
            lean.count == 4 && lean.allSatisfy(\.isFinite)
        }
        var pose: BodyPose3D {
            func v(_ a: [Float]) -> simd_float3 { simd_float3(a[0],a[1],a[2]) }
            let g=v(grip), h=v(head)
            var result=BodyPose3D(joints:Dictionary(uniqueKeysWithValues:BodyJoint.allCases.map { ($0,v(joints[$0.rawValue]!)) }),
                clubDirection:simd_normalize(h-g))
            result.virtualClubGrip=g; result.virtualClubHead=h; result.clubDropped=dropped
            result.lean=simd_quatf(vector:simd_float4(lean[0],lean[1],lean[2],lean[3]))
            return result
        }
    }
    struct Clip: Codable, Sendable {
        let name: String, domain: String
        let impactTime: Double?
        let phases: [String: Double]
        let samples: [Sample]
        var valid: Bool {
            samples.count >= 2 && samples.count <= 1000 && samples.allSatisfy(\.valid) &&
            zip(samples,samples.dropFirst()).allSatisfy { $0.time < $1.time } &&
            phases.values.allSatisfy(\.isFinite)
        }
        func pose(at value: Double) -> BodyPose3D {
            let value=value.isFinite ? value : samples[0].time
            if value <= samples[0].time { return samples[0].pose }
            if value >= samples.last!.time { return samples.last!.pose }
            var low=0, high=samples.count-1
            while high-low>1 { let mid=(low+high)/2; if samples[mid].time<=value { low=mid } else { high=mid } }
            let a=samples[low], b=samples[high]
            let t=Float((value-a.time)/(b.time-a.time))
            var pose=BodyPose3D.lerp(a.pose,b.pose,t)
            // Position interpolation alone shrinks the shaft between samples.
            let length=simd_mix(simd_distance(a.pose.clubHead,a.pose.clubGrip),simd_distance(b.pose.clubHead,b.pose.clubGrip),t)
            pose.virtualClubHead=pose.clubGrip+pose.clubDirection*length
            return pose
        }
    }
    struct Library: Codable, Sendable { let version: Int; let source: String; let clips: [Clip] }
    static let library: Library? = {
        guard let url=Bundle.main.url(forResource:"SunwardMotion",withExtension:"json"),
            let data=try? Data(contentsOf:url),let file=try? JSONDecoder().decode(Library.self,from:data),
            file.version==1,file.clips.allSatisfy(\.valid) else { return nil }
        return file
    }()
    static func family(club: GolfClub, type: ShotType) -> String {
        if club == .putter || type == .putt { return "putt" }
        switch type { case .chip: return "chip"; case .pitch: return "pitch"; case .bunker: return "bunker"; default: return [.driver,.wood3].contains(club) ? "driver" : "iron" }
    }
    static func swing(degrees: Double, club: GolfClub, type: ShotType) -> BodyPose3D {
        guard let clip=library?.clips.first(where:{$0.name==family(club:club,type:type)}) else {
            return AvatarAnimations.swingArc(degrees:degrees,club:club,type:type)
        }
        return clip.pose(at:degrees)
    }
    /// Contact is zero, explicitly aligned with the existing shot event.
    static func followThrough(elapsed: Double, club: GolfClub, type: ShotType) -> BodyPose3D {
        let duration=library?.clips.first(where:{$0.name==family(club:club,type:type)})?.phases["followThroughSeconds"] ?? 0.42
        let t=max(0,min(1,elapsed/duration))
        return swing(degrees:-150*(1-pow(1-t,2)),club:club,type:type)
    }
    static func recovery(time: Double, push: simd_float3) -> BodyPose3D {
        guard var pose=library?.clips.first(where:{$0.name=="knockdown"})?.pose(at:time) else {
            return AvatarAnimations.knockdown(time:time,push:push)
        }
        // Author sits backward (-x); turn that performance away from the actual hit.
        var direction=simd_float3(push.x,0,push.z)
        if simd_length(direction)<0.001 { direction=simd_float3(-1,0,0) }
        let rotation=simd_quatf(from:simd_float3(-1,0,0),to:simd_normalize(direction))
        for joint in BodyJoint.allCases { pose.joints[joint]=rotation.act(pose[joint]) }
        pose.virtualClubGrip=rotation.act(pose.clubGrip)
        pose.virtualClubHead=rotation.act(pose.clubHead)
        pose.clubDirection=rotation.act(pose.clubDirection)
        return pose
    }
    static func reaction(_ kind: AvatarAnimations.Reaction, time: Double) -> BodyPose3D {
        var pose = library?.clips.first(where:{$0.name=="reaction-"+kind.rawValue})?.pose(at:time)
            ?? AvatarAnimations.reaction(kind,time:time)
        // Legacy reactions used the nominal club length, while contact uses the
        // calibrated address shaft. Never change the prop's size after a shot.
        pose.virtualClubHead=pose.clubGrip+pose.clubDirection*simd_distance(AvatarAnimations.address.clubGrip,AvatarSize.ball)
        return pose
    }
    /// Keep a good shot in its own authored finish rather than switching to the
    /// unrelated legacy driver's pose immediately after release.
    static func heldFinish(time: Double, club: GolfClub, type: ShotType) -> BodyPose3D {
        var pose = swing(degrees:-150,club:club,type:type)
        let settle = Float(sin(max(0,time)*2.0)) * 0.018
        pose.joints[.nose]?.y += settle
        return pose
    }

    /// Lower the held club along the reviewed outside-body arc. A shortest-angle
    /// blend directly from the shoulder finish to address cuts the shaft through
    /// the chest even when every joint length is valid.
    static func returnToAddress(progress: Float, club: GolfClub, type: ShotType) -> BodyPose3D {
        swing(degrees:-150*Double(1-max(0,min(1,progress))),club:club,type:type)
    }

    /// Presentation-only blend. Cartesian joint interpolation cuts across the
    /// elbow/knee arc, shrinking limbs and the shaft during a finish-to-idle blend.
    /// Preserve the interpolated contacts, then solve each articulated chain.
    /// Measured camera poses intentionally do not pass through this correction.
    static func transition(_ a: BodyPose3D, _ b: BodyPose3D, _ amount: Float) -> BodyPose3D {
        let t=max(0,min(1,amount))
        if t == 0 { return a }; if t == 1 { return b }
        var pose=BodyPose3D.lerp(a,b,t)
        func bend(_ start: BodyJoint, _ middle: BodyJoint, _ end: BodyJoint, target: simd_float3) -> simd_float3 {
            let axis=simd_normalize(target-pose[start])
            func transported(_ source: BodyPose3D) -> simd_float3 {
                let original=simd_normalize(source[end]-source[start])
                var pole=simd_quatf(from:original,to:axis).act(source[middle]-source[start])
                pole -= simd_dot(pole,axis)*axis
                if simd_length_squared(pole)<0.000001 {
                    let fallback=abs(axis.y)<0.9 ? simd_float3(0,1,0) : simd_float3(1,0,0)
                    pole=fallback-simd_dot(fallback,axis)*axis
                }
                return simd_normalize(pole)
            }
            let from=transported(a),to=transported(b)
            // Interpolate bend ANGLE, not pole positions: opposing poles must
            // travel around the limb axis instead of collapsing through zero.
            let angle=atan2(simd_dot(axis,simd_cross(from,to)),simd_dot(from,to))
            return simd_quatf(angle:angle*t,axis:axis).act(from)
        }
        let rotation=simd_slerp(simd_quatf(angle:0,axis:simd_float3(0,1,0)),
            simd_quatf(from:a.clubDirection,to:b.clubDirection),t)
        pose.clubDirection=simd_normalize(rotation.act(a.clubDirection))
        let arms:[(BodyJoint,BodyJoint,BodyJoint)]=[
            (.leftShoulder,.leftElbow,.leftWrist),(.rightShoulder,.rightElbow,.rightWrist)]
        let attached=arms.map { _,_,w in
            !a.clubDropped && !b.clubDropped && simd_distance(a[w],a.clubGrip)<0.22 && simd_distance(b[w],b.clubGrip)<0.22
        }
        let offsets=arms.map { _,_,w in simd_mix(a[w]-a.clubGrip,b[w]-b.clubGrip,simd_float3(repeating:t)) }
        var grip=pose.clubGrip
        for _ in 0..<12 {
            for (i,arm) in arms.enumerated() where attached[i] {
                let delta=grip+offsets[i]-pose[arm.0],distance=simd_length(delta)
                let reach=AvatarSize.upperArm+AvatarSize.forearm-0.025
                if distance>reach { grip -= delta*((distance-reach)/distance) }
            }
        }
        for (i,arm) in arms.enumerated() {
            let target=attached[i] ? grip+offsets[i] : pose[arm.2]
            let solved=AvatarAnimations.twoBone(from:pose[arm.0],to:target,
                upper:AvatarSize.upperArm,lower:AvatarSize.forearm,bend:bend(arm.0,arm.1,arm.2,target:target))
            pose.joints[arm.1]=solved.joint;pose.joints[arm.2]=solved.end
        }
        for (hip,knee,ankle) in [(BodyJoint.leftHip,BodyJoint.leftKnee,BodyJoint.leftAnkle),(.rightHip,.rightKnee,.rightAnkle)] {
            let solved=AvatarAnimations.twoBone(from:pose[hip],to:pose[ankle],
                upper:AvatarSize.thigh,lower:AvatarSize.shin,bend:bend(hip,knee,ankle,target:pose[ankle]))
            pose.joints[knee]=solved.joint;pose.joints[ankle]=solved.end
        }
        pose.virtualClubGrip=grip
        let length=simd_mix(simd_distance(a.clubGrip,a.clubHead),simd_distance(b.clubGrip,b.clubHead),t)
        pose.virtualClubHead=grip+pose.clubDirection*length
        return pose
    }
}
