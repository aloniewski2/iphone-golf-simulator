# AnimRepair_Backhand2H_RestoreSlots

## 0 — Slot wiring and frozen-label verification

The prior prefab audit found nine distinct, non-null assets. Recorded source frames showed motion in Serve, Forehand, Volley and Smash; the older 4-second takes contained very short actions followed by long Ready tails. No null→Ready alias was found, so no alias bug is claimed.

The new candidate explicitly binds the known-good JumpServe, Forehand and Smash assets from `Models/KneesOffArm/`. Volley is replaced with the front-block action. Ready, Backhand and RunForward use their repaired assets. `SampleForReview` now rejects missing/invalid slots rather than silently evaluating an empty input. `HeroLookImporter` has a single-name opt-in for `Hero_Backhand_v3` as Humanoid; no broad importer change.

The verification now tests BOTH:
1. Real `PlayStroke` / GameTime playback for all eight action slots, ignoring initial blending when measuring motion.
2. Every frame of all nine 120-frame takes through the existing PlayableGraph, with valid Avatar/shared wardrobe/fixed socket checks.

See `../../screenshots/anim_repair_playmode/slot_bindings.txt`, `runtime_slot_checks.txt`, and `verification.txt`. Every swing slot moves. The montage repeats the short active windows of Serve, Forehand, Volley and Smash at 1× with an explicit REPEAT label. `V4_anim_repair_full_takes.mp4` includes all original 1,080 captured frames and Ready tails without trimming.

## A — Ready

- Original feet, stance and lower-body keys retained.
- Right arm is solved from a centered handle target with a softly tilted racket, clear of the face silhouette.
- Right wrist deviation maximum 7.2° from the rig's neutral wrist; elbow interior 97–104°.
- Right elbow/torso surface proxy gap minimum 7.6 cm, reaching ~10.4 cm through the small idle motion.
- Existing repaired left-arm clearance retained. No source wrist roll is pasted back onto the V4 bind.

## B — Hero_Backhand_v3: two hands for the whole clip

Source choice: hand-key in Blender using Eyes Japan `tennis-11-backhand hardhit-yamaoka.c3d` timing reference (local source excerpt 880–1408 at 120 Hz). The old Backhand NEW action is ignored. No Forehand mirroring; no Owned v1 action is loaded. Source provenance remains at `../source/mocapdata-tennis/SOURCE.md`.

A coupled two-hand constraint drives this action:
- Right palm centroid is the fixed racket origin near the butt of the handle.
- Left palm centroid stays **8.5 cm above it along the handle**, with its own optimized grip rotation.
- Both arm chains solve to those exact targets on every frame. No support-hand release, frame/string hold or per-frame floating prop.
- Same RightHand parent/socket: local T (−0.0000394226, +0.0008746348, −0.0002774258), local R (0, 0, −90) degrees, 0.01 prop scale compensating for the imported 100× bone scale.
- Unity measured left-palm target error rounds to **0.0000 m** across the full clip; right socket drift is zero.
- Hips coil **−34°**, then uncoil to **+27°**: total 61° range. Right hip advances with the load; right foot steps 12 cm forward / 5 cm inward; hips lower 3 cm. Chest turn moves from −52° to +38°.
- Timing: load 0–1.50 s (37.5%); contact 1.50–1.60 s (3 frames); follow 1.60–3.20 s (40%); settle afterward.
- Contact frame 48: racket center X **−0.505 m**, right wrist X **−0.129 m**, anatomical left. This is an authored contact marker, not live ball/collision certification.
- Finish lowered and moved across the body so it no longer covers the face in the review camera.
- Right elbow remains 57–110° interior, right wrist max 34.6°; no 90° fold. The discrete solve's largest consecutive right-forearm rotation is ~25° during the 3-frame snap, not a 180° roll flip.
- Minimum sampled hoop-to-head sphere proxy gap **22.5 cm**. Visually reviewed load/contact/follow; both palms remain on handle.

V4 has fixed open toy fingers. This phase keeps those meshes as required: it positions palms and fingers around the handle, without claiming new finger articulation or changing the hero mesh.

## C — Forehand volley

A new short front punch/block replaces the prior drive-like path. The racket uses the repaired neutral grip, stays in front, extends 16 cm at contact and returns by 1.35 s. Three-frame punch transition; minimal preparation. Hips stay facing the net. Left-arm counterbalance remains outside the torso.

Right wrist max 6.6°; right elbow opens from ~89° to 147°; right elbow/torso proxy clearance ≥11 cm. Unity right-hand motion is ~15 cm. It is its own Humanoid clip and passes real-time playback.

## D — Run Forward

Only the right-arm keys were changed from the preceding candidate. The handle moves opposite the already-approved left-arm stride phase, with a controlled tilted racket. The arm stays bent: elbow 94–139° interior; wrist max 8.8°. Unity measures ~9.2 cm of hand motion from the starting sample; the racket stays well clear of torso/thigh proxies. Original body, legs and left-arm cycle retained.

## E — Restored motion / untouched clips

JumpServe, Forehand and Smash explicitly reuse the known-good takes. Actual runtime motion measured about 1.2 m / 0.64 m / 1.3 m respectively at the right hand; none is a Ready substitution.

**Run Right and Run Left are untouched**: exact prior pose data, FBX asset GUIDs and prefab bindings, with SHA-256 evidence in `scope_verification.json`. No mesh, weights, materials, lights, wardrobe or gameplay hit mechanics changes.

## Deliverables

- `Hero_AnimRepair_v3.blend`, four repaired FBXs including `Hero_Backhand_v3.fbx`.
- `V4_anim_repair_montage.mp4` — readable action windows; repeated windows explicitly labelled.
- `V4_anim_repair_full_takes.mp4` — untrimmed nine-clip capture, 36 s / 30 fps.
- `Backhand_LOAD.png`, `Backhand_CONTACT.png`, `Backhand_FOLLOW.png`, `Volley_CONTACT.png`, `RunForward_SIDE.png`, `repair_acceptance_stills.png`.
- Unity scene `Assets/Scenes/Hero01AnimRepair.unity`.
- Unity prefab `Assets/ArtDirection/Hero01/Prefabs/Hero_01_AnimRepair.prefab`.

## Acceptance / limits

A–E pass the authored-pose, runtime, grip and clearance checks described above and the rendered review. Capsule/sphere checks are proxies, not exact mesh collision certification. Visual approval remains Adnan's. No live campaign actor replacement, phone deployment, deeper animation pack or further polish is included.

**STOP: await approval.**
