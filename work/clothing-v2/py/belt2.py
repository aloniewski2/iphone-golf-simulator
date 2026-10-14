"""BELT v2 (golf): a leather belt that runs through the belt loops: the strap follows the REAL fitted waistband of the trousers / skort (ray-cast onto its outer face),
a buckle (frame + bar + prong), the tail with holes lying over the strap, a keeper. Rows are cast from the torso axis so it hugs any waist.
Blender -b --factory-startup --python belt2.py -- <hero> <design> <out.blend>"""
import sys, os, math
sys.path.insert(0, os.path.dirname(__file__))
import numpy as np
import bpy, bmesh
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from polo2 import apply_modifier, smoothstep, set_vattr, grid_faces

KIND = {'belt': 13, 'buckle': 14, 'tail': 13, 'loop': 5}
ROLE_BELT, ROLE_BUCKLE = 0, 1


def design(name):
    D = dict(
        male_golf=dict(width=.031, thick=.0034, lift=.0033, zc=.972, buckle=(.050, .040), tail=.115, roles=('Kit_Belt', 'Kit_Buckle'), n=132, rows=5, fabric=8),
        female_golf=dict(width=.026, thick=.0030, lift=.0030, zc=.9710, buckle=(.044, .033), tail=.095, roles=('Kit_Belt', 'Kit_Buckle'), n=132, rows=5, fabric=8))
    return D[name]


def make(name, V, F, role, kind, fabric, uv=None, corner_uv=None, panel=0, vattr=None):
    me = bpy.data.meshes.new(name)
    me.from_pydata([tuple(v) for v in V], [], [tuple(int(i) for i in f) for f in F]); me.update()
    for k, v in (('role', role), ('face_kind', kind), ('fabric', fabric), ('panel', panel)):
        a = me.attributes.new(k, 'INT', 'FACE'); a.data.foreach_set('value', np.full(len(me.polygons), v, np.int32))
    layer = me.uv_layers.new(name='UVMap')
    if corner_uv is not None: layer.data.foreach_set('uv', np.asarray(corner_uv, np.float32).ravel())
    elif uv is not None:
        li = np.zeros(len(me.loops), np.int32); me.loops.foreach_get('vertex_index', li)
        layer.data.foreach_set('uv', np.asarray(uv, np.float32)[li].ravel())
    for k, v in (vattr or {}).items():
        a = me.attributes.new(k, 'FLOAT', 'POINT'); a.data.foreach_set('value', np.asarray(v, np.float32))
    ob = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(ob)
    return ob


def box_uv(pts, faces, tg, up, nc):
    out = []
    for f in faces:
        q = pts[list(f)]; n = np.cross(q[1] - q[0], q[2] - q[0]); n /= np.linalg.norm(n) + 1e-12
        axes = [tg, up, nc]; k = int(np.argmax([abs(n @ a) for a in axes]))
        a, b_ = [axes[i] for i in range(3) if i != k]
        for p in q: out.append((p @ a, p @ b_))
    return out


