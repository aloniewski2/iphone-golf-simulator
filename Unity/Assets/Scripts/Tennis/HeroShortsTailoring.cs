using System;using System.Collections.Generic;using UnityEngine;
namespace GolfArcade.Tennis {
 /// Look-development athletic shorts cut on the existing garment. The waist,
 /// UVs, indices, skin palette and animation weights retain their authored data.
 public static class HeroShortsTailoring {
  static readonly Dictionary<Mesh,Mesh> cache=new();
  public static Mesh Prepare(MatchHeroLook hero,SkinnedMeshRenderer renderer,Mesh source){
   string form=Environment.GetEnvironmentVariable("VISUAL_SHORTS_FORM");
   if(hero.female||hero.golfKit||renderer.name!="Kit_Bottom"||!source.isReadable||(form!="tailored"&&form!="short"))return source;
   if(source.name.Contains("(athletic shorts cut)"))return source;if(cache.TryGetValue(source,out var mesh))return mesh;
   var old=source.vertices;var points=(Vector3[])old.Clone();var toRoot=hero.transform.worldToLocalMatrix*renderer.transform.localToWorldMatrix;var fromRoot=toRoot.inverse;
   float Smooth(float a,float b,float x){float t=Mathf.Clamp01((x-a)/(b-a));return t*t*(3-2*t);}
   float rise=form=="short"?.115f:.085f;int changed=0;float max=0;
   for(int i=0;i<points.Length;i++){
    var p=toRoot.MultiplyPoint3x4(old[i]);float lower=1-Smooth(.59f,.89f,p.y);if(lower<.00001f)continue;
    float legX=Mathf.Sign(p.x)*.12f;float narrow=1-.13f*lower;
    p.x=legX+(p.x-legX)*narrow;p.z=.045f+(p.z-.045f)*(1-.10f*lower);
    p.y+=rise*lower+.020f*lower*Mathf.Exp(-2*Mathf.Pow(p.x/.055f,2));
    points[i]=fromRoot.MultiplyPoint3x4(p);float delta=Vector3.Distance(points[i],old[i]);max=Mathf.Max(max,delta);if(delta>.000001f)changed++;
   }
   mesh=UnityEngine.Object.Instantiate(source);mesh.name=source.name+" (athletic shorts cut)";mesh.hideFlags=HideFlags.DontSave;mesh.vertices=points;mesh.RecalculateNormals();
   var normals=mesh.normals;var refined=(Vector3[])normals.Clone();var duplicates=new Dictionary<Vector3Int,List<int>>();Vector3Int Key(Vector3 p)=>Vector3Int.RoundToInt(p*1000000);
   for(int i=0;i<points.Length;i++){var k=Key(points[i]);if(!duplicates.TryGetValue(k,out var list))duplicates[k]=list=new List<int>();list.Add(i);}
   for(int i=0;i<points.Length;i++){var sum=normals[i];foreach(int j in duplicates[Key(points[i])])if(Vector3.Dot(normals[i],normals[j])>.4f)sum+=normals[j];refined[i]=sum.normalized;}mesh.normals=refined;mesh.RecalculateBounds();cache[source]=mesh;
   Debug.Log("[HeroShortsTailoring] "+form+" changed="+changed+" maxMetres="+max+" waist/UV/indices/palette/weights unchanged");return mesh;
  }
 }
}
