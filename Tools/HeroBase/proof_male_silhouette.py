"""Native-render proof: only the bald body front/back authorize MALE_SIL."""
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import numpy as np,json,hashlib,shutil
R=Path(__file__).resolve().parents[2];D=R/'ArtDir/hero/base_lock';W=R/'work/male-silhouette';P=D/'proof';B=P/'male_silhouette'
O=Path('/Users/adnanyonathan/Documents/Codex/2026-09-30/open-3/outputs/Male_Silhouette');O.mkdir(parents=True,exist_ok=True)
views=json.loads((W/'reference-cameras.json').read_text())['views'];results=[];authority={'bald_front','bald_back'}
sha=lambda path:hashlib.sha256(path.read_bytes()).hexdigest()
font=ImageFont.truetype('/System/Library/Fonts/Helvetica.ttc',25);small=ImageFont.truetype('/System/Library/Fonts/Helvetica.ttc',18)
primary=np.array(Image.open(D/'male_body_bald.jpg').convert('RGB'))
diagnostic=Image.new('RGB',(1920,820),(29,30,33));dd=ImageDraw.Draw(diagnostic)
dd.text((18,12),'Diagnostic views · excluded from MALE_SIL PASS / FAIL',font=font,fill='white')
for i,v in enumerate(views):
    target_path=W/(v['name']+'_target.png');render_path=B/(v['name']+'.png')
    target=np.array(Image.open(target_path))>127
    render=Image.open(render_path);model=np.array(render.getchannel('A'))>127
    overlap=int((target&model).sum());union=int((target|model).sum());difference=int((target^model).sum());error=difference/union
    results.append({'view':v['name'],'gate_authority':v['name'] in authority,
        'symmetric_difference_over_union':error,'symmetric_difference_pixels':difference,'union_pixels':union,
        'target_pixels':int(target.sum()),'render_pixels':int(model.sum()),'target_mask_sha256':sha(target_path),
        'render_sha256':sha(render_path),'camera':v})
    source=primary if v['name'] in authority else np.array(Image.open(D/v['file']).convert('RGB'))
    source[target&model]=(.74*source[target&model]+.26*np.array([100,165,180])).astype(np.uint8)
    source[target&~model]=np.array([45,230,170]);source[model&~target]=np.array([240,90,150])
    if v['name'] not in authority:
        j=i-2;crop=Image.fromarray(source).crop(v['roi']);factor=640/crop.height
        crop=crop.resize((round(crop.width*factor),640),Image.Resampling.NEAREST)
        x=j*480;diagnostic.paste(crop,(x+(480-crop.width)//2,110))
        dd.text((x+18,65),v['name'].replace('_',' ')+f' · {100*error:.2f}%',font=font,fill='white')
gate='PASS' if all(r['symmetric_difference_over_union']<=.05 for r in results if r['gate_authority']) else 'FAIL'
panel=Image.new('RGB',(1280,822),(29,30,33));draw=ImageDraw.Draw(panel)
draw.text((18,10),'GATE: MALE_SIL '+gate+' · male_body_bald.jpg front + back only',font=font,fill='white')
draw.text((18,43),'Green: reference only   Pink: Body_M only   Tinted: overlap   |   fixed cameras · native 1280 × 720 masks',font=small,fill='white')
front,back=results[:2]
draw.text((18,70),f'bald_front: {front["symmetric_difference_over_union"]*100:.4f}%   |   bald_back: {back["symmetric_difference_over_union"]*100:.4f}%   |   each ≤ 5.0%',font=small,fill='white')
panel.paste(Image.fromarray(primary),(0,102));dst=P/'01_silhouette_overlay_m.png';panel.save(dst)
diagnostic_path=P/'01_silhouette_diagnostic_m.png';diagnostic.save(diagnostic_path)
report={'gate':'MALE_SIL '+gate,'threshold':.05,'phase':'Micro1 continuation: male A-pose silhouette only',
        'gate_authority':['bald_front','bald_back'],'authority_file':'male_body_bald.jpg',
        'excluded_from_gate':['sheet_front','sheet_back','left','right'],
        'metric':'Native frozen source mask >127 versus Blender alpha coverage >127; symmetric difference divided by union. No crop, rescale, registration, camera change or image warp in measurement. Primary proof retains the native plate pixels with a header added.',
        'measurements':results}
(B/'silhouette-metrics.json').write_text(json.dumps(report,indent=2))
audit=json.loads((B/'source-audit.json').read_text());blend=D/'blender/HeroBase_Male_Silhouette.blend'
assert audit['blend_sha256']==sha(blend)
lines=['GATE: MALE_SIL '+gate,'',
       '**Authority for this gate: ONLY `male_body_bald.jpg`, front and back. Each must have symmetric difference / union ≤ 5.0%.**',
       '', '| Authority view | Difference pixels | Union pixels | Error / union | Gate |','|---|---:|---:|---:|---|']
lines += [f'| {r["view"]} | {r["symmetric_difference_pixels"]:,} | {r["union_pixels"]:,} | {r["symmetric_difference_over_union"]*100:.4f}% | '+('PASS' if r['symmetric_difference_over_union']<=.05 else 'FAIL')+' |' for r in results if r['gate_authority']]
lines += ['', 'Continued from the saved Micro1 `HeroBase_Male_Silhouette.blend`; no regeneration or remesh. The existing male mesh was named `Body_M`, and only its vertex coordinates were revised. Shoulder/arm contours were narrowed where excessive, with crown, inner-thigh and heel contour corrections. Depth coordinates, topology, object transforms/origin, six camera transforms/origins/scales, packed references and grey material are unchanged.',
          '',f'Height: {audit["height_metres"]:.6f} m. Unit system: METRIC, scale 1. A-pose. Vertices: {audit["vertices"]:,}; triangles: {audit["triangles"]:,} (unchanged). Maximum vertex displacement: {audit["maximum_vertex_displacement_metres"]*1000:.2f} mm.',
          '', 'No female, Unity, hair, stubble, textures, rig or face polish. Facial shading visible in the overlay is the source photograph; native blockout renders are plain grey.',
          '', f'Proof: `{dst}`',f'Saved continuation: `{blend}`',
          '', 'Frozen source masks and alpha cutoff are unchanged. Error = count(reference XOR render) / count(reference OR render), measured in the full native 1280×720 images. Source-only mask extraction excludes floor shadows and labels, fills enclosed shading holes, and uses +7.5 sRGB levels against row-border background. Boundary uncertainty is approximately 1–2 source pixels. No candidate render changes a reference mask, origin or scale.',
          '', '| Diagnostic view (excluded from gate) | Error / union |','|---|---:|']
lines += [f'| {r["view"]} | {r["symmetric_difference_over_union"]*100:.4f}% |' for r in results if not r['gate_authority']]
lines += ['', 'The multiangle cameras and references remain locked and packed. Their measurements are informational and do not determine this gate.',
          '', f'Continuation source SHA-256: `{audit["continuation_source_sha256"]}`',
          f'Saved mesh connectivity SHA-256: `{audit["connectivity_sha256"]}`',
          f'Saved blend SHA-256: `{audit["blend_sha256"]}`']
(P/'MALE_SIL_RESULTS.md').write_text('\n'.join(lines)+'\n')
for f in [dst,diagnostic_path,P/'MALE_SIL_RESULTS.md',B/'silhouette-metrics.json',B/'source-audit.json',W/'reference-cameras.json',W/'contour-revision.json',blend]:shutil.copy2(f,O/f.name)
native=O/'native';native.mkdir(exist_ok=True)
for v in views:shutil.copy2(B/(v['name']+'.png'),native/(v['name']+'.png'))
print(json.dumps({'gate':gate,'errors':{r['view']:r['symmetric_difference_over_union'] for r in results if r['gate_authority']},'proof':str(dst)},indent=2))
