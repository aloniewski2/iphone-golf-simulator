# Hero Base acceptance contract

Authority: PLAN_HeroBase_MaleFemale_Lock.md, male_body_plain.jpg, female_body_plain.jpg.

**Scope:** character look only (meshes + Unity materials/prefabs). Mixamo / existing animator binding is NOT required for PASS.

Every line needs evidence; incomplete = FAIL.

- Male and female front/back silhouette vs plate ≤ ~5%.
- Faces: eyes/brows/mouth + head proportions match plates.
- Separate Body_M, Hair_M, Body_F, Hair_F; opaque solid hair; no cards/transparency/particles.
- Hair is an independent FBX and prefab. Body-only assets have no hairstyle dependency and retain a complete skin-colored scalp, ears, face and neck when the hairstyle is removed. Verify bald front/back/3q Unity proofs and actual HairRoot deletion without changing the body mesh.
- Blender meters; foot origin; height 1.65–1.75 m.
- 35–50k tris/base; head tex 1K–2K, body ≤1K.
- FBX scale 1; URP Lit; hair Opaque; face crunch off.
- Unity front/back/3q match plate framing; not softer/flatter/yellower than Blender match.
- Prefabs under Assets/Characters/HeroBase; proof overlays + GATE_RESULTS.md.
- No clothes, bandana, fashion, tennis kit.

No quality PASS from tech-only PASS.
