using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GolfArcade.EditorTools
{
    /// MATCH HEROES (HERO_MAINSTAY): the two staged heroes and their 16 match clips, imported as Generic rigs.
    /// Source of truth is work/match-anim-set/export/fbx/<Sex>_<Clip>.fbx (armature + skinned body + face + racket on
    /// Hand_Racket + one baked clip). Each FBX is copied under Assets/Characters/MatchHeroes/<Sex>/ untouched.
    /// <Sex>_ReadyIdle.fbx is the body, face and racket the prefab is built from. A clip is played as the transform curves Blender baked on that very
    /// skeleton: no Humanoid muscle space, no retargeting (a Humanoid import measured 0.5-8.7 mm off at the baked frames and
    /// up to 0.49 m off between two frames of a fast swing, and its muscle-space cross-fades flailed the racket).
    public sealed class MatchHeroPostprocessor : AssetPostprocessor
    {
        public const string Root = "Assets/Characters/MatchHeroes/";
        public const string BaseClip = "ReadyIdle";
        public static readonly string[] Clips =
        {
            "ReadyIdle", "SplitToForehand", "SplitToBackhand", "RecoverCenter", "Forehand", "ForehandOpen", "ForehandWide", "ForehandShort",
            "Backhand", "BackhandWide", "SliceApproach", "Serve", "Return", "VolleyForehand", "VolleyBackhand", "Overhead",
        };
        public static string FbxPath(string sex, string clip) => Root + sex + "/" + sex + "_" + clip + ".fbx";

        public static bool Owns(string path) => path.StartsWith(Root) && path.EndsWith(".fbx") && !path.Contains("/_Probe/");

        /// The three optional humanoid bones the 53-bone rig does not have (no jaw, no eye bones: the face is painted geometry).
        public static bool WideLimit => Environment.GetEnvironmentVariable("MH_WIDE") != "0";
        static float Env(string key, float fallback) { var v = Environment.GetEnvironmentVariable(key); return !string.IsNullOrEmpty(v) && float.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var f) ? f : fallback; }
        static readonly HashSet<string> NoSuchBone = new HashSet<string> { "Jaw", "LeftEye", "RightEye" };
        /// Set MH_HUMANOID=1 to import the same FBXs as Humanoid again (the fidelity probe compares the two).
        public static bool Humanoid => Environment.GetEnvironmentVariable("MH_HUMANOID") == "1";
        /// Unity humanoid name ("Left Thumb Proximal", "LeftUpperArm") -> the rig's bone (spaces removed): the rig is named after the humanoid bones.
        public static HumanBone[] Mapping(ModelImporter m)
        {
            var list = new List<HumanBone>();
            for (int i = 0; i < HumanTrait.BoneCount; i++)
            {
                string human = HumanTrait.BoneName[i], bone = human.Replace(" ", "");
                if (NoSuchBone.Contains(human)) continue;
                // The match clips use the whole range of the joints (two-hand volleys, wide returns). Default muscle limits clamp those
                // frames, so every limit is opened to the full +-180 deg: the clip -> muscle -> bone round trip then stays exact.
                list.Add(new HumanBone { boneName = bone, humanName = human, limit = WideLimit ? new HumanLimit { useDefaultValues = false, min = new Vector3(-180, -180, -180), max = new Vector3(180, 180, 180), center = Vector3.zero } : new HumanLimit { useDefaultValues = true } });
            }
            return list.ToArray();
        }

        void OnPreprocessModel()
        {
            if (!Owns(assetPath)) return;
            var m = (ModelImporter)assetImporter;
            string name = Path.GetFileNameWithoutExtension(assetPath), sex = name.StartsWith("Male") ? "Male" : "Female";
            string clip = name.Substring(sex.Length + 1); bool isBase = clip == BaseClip;
            m.globalScale = 1; m.useFileScale = true; m.bakeAxisConversion = false;   // Blender (x,y,z) -> Unity (-x,z,-y): the hero faces +Z, its right hand is +X
            m.preserveHierarchy = true; m.weldVertices = false; m.optimizeMeshPolygons = false; m.optimizeMeshVertices = false;
            m.meshCompression = ModelImporterMeshCompression.Off;
            m.isReadable = isBase;                    // BakeMesh / bounds are read from the base model only
            m.importNormals = ModelImporterNormals.Import; m.importTangents = ModelImporterTangents.None;
            m.importCameras = false; m.importLights = false; m.importVisibility = false; m.optimizeGameObjects = false;
            m.skinWeights = ModelImporterSkinWeights.Custom; m.maxBonesPerVertex = 4; m.minBoneWeight = .001f;
            m.materialImportMode = isBase ? ModelImporterMaterialImportMode.ImportStandard : ModelImporterMaterialImportMode.None;
            m.animationType = Humanoid ? ModelImporterAnimationType.Human : ModelImporterAnimationType.Generic;
            m.importAnimation = true; m.animationCompression = ModelImporterAnimationCompression.Off;
            if (!Humanoid)
            {
                // Generic: every FBX keeps its own (identical) skeleton and its own baked curves; the prefab plays them straight on it.
                m.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            }
            else if (isBase)
            {
                m.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                var hd = m.humanDescription; hd.human = Mapping(m);
                hd.upperArmTwist = Env("MH_UPPER_ARM_TWIST", .5f); hd.lowerArmTwist = Env("MH_LOWER_ARM_TWIST", .5f);
                hd.upperLegTwist = Env("MH_UPPER_LEG_TWIST", .5f); hd.lowerLegTwist = Env("MH_LOWER_LEG_TWIST", .5f);
                hd.armStretch = Env("MH_ARM_STRETCH", .05f); hd.legStretch = Env("MH_LEG_STRETCH", .05f);
                m.humanDescription = hd;
            }
            else
            {
                var avatar = LoadAvatar(sex);
                if (avatar) { m.avatarSetup = ModelImporterAvatarSetup.CopyFromOther; m.sourceAvatar = avatar; }
                else m.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            }
        }

        /// One clip per FBX, named <Sex>_<Clip>; the root is baked into the pose (original offsets) so the clip plays exactly where it was authored.
        void OnPreprocessAnimation()
        {
            if (!Owns(assetPath)) return;
            var m = (ModelImporter)assetImporter;
            string name = Path.GetFileNameWithoutExtension(assetPath), sex = name.StartsWith("Male") ? "Male" : "Female", clip = name.Substring(sex.Length + 1);
            var defs = m.defaultClipAnimations;
            if (defs == null || defs.Length == 0) return;
            var one = defs[0]; one.name = name; one.loopTime = clip == BaseClip; one.loopPose = false;
            one.lockRootPositionXZ = true; one.lockRootHeightY = true; one.lockRootRotation = true;
            one.keepOriginalPositionXZ = true; one.keepOriginalPositionY = true; one.keepOriginalOrientation = true;
            one.heightFromFeet = false; one.mirror = false;
            m.clipAnimations = new[] { one };
        }

        public static Avatar LoadAvatar(string sex) =>
            AssetDatabase.LoadAllAssetsAtPath(FbxPath(sex, BaseClip)).OfType<Avatar>().FirstOrDefault();
    }

    public static class MatchHeroImport
    {
        public const string Source = "../work/match-anim-set/export/fbx/";

        /// Copy the 32 clip FBXs into the project (byte-identical), base clip first so the others can copy its Avatar.
        public static void CopyAndImport()
        {
            foreach (var sex in new[] { "Male", "Female" })
            {
                Directory.CreateDirectory(MatchHeroPostprocessor.Root + sex);
                foreach (var clip in MatchHeroPostprocessor.Clips)
                {
                    if (ServeAndFeetWire.Owns(clip)) continue;   // SERVE_AND_FEET owns this clip (its own Serve, placed by ServeAndFeetWire.CopyAndImport below), not the match-anim-set one
                    string src = Source + sex + "_" + clip + ".fbx", dst = MatchHeroPostprocessor.FbxPath(sex, clip);
                    if (!File.Exists(src)) throw new FileNotFoundException(src);
                    if (!File.Exists(dst) || !FilesEqual(src, dst)) File.Copy(src, dst, true);
                }
            }
            ServeAndFeetWire.CopyAndImport();   // SERVE_AND_FEET: Serve, Walk, RunForward, RunLeft, RunRight (work/serve-and-feet/export/fbx)
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var sex in new[] { "Male", "Female" })
            {
                AssetDatabase.ImportAsset(MatchHeroPostprocessor.FbxPath(sex, MatchHeroPostprocessor.BaseClip), ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                foreach (var clip in MatchHeroPostprocessor.Clips)
                    if (clip != MatchHeroPostprocessor.BaseClip)
                        AssetDatabase.ImportAsset(MatchHeroPostprocessor.FbxPath(sex, clip), ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            }
            AssetDatabase.SaveAssets();
        }

        static bool FilesEqual(string a, string b)
        {
            var fa = new FileInfo(a); var fb = new FileInfo(b); if (fa.Length != fb.Length) return false;
            using var sa = File.OpenRead(a); using var sb = File.OpenRead(b);
            var ba = new byte[1 << 16]; var bb = new byte[1 << 16];
            while (true)
            {
                int ra = sa.Read(ba, 0, ba.Length), rb = sb.Read(bb, 0, bb.Length);
                if (ra != rb) return false; if (ra == 0) return true;
                for (int i = 0; i < ra; i++) if (ba[i] != bb[i]) return false;
            }
        }

        [MenuItem("Golf Arcade/Match Heroes/Copy And Import")]
        public static void Run()
        {
            try
            {
                CopyAndImport();
                var sb = new System.Text.StringBuilder();
                foreach (var sex in new[] { "Male", "Female" })
                {
                    var avatar = MatchHeroPostprocessor.LoadAvatar(sex);
                    sb.AppendLine($"{sex}: avatar={(avatar ? avatar.name : "none")} valid={(avatar && avatar.isValid)} human={(avatar && avatar.isHuman)} (Generic rig: human=False is expected)");
                    foreach (var clip in MatchHeroPostprocessor.Clips)
                    {
                        string path = MatchHeroPostprocessor.FbxPath(sex, clip);
                        var ac = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview")).ToArray();
                        var imp = (ModelImporter)AssetImporter.GetAtPath(path);
                        sb.AppendLine($"  {clip}: clips={string.Join("|", ac.Select(c => c.name + " len=" + c.length.ToString("0.000") + " human=" + c.humanMotion + " fr=" + c.frameRate))} avatarSetup={imp.avatarSetup} animType={imp.animationType}");
                    }
                }
                Directory.CreateDirectory("../work/hero-mainstay/logs");
                File.WriteAllText("../work/hero-mainstay/logs/import_report.txt", sb.ToString());
                Debug.Log("[MatchHeroImport]\n" + sb);
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (Exception e) { Debug.LogException(e); if (Application.isBatchMode) EditorApplication.Exit(1); }
        }
    }
}
