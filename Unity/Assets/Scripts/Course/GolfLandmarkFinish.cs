using System.Collections.Generic;
using UnityEngine;

namespace GolfArcade.Course
{
    /// Bevelled render candidates match the original local bounds. Physics keeps the
    /// original MeshFilters, active objects and authored obstacle extraction untouched.
    public sealed class GolfLandmarkFinish : MonoBehaviour
    {
        public int Applied { get; private set; }
        public static void Apply(GameObject model,Hole hole,GolfCourseLook look)
        {
            if(model.GetComponent<GolfLandmarkFinish>())return;
            var prefab=Resources.Load<GameObject>($"Course/Resort/Finish_{hole.Number:00}");if(!prefab)return;
            var owner=model.AddComponent<GolfLandmarkFinish>();
            var originals=new Dictionary<string,MeshFilter>();
            foreach(var mf in model.GetComponentsInChildren<MeshFilter>(true))if(mf.sharedMesh)originals[mf.name]=mf;
            foreach(var candidate in prefab.GetComponentsInChildren<MeshFilter>()){
                if(!candidate.sharedMesh||!originals.TryGetValue(candidate.name,out var source)||!source.TryGetComponent<Renderer>(out var old))continue;
                var a=source.sharedMesh.bounds;var b=candidate.sharedMesh.bounds;
                float tolerance=Mathf.Max(.005f,a.size.magnitude*.015f);
                if((a.center-b.center).magnitude>tolerance||(a.size-b.size).magnitude>tolerance){Debug.LogWarning($"[GolfLandmarkFinish] {candidate.name} skipped: local bounds do not match source");continue;}
                var go=new GameObject("RESORT_LANDMARK_"+candidate.name);go.transform.SetParent(source.transform,false);
                go.AddComponent<MeshFilter>().sharedMesh=candidate.sharedMesh;
                var renderer=go.AddComponent<MeshRenderer>();var prototype=candidate.GetComponent<Renderer>();
                var mats=prototype?prototype.sharedMaterials:old.sharedMaterials;
                for(int i=0;i<mats.Length;i++){
                    var dressed=mats[i]?look.Resolve(mats[i],candidate.name):null;
                    if(dressed)mats[i]=dressed;else if(i<old.sharedMaterials.Length)mats[i]=old.sharedMaterials[i];
                }
                renderer.sharedMaterials=mats;renderer.shadowCastingMode=old.shadowCastingMode;renderer.receiveShadows=old.receiveShadows;
                old.enabled=false;owner.Applied++;
            }
            Debug.Log($"[GolfLandmarkFinish] hole {hole.Number}: {owner.Applied} bevelled visual landmarks with original physics retained");
        }
    }
}
