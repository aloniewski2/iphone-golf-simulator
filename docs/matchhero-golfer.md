# The match-hero golfer

2026-10-04: the golfer is **Adnan's match hero** (`HERO_MAINSTAY`, branch `origin/newmapsandmenus`), the boy and the girl:
the grey-bodied, painted-face characters in the white tennis kit, with his default hair. They replaced the icon avatar kit
(`docs/avatar-kit.md`), which is kept in `Unity/Assets/Archive/IconKit/` and `blender/scripts/avatar_*.py` for when its hats and
hair are fitted to these heads.

    blender/scripts/fetch_matchheroes.sh        downloads his sources (Git LFS) into blender/matchhero/ (not committed)
    blender/scripts/matchhero_golf.py           builds one golfer FBX per sex and gives it the golf clips
    blender/scripts/matchhero_icons.py          the locker's picture tiles from a render of each head
    Unity/Assets/Resources/Hero/golfer_m.fbx    the boy: rig, body, face, kit, hair, four clubs, nine clips   (golfer_f.fbx the girl)
    Unity/Assets/Resources/Hero/golfer_m_clips.json   top and impact frames per clip, where the club head is at address and impact
    Unity/Assets/Scripts/Game/HeroGolfer.cs     builds the figure and dresses a look on it

## What he made, and what we took

His branch is the tennis game (24k files). The golf game takes only the characters:

| His file | What it is | Used |
|---|---|---|
| `Characters/MatchHeroes/<Sex>/<Sex>_ReadyIdle.fbx` | the 53-bone rig (humanoid bone names, Generic), the body (66k / 44k triangles), the painted face (13 / 14 materials of layered decals), the racket | rig, body, face (the racket is dropped) |
| `Characters/MatchHeroKit/<Sex>_Kit.fbx` | the same rig and six garments: polo (`Kit_Top`), shorts or skort (`Kit_Bottom`), two socks, two shoes | all six |
| `Characters/HeroBase/Models/Hair_Default_<M\|F>.fbx` | his default hair, static | skinned rigid to `Head`, put at the `HairRoot` of his README |
| `Characters/HeroBase/*_Body.fbx` | bald mannequins, **no skeleton, no clothes, his own gate says FAIL** | not used |
| the tennis clips, URP shaders (`TennisCharacter`, `TennisCloth`) | tennis | not used (the game is Built-in RP: `HeroKit.shader`) |

## What `matchhero_golf.py` does

1. **Assemble.** His rig, the body, the face (skinned rigid to `Head`), the kit and the hair on one skeleton; the V4 studio's four clubs
   (scaled to him) on a `Club` bone.
