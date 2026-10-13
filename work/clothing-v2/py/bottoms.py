"""Bottoms: shorts, trousers, under-shorts (one 'pants' builder) and the pleated skirt. Shell = pelvis + legs of the real body ->
smooth envelope: the seat is the hull of the hips, each leg hangs from its widest point (straight fall / taper), never the thigh muscles."""
import math
import numpy as np
import glib as G
from garment import Garment, solidify, TWILL, RIB, PIQUE


def smoothstep_np(a, b, x):
    t = np.clip((x - a) / (b - a), 0, 1); return t * t * (3 - 2 * t)


def leg_frame(body, sd):
    s = 'Left' if sd < 0 else 'Right'
    hip = body.head(s + 'UpperLeg'); knee = body.head(s + 'LowerLeg'); ankle = body.head(s + 'Foot')
    ax = ankle - hip; ax /= np.linalg.norm(ax)
    return hip, ax, knee, ankle


def pelvis_axis(body):
    zs = np.array([body.head(b)[2] for b in ['Hips', 'Spine', 'Chest']])
    ys = np.array([body.head(b)[1] for b in ['Hips', 'Spine', 'Chest']])
    return zs, ys


def crotch_z(body):
    p = body.pos
    m = (np.abs(p[:, 0]) < .012) & (p[:, 2] > .5) & (p[:, 2] < 1.0)
    return p[m, 2].min()


def pants_region(body, z_top, s_hem, margin=.05):
    p = body.pos
    legside = np.where(body.legL > .5, -1, np.where(body.legR > .5, 1, 0)).astype(np.float64)
    s = np.zeros(len(p))
    for sd in (-1, 1):
        hip, ax, _, _ = leg_frame(body, sd)
        m = legside == sd
        s[m] = (p[m] - hip) @ ax
    ok = (p[:, 2] < z_top) & (body.armL < .3) & (body.armR < .3) & (body.handL < .2) & (body.handR < .2)
    ok &= ~((legside != 0) & (s > s_hem + margin)) & (body.footL < .3) & (body.footR < .3)
    t = body.tri[ok[body.tri].all(1)]
    r = G.compact(p, t, legside, body.W)
    r = G.largest_component(r[0], r[1], r[2], r[3])
    pos_, tri_, side_, W_ = G.fill_tiny_holes(r[0], r[1], .035, r[2], r[3])
    body._last_W = W_
    return pos_, tri_, np.round(side_)


