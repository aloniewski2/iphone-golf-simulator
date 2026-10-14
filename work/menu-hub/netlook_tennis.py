# Online tennis wears the lobby's looks (PLAN_MenuHub_WalkableWorld §4 LOOK): after ConfigureNetwork rebuilds the two actors with
# the participants' bodies, the match heroes are put on again (the old ones were for the previous sexes) and dressed in each
# participant's loadout (skin, shirt, shorts) - the same colours the plaza shows. Idempotent. usage: netlook_tennis.py <TennisGame.Network.cs>
import sys
p = sys.argv[1]; s = open(p).read()
if "DressNetworkHero" in s: print("already applied"); sys.exit(0)
old = '''            Opponent.Build(far.female,PlayerSkinFor(far.female),far.left,PlayerBody(far.female));
'''
new = '''            Opponent.Build(far.female,PlayerSkinFor(far.female),far.left,PlayerBody(far.female));
            // the heroes on court follow the rebuilt bodies and wear the lobby's looks (the plaza shows the same colours)
            foreach(var actor in new[]{Player,Opponent})foreach(var d in actor.GetComponentsInChildren<HeroTennisDriver>(true))DestroyImmediate(d.gameObject);
            TennisHeroSetup.AttachPlayer(this);TennisHeroSetup.AttachRival(this);
            DressNetworkHero(Player,near);DressNetworkHero(Opponent,far);
'''
add = '''        static void DressNetworkHero(TennisActor actor,NetworkParticipant p) {
            var d=actor?actor.GetComponentInChildren<HeroTennisDriver>():null;var l=p?.loadout;
            if(!d||!d.matchLook||l==null)return;
            if(!string.IsNullOrEmpty(l.skinHex))d.matchLook.SetSkin(HeroKit.Hex(l.skinHex));
            d.matchLook.SetKit(l.shirtHex,l.shortsHex,null);
        }
        public void BeginSharedPresentation()'''
anchor = '''        public void BeginSharedPresentation()'''
for a, b in [(old, new), (anchor, add)]:
    if s.count(a) != 1: sys.exit(f"TennisGame.Network.cs: cannot place the look fix ({a[:60]!r} found {s.count(a)}x)")
    s = s.replace(a, b)
open(p, "w").write(s); print("applied")