def build(hero_id, dname, outp):
    D = design(dname)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    import cv2io
    udir = os.path.join(cv2io.ROOT, 'out', hero_id, 'unity')
    g = np.load(os.path.join(udir, 'Kit_Bottom_final.npz'))
    tri = g['tri']; kind = g['kind']; part = g['part']
    bt = tri[(kind == 12) & (part == 0)]
    P = g['pos']
    tree = BVHTree.FromPolygons([tuple(p) for p in P], [tuple(t) for t in bt], all_triangles=True)
    bp_ = P[np.unique(bt)]
    yc = float((bp_[:, 1].min() + bp_[:, 1].max()) / 2)
    z0 = D['zc'] - D['width'] / 2; rows = D['rows']; N = D['n']
    zs = np.linspace(z0, z0 + D['width'], rows)
    ang = np.linspace(0, 2 * math.pi, N, endpoint=False)
    Vg = np.zeros((rows, N, 3)); Ng = np.zeros((rows, N, 3))
    for j, z in enumerate(zs):
        for i, th in enumerate(ang):
            d = np.array([math.sin(th), -math.cos(th), 0.0]); o = np.array([0, yc, z])
            far = None; oo = o + d * .05
            r = tree.ray_cast(Vector(oo), Vector(d), 1.0)
            for _ in range(5):
                if r[0] is None: break
                far = (np.array(r[0]), np.array(r[1])); oo = far[0] + d * 1e-4; r = tree.ray_cast(Vector(oo), Vector(d), 1.0)
            if far is None:                          # no band here (the ray missed): fall back to the ring average
                far = (Vg[j, i - 1] if i else o + d * .2, d)
            nn = far[1] / np.linalg.norm(far[1])
            if nn @ d < 0: nn = -nn
            Vg[j, i] = far[0] + nn * (D['lift'] + .0006); Ng[j, i] = nn
    # smooth the strap a little so it reads as one band (not the band's triangulation)
    for _ in range(3): Vg[:, 1:-1] = (Vg[:, :-2] + 2 * Vg[:, 1:-1] + Vg[:, 2:]) / 4; Vg = (np.roll(Vg, 1, 1) + 2 * Vg + np.roll(Vg, -1, 1)) / 4
    # UV: 4 panels (islands), u = arc length from the panel start, v = height
    arc = np.zeros((rows, N + 1))
    for j in range(rows):
        arc[j, 1:] = np.cumsum(np.linalg.norm(np.diff(np.vstack([Vg[j], Vg[j][:1]]), axis=0), axis=1))
    F = []; corners = []; per = N // 4
    for j in range(rows - 1):
        for i in range(N):
            i2 = (i + 1) % N; pn = i // per
            F.append((j * N + i, j * N + i2, (j + 1) * N + i2, (j + 1) * N + i))
            def u(ii, jj): return arc[jj, ii] - arc[jj, pn * per]
            corners.append([(u(i, j), zs[j]), (u(i + 1, j), zs[j]), (u(i + 1, j + 1), zs[j + 1]), (u(i, j + 1), zs[j + 1])])
    # the buckle sits at the front centre (theta = 0 -> i = 0): leave the strap, the tail overlaps it
    belt = make('Belt', Vg.reshape(-1, 3), F, ROLE_BELT, KIND['belt'], D['fabric'], corner_uv=np.array([c for f in corners for c in f]))
    objs = [belt]
    # outward normal (from the axis): flip the loft if its normals face the axis
    # buckle: a frame (outer rectangle with a hole) at the front centre
    pc = Vg[rows // 2, 0]; nc = Ng[rows // 2, 0]; tg = np.array([nc[1] * -1, nc[0], 0.0]); tg = np.array([-nc[1], nc[0], 0]); tg /= np.linalg.norm(tg)
    up = np.array([0, 0, 1.0]); bw_, bh_ = D['buckle']
    t_out = .0044
    def box(c, sx, sz, sy, bev=.0012):
        """a closed rounded-ish box centred on c in the (tangent, up, normal) frame"""
        pts = []
        for ix in (-1, 1):
            for iz in (-1, 1):
                for iy in (0, 1): pts.append(c + tg * sx / 2 * ix + up * sz / 2 * iz + nc * sy * iy)
        return np.array(pts)
    # frame as 4 bars (each a box): top, bottom, left, right
    bar = .0062; fr_bars = [(pc + up * (bh_ / 2 - bar / 2), bw_, bar), (pc - up * (bh_ / 2 - bar / 2), bw_, bar), (pc + tg * (bw_ / 2 - bar / 2), bar, bh_), (pc - tg * (bw_ / 2 - bar / 2), bar, bh_)]
    for k, (c, sx, sz) in enumerate(fr_bars):
        pts = box(c + nc * .0003, sx, sz, t_out)
        # box faces: indices (ix, iz, iy) -> 4*ix' + 2*iz' + iy with ix,iz in {-1,1} mapped to 0/1
        idx = lambda ix, iz, iy: ((0 if ix < 0 else 1) * 4 + (0 if iz < 0 else 1) * 2 + iy)
        faces = []
        for iy in (0, 1):
            faces.append((idx(-1, -1, iy), idx(1, -1, iy), idx(1, 1, iy), idx(-1, 1, iy)))
        for ix in (-1, 1):
            faces.append((idx(ix, -1, 0), idx(ix, 1, 0), idx(ix, 1, 1), idx(ix, -1, 1)))
        for iz in (-1, 1):
            faces.append((idx(-1, iz, 0), idx(-1, iz, 1), idx(1, iz, 1), idx(1, iz, 0)))
        ob = make('Buckle%d' % k, pts, faces, ROLE_BUCKLE, KIND['buckle'], 0, corner_uv=box_uv(pts, faces, tg, up, nc), panel=2)
        objs.append(ob)
    # prong: a thin bar across the frame, slightly raised
    pr = box(pc + nc * .002, .0034, bh_ - .002, .0034)
    idx = lambda ix, iz, iy: ((0 if ix < 0 else 1) * 4 + (0 if iz < 0 else 1) * 2 + iy)
    faces = [(idx(-1, -1, iy), idx(1, -1, iy), idx(1, 1, iy), idx(-1, 1, iy)) for iy in (0, 1)] + [(idx(ix, -1, 0), idx(ix, 1, 0), idx(ix, 1, 1), idx(ix, -1, 1)) for ix in (-1, 1)] + [(idx(-1, iz, 0), idx(-1, iz, 1), idx(1, iz, 1), idx(1, iz, 0)) for iz in (-1, 1)]
    objs.append(make('Prong', pr, faces, ROLE_BUCKLE, KIND['buckle'], 0, corner_uv=box_uv(pr, faces, tg, up, nc), panel=2))
    # tail: the belt end passes through the buckle and lies over the strap toward the wearer's left (x < 0 in mesh space: -tg direction)
    nT = 16; nW = 5; tailV = []; tailUV = []
    L = D['tail']; half = D['width'] / 2 - .0012
    # follow the ring: step along the strap rows by index so the tail hugs the waist
    istep = int(round(L / (arc[rows // 2, 1] - arc[rows // 2, 0])))
    side = -1                                                       # towards decreasing i (wearer's left in the render)
    for a_ in range(nT):
        ii = (side * int(round(istep * a_ / (nT - 1)))) % N
        for b_ in range(nW):
            zz = zs[0] + (D['width'] - 2 * .0012) * b_ / (nW - 1) + .0012
            jr = min(rows - 1, max(0, int(round((zz - zs[0]) / (zs[1] - zs[0])))))
            p0 = Vg[jr, ii] + Ng[jr, ii] * (D['thick'] * .95)
            tailV.append(np.array([p0[0], p0[1], zz + (p0[2] - Vg[jr, ii][2]) * 0])); tailUV.append((a_ * L / (nT - 1), zz))
    # round the tip: narrow the last columns
    tv = np.array(tailV).reshape(nT, nW, 3)
    for a_ in range(nT - 4, nT):
        k = (a_ - (nT - 5)) / 4.0; zc_ = tv[a_, :, 2].mean()
        tv[a_, :, 2] = zc_ + (tv[a_, :, 2] - zc_) * math.sqrt(max(0, 1 - k * k)) * 1.0 + 0
    tail = make('Tail', tv.reshape(-1, 3), grid_faces(nT, nW), ROLE_BELT, KIND['tail'], D['fabric'], uv=np.array(tailUV), panel=1,
                vattr=dict(tl_u=np.repeat(np.arange(nT) * L / (nT - 1), nW)))
    objs.append(tail)
    # keeper: a small loop just beside the buckle on the strap side
    ik = (-int(round(.020 / (arc[rows // 2, 1] - arc[rows // 2, 0])))) % N
    kv = []; kuv = []
    for a_ in range(3):
        ii = (ik + (a_ - 1) * 2 * 1) % N
        for b_ in range(nW):
            zz = zs[0] + (D['width'] - .0016) * b_ / (nW - 1) + .0008
            jr = min(rows - 1, max(0, int(round((zz - zs[0]) / (zs[1] - zs[0])))))
            kv.append(Vg[jr, ii] + Ng[jr, ii] * (D['thick'] * 1.9)); kuv.append((a_ * .004, zz))
    objs.append(make('Keeper', np.array(kv), grid_faces(3, nW), ROLE_BELT, KIND['loop'], D['fabric'], uv=np.array(kuv), panel=3))
    # orientation: leather parts face away from the waist axis
    ax = Vector((0, yc, D['zc']))
    for o in objs:
        me = o.data; bm = bmesh.new(); bm.from_mesh(me); bm.faces.ensure_lookup_table()
        if o.name.startswith('Buckle') or o.name == 'Prong': bm.free(); continue
        f0 = bm.faces[len(bm.faces) // 2]; c0 = f0.calc_center_median()
        if f0.normal.dot(c0 - Vector((ax.x, ax.y, c0.z))) < 0: bmesh.ops.reverse_faces(bm, faces=bm.faces)
        bm.to_mesh(me); bm.free(); me.update()
    # thickness + rounded edges on the leather, bevel on the metal
    for o in objs:
        o.data.shade_smooth()
        for nm in ('outer', 'inner', 'rim'): o.data.materials.append(bpy.data.materials.get(nm) or bpy.data.materials.new(nm))
        if o.name in ('Belt', 'Tail', 'Keeper'):
            sm = o.modifiers.new('solid', 'SOLIDIFY'); sm.thickness = D['thick'] if o.name != 'Keeper' else .0018; sm.offset = -1; sm.use_even_offset = True
            sm.use_quality_normals = True; sm.use_rim = True; sm.material_offset = 1; sm.material_offset_rim = 2
            apply_modifier(o, sm)
        bv = o.modifiers.new('bevel', 'BEVEL'); bv.width = .0006; bv.segments = 2; bv.limit_method = 'ANGLE'; bv.angle_limit = math.radians(50)
        apply_modifier(o, bv)
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = belt
    bpy.ops.object.join()
    belt.name = 'Kit_Belt'; belt['roles'] = list(D['roles'])
    bpy.ops.wm.save_as_mainfile(filepath=outp)
    print('BELT faces %d' % len(belt.data.polygons))


if __name__ == '__main__' or 'belt2.py' in ' '.join(sys.argv):
    a = sys.argv[sys.argv.index('--') + 1:]
    build(a[0], a[1], a[2])