def pants_shell(body, P):
    zc = crotch_z(body)
    pos, tri, legside = pants_region(body, P['z_waist_back'] + .04, P['s_hem'])
    if P.get('extend_to'):
        pos, tri, legside = extend_leg_tubes(body, pos, tri, legside, P['extend_to'])
    if P.get('subdivide'): pos, tri, legside = G.subdivide(pos, tri, legside); legside = np.round(legside)
    zs, ys = pelvis_axis(body)
    pel_origin = lambda s: np.stack([np.zeros_like(s), np.interp(s, zs, ys), s], 1)
    up = np.array([0, 0, 1.]); fwd = np.array([0, -1., 0])
    bmask = (body.armL < .3) & (body.armR < .3) & (body.handL < .2) & (body.handR < .2)
    penv, pframe = G.build_envelope(body, pel_origin, up, fwd, zc + .01, P['z_waist_back'] + .03, .01, bmask)
    penv.smooth(iters=3)
    if P.get('g_seat'):
        # PATTERN FIRST: seat girth of the garment's own cut (a plumb section from the crotch to the hip line), waist hugging (band)
        import copy
        raw = copy.deepcopy(penv)
        zs_ = P.get('z_seat', zc + .08); c0 = G.hull_centre(raw, zs_); a0 = G.hull_aspect(raw, zs_)
        zw = P['z_waist_front']
        jw = int(np.clip(round((zw - raw.s0) / raw.ds), 0, raw.R.shape[1] - 1))
        rw = raw.R[:, jw]; gw = np.linalg.norm(np.diff(np.stack([rw * np.cos(raw.th), rw * np.sin(raw.th)], 1), axis=0), axis=1).sum() + .02
        penv = G.pattern_envelope(raw, lambda s_: float(np.interp(s_, [zc, zs_, zw - .03, zw + .02], [P['g_seat'], P['g_seat'], gw + .03, gw])),
                                  lambda s_: a0, lambda s_: c0, P['clear'] + .002, smooth=2)
    elif P.get('seat_hang', 0) > 0:     # skirts / loose shorts: the seat hangs straight from the hip down to the crotch
        penv.sweep(int(round((P['z_seat'] - penv.s0) / penv.ds)), -1, P['seat_hang']).smooth(iters=1)
    legs = {}
    for sd in (-1, 1):
        hip, ax, knee, ankle = leg_frame(body, sd)
        lm = ((body.legL if sd < 0 else body.legR) > .5) & bmask
        o_fn = (lambda hip, ax: (lambda s: hip[None, :] + np.outer(s, ax)))(hip, ax)
        s0 = (np.array([0, 0, zc]) - hip) @ ax
        lenv, lframe = G.build_envelope(body, o_fn, ax, fwd, s0 - .02, P['s_hem'] + .05, .01, lm, nth=120)
        lenv.smooth(iters=2)
        if P.get('g_thigh'):
            # PATTERN FIRST: each leg is the garment's own tube (thigh -> knee -> hem girths) on a straight centre line; the leg only pushes it out
            sk = (knee - hip) @ ax
            ca = np.array(G.hull_centre(lenv, s0 + .04)); cb = np.array(G.hull_centre(lenv, min(P['s_hem'], sk)))
            s_a, s_b = s0 + .04, min(P['s_hem'], sk)
            cfun = (lambda ca, cb, s_a, s_b: (lambda s_: tuple(ca + (cb - ca) * float(np.clip((s_ - s_a) / max(1e-6, s_b - s_a), 0, 1)))))(ca, cb, s_a, s_b)
            gs = [s0 - .03, s0 + .03, sk, P['s_hem'] + .06]
            gg = [P['g_thigh'], P['g_thigh'], P.get('g_knee', P['g_hem']), P['g_hem']]
            lenv = G.pattern_envelope(lenv, (lambda gs, gg: (lambda s_: float(np.interp(s_, gs, gg))))(gs, gg), lambda s_: 1.04, cfun, P['clear'] + .002, smooth=2)
        else:
            j0 = int(round((P.get('s_hang', s0 + .03) - lenv.s0) / lenv.ds))
            lenv.sweep(j0, 1, P['leg_slope']).smooth(iters=2)
        legs[sd] = (lenv, lframe, o_fn, hip, ax, s0)

    # flat front panel: the front of the pelvis hangs from the belly / waistband (no tent over the crotch, no groin creases)
    ff = None
    if P.get('flat_front'):
        zb = np.arange(zc - .08, P['z_waist_back'] + .04, .01)
        bp = body.pos
        mid = (np.abs(bp[:, 0]) < .10) & bmask & (bp[:, 1] < 0)
        yf = np.array([bp[mid & (np.abs(bp[:, 2] - z) < .006), 1].min() if (mid & (np.abs(bp[:, 2] - z) < .006)).any() else np.nan for z in zb])
        good = ~np.isnan(yf); yf = np.interp(zb, zb[good], yf[good])
        for j in range(len(zb) - 2, -1, -1): yf[j] = min(yf[j], yf[j + 1] + P.get('front_slope', .25) * .01)
        ff = (zb, yf)

    def env(x):
        z = x[:, 2]
        if ff is not None:
            zb, yf = ff
            yt = np.interp(z, zb, yf) - P['clear'] - P['ease_seat']
            w = (1 - smoothstep_np(.075, .115, np.abs(x[:, 0]))) * (x[:, 1] < -.02) * (z < P['z_waist_back'] + .02)
            w *= smoothstep_np(zc + .005, zc + .045, z)
            x = x.copy(); m = x[:, 1] > yt
            x[m, 1] = x[m, 1] + (yt[m] - x[m, 1]) * w[m]
        top = (legside == 0) & (z > zc + .015)
        bl = np.clip((z - (zc + .015)) / .03, 0, 1)
        x = G.apply_envelope(x, top, penv, pel_origin, pframe, np.zeros(3), P['ease_seat'], bl)
        for sd, (lenv, lframe, o_fn, hip, ax, s0) in legs.items():
            s = (x - hip) @ ax
            m = (legside == sd) & (s > s0 - .01)
            b = np.clip((s - (s0 - .01)) / .04, 0, 1)
            x = G.apply_envelope(x, m, lenv, o_fn, lframe, hip, P['ease_leg'], b)
            # keep the two legs apart at the inseam
            m2 = legside == sd
            x[m2, 0] = sd * np.maximum(sd * x[m2, 0], P.get('inseam_gap', .003))
        return x

    clear = np.full(len(pos), P['clear'])
    obs = []
    for (piece, cl, zmin, zmax) in P.get('over', []):
        hero_id = body.hero.id
        o = G.obstacle(hero_id, piece, cl)
        if o is not None: obs.append((o[0], o[1], (pos[:, 2] > zmin) & (pos[:, 2] < zmax)))
    x = G.solve_shell(body, pos, tri, clear, iters=P.get('iters', 40), env=env, obstacles=obs)
    x = G.relax_tangential(x, tri, 8)
    x = G.solve_shell(body, pos, tri, clear, iters=10, env=env, start=x, obstacles=obs)
    return x, tri, legside, zc


