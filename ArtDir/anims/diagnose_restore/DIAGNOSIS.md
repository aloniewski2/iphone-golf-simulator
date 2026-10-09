# Animator_DiagnoseAndRestore — findings (2026-09-26)

Read-only diagnosis. No clips, prefabs, meshes or code were changed.
Evidence: `motion.json` (per-frame motion, all 30 Hero clips), `runs.json` (per-bone motion),
`latest_montage_every10f.jpg`, `runs_compare_{RunForward,RunRight,RunLeft}.jpg`.

## Where the current clips come from
Seven prefab generations, each rebinding slots in `ModularHeroLook` (one PlayableGraph, no Animator Controller):

| Slot | Current (Hero_01_FullStroke) | Actual motion origin |
|---|---|---|
| Ready / Serve / Forehand / Smash | FullStroke/Hero_Grip_*_v1 | Procedural `TennisActor` + `standard_male_tennis.fbx`, then right arm re-solved |
| Backhand | FullStroke/Hero_Backhand_v4 | Hand-keyed from USTA photos (4th rewrite) |
| Volley | FullStroke/Hero_Volley_v3 | Hand-keyed (3rd rewrite) |
| RunForward | AnimRepair/Hero_Repair_RunForward_v1 | Procedural gait. Left arm edited twice, right arm edited once |
| RunRight | KneesOffArm/Hero_Gameplay_RunRight_v1 | Procedural gait. Left arm edited once |
| RunLeft | BackhandRun/Hero_Gameplay_RunLeft_v1 | Procedural gait. Left arm edited twice |

**Eyes Japan mocap has not been in the shipping path since Owned v1 was rejected.** Owned v1 used a custom
C3D marker solve because Blender 5.2 couldn't read the FBXs. It also used the **one-handed** "backhand hardhit"
take (tennis-11), not the two-handed "backhand double hardhit" (tennis-12) that's on disk.

## Root causes
1. **Frozen montage:** every stroke is a 120-frame (4 s) bake. The action takes about 1 s (frames ~10–40) and
   the remaining ~2.7 s is Ready idle. 8 of 12 evenly spaced samples are Ready.
2. **Volley is actually frozen:** Hero_Volley_v3 moves a total of 40°/84°/72° (upper arm/forearm/hand), 0° on the left arm and
   ~10° on the legs over the whole clip. That's less than the Ready idle's breathing. Frames 70–120 are exactly static.
3. **Implanted grip:** the rig has 22 bones and **no finger bones**. The "grip" swaps the entire body mesh for a
   blendshape copy (`Hero_01_GripBody`) that deforms the fixed mitt, with the socket rotation hard-coded in C#
   (`ModularHeroLook.cs:40-41`, ×100 unit hack). Close-up: the handle crosses the fist and its butt comes out behind the
   wrist.
4. **Edge-sword racket:** the "abs dot = 1.0000" proof (`HeroFullStrokeReview.cs:79`) samples one authored frame per
   stroke. That frame was solved to face the net, and nothing constrains the face during the rest of the swing.
   The source motion is procedural arm sweeps with no forearm/wrist roll.
5. **No unit turn / stiff body:** Spine, Neck, Head and both Shoulder bones have **0° motion in every run** and very little in
   strokes (for example, the forehand's Chest moves 60° in total, compared with 140° in the Eyes forehand).
6. **Runs regressed:** the legs and hips are identical in all run versions. Only the arms were edited: the off-arm pass cut RunRight's
   left-arm swing by 60% (UpperArm.L 1596°→649°), the BackhandRun pass halved it again on RunLeft, and AnimRepair rewrote
   RunForward's right arm (the dangling racket).

## Restorable
- Original untouched run bakes: `Models/Hero_Gameplay_Run{Forward,Right,Left}_v1.fbx` (GameplayFixed, set A).
- Eyes source: `anims/source/mocapdata-tennis/tennis.zip` includes forehand, 2H backhand hardhit, first service,
  forehand volley and forehand smash (FBX + C3D). Raw/clean retargets are in `anims/eyes_raw_retarget`, `anims/eyes_clean`.
