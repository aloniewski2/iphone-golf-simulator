#if UNITY_EDITOR
using System;using System.Linq;using System.Collections;using System.Collections.Generic;using System.IO;using System.Reflection;using GolfArcade.Game;using GolfArcade.Course;using GolfArcade.UI;using GolfArcade.Shot;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine;using Object=UnityEngine.Object;
namespace GolfArcade.EditorTools {
 [InitializeOnLoad] public static class GolfBroadcastReview {
  const string Key="GolfBroadcastReview";static IEnumerator script;static int last=-1;static string output;static GolfGame game;static Camera cam;static CameraRig rig;static readonly BindingFlags P=BindingFlags.Instance|BindingFlags.NonPublic;
  [Serializable] public class Report {public string status;public List<string> checks=new();public List<string> images=new();}static Report report;
  static GolfBroadcastReview(){EditorApplication.update+=Tick;}
  public static void Run(){output=Environment.GetEnvironmentVariable("GOLF_BROADCAST_OUT");Directory.CreateDirectory(output);SessionState.SetString(Key+"out",output);SessionState.SetBool(Key,true);EditorSceneManager.OpenScene("Assets/Scenes/Golf.unity");EditorApplication.isPlaying=true;}
  static void Tick(){if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||last==Time.frameCount)return;last=Time.frameCount;try{if(script==null){game=Object.FindFirstObjectByType<GolfGame>();if(!game||game.Swing==null)return;output=SessionState.GetString(Key+"out","");report=new Report();cam=game.GameplayCamera;rig=cam.GetComponent<CameraRig>();script=Review();}if(!script.MoveNext()){report.status="PASS";Finish(0);}}catch(Exception e){Debug.LogException(e);File.WriteAllText(Path.Combine(output,"error.txt"),e.ToString());report.status="FAIL";Finish(1);}}
  static void Finish(int code){File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(report,true));SessionState.SetBool(Key,false);Time.captureFramerate=0;EditorApplication.Exit(code);}
  static void Capture(string name,int width=1920,int height=1080){
   var old=cam.targetTexture;float aspect=cam.aspect;var active=RenderTexture.active;var rt=RenderTexture.GetTemporary(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Default,4);var tex=new Texture2D(width,height,TextureFormat.RGB24,false);
   try{cam.targetTexture=rt;cam.aspect=(float)width/height;foreach(var hud in Object.FindObjectsByType<GolfShotHud>(FindObjectsSortMode.None))hud.RefreshLayout();foreach(var panel in Object.FindObjectsByType<ShotResultPanel>(FindObjectsSortMode.None))panel.SendMessage("LateUpdate",SendMessageOptions.DontRequireReceiver);Canvas.ForceUpdateCanvases();cam.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,width,height),0,0);tex.Apply();File.WriteAllBytes(Path.Combine(output,name+".png"),tex.EncodeToPNG());report.images.Add(name+".png");}finally{cam.targetTexture=old;cam.aspect=aspect;RenderTexture.active=active;Object.DestroyImmediate(tex);RenderTexture.ReleaseTemporary(rt);}}
  static void Frame(){typeof(GolfGame).GetMethod("FrameAim",P).Invoke(game,null);rig.SnapNext();rig.ApplyFrame();}
  static IEnumerator Review(){Time.captureFramerate=30;game.InstantReplays=false;game.PrepareNativeAddress();for(int i=0;i<8;i++)yield return null;
   foreach(int hole in new[]{7,12}){game.JumpToHole(hole);game.DropBall(game.CurrentHole.Tee);for(int i=0;i<5;i++)yield return null;cam.aspect=16f/9f;Frame();Capture("hole-"+hole+"-address-tv");cam.aspect=9f/16f;Frame();Capture("hole-"+hole+"-address-phone",1080,1920);cam.aspect=16f/9f;Frame();}
   game.JumpToHole(7);game.DropBall(game.CurrentHole.Tee);for(int i=0;i<3;i++)yield return null;cam.aspect=16f/9f;Frame();typeof(GolfGame).GetMethod("SelectClub",P).Invoke(game,new object[]{GolfClub.Iron});yield return null;Capture("club-switch-driver-to-iron");
   game.NativeReady();game.NativeSwing(.76f);int frames=0;var samples=new HashSet<int>();
   while(game.Current==GolfGame.State.Flight&&frames++<1500){yield return null;int tenths=Mathf.RoundToInt((float)game.FlightTime*10);if(new[]{0,6,15,25,40,55,70}.Contains(tenths)&&samples.Add(tenths))Capture("flight-"+tenths.ToString("000"));}
   if(game.Current!=GolfGame.State.Result)throw new Exception("Shot failed to enter result");for(int i=0;i<3;i++)yield return null;Capture("character-cutback-tv");cam.aspect=9f/16f;yield return null;rig.FrameCharacterResult(game.GetComponentInChildren<GolferView>(true).transform);rig.SnapNext();rig.ApplyFrame();Capture("character-cutback-phone",1080,1920);cam.aspect=16f/9f;rig.FrameCharacterResult(game.GetComponentInChildren<GolferView>(true).transform);rig.SnapNext();rig.ApplyFrame();
   if(!game.PlayResultEmote(0))throw new Exception("Emote failed");for(int i=0;i<22;i++)yield return null;Capture("character-emote-tv");game.ContinueShotResult();yield return null;if(game.Current!=GolfGame.State.Aim&&game.Current!=GolfGame.State.HoleDone)throw new Exception("Continue failed");
   report.checks.Add("Address, club change, live flight, character cutback, emote and continue captured through real game flow");
  }
 }
}
#endif
