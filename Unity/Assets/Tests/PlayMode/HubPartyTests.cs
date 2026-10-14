using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GolfArcade.Game;
using GolfArcade.Hub;
using GolfArcade.Multiplayer;
using GolfArcade.Tennis;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GolfArcade.Tests
{
    /// PLAN_MenuHub_WalkableWorld phase 4 (party), Unity half: SYNC (four heroes at 60 fps, friends drawn within 0.3 m of where they
    /// were 120 ms ago - the interpolation delay - with 10 Hz updates arriving 60-140 ms apart), LOOK (an online tennis match
    /// wears the lobby's colours, the same ones the plaza shows), and Call party's path.
    public class HubPartyTests
    {
        static string ProofDir => Environment.GetEnvironmentVariable("HUB_PROOF_DIR");
        static void Write(string text) { if (string.IsNullOrEmpty(ProofDir)) return; Directory.CreateDirectory(ProofDir); File.AppendAllText(System.IO.Path.Combine(ProofDir, "p4_metrics.txt"), text + "\n"); }
        GameObject host; NativeSportsSession bridge;
        void Send(string session, string action, Action<NativeSportsSession.Message> more = null)
        {
            var m = new NativeSportsSession.Message { version = 1, session = session, action = action, touch = true }; more?.Invoke(m); bridge.Receive(JsonUtility.ToJson(m));
        }
        IEnumerator BootHub()
        {
            HubWorld.EditorDriven = true;
            host = new GameObject("NativeSportsSession"); Object.DontDestroyOnLoad(host); bridge = host.AddComponent<NativeSportsSession>();
            Send("hub-p", "start", m => m.sport = "hub");
            for (int i = 0; i < 900 && !(bridge.Ready && NativeSportsSession.HubActive); i++) yield return null;
        }
        [TearDown] public void Clean() { HubWorld.EditorDriven = false; if (host) Object.DestroyImmediate(host); Time.timeScale = 1; }

        static Vector3 Loop(int who, double t)
        {
            // three friends jogging different loops on the plaza (run speed ~2.5-3.3 m/s)
            float r = 5f + who * 1.6f, w = (2.6f + who * .3f) / r;
            return new Vector3(Mathf.Sin((float)(t * w) + who * 2f) * r, 0, 1 + Mathf.Cos((float)(t * w) + who * 2f) * r);
        }

        [UnityTest, Timeout(300000)] public IEnumerator FourHeroesStaySmoothAndFriendsAreDrawnWhereTheyWere()
        {
            yield return BootHub(); var hub = HubWorld.Find(); Assert.IsNotNull(hub);
            var names = new[] { "Mia", "Leo", "Ava" };
            var rng = new System.Random(3); var nextSend = new double[3]; var lastSent = new double[3]; double t0 = Time.unscaledTimeAsDouble; float worstFrame = 0; double lastStall = -1;
            var errors = new List<float>(); var lags = new List<float>(); var frames = new List<float>();
            hub.Player.SetStick(0, 1, .5f);   // you walk too
            float run = 0;
            while (run < 12f)
            {
                yield return null; run += Time.unscaledDeltaTime; frames.Add(Time.unscaledDeltaTime * 1000); worstFrame = Mathf.Max(worstFrame, Time.unscaledDeltaTime * 1000); if (Time.unscaledDeltaTime > .1f) lastStall = Time.unscaledTimeAsDouble;
                double now = Time.unscaledTimeAsDouble, t = now - t0;
                for (int w = 0; w < 3; w++)
                {
                    if (now < nextSend[w]) continue;
                    nextSend[w] = now + .06 + rng.NextDouble() * .08; lastSent[w] = now;   // 10 Hz-ish with jitter
                    var p = Loop(w, t); var ahead = Loop(w, t + .05);
                    var s = new RemoteState { id = "friend-" + w, name = names[w], colour = w + 1, place = HubLayout.Plaza, female = w % 2 == 0, skinHex = "C47A4C",
                        shirt = new[] { "FF63B8", "7BDA4A", "A970FF" }[w], shorts = "24345E", x = p.x, y = 0, z = p.z,
                        yaw = Mathf.Atan2(ahead.x - p.x, ahead.z - p.z) * Mathf.Rad2Deg, speed = 3f };
                    Send("hub-p", "hubRemote", m => m.remote = JsonUtility.ToJson(s));
                }
                if (run > 2f)   // after the spawns have settled; only while updates are fresh (this test is also the sender: a local stall stops "their" packets)
                    for (int w = 0; w < 3; w++)
                        if (now - lastSent[w] < .2 && now - lastStall > .45 && HubRemotes.For(hub).TryGetShown("friend-" + w, out var shown))
                        { errors.Add(Vector3.Distance(shown, Loop(w, t - HubRemotes.Delay))); lags.Add(Vector3.Distance(shown, Loop(w, t))); }
                if (run > 5f && run < 5.1f && hub.Player) hub.Player.SetStick(0, -1, .5f);
            }
            hub.Player.SetStick(0, 0, 0);
            Assert.AreEqual(3, HubRemotes.For(hub).Count);
            errors.Sort(); lags.Sort(); var steady = frames.Skip(frames.Count / 3).ToList();
            float fps = steady.Count / (steady.Sum() / 1000f), p95 = errors[(int)(errors.Count * .95f)], max = errors[errors.Count - 1];
            Write($"sync_worst_frame_ms_editor={worstFrame:0} (a friend's hero spawning; the 0.45 s after any frame over 100 ms are left out of the error, there was no data)\nsync_fps_four_heroes_editor={fps:0.0}\nsync_error_p95_m={p95:0.000}\nsync_error_max_m={max:0.000}\nsync_lag_vs_now_p95_m={lags[(int)(lags.Count * .95f)]:0.000}");
            Assert.LessOrEqual(p95, .3f, "friends are drawn within 0.3 m of where they were (95 %)");
            Assert.GreaterOrEqual(fps, 55f, "four heroes keep the frame rate (editor)");
            HubRemotes.For(hub).Roster(new[] { "friend-0" });
            Assert.AreEqual(1, HubRemotes.For(hub).Count, "a friend who left walks off");
        }

        [UnityTest, Timeout(300000)] public IEnumerator CallPartyPointsTheWayToTheBay()
        {
            yield return BootHub(); var hub = HubWorld.Find();
            Send("hub-p", "hubCall", m => m.mode = "bay-tennis-online");
            yield return null; yield return null;
            Assert.AreEqual("bay-tennis-online", HubRemotes.For(hub).CallBay);
            var bay = HubLayout.SpotById("bay-tennis-online");
            Assert.AreEqual(HubLayout.DoorById("door-plaza-play").position, HubRemotes.Waypoint(HubLayout.Plaza, bay), "from the plaza: the PLAY door");
            Assert.AreEqual(HubLayout.DoorById("door-play-tennis").position, HubRemotes.Waypoint(HubLayout.PlayHall, bay), "in the hall: the tennis arch");
            Assert.AreEqual(bay.position, HubRemotes.Waypoint(HubLayout.TennisRoom, bay));
            Assert.AreEqual(HubLayout.DoorById("door-locker-exit").position, HubRemotes.Waypoint(HubLayout.Locker, bay), "from the locker: out first");
        }

        /// TOGETHER, Unity half: an online match started from a party bay loads behind the plaza and hands off like a solo one.
        [UnityTest, Timeout(600000)] public IEnumerator AnOnlineMatchFromABayLoadsBehindThePlaza()
        {
            yield return BootHub(); var hub = HubWorld.Find();
            hub.GoTo("bay-tennis-online", true); while (hub.Transitioning) yield return null;
            for (int i = 0; i < 90 && !hub.SeatedInBay; i++) yield return null;
            Assert.IsTrue(hub.SeatedInBay);
            NetworkParticipant P(string id, int seat) => new NetworkParticipant { id = id, name = id, seat = seat, ready = true, connected = true, loadout = new NetworkEmoteLoadout { emotes = new[] { "wave", "scuba", "spike" }, skinHex = "C47A4C", shirtHex = "E8505B", shortsHex = "24345E" } };
            var c = new NetworkConfiguration { lobbyID = "L", matchID = "M2", hostID = "a", localID = "a", sport = "tennis", venue = "resort", sets = 1, games = 1, seed = 9, participants = new[] { P("a", 0), P("b", 1) } };
            Send("net-bay", "start", m => { m.sport = "tennis"; m.network = JsonUtility.ToJson(c); m.seamless = true; m.bay = "bay-tennis-online"; });
            for (int i = 0; i < 2400 && !bridge.Ready; i++) yield return null;
            Assert.IsTrue(bridge.Ready); Assert.IsTrue(SportsMultiplayer.Active, "the online match is configured");
            for (int i = 0; i < 120 && UnityEngine.SceneManagement.SceneManager.GetSceneByName("Hub").isLoaded; i++) yield return null;
            Assert.IsFalse(UnityEngine.SceneManagement.SceneManager.GetSceneByName("Hub").isLoaded, "the plaza handed off and unloaded");
            Write("TOGETHER_editor_unity=PASS (online match from a bay loads behind the plaza)");
            Send("net-bay", "end");
        }

        [UnityTest, Timeout(600000)] public IEnumerator OnlineTennisWearsTheLobbyLooks()
        {
            host = new GameObject("NativeSportsSession"); Object.DontDestroyOnLoad(host); bridge = host.AddComponent<NativeSportsSession>();
            NetworkParticipant P(string id, int seat, bool female, string shirt, string skin) => new NetworkParticipant { id = id, name = id, seat = seat, ready = true, loaded = true, connected = true, female = female,
                loadout = new NetworkEmoteLoadout { emotes = new[] { "wave", "scuba", "spike" }, skinHex = skin, shirtHex = shirt, shortsHex = "24345E" } };
            var c = new NetworkConfiguration { lobbyID = "L", matchID = "M", hostID = "a", localID = "a", sport = "tennis", venue = "resort", sets = 1, games = 1, seed = 5,
                participants = new[] { P("a", 0, false, "E8505B", "EEBB8F"), P("b", 1, true, "3F62CC", "965835") } };
            Send("net-look", "start", m => { m.sport = "tennis"; m.network = JsonUtility.ToJson(c); });
            for (int i = 0; i < 900 && !bridge.Ready; i++) yield return null;
            Assert.IsTrue(bridge.Ready);
            var game = Object.FindFirstObjectByType<TennisGame>();
            HeroTennisDriver Hero(TennisActor a) => a.GetComponentsInChildren<HeroTennisDriver>(true).Single();
            var near = Hero(game.Player); var far = Hero(game.Opponent);
            Assert.IsFalse(near.matchLook.female); Assert.IsTrue(far.matchLook.female, "the far hero follows the participant's body");
            Assert.IsTrue(near.matchLook.TryGetKitColour(MatchHeroLook.RoleShirt, out var nearShirt)); Assert.IsTrue(far.matchLook.TryGetKitColour(MatchHeroLook.RoleShirt, out var farShirt));
            Color Want(string hex) { ColorUtility.TryParseHtmlString("#" + hex, out var c0); var k = MatchHeroLook.KitTint(c0); return k; }
            Assert.That(Vector4.Distance(nearShirt, Want("E8505B")), Is.LessThan(.02f), "your shirt colour");
            Assert.That(Vector4.Distance(farShirt, Want("3F62CC")), Is.LessThan(.02f), "your friend's shirt colour");
            Assert.That(Vector4.Distance(far.matchLook.skinTone, HeroKit.Hex("965835")), Is.LessThan(.02f), "your friend's skin");
            Write("LOOK_editor=PASS (near E8505B, far 3F62CC/965835 female)");
            Send("net-look", "end");
        }

        /// Proof stills for phase 4 (written only when HUB_LOOK_DIR is set): three friends on the party pads, the party seated
        /// together in the golf online bay, and Call party's beacon.
        [UnityTest, Timeout(300000)] public IEnumerator PartyStills()
        {
            string dir = Environment.GetEnvironmentVariable("HUB_LOOK_DIR"); if (string.IsNullOrEmpty(dir)) Assert.Ignore("HUB_LOOK_DIR not set");
            yield return BootHub(); var hub = HubWorld.Find(); var p = hub.Player; var cam = hub.Camera;
            var names = new[] { "Mia", "Leo", "Ava" }; var shirts = new[] { "FF63B8", "7BDA4A", "A970FF" }; var skins = new[] { "EEBB8F", "965835", "C47A4C" };
            var emotes = new[] { HeroTennisDriver.Clip.IntroWave.ToString(), HeroTennisDriver.Clip.IntroBringIt.ToString(), "" };
            void Friends(string place, string bay, Func<int, Vector3> at, float yaw, bool emote)
            {
                for (int w = 0; w < 3; w++)
                {
                    var q = at(w);
                    var s = new RemoteState { id = "friend-" + w, name = names[w], colour = w + 1, place = place, bay = bay, female = w % 2 == 0, skinHex = skins[w],
                        shirt = shirts[w], shorts = "24345E", x = q.x, y = q.y, z = q.z, yaw = yaw, speed = 0, emote = emote ? emotes[w] : "" };
                    Send("hub-p", "hubRemote", m => m.remote = JsonUtility.ToJson(s));
                }
            }
            IEnumerator Hold(string place, string bay, Func<int, Vector3> at, float yaw, bool emote, float seconds)
            {
                for (float t = 0; t < seconds; t += .1f) { Friends(place, bay, at, yaw, emote && t > seconds - 1.2f); yield return new WaitForSecondsRealtime(.1f); }
            }
            p.Teleport(HubLayout.Spawn, 0);
            yield return Hold(HubLayout.Plaza, "", w => new Vector3(-3.45f + w * 2.3f, 0, 9.3f), 180, true, 5f);
            Still(dir, cam, "p4_party_plaza");
            Send("hub-p", "hubCall", m => m.mode = "bay-golf-online"); yield return new WaitForSecondsRealtime(.6f);
            Still(dir, cam, "p4_call_party");
            hub.GoTo("bay-golf-online", true); while (hub.Transitioning) yield return null;
            for (int i = 0; i < 120 && !hub.SeatedInBay; i++) yield return null;
            var bay = HubLayout.SpotById("bay-golf-online").position;
            yield return Hold(HubLayout.GolfRoom, "bay-golf-online", w => bay, 0, false, 4f);
            Still(dir, cam, "p4_party_bay_seated");
            Assert.AreEqual(3, HubRemotes.For(hub).Count);
        }
        static void Still(string dir, Camera cam, string name, int w = 1920, int h = 1080)
        {
            Directory.CreateDirectory(dir);
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32); rt.antiAliasing = 4;
            var prevT = cam.targetTexture; var prevA = cam.aspect;
            cam.targetTexture = rt; cam.aspect = w / (float)h; cam.Render(); cam.targetTexture = prevT; cam.aspect = prevA;
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
            RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
            File.WriteAllBytes(System.IO.Path.Combine(dir, name + ".png"), tex.EncodeToPNG()); Object.Destroy(tex);
        }
    }
}
