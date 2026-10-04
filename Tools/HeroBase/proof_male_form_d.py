"""Evidence for the saved male continuation; native locked-mask 2% gate."""
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import numpy as np,json,hashlib,shutil,re
R=Path(__file__).resolve().parents[2];D=R/'ArtDir/hero/base_lock';P=D/'proof';S=R/'work/male-silhouette';W=R/'work/male-form-d';F=P/'male_form_d'
O=Path('/Users/adnanyonathan/Documents/Codex/2026-09-30/open-3/outputs/Male_Form_D');O.mkdir(parents=True,exist_ok=True)
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
load=lambda p:json.loads(p.read_text())
export=load(P/'male-form-export.json');verify=load(P/'male-form-d-source-verification.json');audit=(P/'male-form-unity-audit.txt').read_text()
blend=D/'blender/HeroBase_Male_Silhouette.blend';fbx=D/'unity_import/HeroBase_Male_Body.fbx'
assert sha(blend)==export['source_sha256']==verify['source_sha256']
assert sha(fbx)==export['fbx_sha256']==verify['fbx_sha256']
assert 'MALE_FORM_SAVED_UNITY_VERIFY=PASS' in audit
assert 'FBX_SHA256='+sha(fbx) in audit and 'SOURCE_FBX_SHA256='+sha(fbx) in audit
for name,digest in re.findall(r'CAPTURE=(\w+) SHA256=(\w+)',audit):assert sha(F/'unity'/(name+'.png'))==digest
eyes=load(P/'male-form-d-eye-checks.json');assert eyes['source_sha256']==sha(blend)
assert all(sha(F/'unity'/(n+'.png'))==h for n,h in eyes['unity_capture_sha256'].items())
views=load(S/'reference-cameras.json')['views'][:2]
expected={'bald_front':'520a0b3722ea06aa3f57de4a29dc5fed6ebb141de072a683869937e632649594','bald_back':'bd9ebba965727696c8180062ef647b5ef1926b2ae347106cd62f13d48a735a64'}
assert sha(D/'male_body_bald.jpg')=='76c005d722ecfefab530b266606a147486693340a75bc22654c07b950c6bc4a0'
measurements=[];canvas=np.array(Image.open(D/'male_body_bald.jpg').convert('RGB'))
for v,name in zip(views,['front','back']):
    tfile=S/(v['name']+'_target.png');bfile=P/'male_silhouette'/(v['name']+'.png');ufile=F/'unity'/(name+'_mask.png')
    assert sha(tfile)==expected[v['name']]
    t=np.array(Image.open(tfile).convert('L'))>127;b=np.array(Image.open(bfile).getchannel('A'))>127;u=np.array(Image.open(ufile).convert('L'))>127
    diff=int((t^b).sum());union=int((t|b).sum());delta=float((b^u).sum()/(b|u).sum());assert delta<.01
    measurements.append({'view':v['name'],'difference_pixels':diff,'union_pixels':union,'error':diff/union,
      'unity_plate_error':float((t^u).sum()/(t|u).sum()),'blender_unity_error':delta,'target_sha256':sha(tfile),'render_sha256':sha(bfile),'unity_mask_sha256':sha(ufile),'camera':v})
    canvas[t&b]=(.74*canvas[t&b]+.26*np.array([100,165,180])).astype(np.uint8)
    canvas[t&~b]=[45,230,170];canvas[b&~t]=[240,90,150]
