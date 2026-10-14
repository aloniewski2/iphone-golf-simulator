# HANDOFF — Clothing V2: finish every hero's clothes (written 2026-10-13)

Start here in a new chat. Read this file, then `work/clothing-v2/README.md`, then `work/clothing-v2/py/polo2.py` (the working example).

## 1. The job
Adnan: **"finish all, proceed"**. Rebuild every hero garment as a real, separate garment matching the "after" targets in
`ArtDir/hero/targets-2026-10-12/clothing-v2/` (01 tennis male, 02 tennis female, 03 golf male, 04 golf female, each `NN_*_target.jpg`
plus `NN_*_before_after.png`). Copy only the clothes. Keep body, face, skin, rig and animations untouched.
**Stage only: no commit, push, stash or checkout.**

### Adnan's process rules (binding)
- Do **not** derive a garment from the body surface pushed outward ("bigger skin", rejected 2026-10-12). "Think about how a medium
  shirt fits on multiple people": the garment has its own shape; the body only decides where it must be moved out to clear the skin.
- Per garment: (1) build it in Blender as its own product (separate panels, real construction: collar stand + fall, placket + buttons,
  cuffs, hems with 2–4 mm thickness, waistbands, belt loops, pleats with depth, clean edges) and look at it alone from all sides;
  (2) fit it to the bald base in T-pose with a small gap, loose, not hugging muscles, a few soft folds; (3) copy weights from the body,
  check poses with no skin poking through, keep the same slot/socket so it stays swappable and colour picks still work.
- Fabric: piqué on polos, woven on shorts/trousers, rib on cuffs/waistbands. Soft contact shadow where fabric meets skin.
- Shoes: real tennis/golf shoes (cushioned sole, laces, tongue, tread; golf = spikes/nubs on the outsole, perforated leather look).
- Clothing colour is customisable: the target hues are examples, not a fidelity gate. Keep palette controls / saved picks working.
- Show Adnan real images as you go (plate/target vs now) about every 15–20 min; give honest numbers, say what failed.

## 2. Status (updated 2026-10-13 evening)
All four heroes now wear V2 garments, installed in the Unity repo (staged, not committed) and rendered in the game shaders (`ArtDir/hero/clothing-v2-review/all_heroes_progress_4.png`).
| Hero | Top | Bottom | Socks | Shoes | Extras |
|---|---|---|---|---|---|
| MatchHero_Male (01) | polo V2 (+ follow-ups: narrower collar, shorter placket, longer sleeves) | navy tailored shorts | crew socks | plain white tennis shoes | – |
| MatchHero_Female (02) | racerback V-neck tank | pleated skirt + navy under-shorts | ankle socks + pom | plain white tennis shoes | – |
| GolfKitHero_Male (03) | royal-blue polo (3 buttons, plain) | navy flat-front trousers (crease, loops, pockets) | – | plain white golf shoes, cleats | leather belt + buckle, white glove |
| GolfKitHero_Female (04) | royal-blue fitted polo (lofted sleeves) | cream pleated skort + under-shorts | – | plain white golf shoes, cleats | belt, white glove |

Builders / run scripts: see README.md "2026-10-13 — every hero". `work/clothing-v2/install_to_repo.sh` copies what the Unity clone installed into the repo; `update_look_sources.py` + `lobby/compress_maps.py` + `work/hero-sync/sync_heroes.sh` do the lobby side; `make_share.sh` makes the zips in `share/`.

Known weak spots (honest list): shoes are plain, chunky and not the target's sporty shoe; golf female sleeves are a short cap sleeve (arms hang almost straight) with a visible seam crease; tennis female tank has no tipping/ribbing detail beyond the binding; golf trousers still a little wide at the shoe; male polo armpit poke only reduced (1363 -> ~1256 worst-frame points over 33 clips); tank/skirt pose pokes are mostly gone (tank 325, skirt 223 worst-frame points); golf correctives (pose-driven cloth lift) exist for the tennis tops only.

## 3. How the polo was made (copy this pattern)
All in `work/clothing-v2/` (Blender 5.2 at `/Applications/Blender.app/Contents/MacOS/Blender`, its python has numpy;
system python3 has PIL).

