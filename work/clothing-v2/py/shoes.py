"""Shoes (tennis + spikeless golf) and socks. Upper = smooth hull over foot + sock (toe room, no toe shapes), collar cut round the ankle;
sole = midsole wall + outsole extruded from the upper's bottom line with toe spring; tongue, eyestays, laces and (golf) nub tread as geometry."""
import math
import numpy as np
import glib as G
from garment import Garment, solidify, LEATHER, MESH, RUBBER, FOAM, LACE, PIQUE, RIB
import parts


def foot_frame(body, sd):
    s = 'Left' if sd < 0 else 'Right'
    ankle = body.head(s + 'Foot'); toes = body.head(s + 'Toes')
    side = (body.footL if sd < 0 else body.footR) > .5
    p = body.pos[side & (body.pos[:, 2] < .05)]
    fwd = toes - ankle; fwd[2] = 0; fwd /= np.linalg.norm(fwd)
    lat = np.cross(fwd, [0, 0, 1.]) * (1 if sd > 0 else -1)            # pointing to the outside of this foot
    u = (p - ankle) @ fwd
    heel = ankle + fwd * u.min(); heel[2] = 0
    L = u.max() - u.min()
    return heel, fwd, lat, L


def to_frame(x, heel, fwd, lat):
    d = x - heel
    return np.stack([d @ lat, d @ fwd, d[:, 2]], 1)


def from_frame(q, heel, fwd, lat):
    return heel + np.outer(q[:, 0], lat) + np.outer(q[:, 1], fwd) + np.outer(q[:, 2], [0, 0, 1.])


def build_shoes(body, P):
    out = {}
    for sd in (-1, 1):
        out['Kit_Shoe_' + ('L' if sd < 0 else 'R')] = shoe(body, P, sd)
    return out


def hull_last(body, P, sd, heel, fwd, lat, L):
    """the shoe last: convex hull of the foot (+ ankle up to the collar), toes stretched forward for toe room, resampled from an icosphere
    shot from inside, rounded (Taubin) and offset by the clearance."""
    import bpy, bmesh
    from mathutils import Vector
    fm = (body.footL if sd < 0 else body.footR) > .2
    shin = (body.shinL if sd < 0 else body.shinR) > .4
    p = body.pos
    sel = (fm | shin) & (p[:, 2] < P['collar_back'] + .03)
    q = to_frame(p[sel], heel, fwd, lat)
    k = np.clip((q[:, 1] - .55 * L) / (.45 * L), 0, 1)
    q[:, 1] += k * P['toe_room']
    q[:, 1] -= np.clip((.10 * L - q[:, 1]) / (.10 * L), 0, 1) * P['heel_room']
    q[:, 0] *= 1 + P.get('width_room', .0)
    # sneaker volume: the toe box and instep stand a little higher than the bare foot
    tb = np.clip((q[:, 1] - .45 * L) / (.25 * L), 0, 1) * np.clip(q[:, 2] / .03, 0, 1)
    q[:, 2] += tb * P.get('toe_box_lift', .006)
    bm = bmesh.new()
    for v in q: bm.verts.new(Vector(v))
    res = bmesh.ops.convex_hull(bm, input=bm.verts)
    for e in res.get('geom_interior', []):
        if isinstance(e, bmesh.types.BMVert): bm.verts.remove(e)
    bmesh.ops.triangulate(bm, faces=bm.faces)
    from mathutils.bvhtree import BVHTree
    hv = np.array([v.co[:] for v in bm.verts]); bm.verts.ensure_lookup_table()
    ht = np.array([[v.index for v in f.verts] for f in bm.faces])
    tree = BVHTree.FromPolygons([tuple(x) for x in hv], [tuple(t) for t in ht], all_triangles=True)
    bm.free()
    # icosphere directions from the core
    bm2 = bmesh.new(); bmesh.ops.create_icosphere(bm2, subdivisions=P.get('ico', 4), radius=1.0)
    bm2.verts.ensure_lookup_table()
    sv = np.array([v.co[:] for v in bm2.verts]); st = np.array([[v.index for v in f.verts] for f in bm2.faces]); bm2.free()
    core = np.array([0, .5 * L, .035])
    pts = np.zeros_like(sv)
    for i, d in enumerate(sv):
        r = tree.ray_cast(Vector(core + d * .5), Vector(-d))      # from outside toward the core: first hit = hull surface
        pts[i] = np.array(r[0]) if r[0] is not None else core
    # loft: every cross-section across the foot is that slice's own 2D hull (instep slope kept, toes / arch / tendons gone)
    class FB: pass
    fb = FB(); fb.pos = q; 
    from scipy_free_hull import slices_envelope
    env = slices_envelope(q, -.005, L + P['toe_room'] + .005, .006, core_z=.035, nth=96)
    s_ = pts[:, 1]
    d = pts - np.stack([np.zeros(len(pts)), s_, np.full(len(pts), .035)], 1); d[:, 1] = 0
    rr = np.linalg.norm(d, axis=1); th = np.arctan2(d[:, 2], d[:, 0])
    re = env(th, s_)
    ok = (re > .005) & (s_ > .01) & (s_ < L + P['toe_room'] - .01)
    lo = np.where(ok, np.minimum(rr, re), rr)
    pts = np.where(ok[:, None], np.stack([np.zeros(len(pts)), s_, np.full(len(pts), .035)], 1) + d / (rr[:, None] + 1e-9) * lo[:, None], pts)
    nb = G.neighbours(len(pts), st)
    for _ in range(P.get('round', 25)):
        pts += .5 * G.umbrella(pts, nb); pts -= .52 * G.umbrella(pts, nb)
    n = G.vertex_normals(pts, st)
    if np.mean(np.einsum('ij,ij->i', n, pts - core)) < 0: st = st[:, ::-1]; n = -n
    pts = pts + n * P['upper_clear']
    return pts, st


