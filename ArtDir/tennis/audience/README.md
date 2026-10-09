# Premium tennis audience source

The current accepted-at-crowd-detail Source6 uses the approved hero Body/Face and fitted sporting kit. Original inputs are frozen copies under inputs/ with hashes in source-manifest.json. Main-player resources and original animation sources are untouched.

- source/TennisSeatedHero5.blend: six seated variants; rigid BODY/HEAD/ARM_L/R parts, matching near/far child meshes. The accepted upper bodies are retained; far sneakers and ankle tubes are now sealed curved exterior volumes.
- source/TennisPromenadeHero5.blend: six walking variants, one active renderer per near/far detail and eleven cosmetic bones. Seven joints form the continuous weighted lower body; the four accepted upper parts retain constant one-bone rigid motion. Complete shirts are BODY/ARM; no shirt polygons follow thighs. Original exposed skin and fitted lower garment weights are preserved through bounded nearest-source interpolation. Sneakers are wholly foot-bound; sock blending follows the actual calf/ankle.
- tools/bake-tennis-audience.py: reproducible Blender5.2.2 source bake. It writes staging5/Tennis{Seated,Promenade}Hero3.blend/fbx; the Hero3 asset filename preserves the runtime resource identifier. Use --seated for seated figures.
- tools/sealed_far_sneaker.py: measured curved source exterior with a flat closed sole band,260triangles/shoe. No whole calf/foot convex hull. All four source shoes have zero boundary/nonmanifold edges and positive volume.
- tools/render-walker-joints.py: independent reset per pose, near/far left/right step and close knee source proofs. Blender linear deformation matches Unity's conventional skinning mode.
- tools/render-audience-source.py --seated: matching seated near/far/face/raised-arm source proofs.

Reproduce from the repository root:

    /Applications/Blender.app/Contents/MacOS/Blender --background --python ArtDir/tennis/audience/tools/bake-tennis-audience.py
    /Applications/Blender.app/Contents/MacOS/Blender --background --python ArtDir/tennis/audience/tools/bake-tennis-audience.py -- --seated
    /Applications/Blender.app/Contents/MacOS/Blender --background --python ArtDir/tennis/audience/tools/render-walker-joints.py
    /Applications/Blender.app/Contents/MacOS/Blender --background --python ArtDir/tennis/audience/tools/render-audience-source.py -- --seated

Source proof is a candidate gate only. Actual Unity near/far/cheer capture, all-six gait film, renderer inventory and automatic frame counters are mandatory final checks. Diagnostic isolated images preserve the production mesh/pose/material/light while excluding foreground blockers; normal world views and game film retain all scenery.

Source6 retains all source5 near meshes and all female far meshes exactly, and changes only male far shorts. Two actual leg openings/crotch contours replace the whole-garment convex bridge. Reproduce the isolated change against the frozen accepted source5 baseline (do not regenerate that baseline):

    /Applications/Blender.app/Contents/MacOS/Blender --background --python ArtDir/tennis/audience/tools/bake-far-shorts-candidate.py
    /Applications/Blender.app/Contents/MacOS/Blender --background --python ArtDir/tennis/audience/tools/bake-far-shorts-candidate.py -- --seated
    /Applications/Blender.app/Contents/MacOS/Blender --background --python ArtDir/tennis/audience/tools/apply-far-shorts-only.py
    /Applications/Blender.app/Contents/MacOS/Blender --background --python ArtDir/tennis/audience/tools/audit-source6-preservation.py

The transplant is mandatory: fully regenerated cap geometry can vary in ordering/weights despite an unchanged algorithm. Exact accepted baseline meshes are frozen as inputs/Accepted_Tennis{Seated,Promenade}Hero5.blend; the final transplant/audit reads these immutable baseline files. The original source5 generator remains available for history but is not used to regenerate the accepted baseline during a source6 rebuild. source/Tennis{Seated,Promenade}Hero6.blend and staging6/Tennis{Seated,Promenade}Hero3.fbx are the final editable/export candidates. Male far totals are7,394walking/7,466seated; female far totals remain7,879walking/7,833seated. The preservation audit checks position/palette/weight multisets at1e-6 over all45 untouched mesh representations. Actual Audience6 near/far views show separate male shorts legs; Costs19 and exact36+6+0 legacy inventory are recorded. Final Promenade7 actual-frame cadence/movie review is independent of source preservation.
