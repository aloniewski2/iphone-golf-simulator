GATE: MALE_FORM FAIL
GATE: MALE_SIL FAIL
CHIN: FAIL · Head likeness: FAIL

Continued the saved 8da71e595209 male mesh; only existing jaw, under-chin and lower-neck vertex depth changed. Local positive coordinate fairing reduces surface ripples, and a rounded lower-face depth field is guided by the fixed head-detail profile calipers. No restart, remesh or replacement mesh.

| Native authority view | Difference / union | Error | Required |
|---|---:|---:|---:|
| bald_front | 2865 / 74937 | 3.8232% | ≤2.0% |
| bald_back | 2743 / 74117 | 3.7009% | ≤2.0% |

Both full-body views still fail. Each native alpha mask is identical to this stage’s saved checkpoint: all X/Z coordinates were preserved. The latest direct human 2% requirement supersedes the stored goal’s older 5% rule.

The local graph depth-Laplacian diagnostic fell from 0.465 to 0.249 mm within the specified jaw/neck region. This documents reduced depth ripples, not likeness or a silhouette PASS. Front jaw/throat mean sampled gap is 2.044 mm; rear neck 0.910 mm. The calibrated central profile is retained; these diagnostic samples do not prove the full form matches.

An XYZ fairing candidate was rejected because it worsened both silhouette measurements. An overly strong local depth projection was reduced before source mutation to avoid reversing existing thin face normals. Only the accepted depth field was saved.

Actual all-around grey and skin renders show softer neck ripples and a rounder lower-face depth, but lower-jaw curvature and its transition to the neck remain visibly different. Ears, broad facial planes and provisional cosmetic features still require work. CHIN and head likeness remain FAIL.

METRIC scale 1, A-pose, height 1.702187 m; 24,578 vertices / 49,176 triangles. Body below 1.35 m, upper head above 1.54 m, connectivity, transforms, origins, six locked cameras and three packed plates unchanged. No head triangle normal reversals relative to the checkpoint. Inherited topology defects remain; no production topology PASS is claimed.

Current scale-one FBX reimport and actual saved Unity prefab/capture verification passed. Female sources, exports and prefabs retain their prerequisite hashes. Hair, female, rig and fashion work are absent. Skin and facial features exist only in a separate preview on identical Body_M coordinates.

The continuous reference conflict and native-raster caveat remain in MALE_FORM_RESULTS.md. This is not an exact native-raster impossibility proof, a minimax optimum, or an achieved 2% result.

Proof: 01g_jaw_surface_m.png; 01e_head_all_around_m.png; head-all-around-skin.png; 01_silhouette_overlay_m.png; 01d_form_m.png; 03_unity_m.png.

Saved source SHA-256: `d977d900d02058c4893d5169e690675a6379ae1e328ca26dbe2dc7648301ca6a`
FBX SHA-256: `33b46d43f7cf61c084ba111b1bd26ab1a7dff6da50e2bde1f30dd680a2a792cc`
