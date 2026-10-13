"""Jaw sculpt (2026-10-12): baked into the Body mesh in Blender. Vertex count/order, weights, UVs and shape-key deltas unchanged.

Works in polar coordinates around the neck axis (x = 0, y = neck_y; theta 0 = front, 90 = side; r = horizontal distance).
1. The jaw border is traced on the mesh itself: the band where the surface turns from facing sideways to facing down (n_z ~ -0.45),
   lowest half per theta bin, from the chin (theta 0) back to the jaw angle (theta ~ border_end). That gives r_B(theta), z_B(theta).
2. The neck radius r_N(theta) is the median radius of the neck surface 12-35 mm below the underside (and above the collar).
3. Brush-style operations (each one a smooth field, then the displacement field is smoothed over the surface):
   flat  - the under-jaw: vertices between the neck and the underside's outer edge that hang below the under-jaw plane
           z_P(r) = z_A - tan(shelf_deg) * (r_A - r) are lifted onto it (a flatten brush); (r_A, z_A) = median of the truly
           down-facing underside (n_z < -0.8) in the outer 15 mm, per theta (the chin's own underside at the front);
   tuck  - the neck column (below the border) moves in toward the neck axis: `tuck_front` at the throat, `tuck_side` at the sides,
           only above the collar line (zero at the collar, full `ramp` above it) and never past theta `back_fade`;
   edge  - a small crest on the border itself (out and down along the corner bisector), so it reads as an edge;
   chin  - the chin pad moves forward (low on the chin, front-facing, fading before the lower lip).
The source basis + shape keys are snapshotted to source_<sex>.npz on the first call; each call recomputes from it.
(sculpt_v1_ellipse.py = the first try, with an assumed elliptical border; it sat 5-10 mm too high on the cheeks.)
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


def polar(P, cy):
    ax = np.abs(P[:, 0]); ry = P[:, 1] - cy
    return np.hypot(ax, ry), np.degrees(np.arctan2(ax, -ry))


def trace_border(P, N, prm):
    """r_B, z_B per theta bin."""
    r, th = polar(P, prm['neck_y']); z = P[:, 2]
    z0, z1 = prm['border_z']
    reg = (z > z0) & (z < z1) & (r < prm['border_rmax'])
    reg &= ~((z > prm['chin_z'][1] - .005) & (np.abs(P[:, 0]) < .045))   # not the lower lip's underside
    bn = np.exp(-((N[:, 2] - prm.get('border_nz', -.45)) / .15) ** 2) * reg
    bins = np.arange(0, prm['border_end'] + .1, 5.0)
    rb, zb = [], []
    for a in bins:
        k = (np.abs(th - a) < 3.5) & (bn > .3)
        if k.sum() < 3: rb.append(np.nan); zb.append(np.nan); continue
        sel = z[k] <= np.percentile(z[k], 50)
        w = bn[k][sel]
        rb.append(np.average(r[k][sel], weights=w)); zb.append(np.average(z[k][sel], weights=w))
    rb = np.array(rb); zb = np.array(zb); good = ~np.isnan(rb)
    rb = np.interp(bins, bins[good], rb[good]); zb = np.interp(bins, bins[good], zb[good])
    k = np.array([.25, .5, .25])
    for _ in range(2):
        rb[1:-1] = np.convolve(rb, k, 'valid'); zb[1:-1] = np.convolve(zb, k, 'valid')
    return bins, rb, zb


def underside_anchor(P, N, prm, bins, rb7):
    """Per theta: median height / radius of the truly down-facing jaw underside (n_z < -0.8) in the outer 15 mm inside the border."""
    r, th = polar(P, prm['neck_y']); z = P[:, 2]
    z0, z1 = prm['border_z']
    zr, rr = [], []
    for a, R in zip(bins, rb7):
        k = (np.abs(th - a) < 4) & (N[:, 2] < -.8) & (r > R - .015) & (r < R + .002) & (z > z0) & (z < z1)
        zr.append(np.median(z[k]) if k.sum() >= 3 else np.nan); rr.append(np.median(r[k]) if k.sum() >= 3 else np.nan)
    zr = np.array(zr); rr = np.array(rr); good = ~np.isnan(zr)
    return np.interp(bins, bins[good], zr[good]), np.interp(bins, bins[good], rr[good])


def neck_radius(P, prm, bins, zb, collar):
    r, th = polar(P, prm['neck_y']); z = P[:, 2]
    rn = []
    for a, zz in zip(bins, zb):
        lo = max(zz - .035, collar(a) + .004); hi = zz - .012
        k = (np.abs(th - a) < 4) & (z > lo) & (z < hi) & (r < prm['border_rmax'])
        rn.append(np.median(r[k]) if k.sum() >= 3 else np.nan)
    rn = np.array(rn); good = ~np.isnan(rn)
    return np.interp(bins, bins[good], rn[good])


def fields(P, N, prm, collar):
    x, y, z = P.T
    r, th = polar(P, prm['neck_y'])
    bins, rb, zb = trace_border(P, N, prm)
    _, rb7, zb7 = trace_border(P, N, dict(prm, border_nz=-.7))
    za, ra = underside_anchor(P, N, prm, bins, rb7)
    rn = neck_radius(P, prm, bins, za, collar)
    end = prm['border_end']
    RB = np.interp(th, bins, rb); ZB = np.interp(th, bins, zb); RN = np.interp(th, bins, rn)
    ZA = np.interp(th, bins, za); RA = np.interp(th, bins, ra)
    jaw = 1 - J.smooth(end - prm['border_fade'], end, th)
    cz = collar(th) + prm['collar_gap']
    front = np.where(th < 90, np.cos(np.radians(th)) ** 2, 0)
    ramp = prm['ramp_side'] + (prm['ramp_front'] - prm['ramp_side']) * front
    vc = J.smooth(cz, cz + ramp, z)
    below_border = ZA - z                                       # below the underside (the chin's own underside at the front)
    D = np.zeros_like(P)
    # flatten: between the neck and the border, under the plane
    s = J.smooth(RN + prm['shelf_from'][0], RN + prm['shelf_from'][1], r) * (1 - J.smooth(RA - .001, RA + .006, r))
    zP = ZA - math.tan(math.radians(prm['shelf_deg'])) * (RA - r)
    hang = np.clip(zP - z, 0, None)
    # positional weights only (a per-vertex normal test crumples bumpy skin): deep points are neck, not under-jaw
    lift = s * jaw * vc * (1 - J.smooth(prm['flat_max'] * .5, prm['flat_max'], hang))
    D[:, 2] += hang * lift * prm['flat_k']
    # tuck the neck column below the border
    a0, hA, hB = prm.get('tuck_profile', (1.0, 0.0, 0.01))
    side = np.sin(np.radians(th)) ** 2 * (1 - J.smooth(prm['back_fade'][0], prm['back_fade'][1], th))
    amt = prm['tuck_front'] * front * (a0 + (1 - a0) * J.smooth(hA, hB, below_border)) + prm['tuck_side'] * side
    neckness = 1 - J.smooth(RN + prm['tuck_edge'][0], RN + prm['tuck_edge'][1], r)
    # past the jaw angle the neck side is tucked only below `side_top_z` (never the head side under the ear)
    below_side = prm['side_top_z'] - z
    wj = J.smooth(20, 40, th)                                 # front: the chin's underside; jaw sides: the lower of the two traces
    below_jaw = ZA * (1 - wj) + np.minimum(ZA, ZB) * wj - z
    hf = prm.get('tuck_h_front', prm['tuck_h'])                   # the throat starts right under the shelf; the sides fade in longer
    h0 = hf[0] * front + prm['tuck_h'][0] * (1 - front); h1 = hf[1] * front + prm['tuck_h'][1] * (1 - front)
    under = J.smooth(h0, h1, jaw * below_jaw + (1 - jaw) * below_side)
    t = amt * neckness * under * vc
    rh = np.stack([x, y - prm['neck_y'], np.zeros_like(r)], 1) / np.maximum(r, 1e-6)[:, None]
    D -= rh * t[:, None]
    # crest on the border
    if prm.get('edge', 0):
        bis = rh + np.array([0, 0, -1.0]); bis /= np.linalg.norm(bis, axis=1)[:, None]
        band = np.exp(-((z - ZB - prm['edge_dz']) / prm['edge_w']) ** 2) * np.exp(-((r - RB) / prm['edge_w']) ** 2) * jaw * vc
        band *= J.smooth(prm.get('edge_from', (15, 30))[0], prm.get('edge_from', (15, 30))[1], th)   # the jaw sides, not under the chin
        D += bis * (prm['edge'] * band)[:, None]
    # chin forward
    zc0, zc1 = prm['chin_z']; zm = prm['chin_bottom']
    chin = np.exp(-(x / prm['chin_wx']) ** 2) * J.smooth(zm - .003, zm + .004, z) * (1 - J.smooth(zc0, zc1, z))
    chin *= J.smooth(-prm['chin_y'], -prm['chin_y'] + .012, -y)
    D[:, 1] -= prm['chin_push'] * chin
    return D, dict(th=th, r=r, RB=RB, ZB=ZB, RN=RN, ZA=ZA, RA=RA, s=s, lift=lift, t=t, vc=vc, cz=cz, border=(bins, rb, zb, rn, ra, za), chin=chin)


def smooth_field(D, edges, n, iters, lam, fixed, taubin=True):
    """Laplacian smoothing of the displacement field over the surface graph; `fixed` vertices stay at zero."""
    i, j = edges[:, 0], edges[:, 1]
    deg = np.bincount(i, minlength=n) + np.bincount(j, minlength=n)
    D = D.copy(); D[fixed] = 0
    for it in range(iters):
        acc = np.zeros_like(D)
        np.add.at(acc, i, D[j]); np.add.at(acc, j, D[i])
        step = lam if (it % 2 == 0 or not taubin) else -(lam + .03)   # Taubin: shrink, then inflate back (no loss of amount)
        D = D + step * (acc / np.maximum(deg, 1)[:, None] - D)
        D[fixed] = 0
    return D


def compute(sex, prm):
    base, keys = snapshot(sex)
    me = bpy.data.objects['Body'].data
    idx, tri, edges, nu = J.topology_at(base, me)
    P = np.zeros((nu, 3)); P[idx] = base
    P0 = P.copy(); D0 = np.zeros_like(P); align_info = None
    if prm.get('align'):                                        # stage 0: an edge loop exactly on the jaw line (slides only)
        protect = J.dilate(edges, nu, ear_guard(P0, prm) > .5, 3) if 'ear' in prm else np.zeros(nu, bool)
        protect |= P0[:, 2] < prm['align_zmin']
        D0, align_info = align_chain(P0, tri, edges, nu, prm, protect)
        P = P0 + D0
    N = J.vertex_normals(P, tri)
    collar = collar_fn(prm['collar'])
    D, info = fields(P, N, prm, collar)
    info['align'] = align_info
    x, y, z = P.T
    support = J.dilate(edges, nu, np.linalg.norm(D, axis=1) > 1e-7, prm['smooth_ring'])
    lip = (z > prm['lip_guard']) & (np.abs(x) < .055) & (y < prm['chin_y'] + .02)
    hold = (z <= info['cz']) | lip                             # never moves: at/below the collar, the mouth
    if 'ear' in prm:                                            # and the auricle (+2 rings): its sliver triangles crease
        hold |= J.dilate(edges, nu, ear_guard(P, prm) > .5, 2)
    fixed = ~support | hold
    D[fixed] = 0
    D = smooth_field(D, edges, nu, prm['smooth_iters'], prm['smooth_lam'], fixed)
    if prm.get('carve'):
        ear = ear_guard(P, prm)                                 # the auricle never moves (carve or smoothing)
        info['ear'] = ear
        D2, info['carve'] = carve(P + D, D, prm, collar)
        D2 *= (1 - ear)[:, None]; info['carve']['band'] *= (1 - ear)
        cfixed = (np.linalg.norm(D2, axis=1) < 1e-9) & ~J.dilate(edges, nu, np.linalg.norm(D2, axis=1) > 1e-9, 3)
        D2[hold] = 0
        D2 = smooth_field(D2, edges, nu, prm['carve_smooth'], .5, cfixed | hold)
        D = D + D2
        if prm.get('denoise', 0):                               # smooth brush over the jaw band: the base mesh's fine noise shows on a crisp edge
            Q = P + D; band = info['carve']['band']
            Qs = smooth_field(Q.copy(), edges, nu, prm['denoise'], .5, np.zeros(nu, bool))
            w = np.clip(band, 0, 1)[:, None]; w[hold] = 0
            D = D + (Qs - Q) * w
    D[hold] = 0
    if prm.get('line_relax'):
        D, info['line_relax'] = line_relax(P, D, prm, hold, tri, edges, nu)
    if prm.get('line_profile'):
        for _ in range(prm['profile_passes']):
            D, info['line_profile'] = line_profile(P, D, prm, hold)
    D0[hold] = 0
    Dt, info['crease_rounds'] = crease_guard(P0, D0 + D, tri, edges, nu, hold, prm.get('crease_deg', 58.0))   # vs the untouched source
    return base, keys, idx, P0, Dt, info


def line_relax(P, D, prm, hold, tri, edges, nu):
    """Third pass: one clean jawline. Vertices in a band either side of the jaw line (chin -> corner, corner -> lobe; |distance across|
    under `relax_band`, side of the face only) are smoothed ALONG the line, never across it: each moves toward the average of its mesh
    neighbours weighted by how well the connecting edge lines up with the line (|cos|^4), so bumps and dips along the line go while the
    cross-section (the sharp turn from the jaw side to the underside) stays. The part of the move that is across the line is kept in
    full, the part along it (spacing) at `relax_slide`. Taubin passes (shrink, then inflate back), so the line doesn't creep inward.
    The corner itself (within `relax_corner` of it) is left as is, so the turn under the ear stays sharp."""
    Q = P + D
    x, y, z = Q.T; ax = np.abs(x)
    c0 = np.array(prm['jl_chin']); g = np.array(prm['jl_gonion']); l = np.array(prm['jl_lobe'])
    yz = np.stack([y, z], 1)
    def seg(a, b):
        T = (b - a) / np.linalg.norm(b - a); Nn = np.array([T[1], -T[0]])          # Nn: below the lower line / behind the back line
        s = (yz - a) @ T; t = (yz - a) @ Nn
        return T, s, t, np.linalg.norm(b - a)
    T1, s1, t1, L1 = seg(c0, g); T2, s2, t2, L2 = seg(g, l)
    band = prm['relax_band']
    w1 = (1 - J.smooth(band * .5, band, np.abs(t1))) * J.smooth(-.004, .004, s1) * (1 - J.smooth(L1 - .002, L1 + .004, s1))
    w2 = (1 - J.smooth(band * .5, band, np.abs(t2))) * J.smooth(-.004, .002, s2) * (1 - J.smooth(L2 - .002, L2 + .004, s2))
    corner = J.smooth(prm['relax_corner'][0], prm['relax_corner'][1], np.linalg.norm(yz - g, axis=1))
    use1 = w1 >= w2
    wb = np.where(use1, w1, w2) * corner * J.smooth(prm['carve_lat'][0], prm['carve_lat'][1], ax)
    wb[hold] = 0
    Tyz = np.where(use1[:, None], T1, T2)
    i, j = np.r_[edges[:, 0], edges[:, 1]], np.r_[edges[:, 1], edges[:, 0]]
    for it in range(prm['relax_iters']):
        Q = P + D
        N = J.vertex_normals(Q, tri)
        T3 = np.stack([np.zeros(nu), Tyz[:, 0], Tyz[:, 1]], 1)
        T3 -= N * (T3 * N).sum(1)[:, None]; T3 /= np.maximum(np.linalg.norm(T3, axis=1), 1e-9)[:, None]
        e = Q[j] - Q[i]; el = np.maximum(np.linalg.norm(e, axis=1), 1e-9)
        a = np.abs((e / el[:, None] * T3[i]).sum(1)) ** 4
        acc = np.zeros_like(Q); np.add.at(acc, i, e * a[:, None]); sw = np.bincount(i, weights=a, minlength=nu)
        delta = np.where((sw > 1e-6)[:, None], acc / np.maximum(sw, 1e-9)[:, None], 0)
        along = (delta * T3).sum(1)[:, None] * T3
        move = (delta - along) + prm['relax_slide'] * along
        step = .5 if it % 2 == 0 else -.53
        D = D + step * move * wb[:, None]
    D[hold] = 0
    return D, dict(band_verts=int((wb > .05).sum()), iters=prm['relax_iters'])


def ear_guard(P, prm):
    """1 on the auricle, 0 away from it: the face warp's own ear ellipse (HeroFaceReferenceFit, first ear shrink, in its eye-centred
    face frame) and only what stands out past the ear root."""
    body = bpy.data.objects['Body']; feat = bpy.data.objects['ReferenceFeatures']
    rig = next(o for o in bpy.data.objects if o.type == 'ARMATURE' and o.name.startswith('Rig_'))
    eye, right, up, fwd = J.frame(body, feat, rig)
    d = P - eye; fx = np.abs(d @ right); fy = d @ up; fz = d @ fwd
    yc, zc, ry, rz, root = prm['ear']
    reach = np.sqrt(((fy - yc) / ry) ** 2 + ((fz - zc) / rz) ** 2)
    return (1 - J.smooth(prm.get('ear_reach', (1.05, 1.3))[0], prm.get('ear_reach', (1.05, 1.3))[1], reach)) * J.smooth(root - .014, root - .006, fx)


def carve(Q, D1, prm, collar):
    """Second pass: a defined jaw corner. Seen from the side, the jaw's lower border is the line chin -> corner (jl_chin -> jl_gonion,
    rising ~25 deg), and its back border runs from the corner up to the ear lobe (jl_gonion -> jl_lobe). Everything below the lower line
    or behind the back line (on the side of the head, never at the chin centre or the nape) is recessed toward the neck axis, so the jaw
    becomes one clean edge that turns up toward the ear. The corner itself gets a small outward bump (jaw wider than the neck there) and
    the edge a small crest. Measured on the stage-1 surface Q; amounts already moved inward by stage 1 count toward the recess."""
    x, y, z = Q.T; ax = np.abs(x)
    c0y, c0z = prm['jl_chin']; gy, gz = prm['jl_gonion']; ly, lz = prm['jl_lobe']
    slope = (gz - c0z) / (gy - c0y)
    dl = (c0z + slope * (y - c0y)) - z                          # > 0 below the lower border line
    dr = y - (gy + (ly - gy) / (lz - gz) * (z - gz))            # > 0 behind the back border line
    w = prm['carve_w']
    along = np.clip((y - c0y) / (gy - c0y), 0, 1.5)
    lobe = 1 - J.smooth(lz - .008, lz - .002, z)                # the back recess stops under the lobe
    back = 1 - J.smooth(gy + prm['carve_back'][0], gy + prm['carve_back'][1], y)
    lateral = J.smooth(prm['carve_lat'][0], prm['carve_lat'][1], ax)
    cy = prm['neck_y']; r = np.hypot(x, y - cy); th = np.degrees(np.arctan2(ax, -(y - cy)))
    cz = collar(th) + prm['collar_gap']; vc = J.smooth(cz, cz + prm['ramp_side'], z)
    want = np.maximum(prm['carve_depth'] * J.smooth(0, prm['carve_ramp'], along) * J.smooth(0, w, dl),
                      prm['carve_ramus'] * J.smooth(0, w, dr) * lobe) * lateral * back * vc
    rh = np.stack([x, y - cy, np.zeros_like(r)], 1) / np.maximum(r, 1e-6)[:, None]
    have = np.clip(-(D1 * rh).sum(1), 0, None)                  # already moved in by stage 1
    D2 = -rh * np.clip(want - have, 0, None)[:, None]
    # crest just above / in front of the two lines, and the corner bump (outward, on the side of the face)
    side_out = np.stack([np.sign(x), np.zeros_like(x), np.zeros_like(x)], 1)
    crest = prm['carve_crest'] * (np.exp(-((dl + 1.2 * w) / w) ** 2) * J.smooth(.3, .6, along) + np.exp(-((dr + 1.2 * w) / w) ** 2) * lobe * (z > gz - .004)) * lateral * back * vc
    g = prm['carve_bump'] * np.exp(-(((y - gy) / prm['bump_r']) ** 2 + ((z - gz - .002) / prm['bump_r']) ** 2)) * J.smooth(.045, .060, ax) * vc
    D2 += side_out * (np.clip(crest, 0, None) + g)[:, None]
    band = (np.exp(-(dl / prm.get('denoise_w', .015)) ** 2) + np.exp(-(dr / prm.get('denoise_w', .015)) ** 2) * lobe) * lateral * back * vc
    return D2, dict(max_mm=round(float(np.linalg.norm(D2, axis=1).max() * 1000), 2), band=np.clip(band, 0, 1))


def dihedral(P, tri):
    fn = np.cross(P[tri[:, 1]] - P[tri[:, 0]], P[tri[:, 2]] - P[tri[:, 0]]); fn /= np.maximum(np.linalg.norm(fn, axis=1), 1e-15)[:, None]
    e = np.vstack([np.sort(tri[:, [0, 1]], 1), np.sort(tri[:, [1, 2]], 1), np.sort(tri[:, [2, 0]], 1)]); f = np.tile(np.arange(len(tri)), 3)
    o = np.lexsort((e[:, 1], e[:, 0])); e = e[o]; f = f[o]
    same = (e[1:] == e[:-1]).all(1)
    return e[:-1][same], np.degrees(np.arccos(np.clip(np.einsum('ij,ij->i', fn[f[:-1][same]], fn[f[1:][same]]), -1, 1)))


def crease_guard(P, D, tri, edges, nu, fixed, limit, rounds=12):
    """No new creases: wherever the sculpt makes an edge sharper than `limit` degrees that wasn't before, the change is smoothed
    a little more right there (Unity splits vertices at edges past its 60-degree smoothing angle, and they read as creases)."""
    e0, a0 = dihedral(P, tri)                                   # same topology: e0 and e1 list the same edges in the same order
    for r in range(rounds):
        e1, a1 = dihedral(P + D, tri)
        bad = e1[(a1 > limit) & (a1 > a0 + .25)]                # sharper than the limit AND sharper than before
        if not len(bad): return D, r
        m = np.zeros(nu, bool); m[bad.ravel()] = True
        m = J.dilate(edges, nu, m, 3) & ~fixed
        i, j = edges[:, 0], edges[:, 1]; deg = np.bincount(i, minlength=nu) + np.bincount(j, minlength=nu)
        for _ in range(4):                                      # relax only there; everything else keeps its value
            acc = np.zeros_like(D); np.add.at(acc, i, D[j]); np.add.at(acc, j, D[i])
            avg = acc / np.maximum(deg, 1)[:, None]
            D[m] += .5 * (avg[m] - D[m])
    return D, rounds


def apply(sex, prm):
    base, keys, idx, P, D, info = compute(sex, prm)
    me = bpy.data.objects['Body'].data
    Dv = D[idx]
    for name, k in keys.items():
        J.set_coords(me, k + Dv, name)
    J.set_coords(me, base + Dv)
    me.update()
    m = np.linalg.norm(Dv, axis=1)
    b = info['border']
    return dict(moved=int((m > 1e-6).sum()), max_mm=round(float(m.max() * 1000), 2),
                zmin_moved=round(float(base[m > 1e-6][:, 2].min()), 4) if (m > 1e-6).any() else None,
                below_collar=int(((m > 1e-6) & (base[:, 2] <= info['cz'][idx])).sum()),
                border=[(int(a), round(float(rb) * 1000, 1), round(float(zb), 4), round(float(rn) * 1000, 1), round(float(ra) * 1000, 1), round(float(za), 4)) for a, rb, zb, rn, ra, za in zip(*b)])


def reset(sex):
    base, keys = snapshot(sex)
    me = bpy.data.objects['Body'].data
    for name, k in keys.items(): J.set_coords(me, k, name)
    J.set_coords(me, base); me.update()


def fix_normals(sex, Dv, rings=3, smooth_iters=6):
    """Custom split normals: inside the sculpted area they become the new surface's normals (area-weighted, averaged over
    `smooth_iters` rings like the authored ones; raw facet normals show the mesh's fine noise), blending back
    to the authored normals over `rings` edge rings; everywhere else the authored normals are kept."""
    base, keys = snapshot(sex)
    me = bpy.data.objects['Body'].data
    idx, tri, edges, nu = J.topology_at(base, me)
    P = np.zeros((nu, 3)); P[idx] = J.coords(me)
    Nn = J.vertex_normals(P, tri)
    i, j = edges[:, 0], edges[:, 1]
    for _ in range(smooth_iters):                              # the authored normals are smooth too: average over a few rings
        acc = Nn.copy(); np.add.at(acc, i, Nn[j]); np.add.at(acc, j, Nn[i])
        Nn = acc / np.maximum(np.linalg.norm(acc, axis=1), 1e-12)[:, None]
    moved = np.zeros(nu, bool); moved[idx[np.linalg.norm(Dv, axis=1) > 5e-5]] = True
    w = moved.astype(float); m = moved.copy()
    for k in range(rings):
        nm = J.dilate(edges, nu, m, 1); w[nm & ~m] = 1 - (k + 1) / (rings + 1); m = nm
    loops = len(me.loops)
    old = np.empty(loops * 3); me.corner_normals.foreach_get('vector', old); old = old.reshape(-1, 3)
    lv = np.empty(loops, dtype=np.int64); me.loops.foreach_get('vertex_index', lv)
    wl = w[idx[lv]][:, None]
    new = old * (1 - wl) + Nn[idx[lv]] * wl
    new /= np.linalg.norm(new, axis=1)[:, None]
    me.normals_split_custom_set(new.tolist())
    me.update()
    return int(moved.sum()), int((w > 0).sum())


def reset_normals(sex):
    """Back to the authored custom normals (they live in source_<sex>_normals.npy, saved before the first change)."""
    me = bpy.data.objects['Body'].data
    path = f'{J.WORK}/source_{sex}_normals.npy'
    if not os.path.exists(path):
        a = np.empty(len(me.loops) * 3); me.corner_normals.foreach_get('vector', a); np.save(path, a.reshape(-1, 3))
    me.normals_split_custom_set(np.load(path).tolist()); me.update()


ZOOM = {'Male': (0, -.05, 1.49), 'Female': (0, -.045, 1.47)}


def run(sex, prm, tag, before='0', zs=None):
    """reset -> sculpt -> normals -> game preview -> overlays/stills/table -> before/now strip."""
    import subprocess
    reset(sex); reset_normals(sex)
    st = apply(sex, prm)
    base, keys = snapshot(sex)
    Dv = J.coords(bpy.data.objects['Body'].data) - base
    st['normals'] = transfer_normals(sex, smooth_iters=prm.get('normal_smooth', 4))   # authored normals turned with the surface
    J.build_preview(sex)
    J.evaluate(sex, tag, zoom=ZOOM[sex])
    rows = J.table(sex, tag, list(zs if zs is not None else np.arange(1.40, 1.51, .005)))
    subprocess.run(['/Library/Frameworks/Python.framework/Versions/3.14/bin/python3', J.WORK + '/compare.py', sex[0].lower() + before, tag, sex])
    json.dump(prm, open(f'{J.WORK}/params_{sex.lower()}.json', 'w'), indent=1)
    return st, rows


def transfer_normals(sex, smooth_iters=4):
    """Authored custom normals, each rotated by how much the sculpt turned the surface there: the rotation that takes the
    (ring-smoothed) surface normal of the source to that of the sculpted mesh. Unmoved areas keep their authored normals exactly."""
    base, keys = snapshot(sex)
    me = bpy.data.objects['Body'].data
    idx, tri, edges, nu = J.topology_at(base, me)
    i, j = edges[:, 0], edges[:, 1]
    def smooth_normals(co):
        P = np.zeros((nu, 3)); P[idx] = co
        Nn = J.vertex_normals(P, tri)
        for _ in range(smooth_iters):
            acc = Nn.copy(); np.add.at(acc, i, Nn[j]); np.add.at(acc, j, Nn[i])
            Nn = acc / np.maximum(np.linalg.norm(acc, axis=1), 1e-12)[:, None]
        return Nn
    n0 = smooth_normals(base); n1 = smooth_normals(J.coords(me))
    authored = np.load(f'{J.WORK}/source_{sex}_normals.npy')
    lv = np.empty(len(me.loops), dtype=np.int64); me.loops.foreach_get('vertex_index', lv)
    a = n0[idx[lv]]; b = n1[idx[lv]]
    k = np.cross(a, b); s = np.linalg.norm(k, axis=1); c = np.einsum('ij,ij->i', a, b)
    kk = k / np.maximum(s, 1e-12)[:, None]
    v = authored
    rot = v * c[:, None] + np.cross(kk, v) * s[:, None] + kk * (np.einsum('ij,ij->i', kk, v) * (1 - c))[:, None]
    out = np.where((s > 1e-7)[:, None], rot, v)
    out /= np.linalg.norm(out, axis=1)[:, None]
    me.normals_split_custom_set(out.tolist()); me.update()
    return dict(max_turn_deg=round(float(np.degrees(np.arcsin(np.clip(s.max(), 0, 1)))), 2), turned=int((s > 1e-4).sum()))


def line_profile(P, D, prm, hold):
    """Third pass, second step: the jaw edge sits at the same place all along the line. For every vertex near the line (side of the
    face, same sheet), its depth r (distance from the neck axis) is re-fitted from its neighbours as a straight-line function of
    position ALONG the line, with a long kernel along (`profile_s`) and a short one across (`profile_t`), so the edge's cross-section
    (the sharp turn) is kept but its wobble along the line goes. Only neighbours within `profile_gate` in depth count (never the
    under-jaw shelf behind the side surface). Vertices move sideways only (x), so they stay at the same place along and across the line."""
    from mathutils.kdtree import KDTree
    Q = P + D
    x, y, z = Q.T; ax = np.abs(x); sx = np.where(x >= 0, 1.0, -1.0)
    cy = prm['neck_y']; r = np.hypot(x, y - cy)
    c0 = np.array(prm['jl_chin']); g = np.array(prm['jl_gonion']); l = np.array(prm['jl_lobe'])
    yz = np.stack([y, z], 1)
    ss, st = prm['profile_s'], prm['profile_t']; band = prm['relax_band']
    corner = J.smooth(prm['relax_corner'][0], prm['relax_corner'][1], np.linalg.norm(yz - g, axis=1))
    lateral = J.smooth(prm['carve_lat'][0], prm['carve_lat'][1], ax)
    new_r = r.copy(); wsum = np.zeros(len(r)); acc = np.zeros(len(r))
    for a, b in ((c0, g), (g, l)):
        L = np.linalg.norm(b - a); T = (b - a) / L; Nn = np.array([T[1], -T[0]])
        s_ = (yz - a) @ T; t_ = (yz - a) @ Nn
        wb = (1 - J.smooth(band * .5, band, np.abs(t_))) * J.smooth(-.004, .003, s_) * (1 - J.smooth(L - .003, L + .004, s_)) * corner * lateral
        wb[hold] = 0
        pool = (np.abs(t_) < band + 3 * st) & (s_ > -.006 - 3 * ss) & (s_ < L + .006 + 3 * ss) & (ax > prm['carve_lat'][0] * .5)
        for side in (-1.0, 1.0):
            ids = np.flatnonzero(pool & (sx == side))
            if len(ids) < 10: continue
            kd = KDTree(len(ids))
            for n, i in enumerate(ids): kd.insert((s_[i] / ss, t_[i] / st, 0.0), n)
            kd.balance()
            for i in ids[wb[ids] > .02]:
                hits = kd.find_range((s_[i] / ss, t_[i] / st, 0.0), 3.0)
                js = ids[[h[1] for h in hits]]
                js = js[np.abs(r[js] - r[i]) < prm['profile_gate']]
                if len(js) < 6: continue
                ds = s_[js] - s_[i]; dt = t_[js] - t_[i]
                w = np.exp(-(ds / ss) ** 2 - (dt / st) ** 2)
                A = np.stack([np.ones_like(ds), ds], 1) * np.sqrt(w)[:, None]
                coef = np.linalg.lstsq(A, r[js] * np.sqrt(w), rcond=None)[0]
                acc[i] += wb[i] * coef[0]; wsum[i] += wb[i]
    m = wsum > 1e-6
    target = np.where(m, acc / np.maximum(wsum, 1e-9), r)
    blend = np.clip(wsum, 0, 1)
    new_r = r + (target - r) * blend
    nx = np.sqrt(np.maximum(new_r ** 2 - (y - cy) ** 2, 0)) * sx
    move = np.where(m & (new_r > np.abs(y - cy) + 1e-4), nx - x, 0)
    D = D.copy(); D[:, 0] += move
    D[hold] = 0
    return D, dict(moved=int((np.abs(move) > 1e-5).sum()), max_mm=round(float(np.abs(move).max() * 1000), 2))


def align_chain(P, tri, edges, nu, prm, protect):
    """Stage 0 (third pass): one clean, evenly spaced row of vertices exactly on the jaw line, so the sculpted edge runs along mesh
    edges instead of zig-zagging across ~3 mm triangles. Per side: the cheapest chain of mesh edges from the chin end to the corner
    (and from the corner up to the lobe end) that stays near the line (Dijkstra, cost = length x (1 + k |distance across|)); its
    vertices slide within the ORIGINAL surface onto the line, evenly spaced along it; the two rings either side are relaxed (also
    within the surface) so the triangles stay even. Only sliding: the shape is unchanged; the carve that follows turns at this row."""
    import heapq
    from mathutils.bvhtree import BVHTree
    from mathutils import Vector
    bvh = BVHTree.FromPolygons([tuple(v) for v in P], [tuple(int(k) for k in t) for t in tri])
    x, y, z = P.T; ax = np.abs(x); yz = np.stack([y, z], 1)
    c0 = np.array(prm['jl_chin']); g = np.array(prm['jl_gonion']); l = np.array(prm['jl_lobe'])
    nbr = [[] for _ in range(nu)]
    for a, b in edges: nbr[a].append(b); nbr[b].append(a)
    def on_surface(side, py, pz, near_x):
        hit = bvh.ray_cast(Vector((side * .3, py, pz)), Vector((-side, 0, 0)))
        if hit[0] is None or abs(abs(hit[0].x) - near_x) > .015: return None
        return np.array(hit[0])
    D = np.zeros_like(P); chains = {}
    for side in (-1.0, 1.0):
        chain_all = []
        for name, a, b, s0 in (('lower', c0, g, prm['align_start']), ('ramus', g, l, 0.0)):
            L = np.linalg.norm(b - a); T = (b - a) / L; Nn = np.array([T[1], -T[0]])
            s_ = (yz - a) @ T; t_ = (yz - a) @ Nn
            ok = (np.abs(t_) < prm.get('align_band', .010)) & (s_ > s0 - .006) & (s_ < L + .006) & (x * side > prm['carve_lat'][0])
            cand = np.flatnonzero(ok)
            if not len(cand): continue
            start = chain_all[-1] if chain_all else cand[np.argmin(np.abs(s_[cand] - s0) + 3 * np.abs(t_[cand]))]
            goal = cand[np.argmin(np.abs(s_[cand] - L) + 3 * np.abs(t_[cand]))]
            dist = {start: 0.0}; prev = {}; pq = [(0.0, start)]
            while pq:
                d, u = heapq.heappop(pq)
                if u == goal: break
                if d > dist.get(u, 9): continue
                for v in nbr[u]:
                    if not ok[v] and v != goal: continue
                    w = np.linalg.norm(P[v] - P[u]) * (1 + prm['align_k'] * (abs(t_[u]) + abs(t_[v])) / 2 / .003)
                    if d + w < dist.get(v, 9):
                        dist[v] = d + w; prev[v] = u; heapq.heappush(pq, (d + w, v))
            if goal not in prev and goal != start: continue
            path = [goal]
            while path[-1] != start: path.append(prev[path[-1]])
            path = path[::-1]
            if chain_all: path = path[1:]
            # evenly spaced along the line, exactly on it, on the original surface
            sa = s_[path[0]] if not chain_all else 0.0
            sb = L
            n = len(path)
            for k, v in enumerate(path):
                if name == 'ramus' and k == n - 1: break                 # the lobe end stays (ear guard nearby)
                sk = sa + (sb - sa) * (k + (1 if chain_all else 0)) / max(n - (0 if chain_all else 1), 1)
                sk = s_[v] + prm.get('align_spacing', 1.0) * (sk - s_[v])     # how far toward even spacing
                py, pz = a + sk * T
                if protect[v]: continue
                q = on_surface(side, py, pz, ax[v])
                if q is None: continue
                mv = q - P[v]; ml = np.linalg.norm(mv); cap = prm.get('align_cap', 1.0)
                if ml > cap:                                    # never a long slide: part-way, back onto the surface
                    py, pz = (P[v] + mv * cap / ml)[1:]
                    q = on_surface(side, py, pz, ax[v])
                    if q is None: continue
                D[v] = q - P[v]
            chain_all += path
        chains[side] = chain_all
    # relax two rings either side of each chain, within the surface
    fixed_chain = np.zeros(nu, bool)
    for c in chains.values(): fixed_chain[c] = True
    ring = fixed_chain.copy()
    for _ in range(prm['align_rings']): ring = J.dilate(edges, nu, ring, 1)
    ring &= ~fixed_chain & (ax > prm['carve_lat'][0]) & ~protect
    for _ in range(prm['align_relax']):
        Q = P + D
        for v in np.flatnonzero(ring):
            avg = Q[nbr[v]].mean(0)
            q = on_surface(np.sign(Q[v, 0]), avg[1], avg[2], abs(Q[v, 0]))
            if q is not None: D[v] = D[v] + .5 * (q - Q[v])
    return D, dict(chain=[len(c) for c in chains.values()], moved=int((np.linalg.norm(D, axis=1) > 1e-6).sum()), max_mm=round(float(np.linalg.norm(D, axis=1).max() * 1000), 2))
