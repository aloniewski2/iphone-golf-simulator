import Foundation
import simd

/// Hand-authored conversion of the accepted Seedance studies. Rig-space landmarks,
/// not estimated 3D mocap. Each family has its own silhouette and release amplitude.
/// Reference IDs and rejected defects are recorded in art/Sunward/integration-manifest.json.
enum SunwardMotionAuthor {
    struct Key {
        let grip: simd_float3
        let shaft: simd_float3
        let twist: Float
        let hip: Float
        let heel: Float
    }
    static let address = AvatarAnimations.address
    static let shaftLength = simd_distance(address.clubGrip, AvatarSize.ball)

    static func swing(_ degrees: Double, family: String) -> BodyPose3D {
        let angle = max(-150, min(150, degrees))
        if abs(angle) < 0.00001 { return address }
        if family == "putt" {
            // HD putting study: shoulder pendulum, planted feet, held finish.
            var pose = address
            let rotation = simd_quatf(angle: Float(-angle / 150) * 0.16, axis: simd_float3(1,0,0))
            let pivot = pose.shoulderCenter
            for joint in [BodyJoint.leftShoulder,.rightShoulder,.leftElbow,.rightElbow,.leftWrist,.rightWrist] {
                pose.joints[joint] = pivot + rotation.act(address[joint] - pivot)
            }
            pose.virtualClubGrip = pivot + rotation.act(address.clubGrip - pivot)
            pose.clubDirection = rotation.act(address.clubDirection)
            pose.virtualClubHead = pose.clubGrip + pose.clubDirection * shaftLength
            return pose
        }
        let back = angle > 0
        let sign: Float = back ? 1 : -1
        let compact: Float = family == "chip" ? 0.20 : family == "pitch" ? 0.57 : family == "bunker" ? 0.82 : family == "iron" ? 0.93 : 1
        let topY: Float = family == "bunker" ? 4.65 : 5.05
        let endY: Float = family == "chip" ? 2.55 : family == "pitch" ? 3.55 : family == "bunker" ? 4.85 : 5.0
        let origin = address.clubGrip
        let keys: [Key] = [
            Key(grip: origin, shaft: address.clubDirection, twist:0, hip:0, heel:0),
            Key(grip: simd_float3(1.8,2.7,sign*1.15), shaft:simd_float3(0.55,-0.35,sign*0.95), twist:-sign*0.24, hip:-sign*0.09, heel:0),
            Key(grip: simd_float3(1.12,3.9,sign*1.7), shaft:simd_float3(0.1,0.96,sign*0.25), twist:-sign*0.59, hip:-sign*0.25, heel:back ? 0 : 0.15),
            // The cartoon head is wider than the video performer's human rig.
            // Keep the high shaft outside its silhouette instead of wrapping through the cap.
            Key(grip: simd_float3(back ? 1.10 : -0.85,back ? topY : endY,sign*1.28),
                shaft:back ? simd_float3(0.25,0.85,0.45) : simd_float3(-0.72,0.30,0.62),
                twist:back ? -0.86 : 1.38, hip:back ? -0.48 : 0.98, heel:back ? 0 : 0.34)
        ]
        // Short-game uses its own bounded hand path, not a full-swing finish.
        let progress = Float(abs(angle)/150)
        let u = progress * 3 * (family == "chip" ? compact : family == "pitch" ? 0.72 : 1)
        let i = min(2,Int(u)), t = min(1,u-Float(i))
        let a=keys[max(0,i-1)], b=keys[i], c=keys[i+1], d=keys[min(3,i+2)]
        func spline(_ a:simd_float3,_ b:simd_float3,_ c:simd_float3,_ d:simd_float3)->simd_float3 {
            (2*b+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t)*0.5
        }
        let target=spline(a.grip,b.grip,c.grip,d.grip)
        let direction=simd_normalize(simd_mix(simd_normalize(b.shaft),simd_normalize(c.shaft),simd_float3(repeating:t)))
        let twist=simd_mix(b.twist,c.twist,t)*compact
        var upper=AvatarAnimations.UpperBody(twist:twist)
        upper.hipTurn=simd_mix(b.hip,c.hip,t)*compact
        upper.trailHeel=simd_mix(b.heel,c.heel,t)*compact
        upper.offset.z = back ? 0.06*progress*compact : -0.18*progress*compact
        var pose=upper.pose(left:upper.unrotate(target), right:upper.unrotate(target))
        // Project one shared grip into BOTH arms' reach before solving elbows.
        // Independent reach-clamping separated the wrists during the high finish.
        // Ease from the exact contact offsets; never alter the impact pose.
        let release = smoothstep(0,0.20,progress)
        let offsets = [simd_mix(address[.leftWrist]-origin,-direction*0.12,simd_float3(repeating:release)),
                       simd_mix(address[.rightWrist]-origin,direction*0.12,simd_float3(repeating:release))]
        var grip = target
        let shoulders: [BodyJoint] = [.leftShoulder,.rightShoulder]
        let reach = AvatarSize.upperArm + AvatarSize.forearm - 0.045
        for _ in 0..<12 {
            for side in 0..<2 {
                let delta = grip + offsets[side] - pose[shoulders[side]]
                let distance = simd_length(delta)
                if distance > reach { grip -= delta * ((distance-reach)/distance) }
            }
        }
        for side in 0..<2 {
            let elbow: BodyJoint = side == 0 ? .leftElbow : .rightElbow
            let wrist: BodyJoint = side == 0 ? .leftWrist : .rightWrist
            let shoulder = shoulders[side]
            let from = upper.rotate(simd_normalize(address[wrist]-address[shoulder]))
            let to = simd_normalize(grip+offsets[side]-pose[shoulder])
            // Transport the address bend plane with the arm. A fixed elbow pole
            // becomes parallel to the raised arm and flips the elbow at release.
            let bend = simd_quatf(from:from,to:to).act(upper.rotate(address[elbow]-address[shoulder]))
            let chain = AvatarAnimations.twoBone(from:pose[shoulders[side]],to:grip+offsets[side],
                upper:AvatarSize.upperArm,lower:AvatarSize.forearm,
                bend:bend)
            pose.joints[side == 0 ? .leftElbow : .rightElbow] = chain.joint
            pose.joints[side == 0 ? .leftWrist : .rightWrist] = chain.end
        }
        pose.virtualClubGrip=grip
        pose.clubDirection=direction
        pose.virtualClubHead=pose.clubGrip+direction*shaftLength
        pose.joints[.nose]=simd_mix(address[.nose],pose[.nose],simd_float3(repeating:back ? 0.12 : smoothstep(0.3,1,progress)))
        // Meet the exact legacy contact pose smoothly through the first 10 degrees.
        if abs(angle)<10 {
            pose=BodyPose3D.lerp(address,pose,Float(abs(angle)/10))
            pose.virtualClubHead=pose.clubGrip+pose.clubDirection*shaftLength
        }
        return pose
    }

