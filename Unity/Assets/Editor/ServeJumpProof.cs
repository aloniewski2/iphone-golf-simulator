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
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

namespace GolfArcade.EditorTools
{
    // Editor-only live-game recorder. Graph replays are restored before rendering;
    // no runtime source, graph times, procedural state or actor state is changed.
    [InitializeOnLoad]
    public static class ServeJumpProof
    {
        const string Flag = "ServeJumpProofR2";
        static ServeJumpProof() { EditorApplication.update += Tick; }
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Tennis.unity");
            SessionState.SetBool(Flag, true); EditorApplication.isPlaying = true;
        }
        static void Tick()
        {
            if (!SessionState.GetBool(Flag, false) || !EditorApplication.isPlaying) return;
            var g = Object.FindFirstObjectByType<TennisGame>();
            if (!g || !g.Initialized || g.GetComponent<ServeJumpProofRecorder>()) return;
            SessionState.SetBool(Flag, false);
            try { g.gameObject.AddComponent<ServeJumpProofRecorder>().Begin(g); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
    }

    [DefaultExecutionOrder(2000)]
    public sealed class ServeJumpProofRecorder : MonoBehaviour
    {
        TennisGame game; HeroTennisDriver driver; Camera camera;
        string dir; int warmup, frame, pictures, afterContact;
        bool recording, swung, configured; int entryDelay;
        readonly HashSet<string> capturedKeys = new HashSet<string>();
        StreamWriter trace, bones, contactTrace;
        Transform[] hierarchy, skeleton;
        readonly BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        readonly List<string> values = new List<string>();
        static string Env(string n, string fallback) => Environment.GetEnvironmentVariable(n) ?? fallback;
        static string N(float x) => x.ToString("0.000000", CultureInfo.InvariantCulture);
        T Get<T>(string n) => (T)typeof(HeroTennisDriver).GetField(n, flags).GetValue(driver);
        Transform B(HumanBodyBones b) => driver.matchLook.Bone(b);
        void Number(float x) { if (!float.IsFinite(x)) throw new Exception("Nonfinite proof field"); values.Add(N(x)); }
        void Vec(Vector3 x) { Number(x.x); Number(x.y); Number(x.z); }
        void Quat(Quaternion x) { Number(x.x); Number(x.y); Number(x.z); Number(x.w); }
        Vector3 Local(Vector3 p) => driver.actor.transform.InverseTransformPoint(p);
        static IEnumerable<string> XYZ(string n) => new[] { n + "_x", n + "_y", n + "_z" };
        static IEnumerable<string> XYZW(string n) => new[] { n + "_x", n + "_y", n + "_z", n + "_w" };

        public void Begin(TennisGame g)
        {
            game = g; dir = Path.GetFullPath(Env("SJ_OUT", "../work/serve-toss-jump/r2/male"));
            Directory.CreateDirectory(dir);
            var identity = new List<string>();
            foreach (string path in new[] { "Assets/Characters/MatchHeroes/Male/Male_Serve.fbx", "Assets/Characters/MatchHeroes/Male/Male_Serve.fbx.meta", "Assets/Characters/MatchHeroes/Female/Female_Serve.fbx", "Assets/Characters/MatchHeroes/Female/Female_Serve.fbx.meta", "Assets/Scripts/Tennis/HeroTennisDriver.cs", "Assets/Scripts/Tennis/TennisGame.cs", "Assets/Scripts/Tennis/TennisActor.cs", "Assets/Scripts/Tennis/TennisServeRoutine.cs", "Assets/Resources/Tennis/Customization/PlayerMale.prefab", "Assets/Resources/Tennis/Customization/PlayerFemale.prefab", "Assets/Editor/ServeJumpProof.cs" })
            {
                using (var sha = System.Security.Cryptography.SHA256.Create())
                    identity.Add(BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant() + "  " + path);
            }
            File.WriteAllLines(Path.Combine(dir, "TESTED_SHA256SUMS"), identity);
            File.WriteAllText(Path.Combine(dir, "key_frames.csv"), "label,frame,time,clipTime,state\n");
            Time.captureFramerate = 60; TennisGame.AutoPlayTimingJitter = 0;
            var presentation = game.GetComponent<TennisPresentation>(); if (presentation) presentation.Finish();
            entryDelay = int.Parse(Env("SJ_ENTRY_DELAY", "0"));
            trace = new StreamWriter(Path.Combine(dir, "trace.csv"));
            bones = new StreamWriter(Path.Combine(dir, "bones.csv"));
            contactTrace = new StreamWriter(Path.Combine(dir, "contact.csv"));
            contactTrace.WriteLine("frame,time,ttc,clipTime,strings_x,strings_y,strings_z,Lheel_mm,Ltoe_mm,Rheel_mm,Rtoe_mm,Lknee_deg,Rknee_deg,Relbow_deg");
            bones.WriteLine("frame,clipTime,stage,bone,x,y,z,qx,qy,qz,qw");
            var h = new List<string> { "frame", "time", "dt", "flow", "state", "clipTime", "prepare", "prepareServe", "swinging", "ttc", "baseReady", "baseWalk", "baseServe", "baseRun", "upperWeight", "actionLegs", "legsFromRun", "Lstepping", "Rstepping", "LstepT", "RstepT", "Llocked", "Rlocked", "LlockW", "RlockW", "groundLift", "sinkBeforeGround", "heelRest", "toeRest", "picture", "stepsTaken" };
            foreach (string n in new[] { "worldLheel", "worldLtoe", "worldRheel", "worldRtoe", "actorRoot", "heroRoot", "smoothLocal", "lungeOffset", "LlockPos", "RlockPos", "LstepFrom", "RstepFrom", "ball" }) h.AddRange(XYZ(n));
            foreach (string s in new[] { "live", "layers", "base", "pureServe", "ready" })
            {
                foreach (string n in new[] { "Lheel", "Ltoe", "Rheel", "Rtoe", "Lhand", "Rhand", "head", "pelvis", "palm", "strings" }) h.AddRange(XYZ(s + "_" + n));
                foreach (string n in new[] { "Lwrist", "Rwrist" }) h.AddRange(XYZW(s + "_" + n));
                h.AddRange(new[] { s + "_Lheel_mm", s + "_Ltoe_mm", s + "_Rheel_mm", s + "_Rtoe_mm", s + "_Lknee_deg", s + "_Rknee_deg", s + "_Relbow_deg" });
            }
            h.AddRange(new[] { "readyTime", "readyPhase" });
            trace.WriteLine(string.Join(",", h));
        }

        void LateUpdate()
        {
            try
            {
                if (game == null || trace == null) return;
                if (!configured)
                {
                    if (entryDelay-- > 0) return;
                    bool female = Env("SJ_FEMALE", "0") == "1";
                    game.SelectCharacter(female); game.ConfigureMatch(TennisGame.Mode.Campaign, female ? "Viktor" : "Nadia", female ? "Viktor" : "Nadia", "ROUND");
                    var presentation = game.GetComponent<TennisPresentation>(); if (presentation) presentation.Finish();
                    configured = true;
                }
                if (warmup++ < 60) return;
                if (driver == null)
                {
                    driver = game.Player.GetComponentInChildren<HeroTennisDriver>();
                    if (!driver || !driver.matchLook) throw new Exception("No live match hero");
                    camera = game.GameplayCamera ? game.GameplayCamera : Camera.main;
                    hierarchy = driver.GetComponentsInChildren<Transform>(true);
                    skeleton = driver.matchLook.bones.Where(t => t != null).Distinct().ToArray();
                    File.WriteAllText(Path.Combine(dir, "slots.txt"), driver.SlotReport());
                    driver.actor.Posed += ContactPose;
                }
                if (!recording)
                {
                    if (game.Flow != TennisGame.Phase.PlayerServeHold) return;
                    recording = true; game.AutoPlay = true; game.AutoPlayLean = true;
                }
                Record();
                if (driver.actor.Swinging && driver.CurrentAction == HeroTennisDriver.Clip.Serve && driver.actor.SignedTimeToContact <= 0) swung = true;
                if (swung) afterContact++;
                if (afterContact >= int.Parse(Env("SJ_TAIL", "150")) || frame >= 1800) Finish(frame >= 1800 ? 1 : 0);
            }
            catch (Exception e) { Debug.LogException(e); Finish(1); }
        }

        struct Pose
        {
            public Vector3 p, s; public Quaternion q;
            public Pose(Transform t) { p = t.localPosition; q = t.localRotation; s = t.localScale; }
            public void Apply(Transform t) { t.localPosition = p; t.localRotation = q; t.localScale = s; }
        }
        void ContactPose()
        {
            if (contactTrace == null || !driver.actor.Swinging || driver.CurrentAction != HeroTennisDriver.Clip.Serve || Mathf.Abs(driver.actor.SignedTimeToContact) > .025f) return;
            var p = Local(driver.StringCentre);
            var lf = B(HumanBodyBones.LeftFoot); var rf = B(HumanBodyBones.RightFoot);
            float Knee(HumanBodyBones up, HumanBodyBones lo, Transform foot) => 180 - Vector3.Angle(B(up).position - B(lo).position, foot.position - B(lo).position);
            var el = B(HumanBodyBones.RightLowerArm);
            var time = Get<AnimationClipPlayable[]>("playables")[(int)HeroTennisDriver.Clip.Serve].GetTime();
            contactTrace.WriteLine(string.Join(",", frame, N(Time.time), N(driver.actor.SignedTimeToContact), N((float)time), N(p.x), N(p.y), N(p.z),
                N((Local(lf.position).y - Get<float>("heelRest")) * 1000), N((Local(B(HumanBodyBones.LeftToes).position).y - Get<float>("toeRest")) * 1000),
                N((Local(rf.position).y - Get<float>("heelRest")) * 1000), N((Local(B(HumanBodyBones.RightToes).position).y - Get<float>("toeRest")) * 1000),
                N(Knee(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, lf)), N(Knee(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, rf)),
                N(Vector3.Angle(B(HumanBodyBones.RightUpperArm).position - el.position, B(HumanBodyBones.RightHand).position - el.position))));
            contactTrace.Flush();
        }
        void BoneRows(string stage)
        {
            foreach (var t in skeleton)
            {
                var p = Local(t.position); var q = Quaternion.Inverse(driver.actor.transform.rotation) * t.rotation;
                bones.WriteLine(string.Join(",", frame, N(driver.PlayingClipTime), stage, t.name, N(p.x), N(p.y), N(p.z), N(q.x), N(q.y), N(q.z), N(q.w)));
            }
        }
        void Stage(string stage)
        {
            var lfoot = B(HumanBodyBones.LeftFoot); var ltoe = B(HumanBodyBones.LeftToes);
            var rfoot = B(HumanBodyBones.RightFoot); var rtoe = B(HumanBodyBones.RightToes);
            var lh = B(HumanBodyBones.LeftHand); var rh = B(HumanBodyBones.RightHand); var mid = B(HumanBodyBones.LeftMiddleProximal);
            var palm = mid ? Vector3.Lerp(lh.position, mid.position, .7f) : lh.position;
            foreach (var p in new[] { lfoot.position, ltoe.position, rfoot.position, rtoe.position, lh.position, rh.position, B(HumanBodyBones.Head).position, B(HumanBodyBones.Hips).position, palm, driver.StringCentre }) Vec(Local(p));
            Quat(Quaternion.Inverse(driver.actor.transform.rotation) * lh.rotation);
            Quat(Quaternion.Inverse(driver.actor.transform.rotation) * rh.rotation);
            var heel = Get<float>("heelRest"); var toe = Get<float>("toeRest");
            Number((Local(lfoot.position).y - heel) * 1000); Number((Local(ltoe.position).y - toe) * 1000);
            Number((Local(rfoot.position).y - heel) * 1000); Number((Local(rtoe.position).y - toe) * 1000);
            float Knee(HumanBodyBones up, HumanBodyBones lo, Transform foot) => 180 - Vector3.Angle(B(up).position - B(lo).position, foot.position - B(lo).position);
            Number(Knee(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, lfoot));
            Number(Knee(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, rfoot));
            var elbow = B(HumanBodyBones.RightLowerArm); Number(Vector3.Angle(B(HumanBodyBones.RightUpperArm).position - elbow.position, rh.position - elbow.position));
            BoneRows(stage);
        }
        void Record()
        {
            var a = driver.actor; var w = Get<float[]>("weight");
            var stepping = Get<bool[]>("stepping"); var locked = Get<bool[]>("locked");
            var stepT = Get<float[]>("stepT"); var lockW = Get<float[]>("lockW");
            values.Clear();
            values.Add(frame.ToString()); Number(Time.time); Number(Time.deltaTime); values.Add(game.Flow.ToString()); values.Add(driver.State.Replace(',', ';'));
            Number(driver.PlayingClipTime); Number(a.PrepareAmount); values.Add(a.PrepareServe ? "1" : "0"); values.Add(a.Swinging ? "1" : "0"); Number(a.SignedTimeToContact);
            Number(w[(int)HeroTennisDriver.Clip.Ready]); Number(w[(int)HeroTennisDriver.Clip.Walk]); Number(w[(int)HeroTennisDriver.Clip.Serve]);
            Number(w[(int)HeroTennisDriver.Clip.RunForward] + w[(int)HeroTennisDriver.Clip.RunLeft] + w[(int)HeroTennisDriver.Clip.RunRight]);
            Number(driver.UpperLayerWeight); Number(Get<float>("actionLegs")); Number(Get<float>("legsFromRun"));
            values.Add(stepping[0] ? "1" : "0"); values.Add(stepping[1] ? "1" : "0"); Number(stepT[0]); Number(stepT[1]);
            values.Add(locked[0] ? "1" : "0"); values.Add(locked[1] ? "1" : "0"); Number(lockW[0]); Number(lockW[1]);
            Number(driver.LastGroundLift); Number(driver.SinkBeforeGround); Number(Get<float>("heelRest")); Number(Get<float>("toeRest"));
            bool picture = frame % 2 == 0 && Env("SJ_CAPTURE", "1") == "1"; values.Add(picture ? pictures.ToString() : "-1"); values.Add(driver.StepsTaken.ToString());
            foreach (var b in new[] { HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes, HumanBodyBones.RightFoot, HumanBodyBones.RightToes }) Vec(B(b).position);
            Vec(a.transform.position); Vec(driver.transform.position); Vec(Get<Vector3>("smoothLocal")); Vec(Get<Vector3>("lungeOffset"));
            foreach (var p in Get<Vector3[]>("lockPos")) Vec(p);
            foreach (var p in Get<Vector3[]>("stepFrom")) Vec(p);
            Vec(Local(game.BallPosition));
            var poses = hierarchy.Select(t => new Pose(t)).ToArray();
            var graph = Get<PlayableGraph>("graph"); var mixer = Get<AnimationMixerPlayable>("mixer"); var layers = Get<AnimationLayerMixerPlayable>("layers");
            var playables = Get<AnimationClipPlayable[]>("playables");
            float[] mixWeights = Enumerable.Range(0, mixer.GetInputCount()).Select(i => mixer.GetInputWeight(i)).ToArray();
            double[] clipTimes = playables.Select(p => p.IsValid() ? p.GetTime() : 0).ToArray();
            float upper = layers.GetInputWeight(1);
            try
            {
                Stage("live");
                graph.Evaluate(0); Stage("layers");
                layers.SetInputWeight(1, 0); graph.Evaluate(0); Stage("base");
                for (int i = 0; i < mixer.GetInputCount(); i++) mixer.SetInputWeight(i, i == (int)HeroTennisDriver.Clip.Serve ? 1 : 0);
                playables[(int)HeroTennisDriver.Clip.Serve].SetTime(driver.PlayingClipTime); graph.Evaluate(0); Stage("pureServe");
                for (int i = 0; i < mixer.GetInputCount(); i++) mixer.SetInputWeight(i, i == (int)HeroTennisDriver.Clip.Ready ? 1 : 0);
                graph.Evaluate(0); Stage("ready");
            }
            finally
            {
                for (int i = 0; i < mixer.GetInputCount(); i++) mixer.SetInputWeight(i, mixWeights[i]);
                for (int i = 0; i < playables.Length; i++) if (playables[i].IsValid()) playables[i].SetTime(clipTimes[i]);
                layers.SetInputWeight(1, upper);
                for (int i = 0; i < hierarchy.Length; i++) poses[i].Apply(hierarchy[i]);
            }
            var readyTime = playables[(int)HeroTennisDriver.Clip.Ready].GetTime();
            Number((float)readyTime); Number(Mathf.Repeat((float)readyTime, Get<float[]>("length")[(int)HeroTennisDriver.Clip.Ready]));
            trace.WriteLine(string.Join(",", values)); trace.Flush(); bones.Flush();
            if (picture)
            {
                string id = (pictures++).ToString("00000");
                GameCapture.Save(Path.Combine(dir, "game_" + id + ".png"), 960, 540);
                var root = a.transform; var target = root.position + Vector3.up * 1.2f;
                Shot(Path.Combine(dir, "front_" + id + ".png"), root.TransformPoint(0, 1.7f, 3.6f), target);
                Shot(Path.Combine(dir, "back_" + id + ".png"), root.TransformPoint(0, 1.7f, -3.6f), target);
                Shot(Path.Combine(dir, "side_" + id + ".png"), root.TransformPoint(-3.6f, 1.7f, 0), target);
            }
            if (Env("SJ_CAPTURE", "1") == "1" && driver.State.StartsWith("Serve"))
            {
                var keys = new List<string>();
                if (!capturedKeys.Contains("entry")) keys.Add("entry");
                if (driver.PlayingClipTime >= 1.70f && driver.PlayingClipTime < 1.75f) keys.Add("contact");
                if (driver.PlayingClipTime >= 1.90f && driver.PlayingClipTime < 1.96f) keys.Add("landing");
                if (driver.PlayingClipTime >= 1.95f && driver.PlayingClipTime < 1.978f) keys.Add("settled");
                foreach (var key in keys)
                    if (capturedKeys.Add(key))
                    {
                        var root = a.transform; var target = root.position + Vector3.up * 1.2f;
                        GameCapture.Save(Path.Combine(dir, "key_" + key + "_game.png"), 960, 540);
                        Shot(Path.Combine(dir, "key_" + key + "_front.png"), root.TransformPoint(0, 1.7f, 3.6f), target);
                        Shot(Path.Combine(dir, "key_" + key + "_back.png"), root.TransformPoint(0, 1.7f, -3.6f), target);
                        Shot(Path.Combine(dir, "key_" + key + "_side.png"), root.TransformPoint(-3.6f, 1.7f, 0), target);
                        File.AppendAllText(Path.Combine(dir, "key_frames.csv"), key + "," + frame + "," + N(Time.time) + "," + N(driver.PlayingClipTime) + "," + driver.State.Replace(',', ';') + "\n");
                    }
            }
            frame++;
        }
        void Shot(string path, Vector3 from, Vector3 to)
        {
            var p = camera.transform.position; var q = camera.transform.rotation; var fov = camera.fieldOfView;
            var previous = camera.targetTexture; var active = RenderTexture.active;
            var rt = RenderTexture.GetTemporary(640, 640, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default, 4);
            Texture2D image = null;
            try
            {
                camera.transform.SetPositionAndRotation(from, Quaternion.LookRotation(to - from)); camera.fieldOfView = 40;
                camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
                image = new Texture2D(640, 640, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 640, 640), 0, 0); image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previous; RenderTexture.active = active;
                camera.transform.SetPositionAndRotation(p, q); camera.fieldOfView = fov;
                if (image) DestroyImmediate(image); RenderTexture.ReleaseTemporary(rt);
            }
        }
        void Finish(int code)
        {
            if (driver) driver.actor.Posed -= ContactPose;
            trace?.Dispose(); bones?.Dispose(); contactTrace?.Dispose(); trace = null; bones = null; contactTrace = null;
            Time.captureFramerate = 0;
            Debug.Log($"[ServeJumpProof] frames={frame} pictures={pictures} contactTail={afterContact} exit={code}");
            if (Application.isBatchMode) EditorApplication.Exit(code); else EditorApplication.isPlaying = false;
        }
    }
}
