# Coastal tennis resort — Blender v1

Reference-led 3D environment built around the supplied `tennis court 3d model.fbx`. The source FBX and character-animation studio are unchanged.

Open `coastal-tennis-resort-v1.blend`. The approved concept image is packed into the file in the hidden reference collection. Cameras: behind-player gameplay, elevated resort overview, and clubhouse detail.

## Editable scene groups

1. Court: adapted imported geometry, blue run-off, white line geometry, net tape and center strap.
2. Architecture: limestone promenade, tiered spectator terraces, clubhouse, upper terrace, pergola, and balustrades.
3. Furniture: benches, seats, umbrellas, umpire chair, and lamps.
4. Landscaping: stone pines, cypresses, planters, shrubs, flowers, and balcony vines.
5. Background: actual 3D water surface, coastal landforms, and simplified distant village buildings. No flat concept-image background is used in the render.
6. Lighting and three cameras.

The imported asset was normalized to metre-scale proportions; the playable markings use a 23.77 m court length, 10.97 m doubles width, 8.23 m singles width, and service lines 6.40 m from the net. [ITF court reference](https://www.itftennis.com/en/about-us/organisation/tennis-glossary/). The source mesh's net and surface proportions required separate axis scaling; it is not an untouched uniform-scale import.

## Status and handoff

This is an editable visual environment, not a game-ready Unity scene. The procedural Blender materials, object count, collisions, vegetation instancing/LOD, texture baking, and lighting need engine-specific optimization. No gameplay ball physics or navigation was added. The scene follows the reference's composition and major architectural features, not pixel-identical reconstruction.

The near-baseline camera corridor is deliberately open. Enclosing it with a rear fence would require a camera-friendly visibility solution. Court surface/net collision and material behavior should be authored separately from visible geometry in Unity.
