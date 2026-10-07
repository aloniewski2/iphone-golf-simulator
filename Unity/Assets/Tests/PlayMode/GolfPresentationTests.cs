using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GolfArcade.Course;
using GolfArcade.Game;
using GolfArcade.Tennis;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GolfArcade.PlayTests
{
    public class GolfPresentationTests
    {
        static string Proof => Environment.GetEnvironmentVariable("GOLF_PRESENTATION_PROOF") ?? "Library/Captures/golf-presentation";

        static Color32[] Capture(Camera camera, string name, int width = 960, int height = 540)
        {
            var rt = RenderTexture.GetTemporary(width, height, 24);
            var previous = camera.targetTexture;
            var active = RenderTexture.active;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = rt;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = rt;
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply();
                if (name != null)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(Proof, name + ".png")));
                    File.WriteAllBytes(Path.Combine(Proof, name + ".png"), image.EncodeToPNG());
                }
                return image.GetPixels32();
            }
            finally
            {
                camera.targetTexture = previous;
                RenderTexture.active = active;
                RenderTexture.ReleaseTemporary(rt);
                Object.Destroy(image);
            }
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator EveryCourseAndBothGolfersRenderInTheNativePipeline()
        {
            var originalLook = GolferStyle.Current.Clone();
            var failures = new List<string>();
            TennisQuality.Apply();
            yield return SceneManager.LoadSceneAsync("Golf", LoadSceneMode.Single);
            var game = Object.FindFirstObjectByType<GolfGame>();
            game.InstantReplays = false;
            yield return null;
            try
            {
                foreach (var course in Course.Course.All())
                {
                    game.PrepareNativeAddress(course.Key);
                    foreach (var hole in course.Holes)
                    {
                        game.JumpToHole(hole.Number);
                        game.DropBall(hole.Tee);
                        Assert.That(game.Current, Is.EqualTo(GolfGame.State.Aim), hole.Name);
                        var view = HoleView.Current;
                        Assert.That(view.ModelRoot, Is.Not.Null, hole.Name + " model missing");
                        Assert.That(view.GetComponentsInChildren<MeshCollider>().Length, Is.GreaterThan(0), hole.Name + " ground missing");
                        var camera = game.GameplayCamera;
                        camera.aspect = 16f / 9f;
                        var golfer = game.GetComponentInChildren<GolferView>(true);
                        foreach (bool female in new[] { false, true })
                        {
                            GolferStyle.Body = female ? GolferStyle.BodyKind.Female : GolferStyle.BodyKind.Male;
                            golfer.ApplyStyle();
                            game.DropBall(hole.Tee);
                            yield return null;
                            yield return null;
                            var tag = $"hole-{hole.Number:00}-{(female ? "female" : "male")}";
                            var visible = golfer.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToArray();
                            var before = Capture(camera, tag);
                            foreach (var renderer in visible) renderer.enabled = false;
                            var without = Capture(camera, null);
                            foreach (var renderer in visible) renderer.enabled = true;
                            int characterPixels = 0;
                            int errorPixels = 0;
                            for (int i = 0; i < before.Length; i++)
                            {
                                if (Math.Abs(before[i].r - without[i].r) + Math.Abs(before[i].g - without[i].g) + Math.Abs(before[i].b - without[i].b) > 24) characterPixels++;
                                if (before[i].r > 220 && before[i].b > 220 && before[i].g < 35) errorPixels++;
                            }
                            var incompatible = view.GetComponentsInChildren<Renderer>().Concat(visible)
                                .Where(r => r.enabled).SelectMany(r => r.sharedMaterials).Where(m => m && m.FindPass("ForwardLit") < 0 &&
                                    m.FindPass("Unlit") < 0 && m.shader.name != "GolfArcade/SkyGradient" &&
                                    m.GetTag("RenderPipeline", false, "") != "UniversalPipeline")
                                .Select(m => m.shader.name).Distinct().ToArray();
                            // In URP a surface shader can exist and be "supported" yet draw no forward pass.
                            // Verify pixels as well as compatible material passes, not just scene object presence.
                            var critical = incompatible.Where(s => s == "GolfArcade/Turf" || s == "GolfArcade/HeroKit" || s == "Standard").ToArray();
                            Debug.Log($"GOLF_RENDER {tag}: character pixels={characterPixels}, legacy shaders={string.Join(",", incompatible)}");
                            if (characterPixels < 200) failures.Add(tag + $": golfer invisible ({characterPixels} pixels)");
                            if (errorPixels > 100) failures.Add(tag + $": shader error pixels={errorPixels}");
                            if (critical.Length > 0) failures.Add(tag + ": unsupported course/character material " + string.Join(",", critical));
                        }
                    }
                }
            }
            finally { GolferStyle.SaveDevice(originalLook); }
            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator LiveShotsHoldThenFollowWithoutReversingOrRolling()
        {
            Time.timeScale = 1;
            int oldCaptureRate = Time.captureFramerate;
            Time.captureFramerate = 60;
            TennisQuality.Apply();
            yield return SceneManager.LoadSceneAsync("Golf", LoadSceneMode.Single);
            var game = Object.FindFirstObjectByType<GolfGame>();
            game.InstantReplays = false;
            yield return null;
            try
            {
                game.PrepareNativeAddress();
                foreach (int number in new[] { 7, 8, 10, 16 })
                {
                    game.JumpToHole(number);
                    game.DropBall(game.CurrentHole.Tee);
                    var cam = game.GameplayCamera;
                    cam.aspect = 16f / 9f;
                    yield return null;
                    yield return null;
                    Capture(cam, $"shot-{number}-address");
                    Vector3 launch = cam.transform.position;
                    Quaternion previous = cam.transform.rotation;
                    game.NativeSwing(.78f);
                    Vector3 direction = HoleView.ToWorld(game.LastShot.Landing) - HoleView.ToWorld(game.LastShot.Origin);
                    direction.y = 0; direction.Normalize();
                    float maxTurn = 0, launchTravel = 0, followTravel = 0;
                    int frame = 0, ballOutside = 0, flightFrames = 0;
                    while (game.Current == GolfGame.State.Flight && frame < 1800)
                    {
                        yield return null;
                        frame++;
                        if (game.Current != GolfGame.State.Flight) break; // intentional result cut, after rest
                        if (game.FlightTime <= 0) { previous = cam.transform.rotation; continue; }
                        maxTurn = Mathf.Max(maxTurn, Quaternion.Angle(previous, cam.transform.rotation));
                        previous = cam.transform.rotation;
                        Assert.That(Vector3.Dot(cam.transform.forward, direction), Is.GreaterThan(0), $"hole {number} camera reversed at {game.FlightTime:F2}s");
                        Assert.That(Vector3.Dot(cam.transform.right, Vector3.up), Is.EqualTo(0).Within(.015f), $"hole {number} camera rolled");
                        if (game.FlightTime < 1.45) launchTravel = Mathf.Max(launchTravel, Vector3.Distance(launch, cam.transform.position));
                        if (game.FlightTime > 3) followTravel = Mathf.Max(followTravel, Vector3.Distance(launch, cam.transform.position));
                        if (game.FlightTime > CameraRig.LaunchHoldSeconds + .4f && game.FlightTime < game.LastShot.CarryTime)
                        {
                            var screen = cam.WorldToViewportPoint(game.BallPosition);
                            flightFrames++;
                            if (screen.z <= 0 || screen.x < .03f || screen.x > .97f || screen.y < .03f || screen.y > .97f) ballOutside++;
                        }
                        if (frame % 30 == 0) Capture(cam, $"shot-{number}-{frame:0000}");
                    }
                    Assert.That(frame, Is.LessThan(1800), "shot failed to settle");
                    Assert.That(launchTravel, Is.LessThan(.25f), "launch view must hold for 1.5 seconds");
                    Assert.That(followTravel, Is.GreaterThan(5f), "camera must travel after launch hold");
                    Assert.That(maxTurn, Is.LessThan(12f), "camera must not cut during live flight");
                    Assert.That(ballOutside, Is.LessThanOrEqualTo(Mathf.Max(3, flightFrames / 20)), "airborne ball must remain in frame");
                    Debug.Log($"GOLF_CAMERA hole={number} maxTurn={maxTurn:F3} holdTravel={launchTravel:F3} followTravel={followTravel:F1} ballOutside={ballOutside}/{flightFrames}");
                }
            }
            finally { Time.captureFramerate = oldCaptureRate; Time.timeScale = 1; }
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator NativeSceneLoadsEveryCourseAndRendersBeforeReady()
        {
            var host = new GameObject("Golf native launch regression");
            Object.DontDestroyOnLoad(host);
            var bridge = host.AddComponent<NativeSportsSession>();
            var originalLook = GolferStyle.Current.Clone();
            try
            {
                foreach (var course in Course.Course.All())
                {
                    string session = "golf-render-" + course.Key;
                    bridge.Receive(JsonUtility.ToJson(new NativeSportsSession.Message {
                        version = 1, session = session, action = "start", sport = "golf", course = course.Key, touch = true
                    }));
                    float deadline = Time.realtimeSinceStartup + 40;
                    while (!bridge.Ready && Time.realtimeSinceStartup < deadline)
                    {
                        // Batch mode has no Game view repaint. Render the real gameplay camera
                        // so its normal URP completion callback can release the native ready gate.
                        if (Application.isBatchMode && bridge.GameplayCamera) Capture(bridge.GameplayCamera, null);
                        yield return null;
                    }
                    Assert.That(bridge.Ready, Is.True, course.Name + " never became ready");
                    var game = Object.FindFirstObjectByType<GolfGame>();
                    Assert.That(game.CurrentHole.Number, Is.EqualTo(course.Holes[0].Number));
                    Assert.That(game.Current, Is.EqualTo(GolfGame.State.Aim));
                    Assert.That(game.GetComponentInChildren<GolferView>(), Is.Not.Null, "visible golfer at native ready");
                    Assert.That(Time.timeScale, Is.Zero, "ready waits for the phone's resume command");
                    Capture(bridge.GameplayCamera, "native-" + course.Key);
                    bridge.Receive(JsonUtility.ToJson(new NativeSportsSession.Message { version = 1, session = session, action = "resume" }));
                    bridge.Receive(JsonUtility.ToJson(new NativeSportsSession.Message { version = 1, session = session, action = "golfLoad", value = .5f }));
                    var hud = Object.FindFirstObjectByType<GolfArcade.UI.Hud>();
                    Assert.That(hud.Map.ShowLoad, Is.True, "touch slider must update the TV landing estimate");
                    var expectedLoad = hud.Map.Load;
                    bridge.Receive(JsonUtility.ToJson(new NativeSportsSession.Message { version = 1, session = session, action = "pause" }));
                    bridge.Receive(JsonUtility.ToJson(new NativeSportsSession.Message { version = 1, session = session, action = "golfLoad", value = .9f }));
                    Assert.That(hud.Map.Load, Is.EqualTo(expectedLoad), "paused touch input must not move the marker");

                    bridge.Receive(JsonUtility.ToJson(new NativeSportsSession.Message { version = 1, session = session, action = "end" }));
                }
            }
            finally { Object.Destroy(host); Time.timeScale = 1; GolferStyle.SaveDevice(originalLook); }
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator PuttingKeepsTheSameSideOfTheBallThroughTheCup()
        {
            Time.timeScale = 1;
            int oldCaptureRate = Time.captureFramerate;
            Time.captureFramerate = 60;
            TennisQuality.Apply();
            yield return SceneManager.LoadSceneAsync("Golf", LoadSceneMode.Single);
            var game = Object.FindFirstObjectByType<GolfGame>();
            game.InstantReplays = false;
            yield return null;
            try
            {
                game.PrepareNativeAddress();
                var hole = game.CurrentHole;
                game.DropBall(new CoursePoint(hole.Pin.X, hole.Pin.D - 3));
                yield return null;
                var cam = game.GameplayCamera;
                cam.aspect = 16f / 9f;
                Capture(cam, "putt-address");
                var origin = game.BallPosition;
                var direction = HoleView.ToWorld(hole.Pin) - origin; direction.y = 0; direction.Normalize();
                game.StrikeHolingPutt();
                var previous = cam.transform.rotation;
                float maxTurn = 0;
                for (int frame = 0; game.Current == GolfGame.State.Flight && frame < 1200; frame++)
                {
                    yield return null;
                    if (game.Current != GolfGame.State.Flight) break;
                    maxTurn = Mathf.Max(maxTurn, Quaternion.Angle(previous, cam.transform.rotation));
                    previous = cam.transform.rotation;
                    Assert.That(Vector3.Dot(cam.transform.forward, direction), Is.GreaterThan(0), "putting camera cut behind the cup");
                    if (frame % 30 == 0) Capture(cam, $"putt-{frame:0000}");
                }
                Assert.That(game.Current, Is.Not.EqualTo(GolfGame.State.Flight));
                Assert.That(maxTurn, Is.LessThan(12f));
                Capture(cam, "putt-result");
                Debug.Log($"GOLF_PUTT maxTurn={maxTurn:F3} holed={game.LastShot.IsHoled}");
            }
            finally { Time.captureFramerate = oldCaptureRate; Time.timeScale = 1; }
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator SwingToResultShowsStatsAndLetsBothGolfersEmoteUntilContinue()
        {
            var originalLook = GolferStyle.Current.Clone();
            int oldRate = Time.captureFramerate;
            Time.captureFramerate = 30; Time.timeScale = 1;
            TennisQuality.Apply();
            yield return SceneManager.LoadSceneAsync("Golf", LoadSceneMode.Single);
            var game = Object.FindFirstObjectByType<GolfGame>();
            game.InstantReplays = false;
            yield return null;
            int video = 0;
            try
            {
                game.PrepareNativeAddress();
                foreach (bool female in new[] { false, true })
                {
                    GolferStyle.Body = female ? GolferStyle.BodyKind.Female : GolferStyle.BodyKind.Male;
                    var golfer = game.GetComponentInChildren<GolferView>(true);
                    golfer.ApplyStyle();
                    game.JumpToHole(7); game.DropBall(game.CurrentHole.Tee);
                    var cam = game.GameplayCamera; cam.aspect = 16f / 9f;
                    yield return null; yield return null;
                    Assert.That(game.PlayResultEmote(0), Is.False, "emote during address");
                    for (int frame = 0; frame < 36; frame++)
                    {
                        golfer.ShowLoad(Mathf.Clamp01(frame / 30f) * .78f);
                        yield return null;
                        if (!female) Capture(cam, $"sequence/frame-{video++:0000}");
                        if (frame == 30) Capture(cam, female ? "female-backswing" : "backswing");
                    }
                    var addressPosition = cam.transform.position;
                    var addressRotation = cam.transform.rotation;
                    UnityEngine.Random.InitState(902);
                    game.NativeSwing(.78f);
                    int flightFrames = 0;
                    while (game.Current == GolfGame.State.Flight && flightFrames++ < 1200)
                    {
                        yield return null;
                        if (!female) Capture(cam, $"sequence/frame-{video++:0000}");
                        if (!female && flightFrames == 5) Capture(cam, "swing");
                        if (!female && flightFrames == 25) Capture(cam, "launch");
                        if (!female && flightFrames == 105) Capture(cam, "follow");
                    }
                    Assert.That(game.ShowingShotResult, Is.True);
                    Assert.That(game.ShotStatistics, Does.Contain($"Total {game.LastShot.Total:F0} yd"));
                    Assert.That(game.ShotStatistics, Does.Contain($"Carry {game.LastShot.Carry:F0} yd"));
                    Assert.That(Vector3.Distance(cam.transform.position, addressPosition), Is.LessThan(.01f));
                    Assert.That(Quaternion.Angle(cam.transform.rotation, addressRotation), Is.LessThan(.1f));
                    var screen = cam.WorldToViewportPoint(golfer.transform.position + Vector3.up);
                    Assert.That(screen.z, Is.GreaterThan(0));
                    Assert.That(screen.x, Is.InRange(.15f, .48f), "golfer must occupy the left of the result card");
                    Assert.That(screen.y, Is.InRange(.15f, .8f));
                    Capture(cam, female ? "female-result" : "result");
                    var oldShot = game.LastShot;
                    int strokes = game.Card.Total;
                    Time.timeScale = 0;
                    Assert.That(game.PlayResultEmote(0), Is.False, "emotes must respect pause");
                    game.NativeContinue();
                    Assert.That(game.ShowingShotResult, Is.True, "continue must respect pause");
                    Time.timeScale = 1;
                    Assert.That(game.PlayResultEmote(-1), Is.False);
                    Assert.That(game.PlayResultEmote(3), Is.False);
                    string[] moves = { "Intro_Wave", "Emote_Scuba", "Emote_Spike" };
                    for (int choice = 0; choice < 3; choice++)
                    {
                        Assert.That(game.PlayResultEmote(choice), Is.True, moves[choice] + " clip missing");
                        Assert.That(game.ResultEmote, Is.EqualTo(moves[choice]));
                        for (int frame = 0, frames = Mathf.CeilToInt(golfer.PerformanceDuration * 30) + 15; frame < frames; frame++)
                        {
                            yield return null;
                            Assert.That(Vector3.Distance(cam.transform.position, addressPosition), Is.LessThan(.01f), "emote camera moved away from the swing view");
                            Assert.That(Quaternion.Angle(cam.transform.rotation, addressRotation), Is.LessThan(.1f), "emote camera changed the swing angle");
                            if (!female) Capture(cam, $"sequence/frame-{video++:0000}");
                            if (frame == 25) Capture(cam, (female ? "female-" : "") + moves[choice].ToLowerInvariant());
                        }
                    }
                    if (female)
                    {
                        cam.aspect = 9f / 16f;
                        yield return null; yield return null;
                        Capture(cam, "portrait-result", 540, 960);
                        var portrait = cam.WorldToViewportPoint(golfer.transform.position + Vector3.up);
                        Assert.That(portrait.x, Is.InRange(.1f, .9f));
                        Assert.That(portrait.y, Is.InRange(.1f, .9f));
                        cam.aspect = 16f / 9f;
                    }
                    Assert.That(game.ResultEmote, Is.EqualTo("Idle"), "emotes must finish once and return to idle");
                    // The other three locker choices use the same native loadout ids too.
                    game.EquipResultEmotes(new[] { "thrust", "bringIt", "pushups" });
                    string[] otherMoves = { "Emote_Thrust", "Intro_BringIt", "Intro_Pushups" };
                    for (int choice = 0; choice < 3; choice++)
                    {
                        Assert.That(game.PlayResultEmote(choice), Is.True);
                        Assert.That(game.ResultEmote, Is.EqualTo(otherMoves[choice]));
                        for (int frame = 0, frames = Mathf.CeilToInt(golfer.PerformanceDuration * 30) + 15; frame < frames; frame++) yield return null;
                        Assert.That(game.ResultEmote, Is.EqualTo("Idle"));
                    }
                    game.EquipResultEmotes(null);
                    game.StallForTests(30);
                    yield return null;
                    Assert.That(game.ShowingShotResult, Is.True, "results must wait for the player even after the old watchdog deadline");
                    Assert.That(game.Card.Total, Is.EqualTo(strokes), "emotes must not change scoring");
                    Assert.That(game.LastShot, Is.SameAs(oldShot));
                    game.NativeContinue();
                    Assert.That(game.Current, Is.EqualTo(GolfGame.State.Aim));
                    Assert.That(game.ShowingShotResult, Is.False);
                    Assert.That(golfer.Performing, Is.Null, "emote must return to golf stance");
                }
                // A holed putt still gets the character screen before the scorecard.
                game.DropBall(new CoursePoint(game.CurrentHole.Pin.X, game.CurrentHole.Pin.D - 3));
                game.StrikeHolingPutt();
                for (int frame = 0; game.Current == GolfGame.State.Flight && frame < 1200; frame++) yield return null;
                Assert.That(game.LastShot.IsHoled, Is.True);
                Assert.That(game.ShowingShotResult, Is.True);
                Assert.That(game.ShotResultTitle, Is.EqualTo("IN THE HOLE!"));
                Assert.That(game.ShotStatistics, Does.Contain("HOLED"));
                Assert.That(game.ResultEmote, Is.EqualTo("Idle"));
                Capture(game.GameplayCamera, "holed-result");
                game.NativeContinue();
                Assert.That(game.Current, Is.EqualTo(GolfGame.State.HoleDone));
                for (int frame = 0; game.Current == GolfGame.State.HoleDone && frame < 180; frame++) yield return null;
                Assert.That(game.Current, Is.EqualTo(GolfGame.State.RoundDone));
                game.NativeContinue();
                Assert.That(game.Current, Is.EqualTo(GolfGame.State.Aim));
                game.JumpToHole(8); game.DropBall(game.CurrentHole.Tee);
                game.StrikeToward(new CoursePoint(10, 90));
                for (int frame = 0; game.Current == GolfGame.State.Flight && frame < 1200; frame++) yield return null;
                Assert.That(game.LastShot.PenaltyStrokes, Is.GreaterThan(0));
                Assert.That(game.ShowingShotResult, Is.True);
                Assert.That(game.ShotResultTitle, Is.EqualTo("PENALTY +1"));
                Assert.That(game.PlayResultEmote(0), Is.True);
                Capture(game.GameplayCamera, "penalty-result");
                game.NativeContinue();
                Assert.That(game.Current, Is.EqualTo(GolfGame.State.Aim));
                Assert.That(game.BallPosition.x, Is.EqualTo(HoleView.ToWorld(game.LastShot.NextPosition).x).Within(.2f));
            }
            finally { Time.captureFramerate = oldRate; Time.timeScale = 1; GolferStyle.SaveDevice(originalLook); }
        }
    }
}
