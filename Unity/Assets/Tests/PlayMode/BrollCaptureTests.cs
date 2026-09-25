using System.Collections;
using System.IO;
using GolfArcade.Game;
using GolfArcade.Tennis;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GolfArcade.PlayTests
{
    /// Game footage for the menus' b-roll and for review: numbered 30 fps frames in
    /// Library/Captures/broll/<name>, encoded to MenuVideo/<sport>-game.mp4 outside Unity.
    /// Explicit: for review only.
    public class BrollCaptureTests
    {
        const string Dir = "Library/Captures/broll";

        static string Fresh(string name)
        {
            string dir = $"{Dir}/{name}";
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            Directory.CreateDirectory(dir);
            return dir;
        }

        static IEnumerator Record(string dir, int frames)
        {
            Time.captureFramerate = 30;
            try
            {
                for (int i = 0; i < frames; i++)
                {
                    yield return new WaitForEndOfFrame();
                    GameCapture.Save($"{dir}/f{i:0000}.jpg", 1280, 720);
                }
            }
            finally { Time.captureFramerate = 0; }
        }

        static IEnumerator Until(System.Func<bool> ready, float seconds)
        {
            float waited = 0;
            while (!ready() && waited < seconds) { waited += Time.unscaledDeltaTime; yield return null; }
        }

        /// A rally against a campaign rival, for the tennis b-roll.
        [UnityTest, Explicit] public IEnumerator CaptureTennisBroll()
        {
            yield return SceneManager.LoadSceneAsync("Tennis", LoadSceneMode.Single);
            var game = Object.FindFirstObjectByType<TennisGame>();
            yield return null;
            game.ConfigureMatch(TennisGame.Mode.Exhibition, "Rosa", "Rosa", "EXHIBITION");
            game.AutoPlay = true;
            Time.timeScale = 4;
            yield return Until(() => game.Flow == TennisGame.Phase.Rally, 240);
            Time.timeScale = 1;
            yield return Record(Fresh("tennis-game"), 30 * 10);
            game.AutoPlay = false;
        }

        /// Tee shots on the cliffside course, for the golf b-roll.
        [UnityTest, Explicit] public IEnumerator CaptureGolfBroll()
        {
            yield return SceneManager.LoadSceneAsync("Golf", LoadSceneMode.Single);
            var golf = Object.FindFirstObjectByType<GolfGame>();
            yield return Until(() => golf.Current == GolfGame.State.Aim, 30);
            Assert.AreEqual(GolfGame.State.Aim, golf.Current, "never reached the tee");
            golf.NativeSwing(.9f);
            yield return Record(Fresh("golf-game"), 30 * 10);
        }

        /// The tutorial on court (self-play, from the forehand drill on), for review.
        [UnityTest, Explicit] public IEnumerator CaptureTutorialClip()
        {
            yield return SceneManager.LoadSceneAsync("Tennis", LoadSceneMode.Single);
            var game = Object.FindFirstObjectByType<TennisGame>();
            yield return null;
            game.ConfigureMatch(TennisGame.Mode.Tutorial, null, "Ray", null);
            var tutorial = game.GetComponent<TennisTutorial>();
            Assert.IsNotNull(tutorial, "tutorial mode adds the lesson");
            tutorial.SkipStep();   // self-play cannot walk to the rings
            game.AutoPlay = true;
            LogAssert.ignoreFailingMessages = System.Environment.GetEnvironmentVariable("CAPTURE_DEBUG") == "1";
            yield return Record(Fresh("tutorial"), 30 * 24);
            game.AutoPlay = false;
            foreach (var t in Object.FindObjectsByType<UnityEngine.UI.Text>(FindObjectsSortMode.None))
                if (t.text.Length > 150) Debug.Log($"LONGTEXT {t.name} {t.text.Length}: {t.text.Substring(0, 150)}");
            Assert.AreNotEqual(TutorialLesson.Kind.Move, tutorial.Lesson.Current.Kind);
        }

        /// The player in custom kit colours (character screen), serving, for review.
        [UnityTest, Explicit] public IEnumerator CaptureOutfit()
        {
            yield return SceneManager.LoadSceneAsync("Tennis", LoadSceneMode.Single);
            var game = Object.FindFirstObjectByType<TennisGame>();
            yield return null;
            bool female = System.Environment.GetEnvironmentVariable("OUTFIT_FEMALE") == "1";
            game.SelectCharacter(female);
            game.ApplyOutfit(TennisLook.Kit.From("E0243C", "1E2A6E", "9EE63A", "8A4FFF", female ? 1 : 4));
            game.ConfigureMatch(TennisGame.Mode.Training, null, "Coach", null);
            yield return new WaitForSeconds(1.5f);
            string dir = Fresh(female ? "outfit-female" : "outfit-male");
            for (int i = 0; i < 4; i++)
            {
                yield return new WaitForSeconds(.6f);
                yield return new WaitForEndOfFrame();
                GameCapture.Save($"{dir}/still{i}.jpg", 1920, 1080);
            }
        }
    }
}
