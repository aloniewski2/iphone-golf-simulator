"""Arm sunk in the shirt (same definition for old and new kits): forearm / hand skin that lies OUTSIDE the torso skin but INSIDE the shirt
(a ray from the point away from the torso meets the shirt's outer surface before 6 cm, and the point is less than 6 cm off the torso skin),
counted on the posed frames of every clip.  Blender ... -- <hero> old|new [clip filter]"""
import sys, os, json
sys.path.insert(0, os.path.dirname(__file__))
import numpy as np, cv2io, glib as G
from mathutils import Vector
from mathutils.bvhtree import BVHTree
a = sys.argv[sys.argv.index('--') + 1:]
hero_id, which = a[0], a[1]; filt = a[2] if len(a) > 2 else ''
hero = cv2io.Hero(hero_id); body = hero.body
if which == 'old':
    p = hero.kit['Kit_Top']; idx = np.array([body.bones.index(n) for n in p.bones])
    gpos, gtri, gbi, gbw = p.pos, p.tri, idx[p.bi], p.bw
else:
    g = np.load(os.path.join(cv2io.ROOT, 'out', hero_id, 'unity', 'Kit_Top_final.npz'))
    gpos, gtri, gbi, gbw = g['pos'], g['tri'], g['bi'], g['bw']
CORR = None
cf = os.path.join(cv2io.ROOT, 'out', hero_id, 'unity', 'correctives.npz')
if which == 'new' and os.path.exists(cf) and os.environ.get('CV2_NOCORR') != '1':
    import correctives_rt as RT
    CORR = np.load(cf)
W = np.zeros((len(body.pos), len(body.bones)))
for k in range(4): np.add.at(W, (np.arange(len(body.pos)), body.bi[:, k]), body.bw[:, k])
def grp(pred): return W[:, [i for i, n in enumerate(body.bones) if pred(n)]].sum(1)
fore = grp(lambda n: any(k in n for k in ['LowerArm', 'Hand', 'Thumb', 'Index', 'Middle', 'Ring', 'Little']) or n.startswith('ArmBlend_8_9') or n.startswith('ArmBlend_27_28') or n.startswith('ArmBlend_9_10') or n.startswith('ArmBlend_28_29')) > .5
torso = grp(lambda n: n in ('Hips', 'Spine', 'Chest', 'UpperChest')) > .6
arm_idx = np.nonzero(fore)[0][::2]
ttri = body.tri[torso[body.tri].all(1)]
res = {}
for c in hero.clips:
    if filt and filt not in c['name']: continue
    bf, bones = hero.clip(c['name'])
    worst = 0
    for fi in range(len(bf)):
        skin = hero.skin_matrices(bones[fi].astype(np.float64))
        base = gpos if CORR is None else gpos + RT.blend(hero, bones[fi].astype(np.float64), CORR)
        gp = cv2io.lbs(base, gbi, gbw, skin)
        gt = BVHTree.FromPolygons([tuple(x) for x in gp], [tuple(t) for t in gtri], all_triangles=True)
        bp = bf[fi].astype(np.float64)
        tt = BVHTree.FromPolygons([tuple(x) for x in bp], [tuple(t) for t in ttri], all_triangles=True)
        fn = G.face_normals(bp, ttri); fn /= np.linalg.norm(fn, axis=1, keepdims=True) + 1e-12
        n = 0
        for v in arm_idx:
            q = bp[v]
            r = tt.find_nearest(Vector(q), .06)
            if r[0] is None: continue
            nb = fn[r[2]]; h = np.dot(q - np.array(r[0]), nb)
            if h <= 0: continue
            hit = gt.ray_cast(Vector(q + nb * 1e-4), Vector(nb), .06)
            if hit[0] is not None: n += 1
        worst = max(worst, n)
    res[c['name']] = worst
json.dump(res, open(os.path.join(cv2io.ROOT, 'out', hero_id, 'sunk_%s.json' % which), 'w'), indent=1)
print(which.upper(), hero_id, 'arm points sunk in the shirt, sum of worst frames:', sum(res.values()), ' clips >20:', sum(1 for v in res.values() if v > 20))
for k, v in sorted(res.items(), key=lambda kv: -kv[1])[:6]: print('   ', k, v)
