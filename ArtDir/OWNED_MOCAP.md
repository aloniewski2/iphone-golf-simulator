> **ARCHIVED / REJECTED:** Adnan selected existing gameplay motion. Artifacts moved to `anims/_archived/hero_owned_failed_stylize_v1/`. See current `ANIMATOR.md` for FixGameplayArmRacket.

# OwnTheMocap — Hero 01 v1

Locked target: **POLISH V4**, one shared 22-bone Humanoid and default modular wardrobe. No body, weights, wardrobe, material, light, importer-wide, gameplay-power, or sports-physics changes. This is an authored derivative tennis set, pending Adnan's motion approval.

## Deliverables

- `anims/eyes_raw_retarget/`: raw motion solve on V4; five FBXs and `Hero_eyes_raw_retarget.blend`.
- `anims/eyes_clean/`: mechanical cleanup; five FBXs and `Hero_eyes_clean.blend`.
- `anims/hero_owned/`: the five final FBXs, editable `Hero_Owned_v1.blend`, metrics, verification, and `Hero_Owned_v1_review.mp4`.
- `screenshots/unity_owned_{readyidle,forehand,backhand,serve,volley}.png` and `unity_owned_tennis_contact_sheet.png`.
- Unity prefab: `Assets/ArtDirection/Hero01/Prefabs/Hero_01_OwnedTennis.prefab`.
- Unity scene: `Assets/Scenes/Hero01OwnedTennis.unity` (V4 review lighting preserved).

## Clips and authored timing

| Runtime clip | Source excerpt frames at 120 Hz | Length | Authored contact time |
|---|---:|---:|---:|
| Hero_ReadyIdle_v1 | Receive 2339–2627 | 2.4 s loop | — |
| Hero_Forehand_v1 | Forehand 2231–2759 | 3.6 s | 1.08 s |
| Hero_Backhand_v1 | Backhand Hard Hit 880–1408 | 3.6 s | 1.08 s |
| Hero_Serve_v1 | First Service 1207–1735 | 3.6 s | 1.08 s |
| Hero_Volley_v1 | Forehand Volley 1648–2176 | 3.6 s | 1.08 s |

“Contact” is an authored pose/timing landmark. The capture has no tracked ball or racket; this phase does **not** certify collision timing against a live ball.

All clips are baked at 30 fps and interpolated by Unity. Runtime names contain no supplier branding. Source provenance remains below and beside the original capture.

## A — Retarget

The supplied FBXs use an old FBX format unsupported by Blender 5.2. The matching **actual tennis C3D recordings** were used instead: 120 Hz measured marker positions, interpolated gaps, source-to-hero coordinate alignment, bone-direction solve onto the unchanged V4 skeleton. No baseball clip or unrelated motion substitute.

Waist corners drive Hips; waist-to-shoulder center drives Spine/Chest; shoulder/elbow/wrist markers drive arms; knee/ankle markers drive legs; four head markers supply heading. Retarget retains the target's bone lengths. Raw passes preserve capture travel for comparison. Source marker labels and exact extraction are reproducible in `anims/own_mocap_tools/prepare_markers.py`.

Unity uses the existing explicit Humanoid mapping in **HeroLookImporter**, with `CopyFromOther` referencing the V4 bind Avatar. Its opt-in recognition was extended only to `Hero_*_v1` under `Assets/ArtDirection/Hero01/`. **GolferModelImporter is untouched.**

## B — Mechanical cleanup

- Removed all horizontal hip travel (Blender XY = Unity XZ). No retained lunge/root-motion exception. Vertical weight shift capped at ±5.5 cm.
- Five-sample marker smoothing; seven for the ready stance.
- Two-bone leg solve plants the short hero's ankles, allowing up to 3.5 cm source heel rise. Foot heading limited to ±17.2°; original rest foot pitch retained. This fixes the first draft's tilted shoe solve.
- Hip twist reduced to 65%, chest rotation to 78% of solved source; head heading reduced to 25% to keep the eyes readable.
- Firm wrist orientation follows the forearm with 12% of marker-driven wrist rotation. A knuckle marker is not treated as an unrestricted wrist joint axis.
- Backhand clasp uses a smooth proximity blend to a separated support-hand target. Final authored windup receives another clearance solve if wrist centers come within 10 cm; the source's later single-hand release is retained.
- Ready loop endpoint error is distributed through the loop.

## C — Ownership / stylization

These are curve edits in the saved Blender actions, not just renamed imports:

