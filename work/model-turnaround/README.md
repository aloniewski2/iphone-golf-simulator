# Model turnaround (viewing / capture only)

Studio renders of the current heroes from every angle. Nothing in the game changes: no character, garment, rig, animation or asset is edited or saved.

## Run it
* **Unity menu:** `Tools > Hero Turnaround > Capture All Heroes` (asks to save the open scene, reopens it when done).
* **Shell (editor closed on the project):** `work/model-turnaround/run.sh`
  * one hero: `work/model-turnaround/run.sh MatchHero_Female` (comma-separated list works)
  * no spin videos: `TURNAROUND_VIDEO=0 work/model-turnaround/run.sh`
  * from a project copy: `UNITY_PROJECT=<path> work/model-turnaround/run.sh`
  * about 50 s for all four heroes; log in `logs/turnaround.log`, summary in `report.txt`.

Script: `Unity/Assets/Editor/HeroTurnaround.cs` (`GolfArcade.EditorTools.HeroTurnaround.RunBatch`). Needs graphics (no `-nographics`).

## What it renders
Heroes: `MatchHero_Male`, `MatchHero_Female` (tennis kit + racket), `GolfKitHero_Male`, `GolfKitHero_Female` (golf kit + iron).

| | |
|---|---|
| Poses | **Ready** (tennis `*_ReadyIdle` t=0; golf `Idle` t=0 with the iron in the menu grip) and **Rest** (the body's bind pose: T-ish for the males, arms down for the females; equipment hidden) |
| Full body | front, front-left, left, back-left, back, back-right, right, front-right (hero turns on a turntable, fixed camera and lights), top (60° above), low (camera at the feet) |
| Close-ups | face front / three-quarter / side; left + right hand front / outside; shoes front / left / back / right; shirt front / back; shorts, skirt or trousers front / back |
| Video | `<hero>/<hero>_spin.mp4`, 10 s, 30 fps, 1080x1440, ready stance, 360° |

Per hero: 50 PNGs (`<pose>_<shot>.png`, full body 1080x1440, close-ups 1080x1080, 8x MSAA) in `<hero>/`, plus `<hero>_contact_sheet.png` (here and inside the folder).
"front-left" = camera at the hero's own front-left.

## Look
* Hero setup copies `MatchHeroLockerExport` (prefab / `HeroGolfer.Build`, `MatchHeroLook.OwnMaterials`, racket scale, grip + garment correctives), so the materials are the game's `TennisCharacter` / `HeroFaceSkin` / `TennisCloth`.
* Colours = the "male/female UNITY" columns of `work/lobby-parity/proof/bodies_unity_vs_lobby.png` (CaptureLobbyParity case s3: skin B7744A, shirt 2F6FD6, shorts/skirt F4F0E6, shoes FFFFFF), applied to all four heroes.
* `VISUAL_CHARACTER_SKIN_POLISH` and `VISUAL_BLINK_NORMAL_FIELD` are forced off for the run.
* Shader globals: `HeroLightingProfile.Tennis()` for tennis heroes; `HeroLightingProfile.Golf(...)` fed with the studio light for golf heroes (what a course sets).
* Studio: grey background, warm soft-shadow key (camera-left, 38° up), cool fill (camera-right), faint back light, trilight ambient, no post-processing. The posed meshes are baked to static meshes (Unity `BakeMesh`) before rendering.
