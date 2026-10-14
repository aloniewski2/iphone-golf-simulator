"""Skin hidden under each garment: body vertices deeply covered by the piece (>= margin along the body surface from any uncovered skin)
are never drawn while that piece is worn (Unity: MatchHeroLook.ApplyKitV2Cover). Writes out/<hero>/unity/<piece>_hide.bin (int32 vertex triplets)
and cover.npz (per-vertex hidden-by piece masks) for the poke test.  Blender -b --factory-startup --python cover.py -- <hero>"""
import sys, os, glob, heapq, json
sys.path.insert(0, os.path.dirname(__file__))
import numpy as np
import cv2io, glib as G, finalize
from mathutils import Vector
from mathutils.bvhtree import BVHTree

MARGIN = {'Kit_Top': .045, 'Kit_Bottom': .03, 'Kit_Shoe_L': .018, 'Kit_Shoe_R': .018, 'Kit_Sock_L': .02, 'Kit_Sock_R': .02,
          'Kit_Glove_L': .012, 'Kit_Glove_R': .012, 'Kit_Belt': 1.0}


def covered(hero, body, name, g):
    outer = g['tri'][g['part'] == 0]
    tree = BVHTree.FromPolygons([tuple(p) for p in g['pos']], [tuple(t) for t in outer], all_triangles=True)
    allowed = finalize.allowed_for(name, body.bones)
    Wb = np.zeros((len(body.pos), len(body.bones)))
    for k in range(4): np.add.at(Wb, (np.arange(len(body.pos)), body.bi[:, k]), body.bw[:, k])
    own = Wb[:, allowed].sum(1) > .5
    fn = G.face_normals(g['pos'], outer); fn /= np.linalg.norm(fn, axis=1, keepdims=True) + 1e-12
    cov = np.zeros(len(body.pos), bool)
    for i in np.nonzero(own)[0]:
        r = tree.find_nearest(Vector(body.pos[i]), .05)
        if r[0] is None: continue
        if np.dot(np.array(r[0]) - body.pos[i], body.nrm[i]) <= 0 or np.dot(fn[r[2]], body.nrm[i]) < .3: continue
        cov[i] = True
    return cov


def Wb_foot(body, side):
    W = np.zeros((len(body.pos), len(body.bones)))
    for k in range(4): np.add.at(W, (np.arange(len(body.pos)), body.bi[:, k]), body.bw[:, k])
    idx = [i for i, n in enumerate(body.bones) if n in (side + 'Foot', side + 'Toes')]
    return W[:, idx].sum(1)


def surface_distance(pos, tri, seeds, maxd):
    nb = G.neighbours(len(pos), tri); indptr, idx = nb
    d = np.full(len(pos), np.inf); h = []
    for s in np.nonzero(seeds)[0]: d[s] = 0; h.append((0.0, int(s)))
    heapq.heapify(h)
    while h:
        dv, v = heapq.heappop(h)
        if dv > d[v] or dv > maxd: continue
        for k in range(indptr[v], indptr[v + 1]):
            w = idx[k]; nd = dv + np.linalg.norm(pos[w] - pos[v])
            if nd < d[w]: d[w] = nd; heapq.heappush(h, (nd, w))
    return d


hero_id = sys.argv[sys.argv.index('--') + 1]
hero = cv2io.Hero(hero_id); body = hero.body
udir = os.path.join(cv2io.ROOT, 'out', hero_id, 'unity')
masks = {}
# skin that can be seen from outside in the rest pose (a ray from it escapes past every garment and the body): never hidden
allP = [body.pos]; allT = [body.tri]; off = len(body.pos)
for f in sorted(glob.glob(os.path.join(udir, 'Kit_*_final.npz'))):
    if f.endswith('Kit_Glove_R_final.npz'): continue
    gg = np.load(f); allP.append(gg['pos']); allT.append(gg['tri'] + off); off += len(gg['pos'])
