#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using GolfArcade.Game;
using GolfArcade.Tennis;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
namespace GolfArcade.EditorTools
{
    /// Tracked-controller movement through the actual game adapter. Shows start,
    /// reverse, stop, sprint and a short reaction while repositioning, not Samples.
    [InitializeOnLoad]public static class TennisLocomotionFilm
    {
        const string Flag="TennisLocomotionFilm";static IEnumerator script;static int lastFrame=-1;
        static TennisLocomotionFilm(){EditorApplication.update+=Tick;}
        public static void Run(){script=null;lastFrame=-1;EditorSceneManager.OpenScene("Assets/Scenes/Tennis.unity");SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;}
        static void Tick(){if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying||lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;var g=Object.FindFirstObjectByType<TennisGame>();if(!g||!g.Initialized)return;if(script==null)script=Go(g);try{if(!script.MoveNext())Done(0);}catch(Exception e){Debug.LogException(e);Done(1);}}
        static void Done(int code){SessionState.SetBool(Flag,false);Time.captureFramerate=0;if(Application.isBatchMode)EditorApplication.Exit(code);else EditorApplication.isPlaying=false;}
        static void Shot(Camera camera,string path,Transform actor)
        {
            var p=camera.transform.position;var q=camera.transform.rotation;float fov=camera.fieldOfView;
            var from=actor.TransformPoint(2.9f,1.5f,3.2f);var to=actor.TransformPoint(0,.84f,.1f);camera.transform.SetPositionAndRotation(from,Quaternion.LookRotation(to-from));camera.fieldOfView=44;
            var rt=RenderTexture.GetTemporary(640,640,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Default,4);var target=camera.targetTexture;var active=RenderTexture.active;camera.targetTexture=rt;camera.Render();camera.targetTexture=target;RenderTexture.active=rt;
            var tex=new Texture2D(640,640,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,640,640),0,0);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());Object.DestroyImmediate(tex);RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);camera.transform.SetPositionAndRotation(p,q);camera.fieldOfView=fov;
        }
        static IEnumerator Go(TennisGame game)
        {
            string output=Path.GetFullPath(Environment.GetEnvironmentVariable("TLF_OUT")??"../proof/full-visual-overhaul/tennis-locomotion-film");Directory.CreateDirectory(output);Time.captureFramerate=60;
            var notes=new List<string>{"Real public SetLateralInput tracked controller, live game movement at60fps, sequential30fps gameplay/hero records. Ball feed remains high/out of contact to isolate movement; no actor root or action clock set by fixture."};
            var presentation=game.GetComponent<TennisPresentation>();
            foreach(bool female in new[]{false,true})
            {
                game.SelectCharacter(female);string rival=female?"Viktor":"Nadia";game.ConfigureMatch(TennisGame.Mode.Campaign,rival,rival,"MOVEMENT");if(presentation)presentation.Finish();game.AutoPlay=false;game.NativeControlled=true;game.SetControllerSetup(false);
                for(int n=0;n<40;n++)yield return null;game.PrepareLesson();
                for(int n=0;n<12;n++)yield return null;
                var driver=game.Player.GetComponentInChildren<HeroTennisDriver>();foreach(var skin in driver.GetComponentsInChildren<SkinnedMeshRenderer>(true))skin.updateWhenOffscreen=true;
                string path=Path.Combine(output,female?"female":"male");Directory.CreateDirectory(path);var trace=new List<string>{"frame,unityFrame,phase,input,sprint,speed,forward,clip,time,skate,reactionLegShare,upperWeight,actorX,actorZ"};bool moved=false,reversed=false,reaction=false;
                for(int frame=0;frame<660;frame++)
                {
                    if(frame%30==0)game.InjectBall(new Vector3(0,40,2),new Vector3(0,0,0));
                    if(!game.PlayerUsesTrackedMovement)throw new InvalidOperationException("Actual tracked movement guard not satisfied frame="+frame+" phase="+game.Flow+" native="+game.NativeControlled+" autoplay="+game.AutoPlay+" touch="+NativeSportsSession.Touch+" multiplayer="+GolfArcade.Multiplayer.SportsMultiplayer.Active+"");
                    string phase;float input=0;bool sprint=false;
                    if(frame<60)phase="idle";
                    else if(frame<135){phase="start_right";input=.72f;}
                    else if(frame<240){phase="reverse_left";input=-.72f;}
                    else if(frame<300)phase="stop";
                    else if(frame<365){phase="sprint_right";input=1;sprint=true;}
                    else if(frame<430){phase="sprint_reverse";input=-1;sprint=true;}
                    else if(frame<490)phase="settle";
                    else if(frame<555){phase="reaction_reposition";input=.70f;}
                    else phase="reaction_planted";
                    if(frame==490){driver.PlayJuice(HeroTennisDriver.Clip.SadPointLost);reaction=true;}
                    game.SetLateralInput(input,sprint);yield return null;
                    moved|=driver.actor.Speed>.8f;reversed|=driver.actor.Speed<-.8f;
                    var root=game.Player.transform.position;trace.Add($"{frame},{Time.frameCount},{phase},{input:F3},{sprint},{driver.actor.Speed:F4},{driver.actor.ForwardSpeed:F4},{driver.PlayingClip},{driver.PlayingClipTime:F4},{driver.FootSkate:F5},{driver.ReactionLegShare:F4},{driver.UpperLayerWeight:F4},{root.x:F5},{root.z:F5}");
                    if(frame%2==0){string id=(frame/2).ToString("00000");GameCapture.Save(Path.Combine(path,"game_"+id+".png"),960,540);Shot(game.GameplayCamera?game.GameplayCamera:Camera.main,Path.Combine(path,"hero_"+id+".png"),game.Player.transform);}
                }
                game.SetLateralInput(0,false);if(!moved||!reversed||!reaction||Mathf.Abs(driver.actor.Speed)>.05f)throw new InvalidOperationException("Movement fixture coverage incomplete");File.WriteAllLines(Path.Combine(path,"trace.csv"),trace);notes.Add((female?"female":"male")+": moved="+moved+" reversed="+reversed+" reaction="+reaction+"11seconds");
            }
            File.WriteAllLines(Path.Combine(output,"audit.txt"),notes);Debug.Log("[TennisLocomotionFilm] complete "+output);
        }
    }
}
#endif
