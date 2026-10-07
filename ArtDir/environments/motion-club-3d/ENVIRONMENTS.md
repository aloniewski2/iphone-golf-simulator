# Current direction — living images + 3D locker

Per Adnan’s latest direction, only the locker uses the modeled 3D environment below. Entrance, terrace/home and loading reuse the original bundled `club-entrance.png`, `club-terrace.png` and `club-loading.png` stills.

`ClubLivingImageView` renders these with a SpriteKit cinemagraph shader: approximately 1–2 pixel foliage sway and localized bay ripples/shimmer. Green-color selection limits foliage motion; per-image water bounds avoid courts and buildings. This is a subtle animated-still effect, not generated video or true 3D parallax. The equipped character and native UI remain separate live layers. Aspect-fill adapts to TV/portrait sizes. App inactivity and Reduce Motion pause rendering and remove the shader. Locker meshes, lights and camera behavior are retained.

Actual captures: `review-live-images/`. Preview video: `ArtDir/ui/motion-club-radial-v1/runtime/Motion_Club_Living_Backgrounds.mp4`.

---

## Previous 3D implementation record (outdoor scenes retained as source assets)

# Motion Club — modeled menu environments

## Scope

Replaces the full-screen scenic PNGs on the entrance, home, locker, settings, results, sport selection, guides, campaign and loading routes with real SceneKit meshes. Native menu controls, equipment customization, practice swings, gameplay HUD and serve meter are unchanged. Venue/map thumbnail cards remain UI illustrations, not scene backgrounds.

Four simplified environments share one club material palette:

- **Entrance:** limestone arrival courtyard, stucco clubhouse, teak window casings, terracotta roof and steps.
- **Terrace:** open timber pergola, balustrade, upholstered lounge, round side table, planted pots and distant coastline.
- **Locker:** enclosed room with modeled navy locker doors, brass trim/handles/vents, timber benches and a changing mat.
- **Loading:** courtside practice space, actual court markings, net/fence geometry, seating and a basket of tennis balls.

## ImageGen → projection → bake

Built-in ImageGen generated `sources/club-material-projections.png`. It is a six-panel orthographic material source (stucco, teak, limestone, clay, navy canvas, cream linen), **not a scenic background**. All four scenes reuse this palette for consistency.

`tools/build_club_environments.py` builds the scene geometry in Blender, projects the appropriate material panel onto each surface along its dominant normal axis using a dedicated `ProjectionUV`, then creates a separate unique `BakedUV`. Cycles emission baking transfers the projected base colors into a 2048×2048 atlas per scene with 8-pixel padding. The projection UV set is removed from the exported mesh. Runtime uses ordinary UV texture sampling; there are no projectors, screen-facing scenic cards or source material sheets in the runtime scene.

Lighting is **not** baked into the albedo. SceneKit supplies the warm directional key, fill, soft shadows and distance fog. Sky is a solid renderer background; ocean, islands, clouds and palms are geometry. Roughness is intentionally high and constant for a soft family-sports look.

## Near-camera quality allocation

- Beveled edges and weighted normals on nearby furniture, paving, posts and window frames.
- Real paving joints, upholstery volume, locker handles and trim; no painted silhouettes pretending to be props.
- UV islands are weighted before packing: nearby surfaces 3×, middle-distance 1.5×, far objects 0.3×, large base geometry 0.08×, distant scenery 0.02×. These are packing weights, not guaranteed fixed texels/metre; final relative density is determined by repacking.
- Low segment counts for background clouds/islands/foliage; higher segment counts for the nearby circular table.
- See `bake-report.json` for final per-scene triangle counts. Each scene stays under 100k triangles with one baked material, plus the existing separate character renderer.

## Runtime

`GolfArcade/Unity/ClubEnvironmentView.swift` decodes the baked mesh assets and renders them in a live SCNView. The shared `IslandBackdrop` routes to it. Geometry and UVs use the existing project convention (Y up, flipped image V), exported explicitly from Blender. Baked textures are bundled under `GolfArcade/Unity/ClubEnvironments/`.

