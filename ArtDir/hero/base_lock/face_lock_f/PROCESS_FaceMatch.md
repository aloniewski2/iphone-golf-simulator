# PROCESS — Female FaceMatch (MicroF1)

**Goal:** match the `face_lock_f/` plates. Mirrors the male Micro1h/1i pipeline (`../face_lock/PROCESS_FaceMatch.md`): displacement-on-coarse-head + sticker faces cannot form nose, ears, lids or cheeks — so topology first, volume sculpted into real geometry.

**Start blend:** `ArtDir/hero/base_lock/blender/HeroBase_Female.blend`
**SHA-256:** `200dc3b93e65491a8a7c3a8220c76cde5ca02ec7d1279e339f154005b22685cd` — verify with `shasum -a 256` before any edit. **Mismatch → STOP and report.**
**Work folder:** `work/female-micro-f1-head/` (never edit the ArtDir blend in place until Phase 5 install; only one chat installs at a time — see `parallel_prompts/README_PARALLEL_8.md`).

**Authority:** this folder (`01`–`16`, `SHEET_*`, `FACE_SPEC.md`) + `../female_head_detail.jpg`, `../female_body_bald.jpg`, `../female_multiangle_body.jpg`.
**Contrast only:** `../face_lock/` (male). **REJECT:** `../preview/face_example/`.
**Forbidden:** male blend/FBX/`face_lock/` writes; body/hands/legs/torso edits (other chats own those).

---

## Hard rules (never soft-fail)

1. **No** displacement or `Face_F` stickers as the **volume engine**. Paint layers (iris, brow pigment, lash line, lip tint, blush) may only decorate *after* solid forms exist.
2. **Topology first:** densify the head to **~2–4 mm** spacing **or** build welded nose block + ear shells. Report mean/min edge length in the midface.
3. **Mandatory loops** before claiming a feature PASS: alae/nostril · lids · mouth · jaw→ear · helix+concha.
4. Gate **only** against `face_lock_f/` crops + sheets + `FACE_SPEC.md` binary lines. Adnan's eye-check is final.
5. **Do not copy the male head.** Female differs in: soft brow arch (no shelf), small slim nose, larger eyes, narrow rounded chin, standing-out ears, slender neck with no Adam's apple, no stubble, lighter peach skin.
6. Neck-weld ring and everything below it: **0 verts moved**. Prove with a vert-position diff.
7. One defect cluster per script/pass when headless; always render → read PNG → decide next edit. Push plate-vs-now images roughly every 15–20 min on long runs.
8. Bald only. Hair stays a separate optional asset and may never supply scalp, ears, face or neck.

---

## Path A — Local densify head (preferred)

```
1. Isolate head verts above the neck weld (do not move body/hands).
2. Local subdivide / remesh-fair HEAD ONLY to mean edge ≈ 2–4 mm
   (midface, nose, ears, lids, mouth densest; dome may be slightly coarser).
3. Preserve the neck weld ring; fair so there is no shelf into the locked neck.
4. Cut mandatory loops (table below).
5. Sculpt primary forms INTO the head mesh (coordinate sculpt / multires — topology-preserving).
6. Optional thin paint-only layers for iris, brow, lash line, lip tint, blush.
7. Overlay vs crops → FACE_SPEC gates → Unity proof.
```

## Path B — Welded nose block + ear shells

```
1. Keep the head base; build a small slim nose block (narrow bridge, rounded slightly-upturned tip,
   subtle alae, small nostril openings).
2. Build ear shells (helix rim, antihelix hint, concha bowl, small lobe) that STAND OUT from the head.
3. Boolean/weld/fair into the head mesh with seamless normals + same skin material. Document weld seams.
4. Ensure loop support on alae, lids, mouth, jaw→ear, helix+concha.
5. Carve eye sockets / cheek planes on densified or supported verts only.
6. Overlay → FACE_SPEC → Unity.
```

**Allowed:** A alone, B alone, or A then B for residual features.
**Forbidden:** a Micro1g-style pass of global displacement fields on coarse (~9.5 mm) verts as the main fix.

---

## Mandatory loops

