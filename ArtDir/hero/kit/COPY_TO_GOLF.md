# Copy the approved clothing detail standard into golf

Use `GARMENT_SPEC.md` + `GARMENT_STANDARD.json` as the shared standard. Copy `GARMENT_SELECTION_TEMPLATE.json` for each outfit; replace the selection identity and garment asset references when its real assets are ready. This is an authoring manifest, not a runtime loader or an already installed golf selection.

## Exact starting points

- Construction meshes: `Unity/Assets/Resources/Tennis/KitsV3/standard_male_kit.fbx` and `standard_female_kit.fbx`; editable/bake-only sources in `work/cloth-overhaul/export/`. Copy into the golf work area before editing.
- Neutral N/M maps: `Unity/Assets/Resources/Tennis/HeroDetail/Kit_<Sex>_<Piece>_N.png` and `_M.png`. Preserve importer settings, alpha and mip chains.
- Shared fabric atlas: `Unity/Assets/Resources/Tennis/HeroDetail/Cloth_FabricAtlas.png`.
- Lighting/material behavior: `TennisCloth.shader`, `MatchHeroLook.cs`, and the garment path in `MatchHeroSurfaces.swift`. Reuse their supported fabric/trim behavior; avoid a second independent copy of the equations.
- Colour selection: existing SetKit semantic roles and 35% derived trim, mirrored by MatchHeroData. Preserve per-sport catalog selection and the existing loadout format.
- Approved appearance: `ArtDir/screenshots/cloth_overhaul/CONSTRUCTION_SHEET.png`, `FABRIC_SHEET.png`, `LOCKER_PARITY_male.png`, `LOCKER_PARITY_female.png`, match camera sheets and AFTER films.
- Working data-only example: `work/cloth-overhaul/locker/DUMMY_POLO.json` and `spec_reuse_proof.json`. The same executable, mesh, maps and rig render a different polo pick without shader/code changes.

## Copyable art brief

> Match the approved tennis clothing detail level for every golf selection. Keep the body, face, rig, existing sockets and bone names. Build a loose fitted shell with visible collar roll/stand, placket/buttons, rib cuffs, turned stitched hems, real waistband and pocket openings, moderate baked folds, and sport-appropriate pleats or trouser creases. Shoes retain real flat laces, panel stitches, midsole/outsole separation and readable sole construction. Author colour-neutral tangent N and M.R AO / M.G cavity / M.B trim / M.A fabric ID divided by five. Use the shared supported fabric IDs and atlas, the seven role API, 35% derived trim and screen-space weave fade. Re-bake when shape or UVs change. Apply the same detail in the SceneKit locker and Unity match. Prove close-ups, match readability, all selection colours, swing fit, shimmer, memory, Metal build and device timing before registering the selection.

Golf's existing design remains `golf/GOLF_KIT_DESIGN.md`: golf trousers, belt, glove, cap/visor and spikeless traction are authored for that outfit. The shared standard supplies quality and material behavior. Destination geometry and extra fabric families still need their own implementation/proof; this handoff does not certify unfinished golf work.

## Selection acceptance

An existing garment duplicate/recolour should need asset/catalog data only. A new shape needs geometry plus correctly re-baked maps. A new fabric family needs a deliberate versioned material profile; never renormalize these six fabric IDs or call mesh “leather.” Register the outfit only when Unity and locker can both load it. Selecting it, changing sport and changing every swatch must preserve detail, rig and the existing save/loadout format.

The approved tennis visual baseline is fixed. Keep its proof images available beside golf's new crops; use the same construction checklist and phone shimmer method rather than relying on a single beauty shot.
