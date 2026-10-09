using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace GolfArcade.Tests
{
    /// HERO_REGISTRY guard: hero model names live in HeroAssets.cs (and its Swift mirror in MatchHeroData.swift). Code anywhere else
    /// that names an older hero model directly would let a screen fall back to it unnoticed, so this fails the build.
    public sealed class HeroAssetRegistryTests
    {
        // Older hero models: the pre-Hero golfer, GolfHero_*, the HeroV4 bake, menu / swing / lounge bakes, Custom* heads, the standard characters.
        // The HeroV4 haircut table (identity data) is not a model name, so only the HeroV4 asset files are listed.
        public static readonly Regex Legacy = new Regex(
            @"GolfHero_|HeroMenu|HeroSwing_|HeroLounge|StandardCharacters|Golfer/golfer_|HeroV4(\.bin|\.json|_Atlas|_KitMask|_KitRef|_SkinMask)|Custom(Body|Head|Bob|Curls|Quiff)",
            RegexOptions.Compiled);

        [Test]
        public void NoCodeOutsideTheRegistryNamesAnOlderHero()
        {
            var assets = Application.dataPath;
            var repo = Directory.GetParent(assets).Parent.FullName;
            var files = Directory.GetFiles(Path.Combine(assets, "Scripts"), "*.cs", SearchOption.AllDirectories)
                .Where(f => Path.GetFileName(f) != "HeroAssets.cs")
                .Concat(Directory.GetFiles(Path.Combine(repo, "GolfArcade"), "*.swift", SearchOption.AllDirectories));
            var hits = new System.Collections.Generic.List<string>();
            foreach (var file in files)
            {
                var lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].TrimStart().StartsWith("//")) continue;   // prose and doc comments may talk about older models
                    var m = Legacy.Match(lines[i]);
                    if (m.Success) hits.Add($"{file.Substring(repo.Length + 1)}:{i + 1}  {m.Value}");
                }
            }
            Assert.IsEmpty(hits, "Older hero models must be named only in HeroAssets.cs. Route these through it:\n" + string.Join("\n", hits));
        }

        [Test]
        public void TheLintCatchesAPlantedLegacyName()
        {
            Assert.IsTrue(Legacy.IsMatch("Resources.Load(\"StandardCharacters/standard_male_tennis\")"));
            Assert.IsTrue(Legacy.IsMatch("let n = \"GolfHero_Male\""));
            Assert.IsFalse(Legacy.IsMatch("let n = \"GolfKitHero_Male\""));
            Assert.IsFalse(Legacy.IsMatch("static let haircuts = HeroV4.haircuts"));
        }
    }
}
