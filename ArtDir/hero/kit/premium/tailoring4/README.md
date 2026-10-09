# Garment tailoring candidate4

Status: actual review in progress. This candidate preserves the approved body and changes existing clothing only; it adds no clothing IDs or skins.

The live source is `Unity/Assets/Scripts/Tennis/HeroGarmentTailoring.cs` and `HeroGarmentHemWeights.cs`. The editor `HeroBodyCoverageRepair.Build` restores7,317 original lower-body triangles in Female Golf; tennis and male body topology are unchanged. The exact source audit and first actual coverage retake confirmed continuous thighs and removed the large tennis push-up pelvis gap.

`tools/author-panel-masks.py` reproduces the twelve Top/Shoe maps from self-contained exact construction UV inputs and original masks. Requires Python3 with NumPy, Pillow and OpenCV. Only B changes; R(AO), G(cavity), A(fabric) are bit identical. MatchHeroLook's default shoe trim supplies navy panels; saved base colors retain their existing selection API.

`tools/author-tailored-source.py` creates editable equivalents under `source/` using Blender. The two Golf source templates are preserved here; the existing canonical Tennis SleeveFit sources remain the tennis inputs. Run Blender in background with4CPUthreads. Main bodies, faces, hands, bones and input files are untouched. The bounded changes narrow collar tips, shorten high shoe collars, overlap the Golf polo by12mm at the waist, and carry32% of outer Golf skirt thigh weights on the pelvis. The concealed inner shorts and tennis skirt weights retain original deformation. Live9mm normal averaging is a shading field recorded in the C# helper.

Final acceptance requires actual five-stroke/33-slot images and near/match garment LODs. Concept-reference parity remains partial until those images are reviewed. Phone performance was deferred at the user's request.
