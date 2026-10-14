"""GLOVE pose check over the dumped golf clips (offline LBS == Unity, see work/clothing-v2/py/cv2io.py).
For every frame of every clip of GolfKitHero_<sex>:
  * the glove (its own bones / weights / bindposes) is skinned and its outer wall (shell outer + rim) becomes the enclosing surface;
  * POKE: body skin vertices of the hand/forearm that the glove covers at rest and that are outside the posed glove wall (ray along the posed skin normal finds no wall;
    a backward ray finds the wall behind them): count per frame, depth (distance behind the wall) in mm;
  * FLOAT: glove inner-surface vertices (posed) -> distance to the posed skin surface; reported as the 98th percentile and max, cuff (loose by design) separately.
Blender -b --python glove_pose.py -- <Male|Female> [clip-substring] [--step N]  ->  out/<Sex>/golf_glove/pose_check.json"""
import sys, os, json, time
sys.path.insert(0, os.path.dirname(__file__))
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree
import acc_common as A, glove_fit as FT, glove_sdf as S, glove_mesh as M

OUT = os.path.join(A.ROOT, 'out')


class PoseCheck:
    def __init__(self, sex, side='Left'):
        self.sex = sex; self.side = side; self.pname = 'Kit_Glove_L' if side == 'Left' else 'Kit_Glove_R'
        self.h = A.cv2io.Hero('GolfKitHero_' + sex)
        d = os.path.join(OUT, sex, 'golf_glove')
        self.meta = json.load(open(os.path.join(d, 'piece.json')))
        g = np.load(os.path.join(d, self.pname + '_final.npz'))
        self.g = g
        names = self.meta['piece_bones'][self.pname]
        self.pos_mesh = A.N_to_mesh(g['pos'])                     # N -> body mesh space
        self.bi = np.array([[self.h.body.bones.index(names[i]) for i in row] for row in g['bi']], np.int32)
        self.bw = g['bw'].astype(np.float64)
        tri = g['tri']
        outer = (g['zone'] != 2) & (g['kind'] == 0)               # shell outer + rim (the enclosing wall)
        self.outer_tri = tri[outer]
        self.inner_tri = tri[(g['zone'] == 2)]
        self.inner_vid = np.unique(self.inner_tri)
        self.dcuff = g['a_dcuff'] if 'a_dcuff' in g.files else np.ones(len(g['pos']))
        # skin vertices covered at rest
        hs = FT.HeroSkin(sex, side)
        self.hs = hs
        ax = FT.unit(hs.elbow - hs.wrist)
        axial = hs.P @ ax
        cand = (axial < 0.026)
        d_ = np.load(os.path.join(A.ROOT, 'products', f'glove_hero_{sex if side == "Left" else sex + "_R"}.npz'))
        Vh, Tin = d_['Vh'], d_['T']
        nrm = S.vertex_normals(Vh, Tin); O = Vh + nrm * 0.0014
        bad, _ = FT.uncovered(hs, O, Tin, axial_max=0.030)
        rest_bad = np.zeros(len(hs.P), bool); rest_bad[bad] = True
        self.test_ids = hs.ids[cand & ~rest_bad]                  # body vertex ids to test
        self.n_test = len(self.test_ids)
        self.body_tri = self.h.body.tri
        # vertex adjacency for skin normals: only the triangles touching the test vertices' region
        self.region_tri = self.body_tri[np.isin(self.body_tri, hs.ids).all(1)]

    def frame(self, bodyf, bones):
        skin = self.h.skin_matrices(bones.astype(np.float64))
        gp = A.cv2io.lbs(self.pos_mesh, self.bi, self.bw, skin)               # glove world positions (Unity world)
        bp = bodyf.astype(np.float64)
        tree = BVHTree.FromPolygons([tuple(p) for p in gp], [tuple(t) for t in A.flip_tris(self.outer_tri)], all_triangles=True)
        # posed skin normals for the test vertices
        rt = self.region_tri
        fn = np.cross(bp[rt[:, 1]] - bp[rt[:, 0]], bp[rt[:, 2]] - bp[rt[:, 0]])
        acc = np.zeros_like(bp)
        for k in range(3): np.add.at(acc, rt[:, k], fn)
        nrm = acc / (np.linalg.norm(acc, axis=1, keepdims=True) + 1e-15)
        # a skin vertex pokes when it lies on the outward side of the nearest wall triangle (wall wound outward: flipped for the mesh-space mirror)
        poke = []; depth = []
        for v in self.test_ids:
            q = bp[v]
            r = tree.find_nearest(Vector(q), .06)
            if r[0] is None: continue
            if np.dot(q - np.array(r[0]), np.array(r[1])) > 0 and r[3] > 0.0001:
                poke.append(int(v)); depth.append(float(r[3]))
        # float: inner glove vertices -> posed skin (region triangles)
        skin_tree = BVHTree.FromPolygons([tuple(p) for p in bp], [tuple(t) for t in rt], all_triangles=True)
        iv = self.inner_vid
        gap = np.zeros(len(iv))
        for k, i in enumerate(iv):
            r = skin_tree.find_nearest(Vector(gp[i]), .05)
            gap[k] = r[3] if r[0] is not None else 0.05
        return dict(poke=poke, depth=depth, gap=gap, gp=gp)


