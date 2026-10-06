using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace GolfArcade.Course
{
    /// Bounded visual batches retain each original plant world pivot in UV3.
    /// Original mesh, colour, root weight, phase and collider data are untouched.
    public sealed class GolfPlantInstances:MonoBehaviour
    {
        sealed class Group {public Material material;public ShadowCastingMode shadows;public bool receive;public int layer;public readonly List<CombineInstance> parts=new();public readonly HashSet<MeshRenderer> source=new();}
        readonly List<Mesh> owned=new();
        public static bool ProofMask;
        public static void Apply(GameObject model)
        {
            if(!GolfLook.UseSurfaceShaders||model.GetComponent<GolfPlantInstances>())return;
            var owner=model.AddComponent<GolfPlantInstances>();var groups=new Dictionary<string,Group>();
            foreach(var r in model.GetComponentsInChildren<MeshRenderer>(true))
            {
                if(!r.enabled||!r.gameObject.activeInHierarchy)continue;
                var mf=r.GetComponent<MeshFilter>();if(!mf||!mf.sharedMesh||!mf.sharedMesh.isReadable)continue;
                var mats=r.sharedMaterials;bool plants=mats.Length>0;
                foreach(var m in mats)if(!m||m.shader.name!="GolfArcade/GolfPlants")plants=false;
                if(!plants)continue;
                var mesh=Instantiate(mf.sharedMesh);owner.owned.Add(mesh);
                var pivot=r.transform.position;var uv=new List<Vector3>(mesh.vertexCount);
                for(int i=0;i<mesh.vertexCount;i++)uv.Add(new Vector3(pivot.x,pivot.z,1));mesh.SetUVs(3,uv);
                if(mesh.colors.Length!=mesh.vertexCount){var colours=new Color[mesh.vertexCount];for(int i=0;i<colours.Length;i++)colours[i]=Color.white;mesh.colors=colours;}
                var p=r.bounds.center;int x=Mathf.FloorToInt(p.x/80),z=Mathf.FloorToInt(p.z/80);
                for(int sub=0;sub<mesh.subMeshCount;sub++)
                {
                    var m=mats[Mathf.Min(sub,mats.Length-1)];m.enableInstancing=true;
                    string key=$"{m.GetInstanceID()}/{x}/{z}/{(int)r.shadowCastingMode}/{r.receiveShadows}/{r.gameObject.layer}";
                    if(!groups.TryGetValue(key,out var g)){g=new Group{material=m,shadows=r.shadowCastingMode,receive=r.receiveShadows,layer=r.gameObject.layer};groups.Add(key,g);}
                    g.parts.Add(new CombineInstance{mesh=mesh,subMeshIndex=sub,transform=model.transform.worldToLocalMatrix*r.transform.localToWorldMatrix});g.source.Add(r);
                }
            }
            int count=0,source=0;
            foreach(var g in groups.Values)
            {
                var mesh=new Mesh{name="Golf pivot-preserving plant batch",indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(g.parts.ToArray(),true,true,false);owner.owned.Add(mesh);
                var go=new GameObject("GOLF_PLANT_BATCH_"+count++);go.layer=g.layer;go.transform.SetParent(model.transform,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;
                var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=g.material;r.shadowCastingMode=g.shadows;r.receiveShadows=g.receive;
                foreach(var old in g.source){if(old.enabled)source++;old.enabled=false;}
            }
            Debug.Log($"[GolfPlantInstances] {source} original plant renderers into {count} pivot-preserving visual batches");
        }
        void OnDestroy(){foreach(var mesh in owned)if(mesh)Destroy(mesh);}
    }
}
