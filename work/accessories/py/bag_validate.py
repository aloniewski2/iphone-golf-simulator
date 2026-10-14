"""BAG: validate the per-sex output contract. plain python3 + numpy.  python3 py/bag_validate.py"""
import os, json, sys
import numpy as np
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..'))
REAL = {'Back_Bag', 'UpperChest', 'Chest', 'Spine', 'RightShoulder', 'RightUpperArm', 'Root', 'Hips'}
allok = True
for sex in ('Male', 'Female'):
    d = os.path.join(ROOT, 'out', sex, 'golf_bag')
    meta = json.load(open(os.path.join(d, 'piece.json')))
    g = np.load(os.path.join(d, 'Kit_Bag_final.npz'))
    rep = {}
    n = len(g['pos']); t = len(g['tri'])
    rep['verts'] = n; rep['tris'] = t
    rep['tris_le_30k'] = t <= 30000
    rep['texture'] = meta['piece']['texture']
    bi, bw = g['bi'], g['bw']
    rep['weights_sum_ok'] = bool(np.abs(bw.sum(1) - 1).max() < 1e-4)
    rep['max_influences'] = int((bw > 1e-6).sum(1).max())
    rep['bone_index_range_ok'] = bool(bi.max() < len(meta['fit']['bones']) and bi.min() >= 0)
    ent = meta['piece']
    rep['bones'] = ent['bones']
    rep['bones_valid'] = all(b in REAL for b in ent['bones'])
    rep['no_armblend'] = not any(b.startswith('ArmBlend') for b in ent['bones'])
    rep['uv_in_unit'] = bool(g['uv'].min() >= -1e-6 and g['uv'].max() <= 1 + 1e-6)
    rep['normals_unit'] = bool(np.abs(np.linalg.norm(g['nrm'], axis=1) - 1).max() < 1e-3)
    binsz = os.path.getsize(os.path.join(d, 'Kit_Bag.bin'))
    rep['bin_size_ok'] = binsz == n * (12 + 12 + 8 + 16 + 16) + t * 12
    rep['submeshes'] = ent['submeshes']
    rep['bindposes_floats'] = len(ent['bindposes']) == 16 * len(ent['bones'])
    rep['maps'] = {k: os.path.exists(os.path.join(d, f'Bag_{k}.png')) for k in 'NM'}
    for k in ('slot', 'parent_bone', 'socket', 'pieces', 'tag'): rep[k] = meta[k]
    rep['contract_keys'] = all(k in meta for k in ('item', 'sex', 'slot', 'parent_bone', 'npz', 'tag', 'pieces', 'entries', 'socket', 'socket_local_position', 'socket_local_rotation', 'fit', 'fit_report'))
    ok = all(v for k, v in rep.items() if isinstance(v, bool)) and rep['texture'] == 4096 and all(rep['maps'].values()) and rep['max_influences'] <= 4
    allok &= ok
    print(sex, 'PASS' if ok else 'FAIL', json.dumps({k: v for k, v in rep.items() if k not in ('submeshes',)}))
    json.dump(rep, open(os.path.join(d, 'validate.json'), 'w'), indent=1)
sys.exit(0 if allok else 1)
