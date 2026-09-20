using System.Collections;
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

            yield return new WaitForSecondsRealtime(1.5f);
            Assert.IsNotNull(GameCapture.Save($"{Dir}/1-flyover.png"));
            yield return WaitFor(() => game.Current == GolfGame.State.Aim, 10, "the flyover to end");
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
        }
    }
}
