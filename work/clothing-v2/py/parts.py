"""Construction parts built along garment borders and surfaces: polo collar, placket, buttons, waistbands, belt loops, belts.
Every part is a Garment strip/panel; solidify() gives it real thickness."""
import math
import numpy as np
import glib as G
from garment import Garment, PIQUE, RIB, TWILL, LEATHER
from mathutils import Vector
from mathutils.bvhtree import BVHTree


def bvh(pos, tri):
    return BVHTree.FromPolygons([tuple(p) for p in pos], [tuple(t) for t in tri], all_triangles=True)


def push_out(tree, pts, clear, normals_hint=None):
    """move points to at least `clear` in front of the surface in `tree` (along the surface normal)."""
    out = pts.copy()
    for i, p in enumerate(pts):
        r = tree.find_nearest(Vector(p))
        if r[0] is None: continue
        c = np.array(r[0]); n = np.array(r[1])
        d = np.dot(p - c, n)
        c_ = clear[i] if np.ndim(clear) else clear
        if d < c_: out[i] = c + n * c_
    return out


def project(tree, pts, offset=0.0):
    out = pts.copy(); nn = np.zeros_like(pts)
    for i, p in enumerate(pts):
        r = tree.find_nearest(Vector(p))
        if r[0] is None: continue
        n = np.array(r[1]); out[i] = np.array(r[0]) + n * offset; nn[i] = n
    return out, nn


def resample_polyline(p, n, closed=False):
    q = np.concatenate([p, p[:1]]) if closed else p
    seg = np.linalg.norm(np.diff(q, axis=0), axis=1); s = np.concatenate([[0], np.cumsum(seg)])
    t = np.linspace(0, s[-1], n + (1 if closed else 0))[: n] if closed else np.linspace(0, s[-1], n)
    return np.stack([np.interp(t, s, q[:, k]) for k in range(3)], 1), s[-1]


def grid_tri(ni, nj, closed_i=False):
    """triangles of an ni x nj vertex grid indexed [i*nj + j]."""
    out = []
    mi = ni if closed_i else ni - 1
    for i in range(mi):
        i2 = (i + 1) % ni
        for j in range(nj - 1):
            a, b, c, d = i * nj + j, i2 * nj + j, i2 * nj + j + 1, i * nj + j + 1
            out.append((a, b, c)); out.append((a, c, d))
    return np.array(out, np.int64)


def orient_outward(pos, tri, centre_fn):
    """flip the triangle set so its normals point away from centre_fn(face centroid)."""
    fn = G.face_normals(pos, tri); c = pos[tri].mean(1)
    if np.mean(np.einsum('ij,ij->i', fn, c - centre_fn(c))) < 0: tri = tri[:, ::-1]
    return tri


def polo_collar(body, shell, neck_loop, P, front_dir=np.array([0, -1., 0])):
    """Classic polo collar along the neckline loop (indices into shell.pos), open at centre front for the placket.
    Rows: stand (inside the neckline, rising up the neck) -> rolled fold -> fall lying back down over the stand and the shoulders."""
    loop = shell.pos[neck_loop]
    c = loop.mean(0)
    up = np.array([0, 0, 1.])
    ang = np.arctan2(loop[:, 0] - c[0], -(loop[:, 1] - c[1]))           # 0 = centre front, +- pi = back
    order = np.argsort(ang)
    loop = loop[order]; ang = ang[order]
    gap = P['collar_gap']                                                 # half the opening at the front (metres along the curve)
    # keep the part of the loop beyond the front gap on each side: walk by arc length from the front
    q, total = resample_polyline(np.concatenate([loop, loop[:1]]), 400)
    a = np.arctan2(q[:, 0] - c[0], -(q[:, 1] - c[1]))
    # arc length from the front point (a ~ 0) both ways
    seg = np.linalg.norm(np.diff(q, axis=0), axis=1); s = np.concatenate([[0], np.cumsum(seg)])
    i_front = np.argmin(np.abs(a))
    sf = s[i_front]
    # parameter along the open collar from the left end (hero's left: x < 0, a < 0) around the back to the right end
    L = total
    u = (s - sf) % L                                                      # 0 at front, increasing toward +a
    keep = (u > gap) & (u < L - gap)
    pts = q[keep]; uu = u[keep]
    o = np.argsort(uu); pts = pts[o]
    N = P.get('collar_n', 90)
    base, _ = resample_polyline(pts, N)
    t = np.linspace(0, 1, N)                                              # 0 = right end?, 1 = other end
    a_b = np.arctan2(base[:, 0] - c[0], -(base[:, 1] - c[1]))
    back = (1 - np.cos(a_b)) * .5                                         # 1 at the back, 0 at the front
    hs = P['stand_front'] + (P['stand_back'] - P['stand_front']) * back   # stand height
    wf = P['fall_front'] + (P['fall_back'] - P['fall_front']) * back      # fall width
    # collar points: the fall widens and drops at the two ends
    endness = np.clip(1 - np.minimum(t, 1 - t) / .12, 0, 1) ** 2
    wf = wf + P['point_extra'] * endness
    radial = base - c; radial[:, 2] = 0; radial /= np.linalg.norm(radial, axis=1, keepdims=True)
    th = P['thickness']
    neck_clear = th * 1.5 + .002
    tree_body = body.bvh
    # stand: from 6 mm below the neckline (tucked inside the shirt) up to hs
    rows = []
    for v in np.linspace(-.006, 1.0, 5):
        h = np.where(v < 0, v, v * hs)
        p = base + up * h[:, None] - radial * (.0015 * float(np.clip(v, 0, 1)))   # leans in a touch
        rows.append(p)
    rows = [push_out(tree_body, r, neck_clear) for r in rows]
    top = rows[-1]
    # fold: half circle of radius th*1.6 rolling outward
    rf = th * 1.2
    for k in range(1, 5):
        phi = math.pi * k / 4
        rows.append(top + radial * (rf * (1 - math.cos(phi))) + up * (rf * math.sin(phi) * .6))
    fold_out = rows[-1]
    # fall: runs back down outside the stand, then over the shoulders, lying on the shell (offset th + 1 mm)
    shell_tree = bvh(shell.pos, shell.tri)
    n_fall = 7
    prev = fold_out
    lie = th * 1.2 + .0012
    for k in range(1, n_fall + 1):
        w = wf * k / n_fall
        # descend along the stand first, then out along the shoulder slope
        drop = np.minimum(w, hs + rf)
        out = np.maximum(0, w - (hs + rf))
        p = fold_out - up * drop[:, None] + radial * (out * .92)[:, None] - up * (out * .38)[:, None]
        p = push_out(tree_body, p, neck_clear + 2 * th)
        p = push_out(shell_tree, p, lie)
        rows.append(p)
    R = np.stack(rows, 1)                                                 # (N, nj, 3)
    nj = R.shape[1]
    pos = R.reshape(-1, 3)
    tri = grid_tri(N, nj)
    g = Garment(pos, tri, fabric=RIB, roles=('Kit_Shirt',), kind='collar')
    jj = np.tile(np.arange(nj), N).astype(np.float64)
    # distance from the fall's free edge along the profile (metres) for tipping stripes
    prof = np.concatenate([np.zeros((N, 1)), np.cumsum(np.linalg.norm(np.diff(R, axis=1), axis=2), axis=1)], 1)
    edge = (prof[:, -1:] - prof).reshape(-1)
    g.A['collar_edge'] = edge
    g.A['collar_u'] = np.repeat(t, nj)
    arc = np.concatenate([[0], np.cumsum(np.linalg.norm(np.diff(base, axis=0), axis=1))])
    g.A['pu'] = np.repeat(arc, nj); g.A['pv'] = prof.reshape(-1)
    g.F['proj'][:] = 6
    tri = orient_outward(pos, tri, lambda q: np.tile(c, (len(q), 1)) - 0)   # stand normals face away from the neck
    g.tri = tri
    return g


