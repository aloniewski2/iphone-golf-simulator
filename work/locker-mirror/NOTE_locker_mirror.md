# LOCKER_MIRROR (2026-10-04)

The Swift locker mirror now draws the match heroes the way Unity's cloth / skin pass does (weave + thread map + seams on the kit, soft triplanar skin normal, per-surface smoothness) and moves them by
playing Unity's own clips (`<Sex>_ReadyIdle`, `<Sex>_Serve`) on the hero's skeleton. No tile turns the character root any more. Gate: `GATE_DEFS_preregistered.md` / `GATE_RESULTS.md`; proof: `proof/`.

## What changed (and where)

| file | what |
|---|---|
| `Unity/Assets/Editor/MatchHeroLockerExport.cs` (the only Unity file edited) | runs `MatchHeroLook.OwnMaterials` on the instance (what Awake does in the game) and writes, per submesh, the **look read off the runtime material** (`TennisCloth` / `TennisCharacter` numbers, `weaveTile`, `weaveAngle`, map names, `tileMetres` from `ClothFor` by reflection); **UVs for the kit parts only**; the body's bind-pose positions; `MatchHero_<Sex>_Rig.lzfse` (bind meshes, bone weights, inverse binds, the ReadyIdle and Serve clips as 60 Hz bone tracks in hero space) with a self-check that skinning the exported data reproduces the live rig (≤ 0.0011 mm); copies the maps; optional golden vertices (`MH_LOCKER_GOLDEN`). Output is bit-reproducible. |
| `GolfArcade/Unity/MatchHeroData.swift` (new) | manifest types, loaders, static geometry (+ kit UV / body bind-pose texcoord channels), `buildHero(picks:)`, `RigData` (skinned part data, clips, track interpolation). No UIKit, no `Player`. |
| `GolfArcade/Unity/MatchHeroSurfaces.swift` (new) | the look: Metal shader modifiers for cloth (surface: seam map × tint × thread break, weave normal from screen-space UV derivatives, rim; lighting: wrapped diffuse + faint broad gloss + grazing sheen; fragment: exposure + soft shoulder) and skin (geometry varying + surface: triplanar soft normal from the bind pose), Blinn for eyes / decals / frame / strings; texture loading (colour maps sRGB, data maps tagged linear, rows flipped to Unity's v-up). |
| `GolfArcade/Unity/MatchHeroMotion.swift` (new) | `HeroRig` (swaps the 7 skinned parts to their bind-pose meshes with `SCNSkinner`s on a flat skeleton, rigid parts follow their track; at ReadyIdle[0] it is the static hero) and `MenuMotion` (ReadyIdle loop; one Serve blended in/out over it; settle-to-still). |
| `GolfArcade/Unity/CharacterModelPreview.swift` | `MatchHero` now delegates to the three files; Coordinator `play(_:)` + a `CADisplayLink` at 60 fps pose the skeleton; `configureClub`: locker / settings / store = ReadyIdle, play = one Serve then Ready, reduced motion = static still Ready; `configureIdle(.tennis)` = ReadyIdle + the prop ball; the idle view is 60 fps (was 30); the loading screen's practice swing hands over to the Forehand morph and back; the play tile's camera backs off about the feet during the Serve. |
| `GolfArcade/Unity/CharacterAssets/MatchHero_*` | re-exported (kit meshes had 5 more vertices since HERO_DETAIL, so base + swing frames are regenerated together), + `_Rig.lzfse` ×2 (3.1 / 2.6 MB), + 8 maps per sex (cloth weave, skin normal, 6 kit seam maps; 1.4 MB). Bundle assets 49 → 57 MB. |
| tests | `LockerMirrorTests` (gate, 17 tests), `LockerMirrorProofTests` (stills, films, fps), `DressedHeroSnapshotTests` (kit colours now read from the cloth tint; default trim / sole updated). |

## Why bone tracks and not morph frames "like the Forehand"
One morph frame is 2.4 MB (106 k vertices). A Serve needs 40+ frames of an error-bounded sampling (≥ 100 MB in the bundle, ≈ 200 MB resident per sex) and linear vertex blends flatten rotating limbs.
Bone tracks + weights are 3 MB per sex, play at any rate, are interpolated by slerp, and are verified against Unity's own skinning (25 k golden vertices per sex, ≤ 0.0011 mm). The Forehand morph stays as is for the loading screen.

## Look decisions worth knowing
- **Numbers come from Unity's runtime materials**, not from a copy of `MatchHeroLook`'s constants: the export fails if a hero material is not on `TennisCloth` / `TennisCharacter`.
- **Texture conventions** (the first renders had wrong seams until fixed): Unity's v = 0 is the bottom row of a PNG, Metal's is the top → rows are stored flipped at load (a test checks that hems / collar are darker under the right convention and not under the flipped one). Data maps (weave, skin normal) are tagged linear or SceneKit would sRGB-decode the normals.
- **Weave fade**: a thread is 1/8 of a 2.0-2.9 cm tile; on a phone the whole hero is ≈ 0.6 px/mm so a thread is ≈ 1.5 px and the weave only aliases (moiré, orientation-dependent patches). The weave is faded by the threads' pixel footprint measured from the surface's WORLD-space pixel size (not from UVs: UV density differs per island and made patches); full at ≥ 3 px/thread, gone at ≤ 1.4 px. The locker's close-ups show threads; the whole body shows trim, seams, folds and sheen. `sceneExposure` 1.4 (Unity's 0.7 is tuned to the court's sun) and `weaveMipBias` 0.6 calibrate the SceneKit light rig; both are `static var`s in `MatchHeroSurfaces`.
- Seam map = baked cavity / edge / border / seam darkening on the OUTER shell in the bind pose; its jagged soft grey patches (chest, shorts front, shoe heel) are in the map, Unity draws them too.
- Skin stays on SceneKit Blinn (smoothness 0.2 → specular .037, shininess .18, the mapping the locker always used) + the triplanar normal; the skin normal is subtle by design (0.0045-0.0069 mean luma).

## Pitfalls found (do not repeat)
- **The simulator's OFFSCREEN `SCNRenderer.snapshot(... .multisampling4X)` freezes skinned meshes at their first pose** (rigid parts keep moving). The real `SCNView` (also multisampled) is fine: `testBothMenuViewsRun…` compares real-view pixels over time. Posed proof frames are drawn by a real `SCNView`, or at 2× without multisampling.
- A shader modifier's uniforms must be unique across a material's modifiers (`redefinition of parameter`), `thread` is a reserved word in MSL, a texture-valued argument on a UV-less mesh needs a texcoord (`texcoord0 is missing from the vertex descriptor`: the grip uses a flat variant with no map bound), and a failed compile renders magenta with the error only in os_log: run with `OS_ACTIVITY_DT_MODE=YES`.
- `value(forKey:)` on a material key that was never set can throw: tests read modifier uniforms only where they were set.
- `xcodebuild test` can hang at teardown: `scripts/xcode.sh` kills it after the verdict line; Unity batch runs and xcodebuild must not overlap (Burst), the scripts wait for each other.
- The Serve clip is 2.5 s, has two hops (pre-toss pop +210 mm, contact hop) and ends with the pelvis 0.3 m forward of the start (the follow-through lunge): the blend back to the Ready loop takes 0.4 s. The hero root never moves.
- Serve reach: bounds box y up to 2.42 m on a 1.65 m hero: without the camera back-off the racket leaves the tile.

## Rebuild / re-prove
```
work/locker-mirror/scripts/sync_clone.sh       # copy the exporter into the APFS clone of Unity/ (cp -c -R Unity <scratch>/UnityClone once)
work/locker-mirror/scripts/unity_run.sh GolfArcade.EditorTools.MatchHeroLockerExport.Run <log> MH_LOCKER_OUT=<dir> MH_LOCKER_REPORT=<file> MH_LOCKER_GOLDEN=<dir>
# copy <dir>/MatchHero_* into GolfArcade/Unity/CharacterAssets/ and <dir>/golden/*.json into work/locker-mirror/data/
xcodegen generate --spec project.yml && xcodegen generate --spec project-unity.yml
work/locker-mirror/scripts/xcode.sh build && work/locker-mirror/scripts/xcode.sh test -only-testing:GolfArcadeTests/LockerMirrorTests
work/locker-mirror/scripts/xcode.sh test -only-testing:GolfArcadeTests/LockerMirrorProofTests/<testRecord…>   # stills, films, fps into proof/
python3 work/locker-mirror/scripts/make_sheets.py     # before / after sheets
work/locker-mirror/scripts/protected_files.sh > sha_after.txt; diff sha_before.txt sha_after.txt
work/locker-mirror/harness/build.sh && <binary> <outDir> static|rig|ready|serve|shoedbg|…   # macOS render harness of the same three files (fast look iteration)
```
Rollback: `baseline/` holds the pre-change copies of every edited file (`orig/*.orig`, `SHA256_edited_files_before.txt`) and the 28 previous `MatchHero_*` assets (`CharacterAssets_before/`).

## Not done / open
- No physical iPhone was used (simulator only: locker tile / play tile / idle view 60.0 / 60.0 / 60.0 fps on this Mac's GPU, `pose(at:)` 0.22 ms per frame; a device number is still to be measured).
- The locker TILE state (hero reacting to Locker focus) exists only with a focus-driven input (TV / remote); on the phone a tap on Locker opens the Locker screen, so the real-app phone recording shows the Home play tile and the Locker screen; the locker tile is recorded in the TV root.
- The golf idle (`configureIdle(.golf)`) still sways the whole character (it is a golf prop animation with no clip to play); `HeroTennisDriver`, clips, shaders and the course are untouched.
- Hands hold the racket pose with the racket hidden on the locker / settings / store tiles (pre-existing).
- Line 4 of the gate: two `Unity/Assets/Scripts/Course/` files (`GolfAtmosphere.cs`, `GolfLook.cs`) differ from the start-of-job hashes. They were edited by the golf postcard-look session working in the same tree (see `GATE_RESULTS.md`, "Line 4"); this job never writes into the shared `Unity/` folder apart from the exporter.
- `work/hero-mainstay/proof/swift_locker/` and `work/hero-dressed/proof/swift/` were rewritten by re-running their (existing) proof tests; the old flat-look stills are gone (untracked, no backup).
- Hair, emotes: not in this pass.
