# Familiar character crowds — v1

Updated copies of the three finished venue scenes: tennis, bowling, and boxing. Earlier arena files, original GLBs, and playable character/animation files are unchanged.

## Character source and variation

The crowd uses the prepared `Male_Body` and `Female_Body` meshes derived from the user's original GLBs, not the old generic boxing audience shapes. Both retain their characteristic blank rounded heads and source outfit silhouettes. Static poses are baked from copies of the character rigs and reduced for crowd use. They share reusable mesh data inside each venue rather than carrying hundreds of live armatures.

Variations include six skin palettes, ten shirt colors, four trouser colors, three shoe colors, short/long-sleeve color blocking, subtle scale variation, and seated/resting/applause/cheering/standing pose variants. Wardrobe variation is material-based on the existing outfit geometry; it is **not** a modular clothing system or new garment topology. The source topology still limits close-up deformation quality. Crowd poses are static, not animated applause or cheers.

## Deliverables

- `boxing-familiar-crowd.blend`: replaces the generic crowd; includes full-rope and clear-view scenes.
- `tennis-familiar-crowd.blend`: spectators in the tiered seats and designated viewing areas.
- `bowling-familiar-crowd.blend`: spectators and benches outside the playable lanes.
- `crowd-character-library.blend`: reusable source-derived posed crowd meshes.
- `<sport>-gameplay.png` and `<sport>-crowd-detail.png`: rendered views of each updated venue.
- `<sport>-crowd-report.json`: actual counts and palette diversity.
- `validation.json`: structural validation of the saved scenes.

These are Blender master updates. The earlier venue FBXs do not contain this new crowd pass. Unity crowd instancing, LODs, animation, colliders, and performance profiling remain separate integration work. The golf character practice studio is unchanged; no finished golf-course environment was part of these three venue builds.