2. **Reduce.** 150k triangles is too many for a phone. The body skin the clothes cover (a ray out of each face along its normal meets
   the kit) is deleted, so is any scrap left over, and a thinned body can no longer poke through the cloth. The rest is
   collapse-decimated with the head protected (the painted face sits on the head's own surface) and the kit to a budget:
   about **71k triangles** the boy, **62k** the girl. His hair is lifted 3 % so it never z-fights with the scalp.
3. **The golf swings, authored from scratch** (2026-10-06, "make the animations from scratch based off them", after "fix the arms and the body, they should be like real golf form").
   Drive, IronSwing, HalfSwing, Chip and Putt are not the studio's clips retargeted (that was `--v4swing`, kept for comparison: its V4 golfer held its hands at chest height on a
   hunched back, and its clubs were left-handed models turned a quarter round). `blender/scripts/golf_swing.py` builds each swing the way an animator would: **key poses** (address, takeaway,
   the lead arm parallel, the top, the first move down, the club hip-high, impact, follow-through, finish), each a handful of numbers a golf coach uses, **smooth curves** through them
   (monotone cubics, so nothing wobbles between poses) and a **solver** that poses his skeleton from the numbers. What real form is, in numbers, comes from instruction and from measuring
   real golf: `blender/scripts/cmu_golf_measure.py` reads CMU's public motion capture of a golfer (subject 64: shoulder turn 80 degrees and hips 50 at the top, -4 and -32 at impact, the lead
   arm long and the trail elbow folded to ~70 degrees at the top, the knees straightening into the ball, when each happens). Nothing is copied from it.
   * **The golfer's frame** is the clips' root space: the ball 0.75 in front along -X, the target along -Y, up +Z (a right-hander). The body is a pelvis (turn, forward tilt, side tilt, slide toward
     the target, push, lift), a spine that twists between the hips and the shoulders (turn about the tilted spine axis, forward bend, side bend away from the target), a head that turns less than the chest,
     collar bones that turn a little further than the chest (the lead shoulder slides across), planted feet (the lead one flared, the trail heel comes up through the ball) and knees from a two-bone solve.
     The pelvis is never higher than his legs allow, so a planted foot stays planted.
   * **The address is found from the club**, per club: the club is placed at the ball (face centre one ball radius behind the ball's centre, lowest point of the head on the ground, the shaft at the
     club's real lie), then a pattern search finds the bend from the hips, the pelvis's and the feet's places and the shoulders' tilt for which both arms are stretched as a golfer's are (the lead
     arm ~0.92 of its length, the trail ~0.95) with knees bent 20-35 degrees (`address_solve`). A driver stands taller and further from the ball than a putter crouches.
   * **The club is what is authored**: the lead hand's place in the chest's own turning frame (in units of his reach, so the arms stay in front of the chest as it turns), the shaft's direction
     (in world terms: parallel to the ground and the target line at the takeaway and the top, vertical at the third position, leaning forward at impact) and which way the face looks. The impact
     zone is five keys along the club head's own arc through the ball. The hands sit on the grip with the palms facing each other across it; each hand's fingers are fanned to the angle the
     grip runs across the palm (it follows the wrist's cock), and the arms are solved from the shoulders to the wrists (an elbow is kept out of the torso). A reach assist at impact bends him
     forward only as far as lets both hands reach the grip.
   * **The arms never tangle** (2026-10-06, "the arms should not tangle"). The grip turns each hand a long way about its forearm (60-160 degrees in a swing; the lead hand's face-closing through the ball
     is faster still), and one bone per segment cannot twist that at the wrist without wringing the skin. So the forearm turns with the hand most of the way (55 %), the upper arm a little (25 %),
     the rest stays at the wrist; the amount is smoothed over a few frames (a forearm does not whip with the club) and every turn is unwrapped along the swing's own sequence
     (`Swing.twist_reference`), so a bone never takes the long way round between two frames. An elbow's bend direction comes from where the elbow stands out of the shoulder-wrist line and, as the
     arm straightens and that is unsteady, from the hint's own direction; the torso-clearance push turns it at most 40 degrees and to the side that clears better, so it cannot flip. The finish and
     follow-through keys leave the face's turn about the shaft to the shortest turn from the previous key (the arms used to wind up by 330 degrees by the finish). The pipeline prints the quickest any
     bone turns in a frame (about 45 degrees; a flip was 130).
   * **The clubs** are mirrored (the studio's were left-handed models: toe +X, face -Y, which is a right-hander's club in a mirror) and the head tipped about the face's axis so the sole sits
     flat at the club's lie (`prepare_club_mesh`; the lower shaft eases into it over the hosel); 56 degrees the driver, 62 the iron, 64 the wedge, 70 the putter.
   * **The timing** (`Swing.retime`): authored poses are sampled at times that make the club head's speed rise as tau^1.2 from the top to its peak AT impact and fall away after it (the exponent after
     the ball keeps the speed continuous), the clip's top, impact and finish frames unmoved. The game plays these clips at an even rate (`GolferView.SetClub`).
   * The pieces: `full_swing` (driver: top 56, impact 69; iron: 54, 66), `half_swing` (a pitch: the backswing to the third position, a short balanced finish), `chip`, `putt` (the arms and the
     shoulders rock as one). A spare 0.2-3 cm of reach is looked at in the log (`authored, ... the hands miss the grip by ...`).
   * Looking: `--preview <dir> --frames ...` as before (face-on `f`, down-the-line from behind `b`/`g`); `--v4swing` for the studio's.
   * A **free arm** (Wave, Cheer, FistPump) still reaches as V4's did from ITS shoulder, scaled by the arm; those clips are the studio's, retargeted (the `retarget_clip` code stays for them).
   His own clips (Walk, RunForward, Emote_Scuba/Spike/Thrust, Intro_BringIt/Pushups/Wave) are copied onto the rig unchanged (`BORROWED`): in place, 30 fps.
