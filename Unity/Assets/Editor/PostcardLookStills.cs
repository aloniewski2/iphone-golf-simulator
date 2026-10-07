using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using GolfArcade.Course;
using GolfArcade.Game;

namespace GolfArcade.EditorTools
{
    /// Game-view stills of the postcard holes (8 Needle, 9 Split, 10 Crater) for the POSTCARD_LOOK proof: Play mode, the real GolfGame / HoleView /
    /// CameraRig / GolfAtmosphere, a 900x1600 RenderTexture (the phone), the Canvas and the golfer hidden. PostcardCameraStills (JPG address
    /// stills of the older pass) stays as it was.
    ///
    ///   [GOLF_STILLS_OUT=<dir>] [GOLF_STILLS_CFG=<shots.json>] [GOLF_STILLS_HOLES=8,9,10] [GOLF_STILLS_BOARD=1] [GOLF_STILLS_PIPELINE=<URP asset name>]
    ///     Unity -batchmode -projectPath Unity -executeMethod GolfArcade.EditorTools.PostcardLookStills.Run -logFile <abs log>
    ///   (always through work/postcard-look/heavy.sh)
    ///
    /// Output (default dir ArtDir/screenshots/golf_postcards/look/): <name>.png per shot and stills_stats.json (pixel statistics per shot).
    ///
    /// Shots: with no GOLF_STILLS_CFG every selected hole gets "holeNN_tee" and "holeNN_approach" (CameraRig.FrameAddress, the game's own
    /// address framing: 4.5 yd behind the ball, 2.4 yd up, looking 1.5 yd ahead). GOLF_STILLS_CFG names a JSON file that REPLACES the defaults:
    ///   { "shots": [
    ///       { "name": "hole08_tee",  "hole": 8, "kind": "address", "ball": [0, 0], "aim": [19, 178], "putting": false },
    ///       { "name": "hole08_arch", "hole": 8, "kind": "landmark", "pos": [x, y, z], "look": [x, y, z], "fov": 50, "ball": [x, z] } ] }
    ///   "address": ball = [course x, course d] (the ball is put there, the camera frames it toward aim = [x, d] of the target); "putting" optional.
    ///   "landmark" (the default kind when "pos" is present): pos / look are Unity WORLD YARDS (x right, y up with the play surface at 6.56,
    ///   z down the hole), fov in degrees (default 60), optional "ball": [course x, course d] puts the game ball there (resting on the ground
    ///   under it, as in play) so a shot can prove the ball is not hidden. The camera is the game's camera; near .3, far 900.
    /// GOLF_STILLS_PIPELINE=<asset name, e.g. HeroBaseStudioURP or TennisURP> renders the stills under that URP asset (set IN MEMORY on the current quality level, restored at the
    /// end; ProjectSettings is never written). v2 repair round 1: the stills used to render under whatever QualitySettings pointed at (TennisURP today), so there was no
    /// HeroBaseStudioURP Game-view still. stills_stats.json "pipeline" names the asset that was really used.
    /// GOLF_STILLS_VARIANTS (v2 repair round 3, experiments only, never proof shots): extra renders of every shot with IN-MEMORY tweaks, written as <name>__<variant>.png (no stats):
    ///   "name:key=v[,v..];key=v|name2:..."   keys: fog=0|1, fogcolor=r,g,b (sRGB 0..1), fogrange=start,end, lights=<x> (LAVA_LIGHT intensity multiplier), sun=r,g,b (key colour),
    ///   ambient=<x> (multiplies the trilight colours), ambeq / ambsky / ambgnd=r,g,b (replace one trilight colour), basalt_tint=r,g,b, basalt_emis=r,g,b (LINEAR multiplier), basalt_smooth=<x>, nosmoke=1 (DRESS_SMOKE cards off), smoke=gain,power,softfade,lit,self,fogshare (LK_SMOKE material), hide=<renderer name prefix>.
    ///   Everything is put back after each render. The Game-view proof shots are always rendered WITHOUT variants.
    /// GOLF_STILLS_SWAY=<mph>,<deg>,<seconds> (v2 2026-10-05, area U: the plant sway): a FIXED wind (GolfWindSway.ForcedWind) and a FROZEN clock (GolfWindSway.FreezeTime) for every shot, so two runs
    ///   give pixel-identical plants (the sea is the tennis shader's own engine-clock swell and moves from run to run: see the note below).
    /// GOLF_STILLS_SWAY_PAIR=<dt seconds> (needs GOLF_STILLS_SWAY): after the main frame of every shot render the SAME camera again dt seconds later (plants only change), the frame twice with the
    ///   PLANT_* renderers off (everything that is not a plant must be pixel-identical between the two clocks) and once more at the first clock (repeat = deterministic): writes <name>__sway_t1.png and
    ///   <name>__sway_diff.png and a "sway" block in stills_stats.json (see SwayStats). All of it happens inside ONE player frame, so TennisWater's own _Time does not move between the frames.
    /// GOLF_STILLS_REALTIME_PAIR=<dt seconds> (v2 2026-10-05, area U): the REAL-TIME half of "nothing but plants moves": after each shot the tool lets dt seconds of wall-clock time (and the player loop) pass, then renders the
    ///   same camera again; with every PLANT_* renderer off the two frames must be identical outside the sea (WATER_OCEAN / WATER_SHELF = TennisWater, which reads the engine clock): ground, rocks, lava, surf, fall,
    ///   smoke and sky do not move by themselves. Written to sway_realtime.json (one record per shot).
    /// GOLF_STILLS_SWAY_TESTCOLORS=1: PLANT_* meshes WITHOUT sway colours get height-based synthetic colours IN MEMORY (R = tip weight x height share, B = 0): proves the shader + pipeline on a hole whose installed
    ///   FBX predates the sway colours. Experiments only: the frame pair of such a run says "test_colours": true and never counts as proof of the library colours.
    ///   (Measured, v2/sway: Time.timeScale = 0 does NOT make the sea reproducible between two Unity runs: both froze at Time.time 0.044 and TennisWater still differed in 12 of 18 frames. The sea is therefore
    ///   written as a mask, <name>__sway_water.png, and the cross-run comparison leaves it out; Crater has no sea and is bit-identical.)
    /// GOLF_STILLS_BOARD=1 lays the real LK_ materials on test strips in front of the tee (rough | fairway | green or sand, a lava sheet on
    /// Crater): the proof that the pipeline works while a hole FBX is still the flat baseline. Never use it for proof shots.
    ///
    /// stills_stats.json: { "tool", "unity", "pipeline", "qualityLevel", "size": [900, 1600], "shots": [ {
    ///     "name", "hole", "file", "camera": { "pos", "look", "fov" },
    ///     "mean_rgb": [r, g, b] (0..255, whole frame), "luminance" (0..255, whole frame),
    ///     "central_lower_third": { "region": [x0, y0, x1, y1] (px, y up from the bottom), "mean_rgb", "mean_hsv": [h deg, s, v], "luminance" },
    ///     "hue_bands": { "red_0_30": f, "orange_30_60": f, "yellow_60_90": f, ... "magenta_330_360": f, "grey": f, "dark": f },   // fraction of
    ///                    all pixels; "grey" = S < .15 (and not dark), "dark" = V < .15; 12 hue bands of 30 degrees for the rest
    ///     "smoke": { "cards": n (renderers named DRESS_SMOKE*), and when n > 0: "changed_px" (|delta luminance| > 1.5 between the frame and the SAME frame rendered with the
    ///                smoke cards switched off), "share_changed", "readable_px" (|delta| >= 10 levels: a plume a phone user can pick out), "share_readable" (of the whole frame),
    ///                "delta_lum_median" / "delta_lum_p95" / "delta_lum_p99" (over the changed pixels), "plume_mean_rgb" } }
    ///                v2 repair round 1: the reviewers found no readable plume in any Crater still while the old per-card gate passed on 0.44 % / 0.07 % of the frame;
    ///                postcard_look_verify.py SMOKE_VISIBLE_STILLS reads share_readable from here.
    /// } ] }
    [InitializeOnLoad]
    public static class PostcardLookStills
    {
        const string Flag = "PostcardLookStills";
        const int W = 900, H = 1600;
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [System.Serializable] sealed class Cfg { public ShotCfg[] shots; }
        [System.Serializable] sealed class ShotCfg
        {
            public string name; public int hole; public string kind;
            public float[] pos, look, ball, aim; public float fov; public bool putting;
        }

