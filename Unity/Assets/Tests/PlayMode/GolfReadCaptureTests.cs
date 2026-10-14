using System.Collections;
using System.IO;
using GolfArcade.Course;
using GolfArcade.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GolfArcade.PlayTests
{
    /// What the player has to read before a stroke, from the game's own camera on the TV (16:9) at address: a short
    /// putt, a long breaking putt, a full shot in a strong crosswind (and where it came down), and shots from the rough
    /// and the sand. Frames and a log of the numbers go to GOLF_READ_DIR (default Library/Captures/golf-read).
    /// Run windowed: Unity/Tools/run-playmode-windowed.sh GolfReadCaptureTests
    public class GolfReadCaptureTests
    {
        static string Dir => System.Environment.GetEnvironmentVariable("GOLF_READ_DIR") is { Length: > 0 } d ? d : "Library/Captures/golf-read";
        static StringWriter log;

        static IEnumerator WaitFor(System.Func<bool> done, float seconds, string what)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (!done())
            {
                if (Time.realtimeSinceStartup > until)
                {
                    var game = Object.FindFirstObjectByType<GolfGame>();
                    Assert.Fail($"timed out waiting for {what} (the game is in {game?.Current}, hole {game?.CurrentHole?.Number})");
                }
                yield return null;
            }
        }

        /// The TV's picture: the course camera at 16:9 and the broadcast HUD laid out for 1920×1080 (not the editor window).
        static void Tv(string name)
        {
            var cam = Camera.main;
            cam.aspect = 16f / 9;
            var rt = RenderTexture.GetTemporary(1920, 1080, 24);
            cam.targetTexture = rt;
            foreach (var hud in Object.FindObjectsByType<GolfArcade.UI.GolfShotHud>(FindObjectsSortMode.None)) hud.RefreshLayout();
            cam.targetTexture = null;
            RenderTexture.ReleaseTemporary(rt);
            Canvas.ForceUpdateCanvases();
            Assert.IsNotNull(GameCapture.Save($"{Dir}/{name}.png", 1920, 1080));
        }

        static void Note(GolfGame game, string name)
        {
            var at = game.BallAt; var hole = game.CurrentHole;
            var lie = hole.LieAt(at);
            log.WriteLine($"{name}: hole {hole.Number} ball {at} lie {lie} club {game.ClubInHand} to pin {at.DistanceTo(hole.Pin):F1} yd, wind {game.Wind} ({game.Wind.Describe(game.AimHeading)})");
            // what the aim shows: the line renderer's points, and how far out the dots reach against the shot
            var line = GameObject.Find("Aim line")?.GetComponent<LineRenderer>();
            var dots = GameObject.Find("Aim dots");
            int shown = 0; double farthest = 0;
            if (dots) foreach (Transform d in dots.transform)
                    if (d.name == "Dot" && d.gameObject.activeSelf) { shown++; farthest = System.Math.Max(farthest, HoleView.ToCourse(d.position).DistanceTo(at)); }
            bool putt = game.ClubInHand == Shot.GolfClub.Putter;
            double whole = putt ? at.DistanceTo(hole.Pin) : Shot.BallFlight.Simulate(lie.LaunchFrom(game.ClubInHand, 1, 0, 0)).CarryPoint.DistanceYards;
            log.WriteLine($"  aim line points {(line ? line.positionCount : -1)}, aim dots {shown}, farthest {farthest:F1} yd = {farthest / System.Math.Max(0.01, whole):P0} of the {(putt ? "putt" : "still-air carry")} ({whole:F1} yd)");
            var hud = GameObject.Find("Golf broadcast");
            string Text(string label) { if (!hud) return "-"; foreach (var t in hud.GetComponentsInChildren<UnityEngine.UI.Text>(true)) if (t.name == label) return t.gameObject.activeInHierarchy ? t.text.Replace("\n", " / ") : "(hidden) " + t.text; return "-"; }
            log.WriteLine($"  HUD wind \"{Text("Wind")}\", lie \"{Text("Lie name")}\" \"{Text("Lie cost")}\"");
            var grain = GameObject.Find("Green read");
            log.WriteLine($"  green read {(grain ? string.Join(",", System.Linq.Enumerable.Select(grain.GetComponentsInChildren<MeshFilter>(), f => $"{f.name}:{f.sharedMesh.vertexCount}v")) : "off")}");
        }

        /// The ball dropped at `at` in `wind`, the stroke set up, the camera settled at address.
        static IEnumerator AddressAt(GolfGame game, CoursePoint at, Wind wind)
        {
            game.SetWind(wind);
            game.DropBall(at);
            yield return WaitFor(() => game.Current == GolfGame.State.Aim, 5, "the stroke to set up");
            yield return new WaitForSecondsRealtime(1.2f);
        }

        /// To hole `number` as the app plays it: its opening (the flyover skipped, the golfer's introduction played out) to
        /// the tee shot's address, so nothing of the opening is left on screen.
        static IEnumerator ToHole(GolfGame game, int number)
        {
            game.JumpToHole(number);
            float until = Time.realtimeSinceStartup + 60;
            while (game.Current != GolfGame.State.Aim)
            {
                if (Time.realtimeSinceStartup > until) Assert.Fail($"hole {number}'s opening never reached the tee (the game is in {game.Current})");
                if (game.Current == GolfGame.State.Intro) game.SkipPresentation();
                yield return new WaitForSecondsRealtime(0.25f);
            }
            yield return new WaitForSecondsRealtime(0.5f);
        }

        /// The point `radius` yards from the pin, on the green, whose straight line to the hole crosses the most slope.
        static CoursePoint BreakingPutt(Hole hole, double radius)
        {
            var best = hole.Pin; double most = -1;
            for (int a = 0; a < 72; a++)
            {
                double t = a * 5 * System.Math.PI / 180;
                var p = new CoursePoint(hole.Pin.X + System.Math.Sin(t) * radius, hole.Pin.D + System.Math.Cos(t) * radius);
                if (hole.LieAt(p) != CourseLie.Green) continue;
                double ux = (hole.Pin.X - p.X) / radius, ud = (hole.Pin.D - p.D) / radius, across = 0;
                for (int i = 0; i < 10; i++)
                {
                    var q = new CoursePoint(p.X + ux * radius * i / 10, p.D + ud * radius * i / 10);
                    var g = hole.Surface.Gradient(q);
                    across += g.dx * ud - g.dd * ux;
                }
                if (System.Math.Abs(across) > most) { most = System.Math.Abs(across); best = p; }
            }
            Assert.Greater(most, 0, $"a point {radius} yd out on hole {hole.Number}'s green");
            return best;
        }

        /// A spot in the rough a few yards off the fairway, `yards` short of the pin along the hole's centerline.
        static CoursePoint Rough(Hole hole, double yards)
        {
            var line = hole.Centerline;
            foreach (double margin in new[] { 12.0, 6.0, -1000.0 })
            for (double want = yards; want <= yards + 60; want += 5)
                for (int i = line.Length - 1; i > 0; i--)
                {
                    var a = line[i - 1]; var b = line[i];
                    if (a.DistanceTo(hole.Pin) < want || b.DistanceTo(hole.Pin) > want) continue;
                    double len = a.DistanceTo(b), tx = (b.X - a.X) / len, td = (b.D - a.D) / len;
                    for (double s = 0; s <= len; s += 1)
                    {
                        var c = new CoursePoint(a.X + tx * s, a.D + td * s);
                        if (System.Math.Abs(c.DistanceTo(hole.Pin) - want) > 1) continue;
                        foreach (int side in new[] { -1, 1 })
                        {
                            var p = new CoursePoint(c.X + side * td * (hole.FairwayWidth / 2 + 4), c.D - side * tx * (hole.FairwayWidth / 2 + 4));
                            if (hole.LieAt(p) == CourseLie.Rough && Clear(hole, p, margin)) return p;
                        }
                    }
                }
            Assert.Fail($"no rough {yards} yd out on hole {hole.Number}");
            return default;
        }

        /// Nothing standing near and no hazard close, so the picture is plainly of the rough.
        static bool Clear(Hole hole, CoursePoint p, double margin)
        {
            foreach (var o in hole.Obstacles) if (new CoursePoint(o.X, o.D).DistanceTo(p) < o.Radius + 4) return false;
            foreach (var h in hole.Hazards) if (new CoursePoint(h.X, h.Distance).DistanceTo(p) < System.Math.Max(h.Width, h.Length) / 2 + margin) return false;
            return true;
        }

        /// Line the shot up on the pin (the aim taps the phone sends) and take a club that gets there from this lie.
        static IEnumerator AimAtThePin(GolfGame game)
        {
            var hole = game.CurrentHole; var at = game.BallAt;
            for (int i = 0; i < 200; i++)
            {
                float off = Mathf.DeltaAngle((float)game.AimHeading, (float)at.HeadingTo(hole.Pin));
                if (Mathf.Abs(off) < 0.2f) break;
                game.NativeAim(Mathf.Clamp(off / game.AimTapDegrees, -1, 1));
            }
            var lie = hole.LieAt(at); double yards = at.DistanceTo(hole.Pin);
            for (int i = 0; i < 10 && Shot.GolfClubs.ReferenceDistanceYards(game.ClubInHand) * lie.PowerFactor(game.ClubInHand) < yards - 4; i++) game.NativeClub(-1);
            yield return new WaitForSecondsRealtime(1.0f);
        }

        /// Where the aim dots fall in the picture (the first few), to see which of them show.
        static void NoteDots(GolfGame game)
        {
            var dots = GameObject.Find("Aim dots"); var cam = Camera.main;
            if (!dots || !cam) return;
            var parts = new System.Collections.Generic.List<string>();
            foreach (Transform d in dots.transform)
                if (d.name == "Dot" && d.gameObject.activeSelf && parts.Count < 8)
                {
                    var v = cam.WorldToViewportPoint(d.position);
                    parts.Add($"({v.x:F2},{v.y:F2},{v.z:F0}{(d.GetComponent<Renderer>().isVisible ? "" : " hidden")})");
                }
            log.WriteLine($"  dots in view (x,y,depth): {string.Join(" ", parts)}; queue {(dots.transform.childCount > 0 ? dots.transform.GetChild(0).GetComponent<Renderer>().sharedMaterial.renderQueue : -1)}");
        }

        /// The middle of the bunker nearest `yards` from the pin.
        static CoursePoint Sand(Hole hole, double yards)
        {
            CoursePoint? best = null; double off = double.MaxValue;
            foreach (var h in hole.Hazards)
            {
                if (h.Kind != HazardKind.Bunker) continue;
                var c = new CoursePoint(h.X, h.Distance);
                double o = System.Math.Abs(c.DistanceTo(hole.Pin) - yards);
                if (hole.LieAt(c) == CourseLie.Bunker && o < off) { off = o; best = c; }
            }
            Assert.IsNotNull(best, $"a bunker on hole {hole.Number}");
            return best.Value;
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator CapturesPuttsWindAndLies()
        {
            Time.timeScale = 1f;
            Directory.CreateDirectory(Dir);
            log = new StringWriter();
            yield return SceneManager.LoadSceneAsync("Golf", LoadSceneMode.Single);
            var game = Object.FindFirstObjectByType<GolfGame>();
            game.InstantReplays = false;
            try
            {
                yield return null;
                // golf as the app plays it: the phone is the controller, so no on-screen buttons
                game.PrepareNativeAddress("cliffside");
                yield return null;

                // the island green: a short putt and a long one across its slopes
                yield return ToHole(game, 12);
                var hole = game.CurrentHole;
                yield return AddressAt(game, BreakingPutt(hole, 2.7), new Wind(6, 40));
                Note(game, "1-short-putt"); Tv("1-short-putt");
                yield return AddressAt(game, BreakingPutt(hole, 13), new Wind(6, 40));
                Note(game, "2-long-breaking-putt"); Tv("2-long-breaking-putt");

                // the tee shot over the water in a strong wind from the left
                double teeLine = hole.Tee.HeadingTo(hole.Pin);
                yield return AddressAt(game, hole.Tee, new Wind(18, teeLine + 90));
                Note(game, "3-strong-wind"); NoteDots(game); Tv("3-strong-wind");
                // (diagnostic: the same frame with the dots drawn after every other see-through thing)
                var dotMaterial = GameObject.Find("Aim dots")?.transform.GetChild(0).GetComponent<Renderer>().sharedMaterial;
                if (dotMaterial)
                {
                    int queue = dotMaterial.renderQueue;
                    dotMaterial.renderQueue = 4000; yield return null;
                    Tv("3z-diagnostic-dots-drawn-last");
                    dotMaterial.renderQueue = queue; yield return null;
                }
                double aimed = game.AimHeading;
                Random.InitState(20261012);   // the same strike's luck before and after
                game.NativeSwing(0.85f);      // straight down the line the game set up, at 85 %
                yield return WaitFor(() => game.Current == GolfGame.State.Flight, 5, "impact");
                var shot = game.LastShot;
                double flewOn = game.BallAt.HeadingTo(shot.Landing);
                log.WriteLine($"3-strong-wind shot: {shot.Club} power {shot.Power:F2} carry {shot.Carry:F1} yd, came down {System.Math.Sin((flewOn - aimed) * System.Math.PI / 180) * shot.Carry:F1} yd right of the aim, lie {shot.Lie}");
                yield return WaitFor(() => game.Bounces > 0 || game.Current != GolfGame.State.Flight, 20, "the ball to come down");
                yield return null;
                Tv("3b-strong-wind-landing");
                yield return WaitFor(() => game.Current is GolfGame.State.Result or GolfGame.State.Aim, 25, "the shot to finish");

                // Cliffside: a shot from the rough and one from the sand
                yield return ToHole(game, 7);
                hole = game.CurrentHole;
                yield return AddressAt(game, Rough(hole, 150), new Wind(8, 200));
                yield return AimAtThePin(game);
                Note(game, "4-rough"); NoteDots(game); Tv("4-rough");
                yield return AddressAt(game, Sand(hole, 120), new Wind(8, 200));
                yield return AimAtThePin(game);
                Note(game, "5-sand"); NoteDots(game); Tv("5-sand");
            }
            finally
            {
                File.WriteAllText($"{Dir}/log.txt", log.ToString());
                Camera.main.targetTexture = null;
            }
        }
    }
}
