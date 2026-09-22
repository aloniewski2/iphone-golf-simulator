using System.Collections;
using GolfArcade.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GolfArcade.PlayTests
{
    /// Drives the real scene with the synthetic swing: the hole flies over, the player aims,
    /// a held backswing is released, the ball flies, and a result comes back.
    public class RoundSmokeTests
    {
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
        public IEnumerator TeeShotFliesAndReportsAResult()
        {
            Time.timeScale = 4f;
            yield return SceneManager.LoadSceneAsync("Golf", LoadSceneMode.Single);
            var game = Object.FindFirstObjectByType<GolfGame>();
            Assert.IsNotNull(game, "the Golf scene bootstraps the game");
            Assert.IsNotNull(game.Swing.Synthetic, "no gyro in the editor, so the synthetic swing drives it");
            yield return null;
            game.Play();

            yield return WaitFor(() => game.Current == GolfGame.State.Aim, 14, "the showcase to end");
            yield return WaitFor(() => game.Swing.Phase == Swing.SwingPhase.Address, 5, "the phone to settle");

            game.Swing.Synthetic.Backswing(true);
            yield return new WaitForSecondsRealtime(0.7f);
            game.Swing.Synthetic.Backswing(false);

            yield return WaitFor(() => game.Current == GolfGame.State.Flight, 5, "impact");
            Assert.IsNotNull(game.LastShot);
            Assert.Greater(game.LastShot.Power, 0.2, "a held backswing is a real swing");
            Assert.Greater(game.LastShot.Total, 40, "and it goes somewhere");

            yield return WaitFor(() => game.Current == GolfGame.State.Result, 20, "the ball to stop");
            yield return WaitFor(() => game.Current == GolfGame.State.Aim, 10, "the next stroke to set up");
            Time.timeScale = 1f;
        }
    }
}
