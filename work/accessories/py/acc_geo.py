"""ACCESSORIES geometry builders (Blender python + numpy). Everything is built as real closed shells: an outer surface, an inner surface offset by the
material thickness, and a rounded edge joining them. Part meshes carry integer face attributes (`zone`: 0 outer, 1 rim, 2 inner) and float point attributes.

Blender 5.2 notes: attributes are created with mesh.attributes.new(name, type, domain); `me.from_pydata` for topology; normals via bmesh.recalc_face_normals.
"""
import math
import numpy as np
import bpy, bmesh
from mathutils import Vector

ZONE_OUTER, ZONE_RIM, ZONE_INNER = 0, 1, 2


def unit(v, axis=-1):
    v = np.asarray(v, np.float64)
    return v / (np.linalg.norm(v, axis=axis, keepdims=True) + 1e-15)


def smoothstep(a, b, x):
    t = np.clip((np.asarray(x, np.float64) - a) / (b - a), 0, 1); return t * t * (3 - 2 * t)


# ------------------------------------------------------------------ parametric grid -> thick shell
def grid_normals(P, wrap_u=False, pole_top=False, pole_bottom=False):
    """vertex normals of a (nu, nv, 3) grid (u = first axis, may wrap; v = second). Orientation: du x dv."""
    nu, nv, _ = P.shape
    if wrap_u:
        du = (np.roll(P, -1, 0) - np.roll(P, 1, 0))
    else:
        du = np.gradient(P, axis=0)
    dv = np.gradient(P, axis=1)
    n = np.cross(du, dv)
    ln = np.linalg.norm(n, axis=-1, keepdims=True)
    n = n / np.maximum(ln, 1e-12)
    if pole_top:
        # degenerate row: average the neighbouring row's normals
        m = unit(n[:, -2].mean(0)); n[:, -1] = m
    if pole_bottom:
        m = unit(n[:, 1].mean(0)); n[:, 0] = m
    return n


class Mesh:
    """tiny indexed-mesh builder: add vertices (with optional per-point scalar fields), quads / tris with a zone id."""
    def __init__(self):
        self.P = []; self.F = []; self.zone = []; self.fields = {}; self.nv = 0

    def add_points(self, pts, **fields):
        pts = np.asarray(pts, np.float64).reshape(-1, 3)
        base = self.nv
        self.P.append(pts); self.nv += len(pts)
        for k, v in fields.items():
            self.fields.setdefault(k, []).append((base, np.broadcast_to(np.asarray(v, np.float64), (len(pts),)).copy()))
        return base

    def add_face(self, idx, zone=0):
        self.F.append(list(idx)); self.zone.append(zone)

    def to_object(self, name, collection=None, smooth=True):
        P = np.concatenate(self.P) if self.P else np.zeros((0, 3))
        me = bpy.data.meshes.new(name)
        me.from_pydata([tuple(p) for p in P], [], self.F)
        me.update()
        ob = bpy.data.objects.new(name, me)
        (collection or bpy.context.scene.collection).objects.link(ob)
        if smooth: me.shade_smooth()
        za = me.attributes.new('zone', 'INT', 'FACE'); za.data.foreach_set('value', np.asarray(self.zone, np.int32))
        for k, parts in self.fields.items():
            arr = np.zeros(len(P))
            for base, v in parts: arr[base:base + len(v)] = v
            a = me.attributes.new(k, 'FLOAT', 'POINT'); a.data.foreach_set('value', arr.astype(np.float32))
        fix_normals(ob)
        return ob


def fix_normals(ob):
    me = ob.data
    bm = bmesh.new(); bm.from_mesh(me)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(me); bm.free(); me.update()


