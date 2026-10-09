using System;
using UnityEngine;

namespace GolfArcade.Game
{
    /// Lesson state independent of course physics; only a tutorial component uses it.
    public sealed class GolfTutorialLesson
    {
        public enum Phase { Practice, Aim, Hole, Done }
        public Phase Current { get; private set; }
        public int Strokes { get; private set; }
        public bool Capped { get; private set; }
        public int Misses { get; private set; }
        public const int StrokeCap = 6;
        public bool Shot(bool aimed)
        {
            switch (Current) {
                case Phase.Practice: Current = Phase.Aim; return true;
                case Phase.Aim:
                    if (!aimed) { Misses++; return false; }
                    Current = Phase.Hole; Misses = 0; return true;
                case Phase.Hole:
                    Strokes++;
                    if (Strokes >= StrokeCap) { Capped = true; Current = Phase.Done; return true; }
                    break;
            }
            return false;
        }
        public bool Holed() {
            if (Current != Phase.Hole || Strokes == 0) return false;
            Current = Phase.Done; return true;
        }
        public void Skip() { if (Current != Phase.Done) Current++; }
    }

    public sealed class GolfTutorial : MonoBehaviour
    {
        public static event Action<int, int, string> StepChanged;
        public static event Action<int, bool> HoleDone;
        public static event Action<int, int, bool> StepResolved;
        public static event Action Finished;
        public GolfTutorialLesson Lesson { get; private set; } = new GolfTutorialLesson();
        GolfGame game;
        double heading;
        bool active, waiting, finished;
        float nextPrompt;
        public void Begin(GolfGame target) {
            Stop(); game = target; Lesson = new GolfTutorialLesson(); active = true; finished = false; waiting = false;
            heading = game.TutorialHeading; GolfGame.ShotStruck += Shot; Show();
        }
        public void Stop() { GolfGame.ShotStruck -= Shot; active = false; }
        void OnDestroy() { Stop(); }
        string Prompt => Lesson.Current switch {
            GolfTutorialLesson.Phase.Practice => "PRACTICE: take a smooth swing. On your phone, tap SWING.",
            GolfTutorialLesson.Phase.Aim => "AIM: use the left or right aim arrow, then swing again.",
            GolfTutorialLesson.Phase.Hole => "PLAY THE HOLE: aim toward the flag and swing. Six strokes is a friendly finish.",
            _ => Lesson.Capped ? "Pick it up — nice round!" : "In the hole — nice round!"
        };
        void Show(bool report = true) {
            if (report) StepChanged?.Invoke(Mathf.Min((int)Lesson.Current, 2), 3, Prompt);
            if (game.TutorialHud) game.TutorialHud.ShowBanner(Prompt, 4);
            nextPrompt = Time.time + 4;
        }
        void Shot() {
            if (!active || finished) return;
            int index = (int)Lesson.Current, misses = Lesson.Misses;
            bool changed = Lesson.Shot(Math.Abs(game.TutorialHeading - heading) > .01);
            if (changed) {
                if (index < 2) StepResolved?.Invoke(index, misses, false);
                heading = game.TutorialHeading;
                Show();
            }
            waiting = true;
        }
        public void SkipStep() {
            if (!active || finished) return;
            StepResolved?.Invoke((int)Lesson.Current, Lesson.Misses, true);
            Lesson.Skip(); Show();
            if (Lesson.Current == GolfTutorialLesson.Phase.Done) Complete();
        }
        void Update() {
            if (!active || !game || Time.timeScale == 0) return;
            bool settled = game.Current == GolfGame.State.Result || game.Current == GolfGame.State.HoleDone || game.Current == GolfGame.State.Aim;
            if (waiting && settled) {
                waiting = false;
                if (game.LastShot != null && game.LastShot.IsHoled) Lesson.Holed();
                if (Lesson.Current == GolfTutorialLesson.Phase.Done) { Complete(); return; }
                Show();
            }
            if (Time.time >= nextPrompt) Show(false);
        }
        void Complete() {
            if (finished) return;
            finished = true; Show();
            if (Lesson.Capped && game.Card != null) game.Card.Record(game.TutorialHole, GolfTutorialLesson.StrokeCap);
            HoleDone?.Invoke(Lesson.Strokes, Lesson.Capped);
            Finished?.Invoke(); Stop();
        }
    }
}
