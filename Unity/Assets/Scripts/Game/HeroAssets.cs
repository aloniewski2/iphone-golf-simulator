namespace GolfArcade.Game
{
    /// HERO_REGISTRY: the one list of hero model names the game loads. A new hero model is changed HERE and nowhere else;
    /// Unity/Assets/Tests/EditMode/HeroAssetRegistryTests.cs fails if any other script (or GolfArcade/**/*.swift) names an
    /// older model directly. The Swift mirror is `HeroAssets` in GolfArcade/Unity/MatchHeroData.swift.
    ///
    /// CURRENT heroes: the bald grey-mannequin match heroes (tennis, with the tailored polo / skirt) and the golf kit on the
    /// same body. LEGACY entries stay only so a build without the current hero is loud about it, never silently older.
    public static class HeroAssets
    {
        /// The tennis match hero prefab (Resources/Tennis/Customization/PlayerMale | PlayerFemale).
        public static string Match(bool female) => "Tennis/Customization/Player" + (female ? "Female" : "Male");
        /// The match hero's golf-skeleton FBX, used when a base is rebound for golf.
        public static string MatchGolfRig(bool female) => Match(female) + "Golf";
        /// The golf kit prefab (body, face and golf garments) that HeroGolfer dresses the golfer in.
        public static string GolfKitPrefab(bool female) => Match(female) + "GolfKit";
        /// The golfer FBX: rig, clubs and the golf swing clips, plus "<path>_clips".
        public static string Golf(bool female) => female ? "Hero/golfer_f" : "Hero/golfer_m";
        /// The baked SceneKit export the iOS menus read (CharacterAssets/<name>.json/.lzfse). Golf is the kit export.
        public static string NativeExport(bool golf, bool female, bool distance = false) =>
            (golf ? "GolfKitHero_" : "MatchHero_") + (female ? "Female" : "Male") + (distance && !golf ? "_Distance" : "");

        /// LEGACY: the pre-Hero golfer, used only when the Hero is not in the build. Callers must say so loudly.
        public static string LegacyGolfer(bool female) => female ? "Golfer/golfer_f" : "Golfer/golfer_m";
        /// LEGACY: the hidden standard tennis rig that drives gameplay under the visible match hero. Never shown.
        public static string HiddenTennisRig(bool female) => "StandardCharacters/standard_" + (female ? "female" : "male") + "_tennis";
    }
}