def sneaker_last(body, P, sd, heel, fwd, lat, L):
    """A sneaker last in the foot frame (x lateral, y heel->toe, z up): the footprint of the real foot (plus room), a designed top line
    (heel cup / collar round the real ankle, then the instep falling to a low rounded toe box) and fuller-than-elliptic cross sections."""
    fm = (body.footL if sd < 0 else body.footR) > .2
    shin = (body.shinL if sd < 0 else body.shinR) > .4
    q = to_frame(body.pos, heel, fwd, lat)
    sole = q[fm & (q[:, 2] < .03)]
    Lt = L + P['toe_room']
    ny = 64; nphi = 28
    t = np.linspace(0, 1, ny)
    ys = -P['heel_room'] + (Lt + P['heel_room']) * (.5 - .5 * np.cos(np.pi * t))        # denser rows at heel and toe
    # footprint half-widths from the real sole (each side separately), smoothed, plus room
    lo = np.zeros(ny); hi = np.zeros(ny)
    for j, y in enumerate(ys):
        m = np.abs(sole[:, 1] - min(max(y, .01), L - .005)) < .008
        lo[j], hi[j] = (sole[m, 0].min(), sole[m, 0].max()) if m.sum() > 3 else (np.nan, np.nan)
    for a in (lo, hi):
        good = ~np.isnan(a); a[:] = np.interp(ys, ys[good], a[good])
    for _ in range(6):
        lo[1:-1] = (lo[:-2] + 2 * lo[1:-1] + lo[2:]) / 4; hi[1:-1] = (hi[:-2] + 2 * hi[1:-1] + hi[2:]) / 4
    room = P.get('side_room', .006)
    lo -= room; hi += room
    # round the visible heel and ankle: every row behind the throat is at least as wide as the real heel / ankle up to the collar line
    zc_rows = np.interp(ys, np.array(P['collar_prof_y']) * L, np.minimum(P['collar_prof_z'], .11))
    ank = fm | shin
    for j, y in enumerate(ys):
        if y > .36 * L: continue
        m = ank & (np.abs(q[:, 1] - max(y, q[fm, 1].min() + .004)) < .006) & (q[:, 2] > .006) & (q[:, 2] < zc_rows[j] + .005)
        if m.sum() > 3:
            lo[j] = min(lo[j], q[m, 0].min() - P['upper_clear']); hi[j] = max(hi[j], q[m, 0].max() + P['upper_clear'])
    for _ in range(10):
        lo[1:-1] = np.minimum(lo[1:-1], (lo[:-2] + 2 * lo[1:-1] + lo[2:]) / 4 * 0 + (lo[:-2] + 2 * lo[1:-1] + lo[2:]) / 4)
        hi[1:-1] = (hi[:-2] + 2 * hi[1:-1] + hi[2:]) / 4
    # rounded ends in plan view
    tip = np.clip((ys - .80 * Lt) / (Lt - .80 * Lt), 0, 1); back = np.clip((.10 * L - ys) / (.10 * L + P['heel_room']), 0, 1)
    shrink = np.sqrt(np.clip(1 - tip ** 2, 0, 1)) * np.sqrt(np.clip(1 - back ** 2, 0, 1))
    xc = (lo + hi) / 2; hw = (hi - lo) / 2
    hw = hw * np.maximum(shrink, .0)
    # top line: designed instep / toe box, but never below the real ankle + room in the collar region
    prof_y, prof_z = np.array(P['top_y']) * L, np.array(P['top_z'])
    h = np.interp(ys, prof_y, prof_z)
    zc = np.interp(ys, np.array(P['collar_prof_y']) * L, np.minimum(P['collar_prof_z'], .11))
    h = np.where(ys < .32 * L, np.maximum(h, zc + .014), h)            # the collar line is cut from above
    tip_h = P.get('tip_h', .028)
    h = np.where(ys > .9 * Lt, tip_h + (h - tip_h) * np.sqrt(np.clip(1 - ((ys - .9 * Lt) / (.1 * Lt)) ** 2, 0, 1)), h)
    z0 = P['insole_z'] - .004
    tipr = np.sqrt(np.clip(1 - tip ** 2, 0, 1))
    h = z0 + (h - z0) * (.30 + .70 * tipr ** .5)                         # the toe rounds down to the bumper in side view too
    phi = np.linspace(0, np.pi, nphi)
    rows = []
    for j in range(ny):
        c = np.cos(phi); sn = np.sin(phi)
        x = xc[j] + hw[j] * c
        z = z0 + (h[j] - z0) * sn ** P.get('fullness', .55)
        rows.append(np.stack([x, np.full(nphi, ys[j]), z], 1))
    R = np.stack(rows, 0)                                     # (ny, nphi, 3)
    # close the open bottom between the two edges with a flat insole sheet (hidden; keeps the last a closed shell for the lining)
    pos = R.reshape(-1, 3)
    tri = parts.grid_tri(ny, nphi)
    # extend the walls straight down a little below the insole so the cut at insole_z is clean
    return pos, tri, ys, xc, hw, h


