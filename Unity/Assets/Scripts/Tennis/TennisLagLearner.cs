using System.Collections.Generic;
using UnityEngine;

namespace GolfArcade.Tennis
{
    /// How far behind the game the player's timing runs, in real seconds: the TV's picture
    /// delay plus whatever the player's own habit adds. The phone measures the TV once with
    /// its camera, but that measurement can fail or drift (a TV switching picture modes, a
    /// busy Wi-Fi link), and one bad reading makes every serve and swing late with nothing
    /// the player can do about it.
    ///
    /// So the game also learns it from play, the way rhythm games calibrate: every serve,
    /// toss press and rally hit says how late the player was against the moment they were
    /// aiming for, before any compensation. The estimate is the median of the recent ones,
    /// with the measured delay counted as two votes, so a steady bias is corrected within
    /// three or four swings while single flukes are ignored. It corrects the average only;
    /// the spread -- the skill -- is untouched.
    public sealed class TennisLagLearner
    {
        public const int Window = 9;
        /// The most it will ever compensate, and how far from the current estimate a sample
        /// may be before it is written off as a fluke (a panicked swing, a missed ball).
        public const float Max = .35f, Outlier = .3f;

        readonly List<float> samples = new();
        readonly List<float> scratch = new();
        float prior;

        /// The phone's camera measurement of the TV's delay (0 = none).
        public float Prior { get => prior; set => prior = Mathf.Clamp(value, 0, Max); }
        public int Count => samples.Count;

        /// Seconds to take off every swing and press.
        public float Estimate
        {
            get
            {
                if (samples.Count == 0) return prior;
                scratch.Clear(); scratch.AddRange(samples); scratch.Add(prior); scratch.Add(prior);
                scratch.Sort();
                int n = scratch.Count;
                float median = n % 2 == 1 ? scratch[n / 2] : (scratch[n / 2 - 1] + scratch[n / 2]) * .5f;
                return Mathf.Clamp(median, 0, Max);
            }
        }

        /// The player was `rawLate` real seconds late (negative: early) against what they were
        /// timing, with no compensation applied.
        public void Observe(float rawLate)
        {
            if (float.IsNaN(rawLate) || Mathf.Abs(rawLate - Estimate) > Outlier) return;
            samples.Add(rawLate);
            if (samples.Count > Window) samples.RemoveAt(0);
        }

        /// Start from a previous session's estimate (same TV): counts as a few samples.
        public void Seed(float estimate, int votes = 3)
        {
            samples.Clear();
            for (int i = 0; i < votes; i++) samples.Add(Mathf.Clamp(estimate, 0, Max));
        }

        public void Reset() => samples.Clear();
    }
}
