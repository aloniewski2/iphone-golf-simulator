using System;using System.Collections.Generic;using System.Linq;using UnityEngine;
namespace GolfArcade.Tennis {
 /// Same authored quad construction, fitted to the actual imported Body aperture.
 /// Skin-space head bind -> rigid Face local is exact; no bound-registration guess.
 public static class HeroActualLidFit {
  static readonly Dictionary<Mesh,Mesh> blinkCache=new();
  struct Edge:IEquatable<Edge>{public int a,b;public Edge(int x,int y){a=Mathf.Min(x,y);b=Mathf.Max(x,y);}public bool Equals(Edge o)=>a==o.a&&b==o.b;public override bool Equals(object o)=>o is Edge e&&Equals(e);public override int GetHashCode()=>a*397^b;}
  struct Point{public Vector3 p,n;public Point(Vector3 v,Vector3 normal){p=v;n=normal;}}
  public static bool Build(MatchHeroLook hero,out Vector3[] positions,out Vector3[] normals,out int[] triangles){
   positions=null;normals=null;triangles=null;if(!hero.body||!hero.face)return false;
   var mesh=hero.body.sharedMesh;int head=Array.FindIndex(hero.body.bones,b=>b&&b.name=="Head");if(head<0)return false;
   var map=hero.face.transform.worldToLocalMatrix*hero.body.bones[head].localToWorldMatrix*mesh.bindposes[head];var nm=map.inverse.transpose;
   var vertices=mesh.vertices;var sourceNormals=mesh.normals;var welded=new List<Point>();var ids=new int[vertices.Length];var keys=new Dictionary<Vector3Int,int>();
   for(int v=0;v<vertices.Length;v++){
    var p=map.MultiplyPoint3x4(vertices[v]);var n=nm.MultiplyVector(sourceNormals[v]).normalized;var k=new Vector3Int(Mathf.RoundToInt(p.x*100000),Mathf.RoundToInt(p.y*100000),Mathf.RoundToInt(p.z*100000));
    if(!keys.TryGetValue(k,out int id)){id=welded.Count;keys[k]=id;welded.Add(new Point(p,n));}ids[v]=id;
   }
   var edges=new Dictionary<Edge,int>();for(int sub=0;sub<mesh.subMeshCount;sub++){var t=mesh.GetTriangles(sub);for(int i=0;i<t.Length;i+=3)for(int e=0;e<3;e++){var edge=new Edge(ids[t[i+e]],ids[t[i+(e+1)%3]]);if(edge.a==edge.b)continue;if(!edges.ContainsKey(edge))edges[edge]=0;edges[edge]++;}}
   var adjacency=new Dictionary<int,List<int>>();foreach(var e in edges.Where(e=>e.Value==1).Select(e=>e.Key)){if(!adjacency.ContainsKey(e.a))adjacency[e.a]=new();if(!adjacency.ContainsKey(e.b))adjacency[e.b]=new();adjacency[e.a].Add(e.b);adjacency[e.b].Add(e.a);}
   HeroFaceBasis.Get(hero,out var right,out var up,out var forward);
   var eye=hero.face.GetComponent<MeshFilter>().sharedMesh;var mats=hero.face.sharedMaterials;var eyePoints=new List<Vector3>();for(int sub=0;sub<mats.Length;sub++)if(mats[sub]&&mats[sub].name.Contains("Sclera"))eyePoints.AddRange(eye.GetTriangles(sub).Select(i=>eye.vertices[i]));
   if(eyePoints.Count==0)return false;float eyeUp=eyePoints.Average(p=>Vector3.Dot(p,up));
   var remaining=new HashSet<int>(adjacency.Keys);var loops=new List<List<Point>>();
   while(remaining.Count>0){var todo=new Stack<int>();todo.Push(remaining.First());var loop=new List<Point>();while(todo.Count>0){int v=todo.Pop();if(!remaining.Remove(v))continue;loop.Add(welded[v]);foreach(int n in adjacency[v])todo.Push(n);}if(loop.Count>20&&Mathf.Abs(loop.Average(p=>Vector3.Dot(p.p,up))-eyeUp)<.04f)loops.Add(loop);}
   if(hero.golfKit&&hero.female)foreach(var loop in loops){var centre=loop.Aggregate(Vector3.zero,(sum,p)=>sum+p.p)/loop.Count;var b=new Bounds(loop[0].p,Vector3.zero);foreach(var p in loop)b.Encapsulate(p.p);Debug.Log("[HeroGolfEyeBoundary] count="+loop.Count+" FaceLocal centre="+centre.ToString("F6")+" bounds="+b+" EyeUp="+eyeUp+" EyeWorldCentre="+hero.face.transform.TransformPoint(eyePoints.Aggregate(Vector3.zero,(sum,p)=>sum+p)/eyePoints.Count));}
   if(loops.Count>2){
    // Some detailed golf bodies expose an additional small facial boundary near
    // eye height. Register true apertures to the actual two sclera centres.
    float midX=eyePoints.Average(p=>Vector3.Dot(p,right));
    var targets=new[]{eyePoints.Where(p=>Vector3.Dot(p,right)<midX).ToArray(),eyePoints.Where(p=>Vector3.Dot(p,right)>=midX).ToArray()}.Select(points=>points.Aggregate(Vector3.zero,(sum,p)=>sum+p)/points.Length).ToArray();
    var picked=new List<List<Point>>();
    foreach(var target in targets){if(hero.golfKit&&hero.female)Debug.Log("[HeroGolfEyeTarget] FaceLocal="+target.ToString("F6"));var nearest=loops.Where(loop=>!picked.Contains(loop)).OrderBy(loop=>(loop.Aggregate(Vector3.zero,(sum,p)=>sum+p.p)/loop.Count-target).sqrMagnitude).FirstOrDefault();if(nearest==null)break;var centre=nearest.Aggregate(Vector3.zero,(sum,p)=>sum+p.p)/nearest.Count;if(Vector3.Distance(centre,target)>.035f)break;picked.Add(nearest);}
    if(picked.Count==2){Debug.Log("[HeroActualLidFit] registered2eyeapertures from"+loops.Count+" boundaries against actual sclera");loops=picked;}
   }
   if(loops.Count!=2){Debug.LogWarning("[HeroActualLidFit] actual welded aperture loops="+loops.Count+"; retain authored fallback");return false;}
   PrepareBodyClosure(hero,loops,map,right,up,forward);
   var outP=new List<Vector3>();var outN=new List<Vector3>();var outT=new List<int>();
   foreach(var loop in loops.OrderBy(l=>l.Average(p=>Vector3.Dot(p.p,right)))){
    // Change only coordinate basis; source normals remain the adjacent actual skin.
    var contour=loop.Select(p=>new Point(new Vector3(Vector3.Dot(p.p,right),Vector3.Dot(p.p,up),Vector3.Dot(p.p,forward)),new Vector3(Vector3.Dot(p.n,right),Vector3.Dot(p.n,up),Vector3.Dot(p.n,forward)))).ToList();
    float cx=contour.Average(p=>p.p.x),cy=contour.Average(p=>p.p.y);contour=contour.OrderBy(p=>Mathf.Atan2(p.p.y-cy,p.p.x-cx)).ToList();float xmin=contour.Min(p=>p.p.x),xmax=contour.Max(p=>p.p.x);
    (Point lo,Point hi) Cut(float x){x=Mathf.Clamp(x,xmin+.0000001f,xmax-.0000001f);var cut=new List<Point>();for(int i=0;i<contour.Count;i++){var a=contour[i];var b=contour[(i+1)%contour.Count];if((a.p.x<=x&&x<b.p.x)||(b.p.x<=x&&x<a.p.x)){float t=(x-a.p.x)/(b.p.x-a.p.x);cut.Add(new Point(Vector3.Lerp(a.p,b.p,t),Vector3.Lerp(a.n,b.n,t).normalized));}}return(cut.OrderBy(p=>p.p.y).First(),cut.OrderBy(p=>p.p.y).Last());}
    const int N=64,M=20;int start=outP.Count;var sample=new List<Vector3>();var edgeNormals=new List<Vector3>();
    for(int i=0;i<=N;i++){
     float x=xmin-.0015f+(xmax-xmin+.003f)*i/N;var (lo,hi)=Cut(x);float span=Mathf.Max(.00001f,hi.p.y-lo.p.y);
     float Slope(Point p)=>-p.n.y/(Mathf.Abs(p.n.z)<.15f?Mathf.Sign(p.n.z)*.15f:p.n.z);
     for(int j=0;j<=M;j++){
      float y=Mathf.Lerp(lo.p.y-.0015f,hi.p.y+.0015f,(float)j/M),t=(y-lo.p.y)/span,z;
      if(t<0)z=lo.p.z+Slope(lo)*(y-lo.p.y);else if(t>1)z=hi.p.z+Slope(hi)*(y-hi.p.y);
      else{float t2=t*t,t3=t2*t;z=(2*t3-3*t2+1)*lo.p.z+(t3-2*t2+t)*span*Slope(lo)+(-2*t3+3*t2)*hi.p.z+(t3-t2)*span*Slope(hi);}
      z+=.00012f;var p=right*x+up*y+forward*z;sample.Add(p);edgeNormals.Add((right*Mathf.Lerp(lo.n.x,hi.n.x,Mathf.Clamp01(t))+up*Mathf.Lerp(lo.n.y,hi.n.y,Mathf.Clamp01(t))+forward*Mathf.Lerp(lo.n.z,hi.n.z,Mathf.Clamp01(t))).normalized);
     }
    }
    var smooth=new Vector3[sample.Count];for(int i=0;i<N;i++)for(int j=0;j<M;j++){
     int a=i*(M+1)+j,b=a+M+1,c=b+1,d=a+1;int[] t={a,b,c,a,c,d};for(int k=0;k<t.Length;k+=3){var n=Vector3.Cross(sample[t[k+1]]-sample[t[k]],sample[t[k+2]]-sample[t[k]]);if(Vector3.Dot(n,forward)<0)n=-n;for(int q=0;q<3;q++)smooth[t[k+q]]+=n;}outT.AddRange(t.Select(v=>v+start));
    }
    for(int i=0;i<=N;i++)for(int j=0;j<=M;j++){int v=i*(M+1)+j;float outer=Mathf.Clamp01(Mathf.Min(i,N-i)/3f)*Mathf.Clamp01(Mathf.Min(j,M-j)/3f);outP.Add(sample[v]);outN.Add(Vector3.Lerp(edgeNormals[v],smooth[v].normalized,outer).normalized);}
   }
   positions=outP.ToArray();normals=outN.ToArray();triangles=outT.ToArray();Debug.Log("[HeroActualLidFit] exact Body inverse-bind->Face apertures=2 vertices="+positions.Length);return true;
  }
  static void PrepareBodyClosure(MatchHeroLook hero,List<List<Point>> loops,Matrix4x4 map,Vector3 right,Vector3 up,Vector3 forward){
   var source=hero.body.sharedMesh;if(source.GetBlendShapeIndex("Hero_Blink")>=0)return;if(blinkCache.TryGetValue(source,out var cached)&&cached){hero.body.sharedMesh=cached;return;}var vertices=source.vertices;var originalNormals=source.normals;var changed=(Vector3[])vertices.Clone();var deltas=new Vector3[vertices.Length];var normals=new Vector3[vertices.Length];var tangent=new Vector3[vertices.Length];var inverse=map.inverse;int count=0;float maximum=0;
   var affected=new HashSet<int>();
   foreach(var loop in loops){
    float cx=loop.Average(v=>Vector3.Dot(v.p,right)),cy=loop.Average(v=>Vector3.Dot(v.p,up));float halfX=(loop.Max(v=>Vector3.Dot(v.p,right))-loop.Min(v=>Vector3.Dot(v.p,right)))*.5f,halfY=(loop.Max(v=>Vector3.Dot(v.p,up))-loop.Min(v=>Vector3.Dot(v.p,up)))*.5f;
    var contour=loop.OrderBy(p=>Mathf.Atan2(Vector3.Dot(p.p,up)-cy,Vector3.Dot(p.p,right)-cx)).ToArray();
    Vector3 Nearest(Vector3 p){float best=float.PositiveInfinity;Vector3 result=contour[0].p;for(int i=0;i<contour.Length;i++){var a=contour[i].p;var d=contour[(i+1)%contour.Length].p-a;float t=Mathf.Clamp01(Vector3.Dot(p-a,d)/Mathf.Max(1e-12f,d.sqrMagnitude));var q=a+d*t;float e=(q-p).sqrMagnitude;if(e<best){best=e;result=q;}}return result;}
    float CreaseDepth(float x){var cut=new List<Vector3>();for(int i=0;i<contour.Length;i++){var a=contour[i].p;var b=contour[(i+1)%contour.Length].p;float ax=Vector3.Dot(a,right),bx=Vector3.Dot(b,right);if((ax<=x&&x<bx)||(bx<=x&&x<ax))cut.Add(Vector3.Lerp(a,b,(x-ax)/(bx-ax)));}if(cut.Count<2)return Vector3.Dot(Nearest(right*x+up*cy),forward);var lo=cut.OrderBy(p=>Vector3.Dot(p,up)).First();var hi=cut.OrderBy(p=>Vector3.Dot(p,up)).Last();return Mathf.Lerp(Vector3.Dot(lo,forward),Vector3.Dot(hi,forward),.275f);}
    for(int v=0;v<vertices.Length;v++){
     var point=map.MultiplyPoint3x4(vertices[v]);float x=Vector3.Dot(point,right);if(Mathf.Abs(x-cx)>halfX+.018f)continue;
     var nearest=Nearest(point);float distance=Vector3.Distance(nearest,point);if(distance>.018f)continue;
     float w=1-Mathf.SmoothStep(0,1,Mathf.Clamp01(distance/.018f));float u=Mathf.Clamp((Vector3.Dot(nearest,right)-cx)/Mathf.Max(.001f,halfX),-1,1);float crease=cy+halfY*(-.45f+.22f*u*u);
     float z=CreaseDepth(Mathf.Clamp(Vector3.Dot(nearest,right),cx-halfX+.000001f,cx+halfX-.000001f));
     float side=Vector3.Dot(nearest,up)>=cy?1:-1;float overlap=(hero.female?.00165f:.00045f)*Mathf.Sqrt(Mathf.Max(.08f,1-u*u));crease-=side*overlap;z+=side>0?.0002f:0;
     var next=point+up*((crease-Vector3.Dot(nearest,up))*w)+forward*((z-Vector3.Dot(nearest,forward)+.0012f)*w);changed[v]=inverse.MultiplyPoint3x4(next);deltas[v]=changed[v]-vertices[v];maximum=Mathf.Max(maximum,deltas[v].magnitude);count++;affected.Add(v);
    }
   }
   var closed=UnityEngine.Object.Instantiate(source);closed.vertices=changed;closed.RecalculateNormals();var closedNormals=closed.normals;UnityEngine.Object.DestroyImmediate(closed);
   // The approved source has duplicated UV vertices along the eye contour. Smooth
   // the closed normal field in skin space so those duplicates cannot form fan creases.
   var buckets=new Dictionary<Vector3Int,List<int>>();const float radius=.006f;
   foreach(int v in affected){var p=changed[v];var k=new Vector3Int(Mathf.FloorToInt(p.x/radius),Mathf.FloorToInt(p.y/radius),Mathf.FloorToInt(p.z/radius));if(!buckets.ContainsKey(k))buckets[k]=new();buckets[k].Add(v);}
   foreach(int v in affected){var p=changed[v];var k=new Vector3Int(Mathf.FloorToInt(p.x/radius),Mathf.FloorToInt(p.y/radius),Mathf.FloorToInt(p.z/radius));var n=Vector3.zero;for(int x=-1;x<=1;x++)for(int y=-1;y<=1;y++)for(int z=-1;z<=1;z++)if(buckets.TryGetValue(k+new Vector3Int(x,y,z),out var near))foreach(int q in near){float d=(changed[q]-p).sqrMagnitude;if(d<radius*radius&&Vector3.Dot(closedNormals[q],originalNormals[v])>0)n+=closedNormals[q]*(1-d/(radius*radius));}normals[v]=(n.sqrMagnitude>1e-8f?n.normalized:originalNormals[v])-originalNormals[v];}
   var copy=UnityEngine.Object.Instantiate(source);copy.name=source.name+" (fitted eyelid closure morph)";copy.hideFlags=HideFlags.DontSave;copy.AddBlendShapeFrame("Hero_Blink",100,deltas,normals,tangent);hero.body.sharedMesh=copy;blinkCache[source]=copy;Debug.Log("[HeroActualLidFit] physical eyelid closure morph vertices="+count+" max travel="+maximum+"; original open positions exact");
  }

 }
}
