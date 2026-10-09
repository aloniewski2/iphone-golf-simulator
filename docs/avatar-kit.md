# The avatar kit

> **2026-10-04: retired from the game** in favour of Adnan's match heroes (`docs/matchhero-golfer.md`). The scripts here still run and its
> FBXs and tile pictures are in `Unity/Assets/Archive/IconKit/`; its hats, glasses and hair would need refitting to his heads
> (they are authored in the icon head's space, about 2.2 times the size).

The Hero is a kit, not a model. One skeleton, one body, one head; every haircut, hat, pair of glasses, beard, top,
pair of shorts and pair of shoes is its own small mesh made against the same head and body, so any part fits any
avatar and a new part is one function, not a new character.

The look is the "3D icon avatar" style (the CGTrader *Avatar Job Professions Icon Pack* was the reference): a big
soft rounded head, small glossy eyes, thick brows, an open smile, chunky simple clothes, matte clay shading.

    blender/scripts/avatar_kit.py         shared tools: smooth low-poly geometry, colour roles, surface snapping, studio
    blender/scripts/avatar_base.py        the head (skull block + face) and the body skin; the numbers every part uses
    blender/scripts/avatar_parts.py       clothes: tops, bottoms, shoes
    blender/scripts/avatar_head_parts.py  haircuts, hats, glasses, beards
    blender/scripts/avatar_mock.py        assembles an avatar from the kit and renders it (turnaround, closet, parts lineup)

    Blender -b --factory-startup --python blender/scripts/avatar_mock.py -- --mode hero|closet|kit|bust --out <dir>
        [--hair swept|crop|curls|bob|long|spikes|ponytail|none] [--hat cap|visor|bucket|beanie|straw]
        [--glasses round|square|shades] [--beard beard|mustache]

## The catalog

`avatar_catalog.py` lists every style, in the game's id order, as `(key, game name, builder)`: 15 haircuts (bald is no mesh),
14 hats, 6 glasses, 5 facial hair, 4 tops, 3 bottoms. A style's position is its id in `HeroGolfer` and in the saved
`CharacterLook`, so a new one goes at the END of its list. New head styles live in `avatar_styles.py`, new clothes in
`avatar_wardrobe.py`; `avatar_export.py` writes them all to `Resources/Hero/hero_*.fbx` (every haircut twice: `Hair_X` and
`Hair_X_Hat`, the crown pressed down for when a hat is on).

Adding a style: write the function, add one line to the catalog, then

    Blender -b --factory-startup --python blender/scripts/avatar_export.py
    ICON_ONLY=hat_fez Blender -b --factory-startup --python blender/scripts/avatar_mock.py -- --mode icons --out <dir>
    python3 blender/scripts/avatar_icons.py <dir>

and add its name to the matching list in `HeroGolfer.cs` (and the count in `CharacterLook`); a PlayMode test
(`EveryStyleIsAPartInTheBuild`) fails until the part and its tile picture are both in the build.

## The picker

`UI/Locker.cs`: the golfer over a plain studio (`ClubStage` scene "studio": a soft gradient and a shadow, no painted art),
a white sheet below with six tabs (Skin, Hair, Face, Hats, Outfit, Gear). Each tab scrolls; a style is a picture tile
(`Resources/UI/Look/<kind>_<name>.png`, rendered from the kit by `avatar_mock.py --mode icons`), colours are swatches
with a quiet slider. The Hair tab shows the haircut with no hat over it (`GolferStyle.PreviewBareHead`).

## Head space

Everything on the head is made in *head space*: the head's own centre (0, 0, 0), unscaled, the face looking down -Y,
the skull block `HX, HY, HZ = 0.198, 0.190, 0.200`. `avatar_base.place_head` scales it by `HS` (1.25, the big icon
head) and moves it to `HEAD_POS` on the body. Parts never know the scale, so changing `HS` (or giving one avatar a
bigger head) needs no part to be touched.

## The promises that make parts interchangeable

* **Hair stands at most `HAIR_TOP` (3.6 cm) off the skull at the crown; a hat's inside is `HAT_CLEAR` (4.4 cm) or
  more off it.** So every hat clears every haircut. No per-pair fitting.
* A haircut built with `under_hat=True` has no volume above the hat line (nothing rooted higher than 32 degrees of
  elevation, a flatter crown): the game ships it as a blend shape and turns it on when a hat is worn.
* Glasses rest on the nose socket and end at the ears; a hat's bill stays above the brows.
* A top is drawn a few millimetres clear of the body skin, which is a full torso, arms and legs underneath; a
  sleeveless top or a change of shorts needs nothing cut away.
* **No part has a texture.** A part's colours are its *material roles* (`Hero_Skin`, `Hero_Hair`, `Hero_Top`,
  `Hero_TopTrim`, `Hero_Accent`, `Hero_Bottom`, `Hero_HatA`, `Hero_HatB`, `Hero_Frame`, `Hero_Lens` ...). The game
  tints a role (HeroKit shader, by material name), so one mesh is every colour: skin tone, hair colour, outfit.

## Adding a part

1. Write one function in `avatar_head_parts.py` (head) or `avatar_parts.py` (body) that returns one joined mesh
   using only `Hero_*` roles, made against `HX/HY/HZ` and the sockets in `avatar_base.SOCKETS`.
2. Register it in the dict at the top of `avatar_mock.py` (`HAIR`, `HATS`, `GLASSES`, `BEARDS`) and add a card to
   `board.py`'s kit board. It now appears in the closet and the parts lineup.
3. Keep it under its triangle budget: a haircut about 2k, a hat 1.2k, glasses 1.2k, a top 2k, shoes 2k.

## Budget

The default Hero is about 17k triangles before the reduction pass (head 1.5k, face 2k, body skin 3.5k, polo 2k,
shorts 1.2k, shoes 2.9k, hair 2.2k). Targets for the game are about 8-10k total for the avatar with a hat.
