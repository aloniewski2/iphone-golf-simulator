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
            Assert.Less(game.Player.SwingDuration,soft); Assert.Greater(game.Player.SwingDuration,.6f);
            Assert.That(game.Player.StrokeLabel,Does.Contain("backhand"));
            game.Player.Tick(1,0);
            game.InjectBall(game.Player.transform.position+new Vector3(0,2.3f,1),Vector3.zero);
            game.RequestSwing(.8f,0,.3f); Assert.IsTrue(game.Player.Overhead);
            game.Player.Tick(game.Player.SwingDuration*.4f,0);
            Assert.Greater(game.Player.SweetSpot.position.y,1.8f);
        }
        [UnityTest]
        public IEnumerator RallyStartsWithVisibleServeThenPlayableBall()
        {
            yield return SceneManager.LoadSceneAsync("Tennis",LoadSceneMode.Single);
            yield return null;
            var game=Object.FindFirstObjectByType<TennisGame>(); game.ManualSimulation=true; game.Refeed();
            Assert.IsTrue(game.Serving); Assert.AreEqual(Vector3.zero,game.BallVelocity);
            Assert.Greater(GameObject.Find("Tennis ball").transform.localScale.x,.15f);
            for(int i=0;i<110;i++) game.Step(1f/120);
            Assert.IsFalse(game.Serving); Assert.Less(game.BallVelocity.z,0); Assert.Less(Mathf.Abs(game.BallVelocity.z),15);
            float start=game.Player.transform.position.x;
            game.SetLateralInput(1,false); for(int i=0;i<30;i++) game.Step(1f/120);
            Assert.Greater(game.Player.transform.position.x,start);
            game.RequestSwing(.5f); Assert.IsTrue(game.Player.Swinging);
            Assert.Less(game.Stamina,1);
            game.Refeed(); Assert.IsTrue(game.Serving); Assert.AreEqual(1,game.Stamina);
        }
        [UnityTest]
        public IEnumerator ApprovedArenaAndBothStandardsRunTennisPhysics()
        {
            yield return SceneManager.LoadSceneAsync("Tennis",LoadSceneMode.Single);
            yield return null;
            var game = Object.FindFirstObjectByType<TennisGame>();
            Assert.IsNotNull(game); game.ManualSimulation = true;
            Assert.IsNotNull(GameObject.Find("Approved coastal tennis resort"));
            Assert.IsNotNull(game.Player.SweetSpot);
            int oldRate = Time.captureFramerate; Time.captureFramerate = 60;
            try
            {
                foreach (bool female in new[] {false,true})
                {
                    game.SelectCharacter(female); yield return null;
                    int hitsBefore = game.Hits;
                    Assert.IsTrue(game.Player.GetComponentInChildren<StandardCharacterArms>().FloatingHandsPreview);
                    string directory = "Library/Captures/tennis-" + (female ? "female" : "male"); Directory.CreateDirectory(directory);
                    float before = game.Stamina;
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
                        Assert.IsFalse(float.IsNaN(game.BallPosition.x));
                        yield return null;
                        if (frame == 100) Assert.Less(game.Stamina,before);
                        Assert.IsNotNull(GameCapture.Save($"{directory}/frame-{frame:D4}.png",720,480));
                    }
                    Assert.GreaterOrEqual(game.Hits-hitsBefore,3,"Each character must return both forehands and the backhand");
                }
            }
            finally { Time.captureFramerate=oldRate; }
        }
    }
}
