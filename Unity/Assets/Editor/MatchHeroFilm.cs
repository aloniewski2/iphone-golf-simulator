using System;
using System.Collections;
using System.IO;
using GolfArcade.Game;
using GolfArcade.Tennis;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GolfArcade.EditorTools
{
    /// Records the game camera (HUD included) while the match heroes play a campaign match by self-play: a numbered PNG every other frame (30 fps) until a few points are done.
    ///   MH_FILM_OUT=<dir> MH_FILM_RIVAL=Nadia MH_FILM_FEMALE=0 MH_FILM_POINTS=3 Unity -batchmode -projectPath Unity -executeMethod GolfArcade.EditorTools.MatchHeroFilm.Run
    [InitializeOnLoad]
    public static class MatchHeroFilm
    {
        const string Flag = "MatchHeroFilm"; static IEnumerator script;
        static MatchHeroFilm() { EditorApplication.update += Tick; }
        public static void Run() { EditorSceneManager.OpenScene("Assets/Scenes/Tennis.unity"); SessionState.SetBool(Flag, true); EditorApplication.isPlaying = true; }
        static void Tick()
        {
            if (!SessionState.GetBool(Flag, false) || !EditorApplication.isPlaying) return;
            var game = Object.FindFirstObjectByType<TennisGame>(); if (!game || !game.Initialized) return;
            if (script == null) script = Go(game);
            try { if (!script.MoveNext()) Done(0); } catch (Exception e) { Debug.LogException(e); Done(1); }
        }
        static void Done(int c) { SessionState.SetBool(Flag, false); Time.captureFramerate = 0; if (Application.isBatchMode) EditorApplication.Exit(c); else EditorApplication.isPlaying = false; }
        static string Env(string k, string d) => Environment.GetEnvironmentVariable(k) ?? d;
        static IEnumerator Go(TennisGame game)
        {
            string dir = Path.GetFullPath(Env("MH_FILM_OUT", "../work/hero-mainstay/film/frames")); Directory.CreateDirectory(dir);
            foreach (var f in Directory.GetFiles(dir, "*.png")) File.Delete(f);
            Time.captureFramerate = 60; TennisGame.AutoPlayTimingJitter = .05f;
            var pres = game.GetComponent<TennisPresentation>(); if (pres) pres.Finish();
            bool female = Env("MH_FILM_FEMALE", "0") == "1"; string rival = Env("MH_FILM_RIVAL", "Nadia"); int want = int.Parse(Env("MH_FILM_POINTS", "3"));
            game.SelectCharacter(female); game.ConfigureMatch(TennisGame.Mode.Campaign, rival, rival, "ROUND"); if (pres) pres.Finish();
            for (int i = 0; i < 60; i++) yield return null;
            game.AutoPlay = true; game.AutoPlayLean = true;
            int n = 0, points = 0, tail = 0; var last = game.Flow;
            for (int f = 0; f < 60 * 120; f++)
            {
                yield return null;
                if (f % 2 == 0) GameCapture.Save($"{dir}/f{n++:05}.png", 1280, 720);
                if (game.Flow == TennisGame.Phase.PointOver && last != TennisGame.Phase.PointOver) points++;
                last = game.Flow;
                if (points >= want && ++tail > 150) break;
            }
            Debug.Log($"[MatchHeroFilm] frames={n} points={points}");
        }
    }
}
