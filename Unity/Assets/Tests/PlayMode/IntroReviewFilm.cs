using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using GolfArcade.Game;
using GolfArcade.Tennis;
using GolfArcade.Course;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GolfArcade.PlayTests
{
    /// Review filming of the pre-round shows (golf flyover + intro, tennis drone + intros), their skips, and one golf
    /// drive, as JPG frames at a fixed capture clock: INTRO_FILM_OUT/<name>/f####.jpg, plus measured beat timings in
    /// INTRO_FILM_OUT/timings.txt (the gates read them).
    public class IntroReviewFilm
    {
        static string Root => Environment.GetEnvironmentVariable("INTRO_FILM_OUT") ?? "Library/Captures/intro-review";
        const int FPS = 20;
        static readonly List<string> lines = new();
        static void Note(string line) { lines.Add(line); Debug.Log("[IntroFilm] " + line); File.WriteAllLines(Path.Combine(Root, "timings.txt"), lines); }
        static void ClearSeen()
        {
            foreach (var key in new[] { "tennis.venue." + TennisVenue.Selected, "tennis.walkon", "tennis.start", "golf.walkon", "golf.start" })
                PlayerPrefs.DeleteKey("presentation.v" + PresentationPolicy.Revision + "." + key);
            foreach (var c in new[] { "cliffside", "wildisles", "magma" })
                for (int i = 1; i <= 30; i++) PlayerPrefs.DeleteKey("presentation.v" + PresentationPolicy.Revision + ".golf.venue." + c + "." + i);
        }
        static IEnumerator Film(string name, float seconds, Action<int> action = null, Func<bool> until = null, bool save = true)
        {
            string dir = Path.Combine(Root, name); Directory.CreateDirectory(dir);
            foreach (var f in Directory.GetFiles(dir, "*.jpg")) File.Delete(f);
            var camera = Camera.main; if (camera) camera.aspect = 960f / 540;
            int frames = Mathf.CeilToInt(seconds * FPS);
            for (int frame = 0; frame < frames; frame++)
            {
                action?.Invoke(frame); yield return null;
                if (save) GameCapture.Save(Path.Combine(dir, $"f{frame:0000}.jpg"), 960, 540);
                if (until != null && until()) break;
            }
        }
        /// Seconds (capture clock) each golf beat ran, in order, while filming.
        /// The emote slot the player taps one second into the opening (INTRO_FILM_PICK; unset: no pick, nobody emotes).
        static int Pick => int.TryParse(Environment.GetEnvironmentVariable("INTRO_FILM_PICK"), out var slot) ? slot : -1;
        static int TennisPick => int.TryParse(Environment.GetEnvironmentVariable("INTRO_FILM_TENNIS_PICK"), out var slot) ? slot : Pick;
        static IEnumerator FilmGolf(GolfGame golf, string name, int skipAt, Dictionary<int, float> beats, int pick = -1)
        {
            int frame = 0;
            yield return Film(name, 40, f =>
            {
                frame = f;
                int beat = golf.PresentationBeatIndex;
                if (beat >= 0) beats[beat] = (beats.TryGetValue(beat, out var s) ? s : 0) + 1f / FPS;
                if (f == FPS && pick >= 0) golf.PlayResultEmote(pick);
                if (f == skipAt) golf.NativeContinue();
                if (skipAt >= 0 && f == skipAt + 30) golf.NativeContinue();   // a second press during the golfer's intro must change nothing
            }, () => golf.Current != GolfGame.State.Intro);
        }
        [UnityTest, Explicit, Timeout(3600000)]
        public IEnumerator FilmIntros()
        {
            int oldRate = Time.captureFramerate; Time.captureFramerate = FPS; Time.timeScale = 1;
            string only = Environment.GetEnvironmentVariable("INTRO_FILM_ONLY") ?? "golffull,drive,golfskip,golfshort,tennisfull,tennisskip";
            string courseKey = Environment.GetEnvironmentVariable("INTRO_FILM_COURSE");
            Directory.CreateDirectory(Root); lines.Clear();
            try
            {
                ClearSeen(); PresentationPolicy.Configure("full", false, true, false);
                if (only.Contains("golf") || only.Contains("drive"))
                {
                    yield return SceneManager.LoadSceneAsync("Golf"); yield return null;
                    var golf = Object.FindFirstObjectByType<GolfGame>();
                    foreach (var key in (Environment.GetEnvironmentVariable("INTRO_FILM_COURSES") ?? courseKey ?? "cliffside").Split(','))
                    {
                        if (only.Contains("golffull"))
                        {
                            ClearSeen(); golf.PrepareNativeAddress(key); golf.NativeReady(); golf.TryNativePresentation();
                            var beats = new Dictionary<int, float>();
                            yield return FilmGolf(golf, "golf_full_" + key, -1, beats, Pick);
                            Note($"golf_full_{key} flyover={Get(beats, 0):F2} intro={Get(beats, 1):F2} start={Get(beats, 2):F2} hole={golf.CurrentHole.Number} pick={Pick}");
                        }
                        if (only.Contains("golfshort"))
                        {
                            golf.PrepareNativeAddress(key); golf.NativeReady(); golf.TryNativePresentation();
                            var beats = new Dictionary<int, float>();
                            yield return FilmGolf(golf, "golf_short_" + key, -1, beats);
                            Note($"golf_short_{key} flyover={Get(beats, 0):F2} intro={Get(beats, 1):F2} start={Get(beats, 2):F2}");
                        }
                        if (only.Contains("golfskip"))
                        {
                            ClearSeen(); golf.PrepareNativeAddress(key); golf.NativeReady(); golf.TryNativePresentation();
                            var beats = new Dictionary<int, float>();
                            yield return FilmGolf(golf, "golf_skip_" + key, 40, beats);
                            Note($"golf_skip_{key} flyover={Get(beats, 0):F2} intro={Get(beats, 1):F2} start={Get(beats, 2):F2} (skip pressed at 2.00 s, again at 3.50 s)");
                        }
                        if (only.Contains("drive"))
                        {
                            golf.PrepareNativeAddress(key); golf.NativeReady(); golf.SkipPresentation();
                            for (int f = 0; golf.Current == GolfGame.State.Intro && f < 2000; f++) { if (f % 20 == 0) golf.SkipPresentation(); yield return null; }
                            for (int f = 0; f < 10; f++) yield return null;
                            if (Environment.GetEnvironmentVariable("INTRO_FILM_CALM") == "1") golf.SetWind(new GolfArcade.Course.Wind(0, 0));
                            var urp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
                            if (urp) Note($"drive_{key} shadows distance={urp.shadowDistance} cascades={urp.shadowCascadeCount} resolution={urp.mainLightShadowmapResolution}");
                            golf.StrikeToward(golf.CurrentHole.RecommendedTarget(golf.CurrentHole.Tee));
                            yield return Film("golf_drive_" + key, 14, null, () => golf.Current == GolfGame.State.Result);
                        }
                    }
                }
                if (only.Contains("tennis"))
                {
                    ClearSeen(); PresentationPolicy.Configure("full", false, true, false);
                    yield return SceneManager.LoadSceneAsync("Tennis"); yield return null;
                    var tennis = Object.FindFirstObjectByType<TennisGame>();
                    tennis.AutoPlay = false; tennis.NativeControlled = true; tennis.ManualSimulation = false; tennis.Drill = false;
                    var intro = tennis.GetComponent<TennisPresentation>();
                    if (only.Contains("tennisfull"))
                    {
                        intro.Restart();
                        float flyover = 0, total = 0;
                        yield return Film("tennis_full", 40, f => { if (f == FPS && TennisPick >= 0) tennis.RequestEquippedEmote(TennisPick); if (intro.InFlyover) flyover += 1f / FPS; if (intro.Playing) total += 1f / FPS; }, () => !intro.Playing);
                        Note($"tennis_full drone={flyover:F2} total={total:F2} pick={TennisPick}");
                    }
                    if (only.Contains("tennisskip"))
                    {
                        ClearSeen(); intro.Restart();
                        float flyover = 0, after = 0; bool skipped = false;
                        yield return Film("tennis_skip", 40, f =>
                        {
                            if (f == 30) { tennis.RequestSwing(.8f); skipped = true; }
                            if (f == 70) tennis.RequestSwing(.8f);    // a swing during the intros must not cut them
                            if (intro.InFlyover) flyover += 1f / FPS; else if (skipped && intro.Playing) after += 1f / FPS;
                        }, () => !intro.Playing);
                        Note($"tennis_skip drone={flyover:F2} after_skip={after:F2} (swing at 1.50 s, again at 3.50 s)");
                    }
                }
            }
            finally { Time.captureFramerate = oldRate; PresentationPolicy.Configure("full", false, true, false); PresentationStinger.Stop(); }
        }
        static float Get(Dictionary<int, float> d, int k) => d.TryGetValue(k, out var v) ? v : 0;
    }
}
