# Jaw sculpt, 2026-10-12

Brief (Adnan): the jaw still didn't match the reference heads in `ArtDir/hero/targets-2026-10-12/bald-base/`. Stop shaping the jaw with code.
Sculpt it in Blender on the male and female head meshes, with the reference front and side heads behind the model, lined up to our head size.
Match the reference profile: chin forward, a clear flat surface under the jaw from the chin back to under the ear, and the top of the neck
tucked back and narrower than the jaw. Change only the head and face; keep the vertex count and order. Then export and re-sync the lobby.
Stage only.

## What changed

| | |
|---|---|
| **Removed (code)** | The jawline block in `HeroFaceReferenceFit.Warp` (border, undercut, crest, jowl lean). The jaw shadow band and light catch in `HeroFaceSkin.shader` (and the `underJaw` term in its form shading). The same band in the lobby port, `MatchHeroSurfaces.swift`. The rest of the face warp (eyes, nose, ears, mouth) and the skin-pass shading are unchanged. |
| **Sculpted (Blender)** | `Body` of both heads, starting from the files the game used: `work/reference-rebuild/face-finish30/composed-ears/<Sex>_OriginalFaceFinish30ComposedEars.blend`. Results: `Male_JawSculpt.blend`, `Female_JawSculpt.blend` (this folder). |
| **Installed** | `Unity/Assets/Resources/Tennis/OriginalSeamBlinkFinish/Male.fbx` and `Female.fbx`. Same export settings as the original build; the `.meta` files (GUIDs, import settings) are untouched. |
| **Lobby** | Re-synced with `work/hero-sync/sync_heroes.sh`. |

### The sculpt, in plain words

- **Under the jaw:** the underside of the jaw is flattened into one surface from the chin back to the jaw angle. The rounded corner where the jaw ran into the neck has gone. The jaw border is now a clear line.
- **Neck:** the top of the neck moves in under the jaw. Above the collar, the throat goes back about 1 cm (male) / 0.5–1 cm (female), and the sides of the neck come in about 0.9 cm (male) / 0.4 cm (female). The jaw now overhangs the neck in the front and side views. Every neck change fades to exactly zero at the collar line, measured on the tennis, golf and older match polos, using the lowest collar at each angle.
- **Chin:** the lower chin moves forward about 3 mm (male) / 1.5 mm (female). It stops short of the lower lip.
- **Untouched:** everything else: body, neck below the collar, ears, eyes, brows, lips, nose, cranium, rig, weights, blink shapes, clothes and animations.

### How it was done

`sculpt.py` (run inside Blender) does brush-style moves on the welded surface, in rings around the neck axis:

1. It traces the jaw's real border on the mesh (where the surface turns from facing sideways to facing down) and the true underside.
2. The flatten, tuck and chin moves are smooth fields. The change itself, not the shape, is then smoothed over the surface (Taubin smoothing, so the amount is kept).
3. The authored custom normals are kept and turned with the new surface, so the head shades as before away from the jaw.
4. A crease guard follows. Wherever the change makes an edge sharper than 58° that wasn't before, the change is smoothed a little more right there. Unity splits vertices at edges past its 60° smoothing angle; the first export got 3 extra female vertices in Unity that way, from a small crease where the chin push met the lip guard.

The game still runs its face warp on top: eyes 10 mm lower, male face ×1.085 wide, and so on. So `jawtools.game_warp` (a numpy port of `HeroFaceReferenceFit.Warp`) builds a `GamePreview_*` copy in each blend, and every check is made on that copy: what the game draws, not the raw mesh.

The reference images sit in each blend as `Ref_<Sex>_Front` / `Ref_<Sex>_Side` image empties. They show only in the front (numpad 1) and right (numpad 3) orthographic views. They are lined up by crown height and chin height, which the game's face warp doesn't move; the side one is also shifted to fit the cranium outline. The raw `Body` therefore shows its eyes 10 mm higher than in the game; the hidden `GamePreview_Body` shows the game's face.

