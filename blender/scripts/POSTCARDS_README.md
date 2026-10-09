# Postcard holes (8 Needle, 9 Split, 10 Crater) — pipeline contract

Brief: `ArtDir/environments/13_POSTCARD_HOLES.txt` (the gate lines are authoritative), stills in
`ArtDir/environments/refs/{needle,split,crater}.jpg` (mood only; the brief wins where they disagree).

Copy of the hole 7 pattern, new files only. **Never write to** `blender/hole_07.blend`,
`Unity/Assets/Resources/Course/hole_07.fbx` (+ `.meta`), or `blender/scripts/hole07_*.py`,
`phase*.py` (read them, copy from them). Reference hashes (must stay identical):

    a7842f25ca64a1a91be9d881611ff84de63eb1f2e1ad8111330b9834edc4c4af  blender/hole_07.blend
    813850bff6d107c0424dd9ca22eeaedf1ba9d3fc7ef6e9f0eed54bc67856c6ed  Unity/Assets/Resources/Course/hole_07.fbx

Do not touch hero / tennis / menu / Swift files. Other chats are editing this repo and a Unity batch
process owned by another chat may be holding the Unity project: **do not launch Unity** unless the
lead tells you to. Blender runs headless: `/Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup --python <script> -- <args>` (Blender 5.2.2).
System `python3` (3.14) has no numpy/bpy; Blender's own python does. Do not commit, do not git-add,
do not run `git stash/checkout/reset`.

## Files (all new unless noted)

| file | owner | role |
|---|---|---|
| `blender/scripts/postcard_check.py` | lead (done) | pure-Python mirror of `Hole.cs` scoring + the design gates; `--map` prints the lie map |
| `blender/scripts/postcard_emit_cs.py` | lead (done) | prints the `Course.Postcards()` block from the designs; `--check Hole.cs` verifies no drift |
| `blender/scripts/hole08_design.py`, `hole09_design.py`, `hole10_design.py` | design agents | PURE PYTHON (no bpy, no mathutils) data in COURSE YARDS |
| `blender/scripts/postcard_lib.py` | lib agent | generalised copy of `hole07_lib.py` + terrain-with-holes, ground-surface builders, rock import, export, ray-check |
| `blender/scripts/hole08_build.py`, `hole09_build.py`, `hole10_build.py` | build agents | bpy scene build for one hole (like `phase2_5_course.py` + phases 6-13 merged), saves the .blend, exports the FBX |
| `blender/hole_08.blend`, `hole_09.blend`, `hole_10.blend` | build agents | scenes |
| `blender/previews/hole_08_overview.png` ... | build agents | one ortho still per hole (NOT a flyover) |
| `Unity/Assets/Resources/Course/hole_08.fbx`, `hole_09.fbx`, `hole_10.fbx` (+ `.meta`) | build agents | exports |
| `Unity/Assets/Scripts/Course/Hole.cs` | integrator | add `Course.Postcards()` (+ `Water(...)` helper); leave `Cliffside()` and `Meadow()` untouched |
| `Unity/Assets/Scripts/Game/GolfGame.cs` | integrator | the one `Course.Cliffside()` call in `Awake` becomes `Course.Postcards()` |
| `Unity/Assets/Tests/EditMode/PostcardHoleTests.cs` | integrator | NUnit gate tests for `Course.Postcards()` |
| `Tools/PostcardCheck/` | harness agent | standalone C# runner (Unity's bundled dotnet) that compiles the REAL `Hole.cs`/`CourseShot.cs` and plays shots |

## Design module schema (`holeNN_design.py`, pure Python, module-level names)

```python
NUMBER = 8; NAME = "Needle"; PAR = 4
PLAY_Z = 22.0                       # metres above the sea: the ONE play height (terrain top)
CENTERLINE = [(x, d), ...]          # course yards, X right of the tee line, D down the hole. [0] == (0.0, 0.0) is the tee; [-1] is the pin
FAIRWAY_WIDTH = 16.0; GREEN_RADIUS = 14.0; ROUGH_WIDTH = 8.0
SHORE = [(x, d), ...]               # ONE closed polygon, no repeated first point, dense (>= 40 pts, edges ~3-8 yd), either winding
HAZARDS = [dict(kind="water"|"bunker", x=.., d=.., width=.., length=.., tag="..."), ...]   # axis-aligned ellipses; width along X, length along D
LIMITS = dict(length_min=.., length_max=.., par=.., carry_max=.., water=.., bunker=.., forced=True|False, pads=.., ...)  # see postcard_check.py
SCENERY = dict(...)                 # free-form, read only by this hole's build script (arch/ruin spots, lava, pillar, rock rings ...)
```
Every coordinate/size is rounded to 0.1 yd: Hole.cs carries exactly these numbers (`postcard_emit_cs.py`).
Run `python3 blender/scripts/postcard_check.py hole08_design --map` until every `GATE:` line passes.

## What Hole.cs / CourseShot.cs actually do (verified by reading them; do not change them)

* `LieAt(p)` order: hazards first (ellipse: `((x-X)/(W/2))^2 + ((d-D)/(L/2))^2 <= 1`; water -> Water, bunker -> Bunker),
  then outside `Shore` -> Water, then `|p-pin| <= GreenRadius` -> Green, `|p-tee| <= 4` -> Tee, then distance to the polyline
  `<= FairwayWidth/2` -> Fairway, `<= FairwayWidth/2 + RoughWidth` -> Rough, else OutOfBounds.
  So: hazards beat everything incl. the green; a water ellipse may stick out past the shore; the fairway is one constant width
  around the polyline (round joins, round ends, clipped by the shore and cut by hazards); the green is a disc.
