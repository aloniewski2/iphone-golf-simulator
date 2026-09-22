using System;
using System.Collections.Generic;
using GolfArcade.Game;
using UnityEngine;

namespace GolfArcade.Tennis
{
    /// Ambient visitors, separate from player input and rally simulation. Metre-space paths
    /// stay outside the 20 x 39 metre runoff; timeScale naturally pauses their walks.
    public sealed class TennisResortCrowd : MonoBehaviour
    {
        public const int WalkerCount=16;
        readonly List<Visitor> visitors=new();
        readonly List<Material> ownedMaterials=new();
        sealed class Visitor
        {
            public Transform actor, hipL, hipR, kneeL, kneeR, handL, handR;
            public Quaternion hl,hr,kl,kr;
            public Vector3 a,b; public float phase,speed,held;
        }
        /// The crowd reacts to the rally instead of looping one walk whatever happens:
        /// spectators stop, turn to the court and cheer, harder for longer rallies.
        float cheer, cheerStrength, hush;
        public void Cheer(float strength) { cheer=1; cheerStrength=Mathf.Clamp01(strength); }
        /// Match point: the promenade slows and quietens.
        public void Hush(bool on) => hush=on?1:0;
        void Start()
        {
            Color[] skins={new(.91f,.68f,.48f),new(.65f,.40f,.24f),new(.33f,.18f,.10f),new(.79f,.52f,.34f),new(.48f,.29f,.18f),new(.96f,.78f,.61f)};
            for(int i=0;i<WalkerCount;i++)
            {
                var prefab=Resources.Load<GameObject>("StandardCharacters/standard_"+(i%2==0?"male":"female")+"_tennis");
                if(!prefab) throw new InvalidOperationException("Permanent crowd character missing");
                var obj=Instantiate(prefab,transform); obj.name="Resort walker "+(i+1);
                var animator=obj.GetComponent<Animator>(); if(animator) animator.enabled=false;
                TennisActor.PrepareMaterials(obj,skins[i%skins.Length]);
                foreach(var r in obj.GetComponentsInChildren<Renderer>())
                {
                    if(r.name.IndexOf("racket",StringComparison.OrdinalIgnoreCase)>=0 || r.name.IndexOf("string",StringComparison.OrdinalIgnoreCase)>=0) r.enabled=false;
                    foreach(var m in r.sharedMaterials) if(m && !ownedMaterials.Contains(m)) ownedMaterials.Add(m);
                    r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                    // Background characters: two-bone skinning, and none at all while off
                    // screen -- most of the promenade is behind the camera most of the time.
                    if(r is SkinnedMeshRenderer skinned) { skinned.updateWhenOffscreen=false; skinned.quality=SkinQuality.Bone2; }
                }
                foreach(var m in obj.GetComponentsInChildren<Renderer>())
                    if(m.name.IndexOf("shirt",StringComparison.OrdinalIgnoreCase)>=0 || m.name.IndexOf("skirt",StringComparison.OrdinalIgnoreCase)>=0)
                        foreach(var material in m.sharedMaterials) material.color=Color.HSVToRGB((i*.137f)%1,.62f,.85f);
                var arms=obj.AddComponent<StandardCharacterArms>(); arms.SetFloatingHandsPreview(true); arms.ManualEvaluation=true;
                var bones=obj.GetComponentsInChildren<Transform>();
                Transform Bone(string n)=>Array.Find(bones,t=>t.name==n);
                var v=new Visitor {actor=obj.transform,hipL=Bone("UpperLeg.L"),hipR=Bone("UpperLeg.R"),kneeL=Bone("LowerLeg.L"),kneeR=Bone("LowerLeg.R"),handL=Bone("Hand.L"),handR=Bone("Hand.R"),phase=i*.73f,speed=.65f+(i%4)*.09f};
                if(!v.hipL || !v.hipR || !v.kneeL || !v.kneeR) throw new InvalidOperationException("Crowd walking bones missing");
                v.hl=v.hipL.localRotation;v.hr=v.hipR.localRotation;v.kl=v.kneeL.localRotation;v.kr=v.kneeR.localRotation;
                // Promenades behind both baselines: no spectators through stands or furniture.
                float z=(i<8?-21.5f:21.5f)+(i%3-1)*.55f;
                v.a=new Vector3(-9,.06f,z);v.b=new Vector3(9,.06f,z);
                visitors.Add(v);
            }
            Animate(0);
        }
        float walkClock;
        void Update()
        {
            cheer=Mathf.MoveTowards(cheer,0,Time.deltaTime/2.6f);
            // Walking time only advances while nobody is reacting, so walkers resume from
            // where they stopped instead of teleporting along their path.
            float pace=(1-Mathf.Clamp01(cheer*3))*(1-.6f*hush);
            walkClock+=Time.deltaTime*pace;
            Animate(walkClock);
        }
        void Animate(float time)
        {
            foreach(var v in visitors)
            {
                float distance=Vector3.Distance(v.a,v.b),cycle=(time*v.speed/distance+v.phase)%2;
                bool forward=cycle<1;float t=forward?cycle:2-cycle;
                v.actor.position=Vector3.Lerp(v.a,v.b,t);
                float react=Mathf.Clamp01(cheer*3);
                Vector3 court=new Vector3(0,0,0)-v.actor.position; court.y=0;
                Quaternion facing=Quaternion.LookRotation(react>.01f?court:(forward?v.b-v.a:v.a-v.b));
                v.actor.rotation=Quaternion.RotateTowards(v.actor.rotation,facing,Time.deltaTime*(react>.01f?260:180));
                if(react>.01f)
                {
                    // Arms up and a hop, each spectator slightly out of phase with the next.
                    float bounce=Mathf.Abs(Mathf.Sin((Time.timeSinceLevelLoad*7.5f+v.phase*3)))*.14f*cheerStrength*react;
                    v.actor.position+=Vector3.up*bounce;
                    v.hipL.localRotation=v.hl; v.hipR.localRotation=v.hr; v.kneeL.localRotation=v.kl; v.kneeR.localRotation=v.kr;
                    float wave=Mathf.Sin(Time.timeSinceLevelLoad*9+v.phase)*.12f;
                    float lift=Mathf.Lerp(.9f,1.95f,cheerStrength*react);
                    if(v.handL) v.handL.position=v.actor.TransformPoint(new Vector3(-.3f,lift+wave,.1f));
                    if(v.handR) v.handR.position=v.actor.TransformPoint(new Vector3(.3f,lift-wave,.1f));
                    continue;
                }
                float gait=time*v.speed*7+v.phase;
                float step=Mathf.Sin(gait);
                // Rotate in actor-space around the hip, preserving the authored bone basis.
                v.hipL.localRotation=v.hl; v.hipR.localRotation=v.hr;
                v.hipL.rotation=Quaternion.AngleAxis(step*23,v.actor.right)*v.hipL.rotation;
                v.hipR.rotation=Quaternion.AngleAxis(-step*23,v.actor.right)*v.hipR.rotation;
                v.kneeL.localRotation=v.kl;v.kneeR.localRotation=v.kr;
                v.kneeL.rotation=Quaternion.AngleAxis(Mathf.Max(0,-step)*30,v.actor.right)*v.kneeL.rotation;
                v.kneeR.rotation=Quaternion.AngleAxis(Mathf.Max(0,step)*30,v.actor.right)*v.kneeR.rotation;
                if(v.handL) v.handL.position=v.actor.TransformPoint(new Vector3(-.29f,.82f,-step*.12f));
                if(v.handR) v.handR.position=v.actor.TransformPoint(new Vector3(.29f,.82f,step*.12f));
            }
        }
        void OnDestroy(){foreach(var m in ownedMaterials) if(m) Destroy(m);}
    }
}
