using System;
using UnityEngine;

namespace GolfArcade.Tennis
{
    /// The first-time tennis lesson, as plain state: the steps, what counts toward each, and
    /// when to hint or move on. Kept separate from the court so it can be tested directly.
    public sealed class TutorialLesson
    {
        public enum Kind { Move, Forehand, Backhand, Aim, Timing, Serve, Point }
        public struct Step { public Kind Kind; public string Say, Hint; public int Goal; }

        public static readonly Step[] Steps =
        {
            new Step { Kind = Kind.Move, Goal = 2, Say = "Step LEFT onto the blue ring, then RIGHT — your player follows you.",
                Hint = "Take a real step sideways. Small shuffles barely move you." },
            new Step { Kind = Kind.Forehand, Goal = 3, Say = "FOREHAND: I'll feed your racket side. Swing as the ball comes up off the bounce. Land 3 in.",
                Hint = "Screen toward the TV, and swing when the ball is level with you." },
            new Step { Kind = Kind.Backhand, Goal = 3, Say = "BACKHAND: now your other side. Turn the phone and swing across your body. Land 3 in.",
                Hint = "Start the backhand a touch earlier — take the phone back across your chest." },
            new Step { Kind = Kind.Aim, Goal = 2, Say = "AIM: point the racket face left to hit the LEFT ring, then right for the RIGHT ring.",
                Hint = "Tilt the phone's face where you want the ball to go, and hold it through the swing." },
            new Step { Kind = Kind.Timing, Goal = 2, Say = "TIMING: hit 2 balls GOOD or better — swing as the ball reaches the top of its bounce.",
                Hint = "Watch the ring at your feet: swing as the ball crosses it." },
            new Step { Kind = Kind.Serve, Goal = 2, Say = "SERVE: press TOSS as the meter hits the middle, then swing down hard. Land 2 in.",
                Hint = "Aim for the middle of the box — with a loose toss, the lines are risky." },
            new Step { Kind = Kind.Point, Goal = 1, Say = "Last one: play a real point against me. Win or lose, you're done!",
                Hint = "Get back to the middle after every shot." },
        };
        /// Misses before a hint, and before the lesson moves on anyway so nobody gets stuck.
        public const int HintAfter = 3, MoveOnAfter = 8;

        public int Index { get; private set; }
        public int Progress { get; private set; }
        public int Misses { get; private set; }
        public bool Done => Index >= Steps.Length;
        public Step Current => Steps[Mathf.Min(Index, Steps.Length - 1)];
        /// Aim: the left ring first, then the right.
        public bool AimLeft => Progress == 0;

        /// One success; true when it finished the step.
        public bool Success()
        {
            if (Done) return false;
            Progress++;
            if (Progress < Current.Goal) return false;
            Next(); return true;
        }

        /// One miss; true when the lesson gave up on the step and moved on.
        public bool Miss()
        {
            if (Done) return false;
            Misses++;
            if (Misses < MoveOnAfter) return false;
            Next(); return true;
        }

        public bool HintDue => Misses == HintAfter;

        void Next() { Index++; Progress = 0; Misses = 0; }
    }

    /// Runs the lesson on the court: Coach Ray feeds balls (the game in drill mode, so nothing
    /// is scored and he doesn't rally), draws target rings, says what to do next, and tells the
    /// phone each step (TennisTutorial.StepChanged → "tutorialStep") and the end ("tutorialDone").
    public sealed class TennisTutorial : MonoBehaviour
    {
        public static event Action<int, int, string> StepChanged;
        public static event Action Finished;

        public TutorialLesson Lesson { get; private set; } = new TutorialLesson();
        TennisGame game;
        TennisCoach coach;
        LineRenderer marker;
        float feedAt = -1, serveAt = -1, watchdog = -1;
        bool resolved, active, tipsBefore;
        int feeds;
        static readonly Color Blue = new Color(.2f, .75f, 1f), Gold = new Color(1, .82f, .15f);

        public void Begin(TennisGame g, TennisCoach c)
        {
            Stop();
            game = g; coach = c; Lesson = new TutorialLesson(); active = true; feeds = 0;
            tipsBefore = TennisCoach.TipsEnabled; TennisCoach.TipsEnabled = false;
            TennisGame.Landed += OnLanded; TennisGame.DrillPoint += OnDrillPoint;
            TennisGame.ContactMade += OnContact; TennisGame.ScoreChanged += OnScore;
            if (!marker) marker = game.MakeMarker("Tutorial target", Blue);
            StartStep();
        }

        public void Stop()
        {
            if (!active) return;
            active = false;
            TennisGame.Landed -= OnLanded; TennisGame.DrillPoint -= OnDrillPoint;
            TennisGame.ContactMade -= OnContact; TennisGame.ScoreChanged -= OnScore;
            TennisCoach.TipsEnabled = tipsBefore;
            if (game) game.Drill = false;
            if (marker) marker.enabled = false;
        }

        void OnDestroy() { Stop(); if (marker) Destroy(marker.gameObject); }

        /// Captures and self-play: skip the current step (self-play cannot walk to the rings).
        public void SkipStep()
        {
            if (!active) return;
            var kind = Lesson.Current.Kind;
            while (!Lesson.Done && Lesson.Current.Kind == kind) Lesson.Success();
            StartStep();
        }

