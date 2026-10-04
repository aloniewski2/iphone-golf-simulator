using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GolfArcade.Game;
using GolfArcade.Profile;
using GolfArcade.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GolfArcade.PlayTests
{
    /// The Hero (the avatar kit on Adnan's skeleton) as the golfer: it builds, stands at the ball, swings a clip per
    /// club, meets the ball, and every look (girl/boy, haircut, headwear, colours) builds and shows. Frames to
    /// Library/Captures/hero.
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
            float closest = float.MaxValue; Vector3 at = default;
            float until = Time.realtimeSinceStartup + 1.2f; int shots = 0;
            while (Time.realtimeSinceStartup < until)
            {
                yield return null;
                if (golfer.ClubHeadWorld() is Vector3 h)
                {
                    float d = Vector3.Distance(ball, h);
                    if (d < closest) { closest = d; at = h; }
                }
                if (shots == 0 && game.Current == GolfGame.State.Flight) { shots++; Assert.IsNotNull(GameCapture.Save($"{Dir}/3-impact.png")); }
            }
            Debug.Log($"HERO impact: closest approach {closest:F3} yd at {at}");
            yield return WaitFor(() => game.Current is GolfGame.State.Flight or GolfGame.State.Result or GolfGame.State.Aim, 10, "the shot");
            Assert.IsNotNull(GameCapture.Save($"{Dir}/4-through.png"));
            Assert.Less(closest, 0.6f, "the clubhead comes to within 60 cm of the ball");
        }

        /// Every body × haircut × headwear builds, and shows the right parts (CheckParts). Each is captured close up
        /// from the front and from the side.
        [UnityTest, Timeout(600000)]
        public IEnumerator EveryLookBuilds()
        {
            GolfGame game = null;
            yield return Start(g => game = g);
            var look0 = JsonUtility.ToJson(GolferStyle.Current);
            try
            {
                game.OpenGolferPicker();
                yield return new WaitForSecondsRealtime(0.5f);
                var buttons = GameObject.Find("Tab HEADWEAR").GetComponent<HoldButton>();
                buttons.Pressed();                        // in to the head
                yield return new WaitForSecondsRealtime(1.0f);
                int n = 0;
                // every haircut and every hat at least three times over, in different pairs (not every pairing: 15 x 15 x 2 is too many)
                int cuts = HeroGolfer.HaircutNames.Length, wears = HeroGolfer.HeadwearNames.Length;
                foreach (var female in new[] { false, true })
                    for (int k = 0; k < Mathf.Max(cuts, wears) * 3; k++)
                    {
                        int cut = k % cuts, wear = (k * 7 + k / cuts) % wears;
                        {
                            GolferStyle.Body = female ? GolferStyle.BodyKind.Female : GolferStyle.BodyKind.Male;
                            GolferStyle.Haircut = cut; GolferStyle.Headwear = wear;
                            GolferStyle.Glasses = k % 3 == 0 ? 1 + k % (HeroGolfer.GlassesNames.Length - 1) : 0; GolferStyle.Facial = k % 4 == 0 ? 1 + k % (HeroGolfer.FacialNames.Length - 1) : 0;
                            GolferStyle.Top = k % HeroGolfer.TopNames.Length; GolferStyle.Bottom = k % HeroGolfer.BottomNames.Length;
                            GolferStyle.SkinTone = (cut + wear) % GolferStyle.SkinTones.Length; GolferStyle.HairTone = (cut * 2 + wear) % GolferStyle.HairColors.Length;
                            game.RestyleGolfer();
                            yield return null;
                            var g = Golfer();
                            Assert.IsTrue(g.IsHero);
                            CheckParts(g.Hero, cut, wear);
                            string name = $"{(female ? "f" : "m")}-{k:D2}-{HeroGolfer.HaircutNames[cut]}-{HeroGolfer.HeadwearNames[wear]}";
                            yield return new WaitForSecondsRealtime(0.1f);
                            Assert.IsNotNull(GameCapture.Save($"{Dir}/looks/{name}.png"));
                            n++;
                        }
                    }
                Debug.Log($"HERO looks built: {n}");
            }
            finally { GolferStyle.Edit(l => JsonUtility.FromJsonOverwrite(look0, l)); }
        }

        /// The picker: every tab draws (a frame of each to Library/Captures/locker), and a tile picks its style.
        [UnityTest, Timeout(240000)]
        public IEnumerator TheLockerShowsEveryTabAndATilePicksAStyle()
        {
            GolfGame game = null;
            yield return Start(g => game = g);
            var look0 = JsonUtility.ToJson(GolferStyle.Current);
            try
            {
                game.OpenGolferPicker();
                yield return new WaitForSecondsRealtime(0.8f);
                foreach (var tab in new[] { "BODY", "HAIR", "FACE", "HEADWEAR", "OUTFIT", "GEAR" })
                {
                    var button = GameObject.Find("Tab " + tab);
                    Assert.IsNotNull(button, $"the {tab} tab");
                    button.GetComponent<HoldButton>().Pressed();
                    yield return new WaitForSecondsRealtime(1.0f);
                    Assert.IsNotNull(GameCapture.Save($"Library/Captures/locker/{tab.ToLowerInvariant()}.png"));
                }
                GameObject.Find("Tab HAIR").GetComponent<HoldButton>().Pressed();
                yield return null;
                var tile = GameObject.Find("Tile 8 hair_afro");
                Assert.IsNotNull(tile, "the Afro's tile");
                tile.GetComponent<Locker.Choice>().Clicked();
                yield return null;
                Assert.AreEqual((int)HeroGolfer.Haircut.Afro, GolferStyle.Haircut, "the tile picked the haircut");
                GameObject.Find("Tab FACE").GetComponent<HoldButton>().Pressed();
                yield return null;
                GameObject.Find("Tile 4 glasses_aviator").GetComponent<Locker.Choice>().Clicked();
                GameObject.Find("Tile 3 facial_goatee").GetComponent<Locker.Choice>().Clicked();
                yield return null;
                Assert.AreEqual(4, GolferStyle.Glasses); Assert.AreEqual(3, GolferStyle.Facial);
                yield return new WaitForSecondsRealtime(0.5f);
                Assert.IsNotNull(GameCapture.Save("Library/Captures/locker/face-picked.png"));
            }
            finally { GolferStyle.Edit(l => JsonUtility.FromJsonOverwrite(look0, l)); }
        }

        /// The head and the face are always on; the haircut is one mesh (none when bald), and the version with its crown
        /// pressed down when a hat is on; the hat is one mesh over it; no glasses unless chosen.
        static void CheckParts(HeroGolfer hero, int cut, int wear)
        {
            string look = $"{HeroGolfer.HaircutNames[cut]} + {HeroGolfer.HeadwearNames[wear]}";
            foreach (var slot in new[] { "Hero_Head", "Hero_Face", "Hero_BodySkin", "Shoes_Golf" })
            {
                var part = hero.Part(slot);
                Assert.IsNotNull(part, $"{slot} is in the build");
                Assert.IsTrue(part.enabled, $"{look}: {slot} is on");
            }
            bool bald = cut == (int)HeroGolfer.Haircut.Bald;
            var hairs = hero.Parts.Where(r => r.name.StartsWith("Hair_") && r.enabled).ToList();
            Assert.AreEqual(bald ? 0 : 1, hairs.Count, $"{look}: {(bald ? "no hair mesh" : "one haircut")} ({string.Join(", ", hairs.Select(h => h.name))})");
            if (!bald) Assert.AreEqual("Hair_" + HeroGolfer.HaircutNames[cut] + (wear == 0 ? "" : "_Hat"), hairs[0].name, $"{look}: the haircut, pressed down under a hat");
            var hats = hero.Parts.Where(r => r.name.StartsWith("Hat_") && r.enabled).ToList();
            Assert.AreEqual(wear == 0 ? 0 : 1, hats.Count, $"{look}: the hat");
            if (wear != 0) Assert.AreEqual("Hat_" + HeroGolfer.HeadwearNames[wear], hats[0].name);
            int glasses = GolferStyle.Glasses, facial = GolferStyle.Facial;
            Assert.AreEqual(glasses == 0 ? 0 : 1, hero.Parts.Count(r => r.name.StartsWith("Glasses_") && r.enabled), $"{look}: the glasses");
            Assert.AreEqual(facial == 0 ? 0 : 1, hero.Parts.Count(r => r.name.StartsWith("Face_") && r.enabled), $"{look}: the facial hair");
            Assert.AreEqual(1, hero.Parts.Count(r => r.name.StartsWith("Top_") && r.enabled), $"{look}: one top");
            Assert.AreEqual(1, hero.Parts.Count(r => r.name.StartsWith("Bottom_") && r.enabled), $"{look}: one pair of bottoms");
        }

        /// The style lists are the kit's catalog and what a saved look is clamped to: they must agree, and every name in
        /// them must be a part in the build (so a new style cannot be listed before it is exported, or the reverse).
        [UnityTest, Timeout(120000)]
        public IEnumerator EveryStyleIsAPartInTheBuild()
        {
            GolfGame game = null;
            yield return Start(g => game = g);
            Assert.AreEqual(CharacterLook.Haircuts, HeroGolfer.HaircutNames.Length);
            Assert.AreEqual(CharacterLook.Headwears, HeroGolfer.HeadwearNames.Length);
            Assert.AreEqual(CharacterLook.GlassesStyles, HeroGolfer.GlassesNames.Length);
            Assert.AreEqual(CharacterLook.FacialStyles, HeroGolfer.FacialNames.Length);
            Assert.AreEqual(CharacterLook.Tops, HeroGolfer.TopNames.Length);
            Assert.AreEqual(CharacterLook.Bottoms, HeroGolfer.BottomNames.Length);
            var hero = Golfer().Hero;
            Assert.IsNotNull(hero);
            void Has(string prefix, string[] names, bool skipFirst, string skip = null)
            {
                for (int i = skipFirst ? 1 : 0; i < names.Length; i++)
                {
                    if (names[i] == skip) continue;
                    Assert.IsNotNull(hero.Part(prefix + names[i]), $"{prefix}{names[i]} is in the build");
                    if (prefix == "Hair_") Assert.IsNotNull(hero.Part(prefix + names[i] + "_Hat"), $"{prefix}{names[i]}_Hat is in the build");
                }
            }
            Has("Hair_", HeroGolfer.HaircutNames, false, "Bald"); Has("Hat_", HeroGolfer.HeadwearNames, true);
            Has("Glasses_", HeroGolfer.GlassesNames, true); Has("Face_", HeroGolfer.FacialNames, true);
            Has("Top_", HeroGolfer.TopNames, false); Has("Bottom_", HeroGolfer.BottomNames, false);
            // and each has its picture for the locker's tile
            foreach (var (prefix, names, none) in new[] { ("hair_", HeroGolfer.HaircutNames, "Bald"), ("hat_", HeroGolfer.HeadwearNames, "None"), ("glasses_", HeroGolfer.GlassesNames, "None"),
                                                          ("facial_", HeroGolfer.FacialNames, "None"), ("top_", HeroGolfer.TopNames, ""), ("bottom_", HeroGolfer.BottomNames, "") })
                foreach (var n in names)
                {
                    if (n == none && n != "Bald") continue;
                    Assert.IsNotNull(Resources.Load<Texture2D>("UI/Look/" + prefix + n.ToLowerInvariant()), $"UI/Look/{prefix}{n.ToLowerInvariant()} is a picture");
                }
        }
    }
}