def run(sex, filt='', step=1, verbose=True, keep_worst=True, side='Left'):
    t0 = time.time()
    pc = PoseCheck(sex, side)
    dc = pc.dcuff[pc.inner_vid]
    out = dict(sex=sex, side=side, piece=pc.pname, tested_skin_vertices=int(pc.n_test), clips={})
    worst = dict(count=-1, depth=-1.0)
    for c in pc.h.clips:
        if filt and filt not in c['name']: continue
        bodyf, bones = pc.h.clip(c['name'])
        mx_cnt = 0; mx_depth = 0.0; wf_c = -1; wf_d = -1; mx_gap_body = 0.0; p98 = 0.0; mx_gap_cuff = 0.0; n_gap3 = 0; tot_poke_frames = 0
        for fi in range(0, len(bodyf), step):
            r = pc.frame(bodyf[fi], bones[fi])
            cnt = len(r['poke']); dep = max(r['depth']) if cnt else 0.0
            if cnt: tot_poke_frames += 1
            if cnt > mx_cnt: mx_cnt, wf_c = cnt, fi
            if dep > mx_depth: mx_depth, wf_d = dep, fi
            body_gap = r['gap'][dc > 0.018]; cuff_gap = r['gap'][dc <= 0.018]
            mx_gap_body = max(mx_gap_body, float(body_gap.max())); p98 = max(p98, float(np.percentile(body_gap, 98))); n_gap3 = max(n_gap3, int((body_gap > 0.003).sum()))
            mx_gap_cuff = max(mx_gap_cuff, float(cuff_gap.max()))
            if cnt > worst['count'] or (cnt == worst['count'] and dep > worst['depth']):
                worst = dict(count=cnt, depth=dep, clip=c['name'], frame=fi, poke=r['poke'])
        out['clips'][c['name']] = dict(frames=int(len(bodyf)), max_poke_verts=int(mx_cnt), frame_of_max_count=int(wf_c), max_poke_depth_mm=round(mx_depth * 1000, 2), frame_of_max_depth=int(wf_d),
                                       frames_with_poke=int(tot_poke_frames), max_gap_body_mm=round(mx_gap_body * 1000, 2), p98_gap_body_mm=round(p98 * 1000, 2), verts_gap_over_3mm=int(n_gap3), max_gap_cuff_mm=round(mx_gap_cuff * 1000, 2))
        if verbose: print(sex, c['name'], out['clips'][c['name']], f'{time.time()-t0:.0f}s', flush=True)
    out['worst'] = dict(clip=worst.get('clip'), frame=worst.get('frame'), count=worst['count'], depth_mm=round(worst['depth'] * 1000, 2))
    json.dump(out, open(os.path.join(OUT, sex, 'golf_glove', 'pose_check.json' if (not filt and side == 'Left') else (f'pose_check_{filt}.json' if side == 'Left' else f'pose_check_R{"_" + filt if filt else ""}.json')), 'w'), indent=1)
    return out, worst


if __name__ == '__main__':
    a = A.args_after_dashes()
    sex = a[0]; side = 'Right' if 'Right' in a else 'Left'
    rest = [x for x in a[1:] if x not in ('Left', 'Right') and not x.startswith('--')]
    filt = rest[0] if rest and not rest[0].isdigit() else ''
    step = int(a[a.index('--step') + 1]) if '--step' in a else 1
    out, worst = run(sex, filt, step, side=side)
    print('WORST', out['worst'])
