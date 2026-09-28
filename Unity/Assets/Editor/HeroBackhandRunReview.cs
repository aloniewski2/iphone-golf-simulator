using System;using System.IO;using System.Linq;using System.Collections.Generic;
using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;using GolfArcade.Tennis;using Object=UnityEngine.Object;
namespace GolfArcade.EditorTools {
[InitializeOnLoad] public static class HeroBackhandRunReview {
 const string R=HeroLookImporter.Root;static string Out=>Path.GetFullPath("../ArtDir/screenshots/backhand_run_playmode");
 static readonly string[] Names={"Ready","JumpServe","Forehand","Backhand","RunForward","RunRight","RunLeft","Volley","Smash"};
 static readonly int[] Inputs={-1,2,0,1,4,5,6,3,7};static bool started,posed;static int frame;static List<string> report;static float low,high,maxGrip,maxSocketAngle,minClearance;static float minOffArm,minHand,minKnee,maxKnee,minElbow,maxElbow;static Vector3 handLow,handHigh;static Vector3 expectedSocket;static Quaternion expectedSocketRotation;
 static HeroBackhandRunReview(){EditorApplication.update+=Tick;}
 public static void Run(){try{
  Directory.CreateDirectory(Out);AssetDatabase.Refresh();EditorSceneManager.OpenScene("Assets/Scenes/Hero01Look.unity");var hero=Object.FindFirstObjectByType<ModularHeroLook>();var clips=new Dictionary<string,AnimationClip>();
  foreach(var n in Names){string file=n=="Backhand"?"Hero_Backhand_v1":"Hero_Gameplay_"+n+"_v1";string path=R+"Models/"+(n=="Backhand"||n=="RunForward"||n=="RunLeft"?"BackhandRun/":"KneesOffArm/")+file+".fbx";var importer=(ModelImporter)AssetImporter.GetAtPath(path);var defs=importer.defaultClipAnimations;
   foreach(var c in defs){c.name=file;c.loopTime=n=="Ready";c.loopPose=false;c.lockRootPositionXZ=true;c.lockRootHeightY=true;c.lockRootRotation=true;c.keepOriginalPositionXZ=true;c.keepOriginalPositionY=true;c.keepOriginalOrientation=true;}
   if(importer.clipAnimations.Length==0||importer.clipAnimations[0].name!=defs[0].name){importer.clipAnimations=defs;importer.SaveAndReimport();}
   var clip=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview"));if(!clip.humanMotion)throw new Exception("Not Humanoid: "+n);clips[n]=clip;
  }
  var replacement=AssetDatabase.LoadAssetAtPath<GameObject>(R+"Models/KneesOffArm/Hero_01_KneeBody.fbx").GetComponentsInChildren<SkinnedMeshRenderer>().First(x=>x.name=="Body_Skin");
  var body=hero.GetComponentsInChildren<SkinnedMeshRenderer>().First(x=>x.name=="Body_Skin");
  var bones=hero.skeletonRoot.GetComponentsInChildren<Transform>().GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.First());
  body.sharedMesh=replacement.sharedMesh;body.bones=replacement.bones.Select(b=>bones[b.name]).ToArray();body.quality=SkinQuality.Bone4;
  hero.idle=clips["Ready"];hero.forehand=clips["Forehand"];hero.backhand=clips["Backhand"];hero.serve=clips["JumpServe"];hero.volley=clips["Volley"];hero.runForward=clips["RunForward"];hero.runRight=clips["RunRight"];hero.runLeft=clips["RunLeft"];hero.smash=clips["Smash"];
  var bind=JsonUtility.FromJson<HeroGameplayFixCapture.BindComparison>(File.ReadAllText(Path.GetFullPath("../ArtDir/screenshots/gameplay_fixed/bind_comparison.json")));
  var hand=hero.skeletonRoot.GetComponentsInChildren<Transform>().First(t=>t.name=="Hand.R");var prop=new GameObject("Hero_Racket_FixedSocket").transform;prop.SetParent(hand,false);prop.localPosition=bind.socket;prop.localRotation=Quaternion.Euler(bind.socketEuler);var unit=hand.lossyScale;prop.localScale=new Vector3(1/Mathf.Abs(unit.x),1/Mathf.Abs(unit.y),1/Mathf.Abs(unit.z));
  prop.gameObject.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(R+"Props/Hero_Racket.asset");prop.gameObject.AddComponent<MeshRenderer>().sharedMaterials=new[]{"Hero_Racket_Blue","Hero_Racket_Strings","Hero_Racket_Grip"}.Select(n=>AssetDatabase.LoadAssetAtPath<Material>(R+"Materials/"+n+".mat")).ToArray();hero.racketGrip=prop;
  var pos=hero.transform.position;hero.transform.position=Vector3.zero;PrefabUtility.SaveAsPrefabAsset(hero.gameObject,R+"Prefabs/Hero_01_BackhandRun.prefab");hero.transform.position=pos;
  var cam=Camera.main;cam.transform.position=new Vector3(13.25f,1.4f,5.5f);cam.transform.LookAt(new Vector3(12,1.08f,0));cam.fieldOfView=38;
  EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),"Assets/Scenes/Hero01BackhandRun.unity");AssetDatabase.SaveAssets();SessionState.SetBool("HeroBackhandRun",true);started=false;EditorApplication.isPlaying=true;
 }catch(Exception e){Fail(e);}}
 static void Tick(){if(!SessionState.GetBool("HeroBackhandRun",false)||!EditorApplication.isPlaying)return;try{
  var hero=Object.FindFirstObjectByType<ModularHeroLook>();if(!hero||(!started&&!hero.IdleRunning))return;
  if(!started){expectedSocket=hero.racketGrip.localPosition;expectedSocketRotation=hero.racketGrip.localRotation;minClearance=float.MaxValue;maxSocketAngle=0;minOffArm=minHand=minKnee=minElbow=float.MaxValue;maxKnee=maxElbow=0;handLow=Vector3.one*999;handHigh=Vector3.one*-999;started=true;frame=0;posed=false;report=new List<string>();low=float.MaxValue;high=float.MinValue;maxGrip=0;maxSocketAngle=0;minClearance=float.MaxValue;}
  int index=frame/120,local=frame%120;
  if(index>=Names.Length){File.WriteAllLines(Out+"/verification.txt",report);SessionState.SetBool("HeroBackhandRun",false);hero.enabled=false;if(Application.isBatchMode)EditorApplication.Exit(0);else EditorApplication.isPlaying=false;return;}
  if(!posed){hero.SampleForReview(Inputs[index],local/30.0);posed=true;return;}posed=false;
  if(!hero.animator.isHuman||!hero.animator.avatar.isValid)throw new Exception("Humanoid Avatar invalid");
  var hand=hero.animator.GetBoneTransform(HumanBodyBones.RightHand);if(hero.racketGrip.parent!=hand)throw new Exception("Racket lost fixed hand parent");
  if(hero.GetComponentsInChildren<SkinnedMeshRenderer>().Any(s=>s.bones.Any(b=>!b||!b.IsChildOf(hero.skeletonRoot))))throw new Exception("Wardrobe detached");
  float y=hero.animator.GetBoneTransform(HumanBodyBones.Hips).position.y;low=Mathf.Min(low,y);high=Mathf.Max(high,y);maxGrip=Mathf.Max(maxGrip,Vector3.Distance(hero.racketGrip.position,hand.TransformPoint(expectedSocket)));
  maxSocketAngle=Mathf.Max(maxSocketAngle,Quaternion.Angle(hero.racketGrip.localRotation,expectedSocketRotation));
  var ch=hero.animator.GetBoneTransform(HumanBodyBones.Chest);var hp=hero.animator.GetBoneTransform(HumanBodyBones.Hips);
  var le=hero.animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);var lh=hero.animator.GetBoneTransform(HumanBodyBones.LeftHand);
  var lu=hero.animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);float elbowAngle=Vector3.Angle(lu.position-le.position,lh.position-le.position);minElbow=Mathf.Min(minElbow,elbowAngle);maxElbow=Mathf.Max(maxElbow,elbowAngle);
  var relativeHand=hero.transform.InverseTransformPoint(lh.position)-hero.transform.InverseTransformPoint(lu.position);handLow=Vector3.Min(handLow,relativeHand);handHigh=Vector3.Max(handHigh,relativeHand);
  for(int sample=0;sample<=8;sample++){
   var point=Vector3.Lerp(le.position,lh.position,sample/8f);
   minOffArm=Mathf.Min(minOffArm,CapsuleDistance(point,hp.position+Vector3.up*.06f,ch.position+Vector3.up*.06f,.17f)-.05f);
  }
  minHand=Mathf.Min(minHand,CapsuleDistance(lh.position,hp.position+Vector3.up*.06f,ch.position+Vector3.up*.06f,.17f)-.055f);
  foreach(var leg in new[]{new[]{HumanBodyBones.LeftUpperLeg,HumanBodyBones.LeftLowerLeg,HumanBodyBones.LeftFoot},new[]{HumanBodyBones.RightUpperLeg,HumanBodyBones.RightLowerLeg,HumanBodyBones.RightFoot}}){
   var thigh=hero.animator.GetBoneTransform(leg[0]);var knee=hero.animator.GetBoneTransform(leg[1]);var ankle=hero.animator.GetBoneTransform(leg[2]);float angle=Vector3.Angle(thigh.position-knee.position,ankle.position-knee.position);minKnee=Mathf.Min(minKnee,angle);maxKnee=Mathf.Max(maxKnee,angle);
  }
  if(index==3 && local==33){
   var hoop=hero.transform.InverseTransformPoint(hero.racketGrip.TransformPoint(new Vector3(0,.395f,0)));
   var wrist=hero.transform.InverseTransformPoint(hand.position);
   File.WriteAllText(Out+"/backhand_contact.txt","frame=33; time=1.10s; anatomical left = negative model X; wrist="+wrist.ToString("F4")+"; hoop center="+hoop.ToString("F4"));
   if(hoop.x>=-.15f)throw new Exception("Backhand contact is not on LEFT side");
  }
  if(index==0 || (index>=4&&index<=6)){
   var hips=hero.animator.GetBoneTransform(HumanBodyBones.Hips);var chest=hero.animator.GetBoneTransform(HumanBodyBones.Chest);
   for(int k=0;k<32;k++){float a=k*Mathf.PI*2/32;var point=hero.racketGrip.TransformPoint(new Vector3(Mathf.Sin(a)*.135f,.395f+Mathf.Cos(a)*.18f,0));
    float clearance=CapsuleDistance(point,hips.position+Vector3.up*.07f,chest.position+Vector3.up*.07f,.18f)-.012f;
    foreach(var side in new[]{new[]{HumanBodyBones.LeftUpperLeg,HumanBodyBones.LeftLowerLeg},new[]{HumanBodyBones.RightUpperLeg,HumanBodyBones.RightLowerLeg}})clearance=Mathf.Min(clearance,CapsuleDistance(point,hero.animator.GetBoneTransform(side[0]).position,hero.animator.GetBoneTransform(side[1]).position,.085f)-.012f);
    minClearance=Mathf.Min(minClearance,clearance);
   }
  }
  if(!Environment.GetCommandLineArgs().Contains("-quickReview") || new[]{0,15,20,23,25,30,33,44,45,60,81,90,119}.Contains(local)) Capture(Out+"/frame_"+frame.ToString("D4")+".png");
  if(local==119){report.Add(Names[index]+": 120 frames, Humanoid + shared wardrobe + fixed socket PASS; hip vertical range="+(high-low).ToString("F4")+"m; fixed socket error="+maxGrip.ToString("F7")+"m / "+maxSocketAngle.ToString("F4")+"deg; hoop-to-torso/thigh capsule clearance="+(minClearance==float.MaxValue?"n/a":minClearance.ToString("F4")+"m")+"; off-arm surface proxy min="+minOffArm.ToString("F4")+"m; hand gap="+minHand.ToString("F4")+"m; knee interior angle="+minKnee.ToString("F1")+".."+maxKnee.ToString("F1")+"; left elbow angle="+minElbow.ToString("F1")+".."+maxElbow.ToString("F1")+"; hand relative travel="+(handHigh-handLow).ToString("F3"));minOffArm=minHand=minKnee=minElbow=float.MaxValue;maxKnee=maxElbow=0;handLow=Vector3.one*999;handHigh=Vector3.one*-999;low=float.MaxValue;high=float.MinValue;maxGrip=0;maxSocketAngle=0;minClearance=float.MaxValue;}
  frame++;
 }catch(Exception e){Fail(e);}}
 static float CapsuleDistance(Vector3 p,Vector3 a,Vector3 b,float radius){var axis=b-a;float t=Mathf.Clamp01(Vector3.Dot(p-a,axis)/Mathf.Max(.00001f,axis.sqrMagnitude));return Vector3.Distance(p,a+t*axis)-radius;}
 static void Capture(string path){var cam=Camera.main;var rt=new RenderTexture(960,960,24,RenderTextureFormat.ARGB32);rt.antiAliasing=4;var prev=RenderTexture.active;var old=cam.targetTexture;cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;var tex=new Texture2D(960,960,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,960,960),0,0);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());cam.targetTexture=old;RenderTexture.active=prev;Object.DestroyImmediate(tex);rt.Release();Object.DestroyImmediate(rt);}
 static void Fail(Exception e){Debug.LogException(e);Directory.CreateDirectory(Out);File.WriteAllText(Out+"/error.txt",e.ToString());SessionState.SetBool("HeroBackhandRun",false);if(Application.isBatchMode)EditorApplication.Exit(1);}
}}
