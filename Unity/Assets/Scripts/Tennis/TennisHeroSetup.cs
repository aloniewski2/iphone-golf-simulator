using UnityEngine;

namespace GolfArcade.Tennis
{
    /// Puts the match heroes (HERO_MAINSTAY: the male and female bodies of work/match-anim-set, each with its own skeleton and clips) on the
    /// gameplay actors. The actor's own body and racket are hidden but keep running, so movement, timing, contact and scoring are unchanged.
    /// One standard for every character on court: the player is the male or female hero by the locker's sex; a campaign rival is the male or
    /// female hero by TennisRoster.Female; the default rival is the opposite sex of the player. The old Hero01 prefab is not loaded anywhere.
    public static class TennisHeroSetup
    {
        public static bool Enabled = true;

        public static void Attach(TennisGame game)
        {
            if (!Enabled) return;
            AttachPlayer(game); AttachRival(game);
        }
        /// Re-attach after the game rebuilds an actor (the phone launch calls SelectCharacter, and a campaign
        /// rival replaces the opponent): the hero is a child of the actor and dies with it, so without this the
        /// hidden gameplay body would be all there is on screen.
        /// The player wears their locker look (TennisGame.PlayerLook, re-applied on every rebuild so it never drops back): the skin tone and the
        /// racket colour (HeroKit.Apply); a campaign rival wears their own fixed look from TennisRoster.
        public static HeroTennisDriver AttachPlayer(TennisGame game)
        {
            if (!Enabled || !game.Player) return null;
            var hero = Attach(game, game.Player, true);
            if (hero && game.PlayerLook.HasValue) HeroKit.Apply(hero, game.PlayerLook.Value);
            return hero;
        }
        public static HeroTennisDriver AttachRival(TennisGame game, TennisRoster rivalLook = null)
        {
            if (!Enabled || !game.Opponent) return null;
            var rival = Attach(game, game.Opponent, false);
            if (!rival) return null;
            // Rival identity: same bodies and clip set, different skin and racket colour (never different stats).
            if (rivalLook != null) HeroKit.Apply(rival, rivalLook.HeroLook);
            return rival;
        }

        /// The hero for this actor: male or female by the sex the actor was built with (locker sex for the player, TennisRoster.Female for a
        /// campaign rival, the opposite of the player for the default rival).
        public static HeroTennisDriver Attach(TennisGame game, TennisActor actor, bool player)
        {
            if (!actor) return null;
            var prefab = TennisCustomization.HeroBase(actor.Female);
            if (!prefab) { Debug.LogError("[Tennis] match hero prefab missing: " + TennisCustomization.HeroPath(actor.Female)); return null; }
            var hero = Object.Instantiate(prefab, actor.transform, false);
            hero.name = (actor.Female ? "Match hero Female" : "Match hero Male") + (player ? " (player visual)" : " (rival visual)");
            hero.transform.localPosition = Vector3.zero; hero.transform.localRotation = Quaternion.identity;
            foreach (var r in actor.GetComponentsInChildren<Renderer>(true))
                if (!r.transform.IsChildOf(hero.transform) && !(r is LineRenderer)) r.enabled = false;
            var driver = hero.GetComponent<HeroTennisDriver>();
            driver.actor = actor; driver.game = game; driver.isPlayer = player;
            driver.Build();
            return driver;
        }
    }
}
