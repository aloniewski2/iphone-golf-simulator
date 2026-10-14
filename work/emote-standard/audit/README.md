# Clipping audit tooling (2026-10-13)

Output: `../CLIPPING_AUDIT.md`, `../CLIPPING_AUDIT.json`, `../CLIPPING_AUDIT_images/`. Audit only; nothing was fixed or installed.

* `tools/AnimationAudit.cs` + `AnimationAuditEmote.cs`: the earlier breakdown tool (`work/anim-standard`) made a `partial` and extended. They go into the Unity APFS clone under `Assets/Editor/` (never the real project).
  * `AA_MODE=emote`: per clip 15 fps renders (4 views, ID pass, hands, face), hand / racket / garment-pair hints, 30 fps shoe contact data.
  * `AA_MODE=closeup AA_JOBS=jobs.json`: zoomed stills of each problem (+ body-area label).
  * `AA_MODE=face`: face sweep.
  * Run: `tools/run_unity_clone.sh <outdir> AA_MODE=emote AA_CONTEXTS=tennis_male AA_CLIPS=Emote,Walk,Run` (edit the clone path inside).
* `tools/analyze.py` (episodes + jobs), `tools/build_doc.py` + `assemble.py` (stills + markdown), `facesheets.py`, `pick.py`, `sheet.py` (review sheets).
* `episodes.json`, `jobs.json`, `coverage.json`: every detected episode, before merging by area. `data/`: per-clip hand/racket/garment hints and shoe data (the raw frames, ~700 MB, were left in the scratchpad).
* Detector filters added after looking at stills: no see-through holes (all were background between the legs), no sock/shoe or golf polo/belt overlaps, no male-shorts leg-opening lining.
