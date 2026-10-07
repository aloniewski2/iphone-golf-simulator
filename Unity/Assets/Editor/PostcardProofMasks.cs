using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using GolfArcade.Course;
using GolfArcade.Game;

namespace GolfArcade.EditorTools
{
    /// POSTCARD_LOOK proof, area I: per-shot RENDERER MASKS of the same Game-view frames PostcardLookStills takes, so the pixel metrics (lava colour, shelf vs the
    /// brightest shapes, basalt joints, smoke edge, path vs ball) read the pixels each material really covers instead of guessing from colour.
    /// Same shot file and framing code as PostcardLookStills (the cfg schema of RUNTIME.md v2.8), 900x1600, same camera, same lights.
    ///
    ///   GOLF_PROOF_CFG=<shots.json> GOLF_PROOF_OUT=<dir>
    ///     Unity -batchmode -projectPath Unity -executeMethod GolfArcade.EditorTools.PostcardProofMasks.Run -logFile <abs log>     (always through work/postcard-look/heavy.sh)
    ///
    /// Output per shot: <name>.full.png (the frame itself: must equal the PostcardLookStills frame), <name>.<group>.png (8-bit mask, 255 = the group's renderers change the
    /// frame by more than the group's threshold, i.e. they are visible there), masks_meta.json (counts, thresholds, camera, ball radius in pixels).
    /// A group is hidden by disabling its renderers (water, lava, smoke: they cast no shadow) or by switching them to ShadowsOnly (props, ground, rock: shadows stay, so the
    /// diff is only what the group itself paints). Nothing is written to the project; the scene objects are put back after every group.
    [InitializeOnLoad]
    public static class PostcardProofMasks
    {
        const string Flag = "PostcardProofMasks";
        const int W = 900, H = 1600;
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [System.Serializable] sealed class Cfg { public ShotCfg[] shots; }
        [System.Serializable] sealed class ShotCfg { public string name; public int hole; public string kind; public float[] pos, look, ball, aim; public float fov; public bool putting; }
        sealed class Shot
        {
            public string Name; public int Hole; public bool Landmark;
            public Vector3 Pos, Look; public float Fov = 60;
            public bool HasBall; public float BallX, BallD; public float AimX, AimD; public bool Putting;
        }
        sealed class Group
        {
            public string Name; public System.Func<Renderer, bool> Match; public bool ShadowsOnly; public int Threshold;
            public Group(string n, System.Func<Renderer, bool> m, bool shadowsOnly, int thr = 30) { Name = n; Match = m; ShadowsOnly = shadowsOnly; Threshold = thr; }
        }

        static bool HasMat(Renderer r, string prefix) { foreach (var m in r.sharedMaterials) if (m && m.name.StartsWith(prefix)) return true; return false; }
        // diff masks (hide the renderers, render again, the pixels that change): meaningful for whole-renderer groups and for translucent cards (smoke, surf, waterfall) that an ID render cannot size
        static readonly Group[] Groups =
        {
            new Group("shelf",    r => r.name.StartsWith("WATER_SHELF"), false),
            new Group("ocean",    r => r.name.StartsWith("WATER_OCEAN"), false),
            new Group("surf",     r => r.name.StartsWith("WATER_SURF"), false, 12),
            new Group("fall",     r => r.name.StartsWith("WATER_FALL"), false, 12),
            new Group("lava",     r => r.name.StartsWith("WATER_LAVA"), false),
            new Group("smoke",    r => r.name.StartsWith("DRESS_SMOKE"), false, 6),
            new Group("plant",    r => r.name.StartsWith("PLANT_"), true),
            new Group("rock",     r => r.name.StartsWith("ROCK_"), true),
        };

