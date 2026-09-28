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
        [UnityTest, Timeout(180000)] public IEnumerator LobChasePerfectCameraAndUltimate()
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
                game.InjectBall(new Vector3(2,4,-2),new Vector3(0,4,-5));
                bool chased=false, swung=false; int initialHits=game.Hits;
                for(int f=0;f<240;f++) {
                    game.Step(1f/60); yield return null;
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
