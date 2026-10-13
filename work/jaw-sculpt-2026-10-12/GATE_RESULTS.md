# Jaw sculpt: checks (2026-10-12)

The light checks Adnan asked for (overlays and close-ups in `proof/`), plus the safety checks that come with changing a skinned head.
Distances are millimetres in the game's own shape: `GamePreview_Body`, the source head with the game's face warp applied.
Profile numbers are the front edge of the side silhouette (more negative = further forward). Width numbers are half-widths in the front
view. The target is the reference sheet scaled to our head by crown and chin height (`eval/final_table.json`; source `jawtools.table`).

| # | Check | Result |
|---|---|---|
| G1 | Code jaw removed: no jaw border, undercut, crest, jowl, `underJaw` band or edge light catch left in `HeroFaceReferenceFit.cs`, `HeroFaceSkin.shader` or `MatchHeroSurfaces.swift` | **PASS** |
| G2 | Same vertex count and order, triangles, weights, materials, bones; eye/brow mesh untouched; blink shape-key deltas identical (re-imported FBX vs the original, `export/VERIFY.json`) | **PASS**. Male 43,900 / female 34,366 vertices; delta error 0.0 mm. |
| G3 | Nothing moves at or below the collar line (lowest collar per angle over the tennis, golf and older match polos) | **PASS**. 0 vertices below it. Lowest moved point: male z 1.389 (collar V 1.377), female 1.376 (1.365). |
| G4 | Only the jaw / upper neck move | **PASS**. Male 1,796 vertices, max 10.7 mm; female 2,200, max 7.3 mm. Above z 1.50 the male fades out (≤ 1.3 mm; ≤ 0.2 mm above 1.52). |
| G5 | No folds | **PASS**. Blender: 0 flipped triangles, none turned > 60°, no new edge sharper than 60° (crease guard). Unity `HeroFaceReferenceFit`: `belowHeadChanges=0`, `minJacobian` .220 (male) / .214 (female), topology, weights and binds the same. |
| G6 | Throat (top of the neck) on the target line | **PASS**. Male z 1.43 / 1.44 / 1.445: −76.3 / −78.3 / −80.7, target −76.3 / −79.7 / −81.4 (before −85.7 / −88.3 / −90.0). Female 1.405–1.425 within 1.7 mm of the target (before up to 6.7 mm forward). |
| G7 | Flat under-jaw shelf reaching back to the neck | **PASS**. Male: the corner at chin-bottom height went from −97.7 to −86.0 (target −84.0). Female: the corner was already at the target and stays there (−45.3 vs −47.0). |
| G8 | Front view: neck narrower than the jaw | **PASS**. Male neck 65.8–66.2 at z 1.46–1.47 (was 75.5–76.5) under a jaw of 74.8–79.0 at 1.49–1.50 (was 76.7–79.5: no overhang before). Female neck 46.5–46.7 at 1.43–1.44 (was 50.2–50.7; target 46.9–47.9). |
| G9 | Chin brought forward | **PASS**. Male lower chin +3.0 mm (−145.7 → −148.7 at z 1.46); female +0.7–1.6 mm. |
| G10 | Lobby re-synced (`work/hero-sync/sync_heroes.sh`) | **PASS**. 63 lobby files re-exported and installed. Self-check: CPU skin vs Unity, max 0.001 mm. Unity body vertex counts unchanged (male 44,168, female 34,443). In the manifests only `headCentreY` changed. Lobby vs game head shots: brightness ratio 1.007–1.029, mean difference 5.3–6.6/255 (`work/hero-sync/sheets/PARITY.md`). |
| G11 | Adnan's eye check against the targets | **PENDING** |

## Honest limits

- **Male chin vs target.** Lined up by crown and chin height, our male lower face (lips and chin) already sat about 8–10 mm further forward than the target's before this pass. The brief asked for the chin forward, so it went 3 mm further forward. What changed visually is the chin standing out from the neck, not its distance from the forehead.
- **Back of the neck.** The target's nape is about 1 cm further forward than ours on both heroes. It wasn't in the brief, and the polo collar sits right there (male back 1.456, female 1.41), so changing it would put a step just above the collar. Not changed.
- **Female lower throat.** At z 1.40 her throat is still 4 mm forward of the target. Below that it meets the collar V, which stays untouched.
- **Shading softness.** At runtime, Unity rebuilds head normals from the geometry over 12 mm (`HeroSkinNormals`, unchanged). The jaw edge shades a little softer in game than in the Blender stills.

---

# Pass 2: checks (jaw corner, crisp in game)

