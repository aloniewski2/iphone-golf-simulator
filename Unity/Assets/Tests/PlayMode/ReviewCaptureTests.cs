using System.Collections;
using System.Linq;
using GolfArcade.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GolfArcade.PlayTests
{
    /// Plays a stroke at real speed and saves phone-resolution frames to Library/Captures/review
    /// (address, mid-flight, result) for a human — or Claude — to look at. Runs with the other
    /// PlayMode tests; the pictures are the point, the asserts only keep it honest.
    public class ReviewCaptureTests
    {
        const string Dir = "Library/Captures/review";

        static IEnumerator WaitFor(System.Func<bool> done, float seconds, string what)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (!done())
            {
                if (Time.realtimeSinceStartup > until) Assert.Fail($"timed out waiting for {what}");
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator CapturesAddressFlightAndResult()
        {
            Time.timeScale = 1f;
            yield return SceneManager.LoadSceneAsync("Golf", LoadSceneMode.Single);
            var game = Object.FindFirstObjectByType<GolfGame>();
            Assert.IsNotNull(game?.Swing?.Synthetic);
            yield return new WaitForSecondsRealtime(1.0f);
            Assert.IsNotNull(GameCapture.Save($"{Dir}/0-menu.png"));
            // The golfer picker: a bob in auburn on the female, then back the way it was.
            var body0 = GolferStyle.Body; int skin0 = GolferStyle.SkinTone; var hair0 = GolferStyle.Hair; int hairTone0 = GolferStyle.HairTone;
            game.OpenGolferPicker();
            GolferStyle.Body = GolferStyle.BodyKind.Female; GolferStyle.SkinTone = 3; GolferStyle.Hair = GolferStyle.HairKind.Long; GolferStyle.HairTone = 3;
            game.RestyleGolfer();
            yield return new WaitForSecondsRealtime(1.2f);
            Assert.IsNotNull(GameCapture.Save($"{Dir}/0b-golfer-picker.png"));
            GolferStyle.Hair = GolferStyle.HairKind.Curly; GolferStyle.HairTone = 4; game.RestyleGolfer();
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.IsNotNull(GameCapture.Save($"{Dir}/0c-golfer-curls.png"));
            GolferStyle.Body = body0; GolferStyle.SkinTone = skin0; GolferStyle.Hair = hair0; GolferStyle.HairTone = hairTone0;
            game.CloseGolferPicker();
            yield return null;
            game.Play();

            yield return new WaitForSecondsRealtime(1.8f);
            Assert.IsNotNull(GameCapture.Save($"{Dir}/1-flyover.png"));
            yield return new WaitForSecondsRealtime(2.0f);
            Assert.IsNotNull(GameCapture.Save($"{Dir}/1b-aerial.png"));
            yield return new WaitForSecondsRealtime(3.5f);
            Assert.IsNotNull(GameCapture.Save($"{Dir}/1c-walkthrough.png"));
            yield return WaitFor(() => game.Current == GolfGame.State.Aim, 14, "the showcase to end");
            yield return WaitFor(() => game.Swing.Phase == Swing.SwingPhase.Address, 5, "address");
            yield return null;
            Assert.IsNotNull(GameCapture.Save($"{Dir}/2-address.png"));

            game.Swing.Synthetic.Backswing(true);
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.IsNotNull(GameCapture.Save($"{Dir}/3-backswing.png"));
            yield return new WaitForSecondsRealtime(0.3f);
            game.Swing.Synthetic.Backswing(false);
            yield return WaitFor(() => game.Current == GolfGame.State.Flight, 5, "impact");
            yield return new WaitForSecondsRealtime(1.2f);
            Assert.IsNotNull(GameCapture.Save($"{Dir}/4-flight.png"));
            yield return new WaitForSecondsRealtime(1.5f);
            Assert.IsNotNull(GameCapture.Save($"{Dir}/5-flight-late.png"));
            yield return WaitFor(() => game.Current == GolfGame.State.Result, 20, "the ball to stop");
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.IsNotNull(GameCapture.Save($"{Dir}/6-result.png"));
            yield return WaitFor(() => game.Current == GolfGame.State.Aim, 10, "the next stroke");
            yield return null;
            Assert.IsNotNull(GameCapture.Save($"{Dir}/7-second-shot.png"));

            // The other golfer, a different skin, wound up to the top: the style swap keeps working mid-round.
            var body = GolferStyle.Body; int skin = GolferStyle.SkinTone;
            try
            {
                GolferStyle.Body = body == GolferStyle.BodyKind.Male ? GolferStyle.BodyKind.Female : GolferStyle.BodyKind.Male;
                GolferStyle.SkinTone = (skin + 3) % GolferStyle.SkinTones.Length;
                game.RestyleGolfer();
                yield return WaitFor(() => game.Swing.Phase == Swing.SwingPhase.Address, 5, "address again");
                game.Swing.Synthetic.Backswing(true);
                yield return new WaitForSecondsRealtime(0.8f);
                Assert.IsNotNull(GameCapture.Save($"{Dir}/8-other-golfer-top.png"));
                game.Swing.Synthetic.Backswing(false);
                yield return new WaitForSecondsRealtime(0.5f);
                Assert.IsNotNull(GameCapture.Save($"{Dir}/9-other-golfer-through.png"));
            }
            finally
            {
                GolferStyle.Body = body; GolferStyle.SkinTone = skin;
            }
        }

        /// The island carry: flyover, the tee shot over the water, and where it comes down.
        [UnityTest]
        public IEnumerator CapturesHoleTwelve()
        {
            Time.timeScale = 1f;
            yield return SceneManager.LoadSceneAsync("Golf", LoadSceneMode.Single);
            var game = Object.FindFirstObjectByType<GolfGame>();
            Assert.IsNotNull(game?.Swing?.Synthetic);
            yield return null;
            game.Play();
            yield return null;
            game.JumpToHole(12);
            yield return new WaitForSecondsRealtime(1.5f);
            Assert.IsNotNull(GameCapture.Save($"{Dir}/12-1-flyover.png"));
            // The sea moves: the model's wave grid imported with its four morph targets and is being driven.
            var water = Object.FindFirstObjectByType<GolfArcade.Course.WaterMotion>();
            Assert.IsNotNull(water, "hole 12 should have its animated water (WATER_WAVES with blendshapes)");
            var waveMesh = water.GetComponent<SkinnedMeshRenderer>();
            Assert.AreEqual(4, waveMesh.sharedMesh.blendShapeCount);
            Assert.Greater(Enumerable.Range(0, 4).Sum(k => waveMesh.GetBlendShapeWeight(k)), 50f, "wave weights should be crossfading");
            // After the aerial, the hole's signature shot from Blender: ball in the air over the water.
            yield return WaitFor(() => game.SignaturePlaying, 6, "the signature shot to start");
            yield return new WaitForSecondsRealtime(1.6f);
            Assert.IsTrue(game.SignaturePlaying);
            var ballAt = game.BallPosition;
            float above = ballAt.y - (float)GolfArcade.Course.HoleView.GroundHeight(GolfArcade.Course.HoleView.ToCourse(ballAt));
            Assert.Greater(above, 5f, "the ball should be well in the air mid-carry");
            Assert.IsTrue(ballAt.z > 40 && ballAt.z < 200, $"the ball should be out over the water, at {ballAt}");
            Assert.IsNotNull(GameCapture.Save($"{Dir}/12-1b-signature-shot.png"));
            yield return WaitFor(() => game.Current == GolfGame.State.Aim, 14, "the showcase to end");
            yield return WaitFor(() => game.Swing.Phase == Swing.SwingPhase.Address, 5, "address");
            yield return null;
            Assert.IsNotNull(GameCapture.Save($"{Dir}/12-2-address.png"));
            // The phone as the controller, as it looks with the course on the big screen.
            game.PreviewBigScreen(true);
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.IsNotNull(GameCapture.Save($"{Dir}/12-2b-controller.png"));
            game.Swing.Synthetic.Backswing(true);
            yield return new WaitForSecondsRealtime(0.35f);
            Assert.IsNotNull(GameCapture.Save($"{Dir}/12-2c-controller-load.png"));
            game.Swing.Synthetic.Backswing(false);
            yield return WaitFor(() => game.Current == GolfGame.State.Result, 25, "that shot to finish");
            yield return WaitFor(() => game.Current == GolfGame.State.Aim, 10, "the next stroke");
            game.PreviewBigScreen(false);
            yield return WaitFor(() => game.Swing.Phase == Swing.SwingPhase.Address, 5, "address again");
            game.Swing.Synthetic.Backswing(true);
            yield return new WaitForSecondsRealtime(0.7f);
            game.Swing.Synthetic.Backswing(false);
            yield return WaitFor(() => game.Current == GolfGame.State.Flight, 5, "impact");
            yield return new WaitForSecondsRealtime(1.5f);
            Assert.IsNotNull(GameCapture.Save($"{Dir}/12-3-flight.png"));
            yield return WaitFor(() => game.Bounces > 0 || game.Current != GolfGame.State.Flight, 20, "the ball to come down");
            yield return null;
            Assert.IsNotNull(GameCapture.Save($"{Dir}/12-3b-landing.png"));
            yield return WaitFor(() => game.Current == GolfGame.State.Result, 20, "the ball to stop");
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.IsNotNull(GameCapture.Save($"{Dir}/12-4-result.png"));
            yield return WaitFor(() => game.Current == GolfGame.State.Aim, 10, "the next stroke");
            yield return null;
            Assert.IsNotNull(GameCapture.Save($"{Dir}/12-5-second-shot.png"));
        }

        /// A tee shot that comes up short into the water on Hole 12: the ball must fly a level
        /// arc off the tee island and drop to the sea, never sink into the island.
        [UnityTest]
        public IEnumerator CapturesASplash()
        {
            Time.timeScale = 1f;
            yield return SceneManager.LoadSceneAsync("Golf", LoadSceneMode.Single);
            var game = Object.FindFirstObjectByType<GolfGame>();
            Assert.IsNotNull(game?.Swing?.Synthetic);
            yield return null;
            game.Play();
            yield return null;
            game.JumpToHole(12);
            yield return WaitFor(() => game.Current == GolfGame.State.Aim, 14, "the showcase to end");
            yield return WaitFor(() => game.Swing.Phase == Swing.SwingPhase.Address, 5, "address");
            game.Swing.Synthetic.SpeedScale = 0.7;   // a lazy swing: speed decides the distance now
            game.Swing.Synthetic.Backswing(true);
            yield return new WaitForSecondsRealtime(0.22f);
            game.Swing.Synthetic.Backswing(false);
            yield return WaitFor(() => game.Current == GolfGame.State.Flight, 5, "impact");
            Assert.Less(game.LastShot.Carry, 110, "a short one, into the water");
            yield return new WaitForSecondsRealtime(0.9f);
            Assert.IsNotNull(GameCapture.Save($"{Dir}/12-9-splash-air.png"));
            yield return new WaitForSecondsRealtime(1.4f);
            Assert.IsNotNull(GameCapture.Save($"{Dir}/12-10-splash-drop.png"));
            yield return WaitFor(() => game.Current == GolfGame.State.Result, 20, "the splash");
            Assert.AreEqual(GolfArcade.Course.CourseLie.Water, game.LastShot.Lie);
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.IsNotNull(GameCapture.Save($"{Dir}/12-11-splash-result.png"));
        }

        /// Putting on the sculpted green: the read (grid, beads, ribbon, flag out), the roll
        /// watched from where it was read, and the hole cam as the ball arrives.
        [UnityTest]
        public IEnumerator CapturesTheGreenRead()
        {
            Time.timeScale = 1f;
            yield return SceneManager.LoadSceneAsync("Golf", LoadSceneMode.Single);
            var game = Object.FindFirstObjectByType<GolfGame>();
            Assert.IsNotNull(game?.Swing?.Synthetic);
            yield return null;
            game.Play();
            yield return null;
            game.JumpToHole(12);
            yield return null;
            game.DropBall(new GolfArcade.Course.CoursePoint(-7, 186)); // 13 yd out, across the shelf
            yield return WaitFor(() => game.Current == GolfGame.State.Aim, 5, "the putt to set up");
            yield return WaitFor(() => game.Swing.Phase == Swing.SwingPhase.Address, 5, "address");
            yield return new WaitForSecondsRealtime(0.8f);
            Assert.IsNotNull(GameCapture.Save($"{Dir}/12-6-read.png"));
            game.Swing.Synthetic.Backswing(true);
            yield return new WaitForSecondsRealtime(0.45f);
            game.Swing.Synthetic.Backswing(false);
            yield return WaitFor(() => game.Current == GolfGame.State.Flight, 5, "the stroke");
            Assert.AreEqual(GolfArcade.Shot.GolfClub.Putter, game.LastShot.Club);
            yield return new WaitForSecondsRealtime(1.0f);
            Assert.IsNotNull(GameCapture.Save($"{Dir}/12-7-roll.png"));
            yield return WaitFor(() => game.Current == GolfGame.State.Result, 20, "the putt to stop");
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.IsNotNull(GameCapture.Save($"{Dir}/12-8-holed-or-hole-cam.png"));
        }
    }
}
