# FACE_SPEC — Male HeroBase Face Lock (Micro1g)

**Authority folder:** `ArtDir/hero/base_lock/face_lock/`  
**Authority plates (source JPGs):**
- `ArtDir/hero/base_lock/male_head_detail.jpg` (6 panels)
- `ArtDir/hero/base_lock/male_body_bald.jpg` (front+back — head crops `07`/`08`)
- `ArtDir/hero/base_lock/male_multiangle_body.jpg` (8 angles — head crops `09`–`16`)

**REJECT as authority:** `ArtDir/hero/base_lock/preview/face_example/` (wrong likeness — dough face, hair bowl, pink shirt). See `SHEET_plate_vs_bad_example.png`.

**Character:** bald young-adult male soft-plastic party-tennis HeroBase. Style = Switch Sports / Wii clarity + soft-plastic beauty. **NOT** Roblox, **NOT** photoreal, **NOT** doughy blob. Warm tan skin. Friendly slight closed-mouth smile default. Grey crew shirt on plates is **NOT** mesh scope (body below neck stays locked Body_M).

**Mesh scope:** FACE ONLY on `Body_M` bald head. Prefer sculpting primary forms into `Body_M` head. No hair mesh this pass. Stubble may be texture/paint later; silhouette must leave room.

Use `SHEET_head_detail_6.png`, `SHEET_multiangle_heads.png`, `SHEET_all_face_angles.png` while sculpting. Gate every line PASS/FAIL — no soft language.

---

## Overall likeness (gate first)

| Line | PASS when | FAIL when |
|------|-----------|-----------|
| OVERALL_LIKENESS | Bald soft-plastic male clearly matches plate family at arm’s length (front + 3/4 + profile) | Looks like face_example, generic blob, Roblox, or photoreal scan |
| BALD_NO_HAIR | Completely bald smooth scalp; no hair cards/caps/bowl | Any hair mesh or bowl silhouette |
| STYLE | Soft-plastic stylized clarity (Switch Sports / Wii beauty) | Dough blob, Roblox, photoreal pores/skin detail |
| SKIN_TONE | Warm tan matching plates | Grey clay, yellow plastic, pink candy, dead Blockout_Grey face |
| DEFAULT_EXPR | Slight closed-mouth friendly smile | Flat dead mouth, grimace, open teeth default, angry |
| SHIRT_OUT_OF_SCOPE | Grey crew shirt ignored — mesh is bald body below neck as locked | Modeling shirt into Body_M face pass |

---

## 1. Cranial dome / bald scalp silhouette

| Line | PASS when | FAIL when |
|------|-----------|-----------|
| CRANIAL_FRONT | Front silhouette = smooth tall oval; no flat lid, no peanut pinch | Flat top, undercut, conical, peanut |
| CRANIAL_PROFILE | Continuous soft dome forehead→crown→occiput→nape; matches `02`/`03`/`11`/`12` | Flat back, abrupt shelf, egg too long fore-aft |
| CRANIAL_TOP | Top oval wider aft than fore (`06_top_cranial.png`); ears peek at sides | Circle blob, diamond, missing ear tips |
| SCALP_SURFACE | Smooth soft-plastic; subtle form only | Noise, seams, hairline ridge, cap edge |

---

## 2. Forehead + brow ridge

| Line | PASS when | FAIL when |
|------|-----------|-----------|
| FOREHEAD | Broad smooth forehead with gentle vertical curve | Bulging Neanderthal, knife-edge, concave dent |
| BROW_SHELF | Soft shelf above eyes — depth without angry cliff | Angry cliff brow, no shelf (flat forehead into eyes), pinched focus brows |
| BROW_WIDTH | Shelf spans near orbital width; soft into temples | Narrow pinched center only |

---

## 3. Eyebrows

| Line | PASS when | FAIL when |
|------|-----------|-----------|
| BROW_SHAPE | Thick dark-brown arched brows; clear separate brow mass | Thin lines, floating stickers, fused unibrow, missing |
| BROW_PLACE | Sit on brow shelf; outer tail soft down; match plate arch | Too high/low, angry inward slash, focus-pinched |
| BROW_SEPARATION | Left/right clear gap above nose root | Bridged mass into nose |

---

## 4. Eyes

