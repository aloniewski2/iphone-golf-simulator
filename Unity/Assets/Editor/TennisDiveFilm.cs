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
    /// A real accepted gameplay dive, evaluated at60fps and recorded at30fps.
    /// Public lesson/ball-feed APIs establish the live rally; RequestDive must pass
    /// its actual guard. No dive phase, swing clock or guide is set by reflection.
    [InitializeOnLoad] public static class TennisDiveFilm
    {
        const string Flag="TennisDiveFilm";static IEnumerator script;static int lastFrame=-1;
        static TennisDiveFilm(){EditorApplication.update+=Tick;}
        public static void Run(){script=null;lastFrame=-1;EditorSceneManager.OpenScene("Assets/Scenes/Tennis.unity");SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;}
        static void Tick()
        {
            if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying||lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;
            var game=Object.FindFirstObjectByType<TennisGame>();if(!game||!game.Initialized)return;
            if(script==null)script=Go(game);
            try{if(!script.MoveNext())Done(0);}catch(Exception e){Debug.LogException(e);Done(1);}
        }
        static void Done(int code){SessionState.SetBool(Flag,false);Time.captureFramerate=0;if(Application.isBatchMode)EditorApplication.Exit(code);else EditorApplication.isPlaying=false;}
        static void Shot(Camera camera,string path,Transform actor,bool side)
        {
            var oldPosition=camera.transform.position;var oldRotation=camera.transform.rotation;float oldFov=camera.fieldOfView;
            var position=actor.TransformPoint(side?new Vector3(3.7f,1.25f,.35f):new Vector3(2.55f,1.55f,3.2f));
            var target=actor.TransformPoint(0,.70f,.12f);camera.transform.SetPositionAndRotation(position,Quaternion.LookRotation(target-position));camera.fieldOfView=44;
            var rt=RenderTexture.GetTemporary(720,720,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Default,4);
            var oldTarget=camera.targetTexture;var oldActive=RenderTexture.active;camera.targetTexture=rt;camera.Render();camera.targetTexture=oldTarget;RenderTexture.active=rt;
            var image=new Texture2D(720,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,720,720),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());Object.DestroyImmediate(image);
            RenderTexture.active=oldActive;RenderTexture.ReleaseTemporary(rt);camera.transform.SetPositionAndRotation(oldPosition,oldRotation);camera.fieldOfView=oldFov;
        }
        static IEnumerator Go(TennisGame game)
        {
            string directory=Path.GetFullPath(Environment.GetEnvironmentVariable("TDF_OUT")??"../proof/full-visual-overhaul/tennis-dive-film");Directory.CreateDirectory(directory);Time.captureFramerate=60;
            var audit=new List<string>{"Real live incoming ball, actual CanDive guard + RequestDive, actual gameplay root/contact/timing;60fps evaluated, sequential30fps gameplay/front/side captures."};
            var presentation=game.GetComponent<TennisPresentation>();
            foreach(bool female in new[]{false,true})foreach(int side in new[]{-1,1})
            {
                game.SelectCharacter(female);string rival=female?"Viktor":"Nadia";game.ConfigureMatch(TennisGame.Mode.Campaign,rival,rival,"DIVE PROOF");if(presentation)presentation.Finish();game.AutoPlay=false;
                for(int n=0;n<35;n++)yield return null;
                game.PrepareLesson();for(int n=0;n<12;n++)yield return null;
                var driver=game.Player.GetComponentInChildren<HeroTennisDriver>();foreach(var skin in driver.GetComponentsInChildren<SkinnedMeshRenderer>(true))skin.updateWhenOffscreen=true;
                string path=Path.Combine(directory,(female?"female":"male")+(side<0?"_left":"_right"));Directory.CreateDirectory(path);
                var trace=new List<string>{"frame,unityFrame,canDive,active,pose,state,clip,time,ttc,actorX,actorY,actorZ,hipX,hipY,hipZ,crouch,guideWeight,guiding"};
                bool accepted=false,active=false,recovered=false;int eventFrame=-1;
                for(int frame=0;frame<180;frame++)
                {
                    yield return null;
                    if(frame==20)
                    {
                        var from=game.Player.transform.position+new Vector3(side*2.0f,0,5.6f);from.y=.70f;
                        game.InjectBall(from,new Vector3(-side*1.5f,-.6f,-12));
                        if(!game.CanDive)throw new InvalidOperationException("Focused dive fixture did not satisfy actual CanDive for "+path);
                        accepted=game.RequestDive();eventFrame=frame;
                        if(!accepted)throw new InvalidOperationException("Focused dive input rejected for "+path);
                    }
                    active|=game.DiveActive;if(accepted&&active&&!game.DiveActive&&frame>eventFrame+54)recovered=true;
                    var actor=game.Player.transform.position;var hip=driver.matchLook?driver.matchLook.Bone(HumanBodyBones.Hips):null;
                    var h=hip?hip.position:Vector3.zero;
                    trace.Add($"{frame},{Time.frameCount},{game.CanDive},{game.DiveActive},{game.DivePose:F4},{driver.State},{driver.PlayingClip},{driver.PlayingClipTime:F4},{driver.actor.SignedTimeToContact:F4},{actor.x:F5},{actor.y:F5},{actor.z:F5},{h.x:F5},{h.y:F5},{h.z:F5},{driver.LastCrouch:F5},{driver.actor.ContactGuideWeight:F5},{driver.actor.Guiding}");
                    if(frame%2==0)
                    {
                        string id=(frame/2).ToString("00000");GameCapture.Save(Path.Combine(path,"game_"+id+".png"),960,540);var camera=game.GameplayCamera?game.GameplayCamera:Camera.main;
                        Shot(camera,Path.Combine(path,"front_"+id+".png"),game.Player.transform,false);Shot(camera,Path.Combine(path,"side_"+id+".png"),game.Player.transform,true);
                    }
                }
                if(!accepted||!active||!recovered||game.DiveActive)throw new InvalidOperationException("Focused dive coverage incomplete "+path);
                File.WriteAllLines(Path.Combine(path,"trace.csv"),trace);audit.Add(Path.GetFileName(path)+": accepted="+accepted+" sawDive="+active+" returned="+recovered+" eventFrame="+eventFrame+" duration=3s");
            }
            File.WriteAllLines(Path.Combine(directory,"audit.txt"),audit);Debug.Log("[TennisDiveFilm] complete "+directory);
        }
    }
}
#endif
