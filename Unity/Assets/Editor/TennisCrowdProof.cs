using System;
using System.Collections;
using System.IO;
using System.Reflection;
using GolfArcade.Tennis;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
namespace GolfArcade.EditorTools
{
 [InitializeOnLoad]public static class TennisCrowdProof
 {
  const string Flag="TennisCrowdProof";static IEnumerator script;
  static TennisCrowdProof(){EditorApplication.update+=Tick;}
  public static void Run(){TennisVenue.Selected=TennisVenueKind.Resort;EditorSceneManager.OpenScene("Assets/Scenes/Tennis.unity");SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;}
  static void Tick(){if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying)return;var g=Object.FindFirstObjectByType<TennisGame>();if(!g||!g.Initialized)return;if(script==null)script=Go(g);try{if(!script.MoveNext())Done(0);}catch(Exception e){Debug.LogException(e);Done(1);}}
  static void Done(int code){SessionState.SetBool(Flag,false);Time.captureFramerate=0;if(Application.isBatchMode)EditorApplication.Exit(code);else EditorApplication.isPlaying=false;}
  static void Shot(Camera cam,string file,Vector3 from,Vector3 at,int width,int height)
  {
   var pos=cam.transform.position;var rot=cam.transform.rotation;var fov=cam.fieldOfView;
   cam.transform.SetPositionAndRotation(from,Quaternion.LookRotation(at-from));cam.fieldOfView=35;
   var target=RenderTexture.GetTemporary(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Default,4);var old=cam.targetTexture;var active=RenderTexture.active;cam.targetTexture=target;cam.Render();cam.targetTexture=old;RenderTexture.active=target;
   var tex=new Texture2D(width,height,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,width,height),0,0);tex.Apply();File.WriteAllBytes(file,tex.EncodeToPNG());Object.DestroyImmediate(tex);RenderTexture.active=active;RenderTexture.ReleaseTemporary(target);cam.transform.SetPositionAndRotation(pos,rot);cam.fieldOfView=fov;
  }
  static IEnumerator Go(TennisGame game)
  {
   var dir=Path.GetFullPath(Environment.GetEnvironmentVariable("TCP_OUT")??"../proof/full-visual-overhaul/tennis-crowd-runtime");Directory.CreateDirectory(dir);Time.captureFramerate=60;
   var p=game.GetComponent<TennisPresentation>();if(p)p.Finish();
   typeof(TennisGame).GetProperty("Flow").GetSetMethod(true).Invoke(game,new object[]{TennisGame.Phase.PointOver});typeof(TennisGame).GetField("resetTimer",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(game,1e6f);
   for(int n=0;n<45;n++)yield return null;
   var crowd=game.GetComponent<TennisStandsCrowd>();var cam=game.GameplayCamera?game.GameplayCamera:Camera.main;
   Shot(cam,Path.Combine(dir,"seated_group_idle.png"),new Vector3(-6,3.7f,1),new Vector3(-15,1.65f,1),1536,768);
   foreach(int i in new[]{0,1,2,3,4,5})
   {
    var fan=GameObject.Find("Seated sporting fan "+i);if(!fan)continue;
    var rs=fan.GetComponentsInChildren<Renderer>();var bounds=rs[0].bounds;foreach(var r in rs)bounds.Encapsulate(r.bounds);
    var foliage=Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);var hidden=new System.Collections.Generic.List<Renderer>();
    foreach(var r in foliage)if(r.enabled&&r.name.StartsWith("Tennis foliage pocket")){r.enabled=false;hidden.Add(r);}
    float distance=bounds.size.y/(2*Mathf.Tan(17.5f*Mathf.Deg2Rad))*1.24f;
    var at=bounds.center;Shot(cam,Path.Combine(dir,"seated_variant_"+i+".png"),at+new Vector3(distance,.25f,.38f),at,640,640);
    foreach(var r in hidden)r.enabled=true;
   }
   // Diagnostic only: a sky-only image distinguishes a panorama derivative seam
   // from a floating legacy mesh. Restore exact original enabled states afterwards.
   var renderers=Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);var enabled=new bool[renderers.Length];
   for(int i=0;i<renderers.Length;i++){enabled[i]=renderers[i].enabled;renderers[i].enabled=false;}
   Shot(cam,Path.Combine(dir,"sky_only.png"),cam.transform.position,cam.transform.position+cam.transform.forward,1920,1080);
   for(int i=0;i<renderers.Length;i++)renderers[i].enabled=enabled[i];
   crowd.Cheer(1);
   for(int n=0;n<165;n++){yield return null;if(n==30||n==65||n==130||n==164)Shot(cam,Path.Combine(dir,"seated_cheer_"+n+".png"),new Vector3(-6,3.7f,1),new Vector3(-15,1.65f,1),1536,768);}
   File.WriteAllText(Path.Combine(dir,"audit.txt"),"Real runtime 36-seat rigid crowd, idle and entry/peak/recovery seated cheer; no CPU skin proxy. Actual visual review required. Variant close-ups isolate foreground foliage only; group and cheer retain the complete actual scene.\n");
  }
 }
}
