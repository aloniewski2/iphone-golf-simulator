using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GolfArcade.Tennis;
using UnityEditor;
using UnityEngine;

namespace GolfArcade.EditorTools
{
    /// SERVE_AND_FEET: puts the five new clips (Serve, Walk, RunForward, RunLeft, RunRight; work/serve-and-feet/export/fbx/<Sex>_<Clip>.fbx) on the two match-hero prefabs.
    ///   Unity -batchmode -nographics -quit -projectPath Unity -executeMethod GolfArcade.EditorTools.ServeAndFeetWire.Run        (env SF_REPO = repo root, default "..")
    /// It changes nothing but the driver's slot list (Serve replaced, Walk / RunForward / RunRight / RunLeft added), the three serve ritual times and the two clip-speed fields:
    /// the mesh, the skeleton, the materials, the other 15 clips and everything else on the prefab are left as they are (so a worn kit another job adds is not touched).
    /// MatchHeroImport.CopyAndImport and MatchHeroBuild call CopyAndImport / Apply too, so a rebuild of the prefabs keeps the new clips.
    public static class ServeAndFeetWire
    {
        public static readonly string[] Clips = { "Serve", "Walk", "RunForward", "RunLeft", "RunRight" };
        public static bool Owns(string clip) => Array.IndexOf(Clips, clip) >= 0;
        const string Dir = "Assets/Characters/MatchHeroes/", PrefabDir = "Assets/Resources/Tennis/Customization/";

        [Serializable] public class ClipRow { public string name, sex, fbxSha256; public float length, contact; public bool loop; }
        [Serializable] public class ServeTimes { public float stance, release, trophy, deepest, takeoff, contact, landing; }
        [Serializable] public class Table { public ClipRow[] clips; public ServeTimes serve; public float walkSpeed, runSpeed, runReversePivot; }

        public static string Repo => Path.GetFullPath(Environment.GetEnvironmentVariable("SF_REPO") ?? "..");
        public static string FbxSource => Repo + "/work/serve-and-feet/export/fbx/";
        public static string TablePath => Repo + "/work/serve-and-feet/data/clip_table_new.json";
        public static Table Load() => JsonUtility.FromJson<Table>(File.ReadAllText(TablePath));

        static HeroTennisDriver.Clip SlotOf(string clip)
        {
            switch (clip)
            {
                case "Serve": return HeroTennisDriver.Clip.Serve;
                case "Walk": return HeroTennisDriver.Clip.Walk;
                case "RunForward": return HeroTennisDriver.Clip.RunForward;
                case "RunLeft": return HeroTennisDriver.Clip.RunLeft;
                case "RunRight": return HeroTennisDriver.Clip.RunRight;
            }
            throw new ArgumentException(clip);
        }

        static string FbxPath(string sex, string clip) => Dir + sex + "/" + sex + "_" + clip + ".fbx";

        static string Sha256(string path)
        {
            using var s = File.OpenRead(path); using var h = System.Security.Cryptography.SHA256.Create();
            return string.Concat(h.ComputeHash(s).Select(x => x.ToString("x2")));
        }

        [MenuItem("Golf Arcade/Match Heroes/Serve And Feet - Wire")]
        public static void Run()
        {
            int code = 0;
            try
            {
                var table = Load();
                CopyAndImport(table);
                var report = new System.Text.StringBuilder();
                foreach (var sex in new[] { "Male", "Female" }) report.AppendLine(WirePrefab(sex, table));
                AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
                Directory.CreateDirectory(Repo + "/work/serve-and-feet/logs");
                File.WriteAllText(Repo + "/work/serve-and-feet/logs/wire_report.txt", report.ToString());
                Debug.Log("[ServeAndFeetWire]\n" + report);
            }
            catch (Exception e) { Debug.LogException(e); code = 1; }
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        /// Copy the new FBXs into the project (byte-identical, hash-checked against the table) and import them (MatchHeroPostprocessor owns the folder: Generic rig, clip named <Sex>_<Clip>).
        public static void CopyAndImport(Table table = null)
        {
            table = table ?? Load();
            foreach (var sex in new[] { "Male", "Female" })
            {
                Directory.CreateDirectory(Dir + sex);
                foreach (var clip in Clips)
                {
                    string src = FbxSource + sex + "_" + clip + ".fbx", dst = FbxPath(sex, clip);
                    var row = table.clips.First(c => c.sex == sex && c.name == clip);
                    if (!File.Exists(src)) throw new FileNotFoundException(src);
                    if (Sha256(src) != row.fbxSha256) throw new InvalidOperationException($"{src} differs from the table (sha256 {Sha256(src)} != {row.fbxSha256})");
                    if (!File.Exists(dst) || Sha256(dst) != row.fbxSha256) File.Copy(src, dst, true);
                }
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var sex in new[] { "Male", "Female" })
                foreach (var clip in Clips) AssetDatabase.ImportAsset(FbxPath(sex, clip), ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.SaveAssets();
        }

        static AnimationClip LoadClip(string sex, string clip) =>
            AssetDatabase.LoadAllAssetsAtPath(FbxPath(sex, clip)).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview"));

        /// Slot list + ritual times + speeds on a driver (a prefab instance or a prefab's contents).
        public static string Apply(HeroTennisDriver driver, string sex, Table table)
        {
            var sb = new System.Text.StringBuilder();
            var slots = new List<HeroTennisDriver.ClipSlot>(driver.slots);
            foreach (var clipName in Clips)
            {
                var row = table.clips.First(c => c.sex == sex && c.name == clipName);
                var clip = LoadClip(sex, clipName);
                if (Mathf.Abs(clip.length - row.length) > .02f) throw new InvalidOperationException($"{sex} {clipName}: imported length {clip.length} != table {row.length}");
                var id = SlotOf(clipName);
                int i = slots.FindIndex(s => s.id == id);
                var slot = new HeroTennisDriver.ClipSlot { id = id, clip = clip, contact = row.contact, leftRelease = 0 };
                if (i >= 0) slots[i] = slot; else slots.Add(slot);
                sb.AppendLine($"  slot {id,-14} <- {clip.name,-22} len={clip.length:0.000} contact={row.contact:0.000} {(i >= 0 ? "(replaced)" : "(added)")} asset={FbxPath(sex, clipName)}");
            }
            driver.slots = slots.ToArray();
            driver.serveStanceTime = table.serve.stance; driver.serveReleaseTime = table.serve.release; driver.serveTrophyTime = table.serve.trophy;
            driver.walkClipSpeed = table.walkSpeed; driver.runClipSpeed = table.runSpeed; driver.runReversePivot = table.runReversePivot;
            sb.AppendLine($"  serve ritual stance={driver.serveStanceTime:0.000} release={driver.serveReleaseTime:0.000} trophy={driver.serveTrophyTime:0.000}; walkClipSpeed={driver.walkClipSpeed:0.000} runClipSpeed={driver.runClipSpeed:0.000} runReversePivot={driver.runReversePivot:0.0000}");
            return sb.ToString();
        }

        static string WirePrefab(string sex, Table table)
        {
            string path = PrefabDir + "Player" + sex + ".prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var driver = root.GetComponent<HeroTennisDriver>();
                if (!driver) throw new InvalidOperationException("no HeroTennisDriver on " + path);
                string r = "== " + sex + " " + path + "\n" + Apply(driver, sex, table);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                return r;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
