using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using GolfArcade.Tennis;
using GolfArcade.Game;

namespace GolfArcade.PlayTests
{
    public class TennisGameplayImprovementPlayTests
    {
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        [UnityTest, Timeout(180000)] public IEnumerator LiveCourtUsesScoreOnlyAndSteadyShoulderCamera()
        {
            yield return SceneManager.LoadSceneAsync("Tennis"); yield return null;
            var game=Object.FindFirstObjectByType<TennisGame>(); game.ManualSimulation=true;
            game.ConfigureMatch(TennisGame.Mode.Training,null,null,null);
            game.InjectBall(new Vector3(1,1.5f,4),new Vector3(0,1,-8));
            yield return null; yield return null;
            var camera=game.GameplayCamera;
            var update=typeof(TennisGame).GetMethod("UpdateCamera",Flags);
            update.Invoke(game,new object[]{true});
            var pos=camera.transform.position; var rot=camera.transform.rotation; float fov=camera.fieldOfView;
            game.Juice.Trigger(TennisJuice.Beat.Perfect,game.Player.transform,true);
            game.Juice.PerfectServe(game.Player.transform,null);
            var fx=(TennisFx)typeof(TennisGame).GetField("fx",Flags).GetValue(game);
            fx.Contact(game.BallPosition,Timing.Perfect,true,true);
            for(int i=0;i<8;i++) { yield return null; update.Invoke(game,new object[]{false}); }
            Assert.Less(Vector3.Distance(pos,camera.transform.position),.0001f);
            Assert.Less(Quaternion.Angle(rot,camera.transform.rotation),.001f); Assert.AreEqual(fov,camera.fieldOfView);
            var hud=game.GetComponent<TennisHud>(); hud.ShowGrade(Timing.Perfect,true,"SHOULD BE HIDDEN"); hud.ShowCall("POINT","SHOULD BE HIDDEN",true);
            yield return null;
            var visible=Object.FindObjectsByType<Text>(FindObjectsSortMode.None).Where(t=>t.isActiveAndEnabled && t.text.Length>0 && t.color.a>.01f && t.canvasRenderer.GetAlpha()>.01f);
            Assert.IsTrue(visible.All(t=>t.transform.IsChildOf(hud.RootForTests())),string.Join(" | ",visible.Select(t=>t.gameObject.name+":"+t.text)));
            string dir=System.Environment.GetEnvironmentVariable("GAMEPLAY_PROOF_DIR") ?? "Library/Captures/gameplay-improvements";
            Directory.CreateDirectory(dir);
            GameCapture.Save(dir+"/shoulder-score-only.png",1280,720);
            var shoulderPos=camera.transform.position; var shoulderRot=camera.transform.rotation; float shoulderFov=camera.fieldOfView;
            camera.transform.position=new Vector3(game.Player.transform.position.x*.45f,5.8f,game.Player.transform.position.z-5.8f);
            camera.transform.LookAt(new Vector3(game.Player.transform.position.x*.26f,.95f,game.Player.transform.position.z+7.2f)); camera.fieldOfView=54;
            GameCapture.Save(dir+"/previous-camera-comparison.png",1280,720);
            camera.transform.SetPositionAndRotation(shoulderPos,shoulderRot);camera.fieldOfView=shoulderFov;
            foreach(float across in new[] {-1f,0,1}) foreach(float depth in new[] {0f,.5f,1}) {
                game.SetShotAim(across,depth); yield return null;
                GameCapture.Save($"{dir}/aim-{across}-{depth}.png",1280,720);
            }
        }

