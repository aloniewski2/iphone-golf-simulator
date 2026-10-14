from pathlib import Path
import json, shutil, re
root=Path.cwd(); w=root/'work/postcard-refresh'
out=Path('/Users/adnanyonathan/Documents/Codex/2026-10-08/open-5/outputs')
for sub in ['proof','physics','grass-proof','evidence']:(out/sub).mkdir(parents=True,exist_ok=True)
a=json.loads((w/'release-gates/render-gates.json').read_text())
b=json.loads((w/'windmill-final/render-gates.json').read_text())
rows=sorted([r for r in a['holes'] if r['hole']!=20]+b['holes'],key=lambda r:(r['hole'],['tee','approach','putting'].index(r['pose'])))
assert len(rows)==48 and all(r['gate']=='PASS' and r['drawCalls']<=250 for r in rows)
combined={**a,'gate':'PASS','provenance':{'catalog':'evidence/catalog-render-gates.json','hole20Replacement':'evidence/windmill-render-gates.json','reason':'Catalog passed 47/48 views; Windmill tee was 256. Hole-20-only canopy batching preserved meshes, materials and positions, then all three hole-20 poses passed. These three replace the catalog hole-20 results.'},'holes':rows}
(out/'render-gates.json').write_text(json.dumps(combined,indent=2)+'\n')
for r in rows:
 src=w/('windmill-final' if r['hole']==20 else 'release-gates')
 name=f"hole{r['hole']:02d}-{r['pose']}.png"
 shutil.copy2(src/name,out/'proof'/name)
for h in sorted({r['hole'] for r in rows}):
 src=w/('windmill-final' if h==20 else 'release-gates')/f'hole{h:02d}-physics.json'
 assert json.loads(src.read_text())['gate']=='PASS'
 shutil.copy2(src,out/'physics'/src.name)
shutil.copy2(w/'tennis-release/resort_1_gameplay.png',out/'proof/tennis-gameplay.png')
for p in (w/'hole10-grass').iterdir():
 if p.is_file():shutil.copy2(p,out/'grass-proof'/p.name)
for src,name in [
 (w/'release-gates/render-gates.json','catalog-render-gates.json'),
 (w/'windmill-final/render-gates.json','windmill-render-gates.json'),
 (w/'compile6.log','postcard-tests.log'),
 (w/'camera-final-check.log','tennis-camera-tests.log'),
 (w/'release-queue.json.results.json','catalog-job-results.json'),
 (w/'windmill-queue.json.results.json','final-job-results.json'),
 (w/'changed-files.json','changed-files.json'),
 (w/'split-source/reference-hole09-manifest.json','split-export-manifest.json'),
 (w/'tennis-release/skin-light-audit.txt','tennis-skin-light-audit.txt')]:
 shutil.copy2(src,out/'evidence'/name)
for name in ['postcard-refresh-release-gates','postcard-refresh-windmill-final','postcard-refresh-tennis-release']:
 p=root/'work/full-visual-overhaul'/f'{name}.log'
 s=p.read_text(errors='replace')
 assert not re.search(r'Shader error|error CS\d+|Scripts have compiler errors',s)
 shutil.copy2(p,out/'evidence'/p.name)
def link(label,rel):return f'[{label}]({out/rel})'
table=[]
for h in sorted({r['hole'] for r in rows}):
 vals={r['pose']:r['drawCalls'] for r in rows if r['hole']==h}
 table.append(f"| {h} | {vals['tee']} | {vals['approach']} | {vals['putting']} | PASS |")