vis_tree = BVHTree.FromPolygons([tuple(p) for p in np.concatenate(allP)], [tuple(t) for t in np.concatenate(allT)], all_triangles=True)
rng = np.random.default_rng(5); D = rng.normal(size=(160, 3)); D /= np.linalg.norm(D, axis=1, keepdims=True)
D = D[(D[:, 2] > -.34) & (D[:, 2] < .82)][:64]      # cameras look from 20 deg below to 55 deg above the horizon
def visible_skin(cand):
    vis = np.zeros(len(body.pos), bool)
    for i in np.nonzero(cand)[0]:
        n = body.nrm[i]; o = body.pos[i] + n * .001
        for d in D:
            if d @ n < .15: continue
            r = vis_tree.ray_cast(Vector(o), Vector(d), 2.0)
            if r[0] is None: vis[i] = True; break
    return vis
for f in sorted(glob.glob(os.path.join(udir, 'Kit_*_final.npz'))):
    name = os.path.basename(f)[:-10]
    g = np.load(f)
    cov = covered(hero, body, name, g)
    # the toes / fingers are separate islands of the body: a covered island counts as covered as a whole
    d = surface_distance(body.pos, body.tri, ~cov, MARGIN.get(name, .03) + .01)
    hid = d >= MARGIN.get(name, .03)
    if not name.startswith('Kit_Shoe'):
        vis = visible_skin(hid)
        dv = surface_distance(body.pos, body.tri, vis, .03)
        hid &= dv >= .02   # skin round anything visible stays (garments move a little in play; necklines most)
    if name.startswith('Kit_Shoe'):
        # the whole foot below the collar zone (toes are separate islands of the body: a partly-covered toe still counts)
        foot = (body.footL if name.endswith('_L') else body.footR) if False else None
        side = 'Left' if name.endswith('_L') else 'Right'
        fw = Wb_foot(body, side)
        if 'collar_prof' in g.files:
            import shoe2
            fr2 = shoe2.Frame(G.Body(hero), 'L' if name.endswith('_L') else 'R')
            q = fr2.to(body.pos); L = fr2.L; cp = g['collar_prof']
            zc = np.interp(q[:, 1], cp[:, 0] * L, cp[:, 1])
            hid |= (fw > .3) & ((q[:, 2] < zc - .022) | (q[:, 1] > .42 * L))
        else:
            import shoes, designs
            P = designs.design(hero_id)['shoes']
            GB = G.Body(hero)
            heel, fwd, lat, L = shoes.foot_frame(GB, -1 if name.endswith('_L') else 1)
            q = shoes.to_frame(body.pos, heel, fwd, lat)
            zc = np.interp(q[:, 1], np.array(P['collar_prof_y']) * L, P['collar_prof_z'])
            hid |= (fw > .3) & ((q[:, 2] < zc - .022) | (q[:, 1] > .36 * L))
    tri = body.tri[hid[body.tri].all(1)]
    tri.astype(np.int32).tofile(os.path.join(udir, name + '_hide.bin'))
    masks[name] = hid
    print(f'{name}: covered {cov.sum()} vertices, hidden {hid.sum()} vertices / {len(tri)} triangles')
# skin a garment would uncover in some pose (it slides / lifts away) is never hidden: test every 2nd frame of every clip
import correctives_rt as RT
cf = os.path.join(udir, 'correctives.npz'); CORR = np.load(cf) if os.path.exists(cf) else None
pieces = {os.path.basename(f)[:-10]: np.load(f) for f in sorted(glob.glob(os.path.join(udir, 'Kit_*_final.npz'))) if not f.endswith('Kit_Glove_R_final.npz')}
cand = np.zeros(len(body.pos), bool)
for k, v in masks.items():
    if not k.startswith('Kit_Shoe'): cand |= v        # the shoe is rigid on the foot: what it hides stays hidden (the toes are wider than the last)
