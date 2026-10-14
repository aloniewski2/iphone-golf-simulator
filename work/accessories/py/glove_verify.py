"""verify the written pieces: .bin layout, weights, bone indices, winding (outward) in N and in the body mesh space. Blender -b --python glove_verify.py"""
import sys, os, json
sys.path.insert(0, os.path.dirname(__file__))
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree
import acc_common as A, glove_fit as FT
for sex in ('Male', 'Female'):
    d = os.path.join(A.ROOT, 'out', sex, 'golf_glove')
    meta = json.load(open(os.path.join(d, 'piece.json')))
    for pn, side in (('Kit_Glove_L', 'Left'), ('Kit_Glove_R', 'Right')):
        e = meta['entries'][pn]; n = e['vertexCount']; m = e['triangleCount']
        raw = open(os.path.join(d, pn + '.bin'), 'rb').read()
        exp = n * 3 * 4 * 2 + n * 2 * 4 + n * 4 * 4 + n * 4 * 4 + m * 3 * 4
        pos = np.frombuffer(raw, np.float32, n * 3).reshape(-1, 3); off = n * 12 * 2 + n * 8
        bi = np.frombuffer(raw, np.int32, n * 4, off).reshape(-1, 4); off += n * 16
        bw = np.frombuffer(raw, np.float32, n * 4, off).reshape(-1, 4); off += n * 16
        tri = np.frombuffer(raw, np.int32, m * 3, off).reshape(-1, 3)
        g = np.load(os.path.join(d, pn + '_final.npz'))
        ok_size = len(raw) == exp
        sums = bw.sum(1); nb = (bw > 0).sum(1)
        # outward test in mesh space against the skin region (mesh space positions)
        hs = FT.HeroSkin(sex, side)
        skin_pos = hs.hd.body_pos[hs.ids]                       # N
        tree = BVHTree.FromPolygons([tuple(p) for p in skin_pos], [tuple(t) for t in hs.T], all_triangles=True)
        # N frame check (piece npz is in N)
        P = g['pos']; T = g['tri']; sel = np.nonzero((g['zone'] == 0) & (g['kind'] == 0))[0]
        rng = np.random.default_rng(1); pick = rng.choice(sel, 400, replace=False)
        out_ok = 0
        for t in pick:
            a, b, c = P[T[t]]; fn = np.cross(b - a, c - a); fn /= np.linalg.norm(fn) + 1e-15; cen = (a + b + c) / 3
            r = tree.find_nearest(Vector(cen), .06)
            if r[0] is not None and np.dot(fn, cen - np.array(r[0])) > 0: out_ok += 1
        # .bin (mesh space = X flipped, winding flipped) -> convert back to N and test
        PN = A.mesh_to_N(pos); TN = A.flip_tris(tri)
        pick2 = rng.choice(len(TN), 400, replace=False); ob = 0; cnt = 0
        for t in pick2:
            a, b, c = PN[TN[t]]; fn = np.cross(b - a, c - a); fn /= np.linalg.norm(fn) + 1e-15; cen = (a + b + c) / 3
            r = tree.find_nearest(Vector(cen), .06)
            if r[0] is None: continue
            cnt += 1; ob += np.dot(fn, cen - np.array(r[0])) > 0
        print(f'{sex} {pn}: bin size ok {ok_size} ({len(raw)} vs {exp}); verts {n} tris {m}; weights sum min/max {sums.min():.4f}/{sums.max():.4f}; max bones/vertex {nb.max()}; bone idx max {bi.max()} < {len(e["bones"])}; '
              f'bones {e["bones"][:3]}..; outward share (npz, N): {out_ok / 400:.2f}; outward share (bin back to N): {ob / max(cnt, 1):.2f}; uv range {g["uv"].min():.3f}..{g["uv"].max():.3f}')