def shoe(body, P, sd):
    heel, fwd, lat, L = foot_frame(body, sd)
    if P.get('last') == 'sneaker':
        q, tri, *_ = sneaker_last(body, P, sd, heel, fwd, lat, L)
    else:
        q, tri = hull_last(body, P, sd, heel, fwd, lat, L)
    x = from_frame(q, heel, fwd, lat)
    # the left foot's frame is mirrored: re-orient in mesh space so the outer wall faces out
    cen = x.mean(0)
    fn = G.face_normals(x, tri)
    if np.mean(np.einsum('ij,ij->i', fn, x[tri].mean(1) - cen)) < 0: tri = tri[:, ::-1]
    if P.get('last') != 'sneaker':
        x = G.solve_shell(body, x, tri, np.full(len(x), P['upper_clear'] * .7), iters=0, start=x)
        cp, fi, d = body.nearest(x)
        bad = d < P['upper_clear'] * .7
        if bad.any():
            bc = body.bary(cp[bad], fi[bad]); nn = body.smooth_normal_at(fi[bad], bc); x[bad] = cp[bad] + nn * P['upper_clear'] * .7
    # flatten the bottom onto the insole plane and cut there; collar round the ankle
    q = to_frame(x, heel, fwd, lat)
    g = Garment(x, tri, fabric=P.get('upper_fabric', LEATHER), roles=('Kit_Shoe',), kind='upper')
    g.A['qx'], g.A['qy'], g.A['qz'] = q[:, 0], q[:, 1], q[:, 2]
    z_insole = P['insole_z']
    g.cut(g.A['qz'] - z_insole)
    # collar line in side profile (fraction of the length from the heel -> height above the floor); in front of the throat top nothing is cut
    q = np.stack([g.A['qx'], g.A['qy'], g.A['qz']], 1)
    prof_y = np.array(P['collar_prof_y']) * L
    zc = np.interp(q[:, 1], prof_y, P['collar_prof_z'])
    zc = zc + P.get('heel_tab', .006) * np.exp(-(q[:, 0] / .022) ** 2) * np.clip(1 - q[:, 1] / (.1 * L), 0, 1)
    g.A['f_collar'] = zc - q[:, 2]
    g.cut(g.A['f_collar'])
    g.largest()
    # lacing throat: a narrow V opening along the instep, from the vamp up into the collar opening
    y_v, y_c = P['throat_y0'] * L, P['throat_y1'] * L
    q = np.stack([g.A['qx'], g.A['qy'], g.A['qz']], 1)
    tt = np.clip((y_v - q[:, 1]) / (y_v - y_c), 0, 1)                     # 0 at the vamp, 1 at the collar end
    half = P['throat_w0'] / 2 + (P['throat_w1'] - P['throat_w0']) / 2 * tt
    top = q[:, 2] > P['collar_side'] * .55
    inside = (q[:, 1] < y_v) & (q[:, 1] > y_c - .03) & top
    def throat_fields():
        q = np.stack([g.A['qx'], g.A['qy'], g.A['qz']], 1)
        tt = np.clip((y_v - q[:, 1]) / (y_v - y_c), 0, 1)
        half = P['throat_w0'] / 2 + (P['throat_w1'] - P['throat_w0']) / 2 * tt
        inside = (q[:, 1] < y_v) & (q[:, 1] > y_c - .03) & (q[:, 2] > P['collar_side'] * .55)
        x0 = q[:, 0] - P.get('throat_x', 0.0)
        return inside, x0, half
    for sgn in (1, -1):                          # both slot edges as edge loops first (the slot is narrower than the mesh spacing)
        inside, x0, half = throat_fields()
        g.insert(np.where(inside, sgn * x0 - half, .05))
    inside, x0, half = throat_fields()
    g.A['f_throat'] = np.where(inside, np.abs(x0) - half, .05)
    g.cut(g.A['f_throat'])
    g.largest()
    # padded collar roll: lift near the collar edge
    loops = G.boundary_loops(g.tri)
    top = max(loops, key=lambda l: g.A['qz'][l].mean())
    d = G.edge_distance(g.pos, g.tri, [top])
    g.A['d_collar'] = d
    for v in [.016, .0145]: g.insert(g.A['d_collar'] - v)
    d = g.A['d_collar']
    g.A['lift'] = np.where(d < .016, P.get('collar_pad', .0042) * np.sin(np.clip(d / .016, 0, 1) * math.pi) ** .5, 0)
    # raised eyestays along the lacing throat
    ft = g.A['f_throat']
    for v in [.013, .0118]: g.insert(g.A['f_throat'] - v)
    ft = g.A['f_throat']
    es = (ft < .013) & (g.A['qz'] > P['collar_side'] * .5)
    g.A['lift'][es] = np.maximum(g.A['lift'][es], .0013 * np.clip((.013 - ft[es]) / .0012, 0, 1))
    g.A['eyestay'] = es.astype(np.float64)
    if P.get('eyestay_trim'): g.A['trim'][es] = 1
    # panels: toe cap, heel counter, side swoosh-free accent band -> trim mask
    q = np.stack([g.A['qx'], g.A['qy'], g.A['qz']], 1)
    toecap = q[:, 1] > (P['toecap'] * L + (q[:, 2] - z_insole) * .8)
    counter = (q[:, 1] < P['counter'] * L - (q[:, 2] - z_insole) * .3) & (q[:, 2] < P['collar_side'] - .01)
    g.insert(q[:, 1] - (P['toecap'] * L + (q[:, 2] - z_insole) * .8))
    q = np.stack([g.A['qx'], g.A['qy'], g.A['qz']], 1)
    g.insert((P['counter'] * L - (q[:, 2] - z_insole) * .3) - q[:, 1])
    q = np.stack([g.A['qx'], g.A['qy'], g.A['qz']], 1)
    toecap = q[:, 1] > (P['toecap'] * L + (q[:, 2] - z_insole) * .8) - 1e-5
    counter = q[:, 1] < (P['counter'] * L - (q[:, 2] - z_insole) * .3) + 1e-5
    if P.get('accent'):
        # side accent stripe (tennis): a diagonal band on both sides, from the midfoot up toward the eyestay
        for v in [-P['accent_w'] / 2, P['accent_w'] / 2]:
            a = (g.A['qy'] - P['accent_y'] * L) - (g.A['qz'] - z_insole) * P['accent_slope']
            g.insert(a - v)
        q = np.stack([g.A['qx'], g.A['qy'], g.A['qz']], 1)
        a = (q[:, 1] - P['accent_y'] * L) - (q[:, 2] - z_insole) * P['accent_slope']
        side_ok = (np.abs(q[:, 0]) > P['eyestay_w'] * .9) & (q[:, 2] < P['collar_side'] - .004) & (q[:, 2] > z_insole + .006)
        g.A['trim'][side_ok & (np.abs(a) < P['accent_w'] / 2)] = 1
    q = np.stack([g.A['qx'], g.A['qy'], g.A['qz']], 1)
    if P.get('counter_trim'): g.A['trim'][(q[:, 1] < (P['counter'] * L - (q[:, 2] - z_insole) * .3) + 1e-5)] = 1
    toecap = q[:, 1] > (P['toecap'] * L + (q[:, 2] - z_insole) * .8) - 1e-5
    counter = q[:, 1] < (P['counter'] * L - (q[:, 2] - z_insole) * .3) + 1e-5
    g.A['lift'][toecap | counter] += .0006
    g.A['f_toecap'] = q[:, 1] - (P['toecap'] * L + (q[:, 2] - z_insole) * .8)
    g.A['f_counter'] = (P['counter'] * L - (q[:, 2] - z_insole) * .3) - q[:, 1]
    g.A['f_accent'] = ((q[:, 1] - P.get('accent_y', 0) * L) - (q[:, 2] - z_insole) * P.get('accent_slope', 0)) if P.get('accent') else np.full(len(q), 1.0)
    g.A['qL'] = np.full(len(q), L)
    upper_outline = min(G.boundary_loops(g.tri), key=lambda l: g.A['qz'][l].mean())
    g.F['proj'][:] = 5
    shell = solidify(g, P['upper_thick'], facing=None if P.get('last') == 'sneaker' else .025)
    pieces = [sole(body, g, upper_outline, P, heel, fwd, lat, L, sd)]
    pieces += tongue_laces(body, g, P, heel, fwd, lat, L, sd)
    for pc in pieces: shell.merge(pc)
    return shell


