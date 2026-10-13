using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GolfArcade.Tennis;
using UnityEditor;
using UnityEngine;

namespace GolfArcade.EditorTools
{
    /// EMOTE STANDARD: installs the emotion set and the standard locomotion (work/emote-standard/fbx).
    ///   * the tennis versions (racket in hand) go to Assets/Characters/MatchHeroes/<Sex>/<Sex>_Emote_*.fbx (MatchHeroPostprocessor imports them)
    ///     and seven slots are appended to the PlayerMale / PlayerFemale drivers (EmoteJumpJoy .. EmoteDefeat);
    ///   * the empty-handed versions (golf and the menu plaza) and the standard Walk / Run go to Assets/Resources/HeroMoves/<Sex>/<Name>.fbx,
    ///     one Generic clip named <Name> each (Walk / Run loop), where GolferView and HubHeroAnimator load them.
    ///   Unity -batchmode -projectPath <project> -executeMethod GolfArcade.EditorTools.EmoteStandardWire.Run      (env ES_REPO = repo root)
    public static class EmoteStandardWire
    {
        public static readonly (string name, HeroTennisDriver.Clip id)[] Emotes =
        {
            ("Emote_JumpJoy", HeroTennisDriver.Clip.EmoteJumpJoy), ("Emote_FistPump", HeroTennisDriver.Clip.EmoteFistPump), ("Emote_Stomp", HeroTennisDriver.Clip.EmoteStomp),
            ("Emote_Slump", HeroTennisDriver.Clip.EmoteSlump), ("Emote_Shock", HeroTennisDriver.Clip.EmoteShock), ("Emote_Victory", HeroTennisDriver.Clip.EmoteVictory),
            ("Emote_Defeat", HeroTennisDriver.Clip.EmoteDefeat),
        };
        public static readonly string[] Moves = Emotes.Select(e => e.name).Concat(new[] { "Walk", "Run" }).ToArray();
        const string Dir = "Assets/Characters/MatchHeroes/", PrefabDir = "Assets/Resources/Tennis/Customization/", MovesDir = "Assets/Resources/HeroMoves/";
        static readonly string[] Sexes = { "Male", "Female" };
        public static string Repo => Path.GetFullPath(Environment.GetEnvironmentVariable("ES_REPO") ?? "..");
        static string Source => Repo + "/work/emote-standard/fbx/";
        static string TennisPath(string sex, string clip) => Dir + sex + "/" + sex + "_" + clip + ".fbx";
        static string MovePath(string sex, string move) => MovesDir + sex + "/" + move + ".fbx";

        static string Sha256(string path)
        {
            using var s = File.OpenRead(path); using var h = System.Security.Cryptography.SHA256.Create();
            return string.Concat(h.ComputeHash(s).Select(x => x.ToString("x2")));
        }

