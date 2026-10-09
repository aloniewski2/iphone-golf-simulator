using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GolfArcade.Tennis;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GolfArcade.EditorTools
{
    /// HERO_MAINSTAY: builds the two match-hero prefabs from the imported FBXs (MatchHeroImport):
    ///   Resources/Tennis/Customization/PlayerMale.prefab, PlayerFemale.prefab
    /// Body + painted face + classic racket on Hand_Racket + Animator (the Generic avatar of <Sex>_ReadyIdle.fbx) + MatchHeroLook + HeroTennisDriver
    /// with the 16 clips of the hero. The mesh, the skeleton and the clips are used as imported (nothing is edited); only materials are assigned.
    /// DRESS_MATCH_HEROES: the White tennis kit is added as six extra SkinnedMeshRenderers bound by name to the same bones (MatchHeroKit.Attach); the clip FBXs are untouched.
    /// The old Higgsfield tennis bases that used to live at those two resource names are moved (not deleted) to Assets/Characters/Archive.
    public static class MatchHeroBuild
    {
        const string Dir = "Assets/Characters/MatchHeroes/", MatDir = Dir + "Materials/";
        const string PrefabDir = "Assets/Resources/Tennis/Customization/", ArchiveDir = "Assets/Characters/Archive/OldPlayerBase/";
        const string RacketMatSource = "../work/classic-tennis-racket/export/unity_stage/Materials/";
        const string ClipTablePath = "../work/hero-mainstay/data/clip_table.json";

        [Serializable] class ClipRow { public string name; public float length, contact, leftRelease; public bool thin; public string fbxSha256; }
        [Serializable] class HeroRows { public string sex, bodySha256; public ClipRow[] clips; }
        [Serializable] class ClipTable { public string source, source_sha256; public HeroRows[] heroes; }

        /// FBX clip name -> driver slot. Ready = ReadyIdle, Volley = VolleyForehand, Smash = Overhead; the rest keep their names.
        static readonly (string clip, HeroTennisDriver.Clip id)[] Slots =
        {
            ("ReadyIdle", HeroTennisDriver.Clip.Ready), ("Forehand", HeroTennisDriver.Clip.Forehand), ("Backhand", HeroTennisDriver.Clip.Backhand),
            ("Serve", HeroTennisDriver.Clip.Serve), ("VolleyForehand", HeroTennisDriver.Clip.Volley), ("Overhead", HeroTennisDriver.Clip.Smash),
            ("ForehandWide", HeroTennisDriver.Clip.ForehandWide), ("ForehandShort", HeroTennisDriver.Clip.ForehandShort), ("BackhandWide", HeroTennisDriver.Clip.BackhandWide),
            ("SliceApproach", HeroTennisDriver.Clip.SliceApproach), ("Return", HeroTennisDriver.Clip.Return), ("VolleyBackhand", HeroTennisDriver.Clip.VolleyBackhand),
            ("ForehandOpen", HeroTennisDriver.Clip.ForehandOpen), ("SplitToForehand", HeroTennisDriver.Clip.SplitToForehand),
            ("SplitToBackhand", HeroTennisDriver.Clip.SplitToBackhand), ("RecoverCenter", HeroTennisDriver.Clip.RecoverCenter),
        };

        [MenuItem("Golf Arcade/Match Heroes/Build Prefabs")]
        public static void Run()
        {
            try
            {
                Directory.CreateDirectory(MatDir); Directory.CreateDirectory(ArchiveDir.TrimEnd('/'));
                MatchHeroImport.CopyAndImport();
                MatchHeroKit.CopyAndImport();
                ArchiveOldBases();
                var table = JsonUtility.FromJson<ClipTable>(File.ReadAllText(ClipTablePath));
                var report = new System.Text.StringBuilder();
                foreach (var sex in new[] { "Male", "Female" }) report.AppendLine(BuildPrefab(sex, table.heroes.First(h => h.sex == sex)));
                AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
                Directory.CreateDirectory("../work/hero-mainstay/logs");
                File.WriteAllText("../work/hero-mainstay/logs/build_report.txt", report.ToString());
                Debug.Log("[MatchHeroBuild]\n" + report);
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (Exception e) { Debug.LogException(e); if (Application.isBatchMode) EditorApplication.Exit(1); }
        }

        /// Resources/Tennis/Customization/PlayerMale.fbx / PlayerFemale.fbx (the old tennis bases) leave Resources so the two resource
        /// names can hold the match heroes. Moved with their .meta (same GUID), nothing is deleted.
        static void ArchiveOldBases()
        {
            foreach (var n in new[] { "PlayerMale", "PlayerFemale" })
            {
                string from = PrefabDir + n + ".fbx", to = ArchiveDir + n + ".fbx";
                if (!File.Exists(from)) continue;
                if (File.Exists(to)) throw new IOException("archive target exists: " + to);
                string err = AssetDatabase.MoveAsset(from, to);
                if (!string.IsNullOrEmpty(err)) throw new IOException("move " + from + ": " + err);
            }
        }

        static Material CopyMaterial(Material source, string name)
        {
            string path = MatDir + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing) { existing.CopyPropertiesFromMaterial(source); existing.shader = source.shader; EditorUtility.SetDirty(existing); return existing; }
            var m = new Material(source) { name = name };
            AssetDatabase.CreateAsset(m, path); return m;
        }
        static Material LoadMat(string path)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) throw new FileNotFoundException("material missing: " + path);
            return m;
        }

        /// F3 head values for the female lips (MicroF3 lip palette, work/female-micro-f3-head/data/lip_palette_f3.json), as in the colour pass.
        static readonly Dictionary<string, (Color c, float smooth)> FemaleF3 = new Dictionary<string, (Color, float)>
        {
            { "Face_Lip", (new Color(.88f, .59f, .49f), .45f) }, { "Face_LipUp", (new Color(.82f, .53f, .42f), .40f) }, { "Face_Seam", (new Color(.48f, .24f, .18f), .30f) },
        };

        static Material FaceMaterial(string sex, string slot)
        {
            string src = sex == "Male" ? "Assets/Characters/HeroBase/Materials/Male_" + slot + ".mat" : "Assets/Characters/HeroBase/FemaleHeadF1/Materials/Female_" + slot + ".mat";
            var m = CopyMaterial(LoadMat(src), "MatchHero_" + sex + "_" + slot);
            if (sex == "Female" && FemaleF3.TryGetValue(slot, out var f3)) { m.SetColor("_BaseColor", f3.c); m.SetFloat("_Smoothness", f3.smooth); EditorUtility.SetDirty(m); }
            return m;
        }

        static string BuildPrefab(string sex, HeroRows rows)
        {
            bool female = sex == "Female";
            var sb = new System.Text.StringBuilder("== " + sex + "\n");
            var modelPath = MatchHeroPostprocessor.FbxPath(sex, MatchHeroPostprocessor.BaseClip);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            var avatar = MatchHeroPostprocessor.LoadAvatar(sex);
            if (!model || !avatar || !avatar.isValid) throw new InvalidOperationException("no valid avatar for " + sex);
            if (avatar.isHuman != MatchHeroPostprocessor.Humanoid) throw new InvalidOperationException("avatar type does not match the import mode for " + sex);
            // hash guard: the imported FBX is the staged export, byte for byte
            foreach (var row in rows.clips)
            {
                string imported = MatchHeroPostprocessor.FbxPath(sex, row.name);
                string sha = Sha256(imported);
                if (sha != row.fbxSha256) throw new InvalidOperationException($"{imported} differs from the staged FBX (sha {sha} != {row.fbxSha256})");
            }
            sb.AppendLine("  16 clip FBXs match checks.json sha256; body source sha256 " + rows.bodySha256);

            var root = (GameObject)Object.Instantiate(model); root.name = "Player" + sex;
            try
            {
                var animator = root.GetComponent<Animator>(); if (!animator) animator = root.AddComponent<Animator>();
                animator.avatar = avatar; animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;   // Generic rig: no retargeting
                var body = root.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r => r.name.StartsWith("Body_"));
                var face = root.GetComponentsInChildren<MeshRenderer>(true).First(r => r.name.StartsWith("Face_"));
                var racket = root.GetComponentsInChildren<Transform>(true).First(t => t.name == "Racket_Classic");
                if (root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Any(r => r.name.StartsWith("Hair"))) throw new InvalidOperationException("hair in the hero FBX");

                // ---- materials
                var skinSource = LoadMat(female ? "Assets/Characters/HeroBase/FemaleHeadF1/Materials/Female_Form_Skin.mat" : "Assets/Characters/HeroBase/Materials/Male_Form_Skin.mat");
                var skin = CopyMaterial(skinSource, "MatchHero_" + sex + "_Skin");
                var bodyMats = new Material[body.sharedMaterials.Length]; for (int i = 0; i < bodyMats.Length; i++) bodyMats[i] = skin;   // the whole body is skin (the locker / roster tone tints it)
                body.sharedMaterials = bodyMats;
                var faceMats = face.sharedMaterials; var faceNames = new List<string>();
                for (int i = 0; i < faceMats.Length; i++)
                {
                    string slot = faceMats[i] ? faceMats[i].name.Replace(" (Instance)", "") : "";
                    if (!slot.StartsWith("Face_")) throw new InvalidOperationException("unexpected face slot '" + slot + "' on " + face.name);
                    faceMats[i] = FaceMaterial(sex, slot); faceNames.Add(slot);
                }
                face.sharedMaterials = faceMats;
                var rk = new Dictionary<string, string> { { "White_Frame", "Racket_White_Frame" }, { "White_Strings", "Racket_White_Strings" }, { "Black_Grip", "Racket_Black_Grip" } };
                foreach (var kv in rk)
                {
                    string dst = MatDir + kv.Value + ".mat";
                    if (!AssetDatabase.LoadAssetAtPath<Material>(dst))
                    {
                        File.Copy(RacketMatSource + kv.Key + ".mat", dst, true); AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceSynchronousImport);
                        var staged = AssetDatabase.LoadAssetAtPath<Material>(dst); staged.name = kv.Value; EditorUtility.SetDirty(staged);   // keep the asset's internal name = its file name
                    }
                }
                foreach (var r in racket.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (r.name == "Collider_Racket") continue;
                    var ms = r.sharedMaterials;
                    for (int i = 0; i < ms.Length; i++) { string n = ms[i] ? ms[i].name.Replace(" (Instance)", "") : ""; ms[i] = LoadMat(MatDir + rk[n] + ".mat"); }
                    r.sharedMaterials = ms;
                }
                sb.AppendLine($"  body '{body.name}' verts={body.sharedMesh.vertexCount} slots={bodyMats.Length}; face '{face.name}' slots={string.Join(",", faceNames)}");

                // ---- look
                var look = root.AddComponent<MatchHeroLook>();
                look.female = female; look.animator = animator; look.racketGrip = racket; look.body = body; look.face = face; look.racketFrame = racket.GetComponent<Renderer>();
                look.skinTone = skin.GetColor("_BaseColor");
                // the rig is named after the humanoid bones: map every HumanBodyBones name that exists on it
                var byName = new Dictionary<string, Transform>(); foreach (var t in root.GetComponentsInChildren<Transform>(true)) byName.TryAdd(t.name, t);
                look.bones = new Transform[(int)HumanBodyBones.LastBone]; int mapped = 0;
                for (int i = 0; i < look.bones.Length; i++) if (byName.TryGetValue(((HumanBodyBones)i).ToString(), out var bt)) { look.bones[i] = bt; mapped++; }
                foreach (var need in new[] { HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.Neck, HumanBodyBones.Head, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
                    HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes,
                    HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, HumanBodyBones.RightToes, HumanBodyBones.LeftMiddleProximal, HumanBodyBones.RightMiddleProximal })
                    if (!look.Bone(need)) throw new InvalidOperationException("rig has no bone " + need);
                sb.AppendLine($"  bones mapped by name: {mapped} of {look.bones.Length} humanoid slots (the rest do not exist on this rig: jaw, eyes, ...)");
                StringFrame(racket, look, sb);

                // ---- the worn kit (DRESS_MATCH_HEROES)
                sb.Append(MatchHeroKit.Attach(root, sex, look, body));

                // ---- driver
                var driver = root.AddComponent<HeroTennisDriver>(); driver.matchLook = look;
                var slotList = new List<HeroTennisDriver.ClipSlot>();
                foreach (var (clipName, id) in Slots)
                {
                    var clip = AssetDatabase.LoadAllAssetsAtPath(MatchHeroPostprocessor.FbxPath(sex, clipName)).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview"));
                    var row = rows.clips.First(c => c.name == clipName);
                    if (Mathf.Abs(clip.length - row.length) > .02f) throw new InvalidOperationException($"{sex} {clipName}: imported length {clip.length} != staged {row.length}");
                    slotList.Add(new HeroTennisDriver.ClipSlot { id = id, clip = clip, contact = row.contact, leftRelease = row.leftRelease });
                    sb.AppendLine($"  slot {id,-16} <- {clip.name,-24} len={clip.length:0.000} contact={row.contact:0.0000} leftRelease={row.leftRelease:0.0000}{(row.thin ? " THIN" : "")}");
                }
                driver.slots = slotList.ToArray();
                // Serve ritual (stance with the ball held / toss arm up at the release / trophy), read off the Serve clip itself (MatchHeroProbe.ServeTimeline):
                // the left hand holds still at the racket throat from 0.10 to 0.23 s, lets go at 0.25, is fully up at 0.43-0.47 and stays up to 0.63; the racket
                // is cocked behind the shoulder 0.50-0.63.
                driver.serveStanceTime = .167f; driver.serveReleaseTime = .433f; driver.serveTrophyTime = .567f;
                driver.contactAssist = .5f; driver.bodyReach = .3f;
                sb.Append(ServeAndFeetWire.Apply(driver, sex, ServeAndFeetWire.Load()));   // SERVE_AND_FEET: its Serve (contact, ritual times) + Walk / RunForward / RunLeft / RunRight and the walk / run speeds

                string path = PrefabDir + "Player" + sex + ".prefab";
                PrefabUtility.SaveAsPrefabAsset(root, path);
                sb.AppendLine("  saved " + path);
            }
            finally { Object.DestroyImmediate(root); }
            return sb.ToString();
        }

        /// The string bed of the classic racket, measured on its own mesh and stored in racket-local metres: the centre, "up" along the shaft towards the
        /// head, the face normal (the thin axis) and "right" across the face. The driver builds its contact markers from this (HeroTennisDriver.BuildContactModel).
        static void StringFrame(Transform racket, MatchHeroLook look, System.Text.StringBuilder sb)
        {
            var bed = racket.GetComponentsInChildren<MeshFilter>(true).First(m => m.name == "StringBed");
            var b = bed.sharedMesh.bounds; var ext = new[] { b.size.x, b.size.y, b.size.z };
            int iN = Array.IndexOf(ext, ext.Min()), iU = Array.IndexOf(ext, ext.Max()), iR = 3 - iN - iU;
            Vector3 Axis(int k) { var a = k == 0 ? Vector3.right : k == 1 ? Vector3.up : Vector3.forward; return racket.InverseTransformDirection(bed.transform.TransformDirection(a)).normalized; }
            var centre = racket.InverseTransformPoint(bed.transform.TransformPoint(b.center));
            var up = Axis(iU); if (Vector3.Dot(up, centre) < 0) up = -up;
            var normal = Axis(iN); var right = Axis(iR);
            look.stringCentreLocal = centre; look.stringUpLocal = up; look.stringNormalLocal = normal; look.stringRightLocal = right;
            sb.AppendLine($"  string bed: centre(racket-local)={centre:F4} |centre|={centre.magnitude:F4} up={up:F3} right={right:F3} normal={normal:F3} extents(mesh)={b.size:F4} racket local rot={racket.localEulerAngles:F1} scale={racket.localScale:F3}");
        }

        static string Sha256(string assetPath)
        {
            using var s = File.OpenRead(assetPath); using var h = System.Security.Cryptography.SHA256.Create();
            return string.Concat(h.ComputeHash(s).Select(x => x.ToString("x2")));
        }
    }
}
