"""BAG: clearance / pose check of the fitted bag over the dumped animation clips (offline LBS == Unity skinning).
Blender -b --factory-startup --python bag_pose.py -- <Male|Female> [--heroes GolfKitHero,MatchHero] [--clips Idle,Walk] [--images K]
Per clip: worst signed clearance (mm, negative = the bag/strap is inside the body) of the bag parts and of the strap, min distance to the head, frame of the worst.
Output: out/<Sex>/golf_bag/pose_check.json, images in proof/bag/pose_<Sex>/ (worst frames of the clips that matter)."""
import sys, os, json, time, math
sys.path.insert(0, os.path.dirname(__file__))
import numpy as np, bpy, bmesh
from mathutils import Vector
from mathutils.bvhtree import BVHTree
import acc_common as A, acc_geo as G, acc_fit as F, acc_preview as PV
import cv2io
import bag as B

OUT = os.path.join(A.ROOT, 'out')
ALLOWED = ['UpperChest', 'Chest', 'Spine', 'RightShoulder']
CRITICAL = ['Idle', 'Walk', 'RunForward', 'Wave', 'Cheer', 'FistPump', 'ReadyIdle']      # pass criterion (golf names + tennis equivalents)


def outward_tris(pos, tri):
    bm = bmesh.new()
    vs = [bm.verts.new(tuple(p)) for p in pos]
    for t in tri:
        try: bm.faces.new((vs[t[0]], vs[t[1]], vs[t[2]]))
        except ValueError: pass
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    out = np.array([[v.index for v in f.verts] for f in bm.faces], np.int32)
    bm.free()
    return out


class Rig:
    def __init__(self, sex, hid):
        self.sex = sex; self.hid = hid
        self.hero = cv2io.Hero(hid)
        self.hd = A.HeroData(sex)
        d = os.path.join(OUT, sex, 'golf_bag')
        self.meta = json.load(open(os.path.join(d, 'piece.json')))
        self.g = np.load(os.path.join(d, 'Kit_Bag_final.npz'))
        self.allowed = self.meta['fit']['bones'][1:]
        self.pos_N = self.g['pos']; self.tri = self.g['tri']; self.bi = self.g['bi']; self.bw = self.g['bw'].astype(np.float64)
        self.kind = self.g['kind']; self.zone = self.g['zone']
        self.pos_mesh = A.N_to_mesh(self.pos_N)
        hb = self.hero.body.bones
        self.bidx = [hb.index(n) for n in self.allowed]
        self.uc = hb.index('UpperChest')
        # rigid socket frame (unity world) at rest
        self.W_rest = F.rt4(np.eye(3), A.N_to_unity(np.array(self.meta['fit']['socket_origin_N'])))
        self.rest_uc = self.hero.rest_bones[self.uc]
        self.L = np.linalg.inv(self.rest_uc) @ self.W_rest
        self.bind_sock = np.linalg.inv(self.W_rest) @ self.hero.body.r2w
        # bone indices of the head region in the body mesh (rest)
        P = self.hd.body_pos
        self.head_tris_mask = (P[self.hd.body_tri][..., 2] > self.hd.rest_pos['Head'][2] - 0.06).all(1)
        self.oriented = outward_tris(self.hd.body_pos, self.hd.body_tri)
        vk = np.zeros(len(self.pos_N), bool); sv = np.unique(self.tri[self.kind == B.K['STRAP']]); vk[sv] = True
        self.strap_v = vk
        # vertices worth testing: strap, everything within 14 cm of the body at rest, plus a spread of the rest
        clear = F.Clearance(self.hd.body_pos, self.hd.body_tri)
        c0 = clear(self.pos_N)
        rng = np.random.default_rng(3)
        near = c0 < 0.14
        spread = rng.random(len(c0)) < 0.04
        self.test = np.nonzero(vk | near | spread)[0]
        self.rest_clear = c0

    def skin(self, bones_f):
        sk = np.zeros((1 + len(self.allowed), 4, 4))
        sk[0] = (bones_f[self.uc] @ self.L) @ self.bind_sock
        for k, bi in enumerate(self.bidx):
            sk[1 + k] = bones_f[bi] @ self.hero.body.bind[bi]
        return sk

    def bag_world_N(self, bones_f):
        pw = cv2io.lbs(self.pos_mesh, self.bi, self.bw, self.skin(bones_f))
        return A.unity_to_N(pw)

    def check_rest(self):
        rb = self.hero.rest_bones.astype(np.float64)
        q = self.bag_world_N(rb)
        return float(np.abs(q - self.pos_N).max())


