using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace GolfArcade.Tennis
{
    /// Six fitted background adults, with continuous hero-derived lower skin.
    /// Eleven cosmetic joints share one opaque near/far mesh per adult.
    /// Metre-space paths remain outside the live court and runoff corridor.
    public sealed class TennisResortCrowd : MonoBehaviour
    {
        public const int WalkerCount=6;
        public int VisitorCount=>visitors.Count;
        public int LegacySkinnedCount=>0;
        readonly List<Visitor> visitors=new();
        public int DetailOverride {get;set;}=-1;
        public bool FreezePerformance {get;set;}
        public int NearVisitorCount {get;private set;}
        public int FarVisitorCount {get;private set;}
        Material material;
        sealed class Leg
        {
            public Transform thigh,shin,foot;
            public Quaternion thighRest,shinRest,footRest,levelRest;
            public float upper,lower,previousPhase=-1;
            public Vector3 planted,swingStart,footLocalRest;
            public bool locked;
        }
        sealed class Visitor
        {
            public Transform actor,body,head,left,right;
            public Quaternion headRest,leftRest,rightRest;
            public Vector3 bodyRest,a,b;
            public float phase,speed,held;
            public Leg l,r;public Renderer[] near,far;
        }
        float cheer,cheerStrength,hush,walkClock;
        public void Cheer(float strength){cheer=1;cheerStrength=Mathf.Clamp01(strength);}
        public void Hush(bool on)=>hush=on?1:0;
        static Transform Part(Transform root,string prefix)
        {
            foreach(var t in root.GetComponentsInChildren<Transform>(true))
                if(t.name.StartsWith(prefix,StringComparison.Ordinal))return t;
            return null;
        }
        static Leg MakeLeg(Transform root,string side,Transform actor)
        {
            var l=new Leg{thigh=Part(root,"THIGH_"+side),shin=Part(root,"SHIN_"+side),foot=Part(root,"FOOT_"+side)};
            if(!l.thigh||!l.shin||!l.foot)throw new InvalidOperationException("Promenade visitor lacks the authored hip/knee/ankle chain: "+side);
            l.thighRest=l.thigh.localRotation;l.shinRest=l.shin.localRotation;l.footRest=l.foot.localRotation;l.levelRest=Quaternion.Inverse(actor.rotation)*l.foot.rotation;l.footLocalRest=actor.InverseTransformPoint(l.foot.position);
            l.upper=Vector3.Distance(l.thigh.position,l.shin.position);l.lower=Vector3.Distance(l.shin.position,l.foot.position);return l;
        }
        void Start()
        {
            var prefab=Resources.Load<GameObject>("Tennis/Premium/TennisPromenadeHero3");
            var shader=Resources.Load<Shader>("Tennis/Shaders/TennisSpectator");
            if(!prefab||!shader)throw new InvalidOperationException("Authored promenade visitors / spectator shader missing");
            material=new Material(shader){name="Promenade sporting visitor vertex palette",enableInstancing=true};
            if(material.HasProperty("_SurfaceRoles"))material.SetFloat("_SurfaceRoles",1);
            var prototype=Part(prefab.transform,"FAN_0");var skin=prototype.GetComponentInChildren<SkinnedMeshRenderer>(true);var mesh=skin?skin.sharedMesh:Part(prototype,"HEAD").GetComponent<MeshFilter>().sharedMesh;
            var expected=new Color(.76f,.44f,.27f);var gamma=expected.gamma;float dl=10,dg=10;
            foreach(var c in mesh.colors){dl=Mathf.Min(dl,(new Vector3(c.r,c.g,c.b)-new Vector3(expected.r,expected.g,expected.b)).sqrMagnitude);dg=Mathf.Min(dg,(new Vector3(c.r,c.g,c.b)-new Vector3(gamma.r,gamma.g,gamma.b)).sqrMagnitude);}
            material.SetFloat("_ColorsAreSRGB",dg<dl?1:0);
            for(int i=0;i<WalkerCount;i++)
            {
                var actor=new GameObject("Premium promenade visitor "+i).transform;actor.SetParent(transform,true);
                var model=Instantiate(prefab,actor);model.name="Authored fitted walking adult";Transform selected=null;
                foreach(var t in model.GetComponentsInChildren<Transform>(true))if(t.name.StartsWith("FAN_",StringComparison.Ordinal))
                {bool use=t.name=="FAN_"+i;t.gameObject.SetActive(use);if(use)selected=t;}
                if(!selected)throw new InvalidOperationException("Promenade variant "+i+" missing");selected.localPosition=Vector3.zero;
                foreach(var a in model.GetComponentsInChildren<Animator>(true))Destroy(a);
                foreach(var r in selected.GetComponentsInChildren<Renderer>())
                {r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=true;}
                var v=new Visitor{actor=actor,body=Part(selected,"BODY"),head=Part(selected,"HEAD"),left=Part(selected,"ARM_L"),right=Part(selected,"ARM_R"),phase=Mathf.Repeat((i+1)*1.61803399f,1)*2.4f,speed=.55f+(i%4)*.10f,held=2.1f+(i%3)*.7f,l=MakeLeg(selected,"L",actor),r=MakeLeg(selected,"R",actor)};
                v.headRest=Quaternion.Inverse(actor.rotation)*v.head.rotation;v.leftRest=Quaternion.Inverse(actor.rotation)*v.left.rotation;v.rightRest=Quaternion.Inverse(actor.rotation)*v.right.rotation;v.bodyRest=v.body.localPosition;
                float offset=(i%3-1)*.38f;
                switch(i%4)
                {
                    case 0:v.a=new Vector3(-8,.06f,-24.2f+offset);v.b=new Vector3(7,.06f,-24.2f+offset);break;
                    case 1:v.a=new Vector3(-11,.06f,25.4f+offset);v.b=new Vector3(8,.06f,25.4f+offset);break;
                    case 2:v.a=new Vector3(22.3f+offset,.06f,-13);v.b=new Vector3(22.3f+offset,.06f,12);break;
                    default:v.a=new Vector3(-20.2f+offset,.06f,-15);v.b=new Vector3(-20.2f+offset,.06f,15);break;
                }
                var near=new List<Renderer>();var far=new List<Renderer>();
                foreach(var renderer in selected.GetComponentsInChildren<Renderer>(true))
                {renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=true;if(renderer is SkinnedMeshRenderer bodySkin)bodySkin.updateWhenOffscreen=true;if(renderer.name.Contains("_FAR"))far.Add(renderer);else near.Add(renderer);}
                v.near=near.ToArray();v.far=far.ToArray();visitors.Add(v);
            }
            Animate(0,true);Debug.Log("[Crowd] Six fitted promenade visitors; eleven cosmetic joints and one renderer per active detail, zero legacy full skins.");
        }
        void Update()
        {
            if(FreezePerformance)return;
            cheer=Mathf.MoveTowards(cheer,0,Time.deltaTime/2.6f);
            walkClock+=Time.deltaTime*(1-Mathf.Clamp01(cheer*3))*(1-.6f*hush);Animate(walkClock,false);
        }
        void Animate(float time,bool initial)
        {
            foreach(var v in visitors)
            {
                float distance=Vector3.Distance(v.a,v.b),dwell=v.held*v.speed/distance;
                float cycle=Mathf.Repeat(time*v.speed/distance+v.phase,2+dwell*2);
                bool forward=cycle<1+dwell,observing=cycle>=1&&cycle<1+dwell||cycle>=2+dwell;
                float t=forward?Mathf.Min(1,cycle):Mathf.Max(0,2+dwell-cycle);
                float walkWeight=Mathf.SmoothStep(0,1,Mathf.Min(t,1-t)/.055f)*(observing?0:1)*(1-Mathf.Clamp01(cheer*3));
                float e=.04f,eased=t<e?t*t/(2*e)/(1-e):t>1-e?1-(1-t)*(1-t)/(2*e)/(1-e):(t-e*.5f)/(1-e);
                var next=Vector3.Lerp(v.a,v.b,eased);var direction=forward?v.b-v.a:v.a-v.b;
                var court=-next;court.y=0;float react=Mathf.Clamp01(cheer*3);
                var facing=Quaternion.LookRotation(react>.01f||observing?court:direction);
                if(initial)v.actor.rotation=facing;else v.actor.rotation=Quaternion.RotateTowards(v.actor.rotation,facing,Time.deltaTime*(observing||react>.01f?115:180));
                v.actor.position=next-Vector3.up*(.04f*walkWeight);
                // A slight walking pelvis settle creates knee clearance; planted soles
                // are solved separately and do not inherit a looping vertical bounce.
                v.body.localPosition=v.bodyRest;
                float gait=time*v.speed/.86f+v.phase;
                PoseLeg(v,v.l,-1,Mathf.Repeat(gait,1),walkWeight,initial);
                PoseLeg(v,v.r,1,Mathf.Repeat(gait+.5f,1),walkWeight,initial);
                float arm=Mathf.Sin(gait*Mathf.PI*2)*walkWeight*10;
                v.left.rotation=Quaternion.AngleAxis(arm-react*(104+cheerStrength*24),v.actor.right)*(v.actor.rotation*v.leftRest);
                v.right.rotation=Quaternion.AngleAxis(-arm-react*(112+cheerStrength*21),v.actor.right)*(v.actor.rotation*v.rightRest);
                float glance=observing?Mathf.Clamp(Vector3.SignedAngle(v.actor.forward,court,Vector3.up),-22,22):Mathf.Sin(Time.time*.42f+v.phase)*3;
                v.head.rotation=Quaternion.AngleAxis(glance,Vector3.up)*(v.actor.rotation*v.headRest);
            }
        }
        static void PoseLeg(Visitor v,Leg l,int side,float phase,float weight,bool initial)
        {
            l.thigh.localRotation=l.thighRest;l.shin.localRotation=l.shinRest;l.foot.localRotation=l.footRest;
            if(weight<.001f){l.locked=false;l.previousPhase=-1;return;}
            var neutral=l.foot.position;var forward=v.actor.forward;var lateral=v.actor.right*l.footLocalRest.x;
            const float stance=.62f;bool planted=phase<stance;
            if(initial||l.previousPhase<0){l.planted=neutral+Vector3.up*(.04f*weight);l.swingStart=l.planted;l.locked=planted;}
            if(planted&&!l.locked){l.planted=v.actor.position+lateral+forward*(.22f+l.footLocalRest.z);l.planted.y=v.a.y+l.footLocalRest.y;l.locked=true;}
            Vector3 target;
            if(planted)target=l.planted;
            else
            {
                if(l.locked){l.swingStart=l.planted;l.locked=false;}
                float q=(phase-stance)/(1-stance);float blend=q*q*(3-2*q);
                var destination=v.actor.position+lateral+forward*(.22f+l.footLocalRest.z);destination.y=v.a.y+l.footLocalRest.y;
                target=Vector3.Lerp(l.swingStart,destination,blend)+Vector3.up*(Mathf.Sin(q*Mathf.PI)*.075f);
            }
            target=Vector3.Lerp(neutral,target,weight);l.previousPhase=phase;
            // Bounded two-link cosmetic IK. No colliders, contacts or player rig input.
            Vector3 hip=l.thigh.position,to=target-hip;float length=Mathf.Clamp(to.magnitude,.05f,l.upper+l.lower-.003f);var axis=to.normalized;
            var pole=Vector3.ProjectOnPlane(forward,axis).normalized;if(pole.sqrMagnitude<.01f)pole=v.actor.right;
            float along=(l.upper*l.upper-l.lower*l.lower+length*length)/(2*length);
            var knee=hip+axis*along+pole*Mathf.Sqrt(Mathf.Max(0,l.upper*l.upper-along*along));
            l.thigh.rotation=Quaternion.FromToRotation(l.shin.position-hip,knee-hip)*l.thigh.rotation;
            l.shin.rotation=Quaternion.FromToRotation(l.foot.position-l.shin.position,hip+axis*length-l.shin.position)*l.shin.rotation;
            // Keep the sole level in stance, with a small toe lift during swing.
            float toe=planted?0:Mathf.Sin((phase-stance)/(1-stance)*Mathf.PI)*9;
            l.foot.rotation=Quaternion.AngleAxis(-toe,v.actor.right)*(v.actor.rotation*l.levelRest);
        }
        void LateUpdate()
        {
            var game=GetComponent<TennisGame>();var camera=game&&game.GameplayCamera?game.GameplayCamera:Camera.main;
            float tangent=camera?Mathf.Tan(camera.fieldOfView*.5f*Mathf.Deg2Rad):.53f;NearVisitorCount=0;FarVisitorCount=0;
            foreach(var v in visitors)
            {
                float distance=camera?Vector3.Distance(camera.transform.position,v.actor.position+Vector3.up*.85f):10;
                float height=camera&&camera.orthographic?1.75f/(camera.orthographicSize*2):1.75f/(2*Mathf.Max(.1f,distance)*tangent);
                bool far=DetailOverride>=0?DetailOverride==1:height<.25f;if(far)FarVisitorCount++;else NearVisitorCount++;
                foreach(var renderer in v.near)renderer.enabled=!far;
                foreach(var renderer in v.far)renderer.enabled=far;
            }
        }
        void OnDestroy(){if(material)Destroy(material);}
    }
}
