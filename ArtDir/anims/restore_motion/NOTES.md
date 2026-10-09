# Animator_RestoreMotion_GripFace_BHVolley — V5 review candidate (2026-09-26)

**Status: awaiting Adnan's approval.** No further phase has been started.

## Restored vs re-authored
| Slot | Source | What happened |
|---|---|---|
| Run F / R / L | `Models/Hero_Gameplay_Run*_v1.fbx` (GameplayFixed originals) | **Restored.** These are the clip assets from before any off-arm, BackhandRun or AnimRepair edit, with no curve changes. The only change is a wrist-only carry offset at runtime (`ModularHeroLook.runCarry = 0.85`), which re-aims the racket forward instead of across the legs. |
| Ready | Eyes `tennis-17 receive` | New. The athletic window is chosen from the source: racket up in front, left hand locked on the throat. |
| Forehand | Eyes `tennis-03 forehand hardhit` | New retarget from the real mocap. |
| Backhand | Eyes `tennis-12 backhand **double** hardhit` | New retarget. It is a real two-handed take (earlier passes used the one-handed tennis-11 or hand-keyed motion). The left hand is locked onto the handle for 67 of 84 frames. |
| Serve | Eyes `tennis-15 first service` | New retarget. |
| Volley | Eyes `tennis-05 forehand volley` | New retarget: prep, then a short punch, then recovery (1.9 s). |
| Smash | Eyes `tennis-06 forehand smash` | New retarget. It was not in any earlier Eyes pass. |

## Why this fixes the old failures
- **Frozen clips:** each clip now runs from ready, through the stroke, to the settled finish (1.9–3.6 s). There is no 3-second Ready tail. Every slot passes the runtime `PlayStroke` motion check (0.47–1.26 m of hand travel).
- **Edge-sword racket:** the Eyes takes contain **tracked racket markers** (throat, hoop and tip, previously unused). The hero's right hand is solved from the real racket orientation through the grip socket. The forearm takes the roll and the wrist takes the rest. Earlier passes only aimed each arm bone and discarded forearm roll.
- **Contact:** contact is detected from the racket (highest point for serve and smash, fastest and squarest for the other strokes), not from wrist speed. A 0.5× slow-in over ±100 ms makes it readable. A face assist of up to 35° (party stylize, solved through the wrist) squares the strings over ±8 frames.
- **Implanted fist:** the rig gained **20 finger bones** (4 fingers × 2 plus a 2-bone thumb per hand). Only the existing Hand.L/R weights were redistributed. Mesh, proportions, wardrobe and materials are unchanged. The curl pose was fitted by measured coverage and clearance around the handle.

## Measured in Unity Play Mode (`verification.txt`)
|string normal · net| at contact, where 1 means square:
- Forehand **1.00**
- Backhand **1.00**
- Serve **0.98**
- Volley **0.92**
- Smash **1.00**

These match the Blender values frame for frame, so the Humanoid wrist limits do not clamp them. Racket head height at contact is 2.07 m for the serve and 2.03 m for the smash.

## Socket (shared, one attachment)
- Parent: `Hand.R`.
- `localPosition` (-0.0003000, 0.0010316, 0.0000879). These are bone units; ×100 gives hand-local metres.
- `localRotation` (-0.405581, -0.579226, 0.405578, 0.579231); Euler (0, 270, 70).
- In hand terms: the handle crosses the palm, tilted 20° toward the fingers. The head extends out of the thumb side, and the strings are parallel to the palm.
- **Per-stroke grip roll about the handle axis** (the handle stays in the same finger cradle; this is a real grip change): forehand eastern −34.3°, volley continental +57.2°, all others 0. Values are in Unity degrees in `strokeGripRoll`.
- The grip and socket are stored as directions in a geometric hand frame and rebuilt from bone positions (`grip_pose.json`), so Blender↔Unity axis handedness cannot flip them.

## Remaining debt (honest)
1. **Backhand left hand:** it sits stacked above the right hand (curled, never on the strings). In the close-up, the left fist rests on the right hand more than it visibly wraps its own section of the handle. The chibi mitts are about as wide as the grip.
2. **Serve wind-up** (about frames 32–58, trophy / racket drop): the wrist side-bend exceeds the Humanoid ±40° limit, so Unity clamps the racket angle there. Contact is unaffected.
3. **Forehand contact is low** (racket head about 0.46 m), because the source player was hitting low balls. The motion is authentic but less heroic than a waist-high hit.
4. **Volley prep** shows the racket fairly flat and open at the side before the punch.
5. **Eyes licence terms** (CC-BY vs CC-BY-SA 2.1 Japan) must be settled before shipping any derived clips.
6. The review prefab is still not wired into the live campaign actor. That is out of scope for this phase.

## Paths
- Unity clips and body: `Unity/Assets/ArtDirection/Hero01/Models/RestoreMotion/` — `Hero_{Ready,Forehand,Backhand,Serve,Volley,Smash}_v5.fbx`, `Hero_01_FingerBody.fbx`.
- Prefab: `Assets/ArtDirection/Hero01/Prefabs/Hero_01_RestoreMotion.prefab`. Scene: `Assets/Scenes/Hero01RestoreMotion.unity`.
- Code:
  - `Scripts/Tennis/ModularHeroLook.cs` — additive finger grip, grip roll and run carry. The old grip-mesh path is untouched for the older prefabs.
  - `Editor/HeroRestoreMotionReview.cs` — build and capture.
  - `Editor/HeroLookImporter.cs` — accepts `_v5` clip names.
- Blender: `restore_motion/Hero_01_FingerRig.blend`, `Hero_RestoreMotion_v5.blend`, with tools in `restore_motion/tools/` (`prep.py`, `retarget.py`, `finger_rig.py`, `export_grip.py`, `package.py`).
- Review output:
  - `V5_restore_motion_montage.mp4` / `.gif` (same order: Ready → Serve → FH → BH → RunF → RunR → RunL → Volley → Smash; full clips at 1×).
  - `sheet_grip.png`, `sheet_backhand.png`, `sheet_volley_ready_contacts.png`, plus the individual stills.
  - `verification.txt`, `runtime_slot_checks.txt`, `socket.txt`, `slot_bindings.txt`, `source_windows.json`, `retarget_report.json`.
