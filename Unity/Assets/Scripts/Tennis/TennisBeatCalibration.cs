using System.Collections.Generic;
using UnityEngine;

namespace GolfArcade.Tennis
{
    /// The timing check: a ball bounces on the TV to a steady beat and the player swings on
    /// every bounce. Because the beat is predictable, the player swings *with* it rather than
    /// reacting to it, exactly as they time a ball in play, so how late each swing lands
    /// against the bounce the game drew is the whole chain: the TV's picture delay, the phone's
    /// swing detection, and the player's own habit. The median of those is the lag the game
    /// takes off every swing (see TennisLagLearner, which keeps refining it in play).
    ///
    /// The player is never shown early/late during the check: they would correct for it and
    /// hide the very delay being measured.
    public sealed class TennisBeatCalibration
    {
        /// Beat spacing (a relaxed 75 bpm, the rhythm of a rally), how many beats, and how
        /// many at the start only set the rhythm and are not scored.
        public const float Interval = .8f;
        public const int Beats = 9, WarmUp = 2;
        /// The lead-in before the first bounce, so the ball can be seen falling onto it.
        public const float LeadIn = 1.2f;
        /// A swing further than this from its bounce is not aimed at it.
        public const float Window = .38f;
        /// Scored swings needed, and how tightly they must agree (spread between the
        /// quartiles), for the result to be trusted.
        public const int Needed = 4;
        public const float MaxSpread = .12f;

        readonly float start;
        readonly List<float> late = new();
        readonly bool[] taken = new bool[Beats];

        /// The get-ready countdown before the ball starts dropping, so the player knows what is
        /// coming ("swing every time the ball lands on the line") before it begins.
        public const float Countdown = 5f;

        public TennisBeatCalibration(float now, float countdown = 0) { start = now + countdown; }

        /// Seconds left of the countdown (0 once the ball is on its way).
        public float CountdownLeft(float now) => Mathf.Max(0, start - now);

        public float BeatTime(int beat) => start + LeadIn + beat * Interval;
        public float End => BeatTime(Beats - 1) + Window;
        public bool Finished(float now) => now >= End;
        public int Scored => late.Count;

        /// Where the bouncing ball is, 0 on the line .. 1 at the top, and the beat it is
        /// heading for (-1 before the first).
        public float Height(float now, out int beat)
        {
            float t = now - (start + LeadIn);
            if (t < 0)
            {
                // Dropping in from above onto the first bounce.
                beat = -1;
                float k = Mathf.Clamp01(1 + t / LeadIn);
                return 1 - k * k;
            }
            beat = Mathf.Min(Beats - 1, Mathf.FloorToInt(t / Interval));
            float p = Mathf.Repeat(t, Interval) / Interval;
            return t > (Beats - 1) * Interval ? 0 : 4 * p * (1 - p);
        }

        /// The player swung at `now`. Returns the beat it counted for (-1 if none).
        public int Swing(float now)
        {
            int beat = Mathf.RoundToInt((now - start - LeadIn) / Interval);
            if (beat < 0 || beat >= Beats || taken[beat]) return -1;
            float off = now - BeatTime(beat);
            if (Mathf.Abs(off) > Window) return -1;
            taken[beat] = true;
            if (beat >= WarmUp) late.Add(off);
            return beat;
        }

        /// The measured lag (seconds), or false when there were too few swings or they were
        /// too scattered to be a rhythm.
        public bool TryResult(out float lag)
        {
            lag = 0;
            if (late.Count < Needed) return false;
            var sorted = new List<float>(late); sorted.Sort();
            float q1 = Quantile(sorted, .25f), q3 = Quantile(sorted, .75f);
            if (q3 - q1 > MaxSpread) return false;
            lag = Mathf.Clamp(Quantile(sorted, .5f), 0, TennisLagLearner.Max);
            return true;
        }

        static float Quantile(List<float> sorted, float q)
        {
            float i = q * (sorted.Count - 1);
            int lo = Mathf.FloorToInt(i), hi = Mathf.Min(sorted.Count - 1, lo + 1);
            return Mathf.Lerp(sorted[lo], sorted[hi], i - lo);
        }
    }
}
