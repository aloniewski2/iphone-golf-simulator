using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace GolfArcade.Course
{
    /// Softens only the render copy of legacy shore sheets. Original collision and
    /// water geometry is retained. One subdivision gives the strip an interior row.
    public static class GolfCourseFoam
    {
        struct Edge { public Vector3 a,b;public int count; }
        static string Key(Vector3 p)=>$"{Mathf.RoundToInt(p.x*1000)}/{Mathf.RoundToInt(p.y*1000)}/{Mathf.RoundToInt(p.z*1000)}";
        public static void Feather(Mesh mesh,Transform transform)
        {
            if(mesh.subMeshCount!=1)return;
            var source=mesh.vertices;var uv=mesh.uv;var indices=mesh.triangles;
            var edges=new Dictionary<string,Edge>();
            for(int i=0;i<indices.Length;i+=3)for(int j=0;j<3;j++)
            {
                var a=source[indices[i+j]];var b=source[indices[i+(j+1)%3]];
                string ak=Key(a),bk=Key(b);var key=string.CompareOrdinal(ak,bk)<0?ak+":"+bk:bk+":"+ak;
                if(edges.TryGetValue(key,out var e)){e.count++;edges[key]=e;}
                else edges[key]=new Edge{a=transform.TransformPoint(a),b=transform.TransformPoint(b),count=1};
            }
            var boundary=new List<Edge>();foreach(var e in edges.Values)if(e.count==1)boundary.Add(e);
            var vertices=new List<Vector3>();var tex=new List<Vector2>();var colors=new List<Color>();var triangles=new List<int>();
            for(int i=0;i<indices.Length;i+=3)
            {
                int a=indices[i],b=indices[i+1],c=indices[i+2];
                int at=vertices.Count;
                Add(source[a],uv[a]);Add(source[b],uv[b]);Add(source[c],uv[c]);
                Add((source[a]+source[b])*.5f,(uv[a]+uv[b])*.5f);
                Add((source[b]+source[c])*.5f,(uv[b]+uv[c])*.5f);
                Add((source[c]+source[a])*.5f,(uv[c]+uv[a])*.5f);
                foreach(int v in new[]{0,3,5,3,1,4,5,4,2,3,4,5})triangles.Add(at+v);
            }
            mesh.Clear();mesh.indexFormat=vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16;
            mesh.SetVertices(vertices);mesh.SetUVs(0,tex);mesh.SetColors(colors);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            void Add(Vector3 vertex,Vector2 t)
            {
                var p=transform.TransformPoint(vertex);float distance=100;
                foreach(var e in boundary){var d=e.b-e.a;float s=Mathf.Clamp01(Vector3.Dot(p-e.a,d)/Mathf.Max(d.sqrMagnitude,.000001f));distance=Mathf.Min(distance,(p-e.a-d*s).magnitude);}
                vertices.Add(vertex);tex.Add(t);colors.Add(new Color(1,1,1,Mathf.SmoothStep(0,1,Mathf.Clamp01(distance/.8f))));
            }
        }
    }
}
