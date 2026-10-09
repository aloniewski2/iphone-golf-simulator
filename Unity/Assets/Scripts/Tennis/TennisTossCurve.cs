using UnityEngine;

namespace GolfArcade.Tennis
{
    /// The toss meter's ticker position over time, kept apart from the on-screen meter
    /// (TennisTossMeter) so the host's rules and the tests can judge a press without any
    /// Unity objects.
    public static class TennisTossCurve
    {
        /// Seconds for the ticker to go end to end and back. It moves at a steady speed (not
        /// easing at the ends like a pendulum), so a given distance from the middle is always
        /// the same number of milliseconds and the timing can be learned.
        public const float Period = 1f;
        /// End-to-end speed, in half-widths per second.
        public const float Speed = 4 / Period;

        /// The ticker's position, -1 (left end) .. 1 (right end), `seconds` after it started.
        /// It starts at the left end, so the first pass through the middle comes after a beat.
        public static float PositionAt(float seconds) => Triangle(seconds / Period);

        /// 1 in the middle, 0 at either end.
        public static float AccuracyAt(float seconds) => 1 - Mathf.Abs(PositionAt(Mathf.Max(0, seconds)));

        static float Triangle(float cycles)
        {
            float p = Mathf.Repeat(cycles + .75f, 1f);
            return p < .25f ? 4 * p : p < .75f ? 2 - 4 * p : 4 * p - 4;
        }
    }
}
