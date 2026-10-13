using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GolfArcade.Game;
using GolfArcade.Hub;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GolfArcade.Tests
{
    /// Country-club hub restyle proof (work/hub-country-club): the 7 shots of the brief's before/after sheets, from the SAME
    /// cameras as HubPlazaTests.LookStills (look_03_overview, look_01_spawn, look_04_locker, look_05_play_door, look_07_clubhouse,
    /// look_room_tennis, look_room_golf), plus render-cost numbers in the three gameplay views (spawn, tennis, golf) and a plaza
    /// walk. Writes to $HUB_CC_DIR. Not a repo test: copied into a scratch clone by work/hub-country-club/tools/run.sh.
    public class HubCountryClubProof
    {
        static string Dir => Environment.GetEnvironmentVariable("HUB_CC_DIR");
        static void Still(Camera cam, string name, int w = 1920, int h = 1080)
        {
            if (string.IsNullOrEmpty(Dir) || !cam) return;
            Directory.CreateDirectory(Dir);
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32); rt.antiAliasing = 4;
            var prevT = cam.targetTexture; var prevA = cam.aspect;
            cam.targetTexture = rt; cam.aspect = w / (float)h; cam.Render(); cam.targetTexture = prevT; cam.aspect = prevA;
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
            RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
            File.WriteAllBytes(Path.Combine(Dir, name + ".png"), tex.EncodeToPNG()); Object.Destroy(tex);
        }
        static readonly List<string> report = new List<string>();
        static void Line(string s) { report.Add(s); if (!string.IsNullOrEmpty(Dir)) { Directory.CreateDirectory(Dir); File.WriteAllLines(Path.Combine(Dir, "render_cost.txt"), report); } }

        GameObject host; NativeSportsSession bridge;
        IEnumerator BootHub()
        {
            HubWorld.EditorDriven = true;
            host = new GameObject("NativeSportsSession"); Object.DontDestroyOnLoad(host);
            bridge = host.AddComponent<NativeSportsSession>();
            bridge.Receive(JsonUtility.ToJson(new NativeSportsSession.Message { version = 1, session = "hub-test", action = "start", sport = "hub", female = false, touch = true, shirt = "E8505B", shorts = "24345E", accent = "FFFFFF" }));
            for (int i = 0; i < 900 && !bridge.Ready; i++) yield return null;
        }
        [TearDown] public void Clean()
        {
            HubWorld.EditorDriven = false;
            if (host) Object.DestroyImmediate(host);
            Time.timeScale = 1;
        }

        /// Average render counters over `frames` live frames (the hub camera's own view, the game's real path).
        static IEnumerator Cost(string label, int frames = 90)
        {
            var names = new[] { "SetPass Calls Count", "Draw Calls Count", "Batches Count", "Triangles Count", "Vertices Count", "Shadow Casters Count" };
            var recs = names.Select(n => ProfilerRecorder.StartNew(ProfilerCategory.Render, n)).ToArray();
            var sums = new double[names.Length]; var ms = new List<float>();
            yield return null;
            for (int f = 0; f < frames; f++)
            {
                yield return null;
                for (int i = 0; i < recs.Length; i++) sums[i] += recs[i].LastValue;
                ms.Add(Time.unscaledDeltaTime * 1000);
            }
            foreach (var r in recs) r.Dispose();
            ms.Sort();
            Line($"{label}: " + string.Join(", ", names.Select((n, i) => $"{n.Replace(" Count", "")} {sums[i] / frames:0}")) + $", editor frame median {ms[ms.Count / 2]:0.0} ms p95 {ms[(int)(ms.Count * .95f)]:0.0} ms");
        }

        [UnityTest, Timeout(600000)] public IEnumerator SevenShots()
        {
            report.Clear();
            Application.targetFrameRate = 60; QualitySettings.vSyncCount = 0;
            float t0 = Time.realtimeSinceStartup;
            yield return BootHub();
            Line($"boot to ready: {Time.realtimeSinceStartup - t0:0.00} s (editor)");
            var hub = HubWorld.Find(); var p = hub.Player; var cam = hub.Camera;
            // a fresh boot holds the arrival fly-in's opening shot until the phone says go: skip it (the follow camera is the shot)
            if (hub.Arriving) { hub.HurryArrival(); for (int i = 0; i < 300 && hub.Arriving; i++) yield return null; }
            // 02 spawn (look_01_spawn)
            p.Teleport(HubLayout.Spawn, 0); yield return new WaitForSeconds(1.2f);
            Still(cam, "02_plaza_spawn");
            yield return Cost("plaza spawn view");
            // the look-dev cameras (look_03, 04, 05, 07)
            var pos = cam.transform.position; var rot = cam.transform.rotation;
            void Shot(string name, Vector3 at, Vector3 lookAt) { cam.transform.SetPositionAndRotation(at, Quaternion.LookRotation(lookAt - at)); Still(cam, name); }
            Shot("01_island_overview", new Vector3(0, 30, -30), new Vector3(0, 0, 4));
            Shot("03_locker_door", new Vector3(-6, 3.2f, -3), new Vector3(-14.5f, 2.6f, 4.2f));
            Shot("04_play_door", new Vector3(2, 3.0f, 3), new Vector3(0, 4.5f, 15));
            Shot("05_clubhouse_door", new Vector3(6, 3.2f, -3), new Vector3(14.5f, 2.6f, 4.2f));
            // extra look-dev angles (not the sheet's)
            Shot("x_overview_low", new Vector3(-22, 9, -26), new Vector3(0, 1, 4));
            Shot("x_crest", new Vector3(6, 2.2f, -9.5f), new Vector3(11.4f, 2.4f, -2.6f));
            cam.transform.SetPositionAndRotation(pos, rot);
            // a plaza walk (frame cost while moving)
            p.SetStick(0, 1, .55f); yield return Cost("plaza walking", 120); p.SetStick(0, 0, 0);
            // 06 tennis, 07 golf (look_room_tennis, look_room_golf: the game camera a few steps in)
            foreach (var (room, name) in new[] { (HubLayout.TennisRoom, "06_tennis_court"), (HubLayout.GolfRoom, "07_golf_range") })
            {
                hub.GoTo(room); while (hub.Transitioning) yield return null;
                p.SetStick(0, 1, .4f); yield return new WaitForSeconds(1.4f); p.SetStick(0, 0, 0); yield return new WaitForSeconds(.8f);
                Still(cam, name);
                yield return Cost(name.Substring(3) + " view");
            }
            // extra: seated in a tennis bay, the other rooms
            hub.GoTo("bay-tennis-exhibition"); while (hub.Transitioning) yield return null; yield return new WaitForSeconds(1.0f);
            hub.Interact(); yield return new WaitForSeconds(1.6f); Still(cam, "x_bay_seated"); hub.LeaveStation();
            // the interiors from the game camera a few steps in (look_room_play / _locker / _clubhouse), then the hall closer up
            foreach (var (room, name) in new[] { (HubLayout.PlayHall, "08_play_hall"), (HubLayout.Locker, "09_locker_room"), (HubLayout.Clubhouse, "10_clubhouse_room") })
            {
                hub.GoTo(room); while (hub.Transitioning) yield return null;
                p.SetStick(0, 1, .4f); yield return new WaitForSeconds(1.4f); p.SetStick(0, 0, 0); yield return new WaitForSeconds(.8f);
                Still(cam, name);
            }
            hub.GoTo(HubLayout.PlayHall); while (hub.Transitioning) yield return null;
            p.SetStick(0, 1, .4f); yield return new WaitForSeconds(3.2f); p.SetStick(0, 0, 0); yield return new WaitForSeconds(.8f);
            Still(cam, "x_hall_near");
            yield return Cost("play_hall view");
        }
    }
}
