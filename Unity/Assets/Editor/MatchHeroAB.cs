using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GolfArcade.Tennis;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GolfArcade.EditorTools
{
    /// HERO_MAINSTAY A/B: the same scripted self-play (same seed, same AutoPlay, same 150 s) with the match heroes and with the old Hero01 prefab put on the actors
    /// by hand (the old path of HeroTennisDriver is still compiled), to see whether the new bodies make the strings meet the ball any worse.
    ///   MH_AB=new|legacy  MH_AB_OUT=<file>  Unity -batchmode -projectPath Unity -executeMethod GolfArcade.EditorTools.MatchHeroAB.Run
    /// Evidence tool only: nothing in the game refers to it.
    [InitializeOnLoad]
    public static class MatchHeroAB
    {
        const string Flag = "MatchHeroAB";
        static IEnumerator script;
        static readonly List<string> lines = new List<string>();
        static MatchHeroAB() { EditorApplication.update += Tick; }

        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Tennis.unity");
            SessionState.SetBool(Flag, true);
            EditorApplication.isPlaying = true;
        }

        static void Tick()
        {
            if (!SessionState.GetBool(Flag, false) || !EditorApplication.isPlaying) return;
            var game = Object.FindFirstObjectByType<TennisGame>();
            if (!game || !game.Initialized) return;
            if (script == null) script = Script(game);
            try { if (!script.MoveNext()) Finish(0); }
            catch (Exception e) { Debug.LogException(e); Finish(1); }
        }

        static void Finish(int code)
        {
            SessionState.SetBool(Flag, false);
            var path = Path.GetFullPath(Environment.GetEnvironmentVariable("MH_AB_OUT") ?? "../work/hero-mainstay/logs/ab.txt");
            File.WriteAllText(path, string.Join("\n", lines) + "\n");
            Time.captureFramerate = 0;
            if (Application.isBatchMode) EditorApplication.Exit(code); else EditorApplication.isPlaying = false;
        }

        /// The old Hero01 prefab on an actor, exactly as TennisHeroSetup.Attach used to do it.
        static HeroTennisDriver Legacy(TennisGame game, TennisActor actor, bool player)
        {
            foreach (var d in actor.GetComponentsInChildren<HeroTennisDriver>(true)) Object.DestroyImmediate(d.gameObject);
            var prefab = Resources.Load<GameObject>(LegacyHero01.PrefabPath);
            var hero = Object.Instantiate(prefab, actor.transform, false); hero.name = "Hero01 (A/B)";
            hero.transform.localPosition = Vector3.zero; hero.transform.localRotation = Quaternion.identity;
            foreach (var r in actor.GetComponentsInChildren<Renderer>(true)) if (!r.transform.IsChildOf(hero.transform) && !(r is LineRenderer)) r.enabled = false;
            var driver = hero.GetComponent<HeroTennisDriver>(); driver.actor = actor; driver.game = game; driver.isPlayer = player; driver.Build();
            return driver;
        }

        static IEnumerator Script(TennisGame game)
        {
            bool legacy = Environment.GetEnvironmentVariable("MH_AB") == "legacy";
            Time.captureFramerate = 60; TennisGame.AutoPlayTimingJitter = .05f;
            var presentation = game.GetComponent<TennisPresentation>(); if (presentation) presentation.Finish();
            yield return null;
            lines.Add("mode=" + (legacy ? "LEGACY Hero01 (old clips, old body)" : "NEW match heroes"));
            foreach (var (key, playerFemale) in new[] { ("Nadia", false), ("Viktor", true), ("Milo", false), ("Rosa", true) })
            {
                game.SelectCharacter(playerFemale);
                game.ConfigureMatch(TennisGame.Mode.Campaign, key, key, "ROUND");
                if (presentation) presentation.Finish();
                yield return null; yield return null;
                if (legacy) { Legacy(game, game.Player, true); Legacy(game, game.Opponent, false); }
                for (int i = 0; i < 30; i++) yield return null;
                int h0 = game.Hits; int m0 = game.HonestMisses, w0 = game.RivalWhiffs;
                float pn = game.PlayerGaps.Count, ps = game.PlayerGaps.Sum, on = game.OpponentGaps.Count, os = game.OpponentGaps.Sum; int pv = game.PlayerGaps.Visible, ov = game.OpponentGaps.Visible;
                game.AutoPlay = true; game.AutoPlayLean = true;
                for (int frame = 0; frame < 60 * 150; frame++) yield return null;
                game.AutoPlay = false;
                lines.Add($"[{key}] hits={game.Hits - h0} honestMisses={game.HonestMisses - m0} rivalWhiffs={game.RivalWhiffs - w0} playerGaps n={game.PlayerGaps.Count - pn} mean={(game.PlayerGaps.Sum - ps) / Mathf.Max(1, game.PlayerGaps.Count - pn):0.000} visible(>12cm)={game.PlayerGaps.Visible - pv}" +
                          $" | rivalGaps n={game.OpponentGaps.Count - on} mean={(game.OpponentGaps.Sum - os) / Mathf.Max(1, game.OpponentGaps.Count - on):0.000} visible(>12cm)={game.OpponentGaps.Visible - ov} max={game.OpponentGaps.Max:0.00} | score={game.Match.Scoreboard} longestRally={game.LongestRally}");
            }
        }
    }
}
