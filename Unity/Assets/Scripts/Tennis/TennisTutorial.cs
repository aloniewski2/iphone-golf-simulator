using System;
using UnityEngine;

namespace GolfArcade.Tennis
{
    /// Short, playable lesson. Each exercise teaches a different control; misses retry.
    public sealed class TutorialLesson
    {
        public enum Kind { Serve, Forehand, Backhand, Aim, Point }
        public struct Step { public Kind Kind; public string Say, Hint; public int Goal; }
        public static readonly Step[] Steps =
        {
            new Step { Kind = Kind.Serve, Goal = 1,
                Say = "SERVE: press TOSS near the meter's middle. Swing overhead as the ball reaches its highest point. Land one in the box.",
                Hint = "Leave the aim in the middle. Press TOSS, watch the ball rise, then swing. If you miss the toss, try again." },
            new Step { Kind = Kind.Forehand, Goal = 1,
                Say = "FOREHAND: your player runs to my feed. Keep the phone screen facing the display and swing as the ball rises after its bounce.",
                Hint = "Wait for the bounce, then swing when the ball reaches your racket. A smooth swing is enough." },
            new Step { Kind = Kind.Backhand, Goal = 1,
                Say = "BACKHAND: turn the phone so its rear camera faces the display. Swing across your body after the bounce. Return one in.",
                Hint = "Turn the phone before the ball arrives, then swing across your chest. Your player handles the running." },
            new Step { Kind = Kind.Aim, Goal = 2,
                Say = "AIM LEFT: angle the phone's face left as you swing. Land a return on the left side, toward the gold ring.",
                Hint = "Keep the face angled through contact. In touch mode, use the shot-aim slider, then tap Swing." },
            new Step { Kind = Kind.Point, Goal = 1,
                Say = "PLAY A POINT: toss and serve, then return my shots. Use what you learned. Win or lose, the lesson is complete!",
                Hint = "Your player runs automatically. Watch the bounce and time your swing." },
        };
        public const int HintAfter = 3;
        public int Index { get; private set; }
        public int Progress { get; private set; }
        public int Misses { get; private set; }
        public bool Done => Index >= Steps.Length;
        public Step Current => Steps[Mathf.Min(Index, Steps.Length - 1)];
        public bool AimLeft => Progress == 0;
        public string Instruction => Current.Kind == Kind.Aim && !AimLeft
            ? "AIM RIGHT: now angle the face right and land a return toward the other gold ring."
            : Current.Say;
        public bool Success()
        {
            if (Done) return false;
            Misses = 0;
            if (++Progress < Current.Goal) return false;
            Next(); return true;
        }
        public bool Miss() { if (!Done) Misses++; return false; }
        public bool HintDue => Misses >= HintAfter;
        public void Skip() { if (!Done) Next(); }
        void Next() { Index++; Progress = 0; Misses = 0; }
    }

    public sealed class TennisTutorial : MonoBehaviour
    {
        public static event Action<int, int, string> StepChanged;
        public static event Action Finished;
        public TutorialLesson Lesson { get; private set; } = new TutorialLesson();
        TennisGame game;
        TennisCoach coach;
        LineRenderer marker;
        float feedAt = -1, serveAt = -1, watchdog = -1;
        bool resolved, active, tipsBefore, pendingStep;
        int feeds;
        static readonly Color Gold = new Color(1, .82f, .15f);