def frame_metrics(rig, body_unity_f, bones_f):
    q = rig.bag_world_N(bones_f)
    bp = A.unity_to_N(body_unity_f)
    tree = BVHTree.FromPolygons([tuple(p) for p in bp], [tuple(t) for t in rig.oriented], all_triangles=True)
    c = np.zeros(len(rig.test))
    for j, i in enumerate(rig.test):
        loc, nrm, fi, d = tree.find_nearest(Vector(q[i]))
        v = q[i] - np.array(loc)
        c[j] = d if np.dot(nrm, v) >= 0 else -d
    return q, c, tree


def head_distance(rig, q, body_unity_f):
    bp = A.unity_to_N(body_unity_f)
    hv = np.unique(rig.hd.body_tri[rig.head_tris_mask])
    tree = BVHTree.FromPolygons([tuple(bp[i]) for i in hv], [tuple(np.searchsorted(hv, t)) for t in rig.hd.body_tri[rig.head_tris_mask]], all_triangles=True)
    top = np.nonzero(q[:, 2] > rig.hd.rest_pos['Head'][2] - 0.2)[0]
    best = 9.0
    for i in top[::3]:
        r = tree.find_nearest(Vector(q[i]))
        if r[0] is not None: best = min(best, r[3])
    return best


def run(sex, hero_ids, clip_filter=None, images=3, verbose=True):
    res = {}
    for hid in hero_ids:
        rig = Rig(sex, hid)
        if verbose: print(hid, 'rest check max |dx| (m)', rig.check_rest(), 'test verts', len(rig.test))
        for c in rig.hero.clips:
            name = c['name']
            short = name.replace('Male_', '').replace('Female_', '')
            if clip_filter and not any(short == f or short.startswith(f) for f in clip_filter): continue
            body, bones = rig.hero.clip(name)
            worst_bag = (9, -1); worst_strap = (9, -1); head_min = 9.0; mean_bag = []
            per_frame = []
            for fi in range(len(body)):
                q, cc, tree = frame_metrics(rig, body[fi].astype(np.float64), bones[fi].astype(np.float64))
                sm = rig.strap_v[rig.test]
                cb = cc[~sm].min(); cs = cc[sm].min()
                if cb < worst_bag[0]: worst_bag = (cb, fi)
                if cs < worst_strap[0]: worst_strap = (cs, fi)
                per_frame.append((float(cb), float(cs)))
                if fi % 3 == 0:
                    head_min = min(head_min, head_distance(rig, q, body[fi].astype(np.float64)))
            res[f'{hid}:{short}'] = dict(hero=hid, clip=short, frames=int(len(body)), bag_min_mm=round(worst_bag[0] * 1000, 1), bag_frame=int(worst_bag[1]),
                                         strap_min_mm=round(worst_strap[0] * 1000, 1), strap_frame=int(worst_strap[1]), head_min_mm=round(head_min * 1000, 1),
                                         critical=short in CRITICAL, per_frame=per_frame)
            if verbose: print('  %-26s bag %6.1f mm (f%2d)  strap %6.1f mm (f%2d)  head %6.1f mm' % (hid + ':' + short, worst_bag[0] * 1000, worst_bag[1], worst_strap[0] * 1000, worst_strap[1], head_min * 1000))
    return res


def main():
    a = A.args_after_dashes()
    sex = a[0]
    heroes = a[a.index('--heroes') + 1].split(',') if '--heroes' in a else ['GolfKitHero', 'MatchHero']
    clips = a[a.index('--clips') + 1].split(',') if '--clips' in a else None
    hero_ids = [f'{h}_{sex}' for h in heroes]
    res = run(sex, hero_ids, clips)
    d = os.path.join(OUT, sex, 'golf_bag')
    tag = '' if clips is None else '_' + '_'.join(clips)
    json.dump(res, open(os.path.join(d, f'pose_check{tag}.json'), 'w'), indent=1)
    if clips is None:                                    # summary into piece.json (fit_report.pose_check)
        pj = os.path.join(d, 'piece.json')
        meta = json.load(open(pj))
        summ = {}
        for k, r in res.items():
            summ[k] = {kk: r[kk] for kk in ('bag_min_mm', 'bag_frame', 'strap_min_mm', 'strap_frame', 'head_min_mm', 'critical')}
        meta['fit_report']['pose_check'] = summ
        crit = [r for r in res.values() if r['critical'] and r['hero'].startswith('GolfKitHero')]
        meta['fit_report']['pose_check_critical'] = dict(
            bag_min_mm=min(r['bag_min_mm'] for r in crit), strap_min_mm=min(r['strap_min_mm'] for r in crit), head_min_mm=min(r['head_min_mm'] for r in crit),
            clips=[r['clip'] for r in crit], note='negative = inside the body; strap penetration is under-arm / arms-overhead, see proof/bag/pose_<Sex>_worst_frames.png')
        json.dump(meta, open(pj, 'w'), indent=1)


if __name__ == '__main__':
    main()
