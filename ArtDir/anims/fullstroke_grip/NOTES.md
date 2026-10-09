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
