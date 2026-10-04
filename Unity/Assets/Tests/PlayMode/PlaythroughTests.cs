using System.Collections;
using System.Collections.Generic;
using GolfArcade.Course;
using GolfArcade.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GolfArcade.PlayTests
{
    /// Every hole played out by the game's own aim — each shot at the hole's recommended target,
    /// the putts read and struck — and anything that goes wrong written down: a ball stopped well
    /// short of where it was aimed, a penalty, a ball at rest on a cliff face, a shot that got
    /// nowhere, a hole that cannot be finished. Frames of each shot go to Library/Captures/play.
    public class PlaythroughTests
    {
        const string Dir = "Library/Captures/play";

        static IEnumerator WaitFor(System.Func<bool> done, float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (!done() && Time.realtimeSinceStartup < until) yield return null;
        }

        /// The steepest the ground falls away from a spot, over a yard each way (rise over run).
        static double Steepness(CoursePoint p)
        {
            double h = HoleView.GroundHeight(p), worst = 0;
            foreach (var (dx, dd) in new[] { (1.0, 0.0), (-1.0, 0.0), (0.0, 1.0), (0.0, -1.0) })
                worst = System.Math.Max(worst, System.Math.Abs(HoleView.GroundHeight(new CoursePoint(p.X + dx, p.D + dd)) - h));
            return worst;
        }

        static IEnumerator PlayOut(GolfGame game, int number, List<string> problems, bool frames)
        {
            game.JumpToHole(number);
            yield return WaitFor(() => game.Current == GolfGame.State.Aim, 45);
            game.SetWind(Wind.Calm);
            var hole = game.CurrentHole;
            var at = hole.Tee;
            int strokes = 0;
            for (int shot = 1; shot <= 12; shot++)
            {
                if (game.Current != GolfGame.State.Aim) { problems.Add($"{number}: shot {shot} could not be set up ({game.Current})"); break; }
                bool putt = hole.LieAt(at).IsPuttingSurface();
                CourseShot plan = null;
                CoursePoint target = hole.Pin;
                if (putt) game.StrikeHolingPutt();
                else
                {
                    target = hole.RecommendedTarget(at);
                    plan = game.PlanFor(target);
                    if (frames)
                    {
                        yield return new WaitForSecondsRealtime(1.2f);   // (the camera settles behind the ball)
                        GameCapture.Save($"{Dir}/{number:00}-{shot:00}-a-address.jpg", 540, 1170);
                    }
                    game.StrikeToward(target);
                }
                yield return WaitFor(() => game.Current == GolfGame.State.Flight, 5);
                if (frames && !putt)
                {
                    yield return WaitFor(() => game.LastShot != null && game.FlightTime > game.LastShot.LandingTime * 0.6, 20);
                    GameCapture.Save($"{Dir}/{number:00}-{shot:00}-b-flight.jpg", 540, 1170);
                }
                yield return WaitFor(() => game.Current is GolfGame.State.Result or GolfGame.State.HoleDone, 40);
                var s = game.LastShot;
                strokes += 1 + s.PenaltyStrokes;
                string club = putt ? "putt" : "shot";
                string line = $"{number}: {club} {shot} from ({at.X:F0},{at.D:F0}) at ({target.X:F0},{target.D:F0}) " +
                              $"landed ({s.Landing.X:F0},{s.Landing.D:F0}) rest ({s.Rest.X:F0},{s.Rest.D:F0}) {s.Lie} carry {s.Carry:F0} total {s.Total:F0} knocks {s.Knocks.Count}";
                Debug.Log("PLAY " + line);
                if (!putt && plan != null && plan.Landing.DistanceTo(s.Landing) > 15)
                    problems.Add($"{line}: came down {plan.Landing.DistanceTo(s.Landing):F0} yd from where it was planned");
                if (!putt && s.Rest.DistanceTo(target) > 60 && !s.IsHoled)
                    problems.Add($"{line}: finished {s.Rest.DistanceTo(target):F0} yd from its target");
                if (s.PenaltyStrokes > 0) problems.Add($"{line}: penalty");
                if (!s.IsHoled && Steepness(s.NextPosition) > 0.8) problems.Add($"{line}: at rest on a slope of {Steepness(s.NextPosition):F1} yd a yard");
                if (!s.IsHoled && s.NextPosition.DistanceTo(at) < 1 && !putt) problems.Add($"{line}: got nowhere");
                if (s.IsHoled) { yield return WaitFor(() => game.Current == GolfGame.State.HoleDone, 10); break; }
                at = s.NextPosition;
                yield return WaitFor(() => game.Current is GolfGame.State.Aim or GolfGame.State.HoleDone or GolfGame.State.RoundDone, 20);
                if (shot == 12) problems.Add($"{number}: not holed in 12");
            }
            Debug.Log($"PLAY {number}: {strokes} strokes, par {hole.Par}");
            if (strokes > hole.Par + 3) problems.Add($"{number}: took {strokes} on a par {hole.Par}");
        }

        [UnityTest, Timeout(1800000)]
        public IEnumerator EveryHolePlaysOut() => Play(new[] { 7, 12, 13, 14, 15, 16, 17, 18, 19, 20 });

        /// The Spiral on its own, with frames of every shot.
        [UnityTest, Timeout(600000)]
        public IEnumerator TheSpiralPlaysOut() => Play(new[] { 13 });

        /// The Magma Open's three holes on the crater's lava.
        [UnityTest, Timeout(900000)]
        public IEnumerator TheMagmaOpenPlaysOut() => Play(new[] { 21, 22, 23 });

        static IEnumerator Play(int[] holes)
        {
            Time.timeScale = 1f;
            yield return SceneManager.LoadSceneAsync("Golf", LoadSceneMode.Single);
            var cam = Camera.main;
            if (cam) cam.aspect = GameCapture.PhoneWidth / (float)GameCapture.PhoneHeight;
            var game = Object.FindFirstObjectByType<GolfGame>();
            game.InstantReplays = false;
            yield return null;
            game.ChooseHoles(0);
            game.Play();
            yield return null;
            var problems = new List<string>();
            foreach (var number in holes)
                yield return PlayOut(game, number, problems, number == 13);
            foreach (var p in problems) Debug.Log("PLAY PROBLEM " + p);
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }
    }
}