        sealed class Shot
        {
            public string Name; public int Hole; public bool Landmark;
            public Vector3 Pos, Look; public float Fov = 60;
            public bool HasBall; public float BallX, BallD; public float AimX, AimD; public bool Putting;
        }

        // defaults: hole number -> (ball x, ball d, aim x, aim d) for the tee and the approach (on-land addresses of the older stills)
        static readonly Dictionary<int, ((float, float, float, float) tee, (float, float, float, float) approach)> Defaults = new()
        {
            [8] = ((0f, 0f, 19f, 178f), (19f, 178f, -6f, 340f)),
            [9] = ((0f, 0f, 3f, 60f), (11.8f, 235f, 6f, 416f)),
            [10] = ((0f, 0f, 14.3f, 66.2f), (107.1f, 159.3f, 261f, 164.7f)),
        };

        static readonly List<Shot> queue = new();
        static int warm, shotIndex = -1, wait, startFrame, settleTicks, staleShots;
        static string outDir; static bool board;
        static RenderTexture rt; static Texture2D tex; static Camera cam;
        static GolfGame game; static CameraRig rig; static GameObject boardRoot;
        static int currentHole = -1;
        static readonly List<string> statsJson = new();
        const string SmokePrefix = "DRESS_SMOKE";
        const float ReadableDelta = 10f;       // luminance levels: a plume that moves a pixel by >= 10 (of 255) can be picked out on a phone
        static UnityEngine.Rendering.RenderPipelineAsset previousPipeline; static bool pipelineSwapped;
        // plant sway (area U)
        static bool swayOn, swayTestColours; static float swayMph, swayDeg, swayTime, swayPairDt;
        static readonly Dictionary<Mesh, Mesh> swayMeshes = new();
        static string hookJson = "";
        // real-time pair (area U)
        static float rtDt, rtDue, rtT0; static bool rtPending; static Shot rtShot; static Color32[] rtA, rtADry;
        static readonly List<string> realtimeJson = new();

        static PostcardLookStills() { EditorApplication.update += Tick; }

        public static void Run()
        {
            outDir = System.Environment.GetEnvironmentVariable("GOLF_STILLS_OUT");
            if (string.IsNullOrEmpty(outDir)) outDir = Path.GetFullPath("../ArtDir/screenshots/golf_postcards/look");
            outDir = Path.GetFullPath(outDir);
            Directory.CreateDirectory(outDir);
            SessionState.SetString(Flag + "out", outDir);
            SessionState.SetString(Flag + "cfg", System.Environment.GetEnvironmentVariable("GOLF_STILLS_CFG") ?? "");
            SessionState.SetString(Flag + "holes", System.Environment.GetEnvironmentVariable("GOLF_STILLS_HOLES") ?? "8,9,10");
            SessionState.SetBool(Flag + "board", System.Environment.GetEnvironmentVariable("GOLF_STILLS_BOARD") == "1");
            SessionState.SetString(Flag + "pipeline", System.Environment.GetEnvironmentVariable("GOLF_STILLS_PIPELINE") ?? "");
            EditorSceneManager.OpenScene("Assets/Scenes/Golf.unity");
            SessionState.SetBool(Flag, true);
            EditorApplication.isPlaying = true;
        }

        static bool BuildQueue(out string error)
        {
            error = null;
            queue.Clear();
            var holes = new HashSet<int>();
            foreach (var t in SessionState.GetString(Flag + "holes", "8,9,10").Split(',')) if (int.TryParse(t.Trim(), out var n)) holes.Add(n);
            string cfgPath = SessionState.GetString(Flag + "cfg", "");
            if (string.IsNullOrEmpty(cfgPath))
            {
                foreach (var kv in Defaults.OrderBy(k => k.Key))
                {
                    if (!holes.Contains(kv.Key)) continue;
                    var (tee, app) = kv.Value;
                    queue.Add(new Shot { Name = $"hole{kv.Key:00}_tee", Hole = kv.Key, HasBall = true, BallX = tee.Item1, BallD = tee.Item2, AimX = tee.Item3, AimD = tee.Item4 });
                    queue.Add(new Shot { Name = $"hole{kv.Key:00}_approach", Hole = kv.Key, HasBall = true, BallX = app.Item1, BallD = app.Item2, AimX = app.Item3, AimD = app.Item4 });
                }
                return true;
            }
            if (!File.Exists(cfgPath)) { error = "GOLF_STILLS_CFG not found: " + cfgPath; return false; }
            var cfg = JsonUtility.FromJson<Cfg>(File.ReadAllText(cfgPath));
            if (cfg == null || cfg.shots == null || cfg.shots.Length == 0) { error = "GOLF_STILLS_CFG has no shots"; return false; }
            foreach (var c in cfg.shots)
            {
                if (string.IsNullOrEmpty(c.name)) { error = "a shot has no name"; return false; }
                if (!GolfLook.IsPostcard(c.hole)) { error = $"shot '{c.name}': hole {c.hole} is not a postcard hole (8, 9, 10)"; return false; }
                bool landmark = c.kind == "landmark" || (string.IsNullOrEmpty(c.kind) && c.pos != null);
                var s = new Shot { Name = c.name, Hole = c.hole, Landmark = landmark, Fov = c.fov > 1 ? c.fov : 60, Putting = c.putting };
                if (landmark)
                {
                    if (c.pos == null || c.pos.Length < 3 || c.look == null || c.look.Length < 3) { error = $"shot '{c.name}': landmark needs pos [x,y,z] and look [x,y,z]"; return false; }
                    s.Pos = new Vector3(c.pos[0], c.pos[1], c.pos[2]); s.Look = new Vector3(c.look[0], c.look[1], c.look[2]);
                    if (c.ball != null && c.ball.Length >= 2) { s.HasBall = true; s.BallX = c.ball[0]; s.BallD = c.ball[1]; }
                }
                else
                {
                    if (c.ball == null || c.ball.Length < 2 || c.aim == null || c.aim.Length < 2) { error = $"shot '{c.name}': address needs ball [x,d] and aim [x,d]"; return false; }
                    s.HasBall = true; s.BallX = c.ball[0]; s.BallD = c.ball[1]; s.AimX = c.aim[0]; s.AimD = c.aim[1];
                }
                queue.Add(s);
            }
            var ordered = queue.Select((s, i) => (s, i)).OrderBy(t => t.s.Hole).ThenBy(t => t.i).Select(t => t.s).ToList();   // shots of one hole together, file order kept
            queue.Clear(); queue.AddRange(ordered);
            return true;
        }

