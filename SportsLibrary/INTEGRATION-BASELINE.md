# Combined Unity + standard-character baseline

Working branch: `feature/standard-characters-integration`.

This branch combines `testing` at `424c36b` (Hole 7 island, rigged golfers, HUD, swing and phone-controller work) with `all-design-mockups` at `0f0d3e2`. Neither shared branch was modified. All permanent-standard character sources, 236 V4 animation variants, clothing, arenas, equipment, crowds, and review material remain intact.

## Which character implementation is which?

- `SportsLibrary/Characters` and `SportsLibrary/Blender`: permanent male/female identities and their V4 rigged implementations. These are the design authority for every sport.
- `Unity/Assets/Resources/Golfer/golfer_m.fbx` and `golfer_f.fbx`: the current game implementation brought in from testing. Their presence is not approval to replace the permanent identities. They are separate assets built by `blender/scripts/golfer_build.py`.
- `GolferView` currently loads one baked swing through a manual PlayableGraph, with an emergency primitive fallback. It does not yet load the V4 library.

## First runtime integration slice

Preserve Hole 7, HUD, networking, input, shot physics and existing saved body/skin preferences. Integrate the standard male and female golfers through the visual layer, not by replacing the game systems.

1. Export the actual V4 characters and a golf drive clip with held equipment from the source studio. Verify rest pose, units, orientation, skins, separate garment meshes and materials. Do not export the original multi-view GLBs wholesale.
2. Give the imported standards their own resource paths and explicit clip selection. The current model loader chooses the first clip; it must not receive a multi-clip asset without an explicit lookup.
3. Make address, top, impact and finish landmarks clip-specific. Current constants are `48/60`, `64/60`, `130/60` seconds for the testing branch's swing; the V4 source is authored on a different 30 fps timeline. Reusing those constants would produce wrong contact timing.
4. Keep the existing meters-to-yards conversion (`1.0936`) exactly once, and verify visual club contact against the game ball. V4 named material colors and skin regions need their own material mapping; the existing `MAT_*` mapping does not cover them.
5. Verify both permanent characters and their grip/torso motion, then add handedness, club-specific clips, modular kit choices and the other sports' runtime scenes as separate tested steps.

The full source library is available in this branch. Exporting and connecting every V4 clip, authoring sport controllers and replacing game visuals are not accomplished by the branch merge itself. See [UNITY-HANDOFF.md](UNITY-HANDOFF.md) for the remaining acceptance criteria.

Historical packaging notes describe the original design-only commit; this document describes the later combined baseline. No runtime visual replacement is claimed yet.

## Baseline validation

- Unity 6000.3.24f1 imported and compiled this combined project.
- EditMode: 42 passed, 0 failed (rerun after the test reporter fix).
- PlayMode with graphics enabled: 2 passed, 0 failed, including render captures. The initial `-nographics` run crashed during camera rendering; use graphics-enabled runs for these capture tests.
- The existing editor test reporter now creates its results directory when callbacks run from the command line on a fresh clone, not only when launched through its menu.
- All 268 inventoried source assets retain their recorded hashes; all 236 V4 actions retain both character and handedness variants.
- These are baseline tests, not evidence that the V4 assets are connected to runtime, visually polished, or performant on iPhone. Existing golfer appearance and posture still need visual refinement/migration.
