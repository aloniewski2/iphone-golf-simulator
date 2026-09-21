using System.Collections;
using System.IO;
using GolfArcade.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GolfArcade.PlayTests
{
    public class StandardCharacterGameplayTests
    {
        [UnityTest]
        public IEnumerator BothStandardsPlayPhoneDrivenSwingInGolfGame()
        {
            var originalBody = GolferStyle.Body;
            int originalCaptureRate = Time.captureFramerate;
            float originalScale = Time.timeScale;
            try
            {
                Time.timeScale = 1; Time.captureFramerate = 60;
                foreach (var body in new[] { GolferStyle.BodyKind.Male, GolferStyle.BodyKind.Female })
                {
                    GolferStyle.Body = body;
                    yield return SceneManager.LoadSceneAsync("Golf", LoadSceneMode.Single);
                    var game = Object.FindFirstObjectByType<GolfGame>();
                    Assert.IsNotNull(game);
                    game.Swing.Synthetic.FixedStepSeconds = 1.0 / 60.0;
                    Assert.AreSame(game.Swing.Synthetic, game.Swing.Source);
                    for (int i = 0; i < 900 && (game.Current != GolfGame.State.Aim || game.Swing.Phase != Swing.SwingPhase.Address); i++) yield return null;
                    Assert.AreEqual(GolfGame.State.Aim, game.Current);
                    Assert.AreEqual(Swing.SwingPhase.Address, game.Swing.Phase);
                    var golfer = Object.FindFirstObjectByType<GolferView>();
                    Assert.IsTrue(golfer.UsesStandardCharacter, "Must use the actual V4 model, not a fallback.");
                    var arms = golfer.GetComponentInChildren<StandardCharacterArms>();
                    Assert.IsNotNull(arms);
                    Assert.IsTrue(arms.IsReady);
                    var grip = golfer.GetComponentInChildren<StandardGolfGrip>();
                    Assert.IsNotNull(grip);
                    Assert.IsTrue(grip.IsReady);
                    var contact = System.Array.Find(golfer.GetComponentsInChildren<Transform>(true), t => t.name == "StandardClubContact");
                    Assert.IsNotNull(contact);
                    var meshes = golfer.GetComponentsInChildren<SkinnedMeshRenderer>();
                    Assert.Greater(meshes.Length, 0);
                    foreach (var mesh in meshes) Assert.Less(mesh.bounds.size.magnitude, 8f, "Exploded skin bounds");
                    string directory = $"Library/Captures/standard-{body.ToString().ToLowerInvariant()}";
                    Directory.CreateDirectory(directory);
                    bool sawFlight = false;
                    for (int frame = 0; frame < 360; frame++)
                    {
                        if (frame == 30) game.Swing.Synthetic.Backswing(true);
                        if (frame == 108) game.Swing.Synthetic.Backswing(false);
                        yield return null;
                        sawFlight |= game.Current == GolfGame.State.Flight;
                        Assert.Less(arms.MaximumGripError, .0001f, "Arm correction moved the authored hand target");
                        Assert.Less(arms.MaximumSegmentError, .005f, "Corrected bone lengths changed");
                        Assert.Less(grip.MaximumHandleError, .0001f, "Hands slipped relative to the shared handle");
                        if (frame % 60 == 0)
                            foreach (var mesh in meshes) Assert.Less(mesh.bounds.size.magnitude, 8f);
                        Assert.IsNotNull(GameCapture.Save($"{directory}/frame-{frame:D4}.png", 720, 480));
                        if (frame == 0 || frame == 100 || frame == 140 || frame == 170)
                        {
                            var camera = Camera.main;
                            Vector3 position = camera.transform.position;
                            Quaternion rotation = camera.transform.rotation;
                            try
                            {
                                Vector3 target = arms.transform.TransformPoint(new Vector3(0, 1.1f, 0));
                                Vector3 side = Quaternion.AngleAxis(110, Vector3.up) * Vector3.ProjectOnPlane(position - target, Vector3.up).normalized;
                                camera.transform.position = target + side * 2.3f + Vector3.up * .35f;
                                camera.transform.LookAt(target);
                                GameCapture.Save($"Library/Captures/grip-details/{body}-{frame:D4}.png", 960, 640);
                            }
                            finally { camera.transform.SetPositionAndRotation(position, rotation); }
                        }
                    }
                    Assert.IsTrue(sawFlight, "The real game's synthetic phone input must launch a shot.");
                }
            }
            finally
            {
                GolferStyle.Body = originalBody;
                Time.captureFramerate = originalCaptureRate;
                Time.timeScale = originalScale;
            }
        }
    }
}
