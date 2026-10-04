# The Hero golfer

> **2026-09-30: the Hero's parts are now the avatar kit** (`docs/avatar-kit.md`, `blender/scripts/avatar_*.py`):
> a new body, head, face, haircuts, hats, glasses, shirt, shorts and shoes in the soft clay "icon avatar" style,
> on the same skeleton and golf clips. Adnan's scanned parts, the repair scripts that mended them (`hero_head.py`,
> `hero_repair.py`, `hero_hair.py`, `hero_hats.py`, `hero_parts_export.py`) and the atlas/mask recolouring are no
> longer used by the game; the sections below this table describe that older pipeline and are kept for reference.
> The parts, the skeleton and the rebinding are as described next.

The golfer is the Hero: one skeleton (Adnan's `Hero_01_Rig`, from `origin/tennisgameplaydone-needtofixcharacters`),
and every visible part a separate skinned mesh that can change without touching another. Nothing is textured: a
part's colours are material roles (`Hero_Skin`, `Hero_Hair`, `Hero_Top`, `Hero_HatA` ...) that `HeroGolfer` tints.

## The parts

`blender/scripts/avatar_export.py` writes them (envelope skin weights, two bones a vertex at most; head parts rigid to `Head`).

| Slot | Mesh(es) | Where |
|---|---|---|
| Skeleton, the golf clips, the clubs | `Hero_01_Rig` (42 bones + `Club`), `CLUB_DRIVER/IRON/WEDGE/PUTTER` | `Resources/Hero/hero_golf.fbx` |
| Body (neck, torso, arms, hands, legs) | `Hero_BodySkin` | `hero_body.fbx` |
| Head and face (eyes, brows, nose, mouth, blush, ears) | `Hero_Head`, `Hero_Face` | `hero_head.fbx` |
| Hair | `Hair_<Swept\|Ponytail\|Bob\|Long\|Curly\|Crop\|Spikes>`, each also as `Hair_<Cut>_Hat` (the crown pressed down, shown when a hat is on); bald is no mesh | `hero_hair.fbx` |
| Headwear | `Hat_Visor`, `Hat_Cap`, `Hat_Bucket`, `Hat_Beanie`, `Hat_Straw` | `hero_hats.fbx` |
| Glasses, beards | `Glasses_Round\|Square\|Shades` (`HeroLook.Glasses`), `Face_Beard`, `Face_Mustache` (in the kit, not yet in a menu) | `hero_glasses.fbx` |
| Shirt, shorts, shoes (and socks) | `Top_Polo`, `Bottom_Shorts`, `Shoes_Golf` | `hero_shirt/shorts/shoes.fbx` |

Old part files (`hero_eyes`, `hero_visor`, `hero_cap`, `hero_sweatband`, `Look/`) are no longer loaded.

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
   `hero_repair.py` if it needs them, and run the script (below). A new haircut is a `Cut(...)` in
   `hero_hair.make_cuts()` (a hem and how its locks lie) and a new hat is a builder in `hero_hats.py` (a hat needs
   a colour channel for its hold: G/B/A are taken, so a fourth hat needs one more); the export fits every hat to
   every cut by itself.
3. Name its materials by role (`Hero_01_HairTuft`, `skin_*`, `Hero_01_MatteCloth`...): `HeroGolfer.AssignMaterials`
   picks the material by name, so a renamed material silently loses its colour.
4. Add its id to `CharacterLook` / `HeroGolfer` and a card to the locker (`UI/Locker.cs`).

```bash
# the parts, repaired, into Unity/Assets/Resources/Hero (sources: Adnan's Hero01 folder from his branch, Git LFS);
# --only body,head (for example) redoes just those; "head" is the shell, the haircuts and the three hats together
Blender -b --factory-startup --python blender/scripts/hero_parts_export.py -- --hero-dir <Hero01 dir>
# the skeleton, the golf clips retargeted from the V4 golf rig, the clubs
Blender -b --factory-startup --python blender/scripts/hero_golf_retarget.py -- --hero-dir <Hero01 dir>
# preview the assembled Hero mid-swing
... hero_golf_retarget.py -- --hero-dir <Hero01 dir> --out /tmp/x --preview /tmp/x --atlas <BaseColor.png> --frames 1,53,71,91
```

## The head, the hair and the hats

Adnan's Hero was sculpted wearing a visor, and it showed. His body's head is a mask: a thin face plate that stops
at the forehead in a torn lip, wraps the cheeks in a ragged edge and has shards of geometry where the ears should
be; behind it there was nothing but hair. His haircuts were 10-12k-vertex scans of noisy clay built round the
visor's band (faceted, torn at the edges), and his hats were scans to match. Worn without his hair, or on any other
head, they read as ragged black blobs and floating brims. So the head, the hair and the hats are now made here
(none of his hair or hat sculpts is used):

* **One closed, round, low-poly head** (`hero_head.py`). His body's head is a mask (a thin, lumpy face plate with a
  torn top lip and ragged cheek edge) on a skull set far behind it, so patching or blurring it left a dented,
  bean-shaped head, and none of it is used. `build_round_head` makes the head: an egg (`EGG`: a rounded, box-ish
  ellipsoid with squarish cross-sections, full cheeks and a jaw that narrows) the size of his face and skull
  together, on a 40 x 28 UV sphere (about 1.5k vertices), with a nose raised on it (`NOSE`), an ear on each side,
  and the mouth (a smile) and the brows as thin dark strips lying on it (material `Hero_01_FaceLine`, a dark brown
  in `HeroGolfer`). His eyeballs stand out of it. The shell's vertex colour R is the hairline (`HAIRLINE_PTS`:
  skin on the face, temples, ears and nape); the `HeroKit` shader paints it (`_UseScalp`): bald is all skin, buzz
  and waves are their short-hair maps on it, and under every other cut it is the hair's colour, so nothing shows
  through gaps in the hair. `fix_neck` (run on the body) takes his mask off it and mends the neck below: the shards
  at the collar are peeled, thin flares of skin standing off the neck go, and a closed tube (a circle fitted to the
  whole front of the neck at each height, skinned like the neck beside it) fills the hole that was behind the
  collar, where Adnan's hair used to hide it. To tune the head, change `EGG` and preview it in Blender (the
  proportions are size, width and depth of the egg, how square the cross-sections are, how much the jaw narrows).
