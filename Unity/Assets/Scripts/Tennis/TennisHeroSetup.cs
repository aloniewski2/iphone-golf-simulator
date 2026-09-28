using UnityEngine;

namespace GolfArcade.Tennis
{
    /// Puts the locked Hero01 (approved Hero_* clips) on the gameplay actors. The actor's own body and
    /// racket are hidden but keep running, so movement, timing, contact and scoring are unchanged.
    public static class TennisHeroSetup
    {
        public const string PrefabPath = "Tennis/Hero/Hero_01_Tennis";
        public static bool Enabled = true;

        public static void Attach(TennisGame game)
        {
            if (!Enabled) return;
            var prefab = Resources.Load<GameObject>(PrefabPath);
            if (!prefab) { Debug.LogWarning("[Tennis] Hero01 gameplay prefab missing: " + PrefabPath); return; }
            AttachPlayer(game); AttachRival(game);
        }
        /// Re-attach after the game rebuilds an actor (the phone launch calls SelectCharacter, and a campaign
        /// rival replaces the opponent): the hero is a child of the actor and dies with it, so without this the
        /// old fallback body played (no Hero01, no Hero_* clips) and contacts were judged on its racket.
        /// One standard for every character on court: the Hero V4 body with the Hero_* clips. The player wears
        /// their locker look (TennisGame.PlayerLook, re-applied on every rebuild so it never drops back); a campaign
        /// rival wears their own fixed look from TennisRoster; the default rival wears the sweatband + trim kit.
        public static HeroTennisDriver AttachPlayer(TennisGame game)
        {
            if (!Enabled || !game.Player) return null;
            var prefab = Resources.Load<GameObject>(PrefabPath); if (!prefab) return null;
            var hero = Attach(game, game.Player, prefab, true);
            if (hero && game.PlayerLook.HasValue) HeroKit.Apply(hero, game.PlayerLook.Value);
            return hero;
        }
        public static HeroTennisDriver AttachRival(TennisGame game, TennisRoster rivalLook = null)
        {
            if (!Enabled || !game.Opponent) return null;
            var prefab = Resources.Load<GameObject>(PrefabPath); if (!prefab) return null;
            var rival = Attach(game, game.Opponent, prefab, false);
            if (!rival) return null;
            if (rival.cosmetics) rival.cosmetics.debugHotkeys = rival.cosmetics.debugPanel = false;
            // Rival identity: same body and anim set, different cosmetics (never different stats).
            if (rivalLook != null) HeroKit.Apply(rival, rivalLook.HeroLook);
            else if (rival.cosmetics) { rival.cosmetics.EquipHat(HeroCosmetics.Hat.Sweatband); rival.cosmetics.SetTrim(true); }
            return rival;
        }

        public static HeroTennisDriver Attach(TennisGame game, TennisActor actor, GameObject prefab, bool player)
        {
            if (!actor) return null;
            var hero = Object.Instantiate(prefab, actor.transform, false);
            hero.name = player ? "Hero01 (player visual)" : "Hero01 (rival visual)";
            hero.transform.localPosition = Vector3.zero; hero.transform.localRotation = Quaternion.identity;
            foreach (var r in actor.GetComponentsInChildren<Renderer>(true))
                if (!r.transform.IsChildOf(hero.transform) && !(r is LineRenderer)) r.enabled = false;
            var driver = hero.GetComponent<HeroTennisDriver>();
            driver.actor = actor; driver.game = game; driver.isPlayer = player;
            if (driver.cosmetics) driver.cosmetics.debugHotkeys = driver.cosmetics.debugPanel = player && Debug.isDebugBuild;
            driver.Build();
            return driver;
        }
    }
}
