"""Jaw sculpt (2026-10-12): baked into the Body mesh in Blender. Vertex count/order, weights, UVs, shape-key deltas unchanged.

Brush-style operations, all in Blender object space (metres, -Y = face forward, +Z up), on the welded surface:
  tuck   - the neck under the jaw moves in toward the neck axis (front = back, sides = in), only below the under-jaw surface and only
           above the collar line (zero at the collar, full `ramp` metres above it);
  flat   - the under-jaw: points just below the under-jaw surface are lifted onto it (a flatten brush on that surface);
  chin   - the chin pad moves forward (strongest low on the chin, fading before the lower lip);
  edge   - a small crest right on the jaw border (out and down), so the border reads as an edge;
then the displacement field (not the positions) is smoothed over the surface. Vertices below the collar line, around the mouth and
far from the jaw are held at zero during the smoothing.

The under-jaw surface: seen from below, the jaw border is a U (half-ellipse) from the chin bottom m=(0, ym, zm) to the jaw angles
g=(+-xg, yg, zg). Inside the U the surface height rises from zm (chin) to zg (angle): z_u = zm + (zg-zm) * (1-cos(phi))**rise_p.
The original basis + shape keys are snapshotted to work/jaw-sculpt-2026-10-12/source_<sex>.npz on the first call; every call
recomputes from that snapshot, so iterations never accumulate.
"""
import bpy, numpy as np, math, json, os
import jawtools as J


def collar_fn(line):
    a = np.array(sorted(int(k) for k in line)); v = np.array([line[k] if k in line else line[str(k)] for k in a])
    return lambda theta: np.interp(theta, a, v)


def snapshot(sex):
    path = f'{J.WORK}/source_{sex}.npz'
    me = bpy.data.objects['Body'].data
    if not os.path.exists(path):
        keys = {k.name: J.coords(me, k.name) for k in me.shape_keys.key_blocks}
        np.savez(path, basis=J.coords(me), **{'key_' + k: v for k, v in keys.items()})
    d = np.load(path)
    assert len(d['basis']) == len(me.vertices)
    return d['basis'], {k[4:]: d[k] for k in d.files if k.startswith('key_')}


def fields(P, N, prm, collar):
    x, y, z = P[:, 0], P[:, 1], P[:, 2]
    ax = np.abs(x)
    ym, zm = prm['menton']; xg, yg, zg = prm['gonion']
    ay = yg - ym
    u = ax / xg; v = (yg - y) / ay
    e = np.sqrt(u * u + v * v)
    phi = np.where(v <= 0, math.pi / 2, np.arctan2(u, np.maximum(v, 1e-9)))
    zu = zm + (zg - zm) * (1 - np.cos(phi)) ** prm['rise_p'] + prm.get('shelf_slope', 0) * np.clip(y - ym, 0, None) * np.cos(phi)
    h = zu - z                                                  # > 0 : below the under-jaw surface
    inside = 1 - J.smooth(prm['e_in'], prm['e_out'], e)          # under the jaw, inside the U
    cy = prm['neck_y']
    ry = y - cy; r = np.hypot(x, ry)
    theta = np.degrees(np.arctan2(ax, -ry))                     # 0 front, 90 side, 180 back
    cz = collar(theta) + prm['collar_gap']
    front = np.where(theta < 90, np.cos(np.radians(theta)) ** 2, 0)
    ramp = prm['ramp_side'] + (prm['ramp_front'] - prm['ramp_side']) * front
    vc = J.smooth(cz, cz + ramp, z)
    # 1. tuck
    side = np.sin(np.radians(theta)) ** 2 * (1 - J.smooth(prm['back_fade'][0], prm['back_fade'][1], theta))
    a0, hA, hB = prm.get('tuck_profile', (1.0, 0.0, 0.01))     # front tuck: fraction a0 right under the jaw, full from hA..hB down
    amt = prm['tuck_front'] * front * (a0 + (1 - a0) * J.smooth(hA, hB, h)) + prm['tuck_side'] * side
    below = J.smooth(prm['tuck_h'][0], prm['tuck_h'][1], h)
    neck_r = 1 - J.smooth(prm['neck_rmax'][0], prm['neck_rmax'][1], r)   # the neck column only
    t = amt * below * vc * neck_r
    D = np.zeros_like(P)
    rh = np.stack([x, ry, np.zeros_like(r)], 1) / np.maximum(r, 1e-6)[:, None]
    D -= rh * t[:, None]
    # 2. flatten the under-jaw onto z_u: the band just below it, inside the U, in front of the neck axis
    lift = (1 - J.smooth(prm['flat_h'][0], prm['flat_h'][1], h)) * (h > 0) * inside * J.smooth(prm['flat_front'][0], prm['flat_front'][1], -ry) * vc
    lift = lift * J.smooth(prm.get('flat_nz', (.30, .70))[0], prm.get('flat_nz', (.30, .70))[1], -N[:, 2])   # the underside only
    D[:, 2] += np.clip(h, 0, None) * lift * prm['flat_k']
    # 3. chin forward (low on the chin, front-facing)
    zc0, zc1 = prm['chin_z']
    chin = np.exp(-(x / prm['chin_wx']) ** 2) * J.smooth(zm - .003, zm + .004, z) * (1 - J.smooth(zc0, zc1, z)) * J.smooth(.15, .55, -N[:, 1])
    chin *= J.smooth(-prm['chin_y'], -prm['chin_y'] + .012, -y)
    D[:, 1] -= prm['chin_push'] * chin
    # 4. a crest on the border (out and down along the corner bisector)
    if prm.get('edge', 0):
        out = np.stack([np.sign(x) * np.sin(phi), -np.cos(phi), np.zeros_like(x)], 1)
        bis = out + np.array([0, 0, -1.0]); bis /= np.linalg.norm(bis, axis=1)[:, None]
        band = np.exp(-((h + prm['edge_h']) / prm['edge_w']) ** 2) * J.smooth(.80, .97, e) * (1 - J.smooth(1.05, 1.15, e)) * vc
        D += bis * (prm['edge'] * band)[:, None]
    return D, dict(h=h, e=e, theta=theta, vc=vc, t=t, lift=lift, chin=chin, cz=cz)