* **Hair made to sit on it** (`hero_hair.py`). A haircut is a hem (how low the hair reaches at each angle round the
  head) and a set of locks. Each lock is a tapered, flattened tube started at a point on the hem and followed *up*
  the head, the way hair falls backwards: along the head above its equator, straight up where it hangs below it,
  pushed off the head, the face and the shirt so it never sinks in; then reversed, root to tip. Locks overlap like
  shingles (three layers, a crown swirl, a sweep for the swept cut, a bell of volume for the bob and long), a thin
  cap under them closes any gap at the crown, and the ponytail is a bundle of locks thrown from a low tie.
  Curly is two layers of lumpy puffs. Each is 10-18k triangles. Vertex colour R is the lock's flex (0 at the root,
  1 at the free end): the shader swings the hair by it and darkens the roots a little, so each lock reads as its
  own strand. Skinning: the head above the ears, the neck below them, the chest for the ends that reach the
  shoulders (so long hair rides on the back instead of through it).
* **Hats made to sit over the hair** (`hero_hats.py`). Each hat is a shell that follows the head's envelope: the
  bare head, or the haircut with the hair pressed a little under the hat. It is built once per fit with the same
  topology, so the bare-head fit is the base shape and each haircut's fit is a blend shape `OnHair_<cut>`; the hair
  under a hat is pressed by the same coverage function (`Under_<hat>` on the haircut), and the coverage is also the
  hat's hold channel in the hair's vertex colour (G visor, B cap, A sweatband) so the shader does not sway held
  hair out through the hat. A cap presses hair hard (it is a dome over it); a band rides on most of it, as a
  headband does on big hair. The cap has six panels with raised seams and a button, and the visor and cap bills
  are curved slabs from the hat's lower front edge. Face normals are set outward by signed volume
  (`orient_outward`): Blender's own recalculation guesses *inward* on a thin ring, and the game culls back faces,
  which turned a sweatband into a band seen only from inside.
* **Materials by role.** The haircuts use `Hero_01_HairTuft` (their colour is the look's); the hats use `Cos_Hat`
  (the look's hat colour: the cap orange, the visor and sweatband white until one is picked) and `Cos_Trim` (bill,
  stripes and button: navy on a light hat, a deeper shade of the hat's own on a dark one).

`HeroGolfer.Dress` shows the shell always, the cut's mesh (none for bald, buzz, waves), the hat, and sets the
blend shapes: `OnHair_<cut>` on the hat, `Under_<hat>` on the hair. `HeroGolferTests.EveryLookBuilds` checks all of
that for boy and girl × 8 cuts × 4 headwear, and captures each close up, front and side.

## Other repairs

* **The hair moves.** Each lock's flex (its length from root to tip) is the R of a vertex colour; the `HeroKit`
  shader swings the hair by it and `HeroGolfer.Sway` drives that with a damped spring on the head's acceleration
  (plus a faint stir), so the ends lag and settle and the roots stay.
* **The legs were fat.** Thighs and calves (body), the shorts' legs and the socks are slimmed about each leg's
  axis by bone weight (`hero_repair.slim_legs`; every shape key too, or the girl would undo it).
* **The clothes** carried Tripo noise: a light Laplacian relax on the body, shirt and shorts (hems, seams,
  fingers and the face are left alone). The shirt's collar was
  torn at the back of the neck (his hair covered it): `hero_repair.mend_collar` peels the shards and smooths the
  rim, which is what the camera sees at address, from behind.

## The golf swing on the Hero

The clips were authored on the V4 golf rig. The Hero has the same 22 bone names plus fingers, so
`hero_golf_retarget.py` transfers each bone's world-rotation change, scales the hips' travel by leg length,
keeps the club exactly where V4 had it (so ball contact is unchanged: the test measures the Hero's clubhead
0.18 yd from the ball at address and 0.20 yd at closest approach, the V4 golfer's 0.18 and 0.20), reaches both
hands to where V4's hands held it with two-bone IK, and curls the fingers round the grip.

## Known limits

* Body size, height and face shape are not offered: the Hero has no size morphs, and the handoff asks for
  no inert controls. The face is the same simple one for everybody (eyes, brows, nose, smile).
* Left-handed swings are not made (the game has no handedness yet).
* Spectators are still the older V4 figures.
* The haircuts are 10-18k triangles each (the long and bob the most): they are the heaviest part of the build.
