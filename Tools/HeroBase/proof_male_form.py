"""Current saved-model form/Unity proof; strict 2% authority gate."""
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import numpy as np,json,hashlib,shutil,re
R=Path(__file__).resolve().parents[2];D=R/'ArtDir/hero/base_lock';P=D/'proof';W=R/'work/male-form';S=R/'work/male-silhouette'
O=Path('/Users/adnanyonathan/Documents/Codex/2026-09-30/open-3/outputs/Male_Form');O.mkdir(parents=True,exist_ok=True)
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
font=ImageFont.truetype('/System/Library/Fonts/Helvetica.ttc',23);small=ImageFont.truetype('/System/Library/Fonts/Helvetica.ttc',18)
refs=json.loads((S/'reference-cameras.json').read_text())['views'][:2]
export=json.loads((P/'male-form-export.json').read_text());verification=json.loads((P/'male-form-source-verification.json').read_text())
audit=(P/'male-form-unity-audit.txt').read_text()
assert 'MALE_FORM_SAVED_UNITY_VERIFY=PASS' in audit
assert 'FBX_SHA256='+export['fbx_sha256'] in audit and 'SOURCE_FBX_SHA256='+export['fbx_sha256'] in audit
blend=D/'blender/HeroBase_Male_Silhouette.blend';fbx=D/'unity_import/HeroBase_Male_Body.fbx'
assert sha(blend)==export['source_sha256']==verification['source_sha256'] and sha(fbx)==export['fbx_sha256']
measurements=[];canvas=np.array(Image.open(D/'male_body_bald.jpg').convert('RGB'))
for v,name in zip(refs,['front','back']):
    target_file=S/(v['name']+'_target.png');render_file=P/'male_silhouette'/(v['name']+'.png');unity_file=P/'male_form/unity'/(name+'_mask.png')
    target=np.array(Image.open(target_file))>127;model=np.array(Image.open(render_file).getchannel('A'))>127
    unity=np.array(Image.open(unity_file).convert('L'))>127
    difference=int((target^model).sum());union=int((target|model).sum());error=difference/union
    delta=float((model^unity).sum()/(model|unity).sum());assert delta<.01,'Export does not preserve the Blender silhouette'
    measurements.append({'view':v['name'],'difference_pixels':difference,'union_pixels':union,'error':error,
        'unity_plate_error':float((target^unity).sum()/(target|unity).sum()),'blender_unity_error':delta,
        'target_sha256':sha(target_file),'render_sha256':sha(render_file),'unity_mask_sha256':sha(unity_file),'camera':v})
    canvas[target&model]=(.74*canvas[target&model]+.26*np.array([100,165,180])).astype(np.uint8)
    canvas[target&~model]=[45,230,170];canvas[model&~target]=[240,90,150]
gate='PASS' if all(r['error']<=.02 for r in measurements) else 'FAIL'
panel=Image.new('RGB',(1280,825),(29,30,33));draw=ImageDraw.Draw(panel)
draw.text((18,10),'GATE: MALE_FORM '+gate+' · bald front/back authority · each ≤ 2.0%',font=font,fill='white')
draw.text((18,44),'Green: reference only   Pink: model only   Tinted: overlap · locked cameras · native masks',font=small,fill='white')
draw.text((18,74),'bald_front: '+f'{measurements[0]["error"]*100:.4f}%   |   bald_back: {measurements[1]["error"]*100:.4f}%',font=small,fill='white')
panel.paste(Image.fromarray(canvas),(0,105));panel.save(P/'01_silhouette_overlay_m.png')
for backend,dst in [('blender','01c_form_m.png'),('unity','03_unity_m.png')]:
    panel=Image.new('RGB',(1680,770),(38,38,42));draw=ImageDraw.Draw(panel)
    draw.text((18,10),'Male form continuation · '+backend+' · current saved mesh · GATE: MALE_FORM '+gate,font=font,fill='white')
    for i,(name,cx) in enumerate([('front',353),('back',915.75),('threequarter',640)]):
        path=P/'male_form'/('unity' if backend=='unity' else '')/(name+'.png')
        im=Image.open(path).convert('RGBA');crop=im.crop((round(cx)-280,0,round(cx)+280,720))
        panel.paste(crop,(i*560,50),crop);draw.text((i*560+18,40),name,font=small,fill='white')
    panel.save(P/dst)
limit=json.loads((P/'male-form-reference-limit.json').read_text());edit=json.loads((W/'coordinate-edit.json').read_text())
report={'gate':'MALE_FORM '+gate,'threshold':.02,'authority':'male_body_bald.jpg front/back only','measurements':measurements,
        'shoulders':'Broad deltoid shelf locally faired; rounded neck-to-shoulder-to-upper-arm transition, no peaked horns in the front/back proof.',
        'waist':'Flank ridge softened and waist depth tapered; no spare-tire shelf in the new front/back/three-quarter proof.',
        'source_verification':verification,'coordinate_edits':edit,'reference_conflict':limit,
        'unity_fbx_sha256':export['fbx_sha256'],'unity_prefab_sha256':re.search(r'PREFAB_SHA256=(\w+)',audit).group(1)}