| Loop | Purpose | FAIL lines if missing |
|------|---------|-----------------------|
| Alae / nostril | Small soft wing + opening in 3/4 | NOSE_ALAE / NOSE_WIDTH_FRONT |
| Lids | Upper (lash line + wing) / lower lid readable around the socket | EYE_LIDS / EYE_SOCKET |
| Mouth | Soft lip volume + smile seam | LIP_VOLUME / MOUTH_SMILE |
| Jaw → ear | Cheek→narrow jaw→ear attach | CHEEK_WIDTH / EAR_ATTACH |
| Helix + concha | Readable ear, standing out | EAR_ANATOMY / EAR_STANDOFF |

---

## Female-specific watch list

| Risk | What goes wrong | Guard |
|------|-----------------|-------|
| Male-head inheritance | Base head already shares a male-ish template: heavy brow, broad jaw, big nose | Compare against `SHEET_female_vs_male_contrast.png` every pass |
| Over-narrow chin | Pointed V "witch" chin | Rounded U per `01`/`04`; check profile `02` |
| Eyes too small | Reads beady/male | Plate eye is large; judge in Unity at arm's length |
| Ears pinned flat | Outline vanishes in front view | EAR_STANDOFF is a hard gate |
| Neck too thick / Adam's apple | Reads male | Neck slender; throat smooth in profile |
| Shirt collar modeled | Mock-neck is out of scope | Neck ends at locked weld |
| Plates disagree | `09`–`16` are soft and ~90 px tall | Take detail from `01`–`08`; use `09`–`16` for angle/placement only |

---

## Inspect → edit → prove loop

```
Phase 0 — VERIFY START
- shasum -a 256 HeroBase_Female.blend → 200dc3b9…  (mismatch = STOP)
- Record head tri count, mean edge length, neck-weld ring vert positions (hash/snapshot)
- Snapshot sil % vs female_body_bald.jpg front+back

Phase 1 — TOPOLOGY (no likeness claim yet)
- Choose Path A and/or B; cut mandatory loops
- Wireframe close-ups → work/female-micro-f1-head/

Phase 2 — PRIMARY FORMS (order)
 a. Cranium dome + forehead (tall egg; fair out ripples)
 b. Jaw/chin taper + cheeks (heart/egg face)
 c. Eye sockets + lids
 d. Small slim nose (tip above lip)
 e. Ears: standoff + helix/concha + faired root
 f. Mouth: small, slight smile, lower lip a little fuller
 g. Neck: slender, smooth throat, forward lean

Phase 3 — PLATE MATCH
- Open SHEET_head_detail_6 / SHEET_multiangle_heads / SHEET_all_face_angles
- Overlay each crop 01–16; binary FACE_SPEC every line; reject on any FAIL_* tag (§13)

Phase 4 — REGRESSION
- Below-neck verts: 0 moved (diff vs Phase 0 snapshot)
- Hands/body bit-identical intent
- sil_front + sil_back each ≤ 5.0 %
- Male blend/FBX/face_lock untouched (SHA still 82ea9610… / 68de53ca…)

Phase 5 — DELIVER
- Save to work/…; install into ArtDir blend only when no other chat is installing
- Export Female FBX → Unity HeroBase_Female (female paths only)
- proof/02_match_f_head.png, proof/03_unity_f.png, proof/FEMALE_HEAD_RESULTS.md, proof/female-microf1-checks.json
- GATE: FEMALE_HEAD PASS|FAIL
```

---

## Anti-patterns (automatic process FAIL)

- Volume from displacement / floating shells / stickers
- Full-body remesh, voxel remesh, Tripo regen
- Matching `preview/face_example/` or the male crops
- Declaring PASS on landmark positions alone while Unity reads blockout or male
- Inventing a sil ≤ 2 % gate (gate is ≤ 5 %)
- Reopening torso/limbs/hands "while we're here"
- Adding hair, clothes, collar, rig, Mixamo or Animator

---

## Related

- Spec: `FACE_SPEC.md`
- Paste for chat 6: `PASTE_PROMPT_FemaleFace_MicroF1.txt`
- Male reference pipeline: `../face_lock/PROCESS_FaceMatch.md`, `../face_lock/FACE_SPEC.md`
- Parallel map: `../parallel_prompts/README_PARALLEL_8.md`
