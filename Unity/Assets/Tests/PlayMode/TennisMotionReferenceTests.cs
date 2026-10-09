using System;
using System.Collections;
using System.IO;
using System.Linq;
using GolfArcade.Game;
using GolfArcade.Tennis;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GolfArcade.PlayTests
{
    public class TennisMotionReferenceTests
    {
        [UnityTest]
        public IEnumerator ForwardRunningMovesTheFreeArmAndKeepsTheGrip()
        {
            foreach (bool female in new[] { false, true })
            foreach (bool left in new[] { false, true })
            {
                var go = new GameObject("Motion regression");
                var actor = go.AddComponent<TennisActor>();
                actor.Build(female, new Color(.95f, .62f, .26f), left, TennisGame.PlayerBody(female));
                var bones = actor.GetComponentsInChildren<Transform>();
                var hip = bones.First(t => t.name == "Hips");
                var hand = bones.First(t => t.name == (left ? "Hand.R" : "Hand.L"));
                var shoulder = bones.First(t => t.name == (left ? "UpperArm.R" : "UpperArm.L"));
                var elbow = bones.First(t => t.name == (left ? "LowerArm.R" : "LowerArm.L"));
                Bounds motion = default;
                for (int frame = 0; frame < 120; frame++)
                {
                    go.transform.position += Vector3.forward * (5f / 60);
                    actor.Advance(1f / 60, 0, 5);
                    actor.Pose();
                    Assert.Less(actor.RacketGripError, .005f, $"Grip female={female} left={left}");
                    // Measure relative to the hip after acceleration settles. Translation
                    // across the court must not let a frozen upper body pass this check.
                    Vector3 relative = hand.position - hip.position;
                    if (frame == 40) motion = new Bounds(relative, Vector3.zero);
                    else if (frame > 40) motion.Encapsulate(relative);
                    if (frame > 40)
                    {
                        Assert.Less(elbow.position.y, shoulder.position.y - .04f, "Running elbow stays below the shoulder");
                        float bend = Vector3.Angle(shoulder.position - elbow.position, hand.position - elbow.position);
                        Assert.That(bend, Is.InRange(45f, 140f), "Running elbow maintains a natural bend");
                    }
                }
                Assert.Greater(motion.size.magnitude, .04f, $"Forward arm motion female={female} left={left}");
                Object.Destroy(go);
                yield return null;
            }
        }

        // A repeatable view of the actual runtime poses, independent of opponent decisions
        // and camera cuts. Pass -motionReviewOutput to keep before/after captures separate.
        [UnityTest, Explicit, Timeout(600000)]
        public IEnumerator CaptureReferenceMotions()
        {
            yield return SceneManager.LoadSceneAsync("Tennis", LoadSceneMode.Single);
            yield return null;
            var game = Object.FindFirstObjectByType<TennisGame>();
            game.ManualSimulation = true;
            game.enabled = false;
            game.Player.gameObject.SetActive(false);
            game.Opponent.gameObject.SetActive(false);
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) canvas.gameObject.SetActive(false);
            string[] args = Environment.GetCommandLineArgs();
            int arg = Array.IndexOf(args, "-motionReviewOutput");
            string output = arg >= 0 ? args[arg + 1] : "Library/MotionReference";
            int rateArg = Array.IndexOf(args, "-motionCaptureFps");
            int captureEvery = rateArg >= 0 ? Mathf.Max(1, 60 / int.Parse(args[rateArg + 1])) : 6;
            bool frontView = Array.IndexOf(args, "-motionFrontView") >= 0;
            bool dualView = Array.IndexOf(args, "-motionDualView") >= 0;
            bool bothBodies = Array.IndexOf(args, "-motionBothBodies") >= 0;
            Directory.CreateDirectory(output);
            var camera = Camera.main;
            camera.fieldOfView = 42;
            string[] motions = { "Ready", "RunRight", "RunLeft", "RunForward", "Forehand", "Backhand", "RunningForehand", "Serve", "Smash", "Celebrate" };
            foreach (bool female in bothBodies ? new[] { false, true } : new[] { false })
            foreach (string motion in motions)
            {
                string directory = female ? output + "/female" : output;
                Directory.CreateDirectory(directory);
                if (dualView) { Directory.CreateDirectory(directory + "/front"); Directory.CreateDirectory(directory + "/rear"); }
                var go = new GameObject("Reference motion " + motion);
                go.transform.position = new Vector3(0, 0, -10);
                var actor = go.AddComponent<TennisActor>();
                actor.Build(female, new Color(.95f, .62f, .26f), false, TennisGame.PlayerBody(female));
                actor.LookAt(new Vector3(0, 1, 5), .8f);
                for (int i = 0; i < 30; i++) actor.Tick(1f / 60, 0);
                bool run = motion.StartsWith("Run") && motion != "RunningForehand";
                if (!run && motion != "Ready" && motion != "Celebrate")
                {
                    actor.Prepare(1, motion == "Backhand", motion == "Serve");
                    for (int i = 0; i < 24; i++) actor.Tick(1f / 60, 0);
                    actor.Swing(.8f, motion == "Backhand", motion == "Serve" ? TennisActor.Stroke.Serve : motion == "Smash" ? TennisActor.Stroke.Smash : motion == "RunningForehand" ? TennisActor.Stroke.Running : TennisActor.Stroke.Drive);
                }
                if (motion == "Celebrate") actor.React(true, TennisActor.Moment.Match);
                int count = run ? 120 : motion == "Celebrate" ? 120 : 72;
                for (int frame = 0; frame < count; frame++)
                {
                    float velocity = run ? Mathf.Min(5, frame * .25f) : motion == "RunningForehand" ? 3 : 0;
                    float sideways = motion == "RunLeft" ? -velocity : motion == "RunForward" ? 0 : velocity;
                    float forward = motion == "RunForward" ? velocity : 0;
                    go.transform.position += new Vector3(sideways, 0, forward) / 60;
                    actor.Advance(1f / 60, sideways, forward);
                    actor.Pose();
                    yield return null;
                    if (frame % captureEvery != 0) continue;
                    Vector3 at = go.transform.position;
                    camera.transform.position = at + (frontView ? new Vector3(1.5f, 1.45f, 3.4f) : new Vector3(.35f, 1.6f, -3.8f));
                    camera.transform.LookAt(at + Vector3.up * 1.05f);
                    if (dualView)
                    {
                        camera.transform.position = at + new Vector3(1.5f, 1.45f, 3.4f);
                        camera.transform.LookAt(at + Vector3.up * 1.05f);
                        GameCapture.Save($"{directory}/front/{motion}-{frame:D3}.jpg", 480, 480);
                        camera.transform.position = at + new Vector3(.35f, 1.6f, -3.8f);
                        camera.transform.LookAt(at + Vector3.up * 1.05f);
                        GameCapture.Save($"{directory}/rear/{motion}-{frame:D3}.jpg", 480, 480);
                    }
                    else GameCapture.Save($"{directory}/{motion}-{frame:D3}.jpg", 480, 480);
                }
                Object.Destroy(go);
                yield return null;
            }
        }
    }
}
