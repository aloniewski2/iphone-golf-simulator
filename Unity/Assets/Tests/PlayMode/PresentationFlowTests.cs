using System.Collections;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using GolfArcade.Game;
using GolfArcade.Tennis;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace GolfArcade.PlayTests {
    public class PresentationFlowTests {
        [UnityTest,Timeout(120000)] public IEnumerator TennisOffSettlesWithoutStartingASwing() {
            Time.timeScale=1; Time.captureFramerate=60;
            PresentationPolicy.Configure("off",false,true,false);
            yield return SceneManager.LoadSceneAsync("Tennis");
            var game=Object.FindFirstObjectByType<TennisGame>();
            for(int i=0;i<120 && !game.Initialized;i++)yield return null;
            game.NativeControlled=true;game.AutoPlay=false;game.ManualSimulation=false;
            for(int i=0;i<52 && game.IntroPlaying;i++)yield return null;
            Assert.False(game.IntroPlaying,"Off must release within the 0.8 second settle plus a frame");
            Assert.False(game.Player.Swinging,"Skipping/settling cannot strike a serve");
            Assert.That(game.Flow,Is.EqualTo(TennisGame.Phase.PlayerServeHold));
            Time.captureFramerate=0; PresentationPolicy.Configure("full",false,true,false);
        }
        [UnityTest,Timeout(120000)] public IEnumerator GolfOverviewPreservesTheExactShotAndReturnsToAddress() {
            Time.timeScale=1; Time.captureFramerate=60;
            PresentationPolicy.Configure("off",false,true,false);
            yield return SceneManager.LoadSceneAsync("Golf");
            var game=Object.FindFirstObjectByType<GolfGame>(); yield return null;
            game.PrepareNativeAddress(); game.NativeReady(); game.TryNativePresentation();
            for(int i=0;i<45 && game.PresentationBusy;i++)yield return null;
            Assert.That(game.Current,Is.EqualTo(GolfGame.State.Aim));
            var ball=game.BallPosition; double heading=game.TutorialHeading; int hole=game.TutorialHole;
            game.Overview(); Assert.True(game.PresentationBusy);
            for(int i=0;i<250 && game.PresentationBusy;i++)yield return null;
            Assert.That(game.Current,Is.EqualTo(GolfGame.State.Aim));
            Assert.That(game.BallPosition,Is.EqualTo(ball)); Assert.AreEqual(heading,game.TutorialHeading); Assert.AreEqual(hole,game.TutorialHole);
            Assert.True(game.Swing.Armed,"The unchanged shot must be armed after Overview");
            Time.captureFramerate=0; PresentationPolicy.Configure("full",false,true,false);
        }
        [UnityTest,Timeout(120000)] public IEnumerator GolfOverviewSkipConsumesMotionUntilAQuietPose() {
            Time.timeScale=1; Time.captureFramerate=60;
            PresentationPolicy.Configure("off",false,true,false);
            yield return SceneManager.LoadSceneAsync("Golf"); yield return null;
            var game=Object.FindFirstObjectByType<GolfGame>();
            game.PrepareNativeAddress(); game.NativeReady(); game.TryNativePresentation();
            for(int i=0;i<45 && game.PresentationBusy;i++)yield return null;
            var ball=game.BallPosition; game.Overview();
            for(int i=0;i<25;i++)yield return null;
            var sample=new NativeSportsSession.Sample {qw=1,rx=9,time=1};
            game.NativeMotion(sample);
            Assert.That(game.Current,Is.EqualTo(GolfGame.State.Aim));
            var pose=typeof(GolfGame).GetField("needsReadyPose",BindingFlags.Instance|BindingFlags.NonPublic);
            Assert.True((bool)pose.GetValue(game));
            game.NativeStartSwing();
            for(int i=0;i<40;i++) { sample.time+=.01; sample.rx=i%2==0?9:-9; game.NativeMotion(sample); }
            Assert.True((bool)pose.GetValue(game),"Follow-through must not become a new ready pose");
            Assert.That(game.BallPosition,Is.EqualTo(ball));
            sample.rx=0;
            for(int i=0;i<60;i++) { sample.time+=.01; game.NativeMotion(sample); }
            Assert.False((bool)pose.GetValue(game));
            Time.captureFramerate=0; PresentationPolicy.Configure("full",false,true,false);
        }
        [UnityTest,Timeout(120000)] public IEnumerator TwentyOrdinaryAndAceReactionsReachServeWithinOnePointFiveSeconds() {
            Time.timeScale=1; PresentationPolicy.Configure("full",false,true,false);
            yield return SceneManager.LoadSceneAsync("Tennis");
            var game=Object.FindFirstObjectByType<TennisGame>(); yield return null;
            game.NativeControlled=true; game.ManualSimulation=true; game.AutoPlay=false; game.Drill=false;
            game.GetComponent<TennisPresentation>().Finish();
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var award=typeof(TennisGame).GetMethod("AwardPoint",flags);
            var match=typeof(TennisGame).GetField("match",flags);
            var lines=new List<string>(); float maximum=0;
            for(int i=0;i<20;i++) {
                match.SetValue(game,TennisMatch.New(true,1,3));
                typeof(TennisGame).GetProperty("Feedback").GetSetMethod(true).Invoke(game,new object[]{i%2==0?"ACE":"WINNER"});
                game.InjectBall(new Vector3(0,1,-8),Vector3.zero); award.Invoke(game,new object[]{true,i%2!=0});
                game.HoldNextPoint(10);game.RequestEquippedEmote(i%3);
                int steps=0;
                while(game.Flow==TennisGame.Phase.PointOver && steps<240) {game.Step(1f/120);steps++;}
                float seconds=steps/120f/TennisGame.GameSpeed;maximum=Mathf.Max(maximum,seconds);
                lines.Add("{\"sample\":"+i+",\"seconds\":"+seconds.ToString("F4",System.Globalization.CultureInfo.InvariantCulture)+"}");
                Assert.That(seconds,Is.LessThanOrEqualTo(1.5f));
                Assert.That(game.Flow,Is.EqualTo(TennisGame.Phase.PlayerServeHold));
            }
            string dir="Library/Captures/presentation-prerequisites";Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir,"L5_point_to_serve.json"),"{\"clock\":\"fixed simulation steps divided by gameplay speed\",\"samples\":["+string.Join(",",lines)+"],\"maxSeconds\":"+maximum.ToString("F4",System.Globalization.CultureInfo.InvariantCulture)+"}");
            game.ManualSimulation=false;
        }
        [UnityTest,Timeout(120000)] public IEnumerator MainCamerasRecordPostPrerequisiteAndRenderedStills() {
            Time.timeScale=1; PresentationPolicy.Configure("short",false,true,false); TennisQuality.Apply();
            string dir="Library/Captures/presentation-prerequisites";Directory.CreateDirectory(dir);
            foreach(string sport in new[]{"Tennis","Golf"}) {
                yield return SceneManager.LoadSceneAsync(sport); yield return null; yield return null;
                var tennis=Object.FindFirstObjectByType<TennisGame>();var golf=Object.FindFirstObjectByType<GolfGame>();
                if(golf){golf.PrepareNativeAddress();golf.NativeReady();}
                Camera camera=tennis?tennis.GameplayCamera:golf.GameplayCamera;
                var data=camera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                File.WriteAllText(Path.Combine(dir,sport+"-post.txt"),"post="+(data && data.renderPostProcessing)+" tier="+TennisQuality.Current);
                ScreenCapture.CaptureScreenshot(Path.Combine(dir,sport+".png"));
                yield return new WaitForEndOfFrame(); yield return null;
                Assert.True(camera.enabled,"Acceptance uses the actual gameplay camera");
            }
            PresentationPolicy.Configure("full",false,true,false);
        }
    }
}
