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
3. **Retarget the golf swing.** The clips (Drive, IronSwing, HalfSwing, Chip, Putt, Idle, Wave, Cheer, FistPump) were made on the studio's 23-bone V4 rig,
   a short stylised golfer (hands at chest height, a hunched address). His rig is a realistic 1.7 m body, so a straight copy folded the arms and dropped the head
   between the shoulders. A golfer's swing is built instead:
   * **The club and the hands** are V4's club path and shaft angle, at a **real club's length** (driver 1.14 m, iron 0.97, wedge 0.92, putter 0.86; the grip end
     follows from the head, which stays on V4's path, so the ball is where it always was) with the hands the same distances down the grip. The fingers close round
     the handle (his finger bones curl about their own local X).
   * **The back** is hinged forward from the hips (12-38 degrees, whatever brings his shoulders over the hands at address) and keeps that angle from address to finish,
     coming up only where the arms could not reach. The pelvis sits back and down, the feet a little way behind V4's: knees flexed, the weight on the heels.
   * **The trunk, neck and head** take the V4 bones' rotation change (the shoulder turn is -77 degrees and the hips -37 at the top, +104 and +92 at the finish: a real
     swing's), the hips travelling as V4's did scaled by the height ratio (1.166 the boy, 1.171 the girl).
   * **Each arm** is two-bone IK to the hand on the club, the elbow bending where V4's did. In the backswing, at the top and in the follow-through (not at address
     or through the ball) the lead hand reaches from his shoulder as far as V4's did from its own scaled by the arm, so the lead arm is long where V4's short
     arms folded; the club goes with it. A putt and a chip keep V4's hands (a triangle of arms).
   * **Wrists and elbows are kept out of the torso** (an ellipse round the spine: V4's thinner body let the lead arm cross the chest, which on his body put the forearm
     through the polo): a wrist inside it is pushed out and the club, and the trail hand, go with it; an elbow inside it is turned about the shoulder-wrist line.
   * A **free arm** (Wave, Cheer, FistPump) reaches as V4's did from ITS shoulder, scaled by the arm.
   * **V4's half swing starts with the club a yard in the air** (not at the ball) and V4 labels its impact eight frames before the club gets there. So the half swing is
     stitched onto the iron's address and takeaway (a short blend), and its impact is found where the club head is back at the ball.
   His own clips (Walk, RunForward, Emote_Scuba/Spike/Thrust, Intro_BringIt/Pushups/Wave) are copied onto the rig unchanged (`BORROWED`): in place, 30 fps.
4. **The ball.** The club head is on V4's own path, so at address it is where `GolferView.Stand` puts the ball (0.75 yd from the golfer) and at impact it passes
   it. The landmarks in the JSON (top, impact, where the head is) are measured on his clips.
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

`blender/scripts/matchhero_hair.py`, run by `matchhero_golf.py` against his own heads before they are reduced. A style is a cap (the head's surface above a hairline,
stood off it and rolled down to the skin; `Skull.cap`), locks laid along the skull (`ribbon`), balls for curls, a hanging skirt for long hair and a tube for a tail. One
set serves the boy and the girl (a few proportions follow the head), so any haircut is open to either:

| Name | What |
|---|---|
| Classic | his own hair (a side-swept quiff, the girl's a bob) **with a close scalp cap in the hair colour under it**: his hair left bare patches of scalp (the girl's crown, the boy's temple); now they show hair |
| Short | a close cap with a small quiff (the girl's has a side fringe and short sides) |
| Curly | a ball for each curl over a close cap |
| Long | the sides to the jaw and the back to the collar (the girl's to the shoulder blades) |
| Tail | a high ponytail (the boy's a short one) with a tie |

**No bald spots, no overlaps from any side.** The cap is cut along its hairline curve itself (edges that cross it are split there; cutting by whole triangles left a
saw-tooth hairline and ragged long-hair strips), the cut is laid on the true skull, and the hanging strips of Long hair are drawn straight along themselves, with a
bump where an ear stands out. It is checked three ways:

    Blender ... matchhero_golf.py -- --sex Male --hair-check                         rays from outside the head: any direction where the scalp above a style's hairline shows (target: none)
    Blender ... matchhero_golf.py -- --sex Male --assemble-only --hair-turn --preview <dir>     every style from 24 sides (every 45 degrees at three heights, from above, from low behind)
    PlayMode HeroGolferTests.EveryHaircutFromEverySide                               in the game: each cut on both bodies from 8 sides up close, and the long cuts on the whole golfer
                                                                                    (Unity/Library/Captures/hero/spin; `GolfGame.TurnPickerGolfer` holds the picker's golfer at an angle)

Two material roles: `Hair_M` / `Hair_F` (the hair colour) and `Hair_Dark` (a shade deeper, for the strands). A tile picture for each (`UI/Look/hair_<name>_m|_f.png`) comes from
`matchhero_golf.py --icons` and `matchhero_icons.py`. To add one: a function in `matchhero_hair.py` that returns one joined mesh, add it to `BUILDERS`, its name at the END of
`HeroGolfer.HaircutNames`, the count in `CharacterLook.Haircuts`, and run the export and the icons.

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
* Not hand-animated: the swing is V4's timing and rotations on a better skeleton, posed by rules; a real golfer's weight shift and the club's lag are not authored.
* The kit is white tennis kit: a polo and shorts (the girl's a skort) in one colour each; no hats, glasses, facial hair or other garments yet.
