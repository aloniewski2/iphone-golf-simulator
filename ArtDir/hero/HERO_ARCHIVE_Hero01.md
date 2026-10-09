# Hero_01 — HeroFromPlate

**Status: new hero exported for Adnan’s visual approval. Stop before Mixamo.**

## Source and archive

All 16 files from the rejected hero directory were moved into `_rejected_mannequin/` before work began. None of that mesh or its geometry-building script was used as the new body.

Identity authority: `../plates/01_hero.png`, with `../plates/review-sheet.jpg` inspected for consistency. Proportions/material requirements came from `/Users/adnanyonathan/Downloads/adnan-party-sports-art-bible.md`; phase rules from the supplied `AGENTS.md`. This brief explicitly authorizes a replacement hero and the turnaround → Tripo → Blender sequence.

The new geometry source is **`../blockout/Hero_01_tripo.glb`**, followed by the Blender changes below. It has not been imported into Unity.

## Turnarounds and Tripo provenance

- One built-in imagegen generation, using plate 01 as the strict image reference. No text-only character generation and no rejected-model input.
- `turnarounds/turnaround-sheet.png`: original four-view output.
- `turnarounds/front.png`, `side.png`, `back.png`, `34.png`: extracted views, centered on white 1024-square canvases. Same blond/visor/polo/shorts identity. Back details are inferred because the plate does not show them.
- Full generation brief and extraction notes: `turnarounds/TURNAROUNDS.md`.
- Tripo task **2286dbbb-2ddf-4f3d-8fff-03c30680a07d**.
- Endpoint: multiview-to-model. Model: **v3.1-20260211**.
- Inputs: front, left-facing side, back. Three-quarter was retained for review rather than mislabeled as another orthographic side.
- Parameters: face limit 45,000; detailed texture; original-image texture alignment; PBR enabled by default.
- **One Tripo generation, 40 credits consumed.** First result retained because its face, compact torso, thick limbs, visor and outfit already matched the selected identity.
- Raw GLB preserved unmodified. Original task artifacts and diagnostic renders: `../blockout/hero-from-plate-task/`.

## Mandatory Blender cleanup performed

1. Imported only the new GLB; converted +X-forward to −Y-forward, centered at the feet, and applied geometry transforms.
2. Merged duplicate geometry vertices while retaining UV seams; cleaned loose geometry and normals; triangulated the final export surfaces and checked for degenerate triangles.
3. Reduced the blockout’s oversized head vertically and slightly across its width, with a neck transition. Restored overall height to **1.70 m**. Kept the stocky torso, thick forearms/calves, broad hands and chunky shoes from the new blockout.
4. Preserved the new rounded cheek/nose volume, friendly smile, brows and shaped sclera. Added two shallow curved iris/cornea shells following the sculpted eye surfaces, with brown irises, smaller pupils, highlights and controlled specular response. These are shallow eye surfaces, not a facial animation rig.
5. Inspected the hands from the side: the new blockout already contains four rounded finger indications and a thumb. Preserved them rather than replacing them with the rejected hand geometry. Front-view fingers naturally overlap in the A-pose. Individual finger bones have not been added.
6. Preserved sculpted swept hair volumes and curved visor. Separated their existing surfaces into named meshes with retained UVs. Their boundaries follow the current outfit; swapping cosmetics and hidden scalp coverage still require later testing.
7. Kept the modeled polo collar, navy/orange sleeve piping, orange placket, navy shorts, laced shoes and orange shoe accents. Preserved their UV-aligned textures rather than repainting a different body.
8. Replaced the generated metallic/roughness behavior with five standard material instances: matte cloth, warm skin, hair, visor and eye cornea. Removed the original ORM dependency; reduced normal intensity to .20.
9. Reduced and repacked textures: **2K base color, 1K normal, 256px iris**. Verified that the FBX embeds these reduced images, not the original 4K packed textures.
10. Added one provisional humanoid rest armature, normalized rest weights, and the five requested attachment sockets. No Mixamo binding, animation or Unity work.
11. Rendered front, side and three-quarter review views under one soft key/fill/rim setup. Corrected the cornea depth and studio floor artifact found in the first review.

## Proportion checklist versus plate / bible

