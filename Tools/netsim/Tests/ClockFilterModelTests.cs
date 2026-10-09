using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace NetSim {
    /// Reference model of the shared-clock filter. The production filter is the Swift `ClockFilter` in
    /// GolfArcade/Multiplayer/MultiplayerModels.swift; it must stay line-for-line equivalent to this model
    /// (the fixed vector below is also asserted by GolfArcadeTests/MultiplayerTests.swift).
    ///
    /// A ping/pong gives one sample: rtt = c3 - c0 and offset = hostSentAt - (c0 + c3)/2 (host clock minus client clock).
    /// One slow leg skews a single sample by half the extra delay, so the filter keeps the samples with the lowest
    /// rtt (least queueing) and slews toward their median.
    public sealed class ClockFilterModel {
        public const int Window = 16, Best = 3, RttWindow = 8;
        public const double MaxSlew = .005, JumpThreshold = .1;
        readonly List<(double rtt, double offset)> samples = new();
        readonly List<double> rtts = new();
        public double Offset { get; private set; }
        public bool Locked { get; private set; }

        public double MedianRtt => Median(rtts.ToArray());

        public double Add(double rtt, double rawOffset) {
            if (double.IsNaN(rtt) || double.IsInfinity(rtt) || double.IsNaN(rawOffset) || double.IsInfinity(rawOffset) || rtt < 0) return Offset;
            samples.Add((rtt, rawOffset)); if (samples.Count > Window) samples.RemoveAt(0);
            rtts.Add(rtt); if (rtts.Count > RttWindow) rtts.RemoveAt(0);
            var lowest = samples.OrderBy(s => s.rtt).ThenBy(s => s.offset).Take(Best).Select(s => s.offset).ToArray();
            double target = Median(lowest);
            if (!Locked || Math.Abs(target - Offset) > JumpThreshold) { Offset = target; Locked = true; }
            else Offset += Math.Max(-MaxSlew, Math.Min(MaxSlew, target - Offset));
            return Offset;
        }

        public static double Median(double[] values) {
            if (values.Length == 0) return 0;
            var sorted = values.OrderBy(v => v).ToArray();
            int n = sorted.Length;
            return n % 2 == 1 ? sorted[n / 2] : (sorted[n / 2 - 1] + sorted[n / 2]) / 2;
        }
    }

    public class ClockFilterModelTests {
        // One ping/pong with outbound delay d1 and return delay d2 (seconds); the host clock is the client's + trueOffset.
        static (double rtt, double offset) Sample(double trueOffset, double c0, double d1, double d2) {
            double c3 = c0 + d1 + d2, hostSent = c0 + d1 + trueOffset;
            return (c3 - c0, hostSent - (c0 + c3) / 2);
        }

        // Simulates 10 s of pings at 5/s over a link with jitter and occasional one-way delay spikes.
        // Returns the errors (estimate minus the unavoidable bias from a permanently asymmetric path) after a 2 s warm-up,
        // for the latest-sample-only approach used today and for the filter.
        static (List<double> latest, List<double> filtered) Trial(Random rng) {
            double trueOffset = (rng.NextDouble() - .5) * 2000;
            double base1 = .01 + rng.NextDouble() * .07, base2 = Math.Max(.005, base1 + (rng.NextDouble() - .5) * .04);
            double bias = (base1 - base2) / 2;
            var filter = new ClockFilterModel();
            var latest = new List<double>(); var filtered = new List<double>();
            for (int i = 0; i < 50; i++) {
                double c0 = i * .2;
                double d1 = base1 + Exp(rng, .005), d2 = base2 + Exp(rng, .005);
                if (rng.NextDouble() < .10) { double spike = .05 + rng.NextDouble() * .10; if (rng.Next(2) == 0) d1 += spike; else d2 += spike; }
                var s = Sample(trueOffset, c0, d1, d2);
                double est = filter.Add(s.rtt, s.offset);
                if (i >= 10) { latest.Add(s.offset - (trueOffset + bias)); filtered.Add(est - (trueOffset + bias)); }
            }
            return (latest, filtered);
        }
        static double Exp(Random rng, double mean) => -mean * Math.Log(1 - rng.NextDouble());

        [Test] public void FilterKeepsTheSharedClockWithin20msWhereTheLatestSampleDoesNot() {
            var rng = new Random(20261009);
            int total = 0, latestBad = 0, filteredBad = 0; double worstFiltered = 0;
            for (int t = 0; t < 1000; t++) {
                var (latest, filtered) = Trial(rng);
                total += latest.Count;
                latestBad += latest.Count(e => Math.Abs(e) > .05);
                filteredBad += filtered.Count(e => Math.Abs(e) > .02);
                worstFiltered = Math.Max(worstFiltered, filtered.Max(Math.Abs));
            }
            TestContext.Progress.WriteLine($"clock model: {total} evaluated points; latest-sample error > 50 ms: {100.0 * latestBad / total:F2}%; " +
                $"filtered error > 20 ms: {100.0 * filteredBad / total:F3}%; worst filtered error {worstFiltered * 1000:F1} ms");
            Assert.GreaterOrEqual(100.0 * latestBad / total, 2.0, "The model should reproduce today's problem: single slow packets skew the clock by more than 50 ms.");
            Assert.LessOrEqual(100.0 * filteredBad / total, 0.1, "The filtered clock must stay within 20 ms of the truth (bias excluded) for 99.9% of points.");
        }

        [Test] public void TheFirstSampleLocksAtOnceAndLaterOnesSlew() {
            var f = new ClockFilterModel();
            Assert.AreEqual(100.0, f.Add(.04, 100.0), 1e-12);
            Assert.True(f.Locked);
            double after = f.Add(.04, 100.05);   // the median moves 25 ms, but the clock may only slew 5 ms per update
            Assert.AreEqual(100.0 + ClockFilterModel.MaxSlew, after, 1e-12);
        }

        [Test] public void AGenuineJumpOfTheClockIsFollowedAtOnce() {
            var f = new ClockFilterModel();
            for (int i = 0; i < 20; i++) f.Add(.04, 50.0);
            for (int i = 0; i < 16; i++) f.Add(.04, 51.0);   // e.g. the host restarted its clock
            Assert.AreEqual(51.0, f.Offset, 1e-9);
        }

        [Test] public void BadSamplesAreIgnored() {
            var f = new ClockFilterModel(); f.Add(.04, 10);
            Assert.AreEqual(10, f.Add(double.NaN, 99), 1e-12);
            Assert.AreEqual(10, f.Add(.04, double.PositiveInfinity), 1e-12);
            Assert.AreEqual(10, f.Add(-1, 99), 1e-12);
        }

        // Shared with GolfArcadeTests/MultiplayerTests.swift: the Swift filter must produce exactly these offsets.
        // Slow packets (rtt .2 and .3) are ignored; a 40 ms move of the host clock is followed 5 ms per update;
        // a 460 ms jump is followed at once.
        public static (double rtt, double offset)[] Vector() {
            var v = new List<(double, double)> { (.050, 100.000), (.030, 100.002), (.200, 100.090), (.040, 100.004), (.300, 99.900), (.032, 100.003) };
            for (int i = 0; i < 8; i++) v.Add((.031, 100.040));
            for (int i = 0; i < 4; i++) v.Add((.025, 100.500));
            return v.ToArray();
        }
        public static readonly double[] VectorOffsets = {
            100.000, 100.001, 100.002, 100.002, 100.002, 100.003, 100.003, 100.008, 100.013, 100.018, 100.023, 100.028, 100.033, 100.038, 100.040,
            100.500, 100.500, 100.500,
        };

        [Test] public void FixedVectorForTheSwiftPort() {
            var f = new ClockFilterModel();
            var got = new List<double>();
            foreach (var (rtt, offset) in Vector()) got.Add(Math.Round(f.Add(rtt, offset), 6));
            TestContext.Progress.WriteLine("vector offsets: " + string.Join(", ", got.Select(g => g.ToString("F6"))));
            TestContext.Progress.WriteLine("median rtt: " + Math.Round(f.MedianRtt, 6));
            Assert.AreEqual(VectorOffsets.Length, got.Count, "paste the printed offsets into VectorOffsets");
            for (int i = 0; i < got.Count; i++) Assert.AreEqual(VectorOffsets[i], got[i], 1e-9, "sample " + i);
        }
    }
}
