# Reusable sports garment contract — v1

White and black default kit, no branding. Colours come exclusively from runtime uniforms. Body, bones, clips and sockets are read-only. New garments use the existing six skinned piece names and seven semantic roles; no per-garment shader code.

## Files and channels

Resources/Tennis/HeroDetail/Kit_<Male|Female>_<Top|Bottom|SockL|SockR|ShoeL|ShoeR>_N.png: tangent-space RGB normal, +Y/OpenGL convention, imported as Unity NormalMap, linear, ASTC 8x8. No colour.

Matching _M.png: linear RGBA data; R = AO visibility (1 unoccluded, minimum 0.70), G = cavity/stitch visibility (1 uncreased), B = main/trim mix (0 main, 1 trim), A = fabric ID / 5. Import Default with alpha preserved, ASTC 8x8. No baked garment colour. Legacy _BaseMap has been folded into M.G and removed from TennisCloth.

Top 2048², bottom and shoes 1024², socks 512². Target outer surface density: top 9–18 px/cm; bottom 5.5–14; shoes 8–20; socks 5–10. Report measured density and island occupancy; do not upscale without a memory budget. Non-overlapping UV islands, >=12 px gutter at 2048 (6 px at 1024, 3 px at 512), fill gutters by the nearest island, never dark empty space. Padding must be rechecked at distant mips. Bake the high construction surface over the low shell, with its UV tangents and a maximum 3 mm fold displacement.

## Fabric IDs

| ID | A byte | Surface | Smoothness | Charlie sheen | Detail |
|---|---|---|---|---|---|
| 0 | 0 | body pique / shorts knit | .10 | .55 | staggered pique, 8 threads/tile |
| 1 | 51 | collar, cuff, waist rib | .13 | .38 | fine rib, 12 ribs/tile |
| 2 | 102 | shoe upper mesh | .16 | .24 | open mesh, 8 cells/tile |
| 3 | 153 | rubber outsole / toe reinforcement | .04 | 0 | no weave, baked tread |
| 4 | 204 | foam midsole | .08 | 0 | no weave, faint baked texture |
| 5 | 255 | flat laces / drawcord | .10 | .18 | tight rib |

Shared Cloth_FabricAtlas.png is 512², ASTC 4x4, linear; quadrants: bottom-left pique, bottom-right rib, top-left mesh, top-right neutral. Each quadrant has an 8 px repeat gutter. RG = tangent slope normal, B = thread visibility with mean 1/1.28. Derivatives are calculated before frac; individual threads fade below 3 px/thread and disappear before 1.4 px/thread. Rubber/foam use no thread or weave normal. Knit alone gets grazing fuzz. WeaveAngle rotates both UVs and normal slopes (the old male 41-degree correction must remain for legacy UVs).

## Colour and API

SetKit keeps Kit_Shirt, Kit_ShirtTrim, Kit_Shorts, Kit_ShortsBand, Kit_Shoe, Kit_Sole, Kit_Sock. Main shader colour is lerp(_BaseColor, _TrimColor, M.B). Default main is sRGB .93 and default trim (.035,.035,.0401). A picked main follows KitTint: max(.93*pick, black floor). Its trim follows KitDerive: darken 35% if luminance >=.5, otherwise lighten 35% towards white, then KitTint. Shirt/shorts trim submeshes keep their role, and the main material receives the same trim uniform. Sole and socks retain the authored colour according to the existing API. Shoe accents in the upper mask follow the shoe pick; the separate outsole retains the default neutral trim.

## Drop-in garments and skins

Author the garment over the current read-only body; bind to the existing bone names, <=4 normalized weights, and use the existing kit renderer/socket names. Export the six kit pieces with the existing rig and no animation. Replace the appropriate N/M resources using the above names, fabric IDs and padding. Use the semantic role materials; runtime discovers N/M by sex+piece and sets all maps itself. A new skin colour or kit pick needs data only, never a new shader branch or material keyword. Whole garment swaps use the same prefab/renderer contract; selection/catalog data must register the new asset. No new bones. New silhouette detail <=6000 additional kit triangles per hero; resident kit maps including shared atlas <=6 MB (6,000,000 bytes) per hero.

