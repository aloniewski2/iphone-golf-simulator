"""BAG: own geometry helpers (the shared acc_geo.py is not edited; what the bag needs beyond it lives here).
Blender 5.2 python + numpy. All in the product frame (= N frame: Z up, front -Y, hero's left +X)."""
import sys, os, math
sys.path.insert(0, os.path.dirname(__file__))
import numpy as np, bpy, bmesh
import acc_geo as G

unit = G.unit
smooth = G.smoothstep


def sstep(a, b, x):
    return G.smoothstep(a, b, x)


class SheetMesh(G.Mesh):
    """acc_geo.Mesh without the recalc-normals step (open sheets are oriented by construction)."""
    def to_object(self, name, collection=None, smooth_shade=True, fix=False):
        P = np.concatenate(self.P) if self.P else np.zeros((0, 3))
        me = bpy.data.meshes.new(name)
        me.from_pydata([tuple(p) for p in P], [], self.F)
        me.update()
        ob = bpy.data.objects.new(name, me)
        (collection or bpy.context.scene.collection).objects.link(ob)
        if smooth_shade: me.shade_smooth()
        za = me.attributes.new('zone', 'INT', 'FACE'); za.data.foreach_set('value', np.asarray(self.zone, np.int32))
        for k, parts in self.fields.items():
            arr = np.zeros(len(P))
            for base, v in parts: arr[base:base + len(v)] = v
            a = me.attributes.new(k, 'FLOAT', 'POINT'); a.data.foreach_set('value', arr.astype(np.float32))
        if fix: G.fix_normals(ob)
        return ob


def sheet_grid(name, P, fields=None, wrap_u=False, flip=False, face_attr=None):
    """one-layer surface from a (nu, nv, 3) grid; outward = du x dv (flip reverses).
    face_attr: dict name -> (nu_faces, nv-1) int array, set as face attributes in the grid's face order."""
    nu, nv, _ = P.shape
    m = SheetMesh(); fields = fields or {}
    ids = np.zeros((nu, nv), np.int64)
    for j in range(nv):
        base = m.add_points(P[:, j], **{k: v[:, j] for k, v in fields.items()})
        ids[:, j] = base + np.arange(nu)
    ucount = nu if wrap_u else nu - 1
    order = []
    for i in range(ucount):
        i2 = (i + 1) % nu
        for j in range(nv - 1):
            q = [ids[i, j], ids[i2, j], ids[i2, j + 1], ids[i, j + 1]]
            m.add_face(q[::-1] if flip else q, 0)
            order.append((i, j))
    ob = m.to_object(name)
    if face_attr:
        for k, arr in face_attr.items():
            vals = np.array([arr[i, j] for (i, j) in order], np.int32)
            a = ob.data.attributes.new(k, 'INT', 'FACE'); a.data.foreach_set('value', vals)
    return ob


def rbox(name, size, bevel=0.0012, seg=2, center=(0, 0, 0), R=None, fields=None):
    """closed box with bevelled edges. size = full extents (x, y, z) before rotation R (3x3); centre in the product frame."""
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        v.co.x *= size[0]; v.co.y *= size[1]; v.co.z *= size[2]
    if bevel > 0:
        bmesh.ops.bevel(bm, geom=list(bm.edges), offset=bevel, segments=seg, affect='EDGES', profile=0.5)
    R = np.eye(3) if R is None else np.asarray(R)
    for v in bm.verts:
        p = np.array(v.co) @ R.T + np.asarray(center)
        v.co = tuple(p)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me); bm.free()
    for f in me.polygons: f.use_smooth = True
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    za = me.attributes.new('zone', 'INT', 'FACE'); za.data.foreach_set('value', np.zeros(len(me.polygons), np.int32))
    for k, v in (fields or {}).items():
        a = me.attributes.new(k, 'FLOAT', 'POINT'); a.data.foreach_set('value', np.full(len(me.vertices), v, np.float32))
    return ob