        [MenuItem("Golf Arcade/Match Heroes/Emote Standard - Wire")]
        public static void Run()
        {
            int code = 0;
            var report = new System.Text.StringBuilder();
            try
            {
                Copy(report);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                foreach (var sex in Sexes)
                {
                    foreach (var (name, _) in Emotes) AssetDatabase.ImportAsset(TennisPath(sex, name), ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                    foreach (var move in Moves) ConfigureMove(MovePath(sex, move), move, report);
                }
                foreach (var sex in Sexes) report.AppendLine(WirePrefab(sex));
                AssetDatabase.SaveAssets();
                Directory.CreateDirectory(Repo + "/work/emote-standard/logs");
                File.WriteAllText(Repo + "/work/emote-standard/logs/wire_report.txt", report.ToString());
                Debug.Log("[EmoteStandardWire]\n" + report);
            }
            catch (Exception e) { Debug.LogException(e); code = 1; }
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        static void Copy(System.Text.StringBuilder report)
        {
            foreach (var sex in Sexes)
            {
                Directory.CreateDirectory(Dir + sex); Directory.CreateDirectory(MovesDir + sex);
                foreach (var (name, _) in Emotes) CopyOne(Source + sex + "_" + name + ".fbx", TennisPath(sex, name), report);
                foreach (var move in Moves) CopyOne(Source + sex + "_" + move + "_Free.fbx", MovePath(sex, move), report);
            }
        }

        static void CopyOne(string src, string dst, System.Text.StringBuilder report)
        {
            if (!File.Exists(src)) throw new FileNotFoundException(src);
            if (File.Exists(dst) && Sha256(dst) == Sha256(src)) return;
            File.Copy(src, dst, true); report.AppendLine("  copied " + dst + "  sha256=" + Sha256(dst));
        }

        /// One Generic clip named after the move, the skeleton kept as authored (preserveHierarchy keeps Rig_<Sex> in the curve paths, as on the heroes).
        static void ConfigureMove(string path, string move, System.Text.StringBuilder report)
        {
            var m = (ModelImporter)AssetImporter.GetAtPath(path);
            if (!m) throw new InvalidOperationException("no importer " + path);
            m.globalScale = 1; m.useFileScale = true; m.bakeAxisConversion = false; m.preserveHierarchy = true;
            m.importCameras = false; m.importLights = false; m.importVisibility = false; m.importBlendShapes = false; m.optimizeGameObjects = false;
            m.materialImportMode = ModelImporterMaterialImportMode.None;
            m.animationType = ModelImporterAnimationType.Generic; m.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            m.importAnimation = true; m.animationCompression = ModelImporterAnimationCompression.Off; m.resampleCurves = true;
            var defs = m.defaultClipAnimations;
            if (defs == null || defs.Length == 0) { m.SaveAndReimport(); m = (ModelImporter)AssetImporter.GetAtPath(path); defs = m.defaultClipAnimations; }   // the takes appear once it imports as Generic with animation
            if (defs == null || defs.Length == 0) throw new InvalidOperationException("no take in " + path);
            var one = defs[0]; one.name = move; one.loopTime = move == "Walk" || move == "Run"; one.loopPose = false;
            one.lockRootPositionXZ = true; one.lockRootHeightY = true; one.lockRootRotation = true;
            one.keepOriginalPositionXZ = true; one.keepOriginalPositionY = true; one.keepOriginalOrientation = true;
            one.heightFromFeet = false; one.mirror = false;
            m.clipAnimations = new[] { one };
            m.SaveAndReimport();
            var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview"));
            var first = AnimationUtility.GetCurveBindings(clip).Select(b => b.path).Where(p => p.Length > 0).OrderBy(p => p.Length).FirstOrDefault();
            report.AppendLine($"  move {path} clip={clip.name} len={clip.length:0.000} loop={clip.isLooping} curves={AnimationUtility.GetCurveBindings(clip).Length} root='{first}'");
        }

        public static AnimationClip LoadClip(string sex, string name) =>
            AssetDatabase.LoadAllAssetsAtPath(TennisPath(sex, name)).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview"));

        static string WirePrefab(string sex)
        {
            string path = PrefabDir + "Player" + sex + ".prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var driver = root.GetComponent<HeroTennisDriver>();
                if (!driver) throw new InvalidOperationException("no HeroTennisDriver on " + path);
                var sb = new System.Text.StringBuilder("== " + sex + " " + path + "\n");
                var slots = new List<HeroTennisDriver.ClipSlot>(driver.slots);
                foreach (var (name, id) in Emotes)
                {
                    var clip = LoadClip(sex, name);
                    int i = slots.FindIndex(s => s.id == id);
                    var slot = new HeroTennisDriver.ClipSlot { id = id, clip = clip, contact = 0, leftRelease = 0 };
                    if (i >= 0) slots[i] = slot; else slots.Add(slot);
                    sb.AppendLine($"  slot {id,-14} <- {clip.name,-26} len={clip.length:0.000} {(i >= 0 ? "(replaced)" : "(added)")}");
                }
                driver.slots = slots.ToArray();
                PrefabUtility.SaveAsPrefabAsset(root, path);
                return sb.ToString();
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
