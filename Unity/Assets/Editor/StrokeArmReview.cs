#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using GolfArcade.Tennis;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;
namespace GolfArcade.EditorTools {
 [InitializeOnLoad] public static class StrokeArmReview {
  const string Flag="StrokeArmReview"; static IEnumerator script; static int last=-1; static string output;
  static readonly CultureInfo Inv=CultureInfo.InvariantCulture;
  static StrokeArmReview(){EditorApplication.update+=Tick;}
  public static void Run(){output=Environment.GetEnvironmentVariable("STROKE_BREAKDOWN_OUT");if(string.IsNullOrEmpty(output))throw new Exception("STROKE_BREAKDOWN_OUT required");Directory.CreateDirectory(output);SessionState.SetString(Flag+"out",output);SessionState.SetBool(Flag,true);EditorSceneManager.OpenScene("Assets/Scenes/Tennis.unity");EditorApplication.isPlaying=true;}
  static void Tick(){if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying||last==Time.frameCount)return;last=Time.frameCount;var game=Object.FindFirstObjectByType<TennisGame>();if(!game||!game.Initialized)return;try{output=SessionState.GetString(Flag+"out","");if(script==null)script=Capture(game);if(!script.MoveNext())Finish(0);}catch(Exception e){Debug.LogException(e);File.WriteAllText(Path.Combine(output,"error.txt"),e.ToString());Finish(1);}}
  static void Finish(int code){SessionState.SetBool(Flag,false);Time.captureFramerate=0;EditorApplication.Exit(code);}
  static string F(float v)=>v.ToString("0.000000",Inv);
  static string V(Vector3 v)=>F(v.x)+","+F(v.y)+","+F(v.z);
  static void Shot(Camera cam,string path,Vector3 from,Vector3 at){var p=cam.transform.position;var q=cam.transform.rotation;float fov=cam.fieldOfView,aspect=cam.aspect;var old=cam.targetTexture;var active=RenderTexture.active;cam.transform.SetPositionAndRotation(from,Quaternion.LookRotation(at-from));cam.fieldOfView=38;cam.aspect=1;var rt=RenderTexture.GetTemporary(720,720,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Default,4);cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;var t=new Texture2D(720,720,TextureFormat.RGB24,false);t.ReadPixels(new Rect(0,0,720,720),0,0);t.Apply();File.WriteAllBytes(path,t.EncodeToJPG(94));Object.DestroyImmediate(t);cam.targetTexture=old;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);cam.transform.SetPositionAndRotation(p,q);cam.fieldOfView=fov;cam.aspect=aspect;}
  static IEnumerator Capture(TennisGame game){
   Time.captureFramerate=60;var presentation=game.GetComponent<TennisPresentation>();if(presentation)presentation.Finish();game.ManualSimulation=true;game.AutoPlay=false;game.enabled=false;
   foreach(var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))c.enabled=false;
   var manifest=new List<string>{"sex,stroke,sourceClip,sourceAsset,lengthSeconds,contactSeconds,contactFrame,frameCount,fps"};
   foreach(bool female in new[]{false,true}){
    game.SelectCharacter(female);for(int n=0;n<5;n++)yield return null;
    var actor=game.Player;actor.enabled=false;var root=actor.transform;var driver=actor.GetComponentInChildren<HeroTennisDriver>();driver.enabled=false;var hero=driver.matchLook;if(!hero)throw new Exception("Expected current match hero");
    foreach(var skin in hero.GetComponentsInChildren<SkinnedMeshRenderer>(true))skin.updateWhenOffscreen=true;
    // Keep current court, lighting and materials. Isolate other actors only during this disposable Play-mode review.
    if(game.Opponent)game.Opponent.gameObject.SetActive(false);
    string sex=female?"female":"male";
    if(Environment.GetEnvironmentVariable("STROKE_HIDE_KIT")=="1")foreach(var kit in hero.kit)if(kit)kit.enabled=false;
    var src=hero.body.sharedMesh;var palette=hero.body.bones;var verts=src.vertices;var weights=src.boneWeights;var bind=src.bindposes;
    using(var bw=new BinaryWriter(File.Create(Path.Combine(output,sex+"_mesh.bin")))){
     bw.Write(verts.Length);bw.Write(palette.Length);bw.Write(src.triangles.Length);
     foreach(var v in verts){bw.Write(v.x);bw.Write(v.y);bw.Write(v.z);}
     foreach(var b in weights){bw.Write(b.boneIndex0);bw.Write(b.boneIndex1);bw.Write(b.boneIndex2);bw.Write(b.boneIndex3);bw.Write(b.weight0);bw.Write(b.weight1);bw.Write(b.weight2);bw.Write(b.weight3);}
     foreach(var b in bind)for(int r=0;r<4;r++)for(int c=0;c<4;c++)bw.Write(b[r,c]);
     foreach(int v in src.triangles)bw.Write(v);
    }
    File.WriteAllLines(Path.Combine(output,sex+"_bones.txt"),palette.Select(b=>b.name));
    foreach(var clip in new[]{HeroTennisDriver.Clip.Forehand,HeroTennisDriver.Clip.Backhand}){
     if(clip==HeroTennisDriver.Clip.Forehand&&Environment.GetEnvironmentVariable("STROKE_BACKHAND_ONLY")=="1")continue;
     float length=driver.LengthOf(clip),contact=driver.ContactOf(clip);if(length<=0||contact<=0)throw new Exception("Missing stroke "+clip);
     int count=Mathf.CeilToInt(length*60)+1;var slot=driver.slots.First(s=>s.id==clip);string dir=Path.Combine(output,sex+"_"+clip.ToString().ToLowerInvariant());Directory.CreateDirectory(Path.Combine(dir,"side"));Directory.CreateDirectory(Path.Combine(dir,"pov"));
     manifest.Add(string.Join(",",sex,clip,slot.clip.name,AssetDatabase.GetAssetPath(slot.clip),F(length),F(contact),Mathf.RoundToInt(contact*60),count,60));
     var rows=new List<string>{"frame,timeSeconds,relativeToContact,hipsX,hipsY,hipsZ,chestX,chestY,chestZ,rightHandX,rightHandY,rightHandZ,leftHandX,leftHandY,leftHandZ,leftFootX,leftFootY,leftFootZ,rightFootX,rightFootY,rightFootZ,stringsX,stringsY,stringsZ,leftGripErrorMetres"};
     Vector3 Local(HumanBodyBones b)=>root.InverseTransformPoint(hero.Bone(b).position);
     // Fixed root-relative cameras throughout each cycle; side is the hitting-side profile.
     float side=clip==HeroTennisDriver.Clip.Forehand?1:-1;
     var sideFrom=root.TransformPoint(side*4.5f,1.45f,.05f);var sideAt=root.TransformPoint(0,.90f,.20f);
     var povFrom=root.TransformPoint(.55f,2.10f,-3.7f);var povAt=root.TransformPoint(0,.95f,.35f);
     using(var matrices=new BinaryWriter(File.Create(Path.Combine(dir,"skin.bin"))))
     for(int frame=0;frame<count;frame++){
      float t=Mathf.Min(frame/60f,length);driver.Sample(clip,t);yield return null;
      foreach(var bone in palette){var mat=hero.body.transform.worldToLocalMatrix*bone.localToWorldMatrix;for(int r=0;r<4;r++)for(int c=0;c<4;c++)matrices.Write(mat[r,c]);}
      Shot(game.GameplayCamera,Path.Combine(dir,"side",$"f{frame:0000}.jpg"),sideFrom,sideAt);Shot(game.GameplayCamera,Path.Combine(dir,"pov",$"f{frame:0000}.jpg"),povFrom,povAt);
      if(new[]{0,12,24,32,38,40,48,56,64,72}.Contains(frame)&&Environment.GetEnvironmentVariable("STROKE_HIDE_KIT")=="1"){
       foreach(var kit in hero.kit)if(kit)kit.enabled=true;
       Directory.CreateDirectory(Path.Combine(dir,"clothed-side"));Directory.CreateDirectory(Path.Combine(dir,"clothed-pov"));
       Shot(game.GameplayCamera,Path.Combine(dir,"clothed-side",$"f{frame:0000}.jpg"),sideFrom,sideAt);Shot(game.GameplayCamera,Path.Combine(dir,"clothed-pov",$"f{frame:0000}.jpg"),povFrom,povAt);
       foreach(var kit in hero.kit)if(kit)kit.enabled=false;
      }
      rows.Add(string.Join(",",frame,F(t),F(t-contact),V(Local(HumanBodyBones.Hips)),V(Local(HumanBodyBones.Chest)),V(Local(HumanBodyBones.RightHand)),V(Local(HumanBodyBones.LeftHand)),V(Local(HumanBodyBones.LeftFoot)),V(Local(HumanBodyBones.RightFoot)),V(root.InverseTransformPoint(driver.StringCentre)),F(Vector3.Distance(hero.Bone(HumanBodyBones.LeftHand).TransformPoint(new Vector3(0,.075f*Vector3.Distance(hero.Bone(HumanBodyBones.RightUpperArm).position,hero.Bone(HumanBodyBones.RightLowerArm).position)/.2383f,0)),hero.racketGrip.position+hero.racketGrip.TransformDirection(hero.stringUpLocal)*(.105f*Vector3.Distance(hero.Bone(HumanBodyBones.RightUpperArm).position,hero.Bone(HumanBodyBones.RightLowerArm).position)/.2383f)))));
     }
     File.WriteAllLines(Path.Combine(dir,"trace.csv"),rows);Debug.Log("[StrokeArmReview] "+sex+" "+clip+" frames="+count+" contact="+contact);
    }
   }
   File.WriteAllLines(Path.Combine(output,"manifest.csv"),manifest);
   File.WriteAllText(Path.Combine(output,"capture-contract.txt"),"Current production match heroes and resolved Forehand/Backhand clips, sampled at every 1/60 second including endpoint. Fixed hitting-side profile and elevated behind-player shoulder view. Source-frame review via HeroTennisDriver.Sample, with garment finalization; isolated clips, not a live rally, so no ball/contact assist or locomotion blending. Contact marker is the driver's authored contact timestamp, not a filmed ball collision. Current court, lights, character assets and materials retained. Nothing saved to scenes or prefabs.\n");
  }
 }
}
#endif
