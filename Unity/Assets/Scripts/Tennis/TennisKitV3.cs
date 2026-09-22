using System;
using System.Linq;
using UnityEngine;

namespace GolfArcade.Tennis
{
    public static class TennisKitV3
    {
        public static void Dress(Transform model,bool female)
        {
            var bones=model.GetComponentsInChildren<Transform>(true).GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.First());
            var prefab=Resources.Load<GameObject>("Tennis/KitsV3/standard_"+(female?"female":"male")+"_kit");
            if(!prefab) throw new InvalidOperationException("Fitted tennis kit v3 missing");
            foreach(var r in model.GetComponentsInChildren<Renderer>(true))
            {
                string n=r.name.ToLowerInvariant();
                if(n.Contains("tennis 1 top") || n.Contains("tennis bottom") || n.Contains("skort") || n.Contains("skirt") || n.StartsWith("bare thigh") || n.Contains("visor") || n.Contains("armhole") || n.Contains("waistband") || n.Contains("collar") || n.Contains("shirt button") || n.Contains("shoe ") || n.StartsWith("lace ") || n.Contains("joined hip panel")) r.enabled=false;
            }
            var kit=UnityEngine.Object.Instantiate(prefab,model);kit.name="Fitted Tripo tennis kit v3";
            foreach(var animator in kit.GetComponentsInChildren<Animator>()) animator.enabled=false;
            foreach(var r in kit.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var mesh=UnityEngine.Object.Instantiate(r.sharedMesh);
                var poses=mesh.bindposes;
                for(int i=0;i<r.bones.Length;i++) {
                    var old=r.bones[i];var target=bones[old.name];
                    Vector3 a=old.lossyScale,b=target.lossyScale;
                    // The fitted FBX carries centimetre bone scale; the live rig is metres.
                    poses[i]=Matrix4x4.Scale(new Vector3(Mathf.Abs(a.x/b.x),Mathf.Abs(a.y/b.y),Mathf.Abs(a.z/b.z)))*poses[i];
                }
                mesh.bindposes=poses;r.sharedMesh=mesh;
                r.bones=r.bones.Select(b=>bones.TryGetValue(b.name,out var target)?target:throw new InvalidOperationException("Kit bone missing: "+b.name)).ToArray();
                if(r.rootBone && bones.TryGetValue(r.rootBone.name,out var root)) r.rootBone=root;
                r.updateWhenOffscreen=true;
            }
        }
        /// Distance from the grip to the centre of the string bed in the imported racket.
        public const float HeadCentre=.49049f, GripInset=.075f;

        public static Transform Equip(Transform model,Transform mount,Vector3 center,Vector3 up,Vector3 normal,out float scale)
        {
            var prefab=Resources.Load<GameObject>("Tennis/KitsV3/RacketGameplay");
            if(!prefab) throw new InvalidOperationException("Tripo tennis racket v3 missing");
            foreach(var r in model.GetComponentsInChildren<Renderer>(true))
                if(r.name.IndexOf("V4 racket",StringComparison.OrdinalIgnoreCase)>=0) r.enabled=false;
            // Preserve the imported FBX's centimetre scale/axis conversion below a metre-space socket.
            var visual=new GameObject("Tripo racket v3 — aligned string bed").transform;
            visual.SetParent(mount,false);
            var instance=UnityEngine.Object.Instantiate(prefab,visual,false);
            scale=MatchHitbox(visual,instance);
            visual.localScale=Vector3.one*scale;
            visual.SetPositionAndRotation(center-up*(HeadCentre*scale),Quaternion.LookRotation(normal,up));
            return visual;
        }

        /// Scale the racket so its painted string bed covers roughly the area the physics
        /// actually uses, instead of the hitbox being far wider than the mesh.
        ///
        /// Measured from mesh bounds, NOT `Renderer.bounds`: the latter is a world-space AABB,
        /// and re-transforming its corners into another space inflates it further — it
        /// reported a 0.54m "head width" for a racket a few centimetres thick, which made an
        /// earlier version of this shrink the racket instead of enlarging it.
        static float MatchHitbox(Transform visual,GameObject instance)
        {
            bool any=false; var local=new Bounds();
            foreach(var r in instance.GetComponentsInChildren<Renderer>(true))
            {
                Mesh mesh=r is SkinnedMeshRenderer skinned ? skinned.sharedMesh
                    : r.GetComponent<MeshFilter>() ? r.GetComponent<MeshFilter>().sharedMesh : null;
                if(!mesh) continue;
                Bounds b=mesh.bounds;
                Matrix4x4 toVisual=visual.worldToLocalMatrix*r.transform.localToWorldMatrix;
                for(int i=0;i<8;i++)
                {
                    var corner=toVisual.MultiplyPoint3x4(new Vector3(
                        (i&1)==0?b.min.x:b.max.x,(i&2)==0?b.min.y:b.max.y,(i&4)==0?b.min.z:b.max.z));
                    if(any) local.Encapsulate(corner); else { local=new Bounds(corner,Vector3.zero); any=true; }
                }
            }
            if(!any) return 1;
            // The racket runs along the socket's +Y. Of the two cross axes, the head's width is
            // the larger; the smaller is the frame's thickness.
            float width=Mathf.Max(local.size.x,local.size.z);
            float wanted=TennisRules.StringHalfWidth*2;
            float raw=width>.0001f?wanted/width:1;
            Debug.Log($"RACKETFIT bounds={local.size} width={width:0.000} wanted={wanted:0.000} raw={raw:0.000}");
            if(width<=.0001f) return 1;
            // Only ever enlarge toward the hitbox. Shrinking the racket to match a smaller
            // hitbox would be the wrong direction: the complaint is that contact looks like
            // air hitting the ball, so the mesh should grow, never shrink.
            return Mathf.Clamp(raw,1f,3.5f);
        }
    }
}
