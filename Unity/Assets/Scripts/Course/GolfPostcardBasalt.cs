using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace GolfArcade.Course
{
    /// Crisp hexagonal geology replaces the visual wall modules, including the
    /// stacked block skyline. Original meshes remain the collision/obstacle source.
    public sealed class GolfPostcardBasalt : MonoBehaviour
    {
        readonly List<Mesh> meshes = new();
        Material material;
        public int Columns { get; private set; }
        public static void Apply(GameObject model, Hole hole)
        {
            if (hole.Number != 10 || model.GetComponent<GolfPostcardBasalt>()) return;
            var owner=model.AddComponent<GolfPostcardBasalt>();
            owner.material=new Material(Resources.Load<Shader>("Course/Shaders/GolfBasaltColumns")){name="Crater hexagonal basalt and hot fissures"};
            var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
            foreach(var r in model.GetComponentsInChildren<MeshRenderer>())
            {
                if(!r.enabled || !r.name.StartsWith("ROCK_WALL_BASALT") || !r.TryGetComponent<MeshFilter>(out var mf) || !mf.sharedMesh)continue;
                // Principal horizontal axis of the imported world vertices.
                var centre=r.bounds.center;
                double xx=0,zz=0,xz=0;
                foreach(var vertex in mf.sharedMesh.vertices){var d=mf.transform.TransformPoint(vertex)-centre;xx+=d.x*d.x;zz+=d.z*d.z;xz+=d.x*d.z;}
                float angle=.5f*Mathf.Atan2((float)(2*xz),(float)(xx-zz));
                var along=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));var across=Vector3.Cross(Vector3.up,along);
                float length=0,depth=0;
                foreach(var local in mf.sharedMesh.vertices){var p=mf.transform.TransformPoint(local)-centre;length=Mathf.Max(length,Mathf.Abs(Vector3.Dot(p,along)));depth=Mathf.Max(depth,Mathf.Abs(Vector3.Dot(p,across)));}
                int count=Mathf.Clamp(Mathf.CeilToInt(length*2/2.8f),2,36);
                float step=length*2/count;
                for(int n=0;n<count;n++)
                {
                    var p=centre+along*(-length+(n+.5f)*step);
                    float crest=.68f+.30f*Mathf.PerlinNoise(p.x*.045f+11,p.z*.045f+7)+.12f*Mathf.PerlinNoise(p.x*.8f,p.z*.8f);
                    // Tall rectangular stack modules become sloping organ-pipe ridges.
                    float height=r.bounds.size.y*crest;
                    float top=r.bounds.min.y+height;
                    float bottom=Mathf.Min(r.bounds.min.y,0f);
                    owner.Column(model.transform,vertices,uv,triangles,new Vector3(p.x,bottom,p.z),along,across,step*.57f,Mathf.Max(.9f,depth*.86f),top-bottom);
                }
                r.enabled=false;
            }
            if(vertices.Count>0){
                var mesh=new Mesh{name="Crater continuous hexagonal organ pipes",indexFormat=IndexFormat.UInt32};
                mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();owner.meshes.Add(mesh);
                var go=new GameObject("POSTCARD_CRATER_HEX_RIDGES");go.transform.SetParent(model.transform,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;
                var render=go.AddComponent<MeshRenderer>();render.sharedMaterial=owner.material;render.shadowCastingMode=ShadowCastingMode.Off;render.receiveShadows=true;
            }
            Debug.Log($"[GolfPostcardBasalt] {owner.Columns} hex columns in one geological draw; collision and obstacle sources retained");
        }
        void Column(Transform root,List<Vector3> v,List<Vector2> uv,List<int> t,Vector3 bottom,Vector3 along,Vector3 across,float width,float depth,float height)
        {
            for(int face=0;face<6;face++){
                float a=face*Mathf.PI/3,b=(face+1)*Mathf.PI/3;
                Vector3 A(float angle,float y,float taper)=>root.InverseTransformPoint(bottom+along*(Mathf.Cos(angle)*width*taper)+across*(Mathf.Sin(angle)*depth*taper)+Vector3.up*y);
                int start=v.Count;
                v.Add(A(a,0,1));v.Add(A(b,0,1));v.Add(A(b,height*.965f,1));v.Add(A(a,height*.965f,1));
                uv.AddRange(new[]{new Vector2(0,0),new Vector2(1,0),new Vector2(1,.965f),new Vector2(0,.965f)});
                t.AddRange(new[]{start,start+1,start+2,start,start+2,start+3});
                start=v.Count;v.Add(A(a,height*.965f,1));v.Add(A(b,height*.965f,1));v.Add(A(b,height,.87f));v.Add(A(a,height,.87f));
                uv.AddRange(new[]{new Vector2(.1f,.965f),new Vector2(.9f,.965f),new Vector2(.9f,1),new Vector2(.1f,1)});
                t.AddRange(new[]{start,start+1,start+2,start,start+2,start+3});
                start=v.Count;v.Add(A(a,height,.87f));v.Add(A(b,height,.87f));v.Add(root.InverseTransformPoint(bottom+Vector3.up*height));
                uv.AddRange(new[]{Vector2.one,Vector2.one,Vector2.one});t.AddRange(new[]{start,start+1,start+2});
            }
            Columns++;
        }
        void OnDestroy(){foreach(var mesh in meshes)if(mesh)Destroy(mesh);if(material)Destroy(material);}
    }
}
