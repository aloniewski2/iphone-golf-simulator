using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using GolfArcade.Course;
using GolfArcade.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Profiling;
using Object = UnityEngine.Object;

namespace GolfArcade.EditorTools
{
    /// Catalog-wide captures through the real GolfGame, HoleView and address camera.
    [InitializeOnLoad]
    public static class GolfCourseStandardCapture
    {
        const string Key = "GolfCourseStandardCapture";
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static GolfGame game;
        static CameraRig rig;
        static Camera cam;
        static int[] holes;
        static int index = -1, frame, warm;
        static string output;
        static double deadline;
        [Serializable] public sealed class Slot { public string renderer, material, shader, albedo, normal; public int slot, vertices, triangles; public bool visible; }
        [Serializable] public sealed class Geometry { public string name, hash; public int vertices, triangles; }
        [Serializable] public sealed class Report
        {
            public int hole, obstacleCount, colliders, drawCalls, triangles;
            public string pipeline, unity, obstacleHash;
            public long textureBytes;
            public int cameraMeshTriangles,cameraMaterialPasses;
            public string graphics;
            public int repairedPalms, replacedPalmTriangles, newPalmTriangles;
            public List<Slot> materials = new();
            public List<Geometry> ground = new();
        }

        static GolfCourseStandardCapture() { EditorApplication.update += Tick; }
        public static void Run()
        {
            string path = Environment.GetEnvironmentVariable("GOLF_STANDARD_OUT");
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("GOLF_STANDARD_OUT required");
            Directory.CreateDirectory(path);
            SessionState.SetString(Key + "out", Path.GetFullPath(path));
            SessionState.SetString(Key + "holes", Environment.GetEnvironmentVariable("GOLF_STANDARD_HOLES") ?? "7,12,13,14,15,16,17,18,19,20,21,22,23,8,9,10");
            SessionState.SetBool(Key, true);
            EditorSceneManager.OpenScene("Assets/Scenes/Golf.unity");
            EditorApplication.isPlaying = true;
        }