- Windup: target **+22% angular displacement** from the ready pose on upper arms, forearms, Chest and Spine, shaped by a windup envelope. Actual keyframe measurements are in `anims/hero_owned/verification.json`.
- Right upper-arm windup measured about **+20% forehand, +23% backhand, +20% serve, +21% volley** vs clean at the selected apex. Chest/Spine are about +20–23%. Backhand forearm is about +29%. Serve forearm is an exception: its large rotation follows the shorter quaternion arc and is not claimed as +22%; the serve's shoulder/torso provide the enlarged silhouette.
- Hit: 0.35 s of source approach compressed into **0.13 s** (63% shorter). Per-stroke pose landmark selected around the swing burst rather than blindly equating peak wrist speed with ball contact.
- Follow-through: ease into pose from 1.27–1.72 s, **hold 1.72–2.12 s**, then settle. Source excerpt 4.4 s becomes authored 3.6 s.
- Ready entry/exit blends baked into the action; stable head; no added VFX or racket-tip overshoot.

The percentage is a measured **rotation displacement**, not a claim that every wrist's Cartesian travel increased by precisely 22%. Bone lengths and hero proportions remain fixed.

### In-place evidence

Saved raw backhand hip travel is ~0.98 m laterally / 0.60 m in depth; serve ~1.33 / 0.81 m. Clean and owned hip horizontal ranges are **0 / 0 m**. Clean ankle horizontal range is at most ~1.2 cm; authored transition blends can reach ~2.5 cm. This is a compact in-place set, not a locomotion/root-motion pack.

## D — Unity hooks and racket

`ModularHeroLook` keeps the existing PlayableGraph and wardrobe assembler. It now has one idle input plus four stroke inputs, no Animator Controller:

```csharp
hero.PlayStroke(ModularHeroLook.Stroke.Forehand);
hero.PlayStroke(ModularHeroLook.Stroke.Backhand);
hero.PlayStroke(ModularHeroLook.Stroke.Serve);
hero.PlayStroke(ModularHeroLook.Stroke.Volley);
hero.ReturnToReady();
```

Runtime uses GameTime, 0.10 s blend-in, 0.16 s blend-out, automatic return to ready, `applyRootMotion=false`. Animation APIs are visual only; they do not apply power, reach, score or movement. The existing legacy `TennisActor` campaign model is not silently replaced by this review prefab; these are the V4 hooks for its integration.

Simple blue racket is parented to `Hand.R`, with a 5.5 cm grip offset and local Z −90° rotation. Attachment compensates for the imported skeleton's 100× unit scale. The racket is a combined reusable mesh with three material submeshes, rather than dozens of runtime primitive renderers. No collider/gameplay reach changes.

## Verification and remaining limits

- Unity batch Play Mode capture completed; all five clips import as Humanoid against the V4 Avatar.
- Real GameTime forehand trigger advances and returns to ready before deterministic screenshot sampling. Evidence: `screenshots/owned/playmode_checks.txt`.
- Default wardrobe bones remain on the same skeleton; finite skin bounds; racket remains beneath the right-hand bone. Four skin influences retained.
- Reviewed windup/contact/follow-through frames: shoulders remain attached; no exploded arms or wardrobe detachment. V4 look retained.
- Recording waits one editor tick after sampling before rendering, avoiding a stale skinned pose against a newly moved prop.
- V4 has **no finger, eye or facial animation bones**. The hand retains its authored open toy fingers; a tightly curled sporting grip / finger-level contact cannot be created with these clips alone. No mesh/look reopening was performed.
- No live ball-contact validation, mobile performance build, multiplayer synchronization, or new sports pack is claimed. Minor ankle movement during stylized blends and original sleeve creases remain visible.
- Forehand/backhand stills show follow-through for readability; serve shows the overhead phase. The full video shows all phases without hiding recovery.

## Source and attribution

Timing/performance source: **Eyes, JAPAN / MocapData, Yamaoka tennis recordings** — Forehand, Backhand Hard Hit, First Service, Forehand Volley, Receive.

Publisher: https://mocapdata.com/ ; source archive and provenance: `anims/source/mocapdata-tennis/SOURCE.md`. Terms: https://mocapdata.com/Terms_of_Use . Publisher text references CC Attribution 2.1 Japan and Attribution-ShareAlike 2.1 Japan. Retargeting, cleanup, renaming and stylization do not remove those source obligations; distribution terms remain to be clarified before release. “Owned” here identifies our authored game variants, not an exclusive-ownership or CC0 claim.

## Reproduce / versioning

`anims/own_mocap_tools/` contains extraction, Blender authoring, numerical verification and review packaging scripts. Stage `.blend` files preserve raw, clean and authored actions separately; prior runtime/importer sources are in `anims/own_mocap_before/`. The locked V4 bind/QA masters were read, not overwritten.

Use Blender to run `build_owned.py`, copy final `Hero_*_v1.fbx` into the scoped Unity Models directory, then run `GolfArcade.EditorTools.HeroOwnedReview.Run`. Run `verify_owned.py` in Blender and `package_review.py` with the local numpy/Pillow/imageio-ffmpeg environment.

**STOP: await Adnan's motion approval. No further sports or juice VFX.**
