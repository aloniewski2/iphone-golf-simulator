"""BAG: choose the carry placement (lean, rim height, offset behind the back) so the rigid bag parts clear the body in MOTION, not only at the rest pose.
Blender -b --factory-startup --python bag_place.py -- <Male|Female> [lean list e.g. 8,10] [rim list e.g. 1.40,1.44,1.48]
Writes out/<Sex>/golf_bag/placement_opt.json  (one entry per candidate: ty = how far behind the back the anchor must sit for >= min clearance over the critical clips)."""
import sys, os, json, time, math
sys.path.insert(0, os.path.dirname(__file__))
import numpy as np, bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree
import acc_common as A, acc_geo as G, acc_fit as F
import cv2io
import bag as B, bag_fit as BF, bag_pose as BP

CRIT_CLIPS = ['Idle', 'Walk', 'RunForward', 'Wave', 'Cheer', 'FistPump']


class Motion:
    """per critical frame: BVH of the posed body (N frame) and the rigid motion of the socket (unity world 4x4, relative to rest)."""
    def __init__(self, sex, step=2):
        self.hd = A.HeroData(sex)
        hero = cv2io.Hero(f'GolfKitHero_{sex}')
        oriented = BP.outward_tris(self.hd.body_pos, self.hd.body_tri)
        uc = hero.body.bones.index('UpperChest')
        rest_uc = hero.rest_bones[uc]
        self.frames = []
        for c in hero.clips:
            if c['name'] not in CRIT_CLIPS: continue
            body, bones = hero.clip(c['name'])
            for fi in range(0, len(body), step):
                tree = BVHTree.FromPolygons([tuple(p) for p in A.unity_to_N(body[fi].astype(np.float64))], [tuple(t) for t in oriented], all_triangles=True)
                M = bones[fi][uc].astype(np.float64) @ np.linalg.inv(rest_uc)
                self.frames.append((c['name'], fi, tree, M))

    def clearance(self, Pn):
        """min signed clearance of points Pn (N, rest world) over all frames; returns (min, clip, frame)."""
        pu = A.N_to_unity(Pn)
        p4 = np.concatenate([pu, np.ones((len(pu), 1))], 1)
        worst = (9.0, '', -1)
        for name, fi, tree, M in self.frames:
            q = A.unity_to_N((p4 @ M.T)[:, :3])
            m = 9.0
            for p in q:
                loc, nrm, idx, d = tree.find_nearest(Vector(p))
                s = d if np.dot(nrm, p - np.array(loc)) >= 0 else -d
                if s < m: m = s
            if m < worst[0]: worst = (m, name, fi)
        return worst


def sample_bag(S, n=900):
    parts, _, _ = B.build_product('folded', straps=(), clubs=False, S=S, drop_hidden=True)
    P = np.concatenate([G.positions(p) for p in parts])
    rng = np.random.default_rng(5)
    # the back-most / lowest vertices decide; keep a spread plus every vertex within the lowest/back-most slices
    keep = rng.choice(len(P), min(n, len(P)), replace=False)
    # extra: lower 12 cm and back corners
    low = np.nonzero((P[:, 2] < 0.14))[0]
    keep = np.unique(np.concatenate([keep, rng.choice(low, min(300, len(low)), replace=False)]))
    return P[keep]


def place_candidates(sex, leans, rims, min_clear=0.010, verbose=True):
    S = B.Shell()
    mot = Motion(sex)
    sub = sample_bag(S)
    top_prod = S.ZT + 0.003 + 0.0148
    out = []
    for lean in leans:
        for rim in rims:
            pl, rep = solve_motion(sex, S, lean, rim, min_clear=min_clear, mot=mot, sub=sub)
            top = pl.to_world(np.array([[0, S.YB, top_prod]]))[0]; bot = pl.to_world(np.array([[0, S.YB, 0.0]]))[0]
            rec = dict(lean=lean, rim=rim, ty=round(float(pl.t[1]), 4), tz=round(float(pl.t[2]), 4), worst_mm=rep['worst_mm'], worst_clip=rep['worst_clip'], worst_frame=rep['worst_frame'],
                       top_y=round(float(top[1]), 3), bottom_y=round(float(bot[1]), 3), bottom_z=round(float(bot[2]), 3))
            out.append(rec)
            if verbose: print(rec, flush=True)
    return out


def solve_motion(sex, S, lean, rim, min_clear=0.010, steps=10, mot=None, sub=None):
    """smallest offset behind the back for which the rigid bag parts keep >= min_clear in every critical frame; returns (Placement, report)."""
    mot = mot or Motion(sex)
    sub = sub if sub is not None else sample_bag(S)
    top_prod = S.ZT + 0.003 + 0.0148
    tz = rim - (top_prod - BF.P0[2]) * math.cos(math.radians(lean))
    # clearance is not monotone in the offset (thigh vs glute): scan outward for the first feasible offset, then refine
    lo = None; hi = None
    for ty in np.arange(0.08, 0.34, 0.012):
        w = mot.clearance(BF.Placement(lean, (BF.CFG['tx'], ty, tz)).to_world(sub))
        if w[0] >= min_clear: hi = float(ty); break
        lo = float(ty)
    if hi is None: hi = 0.34
    if lo is None: lo = hi - 0.012
    for _ in range(steps - 6 if steps > 6 else 4):
        ty = 0.5 * (lo + hi)
        w = mot.clearance(BF.Placement(lean, (BF.CFG['tx'], ty, tz)).to_world(sub))
        if w[0] >= min_clear: hi = ty
        else: lo = ty
    pl = BF.Placement(lean, (BF.CFG['tx'], hi, tz))
    w = mot.clearance(pl.to_world(sub))
    return pl, dict(worst_mm=round(w[0] * 1000, 1), worst_clip=w[1], worst_frame=int(w[2]), frames=len(mot.frames))


def main():
    a = A.args_after_dashes()
    sex = a[0]
    leans = [float(v) for v in a[1].split(',')] if len(a) > 1 else [8, 10]
    rims = [float(v) for v in a[2].split(',')] if len(a) > 2 else [1.40, 1.44, 1.48, 1.52]
    res = place_candidates(sex, leans, rims)
    json.dump(res, open(os.path.join(A.ROOT, 'out', sex, 'golf_bag', 'placement_opt.json'), 'w'), indent=1)


if __name__ == '__main__':
    main()