def extend_leg_tubes(body, pos, tri, legside, z_end, rings=7):
    """continue each leg's bottom border straight down (rings) to z_end: the trouser leg falls past the ankle over the shoe."""
    loops = G.boundary_loops(tri)
    P_, T_, S_, W_ = [pos], [tri], [legside], [body._last_W]
    n = len(pos)
    for l in loops:
        if pos[l, 2].mean() > .4: continue
        sd = np.sign(pos[l, 0].mean())
        prev = l
        z0 = pos[l, 2].mean()
        for k in range(1, rings + 1):
            q = pos[l].copy()
            q[:, 2] = z0 + (z_end - z0) * k / rings + (pos[l, 2] - z0) * (1 - k / rings)
            ids = np.arange(n, n + len(l)); n += len(l)
            P_.append(q); S_.append(np.full(len(l), sd)); W_.append(body._last_W[l])
            st = G.ring_strip(prev, ids)
            # orient like the neighbouring shell (outward)
            T_.append(st); prev = ids
    pos2 = np.concatenate(P_); tri2 = np.concatenate(T_); side2 = np.concatenate(S_); body._last_W = np.concatenate(W_)
    # fix orientation of the added strips against the existing triangles' normals
    fn = G.face_normals(pos2, tri2); c = pos2[tri2].mean(1)
    added = np.arange(len(tri), len(tri2))
    for sd in (-1, 1):
        hip, ax, knee, ankle = leg_frame(body, sd)
        m = added[np.sign(c[added, 0]) == sd]
        if len(m) == 0: continue
        radial = c[m] - (ankle + np.outer((c[m] - ankle) @ ax, ax))
        flip = np.einsum('ij,ij->i', fn[m], radial) < 0
        tri2[m[flip]] = tri2[m[flip]][:, ::-1]
    return pos2, tri2, side2


def leg_field(body, g, sd, s_hem):
    hip, ax, _, _ = leg_frame(body, sd)
    s = (g.pos - hip) @ ax
    on = np.abs(g.A['legside'] - sd) < .5
    f = np.where(on, s_hem - s, .1)
    return G.smooth_field(f, g.tri, 10, fixed=on)


def pants_theta(body, x):
    zs, ys = pelvis_axis(body)
    yc = np.interp(x[:, 2], zs, ys)
    return np.arctan2(x[:, 0], -(x[:, 1] - yc)), np.hypot(x[:, 0], x[:, 1] - yc)


