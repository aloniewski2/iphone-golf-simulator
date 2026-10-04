# UniMate tennis tests on V5 — review only

## Deliverables

- `UniMate_V5_Tennis_Review.mp4`: five generated candidates, each shown at normal speed and half speed, from two simultaneous views. 1400 × 900, 30 fps, 30 seconds. Each native sample is 60 frames at 30 fps; slow motion duplicates frames rather than inventing new poses.
- `UniMate_V5_ContactSheet.jpg` and `01_ready_preview.png` through `05_volley_preview.png`: full-character stills.
- `HeroV5_UniMate_Review.blend`: review scene with the V5 default wardrobe, preview racket, and generated actions.
- `exports/`: five review-only animated FBX files. These contain the character and wardrobe; the procedural curve racket is a Blender preview prop and is not included in the FBX.
- `motions/`: untouched decoded model features and pose curves, with prompts and seeds.
- `alternate-prompts/`: separate shorter-prompt forehand/backhand experiments. These do not overwrite the primary samples.

## What actually ran

UniMate official `unimate_uniml3d_f60_v2` checkpoint at step 100000, EMA weights, repository commit `5d6aabe`. Frozen FLAN-T5-base text encoder. Classifier-free guidance 3.0, adaptive Dopri5 sampling with the upstream tolerances, 60 frames per sample. Inference ran on Apple MPS.

The local runner changes only torchdiffeq's solver bookkeeping dtype to float32 because MPS cannot execute float64 operations. It does not change model weights or replace motion with existing gameplay animations. This compatibility adjustment means the Mac samples are not guaranteed bit-identical to CUDA samples.

The condition uses V5's actual rest skeleton and proportions: 41 animated joints, excluding its static Root control. UniMate canonicalization/topology features and upstream rotation decoding are used, then bind-frame conjugation converts the output to Blender pose channels. Quaternion signs are made continuous for interpolation. No hand IK, foot locking, swing cleanup, or old tennis curves are applied.

Source: `ArtDir/hero/v5/Hero_01_V5.blend`

Source SHA256: `bcd2b1e0bef1d1ab3624ad4870a8bbcf6a05af8fd79178349f6e1f8dffc65226`

The preview copy keeps Body, eyes, Hair_Default, Hat_Visor, Shirt_Default, Shorts_Default and Shoes_Default. Alternate hair meshes are excluded from that copy. Approved source geometry, textures, weights and rig are not edited.

## Honest visual assessment

These are **model capability tests, not production tennis clips**. Labels describe the requested action, not a passed acceptance test.

| Requested motion | Seed | Observed result / remaining issue |
| --- | ---: | --- |
| Ready return stance | 730 | A crouched waiting pose with small motion; head is low and feet need planting. No seamless-loop claim. |
| Forehand | 731 | Weight shift and stepping, but insufficient full right-arm stroke. Fails readable forehand mechanics. |
| Two-hand backhand | 732 | Torso turn and lateral movement; left hand does not remain on the grip. Fails two-handed backhand. |
| Serve | 733 | Recognizable overhead arm moment, but stepping/kicking and follow-through are inconsistent. No reliable toss/contact timing. |
| Forehand volley | 734 | Forward movement and racket repositioning; off-hand overlaps the string bed in some poses. Needs a defined short punch and recovery. |

All samples decode to finite curves with normalized quaternions. This numerical check is not evidence of correct tennis mechanics. Foot slide/hover, hand grip, racket face orientation, and some rapid joint changes remain visible.

The blue racket is rigidly attached to Hand.R with one shared palm-relative transform. It follows the generated hand; it does not force a correct grip or correct swing. There is no ball/contact simulation. Stage lighting is for review, not a Unity look pass.

## Scope / implementation status

All preview assets and scripts are isolated under `ArtDir/review/unimate-tennis/`. No Unity import, PlayableGraph binding, controller change, animation-slot replacement, or game build was performed for this task. This is a five-motion evaluation set, not a complete gameplay pack with locomotion, dives, transitions and match reactions.

Before considering gameplay integration, use reference/key-pose conditioning or constrained cleanup for load/contact/follow-through, enforce both-hand grip for backhand, lock planted feet, then evaluate the full clip and transitions. The current outputs should not replace the game's existing tennis motions.

## Reproduce

Run from this review folder:

```sh
/Users/adnanyonathan/Documents/Codex/Tools/UniMate/.venv/bin/python tools/generate.py
/Applications/Blender.app/Contents/MacOS/Blender -b -t 6 --python tools/render.py
/Users/adnanyonathan/Documents/Codex/Tools/UniMate/.venv/bin/python tools/package_review.py
```

`tools/export_skeleton.py` creates the read-only-source preview copy and rest-skeleton data. Generation retains existing `.npy` samples; rendering retains existing frame PNGs. Move a specific output aside if intentionally regenerating it. `prompts.json`, `generation-report.json` and `numeric-validation.json` retain detailed inputs and checks.

## Alternate prompt check

A second forehand sample (seed 1730, “A person swings a tennis racket with their right hand to hit a tennis ball.”) and a second backhand sample (seed 1731, “A person hits a tennis ball with a two handed backhand swing.”) were generated and rendered at frames 1/20/40/60. They remain separate. The sampled poses still do not establish a reliable full tennis stroke; the alternate backhand also puts the racket near the face and has unplanted feet. Neither is promoted into the primary montage. Those stills are diagnostic spot checks, not full-clip acceptance.

Verification: final MP4 fully decoded successfully: 900 frames, 30 fps, 30 seconds, H.264, 1400 × 900. The approved source Blender SHA256 was rechecked and is unchanged.
