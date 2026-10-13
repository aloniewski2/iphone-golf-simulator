"""Fit a designed garment to the body without copying it: only where the garment comes closer than `gap` to the skin (or goes inside) is it
moved, out of the body along the skin's normal, by a SMOOTH field spread over the cloth (so no muscle or rib shows through)."""
import numpy as np
import glib as G


def signed(body, x):
    cp, fi, _ = body.nearest(x)
    bc = body.bary(cp, fi); n = body.smooth_normal_at(fi, bc)
    d = np.einsum('ij,ij->i', x - cp, n)
    return d, n


def push_clear(body, pos, tri, gap=.005, passes=5, spread=5, smooth=12, cap=.03):
    x = pos.copy()
    nb = G.neighbours(len(x), tri); E = G.unique_edges(tri)
    for it in range(passes):
        d, n = signed(body, x)
        need = np.clip(gap - d, 0, cap)
        if need.max() < 2e-4: break
        f = need.copy()
        for _ in range(spread):
            m = f.copy(); np.maximum.at(m, E[:, 0], .8 * f[E[:, 1]]); np.maximum.at(m, E[:, 1], .8 * f[E[:, 0]]); f = m
        for _ in range(smooth):
            f = np.maximum(need, f + .5 * G.umbrella(f[:, None], nb)[:, 0])
        # direction: the skin's normal, smoothed over the cloth
        dirn = n * (f > 0)[:, None]
        for _ in range(4): dirn = dirn + .5 * G.umbrella(dirn, nb)
        dirn /= np.linalg.norm(dirn, axis=1, keepdims=True) + 1e-12
        x = x + dirn * f[:, None]
    d, _ = signed(body, x)
    return x, d


class MeshSurface:
    """another garment as an obstacle (signed distance by face normals, like Body.nearest)."""
    def __init__(self, pos, tri):
        from mathutils.bvhtree import BVHTree
        self.pos = pos; self.tri = tri
        self.tree = BVHTree.FromPolygons([tuple(p) for p in pos], [tuple(t) for t in tri], all_triangles=True)
        fn = G.face_normals(pos, tri); self.fn = fn / (np.linalg.norm(fn, axis=1, keepdims=True) + 1e-12)
    def radial_need(self, x, axis_y, gap):
        """cast from the torso axis outward through x: if the obstacle's surface is beyond x, x must move out to it + gap."""
        from mathutils import Vector
        need = np.zeros(len(x))
        for i, p in enumerate(x):
            o = np.array([0, axis_y, p[2]]); dvec = p - o; L = np.linalg.norm(dvec)
            if L < 1e-6: continue
            dvec /= L
            r = self.tree.ray_cast(Vector(o + dvec * (L * .5)), Vector(dvec), .3)
            # the outermost hit along the ray
            far = None; oo = o + dvec * (L * .5)
            for _ in range(6):
                if r[0] is None: break
                far = np.array(r[0]); oo = far + dvec * 1e-4; r = self.tree.ray_cast(Vector(oo), Vector(dvec), .3)
            if far is None: continue
            need[i] = max(0.0, (far - o) @ dvec + gap - L)
        return need

    def signed(self, x):
        from mathutils import Vector
        d = np.full(len(x), 1.0); n = np.zeros_like(x)
        for i, p in enumerate(x):
            r = self.tree.find_nearest(Vector(p), .05)
            if r[0] is None: continue
            nn = self.fn[r[2]]; d[i] = np.dot(p - np.array(r[0]), nn); n[i] = nn
        return d, n


def push_clear_multi(body, pos, tri, gap=.005, others=(), passes=5, spread=5, smooth=12, cap=.03, axis_y=-.04):
    """push_clear against the body and against other garments worn under this one (gap_other each)."""
    x = pos.copy()
    nb = G.neighbours(len(x), tri); E = G.unique_edges(tri)
    for it in range(passes):
        d, n = signed(body, x)
        need = np.clip(gap - d, 0, cap); dirn = n * (need > 0)[:, None]
        for surf, g2, mask in others:
            nd2 = np.clip(surf.radial_need(x, axis_y, g2), 0, cap) * mask
            d2 = np.zeros(len(x))
            # garments underneath push the cloth straight out from the torso axis (sideways, never up or down)
            n2 = x - np.stack([np.zeros(len(x)), np.full(len(x), axis_y), x[:, 2]], 1); n2 /= np.linalg.norm(n2, axis=1, keepdims=True) + 1e-12

            take = nd2 > need
            need = np.where(take, nd2, need); dirn[take] = n2[take]
        if need.max() < 2e-4: break
        f = need.copy()
        for _ in range(spread):
            m = f.copy(); np.maximum.at(m, E[:, 0], .8 * f[E[:, 1]]); np.maximum.at(m, E[:, 1], .8 * f[E[:, 0]]); f = m
        for _ in range(smooth):
            f = np.maximum(need, f + .5 * G.umbrella(f[:, None], nb)[:, 0])
        dirn = dirn * (f > 0)[:, None]
        for _ in range(4): dirn = dirn + .5 * G.umbrella(dirn, nb)
        dirn /= np.linalg.norm(dirn, axis=1, keepdims=True) + 1e-12
        x = x + dirn * f[:, None]
    d, _ = signed(body, x)
    return x, d