| Step | File | Notes |
|---|---|---|
| body data | `dump/<Hero>/` | bind-pose body (pos/nrm/tri/uv/bone idx+weights), old kit pieces, bone + body matrices for every clip. Read with `py/cv2io.py` (`cv2io.Hero(id)`). Made by `unity/ClothingV2Dump.cs` (copy into `<clone>/Assets/Editor/ClothingV2/`, run `-executeMethod GolfArcade.EditorTools.ClothingV2Dump.Run` with `CV2_OUT=<abs>/work/clothing-v2/dump`, `VISUAL_GARMENT_COVERAGE=0`, `GOLF_MUTE=1`). **Re-dump if the body changed** (the jaw-sculpt chat changed the head on 10-12 after the dump; garments below the neck are unaffected, collars should be checked). |
| build | `py/polo2.py` | `design_male()` = numbers (rings H/L/W/A/M/S/N, sleeve, collar, placket, cuff, hem, vent); `build_cage` (16-around rings + 8-vert sleeve rings along the arm axis) → SUBSURF 3; UVs unwrapped on the cage with seams on panel lines; construction parts as separate meshes; Solidify with `material_offset` 1 (inner) / `material_offset_rim` 2 (rim) to classify faces, inner wall trimmed >6 cm from openings, bevel; joined into `Kit_Top`. Per-vertex fields (`d_seam`, `d_hem`, `d_cuff`, `collar_edge`, `pk_x/pk_z`) drive the map details. |
| fit | `py/fit.py` | `push_clear_multi`: moves cloth out only where clearance < 6 mm (skin) or inside another garment (radial push from the torso axis, `MeshSurface.radial_need`). Smooth field, no body shape copied. |
| export | `py/polo2_export.py` | packs UVs, triangulates, part per face (outer/rim/inner), splits by UV, weights = nearest skin (`finalize.restricted_weights`), sleeves limited to their arm bones (Shoulder/UpperArm + ArmBlend_*), smoothed (`CV2_WSMOOTH=8`). Writes `out/<Hero>/unity/Kit_Top_final.npz`, `Kit_Top.bin`, **`manifest.json` (it OVERWRITES the manifest with only Kit_Top — new garment exports must MERGE their piece into the existing manifest)**, and `polo/polo_v2_final.blend`. |
| skin cover + contact shadow | `py/cover.py` | hides skin that is covered in every pose (`_hide.bytes`), unhides anything a clip exposes, writes the contact shadow (`_contact.bytes`, body UV3.x in Unity). |
| pose correctives | `py/correctives.py` | 24 blend shapes `CV2_PoseNN` (lift-only: `CV2_CORR_MODE=lift`, normals zero), driven at runtime by `KitV2PoseCorrectives`. |
| maps | `py/maps.py` (`details_polo2`), `py/atlas.py` | N map + M map (R AO, G cavity, B trim mix, A fabric id/9; `_FabricVersion` 2). Shared atlas `out/KitV2_FabricAtlas.png`: twill (0,0), piqué (1,0), leather (0,1), rib (1,1). |
| one command | `polo/run_polo.sh` | build → export → cover → correctives → cover → maps. Make one `run_<garment>.sh` like it per new garment. |
| checks | `py/look.py` (studio renders alone / on body, `CV2_MAPS`), `py/posed.py` (`CV2_MARK=1` red = poke), `py/poke.py` + `py/pokesum.py` (poke points per clip), `py/cross2.py`, `py/review_polo.py` (review sheets), `compare/compare_ref.py` (target vs Unity render, measured landmarks) |

`run.sh`, `py/tops.py`, `bottoms.py`, `shoes.py`, `designs.py`, `build.py`, `finalize.py` (except helper functions it exports) are the
REJECTED body-derived pipeline. `run.sh` now exits immediately. Their old outputs are parked in `rejected_bodyderived/` — never install.

### Spaces / conventions (cost hours before)
- Body mesh space: metres, Z up, hero faces −Y, hero's right = +X. Unity import mirrors X. Dump matrices are column-major (transpose).
- Offline LBS == Unity Bone4 skinning (verified 0.0 mm), so offline poke tests are trustworthy.
- `ArmBlend_*` bones exist only at runtime (`HeroArmSkinning`); installer rebinds them by name (`kitV2Bones`).
- Body has tiny holes at the back shoulders (x ±.237, z 1.305): `glib.fill_tiny_holes` before ray tests.

## 4. Unity + app integration (already in place, staged)
- `Unity/Assets/Editor/ClothingV2Install.cs`: reads `out/<Hero>/unity/manifest.json` + `<Piece>.bin`, writes
  `Assets/Characters/KitsV2/<Hero>/<Piece>.asset` (mesh name `KitV2 <Piece>`), maps into `Resources/<Tennis|Golf>/KitsV2/<Sex>/`,
  replaces ONLY the manifest's pieces on the prefab at the same slot (heroes/prefabs: `Resources/Tennis/Customization/PlayerMale`,
  `PlayerFemale`, `PlayerMaleGolfKit`, `PlayerFemaleGolfKit`). Env: `CV2_SRC`, `CV2_HEROES` (always pass it).
