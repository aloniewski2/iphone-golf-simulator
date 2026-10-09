# Coastal botanical asset provenance

The Hole12 botanical pilot adapts public CC0 assets from Poly Haven. All downloaded input files are pinned by URL, byte length and MD5 in `coastal-botany-downloads.json`; the fetch tool validates each file before Blender opens it.

- Fir Sapling Medium — https://polyhaven.com/a/fir_sapling_medium — Rico Cilliers (modeling), Rob Tuytel (photography).
- Fir Tree 01 — https://polyhaven.com/a/fir_tree_01 — Rico Cilliers (modeling), Rob Tuytel (photography).
- Shrub 02 — https://polyhaven.com/a/shrub_02 — Rico Cilliers.
- Fern 02 — https://polyhaven.com/a/fern_02 — Rico Cilliers (modeling), Rob Tuytel (scanning).

License: https://polyhaven.com/license (CC0). The license permits adaptation and redistribution. These credits are retained for provenance.

`source/CoastalBotany.blend` contains packed maps and the final editable geometry. The `tools/rebuild-coastal-botany.py --fetch` pipeline downloads the pinned native sources, adapts their LOD/UV data, renders real directional bakes, packs the runtime maps and exports a new FBX. It writes the generated kit and source proof into `source/coastal-botany-generated`; it does not launch Unity or alter court/course physics. A custom cache/output directory can be passed to the pipeline.

All source foliage positions and individual leaf-card UV coverage are retained during adaptation. Three full sapling forms and an occasional mature fir provide different silhouettes. Selected native lower boughs thicken variant C. Near tree budgets are 10,395–19,016 triangles. Distant tree views are baked from the same geometry and use eight triangles. Native shrub stems are composed into irregular small clumps; far meshes reduce card count. Material maps preserve artist diffuse and normal detail.

Actual game art/material/performance gates remain separate from this source/provenance record.
