# Hero sync: lobby hero = in-game hero

The lobby, locker, menus and loading screen (SwiftUI + SceneKit) never run Unity. They draw an **exported copy** of the Unity heroes:
`GolfArcade/Unity/CharacterAssets/MatchHero_*` (tennis and its `_Distance` variant, which the lobby uses) and `GolfKitHero_*` (golf),
rendered by `GolfArcade/Unity/MatchHeroSurfaces.swift`. Whenever a hero changes in Unity, that copy goes stale. That happened on 2026-10-11:
the lobby kept the old face, ears, shoes and grip.

## The one step

```bash
work/hero-sync/sync_heroes.sh
```

Run it after **any** change to how a hero looks. `look_sources.txt` lists the files that count: mesh shaping, grip, skin, kit, shaders, the
prefabs, the head FBXs and the exporter. Unity runs muted (`GOLF_MUTE=1`) in batch mode, on an APFS clone (`HERO_SYNC_CLONE`, default
`$TMPDIR/hero-sync-UnityClone`), never on the shared project. Nothing is committed.

1. Refresh the clone from `Unity/` (Assets, Packages, ProjectSettings).
2. Export with `MatchHeroLockerExport` (`Run`, `RunDistance`, `RunGolf`). The export samples the runtime heroes after `MatchHeroLook.OwnMaterials`, so it carries:
   - the face warp, ears, shoe shape, racket seat and skirt fit;
   - the grip, which `HeroGripPolish.ApplyGrip` applies to every sampled pose;
   - the material numbers.
3. Install every changed file into `GolfArcade/Unity/CharacterAssets/`. It warns if a garment `.png` changed while its `.ktx` did not.
4. Pack the Unity goldens into `GolfArcadeTests/VisualGolden/`. Rewrite `provenance.json` with the SHA-256 of every file in `look_sources.txt`.
5. Draw lobby-vs-game sheets into `sheets/`:
   - **Game:** Unity draws each hero (`MatchHeroLockerExport.CaptureLobbyParity`, graphics on).
   - **Lobby:** the app's own SceneKit code draws the installed export (`lobby_render.swift`), with the same pose, cameras and lights.
   - Output: `PARITY.md` lists the colour and difference numbers for every shot.

   Use `--no-compare` to skip this step (no graphics needed). Use `--compare-only` to redo only this step, e.g. after editing `MatchHeroSurfaces.swift`.
   To check one lighting term at a time, set `LP_RIG=key` (key light only) for both renderers. Don't use `LP_RIG=ambient` for SceneKit: with no direct light, SceneKit draws the diffuse colour unlit.

   The two engines share light and camera numbers, but Unity is left-handed and SceneKit right-handed. The capture mirrors x so both see the same side lit. Unity's flat ambient colour is gamma, so the capture converts it.

## The guard

`VisualOverhaulNativeTests.testNativeHeroesMatchTheCurrentUnityHeroLook` re-hashes the files in `look_sources.txt` and fails if any of them
changed since the last sync. Running the script fixes it. `testNativeHeroesAreExportedFromTheCurrentUnityHeads` still guards the head FBXs on
their own.

## What the script cannot do

SceneKit cannot run Unity's shaders, so `MatchHeroSurfaces.swift` ports them by hand:

| Unity | SceneKit port |
|---|---|
| `HeroFaceSkin` | `faceSkinBody`, `faceSkinLighting` |
| `TennisCloth` kit edge lines | `kitEdgeGeometry`, `kitEdgeFragment` |
| `TennisCloth` garment | `garmentSurface`, `garmentLighting` |

When one of those Unity shaders changes, the guard fails. Update the Swift port, run the script, and check the sheets.
