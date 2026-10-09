using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.Profiling;
using GolfArcade.Game;

namespace GolfArcade.Tennis
{
    /// Records the real unattended device rally only when the native benchmark
    /// explicitly enables AutoPlay. Ordinary matches do not record or capture.
    public sealed class SportsVisualTelemetry : MonoBehaviour
    {
        [Serializable] sealed class Sample
        {
            public float seconds, meanFps, medianMs, p95Ms, p99Ms, worstMs;
            public int frames, targetFps, qualityReductions;
            public string quality, phase;
            public long allocatedBytes, managedBytes;
        }
        [Serializable] sealed class Report
        {
            public string scope, device, operatingSystem, graphics, unity, sport, venue, startedUtc, status;
            public int screenWidth, screenHeight, initialTargetFps;
            public Sample[] windows;
        }
        TennisGame game;
        GolfGame golf;
        public bool GolfBenchmarkEnabled;
        readonly List<float> frameMs = new();
        readonly List<int> targets = new(), reductions = new();
        readonly List<Sample> windows = new();
        readonly List<float> currentWindow = new();
        string directory, startedUtc;
        float elapsed, windowTime;
        int initialTarget, nextPhoto;
        bool started, finished;
        static readonly float[] PhotoTimes = {8, 30, 60, 120};

        void Awake() { game = GetComponent<TennisGame>(); golf = GetComponent<GolfGame>(); }
        bool Allowed => (game && game.Initialized && game.AutoPlay) || (golf && GolfBenchmarkEnabled && golf.Swing != null);
        string Venue => game ? TennisVenue.Current.ToString() : golf.ActiveCourseId;
        string Quality => game ? TennisQuality.Current.ToString() : QualitySettings.names[QualitySettings.GetQualityLevel()];
        void Update()
        {
            if (finished || !Allowed || Application.isEditor) return;
            // A native result screen hides gameplay. It must never count as sustained play.
            if (started && game && game.Flow == TennisGame.Phase.MatchOver)
            {
                if (currentWindow.Count > 0) windows.Add(Stats(currentWindow, elapsed));
                if (frameMs.Count > 0) windows.Add(Stats(frameMs, elapsed));
                finished = true; Save("match-complete-before-120s"); SaveFrames();
                return;
            }
            if (Time.timeScale == 0) return;
            if (!started)
            {
                started = true; initialTarget = Application.targetFrameRate;
                startedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
                directory = Path.Combine(Application.persistentDataPath, "visual-overhaul", "runtime",
                    (game ? "tennis-" : "golf-")+Venue.ToLowerInvariant()+"-"+initialTarget+"-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
                Directory.CreateDirectory(directory);
                Debug.Log("[VisualDeviceProof] Recording actual automatic rally to "+directory);
                return;
            }
            float dt = Time.unscaledDeltaTime;
            if (!float.IsFinite(dt) || dt <= 0) return;
            elapsed += dt; windowTime += dt;
            frameMs.Add(dt*1000); currentWindow.Add(dt*1000);
            targets.Add(Application.targetFrameRate); reductions.Add(game ? TennisQuality.Reductions : 0);
            if (nextPhoto < PhotoTimes.Length && elapsed >= PhotoTimes[nextPhoto])
            {
                string path = Path.Combine(directory, "gameplay-"+PhotoTimes[nextPhoto].ToString("000")+"s.png");
                nextPhoto++; StartCoroutine(Capture(path));
            }
            if (windowTime >= 10)
            {
                var sample = Stats(currentWindow, elapsed);
                windows.Add(sample); currentWindow.Clear(); windowTime = 0;
                Debug.Log("[VisualDeviceProof] "+elapsed.ToString("F1")+"s "+sample.meanFps.ToString("F2")+"FPS p95="+sample.p95Ms.ToString("F2")+"ms "+sample.quality+" reductions="+sample.qualityReductions);
                Save("recording");
            }
            if (elapsed >= 120)
            {
                if (currentWindow.Count > 0) windows.Add(Stats(currentWindow, elapsed));
                windows.Add(Stats(frameMs, elapsed)); finished = true;
                Save("complete");
                SaveFrames();
                Debug.Log("[VisualDeviceProof] COMPLETE "+directory);
            }
        }
        void SaveFrames()
        {
            using var writer = new StreamWriter(Path.Combine(directory, "frames.csv"));
            writer.WriteLine("frame,elapsed_frame_ms,target_fps,quality_reductions");
            for (int i=0;i<frameMs.Count;i++) writer.WriteLine(i+","+frameMs[i].ToString("F5",CultureInfo.InvariantCulture)+","+targets[i]+","+reductions[i]);
        }
        IEnumerator Capture(string path)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(path);
        }
        Sample Stats(List<float> values, float seconds)
        {
            var sorted = values.ToArray(); Array.Sort(sorted); double sum=0;
            foreach(float value in sorted) sum += value;
            float Percentile(float p) => sorted[Mathf.Clamp(Mathf.CeilToInt((sorted.Length-1)*p),0,sorted.Length-1)];
            return new Sample { seconds=seconds,frames=sorted.Length,meanFps=(float)(sorted.Length*1000.0/sum),
                medianMs=Percentile(.5f),p95Ms=Percentile(.95f),p99Ms=Percentile(.99f),worstMs=sorted[^1],
                targetFps=Application.targetFrameRate,quality=Quality,qualityReductions=game ? TennisQuality.Reductions : 0,
                phase=game ? game.Flow.ToString() : golf.Current+" hole"+golf.CurrentHole.Number,allocatedBytes=Profiler.GetTotalAllocatedMemoryLong(),managedBytes=GC.GetTotalMemory(false) };
        }
        void Save(string status)
        {
            var report = new Report {scope="Actual phone player Update frame intervals, including first active gameplay frames, verification screenshot I/O, and quality governor changes. Screenshots use the actual rendered display. FPS is frames divided by elapsed time; this is not a synthetic estimate.",
                device=SystemInfo.deviceModel,operatingSystem=SystemInfo.operatingSystem,graphics=SystemInfo.graphicsDeviceType+" / "+SystemInfo.graphicsDeviceName,
                unity=Application.unityVersion,sport=game ? "tennis" : "golf",venue=Venue,startedUtc=startedUtc,status=status,screenWidth=Screen.width,screenHeight=Screen.height,
                initialTargetFps=initialTarget,windows=windows.ToArray()};
            File.WriteAllText(Path.Combine(directory,"report.json"),JsonUtility.ToJson(report,true)+"\n");
        }
    }
}
