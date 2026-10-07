using System.Collections;
using System.IO;
using System.Text;
using GolfArcade.Game;
using GolfArcade.Tennis;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GolfArcade.PlayTests {
 public class TennisTimingBalancePlayTests {
  [UnityTest, Timeout(300000)] public IEnumerator SweepSwingTimesOnBothWings() {
   yield return SceneManager.LoadSceneAsync("Tennis"); yield return null;
   var game=Object.FindFirstObjectByType<TennisGame>(); game.ManualSimulation=true;
   string dir=System.Environment.GetEnvironmentVariable("TIMING_PROOF")??"Library/Captures/timing";
   Directory.CreateDirectory(dir);
   var csv=new StringBuilder("wing,swingAt,returned,timing,quality,speed,gap,landingX,landingZ\n");
   int returns=0;
   Debug.Log("TIMING CAMERA "+game.GameplayCamera.name+" mask="+game.GameplayCamera.cullingMask+" pos="+game.GameplayCamera.transform.position+" HEROES="+Object.FindObjectsByType<HeroTennisDriver>(FindObjectsSortMode.None).Length+" STRINGS="+game.Player.SweetSpot.name);

   foreach(float wing in new[]{-1f,1f}) {
    int wingReturns=0;
    for(int trial=0;trial<65;trial++) {
     float at=.35f+trial*.02f;
     game.ConfigureMatch(TennisGame.Mode.Training,null,null,null);
     yield return null;
     game.DisplayLatency=0;game.Player.CancelSwing();game.Player.Tick(1,0);
     game.Player.transform.position=new Vector3(0,.035f,-11.2f);
     game.AimInput=.8f*wing;game.AimDepth=.8f;
     game.InjectBall(new Vector3(wing*.8f,1.3f,6),new Vector3(0,2.2f,-16.5f));
     int before=game.Returns;bool swung=false,returned=false;Vector3 landing=Vector3.zero;
     for(float t=0;t<2.3f;t+=1f/120) {
      if(!swung&&t>=at){game.RequestSwing(.65f);swung=true;}
      game.Step(1f/120);
      if(game.Returns>before){returned=true;TennisRules.PredictLanding(game.BallPosition,game.BallVelocity,out landing,game.BallSpin);break;}
     }
     if(returned){returns++;wingReturns++;
      if(System.Environment.GetEnvironmentVariable("TIMING_BASELINE")!="1")Assert.LessOrEqual(game.LastHit.Speed,34f);
      Assert.AreNotEqual(Timing.Missed,game.LastGrade);
      if(System.Environment.GetEnvironmentVariable("TIMING_BASELINE")!="1"&&game.LastGrade==Timing.Perfect)Assert.Less(Vector3.Distance(landing,TennisRules.PlacementTarget(.8f*wing,.8f)),.25f);
     }
     csv.AppendLine(System.FormattableString.Invariant($"{wing},{at:F3},{returned},{(returned?game.LastHit.Timing:0):F4},{(returned?game.LastHit.Quality:0):F4},{(returned?game.LastHit.Speed:0):F3},{game.LastContactGap:F4},{landing.x:F3},{landing.z:F3}"));
     if(returned&&game.LastGrade>=Timing.Great&&trial%3==0){yield return null;game.GameplayCamera.targetDisplay=0;
      foreach(var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))if(cam!=game.GameplayCamera){cam.enabled=false;cam.tag="Untagged";}
      game.GameplayCamera.tag="MainCamera";
      game.GameplayCamera.cullingMask=~0;
      game.GameplayCamera.transform.position=new Vector3(0,6,-18);
      game.GameplayCamera.transform.LookAt(new Vector3(0,1,-3));
      GameCapture.Save(dir+"/contact-"+wing+".png",1280,720);}
    }
    Assert.Greater(wingReturns,0,"each wing must connect");
   }
   File.WriteAllText(dir+"/sweep.csv",csv.ToString());
   Assert.Greater(returns,8,"a range of swing timings must connect");
  }
  [UnityTest, Timeout(180000)] public IEnumerator CapturePlayableReturn() {
   yield return SceneManager.LoadSceneAsync("Tennis");yield return null;
   var game=Object.FindFirstObjectByType<TennisGame>();game.ManualSimulation=true;
   game.ConfigureMatch(TennisGame.Mode.Training,null,null,null);
   for(int i=0;i<10;i++)yield return null;
   game.Player.CancelSwing();game.Player.Tick(1,0);game.DisplayLatency=0;
   game.Player.transform.position=new Vector3(0,.035f,-11.2f);
   game.AimInput=.8f;game.AimDepth=.8f;
   game.InjectBall(new Vector3(.8f,1.3f,6),new Vector3(0,2.2f,-16.5f));
   int before=game.Returns;bool swung=false;
   for(int frame=0;frame<150&&game.Returns==before;frame++) {
    float due=(game.BallPosition.z-(game.Player.transform.position.z+.65f))/-game.BallVelocity.z;
    if(!swung&&due<=.19f){game.RequestSwing(.65f);swung=true;}
    game.Step(1f/120);game.Step(1f/120);yield return null;
   }
   Assert.Greater(game.Returns,before);
   var cam=game.GameplayCamera;
   foreach(var other in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))if(other!=cam){other.enabled=false;other.tag="Untagged";}
   cam.enabled=true;cam.tag="MainCamera";cam.targetDisplay=0;cam.cullingMask=~0;
   cam.transform.position=game.Player.transform.position+new Vector3(-2.8f,2.8f,-5);
   cam.transform.LookAt(game.Player.transform.position+new Vector3(0,1,1.6f));cam.fieldOfView=52;
   string dir=System.Environment.GetEnvironmentVariable("TIMING_PROOF")??"Library/Captures/timing";
   SaveContact(cam,dir+"/gameplay-contact.png");
   File.WriteAllText(dir+"/capture.txt","Actual rendered return; fixed review camera. Grade="+game.LastGrade+"; planned speed="+game.LastHit.Speed+" m/s; racket gap="+game.LastContactGap+" m.");
  }
  static void SaveContact(Camera camera,string path) {
   // Render URP once with the HUD in camera space; a second UI-only Render clears the world.
   var overlays=new System.Collections.Generic.List<Canvas>();
   foreach(var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
    if(canvas.renderMode==RenderMode.ScreenSpaceOverlay){overlays.Add(canvas);canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=.5f;}
   var rt=RenderTexture.GetTemporary(1280,720,24);var previous=camera.targetTexture;var active=RenderTexture.active;
   camera.targetTexture=rt;Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;
   var tex=new Texture2D(1280,720,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1280,720),0,0);tex.Apply();
   Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllBytes(path,tex.EncodeToPNG());
   Object.Destroy(tex);camera.targetTexture=previous;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);
   foreach(var canvas in overlays)canvas.renderMode=RenderMode.ScreenSpaceOverlay;
  }
 }
}
