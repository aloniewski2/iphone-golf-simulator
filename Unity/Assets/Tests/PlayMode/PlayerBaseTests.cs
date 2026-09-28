using System;
using System.Collections;
using System.IO;
using System.Collections.Generic;
using GolfArcade.Game;
using GolfArcade.Tennis;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace GolfArcade.PlayTests {
 public class PlayerBaseTests {
  [UnityTest,Timeout(180000)] public IEnumerator BasesKeepEquipmentAtBothSizeExtremes() {
   yield return SceneManager.LoadSceneAsync("Tennis",LoadSceneMode.Single);yield return null;
   var game=Object.FindFirstObjectByType<TennisGame>();game.ManualSimulation=true;game.enabled=false;
   var root=new GameObject("Base review");
   var camera=new GameObject("Base camera").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.08f,.12f,.18f);camera.farClipPlane=10;camera.fieldOfView=34;
   var light=new GameObject("Base light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=.8f;light.transform.rotation=Quaternion.Euler(35,-30,0);
   root.transform.position=new Vector3(0,50,0);
   try {
    foreach(bool female in new[]{false,true}) foreach(float size in new[]{0f,.5f,1f}) foreach(bool left in new[]{false,true}) {
     var go=new GameObject("Test tennis base");go.transform.SetParent(root.transform,false);var actor=go.AddComponent<TennisActor>();actor.Build(female,GolferStyle.SkinTones[2],left,TennisGame.PlayerBody(female));
     var kit=TennisLook.Kit.From("2885B5","172C4A","EEEEEE","F4AB38",2);
     actor.Customize(2,female?3:1,1,0,2,2,size,kit);
     float maxGrip=0,minReach=10,maxReach=0;
     var bones=actor.GetComponentsInChildren<Transform>();
     foreach(var kind in new[]{TennisActor.Stroke.Drive,TennisActor.Stroke.Serve,TennisActor.Stroke.Volley}) {
      actor.Swing(.65f,kind==TennisActor.Stroke.Volley,kind);
      for(int frame=0;frame<90;frame++) {
       actor.Tick(1f/60,frame<25?2:0);maxGrip=Mathf.Max(maxGrip,actor.RacketGripError);
       foreach(var side in new[]{"L","R"}) {
        var wrist=Array.Find(bones,t=>t.name=="Hand."+side);var shoulder=Array.Find(bones,t=>t.name=="UpperArm."+side);
        float reach=Vector3.Distance(wrist.position,shoulder.position);minReach=Mathf.Min(minReach,reach);maxReach=Mathf.Max(maxReach,reach);
       }
       if(frame%15==0)AssertWristCoverage(actor.gameObject);
       if(frame==30 && !left) {
        // Unity caches skinned rendering once per frame. Let it consume this pose
        // before comparing the skin with separately rendered hair and equipment.
        yield return null;
        Capture(camera,root.transform.position,new Vector3(0,1.1f,3.5f),$"tennis-{female}-{size}-{kind}");
       }
      }
     }
     Assert.Less(maxGrip,.015f,"Racket left the supplied hand");Assert.Less(maxReach,.85f,"Exploded reach");
     foreach(var r in actor.GetComponentsInChildren<SkinnedMeshRenderer>())Assert.Less(r.bounds.size.magnitude,4,"Exploded player mesh");
     Debug.Log($"BASE TENNIS female={female} size={size} left={left} reach={minReach:F3}..{maxReach:F3} grip={maxGrip:F5}");
     Object.Destroy(go);yield return null;
     if(left)continue;
     GolferStyle.Body=female?GolferStyle.BodyKind.Female:GolferStyle.BodyKind.Male;GolferStyle.Size=size;GolferStyle.Hair=female?3:1;GolferStyle.SkinTone=2;GolferStyle.Outfit=kit;
     var golfer=GolferView.Create(root.transform);golfer.ShowLoad(1);
     var show=typeof(GolferView).GetMethod("Show",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
     show.Invoke(golfer,new object[]{1.77f});
     golfer.Strike();
     var grip=golfer.GetComponentInChildren<StandardGolfGrip>();Assert.IsTrue(grip.IsReady);
     for(int i=0;i<160;i++) {
      show.Invoke(golfer,new object[]{i/159f*3});
      if(i%10==0){AssertSeams(golfer);AssertWristCoverage(golfer.gameObject);}
      Assert.Less(grip.MaximumHandleError,.001f,"Golf hands left the shared grip");
      if(i==65 && !left) {yield return null;Capture(camera,root.transform.position,new Vector3(3.4f,1.2f,3.4f),$"golf-{female}-{size}");}
     }
     Object.Destroy(golfer.gameObject);yield return null;
    }
   } finally {Object.Destroy(root);Object.Destroy(camera.gameObject);Object.Destroy(light.gameObject);GolferStyle.Size=.5f;}
  }
  static void AssertWristCoverage(GameObject character) {
   var r=Array.Find(character.GetComponentsInChildren<SkinnedMeshRenderer>(),m=>m.name.StartsWith("V4 Higgs body"));
   Assert.IsNotNull(r);
   var mesh=r.sharedMesh;var baked=new Mesh();r.BakeMesh(baked,true);
   var rest=mesh.vertices;var posed=baked.vertices;var weights=mesh.boneWeights;
   foreach(var side in new[]{"L","R"}) {
    int bone=Array.FindIndex(r.bones,b=>b.name=="Hand."+side);Assert.GreaterOrEqual(bone,0);
    int count=0;Vector3 sum=Vector3.zero;Vector3 expected=Vector3.zero;
    for(int i=0;i<rest.Length;i++) {
     var w=weights[i];
     if(w.boneIndex0!=bone||w.weight0<.999f)continue;
     var local=mesh.bindposes[bone].MultiplyPoint3x4(rest[i]);

     count++;sum+=r.transform.TransformPoint(posed[i]);expected+=r.bones[bone].TransformPoint(local);
    }
    Assert.GreaterOrEqual(count,12,"No connected sleeve-to-wrist surface for "+side);
    float gap=Vector3.Distance(sum/count,r.bones[bone].position);
    if(gap>.018f)Debug.Log($"WRIST {side} count={count} gap={gap} expected={expected/count} actual={sum/count} wrist={r.bones[bone].position} scale={r.bones[bone].lossyScale}");
    Assert.Less(gap,.018f,"Arm surface ends short of wrist "+side);
   }
   Object.Destroy(baked);
  }
  static void AssertSeams(GolferView golfer) {
   var r=Array.Find(golfer.GetComponentsInChildren<SkinnedMeshRenderer>(),m=>m.name.StartsWith("V4 Higgs body"));
   var vertices=r.sharedMesh.vertices;var baked=new Mesh();r.BakeMesh(baked,true);var posed=baked.vertices;
   var first=new Dictionary<Vector3Int,int>();
   for(int i=0;i<vertices.Length;i++) {
    var v=vertices[i];var key=new Vector3Int(Mathf.RoundToInt(v.x*100000),Mathf.RoundToInt(v.y*100000),Mathf.RoundToInt(v.z*100000));
    if(first.TryGetValue(key,out var j))Assert.Less(Vector3.Distance(posed[i],posed[j]),.001f,"UV seam split during golf swing");
    else first[key]=i;
   }
   Object.Destroy(baked);
  }
  static void Capture(Camera camera,Vector3 target,Vector3 offset,string name) {
   camera.transform.position=target+offset;camera.transform.LookAt(target+Vector3.up*.9f);
   var rt=new RenderTexture(720,900,24);camera.targetTexture=rt;camera.Render();var previous=RenderTexture.active;RenderTexture.active=rt;
   var image=new Texture2D(720,900,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,720,900),0,0);image.Apply();Directory.CreateDirectory("/tmp/player-base-review");File.WriteAllBytes("/tmp/player-base-review/"+name+".png",image.EncodeToPNG());RenderTexture.active=previous;camera.targetTexture=null;Object.Destroy(image);Object.Destroy(rt);
  }
 }
}