        [UnityTest, Timeout(180000)] public IEnumerator CleanShotLandingsKeepLockedAimAcrossTheCourt()
        {
            yield return SceneManager.LoadSceneAsync("Tennis"); yield return null;
            var game=Object.FindFirstObjectByType<TennisGame>(); game.ManualSimulation=true;
            var launch=typeof(TennisGame).GetMethod("ReturnBall",Flags);
            var lockAim=typeof(TennisGame).GetMethod("LockShotAim",Flags);
            var rows=new System.Text.StringBuilder("playerX,aim,depth,landingX,landingZ,errorMetres\n");
            foreach(float playerX in new[] {-2f,0,2}) foreach(float aim in new[] {-1f,0,1}) foreach(float depth in new[] {0f,.5f,1}) {
                game.ConfigureMatch(TennisGame.Mode.Training,null,null,null);
                game.Player.CancelSwing();game.Player.Tick(2,0);
                game.Player.transform.position=new Vector3(playerX,.035f,-11.2f);
                game.Player.Swing(.8f,false,TennisActor.Stroke.Drive);game.Player.Tick(.18f,0);game.Player.Pose();
                game.InjectBall(game.Player.SweetSpot.position,Vector3.back*10);
                game.SetShotAim(aim,depth); lockAim.Invoke(game,null);
                Vector3 target=game.ShotAimTarget;
                game.SetShotAim(-aim,1-depth); // Follow-through must not alter this contact's target.
                Assert.AreEqual(target,game.ShotAimTarget);
                launch.Invoke(game,new object[] {new TennisHit { Contact=true,Timing=1,Quality=1,Speed=30,ErrorDegrees=.6f },Vector2.zero});
                Assert.IsTrue(TennisBall.Landing(game.BallPosition,game.BallVelocity,game.BallSpin,out var at,out _));
                Assert.Less(Vector3.Distance(at,target),.15f);
                rows.AppendLine(System.FormattableString.Invariant($"{playerX},{aim},{depth},{at.x:F3},{at.z:F3},{Vector3.Distance(at,target):F3}"));
                yield return null;
            }
            string dir=System.Environment.GetEnvironmentVariable("GAMEPLAY_PROOF_DIR") ?? "Library/Captures/gameplay-improvements";
            Directory.CreateDirectory(dir); File.WriteAllText(dir+"/placement-landings.csv",rows.ToString());
        }

        [UnityTest, Timeout(180000)] public IEnumerator ServeOnsetWaitsForConfirmationAndCannotPromoteAnAbortedGesture()
        {
            yield return SceneManager.LoadSceneAsync("Tennis"); yield return null;
            var game=Object.FindFirstObjectByType<TennisGame>();game.ManualSimulation=true;
            var flow=typeof(TennisGame).GetProperty("Flow");
            var timer=typeof(TennisGame).GetField("phaseTimer",Flags);
            var committed=typeof(TennisGame).GetField("serveCommitted",Flags);
            var verdict=typeof(TennisGame).GetField("pendingServe",Flags);
            foreach(float frame in new[] {1f/30,1f/60}) {
                game.ConfigureMatch(TennisGame.Mode.Training,null,null,null);
                game.Player.CancelSwing();game.Player.Tick(2,0);game.NativeControlled=true;
                flow.SetValue(game,TennisGame.Phase.PlayerServeToss);
                float original=TennisRules.ServeApex+TennisRules.ServeOnsetLatency+frame+game.Lag;
                timer.SetValue(game,original);game.BeginSwing(0,0,1,frame);
                Assert.IsFalse((bool)committed.GetValue(game),"onset is not yet a confirmed stroke");
                timer.SetValue(game,original+.2f);game.RequestSwing(.8f,0,0,1,frame);
                Assert.IsTrue((bool)committed.GetValue(game));
                Assert.IsTrue(((TennisRules.ServeJudgement)verdict.GetValue(game)).Perfect,"confirmation must grade the stored onset, not its later frame");
                game.Player.CancelSwing();game.Player.Tick(2,0);game.Refeed();flow.SetValue(game,TennisGame.Phase.PlayerServeToss);
                timer.SetValue(game,original);game.BeginSwing(0,0,1,frame);game.AbortSwing();
                timer.SetValue(game,original+.2f);game.RequestSwing(.8f,0,0,1,frame);
                Assert.IsFalse(((TennisRules.ServeJudgement)verdict.GetValue(game)).Perfect,"aborted onset cannot make a later stroke perfect");
            }
        }

