using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using GolfArcade.Course;
using GolfArcade.Game;
using GolfArcade.Multiplayer;
using GolfArcade.Shot;
using GolfArcade.Swing;
using GolfArcade.Tennis;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GolfArcade.PlayTests
{
    /// Exploratory capture, not a gate. Multiplayer tennis on one split TV and a two-phone golf round (plus solo golf for comparison),
    /// both seats driven by scripted controls through the same entry points the phones use, recorded to MP4 with a small "TEST INPUT"
    /// overlay, with an event log. The second player's inputs arrive through a simulated network (delay + jitter).
    /// Runs only with MP_EXPLORE=1, windowed.
    public class MultiplayerExploreCapture
    {
        static string Env(string key, string fallback) { var v = Environment.GetEnvironmentVariable(key); return string.IsNullOrEmpty(v) ? fallback : v; }
        static string Out => Env("MP_EXPLORE_DIR", Path.GetFullPath("Library/Captures/mp-explore"));
        static double Now => Time.realtimeSinceStartupAsDouble;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        static void Clean()
        {
            SportsMultiplayer.TestOutput = null; SportsMultiplayer.Shutdown();
            if (SportsMultiplayer.Instance) Object.DestroyImmediate(SportsMultiplayer.Instance.gameObject);
            Time.timeScale = 1; Time.captureFramerate = 0;
        }

        static void GameView()
        {
#if UNITY_EDITOR
            try { UnityEditor.PlayModeWindow.SetCustomRenderingResolution(1280, 720, "mp-explore 720p"); }
            catch (Exception e) { Debug.LogWarning("[explore] game view size: " + e.Message); }
#endif
        }

        static string Packet(NetworkConfiguration c, string kind, string payload = "", string sender = "a") =>
            JsonUtility.ToJson(new NetworkPacket { lobbyID = c.lobbyID, matchID = c.matchID, sender = sender, kind = kind, payload = payload });

        // ---------------------------------------------------------------- recording

        sealed class Recorder
        {
            public const int W = 1280, H = 720, Fps = 30;
#if UNITY_EDITOR
            UnityEditor.Media.MediaEncoder encoder;
#endif
            RenderTexture grab, scaled; readonly Texture2D tex; double next = -1, lastFrame; readonly double start;
            public int Frames; public readonly string Dir;
            public readonly List<string> Hitches = new();
            public float WorstFrame; public int SlowFrames, AllFrames; public double FrameSum;
            public Recorder(string dir, string name)
            {
                Dir = dir; Directory.CreateDirectory(dir);
                tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
                scaled = new RenderTexture(W, H, 0, RenderTextureFormat.ARGB32);
#if UNITY_EDITOR
                var attrs = new UnityEditor.Media.VideoTrackAttributes { frameRate = new UnityEditor.Media.MediaRational(Fps), width = W, height = H, includeAlpha = false, bitRateMode = UnityEditor.VideoBitrateMode.Medium };
                encoder = new UnityEditor.Media.MediaEncoder(Path.Combine(dir, name + ".mp4"), attrs);
#endif
                start = lastFrame = Now;
            }
            public double T => Now - start;
            void Grab()
            {
                var shot = ScreenCapture.CaptureScreenshotAsTexture();
                if (!shot) return;
                if (shot.width == W && shot.height == H) { tex.SetPixels32(shot.GetPixels32()); tex.Apply(); }
                else { Graphics.Blit(shot, scaled); var prev = RenderTexture.active; RenderTexture.active = scaled; tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply(); RenderTexture.active = prev; }
                Object.Destroy(shot);
            }
            /// After WaitForEndOfFrame, every frame. Real time: a slow frame is repeated, so a hitch shows as a freeze in the video.
            public void Frame(string context)
            {
                double now = Now; float dt = (float)(now - lastFrame); lastFrame = now;
                AllFrames++; FrameSum += dt; WorstFrame = Mathf.Max(WorstFrame, dt);
                if (dt > 1 / 30f) SlowFrames++;
                if (dt > .15f) Hitches.Add($"{T,7:F2}  {dt * 1000,5:F0} ms  {context}");
                if (next < 0) next = now;
                if (now < next) return;
                Grab();
                int n = 0;
                while (next <= now && n < 120)
                {
#if UNITY_EDITOR
                    encoder.AddFrame(tex);
#endif
                    Frames++; n++; next += 1.0 / Fps;
                }
                if (next <= now) next = now + 1.0 / Fps;
            }
            public void Still(string name) { Grab(); File.WriteAllBytes(Path.Combine(Dir, name + ".png"), tex.EncodeToPNG()); }
            public void Close()
            {
#if UNITY_EDITOR
                encoder?.Dispose(); encoder = null;
#endif
                if (grab) Object.Destroy(grab); if (scaled) Object.Destroy(scaled); if (tex) Object.Destroy(tex);
            }
        }

        /// A small box in a corner saying which control each player just used (so the video shows what was pressed). Not game UI.
        sealed class InputOverlay : MonoBehaviour
        {
            public string Title = "TEST INPUT (simulated phones)";
            public string[] Names = { "P1", "P2" };
            public Color[] Colors = { new(.84f, .94f, .27f), new(1, .62f, .42f) };
            public readonly string[] Lines = new string[2]; readonly double[] at = new double[2];
            public Vector2? Stick; public string StickLabel = "";
            public bool Bottom;
            GUIStyle style, head; Texture2D dot;
            public void Say(int seat, string text) { if (seat < 0 || seat > 1) return; Lines[seat] = text; at[seat] = Now; }
            void OnGUI()
            {
                if (style == null)
                {
                    style = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, richText = true, clipping = TextClipping.Clip };
                    style.normal.textColor = Color.white;
                    head = new GUIStyle(style) { fontSize = 11 }; head.normal.textColor = new Color(1, 1, .65f);
                    dot = Texture2D.whiteTexture;
                }
                float s = Screen.height / 720f; var keep = GUI.matrix; GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1));
                float w = 360, h = Stick.HasValue ? 122 : 68, x = Screen.width / s - w - 12, y = Bottom ? 720 - h - 96 : 74;
                GUI.color = new Color(0, 0, 0, .58f); GUI.DrawTexture(new Rect(x, y, w, h), dot); GUI.color = Color.white;
                GUI.Label(new Rect(x + 8, y + 2, w, 16), Title, head);
                for (int i = 0; i < 2; i++)
                {
                    bool fresh = Now - at[i] < 1.4;
                    string c = "#" + ColorUtility.ToHtmlStringRGB(Colors[i]);
                    GUI.Label(new Rect(x + 8, y + 18 + i * 23, w - 16, 23), $"<color={c}>{Names[i]}</color>  " + (fresh ? "" : "<color=#9a9a9a>") + (Lines[i] ?? "") + (fresh ? "" : "</color>"), style);
                }
                if (Stick.HasValue)
                {
                    float cx = x + 36, cy = y + 90, r = 24;
                    GUI.color = new Color(1, 1, 1, .25f); GUI.DrawTexture(new Rect(cx - r, cy - r, 2 * r, 2 * r), dot);
                    var v = Stick.Value; GUI.color = new Color(1, .9f, .2f);
                    GUI.DrawTexture(new Rect(cx + v.x * r - 6, cy - v.y * r - 6, 12, 12), dot); GUI.color = Color.white;
                    GUI.Label(new Rect(cx + r + 10, cy - 12, w - 80, 24), StickLabel, style);
                }
                GUI.matrix = keep;
            }
        }

        sealed class EventLog
        {
            readonly StringBuilder sb = new(); readonly string path; readonly HashSet<string> seen = new();
            public int Errors, Warnings, Exceptions; public Func<double> Clock = () => 0;
            public EventLog(string path) { this.path = path; Directory.CreateDirectory(Path.GetDirectoryName(path)); }
            public void Add(string text) => sb.AppendLine($"{Clock(),7:F2}  {text}");
            public void Raw(string text) => sb.AppendLine(text);
            public void Unity(string msg, string stack, LogType type)
            {
                if (type == LogType.Log) return;
                if (type == LogType.Warning) Warnings++; else if (type == LogType.Exception) Exceptions++; else Errors++;
                string first = (msg ?? "").Split('\n')[0];
                if (seen.Add(type + first.Substring(0, Math.Min(120, first.Length))))
                    sb.AppendLine($"{Clock(),7:F2}  [{type}] {first}" + (type == LogType.Exception ? "  @ " + (stack ?? "").Split('\n').FirstOrDefault() : ""));
            }
            public void Save() => File.WriteAllText(path, sb.ToString());
        }

        /// The second phone's packets: sent now, delivered after a network delay (in order, like the reliable channel).
        sealed class Wire
        {
            readonly NetworkConfiguration c; readonly List<(double due, string json)> queue = new(); readonly System.Random rng = new(5); long events;
            public double Latency = .06, Jitter = .025;
            public Wire(NetworkConfiguration c) { this.c = c; }
            public void Input(string sender, NetworkInput input)
            {
                var net = SportsMultiplayer.Instance; if (!net) return;
                input.time = net.SharedClock; if (input.eventID == 0) input.eventID = ++events;
                var p = new NetworkPacket { lobbyID = c.lobbyID, matchID = c.matchID, sender = sender, kind = "input", payload = JsonUtility.ToJson(input), sentAt = net.SharedClock };
                queue.Add((Now + Math.Max(.005, Latency + (rng.NextDouble() * 2 - 1) * Jitter), JsonUtility.ToJson(p)));
            }
            public void Pump()
            {
                while (queue.Count > 0 && queue[0].due <= Now) { var json = queue[0].json; queue.RemoveAt(0); if (SportsMultiplayer.Instance) SportsMultiplayer.Instance.Receive(json); }
            }
        }

        static void Msg(NativeSportsSession bridge, string session, string action, float value = 0, float value2 = 0) =>
            bridge.Receive(JsonUtility.ToJson(new NativeSportsSession.Message { version = 1, session = session, action = action, value = value, value2 = value2 }));

        // ---------------------------------------------------------------- tennis

        sealed class TennisBot
        {
            public bool introSent; public long emotedPoint = -1; public int emoteCycle;
            public long servePoint = -1; public bool serveSecond, aimSent, tossSent, serveSwung; public double tossAt, tossedAt, swingAfterToss, nudgeUntil, nextNudge; public int nudgeDir; public float aim, depth;
            public long plannedContact = -1; public float offset; public bool skip, dive, dived, aimed; public long swungContact = -1;
            public int swingSeq, startSeq;
        }

        [UnityTest, Timeout(1500000)]
        public IEnumerator TennisSplitScreen()
        {
            if (Environment.GetEnvironmentVariable("MP_EXPLORE") != "1") Assert.Ignore("exploratory capture: run with MP_EXPLORE=1");
            LogAssert.ignoreFailingMessages = true;
            string dir = Path.Combine(Out, "tennis");
            var log = new EventLog(Path.Combine(dir, "tennis_events.txt"));
            Application.logMessageReceived += log.Unity;
            Clean(); GameView();
            var root = new GameObject("MP explore (tennis)"); Object.DontDestroyOnLoad(root);
            var bridge = root.AddComponent<NativeSportsSession>();
            var overlay = root.AddComponent<InputOverlay>(); overlay.Names = new[] { "ADNAN", "SAM" };
            int games = int.TryParse(Env("MP_GAMES", "3"), out var g) ? g : 3;
            const string id = "explore-tennis";
            var c = new NetworkConfiguration
            {
                lobbyID = "couch", matchID = "split-" + DateTime.Now.Ticks, hostID = "a", localID = "a", sport = "tennis", venue = Env("MP_VENUE", "resort"), sets = 1, games = games, seed = 11,
                participants = new[] {
                    new NetworkParticipant { id = "a", name = "Adnan", seat = 0, view = NetworkConfiguration.ViewSplit, loadout = new NetworkEmoteLoadout { emotes = new[] { "wave", "spike", "scuba" } } },
                    new NetworkParticipant { id = "b", name = "Sam", seat = 1, view = NetworkConfiguration.ViewNone, female = true, loadout = new NetworkEmoteLoadout { emotes = new[] { "bringIt", "thrust", "pushups" } } } }
            };
            Assert.IsTrue(c.Valid, "configuration");
            var rng = new System.Random(int.TryParse(Env("MP_SEED", "3"), out var sd) ? sd : 3);
            float R() => (float)rng.NextDouble();
            Recorder rec = null; Wire wire = new Wire(c) { Latency = double.Parse(Env("MP_LATENCY", "0.06"), Inv) };
            try
            {
                bridge.Receive(JsonUtility.ToJson(new NativeSportsSession.Message { version = 1, session = id, action = "start", sport = "tennis", venue = c.venue, touch = false, network = JsonUtility.ToJson(c), emotes = c.participants[0].loadout.emotes, sets = 1, games = games }));
                double loadStart = Now;
                for (int i = 0; i < 3000 && !bridge.Ready; i++) yield return null;
                Assert.IsTrue(bridge.Ready, "tennis loaded");
                log.Raw($"load {Now - loadStart:F1} s, screen {Screen.width}x{Screen.height}, latency guest->host {wire.Latency * 1000:F0}±{wire.Jitter * 1000:F0} ms");
                Msg(bridge, id, "resume");
                var net = SportsMultiplayer.Instance; var tennis = Object.FindFirstObjectByType<TennisGame>();
                Assert.IsTrue(net && tennis, "multiplayer + tennis");
                net.Receive(Packet(c, "run"));
                rec = new Recorder(dir, "tennis_split_screen"); log.Clock = () => rec.T;
                var bots = new[] { new TennisBot(), new TennisBot() };
                string lastPhase = ""; long lastContact = -1, lastPoint = -1; int lastBounces = 0; string lastScore = ""; int[] dives = new int[2]; int[] emotes = new int[2];
                int contactStills = 0, pointStills = 0; double stillAt = -1; string stillName = null; bool introStill = false, meterStill = false, tossStill = false;
                double completeAt = -1, end = Now + double.Parse(Env("MP_TENNIS_SECONDS", "330"), Inv);
                var grades = new Dictionary<string, int>();

                void Send(int seat, NetworkInput input, string say)
                {
                    overlay.Say(seat, say);
                    if (seat == 1) { wire.Input("b", input); return; }
                }
                void Toss(int seat)
                {
                    if (seat == 0) { Msg(bridge, id, "toss", 1); overlay.Say(0, "TOSS (tap)"); }
                    else Send(1, new NetworkInput { action = "toss", age = .04, point = net.TennisState.point, contact = net.TennisState.contact }, "TOSS (tap)");
                }
                void ServeAim(int seat, float aim, float depth)
                {
                    string say = $"serve aim {(aim < -.3f ? "wide" : aim > .3f ? "T" : "body")} · depth {depth:F1}";
                    if (seat == 0) { Msg(bridge, id, "serveAim", aim, depth); overlay.Say(0, say); }
                    else Send(1, new NetworkInput { action = "serveAim", value = aim, value2 = depth }, say);
                }
                void Nudge(int seat, int dir)
                {
                    if (seat == 0) { Msg(bridge, id, "nudge", dir); overlay.Say(0, dir < 0 ? "◀ walk baseline" : "walk baseline ▶"); }
                    else Send(1, new NetworkInput { action = "nudge", value = -dir }, dir < 0 ? "◀ walk baseline" : "walk baseline ▶");
                }
                void Dive(int seat)
                {
                    if (seat == 0) { Msg(bridge, id, "dive"); overlay.Say(0, "DIVE"); }
                    else Send(1, new NetworkInput { action = "dive", point = net.TennisState.point, contact = net.TennisState.contact }, "DIVE");
                }
                void Emote(int seat, int slot)
                {
                    string name = TennisEmotes.Name(c.participants[seat].loadout.emotes[slot]);
                    if (seat == 0) { Msg(bridge, id, "emote", slot); overlay.Say(0, "emote: " + name); }
                    else Send(1, new NetworkInput { action = "emote", value = slot, point = net.TennisState.point, contact = net.TennisState.contact }, "emote: " + name);
                    log.Add($"P{seat + 1} emote slot {slot} ({name})");
                }
                void RallyAim(int seat, float v)
                {
                    if (seat == 0) { Msg(bridge, id, "rallyAim", v, .7f); overlay.Say(0, $"rally aim {(v < 0 ? "left" : "right")}"); }
                    else Send(1, new NetworkInput { action = "aim", value = -v, value2 = .7f }, $"rally aim {(v < 0 ? "left" : "right")}");
                }
                void Swing(int seat, float power, string say)
                {
                    var b = bots[seat];
                    if (seat == 0)
                    {
                        b.startSeq++; b.swingSeq++;
                        SportsMultiplayer.Sample(new NativeSportsSession.Sample { version = 3, flags = 1, time = net.SharedClock, target = net.TennisState.players[0].target, power = power, swingStart = b.startSeq, swing = b.swingSeq, onsetTime = net.SharedClock, confirmationTime = net.SharedClock }, true);
                        overlay.Say(0, say);
                    }
                    else
                    {
                        var s = net.TennisState;
                        Send(1, new NetworkInput { action = "beginSwing", power = power, age = 0, point = s.point, contact = s.contact }, say);
                        wire.Input("b", new NetworkInput { action = "swing", power = power, age = .015, point = s.point, contact = s.contact });
                    }
                }
                void Move(int seat, float worldX)
                {
                    float target = Mathf.Clamp(worldX / 3.6f, -1, 1);
                    var b = bots[seat];
                    // (touch semantics: the aim the player set stays; a move must not reset the serve aim)
                    if (seat == 0) SportsMultiplayer.Sample(new NativeSportsSession.Sample { version = 3, flags = 1, time = net.SharedClock, target = target, power = .7f, swingStart = b.startSeq, swing = b.swingSeq }, true);
                    else if (Time.frameCount % 2 == 0) wire.Input("b", new NetworkInput { action = "move", target = target, aim = net.TennisState.players[1].aim });
                }

                while (Now < end)
                {
                    yield return new WaitForEndOfFrame();
                    var s = net.TennisState;
                    rec.Frame(s == null ? "loading" : $"{s.phase} pt{s.point} c{s.contact}");
                    if (stillName != null && Now >= stillAt) { rec.Still(stillName); stillName = null; }
                    wire.Pump();
                    if (s == null) continue;
                    double t = s.time - s.phaseAt;
                    // --- events
                    if (s.phase != lastPhase)
                    {
                        log.Add($"phase {lastPhase} -> {s.phase}  server P{s.server + 1}{(s.secondServe ? " (2nd serve)" : "")}  score {s.score.Scoreboard}  reason '{s.reason}'{(s.phase == "point" ? $"  WINNER P{s.winner + 1}" : "")}");
                        if (s.phase == "toss") log.Add($"   toss accuracy {s.tossAccuracy:F2}");
                        if (s.phase == "point" && pointStills < 4) { stillName = $"t2{pointStills}_point_{pointStills + 1}"; stillAt = Now + 1.0; pointStills++; }
                        lastPhase = s.phase;
                    }
                    if (s.contact != lastContact)
                    {
                        if (lastContact >= 0 && s.phase == "rally")
                        {
                            int hitter = 1 - s.receiver; string grade = s.serveFlight ? "SERVE" : s.reason;
                            grades[grade] = grades.TryGetValue(grade, out var n) ? n + 1 : 1;
                            log.Add($"contact #{s.contact} by P{hitter + 1}: {grade}  ball ({s.ball.x:F1},{s.ball.y:F1},{s.ball.z:F1}) speed {Mathf.Sqrt(s.velocity.x * s.velocity.x + s.velocity.z * s.velocity.z):F1} m/s");
                            if (contactStills < 6 && stillName == null) { stillName = $"t1{contactStills}_contact_{s.contact}_P{hitter + 1}"; stillAt = Now + .12; contactStills++; }
                        }
                        lastContact = s.contact; lastBounces = 0;
                    }
                    if (s.bounces != lastBounces) { lastBounces = s.bounces; }
                    if (s.point != lastPoint) { lastPoint = s.point; }
                    for (int i = 0; i < 2; i++)
                    {
                        if (s.players[i].dives != dives[i]) { dives[i] = s.players[i].dives; log.Add($"P{i + 1} DIVE accepted (stamina {s.players[i].stamina:F2})"); }
                        if (s.players[i].emoteSequence != emotes[i]) { emotes[i] = s.players[i].emoteSequence; log.Add($"P{i + 1} emote accepted: {s.players[i].emoteID}"); }
                    }
                    string sc = s.score.Scoreboard; if (sc != lastScore) { lastScore = sc; }
                    if (!introStill && s.phase == "intro" && s.time > 1.4) { introStill = true; stillName = "t00_intro"; stillAt = Now; }
                    if (!meterStill && s.phase == "serve" && t > .7) { meterStill = true; stillName = "t01_serve_meter"; stillAt = Now; }
                    if (!tossStill && s.phase == "toss" && t > .45) { tossStill = true; stillName = "t02_toss"; stillAt = Now; }
                    if (s.complete && completeAt < 0) { completeAt = Now; log.Add($"MATCH COMPLETE  {s.score.FinalScore}"); stillName = "t90_match_over"; stillAt = Now + 2.5; }
                    if (completeAt > 0) { if (Now - completeAt > 9) break; continue; }
                    if (s.paused) continue;

                    // --- the two phones
                    for (int seat = 0; seat < 2; seat++)
                    {
                        var b = bots[seat]; var p = s.players[seat]; float sign = seat == 0 ? 1 : -1;
                        if (s.phase == "intro" && !b.introSent && s.time > .8 + seat * 1.1) { b.introSent = true; Emote(seat, seat == 0 ? 0 : 2); }
                        if (s.phase == "point" && s.winner == seat && b.emotedPoint != s.point && t > .3 && R() < .8f) { b.emotedPoint = s.point; Emote(seat, b.emoteCycle++ % 3); }
                        else if (s.phase == "point" && s.winner == seat && b.emotedPoint != s.point && t > .3) b.emotedPoint = s.point;
                        bool receiving = s.phase == "rally" && s.receiver == seat;
                        float want = p.x, tArr = 99;
                        if (receiving)
                        {
                            float zc = p.z + sign * .65f, vz = s.velocity.z;
                            if (Mathf.Abs(vz) > .5f && (zc - s.ball.z) / vz > 0) { tArr = (zc - s.ball.z) / vz; want = s.ball.x + s.velocity.x * tArr; }
                        }
                        else if (s.phase == "rally") want = Mathf.Lerp(p.x, 0, .5f);
                        if (s.phase == "serve" && s.server == seat && b.nudgeDir != 0 && b.servePoint == s.point) want = p.target * 3.6f;
                        if (!(s.phase == "serve" && s.server == seat && b.nudgeDir != 0 && t < b.nudgeUntil)) Move(seat, want);

                        // serve
                        if (s.phase == "serve" && s.server == seat)
                        {
                            if (b.servePoint != s.point || b.serveSecond != s.secondServe)
                            {
                                b.servePoint = s.point; b.serveSecond = s.secondServe; b.aimSent = b.tossSent = b.serveSwung = false;
                                double[] middles = { 1.25, 1.75, 2.25 };
                                b.tossAt = middles[rng.Next(middles.Length)] + (R() < .25f ? (R() - .5f) * .55f : (R() - .5f) * .14f);
                                b.nudgeDir = R() < .35f ? (R() < .5f ? -1 : 1) : 0; b.nudgeUntil = .25 + R() * .45; b.nextNudge = 0;
                                b.aim = R() * 2 - 1; b.depth = .35f + R() * .6f;
                            }
                            if (b.nudgeDir != 0 && t > .15 && t < b.nudgeUntil && Now >= b.nextNudge) { b.nextNudge = Now + 1 / 15.0; Nudge(seat, b.nudgeDir); }
                            if (!b.aimSent && t > .5) { b.aimSent = true; ServeAim(seat, b.aim, b.depth); }
                            if (!b.tossSent && t >= b.tossAt) { b.tossSent = true; b.tossedAt = Now; b.swingAfterToss = .65 + R() * .45; Toss(seat); log.Add($"P{seat + 1} toss sent at t={t:F2} -> phase now {net.TennisState.phase}, accuracy {net.TennisState.tossAccuracy:F2}"); }
                        }
                        if (s.phase == "toss" && s.server == seat && b.tossSent && !b.serveSwung && Now - b.tossedAt >= b.swingAfterToss)
                        { b.serveSwung = true; float pw = .6f + R() * .35f; Swing(seat, pw, $"SERVE swing {pw * 100:F0}%"); log.Add($"P{seat + 1} serve swing at toss+{Now - b.tossedAt:F2} -> phase now {net.TennisState.phase} 2nd={net.TennisState.secondServe} lag {tennis.Lag:F2}"); }

                        // rally
                        if (receiving && b.plannedContact != s.contact)
                        {
                            b.plannedContact = s.contact; b.dived = b.aimed = false;
                            float roll = R();
                            b.skip = roll < .07f;
                            b.offset = roll < .2f ? -.1f - R() * .05f : roll < .33f ? .08f + R() * .05f : (R() - .5f) * .07f;
                            b.dive = R() < .65f;
                        }
                        if (receiving && !b.aimed && tArr < 1.2f && R() < .02f) { b.aimed = true; RallyAim(seat, R() < .5f ? -.7f : .7f); }
                        if (receiving && b.swungContact != s.contact && !b.skip && tArr < 9)
                        {
                            if (!b.dived && b.dive && Mathf.Abs(want - p.x) > 1.6f && tArr < .75f && p.stamina > .3f) { b.dived = true; Dive(seat); }
                            if (tArr <= TennisRules.SweetTime - b.offset)
                            {
                                b.swungContact = s.contact; float pw = .55f + R() * .4f;
                                string when = b.offset < -.06f ? "EARLY" : b.offset > .06f ? "LATE" : "on time";
                                Swing(seat, pw, $"swing {when} ({b.offset * 1000:+0;-0} ms) {pw * 100:F0}%");
                                log.Add($"P{seat + 1} swing planned {when} offset {b.offset * 1000:+0;-0} ms, reach |dx| {Mathf.Abs(want - p.x):F2} m");
                            }
                        }
                        else if (receiving && b.skip && b.swungContact != s.contact && tArr < .2f) { b.swungContact = s.contact; overlay.Say(seat, "(no swing)"); log.Add($"P{seat + 1} lets it go (no swing)"); }
                    }
                }
                var st = net.TennisState;
                log.Raw("");
                log.Raw($"grades: {string.Join(", ", grades.Select(kv => kv.Key + " " + kv.Value))}");
                log.Raw($"final: {(st != null ? st.score.Scoreboard + (st.complete ? "  complete " + st.score.FinalScore : "  (not complete)") : "-")}");
                int rejects = typeof(SportsMultiplayer).GetField("tennis", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(net) is NetworkTennisMatch m ? m.StampRejects : -1;
                log.Raw($"stamp rejects (host): {rejects}");
                log.Raw($"frames {rec.AllFrames}, mean {rec.FrameSum / Math.Max(1, rec.AllFrames) * 1000:F1} ms, worst {rec.WorstFrame * 1000:F0} ms, over 33 ms: {rec.SlowFrames}");
                log.Raw($"unity log: {log.Errors} errors, {log.Exceptions} exceptions, {log.Warnings} warnings");
                log.Raw("hitches (>150 ms):"); foreach (var h in rec.Hitches) log.Raw("  " + h);
            }
            finally
            {
                rec?.Close();
                try { Msg(bridge, id, "end"); } catch { }
                Application.logMessageReceived -= log.Unity; log.Save();
                Object.DestroyImmediate(root); Clean();
            }
        }

        // ---------------------------------------------------------------- golf

        /// One aiming gesture's outcome, for the sensitivity comparison.
        sealed class AimTrial { public string who, control; public double start, target, final, firstJump, overshoot, seconds; public int corrections; }

        sealed class GolfDriver
        {
            public NativeSportsSession bridge; public string session; public GolfGame game; public InputOverlay overlay; public EventLog log; public Wire wire;
            public bool network; public GolfArcade.UI.Joystick stick; public MethodInfo setStick;
            public readonly List<AimTrial> trials = new();
            public System.Random rng = new(9);
            /// (time, guest phone heading, TV heading) while the guest steers: the TV's lag and step size (gate G2).
            public readonly List<(double t, double phone, double tv)> tvTrace = new();
            /// The waiting golfers' positions, sampled each frame of every aiming turn (gate G1).
            public double partyMaxMove; public int partySamples;
            public float R() => (float)rng.NextDouble();
            public void SetStick(Vector2 v) { if (stick && setStick != null) setStick.Invoke(stick, new object[] { v }); overlay.Stick = v == Vector2.zero ? (Vector2?)null : v; }
        }

        static double Wrap(double d) => Mathf.DeltaAngle(0, (float)d);

        static readonly FieldInfo PartyField = typeof(GolfGame).GetField("partyGolfers", BindingFlags.NonPublic | BindingFlags.Instance);
        /// Every waiting golfer's position against where it stood when this turn's aiming began.
        static void PartySample(GolfGame game, NetworkGolfState s, GolfDriver d, Dictionary<int, Vector3> start)
        {
            if (!(PartyField?.GetValue(game) is Dictionary<int, GolferView> party)) return;
            foreach (var kv in party)
            {
                if (kv.Key == s.turn || !kv.Value) continue;
                var at = kv.Value.transform.position;
                if (!start.TryGetValue(kv.Key, out var first)) { start[kv.Key] = at; continue; }
                d.partyMaxMove = Math.Max(d.partyMaxMove, Vector3.Distance(first, at)); d.partySamples++;
            }
        }

        /// Map axes as the phone's map shows them (GolfGame.FrameMinimap: up = tee to pin).
        static double HeadingFromPad(Hole hole, float across, float forward)
        {
            var tee = HoleView.ToWorld(hole.Tee); var pin = HoleView.ToWorld(hole.Pin);
            var up = pin - tee; up.y = 0; up.Normalize(); var right = Vector3.Cross(up, Vector3.down);
            var d = right * across + up * forward;
            return Math.Atan2(d.x, d.z) * 180 / Math.PI;
        }
        static Vector2 PadFromHeading(Hole hole, double heading)
        {
            var tee = HoleView.ToWorld(hole.Tee); var pin = HoleView.ToWorld(hole.Pin);
            var up = pin - tee; up.y = 0; up.Normalize(); var right = Vector3.Cross(up, Vector3.down);
            var dir = new Vector3((float)Math.Sin(heading * Math.PI / 180), 0, (float)Math.Cos(heading * Math.PI / 180));
            return new Vector2(Vector3.Dot(dir, right), Vector3.Dot(dir, up)).normalized;
        }

        /// The best power for this club and line toward `target` (the game's own flight, no strike grading).
        static float PowerFor(Hole hole, GolfClub club, double heading, CoursePoint origin, CoursePoint target, Wind wind)
        {
            float best = .7f; double bestMiss = double.MaxValue;
            for (float p = .04f; p <= 1.001f; p += .02f)
            {
                var shot = new CourseShot(club, Impact(p), heading, origin, hole, 1, wind);
                double miss = shot.IsHoled ? -1 : shot.Rest.DistanceTo(target);
                if (miss < bestMiss) { bestMiss = miss; best = p; }
            }
            return best;
        }
        static SwingImpact Impact(float power) => new SwingImpact { Power = power, Backswing = power, PeakSpeed = power * 16, Commit = 1.2, DownswingSeconds = .25, TempoSeconds = .7 };

        [UnityTest, Timeout(1800000)]
        public IEnumerator GolfTwoPhonesOneTv() { return Golf(true); }

        [UnityTest, Timeout(1800000)]
        public IEnumerator GolfSolo() { return Golf(false); }

        IEnumerator Golf(bool network)
        {
            if (Environment.GetEnvironmentVariable("MP_EXPLORE") != "1") Assert.Ignore("exploratory capture: run with MP_EXPLORE=1");
            LogAssert.ignoreFailingMessages = true;
            string name = network ? "golf_two_phones" : "golf_solo";
            string dir = Path.Combine(Out, name);
            var log = new EventLog(Path.Combine(dir, name + "_events.txt"));
            Application.logMessageReceived += log.Unity;
            Clean(); GameView();
            var root = new GameObject("MP explore (golf)"); Object.DontDestroyOnLoad(root);
            var bridge = root.AddComponent<NativeSportsSession>();
            var overlay = root.AddComponent<InputOverlay>(); overlay.Bottom = false;
            var probe = Environment.GetEnvironmentVariable("MP_PROBE") == "1" ? root.AddComponent<HitchProbe>() : null;
            overlay.Names = network ? new[] { "ADNAN", "SAM" } : new[] { "ADNAN", "" };
            string id = "explore-" + name, course = Env("MP_COURSE", "cliffside");
            var c = new NetworkConfiguration
            {
                lobbyID = "couch", matchID = "golf-" + DateTime.Now.Ticks, hostID = "a", localID = "a", sport = "golf", venue = course, seed = 7,
                participants = new[] {
                    new NetworkParticipant { id = "a", name = "Adnan", seat = 0, loadout = new NetworkEmoteLoadout { emotes = new[] { "wave", "spike", "scuba" } } },
                    new NetworkParticipant { id = "b", name = "Sam", seat = 1, female = true, loadout = new NetworkEmoteLoadout { emotes = new[] { "bringIt", "thrust", "pushups" } } } }
            };
            if (network) Assert.IsTrue(c.Valid, "configuration");
            var wire = new Wire(c) { Latency = double.Parse(Env("MP_LATENCY", "0.06"), Inv) };
            var d = new GolfDriver { bridge = bridge, session = id, overlay = overlay, log = log, wire = wire, network = network };
            Recorder rec = null;
            try
            {
                bridge.Receive(JsonUtility.ToJson(new NativeSportsSession.Message { version = 1, session = id, action = "start", sport = "golf", course = course, mode = "round", touch = true, holeFlyover = true, intros = "full",
                    network = network ? JsonUtility.ToJson(c) : null, emotes = c.participants[0].loadout.emotes }));
                double loadStart = Now;
                for (int i = 0; i < 3000 && !bridge.Ready; i++) yield return null;
                Assert.IsTrue(bridge.Ready, "golf loaded");
                var game = Object.FindFirstObjectByType<GolfGame>(); d.game = game;
                // the phone with the TV draws Unity's controller sheet on its own screen (ConfigureDisplay does it for an external TV);
                // its stick is driven below, and its canvas is kept off the editor's one screen
                game.NativePhoneController();
                foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) if (canvas.GetComponent<GolfArcade.UI.PhoneScreenOnly>()) canvas.enabled = false;
                foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) if (cam.GetComponent<GolfArcade.UI.PhoneScreenOnly>()) cam.enabled = false;
                var hud = Object.FindFirstObjectByType<GolfArcade.UI.Hud>();
                d.stick = hud && hud.Controller != null ? hud.Controller.Stick : null;
                d.setStick = typeof(GolfArcade.UI.Joystick).GetProperty("Value")?.GetSetMethod(true);
                log.Raw($"load {Now - loadStart:F1} s, screen {Screen.width}x{Screen.height}, stick {(d.stick ? "found" : "MISSING")}, network {network}, latency guest->host {wire.Latency * 1000:F0} ms");
                Msg(bridge, id, "resume");
                SportsMultiplayer net = network ? SportsMultiplayer.Instance : null;
                if (network) { Assert.IsTrue(net, "multiplayer"); net.Receive(Packet(c, "run")); }
                rec = new Recorder(dir, name); log.Clock = () => rec.T;
                double end = Now + double.Parse(Env(network ? "MP_GOLF_SECONDS" : "MP_SOLO_SECONDS", network ? "540" : "240"), Inv);
                string lastState = "", lastNetPhase = "", turnKey = null; int lastHole = -1; long lastShot = -1; int stills = 0; string stillName = null; double stillAt = -1;
                var seen = new HashSet<string>(); var partyStart = new Dictionary<int, Vector3>(); long guestDoneFor = -1; double guestResultAt = -1;
                var turn = new Stack<IEnumerator>(); var pausedField = typeof(NativeSportsSession).GetField("paused", BindingFlags.NonPublic | BindingFlags.Instance); var heartbeat = typeof(NativeSportsSession).GetField("lastSample", BindingFlags.NonPublic | BindingFlags.Instance); int b1Hole = -1; double resultSince = -1; int emoteNext = 0; double lastEmote = -10; double holeDoneSince = -1;
                bool continued = false; int hostTurns = 0;

                while (Now < end)
                {
                    yield return new WaitForEndOfFrame();
                    var s = network ? net.GolfState : null;
                    string state = game.NativeState;
                    rec.Frame($"{state}{(s != null ? $" {s.phase} hole{s.hole} turn P{s.turn + 1}" : "")}");
                    if (stillName != null && Now >= stillAt) { rec.Still(stillName); stillName = null; }
                    wire.Pump();
                    void Still(string key, double delay = 0) { if (stillName == null && seen.Add(key)) { stillName = $"g{stills++:00}_{key}"; stillAt = Now + delay; } }

                    if (state != lastState) { log.Add($"view state {lastState} -> {state}"); lastState = state; }
                    if (s != null && s.phase != lastNetPhase) { log.Add($"host phase {lastNetPhase} -> {s.phase}  hole {s.hole + 1} turn P{s.turn + 1}"); lastNetPhase = s.phase; }
                    if (state == "Intro") Still("hole" + (game.CurrentHole?.PlayNumber ?? 0) + "_intro", 1.2);
                    if (state == "Loading") Still("loading_cover", .6);
                    if (state == "Flight") Still("flight_" + (game.ClubInHand == GolfClub.Putter ? "putt" : "full"), .9);
                    if (state == "Replay") Still("replay", 2.0);
                    if (state == "Result") Still("result_" + (s != null ? "P" + (s.shot?.seat + 1) : "solo") + (game.ClubInHand == GolfClub.Putter ? "_putt" : ""), .5);
                    if (state == "HoleDone") Still("hole_done", 1.0);
                    if (state == "RoundDone") Still("round_done", 1.0);

                    if (network)
                    {
                        if (s == null) continue;
                        if (s.phase == "aim" && game.Current == GolfGame.State.Aim) PartySample(game, s, d, partyStart);
                        else partyStart.Clear();
                        if (s.hole != lastHole) { lastHole = s.hole; log.Add($"HOLE {s.hole + 1} (wind {s.windSpeed:F0} mph)"); }
                        // the guest's phone builds each hole and says so
                        if ((s.phase == "loading" || s.phase == "aim" || s.phase == "intro") && b1Hole != s.hole && !s.golfers[1].dnf)
                        { b1Hole = s.hole; wire.Input("b", new NetworkInput { action = "golfHoleReady", value = s.hole, actorSeat = 1 }); }
                        // the guest's phone ends its own shot's result as single player does (3 s, or its emote played out)
                        if (s.shot != null && s.shot.seat == 1 && s.phase == "result" && guestDoneFor != s.shot.id)
                        {
                            if (guestResultAt < 0) guestResultAt = Now;
                            if (Now - guestResultAt >= 3.2) { guestDoneFor = s.shot.id; guestResultAt = -1; wire.Input("b", new NetworkInput { action = "golfResultDone", value = s.shot.id, actorSeat = 1 }); log.Add("P2 phone: result done"); }
                        }
                        if (s.shotID != lastShot && s.shot != null && s.shot.id == s.shotID)
                        {
                            lastShot = s.shotID; var sh = s.shot;
                            log.Add($"SHOT #{sh.id} P{sh.seat + 1} {((GolfClub)sh.club).DisplayName()} power {sh.impact?.power:F2} heading {sh.heading:F1}  carry {sh.carry:F0} roll {sh.roll:F0} -> {sh.lie}{(sh.holed ? " HOLED" : "")}{(sh.penalty > 0 ? " +" + sh.penalty : "")}  rest {new CoursePoint(sh.restX, sh.restD).DistanceTo(game.CurrentHole.Pin):F1} yd from pin");
                        }
                        // the other player reacts while someone else plays (and in the result)
                        if ((s.phase == "result" || s.phase == "flight") && Now - lastEmote > 4 && d.R() < .01f)
                        {
                            int seat = s.phase == "result" ? s.shot?.seat ?? 0 : 1 - s.turn; seat = Mathf.Clamp(seat, 0, 1);
                            int slot = emoteNext++ % 3; lastEmote = Now;
                            string en = TennisEmotes.Name(c.participants[seat].loadout.emotes[slot]);
                            if (seat == 0) Msg(bridge, id, "golfEmote", slot, 0); else wire.Input("b", new NetworkInput { action = "emote", value = slot, actorSeat = 1 });
                            overlay.Say(seat, "emote: " + en); log.Add($"P{seat + 1} emote {en} (during {s.phase})");
                        }
                        if (s.phase == "aim" && !s.paused && game.Current == GolfGame.State.Aim)
                        {
                            var up = s.golfers.First(p => p.seat == s.turn);
                            string key = $"{s.hole}.{s.turn}.{up.strokes}";
                            if (key != turnKey) { turnKey = key; turn.Clear(); turn.Push(s.turn == 0 ? HostTurn(d, s, hostTurns++) : GuestTurn(d, s)); log.Add($"turn P{s.turn + 1} stroke {up.strokes + 1} ({(game.ClubInHand).DisplayName()}, {game.BallAt.DistanceTo(game.CurrentHole.Pin):F0} yd to pin)"); Still($"aim_P{s.turn + 1}", 1.6); }
                        }
                        Step(turn);
                        if (s.complete) { log.Add("ROUND COMPLETE"); for (int i = 0; i < 200; i++) { yield return new WaitForEndOfFrame(); rec.Frame("complete"); } break; }
                    }
                    else
                    {
                        heartbeat?.SetValue(bridge, Now);
                        if (pausedField != null && (bool)pausedField.GetValue(bridge)) Msg(bridge, id, "resume");
                        if (game.Current == GolfGame.State.Aim && !game.NativeShotReady) Msg(bridge, id, "touch");
                        if (game.Current == GolfGame.State.Aim && game.NativeShotReady)
                        {
                            string key = $"{game.CurrentHole?.Number}.{game.BallAt.X:F2}.{game.BallAt.D:F2}";
                            if (key != turnKey) { turnKey = key; turn.Clear(); turn.Push(HostTurn(d, null, hostTurns++)); log.Add($"turn solo ({game.ClubInHand.DisplayName()}, {game.BallAt.DistanceTo(game.CurrentHole.Pin):F0} yd to pin)"); Still("aim_solo", 1.6); }
                        }
                        Step(turn);
                        if (game.LastShot != null && game.Current == GolfGame.State.Result) { if (resultSince < 0) { resultSince = Now; var sh = game.LastShot; log.Add($"SHOT {game.ClubInHand.DisplayName()} carry {sh.Carry:F0} -> {sh.Lie}{(sh.IsHoled ? " HOLED" : "")} rest {sh.Rest.DistanceTo(game.CurrentHole.Pin):F1} yd from pin"); } }
                        else resultSince = -1;
                        if (game.Current == GolfGame.State.Result && resultSince > 0 && Now - resultSince > .8 && Now - lastEmote > 6 && d.R() < .02f)
                        { int slot = emoteNext++ % 3; lastEmote = Now; Msg(bridge, id, "golfEmote", slot); overlay.Say(0, "emote: " + TennisEmotes.Name(c.participants[0].loadout.emotes[slot])); }
                        if (state == "HoleDone") { if (holeDoneSince < 0) holeDoneSince = Now; if (!continued && Now - holeDoneSince > 6) { continued = true; Msg(bridge, id, "golfContinue"); log.Add("golfContinue (next hole)"); } }
                        else holeDoneSince = -1;
                        if (continued && game.CurrentHole != null && game.CurrentHole.PlayNumber >= 2 && state == "Aim" && Now - holeDoneSince > 25) break;
                    }
                }
                // ---- summary
                log.Raw("");
                log.Raw("AIM TRIALS (heading degrees; error = final - wanted)");
                log.Raw("  who   control                         start    wanted   final   error  first-jump overshoot corr  secs");
                foreach (var t in d.trials)
                    log.Raw($"  {t.who,-5} {t.control,-30} {t.start,7:F1} {t.target,8:F1} {t.final,7:F1} {Wrap(t.final - t.target),7:F2} {t.firstJump,9:F1} {t.overshoot,9:F1} {t.corrections,4} {t.seconds,5:F1}");
                log.Raw($"G1 waiting golfers: max move during aiming {d.partyMaxMove:F3} m over {d.partySamples} samples");
                if (d.tvTrace.Count > 2)
                {
                    // the TV's lag: the shift that best lines the TV heading up with the phone's; the steps: the biggest frame-to-frame TV jump
                    double bestLag = 0, bestErr = double.MaxValue, step = 0;
                    for (int i = 1; i < d.tvTrace.Count; i++) step = Math.Max(step, Math.Abs(Wrap(d.tvTrace[i].tv - d.tvTrace[i - 1].tv)));
                    for (double lag = 0; lag <= .4; lag += .01)
                    {
                        double err = 0; int n = 0;
                        foreach (var p in d.tvTrace)
                        {
                            double when = p.t - lag; var q = d.tvTrace.OrderBy(x => Math.Abs(x.t - when)).First();
                            if (Math.Abs(q.t - when) > .03) continue; err += Math.Abs(Wrap(p.tv - q.phone)); n++;
                        }
                        if (n > 5 && err / n < bestErr) { bestErr = err / n; bestLag = lag; }
                    }
                    log.Raw($"G2 TV aim: lag {bestLag * 1000:F0} ms (mean error then {bestErr:F2} deg), biggest one-frame step {step:F2} deg, {d.tvTrace.Count} samples");
                }
                log.Raw($"frames {rec.AllFrames}, mean {rec.FrameSum / Math.Max(1, rec.AllFrames) * 1000:F1} ms, worst {rec.WorstFrame * 1000:F0} ms, over 33 ms: {rec.SlowFrames}");
                log.Raw($"unity log: {log.Errors} errors, {log.Exceptions} exceptions, {log.Warnings} warnings");
                log.Raw("hitches (>150 ms):"); foreach (var h in rec.Hitches) log.Raw("  " + h);
                if (probe) { log.Raw("HITCH PROBE (frames over 250 ms: the markers over 30 ms)"); foreach (var h in probe.Report) log.Raw("  " + h); }
            }
            finally
            {
                rec?.Close();
                try { Msg(bridge, id, "end"); } catch { }
                Application.logMessageReceived -= log.Unity; log.Save();
                Object.DestroyImmediate(root); Clean();
            }
        }

        static IEnumerator Wait(double seconds) { double until = Now + seconds; while (Now < until) yield return null; }
        /// One frame of a coroutine that yields other coroutines (driven by hand, not by Unity). False when it has finished.
        static bool Step(Stack<IEnumerator> stack)
        {
            while (stack.Count > 0)
            {
                var top = stack.Peek();
                if (!top.MoveNext()) { stack.Pop(); continue; }
                if (top.Current is IEnumerator inner) { stack.Push(inner); continue; }
                return true;
            }
            return false;
        }

        /// The player with the TV's phone (or the only player): Unity's stick on its sheet, then the SwiftUI pad (absolute), then the
        /// arrows, the club, the look up the hole, the backswing on the TV, and a touch swing. Alternates the aiming control by turn.
        static IEnumerator HostTurn(GolfDriver d, NetworkGolfState s, int turnIndex)
        {
            var game = d.game; var hole = game.CurrentHole; string who = "P1";
            yield return Wait(.9);
            if (game.Current != GolfGame.State.Aim) yield break;
            bool putt = game.ClubInHand == GolfClub.Putter;
            var origin = game.BallAt; var target = putt ? hole.Pin : hole.RecommendedTarget(origin);
            double ideal = origin.HeadingTo(target);
            // a deliberate line a little off the default one, as a player shaping a shot would
            double want = ideal + (d.R() - .5) * (putt ? 3 : 10);
            // a big look around first (gate G1): the stick hard left, hard right, back — about ±30 degrees
            if (!putt && turnIndex % 2 == 0 && d.stick)
            {
                d.overlay.StickLabel = "sweep the aim"; d.overlay.Say(0, "stick: sweep ±30°");
                foreach (var (x, secs) in new[] { (-1f, .65), (1f, 1.3), (-1f, .65) }) { d.SetStick(new Vector2(x, 0)); yield return Wait(secs); }
                d.SetStick(Vector2.zero); yield return Wait(.3);
            }
            int style = d.trials.Count(t => t.who == who) % 3;
            if (style == 0)
            {
                // Unity sheet's stick: push toward the line, let go when the picture says it is there (0.2 s reaction), correct
                var tr = new AimTrial { who = who, control = putt ? "sheet stick (putt, 1/5 rate)" : "sheet stick (60% push)", start = game.AimHeading, target = want };
                double t0 = Now; var seen = new Queue<(double t, double h)>();
                for (int pass = 0; pass < 4; pass++)
                {
                    double err = Wrap(want - game.AimHeading); if (Math.Abs(err) < .6) break;
                    if (pass > 0) tr.corrections++;
                    float push = pass == 0 ? .6f : .45f; var v = new Vector2(Math.Sign(err) * push, 0);
                    d.SetStick(v); d.overlay.StickLabel = $"stick {(err > 0 ? "right" : "left")} {push * 100:F0}%"; d.overlay.Say(0, d.overlay.StickLabel);
                    double hold = Now;
                    while (Now - hold < 6)
                    {
                        seen.Enqueue((Now, game.AimHeading)); while (seen.Count > 1 && Now - seen.Peek().t > .2) seen.Dequeue();
                        double perceived = seen.Peek().h;   // what the player saw 0.2 s ago
                        if (Math.Sign(Wrap(want - perceived)) != Math.Sign(err) || Math.Abs(Wrap(want - perceived)) < .4) break;
                        yield return null;
                    }
                    d.SetStick(Vector2.zero);
                    yield return Wait(.25);
                    tr.overshoot = Math.Max(tr.overshoot, Math.Sign(err) * Wrap(game.AimHeading - want));
                }
                tr.final = game.AimHeading; tr.seconds = Now - t0; d.trials.Add(tr);
            }
            else if (style == 1)
            {
                // SwiftUI pad (touch controls): absolute direction on the phone's map; the thumb lands off centre, slides to the line, lifts
                var tr = new AimTrial { who = who, control = "phone pad (absolute dir)", start = game.AimHeading, target = want };
                double t0 = Now;
                var aimDir = PadFromHeading(hole, want);
                float landAngle = (float)(d.R() * Math.PI * 2); var land = new Vector2(Mathf.Cos(landAngle), Mathf.Sin(landAngle)) * (11 + d.R() * 5);
                var goal = aimDir * 36 + new Vector2((d.R() - .5f) * 4, (d.R() - .5f) * 4);
                var slip = new Vector2((d.R() - .5f) * 7, (d.R() - .5f) * 7);
                int steps = 14;
                for (int i = 0; i <= steps; i++)
                {
                    var at = Vector2.Lerp(land, goal, i / (float)steps);
                    var n = at.normalized; d.overlay.Stick = n * Mathf.Min(1, at.magnitude / 48); d.overlay.StickLabel = "phone pad";
                    Msg(d.bridge, d.session, "golfAimDirection", n.x, n.y);
                    if (i == 0) { yield return null; tr.firstJump = Wrap(game.AimHeading - tr.start); d.overlay.Say(0, $"pad touch: aim jumped {tr.firstJump:+0.0;-0.0}°"); }
                    yield return Wait(1 / 30.0);
                }
                yield return Wait(.5);
                var lift = (goal + slip).normalized; Msg(d.bridge, d.session, "golfAimDirection", lift.x, lift.y);
                d.overlay.Stick = null; yield return null;
                tr.final = game.AimHeading; tr.seconds = Now - t0; d.trials.Add(tr);
                d.overlay.Say(0, $"pad lift: off by {Wrap(tr.final - want):+0.0;-0.0}°");
            }
            else
            {
                // arrows: 1.5° a tap
                var tr = new AimTrial { who = who, control = "arrow taps (1.5°/tap)", start = game.AimHeading, target = want };
                double t0 = Now;
                for (int i = 0; i < 12; i++)
                {
                    double err = Wrap(want - game.AimHeading); if (Math.Abs(err) < .8) break;
                    Msg(d.bridge, d.session, "aim", Math.Sign(err)); d.overlay.Say(0, err > 0 ? "aim ▶ tap" : "◀ aim tap"); tr.corrections++;
                    yield return Wait(.28);
                }
                tr.final = game.AimHeading; tr.seconds = Now - t0; d.trials.Add(tr);
            }
            yield return Wait(.4);
            if (game.Current != GolfGame.State.Aim) yield break;
            // club: one down and back (not on the green)
            if (!putt && turnIndex % 2 == 0)
            {
                Msg(d.bridge, d.session, "club", 1); d.overlay.Say(0, "club ▼ → " + game.ClubInHand.DisplayName()); yield return Wait(.8);
                Msg(d.bridge, d.session, "club", -1); d.overlay.Say(0, "club ▲ → " + game.ClubInHand.DisplayName()); yield return Wait(.6);
            }
            // look up the hole and back down (the sheet stick's up/down)
            if (!putt)
            {
                d.overlay.Say(0, "stick up: look up the hole"); game.LookHeld = 1; d.overlay.Stick = new Vector2(0, 1); yield return Wait(1.3);
                game.LookHeld = 0; d.overlay.Stick = null; yield return Wait(.8);
            }
            if (game.Current != GolfGame.State.Aim) yield break;
            // backswing on the TV, then the touch swing
            var wind = s != null ? new Wind(s.windSpeed, s.windDirection) : game.Wind;
            float power = PowerFor(hole, game.ClubInHand, game.AimHeading, game.BallAt, target, wind);
            power = Mathf.Clamp(power * (1 + (d.R() - .5f) * (putt ? .14f : .08f)), .03f, 1);
            for (float l = 0; l <= power; l += .06f) { Msg(d.bridge, d.session, "golfLoad", l); d.overlay.Say(0, $"power slider {l * 100:F0}%"); yield return Wait(1 / 20.0); }
            yield return Wait(.25);
            if (game.Current != GolfGame.State.Aim) yield break;
            d.overlay.Say(0, $"SWING {power * 100:F0}%");
            game.NativeSwing(power);
            d.log.Add($"P1 swing {game.ClubInHand.DisplayName()} power {power:F2} heading {game.AimHeading:F1} (wanted {want:F1})");
        }

        /// The guest's phone (no TV of its own): its own Unity aims with the sheet's stick or the arrows and tells the host the line at
        /// 15 Hz (golfAimHeading), the view and the backswing; the swing carries its club, line and strike. All through the wire.
        static IEnumerator GuestTurn(GolfDriver d, NetworkGolfState s0)
        {
            var game = d.game; var net = SportsMultiplayer.Instance; var hole = game.CurrentHole; const string who = "P2";
            yield return Wait(1.0);
            var s = net.GolfState; if (s == null || s.phase != "aim" || s.turn != 1) yield break;
            var me = s.golfers.First(p => p.seat == 1);
            var origin = new CoursePoint(me.x, me.d); bool putt = (GolfClub)me.club == GolfClub.Putter;
            var target = putt ? hole.Pin : hole.RecommendedTarget(origin);
            double heading = me.heading; int club = me.club;
            double want = origin.HeadingTo(target) + (d.R() - .5) * (putt ? 3 : 10);
            double sentHeading = double.NaN, nextSend = 0, lastSendAt = Now;
            void SendHeading(bool force)
            {
                if (!force && (Now < nextSend || (!double.IsNaN(sentHeading) && Math.Abs(Mathf.DeltaAngle((float)sentHeading, (float)heading)) <= .02f))) return;
                float rate = force || double.IsNaN(sentHeading) ? 0 : Mathf.DeltaAngle((float)sentHeading, (float)heading) / Mathf.Max(.02f, (float)(Now - lastSendAt));
                nextSend = Now + 1 / 30.0; sentHeading = heading; lastSendAt = Now;
                d.wire.Input("b", new NetworkInput { action = "golfAimHeading", value = (float)((heading % 360 + 360) % 360), value2 = Mathf.Clamp(rate, -120, 120), actorSeat = 1 });
            }
            int style = d.trials.Count(t => t.who == who) % 2;
            var tr = new AimTrial { who = who, control = style == 0 ? (putt ? "guest stick (putt) via TV" : "guest stick 60% via TV") : "guest arrows via TV", start = heading, target = want };
            double t0 = Now;
            if (style == 0)
            {
                // the guest watches the TV (the host's picture: their line arrives late, 15 times a second) and reacts 0.2 s later
                var tv = new Queue<(double t, double h)>();
                double rate = 1.5 * 28 * (putt ? .2 : 1);
                for (int pass = 0; pass < 4; pass++)
                {
                    double err = Wrap(want - game.AimHeading); if (Math.Abs(err) < .6) break;
                    if (pass > 0) tr.corrections++;
                    float push = pass == 0 ? .6f : .45f; float dz = (push - .2f) / .8f;
                    d.overlay.Say(1, $"stick {(err > 0 ? "right" : "left")} {push * 100:F0}% (own phone)");
                    double hold = Now, last = Now; bool first = true;
                    while (Now - hold < 6)
                    {
                        double dt = Now - last; last = Now; heading += Math.Sign(err) * dz * rate * dt; SendHeading(false);
                        d.tvTrace.Add((Now, heading, game.AimHeading));
                        tv.Enqueue((Now, game.AimHeading)); while (tv.Count > 1 && Now - tv.Peek().t > .2) tv.Dequeue();
                        if (first) { first = false; }
                        double perceived = tv.Peek().h;
                        if (Math.Sign(Wrap(want - perceived)) != Math.Sign(err) || Math.Abs(Wrap(want - perceived)) < .4) break;
                        yield return null;
                    }
                    SendHeading(true);
                    yield return Wait(.35);
                    tr.overshoot = Math.Max(tr.overshoot, Math.Sign(err) * Wrap(game.AimHeading - want));
                }
            }
            else
            {
                for (int i = 0; i < 12; i++)
                {
                    double err = Wrap(want - game.AimHeading); if (Math.Abs(err) < .8) break;
                    heading += Math.Sign(err) * 1.5 * (putt ? .33 : 1); SendHeading(true); tr.corrections++;
                    d.overlay.Say(1, err > 0 ? "aim ▶ tap" : "◀ aim tap");
                    yield return Wait(.32);
                }
            }
            yield return Wait(.3);
            tr.final = game.AimHeading; tr.seconds = Now - t0; d.trials.Add(tr);
            d.log.Add($"P2 aim: phone heading {heading:F2}, TV heading {game.AimHeading:F2}");
            s = net.GolfState; if (s == null || s.phase != "aim" || s.turn != 1) yield break;
            if (!putt && d.trials.Count(t => t.who == who) % 2 == 1)
            {
                int down = (int)GolfClubs.All[(Array.IndexOf(GolfClubs.All, (GolfClub)club) + 1) % GolfClubs.All.Length];
                d.wire.Input("b", new NetworkInput { action = "golfClub", value = down, actorSeat = 1 }); d.overlay.Say(1, "club ▼ → " + ((GolfClub)down).DisplayName()); yield return Wait(.9);
                d.wire.Input("b", new NetworkInput { action = "golfClub", value = club, actorSeat = 1 }); d.overlay.Say(1, "club ▲ → " + ((GolfClub)club).DisplayName()); yield return Wait(.7);
            }
            if (!putt)
            {
                d.overlay.Say(1, "stick up: look up the hole");
                for (float l = 0; l <= 1.01f; l += .1f) { d.wire.Input("b", new NetworkInput { action = "golfLook", value = l, actorSeat = 1 }); yield return Wait(1 / 15.0); }
                yield return Wait(.8);
                for (float l = 1; l >= -.01f; l -= .1f) { d.wire.Input("b", new NetworkInput { action = "golfLook", value = Mathf.Max(0, l), actorSeat = 1 }); yield return Wait(1 / 15.0); }
                yield return Wait(.5);
            }
            s = net.GolfState; if (s == null || s.phase != "aim" || s.turn != 1) yield break;
            float power = PowerFor(hole, (GolfClub)club, heading, origin, target, new Wind(s.windSpeed, s.windDirection));
            power = Mathf.Clamp(power * (1 + (d.R() - .5f) * (putt ? .14f : .08f)), .03f, 1);
            for (float l = 0; l <= power; l += .05f) { d.wire.Input("b", new NetworkInput { action = "golfLoad", value = l, actorSeat = 1 }); d.overlay.Say(1, $"backswing {l * 100:F0}%"); yield return Wait(1 / 20.0); }
            yield return Wait(.2);
            d.wire.Input("b", new NetworkInput { action = "golfLoad", value = 0, actorSeat = 1 });
            var impact = NetworkImpact.From(Impact(power));
            d.wire.Input("b", new NetworkInput { action = "swing", power = power, impact = impact, shotClub = club, heading = (heading % 360 + 360) % 360, actorSeat = 1 });
            d.overlay.Say(1, $"SWING {power * 100:F0}%");
            d.log.Add($"P2 swing {((GolfClub)club).DisplayName()} power {power:F2} heading {heading:F1} (wanted {want:F1})");
        }
    }
}
