# Animation handoff

## Camera priority

Use the eight `gameplay-*` sheets as the primary visual direction. Keep the camera behind the controlled player and the action target ahead. Supporting studio sheets are alternate views for studying silhouettes, equipment, and body language.

- Golf: rear three-quarter down the target line, character offset so the ball and fairway remain visible. Validate a side-on golf stance; the concept images simplify foot alignment.
- Bowling: rear view down the lane, pins readable above the player, ball visible beside the delivery hand and after release.
- Tennis: rear elevated court view, near player below the net, enough space to read wide shots and overhead contact.
- Boxing: rear over-shoulder view, main player in foreground and opponent visible ahead. Preserve clear punch lanes and defensive silhouettes.

These environments establish framing and mood only. They are not proposed replacements for existing game environments.

## Identity and outfits

Preserve the character-specific proportions from `01-model-references`, which are direct renders of the supplied meshes. Generated poses can drift slightly in head shape, limb length, waist shape, and hand details. Blank faces are intentional. Keep clothing modular in future production; the black shirt/trousers and white shoes here are the current reference outfit.

Each original GLB contains multiple character copies in a turnaround arrangement, within a single unskinned mesh. These source files are reference assets, not yet isolated, optimized, rigged player models.

## How to use the sequences

The 12 panels on a sheet are selected pose concepts or state examples, not consecutive frames at a fixed frame rate. Use anticipation, contact/release, follow-through, hold, and recovery as animation events. Build locomotion and transitions separately and blend into sport-ready stances. Use body language for reactions while faces remain blank.

The captions and saved prompt describe the intended action. Generated pixels are not authoritative about exact handedness, anatomy, mechanics, or physics. In particular, some boxing punch silhouettes and tennis preparation poses can be ambiguous about the active arm. Studio poses also vary the exact hand shape and contact point. Review these on the rig instead of tracing them literally. Revised sheets are selected by the catalog when available; original versions remain alongside them for comparison.

## Rig-stage acceptance checks

1. Preserve a consistent dominant hand through every phase; author left-handed variants explicitly.
2. Keep both hands on a shared golf grip or two-hand backhand grip, without sliding through equipment.
3. Lock planted feet; make weight shifts, pivots, and landings coherent.
4. Use one ball and one release/contact event, connected to game simulation.
5. Check swings, serves, gloves, and reactions from the actual rear gameplay camera, including opponent/target visibility.
6. Check every modular outfit against deep bends, overhead reaches, torso twists, and equipment contact.

## Delivery scope

36 selected concept sheets, each with 12 numbered panels: 432 panel concepts. Eight direct source-model renders are also included. This is a broad reference library rather than every possible action or a production animation set. No game code or original GLB was changed. No Unity animations, rigs, or clothing meshes were authored in this mockup pass.
