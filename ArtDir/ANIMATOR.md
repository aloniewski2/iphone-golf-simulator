# Current phase: TennisComplete_CustomizeSafe (characters) — review candidate

See [tennis_complete/NOTES.md](anims/tennis_complete/NOTES.md). New party clips, Hero01 wired into live gameplay (player + rival), cosmetics pack v1. **STOP: await Adnan approval.**

---

# Current phase: FixInvertedHand_ArmBodyClip — V6 review candidate

See [fix_hand_clip/NOTES.md](anims/fix_hand_clip/NOTES.md). Left hand inversion root-caused (sign-unsafe flip test + mirrored left grip) and fixed; arm-vs-torso clearance solver on both arms incl. runs (left arm only). **STOP: await Adnan approval.**

---

# Current phase: Animator_RestoreMotion_GripFace_BHVolley — V5 review candidate

See [restore_motion/NOTES.md](anims/restore_motion/NOTES.md). Eyes mocap retarget with tracked-racket hand solve; 20 finger bones + fitted grip; GameplayFixed runs restored. **STOP: await Adnan approval.**

---

# BackhandFull_VolleySwing_GripFace — V4 review candidate

## Status: INCOMPLETE — visual grip acceptance failed

The phase is **not approved / not complete**. The right-hand close-up still shows an open, distorted finger curl instead of a convincing hold. Keep this as a review candidate, not a finished animation pack.

| Requirement | Result |
|---|---|
| Runs unchanged | PASS: original assets and exact pose samples retained; original body/socket used during all runs. |
| Nine slots visibly animate | PASS: actual GameTime PlayStroke checks plus full-frame review. |
| Full two-hand backhand phases | Authored: load, drop, left contact, high right-side finish, hip/shoulder uncoil and step. |
| Both hand targets stay stacked | PASS for cradle-point constraints; this does **not** certify the visible finger hold. |
| Contact string-bed orientation | PASS: all five measured swing contacts have absolute dot(net, face normal) = 1.0000. |
| Backhand face / torso clearance | Proxy checks pass; latest full take supplies visual evidence. |
| Volley prep / punch / recovery | Authored and playing; combined acceptance remains blocked by grip. |
| Fingers visibly holding / no implanted fist | **FAIL**: right finger pose is visibly distorted/open in `Grip_closeup.png`. |
| Overall C + D + grip/face acceptance | **FAIL** until the grip is repaired. |

**Next proposed repair, requiring Adnan's scope approval:** hand-only topology and finger-rig work. The current 22-bone skeleton has no finger bones; the pose-only hand deformations attempted here have not met the visual standard. The user's explicit no-mesh-reopen restriction prevents proceeding with the proposed topology work. Keep body, wardrobe, V4 materials/lights, and all Run clips locked.

## Changes

- New `Hero_Backhand_v4`: four-second two-hand stroke. Unit turn to 1.0 s, head drop to 1.4 s, hip-led forward swing/contact at 1.7 s, high right-side finish at 2.7 s, hold to 3.2 s, recovery to Ready. Pelvis keys span −57° → +40°; chest −82° → +65°. Right-foot step and forward weight transfer accompany the uncoil. Both hand cradle points are constrained to the handle throughout; this is not a mirrored forehand.
- New `Hero_Volley_v3`: small unit turn/prep to 0.7 s, forward punch/contact at 1.2 s, short extension to 1.5 s, recover by 2.4 s. Contact face is squared to the net.
- Right-arm-only grip/contact-face pass on existing JumpServe, Forehand, Smash. All hip, spine, head, leg, and left-arm pose samples in those three clips are exact copies of the previous gameplay set. Ready retains its lower body.
- Frame-to-frame elbow and hand orientation costs prevent the initial coupled solve from switching elbow poles during recovery. No OWNED v1 body motion is used.

## Holding grip and shared socket

The old socket pointed through palm thickness. The new handle sits diagonally across the lower hand’s fingers, toward the thumb side; the upper hand uses its own curl angle. `Grip_R` / `Grip_L` blendshapes curl existing finger geometry using pose-only deformation of the existing topology. No new topology, finger bones, body proportions, wardrobe, material, or lighting changes. The base unposed body is retained for runs.