        [UnityTest, Timeout(180000)] public IEnumerator ReachableHighBallNeverSwingsWithoutInput()
        {
            yield return SceneManager.LoadSceneAsync("Tennis");yield return null;
            var game=Object.FindFirstObjectByType<TennisGame>();game.ManualSimulation=true;
            game.ConfigureMatch(TennisGame.Mode.Training,null,null,null);
            int before=game.AutoSmashAttempts;
            game.InjectBall(new Vector3(1,4,-2),TennisAbilities.LobVelocity(new Vector3(1,4,-2),new Vector3(1,TennisRules.BallRadius,-8.5f),5));
            bool tracked=false;
            for(int i=0;i<210;i++) { game.Step(1f/60);tracked|=game.TrackingOverhead;yield return null; }
            Assert.IsTrue(tracked);Assert.AreEqual(before,game.AutoSmashAttempts);
        }

        [UnityTest, Timeout(180000)] public IEnumerator NativePracticeCommandsCanPauseRetryAndSwitchToTouch()
        {
            var host=new GameObject("Gameplay bridge test");Object.DontDestroyOnLoad(host);
            var bridge=host.AddComponent<NativeSportsSession>();
            const string session="gameplay-setup-check";
            void Command(string action,float value=0,float depth=0,string id=session) => bridge.Receive(JsonUtility.ToJson(new NativeSportsSession.Message {version=1,session=id,action=action,value=value,value2=depth,aimSequence=19}));
            try {
                bridge.Receive(JsonUtility.ToJson(new NativeSportsSession.Message {version=1,session=session,action="start",sport="tennis",mode="training",touch=false}));
                for(int f=0;f<600 && !bridge.Ready;f++) {
                    // Batch mode has no visible Game view; readiness still needs a real camera render.
                    if(bridge.GameplayCamera) GameCapture.Save("Library/Captures/gameplay-improvements/native-ready.png",640,360);
                    yield return null;
                }
                Assert.IsTrue(bridge.Ready);
                var game=Object.FindFirstObjectByType<TennisGame>();
                Command("aimPractice",1);Assert.IsTrue(game.AimPractice);
                Command("rallyAim",-.7f,.3f);Assert.AreEqual(TennisRules.PlacementTarget(-.7f,.3f),game.ShotAimTarget);
                Command("aimFeed",1,1,"obsolete-session");Assert.AreEqual(0,game.AimPracticeSequence);
                Command("aimFeed",1,1);Assert.AreEqual(19,game.AimPracticeSequence);
                Command("pause");Assert.AreEqual(0,Time.timeScale);
                Command("resume");Assert.AreEqual(1,Time.timeScale);
                Command("timingCheck");Assert.IsTrue(game.CheckingTiming);
                Command("touch");Assert.IsFalse(game.CheckingTiming);Assert.IsFalse(game.AimPractice);Assert.IsFalse(game.Drill);
                Assert.IsTrue(NativeSportsSession.Touch);Assert.IsTrue(game.Serving);
            } finally { Command("end");Object.DestroyImmediate(host);Time.timeScale=1; }
        }

        [UnityTest, Timeout(180000)] public IEnumerator AimPracticeDoesNotChangeMatchScoreAndRestoresPlay()
        {
            yield return SceneManager.LoadSceneAsync("Tennis");yield return null;
            var game=Object.FindFirstObjectByType<TennisGame>();game.ManualSimulation=true;
            game.ConfigureMatch(TennisGame.Mode.Training,null,null,null);
            string score=game.Match.Scoreboard;int hits=game.Hits;
            game.SetAimPractice(true);game.FeedAimPractice(-1,false,17);
            Assert.IsTrue(game.Drill);Assert.AreEqual(17,game.AimPracticeSequence);
            for(int i=0;i<180;i++) { game.Step(1f/60);yield return null; }
            Assert.AreEqual(score,game.Match.Scoreboard);
            string dir=System.Environment.GetEnvironmentVariable("GAMEPLAY_PROOF_DIR") ?? "Library/Captures/gameplay-improvements";
            Directory.CreateDirectory(dir);GameCapture.Save(dir+"/aim-calibration-court.png",1280,720);
            game.SetAimPractice(false);
            Assert.IsFalse(game.Drill);Assert.IsFalse(game.AimPractice);Assert.AreEqual(hits,game.Hits);
            Assert.AreEqual(score,game.Match.Scoreboard);Assert.IsTrue(game.Serving);
        }
    }
    static class GameplayHudTestAccess
    {
        public static Transform RootForTests(this TennisHud hud) => (Transform)typeof(TennisHud).GetField("plaque",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(hud);
    }
}