def sole(body, g, loop, P, heel, fwd, lat, L, sd):
    """midsole + outsole extruded from the upper's bottom outline: bulging foam wall, toe spring, outsole in the trim colour."""
    q0 = np.stack([g.A['qx'][loop], g.A['qy'][loop], g.A['qz'][loop]], 1)
    # resample the outline evenly
    pts, _ = parts.resample_polyline(np.concatenate([q0, q0[:1]]), P.get('sole_n', 96))
    c = pts.mean(0)
    rad = pts[:, :2] - c[:2]; rad /= np.linalg.norm(rad, axis=1, keepdims=True)
    yrel = pts[:, 1] / L
    spring = P['toe_spring'] * np.clip((yrel - .72) / .28, 0, 1) ** 2
    heel_lift = P.get('heel_bevel', .004) * np.clip((.08 - yrel) / .08, 0, 1)
    zb = P['sole_bottom'] + spring + heel_lift + (P['nub_h'] if P.get('nubs') else 0)   # bottom of the outsole (above the nubs on golf shoes)
    zt = P['insole_z'] + P.get('sole_wrap', .010)                    # the midsole wraps up over the upper's bottom edge
    z_os = zb + P['outsole']                                         # outsole / midsole joint
    fl = P['flare']
    rows = []
    # (height, outward offset): rounded lip over the upper, a slim waist groove, the bulging foam wall, a crisp outsole line, a bevelled tread edge
    prof = [(zt, .0022), (zt - .0012, .0034), (zt - .004, fl * .75), ((zt + z_os) * .5 + .003, fl * .95), ((zt + z_os) * .5, fl * .82),
            ((zt + z_os) * .5 - .003, fl), (z_os + .0015, fl * .95), (z_os, fl * 1.02), (z_os - .0006, fl * 1.04), (zb + .0015, fl * .95), (zb, fl * .7)]
    for z, off in prof:
        zz = z if np.ndim(z) else np.full(len(pts), z)
        r = np.concatenate([pts[:, :2] + rad * off, zz[:, None]], 1)
        rows.append(r)
    # bottom cap: shrink to the centre in 4 rings
    for k in (.75, .5, .25):
        r = np.concatenate([c[:2] + (rows[-1][:, :2] - c[:2]) * k, zb[:, None] if np.ndim(zb) else np.full((len(pts), 1), zb)], 1)
        rows.append(r)
    R = np.stack(rows, 1)
    n, m = R.shape[:2]
    posq = R.reshape(-1, 3)
    tri = parts.grid_tri(n, m, closed_i=True)
    # centre point
    cz = (zb.mean() if np.ndim(zb) else zb)
    posq = np.concatenate([posq, [[c[0], c[1], cz]]]); ci = len(posq) - 1
    cap = [(i * m + m - 1, ((i + 1) % n) * m + m - 1, ci) for i in range(n)]
    tri = np.concatenate([tri, np.array(cap, np.int64)])
    pos = from_frame(posq, heel, fwd, lat)
    tri = parts.orient_outward(pos, tri, lambda x: np.tile(from_frame(np.array([[c[0], c[1], zt - .01]]), heel, fwd, lat), (len(x), 1)))
    sg = Garment(pos, tri, fabric=FOAM, roles=('Kit_Sole',), kind='sole')
    sg.F['proj'][:] = 5
    j = np.tile(np.arange(m), n); j = np.concatenate([j, [m - 1]])
    yv = np.concatenate([np.repeat(pts[:, 1] / L, m), [.5]])
    zv = np.concatenate([posq[:-1, 2], [cz]])
    zos_v = np.concatenate([np.repeat(z_os if np.ndim(z_os) else np.full(len(pts), z_os), m), [cz]])
    # outsole rubber (the sole's trim colour) on the tread and the bevel, wrapping up the toe as a bumper and round the heel a little
    bump = np.clip((yv - .86) / .08, 0, 1) * .014 + np.clip((.05 - yv) / .05, 0, 1) * .006
    rubber = (j >= 7) | (zv < zos_v + bump - 1e-4)
    sg.A['trim'][rubber] = 1
    sg.F['fabric'][rubber[tri].all(1)] = RUBBER
    sg.A['sole_q'] = np.concatenate([posq[:, :2], posq[:, 2:]], 1)
    sg.A['outsole'] = rubber.astype(np.float64)
    out = [sg]
    if P.get('nubs'):
        out += nubs(posq, pts, c, zb, P, heel, fwd, lat, L)
    return out[0] if len(out) == 1 else _merge(out)


