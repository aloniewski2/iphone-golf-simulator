using System.Collections;
using System.IO;
using System.Reflection;
using GolfArcade.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GolfArcade.PlayTests
{
    // The active Generic Match Hero replaced the retired floating-hands V4 preview.
    public class StandardCharacterGameplayTests
    {
        [UnityTest, Timeout(240000)]
        public IEnumerator BothStandardsPlayPhoneDrivenSwingInGolfGame()
        {
            var original = GolferStyle.Current.Clone();
            int oldRate = Time.captureFramerate;
            float oldScale = Time.timeScale;
            var oldOverride = GolferStyle.HeroOverride;
            try
            {
                Time.timeScale = 1; Time.captureFramerate = 60; GolferStyle.HeroOverride = true;
                foreach (var body in new[] { GolferStyle.BodyKind.Male, GolferStyle.BodyKind.Female })
                {
                    GolferStyle.Body = body;
                    yield return SceneManager.LoadSceneAsync("Golf", LoadSceneMode.Single);
                    var game = Object.FindFirstObjectByType<GolfGame>();
                    Assert.IsNotNull(game);
                    // Scene startup presents the menu; use its public play action to enter a round.
                    yield return null;
                    game.Play();
                    game.Swing.Synthetic.FixedStepSeconds = 1.0 / 60.0;
                    Assert.AreSame(game.Swing.Synthetic, game.Swing.Source);
                    for (int i = 0; i < 900 && (game.Current != GolfGame.State.Aim || game.Swing.Phase != Swing.SwingPhase.Address); i++) yield return null;
                    Assert.AreEqual(GolfGame.State.Aim, game.Current);
                    var golfer = (GolferView)typeof(GolfGame).GetField("golfer", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game);
                    Assert.IsTrue(golfer.IsHero, "The current approved Match Hero must be used.");
                    Assert.IsNotNull(golfer.Hero.Root);
                    Assert.AreEqual(body == GolferStyle.BodyKind.Female, golfer.Hero.Root.GetComponent<GolfArcade.Tennis.MatchHeroLook>().female);
                    Assert.IsTrue(golfer.Hero.Bones.ContainsKey("RightHand"));
                    Assert.IsTrue(golfer.Hero.Bones.ContainsKey("LeftHand"));
                    Assert.IsNotNull(golfer.ClubHeadWorld(), "The selected club has a skinned contact surface.");
                    bool sawFlight = false;
                    for (int frame = 0; frame < 360; frame++)
                    {
                        if (frame == 30) game.Swing.Synthetic.Backswing(true);
                        if (frame == 108) game.Swing.Synthetic.Backswing(false);
                        yield return null;
                        sawFlight |= game.Current == GolfGame.State.Flight;
                        foreach (var mesh in golfer.GetComponentsInChildren<SkinnedMeshRenderer>())
                        {
                            Assert.Less(mesh.bounds.size.magnitude, 8f, "Exploded skin bounds: " + mesh.name);
                            Assert.IsFalse(float.IsNaN(mesh.bounds.center.x), "Invalid skin transform");
                        }
                        if (frame % 60 == 0)
                        {
                            string directory = $"Library/Captures/current-hero-{body.ToString().ToLowerInvariant()}";
                            Directory.CreateDirectory(directory);
                            Assert.IsNotNull(GameCapture.Save($"{directory}/frame-{frame:D4}.png", 720, 480));
                        }
                    }
                    Assert.IsTrue(sawFlight, "Synthetic phone input must launch a real shot.");
                }
            }
            finally
            {
                GolferStyle.SaveDevice(original); GolferStyle.Unwear(); GolferStyle.HeroOverride = oldOverride;
                Time.captureFramerate = oldRate; Time.timeScale = oldScale;
            }
        }
    }
}
