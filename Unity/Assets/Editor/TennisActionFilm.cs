#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using GolfArcade.Game;
using GolfArcade.Tennis;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
namespace GolfArcade.EditorTools
{
    /// Actual 60fps game/driver, saved every2frames for true30fps full-speed films.
    /// TAF_OUT controls output. Rally/dives use real gameplay; labelled FX fixture
    /// cases call the live existing emitter explicitly without changing gameplay.
    [InitializeOnLoad]public static class TennisActionFilm
    {
        const string Flag="TennisActionFilm";static IEnumerator script;static int lastFrame=-1;
        static TennisActionFilm(){EditorApplication.update+=Tick;}
        public static void Run(){script=null;lastFrame=-1;EditorSceneManager.OpenScene("Assets/Scenes/Tennis.unity");SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;}
        static void Tick(){if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying||lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;var game=Object.FindFirstObjectByType<TennisGame>();if(!game||!game.Initialized)return;if(script==null)script=Go(game);try{if(!script.MoveNext())Done(0);}catch(Exception e){Debug.LogException(e);Done(1);}}
        static void Done(int code){SessionState.SetBool(Flag,false);Time.captureFramerate=0;if(Application.isBatchMode)EditorApplication.Exit(code);else EditorApplication.isPlaying=false;}
        static void Close(Camera cam,string file,Transform root)
        {
            var p=cam.transform.position;var q=cam.transform.rotation;float fov=cam.fieldOfView;
            var from=root.TransformPoint(2.9f,1.42f,2.6f);var at=root.TransformPoint(0,.84f,.10f);cam.transform.SetPositionAndRotation(from,Quaternion.LookRotation(at-from));cam.fieldOfView=46;
            var rt=RenderTexture.GetTemporary(720,720,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Default,4);var target=cam.targetTexture;var active=RenderTexture.active;cam.targetTexture=rt;cam.Render();cam.targetTexture=target;RenderTexture.active=rt;var image=new Texture2D(720,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,720,720),0,0);image.Apply();File.WriteAllBytes(file,image.EncodeToPNG());Object.DestroyImmediate(image);RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);cam.transform.SetPositionAndRotation(p,q);cam.fieldOfView=fov;
        }
        static void Save(string dir,int frame,TennisGame game)
        {
            if(frame%2!=0)return;string id=(frame/2).ToString("00000");GameCapture.Save(Path.Combine(dir,"game_"+id+".png"),960,540);Close(game.GameplayCamera?game.GameplayCamera:Camera.main,Path.Combine(dir,"hero_"+id+".png"),game.Player.transform);
        }
        static void Hold(TennisGame game){typeof(TennisGame).GetProperty("Flow").GetSetMethod(true).Invoke(game,new object[]{TennisGame.Phase.PointOver});typeof(TennisGame).GetField("resetTimer",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(game,1e6f);}
        static IEnumerator Go(TennisGame game)
        {
            string dir=Path.GetFullPath(Environment.GetEnvironmentVariable("TAF_OUT")??"../proof/full-visual-overhaul/tennis-action-film");Directory.CreateDirectory(dir);Time.captureFramerate=60;var notes=new List<string>{"All sequences play actual60fps and save30fps sequential frames. Rally cases use real gameplay. Named FX cases deliberately exercise the production emitter in a parked point; no gameplay physics altered."};
            var presentation=game.GetComponent<TennisPresentation>();if(presentation)presentation.Finish();
            foreach(bool female in new[]{false,true})
            {
                game.SelectCharacter(female);string rival=female?"Viktor":"Nadia";game.ConfigureMatch(TennisGame.Mode.Campaign,rival,rival,"ROUND");if(presentation)presentation.Finish();
                for(int n=0;n<30;n++)yield return null;var driver=game.Player.GetComponentInChildren<HeroTennisDriver>();foreach(var skin in driver.GetComponentsInChildren<SkinnedMeshRenderer>(true))skin.updateWhenOffscreen=true;
                string rally=Path.Combine(dir,female?"female_rally":"male_rally");Directory.CreateDirectory(rally);bool dived=false;game.AutoPlay=true;game.AutoPlayLean=true;
                var trace=new List<string>{"frame,unityFrame,state,clip,time,ttc,speed,forward,skate,guiding,dive,shake,flash,crouch,desiredCrouch,visualLungeX,visualLungeY,visualLungeZ,guideWeight,physicsTtc,physicsCrouch,physicsRestoreError"};var fx=Object.FindFirstObjectByType<TennisFx>();
                for(int n=0;n<1200;n++)
                {
                    yield return null;
                    if(n>640&&!dived&&game.CanDive)dived=game.RequestDive();Save(rally,n,game);
                    trace.Add($"{n},{Time.frameCount},{driver.State},{driver.PlayingClip},{driver.PlayingClipTime:F4},{driver.actor.SignedTimeToContact:F4},{driver.actor.Speed:F3},{driver.actor.ForwardSpeed:F3},{driver.FootSkate:F4},{driver.actor.Guiding},{game.DiveActive},{fx.Shake:F4},{fx.Flash:F4},{driver.LastCrouch:F5},{driver.LastDesiredCrouch:F5},{driver.RenderedLunge.x:F5},{driver.RenderedLunge.y:F5},{driver.RenderedLunge.z:F5},{driver.actor.ContactGuideWeight:F5},{driver.LastPhysicsTtc:F5},{driver.LastPhysicsCrouch:F5},{driver.PhysicsPoseRestoreError:F7}");
                }
                game.AutoPlay=false;File.WriteAllLines(Path.Combine(rally,"trace.csv"),trace);notes.Add((female?"female":"male")+" actual rally20s dive="+dived);
            }
            var emitter=Object.FindFirstObjectByType<TennisFx>();Hold(game);for(int n=0;n<60;n++)yield return null;
            foreach(string name in new[]{"contact_good","contact_perfect","contact_smash","bounce","footstep","slide","confetti","whiff"})
            {
                Hold(game);emitter.Clear();string path=Path.Combine(dir,"fx_"+name);Directory.CreateDirectory(path);var d=game.Player.GetComponentInChildren<HeroTennisDriver>();
                for(int n=0;n<180;n++)
                {
                    yield return null;
                    if(n==20)
                    {
                        var feet=game.Player.transform.position;var hit=d.StringCentre;
                        switch(name){case "contact_good":emitter.Contact(hit,Timing.Good,false);break;case "contact_perfect":emitter.Contact(hit,Timing.Perfect,false);break;case "contact_smash":emitter.Contact(hit,Timing.Perfect,false,true);break;case "bounce":emitter.Bounce(feet+Vector3.forward,new Vector3(4,-8,20),new Color(.18f,.4f,.42f));break;case "footstep":emitter.Footstep(feet,7);break;case "slide":emitter.Slide(feet);break;case "confetti":emitter.Celebrate(feet,1);break;case "whiff":emitter.Whiff(feet,hit);break;}
                    }
                    Save(path,n,game);
                }
                notes.Add(name+": production emitter fixture, event at20/60seconds, full3second30fps film.");
            }
            File.WriteAllLines(Path.Combine(dir,"audit.txt"),notes);Debug.Log("[TennisActionFilm] complete "+dir);
        }
    }
}
#endif
