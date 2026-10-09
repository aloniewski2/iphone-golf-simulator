using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace GolfArcade.Tennis {
 /// Garment-only motion fields. Keep geometry, UVs, materials and named skeleton
 /// exact. Tennis skirts retain their authored weights. The Golf outer skirt
 /// carries part of its hem on the pelvis only after original hidden thigh
 /// triangles have been restored, so a bent-leg pose cannot expose a body cut.
 public static class HeroGarmentHemWeights {
  static readonly Dictionary<(Mesh,bool,bool),Mesh> cache=new();
  public static void Apply(MatchHeroLook hero){
   if(!hero||hero.kit==null)return;
   HeroGarmentTailoring.Apply(hero);
   if(!hero.golfKit||!CoverageReady(hero))return;
   var r=hero.kit.FirstOrDefault(x=>x&&x.name=="Kit_Bottom");if(!r||!r.sharedMesh.isReadable)return;
   r.sharedMesh=Prepare(hero,r,r.sharedMesh,r.bones);
  }
  public static Mesh Prepare(MatchHeroLook hero,SkinnedMeshRenderer r,Mesh src,Transform[] palette){
   src=HeroGarmentTailoring.Prepare(hero,r,src,palette);
   if(!hero||!r||!src||!src.isReadable||!hero.golfKit||!CoverageReady(hero)||r.name!="Kit_Bottom")return src;
   if(src.name.Contains("(hem motion field)"))return src;
   if(cache.TryGetValue((src,hero.female,hero.golfKit),out var found))return found;
   var p=src.vertices;var w=src.boneWeights;var toRoot=hero.transform.worldToLocalMatrix*r.transform.localToWorldMatrix;
   var positions=p.Select(toRoot.MultiplyPoint3x4).ToArray();float hem=positions.Min(v=>v.y),waist=positions.Max(v=>v.y);
   int Id(string name)=>Array.FindIndex(palette,b=>b&&b.name==name);
   int hips=Id("Hips"),ll=Id("LeftLowerLeg"),rl=Id("RightLowerLeg"),lf=Id("LeftFoot"),rf=Id("RightFoot"),lu=Id("LeftUpperLeg"),ru=Id("RightUpperLeg");
   if((hero.female?new[]{hips,lu,ru}:new[]{ll,rl}).Any(i=>i<0))throw new InvalidOperationException("Golf hem motion field requires original leg palette");
   Vector3 Rest(int i)=>toRoot.MultiplyPoint3x4(src.bindposes[i].inverse.GetColumn(3));
   float Smooth(float a,float b,float x){float t=Mathf.Clamp01((x-a)/(b-a));return t*t*(3-2*t);}
   // Welded spatial components keep concealed fitted inner shorts independent
   // from the outer skirt, despite their shared imported material role.
   int[] parent=Enumerable.Range(0,p.Length).ToArray();int Find(int i){while(parent[i]!=i){parent[i]=parent[parent[i]];i=parent[i];}return i;}
   void Join(int a,int b){a=Find(a);b=Find(b);if(a!=b)parent[b]=a;}
   var weld=new Dictionary<Vector3Int,int>();for(int i=0;i<p.Length;i++){var k=Vector3Int.RoundToInt(p[i]*100000);if(weld.TryGetValue(k,out int j))Join(i,j);else weld[k]=i;}
   var triangles=src.triangles;for(int i=0;i<triangles.Length;i+=3){Join(triangles[i],triangles[i+1]);Join(triangles[i],triangles[i+2]);}
   var minima=new Dictionary<int,float>();for(int i=0;i<p.Length;i++){int c=Find(i);minima[c]=Mathf.Min(minima.GetValueOrDefault(c,float.PositiveInfinity),positions[i].y);}
   int changed=0;float largest=0;
   for(int i=0;i<w.Length;i++){
    var old=w[i];int[] ids={old.boneIndex0,old.boneIndex1,old.boneIndex2,old.boneIndex3};float[] values={old.weight0,old.weight1,old.weight2,old.weight3};var weights=new Dictionary<int,float>();for(int k=0;k<4;k++)if(values[k]>0)weights[ids[k]]=weights.GetValueOrDefault(ids[k])+values[k];var before=new Dictionary<int,float>(weights);
    if(hero.female){
     if(minima[Find(i)]>hem+.004f)continue;
     float amount=Smooth(.06f,.22f,waist-positions[i].y);float removed=0;
     foreach(int leg in new[]{lu,ru})if(weights.TryGetValue(leg,out float mass)){float keep=mass*(1-.32f*amount);removed+=mass-keep;weights[leg]=keep;}
     if(removed<.000001f)continue;weights[hips]=weights.GetValueOrDefault(hips)+removed;
    } else {
     float amount=1-Smooth(hem+.022f,hem+.065f,positions[i].y);if(amount<.000001f)continue;
     bool left=(positions[i]-Rest(ll)).sqrMagnitude<(positions[i]-Rest(rl)).sqrMagnitude;int shin=left?ll:rl;
     foreach(int id in weights.Keys.ToArray())weights[id]*=1-amount;weights[shin]=weights.GetValueOrDefault(shin)+amount;
    }
    var pairs=weights.Where(a=>a.Value>.000001f).OrderByDescending(a=>a.Value).Take(4).ToArray();float sum=pairs.Sum(a=>a.Value);var b=new BoneWeight();
    if(pairs.Length>0){b.boneIndex0=pairs[0].Key;b.weight0=pairs[0].Value/sum;}if(pairs.Length>1){b.boneIndex1=pairs[1].Key;b.weight1=pairs[1].Value/sum;}if(pairs.Length>2){b.boneIndex2=pairs[2].Key;b.weight2=pairs[2].Value/sum;}if(pairs.Length>3){b.boneIndex3=pairs[3].Key;b.weight3=pairs[3].Value/sum;}w[i]=b;changed++;
    largest=Mathf.Max(largest,before.Keys.Union(weights.Keys).Sum(id=>Mathf.Abs(before.GetValueOrDefault(id)-weights.GetValueOrDefault(id))));
   }
   var mesh=UnityEngine.Object.Instantiate(src);mesh.name=src.name+" (hem motion field)";mesh.hideFlags=HideFlags.DontSave;mesh.boneWeights=w;cache[(src,hero.female,hero.golfKit)]=mesh;
   Debug.Log("[HeroGarmentHemWeights] "+(hero.female?"Female":"Male")+" garment-only vertices="+changed+" maxWeightL1="+largest+" hemRootY="+hem+" geometry/UV/material/rig unchanged; actual motion proof required");return mesh;
  }
  static bool CoverageReady(MatchHeroLook hero)=>!hero.female||(hero.body&&hero.body.sharedMesh&&(hero.body.sharedMesh.name.Contains("original lower coverage restored")||hero.body.sharedMesh.name.Contains("Covered_Female_Body")));
 }
}
