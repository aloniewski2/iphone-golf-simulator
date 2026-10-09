// Unit tests of the plant sway driver (Unity/Assets/Scripts/Course/GolfWindSway.cs): the numbers the GolfPlants shader is fed and the C# mirror of its displacement.
// The GPU side (the shader really displaces like the mirror, only plants move, Metal compile, SRP Batcher) is Unity/Assets/Editor/GolfPlantSwayCheck.cs (work/postcard-look/v2/sway/run_swaycheck.sh).
using System;
using GolfArcade.Course;
using NUnit.Framework;
using UnityEngine;

namespace GolfArcade.Tests
{
    public class GolfWindSwayTests
    {
        [Test]
        public void AmplitudeIsMonotonicWithAFaintIdleBreeze()
        {
            float prev = -1;
            for (int mph = 0; mph <= 20; mph++)
            {
                float a = GolfWindSway.Amplitude(mph);
                Assert.Greater(a, prev, $"{mph} mph");
                prev = a;
            }
            Assert.Greater(GolfWindSway.Amplitude(0), 0f, "an idle breeze at 0 mph");
            Assert.AreEqual(.25f, GolfWindSway.Amplitude(0) / GolfWindSway.Amplitude(20), .001f, "idle = 25 % of full");
            Assert.AreEqual(GolfWindSway.AmpFullYards, GolfWindSway.Amplitude(Wind.MaxMPH), 1e-6f);
            Assert.AreEqual(GolfWindSway.Amplitude(20), GolfWindSway.Amplitude(55), 1e-6f, "clamped above 20 mph");
        }

        [TestCase(0.0)] [TestCase(45.0)] [TestCase(90.0)] [TestCase(135.0)] [TestCase(180.0)] [TestCase(-90.0)] [TestCase(270.0)]
        public void GlobalVectorPointsWhereTheWindBlowsTowardInWorldXZ(double degrees)
        {
            // Wind.DirectionDegrees = degrees right of +D (down the hole); HoleView.ToWorld: x = course x, z = course d
            var v = GolfWindSway.GlobalValue(new Wind(12, degrees), 3f);
            var want = new Vector2(Mathf.Sin((float)degrees * Mathf.Deg2Rad), Mathf.Cos((float)degrees * Mathf.Deg2Rad));
            var got = new Vector2(v.x, v.z).normalized;
            Assert.AreEqual(want.x, got.x, 1e-4f); Assert.AreEqual(want.y, got.y, 1e-4f);
            Assert.AreEqual(0f, v.y, 0f);
            Assert.AreEqual(3f, v.w, 1e-6f, "the clock");
        }

        [Test]
        public void ClockWrapsSeamlesslyAtTheLoop()
        {
            var wind = new Vector2(.04f, .02f);
            for (int k = 0; k < 20; k++)
            {
                float ox = k * 1.37f - 9, oz = k * .83f + 4, w = (k % 10 + 1) / 10f, ph = k * .05f;
                var a = GolfWindSway.Displacement(w, ph, ox, oz, wind, GolfWindSway.LoopSeconds - 1e-3f);
                var b = GolfWindSway.Displacement(w, ph, ox, oz, wind, 0f);
                Assert.Less(Vector2.Distance(a, b), 2e-4f, $"seam at pivot {k}");
            }
            Assert.AreEqual(.5f, GolfWindSway.GlobalValue(new Wind(5, 0), GolfWindSway.LoopSeconds + .5f).w, 1e-3f);
        }

        [Test]
        public void TheRootNeverMovesAndNoDataIsStatic()
        {
            var wind = GolfWindSway.Direction(33) * GolfWindSway.Amplitude(20);
            for (float t = 0; t < 120; t += .37f)
                Assert.AreEqual(0f, GolfWindSway.Displacement(0f, .3f, 12.5f, -40f, wind, t).magnitude, 0f, "weight 0 = pinned");
            Assert.AreEqual(0f, GolfWindSway.Displacement(1f, .3f, 12.5f, -40f, Vector2.zero, 5f).magnitude, 0f, "no wind vector set = no sway");
        }

        [Test]
        public void DisplacementIsLinearInTheWeightAndPointsDownwind()
        {
            var wind = GolfWindSway.Direction(90) * GolfWindSway.Amplitude(20);        // toward +x
            double sum = 0; int n = 0;
            for (float t = 0; t < 300; t += .41f)
            {
                var full = GolfWindSway.Displacement(1f, .4f, 7f, 3f, wind, t);
                var half = GolfWindSway.Displacement(.5f, .4f, 7f, 3f, wind, t);
                Assert.AreEqual(full.x * .5f, half.x, 1e-6f); Assert.AreEqual(full.y * .5f, half.y, 1e-6f);
                Assert.GreaterOrEqual(full.x, -1e-6f, "the lean is downwind only");
                sum += full.x; n++;
            }
            Assert.Greater(sum / n, .01, "the time-mean lean is downwind");
        }

        // the user's caps at 20 mph, yards, and the tip weights the authoring table asks for (RUNTIME.md "area U", U.0)
        [TestCase("tuft", .06f, 1.0f)] [TestCase("flower", .05f, .84f)] [TestCase("shrub", .03f, .50f)] [TestCase("vine", .08f, 1.0f)]
        public void TipDisplacementStaysUnderTheKindsCap(string kind, float cap, float tipWeight)
        {
            float worst = 0;
            for (int p = 0; p < 40; p++)
            {
                var wind = GolfWindSway.Direction(p * 9) * GolfWindSway.Amplitude(20);
                for (float t = 0; t < GolfWindSway.LoopSeconds; t += .43f) worst = Mathf.Max(worst, GolfWindSway.Displacement(tipWeight, (p * .07f) % 1f, p * 5.3f - 100, p * 2.9f + 9, wind, t).magnitude);
            }
            Assert.LessOrEqual(worst, cap, kind);
            Assert.LessOrEqual(worst, GolfWindSway.MaxDisplacement(tipWeight, 20) + 1e-5f, "the analytic bound");
        }

        [Test]
        public void NeighbouringClumpsDoNotMoveTogether()
        {
            var wind = GolfWindSway.Direction(60) * GolfWindSway.Amplitude(14);
            int identical = 0, pairs = 60;
            for (int i = 0; i < pairs; i++)
            {
                float ox = i * 3.1f, oz = -i * 1.7f; double maxDiff = 0;
                for (float t = 0; t < 30; t += .5f)
                    maxDiff = Math.Max(maxDiff, Vector2.Distance(GolfWindSway.Displacement(1f, .5f, ox, oz, wind, t), GolfWindSway.Displacement(1f, .5f, ox + .5f, oz + .3f, wind, t)));
                if (maxDiff < .01) identical++;
            }
            Assert.LessOrEqual(identical, pairs / 20, "at most 5 % of neighbouring pairs stay within 1 cm of each other over 30 s");
        }
    }
}