4. **The ball.** At address the club's face is against a ball 0.75 m in front of the golfer's origin (where `GolferView.Stand` puts it; a driver's is on a tee) and at impact
   it passes it. The landmarks in the JSON (top, impact, where the head is) come from the authored swings.
5. **Export** at **30 fps** (his files set the Blender scene to 24, which Unity plays 25 % slow: the swing never reached the ball).

Run it (about 15 s a sex):

    blender/scripts/fetch_matchheroes.sh
    Blender -b --factory-startup --python blender/scripts/matchhero_golf.py -- --sex Male      # then Female
        [--preview <dir> --frames 1,53,71,91 --only Drive]      renders instead of exporting
        [--assemble-only --preview <dir>]                       the rest pose, from the front and the head
        [--icons --assemble-only --preview <dir>]               the locker's tiles
        [--nokit | --nobody | --hips | --diag | --dbg]          looking at one part, the hips, the numbers
        [--stance | --elbows | --turn | --armdiag | --miss]     the posture, the elbows, the shoulder and hip turn, arms in the torso, where the arms fall short
        [--v4like]                                              V4's poses scaled up (the first version), for comparison

## The hairstyles

Twelve choices, any of them on the boy or the girl (2026-10-05). Not photo-real: the characters are painted, so the hair is drawn, chunky, soft-shaded, with clean edges.

| Name | Where it comes from |
|---|---|
| Classic | his own hair (a side-swept quiff, the girl's a bob), **with a close scalp cap in the hair colour under it** (his hair left bare patches of scalp: the girl's crown, the boy's temple) |
| Bald | no hair part |
| Crop, Fringe, Mop, Quiff | MakeHuman `short04`, `short03`, `short02`, `short01`: a slicked side-parted crop, a long side fringe over one eye, a layered mop, a textured quiff |
| SideBob, Bob | MakeHuman `bob01` (side-swept) and `bob02` (blunt) |
| Long, Ponytail, Braid | MakeHuman `long01` (straight, centre parted), `ponytail01`, `braid01` (a side braid) |
| Afro | built here: a thick cap covered in big curl balls (`matchhero_hair.afro`); MakeHuman's `afro01` is a lattice of small cards with open ear cut-outs and could not be made solid |

**The free models.** MakeHuman's system assets (makehumancommunity.org) are CC0: no credit owed, but it is given here. Only the hair files are fetched (35 MB of the 267 MB
pack, by HTTP range requests out of the zip):

    python3 blender/scripts/fetch_mhhair.py [--src DIR]    -> blender/mhhair/hair/<folder>/*.obj, *.png (not committed)
                                                              and Unity/Assets/Resources/Hero/Hair/<Name>.png (committed: the game's textures)
    blender/scripts/mhhair_sources.py                      which hair is which game name, and how far its inner surface stands off the scalp

Each hair is a card mesh (1-8k triangles: a shell of overlapping alpha-cut strips) with a 2048 px strand texture. They are **stylised** in `fetch_mhhair.py`, the way a
stylised game does it (Fortnite is the reference: drawn locks, painted streaks, a highlight, clean edges; not photographed strands, not a smooth blob): the single hairs are
blurred away, the locks (the band between a ~2 px blur and a ~13 px one) are kept and sharpened into the colour, and the big dark patches the originals have painted in are
dropped (some hairs more: `OVERRIDE`), the alpha is lightly blurred and cut again so lock tips stay as clean points and thin gaps fill. 1024 px, grey, so any hair colour tints it.

**Fitting** (`matchhero_mhhair.py`, run by `matchhero_golf.py` against his own heads before they are reduced). They were all made on MakeHuman's one head, but not in one place
(their heights differ by up to 2 cm in its coordinates), so: one scale for the lot, found over the four close cuts (rays out of the scalp must meet the hair a given gap off it:
a missing or too small gap is a bald spot or a push-through, and costs most); then each hair placed (front/back, up/down) at that scale; then pushed clear of the head, the ears
and the shirt (nothing inside a collider), the push smoothed over its neighbours (below the chin the margin grows to 3 cm: the hair rides the head bone and the shoulders move under it, which let the boy's polo through his long
hair). The boy's head is about 14 % bigger than the girl's in these units (scale 1.34 against 1.18).

**In the game.** `HeroGolfer.MakeCard`: a `GolfArcade/HeroHairCard` material per cut (`HairCard_<Name>` in the FBX), the strand texture and the hair colour. The shader
(`Shaders/HeroHairCard.shader`) is alpha-cut, double sided (the back face is lit with its normal turned round), lit like HeroKit (a MatCap and a wrapped sun) with a soft sheen.
Textures import with `mipMapsPreserveCoverage` so the cut-out keeps its size down the mips (`HeroModelImporter`). The phone has no MSAA, so the edge is a hard cut.

**Soft hairlines and sides** (2026-10-05, after "the hairlines and sides are sharp and wonky"). The cards used to stop dead at the temple, the sideburn and the forehead. Four things now:
* the open edge of each card is rounded (`smooth_boundary`, 16 passes along the edge) and dissolves: `edge_alpha` bakes the vertex colour's A (0 on the open edge, 1 about a centimetre in), and
  `HeroHairCard.shader` dissolves it, with the texture's own soft alpha ramp, by a fine screen-space dither (interleaved gradient noise: no MSAA on the phone, so no alpha-to-coverage);
* a **soft scalp** under every cut (`fade_cap`, material `Hair_Scalp`): a skin of the head at full resolution, laid 2 mm off the real skin over everything the hair covers (the hair's own surface
  projected on the scalp, gaps under 3 cm closed) and `FADE` metres beyond it, vertex colour R = 1 under the hair, falling to 0 (the skin) at the end of the fade: a hairline or a taper with no
  line. It stays above a line round the head (never down over the brows), and fades out round the ears. HeroKit's `_UseScalp` mode draws it (hair colour to the skin tone by R); the Classic has one too;
* the textures' alpha ramp is wider (`ALPHA_LO/HI`), the back of a card a touch darker (no bright rim);
* test: `HeroGolferTests.EveryCutsScalpFadesIntoTheSkin` (the colour channel and the scalp-mode material are in the build; the meshes are not readable on purpose, so the values cannot be).

**No bald spots, no overlaps from any side.** Checked three ways:

    Blender ... matchhero_golf.py -- --sex Male --hair-check                         rays from outside the head: any direction where the scalp above a cap-built style's hairline shows (Classic, Afro)
    Blender ... matchhero_golf.py -- --sex Male --assemble-only --hair-turn --preview <dir>     every style from 24 sides (every 45 degrees at three heights, from above, from low behind)
    PlayMode HeroGolferTests.EveryHaircutFromEverySide                               in the game: each cut on both bodies from 8 sides up close, and the long cuts on the whole golfer
                                                                                    (Unity/Library/Captures/hero/spin; `GolfGame.TurnPickerGolfer` holds the picker's golfer at an angle)

A tile picture for each (`UI/Look/hair_<name>_m|_f.png`) comes from `matchhero_golf.py --icons` and `matchhero_icons.py`. To add a free hair: a line in `mhhair_sources.py`,
`fetch_mhhair.py`, its name at the END of `HeroGolfer.HaircutNames`, the count in `CharacterLook.Haircuts`, the export and the icons. The cap-built Classic scalp and Afro are in `matchhero_hair.py`
(`Skull.cap`, `exposed`).

## In the game

`HeroGolfer.Build` instantiates `Hero/golfer_<m|f>` (the clips and the clubs come with it, `GolferView.BuildHero` plays them as before) and
makes a `GolfArcade/HeroKit` material for every material name the FBX uses: the skin (`Blockout_Grey`, `Base_Grey_F`, `Skin_F`) takes the
skin tone, `Hair_M|F` the hair colour, the painted face its authored colours (a seam, lip or nostril keeps its ratio to his skin, so it is
a shade deeper on any tone; the brows take the hair's colour), the kit its colours (the trim is a deep shade of a light colour, a light one of a
dark colour). The face layers sit a hair in front of the skin, so each has a depth offset (`_OffsetFactor/_OffsetUnits`) or they z-fight at distance.
The skin is up to four bones a vertex, so `QualitySettings.skinWeights` is raised to 4 (the phone default is 2).

The styles. `HeroGolfer.HaircutNames = {Classic, Bald, Short, Curly, Long, Tail}`; headwear, glasses, facial hair, tops and bottoms each have just
the one entry (none, none, none, Polo, Shorts), so the locker has no FACE or HATS tab (`Locker.TabAvailable`) and no style row on the OUTFIT tab.
A look saved with the icon kit (`CharacterLook.Version` 1) keeps its colours, its bald head stays bald, every other cut is the classic cut.

## Adding a part

A haircut, hat, pair of glasses or beard is a mesh named `Hair_<Name>` / `Hat_<Name>` / `Glasses_<Name>` / `Face_<Name>`, skinned to the golfer's rig
(rigid to `Head` for these), in the golfer FBX (add it in `matchhero_golf.py` next to the hair) with materials named for their role; add its name at the
END of its list in `HeroGolfer` and bump the count in `CharacterLook`; `Dress` shows the picked one, and `EveryStyleIsAPartInTheBuild` fails until the
part and its tile picture (`UI/Look/hair_<name>_m|_f.png` for hair, `<kind>_<name>.png` for the rest) are both in the build. His head is not the icon
head: the kit's hats (head space of `avatar_base.py`, scale 1.25) are about 2.2 times too big and need refitting, not a re-export.

## Known, and next

* A little skin can still show at the armhole and the shorts' leg in the deepest part of a swing; the same shows on his own meshes.
* Adnan's Walk, RunForward, emotes and intros are in the FBX but nothing in the game plays them yet: a walk to the ball and celebrations are the next hook-ups
  (`GolferView.Perform(name)` plays any clip by name).
* The swings are authored, but still one style: a tidy textbook swing for everyone (no amateur's slice, no fast tempo). Another golfer's swing is a different table of numbers in `golf_swing.py`.
* The studio's motion-capture data used to measure real form: "The data used in this project was obtained from mocap.cs.cmu.edu. The database was created with funding from NSF EIA-0196217."
  (CMU Graphics Lab Motion Capture Database; free for research and commercial use, not to be resold). The BVH files are fetched into `blender/cmugolf/` (not committed).
* The kit is white tennis kit: a polo and shorts (the girl's a skort) in one colour each; no hats, glasses, facial hair or other garments yet.