| Line | PASS when | FAIL when |
|------|-----------|-----------|
| EYE_SIZE | Large friendly eyes matching plate scale vs face | Beady dots OR huge anime |
| EYE_IRIS | Large brown irises; simple white specular highlight | Flat black buttons, no highlight, multi photoreal catchlights |
| EYE_LIDS | Soft upper/lower lids readable | No lids, carved trenches, sticker cutouts |
| EYE_SOCKET | Eyes sit in sockets under brow (depth) — not flat stickers on sphere | Flat sticker eyes on cheek sphere (face_example FAIL) |
| EYE_SPACING | ~1 eye-width between inner corners (plate) | Crossed, wall-eyed, too tight/wide vs plate |
| EYE_SYMMETRY | L/R match pairs in front | One droops or scales differently |

---

## 5. Nose (CRITICAL — current mesh nose sits too low)

| Line | PASS when | FAIL when |
|------|-----------|-----------|
| NOSE_HEIGHT | Nose tip height matches plate: brow→tip→lip thirds (see §12). Tip NOT dropped toward mouth | Tip too low (face_example / Micro1f leftover) |
| NOSE_BRIDGE_PROFILE | Straight bridge in profile (`02`/`03`/`11`/`12`) | Concave ski-jump, Roman hook, missing bridge |
| NOSE_TIP | Defined soft tip (not knife, not potato) | Pinched nub, bulbous clown, melted |
| NOSE_ALAE | Soft alae; nostril openings visible in 3/4 (`04`/`05`/`13`/`14`) | No wings, tunnels only from below, hard tubes |
| NOSE_WIDTH_FRONT | Front width matches plate vs eye spacing | Too skinny pin, too wide pig |
| NOSE_ROOT | Soft root under brow; no trench carving into glabella | Deep carved canyon at root |

---

## 6. Cheeks

| Line | PASS when | FAIL when |
|------|-----------|-----------|
| CHEEK_ATHLETIC | Soft athletic cheeks; firm transition cheek→jaw | Balloon bulges under ears (face_example FAIL) |
| CHEEKBONE_3Q | Hint of cheekbone plane in 3/4 — not carved | Knife cheekbone OR zero form (sphere) |
| CHEEK_WIDTH | Matches plate zygomatic width vs jaw | Chipmunk OR gaunt hollows |

---

## 7. Ears

| Line | PASS when | FAIL when |
|------|-----------|-----------|
| EAR_ANATOMY | Stylized but readable helix + simplified antihelix | Smooth lumps / beans (face_example FAIL) |
| EAR_SIZE | Size vs head matches plate profile | Tiny buds OR giant sails |
| EAR_PLACE | Top ≈ brow line; bottom ≈ nose base (plate) | Slid too high/low/forward/back |
| EAR_Y_PAIR | L/R same height in front | Uneven ears |
| EAR_ATTACH | Clean soft attach to head; back views show attach (`10`/`15`/`16`) | Floating disks, hard cylinder stubs |

---

## 8. Mouth

| Line | PASS when | FAIL when |
|------|-----------|-----------|
| MOUTH_SMILE | Slight closed-mouth friendly smile; corners softly up | Flat slit, frown, smirk one-side, open grin default |
| LIP_VOLUME | Soft lip volume (upper slightly thinner than lower per plate) | Razor slit OR sausage lips |
| PHILTRUM | Hint of philtrum under nose | Deep trench OR totally absent flat |
| TEETH_DEFAULT | No teeth on default closed smile | Teeth visible on default |
| MOUTH_WIDTH | Width matches plate vs nose/eyes | Too wide frog OR tiny bead |

---

## 9. Jaw / chin

| Line | PASS when | FAIL when |
|------|-----------|-----------|
| JAW_U | Rounded U-chin; jaw width matches plate | Square brick, pointed V, dough jowls |
| CHIN_CLEFT | Subtle cleft under light (plate) — hint only | Deep butt-chin OR completely spherical chin |
| UNDER_CHIN | Clear under-chin plane into neck (Micro1e chin lock KEEP) | Melted chin into neck, no plane |
| JAW_PROFILE | Profile jaw/chin matches `02`/`03` | Receding melt OR lantern overshoot |
| NO_JOWLS | No soft dough hanging under ears/jaw corners | Dough jowls (face_example FAIL) |

---

## 10. Stubble

