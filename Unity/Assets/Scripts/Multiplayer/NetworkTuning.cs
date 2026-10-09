using System;

namespace GolfArcade.Multiplayer {
    /// Every network fairness/feel constant in one place (PLAN_Multiplayer_OnlineLocal.md, sections 3.3 and 5).
    /// A swing is judged as the player SAW it: the host rewinds the world by
    /// (network time + sensor age + the player's screen delay), up to the limits below.
    public static class NetworkTuning {
        /// Most seconds the host rewinds to judge a swing. Covers network + sensor + screen delay.
        public const double MaxSwingRewind = .40;
        /// Same limit for the serve-toss press. Unchanged from before the screen-delay credit existed.
        public const double MaxTossRewind = .40;
        /// Most screen delay (TV/AirPlay lag) one phone may claim for itself.
        public const double MaxDisplayCredit = .30;
        /// Largest `age` a packet may carry: sensor age plus screen delay.
        public const double MaxInputAge = .50;
        /// Largest sensor age on its own (how long ago the phone's sensors saw the swing).
        public const double MaxSensorAge = .25;
        /// How far in the future an input's timestamp may be before the host treats the clocks as skewed.
        public const double FutureTolerance = .05;
        /// How old an input's timestamp may be before the host ignores it.
        public const double PastTolerance = .5;
        /// When a ball the receiver failed to return is dead but the receiver HAS started a swing whose
        /// confirmation has not arrived yet (slow TV or link), the host waits at most this long (from hearing
        /// the swing start) before awarding the point.
        public const double PendingSwingHold = .35;
        /// A guest that has heard nothing from the host for this long shows "Reconnecting..." (the 2 s Stale label comes later).
        public const double QuietSeconds = .4;
        /// The host's 30-per-second tennis updates use the compact form (NetworkTennisWire): under a third of the bytes, small enough to go
        /// as an unreliable message. The host also tests the form with this build's own JSON at match start and falls back to the full
        /// form if that fails. Set to false to send the full form always (guests read either).
        public const bool CompactSnapshots = true;

        /// The screen delay a phone may credit to its own swings.
        public static double ScreenCredit(double screenDelay) =>
            double.IsNaN(screenDelay) ? 0 : Math.Min(MaxDisplayCredit, Math.Max(0, screenDelay));

        /// `age` for a swing's confirmation: how long ago the sensors saw it, plus the screen delay
        /// (the player swung at a picture that was already that old).
        public static double SwingAge(double sensorAge, double screenDelay) {
            double sensor = double.IsNaN(sensorAge) ? 0 : Math.Min(MaxSensorAge, Math.Max(0, sensorAge));
            return Math.Min(MaxInputAge, sensor + ScreenCredit(screenDelay));
        }

        /// `age` for a swing's start: the screen delay only (the start is stamped when the phone processes it).
        public static double OnsetAge(double screenDelay) => ScreenCredit(screenDelay);
    }
}