## Re-run

```bash
# inside Blender (MCP or GUI) with <Sex>_JawSculpt.blend open:
#   import sys; sys.path.insert(0, '<this folder>'); import jawtools as J, sculpt as SC, json
#   SC.run('Male', json.load(open(J.WORK + '/params_male.json')), 'mX')   # resets from source_<Sex>.npz, sculpts, previews, writes eval/
/Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup --python export_heads.py   # -> export/<Sex>.fbx
/Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup --python verify_fbx.py     # -> export/VERIFY.json
python3 make_proof.py                                                                           # -> proof/
work/hero-sync/sync_heroes.sh
```

`source_<Sex>.npz` / `source_<Sex>_normals.npy` hold the untouched basis, the shape keys and the authored normals, so every run starts from the original.

## Proof

- `proof/profile_overlay_<Sex>.png`: our side profile (game shape, orthographic) outlined on the target side profile. Green = target, yellow = before, red = now.
- `proof/closeups_<Sex>.png`: TARGET / BEFORE / NOW front, three-quarter and side close-ups. BEFORE and NOW are Unity turnaround stills (rest pose). BEFORE = `work/bald-base-2026-10-12/jaw_before` (the head before today's code jaw); NOW = `unity/` (this run). The three jaw framings were added to a clone copy of `HeroTurnaround.cs` only.
- `eval/`: Blender stills and silhouettes (`m0`/`f0` = before, `m18`/`f10` = final).
- Checks: `GATE_RESULTS.md`.

---

# Pass 2 (same day): a real jaw corner, crisp in game

Brief (Adnan): the side profile improved, but in the front and three-quarter views the jaw still looked soft.
- Build a clear jaw corner just below and slightly in front of each ear. The jawline should run as one clean line from the chin back to that corner, then turn up toward the ear. Push further if needed.
- In the front view, the jaw should be slightly wider than the neck at the corner.
- Fix the in-game softening from `HeroSkinNormals`' 12 mm normal smoothing.
- No painted shadows and no code warp. Judge it by eye.

| | |
|---|---|
| **Where the corner goes** | The target's side view, lined up to our head, puts the corner right under the ear lobe, about 2 cm below it, with the line from the chin rising about 25–30°. The game's face warp lifts and shrinks the ears (male lobe about 1 cm higher in game, female about 2 cm). So in the source mesh, the corners sit at male y −33 mm / z 1.506 and female y −8 / z 1.474. Pass 1's border turned about 2.5 cm in front of that and 3–6 cm lower. |
| **Sculpt (Blender, `sculpt.carve`)** | A second stage on top of pass 1: (1) Everything below the line chin → corner, and behind the line corner → lobe, is set in toward the neck axis, on the side of the head only (not the chin centre or the nape). The male goes in up to 9 mm below the line and 6 mm behind it; the female 7.5 / 5 mm. So the jaw becomes one edge that turns up toward the ear. (2) A 1.2–1.5 mm outward bump at the corner, so the jaw is wider than the neck there. (3) A light crest along the edge. (4) A light smoothing brush along the jaw band, so the mesh's fine surface noise doesn't show on the sharper edge. This stage gets only light smoothing, so the edge stays crisp. |
| **Ears untouched** | The auricle (the face warp's own ear ellipse, past the ear root) and 2 rings around it are held exactly. The ears move at most 0.25 mm before that hold; with it, not at all. |
| **No creases** | The crease guard now also catches edges that were already near 58° and got sharper. In the final meshes no edge crosses 60° that didn't before, so Unity gets the same vertex count as before. |
| **Crisp in game (`HeroSkinNormals.cs`)** | Below the jawline (chin → corner, rising about 27°, then up the back of the jaw toward the lobe) and on the neck, the area-weighted normal radius drops from 12 mm to 5.5 mm. The rest of the face keeps 12 mm. The mask is in the head-bone frame, like the file's existing mouth, nose and ear masks; one line fits both heroes within about 3 mm. The lobby gets the same normals, because the export samples the runtime mesh. |

Proof: `proof/pass2_<Sex>.png` (TARGET / PASS 1 / NOW by view, Unity), `proof/closeups_<Sex>.png` (TARGET / BEFORE / PASS 1 / NOW, Unity) and `proof/profile_overlay_<Sex>.png`. Unity stills are in `unity_p2/`.

---

# Pass 3 (same day): a smooth, sharp jawline

Brief (Adnan): the jaw is in the right place now, so don't move it. But the edge looks jagged and lumpy; in the side and three-quarter views the line from the chin to the corner wobbles.
- Make it one clean curve.
- Relax and smooth the vertices along the line and right around it, so they're evenly spaced. Smooth along the line, not across it, and keep the sharp turn.
- Ease the 5.5 mm normal radius to about 8 mm, blended gradually into the face's 12 mm.

**Why it wobbled.** The mesh at the jaw has edges about 3 mm long (male median 3.4, female 2.9), and pass 2 turned the edge over 5 mm. The turn sat across one or two triangles that cross the line at an angle, so the shading stair-stepped. Along the line, the depth went up and down by about 1 mm every centimetre. No amount of smoothing removes that staircase on its own.

| Step (in `sculpt.py`) | What it does |
|---|---|
| `align_chain`: stage 0, before any shaping | On each side, the row of mesh edges that best follows the line from the chin to the corner, and on up toward the lobe (shortest path that stays within 6 mm of the line). Its vertices slide within the original surface onto the line, about halfway to even spacing; no slide is longer than 4 mm. The three rows either side are relaxed within the surface. Nothing changes shape here; only where the vertices sit on the surface. The carve then turns right at this row (`carve_w` 5 → 2.5 mm), so the edge runs along mesh edges. |
| `line_relax` | 14 Taubin passes in a 1 cm band either side of the line. Each vertex moves toward neighbours lined up with the line (weight cos⁴), never across it. The cross-line part of the move is kept in full; the along-line part (spacing) at 50%. |
| `line_profile` | Two passes. The depth (distance from the neck axis) is re-fitted as a straight-line function along the line: about 8 mm along, 1.2 mm across, same surface sheet only. Vertices move sideways only. |
| Corner kept | Within about 1 cm of the corner under the ear, neither step acts, so the turn up toward the ear stays sharp. The ear, collar and mouth hold as before. |
| `HeroSkinNormals.cs` | Under the jawline the radius is 8 mm (was 5.5). It eases back to 12 mm over about 36 mm above the line (was 14 mm). |
| `jawtools.game_normals` | A numpy port of `HeroSkinNormals`' area-weighted normals, including the mouth, nose and ear masks. Blender stills use it, so they shade like the game. |

The jaw's position is unchanged: same corner and same lines (`jl_*` parameters). The vertices moved only along the surface, plus the smoothing.

**Lobby re-sync (pass 3).** Use `jaw_sync.sh`, then `jaw_provenance.py`, instead of `work/hero-sync/sync_heroes.sh`. They run the same steps, but the Unity clone is the *staged* state plus this pass's three files, and the provenance hashes the staged look sources. Why: a parallel session's clothing-V2 polo sat unstaged in the shared tree mid-pass, and `sync_heroes.sh` exports the working tree. That session has since staged its polo, so the lobby now carries polo + pass-3 jaw, which is the staged game.

Proof: `proof/pass3_<Sex>.png` (TARGET / PASS 2 / NOW: front, three-quarter, side) and `proof/pass3_jawline.png` (the jawline alone, three-quarter). PASS 2 stills come from `unity_p2b/` (the staged pass-2 FBXs + 5.5 mm normals); NOW from `unity_p3/`. Same clone, same capture.
