using System.Runtime.InteropServices;

namespace GolfArcade.Game
{
    /// The phone in the player's hand: a tension that builds through the backswing, a strike that says how
    /// the ball was struck, ticks on the buttons, and the point or the hole felt as the TV shows it. Backed by
    /// the iOS CoreHaptics plugin in Plugins/iOS (GolfHaptics.mm, with the strike shapes in GolfHitFeel.h);
    /// everywhere else these are no-ops.
    public static class Haptics
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void GolfHaptics_Warm();
        [DllImport("__Internal")] static extern void GolfHaptics_Hit(int style, int grade, int pips, int flags, float power, float center, float mistime, float heat);
        [DllImport("__Internal")] static extern void GolfHaptics_Cue(int kind, float intensity, float delay);
        [DllImport("__Internal")] static extern void GolfHaptics_Selection();
        [DllImport("__Internal")] static extern void GolfHaptics_Notification(int type);
        [DllImport("__Internal")] static extern void GolfHaptics_TensionStart();
        [DllImport("__Internal")] static extern void GolfHaptics_TensionSet(float intensity, float sharpness);
        [DllImport("__Internal")] static extern void GolfHaptics_TensionStop();
        public const bool Available = true;
#else
        static void GolfHaptics_Warm() { }
        static void GolfHaptics_Hit(int style, int grade, int pips, int flags, float power, float center, float mistime, float heat) { }
        static void GolfHaptics_Cue(int kind, float intensity, float delay) { }
        static void GolfHaptics_Selection() { }
        static void GolfHaptics_Notification(int type) { }
        static void GolfHaptics_TensionStart() { }
        static void GolfHaptics_TensionSet(float intensity, float sharpness) { }
        static void GolfHaptics_TensionStop() { }
        public const bool Available = false;
#endif
        static bool tensionOn;
        /// Real time of the last top-of-backswing click: the downswing that follows is kept silent.
        static float toppedAt = -10;
        public static bool Enabled = true;
        /// Off for a moment that is not this phone's to feel: another phone's shot in a multiplayer round.
        public static bool Suppressed;
        static bool On => Enabled && !Suppressed;

        /// The kind of stroke, which decides how much weight a strike carries (GolfFeel::Style).
        public enum HitStyle { Drive, Touch, Heavy, Serve, Scramble, GolfFull, GolfPutt }

        /// Moments that are not a strike (GolfFeel::Cue).
        public enum Cue { Swing, Whiff, Bounce, PointWon, PointLost, GameWon, MatchWon, Top, Roar }

        /// Everything the hand should learn from one strike.
        public struct HitFeel
        {
            public HitStyle Style;
            /// The grade called on the TV, 0 missed .. 5 PERFECT (TennisRules.Timing order).
            public int Grade;
            /// Clean balls banked toward a supercharge: one small tap each after the strike.
            public int Pips;
            public bool Supercharged;
            /// 0..1: how hard the swing was; how near the sweet spot (1 = dead centre); how far off the
            /// timing was (0 = on time); how long the rally has run.
            public float Power, Center, Mistime, Heat;
        }

        static float Clamp01(double v) => (float)System.Math.Clamp(v, 0, 1);

        /// Start the engine (and make the phone-speaker knocks) before the first strike needs them.
        public static void Warm() { if (Enabled) GolfHaptics_Warm(); }

        /// A strike, felt by how it was struck: a crack as bright as the contact was clean, a body as heavy
        /// as the stroke, the frame stinging after a scrappy one, and the grade's reward after it.
        public static void Hit(in HitFeel f)
        {
            if (!On) return;
            GolfHaptics_Hit((int)f.Style, System.Math.Clamp(f.Grade, 0, 5), System.Math.Max(0, f.Pips), f.Supercharged ? 1 : 0,
                Clamp01(f.Power), Clamp01(f.Center), Clamp01(f.Mistime), Clamp01(f.Heat));
        }

        /// A moment, `delay` real seconds from now: pass the TV's lag for something the player sees on the
        /// TV, so the hand and the picture agree.
        public static void Play(Cue cue, float intensity = 1, float delay = 0)
        {
            if (On) GolfHaptics_Cue((int)cue, Clamp01(intensity), System.Math.Clamp(delay, 0, 1));
        }

        /// The phone has confirmed a swing: a soft swish, held back a moment so that a strike in the same
        /// instant replaces it instead of landing on top of it.
        public static void SwingFelt() => Play(Cue.Swing);

        /// A putt: a soft, short tock that grows with the length of the stroke.
        public static void Putt(double power) => Hit(new HitFeel { Style = HitStyle.GolfPutt, Grade = 3, Power = Clamp01(power), Center = 1 });

        /// The top of the backswing: one crisp click as the club turns for home, and then silence through
        /// the downswing, so the strike lands out of a quiet hand instead of under the wind-up buzz.
        public static void Top(double load)
        {
            if (!On) return;
            StopTension();
            toppedAt = UnityEngine.Time.realtimeSinceStartup;
            Play(Cue.Top, Clamp01(load));
        }

        /// The strike, felt by how it was struck: a PERFECT one cracks and rings, a solid one knocks, a thin
        /// one stings dull and buzzy. Weight still follows the power.
        public static void Strike(GolfArcade.Swing.StrikeGrade grade, double power)
        {
            var feel = new HitFeel { Style = HitStyle.GolfFull, Power = Clamp01(power) };
            switch (grade)
            {
                case GolfArcade.Swing.StrikeGrade.Perfect: feel.Grade = 5; feel.Center = 1; break;
                case GolfArcade.Swing.StrikeGrade.Great: feel.Grade = 3; feel.Center = .85f; feel.Mistime = .2f; break;
                case GolfArcade.Swing.StrikeGrade.Good: feel.Grade = 2; feel.Center = .7f; feel.Mistime = .35f; break;
                default: feel.Grade = 1; feel.Center = .25f; feel.Mistime = .45f; break;
            }
            Hit(feel);
        }

        /// The gallery going up for a holed ball: a rolling run of thumps.
        public static void Roar() => Play(Cue.Roar);

        /// A light tick for a button or a change of club.
        public static void Tick() { if(On) GolfHaptics_Selection(); }

        public static void Success() { if(On) GolfHaptics_Notification(0); }
        public static void Warning() { if(On) GolfHaptics_Notification(1); }
        public static void Failure() { if(On) GolfHaptics_Notification(2); }

        /// Backswing tension: call every load event with the meter's 0–1. It starts the buzz on
        /// the first call and grows it — soft and dull early, hard and sharp at the top.
        public static void Tension(double load)
        {
            if(!On) return;
            // After the top the hand stays quiet for the downswing (load events can still arrive then).
            if (UnityEngine.Time.realtimeSinceStartup - toppedAt < 1.5f) return;
            float l = Clamp01(load);
            if (!tensionOn) { GolfHaptics_TensionStart(); tensionOn = true; }
            GolfHaptics_TensionSet(0.15f + 0.85f * l * l, 0.2f + 0.7f * l);
        }

        public static void Release()
        {
            toppedAt = -10;
            StopTension();
        }

        static void StopTension()
        {
            if (!tensionOn) return;
            tensionOn = false;
            GolfHaptics_TensionStop();
        }
    }
}