Camera movement is a quiet 20-second ±0.24 m lateral orbit about the viewing target, at a requested 30 fps. The foreground moves relative to the coast. Reduce Motion freezes the camera, and teardown stops actions/rendering. Existing menu hinge transitions still operate on the composed screen. Characters remain in the existing separate live SceneKit preview; this does not migrate their rendering into Unity or rebuild their meshes.

## Rebuild

From repo root:

```sh
/Applications/Blender.app/Contents/MacOS/Blender --background --threads 6 --python ArtDir/environments/motion-club-3d/tools/build_club_environments.py -- "$PWD"
xcodegen generate --spec project.yml
xcodegen generate --spec project-unity.yml
```

Editable `.blend` files with packed source/baked textures are under `blender/`. Mesh-only Blender renders and actual TV/phone captures are under `review/`. The left/right terrace captures deliberately move the camera to show that texture sticks to geometry outside the menu viewpoint.

## Verification and limits

Simulator build and `ClubEnvironmentTests`: validate mesh/UV consistency, atlas presence, triangle budgets, camera/Reduce Motion lifecycle, actual TV/phone screens, and alternate geometry viewpoints. Physical iPhone/AirPlay GPU performance has not been measured; 30 fps is a target rather than a device benchmark. The approved mockups guide architecture, landscaping and props. The implementation retains simplified game geometry; it does not reproduce the photoreal detail of the generated concepts. No production screenshot is used as a runtime background.

## Richer resort implementation (approved mockups v2)

`tools/resort_details.py` adds the shared modeled kit. Entrance gains arched clubhouse recesses, curved striped awnings, lanterns, terracotta roof ridges and planted arrival beds. Terrace gains vine-wrapped pergola posts, upholstered seating, pitcher/cup, layered gardens, distant courts and a pavilion. Locker gains timber ceiling beams, open arched garden windows, equipment shelves, towels, a duffel, clock and ferns. Loading gains feathered palms, windscreen, floodlights, umpire chair, courtside gear and clubhouse architecture. The center of the practice court remains clear.

Near props use beveled forms and textured wood/stone/cloth. Palm leaflets and planting are real geometry. Distant plants use simpler crowns. One baked material per scene consolidates the kit for rendering. Source hex colors are converted from sRGB to linear before emission baking. A reserved atlas palette strip gives tiny solid-color leaves/clouds stable UV samples, preventing dark bake holes and mip contamination. Textured surfaces retain the projected ImageGen materials.

Directional shadow projection uses a fixed 35 m footprint and 1–75 m depth range so the very large sea mesh cannot dilute foreground shadow resolution. Existing camera drift, Reduce Motion and hinge transitions remain active.

### Review

Approved concepts: `mockups-v2/`. Actual TV, portrait and alternate-angle runtime captures: `review-v2/`. Editable authoring scenes: `blender/`. Final counts are recorded in `bake-report.json`. These are actual rendered meshes, not the concept images used as backdrops.

Physical-device/AirPlay performance remains unmeasured. The hero retains its separate existing renderer, so it does not cast shadows into the environment scene. Do not interpret the simulator captures as a merged Unity scene or a physical-device frame-rate measurement.

### Final geometry budget

| Scene | Triangles | Atlas |
|---|---:|---|
| entrance | 83,056 | 2048 × 2048 |
| terrace | 80,256 | 2048 × 2048 |
| locker | 64,592 | 2048 × 2048 |
| loading | 77,128 | 2048 × 2048 |

Portrait uses a 55° vertical camera field of view; TV uses 60° horizontal. This keeps the club visible behind the phone avatar instead of exposing an oversized sky area. The atlas palette strip stores sRGB pixel values; shader colors use linear values. `refresh_palette.py` updates this strip in the runtime PNG and packed Blender atlas without rebuilding geometry.

### Verification — 2026-09-29

Final simulator build/test succeeded: 4 `ClubEnvironmentTests`, 0 failures. Checked mesh/UV consistency, atlas availability, <100k triangles per scene, camera drift/Reduce Motion, actual TV menus, portrait menu and left/right alternate views. Reviewed final runtime images after the palette and portrait correction. `git diff --check` passed. No physical-phone installation or performance benchmark was performed.
