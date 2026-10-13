using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using GolfArcade.Game;
using GolfArcade.Tennis;
using UnityEditor;
using UnityEditor.Media;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace GolfArcade.EditorTools
{
    /// MODEL_TURNAROUND: viewing/capture only. Builds each hero the way the game does (prefab or HeroGolfer.Build, MatchHeroLook.OwnMaterials,
    /// the match racket scale, the Ready clip with the same grip / garment correctives as MatchHeroLockerExport), bakes the pose to static meshes
    /// and photographs it in a plain grey studio. Nothing is saved to any asset; the scene is a throwaway.
    ///   Menu:  Tools > Hero Turnaround > Capture All Heroes
    ///   Shell: work/model-turnaround/run.sh   (Unity -batchmode, graphics on, -executeMethod GolfArcade.EditorTools.HeroTurnaround.RunBatch)
    /// Env (optional): TURNAROUND_OUT=<dir>  TURNAROUND_HEROES=MatchHero_Male,...  TURNAROUND_VIDEO=0
    public static class HeroTurnaround
    {
        const string PrefabDir = "Assets/Resources/Tennis/Customization/";
        static readonly string[] AllHeroes = { "MatchHero_Male", "MatchHero_Female", "GolfKitHero_Male", "GolfKitHero_Female" };
        static readonly string[] OffFlags = { "VISUAL_CHARACTER_SKIN_POLISH", "VISUAL_BLINK_NORMAL_FIELD" };
        static string OutRoot => Path.GetFullPath(Environment.GetEnvironmentVariable("TURNAROUND_OUT") ?? Path.Combine(Application.dataPath, "../../work/model-turnaround"));

        const int FullW = 1080, FullH = 1440, CloseW = 1080, CloseH = 1080, Msaa = 8;
        static readonly Color Background = new Color(.47f, .47f, .48f);
        // the look of the "male UNITY / female UNITY" columns in work/lobby-parity/proof/bodies_unity_vs_lobby.png (CaptureLobbyParity case "s3")
        const string LookSkin = "B7744A", LookShirt = "2F6FD6", LookLower = "F4F0E6", LookShoes = "FFFFFF";
        /// TURNAROUND_LOOK=target (proof only): the hues of ArtDir/hero/targets-2026-10-12/clothing-v2 (picks through SetKit, then the trims the
        /// targets show set straight on the hero's own materials). The game's picks and trim rules are unchanged; the default look is the s3 case above.
        static bool TargetLook => Environment.GetEnvironmentVariable("TURNAROUND_LOOK") == "target";
        static void ApplyTargetLook(string id, MatchHeroLook look)
        {
            bool golf = id.StartsWith("GolfKit"), female = id.EndsWith("Female");
            string shirt = golf ? (female ? "2E58C0" : "2C55B8") : (female ? "F4F4F1" : "ECE8DE");
            string lower = golf ? (female ? "E6DFD1" : "1F2C4E") : (female ? "F4F4F1" : "24324F");
            look.SetKit(shirt, lower, "FFFFFF");
            ColorUtility.TryParseHtmlString("#22305A", out var navy);
            var t = new Dictionary<string, Color>();
            if (!golf) { t["Kit_Shirt"] = navy; t["Kit_Shoe"] = navy; t["Kit_Sole"] = navy; t["Kit_Shorts"] = navy; t["Kit_ShortsBand"] = navy; }
            else { t["Kit_Shirt"] = MatchHeroLook.KitTint(MatchHeroLook.KitDerive(ColorUtility.TryParseHtmlString("#" + shirt, out var c) ? c : Color.white)); t["Kit_Sole"] = new Color(.80f, .80f, .78f); }
            foreach (var r in look.kit) if (r) foreach (var m in r.sharedMaterials)
                {
                    if (!m) continue; string role = m.name.Replace(" (hero)", "");
                    if (t.TryGetValue(role, out var col) && m.HasProperty("_TrimColor")) m.SetColor("_TrimColor", MatchHeroLook.KitTint(col));
                    if (!golf && female && role == "Kit_ShortsBand" && m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", MatchHeroLook.KitTint(navy));
                    if (!golf && !female && role == "Kit_ShortsBand" && m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", MatchHeroLook.KitTint(navy));
                }
        }

        [MenuItem("Tools/Hero Turnaround/Capture All Heroes")]
        public static void CaptureFromMenu()
        {
            if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Hero Turnaround", "Stop Play mode first.", "OK"); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string previous = EditorSceneManager.GetActiveScene().path;
            try { Capture(); EditorUtility.RevealInFinder(OutRoot); }
            finally {
                EditorUtility.ClearProgressBar();
                if (!string.IsNullOrEmpty(previous)) EditorSceneManager.OpenScene(previous);
                else EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects);
            }
        }

        /// Unity -batchmode -projectPath <Unity> -executeMethod GolfArcade.EditorTools.HeroTurnaround.RunBatch   (no -nographics)
        public static void RunBatch()
        {
            int code = 0;
            try { Capture(); } catch (Exception e) { Debug.LogException(e); code = 1; }
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        // ------------------------------------------------------------------ studio

        class Part
        {
            public string name, kind; public GameObject go; public Vector3[] local; public byte[] flags;
            public bool Equipment => kind == "equipment";
        }
        const byte Head = 1, HandL = 2, HandR = 4, Feet = 8, Top = 16, Bottom = 32;

        class Shot { public string file, label; public bool full; public string section; }

        static Camera cam;
        static RenderTexture fullRT, closeRT;
        static Texture2D fullTex, closeTex;
        static readonly StringBuilder log = new StringBuilder();

        public static void Capture()
        {
            var keep = OffFlags.ToDictionary(k => k, Environment.GetEnvironmentVariable);
            foreach (var k in OffFlags) Environment.SetEnvironmentVariable(k, null);   // the game's default look: both flags OFF
            var keepWeights = QualitySettings.skinWeights;
            QualitySettings.skinWeights = SkinWeights.FourBones;                       // what HeroGolfer.Build / the court use
            string filter = Environment.GetEnvironmentVariable("TURNAROUND_HEROES");
            var heroes = string.IsNullOrEmpty(filter) ? AllHeroes : filter.Split(',').Select(s => s.Trim()).Where(AllHeroes.Contains).ToArray();
            try
            {
                Directory.CreateDirectory(OutRoot);
                log.Clear();
                log.AppendLine("Hero turnaround " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + "  Unity " + Application.unityVersion);
                log.AppendLine(string.Join("; ", OffFlags.Select(k => k + "=" + (Environment.GetEnvironmentVariable(k) ?? "UNSET"))));
                for (int i = 0; i < heroes.Length; i++) CaptureHero(heroes[i], i, heroes.Length);
                File.WriteAllText(Path.Combine(OutRoot, "report.txt"), log.ToString());
            }
            finally
            {
                foreach (var kv in keep) Environment.SetEnvironmentVariable(kv.Key, kv.Value);
                QualitySettings.skinWeights = keepWeights;
                Release();
            }
        }

        static void Progress(string what, float t)
        {
            if (!Application.isBatchMode) EditorUtility.DisplayProgressBar("Hero Turnaround", what, t);
            else Debug.Log("[HeroTurnaround] " + what);
        }

        static void Studio(bool golf)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            cam = new GameObject("TurnaroundCamera").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Background; cam.nearClipPlane = .05f; cam.enabled = false;
            var urp = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            urp.renderPostProcessing = false; urp.renderShadows = true; urp.antialiasing = AntialiasingMode.None;

            RenderSettings.skybox = null; RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            Color sky = new Color(.50f, .52f, .56f), equator = new Color(.40f, .40f, .41f), ground = new Color(.26f, .25f, .24f);
            RenderSettings.ambientSkyColor = sky; RenderSettings.ambientEquatorColor = equator; RenderSettings.ambientGroundColor = ground;
            var sh = new SphericalHarmonicsL2(); sh.AddAmbientLight(equator.linear);
            sh.AddDirectionalLight(Vector3.up, (sky.linear - equator.linear) * 1.6f, 1); sh.AddDirectionalLight(Vector3.down, (ground.linear - equator.linear) * 1.6f, 1);
            RenderSettings.ambientProbe = sh;   // set directly: edit mode does not rebuild the probe for a fresh scene

            // soft key (camera-left, high, the only shadow caster), cool fill (camera-right, low) and a faint back light for the silhouette.
            // The lights stay put relative to the camera; the hero turns on a turntable, so every angle is lit the same way.
            var key = MakeLight("Key", 1.15f, new Color(1f, .97f, .93f), 38, 35, true);
            MakeLight("Fill", .45f, new Color(.92f, .96f, 1f), 12, -55, false);
            MakeLight("Back", .35f, new Color(1f, .98f, .95f), 30, 165, false);

            if (golf) HeroLightingProfile.Golf(key, sky, equator, ground, .35f, 1.05f);   // what GolfAtmosphere sets on a course, fed with the studio light
            else HeroLightingProfile.Tennis();                                           // what every non-golf scene sets
            log.AppendLine("  shader profile: " + (golf ? "HeroLightingProfile.Golf(studio key, studio ambient, rim .35, cloth 1.05)" : "HeroLightingProfile.Tennis()") +
                           $"  _HeroSkinPolish={Shader.GetGlobalFloat("_HeroSkinPolish")}");

            if (!fullRT) { fullRT = MakeRT(FullW, FullH); closeRT = MakeRT(CloseW, CloseH); fullTex = new Texture2D(FullW, FullH, TextureFormat.RGBA32, false); closeTex = new Texture2D(CloseW, CloseH, TextureFormat.RGBA32, false); }
        }

        static Light MakeLight(string name, float intensity, Color color, float elevation, float azimuth, bool shadows)
        {
            // azimuth: degrees from the camera axis toward camera-left
            var l = new GameObject(name + "Light").AddComponent<Light>();
            l.type = LightType.Directional; l.intensity = intensity; l.color = color;
            float e = elevation * Mathf.Deg2Rad, a = azimuth * Mathf.Deg2Rad;
            var from = new Vector3(-Mathf.Sin(a) * Mathf.Cos(e), Mathf.Sin(e), Mathf.Cos(a) * Mathf.Cos(e));
            l.transform.rotation = Quaternion.LookRotation(-from);
            l.shadows = shadows && Environment.GetEnvironmentVariable("TURNAROUND_SHADOWS") != "0" ? LightShadows.Soft : LightShadows.None; l.shadowStrength = .75f; l.shadowBias = .02f; l.shadowNormalBias = .3f;
            return l;
        }

        static RenderTexture MakeRT(int w, int h)
        {
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = Msaa };
            rt.Create(); return rt;
        }

        static void Release()
        {
            if (fullRT) { fullRT.Release(); Object.DestroyImmediate(fullRT); }
            if (closeRT) { closeRT.Release(); Object.DestroyImmediate(closeRT); }
            if (fullTex) Object.DestroyImmediate(fullTex);
            if (closeTex) Object.DestroyImmediate(closeTex);
        }

        // ------------------------------------------------------------------ hero

        /// The game's static mesh / material caches outlive a scene change, but the meshes they hold are unloaded with the scene.
        /// Drop the dead entries so each hero is prepared as in a fresh session (read-only for assets; only in-memory caches are touched).
        static void PurgeDeadCaches()
        {
            int dropped = 0;
            foreach (var type in typeof(MatchHeroLook).Assembly.GetTypes())
                foreach (var field in type.GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public))
                {
                    if (field.IsLiteral || !(field.GetValue(null) is System.Collections.IDictionary dict)) continue;
                    var dead = new List<object>();
                    foreach (System.Collections.DictionaryEntry e in dict)
                        if ((e.Key is Object k && !k) || (e.Value is Object v && !v)) dead.Add(e.Key);
                    foreach (var k in dead) dict.Remove(k);
                    dropped += dead.Count;
                }
            if (dropped > 0) log.AppendLine($"  dropped {dropped} stale cache entries (meshes unloaded with the previous scene)");
        }

        static GameObject BuildHero(string id, out AnimationClip ready, out Matrix4x4? ironSocket)
        {
            PurgeDeadCaches();
            bool golf = id.StartsWith("GolfKit"), female = id.EndsWith("Female");
            string sex = female ? "Female" : "Male";
            GameObject root; ironSocket = null;
            if (golf)
            {
                // same call MatchHeroLockerExport.RunGolf makes for GolfKitHero_<Sex>
                var golfer = HeroGolfer.Build(null, new HeroLook { Female = female, Haircut = (int)HeroGolfer.Haircut.Bald, Skin = female ? new Color(.953f, .855f, .788f) : new Color(.949f, .816f, .686f), HairColor = new Color(.35f, .25f, .18f) });
                if (golfer == null) throw new FileNotFoundException("golf hero " + sex);
                root = golfer.Root;
            }
            else
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "Player" + sex + ".prefab");
                if (!prefab) throw new FileNotFoundException(PrefabDir + "Player" + sex + ".prefab");
                root = (GameObject)Object.Instantiate(prefab);
            }
            root.name = id;
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); root.transform.localScale = Vector3.one;
            var driver = root.GetComponent<HeroTennisDriver>();
            if (driver) driver.ResolvePerformanceClips();
            if (golf)
            {
                var finish = typeof(GolferView).GetMethod("ClubMaterial", BindingFlags.Static | BindingFlags.NonPublic);
                if (finish != null)
                    foreach (var club in root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r => r.name.StartsWith("CLUB_")))
                        club.sharedMaterials = club.sharedMaterials.Select(m => (Material)finish.Invoke(null, new object[] { m.name.Replace(" (Instance)", "") })).ToArray();
            }
            // Awake does not run in edit mode: move the hero's materials onto the game shaders exactly as Awake would
            var look = root.GetComponent<MatchHeroLook>();
            var own = typeof(MatchHeroLook).GetMethod("OwnMaterials", BindingFlags.Instance | BindingFlags.NonPublic);
            if (!look || own == null) throw new InvalidOperationException("MatchHeroLook.OwnMaterials not found on " + id);
            own.Invoke(look, null);
            ColorUtility.TryParseHtmlString("#" + LookSkin, out var tone); look.SetSkin(tone);
            look.SetKit(LookShirt, LookLower, LookShoes);
            if (TargetLook) ApplyTargetLook(id, look);
            if (!golf) root.GetComponentsInChildren<Transform>(true).First(t => t.name == "Racket_Classic").localScale *= TennisRules.HeroRacketScale;
            foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true)) { smr.quality = SkinQuality.Bone4; smr.updateWhenOffscreen = true; }

            if (golf)
            {
                var clips = Resources.LoadAll<AnimationClip>(HeroGolfer.PathFor(female)).Where(c => !c.name.StartsWith("__preview__")).ToDictionary(c => c.name);
                ready = clips["Idle"];
                // the game shows one club at a time (GolferView); the menu idle holds the iron
                foreach (var club in root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r => r.name.StartsWith("CLUB_"))) club.gameObject.SetActive(club.name == "CLUB_IRON");
                // the menu Idle parks the club off-stage; carry it in the iron address grip like the lobby export does
                var iron = clips["IronSwing"];
                var saved = root.GetComponentsInChildren<Transform>(true).Select(t => (t, t.localPosition, t.localRotation, t.localScale)).ToArray();
                iron.SampleAnimation(root, 0);
                var hand = Find(root.transform, "RightHand"); var clubT = Find(root.transform, "Club");
                if (hand && clubT) ironSocket = hand.worldToLocalMatrix * clubT.localToWorldMatrix;
                foreach (var s in saved) { s.t.localPosition = s.Item2; s.t.localRotation = s.Item3; s.t.localScale = s.Item4; }
            }
            else ready = driver.slots.First(s => s.id == HeroTennisDriver.Clip.Ready).clip;
            return root;
        }

        static Transform Find(Transform root, string name) => root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);

        static void Correctives(GameObject root)
        {
            var look = root.GetComponent<MatchHeroLook>();
            HeroGripPolish.ApplyGrip(look);
            look?.body?.GetComponent<HeroArmSkinning>()?.Apply();
            root.GetComponent<GolfGarmentCorrectives>()?.Apply();
            root.GetComponent<HeroGarmentPoseCorrectives>()?.Apply();
            root.GetComponent<TailoredPoloDeformation>()?.Apply();
            if (look && look.kitV2) { KitV2PoseCorrectives.Attach(look); root.GetComponent<KitV2PoseCorrectives>()?.Apply(); }
        }

        /// Ready stance at t=0 (MatchHeroLockerExport.SamplePose).
        static void ReadyPose(GameObject root, AnimationClip ready, Matrix4x4? ironSocket)
        {
            ready.SampleAnimation(root, 0);
            if (ironSocket.HasValue)
            {
                var hand = Find(root.transform, "RightHand"); var club = Find(root.transform, "Club");
                var m = club.parent.worldToLocalMatrix * hand.localToWorldMatrix * ironSocket.Value;
                club.localPosition = m.GetColumn(3);
                club.localRotation = Quaternion.LookRotation(m.GetColumn(2), m.GetColumn(1));
                club.localScale = new Vector3(m.GetColumn(0).magnitude, m.GetColumn(1).magnitude, m.GetColumn(2).magnitude);
            }
            Correctives(root);
        }

        /// The body's bind pose (the pose the mesh was skinned in).
        static void RestPose(GameObject root)
        {
            var body = root.GetComponent<MatchHeroLook>().body;
            var bones = body.bones; var bind = body.sharedMesh.bindposes; var l2w = body.transform.localToWorldMatrix;
            int Depth(Transform t) { int d = 0; for (; t; t = t.parent) d++; return d; }
            foreach (int i in Enumerable.Range(0, bones.Length).Where(i => bones[i]).OrderBy(i => Depth(bones[i])))
            {
                var m = l2w * bind[i].inverse;
                bones[i].SetPositionAndRotation(m.GetColumn(3), Quaternion.LookRotation(m.GetColumn(2), m.GetColumn(1)));
            }
            Correctives(root);
        }

        static List<Part> Bake(GameObject root, Transform pivot, out List<Renderer> shown)
        {
            var look = root.GetComponent<MatchHeroLook>();
            var racket = Find(root.transform, "Racket_Classic");
            var head = Find(root.transform, "Head"); var handL = Find(root.transform, "LeftHand"); var handR = Find(root.transform, "RightHand");
            var footL = Find(root.transform, "LeftFoot"); var footR = Find(root.transform, "RightFoot");
            byte Flag(Transform t)
            {
                if (!t) return 0; byte f = 0;
                if (head && t.IsChildOf(head)) f |= Head;
                if (handL && t.IsChildOf(handL)) f |= HandL;
                if (handR && t.IsChildOf(handR)) f |= HandR;
                if ((footL && t.IsChildOf(footL)) || (footR && t.IsChildOf(footR))) f |= Feet;
                return f;
            }
            string Kind(Renderer r)
            {
                if (r.name.StartsWith("CLUB_", StringComparison.OrdinalIgnoreCase) || (racket && r.transform.IsChildOf(racket)) || r == look.racketFrame) return "equipment";
                if (r == look.body) return "body";
                if (r == look.face) return "face";
                if (r.name.IndexOf("Hair", StringComparison.OrdinalIgnoreCase) >= 0) return "hair";
                if (r.name.StartsWith("Kit_")) return "kit";
                return "other";
            }
            var parts = new List<Part>(); shown = new List<Renderer>();
            foreach (var r in root.GetComponentsInChildren<Renderer>(false).Where(r => r.enabled))
            {
                shown.Add(r);
                if (r.name.StartsWith("Collider")) continue;
                Mesh mesh; byte[] flags; Matrix4x4 place;
                if (r is SkinnedMeshRenderer smr)
                {
                    if (!smr.sharedMesh) continue;
                    var baked = new Mesh(); smr.BakeMesh(baked, true);
                    mesh = Object.Instantiate(smr.sharedMesh); mesh.name = smr.sharedMesh.name + " (turnaround pose)";
                    if (mesh.blendShapeCount > 0) mesh.ClearBlendShapes();
                    mesh.vertices = baked.vertices; mesh.normals = baked.normals;
                    if (baked.tangents.Length == mesh.vertexCount) mesh.tangents = baked.tangents;
                    mesh.RecalculateBounds(); Object.DestroyImmediate(baked);
                    var boneFlags = smr.bones.Select(Flag).ToArray(); var w = smr.sharedMesh.boneWeights;
                    flags = new byte[mesh.vertexCount];
                    if (w.Length == flags.Length) for (int i = 0; i < flags.Length; i++) flags[i] = boneFlags.Length > 0 ? boneFlags[w[i].boneIndex0] : (byte)0;
                    place = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
                }
                else if (r is MeshRenderer && r.TryGetComponent<MeshFilter>(out var mf) && mf.sharedMesh)
                {
                    mesh = mf.sharedMesh; byte f = Flag(r.transform); flags = Enumerable.Repeat(f, mesh.vertexCount).ToArray();
                    place = r.transform.localToWorldMatrix;
                }
                else continue;
                var go = new GameObject("Baked_" + r.name) { layer = r.gameObject.layer };
                go.transform.SetPositionAndRotation(place.GetColumn(3), Quaternion.LookRotation(place.GetColumn(2), place.GetColumn(1)));
                go.transform.localScale = new Vector3(place.GetColumn(0).magnitude, place.GetColumn(1).magnitude, place.GetColumn(2).magnitude);
                go.transform.SetParent(pivot, true);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterials = r.sharedMaterials; mr.shadowCastingMode = r.shadowCastingMode; mr.receiveShadows = r.receiveShadows; mr.renderingLayerMask = r.renderingLayerMask;
                if (r.HasPropertyBlock()) { var block = new MaterialPropertyBlock(); r.GetPropertyBlock(block); mr.SetPropertyBlock(block); }
                var part = new Part { name = r.name, kind = Kind(r), go = go, local = mesh.vertices, flags = flags };
                if (part.kind == "kit")
                {
                    byte region = 0;
                    if (System.Text.RegularExpressions.Regex.IsMatch(r.name, "Top|Shirt|Polo|Jacket|Vest|Sweater", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) region = Top;
                    else if (System.Text.RegularExpressions.Regex.IsMatch(r.name, "Bottom|Short|Skirt|Pant|Trouser", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) region = Bottom;
                    if (region != 0) for (int i = 0; i < flags.Length; i++) flags[i] |= region;
                }
                parts.Add(part);
            }
            foreach (var r in root.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            return parts;
        }

        // ------------------------------------------------------------------ framing

        static List<Vector3> Points(List<Part> parts, byte mask, bool withEquipment, int stride = 1)
        {
            var list = new List<Vector3>();
            foreach (var p in parts)
            {
                if (!p.go.activeSelf || (p.Equipment && !withEquipment)) continue;
                var m = p.go.transform.localToWorldMatrix;
                for (int i = 0; i < p.local.Length; i += stride) if (mask == 0 || (p.flags[i] & mask) != 0) list.Add(m.MultiplyPoint3x4(p.local[i]));
            }
            return list;
        }

        static Vector3 ViewDir(float elevation) { float e = elevation * Mathf.Deg2Rad; return new Vector3(0, Mathf.Sin(e), Mathf.Cos(e)); }

        /// Camera distance that keeps every point inside the frame (padding = fraction of the half-frame), looking along -view at centre.
        static float Distance(List<Vector3> pts, Vector3 centre, Vector3 view, float fov, float aspect, float pad)
        {
            var rot = Quaternion.LookRotation(-view, Vector3.up); Vector3 right = rot * Vector3.right, up = rot * Vector3.up;
            float tv = Mathf.Tan(fov * .5f * Mathf.Deg2Rad) / pad, th = tv * aspect, d = 0;
            foreach (var p in pts)
            {
                var q = p - centre; float z = Vector3.Dot(q, view);
                d = Mathf.Max(d, z + Mathf.Abs(Vector3.Dot(q, right)) / th, z + Mathf.Abs(Vector3.Dot(q, up)) / tv);
            }
            return d;
        }

        /// Centre of the points' projected extent on the view plane.
        static Vector3 Centre(List<Vector3> pts, Vector3 view)
        {
            var rot = Quaternion.LookRotation(-view, Vector3.up); Vector3 right = rot * Vector3.right, up = rot * Vector3.up;
            Vector3 mean = Vector3.zero; foreach (var p in pts) mean += p; mean /= Mathf.Max(1, pts.Count);
            float x0 = float.MaxValue, x1 = float.MinValue, y0 = float.MaxValue, y1 = float.MinValue;
            foreach (var p in pts) { var q = p - mean; float x = Vector3.Dot(q, right), y = Vector3.Dot(q, up); x0 = Mathf.Min(x0, x); x1 = Mathf.Max(x1, x); y0 = Mathf.Min(y0, y); y1 = Mathf.Max(y1, y); }
            return mean + right * ((x0 + x1) * .5f) + up * ((y0 + y1) * .5f);
        }

        static void Place(Vector3 centre, Vector3 view, float distance, float fov)
        {
            cam.fieldOfView = fov;
            cam.transform.SetPositionAndRotation(centre + view * distance, Quaternion.LookRotation(-view, Vector3.up));
            cam.farClipPlane = distance + 3f;   // URP clamps the shadow range to the far plane: a tight far plane = a sharp self-shadow
        }

        static Texture2D Render(bool full)
        {
            var rt = full ? fullRT : closeRT; var tex = full ? fullTex : closeTex;
            cam.targetTexture = rt; cam.aspect = (float)rt.width / rt.height; cam.Render();
            var active = RenderTexture.active; RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply(false);
            RenderTexture.active = active; cam.targetTexture = null;
            return tex;
        }

        static float FaceYaw(List<Part> parts, Transform pivot)
        {
            // the face decals sit in front of the skull: their centroid minus the head centroid is where the face looks
            var headPts = Points(parts, Head, false, 7); var facePts = new List<Vector3>();
            foreach (var p in parts.Where(p => p.kind == "face")) { var m = p.go.transform.localToWorldMatrix; for (int i = 0; i < p.local.Length; i += 3) facePts.Add(m.MultiplyPoint3x4(p.local[i])); }
            if (headPts.Count == 0 || facePts.Count == 0) return 0;
            var dir = facePts.Aggregate(Vector3.zero, (a, b) => a + b) / facePts.Count - headPts.Aggregate(Vector3.zero, (a, b) => a + b) / headPts.Count;
            dir = Quaternion.Inverse(pivot.rotation) * dir; dir.y = 0;
            return dir.sqrMagnitude < 1e-8f ? 0 : Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        }

        // ------------------------------------------------------------------ capture

        static readonly (string id, string label, float angle)[] Turn = {
            ("front", "front", 0), ("front_left", "front-left", 45), ("left", "left", 90), ("back_left", "back-left", 135),
            ("back", "back", 180), ("back_right", "back-right", 225), ("right", "right", 270), ("front_right", "front-right", 315) };

        static void CaptureHero(string id, int index, int count)
        {
            bool golf = id.StartsWith("GolfKit");
            string dir = Path.Combine(OutRoot, id); Directory.CreateDirectory(dir);
            foreach (var old in Directory.GetFiles(dir, "*.png").Concat(Directory.GetFiles(dir, "*.mp4"))) File.Delete(old);
            Progress(id + ": building", (float)index / count);
            log.AppendLine("== " + id);
            Studio(golf);
            var root = BuildHero(id, out var ready, out var ironSocket);
            var shots = new List<Shot>();
            var pivot = new GameObject("Turntable").transform;

            // rest pose first (bones still at their imported values), then the ready stance
            RestPose(root);
            var restParts = Bake(root, pivot, out var shown);
            log.AppendLine("  parts: " + string.Join(", ", restParts.Select(p => $"{p.name}[{p.kind}] v={p.local.Length}")));
            log.AppendLine($"  tris: {restParts.Where(p => !p.Equipment).Sum(p => p.go.GetComponent<MeshFilter>().sharedMesh.triangles.Length / 3)} (hero) + {restParts.Where(p => p.Equipment).Sum(p => p.go.GetComponent<MeshFilter>().sharedMesh.triangles.Length / 3)} (equipment)");
            log.AppendLine("  top kit: " + string.Join(", ", restParts.Where(p => p.flags.Any(f => (f & Top) != 0)).Select(p => p.name)) +
                           "; bottom kit: " + string.Join(", ", restParts.Where(p => p.flags.Any(f => (f & Bottom) != 0)).Select(p => p.name)));
            foreach (var r in shown) r.enabled = true;
            ReadyPose(root, ready, ironSocket);
            var readyParts = Bake(root, pivot, out _);
            log.AppendLine(TargetLook ? $"  look: skin {LookSkin}, TARGET hues of clothing-v2 (proof only)" : $"  look: skin {LookSkin}, shirt {LookShirt}, shorts/skirt {LookLower}, shoes {LookShoes} (lobby-parity case s3)");
            log.AppendLine($"  ready pose: {ready.name} t=0" + (ironSocket.HasValue ? " + menu iron socket" : ""));

            // turntable axis: the horizontal centre of the rest-pose body; heading: where the rest-pose face looks
            foreach (var p in readyParts) p.go.SetActive(false);
            var restBody = Points(restParts, 0, false, 5);
            var b = new Bounds(restBody[0], Vector3.zero); foreach (var v in restBody) b.Encapsulate(v);
            foreach (var p in restParts.Concat(readyParts)) p.go.transform.SetParent(null, true);
            pivot.position = new Vector3(b.center.x, 0, b.center.z);
            foreach (var p in restParts.Concat(readyParts)) p.go.transform.SetParent(pivot, true);
            float heading = FaceYaw(restParts, pivot);
            log.AppendLine($"  rest bounds {b.size.x:F3} x {b.size.y:F3} x {b.size.z:F3} m; turntable heading {heading:F1} deg");

            foreach (var (pose, poseLabel, parts, other) in new[] { ("ready", "Ready stance", readyParts, restParts), ("rest", "Rest pose (bind)", restParts, readyParts) })
            {
                foreach (var p in other) p.go.SetActive(false);
                foreach (var p in parts) p.go.SetActive(true);
                bool equipment = pose == "ready";
                foreach (var p in parts.Where(p => p.Equipment)) p.go.SetActive(equipment);
                Progress($"{id}: {poseLabel}", (index + (pose == "ready" ? .2f : .6f)) / count);

                // full body: one fixed camera for all eight angles (max distance over a full turn), the hero turns
                const float turnFov = 22;
                float aspect = (float)FullW / FullH;
                var all = Points(parts, 0, true, 3);
                var yb = new Bounds(all[0], Vector3.zero); foreach (var v in all) yb.Encapsulate(v);
                var axis = new Vector3(pivot.position.x, yb.center.y, pivot.position.z);
                float turnDistance = 0;
                for (int a = 0; a < 360; a += 10)
                {
                    pivot.rotation = Quaternion.Euler(0, a - heading, 0);
                    turnDistance = Mathf.Max(turnDistance, Distance(Points(parts, 0, true, 3), axis, ViewDir(0), turnFov, aspect, 1.06f));
                }
                // A fresh batch run draws its very first frame before the hero shaders are ready: draw it once and throw it away.
                pivot.rotation = Quaternion.Euler(0, Turn[0].angle - heading, 0); Place(axis, ViewDir(0), turnDistance, turnFov); Render(true);
                foreach (var t in Turn)
                {
                    pivot.rotation = Quaternion.Euler(0, t.angle - heading, 0);
                    Place(axis, ViewDir(0), turnDistance, turnFov);
                    Save(shots, dir, $"{pose}_full_{t.id}", $"{poseLabel} · {t.label}", true, pose + "_full");
                }
                foreach (var (sid, label, angle, elevation) in new[] { ("top", "top (60° above)", 20f, 60f), ("low", "low (camera at the feet)", 20f, -14f) })
                {
                    pivot.rotation = Quaternion.Euler(0, angle - heading, 0);
                    var pts = Points(parts, 0, true, 3); var view = ViewDir(elevation); var c = Centre(pts, view);
                    Place(c, view, Distance(pts, c, view, 34, aspect, 1.06f), 34);
                    Save(shots, dir, $"{pose}_full_{sid}", $"{poseLabel} · {label}", true, pose + "_full");
                }

                // close-ups. The racket / club is hidden for the face (the ready grip holds it across the chin), kept for hands.
                pivot.rotation = Quaternion.identity; float faceHeading = FaceYaw(parts, pivot);   // per pose: the ready head may be turned
                void Close(string sid, string label, byte mask, float angle, float elevation, bool face, bool hideEquipment, float pad, float fov = 20)
                {
                    foreach (var p in parts.Where(p => p.Equipment)) p.go.SetActive(equipment && !hideEquipment);
                    pivot.rotation = Quaternion.Euler(0, angle - (face ? faceHeading : heading), 0);
                    var pts = Points(parts, mask, false, 1);
                    if (pts.Count == 0) { log.AppendLine($"  SKIP {pose}_{sid}: no vertices for region {mask}"); return; }
                    var view = ViewDir(elevation); var c = Centre(pts, view);
                    Place(c, view, Distance(pts, c, view, fov, (float)CloseW / CloseH, pad), fov);
                    Save(shots, dir, $"{pose}_{sid}", $"{poseLabel} · {label}", false, pose + "_close");
                }
                Close("face_front", "face front", Head, 0, 2, true, true, 1.12f);
                Close("face_three_quarter", "face three-quarter", Head, 40, 2, true, true, 1.12f);
                Close("face_side", "face side (left)", Head, 90, 2, true, true, 1.12f);
                Close("hand_left_front", "left hand · front", HandL, 0, 5, false, false, 1.25f);
                Close("hand_left_outside", "left hand · outside", HandL, 90, 5, false, false, 1.25f);
                Close("hand_right_front", "right hand · front", HandR, 0, 5, false, false, 1.25f);
                Close("hand_right_outside", "right hand · outside", HandR, 270, 5, false, false, 1.25f);
                Close("shoes_front", "shoes · front", Feet, 0, 12, false, true, 1.12f);
                Close("shoes_left", "shoes · left", Feet, 90, 12, false, true, 1.12f);
                Close("shoes_back", "shoes · back", Feet, 180, 12, false, true, 1.12f);
                Close("shoes_right", "shoes · right", Feet, 270, 12, false, true, 1.12f);
                Close("top_front", "shirt · front", Top, 0, 3, false, true, 1.1f, 24);
                Close("top_back", "shirt · back", Top, 180, 3, false, true, 1.1f, 24);
                Close("bottom_front", Lower(id) + " · front", Bottom, 0, 0, false, true, 1.1f, 24);
                Close("bottom_back", Lower(id) + " · back", Bottom, 180, 0, false, true, 1.1f, 24);
                foreach (var p in parts.Where(p => p.Equipment)) p.go.SetActive(equipment);
            }

            // 10 s, 30 fps, 360° turn of the ready stance (same fixed camera as the turnaround)
            if (Environment.GetEnvironmentVariable("TURNAROUND_VIDEO") != "0")
            {
                Progress(id + ": spin video", (index + .9f) / count);
                foreach (var p in restParts) p.go.SetActive(false);
                foreach (var p in readyParts) p.go.SetActive(true);
                var all = Points(readyParts, 0, true, 3);
                var yb = new Bounds(all[0], Vector3.zero); foreach (var v in all) yb.Encapsulate(v);
                var axis = new Vector3(pivot.position.x, yb.center.y, pivot.position.z);
                float d = 0; float aspect = (float)FullW / FullH;
                for (int a = 0; a < 360; a += 10) { pivot.rotation = Quaternion.Euler(0, a - heading, 0); d = Mathf.Max(d, Distance(Points(readyParts, 0, true, 3), axis, ViewDir(0), 22, aspect, 1.06f)); }
                string video = Path.Combine(dir, id + "_spin.mp4");
                var attrs = new VideoTrackAttributes { frameRate = new MediaRational(30), width = FullW, height = FullH, includeAlpha = false, bitRateMode = VideoBitrateMode.High };
                using (var encoder = new MediaEncoder(video, attrs))
                    for (int f = 0; f < 300; f++)
                    {
                        pivot.rotation = Quaternion.Euler(0, f * 360f / 300f - heading, 0);
                        Place(axis, ViewDir(0), d, 22);
                        encoder.AddFrame(Render(true));
                    }
                log.AppendLine("  video: " + Path.GetFileName(video) + " 300 frames @30 fps " + FullW + "x" + FullH);
            }

            Object.DestroyImmediate(root);
            Object.DestroyImmediate(pivot.gameObject);
            Progress(id + ": contact sheet", (index + .95f) / count);
            ContactSheet(id, dir, shots);
            log.AppendLine($"  {shots.Count} stills + contact sheet -> {dir}");
        }

        static string Lower(string id) => id.EndsWith("Female") ? "skirt" : id.StartsWith("GolfKit") ? "trousers" : "shorts";

        static void Save(List<Shot> shots, string dir, string file, string label, bool full, string section)
        {
            var tex = Render(full);
            File.WriteAllBytes(Path.Combine(dir, file + ".png"), tex.EncodeToPNG());
            shots.Add(new Shot { file = file, label = label, full = full, section = section });
        }

        // ------------------------------------------------------------------ contact sheet

        static void ContactSheet(string id, string dir, List<Shot> shots)
        {
            const int cols = 10, tile = 340, gap = 12, margin = 28, labelH = 38, headerH = 54, titleH = 76;
            int fullH = tile * FullH / FullW;
            var sections = new[] { ("ready_full", "READY STANCE · full body (racket / club in hand)"), ("ready_close", "READY STANCE · close-ups (equipment hidden for face, shoes, garments)"),
                                   ("rest_full", "REST POSE (bind pose) · full body (equipment hidden)"), ("rest_close", "REST POSE · close-ups") };
            // layout
            var layout = new List<(Shot shot, Rect rect)>(); var headers = new List<(string text, float y)>();
            float y = margin + titleH;
            foreach (var (key, title) in sections)
            {
                var list = shots.Where(s => s.section == key).ToList(); if (list.Count == 0) continue;
                headers.Add((title, y)); y += headerH;
                for (int i = 0; i < list.Count; i++)
                {
                    int h = list[i].full ? fullH : tile;
                    if (i > 0 && i % cols == 0) y += (list[i - 1].full ? fullH : tile) + labelH + gap;
                    layout.Add((list[i], new Rect(margin + (i % cols) * (tile + gap), y, tile, h)));
                }
                y += (list[list.Count - 1].full ? fullH : tile) + labelH + gap * 2;
            }
            int W = margin * 2 + cols * tile + (cols - 1) * gap, H = Mathf.CeilToInt(y + margin);

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var sheetCam = new GameObject("SheetCamera").AddComponent<Camera>();
            sheetCam.orthographic = true; sheetCam.orthographicSize = H * .5f; sheetCam.clearFlags = CameraClearFlags.SolidColor;
            sheetCam.backgroundColor = new Color(.16f, .16f, .17f); sheetCam.nearClipPlane = .1f; sheetCam.farClipPlane = 100; sheetCam.enabled = false;
            sheetCam.transform.position = new Vector3(W * .5f, -H * .5f, -10);
            var urp = sheetCam.gameObject.AddComponent<UniversalAdditionalCameraData>(); urp.renderPostProcessing = false; urp.renderShadows = false;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            var made = new List<Object>();
            void Text(string s, float x, float top, int size, Color colour, TextAnchor anchor = TextAnchor.UpperLeft)
            {
                var tm = new GameObject("Label").AddComponent<TextMesh>();
                tm.font = font; tm.GetComponent<MeshRenderer>().sharedMaterial = font.material; tm.text = s; tm.fontSize = size * 2; tm.characterSize = 5f;   // glyph height ~ fontSize * characterSize / 10 px
                tm.anchor = anchor; tm.color = colour; tm.transform.position = new Vector3(x, -top, 0);
            }
            Text(id + " — model turnaround", margin, margin, 40, Color.white);
            Text("Unity studio render · game shaders (" + (id.StartsWith("GolfKit") ? "golf" : "tennis") + " hero profile) · VISUAL_CHARACTER_SKIN_POLISH / VISUAL_BLINK_NORMAL_FIELD off · " + DateTime.Now.ToString("yyyy-MM-dd"),
                 margin, margin + 46, 18, new Color(.75f, .75f, .78f));
            foreach (var (text, hy) in headers) Text(text, margin, hy + 12, 24, new Color(1f, .85f, .45f));
            foreach (var (shot, rect) in layout)
            {
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, true); tex.LoadImage(File.ReadAllBytes(Path.Combine(dir, shot.file + ".png")), false);
                tex.filterMode = FilterMode.Trilinear; tex.anisoLevel = 4; tex.wrapMode = TextureWrapMode.Clamp; tex.Apply(true); made.Add(tex);
                var mat = new Material(unlit); mat.SetTexture("_BaseMap", tex); mat.SetColor("_BaseColor", Color.white); made.Add(mat);
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad); Object.DestroyImmediate(quad.GetComponent<Collider>());
                quad.GetComponent<MeshRenderer>().sharedMaterial = mat; quad.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                quad.transform.position = new Vector3(rect.x + rect.width * .5f, -(rect.y + rect.height * .5f), 0); quad.transform.localScale = new Vector3(rect.width, rect.height, 1);
                Text(shot.label, rect.x + 2, rect.y + rect.height + 6, 15, new Color(.92f, .92f, .94f));
                Text(shot.file, rect.x + 2, rect.y + rect.height + 23, 11, new Color(.6f, .6f, .64f));
            }
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 }; rt.Create();
            sheetCam.targetTexture = rt; sheetCam.aspect = (float)W / H; sheetCam.Render();
            var active = RenderTexture.active; RenderTexture.active = rt;
            var outTex = new Texture2D(W, H, TextureFormat.RGB24, false); outTex.ReadPixels(new Rect(0, 0, W, H), 0, 0); outTex.Apply(false);
            RenderTexture.active = active; sheetCam.targetTexture = null;
            File.WriteAllBytes(Path.Combine(dir, id + "_contact_sheet.png"), outTex.EncodeToPNG());
            File.WriteAllBytes(Path.Combine(OutRoot, id + "_contact_sheet.png"), outTex.EncodeToPNG());
            rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(outTex);
            foreach (var o in made) Object.DestroyImmediate(o);
        }
    }
}
