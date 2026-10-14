"""Shoe fit check: distance of the shoe's INNER wall to the hero's skin (foot + shin) and to the sock, per region.  Blender -b --factory-startup --python shoe_check.py -- <hero>
inner wall = faces with material slot 1 ('inner') of the upper + the sole top; a negative / tiny value means skin or sock would poke through."""
import sys, os
sys.path.insert(0, os.path.dirname(__file__))
import numpy as np, bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree
import cv2io, glib as G, shoe2

hero_id = sys.argv[sys.argv.index('--') + 1]
hero = cv2io.Hero(hero_id); body = G.Body(hero)
res = {}
for side in 'LR':
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.wm.open_mainfile(filepath=os.path.join(cv2io.ROOT, 'shoes', ('%s_shoe_%s.blend' if os.environ.get('SHOE_PRE') else '%s_shoe_%s_final.blend') % (hero_id, side)))
    ob = [o for o in bpy.data.objects if o.name.startswith('Kit_Shoe')][0]; me = ob.data
    n = len(me.vertices); p = np.zeros(n * 3); me.vertices.foreach_get('co', p); p = p.reshape(-1, 3)
    mi = np.zeros(len(me.polygons), np.int32); me.attributes['material_index'].data.foreach_get('value', mi)
    role = np.zeros(len(me.polygons), np.int32); me.attributes['role'].data.foreach_get('value', role)
    pan = np.zeros(len(me.polygons), np.int32); me.attributes['panel'].data.foreach_get('value', pan)
    fr_ = shoe2.Frame(body, side)
    for pname, pid in (('upper', 0), ('tongue', 3), ('heeltab', 6)):
        inner = sorted({v for pl, m_, r_, pa in zip(me.polygons, mi, role, pan) if m_ == 1 and r_ == 0 and pa == pid for v in pl.vertices})
        if not inner: continue
        q = p[inner]
        d = np.array([body.bvh.find_nearest(Vector(v))[3] for v in q])
        sg = np.array([np.dot(v - np.array(body.bvh.find_nearest(Vector(v))[0]), np.array(body.bvh.find_nearest(Vector(v))[1])) for v in q])
        ds = np.where(sg >= 0, d, -d); near = d < .025
        # second opinion: ray parity against the body (3 directions, majority) - the nearest-normal sign can flip in creases
        def inside(v):
            votes = 0
            for dv in ((0, 0, 1.), (1., 0, 0), (0, 1., 0)):
                o = Vector(v) + Vector(dv) * 1e-5; n_ = 0; guard = 0
                while guard < 12:
                    h = body.bvh.ray_cast(o, Vector(dv), 2.0)
                    if h[0] is None: break
                    n_ += 1; o = h[0] + Vector(dv) * 1e-5; guard += 1
                votes += n_ % 2
            return votes >= 2
        cand = np.nonzero(ds < 0)[0]
        ins = np.array([inside(q[i]) for i in cand]) if len(cand) else np.array([], bool)
        print('PARITY %s %s %-7s sign-negative %d  of which truly inside the body (ray parity) %d' % (hero_id, side, pname, len(cand), int(ins.sum())), flush=True)
        if pname == 'upper':
            fq = fr_.to(q); w_ = np.argsort(ds)[:8]
            for i_ in w_: print('WORST %s y/L %.2f z %.3f x %.3f signed %.1f mm' % (side, fq[i_, 1] / fr_.L, fq[i_, 2], fq[i_, 0], ds[i_] * 1e3), flush=True)
        print('SHOECHECK %s %s %-7s verts %d  min signed %.1f mm  p1 %.1f mm  median(near) %.1f mm  inside-skin verts %d' % (hero_id, side, pname, len(q), ds.min() * 1e3, np.percentile(ds, 1) * 1e3, np.median(d[near]) * 1e3, int((ds < 0).sum())), flush=True)
    continue
    fr = shoe2.Frame(body, side)
    # skin near the foot only (feet + shin below the collar)
    sel = (fr.wf + fr.wl) > .3
    d = np.array([body.bvh.find_nearest(Vector(v))[3] for v in q])
    # sign: inside the body surface means the shell vertex is under the skin -> poke
    sgn = []
    for v in q[::1]:
        r = body.bvh.find_nearest(Vector(v)); cp, nn = np.array(r[0]), np.array(r[1]); sgn.append(np.dot(v - cp, nn))
    sgn = np.array(sgn); d_signed = np.where(sgn >= 0, d, -d)
    # only the part of the wall that actually surrounds the foot: ignore wall vertices farther than 25 mm (heel cup air etc.)
    near = d < .025
    zq = q[:, 2]
    print('SHOECHECK %s %s inner verts %d  min signed %.1f mm  p1 %.1f mm  median(near) %.1f mm  inside-skin verts %d' % (
        hero_id, side, len(q), d_signed.min() * 1e3, np.percentile(d_signed, 1) * 1e3, np.median(d[near]) * 1e3, int((d_signed < 0).sum())), flush=True)
