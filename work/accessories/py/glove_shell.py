"""GLOVE: turn the inner surface S_in (V, T, cuff loop) into a real leather shell: outer = S_in + n*t(s), a rounded rolled rim around the cuff edge, inner = S_in.
Everything numpy: returns a dict part = dict(pos, tris, zone, kind, fabric, sub, fields{...}) ready for glove_parts.to_object()."""
import os, sys, math
sys.path.insert(0, os.path.dirname(__file__))
import numpy as np
import glove_sdf as S, glove_mesh as M

K_SHELL, K_LINING, K_STRAP, K_PATCH, K_HOOK = range(5)
FAB = dict(twill=6, pique=7, leather=8, rib=9, plastic=4)
T_BASE = 0.0014            # leather shell thickness (inner -> outer)
T_HEM = 0.0027             # thickness of the rolled cuff edge
HEM_LEN = 0.0105           # the roll fades into the plain shell over this distance


def smooth01(x): x = np.clip(x, 0, 1); return x * x * (3 - 2 * x)


def thickness_field(dcuff):
    return T_BASE + (T_HEM - T_BASE) * smooth01(1 - dcuff / HEM_LEN)


def vertex_normals_smooth(V, T, iters=1):
    n = S.vertex_normals(V, T)
    E = S.edges_of(T)
    for _ in range(iters):
        acc = n.copy()
        np.add.at(acc, E[:, 0], n[E[:, 1]]); np.add.at(acc, E[:, 1], n[E[:, 0]])
        n = S.unit(acc)
    return n


def assemble_shell(V, T, loop, edge_steps=6, nrm=None, extra_fields=None, inner_faces=None, inner_g=None, thick_scale=None):
    """V, T: inner surface (triangles outward-facing), loop: ordered vertex ids of the single cuff boundary loop. Returns the part dict."""
    n = len(V)
    dcuff = M.graph_distance(V, T, loop)
    nrm = vertex_normals_smooth(V, T) if nrm is None else nrm
    thick = thickness_field(dcuff)
    if thick_scale is not None: thick = thick * thick_scale            # thinner where the fitted surface is creased (an offset there would turn inside out)
    O = V + nrm * thick[:, None]
    # rim ring: outward tangent at the boundary = away from the interior neighbours
    E, nb = M.adjacency(T, n)
    bset = set(int(v) for v in loop)
    tang = np.zeros((len(loop), 3))
    for i, v in enumerate(loop):
        inn = [w for w in nb[int(v)] if w not in bset]
        c = V[inn].mean(0) if inn else V[v] + np.array([0, 1e-3, 0])
        d = V[v] - c
        d -= nrm[v] * (d @ nrm[v])
        tang[i] = d
    tang = S.unit(tang)
    k = len(loop)
    a = (O[loop] - V[loop]) * .5; ctr = (O[loop] + V[loop]) * .5
    bvec = tang * np.linalg.norm(a, axis=1, keepdims=True)
    rows = []
    for s in range(1, edge_steps):
        th = math.pi * s / edge_steps
        rows.append(ctr + a * math.cos(th) + bvec * math.sin(th))
    # vertex layout: outer [0,n), inner [n,2n), rim rows after. The lining rises to meet the outer wall where it ends (no ledge inside the cuff).
    Iv = V if inner_g is None else V + nrm * (thick * inner_g)[:, None]
    pos = [O, Iv]
    base = 2 * n
    rim_ids = [np.asarray(loop)]
    for r in rows:
        pos.append(r); rim_ids.append(base + np.arange(k)); base += k
    rim_ids.append(n + np.asarray(loop))
    P = np.concatenate(pos)
    if inner_faces is None: inner_faces = np.ones(len(T), bool)
    Ti = T[inner_faces]
    tris = [T, Ti[:, ::-1] + n]
    for s in range(len(rim_ids) - 1):
        a_, b_ = rim_ids[s], rim_ids[s + 1]
        a2, b2 = np.roll(a_, -1), np.roll(b_, -1)
        tris.append(np.stack([a_, b2, a2], 1)); tris.append(np.stack([a_, b_, b2], 1))
    tris = np.concatenate(tris)
    zone = np.concatenate([np.zeros(len(T), np.int32), np.full(len(Ti), 2, np.int32), np.ones(len(tris) - len(T) - len(Ti), np.int32)])
    fabric = np.where(zone == 2, FAB['pique'], FAB['leather']).astype(np.int32)
    kind = np.where(zone == 2, K_LINING, K_SHELL).astype(np.int32)
    # per-vertex fields
    dc = np.concatenate([dcuff, dcuff] + [np.zeros(k) - 0.0] * (edge_steps - 1))
    # rim rows: 0 at the edge crest, negative toward the outside is not needed; keep their own parameter
    ring_t = np.concatenate([np.zeros(2 * n)] + [np.full(k, s / edge_steps) for s in range(1, edge_steps)])
    fields = dict(dcuff=dc, rim_t=ring_t)
    # compact: the lining only exists where inner_faces says so, drop the unused inner vertices (outer vertices keep their ids 0..n-1)
    used = np.unique(tris); remap = -np.ones(len(P), np.int64); remap[used] = np.arange(len(used))
    P = P[used]; tris = remap[tris]
    fields = {k_: v[used] for k_, v in fields.items()}
    return dict(pos=P, tris=tris, zone=zone, kind=kind, fabric=fabric, sub=np.zeros(len(tris), np.int32), fields=fields, used=used,
                n_inner=n, n_outer=n, loop=np.asarray(loop), thick=thick, nrm=nrm, O=O, V=V, T=T, dcuff=dcuff, rim_rows=edge_steps - 1)
