using System;
using System.Collections.Generic;
using UnityEngine;
namespace GolfArcade.Tennis {
 /// Correct the side-torso's inherited upper-arm influence below the axilla.
 /// Rest geometry, UVs, blendshapes, bone palette, skeleton and contact regions
 /// remain exact. The same field is applied to the overlying fitted shirt.
 public static class HeroGarmentAxillaWeights {
  static readonly Dictionary<(Mesh,bool),Mesh> cache=new();
  public static Mesh Prepare(SkinnedMeshRenderer renderer,bool female){
   if(!renderer)return null;
   var hero=renderer.GetComponentInParent<MatchHeroLook>();
   return Prepare(hero,renderer,renderer.sharedMesh,female,renderer.bones);
  }
  public static Mesh Prepare(MatchHeroLook hero,SkinnedMeshRenderer r,Mesh src,Transform[] palette=null)=>Prepare(hero,r,src,hero&&hero.female,palette??r?.bones);
  static Mesh Prepare(MatchHeroLook hero,SkinnedMeshRenderer r,Mesh src,bool female,Transform[] palette){
   if(!hero||!r||!src||palette==null||!src.isReadable||src.name.Contains("(axilla weight field)"))return src;
   if(r!=hero.body&&r.name!="Body"&&!r.name.StartsWith("Body_")&&r.name!="Kit_Top")return src;
   if(cache.TryGetValue((src,female),out var hit))return hit;
   var p=src.vertices;var bw=src.boneWeights;
   if(bw.Length!=p.Length)return src;
   var arm=new HashSet<int>();var torso=new HashSet<int>();
   for(int i=0;i<palette.Length;i++){
    string n=palette[i]?palette[i].name:"";
    if(n=="LeftUpperArm"||n=="RightUpperArm"||n=="LeftShoulder"||n=="RightShoulder")arm.Add(i);
    if(n=="Spine"||n=="Chest"||n=="UpperChest")torso.Add(i);
   }
   if(arm.Count<2||torso.Count<2)return src;
   var toRoot=hero.transform.worldToLocalMatrix*r.transform.localToWorldMatrix;
   int changed=0;float maximumRemoved=0,maximumSumError=0;
   for(int v=0;v<p.Length;v++){
    var q=toRoot.MultiplyPoint3x4(p[v]);var b=bw[v];
    int[] ids={b.boneIndex0,b.boneIndex1,b.boneIndex2,b.boneIndex3};float[] w={b.weight0,b.weight1,b.weight2,b.weight3};
    float torsoMass=0;for(int j=0;j<4;j++)if(torso.Contains(ids[j]))torsoMass+=w[j];
    float x=Mathf.Abs(q.x),low=female?1.05f:1.08f,high=female?1.16f:1.21f,shoulder=female?1.28f:1.33f;
    float field=Smooth(.065f,.105f,x)*(1-Smooth(.140f,.190f,x))*(1-Smooth(high,shoulder,q.y))*Smooth(low,low+.03f,q.y)*Smooth(.07f,.28f,torsoMass)*.90f;
    if(field<=0||torsoMass<=0)continue;
    float removed=0,originalSum=b.weight0+b.weight1+b.weight2+b.weight3;
    for(int j=0;j<4;j++)if(arm.Contains(ids[j])){float take=w[j]*field;w[j]-=take;removed+=take;}
    if(removed<=0)continue;
    float[] original={b.weight0,b.weight1,b.weight2,b.weight3};
    for(int j=0;j<4;j++)if(torso.Contains(ids[j]))w[j]+=removed*original[j]/torsoMass;
    b.weight0=w[0];b.weight1=w[1];b.weight2=w[2];b.weight3=w[3];bw[v]=b;changed++;
    maximumRemoved=Mathf.Max(maximumRemoved,removed);maximumSumError=Mathf.Max(maximumSumError,Mathf.Abs(w[0]+w[1]+w[2]+w[3]-originalSum));
   }
   if(changed==0){cache[(src,female)]=src;return src;}
   var result=UnityEngine.Object.Instantiate(src);result.name=src.name+" (axilla weight field)";result.hideFlags=HideFlags.DontSave;result.boneWeights=bw;
   cache[(src,female)]=result;
   Debug.Log($"[HeroGarmentAxillaWeights] {(female?"Female":"Male")} {r.name}: modified={changed} maxArmMassTransfer={maximumRemoved:R} normalizedError={maximumSumError:R}; rest vertices/UVs/indices/palette and outside weights exact");
   return result;
  }
  static float Smooth(float a,float b,float value){float t=Mathf.Clamp01((value-a)/(b-a));return t*t*(3-2*t);}
 }
}