        void StartStep()
        {
            feedAt = serveAt = watchdog = -1; resolved = true;
            if (Lesson.Done) { Finish(); return; }
            var step = Lesson.Current;
            StepChanged?.Invoke(Lesson.Index, TutorialLesson.Steps.Length, step.Say);
            if (coach) coach.Say(step.Say, 1e6f);
            game.Drill = step.Kind != TutorialLesson.Kind.Point;
            switch (step.Kind)
            {
                case TutorialLesson.Kind.Move: break;
                case TutorialLesson.Kind.Serve: case TutorialLesson.Kind.Point: serveAt = Time.time + 1.2f; break;
                default: feedAt = Time.time + 1.8f; break;
            }
        }

        void Finish()
        {
            if (!active) return;
            if (coach) coach.Say("That's it — you're ready. See you on the Island Circuit!", 10);
            if (marker) marker.enabled = false;
            game.Drill = false;
            Finished?.Invoke();
            Stop();
        }

        void Update()
        {
            if (!active || !game || !game.Player) return;
            var step = Lesson.Current;
            // Targets.
            marker.enabled = step.Kind == TutorialLesson.Kind.Move || step.Kind == TutorialLesson.Kind.Aim;
            if (step.Kind == TutorialLesson.Kind.Move)
            {
                float x = Lesson.Progress == 0 ? -2.2f : 2.2f;
                var at = new Vector3(x, .05f, game.Player.transform.position.z);
                TennisGame.DrawRing(marker, at, .6f);
                marker.startColor = marker.endColor = Blue;
                if (Mathf.Abs(game.Player.transform.position.x - x) < .45f) Advance(Lesson.Success());
            }
            else if (step.Kind == TutorialLesson.Kind.Aim)
            {
                TennisGame.DrawRing(marker, new Vector3(Lesson.AimLeft ? -2.5f : 2.5f, .06f, 8f), 1.1f);
                marker.startColor = marker.endColor = Gold;
            }
            // Feeds and serves, and a watchdog so a ball that never resolves is fed again.
            if (feedAt >= 0 && Time.time >= feedAt)
            {
                feedAt = -1; resolved = false; watchdog = Time.time + 6f; feeds++;
                bool backhand = step.Kind == TutorialLesson.Kind.Backhand
                    || ((step.Kind == TutorialLesson.Kind.Aim || step.Kind == TutorialLesson.Kind.Timing) && feeds % 2 == 0);
                game.Feed(backhand);
            }
            if (serveAt >= 0 && Time.time >= serveAt) { serveAt = -1; game.StartPlayerServe(); }
            if (watchdog >= 0 && Time.time >= watchdog) { watchdog = -1; if (!resolved) { resolved = true; Advance(Lesson.Miss()); } Next(); }
        }

        /// After a result: hint on the third miss, next step if this one is finished.
        void Advance(bool stepEnded)
        {
            if (stepEnded) { StartStep(); return; }
            if (Lesson.HintDue && coach) coach.Say(Lesson.Current.Hint, 7);
            else if (coach && Lesson.Progress > 0) coach.Say($"{Lesson.Current.Say}   ({Lesson.Progress}/{Lesson.Current.Goal})", 1e6f);
        }

        /// Queue what comes after a ball in the current step.
        void Next()
        {
            if (Lesson.Done) return;
            switch (Lesson.Current.Kind)
            {
                case TutorialLesson.Kind.Forehand: case TutorialLesson.Kind.Backhand:
                case TutorialLesson.Kind.Aim: case TutorialLesson.Kind.Timing:
                    if (feedAt < 0) feedAt = Time.time + 1.6f; break;
                case TutorialLesson.Kind.Serve:
                    if (serveAt < 0) serveAt = Time.time + 1.4f; break;
            }
        }

        void OnLanded(bool byPlayer, bool landedIn, Vector3 where, bool serve)
        {
            if (!active || !byPlayer) return;
            var kind = Lesson.Current.Kind;
            switch (kind)
            {
                case TutorialLesson.Kind.Forehand: case TutorialLesson.Kind.Backhand:
                    if (resolved) return;
                    resolved = true; Advance(landedIn ? Lesson.Success() : Lesson.Miss()); break;
                case TutorialLesson.Kind.Aim:
                    if (resolved) return;
                    resolved = true;
                    bool rightSide = Lesson.AimLeft ? where.x < -.8f : where.x > .8f;
                    Advance(landedIn && rightSide ? Lesson.Success() : Lesson.Miss()); break;
                case TutorialLesson.Kind.Serve:
                    if (!serve) return;
                    Advance(landedIn ? Lesson.Success() : Lesson.Miss()); break;
            }
        }

        void OnDrillPoint(bool toPlayer, string why)
        {
            if (!active) return;
            // A fed ball that ended without a landing of the player's: missed it.
            var kind = Lesson.Current.Kind;
            if (!resolved && kind != TutorialLesson.Kind.Serve && kind != TutorialLesson.Kind.Move)
            { resolved = true; Advance(Lesson.Miss()); }
            watchdog = -1;
            Next();
        }

        void OnContact(Vector2 face, Timing grade, bool super)
        {
            if (!active || Lesson.Current.Kind != TutorialLesson.Kind.Timing || resolved) return;
            resolved = true;
            Advance(grade >= Timing.Good ? Lesson.Success() : Lesson.Miss());
        }

        void OnScore(string line)
        {
            // Only a real (non-drill) point reports a score: the last step's point is over.
            if (!active || Lesson.Current.Kind != TutorialLesson.Kind.Point) return;
            Advance(Lesson.Success());
        }
    }
}
