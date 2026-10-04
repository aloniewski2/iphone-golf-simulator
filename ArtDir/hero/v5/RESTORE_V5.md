# Hero V5 restored — 2026-09-29

Adnan requested a return to original Hero V5 after reviewing the integrity experiment.

## Active character

- Authoring: `ArtDir/hero/v5/Hero_01_V5.blend`, restored from the untouched V5 archive `ArtDir/hero/v5_astra/Hero_V5_AstraTex_Baseline.blend`.
- Production: original Body, Hair, Shirt and Shorts FBXs restored from `ArtDir/hero/integrity_v1/before/assets`, together with both original gameplay/restore-motion prefabs. Hat, shoes, Humanoid avatar, racket socket and animation sources were not replaced.
- Original V5 open hair shells render two-sided again in Unity, SceneKit and the browser viewer.
- Native menu idle, 20 forehand bake frames and seated menu pose rebuilt from restored V5. Eight swing targets plus menu/lounge packaged for iOS. Original Ready fallback restored from local Git/LFS.
- Browser review: `ArtDir/hero/v5/viewer/`, static posed mesh using the exact native menu export.

## Preservation / scope

Current club menus, transitions, lighting and gameplay are retained. Binding validation and optional build-delta readers remain compatible infrastructure; they do not change V5 vertices. Original V5 has no BuildSlim/BuildBroad shapes, so the integrity size corrections and its neck seal are not active. The browser size control is locked to the original proportions.

The repaired blend, exports, captures and previous viewer remain in `ArtDir/hero/integrity_v1`. Files overwritten during this return were also saved under `active_before_v5_restore/`. The isolated Unity AstraTex prefab remains an archived experiment and should not be promoted; rebuild a fresh isolated baseline from active V5 before a later texture phase.

## Verification

- Seven authoring/production files match archived V5 SHA-256 hashes (`restore-v5-hashes.json`).
- Unity menu and lounge exports completed successfully; 38 parts, no integrity neck seal or build deltas.
- Menu, lounge and all 20 swing binaries have identical compatible layouts, finite vertices and valid triangle indices.
- All 17 existing gameplay-animation FBXs retain their recorded hashes.
- Browser shows V5 and exports an assembled GLB.
- `git diff --check` passed. Native snapshot tests were adapted to the original V5 asset contract but were not rerun. No phone build was installed by this rollback.
