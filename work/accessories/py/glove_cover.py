"""Skin under the glove is not drawn: body triangles that lie on the cavity side of the glove's outer wall (all three vertices, within 10 mm of it) are listed per glove,
the same idea as the clothing V2 skin cover (the body asset is not touched; HeroAccessories drops these triangles at runtime, so skin can never show through the
wall in a swing). Output: out/<Sex>/golf_glove/<Tag>_hide.bin = int32 body vertex triplets (the Unity body mesh's own indices).
Blender -b --factory-startup --python py/glove_cover.py -- [Male|Female|both]"""
import sys, os, json
sys.path.insert(0, os.path.dirname(__file__))
import numpy as np, bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree
import acc_common as A

OUT = os.path.join(A.ROOT, 'out')
SIDE = {'Kit_Glove_L': ('Left', 'Glove'), 'Kit_Glove_R': ('Right', 'GloveR')}


def cover(sex):
    hd = A.HeroData(sex)
    bone_names = hd.bones
    for piece, (side, tag) in SIDE.items():
        g = np.load(os.path.join(OUT, sex, 'golf_glove', piece + '_final.npz'))
        pos, tri, zone = g['pos'], g['tri'], g['zone']
        outer = tri[zone != 2]
        P = pos
        tree = BVHTree.FromPolygons([tuple(p) for p in P], [tuple(t) for t in outer], all_triangles=True)
        fn = np.cross(P[outer[:, 1]] - P[outer[:, 0]], P[outer[:, 2]] - P[outer[:, 0]]); fn /= np.linalg.norm(fn, axis=1, keepdims=True) + 1e-12
        ctr = P[outer].mean(1); centre = P.mean(0)
        orient = 1.0 if np.mean(np.einsum('ij,ij->i', fn, ctr - centre)) >= 0 else -1.0     # outward normals of the glove's outer wall
        # candidate body vertices: weighted to this side's hand chain, near the glove
        chain = [i for i, n in enumerate(bone_names) if n.startswith(side) and any(k in n for k in ('LowerArm', 'Hand', 'Thumb', 'Index', 'Middle', 'Ring', 'Little')) and 'ArmBlend' not in n]
        wsum = np.zeros(len(hd.body_pos))
        for k in range(4):
            wsum += np.where(np.isin(hd.bi[:, k], chain), hd.bw[:, k], 0)
        lo, hi = P.min(0) - .03, P.max(0) + .03
        cand = np.nonzero((wsum > .15) & np.all((hd.body_pos >= lo) & (hd.body_pos <= hi), axis=1))[0]
        covered = np.zeros(len(hd.body_pos), bool)
        for i in cand:
            loc, nrm, idx, d = tree.find_nearest(Vector(hd.body_pos[i]))
            if loc is None or d > .010: continue
            n = fn[idx] * orient
            if np.dot(n, hd.body_pos[i] - np.array(loc)) < .0005: covered[i] = True      # on the cavity side of the wall (or within half a millimetre outside)
        raw = hd.h.body.tri                                         # the Unity body mesh's own vertex order
        hide = raw[covered[raw].all(1)]
        path = os.path.join(OUT, sex, 'golf_glove', f'{tag}_hide.bin')
        hide.astype(np.int32).tofile(path)
        print(sex, piece, 'candidates', len(cand), 'covered verts', int(covered.sum()), 'hidden triangles', len(hide), '->', path)


if __name__ == '__main__':
    a = A.args_after_dashes(); which = a[0] if a else 'both'
    for s in (['Male', 'Female'] if which == 'both' else [which]): cover(s)
