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
                    for (int frame=0;frame<360;frame++)
                    {
                        game.SetLateralInput(frame<55 ? 1 : frame<100 ? -1 : 0, true);
                        if (frame == 120)
                        {
                            game.InjectBall(game.Player.transform.position+new Vector3(1,1,2),Vector3.zero);
                            game.RequestSwing(.8f);
                        }
                        // Controlled contact fixture through the same live swept-string-bed path.
                        if (frame == 138)
                        {
                            Vector3 center=game.Player.SweetSpot.position, normal=game.Player.StringNormal;
                            game.InjectBall(center+normal*.12f,-normal*18);
                        }
                        game.Step(1f/120);game.Step(1f/120);
                        Assert.IsFalse(float.IsNaN(game.BallPosition.x));
                        yield return null;
                        if (frame == 100) Assert.Less(game.Stamina,before);
                        Assert.IsNotNull(GameCapture.Save($"{directory}/frame-{frame:D4}.png",720,480));
                    }
                    Assert.Greater(game.Hits,hitsBefore,"Each character's timed racket-center contact must return a shot");
                }
            }
            finally { Time.captureFramerate=oldRate; }
        }
    }
}
