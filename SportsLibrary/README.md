# Sports Library — start here

**Runtime update:** the permanent male and female characters now play the golf drive in Unity. See [golf runtime integration](GOLF-RUNTIME-INTEGRATION.md) for implemented behavior and remaining limitations. The source-only notes below describe earlier commits.

This working branch now includes the latest Hole 7 Unity game alongside the library. Read the [combined integration baseline](INTEGRATION-BASELINE.md) for current runtime status and the exact character/swing connection points. Original packaging notes below describe the design-library commit, not a completed runtime migration.

The male and female characters in this library are the **permanent character standards for every sport**, including future sports and crowd variants. Their identity is approved; the current animation and mesh implementations still require technical polish. See [CHARACTER-STANDARD.md](CHARACTER-STANDARD.md) before changing or integrating characters.

## Quick navigation

| Need | Open |
|---|---|
| Character identity and customization rules | [Character standard](CHARACTER-STANDARD.md) · [Machine-readable registry](Characters/standard-characters.json) |
| Everything together in Blender | [Combined arenas and animations V4](Blender/all-arenas-and-animations-v4.blend) |
| Dedicated animation authoring file | [Animation studio V4](Blender/sports-animation-studio-v4.blend) |
| Original male / female source models | [Male](Characters/Originals/StandardMale.glb) · [Female](Characters/Originals/StandardFemale.glb) |
| Animation names and frame ranges | [Golf](Animations/V4/Golf/README.md) · [Tennis](Animations/V4/Tennis/README.md) · [Bowling](Animations/V4/Bowling/README.md) · [Boxing](Animations/V4/Boxing/README.md) |
| Clothing | [16-kit catalog](Clothing/wardrobe-catalog.png) · [Clothing concepts](Clothing/Concepts/README.md) |
| Equipment | [Overview](Equipment/equipment-overview.png) · [Asset manifest](Equipment/asset-manifest.json) · [Editable master](Equipment/standard-sports-equipment.blend) |
| Tennis resort | [Blender](Arenas/Tennis/coastal-tennis-resort-v1.blend) · [Notes](Arenas/Tennis/README.md) |
| Bowling alley | [Blender](Arenas/Bowling/coastal-bowling-resort-v1.blend) · [Notes](Arenas/Bowling/README.md) |
| Boxing ring | [Blender](Arenas/Boxing/boxing-arena-v1.blend) · [Notes](Arenas/Boxing/README.md) |
| Arenas with standard-character crowds | [Crowd library and notes](Crowds/README.md) |
| Motion reviews | [Video index](Previews/README.md) |
| Unity integration and known limitations | [Unity handoff](UNITY-HANDOFF.md) |
| File provenance and integrity | [Asset inventory](asset-inventory.json) |
| Packaging checks | [Validation report](HANDOFF-VALIDATION.md) · [Repeatable verifier](Tools/verify_library.py) |

## What is committed

Current V4 editable masters contain 236 named animation variants across 59 families and 16 separate skinned clothing kits. The library also includes equipment FBX/GLB exports, three arenas, static crowd variants, original supplied character/arena models, concepts, and video reviews. Golf animation staging is in the studio; there is **no finished resort golf-course asset** in this library.

This is an **asset-source and review handoff**, not a claim that all assets are integrated into the Unity game. Source files intentionally live outside `Unity/Assets` so Unity does not automatically import large Blender authoring scenes or concept art. The existing Unity scene and procedural golfer are unchanged by this commit. They do not supersede the permanent standard characters.

Historical concept sheets are retained in `Design/*-Historical` for context. They may show outdated camera directions or text. The character standard and current handoff take precedence: **boxing gameplay is first person**; tennis, golf, and bowling are third person. Reference footage from third parties and personal screen recordings are not redistributed.

Excluded from this commit: duplicate ZIPs, duplicate review encodes, raw rendered frame sequences, `.blend1` backups, intermediate wardrobe builds, older superseded animation masters, and machine-specific installation tools. These remain untouched in the original local workspace. Archived actions already retained inside V4 are preserved.

## Download correctly

Binary models, Blender files, images, and videos use **Git LFS**. Install Git LFS, clone the repository, check out this feature branch, then run `git lfs pull`. Do not open a tiny LFS pointer file as a model. Downloading a GitHub ZIP may not include actual LFS objects depending on repository settings.

Branch from `testing` and submit changes back to `testing`, following [CONTRIBUTING.md](../CONTRIBUTING.md). Publishing this library will require sufficient GitHub LFS storage and transfer quota. This handoff commit does not itself upload data.

## Blender entry points

Open the combined master and choose the Animation workspace. Sport scenes have `| V4` suffixes. Select `Male_<Sport>_Rig` or `Female_<Sport>_Rig` and use the `V4 | wardrobe + expanded motions` NLA track. In the standalone studio the base sport scenes are `01 GOLF`, `02 BOWLING`, `03 TENNIS`, and `04 BOXING`. Use the wardrobe and equipment catalog scenes for modeling review.

The committed files are saved snapshots. Unsaved edits in a running Blender session are not implicitly included. Original report files are retained as provenance and may mention the creator's former local paths; use this index and `asset-inventory.json` for the portable repository paths.
