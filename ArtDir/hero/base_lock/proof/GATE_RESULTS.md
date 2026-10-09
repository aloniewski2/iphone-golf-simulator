GATE: BASE FAIL

Current candidate uses the two replacement bald references supplied on 2026-09-30. The main bases are bald and the independent-hair requirement passes. Overall reference fidelity is still unfinished.

Reference input/framing conflict: the frozen front and mirrored-back source traces differ by male 20.79%; female 14.34%. A single fixed orthographic silhouette cannot be within 5% of both under this framing. The existing targets and gates remain unchanged. REFERENCE_CONSISTENCY.md and reference-consistency.png document the measurement. Face and surface differences still require work independently of that conflict.

| Required line | Result | Evidence |
|---|---|---|
| Male front/back silhouette ≤ ~5% | FAIL | 11.30% / 15.06%; 01_silhouette_overlay_m.png and silhouette-metrics.json. |
| Female front/back silhouette ≤ ~5% | FAIL | 8.07% / 11.01%; 01_silhouette_overlay_f.png and silhouette-metrics.json. |
| Bald skull, jaw and facial features match the new references | FAIL | face-comparison.png: face depth, cheek/chin transitions, nose, eyelids, mouth and ear forms still differ. |
| Unity preserves the reference appearance and soft plastic shading | FAIL | 03_unity_m/f.png: hard facial shadows, generic body forms and shading differences remain. Matching imported geometry does not establish art fidelity. |
| Bald primary bases; independent optional hair; complete scalp | PASS | Both primary prefabs and body-only prefabs have one body renderer and zero hair dependencies. Deleting HairRoot retains the same body mesh. Complete scalp has zero boundary or nonmanifold edges. 04_hair_removal_m/f.png, hair-removal-heads.png and audits. |
| Separate opaque solid optional hair | PASS | Each independent hairstyle FBX has one closed hair mesh. Unity URP Lit opaque surface=0, queue=2000. Optional previews are separate from the bald base proofs. |
| Metres, foot origin and height | PASS | Male 1.704300 m; Female 1.703894 m. Source transforms and public prefab roots are identity; FBX scale 1, Unity importer global/file scale 1. Actual bounds in unity-import.txt. |
| 35–50k body triangles | PASS | Male 39,268; Female 44,708. Source, independently re-imported FBX and Unity counts agree. |
| Optional hairstyle assembly ≤50k triangles | PASS | Male 49,092; Female 49,668. |
| FBX normals and face textures | PASS | FBX scale 1, -Z/Y. Unity mesh compression off, normals imported. Head albedo: 2048, sRGB, mipmaps, Crunch off, uncompressed default importer. Pigment only; no baked directional lighting. |
| Named prefabs and full-body views all around | PASS | 05_allaround_m/f.png show eight actual Unity views every 45 degrees. Canonical front/back/three-quarter proofs and separate hair-removal proofs also retained. |
| No clothes, rig, Mixamo or Animator | PASS | Each saved source contains only its Body and optional Hair meshes, no armature. Static exports and prefabs. |
| Required proofs and gate report | PASS | Named 01/02/03 proofs and this report exist. |

Technical passes do not make BASE PASS.

Male revision: stubble pigment removed completely; lower jaw tapers into a rounded chin that closes in front of the throat. Female head geometry preserved. Eight full-body views per base expose both sides and the back.

Proof method: independent manual tracing of unchanged authority JPEGs, estimated boundary precision ±2 pixels. Native orthographic renders use frozen metres/pixel and horizontal framing translation. The metric is symmetric difference / union at alpha coverage threshold 127. Unity mask capture disables SSAO and writes linear alpha coverage as RGB. No proof warp or target adjustment.

Studio shading: dedicated HeroBaseStudioURP asset uses full-resolution SSAO radius 0.025m, intensity 0.75, 12 samples, 4x MSAA and four per-pixel additional lights. This reduces the broad facial darkening caused by the previous 0.3m/downsampled SSAO setting.

Topology limit: scalp and hair are closed. The full body is not a single watertight shell. Male: 0 body boundary edges and 6 edges with more than two incident faces; Female: 0 body boundary edges and 0 edges with more than two incident faces.

Primary bald prefabs:
/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator/Unity/Assets/Characters/HeroBase/HeroBase_Male.prefab
/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator/Unity/Assets/Characters/HeroBase/HeroBase_Female.prefab

Authoring sources with packed images:
/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator/ArtDir/hero/base_lock/blender/HeroBase_Male.blend
/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator/ArtDir/hero/base_lock/blender/HeroBase_Female.blend

Static Unity preview scene:
/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator/Unity/Assets/Characters/HeroBase/HeroBase_Studio.unity

Overall goal remains active; no reference-fidelity completion claim.
