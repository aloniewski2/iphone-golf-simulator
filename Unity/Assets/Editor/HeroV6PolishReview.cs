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
[InitializeOnLoad] public static class HeroV6PolishReview {
 static int frame;static GameObject root;static HeroTennisDriver driver;static Camera camera;static string output;static List<string> log;
 static string pendingName;static int posedFrame;
 const string Key="HeroV6PolishReview";
 static readonly HeroTennisDriver.Clip[] clips={HeroTennisDriver.Clip.Ready,HeroTennisDriver.Clip.Serve,HeroTennisDriver.Clip.Forehand,HeroTennisDriver.Clip.Backhand,HeroTennisDriver.Clip.RunForward,HeroTennisDriver.Clip.RunRight,HeroTennisDriver.Clip.RunLeft,HeroTennisDriver.Clip.Volley,HeroTennisDriver.Clip.Smash};
 static HeroV6PolishReview(){EditorApplication.update+=Tick;}
 public static void Baseline(){Run("baseline");}
 
 
 static void Run(string folder){
  SessionState.SetString(Key+"Out",folder);SessionState.SetBool(Key,true);frame=-1;
  EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects,NewSceneMode.Single);EditorApplication.isPlaying=true;
 }
 static void Tick(){if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying)return;try{
  if(!root){
   output=Path.GetFullPath("../ArtDir/hero/v6_proof/foundation5/Unity");Directory.CreateDirectory(output);if(File.Exists(output+"/error.txt")){File.Copy(output+"/error.txt",output+"/previous-capture-error.txt",true);File.Delete(output+"/error.txt");}log=new List<string>();
   var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(HeroV6PolishImport.Prefab);root=Object.Instantiate(prefab);root.transform.position=Vector3.zero;
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
   int clip=frame/4;float time=driver.LengthOf(clips[clip])*(new[]{.05f,.30f,.55f,.80f}[frame%4]);driver.Sample(clips[clip],time);root.GetComponent<HeroV6KneeCorrectives>()?.Apply();
   var look=driver.look;var a=look.animator;var joints=new[]{HumanBodyBones.LeftUpperLeg,HumanBodyBones.LeftLowerLeg,HumanBodyBones.LeftFoot,HumanBodyBones.RightUpperLeg,HumanBodyBones.RightLowerLeg,HumanBodyBones.RightFoot,HumanBodyBones.LeftUpperArm,HumanBodyBones.LeftLowerArm,HumanBodyBones.LeftHand,HumanBodyBones.RightUpperArm,HumanBodyBones.RightLowerArm,HumanBodyBones.RightHand};
   foreach(var j in joints){var p=a.GetBoneTransform(j).position;if(float.IsNaN(p.x)||float.IsNaN(p.y)||float.IsNaN(p.z))throw new Exception("Invalid joint "+j);}
   var upper=a.GetBoneTransform(HumanBodyBones.LeftUpperLeg);var knee=a.GetBoneTransform(HumanBodyBones.LeftLowerLeg);var foot=a.GetBoneTransform(HumanBodyBones.LeftFoot);
   float angle=Vector3.Angle(upper.position-knee.position,foot.position-knee.position);log.Add($"{clips[clip]} t={time:F3} left_knee={angle:F1}deg");
   Frame(clips[clip]+"_"+(frame%4),new Vector3(2.5f,1.2f,4.6f),new Vector3(0,.85f,0));return;
  }
  if(frame<68){
   int cut=((frame-36)/4)%2==0?0:5;int hat=(frame-36)%4;
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
   if(!hasBuild)throw new Exception("Build morph missing");
   log.Add($"Female={style.Female} size={style.BodySize} build_morph={hasBuild}");Frame("body_"+(style.Female?"girl":"boy")+"_size_"+((frame-68)%3),new Vector3(2.5f,1.2f,4.6f),new Vector3(0,.85f,0));return;
  }
  if(frame<86){
   var style=HeroKit.Style.Locked;HeroKit.Apply(driver,style);driver.Sample(HeroTennisDriver.Clip.Ready,.6f);
   int f=frame-74;
   if(f<4){float a=f*90*Mathf.Deg2Rad;Frame("turn_"+new[]{"front","side","back","other_side"}[f],new Vector3(Mathf.Sin(a)*5.1f,1.2f,Mathf.Cos(a)*5.1f),new Vector3(0,.85f,0));return;}
   if(f<7){int level=f-4;foreach(var lod in root.GetComponentsInChildren<HeroV6PolishHairLOD>(true))lod.forceLOD=level;Frame("hair_LOD"+level,new Vector3(.7f,1.60f,1.95f),new Vector3(0,1.40f,0));return;}
   foreach(var lod in root.GetComponentsInChildren<HeroV6PolishHairLOD>(true))lod.forceLOD=0;
   if(f==7){Frame("knees_feet",new Vector3(.55f,.45f,2.25f),new Vector3(0,.36f,0));return;}
   if(f==8){style.Haircut=5;style.Headwear=0;HeroKit.Apply(driver,style);Frame("hair_off_front",new Vector3(.3f,1.6f,1.85f),new Vector3(0,1.4f,0));return;}
   if(f==9){style.Haircut=5;style.Headwear=0;HeroKit.Apply(driver,style);Frame("hair_off_back",new Vector3(-.3f,1.6f,-1.85f),new Vector3(0,1.4f,0));return;}
   if(f==10){driver.Sample(HeroTennisDriver.Clip.Serve,driver.LengthOf(HeroTennisDriver.Clip.Serve)*.55f);Frame("serve_armpit_back",new Vector3(-1.3f,1.7f,-2.8f),new Vector3(0,1.1f,0));return;}
   Frame("customize_closeup",new Vector3(.7f,1.6f,1.95f),new Vector3(0,1.4f,0));return;
  }
  if(frame<90){
   HeroKit.Apply(driver,HeroKit.Style.Locked);var correction=root.GetComponent<HeroV6KneeCorrectives>();
   correction.applyCorrectives=(frame%2)==1;driver.Sample(frame<88?HeroTennisDriver.Clip.Ready:HeroTennisDriver.Clip.RunForward,frame<88?.6f:driver.LengthOf(HeroTennisDriver.Clip.RunForward)*.55f);correction.Apply();
   if(!correction.HasCorrectives)throw new Exception("Knee morphs were lost during import");
   log.Add($"CORRECTIVE enabled={correction.applyCorrectives} knee_L={correction.LeftBend:F1} knee_R={correction.RightBend:F1}");
   Frame((frame<88?"ready_knees_":"run_knees_")+(correction.applyCorrectives?"corrected":"linear"),new Vector3(.55f,.45f,2.25f),new Vector3(0,.36f,0));return;
  }
  if(frame<94){
   var contract=root.GetComponent<HeroV6PolishContract>();var look=driver.look;
   if(frame==90){look.Equip(ModularHeroLook.Slot.Hair,look.defaults.First(x=>x.slot==ModularHeroLook.Slot.Hair).asset);
    var slot=root.GetComponentsInChildren<Transform>().First(t=>t.name=="Slot_Hair");
    if(!slot.Find("Hair")||slot.GetComponentsInChildren<HeroV6PolishHairLOD>().Length==0)throw new Exception("Direct wardrobe replacement lost aliases or LOD hooks");log.Add("DIRECT_HAIR_SWAP aliases_and_LOD=PASS");}
   var style=HeroKit.Style.Locked;if(frame==91)style.Shorts=HeroKit.Hex("5B8CFF");HeroKit.Apply(driver,style);driver.Sample(HeroTennisDriver.Clip.Ready,.6f);
   var shorts=root.GetComponentsInChildren<SkinnedMeshRenderer>().First(r=>r.name=="Shorts_Default").sharedMaterials.First(m=>m.name.StartsWith("Hero_V6_ShortsNavy"));
   Color expected=frame==91?style.Shorts:HeroKit.Hex("1E2A5A");if(Vector4.Distance(shorts.GetColor("_BaseColor"),expected)>.001f)throw new Exception("Shorts tint/reset failed");
   if(frame==93){var rt=contract.KitAtlas;int mats=contract.OwnedKitMaterialCount;for(int i=0;i<25;i++)HeroKit.Apply(driver,HeroKit.Style.Locked);
    if(contract.KitAtlas!=rt||contract.OwnedKitMaterialCount!=mats)throw new Exception("Repeated customization allocated new persistent resources");log.Add($"REPEAT_CUSTOMIZE calls=25 same_atlas=PASS materials={mats} stable_material_count=PASS");}
   Frame(frame==91?"shorts_blue":frame==92?"shorts_default_reset":frame==90?"direct_hair_swap":"customize_resource_reuse",new Vector3(2.5f,1.2f,4.6f),new Vector3(0,.85f,0));return;
  }
  root.GetComponent<HeroV6KneeCorrectives>().applyCorrectives=true;
  var audit=new List<string>();int visibleTris=0;
  foreach(var rr in root.GetComponentsInChildren<Renderer>(true)){
   if(!rr.enabled||!rr.gameObject.activeInHierarchy)continue;
   var mf=rr.GetComponent<MeshFilter>();Mesh mm=rr is SkinnedMeshRenderer sk?sk.sharedMesh:(mf?mf.sharedMesh:null);if(mm)visibleTris+=mm.triangles.Length/3;
   foreach(var material in rr.sharedMaterials)if(material)audit.Add($"{rr.name}: material={material.name} shader={material.shader.name} queue={material.renderQueue} surface={(material.HasProperty("_Surface")?material.GetFloat("_Surface"):-1)} alphaClip={(material.HasProperty("_AlphaClip")?material.GetFloat("_AlphaClip"):-1)}");
  }
  audit.Add("Runtime visible triangles (including procedural eyes/prop/occluder)="+visibleTris);
  foreach(var t in root.GetComponentsInChildren<Transform>(true).Where(x=>new[]{"Hair","Visor","Top","Bottom","Shoes","Racket"}.Contains(x.name))){var path=t.name;var pp=t.parent;while(pp&&pp!=root.transform){path=pp.name+"/"+path;pp=pp.parent;}audit.Add("SOCKET "+path);}
  File.WriteAllLines(output+"/runtime-materials-and-sockets.txt",audit);
  foreach(var alias in new[]{"Hair","Visor","Top","Bottom","Shoes","Racket"})if(!root.GetComponentsInChildren<Transform>().Any(t=>t.name==alias))throw new Exception("Missing active contract alias "+alias);
  foreach(var r in root.GetComponentsInChildren<SkinnedMeshRenderer>(true)){
   if(r.sharedMesh.bindposes.Length!=r.bones.Length||r.bones.Any(b=>!b||!b.IsChildOf(driver.look.skeletonRoot)))throw new Exception("Broken binding "+r.name);
   foreach(var w in r.sharedMesh.boneWeights)if(Mathf.Abs(w.weight0+w.weight1+w.weight2+w.weight3-1)>.001f)throw new Exception("Unnormalized weight "+r.name);
  }
  foreach(var female in new[]{false,true}){
   foreach(var r in root.GetComponentsInChildren<SkinnedMeshRenderer>(true)){HeroKit.SetFemale(r,female);HeroKit.SetBuild(r,.5f);}
   foreach(var clip in clips){
    int samples=Mathf.CeilToInt(driver.LengthOf(clip)*60);float movement=0;Vector3 previous=Vector3.zero;
    for(int i=0;i<=samples;i++){
     driver.Sample(clip,i/60f);root.GetComponent<HeroV6KneeCorrectives>().Apply();var a=driver.look.animator;var wrist=a.GetBoneTransform(HumanBodyBones.RightHand).position;var knee=a.GetBoneTransform(HumanBodyBones.LeftLowerLeg).position;
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