def _merge(gs):
    g = gs[0]
    for o in gs[1:]: g.merge(o)
    return g


def inside_polygon(pts, poly):
    x, y = pts[:, 0], pts[:, 1]; inside = np.zeros(len(pts), bool)
    px, py = poly[:, 0], poly[:, 1]; n = len(poly)
    for k in range(n):
        x1, y1, x2, y2 = px[k], py[k], px[(k + 1) % n], py[(k + 1) % n]
        c = ((y1 > y) != (y2 > y)) & (x < (x2 - x1) * (y - y1) / (y2 - y1 + 1e-12) + x1)
        inside ^= c
    return inside


def nubs(posq, pts, c, zb, P, heel, fwd, lat, L):
    """spikeless golf tread: rows of low rounded rubber nubs under the outsole (frame coordinates -> mesh)."""
    sp = P['nub_spacing']; rr = P['nub_r']; h = P['nub_h']
    xs = np.arange(-.08, .08, sp); ys = np.arange(.01, L + .02, sp * .9)
    X, Y = np.meshgrid(xs, ys); cand = np.stack([X.ravel() + (np.round(Y.ravel() / (sp * .9)) % 2) * sp * .5, Y.ravel()], 1)
    shrink = c[:2] + (pts[:, :2] - c[:2]) * .9
    keep = inside_polygon(cand, shrink)
    out = []
    zbase = zb if not np.ndim(zb) else None
    for (x, y) in cand[keep]:
        zz = P['sole_bottom'] + h + P['toe_spring'] * np.clip((y / L - .72) / .28, 0, 1) ** 2
        n = 10
        ring = []
        for (rk, dz) in [(1.0, 0.0), (1.0, -h * .55), (.75, -h * .9), (.0, -h)]:
            ring.append([(x + rk * rr * math.cos(2 * math.pi * k / n), y + rk * rr * math.sin(2 * math.pi * k / n), zz + .0005 + dz) for k in range(n)])
        q = np.array(ring).reshape(-1, 3)
        tri = parts.grid_tri(4, n, closed_i=False)
        # rows are rings: grid_tri treats [i*nj+j] with i = ring, j = around (not closed around) -> build manually
        t = []
        for a in range(3):
            for k in range(n):
                k2 = (k + 1) % n
                t.append((a * n + k, (a + 1) * n + k, (a + 1) * n + k2)); t.append((a * n + k, (a + 1) * n + k2, a * n + k2))
        t = np.array(t, np.int64)
        pos = from_frame(q, heel, fwd, lat)
        t = parts.orient_outward(pos, t, lambda v: np.tile(from_frame(np.array([[x, y, zz + .01]]), heel, fwd, lat), (len(v), 1)))
        ng = Garment(pos, t, fabric=RUBBER, roles=('Kit_Sole',), kind='nub')
        ng.A['trim'][:] = 1
        out.append(ng)
    return out


