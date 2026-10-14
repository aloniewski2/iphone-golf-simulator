"""BAG: search the carry placement so the bag HANGS (top against the upper back, bottom swung out) instead of floating.
Blender -b --factory-startup --python bag_hang.py -- <Male|Female> [leans] [rolls] [rim]
Constraints over the Idle/Walk/RunForward/Wave/Cheer/FistPump frames: every rigid bag vertex >= 10 mm off the body, >= 25 mm off the head.
Objective: the closest bag-to-body gap at the shoulder-blade level (world z 1.20-1.34, rest pose) as near 20 mm as the constraints allow.
Writes out/<Sex>/golf_bag/hang_search.json"""
import sys, os, json, math, time
sys.path.insert(0, os.path.dirname(__file__))
import numpy as np, bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree
import acc_common as A, acc_geo as G, acc_fit as F
import cv2io
import bag as B, bag_fit as BF, bag_pose as BP, bag_place as BPL


class Motion2(BPL.Motion):
    def __init__(self, sex, step=2):
        super().__init__(sex, step)
        hd = self.hd
        hero = cv2io.Hero(f'GolfKitHero_{sex}')
        self.head_tris = hd.body_tri[(hd.body_pos[hd.body_tri][..., 2] > hd.rest_pos['Head'][2] - 0.06).all(1)]
        hv = np.unique(self.head_tris); self.hv = hv
        ht = np.searchsorted(hv, self.head_tris)
        self.head_trees = []
        for name, fi, tree, M in self.frames:
            body, bones = hero.clip(name)
            bp = A.unity_to_N(body[fi].astype(np.float64))
            self.head_trees.append(BVHTree.FromPolygons([tuple(bp[i]) for i in hv], [tuple(t) for t in ht], all_triangles=True))

    def check(self, Pn):
        """(min body clearance m, clip, frame, min head distance m)"""
        pu = A.N_to_unity(Pn)
        p4 = np.concatenate([pu, np.ones((len(pu), 1))], 1)
        top = Pn[:, 2] > 1.22
        worst = (9.0, '', -1); hmin = 9.0
        for (name, fi, tree, M), ht in zip(self.frames, self.head_trees):
            q = A.unity_to_N((p4 @ M.T)[:, :3])
            m = 9.0
            for p in q:
                loc, nrm, idx, d = tree.find_nearest(Vector(p))
                s = d if np.dot(nrm, p - np.array(loc)) >= 0 else -d
                if s < m: m = s
            if m < worst[0]: worst = (m, name, fi)
            for p in q[top]:
                r = ht.find_nearest(Vector(p))
                if r[0] is not None and r[3] < hmin: hmin = r[3]
        return worst[0], worst[1], worst[2], hmin


def band_gap(clear, P_rest_world, normals_world, zlo=1.20, zhi=1.34):
    """closest distance (rest pose) of the bag's hero-facing surface in the shoulder-blade band"""
    m = (P_rest_world[:, 2] > zlo) & (P_rest_world[:, 2] < zhi) & (normals_world[:, 1] < -0.3)
    if not m.any(): return None
    return float(clear(P_rest_world[m]).min())


def bag_sample(S, n=700):
    parts, _, _ = B.build_product('folded', straps=(), clubs=True, S=S, drop_hidden=True)
    P = np.concatenate([G.positions(p) for p in parts])
    rng = np.random.default_rng(5)
    keep = rng.choice(len(P), min(n, len(P)), replace=False)
    low = np.nonzero(P[:, 2] < 0.14)[0]
    keep = np.unique(np.concatenate([keep, rng.choice(low, min(200, len(low)), replace=False)]))
    return P[keep]


def find_ty(mot, sub, lean, roll, tz, min_clear=0.010, min_head=0.025, ty0=0.05, ty1=0.30, step=0.016):
    pl = lambda ty: BF.Placement(lean, (BF.CFG['tx'], ty, tz), roll)
    lo = None; hi = None
    for ty in np.arange(ty0, ty1, step):
        c = mot.check(pl(ty).to_world(sub))
        if c[0] >= min_clear and c[3] >= min_head: hi = float(ty); break
        lo = float(ty)
    if hi is None: return None
    if lo is None: lo = hi - step
    for _ in range(4):
        ty = 0.5 * (lo + hi)
        c = mot.check(pl(ty).to_world(sub))
        if c[0] >= min_clear and c[3] >= min_head: hi = ty
        else: lo = ty
    return pl(hi), mot.check(pl(hi).to_world(sub))


