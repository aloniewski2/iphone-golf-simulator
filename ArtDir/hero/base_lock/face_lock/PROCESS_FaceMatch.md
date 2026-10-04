# PROCESS — FaceMatch (Micro1h fixed pipeline)

**Goal:** match `face_lock/` plates perfectly. Micro1g proved displacement-on-coarse-head + `Face_M` stickers cannot form nose/ears/cheeks. This pipeline replaces that.

**Start blend:** `ArtDir/hero/base_lock/blender/HeroBase_Male_Silhouette.blend`  
**SHA-256:** `c6b66edefc902c38c8696d7d5b25feb846f58c60c3edb21cd7a465d3c8dfd0d4` (Micro1g lock — verify with `shasum -a 256` before edit)

**Authority:** this folder + `../male_head_detail.jpg` + `../male_body_bald.jpg` + `../male_multiangle_body.jpg`  
**REJECT:** `../preview/face_example/`

---

## Hard rules (never soft-fail)

1. **Stop** displacement + `Face_M` stickers as the **volume engine**. Stickers may only decorate *after* solid forms exist (iris color, brow pigment, catchlight).
2. **Topology first:** either densify head to **~2–4 mm** spacing **or** welded nose block + ear shells (hand pattern).
3. **Mandatory loops** before claiming feature PASS: alae/nostril · lids · mouth · jaw→ear · helix+concha.
4. Gate **only** vs `face_lock/` crops + sheets + FACE_SPEC binary lines. Adnan eye-check is final.
5. Keep: body below neck, hands, sil ≤5% bald front/back, Micro1e chin lock.
6. One defect cluster per script/pass when headless; always render → read PNG → decide next edit.

---

## Path A — Local densify head (preferred when reshaping midface/cheeks)

```
1. Isolate head vertex region on Body_M (above neck weld; do not move body/hands).
2. Local subdivide / remesh-fair HEAD ONLY until mean edge length ≈ 2–4 mm
   (target band: midface, nose, ears, lids, mouth denser; cranial dome may stay slightly coarser).
3. Preserve neck weld ring; fair so no shelf into locked neck/chin band.
4. Add / cut mandatory loops (see below).
5. Sculpt primary forms INTO Body_M (Multires or coordinate sculpt — topology-preserving after densify).
6. Optional thin Face_M materials only for non-volume (iris, pigment) — weld or parent clean.
7. Overlay vs face_lock plates → FACE_SPEC gates → Unity proof.
```

## Path B — Welded nose block + ear shells (like hands)

```
1. Keep Body_M head base; build solid nose block (bridge + tip bulb + alar lobes + nostril openings).
2. Build ear shells (helix rim, antihelix hint, concha bowl, lobe) — not tube rims on a bean.
3. Boolean/weld/fair into Body_M (or keep as welded children with seamless normals + same skin mat).
4. Ensure loop support on alae, lids, mouth, jaw→ear attach, helix+concha.
5. Carve sockets / cheek planes on densified or supported verts only.
6. Overlay → FACE_SPEC → Unity. Document weld seams in results.
```

**Allowed:** A alone, B alone, or A then B for residual features.  
**Forbidden:** another Micro1g-style pass of global displacement fields on ~9.5 mm verts as the main fix.

---

## Mandatory loops (checklist)

| Loop | Purpose | FAIL if missing |
|------|---------|-----------------|
| Alae / nostril | Wing silhouette + opening in 3/4 | NOSE_ALAE / NOSE_WIDTH_FRONT |
| Lids | Upper/lower lid readable around socket | EYE_LIDS / EYE_SOCKET |
| Mouth | Lip volume + smile seam | LIP_VOLUME / MOUTH_SMILE |
| Jaw → ear | Clean cheek→jaw→ear attach | CHEEK_WIDTH / EAR_ATTACH |
| Helix + concha | Readable ear anatomy | EAR_ANATOMY |

---

## Inspect → edit → prove loop

```
Phase 0 — VERIFY START
- shasum -a 256 HeroBase_Male_Silhouette.blend → must be c6b66ede…
- Confirm Body_M + any Face_M; hands bit-identical intent; sil baseline

Phase 1 — TOPOLOGY (no likeness claim yet)
- Choose Path A and/or B
- Measure head edge lengths (report mean/min in midface)
- Cut mandatory loops; screenshot wireframe close-ups → proof/_gate/ or work/

Phase 2 — PRIMARY FORMS
- Nose block / alae / bridge height (plates: tip NOT low)
- Eye sockets + lids (depth under brow shelf — not stickers)
- Ears helix+concha
- Cheeks athletic width vs plate (narrow inherited wide jaw if needed WITHOUT melting chin)
- Mouth slight smile + lip volume
- Cranial dome fair; kill displacement ripples

Phase 3 — PLATE MATCH
- Open SHEET_head_detail_6 / SHEET_multiangle_heads / SHEET_all_face_angles
- Overlay each crop 01–16; binary FACE_SPEC every line
- Reject if any FAIL_* tag from FACE_SPEC §14

Phase 4 — REGRESSION
- Hands 0 move (or prove bit-identical)
- Body below neck 0 move
- Chin lock / under-chin plane held
- bald_front + bald_back each ≤ 5.0%

Phase 5 — DELIVER
- Lock blend + FBX → Unity HeroBase_Male
- proof/02_match_m_head.png, proof/03_unity_m.png, proof/MALE_HEAD_RESULTS.md
- GATE: MALE_HEAD PASS|FAIL
```

---

## Anti-patterns (automatic FAIL process)

- Volume from `Face_M` displacement / floating shells
- Full-body remesh / voxel remesh / Tripo regen
- Matching `preview/face_example/`
- Declaring PASS on landmark positions alone while Unity reads blockout
- Inventing sil ≤2% gate
- Reopening hands/body “while we’re here”

---

## Related

- Goal: `GOAL_HeroBase_Micro1h_MaleFaceDensify.md` (project root)
- Paste: `PASTE_PROMPT_MaleFace_Micro1h.txt`
- Spec: `FACE_SPEC.md`
- Prior FAIL audit: `../proof/MALE_HEAD_RESULTS.md` (Micro1g)
