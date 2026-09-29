using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GolfArcade.Game;
using GolfArcade.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GolfArcade.PlayTests
{
    /// Adnan's Hero as the golfer: it builds, stands at the ball, swings a clip per club, meets the ball, and
    /// every look (girl/boy, haircut, headwear, colours) builds and shows. Frames to Library/Captures/hero.
    public class HeroGolferTests
    {
        const string Dir = "Library/Captures/hero";

        static IEnumerator WaitFor(System.Func<bool> done, float seconds, string what)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (!done())
            {
                if (Time.realtimeSinceStartup > until) Assert.Fail($"timed out waiting for {what}");
                yield return null;
            }
        }

        static IEnumerator Start(System.Action<GolfGame> ready)
        {
            Time.timeScale = 1f;
            yield return SceneManager.LoadSceneAsync("Golf", LoadSceneMode.Single);
            var cam = Camera.main;
            if (cam) cam.aspect = GameCapture.PhoneWidth / (float)GameCapture.PhoneHeight;
            var game = Object.FindFirstObjectByType<GolfGame>();
            game.InstantReplays = false;
            yield return null;
            ready(game);
        }

        static GolferView Golfer() => GameObject.Find("Golfer").GetComponent<GolferView>();

        /// How close the clubhead gets to the ball with the older V4 golfer, as the yardstick for the Hero's.
        [UnityTest, Timeout(240000)]
        public IEnumerator TheV4GolferForComparison()
        {
            GolferStyle.HeroOverride = false;
            try
            {
                GolfGame game = null;
                yield return Start(g => game = g);
                game.ChooseHoles(0); game.Play();
                yield return null;
                game.JumpToHole(7);
                yield return WaitFor(() => game.Current == GolfGame.State.Aim, 45, "the tee");
                yield return WaitFor(() => game.Swing.Phase == Swing.SwingPhase.Address, 5, "address");
                var golfer = Golfer();
                Assert.IsFalse(golfer.IsHero);
                yield return new WaitForSecondsRealtime(0.4f);
                Assert.IsNotNull(GameCapture.Save($"{Dir}/v4-1-address.png"));
                var ball = game.BallPosition;
                var head0 = golfer.ClubHeadWorld();
                Debug.Log($"V4 address: ball {ball}, clubhead {head0}, distance {Vector3.Distance(ball, head0.Value):F3} yd");
                game.Swing.Synthetic.Backswing(true);
                yield return new WaitForSecondsRealtime(0.9f);
                Assert.IsNotNull(GameCapture.Save($"{Dir}/v4-2-top.png"));
                game.Swing.Synthetic.Backswing(false);
                float closest = float.MaxValue;
                float until = Time.realtimeSinceStartup + 1.2f;
                while (Time.realtimeSinceStartup < until)
                {
                    yield return null;
                    if (golfer.ClubHeadWorld() is Vector3 h) closest = Mathf.Min(closest, Vector3.Distance(ball, h));
                }
                Debug.Log($"V4 impact: closest approach {closest:F3} yd");
            }
            finally { GolferStyle.HeroOverride = null; }
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator TheHeroSwingsAndMeetsTheBall()
        {
            GolfGame game = null;
            yield return Start(g => game = g);
            Assert.IsTrue(GolferStyle.UseHero, "the Hero is in the build");
            game.ChooseHoles(0); game.Play();
            yield return null;
            game.JumpToHole(7);
            yield return WaitFor(() => game.Current == GolfGame.State.Aim, 45, "the tee");
            yield return WaitFor(() => game.Swing.Phase == Swing.SwingPhase.Address, 5, "address");
            var golfer = Golfer();
            Assert.IsTrue(golfer.IsHero, "the figure is the Hero");
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.IsNotNull(GameCapture.Save($"{Dir}/1-address.png"));
            var ball = game.BallPosition;
            var head0 = golfer.ClubHeadWorld();
            Assert.IsTrue(head0.HasValue, "a club in hand");
            Debug.Log($"HERO address: ball {ball}, clubhead {head0.Value}, distance {Vector3.Distance(ball, head0.Value):F3} yd");

            game.Swing.Synthetic.Backswing(true);
            yield return new WaitForSecondsRealtime(0.9f);
            Assert.IsNotNull(GameCapture.Save($"{Dir}/2-top.png"));
            game.Swing.Synthetic.Backswing(false);
            // the club's path to the ball: its closest approach
            float closest = float.MaxValue; Vector3 at = default; float sway = 0;
            float until = Time.realtimeSinceStartup + 1.2f; int shots = 0;
            while (Time.realtimeSinceStartup < until)
            {
                yield return null;
                sway = Mathf.Max(sway, golfer.HairSway);
                if (golfer.ClubHeadWorld() is Vector3 h)
                {
                    float d = Vector3.Distance(ball, h);
                    if (d < closest) { closest = d; at = h; }
                }
                if (shots == 0 && game.Current == GolfGame.State.Flight) { shots++; Assert.IsNotNull(GameCapture.Save($"{Dir}/3-impact.png")); }
            }
            Debug.Log($"HERO impact: closest approach {closest:F3} yd at {at}; hair swung out {sway:F3}");
            Assert.Greater(sway, 0.004f, "the hair moves with the head in the swing");
            yield return WaitFor(() => game.Current is GolfGame.State.Flight or GolfGame.State.Result or GolfGame.State.Aim, 10, "the shot");
            Assert.IsNotNull(GameCapture.Save($"{Dir}/4-through.png"));
            Assert.Less(closest, 0.6f, "the clubhead comes to within 60 cm of the ball");
        }

        /// Every body × haircut × headwear builds, and shows the right parts: the scalp always (the head is
        /// whole, hat or none), the haircut's mesh (none for bald, buzz, waves), the hat over the hair (its
        /// OnHair shape when there is hair to sit on) and the hair tucked in under that hat (its Under_ shape),
        /// never a piece of hair cut away. Each is captured close up from the front and from the side.
        [UnityTest, Timeout(600000)]
        public IEnumerator EveryLookBuilds()
        {
            GolfGame game = null;
            yield return Start(g => game = g);
            var body0 = GolferStyle.Body; int cut0 = GolferStyle.Haircut, wear0 = GolferStyle.Headwear, skin0 = GolferStyle.SkinTone, tone0 = GolferStyle.HairTone;
            try
            {
                game.OpenGolferPicker();
                yield return new WaitForSecondsRealtime(0.5f);
                var buttons = GameObject.Find("Tab HEADWEAR").GetComponent<HoldButton>();
                buttons.Pressed();                        // in to the head
                yield return new WaitForSecondsRealtime(1.0f);
                var locker = Object.FindFirstObjectByType<Hud>().Locker;
                int n = 0;
                foreach (var female in new[] { false, true })
                    for (int cut = 0; cut < HeroGolfer.HaircutNames.Length; cut++)
                        for (int wear = 0; wear < HeroGolfer.HeadwearNames.Length; wear++)
                        {
                            GolferStyle.Body = female ? GolferStyle.BodyKind.Female : GolferStyle.BodyKind.Male;
                            GolferStyle.Haircut = cut; GolferStyle.Headwear = wear;
                            GolferStyle.SkinTone = (cut + wear) % GolferStyle.SkinTones.Length; GolferStyle.HairTone = (cut * 2 + wear) % GolferStyle.HairColors.Length;
                            game.RestyleGolfer();
                            yield return null;
                            var g = Golfer();
                            Assert.IsTrue(g.IsHero);
                            CheckParts(g.Hero, cut, wear);
                            string name = $"{(female ? "f" : "m")}-{HeroGolfer.HaircutNames[cut]}-{HeroGolfer.HeadwearNames[wear]}";
                            yield return new WaitForSecondsRealtime(0.15f);
                            Assert.IsNotNull(GameCapture.Save($"{Dir}/looks/{name}.png"));
                            locker.Spin(-240f);               // a quarter turn: the side of the head
                            yield return new WaitForSecondsRealtime(0.15f);
                            Assert.IsNotNull(GameCapture.Save($"{Dir}/looks/{name}-side.png"));
                            locker.Spin(240f); locker.SpinDone();
                            n++;
                        }
                Debug.Log($"HERO looks built: {n}");
            }
            finally
            {
                GolferStyle.Body = body0; GolferStyle.Haircut = cut0; GolferStyle.Headwear = wear0; GolferStyle.SkinTone = skin0; GolferStyle.HairTone = tone0;
            }
        }

        static void CheckParts(HeroGolfer hero, int cut, int wear)
        {
            string look = $"{HeroGolfer.HaircutNames[cut]} + {HeroGolfer.HeadwearNames[wear]}";
            var scalp = hero.Part("Head_Scalp");
            Assert.IsNotNull(scalp, "the scalp is in the build");
            Assert.IsTrue(scalp.enabled, $"{look}: the scalp is on");
            bool bare = cut is (int)HeroGolfer.Haircut.Bald or (int)HeroGolfer.Haircut.Buzz or (int)HeroGolfer.Haircut.Waves;
            var hairs = hero.Parts.Where(r => r.name.StartsWith("Hair_") && r.enabled).ToList();
            Assert.AreEqual(bare ? 0 : 1, hairs.Count, $"{look}: {(bare ? "no hair mesh" : "one haircut")} ({string.Join(", ", hairs.Select(h => h.name))})");
            var hats = hero.Parts.Where(r => r.name.StartsWith("Hat_") && r.enabled).ToList();
            Assert.AreEqual(wear == 0 ? 0 : 1, hats.Count, $"{look}: the hat");
            if (wear == 0) return;
            string hatName = HeroGolfer.HeadwearNames[wear];
            Assert.AreEqual("Hat_" + hatName, hats[0].name);
            foreach (var cutName in new[] { "Hair_Default", "Hair_Ponytail", "Hair_Bob", "Hair_Long", "Hair_Curly" })
                Assert.AreEqual(!bare && hairs[0].name == cutName ? 100f : 0f, Weight(hats[0], "OnHair_" + cutName), $"{look}: the hat sits on {cutName}'s hair");
            if (!bare) Assert.AreEqual(100f, Weight(hairs[0], "Under_" + hatName), $"{look}: the hair tucked in under the hat");
        }

        static float Weight(SkinnedMeshRenderer r, string shape)
        {
            var m = r.sharedMesh;
            for (int i = 0; i < m.blendShapeCount; i++)
                if (m.GetBlendShapeName(i).EndsWith(shape)) return r.GetBlendShapeWeight(i);
            Assert.Fail($"{r.name} has no {shape} shape");
            return 0;
        }
    }
}
