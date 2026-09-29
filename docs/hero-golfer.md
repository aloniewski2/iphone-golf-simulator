# The Hero golfer

The golfer is Adnan's modular "Hero" (from `origin/tennisgameplaydone-needtofixcharacters`, his
`docs/character-handoff`): one skeleton, and every visible part a separate skinned mesh that can change
without touching another. This is how it is put together in the golf game, how to change it, and what was
repaired on the way.

## The parts

| Slot | Mesh(es) | Where |
|---|---|---|
| Skeleton, the golf clips, the clubs | `Hero_01_Rig` (42 bones + `Club`), `CLUB_DRIVER/IRON/WEDGE/PUTTER` | `Resources/Hero/hero_golf.fbx` |
| Body (face, arms, legs; the girl is the `Female` blend shape) | `Body_Skin` | `hero_body.fbx` |
| Eyes | `Body_EyeSphere_L/R` | `hero_eyes.fbx` |
| Head | `Head_Scalp` (one closed skull: skin below its hairline, hair colour and the buzz/waves map above) | `hero_hair.fbx` |
| Hair | `Hair_Default` (swept), `Hair_Ponytail`, `Hair_Bob`, `Hair_Long`, `Hair_Curly`; each with blend shapes `Under_Visor`, `Under_Cap`, `Under_Sweatband` | `hero_hair.fbx` |
| Headwear | `Hat_Visor`, `Hat_Cap`, `Hat_Sweatband`; each with a blend shape `OnHair_<cut>` per haircut | `hero_visor/cap/sweatband.fbx` |
| Shirt, shorts, shoes (and socks) | `Shirt_Default`, `Shorts_Default`, `Shoes_Default` (shirt and shorts have `Female`) | `hero_shirt/shorts/shoes.fbx` |

