using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using GolfArcade.Game;
using GolfArcade.Course;
using GolfArcade.Shot;
using GolfArcade.Tennis;
using GolfArcade.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace GolfArcade.PlayTests {
 public class GolfPolishTests {
  static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
  static string Proof=>Environment.GetEnvironmentVariable("GOLF_PRESENTATION_PROOF")??"Library/Captures/golf-polish";
  static void Capture(Camera cam,string name) {
   Directory.CreateDirectory(Proof);var rt=RenderTexture.GetTemporary(1440,810,24,RenderTextureFormat.Default,RenderTextureReadWrite.Default,4);var previous=cam.targetTexture;var active=RenderTexture.active;
   var tex=new Texture2D(1440,810,TextureFormat.RGB24,false);
   try {cam.targetTexture=rt;Canvas.ForceUpdateCanvases();cam.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1440,810),0,0);tex.Apply();File.WriteAllBytes(Path.Combine(Proof,name+".png"),tex.EncodeToPNG());}
   finally{cam.targetTexture=previous;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);Object.Destroy(tex);}
  }
  [UnityTest,Timeout(240000)]
  public IEnumerator DetailedBodiesAndEveryClubClearTheHead() {
   var original=GolferStyle.Current.Clone();TennisQuality.Apply();Time.timeScale=1;
   yield return SceneManager.LoadSceneAsync("Golf",LoadSceneMode.Single);yield return null;
   var game=Object.FindFirstObjectByType<GolfGame>();game.PrepareNativeAddress("postcards");
   var golfer=game.GetComponentInChildren<GolferView>(true);var cam=game.GameplayCamera;cam.aspect=16f/9;
   var rig=cam.GetComponent<CameraRig>();var show=typeof(GolferView).GetMethod("Show",Private);
   var checks=new System.Text.StringBuilder();
   try {
    foreach(bool female in new[]{false,true}) {
     GolferStyle.Body=female?GolferStyle.BodyKind.Female:GolferStyle.BodyKind.Male;golfer.ApplyStyle();game.DropBall(game.CurrentHole.Tee);yield return null;yield return null;
     string sex=female?"female":"male";
     var look=golfer.Hero.Root.GetComponent<MatchHeroLook>();Assert.That(look,Is.Not.Null);
     Assert.That(look.body.sharedMaterial.shader.name,Is.EqualTo("GolfArcade/TennisCharacter"));
     Assert.That(look.body.sharedMesh.uv2.Length,Is.EqualTo(look.body.sharedMesh.vertexCount));
     Assert.That(golfer.GetComponentsInChildren<Renderer>().Where(r=>r.enabled&&r.name.StartsWith("Hair_")).Count(),Is.Zero);
     foreach(var r in look.kit)foreach(var m in r.sharedMaterials){Assert.That(m.shader.name,Is.EqualTo("GolfArcade/TennisCloth"));Assert.That(m.GetFloat("_UseGarmentMaps"),Is.EqualTo(1),r.name);Assert.That(m.GetTexture("_NormalMap"),Is.Not.Null);}
     var topRenderer=look.kit.Single(r=>r.name=="Kit_Top");var topMesh=topRenderer.sharedMesh;
     var deltas=new Vector3[topMesh.vertexCount];
     for(int shape=0;shape<topMesh.blendShapeCount;shape++){topMesh.GetBlendShapeFrameVertices(shape,0,deltas,null,null);Assert.That(deltas.Max(d=>d.magnitude),Is.LessThanOrEqualTo(.12f),"corrupt shirt-fit export");}
     var topBake=new Mesh();var originalTop=topMesh.vertices;var topTriangles=topMesh.triangles;
     Capture(cam,sex+"-address");
     var b=look.body;int hi=Array.FindIndex(b.bones,t=>t==golfer.Head);var bw=b.sharedMesh.boneWeights;var headIds=Enumerable.Range(0,bw.Length).Where(i=>bw[i].boneIndex0==hi&&bw[i].weight0>.8f).ToArray();Assert.That(headIds.Length,Is.GreaterThan(100));
     var baked=new Mesh();float minClearance=999;
     foreach(var pair in new[]{(GolfClub.Driver,false),(GolfClub.Iron,false),(GolfClub.Wedge,false),(GolfClub.Wedge,true),(GolfClub.Putter,false)}) {
      golfer.SetClub(pair.Item1,pair.Item2);float end=(float)typeof(GolferView).GetField("EndTime",Private).GetValue(golfer);
      // Remove crossfade so this samples the entire exact clip, independently of the frame rate.
      typeof(GolferView).GetField("fade",Private).SetValue(golfer,0f);typeof(GolferView).GetMethod("Weigh",Private).Invoke(golfer,null);
      float worst=999;
      var sampled=(AnimationClip)typeof(GolferView).GetField("clipAsset",Private).GetValue(golfer);
      var guard=(GolfSwingClearance)typeof(GolferView).GetField("swingClearance",Private).GetValue(golfer);
      var gripBounds=new Bounds(golfer.Hero.Bones["Club"].position,Vector3.zero);
      for(int i=0;i<=160;i++) {
       sampled.SampleAnimation(golfer.Hero.Root,end*i/160f);guard.Apply();look.GetComponent<GolfGarmentCorrectives>().Apply();
       gripBounds.Encapsulate(golfer.Hero.Bones["Club"].position);
       if(i%8==0){topRenderer.BakeMesh(topBake);var posed=topBake.vertices;float stretch=0;
        for(int k=0;k<topTriangles.Length;k+=3)for(int e=0;e<3;e++){int x=topTriangles[k+e],y=topTriangles[k+(e+1)%3];stretch=Mathf.Max(stretch,Vector3.Distance(posed[x],posed[y])-Vector3.Distance(originalTop[x],originalTop[y]));}
        Assert.That(stretch,Is.LessThan(.15f),$"shirt spike: {sex} {pair} sample {i}");
       }
       b.BakeMesh(baked);var v=baked.vertices;
       var bounds=new Bounds(b.transform.TransformPoint(v[headIds[0]]),Vector3.zero);foreach(int k in headIds)bounds.Encapsulate(b.transform.TransformPoint(v[k]));
       var headCentre=bounds.center;float headRadius=Mathf.Max(bounds.extents.x,bounds.extents.y,bounds.extents.z);
       var a=golfer.Hero.Bones["Club"].position;var z=golfer.ClubHeadWorld().Value;var az=z-a;
       float t=Mathf.Clamp01(Vector3.Dot(headCentre-a,az)/az.sqrMagnitude);float clearance=Vector3.Distance(headCentre,a+az*t)-headRadius;
       worst=Mathf.Min(worst,clearance);minClearance=Mathf.Min(minClearance,clearance);
       Assert.That(clearance,Is.GreaterThan(.015f),$"{sex} {pair} sample {i}, shaft through head");
       if(i==145&&pair.Item1!=GolfClub.Putter){Capture(cam,sex+"-"+pair.Item1+(pair.Item2?"-chip":"")+"-finish");}
      }
      Assert.That(gripBounds.size.magnitude,Is.GreaterThan(pair.Item1==GolfClub.Putter?.1f:.25f),"must sample moving poses, not a cached animation frame");
      checks.AppendLine($"{sex} {pair}: 161 poses, minimum shaft/head gap {worst:F3} yd; grip travel {gripBounds.size.magnitude:F3} yd");
     }
     Object.Destroy(baked);Object.Destroy(topBake);
     golfer.Perform("Idle");yield return null;yield return null;
     var hud=Object.FindFirstObjectByType<GolfShotHud>();hud.gameObject.SetActive(false);
     var centre=golfer.transform.position+Vector3.up*1.02f;
     var forward=golfer.Head.forward;forward.y=0;forward.Normalize();
     rig.Cue(centre+forward*3.25f+golfer.transform.right*.35f,centre);rig.ApplyFrame();
     Capture(cam,sex+"-close");hud.gameObject.SetActive(true);
     checks.AppendLine($"{sex}: shared skin + garment maps, bald PASS");
    }
    File.WriteAllText(Path.Combine(Proof,"clearance.txt"),checks.ToString());
   }finally{GolferStyle.SaveDevice(original);Time.timeScale=1;}
  }
  [UnityTest,Timeout(180000)]
  public IEnumerator LandingMarkersMatchPowerAndCameraHoldsTheGolfer() {
   TennisQuality.Apply();Time.captureFramerate=60;Time.timeScale=1;
   yield return SceneManager.LoadSceneAsync("Golf",LoadSceneMode.Single);yield return null;
   var game=Object.FindFirstObjectByType<GolfGame>();game.PrepareNativeAddress("postcards");var cam=game.GameplayCamera;cam.aspect=16f/9;
   var hud=Object.FindFirstObjectByType<Hud>();
   try {
    for(int club=0;club<3;club++) {
     game.NativeClub(1);game.NativeAim(1);yield return null;
     Assert.That(hud.Map.Targets.Count,Is.EqualTo(4));
     var selected=(GolfClub)typeof(GolfGame).GetField("club",Private).GetValue(game);
     var start=game.CurrentHole.Tee;var lie=game.CurrentHole.LieAt(start);double a=game.TutorialHeading*Math.PI/180;
     for(int i=0;i<4;i++) {
      var launch=lie.LaunchFrom(selected,(i+1)*.25,0,0);launch.WindMPH=game.Wind.SpeedMPH;launch.WindDegrees=game.Wind.RelativeTo(game.TutorialHeading);
      var p=BallFlight.Simulate(launch).CarryPoint;var expected=new Vector3((float)(start.X+p.DistanceYards*Math.Sin(a)+p.LateralYards*Math.Cos(a)),0,(float)(start.D+p.DistanceYards*Math.Cos(a)-p.LateralYards*Math.Sin(a)));
      Assert.That(Vector3.Distance(hud.Map.Targets[i],expected),Is.LessThan(.001),"map marker must be the actual wind-adjusted carry");
     }
    }
    game.DropBall(game.CurrentHole.Tee);yield return null;yield return null;
    var origin=cam.transform.position;var rotation=cam.transform.rotation;var golfer=game.GetComponentInChildren<GolferView>();
    game.NativeSwing(.78f);
    int frames=0;
    while(game.Current==GolfGame.State.Flight&&frames++<1200){yield return null;if(game.FlightTime<0)continue;
     if(game.FlightTime<CameraRig.LaunchHoldSeconds){Assert.That(Vector3.Distance(cam.transform.position,origin),Is.LessThan(.02));Assert.That(Quaternion.Angle(cam.transform.rotation,rotation),Is.LessThan(.3));foreach(var p in new[]{golfer.Head.position,golfer.transform.position}){var v=cam.WorldToViewportPoint(p);Assert.That(v.z,Is.GreaterThan(0));Assert.That(v.x,Is.InRange(.03f,.97f));Assert.That(v.y,Is.InRange(.03f,.97f));}}
     if(game.FlightTime>2&&game.FlightTime<2.1){Assert.That(Vector3.Distance(cam.transform.position,origin),Is.GreaterThan(10),"camera reaches ball quickly after the unchanged hold");Capture(cam,"fast-follow");}
    }
    Assert.That(game.ShowingShotResult,Is.True);Capture(cam,"simple-result");
   } finally {Time.captureFramerate=0;Time.timeScale=1;}
  }
 }
}
