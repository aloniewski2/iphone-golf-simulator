namespace GolfArcade.EditorTools
{
    /// HERO_MAINSTAY: the old Hero01 gameplay prefab is no longer referenced by any runtime code (TennisHeroSetup loads the match heroes).
    /// The asset stays on disk; these legacy editor tools (HeroGameplayBuild, HeroLockerExport) still take its path from here so they
    /// keep compiling and can rebuild the old look by hand. Nothing in Play mode, the locker or a campaign match uses it.
    public static class LegacyHero01
    {
        public const string PrefabPath = "Tennis/Hero/Hero_01_Tennis";
    }
}
