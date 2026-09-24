using System.Collections;
using GolfArcade.Game;
using GolfArcade.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GolfArcade.PlayTests
{
    /// The golfer select screen: both golfers square on with their faces in the middle of the
    /// head, the arrows switching between them, the kit and shirt swatches recolouring the golfer
    /// and being remembered, LET'S GO back to the menu. Frames go to Library/Captures/review/select-*.png.
    public class GolferSelectTests
    {
        const string Dir = "Library/Captures/review";

        static void Press(string name)
        {
            var go = GameObject.Find(name);
            Assert.IsTrue(go, $"no {name} on the screen");
            go.GetComponent<HoldButton>().Pressed();
        }

        [UnityTest]
        public IEnumerator ChoosingAGolfer()
        {
            Time.timeScale = 1f;
            yield return SceneManager.LoadSceneAsync("Golf", LoadSceneMode.Single);
            var cam = Camera.main;
            cam.aspect = GameCapture.PhoneWidth / (float)GameCapture.PhoneHeight;
            var game = Object.FindFirstObjectByType<GolfGame>();
            game.InstantReplays = false;
            yield return new WaitForSecondsRealtime(0.5f);
            var body0 = GolferStyle.Body; int kit0 = GolferStyle.Kit, shirt0 = GolferStyle.Shirt;
            try
            {
                GolferStyle.Body = GolferStyle.BodyKind.Male; GolferStyle.Kit = 0; GolferStyle.Shirt = 0;
                game.OpenGolferPicker();
                yield return new WaitForSecondsRealtime(1.5f);
                Assert.IsTrue(GameObject.Find("Golfer select"), "the select screen is up");
                Assert.AreEqual(GolfGame.State.Golfer, game.Current);
                Assert.IsNotNull(GameCapture.Save($"{Dir}/select-a-male.png"));

                // the face straight on: the decal in the middle of the picture, left to right
                var decal = FaceDecal();
                var v = cam.WorldToViewportPoint(decal.bounds.center);
                Assert.AreEqual(0.5f, v.x, 0.03f, "the face is in the middle of the screen");

                Press("Next golfer");
                yield return new WaitForSecondsRealtime(1.2f);
                Assert.AreEqual(GolferStyle.BodyKind.Female, GolferStyle.Body, "the arrow switches golfer");
                Assert.IsNotNull(GameCapture.Save($"{Dir}/select-b-female.png"));

                Press("KIT Teal");
                Press("SHIRT Sky");
                yield return new WaitForSecondsRealtime(0.4f);
                Assert.AreEqual(1, GolferStyle.Kit); Assert.AreEqual(3, GolferStyle.Shirt);
                Assert.IsNotNull(GameCapture.Save($"{Dir}/select-c-female-teal.png"));

                Press("Previous golfer");
                Press("KIT Crimson");
                Press("SHIRT White");
                yield return new WaitForSecondsRealtime(1.2f);
                Assert.AreEqual(GolferStyle.BodyKind.Male, GolferStyle.Body);
                Assert.IsNotNull(GameCapture.Save($"{Dir}/select-d-male-crimson.png"));

                Press("Lets go");
                yield return null;
                Assert.AreEqual(GolfGame.State.Menu, game.Current, "LET'S GO goes back to the menu");
                Assert.IsFalse(GameObject.Find("Golfer select"), "the screen is put away");
            }
            finally
            {
                GolferStyle.Body = body0; GolferStyle.Kit = kit0; GolferStyle.Shirt = shirt0;
                if (game.Current == GolfGame.State.Golfer) game.CloseGolferPicker();
            }
        }

        static Renderer FaceDecal()
        {
            foreach (var r in GameObject.Find("Golfer").GetComponentsInChildren<Renderer>())
                foreach (var m in r.sharedMaterials)
                    if (m && m.mainTexture && m.mainTexture.name == "FaceAtlas") return r;
            Assert.Fail("no face decal");
            return null;
        }
    }
}
