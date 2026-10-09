GATE: MALE_FORM FAIL
GATE: MALE_SIL FAIL
Head likeness: FAIL

Resumed the saved male mesh from the ec8ff3b checkpoint, without restarting or remeshing. This stage worked only on the head/neck above 1.38 m, in sequence: cranial dome, jaw/chin, forehead and ear volumes, broad face planes, then the shared front/back head contours.

| Native authority view | Before: full body | After: full body | Before: head crop | After: head crop |
|---|---:|---:|---:|---:|
| bald_front | 3.8830% | 3.8232% | 3.0197% | 2.2640% |
| bald_back | 3.7430% | 3.7009% | 2.8102% | 2.2770% |

The head crop uses original image rows 0–159 in each native authority image. It is a diagnostic for this part, not a substitute for the full-body 2% gate. Both full-body views still fail.

Metres, A-pose, height 1.702187 m; 24,578 vertices / 49,176 triangles. Existing connectivity, origins, transforms, six locked cameras and three packed plates preserved. Body below 1.38 m exactly preserved. No female or hair changes. No reversed head triangle normals against the resumed checkpoint. The inherited topology was retained, including its pre-existing defects; no production topology PASS is claimed.

The dome is now rounded through its top and sides; the lower face is narrower and less projected, with a smoother throat transition. Ear volumes and shallow orbital/nasal planes were revised in the existing mesh. Ear attachments/cartilage, jaw-to-neck proportions and face planes still differ from the reference. Head likeness remains FAIL.

Skin/eyes/lids/brows/lips are a separate Blender appearance preview, rendered from this exact current mesh in eight directions plus top. They do not alter the grey gate source or manufacture a silhouette PASS.

Fresh scale-one FBX export, independent coordinate reimport and saved Unity prefab/capture verification passed. The original bald reference masks and cameras are unchanged. The frozen authority pair’s continuous outline conflict remains documented in MALE_FORM_RESULTS.md, with its native raster caveat. Further modelling can improve likeness and the current fit; under 2% is not demonstrated.

Saved source SHA-256: `e87ab1835f49e803a586c8c1a996eb38b1585397d19ec9e4ed58acad6fca5538`

Proof: 01_silhouette_overlay_m.png; 01e_head_all_around_m.png; 01e_head_before_after_m.png; head-all-around-skin.png; 01d_form_m.png; 03_unity_m.png.
