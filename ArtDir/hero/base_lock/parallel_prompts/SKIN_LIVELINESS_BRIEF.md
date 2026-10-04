# Skin liveliness brief (all tones)

## What the plate is doing that we are not

Measured on `SHEET_body_ref_vs_now_v2.png` (skinish pixels):

| | Saturation (HSV S) | Value | Notes |
|---|---|---|---|
| REF (plate row) | **~0.46** | ~0.71 | Warm peach, slight cheek/ear life |
| NOW (our row) | **~0.37** | ~0.67 | Greyer, cooler, flatter |

Root causes (stacked):

1. **Flat pigment** — `Head_Male_Albedo.png` is a constant fill of `plate_profiles.MALE['skin']` `(0.77, 0.565, 0.425)` with **std = 0** and **no blush** (female atlas gets a tiny cheek blush; male was stripped for “clean / no stubble”).
2. **Muted material color** — `Male_Form_Skin.mat` BaseColor `(0.74, 0.555, 0.435)` → HSV S≈0.41, already below plate; render lights push NOW to ~0.37.
3. **URP Lit only** — soft-plastic plate reads warmer in shadow (fake SSS / warm bounce). We have no subsurface / warm rim on HeroBase skin (older `Hero_01_WarmSkin` had a `_Subsurface` tint; current HeroBase mats do not).
4. **Customization path** — `GolferStyle.SkinTones` (Fair→Deep) is the real player palette. Baking one lively peach into albedo **breaks** darker/fairer tones. Liveliness must be a **response** (chroma + soft-plastic shading), not a single RGB.

## Fix that works for every skin color

Do these in order; keep albedo **tintable**:

1. **Retune `SkinTones[]` chroma** (Hue ladder stays Fair→Deep). Raise S ~15–25% on light/mid; smaller bump on Deep so it does not go plastic-orange. Author mid **Tan** to land near plate S≈0.46 under studio light.
2. **Soft-plastic material preset** shared by all tones: Metallic 0, Smoothness ~0.38–0.45, white/neutral multiply so BaseColor = SkinTone. Optional very low warm emission or custom soft-plastic shader with **warm subsurface tint scaled by BaseColor** (multiply, not add fixed peach).
3. **Relative blush map** (optional): cheek/ear/knuckle flush stored as a **small sat/value offset in UV space**, applied after tint so Fair and Deep both get proportional warmth — never a baked peach layer.
4. **Rebuild male head albedo** as near-flat **neutral soft-plastic base** (or keep mid tan only as default authoring tone) + optional relative blush; stop treating one RGB as “the” skin.
5. **Proof gate**: same lighting, Fair / Tan / Deep side-by-side vs plate energy — mid Tan sat within ~0.03 of plate; Fair/Deep look alive, not grey or cartoon-orange.

## Non-goals

- Photoreal SSS / pores
- Stubble bake
- Per-tone unique albedo files (unless generated from one relative stack)

## Files to touch

- `Unity/Assets/Scripts/Game/GolferStyle.cs` — `SkinTones`
- `Unity/Assets/Characters/HeroBase/Materials/Male_Form_Skin.mat`, `Skin_M.mat`, `Male_Face_Skin.mat` (+ female equivalents)
- `Tools/HeroBase/build_albedo.py` + `plate_profiles.py` skin defaults
- Optional: soft-plastic skin shader or reuse WarmSkin subsurface idea under HeroBase