def thick_grid(name, P, T, wrap_u=False, pole_top=False, pole_bottom=False, edge_steps=4, rim_u=(True, True), rim_v=(True, True), fields=None,
               round_edge=True, inner_offset=None, collection=None, edge_bulge=1.0):
    """P: (nu, nv, 3) outer surface (a pole row may be degenerate); T: thickness (scalar or (nu, nv) array) offset inward (against the grid normal,
    which is oriented outward by the caller via `orient`). Returns the object (zones: 0 outer, 1 rim, 2 inner).
    rim_u / rim_v choose which grid borders get a rim: (first, last) along u and along v (ignored where wrap/pole)."""
    nu, nv, _ = P.shape
    Tg = np.broadcast_to(np.asarray(T, np.float64), (nu, nv)).copy()
    Nrm = grid_normals(P, wrap_u, pole_top, pole_bottom)
    inner = P - Nrm * Tg[..., None]
    m = Mesh()
    fields = fields or {}

    def fld(arr):
        return arr
    # outer vertices; a pole row collapses to a single vertex
    def build(Q, tag):
        ids = -np.ones((nu, nv), np.int64)
        for j in range(nv):
            if (pole_top and j == nv - 1) or (pole_bottom and j == 0):
                base = m.add_points(Q[0, j][None], **{k: v[0, j] for k, v in fields.items()})
                ids[:, j] = base
            else:
                base = m.add_points(Q[:, j], **{k: v[:, j] for k, v in fields.items()})
                ids[:, j] = base + np.arange(nu)
        return ids
    ido = build(P, 'o'); idi = build(inner, 'i')
    ucount = nu if wrap_u else nu - 1
    for i in range(ucount):
        i2 = (i + 1) % nu
        for j in range(nv - 1):
            q = [ido[i, j], ido[i2, j], ido[i2, j + 1], ido[i, j + 1]]
            qi = [idi[i, j], idi[i2, j], idi[i2, j + 1], idi[i, j + 1]]
            for face, zone in ((q, 0), (qi[::-1], 2)):
                uq = []
                for v in face:
                    if not uq or uq[-1] != v: uq.append(v)
                if len(uq) > 1 and uq[0] == uq[-1]: uq.pop()
                if len(uq) >= 3: m.add_face(uq, zone)

    def rim_ring(path_o, path_i, tang_out, closed):
        """path_o, path_i: (k, 3) matching boundary points (outer / inner); tang_out: (k, 3) unit vector pointing away from the surface at the border."""
        k = len(path_o)
        rows = []
        for s in range(edge_steps + 1):
            th = math.pi * s / (edge_steps + 1 if False else edge_steps)
            c = (path_o + path_i) * .5
            a = (path_o - path_i) * .5
            b = tang_out * (np.linalg.norm(a, axis=1, keepdims=True) * edge_bulge)
            rows.append(c + a * math.cos(th) + b * math.sin(th) if round_edge else (path_o if s == 0 else path_i if s == edge_steps else c))
        return rows

    def add_rim(o_ids, i_ids, path_o, path_i, tang_out, closed, bf=None):
        k = len(o_ids)
        rows = rim_ring(path_o, path_i, tang_out, closed)
        row_ids = [o_ids]
        for s in range(1, edge_steps):
            base = m.add_points(rows[s], **(bf or {})); row_ids.append(base + np.arange(k))
        row_ids.append(i_ids)
        ncols = k if closed else k - 1
        for s in range(edge_steps):
            for c in range(ncols):
                c2 = (c + 1) % k
                face = [row_ids[s][c], row_ids[s][c2], row_ids[s + 1][c2], row_ids[s + 1][c]]
                uq = []
                for v in face:
                    if not uq or uq[-1] != v: uq.append(v)
                if len(uq) > 1 and uq[0] == uq[-1]: uq.pop()
                if len(uq) >= 3: m.add_face(uq, 1)

    dv = np.gradient(P, axis=1); du = np.gradient(P, axis=0) if not wrap_u else (np.roll(P, -1, 0) - np.roll(P, 1, 0)) * .5
    def bfl(sl):
        return {k: v[sl] for k, v in fields.items()}
    if wrap_u:
        # closed around u (the crown): one ring along each requested v border
        if not pole_bottom and rim_v[0]:
            add_rim(ido[:, 0], idi[:, 0], P[:, 0], inner[:, 0], -unit(dv[:, 0]), True, bfl((slice(None), 0)))
        if not pole_top and rim_v[1]:
            add_rim(ido[:, -1], idi[:, -1], P[:, -1], inner[:, -1], unit(dv[:, -1]), True, bfl((slice(None), -1)))
    else:
        # open sheet: ONE perimeter ring (corners shared), optionally open along the sides without a rim (the brim root)
        per = [(0, j) for j in range(nv)] + [(i, nv - 1) for i in range(1, nu)] + [(nu - 1, j) for j in range(nv - 2, -1, -1)] + [(i, 0) for i in range(nu - 2, 0, -1)]
        side = []
        for (i, j) in per:
            sides = []
            if i == 0: sides.append('u0')
            if i == nu - 1: sides.append('u1')
            if j == 0: sides.append('v0')
            if j == nv - 1: sides.append('v1')
            side.append(sides)
        want = {'u0': rim_u[0], 'u1': rim_u[1], 'v0': rim_v[0], 'v1': rim_v[1]}
        keep = [all(want[s_] for s_ in sd) or (len(sd) > 1 and any(want[s_] for s_ in sd)) for sd in side]
        if all(keep):
            order = list(range(len(per))); closed = True
        elif any(keep):
            # rotate so the dropped run is at the end, then take the kept prefix (an open chain)
            first = next(k for k in range(len(per)) if keep[k] and not keep[k - 1])
            order = []
            k = first
            while keep[k % len(per)] and len(order) < len(per):
                order.append(k % len(per)); k += 1
            closed = False
        else:
            order = []; closed = False
        if order:
            ii = np.array([per[k][0] for k in order]); jj = np.array([per[k][1] for k in order])
            out = np.zeros((len(order), 3))
            for n_, k in enumerate(order):
                i, j = per[k]
                if i == 0: out[n_] += -unit(du[0, j])
                if i == nu - 1: out[n_] += unit(du[-1, j])
                if j == 0: out[n_] += -unit(dv[i, 0])
                if j == nv - 1: out[n_] += unit(dv[i, -1])
            out = unit(out)
            add_rim(ido[ii, jj], idi[ii, jj], P[ii, jj], inner[ii, jj], out, closed,
                    {k_: v[ii, jj] for k_, v in fields.items()})
    ob = m.to_object(name, collection)
    return ob


