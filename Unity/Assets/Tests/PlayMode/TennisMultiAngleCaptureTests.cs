using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using GolfArcade.Tennis;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GolfArcade.PlayTests
{
    public class TennisMultiAngleCaptureTests
    {
        const int W=640,H=480;
        static readonly string[] Names={"Ready","Idle","Forehand","Backhand","Jump serve","Forehand volley","Overhead smash","Run forward","Run right","Run left","Run backward","Perfect reaction","Whiff","Point celebration","Point loss","Match win","Match loss","Dive right","Dive left"};
        static readonly string[] Angles={"FRONT - NET SIDE","FRONT RIGHT","RIGHT SIDE","BACK RIGHT","BACK - PLAYING SIDE","BACK LEFT","LEFT SIDE","FRONT LEFT","OVERHEAD","LOW FRONT","HANDS AND RACKET","KNEES AND FEET"};
        const string Out="../ArtDir/anims/multi_angle_review";
        bool extras;
        Process encoder; Texture2D mosaic; RenderTexture rt; Stream pipe;
        void StartVideo(string name,bool library)
        {
            Directory.CreateDirectory(Out);
            string ff=Environment.GetEnvironmentVariable("TENNIS_REVIEW_FFMPEG"); Assert.IsTrue(File.Exists(ff));
            string filter=""; string font="/System/Library/Fonts/Supplemental/Arial Bold.ttf";
            for(int i=0;i<12;i++) {
                if(filter.Length>0) filter+=",";
                filter+=$"drawtext=fontfile='{font}':text='{Angles[i]}':x={i%4*W+12}:y={i/4*H+12}:fontsize=20:fontcolor=white:box=1:boxcolor=black@0.7:boxborderw=5";
            }
            for(int i=0;i<(extras?3:library?Names.Length:1);i++) {
                string label=extras ? new[]{"20 WALK TO POSITION","21 FOREHAND WHILE RUNNING","22 BACKHAND WHILE RUNNING"}[i] + " - 60 FPS" : library?$"{i+1:D2}  {Names[i].ToUpperInvariant()}  -  CURRENT GAMEPLAY DRIVER  -  60 FPS":"LIVE POINT  -  SYNCHRONIZED GAMEPLAY CAMERAS  -  60 FPS";
                filter+=",drawtext=fontfile='"+font+"':text='"+label+"':x=(w-text_w)/2:y=h-30:fontsize=22:fontcolor=white:box=1:boxcolor=black@0.8:boxborderw=5"+((library||extras)?$":enable='gte(t,{i*3})*lt(t,{(i+1)*3})'":"");
            }
            encoder=Process.Start(new ProcessStartInfo {FileName=ff,Arguments="-y -hide_banner -loglevel error -f image2pipe -framerate 60 -vcodec mjpeg -i pipe:0 -an -vf \""+filter+"\" -c:v libx264 -preset veryfast -crf 20 -pix_fmt yuv420p -r 60 -movflags +faststart \""+Path.GetFullPath(Out+"/"+name)+"\"",UseShellExecute=false,RedirectStandardInput=true});
            pipe=encoder.StandardInput.BaseStream;
            mosaic=new Texture2D(W*4,H*3,TextureFormat.RGB24,false); rt=RenderTexture.GetTemporary(W,H,24);
        }
        void Frame(TennisGame game,HeroTennisDriver driver)
        {
            var cam=game.GameplayCamera; var p=game.Player.transform.position;
            var oldPos=cam.transform.position; var oldRot=cam.transform.rotation; var oldFov=cam.fieldOfView; var oldTarget=cam.targetTexture; var active=RenderTexture.active;
            for(int i=0;i<12;i++) {
                Vector3 target=p+Vector3.up*1.05f,pos;
                if(i<8) { float a=i*45*Mathf.Deg2Rad; pos=target+new Vector3(Mathf.Sin(a)*4.5f,.35f,Mathf.Cos(a)*4.5f); }
                else if(i==8) pos=p+new Vector3(0,5.6f,.05f);
                else if(i==9) pos=p+new Vector3(.8f,.38f,4.8f);
                else if(i==10) {
                    var a=driver.look.animator;
                    target=(a.GetBoneTransform(HumanBodyBones.RightHand).position+a.GetBoneTransform(HumanBodyBones.LeftHand).position)*.5f;
                    pos=target+new Vector3(1.2f,.65f,1.5f);
                } else { target=p+Vector3.up*.45f; pos=target+new Vector3(1.7f,.15f,2); }
                cam.transform.position=pos; cam.transform.LookAt(target,i==8?Vector3.forward:Vector3.up); cam.fieldOfView=36;
                cam.targetTexture=rt; cam.Render(); RenderTexture.active=rt;
                mosaic.ReadPixels(new Rect(0,0,W,H),i%4*W,(2-i/4)*H,false);
            }
            mosaic.Apply(false); byte[] bytes=mosaic.EncodeToJPG(90); pipe.Write(bytes,0,bytes.Length);
            cam.targetTexture=oldTarget; cam.transform.SetPositionAndRotation(oldPos,oldRot);cam.fieldOfView=oldFov; RenderTexture.active=active;
        }
        void FinishVideo()
        {
            if(encoder==null) return;
            encoder.StandardInput.Close();encoder.WaitForExit();int result=encoder.ExitCode;encoder.Dispose();encoder=null;
            if(rt)RenderTexture.ReleaseTemporary(rt);if(mosaic)UnityEngine.Object.Destroy(mosaic);
            Assert.That(result,Is.Zero);
        }
        static void HideHUD() { foreach(var c in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) c.enabled=false; }
        [UnityTest,Explicit,Timeout(600000)] public IEnumerator RecordMovementBlends()
        {
            int oldRate=Time.captureFramerate;Time.captureFramerate=60;extras=true;
            try {
                yield return SceneManager.LoadSceneAsync("Tennis");yield return null;
                var game=UnityEngine.Object.FindFirstObjectByType<TennisGame>();game.ManualSimulation=true;
                var driver=game.Player.GetComponentInChildren<HeroTennisDriver>();
                StartVideo("MovementBlends_12Angles_60fps.mp4",false);
                for(int chapter=0;chapter<3;chapter++) {
                    game.ConfigureMatch(TennisGame.Mode.Training,null,null,null);game.Player.CancelSwing();game.Player.Prepare(0,false);
                    game.InjectBall(new Vector3(0,20,30),Vector3.zero);
                    for(int s=0;s<180;s++){game.Player.Advance(1f/60,0,0);game.Player.Pose();yield return null;}
                    game.Player.transform.position=new Vector3(0,.035f,-10);
                    if(chapter==0) game.Player.Prepare(0,false,true);
                    for(int f=0;f<180;f++) {
                        if(f==55 && chapter>0) game.Player.Swing(.75f,chapter==2,TennisActor.Stroke.Drive);
                        float speed=f>=24 && f<145 ? (chapter==0?1:3) : 0;
                        float x=chapter==1?speed:chapter==2?-speed:0,z=chapter==0?speed:0;
                        game.Player.transform.position+=new Vector3(x,0,z)/60;
                        game.Player.Advance(1f/60,x,z);game.Player.Pose();yield return null;
                        HideHUD();Frame(game,driver);
                    }
                    UnityEngine.Debug.Log($"[MultiAngleExtra] completed {chapter+1}/3");
                }
                FinishVideo();
            } finally {FinishVideo();Time.captureFramerate=oldRate;}
        }
        [UnityTest,Explicit,Timeout(3600000)] public IEnumerator RecordLibraryAndLivePoint()
        {
            int oldRate=Time.captureFramerate;Time.captureFramerate=60;
            try {
                yield return SceneManager.LoadSceneAsync("Tennis");yield return null;
                var game=UnityEngine.Object.FindFirstObjectByType<TennisGame>();game.ManualSimulation=true;
                var driver=game.Player.GetComponentInChildren<HeroTennisDriver>();Assert.IsNotNull(driver);
                File.WriteAllLines(Path.GetFullPath(Out+"/active-clips.txt"),driver.slots.Select(s=>$"{s.id}: {(s.clip?s.clip.name:"MISSING")} / contact={s.contact}"));
                StartVideo("AllAnimations_12Angles_60fps.mp4",true);
                for(int chapter=0;chapter<Names.Length;chapter++) {
                    game.ConfigureMatch(TennisGame.Mode.Training,null,null,null);game.Player.CancelSwing();game.Player.Prepare(0,false);
                    game.InjectBall(new Vector3(0,20,30),Vector3.zero);
                    for(int s=0;s<180;s++){game.Player.Advance(1f/60,0,0);game.Player.Pose();yield return null;}
                    game.Player.transform.position=new Vector3(0,.035f,-9);
                    HideHUD();
                    for(int f=0;f<180;f++) {
                        if(f==24) {
                            if(chapter>=2 && chapter<=6) game.Player.Swing(.75f,chapter==3,chapter==4?TennisActor.Stroke.Serve:chapter==5?TennisActor.Stroke.Volley:chapter==6?TennisActor.Stroke.Smash:TennisActor.Stroke.Drive);
                            if(chapter==1)driver.PlayJuice(HeroTennisDriver.Clip.Idle);
                            if(chapter>=11 && chapter<=16)driver.PlayJuice((HeroTennisDriver.Clip)((int)HeroTennisDriver.Clip.HitPerfect+chapter-11));
                            if(chapter>=17){game.InjectBall(game.Player.transform.position+new Vector3(chapter==17?2:-2,1,2),new Vector3(0,0,-4));Assert.IsTrue(game.RequestDive());}
                        }
                        if(chapter>=17) game.Step(1f/60);
                        else {
                            float x=0,z=0;
                            if(f>=24 && f<140) {if(chapter==7)z=3.5f;if(chapter==8)x=3.5f;if(chapter==9)x=-3.5f;if(chapter==10)z=-3.5f;}
                            game.Player.transform.position+=new Vector3(x,0,z)/60;
                            game.Player.Advance(1f/60,x,z);game.Player.Pose();
                        }
                        yield return null;HideHUD();Frame(game,driver);
                    }
                    File.WriteAllBytes(Path.GetFullPath(Out+$"/chapter-{chapter+1:D2}.jpg"),mosaic.EncodeToJPG(90));
                    UnityEngine.Debug.Log($"[MultiAngle] finished {chapter+1}/{Names.Length}: {Names[chapter]}");
                }
                FinishVideo();
                yield return SceneManager.LoadSceneAsync("Tennis");yield return null;
                game=UnityEngine.Object.FindFirstObjectByType<TennisGame>();driver=game.Player.GetComponentInChildren<HeroTennisDriver>();
                for(int f=0;f<(TennisPresentation.Length+2)*60;f++)yield return null;
                game.ConfigureMatch(TennisGame.Mode.Exhibition,null,"Rival","REVIEW");game.AutoPlay=true;game.AutoPlayLean=true;
                StartVideo("LivePoint_12Angles_60fps.mp4",false);
                bool ended=false;int tail=0,count=0;
                for(;count<120*60;count++) {
                    if(game.Flow==TennisGame.Phase.PointOver || game.Flow==TennisGame.Phase.MatchOver) {ended=true;game.AutoPlay=false;if(++tail>=90)break;}
                    yield return null;HideHUD();Frame(game,driver);
                }
                game.AutoPlay=false;FinishVideo();Assert.IsTrue(ended);
                File.WriteAllText(Path.GetFullPath(Out+"/live-point.txt"),$"Frames: {count}; fps: 60; returns: {game.Hits}; longest rally: {game.LongestRally}; score: {game.Match.Scoreboard}. Autoplay inputs.\n");
            } finally {FinishVideo();Time.captureFramerate=oldRate;}
        }
    }
}
