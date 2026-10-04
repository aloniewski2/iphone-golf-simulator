using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GolfArcade.Tennis;
using UnityEditor;
using UnityEngine;

namespace GolfArcade.EditorTools
{
    /// EMOTES: puts the six emote clips (Emote_Scuba, Emote_Thrust, Emote_Spike, Intro_Wave, Intro_BringIt, Intro_Pushups; work/emotes/export/fbx/<Sex>_<Clip>.fbx) on the two match-hero prefabs.
    ///   Unity -batchmode -projectPath Unity -quit -executeMethod GolfArcade.EditorTools.EmoteWire.Run        (env EW_REPO = repo root, default "..")
    /// It copies the FBXs byte-identical into Assets/Characters/MatchHeroes/<Sex>/ (MatchHeroPostprocessor imports them: Generic rig, one clip named <Sex>_<Clip>) and APPENDS six slots to the driver's
    /// slot list of PlayerMale / PlayerFemale. Nothing else on the prefab changes; no other FBX is touched.
    public static class EmoteWire
    {
        public static readonly string[] Names = { "Emote_Scuba", "Emote_Thrust", "Emote_Spike", "Intro_Wave", "Intro_BringIt", "Intro_Pushups" };
        const string Dir = "Assets/Characters/MatchHeroes/", PrefabDir = "Assets/Resources/Tennis/Customization/";
        public static string Repo => Path.GetFullPath(Environment.GetEnvironmentVariable("EW_REPO") ?? "..");
        public static string FbxSource => Repo + "/work/emotes/export/fbx/";
        static string FbxPath(string sex, string clip) => Dir + sex + "/" + sex + "_" + clip + ".fbx";

        public static HeroTennisDriver.Clip SlotOf(string clip)
        {
            switch (clip)
            {
                case "Emote_Scuba": return HeroTennisDriver.Clip.EmoteScuba;
                case "Emote_Thrust": return HeroTennisDriver.Clip.EmoteThrust;
                case "Emote_Spike": return HeroTennisDriver.Clip.EmoteSpike;
                case "Intro_Wave": return HeroTennisDriver.Clip.IntroWave;
                case "Intro_BringIt": return HeroTennisDriver.Clip.IntroBringIt;
                case "Intro_Pushups": return HeroTennisDriver.Clip.IntroPushups;
            }
            throw new ArgumentException(clip);
        }

        static string Sha256(string path)
        {
            using var s = File.OpenRead(path); using var h = System.Security.Cryptography.SHA256.Create();
            return string.Concat(h.ComputeHash(s).Select(x => x.ToString("x2")));
        }

        [MenuItem("Golf Arcade/Match Heroes/Emotes - Wire")]
        public static void Run()
        {
            int code = 0;
            try
            {
                CopyAndImport();
                var report = new System.Text.StringBuilder();
                foreach (var sex in new[] { "Male", "Female" }) report.AppendLine(WirePrefab(sex));
                AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
                Directory.CreateDirectory(Repo + "/work/emotes/logs");
                File.WriteAllText(Repo + "/work/emotes/logs/wire_report.txt", report.ToString());
                Debug.Log("[EmoteWire]\n" + report);
            }
            catch (Exception e) { Debug.LogException(e); code = 1; }
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        public static void CopyAndImport()
        {
            foreach (var sex in new[] { "Male", "Female" })
            {
                Directory.CreateDirectory(Dir + sex);
                foreach (var clip in Names)
                {
                    string src = FbxSource + sex + "_" + clip + ".fbx", dst = FbxPath(sex, clip);
                    if (!File.Exists(src)) throw new FileNotFoundException(src);
                    if (!File.Exists(dst) || Sha256(dst) != Sha256(src)) File.Copy(src, dst, true);
                }
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var sex in new[] { "Male", "Female" })
                foreach (var clip in Names) AssetDatabase.ImportAsset(FbxPath(sex, clip), ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.SaveAssets();
        }

        public static AnimationClip LoadClip(string sex, string clip) =>
            AssetDatabase.LoadAllAssetsAtPath(FbxPath(sex, clip)).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview"));

        /// Append (or replace) the six slots on a driver.
        public static string Apply(HeroTennisDriver driver, string sex)
        {
            var sb = new System.Text.StringBuilder();
            var slots = new List<HeroTennisDriver.ClipSlot>(driver.slots);
            foreach (var clipName in Names)
            {
                var clip = LoadClip(sex, clipName);
                var id = SlotOf(clipName);
                int i = slots.FindIndex(s => s.id == id);
                var slot = new HeroTennisDriver.ClipSlot { id = id, clip = clip, contact = 0, leftRelease = 0 };
                if (i >= 0) slots[i] = slot; else slots.Add(slot);
                sb.AppendLine($"  slot {id,-14} <- {clip.name,-26} len={clip.length:0.000} {(i >= 0 ? "(replaced)" : "(added)")} asset={FbxPath(sex, clipName)}");
            }
            driver.slots = slots.ToArray();
            return sb.ToString();
        }

        static string WirePrefab(string sex)
        {
            string path = PrefabDir + "Player" + sex + ".prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var driver = root.GetComponent<HeroTennisDriver>();
                if (!driver) throw new InvalidOperationException("no HeroTennisDriver on " + path);
                string r = "== " + sex + " " + path + "\n" + Apply(driver, sex);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                return r;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