report=f'''# Golf and tennis visual refresh — gate results

Implemented the nine requested visual changes in the existing Unity project on `codex/full-visual-overhaul`. Final captured golf results: **48/48 PASS**, across 16 authored holes and three gameplay poses per hole. Recorded draw calls range from **113 to 249**, including the gameplay HUD and minimap.

These are Unity 6000.3.24f1 Editor measurements on Metal / Apple M5. They establish the budget for the captured views; iPhone frame rate, thermal behavior and every possible camera position have not been profiled.

## Visual review

- {link('Split: tee, cliffs and surf','proof/hole09-tee.png')} · {link('approach','proof/hole09-approach.png')}
- {link('Crater: tee, basalt and lava','proof/hole10-tee.png')} · {link('approach','proof/hole10-approach.png')}
- {link('Tennis: production gameplay camera','proof/tennis-gameplay.png')}
- {link('Cold course grass','proof/hole17-tee.png')} · {link('Volcanic grass','proof/hole22-tee.png')} · {link('Windmill final budget check','proof/hole20-tee.png')}

The `proof` directory contains all 48 golf captures plus the tennis capture. Golf captures are 900 × 1600 with 4× MSAA; tennis is 1920 × 1080.

## Requested changes

| Request | Result |
| --- | --- |
| Post-processing | Connected URP post-process resources and the actual golf camera; HDR, ACES, bloom, grading and vignette now run. Each camera owns one private profile. Low quality retains tone mapping while disabling bloom. Lighting and emissive intensity were retuned for the active pipeline. |
| Postcard turf | Brighter lime palette with bold two-tone fairway and tee stripes, coordinated blade colors and distinct rough. |
| Fog and haze | Fog starts at 150 yd, with longer blue atmospheric falloff suited to coastal and volcanic worlds. |
| Hero lighting | Reduced camera fill, removed legacy color compensation, added world-light rim and ground bounce, plus terrain-aware contact and shoe occlusion. Crater skin no longer inherits the strong blue fill. |
| Camera framing | Higher tennis camera with a longer lens; closer golf address framing. Coastal tee framing keeps the hero clear of the HUD while revealing more shoreline. |
| Grass | Enabled holes 16, 17, 18, 21, 22 and 23. Hole 10 passed its moving-turf test and is enabled. Exposed rock, snow, ash and paths remain excluded from grass placement. |
| Crater | Combined hexagonal basalt columns, glowing fissures, cooled lava plates and emissive lava; existing smoke remains integrated. The black tower is replaced visually by a continuous basalt formation. |
| Split | Authored a new reference export and coordinated shoreline mapping so near cliffs and surf read from gameplay, using the coastal composition approach established for hole 12. |
| Foliage and budget | Denser instanced clusters, cheap distant plant representations and culling. Coastal trees use shared near/far meshes. Minimap renders are cached until their framing or course changes. |

## Render and physics gates

Every accepted view reports HDR and post-processing enabled, renderer resources present, ACES and bloom active, one private volume profile, fog start at 150 yd, and exact before/after visual-dresser physics. All tee views have active grass cells.

Draw counts are the maximum recorded across settled, completed frames for each pose, with the actual gameplay camera, HUD and minimap. Shadow rendering uses a 60-unit range, one cascade and a 4096 atlas in High quality; contact occlusion supports the close hero view.

| Hole | Tee draws | Approach draws | Putting draws | Gate |
| --- | ---: | ---: | ---: | --- |
{chr(10).join(table)}

{link('Combined machine-readable results','render-gates.json')} retain the provenance of two runs. The initial catalog passed 47/48 views; Windmill's tee measured 256 draws. A hole-20-only canopy batching adjustment reduced its tee to 249 without changing plant meshes, positions, materials or shadows. All three Windmill poses were then recaptured and replace its earlier rows. The original catalog and final Windmill JSON files are preserved under `evidence`.

**Physics scope:** the exactness checks compare collision state before and after runtime visual dressing. Split's near shoreline was intentionally reshaped as part of its authored art pass; this is a local course-geometry change. Tee/pin anchors and the distant layout were retained, and the analytic shoreline was updated to match the new export. Split passed 2,664 interior land/water samples with zero mismatches; samples exclude the 2-yard tessellated edge margin. The original Split source asset remains available.

## Hole 10 grass acceptance

{link('Moving-turf proof','grass-proof/moving-turf-proof.json')}: PASS. Seven address poses passed; a 397-frame flight across 42 coarse stations produced no flight-time turf rebuilds. Ownership, pool/material/matrix checks and static physics checks passed. Final catalog captures independently confirm active hole-10 grass after the lighting and batching refinements. The grass-proof screenshots predate those final lighting refinements and are retained as grass-behavior evidence.

## Compilation and reproducibility

- Postcard-focused checks: **5 passed, 0 failed**; runtime and test assemblies compiled.
- Tennis gameplay/camera checks: **11 passed, 0 failed**; runtime and test assemblies compiled.
- Final real-Unity catalog, Windmill and tennis captures contain no C# compiler or shader errors. Existing compiler warnings are preserved in the test logs.
- All 53 touched Unity source/asset files byte-match the isolated project used for final verification. The {link('54-file SHA-256 manifest','evidence/changed-files.json')} also includes the Split Blender authoring script.

Project: `{root}`. The editable Split export is generated by `blender/scripts/build_reference_hole09.py`; its native Blender scene and export are retained in `work/postcard-refresh/split-source`. Render verification is implemented in `Unity/Assets/Scripts/Course/PostcardRefreshProofRunner.cs`, with entry point `GolfArcade.EditorTools.PostcardRefreshProof.Run`. The capture queue and isolated verification project are retained under `work/postcard-refresh`.

No device build, deployment or commit was made. Existing unrelated workspace changes were preserved.
'''
(out/'GATE_RESULTS.md').write_text(report)
(w/'GATE_RESULTS.md').write_text(report)
assert len(list((out/'proof').glob('hole*.png')))==48
for target in re.findall(r'\]\(([^)]+)\)',report):assert Path(target).is_file(),target
print(f'Packaged {len(rows)} passing golf views, tennis, 16 physics audits and hole-10 grass evidence in {out}')
print(report)