- Parent: `Hand.R`, same fixed attachment for Ready and every swing.
- Local position in imported bone coordinates: **(0.000600, 0.0005302886, 0.00002500)**. Imported bones have ×100 scale; equivalent hand-local metres: **(0.060, 0.05302886, 0.002500)**.
- Local quaternion XYZW: **(−0.25000000, 0.25000000, −0.06698730, 0.93301270)**; equivalent Unity Euler **(334.3411°, 33.6901°, 343.8979°)**.
- Racket-local right cradle Y = **0.045 m**; left cradle Y = **0.135 m**. Left remains above right. Hand-local cradle is (0.060, 0.092, −0.020) m.
- Existing racket hoop/strings are retained. A dark grip wrap extends along the upper handle to Y=0.205 m for the second hand; neither hand targets strings/frame.
- `ModularHeroLook.ApplyHoldingGrip` applies the same pose/socket during actual `PlayStroke`, Ready, and deterministic review. Right grip is active on Ready/swings; left grip only on Backhand. Runs restore the original mesh, original socket, and disable the wrap.

## Runs — untouched

RunForward still binds `Models/AnimRepair/Hero_Repair_RunForward_v1.fbx`; RunRight uses `Models/KneesOffArm/Hero_Gameplay_RunRight_v1.fbx`; RunLeft uses `Models/BackhandRun/Hero_Gameplay_RunLeft_v1.fbx`. No new run FBXs. Full JSON pose equality is asserted, and all 360 rendered run frames are compared to the previous montage. See `scope_verification.json` for exact pixel results; sparse shadow/edge raster differences are reported rather than presented as bitwise identity.

## Assets / wiring

- Review scene: `Unity/Assets/Scenes/Hero01FullStroke.unity`.
- Review prefab: `Unity/Assets/ArtDirection/Hero01/Prefabs/Hero_01_FullStroke.prefab`.
- Six swing/Ready FBXs and the grip shape mesh: `Unity/Assets/ArtDirection/Hero01/Models/FullStroke/`.
- Authoring: `Hero_FullStroke_v4.blend` (skeletal curves) + `Hero_GripPose.blend` (hand blendshapes), with reproducible tools alongside.
- Same Humanoid Avatar and existing nine-input PlayableGraph. Importer change only recognizes the two new clip version names and calculates normals for this one grip blendshape mesh. `GolferModelImporter` is untouched.

## Reference actually used

Hand-keyed from the USTA photographed stroke sequence and grip guidance; no claim that a Djokovic/Sinner video was watched or retargeted.

