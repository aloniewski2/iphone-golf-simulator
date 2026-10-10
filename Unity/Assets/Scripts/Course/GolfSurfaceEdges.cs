using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace GolfArcade.Course
{
    /// Render-only distance field from the existing mesh outlines. Never changes
    /// a Mesh, collider, transform, course point or scoring value.
    ///
    /// Build cost notes (the pixels, the bounds and the per-kind segment lists are bit-identical to the
    /// original per-triangle string-key / brute-force version; Assets/Editor/SurfaceEdgesCheck.cs keeps that
    /// version as a reference and compares every hole):
    ///  - outline edges are grouped by an integer key (welded rounded-millimetre point ids, ordered pair) instead
    ///    of "x:z/x:z" strings, in the same first-seen order as the old Dictionary enumeration;
    ///  - each distance is the same float expression as before (Vector2 operators, so a JIT that fuses multiply-adds
    ///    rounds old and new alike), but only evaluated against segments that can still lower the clamped minimum:
    ///    bounding-box lower bounds per 16x16 and per 4x4 pixel block, then per pixel against the running minimum;
    ///  - a second Apply on a model whose contributing meshes, transforms and published textures are unchanged
    ///    returns at once (GolfLook.DressModel and GolfCourseLook.FinishModel both call it on postcard holes).
    public static class GolfSurfaceEdges
    {
        const int Size=256;
        const float Clamp=32;                       // yards: distances are clamped here (best starts at Clamp*Clamp)
        const float Reach=Clamp+.05f;               // pruning radius: the clamp plus float slack
        const float Slack=.05f;                     // yards added to a distance before it is compared with a lower bound (>> its float error)
        const int Block=16,Sub=4,Blocks=Size/Block,Subs=Block/Sub;

        static Texture2D active;
        static Texture2D neutral;
        static Signature applied;                   // what `active` was built from (null: unknown / reset)
        static Vector4 appliedBounds;

#if UNITY_EDITOR
        /// Editor verification hook (Assets/Editor/SurfaceEdgesCheck.cs): called at the start of every Apply.
        public static Action<GameObject> BeforeApplyForTests;
#endif

        public static void Reset()
        {
            applied=null;
            if(!neutral) {
                neutral=new Texture2D(1,1,TextureFormat.RGBA32,false,true){name="Golf neutral surface edges"};
                neutral.SetPixel(0,0,new Color(1,1,.5f,1));neutral.Apply(false,true);
            }
            Shader.SetGlobalTexture("_GolfEdgeMap",neutral);
            Shader.SetGlobalVector("_GolfEdgeBounds",Vector4.zero);
        }
        static float Smooth(float a,float b,float x){float t=Mathf.Clamp01((x-a)/(b-a));return t*t*(3-2*t);}
        static byte Byte(float x)=>(byte)Mathf.RoundToInt(Mathf.Clamp01(x)*255);

        public static void Apply(GameObject model)
        {
#if UNITY_EDITOR
            BeforeApplyForTests?.Invoke(model);
#endif
            var found=Collect(model);
            var signature=Signature.Of(model,found);
            if(applied!=null&&applied.Equals(signature)&&StillPublished())return;   // same model, same meshes: the maps are already set
            var field=Compute(found,false);
            if(field==null){Reset();return;}
            if(active)UnityEngine.Object.Destroy(active);
            active=new Texture2D(Size,Size,TextureFormat.RGBA32,false,true){name="Golf original outline factors",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
            active.SetPixels32(field.Pixels);active.Apply(false,true);
            Shader.SetGlobalTexture("_GolfEdgeMap",active);
            Shader.SetGlobalVector("_GolfEdgeBounds",field.Bounds);
            applied=signature;appliedBounds=field.Bounds;
            var c=field.SegmentCounts;
            Debug.Log($"[GolfSurfaceEdges] original top-boundary segments {c[0]}/{c[1]}/{c[2]}/{c[3]}; {Size}x{Size}, 262144 bytes, geometry unchanged");
        }

        /// True while the textures a previous Apply published are still the ones the shaders see.
        static bool StillPublished()
        {
            if(!active||Shader.GetGlobalTexture("_GolfEdgeMap")!=active)return false;
            var b=Shader.GetGlobalVector("_GolfEdgeBounds");
            return b.x==appliedBounds.x&&b.y==appliedBounds.y&&b.z==appliedBounds.z&&b.w==appliedBounds.w;
        }

        // ---------------------------------------------------------------------------------------------------
        // What a model contributes, and the "did anything change" signature built from it

        struct Contributor { public MeshFilter filter; public Mesh mesh; public int kind; }

        static List<Contributor> Collect(GameObject model)
        {
            var list=new List<Contributor>();
            foreach(var mf in model.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh=mf.sharedMesh;if(!mesh||!mesh.isReadable)continue;
                string name=mf.name;int kind=name.StartsWith("GREEN")?0:name.StartsWith("FAIRWAY")?1:
                    name.StartsWith("BUNKER")||name.StartsWith("DRESS_SAND")?2:name.StartsWith("WATER_OCEAN")||name.StartsWith("WATER_SHELF")?3:-1;
                if(kind<0)continue;
                list.Add(new Contributor{filter=mf,mesh=mesh,kind=kind});
            }
            return list;
        }

        /// The GREEN / FAIRWAY / BUNKER / DRESS_SAND / WATER_OCEAN / WATER_SHELF mesh filters Apply reads: which
        /// filter, which mesh instance, its vertex and index counts, the kind and the full local-to-world matrix.
        sealed class Signature
        {
            int model;int[] ids;float[] matrices;
            public static Signature Of(GameObject model,List<Contributor> found)
            {
                var s=new Signature{model=model.GetInstanceID(),ids=new int[found.Count*5],matrices=new float[found.Count*16]};
                for(int i=0;i<found.Count;i++)
                {
                    var f=found[i];var m=f.mesh;long indices=0;
                    for(int sub=0;sub<m.subMeshCount;sub++)indices+=m.GetIndexCount(sub);
                    s.ids[i*5]=f.filter.GetInstanceID();s.ids[i*5+1]=m.GetInstanceID();s.ids[i*5+2]=m.vertexCount;s.ids[i*5+3]=(int)indices;s.ids[i*5+4]=f.kind;
                    Matrix4x4 w=f.filter.transform.localToWorldMatrix;
                    for(int e=0;e<16;e++)s.matrices[i*16+e]=w[e];
                }
                return s;
            }
            public bool Equals(Signature o)
            {
                if(o==null||model!=o.model||ids.Length!=o.ids.Length)return false;
                for(int i=0;i<ids.Length;i++)if(ids[i]!=o.ids[i])return false;
                for(int i=0;i<matrices.Length;i++)if(BitConverter.SingleToInt32Bits(matrices[i])!=BitConverter.SingleToInt32Bits(o.matrices[i]))return false;
                return true;
            }
        }

        // ---------------------------------------------------------------------------------------------------
        // The pure computation (no globals touched)

        /// Result of the pure part of Apply. Public so the editor equality check can compare it with the original.
        public sealed class Field
        {
            public Color32[] Pixels;                // Size*Size, row-major, y up in world z
            public Vector4 Bounds;                  // _GolfEdgeBounds
            public int[] SegmentCounts;             // boundary segments per kind: green / fairway / sand / water
            public float[] Distances;               // debug only: 4*Size*Size clamped distances, kind-major
            public float[][] Segments;              // debug only: per kind a.x,a.y,b.x,b.y of every segment, in scan order
            public double ScanMs,FieldMs;           // time spent reading the meshes / filling the 256x256 map
        }

        /// The edge map of `model` without touching any global; null when no green/fairway/sand mesh contributes
        /// (Apply then resets to the neutral map). `debug` also returns every distance and segment.
        public static Field Compute(GameObject model,bool debug=false)=>Compute(Collect(model),debug);

        static Field Compute(List<Contributor> found,bool debug)
        {
            long start=System.Diagnostics.Stopwatch.GetTimestamp();
            var lines=new SegBuf[]{new(),new(),new(),new()};
            var lo=new Vector2(float.MaxValue,float.MaxValue);var hi=new Vector2(float.MinValue,float.MinValue);
            foreach(var c in found)
            {
                var tr=c.filter.transform;
                var p=c.mesh.vertices;for(int i=0;i<p.Length;i++)p[i]=tr.TransformPoint(p[i]);
                Scan(p,c.mesh.triangles,c.kind,lines[c.kind],ref lo,ref hi);
            }
            double scanMs=(System.Diagnostics.Stopwatch.GetTimestamp()-start)*1000.0/System.Diagnostics.Stopwatch.Frequency;
            var field=Finish(lines,lo,hi,debug);
            if(field!=null){field.ScanMs=scanMs;field.FieldMs=(System.Diagnostics.Stopwatch.GetTimestamp()-start)*1000.0/System.Diagnostics.Stopwatch.Frequency-scanMs;}
            return field;
        }

        /// A mesh already in world space; `Kind` 0 green, 1 fairway, 2 sand, 3 water.
        public struct WorldMesh { public Vector3[] Positions;public int[] Triangles;public int Kind; }

        /// Same as Compute(GameObject) for meshes given in world space (used by the EditMode equality tests).
        public static Field ComputeWorld(IList<WorldMesh> meshes,bool debug=false)
        {
            var lines=new SegBuf[]{new(),new(),new(),new()};
            var lo=new Vector2(float.MaxValue,float.MaxValue);var hi=new Vector2(float.MinValue,float.MinValue);
            foreach(var m in meshes)Scan(m.Positions,m.Triangles,m.Kind,lines[m.Kind],ref lo,ref hi);
            return Finish(lines,lo,hi,debug);
        }

        static Field Finish(SegBuf[] lines,Vector2 lo,Vector2 hi,bool debug)
        {
            if(lo.x==float.MaxValue)return null;
            lo-=Vector2.one*4;hi+=Vector2.one*4;var extent=hi-lo;var origin=lo;
            var field=new Field{Pixels=new Color32[Size*Size],Bounds=new Vector4(lo.x,lo.y,1/extent.x,1/extent.y),SegmentCounts=new[]{lines[0].n,lines[1].n,lines[2].n,lines[3].n}};
            if(debug){field.Distances=new float[4*Size*Size];field.Segments=new float[4][];for(int k=0;k<4;k++)field.Segments[k]=lines[k].Flatten();}
            Distance(lines,origin,extent,field);
            return field;
        }

        // ----- scan: boundary segments of one mesh ----------------------------------------------------------

        sealed class SegBuf
        {
            public float[] ax=new float[64],ay=new float[64],bx=new float[64],by=new float[64];public int n;
            public void Add(float a0,float a1,float b0,float b1)
            {
                if(n==ax.Length){int c=n*2;Array.Resize(ref ax,c);Array.Resize(ref ay,c);Array.Resize(ref bx,c);Array.Resize(ref by,c);}
                ax[n]=a0;ay[n]=a1;bx[n]=b0;by[n]=b1;n++;
            }
            public float[] Flatten(){var r=new float[n*4];for(int i=0;i<n;i++){r[i*4]=ax[i];r[i*4+1]=ay[i];r[i*4+2]=bx[i];r[i*4+3]=by[i];}return r;}
        }

        /// Open-addressing long -> int map (insert-only); value+1 is stored so 0 marks an empty slot.
        sealed class LongTable
        {
            long[] keys;int[] vals;int mask,count;
            public LongTable(int minCapacity){int c=16;while(c<minCapacity)c<<=1;keys=new long[c];vals=new int[c];mask=c-1;}
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static int Slot(long k)=>(int)(((ulong)k*0x9E3779B97F4A7C15UL)>>32);
            /// The value stored for `key`, or `value` (now stored) when the key was new.
            public int GetOrAdd(long key,int value)
            {
                if((count+1)*10>keys.Length*7)Grow();
                int h=Slot(key)&mask;
                while(true)
                {
                    int v=vals[h];
                    if(v==0){keys[h]=key;vals[h]=value+1;count++;return value;}
                    if(keys[h]==key)return v-1;
                    h=(h+1)&mask;
                }
            }
            void Grow()
            {
                var ok=keys;var ov=vals;int c=ok.Length*2;keys=new long[c];vals=new int[c];mask=c-1;
                for(int i=0;i<ok.Length;i++)if(ov[i]!=0){int h=Slot(ok[i])&mask;while(vals[h]!=0)h=(h+1)&mask;keys[h]=ok[i];vals[h]=ov[i];}
            }
        }

        /// Insert-only edge table: how often each undirected edge (welded point ids, smaller id first) occurs and
        /// where it first occurred. `seen` saturates at 2; 0 marks an empty slot.
        sealed class EdgeTable
        {
            long[] keys;int[] first;byte[] seen;int mask,count;
            public EdgeTable(int minCapacity){int c=16;while(c<minCapacity)c<<=1;keys=new long[c];first=new int[c];seen=new byte[c];mask=c-1;}
            public void Add(long key,int occurrence)
            {
                if((count+1)*10>keys.Length*7)Grow();
                int h=((int)(((ulong)key*0x9E3779B97F4A7C15UL)>>32))&mask;
                while(true)
                {
                    if(seen[h]==0){keys[h]=key;first[h]=occurrence;seen[h]=1;count++;return;}
                    if(keys[h]==key){seen[h]=2;return;}
                    h=(h+1)&mask;
                }
            }
            /// First-occurrence indices of the edges that occurred exactly once, ascending.
            public int[] Singles()
            {
                int n=0;for(int i=0;i<seen.Length;i++)if(seen[i]==1)n++;
                var r=new int[n];n=0;for(int i=0;i<seen.Length;i++)if(seen[i]==1)r[n++]=first[i];
                Array.Sort(r);return r;
            }
            void Grow()
            {
                var ok=keys;var of=first;var os=seen;int c=ok.Length*2;keys=new long[c];first=new int[c];seen=new byte[c];mask=c-1;
                for(int i=0;i<ok.Length;i++)if(os[i]!=0)
                {
                    int h=((int)(((ulong)ok[i]*0x9E3779B97F4A7C15UL)>>32))&mask;while(seen[h]!=0)h=(h+1)&mask;
                    keys[h]=ok[i];first[h]=of[i];seen[h]=os[i];
                }
            }
        }

        /// `p` are the mesh's vertices in world space.
        static void Scan(Vector3[] p,int[] tri,int kind,SegBuf output,ref Vector2 lo,ref Vector2 hi)
        {
            // Pass 1: keep the upward triangles and give every vertex they use a welded (1 mm) point id.
            var points=new LongTable(p.Length*3/2+16);var point=new int[p.Length];   // point id + 1 per vertex, 0 = not yet looked up
            var kept=new int[tri.Length/3];int keptCount=0,pointCount=0;
            for(int j=0;j<tri.Length;j+=3)
            {
                int i0=tri[j],i1=tri[j+1],i2=tri[j+2];
                Vector3 a=p[i0],b=p[i1],c=p[i2];
                if(Vector3.Cross(b-a,c-a).normalized.y<.85f)continue;
                if(point[i0]==0){int u=points.GetOrAdd(PointKey(a),pointCount);if(u==pointCount)pointCount++;point[i0]=u+1;}
                if(point[i1]==0){int u=points.GetOrAdd(PointKey(b),pointCount);if(u==pointCount)pointCount++;point[i1]=u+1;}
                if(point[i2]==0){int u=points.GetOrAdd(PointKey(c),pointCount);if(u==pointCount)pointCount++;point[i2]=u+1;}
                kept[keptCount++]=j;
            }
            // Pass 2: count the undirected edges (a shared edge is seen twice, an outline edge once).
            var edges=new EdgeTable(keptCount*3);
            for(int n=0;n<keptCount;n++)
            {
                int j=kept[n];int i0=tri[j],i1=tri[j+1],i2=tri[j+2];
                int u0=point[i0]-1,u1=point[i1]-1,u2=point[i2]-1;
                if(u0!=u1)
                {
                    edges.Add(u0<u1?((long)u0<<32)|(uint)u1:((long)u1<<32)|(uint)u0,j);
                    if(kind<3){lo=new Vector2(Mathf.Min(lo.x,p[i0].x),Mathf.Min(lo.y,p[i0].z));hi=new Vector2(Mathf.Max(hi.x,p[i0].x),Mathf.Max(hi.y,p[i0].z));}
                }
                if(u1!=u2)
                {
                    edges.Add(u1<u2?((long)u1<<32)|(uint)u2:((long)u2<<32)|(uint)u1,j+1);
                    if(kind<3){lo=new Vector2(Mathf.Min(lo.x,p[i1].x),Mathf.Min(lo.y,p[i1].z));hi=new Vector2(Mathf.Max(hi.x,p[i1].x),Mathf.Max(hi.y,p[i1].z));}
                }
                if(u2!=u0)
                {
                    edges.Add(u2<u0?((long)u2<<32)|(uint)u0:((long)u0<<32)|(uint)u2,j+2);
                    if(kind<3){lo=new Vector2(Mathf.Min(lo.x,p[i2].x),Mathf.Min(lo.y,p[i2].z));hi=new Vector2(Mathf.Max(hi.x,p[i2].x),Mathf.Max(hi.y,p[i2].z));}
                }
            }
            // The outline: edges seen once, in the order they first appeared (a triangle's edge k runs from corner k to corner k+1).
            foreach(int occurrence in edges.Singles())
            {
                int j=occurrence/3*3,k=occurrence-j;
                var x=p[tri[j+k]];var y=p[tri[j+(k+1)%3]];
                output.Add(x.x,x.z,y.x,y.z);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static long PointKey(Vector3 p)=>((long)Mathf.RoundToInt(p.x*1000)<<32)|(uint)Mathf.RoundToInt(p.z*1000);

        // ----- distance field ------------------------------------------------------------------------------

        /// One kind's segments laid out for the search: start, delta, squared length and bounding box.
        sealed class Kind
        {
            public readonly int n;
            public readonly Vector2[] a,d;
            public readonly float[] den,x0,x1,y0,y1;
            public Kind(SegBuf s)
            {
                n=s.n;a=new Vector2[n];d=new Vector2[n];den=new float[n];x0=new float[n];x1=new float[n];y0=new float[n];y1=new float[n];
                for(int i=0;i<n;i++)
                {
                    var sa=new Vector2(s.ax[i],s.ay[i]);var sb=new Vector2(s.bx[i],s.by[i]);
                    a[i]=sa;d[i]=sb-sa;den[i]=Vector2.Dot(d[i],d[i]);
                    x0[i]=Mathf.Min(sa.x,sb.x);x1[i]=Mathf.Max(sa.x,sb.x);y0[i]=Mathf.Min(sa.y,sb.y);y1[i]=Mathf.Max(sa.y,sb.y);
                }
            }
        }

        /// Squared distance from p to segment i. The expression is the original loop's, operator for operator (Vector2
        /// maths, not hand-expanded scalars): a JIT that fuses multiply-adds (Mono on arm64 does) then fuses both
        /// versions the same way, so the floats come out identical bit for bit.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static float Sq(Kind k,int i,Vector2 p)
        {
            var a=k.a[i];var d=k.d[i];float den=k.den[i];
            float t=den>0?Mathf.Clamp01(Vector2.Dot(p-a,d)/den):0;
            return (p-a-d*t).sqrMagnitude;
        }

        sealed class Scratch
        {
            public readonly int[][] outer=new int[4][],inner=new int[4][];public readonly float[][] innerLb=new float[4][];public readonly int[] outerN=new int[4],innerN=new int[4];
            public Scratch(Kind[] kinds)
            {
                for(int k=0;k<4;k++){outer[k]=new int[kinds[k].n];inner[k]=new int[kinds[k].n];innerLb[k]=new float[kinds[k].n];}
            }
        }

        static void Distance(SegBuf[] lines,Vector2 origin,Vector2 extent,Field field)
        {
            var kinds=new Kind[4];for(int k=0;k<4;k++)kinds[k]=new Kind(lines[k]);
            var px=new float[Size];var py=new float[Size];
            for(int i=0;i<Size;i++){var p=origin+new Vector2((i+.5f)/Size*extent.x,(i+.5f)/Size*extent.y);px[i]=p.x;py[i]=p.y;}
            var pixels=field.Pixels;var distances=field.Distances;
            int next=-1;int workers=Math.Max(1,Math.Min(Environment.ProcessorCount,Blocks*Blocks));
            Parallel.For(0,workers,_=>{
                var scratch=new Scratch(kinds);int block;
                while((block=Interlocked.Increment(ref next))<Blocks*Blocks)Process(block%Blocks,block/Blocks,kinds,px,py,scratch,pixels,distances);
            });
        }

        static void Process(int bx,int by,Kind[] kinds,float[] px,float[] py,Scratch s,Color32[] pixels,float[] distances)
        {
            int xs=bx*Block,ys=by*Block;
            float rx0=px[xs],rx1=px[xs+Block-1],ry0=py[ys],ry1=py[ys+Block-1];
            for(int k=0;k<4;k++)s.outerN[k]=Gather(kinds[k],rx0,rx1,ry0,ry1,s.outer[k]);
            for(int sy=0;sy<Subs;sy++)for(int sx=0;sx<Subs;sx++)
            {
                int x0=xs+sx*Sub,y0=ys+sy*Sub;
                float cx0=px[x0],cx1=px[x0+Sub-1],cy0=py[y0],cy1=py[y0+Sub-1];
                for(int k=0;k<4;k++)s.innerN[k]=Refine(kinds[k],s.outer[k],s.outerN[k],cx0,cx1,cy0,cy1,s.inner[k],s.innerLb[k]);
                for(int y=y0;y<y0+Sub;y++)for(int x=x0;x<x0+Sub;x++)
                {
                    var p=new Vector2(px[x],py[y]);
                    float d0=Nearest(kinds[0],s.inner[0],s.innerLb[0],s.innerN[0],p),d1=Nearest(kinds[1],s.inner[1],s.innerLb[1],s.innerN[1],p);
                    float d2=Nearest(kinds[2],s.inner[2],s.innerLb[2],s.innerN[2],p),d3=Nearest(kinds[3],s.inner[3],s.innerLb[3],s.innerN[3],p);
                    if(distances!=null){int at=y*Size+x;distances[at]=d0;distances[Size*Size+at]=d1;distances[2*Size*Size+at]=d2;distances[3*Size*Size+at]=d3;}
                    // Preintegrate the smooth outline transitions once per hole; the
                    // fragment shader only samples the resulting multipliers.
                    float apron=1-.045f*(1-Smooth(.3f,1.8f,d1));
                    float fringe=1-.085f*(1-Smooth(.6f,2.4f,d0));
                    float sand=1+.10f*(1-Smooth(.3f,1.8f,d2))-.12f*(1-Smooth(.5f,5f,d3));
                    pixels[y*Size+x]=new Color32(Byte(apron*fringe),Byte(apron),Byte(sand*.5f),255);
                }
            }
        }

        /// Segments whose bounding box comes within Reach of the pixel rectangle: the only ones that can be closer than the clamp.
        static int Gather(Kind k,float rx0,float rx1,float ry0,float ry1,int[] list)
        {
            float lx=rx0-Reach,hx=rx1+Reach,ly=ry0-Reach,hy=ry1+Reach;int m=0;
            for(int i=0;i<k.n;i++)if(k.x0[i]<=hx&&k.x1[i]>=lx&&k.y0[i]<=hy&&k.y1[i]>=ly)list[m++]=i;
            return m;
        }

        /// Narrows `from` for a smaller pixel rectangle: drops segments whose bounding box is farther than Reach from it (they
        /// are beyond the clamp for every pixel inside) and keeps, per kept segment, that bounding-box distance squared as
        /// a lower bound on its distance to any pixel of the rectangle. The nearest-looking one goes first.
        static int Refine(Kind k,int[] from,int count,float rx0,float rx1,float ry0,float ry1,int[] to,float[] toLb)
        {
            const float limit=Reach*Reach;int r=0,arg=0;float least=float.MaxValue;
            for(int j=0;j<count;j++)
            {
                int i=from[j];
                float gx=Mathf.Max(0,Mathf.Max(k.x0[i]-rx1,rx0-k.x1[i])),gy=Mathf.Max(0,Mathf.Max(k.y0[i]-ry1,ry0-k.y1[i]));
                float g=gx*gx+gy*gy;
                if(g>limit)continue;
                to[r]=i;toLb[r]=g;if(g<least){least=g;arg=r;}r++;
            }
            if(arg>0){int ti=to[0];to[0]=to[arg];to[arg]=ti;float tl=toLb[0];toLb[0]=toLb[arg];toLb[arg]=tl;}
            return r;
        }

        /// Clamped distance from p to the nearest of the listed segments, the original running minimum over the very
        /// same per-segment floats. A segment is skipped without evaluating it only when its lower bound already
        /// exceeds the best distance so far plus Slack, so it could not have lowered the minimum.
        static float Nearest(Kind k,int[] list,float[] lower,int count,Vector2 p)
        {
            float best=32*32,limit=Reach*Reach;
            for(int j=0;j<count;j++)
            {
                if(lower[j]>limit)continue;
                float now=Mathf.Min(best,Sq(k,list[j],p));
                if(now!=best){best=now;float reach=Mathf.Sqrt(best)+Slack;limit=reach*reach;}
            }
            return Mathf.Sqrt(best);
        }
    }
}
