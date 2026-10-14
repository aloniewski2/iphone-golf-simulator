"""BAG: fast strap-only iteration. Builds the fitted strap for a hero (placement from placement.json), weights it and measures its clearance over the critical clips.
Blender -b --factory-startup --python bag_strap_eval.py -- <Male|Female> ['{"json overrides of bag_fit.CFG"}']"""
import sys, os, json, math
sys.path.insert(0, os.path.dirname(__file__))
import numpy as np, bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree
import acc_common as A, acc_geo as G, acc_fit as F
import cv2io
import bag as B, bag_geo as BG, bag_fit as BF, bag_pose as BP

CRIT = ['Idle', 'Walk', 'RunForward', 'Wave', 'Cheer', 'FistPump']


class StrapEval:
    def __init__(self, sex, step=1):
        self.sex = sex
        self.hero = cv2io.Hero(f'GolfKitHero_{sex}')
        self.hd = A.HeroData(sex)
        self.oriented = BP.outward_tris(self.hd.body_pos, self.hd.body_tri)
        hb = self.hero.body.bones
        self.uc = hb.index('UpperChest')
        self.bidx = [hb.index(n) for n in BF.ALLOWED]
        self.sock_bind = np.linalg.inv(self.hero.rest_bones[self.uc]) @ self.hero.body.r2w
        self.frames = []
        for c in self.hero.clips:
            if c['name'] not in CRIT: continue
            body, bones = self.hero.clip(c['name'])
            for fi in range(0, len(body), step):
                bp = A.unity_to_N(body[fi].astype(np.float64))
                tree = BVHTree.FromPolygons([tuple(p) for p in bp], [tuple(t) for t in self.oriented], all_triangles=True)
                bf = bones[fi].astype(np.float64)
                sk = np.zeros((1 + len(self.bidx), 4, 4)); sk[0] = bf[self.uc] @ self.sock_bind
                for k, bi in enumerate(self.bidx): sk[1 + k] = bf[bi] @ self.hero.body.bind[bi]
                self.frames.append((c['name'], fi, tree, sk, bp))

    def evaluate(self, pos_N, bi, bw, sa=None):
        pm = A.N_to_mesh(pos_N)
        res = {}
        for name, fi, tree, sk, bp in self.frames:
            q = A.unity_to_N(cv2io.lbs(pm, bi, bw.astype(np.float64), sk))
            worst = 9.0; wj = -1
            for j, p in enumerate(q):
                loc, nrm, idx, d = tree.find_nearest(Vector(p))
                s = d if np.dot(nrm, p - np.array(loc)) >= 0 else -d
                if s < worst: worst, wj = s, j
            r = res.setdefault(name, dict(worst=9.0, frame=-1, vert=-1, bone=''))
            if worst < r['worst']:
                d2 = np.linalg.norm(bp - q[wj], axis=1); v = int(np.argmin(d2)); k = int(np.argmax(self.hero.body.bw[v]))
                r.update(worst=worst, frame=fi, vert=wj, bone=self.hero.body.bones[self.hero.body.bi[v][k]])
        return res


def nnls(Aa, b, iters=60):
    """Lawson-Hanson non-negative least squares (small problems)"""
    n = Aa.shape[1]; x = np.zeros(n); Pset = np.zeros(n, bool)
    w = Aa.T @ (b - Aa @ x)
    for _ in range(iters):
        if Pset.all() or (w[~Pset] <= 1e-12).all(): break
        j = np.argmax(np.where(~Pset, w, -1e9)); Pset[j] = True
        while True:
            z = np.zeros(n)
            z[Pset] = np.linalg.lstsq(Aa[:, Pset], b, rcond=None)[0]
            if (z[Pset] > 1e-12).all(): x = z; break
            m = Pset & (z <= 1e-12)
            alpha = np.min(x[m] / np.maximum(x[m] - z[m], 1e-12))
            x = x + alpha * (z - x)
            Pset &= x > 1e-12
            if not Pset.any(): break
        w = Aa.T @ (b - Aa @ x)
    return x


