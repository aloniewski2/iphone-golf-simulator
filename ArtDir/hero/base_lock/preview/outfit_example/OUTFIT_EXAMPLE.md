# Male HeroBase — outfit + expression example (preview only)

Requested by Adnan 2026-10-01: "color and clothe the model so I can see what it would look like in practice… give him facial expression, etc."
This is a **look-dev preview in Blender (EEVEE)**, not a gate item and not a Unity import. It does not touch the locked base.

## Files
| File | What |
|---|---|
| `01_hero_cheer_q3.png` | hero render, Coral Court kit, "Cheer" face |
| `02_turnaround.png` | front · 3/4 · side · back · 3/4-back |
| `03_expressions.png` | Happy · Cheer · Focus · Surprise (front + 3/4) |
| `04_colorways.png` | Coral Court · Sky Doubles · Violet Smash |
| `HeroBase_Male_OutfitExample.blend` | the whole scene: studio, camera, lights, all layers |

## What is on the model (every item is a separate object; `Body_M` is untouched)
* **Skin** – single warm soft-plastic material on `Body_M` (colour only).
* **Face** – eyes, lids, brows, nostrils, mouth/teeth/tongue as thin layers 1–3 mm proud of the head, one collection per expression: `Face_happy`, `Face_cheer`, `Face_focus`, `Face_surprise` (toggle `hide_render` on the collection).
* **Hair** – the project's `Hair_Default_Male.fbx` (hanging side/back locks removed so it reads as a short crop) + a scalp cap that fades into the skin at the hairline. Separate objects in `Hair_Example`.
* **Outfit** (`Outfit_Example`) – T-shirt with rolled collar, sleeve cuffs, chest band and tennis-ball badge; shorts with waistband, hem trim and side stripe; crew socks with a top band; sneakers (upper, toe cap, sole, padded collar, laces); wristbands. Garments are offset shells of the body surface with 4–5 mm real thickness. Stripes/trims are crisp object-space shader masks (no vertex paint, no UVs).
* **Colourways** – only material colours change (`outfit_parts.COLORWAYS`).

## Integrity
* `Body_M` coordinates in this blend are bit-identical to `ArtDir/hero/base_lock/blender/HeroBase_Male_Silhouette.blend` (SHA-256 `35766aea…`, unchanged).
* The scene was built from the authority blend and immediately saved under a new name; the authority file and `HeroBase_Male_Body.fbx` were never written.
* Verification output: `work/male-outfit-preview/final/verify.json`.

## Honest limitations
* **Not game-ready.** Triangle counts are mock-up level: body 49.3k + outfit 84.8k (shirt 29k, shorts 20k, socks 14k, shoes 10k, rest trims) + hair 7.7k + one face ≈ 8–10k ⇒ ~150k. Production garments need retopo/decimation to a mobile budget (rough targets: shirt ≈ 5–6k, shorts ≈ 4k, shoes ≈ 3k each pair, socks ≈ 1.5k, face layers → texture/blendshape).
* **The head is still the blank blockout.** Nose sits low, cheeks/ears are wider than the plate; the face layers add features, they do not reshape the head. The hair-front band reads like a headband from straight ahead.
* **Shoes are chunky slip-on shapes** (foot shell + collar + laces); no tongue.
* Hands are in the locked A-pose, so no racket (needs a grip pose + `Hand_Racket` socket).
* The materials use Blender shader nodes (object-space masks); a Unity version needs those baked to a texture or vertex colours.
* `AGENTS.md` still lists clothes as out of scope; this example was done on Adnan's explicit request and does not change that file.

## Rebuild
`work/male-outfit-preview/`: `build_example.py` (open authority → save as preview → build everything), `render_example.py` (`final_set`), `compose_sheets.py` (these PNGs), `face_build.py`, `outfit.py`, `outfit_parts.py`, `shader_zones.py`, `render_setup.py`, `verify_preview.py`. Run build/render inside Blender (live MCP or `blender -b --python`), then `python3 compose_sheets.py`.