        public void Begin(TennisGame g, TennisCoach c)
        {
            Stop(); game = g; coach = c; Lesson = new TutorialLesson(); active = true; feeds = 0;
            tipsBefore = TennisCoach.TipsEnabled; TennisCoach.TipsEnabled = false;
            TennisGame.Landed += OnLanded; TennisGame.DrillPoint += OnDrillPoint; TennisGame.ScoreChanged += OnScore;
            if (!marker) marker = game.MakeMarker("Tutorial target", Gold);
            StartStep();
        }
        public void Stop()
        {
            if (!active) return;
            active = false;
            TennisGame.Landed -= OnLanded; TennisGame.DrillPoint -= OnDrillPoint; TennisGame.ScoreChanged -= OnScore;
            TennisCoach.TipsEnabled = tipsBefore;
            if (game) game.Drill = false;
            if (marker) marker.enabled = false;
        }
        void OnDestroy() { Stop(); if (marker) Destroy(marker.gameObject); }
        /// An explicit accessibility/replay option; missing balls never silently passes a lesson.
        public void SkipStep()
        {
            if (!active || pendingStep) return;
            Lesson.Skip(); Advance(true);
        }
        void ShowInstruction()
        {
            string text = Lesson.HintDue ? Lesson.Current.Hint : Lesson.Instruction;
            StepChanged?.Invoke(Lesson.Index, TutorialLesson.Steps.Length, Lesson.Instruction);
            if (coach) coach.Say(text, 1e6f);
        }
        void StartStep()
        {
            pendingStep = false; feedAt = serveAt = watchdog = -1; resolved = true;
            if (Lesson.Done) { Finish(); return; }
            ShowInstruction();
            game.Drill = Lesson.Current.Kind != TutorialLesson.Kind.Point;
            game.PrepareLesson();
            if (Lesson.Current.Kind == TutorialLesson.Kind.Serve || Lesson.Current.Kind == TutorialLesson.Kind.Point)
                serveAt = Time.time + 1.2f;
            else feedAt = Time.time + 1.8f;
        }
        void Finish()
        {
            if (!active) return;
            if (coach) coach.Say("You're ready. See you on the Island Circuit!", 10);
            if (marker) marker.enabled = false;
            game.Drill = false; Finished?.Invoke(); Stop();
        }
        void Update()
        {
            if (!active || !game || !game.Player || Time.timeScale == 0 || game.CheckingTiming) return;
            if (pendingStep) { StartStep(); return; }
            marker.enabled = Lesson.Current.Kind == TutorialLesson.Kind.Aim;
            if (marker.enabled)
                TennisGame.DrawRing(marker, new Vector3(Lesson.AimLeft ? -2.5f : 2.5f, .06f, 8f), 1.1f);
            if (feedAt >= 0 && Time.time >= feedAt)
            {
                feedAt = -1; resolved = false; watchdog = Time.time + 6f; feeds++;
                game.Feed(Lesson.Current.Kind == TutorialLesson.Kind.Backhand
                    || (Lesson.Current.Kind == TutorialLesson.Kind.Aim && feeds % 2 == 0));
            }
            if (serveAt >= 0 && Time.time >= serveAt)
            { serveAt = -1; resolved = false; game.StartPlayerServe(); }
            if (watchdog >= 0 && Time.time >= watchdog)
            {
                watchdog = -1;
                if (!resolved) { resolved = true; Advance(Lesson.Miss()); }
                NextBall();
            }
        }
        void Advance(bool stepEnded)
        {
            if (stepEnded)
            {
                // Landed/Score callbacks run inside physics. Let the old point finish first.
                pendingStep = true; feedAt = serveAt = watchdog = -1; return;
            }
            ShowInstruction();
        }
        void NextBall()
        {
            if (Lesson.Done || pendingStep) return;
            if (Lesson.Current.Kind == TutorialLesson.Kind.Serve)
            { if (serveAt < 0) serveAt = Time.time + 1.4f; }
            else if (Lesson.Current.Kind != TutorialLesson.Kind.Point && feedAt < 0)
                feedAt = Time.time + 1.6f;
        }
        void OnLanded(bool byPlayer, bool landedIn, Vector3 where, bool serve)
        {
            if (!active || pendingStep || !byPlayer || resolved || Lesson.Current.Kind == TutorialLesson.Kind.Point) return;
            if (Lesson.Current.Kind == TutorialLesson.Kind.Serve && !serve) return;
            resolved = true;
            bool success = landedIn;
            if (Lesson.Current.Kind == TutorialLesson.Kind.Aim)
                success &= Lesson.AimLeft ? where.x < -.8f : where.x > .8f;
            Advance(success ? Lesson.Success() : Lesson.Miss());
            if (serve) NextBall();
        }
        void OnDrillPoint(bool toPlayer, string why)
        {
            if (!active || pendingStep) return;
            if (!resolved) { resolved = true; Advance(Lesson.Miss()); }
            watchdog = -1; NextBall();
        }
        void OnScore(string line)
        {
            if (active && !pendingStep && Lesson.Current.Kind == TutorialLesson.Kind.Point)
                Advance(Lesson.Success());
        }
    }
}