def build_pants(body, P):
    import parts
    x, tri, legside, zc = pants_shell(body, P)
    fab = P.get('fabric', TWILL)
    g = Garment(x, tri, fabric=fab, roles=(P.get('role', 'Kit_Shorts'),), legside=legside, W=body._last_W)
    th, r = pants_theta(body, g.pos)
    zw = P['z_waist_front'] + (P['z_waist_back'] - P['z_waist_front']) * (1 - np.cos(th)) * .5
    g.A['f_waist'] = zw - g.pos[:, 2]
    g.cut(g.A['f_waist'])
    for sd in (-1, 1):
        if P.get('hem_z_front') is not None:
            hip, ax, knee, ankle = leg_frame(body, sd)
            on = np.abs(g.A['legside'] - sd) < .5
            fy = np.clip(-(g.pos[:, 1] - ankle[1]) / .08, -1, 1) * .5 + .5          # 1 at the front of the leg, 0 at the back
            zh = P['hem_z_back'] + (P['hem_z_front'] - P['hem_z_back']) * fy
            g.A['f_leg%d' % sd] = np.where(on, g.pos[:, 2] - zh, .5)
        else:
            g.A['f_leg%d' % sd] = leg_field(body, g, sd, P['s_hem'])
        g.cut(g.A['f_leg%d' % sd])
    # side slits at the outer seam
    if P.get('slit', 0) > 0:
        for sd in (-1, 1):
            hip, ax, _, _ = leg_frame(body, sd)
            f = g.A['f_leg%d' % sd]
            outdir = np.array([sd, 0, 0.]); outdir -= ax * (outdir @ ax); outdir /= np.linalg.norm(outdir)
            fw = np.cross(ax, outdir)
            q = g.pos - hip; q -= np.outer(q @ ax, ax)
            ang = np.arctan2(q @ fw, q @ outdir); rr = np.linalg.norm(q, axis=1)
            d = np.abs(ang) * rr
            hw = P['slit_w'] * np.clip(1 - f / P['slit'], 0, 1)
            on = np.abs(g.A['legside'] - sd) < .5
            g.cut(np.where(on, d - hw + 1e-5 * (f > P['slit']), 1.0))
    g.largest()
    # waistband
    wb = P.get('band', .038)
    for v in [wb, wb - .0015, .006]:
        g.insert(g.A['f_waist'] - v)
    band = g.A['f_waist'] < wb - 1e-4
    g.A['lift'][band] = P.get('band_lift', .0012) * np.clip((wb - g.A['f_waist'][band]) / .0015, 0, 1)
    if P.get('band_rib'):
        g.F['fabric'][(g.A['f_waist'][g.tri].mean(1) < wb)] = RIB
    # hems
    hem = P.get('hem', .025)
    for sd in (-1, 1):
        for v in [hem, hem - .0012]:
            g.insert(g.A['f_leg%d' % sd] - v)
    for sd in (-1, 1):
        f = g.A['f_leg%d' % sd]; on = np.abs(g.A['legside'] - sd) < .5
        m = on & (f < hem)
        g.A['lift'][m] = np.maximum(g.A['lift'][m], .0006)
    lf = g.A['legside'][g.tri].mean(1)
    g.F['proj'] = np.where(lf < -.5, 3, np.where(lf > .5, 4, 0)).astype(np.int32)
    from garment import smooth_borders
    smooth_borders(g)
    shell = solidify(g, P.get('thickness', .0025), facing=.035)
    extras = []
    if P.get('drawcord'):
        extras += drawcord(body, g, P)
    if P.get('loops'):
        extras += belt_loops(body, g, P)
    for e in extras:
        e.F['part'][:] = 3          # decoration: not part of the surface other garments rest on
        shell.merge(e)
    return {P.get('piece', 'Kit_Bottom'): shell}


