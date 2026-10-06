using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace GolfArcade.Course
{
    /// Visual batching only. Original filters, meshes and colliders remain intact.
    /// Bounded cells retain useful culling and local lava-light selection.
    public sealed class GolfSurfaceBatching : MonoBehaviour
    {
        readonly List<Mesh> meshes=new();
        sealed class Group
        {
            public Material material;public ShadowCastingMode shadows;public bool receive;
            public readonly List<CombineInstance> parts=new();public readonly HashSet<MeshRenderer> source=new();
        }
        public static void Apply(GameObject model)
        {
            if(!GolfLook.UseSurfaceShaders||model.GetComponent<GolfSurfaceBatching>())return;
            var owner=model.AddComponent<GolfSurfaceBatching>();var groups=new Dictionary<string,Group>();
            foreach(var r in model.GetComponentsInChildren<MeshRenderer>(true))
            {
                if(!r.enabled||!r.gameObject.activeInHierarchy)continue;
                // Model stand-ins are disabled later by HoleView. Never bake
                // them into an always-visible batch before that happens.
                bool placeholder=false;
                for(var t=r.transform;t&&t!=model.transform;t=t.parent){
                    foreach(var name in new[]{"FLAG","FLAG_POLE","HOLE_CUP","BALL_START","MARKER_TEE","MARKER_PIN","MARKER_UP"})
                        if(t.name==name){placeholder=true;break;}
                    if(placeholder)break;
                }
                if(placeholder)continue;
                var mf=r.GetComponent<MeshFilter>();if(!mf||!mf.sharedMesh||!mf.sharedMesh.isReadable)continue;
                var mats=r.sharedMaterials;bool surface=true;
                foreach(var m in mats)if(!m||!(m.shader.name=="GolfArcade/GolfGround"||m.shader.name=="GolfArcade/GolfRock"||m.shader.name=="Universal Render Pipeline/Lit"))surface=false;
                if(!surface)continue;
                var p=r.bounds.center;float cell=40;
                bool basalt=false;foreach(var m in mats)if(m.name.Contains("BASALT"))basalt=true;
                if(!basalt)cell=240;
                int x=Mathf.FloorToInt(p.x/cell),z=Mathf.FloorToInt(p.z/cell);
                for(int sub=0;sub<mf.sharedMesh.subMeshCount;sub++)
                {
                    var m=mats[Mathf.Min(sub,mats.Length-1)];string key=$"{m.GetInstanceID()}/{x}/{z}/{(int)r.shadowCastingMode}/{r.receiveShadows}";
                    if(!groups.TryGetValue(key,out var g)){g=new Group{material=m,shadows=r.shadowCastingMode,receive=r.receiveShadows};groups.Add(key,g);}
                    g.parts.Add(new CombineInstance{mesh=mf.sharedMesh,subMeshIndex=sub,transform=model.transform.worldToLocalMatrix*r.transform.localToWorldMatrix});g.source.Add(r);
                }
            }
            int count=0,source=0;
            foreach(var g in groups.Values)
            {
                var mesh=new Mesh{name="Golf visual surface batch",indexFormat=IndexFormat.UInt32};
                mesh.CombineMeshes(g.parts.ToArray(),true,true,false);owner.meshes.Add(mesh);
                var go=new GameObject("GOLF_SURFACE_BATCH_"+count++);go.transform.SetParent(model.transform,false);
                go.AddComponent<MeshFilter>().sharedMesh=mesh;var r=go.AddComponent<MeshRenderer>();
                r.sharedMaterial=g.material;r.shadowCastingMode=g.shadows;r.receiveShadows=g.receive;
                foreach(var old in g.source){if(old.enabled)source++;old.enabled=false;}
            }
            Debug.Log($"[GolfSurfaceBatching] {source} original visual renderers into {count} bounded groups; meshes/colliders/scoring unchanged");
        }
        void OnDestroy(){foreach(var mesh in meshes)if(mesh)Destroy(mesh);}
    }
}
