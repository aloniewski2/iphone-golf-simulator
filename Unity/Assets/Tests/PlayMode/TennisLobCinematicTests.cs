using System.Collections;
using System.IO;
using GolfArcade.Game;
using GolfArcade.Tennis;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GolfArcade.PlayTests
{
    public class TennisLobCinematicTests
    {
        [UnityTest, Timeout(180000)] public IEnumerator ReachableLobsKeepTheirOverheadContact()
        {
            yield return SceneManager.LoadSceneAsync("Tennis"); yield return null;
            var game=Object.FindFirstObjectByType<TennisGame>(); game.ManualSimulation=true;
            int oldRate=Time.captureFramerate; Time.captureFramerate=60;
            var results=new System.Collections.Generic.List<string>();
            int returned=0;
            try {
                foreach (float apex in new[]{4.5f,6f,8f})
                foreach (float landingX in new[]{-2f,0f,2f}) {
                    game.ConfigureMatch(TennisGame.Mode.Exhibition,null,null,null);
                    game.AutoPlay=false; game.Player.CancelSwing(); game.Player.Tick(2,0);
                    game.Player.transform.position=new Vector3(0,.035f,-11.2f);
                    var from=new Vector3(1,1.5f,5);
                    game.InjectBall(from,TennisAbilities.LobVelocity(from,new Vector3(landingX,TennisRules.BallRadius,-8.5f),apex));
                    int hits=game.Hits, attempts=game.AutoSmashAttempts; bool tracked=false;
                    var trace=new System.Text.StringBuilder();
                    for(int f=0;f<300 && game.Hits==hits && game.Flow==TennisGame.Phase.Rally;f++) {
                        game.Step(1f/60);
                    if(game.TrackingOverhead && !game.Player.Swinging) { var op=(TennisRules.InterceptPlan)typeof(TennisGame).GetField("overheadPlan",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(game); if(op.Time>.1f && op.Time<.3f) game.RequestSwing(.85f); }
                    yield return null; tracked |= game.TrackingOverhead;
                        if(f%6==0) trace.AppendLine($"{f/60f:F2},ball={game.BallPosition:F2},player={game.Player.transform.position:F2},goal={game.MoveGoal:F2},tracking={game.TrackingOverhead},stroke={game.Player.Kind},swing={game.Player.Swinging},gap={game.LastContactGap:F3}");
                    }
                    if(game.Hits>hits) returned++;
                    string line=$"apex={apex} x={landingX} tracked={tracked} attempts={game.AutoSmashAttempts-attempts} hits={game.Hits-hits} gap={game.LastContactGap}";
                    Debug.Log("[LobAudit] "+line); results.Add(line);
                    Directory.CreateDirectory("Library/Captures/lob-audit");
                    File.WriteAllText($"Library/Captures/lob-audit/{apex}-{landingX}.txt",trace.ToString());
                }
                Assert.That(returned,Is.EqualTo(9),string.Join("\n",results));
            } finally { Time.captureFramerate=oldRate; }
        }

        [UnityTest, Timeout(180000)] public IEnumerator ManualLobChaseKeepsGameplayCameraAndOptionalUltimate()
        {
            yield return SceneManager.LoadSceneAsync("Tennis"); yield return null;
            var game=Object.FindFirstObjectByType<TennisGame>(); game.ManualSimulation=true;
            Time.captureFramerate=60;
            string dir="Library/Captures/lob-cinematic"; Directory.CreateDirectory(dir);
            int frame=0;
            try {
                game.ConfigureMatch(TennisGame.Mode.Training,null,null,null);
                game.Player.CancelSwing(); game.Player.Tick(2,0);
                game.Player.transform.position=new Vector3(0,.035f,-11.2f);
                var from=new Vector3(1,1.5f,5);
                game.InjectBall(from,TennisAbilities.LobVelocity(from,new Vector3(2,TennisRules.BallRadius,-8.5f),6));
                bool chased=false, swung=false; int initialHits=game.Hits;
                for(int f=0;f<240;f++) {
                    game.Step(1f/60);
                    if(game.TrackingOverhead && !game.Player.Swinging) { var op=(TennisRules.InterceptPlan)typeof(TennisGame).GetField("overheadPlan",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(game); if(op.Time>.1f && op.Time<.3f) game.RequestSwing(.85f); }
                    yield return null;
                    chased |= game.TrackingOverhead; swung |= game.Player.Kind==TennisActor.Stroke.Smash && game.Player.Swinging;
                    GameCapture.Save($"{dir}/frame-{frame++:D4}.jpg",1280,720);
                }
                Debug.Log($"[LobProof] tracked={chased} attempts={game.AutoSmashAttempts} swung={swung} hits={game.Hits-initialHits} gap={game.LastContactGap}");
                Assert.IsTrue(chased); Assert.IsTrue(swung); Assert.Greater(game.AutoSmashAttempts,0); Assert.Greater(game.Hits,initialHits,"reachable lob should produce a real overhead return");
                // Point/reaction cameras must not contaminate the isolated perfect-hit checks.
                var go=new GameObject("Isolated presentation test"); var j=go.AddComponent<TennisJuice>();
                j.Trigger(TennisJuice.Beat.Perfect,game.Player.transform,true);
                Assert.IsFalse(j.OverrideCamera(game.GameplayCamera));
                j.PerfectServe(game.Player.transform,null);
                Assert.IsFalse(j.OverrideCamera(game.GameplayCamera));
                j.Trigger(TennisJuice.Beat.Smash,game.Player.transform); Assert.IsFalse(j.OverrideCamera(game.GameplayCamera));
                Object.Destroy(go);
                if(!TennisAbilities.UltimatesEnabled) yield break;
                foreach(var name in new[]{"Skybreaker","Rescue Lob","Curveball"}) {
                    game.Player.CancelSwing(); game.Player.Tick(2,0); game.Player.Swing(.8f,false,name=="Skybreaker" ? TennisActor.Stroke.Smash : TennisActor.Stroke.Drive); game.Player.Tick(.08f,0); game.Player.Pose(); yield return null;
                    game.Juice.UltimateCharge(game.Player.transform,"YOU",name);
                    Assert.IsTrue(game.Juice.UltimateActive); Assert.That(Time.timeScale,Is.Zero);
                    for(int f=0;f<150;f++) { yield return null; GameCapture.Save($"{dir}/frame-{frame++:D4}.jpg",1280,720); }
                    Assert.IsFalse(game.Juice.UltimateActive); Assert.That(Time.timeScale,Is.EqualTo(1));
                }
            } finally { Time.captureFramerate=0; Time.timeScale=1; }
        }
    }
}
