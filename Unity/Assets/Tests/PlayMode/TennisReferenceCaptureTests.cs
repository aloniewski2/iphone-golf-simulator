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
    /// Captures for the static reference-matching pass: the game camera's real output at the
    /// reference frame's 1920x1080, from the untouched game and from the reference scene.
    /// Explicit: for review only.
    public class TennisReferenceCaptureTests
    {
        const string Dir = "Library/Captures/reference";

        /// CAPTURE_RIVAL=Viktor plays the clip against that campaign rival (their body and profile).
        static void Rival(TennisGame game)
        {
            string key = System.Environment.GetEnvironmentVariable("CAPTURE_RIVAL");
            if (!string.IsNullOrEmpty(key)) game.ConfigureMatch(TennisGame.Mode.Campaign, key, key, "FINAL", 3, 6);
        }

        [UnityTest, Explicit] public IEnumerator CaptureCurrentGameCamera()
        {
            yield return SceneManager.LoadSceneAsync("Tennis", LoadSceneMode.Single);
            // Past the opening fly-over, into ordinary play.
            for (float t = 0; t < 14; t += Time.unscaledDeltaTime) yield return null;
            Directory.CreateDirectory(Dir);
            GameCapture.Save($"{Dir}/current-game.jpg", 1920, 1080);
        }

        /// A serve as it plays in the game: the real match on AutoPlay, recorded from the game
        /// camera at a fixed 30 fps from the moment the player steps up to serve, as numbered
        /// frames for ffmpeg. Explicit: for review.
        [UnityTest, Explicit] public IEnumerator CaptureServeClip()
        {
            yield return SceneManager.LoadSceneAsync("Tennis", LoadSceneMode.Single);
            var game = Object.FindFirstObjectByType<TennisGame>();
            yield return null; Rival(game);
            game.AutoPlay = true;
            string dir = $"{Dir}/serve-clip";
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            Directory.CreateDirectory(dir);
            // Fast-forward to the player's serve (or, with SERVE_SIDE=opponent, the opponent's).
            bool opponent = System.Environment.GetEnvironmentVariable("SERVE_SIDE") == "opponent";
            var wanted = opponent ? TennisGame.Phase.OpponentServe : TennisGame.Phase.PlayerServeHold;
            Time.timeScale = 4;
            float waited = 0;
            while (game.Flow != wanted && waited < 240) { waited += Time.unscaledDeltaTime; yield return null; }
            Assert.AreEqual(wanted, game.Flow, "never reached the serve");
            Time.timeScale = 1;
            Time.captureFramerate = 30;
            try
            {
                for (int i = 0; i < 240; i++)
                {
                    yield return new WaitForEndOfFrame();
                    GameCapture.Save($"{dir}/f{i:0000}.jpg", 1280, 720);
                }
            }
            finally { Time.captureFramerate = 0; game.AutoPlay = false; }
        }

        /// The match intro (drone, rival card and emote, player card and emote) as it plays,
        /// recorded at 30 fps. Explicit: for review.
        [UnityTest, Explicit] public IEnumerator CaptureIntroClip()
        {
            yield return SceneManager.LoadSceneAsync("Tennis", LoadSceneMode.Single);
            string dir = $"{Dir}/intro-clip";
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            Directory.CreateDirectory(dir);
            Time.captureFramerate = 30;
            try
            {
                for (int i = 0; i < 30 * 15; i++)
                {
                    yield return new WaitForEndOfFrame();
                    GameCapture.Save($"{dir}/f{i:0000}.jpg", 1280, 720);
                }
            }
            finally { Time.captureFramerate = 0; }
        }

        /// A rally on AutoPlay (forehands and backhands from both players), recorded at 30 fps
        /// from the first shot after the serve. Explicit: for review.
        [UnityTest, Explicit] public IEnumerator CaptureRallyClip()
        {
            yield return SceneManager.LoadSceneAsync("Tennis", LoadSceneMode.Single);
            var game = Object.FindFirstObjectByType<TennisGame>();
            yield return null; Rival(game);
            game.AutoPlay = true;
            string dir = $"{Dir}/rally-clip";
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            Directory.CreateDirectory(dir);
            Time.timeScale = 4;
            float waited = 0;
            while (game.Flow != TennisGame.Phase.Rally && waited < 240) { waited += Time.unscaledDeltaTime; yield return null; }
            Assert.AreEqual(TennisGame.Phase.Rally, game.Flow, "never reached a rally");
            Time.timeScale = 1;
            Time.captureFramerate = 30;
            try
            {
                for (int i = 0; i < 30 * 12; i++)
                {
                    yield return new WaitForEndOfFrame();
                    GameCapture.Save($"{dir}/f{i:0000}.jpg", 1280, 720);
                }
            }
            finally { Time.captureFramerate = 0; game.AutoPlay = false; }
        }

        /// The video's key poses, in engine: a forehand and a backhand from the reference
        /// camera, captured at the backswing, contact and follow-through (the phases of video
        /// frames 108/114/121 and 45/53/63). Explicit: for review.
        [UnityTest, Explicit] public IEnumerator CaptureVideoKeyPoses()
        {
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Scenes/TennisReference.unity", new LoadSceneParameters(LoadSceneMode.Single));
#endif
            for (int i = 0; i < 60; i++) yield return null;
            var game = Object.FindFirstObjectByType<TennisGame>();
            Directory.CreateDirectory(Dir);
            foreach (bool backhand in new[] { false, true })
            {
                string label = backhand ? "backhand" : "forehand";
                for (int i = 0; i < 40; i++) { game.Player.Tick(1f / 60, 0); yield return null; }
                game.Player.Swing(.8f, backhand, TennisActor.Stroke.Drive);
                // Seconds after the swing starts: early (backswing), contact, follow-through, finish.
                float[] at = { .02f, TennisRules.SweetTime * .9f, .3f, .45f };
                float t = 0; int shot = 0;
                while (shot < at.Length)
                {
                    game.Player.Tick(1f / 120, 0); t += 1f / 120;
                    if (t >= at[shot])
                    {
                        yield return new WaitForEndOfFrame();
                        GameCapture.Save($"{Dir}/keypose-{label}-{shot}.jpg", 1920, 1080);
                        shot++;
                    }
                }
            }
        }

        [UnityTest, Explicit] public IEnumerator CaptureReferenceScene()
        {
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Scenes/TennisReference.unity", new LoadSceneParameters(LoadSceneMode.Single));
#endif
            for (int i = 0; i < 90; i++) yield return null;
            Assert.IsNotNull(Object.FindFirstObjectByType<TennisReferenceMatch>());
            // Capture after LateUpdate, where the reference stage sets the camera.
            yield return new WaitForEndOfFrame();
            Directory.CreateDirectory(Dir);
            string label = System.Environment.GetEnvironmentVariable("REFERENCE_PASS") ?? "pass";
            GameCapture.Save($"{Dir}/{label}.jpg", 1920, 1080);
        }
    }
}
