using System;
using UnityEngine;
using UnityEngine.Profiling;

namespace GolfArcade.Game
{
    /// Which frame rate to ask for. 120 is only requested when the player turned it on AND
    /// the panel refreshes that fast; asking a 60Hz panel for 120 just burns battery.
    public static class FrameRate
    {
        public static int Target(int requested, double panelHz) =>
            requested >= 120 && panelHz >= 100 ? 120 : 60;
    }

    /// Frame-time probe. Records every frame into a fixed ring (no allocation per frame) and
    /// reports average fps, the 1% low, hitches and garbage-collector activity every
    /// `Interval` seconds. On the phone the report goes to the diagnostics log, so a real
    /// device run leaves numbers behind instead of an impression.
    public sealed class FrameProbe : MonoBehaviour
    {
        public const float Interval = 10f;
        public Action<string> Report;
        public Stats Last { get; private set; }
        readonly float[] frames = new float[2048];
        readonly float[] sorted = new float[2048];
        int count;
        float elapsed, sessionTime;
        int gcAtStart;
        long monoAtStart;

        public struct Stats
        {
            public int Frames;
            public float AverageFps, OnePercentLowFps, WorstMs;
            public int Hitches, Collections;
            public long MonoGrowthBytes;
            public override string ToString() =>
                $"frames={Frames} avg={AverageFps:0.0}fps low1%={OnePercentLowFps:0.0}fps worst={WorstMs:0.0}ms hitches={Hitches} gc={Collections} monoGrowth={MonoGrowthBytes / 1024}KB";
        }

        void OnEnable() { count = 0; elapsed = 0; gcAtStart = GC.CollectionCount(0); monoAtStart = Profiler.GetMonoUsedSizeLong(); }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (count < frames.Length) frames[count++] = dt;
            elapsed += dt; sessionTime += dt;
            if (elapsed < Interval) return;
            Last = Summarise(frames, sorted, count, Application.targetFrameRate > 0 ? Application.targetFrameRate : 60,
                GC.CollectionCount(0) - gcAtStart, Profiler.GetMonoUsedSizeLong() - monoAtStart);
            string line = $"t={sessionTime:0}s target={Application.targetFrameRate} {Last} device={SystemInfo.deviceModel} tier={GolfArcade.Tennis.TennisQuality.Current}";
            if (Report != null) Report(line); else Debug.Log("[FrameProbe] " + line);
            OnEnable();
        }

        /// Pure so the maths can be tested without a running player loop. A hitch is any frame
        /// that took at least one and a half target intervals -- a visibly repeated frame.
        public static Stats Summarise(float[] frames, float[] scratch, int count, int targetFps, int collections, long monoGrowth)
        {
            var stats = new Stats { Frames = count, Collections = collections, MonoGrowthBytes = monoGrowth };
            if (count == 0) return stats;
            float total = 0, worst = 0, hitchAt = 1.5f / Mathf.Max(1, targetFps);
            for (int i = 0; i < count; i++)
            {
                float f = frames[i]; total += f; worst = Mathf.Max(worst, f);
                if (f >= hitchAt) stats.Hitches++;
                scratch[i] = f;
            }
            Array.Sort(scratch, 0, count);
            // 1% low: the frame time only 1% of frames were slower than.
            float p99 = scratch[Mathf.Clamp(Mathf.CeilToInt(count * .99f) - 1, 0, count - 1)];
            stats.AverageFps = count / Mathf.Max(1e-5f, total);
            stats.OnePercentLowFps = 1 / Mathf.Max(1e-5f, p99);
            stats.WorstMs = worst * 1000;
            return stats;
        }
    }
}
