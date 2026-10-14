# Loaded for the animation video (fresh copy made 2026-10-13 13:48 from the live project files)

Source: the real project working tree on branch `10/13` (HEAD c377e360, no uncommitted changes under `Unity/Assets`). Unity ran in a new APFS clone of it made this session; heroes come from the live prefabs / `HeroGolfer.Build`, not from old FBX exports.

Newest changes in the project: **2026-10-12 16:11** (`HeroSkinNormals.cs`, jaw; heads `OriginalSeamBlinkFinish/*.fbx`), clothing **2026-10-12 15:50** (male polo).

| Hero | Piece | File | Last modified | vs newest change |
|---|---|---|---|---|
| tennis male | prefab (live) | `Assets/Resources/Tennis/Customization/PlayerMale.prefab` | 2026-10-12 20:23 | current |
| tennis male | body + head (jaw), what the game draws | `Assets/Resources/Tennis/OriginalSeamBlinkFinish/Male.fbx` | 2026-10-12 16:11 | current |
| tennis male | jaw skin normals code | `Assets/Scripts/Tennis/HeroSkinNormals.cs` | 2026-10-12 16:11 | current |
| tennis male | polo Kit_Top (Clothing V2) | `Assets/Characters/KitsV2/MatchHero_Male/Kit_Top.asset` | 2026-10-12 15:50 | **OLDER** |
| tennis male | polo maps / correctives | `Assets/Resources/Tennis/KitsV2/Male/KitV2_Tennis_Male_Top_correctives.json` | 2026-10-12 15:49 | current (this IS the newest clothing change) |
| tennis male | shorts, socks, shoes (old kit) | `Assets/Characters/MatchHeroKit/Male_Kit.fbx` | 2026-10-05 16:21 | **OLDER** |
| tennis female | prefab (live) | `Assets/Resources/Tennis/Customization/PlayerFemale.prefab` | 2026-10-12 20:23 | current |
| tennis female | body + head (jaw) | `Assets/Resources/Tennis/OriginalSeamBlinkFinish/Female.fbx` | 2026-10-12 16:11 | current |
| tennis female | polo, skirt, socks, shoes (old kit) | `Assets/Characters/MatchHeroKit/Female_Kit.fbx` | 2026-10-05 15:08 | **OLDER** |
| tennis female | polo runtime tailoring | `Assets/Scripts/Tennis/TailoredPoloDeformation.cs` | 2026-10-11 11:21 | runtime code, not a garment file |
| golf male | body, face, polo, trousers, glove, shoes (one FBX) | `Assets/Resources/Hero/golfer_m.fbx` | 2026-10-12 20:47 | current |
| golf female | body, face, polo, skirt/shorts, glove, shoes (one FBX) | `Assets/Resources/Hero/golfer_f.fbx` | 2026-10-12 20:47 | current |

Rest-pose stills of all four hero/kit combinations: `rest_poses.jpg` (full body + head each), originals in `stills/`.

**Stop:** the female polo, skirt, socks and shoes (`Female_Kit.fbx`, 10-05 15:08) and the male shorts, socks and shoes (`Male_Kit.fbx`, 10-05 16:21) are older than the newest clothing change (male polo, 10-12 15:50). Heads, bodies, male polo and golf figures are current. The video was not made.