def placket(shell, P, front_x=0.0):
    """Raised button placket on the shell's centre front, from the neckline down P['placket_len']."""
    tree = bvh(shell.pos, shell.tri)
    z_top = P['placket_top']; z_bot = z_top - P['placket_len']; w = P['placket_w']
    nz, nx = 28, 7
    zs = np.linspace(z_bot, z_top, nz); xs = np.linspace(-w / 2, w / 2, nx)
    # find the front surface: cast from the front
    pts = []
    for z in zs:
        for x in xs:
            r = tree.ray_cast(Vector((front_x + x, -1.0, z)), Vector((0, 1, 0)))
            pts.append(np.array(r[0]) if r[0] is not None else np.array([x, -.15, z]))
    pts = np.array(pts)
    pos, nn = project(tree, pts, P['placket_lift'])
    tri = grid_tri(nz, nx)
    tri = orient_outward(pos, tri, lambda q: q - nn[0] * 0 + np.array([0, .2, 0]))
    g = Garment(pos, tri, fabric=PIQUE, roles=('Kit_Shirt',), kind='placket')
    g.A['pk_x'] = np.tile(xs, nz); g.A['pk_z'] = np.repeat(zs - z_bot, nx)
    g.A['pu'] = g.A['pk_x'].copy(); g.A['pv'] = g.A['pk_z'].copy(); g.F['proj'][:] = 6
    return g


def button(centre, normal, radius=.0055, thick=.0022, n=20):
    """small rounded disc with a dished face (two holes are painted into the cavity map)."""
    normal = normal / np.linalg.norm(normal)
    a = np.cross(normal, [0, 0, 1.]); a /= np.linalg.norm(a); b = np.cross(normal, a)
    rings = [(0.0, thick * .55), (.45, thick * .45), (.8, thick * .75), (1.0, thick * .5), (1.0, 0.0)]
    pos = [centre + normal * thick * .55]
    for rr, hh in rings[1:]:
        for k in range(n):
            ph = 2 * math.pi * k / n
            pos.append(centre + (a * math.cos(ph) + b * math.sin(ph)) * radius * rr + normal * hh)
    pos = np.array(pos)
    tri = []
    for k in range(n): tri.append((0, 1 + k, 1 + (k + 1) % n))
    for r in range(len(rings) - 2):
        o0 = 1 + r * n; o1 = 1 + (r + 1) * n
        for k in range(n):
            k2 = (k + 1) % n
            tri.append((o0 + k, o1 + k, o1 + k2)); tri.append((o0 + k, o1 + k2, o0 + k2))
    tri = np.array(tri, np.int64)
    tri = orient_outward(pos, tri, lambda q: np.tile(centre - normal * .01, (len(q), 1)))
    g = Garment(pos, tri, fabric=PIQUE, roles=('Kit_Shirt',), kind='button')
    g.A['button'] = np.ones(len(pos))
    return g
