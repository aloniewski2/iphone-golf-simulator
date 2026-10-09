using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using GolfArcade.Game;
using GolfArcade.Tennis;
using GolfArcade.Course;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace GolfArcade.PlayTests {
    // Explicit review filming only. Uses the production cameras/HUD/adapters at a fixed
    // capture clock; scripted outcomes are labeled editor demonstrations, not device proof.
    public class PresentationFilmTests {
        static string Root => Environment.GetEnvironmentVariable("PRESENTATION_FILM_OUTPUT") ?? "Library/Captures/presentation-films";
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        const int FPS=20;
        static void ClearSeen() {
            foreach(var key in new[]{"tennis.venue."+TennisVenue.Selected,"tennis.walkon","tennis.start","tennis.reaction","tennis.game","tennis.set","tennis.matchend","golf.walkon","golf.start","golf.reaction","golf.boundary","golf.matchend","golf.venue.cliffside.1"})
                PlayerPrefs.DeleteKey("presentation.v"+PresentationPolicy.Revision+"."+key);
            for(int i=1;i<=18;i++)PlayerPrefs.DeleteKey("presentation.v"+PresentationPolicy.Revision+".golf.venue.cliffside."+i);
            for(int i=1;i<=18;i++)PlayerPrefs.DeleteKey("presentation.v"+PresentationPolicy.Revision+".golf.holeout.cliffside."+i);
            foreach(var outcome in new[]{"holeinone","eagle","birdie","par","bogey"})PlayerPrefs.DeleteKey("presentation.v"+PresentationPolicy.Revision+".golf.reaction."+outcome);
        }
        IEnumerator Film(string name,float seconds,Action<int> action=null,bool phone=false) {
            Directory.CreateDirectory(Root);
            string image=Path.Combine(Root,"current.jpg");
            int width=phone?540:960,height=phone?1170:540;
            var camera=Camera.main; camera.aspect=(float)width/height;
            using(var process=Process.Start(new ProcessStartInfo {
                FileName=Environment.GetEnvironmentVariable("PRESENTATION_FFMPEG"),
                Arguments="-y -hide_banner -loglevel error -f image2pipe -framerate "+FPS+" -vcodec mjpeg -i pipe:0 -an -c:v libx264 -preset veryfast -crf 20 -pix_fmt yuv420p -movflags +faststart \""+Path.Combine(Root,name+".mp4")+"\"",
                UseShellExecute=false,RedirectStandardInput=true,CreateNoWindow=true
            })) {
                for(int frame=0;frame<Mathf.CeilToInt(seconds*FPS);frame++) {
                    action?.Invoke(frame); yield return null;
                    GameCapture.Save(image,width,height);
                    byte[] bytes=File.ReadAllBytes(image);process.StandardInput.BaseStream.Write(bytes,0,bytes.Length);
                    if(frame==0 || frame==Mathf.CeilToInt(seconds*FPS)-1) File.Copy(image,Path.Combine(Root,name+(frame==0?"-first":"-last")+".jpg"),true);
                }
                process.StandardInput.Close();process.WaitForExit();Assert.That(process.ExitCode,Is.Zero,name);
            }
        }
        static void RestartTennis(TennisGame game,string cut,bool shared=false) {
            PresentationPolicy.Configure(cut,false,true,shared); game.AutoPlay=false;game.NativeControlled=true;game.ManualSimulation=false;game.Drill=false;
            var intro=game.GetComponent<TennisPresentation>();
            typeof(TennisPresentation).GetField("t",Private).SetValue(intro,0f);
            foreach(string flag in new[]{"skipped","rivalEmoted","playerEmoted","umpireCalled","chosenPlayed"}) typeof(TennisPresentation).GetField(flag,Private).SetValue(intro,false);
            typeof(TennisPresentation).GetMethod("Configure",Private).Invoke(intro,new object[]{shared});
        }
        static void Point(TennisGame game,string beat) {
            game.GetComponent<TennisPresentation>().Finish();
            var match=TennisMatch.New(true,beat=="set"?2:1,3);
            if(beat=="game" || beat=="set" || beat=="match")match.PlayerPoints=3;
            if(beat=="set" || beat=="match")match.PlayerGames=2;
            typeof(TennisGame).GetField("match",Private).SetValue(game,match);
            typeof(TennisGame).GetField("matchReported",Private).SetValue(game,false);
            typeof(TennisGame).GetProperty("Feedback").GetSetMethod(true).Invoke(game,new object[]{beat=="ace"?"ACE":"WINNER"});
            game.InjectBall(new Vector3(0,1,-8),Vector3.zero);
            typeof(TennisGame).GetMethod("AwardPoint",Private).Invoke(game,new object[]{beat!="loss",beat=="ace"});
            game.HoldNextPoint(10);game.RequestEquippedEmote(1);
        }
        [UnityTest,Explicit,Timeout(1800000)] public IEnumerator FilmChangedGameFlows() {
            int oldRate=Time.captureFramerate;Time.captureFramerate=FPS;Time.timeScale=1;
            Directory.CreateDirectory(Root);
            try {
                ClearSeen();PresentationPolicy.Configure("full",false,true,false);
                yield return SceneManager.LoadSceneAsync("Tennis");yield return null;
                var tennis=Object.FindFirstObjectByType<TennisGame>();
                RestartTennis(tennis,"full");yield return Film("L2_L3_L4_tennis_full_tv_editor",9);
                RestartTennis(tennis,"full");yield return Film("L2_L3_L4_tennis_repeat_short_tv_editor",5);
                ClearSeen();RestartTennis(tennis,"full");yield return Film("L2_L3_L4_tennis_skip_phone_editor",4,f=>{if(f==20)tennis.RequestSwing(.8f);},true);
                RestartTennis(tennis,"off");yield return Film("L4_tennis_off_tv_editor",2);
                RestartTennis(tennis,"short",true);yield return Film("L2_L3_L4_tennis_shared_skip_rejected_tv_editor",5,f=>{if(f==12)tennis.RequestSwing(.8f);});
                PresentationPolicy.Configure("full",false,true,false);
                foreach(var beat in new[]{"ordinary","ace","loss","game","set","match"}) {
                    Point(tennis,beat);
                    yield return Film((beat=="match"?"L7":beat=="game"||beat=="set"?"L6":"L5")+"_tennis_"+beat+"_tv_editor",beat=="match"?8:beat=="set"?6:4);
                }
                Point(tennis,"match");yield return Film("L7_tennis_match_skip_tv_editor",4,f=>{if(f==34)tennis.RequestSwing(.8f);});
                ClearSeen();PresentationPolicy.Configure("full",false,true,false);
                yield return SceneManager.LoadSceneAsync("Golf");yield return null;
                var golf=Object.FindFirstObjectByType<GolfGame>();
                golf.PrepareNativeAddress();golf.NativeReady();golf.TryNativePresentation();
                yield return Film("L2_L3_L4_golf_full_tv_editor",8);
                Assert.That(PresentationPolicy.Cut("golf.start"),Is.EqualTo(PresentationCut.Short),"A naturally completed full address settle must use short on repeats");
                golf.PrepareNativeAddress();golf.NativeReady();golf.TryNativePresentation();
                yield return Film("L2_L3_L4_golf_repeat_short_tv_editor",5);
                PresentationPolicy.Configure("off",false,true,false);golf.PrepareNativeAddress();golf.NativeReady();golf.TryNativePresentation();
                yield return Film("L4_golf_off_phone_editor",2,null,true);
                ClearSeen();PresentationPolicy.Configure("full",false,true,false);golf.PrepareNativeAddress();golf.NativeReady();golf.TryNativePresentation();
                yield return Film("L2_L3_L4_golf_skip_tv_editor",4,f=>{if(f==20)golf.NativeContinue();});
                var ball=golf.BallPosition;golf.Overview();yield return Film("L2_golf_overview_return_tv_editor",5);
                Assert.That(golf.BallPosition,Is.EqualTo(ball));
                golf.Overview();yield return Film("L2_golf_overview_motion_skip_phone_editor",3,f=>{if(f==20)golf.NativeMotion(new NativeSportsSession.Sample {qw=1,rx=9,time=10});},true);
                Assert.That(golf.BallPosition,Is.EqualTo(ball));
                // A genuine short putt provides the authoritative holed result; vary the
                // already-counted strokes to review boundary precedence without altering physics.
                foreach(var outcome in new[]{"par","birdie","eagle","hole_in_one","hole_in_one_skip","round_end"}) {
                    golf.PrepareNativeAddress();golf.NativeReady();
                    if(outcome=="round_end") {
                        var course=(GolfArcade.Course.Course)typeof(GolfGame).GetField("course",Private).GetValue(golf);
                        golf.JumpToHole(course.Holes[course.Holes.Length-1].Number);
                    }
                    golf.DropBall(new CoursePoint(golf.CurrentHole.Pin.X,golf.CurrentHole.Pin.D-3));
                    typeof(GolfGame).GetField("holeStrokes",Private).SetValue(golf,outcome.StartsWith("hole_in_one")?0:outcome=="birdie"?golf.CurrentHole.Par-2:outcome=="eagle"?golf.CurrentHole.Par-3:golf.CurrentHole.Par-1);
                    golf.StrikeHolingPutt();
                    for(int f=0;golf.Current==GolfGame.State.Flight && f<1000;f++)yield return null;
                    Assert.True(golf.LastShot.IsHoled);
                    yield return Film((outcome=="round_end"?"L7":"L5_L6")+"_golf_"+outcome+"_tv_editor",outcome=="round_end"?8:6,f=>{if(outcome.EndsWith("skip") && f==24)golf.NativeContinue();});
                }
                // Actual synthesized Unity cue for audio review, separate from silent films.
                PresentationStinger.Lead();var stinger=Object.FindFirstObjectByType<PresentationStinger>();
                var clip=(AudioClip)typeof(PresentationStinger).GetField("cue",Private).GetValue(stinger);
                var data=new float[clip.samples];clip.GetData(data,0);
                using(var w=new BinaryWriter(File.Create(Path.Combine(Root,"P7_presentation_stinger.wav")))) {
                    w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));w.Write(36+data.Length*2);w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));w.Write(16);w.Write((short)1);w.Write((short)1);w.Write(clip.frequency);w.Write(clip.frequency*2);w.Write((short)2);w.Write((short)16);w.Write(System.Text.Encoding.ASCII.GetBytes("data"));w.Write(data.Length*2);foreach(float value in data)w.Write((short)(Mathf.Clamp(value,-1,1)*32767));
                }
            } finally {Time.captureFramerate=oldRate;PresentationPolicy.Configure("full",false,true,false);PresentationStinger.Stop();}
        }
    }
}
