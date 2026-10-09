# Skin liveliness (chat 9, 2026-10-02)

Brief: `ArtDir/hero/base_lock/parallel_prompts/09_MALE_NAPE_PELVIS_SKIN.txt` (skin part) + `SKIN_LIVELINESS_BRIEF.md`. Work folder `work/male-nape-pelvis-skin/skin/`.

```
GATE: SKIN_LIVELINESS PASS   (limits disclosed below: Lit only, no subsurface/rim; Fair stays pale; shadow chroma lower than the plate)
```

## What was actually wrong (differs from the brief's diagnosis)

The sheet's NOW skin pixels (S ≈ 0.37) come from the **HeroBase default male skin**, a one-off flat colour (0.74, 0.555, 0.435) measured off the plate, not from a palette tone. I reproduced the sheet with the real Unity studio (`HeroBaseSkinProof`: runtime materials, same 4 suns + trilight ambient as `HeroBaseMaleFormProof`): that colour renders at **S 0.383 / V 0.77**, the palette's own **Tan (226,160,110) already renders at S 0.480 / V 0.91** on the same material stack, the plate's skinish pixels measure **S 0.458–0.484 / V 0.70–0.73** (body sheet / head sheet, same detector: hue 8–45°, S > 0.2, V > 0.3). So the main fix is "the HeroBase default is the palette's Tan", not "raise Tan's chroma"; raising Tan by the brief's 15–25 % would overshoot SKIN_CHROMA_TAN (≈ 0.55–0.59 rendered). The palette retune was applied only where a tone rendered pale.

## Changes

| file | change |
|---|---|
| `Unity/Assets/Scripts/Game/GolferStyle.cs` | `SkinTones` retuned (table below) + comment |
| `Unity/Assets/Scripts/Tennis/HeroKit.cs` (`SkinHex`), `GolfArcade/Unity/SportProgress.swift` (`Outfit.skins`) | the same six colours mirrored (they were hand-copied hex lists "matching the six the game offers"); the Swift line is not compiled/tested |
| `Unity/Assets/Characters/HeroBase/Materials/Male_Form_Skin.mat`, `Skin_M.mat`, `Male_Face_Skin.mat` | BaseColor = Tan (226,160,110) = `SkinTones[2]`, Metallic 0, Smoothness 0.40, URP Lit, opaque, no texture (Skin_M keeps the neutral atlas) |
| `Unity/Assets/Editor/HeroBaseMaleFormProof.cs`, `HeroBaseStaticImport.cs` | male skin colour read from `GolferStyle.SkinTones[2]` instead of a literal; `ExpectedTriangles` 66158 |
| `Tools/HeroBase/build_albedo.py` | male atlas = **neutral white + relative cheek/ear flush** (G −3.5 %, B −4.5 % at full flush, applied after the tint multiplies in), no baked RGB, no grey collar, no stubble/pores; female atlas path unchanged (regenerated bit-identical, `7104891a…`); default run builds Male only. New male atlas `f4d99f5a47b98372…` (was `d64ab859…`) in `Unity/Assets/Characters/HeroBase/Textures/` and `ArtDir/hero/base_lock/unity_import/Textures/` |
| `Tools/HeroBase/plate_profiles.py` | `MALE['skin']` = Tan so authoring/preview materials do not fight the palette |
| `Unity/Assets/Editor/HeroBaseSkinProof.cs` (new) + `skin_unity.py`, `skin/cases_final.py` | proof harness: renders runtime copies of the real material asset with the code palette, writes PNGs, measures skinish S/V/H; changes no asset |

Palette (sRGB, old → new; rendered = HeroBase studio, URP Lit, smoothness 0.40, same detector as the plate):

| tone | old | new | rendered S before → after | V |
|---|---|---|---|---|
| Fair | 255,224,196 | **243,201,166** | 0.224 → 0.278 | 0.90 → 0.94 (value 1.00 → 0.95 so highlights stop clipping to chalk) |
| Light | 238,196,160 | **238,187,143** | 0.292 → 0.361 | 0.94 |
| Tan | 226,160,110 | 226,160,110 (held) | 0.480 → 0.480 | 0.91 |
| Olive | 196,124,80 | **196,122,76** | 0.547 → 0.564 | 0.80 |
| Brown | 150,90,56 | **150,88,53** | 0.549 → 0.565 | 0.63 |
| Deep | 96,60,40 | **96,57,36** | 0.448 → 0.474 | 0.43 |
| *HeroBase default male skin* | flat (0.74,0.555,0.435) | = Tan | **0.383 → 0.480** | 0.77 → 0.91 |

