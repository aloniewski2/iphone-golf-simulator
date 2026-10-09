# Golf kit design — 2026-10-05

P8 and the final cloth release are satisfied. Geometry and maps remain isolated candidates until the complete gate and eye review. This document describes the current assets; it does not certify unfinished checks. This file can be copied to `ArtDir/hero/kit/golf/GOLF_KIT_DESIGN.md` without editing any other kit authority.

## Construction references

These pages supply construction notes only. No product photography, branding, embroidery or text is copied onto the meshes or textures.

| Piece | Primary reference | Observation used |
|---|---|---|
| Polo | [FootJoy Solid Pique](https://www.footjoy.com/men/golf-apparel/solid-pique-wtrim/SPTM25.html) | A performance polo as the garment category; fabric choice is our design decision. |
| Polo pattern | [Maelo polo assembly guide](https://maelodesign.com/blogs/Sewing_a_Polo_Shirt) | Separate collar and collar stand, layered placket, sleeve binding, reinforced side vents, and a rear hem drop. |
| Trousers | [FootJoy Tour Pant](https://www.footjoy.com/product/men/apparel-men/apparel-men-pants/tour-pant/37770-W36-L30.html) | Flat front, taper from hip to hem, usable side pockets, stretch waist. Our pressed crease and belt loops are authored additions. |
| Female skort | [adidas Ultimate365 Tour Pleated Skort](https://www.adidas.com/us/ultimate365-tour-pleated-skort/KC2059.html) | Pleated golf skort silhouette. Omit all logos; use separate inner shorts and outer pleats. |
| Glove | [FootJoy StaSof](https://www.footjoy.com/product/men/gloves-men/stasof/006STA.html) | Leather golf glove category and hand-specific fit. Perforations and articulated knuckles are our construction targets, not copied patterns. |
| Shoes | [FootJoy Traditions Spikeless](https://www.footjoy.com/sale/previous-season-shoes/traditions-spikeless/003FJT-57924.html) | Structured spikeless golf shoe; authored saddle panel, welt and low rubber traction nubs. |
| Cap pattern | [Katagami baseball cap guide](https://katagami.org/en/howto-cap.html) | Six crown panels, layered stiffened brim, concentric topstitch rows and bound opening. Both heroes use a six-panel cap with a curved brim. |

Choose **smooth performance knit** for the polo: its restrained grain stays legible on the phone, supports a relaxed golf fit, and leaves collar/placket construction as the main close-up cue. Twill on trousers and headwear provides a contrasting directional fabric response. These are original style decisions; no purchase is needed.

## Outfit and silhouette

Male: relaxed short-sleeve white polo, charcoal flat-front trousers, narrow black belt with a real buckle, white lead-hand glove, white spikeless shoes with black saddles/soles, black six-panel cap.

Female: relaxed short-sleeve white polo, charcoal pleated skort with separate inner shorts, narrow black belt, white lead-hand glove, matching shoes, black six-panel cap. The short sleeves provide shoulder clearance and consistent construction cues on both rigs. The skort reads as golf at the tee and remains distinct from the tennis kit. The female cap covers the crown gaps in the unchanged optional hairstyle; its fitted band, brim and conservative crown must pass hair/scalp clearance in every proof pose.

Body, face, hair, rig, bone rest matrices and existing sockets remain unchanged. Draft accessories use existing `Head` weights (1.0), with no `Head_Hat` socket or new bone. If a socket is later required, its contract belongs in GARMENT_SPEC after 29 finishes.

## Front, side and back callouts

| Garment | Front | Side | Back | Fit / real construction |
|---|---|---|---|---|
| Polo (both) | Three individual buttons above a reinforced placket, collar points and lifted collar stand | Set-in short sleeves, a visible cuff edge, 30 mm open side vent | Longer rear tail, shoulder-yoke seam, twin hem stitch | Current corresponding-body outer polo clearance is 10 mm, with an 8 mm inner construction surface. The hanging hem and relaxed golf silhouette still require eye approval. Preserve a loose hanging hem. Stand/hem/cuffs have real thickness. No copied printed shadows. |
| Male trousers | Flat front, two slant pocket openings, shallow pressed crease, belt buckle | Pocket bag clearance, knee ease and slight break over the shoe | Two welt pocket cues, five loops distributed around waistband | Separate from body, 18–30 mm ease at thigh, 12–20 mm at calf. Cuff and waistband are closed edges. Crease reads through shape and map. |
| Female skort | Broad waistband above ordered pleats, belt/buckle visible | Pleats open below hip; inner short stays inside outer hem | Pleat rhythm continues around back | Outer skirt 25–45 mm clearance; real hem edge. Pleat depth grows toward hem. Inner shorts are separate; no body shrink-wrap. |
| Glove (each hand) | Closed fingertips, seams between fingers, thumb gusset | Individual knuckle loops, thin cuff, a closure flap | Sparse perforation dots on finger backs, unbranded closure tab | Surface derived from the actual weighted hand. Leather thickness 0.7–1.2 mm with 2–3 mm clearance. No finger is rigidly assigned to Hand alone. |
| Shoes (each foot) | White toe/vamp, black saddle area, simple unlabeled laces | Welt projects below upper; heel counter and rubber sole separate | White heel tab, black rubber perimeter | Fit around existing foot, no skeleton change. Nubs are shallow rounded volumes, visible in sole crops. Foot/toe weights follow existing bones. |
| Male cap | Six panels converge at crown; subtle centre seam; curved brim | Brim has real thickness, four restrained stitch arcs | Unbranded adjuster gap/strap | Rigid Head weights. Clear scalp by at least 5 mm. Panel seams are construction, not a graphic. |
| Female cap | Six panels over her unchanged hair; curved brim | Bound band, turned brim and stitch | Structured crown; visible hair below the band | Rigid Head weights; scalp/hair clearance is independent of rigid attachment drift. |
| Belt | Thin rectangular frame buckle, separate tongue | Five loops bridge waistband and belt | Rear loop at centre | Belt is a separate trim-mask region. Buckle is geometry, not a rectangular printed patch. |

The isolated first model covers the main shell, physical buttons, belt/buckle/loops, lead-hand surface, shoe saddle/nubs and headwear/brim stitch. Collar/vent/pockets/cuffs/inner shorts must be inspected in labelled crops and completed as needed. A source polo shell is a starting point, never evidence that every golf detail passes.

## Colour and fabric contract

| Region | Default sRGB | Recolour group | Fabric key / v2 numeric ID |
|---|---|---|---|
| Polo body, collar, placket | #EDEDED | Costume main | golf.performance_knit / 7 |
| Polo seam/cuff detail | Restrained black accent where authored | Costume trim | golf.performance_knit / 7 |
| Trousers / skort | #111214 | Costume bottom | golf.twill / 6 |
| Belt and cap | #09090A | Costume trim | golf.cap_twill; belt uses golf.leather / 8 |
| Glove | #EDEDED | Costume glove/main | golf.leather / 8 |
| Shoe upper | #EDEDED | Shoes main | golf.synthetic_leather / 8 |
| Saddle | #09090A | Shoes trim | golf.synthetic_leather / 8 |
| Sole / nubs | #121214 | Shoes trim | shared rubber (reuse 29 ID) |
| Buckle / buttons | Neutral pale grey | Fixed neutral construction detail, subject to approved trim contract | shared hard accessory profile |

Default black trousers/skort are intentional; do not accidentally turn them white by inheriting the tennis bottom's default. Reuse SetKit's existing main/derived-trim rule for colour picks. For a light pick, trim darkens by 35%; for a dark pick, trim lightens by 35% toward white; use the existing KitTint floor. The default authored black trim is retained until a user colour is picked. Test every catalog swatch, including white and black.

Golf uses the version 2 extension in candidate GARMENT_SPEC: existing IDs 0–5 retain their meanings, with twill 6, performance knit 7, leather 8 and cap twill 9. Its mask alpha encodes ID/9; tennis retains version 1 ID/5 byte-for-byte. R is AO visibility, G cavity/stitch, B trim. N is a neutral tangent normal; no garment colour or baked lighting.

Actual texture allocation per hero is 5,855,200 bytes, including full mip chains, the hidden alternative lead glove, golf atlas and legacy weave. Tops N/M are 2048², bottom and both shoes 1024², both gloves and headwear 512², ASTC 8×8; the 512² golf atlas is ASTC 4×4. UV triangles must remain nonoverlapping with 12/6/3-pixel gutters. Source-triangle export r31 has zero ambiguous top texels; final bake/import validation remains required.

Current kit triangle counts: male 78,313 / limit 97,839; female 57,055 / limit 80,962. These limits use the completed post-cloth tennis kits plus 4,000. The exporter preserves source tessellation, including nonplanar trouser quads, and retains real turned edges while omitting fully hidden main linings. Seven garment-only corrective shapes cover eight both-handed pose anchors. No body, face, hair, rest-rig, bone or socket edits are made.

## Locker items and sport selection

- Skin slot: `golf-classic-kit`, title **Golf Polo**, sports `[.golf]`, tintable. This is golf's default costume while tennis defaults stay untouched.
- Shoes slot: `golf-spikeless`, title **Spikeless**, sports `[.golf]`, tintable, white upper/black saddle default.
- Existing club slot remains available. Skin tone remains the hero's skin tone; the catalog's historical `skin` slot means whole outfit, not painted skin.
- `loadoutPayload(sport: .golf)` carries costume and shoes IDs plus the current selected colours. Changing sport swaps the kit only, preserving face, skin and rig.
- SceneKit export uses the same match hero data and the golf shell/maps. Runtime Unity and locker regions must match the same mask rule.

LockerCatalog.swift, CharacterModelPreview.swift and MatchHero exports are currently dirty/owned elsewhere. Their edits happen last after ownership release and G0. Do not install a placeholder catalog item with no real renderer/export behind it.

## Course surface targets tied to the stills

| Surface | Needle | Split | Crater | Shader construction / gate evidence |
|---|---|---|---|---|
| Fairway | Warm yellow-green ribbons linking the islands | Soft green continuous corridor beside olive scrub | Lime strip stays distinct from near-black basalt | World-space stripe direction/width per hole; subtle cross-cut. Use the existing centreline for direction without editing it. Measure G3 luma contrast on real phone captures. Contract's numeric interval is currently absent. |
| Rough | Dark irregular crown above strata | Drier olive tufts on left ridge, distinct from fairway | Small grey-green edge tufts | Distance-faded blade normal, broad low-contrast mottle, no high-frequency glitter. Rough remains its existing lie. |
| Green / fringe | Fine circular cutting around round greens | Clean ring around the high island's green | Crisp circular green on the isolated pillar | Shader-only ring from existing outline distance; exact geometry unchanged. Keep fine sheen from albedo alpha. |
| Tee | Small flat tee at the nearest end | Crisp cut edge visible at bottom of still | Rectangular crisp patch on basalt platform | No outline, height or collider alteration. Shader edge coverage respects existing polygon. |
| Sand | Pale smooth bunker body against greenery | Cream sand recesses with a lighter thin lip | Warm tan body, readable against lime | Soft ripple normal with distance fade. Lit lip brighter than body. Dampen only real edges near water. |
| Rock / cliffs | Dark horizontal ledges with mossy caps | Broken grey rock faces and ruin masonry | Clear vertical hex columns and charcoal terraces | World-position strata projection / triplanar. Cap mask from world normal; no stretching on vertical faces. Basalt heat remains the existing lava/basalt relationship. |
| Distance | Blue sea haze | Clear cool horizon | Neutral smoky haze above warm lava | Cheap profile sky tint; no new post, bloom, toon ramp or outline. |

G3 worst-cliff sheets must include all three projections at maximum face elongation. G4 uses a reproducible three-second camera path at 1170×2532, comparing registered temporal residuals to BEFORE. Do not certify shimmer by a single still.

## Implementation handoff after G0

1. Freeze delivered 15 commit and 29 report/garment spec, then refresh protected hashes and tennis BEFORE captures.
2. Install the proof rig in an isolated Unity copy, compile via heavy.sh, and capture current V4 plus both ReadyIdle heroes on 7–10 before material changes.
3. Extract shared HeroLighting/HeroCloth core while keeping today's tennis profile exact. A disabled profile path must reproduce old arithmetic; measured G10 is the authority.
4. GolfAtmosphere sets key/sky/ground/exposure/knee profile; GolfFigureExposure becomes an adapter. Keep real course shadows and contact coverage.
5. Build GolfGround/GolfRock behind the one-line GolfLook switch. Prove Metal cost before claiming that triplanar or distance fades are cheaper than Lit.
6. Complete swing retarget proof poses in work only, both sexes/handednesses; measure penetrations and head offset. Bake final garment maps against the delivered spec.
7. Bind/recolour golf shells and export locker parity, then capture all G0–G12 evidence. Stage only task-owned files, no commit. Adnan's eye remains the final gate.

## Draft 29 contract observed during preparation

`ArtDir/hero/kit/GARMENT_SPEC.md` now exists, but 29 is active and has no completion report. It uses N OpenGL +Y; M.R AO, M.G cavity/stitch, M.B trim, M.A ID/5 for six existing IDs. The 1024² polo reservation above is a planning estimate, not an approved exception to its 2048² tennis-top density target. Measure golf UV density and revise resolution/allocation while retaining the 6 MB cap before G11. Shared fabric atlas residency must be counted.

Do not renormalize existing tennis M.A bytes to make room for golf IDs. A possible shared-core extension is a material-specific fabric encoding scale/profile with the tennis default remaining exactly 5; golf can use additional registered values under its own material profile. This is a proposal requiring implementation and G10 comparison after 29 completes, not a contract change already made. No numeric IDs or golf packing have been installed.
