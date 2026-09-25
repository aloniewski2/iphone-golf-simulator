using System.Collections;
using GolfArcade.Game;
using GolfArcade.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace GolfArcade.PlayTests
{
    /// The first screen (option C, "On the Tee"): the golfer at address on the first tee under
    /// the logo, PLAY between COURSE and GOLFER; COURSE opens the course screen, the hole circling
    /// with arrows between the holes, and SELECT picks one — the home screen then stands on its
    /// tee. Frames go to Library/Captures/review/home-*.png.
    public class HomeScreenTests
    {
        const string Dir = "Library/Captures/review";

        static void Press(string name)
        {
            var go = GameObject.Find(name);
            Assert.IsTrue(go, $"no {name} on the screen");
            go.GetComponent<HoldButton>().Pressed();
        }

        [UnityTest]
        public IEnumerator HomeAndTheCourseScreen()
        {
            Time.timeScale = 1f;
            yield return SceneManager.LoadSceneAsync("Golf", LoadSceneMode.Single);
            var cam = Camera.main;
            cam.aspect = GameCapture.PhoneWidth / (float)GameCapture.PhoneHeight;
            var game = Object.FindFirstObjectByType<GolfGame>();
            int holes = game.ChosenHoles;
            try
            {
                game.ChooseHoles(0);
                yield return new WaitForSecondsRealtime(1.5f);
                Assert.AreEqual(GolfGame.State.Menu, game.Current);
                foreach (var name in new[] { "Menu", "Play", "Course", "Golfer", "Big screen", "Logo" })
                    Assert.IsTrue(GameObject.Find(name), $"the home screen has {name}");
                Assert.IsTrue(GameObject.Find("Golfer model"), "the golfer is on the tee");
                Assert.IsTrue(GameObject.Find("Avatar").GetComponent<RawImage>().enabled, "the golfer's face is in their button");
                Assert.IsNotNull(GameCapture.Save($"{Dir}/home-a.png"));

                Press("Course");
                yield return new WaitForSecondsRealtime(2.5f);
                Assert.IsTrue(GameObject.Find("Course select"), "COURSE opens the course screen");
                Assert.IsFalse(GameObject.Find("Menu"), "the home screen is put away");
                var first = game.BrowsedHole;
                Assert.IsNotNull(first);
                Assert.IsNotNull(GameCapture.Save($"{Dir}/home-b-course.png"));
                var camAt = cam.transform.position;
                yield return new WaitForSecondsRealtime(1.5f);
                Assert.Greater(Vector3.Distance(camAt, cam.transform.position), 1f, "the camera circles the hole");

                Press("Next hole");
                yield return new WaitForSecondsRealtime(2.5f);
                var second = game.BrowsedHole;
                Assert.AreNotEqual(first.Number, second.Number, "the arrow goes to the next hole");
                Assert.IsNotNull(GameCapture.Save($"{Dir}/home-c-course-next.png"));
                Press("Previous hole"); Press("Next hole");
                yield return new WaitForSecondsRealtime(0.5f);
                for (int i = 0; i < 2; i++) { Press("Next hole"); yield return new WaitForSecondsRealtime(1.8f); }
                Assert.IsNotNull(GameCapture.Save($"{Dir}/home-d-course-far.png"));
                Press("Previous hole"); Press("Previous hole");
                yield return null;
                Assert.AreEqual(second.Number, game.BrowsedHole.Number);

                Press("This hole");
                Press("Select");
                yield return new WaitForSecondsRealtime(1.5f);
                Assert.AreEqual(second.Number, game.ChosenHoles, "SELECT picks the hole on show");
                Assert.IsTrue(GameObject.Find("Menu"), "back on the home screen");
                Assert.AreEqual(second.Number, game.CurrentHole.Number, "standing on its tee");
                Assert.IsNotNull(GameCapture.Save($"{Dir}/home-e-picked.png"));

                // the back arrow keeps the choice
                Press("Course");
                yield return new WaitForSecondsRealtime(0.3f);
                Press("Full round");
                Press("Back");
                yield return null;
                Assert.AreEqual(second.Number, game.ChosenHoles, "back leaves the choice as it was");

                // every hole's tee: the golfer in view, above the dock
                foreach (var h in GolfArcade.Course.Course.Cliffside().Holes)
                {
                    game.ChooseHoles(h.Number);
                    yield return new WaitForSecondsRealtime(1.2f);
                    var feet = cam.WorldToViewportPoint(GameObject.Find("Golfer").transform.position);
                    Assert.IsNotNull(GameCapture.Save($"{Dir}/home-tee-{h.Number}.png"));
                    Assert.IsTrue(feet.z > 0 && feet.x > 0.1f && feet.x < 0.9f && feet.y > 0.12f && feet.y < 0.7f, $"hole {h.Number}: the golfer's feet in the picture, above the dock ({feet})");
                }

                game.Play();
                yield return null;
                Assert.IsTrue(game.Current is GolfGame.State.Intro or GolfGame.State.Aim, "PLAY starts the round");
                Assert.IsFalse(GameObject.Find("Menu"));
            }
            finally
            {
                PlayerPrefs.SetInt("holes", holes); PlayerPrefs.Save();
            }
        }
    }
}
