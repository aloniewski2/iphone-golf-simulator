#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GolfArcade.Tennis;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
namespace GolfArcade.EditorTools
{
    /// Bakes reference-led arm paths onto the measured Generic skeleton. Source
    /// torso rotations, duration and gameplay contact timestamps are retained.
    /// Supporting backhand digits receive a bounded grip adjustment.
    /// The backhand plants the source feet while easing the hips into its load.
    public static class TennisArmStrokeBuild
    {
        struct Point { public float t; public Vector3 p; public Point(float time,float x,float y,float z){t=time;p=new Vector3(x,y,z);} }
        static Vector3 Path(Point[] keys,float t)
        {
            int i=0;while(i<keys.Length-2&&t>keys[i+1].t)i++;
            float span=keys[i+1].t-keys[i].t,u=Mathf.Clamp01((t-keys[i].t)/span);
            Vector3 m0=i==0?Vector3.zero:(keys[i+1].p-keys[i-1].p)/(keys[i+1].t-keys[i-1].t);
            Vector3 m1=i+1==keys.Length-1?Vector3.zero:(keys[i+2].p-keys[i].p)/(keys[i+2].t-keys[i].t);
            return (2*u*u*u-3*u*u+1)*keys[i].p+(u*u*u-2*u*u+u)*span*m0+(-2*u*u*u+3*u*u)*keys[i+1].p+(u*u*u-u*u)*span*m1;
        }
        static Point[] FH=new[]{new Point(0,.067f,.916f,.200f),new Point(.15f,.25f,1.02f,-.04f),new Point(.32f,.02f,1.16f,-.28f),new Point(.45f,-.04f,1.20f,-.29f),new Point(.54f,.21f,1.01f,-.28f),new Point(.61f,.40f,.94f,.06f),new Point(2f/3f,.18f,.90f,.47f),new Point(.74f,-.04f,1.15f,.58f),new Point(.80f,-.20f,1.28f,.50f),new Point(.87f,-.27f,1.38f,.32f),new Point(.99f,-.24f,1.29f,.20f),new Point(1.11f,.005f,1.01f,.27f),new Point(1.2f,.067f,.916f,.200f)};
        // Traced from the supplied Alcaraz rear-view sequence (reference frames 0–45).
        // Hands stay ahead of the torso, extend through impact, then wrap to the right shoulder.
        static Point[] BH=new[]{new Point(0,.067f,.916f,.200f),new Point(.15f,-.40f,.95f,.16f),new Point(.32f,-.48f,.97f,.12f),new Point(.46f,-.44f,.90f,.18f),new Point(.54f,-.38f,.79f,.28f),new Point(.60f,-.19f,.84f,.48f),new Point(19f/30f,-.11f,.96f,.67f),new Point(.71f,.04f,1.09f,.60f),new Point(.82f,.23f,1.30f,.32f),new Point(.97f,.29f,1.36f,.18f),new Point(1.11f,.14f,1.08f,.31f),new Point(1.2f,.067f,.916f,.200f)};
        static Point[] FHShaft=new[]{new Point(0,0,.72f,.69f),new Point(.32f,.20f,.97f,-.12f),new Point(.45f,.27f,.94f,-.20f),new Point(.55f,.58f,-.76f,-.29f),new Point(2f/3f,.90f,.20f,.39f),new Point(.74f,.25f,.72f,.64f),new Point(.85f,-.70f,.46f,-.54f),new Point(.99f,-.80f,.42f,-.42f),new Point(1.2f,0,.72f,.69f)};
        static Point[] BHShaft=new[]{new Point(0,0,.72f,.69f),new Point(.22f,-.65f,.75f,.10f),new Point(.40f,-.70f,.65f,.15f),new Point(.54f,-.65f,-.70f,.05f),new Point(19f/30f,-.92f,.10f,.38f),new Point(.72f,-.50f,.82f,.25f),new Point(.84f,.18f,.85f,-.50f),new Point(.99f,-.90f,.30f,-.30f),new Point(1.2f,0,.72f,.69f)};
        static Point[] BackhandElbow=new[]{new Point(0,.23f,1.05f,.20f),new Point(.20f,-.42f,.94f,.34f),new Point(.40f,-.46f,.94f,.30f),new Point(19f/30f,.16f,.98f,.36f),new Point(.80f,.40f,1.12f,.32f),new Point(.97f,.47f,1.22f,.18f),new Point(1.2f,.23f,1.05f,.20f)};
        static Point[] BackhandSupportElbow=new[]{new Point(0,-.48f,.90f,.05f),new Point(.20f,-.53f,.96f,.38f),new Point(.40f,-.55f,.95f,.38f),new Point(19f/30f,-.36f,1.00f,.58f),new Point(.80f,0,1.12f,.40f),new Point(.98f,.10f,1.18f,.30f),new Point(1.2f,-.48f,.90f,.05f)};
        static Point[] ForehandElbow=new[]{new Point(0,.23f,1.05f,.06f),new Point(.32f,.32f,1.07f,-.08f),new Point(2f/3f,.34f,1.02f,.21f),new Point(.80f,.10f,1.15f,.44f),new Point(.94f,.08f,1.27f,.34f),new Point(1.2f,.23f,1.05f,.06f)};
        static Point[] Free=new[]{new Point(0,-.015f,.951f,.415f),new Point(.18f,.18f,1.01f,.42f),new Point(.36f,.40f,1.10f,.45f),new Point(.51f,.30f,1.06f,.42f),new Point(2f/3f,-.22f,.95f,.30f),new Point(.82f,-.27f,1.00f,.13f),new Point(.99f,-.19f,.96f,.12f),new Point(1.2f,-.015f,.951f,.415f)};
        class Arm
        {
            public Transform upper,lower,hand;public Quaternion restWrist,previousUpper,previousLower;public float l1,l2,maxWrist,maxReach,maxStep;public Vector3 elbow; Quaternion restUpper,restLower,restParent,restChest;Transform chest; public float wristLimit=80;public bool elbowBelow,referenceMode,continuousPole;public Vector3 previousAxis,previousPole; public float continuityWeight=2; public int poleSpan=30,twistSpan=20; public bool smoothWrist; public Quaternion previousWrist; public Quaternion RestUpperLocal=>Quaternion.Inverse(restParent)*restUpper;
            public Arm(MatchHeroLook h,bool right)
            {
                upper=h.Bone(right?HumanBodyBones.RightUpperArm:HumanBodyBones.LeftUpperArm);lower=h.Bone(right?HumanBodyBones.RightLowerArm:HumanBodyBones.LeftLowerArm);hand=h.Bone(right?HumanBodyBones.RightHand:HumanBodyBones.LeftHand);
                var bones=h.body.bones;var bind=h.body.sharedMesh.bindposes;Quaternion Rest(Transform b)=>bind[Array.IndexOf(bones,b)].inverse.rotation;
                chest=h.Bone(HumanBodyBones.Chest);restChest=h.body.transform.rotation*Rest(chest);
                restUpper=h.body.transform.rotation*Rest(upper);restLower=h.body.transform.rotation*Rest(lower);restParent=h.body.transform.rotation*Rest(upper.parent);
                restWrist=Quaternion.Inverse(Rest(lower))*Rest(hand);previousWrist=restWrist;l1=Vector3.Distance(upper.position,lower.position);l2=Vector3.Distance(lower.position,hand.position);previousUpper=upper.rotation;previousLower=lower.rotation;
            }
            float TorsoCost(Vector3 e,Vector3 w)
            {
                if(!referenceMode)return 0;var q=Quaternion.Inverse(chest.rotation*Quaternion.Inverse(restChest));float cost=0;
                for(int k=0;k<=4;k++){
                    var p=q*(Vector3.Lerp(e,w,k/4f)-chest.position)-new Vector3(0,.02f,0);
                    var v=new Vector3(p.x/.18f,p.y/.24f,p.z/.12f);cost+=Mathf.Pow(Mathf.Max(0,1-v.sqrMagnitude),2);
                }
                return cost*10000000000f;
            }
            public void Solve(Vector3 target,Quaternion handRotation,Vector3 pole,float blend,bool free=false)
            {
                var oldU=upper.rotation;var oldL=lower.rotation;var oldH=hand.rotation;
                Vector3 start=upper.position,delta=target-start;float original=delta.magnitude,len=Mathf.Clamp(original,.08f,(l1+l2)*.985f);maxReach=Mathf.Max(maxReach,original-len);Vector3 axis=delta.normalized;
                float along=(l1*l1-l2*l2+len*len)/(2*len),radius=Mathf.Sqrt(Mathf.Max(0,l1*l1-along*along));
                Vector3 basePole=Vector3.ProjectOnPlane(pole-start,axis).normalized;
                if(basePole.sqrMagnitude<.1f)basePole=Vector3.ProjectOnPlane(Vector3.down,axis).normalized;
                if(continuousPole&&previousAxis.sqrMagnitude>.5f){
                    var carried=Quaternion.FromToRotation(previousAxis,axis)*previousPole;
                    basePole=Vector3.RotateTowards(carried,basePole,8*Mathf.Deg2Rad,0).normalized;
                }
                if(continuousPole){previousAxis=axis;previousPole=basePole;}
                float best=float.PositiveInfinity;Quaternion bestU=oldU,bestL=oldL;Vector3 bestE=lower.position;
                for(int swivel=-poleSpan;swivel<=poleSpan;swivel+=10)
                {
                    Vector3 e=start+axis*along+Quaternion.AngleAxis(swivel,axis)*basePole*radius;Vector3 du=(e-start).normalized,dl=(start+axis*len-e).normalized,n=Vector3.Cross(du,dl).normalized;
                    if(n.sqrMagnitude<.1f || (elbowBelow&&e.y>start.y-.025f))continue;
                    var qu=Quaternion.LookRotation(Vector3.Cross(n,du),du);var ql=Quaternion.LookRotation(Vector3.Cross(n,dl),dl);
                    var upperSkin=qu*Quaternion.Inverse(restUpper);var parentSkin=upper.parent.rotation*Quaternion.Inverse(restParent);
                    float shoulderExcess=Mathf.Max(0,Quaternion.Angle(parentSkin,upperSkin)-(referenceMode?150:130));
                    for(int twist=-twistSpan;twist<=twistSpan;twist+=10)
                    {
                        var qlt=ql*Quaternion.AngleAxis(twist,Vector3.up);float wrist=free?0:Quaternion.Angle(Quaternion.Inverse(qlt)*handRotation,restWrist);
                        float heightPenalty=Mathf.Max(0,e.y-start.y+.035f)*(target.y>start.y?.08f:1);
                        float elbowExcess=Mathf.Max(0,Quaternion.Angle(upperSkin,qlt*Quaternion.Inverse(restLower))-(referenceMode?165:145));
                        float bend=Vector3.Angle(qlt*Vector3.up,handRotation*Vector3.up);float wristExcess=Mathf.Max(0,wrist-wristLimit);
                        float cost=TorsoCost(e,start+axis*len)+(shoulderExcess*shoulderExcess+elbowExcess*elbowExcess)*600+heightPenalty*heightPenalty*100000+(referenceMode?Mathf.Pow(Mathf.Max(0,bend-55),2)*1000+wristExcess*wristExcess*1000+bend*bend*.2f:wrist*wrist)+swivel*swivel*(referenceMode?.25f:4f)+twist*twist*(referenceMode?.1f:4f)+Mathf.Pow(Quaternion.Angle(qu,previousUpper),2)*continuityWeight+Mathf.Pow(Quaternion.Angle(qlt,previousLower),2)*continuityWeight*.4f;
                        if(cost<best){best=cost;bestU=qu;bestL=qlt;bestE=e;}
                    }
                }
                upper.rotation=bestU;lower.rotation=bestL;
                var relative=Quaternion.Inverse(lower.rotation)*handRotation;float wristAngle=Quaternion.Angle(restWrist,relative);
                if(free){relative=Quaternion.Slerp(relative,restWrist,blend);wristAngle=Quaternion.Angle(restWrist,relative);}
                var bounded=Quaternion.Slerp(restWrist,relative,Mathf.Min(1,wristLimit/Mathf.Max(.001f,wristAngle)));
                // The completed clip smooths wrists outward from the impact anchor.
                previousWrist=smoothWrist?Quaternion.RotateTowards(previousWrist,bounded,15):bounded;
                hand.rotation=lower.rotation*previousWrist;
                if(referenceMode){float bend=Vector3.Angle(hand.up,lower.up);if(bend>60)hand.rotation=Quaternion.Slerp(Quaternion.identity,Quaternion.FromToRotation(hand.up,lower.up),(bend-60)/bend)*hand.rotation;}
                maxWrist=Mathf.Max(maxWrist,Quaternion.Angle(Quaternion.Inverse(lower.rotation)*hand.rotation,restWrist));
                maxStep=Mathf.Max(maxStep,Quaternion.Angle(previousUpper,upper.rotation));previousUpper=upper.rotation;previousLower=lower.rotation;elbow=bestE;
            }
        }
        static void PlantLeg(Transform upper,Transform lower,Transform foot,Vector3 target,Quaternion rotation,Vector3 forward)
        {
            Vector3 s=upper.position,e=lower.position,w=foot.position;float a=Vector3.Distance(s,e),b=Vector3.Distance(e,w),distance=Mathf.Clamp(Vector3.Distance(s,target),.05f,a+b-.001f);
            Vector3 direction=(target-s).normalized;float x=(a*a-b*b+distance*distance)/(2*distance);float height=Mathf.Sqrt(Mathf.Max(0,a*a-x*x));
            Vector3 knee=s+direction*x+Vector3.ProjectOnPlane(forward,direction).normalized*height;
            upper.rotation=Quaternion.FromToRotation(e-s,knee-s)*upper.rotation;
            lower.rotation=Quaternion.FromToRotation(foot.position-lower.position,target-lower.position)*lower.rotation;foot.rotation=rotation;
        }
        static bool review;
        public static void BuildAndReview(){review=true;Run();}
        public static void Run()
        {
            Debug.Log("[TennisArmStrokeBuild] reference-shared-palm-v5");
            var report=new StringBuilder();string dir="Assets/Resources/Tennis/Performance";
            foreach(string sex in new[]{"Male","Female"})foreach(bool bh in new[]{false,true})
            {
                if(!bh&&Environment.GetEnvironmentVariable("TENNIS_BUILD_BACKHAND_ONLY")=="1")continue;
                var root=Object.Instantiate(Resources.Load<GameObject>("Tennis/Customization/Player"+sex));
                try
                {
                    var h=root.GetComponent<MatchHeroLook>();var driver=root.GetComponent<HeroTennisDriver>();string stroke=bh?"Backhand":"Forehand";
                    string path=$"Assets/Characters/MatchHeroes/{sex}/{sex}_{stroke}.fbx";var source=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__"));
                    h.SetSkin(h.skinTone);
                    var clone=Object.Instantiate(source);clone.name=sex+"_"+stroke+"ArmCorrected";clone.frameRate=60;
                    var right=new Arm(h,true);var left=new Arm(h,false);if(bh){right.poleSpan=180;right.twistSpan=80;right.wristLimit=150;right.referenceMode=true;right.continuousPole=true;right.continuityWeight=8;left.poleSpan=180;left.twistSpan=80;left.wristLimit=150;left.referenceMode=true;left.continuousPole=true;left.continuityWeight=8;}
                    Debug.Log(sex+" child directions "+right.lower.localPosition+" / "+right.hand.localPosition+" wristrest "+right.restWrist);var hips=h.Bone(HumanBodyBones.Hips);
                    var legs=new[]{h.Bone(HumanBodyBones.RightUpperLeg),h.Bone(HumanBodyBones.RightLowerLeg),h.Bone(HumanBodyBones.RightFoot),h.Bone(HumanBodyBones.LeftUpperLeg),h.Bone(HumanBodyBones.LeftLowerLeg),h.Bone(HumanBodyBones.LeftFoot)};
                    var supportDigits=bh?new[]{"LeftIndexProximal","LeftIndexIntermediate","LeftIndexDistal","LeftMiddleProximal","LeftMiddleIntermediate","LeftMiddleDistal","LeftRingProximal","LeftRingIntermediate","LeftRingDistal","LeftLittleProximal","LeftLittleIntermediate","LeftLittleDistal"}.Select(n=>h.Bone((HumanBodyBones)Enum.Parse(typeof(HumanBodyBones),n))).ToArray():Array.Empty<Transform>();
                    var joints=new[]{right.upper,right.lower,right.hand,left.upper,left.lower,left.hand}.Concat(legs).Concat(supportDigits).ToArray();
                    var hipCurves=new[]{new AnimationCurve(),new AnimationCurve(),new AnimationCurve()};
                    var diagnostics=new List<string>{"frame,rightWrist,leftWrist,rightElbowX,rightElbowY,rightElbowZ,rightWristX,rightWristY,rightWristZ"};
                    var curves=new AnimationCurve[joints.Length,4];for(int j=0;j<joints.Length;j++)for(int k=0;k<4;k++)curves[j,k]=new AnimationCurve();
                    float contact=driver.slots.First(s=>s.id==(bh?HeroTennisDriver.Clip.Backhand:HeroTennisDriver.Clip.Forehand)).contact;
                    source.SampleAnimation(root,0);Quaternion racketOffset=Quaternion.Inverse(right.hand.rotation)*h.racketGrip.rotation;
                    Vector3 racketPositionOffset=right.hand.InverseTransformPoint(h.racketGrip.position);Quaternion leftOffset=Quaternion.Inverse(h.racketGrip.rotation)*left.hand.rotation;
                    Vector3 initialRight=right.hand.position,initialLeft=left.hand.position;Quaternion initialRacket=h.racketGrip.rotation;
                    source.SampleAnimation(root,contact);Vector3 sweet=h.racketGrip.TransformPoint(h.stringCentreLocal);Quaternion contactRacket=h.racketGrip.rotation;Vector3 contactHand;
                    float scale=bh?(sex=="Female"?.95f:1):right.l1/.2383f;Quaternion previousRacket=initialRacket;
                    for(int f=0;f<=72;f++)
                    {
                        float t=f/60f;source.SampleAnimation(root,t);
                        if(bh){
                            float crouch=Mathf.SmoothStep(0,1,Mathf.Clamp01(1-Mathf.Abs(t-contact)/.20f));
                            var footP=new[]{legs[2].position,legs[5].position};var footQ=new[]{legs[2].rotation,legs[5].rotation};
                            hips.position+=root.transform.TransformVector(new Vector3(0,.04f*scale,0))*crouch;
                            for(int side=0;side<2;side++)PlantLeg(legs[side*3],legs[side*3+1],legs[side*3+2],footP[side],footQ[side],root.transform.forward);
                        }
                        for(int k=0;k<3;k++)hipCurves[k].AddKey(t,hips.localPosition[k]);
                        float blend=Mathf.SmoothStep(0,1,Mathf.Clamp01(t/.13f))*Mathf.SmoothStep(0,1,Mathf.Clamp01((1.2f-t)/.12f));
                        Vector3 shaft=Path(bh?BHShaft:FHShaft,t).normalized;
                        var carried=Quaternion.FromToRotation(previousRacket*h.stringUpLocal,shaft)*previousRacket;
                        Vector3 normal=Vector3.ProjectOnPlane(Vector3.forward,shaft).normalized;
                        var carriedNormal=carried*h.stringNormalLocal;
                        if(Vector3.Dot(normal,carriedNormal)<0)normal=-normal;
                        // The string face remains generally toward the net as the shaft sweeps.
                        if(normal.sqrMagnitude<.1f)normal=Vector3.ProjectOnPlane(Vector3.up,shaft).normalized;
                        Quaternion racket=Quaternion.LookRotation(normal,shaft)*Quaternion.Inverse(Quaternion.LookRotation(h.stringNormalLocal,h.stringUpLocal));
                        float faceWeight=bh?.65f*Mathf.SmoothStep(0,1,Mathf.Clamp01(1-Mathf.Abs(t-contact)/.12f)):Mathf.Lerp(.18f,.65f,Mathf.Clamp01(1-Mathf.Abs(t-contact)/.12f));
                        racket=Quaternion.Slerp(carried,racket,faceWeight);
                        if(!bh)racket=Quaternion.Slerp(racket,contactRacket,Mathf.SmoothStep(0,1,Mathf.Clamp01(1-Mathf.Abs(t-contact)/.10f)));
                        racket=Quaternion.Slerp(initialRacket,racket,blend);previousRacket=racket;Quaternion wantHand=racket*Quaternion.Inverse(racketOffset);
                        contactHand=sweet-racket*h.stringCentreLocal-wantHand*racketPositionOffset;
                        Vector3 target=Path(bh?BH:FH,t)*scale;
                        if(bh)target-=wantHand*racketPositionOffset; // reference tracks the grip/palm, not the wrist joint
                        // Correct the path locally around impact so the authored string centre stays exact.
                        float window=Mathf.SmoothStep(0,1,Mathf.Clamp01(1-Mathf.Abs(t-contact)/.085f));
                        Vector3 atContact=Path(bh?BH:FH,contact)*scale;
                        if(!bh)target+=window*(contactHand-atContact);
                        Vector3 pole=root.transform.TransformPoint(Path(bh?BackhandElbow:ForehandElbow,t)*scale);
                        if(f==0){right.previousUpper=right.upper.rotation;right.previousLower=right.lower.rotation;left.previousUpper=left.upper.rotation;left.previousLower=left.lower.rotation;
                            Quaternion InitialWrist(Arm arm){var rel=Quaternion.Inverse(arm.lower.rotation)*arm.hand.rotation;float a=Quaternion.Angle(arm.restWrist,rel);return Quaternion.Slerp(arm.restWrist,rel,Mathf.Min(1,80/Mathf.Max(.001f,a)));}
                            right.previousWrist=InitialWrist(right);left.previousWrist=InitialWrist(left);}
                        var rightTarget=Vector3.Lerp(right.hand.position,root.transform.TransformPoint(target),blend);
                        var rightRotation=Quaternion.Slerp(right.hand.rotation,root.transform.rotation*wantHand,blend);
                        if(bh){
                            // Reference coordinates mark the right palm on the handle. Project
                            // that shared grip into both arms' reach before solving either arm.
                            float gripScale=right.l1/.2383f;var palmOffset=new Vector3(0,.075f*gripScale,0);
                            var gripGoal=Vector3.Lerp(initialRight+initialRacket*Quaternion.Inverse(racketOffset)*racketPositionOffset,root.transform.TransformPoint(Path(BH,t)*scale),blend);
                            var leftRotation=Quaternion.Slerp(left.hand.rotation,root.transform.rotation*racket*leftOffset,blend);
                            var shaftWorld=root.transform.rotation*racket*h.stringUpLocal;
                            var centreR=right.upper.position+rightRotation*racketPositionOffset;
                            var centreL=left.upper.position+leftRotation*palmOffset-shaftWorld*(.105f*gripScale);
                            for(int pass=0;pass<12;pass++){
                                gripGoal=centreR+Vector3.ClampMagnitude(gripGoal-centreR,(right.l1+right.l2)*.975f);
                                if(blend>.95f)gripGoal=centreL+Vector3.ClampMagnitude(gripGoal-centreL,(left.l1+left.l2)*.975f);
                            }
                            rightTarget=gripGoal-rightRotation*racketPositionOffset;
                            right.elbowBelow=t<.62f;left.elbowBelow=t<.62f;
                            right.Solve(rightTarget,rightRotation,pole,1);
                            var actualShaft=h.racketGrip.TransformDirection(h.stringUpLocal);
                            leftRotation=Quaternion.Slerp(left.hand.rotation,h.racketGrip.rotation*leftOffset,blend);
                            var supportTarget=h.racketGrip.position+actualShaft*(.105f*gripScale)-leftRotation*palmOffset;
                            var supportPole=root.transform.TransformPoint(Path(BackhandSupportElbow,t)*scale);
                            var previousAxis=left.previousAxis;var previousPole=left.previousPole;
                            var previousUpper=left.previousUpper;var previousLower=left.previousLower;
                            var palmGoal=Vector3.Lerp(left.hand.TransformPoint(palmOffset),h.racketGrip.position+actualShaft*(.105f*gripScale),blend);
                            left.Solve(Vector3.Lerp(left.hand.position,supportTarget,blend),leftRotation,supportPole,1);
                            // Refit the wrist position to the bounded, anatomically valid hand
                            // orientation; the palm stays on the exact shared handle.
                            var fittedRotation=left.hand.rotation;
                            for(int pass=0;pass<6;pass++){
                                left.previousAxis=previousAxis;left.previousPole=previousPole;
                                left.previousUpper=previousUpper;left.previousLower=previousLower;
                                left.Solve(palmGoal-fittedRotation*palmOffset,fittedRotation,supportPole,1);
                                fittedRotation=left.hand.rotation;
                            }
                            HeroGripPolish.ApplySupportGrip(h);
                        }
                        else right.Solve(rightTarget,rightRotation,pole,1);
                        if(!bh&&window>0)
                        {
                            var expectedCentre=rightTarget+rightRotation*(racketPositionOffset+racketOffset*h.stringCentreLocal);
                            for(int pass=0;pass<20;pass++){
                                var miss=expectedCentre-h.racketGrip.TransformPoint(h.stringCentreLocal);
                                if(miss.magnitude<.0001f)break;
                                rightTarget+=miss*(.5f*window);
                                right.Solve(rightTarget,rightRotation,pole,1);
                            }
                        }
                        if(bh)
                        {
                            // The shared grip is solved after the dominant-hand curve is complete.
                        }
                        else
                        {
                            var wantedLeft=Path(Free,t)*scale;var leftRot=left.hand.rotation;
                            left.Solve(Vector3.Lerp(left.hand.position,root.transform.TransformPoint(wantedLeft),blend),leftRot,root.transform.TransformPoint(-.48f*scale,.87f*scale,.02f*scale),blend,true);
                        }
                        diagnostics.Add($"{f},{Quaternion.Angle(Quaternion.Inverse(right.lower.rotation)*right.hand.rotation,right.restWrist)},{Quaternion.Angle(Quaternion.Inverse(left.lower.rotation)*left.hand.rotation,left.restWrist)},{right.lower.position.x},{right.lower.position.y},{right.lower.position.z},{right.hand.position.x},{right.hand.position.y},{right.hand.position.z}");
                        for(int j=0;j<joints.Length;j++){var q=joints[j].localRotation;for(int k=0;k<4;k++)curves[j,k].AddKey(t,q[k]);}
                    }
                    // Keep the exact impact wrist pose. Propagate a bounded local rotation
                    // outward in time on both sides, so a half-turn target crossing cannot
                    // snap between opposite edges of the wrist envelope.
                    int impactFrame=Mathf.RoundToInt(contact*60);
                    foreach(int joint in bh?Array.Empty<int>():new[]{0,1,2,3,4,5})
                    {
                        var rotations=new Quaternion[73];
                        for(int f=0;f<=72;f++)rotations[f]=new Quaternion(curves[joint,0][f].value,curves[joint,1][f].value,curves[joint,2][f].value,curves[joint,3][f].value);
                        if(joint==0||joint==3)
                        {
                            var neutral=(joint==0?right:left).RestUpperLocal;var vectors=new Vector3[73];
                            for(int f=0;f<=72;f++){
                                var relative=Quaternion.Inverse(neutral)*rotations[f];relative.ToAngleAxis(out float angle,out Vector3 axis);
                                if(angle>180)angle-=360;vectors[f]=Vector3.ClampMagnitude(axis*angle,130);
                            }
                            // Interpolate within the shoulder's anatomical rotation envelope.
                            // A shortest quaternion arc can otherwise jump across its far edge.
                            for(int f=impactFrame-1;f>=0;f--)vectors[f]=Vector3.MoveTowards(vectors[f+1],vectors[f],22);
                            for(int f=impactFrame+1;f<=72;f++)vectors[f]=Vector3.MoveTowards(vectors[f-1],vectors[f],22);
                            for(int f=0;f<=72;f++)rotations[f]=neutral*(vectors[f].sqrMagnitude<.00001f?Quaternion.identity:Quaternion.AngleAxis(vectors[f].magnitude,vectors[f].normalized));
                        }
                        else
                        {
                            for(int f=impactFrame-1;f>=0;f--)rotations[f]=Quaternion.RotateTowards(rotations[f+1],rotations[f],15);
                            for(int f=impactFrame+1;f<=72;f++)rotations[f]=Quaternion.RotateTowards(rotations[f-1],rotations[f],15);
                        }
                        for(int k=0;k<4;k++){curves[joint,k]=new AnimationCurve();for(int f=0;f<=72;f++)curves[joint,k].AddKey(f/60f,rotations[f][k]);}
                    }
                    for(int j=0;j<joints.Length;j++)for(int k=0;k<4;k++)
                    {
                        var binding=EditorCurveBinding.FloatCurve(AnimationUtility.CalculateTransformPath(joints[j],root.transform),typeof(Transform),"m_LocalRotation."+"xyzw"[k]);
                        for(int f=0;f<curves[j,k].length;f++){AnimationUtility.SetKeyLeftTangentMode(curves[j,k],f,AnimationUtility.TangentMode.Linear);AnimationUtility.SetKeyRightTangentMode(curves[j,k],f,AnimationUtility.TangentMode.Linear);}
                        AnimationUtility.SetEditorCurve(clone,binding,curves[j,k]);
                    }
                    for(int k=0;k<3;k++)AnimationUtility.SetEditorCurve(clone,EditorCurveBinding.FloatCurve(AnimationUtility.CalculateTransformPath(hips,root.transform),typeof(Transform),"m_LocalPosition."+"xyz"[k]),hipCurves[k]);
                    clone.EnsureQuaternionContinuity();clone.SampleAnimation(root,contact);float error=Vector3.Distance(sweet,h.racketGrip.TransformPoint(h.stringCentreLocal));
                    report.AppendLine($"{clone.name}: old source contact delta={error:F6}m; wrist R={right.maxWrist:F1} L={left.maxWrist:F1}deg; reach R={right.maxReach:F4} L={left.maxReach:F4}m; max raw upper step before filtering={right.maxStep:F1}deg");
                    File.WriteAllLines(System.IO.Path.GetFullPath("../"+clone.name+"-poses.csv"),diagnostics);
                    string targetPath=dir+"/"+clone.name+".anim";var current=AssetDatabase.LoadAssetAtPath<AnimationClip>(targetPath);if(current){EditorUtility.CopySerialized(clone,current);Object.DestroyImmediate(clone);}else AssetDatabase.CreateAsset(clone,targetPath);
                }
                finally{Object.DestroyImmediate(root);}
            }
            AssetDatabase.SaveAssets();File.WriteAllText(System.IO.Path.GetFullPath("../arm-stroke-build.txt"),report.ToString());Debug.Log(report);if(review)StrokeArmReview.Run();else EditorApplication.Exit(0);
        }
    }
}
#endif
