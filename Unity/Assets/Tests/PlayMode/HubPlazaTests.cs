using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GolfArcade.Game;
using GolfArcade.Hub;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GolfArcade.Tests
{
    /// PLAN_MenuHub_WalkableWorld phase 1 (greybox walk) gates, editor half. Each test writes its numbers to
    /// $HUB_PROOF_DIR/phase1_metrics.json (merged) and stills to $HUB_PROOF_DIR, when that is set.
    public class HubPlazaTests
    {
        static string ProofDir => Environment.GetEnvironmentVariable("HUB_PROOF_DIR");
        static readonly Dictionary<string, string> metrics = new Dictionary<string, string>();
        static void Metric(string key, object value)
        {
            metrics[key] = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
            if (string.IsNullOrEmpty(ProofDir)) return;
            Directory.CreateDirectory(ProofDir);
            var path = Path.Combine(ProofDir, "phase1_metrics.json");
            var all = new SortedDictionary<string, string>();
            if (File.Exists(path)) foreach (var line in File.ReadAllLines(path)) { var t = line.Trim().TrimEnd(','); int c = t.IndexOf("\": \""); if (t.StartsWith("\"") && c > 0) all[t.Substring(1, c - 1)] = t.Substring(c + 4).TrimEnd('"'); }
            foreach (var kv in metrics) all[kv.Key] = kv.Value;
            File.WriteAllText(path, "{\n" + string.Join(",\n", all.Select(kv => $"  \"{kv.Key}\": \"{kv.Value}\"")) + "\n}\n");
        }
        static void Still(Camera cam, string name, int w = 1280, int h = 720)
        {
            if (string.IsNullOrEmpty(ProofDir) || !cam) return;
            Directory.CreateDirectory(ProofDir);
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32); rt.antiAliasing = 4;
            var prevT = cam.targetTexture; var prevA = cam.aspect;
            cam.targetTexture = rt; cam.aspect = w / (float)h; cam.Render(); cam.targetTexture = prevT; cam.aspect = prevA;
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
            RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
            File.WriteAllBytes(Path.Combine(ProofDir, name + ".png"), tex.EncodeToPNG()); Object.Destroy(tex);
        }

        GameObject host; NativeSportsSession bridge;

        IEnumerator BootHub(bool female = false)
        {
            HubWorld.EditorDriven = true;
            host = new GameObject("NativeSportsSession"); Object.DontDestroyOnLoad(host);
            bridge = host.AddComponent<NativeSportsSession>();
            bridge.Receive(JsonUtility.ToJson(new NativeSportsSession.Message { version = 1, session = "hub-test", action = "start", sport = "hub", female = female, touch = true, shirt = "E8505B", shorts = "24345E", accent = "FFFFFF" }));
            for (int i = 0; i < 900 && !bridge.Ready; i++) yield return null;
        }
        [TearDown] public void Clean()
        {
            HubWorld.EditorDriven = false;
            if (host) Object.DestroyImmediate(host);
            Time.timeScale = 1;
        }

        [UnityTest, Timeout(300000)] public IEnumerator HubBootsFromTheNativeStartAndRendersThePlaza()
        {
            float t0 = Time.realtimeSinceStartup;
            yield return BootHub();
            Assert.IsTrue(bridge.Ready, "the plaza camera must render a frame before ready");
            Metric("hub_boot_seconds_editor", (Time.realtimeSinceStartup - t0).ToString("0.00"));
            var hub = HubWorld.Find(); Assert.IsNotNull(hub); Assert.IsTrue(hub.Initialized);
            Assert.IsTrue(NativeSportsSession.Active); Assert.IsTrue(NativeSportsSession.HubActive);
            Assert.AreEqual(hub.Camera, bridge.GameplayCamera);
            Assert.AreEqual(1, Time.timeScale, "the plaza never pauses");
            Assert.AreEqual(HubLayout.Plaza, hub.Place);
            Assert.IsNotNull(hub.Player.hero, "the plaza hero is the match hero with the plaza animator");
            Assert.IsNotNull(hub.Player.look);
            Assert.IsFalse(hub.Player.look.female);
            Assert.IsTrue(hub.Player.look.TryGetKitColour("Kit_Shirt", out var shirt));
            Assert.Greater(shirt.r, shirt.b, "the launch's shirt colour reaches the plaza hero");
            Assert.IsNotEmpty(hub.Player.hero.Emotes.ToList(), "emote clips resolved from the hero prefab");
            yield return new WaitForSeconds(.5f);
            Still(hub.Camera, "p1_01_plaza_spawn");
            // overview still from above
            var cam = hub.Camera; var pos = cam.transform.position; var rot = cam.transform.rotation;
            cam.transform.SetPositionAndRotation(new Vector3(0, 32, -26), Quaternion.Euler(48, 0, 0)); Still(cam, "p1_02_plaza_overview");
            cam.transform.SetPositionAndRotation(pos, rot);
            Metric("HUB_BOOT_editor", "PASS");
        }

        /// Look-development stills (not a gate): the concept's framing, the doors, the crest. HUB_LOOK_DIR overrides the folder.
        [UnityTest, Timeout(300000)] public IEnumerator LookStills()
        {
            yield return BootHub();
            var hub = HubWorld.Find(); var p = hub.Player; var cam = hub.Camera;
            string dir = Environment.GetEnvironmentVariable("HUB_LOOK_DIR"); if (!string.IsNullOrEmpty(dir)) Environment.SetEnvironmentVariable("HUB_PROOF_DIR", dir);
            p.Teleport(HubLayout.Spawn, 0); yield return new WaitForSeconds(1.2f);
            Still(cam, "look_01_spawn", 1920, 1080);
            // walking toward PLAY, mid-plaza
            p.SetStick(0, 1, .55f); yield return new WaitForSeconds(2.2f); Still(cam, "look_02_walking", 1920, 1080); p.SetStick(0, 0, 0);
            yield return new WaitForSeconds(1.2f);
            var pos = cam.transform.position; var rot = cam.transform.rotation;
            void Shot(string name, Vector3 at, Vector3 lookAt) { cam.transform.SetPositionAndRotation(at, Quaternion.LookRotation(lookAt - at)); Still(cam, name, 1920, 1080); }
            Shot("look_03_overview", new Vector3(0, 30, -30), new Vector3(0, 0, 4));
            Shot("look_04_locker", new Vector3(-6, 3.2f, -3), new Vector3(-14.5f, 2.6f, 4.2f));
            Shot("look_05_play_door", new Vector3(2, 3.0f, 3), new Vector3(0, 4.5f, 15));
            Shot("look_06_crest", new Vector3(6, 2.2f, -9.5f), new Vector3(11.4f, 2.4f, -2.6f));
            Shot("look_07_clubhouse", new Vector3(6, 3.2f, -3), new Vector3(14.5f, 2.6f, 4.2f));
            // the hero standing still, from the front and the side (the plaza stance)
            var hp = p.transform.position;
            Shot("look_08_hero_front", hp + new Vector3(0, 1.2f, 3.2f), hp + Vector3.up * .95f);
            Shot("look_09_hero_side", hp + new Vector3(3.2f, 1.2f, 0), hp + Vector3.up * .95f);
            cam.transform.SetPositionAndRotation(pos, rot);
            // every room from the game camera, a few steps in
            foreach (var room in new[] { HubLayout.Locker, HubLayout.Clubhouse, HubLayout.PlayHall, HubLayout.TennisRoom, HubLayout.GolfRoom })
            {
                hub.GoTo(room); while (hub.Transitioning) yield return null;
                p.SetStick(0, 1, .4f); yield return new WaitForSeconds(1.4f); p.SetStick(0, 0, 0); yield return new WaitForSeconds(.8f);
                Still(cam, "look_room_" + room, 1920, 1080);
            }
            hub.GoTo("rack-shirt", true); while (hub.Transitioning) yield return null; yield return new WaitForSeconds(1.2f);
            Still(cam, "look_station_shirt", 1920, 1080);
            hub.LeaveStation();
            hub.GoTo("bay-tennis-exhibition"); while (hub.Transitioning) yield return null; yield return new WaitForSeconds(1.5f);
            Still(cam, "look_bay_exhibition", 1920, 1080);
            hub.Interact(); yield return new WaitForSeconds(1.6f);
            Still(cam, "look_bay_seated", 1920, 1080);
            hub.LeaveStation();
            hub.GoTo(HubLayout.GolfRoom); while (hub.Transitioning) yield return null;
            p.SetStick(0, 1, .4f); yield return new WaitForSeconds(1.0f); p.SetStick(0, 0, 0); yield return new WaitForSeconds(1.2f);
            Still(cam, "look_room_golf_screens", 1920, 1080);
            if (!string.IsNullOrEmpty(dir)) Environment.SetEnvironmentVariable("HUB_PROOF_DIR", null);
        }

        [UnityTest, Timeout(300000)] public IEnumerator StickIsAnalogWalkRunSprintAndStopsQuickly()
        {
            yield return BootHub();
            var hub = HubWorld.Find(); var p = hub.Player;
            // walk: short push
            p.SetStick(0, 1, .4f); yield return new WaitForSeconds(1.2f);
            float walk = p.Speed; string walkClip = Dominant(p);
            Assert.That(walk, Is.InRange(HubPlayer.WalkMin, HubPlayer.WalkMax), "a short push walks");
            // run: full push, briefly
            p.SetStick(0, 1, .8f); yield return new WaitForSeconds(.6f);
            float run = p.Speed; Assert.That(run, Is.EqualTo(HubPlayer.RunSpeed).Within(.15f), "a full push runs");
            // sprint: hold full
            p.SetStick(0, 1, 1f); yield return new WaitForSeconds(1.4f);
            float sprint = p.Speed; Assert.That(sprint, Is.EqualTo(HubPlayer.SprintSpeed).Within(.2f), "holding full sprints");
            Still(hub.Camera, "p1_03_sprinting");
            // stop
            p.SetStick(0, 0, 0); float t = 0;
            while (p.Speed > .05f && t < 2) { yield return null; t += Time.deltaTime; }
            Metric("stick_walk_mps", walk.ToString("0.00")); Metric("stick_walk_clip", walkClip); Metric("stick_run_mps", run.ToString("0.00"));
            Metric("stick_sprint_mps", sprint.ToString("0.00")); Metric("stick_stop_seconds", t.ToString("0.000"));
            Assert.LessOrEqual(t, .25f, "letting go stops within 0.25 s");
            Metric("STICK_ANALOG_editor", "PASS");
        }
        static string Dominant(HubPlayer p) => p.hero.Speed < .1f ? "Idle" : p.hero.Speed < HubHeroAnimator.WalkTop ? "Walk" : "Run";

        [UnityTest, Timeout(300000)] public IEnumerator StickReachesTheHeroOnTheNextFrame()
        {
            yield return BootHub();
            var p = HubWorld.Find().Player;
            yield return new WaitForSeconds(.3f);
            int frames = 0; float ms = 0;
            p.SetStick(1, 0, 1); float started = Time.realtimeSinceStartup;
            while (p.Speed < .01f && frames < 30) { yield return null; frames++; }
            ms = (Time.realtimeSinceStartup - started) * 1000;
            Metric("latency_stick_to_motion_frames_editor", frames); Metric("latency_stick_to_motion_ms_editor", ms.ToString("0"));
            Assert.LessOrEqual(frames, 2, "the stick moves the hero on the next frame");
        }

        [UnityTest, Timeout(600000)] public IEnumerator FeetDoNotSlideWhileWalkingAndRunning()
        {
            yield return BootHub();
            var hub = HubWorld.Find(); var p = hub.Player;
            yield return new WaitForSeconds(.5f);
            // a 24 s wander on the plaza: walks, runs, a sprint, turns and stops (inside the ring, away from the stage centre)
            var script = new (float seconds, float x, float y, float mag)[] {
                (2.5f, 0, 1, .35f), (1.0f, 0, 0, 0), (2.0f, 1, 0, .55f), (2.0f, 0, -1, .8f), (1.5f, -1, 0, .8f), (.8f, 0, 0, 0),
                (2.0f, -1, 1, .5f), (2.5f, 1, 0, 1f), (1.5f, 0, -1, 1f), (1.0f, 0, 0, 0), (2.0f, .7f, .7f, .3f), (2f, -.7f, -.7f, .9f), (1.2f, 0, 0, 0) };
            int moving = 0, sliding = 0, frames = 0; float worstSlide = 0;
            var slides = new List<float>(); var csv = new System.Text.StringBuilder("t,speed,yaw,toe,wIdle,wWalk,wRun,wAct,stickMag\n"); float clock = 0;
            p.Teleport(new Vector3(-4, 0, -6), 0);
            foreach (var step in script)
            {
                p.SetStick(step.x, step.y, step.mag); float t = 0;
                while (t < step.seconds) {
                    yield return null; t += Time.deltaTime; frames++; clock += Time.deltaTime;
                    var w = p.hero.Weights; csv.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0.000},{1:0.000},{2:0.0},{3:0.000},{4:0.00},{5:0.00},{6:0.00},{7:0.00},{8:0.00}", clock, p.hero.Speed, p.Yaw, p.hero.PlantedToeSpeed, w.x, w.y, w.z, w.w, p.StickMagnitude));
                    if (p.hero.Speed > .25f && !hub.Transitioning) { moving++; float s = p.hero.PlantedToeSpeed; slides.Add(s); if (s > .15f) sliding++; worstSlide = Mathf.Max(worstSlide, s); }
                }
            }
            float share = moving > 0 ? sliding / (float)moving : 1;
            if (!string.IsNullOrEmpty(ProofDir)) File.WriteAllText(Path.Combine(ProofDir, "p1_feet_trace.csv"), csv.ToString());
            slides.Sort();
            Metric("feet_moving_frames", moving); Metric("feet_slide_over_015_frames", sliding);
            Metric("feet_slide_share", share.ToString("0.000")); Metric("feet_planted_toe_speed_p50", slides.Count > 0 ? slides[slides.Count / 2].ToString("0.000") : "0");
            Metric("feet_planted_toe_speed_p95", slides.Count > 0 ? slides[(int)(slides.Count * .95f)].ToString("0.000") : "0");
            Assert.Greater(moving, 200);
            Assert.LessOrEqual(share, .20f, $"planted toe slides faster than 0.15 m/s on {share:P1} of moving frames");
            Metric("FEET_editor", "PASS");
        }

        [UnityTest, Timeout(600000)] public IEnumerator EveryDoorWalksInAndOutWithinHalfASecond()
        {
            yield return BootHub();
            var hub = HubWorld.Find(); var p = hub.Player;
            yield return new WaitForSeconds(.4f);
            var report = new List<string>(); float worstSeconds = 0, worstFrame = 0; int doors = 0;
            foreach (var door in HubLayout.Doors.Where(d => !d.locked))
            {
                // stand 2.2 m in front of the door, in its place, and push the stick at it
                p.SetStick(0, 0, 0);
                if (!hub.GoTo(door.place)) { Assert.Fail("quick travel to " + door.place); }
                while (hub.Transitioning) yield return null;
                p.Teleport(door.position - door.inward * 2.2f, Mathf.Atan2(door.inward.x, door.inward.z) * Mathf.Rad2Deg);
                yield return null;
                int before = hub.DoorLog.Count; HubLayout.Destination(door, out var partner);
                p.SetStick(door.inward.x, door.inward.z, .8f);
                float t = 0; while (hub.DoorLog.Count == before && t < 5) { yield return null; t += Time.deltaTime; }
                p.SetStick(0, 0, 0);
                Assert.Greater(hub.DoorLog.Count, before, "walking into " + door.id + " goes through it");
                var log = hub.DoorLog[hub.DoorLog.Count - 1];
                Assert.AreEqual(partner.place, hub.Place, door.id + " leads to " + partner.place);
                report.Add($"{door.id} -> {partner.place}: {log.seconds:0.000}s worst frame {log.worstFrame * 1000:0.0}ms");
                worstSeconds = Mathf.Max(worstSeconds, log.seconds); worstFrame = Mathf.Max(worstFrame, log.worstFrame); doors++;
                if (door.place == HubLayout.Plaza) { yield return new WaitForSeconds(.35f); Still(hub.Camera, "p1_room_" + partner.place); }
            }
            Metric("doors_walked", doors); Metric("doors_worst_seconds", worstSeconds.ToString("0.000")); Metric("doors_worst_frame_ms_editor", (worstFrame * 1000).ToString("0.0"));
            if (!string.IsNullOrEmpty(ProofDir)) File.WriteAllLines(Path.Combine(ProofDir, "p1_doors.txt"), report);
            Assert.AreEqual(HubLayout.Doors.Count(d => !d.locked), doors);
            Assert.LessOrEqual(worstSeconds, .5f, "every door in and out within 0.5 s");
            Metric("DOORS_editor_time", "PASS");
        }

        [UnityTest, Timeout(1800000)] public IEnumerator SoakHoldsFrameRate()
        {
            float seconds = float.TryParse(Environment.GetEnvironmentVariable("HUB_SOAK_SECONDS"), out var s) ? s : 45f;
            yield return BootHub();
            var hub = HubWorld.Find(); var p = hub.Player;
            Application.targetFrameRate = 60; QualitySettings.vSyncCount = 0;
            yield return new WaitForSeconds(1f);
            var rng = new System.Random(7); float t = 0, next = 0; var frameMs = new List<float>(); long gc0 = GC.CollectionCount(0);
            GC.Collect(); long mem0 = GC.GetTotalMemory(true);
            while (t < seconds)
            {
                yield return null; float dt = Time.unscaledDeltaTime; t += dt; frameMs.Add(dt * 1000);
                if (t >= next)
                {
                    next = t + 1.5f + (float)rng.NextDouble() * 2;
                    // wander: aim back at the plaza centre when near the edge, sometimes take a door
                    var pos = p.transform.position;
                    var place = HubLayout.PlaceOf(hub.Place);
                    var toCentre = place.origin - pos; toCentre.y = 0;
                    var dir = toCentre.magnitude > (place.outdoor ? 9 : 3) ? toCentre.normalized : new Vector3((float)rng.NextDouble() * 2 - 1, 0, (float)rng.NextDouble() * 2 - 1).normalized;
                    float mag = (float)rng.NextDouble(); if (rng.NextDouble() < .15) mag = 0;
                    p.SetStick(dir.x, dir.z, mag);
                    if (rng.NextDouble() < .08) hub.GoTo(HubLayout.Places[rng.Next(HubLayout.Places.Length)].id);
                }
            }
            frameMs.Sort();
            float avgFps = frameMs.Count / (frameMs.Sum() / 1000f);
            Metric("soak_seconds", seconds); Metric("soak_avg_fps_editor", avgFps.ToString("0.0"));
            Metric("soak_p99_frame_ms_editor", frameMs[(int)(frameMs.Count * .99f)].ToString("0.0"));
            Metric("soak_worst_frame_ms_editor", frameMs[frameMs.Count - 1].ToString("0.0"));
            Metric("soak_gen0_collections", GC.CollectionCount(0) - gc0);
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            Metric("soak_managed_retained_growth_kb", ((GC.GetTotalMemory(true) - mem0) / 1024).ToString());
            Assert.GreaterOrEqual(avgFps, 58f, "the plaza holds 60 fps in the editor soak");
        }

        [UnityTest, Timeout(600000)] public IEnumerator MatchStartLeavesThePlazaAndTheHubComesBack()
        {
            yield return BootHub();
            Assert.IsTrue(bridge.Ready);
            bridge.Receive(JsonUtility.ToJson(new NativeSportsSession.Message { version = 1, session = "match-1", action = "start", sport = "tennis", touch = true }));
            for (int i = 0; i < 900 && !(bridge.Ready && Object.FindFirstObjectByType<GolfArcade.Tennis.TennisGame>()); i++) yield return null;
            Assert.IsFalse(NativeSportsSession.HubActive, "a match start leaves the plaza");
            Assert.IsNull(HubWorld.Find(), "the match scene replaced Hub.unity");
            bridge.Receive(JsonUtility.ToJson(new NativeSportsSession.Message { version = 1, session = "match-1", action = "end" }));
            bridge.Receive(JsonUtility.ToJson(new NativeSportsSession.Message { version = 1, session = "hub-2", action = "start", sport = "hub", touch = true }));
            for (int i = 0; i < 900 && !(bridge.Ready && HubWorld.Find()); i++) yield return null;
            Assert.IsTrue(NativeSportsSession.HubActive); Assert.IsNotNull(HubWorld.Find());
            Assert.AreEqual(1, Time.timeScale);
        }

        [UnityTest, Timeout(300000)] public IEnumerator PhoneButtonsReachThePlaza()
        {
            yield return BootHub();
            var hub = HubWorld.Find();
            void Send(string action, string mode = null) => bridge.Receive(JsonUtility.ToJson(new NativeSportsSession.Message { version = 1, session = "hub-test", action = action, mode = mode, sport = "hub" }));
            Send("hubGo", "rack-shirt");
            for (int i = 0; i < 60 && hub.Transitioning; i++) yield return null;
            yield return new WaitForSeconds(.5f);
            Assert.AreEqual(HubLayout.Locker, hub.Place);
            Assert.AreEqual("rack-shirt", hub.ZoneId, "quick travel to a station stands you at it");
            string acted = null; hub.Interacted += id => acted = id;
            Send("hubA"); Assert.AreEqual("rack-shirt", acted);
            Still(hub.Camera, "p1_04_locker_station_prompt");
            Send("hubGo", HubLayout.Plaza); yield return new WaitForSeconds(.6f);
            Send("hubEmote", "wave"); yield return new WaitForSeconds(.4f);
            Assert.IsTrue(hub.Player.hero.ActionActive, "the wave emote plays");
            Still(hub.Camera, "p1_05_wave_emote");
            Send("hubLook"); // empty look: male, default kit; must not throw
            Send("end"); Assert.IsFalse(NativeSportsSession.Active); Assert.IsFalse(NativeSportsSession.HubActive);
        }
    }
}
