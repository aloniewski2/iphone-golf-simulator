import sys, time, math
sys.path.insert(0, '/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator/work/accessories/py')
import numpy as np, bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree
import acc_common as A
for sex in ('Male','Female'):
    hd = A.HeroData(sex)
    tree = BVHTree.FromPolygons([tuple(p) for p in hd.body_pos], [tuple(t) for t in hd.body_tri], all_triangles=True)
    print(sex)
    for xs in (-0.08, -0.095, -0.105, -0.12, -0.14):
        best = None
        for y in np.linspace(-0.12, 0.14, 53):
            r = tree.ray_cast(Vector((xs, y, 2.0)), Vector((0, 0, -1)))
            if r[0] is not None and (best is None or r[0][2] > best[1]): best = (y, r[0][2])
        print('  crest at x=%.3f: y=%.3f z=%.3f' % (xs, best[0], best[1]))
    for z in (1.35, 1.31, 1.27, 1.23, 1.19, 1.15):
        row = []
        for xs in (-0.10, -0.13, -0.16):
            r = tree.ray_cast(Vector((xs, -0.6, z)), Vector((0, 1, 0)))
            row.append('x=%.2f front y=%s' % (xs, ('%.3f' % r[0][1]) if r[0] is not None else '-'))
        print('  z=%.2f' % z, ' | '.join(row))
    for z in (1.20, 1.15, 1.10, 1.05):
        for y in (-0.05, 0.0, 0.05):
            r = tree.ray_cast(Vector((0, y, z)), Vector((-1, 0, 0)))
            print('  flank z=%.2f y=%.2f  x=%s' % (z, y, ('%.3f' % r[0][0]) if r[0] is not None else '-'))
