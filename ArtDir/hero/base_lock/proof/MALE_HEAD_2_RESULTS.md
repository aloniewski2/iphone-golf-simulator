GATE: MALE_FORM FAIL
GATE: MALE_SIL FAIL
CHIN: FAIL · Head likeness: FAIL

Continued the existing male mesh from the saved e87ab1835f49 checkpoint. This pass focused on the jaw/neck depth relationship and its transition into the cranium. Only existing Body_M vertex Y coordinates changed. No restart, remesh, replacement mesh, hair or female work.

| Authority view | Native difference / union | Error | 2% gate |
|---|---:|---:|---|
| bald_front | 2865 / 74937 | 3.8232% | FAIL |
| bald_back | 2743 / 74117 | 3.7009% | FAIL |

The full-body front/back errors are unchanged from this pass’s checkpoint because every X/Z coordinate is unchanged. Both still fail the direct human 2% requirement. No part crop or profile caliper replaces that gate.

| Fixed head-detail profile diagnostic | Before mean absolute depth gap | After |
|---|---:|---:|
| Eight front jaw/throat calipers | 11.682 mm | 2.044 mm |
| Five rear neck calipers | 10.472 mm | 0.910 mm |

These sampled contour gaps use a fixed, normalized head-detail calibration documented in profile-calibration.json. They are diagnostics, not an independent likeness gate. The original image and all primary silhouette camera/mask data remain unchanged. Stubble coloration was ignored; the outer contour guided the sculpt.

The anterior cranium was retracted relative to the retained chin, the nasal/lip plane moved back, the throat moved behind the chin, and the rear neck curve moved toward the reference. The final local displacement field has continuous falloffs. A direct per-layer projection that produced facial ridges was rejected before editing the main source.

Actual all-around grey and skin renders still show rough chin/jaw surfaces, a poor neck/shoulder transition, weak ear structure and unfinished facial planes. CHIN remains FAIL even though central sampled contours are closer. The other four flags cover their defined body-form defects, not full character likeness.

METRIC scale 1, A-pose, height 1.702187 m, 24,578 vertices / 49,176 triangles. Connectivity, origins, transforms, six locked cameras and three packed references are preserved. Body below 1.36 m is exactly unchanged from this checkpoint. No head triangle normal reversals against the checkpoint. Inherited topology defects were retained; no production topology PASS is claimed.

Fresh FBX reimport and actual saved Unity prefab/capture verification passed. Source and Unity FBX hashes agree. Female sources, exports and prefabs retain their prerequisite hashes. Skin, eyes, brows and lips exist only in the separate appearance preview on identical Body_M coordinates.

The continuous front/back reference conflict and native raster caveat remain in MALE_FORM_RESULTS.md. This pass does not prove an exact native-raster impossibility or a minimax optimum. Under 2% is not achieved.

Proof: 01_silhouette_overlay_m.png; 01f_jaw_neck_profile_m.png; 01e_head_all_around_m.png; 01f_head_before_after_m.png; head-all-around-skin.png; 01d_form_m.png; 03_unity_m.png.

Saved blend SHA-256: `8da71e595209e5aaae165e509525406c6fac76d2df9b69148ac991bb7c2dcf3b`
FBX SHA-256: `5d871c49c07af3e0255fd7e56a37e91ddb06d1ddbdcd7c7fd78e20774477611a`
