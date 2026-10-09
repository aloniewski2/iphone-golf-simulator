using System;
using System.Collections;
using GolfArcade.Game;
using GolfArcade.Tennis;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GolfArcade.PlayTests
{
    public class PlayerBaseTests
    {
        [UnityTest, Timeout(240000)]
        public IEnumerator CurrentBodiesKeepEquipmentAcrossStrokes()
        {
            yield return SceneManager.LoadSceneAsync("Tennis", LoadSceneMode.Single);
            var game = Object.FindFirstObjectByType<TennisGame>();
            while (!game || !game.Initialized) { yield return null; game = Object.FindFirstObjectByType<TennisGame>(); }
            game.ManualSimulation = true;
            game.enabled = false;
            foreach (bool female in new[] { false, true })
            {
                game.SelectCharacter(female);
                yield return null;
                var driver = game.Player.GetComponentInChildren<HeroTennisDriver>();
                Assert.IsNotNull(driver);
                Assert.IsNotNull(driver.matchLook, "The actual match body is required.");
                Assert.AreEqual(female, driver.matchLook.female);
                driver.enabled=false; // Keep each sampled diagnostic pose through the rendered frame.
                Assert.IsNotNull(game.Player.SweetSpot);
                foreach (var clip in new[] { HeroTennisDriver.Clip.Ready, HeroTennisDriver.Clip.Forehand,
                    HeroTennisDriver.Clip.Backhand, HeroTennisDriver.Clip.Serve, HeroTennisDriver.Clip.Volley,
                    HeroTennisDriver.Clip.Smash, HeroTennisDriver.Clip.Walk })
                {
                    Assert.Greater(driver.LengthOf(clip), 0, "Missing current clip: " + clip);
                    for (int frame = 0; frame <= 20; frame++)
                    {
                        driver.Sample(clip, driver.LengthOf(clip) * frame / 20f);
                        yield return null;
                        foreach (var mesh in driver.GetComponentsInChildren<SkinnedMeshRenderer>())
                        {
                            Assert.Less(mesh.bounds.size.magnitude, 8f, "Exploded " + mesh.name + " in " + clip);
                            Assert.IsFalse(float.IsNaN(mesh.bounds.center.x));
                        }
                        var p = game.Player.SweetSpot.position;
                        Assert.IsFalse(float.IsNaN(p.x) || float.IsInfinity(p.x));
                        Assert.Less((p - game.Player.transform.position).magnitude, 4f, "Equipment left the character");
                    }
                }
            }
        }
    }
}
