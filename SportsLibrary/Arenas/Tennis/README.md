# Tennis resort — Blender authoring library

## Current authoring version: Tropical v3

Open [tropical-tennis-resort-v3.blend](tropical-tennis-resort-v3.blend) for the rebuilt tropical arena. It includes 31 image-derived asset interpretations plus a coconut palm, a precisely rebuilt court/net, detailed PBR resort props, and standard-derived floating-hands spectators. The file has separate Tennis, Equipment, Clothing, and Asset Review scenes. Environment GLBs are in `Tripo-v3/Assets`; tennis equipment and clothing have their own `SportsLibrary/Equipment/Tennis/Tripo-v3` and `SportsLibrary/Clothing/Tennis/Tripo-v3` folders.

The generated court/net/sea interpretations remain in Asset Review; measured geometry and separate water/sky are used in the arena. Original 4K GLBs are preserved; secondary working textures are 2K in Blender. Clothing remains separate static geometry, not fitted or rigged. Repaired metric racket/ball exports are in the tennis equipment section.

This version is now integrated into Unity: `TennisGame` loads `Resources/Tennis/TropicalV3/TropicalTennisResort.fbx`, with texture-preserving materials prepared by `TropicalArenaImporter`. Its orientation is corrected at runtime to put the clubhouse on the right and the coastal gardens behind the far baseline. Older v1/v2 assets are preserved but are no longer the active tennis arena.

The live arena includes 36 seated permanent-identity spectators and 16 animated permanent-identity visitors on the baseline promenades. Walkers have varied skin/outfit colors, procedural hip/knee gait and floating hands; they remain outside the playing surface and pause with game time. They are ambient visitors, not gameplay opponents or navigation agents. Equipment/clothing catalog scenes are not included in the environment export.

Unity Play-mode verification loaded the actual Tennis scene, found 234 textured environment renderers and all 16 walkers, and verified walker displacement over three seconds. Gameplay/overview captures and the report are in `outputs/tennis-tropical-rebuild-v3/Unity-Gameplay`. The iOS Unity export succeeded with zero errors. This does not establish real-phone FPS, thermal behavior, or TV performance; installation and device checks are separate.

The exporter is retained at `outputs/tennis-tropical-rebuild-v3/Tools/export_tropical_unity.py`. It extracts the active arena only, realizes collection instances, reduces high-poly image-generated props for mobile, preserves measured court geometry, and exports textures up to 2K. Run `GolfArcade.EditorTools.TropicalArenaReview.Run` for a batch-mode visual check and `GolfArcade.EditorTools.BuildMenu.BuildIOS` to regenerate embedded iOS content.

## Gameplay v2 revision

Archived predecessor: `coastal-tennis-resort-gameplay-v2.blend` and `Resources/Tennis/CoastalTennisResort.fbx`. Five grass garden areas support all 16 tree bases outside the blue court run-off; `gameplay-landscaping-validation.json` records those older checks. Runtime gameplay is documented in `../../TENNIS-GAMEPLAY.md`.

Reference-led 3D environment built around the supplied `tennis court 3d model.fbx`. The source FBX and character-animation studio are unchanged.

Open `coastal-tennis-resort-v1.blend`. The approved concept image is packed into the file in the hidden reference collection. Cameras: behind-player gameplay, elevated resort overview, and clubhouse detail.

## Editable scene groups

1. Court: adapted imported geometry, blue run-off, white line geometry, net tape and center strap.
2. Architecture: limestone promenade, tiered spectator terraces, clubhouse, upper terrace, pergola, and balustrades.
3. Furniture: benches, seats, umbrellas, umpire chair, and lamps.
4. Landscaping: stone pines, cypresses, planters, shrubs, flowers, and balcony vines.
5. Background: actual 3D water surface, coastal landforms, and simplified distant village buildings. No flat concept-image background is used in the render.
6. Lighting and three cameras.

The imported asset was normalized to metre-scale proportions; the playable markings use a 23.77 m court length, 10.97 m doubles width, 8.23 m singles width, and service lines 6.40 m from the net. [ITF court reference](https://www.itftennis.com/en/about-us/organisation/tennis-glossary/). The source mesh's net and surface proportions required separate axis scaling; it is not an untouched uniform-scale import.

## Status and handoff

This is an editable visual environment, not a game-ready Unity scene. The procedural Blender materials, object count, collisions, vegetation instancing/LOD, texture baking, and lighting need engine-specific optimization. No gameplay ball physics or navigation was added. The scene follows the reference's composition and major architectural features, not pixel-identical reconstruction.

The near-baseline camera corridor is deliberately open. Enclosing it with a rear fence would require a camera-friendly visibility solution. Court surface/net collision and material behavior should be authored separately from visible geometry in Unity.