        static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying) return;
            try
            {
                if (!game)
                {
                    if (++warm < 30 || !HoleView.Current) return;
                    game = Object.FindFirstObjectByType<GolfGame>();
                    rig = Object.FindFirstObjectByType<CameraRig>();
                    cam = rig.GetComponentInChildren<Camera>();
                    output = SessionState.GetString(Key + "out", "");
                    holes = SessionState.GetString(Key + "holes", "12,21").Split(',').Select(int.Parse).ToArray();
                    game.ChooseHoles(0); game.Play();
                    GolfWindSway.FreezeTime = 0;
                    GolfWindSway.ForcedWind = Wind.Calm;
                    Next(); return;
                }
                if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Hole capture did not settle");
                if (Time.frameCount - frame < 12) return;
                if (Object.FindObjectsByType<HoleView>(FindObjectsSortMode.None).Length != 1) return;
                Capture(); Next();
            }
            catch (Exception e) { Debug.LogException(e); Finish(1); }
        }

        static void Next()
        {
            if (++index >= holes.Length) { Finish(0); return; }
            game.enabled = true; rig.enabled = true;
            game.JumpToHole(holes[index]);
            game.DropBall(game.CurrentHole.Tee);
            frame = Time.frameCount; deadline = EditorApplication.timeSinceStartup + 180;
        }

        static string Hash(Action<BinaryWriter> write)
        {
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true)) write(writer);
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(stream.ToArray())).Replace("-", "").ToLowerInvariant();
        }

        static void Capture()
        {
            game.enabled = false; rig.enabled = false;
            var h = game.CurrentHole;
            var view = HoleView.Current;
            var golfer = (GolferView)typeof(GolfGame).GetField("golfer", Private).GetValue(game);
            var ball = (Transform)typeof(GolfGame).GetField("ball", Private).GetValue(game);
            var line = (LineRenderer)typeof(GolfGame).GetField("aimLine", Private).GetValue(game);
            var marker = (Transform)typeof(GolfGame).GetField("landingMarker", Private).GetValue(game);
            if (line) line.enabled = false;
            var dots=(AimDots)typeof(GolfGame).GetField("aimDots",Private).GetValue(game);
            var landing=(LandingZone)typeof(GolfGame).GetField("landingZone",Private).GetValue(game);
            if(dots)dots.Hide();if(landing)landing.gameObject.SetActive(false);
            if (marker) marker.gameObject.SetActive(false);
            var report = new Report { hole = h.Number, obstacleCount = h.Obstacles.Length, unity = Application.unityVersion,
                graphics = SystemInfo.graphicsDeviceType+" / "+SystemInfo.graphicsDeviceName,
                pipeline = (QualitySettings.renderPipeline ? QualitySettings.renderPipeline : UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline).name };
            report.obstacleHash = Hash(w => { foreach (var o in h.Obstacles) {
                w.Write((int)o.Kind); w.Write(o.X); w.Write(o.D); w.Write(o.Base); w.Write(o.Top);
                w.Write(o.Radius); w.Write(o.TrunkRadius); w.Write(o.CrownBase); w.Write(o.Cone);
            } });
            var textures = new HashSet<Texture>();
            var palms = view.GetComponentInChildren<GolfCoursePalms>();
            if(palms) { report.repairedPalms=palms.PalmCount;report.replacedPalmTriangles=palms.RemovedFrondTriangles;report.newPalmTriangles=palms.AddedTriangles; }
            foreach (var r in view.GetComponentsInChildren<Renderer>(true))
            {
                Mesh mesh = r is SkinnedMeshRenderer skin ? skin.sharedMesh : r.TryGetComponent<MeshFilter>(out var filter) ? filter.sharedMesh : null;
                for (int i = 0; i < r.sharedMaterials.Length; i++)
                {
                    var m = r.sharedMaterials[i]; if (!m) continue;
                    var a = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null;
                    var n = m.HasProperty("_BumpMap") ? m.GetTexture("_BumpMap") : null;
                    report.materials.Add(new Slot { renderer = r.name, material = m.name, slot = i, shader = m.shader.name,
                        albedo = a ? a.name : "", normal = n ? n.name : "", visible = r.enabled && r.gameObject.activeInHierarchy,
                        vertices = mesh ? mesh.vertexCount : 0, triangles = mesh && mesh.isReadable ? mesh.triangles.Length / 3 : 0 });
                    foreach (string prop in m.GetTexturePropertyNames()) { var t = m.GetTexture(prop); if (t) textures.Add(t); }
                }
            }
            foreach (var c in view.GetComponentsInChildren<MeshCollider>(true))
            {
                var mesh = c.sharedMesh; if (!mesh || !mesh.isReadable) continue;
                report.ground.Add(new Geometry { name = c.name, vertices = mesh.vertexCount, triangles = mesh.triangles.Length / 3,
                    hash = Hash(w => { foreach (var p in mesh.vertices) { var v = c.transform.TransformPoint(p); w.Write(v.x); w.Write(v.y); w.Write(v.z); } foreach (int t in mesh.triangles) w.Write(t); }) });
            }
            report.colliders = report.ground.Count;
            foreach (var t in textures) report.textureBytes += Profiler.GetRuntimeMemorySizeLong(t);
            var tee = HoleView.ToWorld(h.Tee); var pin = HoleView.ToWorld(h.Pin);
            var direction = pin - tee; direction.y = 0; direction.Normalize();
            var right = Vector3.Cross(Vector3.up, direction);
            string stem = Path.Combine(output, "hole" + h.Number.ToString("00"));
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) canvas.enabled = false;
            golfer.SetVisible(false);
            Address(h.Tee, h.RecommendedTarget(h.Tee), false); Save(stem + "_tee.png");
            var planes=GeometryUtility.CalculateFrustumPlanes(cam);
            foreach(var r in view.GetComponentsInChildren<Renderer>())
            {
                if(!r.enabled||!r.gameObject.activeInHierarchy||!GeometryUtility.TestPlanesAABB(planes,r.bounds))continue;
                Mesh m=r is SkinnedMeshRenderer sk?sk.sharedMesh:r.TryGetComponent<MeshFilter>(out var mf)?mf.sharedMesh:null;
                if(!m)continue;
                for(int sub=0;sub<m.subMeshCount;sub++)report.cameraMeshTriangles+=(int)m.GetIndexCount(sub)/3;
                report.cameraMaterialPasses+=m.subMeshCount;
            }
            report.drawCalls = UnityStats.drawCalls; report.triangles = UnityStats.triangles;
            var approach = h.Centerline[Math.Max(0, h.Centerline.Length - 2)];
            if (h.Centerline.Length == 2) approach = new CoursePoint(h.Pin.X - direction.x * 18, h.Pin.D - direction.z * 18);
            Address(approach, h.Pin, false); Save(stem + "_approach.png");
            var putt = new CoursePoint(h.Pin.X - direction.x * 5, h.Pin.D - direction.z * 5);
            Address(putt, h.Pin, true); Save(stem + "_putting.png");
            float length = Vector3.Distance(tee, pin);
            rig.transform.position = (tee + pin) * .5f - direction * length * .4f + right * length * .25f + Vector3.up * (length * .7f + 55);
            rig.transform.LookAt((tee + pin) * .5f); cam.fieldOfView = 60;
            Save(stem + "_aerial.png");
            if(Environment.GetEnvironmentVariable("GOLF_STANDARD_TREES")=="1" && h.Number is 16 or 19 or 21 or 22 or 23)
            {
                int plant = h.Number switch { 21=>7,22=>10,23=>12,_=>0 };
                var root = view.ModelRoot.GetComponentsInChildren<Transform>().First(t=>t.name.StartsWith($"PLANT_{plant:000}_"));
                float height = int.Parse(root.name.Split('_')[3])/100f*Mathf.Abs(root.lossyScale.x);
                var target = root.position+Vector3.up*height*.68f;
                ball.gameObject.SetActive(false);cam.fieldOfView=50;
                rig.transform.position=target+new Vector3(24,3,-29);rig.transform.LookAt(target);
                Save(stem+"_palm.png",1200,900);
                rig.transform.position=target+new Vector3(-14,-height*.48f,17);rig.transform.LookAt(target+Vector3.up*height*.22f);
                Save(stem+"_palm_under.png",1200,900);
            }
            Address(h.Tee, h.RecommendedTarget(h.Tee), false);
            golfer.Stand(ball.position, (HoleView.ToWorld(h.RecommendedTarget(h.Tee)) - tee).normalized); golfer.SetVisible(true);
            string motionHoles=Environment.GetEnvironmentVariable("GOLF_STANDARD_MOTION_HOLES");
            if(Environment.GetEnvironmentVariable("GOLF_STANDARD_MOTION")=="1"
                && (string.IsNullOrEmpty(motionHoles)||motionHoles.Split(',').Contains(h.Number.ToString())))
            {
                golfer.SetVisible(false);ball.gameObject.SetActive(false);
                string frames=stem+"_motion";Directory.CreateDirectory(frames);
                for(int f=0;f<96;f++)
                {
                    float t=f/95f,station=t*(h.Centerline.Length-1);
                    int segment=Mathf.Min(h.Centerline.Length-2,Mathf.FloorToInt(station));
                    var a=HoleView.ToWorld(h.Centerline[segment]);var b=HoleView.ToWorld(h.Centerline[segment+1]);
                    var target=Vector3.Lerp(a,b,station-segment);var forward=(b-a).normalized;
                    rig.transform.position=target-forward*26+right*(12*Mathf.Sin(t*Mathf.PI))+Vector3.up*16;
                    rig.transform.LookAt(target+forward*14);cam.fieldOfView=60;
                    GolfWindSway.FreezeTime=f/24f;
                    Save(Path.Combine(frames,$"f_{f:0000}.png"),450,800);
                }
                GolfWindSway.FreezeTime=0;
            }
            game.DropBall(h.Tee);Address(h.Tee,h.RecommendedTarget(h.Tee),false);
            golfer.SetVisible(true);
            foreach(var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))canvas.enabled=true;
            Object.FindFirstObjectByType<GolfArcade.UI.Hud>().HideHoleIntro();
            Canvas.ForceUpdateCanvases();
            var canvases=new List<(Canvas canvas,RenderMode mode,Camera camera,float distance)>();
            foreach(var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                canvases.Add((canvas,canvas.renderMode,canvas.worldCamera,canvas.planeDistance));
                canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=cam;canvas.planeDistance=1;
            }
            Canvas.ForceUpdateCanvases();
            try { Save(stem+"_gameplay.png"); }
            finally { foreach(var c in canvases){c.canvas.renderMode=c.mode;c.canvas.worldCamera=c.camera;c.canvas.planeDistance=c.distance;} }
            File.WriteAllText(stem + "_audit.json", JsonUtility.ToJson(report, true));
            Debug.Log($"[GolfCourseStandard] captured {h.Number}, {report.materials.Count} slots, {report.drawCalls} draws, {report.triangles} triangles");

            void Address(CoursePoint at, CoursePoint target, bool putting)
            {
                ball.position = HoleView.ToWorld(at, .06); ball.gameObject.SetActive(true);
                var aim = HoleView.ToWorld(target) - HoleView.ToWorld(at); aim.y = 0; aim.Normalize();
                cam.fieldOfView = 60; rig.FrameAddress(ball.position, aim, putting); rig.SnapNext(); rig.ApplyFrame();
            }
        }

        static void Save(string path,int width=900,int height=1600)
        {
            var rt = new RenderTexture(width, height, 24) { antiAliasing = 4 };
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            var oldTarget = cam.targetTexture; var oldActive = RenderTexture.active;
            try { GolfWindSway.Push(); cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0); tex.Apply(); File.WriteAllBytes(path, tex.EncodeToPNG()); }
            finally { cam.targetTexture = oldTarget; RenderTexture.active = oldActive; rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(tex); }
        }
        static void Finish(int result)
        {
            SessionState.SetBool(Key, false); GolfWindSway.FreezeTime = null; GolfWindSway.ForcedWind = null;
            EditorApplication.Exit(result);
        }
    }
}
