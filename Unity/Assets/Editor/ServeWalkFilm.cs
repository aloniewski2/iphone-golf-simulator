using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using GolfArcade.Game;
using GolfArcade.Tennis;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GolfArcade.EditorTools
{
    // SW_OUT=<dir> SW_FEMALE=0|1 SW_LEFTY=0|1 SW_SIDE=deuce|ad
    // Unity -batchmode -projectPath <isolated clone> -executeMethod GolfArcade.EditorTools.ServeWalkFilm.Run
    // No -quit / -nographics. Measurement runs after the real game's LateUpdate, at 60 Hz.
    [InitializeOnLoad]
    public static class ServeWalkFilm
    {
        const string Flag = "ServeWalkFilm.Active";
        static ServeWalkFilm() { EditorApplication.update += Bootstrap; }
        public static string Env(string key, string fallback) => Environment.GetEnvironmentVariable(key) ?? fallback;
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Tennis.unity");
            SessionState.SetBool(Flag, true);
            EditorApplication.isPlaying = true;
        }
        static void Bootstrap()
        {
            if (!SessionState.GetBool(Flag, false) || !EditorApplication.isPlaying) return;
            var game = Object.FindFirstObjectByType<TennisGame>();
            if (!game || !game.Initialized || Object.FindFirstObjectByType<ServeWalkRecorder>()) return;
            try { new GameObject("Serve walk proof recorder").AddComponent<ServeWalkRecorder>().Initialize(game); }
            catch (Exception e) { Debug.LogException(e); Finish(1); }
        }
        public static void Finish(int code)
        {
            SessionState.SetBool(Flag, false); Time.captureFramerate = 0;
            if (Application.isBatchMode) EditorApplication.Exit(code);
            else EditorApplication.isPlaying = false;
        }
    }

    [Serializable] public sealed class ProofSourceRecord { public string projectPath, inputPath; public string[] sources, sha256; }

    [DefaultExecutionOrder(2000)]
    public sealed class ServeWalkRecorder : MonoBehaviour
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        TennisGame game; HeroTennisDriver driver; MatchHeroLook hero; Camera cam;
        Surface trunk, whole; Mesh baked; StreamWriter trace;
        string dir, segment; int frame = -60, picture; bool controls, tossed, swung, done;
        bool female, lefty, ad, keyboard, opponent, adapter, controlOnly, images;
        string resetReason = "", ballExclusion = ""; int resetAt = -999, actualTeleportAt = -999; Vector3 lastMeasuredRoot; bool haveMeasuredRoot;
        float lastTime, worst, worstAll; int worstFrame; string worstBone;
        Vector3 lastHand, lastVelocity; bool haveLast; float lengthUpper, lengthLower;
        readonly List<Vector3> verts = new List<Vector3>();
        List<int> arm, pelvis; int[] dominant; string[] boneNames;
        static readonly CultureInfo CI = CultureInfo.InvariantCulture;
        static float Field(object o, string name) => Convert.ToSingle(o.GetType().GetField(name, Private).GetValue(o));
        static bool Method(object o, string name) => (bool)o.GetType().GetMethod(name, Private).Invoke(o, null);
        static void SetField(object o, string name, object value) => o.GetType().GetField(name, Private).SetValue(o, value);
        static string F(float n) => n.ToString("0.000000", CI);
        static string V(Vector3 p) => F(p.x) + "," + F(p.y) + "," + F(p.z);
        Transform Bone(HumanBodyBones b) => hero.Bone(b);
        public void Initialize(TennisGame g)
        {
            game = g; female = ServeWalkFilm.Env("SW_FEMALE", "0") == "1";
            lefty = ServeWalkFilm.Env("SW_LEFTY", "0") == "1"; ad = ServeWalkFilm.Env("SW_SIDE", "deuce") == "ad";
            keyboard = ServeWalkFilm.Env("SW_KEYBOARD", "0") == "1";
            opponent = ServeWalkFilm.Env("SW_ROLE", "player") == "opponent";
            adapter = ServeWalkFilm.Env("SW_INPUT_PATH", "fixture") == "adapter";
            controlOnly = ServeWalkFilm.Env("SW_CONTROL_ONLY", "0") == "1"; images = ServeWalkFilm.Env("SW_IMAGES", "1") == "1";
            dir = Path.GetFullPath(ServeWalkFilm.Env("SW_OUT", "../work/serve-walk-arm/before/male-rh-deuce"));
            Directory.CreateDirectory(dir);
            string[] proofSources = { "Scripts/Tennis/HeroTennisDriver.cs", "Scripts/Tennis/TennisActor.cs", "Scripts/Tennis/TennisServeRoutine.cs", "Scripts/Tennis/TennisGame.cs", "Editor/ServeWalkFilm.cs" };
            string[] hashes = proofSources.Select(f => { using (var hash = System.Security.Cryptography.SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(Path.Combine(Application.dataPath, f)))).Replace("-", "").ToLowerInvariant(); }).ToArray();
            File.WriteAllText(Path.Combine(dir, "source.json"), JsonUtility.ToJson(new ProofSourceRecord { projectPath = Path.GetDirectoryName(Application.dataPath), inputPath = adapter ? "SetLateralInput" : keyboard ? "keyboard" : "fixture_ServeNudge", sources = proofSources, sha256 = hashes }, true));
            Time.captureFramerate = 60; Time.timeScale = 1;
            typeof(NativeSportsSession).GetProperty("Left").SetValue(null, lefty);
            game.NativeControlled = !keyboard && !adapter; game.AutoPlay = false; game.ManualSimulation = false;
            var presentation = game.GetComponent<TennisPresentation>(); if (presentation) presentation.Finish();
            game.SelectCharacter(opponent ? !female : female);
            string rival = opponent ? (female ? "Nadia" : "Viktor") : (female ? "Viktor" : "Nadia");
            game.ConfigureMatch(TennisGame.Mode.Campaign, rival, rival, "ROUND");
            if (presentation) presentation.Finish();
            ConfigurePoint();
            if (adapter)
            {
                // Test adapter calls the production input-handler method. Keep the physical-keyboard
                // fallback from overwriting it by holding the existing motion-control guard active.
                var phone = game.GetComponent<TennisPhoneInput>();
                if (phone) { phone.enabled = false; SetField(phone, "clock", 0d); SetField(phone, "lastSampleAt", 0d); }
            }
            cam = game.GameplayCamera ? game.GameplayCamera : Camera.main;
            driver = (opponent ? game.Opponent : game.Player).GetComponentInChildren<HeroTennisDriver>(); hero = driver.matchLook;
            baked = new Mesh();
            BuildSurfaces();
            if (keyboard) EditorApplication.delayCall += () => EditorWindow.GetWindow(AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("UnityEditor.GameView")).First(t => t != null)).Focus();
            var up = Bone(HumanBodyBones.LeftUpperArm); var lo = Bone(HumanBodyBones.LeftLowerArm); var wrist = Bone(HumanBodyBones.LeftHand);
            lengthUpper = Vector3.Distance(up.position, lo.position); lengthLower = Vector3.Distance(lo.position, wrist.position);
            trace = new StreamWriter(Path.Combine(dir, "trace.csv"));
            var header = "frame,picture,t,segment,phase,routine_time,clip_time,prepare,walkYaw,serveYaw,speed,walkMode,tossWeight,deepest_mm,inside_pct,inside_count,arm_count,worst_vertex,worst_bone,ball_palm_m,game_visible,front_visible,ball_inside_body,elbow_flexion,upper_length_m,lower_length_m,length_delta_mm,hand_speed,hand_speed_change,hand_step_m,hip_lateral_m";
            foreach (var name in new[] { "ball", "shoulder", "elbow", "wrist", "palm", "hips", "chest", "left_thigh", "right_thigh", "strings", "root", "target" }) header += "," + name + "_x," + name + "_y," + name + "_z";
            foreach (var name in new[] { "upper", "lower", "hand" }) header += "," + name + "_qx," + name + "_qy," + name + "_qz," + name + "_qw";
            header += ",palm_trunk_clearance_m,wrist_above_hips_m,carry_blend,side_visible,in_hand,ball_exclusion,reset_reason,root_frame,target_frame,unity_frame,input_path,supported,pelvis_top_y,wrist_above_belt_m,pelvis_top_above_hips_m,pelvis_vertices,signed_ttc,swinging,kind,contact_age,camera_x,camera_y,camera_z,runtime_belt_y,runtime_belt_error_mm";
            trace.WriteLine(header); trace.Flush();
            File.WriteAllText(Path.Combine(dir, "input.json"), $"{{\"role\":\"{(opponent ? "opponent" : "player")}\",\"female\":{female.ToString().ToLowerInvariant()},\"requested_lefty\":{lefty.ToString().ToLowerInvariant()},\"actual_lefty\":{driver.actor.LeftHanded.ToString().ToLowerInvariant()},\"side\":\"{(ad ? "ad" : "deuce")}\",\"keyboard\":{keyboard.ToString().ToLowerInvariant()}}}");
            Debug.Log($"[ServeWalkFilm] actual actor lefty={driver.actor.LeftHanded}; match driver tossing side=Left (current code); arm verts={arm.Count}; trunk triangles={trunk.TriangleCount}; closure={trunk.ClosureReport}");
        }
        void ConfigurePoint()
        {
            var match = game.Match; match.PlayerPoints = ad ? 1 : 0; match.OpponentPoints = 0; match.PlayerServes = !opponent;
            SetField(game, "match", match); game.Refeed(); resetReason = "fixture_point_reset"; resetAt = frame;
            if (opponent)
            {
                float mark = TennisRules.ServerStanceX(false, game.Match.DeuceCourt);
                game.Opponent.transform.position = new Vector3(mark + (controlOnly ? 0 : 2.5f), game.Opponent.transform.position.y, TennisRules.ServeDepth);
                return;
            }
            // Fixture only: legal starting location, safely inside either service half. Movement thereafter is ServeNudge only.
            float x = ad ? -2.7f : 2.7f;
            game.Player.transform.position = new Vector3(x, game.Player.transform.position.y, -TennisRules.ServeDepth);
            SetField(game, "serveX", x); SetField(game, "serveWalkSpeed", 0f);
        }
        void BuildSurfaces()
        {
            var mesh = hero.body.sharedMesh; var weights = mesh.boneWeights; var bones = hero.body.bones;
            boneNames = bones.Select(b => b ? b.name : "null").ToArray();
            bool ArmBone(int b) => boneNames[b].StartsWith("Left") && (boneNames[b].Contains("Arm") || boneNames[b].Contains("Hand") || boneNames[b].Contains("Thumb") || boneNames[b].Contains("Index") || boneNames[b].Contains("Middle") || boneNames[b].Contains("Ring") || boneNames[b].Contains("Little"));
            bool TrunkBone(int b) => new[] { "Hips", "Spine", "Chest", "UpperChest", "LeftUpperLeg", "RightUpperLeg" }.Contains(boneNames[b]);
            arm = new List<int>(); pelvis = new List<int>(); dominant = new int[weights.Length]; var trunkVert = new bool[weights.Length];
            for (int i = 0; i < weights.Length; i++)
            {
                var w = weights[i]; int[] bi = { w.boneIndex0, w.boneIndex1, w.boneIndex2, w.boneIndex3 }; float[] bw = { w.weight0, w.weight1, w.weight2, w.weight3 };
                float aw = 0, tw = 0, max = -1;
                for (int k = 0; k < 4; k++) { if (ArmBone(bi[k])) aw += bw[k]; if (TrunkBone(bi[k])) tw += bw[k]; if (bw[k] > max) { max = bw[k]; dominant[i] = bi[k]; } }
                if (aw > .5f) arm.Add(i); float hw = 0; for (int k = 0; k < 4; k++) if (boneNames[bi[k]] == "Hips") hw += bw[k]; if (hw > .5f) pelvis.Add(i); trunkVert[i] = tw > .5f;
            }
            var triangles = mesh.triangles; var selected = new List<int>();
            for (int i = 0; i < triangles.Length; i += 3)
                if (trunkVert[triangles[i]] && trunkVert[triangles[i + 1]] && trunkVert[triangles[i + 2]]) selected.AddRange(new[] { triangles[i], triangles[i + 1], triangles[i + 2] });
            trunk = new Surface(mesh.vertices, selected.ToArray(), true);
            whole = new Surface(mesh.vertices, triangles, false);
            File.WriteAllText(Path.Combine(dir, "measurement.txt"),
                $"Source: MatchHeroLook.body, BakeMesh each rendered frame. Bone sums > 0.5.\nARM=Left upper/lower arm, hand, all fingers ({arm.Count} vertices).\nTRUNK=Hips, Spine, Chest, UpperChest, both UpperLeg.\nTrunk triangles require all three vertices in TRUNK. Weld coincident bind vertices at 1 micrometre, cap partition boundary loops.\n{trunk.ClosureReport}\nInside: majority parity of three non-axis rays through closed trunk. Depth: closest ORIGINAL trunk triangle, excluding synthetic caps. BVH rebuilt once and refitted to real skin per frame.\nVisibility: ray from ball towards camera, excluding the first ball-radius distance, against full baked body; frustum checked.\nCurrent HeroTennisDriver ALWAYS solves Left arm even when actor.LeftHanded=true; recorded faithfully, no simulation mirroring.\nBall-palm uses 0.7 between wrist and middle proximal, same as production.\n");
        }
        void Update()
        {
            if (done || !game) return;
            frame++;
            if (frame < 0) { game.ServeNudge = 0; if (adapter) game.SetLateralInput(0, false); return; }
            resetReason = frame - resetAt <= 1 ? "fixture_point_reset" : "";
            float t = frame / 60f;
            if (keyboard)
            {
                segment = Input.GetKey(KeyCode.A) ? "keyboard_left" : Input.GetKey(KeyCode.D) ? "keyboard_right" : "keyboard_plant";
                if (t >= float.Parse(ServeWalkFilm.Env("SW_DURATION", "15"), CI)) Complete();
                return; // The existing TennisGame.Update keyboard path writes MoveInput; no injected ServeNudge.
            }
            if (controlOnly)
            {
                segment = "control"; game.ServeNudge = 0;
                if (!opponent && !tossed && t >= 3.7f) { game.Toss(1); tossed = true; }
                if (!opponent && !swung && game.Flow == TennisGame.Phase.PlayerServeToss && Field(game, "phaseTimer") >= TennisRules.ServeApex - .08f) { game.RequestSwing(.75f); swung = true; }
                if (t >= float.Parse(ServeWalkFilm.Env("SW_DURATION", "8"), CI)) Complete();
                return;
            }
            if (opponent)
            {
                if (frame == 0 || frame == 300 || frame == 600)
                {
                    ConfigurePoint();
                    if (frame >= 300)
                    {
                        var p = game.Opponent.transform.position; p.x = TennisRules.ServerStanceX(false, game.Match.DeuceCourt) - (frame == 300 ? 2.5f : 0); game.Opponent.transform.position = p;
                    }
                }
                segment = frame < 300 ? "opponent_walk_left" : frame < 600 ? "opponent_walk_right" : "control";
                if (t >= 15) Complete();
                return;
            }
            segment = t < .5f ? "stand" : t < 2 ? "walk_left" : t < 2.8f ? "plant_left" : t < 4.3f ? "walk_right" : t < 5.1f ? "plant_right" : t < 5.35f ? "tap_left" : t < 6.15f ? "plant_tap_left" : t < 6.4f ? "tap_right" : t < 9.2f ? "plant_tap_right" : "serve";
            float input = segment == "walk_left" || segment == "tap_left" ? -1 : segment == "walk_right" || segment == "tap_right" ? 1 : 0;
            if (adapter) { game.ServeNudge = 0; game.SetLateralInput(input, false); }
            else game.ServeNudge = input;
            if (!controls && !tossed && t >= 9.2f) { game.Toss(1); tossed = true; }
            if (!controls && !swung && game.Flow == TennisGame.Phase.PlayerServeToss && Field(game, "phaseTimer") >= TennisRules.ServeApex - .08f) { game.RequestSwing(.75f); swung = true; }
            if (t >= 12.8f && !controls)
            {
                controls = true; tossed = swung = false; game.Refeed(); resetAt = frame; resetReason = "fixture_point_reset";
                SetField(game, "serveX", game.Player.transform.position.x); SetField(game, "serveWalkSpeed", 0f);
            }
            if (controls)
            {
                segment = "control"; game.ServeNudge = 0;
                if (!tossed && t >= 16.5f) { game.Toss(1); tossed = true; }
                if (!swung && game.Flow == TennisGame.Phase.PlayerServeToss && Field(game, "phaseTimer") >= TennisRules.ServeApex - .08f) { game.RequestSwing(.75f); swung = true; }
            }
            if (t >= float.Parse(ServeWalkFilm.Env("SW_DURATION", "20"), CI)) Complete();
        }
        void LateUpdate()
        {
            if (done || frame < 0 || !hero) return;
            try { Record(); } catch (Exception e) { Debug.LogException(e); Complete(1); }
        }
        bool Held(float t)
        {
            float windStart = TennisRules.ServeTossDelay - TennisServeRoutine.WindUp;
            if (t >= windStart) return t < TennisRules.ServeTossDelay;
            float h = TennisServeRoutine.Hold(driver.actor).y - .08f - driver.actor.transform.position.y;
            float fall = (-3.5f + Mathf.Sqrt(3.5f * 3.5f + 2 * 9.81f * Mathf.Max(.1f, h - TennisRules.BallRadius))) / 9.81f;
            float rise = (3.5f + 9.81f * fall) * .7f / 9.81f, cycle = .06f + fall + rise + .18f;
            float b = t - Mathf.Max(0, windStart - TennisServeRoutine.Gather - TennisServeRoutine.Bounces * cycle);
            int n = Mathf.FloorToInt(b / cycle); float u = b - n * cycle;
            return b < 0 || n >= TennisServeRoutine.Bounces || u < .06f || u >= .06f + fall + rise;
        }
        object Diagnostic(string key) => driver.GetType().GetProperty(key)?.GetValue(driver);
        void Record()
        {
            Vector3 measuredRoot = (opponent ? game.Opponent : game.Player).transform.position;
            if (opponent && frame == resetAt && (!haveMeasuredRoot || Vector3.Distance(measuredRoot, lastMeasuredRoot) > .3f)) actualTeleportAt = frame;
            resetReason = opponent && frame - actualTeleportAt <= 1 ? "fixture_opponent_root_teleport" : "";
            lastMeasuredRoot = measuredRoot; haveMeasuredRoot = true;
            hero.body.BakeMesh(baked); baked.GetVertices(verts);
            var world = new Vector3[verts.Count]; for (int i = 0; i < world.Length; i++) world[i] = hero.body.transform.TransformPoint(verts[i]);
            trunk.Refit(world); whole.Refit(world);
            float depth = 0; int inside = 0, worstVertex = -1;
            foreach (int i in arm)
            {
                if (!trunk.Inside(world[i])) continue;
                float d = trunk.Distance(world[i]); if (d < .000001f) continue;
                inside++; if (d > depth) { depth = d; worstVertex = i; }
            }
            var up = Bone(HumanBodyBones.LeftUpperArm); var lo = Bone(HumanBodyBones.LeftLowerArm); var hand = Bone(HumanBodyBones.LeftHand); var middle = Bone(HumanBodyBones.LeftMiddleProximal);
            Vector3 palm = middle ? Vector3.Lerp(hand.position, middle.position, .7f) : hand.position;
            float upper = Vector3.Distance(up.position, lo.position), lower = Vector3.Distance(lo.position, hand.position);
            var velocity = haveLast ? (hand.position - lastHand) * 60 : Vector3.zero;
            float speedChange = haveLast ? Mathf.Abs(velocity.magnitude - lastVelocity.magnitude) : 0;
            float step = haveLast ? Vector3.Distance(hand.position, lastHand) : 0;
            float elbow = 180 - Vector3.Angle(up.position - lo.position, hand.position - lo.position);
            var server = opponent ? game.Opponent : game.Player;
            var root = server.transform; var frontFrom = root.position + root.forward * 3.4f + Vector3.up * 1.25f;
            bool Visible(Vector3 from) { var ray = from - game.BallPosition; float len = ray.magnitude; return !whole.Hit(new Ray(game.BallPosition + ray.normalized * (TennisRules.BallRadius + .001f), ray.normalized), len - TennisRules.BallRadius); }
            var screen = cam.WorldToViewportPoint(game.BallPosition);
            bool gameVisible = screen.z > 0 && screen.x >= 0 && screen.x <= 1 && screen.y >= 0 && screen.y <= 1 && Visible(cam.transform.position);
            string wb = worstVertex >= 0 ? boneNames[dominant[worstVertex]] : "none";
            float routine = Field(game, "windup") < 0 ? Mathf.Min(Mathf.Repeat(Field(game, "phaseTimer"), TennisRules.ServeTossDelay - TennisServeRoutine.WindUp + .6f), TennisRules.ServeTossDelay - TennisServeRoutine.WindUp - .001f) : TennisRules.ServeTossDelay - TennisServeRoutine.WindUp + TennisServeRoutine.WindUp * Mathf.Clamp01(Field(game, "windup") / TennisRules.ServeWindUp);
            if (opponent) routine = Field(game, "serveTimer");
            else if (Field(game, "serveWalkSpeed") > .2f) routine = 0;
            var parts = new List<string> { frame.ToString(), (frame % 2 == 0 ? picture : -1).ToString(), F(frame / 60f), segment, game.Flow.ToString(), F(routine), F(driver.PlayingClipTime), F((opponent ? game.Opponent : game.Player).PrepareAmount), F(Field(driver, "walkYaw")), F(Field(driver, "serveYaw")), F((opponent ? game.Opponent : game.Player).Speed), Method(driver, "WalkMode") ? "1" : "0", F(Field(driver, "tossWeight")), F(depth * 1000), F(100f * inside / arm.Count), inside.ToString(), arm.Count.ToString(), worstVertex.ToString(), wb, F(Vector3.Distance(game.BallPosition, palm)), gameVisible ? "1" : "0", Visible(frontFrom) ? "1" : "0", whole.Inside(game.BallPosition) ? "1" : "0", F(elbow), F(upper), F(lower), F(1000 * Mathf.Max(Mathf.Abs(upper - lengthUpper), Mathf.Abs(lower - lengthLower))), F(velocity.magnitude), F(speedChange), F(step), F(Mathf.Abs(driver.transform.InverseTransformPoint(palm).x - driver.transform.InverseTransformPoint(Bone(HumanBodyBones.Hips).position).x)) };
            foreach (var v in new[] { game.BallPosition, up.position, lo.position, hand.position, palm, Bone(HumanBodyBones.Hips).position, Bone(HumanBodyBones.Chest).position, Bone(HumanBodyBones.LeftUpperLeg).position, Bone(HumanBodyBones.RightUpperLeg).position, driver.StringCentre, root.position, server.TossReachTarget }) parts.Add(V(v));
            foreach (var b in new[] { up, lo, hand }) { var q = Quaternion.Inverse(root.rotation) * b.rotation; parts.Add(F(q.x) + "," + F(q.y) + "," + F(q.z) + "," + F(q.w)); }
            parts.Add(F(trunk.Distance(palm) * (trunk.Inside(palm) ? -1 : 1)));
            parts.Add(F(hand.position.y - Bone(HumanBodyBones.Hips).position.y));
            var carryField = driver.GetType().GetField("matchCarryT", Private);
            parts.Add(F(carryField != null ? (float)carryField.GetValue(driver) : 0));
            bool pre = opponent ? game.Flow == TennisGame.Phase.OpponentServe && routine < TennisRules.ServeTossDelay : game.Flow == TennisGame.Phase.PlayerServeHold;
            bool held = pre && Held(routine);
            var state = Diagnostic("ServeBallInHand"); if (state != null && pre) held = (bool)state;
            ballExclusion = frame == resetAt ? "refeed_before_rebind" : !pre ? "released_or_unbound" : routine >= TennisRules.ServeTossDelay ? "release_boundary" : !held ? "intentional_bounce_flight" : "";
            if (ballExclusion.Length > 0) held = false;
            parts.Add(Visible(root.position - root.right * 3.3f + Vector3.up * 1.2f) ? "1" : "0");
            parts.Add(held ? "1" : "0"); parts.Add(ballExclusion); parts.Add(resetReason);
            parts.Add((Diagnostic("ServeRootFrame") ?? -1).ToString()); parts.Add((Diagnostic("ServeTargetFrame") ?? -1).ToString());
            parts.Add(Time.frameCount.ToString()); parts.Add(adapter ? "SetLateralInput" : keyboard ? "keyboard" : "fixture_ServeNudge"); parts.Add(lefty ? "0" : "1");
            float pelvisTop = pelvis.Max(i => world[i].y);
            parts.Add(F(pelvisTop)); parts.Add(F(hand.position.y - pelvisTop)); parts.Add(F(pelvisTop - Bone(HumanBodyBones.Hips).position.y)); parts.Add(pelvis.Count.ToString());
            parts.Add(F(server.SignedTimeToContact)); parts.Add(server.Swinging ? "1" : "0"); parts.Add(server.Kind.ToString()); parts.Add(F(server.ContactAge)); parts.Add(V(cam.transform.position));
            var runtimeBelt = Diagnostic("ServeBeltHeight"); float runtimeY = runtimeBelt != null ? (float)runtimeBelt : -99; parts.Add(F(runtimeY)); parts.Add(F(runtimeBelt != null ? (runtimeY - pelvisTop) * 1000 : -99999));
            trace.WriteLine(string.Join(",", parts)); trace.Flush();
            if (depth > worst && segment.Contains("left")) { worst = depth; worstFrame = frame; worstBone = wb; File.WriteAllText(Path.Combine(dir, "worst.json"), $"{{\"frame\":{frame},\"depth_mm\":{F(depth * 1000)},\"inside_pct\":{F(100f * inside / arm.Count)},\"bone\":\"{wb}\",\"vertex\":{worstVertex}}}"); }
            if (images && depth > worstAll)
            {
                worstAll = depth;
                Shot("worst-front", frame.ToString("00000"), frontFrom, root.position + Vector3.up * .98f, 480);
                Shot("worst-left", frame.ToString("00000"), root.position - root.right * 3.3f + Vector3.up * 1.2f, root.position + Vector3.up * .98f, 480);
            }
            if (images && frame % 2 == 0)
            {
                string id = (picture++).ToString("00000");
                GameCapture.Save(Path.Combine(dir, "game_" + id + ".png"), 960, 540);
                Shot("front", id, frontFrom, root.position + Vector3.up * .98f, 480);
                Shot("left", id, root.position - root.right * 3.3f + Vector3.up * 1.2f, root.position + Vector3.up * .98f, 480);
                Shot("top", id, root.position + Vector3.up * 4f + root.forward * .001f, root.position + Vector3.up * .7f, 480);
            }
            if ((images || ServeWalkFilm.Env("SW_PLANT_SNAPSHOT", "0") == "1") && frame == 547)
            {
                GameCapture.Save(Path.Combine(dir, "plant547-game.png"), 960, 540);
                Shot("plant547-front", "00547", frontFrom, root.position + Vector3.up * .98f, 480);
                Shot("plant547-side", "00547", root.position - root.right * 3.3f + Vector3.up * 1.2f, root.position + Vector3.up * .98f, 480);
            }
            lastHand = hand.position; lastVelocity = velocity; haveLast = true; lastTime = Time.time;
            if (frame % 120 == 0) Debug.Log($"[ServeWalkFilm] f={frame} stage={segment} depth={depth * 1000:F3}mm inside={inside}/{arm.Count} speed={game.LateralSpeed:F2} yaw={Field(driver, "walkYaw"):F1}");
        }
        void Shot(string view, string id, Vector3 from, Vector3 at, int size)
        {
            var p = cam.transform.position; var r = cam.transform.rotation; float fov = cam.fieldOfView;
            var previous = cam.targetTexture; var active = RenderTexture.active;
            var rt = RenderTexture.GetTemporary(size, size, 24); var tex = new Texture2D(size, size, TextureFormat.RGB24, false);
            try { cam.transform.SetPositionAndRotation(from, Quaternion.LookRotation(at - from)); cam.fieldOfView = 38; cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, size, size), 0, 0); tex.Apply(); File.WriteAllBytes(Path.Combine(dir, view + "_" + id + ".png"), tex.EncodeToPNG()); }
            finally { cam.targetTexture = previous; cam.transform.SetPositionAndRotation(p, r); cam.fieldOfView = fov; RenderTexture.active = active; RenderTexture.ReleaseTemporary(rt); Object.DestroyImmediate(tex); }
        }
        void Complete(int code = 0)
        {
            if (done) return; done = true; trace?.Dispose();
            File.WriteAllText(Path.Combine(dir, "completed.json"), $"{{\"frames\":{frame},\"worst_left_mm\":{F(worst * 1000)},\"worst_frame\":{worstFrame},\"bone\":\"{worstBone}\",\"exit_code\":{code}}}");
            if (keyboard && ServeWalkFilm.Env("SW_EXIT", "0") == "1") { trace?.Dispose(); EditorApplication.Exit(code); return; }
            Debug.Log($"[ServeWalkFilm] complete f={frame} worst left={worst * 1000:F3}mm @ {worstFrame}"); ServeWalkFilm.Finish(code);
        }

        // Refit BVH over the actual deforming triangle surface, with parity ray tests and exact nearest triangles.
        sealed class Surface
        {
            struct Tri { public int a, b, c; public bool cap; }
            sealed class Node { public Bounds bounds; public Node left, right; public int start, count; }
            readonly List<Tri> tris = new List<Tri>(); readonly List<int[]> loops = new List<int[]>();
            readonly int sourceCount; readonly int[] canonical; Vector3[] points; Node root;
            public int TriangleCount => tris.Count;
            public string ClosureReport { get; private set; }
            public Surface(Vector3[] bind, int[] indices, bool close)
            {
                sourceCount = bind.Length; canonical = new int[bind.Length]; var welded = new Dictionary<(int, int, int), int>();
                for (int i = 0; i < bind.Length; i++) { var p = bind[i]; var key = (Mathf.RoundToInt(p.x * 1000000), Mathf.RoundToInt(p.y * 1000000), Mathf.RoundToInt(p.z * 1000000)); if (!welded.TryGetValue(key, out var id)) welded[key] = id = i; canonical[i] = id; }
                var edges = new Dictionary<(int, int), int>(); var direction = new Dictionary<(int, int), (int, int)>();
                void Edge(int a, int b) { var key = (Math.Min(a, b), Math.Max(a, b)); edges.TryGetValue(key, out int n); edges[key] = n + 1; direction[key] = (a, b); }
                for (int i = 0; i < indices.Length; i += 3) { int a = canonical[indices[i]], b = canonical[indices[i + 1]], c = canonical[indices[i + 2]]; if (a == b || b == c || a == c) continue; tris.Add(new Tri { a = a, b = b, c = c }); Edge(a, b); Edge(b, c); Edge(c, a); }
                int boundaryCount = edges.Count(e => e.Value == 1), unresolved = 0;
                if (close)
                {
                    var next = new Dictionary<int, List<int>>(); var remaining = new HashSet<(int, int)>();
                    foreach (var e in edges.Where(e => e.Value == 1)) { var d = direction[e.Key]; if (!next.TryGetValue(d.Item1, out var list)) next[d.Item1] = list = new List<int>(); list.Add(d.Item2); remaining.Add(d); }
                    while (remaining.Count > 0)
                    {
                        var edge = remaining.First(); var loop = new List<int> { edge.Item1 }; int cursor = edge.Item1;
                        for (int guard = 0; guard < boundaryCount + 1; guard++)
                        {
                            if (!next.TryGetValue(cursor, out var candidates)) break;
                            int dest = -1; foreach (int candidate in candidates) if (remaining.Contains((cursor, candidate))) { dest = candidate; break; } if (dest < 0) break;
                            remaining.Remove((cursor, dest)); cursor = dest; if (cursor == loop[0]) break; loop.Add(cursor);
                        }
                        if (cursor != loop[0] || loop.Count < 3) { unresolved += loop.Count; continue; }
                        int centre = sourceCount + loops.Count; loops.Add(loop.ToArray());
                        for (int k = 0; k < loop.Count; k++) tris.Add(new Tri { a = loop[(k + 1) % loop.Count], b = loop[k], c = centre, cap = true });
                    }
                }
                ClosureReport = $"boundary_edges={boundaryCount}, capped_loops={loops.Count}, unresolved_edges={unresolved}, nonmanifold_edges={edges.Count(e => e.Value > 2)}";
                if (close && unresolved > 0) throw new InvalidOperationException("Trunk surface is not closed: " + ClosureReport);
                Refit(bind); root = Build(0, tris.Count); UpdateBounds(root);
            }
            public void Refit(Vector3[] vertices)
            {
                points = new Vector3[sourceCount + loops.Count]; Array.Copy(vertices, points, sourceCount);
                for (int i = 0; i < loops.Count; i++) { Vector3 c = Vector3.zero; foreach (int v in loops[i]) c += vertices[v]; points[sourceCount + i] = c / loops[i].Length; }
                if (root != null) UpdateBounds(root);
            }
            Bounds TB(Tri t) { var b = new Bounds(points[t.a], Vector3.zero); b.Encapsulate(points[t.b]); b.Encapsulate(points[t.c]); return b; }
            Node Build(int start, int count)
            {
                var n = new Node { start = start, count = count }; var bounds = TB(tris[start]); for (int i = start + 1; i < start + count; i++) bounds.Encapsulate(TB(tris[i])); n.bounds = bounds;
                if (count <= 8) return n;
                int axis = bounds.size.x > bounds.size.y ? 0 : 1; if (bounds.size.z > bounds.size[axis]) axis = 2;
                var list = tris.GetRange(start, count); list.Sort((a, b) => TB(a).center[axis].CompareTo(TB(b).center[axis])); for (int i = 0; i < count; i++) tris[start + i] = list[i];
                int half = count / 2; n.left = Build(start, half); n.right = Build(start + half, count - half); return n;
            }
            Bounds UpdateBounds(Node n)
            {
                if (n.left != null) { n.bounds = UpdateBounds(n.left); n.bounds.Encapsulate(UpdateBounds(n.right)); }
                else { n.bounds = TB(tris[n.start]); for (int i = n.start + 1; i < n.start + n.count; i++) n.bounds.Encapsulate(TB(tris[i])); }
                return n.bounds;
            }
            static readonly Vector3[] Rays = { new Vector3(1, .37139f, .12713f).normalized, new Vector3(.23971f, 1, .49183f).normalized, new Vector3(.53117f, .21931f, 1).normalized };
            public bool Inside(Vector3 p)
            {
                if (!root.bounds.Contains(p)) return false; int votes = 0;
                foreach (var direction in Rays) { int crossings = Crossings(root, new Ray(p, direction), float.PositiveInfinity, false); if (crossings % 2 != 0) votes++; }
                return votes >= 2;
            }
            public bool Hit(Ray ray, float maximum) => Crossings(root, ray, maximum, true) > 0;
            int Crossings(Node n, Ray ray, float maximum, bool first)
            {
                if (!n.bounds.IntersectRay(ray, out float entry) || entry > maximum) return 0;
                if (n.left != null) { int count = Crossings(n.left, ray, maximum, first); if (first && count > 0) return count; return count + Crossings(n.right, ray, maximum, first); }
                int hits = 0;
                for (int i = n.start; i < n.start + n.count; i++)
                {
                    var t = tris[i]; Vector3 a = points[t.a], e1 = points[t.b] - a, e2 = points[t.c] - a;
                    var h = Vector3.Cross(ray.direction, e2); float det = Vector3.Dot(e1, h); if (Mathf.Abs(det) < 1e-9f) continue;
                    float inv = 1 / det; var s = ray.origin - a; float u = inv * Vector3.Dot(s, h); if (u < 0 || u > 1) continue;
                    var q = Vector3.Cross(s, e1); float v = inv * Vector3.Dot(ray.direction, q); if (v < 0 || u + v > 1) continue;
                    float d = inv * Vector3.Dot(e2, q); if (d <= .000001f || d > maximum) continue;
                    hits++; if (first) return hits;
                }
                return hits;
            }
            public float Distance(Vector3 p) { float best = float.PositiveInfinity; Nearest(root, p, ref best); return Mathf.Sqrt(best); }
            void Nearest(Node n, Vector3 p, ref float best)
            {
                if (n.bounds.SqrDistance(p) > best) return;
                if (n.left != null) { var a = n.left; var b = n.right; if (a.bounds.SqrDistance(p) > b.bounds.SqrDistance(p)) { a = n.right; b = n.left; } Nearest(a, p, ref best); Nearest(b, p, ref best); return; }
                for (int i = n.start; i < n.start + n.count; i++) { var t = tris[i]; if (t.cap) continue; best = Mathf.Min(best, (p - Closest(p, points[t.a], points[t.b], points[t.c])).sqrMagnitude); }
            }
            static Vector3 Closest(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
            {
                var ab = b - a; var ac = c - a; var ap = p - a; float d1 = Vector3.Dot(ab, ap), d2 = Vector3.Dot(ac, ap); if (d1 <= 0 && d2 <= 0) return a;
                var bp = p - b; float d3 = Vector3.Dot(ab, bp), d4 = Vector3.Dot(ac, bp); if (d3 >= 0 && d4 <= d3) return b;
                float vc = d1 * d4 - d3 * d2; if (vc <= 0 && d1 >= 0 && d3 <= 0) return a + ab * (d1 / (d1 - d3));
                var cp = p - c; float d5 = Vector3.Dot(ab, cp), d6 = Vector3.Dot(ac, cp); if (d6 >= 0 && d5 <= d6) return c;
                float vb = d5 * d2 - d1 * d6; if (vb <= 0 && d2 >= 0 && d6 <= 0) return a + ac * (d2 / (d2 - d6));
                float va = d3 * d6 - d5 * d4; if (va <= 0 && d4 - d3 >= 0 && d5 - d6 >= 0) return b + (c - b) * ((d4 - d3) / ((d4 - d3) + (d5 - d6)));
                float denom = 1 / (va + vb + vc); return a + ab * (vb * denom) + ac * (vc * denom);
            }
        }
    }
}