`HeroGolfer` (`Scripts/Game/HeroGolfer.cs`) builds the figure: it instantiates `hero_golf`, then each part, and
**rebinds every part's `SkinnedMeshRenderer.bones` to that one skeleton by bone name, in the part's own
bone order** (copying one renderer's array to another deforms badly). `GolferView` plays the clips on it and
holds the swing contract (phone load scrubs the backswing, impact time comes from the clip's landmarks).

## The look (`CharacterLook`, `Profile/CharacterLook.cs`)

A versioned record with stable ids, kept on each profile and on the phone:
`Body` (0 boy, 1 girl), `Skin`, `Haircut` (`HeroGolfer.Haircut`; -1 follows the body), `Hair`, `Headwear`
(`HeroGolfer.Headwear`), `Shirt`, `Shorts`, `Shoes`, `Hat`. Colours are `RRGGBB` hex; empty is the kit as
designed. Older saves (body, kit and shirt only) are migrated on load; the server still gets body, kit and
shirt. `GolferStyle` is the facade the game uses (`Current`, `Edit`, `Wear` for local versus).

A look change never rebuilds the figure: `HeroGolfer.Dress` shows the right parts and sets the colours, so a
slider can drag live.

## Adding or changing a part

1. Make the mesh on the shared rig (same bone names; Adnan's `Hero_01_Assembled.blend` is the reference).
2. Add it to `PARTS` in `blender/scripts/hero_parts_export.py` (or replace the source FBX), add repairs in
   `hero_repair.py` if it needs them, and run the script (below). A new haircut goes in `CUTS` and a new hat
   in `HATS` (a hat needs a colour channel for its hold: G/B/A are taken, so a fourth hat needs one more);
   the head export fits every hat to every cut by itself.
3. Name its materials by role (`Hero_01_HairTuft`, `skin_*`, `Hero_01_MatteCloth`...): `HeroGolfer.AssignMaterials`
   picks the material by name, so a renamed material silently loses its colour.
4. Add its id to `CharacterLook` / `HeroGolfer` and a card to the locker (`UI/Locker.cs`).

```bash
# the parts, repaired, into Unity/Assets/Resources/Hero (sources: Adnan's Hero01 folder from his branch, Git LFS);
# --only body,head (for example) redoes just those; "head" is the scalp, the hair and the three hats together
Blender -b --factory-startup --python blender/scripts/hero_parts_export.py -- --hero-dir <Hero01 dir>
# the skeleton, the golf clips retargeted from the V4 golf rig, the clubs
Blender -b --factory-startup --python blender/scripts/hero_golf_retarget.py -- --hero-dir <Hero01 dir>
# preview the assembled Hero mid-swing
... hero_golf_retarget.py -- --hero-dir <Hero01 dir> --out /tmp/x --preview /tmp/x --atlas <BaseColor.png> --frames 1,53,71,91
```

## The head, the hair and the hats (`blender/scripts/hero_head.py`)

Adnan's Hero was sculpted wearing a visor, and it showed: the body's head stopped at the forehead in a torn
lip, the skull existed only as the bald piece set well back from the face (a hole in the front of the head),
and every haircut was built on a flat ring where the visor's band pressed it (without the visor, that ring
stood out from the forehead like a brim and the hair floated above the head). Now:

* **One closed head.** `build_scalp` makes a skull round the head's centre: out to Adnan's skull where there
  is skull, just under the face where there is face, and the forehead and temples he never modelled filled in
  smoothly. `trim_face` cuts the face's torn top back to a smooth line and `blend_face_edge` eases its last
  2 cm down onto the scalp, so the forehead runs into the scalp with no step, ridge or sawtooth. The scalp's
  vertex colour R is its hairline (skin at the forehead and temples, hair above); the `HeroKit` shader paints
  it (`_UseScalp`): bald is all skin, buzz and waves are their short-hair maps on it, and under every other cut
  it is the hair's colour, so nothing shows through gaps in the hair.
* **Hair that sits on the head.** Each cut is Adnan's hat-less ("_Free") sculpt with the visor ring removed
  (`remove_band`) and lowered onto the scalp (`seat_hair`: in every direction it comes in by the gap between its
  innermost layer and the head, so its roots touch and its thickness is kept).
* **Hats over the hair, not cut into it.** `fit_hat` scales and moves a whole hat (keeping its brim's angle) so
  its lower edge meets the head all round, then eases it out wherever it would still sink in; open shells get a
  back face (`double_side`, so a brim shows from below). Its base shape sits on the bare head (bald, buzz,
  waves); each `OnHair_<cut>` shape sits on that cut: a band (visor, sweatband) rides on most of the hair,
  as a headband does on big hair, and a cap presses it flat under its crown. Each cut's `Under_<hat>` shape
  tucks in only the hair still outside that hat, easing the strands in under its edge; vertex colour G/B/A
  says how firmly the visor/cap/sweatband holds each vertex, and the shader keeps held hair from swaying out
  through the hat (`_HatHold`).

`HeroGolfer.Dress` shows the scalp always, the cut's mesh (none for bald, buzz, waves), the hat, and sets the
blend shapes: `OnHair_<cut>` on the hat, `Under_<hat>` on the hair. `HeroGolferTests.EveryLookBuilds` checks
all of that for boy and girl × 8 cuts × 4 headwear, and captures each close up, front and side.

## Other repairs

* **The hair moves.** `hero_repair.hair_flex` writes each hair vertex's distance from the scalp as the R of
  a vertex colour; the `HeroKit` shader swings the hair by it and `HeroGolfer.Sway` drives that with a damped
  spring on the head's acceleration (plus a faint stir), so the ends lag and settle and the roots stay.
* **The legs were fat.** Thighs and calves (body), the shorts' legs and the socks are slimmed about each leg's
  axis by bone weight (`hero_repair.slim_legs`; every shape key too, or the girl would undo it).
* **The clothes** carried Tripo noise: a light Laplacian relax on the body, shirt and shorts (hems, seams,
  fingers and the face are left alone).

## The golf swing on the Hero

The clips were authored on the V4 golf rig. The Hero has the same 22 bone names plus fingers, so
`hero_golf_retarget.py` transfers each bone's world-rotation change, scales the hips' travel by leg length,
keeps the club exactly where V4 had it (so ball contact is unchanged: the test measures the Hero's clubhead
0.18 yd from the ball at address and 0.20 yd at closest approach, the V4 golfer's 0.18 and 0.20), reaches both
hands to where V4's hands held it with two-bone IK, and curls the fingers round the grip.

## Known limits

* Body size, height and face shape are not offered: the Hero has no size morphs, and the handoff asks for
  no inert controls.
* Left-handed swings are not made (the game has no handedness yet).
* Spectators are still the older V4 figures.
* Hair cuts are Tripo sculpts of 10-12k vertices each; they are the heaviest part of the build.
* The atlas still carries a faint shadow at the top of the forehead where the visor's band sat (a slightly
  darker line on a bald head).
