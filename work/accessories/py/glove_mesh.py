"""GLOVE: mesh utilities on numpy (V, T) with Blender (bmesh / modifiers) doing the topological work: decimation, plane cut, boundary loops, graph distances."""
import os, sys, math, heapq
sys.path.insert(0, os.path.dirname(__file__))
import numpy as np
import bpy, bmesh
import glove_sdf as S


def to_obj(V, T, name='tmp'):
    me = bpy.data.meshes.new(name)
    me.vertices.add(len(V)); me.vertices.foreach_set('co', np.asarray(V, np.float32).ravel())
    T = np.asarray(T, np.int32)
    me.loops.add(T.size); me.loops.foreach_set('vertex_index', T.ravel())
    me.polygons.add(len(T)); me.polygons.foreach_set('loop_start', np.arange(0, T.size, 3, dtype=np.int32))
    me.update(calc_edges=True); me.validate(clean_customdata=False)
    ob = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(ob)
    return ob


def from_obj(ob, evaluated=False):
    if evaluated:
        dg = bpy.context.evaluated_depsgraph_get(); eo = ob.evaluated_get(dg); me = eo.to_mesh()
    else: me = ob.data
    n = len(me.vertices)
    V = np.zeros(n * 3); me.vertices.foreach_get('co', V); V = V.reshape(-1, 3)
    bm = bmesh.new(); bm.from_mesh(me); bmesh.ops.triangulate(bm, faces=bm.faces)
    bm.verts.ensure_lookup_table()
    T = np.array([[v.index for v in f.verts] for f in bm.faces], np.int64)
    bm.free()
    if evaluated: eo.to_mesh_clear()
    return V, T


def drop_unused(V, T):
    used = np.unique(T); remap = -np.ones(len(V), np.int64); remap[used] = np.arange(len(used))
    return V[used], remap[T], used


def decimate(V, T, ratio):
    ob = to_obj(V, T, 'dec')
    m = ob.modifiers.new('d', 'DECIMATE'); m.decimate_type = 'COLLAPSE'; m.ratio = ratio; m.use_collapse_triangulate = True
    V2, T2 = from_obj(ob, evaluated=True)
    bpy.data.objects.remove(ob)
    V2, T2, _ = drop_unused(V2, T2)
    return V2, T2


def bisect_plane(V, T, co, no, keep_positive=True):
    """exact cut by a plane; keeps the side the normal points to (keep_positive) -> (V, T, new_vertex_mask)."""
    bm = bmesh.new()
    vs = [bm.verts.new(tuple(p)) for p in V]
    for t in T: bm.faces.new([vs[i] for i in t])
    bm.verts.ensure_lookup_table(); bm.faces.ensure_lookup_table()
    geom = list(bm.verts) + list(bm.edges) + list(bm.faces)
    r = bmesh.ops.bisect_plane(bm, geom=geom, plane_co=tuple(co), plane_no=tuple(no), clear_outer=not keep_positive, clear_inner=keep_positive, dist=1e-9)
    bm.verts.ensure_lookup_table()
    # remove loose
    loose = [v for v in bm.verts if not v.link_faces]
    bmesh.ops.delete(bm, geom=loose, context='VERTS')
    bmesh.ops.triangulate(bm, faces=bm.faces)
    bm.verts.ensure_lookup_table()
    P = np.array([v.co[:] for v in bm.verts]); idx = {v: i for i, v in enumerate(bm.verts)}
    T2 = np.array([[idx[v] for v in f.verts] for f in bm.faces], np.int64)
    bm.free()
    onplane = np.abs((P - np.asarray(co)) @ np.asarray(no)) < 1e-7
    return P, T2, onplane


def boundary_edges(T):
    e = np.concatenate([T[:, [0, 1]], T[:, [1, 2]], T[:, [2, 0]]])
    key = np.sort(e, 1)
    _, inv, cnt = np.unique(key, axis=0, return_inverse=True, return_counts=True)
    inv = inv.ravel()
    b = cnt[inv] == 1
    return e[b]                                                  # directed as in the face (outer boundary runs with the face on its left)


def boundary_loops(T):
    be = boundary_edges(T)
    nxt = {int(a): int(b) for a, b in be}
    loops = []; seen = set()
    for a in nxt:
        if a in seen: continue
        loop = [a]; seen.add(a); c = nxt[a]
        while c != a and c not in seen:
            loop.append(c); seen.add(c); c = nxt.get(c, a)
        loops.append(np.array(loop))
    return loops


def adjacency(T, n):
    E = S.edges_of(T)
    nb = [[] for _ in range(n)]
    for a, b in E: nb[a].append(b); nb[b].append(a)
    return E, nb


def graph_distance(V, T, sources, maxd=1e9):
    """Dijkstra over mesh edges from the source vertex ids (list) -> per-vertex distance."""
    n = len(V); E, nb = adjacency(T, n)
    d = np.full(n, np.inf); h = []
    for s in sources: d[s] = 0; heapq.heappush(h, (0.0, int(s)))
    while h:
        dd, u = heapq.heappop(h)
        if dd > d[u]: continue
        if dd > maxd: break
        for w in nb[u]:
            nd = dd + float(np.linalg.norm(V[u] - V[w]))
            if nd < d[w]: d[w] = nd; heapq.heappush(h, (nd, w))
    return d


def face_areas(V, T):
    return .5 * np.linalg.norm(np.cross(V[T[:, 1]] - V[T[:, 0]], V[T[:, 2]] - V[T[:, 0]]), axis=1)


def laplace_smooth(V, T, fixed, lam=.5, iters=3):
    E = S.edges_of(T); n = len(V); deg = np.bincount(E.ravel(), minlength=n).astype(float)
    for _ in range(iters):
        acc = np.zeros_like(V)
        np.add.at(acc, E[:, 0], V[E[:, 1]]); np.add.at(acc, E[:, 1], V[E[:, 0]])
        avg = acc / np.maximum(deg, 1)[:, None]
        step = lam * (avg - V); step[fixed] = 0
        V = V + step
    return V
