using UnityEngine;

namespace GolfArcade.Tennis
{
    public sealed partial class TennisGame
    {
        public bool AimPractice { get; private set; }
        public int AimPracticeSequence { get; private set; }
        LineRenderer practiceTarget;
        bool practicePreviousDrill;
        int practiceHits, practiceMisses, practiceReturns, practiceStreak, practiceLongest;
        int practiceIncoming, practiceJumps, practiceDives, practiceReachable;
        int[] practiceSideBalls, practiceSideMisses;
        float practiceStarted;
        int[] practiceGrades;

        public void SetAimPractice(bool active)
        {
            if (!Player || AimPractice == active) return;
            if (active) {
                practicePreviousDrill = Drill;
                practiceHits = Hits; practiceMisses = Misses; practiceReturns = Returns;
                practiceStreak = Streak; practiceLongest = LongestRally; practiceStarted = Time.time;
                practiceIncoming = IncomingBalls; practiceJumps = GoodJumps; practiceDives = Dives; practiceReachable = ReachablePlans;
                practiceSideBalls = (int[])sideBalls.Clone(); practiceSideMisses = (int[])sideMisses.Clone();
                practiceGrades = coach ? (int[])coach.GradeCounts.Clone() : null;
                if (presentation) presentation.Skip();
                AimPractice = Drill = true;
                PrepareLesson();
                if (hud) hud.MatchVisible = false;
                if (!practiceTarget) practiceTarget = MakeMarker("Aiming calibration target", new Color(.8f, .94f, .3f));
            } else {
                AimPractice = false; Drill = practicePreviousDrill;
                Hits = practiceHits; Misses = practiceMisses; Returns = practiceReturns;
                Streak = practiceStreak; LongestRally = practiceLongest;
                IncomingBalls = practiceIncoming; GoodJumps = practiceJumps; Dives = practiceDives; ReachablePlans = practiceReachable;
                System.Array.Copy(practiceSideBalls, sideBalls, sideBalls.Length); System.Array.Copy(practiceSideMisses, sideMisses, sideMisses.Length);
                statStartedAt += Time.time - practiceStarted;
                if (coach && practiceGrades != null) System.Array.Copy(practiceGrades, coach.GradeCounts, practiceGrades.Length);
                if (practiceTarget) practiceTarget.enabled = false;
                if (hud) hud.MatchVisible = true;
                Refeed();
            }
        }

        public void FeedAimPractice(float direction, bool backhand, int sequence = 0)
        {
            if (!AimPractice || !Player) return;
            PrepareLesson();
            AimPracticeSequence = sequence;
            Vector3 target = TennisRules.PlacementTarget(Mathf.Clamp(direction, -1, 1) * .8f, .75f);
            target.y = .07f;
            practiceTarget.enabled = true;
            DrawRing(practiceTarget, target, .9f);
            if (coach) coach.Say((backhand ? "BACKHAND" : "FOREHAND") + " · AIM " + (direction < -.3f ? "LEFT" : direction > .3f ? "RIGHT" : "STRAIGHT"), 12);
            Feed(backhand, 19f);
        }
    }
}
