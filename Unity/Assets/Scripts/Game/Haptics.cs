using System.Runtime.InteropServices;

namespace GolfArcade.Game
{
    /// The phone in the player's hand: a tension that builds through the backswing, a thump at
    /// impact, ticks on the buttons, a flourish or a buzz for the result. Backed by the iOS
    /// CoreHaptics plugin in Plugins/iOS; everywhere else these are no-ops.
    public static class Haptics
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void GolfHaptics_Impact(float intensity);
        [DllImport("__Internal")] static extern void GolfHaptics_Selection();
        [DllImport("__Internal")] static extern void GolfHaptics_Notification(int type);
        [DllImport("__Internal")] static extern void GolfHaptics_TensionStart();
        [DllImport("__Internal")] static extern void GolfHaptics_TensionSet(float intensity, float sharpness);
        [DllImport("__Internal")] static extern void GolfHaptics_TensionStop();
        [DllImport("__Internal")] static extern void GolfHaptics_Pattern(int kind, float intensity);
        public const bool Available = true;
#else
        static void GolfHaptics_Impact(float intensity) { }
        static void GolfHaptics_Selection() { }
        static void GolfHaptics_Notification(int type) { }
        static void GolfHaptics_TensionStart() { }
        static void GolfHaptics_TensionSet(float intensity, float sharpness) { }
        static void GolfHaptics_TensionStop() { }
        static void GolfHaptics_Pattern(int kind, float intensity) { }
        public const bool Available = false;
#endif
        static bool tensionOn;

        /// The strike: intensity follows the meter, so a chip taps and a full drive thumps.
        public static void Impact(double power) => GolfHaptics_Impact(0.35f + 0.65f * (float)System.Math.Clamp(power, 0, 1));

        /// The top of the backswing: one crisp click as the club turns for home, so the hands
        /// feel the moment the swing changes direction.
        public static void Top(double load) => GolfHaptics_Pattern(0, (float)System.Math.Clamp(load, 0, 1));

        /// The strike, felt by how it was struck: a PERFECT one cracks and rings, a solid one
        /// knocks, a thin one stings dull and buzzy. Intensity still follows the power.
        public static void Strike(GolfArcade.Swing.StrikeGrade grade, double power)
        {
            float p = 0.35f + 0.65f * (float)System.Math.Clamp(power, 0, 1);
            int kind = grade switch
            {
                GolfArcade.Swing.StrikeGrade.Perfect => 1,
                GolfArcade.Swing.StrikeGrade.Thin => 3,
                _ => 2,
            };
            GolfHaptics_Pattern(kind, p);
        }

        /// The gallery going up for a holed ball: a rolling run of thumps.
        public static void Roar() => GolfHaptics_Pattern(4, 1);

        /// A light tick for a button or a change of club.
        public static void Tick() => GolfHaptics_Selection();

        public static void Success() => GolfHaptics_Notification(0);
        public static void Warning() => GolfHaptics_Notification(1);
        public static void Failure() => GolfHaptics_Notification(2);

        /// Backswing tension: call every load event with the meter's 0–1. It starts the buzz on
        /// the first call and grows it — soft and dull early, hard and sharp at the top.
        public static void Tension(double load)
        {
            float l = (float)System.Math.Clamp(load, 0, 1);
            if (!tensionOn) { GolfHaptics_TensionStart(); tensionOn = true; }
            GolfHaptics_TensionSet(0.15f + 0.85f * l * l, 0.2f + 0.7f * l);
        }

        public static void Release()
        {
            if (!tensionOn) return;
            tensionOn = false;
            GolfHaptics_TensionStop();
        }
    }
}
