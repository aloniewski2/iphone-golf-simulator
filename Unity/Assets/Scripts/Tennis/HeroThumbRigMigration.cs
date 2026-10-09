using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace GolfArcade.Tennis {
 /// Versioned authored hand anatomy. Original approved clips are rebased from their
 /// old rest, preserving their old skinning deltas. Named hierarchy stays53/54.
 /// Manual samplers MUST BeforeSample -> old clip sample -> AfterSample.
 [DefaultExecutionOrder(1110),DisallowMultipleComponent]
 public sealed class HeroThumbRigMigration:MonoBehaviour {
  public const string Version="match-hero-hand-anatomy-v3";
  public MatchHeroLook look;
  sealed class Joint { public Transform bone;public Matrix4x4 oldRest,newRest;public Vector3 rawPosition,rawScale;public Quaternion rawRotation; }
  readonly List<Joint> joints=new();bool prepared,applied;
  public static HeroThumbRigMigration Prepare(MatchHeroLook hero) {
   if(!hero)return null;var migration=hero.GetComponent<HeroThumbRigMigration>();if(!migration)migration=hero.gameObject.AddComponent<HeroThumbRigMigration>();migration.look=hero;migration.Init();return migration;
  }
  void Init() {
   if(prepared||!look||!look.body)return;
   var body=look.body;var palette=body.bones;var binds=body.sharedMesh.bindposes;
   var old=new Dictionary<Transform,Matrix4x4>();var toRoot=transform.worldToLocalMatrix*body.transform.localToWorldMatrix;
   for(int b=0;b<palette.Length;b++)old[palette[b]]=toRoot*binds[b].inverse;
   var reference=Resources.Load<GameObject>("Tennis/Premium/HandRest/"+(look.female?"Female":"Male"));
   if(!reference)throw new InvalidOperationException("Authored hand-v3 rest missing");
   var source=reference.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r=>r.name.StartsWith("Body"));
   var sourceToRoot=reference.transform.worldToLocalMatrix*source.transform.localToWorldMatrix;
   var authored=source.bones.Select((b,i)=>new{b.name,rest=sourceToRoot*source.sharedMesh.bindposes[i].inverse}).ToDictionary(x=>x.name,x=>x.rest);
   foreach(var entry in old){
    string name=entry.Key.name;
    bool digit=new[]{"Thumb","Index","Middle","Ring","Little"}.Any(d=>name.StartsWith("Left"+d)||name.StartsWith("Right"+d));
    if(!digit)continue;
    if(!authored.TryGetValue(name,out var wanted))throw new InvalidOperationException("Hand-v3 rest bone missing "+name);
    joints.Add(new Joint{bone=entry.Key,oldRest=entry.Value,newRest=wanted,rawPosition=entry.Key.localPosition,rawRotation=entry.Key.localRotation,rawScale=entry.Key.localScale});
   }
   // Parent-first order is required when converted world tracks become local TRS.
   joints.Sort((a,b)=>Depth(a.bone).CompareTo(Depth(b.bone)));
   // Every skin uses the same new inverse binds; distance swaps are prepared
   // through the same helper, so a palette cannot silently return to old rest.
   foreach(var renderer in new[]{look.body}.Concat(look.kit??Array.Empty<SkinnedMeshRenderer>()).Where(r=>r).Distinct())PrepareRenderer(renderer);
   var restore=gameObject.GetComponent<HeroThumbSourceRestore>();if(!restore)restore=gameObject.AddComponent<HeroThumbSourceRestore>();restore.migration=this;
   prepared=true;Debug.Log("[HeroThumbRigMigration] prepared "+Version+" "+(look.female?"Female":"Male")+"; names/hierarchy exact; old clips require consistent rebase");
  }
  static int Depth(Transform bone){int d=0;for(var p=bone.parent;p;p=p.parent)d++;return d;}
  readonly Dictionary<(Mesh source,int renderer),Mesh> boundMeshes=new();
  public void PrepareRenderer(SkinnedMeshRenderer renderer){
   if(!renderer||!renderer.sharedMesh)return;var source=renderer.sharedMesh;var key=(source,renderer.GetInstanceID());
   if(boundMeshes.TryGetValue(key,out var found)&&found){renderer.sharedMesh=found;return;}
   var updated=(Matrix4x4[])source.bindposes.Clone();bool changed=false;
   var sourceToRoot=transform.worldToLocalMatrix*renderer.transform.localToWorldMatrix;
   for(int b=0;b<renderer.bones.Length;b++){
    var joint=joints.FirstOrDefault(j=>j.bone==renderer.bones[b]);if(joint==null)continue;
    var wanted=joint.newRest.inverse*sourceToRoot;float error=0;for(int k=0;k<16;k++)error=Mathf.Max(error,Mathf.Abs(updated[b][k]-wanted[k]));
    if(error<.0000001f)continue;updated[b]=wanted;changed=true;
   }
   if(!changed){boundMeshes[key]=source;return;}
   var mesh=UnityEngine.Object.Instantiate(source);mesh.name=source.name+" (hand v3 bind)";mesh.hideFlags=HideFlags.DontSave;mesh.bindposes=updated;renderer.sharedMesh=mesh;boundMeshes[key]=mesh;boundMeshes[(mesh,renderer.GetInstanceID())]=mesh;
  }
  public void RestoreRaw() {
   if(!prepared)return;
   foreach(var j in joints){j.bone.localPosition=j.rawPosition;j.bone.localRotation=j.rawRotation;j.bone.localScale=j.rawScale;}
   applied=false;
  }
  public void Rebase() {
   Init();if(!prepared||applied)return;
   var rootInverse=transform.worldToLocalMatrix;var poses=joints.Select(j=>rootInverse*j.bone.localToWorldMatrix*j.oldRest.inverse*j.newRest).ToArray();
   for(int i=0;i<joints.Count;i++){var j=joints[i];j.rawPosition=j.bone.localPosition;j.rawRotation=j.bone.localRotation;j.rawScale=j.bone.localScale;}
   for(int i=0;i<joints.Count;i++) {
    var bone=joints[i].bone;var parent=transform.worldToLocalMatrix*bone.parent.localToWorldMatrix;var local=parent.inverse*poses[i];
    Decompose(local,out var position,out var rotation,out var scale);bone.localPosition=position;bone.localRotation=rotation;bone.localScale=scale;
   }
   applied=true;
  }
  static void Decompose(Matrix4x4 matrix,out Vector3 p,out Quaternion q,out Vector3 s) {
   p=matrix.GetColumn(3);Vector3 x=matrix.GetColumn(0),y=matrix.GetColumn(1),z=matrix.GetColumn(2);s=new Vector3(x.magnitude,y.magnitude,z.magnitude);if(Vector3.Dot(Vector3.Cross(x,y),z)<0)s.x=-s.x;q=Quaternion.LookRotation(z/s.z,y/s.y);
  }
  public static void BeforeSample(GameObject root){var migration=root.GetComponent<HeroThumbRigMigration>();if(migration)migration.RestoreRaw();}
  public static void AfterSample(GameObject root){var hero=root.GetComponent<MatchHeroLook>();var migration=Prepare(hero);if(migration)migration.Rebase();}
  void LateUpdate(){Rebase();}
  /// Record original-layout curves after authoring a v3-pose gesture. Runtime then
  /// applies the same old->new rebase as every unchanged original approved clip.
  public void ToSourcePose(){
   if(!prepared||!applied)return;var inverse=transform.worldToLocalMatrix;var poses=joints.Select(j=>inverse*j.bone.localToWorldMatrix*j.newRest.inverse*j.oldRest).ToArray();
   for(int i=0;i<joints.Count;i++){var j=joints[i];var parent=transform.worldToLocalMatrix*j.bone.parent.localToWorldMatrix;Decompose(parent.inverse*poses[i],out var p,out var q,out var scale);j.bone.localPosition=p;j.bone.localRotation=q;j.bone.localScale=scale;j.rawPosition=p;j.rawRotation=q;j.rawScale=scale;}applied=false;
  }
  public bool Applied=>applied;
  public void ApplyAnatomicalWeights(){
   if(!prepared)return;var body=look.body;var source=body.sharedMesh;var weights=source.boneWeights;var vertices=source.vertices;var binds=source.bindposes;var palette=body.bones;int changed=0;
   float Smooth(float a,float b,float v){float t=Mathf.Clamp01((v-a)/(b-a));return t*t*(3-2*t);}
   foreach(string side in new[]{"Left","Right"}){
    int Id(string suffix)=>Array.FindIndex(palette,b=>b&&b.name==side+suffix);
    int hand=Id("Hand"),prox=Id("ThumbProximal"),middle=Id("ThumbIntermediate"),distal=Id("ThumbDistal");if(hand<0||prox<0||middle<0||distal<0)continue;
    Vector3 p=binds[prox].inverse.GetColumn(3),m=binds[middle].inverse.GetColumn(3),d=binds[distal].inverse.GetColumn(3);float first=Vector3.Distance(p,m),second=Vector3.Distance(m,d),radius=look.female ? .015f:.014f;
    Vector3 tip=d+(d-m).normalized*.018f;var chain=new[]{p,m,d,tip};float[] stations={0,first,first+second,first+second+.018f};
    for(int v=0;v<vertices.Length;v++){
     var bw=weights[v];int[] ids={bw.boneIndex0,bw.boneIndex1,bw.boneIndex2,bw.boneIndex3};float[] ws={bw.weight0,bw.weight1,bw.weight2,bw.weight3};var original=new Dictionary<int,float>();float oldThumb=0;
     for(int i=0;i<4;i++){if(ws[i]<=0)continue;if(!original.ContainsKey(ids[i]))original[ids[i]]=0;original[ids[i]]+=ws[i];if(ids[i]==prox||ids[i]==middle||ids[i]==distal)oldThumb+=ws[i];}
     float nearest=float.MaxValue,station=0;for(int k=0;k<3;k++){var a=chain[k];var axis=chain[k+1]-a;float t=Mathf.Clamp01(Vector3.Dot(vertices[v]-a,axis)/axis.sqrMagnitude);float distance=Vector3.Distance(vertices[v],a+axis*t);if(distance<nearest){nearest=distance;station=Mathf.Lerp(stations[k],stations[k+1],t);}}
     // A complete geometric chain field replaces inherited cap/web leakage. At a
     // shared position it is continuous regardless of original UV/normal splits.
     float strength=1-Smooth(radius*.65f,radius,nearest);strength*=Smooth(.002f,.013f,station);if(oldThumb>.02f)strength=Mathf.Max(strength,1-Smooth(radius, radius*1.5f,nearest));if(strength<.002f)continue;
     float basal=Smooth(0,.016f,station),mid=Smooth(first-.007f,first+.007f,station),end=Smooth(first+second-.006f,first+second+.006f,station);
     var field=new Dictionary<int,float>{{hand,1-basal},{prox,basal*(1-mid)},{middle,mid*(1-end)},{distal,end}};
     var map=original.ToDictionary(k=>k.Key,k=>k.Value*(1-strength));foreach(var part in field){if(!map.ContainsKey(part.Key))map[part.Key]=0;map[part.Key]+=part.Value*strength;}
     var pairs=map.OrderByDescending(a=>a.Value).Take(4).ToArray();float sum=pairs.Sum(a=>a.Value);var w=new BoneWeight();
     if(pairs.Length>0){w.boneIndex0=pairs[0].Key;w.weight0=pairs[0].Value/sum;}if(pairs.Length>1){w.boneIndex1=pairs[1].Key;w.weight1=pairs[1].Value/sum;}if(pairs.Length>2){w.boneIndex2=pairs[2].Key;w.weight2=pairs[2].Value/sum;}if(pairs.Length>3){w.boneIndex3=pairs[3].Key;w.weight3=pairs[3].Value/sum;}weights[v]=w;changed++;
    }
   }
   var copy=UnityEngine.Object.Instantiate(source);copy.hideFlags=HideFlags.DontSave;copy.name=source.name+" (anatomical thumb chain weights)";copy.boneWeights=weights;body.sharedMesh=copy;Debug.Log("[HeroThumbRigMigration] geometric thumb chain weights changed="+changed);
  }
 }
 [DefaultExecutionOrder(-1000),DisallowMultipleComponent] public sealed class HeroThumbSourceRestore:MonoBehaviour {public HeroThumbRigMigration migration;void Update(){if(migration)migration.RestoreRaw();}}
}
