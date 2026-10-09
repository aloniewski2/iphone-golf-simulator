# FixInvertedHand_ArmBodyClip — V6 review candidate (2026-09-26)

**Status: awaiting Adnan's approval.** No further phase has been started.

## Root cause: inverted left hand
There was no bad bind pose and no mirrored retarget of the body. Three bugs, all in the V5 left-hand solve (`restore_motion/tools/retarget.py`):
1. **Unsafe flip test.** Each frame, the free left hand could pick the "180°-flipped" reading of the wrist markers if it looked closer to neutral. The comparison used raw quaternion angles, which wrap past 180°, so on many frames it chose the flipped hand (a backwards glove). The wrist markers were never actually swapped. The swapped reading needs 130–171° of forearm twist in every take, and the correct one needs 10–51°.
2. **Stale neutral.** The hand was blended 60% from the forearm neutral *before* the forearm twist was applied, which corkscrewed the wrist.
3. **Mirrored grip for the locked left hand** (Ready throat, backhand). The left grip was a geometric mirror of the right grip, which forced the left wrist 90–150° backwards on 66 of 66 Ready frames and 78 of 84 backhand frames.

**Fixes:**
- Sign-safe angles everywhere.
- The wrist-marker convention is chosen once per clip by an anatomical test (all six clips were labelled correctly).
- The forearm takes the roll; the hand keeps its bend, clamped to a human range.
- The locked left hand now **searches its roll around the handle**: the handle stays through the finger cradle with the thumb side toward the racket head, and the solver picks the most natural wrist, the best reach and no torso clipping.
- The left finger weights are an exact mirror of the right hand's, which removed the small thumb/index shards.

**Proof:** Unity now reproduces both hands **exactly as authored in every clip** (0° median and max difference; the check is in `verification.txt`). No humanoid clamping is hiding anything.

## Arms through the body: both arms
- **Volume:** the torso is measured from the real body, shirt and shorts mesh (arm vertices excluded), as one ellipse per 4 cm slice. Sleeve and arm radii are measured the same way.
- **Solver:** each frame, first swivel the elbow around the shoulder–wrist line (the hand doesn't move). If that isn't enough, rotate the arm outward from the shoulder (the racket moves but keeps its face angle).
- **Two-hand frames:** in Ready and the backhand, both arms are solved together. The racket slides forward or toward the left shoulder, up to 36 cm, until the left hand can reach the handle without clipping.
- **Backhand release:** where even that fails, from frame 61 (about 0.5 s after contact) the left hand releases smoothly for a one-hand finish. Two hands are kept through load, drop and contact.
- **Runs:** these are the original GameplayFixed actions with **only the left arm** moved out of the torso. Stride, hips and right arm are identical.

| Clip | Frames with >1 cm contact, before → after |
|---|---|
| Forehand R / L | 39 / 24 → 0 / 0 |
| Serve R / L | 46 / 49 → 0 / 1 (3.7 cm, one frame) |
| Smash R / L | 38 / 31 → 0 / 0 |
| Volley | 0 → 0 |
| Backhand R / L | 38 / 73 → 0 / 6 (up to ~3 cm around contact) |
| Ready R / L | 72 / 72 → 0 / 17 (≤ 2–3 cm per sample) |
| Run F / R / L (left arm) | 69 / 72 / 70 → 0 / 0 / 0 |

## Kept (C)
- **Strings at contact** (Unity): forehand 1.00, backhand 1.00, serve 0.98, volley 0.93, smash 1.00.
- **Racket vs head:** the racket hoop clears the head on the serve drop (5.3 cm) and the smash wind-up (3.5 cm). This uses a sphere proxy.
- **Racket vs right leg:** it clears on Run F (17.9 cm) and Run R (9.1 cm), using a capsule proxy.
- **Ready:** the left hand is locked on the throat for all 72 frames, with zero reach error.
- **Backhand:** both hands are on the handle through contact, then the left hand releases.
- **Forehand:** no rewrite. The only changes are left-hand and arm clearance.

## Remaining debt
- A few 2–3 cm skin contacts remain in Ready and around backhand contact, where the chibi mitt meets the chest. No visible pass-through in the stills.
- The backhand left hand releases in the finish. The chibi arm can't reach over the big head for a full two-hand wrap.
- The Ready racket is held further forward than the source player held it (for two-hand reach and clearance).
- The smash head clearance of 3.5 cm is tight. It is a sphere proxy, not a mesh test.

## Outputs (`ArtDir/anims/fix_hand_clip/`)
- `V6_fixhand_armclear_montage.mp4` / `.gif`, in the same order as before.
- `sheet_left_hand.png`: Ready hands (front, top, left), backhand load and contact two-hand grip.
- `sheet_arm_clearance.png`: former worst frames for FH, BH, serve, smash and Ready, plus run arm-pump frames.
- `sheet_clearance_contacts.png`: serve and smash head clearance, Run F and Run R leg clearance, volley, backhand, forehand and smash contacts.
- All individual stills, `verification.txt`, `runs_fix_report.json`, `retarget_report.json`.

## Changed files
- `Unity/Assets/ArtDirection/Hero01/Models/RestoreMotion/`:
  - `Hero_{Ready,Forehand,Backhand,Serve,Volley,Smash}_v5.fbx` (re-baked).
  - **New:** `Hero_Run{Forward,Right,Left}_v5.fbx`.
  - `Hero_01_FingerBody.fbx` (mirrored left finger weights).
- `Unity/Assets/Editor/HeroRestoreMotionReview.cs`: the runs are rebound, and the capture adds the hand-playback check plus the clearance and former-worst stills.
- `ArtDir/anims/restore_motion/tools/`:
  - `retarget.py`: the fixes above.
  - `finger_rig.py`: mirrored left finger weights.
  - `measure_body.py`, `runs_pen.py`, `wrist_limits.py`: new measurement tools.
