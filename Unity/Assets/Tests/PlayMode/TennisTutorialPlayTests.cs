using System.Collections;
using GolfArcade.Tennis;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GolfArcade.PlayTests
{
    public class TennisTutorialPlayTests
    {
        const float Dt = 1f / 120;
        static void Simulate(TennisGame game, float seconds)
        { for (int i = 0; i < seconds * 120; i++) game.Step(Dt); }

        static void Serve(TennisGame game, bool good)
        {
            game.Toss(good ? 1 : 0);
            for (int i = 0; i < 300 && game.Flow != TennisGame.Phase.PlayerServeToss; i++) game.Step(Dt);
            if (good) Simulate(game, TennisRules.ServeApex + TennisRules.ServeLatency);
            game.RequestSwing(.7f);
            Simulate(game, 6);
        }

        [UnityTest, Timeout(120000)] public IEnumerator CustomizedCharacterKeepsRigAndRacket()
        {
            yield return SceneManager.LoadSceneAsync("Tennis", LoadSceneMode.Single); yield return null;
            var game=Object.FindFirstObjectByType<TennisGame>();
            game.ManualSimulation=true;
            game.ApplyOutfit(TennisLook.Kit.From("E0243C","1E2A6E","9EE63A","8A4FFF",4));
            game.Player.Customize(4,2,1,1,3,3);
            Assert.IsNotNull(game.Player.GetComponent<TennisCustomization>());
            Assert.AreEqual(Vector3.one,game.Player.transform.localScale, "Cosmetics must not change gameplay reach");
            var head=System.Array.Find(game.Player.GetComponentsInChildren<Transform>(),t=>t.name.EndsWith("Curls"));
            Assert.IsNotNull(head);
            var camera=new GameObject("Customization review camera").AddComponent<Camera>();
            camera.transform.position=game.Player.transform.position+new Vector3(0,1.1f,3.3f);
            camera.transform.LookAt(game.Player.transform.position+Vector3.up*.95f);
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.05f,.08f,.13f);
            var target=new RenderTexture(600,800,24);camera.targetTexture=target;
            yield return null;camera.Render();RenderTexture.active=target;
            var image=new Texture2D(600,800,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,600,800),0,0);image.Apply();
            System.IO.File.WriteAllBytes("/tmp/customization-unity-player.png",image.EncodeToPNG());
            RenderTexture.active=null;camera.targetTexture=null;Object.Destroy(target);Object.Destroy(image);Object.Destroy(camera.gameObject);
            var position=game.Player.SweetSpot.position;
            Assert.IsFalse(float.IsNaN(position.x));
            game.RequestSwing(.6f);
        }

        [UnityTest, Timeout(180000)] public IEnumerator CompleteTheWholeLessonWithRealShotsAndNoSkipping()
        {
            yield return SceneManager.LoadSceneAsync("Tennis", LoadSceneMode.Single); yield return null;
            var game = Object.FindFirstObjectByType<TennisGame>();
            game.ManualSimulation = true; game.NativeControlled = true;
            game.ConfigureMatch(TennisGame.Mode.Tutorial, "", "Ray", "TUTORIAL");
            var tutorial = game.GetComponent<TennisTutorial>();
            int finished = 0;
            System.Action onFinished = () => finished++;
            TennisTutorial.Finished += onFinished;
            Time.timeScale = 8;
            try
            {
                Assert.AreEqual(TutorialLesson.Kind.Serve, tutorial.Lesson.Current.Kind);
                // No impossible walking objective. First actual action is a normal serve.
                yield return new WaitForSeconds(1.6f);
                Assert.AreEqual(TennisGame.Phase.PlayerServeHold, game.Flow);
                Serve(game, false); yield return null;
                Assert.AreEqual(1, tutorial.Lesson.Misses, "net faults retry with coaching");
                Assert.AreEqual(TutorialLesson.Kind.Serve, tutorial.Lesson.Current.Kind);
                yield return new WaitForSeconds(1.6f);
                Serve(game, true); yield return null;
                Assert.AreEqual(TutorialLesson.Kind.Forehand, tutorial.Lesson.Current.Kind);
                foreach (var kind in new[] { TutorialLesson.Kind.Forehand, TutorialLesson.Kind.Backhand, TutorialLesson.Kind.Aim })
                {
                    Assert.AreEqual(kind, tutorial.Lesson.Current.Kind);
                    // Search human-playable swing timing using the scheduled real coach feeds.
                    for (int attempt = 0; attempt < 80 && tutorial.Lesson.Current.Kind == kind; attempt++)
                    {
                        yield return new WaitForSeconds(2);
                        game.AimInput = kind == TutorialLesson.Kind.Aim ? (tutorial.Lesson.AimLeft ? -.6f : .6f) : 0;
                        Simulate(game, .6f + (attempt % 60) * .04f);
                        bool back = game.BallPosition.x < game.Player.transform.position.x;
                        game.RequestSwing(.65f, back ? -1 : 1, 0, back ? -1 : 1);
                        Simulate(game, 4);
                        yield return null;
                    }
                    Assert.AreNotEqual(kind, tutorial.Lesson.Current.Kind, "must pass " + kind + " through actual successful shots");
                }
                Assert.AreEqual(TutorialLesson.Kind.Point, tutorial.Lesson.Current.Kind);
                Assert.IsFalse(tutorial.Lesson.Done, "the previous drill must not finish the real point");
                yield return new WaitForSeconds(1.6f);
                Assert.IsFalse(game.Drill);
                Serve(game, false); Serve(game, false); yield return null; yield return null;
                Assert.IsTrue(tutorial.Lesson.Done);
                Assert.AreEqual(1, finished);
                Assert.IsFalse(game.Drill);
            }
            finally { TennisTutorial.Finished -= onFinished; tutorial.Stop(); Time.timeScale = 1; }
        }

        [UnityTest, Timeout(180000)] public IEnumerator CoachFeedsCanBeReturnedToBothAimTargetsWithGoodTiming()
        {
            yield return SceneManager.LoadSceneAsync("Tennis", LoadSceneMode.Single); yield return null;
            var game = Object.FindFirstObjectByType<TennisGame>();
            game.ManualSimulation = true; game.NativeControlled = true; game.Drill = true;
            foreach (bool backhand in new[] { false, true })
            foreach (float aim in new[] { -.6f, .6f })
            {
                bool achieved = false;
                for (float swingAt = .6f; swingAt < 3f && !achieved; swingAt += .04f)
                {
                    game.PrepareLesson(); game.AimInput = aim;
                    game.Feed(backhand);
                    bool landed = false;
                    System.Action<bool, bool, Vector3, bool> onLanding = (player, inside, at, serve) =>
                    { if (player && inside && at.x * Mathf.Sign(aim) > .8f) landed = true; };
                    TennisGame.Landed += onLanding;
                    try
                    {
                        Simulate(game, swingAt);
                        game.RequestSwing(.65f, backhand ? -1 : 1, 0, backhand ? -1 : 1);
                        Simulate(game, 4);
                        achieved = landed && game.LastGrade >= Timing.Good;
                    }
                    finally { TennisGame.Landed -= onLanding; }
                }
                Assert.IsTrue(achieved, $"Coach feed backhand={backhand}, aim={aim} must allow a GOOD in-court return to target");
            }
        }
    }
}
