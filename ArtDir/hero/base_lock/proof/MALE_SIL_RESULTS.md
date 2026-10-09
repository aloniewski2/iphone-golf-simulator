GATE: MALE_SIL FAIL

Authority: ONLY `male_body_bald.jpg`, front/back, **each ≤2.0%**, per the latest human instruction. This replaces the historical 5% PASS for the earlier candidate.

| Authority view | Difference pixels | Union pixels | Blender error | Unity error | Blender–Unity difference | 2% gate |
|---|---:|---:|---:|---:|---:|---|
| bald_front | 2,831 | 75,001 | 3.7746% | 3.8826% | 0.6414% | FAIL |
| bald_back | 2,807 | 74,222 | 3.7819% | 3.7642% | 0.6001% | FAIL |

Continued the existing `HeroBase_Male_Silhouette.blend`, revising only `Body_M` vertex coordinates. No remesh, replacement mesh, female work, hair, rig or face textures. METRIC scale 1; A-pose; height 1.702187 m; 24,578 vertices / 49,176 triangles. Connectivity, body transform/origin, all six locked reference cameras and three packed plates are unchanged. This fourth resumed stage repairs stretched distal-hand/thumb coordinates using verified existing vertex correspondence, a continuous wrist falloff, shared front/back palm/finger envelopes and reduced excessive distal-hand depth. All vertices outside the distal-hand region, including the head/neck, torso and legs, are exactly unchanged from this stage checkpoint. Full head likeness and CHIN remain FAIL; fingers remain an unfinished blockout. Cosmetic features are a separate appearance preview.

Metric: count(reference XOR render) / count(reference OR render), each in the original 1280×720 image. Reference masks are the frozen source-only masks; Blender alpha >127. No refitting of masks, cameras, origins or image scales. Multiangle sheet views are excluded from the numeric gate; their locked cameras remain packed.

The frozen front and reflected back masks disagree by 6.2479% in continuous world space. One closed mesh has a shared X/Z outline in these opposite orthographic views. The Jaccard triangle inequality places at least one continuous view at ≥3.1239%. This documents why a clean physical 2% fit conflicts with the authority pair. Native raster sampling adds boundary quantization, so this continuous bound is not presented as an exact impossibility proof for the separate native pixel gate. The actual gate remains the unchanged native XOR/union measurement above; the model is not claimed to attain the minimax optimum. Cameras, masks and reference images were not altered.

Proof: `01_silhouette_overlay_m.png`.
Saved blend SHA-256: `d39c6c5e5157ff15428759c01eaa227a09ea1bcd4b0d1a51acfe4110795d2413`