- [USTA School Team Tennis Manual](https://www.usta.com/es/content/dam/usta/sections/southern/pdf/net-generation-high-school-team-tennis-manual.pdf), printed pages 90 (two-hand backhand) and 92 (volley): unit turn, high-to-low racket preparation, extension through contact; compact volley preparation and forward path. The photographed backhand is left-handed; this new pose sequence is authored for the right-handed hero.
- [USTA Tennis 101: Perfecting the Backhand](https://www.usta.com/en/home/improve/tips-and-instruction/national/learning-the-basics--backhand.html): dominant hand lower, non-dominant above; contact left/out-front for a right-handed player and deliberate face control.

## Review outputs

- `V4_fullstroke_montage.mp4` — same nine-clip order. Backhand/volley play as full takes; existing brief serve/forehand/smash action windows repeat at 1× and are labeled as repeats.
- `V4_fullstroke_full_takes.mp4` — all 1,080 frames, uncut, 30 fps.
- `Backhand_LOAD_side.png`, `Backhand_LOAD.png`, `Backhand_DROP.png`, `Backhand_CONTACT.png`, `Backhand_FOLLOW.png`.
- `Volley_PREP.png`, `Volley_CONTACT.png`, `Grip_closeup.png`, `fullstroke_contact_sheet.png`.
- Runtime evidence: `ArtDir/screenshots/fullstroke_grip_playmode/{slot_bindings,runtime_slot_checks,contact_faces,verification}.txt`.

## Review limits

This remains a stylized short-limbed V4 hero with its existing hand topology and shoulder skinning. Grip correction is a pose, not a new sculpt. Numerical cradle lock does not by itself certify every skin triangle against the handle; review the supplied close-up and full take for visual acceptance. This candidate is not described as approved. Stop here for Adnan; no new sports, mesh/look phase, or phone deployment.


---

# Previous phase history (superseded where noted above)

# Current phase: AnimRepair_Backhand2H_RestoreSlots

## Checklist result

| Item | Result / evidence |
|---|---|
| Slot wiring / no frozen swing labels | PASS — nine distinct clips; real GameTime `PlayStroke` checks pass for every action; all 1,080 frames sampled. |
| Ready | PASS — neutral wrist max 7.2°; elbow off ribs, minimum surface proxy gap 7.6 cm; feet unchanged. |
| Two-handed Backhand v3 | PASS — both palms constrained to handle for every frame, left palm 8.5 cm above right; zero measured grip drift. |
| Hip coil / uncoil | PASS — pelvis −34° load → +27° follow; 61° total range plus step-in. |
| Left-side contact / face clearance | PASS — hoop X −0.505 m at frame 48; lowered follow-through; minimum head-sphere proxy gap 22.5 cm. |
| Forehand volley | PASS — own front-block clip, 16 cm punch, short return, wrist max 6.6°, hips face net. |
| Run Forward racket arm | PASS — bent elbow 94–139°, controlled opposite pump, wrist max 8.8°. |
| Run Right / Run Left | UNTOUCHED — same original assets/GUIDs and pose data. |

## Slot findings

The previous bindings were distinct and non-null; no null/Ready alias was found. Short actions were buried in long Ready tails. The candidate explicitly reuses the known-good Serve/Forehand/Smash assets and replaces Ready/Backhand/Volley/RunForward. Missing review slots now throw rather than silently sampling nothing. Added a narrow Humanoid importer allowance for `Hero_Backhand_v3`.

The primary montage repeats short active windows at 1× with visible REPEAT labels. An untrimmed 36-second full-take video is also supplied. Both come from the same verified Play Mode capture.

## Source and outputs

Backhand v3 is hand-keyed with coupled grip constraints using Eyes Japan Backhand Hard Hit timing reference. Old Backhand NEW curves are ignored; no Forehand mirror or Owned v1 body/arm swap.

- [Detailed changes, source and limits](anims/anim_repair/NOTES.md)
- `anims/anim_repair/V4_anim_repair_montage.mp4`
- `anims/anim_repair/V4_anim_repair_full_takes.mp4`
- `anims/anim_repair/Backhand_LOAD.png`, `Backhand_CONTACT.png`, `Backhand_FOLLOW.png`
- `anims/anim_repair/Volley_CONTACT.png`, `RunForward_SIDE.png`
- `anims/anim_repair/Hero_AnimRepair_v3.blend` and repaired FBXs
- Scene: `Assets/Scenes/Hero01AnimRepair.unity`
- Prefab: `Assets/ArtDirection/Hero01/Prefabs/Hero_01_AnimRepair.prefab`
- Evidence: `screenshots/anim_repair_playmode/{slot_bindings.txt,runtime_slot_checks.txt,verification.txt,backhand_contact.txt}` and `anims/anim_repair/{acceptance_metrics.json,scope_verification.json}`.

No mesh/look changes. Fixed toy fingers remain; palm-to-handle constraints do not add finger articulation. Clearance tests use geometric proxies plus rendered review. **STOP: await Adnan approval.**

---

# Previous phase: NewBackhand_FixRunArm

- **New `Hero_Backhand_v1`:** hand-keyed in Blender using Eyes Japan Backhand Hard Hit timing (option 3). Left-side windup and contact, right-foot step-in, hip/shoulder uncoil and follow-through across the body. No forehand mirroring or Owned v1 curves.
- **Contact:** authored frame 33 / 1.10 s. Unity measures wrist X **−0.200 m**, racket center X **−0.581 m**: both on the character's left side.
- **Run Forward:** restored pre-clearance gait rhythm with a ±27° left-arm pump and a forward elbow hinge. Removed the previous clearance solver's rigid/searching arm path.
- **Run Left:** softer ±18° pump. Both runs use 78–94° elbow flexion and neutral wrists; only the four left-arm bones changed.
- **Preserved:** Ready, Serve, Forehand, Run Right, Volley and Smash reuse the exact preceding clip assets. Current knee weights, V4 look, wardrobe, right-hand socket and PlayableGraph API are retained.
- **Mapping:** `ModularHeroLook.backhand` on `Hero_01_BackhandRun.prefab` points only to the new `Models/BackhandRun/Hero_Backhand_v1.fbx` (GUID `59b35041f46cc4ab8b1a14270a27e408`). Previous backhand is no longer mapped in this candidate.

## Full Play Mode check

All 1,080 frames captured. Humanoid avatar, shared wardrobe and fixed socket checks pass throughout; socket drift remains 0 m / 0°. Run Forward left-elbow interior angle is 86.9–102.6°, hand fore/aft travel relative to shoulder 30.7 cm; minimum forearm/torso proxy gap 3.9 cm. Run Left elbow is 86.7–102.5°, minimum proxy gap 3.2 cm. These are proxy clearance measurements supplemented by rendered frame review.

## Outputs / detail

- `anims/backhand_run/V4_backhand_run_full.mp4`
- `anims/backhand_run/V4_backhand_run_before_after.mp4`
- `anims/backhand_run/V4_backhand_run_contact_sheet.png`
- `anims/backhand_run/Hero_Backhand_Run_v1.blend` + three FBXs
- Scene: `Assets/Scenes/Hero01BackhandRun.unity`
- [Source, key timing, run edits and limits](anims/backhand_run/NOTES.md)
- Evidence: `anims/backhand_run/scope_verification.json`, `author_report.json`, `screenshots/backhand_run_playmode/verification.txt`, `backhand_contact.txt`.

**STOP: await Adnan approval.**

---

# Previous phase: FixKneesAndOffArm — V4 gameplay review

## What changed

- **Knee weights:** 1,814 left / 1,773 right vertices reweighted with a consistent 0.31–0.57 m bind-height falloff across visible and underlying skin. Existing knee topology was sufficient; no new vertices, body reshaping, UV or material changes.
- **Knee bend plane:** corrected toward foot heading, bounded to 28° / 80% so airborne poses keep their original intent. Hip and ankle targets remain from the existing gameplay set. Local leg normals refreshed.
- **Off arm:** edited `Shoulder.L`, `UpperArm.L`, `LowerArm.L`, `Hand.L` on all nine clips, including their shared ready/return poses. Clavicle turns 12° forward; wrist targets and elbow poles keep the arm in front of the torso. Added temporal smoothing, with the clearer original correction retained for backhand.
- **Right arm/racket:** the protected right-arm, torso and head pose components remain exactly identical across all 1,080 authored frames. Same fixed palm socket and local T/R from FixGameplayArmRacket.
- **Skinning:** standard Unity four-influence skinning retained. `SkinQuality` is not a DQ switch. Blender Preserve Volume is not exported as a Unity skinning mode; no DQ shader framework added. [Unity Skin Quality documentation](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/skinnedmeshrenderer/quality).

## Review outputs

- `anims/knees_offarm/V4_knees_offarm_before_after.mp4` — synchronized previous fixed-right-arm set versus this pass.
- `anims/knees_offarm/V4_knees_offarm_full.mp4` — full-speed, full 120-frame excerpts of all nine clips, 36 seconds total.
- `anims/knees_offarm/V4_knees_offarm_contact_sheet.png`.
- `anims/knees_offarm/Hero_01_KneeBody.fbx` and `Hero_Gameplay_*_v1.fbx` — updated weights and Humanoid clips.
- `anims/knees_offarm/Hero_01_Knees_QA.blend` — weighted bind; `Hero_Gameplay_Fixed_v1.blend` — all baked actions.
- Unity: `Assets/Scenes/Hero01KneesOffArm.unity`, `Assets/ArtDirection/Hero01/Prefabs/Hero_01_KneesOffArm.prefab`.
- Authoring details and known limits: `anims/knees_offarm/PHASE_NOTES.md`.
- Measurements: `anims/knees_offarm/fix_report.json`, `protected_curve_check.json`, `bake_verification.json`, `screenshots/knees_offarm_playmode/verification.txt`.

## Final Unity verification

All nine clips sampled for 120 frames each. Valid Humanoid avatar, shared wardrobe and fixed racket socket passed throughout; socket translation/rotation drift was zero.

| Clip | Forearm proxy gap, minimum | Hand proxy gap, minimum |
|---|---:|---:|
| Ready | 5.3 cm | 9.3 cm |
| JumpServe | 3.5 cm | 9.3 cm |
| Forehand | 2.5 cm | 9.3 cm |
| Backhand | 2.4 cm | 9.3 cm |
| RunForward | 3.8 cm | 9.3 cm |
| RunRight | 5.2 cm | 9.2 cm |
| RunLeft | 3.3 cm | 9.4 cm |
| Volley | 5.3 cm | 9.3 cm |
| Smash | 0.9 cm | 6.5 cm |

Proxy radii: torso 17 cm, forearm 5 cm / hand 5.5 cm. The tightest remaining clearance is the smash forearm at 0.9 cm; the hand remains 6.5 cm clear there. This is a review limitation, not an exact mesh collision certificate. Knee interior ranges include 94–171° for jump serve and ~95–173° for running.

## Scope / approval

Priorities 1–2 are the review deliverable. Backhand receives knee/off-arm clearance fixes but **its choreography is not rewritten**; the existing PlayableGraph slot is retained. Knees stay soft toy joints rather than sculpted anatomical patellae. Clearance measurements use torso/arm capsule proxies and visual frame review, not an exact mesh collision guarantee. Original fast body transitions remain.

No Owned v1 source; no right-arm rewrite; no character look pass; no live campaign replacement or phone deployment. Earlier artifacts remain available.

**STOP: await Adnan approval.**

---

# Previous phase: FixGameplayArmRacket — existing gameplay selected

**Source selected by Adnan:** the existing `TennisActor` gameplay set (video 1). **Owned Tennis v1 is rejected and archived**, and supplies none of the body or arm curves in this set.

## Outputs

- `anims/gameplay_fixed/Hero_Gameplay_Fixed_v1.blend` and nine `Hero_Gameplay_*_v1.fbx` files.
- `anims/gameplay_fixed/V4_gameplay_fixed_full.mp4`: full 120-frame playback for every motion, 30 fps / 36 s total.
- `anims/gameplay_fixed/V4_gameplay_before_after.mp4`: synchronized old transfer / corrected transfer, same motion timing.
- `anims/gameplay_fixed/V4_gameplay_fixed_contact_sheet.png` and individual stills.
- Unity prefab: `Assets/ArtDirection/Hero01/Prefabs/Hero_01_GameplayFixed.prefab`.
- Unity scene: `Assets/Scenes/Hero01GameplayFixed.unity`.

## 1. Baseline / what was broken

The old standard-character FBX is Generic, with procedural arm/foot layers in `TennisActor`; V4 is Humanoid with different forearm/hand rest axes. Its prefab transforms are not a reliable substitute for skin bind matrices. The earlier transfer retained incompatible roll, and then assigned the racket a separate **world** rotation. That combination could twist the forearm while the prop rotated independently of the palm.

The corrected bake uses the existing gameplay actor's timing, torso/leg poses and wrist trajectory. `V4GameplayArmRacket` rebuilds the right-arm frame from V4 bind axes and the existing elbow plane. This avoids carrying the old forearm roll into V4. The correction is baked; it is not a mesh or skin-weight change.

Rest-axis comparison, Euler XYZ values expressed with Unity's Z-X-Y Euler convention (degrees; diagnostic source-bind → V4-bind difference):

| Bone | Rest-axis difference |
|---|---|
| RightLowerArm / LowerArm.R | +6.725, +15.419, −14.284 |
| RightHand / Hand.R | +6.800, +2.859, −1.870 |
| V4 neutral Hand.R relative to LowerArm.R | −0.935, −20.411, +19.835 |

These are bind-axis differences, **not** three constant Euler keys pasted onto every swing. Applied correction uses quaternion swing/twist decomposition: desired roll capped at ±55°, shared 25% to upper arm and 75% to forearm (forearm maximum ±41.25°). Wrist rotation from the neutral V4 wrist is limited to 28° (measured 28.04° with floating-point angular conversion).

## 2. One fixed racket socket

Parent: **Hand.R / Unity RightHand**. Handle origin seats at the centroid of strongly Hand.R-weighted skin vertices; it is no longer given a per-frame world-space racket rotation.

| Property | Exact setting |
|---|---|
| Local translation, imported bone units | **(−0.0000394226, +0.0008746348, −0.0002774258)** |
| Equivalent offset in metres, accounting for 100× bone scale | **(−0.00394226, +0.08746348, −0.02774258)** |
| Local Euler rotation | **(0°, 0°, −90°)** |
| Prop local scale | **(0.01, 0.01, 0.01)** to compensate for imported bone scale |

The same translation/rotation is stored in the prefab and used for all nine clips. No clip-specific socket, loose world-space prop or hidden racket. Fixed socket offset drift is checked against the initially recorded offset, not against an animated substitute.

## 3. Elbow and clearance

- Two-bone solve retains V4 upper/forearm lengths and uses the existing animated elbow plane during swings.
- Ready pole eases outward; ready elbow interior angle is **94.3–97.7°**.
- Added a **55° minimum elbow interior angle**: the first fixed draft still reached 29° in forehand recovery frames 29–31. The final bake prevents that fold while preserving the body curves.
- Ready wrist target relative to Chest: model right **+0.31 m**, forward **+0.29 m**, down **0.10 m**. This is the retargeted carry target; the old source carry did not fit V4's proportions.
- Run wrist clearance offset: model right **+4.5 cm**, forward **+7.5 cm**, keeping the existing run trajectory and arm cadence.
- During active strokes the carry correction fades out; recovery eases it back over ~0.25 s. The wrist and elbow corrections remain active to prevent inversion/collapse.
- Torso and thigh capsule checks sample 32 points on the hoop during every idle/run frame, alongside visual review. These are conservative proxy checks, not an exact mesh-to-mesh collision proof.

## 4. Re-bake, import and playback

Existing `standard_male_tennis.fbx` clips plus the existing `TennisActor.Advance/Pose` procedural layers are sampled at 30 fps. Fixed poses are converted to Blender coordinates and baked onto the unchanged V4 bind. Maximum bone-position round-trip conversion error is under **0.000001 m**.

The nine FBXs are imported as **Humanoid**, using the existing V4 bind Avatar through the scoped `HeroLookImporter`. `GolferModelImporter` is unchanged. The existing `ModularHeroLook` PlayableGraph was extended to expose running and smash alongside the four stroke inputs; no Animator Controller was introduced.

Clips: **Ready, JumpServe, Forehand, Backhand, RunForward, RunRight, RunLeft, Volley, Smash**. All retain their original gameplay sample timing. These 4-second review bakes include the initial ready lead-in; action trigger is sample 15 / 0.5 s. For a gameplay playhead, action sample time is 0.5 s + elapsed action time; do not interpret the lead-in as extra swing latency. Running bakes demonstrate the existing procedural gait, rather than replacing that live gait system with a new locomotion loop.

## 5. Verification

- **1,080 frames** sampled in the corrected bake and replayed from the reimported Humanoid FBXs with the default modular wardrobe.
- Root/Hips/Spine/Chest/leg rotations: **0° change** when applying the arm correction to the video-1 transfer.
- Jump-serve hip vertical range: **0.5185 m** before and after the Humanoid round trip. Smash: **0.2839 m**.
- Right elbow interior angle: Ready 94–98°; serve 58–154°; forehand **55–150°**; backhand 61–154°; runs 117–154°; volley 70–114°; smash 83–151°.
- Fixed socket, shared wardrobe bones and valid Humanoid Avatar checked throughout all nine clips.
- Full windup, hit, follow-through, return and running samples reviewed; montage includes the complete recorded clips, not only favorable first frames.
- Evidence: `anims/gameplay_fixed/elbow_verification.json`, `bake_verification.json`, `screenshots/gameplay_fixed/capture_report.txt`, `screenshots/gameplay_fixed_playmode/verification.txt`.

## Scope / approval

Original `TennisActor` hit simulation, stroke timings, ball contacts, scoring and movement code are unchanged. This delivers the **fixed V4 prefab and re-baked gameplay set** for review; it does not silently replace the live campaign actor or deploy a phone build. V4's fixed finger geometry remains as approved; no new finger rig, remesh, look polish or extra swing stylization.

Owned v1 is preserved at `anims/_archived/hero_owned_failed_stylize_v1/` with an explicit rejected status. It is not referenced by the fixed gameplay prefab.

**STOP: await Adnan approval before additional stylization or juice.**

---

# Earlier phase history

# Animator — source selection pending

Locked look: POLISH V4. User requested new tennis animations, preferably Mixamo; no baseball or other-sport substitutes.

## Mixamo live catalog check — 2026-09-26
Signed in with Hero_01_Body selected. “tennis”, “forehand”, and “tennis serve” returned no results. “backhand” returned an axe attack and axe pack; “racket” returned Jazz Dancing (Rockette Kick). No suitable tennis clips found. No substitute animation was installed.

## Actual tennis candidates
- WondAR Studios Tennis forehand shot: https://www.fab.com/listings/277d5d2e-adbf-4b85-b194-3c7ede0890e2 — FBX, 4 seconds, motion capture.
- WondAR Studios Tennis backhand shot: https://www.fab.com/listings/6f1917e5-dd51-46f7-b314-70a4fe2f8d4e — FBX, 3 seconds, motion capture.
- Ailive Tennis Animation Pack 1: https://www.fab.com/listings/125cd00a-7787-4e6e-a31f-a1d9944673ee — five FBX motions including ready, serve and backhand.

No purchase or licensed downloads acquired. Source selection/acquisition is required before new clips can be retargeted and visually verified.

## Implementation status
Existing V4 idle and wardrobe are retained. Unverified mixer draft saved under anims/animator_before/ModularHeroLook_motion_draft.cs.txt; active component restored to its pre-phase implementation. Legacy tennis FBX staging removed from Unity. No completed forehand/backhand integration, racket attachment, or animation review screenshots yet. No Animator Controller, importer rewrite, mesh/look changes, or multiplayer sync.

## OwnTheMocap v1 — 2026-09-26

Actual Eyes Japan tennis marker recordings now have separate raw-retarget, cleaned, and stylized V4 Humanoid stages. Final clips: Hero_ReadyIdle_v1, Hero_Forehand_v1, Hero_Backhand_v1, Hero_Serve_v1, Hero_Volley_v1. See `OWNED_MOCAP.md` for measured authoring changes, right-hand prop, source obligations and remaining grip limitations. Review scene: `Assets/Scenes/Hero01OwnedTennis.unity`; graph/API lives in the existing ModularHeroLook. Await motion approval; legacy live TennisActor replacement is not part of this capture.

## 2026-09-27 — 1.2× visual tempo trial

- `HeroTennisDriver.AnimationTempo` defaults to 1.2 (set to 1 for rollback).
- Retimes the existing PlayableGraph stroke sample around its authored contact timestamp, including the upper-body layer and contact substeps. At time-to-contact zero the clip sample is unchanged. Serve prep clamps at the trophy pose; follow-through and point reactions advance at 1.2×.
- Idle/Ready remain 1×; running cadence still follows ground speed. Ball physics, movement speed, gameplay swing windows and source clips are unchanged.
- Review: `ArtDir/anims/captures/AnimationTempo_100_vs_120_60fps.mp4`. Left 1×, right 1.2×; forehand, backhand, jump serve, volley, smash. Actual Unity renders at fixed 1/60 steps, 750 frames per side, encoded at 60 fps. This is a controlled stroke preview without a live ball, not a device performance benchmark or interpolated slow-motion footage.
- Capture test asserts all stroke contact timestamps stay anchored at both tempos. Look and wardrobe untouched. Not deployed to the phone.
