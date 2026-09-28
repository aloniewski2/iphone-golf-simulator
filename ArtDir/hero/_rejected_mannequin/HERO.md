# Hero_01 — BlenderHeroPolish review

**One hero. Polished files exported; awaiting Adnan’s visual approval before Mixamo.**

## Assessment against plate 01

The design identity is approximately 80% matched in this author’s subjective assessment: blond hair, visor, big expressive brown eyes, warm skin, white/navy/orange polo, navy shorts, chunky limbs and white/orange shoes. This is **not** a measured similarity score or an assertion that the plate’s finish has been reached. Adnan rejected the previous version; that rejection stands. This revision needs a fresh review using `Hero_01_compare.png`.

Remaining visible differences: the plate has more natural hair flow, a softer integrated face, more relaxed garment folds, a fuller collar and more detailed shoes. Our side silhouette still exposes the layered construction of the eyes and hair. The rest pose is intentionally different from the plate’s equipment-holding pose. No racket is included.

## A — Starting gaps and changes

| Area | Starting severity | Applied polish |
|---|---|---|
| Eyes | Critical: black protruding ovals | Fixed the flat-black-eye treatment: shaped white sclera, brown radial iris texture, smaller pupil, two highlights, curved upper lids and brows. Eye depth reduced after side review. |
| Face / skin | Critical: chalky egg form | Warmer peach albedo, painted blush, widened lower cheeks, shallow eye sockets, soft cheek bulges, nose bridge/tip and thin smiling mouth. Smile moved onto the face after side review. |
| Hair | High: undifferentiated cap | Fixed the single-blob treatment: seven rounded swept crown locks and six tapered side locks over a supporting cap. Three blond values. Subdivision applied to remove pointed facets. |
| Hands | Critical: mitten silhouette | Fixed mitten hands: three individually indicated rounded fingers plus a thumb, fused to each palm, smoothed knuckles and wrist bridges. Enlarged the hand silhouettes slightly. No individual finger bones yet. |
| Clothes | High: weak accents | Broader orange collar piping, navy/orange sleeve cuffs, orange placket, larger orange shoe accents, rounded toe forms and softened shoulders. |
| Materials | High: same chalk response throughout | Five material instances sharing one atlas and the same standard Principled shading model, with separate skin/cloth/hair/visor/eye roughness. No custom shader, pores, clearcoat or 4K maps. |
| Review lighting | High: flat/washed-out review | Warm key, cool fill and rear rim; reduced exposure after checking skin/trim. All three views use the same setup. |

## Reuse and scope

The rejected `Hero_01.blend` design/scaffold remains the single hero: torso and limb proportions, shorts, 22-bone rest rig and socket locations retained. Head forms, eye construction, hands, hair locks, trim, materials and review lighting were revised through `build_hero.py`. Hair and visor remain separate cosmetic meshes. Original male/female prototypes and runtime game assets were not edited.

No Tripo, Mixamo, Unity import, Higgsfield, second body or animation work was performed.

## Outputs

- `Hero_01.blend`: canonical editable asset, one `Hero_01` collection plus a separate `REVIEW_ONLY • cameras lights floor` collection.
- `Hero_01.fbx`: rest A-pose, one armature, three skinned meshes, five sockets. Review objects excluded.
- `Hero_01_front.png`, `Hero_01_side.png`, `Hero_01_threequarter.png`: actual Blender orthographic renders, 1000 × 1200.
- `Hero_01_compare.png`: locked plate character crop beside current three-quarter render. No retouching of the rendered hero.
- `Hero_01_review.jpg`: plate and all three current views.
- `Hero_01_Atlas.png`: one packed/embedded 1024 × 1024 sRGB atlas.
- `build_hero.py`, `compose_review.py`, `verify_hero.py`: local reproducible geometry, review layout and validation.
- `validation.json`, `verification.json`: measured results from this revision.

## Proportions and geometry

| Metric | Final |
|---|---|
| Standing height | 1.7466 m, feet at Z = 0 |
| Chin | Z = 1.313 m |
| Crown-to-chin / height | 24.82% |
| Body triangles | 28,174 |
| Hair triangles | 6,768 |
| Visor triangles | 1,616 |
| Total triangles | **36,558** |
| Meshes / armatures / bones | 3 / 1 / 22 |
| Texture | One 1K atlas |
| Material instances | 5, one shared shading model |
| Skinning | Normalized analytic rest weights; maximum 2 influences |

This is a hero review budget, not a measured iPhone frame-time guarantee. Eye detail accounts for part of the increased mesh budget. No LOD pass has occurred. Major limbs retain ring topology; hands are remeshed/decimated. Facial forms and garments contain layered/intersecting surfaces; the complete character is not a single watertight surface.

## Palette and materials

- Skin: **#F9BF85** base, warmer painted cheek tint; deliberately warmer than the bible’s default #FFE0C2 to follow plate 01.
- Cloth: #FAFAF7; navy #283952; orange **#FF6B3D**.
- Hair: #E9B34B, #BC8434 and #F5CC72.
- Iris: painted brown with a dark edge; separate small dark pupil and white highlights.
- Blender roughness: skin .52, cloth .72, hair .53, visor .43, eyes .20. No metallic or clearcoat effects. Eye highlights are the small deliberate glossy exception.
- URP-friendly texture/material inputs are provided; FBX does not guarantee identical URP shader reconstruction. Material mapping belongs to the later authorized Unity phase.
- Atlas is 4 × 4 broad swatches plus projected face paint and radial iris islands. It is **not** a unique full-body garment unwrap. More detailed clothing textures would need dedicated UV work.

## Review lighting

Three soft area lights: warm key 380 W / 3 m, cool fill 120 W / 3 m, rear rim 280 W / 2 m. Neutral world strength .14. AgX Medium High Contrast, exposure −.20. Cycles 48 samples. Same floor and lighting for all three orthographic views. This studio scene is for mesh/material review, not a Unity lighting implementation or runtime performance proxy.

## Attachment sockets

Bone-parented empties in Blender rest coordinates (X right, −Y forward, Z up):

| Name | Parent | Position, meters |
|---|---|---|
| Hat | Head | (0, 0, 1.715) |
| Hand_R | Hand.R | (−0.680, −0.020, 0.770) |
| Hand_L | Hand.L | (0.680, −0.020, 0.770) |
| Back | Chest | (0, 0.145, 1.120) |
| FaceExtra | Head | (0, −0.195, 1.510) |

Anatomical right is screen-left in the front view. Grip offsets and animated cosmetic clearance are not yet tested.

## Verification

Final FBX was imported into a fresh Blender scene: one armature, three meshes, five sockets, embedded 1K atlas and no actions. Source checks found identity mesh transforms, neutral rest pose, zero unweighted vertices, zero bad weight sums and zero zero-area triangles. All three final renders inspected.

Not yet verified: Mixamo acceptance, Humanoid mapping, motion deformation, racket/club contact, facial animation, LODs or device performance. These checks do not certify the current analytic skinning for play.

**STOP — Adnan: approve or reject this polish from `Hero_01_compare.png`. No Mixamo until approval.**
