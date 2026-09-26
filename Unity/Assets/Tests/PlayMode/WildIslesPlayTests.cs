using System.Collections;
using GolfArcade.Course;
using GolfArcade.Game;
using GolfArcade.Profile;
using GolfArcade.Shot;
using GolfArcade.Swing;
using GolfArcade.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GolfArcade.PlayTests
{
    /// Wild Isles in the game: a drive skidding across Frostbite Fjord's ice, one into Volcano
    /// Rim's lava, the windmill's sails knocking balls down or letting them through; and what is
    /// earned — the locked course, the GEAR page, the announcement, a trail. Frames go to
    /// Library/Captures/wild.
    public class WildIslesPlayTests
    {
        const string Dir = "Library/Captures/wild";

        static IEnumerator WaitFor(System.Func<bool> done, float seconds, string what)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (!done())
            {
                if (Time.realtimeSinceStartup > until) Assert.Fail($"timed out waiting for {what}");
                yield return null;
            }
        }

        static void Press(string name)
        {
            var go = GameObject.Find(name);
            Assert.IsTrue(go, $"no {name} on the screen");
            go.GetComponent<HoldButton>().Pressed();
        }

        static IEnumerator Round(System.Action<GolfGame> ready)
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
            ready(game);
        }

        [UnityTest]
        public IEnumerator TheIceTheLavaAndTheSails()
        {
            GolfGame game = null;
            yield return Round(g => game = g);

            // Frostbite Fjord: a drive down onto the frozen lake skids on across it
            game.JumpToHole(17);
            yield return WaitFor(() => game.Current == GolfGame.State.Aim, 45, "hole 17's tee");
            var hole = game.CurrentHole;
            // a spot on the ice a drive comes down on clear of the pines round the lake
            CoursePoint ice = default; bool found = false;
            for (double d = 175; d <= 300 && !found; d += 12)
                for (double x = 60; x <= 150 && !found; x += 12)
                {
                    var p = new CoursePoint(x, d);
                    if (hole.LieAt(p) != CourseLie.Ice) continue;
                    var plan = game.PlanFor(p);
                    if (plan.Knocks.Count == 0 && hole.LieAt(plan.Landing) == CourseLie.Ice) { ice = p; found = true; }
                }
            Assert.IsTrue(found, "a drive can reach the ice");
            Assert.Greater(HoleView.GroundHeight(ice), 5, "and the ice is ground the ball lies on");
            game.StrikeToward(ice);
            yield return WaitFor(() => game.Current == GolfGame.State.Flight, 5, "the drive");
            yield return WaitFor(() => game.LastShot != null && game.FlightTime > game.LastShot.LandingTime + 0.6, 20, "the ball down on the ice");
            Assert.IsNotNull(GameCapture.Save($"{Dir}/ice-a-skid.png"));
            yield return WaitFor(() => game.Current == GolfGame.State.Result, 30, "the drive to stop");
            var shot = game.LastShot;
            Debug.Log($"WILD ice: landed {shot.Landing} ({hole.LieAt(shot.Landing)}), rolled {shot.Roll:F0} yd, rests {shot.Rest} ({shot.Lie})");
            Assert.AreEqual(CourseLie.Ice, hole.LieAt(shot.Landing), "it came down on the ice");
            Assert.Greater(shot.Landing.DistanceTo(shot.Rest), 25, "and skidded on");
            Assert.AreEqual(0, shot.PenaltyStrokes, "no penalty");
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.IsNotNull(GameCapture.Save($"{Dir}/ice-b-result.png"));
            yield return WaitFor(() => game.Current == GolfGame.State.Aim, 10, "the next shot");

            // Volcano Rim: a drive into the lava river is a penalty, and a flare
            game.JumpToHole(16);
            yield return WaitFor(() => game.Current == GolfGame.State.Aim, 45, "hole 16's tee");
            hole = game.CurrentHole;
            var lava = new CoursePoint(0, 205.6);
            Assert.AreEqual(HazardKind.Lava, hole.HazardAt(lava));
            game.StrikeToward(lava);
            yield return WaitFor(() => game.Current == GolfGame.State.Flight, 5, "the drive");
            yield return WaitFor(() => game.Current == GolfGame.State.Result || (game.LastShot != null && game.FlightTime > game.LastShot.Duration + 0.15), 30, "the drive into the lava");
            Assert.IsNotNull(GameCapture.Save($"{Dir}/lava-a-flare.png"));
            yield return WaitFor(() => game.Current == GolfGame.State.Result, 10, "the result");
            shot = game.LastShot;
            Assert.AreEqual(CourseLie.Water, shot.Lie, $"in the lava ({shot.Rest})");
            Assert.AreEqual(HazardKind.Lava, hole.HazardAt(shot.Rest));
            StringAssert.StartsWith("Lava", game.LastResult);
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.IsNotNull(GameCapture.Save($"{Dir}/lava-b-badge.png"));
            yield return WaitFor(() => game.Current == GolfGame.State.Aim, 10, "the drop");

            // Windmill Links: from in front of the sails, the same shots at the same blades on
            // their way round — some knocked down by a blade, some through the gaps
            game.JumpToHole(20);
            yield return WaitFor(() => game.Current == GolfGame.State.Aim, 45, "hole 20's tee");
            hole = game.CurrentHole;
            var sails = hole.Windmill;
            Assert.IsNotNull(sails, "the windmill's sails are read off the model");
            Debug.Log($"WILD sails: hub ({sails.HubX:F1}, {sails.HubY:F1}, {sails.HubD:F1}), reach {sails.Reach:F1} yd, axle ({sails.Ax:F2}, {sails.Ay:F2}, {sails.Ad:F2})");
            Assert.That(sails.Reach, Is.InRange(14.0, 22.0), "blades about 16 m long");
            Assert.Less(System.Math.Abs(sails.Ay), 0.1, "the axle is level: the blades turn in an upright plane");
            double lx = sails.Ax, ld = sails.Ad, ll = System.Math.Sqrt(lx * lx + ld * ld);
            var hub = new CoursePoint(sails.HubX, sails.HubD);
            var from = new CoursePoint(sails.HubX + lx / ll * 70, sails.HubD + ld / ll * 70);
            Assert.AreNotEqual(CourseLie.Water, hole.LieAt(from), "the spot in front of the windmill is dry");
            int hits = 0, through = 0;
            double keep = sails.AngleAtLaunch;
            // aimed beside the hub (not at it: the hub and the blades' stocks are always there)
            foreach (double beside in new[] { -11.0, -7.0, 7.0, 11.0 })
            for (double power = 0.5; power <= 0.9; power += 0.1)
                for (double angle = 0; angle < 360; angle += 20)
                {
                    sails.AngleAtLaunch = angle;
                    var aim = new CoursePoint(hub.X - ld / ll * beside, hub.D + lx / ll * beside);
                    var s = new CourseShot(GolfClub.Iron, new SwingImpact { Power = power }, from.HeadingTo(aim), from, hole, 1, Wind.Calm);
                    bool blade = false;
                    foreach (var k in s.Knocks)
                        if (System.Math.Sqrt((k.X - sails.HubX) * (k.X - sails.HubX) + (k.D - sails.HubD) * (k.D - sails.HubD)) < sails.Reach + 2 && k.Hard) blade = true;
                    if (blade) hits++; else if (s.Carry > 75) through++;
                }
            sails.AngleAtLaunch = keep;
            Debug.Log($"WILD sails: {hits} knocked down by a blade, {through} through the gaps");
            Assert.Greater(hits, 0, "some meet a blade");
            Assert.Greater(through, 0, "some slip between");
            var spinner = Object.FindFirstObjectByType<Spinner>();
            Assert.IsNotNull(spinner);
            double a0 = spinner.Angle;
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.Greater(spinner.Angle, a0 + 5, "the sails turn");
            var cam = Camera.main;
            var hubW = new Vector3((float)sails.HubX, (float)sails.HubY, (float)sails.HubD);
            game.enabled = false;
            cam.transform.position = hubW + new Vector3((float)(lx / ll), 0, (float)(ld / ll)) * 45 + Vector3.down * 12;
            cam.transform.LookAt(hubW + Vector3.down * 6);
            Assert.IsNotNull(GameCapture.Save($"{Dir}/sails.png"));
            game.enabled = true;
        }

        [UnityTest]
        public IEnumerator EarningThings()
        {
            Time.timeScale = 1f;
            yield return SceneManager.LoadSceneAsync("Golf", LoadSceneMode.Single);
            var cam = Camera.main;
            cam.aspect = GameCapture.PhoneWidth / (float)GameCapture.PhoneHeight;
            var game = Object.FindFirstObjectByType<GolfGame>();
            game.InstantReplays = false;
            var me = ProfileStore.Active;
            string saved = JsonUtility.ToJson(me);
            int holes = game.ChosenHoles;
            try
            {
                Unlocks.Everything = false;
                me.Stats = new ProfileStats(); me.Announced.Clear();
                me.Ball = "ball.white"; me.Trail = "trail.none"; me.Club = "club.classic";
                game.ChooseHoles(0);
                yield return new WaitForSecondsRealtime(1f);

                // the course screen: Wild Isles is locked until Cliffside is beaten
                Press("Course");
                yield return new WaitForSecondsRealtime(1f);
                for (int i = 0; i < 12 && game.BrowsedHole.Number != 16; i++) { Press("Next hole"); yield return null; }
                Assert.AreEqual(16, game.BrowsedHole.Number);
                yield return new WaitForSecondsRealtime(2f);
                Assert.IsTrue(GameObject.Find("Locked"), "the padlock and what it takes");
                Assert.IsNotNull(GameCapture.Save($"{Dir}/lock-a-course.png"));
                Press("Full round"); Press("Select");
                yield return null;
                Assert.IsTrue(GameObject.Find("Course select"), "SELECT does nothing while it is locked");
                me.Stats.RecordCourse("cliffside", -1);
                Press("Previous hole"); Press("Next hole");
                yield return null;
                Assert.IsFalse(GameObject.Find("Locked"), "beaten Cliffside: open");
                Press("Back");
                yield return new WaitForSecondsRealtime(0.5f);

                // the golfer screen's GEAR page, the gold ball padlocked
                game.OpenGolferPicker();
                yield return new WaitForSecondsRealtime(1f);
                Press("Gear");
                yield return new WaitForSecondsRealtime(0.5f);
                Press("BALL Gold");
                yield return null;
                Assert.AreEqual("ball.white", me.Ball, "the gold ball is not theirs yet");
                Assert.IsNotNull(GameCapture.Save($"{Dir}/lock-b-gear.png"));
                Press("Lets go");
                yield return new WaitForSecondsRealtime(0.5f);

                // an eagle: the round's end announces what it earned, and the gold ball is theirs
                me.Stats.Eagles = 1; me.Stats.LongestDrive = 270;
                var news = Unlocks.Announce(me);
                CollectionAssert.Contains(news.ConvertAll(r => r.Id), "ball.gold");
                var hud = Object.FindFirstObjectByType<Hud>();
                hud.ShowUnlocks(news.ConvertAll(r => ((string)null, r)));
                yield return new WaitForSecondsRealtime(0.5f);
                Assert.IsNotNull(hud.UnlockShowing, "announced");
                Assert.IsNotNull(GameCapture.Save($"{Dir}/lock-c-announced.png"));
                game.OpenGolferPicker();
                yield return new WaitForSecondsRealtime(0.8f);
                Press("Gear");
                Press("BALL Gold"); Press("TRAIL Fire");
                yield return new WaitForSecondsRealtime(0.5f);
                Assert.AreEqual("ball.gold", me.Ball);
                Assert.AreEqual("trail.fire", me.Trail);
                Assert.IsNotNull(GameCapture.Save($"{Dir}/lock-d-gear-gold.png"));
                Press("Lets go");
                yield return new WaitForSecondsRealtime(0.5f);

                // and the fire trail behind the ball
                game.Play();
                yield return null;
                game.JumpToHole(12);
                yield return WaitFor(() => game.Current == GolfGame.State.Aim, 45, "the tee");
                game.StrikeToward(game.CurrentHole.Pin);
                yield return WaitFor(() => game.LastShot != null && game.FlightTime > game.LastShot.LandingTime * 0.55, 20, "the ball in the air");
                Assert.IsNotNull(GameCapture.Save($"{Dir}/lock-e-trail.png"));
                yield return WaitFor(() => game.Current == GolfGame.State.Result, 30, "the shot to finish");
            }
            finally
            {
                JsonUtility.FromJsonOverwrite(saved, me);
                ProfileStore.Save();
                game.ChooseHoles(holes);
                Unlocks.Everything = false;
            }
        }
    }
}
