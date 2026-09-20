# Quaternius nature assets

Official source: https://quaternius.itch.io/stylized-nature-megakit
Creator: Quaternius. Pack: Stylized Nature MegaKit, free Standard edition.
Downloaded 2026-09-19 through the ordinary free download flow. No purchase.
License: CC0 1.0; original License_Standard.txt is retained alongside selected sources.
ZIP SHA-256: 298f6732b872e4cf7b30e6e7abf9641c7f6dc6b326df37ac089533ed7e3d58c9

Selected glTF sources and textures are retained in glTF/. Runtime conversion:
`node scripts/import-nature.mjs art/third-party/quaternius/glTF GolfArcade/Resources/Nature`

## Current crown-fit cleanup and woodland integration

September 19, 2026: the importer now clips bark triangles above 3.7 yards in its pre-normalized source space, inside the new opaque lower crown. Edge intersections interpolate position, UV, normal and vertex color; final normals are normalized. Lower sculpted trunks, flared roots and lower branches remain; source glTF is untouched. This removes the old branch tips that protruded through the newly shaped crowns. Runtime tree variants 3–5 are now **2,417 / 2,076 / 2,490 triangles**. Their 8-yard asset height and 3.6-yard radial envelope are unchanged. The following paragraph describes the prior crown-rewrite checkpoint, before this pruning.

The playable course additionally reuses these assets in up to eleven six-tree woodland groups. Each group is a cullable mesh with shared bark and up to three crown materials; empty material/index groups are removed to avoid missing crowns in SceneKit. No colliders are added, and every site and transformed mesh is checked against playable lies. Woodland uses the same source licenses and consumes no generation credits.

## Previous crown rewrite

The separate importer preserves modeled trunks, branches, bark texture and vertex colors. The original leaf-card render was rejected as sparse and shimmering against the rounded film reference. On September 19 the six isolated fitted crown lobes were replaced with three connected lower pear-shaped lobes and a taller tapered leader. Deterministic variation changes their height, tilt and green hue. Analytic finite-difference normals follow the actual rippled surfaces. The finished asset remains normalized to eight yards tall and a 3.6-yard radial envelope. The three shipped grove variants (3–5) now have 3,241 / 4,816 / 3,418 triangles, fewer than the previous 3,625 / 5,200 / 3,802. Runtime decorative groves are stretched vertically by 1.25 without enlarging their reserved ground footprint; authoritative collision trunks retain their own exact dimensions. Backdrop spacing now forms irregular three-tree groups in two depth layers. Runtime bark is downsampled to 1K; the source 2K atlas is retained. The decoder also supports the original alpha masks for future suitable props. This never edits the authored golfer or motion library. These are CC0-derived models, not Higgsfield generations, and consume no Higgsfield credits. Current visual improvement is not overall film-quality acceptance.
