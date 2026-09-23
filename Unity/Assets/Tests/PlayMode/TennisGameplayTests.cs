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
    public class TennisGameplayTests
    {
        [UnityTest] public IEnumerator StrengthAndPhonePositionChooseReadableStrokes()
        {
            yield return SceneManager.LoadSceneAsync("Tennis",LoadSceneMode.Single); yield return null;
            var game=Object.FindFirstObjectByType<TennisGame>(); game.ManualSimulation=true;
            game.InjectBall(game.Player.transform.position+new Vector3(1,1,1),Vector3.zero);
            game.RequestSwing(.2f,.2f); float soft=game.Player.SwingDuration;
            Assert.That(game.Player.StrokeLabel,Does.Contain("Soft"));
            game.Player.Tick(1,0); game.RequestSwing(.9f,-.2f);
            // Strokes were deliberately shortened from 0.62-0.90s to 0.40-0.58s: the old
            // timing read as slow motion. Still ordered by power, just faster.
            Assert.Less(game.Player.SwingDuration,soft); Assert.Greater(game.Player.SwingDuration,.35f);
            Assert.Less(game.Player.SwingDuration,.5f);
            Assert.That(game.Player.StrokeLabel,Does.Contain("backhand"));
            game.Player.Tick(1,0);
            game.InjectBall(game.Player.transform.position+new Vector3(0,2.3f,1),Vector3.zero);
            game.RequestSwing(.8f,0,.3f); Assert.IsTrue(game.Player.Overhead);
            game.Player.Tick(game.Player.SwingDuration*.4f,0);
            Assert.Greater(game.Player.SweetSpot.position.y,1.8f);
        }
        /// Drives the character from one corner to the other and back, capturing frames.
        /// This is the path the locomotion work actually changed -- the run clips, the
        /// crossfade between RunLeft and RunRight, and the stride rate that now follows real
        /// ground speed -- and none of it is exercised by the stroke-focused tests.
        [UnityTest]
        public IEnumerator RunCycleCoversTheCourtBothWays()
        {
            yield return SceneManager.LoadSceneAsync("Tennis",LoadSceneMode.Single);
            yield return null;
            var game=Object.FindFirstObjectByType<TennisGame>(); game.ManualSimulation=true;
            string directory="Library/Captures/run-cycle"; Directory.CreateDirectory(directory);
            int oldRate=Time.captureFramerate; Time.captureFramerate=60;
            float reachedRight=-99, reachedLeft=99;
            try
            {
                int frame=0;
                // Feed alternate corners often enough that a ball is always live, otherwise the
                // point ends and the serve lock plants the character mid-run.
                foreach (float target in new[]{4.2f,-4.2f,4.2f,-4.2f,3.8f,-3.8f})
                {
                    // Movement is human now: an 8m switch is only covered with a good jump,
                    // i.e. leaning toward the ball while it is being read.
                    game.InjectBall(new Vector3(target,1.3f,-4.5f),new Vector3(0,1.5f,-6.5f));
                    game.SetLateralInput(Mathf.Sign(target),true);
                    for(int i=0;i<100;i++)
                    {
                        game.Step(1f/120); game.Step(1f/120);
                        float x=game.Player.transform.position.x;
                        reachedRight=Mathf.Max(reachedRight,x); reachedLeft=Mathf.Min(reachedLeft,x);
                        yield return null;
                        GameCapture.Save($"{directory}/frame-{frame:D4}.png",720,480); frame++;
                    }
                }
            }
            finally { Time.captureFramerate=oldRate; }
            // It has to actually cover ground both ways, or the capture proves nothing.
            Assert.Greater(reachedRight,2.5f,"character must chase the ball out to the right");
            Assert.Less(reachedLeft,-2.5f,"and back out to the left");
        }

        [UnityTest]
        public IEnumerator RallyStartsWithVisibleServeThenPlayableBall()
        {
            yield return SceneManager.LoadSceneAsync("Tennis",LoadSceneMode.Single);
            yield return null;
            var game=Object.FindFirstObjectByType<TennisGame>(); game.ManualSimulation=true; game.Refeed();
            // Every point now starts from a serve. The player serves first, and the toss is
            // automatic: the ball sits in the hand, goes up on its own, and must be struck.
            Assert.IsTrue(game.Serving); Assert.AreEqual(Vector3.zero,game.BallVelocity);
            Assert.AreEqual(TennisGame.Phase.PlayerServeHold,game.Flow);
            Assert.Greater(GameObject.Find("Tennis ball").transform.localScale.x,.15f);
            for(int i=0;i<(int)(TennisRules.ServeTossDelay*120)+2;i++) game.Step(1f/120);
            Assert.AreEqual(TennisGame.Phase.PlayerServeToss,game.Flow,"toss must launch on its own");
            // Not swinging is not a fault: it drops back to the hand and is tossed again.
            for(int i=0;i<(int)(TennisRules.ServeCatch*120)+2;i++) game.Step(1f/120);
            Assert.AreEqual(TennisGame.Phase.PlayerServeHold,game.Flow);
            Assert.IsFalse(game.SecondServe,"a caught toss must not cost a fault");
            // Toss again, then strike it near the apex: the ball leaves toward the far court.
            for(int i=0;i<(int)(TennisRules.ServeTossDelay*120)+2 && game.Flow!=TennisGame.Phase.PlayerServeToss;i++) game.Step(1f/120);
            Assert.AreEqual(TennisGame.Phase.PlayerServeToss,game.Flow,"the ball is tossed again");
            for(int i=0;i<(int)(TennisRules.ServeIdealContact*120);i++) game.Step(1f/120);
            game.RequestSwing(.8f,0,.3f);
            // The ball leaves when the animated racket reaches it, not at the instant the
            // swing is reported, so the serve never flies before it has been hit.
            for(int i=0;i<60 && game.Flow!=TennisGame.Phase.Rally;i++) game.Step(1f/120);
            Assert.AreEqual(TennisGame.Phase.Rally,game.Flow,"a well-timed serve starts the rally");
            Assert.Greater(game.BallVelocity.z,0,"the player's serve travels to the far end");
            // Wii Sports model: the character takes itself to the ball. Put one out wide and
            // it must close on the bounce without any movement input at all.
            game.InjectBall(new Vector3(4f,1.4f,-4f),new Vector3(0,1.2f,-6f));
            float start=game.Player.transform.position.x;
            for(int i=0;i<180;i++) game.Step(1f/120);
            Assert.Greater(game.Player.transform.position.x,start+1f,
                $"auto-positioning must chase a wide ball (flow={game.Flow} predicted={game.PredictedInterceptX:0.00} ball={game.BallPosition} x={game.Player.transform.position.x:0.00} start={start:0.00})");
            game.SetLateralInput(-1,false); float held=game.Player.transform.position.x;
            for(int i=0;i<30;i++) game.Step(1f/120);
            Assert.Greater(game.Player.transform.position.x,held-1f,"steering input must no longer drive position");
            game.Refeed(); Assert.IsTrue(game.Serving); Assert.AreEqual(1,game.Stamina);
        }
        [UnityTest]
        public IEnumerator ApprovedArenaAndBothStandardsRunTennisPhysics()
        {
            yield return SceneManager.LoadSceneAsync("Tennis",LoadSceneMode.Single);
            yield return null;
            var game = Object.FindFirstObjectByType<TennisGame>();
            Assert.IsNotNull(game); game.ManualSimulation = true;
            Assert.IsNotNull(GameObject.Find("Tropical tennis resort v3 — live arena"));
            Assert.IsNotNull(game.Player.SweetSpot);
            int oldRate = Time.captureFramerate; Time.captureFramerate = 60;
            try
            {
                foreach (bool female in new[] {false,true})
                {
                    game.SelectCharacter(female); yield return null;
                    int hitsBefore = game.Hits;
                    // Tennis characters show real arms: the fitted Higgsfield body is one skinned
                    // mesh with continuous arms, replacing the procedural arm tubes.
                    Assert.IsTrue(game.Player.SkinnedBody, "tennis characters should use the fitted skinned body");
                    Assert.IsTrue(System.Array.Exists(game.Player.GetComponentsInChildren<SkinnedMeshRenderer>(),
                        r => r.name.StartsWith("V4 Higgs body") && r.enabled), "tennis characters should have visible arms");
                    // The clothes are part of the fitted body; the old runtime kit must not be layered on.
                    Assert.IsNull(game.Player.transform.Find("Permanent "+(female?"female":"male")+" tennis player/Fitted Tripo tennis kit v3"));
                    string directory = "Library/Captures/tennis-" + (female ? "female" : "male"); Directory.CreateDirectory(directory);
                    // A serving player has their feet planted, so movement and stamina cannot
                    // be exercised until a ball is live. This test is about physics, the
                    // racket and the characters; the serve flow is covered separately.
                    // Movement is automatic now, so stamina is exercised by giving the
                    // character somewhere to run rather than by feeding it lateral input.
                    game.InjectBall(new Vector3(4.2f,1.2f,-6f),new Vector3(0,1.5f,-5f));
                    float before = game.Stamina, lowestStamina = game.Stamina;
                    for (int frame=0;frame<480;frame++)
                    {
                        game.SetLateralInput(frame<55 ? 1 : frame<100 ? -1 : 0, true);
                        int swingStart = frame < 220 ? 100 : frame < 340 ? 220 : 340;
                        if (frame == 100 || frame == 220 || frame == 340)
                        {
                            float side = frame == 220 ? -1 : 1;
                            game.InjectBall(game.Player.transform.position+new Vector3(side,1,2),Vector3.zero);
                            game.RequestSwing(frame == 100 ? .55f : .9f);
                        }
                        // Controlled contact fixture through the same live swept-string-bed path.
                        if (frame == swingStart + Mathf.FloorToInt((TennisRules.SweetTime-.012f)*60))
                        {
                            Vector3 center=game.Player.SweetSpot.position, normal=game.Player.StringNormal;
                            game.InjectBall(center+normal*.12f,-normal*30+game.Player.SweetVelocity);
                        }
                        game.Step(1f/120);game.Step(1f/120);
                        lowestStamina = Mathf.Min(lowestStamina, game.Stamina);
                        Assert.Less(game.Player.RacketGripError,.005f,"New racket must stay on the gripping palm");
                        Assert.IsFalse(float.IsNaN(game.BallPosition.x));
                        yield return null;
                        // Recovery outpaces the drain once the character stops, so the dip has
                        // to be sampled during the run, not read afterwards.
                        if (frame == 100) Assert.Less(lowestStamina,before,"auto-running must cost stamina");
                        Assert.IsNotNull(GameCapture.Save($"{directory}/frame-{frame:D4}.png",720,480));
                    }
                    Assert.GreaterOrEqual(game.Hits-hitsBefore,3,"Each character must return both forehands and the backhand");
                }
            }
            finally { Time.captureFramerate=oldRate; }
        }
    }
}
