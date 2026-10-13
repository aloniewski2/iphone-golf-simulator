"""Skin poke-through over every clip: body vertices that end up OUTSIDE a garment they are covered by (offline LBS == Unity skinning).
Blender -b --factory-startup --python poke.py -- <hero> [clip-substring]"""
import sys, os, json, glob
sys.path.insert(0, os.path.dirname(__file__))
import numpy as np
import cv2io, glib as G
from mathutils import Vector
from mathutils.bvhtree import BVHTree

argv = sys.argv[sys.argv.index('--') + 1:]
hero_id = argv[0]; filt = argv[1] if len(argv) > 1 else ''
hero = cv2io.Hero(hero_id); body = hero.body
udir = os.path.join(cv2io.ROOT, 'out', hero_id, 'unity')
bpos = body.pos; bn = body.nrm
report = {}
pieces = sorted(glob.glob(os.path.join(udir, 'Kit_*_final.npz')))
data = {}
cv = np.load(os.path.join(udir, 'cover.npz')) if os.path.exists(os.path.join(udir, 'cover.npz')) else {}
hidden = np.zeros(len(bpos), bool)
for k in cv.files if hasattr(cv, 'files') else []:
    if k != 'Kit_Glove_R': hidden |= cv[k]
for f in pieces:
    name = os.path.basename(f)[:-10]
    g = np.load(f)
    outer = g['tri'][g['part'] == 0]
    tree = BVHTree.FromPolygons([tuple(p) for p in g['pos']], [tuple(t) for t in outer], all_triangles=True)
    # covered body vertices: the nearest point of this garment's outer wall is within 4.5 cm, outside the skin along the skin normal,
    # facing the same way, and the vertex belongs to the bones this garment follows (no arm vertex 'under' the torso, no foot under the other shoe)
    import finalize
    allowed = finalize.allowed_for(name, body.bones)
    Wb = np.zeros((len(bpos), len(body.bones)))
    for k in range(4): np.add.at(Wb, (np.arange(len(bpos)), body.bi[:, k]), body.bw[:, k])
    own = Wb[:, allowed].sum(1) > .6
    fn_all = G.face_normals(g['pos'], outer); fn_all /= np.linalg.norm(fn_all, axis=1, keepdims=True) + 1e-12
    cov = []; hit_tri = []; hit_bc = []; d0 = []
    for i in np.nonzero(own & ~hidden)[0]:
        r = tree.find_nearest(Vector(bpos[i]), .045)
        if r[0] is None: continue
        if np.dot(np.array(r[0]) - bpos[i], bn[i]) <= 0 or np.dot(fn_all[r[2]], bn[i]) < .6: continue
        t = outer[r[2]]; a, b, c = g['pos'][t]
        q = np.array(r[0]); v0, v1, v2 = b - a, c - a, q - a
        d00, d01, d11, d20, d21 = v0 @ v0, v0 @ v1, v1 @ v1, v2 @ v0, v2 @ v1
        den = d00 * d11 - d01 * d01 or 1e-20
        vv = (d11 * d20 - d01 * d21) / den; ww = (d00 * d21 - d01 * d20) / den
        cov.append(i); hit_tri.append(t); hit_bc.append((1 - vv - ww, vv, ww)); d0.append(r[3])
    # keep only body vertices well inside the garment: the hit point is >= 15 mm from any border of the outer wall
    loops = G.boundary_loops(outer)
    if cov:
        hp = np.array([np.array(hit_bc[k]) @ g['pos'][hit_tri[k]] for k in range(len(cov))])
        de = G.edge_distance(g['pos'], outer, loops) if loops else np.full(len(g['pos']), 1.0)
        dh = np.array([np.array(hit_bc[k]) @ de[hit_tri[k]] for k in range(len(cov))])
        keep = dh > .015
    else: keep = np.array([], bool)
    data[name] = dict(g=g, cov=np.array(cov)[keep], tri=np.array(hit_tri).reshape(-1, 3)[keep], bc=np.array(hit_bc).reshape(-1, 3)[keep], d0=np.array(d0)[keep])
    print(f'{name}: covered body vertices {keep.sum()}')

CORR = None
cf = os.path.join(udir, 'correctives.npz')
if os.path.exists(cf) and os.environ.get('CV2_NOCORR') != '1':
    import correctives_rt as RT
    CORR = np.load(cf)
for c in hero.clips:
    if filt and filt not in c['name']: continue
    bodyf, bones = hero.clip(c['name'])
    for name, D in data.items():
        g = D['g']
        if len(D['cov']) == 0: continue
        worst = 0.0; count = 0; wf = -1; wv = -1
        outer = g['tri'][g['part'] == 0]
        bt = body.tri
        zcut = np.percentile(g['pos'][:, 2], 1) + .03 if name == 'Kit_Top' else -9
        for fi in range(len(bodyf)):
            skin = hero.skin_matrices(bones[fi].astype(np.float64))
            base = g['pos'] + (RT.blend(hero, bones[fi].astype(np.float64), CORR) if (CORR is not None and name == 'Kit_Top' and len(CORR['shapes'])) else 0)
            gp = cv2io.lbs(base, g['bi'], g['bw'], skin)
            tree = BVHTree.FromPolygons([tuple(p) for p in gp], [tuple(t) for t in outer], all_triangles=True)
            bp = bodyf[fi].astype(np.float64)
            bnrm = G.vertex_normals(bp, bt)
            cnt = 0; m = 0.0; mv = -1
            for v in D['cov']:
                if body.pos[v, 2] < zcut: continue
                q = bp[v]; n = bnrm[v]
                fwd = tree.ray_cast(Vector(q + n * 1e-4), Vector(n), .08)
                if fwd[0] is not None: continue                      # still under the cloth
                back = tree.ray_cast(Vector(q - n * 1e-4), Vector(-n), .03)
                if back[0] is None: continue                         # the cloth moved away (a gap), not a poke
                cnt += 1
                if back[3] > m: m, mv = back[3], int(v)
            count = max(count, cnt)
            if m > worst: worst, wf, wv = m, fi, mv
        report.setdefault(name, {})[c['name']] = dict(max_mm=round(worst * 1000, 1), verts=count, frame=wf, vertex=wv)
out = os.path.join(cv2io.ROOT, 'out', hero_id, 'poke.json')
json.dump(report, open(out, 'w'), indent=1)
for name, r in report.items():
    bad = {k: v for k, v in r.items() if v['max_mm'] > .5}
    print(name, 'clips with poke-through > 0.5 mm:', len(bad), '/', len(r), ' worst', max([v['max_mm'] for v in r.values()] + [0]), 'mm, most points', max([v['verts'] for v in r.values()] + [0]))
    for k, v in sorted(bad.items(), key=lambda kv: -kv[1]['max_mm'])[:6]: print('   ', k, v)