Plate reference (same detector): S 0.458 (body sheet) / 0.484 (head sheet), V ≈ 0.70–0.73, hue ≈ 23°.

## Gate lines

| line | result | evidence |
|---|---|---|
| SKIN_CHROMA_TAN | PASS | Tan renders S 0.480 vs plate 0.458 (body sheet) / 0.484 (head sheet): within 0.03 on both; default HeroBase skin 0.383 → 0.480 |
| SKIN_ALL_TONES | PASS | `proof/SKIN_liveliness_Fair_Tan_Deep.png`, `proof/SKIN_six_tones_before_after.png`: all six from one mesh + one material asset; Fair creamy peach (was near-white yellow), Deep warm dark brown, Olive/Brown not orange-plastic (S ≤ 0.57) |
| SKIN_NO_BAKED_PEACH | PASS | the `after_*` renders are the same Body_M and the same `Male_Form_Skin.mat` asset with only `_BaseColor` swapped to `SkinTones[i]`; the male atlas is white (no pigment), so `_BaseColor` carries the tone and the flush is relative |
| SKIN_SOFT_PLASTIC | PASS | URP Lit, Metallic 0, Smoothness 0.40, no subsurface/SSS, no normal map |
| no stubble / pores | PASS | atlas has none; face stickers unchanged |

## Honest notes / limits

1. **Lit only.** I did not add the optional warm subsurface/rim shader. The plate's saturation rises toward the shadows (S by brightness bin V 0.30–0.45 / 0.45–0.55 / 0.55–0.65 / 0.65–0.75 / 0.75–0.85 / 0.85–1.0: plate 0.549 / 0.525 / 0.507 / 0.499 / 0.478 / 0.455), ours is flatter (Tan after: 0.447 / 0.473 / 0.492 / 0.505 / 0.498 / 0.489): about 0.05–0.10 less chroma in the darkest areas, 0.03 more in the highlights. The existing `GolfArcade/TennisCharacter` shader (wrap + `_Subsurface` + rim) did not fix it in a quick test (Tan: 0.454 / 0.463 / 0.462 / 0.478 / 0.505 / 0.507) and has `_Saturation` 0.82 by default, so I left it out; a bespoke skin shader that scales the scatter with BaseColor is the follow-up if you want warmer shadows.
2. **Fair is pale by nature** (S 0.28, V 0.94); it is no longer yellow-white but it is not "peachy" like the plate's tan. A pinker Fair (hue 24° instead of 27°) is possible if you want it.
3. The current male mesh has **no UV set**, so the active prefab (flat tone material) never samples the atlas; the relative flush only applies to UV-mapped heads/legacy `Skin_M`.
4. In the game the tennis characters take `SkinTones` through `KitRecolor` + `TennisCharacter` (saturation × 0.82); Tan is unchanged there, Fair/Light/Olive/Brown/Deep are 3–37 % more saturated in the palette. Not re-tested in-game. `LockerColors.swift` `skinStops` (10-step slider) is untouched.
5. Real Unity render path: `HeroBaseMaleFormProof.Run` refreshed, verify PASS; `proof/np_unity_before_after_crops.png` shows the new tan on the real prefab.

## Reproduce

`python3 work/male-nape-pelvis-skin/skin_unity.py work/male-nape-pelvis-skin/skin/cases_final.py` (runs Unity batch, ~25 s, writes `skin/unity/*.png`, `skin/measure_cases_final.json`), then `python3 skin/compose_skin_sheet.py`. Atlas: `python3 Tools/HeroBase/build_albedo.py` (Male) / `... Female`. Before-state files: `skin/Head_Male_Albedo_before.png`, `skin/Head_Female_Albedo_before.png`.
