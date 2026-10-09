using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace GolfArcade.Course
{
    /// Reuses the Postcard grass meshes and wind shader. Decoration is added only
    /// after gameplay geometry is scanned, on actual rough triangles away from the ball.
    public static class GolfCourseFringe
    {
        public static void Dress(GameObject model,Hole hole)
        {
            if(hole.Number==12)return; // Dedicated short coastal turf replaces coarse Postcard tufts.
            var prefab=Resources.Load<GameObject>("Course/Standard/Fringe");
            if(!prefab) { Debug.LogError("Missing shared golf fringe library");return; }
            var prototypes=prefab.GetComponentsInChildren<MeshFilter>();
            var colliders=model.GetComponentsInChildren<MeshCollider>();
            var playable=new List<Bounds>();
            foreach(var c in colliders)
                if(c.name.StartsWith("FAIRWAY")||c.name.StartsWith("GREEN")||c.name.StartsWith("TEE_BOX")) playable.Add(c.bounds);
            if(playable.Count==0)return;
            var rng=new System.Random(12009+hole.Number*971);
            var root=new GameObject("DRESS_SHARED_FRINGE");root.transform.SetParent(model.transform,false);
            int count=0,tris=0;
            for(int attempt=0;attempt<2600 && count<320;attempt++)
            {
                var b=playable[rng.Next(playable.Count)];
                float x=b.min.x-5+(float)rng.NextDouble()*(b.size.x+10),z=b.min.z-5+(float)rng.NextDouble()*(b.size.z+10);
                var point=new CoursePoint(x,z);
                if(point.DistanceTo(hole.Tee)<6||point.DistanceTo(hole.Pin)<8)continue;
                MeshCollider top=null;RaycastHit hit=default;float distance=float.MaxValue;
                var ray=new Ray(new Vector3(x,1000,z),Vector3.down);
                foreach(var c in colliders)
                    if(c.Raycast(ray,out var candidate,2000)&&candidate.distance<distance){top=c;hit=candidate;distance=candidate.distance;}
                if(!top||!top.name.StartsWith("TERRAIN")||hit.normal.y<.88f)continue;
                // An island may contain rough, cliff, snow and lava in separate material slots.
                // Inspect the hit triangle, not just the terrain object's name.
                if(!top.TryGetComponent<Renderer>(out var renderer))continue;
                int index=hit.triangleIndex,sub=0;
                for(;sub<top.sharedMesh.subMeshCount;sub++){int n=(int)top.sharedMesh.GetIndexCount(sub)/3;if(index<n)break;index-=n;}
                var mats=renderer.sharedMaterials;
                if(sub>=mats.Length||!mats[sub]||!mats[sub].name.EndsWith(" Rough"))continue;
                string kind=(hole.Number==18||hole.Number==16||hole.Number>=21)?"PLANT_TUFT_T":count%6==0?"PLANT_TUFT_D":"PLANT_TUFT_S";
                MeshFilter source=Array.Find(prototypes,p=>p.name.StartsWith(kind));
                if(!source||!source.sharedMesh)continue;
                int cost=(int)source.sharedMesh.GetIndexCount(0)/3;
                if(tris+cost>18000)break;
                var go=new GameObject("DRESS_FRINGE_"+count++);go.transform.SetParent(root.transform,false);
                go.transform.position=hit.point-Vector3.up*.025f;
                go.transform.rotation=Quaternion.AngleAxis((float)rng.NextDouble()*360,Vector3.up)*source.transform.rotation;
                // Source meshes are metres; gameplay world distances are yards.
                float scale=(.65f+(float)rng.NextDouble()*.35f)/.9144f;
                var unit=source.transform.lossyScale*scale;var parent=root.transform.lossyScale;
                go.transform.localScale=new Vector3(unit.x/parent.x,unit.y/parent.y,unit.z/parent.z);
                go.AddComponent<MeshFilter>().sharedMesh=source.sharedMesh;
                var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=GolfLook.Get("LK_PLANTS");
                r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=true;
                tris+=cost;
            }
            Debug.Log($"[GolfCourseFringe] hole {hole.Number}: {count} shared Postcard tufts, {tris} triangles");
        }
    }
}