def tube(path, radius, n=10, closed_ends=True):
    """round tube along a polyline (ends capped)."""
    path = np.asarray(path); m = len(path)
    t = np.gradient(path, axis=0); t /= np.linalg.norm(t, axis=1, keepdims=True)
    a = np.cross(t, [0, 0, 1.]); bad = np.linalg.norm(a, axis=1) < 1e-6; a[bad] = np.cross(t[bad], [1, 0, 0.])
    a /= np.linalg.norm(a, axis=1, keepdims=True); b = np.cross(t, a)
    r = np.broadcast_to(np.asarray(radius, np.float64), (m,))
    ph = np.linspace(0, 2 * math.pi, n, endpoint=False)
    ring = path[:, None, :] + r[:, None, None] * (np.cos(ph)[None, :, None] * a[:, None, :] + np.sin(ph)[None, :, None] * b[:, None, :])
    pos = ring.reshape(-1, 3)
    import parts
    tri = parts.grid_tri(m, n, closed_i=False)
    # grid_tri expects [i*nj + j] with i along path, j around: wrap around j
    out = []
    for i in range(m - 1):
        for j in range(n):
            j2 = (j + 1) % n
            a_, b_, c_, d_ = i * n + j, (i + 1) * n + j, (i + 1) * n + j2, i * n + j2
            out.append((a_, b_, c_)); out.append((a_, c_, d_))
    tri = np.array(out, np.int64)
    if closed_ends:
        c0 = len(pos); c1 = c0 + 1
        pos = np.concatenate([pos, path[:1] - t[:1] * r[0] * .6, path[-1:] + t[-1:] * r[-1] * .6])
        cap = [(c0, (j + 1) % n, j) for j in range(n)] + [(c1, (m - 1) * n + j, (m - 1) * n + (j + 1) % n) for j in range(n)]
        tri = np.concatenate([tri, np.array(cap, np.int64)])
    # orientation: outward from the path
    fn = G.face_normals(pos, tri[:2 * n]); cc = pos[tri[:2 * n]].mean(1)
    if np.mean(np.einsum('ij,ij->i', fn, cc - path[0])) < 0: tri = tri[:, ::-1]
    return pos, tri


def drawcord(body, g, P):
    import parts
    tree = parts.bvh(g.pos, g.tri)
    out = []
    z0 = P['z_waist_front'] - .016
    for sx in (-1, 1):
        x0 = sx * .011
        r = tree.ray_cast(parts.Vector((x0, -1.0, z0)), parts.Vector((0, 1, 0)))
        if r[0] is None: continue
        p0 = np.array(r[0]) + np.array([0, -.003, 0])
        L = P.get('cord_len', .085)
        k = np.linspace(0, 1, 16)
        path = p0 + np.stack([sx * (.004 + .012 * k ** 1.5), -.004 * np.sin(k * math.pi) - .002 * k, -L * k], 1)
        # keep it in front of the shorts
        path = parts.push_out(tree, path, .0045)
        rad = np.full(len(k), .0021); rad[-3:] = .0027
        pos, tri = tube(path, rad, 10)
        cg = Garment(pos, tri, fabric=5, roles=(P.get('cord_role', 'Kit_ShortsBand'),), kind='cord')
        cg.A['trim'][:] = 0
        out.append(cg)
    return out


def belt_loops(body, g, P):
    import parts
    tree = parts.bvh(g.pos, g.tri)
    out = []
    zt = P['z_waist_front']; h = P.get('band', .038) + .006
    for a in P['loop_angles']:
        th = math.radians(a)
        zs, ys = pelvis_axis(body)
        yc = np.interp(zt, zs, ys)
        d = np.array([math.sin(th), -math.cos(th), 0])
        rows = []
        for z in np.linspace(zt - h, zt + .002, 9):
            rr = tree.ray_cast(parts.Vector(np.array([0, yc, z]) + d * .6), parts.Vector(-d))
            c = np.array(rr[0]) if rr[0] is not None else np.array([0, yc, z]) + d * .17
            n = np.array(rr[1]) if rr[0] is not None else d
            side = np.cross(n, [0, 0, 1.]); side /= np.linalg.norm(side)
            lift = P.get('loop_lift', .0028) * math.sin(math.pi * np.clip((z - (zt - h)) / h, 0, 1)) ** .4 + .0012
            rows.append([c + n * lift + side * w for w in np.linspace(-.0055, .0055, 3)])
        pos = np.array(rows).reshape(-1, 3)
        tri = parts.grid_tri(9, 3)
        tri = parts.orient_outward(pos, tri, lambda q: np.tile(np.array([0, yc, zt]), (len(q), 1)))
        lg = Garment(pos, tri, fabric=g.F['fabric'][0], roles=g.roles[:1], kind='loop')
        out.append(solidify(lg, .0018, facing=None))
    return out


