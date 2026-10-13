using GolfArcade.Game;
using UnityEngine;

namespace GolfArcade.Tennis
{
    /// How a tennis contact feels in the hand: the game's own reading of the hit (the grade the TV calls,
    /// where it met the strings, how early or late, the stroke, the streak) handed to the phone's haptics.
    /// One place, so rally, serve and online hits all speak the same language.
    public static class TennisHitFeel
    {
        /// Beyond this lateness a hit has left GOOD (TennisRules.TimingScore), so it feels fully mistimed.
        const float MistimedAt = .14f;

        public static Haptics.HitStyle Style(TennisActor.Stroke kind) => kind switch
        {
            TennisActor.Stroke.Smash => Haptics.HitStyle.Heavy,
            TennisActor.Stroke.Serve => Haptics.HitStyle.Serve,
            TennisActor.Stroke.Volley or TennisActor.Stroke.Lob or TennisActor.Stroke.Slice or TennisActor.Stroke.LowPickup => Haptics.HitStyle.Touch,
            TennisActor.Stroke.Dive => Haptics.HitStyle.Scramble,
            _ => Haptics.HitStyle.Drive,
        };

        /// A rally contact. `faceOffset` is where it met the strings (metres), `late` how late the swing was
        /// (real seconds, negative early), `streak` the clean balls now banked toward a supercharge.
        public static Haptics.HitFeel Rally(Timing grade, Vector2 faceOffset, float late, TennisActor.Stroke kind, float power,
            int streak, bool supercharged, int rallyShots) => new Haptics.HitFeel
        {
            Style = Style(kind),
            Grade = (int)grade,
            Pips = supercharged ? 0 : Mathf.Clamp(streak, 0, TennisRules.SuperchargeStreak - 1),
            Supercharged = supercharged,
            Power = power,
            Center = 1 - Mathf.Clamp01(TennisRules.FaceError(faceOffset)),
            Mistime = Mathf.Clamp01(Mathf.Abs(late) / MistimedAt),
            Heat = Mathf.Clamp01((rallyShots - 2) / 10f),
        };

        /// The player's serve, graded as the TV grades it (LaunchServe's ShowGrade); a fault is a dull one.
        public static Haptics.HitFeel Serve(TennisRules.ServeJudgement verdict) => new Haptics.HitFeel
        {
            Style = Haptics.HitStyle.Serve,
            Grade = (int)(!verdict.Legal ? Timing.Ok : verdict.Perfect ? Timing.Perfect : verdict.Power > .75f ? Timing.Excellent
                : verdict.Power > .45f ? Timing.Great : Timing.Good),
            Power = verdict.Power,
            Center = verdict.Legal ? Mathf.Lerp(.6f, 1, verdict.Accuracy) : .35f,
            Mistime = verdict.Legal ? 0 : .5f,
        };

        /// An online hit, where the host sends only the grade.
        public static Haptics.HitFeel Graded(Timing grade, float power) => new Haptics.HitFeel
        {
            Style = Haptics.HitStyle.Drive,
            Grade = (int)grade,
            Power = power,
            Center = TennisRules.GradeFeel(grade),
            Mistime = 1 - TennisRules.GradeFeel(grade),
        };
    }
}