sil='PASS' if all(m['error']<=.02 for m in measurements) else 'FAIL'
gate='PASS' if sil=='PASS' and all(v['flag']=='PASS' for v in eyes['checks'].values()) else 'FAIL'
font=lambda n:ImageFont.truetype('/System/Library/Fonts/Helvetica.ttc',n)
panel=Image.new('RGB',(1280,825),(29,30,33));draw=ImageDraw.Draw(panel)
draw.text((18,10),'GATE: MALE_SIL '+sil+' · bald front/back · each ≤ 2.0%',font=font(23),fill='white')
draw.text((18,44),'Green: reference only   Pink: model only   Tinted: overlap · frozen native masks',font=font(18),fill='white')
draw.text((18,74),f'bald_front: {100*measurements[0]["error"]:.4f}%   |   bald_back: {100*measurements[1]["error"]:.4f}%',font=font(18),fill='white')
panel.paste(Image.fromarray(canvas),(0,105));panel.save(P/'01_silhouette_overlay_m.png')
def paste_fit(panel,im,box):
    im=im.convert('RGBA');x,y,w,h=box;scale=min(w/im.width,h/im.height);im=im.resize((round(im.width*scale),round(im.height*scale)),Image.Resampling.LANCZOS)
    panel.paste(im,(x+(w-im.width)//2,y+(h-im.height)//2),im)
for backend,dest in [('blender','01d_form_m.png'),('unity','03_unity_m.png')]:
    panel=Image.new('RGB',(2400,1270),(38,38,42));draw=ImageDraw.Draw(panel)
    draw.text((20,14),f'Male form D · {backend} · GATE: MALE_FORM {gate} · saved continuation, no remesh',font=font(28),fill='white')
    draw.text((20,53),f'Bald front {100*measurements[0]["error"]:.4f}% / back {100*measurements[1]["error"]:.4f}% · required each ≤ 2.0% · height {verify["height_metres"]:.4f} m',font=font(22),fill='white')
    for i,(name,cx) in enumerate([('front',353),('back',915.75),('left',640),('right',640),('threequarter',640)]):
        path=F/('unity' if backend=='unity' else '')/(name+'.png');im=Image.open(path).convert('RGBA')
        crop=im.crop((round(cx)-280,0,round(cx)+280,720))
        paste_fit(panel,crop,(i*480,114,480,690));draw.text((i*480+20,86),name.replace('threequarter','three-quarter'),font=font(22),fill='white')
    # Native closeups are cropped/resized for display only, preserving aspect.
    for i,name in enumerate(['head_side','foot_side']):
        path=F/('unity' if backend=='unity' else '')/(name+'.png');im=Image.open(path)
        if backend=='unity':im=im.crop((280,0,1000,720))
        paste_fit(panel,im,(i*600,845,580,360));draw.text((i*600+20,817),'chin / neck' if i==0 else 'heel / sole / ankle / toe',font=font(22),fill='white')
    ref=Image.open(D/'male_head_detail.jpg').crop((440,66,834,352));paste_fit(panel,ref,(1200,845,580,360));draw.text((1220,817),'head-detail authority',font=font(22),fill='white')
    ref=Image.open(D/'male_multiangle_body.jpg').crop((646,43,1197,346));paste_fit(panel,ref,(1800,845,580,360));draw.text((1820,817),'side authority (eye check)',font=font(22),fill='white')
    draw.text((20,1226),'Geometry defect checks only; upper face and hands remain an unpolished blockout. Native gate images are unchanged in scale.',font=font(21),fill=(210,210,210))
    panel.save(P/dest)
limit=load(P/'male-form-reference-limit.json');edit=load(W/'coordinate-edit.json')
report={'gate':'MALE_FORM '+gate,'silhouette_gate':'MALE_SIL '+sil,'threshold':.02,'authority':'male_body_bald.jpg front/back only','measurements':measurements,
        'eye_checks':eyes,'source_verification':verify,'coordinate_edits':edit,'reference_conflict':limit,'unity_fbx_sha256':sha(fbx),'unity_prefab_sha256':re.search(r'PREFAB_SHA256=(\w+)',audit).group(1)}
(P/'male-form-d-metrics.json').write_text(json.dumps(report,indent=2))
rows=['| Authority view | Difference pixels | Union pixels | Blender error | Unity error | Blender–Unity difference | 2% gate |','|---|---:|---:|---:|---:|---:|---|']
for m in measurements:rows.append(f'| {m["view"]} | {m["difference_pixels"]:,} | {m["union_pixels"]:,} | {100*m["error"]:.4f}% | {100*m["unity_plate_error"]:.4f}% | {100*m["blender_unity_error"]:.4f}% | '+('PASS' if m['error']<=.02 else 'FAIL')+' |')
conflict=f'The frozen front and reflected back masks disagree by {100*limit["continuous_world_space_reference_distance"]:.4f}% in continuous world space. One closed mesh has a shared X/Z outline in these opposite orthographic views. The Jaccard triangle inequality places at least one continuous view at ≥{100*limit["continuous_world_space_minimax_lower_bound"]:.4f}%. This documents why a clean physical 2% fit conflicts with the authority pair. Native raster sampling adds boundary quantization, so this continuous bound is not presented as an exact impossibility proof for the separate native pixel gate. The actual gate remains the unchanged native XOR/union measurement above; the model is not claimed to attain the minimax optimum. Cameras, masks and reference images were not altered.'
technical=f'Continued the existing `HeroBase_Male_Silhouette.blend`, revising only `Body_M` vertex coordinates. No remesh, replacement mesh, female work, hair, rig or face textures. METRIC scale 1; A-pose; height {verify["height_metres"]:.6f} m; {verify["vertices"]:,} vertices / {verify["triangles"]:,} triangles. Connectivity, body transform/origin, all six locked reference cameras and three packed plates are unchanged. This jaw stage corrects the pointed under-chin using the full visible mesh profile against the fixed head-detail outer contour, with local lower-face coordinate fairing and smooth depth displacement. Central-only ray samples had missed the low off-centre chin tip. All vertices outside the accepted lower-face/neck region, including the hands, torso, legs and upper head above 1.54 m, are exactly unchanged from this stage checkpoint. Both native bald alpha masks remain identical to the checkpoint. Full head likeness and CHIN remain FAIL; fingers remain an unfinished blockout. Cosmetic features are a separate appearance preview.'
lines=['GATE: MALE_FORM '+gate,'','Latest direct user instruction: **each bald view ≤2.0%**. This supersedes the stored goal/brief’s older 5% rule. The 2% gate remains unmet.','',*rows,'','| Binary eye check | Flag | Evidence |','|---|---|---|']
for name,result in eyes['checks'].items():lines.append(f'| {name} | {result["flag"]} | {result["note"]} |')
lines += ['', 'These flags cover the five requested geometry defects, reviewed in Blender and the actual saved Unity prefab. They are not a claim that the full face or hands match the finished plate; those remain unpolished.', '',technical,
 '',f'Fresh FBX export and independent Blender re-import verification: PASS, maximum coordinate difference {verify["independent_fbx_reimport_maximum_coordinate_error_metres"]*1000:.7f} mm. Unity: importer scale 1, file scale 1, compression off, saved normals imported; one opaque plain grey URP Lit renderer, no hair. The main `HeroBase_Male` prefab uses this current FBX. All seven captures were taken in Play Mode and checked against their recorded SHA-256 values.',
 '',f'Foot contact: {verify["foot_low_contact_vertices"]} vertices below 4 mm, lowest contact {verify["foot_contact_height_range_metres"][0]*1000:.3f} mm. Female source, exports and prefabs retain their prerequisite hashes.',
 '',conflict,'','Proof: `01_silhouette_overlay_m.png` (native pink/green disagreement); `01d_form_m.png` (five Blender body views plus details); `03_unity_m.png` (five actual Unity body views plus details); `01e_head_all_around_m.png` (eight head directions plus top). Side views inform the arm/foot eye checks and do not introduce a separate numeric silhouette gate.',
 '',f'Saved blend SHA-256: `{sha(blend)}`',f'Source and Unity FBX SHA-256: `{sha(fbx)}`',f'Unity prefab SHA-256: `{report["unity_prefab_sha256"]}`']
(P/'MALE_FORM_RESULTS.md').write_text('\n'.join(lines)+'\n')
(P/'MALE_SIL_RESULTS.md').write_text('\n'.join(['GATE: MALE_SIL '+sil,'','Authority: ONLY `male_body_bald.jpg`, front/back, **each ≤2.0%**, per the latest human instruction. This replaces the historical 5% PASS for the earlier candidate.','',*rows,'',technical,'','Metric: count(reference XOR render) / count(reference OR render), each in the original 1280×720 image. Reference masks are the frozen source-only masks; Blender alpha >127. No refitting of masks, cameras, origins or image scales. Multiangle sheet views are excluded from the numeric gate; their locked cameras remain packed.','',conflict,'','Proof: `01_silhouette_overlay_m.png`.',f'Saved blend SHA-256: `{sha(blend)}`'])+'\n')
files=[blend,fbx,P/'01_silhouette_overlay_m.png',P/'01d_form_m.png',P/'03_unity_m.png',P/'MALE_FORM_RESULTS.md',P/'MALE_SIL_RESULTS.md',P/'MALE_FORM_REFERENCE_CONFLICT.png',
       P/'male-form-d-metrics.json',P/'male-form-d-eye-checks.json',P/'male-form-d-source-verification.json',P/'male-form-d-source-audit.json',P/'male-form-d-female-unchanged.json',P/'male-form-unity-audit.txt',P/'male-form-reference-limit.json',P/'male-form-export.json']
for path in files:shutil.copy2(path,O/path.name)
for backend in ['blender','unity']:
    out=O/backend;out.mkdir(exist_ok=True)
    for name in ['front','back','left','right','threequarter','head_side','foot_side']:
        path=F/('unity' if backend=='unity' else '')/(name+'.png');shutil.copy2(path,out/path.name)
        if backend=='unity':shutil.copy2(path.with_name(name+'_mask.png'),out/(name+'_mask.png'))
out=O/'native_masks';out.mkdir(exist_ok=True)
for m in measurements:
    name=m['view'];shutil.copy2(S/(name+'_target.png'),out/(name+'_target.png'));shutil.copy2(P/'male_silhouette'/(name+'.png'),out/(name+'.png'))
shutil.copy2(S/'reference-cameras.json',out/'reference-cameras.json')
manifest={str(p.relative_to(O)):sha(p) for p in O.rglob('*') if p.is_file() and p.name!='manifest.json'};(O/'manifest.json').write_text(json.dumps(manifest,indent=2))
print(json.dumps({'gate':report['gate'],'checks':{n:v['flag'] for n,v in eyes['checks'].items()},'bald_percent':{m['view']:100*m['error'] for m in measurements},'output':str(O)},indent=2))
