"""Package truthful, current proofs for the user's replacement bald references."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import numpy as np, json, hashlib, shutil, re, zipfile
from plate_profiles import MALE, FEMALE
R=Path(__file__).resolve().parents[2]
D=R/'ArtDir/hero/base_lock'; P=D/'proof'
OUT=Path('/Users/adnanyonathan/Documents/Codex/2026-09-30/open-3/outputs')
O=OUT/'HeroBase_Review'; O.mkdir(parents=True,exist_ok=True)
U=R/'Unity/Assets/Characters/HeroBase'
font=ImageFont.truetype('/System/Library/Fonts/Helvetica.ttc',20)
small=ImageFont.truetype('/System/Library/Fonts/Helvetica.ttc',15)
source=json.loads((P/'saved-source-audit.json').read_text())
blender=json.loads((P/'silhouette-metrics.json').read_text())
unity_audit=(P/'unity-mesh-material-audit.txt').read_text()
bounds_audit=(P/'unity-import.txt').read_text()
plates=json.loads((D/'plate-measurements.json').read_text())
consistency=json.loads((P/'reference-consistency.json').read_text())
reference_conflict='; '.join(f"{r['sex']} {r['reference_front_vs_mirrored_back_jaccard_distance']*100:.2f}%" for r in consistency['measurements'])
views_order=['front','back','threequarter']
orbit_views=['front','front_right','right','back_right','back','back_left','left','front_left']
results=[]
orbit_results=[]
for sex,suf,pr in [('Male','M',MALE),('Female','F',FEMALE)]:
    panel=Image.new('RGB',(1680,770),(32,32,36)); draw=ImageDraw.Draw(panel)
    draw.text((18,12),f'HeroBase_{sex} — BALD BASE · current candidate · GATE: BASE FAIL',font=font,fill='white')
    removal=Image.new('RGB',(1680,1500),(32,32,36)); rd=ImageDraw.Draw(removal)
    rd.text((18,12),f'{sex} — separate optional hairstyle / after removal · actual Unity renders',font=font,fill='white')
    views={}
    for i,view in enumerate(views_order):
        shot=Image.open(P/'unity'/(suf+'_'+view+'.png')).convert('RGB')
        panel.paste(shot.crop((360,0,920,720)),(560*i,50))
        draw.text((560*i+16,40),view,font=small,fill='white')
        optional=Image.open(P/'unity'/(suf+'_withhair_'+view+'.png')).convert('RGB')
        bald=Image.open(P/'unity'/(suf+'_bald_'+view+'.png')).convert('RGB')
        removal.paste(optional.crop((360,0,920,720)),(560*i,50))
        rd.text((560*i+16,37),'Optional hairstyle · '+view,font=small,fill='white')
        removal.paste(bald.crop((360,0,920,720)),(560*i,780))
        rd.text((560*i+16,767),'After hair removal · '+view,font=small,fill='white')
        a=np.array(Image.open(P/'blender'/(suf+'_'+view+'.png')).getchannel('A'))>127
        mask=np.array(Image.open(P/'unity'/(suf+'_'+view+'_mask.png')).convert('RGB')).mean(axis=2)>127
        bm=np.array(Image.open(P/'unity'/(suf+'_bald_'+view+'_mask.png')).convert('RGB')).mean(axis=2)>127
        views[view]={'blender_unity_silhouette_difference_over_union':float((a^mask).sum()/(a|mask).sum()),'primary_base_vs_independent_body_mask_difference_pixels':int((mask^bm).sum())}
        assert np.array_equal(np.asarray(shot),np.asarray(bald)), 'Bald main prefab and body-only prefab render differently'
        if view in ['front','back']:
            cx=pr['cx' if view=='front' else 'back_cx']
            b=np.array(Image.open(P/'masks'/(suf+'_'+view+'_target.png')))>0
            shifted=Image.new('L',(1280,720)); shifted.paste(Image.fromarray(np.uint8(mask)*255),(cx-640,0)); c=np.array(shifted)>0
            views[view]['unity_plate_silhouette_difference_over_union']=float((b^c).sum()/(b|c).sum())
    panel.save(P/('03_unity_'+suf.lower()+'.png'))
    removal.save(P/('04_hair_removal_'+suf.lower()+'.png'))
    orbit=Image.new('RGB',(2240,1540),(32,32,36));od=ImageDraw.Draw(orbit)
    od.text((18,12),f'{sex} bald base — full-body views every 45° · '+('clean skin; no stubble' if sex=='Male' else 'head geometry preserved'),font=font,fill='white')
    for i,view in enumerate(orbit_views):
        im=Image.open(P/'unity'/(suf+'_allaround_'+view+'.png')).convert('RGB')
        ba=np.array(Image.open(P/'blender'/(suf+'_allaround_'+view+'.png')).getchannel('A'))>127
        um=np.array(Image.open(P/'unity'/(suf+'_allaround_'+view+'_mask.png')).convert('L'))>127
        delta=float((ba^um).sum()/(ba|um).sum())
        assert delta<.01,'All-around export differs from source: '+sex+' '+view
        orbit_results.append({'sex':sex,'view':view,'degrees':i*45,'blender_unity_silhouette_difference_over_union':delta,'native_resolution':[1280,720]})
        x=(i%4)*560;y=50+(i//4)*744
        orbit.paste(im.crop((360,0,920,720)),(x,y))
        od.text((x+16,y-20),view.replace('_',' '),font=small,fill='white')
    orbit.save(P/('05_allaround_'+suf.lower()+'.png'))
    results.append({'sex':sex,'views':views})
(P/'orbit-comparison-metrics.json').write_text(json.dumps({'views_per_base':8,'full_body':True,'method':'Actual orthographic Blender and Unity captures, fixed metre/pixel framing, rotating studio lights; alpha coverage masks threshold 127. Contact sheets crop only empty horizontal margins.','measurements':orbit_results},indent=2))
# Native-image crops use identical scale, with no shape transformation.
face=Image.new('RGB',(1344,475),(36,36,39)); fd=ImageDraw.Draw(face)
for i,(sex,suf,pr,y0,y1) in enumerate([('Male','M',MALE,55,185),('Female','F',FEMALE,19,156)]):
    for j,(label,path,cx) in enumerate([(sex+' new reference',D/(sex.lower()+'_body_plain.jpg'),pr['cx']),(sex+' bald Unity base',P/'unity'/(suf+'_front.png'),640)]):
        x=(2*i+j)*336
        im=Image.open(path).convert('RGB').crop((cx-52,y0,cx+52,y1)).resize((312,(y1-y0)*3),Image.Resampling.LANCZOS)
        face.paste(im,(x+12,48)); fd.text((x+12,16),label,font=small,fill='white')
face.save(P/'face-comparison.png')
heads=Image.new('RGB',(1440,780),(32,32,36)); hd=ImageDraw.Draw(heads)
hd.text((18,12),'Complete bald head / separate optional hairstyle · Unity URP',font=font,fill='white')
for row,(sex,suf) in enumerate([('Male','M'),('Female','F')]):
    y=55+row*355; y0,y1=(30,185) if sex=='Male' else (12,156)
    for col,(view,optional) in enumerate([(v,o) for v in views_order for o in [False,True]]):
        hd.text((col*240+8,y),sex+' '+view+(' · optional' if optional else ' · bald'),font=small,fill='white')
        path=P/'unity'/(suf+('_withhair' if optional else '')+'_'+view+'.png')
        im=Image.open(path).convert('RGB').crop((585,y0,695,y1)).resize((224,round((y1-y0)*224/110)),Image.Resampling.LANCZOS)
        heads.paste(im,(col*240+8,y+26))
heads.save(P/'hair-removal-heads.png')
(P/'unity-comparison-metrics.json').write_text(json.dumps({'method':'Native 1280x720 Unity images. Separate geometry captures use transparent clear, opaque unlit geometry, SSAO disabled, linear render target and 4x MSAA. Alpha coverage is saved as RGB bytes; threshold 127 matches Blender alpha. Bald main prefab and independently instantiated body prefab must render identically. Plate comparisons use frozen metre/pixel scale with x-only framing translation. No image warp or target adjustment. Shading and face fidelity remain visual gates.','measurements':results},indent=2))
manifest={}; metric_rows=[]; removal_rows=[]; topology=[]; total_budget=True
for row in source:
    sex=row['sex']; suf=sex[0]; body,hair=row['parts']; total=body['triangles']+hair['triangles']; attachment=row['attachment']; exports={}
    assert row['plate_hash_matches'] and row['armatures']==0
    assert set(row['meshes'])=={'Body_'+suf,'Hair_'+suf}
    assert body['scalp_boundary_edges']==body['scalp_nonmanifold_edges']==body['hair_materials']==0
    assert hair['boundary_edges']==hair['nonmanifold_edges_more_than_two_faces']==0
    for marker in [' REMOVE_HAIR_ROOT body_mesh_unchanged=True body_renderers=1',' BODY_PREFAB hair_dependencies=0',' BASE_PREFAB bald=True hair_dependencies=0']:
        assert sex+marker in unity_audit
    for label,n in [('BASE_BALD',1),('BALD',1),('OPTIONAL_HAIR',2)]:
        assert re.search(sex+' '+label+r' .* renderers='+str(n),bounds_audit)
    for part in row['parts']:
        match=re.search(sex+' '+part['name']+r' vertices=\d+ tris=(\d+)',unity_audit)
        assert match and int(match.group(1))==part['triangles']
    for asset in row['exports']:
        src=D/'unity_import'/asset['path']; dst=U/'Models'/src.name
        sha=hashlib.sha256(src.read_bytes()).hexdigest()
        assert sha==asset['sha256']==hashlib.sha256(dst.read_bytes()).hexdigest(),'Stale Unity model'
        exports[asset['path']]={'sha256':sha,'mesh_count':len(asset['mesh_names']),'triangles':asset['triangles'],'materials':asset['materials']}
    prefabs={}
    for rel in [f'HeroBase_{sex}.prefab',f'Bodies/HeroBase_{sex}_Body.prefab',f'Hair/Hair_Default_{sex}.prefab',f'Previews/HeroBase_{sex}_DefaultHair.prefab']:
        f=U/rel; prefabs[rel]={'sha256':hashlib.sha256(f.read_bytes()).hexdigest(),'path':str(f)}
    manifest[sex]={'reference_sha256':plates[sex.lower()]['sha256'],'source_blend_sha256':row['source_sha256'],'exports':exports,'prefabs':prefabs,'hair_anchor_metres':attachment['hair_anchor_metres'],'primary_base_is_bald':True,'body_contains_hair':False,'scalp_boundary_edges':0,'scalp_nonmanifold_edges':0}
    bald=float(re.search(sex+r' BASE_BALD .* HEIGHT=([0-9.]+)',bounds_audit).group(1))
    styled=float(re.search(sex+r' OPTIONAL_HAIR .* HEIGHT=([0-9.]+)',bounds_audit).group(1))
    assert 1.65<=bald<=1.75 and 35000<=body['triangles']<=50000
    total_budget &= total<=50000
    metric_rows.append(f'| {sex} | {body["triangles"]:,} | {hair["triangles"]:,} | {total:,} | {bald:.6f} | {styled:.6f} |')
    removal_rows.append(f'| {sex} | Body_{suf} only; no hair material | main/base: one renderer, zero hairstyle dependencies | 0 open / 0 nonmanifold scalp edges | {bald:.6f} m |')
    topology.append(f'{sex}: {body["boundary_edges"]} body boundary edges and {body["nonmanifold_edges_more_than_two_faces"]} edges with more than two incident faces')
(P/'candidate-manifest.json').write_text(json.dumps(manifest,indent=2))
lines=['GATE: BASE FAIL','','Current candidate uses the two replacement bald references supplied on 2026-09-30. The main bases are bald and the independent-hair requirement passes. Overall reference fidelity is still unfinished.','','Reference input/framing conflict: the frozen front and mirrored-back source traces differ by '+reference_conflict+'. A single fixed orthographic silhouette cannot be within 5% of both under this framing. The existing targets and gates remain unchanged. REFERENCE_CONSISTENCY.md and reference-consistency.png document the measurement. Face and surface differences still require work independently of that conflict.','','| Required line | Result | Evidence |','|---|---|---|']
for row in blender['measurements']:
    m=row['metrics']; lines.append(f"| {row['sex'].title()} front/back silhouette ≤ ~5% | FAIL | {m['front']['symmetric_difference_over_union']*100:.2f}% / {m['back']['symmetric_difference_over_union']*100:.2f}%; 01_silhouette_overlay_{row['sex'][0]}.png and silhouette-metrics.json. |")
lines += [
'| Bald skull, jaw and facial features match the new references | FAIL | face-comparison.png: face depth, cheek/chin transitions, nose, eyelids, mouth and ear forms still differ. |',
'| Unity preserves the reference appearance and soft plastic shading | FAIL | 03_unity_m/f.png: hard facial shadows, generic body forms and shading differences remain. Matching imported geometry does not establish art fidelity. |',
'| Bald primary bases; independent optional hair; complete scalp | PASS | Both primary prefabs and body-only prefabs have one body renderer and zero hair dependencies. Deleting HairRoot retains the same body mesh. Complete scalp has zero boundary or nonmanifold edges. 04_hair_removal_m/f.png, hair-removal-heads.png and audits. |',
'| Separate opaque solid optional hair | PASS | Each independent hairstyle FBX has one closed hair mesh. Unity URP Lit opaque surface=0, queue=2000. Optional previews are separate from the bald base proofs. |',
'| Metres, foot origin and height | PASS | '+ '; '.join(f"{r['sex']} {r['parts'][0]['height_metres']:.6f} m" for r in source)+'. Source transforms and public prefab roots are identity; FBX scale 1, Unity importer global/file scale 1. Actual bounds in unity-import.txt. |',
'| 35–50k body triangles | PASS | '+ '; '.join(f"{r['sex']} {r['parts'][0]['triangles']:,}" for r in source)+'. Source, independently re-imported FBX and Unity counts agree. |',
'| Optional hairstyle assembly ≤50k triangles | '+('PASS' if total_budget else 'FAIL')+' | '+ '; '.join(f"{r['sex']} {sum(p['triangles'] for p in r['parts']):,}" for r in source)+'. |',
'| FBX normals and face textures | PASS | FBX scale 1, -Z/Y. Unity mesh compression off, normals imported. Head albedo: 2048, sRGB, mipmaps, Crunch off, uncompressed default importer. Pigment only; no baked directional lighting. |',
'| Named prefabs and full-body views all around | PASS | 05_allaround_m/f.png show eight actual Unity views every 45 degrees. Canonical front/back/three-quarter proofs and separate hair-removal proofs also retained. |',
'| No clothes, rig, Mixamo or Animator | PASS | Each saved source contains only its Body and optional Hair meshes, no armature. Static exports and prefabs. |',
'| Required proofs and gate report | PASS | Named 01/02/03 proofs and this report exist. |','','Technical passes do not make BASE PASS.','','Male revision: stubble pigment removed completely; lower jaw tapers into a rounded chin that closes in front of the throat. Female head geometry preserved. Eight full-body views per base expose both sides and the back.','','Proof method: independent manual tracing of unchanged authority JPEGs, estimated boundary precision ±2 pixels. Native orthographic renders use frozen metres/pixel and horizontal framing translation. The metric is symmetric difference / union at alpha coverage threshold 127. Unity mask capture disables SSAO and writes linear alpha coverage as RGB. No proof warp or target adjustment.','','Studio shading: dedicated HeroBaseStudioURP asset uses full-resolution SSAO radius 0.025m, intensity 0.75, 12 samples, 4x MSAA and four per-pixel additional lights. This reduces the broad facial darkening caused by the previous 0.3m/downsampled SSAO setting.','','Topology limit: scalp and hair are closed. The full body is not a single watertight shell. '+ '; '.join(topology)+'.','','Primary bald prefabs:']
lines += [str(U/('HeroBase_'+sex+'.prefab')) for sex in ['Male','Female']]
lines += ['','Authoring sources with packed images:']+[str(D/'blender'/('HeroBase_'+sex+'.blend')) for sex in ['Male','Female']]
lines += ['','Static Unity preview scene:',str(U/'HeroBase_Studio.unity'),'','Overall goal remains active; no reference-fidelity completion claim.']
(P/'GATE_RESULTS.md').write_text('\n'.join(lines)+'\n')
(D/'METRICS.md').write_text('# Current bald base measurements\n\nThe current reference hashes and landmarks are recorded in plate-measurements.json. Earlier hair-on references are archived.\n\n| Base | Bald body triangles | Optional hair triangles | Optional assembly triangles | Bald height m | Optional assembly height m |\n|---|---:|---:|---:|---:|---:|\n'+'\n'.join(metric_rows)+'\n\nSource, independent FBX re-import and Unity counts agree.\n\nCurrent silhouette errors: '+'; '.join(f"{r['sex'].title()} front {r['metrics']['front']['symmetric_difference_over_union']*100:.2f}%, back {r['metrics']['back']['symmetric_difference_over_union']*100:.2f}%" for r in blender['measurements'])+'. These fail the ~5% gate.\n\nSee proof/GATE_RESULTS.md and the machine-readable audits.\n')
pivots='\n'.join(f"| {r['sex']} | (0, {r['attachment']['hair_anchor_metres'][1]:.7f}, 0) |" for r in source)
(D/'SCALE.md').write_text('''# Bald base scale and hair attachment contract

The primary HeroBase_Male/Female prefabs are bald. Each complete Body contains its scalp, ears, face and neck. Hair is an independent optional asset; it supplies no missing head surface.

Blender units are metres, body origins are at the foot midpoint, source transforms are identity. FBX scale 1, FBX All, -Z forward/Y up. Unity global/file scales are 1; axis conversion is confined to imported mesh children. Public prefab roots stay at position/rotation zero, scale one. Measured bald heights are recorded in METRICS.md; both are within 1.65–1.75m.

Main prefabs have one body renderer, no hairstyle dependency, and an empty HairRoot. Bodies/ contains independent body prefabs. Hair/ contains separate head-local opaque hairstyle prefabs. Previews/ contains optional assemblies. Instantiate the matching style under HairRoot with local position/rotation zero, scale one. Deleting HairRoot or replacing its style child leaves the same body mesh intact.

| Base | Unity Y-up HairRoot position in metres |
|---|---|
'''+pivots+'''

New styles must fit the complete head around the matching pivot. Do not merge hair into Body or rely on a hairstyle to cover missing scalp. Future rigging may parent HairRoot to the head transform. The delivered scope is static, with no skeleton, Mixamo, Animator, clothes or clips.

Bald body target is 35–50k triangles. Optional preview assembly counts are in METRICS.md. Head albedo is 2048 sRGB with mipmaps, no Crunch, uncompressed default importer. Pigment contains no hair cap or baked illumination.

Full body topology and reference fidelity are not approved by this technical attachment contract. See proof/GATE_RESULTS.md.
''')
(P/'HAIR_REMOVAL_RESULTS.md').write_text('GATE: REMOVABLE HAIR PASS\n\nBoth primary HeroBase prefabs are bald by default, matching the replacement reference scope.\n\n| Base | Independent body FBX | Unity primary and body-only prefab | Scalp topology | Bald height |\n|---|---|---|---|---|\n'+'\n'.join(removal_rows)+'\n\nThe actual Unity deletion check removes HairRoot and retains the same body mesh. Independently instantiated bald main/base prefabs and body-only prefabs produce identical native images and geometry masks in all three views. Front, back and three-quarter scalp proofs show complete heads without a hairstyle. Source, independently imported FBX and Unity body counts agree.\n\nUse HeroBase_Male/Female.prefab for the bald base. Optional styles are under Hair/ and optional assemblies under Previews/. Attachment coordinates are in SCALE.md and the Unity README.\n\nThe overall BASE gate remains FAIL for reference silhouettes, facial fidelity and shading.\n')
files=['01_silhouette_overlay_m.png','01_silhouette_overlay_f.png','02_match_m.png','02_match_f.png','03_unity_m.png','03_unity_f.png','04_hair_removal_m.png','04_hair_removal_f.png','hair-removal-heads.png','HAIR_REMOVAL_RESULTS.md','face-comparison.png','GATE_RESULTS.md','BLENDER_STARTUP_DIAGNOSIS.md','candidate-manifest.json','silhouette-metrics.json','unity-comparison-metrics.json','saved-source-audit.json','unity-import.txt','unity-mesh-material-audit.txt','REFERENCE_CONSISTENCY.md','reference-consistency.json','reference-consistency.png']
files+=['05_allaround_m.png','05_allaround_f.png','orbit-comparison-metrics.json']
for name in files:shutil.copy2(P/name,O/name)
orbit_dir=O/'allaround';orbit_dir.mkdir(exist_ok=True)
for suffix in ['M','F']:
    for view in orbit_views:
        shutil.copy2(P/'unity'/(suffix+'_allaround_'+view+'.png'),orbit_dir/(suffix+'_'+view+'.png'))
shutil.copy2(Path(__file__).with_name('orbit_review.html'),O/'Full_Character_Review.html')
for name in ['REFERENCE_CHANGE.md','plate-measurements.json','SCALE.md','METRICS.md']:shutil.copy2(D/name,O/name)
shutil.copy2(U/'README.md',O/'HAIR_CUSTOMIZATION.md')
with zipfile.ZipFile(OUT/'HeroBase_Modular_FBX.zip','w',compression=zipfile.ZIP_DEFLATED) as z:
    for path in sorted((D/'unity_import').rglob('*')):
        if path.is_file() and (path.name.endswith('_Body.fbx') or path.name.startswith('Hair_Default_') or path.suffix in ['.json','.png']):z.write(path,str(path.relative_to(D/'unity_import')))
    for name in ['SCALE.md','METRICS.md','REFERENCE_CHANGE.md','plate-measurements.json']:z.write(D/name,name)
    z.write(P/'HAIR_REMOVAL_RESULTS.md','HAIR_REMOVAL_RESULTS.md');z.write(P/'GATE_RESULTS.md','GATE_RESULTS.md')
with zipfile.ZipFile(OUT/'HeroBase_Blender.zip','w',compression=zipfile.ZIP_DEFLATED) as z:
    for sex in ['Male','Female']:z.write(D/'blender'/('HeroBase_'+sex+'.blend'),'HeroBase_'+sex+'.blend')
    for name in ['SCALE.md','METRICS.md','REFERENCE_CHANGE.md']:z.write(D/name,name)
    z.write(P/'GATE_RESULTS.md','GATE_RESULTS.md')
print(json.dumps(results,indent=2));print('REVIEW_WRITTEN',O)