        static void Tick()
        {
            if (!SessionState.GetBool(Flag, false) || !EditorApplication.isPlaying) return;
            if (shotIndex < 0)
            {
                if (!HoleView.Current || ++warm < 90) return;
                outDir = SessionState.GetString(Flag + "out", outDir);
                board = SessionState.GetBool(Flag + "board", false);
                if (!BuildQueue(out var error)) { Debug.LogError("[PostcardLookStills] " + error); Finish(1); return; }
                game = Object.FindFirstObjectByType<GolfGame>();
                rig = Object.FindFirstObjectByType<CameraRig>();
                cam = rig ? rig.GetComponentInChildren<Camera>() : Camera.main;
                foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) c.enabled = false;
                if (!UsePipeline(SessionState.GetString(Flag + "pipeline", ""), out error)) { Debug.LogError("[PostcardLookStills] " + error); Finish(1); return; }
                InitSway();
                rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
                tex = new Texture2D(W, H, TextureFormat.RGB24, false);
                var pipeline = QualitySettings.renderPipeline ? QualitySettings.renderPipeline : UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
                Debug.Log($"[PostcardLookStills] {queue.Count} shot(s) -> {outDir}; pipeline asset '{(pipeline ? pipeline.name : "none")}', quality level {QualitySettings.GetQualityLevel()} '{QualitySettings.names[QualitySettings.GetQualityLevel()]}'{(board ? "; TEST BOARD ON (not proof)" : "")}");
                shotIndex = 0; currentHole = -1; wait = 1; return;
            }
            if (rtPending) { if (Time.realtimeSinceStartup < rtDue) return; FinishRealtime(); }
            if (--wait > 0) return;
            if (shotIndex >= queue.Count) { Finish(staleShots > 0 ? 2 : 0); return; }
            var s = queue[shotIndex];
            if (s.Hole != currentHole) { LoadHole(s.Hole); return; }
            // review K 2026-10-04: EditorApplication.update can tick faster than the player loop in batch mode, so "30 ticks" did not guarantee that GolfGame.StartHole's
            // Destroy(previous HoleView) had run (the probe then rendered the previous model under the new one). Wait for real player-loop frames and a single HoleView.
            if (!Settled(out var why) && ++settleTicks < 3000) return;
            settleTicks = 0;
            Take(s);
            shotIndex++; wait = 3;
        }

        static void LoadHole(int number)
        {
            int index = System.Array.FindIndex(GolfArcade.Course.Course.Postcards().Holes, h => h.Number == number);
            game.enabled = true; rig.enabled = true;
            startFrame = Time.frameCount; settleTicks = 0;
            typeof(GolfGame).GetMethod("StartHole", Private).Invoke(game, new object[] { index });
            currentHole = number; wait = 30;
            CheckWindHook(number);
            if (swayTestColours) ApplySwayTestColours();
            if (boardRoot) Object.Destroy(boardRoot);
            boardRoot = board ? TestBoard(number) : null;
        }

        /// The previous hole's HoleView is really gone and a few real frames have passed since StartHole.
        static bool Settled(out string why)
        {
            int frames = Time.frameCount - startFrame, views = Object.FindObjectsByType<HoleView>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            why = $"{frames} player frame(s) since StartHole, {views} HoleView(s) alive";
            return frames >= 5 && views == 1;
        }