    static func recovery(_ seconds: Double) -> BodyPose3D {
        // The Seedance take loses/breaks its club in the fall. Re-author that defect:
        // one intact dropped club, a seated landing, hands braced, then rise to standing.
        let t=Float(max(0,min(2.2,seconds)))
        let sit=smoothstep(0.15,0.60,t)*(1-smoothstep(1.1,2.1,t))
        var upper=AvatarAnimations.UpperBody(lean:-0.10*sit,crouch:1.8*sit)
        upper.offset.x = -0.25*sit
        var pose=upper.pose(left:simd_float3(0.35,-0.3-0.1*sit,-1.0),right:simd_float3(0.35,-0.3-0.1*sit,1.0))
        for (hip,knee,ankle,side) in [(BodyJoint.leftHip,BodyJoint.leftKnee,BodyJoint.leftAnkle,Float(-1)),(.rightHip,.rightKnee,.rightAnkle,Float(1))] {
            let foot=simd_float3(0.05+1.65*sit,0.12,side*(0.6+0.18*sit))
            let chain=AvatarAnimations.twoBone(from:pose[hip],to:foot,upper:AvatarSize.thigh,lower:AvatarSize.shin,bend:simd_float3(0,1,0))
            pose.joints[knee]=chain.joint;pose.joints[ankle]=chain.end
        }
        pose.clubDropped=true
        pose.clubDirection=simd_float3(0,-1,0)
        return pose
    }
}