def skirt(body, P, under=None):
    """Knife-pleated skirt: a flared cone from the waistband to the hem, clear of the hips, thighs, the under-shorts and the hanging hands.
    Pleats are a sawtooth in angle (sharp fold, flat face) that open from the hip line to the hem."""
    import parts
    zs, ys = pelvis_axis(body)
    yc = lambda z: np.interp(z, zs, ys)
    up = np.array([0, 0, 1.]); fwd = np.array([0, -1., 0])
    origin = lambda s: np.stack([np.zeros_like(s), yc(s), s], 1)
    bmask = (body.armL < .3) & (body.armR < .3) & (body.handL < .2) & (body.handR < .2)
    z_top = P['z_top']; z_hem = P['z_hem']
    env, frame = G.build_envelope(body, origin, up, fwd, z_hem - .05, z_top + .01, .01, bmask, nth=240)
    env.smooth(iters=2)
    # hang: the skirt falls from the widest hip line, then flares
    if P.get('g_hem'):
        import copy
        raw = copy.deepcopy(env)
        c0 = G.hull_centre(raw, P['z_hip']); a0 = G.hull_aspect(raw, P['z_hip'])
        jt = int(np.clip(round((z_top - raw.s0) / raw.ds), 0, raw.R.shape[1] - 1))
        rt = raw.R[:, jt]; gt = np.linalg.norm(np.diff(np.stack([rt * np.cos(raw.th), rt * np.sin(raw.th)], 1), axis=0), axis=1).sum() + .02
        env = G.pattern_envelope(raw, lambda s_: float(np.interp(s_, [z_hem - .05, z_hem, z_top, z_top + .02], [P['g_hem'], P['g_hem'], gt, gt])),
                                 lambda s_: a0 * .9 + .1, lambda s_: c0, .004, smooth=2)
    else:
        j_hip = int(round((P['z_hip'] - env.s0) / env.ds))
        env.sweep(j_hip, -1, -P['flare'])                      # negative slope = grows going down (flare)
        env.smooth(iters=2)
    # under-shorts / other garments inside: their hull too
    if under is not None:
        uenv, _ = G.build_envelope(type('B', (), {'pos': under.pos, 'tri': under.tri})(), origin, up, fwd, z_hem - .05, z_top + .01, .01, None, nth=240)
        env.R = np.maximum(env.R, uenv.R + P.get('under_clear', .004))
    N = P['pleats']; per = P.get('cols_per_pleat', 8)
    ncol = N * per
    th = np.linspace(-math.pi, math.pi, ncol, endpoint=False) + math.pi / ncol
    nrow = P.get('rows', 36)
    rows = []
    zf = lambda thv: z_hem + P.get('hem_side_rise', .012) * np.abs(np.sin(thv))      # curved hem: a touch shorter at the sides
    for k in range(nrow):
        v = k / (nrow - 1)                                    # 0 = top, 1 = hem
        zt = z_top
        z = zt + (zf(th) - zt) * v
        r = env(th, z) + P['ease']
        # pleat profile: sawtooth in the pleat's own angle; depth opens from 0 at the stitched-down line to full at the hem
        u = (th + math.pi) / (2 * math.pi) * N
        fr = u - np.floor(u)
        open_ = np.clip((z_top - z - P['stitch_down']) / max(1e-3, (z_top - z_hem - P['stitch_down'])), 0, 1) ** .8
        depth = P['pleat_depth'] * (.25 + .75 * open_)
        saw = np.where(fr < .12, fr / .12, 1 - (fr - .12) / .88)      # sharp rise at the fold, long flat face
        r = r + depth * saw - depth * .5
        p = np.stack([r * np.sin(th), yc(z) - r * np.cos(th), z], 1)
        rows.append(p)
    R = np.stack(rows, 1)                                           # (ncol, nrow, 3)
    pos = R.reshape(-1, 3)
    tri = parts.grid_tri(ncol, nrow, closed_i=True)
    tri = parts.orient_outward(pos, tri, lambda q: np.stack([np.zeros(len(q)), yc(q[:, 2]), q[:, 2]], 1))
    g = Garment(pos, tri, fabric=P.get('fabric', TWILL), roles=(P.get('role', 'Kit_Shorts'),), kind='skirt')
    fold = np.repeat(np.where(((th + math.pi) / (2 * math.pi) * N) % 1 < .12, 1., 0.), nrow)
    g.A['pleat_fold'] = fold
    rr = np.linalg.norm(R[:, :, :2] - np.array([0, 0]), axis=2).mean()
    g.A['pu'] = np.repeat((th + math.pi) * .19, nrow); g.A['pv'] = R[:, :, 2].reshape(-1)
    g.F['proj'][:] = 6
    g.A['f_hem'] = np.tile(np.linspace(1, 0, nrow), ncol) * (z_top - z_hem)
    # keep it clear of the body (push out where the hands or thighs reach)
    x = G.solve_shell(body, pos, tri, np.full(len(pos), P.get('clear', .006)), iters=0, start=pos)
    g.pos = x
    return g


