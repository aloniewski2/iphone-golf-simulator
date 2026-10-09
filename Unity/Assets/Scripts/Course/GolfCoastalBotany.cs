using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace GolfArcade.Course
{
    /// Hole12 art pilot only. Artist-derived botanical meshes are render-only;
    /// original MeshFilters, planted pivots, colliders, obstacles and Ground remain authoritative.
    public sealed class GolfCoastalBotany : MonoBehaviour
    {
        sealed class Asset { public Mesh near,far;public Material[] nearMaterials,farMaterials;public float height; }
        sealed class Cell { public readonly List<CombineInstance> near=new(),far=new();public Asset asset; }
        sealed class Crest { public Vector3 a,b,normal;public bool top,side; }
        readonly Dictionary<string,Asset> assets=new();
        readonly Dictionary<(Vector2Int,string),Cell> shrubs=new();
        readonly Dictionary<string,Material> materialCache=new();
        readonly List<Mesh> meshes=new();
        readonly List<Material> materials=new();
        Hole hole;
        Shader shader;
        GolfCoastalInstances instances;
        MeshCollider[] colliders;
        public int TreeCount { get; private set; }
        public int UnderstoryCount { get; private set; }
        public int CrestPocketCount { get; private set; }
        public int HiddenFlowerRenderers { get; private set; }
        public int NearTriangles { get; private set; }
        public int FarTriangles { get; private set; }

        public static GolfCoastalBotany Apply(GameObject model,Hole hole)
        {
            if(!model||hole==null||hole.Number!=12)return null;
            var existing=model.GetComponent<GolfCoastalBotany>();if(existing)return existing;
            var prefab=Resources.Load<GameObject>("Course/Resort/CoastalBotany");
            var shader=Resources.Load<Shader>("Course/Shaders/GolfCoastalFoliage");
            if(!prefab||!shader){Debug.LogError("[GolfCoastalBotany] missing standalone artist kit/shader");return null;}
            var owner=model.AddComponent<GolfCoastalBotany>();owner.hole=hole;owner.shader=shader;
            owner.instances=model.AddComponent<GolfCoastalInstances>();
            owner.colliders=model.GetComponentsInChildren<MeshCollider>();Physics.SyncTransforms();
            var sources=new Dictionary<string,(Mesh,Material[])>();
            foreach(var mf in prefab.GetComponentsInChildren<MeshFilter>())
                if(mf.sharedMesh&&mf.sharedMesh.isReadable)
                    sources[mf.name]=owner.Prepare(mf,prefab.transform.worldToLocalMatrix*mf.transform.localToWorldMatrix);
            foreach(var kind in new[]{"CONIFER_A","CONIFER_B","CONIFER_C","CONIFER_D","SHRUB_A","SHRUB_B","FERN"})
                if(sources.TryGetValue(kind,out var near)&&sources.TryGetValue(kind+"_FAR",out var far))
                    owner.assets[kind]=new Asset{near=near.Item1,far=far.Item1,nearMaterials=near.Item2,farMaterials=far.Item2,height=near.Item1.bounds.size.y};
            if(owner.assets.Count!=7){Debug.LogError("[GolfCoastalBotany] incomplete artist near/far kit");return owner;}
            owner.ReplacePositions();owner.PlantCrestPockets();owner.FlushShrubs();
            // Suppress source botanical renderers only. MeshFilters, collision,
            // extracted obstacle/root data and flower trigger data stay intact.
            foreach(var r in model.GetComponentsInChildren<MeshRenderer>(true))
            {
                bool sourcePlant=r.name=="TREES"||r.name.StartsWith("TREES.")||r.name=="SHRUBS"||r.name.StartsWith("TREE_")||r.name.StartsWith("SHRUB_");
                bool flower=r.name.Contains("FLOWER")||r.name.Contains("BLOOM")||r.name.Contains("TULIP");
                foreach(var m in r.sharedMaterials)
                    if(m&&(m.name.Contains("MAT_FLOWER")||m.name.Contains("MAT_BLOOM")||m.name.Contains("MAT_TULIP")))flower=true;
                if(sourcePlant||flower){if(flower&&r.enabled)owner.HiddenFlowerRenderers++;r.enabled=false;}
            }
            Debug.Log($"[GolfCoastalBotany] hole12 artist CC0: exact tree pivots={owner.TreeCount}; understory={owner.UnderstoryCount}; crest pockets={owner.CrestPocketCount}; full source near={owner.NearTriangles}tri / far={owner.FarTriangles}tri; hidden source flower renderers={owner.HiddenFlowerRenderers}; physics unchanged");
            return owner;
        }

        Material ResolveMaterial(string name)
        {
            string role;
            bool baked=name.Contains("COAST_FIR_VIEW_");
            if(baked)role="FirView_"+name.Substring(name.IndexOf("COAST_FIR_VIEW_",StringComparison.Ordinal)+15,1);
            else if(name.Contains("shrub_02"))role="Shrub";
            else if(name.Contains("fern_02"))role="Fern";
            else if(name.Contains("fir_sapling")&&name.Contains("twig"))role="SaplingNeedles";
            else if(name.Contains("fir_tree")&&name.Contains("twig"))role="FirNeedles";
            else if(name.Contains("trunk_c"))role="FirTrunkC";
            else role="FirBark";
            if(materialCache.TryGetValue(role,out var cached))return cached;
            var diffuse=Resources.Load<Texture2D>("Course/Resort/CoastalBotanyMaps/"+role+"_C");
            var normal=Resources.Load<Texture2D>("Course/Resort/CoastalBotanyMaps/"+role+"_N");
            bool leaf=baked||role.Contains("Needles")||role is "Shrub" or "Fern";
            var mat=new Material(shader){name="Coastal12 artist "+role,enableInstancing=true};
            if(diffuse)mat.SetTexture("_BaseMap",diffuse);else Debug.LogError("[GolfCoastalBotany] missing artist diffuse "+role);
            if(normal)mat.SetTexture("_BumpMap",normal);
            // Warm the author's restrained green towards the reference's olive
            // palette. Texture variation and directional light stay visible.
            mat.SetColor("_BaseColor",leaf?new Color(1.10f,1.20f,.83f,1):Color.white);
            mat.SetFloat("_NormalStrength",baked?0:.4f);mat.SetFloat("_Cutoff",leaf?.30f:0);
            mat.SetFloat("_Leaf",leaf?1:0);mat.SetFloat("_Baked",baked?1:0);
            mat.SetFloat("_CanopyBrightness",role=="Shrub"?1.55f:1.7f);
            mat.SetFloat("_BakedBrightness",4.6f);mat.SetFloat("_ShadeTransmission",.28f);mat.SetFloat("_CanopyFill",.14f);
            mat.SetFloat("_Cull",baked||!leaf?(float)CullMode.Back:(float)CullMode.Off);
            materialCache.Add(role,mat);materials.Add(mat);return mat;
        }

        (Mesh,Material[]) Prepare(MeshFilter source,Matrix4x4 toRoot)
        {
            var old=source.sharedMesh;var vertices=old.vertices;var normals=old.normals;var uv=old.uv;
            var sourceMaterials=source.GetComponent<Renderer>().sharedMaterials;
            var positions=new Vector3[vertices.Length];var ns=new Vector3[vertices.Length];var colors=new Color[vertices.Length];
            var normalMatrix=toRoot.inverse.transpose;
            bool far=source.name.Contains("CONIFER")&&source.name.EndsWith("_FAR",StringComparison.Ordinal);
            // Far posters include empty padding below the root. Preserve it;
            // near source and all plants are already rooted at source y=0.
            for(int i=0;i<vertices.Length;i++)
            {
                positions[i]=toRoot.MultiplyPoint3x4(vertices[i]);
                ns[i]=i<normals.Length?normalMatrix.MultiplyVector(normals[i]).normalized:Vector3.up;
                colors[i]=new Color(1,1,1,far?0:Mathf.Clamp01(positions[i].y/Mathf.Max(.01f,old.bounds.size.y))*.25f);
            }
            var result=new Mesh{name=source.name+" artist coastal UV",indexFormat=IndexFormat.UInt32};
            result.vertices=positions;result.normals=ns;result.colors=colors;
            if(uv.Length==vertices.Length)result.uv=uv;
            result.subMeshCount=old.subMeshCount;var mats=new Material[old.subMeshCount];
            for(int sub=0;sub<old.subMeshCount;sub++)
            {
                result.SetTriangles(old.GetTriangles(sub),sub,false);
                mats[sub]=ResolveMaterial(sub<sourceMaterials.Length&&sourceMaterials[sub]?sourceMaterials[sub].name:"");
            }
            result.RecalculateBounds();if(uv.Length==vertices.Length)result.RecalculateTangents();meshes.Add(result);return(result,mats);
        }

        void ReplacePositions()
        {
            if(hole.Obstacles==null)return;var rng=new System.Random(121904);int index=0;
            foreach(var obstacle in hole.Obstacles)
            {
                if(obstacle.Kind!=ObstacleKind.Tree&&obstacle.Kind!=ObstacleKind.Bush)continue;
                var root=new Vector3((float)obstacle.X,(float)obstacle.Base,(float)obstacle.D);
                if(obstacle.Kind==ObstacleKind.Tree)
                {
                    // Full lower crowns dominate; a few mature open specimens
                    // add believable age variation at the same source pivots.
                    string kind=index%11==0?"CONIFER_D":index%7==0?"CONIFER_A":index%3==0?"CONIFER_B":"CONIFER_C";index++;
                    float height=Mathf.Clamp((float)(obstacle.Top-obstacle.Base)*1.15f,10.5f,18.5f);
                    AddTree(kind,root,height,(float)rng.NextDouble()*360);
                }
                else
                {
                    float height=Mathf.Clamp((float)(obstacle.Top-obstacle.Base)*.80f,.70f,1.45f);
                    AddUnderstory(index++%2==0?"SHRUB_A":"SHRUB_B",root,height,(float)rng.NextDouble()*360);
                }
            }
        }

        void AddTree(string kind,Vector3 point,float height,float angle)
        {
            var asset=assets[kind];float scale=height/Mathf.Max(.01f,asset.height);
            var at=new GameObject("COASTAL12_"+kind+"_"+TreeCount++).transform;at.SetParent(transform,false);
            at.localPosition=transform.InverseTransformPoint(point);at.rotation=Quaternion.Euler(0,angle,0);
            at.localScale=new Vector3(scale/Mathf.Abs(transform.lossyScale.x),scale/Mathf.Abs(transform.lossyScale.y),scale/Mathf.Abs(transform.lossyScale.z));
            instances.Add(asset.near,asset.far,asset.nearMaterials,asset.farMaterials,
                transform.worldToLocalMatrix*at.localToWorldMatrix,true);
            NearTriangles+=asset.near.triangles.Length/3;FarTriangles+=asset.far.triangles.Length/3;
        }

        void AddUnderstory(string kind,Vector3 point,float height,float angle)
        {
            if(!assets.TryGetValue(kind,out var asset))return;float scale=height/Mathf.Max(.01f,asset.height);
            var key=(new Vector2Int(Mathf.FloorToInt(point.x/22),Mathf.FloorToInt(point.z/22)),kind);
            if(!shrubs.TryGetValue(key,out var cell)){cell=new Cell{asset=asset};shrubs.Add(key,cell);}
            var matrix=transform.worldToLocalMatrix*Matrix4x4.TRS(point,Quaternion.Euler(0,angle,0),Vector3.one*scale);
            cell.near.Add(new CombineInstance{mesh=asset.near,transform=matrix});cell.far.Add(new CombineInstance{mesh=asset.far,transform=matrix});
            NearTriangles+=asset.near.triangles.Length/3;FarTriangles+=asset.far.triangles.Length/3;UnderstoryCount++;
        }
        bool TopGround(Vector3 around,out Vector3 point)
        {
            point=default;float highest=float.NegativeInfinity;MeshCollider land=null;RaycastHit best=default;
            var ray=new Ray(new Vector3(around.x,90,around.z),Vector3.down);
            foreach(var collider in colliders)
                if(collider.name.StartsWith("TERRAIN")&&collider.Raycast(ray,out var hit,100)&&hit.normal.y>.80f&&hit.point.y>5&&hit.point.y>highest)
                    {highest=hit.point.y;land=collider;best=hit;}
            if(!land)return false;
            foreach(var other in colliders)
                if(other!=land&&!other.name.StartsWith("TERRAIN")&&other.Raycast(ray,out var overlay,100)&&overlay.distance<=best.distance+.10f)
                    return false;
            // Never plant inside the tee or green approach, even on coincident
            // terrain. Low crest pockets frame the carry from the rough margins.
            var p=new CoursePoint(best.point.x,best.point.z);
            if(p.DistanceTo(hole.Tee)<12||p.DistanceTo(hole.Pin)<hole.GreenRadius+4)return false;
            if(hole.Hazards!=null)foreach(var hazard in hole.Hazards)if(hazard.Contains(p))return false;
            if(hole.Obstacles!=null)foreach(var obstacle in hole.Obstacles)
                if(obstacle.Kind==ObstacleKind.Wall&&Math.Pow(obstacle.X-p.X,2)+Math.Pow(obstacle.D-p.D,2)<Math.Pow(obstacle.Radius+2,2))return false;
            point=best.point-Vector3.up*.025f;return true;
        }

        static int Compare(Vector3Int a,Vector3Int b)=>a.x!=b.x?a.x.CompareTo(b.x):a.y!=b.y?a.y.CompareTo(b.y):a.z.CompareTo(b.z);
        void PlantCrestPockets()
        {
            var edges=new Dictionary<(Vector3Int,Vector3Int),Crest>();
            Vector3Int Key(Vector3 p)=>new(Mathf.RoundToInt(p.x*100),Mathf.RoundToInt(p.y*100),Mathf.RoundToInt(p.z*100));
            foreach(var collider in colliders)
            {
                if(!collider.name.StartsWith("TERRAIN")||!collider.sharedMesh||!collider.sharedMesh.isReadable)continue;
                var mesh=collider.sharedMesh;var vertices=mesh.vertices;var ids=mesh.triangles;
                for(int n=0;n<ids.Length;n+=3)
                {
                    var a=collider.transform.TransformPoint(vertices[ids[n]]);var b=collider.transform.TransformPoint(vertices[ids[n+1]]);var c=collider.transform.TransformPoint(vertices[ids[n+2]]);
                    var normal=Vector3.Cross(b-a,c-a).normalized;bool top=normal.y>.75f,side=Mathf.Abs(normal.y)<.55f;
                    if(!top&&!side)continue;
                    Edge(a,b,normal,top,side);Edge(b,c,normal,top,side);Edge(c,a,normal,top,side);
                }
            }
            void Edge(Vector3 a,Vector3 b,Vector3 normal,bool top,bool side)
            {
                if(Mathf.Min(a.y,b.y)<5||Mathf.Abs(a.y-b.y)>1.9f)return;
                var ka=Key(a);var kb=Key(b);if(Compare(ka,kb)>0){var swap=ka;ka=kb;kb=swap;}
                var key=(ka,kb);if(!edges.TryGetValue(key,out var edge)){edge=new Crest{a=a,b=b};edges.Add(key,edge);}
                edge.top|=top;edge.side|=side;if(side){normal.y=0;edge.normal+=normal.normalized;}
            }
            var rng=new System.Random(129204);var centres=new List<Vector3>();var zoneCounts=new int[3];
            foreach(var edge in edges.Values)
            {
                if(!edge.top||!edge.side||edge.normal.sqrMagnitude<.001f)continue;
                var outward=edge.normal.normalized;var along=(edge.b-edge.a).normalized;float length=Vector3.Distance(edge.a,edge.b);
                int steps=Mathf.Max(1,Mathf.CeilToInt(length/10));
                for(int n=0;n<steps;n++)
                {
                    float t=(n+.30f+(float)rng.NextDouble()*.4f)/steps;
                    var candidate=Vector3.Lerp(edge.a,edge.b,t)-outward*(1.35f+(float)rng.NextDouble()*1.10f);
                    int zone=candidate.z<70?0:candidate.z<145?1:2;if(zoneCounts[zone]>=14)continue;
                    bool crowded=false;foreach(var p in centres)if((new Vector2(p.x-candidate.x,p.z-candidate.z)).sqrMagnitude<55){crowded=true;break;}
                    if(crowded||rng.NextDouble()<.20||!TopGround(candidate,out var root))continue;
                    centres.Add(root);CrestPocketCount++;zoneCounts[zone]++;
                    int count=4+rng.Next(4);
                    for(int k=0;k<count;k++)
                    {
                        var spread=root+along*((float)rng.NextDouble()-.5f)*5.3f-outward*((float)rng.NextDouble()-.3f)*2.1f;
                        if(!TopGround(spread,out var at))continue;
                        string kind=k%4==0?"FERN":k%2==0?"SHRUB_A":"SHRUB_B";
                        float height=kind=="FERN"?.48f+(float)rng.NextDouble()*.35f:.70f+(float)rng.NextDouble()*.55f;
                        AddUnderstory(kind,at,height,(float)rng.NextDouble()*360);
                    }
                }
            }
        }

        MeshRenderer Make(Transform parent,string name,Mesh mesh,Material[] mats,ShadowCastingMode shadows)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterials=mats;renderer.shadowCastingMode=shadows;renderer.receiveShadows=true;return renderer;
        }
        void FlushShrubs()
        {
            foreach(var cell in shrubs.Values)
                foreach(var part in cell.near)
                    instances.Add(cell.asset.near,cell.asset.far,cell.asset.nearMaterials,cell.asset.farMaterials,part.transform,false);
        }
        void OnDestroy(){foreach(var mesh in meshes)if(mesh)Destroy(mesh);foreach(var mat in materials)if(mat)Destroy(mat);}
    }
}
