using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GolfArcade.Game;
using GolfArcade.Profile;
using GolfArcade.Shot;
using GolfArcade.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GolfArcade.PlayTests
{
    /// The golfer (Adnan's match hero, boy and girl): it builds, stands at the ball, swings a clip per
    /// club, meets the ball, and every look (girl/boy, haircut, skin and kit colours) builds and shows. Frames to
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
            // the club's path to the ball: its closest approach (no screenshots in this loop: one takes a third of a second, which is a frame that skips the ball)
            float closest = float.MaxValue; Vector3 at = default;
            float until = Time.realtimeSinceStartup + 1.2f;
            var path = new System.Text.StringBuilder(); float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup < until)
            {
                yield return null;
                if (golfer.ClubHeadWorld() is Vector3 h)
                {
                    float d = Vector3.Distance(ball, h);
                    if (d < closest) { closest = d; at = h; }
                    path.Append($"[{Time.realtimeSinceStartup - t0:F2}s {game.Current} d={d:F2}] ");
                }
            }
            Debug.Log($"HERO impact: closest approach {closest:F3} yd at {at}");
            Debug.Log("HERO path: " + path);
            yield return WaitFor(() => game.Current is GolfGame.State.Flight or GolfGame.State.Result or GolfGame.State.Aim, 10, "the shot");
            Assert.IsNotNull(GameCapture.Save($"{Dir}/4-through.png"));
            Assert.Less(closest, 0.6f, "the clubhead comes to within 60 cm of the ball");
        }

        /// The same swing in slow motion, boy and girl, a frame every moment from the top through the ball to the finish
        /// (Library/Captures/hero/slow-<m|f>-NN.png).
        [UnityTest, Timeout(480000)]
        public IEnumerator TheSwingInSlowMotion()
        {
            GolfGame game = null;
            yield return Start(g => game = g);
            var look0 = JsonUtility.ToJson(GolferStyle.Current);
            try
            {
                foreach (var female in new[] { false, true })
                {
                    GolferStyle.Body = female ? GolferStyle.BodyKind.Female : GolferStyle.BodyKind.Male;
                    game.ChooseHoles(0); game.Play();
                    yield return null;
                    game.JumpToHole(7);
                    yield return WaitFor(() => game.Current == GolfGame.State.Aim, 45, "the tee");
                    yield return WaitFor(() => game.Swing.Phase == Swing.SwingPhase.Address, 5, "address");
                    game.RestyleGolfer();
                    yield return new WaitForSecondsRealtime(0.6f);
                    Assert.IsNotNull(GameCapture.Save($"{Dir}/slow-{(female ? "f" : "m")}-addr.png"));
                    game.Swing.Synthetic.Backswing(true);
                    yield return new WaitForSecondsRealtime(1.2f);
                    game.Swing.Synthetic.Backswing(false);
                    Time.timeScale = 0.08f;
                    try
                    {
                        for (int i = 0; i < 12; i++)
                        {
                            yield return new WaitForSecondsRealtime(0.25f);
                            Assert.IsNotNull(GameCapture.Save($"{Dir}/slow-{(female ? "f" : "m")}-{i:D2}.png"));
                        }
                    }
                    finally { Time.timeScale = 1f; }
                    yield return WaitFor(() => game.Current is GolfGame.State.Result or GolfGame.State.Aim, 30, "the shot to end");
                }
            }
            finally { Time.timeScale = 1f; GolferStyle.Edit(l => JsonUtility.FromJsonOverwrite(look0, l)); }
        }

        /// Every club's swing on the golfer, straight on the figure (no ball): address, the top, then slow motion through the ball
        /// (Library/Captures/hero/club-<label>-*.png). Each starts with the club at the ball.
        [UnityTest, Timeout(480000)]
        public IEnumerator EveryClubSwings()
        {
            GolfGame game = null;
            yield return Start(g => game = g);
            game.ChooseHoles(0); game.Play();
            yield return null;
            game.JumpToHole(7);
            yield return WaitFor(() => game.Current == GolfGame.State.Aim, 45, "the tee");
            yield return WaitFor(() => game.Swing.Phase == Swing.SwingPhase.Address, 5, "address");
            var golfer = Golfer();
            foreach (var (club, shortShot, label) in new[] { (GolfClub.Iron, false, "iron"), (GolfClub.Wedge, false, "wedge-half"), (GolfClub.Wedge, true, "chip"), (GolfClub.Putter, false, "putt") })
            {
                golfer.SetClub(club, shortShot);
                golfer.Settle();
                yield return new WaitForSecondsRealtime(0.4f);
                Assert.IsNotNull(GameCapture.Save($"{Dir}/club-{label}-addr.png"));
                var head = golfer.ClubHeadWorld(); Assert.IsTrue(head.HasValue, $"{label}: a club in hand");
                Assert.Less(Vector3.Distance(game.BallPosition, head.Value), 0.45f, $"{label}: the club starts at the ball ({Vector3.Distance(game.BallPosition, head.Value):F2} yd)");
                golfer.ShowLoad(1f);
                yield return new WaitForSecondsRealtime(1.2f);
                Assert.IsNotNull(GameCapture.Save($"{Dir}/club-{label}-top.png"));
                golfer.Strike();
                Time.timeScale = 0.1f;
                float closest = float.MaxValue;
                try
                {
                    for (int i = 0; i < 8; i++)
                    {
                        yield return new WaitForSecondsRealtime(0.22f);
                        if (golfer.ClubHeadWorld() is Vector3 h) closest = Mathf.Min(closest, Vector3.Distance(game.BallPosition, h));
                        Assert.IsNotNull(GameCapture.Save($"{Dir}/club-{label}-{i}.png"));
                    }
                }
                finally { Time.timeScale = 1f; }
                golfer.Settle();
                yield return new WaitForSecondsRealtime(0.3f);
            }
        }

        /// Every body × haircut builds, in several skin tones, hair colours and kit colours, and shows the right parts (CheckParts).
        /// Each is captured close up.
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
                GameObject.Find("Tab HAIR").GetComponent<HoldButton>().Pressed();      // in to the head
                yield return new WaitForSecondsRealtime(1.0f);
                int n = 0;
                int cuts = HeroGolfer.HaircutNames.Length;
                foreach (var female in new[] { false, true })
                    for (int k = 0; k < cuts * 3; k++)
                    {
                        int cut = k % cuts;
                        GolferStyle.Body = female ? GolferStyle.BodyKind.Female : GolferStyle.BodyKind.Male;
                        GolferStyle.Haircut = cut;
                        GolferStyle.SkinTone = k % GolferStyle.SkinTones.Length; GolferStyle.HairTone = (k * 2 + 1) % GolferStyle.HairColors.Length;
                        GolferStyle.Kit = k % GolferStyle.KitColors.Length; GolferStyle.Shirt = (k + 1) % GolferStyle.ShirtColors.Length;
                        game.RestyleGolfer();
                        yield return null;
                        var g = Golfer();
                        Assert.IsTrue(g.IsHero);
                        CheckParts(g.Hero, cut);
                        string name = $"{(female ? "f" : "m")}-{k:D2}-{HeroGolfer.HaircutNames[cut]}-skin{GolferStyle.SkinTone}";
                        yield return new WaitForSecondsRealtime(0.1f);
                        Assert.IsNotNull(GameCapture.Save($"{Dir}/looks/{name}.png"));
                        n++;
                    }
                Debug.Log($"HERO looks built: {n}");
            }
            finally { GolferStyle.Edit(l => JsonUtility.FromJsonOverwrite(look0, l)); }
        }

        /// Every haircut on both bodies seen from all the way round (every 45 degrees), up close on the HAIR tab, and the whole golfer from the same
        /// eight sides in the long cuts (the hair on the shoulders): frames in Library/Captures/hero/spin, for the eyes that look for bald spots and overlaps.
        [UnityTest, Timeout(900000)]
        public IEnumerator EveryHaircutFromEverySide()
        {
            GolfGame game = null;
            yield return Start(g => game = g);
            var look0 = JsonUtility.ToJson(GolferStyle.Current);
            try
            {
                game.OpenGolferPicker();
                yield return new WaitForSecondsRealtime(0.5f);
                foreach (var close in new[] { true, false })
                {
                    if (close) GameObject.Find("Tab HAIR").GetComponent<HoldButton>().Pressed();      // in to the head
                    else GameObject.Find("Tab OUTFIT").GetComponent<HoldButton>().Pressed();          // the whole golfer
                    yield return new WaitForSecondsRealtime(1.2f);
                    foreach (var female in new[] { false, true })
                        for (int cut = 0; cut < HeroGolfer.HaircutNames.Length; cut++)
                        {
                            if (!close && HeroGolfer.HaircutNames[cut] is not ("Long" or "Ponytail" or "Braid" or "Afro")) continue;
                            GolferStyle.Body = female ? GolferStyle.BodyKind.Female : GolferStyle.BodyKind.Male;
                            GolferStyle.Haircut = cut; GolferStyle.SkinTone = 2; GolferStyle.HairTone = 1;
                            game.RestyleGolfer();
                            for (int a = 0; a < 8; a++)
                            {
                                game.TurnPickerGolfer(a * 45f);
                                yield return null; yield return null;
                                string name = $"{(close ? "head" : "body")}-{(female ? "f" : "m")}-{HeroGolfer.HaircutNames[cut]}-{a * 45:D3}";
                                Assert.IsNotNull(GameCapture.Save($"{Dir}/spin/{name}.png"));
                            }
                        }
                }
            }
            finally { GolferStyle.Edit(l => JsonUtility.FromJsonOverwrite(look0, l)); }
        }

        /// Under every cut but the afro lies a soft scalp (the Hair_Scalp material): the hair colour under the hair, fading into the skin past its edge (a hairline, a taper:
        /// no hard line). It is the vertex colour's R that does it, 1 under the hair and 0 at the skin, so the mesh must arrive with vertex colours and a scalp material in scalp mode.
        [UnityTest, Timeout(240000)]
        public IEnumerator EveryCutsScalpFadesIntoTheSkin()
        {
            GolfGame game = null;
            yield return Start(g => game = g);
            var look0 = JsonUtility.ToJson(GolferStyle.Current);
            try
            {
                game.OpenGolferPicker();
                yield return new WaitForSecondsRealtime(0.4f);
                foreach (var female in new[] { false, true })
                    for (int cut = 0; cut < HeroGolfer.HaircutNames.Length; cut++)
                    {
                        if (cut == (int)HeroGolfer.Haircut.Bald || cut == (int)HeroGolfer.Haircut.Afro) continue;
                        GolferStyle.Body = female ? GolferStyle.BodyKind.Female : GolferStyle.BodyKind.Male;
                        GolferStyle.Haircut = cut;
                        game.RestyleGolfer();
                        yield return null;
                        string name = HeroGolfer.HaircutNames[cut];
                        var part = Golfer().Hero.Part("Hair_" + name);
                        Assert.IsNotNull(part, $"Hair_{name}");
                        var mesh = part.sharedMesh;
                        // (the meshes are not readable on purpose: the phone keeps no second copy: so the colour channel is asked for, not read)
                        Assert.IsTrue(mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.Color), $"{name}: the hair mesh carries vertex colours");
                        int scalpSub = System.Array.FindIndex(part.sharedMaterials, m => m && m.name.StartsWith("Hair_Scalp"));
                        Assert.GreaterOrEqual(scalpSub, 0, $"{name}: a Hair_Scalp material");
                        Assert.Greater(mesh.GetSubMesh(scalpSub).indexCount, 0, $"{name}: the scalp has triangles");
                        Assert.AreEqual(1f, part.sharedMaterials[scalpSub].GetFloat("_UseScalp"), $"{name}: drawn in scalp mode");
                    }
            }
            finally { GolferStyle.Edit(l => JsonUtility.FromJsonOverwrite(look0, l)); }
        }

        /// The picker: every tab it has draws (a frame of each to Library/Captures/locker), a tile picks its style, and the tabs the
        /// golfer has nothing for (glasses, hats) are not there.
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
                foreach (var (tab, kind) in new[] { ("BODY", Locker.Tab.Body), ("HAIR", Locker.Tab.Hair), ("FACE", Locker.Tab.Face), ("HEADWEAR", Locker.Tab.Headwear), ("OUTFIT", Locker.Tab.Outfit), ("GEAR", Locker.Tab.Gear) })
                {
                    var button = GameObject.Find("Tab " + tab);
                    if (!Locker.TabAvailable(kind)) { Assert.IsNull(button, $"no {tab} tab while there is nothing on it"); continue; }
                    Assert.IsNotNull(button, $"the {tab} tab");
                    button.GetComponent<HoldButton>().Pressed();
                    yield return new WaitForSecondsRealtime(1.0f);
                    Assert.IsNotNull(GameCapture.Save($"Library/Captures/locker/{tab.ToLowerInvariant()}.png"));
                }
                GameObject.Find("Tab HAIR").GetComponent<HoldButton>().Pressed();
                yield return null;
                var tile = GameObject.Find("Tile 1 hair_bald");
                Assert.IsNotNull(tile, "the bald head's tile");
                tile.GetComponent<Locker.Choice>().Clicked();
                yield return null;
                Assert.AreEqual((int)HeroGolfer.Haircut.Bald, GolferStyle.Haircut, "the tile picked the haircut");
                yield return new WaitForSecondsRealtime(0.5f);
                Assert.IsNotNull(GameCapture.Save("Library/Captures/locker/hair-picked.png"));
                Assert.IsFalse(Golfer().Hero.Part("Hair_Classic").enabled, "bald: the hair is off");
            }
            finally { GolferStyle.Edit(l => JsonUtility.FromJsonOverwrite(look0, l)); }
        }

        /// The body, the painted face and the kit are always on; his hair is on unless the head is bald. (Nothing is worn that the golfer has not got: no hats, no glasses.)
        static void CheckParts(HeroGolfer hero, int cut)
        {
            string look = HeroGolfer.HaircutNames[cut];
            foreach (var slot in new[] { "Body", "Face", "Kit_Top", "Kit_Bottom", "Kit_Sock_L", "Kit_Sock_R", "Kit_Shoe_L", "Kit_Shoe_R" })
            {
                var part = hero.Part(slot);
                Assert.IsNotNull(part, $"{slot} is in the build");
                Assert.IsTrue(part.enabled, $"{look}: {slot} is on");
            }
            bool bald = cut == (int)HeroGolfer.Haircut.Bald;
            var hairs = hero.Parts.Where(r => r.name.StartsWith("Hair_") && r.enabled).ToList();
            Assert.AreEqual(bald ? 0 : 1, hairs.Count, $"{look}: {(bald ? "no hair mesh" : "one haircut")} ({string.Join(", ", hairs.Select(h => h.name))})");
            if (!bald) Assert.AreEqual("Hair_" + look, hairs[0].name, $"{look}: the haircut picked");
            if (!bald && cut != (int)HeroGolfer.Haircut.Classic && cut != (int)HeroGolfer.Haircut.Afro)      // (the afro is built from curls, not cards)
            {
                // a strand-card cut: its card drawn by the card shader with its texture, and the soft scalp under it by the hero shader in scalp mode
                var mats = hairs[0].sharedMaterials;
                var card = mats.FirstOrDefault(m => m && m.name.StartsWith("HairCard_"));
                Assert.IsNotNull(card, $"{look}: its card material");
                Assert.AreEqual("GolfArcade/HeroHairCard", card.shader.name, $"{look}: drawn as hair cards");
                Assert.IsNotNull(card.mainTexture, $"{look}: its strand texture");
                var scalp = mats.FirstOrDefault(m => m && m.name.StartsWith("Hair_Scalp"));
                Assert.IsNotNull(scalp, $"{look}: the soft scalp under it");
                Assert.AreEqual(1f, scalp.GetFloat("_UseScalp"), $"{look}: the scalp fades into the skin");
            }
        }

        /// The style lists are what a saved look is clamped to: they must agree, and every style in them must be a part in the build
        /// (so a new style cannot be listed before it is exported, or the reverse).
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
            foreach (var female in new[] { false, true })
            {
                GolferStyle.Body = female ? GolferStyle.BodyKind.Female : GolferStyle.BodyKind.Male;
                game.RestyleGolfer();
                yield return null;
                var hero = Golfer().Hero;
                Assert.IsNotNull(hero);
                for (int i = 0; i < HeroGolfer.HaircutNames.Length; i++)
                    if (i != (int)HeroGolfer.Haircut.Bald) Assert.IsNotNull(hero.Part("Hair_" + HeroGolfer.HaircutNames[i]), $"{(female ? "her" : "his")} Hair_{HeroGolfer.HaircutNames[i]} is in the build");
                for (int i = 1; i < HeroGolfer.HeadwearNames.Length; i++) Assert.IsNotNull(hero.Part("Hat_" + HeroGolfer.HeadwearNames[i]), $"Hat_{HeroGolfer.HeadwearNames[i]} is in the build");
                for (int i = 1; i < HeroGolfer.GlassesNames.Length; i++) Assert.IsNotNull(hero.Part("Glasses_" + HeroGolfer.GlassesNames[i]));
                for (int i = 1; i < HeroGolfer.FacialNames.Length; i++) Assert.IsNotNull(hero.Part("Face_" + HeroGolfer.FacialNames[i]));
            }
            // and each style has its picture for the locker's tile (a haircut one for the boy and one for the girl)
            foreach (var n in HeroGolfer.HaircutNames)
                foreach (var sex in new[] { "_m", "_f" })
                    Assert.IsNotNull(Resources.Load<Texture2D>("UI/Look/hair_" + n.ToLowerInvariant() + sex), $"UI/Look/hair_{n.ToLowerInvariant()}{sex} is a picture");
            foreach (var (prefix, names) in new[] { ("hat_", HeroGolfer.HeadwearNames), ("glasses_", HeroGolfer.GlassesNames), ("facial_", HeroGolfer.FacialNames) })
                for (int i = 1; i < names.Length; i++)
                    Assert.IsNotNull(Resources.Load<Texture2D>("UI/Look/" + prefix + names[i].ToLowerInvariant()), $"UI/Look/{prefix}{names[i].ToLowerInvariant()} is a picture");
            Assert.IsNotNull(Resources.Load<Texture2D>("UI/Look/tab_outfit"), "the outfit tab's picture");
        }
    }
}