def build_skort(body, P):
    """tennis / golf skort: tight under-shorts + pleated skirt + rib waistband (Kit_Bottom), all one piece."""
    U = dict(P['under']); U.setdefault('piece', 'Kit_Bottom')
    under = build_pants(body, U)['Kit_Bottom']
    under.F['kind'][under.F['kind'] == 0] = 18
    sk = skirt(body, P['skirt'], under=under)
    shell = solidify(sk, P['skirt'].get('thickness', .0018), facing=None)
    under.merge(shell)
    if P.get('band'):
        bd = waistband(body, P['band'], inside=[under])
        under.merge(bd)
    return {'Kit_Bottom': under}


def waistband(body, B, inside=()):
    """a separate band (rib or twill) around the waist from z_bot to z_top, outside everything listed in `inside`."""
    import parts
    zs, ys = pelvis_axis(body)
    yc = lambda z: np.interp(z, zs, ys)
    up = np.array([0, 0, 1.]); fwd = np.array([0, -1., 0])
    origin = lambda s: np.stack([np.zeros_like(s), yc(s), s], 1)
    bmask = (body.armL < .3) & (body.armR < .3) & (body.handL < .2) & (body.handR < .2)
    env, frame = G.build_envelope(body, origin, up, fwd, B['z_bot'] - .01, B['z_top'] + .01, .005, bmask, nth=240)
    for g in inside:
        e2, _ = G.build_envelope(type('B', (), {'pos': g.pos, 'tri': g.tri})(), origin, up, fwd, B['z_bot'] - .01, B['z_top'] + .01, .005, None, nth=240)
        env.R = np.maximum(env.R, e2.R + .001)
    env.smooth(iters=3)
    ncol, nrow = B.get('ncol', 128), 6
    th = np.linspace(-math.pi, math.pi, ncol, endpoint=False)
    rows = []
    for k in range(nrow):
        z = B['z_bot'] + (B['z_top'] - B['z_bot']) * k / (nrow - 1)
        r = env(th, np.full(ncol, z)) + B.get('ease', .003)
        rows.append(np.stack([r * np.sin(th), yc(z) - r * np.cos(th), np.full(ncol, z)], 1))
    R = np.stack(rows, 1); pos = R.reshape(-1, 3)
    tri = parts.grid_tri(ncol, nrow, closed_i=True)
    tri = parts.orient_outward(pos, tri, lambda q: np.stack([np.zeros(len(q)), yc(q[:, 2]), q[:, 2]], 1))
    g = Garment(pos, tri, fabric=B.get('fabric', RIB), roles=(B.get('role', 'Kit_Shorts'),), kind='belt' if B.get('role') == 'Kit_Belt' else 'band')
    g.A['band_v'] = np.tile(np.linspace(0, 1, nrow), ncol)
    g.A['pu'] = np.repeat((th + math.pi) * .17, nrow); g.A['pv'] = R[:, :, 2].reshape(-1); g.F['proj'][:] = 6
    return solidify(g, B.get('thickness', .003), facing=None)


