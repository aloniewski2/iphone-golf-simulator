using System;
using System.Collections.Generic;
using UnityEngine;
namespace GolfArcade.Tennis {
 /// Reauthor a near-elbow polo sleeve as a fitted upper-arm sleeve using the
 /// original bind landmarks. Preserve garment indices, UVs, weights and rig.
 public static class HeroGarmentSleeveGeometry {
  static readonly Dictionary<Mesh,Mesh> cache=new();
  const string Marker=" (fitted short sleeves)";
  public static Mesh Prepare(MatchHeroLook hero,SkinnedMeshRenderer r,Mesh src,Transform[] palette=null){
   if(!hero||hero.golfKit||!r||r.name!="Kit_Top"||!src||!src.isReadable||src.name.Contains(Marker))return src;
   if(cache.TryGetValue(src,out var hit))return hit;
   palette??=r.bones;var binds=src.bindposes;var bw=src.boneWeights;var p=src.vertices;
   if(palette==null||binds.Length!=palette.Length||bw.Length!=p.Length)return src;
   var toRoot=hero.transform.worldToLocalMatrix*r.transform.localToWorldMatrix;var inverse=toRoot.inverse;var root=new Vector3[p.Length];for(int i=0;i<p.Length;i++)root[i]=toRoot.MultiplyPoint3x4(p[i]);
   int changed=0;float max=0;
   foreach(string side in new[]{"Left","Right"}){
    int Id(string name)=>Array.FindIndex(palette,b=>b&&b.name==side+name);
    int upper=Id("UpperArm"),lower=Id("LowerArm"),shoulder=Id("Shoulder");if(upper<0||lower<0)continue;
    var a=toRoot.MultiplyPoint3x4(binds[upper].inverse.MultiplyPoint3x4(Vector3.zero));var b=toRoot.MultiplyPoint3x4(binds[lower].inverse.MultiplyPoint3x4(Vector3.zero));var axis=b-a;float length=axis.magnitude;if(length<.10f||length>.40f)throw new InvalidOperationException("Polo sleeve bind landmarks unavailable");var direction=axis/length;
    for(int i=0;i<p.Length;i++){
     var w=bw[i];int[] ids={w.boneIndex0,w.boneIndex1,w.boneIndex2,w.boneIndex3};float[] mass={w.weight0,w.weight1,w.weight2,w.weight3};float arm=0;for(int j=0;j<4;j++)if(ids[j]==upper||ids[j]==lower||ids[j]==shoulder)arm+=mass[j];
     float t=Vector3.Dot(root[i]-a,direction)/length;float amount=Mathf.Max(t-.24f,0)*.62f*Smooth(.08f,.35f,arm)*Smooth(.20f,.37f,t);if(amount<=.000001f)continue;
     var q=toRoot.MultiplyPoint3x4(p[i])-axis*amount;var next=inverse.MultiplyPoint3x4(q);max=Mathf.Max(max,(next-p[i]).magnitude);p[i]=next;changed++;
    }
   }
   if(changed==0){cache[src]=src;return src;}
   var mesh=UnityEngine.Object.Instantiate(src);mesh.name=src.name+Marker;mesh.hideFlags=HideFlags.DontSave;mesh.vertices=p;mesh.RecalculateBounds();cache[src]=mesh;
   Debug.Log($"[HeroGarmentSleeveGeometry] {r.name} vertices={changed} max={max:R}m; full/LOD bind landmarks, existing faces/UVs/weights/rig retained");return mesh;
  }
  static float Smooth(float a,float b,float v){float t=Mathf.Clamp01((v-a)/(b-a));return t*t*(3-2*t);}
 }
}
