# FACE_SPEC — Female HeroBase Face Lock (MicroF1)

**Authority folder:** `ArtDir/hero/base_lock/face_lock_f/`
**Authority plates (source JPGs):**
- `ArtDir/hero/base_lock/female_head_detail.jpg` (6 panels → crops `01`–`06`)
- `ArtDir/hero/base_lock/female_body_bald.jpg` (front+back — head crops `07`/`08`)
- `ArtDir/hero/base_lock/female_multiangle_body.jpg` (8 angles — head crops `09`–`16`; ~90 px tall, **soft** — use for angle/placement only, take detail from `01`–`08`)

**Contrast only (never copy):** `../face_lock/` (male). See `SHEET_female_vs_male_contrast.png`. Male brow shelf, square jaw, stubble, Adam's apple, tan skin, and large nose are all FAIL on the female.
**REJECT as authority:** `ArtDir/hero/base_lock/preview/face_example/`, any hair-on plate (`female_body_plain.jpg` is byte-identical to `female_body_bald.jpg` — both bald; hair-on plates are archived).

**Character:** bald young-adult female, soft-plastic party-tennis HeroBase. Style = Switch Sports / Wii clarity + soft-plastic beauty (Pixar-adjacent stylization). **NOT** Roblox, **NOT** photoreal, **NOT** doughy blob, **NOT** a re-skinned male head. Light warm **peach** skin (lighter and pinker than the male's tan). Friendly slight closed-mouth smile default. Grey mock-neck bodysuit on the plates is **NOT** mesh scope (body below neck stays the locked female body).

**Mesh scope:** FACE + HEAD + NECK only on the female base mesh. Sculpt primary forms INTO the head mesh. No hair mesh (hair is a separate optional asset). Zero verts moved below the neck weld.

Use `SHEET_head_detail_6.png`, `SHEET_multiangle_heads.png`, `SHEET_all_face_angles.png` while sculpting. Gate every line PASS/FAIL — no soft language.

---

## Plate-read facts (what the plates actually show)

Recorded once so chat 6 does not re-derive them. Verified on `01`/`02`/`04` at 3x zoom.

- **Face shape:** egg/heart. Wide soft forehead and cranium tapering to a **narrow rounded chin**. Jaw is soft and short — no angular corner, no jowl, no masseter bulge. Cheeks are full and round but firm (youthful, not baby-dough).
- **Cranium:** tall rounded dome; front silhouette is a smooth tall oval. Top view (`06`) is an egg, **wider aft than fore**, ears peeking at the sides, nose tip visible at the bottom edge.
- **Eyes:** large, round-almond, dark-brown irises with a big white specular + small secondary glint; white sclera visible at both sides; **dark upper lash line that thickens and flicks into a small wing at the outer corner**; thin lower lid line; soft double-lid crease. No lash geometry strands — lash is a painted/inked line on the lid, not hair.
- **Brows:** dark brown, medium-thick soft arch, slight taper to the outer tail (tail ends slightly lower than the peak). Clear gap over the nose root. Sit **on a soft brow arch** — much gentler shelf than the male.
- **Nose:** small and slim. Narrow bridge, **rounded soft tip, tip very slightly upturned** in profile; nostril wings subtle. No hump, no ski-jump. Tip is **well above** the lip (thirds not compressed).
- **Mouth:** small. Closed slight smile, corners softly up. Lower lip a little fuller than upper; lips are a muted peach-pink one step deeper than skin. No teeth. Faint philtrum.
- **Ears:** clearly visible from the front, **stand out from the head** (front view shows the full ear outline both sides). Stylized but readable helix, antihelix hint, concha bowl, small soft lobe. Top ≈ brow line, bottom ≈ nose base.
- **Neck:** slender, longer-looking column than the male, gently forward-leaning in profile; smooth (no Adam's apple). Plate neck blends into the mock-collar line — neck mesh ends at the locked neck weld, collar is out of scope.
- **Skin:** smooth, even, soft-plastic. No stubble, no pores, no freckles, no beard shadow. Faint warm blush on cheeks is allowed as paint only.

---

## Plate inconsistencies found while building MicroF1 (2026-10-01/02)

- The 3/4 plates (`04`/`05`, `13`–`16`) are perspective renders: head width and yaw do not agree with the front/profile plates (a landmark fit gives ≈ −23° / −29°, a silhouette fit ≈ 0°). Use them for angle/placement only.
- `female_body_bald.jpg`: the BACK figure's head is ≈ 3–4 % narrower than the FRONT figure's (cranium 192 vs 200 mm, ears 226 vs 236 mm). The SIL gate is met by splitting the difference (skull −1.3 mm/side, ear standoff −1.8 %).
- Top view `06` shows the ears barely peeking, while the front view `01` shows them standing ≈ 20–28 mm beyond the skull; the front plate wins.
- The two eye-spacing statements above were measured, not estimated.

---

## Overall likeness (gate first)

| Line | PASS when | FAIL when |
|------|-----------|-----------|
| OVERALL_LIKENESS | Bald soft-plastic **female** clearly matches the plate family at arm's length (front + 3/4 + profile) in **Unity** capture | Reads male, generic blob, Roblox, photoreal scan, or the male head re-scaled |
| FEMALE_READ | Instantly reads as a young woman at phone distance without hair or clothes | Reads androgynous/male (heavy brow shelf, square jaw, thick neck, big nose) |
| BALD_NO_HAIR | Completely bald smooth scalp; no hair cards/caps/bowl | Any hair mesh or bowl silhouette |
| STYLE | Soft-plastic stylized clarity | Dough blob, Roblox, photoreal pores/skin detail |
| SKIN_TONE | Light warm peach matching plates | Grey clay, yellow plastic, male-tan, pink candy |
| DEFAULT_EXPR | Slight closed-mouth friendly smile | Flat dead mouth, grimace, open teeth default, smirk |
| MALE_NOT_COPIED | No male feature transferred (brow shelf, jaw width, nose scale, stubble zone) | Side-by-side with `SHEET_female_vs_male_contrast.png` shows male proportions |
| COLLAR_OUT_OF_SCOPE | Mock-neck collar ignored; neck is bare skin to the locked weld | Collar/clothing modeled into the head pass |

---

## 1. Cranial dome / bald scalp silhouette

| Line | PASS when | FAIL when |
|------|-----------|-----------|
| CRANIAL_FRONT | Smooth tall oval; no flat lid, no peanut pinch | Flat top, undercut, conical, peanut |
| CRANIAL_PROFILE | Continuous round dome forehead→crown→occiput→nape; matches `02`/`03`/`11`/`12` | Flat back, abrupt shelf, egg too long fore-aft |
| CRANIAL_TOP | Egg wider aft than fore (`06`); ear tips peek at sides; nose tip just visible at front | Circle blob, diamond, missing ear tips |
| SCALP_SURFACE | Smooth; subtle form only | Noise, seams, hairline ridge, cap edge, displacement ripples |

## 2. Forehead + brow arch

| Line | PASS when | FAIL when |
|------|-----------|-----------|
| FOREHEAD | Tall smooth forehead, gentle rounded curve, near-vertical in profile | Bulging, knife-edge, concave dent, male slope |
| BROW_ARCH | **Soft** arch above eyes — a gentle swell, not a shelf | Heavy male brow shelf/cliff, or flat forehead straight into lids |
| BROW_WIDTH | Arch spans about the orbit width, fading into temples | Narrow pinched centre only |

## 3. Eyebrows

| Line | PASS when | FAIL when |
|------|-----------|-----------|
| BROW_SHAPE | Dark-brown, medium-thick arched brows, clear tapered tail | Hairline-thin strips, floating stickers, fused unibrow, missing, male-thick slabs |
| BROW_PLACE | On the brow arch; peak above outer iris; tail slightly lower than peak | Too high/low, angry inward slash, flat bar |
| BROW_SEPARATION | Clear gap above nose root | Bridged mass into nose |

## 4. Eyes

| Line | PASS when | FAIL when |
|------|-----------|-----------|
| EYE_SIZE | Large friendly eyes (larger relative to face than the male) matching plate | Beady dots OR full anime saucer |
| EYE_IRIS | Large dark-brown irises; big white specular highlight (+ small secondary glint OK) | Flat black buttons, no highlight, photoreal catchlight stack |
| EYE_LIDS | Readable upper lid with dark lash line + small outer-corner wing; thin lower lid; soft crease | No lids, carved trenches, sticker cut-outs, 3D lash strands |
| EYE_SOCKET | Eyeball sits in a real socket under the brow arch (depth read in 3/4 + profile) | Flat sticker eyes on cheek sphere |
| EYE_SPACING | Inner-corner gap ≈ 0.6 × the eye aperture width (measured on `01`: gap ≈ 39 mm, aperture ≈ 64 mm; ≈ 0.5× including the wing) — match the plate | Crossed, wall-eyed, too tight/wide vs plate |
| EYE_SYMMETRY | L/R pairs match in front view | One droops or scales differently |
| EYE_SCLERA | White visible both sides of the iris in front view | Iris touching both corners (staring) or no white |

## 5. Nose

| Line | PASS when | FAIL when |
|------|-----------|-----------|
| NOSE_HEIGHT | Tip well above the lip; brow→tip→lip→chin thirds match plate (see §12) | Tip dropped toward the mouth |
| NOSE_SIZE | **Small, slim** vs the male (profile nose protrusion visibly shorter) | Male-sized nose, pig snout |
| NOSE_BRIDGE_PROFILE | Short straight bridge; gentle rise to the tip | Hump, hook, deep ski-jump concave |
| NOSE_TIP | Soft rounded tip, very slightly upturned (`02`/`03`/`11`/`12`) | Pinched nub, bulbous clown, pointy witch, melted |
| NOSE_ALAE | Subtle soft alae; small nostril read in 3/4 (`04`/`05`/`13`/`14`) | No wings, hard tubes, flared pig nostrils |
| NOSE_WIDTH_FRONT | Front width ≈ the inner-eye gap or slightly narrower | Pin-thin, or wide like the male |
| NOSE_ROOT | Soft root under brow arch; no trench | Deep canyon at glabella |

## 6. Cheeks

| Line | PASS when | FAIL when |
|------|-----------|-----------|
| CHEEK_FULL | Soft round youthful cheeks; firm transition into the narrow jaw | Balloon bulges under ears, hollow gaunt cheeks |
| CHEEKBONE_3Q | Gentle malar swell in 3/4 (`04`/`05`) | Knife cheekbone OR zero form (sphere) |
| CHEEK_WIDTH | Face tapers: widest at the cheek/ear level, narrowing to chin (heart/egg) | Chipmunk, or square/parallel sides |

## 7. Ears

| Line | PASS when | FAIL when |
|------|-----------|-----------|
| EAR_ANATOMY | Stylized but readable helix + antihelix hint + concha bowl + soft lobe | Smooth lumps / beans, glued-on disks |
| EAR_SIZE | Size vs head matches plate profile (`02`/`03`) | Tiny buds OR giant sails |
| EAR_STANDOFF | Ears visibly **stand out** from the head in front view (`01`/`07`), full outline both sides | Pinned flat to skull so the outline vanishes |
| EAR_PLACE | Top ≈ brow line; bottom ≈ nose base | Slid too high/low/forward/back |
| EAR_Y_PAIR | L/R same height in front | Uneven ears |
| EAR_ATTACH | Clean soft attach to skull; back views (`08`/`10`/`15`/`16`) show a faired root | Floating disks, hard cylinder stubs, glued seam |

## 8. Mouth

| Line | PASS when | FAIL when |
|------|-----------|-----------|
| MOUTH_SMILE | Slight closed-mouth friendly smile; corners softly up | Flat slit, frown, one-side smirk, open grin default |
| LIP_VOLUME | Soft small lips; lower slightly fuller than upper | Razor slit OR sausage lips OR male thin-lip |
| PHILTRUM | Faint philtrum under nose | Deep trench OR flat absent |
| TEETH_DEFAULT | No teeth on default | Teeth visible |
| MOUTH_WIDTH | Small mouth; width ≈ 0.7× the iris-centre-to-iris-centre distance (≈ 1.6× nose width) on `01` | Wide frog OR tiny bead |
| LIP_COLOR | Muted peach-pink one step deeper than skin (paint only) | Red lipstick, grey lips, same as skin with zero separation |

## 9. Jaw / chin

| Line | PASS when | FAIL when |
|------|-----------|-----------|
| JAW_SOFT_U | Narrow **rounded** chin; jaw short and soft, tapering from cheeks | Square brick, hard male corner, pointed V witch-chin, dough jowls |
| CHIN_SIZE | Small soft chin, no cleft | Cleft, butt-chin, big male chin |
| UNDER_CHIN | Clear under-chin plane into neck | Melted chin into neck, double-chin |
| JAW_PROFILE | Profile jaw/chin matches `02`/`03`/`11`/`12` — chin slightly recessed vs lip line, gentle sweep to neck | Receding melt OR lantern overshoot OR male protruding chin |
| NO_JOWLS | No soft dough under ears/jaw corners | Jowls |
| NO_STUBBLE | Skin clean and smooth | Any stubble zone, beard shadow, sculpted hair spikes |

## 10. Neck (female anatomy)

| Line | PASS when | FAIL when |
|------|-----------|-----------|
| NECK_COLUMN | Slender smooth column, visibly thinner than the male's, flows into shoulders per plates | Thick male neck, stick neck, fat cylinder |
| NO_ADAMS_APPLE | Smooth throat in profile (`02`/`03`/`11`/`12`) — no laryngeal bump | Any Adam's apple, sternocleidomastoid cords, trapezius bulk |
| NECK_LEAN | Neck leans slightly forward in profile, head carried forward of shoulders (plate posture) | Rigid vertical post, or exaggerated craned neck |
| NAPE | Back/nape continuous scalp→neck (`08`/`10`) | Shelf cut, hole, hairline |
| NECK_WELD | Neck-base ring is untouched; no gap/shelf into the locked body | Seam, moved ring, regressed body |

## 11. Proportions (relative landmarks)

Measure on `01_front_face.png` and `02_left_profile.png` + mesh overlay. Binary eye-check vs plate, not invented numbers.

| Line | PASS when | FAIL when |
|------|-----------|-----------|
| PROP_THIRDS | Vertical: brow → nose tip → lip line → chin spacing matches plate (nose NOT low); eyes sit near the **mid-line of the head** (female plate: eye line close to half head height) | Compressed midface; low nose; long chin |
| PROP_EYE_SPACE | Inner-corner spacing ≈ 0.6 × eye width (plate `01`; the earlier "one eye width" wording was wrong) | Wall-eye OR tight cyclops |
| PROP_EAR_ALIGN | Ear top↔brow; ear bottom↔nose base (profile+front) | Floated ears |
| PROP_NOSE_MOUTH | Nose base to upper lip gap matches plate | Mouth jammed under nose OR huge gap |
| PROP_HEAD_NECK | Head mass vs slender neck matches plate | Bobblehead OR no-neck |
| PROP_HEAD_HEIGHT | Head height / body height consistent with the female plate (`female_body_bald.jpg`: top y=24, sole y=679 → head top→chin ≈ 109 px ≈ 1/6 of height; confirm by overlay) | Oversized chibi head OR tiny head |

## 12. Material / construction

| Line | PASS when | FAIL when |
|------|-----------|-----------|
| MAT_SKIN | Light warm soft-plastic skin; roughness ~0.55–0.70; no metallic | Grey clay, metallic, muddy SSS |
| CONSTRUCT | Primary forms sculpted into the female base head; thin paint layers OK | Separate floating face shell; hair geo |
| NO_STICKER_VOLUME | Volume comes from topology (densified or welded blocks) | Any form made by displacement or Face_F stickers |
| NO_HAIR_MESH | No hair cards/caps | Any hair geo |
| BODY_BELOW_NECK | Below-neck body, hands, feet unchanged (bit-identical or 0 verts moved) | Any regression |
| SIL_BALD | Silhouette overlay vs `female_body_bald.jpg` front+back each ≤ **5.0 %** | > 5.0 % |
| TRI_BUDGET | Document tris; densify OK with a note; flag if total > 60k | Unreported growth |

## 13. Explicit FAIL list

Any of these = automatic FAIL:

| FAIL tag | Description |
|----------|-------------|
| FAIL_MALE_HEAD | Male head/features re-used on female (check against `SHEET_female_vs_male_contrast.png`) |
| FAIL_HAIR_BOWL | Hair bowl/cap on head |
| FAIL_DOUGH_CHEEKS | Balloon dough cheeks / jowls |
| FAIL_LUMP_EARS | Bean/lump ears, no helix read |
| FAIL_STICKER_EYES | Flat sticker eyes, no socket depth |
| FAIL_HEAVY_BROW | Male brow shelf / angry pinched brows |
| FAIL_LOW_NOSE | Nose tip too low vs brow/mouth |
| FAIL_BIG_NOSE | Nose protrusion/width male-sized |
| FAIL_ADAMS_APPLE | Laryngeal bump or neck cords |
| FAIL_STUBBLE | Stubble/beard zone |
| FAIL_SQUARE_JAW | Angular male jaw |
| FAIL_COLLAR_MODELED | Mock-neck collar/clothes modeled into the mesh |
| FAIL_WRONG_LIKENESS | Overall not the plate woman |

---

## Densify / topology requirement (carried from male Micro1h/1i)

- Densify the **head only** to **~2–4 mm** spacing (midface/nose/ears/lids/mouth densest; cranial dome may stay slightly coarser) **or** welded nose block + ear shells.
- Mandatory loops: alae/nostril · lids · mouth · jaw→ear · helix+concha. Missing loop = FAIL on the mapped line (see `PROCESS_FaceMatch.md`).
- Do **not** use displacement + `Face_F` stickers as the volume engine.
- Keep the neck-weld ring and all below-neck verts untouched.

## Proof / reply contract (chat 6)

Deliverables:
- `proof/02_match_f_head.png` — multi-angle overlays plate vs mesh (crops `01`–`16`)
- `proof/03_unity_f.png` — Unity head close-ups at the same angles
- `proof/FEMALE_HEAD_RESULTS.md` — every line above with PASS/FAIL
- `proof/female-microf1-checks.json` — machine-readable line results + SIL %

Reply only:
```
GATE: FEMALE_HEAD PASS
```
or
```
GATE: FEMALE_HEAD FAIL
<failed lines only>
```
