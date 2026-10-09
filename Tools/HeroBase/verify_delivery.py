"""Verify portable archives against the current source/export/Unity assets."""
from pathlib import Path
import json,hashlib,re,tarfile,zipfile,shutil
R=Path(__file__).resolve().parents[2];D=R/'ArtDir/hero/base_lock';P=D/'proof';U=R/'Unity/Assets/Characters/HeroBase'
O=Path('/Users/adnanyonathan/Documents/Codex/2026-09-30/open-3/outputs')
manifest=json.loads((P/'candidate-manifest.json').read_text());source=json.loads((P/'saved-source-audit.json').read_text())
sha=lambda data:hashlib.sha256(data).hexdigest()
with tarfile.open(O/'HeroBase_Candidate.unitypackage','r:gz') as t:
    packed={}
    for m in t.getmembers():
        if m.name.endswith('/pathname'):
            path=t.extractfile(m).read().decode('utf8').rstrip('\x00\n')
            asset=m.name.rsplit('/',1)[0]+'/asset'
            try:packed[path]=t.extractfile(asset).read()
            except KeyError:pass
    required=[]
    for sex,entry in manifest.items():
        for rel,e in entry['prefabs'].items():
            path='Assets/Characters/HeroBase/'+rel; data=packed[path]
            assert sha(data)==e['sha256']==sha((U/rel).read_bytes()),'Stale prefab in Unity package'
            txt=data.decode('utf8')
            roots=[b for b in re.split(r'^--- ',txt,flags=re.M) if b.startswith('!u!4 ') and re.search(r'^  m_Father: \{fileID: 0\}$',b,flags=re.M)]
            assert len(roots)==1
            for field,expected in [('m_LocalPosition','{x: 0, y: 0, z: 0}'),('m_LocalRotation','{x: 0, y: 0, z: 0, w: 1}'),('m_LocalScale','{x: 1, y: 1, z: 1}')]:
                assert field+': '+expected in roots[0], 'Nonidentity public prefab root'
            assert '\nAnimator:' not in txt
            if rel.startswith(('Bodies/','Hair/')):assert txt.count('\nMeshRenderer:')==1
            required.append(path)
        for rel,e in entry['exports'].items():
            path='Assets/Characters/HeroBase/Models/'+rel
            assert sha(packed[path])==e['sha256']==sha((D/'unity_import'/rel).read_bytes())
            required.append(path)
        for kind in ['Head_'+sex+'_Albedo.png']:
            path='Assets/Characters/HeroBase/Textures/'+kind
            assert sha(packed[path])==sha((D/'unity_import/Textures'/kind).read_bytes())
            required.append(path)
    for path,data in packed.items():
        current=R/'Unity'/path
        if current.is_file():assert sha(data)==sha(current.read_bytes()),'Stale packaged asset: '+path
    assert len([p for p in packed if p.endswith('.prefab')])==8
    assert len([p for p in packed if p.endswith('.fbx')])==4
with zipfile.ZipFile(O/'HeroBase_Modular_FBX.zip') as z:
    assert z.testzip() is None
    fbxs=[n for n in z.namelist() if n.endswith('.fbx')];assert len(fbxs)==4
    for f in fbxs:assert sha(z.read(f))==sha((D/'unity_import'/f).read_bytes())
with zipfile.ZipFile(O/'HeroBase_Blender.zip') as z:
    assert z.testzip() is None
    for row in source:
        f='HeroBase_'+row['sex']+'.blend'
        assert sha(z.read(f))==row['source_sha256']==sha((D/'blender'/f).read_bytes())
        assert row['source_images'] and all(x['packed'] for x in row['source_images'])
for sex,entry in manifest.items():
    assert sha((D/(sex.lower()+'_body_plain.jpg')).read_bytes())==entry['reference_sha256']
    assert entry['primary_base_is_bald'] and not entry['body_contains_hair']
metrics=json.loads((P/'unity-comparison-metrics.json').read_text())
assert all(v['primary_base_vs_independent_body_mask_difference_pixels']==0 for row in metrics['measurements'] for v in row['views'].values())
assert all(35000<=r['parts'][0]['triangles']<=50000 and sum(p['triangles'] for p in r['parts'])<=50000 for r in source)
orbit=json.loads((P/'orbit-comparison-metrics.json').read_text())
assert len(orbit['measurements'])==16 and orbit['full_body']
for row in orbit['measurements']:
    assert row['blender_unity_silhouette_difference_over_union']<.01
    delivered=O/'HeroBase_Review/allaround'/(row['sex'][0]+'_'+row['view']+'.png')
    assert sha(delivered.read_bytes())==sha((P/'unity'/(row['sex'][0]+'_allaround_'+row['view']+'.png')).read_bytes())
report={'status':'PASS','unity_prefabs':8,'primary_bases_bald':True,'independent_fbx_files':4,'public_prefab_roots_identity':True,'independent_prefab_renderer_count':1,'packaged_asset_hashes_match_current_assets':True,'packed_blender_images':True,'archive_integrity':'PASS','both_bald_and_optional_assembly_triangle_budgets':'PASS','reference_hashes':{sex:e['reference_sha256'] for sex,e in manifest.items()},'base_vs_body_only_render_difference_pixels':0,'overall_base_fidelity_gate':'FAIL','verified_asset_paths':required}
(P/'delivery-verification.json').write_text(json.dumps(report,indent=2));shutil.copy2(P/'delivery-verification.json',O/'HeroBase_Review/delivery-verification.json')
print(json.dumps(report,indent=2))
