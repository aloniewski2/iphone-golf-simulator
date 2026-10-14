using System;
using System.Collections;
using System.IO;
using System.Linq;
using GolfArcade.Game;
using GolfArcade.Hub;
using GolfArcade.Tennis;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GolfArcade.Tests
{
    /// PLAN_MenuHub_WalkableWorld phase 3 (bays + seamless load), editor half: HITCH, MEM, RETURN, CANCEL, and the Unity side of
    /// NO_COVER (the plaza keeps the screen until the match's first frame; the match never draws while it loads behind it).
    public class HubBayTests
    {
        static string ProofDir => Environment.GetEnvironmentVariable("HUB_PROOF_DIR");
        static void Write(string name, string text) { if (string.IsNullOrEmpty(ProofDir)) return; Directory.CreateDirectory(ProofDir); File.AppendAllText(Path.Combine(ProofDir, name), text + "\n"); }
        GameObject host; NativeSportsSession bridge;
        const string Bay = "bay-tennis-exhibition";

        void Send(string session, string action, Action<NativeSportsSession.Message> more = null)
        {
            var m = new NativeSportsSession.Message { version = 1, session = session, action = action, touch = true };
            more?.Invoke(m); bridge.Receive(JsonUtility.ToJson(m));
        }
        IEnumerator BootHub(string session = "hub-1", string bay = null)
        {
            HubWorld.EditorDriven = true;
            if (!host) { host = new GameObject("NativeSportsSession"); Object.DontDestroyOnLoad(host); bridge = host.AddComponent<NativeSportsSession>(); }
            Send(session, "start", m => { m.sport = "hub"; m.bay = bay; });
            for (int i = 0; i < 900 && !(bridge.Ready && HubWorld.Find() && NativeSportsSession.HubActive); i++) yield return null;
        }
        IEnumerator SitInBay()
        {
            var hub = HubWorld.Find();
            hub.GoTo(Bay, true); while (hub.Transitioning) yield return null;
            for (int i = 0; i < 120 && !hub.SeatedInBay; i++) yield return null;
            yield return new WaitForSecondsRealtime(.6f);
        }
        [TearDown] public void Clean()
        {
            HubWorld.EditorDriven = false; TennisGame.SliceInit = false;
            if (host) Object.DestroyImmediate(host);
            Time.timeScale = 1; AudioListener.pause = false;
        }

        [UnityTest, Timeout(600000)] public IEnumerator SeatedLoadKeepsThePlazaSmoothAndHandsOffWithoutACover()
        {
            yield return BootHub(); var hub = HubWorld.Find(); Assert.IsNotNull(hub);
            yield return SitInBay();
            Assert.IsTrue(hub.SeatedInBay, "A at a bay sits you on its bench");
            long before = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
            float t0 = Time.realtimeSinceStartup;
            Send("match-1", "start", m => { m.sport = "tennis"; m.mode = "exhibition"; m.seamless = true; m.bay = Bay; });
            int matchCameraFramesBeforeReady = 0; bool sawRing = false;
            var plazaCam = hub.Camera;
            // any camera other than the plaza's that draws to the screen while the plaza is still up = a frame of the match too early
            void Count(UnityEngine.Rendering.ScriptableRenderContext _, Camera c)
            {
                if (c && c != plazaCam && !c.targetTexture && c.cameraType == CameraType.Game && plazaCam && plazaCam.isActiveAndEnabled) matchCameraFramesBeforeReady++;
            }
            UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering += Count;
            try
            {
                for (int i = 0; i < 2400 && !bridge.Ready; i++)
                {
                    yield return null;
                    if (HubWorld.Find() && HubWorld.Find().BayProgress > 0) sawRing = true;
                }
            }
            finally { UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering -= Count; }
            float seconds = Time.realtimeSinceStartup - t0;
            Assert.IsTrue(bridge.Ready, "the match took the screen");
            var game = Object.FindFirstObjectByType<TennisGame>(); Assert.IsNotNull(game); Assert.IsTrue(game.Initialized);
            Assert.AreEqual(game.GameplayCamera, bridge.GameplayCamera, "the match's own camera, not the plaza's");
            Assert.AreEqual(0, matchCameraFramesBeforeReady, "the match never draws while it loads behind the plaza");
            Assert.IsTrue(sawRing, "the bay ring filled while loading");
            for (int i = 0; i < 120 && SceneManager.GetSceneByName("Hub").isLoaded; i++) yield return null;
            Assert.IsFalse(SceneManager.GetSceneByName("Hub").isLoaded, "the plaza unloads after the hand-off");
            Assert.IsFalse(AudioListener.pause);
            var ms = NativeSportsSession.BehindFrameMs.ToList(); ms.Sort();
            float worst = ms.Count > 0 ? ms[ms.Count - 1] : 0, p99 = ms.Count > 0 ? ms[(int)(ms.Count * .99f)] : 0;
            Write("p3_metrics.txt", $"seamless_load_seconds_editor={seconds:0.00}\nplaza_frames_while_loading={ms.Count}\nplaza_frame_p99_ms_editor={p99:0.0}\nplaza_frame_worst_ms_editor={worst:0.0}\n" +
                                     $"frames_over_50ms={ms.Count(x => x > 50)}\nmemory_before_bytes={before}\nmemory_peak_bytes={NativeSportsSession.BehindPeakBytes}\nworst_frames_ms={string.Join(",", ms.Skip(Math.Max(0, ms.Count - 8)).Select(x => x.ToString("0")))}\nslow_frames:\n  {string.Join("\n  ", NativeSportsSession.BehindSlowFrames)}");
            // HITCH is a device gate; the editor numbers above are written for the record and judged in GATE_RESULTS.md
            Send("match-1", "end");
        }

        /// Diagnostic: the same seated load twice in one session (is the build's cost loading or building?).
        [UnityTest, Timeout(900000)] public IEnumerator SecondSeatedLoadDiagnostic()
        {
            for (int round = 0; round < 2; round++)
            {
                yield return BootHub("hub-d" + round); yield return SitInBay();
                float t0 = Time.realtimeSinceStartup;
                Send("match-d" + round, "start", m => { m.sport = "tennis"; m.mode = "exhibition"; m.seamless = true; m.bay = Bay; });
                for (int i = 0; i < 3000 && !bridge.Ready; i++) yield return null;
                Write("p3_diag.txt", $"round {round}: {Time.realtimeSinceStartup - t0:0.00}s slow {string.Join(" | ", NativeSportsSession.BehindSlowFrames)}");
                Send("match-d" + round, "end");
                yield return null;
            }
        }

        [UnityTest, Timeout(600000)] public IEnumerator StandingUpMidLoadLeavesNothingLoaded()
        {
            yield return BootHub(); yield return SitInBay();
            Send("match-2", "start", m => { m.sport = "tennis"; m.mode = "exhibition"; m.seamless = true; m.bay = Bay; });
            yield return new WaitForSecondsRealtime(.4f);
            Send("match-2", "end");   // the phone stands up: SportsSession.end()
            for (int i = 0; i < 900 && SceneManager.GetSceneByName("Tennis").IsValid() && SceneManager.GetSceneByName("Tennis").isLoaded; i++) yield return null;
            yield return null; yield return null;
            Assert.IsFalse(SceneManager.GetSceneByName("Tennis").isLoaded, "the match scene is unloaded");
            Assert.IsNull(Object.FindFirstObjectByType<TennisGame>(), "nothing of the match is left");
            Assert.IsNotNull(HubWorld.Find(), "the plaza is still there");
            Assert.IsFalse(TennisGame.SliceInit);
            // the phone re-opens the plaza: it is adopted, not reloaded, and the hero stands up
            var same = HubWorld.Find();
            yield return BootHub("hub-2");
            Assert.AreSame(same, HubWorld.Find(), "the plaza was adopted, not reloaded");
            Assert.IsTrue(NativeSportsSession.HubActive);
            Assert.IsFalse(HubWorld.Find().SeatedInBay, "standing up mid-load leaves you standing");
            Write("p3_metrics.txt", "CANCEL_editor=PASS");
        }

        [UnityTest, Timeout(300000)] public IEnumerator BackFromAMatchYouSitOnTheSameBench()
        {
            yield return BootHub("hub-r", Bay);
            var hub = HubWorld.Find(); Assert.IsNotNull(hub);
            yield return new WaitForSecondsRealtime(.3f);
            Assert.IsTrue(hub.SeatedInBay); Assert.AreEqual(Bay, hub.Station.id);
            Assert.AreEqual(HubLayout.TennisRoom, hub.Place);
            Assert.Less(Vector3.Distance(hub.Player.transform.position, HubWorld.SeatOf(HubLayout.SpotById(Bay))), .05f, "on the bench");
            Assert.AreEqual(1f, hub.Player.hero.SeatWeight, .01f, "already seated, no sit-down replay");
            Write("p3_metrics.txt", "RETURN_editor=PASS");
        }
    }
}
