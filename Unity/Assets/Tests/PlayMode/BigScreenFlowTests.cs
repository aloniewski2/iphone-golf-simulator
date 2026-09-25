using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GolfArcade.Game;
using GolfArcade.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GolfArcade.PlayTests
{
    /// With the course on the TV the phone is the controller — but the round still has to be
    /// startable from it: the menu's Play button is on top and takes the tap, the opening
    /// plays with a skip button on the phone, and the round starts at the tee after it.
    public class BigScreenFlowTests
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

        /// What a tap at the middle of `target` would land on first.
        static GameObject TopHitAt(RectTransform target)
        {
            var at = RectTransformUtility.WorldToScreenPoint(null, target.TransformPoint(target.rect.center));
            var hits = new List<RaycastResult>();
            var events = EventSystem.current ?? Object.FindFirstObjectByType<EventSystem>();
            Assert.IsNotNull(events, "the scene has an EventSystem");
            events.RaycastAll(new PointerEventData(events) { position = at }, hits);
            return hits.Count > 0 ? hits[0].gameObject : null;
        }

        /// Runs a step, and on an exception fails with where it came from.
        static void Step(string what, System.Action act)
        {
            try { act(); }
            catch (System.Exception e) when (e is not AssertionException) { Assert.Fail($"{what}: {e}"); }
        }

        [UnityTest]
        public IEnumerator TheRoundStartsFromThePhoneWithTheCourseOnTheTv()
        {
            Time.timeScale = 1f;
            yield return SceneManager.LoadSceneAsync("Golf", LoadSceneMode.Single);
            var game = Object.FindFirstObjectByType<GolfGame>();
            game.InstantReplays = false;   // (ReplayTests covers the replays; these keep their timings)
            var hud = Object.FindFirstObjectByType<Hud>();
            yield return new WaitForSecondsRealtime(0.5f);
            try
            {
                Assert.IsNotNull(game, "the game"); Assert.IsNotNull(hud, "the HUD");
                Step("turning the TV on at the menu", () => game.PreviewBigScreen(true));
                yield return null;
                Assert.AreEqual(GolfGame.State.Menu, game.Current);
                Assert.IsNotNull(hud.Controller, "the controller layout is on");
                Assert.IsFalse(hud.Controller.Shown, "the controller keeps out of the menu's way");
                HoldButton play = null; GameObject hit = null;
                Step("finding what a tap on Play hits", () =>
                {
                    play = Object.FindObjectsByType<HoldButton>(FindObjectsSortMode.None).First(b => b.name == "Play" && b.isActiveAndEnabled);
                    hit = TopHitAt((RectTransform)play.transform);
                });
                Assert.IsTrue(hit && hit.transform.IsChildOf(play.transform), $"a tap on Play reaches it (it hit {(hit ? hit.name : "nothing")})");
                Assert.IsNotNull(GameCapture.Save($"{Dir}/0d-menu-tv.png"));

                Step("pressing Play", game.Play);
                yield return WaitFor(() => game.Current == GolfGame.State.Intro, 5, "the opening");
                yield return new WaitForSecondsRealtime(1.0f);
                Assert.IsTrue(hud.Controller.Shown && hud.Controller.ShowingIntro, "the phone offers to skip the opening");
                Assert.IsTrue(hud.HoleIntroShowing, "the tournament title is up over the flyover");
                Assert.IsNotNull(GameCapture.Save($"{Dir}/0e-intro-tv.png"));

                Step("pressing Skip", () => hud.Controller.Skip.Pressed());
                yield return WaitFor(() => game.Current == GolfGame.State.Aim, 3, "the round to start at the tee");
                Assert.IsTrue(hud.Controller.Shown && !hud.Controller.ShowingIntro, "the aim pad is back for the round");
                Assert.IsFalse(hud.HoleIntroShowing, "skipped from the flyover, the tournament title goes with it");

                // The joystick aims: a press on the knob, dragged right, turns the line right.
                yield return WaitFor(() => game.Swing.Phase == Swing.SwingPhase.Address, 5, "address");
                var knob = (RectTransform)hud.Controller.Knob.transform;
                Assert.IsTrue(knob.gameObject.activeInHierarchy, "the knob is up while aiming");
                var events = EventSystem.current ?? Object.FindFirstObjectByType<EventSystem>();
                var at = RectTransformUtility.WorldToScreenPoint(null, knob.TransformPoint(knob.rect.center));
                var press = new PointerEventData(events) { position = at, pressPosition = at, button = PointerEventData.InputButton.Left };
                double before = game.AimHeading;
                Step("pressing the knob", () => ExecuteEvents.Execute(knob.gameObject, press, ExecuteEvents.pointerDownHandler));
                for (int i = 0; i < 40; i++)
                {
                    press.position = at + new Vector2(Mathf.Min(i * 12f, 160f) * knob.lossyScale.x / Mathf.Max(0.0001f, hud.transform.lossyScale.x) , 0);
                    Step("dragging the knob", () => ExecuteEvents.Execute(knob.gameObject, press, ExecuteEvents.dragHandler));
                    yield return null;
                }
                float pushed = hud.AimStick;
                Step("letting go", () => ExecuteEvents.Execute(knob.gameObject, press, ExecuteEvents.pointerUpHandler));
                Assert.Greater(pushed, 0.5f, "the stick reads a push to the right");
                Assert.Greater(game.AimHeading - before, 1.0, $"the line turned right (from {before:F1}° to {game.AimHeading:F1}°)");
                Assert.AreEqual(0f, hud.AimStick, "let go, the knob springs back");
            }
            finally { game.PreviewBigScreen(false); }
        }
    }
}