## Proof conditions

Use ClothProofRig's fixed targets/cameras and real Tennis court lighting. Construction crops, four skirt views, all motion poses and phone shimmer film are mandatory. White L*: shirt 90–95, bottom 88–95; authored black trim 8–20. Folds must darken fixed lit crops 8–20%, never >30%. Device performance and compiled Metal instruction/sample counts must be measured, not inferred from shader source or Mac editor timing. The user's eye approval is a separate required gate.

## Shared selection quality standard

This is the approved clothing detail baseline for **tennis and golf selections**, not a tennis-only art recipe. Adnan approved the delivered clothes on 2026-10-05 and requested this reusable standard. Every selectable outfit inherits the same construction, neutral map channels, trim rule, fabric response, distance filtering, rig safety and locker/match parity. Sport changes silhouette, fit and authored construction; it does not lower the close-up quality bar.

Copy `GARMENT_STANDARD.json` as the numeric authoring contract and `GARMENT_SELECTION_TEMPLATE.json` as an outfit authoring manifest. `COPY_TO_GOLF.md` names the exact source assets, the detail to retain and the checks required for a golf selection. These are authoring/handoff files; the current runtime does not parse these JSON manifests. The existing Unity and SceneKit binders remain the implementation authority. Register a finished outfit through the existing sport-specific catalog/asset path when its assets are ready.

For a duplicate or recolour, retain the delivered mesh, N/M and atlas; change selection data and colour picks only. For a different garment shape, transfer the construction recipe and re-bake over its own UVs. Copying tennis normals onto differently unwrapped trousers would produce misplaced seams. Six fabric IDs and the exact M.A encoding stay fixed. Golf's proposed twill/leather/headwear profiles are not silently mapped to misleading tennis IDs: use the existing supported profile deliberately, or extend a separate versioned profile with new compile, memory, parity and timing proof. A new profile must leave this v1 encoding intact.

The completed dummy polo proof establishes data-only reuse of an existing garment. It does not certify the future golf geometry or catalog selection; those require their own swing, fit, resource and selection checks. Keep the final approved tennis proof sheets as the quality reference.


## Golf extension — encoding version 2 (30b candidate)

The delivered version 1 textures and ID/5 decoder remain byte-unchanged. Golf material instances explicitly set `_FabricVersion=2`; only their new M maps encode A=ID/9. Existing IDs 0–5 retain their meaning. No map may be interpreted under a different version. Both versions use HeroCloth.hlsl and the existing TennisCloth shader name.

| ID | Material | smoothness | sheen | atlas quadrant |
|---|---|---|---|---|
| 6 | trouser/skort twill | .09 | .24 | 0,0 |
| 7 | polo performance knit | .12 | .32 | 1,0 |
| 8 | glove/shoe synthetic leather | .18 | .10 | 0,1 |
| 9 | cap twill | .08 | .18 | 1,1 |

Performance knit is the golf polo choice: a relaxed, even shell with a self-fabric collar and three-button placket. The separate 512² golf atlas carries neutral normal slopes and thread break, including twill diagonals and leather grain. Version 1's 512² atlas is unchanged. Golf defaults to white polo/glove/shoe upper, black trousers or skort/headwear, and black trim. The existing 35% trim rule applies to picked main colours. Masks remain R AO >=.70, G cavity/stitch, B trim; no baked colour or light. Top N/M 2048; bottom/shoes1024; gloves/headwear512, ASTC8x8 with mips. Atlas ASTC4x4. Target per outfit including the hidden alternative lead glove <=6MB; final measured ledger required.

No Head_Hat socket is added: existing headwear is rigidly weighted to Head, with no new bones. Drift is measured separately from hair/scalp clearance. Golf swing extremes are proof only; no match-hero golf swing is installed into GolferView.

This extension is a candidate contract until maps, colour grids, Metal/device and eye gates pass. Production GARMENT_SPEC is not yet replaced.
