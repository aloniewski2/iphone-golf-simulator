using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace GolfArcade.Tennis {
 /// Bounded tailoring on the existing garment only. Preserve all UVs, faces,
 /// named skin weights and animation deltas; keep the full approved body exact.
 public static class HeroGarmentTailoring {
  static readonly Dictionary<(Mesh,bool,bool,string),Mesh> cache=new();
  public static void Apply(MatchHeroLook hero){
   if(!hero||hero.kit==null)return;
   foreach(var r in hero.kit)if(r&&r.sharedMesh)r.sharedMesh=Prepare(hero,r,r.sharedMesh);
  }
  public static Mesh Prepare(MatchHeroLook hero,SkinnedMeshRenderer r,Mesh src,Transform[] palette=null){
   if(src && src.name.StartsWith("TailoredSkirt")) return src;
   if(src && src.name.StartsWith("TailoredPolo")) return TailoredPoloDeformation.Prepare(hero,r,src); // Source-authored garment has its own coherent binding.
   if(hero&&r&&src&&r.name=="Kit_Bottom"&&!hero.female&&!hero.golfKit)return HeroShortsTailoring.Prepare(hero,r,src);
   if(!hero||!r||!src||!src.isReadable||(r.name!="Kit_Top"&&!r.name.StartsWith("Kit_Shoe_")))return src;
   if(r.name=="Kit_Top"){src=HeroGarmentSleeveGeometry.Prepare(hero,r,src,palette);src=HeroGarmentAxillaWeights.Prepare(hero,r,src,palette);}
   if(src.name.Contains("(sport tailoring)"))return HeroGarmentPoseCorrectives.Prepare(hero,r,src);
   var key=(src,hero.female,hero.golfKit,r.name);if(cache.TryGetValue(key,out var found))return HeroGarmentPoseCorrectives.Prepare(hero,r,found);
   var p=src.vertices;var n=src.normals;var old=(Vector3[])p.Clone();
   var toRoot=hero.transform.worldToLocalMatrix*r.transform.localToWorldMatrix;var fromRoot=toRoot.inverse;
   var root=p.Select(toRoot.MultiplyPoint3x4).ToArray();int changed=0;float largest=0;
   if(r.name=="Kit_Top"){
    int[] parent=Enumerable.Range(0,p.Length).ToArray();
    int Find(int i){while(parent[i]!=i){parent[i]=parent[parent[i]];i=parent[i];}return i;}
    void Join(int a,int b){a=Find(a);b=Find(b);if(a!=b)parent[b]=a;}
    var weld=new Dictionary<Vector3Int,int>();for(int i=0;i<p.Length;i++){var k=Vector3Int.RoundToInt(p[i]*100000);if(weld.TryGetValue(k,out int j))Join(i,j);else weld[k]=i;}
    var tri=src.triangles;for(int t=0;t<tri.Length;t+=3){Join(tri[t],tri[t+1]);Join(tri[t],tri[t+2]);}
    var bounds=new Dictionary<int,Bounds>();
    for(int i=0;i<root.Length;i++){int c=Find(i);if(!bounds.TryGetValue(c,out var b))b=new Bounds(root[i],Vector3.zero);else b.Encapsulate(root[i]);bounds[c]=b;}
    for(int i=0;i<p.Length;i++){
     var b=bounds[Find(i)];float top=hero.female?1.38f:1.43f;
     bool collar=b.min.y>(hero.female?1.20f:1.26f)&&b.max.y>top&&b.size.x>.12f&&b.size.x<.34f;
     if(!collar)continue;
     var q=root[i];float width=Mathf.Abs(q.x);
     if(width>.055f)q.x=Mathf.Sign(q.x)*(.055f+(width-.055f)*.78f);
     // Fold tips sit nearer the chest, with a shorter, narrower point.
     float front=Mathf.Abs(q.z);if(q.z>0&&front>.065f)q.z=.065f+(front-.065f)*.80f;
     float bottom=hero.female?1.285f:1.325f;float fade=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(bottom,bottom+.10f,q.y));q.y+=.009f*fade;
     p[i]=fromRoot.MultiplyPoint3x4(q);
    }
    if(hero.golfKit){
     // Maintain a deliberate tuck under the belt when the lower torso bends.
     // The body remains complete beneath this overlap.
     for(int i=0;i<p.Length;i++)if(root[i].y<1.055f){var q=toRoot.MultiplyPoint3x4(p[i]);q.y-=.012f*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.985f,1.055f,q.y)));p[i]=fromRoot.MultiplyPoint3x4(q);}
    }
   }else{
    // The previous high shoe collar cuts through the trouser hem. Keep the
    // rounded sneaker volume and sole, compress only its upper ankle edge.
    float start=hero.female?.100f:.112f;
    for(int i=0;i<p.Length;i++)if(root[i].y>start){var q=root[i];q.y=start+(q.y-start)*.56f;p[i]=fromRoot.MultiplyPoint3x4(q);}
   }
   for(int i=0;i<p.Length;i++){float d=(p[i]-old[i]).magnitude;if(d>.000001f)changed++;largest=Mathf.Max(largest,d);}
   var mesh=UnityEngine.Object.Instantiate(src);mesh.name=src.name+" (sport tailoring)";mesh.hideFlags=HideFlags.DontSave;mesh.vertices=p;
   // Original imported UV splits can carry different normals on a smooth cloth
   // panel. A local, angle-bounded field softens that seam while preserving the
   // opposed lining and real turned edges.
   if(n.Length==p.Length){
    const float radius=.009f;var grid=new Dictionary<Vector3Int,List<int>>();
    Vector3Int Cell(Vector3 v)=>new(Mathf.FloorToInt(v.x/radius),Mathf.FloorToInt(v.y/radius),Mathf.FloorToInt(v.z/radius));
    for(int i=0;i<p.Length;i++){var c=Cell(p[i]);if(!grid.TryGetValue(c,out var list))grid[c]=list=new List<int>();list.Add(i);}
    var corrected=(Vector3[])n.Clone();
    for(int i=0;i<p.Length;i++){
     var cell=Cell(p[i]);var sum=n[i]*2;
     for(int x=-1;x<=1;x++)for(int y=-1;y<=1;y++)for(int z=-1;z<=1;z++)if(grid.TryGetValue(cell+new Vector3Int(x,y,z),out var list))foreach(int j in list){float d=(p[i]-p[j]).sqrMagnitude;if(d>=radius*radius||Vector3.Dot(n[i],n[j])<.62f)continue;sum+=n[j]*(1-d/(radius*radius));}
     corrected[i]=sum.normalized;
    }
    mesh.normals=corrected;
   }
   mesh.RecalculateBounds();cache[key]=mesh;
   Debug.Log($"[HeroGarmentTailoring] {r.name} {(hero.female?"Female":"Male")} geometry vertices={changed} maxDelta={largest:R}m; UV/triangles/weights/palette exact");
   return HeroGarmentPoseCorrectives.Prepare(hero,r,mesh);
  }
 }
}