def build_belt(body, P):
    """leather belt round the waistband (between the band and the belt loops), metal buckle at the front, the tip end lapping past it."""
    import parts
    surf = G.load_surface(body.hero.id, P['over'])
    class S: pass
    inner = S(); inner.pos, inner.tri = surf
    B = dict(z_bot=P['z_mid'] - P['width'] / 2, z_top=P['z_mid'] + P['width'] / 2, ease=P.get('gap', .0008), thickness=P['thickness'],
             fabric=8, role='Kit_Belt')
    belt = waistband(body, B, inside=[inner])
    belt.A['belt'] = np.ones(len(belt.pos))
    zs, ys = pelvis_axis(body)
    yc = np.interp(P['z_mid'], zs, ys)
    tree = parts.bvh(belt.pos, belt.tri)
    def front_point(xoff, z):
        r = tree.ray_cast(parts.Vector((xoff, -1.0, z)), parts.Vector((0, 1, 0)))
        return (np.array(r[0]), np.array(r[1])) if r[0] is not None else (np.array([xoff, yc - .17, z]), np.array([0, -1., 0]))
    # tip end: an overlapping strap from the buckle toward the hero's left (-x), a touch outside the belt
    x0 = P.get('buckle_x', .0)
    rows = []
    xs = np.linspace(x0, x0 - P['tip_len'], 14)
    for xx in xs:
        row = []
        for z in np.linspace(P['z_mid'] - P['width'] / 2 + .002, P['z_mid'] + P['width'] / 2 - .002, 4):
            p, n = front_point(xx, z)
            row.append(p + n * (P['thickness'] + .0006))
        rows.append(row)
    pos = np.array(rows).reshape(-1, 3); tri = parts.grid_tri(len(xs), 4)
    tri = parts.orient_outward(pos, tri, lambda q: np.tile(np.array([0, yc, P['z_mid']]), (len(q), 1)))
    tip = Garment(pos, tri, fabric=8, roles=('Kit_Belt',), kind='tip')
    tip.A['belt_hole'] = np.repeat(np.linspace(0, P['tip_len'], 14), 4)
    belt.merge(solidify(tip, P['thickness'] * .85, facing=None))
    # buckle: rectangular frame + centre bar + prong
    c, n = front_point(x0 + .002, P['z_mid'])
    up = np.array([0, 0, 1.]); side = np.cross(up, n); side /= np.linalg.norm(side)
    W, H, bar, dep = P['buckle_w'], P['buckle_h'], P['buckle_bar'], P['buckle_depth']
    base = c + n * (P['thickness'] + .0012)
    def box(cen, a, b, d):
        """rounded-ish box: 8 corners, extruded along n."""
        corners = []
        for k in (0, 1):
            for (u, v) in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
                corners.append(cen + side * u * a + up * v * b + n * (k * d))
        q = np.array(corners)
        f = [(0, 1, 2), (0, 2, 3), (4, 6, 5), (4, 7, 6), (0, 4, 5), (0, 5, 1), (1, 5, 6), (1, 6, 2), (2, 6, 7), (2, 7, 3), (3, 7, 4), (3, 4, 0)]
        return q, np.array(f, np.int64)
    parts_ = []
    for (cx, cy, a, b) in [(0, (H - bar) / 2, W / 2, bar / 2), (0, -(H - bar) / 2, W / 2, bar / 2), (-(W - bar) / 2, 0, bar / 2, H / 2), ((W - bar) / 2, 0, bar / 2, H / 2), (-W * .12, 0, bar * .35, H / 2 - bar * .5)]:
        q, f = box(base + side * cx + up * cy, a, b, dep)
        f = parts.orient_outward(q, f, lambda v: np.tile(base + side * cx + up * cy + n * dep * .5, (len(v), 1)))
        parts_.append(Garment(q, f, fabric=8, roles=('Kit_Buckle',), kind='buckle'))
    for pc in parts_: belt.merge(pc)
    return {'Kit_Belt': belt}
