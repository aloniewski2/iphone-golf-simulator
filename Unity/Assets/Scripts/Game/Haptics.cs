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
        [DllImport("__Internal")] static extern void GolfHaptics_Strike(float quality, int twice);
        [DllImport("__Internal")] static extern void GolfHaptics_Notification(int type);
        [DllImport("__Internal")] static extern void GolfHaptics_TensionStart();
        [DllImport("__Internal")] static extern void GolfHaptics_TensionSet(float intensity, float sharpness);
        [DllImport("__Internal")] static extern void GolfHaptics_TensionStop();
        public const bool Available = true;
#else
        static void GolfHaptics_Impact(float intensity) { }
        static void GolfHaptics_Selection() { }
        static void GolfHaptics_Strike(float quality, int twice) { }
        static void GolfHaptics_Notification(int type) { }
        static void GolfHaptics_TensionStart() { }
        static void GolfHaptics_TensionSet(float intensity, float sharpness) { }
        static void GolfHaptics_TensionStop() { }
        public const bool Available = false;
#endif
        static bool tensionOn;
        public static bool Enabled = true;

        /// The strike: intensity follows the meter, so a chip taps and a full drive thumps.
        public static void Impact(double power) { if(Enabled) GolfHaptics_Impact(0.35f + 0.65f * (float)System.Math.Clamp(power, 0, 1)); }

        /// Racket on ball: a crisp click from the phone at the instant of contact, stronger for a
        /// cleaner hit, doubled for a super shot. Instant, where the TV's hit sound is not.
        public static void Strike(double quality, bool supercharged = false) { if(Enabled) GolfHaptics_Strike((float)System.Math.Clamp(quality, 0, 1), supercharged ? 1 : 0); }

        /// A light tick for a button or a change of club.
        public static void Tick() { if(Enabled) GolfHaptics_Selection(); }

        public static void Success() { if(Enabled) GolfHaptics_Notification(0); }
        public static void Warning() { if(Enabled) GolfHaptics_Notification(1); }
        public static void Failure() { if(Enabled) GolfHaptics_Notification(2); }

        /// Backswing tension: call every load event with the meter's 0–1. It starts the buzz on
        /// the first call and grows it — soft and dull early, hard and sharp at the top.
        public static void Tension(double load)
        {
            if(!Enabled) return;
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