def tongue_laces(body, g, P, heel, fwd, lat, L, sd):
    """padded tongue under the throat (shows between the eyestays, stands up out of the collar), eyelets either side,
    flat laces criss-crossing over it and a bow at the top."""
    tree = parts.bvh(g.pos, g.tri)
    out = []
    y_v, y_c = P['throat_y0'] * L, P['throat_y1'] * L
    def hit(qx, qy, lift=0.0):
        p = from_frame(np.array([[qx, qy, .3]]), heel, fwd, lat)[0]
        r = tree.ray_cast(parts.Vector(p), parts.Vector((0, 0, -1)))
        if r[0] is None: return None, None
        return np.array(r[0]) + np.array(r[1]) * lift, np.array(r[1])
    def half(qy):
        tt = np.clip((y_v - qy) / (y_v - y_c), 0, 1)
        return P['throat_w0'] / 2 + (P['throat_w1'] - P['throat_w0']) / 2 * tt
    # tongue: the throat edges' surface, a little below them, wider than the gap
    ys = np.linspace(y_v + .012, y_c, 16)
    nx = 7; tw = P['tongue_w']
    rows = []
    for y in ys:
        row = []
        for xx in np.linspace(-tw / 2, tw / 2, nx):
            # sample the closed last surface: take the eyestay height at this y and interpolate across
            pl, _ = hit(-half(y) - .003, y); pr, _ = hit(half(y) + .003, y)
            if pl is None or pr is None:
                p0 = from_frame(np.array([[xx, y, P['collar_front'] * .8]]), heel, fwd, lat)[0]
            else:
                a = (xx + tw / 2) / tw
                p0 = pl + (pr - pl) * a + np.array([0, 0, .006 * math.sin(a * math.pi) + .0005])
            row.append(p0)
        rows.append(row)
    top_row = np.array(rows[-1])
    for k in range(1, 5):
        rows.append(top_row + np.array([0, 0, P['tongue_rise'] * k / 4]) - fwd * (.006 * k / 4))
    pos = np.array(rows).reshape(-1, 3)
    tri = parts.grid_tri(len(rows), nx)
    tri = parts.orient_outward(pos, tri, lambda x: x - np.array([0, 0, .05]) + fwd * .0)
    tg = Garment(pos, tri, fabric=MESH, roles=('Kit_Shoe',), kind='tongue')
    tg.A['tongue'] = np.ones(len(pos))
    out.append(solidify(tg, .0035, facing=None))
    ttree = parts.bvh(tg.pos, tg.tri)
    def over_tongue(p, clear=.0028):
        r = ttree.ray_cast(parts.Vector(p + np.array([0, 0, .2])), parts.Vector((0, 0, -1)))
        return p if r[0] is None else np.array([p[0], p[1], max(p[2], r[0][2] + clear)])
    # laces: criss-cross between eyelets 6 mm outside the throat edges
    nl = P['lace_rows']
    yl = np.linspace(y_v - .006, y_c + .012, nl)
    from bottoms import tube
    def eyelet(sgn, y):
        p, n = hit(sgn * (half(y) + .0065), y, .0015)
        return p
    for i in range(nl):
        for sgn in ((-1, 1) if i < nl - 1 else ()):
            a = eyelet(sgn, yl[i]); b = eyelet(-sgn, yl[i + 1])
            if a is None or b is None: continue
            mid = over_tongue((a + b) / 2, .0028 + .0012 * (sgn > 0))
            q1 = over_tongue(a * .62 + mid * .38, .0022); q2 = over_tongue(b * .62 + mid * .38, .0022)
            path = np.array([a, q1, mid, q2, b])
            pp, tt = tube(path, .0019, 6)
            c = path.mean(0); up_ = np.array([0, 0, 1.])
            pp = pp - np.outer((pp - c) @ up_, up_) * .5                    # flat lace
            out.append(Garment(pp, tt, fabric=LACE, roles=('Kit_Shoe',), kind='lace'))
    a = eyelet(-1, yl[0]); b = eyelet(1, yl[0])
    if a is not None and b is not None:
        path = np.array([a, (a + b) / 2 + np.array([0, 0, .003]), b]); pp, tt = tube(path, .0019, 6)
        out.append(Garment(pp, tt, fabric=LACE, roles=('Kit_Shoe',), kind='lace'))
    # bow at the top pair
    t0 = eyelet(1, yl[-1])
    t1 = eyelet(-1, yl[-1])
    if t0 is not None and t1 is not None:
        top = over_tongue((t0 + t1) / 2, .004)
        for sgn in (-1, 1):
            ang = np.linspace(0, 2 * math.pi, 16)
            loop = top + np.outer(np.cos(ang) * .011 + .011, lat * sgn) + np.outer(np.sin(ang) * .0075, fwd) + np.outer(np.abs(np.sin(ang)) * .0025 + .001, [0, 0, 1.])
            pp, tt = tube(loop, .0016, 5)
            out.append(Garment(pp, tt, fabric=LACE, roles=('Kit_Shoe',), kind='lace'))
            end = top + np.outer(np.linspace(0, 1, 7), lat * sgn * .012 + fwd * .024) + np.outer(np.linspace(0, 1, 7) ** 2, [0, 0, -.004])
            pp, tt = tube(end, .0016, 5)
            out.append(Garment(pp, tt, fabric=LACE, roles=('Kit_Shoe',), kind='lace'))
    return out


