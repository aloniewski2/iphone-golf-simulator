"""The catalog: every style the avatar kit makes, in the game's id order.

Each entry is (key, game name, builder). The position in a list is the style's id in the game (HeroGolfer.Haircut,
Headwear, GlassesNames, FacialNames, TopNames, BottomNames; saved on the profile in CharacterLook), so a new style goes
at the END of its list, never in the middle. Builders take the head object (head-space parts) or nothing (clothes).
Entry 0 of HATS, GLASSES and FACIAL is "none"; haircut 5 is "bald": no mesh.

Adding a style: write its function (avatar_styles.py for the head, avatar_wardrobe.py for clothes), add one line here,
re-run avatar_export.py and icons (avatar_mock.py --mode icons), and add its name to the matching list in HeroGolfer.cs.
"""
import avatar_head_parts as H
import avatar_styles as S
import avatar_parts as P
import avatar_wardrobe as W

HAIR = [("swept", "Swept", H.swept), ("ponytail", "Ponytail", H.ponytail), ("bob", "Bob", H.bob), ("long", "Long", H.long_hair),
        ("curls", "Curly", H.curls), ("bald", "Bald", None), ("crop", "Crop", H.crop), ("spikes", "Spikes", H.spikes),
        ("afro", "Afro", S.afro), ("bun", "Bun", S.bun), ("pigtails", "Pigtails", S.pigtails), ("mohawk", "Mohawk", S.mohawk),
        ("quiff", "Quiff", S.quiff), ("buzz", "Buzz", S.buzz), ("braids", "Braids", S.braids)]

HATS = [("none", "None", None), ("visor", "Visor", H.visor), ("cap", "Cap", H.golf_cap), ("bucket", "Bucket", H.bucket),
        ("beanie", "Beanie", H.beanie), ("straw", "Straw", H.straw), ("headband", "Headband", S.headband), ("beret", "Beret", S.beret),
        ("flatcap", "Flatcap", S.flatcap), ("fedora", "Fedora", S.fedora), ("cowboy", "Cowboy", S.cowboy),
        ("headphones", "Headphones", S.headphones), ("bandana", "Bandana", S.bandana), ("tophat", "Tophat", S.tophat), ("crown", "Crown", S.crown)]

GLASSES = [("none", "None", None), ("round", "Round", H.round_glasses), ("square", "Square", H.square_glasses), ("shades", "Shades", H.sunglasses),
           ("aviator", "Aviator", S.aviators), ("cateye", "Cateye", S.cateye), ("wrap", "Wrap", S.wrap_shades)]

FACIAL = [("none", "None", None), ("beard", "Beard", H.beard), ("mustache", "Mustache", H.mustache), ("goatee", "Goatee", S.goatee),
          ("handlebar", "Handlebar", S.handlebar), ("stubble", "Stubble", S.stubble)]

TOPS = [("polo", "Polo", P.polo), ("tee", "Tee", W.tee), ("hoodie", "Hoodie", W.hoodie), ("vest", "Vest", W.vest)]

BOTTOMS = [("shorts", "Shorts", P.shorts), ("trousers", "Trousers", W.trousers), ("skirt", "Skirt", W.skirt)]


def by_key(entries):
    return {k: f for k, _, f in entries if f}


def name_of(entries, key):
    return next(n for k, n, _ in entries if k == key)
