using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using GolfArcade.Tennis;
using Object=UnityEngine.Object;
namespace GolfArcade.EditorTools {
[InitializeOnLoad] public static class HeroV6PolishBeforeReview {
 static int frame;static GameObject root;static HeroTennisDriver driver;static Camera camera;static string output;static List<string> log;
 static string pendingName;static int posedFrame;
 const string Key="HeroV6PolishBeforeReview";
 static readonly HeroTennisDriver.Clip[] clips={HeroTennisDriver.Clip.Ready,HeroTennisDriver.Clip.Serve,HeroTennisDriver.Clip.Forehand,HeroTennisDriver.Clip.Backhand,HeroTennisDriver.Clip.RunForward,HeroTennisDriver.Clip.RunRight,HeroTennisDriver.Clip.RunLeft,HeroTennisDriver.Clip.Volley,HeroTennisDriver.Clip.Smash};
 static HeroV6PolishBeforeReview(){EditorApplication.update+=Tick;}
 public static void Baseline(){Run("baseline");}
 
 
 static void Run(string folder){
  SessionState.SetString(Key+"Out",folder);SessionState.SetBool(Key,true);frame=-1;
  EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects,NewSceneMode.Single);EditorApplication.isPlaying=true;
 }
 static void Tick(){if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying)return;try{
  if(!root){
   output=Path.GetFullPath("../ArtDir/hero/v6_target/BEFORE/Unity");Directory.CreateDirectory(output);log=new List<string>();
   var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(SessionState.GetString(Key+"Out","")=="after"?"Assets/ArtDirection/HeroV5AstraTex/Hero_V5_AstraTex_Baseline.prefab":"Assets/Resources/Tennis/Hero/Hero_01_Tennis.prefab");root=Object.Instantiate(prefab);root.transform.position=Vector3.zero;
   driver=root.GetComponent<HeroTennisDriver>();driver.Build();HeroKit.Apply(driver,HeroKit.Style.Locked);
   camera=Camera.main;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.85f,.90f,.94f);camera.fieldOfView=35;
   var sun=Object.FindFirstObjectByType<Light>();sun.intensity=1.35f;sun.color=new Color(1,.95f,.86f);sun.transform.rotation=Quaternion.Euler(40,-35,0);
   var fill=new GameObject("Review fill").AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=.5f;fill.color=new Color(.84f,.92f,1);fill.transform.rotation=Quaternion.Euler(35,140,0);
   foreach(var r in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))r.quality=SkinQuality.Bone4;
   frame=0;return;
  }
  // Let Unity update GPU skinning after the manually sampled pose. Capturing immediately in an
  // EditorApplication tick mixed last-frame body skinning with newly enabled haircut renderers.
  if(pendingName!=null){if(Time.frameCount<=posedFrame)return;Capture(pendingName);pendingName=null;frame++;return;}
  if(frame<36){
   int clip=frame/4;float time=driver.LengthOf(clips[clip])*(new[]{.05f,.30f,.55f,.80f}[frame%4]);driver.Sample(clips[clip],time);
   var look=driver.look;var a=look.animator;var joints=new[]{HumanBodyBones.LeftUpperLeg,HumanBodyBones.LeftLowerLeg,HumanBodyBones.LeftFoot,HumanBodyBones.RightUpperLeg,HumanBodyBones.RightLowerLeg,HumanBodyBones.RightFoot,HumanBodyBones.LeftUpperArm,HumanBodyBones.LeftLowerArm,HumanBodyBones.LeftHand,HumanBodyBones.RightUpperArm,HumanBodyBones.RightLowerArm,HumanBodyBones.RightHand};
   foreach(var j in joints){var p=a.GetBoneTransform(j).position;if(float.IsNaN(p.x)||float.IsNaN(p.y)||float.IsNaN(p.z))throw new Exception("Invalid joint "+j);}
   var upper=a.GetBoneTransform(HumanBodyBones.LeftUpperLeg);var knee=a.GetBoneTransform(HumanBodyBones.LeftLowerLeg);var foot=a.GetBoneTransform(HumanBodyBones.LeftFoot);
   float angle=Vector3.Angle(upper.position-knee.position,foot.position-knee.position);log.Add($"{clips[clip]} t={time:F3} left_knee={angle:F1}deg");
   Frame(clips[clip]+"_"+(frame%4),new Vector3(2.5f,1.2f,4.6f),new Vector3(0,.85f,0));return;
  }
  if(frame<68){
   int cut=(frame-36)/4;int hat=(frame-36)%4;
   var style=HeroKit.Style.Locked;style.Haircut=cut;style.Headwear=hat;
   HeroKit.Apply(driver,style);driver.Sample(HeroTennisDriver.Clip.Ready,.6f);
   if(frame==36){
    var head=driver.look.animator.GetBoneTransform(HumanBodyBones.Head);log.Add("Head position="+head.position+" scale="+head.lossyScale);
    foreach(var r in root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r=>r.name=="Body_Skin"||r.name=="Hair_Default_Free"||r.name=="Hair_Default")){
     var baked=new Mesh();r.BakeMesh(baked,false);var v=baked.vertices;var w=r.sharedMesh.boneWeights;Vector3 centre=Vector3.zero;int n=0;
     for(int i=0;i<v.Length;i++)if(w[i].weight0>.95f&&r.bones[w[i].boneIndex0]==head){centre+=r.transform.position+r.transform.rotation*v[i];n++;}
     int hi=Array.IndexOf(r.bones,head);log.Add(r.name+" head-centroid="+(centre/Mathf.Max(1,n))+" renderer="+r.transform.position+" rotation="+r.transform.eulerAngles+" scale="+r.transform.lossyScale+" n="+n+" bind="+(hi>=0?r.sharedMesh.bindposes[hi].ToString():"missing"));Object.Destroy(baked);
    }
   }
   int visible=root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Count(r=>r.enabled&&r.name.StartsWith("Hair_")&&!r.name.StartsWith("Hair_BandFill"));
   log.Add($"Hair={cut} Headwear={style.Headwear} visible_hair={visible}");if(visible!=1)throw new Exception("Hair selection not singular");
   Frame("hair_"+cut+"_hat_"+style.Headwear,new Vector3(-.8f,1.6f,-1.65f),new Vector3(0,1.4f,0));return;
  }
  if(frame<74){
   var style=HeroKit.Style.Locked;style.Female=(frame-68)/3==1;style.BodySize=((frame-68)%3)*.5f;HeroKit.Apply(driver,style);driver.Sample(HeroTennisDriver.Clip.Ready,.6f);
   var body=root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r=>r.name=="Body_Skin");
   bool hasBuild=Enumerable.Range(0,body.sharedMesh.blendShapeCount).Any(i=>body.sharedMesh.GetBlendShapeName(i).EndsWith("BuildSlim"));
   if(SessionState.GetString(Key+"Out","")!="baseline"&&!hasBuild)throw new Exception("Build morph missing");
   log.Add($"Female={style.Female} size={style.BodySize} build_morph={hasBuild}");Frame("body_"+(style.Female?"girl":"boy")+"_size_"+((frame-68)%3),new Vector3(2.5f,1.2f,4.6f),new Vector3(0,.85f,0));return;
  }
  foreach(var r in root.GetComponentsInChildren<SkinnedMeshRenderer>(true)){
   if(r.sharedMesh.bindposes.Length!=r.bones.Length||r.bones.Any(b=>!b||!b.IsChildOf(driver.look.skeletonRoot)))throw new Exception("Broken binding "+r.name);
   foreach(var w in r.sharedMesh.boneWeights)if(Mathf.Abs(w.weight0+w.weight1+w.weight2+w.weight3-1)>.001f)throw new Exception("Unnormalized weight "+r.name);
  }
  foreach(var female in new[]{false,true}){
   foreach(var r in root.GetComponentsInChildren<SkinnedMeshRenderer>(true)){HeroKit.SetFemale(r,female);HeroKit.SetBuild(r,.5f);}
   foreach(var clip in clips){
    int samples=Mathf.CeilToInt(driver.LengthOf(clip)*60);float movement=0;Vector3 previous=Vector3.zero;
    for(int i=0;i<=samples;i++){
     driver.Sample(clip,i/60f);var a=driver.look.animator;var wrist=a.GetBoneTransform(HumanBodyBones.RightHand).position;var knee=a.GetBoneTransform(HumanBodyBones.LeftLowerLeg).position;
     if(float.IsNaN(wrist.x)||float.IsNaN(knee.x))throw new Exception("Nonfinite full-clip pose "+clip);
     if(i>0)movement+=(wrist-previous).magnitude;previous=wrist;
     if(i%15==0){var body=root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r=>r.name=="Body_Skin");var baked=new Mesh();body.BakeMesh(baked,false);
      if(baked.vertices.Any(v=>!float.IsFinite(v.x)||!float.IsFinite(v.y)||!float.IsFinite(v.z)||v.magnitude>10))throw new Exception("Exploding skin "+clip);Object.Destroy(baked);}
    }
    if(movement<.005f)throw new Exception("Frozen clip "+clip);log.Add($"FULL_CLIP Female={female} {clip} samples={samples+1} wrist_path={movement:F3}m PASS");
   }
  }
  File.WriteAllLines(output+"/motion-and-swaps.txt",log);SessionState.SetBool(Key,false);EditorApplication.Exit(0);
 }catch(Exception ex){Debug.LogException(ex);if(output!=null)File.WriteAllText(output+"/error.txt",ex.ToString());SessionState.SetBool(Key,false);EditorApplication.Exit(1);}}
 static void Frame(string name,Vector3 position,Vector3 target){
  camera.transform.position=position;camera.transform.LookAt(target);pendingName=name;posedFrame=Time.frameCount;
 }
 static void Capture(string name){
  var rt=new RenderTexture(960,960,24);rt.antiAliasing=4;camera.targetTexture=rt;camera.Render();var old=RenderTexture.active;RenderTexture.active=rt;
  var tex=new Texture2D(960,960,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,960,960),0,0);tex.Apply();File.WriteAllBytes(output+"/"+name+".png",tex.EncodeToPNG());camera.targetTexture=null;RenderTexture.active=old;rt.Release();Object.Destroy(tex);Object.Destroy(rt);
 }
}
}