def build_socks(body, P):
    out = {}
    for sd in (-1, 1):
        out['Kit_Sock_' + ('L' if sd < 0 else 'R')] = sock(body, P, sd)
    return out


def sock(body, P, sd):
    fm = (body.footL if sd < 0 else body.footR) > .3
    shin = (body.shinL if sd < 0 else body.shinR) > .5
    p = body.pos
    region = (fm | shin) & (p[:, 2] < P['top'] + .03)
    t = body.tri[region[body.tri].all(1)]
    r = G.compact(p, t, body.W); r = G.largest_component(r[0], r[1], r[2])
    pos, tri = r[0], r[1]
    x = G.solve_shell(body, pos, tri, np.full(len(pos), P['clear']), iters=12, lam=.4, mu=-.42)
    g = Garment(x, tri, fabric=PIQUE, roles=('Kit_Sock',), W=r[2])
    g.F['proj'][:] = 3 if sd < 0 else 4
    g.A['f_top'] = P['top'] - g.pos[:, 2]
    g.cut(g.A['f_top']); g.largest()
    if P.get('bottom') is not None:          # inside the shoe only the top few cm of the sock can ever be seen
        g.cut(g.pos[:, 2] - P['bottom']); g.largest()
        heel, fwd, lat, L = foot_frame(body, sd)
        q = to_frame(g.pos, heel, fwd, lat)
        g.cut(.42 * L - q[:, 1]); g.largest()
    cuff = P.get('cuff', .02)
    for v in [cuff, cuff - .001]: g.insert(g.A['f_top'] - v)
    m = g.A['f_top'] < cuff
    g.A['lift'][m] = .0007
    g.F['fabric'][(g.A['f_top'][g.tri].mean(1) < cuff)] = RIB
    from garment import smooth_borders
    smooth_borders(g)
    shell = solidify(g, P.get('thickness', .0015), facing=.02)
    if P.get('pompom'):
        heel, fwd, lat, L = foot_frame(body, sd)
        tree = parts.bvh(g.pos, g.tri)
        c = heel + np.array([0, 0, P['top'] - .012]) - fwd * .05
        rr = tree.ray_cast(parts.Vector(c), parts.Vector(fwd))
        if rr[0] is not None:
            ctr = np.array(rr[0]) - fwd * P['pompom'] * .8
            shell.merge(ball(ctr, P['pompom']))
    return shell


def ball(c, r, n=12):
    pos = []; tri = []
    for i in range(n + 1):
        th = math.pi * i / n
        for j in range(2 * n):
            ph = 2 * math.pi * j / (2 * n)
            # fluffy: a little lumpy radius
            rr = r * (1 + .06 * math.sin(5 * ph) * math.sin(4 * th))
            pos.append(c + rr * np.array([math.sin(th) * math.cos(ph), math.sin(th) * math.sin(ph), math.cos(th)]))
    m = 2 * n
    for i in range(n):
        for j in range(m):
            a, b, cc, d = i * m + j, (i + 1) * m + j, (i + 1) * m + (j + 1) % m, i * m + (j + 1) % m
            tri.append((a, b, cc)); tri.append((a, cc, d))
    pos = np.array(pos); tri = np.array(tri, np.int64)
    tri = parts.orient_outward(pos, tri, lambda q: np.tile(c, (len(q), 1)))
    g = Garment(pos, tri, fabric=PIQUE, roles=('Kit_Sock',), kind='pompom')
    g.A['cavity'][:] = .85
    return g
