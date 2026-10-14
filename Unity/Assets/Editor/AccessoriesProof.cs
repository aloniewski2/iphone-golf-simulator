using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using GolfArcade.Game;
using GolfArcade.Tennis;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace GolfArcade.EditorTools
{
    /// ACCESSORIES proof renders (viewing only; nothing is saved to any asset). Builds a hero the way the game does (prefab / HeroGolfer.Build + OwnMaterials -> which equips
    /// the default accessories), poses it, and photographs it in the model-turnaround studio look (same lights, ambient, shader profile).
    ///   Unity -batchmode -projectPath <clone> -executeMethod GolfArcade.EditorTools.AccessoriesProof.RunBatch   (graphics on, never -nographics)
    /// Env: ACC_PROOF_OUT=<dir>  ACC_PROOF_HEROES=MatchHero_Male,...  ACC_PROOF_SHOTS=name:kind:azimuth[:elev[:dist]],...  ACC_PROOF_POSE=rest|idle|<clip>@<seconds>
    ///      ACC_PROOF_ALONE=1 (hide body, face and kit: only the accessory pieces)  ACC_PROOF_BAG=1 (SetCarry(true))  ACC_ITEMS / ACC_DEFAULTS (see HeroAccessories)
    ///   kind = full | bust | head | hand | back | foot | body | acc (the worn accessory pieces) ; azimuth 0 = from the front, +90 = from the hero's left, 180 = back, -90 = right.
    public static class AccessoriesProof
    {
        const string PrefabDir = "Assets/Resources/Tennis/Customization/";
        static readonly string[] AllHeroes = { "MatchHero_Male", "MatchHero_Female", "GolfKitHero_Male", "GolfKitHero_Female" };
        static string OutRoot => Path.GetFullPath(Environment.GetEnvironmentVariable("ACC_PROOF_OUT") ?? Path.Combine(Application.dataPath, "../../work/accessories/proof/unity"));
        static readonly Color Background = new Color(.47f, .47f, .48f);
        const string LookSkin = "B7744A", LookShirt = "2F6FD6", LookLower = "F4F0E6", LookShoes = "FFFFFF";
        static Camera cam;

        public static void RunBatch()
        {
            int code = 0;
            try { Capture(); } catch (Exception e) { Debug.LogException(e); code = 1; }
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        static Light MakeLight(string name, float intensity, Color color, float elevation, float azimuth, bool shadows)
        {
            var l = new GameObject(name + "Light").AddComponent<Light>();
            l.type = LightType.Directional; l.intensity = intensity; l.color = color;
            float e = elevation * Mathf.Deg2Rad, a = azimuth * Mathf.Deg2Rad;
            var from = new Vector3(-Mathf.Sin(a) * Mathf.Cos(e), Mathf.Sin(e), Mathf.Cos(a) * Mathf.Cos(e));
            l.transform.rotation = Quaternion.LookRotation(-from);
            l.shadows = shadows ? LightShadows.Soft : LightShadows.None; l.shadowStrength = .75f; l.shadowBias = .02f; l.shadowNormalBias = .3f;
            return l;
        }

        static void Studio(bool golf, out Transform lightRig)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            cam = new GameObject("ProofCamera").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Background; cam.nearClipPlane = .02f; cam.enabled = false;
            var urp = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            urp.renderPostProcessing = false; urp.renderShadows = true; urp.antialiasing = AntialiasingMode.None;
            RenderSettings.skybox = null; RenderSettings.fog = false; RenderSettings.ambientMode = AmbientMode.Trilight;
            Color sky = new Color(.50f, .52f, .56f), equator = new Color(.40f, .40f, .41f), ground = new Color(.26f, .25f, .24f);
            RenderSettings.ambientSkyColor = sky; RenderSettings.ambientEquatorColor = equator; RenderSettings.ambientGroundColor = ground;
            var sh = new SphericalHarmonicsL2(); sh.AddAmbientLight(equator.linear);
            sh.AddDirectionalLight(Vector3.up, (sky.linear - equator.linear) * 1.6f, 1); sh.AddDirectionalLight(Vector3.down, (ground.linear - equator.linear) * 1.6f, 1);
            RenderSettings.ambientProbe = sh;
            var rig = new GameObject("LightRig").transform; lightRig = rig;
            var key = MakeLight("Key", 1.15f, new Color(1f, .97f, .93f), 38, 35, true);
            var fill = MakeLight("Fill", .45f, new Color(.92f, .96f, 1f), 12, -55, false);
            var back = MakeLight("Back", .35f, new Color(1f, .98f, .95f), 30, 165, false);
            foreach (var l in new[] { key, fill, back }) l.transform.SetParent(rig, true);
            if (golf) HeroLightingProfile.Golf(key, sky, equator, ground, .35f, 1.05f); else HeroLightingProfile.Tennis();
        }

        static void PurgeDeadCaches()
        {
            foreach (var type in typeof(MatchHeroLook).Assembly.GetTypes())
                foreach (var field in type.GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public))
                {
                    if (field.IsLiteral || !(field.GetValue(null) is System.Collections.IDictionary dict)) continue;
                    var dead = new List<object>();
                    foreach (System.Collections.DictionaryEntry e in dict) if ((e.Key is Object k && !k) || (e.Value is Object v && !v)) dead.Add(e.Key);
                    foreach (var k in dead) dict.Remove(k);
                }
        }

        static Transform Find(Transform root, string name) => root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);

        internal static GameObject BuildHero(string id, out AnimationClip idle)
        {
            PurgeDeadCaches();
            bool golf = id.StartsWith("GolfKit"), female = id.EndsWith("Female");
            string sex = female ? "Female" : "Male";
            GameObject root;
            if (golf)
            {
                var golfer = HeroGolfer.Build(null, new HeroLook { Female = female, Haircut = (int)HeroGolfer.Haircut.Bald, Skin = female ? new Color(.953f, .855f, .788f) : new Color(.949f, .816f, .686f), HairColor = new Color(.35f, .25f, .18f) });
                if (golfer == null) throw new FileNotFoundException("golf hero " + sex);
                root = golfer.Root;
            }
            else
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "Player" + sex + ".prefab");
                root = (GameObject)Object.Instantiate(prefab);
            }
            root.name = id;
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); root.transform.localScale = Vector3.one;
            var driver = root.GetComponent<HeroTennisDriver>();
            if (driver) driver.ResolvePerformanceClips();
            // the far-garment LOD swaps pieces by camera distance while rendering: proofs show the near meshes the game uses up close
            var lod = root.GetComponent<HeroGarmentLOD>(); if (lod) Object.DestroyImmediate(lod);
            var look = root.GetComponent<MatchHeroLook>();
            var own = typeof(MatchHeroLook).GetMethod("OwnMaterials", BindingFlags.Instance | BindingFlags.NonPublic);
            own.Invoke(look, null);                                            // ... which ends in HeroAccessories.Setup (the default accessories)
            ColorUtility.TryParseHtmlString("#" + LookSkin, out var tone); look.SetSkin(tone);
            look.SetKit(LookShirt, LookLower, LookShoes);
            if (!golf) { var rk = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Racket_Classic"); if (rk) foreach (var r in rk.GetComponentsInChildren<Renderer>(true)) r.enabled = false; }
            foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true)) { smr.quality = SkinQuality.Bone4; smr.updateWhenOffscreen = true; }
            if (golf)
            {
                foreach (var club in root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r => r.name.StartsWith("CLUB_"))) club.gameObject.SetActive(false);
                idle = Resources.LoadAll<AnimationClip>(HeroGolfer.PathFor(female)).First(c => c.name == "Idle");
            }
            else idle = driver.slots.First(s => s.id == HeroTennisDriver.Clip.Ready).clip;
            return root;
        }

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
            // sockets (children of bones) keep their local pose, so they follow
        }

        internal static void Pose(GameObject root, AnimationClip idle, string pose, Dictionary<string, AnimationClip> clips)
        {
            if (pose == "rest") { RestPose(root); return; }
            AnimationClip clip = idle; float t = 0;
            if (pose != "idle")
            {
                var parts = pose.Split('@'); if (parts.Length > 1) float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out t);
                if (!clips.TryGetValue(parts[0], out clip)) throw new InvalidOperationException("clip " + parts[0] + " not found");
            }
            clip.SampleAnimation(root, t);
            var look = root.GetComponent<MatchHeroLook>();
            HeroGripPolish.ApplyGrip(look);
            look?.body?.GetComponent<HeroArmSkinning>()?.Apply();
            root.GetComponent<GolfGarmentCorrectives>()?.Apply();
            root.GetComponent<HeroGarmentPoseCorrectives>()?.Apply();
            root.GetComponent<TailoredPoloDeformation>()?.Apply();
            if (look && look.kitV2) { KitV2PoseCorrectives.Attach(look); root.GetComponent<KitV2PoseCorrectives>()?.Apply(); }
        }

        /// Every clip the hero can play: the golf swing set or the tennis driver's slots, plus the emotion emotes / Walk / Run (Resources/HeroMoves).
        internal static Dictionary<string, AnimationClip> ClipsFor(string id, GameObject root)
        {
            bool golf = id.StartsWith("GolfKit"); string sex = id.EndsWith("Female") ? "Female" : "Male";
            var clips = golf ? Resources.LoadAll<AnimationClip>(HeroGolfer.PathFor(id.EndsWith("Female"))).Where(c => !c.name.StartsWith("__preview__")).GroupBy(c => c.name).ToDictionary(g => g.Key, g => g.First())
                : root.GetComponent<HeroTennisDriver>().slots.Where(s => s.clip).GroupBy(s => s.id.ToString()).ToDictionary(g => g.Key, g => g.First().clip);
            foreach (var m in Resources.LoadAll<AnimationClip>("HeroMoves/" + sex).Where(c => !c.name.StartsWith("__preview__"))) clips[m.name] = m;
            return clips;
        }

        struct Shot { public string name, kind; public float az, el, dist; }
        static Shot[] ParseShots(string spec)
        {
            return spec.Split(',').Select(s =>
            {
                var p = s.Split(':'); var c = System.Globalization.CultureInfo.InvariantCulture;
                return new Shot { name = p[0], kind = p[1], az = float.Parse(p[2], c), el = p.Length > 3 ? float.Parse(p[3], c) : 6f, dist = p.Length > 4 ? float.Parse(p[4], c) : 0f };
            }).ToArray();
        }

        const string DefaultShots = "front:full:0,q34:full:35,side:full:90,back:full:180,head_front:head:0,head_q34:head:38,head_side:head:90,head_back:head:180";

        public static void Capture()
        {
            var keepWeights = QualitySettings.skinWeights; QualitySettings.skinWeights = SkinWeights.FourBones;
            QualitySettings.streamingMipmapsActive = false;   // the first frame after a texture load would otherwise draw its lowest mips (blotchy garments in the first shot)
            string filter = Environment.GetEnvironmentVariable("ACC_PROOF_HEROES");
            var heroes = string.IsNullOrEmpty(filter) ? AllHeroes : filter.Split(',').Select(s => s.Trim()).Where(AllHeroes.Contains).ToArray();
            var shots = ParseShots(Environment.GetEnvironmentVariable("ACC_PROOF_SHOTS") ?? DefaultShots);
            string pose = Environment.GetEnvironmentVariable("ACC_PROOF_POSE") ?? "rest";
            bool alone = Environment.GetEnvironmentVariable("ACC_PROOF_ALONE") == "1", bag = Environment.GetEnvironmentVariable("ACC_PROOF_BAG") == "1";
            Directory.CreateDirectory(OutRoot);
            var rtFull = new RenderTexture(1080, 1440, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 8, hideFlags = HideFlags.HideAndDontSave }; rtFull.Create();
            var rtSq = new RenderTexture(1080, 1080, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 8, hideFlags = HideFlags.HideAndDontSave }; rtSq.Create();
            try
            {
                foreach (var id in heroes)
                {
                    bool golf = id.StartsWith("GolfKit");
                    Studio(golf, out var lightRig);
                    var root = BuildHero(id, out var idle);
                    var look = root.GetComponent<MatchHeroLook>();
                    var acc = HeroAccessories.For(look);
                    if (bag) acc.SetCarry(true);
                    var clips = ClipsFor(id, root);
                    Pose(root, idle, pose, clips);
                    if (alone)
                    {
                        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                            if (!r.name.StartsWith("Acc_")) r.enabled = false;
                    }
                    var head = Find(root.transform, "Head"); var hand = Find(root.transform, "LeftHand"); var chest = Find(root.transform, "UpperChest"); var foot = Find(root.transform, "LeftFoot");
                    foreach (var s in shots)
                    {
                        Vector3 target; float dist, fov = 30f; bool full = s.kind == "full" || s.kind == "body"; float el = s.el;
                        switch (s.kind)
                        {
                            case "acc":      // frame the accessory pieces that are drawn (ACC_PROOF_ALONE shows only them)
                            {
                                var b = new Bounds(); bool first = true; var scratch = new Mesh();
                                foreach (var r in acc.AllRenderers().Where(r => r.enabled))
                                {
                                    r.BakeMesh(scratch, true); var m = Matrix4x4.TRS(r.transform.position, r.transform.rotation, Vector3.one);
                                    foreach (var v in scratch.vertices) { var w = m.MultiplyPoint3x4(v); if (first) { b = new Bounds(w, Vector3.zero); first = false; } else b.Encapsulate(w); }
                                }
                                Object.DestroyImmediate(scratch);
                                target = first ? new Vector3(0, 1.5f, 0) : b.center; fov = 22;
                                dist = first ? 1.5f : b.extents.magnitude * 1.25f / Mathf.Tan(fov * .5f * Mathf.Deg2Rad);
                                break;
                            }
                            case "head": target = head.position + new Vector3(0, .115f, 0); dist = 1.25f; fov = 26; break;
                            case "bust": target = head.position + new Vector3(0, -.06f, 0); dist = 2.05f; fov = 28; break;
                            case "hand": target = hand.position + new Vector3(0, -.05f, 0); dist = .75f; fov = 26; break;
                            case "back": target = chest.position + new Vector3(0, -.15f, 0); dist = 3.2f; fov = 28; break;
                            case "foot": target = foot.position + new Vector3(0, .1f, 0); dist = 1.2f; break;
                            default: target = new Vector3(0, .88f, 0); dist = 4.7f; break;
                        }
                        if (s.dist > 0) dist = s.dist;
                        float a = s.az * Mathf.Deg2Rad, e = el * Mathf.Deg2Rad;
                        // hero faces +Z (Unity); az +90 looks at the hero's LEFT side (hero's left is -X in Unity)
                        var dir = new Vector3(-Mathf.Sin(a) * Mathf.Cos(e), Mathf.Sin(e), Mathf.Cos(a) * Mathf.Cos(e));
                        cam.fieldOfView = fov;
                        cam.transform.position = target + dir * dist; cam.transform.rotation = Quaternion.LookRotation(target - cam.transform.position, Vector3.up);
                        lightRig.rotation = Quaternion.Euler(0, -s.az, 0); lightRig.position = target;
                        // keep the same light directions relative to the camera: the rig yaws with the camera azimuth
                        var rt = full && s.kind == "full" ? rtFull : rtSq; cam.targetTexture = rt; cam.Render(); cam.Render();   // twice: the first render of a new scene warms the caches
                        var prev = RenderTexture.active; RenderTexture.active = rt;
                        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false); tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply();
                        RenderTexture.active = prev;
                        File.WriteAllBytes(Path.Combine(OutRoot, $"{id}_{(alone ? "alone_" : "")}{s.name}.png"), tex.EncodeToPNG());
                        Object.DestroyImmediate(tex);
                    }
                    Debug.Log($"[AccessoriesProof] {id}: worn {string.Join(",", new[] { AccessorySlot.Headwear, AccessorySlot.Glove, AccessorySlot.BackCarry }.Select(sl => sl + "=" + (acc.Item(sl) ?? "-")))} pose={pose}");
                }
            }
            finally { QualitySettings.skinWeights = keepWeights; if (cam) cam.targetTexture = null; if (rtFull) rtFull.Release(); if (rtSq) rtSq.Release(); }
        }
    }
}
