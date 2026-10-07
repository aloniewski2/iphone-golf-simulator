# Golf clothing fit and detail — 6 October 2026

Updated the male and female golf outfits in Unity and the native locker assets. The polo tucks inside the waistband, the belt follows the waist, and the trouser hem has a clean break above the shoe. Polo knit, directional twill, belt leather and shoe panel stitching have stronger material definition.

## Gates

- GATE: Character identity PASS. Original body, face, optional hair and 53-bone rest rig match the source fingerprints. The live golfer restores original sleeve and ankle skin, while a derived golf mesh masks only triangles covered by the polo and waistband. The full original body asset is preserved.
- GATE: Clothing separation PASS. Reviewed rendered address views from four angles and the actual drive backswing, impact and finish (rendered from baked skinned snapshots) from two angles, plus waist and shoe crops. Belt and shirt have separate boundaries; garment sections retain their own materials.
- GATE: Surface mapping PASS. Zero ambiguous UV texels across the eight edited top, bottom and shoe surfaces. Knit, twill and leather use separate fabric identities.
- GATE: Runtime rendering PASS. Isolated Unity 6000.3.24f1 runtime/editor compilation and Metal shader renders completed. Both native locker characters rendered with the regenerated assets and golf-specific surface parameters.
- GATE: Asset consistency PASS. Both native manifests and compressed meshes resolve their maps. Seventeen texture maps retain their existing resolutions and ASTC formats. Kit triangle counts: male 83,649, female 57,499.
- GATE: Full repository test suite FAIL (pre-existing compile blockers). Legacy tests refer to removed GolferView.UsesStandardCharacter, SetFloatingHandsPreview and GolferStyle.Size/Hair/Outfit APIs. These test files were excluded only from the isolated render checkout. Production tests were not changed. No phone build or installation was performed.

## Evidence

[Outfit and detail review](Golf_tailoring_review.png), [male pose sheet](Male_poses.png), [female pose sheet](Female_poses.png). Full-resolution captures are in verified/ and native_verified/. JSON checks and renderer logs are alongside this report. The pose harness is saved as TailoringProof.cs.txt.

Editable source garments are in ../source/. Production assets and four targeted C# changes were applied without committing. promoted.json records exact file hashes; previous versions are backed up under work/golf-tailoring/rollback/production/.


## Swing preview

GATE: Swing preview PASS. Both updated outfits are shown through the full Drive clip at 30 fps, with runtime swing clearance and garment correctives. The fixed cameras fit the full body and club trajectory; all 210 source frames have zero edge contacts. The 4.5-second preview includes short start/end holds. The MP4 was decoded and visually checked after encoding.

[Play swing video](Golf_swing_preview.mp4) · [Looping preview](Golf_swing_preview.gif). Harness, render log and swing_checks.json are saved alongside this report.
