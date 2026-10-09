using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace GolfArcade.Course
{
    /// A continuous eroded render surface. The source MeshFilter and collider remain
    /// untouched; only their renderer is replaced after obstacle/ground extraction.
    public sealed class GolfCliffSculpt : MonoBehaviour
    {
        readonly List<Mesh> owned = new();
        public int AddedTriangles { get; private set; }
        public int MovedVertices { get; private set; }
        public float MaximumDisplacement { get; private set; }
        public static void Apply(GameObject root,Hole hole)
        {
            if(root.GetComponent<GolfCliffSculpt>())return;
            var owner=root.AddComponent<GolfCliffSculpt>();
            foreach(var collider in root.GetComponentsInChildren<MeshCollider>())
                if(collider.name.StartsWith("TERRAIN")&&collider.sharedMesh&&collider.sharedMesh.isReadable&&collider.TryGetComponent<MeshRenderer>(out var renderer))
                    owner.Sculpt(collider,renderer,hole);
            Debug.Log($"[GolfCliffSculpt] hole {hole.Number}: {owner.owned.Count} continuous erosion shells, {owner.AddedTriangles} added triangles, {owner.MovedVertices} displaced vertices, max {owner.MaximumDisplacement:F2} yd; collision meshes unchanged");
        }
        void Sculpt(MeshCollider source,MeshRenderer renderer,Hole hole)
        {
            var mesh=source.sharedMesh;var mats=renderer.sharedMaterials;
            var rock=new bool[mesh.subMeshCount];bool any=false;
            for(int s=0;s<rock.Length;s++){
                string name=s<mats.Length&&mats[s]?mats[s].name:"";
                rock[s]=name.Contains(" Cliff")||name.Contains(" Basalt")||name.Contains(" Sandstone");any|=rock[s];
            }
            if(!any)return;
            var original=mesh.vertices;var originalUV=mesh.uv;var originalColor=mesh.colors;var originalNormal=mesh.normals;
            var vertices=new List<Vector3>(original);var uv=new List<Vector2>();var colors=new List<Color>();
            for(int i=0;i<original.Length;i++){uv.Add(i<originalUV.Length?originalUV[i]:Vector2.zero);colors.Add(i<originalColor.Length?originalColor[i]:Color.white);}
            var locked=new HashSet<Vector3Int>();
            Vector3Int Key(Vector3 p)=>new(Mathf.RoundToInt(p.x*1000),Mathf.RoundToInt(p.y*1000),Mathf.RoundToInt(p.z*1000));
            // Every shared top/rough/surf vertex is exactly retained, avoiding edge cracks.
            for(int s=0;s<rock.Length;s++)if(!rock[s])foreach(int i in mesh.GetTriangles(s))locked.Add(Key(source.transform.TransformPoint(original[i])));
            // Read the authored cliff columns directly. Ground queries just outside a
            // cliff return sea level and would suppress all erosion on those faces.
            var columns=new Dictionary<Vector2Int,Vector3>();
            var columnNormals=new Dictionary<Vector2Int,Vector3>();
            for(int s=0;s<rock.Length;s++)if(rock[s])foreach(int i in mesh.GetTriangles(s)){
                var p=source.transform.TransformPoint(original[i]);var key=new Vector2Int(Mathf.RoundToInt(p.x*10),Mathf.RoundToInt(p.z*10));
                if(!columns.TryGetValue(key,out var old)||p.y>old.y)columns[key]=p;
            }
            for(int sub=0;sub<rock.Length;sub++)if(rock[sub]){
                var ids=mesh.GetTriangles(sub);
                for(int t=0;t<ids.Length;t+=3){
                    var a=source.transform.TransformPoint(original[ids[t]]);var b=source.transform.TransformPoint(original[ids[t+1]]);var c=source.transform.TransformPoint(original[ids[t+2]]);
                    var face=Vector3.Cross(b-a,c-a);face.y=0;if(face.sqrMagnitude<.00001f)continue;
                    for(int j=0;j<3;j++){
                        var p=source.transform.TransformPoint(original[ids[t+j]]);var k=new Vector2Int(Mathf.RoundToInt(p.x*10),Mathf.RoundToInt(p.z*10));
                        columnNormals.TryGetValue(k,out var sum);columnNormals[k]=sum+face.normalized;
                    }
                }
            }
            var crests=new List<Vector3>(columns.Values);
            var welded=new Dictionary<Vector3Int,int>();var sections=new List<int>[mesh.subMeshCount];
            int vertex(Vector3 world,Vector2 tex,Vector3 normal){
                var key=Key(world);if(welded.TryGetValue(key,out int index))return index;
                var p=world;
                if(!locked.Contains(key)){
                    float closest=float.MaxValue,second=float.MaxValue,top=source.bounds.max.y,nextTop=top;
                    Vector3 crestNormal=normal,nextNormal=normal;
                    foreach(var crest in crests){
                        float d=(crest.x-p.x)*(crest.x-p.x)+(crest.z-p.z)*(crest.z-p.z);
                        if(d<closest){second=closest;nextTop=top;nextNormal=crestNormal;closest=d;top=crest.y;columnNormals.TryGetValue(new Vector2Int(Mathf.RoundToInt(crest.x*10),Mathf.RoundToInt(crest.z*10)),out crestNormal);}
                        else if(d<second){second=d;nextTop=crest.y;columnNormals.TryGetValue(new Vector2Int(Mathf.RoundToInt(crest.x*10),Mathf.RoundToInt(crest.z*10)),out nextNormal);}
                    }
                    top=Mathf.Max(top,nextTop);
                    float fade=Mathf.SmoothStep(0,1,Mathf.Clamp01((top-p.y)/.8f));
                    fade*=Mathf.SmoothStep(0,1,Mathf.Clamp01((p.y-source.bounds.min.y)/1.0f));
                    float broad=Mathf.PerlinNoise(p.x*.055f+19.3f,p.z*.055f+p.y*.078f+7.7f)-.5f;
                    float shelves=Mathf.PerlinNoise(p.x*.13f+p.z*.07f,p.y*.25f+31)-.5f;
                    float shelf=Mathf.Sin(p.y*.43f+Mathf.PerlinNoise(p.x*.07f,p.z*.07f)*3.0f);
                    float erosion=(.35f+broad*2.1f+shelves*.65f+shelf*.24f)*fade;
                    var outward=(crestNormal.normalized+nextNormal.normalized)*.5f;if(outward.sqrMagnitude<.01f)outward=normal;outward.y=0;outward.Normalize();
                    p+=outward*erosion;
                    // Lateral warping gives strata irregular shoulders rather than rows.
                    p.x+=(Mathf.PerlinNoise(p.z*.10f+52,p.y*.12f)-.5f)*.24f*fade;
                    p.z+=(Mathf.PerlinNoise(p.x*.10f+11,p.y*.12f+18)-.5f)*.24f*fade;
                }
                float moved=(p-world).magnitude;if(moved>.01f){MovedVertices++;MaximumDisplacement=Mathf.Max(MaximumDisplacement,moved);}
                index=vertices.Count;vertices.Add(source.transform.InverseTransformPoint(p));uv.Add(tex);colors.Add(Color.white);welded.Add(key,index);return index;
            }
            void triangle(Vector3 a,Vector3 b,Vector3 c,Vector2 ua,Vector2 ub,Vector2 uc,List<int> output,int depth){
                float ab=(a-b).sqrMagnitude,bc=(b-c).sqrMagnitude,ca=(c-a).sqrMagnitude;
                if(depth<7&&Mathf.Max(ab,Mathf.Max(bc,ca))>16.0f){
                    if(ab>=bc&&ab>=ca){var mid=(a+b)*.5f;var t=(ua+ub)*.5f;triangle(a,mid,c,ua,t,uc,output,depth+1);triangle(mid,b,c,t,ub,uc,output,depth+1);}
                    else if(bc>=ca){var mid=(b+c)*.5f;var t=(ub+uc)*.5f;triangle(a,b,mid,ua,ub,t,output,depth+1);triangle(a,mid,c,ua,t,uc,output,depth+1);}
                    else {var mid=(c+a)*.5f;var t=(uc+ua)*.5f;triangle(a,b,mid,ua,ub,t,output,depth+1);triangle(mid,b,c,t,ub,uc,output,depth+1);}
                    return;
                }
                var n=Vector3.Cross(b-a,c-a).normalized;output.Add(vertex(a,ua,n));output.Add(vertex(b,ub,n));output.Add(vertex(c,uc,n));
            }
            int before=0,after=0;
            for(int s=0;s<sections.Length;s++){
                int[] indices=mesh.GetTriangles(s);before+=indices.Length/3;sections[s]=new List<int>();
                if(!rock[s])sections[s].AddRange(indices);
                else for(int t=0;t<indices.Length;t+=3){
                    int a=indices[t],b=indices[t+1],c=indices[t+2];
                    triangle(source.transform.TransformPoint(original[a]),source.transform.TransformPoint(original[b]),source.transform.TransformPoint(original[c]),uv[a],uv[b],uv[c],sections[s],0);
                }
                after+=sections[s].Count/3;
            }
            var sculpt=new Mesh{name="Resort continuous eroded cliff",indexFormat=IndexFormat.UInt32};
            sculpt.SetVertices(vertices);sculpt.SetUVs(0,uv);sculpt.SetColors(colors);sculpt.subMeshCount=sections.Length;
            for(int s=0;s<sections.Length;s++)sculpt.SetTriangles(sections[s],s);
            sculpt.RecalculateNormals();var normals=sculpt.normals;for(int i=0;i<originalNormal.Length;i++)normals[i]=originalNormal[i];sculpt.normals=normals;sculpt.RecalculateBounds();owned.Add(sculpt);AddedTriangles+=after-before;
            var go=new GameObject("RESORT_ERODED_CLIFF");go.transform.SetParent(source.transform,false);
            go.AddComponent<MeshFilter>().sharedMesh=sculpt;var r=go.AddComponent<MeshRenderer>();r.sharedMaterials=mats;r.shadowCastingMode=renderer.shadowCastingMode;r.receiveShadows=renderer.receiveShadows;
            renderer.enabled=false;
        }
        void OnDestroy(){foreach(var mesh in owned)if(mesh)Destroy(mesh);}
    }
}
