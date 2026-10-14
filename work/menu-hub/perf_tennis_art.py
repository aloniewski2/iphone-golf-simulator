# TennisVenueArt.BotanicalBatch.Append: cache each prototype's mesh arrays and per-vertex colours once instead of copying them
# for every plant instance (same output, a fraction of the time). Idempotent. usage: perf_tennis_art.py <TennisVenueArt.cs>
import sys
p = sys.argv[1]; s = open(p).read()
if "sealed class ProtoData" in s: print("already applied"); sys.exit(0)
old = '''                var mf=proto.filter;var source=mf.sharedMesh;var vertices=source.vertices;var normals=source.normals;var weight=source.colors;var leafPivots=source.uv2;
                var sourceColours=new Color[vertices.Length];var renderer=mf.GetComponent<Renderer>();
                var placement=Matrix4x4.TRS(p,Quaternion.Euler(0,yaw,0),new Vector3(radius,height,radius));
                var matrix=placement*proto.matrix;var normalMatrix=matrix.inverse.transpose;
                var leafNormalMatrix=Matrix4x4.Rotate(Quaternion.Euler(0,yaw,0))*proto.matrix.inverse.transpose;
                int start=cell.vertices.Count;
                for(int sub=0;sub<source.subMeshCount;sub++)
                {
                    int role=renderer&&sub<renderer.sharedMaterials.Length&&renderer.sharedMaterials[sub]?Role(renderer.sharedMaterials[sub].name):Mathf.Min(sub,6);
                    foreach(int index in source.GetTriangles(sub))
                    {
                        var colour=TennisResortMaterials.BotanicalColour(Palette[role],role);colour.a=weight.Length==vertices.Length?weight[index].r:(role>0&&role<6?1:0);
                        sourceColours[index]=colour;cell.triangles.Add(start+index);
                    }
                }
                for(int i=0;i<vertices.Length;i++)'''
new = '''                // the prototype's arrays and per-vertex colours depend only on the prototype: read them once (they used to be copied
                // out of the mesh for every plant, which made a resort build spend seconds here)
                var data=Data(name,proto.filter);var vertices=data.vertices;var normals=data.normals;var leafPivots=data.leafPivots;var sourceColours=data.colours;
                var placement=Matrix4x4.TRS(p,Quaternion.Euler(0,yaw,0),new Vector3(radius,height,radius));
                var matrix=placement*proto.matrix;var normalMatrix=matrix.inverse.transpose;
                var leafNormalMatrix=Matrix4x4.Rotate(Quaternion.Euler(0,yaw,0))*proto.matrix.inverse.transpose;
                int start=cell.vertices.Count;
                foreach(int index in data.triangles)cell.triangles.Add(start+index);
                for(int i=0;i<vertices.Length;i++)'''
add = '''            sealed class ProtoData { public Vector3[] vertices,normals; public Vector2[] leafPivots; public Color[] colours; public int[] triangles; }
            readonly Dictionary<string,ProtoData> data=new();
            ProtoData Data(string name,MeshFilter mf)
            {
                if(data.TryGetValue(name,out var d))return d;
                var source=mf.sharedMesh;var vertices=source.vertices;var weight=source.colors;var renderer=mf.GetComponent<Renderer>();var materials=renderer?renderer.sharedMaterials:null;
                d=new ProtoData{vertices=vertices,normals=source.normals,leafPivots=source.uv2,colours=new Color[vertices.Length]};
                var triangles=new List<int>();
                for(int sub=0;sub<source.subMeshCount;sub++)
                {
                    int role=materials!=null&&sub<materials.Length&&materials[sub]?Role(materials[sub].name):Mathf.Min(sub,6);
                    var baseColour=TennisResortMaterials.BotanicalColour(Palette[role],role);
                    foreach(int index in source.GetTriangles(sub))
                    {
                        var colour=baseColour;colour.a=weight.Length==vertices.Length?weight[index].r:(role>0&&role<6?1:0);
                        d.colours[index]=colour;triangles.Add(index);
                    }
                }
                d.triangles=triangles.ToArray();data[name]=d;return d;
            }
            void Append(string name,Vector3 p,float radius,float height,float yaw,Dictionary<Vector2Int,Cell> pockets,float cellSize)'''
anchor = '''            void Append(string name,Vector3 p,float radius,float height,float yaw,Dictionary<Vector2Int,Cell> pockets,float cellSize)'''
for a, b in [(old, new), (anchor, add)]:
    if s.count(a) != 1: sys.exit(f"TennisVenueArt.cs: cannot place the cache ({a[:60]!r} found {s.count(a)}x)")
    s = s.replace(a, b)
open(p, "w").write(s); print("applied")