- Slots: tennis `Kit_Top, Kit_Bottom, Kit_Sock_L/R, Kit_Shoe_L/R`; golf `Kit_Top, Kit_Bottom, Kit_Shoe_L/R, Kit_Glove_L/R` (+ `Kit_Head`,
  leave it). Belt: installer supports a new `Kit_Belt` piece. Material roles: `Kit_Shirt, Kit_Shorts, Kit_ShortsBand, Kit_Sock,
  Kit_Shoe, Kit_Sole, Kit_Belt, Kit_Buckle, Kit_Glove` (colour picks key off these roles — keep them).
- Runtime: `MatchHeroLook.IsKitV2(mesh)` (mesh name starts "KitV2") makes the old repairs skip that piece (tailored polo, coverage,
  tailoring, LOD swap, FinishFootwear, distance export); `ApplyKitV2Cover()` = hide + contact shadow (`_KitContact` .55 in
  TennisCharacter/HeroFaceSkin shaders); `KitV2PoseCorrectives` loads `Resources/<Sport>/KitsV2/<Sex>/KitV2_<Sport>_<Sex>_Top_correctives`
  (Top only today — extend it per piece if bottoms need correctives). Also touched: `HeroGarmentLOD.cs`, `HeroGarmentTailoring.cs`,
  `HeroGolfer.cs`, `MatchHeroLockerExport.cs`, `HeroTurnaround.cs` (`TURNAROUND_LOOK=target` = target hues for proofs only).
- **Never run batch Unity on the shared project**: use the APFS clone. `unity_iter.sh <outdir>` rsyncs the repo into
  `$TMPDIR/cv2-UnityClone` (keeps the clone's KitsV2/prefabs), runs the installer, renders the turnaround. Then copy
  `Characters/KitsV2`, `Resources/<Sport>/KitsV2`, the changed prefab(s) back into the repo. Always `GOLF_MUTE=1`.
- Proof renders: `UNITY_PROJECT=$TMPDIR/cv2-UnityClone TURNAROUND_OUT=<dir> TURNAROUND_HEROES=<Hero> TURNAROUND_LOOK=target work/model-turnaround/run.sh`.

### Lobby (iPhone SceneKit copies) — must match the game
1. `python3 work/clothing-v2/lobby/compress_maps.py` (makes the .ktx the Swift garment shader samples; without them the app hits
   `fatalError("Incomplete garment maps")`). It converts every `KitV2_*.png` under `Resources/Tennis/KitsV2` — extend it to
   `Resources/Golf/KitsV2` when golf pieces exist.
2. Append new look files (meshes, maps, hide/contact/correctives) to `work/hero-sync/look_sources.txt`.
3. `HERO_SYNC_CLONE=$TMPDIR/cv2-hero-sync-clone work/hero-sync/sync_heroes.sh` → check `work/hero-sync/sheets/` + `PARITY.md`.
4. Other chats also run sync_heroes.sh; their outputs land in the same files. Stage only what you produced.

### Tests
- Unity EditMode (in the clone): baseline 481/489, the 8 failures pre-exist (6 MultiplayerRulesTests golf, HeroAssetRegistryTests,
  NetworkViewTests on clone paths).
- Native hero tests (`VisualOverhaulNativeTests`, `MatchHeroLockerTests`, `LockerMirrorTests`): 14 fail before any clothing work
  (compare against a baseline: `git ls-files -z GolfArcade GolfArcadeTests GolfArcadeUITests project.yml | xargs -0 git checkout-index --prefix=<tmp>/`).
  Run from a scratch xcodegen project: copy `project.yml`, make `CODE_SIGN_ENTITLEMENTS`, `INFOPLIST_FILE`, `SWIFT_OBJC_BRIDGING_HEADER`
  absolute, `xcodegen generate --spec <tmp>/project.yml --project-root <repo> --project <tmp>`, then `xcodebuild test ... -only-testing:...`
  on the booted iPhone 17 Pro simulator. A hero wearing a V2 garment has 2 extra body texcoords (contact shadow); two tests were
  updated for that rule.

## 5. Files to hand out
`work/clothing-v2/share/Male_Polo_V2.zip` (blends, FBX, Unity asset, maps, lobby copies). Make one zip per finished hero the same way.

## 6. Constraints
Stage only. Don't touch body/face/skin/rig/animation files, maps, lights, cameras. Parallel chats are active (jaw sculpt, swing,
emotes, golf UI): stage files by explicit path, never `git add -A`. Disk: the two Unity clones in `$TMPDIR` are ~31 GB.