cand_idx = np.nonzero(cand)[0]
exposed = np.zeros(len(body.pos), bool)
for c in hero.clips:
    bodyf, bones = hero.clip(c['name'])
    for fi in range(0, len(bodyf), 2):
        skin = hero.skin_matrices(bones[fi].astype(np.float64))
        P_, T_ = [], []; off = 0
        for name, gg in pieces.items():
            base = gg['pos'] + (RT.blend(hero, bones[fi].astype(np.float64), CORR) if (CORR is not None and name == 'Kit_Top' and len(CORR['shapes'])) else 0)
            P_.append(cv2io.lbs(base, gg['bi'], gg['bw'], skin)); T_.append(gg['tri'] + off); off += len(gg['pos'])
        tree = BVHTree.FromPolygons([tuple(p) for p in np.concatenate(P_)], [tuple(t) for t in np.concatenate(T_)], all_triangles=True)
        bp = bodyf[fi].astype(np.float64); bn = G.vertex_normals(bp, body.tri)
        for v in cand_idx[~exposed[cand_idx]]:
            r = tree.ray_cast(Vector(bp[v] + bn[v] * 1e-4), Vector(bn[v]), .10)
            if r[0] is None: exposed[v] = True
print('skin a garment uncovers in some pose:', int(exposed.sum()), 'of', int(cand.sum()), 'hidden candidates')
keep = surface_distance(body.pos, body.tri, exposed, .025) < .015
for name in list(masks):
    if name.startswith('Kit_Shoe'): continue
    before = int(masks[name].sum()); masks[name] = masks[name] & ~keep
    tri = body.tri[masks[name][body.tri].all(1)]
    tri.astype(np.int32).tofile(os.path.join(udir, name + '_hide.bin'))
    print(f'{name}: hidden {before} -> {int(masks[name].sum())} vertices after the pose test')
np.savez(os.path.join(udir, 'cover.npz'), **masks)

# ---- contact shadow on the skin: how much of each visible skin vertex's sky the garment blocks within a few cm
import math
rng = np.random.default_rng(3); R = 24
u1 = rng.random(R); u2 = rng.random(R)
dirs = np.stack([np.sqrt(u1) * np.cos(2 * math.pi * u2), np.sqrt(u1) * np.sin(2 * math.pi * u2), np.sqrt(1 - u1)], 1)
hidden_all = np.zeros(len(body.pos), bool)
for k, v in masks.items():
    if k != 'Kit_Glove_R': hidden_all |= v
for f in sorted(glob.glob(os.path.join(udir, 'Kit_*_final.npz'))):
    name = os.path.basename(f)[:-10]
    g = np.load(f)
    tree = BVHTree.FromPolygons([tuple(p) for p in g['pos']], [tuple(t) for t in g['tri']], all_triangles=True)
    near = np.zeros(len(body.pos), bool)
    lo = g['pos'].min(0) - .05; hi = g['pos'].max(0) + .05
    cand = np.nonzero(((body.pos > lo) & (body.pos < hi)).all(1) & ~masks[name])[0]
    val = np.zeros(len(body.pos))
    for i in cand:
        r0 = tree.find_nearest(Vector(body.pos[i]), .05)
        if r0[0] is None: continue
        n = body.nrm[i]
        a = np.cross(n, [0, 0, 1.] if abs(n[2]) < .9 else [1, 0, 0.]); a /= np.linalg.norm(a) + 1e-12; b = np.cross(n, a)
        o = body.pos[i] + n * .0005
        occ = 0.0
        for d in dirs:
            w = a * d[0] + b * d[1] + n * d[2]
            r = tree.ray_cast(Vector(o), Vector(w), .045)
            if r[0] is not None: occ += 1 - (r[3] / .045) ** .6
        val[i] = occ / R
    # soften along the skin (two umbrella passes)
    nb = G.neighbours(len(body.pos), body.tri)
    for _ in range(2): val = np.maximum(0, val + .5 * G.umbrella(val[:, None], nb)[:, 0])
    idx = np.nonzero(val > .02)[0].astype(np.int32)
    with open(os.path.join(udir, name + '_contact.bin'), 'wb') as fh:
        fh.write(idx.tobytes()); fh.write(np.clip(val[idx], 0, 1).astype(np.float32).tobytes())
    print(f'{name}: contact on {len(idx)} skin vertices, max {val.max():.2f}')