(P/'male-form-metrics.json').write_text(json.dumps(report,indent=2))
lines=['GATE: MALE_FORM '+gate,'','The required ≤2.0% per-view silhouette gate is unmet. The older 5% limit has not been substituted.',
 '', '| Authority view | Difference pixels | Union pixels | Blender error | Unity error | Blender–Unity difference | 2% gate |',
 '|---|---:|---:|---:|---:|---:|---|']
for m in measurements:
    lines.append(f'| {m["view"]} | {m["difference_pixels"]:,} | {m["union_pixels"]:,} | {100*m["error"]:.4f}% | {100*m["unity_plate_error"]:.4f}% | {100*m["blender_unity_error"]:.4f}% | '+('PASS' if m['error']<=.02 else 'FAIL')+' |')
lines += ['', 'Shoulders: the broad deltoid shelves were reduced with local coordinate fairing; the neck root, shoulder and upper arm now form a rounded transition. Front/back outlines have no peaked horns. See 01c_form_m.png and the actual URP captures in 03_unity_m.png.',
          '', 'Waist: the flank intersection ridge was softened, waist depth reduced and the lateral contour refined toward the locked plate. The torso tapers gradually above the hips; the previous spare-tire bulge is reduced. The hips and stocky legs retain the reference silhouette.',
          '', f'Source: continued the existing `HeroBase_Male_Silhouette.blend`, changing vertex coordinates only. Height {verification["height_metres"]:.6f} m, METRIC scale 1, A-pose; {verification["vertices"]:,} vertices and {verification["triangles"]:,} triangles. Connectivity, body origin/transforms and all six packed reference cameras are unchanged. The complete head vertices above 1.43 m match the prerequisite mesh exactly. No remesh, restart, female work, hair, rig, face textures or face polish.',
          '', f'Independent FBX re-import maximum coordinate difference: {verification["independent_fbx_reimport_maximum_coordinate_error_metres"]*1000:.7f} mm. FBX scale 1; Unity importer global/file scale 1, mesh compression off, saved normals imported. The actual saved `HeroBase_Male` prefab has one plain grey URP Lit body renderer. The Unity proof replaces the older candidate shot with this current blockout; the head remains unpolished.',
          '', 'Feasibility evidence: source masks are inconsistent under the locked orthographic framing. A front/back view of one fixed closed mesh has the same X/Z silhouette after reflection. Exact intersection/union of the reference pixel cells in world space gives '+f'{100*limit["continuous_world_space_reference_distance"]:.4f}% Jaccard distance. By the Jaccard triangle inequality, at least one view of any shared continuous silhouette is ≥{100*limit["continuous_world_space_minimax_lower_bound"]:.4f}% away. The requested 2% per-view continuous match therefore cannot be attained with these locked outlines.',
          '', 'The delivered gate still uses the unchanged native raster masks and alpha >127. Native pixel sampling introduces subpixel boundary quantization; the source-only continuous calculation documents the underlying outline conflict rather than replacing the native metric. Pink/green disagreement in MALE_FORM_REFERENCE_CONFLICT.png identifies the conflicting arms, lower legs and foot shapes. No target, scale or camera was changed to pass.',
          '', 'The source discrepancy does not excuse remaining model error: these are current measurements, not a claim that the candidate is an exact minimax optimum. The strict gate remains FAIL. A consistent orthographic front/back authority pair is needed to support a clean shared silhouette at 2%.',
          '', 'Deliverables:',str(blend),str(fbx),str(R/'Unity/Assets/Characters/HeroBase/HeroBase_Male.prefab'),str(P/'01_silhouette_overlay_m.png'),str(P/'01c_form_m.png'),str(P/'03_unity_m.png'),
          '', f'Saved blend SHA-256: `{export["source_sha256"]}`',f'Source and Unity FBX SHA-256: `{export["fbx_sha256"]}`',f'Unity prefab SHA-256: `{report["unity_prefab_sha256"]}`']
(P/'MALE_FORM_RESULTS.md').write_text('\n'.join(lines)+'\n')
files=[blend,fbx,P/'01_silhouette_overlay_m.png',P/'01c_form_m.png',P/'03_unity_m.png',P/'MALE_FORM_RESULTS.md',P/'MALE_FORM_REFERENCE_CONFLICT.png',
       P/'male-form-metrics.json',P/'male-form-source-verification.json',P/'male-form-unity-audit.txt',P/'male-form-reference-limit.json',P/'male-form-export.json']
for file in files:shutil.copy2(file,O/file.name)
for backend in ['blender','unity']:
    out=O/backend;out.mkdir(exist_ok=True)
    for name in ['front','back','threequarter']:
        path=P/'male_form'/('unity' if backend=='unity' else '')/(name+'.png');shutil.copy2(path,out/path.name)
        if backend=='unity':shutil.copy2(path.with_name(name+'_mask.png'),out/(name+'_mask.png'))
manifest={file.name:sha(file) for file in files};(O/'manifest.json').write_text(json.dumps(manifest,indent=2))
print(json.dumps({'gate':report['gate'],'bald_percent':{m['view']:100*m['error'] for m in measurements},'output':str(O)},indent=2))
