using System.Collections;
using GolfArcade.Game;
using GolfArcade.Profile;
using GolfArcade.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GolfArcade.PlayTests
{
    /// The locker (Adnan's character screen): the golfer big in the top of the screen and a panel of choices
    /// under a row of tabs. Boy or girl, skin, haircut, headwear and outfit colours each change the golfer on
    /// the spot and are remembered; SHUFFLE, sliders, padlocks and LET'S GO. Frames: Library/Captures/review/locker-*.png.
    public class GolferSelectTests
    {
        const string Dir = "Library/Captures/review";

        static void Press(string name)
        {
            var go = GameObject.Find(name);
            Assert.IsTrue(go, $"no {name} on the screen");
            go.GetComponent<HoldButton>().Pressed();
        }

        /// A press and a let-go on a slider's track, at `t` of the way along it (a finger).
        static void Drag(GameObject slider, float t)
        {
            var track = slider.transform.Find("Track").GetComponent<RectTransform>();
            var corners = new Vector3[4];
            track.GetWorldCorners(corners);
            var screen = (Vector2)Vector3.Lerp(corners[0], corners[3], t) + new Vector2(0, (corners[1].y - corners[0].y) / 2);
            var e = new PointerEventData(EventSystem.current) { position = screen };
            ExecuteEvents.Execute(slider, e, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(slider, e, ExecuteEvents.pointerUpHandler);
        }

        static GameObject ActiveSlider(int index)
        {
            var found = new System.Collections.Generic.List<GameObject>();
            foreach (var s in Object.FindObjectsByType<RectTransform>(FindObjectsSortMode.None))
                if (s.name == "Slider" && s.gameObject.activeInHierarchy) found.Add(s.gameObject);
            found.Sort((a, b) => b.transform.position.y.CompareTo(a.transform.position.y));
            return found[index];
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator MakingAGolfer()
        {
            Time.timeScale = 1f;
            yield return SceneManager.LoadSceneAsync("Golf", LoadSceneMode.Single);
            var cam = Camera.main;
            cam.aspect = GameCapture.PhoneWidth / (float)GameCapture.PhoneHeight;
            var game = Object.FindFirstObjectByType<GolfGame>();
            game.InstantReplays = false;
            yield return new WaitForSecondsRealtime(0.5f);
            var look0 = GolferStyle.Current.Clone();
            var profile = ProfileStore.Active;
            var profileLook0 = profile.LookOrMigrated().Clone();
            var stats0 = profile.Stats.RoundsPlayed;
            try
            {
                profile.Stats.RoundsPlayed = 0;   // (no mixer yet)
                GolferStyle.Edit(l => { l.Body = 0; l.Skin = ""; l.Haircut = -1; l.Hair = ""; l.Headwear = 1; l.Shirt = ""; l.Shorts = ""; l.Shoes = ""; l.Hat = ""; });
                game.OpenGolferPicker();
                yield return new WaitForSecondsRealtime(1.5f);
                Assert.IsTrue(GameObject.Find("Locker"), "the locker is up");
                Assert.AreEqual(GolfGame.State.Golfer, game.Current);
                Assert.IsTrue(GameObject.Find("Golfer").GetComponent<GolferView>().IsHero, "the golfer is the Hero");
                Assert.IsNotNull(GameCapture.Save($"{Dir}/locker-1-body.png"));

                // BODY: the girl, and a skin tone from the slider
                Press("Card GIRL");
                yield return new WaitForSecondsRealtime(0.3f);
                Assert.AreEqual(GolferStyle.BodyKind.Female, GolferStyle.Body);
                Drag(ActiveSlider(0), 0.85f);
                yield return new WaitForSecondsRealtime(0.3f);
                Assert.AreNotEqual("", GolferStyle.Current.Skin, "the skin slider sets a tone");
                Assert.Greater(GolferStyle.SkinColor.r - GolferStyle.SkinColor.b, 0f);
                var deep = GolferStyle.SkinColor;
                Assert.Less(deep.r + deep.g + deep.b, 1.9f, "a deeper tone from the far end of the slider");
                Assert.IsNotNull(GameCapture.Save($"{Dir}/locker-2-girl-skin.png"));

                // HAIR: the camera comes in to the head; a haircut and a colour
                Press("Tab HAIR");
                yield return new WaitForSecondsRealtime(1.2f);
                Press("Card BOB");
                Assert.AreEqual((int)HeroGolfer.Haircut.Bob, GolferStyle.Haircut);
                Drag(ActiveSlider(0), 0.72f);
                yield return new WaitForSecondsRealtime(0.4f);
                Assert.AreNotEqual("", GolferStyle.Current.Hair);
                Assert.IsNotNull(GameCapture.Save($"{Dir}/locker-3-hair.png"));

                // HEADWEAR: the cap, then none: the hair shows in full without it
                Press("Tab HEADWEAR");
                yield return new WaitForSecondsRealtime(0.5f);
                Press("Card CAP");
                Assert.AreEqual((int)HeroGolfer.Headwear.Cap, GolferStyle.Headwear);
                yield return new WaitForSecondsRealtime(0.3f);
                Assert.IsNotNull(GameCapture.Save($"{Dir}/locker-4-cap.png"));
                Press("Card NONE");
                yield return new WaitForSecondsRealtime(0.3f);
                Assert.AreEqual(0, GolferStyle.Headwear);
                Assert.IsNotNull(GameCapture.Save($"{Dir}/locker-5-bare.png"));

                // OUTFIT: the shorts in a quick pick; a padlock says what it takes; the mixer is earned
                Press("Tab OUTFIT");
                yield return new WaitForSecondsRealtime(1.0f);
                Press("Card SHORTS");
                Press("Swatch 1");
                yield return new WaitForSecondsRealtime(0.3f);
                Assert.AreEqual(1, GolferStyle.Kit, "teal shorts");
                Press("Swatch 4");   // forest: not earned
                Assert.AreEqual(1, GolferStyle.Kit, "a padlocked colour stays shut");
                Drag(ActiveSlider(0), 0.3f);
                Assert.AreEqual(1, GolferStyle.Kit, "and so is the mixer until it is earned");
                profile.Stats.RoundsPlayed = 2;
                Drag(ActiveSlider(0), 0.3f);
                yield return new WaitForSecondsRealtime(0.3f);
                Assert.AreEqual(-1, GolferStyle.IndexOf(GolferStyle.KitColors, GolferStyle.Current.Shorts, -1) , "a mixed colour, once the mixer is earned");
                Assert.IsNotNull(GameCapture.Save($"{Dir}/locker-6-outfit.png"));

                // GEAR
                Press("Tab GEAR");
                yield return new WaitForSecondsRealtime(0.5f);
                Assert.IsNotNull(GameCapture.Save($"{Dir}/locker-7-gear.png"));

                // SHUFFLE gives another look
                var before = GolferStyle.Current.Clone();
                Press("Shuffle");
                yield return new WaitForSecondsRealtime(0.3f);
                Assert.IsFalse(before.SameAs(GolferStyle.Current), "shuffle changes the golfer");
                var chosen = GolferStyle.Current.Clone();

                // LET'S GO: back to the menu, the look kept on the profile and the phone
                Press("Lets go");
                yield return null;
                Assert.AreEqual(GolfGame.State.Menu, game.Current, "LET'S GO goes back to the menu");
                Assert.IsFalse(GameObject.Find("Locker"), "the screen is put away");
                Assert.IsTrue(chosen.SameAs(ProfileStore.Active.Look), "the look is kept on the profile");
                Assert.IsTrue(chosen.SameAs(GolferStyle.Device), "and on the phone");
            }
            finally
            {
                profile.Stats.RoundsPlayed = stats0;
                profile.Look = profileLook0;
                GolferStyle.SaveDevice(look0);
                if (game.Current == GolfGame.State.Golfer) game.CloseGolferPicker();
                ProfileStore.Save();
            }
        }
    }
}
