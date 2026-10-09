using UnityEngine;

namespace GolfArcade.Tennis
{
    /// Shared finger refinement. Golf preserves the closed authored grip and
    /// relaxes the arms around it without changing the club trajectory or bind rig.
    [DefaultExecutionOrder(1160), DisallowMultipleComponent]
    public sealed class HeroGripPolish : MonoBehaviour
    {
        public MatchHeroLook look;
        void LateUpdate() {
            if (!look) look=GetComponent<MatchHeroLook>();
            // Golf evaluates grip synchronously once per source pose.
            // Re-canonicalizing digits on an already rolled hand breaks that grip.
            if (look && look.golfKit) return;
            ApplyGrip(look);
        }
        static Transform Bone(MatchHeroLook hero, bool left, string finger, string joint)
        {
            string name=(left ? "Left":"Right")+finger+joint;
            return System.Enum.TryParse<HumanBodyBones>(name,out var id) ? hero.Bone(id):null;
        }
        /// Shared by reaction authoring: true anatomical hinge axes, including differently
        /// rolled intermediate/distal joints. Strength zero preserves the sampled pose.
        public static void ApplyFist(MatchHeroLook hero,bool left,float strength,float palmHeight=.68f)
        {
            if(!hero||strength<=0)return;
            if(HeroAuthoredHandPose.ApplyFist(hero,left,strength))return;
            // Keep the authored wrist pose: finger closure must not rotate the wrist seam.
            var rest=Rest(hero,palmHeight);var hand=hero.Bone(left ? HumanBodyBones.LeftHand:HumanBodyBones.RightHand);
            if(!hand)return;float w=Mathf.Clamp01(strength);string prefix=left ? "Left":"Right";
            string[] joints={"Proximal","Intermediate","Distal"};float[] curl={90,110,65};
            foreach(string digit in new[]{"Index","Middle","Ring","Little"}){
                var chain=new RestJoint[3];bool complete=true;
                for(int j=0;j<3;j++)if(!rest.TryGetValue(prefix+digit+joints[j],out chain[j])){complete=false;break;}
                if(!complete)continue;var original=new[]{chain[0].bone.localRotation,chain[1].bone.localRotation,chain[2].bone.localRotation};
                var angles=new[]{80f,100f,50f};
                void Set(int j,float a){angles[j]=a;chain[j].bone.localRotation=chain[j].local*Quaternion.AngleAxis(a,chain[j].hinge);}
                var target=hand.TransformPoint(chain[0].palmTarget);
                Vector3 Tip()=>chain[2].bone.TransformPoint(chain[2].tip);
                float Cost(){float e=(Tip()-target).sqrMagnitude;return e+1e-10f*((angles[0]-80)*(angles[0]-80)+(angles[1]-100)*(angles[1]-100)+(angles[2]-50)*(angles[2]-50));}
                // Complete three-joint anatomical search avoids the folded-chain local
                // minimum of a single-joint CCD pass. Pads target the real palm skin.
                var bestAngles=new float[3];float bestError=float.PositiveInfinity;
                for(float a=0;a<=100;a+=10){Set(0,a);for(float b=0;b<=140;b+=10){Set(1,b);for(float c=0;c<=80;c+=10){Set(2,c);float e=Cost();if(e<bestError){bestError=e;System.Array.Copy(angles,bestAngles,3);}}}}
                for(int j=0;j<3;j++)Set(j,bestAngles[j]);
                for(int pass=0;pass<4;pass++)for(int j=2;j>=0;j--){
                    float best=angles[j],error=float.PositiveInfinity,limit=j==0?105:j==1?140:85;
                    float lo=Mathf.Max(0,angles[j]-12),hi=Mathf.Min(limit,angles[j]+12);
                    for(float a=lo;a<=hi;a+=1){Set(j,a);float e=Cost();if(e<error){error=e;best=a;}}Set(j,best);
                }
                for(int j=0;j<3;j++)chain[j].bone.localRotation=Quaternion.Slerp(original[j],chain[j].bone.localRotation,w);
            }
            if(hero.body.sharedMesh.name.Contains("(continuous hands)"))ApplyThumbOpposition(hero,left,w);
        }
        static void ApplyThumbOpposition(MatchHeroLook hero,bool left,float strength)
        {
            var rest=Rest(hero);string prefix=left ? "Left":"Right";
            if(!rest.TryGetValue(prefix+"ThumbProximal",out var p)||!rest.TryGetValue(prefix+"ThumbIntermediate",out var m)||!rest.TryGetValue(prefix+"ThumbDistal",out var d))return;
            var hand=hero.Bone(left ? HumanBodyBones.LeftHand:HumanBodyBones.RightHand);
            var index=Bone(hero,left,"Index","Proximal");var middle=Bone(hero,left,"Middle","Proximal");var little=Bone(hero,left,"Little","Proximal");if(!hand||!index||!middle||!little)return;
            var palm=hand.TransformVector(p.palmNormal).normalized;
            var indexMid=Bone(hero,left,"Index","Intermediate");var target=Vector3.Lerp(index.position,indexMid.position,.65f)+palm*.008f;
            var old=new[]{p.bone.localRotation,m.bone.localRotation,d.bone.localRotation};p.bone.localRotation=p.local;
            float distance=Vector3.Distance(target,p.bone.position),best=float.PositiveInfinity,ma=0,da=0;
            // First choose joint flexion that makes the real thumb tip's basal
            // radius match the knuckle target; then oppose the basal pivot in 3D.
            for(float a=0;a<=140;a+=10)for(float b=0;b<=110;b+=10){
                m.bone.localRotation=m.local*Quaternion.AngleAxis(a,m.hinge);d.bone.localRotation=d.local*Quaternion.AngleAxis(b,d.hinge);
                float radius=Vector3.Distance(d.bone.TransformPoint(d.tip),p.bone.position);float e=(radius-distance)*(radius-distance); // geometric contact only
                if(e<best){best=e;ma=a;da=b;}
            }
            m.bone.localRotation=m.local*Quaternion.AngleAxis(ma,m.hinge);d.bone.localRotation=d.local*Quaternion.AngleAxis(da,d.hinge);
            var from=d.bone.TransformPoint(d.tip)-p.bone.position;var to=target-p.bone.position;
            if(from.sqrMagnitude>1e-8f&&to.sqrMagnitude>1e-8f){
                var turn=Matrix4x4.Translate(p.bone.position)*Matrix4x4.Rotate(Quaternion.FromToRotation(from,to))*Matrix4x4.Translate(-p.bone.position);var local=p.bone.parent.worldToLocalMatrix*turn*p.bone.localToWorldMatrix;Vector3 x=local.GetColumn(0),y=local.GetColumn(1),z=local.GetColumn(2);var scale=new Vector3(x.magnitude,y.magnitude,z.magnitude);if(Vector3.Dot(Vector3.Cross(x,y),z)<0)scale.x=-scale.x;p.bone.localPosition=local.GetColumn(3);p.bone.localRotation=Quaternion.LookRotation(z/scale.z,y/scale.y);p.bone.localScale=scale;
            }
            p.bone.localRotation=Quaternion.Slerp(old[0],p.bone.localRotation,strength);m.bone.localRotation=Quaternion.Slerp(old[1],m.bone.localRotation,strength);d.bone.localRotation=Quaternion.Slerp(old[2],d.bone.localRotation,strength);
        }
        public static void AlignWrist(MatchHeroLook hero,bool left,float strength=1)
        {
            if(!hero||!hero.body)return;var hand=hero.Bone(left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);var arm=hero.Bone(left?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm);var middle=Bone(hero,left,"Middle","Proximal");if(!hand||!arm||!middle)return;
            int h=System.Array.IndexOf(hero.body.bones,hand),m=System.Array.IndexOf(hero.body.bones,middle);if(h<0||m<0)return;var bind=hero.body.sharedMesh.bindposes;var palmAxis=bind[h].MultiplyPoint3x4(bind[m].inverse.GetColumn(3)).normalized;
            var from=hand.TransformVector(palmAxis).normalized;var to=(hand.position-arm.position).normalized;var old=hand.localRotation;
            var turn=Matrix4x4.Translate(hand.position)*Matrix4x4.Rotate(Quaternion.FromToRotation(from,to))*Matrix4x4.Translate(-hand.position);var local=hand.parent.worldToLocalMatrix*turn*hand.localToWorldMatrix;
            Vector3 x=local.GetColumn(0),y=local.GetColumn(1),z=local.GetColumn(2);var scale=new Vector3(x.magnitude,y.magnitude,z.magnitude);if(Vector3.Dot(Vector3.Cross(x,y),z)<0)scale.x=-scale.x;var rotation=Quaternion.LookRotation(z/scale.z,y/scale.y);hand.localRotation=Quaternion.Slerp(old,rotation,Mathf.Clamp01(strength));
        }
        public static Vector3 PalmContactTarget(MatchHeroLook hero,bool left,string digit)
        {
            var hand=hero.Bone(left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);var records=Rest(hero);string prefix=left?"Left":"Right";
            if(!hand||!records.TryGetValue(prefix+digit+"Proximal",out var r))return Vector3.zero;
            if(digit=="Thumb")return Vector3.Lerp(Bone(hero,left,"Index","Proximal").position,Bone(hero,left,"Index","Intermediate").position,.65f)+hand.TransformVector(r.palmNormal).normalized*.008f;
            return hand.TransformPoint(r.palmTarget);
        }
        static void Turn(Transform bone,Vector3 direction,Vector3 palm,float degrees)
        {
            var axis=Vector3.Cross(direction.normalized,palm).normalized;
            if(axis.sqrMagnitude>.5f)bone.rotation=Quaternion.AngleAxis(degrees,axis)*bone.rotation;
        }
        /// One sampled pose in, one corrected pose out. Export uses this exact helper before
        /// skinning every base/clip sample, so the native locker gets the same grip.
        public static void ApplyGrip(MatchHeroLook hero)
        {
            if(!hero)return;
            if(hero.golfKit){ApplyGolfGrip(hero);return;}
            if(!hero.racketGrip)return;
            var hand=hero.Bone(HumanBodyBones.RightHand);if(!hand)return;
            var grip=hero.racketGrip;
            if(Vector3.Distance(hand.position,grip.position)>.145f)return; // released prop
            var axis=grip.up.normalized;var origin=grip.position;
            float radius=.017f*Mathf.Abs(grip.lossyScale.x);
            FitDigits(hero,false,origin,axis,radius,.006f,24,32);
        }
        /// Baked supporting fingers for the reference two-handed backhand.
        public static void ApplySupportGrip(MatchHeroLook hero)
        {
            if(!hero||hero.golfKit||!hero.racketGrip)return;
            var hand=hero.Bone(HumanBodyBones.LeftHand);var upper=hero.Bone(HumanBodyBones.RightUpperArm);var lower=hero.Bone(HumanBodyBones.RightLowerArm);
            if(!hand||!upper||!lower)return;float scale=Vector3.Distance(upper.position,lower.position)/.2383f;
            var goal=hero.racketGrip.position+hero.racketGrip.TransformDirection(hero.stringUpLocal)*(.105f*scale);
            if(Vector3.Distance(hand.TransformPoint(new Vector3(0,.075f*scale,0)),goal)>.035f)return;
            var digits=new System.Collections.Generic.List<Transform>();var original=new System.Collections.Generic.List<Quaternion>();
            foreach(string digit in new[]{"Index","Middle","Ring","Little"})foreach(string joint in new[]{"Proximal","Intermediate","Distal"}){
                var bone=Bone(hero,true,digit,joint);if(bone){digits.Add(bone);original.Add(bone.localRotation);}
            }
            FitDigits(hero,true,hero.racketGrip.position,hero.racketGrip.up.normalized,.017f*Mathf.Abs(hero.racketGrip.lossyScale.x),.006f,24,32);
            for(int i=0;i<digits.Count;i++)digits[i].localRotation=Quaternion.RotateTowards(original[i],digits[i].localRotation,10);
        }
        sealed class GolfGripData { public Transform grip;public SkinnedMeshRenderer[] clubs;public Vector3[] ends;public GolfWristResult[] wrists = new GolfWristResult[2];public Transform[] solvedBones;public Matrix4x4[] solvedPose; }
        sealed class GolfWristResult {
            public Vector3 shoulder, elbow, wrist, grip, shaft;
            public Quaternion upperRotation, lowerRotation, handRotation;
        }
        static readonly System.Collections.Generic.Dictionary<MatchHeroLook,GolfGripData> golfData=new();
        sealed class RestJoint { public Transform bone;public Quaternion local;public Vector3 hinge,tip,palmTarget,palmNormal; }
        static readonly System.Collections.Generic.Dictionary<MatchHeroLook,System.Collections.Generic.Dictionary<string,RestJoint>> restData=new();
        void OnDestroy(){if(look){golfData.Remove(look);restData.Remove(look);}}
        static System.Collections.Generic.Dictionary<string,RestJoint> Rest(MatchHeroLook hero,float palmHeight=.68f)
        {
            if(restData.TryGetValue(hero,out var found))return found;
            var result=new System.Collections.Generic.Dictionary<string,RestJoint>();
            hero.body.sharedMesh=HeroHandWeights.Prepare(hero.body,hero.female);
            var body=hero.body;var bind=body.sharedMesh.bindposes;var palette=body.bones;
            var matrix=new System.Collections.Generic.Dictionary<Transform,Matrix4x4>();
            for(int i=0;i<palette.Length;i++)matrix[palette[i]]=bind[i].inverse;
            foreach(bool left in new[]{false,true}){
                var hand=hero.Bone(left ? HumanBodyBones.LeftHand:HumanBodyBones.RightHand);
                var index=Bone(hero,left,"Index","Proximal");var little=Bone(hero,left,"Little","Proximal");var middle=Bone(hero,left,"Middle","Proximal");
                if(!hand||!index||!little||!middle||!matrix.ContainsKey(hand))continue;
                Vector3 P(Transform b)=>matrix[b].GetColumn(3);
                var toHand=matrix[hand].inverse;
                Vector3 H(Transform b)=>toHand.MultiplyPoint3x4(P(b));
                var palm=Vector3.Cross((H(index)-H(little)).normalized,H(middle).normalized).normalized; // actual single-joint evaluated proof: left flexion is the opposite old sign
                foreach(string digit in new[]{"Index","Middle","Ring","Little","Thumb"})foreach(string joint in new[]{"Proximal","Intermediate","Distal"}){
                    var b=Bone(hero,left,digit,joint);if(!b||!matrix.ContainsKey(b)||!matrix.ContainsKey(b.parent))continue;
                    var next=joint=="Proximal" ? Bone(hero,left,digit,"Intermediate"):joint=="Intermediate" ? Bone(hero,left,digit,"Distal"):null;
                    var fingerToHand=toHand*matrix[b];
                    var direction=next ? H(next)-H(b):fingerToHand.MultiplyVector(Vector3.up);
                    var axis=Vector3.Cross(direction.normalized,palm).normalized;
                    var record=new RestJoint{bone=b,palmNormal=palm,local=(matrix[b.parent].inverse*matrix[b]).rotation,hinge=fingerToHand.inverse.MultiplyVector(axis).normalized};
                    var proximal=Bone(hero,left,digit,"Proximal");
                    var proximalPoint=H(proximal);var proximalMid=Bone(hero,left,digit,"Intermediate");var flexionAxis=Vector3.Cross((H(proximalMid)-proximalPoint).normalized,palm).normalized;var palmAim=proximalPoint+Vector3.ProjectOnPlane(Vector3.up*(proximalPoint.y*(palmHeight-1)),flexionAxis);var palmPoints=new System.Collections.Generic.List<Vector3>();float bestPlane=float.PositiveInfinity;
                    int handPalette=System.Array.IndexOf(palette,hand);var bodyVertices=body.sharedMesh.vertices;var bodyWeights=body.sharedMesh.boneWeights;
                    for(int v=0;v<bodyWeights.Length;v++){var bw=bodyWeights[v];float amount=(bw.boneIndex0==handPalette?bw.weight0:0)+(bw.boneIndex1==handPalette?bw.weight1:0)+(bw.boneIndex2==handPalette?bw.weight2:0)+(bw.boneIndex3==handPalette?bw.weight3:0);if(amount<.8f)continue;var h=bind[handPalette].MultiplyPoint3x4(bodyVertices[v]);float distance=Vector3.ProjectOnPlane(h-palmAim,palm).sqrMagnitude;if(distance<bestPlane){bestPlane=distance;}palmPoints.Add(h);}
                    var surface=palmAim;float front=float.NegativeInfinity;foreach(var h in palmPoints)if(Vector3.ProjectOnPlane(h-palmAim,palm).sqrMagnitude<bestPlane+.000004f&&Vector3.Dot(h,palm)>front){surface=h;front=Vector3.Dot(h,palm);}
                    record.palmTarget=surface-flexionAxis*Vector3.Dot(surface-proximalPoint,flexionAxis)+palm*(hero.female?.0035f:.0040f);
                    if(joint=="Distal"){
                        int indexInPalette=System.Array.IndexOf(palette,b);var vertices=body.sharedMesh.vertices;var weights=body.sharedMesh.boneWeights;var tips=new System.Collections.Generic.List<Vector3>();float max=0;
                        for(int v=0;v<weights.Length;v++){var w=weights[v];float weight=(w.boneIndex0==indexInPalette?w.weight0:0)+(w.boneIndex1==indexInPalette?w.weight1:0)+(w.boneIndex2==indexInPalette?w.weight2:0)+(w.boneIndex3==indexInPalette?w.weight3:0);
                            if(weight<.4f)continue;var point=bind[indexInPalette].MultiplyPoint3x4(vertices[v]);tips.Add(point);max=Mathf.Max(max,point.magnitude);}
                        var sum=Vector3.zero;int count=0;foreach(var point in tips)if(point.magnitude>max*.8f){sum+=point;count++;}
                        record.tip=count>0 ? sum/count:Vector3.up*.018f;
                    }
                    result[b.name]=record;
                }
            }
            restData[hero]=result;return result;
        }
        static float GolfRollPower8(float value)
        {
            float squared = value * value; squared *= squared; return squared * squared;
        }
        // Pose-local Gauss-Newton step, smoothly bounded before solving the arm.
        // No angle search, previous-frame state, or final feasibility rejection.
        static void ApplyGolfWristRoll(MatchHeroLook hero, GolfGripData data, bool left, Vector3 axis, float scale)
        {
            var upper = hero.Bone(left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm);
            var lower = hero.Bone(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
            var hand = hero.Bone(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            var middle = Bone(hero, left, "Middle", "Proximal");
            if (!upper || !lower || !hand || !middle || scale < .0001f) return;
            var shoulder = upper.position; var handRotation = hand.rotation;
            var clubPosition = data.grip.position; var clubRotation = data.grip.rotation;
            // Model metres keep the bound independent of the locker/game scale.
            var e = (lower.position - shoulder) / scale;
            var h = (hand.position - shoulder) / scale;
            var c = (clubPosition - shoulder) / scale;
            var palm = (middle.position - hand.position).normalized;
            axis.Normalize();
            float a = e.magnitude, b = (h - e).magnitude, d = h.magnitude;
            if (a <= 0 || b <= 0 || d <= 0) return;
            var forward = h / d; float along = Vector3.Dot(e, forward);
            var pole = e - forward * along; float radius = pole.magnitude;
            float margin = Mathf.Min(a + b - d, d - Mathf.Abs(a - b));
            // At a degenerate arm the continuous safe roll tends to zero.
            if (margin <= 0 || radius <= 0) return;
            var normal = pole / radius; var forearm = (h - e) / b;
            float originalAngle = Vector3.Angle(forearm, palm);
            float activation = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(35, 50, originalAngle));
            var handDerivative = Vector3.Cross(axis, h - c);
            float distanceDerivative = Vector3.Dot(forward, handDerivative);
            var forwardDerivative = (handDerivative - forward * distanceDerivative) / d;
            float alongDerivative = .5f * (1 - (a * a - b * b) / (d * d)) * distanceDerivative;
            float radiusDerivative = -along * alongDerivative / Mathf.Max(radius, 1e-12f);
            var elbowDerivative = forward * alongDerivative + forwardDerivative * along + normal * radiusDerivative
                - forward * (radius * Vector3.Dot(normal, forwardDerivative));
            var forearmDerivative = (handDerivative - elbowDerivative) / b;
            var palmDerivative = Vector3.Cross(axis, palm);
            float gradient = Vector3.Dot(forearmDerivative, palm) + Vector3.Dot(forearm, palmDerivative);
            float rawRoll = gradient / ((forearmDerivative - palmDerivative).sqrMagnitude + .12f + 15.23f * elbowDerivative.sqrMagnitude);

            // Every wrist on this orbit stays within q0 of its source. Over that
            // ball, sensitivity bounds elbow displacement / wrist displacement.
            // See continuous-review/CONTINUITY.md for the geometric derivation.
            float q0 = .5f * margin, dMin = d - q0, dMax = d + q0;
            float lMin = (a * a - b * b + dMin * dMin) / (2 * dMin);
            float lMax = (a * a - b * b + dMax * dMax) / (2 * dMax);
            float rMin = Mathf.Sqrt(Mathf.Max(1e-24f, Mathf.Min(a * a - lMin * lMin, a * a - lMax * lMax)));
            float lPrime = Mathf.Max(Mathf.Abs(.5f * (1 - (a * a - b * b) / (dMin * dMin))),
                Mathf.Abs(.5f * (1 - (a * a - b * b) / (dMax * dMax))));
            float sensitivity = a * (1 / Mathf.Sqrt(d * d - q0 * q0) + lPrime / rMin);
            float radial = Vector3.Cross(axis, h - c).magnitude;
            const float limit = .04f;
            float rate0 = 4 / Mathf.PI, rate1 = radial / limit, rate2 = radial / q0, rate3 = radial * sensitivity / limit;
            float rate = Mathf.Max(Mathf.Max(rate0, rate1), Mathf.Max(rate2, rate3));
            float rollCap = 1 / (rate * Mathf.Pow(GolfRollPower8(rate0 / rate) + GolfRollPower8(rate1 / rate)
                + GolfRollPower8(rate2 / rate) + GolfRollPower8(rate3 / rate), .125f));
            float roll = rollCap * (float)System.Math.Tanh(rawRoll / rollCap) * activation;
            var turn = Quaternion.AngleAxis(roll * Mathf.Rad2Deg, axis);
            var targetHand = c + turn * (h - c);
            float targetDistance = targetHand.magnitude;
            var targetForward = targetHand / targetDistance;
            float targetAlong = (a * a - b * b + targetDistance * targetDistance) / (2 * targetDistance);
            // Transport the source elbow plane: projecting the old elbow into
            // each trial plane created a second discontinuity near straight arms.
            var cross = Vector3.Cross(forward, targetForward);
            var targetNormal = normal + Vector3.Cross(cross, normal)
                + Vector3.Cross(cross, Vector3.Cross(cross, normal)) / (1 + Vector3.Dot(forward, targetForward));
            var targetElbow = targetForward * targetAlong
                + targetNormal * Mathf.Sqrt(Mathf.Max(0, a * a - targetAlong * targetAlong));
            var bestElbow = shoulder + targetElbow * scale;
            var bestWrist = shoulder + targetHand * scale;
            if (System.Environment.GetEnvironmentVariable("VISUAL_GOLF_WRIST_AUDIT") == "1")
                Debug.Log($"[GolfWristRoll] {(hero.female ? "Female" : "Male")} {(left ? "Left" : "Right")} angle={originalAngle:F3} roll={roll * Mathf.Rad2Deg:F3} scale={scale:F6} wristDelta={(targetHand-h).magnitude * scale:F6} elbowDelta={(targetElbow-e).magnitude * scale:F6} axis={axis:F6}");
            upper.rotation = Quaternion.FromToRotation(lower.position - shoulder, bestElbow - shoulder) * upper.rotation;
            lower.rotation = Quaternion.FromToRotation(hand.position - lower.position, bestWrist - lower.position) * lower.rotation;
            hand.SetPositionAndRotation(bestWrist, turn * handRotation);
            // The sampled digit local transforms are untouched. Preserve the
            // club explicitly, including rigs that parent it beneath a hand.
            data.grip.SetPositionAndRotation(clubPosition, clubRotation);
        }
        // Swing the elbow on its two-bone reach circle toward the palm axis.
        // Hands, fingers and club stay pinned; segment lengths remain unchanged.
        // Smooth torque/caps avoid frame-to-frame angle-search switches.
        static void RelaxGolfElbow(MatchHeroLook hero, GolfGripData data, bool left, float scale)
        {
            var upper=hero.Bone(left?HumanBodyBones.LeftUpperArm:HumanBodyBones.RightUpperArm);
            var lower=hero.Bone(left?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm);
            var hand=hero.Bone(left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);
            var middle=Bone(hero,left,"Middle","Proximal");
            if(!upper||!lower||!hand||!middle||scale<.0001f)return;
            var shoulder=upper.position;var wrist=hand.position;var elbow=lower.position;
            var forward=(wrist-shoulder).normalized;var palm=(middle.position-wrist).normalized;
            var centre=shoulder+forward*Vector3.Dot(elbow-shoulder,forward);
            var radial=elbow-centre;float radius=radial.magnitude;
            if(radius<.00001f)return;
            var desired=Vector3.ProjectOnPlane(-palm,forward);
            float projection=desired.magnitude;if(projection<.00001f)return;
            desired/=projection;var normal=radial/radius;
            float activation=Mathf.SmoothStep(0,1,Mathf.InverseLerp(30,55,Vector3.Angle(wrist-elbow,palm)));
            float torque=Vector3.Dot(Vector3.Cross(normal,desired),forward)/(1.3f+Vector3.Dot(normal,desired));
            // <=45 degrees and <=80 mm of elbow travel in model metres.
            float angleRate=1/.785398f,travelRate=radius/(.08f*scale);
            float rate=Mathf.Max(angleRate,travelRate);
            float cap=1/(rate*Mathf.Pow(GolfRollPower8(angleRate/rate)+GolfRollPower8(travelRate/rate),.125f));
            float turn=cap*(float)System.Math.Tanh(2*torque/cap)*activation*projection;
            var target=centre+Quaternion.AngleAxis(turn*Mathf.Rad2Deg,forward)*radial;
            var handRotation=hand.rotation;var clubPosition=data.grip.position;var clubRotation=data.grip.rotation;
            upper.rotation=Quaternion.FromToRotation(elbow-shoulder,target-shoulder)*upper.rotation;
            lower.rotation=Quaternion.FromToRotation(hand.position-lower.position,wrist-lower.position)*lower.rotation;
            hand.SetPositionAndRotation(wrist,handRotation);
            data.grip.SetPositionAndRotation(clubPosition,clubRotation);
        }
        public static void ApplyGolfGrip(MatchHeroLook hero)
        {
            if(!hero||!hero.golfKit)return;
            if(!golfData.TryGetValue(hero,out var data)){
                data=new GolfGripData();foreach(var t in hero.GetComponentsInChildren<Transform>(true))if(t.name=="Club"){data.grip=t;break;}
                if(!data.grip)return;
                var all=hero.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                data.clubs=System.Array.FindAll(all,r=>r.name.StartsWith("CLUB_"));data.ends=new Vector3[data.clubs.Length];
                for(int k=0;k<data.clubs.Length;k++){
                    var r=data.clubs[k];int b=System.Array.FindIndex(r.bones,t=>t==data.grip);if(b<0)continue;
                    foreach(var v in r.sharedMesh.vertices){var point=r.sharedMesh.bindposes[b].MultiplyPoint3x4(v);if(point.sqrMagnitude>data.ends[k].sqrMagnitude)data.ends[k]=point;}
                }
                data.solvedBones = new[] {
                    hero.Bone(HumanBodyBones.LeftUpperArm), hero.Bone(HumanBodyBones.LeftLowerArm), hero.Bone(HumanBodyBones.LeftHand),
                    hero.Bone(HumanBodyBones.RightUpperArm), hero.Bone(HumanBodyBones.RightLowerArm), hero.Bone(HumanBodyBones.RightHand), data.grip };
                // The sampler can rewrite digit channels while the arm is unchanged.
                // Cache the complete grip output, not only its seven rigid anchors.
                var complete = new System.Collections.Generic.List<Transform>(data.solvedBones);
                foreach(bool left in new[]{false,true})
                    foreach(string digit in new[]{"Thumb","Index","Middle","Ring","Little"})
                        foreach(string joint in new[]{"Proximal","Intermediate","Distal"}) {
                            var bone=Bone(hero,left,digit,joint);if(bone)complete.Add(bone);
                        }
                data.solvedBones=complete.ToArray();
                golfData[hero]=data;
            }
            int active=System.Array.FindIndex(data.clubs,r=>r&&r.gameObject.activeInHierarchy);if(active<0)return;
            // Apply once to each sampled pose, including synchronous scrubbing/export.
            if (data.solvedPose != null) {
                bool same = true;
                for (int bone=0;bone<data.solvedBones.Length && same;bone++) {
                    if (!data.solvedBones[bone]) { same=false;break; }
                    var matrix=data.solvedBones[bone].localToWorldMatrix;
                    for(int k=0;k<16;k++) if(Mathf.Abs(matrix[k]-data.solvedPose[bone][k])>1e-6f) { same=false;break; }
                }
                // Covers repeated export/sample calls too, before any digit reset.
                if(same)return;
            }
            // The golf clip already closes all five digits around the handle.
            // Canonical rest curls and radial CCD discarded its fan and opened
            // the grip. Carry the authored digit transforms through the arm solve.
            float scale=Mathf.Abs(hero.transform.lossyScale.x);var end=data.ends[active];
            // The head's toe protrudes from the shaft. Fit to the shaft's dominant grip-
            // local axis, not a diagonal from the grip to the farthest head corner.
            Vector3 localAxis=Mathf.Abs(end.x)>Mathf.Abs(end.y)&&Mathf.Abs(end.x)>Mathf.Abs(end.z) ? Vector3.right*Mathf.Sign(end.x) : Mathf.Abs(end.y)>Mathf.Abs(end.z) ? Vector3.up*Mathf.Sign(end.y):Vector3.forward*Mathf.Sign(end.z);
            var along=data.grip.TransformVector(localAxis).normalized;
            // Roll the rigid hand/subtree around the real shaft before elbow relaxation.
            // Every authored pad keeps its shaft station and cylinder radius.
            ApplyGolfWristRoll(hero, data, false, along, scale);
            ApplyGolfWristRoll(hero, data, true, along, scale);
            RelaxGolfElbow(hero, data, false, scale);
            RelaxGolfElbow(hero, data, true, scale);
            data.solvedPose ??= new Matrix4x4[data.solvedBones.Length];
            for(int bone=0;bone<data.solvedBones.Length;bone++)if(data.solvedBones[bone])data.solvedPose[bone]=data.solvedBones[bone].localToWorldMatrix;
        }
        static void FitDigits(MatchHeroLook hero,bool left,Vector3 origin,Vector3 axis,float radius,float padding,float proximalLimit,float distalLimit)
        {
            foreach(string digit in new[]{"Index","Middle","Ring","Little"})
            {
                var p=Bone(hero,left,digit,"Proximal");var m=Bone(hero,left,digit,"Intermediate");var d=Bone(hero,left,digit,"Distal");
                if(!p||!m||!d)continue;
                float length=Mathf.Max(.009f,Vector3.Distance(m.position,d.position)*.75f);
                Vector3 Tip()=>d.position+d.up*length;
                var originalTip=Tip();var delta=originalTip-origin;float along=Vector3.Dot(delta,axis);
                var radial=delta-axis*along;if(radial.magnitude<radius+padding)continue;
                var target=origin+axis*along+radial.normalized*(radius+padding);
                var chain=new[]{p,m,d};var total=new float[3];
                for(int pass=0;pass<3;pass++)for(int j=2;j>=0;j--)
                {
                    var b=chain[j];var finger=j<2 ? chain[j+1].position-b.position:d.up*length;
                    var centre=origin+axis*Vector3.Dot(b.position-origin,axis);
                    var hinge=Vector3.Cross(finger.normalized,(centre-b.position).normalized).normalized;
                    if(hinge.sqrMagnitude<.5f)continue;
                    var from=Vector3.ProjectOnPlane(Tip()-b.position,hinge);var to=Vector3.ProjectOnPlane(target-b.position,hinge);
                    if(from.sqrMagnitude<1e-8f||to.sqrMagnitude<1e-8f)continue;
                    float request=Vector3.SignedAngle(from,to,hinge);
                    float next=Mathf.Clamp(total[j]+request,-15,j==0 ? proximalLimit:distalLimit);
                    b.rotation=Quaternion.AngleAxis(next-total[j],hinge)*b.rotation;total[j]=next;
                }
            }
        }
    }
}
