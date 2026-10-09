using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace GolfArcade.Game
{
    /// Standing authored background adults. Four opaque rigid art pieces keep
    /// the gallery inexpensive and independent of the hero's detailed rig.
    public sealed class GolfGalleryPresentation : MonoBehaviour
    {
        static GameObject source;
        static Material palette;
        Transform head,left,right;
        Quaternion headRest,leftRest,rightRest;
        bool restReady;
        string action="Idle";
        float phase,actionAt,gesture,leftAngle,rightAngle;
        public Transform Head=>head;
        public string Action=>action;
        public int RigidPieceCount { get; private set; }
        public int MeshTriangles { get; private set; }
        public int DistantTriangles { get; private set; }

        static Transform FindPart(Transform root,string prefix)
        {
            foreach(var t in root.GetComponentsInChildren<Transform>(true))
                if(t.name.StartsWith(prefix,StringComparison.Ordinal))return t;
            return null;
        }
        static Material Palette(GameObject prefab)
        {
            if(palette)return palette;
            var shader=Resources.Load<Shader>("Tennis/Shaders/TennisSpectator");
            if(!shader)throw new InvalidOperationException("Gallery requires the shared opaque spectator shader.");
            palette=new Material(shader){name="Premium golf gallery vertex palette",enableInstancing=true};
            if(palette.HasProperty("_SurfaceRoles"))palette.SetFloat("_SurfaceRoles",1);
            // Unity's FBX colour import convention varies with importer version.
            // The source0 skin value is known, so decode only when actually encoded.
            var prototype=FindPart(prefab.transform,"FAN_0");var hp=prototype?FindPart(prototype,"HEAD"):null;
            var mf=hp?hp.GetComponent<MeshFilter>():null;
            if(mf&&mf.sharedMesh){
                var expected=new Color(.76f,.44f,.27f);var gamma=expected.gamma;
                float linearDistance=10,gammaDistance=10;
                foreach(var c in mf.sharedMesh.colors){
                    float Distance(Color x)=>Mathf.Pow(c.r-x.r,2)+Mathf.Pow(c.g-x.g,2)+Mathf.Pow(c.b-x.b,2);
                    linearDistance=Mathf.Min(linearDistance,Distance(expected));gammaDistance=Mathf.Min(gammaDistance,Distance(gamma));
                }
                palette.SetFloat("_ColorsAreSRGB",gammaDistance<linearDistance?1:0);
                Debug.Log($"[GolfGallery] source palette decodeSRGB={gammaDistance<linearDistance}");
            }
            return palette;
        }
        public static GolfGalleryPresentation Create(Transform parent,int variant)
        {
            if(!source)source=Resources.Load<GameObject>("Course/Resort/GolfGallerySpectators");
            if(!source)throw new InvalidOperationException("Authored standing golf gallery source is missing.");
            var root=new GameObject("Premium golf gallery fan "+variant);root.transform.SetParent(parent,false);
            var instance=Instantiate(source,root.transform);instance.name="Authored standing spectator";
            Transform selected=null;
            foreach(var t in instance.GetComponentsInChildren<Transform>(true)){
                if(!t.name.StartsWith("FAN_",StringComparison.Ordinal))continue;
                bool use=t.name=="FAN_"+variant;t.gameObject.SetActive(use);if(use)selected=t;
            }
            if(!selected){Destroy(root);throw new InvalidOperationException("Standing gallery variant is missing: "+variant);}
            selected.localPosition=Vector3.zero;
            var material=Palette(source);
            var view=root.AddComponent<GolfGalleryPresentation>();
            view.head=FindPart(selected,"HEAD");view.left=FindPart(selected,"ARM_L");view.right=FindPart(selected,"ARM_R");
            if(!view.head||!view.left||!view.right){Destroy(root);throw new InvalidOperationException("Standing gallery art joints are incomplete.");}
            var near=new List<Renderer>();var far=new List<Renderer>();
            foreach(var r in selected.GetComponentsInChildren<MeshRenderer>()){
                r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.On;r.receiveShadows=true;
                bool distant=r.name.Contains("_FAR",StringComparison.Ordinal);(distant?far:near).Add(r);
                var mf=r.GetComponent<MeshFilter>();int tris=mf&&mf.sharedMesh?(int)mf.sharedMesh.GetIndexCount(0)/3:0;
                if(distant)view.DistantTriangles+=tris;
                else {view.RigidPieceCount++;view.MeshTriangles+=tris;}
            }
            if(near.Count!=4||far.Count!=4||view.DistantTriangles>8000){
                Destroy(root);throw new InvalidOperationException("Standing gallery requires four rigid parts per LOD and <=8000 distant triangles.");
            }
            var lod=selected.gameObject.AddComponent<LODGroup>();
            lod.fadeMode=LODFadeMode.None;
            lod.SetLODs(new[]{new LOD(.25f,near.ToArray()),new LOD(.002f,far.ToArray())});
            // Body height controls the fit instead of animated raised-arm bounds.
            // This changes to <=8k around6–8 yards at gameplay's48–60 degree FOV,
            // preserving near faces while keeping ordinary gallery gameplay inexpensive.
            lod.localReferencePoint=selected.InverseTransformPoint(root.transform.position+root.transform.up*.87f);
            lod.size=1.75f;
            // FBX has no bones, colliders or clips. A generated Animator is unnecessary.
            foreach(var a in instance.GetComponentsInChildren<Animator>(true))Destroy(a);
            return view;
        }
        void EnsureRest()
        {
            if(restReady)return;
            var inverse=Quaternion.Inverse(transform.rotation);
            headRest=inverse*head.rotation;leftRest=inverse*left.rotation;rightRest=inverse*right.rotation;restReady=true;
        }
        public void Perform(string move,float offset=0)
        {
            EnsureRest();action=move;phase=offset;actionAt=Time.time;
            if(move=="Idle"&&gesture<.001f)Evaluate(Time.time);
        }
        void Update()=>Evaluate(Time.time);
        void Evaluate(float now)
        {
            if(!head||!left||!right)return;EnsureRest();
            float wanted=action=="Idle"?0:1;gesture=Mathf.MoveTowards(gesture,wanted,Time.deltaTime*4.8f);
            float t=now-actionAt+phase;
            float idle=Mathf.Sin(now*.72f+phase*3.1f),breath=Mathf.Sin(now*1.35f+phase*2.7f);
            float pulse=.94f+.06f*Mathf.Sin(t*7.3f+phase*2);
            float leftLift=0,rightLift=0;
            if(action=="FistPump"){
                leftLift=24*gesture;rightLift=(118+13*Mathf.Sin(t*8+phase))*gesture;
            }else if(action=="Cheer"){
                leftLift=(158+6*Mathf.Sin(t*6.1f+phase))*pulse*gesture;
                rightLift=(166+6*Mathf.Sin(t*5.4f+phase+.6f))*pulse*gesture;
            }
            float blend=1-Mathf.Exp(-Time.deltaTime*10);
            leftAngle=Mathf.Lerp(leftAngle,leftLift,blend);rightAngle=Mathf.Lerp(rightAngle,rightLift,blend);
            // All shoulder axes are actor-relative, so arbitrary gallery orientation
            // remains correct. Four rigid meshes use the authored joint origins.
            var facing=transform.rotation;var axis=transform.right;var forward=transform.forward;
            left.rotation=Quaternion.AngleAxis(gesture*12+idle*1.2f,forward)*Quaternion.AngleAxis(-leftAngle+breath*1.3f,axis)*(facing*leftRest);
            right.rotation=Quaternion.AngleAxis(-gesture*10-idle*1.2f,forward)*Quaternion.AngleAxis(-rightAngle-breath*1.2f,axis)*(facing*rightRest);
            var headTarget=Quaternion.AngleAxis(idle*6.5f,transform.up)*Quaternion.AngleAxis(breath*1.7f-gesture*4,axis)*(facing*headRest);
            head.rotation=Quaternion.Slerp(head.rotation,headTarget,blend);
        }
    }
}
