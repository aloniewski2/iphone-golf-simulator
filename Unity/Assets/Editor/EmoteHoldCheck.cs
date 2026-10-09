using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using GolfArcade.Game;
using GolfArcade.Tennis;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GolfArcade.EditorTools
{
    /// EMOTES hold check: after a won GAME the game waits 2.4 s (4.4 s after a set) before the next serve, which cut the old 2 s taunts at the end and would cut the longer ones (Spike 3.2 s, Thrust 3.5 s, Scuba 4.7 s).
    /// TennisGame.HoldNextPoint(seconds) (asked for by HeroTennisDriver.PlayEmote for a taunt) keeps the serve waiting until the taunt has played out.  This plays each of the three taunts through the real
    /// game flow (a won game while the serve timer runs) and records when the emote ended and when the next point started.
    ///   EH_OUT=<dir> EH_FEMALE=0|1 Unity -batchmode -projectPath Unity -executeMethod GolfArcade.EditorTools.EmoteHoldCheck.Run      (no -quit, needs graphics)
    [InitializeOnLoad]
    public static class EmoteHoldCheck
    {
        const string Flag = "EmoteHoldCheck"; static IEnumerator script;
        static EmoteHoldCheck() { EditorApplication.update += Tick; }
        public static void Run() { EditorSceneManager.OpenScene("Assets/Scenes/Tennis.unity"); SessionState.SetBool(Flag, true); EditorApplication.isPlaying = true; }
        static string Env(string k, string d) => Environment.GetEnvironmentVariable(k) ?? d;
        static void Tick()
        {
            if (!SessionState.GetBool(Flag, false) || !EditorApplication.isPlaying) return;
            var game = Object.FindFirstObjectByType<TennisGame>(); if (!game || !game.Initialized) return;
            if (script == null) script = Go(game);
            try { if (!script.MoveNext()) Done(0); } catch (Exception e) { Debug.LogException(e); Done(1); }
        }
        static void Done(int c) { SessionState.SetBool(Flag, false); Time.captureFramerate = 0; if (Application.isBatchMode) EditorApplication.Exit(c); else EditorApplication.isPlaying = false; }
        static string F(float x) => x.ToString("0.000", CultureInfo.InvariantCulture);

        static IEnumerator Go(TennisGame game)
        {
            string dir = Path.GetFullPath(Env("EH_OUT", "../work/emote-overhaul/proof/hold")); Directory.CreateDirectory(dir);
            Time.captureFramerate = 60; TennisGame.AutoPlayTimingJitter = 0f;
            var pres = game.GetComponent<TennisPresentation>(); if (pres) pres.Finish();
            bool female = Env("EH_FEMALE", "0") == "1"; string rival = female ? "Viktor" : "Nadia";
            game.SelectCharacter(female); game.ConfigureMatch(TennisGame.Mode.Campaign, rival, rival, "ROUND"); if (pres) pres.Finish();
            for (int i = 0; i < 90; i++) yield return null;
            var pd = game.Player.GetComponentInChildren<HeroTennisDriver>(); var actor = game.Player;
            var flowSet = typeof(TennisGame).GetProperty("Flow").GetSetMethod(true);
            var resetField = typeof(TennisGame).GetField("resetTimer", BindingFlags.NonPublic | BindingFlags.Instance);
            var matchField = typeof(TennisGame).GetField("match", BindingFlags.NonPublic | BindingFlags.Instance);
            var gamesField = typeof(TennisMatch).GetField("PlayerGames");
            var awardPoint = typeof(TennisGame).GetMethod("AwardPoint", BindingFlags.NonPublic | BindingFlags.Instance);
            var rows = new List<string>();
            string[] names = { "EmoteScuba", "EmoteThrust", "EmoteSpike" };
            int only = int.Parse(Env("EH_TAUNT", "0"));       // 0..2 = that taunt after a won game, -1 = a plain point
            for (int idx = 0; idx < 3; idx++)
            {
                if (idx != only) continue;
                // the player holds game point (40-0) and wins the point in a rally: the REAL AwardPoint runs (reactions, taunt, then the 2.4 s serve timer is set)
                flowSet.Invoke(game, new object[] { TennisGame.Phase.Rally }); resetField.SetValue(game, 0f);
                object boxed = matchField.GetValue(game); typeof(TennisMatch).GetField("PlayerPoints").SetValue(boxed, 3); typeof(TennisMatch).GetField("OpponentPoints").SetValue(boxed, 0); matchField.SetValue(game, boxed);
                HeroTennisDriver.PickedTaunt = idx;
                int played0 = pd.EmotesPlayed;
                float t0 = Time.time;
                awardPoint.Invoke(game, new object[] { true, true });
                yield return null;
                float timerAfter = (float)resetField.GetValue(game); bool started = pd.EmoteActive && pd.EmotesPlayed == played0 + 1; string clip = pd.LastEmotePlayed;
                float tEnd = -1, tNext = -1; int guard = 0; bool sawEmote = pd.EmoteActive;
                while (guard++ < 60 * 10)
                {
                    yield return null;
                    if (pd.EmoteActive) sawEmote = true;
                    if (tEnd < 0 && sawEmote && !pd.EmoteActive) tEnd = Time.time - t0;
                    if (tNext < 0 && game.Flow != TennisGame.Phase.PointOver) tNext = Time.time - t0;
                    if (tEnd >= 0 && tNext >= 0) break;
                }
                bool ok = started && clip == names[idx] && tEnd > 0 && (tNext < 0 || tNext >= tEnd - 0.02f) && timerAfter > 2.4f;
                rows.Add($"{{\"taunt\": \"{names[idx]}\", \"started\": {started.ToString().ToLower()}, \"clip\": \"{clip}\", \"serve_timer_after_award\": {F(timerAfter)}, \"emote_ended_at\": {F(tEnd)}, \"next_point_at\": {F(tNext)}, \"ok\": {ok.ToString().ToLower()}}}");
                Debug.Log("[EmoteHoldCheck] " + rows[rows.Count - 1]);
                for (int i = 0; i < 30; i++) yield return null;
            }
            // a plain point (no game won) must keep the normal 2.4 s beat: no taunt, no hold
            if (only < 0)
            {
                flowSet.Invoke(game, new object[] { TennisGame.Phase.Rally }); resetField.SetValue(game, 0f);
                object boxed = matchField.GetValue(game); typeof(TennisMatch).GetField("PlayerPoints").SetValue(boxed, 0); typeof(TennisMatch).GetField("OpponentPoints").SetValue(boxed, 0); matchField.SetValue(game, boxed);
                int played0 = pd.EmotesPlayed;
                awardPoint.Invoke(game, new object[] { true, false });
                yield return null;
                float timerAfter = (float)resetField.GetValue(game);
                bool ok = pd.EmotesPlayed == played0 && timerAfter <= 2.45f && timerAfter > 2.0f;
                rows.Add($"{{\"taunt\": \"none (plain point)\", \"serve_timer_after_award\": {F(timerAfter)}, \"emotes_played\": {pd.EmotesPlayed - played0}, \"ok\": {ok.ToString().ToLower()}}}");
                Debug.Log("[EmoteHoldCheck] " + rows[rows.Count - 1]);
            }
            HeroTennisDriver.PickedTaunt = -1;
            File.WriteAllText(dir + "/hold_" + (female ? "female" : "male") + "_" + (only < 0 ? "plain" : names[only]) + ".json", "[\n  " + string.Join(",\n  ", rows) + "\n]\n");
        }
    }
}
