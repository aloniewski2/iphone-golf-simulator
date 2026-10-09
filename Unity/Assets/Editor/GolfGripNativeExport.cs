#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using GolfArcade.Game;
using GolfArcade.Tennis;
using UnityEngine;
using UnityEditor;
namespace GolfArcade.EditorTools {
 /// Refresh only arm/hand motion tracks at the native manifest's existing sample times.
 /// Meshes, materials, bind poses, other tracks and manifest offsets remain exact.
 public static class GolfGripNativeExport {
  [Serializable] class Manifest {public Rig rig;}
  [Serializable] class Rig {public string[] bones;public int trackCount;public Clip[] clips;}
  [Serializable] class Clip {public string id,name,equipment;public int frames,offset;public float length;public float[] times;}
  public static void Run(){try{foreach(bool female in new[]{false,true})Export(female);EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
  static void Export(bool female){
   string sex=female?"Female":"Male",folder=Environment.GetEnvironmentVariable("GRIP_NATIVE_OUT");
   var rig=JsonUtility.FromJson<Manifest>(File.ReadAllText(Path.Combine(folder,"GolfKitHero_"+sex+".json"))).rig;
   var raw=File.ReadAllBytes(Path.Combine(folder,sex+".before.raw"));
   var hero=HeroGolfer.Build(null,new HeroLook{Female=female,Haircut=0,Skin=new Color(.95f,.82f,.69f)});
   try{
    var look=hero.Root.GetComponent<MatchHeroLook>();var clips=Resources.LoadAll<AnimationClip>(HeroGolfer.PathFor(female));
    var equipment=hero.Root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r=>r.name.StartsWith("CLUB_")).ToArray();
    var flip=Matrix4x4.Scale(new Vector3(1,1,-1));int samples=0;
    var selected=rig.bones.Select((name,index)=>new{name,index}).Where(b=>b.name.StartsWith("Left")||b.name.StartsWith("Right")).Where(b=>b.name.EndsWith("UpperArm")||b.name.EndsWith("LowerArm")||b.name.EndsWith("Hand")||new[]{"Thumb","Index","Middle","Ring","Little"}.Any(d=>b.name.Contains(d))).ToArray();
    foreach(var motion in rig.clips.Where(c=>c.id.StartsWith("golf")||c.id=="serve")){
     var clip=clips.First(c=>c.name==motion.name);if(Mathf.Abs(clip.length-motion.length)>.0001f)throw new Exception("Native clip/source duration changed");
     foreach(var e in equipment)e.gameObject.SetActive(e.name.Equals("CLUB_"+motion.equipment,StringComparison.OrdinalIgnoreCase));
     var previous=new Quaternion[rig.trackCount];
     for(int frame=0;frame<motion.frames;frame++){
      float time=motion.times!=null&&motion.times.Length==motion.frames?motion.times[frame]:frame*motion.length/(motion.frames-1);
      clip.SampleAnimation(hero.Root,time);HeroGripPolish.ApplyGrip(look);
      foreach(var b in selected){
       var matrix=flip*(hero.Root.transform.worldToLocalMatrix*hero.Bones[b.name].localToWorldMatrix)*flip;
       Vector3 x=matrix.GetColumn(0),y=matrix.GetColumn(1),z=matrix.GetColumn(2);var scale=new Vector3(x.magnitude,y.magnitude,z.magnitude);if(Vector3.Dot(Vector3.Cross(x,y),z)<0)scale.x=-scale.x;
       var q=Quaternion.LookRotation(z/scale.z,y/scale.y);if(frame>0&&Quaternion.Dot(q,previous[b.index])<0)q=new Quaternion(-q.x,-q.y,-q.z,-q.w);previous[b.index]=q;Vector3 p=matrix.GetColumn(3);
       var values=new[]{p.x,p.y,p.z,q.x,q.y,q.z,q.w,scale.x,scale.y,scale.z};Buffer.BlockCopy(values,0,raw,motion.offset+(frame*rig.trackCount+b.index)*40,40);
      }
      samples++;
     }
    }
    File.WriteAllBytes(Path.Combine(folder,sex+".corrected.raw"),raw);Debug.Log($"[GolfGripNativeExport] {sex} {samples} native samples; {selected.Length} arm/hand tracks; existing sample times and other bytes preserved");
   }finally{UnityEngine.Object.DestroyImmediate(hero.Root);}
  }
 }
}
#endif