| Target | Result |
|---|---|
| One stocky toy-athlete | Compact torso, broad arms, full calves and broad shoes retained from the plate-guided blockout |
| Height 1.6–1.9 m | **1.700 m** |
| Head around 20–25% | Crown-to-selected-chin landmark: **24.47%**; chin Z ≈ 1.284 m |
| Rounded recognizable face | Same simple smile, round cheeks, small nose, large brown eyes and arched brows |
| Blond hair and visor | Swept molded locks, white curved visor and navy brim |
| Clothing identity | White polo, navy collar/orange trim, navy shorts, white/orange shoes |
| Chunky fingered hands | Four rounded finger indications plus thumb; strongest separation visible in side view |
| Matte soft plastic | Cloth roughness .74, skin .62, hair .57, visor .43; eyes .23 as the small specular exception |
| Bind-friendly rest pose | Relaxed A-pose; feet separated; arms clear of torso; applied scale and feet origin |

`Hero_01_compare.png` shows **only plate crop | new three-quarter render**, with approximately equal standing height. No retouching or silhouette warping was applied to the render. The stocky silhouette and face are recognizably derived from the plate; final likeness acceptance belongs to Adnan, not an automated percentage score.

Remaining visible differences: the plate has more saturated daylight skin/hair, finer hair ridges and cloth detail, and a racket-holding pose. The model uses larger simplified hair volumes, a neutral A-pose and soft studio light. Shoe/pocket accent shapes contain some Tripo simplification. No racket is included in this phase.

## Geometry and mobile budget

| Mesh | Triangles |
|---|---:|
| Hero_01_Body | 34,263 |
| Hero_01_Hair | 6,905 |
| Hero_01_Visor | 2,158 |
| Hero_01_EyeSphere_L | 1,756 |
| Hero_01_EyeSphere_R | 1,756 |
| **Total** | **46,838** |

Five meshes, one 22-bone armature, five material instances using a shared standard shading model. Body/hair/visor share the same 2K color and 1K normal images. Eyes share one 256px texture. This is the close-up hero budget, **not a measured device-performance guarantee**. No LOD pass has been performed.

The cleaned source remains predominantly triangulated Tripo topology. This phase did not produce hand-authored quad deformation loops. Rest weights are a provisional handoff; shoulder, elbow, knee and finger deformation must be tested during the authorized rigging phase. Do not infer production animation readiness from a successful rest-pose export.

## Sockets

Bone-parented empties; Blender rest coordinates in meters, −Y forward / Z up:

| Socket | Parent | Position |
|---|---|---|
| Hat | Head | (0, 0, 1.665) |
| Hand_R | Hand.R | (−0.477, −0.023, 0.695) |
| Hand_L | Hand.L | (0.477, −0.023, 0.695) |
| Back | Chest | (0, 0.150, 1.060) |
| FaceExtra | Head | (0, −0.150, 1.405) |

These are provisional attachment origins, not validated racket/club grip offsets. Anatomical right is screen-left in the front view. A later Mixamo rebind must preserve or reparent the sockets.

## Files and validation

- `Hero_01.blend`: canonical new hero, with asset collection `Hero_01` and separate `REVIEW_ONLY` studio.
- `Hero_01.fbx`: selected asset only, rest pose, no leaf bones or animation; −Z forward / Y up; embedded textures.
- `Hero_01_front.png`, `Hero_01_side.png`, `Hero_01_threequarter.png`: 1100 × 1300 orthographic Blender renders.
- `Hero_01_compare.png`: plate crop beside new three-quarter view.
- `Hero_01_BaseColor.png`, `Hero_01_Normal.png`, `Hero_01_Iris.png`: exported textures, also packed in Blender.
- `clean_hero.py`: reproducible cleanup from the new raw GLB, never from the archived mannequin.
- `compose_review.py`, `verify_hero.py`, `validation.json`, `verification.json`, `provenance.json`: review and measured evidence.

Final verification **passed**: exactly one armature; identity mesh transforms; zero unweighted vertices; zero bad weight sums; maximum two influences; zero zero-area triangles. A fresh FBX import preserves five meshes, one armature, all five sockets, zero actions, and the 2K/1K/256px embedded texture sizes.

Review lighting: broad warm key 550 W / 4 m, cool fill 230 W / 3 m, soft rim 450 W / 3 m, low ambient fill; AgX Medium High Contrast, exposure +.1; Cycles 64 samples. Lights/floor/camera are excluded from FBX. Blender material roughness is supplied; URP material conversion has not been tested.

**STOP: Adnan approval required before Mixamo. No Unity import or trailer work was performed.**
