using System;
using System.Collections;
using System.Collections.Generic;
using GolfArcade.Course;
using GolfArcade.UI;
using UnityEngine;

namespace GolfArcade.Game
{
    /// The wait between two holes. Building a hole takes the main thread for a second or three, so the next hole
    /// is started behind a loading screen (UI/HoleLoadingCover.cs): the screen goes up first and is drawn, and only
    /// then is the hole built, slice by slice (StartHoleSteps), the screen drawing between the slices.
    public sealed partial class GolfGame
    {
        /// The least a loading screen stays up, so a quick hole does not make it flash (seconds, unscaled). The tests set it to 0.
        public static float LoadCoverMinimum = 0.9f;
        /// How long the screen is up and drawn before the first slice of the build, so the TV's and the phone's own
        /// covers are up too (seconds, unscaled).
        public static float LoadCoverLead = 0.3f;

        HoleLoadingCover loadingCover;
        bool loadingHole, coverNextRoundStart;

        /// The next hole is being built behind the loading screen: the game's frame loop stands still till it is done.
        public bool LoadingHole => loadingHole;
        public HoleLoadingCover LoadingCover => loadingCover;
        /// What the last hole load did: the progress it had reached and the milliseconds the slice before it took.
        public readonly List<(float progress, double ms)> HoleLoadLog = new();
        public double LastHoleLoadMilliseconds { get; private set; }
        public double LongestLoadSliceMilliseconds { get; private set; }
        /// The state the phone is told: "Loading" while the screen is up (and "Replay" for the beat before a replay: NativePhase).
        public string NativeState => loadingHole ? "Loading" : NativePhase;

        /// Starts a hole of the round behind the loading screen; holeIndex etc. follow when it is built. A game that is
        /// not running (a test that never drew a frame) starts it at once.
        void StartHoleBehindCover(int index)
        {
            if (loadingHole) return;
            if (!isActiveAndEnabled || !rig) { StartHole(index); return; }
            StartCoroutine(LoadHole(index));
        }

        HoleLoadingCover.Info InfoFor(int index)
        {
            var next = course.Holes[index];
            return new HoleLoadingCover.Info
            {
                Hole = next, Course = course.Name, Name = next.Name, Blurb = next.Blurb,
                Ordinal = index + 1, Count = course.Holes.Length, Par = next.Par, Yards = next.Length,
            };
        }

        static string WordsFor(float p) =>
            p < 0.1f ? "Packing the clubs…" : p < 0.3f ? "Shaping the fairway…" : p < 0.55f ? "Rolling out the greens…"
            : p < 0.85f ? "Planting the trees…" : p < 0.97f ? "Calling in the gallery…" : "Teeing it up…";

        IEnumerator LoadHole(int index)
        {
            loadingHole = true;
            HoleLoadLog.Clear(); LongestLoadSliceMilliseconds = 0;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            hud.HideScorecard();
            shotResultPanel?.gameObject.SetActive(false);
            if (!loadingCover) loadingCover = HoleLoadingCover.Create(rig.Camera);
            loadingCover.Show(InfoFor(index));
            loadingCover.SetProgress(0.04f, WordsFor(0));
            // up and drawn before the main thread is busy: the cover fully opaque, a moment longer for the
            // phone's and the TV's own covers, and one more frame so it is on the glass
            float until = Time.unscaledTime + LoadCoverLead;
            yield return null;
            while (!loadingCover.Opaque || Time.unscaledTime < until) yield return null;
            yield return null;

            var steps = StartHoleSteps(index);
            double previous = watch.Elapsed.TotalMilliseconds;
            while (true)
            {
                bool more = false; float progress = 0; Exception failure = null;
                try { more = steps.MoveNext(); if (more) progress = steps.Current; }
                catch (Exception e) { failure = e; }
                double now = watch.Elapsed.TotalMilliseconds;
                if (failure != null)
                {
                    Debug.LogException(failure);
                    loadingHole = false; loadingCover.Hide(true);
                    hud.Notice("Something went wrong — back to the menu", 2.5f);
                    SafeMenu();
                    yield break;
                }
                if (more) HoleLoadLog.Add((progress, now - previous));
                LongestLoadSliceMilliseconds = Math.Max(LongestLoadSliceMilliseconds, now - previous);
                previous = now;
                if (!more) break;
                loadingCover.SetProgress(progress, WordsFor(progress));
                yield return null;   // drawn: the ball rolls on, the next slice can take its turn
            }
            LastHoleLoadMilliseconds = watch.Elapsed.TotalMilliseconds;
            Debug.Log($"[HoleLoad] hole {hole.Number}: {LastHoleLoadMilliseconds:F0} ms in {HoleLoadLog.Count} slices, longest {LongestLoadSliceMilliseconds:F0} ms");
            loadingCover.SetProgress(1f, WordsFor(1));
            yield return null;
            while (loadingCover.ShownFor < LoadCoverMinimum) yield return null;
            loadingHole = false;
            loadingCover.Hide();
        }
    }
}
