#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using GolfArcade.Course;
using GolfArcade.Game;
using GolfArcade.Shot;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
namespace GolfArcade.EditorTools {
 [InitializeOnLoad] public static class GolfMotionFilm {
  const string Flag="GolfMotionFilm"; const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
  static IEnumerator script; static int lastFrame=-1;
  static GolfMotionFilm(){EditorApplication.update+=Tick;}
  public static void Run(){script=null;lastFrame=-1;SessionState.SetBool(Flag,true);EditorSceneManager.OpenScene("Assets/Scenes/Golf.unity");EditorApplication.isPlaying=true;}
  static void Tick(){
   if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying||Time.frameCount==lastFrame)return;lastFrame=Time.frameCount;
   var g=Object.FindFirstObjectByType<GolfGame>();if(!g||g.Swing==null)return;
   try{if(script==null)script=Go(g);if(!script.MoveNext())Done(0);}catch(Exception e){Debug.LogException(e);Done(1);}
  }
  static void Done(int c){SessionState.SetBool(Flag,false);Time.captureFramerate=0;EditorApplication.Exit(c);}
  static GolferView Figure(GolfGame g)=>(GolferView)typeof(GolfGame).GetField("golfer",Private).GetValue(g);
  static void Close(Camera cam,string path,GolferView figure,bool side=false){
   var pos=cam.transform.position;var rot=cam.transform.rotation;float fov=cam.fieldOfView,aspect=cam.aspect;
   var root=figure.transform;var from=root.TransformPoint(side?new Vector3(3.2f,1.3f,.1f):new Vector3(2.2f,1.35f,3.2f));var at=root.position+Vector3.up*.87f;
   cam.transform.SetPositionAndRotation(from,Quaternion.LookRotation(at-from));cam.fieldOfView=42;cam.aspect=1;
   var rt=RenderTexture.GetTemporary(720,720,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Default,4);var old=cam.targetTexture;var active=RenderTexture.active;
   var image=new Texture2D(720,720,TextureFormat.RGB24,false);
   try{cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,720,720),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToJPG(94));}
   finally{cam.targetTexture=old;RenderTexture.active=active;Object.DestroyImmediate(image);RenderTexture.ReleaseTemporary(rt);cam.transform.SetPositionAndRotation(pos,rot);cam.fieldOfView=fov;cam.aspect=aspect;}
  }
  static void Save(string path,int n,GolferView hero,bool close=true){
   if(n%2!=0)return;var id=(n/2).ToString("00000");GameCapture.Save(Path.Combine(path,"game_"+id+".jpg"),720,1280);
   if(close)Close(Camera.main,Path.Combine(path,"hero_"+id+".jpg"),hero);
  }
  static CoursePoint ShotOrigin(Hole hole,GolfClub club,double distance){
   if(club==GolfClub.Driver)return hole.Tee;
   CoursePoint best=hole.Pin;double error=double.MaxValue;
   for(int i=1;i<hole.Centerline.Length;i++){
    var a=hole.Centerline[i-1];var b=hole.Centerline[i];int steps=Math.Max(1,(int)Math.Ceiling(a.DistanceTo(b)*4));
    for(int k=0;k<=steps;k++){
     double t=(double)k/steps;var p=new CoursePoint(a.X+(b.X-a.X)*t,a.D+(b.D-a.D)*t);var lie=hole.LieAt(p);
     if(lie==CourseLie.Water||lie==CourseLie.OutOfBounds||lie==CourseLie.Bunker)continue;
     if(club==GolfClub.Putter&&!lie.IsPuttingSurface())continue;
     double e=Math.Abs(p.DistanceTo(hole.Pin)-distance);if(e<error){error=e;best=p;}
    }
   }
   if(error>3)throw new Exception("No playable film lie for "+club+" at "+distance+"yd; error "+error);
   return best;
  }
  static IEnumerator Go(GolfGame game){
   var output=Path.GetFullPath(Environment.GetEnvironmentVariable("GMF_OUT")??"../proof/full-visual-overhaul/golf-motion-film");
   Directory.CreateDirectory(output);Time.captureFramerate=60;Time.timeScale=1;GolferStyle.HeroOverride=true;
   var notes=new List<string>{"Actual production GolfGame at60fps, consecutive images saved at30fps. Intro uses the production camera and signature path. Stroke cases use real synthetic phone backswing/commit. Explicit performance cases invoke the production GolferView performance API in a parked aim state."};
   game.ChooseHoles(0);game.Play();Camera.main.aspect=9f/16f;
   foreach(bool female in new[]{false,true}){
    GolferStyle.Body=female?GolferStyle.BodyKind.Female:GolferStyle.BodyKind.Male;game.RestyleGolfer();
    string sex=female?"female":"male";game.JumpToHole(12);game.SetWind(Wind.Calm);
    var hero=Figure(game);if(!hero.IsHero)throw new Exception("Golf film requires actual Match Hero.");
    foreach(var skin in hero.GetComponentsInChildren<SkinnedMeshRenderer>())skin.updateWhenOffscreen=true;
    string intro=Path.Combine(output,sex+"_intro");Directory.CreateDirectory(intro);
    var introTrace=new List<string>{"frame,unityFrame,state,heroPosition"};int frames=0;while(game.Current==GolfGame.State.Intro&&frames<5400){yield return null;introTrace.Add(frames+","+Time.frameCount+","+game.Current+",\""+hero.transform.position+"\"");Save(intro,frames++,hero,false);}File.WriteAllLines(Path.Combine(intro,"trace.csv"),introTrace);
    if(game.Current!=GolfGame.State.Aim)throw new Exception("Production intro did not reach Aim.");notes.Add(sex+" intro "+frames+" /60seconds.");
    foreach(var club in new[]{GolfClub.Driver,GolfClub.Iron,GolfClub.PitchingWedge,GolfClub.Wedge,GolfClub.Putter}){
     game.JumpToHole(12);var h=game.CurrentHole;var delta=h.Pin.D-h.Tee.D;var dx=h.Pin.X-h.Tee.X;var len=Math.Sqrt(dx*dx+delta*delta);
     double distance=club==GolfClub.Putter?5:club==GolfClub.Wedge?22:club==GolfClub.PitchingWedge?60:club==GolfClub.Iron?100:len;
     var at=ShotOrigin(h,club,distance);
     game.DropBall(at);game.SetWind(Wind.Calm);typeof(GolfGame).GetMethod("SelectClub",Private).Invoke(game,new object[]{club});
     for(int n=0;n<15;n++)yield return null;
     hero=Figure(game);game.Swing.Synthetic.FixedStepSeconds=1.0/60.0;
     string path=Path.Combine(output,sex+"_"+club);Directory.CreateDirectory(path);bool flight=false;
     var trace=new List<string>{"frame,unityFrame,state,swingPhase,performing,ball,clubHead"};
     for(int n=0;n<600;n++){
      if(n==30)game.Swing.Synthetic.Backswing(true);if(n==108)game.Swing.Synthetic.Backswing(false);
      yield return null;flight|=game.Current==GolfGame.State.Flight;Save(path,n,hero);
      trace.Add(n+","+Time.frameCount+","+game.Current+","+game.Swing.Phase+","+hero.Performing+",\""+game.BallPosition+"\",\""+hero.ClubHeadWorld()+"\"");
     }
     File.WriteAllLines(Path.Combine(path,"trace.csv"),trace);if(!flight)throw new Exception("Synthetic shot did not launch: "+sex+" "+club);
     notes.Add(sex+" "+club+": actual shot launched;600/60s,300frames.");
    }
    game.DropBall(game.CurrentHole.Tee);game.enabled=false;var rig=Object.FindFirstObjectByType<CameraRig>();rig.enabled=false;hero=Figure(game);
    foreach(string move in new[]{"Idle","Wave","Cheer","FistPump"}){
     if(!hero.Perform(move,0,false))throw new Exception("Missing golf performance "+move);
     string path=Path.Combine(output,sex+"_"+move);Directory.CreateDirectory(path);float duration=hero.PerformanceDuration;int count=Mathf.CeilToInt(duration*60)+24,firstFrame=Time.frameCount;bool sawTarget=false;var trace=new List<string>{"frame,unityFrame,performing,clipTime,clipLength"};
     for(int n=0;n<count;n++){yield return null;sawTarget|=hero.Performing==move;trace.Add(n+","+Time.frameCount+","+hero.Performing+","+typeof(GolferView).GetField("performTime",Private).GetValue(hero)+","+typeof(GolferView).GetField("performLength",Private).GetValue(hero));if(n%2==0){var id=(n/2).ToString("00000");Close(Camera.main,Path.Combine(path,"front_"+id+".jpg"),hero);Close(Camera.main,Path.Combine(path,"side_"+id+".jpg"),hero,true);}}
     File.WriteAllLines(Path.Combine(path,"trace.csv"),trace);if(!sawTarget||hero.Performing!="Idle")throw new Exception("Golf performance did not play/return to Idle: "+sex+" "+move+" final="+hero.Performing);notes.Add(sex+" "+move+": observed target, duration="+duration+", returned="+hero.Performing+", "+count+" /60s.");
    }
    hero.Settle();game.enabled=true;rig.enabled=true;
   }
   File.WriteAllLines(Path.Combine(output,"audit.txt"),notes);Debug.Log("[GolfMotionFilm] actual complete "+output);
  }
 }
}
#endif