        /// GOLF_STILLS_PIPELINE: swap the current quality level's URP asset for the named one (in memory; RestorePipeline puts it back). Empty = keep whatever is active.
        static bool UsePipeline(string name, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(name)) return true;
            UnityEngine.Rendering.RenderPipelineAsset found = null;
            foreach (var guid in AssetDatabase.FindAssets(name.Trim() + " t:UniversalRenderPipelineAsset"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == name.Trim()) { found = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.RenderPipelineAsset>(path); break; }
            }
            if (!found) { error = "GOLF_STILLS_PIPELINE: no URP asset named '" + name + "'"; return false; }
            previousPipeline = QualitySettings.renderPipeline; pipelineSwapped = true;
            QualitySettings.renderPipeline = found;
            Debug.Log($"[PostcardLookStills] pipeline asset set in memory to '{found.name}' (was '{(previousPipeline ? previousPipeline.name : "none")}'; restored at the end, ProjectSettings is never written)");
            return true;
        }

        static void RestorePipeline()
        {
            if (!pipelineSwapped) return;
            QualitySettings.renderPipeline = previousPipeline; pipelineSwapped = false;
        }

        static void HideGameBits()
        {
            var golfer = (GolferView)typeof(GolfGame).GetField("golfer", Private).GetValue(game);
            if (golfer) golfer.SetVisible(false);
            var line = (LineRenderer)typeof(GolfGame).GetField("aimLine", Private).GetValue(game);
            if (line) line.enabled = false;
            var marker = (Transform)typeof(GolfGame).GetField("landingMarker", Private).GetValue(game);
            if (marker) marker.gameObject.SetActive(false);
        }

        static void Take(Shot s)
        {
            if (!Settled(out var why)) { staleShots++; Debug.LogError($"[PostcardLookStills] {s.Name}: NOT settled ({why}): the frame may show the previous hole too, the run will exit 2"); }
            HideGameBits();
            game.enabled = false; rig.enabled = false;
            var ballT = (Transform)typeof(GolfGame).GetField("ball", Private).GetValue(game);
            if (s.HasBall && ballT) { ballT.gameObject.SetActive(true); ballT.position = HoleView.ToWorld(new CoursePoint(s.BallX, s.BallD), 0.06); }
            cam.fieldOfView = s.Fov;
            if (s.Landmark)
            {
                rig.transform.position = s.Pos;
                rig.transform.rotation = Quaternion.LookRotation(s.Look - s.Pos, Vector3.up);
            }
            else
            {
                var ballAt = new CoursePoint(s.BallX, s.BallD);
                var ball = HoleView.ToWorld(ballAt, 0.06);
                var aim = HoleView.ToWorld(new CoursePoint(s.AimX, s.AimD), 0) - HoleView.ToWorld(ballAt, 0);
                aim.y = 0; aim.Normalize();
                rig.FrameAddress(ball, aim, s.Putting); rig.SnapNext(); rig.ApplyFrame();
            }
            if (swayOn) { GolfWindSway.FreezeTime = swayTime; GolfWindSway.Push(); }
            cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply(); RenderTexture.active = null; cam.targetTexture = null;
            File.WriteAllBytes(Path.Combine(outDir, s.Name + ".png"), tex.EncodeToPNG());
            var full = tex.GetPixels32();
            string swayJson = swayOn && swayPairDt > 0 ? SwayStats(s, full) : null;
            statsJson.Add(Stats(s, full, SmokeStats(full) + (hookJson.Length > 0 ? ",\n      " + hookJson : "") + (swayJson != null ? ",\n      " + swayJson : "")));
            Debug.Log($"[PostcardLookStills] hole {s.Hole} {s.Name}");
            if (rtDt > 0) BeginRealtime(s);
            RenderVariants(s);
            cam.fieldOfView = 60;
        }

        // ------------------------------------------------------------------ plant sway (area U)

        static void InitSway()
        {
            rtDt = 0; rtPending = false; realtimeJson.Clear();
            { var rts = System.Environment.GetEnvironmentVariable("GOLF_STILLS_REALTIME_PAIR"); if (!string.IsNullOrWhiteSpace(rts) && float.TryParse(rts, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var rd) && rd > 0) rtDt = rd; }
            swayOn = false; swayPairDt = 0; swayTestColours = System.Environment.GetEnvironmentVariable("GOLF_STILLS_SWAY_TESTCOLORS") == "1";
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            string spec = System.Environment.GetEnvironmentVariable("GOLF_STILLS_SWAY");
            if (!string.IsNullOrWhiteSpace(spec))
            {
                var f = Floats(spec);
                if (f.Length >= 3) { swayMph = f[0]; swayDeg = f[1]; swayTime = f[2]; swayOn = true; GolfWindSway.ForcedWind = new Wind(swayMph, swayDeg); GolfWindSway.FreezeTime = swayTime; GolfWindSway.Push(); }
                else Debug.LogError("[PostcardLookStills] GOLF_STILLS_SWAY needs mph,deg,seconds");
            }
            string pair = System.Environment.GetEnvironmentVariable("GOLF_STILLS_SWAY_PAIR");
            if (!string.IsNullOrWhiteSpace(pair) && float.TryParse(pair, System.Globalization.NumberStyles.Float, inv, out var dt) && dt > 0) swayPairDt = dt;
            if (swayPairDt > 0 && !swayOn) { Debug.LogError("[PostcardLookStills] GOLF_STILLS_SWAY_PAIR needs GOLF_STILLS_SWAY (a fixed wind and a frozen clock)"); swayPairDt = 0; }
            if (swayOn) Debug.Log($"[PostcardLookStills] plant sway: fixed wind {swayMph:F0} mph toward {swayDeg:F0}, clock frozen at {swayTime:F2} s{(swayPairDt > 0 ? $", frame pairs {swayPairDt:F2} s apart" : "")}{(swayTestColours ? ", SYNTHETIC test colours for plants without sway data (experiment)" : "")}");
        }

        /// RUNTIME.md "area U", SWAY_HOOK: GolfGame.StartHole handed the hole's wind to GolfWindSway (the hook), and the plants will sway to it (unless a test forces another wind).
        static void CheckWindHook(int number)
        {
            var w = game.Wind; var h = GolfWindSway.HookWind;
            bool same = GolfWindSway.HookSet && System.Math.Abs(w.SpeedMPH - h.SpeedMPH) < 1e-9 && System.Math.Abs(w.DirectionDegrees - h.DirectionDegrees) < 1e-9;
            Debug.Log($"[PostcardLookStills] wind hook hole {number}: GolfGame.Wind {w.SpeedMPH:F0} mph toward {w.DirectionDegrees:F0}; GolfWindSway hook wind {(GolfWindSway.HookSet ? $"{h.SpeedMPH:F0} mph toward {h.DirectionDegrees:F0}" : "NOT SET")}: {(same ? "SAME" : "DIFFERENT")}");
            hookJson = "\"wind_hook\": { \"game_mph\": " + F((float)w.SpeedMPH) + ", \"game_deg\": " + F((float)w.DirectionDegrees) + ", \"hook_set\": " + (GolfWindSway.HookSet ? "true" : "false") + ", \"same\": " + (same ? "true" : "false") + " }";
        }

        static readonly (string kind, string[] prefixes, float cap, float tip)[] SwayKinds =
        {
            ("tuft", new[] { "PLANT_TUFT", "PLANT_GRASS" }, .06f, 1f), ("flower", new[] { "PLANT_FLOWER", "PLANT_AGAVE" }, .05f, .80f), ("shrub", new[] { "PLANT_SHRUB", "PLANT_BUSH" }, .03f, .48f), ("tree", new[] { "PLANT_TREE", "PLANT_PINE" }, .03f, .30f), ("vine", new[] { "PLANT_VINE" }, .08f, 1f),
        };
        static (string kind, float cap, float tip) SwayKindOf(string name)
        {
            foreach (var k in SwayKinds) foreach (var p in k.prefixes) if (name.StartsWith(p)) return (k.kind, k.cap, k.tip);
            return ("other", .06f, 1f);
        }

        /// GOLF_STILLS_SWAY_TESTCOLORS: synthetic sway colours on the PLANT_* meshes that have none (copies, in memory): R = tip weight x the height share (vines: from the top), G = a hash, B = 0, A = 1.
        static void ApplySwayTestColours()
        {
            var view = HoleView.Current; if (!view) return;
            int meshes = 0, objects = 0;
            foreach (var mf in view.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!mf.name.StartsWith("PLANT_") || !mf.sharedMesh) continue;
                var src = mf.sharedMesh;
                if (src.colors32 != null && src.colors32.Length == src.vertexCount) continue;      // real sway data: leave it alone
                if (!swayMeshes.TryGetValue(src, out var copy))
                {
                    copy = Object.Instantiate(src); copy.name = src.name + " (test sway colours)";
                    var v = copy.vertices; float lo = float.MaxValue, hi = float.MinValue;
                    foreach (var p in v) { lo = Mathf.Min(lo, p.y); hi = Mathf.Max(hi, p.y); }
                    var (kind, _, tip) = SwayKindOf(mf.name); float h = Mathf.Max(hi - lo, 1e-4f);
                    var cols = new Color32[v.Length];
                    for (int i = 0; i < v.Length; i++)
                    {
                        float t = kind == "vine" ? (hi - v[i].y) / h : (v[i].y - lo) / h;
                        cols[i] = new Color32((byte)Mathf.RoundToInt(Mathf.Clamp01(tip * t) * 255f), (byte)(i * 37 % 256), 0, 255);
                    }
                    copy.colors32 = cols; swayMeshes[src] = copy; meshes++;
                }
                mf.sharedMesh = copy; objects++;
            }
            Debug.Log($"[PostcardLookStills] test sway colours: {objects} PLANT_* object(s) on {meshes} new mesh copies (meshes that had real sway colours were left alone)");
        }

        static Color32[] RenderNow()
        {
            cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply(); RenderTexture.active = null; cam.targetTexture = null;
            return tex.GetPixels32();
        }

        /// The sway frame pair of one shot (all in the current player frame): A = the shot's frame at swayTime, B = the same camera at swayTime + dt, N0 / N1 = the same two clocks with every PLANT_* renderer off.
        ///   plants   = pixels where a plant is in A or B (frame minus the plant-less frame differs);  changed = plant pixels whose colour moved by >= 6 levels between A and B;
        ///   nonplant = the plant-less frames N0 and N1 must be IDENTICAL (nothing but plants moves: ground, rocks, lava, surf, sky, water all read no clock of ours);  repeat = A rendered again (deterministic).
        ///   mirror_max_disp_yd = the largest displacement the shader gives any coloured plant of the hole at those two clocks (GolfWindSway.Displacement on the real instance pivots), per kind, vs the caps.
        static string SwayStats(Shot s, Color32[] A)
        {
            var view = HoleView.Current;
            var plants = new List<Renderer>();
            if (view) foreach (var r in view.GetComponentsInChildren<Renderer>(false)) if (r.enabled && r.name.StartsWith("PLANT_")) plants.Add(r);
            float t0 = swayTime, t1 = swayTime + swayPairDt;
            void Plants(bool on) { foreach (var r in plants) r.enabled = on; }
            var A2 = RenderNow();
            Plants(false); var N0 = RenderNow(); Plants(true);
            GolfWindSway.FreezeTime = t1; GolfWindSway.Push();
            var B = RenderNow(); File.WriteAllBytes(Path.Combine(outDir, s.Name + "__sway_t1.png"), tex.EncodeToPNG());
            int waterPx = 0; var sea = new List<Renderer>();
            if (view) foreach (var r in view.GetComponentsInChildren<Renderer>(false)) if (r.enabled && (r.name.StartsWith("WATER_OCEAN") || r.name.StartsWith("WATER_SHELF"))) sea.Add(r);
            foreach (var r in sea) r.enabled = false; var dryB = RenderNow(); foreach (var r in sea) r.enabled = true;     // the sea at the second clock too: where a plant moved away it shows through
            Plants(false); var N1 = RenderNow(); Plants(true);
            GolfWindSway.FreezeTime = t0; GolfWindSway.Push();
            // the sea (WATER_OCEAN / WATER_SHELF = TennisWater) reads the engine clock, not ours: where it is, two separate runs can differ. Its pixels are written as a mask so the cross-run comparison can leave them out.
            foreach (var r in sea) r.enabled = false; var dry = RenderNow(); foreach (var r in sea) r.enabled = true;
            { var mt = new Texture2D(W, H, TextureFormat.RGB24, false); var mp = new Color32[A.Length]; int seaPx = 0;
              for (int i = 0; i < A.Length; i++) { bool on = Mathf.Abs(A[i].r - dry[i].r) + Mathf.Abs(A[i].g - dry[i].g) + Mathf.Abs(A[i].b - dry[i].b) > 0 || Mathf.Abs(B[i].r - dryB[i].r) + Mathf.Abs(B[i].g - dryB[i].g) + Mathf.Abs(B[i].b - dryB[i].b) > 0; if (on) seaPx++; mp[i] = on ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 255); }
              mt.SetPixels32(mp); mt.Apply(); File.WriteAllBytes(Path.Combine(outDir, s.Name + "__sway_water.png"), mt.EncodeToPNG()); Object.DestroyImmediate(mt); waterPx = seaPx; }
            int Mx(Color32 a, Color32 b) => Mathf.Max(Mathf.Abs(a.r - b.r), Mathf.Max(Mathf.Abs(a.g - b.g), Mathf.Abs(a.b - b.b)));
            int repeatMax = 0, nonDiff = 0, nonMax = 0, plantPx = 0, changed = 0, outside = 0; long sumChange = 0;
            var diff = new Texture2D(W, H, TextureFormat.RGB24, false); var dpx = new Color32[A.Length];
            for (int i = 0; i < A.Length; i++)
            {
                repeatMax = Mathf.Max(repeatMax, Mx(A[i], A2[i]));
                int n = Mx(N0[i], N1[i]); if (n > 0) nonDiff++; nonMax = Mathf.Max(nonMax, n);
                bool plant = Mx(A[i], N0[i]) > 0 || Mx(B[i], N1[i]) > 0;
                int d = Mx(A[i], B[i]);
                if (plant) { plantPx++; sumChange += d; if (d >= 6) changed++; } else if (d > 0) outside++;
                byte v = (byte)Mathf.Min(255, d * 4); dpx[i] = new Color32(v, v, v, 255);
            }
            diff.SetPixels32(dpx); diff.Apply(); File.WriteAllBytes(Path.Combine(outDir, s.Name + "__sway_diff.png"), diff.EncodeToPNG()); Object.DestroyImmediate(diff);
            // the shader's own displacement of the real plants at the two clocks (mirror), per kind, over every coloured instance
            var wind = ForcedWindValue();
            var maxDisp = new Dictionary<string, float>(); int colouredMeshes = 0, plantMeshes = 0, colouredObjects = 0; var seen = new HashSet<Mesh>(); var tipOf = new Dictionary<Mesh, (float w, float g)>();
            foreach (var r in plants)
            {
                var mf = r.GetComponent<MeshFilter>(); if (!mf || !mf.sharedMesh) continue;
                var m = mf.sharedMesh; bool first = seen.Add(m); if (first) plantMeshes++;
                if (!tipOf.TryGetValue(m, out var tw))
                {
                    var c = m.colors32; float bw = 0, bg = 0; bool has = c != null && c.Length == m.vertexCount;
                    if (has) for (int i = 0; i < c.Length; i++) if (c[i].b < 128 && c[i].r / 255f > bw) { bw = c[i].r / 255f; bg = c[i].g / 255f; }
                    tw = (has ? bw : 0f, bg); tipOf[m] = tw; if (has && bw > 0) colouredMeshes++;
                }
                if (tw.w <= 0) continue;
                colouredObjects++;
                var kind = SwayKindOf(r.name).kind; var p = r.transform.position;
                float d = Mathf.Max(GolfWindSway.Displacement(tw.w, tw.g, p.x, p.z, wind, GolfWindSway.GlobalValue(ForcedWindNow(), t0).w).magnitude, GolfWindSway.Displacement(tw.w, tw.g, p.x, p.z, wind, GolfWindSway.GlobalValue(ForcedWindNow(), t1).w).magnitude);
                maxDisp[kind] = Mathf.Max(maxDisp.TryGetValue(kind, out var o) ? o : 0, d);
            }
            var disp = string.Join(", ", maxDisp.OrderBy(k => k.Key).Select(k => "\"" + k.Key + "\": " + F(k.Value)).ToArray());
            var caps = string.Join(", ", SwayKinds.Select(k => "\"" + k.kind + "\": " + F(k.cap)).ToArray());
            Debug.Log($"[PostcardLookStills] sway pair {s.Name}: plant px {plantPx} ({plantPx * 100f / A.Length:F1} %), changed {changed} ({(plantPx > 0 ? changed * 100f / plantPx : 0):F1} % of them), plant-less frames differ in {nonDiff} px (max {nonMax}), repeat max {repeatMax}, outside-plant changes {outside}, coloured plant meshes {colouredMeshes}/{plantMeshes}");
            return "\"sway\": { \"mph\": " + F(swayMph) + ", \"deg\": " + F(swayDeg) + ", \"t0\": " + F(t0) + ", \"t1\": " + F(t1) + ", \"test_colours\": " + (swayTestColours ? "true" : "false") +
                   ", \"plant_objects\": " + plants.Count + ", \"plant_meshes\": " + plantMeshes + ", \"coloured_plant_meshes\": " + colouredMeshes + ", \"coloured_plant_objects\": " + colouredObjects +
                   ", \"repeat_max_diff\": " + repeatMax + ", \"nonplant_px_diff\": " + nonDiff + ", \"nonplant_max_diff\": " + nonMax + ", \"plant_px\": " + plantPx + ", \"plant_share\": " + F(plantPx / (float)A.Length) +
                   ", \"changed_px\": " + changed + ", \"changed_share_of_plant_px\": " + F(plantPx > 0 ? changed / (float)plantPx : 0) + ", \"mean_abs_change_plant_px\": " + F(plantPx > 0 ? sumChange / (float)plantPx : 0) +
                   ", \"outside_plant_changed_px\": " + outside + ", \"water_px\": " + waterPx + ", \"water_mask\": \"" + s.Name + "__sway_water.png\"" + ", \"mirror_max_disp_yd\": { " + disp + " }, \"caps_yd\": { " + caps + " }, \"frame_b\": \"" + s.Name + "__sway_t1.png\", \"diff\": \"" + s.Name + "__sway_diff.png\" }";
        }

        static List<Renderer> RenderersNamed(params string[] prefixes)
        {
            var list = new List<Renderer>(); var view = HoleView.Current;
            if (view) foreach (var r in view.GetComponentsInChildren<Renderer>(false)) if (r.enabled) foreach (var p in prefixes) if (r.name.StartsWith(p)) { list.Add(r); break; }
            return list;
        }

        /// The plant-less frame and the plant-less + sea-less frame of the shot as it stands now (cameras / game state untouched).
        static (Color32[] noPlants, Color32[] dry) PlantlessFrames()
        {
            var plants = RenderersNamed("PLANT_"); var sea = RenderersNamed("WATER_OCEAN", "WATER_SHELF");
            foreach (var r in plants) r.enabled = false;
            var a = RenderNow();
            foreach (var r in sea) r.enabled = false; var b = RenderNow(); foreach (var r in sea) r.enabled = true;
            foreach (var r in plants) r.enabled = true;
            return (a, b);
        }

        static void BeginRealtime(Shot s)
        {
            cam.fieldOfView = s.Fov;
            (rtA, rtADry) = PlantlessFrames();
            rtShot = s; rtT0 = Time.realtimeSinceStartup; rtDue = rtT0 + rtDt; rtPending = true;
        }

        static void FinishRealtime()
        {
            rtPending = false;
            cam.fieldOfView = rtShot.Fov;
            var (b, bDry) = PlantlessFrames();
            float dt = Time.realtimeSinceStartup - rtT0;
            int n = rtA.Length; var sea = new bool[n]; int seaPx = 0;
            int Mx(Color32 p, Color32 q) => Mathf.Max(Mathf.Abs(p.r - q.r), Mathf.Max(Mathf.Abs(p.g - q.g), Mathf.Abs(p.b - q.b)));
            for (int i = 0; i < n; i++) if (Mx(rtA[i], rtADry[i]) > 0 || Mx(b[i], bDry[i]) > 0) { sea[i] = true; seaPx++; }
            var grown = (bool[])sea.Clone();                                    // 3 px around the sea (anti-aliased shoreline)
            for (int pass = 0; pass < 3; pass++)
            {
                var g2 = (bool[])grown.Clone();
                for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) { int i = y * W + x; if (!grown[i]) continue; if (x > 0) g2[i - 1] = true; if (x < W - 1) g2[i + 1] = true; if (y > 0) g2[i - W] = true; if (y < H - 1) g2[i + W] = true; }
                grown = g2;
            }
            int nonSeaDiff = 0, nonSeaMax = 0, seaDiff = 0, other = 0;
            for (int i = 0; i < n; i++)
            {
                int d = Mx(rtA[i], b[i]);
                if (grown[i]) { if (d > 0) seaDiff++; } else { if (d > 0) nonSeaDiff++; nonSeaMax = Mathf.Max(nonSeaMax, d); other++; }
            }
            Debug.Log($"[PostcardLookStills] real-time pair {rtShot.Name}: {dt:F2} s apart, plant-less frames outside the sea differ in {nonSeaDiff} px (max {nonSeaMax}) of {other}; sea {seaPx} px (grown {grown.Count(v => v)}), {seaDiff} of them moved");
            realtimeJson.Add("    { \"name\": \"" + rtShot.Name + "\", \"hole\": " + rtShot.Hole + ", \"dt_real\": " + F(dt) + ", \"nonsea_px\": " + other + ", \"nonsea_px_diff\": " + nonSeaDiff + ", \"nonsea_max_diff\": " + nonSeaMax + ", \"sea_px\": " + seaPx + ", \"sea_px_moved\": " + seaDiff + " }");
        }

        static Wind ForcedWindNow() => GolfWindSway.ForcedWind ?? new Wind(swayMph, swayDeg);
        static Vector2 ForcedWindValue() { var v = GolfWindSway.GlobalValue(ForcedWindNow(), 0); return new Vector2(v.x, v.z); }

        // ------------------------------------------------------------------ variants (GOLF_STILLS_VARIANTS: experiments, in memory, put back after each render)

        static List<(string name, List<(string key, string val)> tweaks)> ParseVariants()
        {
            var list = new List<(string, List<(string, string)>)>();
            string spec = System.Environment.GetEnvironmentVariable("GOLF_STILLS_VARIANTS");
            if (string.IsNullOrWhiteSpace(spec)) return list;
            foreach (var v in spec.Split('|'))
            {
                int colon = v.IndexOf(':'); if (colon <= 0) continue;
                var tweaks = new List<(string, string)>();
                foreach (var t in v.Substring(colon + 1).Split(';'))
                {
                    int eq = t.IndexOf('='); if (eq <= 0) continue;
                    tweaks.Add((t.Substring(0, eq).Trim(), t.Substring(eq + 1).Trim()));
                }
                list.Add((v.Substring(0, colon).Trim(), tweaks));
            }
            return list;
        }

        static float[] Floats(string s) { var p = s.Split(','); var f = new float[p.Length]; for (int i = 0; i < p.Length; i++) float.TryParse(p[i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out f[i]); return f; }

        static void RenderVariants(Shot s)
        {
            foreach (var (name, tweaks) in ParseVariants())
            {
                var undo = new List<System.Action>();
                foreach (var (key, val) in tweaks) Tweak(key, val, undo);
                cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply(); RenderTexture.active = null; cam.targetTexture = null;
                File.WriteAllBytes(Path.Combine(outDir, s.Name + "__" + name + ".png"), tex.EncodeToPNG());
                for (int i = undo.Count - 1; i >= 0; i--) undo[i]();
                Debug.Log($"[PostcardLookStills] variant {name} of {s.Name}");
            }
        }

        static void Tweak(string key, string val, List<System.Action> undo)
        {
            var f = key == "hide" ? null : Floats(val);
            switch (key)
            {
                case "fog": { bool o = RenderSettings.fog; RenderSettings.fog = f[0] != 0; undo.Add(() => RenderSettings.fog = o); break; }
                case "fogcolor": { var o = RenderSettings.fogColor; RenderSettings.fogColor = new Color(f[0], f[1], f[2]); undo.Add(() => RenderSettings.fogColor = o); break; }
                case "fogrange": { float a = RenderSettings.fogStartDistance, b = RenderSettings.fogEndDistance; RenderSettings.fogStartDistance = f[0]; RenderSettings.fogEndDistance = f[1]; undo.Add(() => { RenderSettings.fogStartDistance = a; RenderSettings.fogEndDistance = b; }); break; }
                case "ambient":
                {
                    Color a = RenderSettings.ambientSkyColor, b = RenderSettings.ambientEquatorColor, c = RenderSettings.ambientGroundColor;
                    RenderSettings.ambientSkyColor = a * f[0]; RenderSettings.ambientEquatorColor = b * f[0]; RenderSettings.ambientGroundColor = c * f[0];
                    undo.Add(() => { RenderSettings.ambientSkyColor = a; RenderSettings.ambientEquatorColor = b; RenderSettings.ambientGroundColor = c; });
                    break;
                }
                case "ambeq": { var o = RenderSettings.ambientEquatorColor; RenderSettings.ambientEquatorColor = new Color(f[0], f[1], f[2]); undo.Add(() => RenderSettings.ambientEquatorColor = o); break; }
                case "ambsky": { var o = RenderSettings.ambientSkyColor; RenderSettings.ambientSkyColor = new Color(f[0], f[1], f[2]); undo.Add(() => RenderSettings.ambientSkyColor = o); break; }
                case "ambgnd": { var o = RenderSettings.ambientGroundColor; RenderSettings.ambientGroundColor = new Color(f[0], f[1], f[2]); undo.Add(() => RenderSettings.ambientGroundColor = o); break; }
                case "sun": { var l = RenderSettings.sun; if (!l) break; var o = l.color; l.color = new Color(f[0], f[1], f[2]); undo.Add(() => l.color = o); break; }
                case "lights":
                {
                    var view = HoleView.Current; if (!view) break;
                    foreach (var l in view.GetComponentsInChildren<Light>(true)) if (l.type == LightType.Point) { float o = l.intensity; var ll = l; ll.intensity = o * f[0]; undo.Add(() => ll.intensity = o); }
                    break;
                }
                case "basalt_tint": { var m = GolfLook.Get("LK_BASALT"); var o = m.GetColor("_BaseColor"); m.SetColor("_BaseColor", new Color(f[0], f[1], f[2])); undo.Add(() => m.SetColor("_BaseColor", o)); break; }
                case "basalt_emis": { var m = GolfLook.Get("LK_BASALT"); var o = m.GetVector("_EmissionColor"); m.SetVector("_EmissionColor", new Vector4(f[0], f[1], f[2], 1)); undo.Add(() => m.SetVector("_EmissionColor", o)); break; }
                case "basalt_smooth": { var m = GolfLook.Get("LK_BASALT"); float o = m.GetFloat("_Smoothness"); m.SetFloat("_Smoothness", f[0]); undo.Add(() => m.SetFloat("_Smoothness", o)); break; }
                case "smoke":   // gain, power, softfade, lit, selflight, fogshare (any prefix of the list)
                {
                    var m = GolfLook.Get("LK_SMOKE");
                    string[] props = { "_AlphaGain", "_AlphaPower", "_SoftFade", "_Lit", "_Emission", "_FogShare" };
                    for (int i = 0; i < f.Length && i < props.Length; i++) { string pn = props[i]; float o = m.GetFloat(pn); m.SetFloat(pn, f[i]); undo.Add(() => m.SetFloat(pn, o)); }
                    break;
                }
                case "hide":    // renderers whose name starts with this prefix (string value, e.g. hide=DRESS_CONE)
                {
                    var view = HoleView.Current; if (!view) break;
                    foreach (var r in view.GetComponentsInChildren<Renderer>(false)) if (r.enabled && r.name.StartsWith(val)) { var rr = r; rr.enabled = false; undo.Add(() => rr.enabled = true); }
                    break;
                }
                case "ball":    // r,g,b multiplier on the ball's _BaseColor (a MaterialPropertyBlock on the game ball, put back afterwards)
                {
                    var ballT = (Transform)typeof(GolfGame).GetField("ball", Private).GetValue(game); var br = ballT ? ballT.GetComponent<Renderer>() : null; if (!br) break;
                    var mpb = new MaterialPropertyBlock(); mpb.SetColor("_BaseColor", new Color(f[0], f[1], f[2], 1)); br.SetPropertyBlock(mpb);
                    undo.Add(() => br.SetPropertyBlock(null));
                    break;
                }
                case "lavafog": { var m = GolfLook.Get("LK_LAVA"); if (!m.HasProperty("_FogShare")) break; float o = m.GetFloat("_FogShare"); m.SetFloat("_FogShare", f[0]); undo.Add(() => m.SetFloat("_FogShare", o)); break; }
                case "ballmat": // r,g,b exposure, selflight (GolfAtmosphere.SetBallLook; the previous values are put back)
                {
                    var e0 = GolfAtmosphere.BallExposureNow; float s0 = GolfAtmosphere.BallSelfLightNow;
                    GolfAtmosphere.SetBallLook(new Color(f[0], f[1], f[2]), f.Length > 3 ? f[3] : 0);
                    undo.Add(() => GolfAtmosphere.SetBallLook(e0, s0));
                    break;
                }
                case "nosmoke":
                {
                    var view = HoleView.Current; if (!view) break;
                    foreach (var r in view.GetComponentsInChildren<Renderer>(false)) if (r.enabled && r.name.StartsWith(SmokePrefix)) { var rr = r; rr.enabled = false; undo.Add(() => rr.enabled = true); }
                    break;
                }
                default: Debug.LogWarning("[PostcardLookStills] unknown variant tweak '" + key + "'"); break;
            }
        }

        static void Finish(int code)
        {
            string json = "{\n  \"tool\": \"PostcardLookStills\",\n  \"unity\": \"" + Application.unityVersion + "\",\n  \"pipeline\": \"" +
                (QualitySettings.renderPipeline ? QualitySettings.renderPipeline.name : UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline ? UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.name : "none") +
                "\",\n  \"qualityLevel\": " + QualitySettings.GetQualityLevel() + ",\n  \"anisotropicFiltering\": \"" + QualitySettings.anisotropicFiltering + "\",\n  \"golfLookAniso\": " + GolfLook.Aniso + ",\n  \"board\": " + (board ? "true" : "false") + ",\n  \"size\": [" + W + ", " + H + "],\n  \"shots\": [\n" +
                string.Join(",\n", statsJson.ToArray()) + "\n  ]\n}\n";
            if (statsJson.Count > 0) File.WriteAllText(Path.Combine(outDir, "stills_stats.json"), json);
            if (realtimeJson.Count > 0) File.WriteAllText(Path.Combine(outDir, "sway_realtime.json"), "{\n  \"dt\": " + F(rtDt) + ",\n  \"shots\": [\n" + string.Join(",\n", realtimeJson.ToArray()) + "\n  ]\n}\n");
            RestorePipeline();
            GolfWindSway.FreezeTime = null; GolfWindSway.ForcedWind = null;
            SessionState.SetBool(Flag, false);
            if (Application.isBatchMode) EditorApplication.Exit(code); else EditorApplication.isPlaying = false;
        }

        // ------------------------------------------------------------------ pixel statistics

        static readonly string[] BandNames = { "red_0_30", "orange_30_60", "yellow_60_90", "lime_90_120", "green_120_150", "spring_150_180", "cyan_180_210", "azure_210_240", "blue_240_270", "violet_270_300", "magenta_300_330", "rose_330_360" };

        static string F(float v) => v.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);
        static string Arr(params float[] v) => "[" + string.Join(", ", System.Array.ConvertAll(v, F)) + "]";

        /// The smoke cards' own contribution to the frame: render the same camera again with every DRESS_SMOKE* renderer off and compare. (Two consecutive renders of
        /// one player frame share Time.time, so the animated sea does not move between them: only the cards differ.)
        static string SmokeStats(Color32[] full)
        {
            var view = HoleView.Current;
            var cards = new List<Renderer>();
            if (view) foreach (var r in view.GetComponentsInChildren<Renderer>(false)) if (r.enabled && r.name.StartsWith(SmokePrefix)) cards.Add(r);
            if (cards.Count == 0) return "\"smoke\": { \"cards\": 0 }";
            foreach (var r in cards) r.enabled = false;
            cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply(); RenderTexture.active = null; cam.targetTexture = null;
            var dry = tex.GetPixels32();
            foreach (var r in cards) r.enabled = true;
            var deltas = new List<float>(); int readable = 0; double pr = 0, pg = 0, pb = 0;
            for (int i = 0; i < full.Length; i++)
            {
                float a = .2126f * full[i].r + .7152f * full[i].g + .0722f * full[i].b, b = .2126f * dry[i].r + .7152f * dry[i].g + .0722f * dry[i].b, d = Mathf.Abs(a - b);
                if (d <= 1.5f) continue;
                deltas.Add(d); pr += full[i].r; pg += full[i].g; pb += full[i].b;
                if (d >= ReadableDelta) readable++;
            }
            deltas.Sort();
            int n = deltas.Count; double all = full.Length;
            float Q(float q) => n == 0 ? 0 : deltas[Mathf.Min(n - 1, (int)(n * q))];
            return "\"smoke\": { \"cards\": " + cards.Count + ", \"changed_px\": " + n + ", \"share_changed\": " + F((float)(n / all)) + ", \"readable_px\": " + readable + ", \"share_readable\": " + F((float)(readable / all))
                 + ", \"delta_lum_median\": " + F(Q(.5f)) + ", \"delta_lum_p95\": " + F(Q(.95f)) + ", \"delta_lum_p99\": " + F(Q(.99f)) + ", \"plume_mean_rgb\": " + (n > 0 ? Arr((float)(pr / n), (float)(pg / n), (float)(pb / n)) : "[0, 0, 0]") + " }";
        }

        static string Stats(Shot s, Color32[] px, string smoke)
        {
            double r = 0, g = 0, b = 0, lum = 0;
            var bands = new int[BandNames.Length]; int grey = 0, dark = 0;
            foreach (var p in px)
            {
                r += p.r; g += p.g; b += p.b; lum += .2126 * p.r + .7152 * p.g + .0722 * p.b;
                Color.RGBToHSV(p, out var h, out var sat, out var v);
                if (v < .15f) dark++; else if (sat < .15f) grey++; else bands[Mathf.Min(BandNames.Length - 1, (int)(h * 12f))]++;
            }
            double n = px.Length;
            int x0 = W / 3, x1 = 2 * W / 3, y0 = 0, y1 = H / 3;
            double cr = 0, cg = 0, cb = 0, cl = 0; int cn = 0;
            for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++) { var p = px[y * W + x]; cr += p.r; cg += p.g; cb += p.b; cl += .2126 * p.r + .7152 * p.g + .0722 * p.b; cn++; }
            var mean = new Color((float)(cr / cn / 255), (float)(cg / cn / 255), (float)(cb / cn / 255));
            Color.RGBToHSV(mean, out var mh, out var ms, out var mv);
            var sb = new StringBuilder();
            sb.Append("    { \"name\": \"").Append(s.Name).Append("\", \"hole\": ").Append(s.Hole).Append(", \"file\": \"").Append(s.Name).Append(".png\",\n");
            sb.Append("      \"camera\": { \"pos\": ").Append(Arr(cam.transform.position.x, cam.transform.position.y, cam.transform.position.z))
              .Append(", \"look\": ").Append(Arr(cam.transform.position.x + cam.transform.forward.x, cam.transform.position.y + cam.transform.forward.y, cam.transform.position.z + cam.transform.forward.z))
              .Append(", \"fov\": ").Append(F(cam.fieldOfView)).Append(" },\n");
            sb.Append("      \"mean_rgb\": ").Append(Arr((float)(r / n), (float)(g / n), (float)(b / n))).Append(", \"luminance\": ").Append(F((float)(lum / n))).Append(",\n");
            sb.Append("      \"central_lower_third\": { \"region\": [").Append(x0).Append(", ").Append(y0).Append(", ").Append(x1).Append(", ").Append(y1).Append("], \"mean_rgb\": ")
              .Append(Arr((float)(cr / cn), (float)(cg / cn), (float)(cb / cn))).Append(", \"mean_hsv\": ").Append(Arr(mh * 360f, ms, mv)).Append(", \"luminance\": ").Append(F((float)(cl / cn))).Append(" },\n");
            sb.Append("      \"hue_bands\": { ");
            for (int i = 0; i < BandNames.Length; i++) sb.Append('"').Append(BandNames[i]).Append("\": ").Append(F((float)(bands[i] / n))).Append(", ");
            sb.Append("\"grey\": ").Append(F((float)(grey / n))).Append(", \"dark\": ").Append(F((float)(dark / n))).Append(" },\n      ").Append(smoke).Append(" }");
            return sb.ToString();
        }

        // ------------------------------------------------------------------ test board (GOLF_STILLS_BOARD=1; not for proof)

        static GameObject TestBoard(int number)
        {
            var root = new GameObject("Stills test board");
            var (ballAt, aim) = number switch { 8 => (new CoursePoint(0, 0), new Vector3(19, 0, 178)), 9 => (new CoursePoint(0, 0), new Vector3(3, 0, 60)), _ => (new CoursePoint(0, 0), new Vector3(14.3f, 0, 66.2f)) };
            var a = aim.normalized;
            var origin = HoleView.ToWorld(ballAt, 0); origin.y += .03f;
            var right = new Vector3(a.z, 0, -a.x);
            float len = number == 10 ? 22 : number == 8 ? 14 : 70;
            Material M(string n) => GolfLook.GetForHole(n, number);
            string left = number == 9 ? "LK_SCRUB" : "LK_ROUGH", third = number == 8 ? "LK_PATH" : number == 9 ? "LK_SAND" : "LK_GREEN";
            void Strip(string n, float x0, float x1, float tile) => LookPixels.Quad(root, n, M(n), tile, origin + right * x0, origin + right * x1, origin + right * x1 + a * len, origin + right * x0 + a * len, Vector3.up);
            Strip(left, -24, -8, 12); Strip("LK_FAIRWAY", -8, 8, 10); Strip(third, 8, 24, third == "LK_SAND" ? 6 : 5);
            if (number == 10)
            {
                var c = new Vector3(171.1f, .08f, 161.5f);
                LookPixels.Quad(root, "LK_LAVA", M("LK_LAVA"), 24, c + new Vector3(-160, 0, -160), c + new Vector3(-160, 0, 160), c + new Vector3(160, 0, 160), c + new Vector3(160, 0, -160), Vector3.up);
            }
            return root;
        }
    }
}