def solve_hang(sex, lean, roll, rim=None, band_target=0.020, mot=None, verbose=True):
    """returns (Placement, report). ty = the larger of: (a) the smallest offset that keeps >= 10 mm to the body and >= 25 mm to the head in every critical frame,
    (b) the offset that puts the closest approach at the shoulder-blade level (world z 1.20-1.34, rest) at band_target."""
    S = B.Shell(); hd = A.HeroData(sex)
    rim = rim or BF.CFG['rim_top'][sex]
    clear = F.Clearance(hd.body_pos, hd.body_tri)
    mot = mot or Motion2(sex, step=1)                   # every frame: a raised arm passes the bag top in a few frames only
    parts, _, _ = B.build_product('folded', straps=(), clubs=True, S=S, drop_hidden=True)
    import bag_export as BE
    piece, joined = BE.make_piece(parts)
    Pp, Np, kind = piece['pos'], piece['nrm'], piece['kind']
    rng = np.random.default_rng(5)
    facing = np.nonzero(Np[:, 1] > 0.5)[0]
    wz = pl_z = None
    top_idx = np.nonzero(Pp[:, 2] > 0.74)[0]                      # product frame: everything from the rim up (rim, handle, covers, irons, grips, strap start): ALL of it, a single iron tip matters
    mid_idx = np.nonzero((Pp[:, 2] > 0.55) & (Pp[:, 2] <= 0.74))[0]
    sub = Pp[np.unique(np.concatenate([rng.choice(len(Pp), 900, replace=False), rng.choice(facing, min(1500, len(facing)), replace=False), top_idx, rng.choice(mid_idx, min(800, len(mid_idx)), replace=False)]))]
    top_prod = S.ZT + 0.003 + 0.0148
    tz = rim - (top_prod - BF.P0[2]) * math.cos(math.radians(abs(lean))) * math.cos(math.radians(roll))
    r = find_ty(mot, sub, lean, roll, tz)
    ty_m = float(r[0].t[1]); cm = r[1]
    lo, hi = 0.04, 0.34
    for _ in range(12):
        ty = 0.5 * (lo + hi)
        pl = BF.Placement(lean, (BF.CFG['tx'], ty, tz), roll)
        g = band_gap(clear, pl.to_world(Pp), pl.dir_world(Np))
        if g is not None and g >= band_target: hi = ty
        else: lo = ty
    ty_b = hi
    ty = max(ty_m, ty_b)
    pl = BF.Placement(lean, (BF.CFG['tx'], ty, tz), roll)
    c = mot.check(pl.to_world(sub)); g = band_gap(clear, pl.to_world(Pp), pl.dir_world(Np))
    rep = dict(lean=lean, roll=roll, ty=round(ty, 4), tz=round(tz, 4), ty_motion=round(ty_m, 4), ty_band=round(ty_b, 4), motion_min_mm=round(c[0] * 1000, 1), motion_worst=f'{c[1]}:{c[2]}',
               head_min_mm=round(c[3] * 1000, 1), band_gap_mm=None if g is None else round(g * 1000, 1))
    if verbose: print(sex, rep, flush=True)
    return pl, rep


def main():
    a = A.args_after_dashes()
    sex = a[0]
    leans = [float(v) for v in a[1].split(',')] if len(a) > 1 else [-12, -9, -6, -3, 0]
    rolls = [float(v) for v in a[2].split(',')] if len(a) > 2 else [0, 3]
    rim = float(a[3]) if len(a) > 3 else BF.CFG['rim_top'][sex]
    S = B.Shell()
    hd = A.HeroData(sex)
    clear = F.Clearance(hd.body_pos, hd.body_tri)
    mot = Motion2(sex, step=3)
    sub = bag_sample(S)
    # a dense, hero-facing sample for the band gap (shell + legs + bracket), with normals
    parts, _, _ = B.build_product('folded', straps=(), clubs=False, S=S, drop_hidden=True)
    import bag_export as BE
    piece, joined = BE.make_piece(parts)
    Pp, Np = piece['pos'], piece['nrm']
    top_prod = S.ZT + 0.003 + 0.0148
    out = []
    for lean in leans:
        for roll in rolls:
            tz = rim - (top_prod - BF.P0[2]) * math.cos(math.radians(abs(lean)))
            r = find_ty(mot, sub, lean, roll, tz)
            if r is None:
                print({'lean': lean, 'roll': roll, 'feasible': False}, flush=True); continue
            pl, (cb, clip, fr, ch) = r
            W = pl.to_world(Pp); Nw = pl.dir_world(Np)
            g = band_gap(clear, W, Nw)
            allback = Nw[:, 1] < -0.7
            c_all = clear(W[allback])
            topy = pl.to_world(np.array([[0, S.YB, top_prod]]))[0]; boty = pl.to_world(np.array([[0, S.YB, 0.0]]))[0]
            rec = dict(lean=lean, roll=roll, rim=rim, ty=round(float(pl.t[1]), 4), tz=round(float(pl.t[2]), 4), motion_min_mm=round(cb * 1000, 1), worst=f'{clip}:{fr}',
                       head_min_mm=round(ch * 1000, 1), band_gap_mm=None if g is None else round(g * 1000, 1), back_gap_mm=[round(float(c_all.min()) * 1000, 1), round(float(np.median(c_all)) * 1000, 1), round(float(c_all.max()) * 1000, 1)],
                       top_y=round(float(topy[1]), 3), bottom_y=round(float(boty[1]), 3))
            out.append(rec); print(rec, flush=True)
    json.dump(out, open(os.path.join(A.ROOT, 'out', sex, 'golf_bag', 'hang_search.json'), 'w'), indent=1)


if __name__ == '__main__':
    main()