        // ID render: every material slot of the model swapped for a flat unlit colour of its category (fog off, black clear): exact per-material pixel masks, also for meshes with several slots
        static readonly string[] Categories =
            { "fairway", "green", "rough", "scrub", "sand", "path", "cliff", "masonry", "rock", "basalt", "plant", "ocean", "shelf", "surf", "fall", "lava", "smoke", "gameplay", "other" };
        static string CategoryOf(Renderer r, Material m)
        {
            string n = m ? m.name : "";
            if (n.StartsWith("LK_FAIRWAY")) return "fairway";
            if (n.StartsWith("LK_GREEN")) return "green";
            if (n.StartsWith("LK_ROUGH")) return "rough";
            if (n.StartsWith("LK_SCRUB")) return "scrub";
            if (n.StartsWith("LK_SAND")) return "sand";
            if (n.StartsWith("LK_PATH")) return "path";
            if (n.StartsWith("LK_CLIFF")) return "cliff";
            if (n.StartsWith("LK_MASONRY")) return "masonry";
            if (n.StartsWith("LK_ROCK")) return "rock";
            if (n.StartsWith("LK_BASALT")) return "basalt";
            if (n.StartsWith("LK_PLANTS")) return "plant";
            if (n.StartsWith("LK_WATER_SHALLOW")) return "shelf";
            if (n.StartsWith("LK_WATER")) return "ocean";
            if (n.StartsWith("LK_SURF")) return "surf";
            if (n.StartsWith("LK_FALL")) return "fall";
            if (n.StartsWith("LK_LAVA")) return "lava";
            if (n.StartsWith("LK_SMOKE")) return "smoke";
            if (r.name.StartsWith("FLAG") || r.name.StartsWith("POLE") || r.name.StartsWith("HOLE_CUP")) return "gameplay";
            return "other";
        }
        static Color32 IdColor(int i)
        {
            int[] v = { 40, 120, 200, 255 };
            int k = i + 1;                              // skip black
            return new Color32((byte)v[k / 16 % 4], (byte)v[k / 4 % 4], (byte)v[k % 4], 255);
        }

        static readonly List<Shot> queue = new();
        static int warm, shotIndex = -1, wait, startFrame, settleTicks, staleShots;
        static string outDir;
        static RenderTexture rt; static Texture2D tex; static Camera cam;
        static GolfGame game; static CameraRig rig;
        static int currentHole = -1;
        static readonly List<string> meta = new();

        static PostcardProofMasks() { EditorApplication.update += Tick; }

        public static void Run()
        {
            outDir = System.Environment.GetEnvironmentVariable("GOLF_PROOF_OUT");
            if (string.IsNullOrEmpty(outDir)) { Debug.LogError("[PostcardProofMasks] GOLF_PROOF_OUT is required"); EditorApplication.Exit(1); return; }
            outDir = Path.GetFullPath(outDir);
            Directory.CreateDirectory(outDir);
            SessionState.SetString(Flag + "out", outDir);
            SessionState.SetString(Flag + "cfg", System.Environment.GetEnvironmentVariable("GOLF_PROOF_CFG") ?? "");
            EditorSceneManager.OpenScene("Assets/Scenes/Golf.unity");
            SessionState.SetBool(Flag, true);
            EditorApplication.isPlaying = true;
        }