def catmull(pts, n_per=12, alpha=0.5):
    """centripetal Catmull-Rom through pts (k,3) -> dense polyline (open)."""
    pts = np.asarray(pts, np.float64)
    P = np.vstack([2 * pts[0] - pts[1], pts, 2 * pts[-1] - pts[-2]])
    out = []
    for i in range(1, len(P) - 2):
        p0, p1, p2, p3 = P[i - 1], P[i], P[i + 1], P[i + 2]
        t0 = 0.0
        t1 = t0 + max(np.linalg.norm(p1 - p0), 1e-9) ** alpha
        t2 = t1 + max(np.linalg.norm(p2 - p1), 1e-9) ** alpha
        t3 = t2 + max(np.linalg.norm(p3 - p2), 1e-9) ** alpha
        ts = np.linspace(t1, t2, n_per, endpoint=False)[:, None]
        A1 = (t1 - ts) / (t1 - t0) * p0 + (ts - t0) / (t1 - t0) * p1
        A2 = (t2 - ts) / (t2 - t1) * p1 + (ts - t1) / (t2 - t1) * p2
        A3 = (t3 - ts) / (t3 - t2) * p2 + (ts - t2) / (t3 - t2) * p3
        B1 = (t2 - ts) / (t2 - t0) * A1 + (ts - t0) / (t2 - t0) * A2
        B2 = (t3 - ts) / (t3 - t1) * A2 + (ts - t1) / (t3 - t1) * A3
        C = (t2 - ts) / (t2 - t1) * B1 + (ts - t1) / (t2 - t1) * B2
        out.append(C)
    out.append(pts[-1][None])
    return np.vstack(out)


def arclen(path):
    d = np.linalg.norm(np.diff(path, axis=0), axis=1)
    return np.concatenate([[0], np.cumsum(d)])


def resample(path, n):
    s = arclen(path); t = np.linspace(0, s[-1], n)
    return np.stack([np.interp(t, s, path[:, k]) for k in range(3)], 1), t


def smooth_path(path, iters=2, keep_ends=True):
    p = path.copy()
    for _ in range(iters):
        q = p.copy(); q[1:-1] = 0.25 * p[:-2] + 0.5 * p[1:-1] + 0.25 * p[2:]
        p = q
    return p


def rot_to(a, b):
    """rotation matrix taking unit a to unit b."""
    a = unit(a); b = unit(b)
    v = np.cross(a, b); c = float(np.dot(a, b))
    if c < -0.999999:
        ax = unit(np.cross(a, [1, 0, 0] if abs(a[0]) < .9 else [0, 1, 0]))
        return 2 * np.outer(ax, ax) - np.eye(3)
    K = np.array([[0, -v[2], v[1]], [v[2], 0, -v[0]], [-v[1], v[0], 0]])
    return np.eye(3) + K + K @ K / (1 + c)


def frame_from_axis(axis, hint=(0, 1, 0)):
    """orthonormal (e1, e2, e3=axis) with e1 closest to hint."""
    e3 = unit(axis)
    e1 = np.asarray(hint, float) - e3 * np.dot(hint, e3)
    if np.linalg.norm(e1) < 1e-6: e1 = np.array([1.0, 0, 0]) - e3 * e3[0]
    e1 = unit(e1); e2 = np.cross(e3, e1)
    return e1, e2, e3


def set_face_int(ob, name, vals):
    me = ob.data
    a = me.attributes.new(name, 'INT', 'FACE')
    a.data.foreach_set('value', np.broadcast_to(np.asarray(vals), (len(me.polygons),)).astype(np.int32))


def set_point_float(ob, name, vals):
    me = ob.data
    a = me.attributes.new(name, 'FLOAT', 'POINT')
    a.data.foreach_set('value', np.broadcast_to(np.asarray(vals, np.float32), (len(me.vertices),)).astype(np.float32))


def tri_count(ob):
    me = ob.data
    return sum(max(len(p.vertices) - 2, 0) for p in me.polygons)