* Rough x0.85 club speed, bunker x0.6, Water and OutOfBounds +1 stroke (water: drop back toward the origin; OB: replay).
* The ball flight is NOT checked against water in the air; only the roll (from touchdown on) tests `LieAt`. A carry therefore means: touch down beyond the water.
* Clubs: carry of a full strike Driver 250 yd, 7-iron 160, wedge 90 (`GolfClub.ReferenceDistanceYards`). Lie speed factors cut distance more than linearly.
* `RecommendedTarget(ball)` (aim assist) aims at the next CENTERLINE STATION >= 35 yd away: every station must be dry fairway, and the stations are the intended landing spots.
* The brief's "carry" = length of the centerline run that is Water (what `postcard_check.py` measures as CARRY_MAX), with >= 40 yd of clean
  dry fairway short of it (lay-up) and >= 30 yd after it. Tight design target: carries well under the limits (about 85-120 yd).
* Rough band: Needle says "rough about 8 then the shore" so on-land out-of-bounds grass must be ~0%. Split needs `ROUGH_WIDTH` large
  enough that the left ridge (and every dry cell) is Rough, not OutOfBounds.

## Unity side (HoleView.cs) — what the FBX must satisfy

* `Resources.Load("Course/hole_{Number:00}")`; finds `MARKER_TEE`, `MARKER_PIN`, `MARKER_UP` anywhere under the root; maps tee->pin
  (axis-free, uniform scale metres->yards, no mirror) onto the hole's tee->pin. **Author the model with the tee marker at Blender
  (0, 0), +Y = down the hole = course +D, +X = right = course +X, 1 m = 1/1.0936 yd, i.e. blender_xy = course_yards * 0.9144.**
  `MARKER_UP` = 50 m straight above the tee marker. The water level is Blender z = 0 and stays at Unity y = 0.
* Ground = every mesh whose name STARTS WITH `TERRAIN`, `FAIRWAY`, `GREEN`, `TEE_BOX`, `BUNKER` (or `CART_PATH`): it gets a MeshCollider and
  `GroundHeight` raycasts down from y = 400 onto the topmost hit; miss -> height 0 (the water). Anything else is scenery: **no scenery mesh may
  start with one of those prefixes** (rocks, arch, pillar, flag, lava, foam, water: `ROCK_*`, `CLIFF_ROCK*`, `WATER_*`, `FLAG*`, `HOLE_CUP`, `TEE_MARKER_*`...).
* Colliders are single-sided: never leave a face pointing up inside a water ellipse or outside the shore. Cliff walls must be vertical or
  undercut (never wider at the bottom than at the top edge in plan view) and must not have any upward-facing surface outside the top-surface outline.
* Material names are overridden by `HoleView.Palette` (flat game colours): reuse `MAT_ROUGH, MAT_FAIRWAY, MAT_FAIRWAY_STRIPE, MAT_FIRSTCUT,
  MAT_GREEN, MAT_BUNKER_LIP, MAT_SAND, MAT_WATER, MAT_WATER_SHALLOW, MAT_FOAM, MAT_CLIFF, MAT_CLIFF_DARK, MAT_ROCK, MAT_ROCK_DARK, MAT_FLAG, MAT_POLE,
  MAT_CUP, MAT_BALL` with the exact names and the colours from `phase11_13_look.py`. Only the crater may add one new material, `MAT_LAVA` (orange),
  used on its water mesh only (name the mesh `WATER_LAVA`). Names starting `WATER` cast no shadow in Unity. No animation anywhere.
* Gameplay placeholders Unity hides: `FLAG, FLAG_POLE, HOLE_CUP, BALL_START, MARKER_TEE, MARKER_PIN, MARKER_UP` (build them like `phase9_10_clubhouse_gameplay.py`;
  `TEE_MARKER_1/2` stay visible).
* Import settings: copy `hole_07.fbx.meta` for each new FBX with a fresh `guid` (32 hex chars). `isReadable: 1` must stay.

## Flat play surface (brief: "play surfaces are one height")

`PLAY_Z` is constant. TERRAIN top faces are at exactly PLAY_Z; hole 7's small z offsets are kept: fairway +0.14 m, first-cut +0.08, green +0.26, apron +0.18,
tee +0.24, bunker sand +0.30 (lip up to +0.57), all within +0.6 m of PLAY_Z. No slope anywhere on a play surface. Cliffs, lava and sea are below, as scenery.

## Hole 7 object contract (the new FBXs reuse it)

Collections `COURSE, ENVIRONMENT, STRUCTURES, GAMEPLAY, LIGHTING, REFERENCE`, hidden `ASSET_LIBRARY`; one root empty `HOLE_NN_ROOT` parenting everything;
objects `TERRAIN_*, FAIRWAY, FAIRWAY_FIRSTCUT, GREEN, GREEN_APRON, TEE_BOX, BUNKER_NN + BUNKER_NN_LIP, WATER_OCEAN (4000 m plane at z=0), WATER_SHALLOW,
WATER_FOAM, ROCK_*/CLIFF_ROCK.nnn instances of the library rocks, HOLE_CUP, FLAG_POLE, FLAG, TEE_MARKER_1/2, BALL_START, MARKER_TEE/PIN/UP`, cameras and suns
(not exported). FBX export like the other repo scripts: `bpy.ops.export_scene.fbx(object_types={'MESH','EMPTY'}, axis_forward='-Z', axis_up='Y',
apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', bake_anim=False, use_mesh_modifiers=True, add_leaf_bones=False, path_mode='AUTO', use_selection=True)`
with the library originals and cameras/lights excluded; compare `GlobalSettings` and object transforms of the result with `hole_07.fbx` (import both in Blender).