| Line | PASS when | FAIL when |
|------|-----------|-----------|
| STUBBLE_ROOM | Jaw/chin silhouette leaves room for fine dark stubble (texture/paint OK later) | Chin form so soft stubble can’t read; OR sculpted hair spikes |
| STUBBLE_ZONE | Zone = jaw, chin, upper lip shadow per plates | Random cheek patches; full beard mass |

---

## 11. Neck

| Line | PASS when | FAIL when |
|------|-----------|-----------|
| NECK_COLUMN | Thick column into shoulders matching plates | Stick neck OR melted fat cylinder |
| CHIN_LOCK | Keep Micro1e chin/under-chin lock — do not reopen unless face work forces micro-fix (document) | Chin plane regresses |
| NAPE | Back/nape continuous scalp→neck (`08`/`10`) | Shelf cut, hole, hairline |

---

## 12. Proportions (relative landmarks)

Measure on front plate `01_front_face.png` / mesh overlay. Binary eye-check vs plate, not invented numbers.

| Line | PASS when | FAIL when |
|------|-----------|-----------|
| PROP_THIRDS | Vertical: brow shelf → nose tip → lip line → chin spacing matches plate (nose NOT low) | Compressed midface; low nose; long chin vs plate |
| PROP_EYE_SPACE | Inner-corner spacing ≈ one eye width | Far wall-eye OR tight cyclops |
| PROP_EAR_ALIGN | Ear top↔brow; ear bottom↔nose base (profile+front) | Ears floated |
| PROP_NOSE_MOUTH | Nose base to upper lip gap matches plate philtrum length | Mouth jammed under nose OR huge gap |
| PROP_HEAD_NECK | Head mass vs neck thickness matches plate | Bobblehead OR no-neck |

---

## 13. Material / construction

| Line | PASS when | FAIL when |
|------|-----------|-----------|
| MAT_SKIN | Warm soft-plastic skin (Unity `Male_Form_Skin` approx OK); roughness ~0.55–0.70 | Grey clay face, metallic, muddy SSS soup |
| CONSTRUCT | Primary forms sculpted into `Body_M` head preferred; thin feature layers OK if welded clean | Separate floating face shell; hair mesh this pass |
| NO_HAIR_MESH | No hair cards/caps this pass | Any hair geo |
| BODY_BELOW_NECK | Body below neck unchanged lock (hands/silhouette Micro1f) | Regress hands, waist, silhouette |

---

## 14. Explicit FAIL list (face_example)

Any of these = automatic FAIL (see `SHEET_plate_vs_bad_example.png`):

| FAIL tag | Description |
|----------|-------------|
| FAIL_HAIR_BOWL | Brown bowl hair sitting on head |
| FAIL_PINK_SHIRT | Pink shirt in face proof / treating shirt as face scope |
| FAIL_DOUGH_CHEEKS | Balloon dough cheeks under ears |
| FAIL_LUMP_EARS | Smooth lump/bean ears without helix read |
| FAIL_STICKER_EYES | Flat sticker eyes on sphere; no socket depth |
| FAIL_PINCH_BROWS | Pinched focus/angry brows |
| FAIL_LOW_NOSE | Nose tip too low vs brow/mouth |
| FAIL_WRONG_LIKENESS | Overall not the plate male |

---

## Proof / reply contract

Deliverables (Micro1g):
- `proof/02_match_m_head.png` — multi-angle overlays plate vs mesh
- refreshed `proof/03_unity_m.png` head close-ups
- `proof/MALE_HEAD_RESULTS.md`
- Binary GATE lines for each face region above

Reply only:
```
GATE: MALE_HEAD PASS
```
or
```
GATE: MALE_HEAD FAIL
<failed lines only>
```

---

## Micro1h densify requirement (topology)

**Micro1g FAIL root cause:** ~9.5 mm head vertex spacing + displacement/`Face_M` stickers cannot form nose alae, ear helix+concha, or clean cheek planes.

**Required for PASS (see `PROCESS_FaceMatch.md`):**
- Local densify head to **~2–4 mm** spacing **or** welded nose block + ear shells (hand pattern)
- Mandatory loops: alae/nostril, lids, mouth, jaw→ear, helix+concha
- Do **not** use displacement + Face_M stickers as the volume engine
- Goal: `GOAL_HeroBase_Micro1h_MaleFaceDensify.md` · Paste: `PASTE_PROMPT_MaleFace_Micro1h.txt`
