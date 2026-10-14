"""Arm through shirt: arm skin (beyond the sleeves: forearm, hand, and upper arm below the sleeve hem) that is INSIDE the posed shirt's torso
(behind its outer wall, within 4 cm) per clip.  Blender ... -- <hero> [old] [clip filter]"""
import sys, os, json
sys.path.insert(0, os.path.dirname(__file__))
import numpy as np, cv2io, glib as G
from mathutils import Vector
from mathutils.bvhtree import BVHTree
a = sys.argv[sys.argv.index('--') + 1:]
hero_id = a[0]; old = len(a) > 1 and a[1] == 'old'; filt = a[2] if len(a) > 2 else ''
hero = cv2io.Hero(hero_id); body = hero.body
if old:
    p = hero.kit['Kit_Top']; idx = np.array([body.bones.index(n) for n in p.bones])
    gpos, gtri, gbi, gbw = p.pos, p.tri, idx[p.bi], p.bw
    torso_face = np.ones(len(gtri), bool)
else:
    g = np.load(os.path.join(cv2io.ROOT, 'out', hero_id, 'unity', 'Kit_Top_final.npz'))
    gpos, gtri, gbi, gbw = g['pos'], g['tri'][g['part'] == 0], g['bi'], g['bw']
# arm vertices: forearm / hand, and their side
W = np.zeros((len(body.pos), len(body.bones)))
for k in range(4): np.add.at(W, (np.arange(len(body.pos)), body.bi[:, k]), body.bw[:, k])
def grp(pred): return W[:, [i for i, n in enumerate(body.bones) if pred(n)]].sum(1)
fore = grp(lambda n: any(k in n for k in ['LowerArm', 'Hand', 'Thumb', 'Index', 'Middle', 'Ring', 'Little']) or n.startswith('ArmBlend_8_9') or n.startswith('ArmBlend_27_28') or n.startswith('ArmBlend_9_10') or n.startswith('ArmBlend_28_29')) > .5
arm_idx = np.nonzero(fore)[0]
# garment torso faces: dominated by torso bones (not the sleeves)
gW = np.zeros((len(gpos), len(body.bones)))
for k in range(4): np.add.at(gW, (np.arange(len(gpos)), gbi[:, k]), gbw[:, k])
tor_v = gW[:, [i for i, n in enumerate(body.bones) if n in ('Hips', 'Spine', 'Chest', 'UpperChest')]].sum(1) > .6
gtri_t = gtri[tor_v[gtri].all(1)]
res = {}
for c in hero.clips:
    if filt and filt not in c['name']: continue
    bf, bones = hero.clip(c['name'])
    worst = 0; cnt = 0
    for fi in range(len(bf)):
        skin = hero.skin_matrices(bones[fi].astype(np.float64))
        gp = cv2io.lbs(gpos, gbi, gbw, skin)
        tree = BVHTree.FromPolygons([tuple(p) for p in gp], [tuple(t) for t in gtri_t], all_triangles=True)
        hips = bones[fi][body.bones.index('Hips')][:3, 3].astype(np.float64); neck = bones[fi][body.bones.index('Neck')][:3, 3].astype(np.float64)
        ax = neck - hips; ax /= np.linalg.norm(ax)
        n = 0
        for v in arm_idx[::2]:
            q = bf[fi][v].astype(np.float64)
            rad = (q - hips) - ax * ((q - hips) @ ax)
            if np.linalg.norm(rad) < 1e-6: continue
            rad /= np.linalg.norm(rad)
            # inside the shirt's torso tube = an outward ray crosses its wall an odd number of times (within 25 cm)
            hits = 0; o = q.copy(); dist = 0; first = None
            for _ in range(4):
                r = tree.ray_cast(Vector(o + rad * 1e-4), Vector(rad), .25 - dist)
                if r[0] is None: break
                hits += 1; step = np.linalg.norm(np.array(r[0]) - o); dist += step; o = np.array(r[0])
                if first is None: first = step
            if hits % 2 == 1 and first is not None:
                n += 1; worst = max(worst, first)
        cnt = max(cnt, n)
    res[c['name']] = dict(max_mm=round(worst * 1000, 1), verts=cnt)
json.dump(res, open(os.path.join(cv2io.ROOT, 'out', hero_id, 'cross_%s.json' % ('old' if old else 'new')), 'w'), indent=1)
tot = sum(v['verts'] for v in res.values())
print(('OLD' if old else 'NEW'), 'clips with arm inside shirt:', sum(1 for v in res.values() if v['verts'] > 0), '/', len(res), ' sum of worst-frame counts', tot)
for k, v in sorted(res.items(), key=lambda kv: -kv[1]['verts'])[:8]: print('   ', k, v)
