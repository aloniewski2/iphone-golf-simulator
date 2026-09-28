# NewBackhand_FixRunArm

## Source choice

**Option 3: hand-key in Blender using Eyes Japan Backhand Hard Hit timing.** The local recording is available (`tennis-11-backhand hardhit-yamaoka.c3d`, source excerpt 880–1408 at 120 Hz). The earlier marker retarget and clean actions exist, but their arm frames were not reused: explicit V4 keying provides a controlled left-side contact and wrist pose on the short hero. Reference contact landmark is 56/30 seconds in the prior 30 fps analysis; authored contact is 1.10 seconds.

No Owned v1 action is loaded. The new action starts from the current Ready pose. It is not copied or mirrored from Forehand. Timing reference provenance stays in `../source/mocapdata-tennis/SOURCE.md`; naming this authored action does not change source attribution obligations.

## New backhand

- Export/action name: **Hero_Backhand_v1**.
- Windup by 0.85 s: right hand across the left side, shoulder turn approximately −57°, hip turn −25°.
- Step-in: right ankle advances 16 cm and crosses 7 cm inward; hips shift 3.5 cm left / 4 cm forward and lower 2.5 cm. Feet solved against existing limb lengths.
- Contact at 1.10 s: right wrist **X −20 cm**, racket center measured in Unity **X −58 cm**. Anatomical left is negative model X. No tracked ball collision is implied by this authored contact marker.
- Uncoil: hips reach +18°, chest +32°; follow-through across the body through 1.45–1.90 s, settle to current Ready by 2.70 s.
- Same RightHand socket/local translation/rotation as the approved fixed grip. Forearm roll remains bounded; wrist deviation limited to 28° in the authored solve. No arm keys removed and no floating prop.
- Off hand accompanies preparation, then stays in front during follow-through. Existing open finger geometry is retained; no finger-rig or mesh change.

## Run Forward / Run Left

The clearance pass's searched elbow pole and hand travel clamp were removed from these two left arms. Their pump cadence comes from the **pre-clearance gameplay run** wrist depth, normalized over the same 120 samples.

- Run Forward: upper-arm pump **−27° to +27°**.
- Run Left: softer **−18° to +18°** pump.
- Elbow flexion **78–94°**, always in the forward hinge plane. Neutral hand rotation follows the forearm; no inherited legacy wrist roll.
- Small outward upper-arm direction component (0.16 forward run / 0.12 left run) maintains space at the ribs without the earlier locked-out pose.
- Only `Shoulder.L`, `UpperArm.L`, `LowerArm.L`, `Hand.L` are changed in those two clips. Body, knees, feet, right arm and racket are identical to the preceding phase.

## Wiring and preservation

`ModularHeroLook.backhand` on `Hero_01_BackhandRun.prefab` points exclusively to `Models/BackhandRun/Hero_Backhand_v1.fbx`. The former backhand mapping is absent from this review prefab. Old prefabs/artifacts remain historical comparisons.

Ready, Jump Serve, Forehand, Run Right, Volley and Smash reuse their exact existing `Models/KneesOffArm` clip assets. No re-export of those six clips; no PlayableGraph API changes or .controller. The V4 weighted body, modular wardrobe, materials, lights and fixed socket are reused.

## Outputs

- `Hero_Backhand_Run_v1.blend`: three editable keyed actions on the current V4 rig.
- `Hero_Backhand_v1.fbx`, `Hero_Gameplay_RunForward_v1.fbx`, `Hero_Gameplay_RunLeft_v1.fbx`.
- `V4_backhand_run_full.mp4`: all nine full clips, 30 fps / 36 s.
- `V4_backhand_run_before_after.mp4`: synchronized prior/new review.
- `V4_backhand_run_contact_sheet.png` and per-clip stills.
- Unity scene `Assets/Scenes/Hero01BackhandRun.unity`.
- Unity prefab `Assets/ArtDirection/Hero01/Prefabs/Hero_01_BackhandRun.prefab`.

Verification: `author_report.json`, `scope_verification.json`, `bake_verification.json`, and `../../screenshots/backhand_run_playmode/{verification.txt,backhand_contact.txt}`. Checks sample every frame, including entry and recovery. Torso clearance uses capsule proxies plus visual inspection, not exact mesh collision certification.

**STOP: await Adnan approval.** This is the V4 review candidate; live campaign hit-event synchronization and phone deployment are not claimed.
