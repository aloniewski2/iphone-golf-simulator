using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace GolfArcade.Course
{
    /// Render-only distance field from the existing mesh outlines. Never changes
    /// a Mesh, collider, transform, course point or scoring value.
    public static class GolfSurfaceEdges
    {
        const int Size=256;
        struct Segment { public Vector2 a,b; }
        static Texture2D active;
        static Texture2D neutral;
        public static void Reset()
        {
            if(!neutral) {
                neutral=new Texture2D(1,1,TextureFormat.RGBA32,false,true){name="Golf neutral surface edges"};
                neutral.SetPixel(0,0,new Color(1,1,.5f,1));neutral.Apply(false,true);
            }
            Shader.SetGlobalTexture("_GolfEdgeMap",neutral);
            Shader.SetGlobalVector("_GolfEdgeBounds",Vector4.zero);
        }
        static float Smooth(float a,float b,float x){float t=Mathf.Clamp01((x-a)/(b-a));return t*t*(3-2*t);}
        static byte Byte(float x)=>(byte)Mathf.RoundToInt(Mathf.Clamp01(x)*255);
        static string Point(Vector3 p) => $"{Mathf.RoundToInt(p.x*1000)}:{Mathf.RoundToInt(p.z*1000)}";
        public static void Apply(GameObject model)
        {
            var lines=new List<Segment>[] {new(),new(),new(),new()};
            var lo=new Vector2(float.MaxValue,float.MaxValue);var hi=new Vector2(float.MinValue,float.MinValue);
            foreach(var mf in model.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh=mf.sharedMesh;if(!mesh||!mesh.isReadable)continue;
                string name=mf.name;int kind=name.StartsWith("GREEN")?0:name.StartsWith("FAIRWAY")?1:
                    name.StartsWith("BUNKER")||name.StartsWith("DRESS_SAND")?2:name.StartsWith("WATER_OCEAN")||name.StartsWith("WATER_SHELF")?3:-1;
                if(kind<0)continue;
                var p=mesh.vertices;for(int i=0;i<p.Length;i++)p[i]=mf.transform.TransformPoint(p[i]);
                var counts=new Dictionary<string,int>();var segs=new Dictionary<string,Segment>();var tri=mesh.triangles;
                for(int j=0;j<tri.Length;j+=3)
                {
                    Vector3 a=p[tri[j]],b=p[tri[j+1]],c=p[tri[j+2]];
                    if(Vector3.Cross(b-a,c-a).normalized.y<.85f)continue;
                    var t=new[]{a,b,c};
                    for(int k=0;k<3;k++)
                    {
                        var x=t[k];var y=t[(k+1)%3];string u=Point(x),v=Point(y);
                        if(u==v)continue;string key=string.CompareOrdinal(u,v)<0?u+"/"+v:v+"/"+u;
                        counts.TryGetValue(key,out int count);counts[key]=count+1;
                        segs[key]=new Segment{a=new Vector2(x.x,x.z),b=new Vector2(y.x,y.z)};
                        if(kind<3){lo=Vector2.Min(lo,new Vector2(x.x,x.z));hi=Vector2.Max(hi,new Vector2(x.x,x.z));}
                    }
                }
                foreach(var e in counts)if(e.Value==1)lines[kind].Add(segs[e.Key]);
            }
            if(lo.x==float.MaxValue){Reset();return;}
            lo-=Vector2.one*4;hi+=Vector2.one*4;var extent=hi-lo;var origin=lo;
            var pixels=new Color32[Size*Size];
            Parallel.For(0,Size,y=>{
                for(int x=0;x<Size;x++)
                {
                    var p=origin+new Vector2((x+.5f)/Size*extent.x,(y+.5f)/Size*extent.y);
                    var distances=new float[4];
                    for(int k=0;k<4;k++)
                    {
                        float best=32*32;
                        foreach(var s in lines[k])
                        {
                            var d=s.b-s.a;float den=Vector2.Dot(d,d);
                            float t=den>0?Mathf.Clamp01(Vector2.Dot(p-s.a,d)/den):0;
                            best=Mathf.Min(best,(p-s.a-d*t).sqrMagnitude);
                        }
                        distances[k]=Mathf.Sqrt(best);
                    }
                    // Preintegrate the smooth outline transitions once per hole; the
                    // fragment shader only samples the resulting multipliers.
                    float apron=1-.045f*(1-Smooth(.3f,1.8f,distances[1]));
                    float fringe=1-.085f*(1-Smooth(.6f,2.4f,distances[0]));
                    float sand=1+.10f*(1-Smooth(.3f,1.8f,distances[2]))-.12f*(1-Smooth(.5f,5f,distances[3]));
                    pixels[y*Size+x]=new Color32(Byte(apron*fringe),Byte(apron),Byte(sand*.5f),255);
                }
            });
            if(active)UnityEngine.Object.Destroy(active);
            active=new Texture2D(Size,Size,TextureFormat.RGBA32,false,true){name="Golf original outline factors",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
            active.SetPixels32(pixels);active.Apply(false,true);
            Shader.SetGlobalTexture("_GolfEdgeMap",active);
            Shader.SetGlobalVector("_GolfEdgeBounds",new Vector4(lo.x,lo.y,1/extent.x,1/extent.y));
            Debug.Log($"[GolfSurfaceEdges] original top-boundary segments {lines[0].Count}/{lines[1].Count}/{lines[2].Count}/{lines[3].Count}; {Size}x{Size}, 262144 bytes, geometry unchanged");
        }
    }
}
