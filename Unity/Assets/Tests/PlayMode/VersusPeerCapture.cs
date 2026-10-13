using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using GolfArcade.Game;
using GolfArcade.Multiplayer;
using GolfArcade.Tennis;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GolfArcade.PlayTests
{
    /// One phone of a two-phone versus tennis match (TennisGame.Versus): run two editors at once, one per seat (MP_SEAT=0/1), joined
    /// over UDP on this Mac with a simulated network (delay, jitter, lost pose updates). Each plays its own player only through single
    /// player's controls (toss, serve aim, the phone's swing onset then confirmation, dives, emotes) and writes what it saw; the
    /// windowed one records its TV. Not a gate on its own: compare the two summaries (scripts in the session scratchpad).
    public class VersusPeerCapture
    {
        static string Env(string key, string fallback) { var v = Environment.GetEnvironmentVariable(key); return string.IsNullOrEmpty(v) ? fallback : v; }
        static double Now => Time.realtimeSinceStartupAsDouble;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        sealed class Link : IDisposable
        {
            readonly UdpClient udp; readonly IPEndPoint peer;
            readonly List<(double due, string text)> inbox = new(); readonly System.Random rng = new(17);
            public double Latency, Jitter, PoseLoss; public int Sent, Received, Dropped;
            public Link(int self, int other) { udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, self)); peer = new IPEndPoint(IPAddress.Loopback, other); }
            public void Send(string text) { var b = Encoding.UTF8.GetBytes(text); try { udp.Send(b, b.Length, peer); Sent++; } catch { } }
            /// Datagrams that have "arrived" (after the simulated delay), in send order.
            public IEnumerable<string> Pump()
            {
                while (udp.Available > 0)
                {
                    IPEndPoint from = null; byte[] b;
                    try { b = udp.Receive(ref from); } catch { break; }
                    string text = Encoding.UTF8.GetString(b);
                    if (text.StartsWith("{") && PoseLoss > 0 && text.Contains("\\\"k\\\":\\\"pose\\\"") && rng.NextDouble() < PoseLoss) { Dropped++; continue; }
                    double delay = text.StartsWith("{") ? Math.Max(.002, Latency + (rng.NextDouble() * 2 - 1) * Jitter) : 0;
                    inbox.Add((Now + delay, text)); Received++;
                }
                while (inbox.Count > 0 && inbox[0].due <= Now) { var t = inbox[0].text; inbox.RemoveAt(0); yield return t; }
            }
            public void Dispose() { try { udp.Close(); } catch { } }
        }

        sealed class Recorder
        {
            public const int W = 1280, H = 720, Fps = 30;
#if UNITY_EDITOR
            UnityEditor.Media.MediaEncoder encoder;
#endif
            RenderTexture grab, scaled; readonly Texture2D tex; int lastGrabFrame = -1; double next = -1; public readonly string Dir;
            public Recorder(string dir, string name)
            {
                Dir = dir; tex = new Texture2D(W, H, TextureFormat.RGBA32, false); scaled = new RenderTexture(W, H, 0, RenderTextureFormat.ARGB32);
#if UNITY_EDITOR
                encoder = new UnityEditor.Media.MediaEncoder(Path.Combine(dir, name + ".mp4"), new UnityEditor.Media.VideoTrackAttributes { frameRate = new UnityEditor.Media.MediaRational(Fps), width = W, height = H, includeAlpha = false, bitRateMode = UnityEditor.VideoBitrateMode.Medium });
#endif
            }
            void Grab()
            {
                // the game's own picture at the Game view's resolution (not the editor window's back buffer, which may be scaled or cropped)
                var shot = ScreenCapture.CaptureScreenshotAsTexture();
                if (!shot) return;
                if (shot.width == W && shot.height == H) { tex.SetPixels32(shot.GetPixels32()); tex.Apply(); }
                else {
                    Graphics.Blit(shot, scaled);
                    var prev = RenderTexture.active; RenderTexture.active = scaled; tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply(); RenderTexture.active = prev;
                }
                Object.Destroy(shot);
            }
            public void Frame()
            {
                if (Time.frameCount == lastGrabFrame) return;
                lastGrabFrame = Time.frameCount;
                double now = Now; if (next < 0) next = now; if (now < next) return;
                Grab(); int n = 0;
                while (next <= now && n < 120) {
#if UNITY_EDITOR
                    encoder.AddFrame(tex);
#endif
                    n++; next += 1.0 / Fps; }
                if (next <= now) next = now + 1.0 / Fps;
            }
            public void Close()
            {
#if UNITY_EDITOR
                encoder?.Dispose(); encoder = null;
#endif
            }
        }

        [Serializable] class Summary
        {
            public int seat; public string view; public double seconds; public bool complete; public string final;
            public List<string> scores = new(); public List<string> events = new(); public List<string> hitches = new(); public List<string> contacts = new();
            public string[] rivalStrokes; public float rivalGapMean, rivalGapMax, visibleGapMean, visibleGapMax, heldSeconds; public int rivalHits, holds, visibleOver30cm;
            public int callsMade, callsTaken, sent, received, dropped, errors, exceptions, warnings, frames, slowFrames; public float worstFrame;
            public int mySwings, myHits, myServes, myDives, myEmotes, honestMisses;
        }

        [UnityTest, Timeout(1800000)]
        public IEnumerator TennisVersusPeer()
        {
            if (Environment.GetEnvironmentVariable("MP_VERSUS") != "1") Assert.Ignore("two-editor capture: MP_VERSUS=1");
            LogAssert.ignoreFailingMessages = true;
            int seat = int.Parse(Env("MP_SEAT", "0"));
            string view = Env("MP_VIEW", "near");
            string dir = Env("MP_OUT", Path.GetFullPath("Library/Captures/versus")); Directory.CreateDirectory(dir);
            bool record = Env("MP_RECORD", "0") == "1" && !Application.isBatchMode;
            var sum = new Summary { seat = seat, view = view };
            void Log(string text) { sum.events.Add($"{Now:F2} {text}"); }
            Application.logMessageReceived += (msg, stack, type) => {
                if (type == LogType.Error || type == LogType.Assert) { sum.errors++; if (sum.errors < 12) Log("[Error] " + msg.Split('\n')[0]); }
                else if (type == LogType.Exception) { sum.exceptions++; if (sum.exceptions < 12) Log("[Exception] " + msg.Split('\n')[0] + " @ " + (stack ?? "").Split('\n').FirstOrDefault()); }
                else if (type == LogType.Warning) sum.warnings++;
                else if (msg.StartsWith("[Versus]")) Log(msg);
            };
#if UNITY_EDITOR
            if (record) try {
                // the Game view filling the editor window, so its 1280x720 picture is not cropped to a small panel
                UnityEditor.PlayModeWindow.SetCustomRenderingResolution(1280, 720, "versus 720p");
            } catch (Exception e) { Debug.LogWarning("[versus] game view: " + e.Message); }
#endif
            SportsMultiplayer.TestOutput = null; SportsMultiplayer.Shutdown();
            var c = new NetworkConfiguration
            {
                lobbyID = "versus", matchID = Env("MP_MATCH", "match-1"), hostID = "a", localID = seat == 0 ? "a" : "b", sport = "tennis", venue = Env("MP_VENUE", "resort"),
                sets = 1, games = int.Parse(Env("MP_GAMES", "1")), seed = 3,
                participants = new[] {
                    new NetworkParticipant { id = "a", name = "Adnan", seat = 0, view = view == "split" ? "split" : view == "none" ? "none" : "near", loadout = new NetworkEmoteLoadout { emotes = new[] { "wave", "spike", "scuba" } } },
                    new NetworkParticipant { id = "b", name = "Sam", seat = 1, female = true, view = view == "split" ? "none" : view == "none" ? "split" : "near", loadout = new NetworkEmoteLoadout { emotes = new[] { "bringIt", "thrust", "pushups" }, shirtHex = "FF7A45" } } }
            };
            Assert.IsTrue(c.Valid, "configuration");
            using var link = new Link(int.Parse(Env("MP_PORT_SELF", seat == 0 ? "47110" : "47111")), int.Parse(Env("MP_PORT_PEER", seat == 0 ? "47111" : "47110")))
            { Latency = double.Parse(Env("MP_LATENCY", ".06"), Inv), Jitter = double.Parse(Env("MP_JITTER", ".025"), Inv), PoseLoss = double.Parse(Env("MP_LOSS", ".03"), Inv) };
            SportsMultiplayer.TestOutput = json => { if (json.Contains("\"kind\":\"peer\"") || json.Contains("\"kind\":\"result\"")) link.Send(json); };

            var root = new GameObject("Versus peer " + seat); Object.DontDestroyOnLoad(root);
            var bridge = root.AddComponent<NativeSportsSession>();
            const string id = "versus-tennis";
            Recorder rec = null;
            double epoch = DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalSeconds - Now;
            try
            {
                bridge.Receive(JsonUtility.ToJson(new NativeSportsSession.Message { version = 1, session = id, action = "start", sport = "tennis", venue = c.venue, touch = false,
                    female = seat == 1, network = JsonUtility.ToJson(c), emotes = c.participants[seat].loadout.emotes, sets = 1, games = c.games, mode = "exhibition", intros = "full" }));
                // (batch mode never draws a frame, and the launch waits for one: draw it by hand, as a controller-only phone's Unity does off screen)
                for (int i = 0; i < 4000 && !bridge.Ready; i++) { if (Application.isBatchMode && bridge.GameplayCamera) bridge.GameplayCamera.Render(); yield return null; }
                Assert.IsTrue(bridge.Ready, "tennis loaded");
                Msg(bridge, id, "resume");
                var net = SportsMultiplayer.Instance; var game = Object.FindFirstObjectByType<TennisGame>();
                Assert.IsTrue(net && game && game.Versus, "versus configured");
                Log($"loaded, versus seat {game.VersusSeat}, rival {game.VersusRivalName}, screen {Screen.width}x{Screen.height}");

                // hello: clock epochs (the shared clock is the host's), then the host starts the match
                double peerEpoch = double.NaN, runAt = double.NaN, nextHello = 0, waitUntil = Now + 300;
                while ((double.IsNaN(peerEpoch) || double.IsNaN(runAt)) && Now < waitUntil)
                {
                    if (Now >= nextHello) { nextHello = Now + .2; link.Send($"HELLO|{seat}|{epoch.ToString("R", Inv)}"); }
                    foreach (var text in link.Pump())
                    {
                        if (text.StartsWith("HELLO|")) peerEpoch = double.Parse(text.Split('|')[2], Inv);
                        else if (text.StartsWith("RUN|")) runAt = double.Parse(text.Split('|')[1], Inv);
                        else if (text.StartsWith("{")) net.Receive(text);
                    }
                    if (seat == 0 && !double.IsNaN(peerEpoch) && double.IsNaN(runAt)) runAt = net.SharedClock + 1.5;
                    if (seat == 0 && !double.IsNaN(runAt)) link.Send($"RUN|{runAt.ToString("R", Inv)}");
                    yield return null;
                }
                Assert.IsFalse(double.IsNaN(peerEpoch) || double.IsNaN(runAt), "the other phone joined");
                if (seat != 0) net.Receive(JsonUtility.ToJson(new NetworkPacket { lobbyID = c.lobbyID, matchID = c.matchID, sender = c.localID, kind = "clock", payload = (epoch - peerEpoch).ToString("R", Inv) }));
                for (int i = 0; i < 20; i++) { link.Send($"RUN|{runAt.ToString("R", Inv)}"); yield return null; }
                net.Receive(JsonUtility.ToJson(new NetworkPacket { lobbyID = c.lobbyID, matchID = c.matchID, sender = c.hostID, kind = "run", payload = runAt.ToString("R", Inv) }));
                Log($"run at {runAt:F2} (shared), clock offset {(seat == 0 ? 0 : epoch - peerEpoch):F3}");
                if (record) rec = new Recorder(dir, $"versus_seat{seat}_{view}");

                var rng = new System.Random(seat == 0 ? 101 : 202);
                float R() => (float)rng.NextDouble();
                double end = Now + double.Parse(Env("MP_SECONDS", "240"), Inv), completeAt = -1, lastFrame = Now;
                double holdStart = -1, tossStart = -1, swingAtToss = 0, tossDelay = 1.2; bool tossed = false, serveSwung = false, introEmote = false;
                long lastPoint = -1; int confirmIn = -1; float confirmPower = 0, confirmLift = 0, confirmFacing = 0; double nudgeUntil = 0; float nudge = 0;
                float swingAt = .15f; bool planned = false; var flowSeen = TennisGame.Phase.MatchOver; int lastHits = 0;
                while (Now < end)
                {
                    if (rec != null) { yield return new WaitForEndOfFrame(); rec.Frame(); }
                    yield return null;
                    double dt = Now - lastFrame; lastFrame = Now; sum.frames++;
                    if (dt > 1 / 30.0) sum.slowFrames++; sum.worstFrame = Mathf.Max(sum.worstFrame, (float)dt);
                    if (dt > .2) sum.hitches.Add($"{Now:F2} {dt * 1000:F0} ms {game.Flow}");
                    foreach (var text in link.Pump()) if (text.StartsWith("{")) net.Receive(text);
                    if (game.Match.Complete) { if (completeAt < 0) { completeAt = Now; Log("MATCH COMPLETE " + game.Match.FinalScore); } if (Now - completeAt > 9) break; continue; }

                    // ---- this phone's player, through single player's controls
                    if (game.IntroPlaying && !introEmote && game.EmoteWindow == "intro" && R() < .02f) { introEmote = true; if (game.RequestEquippedEmote(2)) { sum.myEmotes++; Log("intro emote"); } }
                    if (game.Flow != flowSeen) { flowSeen = game.Flow; if (flowSeen == TennisGame.Phase.PlayerServeHold) { holdStart = Now; tossed = false; serveSwung = false; tossDelay = .9 + R() * 1.6; nudge = R() < .3f ? (R() < .5f ? -1 : 1) : 0; nudgeUntil = Now + .3 + R() * .5; game.SetServeAim(R() * 2 - 1, .3f + R() * .7f); } if (flowSeen == TennisGame.Phase.PlayerServeToss) { tossStart = Now; swingAtToss = .62 + R() * .38; } }
                    if (game.Flow == TennisGame.Phase.PlayerServeHold)
                    {
                        game.ServeNudge = Now < nudgeUntil ? nudge : 0;
                        if (!tossed && Now - holdStart > tossDelay) { tossed = true; game.ServeNudge = 0; game.Toss(); }
                    }
                    if (game.Flow == TennisGame.Phase.PlayerServeToss && !serveSwung && Now - tossStart >= swingAtToss)
                    { serveSwung = true; sum.myServes++; game.BeginSwing(0, .3f, 1); confirmIn = 3; confirmPower = .7f + R() * .3f; confirmLift = .3f; confirmFacing = 1; }
                    if (game.Flow == TennisGame.Phase.Rally && game.BallVelocity.z < -1 && !game.Player.Swinging && confirmIn < 0)
                    {
                        float toContact = (game.BallPosition.z - (game.Player.transform.position.z + .65f)) / -game.BallVelocity.z;
                        if (!planned) { planned = true; float roll = R(); swingAt = roll < .15f ? .3f + R() * .08f : roll < .3f ? .06f + R() * .04f : .14f + (R() - .5f) * .08f; }
                        if (game.CanDive && Mathf.Abs(game.PredictedInterceptX - game.Player.transform.position.x) > 2.2f && toContact < .6f && R() < .5f) { if (game.RequestDive()) { sum.myDives++; Log("dive"); } }
                        else if (toContact < swingAt && toContact > .015f)
                        {
                            planned = false; sum.mySwings++;
                            float lift = R() < .2f ? -.1f : R() < .25f ? .26f : 0, facing = game.BallPosition.x < game.Player.transform.position.x ? -1 : 1;
                            game.BeginSwing(0, lift, facing); confirmIn = 2 + rng.Next(3); confirmPower = .5f + R() * .45f; confirmLift = lift; confirmFacing = facing;
                        }
                    }
                    if (game.Flow != TennisGame.Phase.Rally || game.BallVelocity.z > 0) planned = false;
                    if (confirmIn >= 0 && confirmIn-- == 0) game.RequestSwing(confirmPower, 0, confirmLift, confirmFacing);
                    if (game.EmoteWindow == "point" && R() < .03f) { int slot = rng.Next(3); if (game.RequestEquippedEmote(slot)) { sum.myEmotes++; Log("point emote " + slot); } }
                    if (game.VersusPoints != lastPoint) { lastPoint = game.VersusPoints; }
                    if (game.Hits != lastHits) { lastHits = game.Hits; sum.myHits = lastHits; }
                }
                sum.seconds = Now - (end - double.Parse(Env("MP_SECONDS", "240"), Inv));
                sum.complete = game.Match.Complete; sum.final = game.Match.Complete ? game.Match.FinalScore : game.Match.Scoreboard;
                sum.scores = game.VersusScoreLog.ToList();
                sum.rivalStrokes = game.VersusRivalStrokes.Select(kv => kv.Key + "=" + kv.Value).ToArray();
                sum.contacts = game.VersusContactLog.ToList();
                sum.visibleGapMean = game.VersusRivalVisibleGaps.Mean; sum.visibleGapMax = game.VersusRivalVisibleGaps.Max; sum.holds = game.VersusHolds; sum.heldSeconds = game.VersusHeldSeconds;
                sum.rivalGapMean = game.VersusRivalGaps.Mean; sum.rivalGapMax = game.VersusRivalGaps.Max; sum.rivalHits = game.VersusRivalGaps.Count;
                sum.callsMade = game.VersusCallsMade; sum.callsTaken = game.VersusCallsTaken; sum.honestMisses = game.HonestMisses;
                sum.sent = link.Sent; sum.received = link.Received; sum.dropped = link.Dropped;
            }
            finally
            {
                rec?.Close();
                File.WriteAllText(Path.Combine(dir, $"seat{seat}_summary.json"), JsonUtility.ToJson(sum, true));
                try { Msg(bridge, id, "end"); } catch { }
                Object.DestroyImmediate(root); SportsMultiplayer.TestOutput = null; SportsMultiplayer.Shutdown();
                if (SportsMultiplayer.Instance) Object.DestroyImmediate(SportsMultiplayer.Instance.gameObject);
            }
        }

        static void Msg(NativeSportsSession bridge, string session, string action, float value = 0, float value2 = 0) =>
            bridge.Receive(JsonUtility.ToJson(new NativeSportsSession.Message { version = 1, session = session, action = action, value = value, value2 = value2 }));
    }
}