def tube(name, path, radius, sides=8, closed=False, caps=True, radius_fn=None, fields=None, collection=None):
    """a round tube along a polyline (k,3). radius may be a scalar or a (k,) array. Closed shells with end caps."""
    path = np.asarray(path, np.float64); k = len(path)
    rad = np.broadcast_to(np.asarray(radius, np.float64), (k,))
    tang = np.zeros_like(path)
    if closed:
        tang = np.roll(path, -1, 0) - np.roll(path, 1, 0)
    else:
        tang[1:-1] = path[2:] - path[:-2]; tang[0] = path[1] - path[0]; tang[-1] = path[-1] - path[-2]
    tang = unit(tang)
    # parallel-transport frame
    ref = np.array([0, 0, 1.]) if abs(tang[0, 2]) < .9 else np.array([1., 0, 0])
    n0 = unit(np.cross(tang[0], ref)); b0 = np.cross(tang[0], n0)
    Ns = [n0];
    for i in range(1, k):
        v = Ns[-1] - tang[i] * np.dot(Ns[-1], tang[i]); Ns.append(unit(v))
    Ns = np.array(Ns); Bs = np.cross(tang, Ns)
    m = Mesh(); ids = []
    for i in range(k):
        ang = np.linspace(0, 2 * math.pi, sides, endpoint=False)
        ring = path[i] + (np.cos(ang)[:, None] * Ns[i] + np.sin(ang)[:, None] * Bs[i]) * rad[i]
        fl = {kk: v[i] for kk, v in (fields or {}).items()}
        base = m.add_points(ring, **fl); ids.append(base + np.arange(sides))
    nseg = k if closed else k - 1
    for i in range(nseg):
        a, b = ids[i], ids[(i + 1) % k]
        for s in range(sides):
            s2 = (s + 1) % sides
            m.add_face([a[s], a[s2], b[s2], b[s]], 0)
    if not closed and caps:
        for end, idr in ((0, ids[0]), (1, ids[-1])):
            c = m.add_points(path[0 if end == 0 else -1][None], **{kk: v[0 if end == 0 else -1] for kk, v in (fields or {}).items()})
            for s in range(sides):
                s2 = (s + 1) % sides
                m.add_face([c, idr[s], idr[s2]] if end == 0 else [c, idr[s2], idr[s]], 0)
    return m.to_object(name)


