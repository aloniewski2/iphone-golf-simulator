using System;using System.Collections.Generic;using System.Linq;using UnityEngine;
namespace GolfArcade.Tennis {
 /// Bounded facial volume fairing. The outer bald silhouette, ears, eye-hole rims,
 /// neck/body, rig and original data outside the front facial interior stay exact.
 public static class HeroHeadVolume {
  static readonly Dictionary<Mesh,Mesh> cache=new();
  public static Mesh Prepare(MatchHeroLook hero){
   var source=hero.body.sharedMesh;if(!source||!source.isReadable||source.name.Contains("(facial volume)"))return source;if(cache.TryGetValue(source,out var ready)&&ready)return ready;
   var head=hero.Bone(HumanBodyBones.Head);int hi=Array.IndexOf(hero.body.bones,head);if(hi<0)return source;
   string candidate=Environment.GetEnvironmentVariable("VISUAL_HEAD_FORM")??"baseline";
   bool strong=candidate=="strong";bool broad=candidate=="broad";bool crafted=candidate=="crafted"||candidate=="crafted2"||candidate=="crafted3";bool crafted2=candidate=="crafted2"||candidate=="crafted3";bool weighted=candidate=="crafted3";
   int relaxationSteps=crafted2?64:strong||crafted?108:broad?72:36;
   float displacementLimit=crafted2?.0100f:strong||crafted?.016f:broad?.012f:.008f;
   var p=source.vertices;var n=source.normals;var w=source.boneWeights;var bind=source.bindposes[hi];HeroFaceBasis.Get(hero,out var fr,out var fu,out var ff);var faceFromBody=hero.face.transform.worldToLocalMatrix*head.localToWorldMatrix*bind;
   var eye=hero.face.GetComponent<MeshFilter>().sharedMesh;var ep=new List<Vector3>();var fm=hero.face.sharedMaterials;for(int s=0;s<Mathf.Min(fm.Length,eye.subMeshCount);s++)if(fm[s]&&fm[s].name.Contains("Sclera"))ep.AddRange(eye.GetTriangles(s).Select(i=>eye.vertices[i]));if(ep.Count==0)return source;
   var centre=ep.Aggregate(Vector3.zero,(sum,v)=>sum+v)/ep.Count;var matrix=faceFromBody;var back=matrix.inverse;
   Matrix4x4[] weightedMap=null;
   if(weighted){
    var palette=hero.body.bones.Select((bone,i)=>hero.face.transform.worldToLocalMatrix*bone.localToWorldMatrix*source.bindposes[i]).ToArray();
    weightedMap=new Matrix4x4[p.Length];
    for(int raw=0;raw<p.Length;raw++){
     var bw=w[raw];var m=Matrix4x4.zero;
     for(int k=0;k<16;k++)m[k]=palette[bw.boneIndex0][k]*bw.weight0+palette[bw.boneIndex1][k]*bw.weight1+palette[bw.boneIndex2][k]*bw.weight2+palette[bw.boneIndex3][k]*bw.weight3;
     weightedMap[raw]=m;
    }
   }
   var points=p.Select((point,i)=>weighted?weightedMap[i].MultiplyPoint3x4(point):matrix.MultiplyPoint3x4(point)).ToArray();
   Vector3Int K(Vector3 a)=>new(Mathf.RoundToInt(a.x*1000000),Mathf.RoundToInt(a.y*1000000),Mathf.RoundToInt(a.z*1000000));var welded=new Dictionary<Vector3Int,int>();var ids=new int[p.Length];var unique=new List<Vector3>();var copies=new List<List<int>>();
   for(int i=0;i<p.Length;i++){var k=K(p[i]);if(!welded.TryGetValue(k,out int id)){id=unique.Count;welded[k]=id;unique.Add(points[i]);copies.Add(new List<int>());}ids[i]=id;copies[id].Add(i);}
   var adjacency=Enumerable.Range(0,unique.Count).Select(_=>new HashSet<int>()).ToArray();var edgeCount=new Dictionary<(int,int),int>();var tris=source.triangles;
   for(int t=0;t<tris.Length;t+=3)for(int e=0;e<3;e++){int a=ids[tris[t+e]],b=ids[tris[t+(e+1)%3]];if(a==b)continue;adjacency[a].Add(b);adjacency[b].Add(a);var key=a<b?(a,b):(b,a);edgeCount[key]=edgeCount.GetValueOrDefault(key)+1;}
   var boundary=edgeCount.Where(x=>x.Value==1).SelectMany(x=>new[]{x.Key.Item1,x.Key.Item2}).Distinct().Select(i=>unique[i]).Where(v=>Vector3.Distance(v,centre)<.15f).ToArray();var mask=new Dictionary<int,float>();
   float Smooth(float a,float b,float x){float t=Mathf.Clamp01((x-a)/(b-a));return t*t*(3-2*t);}
   foreach(int i in Enumerable.Range(0,unique.Count)){
    int raw=copies[i][0];var bw=w[raw];float mass=(bw.boneIndex0==hi?bw.weight0:0)+(bw.boneIndex1==hi?bw.weight1:0)+(bw.boneIndex2==hi?bw.weight2:0)+(bw.boneIndex3==hi?bw.weight3:0);if(!crafted&&mass<.8f)continue;var v=unique[i]-centre;float x=Vector3.Dot(v,fr),y=Vector3.Dot(v,fu),z=Vector3.Dot(v,ff);
    float amount=(1-Smooth(strong||broad?.070f:.062f,strong||broad?.094f:.086f,Mathf.Abs(x)))*Smooth(-.175f,-.145f,y)*(1-Smooth(strong||broad?.090f:.065f,strong||broad?.120f:.100f,y))*Smooth(-.145f,-.105f,z);if(crafted)amount=(1-Smooth(.066f,.091f,Mathf.Abs(x)))*Smooth(-.215f,-.185f,y)*(1-Smooth(.000f,.025f,y))*Smooth(-.140f,-.090f,z);
    if(crafted2){float nx=x/.029f,ny=(y+.042f)/.030f;float nosePin=Mathf.Exp(-2*(nx*nx+ny*ny));amount*=1-.995f*nosePin;}
    if(amount<.001f)continue;
    float distance=boundary.Length>0?boundary.Min(b=>Vector3.Distance(b,unique[i])):1;amount*=Smooth(strong||broad||crafted?.003f:.006f,strong||broad||crafted?.009f:.014f,distance);if(amount>.001f)mask[i]=amount;
   }
   var original=unique.ToArray();var current=(Vector3[])original.Clone();
   // Positive relaxation removes broad sculpt ripples rather than restoring
   // them with a negative Taubin pass. The silhouette/rims remain pinned by mask.
   for(int repeat=0;repeat<relaxationSteps;repeat++){
    var next=(Vector3[])current.Clone();foreach(var item in mask){int i=item.Key;if(adjacency[i].Count==0)continue;var mean=adjacency[i].Aggregate(Vector3.zero,(sum,j)=>sum+current[j])/adjacency[i].Count;var proposal=current[i]+(mean-current[i])*(.40f*item.Value);next[i]=original[i]+Vector3.ClampMagnitude(proposal-original[i],displacementLimit*item.Value);}current=next;
   }
   int changed=0;int lowerChanged=0;float maximum=0;foreach(var item in mask)foreach(int raw in copies[item.Key]){var next=weighted?weightedMap[raw].inverse.MultiplyPoint3x4(current[item.Key]):back.MultiplyPoint3x4(current[item.Key]);float distance=Vector3.Distance(next,p[raw]);if(distance>.000001f){changed++;if(Vector3.Dot(unique[item.Key]-centre,fu)<-.055f)lowerChanged++;}maximum=Mathf.Max(maximum,distance);p[raw]=next;}
   ready=UnityEngine.Object.Instantiate(source);ready.name=source.name+" (facial volume)";ready.hideFlags=HideFlags.DontSave;ready.vertices=p;// Rebuild area-weighted normals across exact positional seams. Imported
   // duplicate vertices must not introduce lighting creases into a smooth face.
   var areaNormals=new Vector3[unique.Count];
   for(int t=0;t<tris.Length;t+=3){int a=ids[tris[t]],b=ids[tris[t+1]],c=ids[tris[t+2]];var cross=Vector3.Cross(current[b]-current[a],current[c]-current[a]);areaNormals[a]+=cross;areaNormals[b]+=cross;areaNormals[c]+=cross;}
   var refined=new Vector3[p.Length];for(int raw=0;raw<p.Length;raw++)refined[raw]=(weighted?weightedMap[raw].transpose.MultiplyVector(areaNormals[ids[raw]].normalized):back.MultiplyVector(areaNormals[ids[raw]].normalized)).normalized;foreach(var item in mask)foreach(int raw in copies[item.Key])n[raw]=Vector3.Slerp(n[raw],refined[raw],item.Value).normalized;ready.normals=n;cache[source]=ready;Debug.Log("[HeroHeadVolume] candidate="+candidate+" steps="+relaxationSteps+" front interior fairing vertices="+changed+" lowerFaceVertices="+lowerChanged+" maxMetres="+maximum+"; original outer bald silhouette/eye rims/body/rig exact");return ready;
  }
 }
}
