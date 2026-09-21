# Sports animation studio — wardrobe and animation v4

## Main Blender file

[Animation studio](sports-animation-studio-v4.blend)

[Combined project with the preserved arenas](all-arenas-and-animations-v4.blend) — this is the file currently open in Blender. The Animation workspace is showing the V4 tennis section, with the RunLeft preview range selected.

This revision extends the existing 01 GOLF, 02 BOWLING, 03 TENNIS and 04 BOXING scenes. Look for the **V4 | wardrobe + expanded motions** NLA track on each character rig. Earlier tracks are retained and muted, not deleted. Each new action is also available by its `__V4` name in the Action Editor. The timeline contains labeled clip starts. Gaps separate clip studies; this is not a continuous gameplay state machine.

## Included

- 236 hand-authored action variants: 59 motion families, each for male/female and RH/LH. These include both rebuilt core actions and additional movement/defense/recovery studies; they are not 236 unrelated movements.
- 16 modular skinned clothing kits: two looks per gender per sport. KIT 1 is enabled by default; KIT 2 collections are hidden in viewport and render. Disable KIT 1 and enable KIT 2 in both places when swapping.
- The original source-derived heads and revised gripping hands, with separately modeled limbs and garment shells. Older fused body/outfit objects remain archived and hidden.
- Standard racket, balls, gloves, golf clubs and tee used as animated props. All 16 equipment variants are available in the equipment scene.
- Wardrobe catalog scene, equipment library scene, and a first-person boxing review scene.
- Packed animation, equipment and clothing reference images in the hidden V4 MOCKUPS collection in CHARACTER LAB.

## Where to look

| Scene | Purpose |
|---|---|
| 01 GOLF | Hip/torso swing, iron, half-swing, chip, putt and abort studies |
| 02 BOWLING | Approach, straight/hook release, early/late release, recovery and abort |
| 03 TENNIS | Lateral movement, running strokes, dives, recovery, serve, smash and volleys |
| 04 BOXING | Full-body attacks, defense, movement and hit reaction |
| 04 BOXING — first-person V4 | Player arms/gloves and an opponent; camera presentation study |
| 05 WARDROBE — 16 fitted kits V4 | All modeled kits at once |
| 06 EQUIPMENT — standard kit V4 | All standard props and handed variants |

Exact scene names use `|` separators in Blender. The combined live project adds `| V4` suffixes to imported scene names to make the revision explicit.

## Review videos

- [Tennis](videos/tennis-v4.mp4)
- [Bowling](videos/bowling-v4.mp4)
- [Golf](videos/golf-v4.mp4)
- [Boxing — full body](videos/boxing-v4.mp4)
- [Boxing — first person](videos/boxing-pov-v4.mp4)
- [Wardrobe catalog render](previews/wardrobe-catalog.png)

Videos are shaded Blender animation reviews sampled from the actual timelines. They are not AI-generated motion videos and not recordings of Unity gameplay.

## Manifests and checks

- [Action manifest](action-manifest.json): exact action names, handedness and timeline ranges.
- [Timeline index](timeline.json): per-sport clip positions.
- [Structural validation](validation.json): weights, finite geometry and sampled character bounds; not a visual-quality certification.
- [Ground-contact corrections](ground-contact-corrections.json).
- [Golf prop fitting](golf-contact-fit.json).
- `live-integration.json` records the combined project and backup of the previously open Blender file.

## Production limitations

This is an editable animation-and-wardrobe prototype, not a final animation system. No motion capture was inferred from stills. The source pose functions reuse the earlier reference-led golf, bowling and tennis work; new actions are manually parameterized from the approved mockups. No claim is made that new Switch Sports footage was watched in this pass.

- Garments are modeled interpretations of the mockups, not exact fabric reconstruction. They have skinned geometry and editable material regions; no cloth simulation, sewing patterns, texture baking or LOD pass is included.
- Hands retain a fixed grip shape without a new finger-animation rig. Extreme grips, garment/body intersections and transitions require additional visual polish.
- Ground correction prevents major sampled penetration but is not a full foot-locking or collision solver.
- Dives, uppercuts, root-motion transitions and first-person comfort need gameplay testing and animator polish.
- Equipment paths are authored demonstration trajectories, not ball or punch physics.
- Boxer opponent timing is a presentation study, not AI or combat rules. One-phone/two-fist interpretation remains a Unity design decision.
- No phone input, UI, scoring, Unity state machines, runtime clothing selection or performance certification was implemented.
- Color edits are available through separate materials. Duplicate shared materials for per-character color changes.

The earlier .blend revisions and the backup of the live arena project are retained. Use the main v4 file for continued animation work, not the intermediate wardrobe-build-stage file.