def lathe(name, profile, sides=24, axis_pts=True, closed_top=True, closed_bottom=True, scale_xy=(1, 1), fields=None):
    """revolve a (k,2) profile (r, z) around Z. Ends with r=0 get a single pole vertex."""
    prof = np.asarray(profile, np.float64); k = len(prof)
    m = Mesh(); ids = []
    ang = np.linspace(0, 2 * math.pi, sides, endpoint=False)
    for i in range(k):
        r, z = prof[i]
        if r < 1e-9:
            base = m.add_points(np.array([[0, 0, z]])); ids.append(np.full(sides, base))
        else:
            pts = np.stack([r * scale_xy[0] * np.cos(ang), r * scale_xy[1] * np.sin(ang), np.full(sides, z)], 1)
            base = m.add_points(pts); ids.append(base + np.arange(sides))
    for i in range(k - 1):
        for s in range(sides):
            s2 = (s + 1) % sides
            f = [ids[i][s], ids[i][s2], ids[i + 1][s2], ids[i + 1][s]]
            uq = []
            for v in f:
                if not uq or uq[-1] != v: uq.append(v)
            if len(uq) > 1 and uq[0] == uq[-1]: uq.pop()
            if len(uq) >= 3: m.add_face(uq, 0)
    # flat caps if the first/last profile points are off-axis
    for end, row in ((0, ids[0]), (1, ids[-1])):
        r = prof[0 if end == 0 else -1][0]
        if r > 1e-9 and ((end == 0 and closed_bottom) or (end == 1 and closed_top)):
            c = m.add_points(np.array([[0, 0, prof[0 if end == 0 else -1][1]]]))
            for s in range(sides):
                s2 = (s + 1) % sides
                m.add_face([c, row[s], row[s2]] if end == 0 else [c, row[s2], row[s]], 0)
    return m.to_object(name)


# ------------------------------------------------------------------ attributes, UVs, materials
def set_face_attr(ob, name, values, typ='INT'):
    me = ob.data
    a = me.attributes.new(name, typ, 'FACE')
    a.data.foreach_set('value', np.broadcast_to(np.asarray(values), (len(me.polygons),)).astype(np.int32 if typ == 'INT' else np.float32))


def point_attr(ob, name):
    me = ob.data
    if name not in me.attributes: return np.zeros(len(me.vertices))
    v = np.zeros(len(me.vertices), np.float32); me.attributes[name].data.foreach_get('value', v); return v.astype(np.float64)


def positions(ob):
    me = ob.data
    p = np.zeros(len(me.vertices) * 3); me.vertices.foreach_get('co', p); return p.reshape(-1, 3)


def set_positions(ob, P):
    ob.data.vertices.foreach_set('co', np.asarray(P, np.float32).ravel()); ob.data.update()


def mark_seams_between_zones(ob, extra_edges=None):
    """UV seams: every edge shared by faces of different zone, plus `extra_edges` (a set of vertex-index pairs)."""
    me = ob.data
    zone = np.zeros(len(me.polygons), np.int32)
    if 'zone' in me.attributes: me.attributes['zone'].data.foreach_get('value', zone)
    bm = bmesh.new(); bm.from_mesh(me)
    bm.faces.ensure_lookup_table(); bm.edges.ensure_lookup_table()
    extra = {tuple(sorted(e)) for e in (extra_edges or [])}
    for e in bm.edges:
        zs = {zone[f.index] for f in e.link_faces}
        e.seam = (len(zs) > 1) or (tuple(sorted(v.index for v in e.verts)) in extra) or len(e.link_faces) == 1
    bm.to_mesh(me); bm.free(); me.update()


def unwrap(ob, margin=.004, method='ANGLE_BASED'):
    bpy.context.view_layer.objects.active = ob
    for o in bpy.context.selected_objects: o.select_set(False)
    ob.select_set(True)
    if not ob.data.uv_layers: ob.data.uv_layers.new(name='UVMap')
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.unwrap(method=method, margin=margin)
    bpy.ops.object.mode_set(mode='OBJECT')


def join(objs, name):
    for o in bpy.context.selected_objects: o.select_set(False)
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join()
    ob = bpy.context.view_layer.objects.active; ob.name = name; ob.data.name = name
    return ob


def apply_modifier(ob, mod):
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.modifier_apply(modifier=mod.name)


def boolean_diff(ob, cutter, solver='EXACT'):
    m = ob.modifiers.new('bool', 'BOOLEAN'); m.operation = 'DIFFERENCE'; m.object = cutter; m.solver = solver
    apply_modifier(ob, m)
    bpy.data.objects.remove(cutter)


def mesh_stats(ob):
    me = ob.data
    bm = bmesh.new(); bm.from_mesh(me)
    bmesh.ops.triangulate(bm, faces=bm.faces)
    t = len(bm.faces); v = len(bm.verts)
    # non-manifold edge count
    nm = sum(1 for e in bm.edges if len(e.link_faces) != 2)
    bm.free()
    return dict(verts=v, tris=t, non_manifold_edges=nm)