def _fit_rows(self, hd, tree, pos, sa, bones_idx, col=None):
    """per (cross-section row, across column): bone weights (over self.bidx order) that best reproduce the real skin motion of the nearest body vertex over all critical
    frames. Then smoothed along the strap per column. Returns (weights per vertex)."""
    key = np.round(sa / 2e-4).astype(np.int64)
    col = np.zeros(len(pos), np.int64) if col is None else col
    gk = key * 16 + (col + 8)
    uk, inv = np.unique(gk, return_inverse=True)
    nb = len(self.bidx)
    gw = np.zeros((len(uk), nb)); gsa = np.zeros(len(uk)); gcol = np.zeros(len(uk), np.int64)
    for r in range(len(uk)):
        m = inv == r
        c = pos[m].mean(0); gsa[r] = sa[m].mean(); gcol[r] = col[m][0]
        loc, nrm, fi, d = tree.find_nearest(Vector(c))
        tri = hd.body_tri[fi]; v = tri[np.argmin(np.linalg.norm(hd.body_pos[tri] - np.array(loc), axis=1))]
        s4 = np.append(A.N_to_mesh(hd.body_pos[v][None])[0], 1.0)
        cols = []; tgt = []
        for name, fi_, tr, sk, bp in self.frames:
            tgt.append(bp[v])
            cols.append(np.stack([A.unity_to_N(((sk[1 + k] @ s4)[:3])[None])[0] for k in range(nb)], 1))
        fw = np.repeat([np.sqrt(BF.CFG['fist_w'] if f[0] == 'FistPump' else 1.0) for f in self.frames], 3)
        Aa = np.concatenate(cols, 0) * fw[:, None]; b = np.concatenate(tgt, 0) * fw
        lam = 200.0
        w = nnls(np.vstack([Aa, lam * np.ones((1, nb))]), np.append(b, lam)); gw[r] = w / max(w.sum(), 1e-9)
    # smooth along the strap, per column
    sig = BF.CFG['w_sigma']
    sm = np.zeros_like(gw)
    for c in np.unique(gcol):
        ix = np.nonzero(gcol == c)[0]
        for i in ix:
            w = np.exp(-((gsa[ix] - gsa[i]) / sig) ** 2); sm[i] = (w[:, None] * gw[ix]).sum(0) / w.sum()
    sm = sm / np.maximum(sm.sum(1, keepdims=True), 1e-9)
    return sm[inv]


StrapEval.fit_rows = _fit_rows


def build_strap(sex, ev=None, placement=None, verbose=True):
    hd = ev.hd if ev else A.HeroData(sex)
    clear = F.Clearance(hd.body_pos, hd.body_tri); tree = BF.body_tree(hd)
    A.reset()
    S = B.Shell()
    pl, _ = BF.get_placement(sex) if placement is None else (placement, None)
    ctrl_w, hint_w = BF.fit_strap_ctrl(hd, sex, pl, S, clear, tree)
    low_web = BF.web_length(ctrl_w)
    ctrl_w = BF.conform_strap(None, ctrl_w, hint_w, pl, clear, S, low_web=low_web, web_clear=BF.wc(sex))
    ctrl_p, hint_p = pl.to_prod(ctrl_w), pl.dir_prod(hint_w)
    ob, info = B.build_strap('Strap_eval', ctrl_p, hint_p, low_web=low_web)
    pos = pl.to_world(G.positions(ob))
    sa = G.point_attr(ob, 'sa')
    tot = info['tot']
    tail0 = ctrl_p[len(ctrl_p) - (len(B.product_strap_ctrl(S, 1)[0]) - BF.CFG['tail_idx'][sex])]
    s_join = float(info['sa'][np.argmin(np.linalg.norm(info['C'] - tail0, axis=1))])
    W = BF.strap_weights_arrays(hd, tree, pos, sa, tot, s_join, ev=ev, sx=G.point_attr(ob, 'sx'), hwid=G.point_attr(ob, 'hwid'))
    bi, bw = BF.top4(W)
    c_rest = clear(pos)
    return dict(pos=pos, sa=sa, tot=tot, s_join=s_join, bi=bi, bw=bw, rest_min=float(c_rest.min()), hd=hd, clear=clear, tree=tree, S=S, pl=pl, ctrl_w=ctrl_w, ob=ob)


def report(sex, ev, st):
    res = ev.evaluate(st['pos'], st['bi'], st['bw'])
    names = ['Back_Bag'] + BF.ALLOWED
    print(sex, 'strap rest min %.1f mm, length %.3f, join %.3f' % (st['rest_min'] * 1000, st['tot'], st['s_join']))
    for c in CRIT:
        r = res[c]
        sa = st['sa'][r['vert']]
        w = {names[st['bi'][r['vert']][k]]: round(float(st['bw'][r['vert']][k]), 2) for k in range(4) if st['bw'][r['vert']][k] > 0.01}
        print('   %-10s worst %6.1f mm  frame %2d  at sa %.2f  vs %-14s w=%s' % (c, r['worst'] * 1000, r['frame'], sa, r['bone'], w))
    return res


if __name__ == '__main__':
    a = A.args_after_dashes()
    sex = a[0]
    if len(a) > 1:
        ov = json.loads(a[1])
        for k, v in ov.items():
            if isinstance(v, dict) and isinstance(BF.CFG.get(k), dict): BF.CFG[k].update(v)
            else: BF.CFG[k] = v
    ev = StrapEval(sex)
    st = build_strap(sex, ev)
    report(sex, ev, st)
