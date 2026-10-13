# Swing fix — 2026-10-12

These are the golf swing animation fixes from the comparison in `work/swing-compare` (the tour references and the before film). The scope is animation only: no clothes, rig, meshes, maps or cameras.

**Installed** in the working tree (uncommitted on `build-10/12`):

| file | change |
|---|---|
| `blender/scripts/golf_swing.py` | The swing source. The lead arm is straight at address (`ARMS_WANT`, `ARM_WEIGHT`, `ADDRESS_LEAN`, `ADDRESS_MISS_WEIGHT`) and is held long through the backswing, the downswing and the release (`straight` curve, `LEAD_STRAIGHT`). The downswing holds the lag: new P5/P6 keys in `FS`. The hips and chest open earlier and tilt more, so the trail arm reaches without diving (`hip_p6`, `hip_imp`, `imp_turn`, `imp_side`, impact-zone `slope`/`span`). The backswing sways less. Per-club values: `FS_CLUB`, `HS`, `CHIP_TOP`, `CHIP_LEAN`. Debug prints: `DEBUG_MISS`. |
| `Unity/Assets/Resources/Hero/golfer_m.fbx`, `golfer_f.fbx` | Only the Drive, IronSwing, HalfSwing and Chip takes are re-authored (`scripts/splice_swings.py` on the shipped FBX, with matchhero_golf.py's export settings). Everything else is identical (gate 2). |
| `Unity/Assets/Resources/Hero/golfer_m_clips.json`, `golfer_f_clips.json` | `headAddress` / `headImpact` for those takes. Frames, top and impact are unchanged. |
| `Unity/Assets/Scripts/Game/GolferView.cs` | Tempo: the full-swing downswing goes from 0.38 to 0.25 s, the HalfSwing from 0.34 to 0.26 s (`scripts/tempo_patch.py`). The driver's follow-through wrap now fades out with the finish pose during a crossfade (`scripts/clearance_patch.py`). Only those lines changed; another session's footwork and mood edits in this file are untouched. |
| `Unity/Assets/Scripts/Game/GolfSwingClearance.cs` | The `driveReleaseShaft` constants are re-measured for the new Drive (`scripts/release_shaft.py`). `ApplyDriveFollowThrough` gains a `weight` parameter. |

**Fixed along the way** (pre-existing, also present with the original files): the club jumped 1.18 m on the first frame of `Settle()` after a drive. That made `SwingSmoothnessTests.AFullDriveIsFluid` fail on the original assets too (`logs/tests_baseline.log`). The cause: the follow-through wrap was applied with the new clip's time (address), not the outgoing finish pose's.

## Results (in-game, `frames/` + `scripts/measure.py`; before = `work/swing-compare/frames`)

| male driver | before | after | tour (ref) |
|---|---|---|---|
| downswing, top → ball | 0.38 s | **0.25 s** | 0.19–0.23 s (7 pros measured) |
| impact → clip end | 1.02 s | **0.65 s** | ~0.35 s to the finish pose |
| lead elbow, address / top / impact | 68° / 38° / 93° | **33° / 15° / 20°** | straight-ish (~10–20°) |
| wrist hinge at P5 (lead arm parallel, downswing) | 25° | **81°** | ~90° |
| spine forward bend at impact (address 25°) | 43° | **26°** | ≈ address |
| head drop at impact | 14.5 cm | **7.5 cm** | a few cm |
| head toward the ball at impact | 13.3 cm | **−1.3 cm** | ~0 |
| club head speed at the ball (SwingSmoothnessTests) | ~37 m/s | **47 m/s** | |
| trail knee flex at impact | 62° | 60° (**not fixed**) | ~35–45° |

The female driver in-game:
- lead elbow 66/57/87° → 45/24/27°;
- head drop 13.8 → 4.1 cm;
- head toward the ball 12.3 → −4.3 cm;
- trail knee 62 → 55°.

The iron, HalfSwing and Chip (both sexes) are measured in Blender: `logs/baseline_*.txt` vs `logs/new_*.txt`. They improve on the same lines; the details are in GATE_RESULTS.md.

## Proof
- `sheets/01_faceon_before_after_scott.jpg`: before / after / Adam Scott, 8 phases.
- `sheets/02_behind_before_after_pros.jpg`: broadcast and down-the-line views, before / after / Åberg, Kim, Homa.
- `sheets/03_after_all_angles.jpg`: the after swing from 8 cameras.
- `sheets/04_other_takes.jpg`: female driver, iron, wedge.
- `film/05_faceon_before_after_scott.mp4`, `film/06_behind_before_after.mp4`: synced at impact, 1x then 0.25x.
- The reference frames are third-party footage: keep them local and don't commit them.

## Rollback
Copy back from `rollback/`:
- `golfer_m.fbx`, `golfer_f.fbx` and their `_clips.json` → `Unity/Assets/Resources/Hero/`;
- `golf_swing.py` → `blender/scripts/`;
- `GolfSwingClearance.cs` → `Unity/Assets/Scripts/Game/`.

For `GolferView.cs`, revert only the two blocks shown by `diff rollback/GolferView.at-install.cs Unity/Assets/Scripts/Game/GolferView.cs`. Another session's edits are in that file, so do not copy the whole file back.

## Rebuild / iterate
```
scripts/run.sh Male Drive [--verify]                    # measure the takes golf_swing.py (gs_new/) authors on the shipped FBX, ~5 s/clip
Blender -b --factory-startup --python scripts/splice_swings.py -- --sex Male --in <golfer_m.fbx> --gs <dir> --only Drive,IronSwing,HalfSwing,Chip --out out
Blender -b --factory-startup --python scripts/dump_fbx.py -- <fbx> <npz>     # before/after data comparison (gate 2)
../swing-compare/scripts/film.sh <log> SF_OUT=$PWD/frames SF_SWINGS=male:Driver,...   # in-game film in the APFS clone
python3 scripts/compose_fix.py
```

The splice tool reproduces the shipped takes from the matching `golf_swing.py` to within 0.08 mm (`--verify`). Run it against the installed FBX with the installed `golf_swing.py` before changing anything.