def smooth_field(D, edges, n, iters, lam, fixed):
    """Laplacian smoothing of the displacement field over the surface graph; `fixed` vertices stay at zero."""
    i, j = edges[:, 0], edges[:, 1]
    deg = np.bincount(i, minlength=n) + np.bincount(j, minlength=n)
    D = D.copy(); D[fixed] = 0
    for _ in range(iters):
        acc = np.zeros_like(D)
        np.add.at(acc, i, D[j]); np.add.at(acc, j, D[i])
        D = D + lam * (acc / np.maximum(deg, 1)[:, None] - D)
        D[fixed] = 0
    return D


def compute(sex, prm):
    base, keys = snapshot(sex)
    me = bpy.data.objects['Body'].data
    idx, tri, edges, nu = J.topology_at(base, me)
    P = np.zeros((nu, 3)); P[idx] = base
    N = J.vertex_normals(P, tri)
    collar = collar_fn(prm['collar'])
    D, info = fields(P, N, prm, collar)
    x, y, z = P.T
    support = J.dilate(edges, nu, np.linalg.norm(D, axis=1) > 1e-7, prm['smooth_ring'])
    lip = (z > prm['lip_guard']) & (np.abs(x) < .055) & (y < prm['menton'][0] + .045)
    fixed = ~support | (z <= info['cz']) | lip
    D = smooth_field(D, edges, nu, prm['smooth_iters'], prm['smooth_lam'], fixed)
    return base, keys, idx, P, D, info


def apply(sex, prm):
    base, keys, idx, P, D, info = compute(sex, prm)
    me = bpy.data.objects['Body'].data
    Dv = D[idx]
    for name, k in keys.items():
        J.set_coords(me, k + Dv, name)
    J.set_coords(me, base + Dv)
    me.update()
    m = np.linalg.norm(Dv, axis=1)
    return dict(moved=int((m > 1e-6).sum()), max_mm=round(float(m.max() * 1000), 2),
                zmin_moved=round(float(base[m > 1e-6][:, 2].min()), 4) if (m > 1e-6).any() else None,
                below_collar=int(((m > 1e-6) & (base[:, 2] <= info['cz'][idx])).sum()))


def reset(sex):
    base, keys = snapshot(sex)
    me = bpy.data.objects['Body'].data
    for name, k in keys.items(): J.set_coords(me, k, name)
    J.set_coords(me, base); me.update()