Judged by eye against the targets, as asked: `proof/closeups_<Sex>.png` (Unity, rest pose). The safety checks still apply.

| # | Check | Result |
|---|---|---|
| P1 | Clear jaw corner under / slightly in front of each ear; one clean line from the chin to it, turning up toward the ear | **PASS (my reading)**. Clear in the three-quarter and side close-ups, both heroes; the female is softer than the male, as in the target. |
| P2 | Front view: jaw slightly wider than the neck at the corner, neck tucked in below | **PASS (my reading)** |
| P3 | Edge stays crisp in game (no 12 mm blur) | **PASS (my reading)**. `HeroSkinNormals` radius is 5.5 mm under the jawline and on the neck; the rest of the face keeps 12 mm. No shading noise in the Unity close-ups. |
| P4 | No painted shadow, no code warp for the jaw | **PASS**. Only geometry (Blender) and a normal-averaging radius changed. |
| P5 | Same vertex count and order, triangles, weights, bones, blink deltas; eyes/brows untouched; nothing at or below the collar moved | **PASS** (`export/VERIFY.json`). Male 2,134 vertices moved (max 10.7 mm), female 2,556 (max 9.0 mm). 0 below the collar. |
| P6 | No folds or creases | **PASS**. 0 flipped triangles; no edge newly past 60°; Unity `HeroFaceReferenceFit`: belowHeadChanges 0, minJacobian .220 / .214. |
| P7 | Ears untouched | **PASS**. The auricle and 2 rings around it are held exactly. |
| P8 | Lobby re-synced | **PASS**. 64 files. Self-check: max 0.001 mm. Unity vertex counts unchanged (44,168 / 34,443). Lobby vs game head shots: brightness ratio 0.968–1.029, mean difference 5.3–6.6/255. |
| P9 | Adnan's eye check | **PENDING** |

---

# Pass 3: checks (smooth, sharp jawline; jaw not moved)

Judged by eye, as asked: `proof/pass3_<Sex>.png` (TARGET / PASS 2 / NOW: front, three-quarter, side) and `proof/pass3_jawline.png` (the jawline alone, three-quarter, PASS 2 vs NOW). All in-game Unity stills, rest pose. The safety checks still apply.

| # | Check | Result |
|---|---|---|
| Q1 | The jawline is one clean continuous curve from the chin to the corner under the ear, with no bumps or dips (side and three-quarter) | **PASS (my reading)**. Pass 2's kinks (male near the corner, female mid-jaw) are gone in the jawline close-up. Both lines run as one curve and turn up toward the ear. |
| Q2 | Jaw not moved; sharp turn kept | **PASS**. Side profile identical to pass 2; front widths within 0.6 mm (`eval/final_table_pass3.json`). The corner and lines (`jl_*`) are unchanged. Smoothing is off within about 1 cm of the corner, and the across-line profile is kept. |
| Q3 | Vertices along the line evenly spaced, smoothed along the line, not across it | **PASS**. A mesh-edge row is laid on the line (half-even spacing, slides ≤ 4 mm, on the original surface; three rings relaxed). Along-line-only relax (cos⁴ weights, 14 Taubin passes) and an along-line depth fit (8 mm along / 1.2 mm across). |
| Q4 | Normal radius about 8 mm under the jaw, blending into 12 mm with no visible border | **PASS (my reading)**. `HeroSkinNormals`: 8 mm under the jawline, easing to 12 mm over about 36 mm; no border visible in the close-ups. |
| Q5 | Same vertex count and order, triangles, weights, bones, blink deltas; eyes untouched; nothing at or below the collar | **PASS** (`export/VERIFY.json`). Male 2,133 vertices moved (max 10.7 mm), female 2,551 (max 11.0 mm, including slides of up to 4 mm). 0 below the collar. |
| Q6 | No folds or new creases | **PASS**. 0 flipped triangles. No edge newly past 60° against the untouched source. Unity: belowHeadChanges 0, minJacobian .220 / .214. |
| Q7 | No painted shadow, no code warp | **PASS** |
| Q8 | Lobby re-synced | **PASS**. `jaw_sync.sh` exported the staged state, which now includes the other session's staged clothing-V2 male polo, plus this pass. The result is byte-identical to the installed lobby files. Self-check max 0.001 mm. Unity vertex counts unchanged (44,168 / 34,443). Head shots: brightness ratio 0.968–1.029, mean difference 3.9–6.1/255. Provenance hashes the 48 staged look sources; none has an unstaged edit. |
| Q9 | Adnan's eye check | **PENDING** |