        static bool BuildQueue(out string error)
        {
            error = null; queue.Clear();
            string cfgPath = SessionState.GetString(Flag + "cfg", "");
            if (!File.Exists(cfgPath)) { error = "GOLF_PROOF_CFG not found: " + cfgPath; return false; }
            var cfg = JsonUtility.FromJson<Cfg>(File.ReadAllText(cfgPath));
            if (cfg == null || cfg.shots == null || cfg.shots.Length == 0) { error = "GOLF_PROOF_CFG has no shots"; return false; }
            foreach (var c in cfg.shots)
            {
                if (string.IsNullOrEmpty(c.name)) { error = "a shot has no name"; return false; }
                bool landmark = c.kind == "landmark" || (string.IsNullOrEmpty(c.kind) && c.pos != null);
                var s = new Shot { Name = c.name, Hole = c.hole, Landmark = landmark, Fov = c.fov > 1 ? c.fov : 60, Putting = c.putting };
                if (landmark)
                {
                    if (c.pos == null || c.pos.Length < 3 || c.look == null || c.look.Length < 3) { error = $"shot '{c.name}': landmark needs pos and look"; return false; }
                    s.Pos = new Vector3(c.pos[0], c.pos[1], c.pos[2]); s.Look = new Vector3(c.look[0], c.look[1], c.look[2]);
                    if (c.ball != null && c.ball.Length >= 2) { s.HasBall = true; s.BallX = c.ball[0]; s.BallD = c.ball[1]; }
                }
                else
                {
                    if (c.ball == null || c.ball.Length < 2 || c.aim == null || c.aim.Length < 2) { error = $"shot '{c.name}': address needs ball and aim"; return false; }
                    s.HasBall = true; s.BallX = c.ball[0]; s.BallD = c.ball[1]; s.AimX = c.aim[0]; s.AimD = c.aim[1];
                }
                queue.Add(s);
            }
            var ordered = queue.Select((s, i) => (s, i)).OrderBy(t => t.s.Hole).ThenBy(t => t.i).Select(t => t.s).ToList();
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
                if (!BuildQueue(out var error)) { Debug.LogError("[PostcardProofMasks] " + error); Finish(1); return; }
                game = Object.FindFirstObjectByType<GolfGame>();
                rig = Object.FindFirstObjectByType<CameraRig>();
                cam = rig ? rig.GetComponentInChildren<Camera>() : Camera.main;
                foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) c.enabled = false;
                rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
                tex = new Texture2D(W, H, TextureFormat.RGB24, false);
                var pipeline = QualitySettings.renderPipeline ? QualitySettings.renderPipeline : GraphicsSettings.currentRenderPipeline;
                Debug.Log($"[PostcardProofMasks] {queue.Count} shot(s) -> {outDir}; pipeline asset '{(pipeline ? pipeline.name : "none")}', quality level {QualitySettings.GetQualityLevel()}");
                shotIndex = 0; currentHole = -1; wait = 1; return;
            }
            if (--wait > 0) return;
            if (shotIndex >= queue.Count) { Finish(staleShots > 0 ? 2 : 0); return; }
            var s = queue[shotIndex];
            if (s.Hole != currentHole) { LoadHole(s.Hole); return; }
            if (!Settled(out _) && ++settleTicks < 3000) return;
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
        }

        static bool Settled(out string why)
        {
            int frames = Time.frameCount - startFrame, views = Object.FindObjectsByType<HoleView>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            why = $"{frames} player frame(s) since StartHole, {views} HoleView(s) alive";
            return frames >= 5 && views == 1;
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

        static void SaveMask(string file, bool[] mask)
        {
            var t = new Texture2D(W, H, TextureFormat.RGB24, false);
            var px = new Color32[mask.Length];
            for (int i = 0; i < mask.Length; i++) px[i] = mask[i] ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 255);
            t.SetPixels32(px); t.Apply();
            File.WriteAllBytes(Path.Combine(outDir, file), t.EncodeToPNG());
            Object.DestroyImmediate(t);
        }

        static void Take(Shot s)
        {
            if (!Settled(out var why)) { staleShots++; Debug.LogError($"[PostcardProofMasks] {s.Name}: NOT settled ({why})"); }
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
            var full = LookPixels.Render(cam, rt, tex);
            File.WriteAllBytes(Path.Combine(outDir, s.Name + ".full.png"), tex.EncodeToPNG());

            var view = HoleView.Current;
            var model = view ? view.transform.Find("Course model") : null;
            var rends = model ? model.GetComponentsInChildren<Renderer>(false).Where(r => r.enabled && r.gameObject.activeInHierarchy).ToArray() : new Renderer[0];
            var sb = new StringBuilder();
            sb.Append("    { \"name\": \"").Append(s.Name).Append("\", \"hole\": ").Append(s.Hole).Append(", \"renderers\": ").Append(rends.Length).Append(", \"groups\": {");
            bool first = true;
            foreach (var g in Groups)
            {
                var list = rends.Where(g.Match).ToArray();
                int count = 0, nr = list.Length;
                if (nr > 0)
                {
                    var oldEnabled = new bool[nr]; var oldShadow = new ShadowCastingMode[nr];
                    for (int i = 0; i < nr; i++)
                    {
                        oldEnabled[i] = list[i].enabled; oldShadow[i] = list[i].shadowCastingMode;
                        if (g.ShadowsOnly) list[i].shadowCastingMode = ShadowCastingMode.ShadowsOnly; else list[i].enabled = false;
                    }
                    var dry = LookPixels.Render(cam, rt, tex);
                    if (g.Name == "smoke" || g.Name == "lava") File.WriteAllBytes(Path.Combine(outDir, $"{s.Name}.{g.Name}_dry.png"), tex.EncodeToPNG());      // the frame without the group: the smoke's alpha / the lava's contribution is full - dry
                    for (int i = 0; i < nr; i++) { list[i].enabled = oldEnabled[i]; list[i].shadowCastingMode = oldShadow[i]; }
                    var mask = LookPixels.Diff(full, dry, g.Threshold, out count);
                    SaveMask($"{s.Name}.{g.Name}.png", mask);
                }
                if (!first) sb.Append(", "); first = false;
                sb.Append('"').Append(g.Name).Append("\": { \"renderers\": ").Append(nr).Append(", \"pixels\": ").Append(count).Append(", \"threshold\": ").Append(g.Threshold).Append(" }");
            }
            sb.Append(" }");
            // ID render: exact per-material masks
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                var idMats = new Dictionary<string, Material>();
                for (int i = 0; i < Categories.Length; i++)
                {
                    var im = new Material(shader) { name = "ID_" + Categories[i], hideFlags = HideFlags.HideAndDontSave };
                    im.SetColor("_BaseColor", IdColor(i)); im.color = IdColor(i);
                    if (im.HasProperty("_Cull")) im.SetFloat("_Cull", 0);
                    if (im.HasProperty("_Surface")) im.SetFloat("_Surface", 0);
                    idMats[Categories[i]] = im;
                }
                var saved = new Dictionary<Renderer, Material[]>();
                var cats = new int[Categories.Length];
                foreach (var r in rends)
                {
                    var old = r.sharedMaterials; saved[r] = old;
                    var swapped = new Material[old.Length];
                    for (int k = 0; k < old.Length; k++) swapped[k] = idMats[CategoryOf(r, old[k])];
                    r.sharedMaterials = swapped;
                }
                bool fog = RenderSettings.fog; var clear = cam.clearFlags; var bg = cam.backgroundColor;
                RenderSettings.fog = false; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black;
                var ids = LookPixels.Render(cam, rt, tex);
                File.WriteAllBytes(Path.Combine(outDir, s.Name + ".id.png"), tex.EncodeToPNG());
                RenderSettings.fog = fog; cam.clearFlags = clear; cam.backgroundColor = bg;
                foreach (var kv in saved) kv.Key.sharedMaterials = kv.Value;
                foreach (var m in idMats.Values) Object.DestroyImmediate(m);
            }
            // the game ball: how many of the pixels it should cover are really its (path / grass in front of it would remove them)
            if (ballT && ballT.gameObject.activeInHierarchy)
            {
                var br = ballT.GetComponentsInChildren<Renderer>(false).Where(r => r.enabled).ToArray();
                if (br.Length > 0)
                {
                    var b = br[0].bounds;
                    float dist = Vector3.Dot(b.center - cam.transform.position, cam.transform.forward);
                    float radius = dist > .01f ? b.extents.x / (dist * Mathf.Tan(cam.fieldOfView * .5f * Mathf.Deg2Rad)) * (H * .5f) : 0;      // projected radius in render-target pixels
                    var oldMode = br.Select(r => r.shadowCastingMode).ToArray();
                    foreach (var r in br) r.shadowCastingMode = ShadowCastingMode.ShadowsOnly;           // the ball itself disappears, its shadow stays: the diff is the ball's own pixels
                    var dry = LookPixels.Render(cam, rt, tex);
                    for (int i = 0; i < br.Length; i++) br[i].shadowCastingMode = oldMode[i];
                    var mask = LookPixels.Diff(full, dry, 30, out int count);
                    SaveMask($"{s.Name}.ball.png", mask);
                    var sp = cam.WorldToViewportPoint(b.center);
                    sb.Append(", \"ball\": { \"centre_px\": [").Append((sp.x * W).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)).Append(", ").Append((sp.y * H).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture))
                      .Append("], \"distance_yd\": ").Append(dist.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)).Append(", \"radius_px\": ").Append(radius.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)).Append(", \"expected_px\": ")
                      .Append((Mathf.PI * radius * radius).ToString("0", System.Globalization.CultureInfo.InvariantCulture)).Append(", \"visible_px\": ").Append(count).Append(" }");
                }
            }
            sb.Append(", \"id_colors\": {").Append(string.Join(", ", Categories.Select((c, i) => "\"" + c + "\": [" + IdColor(i).r + ", " + IdColor(i).g + ", " + IdColor(i).b + "]").ToArray())).Append(" }");
            sb.Append(", \"camera\": { \"pos\": [").Append(string.Join(", ", new[] { cam.transform.position.x, cam.transform.position.y, cam.transform.position.z }.Select(v => v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)).ToArray()))
              .Append("], \"fov\": ").Append(cam.fieldOfView.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)).Append(" } }");
            meta.Add(sb.ToString());
            Debug.Log($"[PostcardProofMasks] {s.Name} done");
            cam.fieldOfView = 60;
        }

        static void Finish(int code)
        {
            var pipeline = QualitySettings.renderPipeline ? QualitySettings.renderPipeline : GraphicsSettings.currentRenderPipeline;
            string json = "{\n  \"tool\": \"PostcardProofMasks\",\n  \"unity\": \"" + Application.unityVersion + "\",\n  \"pipeline\": \"" + (pipeline ? pipeline.name : "none") + "\",\n  \"size\": [" + W + ", " + H + "],\n  \"shots\": [\n" +
                string.Join(",\n", meta.ToArray()) + "\n  ]\n}\n";
            if (meta.Count > 0) File.WriteAllText(Path.Combine(outDir, "masks_meta.json"), json);
            SessionState.SetBool(Flag, false);
            if (Application.isBatchMode) EditorApplication.Exit(code); else EditorApplication.isPlaying = false;
        }
    }
}
